using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Time;
using MSC.Economy;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Services.Tests.EditMode
{
    public sealed class ServiceRuntimeTests
    {
        private GameObject runtimeObject;
        private ServiceCatalog catalog;

        [TearDown]
        public void TearDown()
        {
            if (runtimeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
                runtimeObject = null;
            }

            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                catalog = null;
            }
        }

        [Test]
        public void HomeMailOrder_PhysicalEnvelopeWaitPaymentAndDelivery_AreIndependentOfVehicle()
        {
            var economy = new FakeEconomy(2_000_000);
            var clock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                clock: clock);
            string offerId = HomePartsMailOrderCatalog.Offers[0].Id;
            HomeMailOrderEnvelopePlan envelopePlan = default;

            Assert.That(runtime.TryToggleHomeMailOrderDraft(offerId), Is.True);
            ServiceResult envelope = runtime.TryCreateHomeMailOrderEnvelope(plan =>
            {
                envelopePlan = plan;
                return true;
            });

            Assert.That(envelope.Succeeded, Is.True);
            Assert.That(envelopePlan.Offers.Count, Is.EqualTo(1));
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.EnvelopeCreated));
            Assert.That(economy.CommitCallCount, Is.Zero,
                "Writing the envelope must not debit money.");

            ServiceResult submitted = runtime.TrySubmitHomeMailOrderEnvelope(
                envelopePlan.EnvelopeStableEntityId);
            Assert.That(submitted.Succeeded, Is.True);
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.Submitted));

            clock.Advance(2001d * 60d / 12d);
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.ReadyForPayment));

            HomeMailOrderDeliveryPlan deliveryPlan = default;
            ServiceResult paid = runtime.TryPayAndMaterializeHomeMailOrder(plan =>
            {
                deliveryPlan = plan;
                return true;
            });

            Assert.That(paid.Succeeded, Is.True);
            Assert.That(deliveryPlan.Offers.Count, Is.EqualTo(1));
            Assert.That(
                deliveryPlan.Offers[0].ItemDefinitionId,
                Is.EqualTo(HomePartsMailOrderCatalog.Offers[0].ItemDefinitionId));
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.Delivered));
        }

        [Test]
        public void HomeMailOrder_PaidPendingMaterialization_RetriesWithoutSecondDebit()
        {
            var economy = new FakeEconomy(2_000_000);
            var clock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                clock: clock);
            runtime.TryToggleHomeMailOrderDraft(
                HomePartsMailOrderCatalog.Offers[1].Id);
            HomeMailOrderEnvelopePlan envelopePlan = default;
            runtime.TryCreateHomeMailOrderEnvelope(plan =>
            {
                envelopePlan = plan;
                return true;
            });
            runtime.TrySubmitHomeMailOrderEnvelope(
                envelopePlan.EnvelopeStableEntityId);
            clock.Advance(2001d * 60d / 12d);

            ServiceResult deferred =
                runtime.TryPayAndMaterializeHomeMailOrder(_ => false);
            Assert.That(deferred.Succeeded, Is.False);
            Assert.That(
                deferred.FailureReason,
                Is.EqualTo(ServiceFailureReason.PendingFulfillment));
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.PaidPendingMaterialization));

            ServiceResult retried =
                runtime.TryPayAndMaterializeHomeMailOrder(_ => true);
            Assert.That(retried.Succeeded, Is.True);
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.Delivered));
        }

        [Test]
        public void LegacyServiceStateWithoutMailOrder_RestoresAsEmptyMailOrder()
        {
            var economy = new FakeEconomy(2_000_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true);
            ServiceStateDto legacy = runtime.CaptureDto();
            legacy.homeMailOrder = null;

            Assert.That(runtime.TryRestoreDto(legacy, out string failure), Is.True, failure);
            Assert.That(runtime.HomeMailOrderDraftCount, Is.Zero);
            Assert.That(
                runtime.CurrentHomeMailOrderPhase,
                Is.EqualTo(HomeMailOrderPhase.None));
        }

        [Test]
        public void AdditiveRetailCatalogExtension_RestoresLegacyStockCoverage()
        {
            var economy = new FakeEconomy(2_000_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true);
            ServiceStateDto legacy = runtime.CaptureDto();
            legacy.retailStocks = legacy.retailStocks
                .Where(value => string.Equals(
                    value.offerId,
                    "service.retail.tests.restockable",
                    StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                runtime.TryRestoreDto(legacy, out string failure),
                Is.True,
                failure);
            ServiceStateDto restored = runtime.CaptureDto();
            Assert.That(restored.retailStocks, Has.Length.EqualTo(2));
            Assert.That(
                restored.retailStocks.Single(value => string.Equals(
                    value.offerId,
                    "service.retail.tests.suomi-cover",
                    StringComparison.Ordinal)).remaining,
                Is.EqualTo(1));
        }

        [Test]
        public void WorkshopOrder_WithoutPlayerVehicleBackend_RejectsBeforeDebit()
        {
            var economy = new FakeEconomy(2_000_000);
            ServiceRuntime runtime = CreateRuntime(economy, includeRetail: false);
            ServiceResult selection = runtime.TrySetWorkshopSelection(
                new WorkshopSelectionStateDto
                {
                    offerIds = new[] { "service.workshop.tests.body-repair" },
                    paintVariant = -1,
                    rimVariant = -1,
                    tireVariant = -1,
                    finalGearRatio = 4.286f,
                });

            ServiceResult result = runtime.TryPlaceWorkshopOrder(
                "service.order.tests.fleetari",
                "vehicle.tests.player-service-target");

            Assert.That(selection.Succeeded, Is.True);
            Assert.That(runtime.WorkshopOrderingAvailable, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.VehicleOutcomeUnavailable));
            Assert.That(economy.CommitCallCount, Is.Zero);
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(2_000_000));
            Assert.That(runtime.WorkshopPhase, Is.EqualTo(WorkshopOrderPhase.None));
        }

        [Test]
        public void Inspection_WithoutVehicleAssessment_RejectsBeforeDebit()
        {
            var economy = new FakeEconomy(100_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeInspection: true);

            ServiceResult result = runtime.TryStartInspection(
                "service.operation.tests.inspection",
                "service.inspection.tests.vehicle");

            Assert.That(runtime.InspectionOrderingAvailable, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.InspectionUnavailable));
            Assert.That(economy.CommitCallCount, Is.Zero);
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(100_000));
            Assert.That(
                runtime.CaptureDto().inspectionOrder.phase,
                Is.EqualTo((int)InspectionOrderPhase.None));
        }

        [Test]
        public void ThursdayRestock_DoesNotReplenishOneTimeSuomiCovers()
        {
            var economy = new FakeEconomy(2_000_000);
            var handoff = new SuccessfulHandoff();
            var clock = new GameTimeService(GameTimeConfig.RemakeDesignTargetDefaults);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                clock: clock,
                handoff: handoff);

            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.restockable").Succeeded, Is.True);
            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.suomi-cover").Succeeded, Is.True);
            Assert.That(runtime.TryCheckoutStore(
                "operation.services.tests.suomi-checkout").Succeeded, Is.True);

            // Default clock starts on Tuesday. Two configured game days reach
            // Thursday and execute the donor weekly restock edge.
            clock.Advance(
                GameTimeConfig.RemakeDesignTargetDefaults
                    .DayLengthSimulationSeconds * 2d);

            ServiceStateDto state = runtime.CaptureDto();
            Assert.That(
                Remaining(state, "service.retail.tests.restockable"),
                Is.EqualTo(1));
            Assert.That(
                Remaining(state, "service.retail.tests.suomi-cover"),
                Is.Zero,
                "One-time SUOMI covers must not respawn every Thursday.");
            Assert.That(handoff.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void Checkout_WithUnavailableEffect_RejectsBeforeDebit()
        {
            var economy = new FakeEconomy(100_000);
            var handoff = new RejectingPreflightHandoff();
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                handoff: handoff);

            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.suomi-cover").Succeeded, Is.True);

            ServiceResult result = runtime.TryCheckoutStore(
                "operation.services.tests.unavailable-effect");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.HandoffUnavailable));
            Assert.That(economy.CommitCallCount, Is.Zero);
            Assert.That(runtime.GetBasketQuantity(
                "service.retail.tests.suomi-cover"), Is.EqualTo(1));
            Assert.That(handoff.FulfillCallCount, Is.Zero);
        }

        [Test]
        public void Checkout_EmptyIntentReplays_ButNewIntentWithSameIdConflicts()
        {
            var economy = new FakeEconomy(10_000);
            var handoff = new SuccessfulHandoff();
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                handoff: handoff);
            const string operationId =
                "operation.services.tests.checkout-replay";

            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.restockable").Succeeded, Is.True);
            ServiceResult first = runtime.TryCheckoutStore(operationId);
            ServiceResult replay = runtime.TryCheckoutStore(operationId);
            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.suomi-cover").Succeeded, Is.True);
            ServiceResult conflict = runtime.TryCheckoutStore(operationId);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.WasIdempotentReplay, Is.False);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.WasIdempotentReplay, Is.True);
            Assert.That(conflict.Succeeded, Is.False);
            Assert.That(
                conflict.FailureReason,
                Is.EqualTo(ServiceFailureReason.InvalidRequest));
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(9_900));
            Assert.That(handoff.CallCount, Is.EqualTo(1));
            Assert.That(runtime.GetBasketQuantity(
                "service.retail.tests.suomi-cover"), Is.EqualTo(1));
        }

        [Test]
        public void PrunedStoreOperationId_CannotFulfillAgainForOldDebit()
        {
            const long initialBalance = 100_000;
            const string pubOfferId = "service.pub.tests.beer";
            const string prunedOperationId =
                "operation.services.tests.checkout.pruned";
            var economy = new FakeEconomy(initialBalance);
            var handoff = new SuccessfulHandoff();
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                handoff: handoff,
                includePub: true);

            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.restockable").Succeeded, Is.True);
            Assert.That(
                runtime.TryCheckoutStore(prunedOperationId).Succeeded,
                Is.True);
            for (int index = 0; index < 128; index++)
            {
                ServiceResult purchase = runtime.TryPurchasePub(
                    $"operation.services.tests.pub.{index:000}",
                    pubOfferId);
                Assert.That(purchase.Succeeded, Is.True, $"purchase {index}");
            }

            Assert.That(
                runtime.CaptureDto().pendingOperations.Any(value =>
                    string.Equals(
                        value.operationId,
                        prunedOperationId,
                        StringComparison.Ordinal)),
                Is.False,
                "The capacity pass must have pruned the oldest completed ID.");
            int fulfilledBeforeReuse = handoff.CallCount;
            long balanceBeforeReuse = economy.Snapshot.BalanceMinorUnits;
            Assert.That(runtime.TryAddStoreItem(
                "service.retail.tests.suomi-cover").Succeeded, Is.True);

            ServiceResult staleReplay = runtime.TryCheckoutStore(
                prunedOperationId);

            Assert.That(staleReplay.Succeeded, Is.False);
            Assert.That(
                staleReplay.FailureReason,
                Is.EqualTo(ServiceFailureReason.TransactionRejected));
            Assert.That(handoff.CallCount, Is.EqualTo(fulfilledBeforeReuse));
            Assert.That(
                economy.Snapshot.BalanceMinorUnits,
                Is.EqualTo(balanceBeforeReuse));
            Assert.That(balanceBeforeReuse, Is.EqualTo(initialBalance - 12_900));
            Assert.That(runtime.GetBasketQuantity(
                "service.retail.tests.suomi-cover"), Is.EqualTo(1));
        }

        [Test]
        public void EconomyOnlyPubReplay_CannotCreateUntrackedHandoff()
        {
            const string operationId =
                "operation.services.tests.pub.economy-only";
            var economy = new FakeEconomy(10_000);
            var handoff = new SuccessfulHandoff();
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                handoff: handoff,
                includePub: true);
            var historicalPayment = new EconomyTransactionRequest(
                $"transaction.services.{operationId}",
                EconomyTransactionKind.ServicePayment,
                EconomyTransactionDirection.Debit,
                100,
                "service.source.tests.pub");
            Assert.That(economy.TryCommit(
                in historicalPayment,
                out _), Is.True);
            long balanceAfterHistoricalPayment =
                economy.Snapshot.BalanceMinorUnits;

            ServiceResult staleReplay = runtime.TryPurchasePub(
                operationId,
                "service.pub.tests.beer");

            Assert.That(staleReplay.Succeeded, Is.False);
            Assert.That(
                staleReplay.FailureReason,
                Is.EqualTo(ServiceFailureReason.TransactionRejected));
            Assert.That(handoff.CallCount, Is.Zero);
            Assert.That(
                economy.Snapshot.BalanceMinorUnits,
                Is.EqualTo(balanceAfterHistoricalPayment));
            Assert.That(runtime.CaptureDto().pendingOperations, Is.Empty);
        }

        [Test]
        public void PubOperationId_CannotMasqueradeAsEmptyStoreReplay()
        {
            const string operationId =
                "operation.services.tests.cross-endpoint";
            var economy = new FakeEconomy(10_000);
            var handoff = new SuccessfulHandoff();
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: true,
                handoff: handoff,
                includePub: true);
            Assert.That(runtime.TryPurchasePub(
                operationId,
                "service.pub.tests.beer").Succeeded, Is.True);

            ServiceResult wrongEndpointReplay =
                runtime.TryCheckoutStore(operationId);

            Assert.That(wrongEndpointReplay.Succeeded, Is.False);
            Assert.That(
                wrongEndpointReplay.FailureReason,
                Is.EqualTo(ServiceFailureReason.InvalidRequest));
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(handoff.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FuelCatalog_WithAllThreeGradeLocations_Initializes()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);

            Assert.That(
                runtime.GetFuelPriceMinorUnitsPerLiter(FuelGrade.Gasoline98),
                Is.EqualTo(475));
            Assert.That(
                runtime.GetFuelPriceMinorUnitsPerLiter(FuelGrade.Diesel),
                Is.EqualTo(423));
            Assert.That(
                runtime.GetFuelPriceMinorUnitsPerLiter(FuelGrade.FuelOil),
                Is.EqualTo(213));
        }

        [Test]
        public void AcceptedFuel_FlagsTheftOnlyWhenNozzleIsReleased()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);

            ServiceResult accepted = runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                1_000);
            ServiceStateDto beforeRelease = runtime.CaptureDto();

            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(accepted.AmountMinorUnits, Is.EqualTo(475));
            Assert.That(beforeRelease.fuelStolen, Is.False);
            Assert.That(beforeRelease.fuelDebts, Has.Length.EqualTo(1));
            Assert.That(
                beforeRelease.fuelDebts[0].dispensedMilliliters,
                Is.EqualTo(1_000));

            ServiceResult released = runtime.TryReleaseFuelNozzle(
                FuelGrade.Gasoline98);
            ServiceStateDto afterRelease = runtime.CaptureDto();

            Assert.That(released.Succeeded, Is.True);
            Assert.That(afterRelease.fuelStolen, Is.True);
            Assert.That(afterRelease.fuelDebts, Has.Length.EqualTo(1));
            Assert.That(runtime.TryReleaseFuelNozzle(
                FuelGrade.Gasoline98).Succeeded, Is.True,
                "Duplicate physical release notifications must be harmless.");
        }

        [Test]
        public void FuelPreflight_IsNonMutating_AndOverflowCannotCorruptDebt()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);

            ServiceResult preflight = runtime.TryPreflightAcceptedFuel(
                FuelGrade.Gasoline98,
                1_000);
            Assert.That(preflight.Succeeded, Is.True);
            Assert.That(preflight.AmountMinorUnits, Is.EqualTo(475));
            Assert.That(runtime.CaptureDto().fuelDebts, Is.Empty);

            Assert.That(runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                1).Succeeded, Is.True);
            ServiceResult overflowPreflight = runtime.TryPreflightAcceptedFuel(
                FuelGrade.Gasoline98,
                long.MaxValue);
            ServiceResult overflowRecord = runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                long.MaxValue);
            ServiceStateDto state = runtime.CaptureDto();

            Assert.That(overflowPreflight.Succeeded, Is.False);
            Assert.That(
                overflowPreflight.FailureReason,
                Is.EqualTo(ServiceFailureReason.TransactionRejected));
            Assert.That(overflowRecord.Succeeded, Is.False);
            Assert.That(state.fuelDebts, Has.Length.EqualTo(1));
            Assert.That(state.fuelDebts[0].dispensedMilliliters, Is.EqualTo(1));
            Assert.That(state.fuelDebts[0].chargeMinorUnits, Is.Zero);
            Assert.That(state.fuelStolen, Is.False);
        }

        [Test]
        public void FuelCheckout_ClearsDebtAndTheft_AfterSuccessfulDebit()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);
            Assert.That(runtime.TryRecordAcceptedFuel(
                FuelGrade.Diesel,
                2_000).Succeeded, Is.True);
            Assert.That(runtime.TryReleaseFuelNozzle(
                FuelGrade.Diesel).Succeeded, Is.True);

            ServiceResult checkout = runtime.TryCheckoutStore(
                "operation.services.tests.fuel-checkout");
            ServiceStateDto paid = runtime.CaptureDto();

            Assert.That(checkout.Succeeded, Is.True);
            Assert.That(checkout.AmountMinorUnits, Is.EqualTo(846));
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(9_154));
            Assert.That(paid.fuelDebts, Is.Empty);
            Assert.That(paid.fuelStolen, Is.False);
        }

        [Test]
        public void FailedFuelCheckout_PreservesDebtAndTheft()
        {
            var economy = new FakeEconomy(100);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);
            Assert.That(runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                1_000).Succeeded, Is.True);
            Assert.That(runtime.TryReleaseFuelNozzle(
                FuelGrade.Gasoline98).Succeeded, Is.True);

            ServiceResult checkout = runtime.TryCheckoutStore(
                "operation.services.tests.fuel-checkout-rejected");
            ServiceStateDto unpaid = runtime.CaptureDto();

            Assert.That(checkout.Succeeded, Is.False);
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(100));
            Assert.That(unpaid.fuelDebts, Has.Length.EqualTo(1));
            Assert.That(unpaid.fuelStolen, Is.True);
        }

        [Test]
        public void FuelOpenSession_RoundTripsWithoutInventingTheft()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);
            Assert.That(runtime.TryRecordAcceptedFuel(
                FuelGrade.FuelOil,
                1_500).Succeeded, Is.True);
            ServiceStateDto openSession = runtime.CaptureDto();
            Assert.That(openSession.fuelStolen, Is.False);

            Assert.That(runtime.TryReleaseFuelNozzle(
                FuelGrade.FuelOil).Succeeded, Is.True);
            ServiceStateDto releasedSession = runtime.CaptureDto();
            Assert.That(runtime.TryRestoreDto(
                openSession,
                out string failure), Is.True, failure);
            ServiceStateDto restored = runtime.CaptureDto();

            Assert.That(restored.fuelStolen, Is.False);
            Assert.That(restored.fuelDebts, Has.Length.EqualTo(1));
            Assert.That(restored.fuelDebts[0].grade,
                Is.EqualTo((int)FuelGrade.FuelOil));
            Assert.That(restored.fuelDebts[0].dispensedMilliliters,
                Is.EqualTo(1_500));
            Assert.That(restored.fuelDebts[0].chargeMinorUnits,
                Is.EqualTo(320));

            Assert.That(runtime.TryRestoreDto(
                releasedSession,
                out failure), Is.True, failure);
            Assert.That(runtime.CaptureDto().fuelStolen, Is.True);
        }

        [Test]
        public void FuelSave_RejectsTheftWithoutDebt()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);
            ServiceStateDto invalid = runtime.CaptureDto();
            invalid.fuelDebts = new[]
            {
                new FuelDebtStateDto
                {
                    grade = (int)FuelGrade.Gasoline98,
                },
            };

            Assert.That(runtime.TryValidateRestoreCandidate(
                invalid,
                out string failure), Is.False);
            StringAssert.Contains("debt", failure.ToLowerInvariant());

            invalid.fuelDebts = Array.Empty<FuelDebtStateDto>();
            invalid.fuelStolen = true;

            Assert.That(runtime.TryValidateRestoreCandidate(
                invalid,
                out failure), Is.False);
            StringAssert.Contains("theft", failure.ToLowerInvariant());
        }

        [Test]
        public void FuelGrade_UsesItsExactStaffedLocation()
        {
            var economy = new FakeEconomy(10_000);
            var availability = new DeniedLocationAvailability(
                "service.location.teimo-fuel-diesel");
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true,
                availability: availability);

            ServiceResult gasoline = runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                1_000);
            ServiceResult diesel = runtime.TryRecordAcceptedFuel(
                FuelGrade.Diesel,
                1_000);

            Assert.That(gasoline.Succeeded, Is.True);
            Assert.That(diesel.Succeeded, Is.False);
            Assert.That(
                diesel.FailureReason,
                Is.EqualTo(ServiceFailureReason.Closed));
            Assert.That(
                runtime.GetFuelDebtMinorUnits(FuelGrade.Diesel),
                Is.Zero);
        }

        [Test]
        public void ReleasingUnusedNozzle_DoesNotCreateTheftOrDebt()
        {
            var economy = new FakeEconomy(10_000);
            ServiceRuntime runtime = CreateRuntime(
                economy,
                includeRetail: false,
                includeFuel: true);

            ServiceResult result = runtime.TryReleaseFuelNozzle(
                FuelGrade.Gasoline98);
            ServiceStateDto state = runtime.CaptureDto();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(ServiceFailureReason.InvalidRequest));
            Assert.That(state.fuelDebts, Is.Empty);
            Assert.That(state.fuelStolen, Is.False);
            Assert.That(economy.CommitCallCount, Is.Zero);
        }

        private ServiceRuntime CreateRuntime(
            FakeEconomy economy,
            bool includeRetail,
            bool includeInspection = false,
            bool includeFuel = false,
            GameTimeService clock = null,
            IServiceHandoffBackend handoff = null,
            IServiceAvailabilitySource availability = null,
            bool includePub = false)
        {
            catalog = CreateCatalog(
                includeRetail,
                includeInspection,
                includeFuel,
                includePub);
            runtimeObject = new GameObject("ServiceRuntimeTests");
            ServiceRuntime runtime = runtimeObject.AddComponent<ServiceRuntime>();
            runtime.Initialize(
                catalog,
                economy,
                clock ?? new GameTimeService(
                    GameTimeConfig.RemakeDesignTargetDefaults),
                configuredAvailability: availability,
                configuredHandoff: handoff,
                configuredWorkshopOutcome: null);
            return runtime;
        }

        private static ServiceCatalog CreateCatalog(
            bool includeRetail,
            bool includeInspection,
            bool includeFuel,
            bool includePub)
        {
            ServiceAvailabilityWindow allDay = new();
            allDay.ConfigureForAuthoring(127, 0, 0);

            ServiceLocationDefinition workshop = Location(
                "service.location.workshop.fleetari",
                ServiceLocationKind.Workshop,
                allDay);
            var locations = new List<ServiceLocationDefinition> { workshop };
            var offers = new List<ServiceOfferDefinition>
            {
                Offer(
                    "service.workshop.tests.body-repair",
                    "service.location.workshop.fleetari",
                    ServiceOfferKind.Workshop,
                    875_000),
            };

            if (includeRetail || includeFuel)
            {
                locations.Add(Location(
                    "service.location.teimo-store",
                    ServiceLocationKind.Store,
                    allDay));
            }

            if (includeRetail)
            {
                offers.Add(RetailOffer(
                    "service.retail.tests.restockable",
                    "service.effect.tests.restockable",
                    restockable: true));
                offers.Add(RetailOffer(
                    "service.retail.tests.suomi-cover",
                    "service.effect.tests.suomi-cover",
                    restockable: false));
            }

            if (includePub)
            {
                locations.Add(Location(
                    "service.location.teimo-pub",
                    ServiceLocationKind.Pub,
                    allDay));
                offers.Add(PubOffer(
                    "service.pub.tests.beer",
                    "service.effect.tests.beer"));
            }

            var fuelPrices = new List<ServiceFuelPriceDefinition>();
            if (includeFuel)
            {
                locations.Add(Location(
                    "service.location.teimo-fuel",
                    ServiceLocationKind.FuelStation,
                    allDay));
                locations.Add(Location(
                    "service.location.teimo-fuel-diesel",
                    ServiceLocationKind.FuelStation,
                    allDay));
                locations.Add(Location(
                    "service.location.teimo-fuel-oil",
                    ServiceLocationKind.FuelStation,
                    allDay));
                offers.Add(FuelOffer(
                    "service.fuel.tests.gasoline98",
                    "service.location.teimo-fuel",
                    FuelGrade.Gasoline98));
                offers.Add(FuelOffer(
                    "service.fuel.tests.diesel",
                    "service.location.teimo-fuel-diesel",
                    FuelGrade.Diesel));
                offers.Add(FuelOffer(
                    "service.fuel.tests.fuel-oil",
                    "service.location.teimo-fuel-oil",
                    FuelGrade.FuelOil));
                fuelPrices.Add(FuelPrice(FuelGrade.Gasoline98, 475));
                fuelPrices.Add(FuelPrice(FuelGrade.Diesel, 423));
                fuelPrices.Add(FuelPrice(FuelGrade.FuelOil, 213));
            }

            if (includeInspection)
            {
                locations.Add(Location(
                    "service.location.inspection-station",
                    ServiceLocationKind.Inspection,
                    allDay));
                offers.Add(Offer(
                    "service.inspection.tests.vehicle",
                    "service.location.inspection-station",
                    ServiceOfferKind.Inspection,
                    32_500));
            }

            ServiceCatalog result =
                ScriptableObject.CreateInstance<ServiceCatalog>();
            result.ConfigureForAuthoring(
                "service.catalog.runtime-tests",
                new string('a', 64),
                locations.ToArray(),
                offers.ToArray(),
                fuelPrices.ToArray());
            Assert.That(result.TryValidate(out string failure), Is.True, failure);
            return result;
        }

        private static ServiceLocationDefinition Location(
            string locationId,
            ServiceLocationKind kind,
            ServiceAvailabilityWindow allDay)
        {
            var location = new ServiceLocationDefinition();
            location.ConfigureForAuthoring(
                locationId,
                locationId,
                $"service.source.tests.{kind.ToString().ToLowerInvariant()}",
                kind,
                $"service.anchor.tests.{kind.ToString().ToLowerInvariant()}.interaction",
                $"service.anchor.tests.{kind.ToString().ToLowerInvariant()}.handoff",
                Vector3.zero,
                new[] { allDay });
            return location;
        }

        private static ServiceOfferDefinition Offer(
            string offerId,
            string locationId,
            ServiceOfferKind kind,
            long price)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                locationId,
                offerId,
                kind,
                configuredPriceId: string.Empty,
                configuredBasePriceMinorUnits: price);
            return offer;
        }

        private static ServiceOfferDefinition RetailOffer(
            string offerId,
            string effectId,
            bool restockable)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                "service.location.teimo-store",
                offerId,
                ServiceOfferKind.RetailItem,
                configuredPriceId: string.Empty,
                configuredEffectId: effectId,
                configuredStockCapacity: 1,
                configuredBasePriceMinorUnits: 100,
                configuredRestockable: restockable);
            return offer;
        }

        private static ServiceOfferDefinition FuelOffer(
            string offerId,
            string locationId,
            FuelGrade grade)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                locationId,
                offerId,
                ServiceOfferKind.Fuel,
                configuredPriceId: string.Empty,
                configuredFuelGrade: grade);
            return offer;
        }

        private static ServiceOfferDefinition PubOffer(
            string offerId,
            string effectId)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                "service.location.teimo-pub",
                offerId,
                ServiceOfferKind.PubItem,
                configuredPriceId: string.Empty,
                configuredEffectId: effectId,
                configuredBasePriceMinorUnits: 100);
            return offer;
        }

        private static ServiceFuelPriceDefinition FuelPrice(
            FuelGrade grade,
            long price)
        {
            var definition = new ServiceFuelPriceDefinition();
            definition.ConfigureForAuthoring(grade, price, price, price);
            return definition;
        }

        private static int Remaining(ServiceStateDto state, string offerId) =>
            state.retailStocks.Single(value =>
                string.Equals(value.offerId, offerId, StringComparison.Ordinal))
                .remaining;

        private sealed class SuccessfulHandoff : IServiceHandoffBackend
        {
            public int CallCount { get; private set; }

            public bool TryFulfill(
                in ServiceHandoffRequest request,
                out string failure)
            {
                CallCount++;
                failure = string.Empty;
                return true;
            }
        }

        private sealed class RejectingPreflightHandoff :
            IServiceHandoffBackend,
            IServiceHandoffPreflight
        {
            public int FulfillCallCount { get; private set; }

            public bool CanFulfill(
                in ServiceHandoffRequest request,
                out string failure)
            {
                failure = "No vehicle-bound cover target exists.";
                return false;
            }

            public bool TryFulfill(
                in ServiceHandoffRequest request,
                out string failure)
            {
                FulfillCallCount++;
                failure = string.Empty;
                return true;
            }
        }

        private sealed class DeniedLocationAvailability :
            IServiceAvailabilitySource
        {
            private readonly string deniedLocationId;

            public DeniedLocationAvailability(string deniedLocationId)
            {
                this.deniedLocationId = deniedLocationId;
            }

            public bool IsLocationStaffed(string locationId) =>
                !string.Equals(
                    locationId,
                    deniedLocationId,
                    StringComparison.Ordinal);
        }

        private sealed class FakeEconomy : IEconomyTransactionService
        {
            private readonly Dictionary<string, EconomyTransactionRequest>
                committed = new(StringComparer.Ordinal);
            private long balance;
            private ulong revision;

            public FakeEconomy(long initialBalance)
            {
                balance = initialBalance;
            }

            public int CommitCallCount { get; private set; }
            public EconomySnapshot Snapshot =>
                new(revision, balance, committed.Count);
            public event Action<EconomySnapshot> StateChanged;

            public bool TryQuote(
                string priceId,
                int quantity,
                out EconomyPriceQuote quote,
                out EconomyTransactionFailureReason failureReason)
            {
                quote = default;
                failureReason = EconomyTransactionFailureReason.UnknownPrice;
                return false;
            }

            public bool TryPurchase(
                string transactionId,
                string priceId,
                int quantity,
                string sourceStableId,
                out EconomyTransactionReceipt receipt)
            {
                receipt = Failure(transactionId);
                return false;
            }

            public bool TryRefund(
                string transactionId,
                string originalTransactionId,
                long amountMinorUnits,
                string sourceStableId,
                out EconomyTransactionReceipt receipt)
            {
                receipt = Failure(transactionId);
                return false;
            }

            public bool TryCommit(
                in EconomyTransactionRequest request,
                out EconomyTransactionReceipt receipt)
            {
                CommitCallCount++;
                if (committed.TryGetValue(request.TransactionId, out _))
                {
                    receipt = new EconomyTransactionReceipt(
                        true,
                        true,
                        EconomyTransactionFailureReason.None,
                        request.TransactionId,
                        balance,
                        balance,
                        committed.Count);
                    return true;
                }

                if (request.AmountMinorUnits <= 0 ||
                    request.AmountMinorUnits > balance)
                {
                    receipt = Failure(request.TransactionId);
                    return false;
                }

                long before = balance;
                balance -= request.AmountMinorUnits;
                committed.Add(request.TransactionId, request);
                revision++;
                receipt = new EconomyTransactionReceipt(
                    true,
                    false,
                    EconomyTransactionFailureReason.None,
                    request.TransactionId,
                    before,
                    balance,
                    committed.Count);
                StateChanged?.Invoke(Snapshot);
                return true;
            }

            private EconomyTransactionReceipt Failure(string transactionId) =>
                new(
                    false,
                    false,
                    EconomyTransactionFailureReason.InvalidRequest,
                    transactionId,
                    balance,
                    balance,
                    0);
        }
    }
}
