using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Services.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Services.Tests.EditMode
{
    public sealed class ServiceInteractionTargetsTests
    {
        private readonly List<UnityEngine.Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

        [Test]
        public void StoreTargets_ResolveCatalogAnchor_AndUseAuthoritativeBasketCheckout()
        {
            RuntimeRig rig = CreateRig();
            TeimoStoreOfferInteractionTarget shelf = AddTarget<
                TeimoStoreOfferInteractionTarget>("StoreShelf");
            shelf.ConfigureForAuthoring(
                StoreLocationId,
                Anchor(StoreLocationId),
                StoreOfferId);
            shelf.Bind(rig.Runtime);

            Assert.That(shelf.TryValidateBinding(out string failure), Is.True, failure);
            Assert.That(shelf.CanInteract(Context()), Is.True);
            shelf.Interact(Context());
            Assert.That(rig.Runtime.GetBasketQuantity(StoreOfferId), Is.EqualTo(1));
            Assert.That(shelf.LastFeedback.Result.Succeeded, Is.True);

            TeimoStoreCheckoutInteractionTarget checkout = AddTarget<
                TeimoStoreCheckoutInteractionTarget>("StoreCheckout");
            checkout.ConfigureForAuthoring(
                StoreLocationId,
                Anchor(StoreLocationId));
            checkout.Bind(rig.Runtime);
            checkout.Interact(Context());

            Assert.That(checkout.LastFeedback.Result.Succeeded, Is.True);
            Assert.That(rig.Economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(rig.Runtime.GetBasketQuantity(StoreOfferId), Is.Zero);
            Assert.That(rig.Handoff.FulfillCallCount, Is.EqualTo(1));
        }

        [Test]
        public void PubTarget_PurchasesThroughRuntime_WithoutOwningMoneyState()
        {
            RuntimeRig rig = CreateRig();
            TeimoPubOfferInteractionTarget target = AddTarget<
                TeimoPubOfferInteractionTarget>("PubOffer");
            target.ConfigureForAuthoring(
                PubLocationId,
                Anchor(PubLocationId),
                PubOfferId);
            target.Bind(rig.Runtime);

            target.Interact(Context());

            Assert.That(target.LastFeedback.Result.Succeeded, Is.True);
            Assert.That(target.LastFeedback.Kind, Is.EqualTo(
                ServiceInteractionKind.PubPurchase));
            Assert.That(rig.Economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(rig.Handoff.FulfillCallCount, Is.EqualTo(1));
        }

        [Test]
        public void FuelTarget_WithoutRealReceiver_ExposesUnavailable_AndRecordsNothing()
        {
            RuntimeRig rig = CreateRig();
            ServiceFuelDispenseInteractionTarget target = AddTarget<
                ServiceFuelDispenseInteractionTarget>("FuelPump");
            target.ConfigureForAuthoring(
                FuelLocationId,
                Anchor(FuelLocationId),
                FuelOfferId,
                configuredReceiver: null,
                configuredRequestedLitres: 0.75f);
            target.Bind(rig.Runtime);
            long initialSequence = rig.Runtime.CaptureDto().nextOperationSequence;

            Assert.That(target.CanInteract(Context()), Is.True);
            Assert.That(target.HasUsableReceiver, Is.False);
            Assert.That(target.HasActiveFuelSession, Is.False);
            Assert.That(target.InteractionPrompt, Does.Contain("\u041d\u0435\u0447\u0435\u0433\u043e"));
            Assert.That(
                target.InteractionDisplayName,
                Does.Contain("4,75 MK/\u043b"),
                "The pump HUD title must expose the authoritative current price per litre.");
            target.Interact(Context());

            Assert.That(target.LastFeedback.Result.Succeeded, Is.False);
            Assert.That(target.TryReleaseNozzle().Succeeded, Is.False);
            Assert.That(target.HasActiveFuelSession, Is.False);
            Assert.That(
                rig.Runtime.GetFuelDebtMinorUnits(FuelGrade.Gasoline98),
                Is.Zero);
            Assert.That(rig.Runtime.CaptureDto().fuelStolen, Is.False);
            Assert.That(
                rig.Runtime.CaptureDto().nextOperationSequence,
                Is.EqualTo(initialSequence));
            Assert.That(rig.Economy.CommitCallCount, Is.Zero);
        }

        [Test]
        public void FuelTarget_RecordsOnlyVolumeAcceptedByExplicitLiquidReceiver()
        {
            RuntimeRig rig = CreateRig();
            GameObject receiverObject = Own(new GameObject("RealFuelReceiver"));
            FakeFuelReceiver receiver = receiverObject.AddComponent<
                FakeFuelReceiver>();
            receiver.AcceptedLitres = 0.75f;

            ServiceFuelDispenseInteractionTarget target = AddTarget<
                ServiceFuelDispenseInteractionTarget>("FuelPump");
            target.ConfigureForAuthoring(
                FuelLocationId,
                Anchor(FuelLocationId),
                FuelOfferId,
                receiver,
                1f);
            target.Bind(rig.Runtime);
            target.Interact(Context());

            FuelDebtStateDto debt = rig.Runtime.CaptureDto().fuelDebts.Single();
            Assert.That(receiver.LastLiquidId, Is.EqualTo(
                "liquid.gasoline"));
            Assert.That(debt.dispensedMilliliters, Is.EqualTo(750));
            Assert.That(debt.chargeMinorUnits, Is.EqualTo(356));
            Assert.That(target.LastFeedback.Result.Succeeded, Is.True);
            Assert.That(target.HasActiveFuelSession, Is.True);
            Assert.That(rig.Runtime.CaptureDto().fuelStolen, Is.False,
                "Accepted fuel is debt; donor theft starts only when the used nozzle is released.");

            ServiceResult released = target.TryReleaseNozzle();

            Assert.That(released.Succeeded, Is.True);
            Assert.That(target.HasActiveFuelSession, Is.False);
            Assert.That(rig.Runtime.CaptureDto().fuelStolen, Is.True);
            Assert.That(target.TryReleaseNozzle().Succeeded, Is.False,
                "The target must not release the same physical nozzle session twice.");
            Assert.That(rig.Economy.CommitCallCount, Is.Zero,
                "Dispensing records Teimo debt; checkout owns the later debit.");
        }

        [Test]
        public void FuelTarget_UnrepresentableRequestedVolume_FailsBeforeReceiverMutation()
        {
            RuntimeRig rig = CreateRig();
            GameObject receiverObject = Own(new GameObject("GuardedFuelReceiver"));
            FakeFuelReceiver receiver = receiverObject.AddComponent<
                FakeFuelReceiver>();
            receiver.AcceptedLitres = 1f;

            ServiceFuelDispenseInteractionTarget target = AddTarget<
                ServiceFuelDispenseInteractionTarget>("FuelPump");
            target.ConfigureForAuthoring(
                FuelLocationId,
                Anchor(FuelLocationId),
                FuelOfferId,
                receiver,
                float.MaxValue);
            target.Bind(rig.Runtime);

            target.Interact(Context());

            Assert.That(target.LastFeedback.Result.FailureReason, Is.EqualTo(
                ServiceFailureReason.InvalidRequest));
            Assert.That(receiver.CanReceiveCallCount, Is.Zero);
            Assert.That(receiver.TryReceiveCallCount, Is.Zero,
                "Checked litres-to-millilitres validation and runtime preflight must precede receiver mutation.");
            Assert.That(rig.Runtime.CaptureDto().fuelDebts, Is.Empty);
            Assert.That(rig.Runtime.CaptureDto().fuelStolen, Is.False);
            Assert.That(target.HasActiveFuelSession, Is.False);
        }

        [Test]
        public void FuelTarget_AccountingPreflightRejectsBeforeReceiverMutation()
        {
            RuntimeRig rig = CreateRig();
            ServiceResult seeded = rig.Runtime.TryRecordAcceptedFuel(
                FuelGrade.Gasoline98,
                long.MaxValue - 1_000L);
            Assert.That(seeded.Succeeded, Is.True);
            long debtBefore = rig.Runtime.CaptureDto()
                .fuelDebts.Single().dispensedMilliliters;

            GameObject receiverObject = Own(new GameObject("GuardedFuelReceiver"));
            FakeFuelReceiver receiver = receiverObject.AddComponent<
                FakeFuelReceiver>();
            receiver.AcceptedLitres = 1f;

            ServiceFuelDispenseInteractionTarget target = AddTarget<
                ServiceFuelDispenseInteractionTarget>("FuelPump");
            target.ConfigureForAuthoring(
                FuelLocationId,
                Anchor(FuelLocationId),
                FuelOfferId,
                receiver,
                2f);
            target.Bind(rig.Runtime);

            target.Interact(Context());

            Assert.That(target.LastFeedback.Result.FailureReason, Is.EqualTo(
                ServiceFailureReason.TransactionRejected));
            Assert.That(receiver.CanReceiveCallCount, Is.Zero);
            Assert.That(receiver.TryReceiveCallCount, Is.Zero,
                "Accounting rejection must happen before the receiver can accept physical fuel.");
            Assert.That(
                rig.Runtime.CaptureDto().fuelDebts.Single()
                    .dispensedMilliliters,
                Is.EqualTo(debtBefore));
            Assert.That(rig.Runtime.CaptureDto().fuelStolen, Is.False);
            Assert.That(target.HasActiveFuelSession, Is.False);
        }

        [Test]
        public void WorkshopAndInspectionTargets_WithoutRealBackends_NeverDebitOrAllocate()
        {
            RuntimeRig rig = CreateRig();
            FleetariWorkshopInteractionTarget workshop = AddTarget<
                FleetariWorkshopInteractionTarget>("FleetariBrochure");
            workshop.ConfigureForAuthoring(
                WorkshopLocationId,
                Anchor(WorkshopLocationId),
                WorkshopOfferId,
                configuredPlayerVehicle: null);
            workshop.Bind(rig.Runtime);

            VehicleInspectionInteractionTarget inspection = AddTarget<
                VehicleInspectionInteractionTarget>("InspectionDesk");
            inspection.ConfigureForAuthoring(
                InspectionLocationId,
                Anchor(InspectionLocationId),
                InspectionOfferId);
            inspection.Bind(rig.Runtime);
            ServiceStateDto before = rig.Runtime.CaptureDto();

            Assert.That(workshop.CanInteract(Context()), Is.True);
            Assert.That(workshop.InteractionPrompt, Does.Contain("\u041d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u043e"));
            workshop.Interact(Context());
            inspection.Interact(Context());

            Assert.That(workshop.LastFeedback.Result.FailureReason, Is.EqualTo(
                ServiceFailureReason.VehicleOutcomeUnavailable));
            Assert.That(inspection.LastFeedback.Result.FailureReason, Is.EqualTo(
                ServiceFailureReason.InspectionUnavailable));
            Assert.That(rig.Economy.CommitCallCount, Is.Zero);
            Assert.That(rig.Runtime.WorkshopPhase, Is.EqualTo(
                WorkshopOrderPhase.None));
            Assert.That(rig.Runtime.CaptureDto().inspectionOrder.phase, Is.EqualTo(
                (int)InspectionOrderPhase.None));
            Assert.That(
                rig.Runtime.CaptureDto().nextOperationSequence,
                Is.EqualTo(before.nextOperationSequence),
                "Unavailable shells must not even allocate a paid operation ID.");
        }

        [Test]
        public void Binding_WithWrongCatalogAnchor_IsNotInteractable()
        {
            RuntimeRig rig = CreateRig();
            TeimoStoreOfferInteractionTarget target = AddTarget<
                TeimoStoreOfferInteractionTarget>("WrongAnchor");
            target.ConfigureForAuthoring(
                StoreLocationId,
                "service.anchor.tests.wrong",
                StoreOfferId);
            target.Bind(rig.Runtime);

            Assert.That(target.TryValidateBinding(out string failure), Is.False);
            Assert.That(failure, Does.Contain("does not match"));
            Assert.That(target.CanInteract(Context()), Is.False);

            TeimoStoreOfferInteractionTarget wrongOffer = AddTarget<
                TeimoStoreOfferInteractionTarget>("WrongOffer");
            wrongOffer.ConfigureForAuthoring(
                StoreLocationId,
                Anchor(StoreLocationId),
                PubOfferId);
            wrongOffer.Bind(rig.Runtime);

            Assert.That(wrongOffer.CanInteract(Context()), Is.False,
                "An offer from another catalog location must not materialize as an interaction.");
        }

        private RuntimeRig CreateRig()
        {
            ServiceCatalog catalog = Own(CreateCatalog());
            var economy = new FakeEconomy(1_000_000);
            var handoff = new FakeHandoff();
            GameObject runtimeObject = Own(new GameObject("ServiceRuntime"));
            ServiceRuntime runtime = runtimeObject.AddComponent<ServiceRuntime>();
            runtime.Initialize(
                catalog,
                economy,
                new GameTimeService(GameTimeConfig.RemakeDesignTargetDefaults),
                configuredHandoff: handoff,
                configuredInspection: null,
                configuredWorkshopOutcome: null);
            return new RuntimeRig(runtime, economy, handoff);
        }

        private T AddTarget<T>(string name) where T : Component
        {
            GameObject targetObject = Own(new GameObject(name));
            return targetObject.AddComponent<T>();
        }

        private InteractionContext Context() =>
            new(null, Vector3.zero, Vector3.forward);

        private T Own<T>(T instance) where T : UnityEngine.Object
        {
            owned.Add(instance);
            return instance;
        }

        private static ServiceCatalog CreateCatalog()
        {
            ServiceAvailabilityWindow allDay = new();
            allDay.ConfigureForAuthoring(127, 0, 0);
            string[] locationIds =
            {
                StoreLocationId,
                PubLocationId,
                FuelLocationId,
                WorkshopLocationId,
                InspectionLocationId,
            };
            ServiceLocationKind[] locationKinds =
            {
                ServiceLocationKind.Store,
                ServiceLocationKind.Pub,
                ServiceLocationKind.FuelStation,
                ServiceLocationKind.Workshop,
                ServiceLocationKind.Inspection,
            };
            var locations = new ServiceLocationDefinition[locationIds.Length];
            for (int index = 0; index < locations.Length; index++)
            {
                locations[index] = new ServiceLocationDefinition();
                locations[index].ConfigureForAuthoring(
                    locationIds[index],
                    locationIds[index],
                    $"service.source.tests.{index}",
                    locationKinds[index],
                    Anchor(locationIds[index]),
                    $"service.anchor.tests.handoff-{index}",
                    Vector3.zero,
                    new[] { allDay });
            }

            ServiceOfferDefinition[] offers =
            {
                Offer(
                    StoreOfferId,
                    StoreLocationId,
                    ServiceOfferKind.RetailItem,
                    1_000,
                    itemId: "item.tests.store",
                    stockCapacity: 2),
                Offer(
                    PubOfferId,
                    PubLocationId,
                    ServiceOfferKind.PubItem,
                    800,
                    effectId: "service.effect.tests.pub"),
                Offer(
                    FuelOfferId,
                    FuelLocationId,
                    ServiceOfferKind.Fuel,
                    0,
                    fuelGrade: FuelGrade.Gasoline98),
                Offer(
                    WorkshopOfferId,
                    WorkshopLocationId,
                    ServiceOfferKind.Workshop,
                    875_000),
                Offer(
                    InspectionOfferId,
                    InspectionLocationId,
                    ServiceOfferKind.Inspection,
                    32_500),
            };
            var fuelPrices = new ServiceFuelPriceDefinition[3];
            for (int index = 0; index < fuelPrices.Length; index++)
            {
                fuelPrices[index] = new ServiceFuelPriceDefinition();
                FuelGrade grade = (FuelGrade)index;
                long price = grade switch
                {
                    FuelGrade.Gasoline98 => 475,
                    FuelGrade.Diesel => 423,
                    _ => 213,
                };
                fuelPrices[index].ConfigureForAuthoring(
                    grade,
                    price,
                    price,
                    price);
            }

            ServiceCatalog catalog = ScriptableObject.CreateInstance<
                ServiceCatalog>();
            catalog.ConfigureForAuthoring(
                "service.catalog.interaction-tests",
                new string('c', 64),
                locations,
                offers,
                fuelPrices);
            Assert.That(catalog.TryValidate(out string failure), Is.True, failure);
            return catalog;
        }

        private static ServiceOfferDefinition Offer(
            string offerId,
            string locationId,
            ServiceOfferKind kind,
            long price,
            string itemId = "",
            string effectId = "",
            int stockCapacity = 0,
            FuelGrade fuelGrade = FuelGrade.Gasoline98)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                locationId,
                offerId,
                kind,
                configuredPriceId: string.Empty,
                configuredItemDefinitionId: itemId,
                configuredEffectId: effectId,
                configuredStockCapacity: stockCapacity,
                configuredFuelGrade: fuelGrade,
                configuredBasePriceMinorUnits: price);
            return offer;
        }

        private static string Anchor(string locationId) =>
            locationId + ".interaction";

        private const string StoreLocationId = "service.location.teimo-store";
        private const string PubLocationId = "service.location.teimo-pub";
        private const string FuelLocationId = "service.location.teimo-fuel";
        private const string WorkshopLocationId =
            "service.location.workshop.fleetari";
        private const string InspectionLocationId =
            "service.location.inspection-station";
        private const string StoreOfferId = "service.retail.tests.sausages";
        private const string PubOfferId = "service.pub.tests.beer";
        private const string FuelOfferId = "service.fuel.tests.gasoline-98";
        private const string WorkshopOfferId =
            "service.workshop.tests.body-repair";
        private const string InspectionOfferId =
            "service.inspection.tests.vehicle";

        private readonly struct RuntimeRig
        {
            public RuntimeRig(
                ServiceRuntime runtime,
                FakeEconomy economy,
                FakeHandoff handoff)
            {
                Runtime = runtime;
                Economy = economy;
                Handoff = handoff;
            }

            public ServiceRuntime Runtime { get; }
            public FakeEconomy Economy { get; }
            public FakeHandoff Handoff { get; }
        }

        private sealed class FakeFuelReceiver : MonoBehaviour,
            ILiquidReceiverTarget
        {
            public float AcceptedLitres { get; set; }
            public string LastLiquidId { get; private set; } = string.Empty;
            public int CanReceiveCallCount { get; private set; }
            public int TryReceiveCallCount { get; private set; }

            public bool CanReceiveLiquid(
                string liquidId,
                float offeredLitres,
                in InteractionContext context)
            {
                CanReceiveCallCount++;
                return offeredLitres > 0f;
            }

            public bool TryReceiveLiquid(
                string liquidId,
                float offeredLitres,
                in InteractionContext context,
                out float acceptedLitres)
            {
                TryReceiveCallCount++;
                LastLiquidId = liquidId;
                acceptedLitres = Mathf.Min(offeredLitres, AcceptedLitres);
                return acceptedLitres > 0f;
            }
        }

        private sealed class FakeHandoff : IServiceHandoffBackend,
            IServiceHandoffPreflight
        {
            public int FulfillCallCount { get; private set; }

            public bool CanFulfill(
                in ServiceHandoffRequest request,
                out string failure)
            {
                failure = string.Empty;
                return true;
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
                if (committed.ContainsKey(request.TransactionId))
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
