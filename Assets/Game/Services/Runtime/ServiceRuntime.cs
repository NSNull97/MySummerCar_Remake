using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Time;
using MSC.Economy;
using UnityEngine;

namespace MSC.Services
{
    /// <summary>
    /// Authoritative Phase-1 commerce state. Presentation, NPC schedules, item
    /// spawning and vehicles are adapters; none of them own money or orders.
    /// </summary>
    public sealed class ServiceRuntime : MonoBehaviour
    {
        private const string StoreLocationId = "service.location.teimo-store";
        private const string PubLocationId = "service.location.teimo-pub";
        private const string FuelLocationId = "service.location.teimo-fuel";
        private const string DieselFuelLocationId =
            "service.location.teimo-fuel-diesel";
        private const string FuelOilLocationId =
            "service.location.teimo-fuel-oil";
        private const string WorkshopLocationId =
            "service.location.workshop.fleetari";
        private const string InspectionLocationId =
            "service.location.inspection-station";
        private const float StoreRestockExclusionMeters = 50f;
        private const int MaximumPersistedOperations = 128;
        private const int CompletedOperationPruneTarget = 96;
        // Donor Delivery FSM rolls Arrival in [1999, 2000]. The project clock
        // maps that audited counter to game minutes until a narrower donor
        // clock-unit trace is approved.
        private const double MailOrderMinimumDeliveryGameSeconds = 1999d * 60d;
        private const double MailOrderMaximumDeliveryGameSeconds = 2000d * 60d;

        private readonly Dictionary<string, RetailStockStateDto> stocks =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, StoreBasketLineDto> basket =
            new(StringComparer.Ordinal);
        private readonly Dictionary<FuelGrade, FuelDebtStateDto> fuelDebts = new();
        private readonly Dictionary<FuelGrade, FuelPriceStateDto> fuelPrices = new();
        private readonly Dictionary<string, ServiceOperationStateDto> operations =
            new(StringComparer.Ordinal);

        private ServiceCatalog catalog;
        private IEconomyTransactionService economy;
        private IGameTimeService gameTime;
        private IServiceAvailabilitySource availability;
        private IServiceProximitySource proximity;
        private IServiceHandoffBackend handoff;
        private IInspectionAssessmentBackend inspection;
        private WorkshopServiceEngine workshop;
        private ServiceStateDto state;
        private IDisposable timeSubscription;
        private bool initialized;

        public ServiceCatalog Catalog => catalog;
        public ulong Revision => state?.revision ?? 0;
        public bool IsInitialized => initialized;
        public WorkshopOrderPhase WorkshopPhase =>
            workshop?.Phase ?? WorkshopOrderPhase.None;
        public bool WorkshopOrderingAvailable =>
            initialized && workshop != null && workshop.CanApplyOutcomes;
        public bool InspectionOrderingAvailable =>
            initialized && inspection != null;
        public HomeMailOrderPhase CurrentHomeMailOrderPhase =>
            state?.homeMailOrder == null
                ? HomeMailOrderPhase.None
                : (HomeMailOrderPhase)state.homeMailOrder.phase;
        public int HomeMailOrderDraftCount =>
            state?.homeMailOrder?.draftOfferIds?.Length ?? 0;
        public long HomeMailOrderDraftTotalMinorUnits =>
            SumHomeMailOrderOffers(
                state?.homeMailOrder?.draftOfferIds ?? Array.Empty<string>());

        public event Action<ulong> StateChanged;
        public event Action<ServiceNotification> NotificationRaised;

        public void Initialize(
            ServiceCatalog configuredCatalog,
            IEconomyTransactionService configuredEconomy,
            IGameTimeService configuredGameTime,
            IServiceAvailabilitySource configuredAvailability = null,
            IServiceProximitySource configuredProximity = null,
            IServiceHandoffBackend configuredHandoff = null,
            IInspectionAssessmentBackend configuredInspection = null,
            IWorkshopOutcomeBackend configuredWorkshopOutcome = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Service runtime is already initialized.");
            }

            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            economy = configuredEconomy ??
                throw new ArgumentNullException(nameof(configuredEconomy));
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            if (!catalog.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredCatalog));
            }

            if (!TryValidateRuntimeCatalog(out failure))
            {
                throw new ArgumentException(failure, nameof(configuredCatalog));
            }

            availability = configuredAvailability;
            proximity = configuredProximity;
            handoff = configuredHandoff;
            inspection = configuredInspection;
            workshop = new WorkshopServiceEngine(
                catalog,
                economy,
                WorkshopLocationId,
                configuredWorkshopOutcome);
            ApplyState(CreateFreshState(catalog, gameTime.Snapshot));
            RestoreWorkshopOrThrow(
                state.workshopSelection,
                state.workshopOrder);
            timeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            initialized = true;
        }

        public static ServiceStateDto CreateFreshState(
            ServiceCatalog catalog,
            GameTimeSnapshot snapshot)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(catalog));
            }

            return new ServiceStateDto
            {
                catalogId = catalog.CatalogId,
                revision = 0,
                nextOperationSequence = 1,
                lastProcessedDayIndex = snapshot.DayIndex,
                restockPending = false,
                retailStocks = catalog.Offers
                    .Where(value => value.Kind == ServiceOfferKind.RetailItem)
                    .OrderBy(value => value.OfferId, StringComparer.Ordinal)
                    .Select(value => new RetailStockStateDto
                    {
                        offerId = value.OfferId,
                        remaining = value.StockCapacity,
                    })
                    .ToArray(),
                storeBasket = Array.Empty<StoreBasketLineDto>(),
                fuelDebts = Array.Empty<FuelDebtStateDto>(),
                fuelPrices = catalog.FuelPrices
                    .OrderBy(value => (int)value.Grade)
                    .Select(value => new FuelPriceStateDto
                    {
                        grade = (int)value.Grade,
                        currentMinorUnitsPerLiter =
                            value.InitialMinorUnitsPerLiter,
                    })
                    .ToArray(),
                pendingOperations = Array.Empty<ServiceOperationStateDto>(),
                workshopSelection = new WorkshopSelectionStateDto(),
                workshopOrder = new WorkshopOrderStateDto(),
                inspectionOrder = new InspectionOrderStateDto(),
                homeMailOrder = new HomeMailOrderStateDto(),
            };
        }

        public bool IsHomeMailOrderDraftSelected(string offerId)
        {
            EnsureInitialized();
            return (state.homeMailOrder?.draftOfferIds ?? Array.Empty<string>())
                .Contains(offerId ?? string.Empty, StringComparer.Ordinal);
        }

        public bool TryToggleHomeMailOrderDraft(string offerId)
        {
            EnsureInitialized();
            if (!HomePartsMailOrderCatalog.TryGet(offerId, out _))
            {
                return false;
            }

            HomeMailOrderStateDto order = EnsureHomeMailOrderState();
            HomeMailOrderPhase phase = (HomeMailOrderPhase)order.phase;
            if (phase != HomeMailOrderPhase.None &&
                phase != HomeMailOrderPhase.Delivered)
            {
                return false;
            }

            if (phase == HomeMailOrderPhase.Delivered)
            {
                state.homeMailOrder = order = new HomeMailOrderStateDto();
            }

            var ids = new HashSet<string>(
                order.draftOfferIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (!ids.Add(offerId))
            {
                ids.Remove(offerId);
            }

            order.draftOfferIds = ids
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Touch();
            return true;
        }

        public ServiceResult TryCreateHomeMailOrderEnvelope(
            Func<HomeMailOrderEnvelopePlan, bool> materializeEnvelope)
        {
            EnsureInitialized();
            if (materializeEnvelope == null)
            {
                return Failure(ServiceFailureReason.HandoffUnavailable);
            }

            HomeMailOrderStateDto current = EnsureHomeMailOrderState();
            if ((HomeMailOrderPhase)current.phase != HomeMailOrderPhase.None ||
                current.draftOfferIds.Length == 0)
            {
                return Failure(current.draftOfferIds.Length == 0
                    ? ServiceFailureReason.InvalidRequest
                    : ServiceFailureReason.OrderAlreadyActive);
            }

            string[] orderedIds = current.draftOfferIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            HomePartsMailOrderOffer[] offers = orderedIds
                .Select(value =>
                {
                    HomePartsMailOrderCatalog.TryGet(value, out HomePartsMailOrderOffer offer);
                    return offer;
                })
                .ToArray();
            long amount = SumHomeMailOrderOffers(orderedIds);
            long sequence = state.nextOperationSequence;
            string orderId = $"operation.services.home-mail-order.{sequence}";
            string transactionId = TransactionId(orderId);
            string envelopeStableId = Guid.NewGuid().ToString("N");
            var plan = new HomeMailOrderEnvelopePlan(
                orderId,
                envelopeStableId,
                offers,
                amount);
            if (!CanUseOperationId(orderId) || !materializeEnvelope(plan))
            {
                return Failure(ServiceFailureReason.HandoffUnavailable);
            }

            state.nextOperationSequence = checked(sequence + 1);
            state.homeMailOrder = new HomeMailOrderStateDto
            {
                phase = (int)HomeMailOrderPhase.EnvelopeCreated,
                orderId = orderId,
                transactionId = transactionId,
                envelopeStableEntityId = envelopeStableId,
                draftOfferIds = Array.Empty<string>(),
                orderedOfferIds = orderedIds,
                amountMinorUnits = amount,
            };
            Touch();
            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                orderId,
                amount);
        }

        public ServiceResult TrySubmitHomeMailOrderEnvelope(
            string envelopeStableEntityId)
        {
            EnsureInitialized();
            HomeMailOrderStateDto order = EnsureHomeMailOrderState();
            if ((HomeMailOrderPhase)order.phase !=
                    HomeMailOrderPhase.EnvelopeCreated ||
                !string.Equals(
                    order.envelopeStableEntityId,
                    envelopeStableEntityId,
                    StringComparison.Ordinal))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            double submitted = gameTime.Snapshot.ElapsedGameSeconds;
            order.phase = (int)HomeMailOrderPhase.Submitted;
            order.submittedElapsedGameSeconds = submitted;
            order.readyElapsedGameSeconds = submitted +
                ResolveMailOrderDeliveryDuration(order.orderId);
            Touch();
            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                order.orderId,
                order.amountMinorUnits);
        }

        public ServiceResult TryPayAndMaterializeHomeMailOrder(
            Func<HomeMailOrderDeliveryPlan, bool> materializeDelivery)
        {
            EnsureInitialized();
            if (materializeDelivery == null)
            {
                return Failure(ServiceFailureReason.HandoffUnavailable);
            }

            if (!IsLocationOpen(StoreLocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            TryAdvanceHomeMailOrder(gameTime.Snapshot);
            HomeMailOrderStateDto order = EnsureHomeMailOrderState();
            HomeMailOrderPhase phase = (HomeMailOrderPhase)order.phase;
            if (phase != HomeMailOrderPhase.ReadyForPayment &&
                phase != HomeMailOrderPhase.PaidPendingMaterialization)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            ServiceResult debit;
            if (phase == HomeMailOrderPhase.ReadyForPayment)
            {
                if (!TryDebit(
                        order.orderId,
                        StoreLocationId,
                        order.amountMinorUnits,
                        out debit))
                {
                    return debit;
                }

                order.phase = (int)HomeMailOrderPhase.PaidPendingMaterialization;
                Touch();
            }
            else
            {
                debit = new ServiceResult(
                    true,
                    ServiceFailureReason.None,
                    order.orderId,
                    order.amountMinorUnits,
                    true);
            }

            HomePartsMailOrderOffer[] offers = order.orderedOfferIds
                .Select(value =>
                {
                    HomePartsMailOrderCatalog.TryGet(value, out HomePartsMailOrderOffer offer);
                    return offer;
                })
                .ToArray();
            if (!materializeDelivery(new HomeMailOrderDeliveryPlan(
                    order.orderId,
                    offers,
                    order.amountMinorUnits)))
            {
                return new ServiceResult(
                    false,
                    ServiceFailureReason.PendingFulfillment,
                    order.orderId,
                    order.amountMinorUnits,
                    debit.WasIdempotentReplay);
            }

            order.phase = (int)HomeMailOrderPhase.Delivered;
            Touch();
            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                order.orderId,
                order.amountMinorUnits,
                debit.WasIdempotentReplay);
        }

        public bool IsLocationOpen(string locationId)
        {
            EnsureInitialized();
            if (!catalog.TryGetLocation(locationId, out ServiceLocationDefinition location))
            {
                return false;
            }

            GameTimeSnapshot snapshot = gameTime.Snapshot;
            var date = new DateTime(
                snapshot.Date.Year,
                snapshot.Date.Month,
                snapshot.Date.Day);
            int minute = (int)(snapshot.SecondsOfDay / 60d);
            bool clockOpen = location.Availability.Any(
                window => window.Contains(date.DayOfWeek, minute));
            return clockOpen &&
                   (availability == null ||
                    availability.IsLocationStaffed(locationId));
        }

        public string AllocateOperationId(string purpose)
        {
            EnsureInitialized();
            if (!ServiceStableId.IsCanonical(purpose))
            {
                throw new ArgumentException(
                    "Service operation purpose is not canonical.",
                    nameof(purpose));
            }

            long sequence = state.nextOperationSequence;
            string operationId = $"operation.services.{purpose}.{sequence}";
            if (!CanUseOperationId(operationId))
            {
                throw new InvalidOperationException(
                    "Allocated service operation ID exceeds transaction limits.");
            }

            state.nextOperationSequence = checked(sequence + 1);
            Touch();
            return operationId;
        }

        public int GetRemainingStock(string offerId)
        {
            EnsureInitialized();
            return stocks.TryGetValue(offerId ?? string.Empty, out RetailStockStateDto stock)
                ? stock.remaining
                : 0;
        }

        public int GetBasketQuantity(string offerId)
        {
            EnsureInitialized();
            return basket.TryGetValue(offerId ?? string.Empty, out StoreBasketLineDto line)
                ? line.quantity
                : 0;
        }

        public long GetFuelDebtMinorUnits(FuelGrade grade)
        {
            EnsureInitialized();
            return fuelDebts.TryGetValue(grade, out FuelDebtStateDto debt)
                ? debt.chargeMinorUnits
                : 0;
        }

        public long GetFuelPriceMinorUnitsPerLiter(FuelGrade grade)
        {
            EnsureInitialized();
            return fuelPrices.TryGetValue(grade, out FuelPriceStateDto price)
                ? price.currentMinorUnitsPerLiter
                : 0;
        }

        public ServiceResult TryAddStoreItem(string offerId, int quantity = 1)
        {
            EnsureInitialized();
            if (quantity <= 0 ||
                !catalog.TryGetOffer(offerId, out ServiceOfferDefinition offer) ||
                offer.Kind != ServiceOfferKind.RetailItem)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (!IsLocationOpen(offer.LocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            RetailStockStateDto stock = stocks[offer.OfferId];
            if (stock.remaining < quantity)
            {
                return Failure(ServiceFailureReason.OutOfStock);
            }

            stock.remaining -= quantity;
            if (!basket.TryGetValue(offer.OfferId, out StoreBasketLineDto line))
            {
                line = new StoreBasketLineDto { offerId = offer.OfferId };
                basket.Add(line.offerId, line);
            }

            line.quantity = checked(line.quantity + quantity);
            Touch();
            return Success();
        }

        public ServiceResult TryRemoveStoreItem(string offerId, int quantity = 1)
        {
            EnsureInitialized();
            if (quantity <= 0 ||
                !basket.TryGetValue(offerId ?? string.Empty, out StoreBasketLineDto line) ||
                line.quantity < quantity ||
                !stocks.TryGetValue(line.offerId, out RetailStockStateDto stock))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            line.quantity -= quantity;
            stock.remaining = checked(stock.remaining + quantity);
            if (line.quantity == 0)
            {
                basket.Remove(line.offerId);
            }

            Touch();
            return Success();
        }

        public ServiceResult TryCheckoutStore(string operationId)
        {
            EnsureInitialized();
            if (!CanUseOperationId(operationId))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (operations.TryGetValue(
                    operationId,
                    out ServiceOperationStateDto existingOperation))
            {
                // A completed checkout consumes and clears its mutable cart,
                // fuel-debt and window-charge intent. An empty intent is the
                // canonical retry of that already recorded operation. Any new
                // intent paired with the same ID is a conflicting request and
                // must not inherit the old success result.
                if (!string.Equals(
                        existingOperation.locationId,
                        StoreLocationId,
                        StringComparison.Ordinal) ||
                    HasStoreCheckoutIntent())
                {
                    return Failure(ServiceFailureReason.InvalidRequest);
                }

                return TryReplayOperation(operationId, out ServiceResult replay)
                    ? replay
                    : Failure(ServiceFailureReason.TransactionRejected);
            }

            if (!IsLocationOpen(StoreLocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            long total;
            try
            {
                total = QuoteBasket();
                foreach (FuelDebtStateDto debt in fuelDebts.Values)
                {
                    total = checked(total + debt.chargeMinorUnits);
                }

                total = checked(total + state.brokenWindowChargeMinorUnits);
            }
            catch (InvalidOperationException)
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }
            catch (OverflowException)
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            if (total <= 0)
            {
                return Failure(ServiceFailureReason.EmptyBasket);
            }

            ServiceHandoffLineDto[] lines = basket.Values
                .OrderBy(value => value.offerId, StringComparer.Ordinal)
                .Select(value => CreateHandoffLine(value.offerId, value.quantity))
                .ToArray();
            if (!CanFulfillBeforeDebit(
                    operationId,
                    StoreLocationId,
                    lines))
            {
                return Failure(ServiceFailureReason.HandoffUnavailable);
            }

            if (!EnsureOperationCapacity())
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            if (!TryDebit(operationId, StoreLocationId, total, out ServiceResult debit))
            {
                return debit;
            }

            // The Economy ledger outlives the bounded Services operation cache.
            // A replay with no matching Services operation is therefore a
            // pruned/stale request, never permission to clear a new cart or
            // hand out goods for a historical debit.
            if (debit.WasIdempotentReplay)
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            PruneCompletedOperationsForCapacity();
            ServiceOperationStateDto operation = CreateOperation(
                operationId,
                StoreLocationId,
                total,
                lines);
            operations.Add(operation.operationId, operation);
            basket.Clear();
            fuelDebts.Clear();
            state.brokenWindowChargeMinorUnits = 0;
            state.fuelStolen = false;
            CompleteOrFulfill(operation);
            Touch();
            return ResultFor(operation, debit.WasIdempotentReplay);
        }

        public ServiceResult TryPurchasePub(
            string operationId,
            string offerId,
            int quantity = 1)
        {
            EnsureInitialized();
            if (!CanUseOperationId(operationId) || quantity <= 0 ||
                !catalog.TryGetOffer(offerId, out ServiceOfferDefinition offer) ||
                offer.Kind != ServiceOfferKind.PubItem)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            long total;
            ServiceHandoffLineDto requestedLine;
            try
            {
                total = checked(offer.BasePriceMinorUnits * quantity);
                requestedLine = CreateHandoffLine(offer.OfferId, quantity);
            }
            catch (Exception exception) when (
                exception is OverflowException ||
                exception is InvalidOperationException)
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            if (operations.TryGetValue(
                    operationId,
                    out ServiceOperationStateDto existingOperation))
            {
                if (!MatchesPubOperation(
                        existingOperation,
                        offer.LocationId,
                        total,
                        requestedLine))
                {
                    return Failure(ServiceFailureReason.InvalidRequest);
                }

                return TryReplayOperation(operationId, out ServiceResult replay)
                    ? replay
                    : Failure(ServiceFailureReason.TransactionRejected);
            }

            if (!IsLocationOpen(offer.LocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            if (!CanFulfillBeforeDebit(
                    operationId,
                    offer.LocationId,
                    new[] { requestedLine }))
            {
                return Failure(ServiceFailureReason.HandoffUnavailable);
            }

            if (!EnsureOperationCapacity())
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            if (!TryDebit(operationId, offer.LocationId, total, out ServiceResult debit))
            {
                return debit;
            }

            if (debit.WasIdempotentReplay)
            {
                return Failure(ServiceFailureReason.TransactionRejected);
            }

            PruneCompletedOperationsForCapacity();
            ServiceOperationStateDto operation = CreateOperation(
                operationId,
                offer.LocationId,
                total,
                new[] { requestedLine });
            operations.Add(operation.operationId, operation);
            CompleteOrFulfill(operation);
            Touch();
            return ResultFor(operation, debit.WasIdempotentReplay);
        }

        /// <summary>
        /// Non-mutating accounting preflight for a receiver's maximum possible
        /// accepted volume. Presentation calls this before mutating the physical
        /// receiver, then records the actual accepted volume below.
        /// </summary>
        public ServiceResult TryPreflightAcceptedFuel(
            FuelGrade grade,
            long maximumAcceptedMilliliters)
        {
            EnsureInitialized();
            if (!TryQuoteAcceptedFuel(
                    grade,
                    maximumAcceptedMilliliters,
                    out _,
                    out long totalCharge,
                    out ServiceFailureReason failureReason))
            {
                return Failure(failureReason);
            }

            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                amountMinorUnits: totalCharge);
        }

        /// <summary>
        /// Records volume already accepted by a Vehicle-owned fuel receiver.
        /// This method never mutates a vehicle or guesses tank capacity/type.
        /// Callers must not invoke it until a real receiver has reported the
        /// accepted volume; rejected or unavailable receivers record nothing.
        /// </summary>
        public ServiceResult TryRecordAcceptedFuel(
            FuelGrade grade,
            long acceptedMilliliters)
        {
            EnsureInitialized();
            if (!TryQuoteAcceptedFuel(
                    grade,
                    acceptedMilliliters,
                    out long totalMilliliters,
                    out long totalCharge,
                    out ServiceFailureReason failureReason))
            {
                return Failure(failureReason);
            }

            if (!fuelDebts.TryGetValue(grade, out FuelDebtStateDto debt))
            {
                debt = new FuelDebtStateDto { grade = (int)grade };
                fuelDebts.Add(grade, debt);
            }

            debt.dispensedMilliliters = totalMilliliters;
            debt.chargeMinorUnits = totalCharge;

            Touch();
            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                amountMinorUnits: debt.chargeMinorUnits);
        }

        /// <summary>
        /// Records the donor theft edge when a nozzle that dispensed fuel is
        /// released before checkout. Physical nozzle ownership stays in the
        /// presentation adapter; this method only advances service state.
        /// </summary>
        public ServiceResult TryReleaseFuelNozzle(FuelGrade grade)
        {
            EnsureInitialized();
            if (!fuelPrices.ContainsKey(grade) ||
                !fuelDebts.TryGetValue(grade, out FuelDebtStateDto debt) ||
                debt.dispensedMilliliters <= 0)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            if (state.fuelStolen)
            {
                return Success();
            }

            state.fuelStolen = true;
            Touch();
            return Success();
        }

        public ServiceResult SetBrokenWindowCharge(long chargeMinorUnits)
        {
            EnsureInitialized();
            if (chargeMinorUnits < 0)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            state.brokenWindowChargeMinorUnits = chargeMinorUnits;
            Touch();
            return Success();
        }

        public ServiceResult RetryFulfillment(string operationId)
        {
            EnsureInitialized();
            if (!operations.TryGetValue(
                    operationId ?? string.Empty,
                    out ServiceOperationStateDto operation))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            CompleteOrFulfill(operation);
            Touch();
            return ResultFor(operation, true);
        }

        public ServiceResult TryStartInspection(
            string operationId,
            string offerId)
        {
            EnsureInitialized();
            if (!CanUseOperationId(operationId) ||
                !catalog.TryGetOffer(offerId, out ServiceOfferDefinition offer) ||
                offer.Kind != ServiceOfferKind.Inspection)
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            InspectionOrderStateDto order = state.inspectionOrder;
            if (!string.IsNullOrEmpty(order.operationId))
            {
                return string.Equals(
                        order.operationId,
                        operationId,
                        StringComparison.Ordinal)
                    ? new ServiceResult(
                        true,
                        ServiceFailureReason.None,
                        operationId,
                        order.amountMinorUnits,
                        true)
                    : Failure(ServiceFailureReason.OrderAlreadyActive);
            }

            if (!IsLocationOpen(offer.LocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            // Donor ordering first verifies that a real vehicle is positioned
            // at the inspection checkpoint. The project has no Satsuma yet, so
            // a missing/failed assessment must not turn into a paid IOU for a
            // fictional future inspection.
            if (inspection == null ||
                !inspection.TryAssess(
                    out InspectionAssessment assessment,
                    out _))
            {
                return Failure(ServiceFailureReason.InspectionUnavailable);
            }

            if (!TryDebit(
                    operationId,
                    offer.LocationId,
                    offer.BasePriceMinorUnits,
                    out ServiceResult debit))
            {
                return debit;
            }

            order.operationId = operationId;
            order.transactionId = TransactionId(operationId);
            order.amountMinorUnits = offer.BasePriceMinorUnits;
            order.phase = (int)(assessment.Passed
                ? InspectionOrderPhase.Passed
                : InspectionOrderPhase.Failed);
            order.reportCode = assessment.ReportCode;

            Touch();
            return new ServiceResult(
                true,
                ServiceFailureReason.None,
                operationId,
                order.amountMinorUnits,
                debit.WasIdempotentReplay);
        }

        public WorkshopSelectionStateDto CaptureWorkshopSelection()
        {
            EnsureInitialized();
            return workshop.CaptureSelectionDto();
        }

        public WorkshopOrderStateDto CaptureWorkshopOrder()
        {
            EnsureInitialized();
            return workshop.CaptureOrderDto();
        }

        public ServiceResult TrySetWorkshopSelection(
            WorkshopSelectionStateDto selection)
        {
            EnsureInitialized();
            if (!IsLocationOpen(WorkshopLocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            ServiceResult result = workshop.TrySetSelection(selection);
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public bool TryQuoteWorkshop(
            out WorkshopPriceQuote quote,
            out ServiceFailureReason failure)
        {
            EnsureInitialized();
            if (!IsLocationOpen(WorkshopLocationId))
            {
                quote = default;
                failure = ServiceFailureReason.Closed;
                return false;
            }

            return workshop.TryQuote(CurrentDate(), out quote, out failure);
        }

        /// <summary>
        /// Places a donor-timed Fleetari order only when a real persistent
        /// player-vehicle target and an idempotent outcome backend are present.
        /// The current project has no Satsuma, so the production flow rejects
        /// the request before taking money instead of selling a fictional repair.
        /// </summary>
        public ServiceResult TryPlaceWorkshopOrder(
            string orderId,
            string playerVehicleStableId = "",
            string loanerVehicleStableId = "")
        {
            EnsureInitialized();
            if (!CanUseOperationId(orderId))
            {
                return Failure(ServiceFailureReason.InvalidRequest);
            }

            // There is currently no Satsuma (or any other authored player
            // vehicle service target) in the project. Never take money for a
            // repair that cannot be applied to a real persistent vehicle.
            if (!workshop.CanApplyOutcomes ||
                !ServiceStableId.IsCanonical(playerVehicleStableId))
            {
                return Failure(ServiceFailureReason.VehicleOutcomeUnavailable);
            }

            if (!IsLocationOpen(WorkshopLocationId))
            {
                return Failure(ServiceFailureReason.Closed);
            }

            ServiceResult result = workshop.TryPlaceOrder(
                orderId,
                TransactionId(orderId),
                CurrentDate(),
                ResolveWorkshopDuration(orderId),
                playerVehicleStableId,
                loanerVehicleStableId);
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceResult TryRetryDeferredWorkshopOutcome()
        {
            EnsureInitialized();
            ServiceResult result = workshop.TryRetryDeferredOutcome();
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceResult TryBorrowWorkshopLoaner(string loanerStableId)
        {
            EnsureInitialized();
            ServiceResult result = workshop.TryBorrowLoaner(loanerStableId);
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceResult TryReturnWorkshopLoaner()
        {
            EnsureInitialized();
            ServiceResult result = workshop.TryReturnLoaner();
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceResult TryProcessWorkshopLoanerSleep()
        {
            EnsureInitialized();
            ServiceResult result = workshop.TryProcessNextSleep();
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceResult TryAcknowledgeWorkshopOrder()
        {
            EnsureInitialized();
            ServiceResult result = workshop.TryAcknowledgeReadyOrder();
            if (result.Succeeded)
            {
                Touch();
            }

            return result;
        }

        public ServiceStateDto CaptureDto()
        {
            EnsureInitialized();
            SynchronizeArrays();
            return state.DeepClone();
        }

        public bool TryRestoreDto(ServiceStateDto dto, out string failure)
        {
            return TryRestoreDtoCore(
                dto,
                gameTime?.Snapshot.DayIndex ?? -1L,
                reconcileAgainstLiveClock: true,
                out failure);
        }

        /// <summary>
        /// Restores services while core.time is staged but not yet applied to
        /// the live clock. Restock reconciliation is deferred to the subsequent
        /// StateRestored notification from the authoritative clock.
        /// </summary>
        public bool TryRestoreDtoForStagedClock(
            ServiceStateDto dto,
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
            ServiceStateDto dto,
            long authoritativeDayIndex,
            bool reconcileAgainstLiveClock,
            out string failure)
        {
            EnsureInitialized();
            ServiceStateDto restoreCandidate =
                CreateAdditiveCatalogRestoreCandidate(dto);
            if (!TryValidateRestoreCandidateCore(
                    restoreCandidate,
                    out failure))
            {
                return false;
            }

            if (authoritativeDayIndex < 0L ||
                restoreCandidate.lastProcessedDayIndex >
                    authoritativeDayIndex)
            {
                failure = "Service state is ahead of authoritative game time.";
                return false;
            }

            if (!workshop.TryRestore(
                    restoreCandidate.workshopSelection,
                    restoreCandidate.workshopOrder,
                    out failure))
            {
                return false;
            }

            ApplyState(restoreCandidate);
            if (reconcileAgainstLiveClock)
            {
                TryAdvanceHomeMailOrder(gameTime.Snapshot);
                TryProcessRestock(gameTime.Snapshot);
            }

            Touch();
            failure = string.Empty;
            return true;
        }

        /// <summary>
        /// Performs the non-mutating portion of restore validation. Cross-domain
        /// time ordering is checked during Apply, after Core Time has restored.
        /// </summary>
        public bool TryValidateRestoreCandidate(
            ServiceStateDto dto,
            out string failure)
        {
            EnsureInitialized();
            return TryValidateRestoreCandidateCore(
                CreateAdditiveCatalogRestoreCandidate(dto),
                out failure);
        }

        private bool TryValidateRestoreCandidateCore(
            ServiceStateDto dto,
            out string failure)
        {
            if (dto == null)
            {
                failure = "Service state is missing.";
                return false;
            }

            if (!dto.TryValidate(catalog, out failure))
            {
                return false;
            }

            return workshop.TryValidateRestoreCandidate(
                dto.workshopSelection,
                dto.workshopOrder,
                out failure);
        }

        private ServiceStateDto CreateAdditiveCatalogRestoreCandidate(
            ServiceStateDto source)
        {
            if (source?.retailStocks == null)
            {
                return source;
            }

            ServiceStateDto candidate = source.DeepClone();
            var presentOfferIds = new HashSet<string>(
                candidate.retailStocks
                    .Where(value => value != null)
                    .Select(value => value.offerId),
                StringComparer.Ordinal);
            var stocks = new List<RetailStockStateDto>(
                candidate.retailStocks);
            foreach (ServiceOfferDefinition offer in catalog.Offers)
            {
                if (offer.Kind != ServiceOfferKind.RetailItem ||
                    !presentOfferIds.Add(offer.OfferId))
                {
                    continue;
                }

                stocks.Add(new RetailStockStateDto
                {
                    offerId = offer.OfferId,
                    remaining = offer.StockCapacity,
                });
            }

            candidate.retailStocks = stocks
                .OrderBy(value => value?.offerId, StringComparer.Ordinal)
                .ToArray();
            return candidate;
        }

        private long QuoteBasket()
        {
            long total = 0;
            foreach (StoreBasketLineDto line in basket.Values)
            {
                if (!catalog.TryGetOffer(
                        line.offerId,
                        out ServiceOfferDefinition offer))
                {
                    throw new InvalidOperationException(
                        $"Cannot quote service basket offer '{line.offerId}'.");
                }

                long lineTotal;
                if (offer.BasePriceMinorUnits > 0)
                {
                    lineTotal = checked(
                        offer.BasePriceMinorUnits * line.quantity);
                }
                else if (economy.TryQuote(
                             offer.PriceId,
                             line.quantity,
                             out EconomyPriceQuote quote,
                             out _))
                {
                    lineTotal = quote.TotalPriceMinorUnits;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot quote service basket offer '{line.offerId}'.");
                }

                total = checked(total + lineTotal);
            }

            return total;
        }

        private bool TryValidateRuntimeCatalog(out string failure)
        {
            if (!catalog.TryGetLocation(
                    WorkshopLocationId,
                    out ServiceLocationDefinition workshopLocation) ||
                workshopLocation.Kind != ServiceLocationKind.Workshop)
            {
                failure =
                    $"Service runtime requires workshop location '{WorkshopLocationId}'.";
                return false;
            }

            foreach (ServiceOfferDefinition offer in catalog.Offers)
            {
                string requiredLocationId = offer.Kind switch
                {
                    ServiceOfferKind.RetailItem => StoreLocationId,
                    ServiceOfferKind.PubItem => PubLocationId,
                    ServiceOfferKind.Fuel => FuelLocationIdFor(offer.FuelGrade),
                    ServiceOfferKind.Workshop => WorkshopLocationId,
                    ServiceOfferKind.Inspection => InspectionLocationId,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(offer.Kind)),
                };
                if (!string.Equals(
                        offer.LocationId,
                        requiredLocationId,
                        StringComparison.Ordinal))
                {
                    failure =
                        $"Service offer '{offer.OfferId}' must use runtime location '{requiredLocationId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static string FuelLocationIdFor(FuelGrade grade) => grade switch
        {
            FuelGrade.Gasoline98 => FuelLocationId,
            FuelGrade.Diesel => DieselFuelLocationId,
            FuelGrade.FuelOil => FuelOilLocationId,
            _ => throw new ArgumentOutOfRangeException(nameof(grade)),
        };

        private bool TryQuoteAcceptedFuel(
            FuelGrade grade,
            long acceptedMilliliters,
            out long totalMilliliters,
            out long totalCharge,
            out ServiceFailureReason failureReason)
        {
            totalMilliliters = 0;
            totalCharge = 0;
            if (acceptedMilliliters <= 0 ||
                !fuelPrices.TryGetValue(grade, out FuelPriceStateDto price))
            {
                failureReason = ServiceFailureReason.InvalidRequest;
                return false;
            }

            if (!IsLocationOpen(FuelLocationIdFor(grade)))
            {
                failureReason = ServiceFailureReason.Closed;
                return false;
            }

            long currentMilliliters = fuelDebts.TryGetValue(
                grade,
                out FuelDebtStateDto debt)
                    ? debt.dispensedMilliliters
                    : 0;
            try
            {
                totalMilliliters = checked(
                    currentMilliliters + acceptedMilliliters);
                decimal exact = totalMilliliters / 1000m *
                                price.currentMinorUnitsPerLiter;
                totalCharge = checked((long)decimal.Round(
                    exact,
                    0,
                    MidpointRounding.AwayFromZero));
            }
            catch (OverflowException)
            {
                failureReason = ServiceFailureReason.TransactionRejected;
                return false;
            }

            failureReason = ServiceFailureReason.None;
            return true;
        }

        private bool TryDebit(
            string operationId,
            string locationId,
            long amountMinorUnits,
            out ServiceResult result)
        {
            if (!catalog.TryGetLocation(
                    locationId,
                    out ServiceLocationDefinition location) ||
                amountMinorUnits <= 0)
            {
                result = Failure(ServiceFailureReason.InvalidRequest);
                return false;
            }

            bool succeeded = economy.TryCommit(
                new EconomyTransactionRequest(
                    TransactionId(operationId),
                    EconomyTransactionKind.ServicePayment,
                    EconomyTransactionDirection.Debit,
                    amountMinorUnits,
                    location.SourceStableId),
                out EconomyTransactionReceipt receipt);
            if (!succeeded)
            {
                result = Failure(receipt.FailureReason ==
                    EconomyTransactionFailureReason.InsufficientFunds
                        ? ServiceFailureReason.InsufficientFunds
                        : ServiceFailureReason.TransactionRejected);
                return false;
            }

            result = new ServiceResult(
                true,
                ServiceFailureReason.None,
                operationId,
                amountMinorUnits,
                receipt.WasIdempotentReplay);
            return true;
        }

        private ServiceOperationStateDto CreateOperation(
            string operationId,
            string locationId,
            long amountMinorUnits,
            ServiceHandoffLineDto[] lines) =>
            new()
            {
                operationId = operationId,
                transactionId = TransactionId(operationId),
                locationId = locationId,
                phase = lines.Length == 0
                    ? (int)ServiceOperationPhase.Completed
                    : (int)ServiceOperationPhase.Debited,
                amountMinorUnits = amountMinorUnits,
                handoffLines = lines,
            };

        private ServiceHandoffLineDto CreateHandoffLine(
            string offerId,
            int purchasedUnits)
        {
            ServiceOfferDefinition offer = catalog.TryGetOffer(offerId, out var value)
                ? value
                : throw new InvalidOperationException(
                    $"Service offer '{offerId}' disappeared from its catalog.");
            return new ServiceHandoffLineDto
            {
                offerId = offer.OfferId,
                itemDefinitionId = offer.ItemDefinitionId,
                effectId = offer.EffectId,
                variantIndex = offer.VariantIndex,
                quantity = checked(purchasedUnits * offer.QuantityPerUnit),
            };
        }

        private bool CanFulfillBeforeDebit(
            string operationId,
            string locationId,
            ServiceHandoffLineDto[] lines)
        {
            if (handoff is not IServiceHandoffPreflight preflight ||
                !catalog.TryGetLocation(
                    locationId,
                    out ServiceLocationDefinition location))
            {
                return true;
            }

            for (int index = 0; index < lines.Length; index++)
            {
                ServiceHandoffLineDto line = lines[index];
                var request = new ServiceHandoffRequest(
                    operationId,
                    index,
                    locationId,
                    line.offerId,
                    line.itemDefinitionId,
                    line.effectId,
                    line.variantIndex,
                    line.quantity,
                    location.HandoffAnchorId);
                if (!preflight.CanFulfill(in request, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private void CompleteOrFulfill(ServiceOperationStateDto operation)
        {
            if ((ServiceOperationPhase)operation.phase ==
                ServiceOperationPhase.Completed)
            {
                return;
            }

            operation.phase = (int)ServiceOperationPhase.Fulfilling;
            if (handoff == null ||
                !catalog.TryGetLocation(
                    operation.locationId,
                    out ServiceLocationDefinition location))
            {
                return;
            }

            for (int index = 0; index < operation.handoffLines.Length; index++)
            {
                ServiceHandoffLineDto line = operation.handoffLines[index];
                if (line.completed)
                {
                    continue;
                }

                var request = new ServiceHandoffRequest(
                    operation.operationId,
                    index,
                    operation.locationId,
                    line.offerId,
                    line.itemDefinitionId,
                    line.effectId,
                    line.variantIndex,
                    line.quantity,
                    location.HandoffAnchorId);
                if (!handoff.TryFulfill(in request, out _))
                {
                    return;
                }

                line.completed = true;
            }

            operation.phase = (int)ServiceOperationPhase.Completed;
        }

        private bool TryReplayOperation(
            string operationId,
            out ServiceResult result)
        {
            if (!operations.TryGetValue(operationId, out ServiceOperationStateDto operation))
            {
                result = default;
                return false;
            }

            CompleteOrFulfill(operation);
            Touch();
            result = ResultFor(operation, true);
            return true;
        }

        private bool EnsureOperationCapacity()
        {
            if (operations.Count < MaximumPersistedOperations)
            {
                return true;
            }

            // Capacity is checked before the debit, but pruning is delayed until
            // a genuinely new debit succeeds. A rejected or orphaned replay
            // must leave the Services journal byte-for-byte unchanged.
            return operations.Values.Any(value =>
                (ServiceOperationPhase)value.phase ==
                ServiceOperationPhase.Completed);
        }

        private void PruneCompletedOperationsForCapacity()
        {
            if (operations.Count < MaximumPersistedOperations)
            {
                return;
            }

            string[] removable = operations.Values
                .Where(value => (ServiceOperationPhase)value.phase ==
                                ServiceOperationPhase.Completed)
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Take(Math.Max(
                    1,
                    operations.Count - CompletedOperationPruneTarget))
                .Select(value => value.operationId)
                .ToArray();
            for (int index = 0; index < removable.Length; index++)
            {
                operations.Remove(removable[index]);
            }
        }

        private bool HasStoreCheckoutIntent() =>
            basket.Count > 0 ||
            fuelDebts.Count > 0 ||
            state.brokenWindowChargeMinorUnits > 0;

        private static bool MatchesPubOperation(
            ServiceOperationStateDto operation,
            string locationId,
            long amountMinorUnits,
            ServiceHandoffLineDto requestedLine)
        {
            if (!string.Equals(
                    operation.locationId,
                    locationId,
                    StringComparison.Ordinal) ||
                operation.amountMinorUnits != amountMinorUnits ||
                operation.handoffLines == null ||
                operation.handoffLines.Length != 1)
            {
                return false;
            }

            ServiceHandoffLineDto existing = operation.handoffLines[0];
            return existing != null &&
                   string.Equals(
                       existing.offerId,
                       requestedLine.offerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.itemDefinitionId,
                       requestedLine.itemDefinitionId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.effectId,
                       requestedLine.effectId,
                       StringComparison.Ordinal) &&
                   existing.variantIndex == requestedLine.variantIndex &&
                   existing.quantity == requestedLine.quantity;
        }

        private ServiceResult ResultFor(
            ServiceOperationStateDto operation,
            bool replay) =>
            new(
                (ServiceOperationPhase)operation.phase ==
                    ServiceOperationPhase.Completed,
                (ServiceOperationPhase)operation.phase ==
                    ServiceOperationPhase.Completed
                        ? ServiceFailureReason.None
                        : ServiceFailureReason.PendingFulfillment,
                operation.operationId,
                operation.amountMinorUnits,
                replay);

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            TryAdvanceHomeMailOrder(gameTimeEvent.Current);
            if (gameTimeEvent.Kind == GameTimeEventKind.DayChanged ||
                gameTimeEvent.Kind == GameTimeEventKind.StateRestored ||
                state.restockPending)
            {
                TryProcessRestock(gameTimeEvent.Current);
            }
        }

        private void TryAdvanceHomeMailOrder(GameTimeSnapshot snapshot)
        {
            HomeMailOrderStateDto order = EnsureHomeMailOrderState();
            if ((HomeMailOrderPhase)order.phase != HomeMailOrderPhase.Submitted ||
                snapshot.ElapsedGameSeconds < order.readyElapsedGameSeconds)
            {
                return;
            }

            order.phase = (int)HomeMailOrderPhase.ReadyForPayment;
            Vector3 position = catalog.TryGetLocation(
                StoreLocationId,
                out ServiceLocationDefinition location)
                    ? location.WorldPosition
                    : Vector3.zero;
            NotificationRaised?.Invoke(
                new ServiceNotification(
                    "event.service.home-mail-order.ready",
                    "Teimo says your mail-order parts have arrived. Pay at the post counter.",
                    position));
            Touch();
        }

        private void TryProcessRestock(GameTimeSnapshot snapshot)
        {
            if (snapshot.DayIndex < state.lastProcessedDayIndex)
            {
                return;
            }

            var currentDate = new DateTime(
                snapshot.Date.Year,
                snapshot.Date.Month,
                snapshot.Date.Day);
            int offset = ((int)currentDate.DayOfWeek -
                          (int)DayOfWeek.Thursday + 7) % 7;
            long lastThursday = snapshot.DayIndex - offset;
            bool due = state.restockPending ||
                       lastThursday > state.lastProcessedDayIndex;
            if (due && proximity != null &&
                proximity.IsPlayerWithin(
                    StoreLocationId,
                    StoreRestockExclusionMeters))
            {
                state.restockPending = true;
                return;
            }

            if (due)
            {
                foreach (RetailStockStateDto stock in stocks.Values)
                {
                    if (catalog.TryGetOffer(
                            stock.offerId,
                            out ServiceOfferDefinition offer) &&
                        offer.Restockable)
                    {
                        int reserved = basket.TryGetValue(
                            stock.offerId,
                            out StoreBasketLineDto line)
                                ? line.quantity
                                : 0;
                        stock.remaining = Math.Max(
                            0,
                            offer.StockCapacity - reserved);
                    }
                }

                state.restockPending = false;
            }

            state.lastProcessedDayIndex = snapshot.DayIndex;
            if (due)
            {
                Touch();
            }
        }

        private void ApplyState(ServiceStateDto dto)
        {
            state = dto.DeepClone();
            EnsureHomeMailOrderState();
            stocks.Clear();
            foreach (RetailStockStateDto stock in state.retailStocks)
            {
                stocks.Add(stock.offerId, stock);
            }

            basket.Clear();
            foreach (StoreBasketLineDto line in state.storeBasket)
            {
                basket.Add(line.offerId, line);
            }

            fuelDebts.Clear();
            foreach (FuelDebtStateDto debt in state.fuelDebts)
            {
                fuelDebts.Add((FuelGrade)debt.grade, debt);
            }

            fuelPrices.Clear();
            foreach (FuelPriceStateDto price in state.fuelPrices)
            {
                fuelPrices.Add((FuelGrade)price.grade, price);
            }

            operations.Clear();
            foreach (ServiceOperationStateDto operation in state.pendingOperations)
            {
                operations.Add(operation.operationId, operation);
            }
        }

        private void SynchronizeArrays()
        {
            state.retailStocks = stocks.Values
                .OrderBy(value => value.offerId, StringComparer.Ordinal)
                .Select(value => value.DeepClone())
                .ToArray();
            state.storeBasket = basket.Values
                .OrderBy(value => value.offerId, StringComparer.Ordinal)
                .Select(value => value.DeepClone())
                .ToArray();
            state.fuelDebts = fuelDebts.Values
                .OrderBy(value => value.grade)
                .Select(value => value.DeepClone())
                .ToArray();
            state.fuelPrices = fuelPrices.Values
                .OrderBy(value => value.grade)
                .Select(value => value.DeepClone())
                .ToArray();
            state.pendingOperations = operations.Values
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(value => value.DeepClone())
                .ToArray();
            state.workshopSelection = workshop?.CaptureSelectionDto() ??
                new WorkshopSelectionStateDto();
            state.workshopOrder = workshop?.CaptureOrderDto() ??
                new WorkshopOrderStateDto();
        }

        private void RestoreWorkshopOrThrow(
            WorkshopSelectionStateDto selection,
            WorkshopOrderStateDto order)
        {
            if (!workshop.TryRestore(selection, order, out string failure))
            {
                throw new InvalidOperationException(
                    "Workshop service state is invalid: " + failure);
            }
        }

        private DateTime CurrentDate()
        {
            GameTimeSnapshot snapshot = gameTime.Snapshot;
            return new DateTime(
                snapshot.Date.Year,
                snapshot.Date.Month,
                snapshot.Date.Day);
        }

        private HomeMailOrderStateDto EnsureHomeMailOrderState()
        {
            state.homeMailOrder ??= new HomeMailOrderStateDto();
            state.homeMailOrder.draftOfferIds ??= Array.Empty<string>();
            state.homeMailOrder.orderedOfferIds ??= Array.Empty<string>();
            return state.homeMailOrder;
        }

        private static long SumHomeMailOrderOffers(IEnumerable<string> offerIds)
        {
            long total = 0L;
            foreach (string offerId in offerIds ?? Array.Empty<string>())
            {
                if (HomePartsMailOrderCatalog.TryGet(
                        offerId,
                        out HomePartsMailOrderOffer offer))
                {
                    total = checked(total + offer.PriceMinorUnits);
                }
            }

            return total;
        }

        private static double ResolveMailOrderDeliveryDuration(string orderId)
        {
            uint hash = 2166136261u;
            for (int index = 0; index < orderId.Length; index++)
            {
                hash ^= orderId[index];
                hash *= 16777619u;
            }

            double normalized = (hash & 0x00ffffffu) / 16777215d;
            return MailOrderMinimumDeliveryGameSeconds +
                   (MailOrderMaximumDeliveryGameSeconds -
                    MailOrderMinimumDeliveryGameSeconds) * normalized;
        }

        private static float ResolveWorkshopDuration(string orderId)
        {
            uint hash = 2166136261u;
            for (int index = 0; index < orderId.Length; index++)
            {
                hash ^= orderId[index];
                hash *= 16777619u;
            }

            float normalized = (hash & 0x00ffffffu) / 16777215f;
            return Mathf.Lerp(
                WorkshopServiceEngine.MinimumWorkRealSeconds,
                WorkshopServiceEngine.MaximumWorkRealSeconds,
                normalized);
        }

        private void Touch()
        {
            state.revision = checked(state.revision + 1);
            SynchronizeArrays();
            StateChanged?.Invoke(state.revision);
        }

        private static string TransactionId(string operationId) =>
            $"transaction.services.{operationId}";

        private static bool CanUseOperationId(string operationId) =>
            ServiceStableId.IsCanonical(operationId) &&
            TransactionId(operationId).Length <= 128;

        private static ServiceResult Success() =>
            new(true, ServiceFailureReason.None);

        private static ServiceResult Failure(ServiceFailureReason reason) =>
            new(false, reason);

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "Service runtime is not initialized.");
            }
        }

        private void OnDestroy()
        {
            timeSubscription?.Dispose();
            timeSubscription = null;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (state.restockPending)
            {
                TryProcessRestock(gameTime.Snapshot);
            }

            if (workshop.Phase == WorkshopOrderPhase.Ready &&
                workshop.TryMarkRepairReadyNotified())
            {
                RaiseWorkshopNotification(
                    "event.service.fleetari.repair-ready",
                    "Fleetari here! Your repair work is ready. Come here and get your stuff.");
                Touch();
            }

            bool wasReady = workshop.Phase == WorkshopOrderPhase.Ready;
            bool warningWasRaised = workshop.LoanerWarningNotified;
            bool overdueWasRaised = workshop.LoanerOverdue;
            float playerDistance = proximity == null
                ? 0f
                : proximity.IsPlayerWithin(
                    WorkshopLocationId,
                    WorkshopServiceEngine.WorkPauseDistanceMeters)
                    ? 0f
                    : WorkshopServiceEngine.WorkPauseDistanceMeters + 1f;
            if (!workshop.Tick(Time.unscaledDeltaTime, playerDistance))
            {
                return;
            }

            if (!wasReady && workshop.Phase == WorkshopOrderPhase.Ready &&
                workshop.TryMarkRepairReadyNotified())
            {
                RaiseWorkshopNotification(
                    "event.service.fleetari.repair-ready",
                    "Fleetari here! Your repair work is ready. Come here and get your stuff.");
            }

            if (!warningWasRaised && workshop.LoanerWarningNotified)
            {
                RaiseWorkshopNotification(
                    "event.service.fleetari.loaner-warning",
                    "Fleetari wants his loaner returned.");
            }

            if (!overdueWasRaised && workshop.LoanerOverdue)
            {
                RaiseWorkshopNotification(
                    "event.service.fleetari.loaner-overdue",
                    "Fleetari's loaner return is overdue.");
            }

            Touch();
        }

        private void RaiseWorkshopNotification(
            string notificationId,
            string fallbackText)
        {
            Vector3 position = catalog.TryGetLocation(
                WorkshopLocationId,
                out ServiceLocationDefinition location)
                ? location.WorldPosition
                : Vector3.zero;
            NotificationRaised?.Invoke(
                new ServiceNotification(notificationId, fallbackText, position));
        }
    }
}
