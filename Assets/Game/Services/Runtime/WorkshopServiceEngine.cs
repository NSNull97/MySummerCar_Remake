using System;
using System.Collections.Generic;
using MSC.Economy;

namespace MSC.Services
{
    public readonly struct WorkshopPriceQuote
    {
        public WorkshopPriceQuote(
            long basePriceMinorUnits,
            int discountBasisPoints,
            long payableMinorUnits)
        {
            BasePriceMinorUnits = basePriceMinorUnits;
            DiscountBasisPoints = discountBasisPoints;
            PayableMinorUnits = payableMinorUnits;
        }

        public long BasePriceMinorUnits { get; }
        public int DiscountBasisPoints { get; }
        public long PayableMinorUnits { get; }
    }

    /// <summary>
    /// Pure workshop order state machine. It owns payment and timing, while all
    /// vehicle mutations stay behind <see cref="IWorkshopOutcomeBackend"/>.
    /// A Ready order with a deferred outcome means the paperwork/timer finished;
    /// it must not be presented as a repaired vehicle until OutcomeApplied is true.
    /// </summary>
    public sealed class WorkshopServiceEngine
    {
        public const int NormalDiscountBasisPoints = 1_000;
        public const int ChristmasDiscountBasisPoints = 7_500;
        public const float MinimumWorkRealSeconds = 1_400f;
        public const float MaximumWorkRealSeconds = 2_600f;
        public const float WorkPauseDistanceMeters = 100f;
        public const float OutcomeFinalizingRealSeconds = 7f;
        public const float LoanerOfferDistanceMeters = 50f;
        public const float LoanerReturnDistanceMeters = 30f;
        public const float LoanerWarningRealSeconds = 2_000f;
        public const float LoanerOverdueRealSeconds = 3_000f;

        private readonly ServiceCatalog catalog;
        private readonly IEconomyTransactionService economy;
        private readonly IWorkshopOutcomeBackend outcomeBackend;
        private readonly ServiceLocationDefinition workshopLocation;

        private WorkshopSelectionStateDto selection;
        private WorkshopOrderStateDto order;

        public WorkshopServiceEngine(
            ServiceCatalog configuredCatalog,
            IEconomyTransactionService configuredEconomy,
            string workshopLocationId,
            IWorkshopOutcomeBackend configuredOutcomeBackend = null)
        {
            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            economy = configuredEconomy ??
                throw new ArgumentNullException(nameof(configuredEconomy));
            outcomeBackend = configuredOutcomeBackend;
            if (!catalog.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredCatalog));
            }

            if (!catalog.TryGetLocation(
                    workshopLocationId,
                    out workshopLocation) ||
                workshopLocation.Kind != ServiceLocationKind.Workshop)
            {
                throw new ArgumentException(
                    "Workshop location is missing or has the wrong kind.",
                    nameof(workshopLocationId));
            }

            selection = CreateEmptySelection();
            order = CreateEmptyOrder();
        }

        public WorkshopOrderPhase Phase => (WorkshopOrderPhase)order.phase;
        public bool HasActiveOrder => Phase != WorkshopOrderPhase.None;
        public bool OutcomeApplied => order.outcomeApplied;
        public bool OutcomeDeferred => order.outcomeDeferred;
        public bool CanApplyOutcomes => outcomeBackend != null;
        public bool LoanerWarningNotified => order.loanerWarningNotified;
        public bool LoanerOverdue => order.loanerOverdue;

        public WorkshopSelectionStateDto CaptureSelectionDto() =>
            selection.DeepClone();

        public WorkshopOrderStateDto CaptureOrderDto() => order.DeepClone();

        public bool TryMarkRepairReadyNotified()
        {
            if (Phase != WorkshopOrderPhase.Ready ||
                order.repairReadyNotified)
            {
                return false;
            }

            order.repairReadyNotified = true;
            return true;
        }

        public ServiceResult TrySetSelection(
            WorkshopSelectionStateDto requestedSelection)
        {
            if (HasActiveOrder)
            {
                return Failure(ServiceFailureReason.OrderAlreadyActive);
            }

            if (!TryNormalizeSelection(
                    requestedSelection,
                    allowEmpty: true,
                    out WorkshopSelectionStateDto normalized,
                    out ServiceFailureReason failure))
            {
                return Failure(failure);
            }

            selection = normalized;
            return Success();
        }

        public bool TryQuote(
            DateTime serviceDate,
            out WorkshopPriceQuote quote,
            out ServiceFailureReason failure)
        {
            if (!TryNormalizeSelection(
                    selection,
                    allowEmpty: false,
                    out WorkshopSelectionStateDto normalized,
                    out failure))
            {
                quote = default;
                return false;
            }

            try
            {
                long basePrice = 0;
                for (int index = 0; index < normalized.offerIds.Length; index++)
                {
                    catalog.TryGetOffer(
                        normalized.offerIds[index],
                        out ServiceOfferDefinition offer);
                    basePrice = checked(
                        basePrice + offer.BasePriceMinorUnits);
                }

                int discount = GetDiscountBasisPoints(serviceDate);
                long payable = checked((long)decimal.Round(
                    basePrice * ((10_000 - discount) / 10_000m),
                    0,
                    MidpointRounding.AwayFromZero));
                quote = new WorkshopPriceQuote(
                    basePrice,
                    discount,
                    payable);
                failure = ServiceFailureReason.None;
                return true;
            }
            catch (OverflowException)
            {
                quote = default;
                failure = ServiceFailureReason.InvalidRequest;
                return false;
            }
        }

        public ServiceResult TryPlaceOrder(
            string orderId,
            string transactionId,
            DateTime serviceDate,
            float workDurationRealSeconds,
            string playerVehicleStableId = "",
            string loanerVehicleStableId = "")
        {
            if (!ServiceStableId.IsCanonical(orderId) ||
                !ServiceStableId.IsCanonical(transactionId) ||
                !IsFinite(workDurationRealSeconds) ||
                workDurationRealSeconds < MinimumWorkRealSeconds ||
                workDurationRealSeconds > MaximumWorkRealSeconds ||
                !IsOptionalStableId(playerVehicleStableId) ||
                !IsOptionalStableId(loanerVehicleStableId))
            {
                return Failure(ServiceFailureReason.InvalidRequest, orderId);
            }

            if (HasActiveOrder)
            {
                return IsMatchingReplay(
                        orderId,
                        transactionId,
                        workDurationRealSeconds,
                        playerVehicleStableId,
                        loanerVehicleStableId)
                    ? Success(
                        orderId,
                        order.amountMinorUnits,
                        wasIdempotentReplay: true)
                    : Failure(
                        ServiceFailureReason.OrderAlreadyActive,
                        orderId);
            }

            ServiceFailureReason quoteFailure = ServiceFailureReason.None;
            if (!TryNormalizeSelection(
                    selection,
                    allowEmpty: false,
                    out WorkshopSelectionStateDto normalized,
                    out ServiceFailureReason selectionFailure) ||
                !TryQuote(
                    serviceDate,
                    out WorkshopPriceQuote quote,
                    out quoteFailure))
            {
                return Failure(
                    selectionFailure != ServiceFailureReason.None
                        ? selectionFailure
                        : quoteFailure,
                    orderId);
            }

            var payment = new EconomyTransactionRequest(
                transactionId,
                EconomyTransactionKind.ServicePayment,
                EconomyTransactionDirection.Debit,
                quote.PayableMinorUnits,
                workshopLocation.SourceStableId);
            if (!economy.TryCommit(in payment, out EconomyTransactionReceipt receipt))
            {
                return Failure(
                    receipt.FailureReason ==
                        EconomyTransactionFailureReason.InsufficientFunds
                            ? ServiceFailureReason.InsufficientFunds
                            : ServiceFailureReason.TransactionRejected,
                    orderId,
                    quote.PayableMinorUnits);
            }

            // Active orders are replayed above from their full stored payload.
            // If Economy alone recognizes this transaction, the corresponding
            // order was already acknowledged and cleared. Reusing it must not
            // create a second free work cycle after the historical debit.
            if (receipt.WasIdempotentReplay)
            {
                return Failure(
                    ServiceFailureReason.TransactionRejected,
                    orderId,
                    quote.PayableMinorUnits);
            }

            selection = normalized;
            order = new WorkshopOrderStateDto
            {
                phase = (int)WorkshopOrderPhase.InProgress,
                orderId = orderId,
                transactionId = transactionId,
                amountMinorUnits = quote.PayableMinorUnits,
                quotedBaseMinorUnits = quote.BasePriceMinorUnits,
                discountBasisPoints = quote.DiscountBasisPoints,
                initialWorkRealSeconds = workDurationRealSeconds,
                remainingWorkRealSeconds = workDurationRealSeconds,
                outcomeDelayRealSeconds = 0f,
                outcomeApplied = false,
                outcomeDeferred = false,
                loanerOffered = outcomeBackend != null &&
                    outcomeBackend.IsPlayerVehicleWithin(
                        LoanerOfferDistanceMeters),
                loanerBorrowed = false,
                loanerReturned = false,
                repairReadyNotified = false,
                loanerMissingRealSeconds = 0f,
                loanerWarningNotified = false,
                loanerOverdue = false,
                loanerOverdueRealSeconds = 0f,
                relocatePlayerVehicleOnNextSleep = false,
                playerVehicleStableId = playerVehicleStableId ?? string.Empty,
                loanerVehicleStableId = loanerVehicleStableId ?? string.Empty,
                orderedSelection = normalized.DeepClone(),
            };
            return Success(
                orderId,
                quote.PayableMinorUnits,
                receipt.WasIdempotentReplay);
        }

        public bool Tick(float realDeltaSeconds, float playerDistanceMeters)
        {
            if (!IsFinite(realDeltaSeconds) || realDeltaSeconds < 0f ||
                !IsFinite(playerDistanceMeters) || playerDistanceMeters < 0f ||
                realDeltaSeconds == 0f || !HasActiveOrder)
            {
                return false;
            }

            bool changed = false;
            if (Phase == WorkshopOrderPhase.InProgress &&
                playerDistanceMeters > WorkPauseDistanceMeters)
            {
                float previous = order.remainingWorkRealSeconds;
                order.remainingWorkRealSeconds = Math.Max(
                    0f,
                    previous - realDeltaSeconds);
                changed = order.remainingWorkRealSeconds != previous;
                if (order.remainingWorkRealSeconds <= 0f)
                {
                    order.phase = (int)WorkshopOrderPhase.ApplyingOutcomes;
                    order.outcomeDelayRealSeconds =
                        OutcomeFinalizingRealSeconds;
                    TryApplyOutcomeInternal();
                    changed = true;
                }
            }
            else if (Phase == WorkshopOrderPhase.ApplyingOutcomes)
            {
                float previous = order.outcomeDelayRealSeconds;
                order.outcomeDelayRealSeconds = Math.Max(
                    0f,
                    previous - realDeltaSeconds);
                changed = order.outcomeDelayRealSeconds != previous;
                if (order.outcomeDelayRealSeconds <= 0f)
                {
                    order.phase = (int)WorkshopOrderPhase.Ready;
                    changed = true;
                }
            }

            if (Phase == WorkshopOrderPhase.Ready &&
                order.loanerBorrowed && !order.loanerReturned)
            {
                changed |= TickMissingLoaner(realDeltaSeconds);
            }

            return changed;
        }

        public ServiceResult TryRetryDeferredOutcome()
        {
            if ((Phase != WorkshopOrderPhase.ApplyingOutcomes &&
                 Phase != WorkshopOrderPhase.Ready) ||
                order.outcomeApplied || !order.outcomeDeferred)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (TryApplyOutcomeInternal())
            {
                return Success(order.orderId, order.amountMinorUnits);
            }

            return Failure(
                ServiceFailureReason.VehicleOutcomeUnavailable,
                order.orderId,
                order.amountMinorUnits);
        }

        public ServiceResult TryBorrowLoaner(string loanerVehicleStableId)
        {
            if (!HasActiveOrder || !order.loanerOffered ||
                order.loanerBorrowed ||
                !ServiceStableId.IsCanonical(loanerVehicleStableId))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            order.loanerBorrowed = true;
            order.loanerReturned = false;
            order.loanerVehicleStableId = loanerVehicleStableId;
            return Success(order.orderId);
        }

        public ServiceResult TryReturnLoaner()
        {
            if (!HasActiveOrder || !order.loanerBorrowed ||
                order.loanerReturned)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (outcomeBackend == null ||
                !outcomeBackend.IsLoanerWithin(LoanerReturnDistanceMeters))
            {
                return Failure(ServiceFailureReason.VehicleOutcomeUnavailable);
            }

            order.loanerReturned = true;
            order.loanerOverdue = false;
            order.relocatePlayerVehicleOnNextSleep = false;
            return Success(order.orderId);
        }

        public ServiceResult TryProcessNextSleep()
        {
            if (!order.relocatePlayerVehicleOnNextSleep)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (outcomeBackend == null ||
                !outcomeBackend.TryRelocatePlayerVehicleForOverdueLoaner(
                    out _))
            {
                return Failure(ServiceFailureReason.VehicleOutcomeUnavailable);
            }

            order.relocatePlayerVehicleOnNextSleep = false;
            return Success(order.orderId);
        }

        public ServiceResult TryAcknowledgeReadyOrder()
        {
            if (Phase != WorkshopOrderPhase.Ready)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (!order.outcomeApplied)
            {
                return Failure(
                    ServiceFailureReason.VehicleOutcomeUnavailable,
                    order.orderId,
                    order.amountMinorUnits);
            }

            if (order.loanerBorrowed && !order.loanerReturned)
            {
                return Failure(
                    ServiceFailureReason.OrderAlreadyActive,
                    order.orderId,
                    order.amountMinorUnits);
            }

            string completedOrderId = order.orderId;
            order = CreateEmptyOrder();
            selection = CreateEmptySelection();
            return Success(completedOrderId);
        }

        public bool TryRestore(
            WorkshopSelectionStateDto restoredSelection,
            WorkshopOrderStateDto restoredOrder,
            out string failure)
        {
            if (!TryBuildRestoreCandidate(
                    restoredSelection,
                    restoredOrder,
                    out WorkshopSelectionStateDto normalizedSelection,
                    out WorkshopOrderStateDto candidate,
                    out failure))
            {
                return false;
            }

            selection = normalizedSelection;
            order = candidate;
            return true;
        }

        public bool TryValidateRestoreCandidate(
            WorkshopSelectionStateDto restoredSelection,
            WorkshopOrderStateDto restoredOrder,
            out string failure) =>
            TryBuildRestoreCandidate(
                restoredSelection,
                restoredOrder,
                out _,
                out _,
                out failure);

        private bool TryBuildRestoreCandidate(
            WorkshopSelectionStateDto restoredSelection,
            WorkshopOrderStateDto restoredOrder,
            out WorkshopSelectionStateDto normalizedSelection,
            out WorkshopOrderStateDto candidate,
            out string failure)
        {
            if (!TryNormalizeSelection(
                    restoredSelection,
                    allowEmpty: true,
                    out normalizedSelection,
                    out _))
            {
                candidate = null;
                failure = "Workshop selection state is invalid.";
                return false;
            }

            candidate = restoredOrder?.DeepClone();
            if (!TryValidateOrder(candidate, out failure))
            {
                return false;
            }

            if ((WorkshopOrderPhase)candidate.phase !=
                    WorkshopOrderPhase.None &&
                !SelectionsEqual(
                    normalizedSelection,
                    candidate.orderedSelection))
            {
                failure = "Workshop selection does not match the active order.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static int GetDiscountBasisPoints(DateTime serviceDate) =>
            serviceDate.Month == 12 &&
            serviceDate.Day >= 24 && serviceDate.Day <= 26
                ? ChristmasDiscountBasisPoints
                : NormalDiscountBasisPoints;

        private bool TryNormalizeSelection(
            WorkshopSelectionStateDto requested,
            bool allowEmpty,
            out WorkshopSelectionStateDto normalized,
            out ServiceFailureReason failure)
        {
            normalized = null;
            if (requested == null || requested.offerIds == null ||
                requested.offerIds.Length > 64 ||
                !IsFinite(requested.finalGearRatio) ||
                requested.finalGearRatio < 2f ||
                requested.finalGearRatio > 6f)
            {
                failure = ServiceFailureReason.InvalidRequest;
                return false;
            }

            if (requested.offerIds.Length == 0)
            {
                if (!allowEmpty)
                {
                    failure = ServiceFailureReason.EmptyBasket;
                    return false;
                }

                normalized = requested.DeepClone();
                failure = ServiceFailureReason.None;
                return true;
            }

            var offerIds = new HashSet<string>(StringComparer.Ordinal);
            var exclusiveGroups = new HashSet<string>(StringComparer.Ordinal);
            string[] sortedIds = (string[])requested.offerIds.Clone();
            Array.Sort(sortedIds, StringComparer.Ordinal);
            for (int index = 0; index < sortedIds.Length; index++)
            {
                string offerId = sortedIds[index];
                if (!ServiceStableId.IsCanonical(offerId) ||
                    !offerIds.Add(offerId) ||
                    !catalog.TryGetOffer(
                        offerId,
                        out ServiceOfferDefinition offer) ||
                    offer.Kind != ServiceOfferKind.Workshop ||
                    !string.Equals(
                        offer.LocationId,
                        workshopLocation.LocationId,
                        StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(offer.ExclusiveGroupId) &&
                    !exclusiveGroups.Add(offer.ExclusiveGroupId))
                {
                    failure = ServiceFailureReason.InvalidRequest;
                    return false;
                }
            }

            normalized = new WorkshopSelectionStateDto
            {
                offerIds = sortedIds,
                paintVariant = requested.paintVariant,
                rimVariant = requested.rimVariant,
                tireVariant = requested.tireVariant,
                finalGearRatio = requested.finalGearRatio,
            };
            failure = ServiceFailureReason.None;
            return true;
        }

        private bool TryValidateOrder(
            WorkshopOrderStateDto candidate,
            out string failure)
        {
            if (candidate == null ||
                !Enum.IsDefined(typeof(WorkshopOrderPhase), candidate.phase) ||
                !IsFinite(candidate.initialWorkRealSeconds) ||
                !IsFinite(candidate.remainingWorkRealSeconds) ||
                !IsFinite(candidate.outcomeDelayRealSeconds) ||
                !IsFinite(candidate.loanerMissingRealSeconds) ||
                !IsFinite(candidate.loanerOverdueRealSeconds) ||
                candidate.remainingWorkRealSeconds < 0f ||
                candidate.outcomeDelayRealSeconds < 0f ||
                candidate.loanerMissingRealSeconds < 0f ||
                candidate.loanerOverdueRealSeconds < 0f)
            {
                failure = "Workshop order state has invalid bounds.";
                return false;
            }

            WorkshopOrderPhase phase =
                (WorkshopOrderPhase)candidate.phase;
            if (phase == WorkshopOrderPhase.None)
            {
                if (!string.IsNullOrEmpty(candidate.orderId) ||
                    !string.IsNullOrEmpty(candidate.transactionId) ||
                    candidate.amountMinorUnits != 0 ||
                    candidate.quotedBaseMinorUnits != 0)
                {
                    failure = "Empty workshop order carries transaction data.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            if (!ServiceStableId.IsCanonical(candidate.orderId) ||
                !ServiceStableId.IsCanonical(candidate.transactionId) ||
                !IsOptionalStableId(candidate.playerVehicleStableId) ||
                !IsOptionalStableId(candidate.loanerVehicleStableId) ||
                candidate.amountMinorUnits <= 0 ||
                candidate.quotedBaseMinorUnits <= 0 ||
                candidate.discountBasisPoints !=
                    NormalDiscountBasisPoints &&
                candidate.discountBasisPoints !=
                    ChristmasDiscountBasisPoints ||
                candidate.initialWorkRealSeconds < MinimumWorkRealSeconds ||
                candidate.initialWorkRealSeconds > MaximumWorkRealSeconds ||
                candidate.remainingWorkRealSeconds >
                    candidate.initialWorkRealSeconds ||
                !TryNormalizeSelection(
                    candidate.orderedSelection,
                    allowEmpty: false,
                    out WorkshopSelectionStateDto normalized,
                    out _))
            {
                failure = "Active workshop order is inconsistent.";
                return false;
            }

            if (!TryCalculateStoredQuote(
                    normalized,
                    candidate.discountBasisPoints,
                    out long expectedBase,
                    out long expectedPayable) ||
                candidate.quotedBaseMinorUnits != expectedBase ||
                candidate.amountMinorUnits != expectedPayable ||
                candidate.outcomeApplied && candidate.outcomeDeferred ||
                candidate.loanerReturned && !candidate.loanerBorrowed ||
                candidate.loanerOverdue && !candidate.loanerBorrowed ||
                candidate.relocatePlayerVehicleOnNextSleep &&
                    !candidate.loanerOverdue ||
                phase == WorkshopOrderPhase.InProgress &&
                    (candidate.outcomeApplied || candidate.outcomeDeferred ||
                     candidate.outcomeDelayRealSeconds != 0f) ||
                phase == WorkshopOrderPhase.ApplyingOutcomes &&
                    candidate.outcomeDelayRealSeconds >
                        OutcomeFinalizingRealSeconds ||
                phase == WorkshopOrderPhase.Ready &&
                    candidate.outcomeDelayRealSeconds != 0f)
            {
                failure = "Workshop order quote or phase flags are inconsistent.";
                return false;
            }

            candidate.orderedSelection = normalized;
            failure = string.Empty;
            return true;
        }

        private bool TryCalculateStoredQuote(
            WorkshopSelectionStateDto normalized,
            int discountBasisPoints,
            out long basePriceMinorUnits,
            out long payableMinorUnits)
        {
            try
            {
                long basePrice = 0;
                for (int index = 0;
                     index < normalized.offerIds.Length;
                     index++)
                {
                    if (!catalog.TryGetOffer(
                            normalized.offerIds[index],
                            out ServiceOfferDefinition offer))
                    {
                        basePriceMinorUnits = 0;
                        payableMinorUnits = 0;
                        return false;
                    }

                    basePrice = checked(
                        basePrice + offer.BasePriceMinorUnits);
                }

                basePriceMinorUnits = basePrice;
                payableMinorUnits = checked((long)decimal.Round(
                    basePrice *
                    ((10_000 - discountBasisPoints) / 10_000m),
                    0,
                    MidpointRounding.AwayFromZero));
                return basePriceMinorUnits > 0 && payableMinorUnits > 0;
            }
            catch (OverflowException)
            {
                basePriceMinorUnits = 0;
                payableMinorUnits = 0;
                return false;
            }
        }

        private bool TryApplyOutcomeInternal()
        {
            if (order.outcomeApplied)
            {
                return true;
            }

            if (outcomeBackend == null)
            {
                order.outcomeDeferred = true;
                return false;
            }

            WorkshopSelectionStateDto ordered = order.orderedSelection;
            var request = new WorkshopOutcomeRequest(
                order.orderId,
                order.playerVehicleStableId,
                (string[])ordered.offerIds.Clone(),
                ordered.paintVariant,
                ordered.rimVariant,
                ordered.tireVariant,
                ordered.finalGearRatio);
            if (!outcomeBackend.TryApply(in request, out _))
            {
                order.outcomeDeferred = true;
                return false;
            }

            order.outcomeApplied = true;
            order.outcomeDeferred = false;
            return true;
        }

        private bool TickMissingLoaner(float realDeltaSeconds)
        {
            bool changed = false;
            if (!order.loanerWarningNotified)
            {
                float previous = order.loanerMissingRealSeconds;
                order.loanerMissingRealSeconds = Math.Min(
                    LoanerWarningRealSeconds,
                    previous + realDeltaSeconds);
                changed = order.loanerMissingRealSeconds != previous;
                if (order.loanerMissingRealSeconds >=
                    LoanerWarningRealSeconds)
                {
                    order.loanerWarningNotified = true;
                    changed = true;
                }

                return changed;
            }

            if (!order.loanerOverdue)
            {
                float previous = order.loanerOverdueRealSeconds;
                order.loanerOverdueRealSeconds = Math.Min(
                    LoanerOverdueRealSeconds,
                    previous + realDeltaSeconds);
                changed = order.loanerOverdueRealSeconds != previous;
                if (order.loanerOverdueRealSeconds >=
                    LoanerOverdueRealSeconds)
                {
                    order.loanerOverdue = true;
                    order.relocatePlayerVehicleOnNextSleep = true;
                    changed = true;
                }
            }

            return changed;
        }

        private bool IsMatchingReplay(
            string orderId,
            string transactionId,
            float workDurationRealSeconds,
            string playerVehicleStableId,
            string loanerVehicleStableId) =>
            string.Equals(order.orderId, orderId, StringComparison.Ordinal) &&
            string.Equals(
                order.transactionId,
                transactionId,
                StringComparison.Ordinal) &&
            string.Equals(
                order.playerVehicleStableId,
                playerVehicleStableId ?? string.Empty,
                StringComparison.Ordinal) &&
            string.Equals(
                order.loanerVehicleStableId,
                loanerVehicleStableId ?? string.Empty,
                StringComparison.Ordinal) &&
            Math.Abs(
                order.initialWorkRealSeconds - workDurationRealSeconds) <
                0.001f &&
            SelectionsEqual(selection, order.orderedSelection);

        private static bool SelectionsEqual(
            WorkshopSelectionStateDto left,
            WorkshopSelectionStateDto right)
        {
            if (left == null || right == null ||
                left.offerIds == null || right.offerIds == null ||
                left.offerIds.Length != right.offerIds.Length ||
                left.paintVariant != right.paintVariant ||
                left.rimVariant != right.rimVariant ||
                left.tireVariant != right.tireVariant ||
                Math.Abs(left.finalGearRatio - right.finalGearRatio) >= 0.001f)
            {
                return false;
            }

            for (int index = 0; index < left.offerIds.Length; index++)
            {
                if (!string.Equals(
                        left.offerIds[index],
                        right.offerIds[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static WorkshopSelectionStateDto CreateEmptySelection() =>
            new()
            {
                offerIds = Array.Empty<string>(),
                paintVariant = -1,
                rimVariant = -1,
                tireVariant = -1,
                finalGearRatio = 4.286f,
            };

        private static WorkshopOrderStateDto CreateEmptyOrder() => new()
        {
            phase = (int)WorkshopOrderPhase.None,
            orderedSelection = CreateEmptySelection(),
        };

        private static bool IsOptionalStableId(string value) =>
            string.IsNullOrEmpty(value) || ServiceStableId.IsCanonical(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static ServiceResult Success(
            string operationId = "",
            long amountMinorUnits = 0,
            bool wasIdempotentReplay = false) =>
            new(
                true,
                ServiceFailureReason.None,
                operationId,
                amountMinorUnits,
                wasIdempotentReplay);

        private static ServiceResult Failure(
            ServiceFailureReason reason,
            string operationId = "",
            long amountMinorUnits = 0) =>
            new(false, reason, operationId, amountMinorUnits);
    }
}
