using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaReviewedGraphShapeTests
    {
        [Test]
        public void MountGuardAcceptsExactPreviousAndAdditiveIdentities()
        {
            string[] current = Assembly().MountPoints.Select(mount => mount.MountId).ToArray();
            string[] previous = current.Where(id => !SatsumaCanonicalNightTestShape.IsAddedMount(id)).ToArray();
            Assert.That(previous, Has.Length.EqualTo(117));
            Assert.That(current, Has.Length.EqualTo(124));
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(previous), Is.True);
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(current), Is.True);
        }

        [TestCase("substitute-base")]
        [TestCase("substitute-added")]
        [TestCase("partial")]
        [TestCase("duplicate")]
        public void MountGuardRejectsSameCountDriftOrPartialAddition(string drift)
        {
            string[] ids = Assembly().MountPoints.Select(mount => mount.MountId).ToArray();
            if (drift == "substitute-base") ids[System.Array.IndexOf(ids, "mount.satsuma.battery")] = "mount.satsuma.unreviewed";
            if (drift == "substitute-added") ids[System.Array.IndexOf(ids, SatsumaCanonicalNightTestShape.AddedMountIds[0])] = "mount.satsuma.unreviewed";
            if (drift == "partial") ids = ids.Where(id => id != SatsumaCanonicalNightTestShape.AddedMountIds[0]).ToArray();
            if (drift == "duplicate") ids[0] = ids[1];
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(ids), Is.False);
        }

        [Test]
        public void FastenerGuardAcceptsOnlyCompleteReviewedPacketsBesidePreviousRoster()
        {
            var targets = Assembly().GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            SatsumaCanonicalNightTestShape.AssertCanonicalTargets(targets);
            string[] current = targets.Select(target => target.FastenerDefinitionId).ToArray();
            var intermediateTargets = targets.Where(target => !SatsumaCanonicalNightTestShape.IsHeadlightFastener(target)).ToArray();
            string[] retired = SatsumaRockerShaftFastenerMigration.RetiredIds;
            SatsumaCanonicalNightTestShape.AssertPrevious298Keys(intermediateTargets
                .Select(target => target.MountId + "/" + target.FastenerDefinitionId)
                .Concat(retired.Select(id => SatsumaRockerShaftFastenerMigration.MountId + "/" + id)));
            string[] intermediate = intermediateTargets.Select(target => target.FastenerDefinitionId).ToArray();
            string[] previous = targets.Where(target => !SatsumaCanonicalNightTestShape.IsAddedFastener(target))
                .Select(target => target.FastenerDefinitionId).ToArray();
            Assert.That(previous, Has.Length.EqualTo(265));
            foreach (string[] cohort in new[] { previous, intermediate, current })
            {
                Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(cohort, 265), Is.True);
                Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(cohort.Concat(retired), 273), Is.True);
            }
        }

        [TestCase("missing-stock")]
        [TestCase("missing-thread")]
        [TestCase("missing-headlight")]
        [TestCase("substitute-added")]
        [TestCase("substitute-headlight")]
        [TestCase("duplicate")]
        public void FastenerGuardRejectsPartialOrSubstitutedAdditivePackets(string drift)
        {
            string[] ids = Assembly().GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Select(target => target.FastenerDefinitionId).ToArray();
            const string stock = "fastener.satsuma.fuel-tank.boltpm-7";
            const string thread = "fastener.satsuma.cylinder-head-spark-plug-4.thread";
            const string headlight = "fastener.satsuma.headlight-right.boltpm-2";
            if (drift == "missing-stock") ids = ids.Where(id => id != stock).ToArray();
            if (drift == "missing-thread") ids = ids.Where(id => id != thread).ToArray();
            if (drift == "missing-headlight") ids = ids.Where(id => id != headlight).ToArray();
            if (drift == "substitute-added") ids[System.Array.IndexOf(ids, stock)] = "fastener.satsuma.unreviewed";
            if (drift == "substitute-headlight") ids[System.Array.IndexOf(ids, headlight)] = "fastener.satsuma.unreviewed";
            if (drift == "duplicate") ids[0] = ids[1];
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(ids, 265), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HeadlightPacketRequiresCompleteStock21WithOrWithoutPlugThreads(bool includePlugs)
        {
            var targets = Assembly().GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            SatsumaCanonicalNightTestShape.AssertCanonicalTargets(targets);
            var baseIds = targets.Where(target => !SatsumaCanonicalNightTestShape.IsAddedFastener(target))
                .Select(target => target.FastenerDefinitionId);
            var headlights = targets.Where(SatsumaCanonicalNightTestShape.IsHeadlightFastener)
                .Select(target => target.FastenerDefinitionId);
            var plugs = includePlugs ? Enumerable.Range(1, 4).Select(index =>
                "fastener.satsuma.cylinder-head-spark-plug-" + index + ".thread") : Enumerable.Empty<string>();
            string[] missingStock = baseIds.Concat(headlights).Concat(plugs).ToArray();
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(missingStock, 265), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoneStock21AndStock21PlusHeadlightsAreSupportedBodyCohorts(bool includePlugs)
        {
            var targets = Assembly().GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            SatsumaCanonicalNightTestShape.AssertCanonicalTargets(targets);
            string[] previous = targets.Where(target => !SatsumaCanonicalNightTestShape.IsAddedFastener(target))
                .Select(target => target.FastenerDefinitionId).ToArray();
            string[] stock = targets.Where(target => SatsumaCanonicalNightTestShape.IsAddedFastener(target) &&
                    !SatsumaCanonicalNightTestShape.IsHeadlightFastener(target) &&
                    !SatsumaCanonicalNightTestShape.IsAddedMount(target.MountId))
                .Select(target => target.FastenerDefinitionId).ToArray();
            string[] headlights = targets.Where(SatsumaCanonicalNightTestShape.IsHeadlightFastener)
                .Select(target => target.FastenerDefinitionId).ToArray();
            Assert.That(stock, Has.Length.EqualTo(21));
            Assert.That(headlights, Has.Length.EqualTo(4));
            var plugs = includePlugs ? Enumerable.Range(1, 4).Select(index =>
                "fastener.satsuma.cylinder-head-spark-plug-" + index + ".thread") : Enumerable.Empty<string>();
            string[] initial = previous.Concat(plugs).ToArray();
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(initial, 265), Is.True);
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(initial.Concat(stock), 265), Is.True);
            Assert.That(Phase1SatsumaReviewedGraphShape.HasReviewedFastenerRoster(initial.Concat(stock).Concat(headlights), 265), Is.True);
        }

        private static VehicleAssemblyController Assembly()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null, "The generated canonical Satsuma is required.");
            return prefab.GetComponent<VehicleAssemblyController>();
        }
    }
}
