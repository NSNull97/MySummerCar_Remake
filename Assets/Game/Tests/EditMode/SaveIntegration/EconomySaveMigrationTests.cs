using System;
using System.Linq;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Weather.Production;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class EconomySaveMigrationTests
    {
        [Test]
        public void VersionThirteen_AddsRequiredFreshEconomyWithoutMutatingSource()
        {
            EconomyPriceCatalog catalog = CreateCatalog();
            try
            {
                var clock = new GameTimeService(
                    GameTimeConfig.RemakeDesignTargetDefaults);
                EconomyStateDto fresh = EconomyRuntime.CreateFreshState(
                    catalog,
                    clock.Snapshot);
                var source = new SaveDocument
                {
                    Header = new SaveHeader { DocumentVersion = 13 },
                    Domains = Array.Empty<SaveDomainEnvelope>(),
                };

                SaveDocument migrated = new Milestone12AEconomySaveMigration(
                        fresh,
                        domainRequired: true)
                    .Migrate(source, new UnresolvedContentReport());

                Assert.That(source.Header.DocumentVersion, Is.EqualTo(13));
                Assert.That(source.Domains, Is.Empty);
                Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(14));
                SaveDomainEnvelope envelope = migrated.Domains.Single();
                Assert.That(
                    envelope.DomainId,
                    Is.EqualTo(EconomySaveParticipant.DomainId));
                Assert.That(envelope.Required, Is.True);
                EconomyStateDto restored =
                    SaveParticipantJson.Deserialize<EconomyStateDto>(
                        envelope.PayloadJson);
                Assert.That(
                    restored.TryValidate(catalog, out string failure),
                    Is.True,
                    failure);
                Assert.That(restored.balanceMinorUnits, Is.EqualTo(300_000));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Participant_UsesStagedCoreTimeBeforeLiveClockRestore()
        {
            EconomyPriceCatalog catalog = CreateCatalog();
            var targetClock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            var futureClock = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            futureClock.Advance(
                GameTimeConfig.RemakeDesignTargetDefaults
                    .DayLengthSimulationSeconds * 3d);
            var targetObject = new GameObject("EconomyStagedClockTarget");
            var sourceObject = new GameObject("EconomyStagedClockSource");
            var environmentObject =
                new GameObject("EconomyStagedClockEnvironmentFixture");
            try
            {
                EconomyRuntime target =
                    targetObject.AddComponent<EconomyRuntime>();
                target.Initialize(catalog, targetClock);
                EconomyRuntime source =
                    sourceObject.AddComponent<EconomyRuntime>();
                source.Initialize(catalog, futureClock);
                EconomyStateDto future = source.CaptureDto();
                EconomyStateDto checkpoint = target.CaptureDto();

                ProductionEnvironmentController environment =
                    environmentObject.AddComponent<ProductionEnvironmentController>();
                var bridge = new ProductionEnvironmentRestoreBridge(environment);
                bridge.PrepareTimeForApply(
                    futureClock.CaptureDto(),
                    targetClock.CaptureDto());
                var participant = new EconomySaveParticipant(target, bridge);

                Assert.DoesNotThrow(() => participant.ApplyPreparedRestore(
                    future,
                    new SaveRestoreContext(
                        new UnresolvedContentReport(),
                        new DeferredStableEntityStore())));
                Assert.That(
                    target.CaptureDto().lastProcessedDayIndex,
                    Is.EqualTo(futureClock.Snapshot.DayIndex));
                Assert.That(targetClock.Snapshot.DayIndex, Is.Zero);

                Assert.DoesNotThrow(() => participant.Rollback(checkpoint));
                Assert.That(
                    target.CaptureDto().lastProcessedDayIndex,
                    Is.EqualTo(targetClock.Snapshot.DayIndex));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(environmentObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private static EconomyPriceCatalog CreateCatalog()
        {
            var scope = new EconomyPriceScopeDefinition();
            scope.ConfigureForAuthoring(
                "scope.tests.store",
                EconomyPricePolicy.WeeklyAdditiveInflation,
                540,
                DayOfWeek.Thursday);
            var price = new EconomyPriceDefinition();
            price.ConfigureForAuthoring(
                "price.tests.item",
                scope.ScopeId,
                "TestItem",
                100);
            var catalog = ScriptableObject.CreateInstance<EconomyPriceCatalog>();
            catalog.ConfigureForAuthoring(
                "catalog.tests.economy",
                new string('a', 64),
                new string('b', 64),
                300_000,
                new[] { scope },
                new[] { price });
            return catalog;
        }
    }
}
