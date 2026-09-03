using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;

namespace MSC.Services
{
    [Serializable]
    public sealed class RetailStockStateDto
    {
        public string offerId = string.Empty;
        public int remaining;

        public RetailStockStateDto DeepClone() =>
            (RetailStockStateDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class StoreBasketLineDto
    {
        public string offerId = string.Empty;
        public int quantity;

        public StoreBasketLineDto DeepClone() =>
            (StoreBasketLineDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class FuelDebtStateDto
    {
        public int grade;
        public long dispensedMilliliters;
        public long chargeMinorUnits;

        public FuelDebtStateDto DeepClone() =>
            (FuelDebtStateDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class FuelPriceStateDto
    {
        public int grade;
        public long currentMinorUnitsPerLiter;

        public FuelPriceStateDto DeepClone() =>
            (FuelPriceStateDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class ServiceHandoffLineDto
    {
        public string offerId = string.Empty;
        public string itemDefinitionId = string.Empty;
        public string effectId = string.Empty;
        public int variantIndex;
        public int quantity;
        public bool completed;

        public ServiceHandoffLineDto DeepClone() =>
            (ServiceHandoffLineDto)MemberwiseClone();
    }

    [Serializable]
    public sealed class ServiceOperationStateDto
    {
        public string operationId = string.Empty;
        public string transactionId = string.Empty;
        public string locationId = string.Empty;
        public int phase;
        public long amountMinorUnits;
        public ServiceHandoffLineDto[] handoffLines =
            Array.Empty<ServiceHandoffLineDto>();

        public ServiceOperationStateDto DeepClone() =>
            new()
            {
                operationId = operationId,
                transactionId = transactionId,
                locationId = locationId,
                phase = phase,
                amountMinorUnits = amountMinorUnits,
                handoffLines = (handoffLines ??
                    Array.Empty<ServiceHandoffLineDto>())
                    .Select(value => value?.DeepClone())
                    .ToArray(),
            };
    }

    [Serializable]
    public sealed class WorkshopSelectionStateDto
    {
        public string[] offerIds = Array.Empty<string>();
        public int paintVariant = -1;
        public int rimVariant = -1;
        public int tireVariant = -1;
        public float finalGearRatio = 4.286f;

        public WorkshopSelectionStateDto DeepClone() =>
            new()
            {
                offerIds = (string[])(offerIds ?? Array.Empty<string>()).Clone(),
                paintVariant = paintVariant,
                rimVariant = rimVariant,
                tireVariant = tireVariant,
                finalGearRatio = finalGearRatio,
            };
    }

    [Serializable]
    public sealed class WorkshopOrderStateDto
    {
        public int phase;
        public string orderId = string.Empty;
        public string transactionId = string.Empty;
        public long amountMinorUnits;
        public long quotedBaseMinorUnits;
        public int discountBasisPoints;
        public float initialWorkRealSeconds;
        public float remainingWorkRealSeconds;
        public float outcomeDelayRealSeconds;
        public bool outcomeApplied;
        public bool outcomeDeferred;
        public bool loanerOffered;
        public bool loanerBorrowed;
        public bool loanerReturned;
        public bool repairReadyNotified;
        public float loanerMissingRealSeconds;
        public bool loanerWarningNotified;
        public bool loanerOverdue;
        public float loanerOverdueRealSeconds;
        public bool relocatePlayerVehicleOnNextSleep;
        public string playerVehicleStableId = string.Empty;
        public string loanerVehicleStableId = string.Empty;
        public WorkshopSelectionStateDto orderedSelection = new();

        public WorkshopOrderStateDto DeepClone() =>
            new()
            {
                phase = phase,
                orderId = orderId,
                transactionId = transactionId,
                amountMinorUnits = amountMinorUnits,
                quotedBaseMinorUnits = quotedBaseMinorUnits,
                discountBasisPoints = discountBasisPoints,
                initialWorkRealSeconds = initialWorkRealSeconds,
                remainingWorkRealSeconds = remainingWorkRealSeconds,
                outcomeDelayRealSeconds = outcomeDelayRealSeconds,
                outcomeApplied = outcomeApplied,
                outcomeDeferred = outcomeDeferred,
                loanerOffered = loanerOffered,
                loanerBorrowed = loanerBorrowed,
                loanerReturned = loanerReturned,
                repairReadyNotified = repairReadyNotified,
                loanerMissingRealSeconds = loanerMissingRealSeconds,
                loanerWarningNotified = loanerWarningNotified,
                loanerOverdue = loanerOverdue,
                loanerOverdueRealSeconds = loanerOverdueRealSeconds,
                relocatePlayerVehicleOnNextSleep =
                    relocatePlayerVehicleOnNextSleep,
                playerVehicleStableId = playerVehicleStableId,
                loanerVehicleStableId = loanerVehicleStableId,
                orderedSelection = orderedSelection?.DeepClone() ?? new(),
            };
    }

    [Serializable]
    public sealed class InspectionOrderStateDto
    {
        public int phase;
        public string operationId = string.Empty;
        public string transactionId = string.Empty;
        public long amountMinorUnits;
        public string reportCode = string.Empty;

        public InspectionOrderStateDto DeepClone() =>
            (InspectionOrderStateDto)MemberwiseClone();
    }

    public enum HomeMailOrderPhase
    {
        None = 0,
        EnvelopeCreated = 1,
        Submitted = 2,
        ReadyForPayment = 3,
        PaidPendingMaterialization = 4,
        Delivered = 5,
    }

    [Serializable]
    public sealed class HomeMailOrderStateDto
    {
        public int phase;
        public string orderId = string.Empty;
        public string transactionId = string.Empty;
        public string envelopeStableEntityId = string.Empty;
        public string[] draftOfferIds = Array.Empty<string>();
        public string[] orderedOfferIds = Array.Empty<string>();
        public long amountMinorUnits;
        public double submittedElapsedGameSeconds;
        public double readyElapsedGameSeconds;

        public HomeMailOrderStateDto DeepClone() =>
            new()
            {
                phase = phase,
                orderId = orderId,
                transactionId = transactionId,
                envelopeStableEntityId = envelopeStableEntityId,
                draftOfferIds = (string[])(draftOfferIds ??
                    Array.Empty<string>()).Clone(),
                orderedOfferIds = (string[])(orderedOfferIds ??
                    Array.Empty<string>()).Clone(),
                amountMinorUnits = amountMinorUnits,
                submittedElapsedGameSeconds = submittedElapsedGameSeconds,
                readyElapsedGameSeconds = readyElapsedGameSeconds,
            };
    }

    [Serializable]
    public sealed class ServiceStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string catalogId = string.Empty;
        public ulong revision;
        public long nextOperationSequence = 1;
        public long lastProcessedDayIndex;
        public bool restockPending;
        public RetailStockStateDto[] retailStocks =
            Array.Empty<RetailStockStateDto>();
        public StoreBasketLineDto[] storeBasket =
            Array.Empty<StoreBasketLineDto>();
        public FuelDebtStateDto[] fuelDebts =
            Array.Empty<FuelDebtStateDto>();
        public FuelPriceStateDto[] fuelPrices =
            Array.Empty<FuelPriceStateDto>();
        public long brokenWindowChargeMinorUnits;
        public bool fuelStolen;
        public ServiceOperationStateDto[] pendingOperations =
            Array.Empty<ServiceOperationStateDto>();
        public WorkshopSelectionStateDto workshopSelection = new();
        public WorkshopOrderStateDto workshopOrder = new();
        public InspectionOrderStateDto inspectionOrder = new();
        public HomeMailOrderStateDto homeMailOrder = new();

        public ServiceStateDto DeepClone() =>
            new()
            {
                schemaVersion = schemaVersion,
                catalogId = catalogId,
                revision = revision,
                nextOperationSequence = nextOperationSequence,
                lastProcessedDayIndex = lastProcessedDayIndex,
                restockPending = restockPending,
                retailStocks = Clone(retailStocks),
                storeBasket = Clone(storeBasket),
                fuelDebts = Clone(fuelDebts),
                fuelPrices = Clone(fuelPrices),
                brokenWindowChargeMinorUnits = brokenWindowChargeMinorUnits,
                fuelStolen = fuelStolen,
                pendingOperations = Clone(pendingOperations),
                workshopSelection = workshopSelection?.DeepClone() ?? new(),
                workshopOrder = workshopOrder?.DeepClone() ?? new(),
                inspectionOrder = inspectionOrder?.DeepClone() ?? new(),
                homeMailOrder = homeMailOrder?.DeepClone() ?? new(),
            };

        public bool TryValidate(ServiceCatalog catalog, out string failure)
        {
            if (catalog == null || schemaVersion != CurrentSchemaVersion ||
                !string.Equals(catalogId, catalog.CatalogId, StringComparison.Ordinal) ||
                nextOperationSequence < 1 || lastProcessedDayIndex < 0 ||
                brokenWindowChargeMinorUnits < 0 ||
                retailStocks == null || retailStocks.Length > 256 ||
                storeBasket == null || storeBasket.Length > 256 ||
                fuelDebts == null || fuelDebts.Length > 3 ||
                fuelPrices == null || fuelPrices.Length > 3 ||
                pendingOperations == null || pendingOperations.Length > 128 ||
                workshopSelection == null || workshopOrder == null ||
                inspectionOrder == null)
            {
                failure = "Service save header or collection bounds are invalid.";
                return false;
            }

            var stockIds = new HashSet<string>(StringComparer.Ordinal);
            var stocksByOfferId =
                new Dictionary<string, RetailStockStateDto>(StringComparer.Ordinal);
            foreach (RetailStockStateDto stock in retailStocks)
            {
                if (stock == null ||
                    !catalog.TryGetOffer(stock.offerId, out ServiceOfferDefinition offer) ||
                    offer.Kind != ServiceOfferKind.RetailItem ||
                    stock.remaining < 0 || stock.remaining > offer.StockCapacity ||
                    !stockIds.Add(stock.offerId) ||
                    !stocksByOfferId.TryAdd(stock.offerId, stock))
                {
                    failure = "Service retail stock state is invalid or duplicated.";
                    return false;
                }
            }

            int expectedStocks = catalog.Offers.Count(
                value => value.Kind == ServiceOfferKind.RetailItem);
            if (stockIds.Count != expectedStocks)
            {
                failure = "Service save does not contain every retail stock line.";
                return false;
            }

            var basketIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoreBasketLineDto line in storeBasket)
            {
                if (line == null || line.quantity <= 0 ||
                    !catalog.TryGetOffer(line.offerId, out ServiceOfferDefinition offer) ||
                    offer.Kind != ServiceOfferKind.RetailItem ||
                    !stocksByOfferId.TryGetValue(
                        line.offerId,
                        out RetailStockStateDto stock) ||
                    (long)stock.remaining + line.quantity > offer.StockCapacity ||
                    !basketIds.Add(line.offerId))
                {
                    failure = "Service store basket is invalid or duplicated.";
                    return false;
                }
            }

            var grades = new HashSet<FuelGrade>();
            foreach (FuelDebtStateDto debt in fuelDebts)
            {
                if (debt == null || !Enum.IsDefined(typeof(FuelGrade), debt.grade) ||
                    debt.dispensedMilliliters <= 0 || debt.chargeMinorUnits < 0 ||
                    !catalog.TryGetFuelPrice((FuelGrade)debt.grade, out _) ||
                    !grades.Add((FuelGrade)debt.grade))
                {
                    failure = "Service fuel debt state is invalid or duplicated.";
                    return false;
                }
            }

            if (fuelStolen && grades.Count == 0)
            {
                failure = "Service fuel theft state requires unpaid fuel debt.";
                return false;
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ServiceOperationStateDto operation in pendingOperations)
            {
                if (operation == null ||
                    !ServiceStableId.IsCanonical(operation.operationId) ||
                    !ServiceStableId.IsCanonical(operation.transactionId) ||
                    !catalog.TryGetLocation(operation.locationId, out _) ||
                    !Enum.IsDefined(typeof(ServiceOperationPhase), operation.phase) ||
                    operation.amountMinorUnits <= 0 ||
                    operation.handoffLines == null ||
                    operation.handoffLines.Length > 256 ||
                    !operationIds.Add(operation.operationId) ||
                    !transactionIds.Add(operation.transactionId))
                {
                    failure = "Pending service operation is invalid or duplicated.";
                    return false;
                }

                if (operation.handoffLines.Length == 0 &&
                    (ServiceOperationPhase)operation.phase !=
                        ServiceOperationPhase.Completed)
                {
                    failure = "An unfinished service operation has no handoff lines.";
                    return false;
                }

                foreach (ServiceHandoffLineDto line in operation.handoffLines)
                {
                    if (line == null || line.quantity <= 0 || line.variantIndex < 0 ||
                        !catalog.TryGetOffer(line.offerId, out ServiceOfferDefinition offer) ||
                        !string.Equals(
                            line.itemDefinitionId,
                            offer.ItemDefinitionId,
                            StringComparison.Ordinal) ||
                        !string.Equals(line.effectId, offer.EffectId, StringComparison.Ordinal))
                    {
                        failure = "Pending service handoff line is invalid.";
                        return false;
                    }
                }

                bool allHandoffsCompleted = operation.handoffLines.All(
                    line => line.completed);
                if (((ServiceOperationPhase)operation.phase ==
                     ServiceOperationPhase.Completed) != allHandoffsCompleted)
                {
                    failure =
                        "Service operation phase does not match handoff completion.";
                    return false;
                }
            }

            var fuelPriceGrades = new HashSet<FuelGrade>();
            foreach (FuelPriceStateDto price in fuelPrices)
            {
                ServiceFuelPriceDefinition definition;
                if (price == null ||
                    !Enum.IsDefined(typeof(FuelGrade), price.grade) ||
                    !catalog.TryGetFuelPrice(
                        (FuelGrade)price.grade,
                        out definition) ||
                    !IsValidFuelPrice(price.currentMinorUnitsPerLiter, definition) ||
                    !fuelPriceGrades.Add((FuelGrade)price.grade))
                {
                    failure = "Service fuel price state is invalid or duplicated.";
                    return false;
                }
            }

            if (fuelPriceGrades.Count != catalog.FuelPrices.Count)
            {
                failure = "Service save does not contain every fuel price.";
                return false;
            }

            if (!ValidateWorkshopSelection(catalog, workshopSelection, out failure) ||
                !ValidateWorkshopOrder(catalog, workshopOrder, out failure) ||
                !ValidateInspection(inspectionOrder, out failure) ||
                !ValidateHomeMailOrder(homeMailOrder ?? new(), out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateHomeMailOrder(
            HomeMailOrderStateDto order,
            out string failure)
        {
            if (!Enum.IsDefined(typeof(HomeMailOrderPhase), order.phase) ||
                order.draftOfferIds == null || order.draftOfferIds.Length > 46 ||
                order.orderedOfferIds == null || order.orderedOfferIds.Length > 46 ||
                order.amountMinorUnits < 0 ||
                !double.IsFinite(order.submittedElapsedGameSeconds) ||
                !double.IsFinite(order.readyElapsedGameSeconds) ||
                order.submittedElapsedGameSeconds < 0d ||
                order.readyElapsedGameSeconds < 0d)
            {
                failure = "Home mail-order state bounds are invalid.";
                return false;
            }

            var draftIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string offerId in order.draftOfferIds)
            {
                if (!HomePartsMailOrderCatalog.TryGet(offerId, out _) ||
                    !draftIds.Add(offerId))
                {
                    failure = "Home mail-order draft is invalid or duplicated.";
                    return false;
                }
            }

            var orderedIds = new HashSet<string>(StringComparer.Ordinal);
            long expectedAmount = 0L;
            foreach (string offerId in order.orderedOfferIds)
            {
                if (!HomePartsMailOrderCatalog.TryGet(
                        offerId,
                        out HomePartsMailOrderOffer offer) ||
                    !orderedIds.Add(offerId))
                {
                    failure = "Home mail-order lines are invalid or duplicated.";
                    return false;
                }

                expectedAmount = checked(expectedAmount + offer.PriceMinorUnits);
            }

            HomeMailOrderPhase phase = (HomeMailOrderPhase)order.phase;
            bool empty = phase == HomeMailOrderPhase.None;
            if (empty)
            {
                if (order.orderId.Length != 0 || order.transactionId.Length != 0 ||
                    order.envelopeStableEntityId.Length != 0 ||
                    order.orderedOfferIds.Length != 0 ||
                    order.amountMinorUnits != 0L ||
                    order.submittedElapsedGameSeconds != 0d ||
                    order.readyElapsedGameSeconds != 0d)
                {
                    failure = "Empty home mail-order state contains active order data.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            if (!ServiceStableId.IsCanonical(order.orderId) ||
                !ServiceStableId.IsCanonical(order.transactionId) ||
                order.orderedOfferIds.Length == 0 ||
                order.amountMinorUnits <= 0L ||
                order.amountMinorUnits != expectedAmount ||
                !StableEntityId.TryParse(
                    order.envelopeStableEntityId,
                    out _))
            {
                failure = "Active home mail-order identity or amount is invalid.";
                return false;
            }

            bool submitted = phase >= HomeMailOrderPhase.Submitted;
            if (submitted && order.readyElapsedGameSeconds <=
                    order.submittedElapsedGameSeconds ||
                !submitted && (order.submittedElapsedGameSeconds != 0d ||
                    order.readyElapsedGameSeconds != 0d))
            {
                failure = "Home mail-order delivery timestamps do not match its phase.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsValidFuelPrice(
            long currentMinorUnitsPerLiter,
            ServiceFuelPriceDefinition definition)
        {
            // The donor-authored initial price is a persisted first-session value.
            // Thursday restocks roll only inside the later min/max range, and the
            // diesel initial value (4.23 MK/L) intentionally sits above that range.
            return currentMinorUnitsPerLiter ==
                       definition.InitialMinorUnitsPerLiter ||
                   currentMinorUnitsPerLiter >=
                       definition.MinimumMinorUnitsPerLiter &&
                   currentMinorUnitsPerLiter <=
                       definition.MaximumMinorUnitsPerLiter;
        }

        private static bool ValidateWorkshopSelection(
            ServiceCatalog catalog,
            WorkshopSelectionStateDto selection,
            out string failure)
        {
            if (selection.offerIds == null || selection.offerIds.Length > 64 ||
                !float.IsFinite(selection.finalGearRatio) ||
                selection.finalGearRatio < 2f || selection.finalGearRatio > 6f)
            {
                failure = "Workshop selection bounds are invalid.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (string offerId in selection.offerIds)
            {
                if (!catalog.TryGetOffer(offerId, out ServiceOfferDefinition offer) ||
                    offer.Kind != ServiceOfferKind.Workshop || !ids.Add(offerId) ||
                    !string.IsNullOrEmpty(offer.ExclusiveGroupId) &&
                    !groups.Add(offer.ExclusiveGroupId))
                {
                    failure = "Workshop selection is invalid or violates an exclusive group.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateWorkshopOrder(
            ServiceCatalog catalog,
            WorkshopOrderStateDto order,
            out string failure)
        {
            string selectionFailure = "Workshop selection is missing.";
            if (order.orderedSelection == null ||
                !ValidateWorkshopSelection(
                    catalog,
                    order.orderedSelection,
                    out selectionFailure))
            {
                failure = selectionFailure;
                return false;
            }

            if (!Enum.IsDefined(typeof(WorkshopOrderPhase), order.phase) ||
                !float.IsFinite(order.remainingWorkRealSeconds) ||
                !float.IsFinite(order.initialWorkRealSeconds) ||
                !float.IsFinite(order.outcomeDelayRealSeconds) ||
                !float.IsFinite(order.loanerMissingRealSeconds) ||
                !float.IsFinite(order.loanerOverdueRealSeconds) ||
                order.initialWorkRealSeconds < 0f ||
                order.remainingWorkRealSeconds < 0f ||
                order.outcomeDelayRealSeconds < 0f ||
                order.loanerMissingRealSeconds < 0f ||
                order.loanerOverdueRealSeconds < 0f ||
                order.discountBasisPoints < 0 ||
                order.discountBasisPoints > 10_000 ||
                order.quotedBaseMinorUnits < 0)
            {
                failure = "Workshop order state is invalid.";
                return false;
            }

            WorkshopOrderPhase phase = (WorkshopOrderPhase)order.phase;
            bool empty = phase == WorkshopOrderPhase.None;
            if (empty != string.IsNullOrEmpty(order.orderId) ||
                empty != string.IsNullOrEmpty(order.transactionId) ||
                empty != (order.amountMinorUnits == 0) ||
                !empty && (!ServiceStableId.IsCanonical(order.orderId) ||
                    !ServiceStableId.IsCanonical(order.transactionId) ||
                    order.amountMinorUnits <= 0 ||
                    order.quotedBaseMinorUnits <= 0 ||
                    order.initialWorkRealSeconds < 1400f ||
                    order.initialWorkRealSeconds > 2600f ||
                    order.remainingWorkRealSeconds >
                        order.initialWorkRealSeconds ||
                    order.orderedSelection.offerIds.Length == 0))
            {
                failure = "Workshop order identity or amount is inconsistent.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateInspection(
            InspectionOrderStateDto order,
            out string failure)
        {
            if (!Enum.IsDefined(typeof(InspectionOrderPhase), order.phase) ||
                order.amountMinorUnits < 0)
            {
                failure = "Inspection order state is invalid.";
                return false;
            }

            bool empty = (InspectionOrderPhase)order.phase ==
                         InspectionOrderPhase.None;
            if (empty != string.IsNullOrEmpty(order.operationId) ||
                empty != string.IsNullOrEmpty(order.transactionId) ||
                empty != (order.amountMinorUnits == 0) ||
                !empty && (!ServiceStableId.IsCanonical(order.operationId) ||
                    !ServiceStableId.IsCanonical(order.transactionId) ||
                    order.amountMinorUnits <= 0))
            {
                failure = "Inspection order identity is inconsistent.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static T[] Clone<T>(T[] source) where T : class
        {
            T[] values = source ?? Array.Empty<T>();
            var result = new T[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                object value = values[index];
                result[index] = value switch
                {
                    RetailStockStateDto stock => stock.DeepClone() as T,
                    StoreBasketLineDto basket => basket.DeepClone() as T,
                    FuelDebtStateDto fuel => fuel.DeepClone() as T,
                    FuelPriceStateDto price => price.DeepClone() as T,
                    ServiceOperationStateDto operation => operation.DeepClone() as T,
                    _ => null,
                };
            }

            return result;
        }
    }
}
