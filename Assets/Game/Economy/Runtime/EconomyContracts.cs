using System;
using System.Collections.Generic;

namespace MSC.Economy
{
    public enum EconomyTransactionKind
    {
        Purchase = 0,
        Refund = 1,
        Reward = 2,
        Fine = 3,
        ServicePayment = 4,
        BillPayment = 5,
        Adjustment = 6,
    }

    public enum EconomyTransactionDirection
    {
        Debit = 0,
        Credit = 1,
    }

    public enum EconomyTransactionFailureReason
    {
        None = 0,
        InvalidRequest = 1,
        InvalidAmount = 2,
        InsufficientFunds = 3,
        DuplicateTransactionConflict = 4,
        BalanceOverflow = 5,
        UnknownPrice = 6,
        RefundNotAllowed = 7,
    }

    public readonly struct EconomySnapshot
    {
        public EconomySnapshot(ulong revision, long balanceMinorUnits, int ledgerCount)
        {
            Revision = revision;
            BalanceMinorUnits = balanceMinorUnits;
            LedgerCount = ledgerCount;
        }

        public ulong Revision { get; }
        public long BalanceMinorUnits { get; }
        public decimal BalanceMarkka => BalanceMinorUnits / 100m;
        public int LedgerCount { get; }
    }

    public interface IPlayerMoneyService
    {
        EconomySnapshot Snapshot { get; }
        event Action<EconomySnapshot> StateChanged;
    }

    /// <summary>
    /// Explicit write boundary for gameplay systems that create monetary
    /// transactions. Read-only HUD consumers continue to depend on
    /// <see cref="IPlayerMoneyService"/> and cannot mutate the wallet.
    /// </summary>
    public interface IEconomyTransactionService : IPlayerMoneyService
    {
        bool TryQuote(
            string priceId,
            int quantity,
            out EconomyPriceQuote quote,
            out EconomyTransactionFailureReason failureReason);

        bool TryPurchase(
            string transactionId,
            string priceId,
            int quantity,
            string sourceStableId,
            out EconomyTransactionReceipt receipt);

        bool TryRefund(
            string transactionId,
            string originalTransactionId,
            long amountMinorUnits,
            string sourceStableId,
            out EconomyTransactionReceipt receipt);

        bool TryCommit(
            in EconomyTransactionRequest request,
            out EconomyTransactionReceipt receipt);
    }

    /// <summary>
    /// Optional presentation-facing transaction outcome stream. Consumers can
    /// react to a rejected request without widening the authoritative wallet
    /// mutation boundary or polling its balance.
    /// </summary>
    public interface IEconomyTransactionFeedbackSource
    {
        event Action<EconomyTransactionRejected> TransactionRejected;
    }

    public readonly struct EconomyPriceQuote
    {
        public EconomyPriceQuote(
            string priceId,
            int quantity,
            long unitPriceMinorUnits,
            long totalPriceMinorUnits,
            int appliedInflationCycles)
        {
            PriceId = priceId ?? string.Empty;
            Quantity = quantity;
            UnitPriceMinorUnits = unitPriceMinorUnits;
            TotalPriceMinorUnits = totalPriceMinorUnits;
            AppliedInflationCycles = appliedInflationCycles;
        }

        public string PriceId { get; }
        public int Quantity { get; }
        public long UnitPriceMinorUnits { get; }
        public long TotalPriceMinorUnits { get; }
        public int AppliedInflationCycles { get; }
    }

    public readonly struct EconomyTransactionRequest
    {
        public EconomyTransactionRequest(
            string transactionId,
            EconomyTransactionKind kind,
            EconomyTransactionDirection direction,
            long amountMinorUnits,
            string sourceStableId,
            string priceId = "",
            int quantity = 0,
            string relatedTransactionId = "")
        {
            TransactionId = transactionId ?? string.Empty;
            Kind = kind;
            Direction = direction;
            AmountMinorUnits = amountMinorUnits;
            SourceStableId = sourceStableId ?? string.Empty;
            PriceId = priceId ?? string.Empty;
            Quantity = quantity;
            RelatedTransactionId = relatedTransactionId ?? string.Empty;
        }

        public string TransactionId { get; }
        public EconomyTransactionKind Kind { get; }
        public EconomyTransactionDirection Direction { get; }
        public long AmountMinorUnits { get; }
        public string SourceStableId { get; }
        public string PriceId { get; }
        public int Quantity { get; }
        public string RelatedTransactionId { get; }
    }

    public readonly struct EconomyTransactionReceipt
    {
        public EconomyTransactionReceipt(
            bool succeeded,
            bool wasIdempotentReplay,
            EconomyTransactionFailureReason failureReason,
            string transactionId,
            long balanceBeforeMinorUnits,
            long balanceAfterMinorUnits,
            long sequence)
        {
            Succeeded = succeeded;
            WasIdempotentReplay = wasIdempotentReplay;
            FailureReason = failureReason;
            TransactionId = transactionId ?? string.Empty;
            BalanceBeforeMinorUnits = balanceBeforeMinorUnits;
            BalanceAfterMinorUnits = balanceAfterMinorUnits;
            Sequence = sequence;
        }

        public bool Succeeded { get; }
        public bool WasIdempotentReplay { get; }
        public EconomyTransactionFailureReason FailureReason { get; }
        public string TransactionId { get; }
        public long BalanceBeforeMinorUnits { get; }
        public long BalanceAfterMinorUnits { get; }
        public long Sequence { get; }
    }

    public readonly struct EconomyTransactionRejected
    {
        public EconomyTransactionRejected(
            in EconomyTransactionRequest request,
            in EconomyTransactionReceipt receipt)
        {
            Request = request;
            Receipt = receipt;
        }

        public EconomyTransactionRequest Request { get; }
        public EconomyTransactionReceipt Receipt { get; }
    }

    [Serializable]
    public sealed class EconomyPriceScopeStateDto
    {
        public string scopeId = string.Empty;
        public int appliedInflationCycles;

        public EconomyPriceScopeStateDto DeepClone() =>
            (EconomyPriceScopeStateDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class EconomyTransactionRecordDto
    {
        public long sequence;
        public string transactionId = string.Empty;
        public int kind;
        public int direction;
        public long amountMinorUnits;
        public long balanceBeforeMinorUnits;
        public long balanceAfterMinorUnits;
        public string sourceStableId = string.Empty;
        public string priceId = string.Empty;
        public int quantity;
        public string relatedTransactionId = string.Empty;
        public long gameTimeTicks;

        public EconomyTransactionRecordDto DeepClone() =>
            (EconomyTransactionRecordDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class EconomyStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string catalogId = string.Empty;
        public long balanceMinorUnits;
        public long nextSequence = 1;
        public long lastProcessedDayIndex;
        public EconomyPriceScopeStateDto[] priceScopes =
            Array.Empty<EconomyPriceScopeStateDto>();
        public EconomyTransactionRecordDto[] ledger =
            Array.Empty<EconomyTransactionRecordDto>();

        public EconomyStateDto DeepClone()
        {
            return new EconomyStateDto
            {
                schemaVersion = schemaVersion,
                catalogId = catalogId,
                balanceMinorUnits = balanceMinorUnits,
                nextSequence = nextSequence,
                lastProcessedDayIndex = lastProcessedDayIndex,
                priceScopes = Clone(priceScopes),
                ledger = Clone(ledger),
            };
        }

        public bool TryValidate(EconomyPriceCatalog catalog, out string failure)
        {
            if (catalog == null ||
                schemaVersion != CurrentSchemaVersion ||
                !string.Equals(catalogId, catalog.CatalogId, StringComparison.Ordinal) ||
                balanceMinorUnits < 0 || nextSequence < 1 ||
                lastProcessedDayIndex < 0)
            {
                failure = "Economy save header or balance is invalid.";
                return false;
            }

            var scopeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyPriceScopeStateDto scope in
                     priceScopes ?? Array.Empty<EconomyPriceScopeStateDto>())
            {
                if (scope == null ||
                    !catalog.TryGetScope(scope.scopeId, out _) ||
                    scope.appliedInflationCycles < 0 ||
                    !scopeIds.Add(scope.scopeId))
                {
                    failure = "Economy price scope state is invalid or duplicated.";
                    return false;
                }
            }

            if (scopeIds.Count != catalog.Scopes.Count)
            {
                failure = "Economy save does not contain every configured price scope.";
                return false;
            }

            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            long previousSequence = 0;
            long expectedBalance = 0;
            bool hasRecord = false;
            foreach (EconomyTransactionRecordDto record in
                     ledger ?? Array.Empty<EconomyTransactionRecordDto>())
            {
                if (record == null || record.sequence <= previousSequence ||
                    !StableIdRules.IsCanonical(record.transactionId) ||
                    !Enum.IsDefined(typeof(EconomyTransactionKind), record.kind) ||
                    !Enum.IsDefined(typeof(EconomyTransactionDirection), record.direction) ||
                    record.amountMinorUnits <= 0 ||
                    record.balanceBeforeMinorUnits < 0 ||
                    record.balanceAfterMinorUnits < 0 ||
                    !StableIdRules.IsCanonical(record.sourceStableId) ||
                    !transactionIds.Add(record.transactionId))
                {
                    failure = "Economy ledger contains invalid or duplicate records.";
                    return false;
                }

                long calculated = (EconomyTransactionDirection)record.direction ==
                    EconomyTransactionDirection.Debit
                        ? record.balanceBeforeMinorUnits - record.amountMinorUnits
                        : record.balanceBeforeMinorUnits + record.amountMinorUnits;
                if (calculated != record.balanceAfterMinorUnits ||
                    !hasRecord && record.balanceBeforeMinorUnits !=
                        catalog.InitialBalanceMinorUnits ||
                    hasRecord && record.balanceBeforeMinorUnits != expectedBalance)
                {
                    failure = "Economy ledger balance chain is invalid.";
                    return false;
                }

                previousSequence = record.sequence;
                expectedBalance = record.balanceAfterMinorUnits;
                hasRecord = true;
            }

            if (hasRecord && expectedBalance != balanceMinorUnits ||
                nextSequence <= previousSequence)
            {
                failure = "Economy ledger tail does not match the saved balance.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static EconomyPriceScopeStateDto[] Clone(
            EconomyPriceScopeStateDto[] source)
        {
            EconomyPriceScopeStateDto[] values = source ??
                Array.Empty<EconomyPriceScopeStateDto>();
            var result = new EconomyPriceScopeStateDto[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = values[index]?.DeepClone();
            }

            return result;
        }

        private static EconomyTransactionRecordDto[] Clone(
            EconomyTransactionRecordDto[] source)
        {
            EconomyTransactionRecordDto[] values = source ??
                Array.Empty<EconomyTransactionRecordDto>();
            var result = new EconomyTransactionRecordDto[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = values[index]?.DeepClone();
            }

            return result;
        }
    }
}
