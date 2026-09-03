using System;
using System.IO;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class P0SatsumaBoltCheckParityTests
    {
        private const string LocalConfigurationPath =
            "Config/DonorPaths.local.json";

        [Test]
        public void RearAndWheelFastenerGroupsMatchLockedDonorInvariants()
        {
            GameObject prefab = LoadRequiredPrefab();
            VehicleAssemblyController assembly = prefab
                .GetComponent<VehicleAssemblyController>();

            AssertGroup(
                FindMount(assembly, "mount.satsuma.trail-arm-rl"),
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter12,
                },
                maximum: 16,
                onThreshold: 12,
                offThreshold: 0);
            AssertGroup(
                FindMount(assembly, "mount.satsuma.trail-arm-rr"),
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter12,
                },
                maximum: 16,
                onThreshold: 12,
                offThreshold: 0);
            AssertGroup(
                FindMount(assembly, "mount.satsuma.drum-brake-rl"),
                new[] { FastenerSize.Millimeter14 },
                maximum: 8,
                onThreshold: 8,
                offThreshold: 0);
            AssertGroup(
                FindMount(assembly, "mount.satsuma.drum-brake-rr"),
                new[] { FastenerSize.Millimeter14 },
                maximum: 8,
                onThreshold: 8,
                offThreshold: 0);
            AssertGroup(
                FindMount(assembly, "mount.satsuma.shock-rl"),
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter6,
                    FastenerSize.Millimeter6,
                },
                maximum: 24,
                onThreshold: 2,
                offThreshold: 0);
            AssertGroup(
                FindMount(assembly, "mount.satsuma.shock-rr"),
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter6,
                    FastenerSize.Millimeter6,
                },
                maximum: 24,
                onThreshold: 2,
                offThreshold: 0);

            AssemblyFastenerInteractionTarget[] targets = prefab
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            foreach (string shockMountId in new[]
                     {
                         "mount.satsuma.shock-rl",
                         "mount.satsuma.shock-rr",
                     })
            {
                Assert.That(
                    targets.Where(value => value.MountId == shockMountId)
                        .Select(value => value.FastenerDefinitionId)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    Is.EqualTo(3),
                    shockMountId +
                    " must expose the donor 12/6/6 fasteners across both presentation roots.");
            }

            foreach (string wheelMountId in new[]
                     {
                         "mount.satsuma.wheelfl-new",
                         "mount.satsuma.wheelfr-new",
                         "mount.satsuma.wheelrl-new",
                         "mount.satsuma.wheelrr-new",
                     })
            {
                MountPointAuthoring wheelMount = FindMount(
                    assembly,
                    wheelMountId);
                AssertGroup(
                    wheelMount,
                    new[]
                    {
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                    },
                    maximum: 32,
                    onThreshold: 1,
                    offThreshold: 0,
                    retention:
                        FastenerSpeedRetentionPolicy.DonorWheelBoltCheck,
                    looseBreakSpeedKph: 5f,
                    partialCheckSpeedKph: 33f,
                    breakAction: FastenerBreakAction.DetachInstalledPart);
                Assert.That(
                    wheelMount.Definition.Fasteners
                        .Select(value => value.DefinitionId)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    Is.EqualTo(4),
                    wheelMountId + " must own four unique 13 mm nuts.");
            }
        }

        [Test]
        public void RearInstallAllowsLooseSupportThenCollapsesUntilSupportIsBolted()
        {
            GameObject prefab = LoadRequiredPrefab();
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();

                foreach (string corner in new[] { "rl", "rr" })
                {
                    string armId = "mount.satsuma.trail-arm-" + corner;
                    AssertRearChildRetention(
                        FindMount(assembly, "mount.satsuma.drum-brake-" + corner),
                        armId);
                    AssertRearChildRetention(
                        FindMount(assembly, "mount.satsuma.coilspring-" + corner),
                        armId);
                    AssertRearChildRetention(
                        FindMount(assembly, "mount.satsuma.long-coilspring-" + corner),
                        armId);
                    AssertRearChildRetention(
                        FindMount(assembly, "mount.satsuma.shock-" + corner),
                        armId);
                }

                AssertWheelGate(
                    FindMount(assembly, "mount.satsuma.wheelfl-new"),
                    "mount.satsuma.discbrake-fl");
                AssertWheelGate(
                    FindMount(assembly, "mount.satsuma.wheelfr-new"),
                    "mount.satsuma.discbrake-fr");
                AssertWheelGate(
                    FindMount(assembly, "mount.satsuma.wheelrl-new"),
                    "mount.satsuma.drum-brake-rl");
                AssertWheelGate(
                    FindMount(assembly, "mount.satsuma.wheelrr-new"),
                    "mount.satsuma.drum-brake-rr");

                MountPointAuthoring armMount = FindMount(
                    assembly,
                    "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = FindMount(
                    assembly,
                    "mount.satsuma.drum-brake-rl");
                MountPointAuthoring springMount = FindMount(
                    assembly,
                    "mount.satsuma.coilspring-rl");
                MountPointAuthoring shockMount = FindMount(
                    assembly,
                    "mount.satsuma.shock-rl");
                PartInstance arm = FindLooseAcceptedPart(assembly, armMount);
                PartInstance drum = FindLooseAcceptedPart(assembly, drumMount);
                PartInstance spring = FindLooseAcceptedPart(assembly, springMount);
                PartInstance shock = FindLooseAcceptedPart(assembly, shockMount);

                Install(assembly, arm, armMount);
                MountPointRuntime armRuntime = ResolveRuntimeMount(
                    assembly,
                    armMount.MountId);
                Assert.That(armRuntime.FastenerGroup.Tightness, Is.Zero);
                Assert.That(armRuntime.FastenerGroup.IsBolted, Is.False);

                PositionAtMount(drum, drumMount);
                PositionAtMount(spring, springMount);
                PositionAtMount(shock, shockMount);
                Assert.That(assembly.EvaluateInstall(drum, drumMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(spring, springMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(shock, shockMount).Succeeded, Is.True);

                Install(assembly, drum, drumMount);
                Assert.That(
                    arm.IsInstalled,
                    Is.False,
                    "The donor socket accepts the drum, then the unbolted " +
                    "support and its child must fall loose together.");
                Assert.That(drum.IsInstalled, Is.False);
                Assert.That(armRuntime.IsOccupied, Is.False);
                Assert.That(
                    ResolveRuntimeMount(assembly, drumMount.MountId).IsOccupied,
                    Is.False);

                Install(assembly, arm, armMount);
                armRuntime = ResolveRuntimeMount(
                    assembly,
                    armMount.MountId);

                TightenGroupTo(assembly, armRuntime, 12);
                Assert.That(armRuntime.FastenerGroup.Tightness, Is.EqualTo(12));
                Assert.That(armRuntime.FastenerGroup.IsBolted, Is.True);
                Assert.That(assembly.EvaluateInstall(drum, drumMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(spring, springMount).Succeeded, Is.True);
                Assert.That(assembly.EvaluateInstall(shock, shockMount).Succeeded, Is.True);

                Install(assembly, shock, shockMount);
                MountPointRuntime shockRuntime = ResolveRuntimeMount(
                    assembly,
                    shockMount.MountId);
                AssemblyInstalledPartInteractionTarget shockRemoval = shock
                    .GetComponent<AssemblyInstalledPartInteractionTarget>();
                InteractionTargetHost shockHost = shock
                    .GetComponent<InteractionTargetHost>();
                var interactionContext = new InteractionContext(
                    instance,
                    shock.transform.position,
                    Vector3.forward);
                var shockCandidate = new InteractionCandidate(
                    shockHost,
                    shock.transform.position,
                    Vector3.up,
                    0f);
                Assert.That(shockRemoval, Is.Not.Null);
                Assert.That(shockHost, Is.Not.Null);

                void AssertRemovalPresentation(
                    bool expected,
                    string state)
                {
                    Assert.That(
                        shockRemoval.CanInteract(interactionContext),
                        Is.EqualTo(expected),
                        state);
                    string prompt = shockCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: interactionContext);
                    if (expected)
                    {
                        Assert.That(prompt, Does.StartWith("Снять:"), state);
                    }
                    else
                    {
                        Assert.That(prompt, Is.Empty, state);
                    }
                }

                FastenerInstance firstShockFastener = shockRuntime.Fasteners[0];
                ToolDefinition wrench = FindTool(
                    assembly,
                    firstShockFastener.Definition.Size);

                Turn(assembly, shockRuntime, firstShockFastener, wrench, true);
                Assert.That(shockRuntime.FastenerGroup.Tightness, Is.EqualTo(1));
                Assert.That(shockRuntime.FastenerGroup.IsBolted, Is.False);
                Assert.That(assembly.EvaluateRemoval(shock).Succeeded, Is.True);
                AssertRemovalPresentation(
                    expected: true,
                    state: "Below donor BoltedOnThreshold.");

                Turn(assembly, shockRuntime, firstShockFastener, wrench, true);
                Assert.That(shockRuntime.FastenerGroup.Tightness, Is.EqualTo(2));
                Assert.That(shockRuntime.FastenerGroup.IsBolted, Is.True);
                Assert.That(
                    assembly.EvaluateRemoval(shock).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.FastenerSecured));
                AssertRemovalPresentation(
                    expected: false,
                    state: "At donor BoltedOnThreshold.");

                Turn(assembly, shockRuntime, firstShockFastener, wrench, false);
                Assert.That(shockRuntime.FastenerGroup.Tightness, Is.EqualTo(1));
                Assert.That(
                    shockRuntime.FastenerGroup.IsBolted,
                    Is.True,
                    "The donor latch must survive a partial loosen from 2 to 1.");
                AssertRemovalPresentation(
                    expected: false,
                    state: "Above BoltedOffThreshold while the donor latch remains set.");

                Turn(assembly, shockRuntime, firstShockFastener, wrench, false);
                Assert.That(shockRuntime.FastenerGroup.Tightness, Is.Zero);
                Assert.That(shockRuntime.FastenerGroup.IsBolted, Is.False);
                Assert.That(assembly.EvaluateRemoval(shock).Succeeded, Is.True);
                AssertRemovalPresentation(
                    expected: true,
                    state: "At donor BoltedOffThreshold.");

                Install(assembly, spring, springMount);
                MountPointRuntime springRuntime = ResolveRuntimeMount(
                    assembly,
                    springMount.MountId);
                Assert.That(
                    springRuntime.FastenerGroup.Definition.HasFasteners,
                    Is.False);
                AssemblyInstalledPartInteractionTarget springRemoval = spring
                    .GetComponent<AssemblyInstalledPartInteractionTarget>();
                InteractionTargetHost springHost = spring
                    .GetComponent<InteractionTargetHost>();
                var springContext = new InteractionContext(
                    instance,
                    spring.transform.position,
                    Vector3.forward);
                var springCandidate = new InteractionCandidate(
                    springHost,
                    spring.transform.position,
                    Vector3.up,
                    0f);
                Assert.That(springRemoval, Is.Not.Null);
                Assert.That(springHost, Is.Not.Null);
                Assert.That(
                    assembly.EvaluateRemoval(spring).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.RemovalBlocked),
                    "The donor spring Removal FSM checks Shock/Data/Installed, " +
                    "not its fastener stage.");
                Assert.That(
                    springRemoval.CanInteract(springContext),
                    Is.False,
                    "A same-corner installed shock must suppress the spring " +
                    "removal capability and outline even when fully loose.");
                Assert.That(
                    springCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: springContext),
                    Is.Empty);

                Assert.That(
                    assembly.TryRemove(shock).Succeeded,
                    Is.True,
                    "The fully loosened shock must be removed before its spring.");
                Assert.That(assembly.EvaluateRemoval(spring).Succeeded, Is.True);
                Assert.That(springRemoval.CanInteract(springContext), Is.True);
                Assert.That(
                    springCandidate.GetPrompt(
                        hasCarriedObject: false,
                        context: springContext),
                    Does.StartWith("Снять:"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WheelBreakPolicyMatchesDonorSpeedAndChanceFormula()
        {
            VehicleAssemblyController assembly = LoadRequiredPrefab()
                .GetComponent<VehicleAssemblyController>();
            FastenerGroupDefinition group = FindMount(
                    assembly,
                    "mount.satsuma.wheelfl-new")
                .Definition.FastenerGroup;

            Assert.That(group.CalculateBreakChance01(0, 5f), Is.Zero);
            Assert.That(group.ShouldBreak(0, 5.01f, 0.999f), Is.True);
            Assert.That(group.CalculateBreakChance01(1, 33f), Is.Zero);

            float expectedPartialChance = 0.31f / 1.31f;
            Assert.That(
                group.CalculateBreakChance01(1, 33.01f),
                Is.EqualTo(expectedPartialChance).Within(0.000001f));
            Assert.That(
                group.ShouldBreak(
                    1,
                    34f,
                    FastenerGroupDefinition.Sample01(2u)),
                Is.True,
                "Seed 2 is below the donor normalized Chance weight.");
            Assert.That(
                group.ShouldBreak(
                    1,
                    34f,
                    FastenerGroupDefinition.Sample01(1u)),
                Is.False,
                "Seed 1 is above the donor normalized Chance weight.");
            Assert.That(group.ShouldBreak(32, 200f, 0f), Is.False);
        }

        [TestCase(205)]
        [TestCase(220)]
        public void Legacy115SaveShapeMigratesMissingFastenersAsFullyTight(
            int legacyFastenerCount)
        {
            GameObject prefab = LoadRequiredPrefab();
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Assert.That(assembly.AllowsAdditiveSaveMigration, Is.True);

                MountPointAuthoring armMount = FindMount(
                    assembly,
                    "mount.satsuma.trail-arm-rl");
                PartInstance arm = FindLooseAcceptedPart(assembly, armMount);
                Install(assembly, arm, armMount);

                MountPointAuthoring subframeMount = FindMount(
                    assembly,
                    "mount.satsuma.sub-frame");
                PartInstance subframe = FindLooseAcceptedPart(
                    assembly,
                    subframeMount);
                Install(assembly, subframe, subframeMount);
                MountPointRuntime subframeRuntime = ResolveRuntimeMount(
                    assembly,
                    subframeMount.MountId);
                FastenerInstance preservedFastener = subframeRuntime.Fasteners[0];
                ToolDefinition preservedTool = FindTool(
                    assembly,
                    preservedFastener.Definition.Size);
                for (int stage = 0; stage < 3; stage++)
                {
                    Turn(
                        assembly,
                        subframeRuntime,
                        preservedFastener,
                        preservedTool,
                        tighten: true);
                }

                PartInstance loosePart = assembly.Parts.First(value =>
                    !value.IsInstalled && !value.IsAssemblyRoot &&
                    value.Body != null);
                Vector3 loosePosition = new Vector3(2.75f, 1.15f, -4.5f);
                Quaternion looseRotation = Quaternion.Euler(17f, 93f, -11f);
                loosePart.transform.SetPositionAndRotation(
                    loosePosition,
                    looseRotation);
                loosePart.Body.position = loosePosition;
                loosePart.Body.rotation = looseRotation;

                VehicleAssemblySaveData current = assembly.CaptureSaveData();
                MountSaveDto armMountDto = current.mounts.Single(value =>
                    value.mountId == armMount.MountId);
                MountSaveDto subframeMountDto = current.mounts.Single(value =>
                    value.mountId == subframeMount.MountId);
                FastenerSaveDto[] prioritizedFasteners = current.fasteners
                    .Where(value => value.mountId == subframeMount.MountId)
                    .Concat(current.fasteners.Where(value =>
                        value.mountId != subframeMount.MountId &&
                        value.mountId != armMount.MountId))
                    .Take(legacyFastenerCount)
                    .ToArray();
                var legacy = new VehicleAssemblySaveData
                {
                    schemaVersion = VehicleAssemblySaveData.LegacySchemaVersion,
                    parts = current.parts,
                    mounts = new[] { armMountDto, subframeMountDto }
                        .Concat(current.mounts.Where(value =>
                            value.mountId != armMount.MountId &&
                            value.mountId != subframeMount.MountId))
                        .Take(115)
                        .ToArray(),
                    fasteners = prioritizedFasteners,
                    fastenerGroups = Array.Empty<FastenerGroupSaveDto>(),
                };

                Assert.That(legacy.parts, Has.Length.EqualTo(current.parts.Length));
                Assert.That(legacy.mounts, Has.Length.EqualTo(115));
                Assert.That(legacy.fasteners, Has.Length.EqualTo(legacyFastenerCount));
                Assert.That(
                    legacy.fasteners.Any(value =>
                        value.mountId == armMount.MountId),
                    Is.False,
                    "The arm fasteners must be genuinely absent from the legacy payload.");
                Assert.That(assembly.ValidateSaveDataForRestore(legacy).Succeeded, Is.True);
                Assert.That(assembly.RestoreSaveData(legacy).Succeeded, Is.True);

                MountPointRuntime migratedArm = ResolveRuntimeMount(
                    assembly,
                    armMount.MountId);
                Assert.That(migratedArm.IsOccupied, Is.True);
                Assert.That(
                    migratedArm.Fasteners,
                    Is.All.Matches<FastenerInstance>(value =>
                        value.IsInserted && value.IsSeated &&
                        value.Stage == value.Definition.MaximumStage));
                Assert.That(migratedArm.FastenerGroup.Tightness, Is.EqualTo(16));
                Assert.That(migratedArm.FastenerGroup.IsBolted, Is.True);

                MountPointRuntime migratedSubframe = ResolveRuntimeMount(
                    assembly,
                    subframeMount.MountId);
                Assert.That(
                    migratedSubframe.Fasteners[0].Stage,
                    Is.EqualTo(3),
                    "Existing legacy stages must survive additive migration unchanged.");

                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                Assert.That(
                    migrated.schemaVersion,
                    Is.EqualTo(VehicleAssemblySaveData.CurrentSchemaVersion));
                Assert.That(
                    migrated.fastenerGroups.Single(value =>
                        value.mountId == armMount.MountId).isBolted,
                    Is.True);
                Assert.That(
                    migrated.parts.Single(value =>
                        value.stableEntityId == loosePart.StableId.Value)
                        .worldPosition,
                    Is.EqualTo(loosePosition));

                string json = JsonUtility.ToJson(migrated);
                VehicleAssemblySaveData roundTripped = JsonUtility.FromJson<
                    VehicleAssemblySaveData>(json);
                Assert.That(
                    assembly.RestoreSaveData(roundTripped).Succeeded,
                    Is.True);
                Assert.That(
                    ResolveRuntimeMount(assembly, armMount.MountId)
                        .FastenerGroup.IsBolted,
                    Is.True);
                Assert.That(
                    loosePart.transform.position,
                    Is.EqualTo(loosePosition));
                Assert.That(
                    Quaternion.Angle(loosePart.transform.rotation, looseRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject LoadRequiredPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            if (File.Exists(LocalConfigurationPath))
            {
                DonorPathConfiguration paths =
                    DonorPathConfiguration.LoadFromFile(LocalConfigurationPath);
                Assert.Fail(
                    "Canonical generated Satsuma prefab is missing while a " +
                    "donor path is configured: " + paths.DonorStagingDirectory);
            }

            Assert.Ignore(
                "No canonical generated Satsuma prefab and no local donor path are available.");
            return null;
        }

        private static void AssertGroup(
            MountPointAuthoring mount,
            FastenerSize[] expectedSizes,
            int maximum,
            int onThreshold,
            int offThreshold,
            FastenerSpeedRetentionPolicy retention =
                FastenerSpeedRetentionPolicy.None,
            float looseBreakSpeedKph = 0f,
            float partialCheckSpeedKph = 0f,
            FastenerBreakAction breakAction = FastenerBreakAction.None)
        {
            FastenerDefinition[] fasteners = mount.Definition.Fasteners;
            FastenerGroupDefinition group = mount.Definition.FastenerGroup;
            Assert.That(group, Is.Not.Null, mount.MountId);
            Assert.That(fasteners.Select(value => value.Size), Is.EquivalentTo(expectedSizes));
            Assert.That(
                fasteners.Select(value => value.DefinitionId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Is.EqualTo(expectedSizes.Length),
                mount.MountId);
            Assert.That(
                group.FastenerDefinitionIds,
                Is.EquivalentTo(fasteners.Select(value => value.DefinitionId)),
                mount.MountId);
            Assert.That(group.AggregateMaximumTightness, Is.EqualTo(maximum));
            Assert.That(group.BoltedOnThreshold, Is.EqualTo(onThreshold));
            Assert.That(group.BoltedOffThreshold, Is.EqualTo(offThreshold));
            Assert.That(group.SpeedRetentionPolicy, Is.EqualTo(retention));
            Assert.That(group.LooseBreakSpeedKph, Is.EqualTo(looseBreakSpeedKph));
            Assert.That(group.PartialCheckSpeedKph, Is.EqualTo(partialCheckSpeedKph));
            Assert.That(group.BreakAction, Is.EqualTo(breakAction));
        }

        private static void AssertRearChildRetention(
            MountPointAuthoring mount,
            string armMountId)
        {
            Assert.That(
                mount.Definition.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { armMountId }),
                mount.MountId);
            Assert.That(
                mount.Definition.RequiredBoltedMountIds,
                Is.EqualTo(new[] { armMountId }),
                mount.MountId);
            Assert.That(mount.Definition.RequiredAnyOccupiedMountIds, Is.Empty);
            Assert.That(mount.Definition.RequiredAnyBoltedMountIds, Is.Empty);
        }

        private static void AssertWheelGate(
            MountPointAuthoring mount,
            string requiredBrakeMountId)
        {
            Assert.That(
                mount.Definition.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { requiredBrakeMountId }),
                mount.MountId);
            Assert.That(
                mount.Definition.RequiredBoltedMountIds,
                Is.Empty,
                "Wheel installation must not invent a drum-Bolted gate.");
            Assert.That(mount.Definition.RequiredAnyBoltedMountIds, Is.Empty);
        }

        private static MountPointAuthoring FindMount(
            VehicleAssemblyController assembly,
            string mountId) =>
            assembly.MountPoints.Single(value => value.MountId == mountId);

        private static MountPointRuntime ResolveRuntimeMount(
            VehicleAssemblyController assembly,
            string mountId)
        {
            Assert.That(
                assembly.Graph.TryGetMount(mountId, out MountPointRuntime mount),
                Is.True,
                mountId);
            return mount;
        }

        private static PartInstance FindLooseAcceptedPart(
            VehicleAssemblyController assembly,
            MountPointAuthoring mount) =>
            assembly.Parts.First(value =>
                !value.IsInstalled && value.Definition != null &&
                mount.Definition.AcceptsPart(value.Definition.DefinitionId));

        private static ToolDefinition FindTool(
            VehicleAssemblyController assembly,
            FastenerSize size) =>
            assembly.Tools.Single(value => value.Size == size);

        private static void PositionAtMount(
            PartInstance part,
            MountPointAuthoring mount)
        {
            part.transform.SetPositionAndRotation(
                mount.Pose.position,
                mount.Pose.rotation);
            if (part.Body != null)
            {
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
            }

            Physics.SyncTransforms();
        }

        private static void Install(
            VehicleAssemblyController assembly,
            PartInstance part,
            MountPointAuthoring mount)
        {
            PositionAtMount(part, mount);
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void TightenGroupTo(
            VehicleAssemblyController assembly,
            MountPointRuntime mount,
            int targetTightness)
        {
            int turnsRemaining = targetTightness -
                                 mount.FastenerGroup.Tightness;
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                ToolDefinition tool = FindTool(
                    assembly,
                    fastener.Definition.Size);
                while (turnsRemaining > 0 &&
                       fastener.Stage < fastener.Definition.MaximumStage)
                {
                    Turn(assembly, mount, fastener, tool, tighten: true);
                    turnsRemaining--;
                }
            }

            Assert.That(turnsRemaining, Is.Zero);
        }

        private static void Turn(
            VehicleAssemblyController assembly,
            MountPointRuntime mount,
            FastenerInstance fastener,
            ToolDefinition tool,
            bool tighten)
        {
            AssemblyOperationResult result = assembly.TryOperateFastener(
                mount.MountId,
                fastener.Definition.DefinitionId,
                tool,
                tighten);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }
    }
}
