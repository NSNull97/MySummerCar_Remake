using System;
using System.IO;
using System.Linq;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Services;
using MSC.Weather.Production;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class ServicesSaveIntegrationTests
    {
        private ServiceCatalog catalog;
        private GameObject runtimeObject;
        private GameTimeService clock;
        private ServiceRuntime runtime;
        private ServicesSaveParticipant participant;

        [SetUp]
        public void SetUp()
        {
            catalog = CreateCatalog();
            runtimeObject = new GameObject("ServicesSaveRuntimeFixture");
            runtime = runtimeObject.AddComponent<ServiceRuntime>();
            clock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            runtime.Initialize(
                catalog,
                new FakeEconomyTransactionService(),
                clock);
            participant = new ServicesSaveParticipant(runtime);
        }

        [TearDown]
        public void TearDown()
        {
            if (runtimeObject != null)
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
            }

            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void VersionFourteen_AddsRequiredFreshServicesWithoutMutatingSource()
        {
            ServiceStateDto fresh = ServiceRuntime.CreateFreshState(
                catalog,
                new GameTimeService(GameTimeConfig.RemakeDesignTargetDefaults)
                    .Snapshot);
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 14 },
                Domains = Array.Empty<SaveDomainEnvelope>(),
            };

            SaveDocument migrated = new Milestone12AServicesSaveMigration(
                    fresh,
                    domainRequired: true)
                .Migrate(source, new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(14));
            Assert.That(source.Domains, Is.Empty);
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(15));
            SaveDomainEnvelope envelope = migrated.Domains.Single();
            Assert.That(
                envelope.DomainId,
                Is.EqualTo(ServicesSaveParticipant.DomainId));
            Assert.That(envelope.Required, Is.True);
            ServiceStateDto restored =
                SaveParticipantJson.Deserialize<ServiceStateDto>(
                    envelope.PayloadJson);
            Assert.That(
                restored.TryValidate(catalog, out string failure),
                Is.True,
                failure);
        }

        [Test]
        public void Participant_PreflightsCatalogAndRollbackRestoresCheckpoint()
        {
            ServiceStateDto checkpoint = runtime.CaptureDto();
            ServiceStateDto changed = checkpoint.DeepClone();
            changed.brokenWindowChargeMinorUnits = 1_000;
            Assert.That(
                runtime.TryRestoreDto(changed, out string restoreFailure),
                Is.True,
                restoreFailure);

            participant.Rollback(checkpoint);

            Assert.That(
                runtime.CaptureDto().brokenWindowChargeMinorUnits,
                Is.Zero);

            ServiceStateDto incompatible = checkpoint.DeepClone();
            incompatible.catalogId = "catalog.tests.incompatible";
            Assert.Throws<InvalidDataException>(() =>
                participant.PrepareRestore(
                    Envelope(incompatible),
                    new SaveRestorePreparationContext(
                        new UnresolvedContentReport(),
                        new DeferredStableEntityStore())));
        }

        [Test]
        public void StateValidation_RejectsStockPlusReservedBeyondCapacity()
        {
            ServiceStateDto dto = runtime.CaptureDto();
            RetailStockStateDto stock = dto.retailStocks.Single();
            dto.storeBasket = new[]
            {
                new StoreBasketLineDto
                {
                    offerId = stock.offerId,
                    quantity = 1,
                },
            };

            Assert.That(
                dto.TryValidate(catalog, out string failure),
                Is.False,
                failure);
        }

        [Test]
        public void StateValidation_RejectsCompletedOperationWithIncompleteHandoff()
        {
            ServiceStateDto dto = runtime.CaptureDto();
            ServiceOfferDefinition offer = catalog.Offers.Single(
                value => value.Kind == ServiceOfferKind.RetailItem);
            dto.pendingOperations = new[]
            {
                new ServiceOperationStateDto
                {
                    operationId = "service.operation.tests.incomplete",
                    transactionId = "service.transaction.tests.incomplete",
                    locationId = offer.LocationId,
                    phase = (int)ServiceOperationPhase.Completed,
                    amountMinorUnits = 100,
                    handoffLines = new[]
                    {
                        new ServiceHandoffLineDto
                        {
                            offerId = offer.OfferId,
                            itemDefinitionId = offer.ItemDefinitionId,
                            variantIndex = offer.VariantIndex,
                            quantity = 1,
                            completed = false,
                        },
                    },
                },
            };

            Assert.That(
                dto.TryValidate(catalog, out string failure),
                Is.False,
                failure);
        }

        [Test]
        public void StateValidation_AllowsSubMinorUnitFuelDebtToAccumulate()
        {
            ServiceStateDto dto = runtime.CaptureDto();
            dto.fuelDebts = new[]
            {
                new FuelDebtStateDto
                {
                    grade = (int)FuelGrade.Gasoline98,
                    dispensedMilliliters = 1,
                    chargeMinorUnits = 0,
                },
            };

            Assert.That(
                dto.TryValidate(catalog, out string failure),
                Is.True,
                failure);
        }

        [Test]
        public void VersionFourteen_WithoutServicesRuntimeFailsVisibly()
        {
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 14 },
                Domains = Array.Empty<SaveDomainEnvelope>(),
            };
            var migration = new Milestone12AServicesSaveMigration(
                configuredFreshState: null,
                domainRequired: false);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    migration.Migrate(source, new UnresolvedContentReport()));

            Assert.That(
                exception.Message,
                Does.Contain("without an initialized ServiceRuntime"));
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(14));
            Assert.That(source.Domains, Is.Empty);
        }

        [Test]
        public void Participant_UsesStagedCoreTimeBeforeLiveClockRestore()
        {
            var futureClock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            futureClock.Advance(
                GameTimeConfig.RemakeDesignTargetDefaults
                    .DayLengthSimulationSeconds * 3d);
            ServiceStateDto future = runtime.CaptureDto();
            future.lastProcessedDayIndex = futureClock.Snapshot.DayIndex;
            ServiceStateDto checkpoint = runtime.CaptureDto();

            var environmentObject =
                new GameObject("ServicesStagedClockEnvironmentFixture");
            try
            {
                ProductionEnvironmentController environment =
                    environmentObject.AddComponent<ProductionEnvironmentController>();
                var bridge = new ProductionEnvironmentRestoreBridge(environment);
                bridge.PrepareTimeForApply(
                    futureClock.CaptureDto(),
                    clock.CaptureDto());
                var stagedParticipant =
                    new ServicesSaveParticipant(runtime, bridge);

                Assert.DoesNotThrow(() => stagedParticipant.ApplyPreparedRestore(
                    future,
                    new SaveRestoreContext(
                        new UnresolvedContentReport(),
                        new DeferredStableEntityStore())));
                Assert.That(
                    runtime.CaptureDto().lastProcessedDayIndex,
                    Is.EqualTo(futureClock.Snapshot.DayIndex));
                Assert.That(clock.Snapshot.DayIndex, Is.Zero);

                Assert.DoesNotThrow(() => stagedParticipant.Rollback(checkpoint));
                Assert.That(
                    runtime.CaptureDto().lastProcessedDayIndex,
                    Is.EqualTo(clock.Snapshot.DayIndex));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(environmentObject);
            }
        }

        private static SaveDomainEnvelope Envelope(ServiceStateDto dto) =>
            new()
            {
                DomainId = ServicesSaveParticipant.DomainId,
                SchemaVersion = ServiceStateDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = SaveParticipantJson.Serialize(dto),
            };

        private static ServiceCatalog CreateCatalog()
        {
            var availability = new ServiceAvailabilityWindow();
            availability.ConfigureForAuthoring(127, 0, 0);
            var inspectionLocation = new ServiceLocationDefinition();
            inspectionLocation.ConfigureForAuthoring(
                "service.location.inspection-station",
                "Inspection fixture",
                "service.source.tests.inspection",
                ServiceLocationKind.Inspection,
                "service.anchor.tests.interaction",
                "service.anchor.tests.handoff",
                Vector3.zero,
                new[] { availability });
            var workshopLocation = new ServiceLocationDefinition();
            workshopLocation.ConfigureForAuthoring(
                "service.location.workshop.fleetari",
                "Fleetari fixture",
                "service.source.tests.workshop",
                ServiceLocationKind.Workshop,
                "service.anchor.tests.workshop.interaction",
                "service.anchor.tests.workshop.handoff",
                Vector3.one,
                new[] { availability });
            var storeLocation = new ServiceLocationDefinition();
            storeLocation.ConfigureForAuthoring(
                "service.location.teimo-store",
                "Store fixture",
                "service.source.tests.store",
                ServiceLocationKind.Store,
                "service.anchor.tests.store.interaction",
                "service.anchor.tests.store.handoff",
                Vector3.right,
                new[] { availability });
            var inspectionOffer = new ServiceOfferDefinition();
            inspectionOffer.ConfigureForAuthoring(
                "service.offer.tests.inspection",
                inspectionLocation.LocationId,
                "Inspection",
                ServiceOfferKind.Inspection,
                string.Empty,
                configuredBasePriceMinorUnits: 100);
            var retailOffer = new ServiceOfferDefinition();
            retailOffer.ConfigureForAuthoring(
                "service.offer.tests.retail",
                storeLocation.LocationId,
                "Retail fixture",
                ServiceOfferKind.RetailItem,
                string.Empty,
                configuredItemDefinitionId: "item.tests.retail",
                configuredQuantityPerUnit: 1,
                configuredStockCapacity: 10,
                configuredBasePriceMinorUnits: 100);
            var gasolinePrice = new ServiceFuelPriceDefinition();
            gasolinePrice.ConfigureForAuthoring(
                FuelGrade.Gasoline98,
                initial: 475,
                minimum: 410,
                maximum: 530);
            var configured = ScriptableObject.CreateInstance<ServiceCatalog>();
            configured.ConfigureForAuthoring(
                "catalog.tests.services",
                new string('a', 64),
                new[]
                {
                    inspectionLocation,
                    workshopLocation,
                    storeLocation,
                },
                new[] { inspectionOffer, retailOffer },
                new[] { gasolinePrice });
            return configured;
        }

        private sealed class FakeEconomyTransactionService :
            IEconomyTransactionService
        {
            public EconomySnapshot Snapshot => new(0, 300_000, 0);

            public event Action<EconomySnapshot> StateChanged
            {
                add { }
                remove { }
            }

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
                receipt = default;
                return false;
            }

            public bool TryRefund(
                string transactionId,
                string originalTransactionId,
                long amountMinorUnits,
                string sourceStableId,
                out EconomyTransactionReceipt receipt)
            {
                receipt = default;
                return false;
            }

            public bool TryCommit(
                in EconomyTransactionRequest request,
                out EconomyTransactionReceipt receipt)
            {
                receipt = default;
                return false;
            }
        }
    }
}
