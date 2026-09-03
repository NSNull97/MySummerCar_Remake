using MSC.Core.Time;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Economy.Tests.EditMode
{
    public sealed class EconomyRuntimeTests
    {
        private const string CatalogPath =
            "Assets/Game/Economy/Content/Phase1/EconomyPriceCatalog.asset";

        [Test]
        public void GeneratedCatalog_MatchesLockedDonorFoundationAndExpandedShop()
        {
            EconomyPriceCatalog catalog = RequireCatalog();

            Assert.That(catalog.TryValidate(out string failure), Is.True, failure);
            Assert.That(catalog.InitialBalanceMinorUnits, Is.EqualTo(300_000));
            Assert.That(catalog.Prices.Count, Is.EqualTo(57));
            Assert.That(catalog.Scopes.Count, Is.EqualTo(1));
            Assert.That(
                catalog.TryGetPrice(
                    "price.store.juice",
                    out EconomyPriceDefinition juice),
                Is.True);
            Assert.That(juice.BaseMinorUnits, Is.EqualTo(1_295));
            Assert.That(
                catalog.TryGetPrice(
                    "price.store.beer",
                    out EconomyPriceDefinition beer),
                Is.True);
            Assert.That(beer.BaseMinorUnits, Is.EqualTo(14_900));
            Assert.That(
                catalog.TryGetPrice(
                    "price.store.expanded-shop-can-opener",
                    out EconomyPriceDefinition canOpener),
                Is.True);
            Assert.That(canOpener.BaseMinorUnits, Is.EqualTo(7_900));
            Assert.That(catalog.Scopes[0].InflationBasisPoints, Is.EqualTo(540));
            Assert.That(
                catalog.Scopes[0].RestockDayOfWeek,
                Is.EqualTo(System.DayOfWeek.Thursday));
        }

        [Test]
        public void ThursdayCycle_AppliesDonorAdditiveInflationExactlyOnce()
        {
            using EconomyFixture fixture = CreateFixture();

            Assert.That(
                fixture.Runtime.TryQuote(
                    "price.store.juice",
                    1,
                    out EconomyPriceQuote initial,
                    out EconomyTransactionFailureReason initialFailure),
                Is.True,
                initialFailure.ToString());
            Assert.That(initial.UnitPriceMinorUnits, Is.EqualTo(1_295));

            fixture.Clock.Advance(14_400d);

            Assert.That(
                fixture.Runtime.TryQuote(
                    "price.store.juice",
                    1,
                    out EconomyPriceQuote inflated,
                    out EconomyTransactionFailureReason inflatedFailure),
                Is.True,
                inflatedFailure.ToString());
            Assert.That(inflated.AppliedInflationCycles, Is.EqualTo(1));
            Assert.That(inflated.UnitPriceMinorUnits, Is.EqualTo(1_365));
        }

        [Test]
        public void Purchase_IsAtomicIdempotentAndRejectsConflictingReplay()
        {
            using EconomyFixture fixture = CreateFixture();

            Assert.That(
                fixture.Runtime.TryPurchase(
                    "transaction.tests.purchase-1",
                    "price.store.beer",
                    1,
                    "store.teimo.checkout",
                    out EconomyTransactionReceipt purchase),
                Is.True);
            Assert.That(purchase.BalanceAfterMinorUnits, Is.EqualTo(285_100));
            Assert.That(
                fixture.Runtime.TryPurchase(
                    "transaction.tests.purchase-1",
                    "price.store.beer",
                    1,
                    "store.teimo.checkout",
                    out EconomyTransactionReceipt replay),
                Is.True);
            Assert.That(replay.WasIdempotentReplay, Is.True);
            Assert.That(fixture.Runtime.Snapshot.LedgerCount, Is.EqualTo(1));

            Assert.That(
                fixture.Runtime.TryPurchase(
                    "transaction.tests.purchase-1",
                    "price.store.juice",
                    1,
                    "store.teimo.checkout",
                    out EconomyTransactionReceipt conflict),
                Is.False);
            Assert.That(
                conflict.FailureReason,
                Is.EqualTo(
                    EconomyTransactionFailureReason.DuplicateTransactionConflict));
            Assert.That(fixture.Runtime.Snapshot.BalanceMinorUnits, Is.EqualTo(285_100));

            Assert.That(
                fixture.Runtime.TryPurchase(
                    "transaction.tests.too-expensive",
                    "price.store.beer",
                    20,
                    "store.teimo.checkout",
                    out EconomyTransactionReceipt insufficient),
                Is.False);
            Assert.That(
                insufficient.FailureReason,
                Is.EqualTo(EconomyTransactionFailureReason.InsufficientFunds));
            Assert.That(fixture.Runtime.Snapshot.LedgerCount, Is.EqualTo(1));
        }

        [Test]
        public void Refund_IsBoundedByOriginalDebitAndStateRoundTrips()
        {
            using EconomyFixture fixture = CreateFixture();
            Assert.That(
                fixture.Runtime.TryPurchase(
                    "transaction.tests.purchase-2",
                    "price.store.beer",
                    1,
                    "store.teimo.checkout",
                    out _),
                Is.True);
            Assert.That(
                fixture.Runtime.TryRefund(
                    "transaction.tests.refund-1",
                    "transaction.tests.purchase-2",
                    5_000,
                    "store.teimo.checkout",
                    out _),
                Is.True);
            Assert.That(
                fixture.Runtime.TryRefund(
                    "transaction.tests.refund-2",
                    "transaction.tests.purchase-2",
                    10_000,
                    "store.teimo.checkout",
                    out EconomyTransactionReceipt rejected),
                Is.False);
            Assert.That(
                rejected.FailureReason,
                Is.EqualTo(EconomyTransactionFailureReason.RefundNotAllowed));

            EconomyStateDto captured = fixture.Runtime.CaptureDto();
            using EconomyFixture restored = CreateFixture();
            Assert.That(
                restored.Runtime.TryRestoreDto(captured, out string failure),
                Is.True,
                failure);
            Assert.That(
                restored.Runtime.Snapshot.BalanceMinorUnits,
                Is.EqualTo(fixture.Runtime.Snapshot.BalanceMinorUnits));
            Assert.That(restored.Runtime.Snapshot.LedgerCount, Is.EqualTo(2));
        }

        [Test]
        public void InsufficientFunds_PublishesPresentationFeedbackWithoutMutation()
        {
            using EconomyFixture fixture = CreateFixture();
            int rejectionCount = 0;
            EconomyTransactionRejected observed = default;
            fixture.Runtime.TransactionRejected += rejected =>
            {
                rejectionCount++;
                observed = rejected;
            };
            var request = new EconomyTransactionRequest(
                "transaction.tests.player-reaction",
                EconomyTransactionKind.Purchase,
                EconomyTransactionDirection.Debit,
                fixture.Runtime.Snapshot.BalanceMinorUnits + 1,
                "store.teimo.checkout");

            Assert.That(
                fixture.Runtime.TryCommit(in request, out _),
                Is.False);
            Assert.That(rejectionCount, Is.EqualTo(1));
            Assert.That(
                observed.Receipt.FailureReason,
                Is.EqualTo(EconomyTransactionFailureReason.InsufficientFunds));
            Assert.That(
                observed.Request.TransactionId,
                Is.EqualTo(request.TransactionId));
            Assert.That(
                fixture.Runtime.Snapshot.BalanceMinorUnits,
                Is.EqualTo(300_000));
            Assert.That(fixture.Runtime.Snapshot.LedgerCount, Is.Zero);
        }

        private static EconomyPriceCatalog RequireCatalog() =>
            AssetDatabase.LoadAssetAtPath<EconomyPriceCatalog>(CatalogPath) ??
            throw new AssertionException(
                "The generated Phase 1 economy catalog is missing.");

        private static EconomyFixture CreateFixture()
        {
            var clock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            var owner = new GameObject("EconomyRuntimeTests");
            EconomyRuntime runtime = owner.AddComponent<EconomyRuntime>();
            runtime.Initialize(RequireCatalog(), clock);
            return new EconomyFixture(owner, clock, runtime);
        }

        private sealed class EconomyFixture : System.IDisposable
        {
            private readonly GameObject owner;

            public EconomyFixture(
                GameObject owner,
                GameTimeService clock,
                EconomyRuntime runtime)
            {
                this.owner = owner;
                Clock = clock;
                Runtime = runtime;
            }

            public GameTimeService Clock { get; }
            public EconomyRuntime Runtime { get; }

            public void Dispose() => Object.DestroyImmediate(owner);
        }
    }
}
