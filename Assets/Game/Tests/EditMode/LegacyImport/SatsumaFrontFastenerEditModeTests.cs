using System;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaFrontFastenerEditModeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Legacy252FastenersPreserveExistingStagesAndAddOnlyLooseLowerBolts(bool partiallyTight)
        {
            GameObject instance = CreateFixture();
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                PrepareLeftSide(assembly, partiallyTight);
                VehicleAssemblySaveData legacy = assembly.CaptureSaveData();
                AssertCurrentShape(legacy);
                legacy.fasteners = BuildLegacy252Shape(legacy);
                Assert.That(legacy.fasteners, Has.Length.EqualTo(252));
                string originalJson = JsonUtility.ToJson(legacy);
                string[] originalStates = UnchangedFastenerStates(legacy);

                // Restoring must replace state, not accidentally retain the
                // already-materialized new bolt's current tightness.
                TurnToStage(assembly, "strut-fl", "fastener.satsuma.strut-fl.lower-1", 4);
                AssemblyOperationResult validation = assembly.ValidateSaveDataForRestore(legacy);
                Assert.That(validation.Succeeded, Is.True, validation.Message);
                AssemblyOperationResult restore = assembly.RestoreSaveData(legacy);
                Assert.That(restore.Succeeded, Is.True, restore.Message);
                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                AssertCurrentShape(migrated);
                Assert.That(UnchangedFastenerStates(migrated), Is.EquivalentTo(originalStates));
                foreach (FastenerSaveDto added in migrated.fasteners.Where(IsNewLower))
                {
                    bool installedSide = added.mountId == "mount.satsuma.strut-fl";
                    Assert.That(added.inserted, Is.EqualTo(installedSide), added.fastenerDefinitionId);
                    Assert.That(added.seated, Is.EqualTo(installedSide), added.fastenerDefinitionId);
                    Assert.That(added.stage, Is.Zero, added.fastenerDefinitionId);
                }
                Assert.That(JsonUtility.ToJson(legacy), Is.EqualTo(originalJson),
                    "Migration must not rewrite the caller's legacy payload.");
                Assert.That(migrated.fastenerGroups.Select(GroupState),
                    Is.EquivalentTo(legacy.fastenerGroups.Select(GroupState)));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void Current280FastenerSaveRoundTripRetainsAllIdsAndStages()
        {
            GameObject instance = CreateFixture();
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                PrepareLeftSide(assembly, partiallyTight: true);
                TurnToStage(assembly, "strut-fl", "fastener.satsuma.strut-fl.lower-1", 4);
                VehicleAssemblySaveData saved = assembly.CaptureSaveData();
                AssertCurrentShape(saved);
                string[] expected = FastenerStates(saved);
                TurnToStage(assembly, "strut-fl", "fastener.satsuma.strut-fl.lower-1", 1);
                AssemblyOperationResult restore = assembly.RestoreSaveData(saved);
                Assert.That(restore.Succeeded, Is.True, restore.Message);
                VehicleAssemblySaveData restored = assembly.CaptureSaveData();
                AssertCurrentShape(restored);
                Assert.That(FastenerStates(restored), Is.EquivalentTo(expected));
                Assert.That(restored.fastenerGroups.Select(GroupState),
                    Is.EquivalentTo(saved.fastenerGroups.Select(GroupState)));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CorruptLegacyFastenerShapesAreRejectedWithoutChangingLiveState(bool keep252Records)
        {
            GameObject instance = CreateFixture();
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                PrepareLeftSide(assembly, partiallyTight: true);
                VehicleAssemblySaveData current = assembly.CaptureSaveData();
                FastenerSaveDto added = current.fasteners.First(IsNewLower);
                VehicleAssemblySaveData corrupt = current;
                corrupt.fasteners = BuildLegacy252Shape(current).Where(value =>
                    value.fastenerDefinitionId !=
                        "fastener.satsuma.strut-fl.boltpm-1").ToArray();
                if (keep252Records)
                {
                    // Same count is insufficient: an old upper bolt is missing
                    // and one new lower bolt is present instead.
                    corrupt.fasteners = corrupt.fasteners.Concat(new[] { added }).ToArray();
                }
                Assert.That(corrupt.fasteners, Has.Length.EqualTo(keep252Records ? 252 : 251));
                string[] before = FastenerStates(assembly.CaptureSaveData());
                Assert.That(assembly.ValidateSaveDataForRestore(corrupt).Succeeded, Is.False);
                Assert.That(assembly.RestoreSaveData(corrupt).Succeeded, Is.False);
                Assert.That(FastenerStates(assembly.CaptureSaveData()), Is.EquivalentTo(before));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static GameObject CreateFixture()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore("Private donor-derived Satsuma baseline has not been built.");
            }
            GameObject instance = Object.Instantiate(prefab);
            instance.GetComponent<VehicleAssemblyController>().Initialize();
            return instance;
        }

        private static void PrepareLeftSide(VehicleAssemblyController assembly, bool partiallyTight)
        {
            foreach (string slug in new[] { "sub-frame", "wishbone-fl", "spindle-fl", "steering-rack" })
            {
                Install(assembly, slug, tighten: true);
            }
            Install(assembly, "strut-fl", tighten: false);
            Install(assembly, "steering-rod-fl", tighten: false);
            foreach (FastenerDefinition fastener in FindMount(assembly, "strut-fl").Definition.Fasteners
                .Where(value => value.Size == FastenerSize.Millimeter10))
            {
                TurnToStage(assembly, "strut-fl", fastener.DefinitionId, partiallyTight ? 3 : 0);
            }
            TurnToStage(assembly, "steering-rod-fl", "fastener.satsuma.steering-rod-fl.outer-joint",
                partiallyTight ? 2 : 0);
        }

        private static void Install(VehicleAssemblyController assembly, string slug, bool tighten)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);
            MountPointAuthoring mount = FindMount(assembly, slug);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            if (tighten)
            {
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
                {
                    TurnToStage(assembly, slug, fastener.DefinitionId, fastener.MaximumStage);
                }
            }
        }

        private static void TurnToStage(VehicleAssemblyController assembly, string slug, string id, int stage)
        {
            MountPointRuntime mount = assembly.ResolveMount(FindMount(assembly, slug));
            Assert.That(mount.TryGetFastener(id, out FastenerInstance fastener), Is.True, id);
            int attempts = 0;
            while (fastener.Stage != stage)
            {
                Assert.That(attempts++, Is.LessThan(fastener.Definition.MaximumStage + 2));
                ToolDefinition tool = assembly.Tools.First(value => value != null &&
                    value.Size == fastener.Definition.Size);
                AssemblyOperationResult result = assembly.TryOperateFastener(mount.MountId, id, tool,
                    tighten: fastener.Stage < stage);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }

        private static MountPointAuthoring FindMount(VehicleAssemblyController assembly, string slug) =>
            assembly.MountPoints.Single(value => value.MountId == "mount.satsuma." + slug);

        private static bool IsNewLower(FastenerSaveDto value) => new[] { "fl", "fr" }.Any(corner =>
            Enumerable.Range(1, 4).Any(index => value.mountId == "mount.satsuma.strut-" + corner &&
                value.fastenerDefinitionId == "fastener.satsuma.strut-" + corner + ".lower-" + index));

        private static FastenerSaveDto[] BuildLegacy252Shape(
            VehicleAssemblySaveData current)
        {
            var legacy = current.fasteners
                .Where(value => !IsNewLower(value) &&
                    !IsExteriorPanelFastener(value) &&
                    !IsSteeringWheelNut(value))
                .Select(CloneFastener)
                .ToList();
            FastenerSaveDto secondPhysicalColumnBolt = legacy.Single(value =>
                value.mountId == "mount.satsuma.steering-column" &&
                value.fastenerDefinitionId ==
                    "fastener.satsuma.steering-column.boltpm-2");
            legacy.Add(new FastenerSaveDto
            {
                mountId = secondPhysicalColumnBolt.mountId,
                fastenerDefinitionId =
                    "fastener.satsuma.steering-column.boltpm-3",
                inserted = secondPhysicalColumnBolt.inserted,
                seated = secondPhysicalColumnBolt.seated,
                stage = secondPhysicalColumnBolt.stage,
            });
            return legacy.ToArray();
        }

        private static bool IsExteriorPanelFastener(FastenerSaveDto value)
        {
            int count;
            switch (value.mountId)
            {
                case "mount.satsuma.bumper-front":
                case "mount.satsuma.bumper-rear":
                case "mount.satsuma.grille":
                    count = 2;
                    break;
                case "mount.satsuma.fender-left":
                case "mount.satsuma.fender-right":
                    count = 5;
                    break;
                case "mount.satsuma.hood":
                    count = 4;
                    break;
                default:
                    return false;
            }

            string prefix = "fastener." + value.mountId.Substring("mount.".Length) +
                ".boltpm-";
            return Enumerable.Range(1, count).Any(index =>
                value.fastenerDefinitionId == prefix + index);
        }

        private static bool IsSteeringWheelNut(FastenerSaveDto value) =>
            value.mountId == "mount.satsuma.steering-wheel" &&
            value.fastenerDefinitionId ==
                "fastener.satsuma.steering-wheel.boltpm-1";

        private static bool IsSteeringColumnRevisionFastener(
            FastenerSaveDto value) =>
            value.mountId == "mount.satsuma.steering-column" &&
            (value.fastenerDefinitionId ==
                 "fastener.satsuma.steering-column.boltpm-2" ||
             value.fastenerDefinitionId ==
                 "fastener.satsuma.steering-column.boltpm-3");

        private static FastenerSaveDto CloneFastener(FastenerSaveDto value) =>
            new FastenerSaveDto
            {
                mountId = value.mountId,
                fastenerDefinitionId = value.fastenerDefinitionId,
                inserted = value.inserted,
                seated = value.seated,
                stage = value.stage,
            };

        private static string[] UnchangedFastenerStates(
            VehicleAssemblySaveData data) => data.fasteners.Where(value =>
                !IsNewLower(value) &&
                !IsExteriorPanelFastener(value) &&
                !IsSteeringWheelNut(value) &&
                !IsSteeringColumnRevisionFastener(value)).Select(value =>
                    string.Join("|", value.mountId, value.fastenerDefinitionId,
                        value.inserted, value.seated, value.stage)).ToArray();

        private static string[] FastenerStates(VehicleAssemblySaveData data) =>
            data.fasteners.Select(value =>
                string.Join("|", value.mountId, value.fastenerDefinitionId, value.inserted,
                    value.seated, value.stage)).ToArray();

        private static string GroupState(FastenerGroupSaveDto value) => value.mountId + "|" + value.isBolted;

        private static void AssertCurrentShape(VehicleAssemblySaveData data)
        {
            Assert.That(data.parts, Has.Length.EqualTo(126));
            Assert.That(data.mounts, Has.Length.EqualTo(117));
            Assert.That(data.fasteners, Has.Length.EqualTo(280));
            Assert.That(data.fastenerGroups, Has.Length.EqualTo(117));
            Assert.That(data.fasteners.Count(IsNewLower), Is.EqualTo(8));
        }
    }
}
