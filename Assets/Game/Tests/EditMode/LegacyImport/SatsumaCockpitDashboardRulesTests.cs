using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaCockpitDashboardRulesTests
    {
        private const string MountId = "mount.satsuma.dashboard.meters";
        private const string OwnerPartId = "vehicle.satsuma.part.dashboard";
        private const string MeterPartId = "vehicle.satsuma.part.dashboard-meters";

        private static readonly string[] FastenerIds =
        {
            "fastener.satsuma.dashboard-meters.boltpm-1",
            "fastener.satsuma.dashboard-meters.boltpm-2",
        };

        private readonly List<Object> transientObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = transientObjects.Count - 1; index >= 0; index--)
            {
                if (transientObjects[index] != null)
                {
                    Object.DestroyImmediate(transientObjects[index]);
                }
            }

            transientObjects.Clear();
        }

        [Test]
        public void GeneratedDashboardMetersOwnExactDonorLatchAndStableFasteners()
        {
            MountPointDefinition definition = GeneratedMount().Definition;
            Assert.That(definition.DefinitionId, Is.EqualTo(MountId));
            Assert.That(definition.OwnerPartDefinitionId, Is.EqualTo(OwnerPartId));
            Assert.That(definition.AcceptedPartDefinitionIds,
                Is.EqualTo(new[] { MeterPartId }));
            Assert.That(definition.Fasteners, Has.Length.EqualTo(2));
            Assert.That(definition.Fasteners.Select(value => value.DefinitionId),
                Is.EqualTo(FastenerIds));
            Assert.That(definition.Fasteners.All(value =>
                    value.Size == FastenerSize.Millimeter6 &&
                    value.MaximumStage == 8 &&
                    value.InsertedOnInstall && value.RequiredForRemoval &&
                    value.ToolRule.ToolType == "Wrench" &&
                    value.ToolRule.FastenerSize == FastenerSize.Millimeter6),
                Is.True);

            FastenerGroupDefinition group = definition.FastenerGroup;
            Assert.That(group, Is.Not.Null);
            Assert.That(group.FastenerDefinitionIds, Is.EqualTo(FastenerIds));
            Assert.That(group.AggregateMaximumTightness, Is.EqualTo(16));
            Assert.That(group.BoltedOnThreshold, Is.EqualTo(12));
            Assert.That(group.BoltedOffThreshold, Is.Zero);
            Assert.That(group.SpeedRetentionPolicy,
                Is.EqualTo(FastenerSpeedRetentionPolicy.None));
            Assert.That(group.LooseBreakSpeedKph, Is.Zero);
            Assert.That(group.PartialCheckSpeedKph, Is.Zero);
            Assert.That(group.ChanceDivisor, Is.EqualTo(100f));
            Assert.That(group.BreakAction, Is.EqualTo(FastenerBreakAction.None));
        }

        [Test]
        public void LegacyHelperChangesOnlyOnThresholdAndIsIdempotent()
        {
            MountPointDefinition definition = CreateDashboardDefinition(onThreshold: 1);
            string before = EditorJsonUtility.ToJson(definition);
            string expected = before.Replace(
                "\"boltedOnThreshold\":1",
                "\"boltedOnThreshold\":12");
            Assert.That(expected, Is.Not.EqualTo(before),
                "The serialized sentinel must expose the latch field under test.");

            Assert.That(Phase1SatsumaCockpitRules.ApplyDashboardMeterLatch(definition),
                Is.True);
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(expected),
                "C1a must preserve every field except the reviewed ON threshold.");

            string migrated = EditorJsonUtility.ToJson(definition);
            Assert.That(Phase1SatsumaCockpitRules.ApplyDashboardMeterLatch(definition),
                Is.False);
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(migrated));
        }

        [TestCase("definition-id")]
        [TestCase("owner")]
        [TestCase("accepted-part")]
        [TestCase("fastener-count")]
        [TestCase("fastener-id")]
        [TestCase("fastener-size")]
        [TestCase("fastener-maximum")]
        [TestCase("fastener-inserted")]
        [TestCase("fastener-required")]
        [TestCase("group-ids")]
        [TestCase("group-maximum")]
        [TestCase("group-on")]
        [TestCase("group-off")]
        [TestCase("retention")]
        [TestCase("break-action")]
        public void HelperRejectsDriftBeforeMutatingDefinition(string drift)
        {
            MountPointDefinition definition = CreateDashboardDefinition(onThreshold: 1);
            ApplyDrift(definition, drift);
            string before = EditorJsonUtility.ToJson(definition);

            Assert.Throws<InvalidDataException>(() =>
                Phase1SatsumaCockpitRules.ApplyDashboardMeterLatch(definition));
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(before),
                drift + " must fail closed before any serialized field changes.");
        }

        [Test]
        public void DonorLatchUsesOnTwelveOffZeroHysteresis()
        {
            MountPointDefinition definition = CreateDashboardDefinition(onThreshold: 12);
            FastenerInstance[] fasteners = definition.Fasteners
                .Select(value => new FastenerInstance(value))
                .ToArray();
            var group = new FastenerGroupState(definition.FastenerGroup, fasteners);

            SetAggregate(fasteners, group, 11);
            Assert.That(group.IsBolted, Is.False,
                "A fresh group must remain OFF immediately below donor ON12.");
            SetAggregate(fasteners, group, 12);
            Assert.That(group.IsBolted, Is.True,
                "The latch must engage at 12, not at full tightness 16.");
            SetAggregate(fasteners, group, 11);
            Assert.That(group.IsBolted, Is.True,
                "Once latched, partial loosening above OFF0 must retain B=true.");
            SetAggregate(fasteners, group, 1);
            Assert.That(group.IsBolted, Is.True);
            SetAggregate(fasteners, group, 0);
            Assert.That(group.IsBolted, Is.False,
                "The donor latch releases only at aggregate tightness zero.");
            SetAggregate(fasteners, group, 11);
            Assert.That(group.IsBolted, Is.False,
                "After OFF0, sub-threshold tightening must not recreate the latch.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        public void HistoricalTrueLatchAboveOffThresholdSurvivesRestore(int tightness)
        {
            using var fixture = new SaveFixture();
            fixture.AssertRestore(
                fixture.InstalledSnapshot(tightness, bolted: true),
                tightness,
                bolted: true);
        }

        [Test]
        public void HistoricalTrueLatchAtOffThresholdIsRejectedWithoutMutation()
        {
            using var fixture = new SaveFixture();
            fixture.AssertRejectedWithoutMutation(
                fixture.InstalledSnapshot(tightness: 0, bolted: true));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        public void FreshFalseLatchBelowOnThresholdSurvivesRoundTrip(int tightness)
        {
            using var fixture = new SaveFixture();
            fixture.AssertRestore(
                fixture.InstalledSnapshot(tightness, bolted: false),
                tightness,
                bolted: false);
        }

        private MountPointDefinition CreateDashboardDefinition(int onThreshold)
        {
            FastenerDefinition first = CreateFastener(FastenerIds[0],
                FastenerSize.Millimeter6, 8, inserted: true, required: true,
                direction: FastenerDirection.ClockwiseToTighten,
                toolType: "Wrench");
            FastenerDefinition second = CreateFastener(FastenerIds[1],
                FastenerSize.Millimeter6, 8, inserted: true, required: true,
                direction: FastenerDirection.CounterClockwiseToTighten,
                toolType: "SentinelWrench");
            MountPointDefinition definition = Track(
                ScriptableObject.CreateInstance<MountPointDefinition>());
            definition.Configure(
                MountId,
                "Dashboard meters sentinel",
                "satsuma.socket.dashboard.meters.sentinel",
                OwnerPartId,
                new[] { MeterPartId },
                new MountConstraint(0.314f, 27f, 0.99f, 0.123f),
                0.021f,
                new[] { first, second });
            definition.ConfigureSequence(
                new[] { "mount.required.sentinel" },
                new[] { "mount.required-any.sentinel" },
                new[] { "mount.blocked.sentinel" });
            definition.ConfigureBoltedSequence(
                new[] { "mount.bolted.sentinel" },
                new[] { "mount.bolted-any.sentinel" });
            definition.ConfigureRemovalBlockers(
                new[] { "mount.removal.sentinel" });
            definition.ConfigureInstallationChecks(
                "mount.support.sentinel",
                new[] { "mount.install-bolted.sentinel" });
            definition.ConfigureRemovalChecks(
                new[] { "mount.remove-bolted.sentinel" },
                new[] { "mount.ignored-dependent.sentinel" });
            var group = new FastenerGroupDefinition();
            group.Configure(
                FastenerIds.ToArray(),
                maximumTightness: 16,
                onThreshold: onThreshold,
                offThreshold: 0,
                retentionPolicy: FastenerSpeedRetentionPolicy.None,
                configuredLooseBreakSpeedKph: 17.25f,
                configuredPartialCheckSpeedKph: 29.5f,
                configuredChanceDivisor: 73.75f,
                configuredBreakAction: FastenerBreakAction.None);
            definition.ConfigureFastenerGroup(group);
            return definition;
        }

        private FastenerDefinition CreateFastener(
            string id,
            FastenerSize size,
            int maximum,
            bool inserted,
            bool required,
            FastenerDirection direction,
            string toolType)
        {
            FastenerDefinition definition = Track(
                ScriptableObject.CreateInstance<FastenerDefinition>());
            definition.Configure(
                id,
                "Sentinel " + id,
                size,
                maximum,
                direction,
                inserted,
                required,
                ToolCompatibilityRule.Create(toolType, size));
            return definition;
        }

        private static void ApplyDrift(MountPointDefinition definition, string drift)
        {
            FastenerDefinition[] fasteners = definition.Fasteners;
            FastenerGroupDefinition group = definition.FastenerGroup;
            switch (drift)
            {
                case "definition-id":
                    ReconfigureMount(definition, id: "mount.satsuma.dashboard.wrong");
                    return;
                case "owner":
                    ReconfigureMount(definition, owner: "vehicle.satsuma.part.body");
                    return;
                case "accepted-part":
                    ReconfigureMount(definition,
                        accepted: new[] { "vehicle.satsuma.part.dashboard-wrong" });
                    return;
                case "fastener-count":
                    ReconfigureMount(definition, fasteners: new[] { fasteners[0] });
                    return;
                case "fastener-id":
                    ReconfigureFastener(fasteners[0], id: "fastener.wrong");
                    return;
                case "fastener-size":
                    ReconfigureFastener(fasteners[0], size: FastenerSize.Millimeter7);
                    return;
                case "fastener-maximum":
                    ReconfigureFastener(fasteners[0], maximum: 7);
                    return;
                case "fastener-inserted":
                    ReconfigureFastener(fasteners[0], inserted: false);
                    return;
                case "fastener-required":
                    ReconfigureFastener(fasteners[0], required: false);
                    return;
                case "group-ids":
                    ReconfigureGroup(group, ids: FastenerIds.Reverse().ToArray());
                    return;
                case "group-maximum":
                    ReconfigureGroup(group, maximum: 15);
                    return;
                case "group-on":
                    ReconfigureGroup(group, on: 11);
                    return;
                case "group-off":
                    ReconfigureGroup(group, off: 1);
                    return;
                case "retention":
                    ReconfigureGroup(group,
                        retention: FastenerSpeedRetentionPolicy.DonorWheelBoltCheck);
                    return;
                case "break-action":
                    ReconfigureGroup(group,
                        breakAction: FastenerBreakAction.DetachInstalledPart);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
            }
        }

        private static void ReconfigureMount(
            MountPointDefinition definition,
            string id = null,
            string owner = null,
            string[] accepted = null,
            FastenerDefinition[] fasteners = null)
        {
            definition.Configure(
                id ?? definition.DefinitionId,
                definition.DisplayName,
                definition.SocketType,
                owner ?? definition.OwnerPartDefinitionId,
                accepted ?? definition.AcceptedPartDefinitionIds,
                definition.Constraint,
                definition.ReferenceCandidateRadiusMeters,
                fasteners ?? definition.Fasteners);
        }

        private static void ReconfigureFastener(
            FastenerDefinition definition,
            string id = null,
            FastenerSize? size = null,
            int? maximum = null,
            bool? inserted = null,
            bool? required = null)
        {
            FastenerSize resolvedSize = size ?? definition.Size;
            definition.Configure(
                id ?? definition.DefinitionId,
                definition.DisplayName,
                resolvedSize,
                maximum ?? definition.MaximumStage,
                definition.TighteningDirection,
                inserted ?? definition.InsertedOnInstall,
                required ?? definition.RequiredForRemoval,
                ToolCompatibilityRule.Create(
                    definition.ToolRule.ToolType,
                    resolvedSize));
        }

        private static void ReconfigureGroup(
            FastenerGroupDefinition group,
            string[] ids = null,
            int? maximum = null,
            int? on = null,
            int? off = null,
            FastenerSpeedRetentionPolicy? retention = null,
            FastenerBreakAction? breakAction = null)
        {
            group.Configure(
                ids ?? group.FastenerDefinitionIds,
                maximum ?? group.AggregateMaximumTightness,
                on ?? group.BoltedOnThreshold,
                off ?? group.BoltedOffThreshold,
                retention ?? group.SpeedRetentionPolicy,
                group.LooseBreakSpeedKph,
                group.PartialCheckSpeedKph,
                group.ChanceDivisor,
                breakAction ?? group.BreakAction);
        }

        private T Track<T>(T value) where T : Object
        {
            transientObjects.Add(value);
            return value;
        }

        private static MountPointAuthoring GeneratedMount()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null,
                "The private generated Satsuma baseline is required.");
            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            return assembly.MountPoints.Single(value => value.MountId == MountId);
        }

        private static void SetAggregate(
            FastenerInstance[] fasteners,
            FastenerGroupState group,
            int total)
        {
            int remaining = total;
            foreach (FastenerInstance fastener in fasteners)
            {
                int stage = Mathf.Min(remaining, fastener.Definition.MaximumStage);
                Assert.That(fastener.TryRestore(
                        restoredInserted: true,
                        restoredSeated: true,
                        restoredStage: stage),
                    Is.True);
                remaining -= stage;
            }

            Assert.That(remaining, Is.Zero);
            group.Reevaluate(mountOccupied: true);
            Assert.That(group.Tightness, Is.EqualTo(total));
        }

        private sealed class SaveFixture : IDisposable
        {
            private readonly GameObject instance;
            private readonly VehicleAssemblySaveData emptySnapshot;
            private readonly PartInstance[] ownedParts;

            public SaveFixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                Assert.That(prefab, Is.Not.Null,
                    "The private generated Satsuma baseline is required.");
                instance = Object.Instantiate(prefab);
                Assembly = instance.GetComponent<VehicleAssemblyController>();
                Assert.That(Assembly, Is.Not.Null);
                ownedParts = Assembly.Parts.ToArray();
                Assembly.Initialize();
                emptySnapshot = Assembly.CaptureSaveData();
                Assert.That(Mount.Definition.FastenerGroup.BoltedOnThreshold,
                    Is.EqualTo(12), "Run the C1a scoped refresh first.");
            }

            private VehicleAssemblyController Assembly { get; }

            private MountPointAuthoring Mount => Assembly.MountPoints.Single(value =>
                value.MountId == MountId);

            public VehicleAssemblySaveData InstalledSnapshot(int tightness, bool bolted)
            {
                var data = JsonUtility.FromJson<VehicleAssemblySaveData>(
                    JsonUtility.ToJson(emptySnapshot));
                InstallHistoricalOccupancy(data, Mount);
                foreach (FastenerSaveDto saved in data.fasteners.Where(value =>
                             value.mountId == MountId))
                {
                    saved.inserted = true;
                    saved.seated = true;
                    saved.stage = 0;
                }

                int remaining = tightness;
                foreach (string id in FastenerIds)
                {
                    FastenerDefinition definition = Mount.Definition.Fasteners.Single(value =>
                        value.DefinitionId == id);
                    FastenerSaveDto saved = data.fasteners.Single(value =>
                        value.mountId == MountId && value.fastenerDefinitionId == id);
                    saved.stage = Mathf.Min(remaining, definition.MaximumStage);
                    remaining -= saved.stage;
                }

                Assert.That(remaining, Is.Zero,
                    "Requested tightness exceeds the dashboard-meter group.");
                data.fastenerGroups.Single(value => value.mountId == MountId).isBolted = bolted;
                return data;
            }

            public void AssertRestore(
                VehicleAssemblySaveData source,
                int tightness,
                bool bolted)
            {
                string sourceBefore = JsonUtility.ToJson(source);
                AssertSuccess(Assembly.ValidateSaveDataForRestore(source));
                AssertSuccess(Assembly.RestoreSaveData(source));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore),
                    "Validation and restore must not mutate the caller's DTO.");
                AssertLatch(tightness, bolted);

                VehicleAssemblySaveData roundTrip = Assembly.CaptureSaveData();
                Assert.That(roundTrip.fastenerGroups.Single(value =>
                    value.mountId == MountId).isBolted, Is.EqualTo(bolted));
                AssertSuccess(Assembly.RestoreSaveData(roundTrip));
                AssertLatch(tightness, bolted);
            }

            public void AssertRejectedWithoutMutation(VehicleAssemblySaveData source)
            {
                string sourceBefore = JsonUtility.ToJson(source);
                string assemblyBefore = JsonUtility.ToJson(Assembly.CaptureSaveData());
                Assert.That(Assembly.ValidateSaveDataForRestore(source).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
                Assert.That(Assembly.RestoreSaveData(source).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
                Assert.That(JsonUtility.ToJson(Assembly.CaptureSaveData()),
                    Is.EqualTo(assemblyBefore));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceBefore));
            }

            public void Dispose()
            {
                foreach (PartInstance part in ownedParts)
                {
                    if (part != null && !part.transform.IsChildOf(instance.transform))
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }

                Object.DestroyImmediate(instance);
            }

            private void AssertLatch(int tightness, bool bolted)
            {
                MountPointRuntime runtime = Assembly.ResolveMount(Mount);
                Assert.That(runtime.IsOccupied, Is.True);
                Assert.That(runtime.FastenerGroup.Tightness, Is.EqualTo(tightness));
                Assert.That(runtime.FastenerGroup.IsBolted, Is.EqualTo(bolted));
            }

            private void InstallHistoricalOccupancy(
                VehicleAssemblySaveData data,
                MountPointAuthoring mount)
            {
                MountSaveDto mountDto = data.mounts.Single(value =>
                    value.mountId == mount.MountId);
                if (!string.IsNullOrEmpty(mountDto.installedPartStableEntityId))
                {
                    return;
                }

                AssemblyOwnedMountAuthoring owned =
                    mount.GetComponent<AssemblyOwnedMountAuthoring>();
                if (owned != null && owned.RequireInstalledOwner &&
                    !owned.OwnerPart.IsAssemblyRoot)
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

            private static void AssertSuccess(AssemblyOperationResult result)
            {
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }
    }
}
