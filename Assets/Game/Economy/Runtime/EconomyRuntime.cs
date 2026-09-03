using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Time;
using UnityEngine;

namespace MSC.Economy
{
    [DisallowMultipleComponent]
    public sealed class EconomyRuntime : MonoBehaviour,
        IEconomyTransactionService,
        IEconomyTransactionFeedbackSource
    {
        private readonly Dictionary<string, EconomyPriceScopeStateDto>
            priceScopes = new(StringComparer.Ordinal);
        private readonly List<EconomyTransactionRecordDto> ledger = new();
        private readonly Dictionary<string, EconomyTransactionRecordDto>
            recordsById = new(StringComparer.Ordinal);

        private EconomyPriceCatalog catalog;
        private IGameTimeService gameTime;
        private IDisposable timeSubscription;
        private long balanceMinorUnits;
        private long nextSequence = 1;
        private long lastProcessedDayIndex;
        private ulong revision;
        private bool initialized;

        public event Action<EconomySnapshot> StateChanged;
        public event Action<EconomyTransactionRejected> TransactionRejected;

        public EconomySnapshot Snapshot => new(
            revision,
            balanceMinorUnits,
            ledger.Count);

        public bool IsInitialized => initialized;
        public EconomyPriceCatalog Catalog => catalog;
        public IReadOnlyList<EconomyTransactionRecordDto> Ledger => ledger;

        public void Initialize(
            EconomyPriceCatalog configuredCatalog,
            IGameTimeService configuredGameTime)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Economy runtime is already initialized.");
            }

            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            if (!catalog.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredCatalog));
            }

            ApplyState(CreateFreshState(catalog, gameTime.Snapshot));
            timeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            initialized = true;
        }

        public static EconomyStateDto CreateFreshState(
            EconomyPriceCatalog catalog,
            GameTimeSnapshot snapshot)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryValidate(out string failure))
            {
                throw new ArgumentException(
                    failure,
                    nameof(catalog));
            }

            return new EconomyStateDto
            {
                catalogId = catalog.CatalogId,
                balanceMinorUnits = catalog.InitialBalanceMinorUnits,
                nextSequence = 1,
                lastProcessedDayIndex = snapshot.DayIndex,
                priceScopes = catalog.Scopes.Select(scope =>
                    new EconomyPriceScopeStateDto
                    {
                        scopeId = scope.ScopeId,
                        appliedInflationCycles = 0,
                    }).ToArray(),
                ledger = Array.Empty<EconomyTransactionRecordDto>(),
            };
        }

        public bool TryQuote(
            string priceId,
            int quantity,
            out EconomyPriceQuote quote,
            out EconomyTransactionFailureReason failureReason)
        {
            EnsureInitialized();
            ProcessPriceChangesThrough(gameTime.Snapshot);
            if (quantity <= 0 ||
                !catalog.TryGetPrice(priceId, out EconomyPriceDefinition price) ||
                !priceScopes.TryGetValue(
                    price.ScopeId,
                    out EconomyPriceScopeStateDto scopeState) ||
                !catalog.TryGetScope(
                    price.ScopeId,
                    out EconomyPriceScopeDefinition scope))
            {
                quote = default;
                failureReason = quantity <= 0
                    ? EconomyTransactionFailureReason.InvalidAmount
                    : EconomyTransactionFailureReason.UnknownPrice;
                return false;
            }

            try
            {
                decimal multiplier = 1m;
                if (scope.Policy ==
                    EconomyPricePolicy.WeeklyAdditiveInflation)
                {
                    multiplier += scopeState.appliedInflationCycles *
                        (scope.InflationBasisPoints / 10_000m);
                }

                long unitPrice = checked((long)decimal.Round(
                    price.BaseMinorUnits * multiplier,
                    0,
                    MidpointRounding.AwayFromZero));
                long total = checked(unitPrice * quantity);
                quote = new EconomyPriceQuote(
                    price.PriceId,
                    quantity,
                    unitPrice,
                    total,
                    scopeState.appliedInflationCycles);
                failureReason = EconomyTransactionFailureReason.None;
                return true;
            }
            catch (OverflowException)
            {
                quote = default;
                failureReason = EconomyTransactionFailureReason.BalanceOverflow;
                return false;
            }
        }

        public bool TryPurchase(
            string transactionId,
            string priceId,
            int quantity,
            string sourceStableId,
            out EconomyTransactionReceipt receipt)
        {
            if (!TryQuote(
                    priceId,
                    quantity,
                    out EconomyPriceQuote quote,
                    out EconomyTransactionFailureReason failure))
            {
                receipt = Failure(transactionId, failure);
                return false;
            }

            return TryCommit(
                new EconomyTransactionRequest(
                    transactionId,
                    EconomyTransactionKind.Purchase,
                    EconomyTransactionDirection.Debit,
                    quote.TotalPriceMinorUnits,
                    sourceStableId,
                    quote.PriceId,
                    quote.Quantity),
                out receipt);
        }

        public bool TryRefund(
            string transactionId,
            string originalTransactionId,
            long amountMinorUnits,
            string sourceStableId,
            out EconomyTransactionReceipt receipt)
        {
            return TryCommit(
                new EconomyTransactionRequest(
                    transactionId,
                    EconomyTransactionKind.Refund,
                    EconomyTransactionDirection.Credit,
                    amountMinorUnits,
                    sourceStableId,
                    relatedTransactionId: originalTransactionId),
                out receipt);
        }

        public bool TryCommit(
            in EconomyTransactionRequest request,
            out EconomyTransactionReceipt receipt)
        {
            EnsureInitialized();
            if (recordsById.TryGetValue(
                    request.TransactionId,
                    out EconomyTransactionRecordDto existing))
            {
                if (Matches(existing, in request))
                {
                    receipt = new EconomyTransactionReceipt(
                        true,
                        true,
                        EconomyTransactionFailureReason.None,
                        existing.transactionId,
                        existing.balanceBeforeMinorUnits,
                        existing.balanceAfterMinorUnits,
                        existing.sequence);
                    return true;
                }

                return Reject(
                    in request,
                    EconomyTransactionFailureReason.DuplicateTransactionConflict,
                    out receipt);
            }

            EconomyTransactionFailureReason validation =
                ValidateRequest(in request);
            if (validation != EconomyTransactionFailureReason.None)
            {
                return Reject(in request, validation, out receipt);
            }

            long balanceAfter;
            try
            {
                balanceAfter = request.Direction ==
                    EconomyTransactionDirection.Debit
                        ? checked(balanceMinorUnits - request.AmountMinorUnits)
                        : checked(balanceMinorUnits + request.AmountMinorUnits);
            }
            catch (OverflowException)
            {
                return Reject(
                    in request,
                    EconomyTransactionFailureReason.BalanceOverflow,
                    out receipt);
            }

            if (balanceAfter < 0)
            {
                return Reject(
                    in request,
                    EconomyTransactionFailureReason.InsufficientFunds,
                    out receipt);
            }

            long sequence = nextSequence;
            try
            {
                nextSequence = checked(nextSequence + 1);
            }
            catch (OverflowException)
            {
                return Reject(
                    in request,
                    EconomyTransactionFailureReason.BalanceOverflow,
                    out receipt);
            }

            var record = new EconomyTransactionRecordDto
            {
                sequence = sequence,
                transactionId = request.TransactionId,
                kind = (int)request.Kind,
                direction = (int)request.Direction,
                amountMinorUnits = request.AmountMinorUnits,
                balanceBeforeMinorUnits = balanceMinorUnits,
                balanceAfterMinorUnits = balanceAfter,
                sourceStableId = request.SourceStableId,
                priceId = request.PriceId,
                quantity = request.Quantity,
                relatedTransactionId = request.RelatedTransactionId,
                gameTimeTicks = GetCurrentGameTimeTicks(),
            };
            balanceMinorUnits = balanceAfter;
            ledger.Add(record);
            recordsById.Add(record.transactionId, record);
            revision++;
            receipt = new EconomyTransactionReceipt(
                true,
                false,
                EconomyTransactionFailureReason.None,
                record.transactionId,
                record.balanceBeforeMinorUnits,
                record.balanceAfterMinorUnits,
                record.sequence);
            StateChanged?.Invoke(Snapshot);
            return true;
        }

        public EconomyStateDto CaptureDto()
        {
            EnsureInitialized();
            ProcessPriceChangesThrough(gameTime.Snapshot);
            return new EconomyStateDto
            {
                catalogId = catalog.CatalogId,
                balanceMinorUnits = balanceMinorUnits,
                nextSequence = nextSequence,
                lastProcessedDayIndex = lastProcessedDayIndex,
                priceScopes = catalog.Scopes.Select(scope =>
                    priceScopes[scope.ScopeId].DeepClone()).ToArray(),
                ledger = ledger.Select(record => record.DeepClone()).ToArray(),
            };
        }

        public bool TryRestoreDto(EconomyStateDto dto, out string failure)
        {
            return TryRestoreDtoCore(
                dto,
                gameTime?.Snapshot.DayIndex ?? -1L,
                reconcileAgainstLiveClock: true,
                out failure);
        }

        /// <summary>
        /// Restores a save domain while core.time is staged but has not yet
        /// mutated the live clock. The later StateRestored notification performs
        /// the normal deterministic price reconciliation.
        /// </summary>
        public bool TryRestoreDtoForStagedClock(
            EconomyStateDto dto,
            long stagedAuthoritativeDayIndex,
            out string failure)
        {
            return TryRestoreDtoCore(
                dto,
                stagedAuthoritativeDayIndex,
                reconcileAgainstLiveClock: false,
                out failure);
        }

        private bool TryRestoreDtoCore(
            EconomyStateDto dto,
            long authoritativeDayIndex,
            bool reconcileAgainstLiveClock,
            out string failure)
        {
            EnsureInitialized();
            if (dto == null)
            {
                failure = "Economy state is missing.";
                return false;
            }

            if (!dto.TryValidate(catalog, out failure))
            {
                return false;
            }

            if (authoritativeDayIndex < 0L ||
                dto.lastProcessedDayIndex > authoritativeDayIndex)
            {
                failure = "Economy price state is newer than the restored clock.";
                return false;
            }

            ApplyState(dto);
            if (reconcileAgainstLiveClock)
            {
                ProcessPriceChangesThrough(gameTime.Snapshot);
            }

            revision++;
            StateChanged?.Invoke(Snapshot);
            failure = string.Empty;
            return true;
        }

        private EconomyTransactionFailureReason ValidateRequest(
            in EconomyTransactionRequest request)
        {
            if (!StableIdRules.IsCanonical(request.TransactionId) ||
                !StableIdRules.IsCanonical(request.SourceStableId) ||
                !Enum.IsDefined(typeof(EconomyTransactionKind), request.Kind) ||
                !Enum.IsDefined(
                    typeof(EconomyTransactionDirection),
                    request.Direction))
            {
                return EconomyTransactionFailureReason.InvalidRequest;
            }

            if (request.AmountMinorUnits <= 0 || request.Quantity < 0)
            {
                return EconomyTransactionFailureReason.InvalidAmount;
            }

            bool expectedDebit = request.Kind == EconomyTransactionKind.Purchase ||
                                 request.Kind == EconomyTransactionKind.Fine ||
                                 request.Kind == EconomyTransactionKind.ServicePayment ||
                                 request.Kind == EconomyTransactionKind.BillPayment;
            bool expectedCredit = request.Kind == EconomyTransactionKind.Refund ||
                                  request.Kind == EconomyTransactionKind.Reward;
            if (expectedDebit && request.Direction != EconomyTransactionDirection.Debit ||
                expectedCredit && request.Direction != EconomyTransactionDirection.Credit)
            {
                return EconomyTransactionFailureReason.InvalidRequest;
            }

            if (!string.IsNullOrEmpty(request.PriceId) &&
                (!catalog.TryGetPrice(request.PriceId, out _) ||
                 request.Quantity <= 0))
            {
                return EconomyTransactionFailureReason.UnknownPrice;
            }

            if (request.Kind != EconomyTransactionKind.Refund)
            {
                return EconomyTransactionFailureReason.None;
            }

            if (!recordsById.TryGetValue(
                    request.RelatedTransactionId,
                    out EconomyTransactionRecordDto original) ||
                (EconomyTransactionDirection)original.direction !=
                    EconomyTransactionDirection.Debit)
            {
                return EconomyTransactionFailureReason.RefundNotAllowed;
            }

            long refunded = 0;
            try
            {
                for (int index = 0; index < ledger.Count; index++)
                {
                    EconomyTransactionRecordDto record = ledger[index];
                    if ((EconomyTransactionKind)record.kind ==
                            EconomyTransactionKind.Refund &&
                        string.Equals(
                            record.relatedTransactionId,
                            original.transactionId,
                            StringComparison.Ordinal))
                    {
                        refunded = checked(refunded + record.amountMinorUnits);
                    }
                }

                return checked(refunded + request.AmountMinorUnits) <=
                    original.amountMinorUnits
                        ? EconomyTransactionFailureReason.None
                        : EconomyTransactionFailureReason.RefundNotAllowed;
            }
            catch (OverflowException)
            {
                return EconomyTransactionFailureReason.RefundNotAllowed;
            }
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            if (gameTimeEvent.Kind == GameTimeEventKind.DayChanged ||
                gameTimeEvent.Kind == GameTimeEventKind.StateRestored)
            {
                ProcessPriceChangesThrough(gameTimeEvent.Current);
            }
        }

        private void ProcessPriceChangesThrough(GameTimeSnapshot snapshot)
        {
            if (snapshot.DayIndex <= lastProcessedDayIndex)
            {
                return;
            }

            foreach (EconomyPriceScopeDefinition scope in catalog.Scopes)
            {
                if (scope.Policy !=
                    EconomyPricePolicy.WeeklyAdditiveInflation)
                {
                    continue;
                }

                int cycles = CountWeekdays(
                    lastProcessedDayIndex,
                    snapshot.DayIndex,
                    snapshot.Date,
                    scope.RestockDayOfWeek);
                if (cycles <= 0)
                {
                    continue;
                }

                EconomyPriceScopeStateDto state = priceScopes[scope.ScopeId];
                state.appliedInflationCycles = checked(
                    state.appliedInflationCycles + cycles);
            }

            lastProcessedDayIndex = snapshot.DayIndex;
            revision++;
            StateChanged?.Invoke(Snapshot);
        }

        private static int CountWeekdays(
            long previousExclusiveDayIndex,
            long currentInclusiveDayIndex,
            GameDate currentDate,
            DayOfWeek targetDay)
        {
            if (currentInclusiveDayIndex <= previousExclusiveDayIndex)
            {
                return 0;
            }

            DayOfWeek currentDay = new DateTime(
                currentDate.Year,
                currentDate.Month,
                currentDate.Day).DayOfWeek;
            int offsetBack = ((int)currentDay - (int)targetDay + 7) % 7;
            long lastMatchingIndex = currentInclusiveDayIndex - offsetBack;
            if (lastMatchingIndex <= previousExclusiveDayIndex)
            {
                return 0;
            }

            long count = (lastMatchingIndex -
                          (previousExclusiveDayIndex + 1)) / 7 + 1;
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }

        private void ApplyState(EconomyStateDto dto)
        {
            balanceMinorUnits = dto.balanceMinorUnits;
            nextSequence = dto.nextSequence;
            lastProcessedDayIndex = dto.lastProcessedDayIndex;
            priceScopes.Clear();
            foreach (EconomyPriceScopeStateDto scope in dto.priceScopes)
            {
                priceScopes.Add(scope.scopeId, scope.DeepClone());
            }

            ledger.Clear();
            recordsById.Clear();
            foreach (EconomyTransactionRecordDto record in dto.ledger)
            {
                EconomyTransactionRecordDto clone = record.DeepClone();
                ledger.Add(clone);
                recordsById.Add(clone.transactionId, clone);
            }
        }

        private EconomyTransactionReceipt Failure(
            string transactionId,
            EconomyTransactionFailureReason reason) =>
            new(
                false,
                false,
                reason,
                transactionId,
                balanceMinorUnits,
                balanceMinorUnits,
                0);

        private bool Reject(
            in EconomyTransactionRequest request,
            EconomyTransactionFailureReason reason,
            out EconomyTransactionReceipt receipt)
        {
            receipt = Failure(request.TransactionId, reason);
            TransactionRejected?.Invoke(
                new EconomyTransactionRejected(in request, in receipt));
            return false;
        }

        private static bool Matches(
            EconomyTransactionRecordDto record,
            in EconomyTransactionRequest request) =>
            record.kind == (int)request.Kind &&
            record.direction == (int)request.Direction &&
            record.amountMinorUnits == request.AmountMinorUnits &&
            string.Equals(
                record.sourceStableId,
                request.SourceStableId,
                StringComparison.Ordinal) &&
            string.Equals(record.priceId, request.PriceId, StringComparison.Ordinal) &&
            record.quantity == request.Quantity &&
            string.Equals(
                record.relatedTransactionId,
                request.RelatedTransactionId,
                StringComparison.Ordinal);

        private long GetCurrentGameTimeTicks()
        {
            GameTimeSnapshot snapshot = gameTime.Snapshot;
            return checked(
                snapshot.DayIndex * GameTimeConfig.TicksPerGameDay +
                snapshot.TimeOfDayTicks);
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "Economy runtime is not initialized.");
            }
        }

        private void OnDestroy()
        {
            timeSubscription?.Dispose();
            timeSubscription = null;
        }
    }
}
