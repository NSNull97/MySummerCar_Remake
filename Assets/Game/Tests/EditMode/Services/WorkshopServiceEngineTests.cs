using System;
using System.Collections.Generic;
using MSC.Economy;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Services.Tests.EditMode
{
    public sealed class WorkshopServiceEngineTests
    {
        private const string WorkshopLocationId =
            "service.location.workshop.fleetari";

        private ServiceCatalog catalog;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null)
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                catalog = null;
            }
        }

        [Test]
        public void Quote_UsesExactCatalogPricesAndNormalDiscount()
        {
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000);
            ServiceResult selection = engine.TrySetSelection(Selection(
                "service.workshop.body-repair",
                "service.workshop.door-left",
                "service.workshop.tires-standard"));

            bool quoted = engine.TryQuote(
                new DateTime(1995, 8, 10),
                out WorkshopPriceQuote quote,
                out ServiceFailureReason failure);

            Assert.That(selection.Succeeded, Is.True);
            Assert.That(quoted, Is.True);
            Assert.That(failure, Is.EqualTo(ServiceFailureReason.None));
            Assert.That(quote.BasePriceMinorUnits, Is.EqualTo(1_173_000));
            Assert.That(
                quote.DiscountBasisPoints,
                Is.EqualTo(WorkshopServiceEngine.NormalDiscountBasisPoints));
            Assert.That(quote.PayableMinorUnits, Is.EqualTo(1_055_700));
        }

        [TestCase(24)]
        [TestCase(25)]
        [TestCase(26)]
        public void Quote_DecemberTwentyFourThroughTwentySix_PaysQuarter(
            int day)
        {
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair",
                "service.workshop.door-left",
                "service.workshop.tires-standard"));

            bool quoted = engine.TryQuote(
                new DateTime(1995, 12, day),
                out WorkshopPriceQuote quote,
                out _);

            Assert.That(quoted, Is.True);
            Assert.That(
                quote.DiscountBasisPoints,
                Is.EqualTo(
                    WorkshopServiceEngine.ChristmasDiscountBasisPoints));
            Assert.That(quote.PayableMinorUnits, Is.EqualTo(293_250));
        }

        [Test]
        public void EmptySelection_DoesNotCreatePaymentOrOrder()
        {
            var economy = new FakeEconomy(initialBalance: 2_000_000);
            WorkshopServiceEngine engine = CreateEngine(economy: economy);

            ServiceResult result = engine.TryPlaceOrder(
                "service.order.fleetari.empty",
                "service.transaction.fleetari.empty",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.EmptyBasket));
            Assert.That(economy.CommitCallCount, Is.Zero);
            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.None));
        }

        [Test]
        public void InsufficientFunds_DoesNotMutateWorkshopState()
        {
            var economy = new FakeEconomy(initialBalance: 100);
            WorkshopServiceEngine engine = CreateEngine(economy: economy);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair"));

            ServiceResult result = engine.TryPlaceOrder(
                "service.order.fleetari.poor",
                "service.transaction.fleetari.poor",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.InsufficientFunds));
            Assert.That(economy.Snapshot.BalanceMinorUnits, Is.EqualTo(100));
            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.None));
        }

        [Test]
        public void SameOrderReplay_DebitsExactlyOnce()
        {
            var economy = new FakeEconomy(initialBalance: 2_000_000);
            WorkshopServiceEngine engine = CreateEngine(economy: economy);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair"));

            ServiceResult first = engine.TryPlaceOrder(
                "service.order.fleetari.body",
                "service.transaction.fleetari.body",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);
            ServiceResult replay = engine.TryPlaceOrder(
                "service.order.fleetari.body",
                "service.transaction.fleetari.body",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.WasIdempotentReplay, Is.False);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.WasIdempotentReplay, Is.True);
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(
                economy.Snapshot.BalanceMinorUnits,
                Is.EqualTo(1_212_500));
        }

        [Test]
        public void AcknowledgedOrderId_CannotStartSecondFreeWorkCycle()
        {
            var economy = new FakeEconomy(initialBalance: 2_000_000);
            var backend = new FakeWorkshopOutcomeBackend
            {
                ApplySucceeds = true,
            };
            WorkshopServiceEngine engine = CreateEngine(
                economy: economy,
                backend: backend);
            WorkshopSelectionStateDto selection = Selection(
                "service.workshop.body-repair");
            engine.TrySetSelection(selection);
            const string orderId = "service.order.fleetari.acknowledged";
            const string transactionId =
                "service.transaction.fleetari.acknowledged";

            ServiceResult first = engine.TryPlaceOrder(
                orderId,
                transactionId,
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);
            engine.Tick(
                WorkshopServiceEngine.MinimumWorkRealSeconds,
                WorkshopServiceEngine.WorkPauseDistanceMeters + 1f);
            engine.Tick(
                WorkshopServiceEngine.OutcomeFinalizingRealSeconds,
                0f);
            Assert.That(
                engine.TryAcknowledgeReadyOrder().Succeeded,
                Is.True);
            Assert.That(engine.TrySetSelection(selection).Succeeded, Is.True);
            long balanceAfterFirst = economy.Snapshot.BalanceMinorUnits;

            ServiceResult staleReplay = engine.TryPlaceOrder(
                orderId,
                transactionId,
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(staleReplay.Succeeded, Is.False);
            Assert.That(
                staleReplay.FailureReason,
                Is.EqualTo(ServiceFailureReason.TransactionRejected));
            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.None));
            Assert.That(
                economy.Snapshot.BalanceMinorUnits,
                Is.EqualTo(balanceAfterFirst));
            Assert.That(economy.CommitCallCount, Is.EqualTo(2));
            Assert.That(backend.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void Timer_PausesWithinHundredMeters_ThenFinalizesForSevenSeconds()
        {
            var backend = new FakeWorkshopOutcomeBackend
            {
                ApplySucceeds = true,
            };
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000,
                backend: backend);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair"));
            engine.TryPlaceOrder(
                "service.order.fleetari.timer",
                "service.transaction.fleetari.timer",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            bool changedNear = engine.Tick(1_000f, 100f);
            WorkshopOrderStateDto paused = engine.CaptureOrderDto();
            bool changedFar = engine.Tick(1_400f, 100.01f);

            Assert.That(changedNear, Is.False);
            Assert.That(
                paused.remainingWorkRealSeconds,
                Is.EqualTo(1_400f));
            Assert.That(changedFar, Is.True);
            Assert.That(
                engine.Phase,
                Is.EqualTo(WorkshopOrderPhase.ApplyingOutcomes));
            Assert.That(engine.OutcomeApplied, Is.True);
            Assert.That(backend.ApplyCount, Is.EqualTo(1));

            engine.Tick(6.9f, 0f);
            Assert.That(
                engine.Phase,
                Is.EqualTo(WorkshopOrderPhase.ApplyingOutcomes));
            engine.Tick(0.2f, 0f);
            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.Ready));

            ServiceResult retry = engine.TryRetryDeferredOutcome();
            Assert.That(retry.Succeeded, Is.False);
            Assert.That(backend.ApplyCount, Is.EqualTo(1));
            Assert.That(
                engine.TryAcknowledgeReadyOrder().Succeeded,
                Is.True);
            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.None));
        }

        [Test]
        public void MissingVehicleBackend_DefersOutcomeWithoutPretendingRepair()
        {
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000,
                backend: null);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair"));
            engine.TryPlaceOrder(
                "service.order.fleetari.deferred",
                "service.transaction.fleetari.deferred",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            engine.Tick(1_400f, 101f);
            engine.Tick(7f, 0f);
            ServiceResult acknowledge = engine.TryAcknowledgeReadyOrder();

            Assert.That(engine.Phase, Is.EqualTo(WorkshopOrderPhase.Ready));
            Assert.That(engine.OutcomeApplied, Is.False);
            Assert.That(engine.OutcomeDeferred, Is.True);
            Assert.That(acknowledge.Succeeded, Is.False);
            Assert.That(
                acknowledge.FailureReason,
                Is.EqualTo(
                    ServiceFailureReason.VehicleOutcomeUnavailable));
        }

        [Test]
        public void Restore_ContinuesTimerWithoutSecondDebitOrOutcomeReplay()
        {
            var economy = new FakeEconomy(initialBalance: 2_000_000);
            WorkshopServiceEngine original = CreateEngine(economy: economy);
            original.TrySetSelection(Selection(
                "service.workshop.door-left"));
            original.TryPlaceOrder(
                "service.order.fleetari.restore",
                "service.transaction.fleetari.restore",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);
            original.Tick(200f, 101f);

            WorkshopSelectionStateDto selection =
                original.CaptureSelectionDto();
            WorkshopOrderStateDto order = original.CaptureOrderDto();
            var backend = new FakeWorkshopOutcomeBackend
            {
                ApplySucceeds = true,
            };
            WorkshopServiceEngine restored = CreateEngine(
                economy: economy,
                backend: backend,
                reuseCatalog: true);

            bool accepted = restored.TryRestore(
                selection,
                order,
                out string failure);
            restored.Tick(1_200f, 101f);

            Assert.That(accepted, Is.True, failure);
            Assert.That(
                restored.Phase,
                Is.EqualTo(WorkshopOrderPhase.ApplyingOutcomes));
            Assert.That(restored.OutcomeApplied, Is.True);
            Assert.That(economy.CommitCallCount, Is.EqualTo(1));
            Assert.That(backend.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void ExclusiveWorkshopOffers_CannotBeSelectedTogether()
        {
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000);

            ServiceResult result = engine.TrySetSelection(Selection(
                "service.workshop.paint-regular",
                "service.workshop.paint-metallic"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(ServiceFailureReason.InvalidRequest));
        }

        [Test]
        public void LoanerState_TracksWarningAndOverdueWithoutSpawningVehicles()
        {
            var backend = new FakeWorkshopOutcomeBackend
            {
                ApplySucceeds = true,
                PlayerVehicleWithinRange = true,
                LoanerWithinRange = true,
            };
            WorkshopServiceEngine engine = CreateEngine(
                initialBalance: 2_000_000,
                backend: backend);
            engine.TrySetSelection(Selection(
                "service.workshop.body-repair"));
            engine.TryPlaceOrder(
                "service.order.fleetari.loaner",
                "service.transaction.fleetari.loaner",
                new DateTime(1995, 8, 10),
                WorkshopServiceEngine.MinimumWorkRealSeconds);

            ServiceResult borrowed = engine.TryBorrowLoaner(
                "vehicle.tests.loaner-service-target");
            engine.Tick(1_400f, 101f);
            engine.Tick(7f, 0f);
            engine.Tick(2_000f, 0f);
            WorkshopOrderStateDto warned = engine.CaptureOrderDto();
            engine.Tick(3_000f, 0f);
            WorkshopOrderStateDto overdue = engine.CaptureOrderDto();
            ServiceResult returned = engine.TryReturnLoaner();

            Assert.That(borrowed.Succeeded, Is.True);
            Assert.That(warned.loanerWarningNotified, Is.True);
            Assert.That(warned.loanerOverdue, Is.False);
            Assert.That(overdue.loanerOverdue, Is.True);
            Assert.That(
                overdue.relocatePlayerVehicleOnNextSleep,
                Is.True);
            Assert.That(returned.Succeeded, Is.True);
            Assert.That(
                engine.CaptureOrderDto()
                    .relocatePlayerVehicleOnNextSleep,
                Is.False);
        }

        private WorkshopServiceEngine CreateEngine(
            long initialBalance = 2_000_000,
            FakeEconomy economy = null,
            FakeWorkshopOutcomeBackend backend = null,
            bool reuseCatalog = false)
        {
            if (!reuseCatalog || catalog == null)
            {
                catalog = CreateCatalog();
            }

            return new WorkshopServiceEngine(
                catalog,
                economy ?? new FakeEconomy(initialBalance),
                WorkshopLocationId,
                backend);
        }

        private static WorkshopSelectionStateDto Selection(
            params string[] offerIds) =>
            new()
            {
                offerIds = offerIds,
                paintVariant = -1,
                rimVariant = -1,
                tireVariant = -1,
                finalGearRatio = 4.286f,
            };

        private static ServiceCatalog CreateCatalog()
        {
            var availability = new ServiceAvailabilityWindow();
            availability.ConfigureForAuthoring(127, 0, 0);
            var location = new ServiceLocationDefinition();
            location.ConfigureForAuthoring(
                WorkshopLocationId,
                "Fleetari",
                "service.source.workshop.fleetari",
                ServiceLocationKind.Workshop,
                "service.anchor.workshop.fleetari.interaction",
                "service.anchor.workshop.fleetari.handoff",
                new Vector3(1725.0482f, 6.3119974f, -301.45422f),
                new[] { availability });

            ServiceOfferDefinition[] offers =
            {
                Offer(
                    "service.workshop.body-repair",
                    875_000),
                Offer(
                    "service.workshop.door-left",
                    123_000),
                Offer(
                    "service.workshop.tires-standard",
                    175_000,
                    "service.workshop.group.tires"),
                Offer(
                    "service.workshop.paint-regular",
                    1_015_000,
                    "service.workshop.group.paint"),
                Offer(
                    "service.workshop.paint-metallic",
                    1_895_000,
                    "service.workshop.group.paint"),
            };

            ServiceCatalog result =
                ScriptableObject.CreateInstance<ServiceCatalog>();
            result.ConfigureForAuthoring(
                "service.catalog.tests",
                new string('a', 64),
                new[] { location },
                offers,
                Array.Empty<ServiceFuelPriceDefinition>());
            Assert.That(result.TryValidate(out string failure), Is.True, failure);
            return result;
        }

        private static ServiceOfferDefinition Offer(
            string offerId,
            long basePriceMinorUnits,
            string exclusiveGroupId = "")
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                WorkshopLocationId,
                offerId,
                ServiceOfferKind.Workshop,
                configuredPriceId: "",
                configuredExclusiveGroupId: exclusiveGroupId,
                configuredBasePriceMinorUnits: basePriceMinorUnits);
            return offer;
        }

        private sealed class FakeWorkshopOutcomeBackend :
            IWorkshopOutcomeBackend
        {
            public bool ApplySucceeds { get; set; }
            public bool PlayerVehicleWithinRange { get; set; }
            public bool LoanerWithinRange { get; set; }
            public bool RelocateSucceeds { get; set; }
            public int ApplyCount { get; private set; }

            public bool IsPlayerVehicleWithin(float distanceMeters) =>
                PlayerVehicleWithinRange;

            public bool IsLoanerWithin(float distanceMeters) =>
                LoanerWithinRange;

            public bool TryApply(
                in WorkshopOutcomeRequest request,
                out string failure)
            {
                ApplyCount++;
                failure = ApplySucceeds ? string.Empty : "Unavailable.";
                return ApplySucceeds;
            }

            public bool TryRelocatePlayerVehicleForOverdueLoaner(
                out string failure)
            {
                failure = RelocateSucceeds ? string.Empty : "Unavailable.";
                return RelocateSucceeds;
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
                receipt = Failure(
                    transactionId,
                    EconomyTransactionFailureReason.UnknownPrice);
                return false;
            }

            public bool TryRefund(
                string transactionId,
                string originalTransactionId,
                long amountMinorUnits,
                string sourceStableId,
                out EconomyTransactionReceipt receipt)
            {
                receipt = Failure(
                    transactionId,
                    EconomyTransactionFailureReason.RefundNotAllowed);
                return false;
            }

            public bool TryCommit(
                in EconomyTransactionRequest request,
                out EconomyTransactionReceipt receipt)
            {
                CommitCallCount++;
                if (committed.TryGetValue(
                        request.TransactionId,
                        out EconomyTransactionRequest existing))
                {
                    if (Matches(in existing, in request))
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

                    receipt = Failure(
                        request.TransactionId,
                        EconomyTransactionFailureReason
                            .DuplicateTransactionConflict);
                    return false;
                }

                if (request.Direction !=
                        EconomyTransactionDirection.Debit ||
                    request.AmountMinorUnits <= 0)
                {
                    receipt = Failure(
                        request.TransactionId,
                        EconomyTransactionFailureReason.InvalidRequest);
                    return false;
                }

                if (request.AmountMinorUnits > balance)
                {
                    receipt = Failure(
                        request.TransactionId,
                        EconomyTransactionFailureReason.InsufficientFunds);
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

            private EconomyTransactionReceipt Failure(
                string transactionId,
                EconomyTransactionFailureReason reason) =>
                new(
                    false,
                    false,
                    reason,
                    transactionId,
                    balance,
                    balance,
                    0);

            private static bool Matches(
                in EconomyTransactionRequest left,
                in EconomyTransactionRequest right) =>
                left.Kind == right.Kind &&
                left.Direction == right.Direction &&
                left.AmountMinorUnits == right.AmountMinorUnits &&
                string.Equals(
                    left.SourceStableId,
                    right.SourceStableId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.PriceId,
                    right.PriceId,
                    StringComparison.Ordinal) &&
                left.Quantity == right.Quantity &&
                string.Equals(
                    left.RelatedTransactionId,
                    right.RelatedTransactionId,
                    StringComparison.Ordinal);
        }
    }
}
