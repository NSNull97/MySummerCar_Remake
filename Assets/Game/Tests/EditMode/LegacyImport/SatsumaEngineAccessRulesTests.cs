using System;
using System.Collections.Generic;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineAccessRulesTests
    {
        private GameObject instance;
        private VehicleAssemblyController assembly;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null, "The private Satsuma baseline is required.");
            instance = Object.Instantiate(prefab);
            assembly = instance.GetComponent<VehicleAssemblyController>();
            assembly.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("oilpan")]
        [TestCase("main-bearing1")]
        [TestCase("main-bearing2")]
        [TestCase("main-bearing3")]
        [TestCase("timing-cover")]
        public void CrankshaftCannotBeInsertedThroughInstalledObstructions(string blocker)
        {
            Install(blocker);
            PartInstance crank = Part("crankshaft");
            MountPointAuthoring mount = Mount("crankshaft");
            PlaceAtMount(crank, mount);

            AssemblyOperationResult result = assembly.TryInstall(crank, mount);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));
            Assert.That(crank.IsInstalled, Is.False);
            Assert.That(Part(blocker).IsInstalled, Is.True);

            Remove(blocker);
            Install("crankshaft");
            Assert.That(Part("engine-block").IsInstalled, Is.False,
                "The engine remains buildable on its loose block.");
        }

        [TestCase("main-bearing1")]
        [TestCase("main-bearing2")]
        [TestCase("main-bearing3")]
        [TestCase("timing-cover")]
        public void CrankshaftRemovalWaitsForInstalledBearingCapsAndTimingCover(string blocker)
        {
            Install("crankshaft");
            Install(blocker);
            AssertRemovalBlocked("crankshaft");
            Assert.That(assembly.ResolveMount(Mount(blocker)).FastenerGroup.IsBolted,
                Is.False, "Presence, not fastening, closes this access path.");
            Remove(blocker);
            Remove("crankshaft");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void BearingCapsCanBePlacedWithoutCrankButNotThroughOilpan(int number)
        {
            string bearing = "main-bearing" + number;
            Install("oilpan");
            PartInstance part = Part(bearing);
            PlaceAtMount(part, Mount(bearing));
            Assert.That(assembly.TryInstall(part, Mount(bearing)).Succeeded, Is.False);
            Assert.That(part.IsInstalled, Is.False);
            Remove("oilpan");

            Install(bearing);
            Assert.That(Part("crankshaft").IsInstalled, Is.False,
                "Donor cap Assembly has no crankshaft-presence prerequisite.");
            Install("oilpan");
            AssertRemovalBlocked(bearing);
            Remove("oilpan");
            Remove(bearing);
        }

        [Test]
        public void OilpanDoesNotBecomeAnInventedCrankshaftRemovalGate()
        {
            Install("crankshaft");
            Install("oilpan");
            Remove("crankshaft");
            Assert.That(Part("oilpan").IsInstalled, Is.True,
                "Donor crank Removal does not read Oilpan.Installed.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void HeadClosesPistonAccessWhileExistingCrankRequirementRemains(int number)
        {
            string piston = "piston" + number;
            PartInstance part = Part(piston);
            PlaceAtMount(part, Mount(piston));
            Assert.That(assembly.EvaluateInstall(part, Mount(piston)).FailureReason,
                Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));

            Install("crankshaft");
            Install("cylinder-head");
            PlaceAtMount(part, Mount(piston));
            Assert.That(assembly.TryInstall(part, Mount(piston)).Succeeded, Is.False);
            Assert.That(part.IsInstalled, Is.False);
            Remove("cylinder-head");
            Install(piston);
            AssertRemovalBlocked("crankshaft");

            Install("cylinder-head");
            AssertRemovalBlocked(piston);
            Remove("cylinder-head");
            Remove(piston);
            Remove("crankshaft");
        }

        [Test]
        public void ExistingAssembledEngineRestoresOccupancyFasteningAndNewAccessGate()
        {
            Install("crankshaft");
            Install("piston1");
            MountPointRuntime pistonMount = assembly.ResolveMount(Mount("piston1"));
            FastenerInstance bolt = pistonMount.Fasteners[0];
            ToolDefinition tool = assembly.Tools.First(value =>
                bolt.Definition.ToolRule.Matches(value));
            Assert.That(assembly.TryOperateFastener(pistonMount.MountId,
                bolt.Definition.DefinitionId, tool, true).Succeeded, Is.True);
            Install("cylinder-head");
            string savedJson = JsonUtility.ToJson(assembly.CaptureSaveData());

            Assert.That(assembly.TryBreakInstalledPart(Part("cylinder-head")).Succeeded, Is.True);
            Assert.That(assembly.TryBreakInstalledPart(Part("piston1")).Succeeded, Is.True);
            VehicleAssemblySaveData saved = JsonUtility.FromJson<VehicleAssemblySaveData>(savedJson);
            string inputBefore = JsonUtility.ToJson(saved);
            Assert.That(assembly.RestoreSaveData(saved).Succeeded, Is.True);
            Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(inputBefore),
                "Restoring must not rewrite the caller's DTO.");

            Assert.That(Part("crankshaft").IsInstalled, Is.True);
            Assert.That(Part("piston1").IsInstalled, Is.True);
            Assert.That(Part("cylinder-head").IsInstalled, Is.True);
            Assert.That(Part("engine-block").IsInstalled, Is.False);
            Assert.That(assembly.ResolveMount(Mount("piston1")).Fasteners[0].Stage,
                Is.EqualTo(1));
            Assert.That(assembly.ResolveMount(Mount("piston1")).FastenerGroup.IsBolted, Is.True);
            AssertRemovalBlocked("crankshaft");
            VehicleAssemblySaveData restored = assembly.CaptureSaveData();
            Assert.That(restored.schemaVersion, Is.EqualTo(saved.schemaVersion));
            Assert.That(restored.parts.Select(value => (value.stableEntityId,
                    value.partDefinitionId, value.lifecycleState, value.installedMountId)),
                Is.EqualTo(saved.parts.Select(value => (value.stableEntityId,
                    value.partDefinitionId, value.lifecycleState, value.installedMountId))));
            Assert.That(restored.mounts.Select(value => JsonUtility.ToJson(value)),
                Is.EqualTo(saved.mounts.Select(value => JsonUtility.ToJson(value))));
            Assert.That(restored.fasteners.Select(value => JsonUtility.ToJson(value)),
                Is.EqualTo(saved.fasteners.Select(value => JsonUtility.ToJson(value))));
            Assert.That(restored.fastenerGroups.Select(value => JsonUtility.ToJson(value)),
                Is.EqualTo(saved.fastenerGroups.Select(value => JsonUtility.ToJson(value))));
        }

        [Test]
        public void EngineAccessRefreshIsIdempotentAndPreservesAllOtherMountData()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController controller = contents.GetComponent<VehicleAssemblyController>();
                Dictionary<string, string> before = controller.MountPoints.ToDictionary(
                    value => value.MountId,
                    value => EditorJsonUtility.ToJson(value.Definition),
                    StringComparer.Ordinal);
                string controllerBefore = EditorJsonUtility.ToJson(controller);
                MountPointDefinition[] changed =
                    Phase1SatsumaEngineAssemblyRules.RefreshEngineAccessRules(contents);
                Assert.That(changed, Is.Empty, "Run the E1 scoped refresh first.");
                foreach (MountPointAuthoring mount in controller.MountPoints)
                {
                    Assert.That(EditorJsonUtility.ToJson(mount.Definition),
                        Is.EqualTo(before[mount.MountId]), mount.MountId);
                }

                Assert.That(EditorJsonUtility.ToJson(controller), Is.EqualTo(controllerBefore),
                    "The definition-only refresh must preserve controller references and dependencies.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private void AssertRemovalBlocked(string slug)
        {
            AssemblyOperationResult result = assembly.EvaluateRemoval(Part(slug));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.RemovalBlocked),
                result.Message);
        }

        private void Install(string slug)
        {
            PartInstance part = Part(slug);
            MountPointAuthoring mount = Mount(slug);
            PlaceAtMount(part, mount);
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, slug + ": " + result.Message);
            Assert.That(part.IsInstalled, Is.True, slug);
        }

        private void Remove(string slug)
        {
            AssemblyOperationResult result = assembly.TryRemove(Part(slug));
            Assert.That(result.Succeeded, Is.True, slug + ": " + result.Message);
        }

        private PartInstance Part(string slug) => assembly.Parts.Single(value =>
            value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);

        private MountPointAuthoring Mount(string slug) => assembly.MountPoints.Single(value =>
            value.MountId == "mount.satsuma.engine-block." + slug);

        private static void PlaceAtMount(PartInstance part, MountPointAuthoring mount)
        {
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Physics.SyncTransforms();
        }
    }
}
