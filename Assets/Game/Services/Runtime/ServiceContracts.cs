using System;
using UnityEngine;

namespace MSC.Services
{
    public enum ServiceLocationKind
    {
        Store = 0,
        Pub = 1,
        FuelStation = 2,
        Workshop = 3,
        Inspection = 4,
    }

    public enum ServiceOfferKind
    {
        RetailItem = 0,
        PubItem = 1,
        Fuel = 2,
        Workshop = 3,
        Inspection = 4,
    }

    public enum ServiceFailureReason
    {
        None = 0,
        NotInitialized = 1,
        UnknownLocation = 2,
        UnknownOffer = 3,
        Closed = 4,
        OutOfStock = 5,
        EmptyBasket = 6,
        InsufficientFunds = 7,
        TransactionRejected = 8,
        PendingFulfillment = 9,
        InvalidRequest = 10,
        OrderAlreadyActive = 11,
        VehicleOutcomeUnavailable = 12,
        InspectionUnavailable = 13,
        HandoffUnavailable = 14,
    }

    public enum ServiceOperationPhase
    {
        Debited = 0,
        Fulfilling = 1,
        Completed = 2,
    }

    public enum WorkshopOrderPhase
    {
        None = 0,
        InProgress = 1,
        ApplyingOutcomes = 2,
        Ready = 3,
    }

    public enum InspectionOrderPhase
    {
        None = 0,
        PaidPendingAssessment = 1,
        Passed = 2,
        Failed = 3,
        Deferred = 4,
    }

    public enum FuelGrade
    {
        Gasoline98 = 0,
        Diesel = 1,
        FuelOil = 2,
    }

    public readonly struct ServiceResult
    {
        public ServiceResult(
            bool succeeded,
            ServiceFailureReason failureReason,
            string operationId = "",
            long amountMinorUnits = 0,
            bool wasIdempotentReplay = false)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            OperationId = operationId ?? string.Empty;
            AmountMinorUnits = amountMinorUnits;
            WasIdempotentReplay = wasIdempotentReplay;
        }

        public bool Succeeded { get; }
        public ServiceFailureReason FailureReason { get; }
        public string OperationId { get; }
        public long AmountMinorUnits { get; }
        public bool WasIdempotentReplay { get; }
    }

    public readonly struct ServiceHandoffRequest
    {
        public ServiceHandoffRequest(
            string operationId,
            int lineIndex,
            string locationId,
            string offerId,
            string itemDefinitionId,
            string effectId,
            int variantIndex,
            int quantity,
            string handoffAnchorId)
        {
            OperationId = operationId ?? string.Empty;
            LineIndex = lineIndex;
            LocationId = locationId ?? string.Empty;
            OfferId = offerId ?? string.Empty;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            EffectId = effectId ?? string.Empty;
            VariantIndex = variantIndex;
            Quantity = quantity;
            HandoffAnchorId = handoffAnchorId ?? string.Empty;
        }

        public string OperationId { get; }
        public int LineIndex { get; }
        public string LocationId { get; }
        public string OfferId { get; }
        public string ItemDefinitionId { get; }
        public string EffectId { get; }
        public int VariantIndex { get; }
        public int Quantity { get; }
        public string HandoffAnchorId { get; }
    }

    public interface IServiceHandoffBackend
    {
        bool TryFulfill(in ServiceHandoffRequest request, out string failure);
    }

    /// <summary>
    /// Optional non-mutating fulfillment check used before money is committed.
    /// Production handoff backends implement this when an item definition or
    /// service effect may be unavailable in the current build.
    /// </summary>
    public interface IServiceHandoffPreflight
    {
        bool CanFulfill(in ServiceHandoffRequest request, out string failure);
    }

    /// <summary>
    /// Applies non-item service results such as a consumed pub drink. Calls are
    /// idempotent by operation ID and line index.
    /// </summary>
    public interface IServiceEffectHandoffBackend
    {
        bool TryApply(
            string operationId,
            int lineIndex,
            string effectId,
            int quantity,
            out string failure);
    }

    public interface IServiceEffectHandoffPreflight
    {
        bool CanApply(
            string effectId,
            int quantity,
            out string failure);
    }

    /// <summary>
    /// Optional staffing authority. Catalog hours remain authoritative for the
    /// clock; this boundary prevents a checkout from operating while its NPC is
    /// absent or still walking to the counter.
    /// </summary>
    public interface IServiceAvailabilitySource
    {
        bool IsLocationStaffed(string locationId);
    }

    /// <summary>
    /// Optional world-distance boundary used by donor rules that deliberately
    /// pause while the player is nearby. The Services module never searches the
    /// streamed world or player hierarchy itself.
    /// </summary>
    public interface IServiceProximitySource
    {
        bool IsPlayerWithin(string locationId, float distanceMeters);
    }

    public readonly struct WorkshopOutcomeRequest
    {
        public WorkshopOutcomeRequest(
            string orderId,
            string playerVehicleStableId,
            string[] offerIds,
            int paintVariant,
            int rimVariant,
            int tireVariant,
            float finalGearRatio)
        {
            OrderId = orderId ?? string.Empty;
            PlayerVehicleStableId = playerVehicleStableId ?? string.Empty;
            OfferIds = offerIds ?? Array.Empty<string>();
            PaintVariant = paintVariant;
            RimVariant = rimVariant;
            TireVariant = tireVariant;
            FinalGearRatio = finalGearRatio;
        }

        public string OrderId { get; }
        public string PlayerVehicleStableId { get; }
        public string[] OfferIds { get; }
        public int PaintVariant { get; }
        public int RimVariant { get; }
        public int TireVariant { get; }
        public float FinalGearRatio { get; }
    }

    /// <summary>
    /// Vehicle-owned Fleetari outcome boundary. Implementations must be
    /// idempotent by <see cref="WorkshopOutcomeRequest.OrderId"/> because a
    /// save may be captured after the physical mutation but before Services
    /// records the completed outcome.
    /// </summary>
    public interface IWorkshopOutcomeBackend
    {
        bool IsPlayerVehicleWithin(float distanceMeters);
        bool IsLoanerWithin(float distanceMeters);
        bool TryApply(in WorkshopOutcomeRequest request, out string failure);
        bool TryRelocatePlayerVehicleForOverdueLoaner(out string failure);
    }

    public readonly struct InspectionAssessment
    {
        public InspectionAssessment(bool passed, string reportCode)
        {
            Passed = passed;
            ReportCode = reportCode ?? string.Empty;
        }

        public bool Passed { get; }
        public string ReportCode { get; }
    }

    public interface IInspectionAssessmentBackend
    {
        bool TryAssess(out InspectionAssessment assessment, out string failure);
    }

    public readonly struct ServiceNotification
    {
        public ServiceNotification(
            string notificationId,
            string fallbackText,
            Vector3 worldPosition)
        {
            NotificationId = notificationId ?? string.Empty;
            FallbackText = fallbackText ?? string.Empty;
            WorldPosition = worldPosition;
        }

        public string NotificationId { get; }
        public string FallbackText { get; }
        public Vector3 WorldPosition { get; }
    }

    internal static class ServiceStableId
    {
        public static bool IsCanonical(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '-' &&
                    character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
