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
    public sealed class SatsumaFrontLatchSaveCompatibilityTests
    {
        [TestCase("sub-frame", 26)]
        [TestCase("steering-rack", 24)]
        [TestCase("steering-column", 10)]
        [TestCase("strut-fl", 3)]
        [TestCase("strut-fr", 3)]
        [TestCase("discbrake-fl", 2)]
        [TestCase("discbrake-fr", 2)]
        [TestCase("halfshaft-fl", 2)]
        [TestCase("halfshaft-fr", 2)]
        public void HigherOnThresholdPreservesSavedLatchHistoryAndRejectsImpossibleStates(
            string slug, int expectedOn)
        {
            using var fixture = new Fixture();
            MountPointAuthoring mount = fixture.Mount(slug);
            Assert.That(mount.Definition.FastenerGroup.BoltedOnThreshold,
                Is.EqualTo(expectedOn));
            Assert.That(mount.Definition.FastenerGroup.BoltedOffThreshold, Is.Zero);

            // Old ON=1 saves can legitimately contain B=true,T=1. ON is not
            // a minimum holding threshold: saved history remains valid until OFF.
            VehicleAssemblySaveData oldLatched = fixture.InstalledSnapshot(slug, 1, true);
            fixture.AssertRestore(oldLatched, slug, 1, true);
            FastenerDefinition first = mount.Definition.Fasteners.First(value =>
                value.DefinitionId == mount.Definition.FastenerGroup.FastenerDefinitionIds[0]);
            ToolDefinition tool = fixture.Assembly.Tools.First(value =>
                value != null && value.Size == first.Size);
            AssertSuccess(fixture.Assembly.TryOperateFastener(
                mount.MountId, first.DefinitionId, tool, tighten: false));
            fixture.AssertLatch(slug, 0, false);

            // The same sum below ON can also be an as-yet-unlatched new group.
            VehicleAssemblySaveData notYetLatched =
                fixture.InstalledSnapshot(slug, expectedOn - 1, false);
            fixture.AssertRestore(notYetLatched, slug, expectedOn - 1, false);

            fixture.AssertRejectedWithoutMutation(
                fixture.InstalledSnapshot(slug, 0, true));
            fixture.AssertRejectedWithoutMutation(
                fixture.InstalledSnapshot(slug, expectedOn, false));
            fixture.AssertRestore(
                fixture.InstalledSnapshot(slug, expectedOn, true),
                slug, expectedOn, true);
        }

        [TestCase(1, 0, true)]
        [TestCase(0, 8, false)]
        public void RetiredColumnRemapKeepsValidLatchButDropsLatchSupportedOnlyByRetiredBolt(
            int physicalTightness, int retiredTightness, bool expectedBolted)
        {
            using var fixture = new Fixture();
            const string slug = "steering-column";
            const string mountId = "mount.satsuma.steering-column";
            VehicleAssemblySaveData source =
                fixture.InstalledSnapshot(slug, physicalTightness, true);
            FastenerSaveDto first = source.fasteners.Single(value =>
                value.fastenerDefinitionId == "fastener.satsuma.steering-column.boltpm-1");
            FastenerSaveDto second = source.fasteners.Single(value =>
                value.fastenerDefinitionId == "fastener.satsuma.steering-column.boltpm-2");
            first.stage = 0;
            second.stage = retiredTightness;
            source.fasteners = source.fasteners.Where(value =>
                    value.fastenerDefinitionId != "fastener.satsuma.steering-wheel.boltpm-1")
                .Concat(new[]
                {
                    new FastenerSaveDto
                    {
                        mountId = mountId,
                        fastenerDefinitionId = "fastener.satsuma.steering-column.boltpm-3",
                        inserted = true,
                        seated = true,
                        stage = physicalTightness,
                    },
                }).ToArray();

            fixture.AssertRestore(source, slug, physicalTightness, expectedBolted);
            VehicleAssemblySaveData restored = fixture.Assembly.CaptureSaveData();
            Assert.That(restored.fasteners.Any(value =>
                value.fastenerDefinitionId == "fastener.satsuma.steering-column.boltpm-3"),
                Is.False);
            Assert.That(restored.fasteners.Single(value =>
                value.fastenerDefinitionId == "fastener.satsuma.steering-column.boltpm-2").stage,
                Is.EqualTo(physicalTightness));
            Assert.That(restored.fasteners.Count(value =>
                value.fastenerDefinitionId == "fastener.satsuma.steering-wheel.boltpm-1"),
                Is.EqualTo(1));
        }

        [Test]
        public void SchemaOneWithoutLatchHistoryReconstructsUsingCurrentThreshold()
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source =
                fixture.InstalledSnapshot("steering-column", 1, true);
            source.schemaVersion = VehicleAssemblySaveData.LegacySchemaVersion;
            source.fastenerGroups = Array.Empty<FastenerGroupSaveDto>();

            // Schema 1 never recorded B. Preserve its established deterministic
            // reconstruction policy instead of inventing absent latch history.
            fixture.AssertRestore(source, "steering-column", 1, false);
            Assert.That(fixture.Assembly.CaptureSaveData().schemaVersion,
                Is.EqualTo(VehicleAssemblySaveData.CurrentSchemaVersion));
        }

        private static void AssertSuccess(AssemblyOperationResult result) =>
            Assert.That(result.Succeeded, Is.True, result.Message);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject instance;
            private readonly VehicleAssemblySaveData emptySnapshot;
            private readonly PartInstance[] ownedParts;

            public Fixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                if (prefab == null)
                {
                    Assert.Ignore("Private donor-derived Satsuma baseline has not been built.");
                }
                instance = Object.Instantiate(prefab);
                Assembly = instance.GetComponent<VehicleAssemblyController>();
                ownedParts = Assembly.Parts.ToArray();
                Assembly.Initialize();
                emptySnapshot = Assembly.CaptureSaveData();
            }

            public VehicleAssemblyController Assembly { get; }

            public MountPointAuthoring Mount(string slug) => Assembly.MountPoints.Single(
                value => value.MountId == "mount.satsuma." + slug);

            public VehicleAssemblySaveData InstalledSnapshot(string slug, int tightness, bool bolted)
            {
                var data = JsonUtility.FromJson<VehicleAssemblySaveData>(
                    JsonUtility.ToJson(emptySnapshot));
                MountPointAuthoring mount = Mount(slug);
                InstallHistoricalOccupancy(data, mount);

                // A save is existing assembly state, not a new install request.
                // Keep real owner occupancy, but do not retrofit newer click-time
                // prerequisites onto parts installed by an older game revision.
                foreach (FastenerSaveDto saved in data.fasteners.Where(value =>
                             value.mountId == mount.MountId))
                {
                    saved.inserted = true;
                    saved.seated = true;
                    saved.stage = 0;
                }
                int remaining = tightness;
                foreach (string id in mount.Definition.FastenerGroup.FastenerDefinitionIds)
                {
                    FastenerDefinition definition = mount.Definition.Fasteners.Single(value =>
                        value.DefinitionId == id);
                    FastenerSaveDto saved = data.fasteners.Single(value =>
                        value.mountId == mount.MountId && value.fastenerDefinitionId == id);
                    saved.stage = Mathf.Min(remaining, definition.MaximumStage);
                    remaining -= saved.stage;
                }
                Assert.That(remaining, Is.Zero, mount.MountId + " tightness exceeds its group.");
                data.fastenerGroups.Single(value => value.mountId == mount.MountId).isBolted = bolted;
                return data;
            }

            private void InstallHistoricalOccupancy(
                VehicleAssemblySaveData data, MountPointAuthoring mount)
            {
                MountSaveDto mountDto = data.mounts.Single(value => value.mountId == mount.MountId);
                if (!string.IsNullOrEmpty(mountDto.installedPartStableEntityId))
                {
                    return;
                }
                AssemblyOwnedMountAuthoring owned = mount.GetComponent<AssemblyOwnedMountAuthoring>();
                if (owned != null && owned.RequireInstalledOwner && !owned.OwnerPart.IsAssemblyRoot)
                {
                    string ownerId = owned.OwnerPart.Definition.DefinitionId;
                    InstallHistoricalOccupancy(data, Assembly.MountPoints.Single(value =>
                        value.Definition.AcceptsPart(ownerId)));
                }
                PartSaveDto part = data.parts.First(value =>
                    value.lifecycleState == PartLifecycleState.Loose &&
                    mount.Definition.AcceptsPart(value.partDefinitionId));
                part.lifecycleState = PartLifecycleState.Installed;
                part.installedMountId = mount.MountId;
                part.worldPosition = mount.Pose.position;
                part.worldRotation = mount.Pose.rotation;
                mountDto.installedPartStableEntityId = part.stableEntityId;
            }

            public void AssertRestore(
                VehicleAssemblySaveData source, string slug, int tightness, bool bolted)
            {
                string originalJson = JsonUtility.ToJson(source);
                AssertSuccess(Assembly.ValidateSaveDataForRestore(source));
                AssertSuccess(Assembly.RestoreSaveData(source));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalJson),
                    "Validation and migration must not mutate the caller's historical DTO.");
                AssertLatch(slug, tightness, bolted);
                VehicleAssemblySaveData roundTrip = Assembly.CaptureSaveData();
                Assert.That(roundTrip.fastenerGroups.Single(value =>
                    value.mountId == Mount(slug).MountId).isBolted, Is.EqualTo(bolted));
                AssertSuccess(Assembly.RestoreSaveData(roundTrip));
                AssertLatch(slug, tightness, bolted);
            }

            public void AssertLatch(string slug, int tightness, bool bolted)
            {
                MountPointRuntime runtime = Assembly.ResolveMount(Mount(slug));
                Assert.That(runtime.IsOccupied, Is.True, slug);
                Assert.That(runtime.FastenerGroup.Tightness, Is.EqualTo(tightness), slug);
                Assert.That(runtime.FastenerGroup.IsBolted, Is.EqualTo(bolted), slug);
            }

            public void AssertRejectedWithoutMutation(VehicleAssemblySaveData source)
            {
                string originalJson = JsonUtility.ToJson(source);
                string before = JsonUtility.ToJson(Assembly.CaptureSaveData());
                AssemblyOperationResult validation = Assembly.ValidateSaveDataForRestore(source);
                Assert.That(validation.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
                AssemblyOperationResult restore = Assembly.RestoreSaveData(source);
                Assert.That(restore.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
                Assert.That(JsonUtility.ToJson(Assembly.CaptureSaveData()), Is.EqualTo(before));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalJson));
            }

            public void Dispose()
            {
                // Restore may place loose parts under a separate root. Own only
                // this fixture's original parts; never scan/delete foreign objects.
                foreach (PartInstance part in ownedParts)
                {
                    if (part != null && !part.transform.IsChildOf(instance.transform))
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }
                Object.DestroyImmediate(instance);
            }
        }
    }
}
