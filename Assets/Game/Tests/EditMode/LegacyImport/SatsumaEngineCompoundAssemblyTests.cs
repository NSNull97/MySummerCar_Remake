using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Rules = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineCompoundAssemblyRules;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineCompoundAssemblyTests
    {
        private Fixture fixture;

        [SetUp]
        public void SetUp() => fixture = new Fixture();

        [TearDown]
        public void TearDown() => fixture?.Dispose();

        [Test]
        public void HeadGasketCannotBeInsertedOrRemovedThroughCylinderHead()
        {
            fixture.Install(Rules.HeadMount);
            fixture.AssertInstallBlocked(Rules.GasketMount);
            fixture.Remove(Rules.HeadMount);
            fixture.Install(Rules.GasketMount);
            fixture.Install(Rules.HeadMount);
            fixture.AssertRemovalBlocked(Rules.GasketMount);
            fixture.Remove(Rules.HeadMount);
            fixture.Remove(Rules.GasketMount);
        }

        [TestCase(Rules.GearboxMount)]
        [TestCase(Rules.FlywheelMount)]
        public void EnginePlateCannotBeInsertedThroughGearboxOrFlywheel(string blocker)
        {
            fixture.Install(blocker);
            fixture.AssertInstallBlocked(Rules.PlateMount);
            fixture.Remove(blocker);
            fixture.Install(Rules.PlateMount);
        }

        [Test]
        public void EnginePlateRemovalWaitsForFlywheelWithoutInventingGearboxPresenceGate()
        {
            fixture.Install(Rules.PlateMount);
            fixture.Install(Rules.FlywheelMount);
            fixture.AssertRemovalBlocked(Rules.PlateMount);
            fixture.Remove(Rules.FlywheelMount);
            fixture.Install(Rules.GearboxMount);
            fixture.Remove(Rules.PlateMount);
        }

        [Test]
        public void ClutchDiscRequiresPressurePlateButNotAnInstalledCover()
        {
            fixture.AssertInstallBlocked(Rules.DiscMount);
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            Assert.That(fixture.PartAt(Rules.CoverMount).IsInstalled, Is.False);
        }

        [TestCase(Rules.PressureMount)]
        [TestCase(Rules.DiscMount)]
        public void InnerClutchPartsCannotBeInsertedAfterCoverIsOnFlywheel(string innerMount)
        {
            if (innerMount == Rules.DiscMount)
            {
                fixture.Install(Rules.PressureMount);
            }

            fixture.Install(Rules.CoverMount);
            fixture.AssertInstallBlocked(innerMount);
            fixture.Remove(Rules.CoverMount);
            fixture.Install(innerMount);
        }

        [Test]
        public void PressurePlateRemovalWaitsForDisc()
        {
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            fixture.AssertRemovalBlocked(Rules.PressureMount);
            fixture.Remove(Rules.DiscMount);
            fixture.Remove(Rules.PressureMount);
        }

        [Test]
        public void CompleteClutchInstallsAndRemovesAsOneAssemblyKeepingBothChildren()
        {
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            PartInstance cover = fixture.PartAt(Rules.CoverMount);
            PartInstance pressure = fixture.PartAt(Rules.PressureMount);
            PartInstance disc = fixture.PartAt(Rules.DiscMount);
            fixture.Install(Rules.CoverMount);
            fixture.SetFastened(Rules.CoverMount, true);
            Assert.That(fixture.Assembly.EvaluateRemoval(cover).FailureReason,
                Is.EqualTo(AssemblyFailureReason.FastenerSecured));
            fixture.AssertRemovalBlocked(Rules.DiscMount);
            fixture.SetFastened(Rules.CoverMount, false);

            string pressureMount = pressure.RuntimeState.InstalledMountId;
            string discMount = disc.RuntimeState.InstalledMountId;
            fixture.Remove(Rules.CoverMount);
            Assert.That(cover.IsInstalled, Is.False);
            Assert.That(pressure.IsInstalled && disc.IsInstalled, Is.True);
            Assert.That(pressure.RuntimeState.InstalledMountId, Is.EqualTo(pressureMount));
            Assert.That(disc.RuntimeState.InstalledMountId, Is.EqualTo(discMount));
            Assert.That(pressure.transform.IsChildOf(cover.transform), Is.True);
            Assert.That(disc.transform.IsChildOf(cover.transform), Is.True);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mounts[Rules.CoverMount]).IsOccupied,
                Is.False);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mounts[Rules.PressureMount]).IsOccupied,
                Is.True);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mounts[Rules.DiscMount]).IsOccupied,
                Is.True);

            fixture.Install(Rules.CoverMount);
            Assert.That(pressure.transform.IsChildOf(cover.transform), Is.True);
            Assert.That(disc.transform.IsChildOf(cover.transform), Is.True);
        }

        [Test]
        public void RemovedCompleteClutchRestoresWithLooseCoverAndInstalledInnerParts()
        {
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            fixture.Install(Rules.CoverMount);
            fixture.Remove(Rules.CoverMount);
            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
            string before = JsonUtility.ToJson(saved);
            fixture.Remove(Rules.DiscMount);
            fixture.Remove(Rules.PressureMount);
            AssemblyOperationResult restored = fixture.Assembly.RestoreSaveData(saved);
            Assert.That(restored.Succeeded, Is.True, restored.Message);
            Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(before));
            Assert.That(fixture.PartAt(Rules.CoverMount).IsInstalled, Is.False);
            Assert.That(fixture.PartAt(Rules.PressureMount).IsInstalled, Is.True);
            Assert.That(fixture.PartAt(Rules.DiscMount).IsInstalled, Is.True);
            fixture.Install(Rules.CoverMount);
            fixture.Remove(Rules.CoverMount);
        }

        [Test]
        public void EmptyRetainedChildWhitelistPreservesDefaultOwnershipBlocker()
        {
            fixture.Definitions[Rules.CoverMount].ConfigureRetainedRemovalChildren(Array.Empty<string>());
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.CoverMount);
            fixture.AssertRemovalBlocked(Rules.CoverMount);
        }

        [Test]
        public void RetainedChildWhitelistDoesNotBypassAnUnlistedChildOrExplicitBlocker()
        {
            fixture.Definitions[Rules.CoverMount].ConfigureRetainedRemovalChildren(
                new[] { Rules.PressureMount });
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            fixture.Install(Rules.CoverMount);
            fixture.AssertRemovalBlocked(Rules.CoverMount);
            fixture.Definitions[Rules.CoverMount].ConfigureRetainedRemovalChildren(
                new[] { Rules.PressureMount, Rules.DiscMount });
            fixture.Definitions[Rules.CoverMount].ConfigureRemovalBlockers(new[] { Rules.DiscMount });
            fixture.AssertRemovalBlocked(Rules.CoverMount);
        }

        [Test]
        public void OnlyFaultyClutchDependencyPairIsRemoved()
        {
            var keep = AssemblyDependency.Create("vehicle.satsuma.part.flywheel",
                "vehicle.satsuma.part.clutch-cover-plate", AssemblyDependencyKind.RemovalBlockedWhileInstalled);
            var wrongKind = AssemblyDependency.Create("vehicle.satsuma.part.clutch",
                "vehicle.satsuma.part.clutch-cover-plate", AssemblyDependencyKind.InstallRequiresBolted);
            AssemblyDependency[] source = Fixture.LegacyDependencies().Concat(new[] { keep, wrongKind }).ToArray();
            AssemblyDependency[] filtered = Rules.FilterEngineDependencies(source);
            Assert.That(filtered, Is.EqualTo(new[] { keep, wrongKind }));
            Assert.That(Rules.FilterEngineDependencies(filtered), Is.EqualTo(filtered));
        }

        [Test]
        public void TimingChainGoesInBeforeCoverAndCannotBeRemovedThroughIt()
        {
            fixture.Install(Rules.TimingCoverMount);
            fixture.AssertInstallBlocked(Rules.TimingChainMount);
            fixture.Remove(Rules.TimingCoverMount);
            fixture.Install(Rules.TimingChainMount);
            fixture.Install(Rules.TimingCoverMount);
            fixture.AssertRemovalBlocked(Rules.TimingChainMount);
            fixture.Remove(Rules.TimingCoverMount);
            fixture.Remove(Rules.TimingChainMount);
        }

        [TestCase(Rules.GearboxMount)]
        [TestCase(Rules.OilpanMount)]
        [TestCase(Rules.SubframeMount)]
        public void EngineNeedsGearboxOilpanAndInstalledSubframeForChassisInstallation(string missing)
        {
            UseEngineFixture();
            foreach (string prerequisite in new[] { Rules.GearboxMount, Rules.OilpanMount, Rules.SubframeMount })
            {
                if (prerequisite != missing)
                {
                    fixture.Install(prerequisite);
                }
            }

            fixture.AssertInstallBlocked(Rules.EngineMount);
            fixture.Install(missing);
            fixture.Install(Rules.EngineMount);
        }

        [Test]
        public void CompleteEngineRetainsNestedClutchAndTightInternalsWhenRemovedFromChassis()
        {
            UseEngineFixture();
            fixture.Install("mount.satsuma.engine-block.crankshaft");
            fixture.Install(Rules.GasketMount);
            fixture.Install(Rules.HeadMount);
            fixture.Install(Rules.PlateMount);
            fixture.Install(Rules.PressureMount);
            fixture.Install(Rules.DiscMount);
            fixture.Install(Rules.CoverMount);
            fixture.SetFastened(Rules.CoverMount, true);
            PrepareEngineForCar();
            fixture.Install(Rules.EngineMount);
            fixture.SetFastened(Rules.EngineMount, true);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.PartAt(Rules.EngineMount)).FailureReason,
                Is.EqualTo(AssemblyFailureReason.FastenerSecured));
            fixture.SetFastened(Rules.EngineMount, false);
            string[] retained = { Rules.GearboxMount, Rules.OilpanMount, Rules.GasketMount,
                Rules.HeadMount, Rules.PlateMount, Rules.FlywheelMount, Rules.CoverMount,
                Rules.PressureMount, Rules.DiscMount };
            fixture.Remove(Rules.EngineMount);
            PartInstance block = fixture.PartAt(Rules.EngineMount);
            foreach (string id in retained)
            {
                Assert.That(fixture.PartAt(id).IsInstalled, Is.True, id);
                Assert.That(fixture.PartAt(id).transform.IsChildOf(block.transform), Is.True, id);
            }

            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
            Assert.That(fixture.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            fixture.Install(Rules.EngineMount);
            fixture.Remove(Rules.EngineMount);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mounts[Rules.CoverMount]).FastenerGroup.IsBolted,
                Is.True, "Engine removal must not reset inner fastener state.");
        }

        [TestCase(Rules.HalfshaftLeftMount)]
        [TestCase(Rules.HalfshaftRightMount)]
        [TestCase(Rules.GearLinkageMount)]
        public void ExternalConnectionsBlockEngineRemovalOnlyWhileBolted(string external)
        {
            UseEngineFixture(external);
            PrepareEngineForCar();
            fixture.Install(Rules.EngineMount);
            fixture.SetFastened(external, true);
            fixture.AssertRemovalBlocked(Rules.EngineMount);
            fixture.SetFastened(external, false);
            fixture.Remove(Rules.EngineMount);
            Assert.That(fixture.PartAt(external).IsInstalled, Is.True,
                "Manual engine removal does not force-detach a loose external connection.");
        }

        [Test]
        public void ClutchLinePresenceBlocksEngineRemovalEvenWhenUnfastened()
        {
            UseEngineFixture(Rules.ClutchLiningMount);
            PrepareEngineForCar();
            fixture.Install(Rules.EngineMount);
            fixture.AssertRemovalBlocked(Rules.EngineMount);
            fixture.Remove(Rules.ClutchLiningMount);
            fixture.Remove(Rules.EngineMount);
        }

        [Test]
        public void ExhaustPresenceAloneDoesNotInventABoltedRemovalBlocker()
        {
            UseEngineFixture(Rules.ExhaustMount);
            PrepareEngineForCar();
            fixture.Install(Rules.EngineMount);
            fixture.Remove(Rules.EngineMount);
            Assert.That(fixture.Definitions[Rules.EngineMount].RemovalBlockedWhileBoltedMountIds,
                Does.Contain(Rules.ExhaustMount));
        }

        [Test]
        public void NewUnreviewedEngineOwnedSocketStillBlocksWholeEngineRemoval()
        {
            fixture.Dispose();
            fixture = new Fixture(engineToCar: true, addUnreviewedSocket: true);
            PrepareEngineForCar();
            fixture.Install(Fixture.UnreviewedMount);
            fixture.Install(Rules.EngineMount);
            fixture.AssertRemovalBlocked(Rules.EngineMount);
            fixture.Remove(Fixture.UnreviewedMount);
            fixture.Remove(Rules.EngineMount);
        }

        [Test]
        public void EngineToCarRefreshIsScopedIdempotentAndDoesNotCreateReverseDependencies()
        {
            UseEngineFixture();
            string[] before = fixture.Definitions.Values.Select(value => EditorJsonUtility.ToJson(value)).ToArray();
            Assert.That(Rules.ApplyEngineToCarRules(fixture.Definitions.Values.ToArray()), Is.Empty);
            Assert.That(fixture.Definitions.Values.Select(value => EditorJsonUtility.ToJson(value)), Is.EqualTo(before));
            Assert.That(fixture.Definitions[Rules.EngineMount].RequiredOccupiedMountIds, Is.Empty);
            string[] retained = fixture.Definitions[Rules.EngineMount].RemovalRetainedChildMountIds;
            Assert.That(retained, Does.Contain(SatsumaConsumableAssemblyRules.BeltMountId));
            Assert.That(retained.Count(id => id != SatsumaConsumableAssemblyRules.BeltMountId), Is.EqualTo(20));
            Assert.That(retained.Distinct().Count(), Is.EqualTo(21));
            Assert.That(fixture.Definitions[Rules.EngineMount].InstallationRequiredOccupiedMountIds,
                Is.EquivalentTo(new[] { Rules.GearboxMount, Rules.OilpanMount, Rules.SubframeMount }));
        }

        private void UseEngineFixture(params string[] initiallyInstalledExternalMounts)
        {
            fixture.Dispose();
            fixture = new Fixture(true, initiallyInstalledExternalMounts);
        }

        private void PrepareEngineForCar()
        {
            fixture.Install(Rules.GearboxMount);
            fixture.Install(Rules.OilpanMount);
            fixture.Install(Rules.SubframeMount);
        }

        [Test]
        public void ScopedRulesAreIdempotentAndRetainFastenersPosesAndUnrelatedFields()
        {
            string[] before = fixture.Definitions.Values.Select(value => EditorJsonUtility.ToJson(value)).ToArray();
            Assert.That(Rules.ApplyEngineCompoundRules(fixture.Definitions.Values.ToArray()), Is.Empty);
            Assert.That(fixture.Definitions.Values.Select(value => EditorJsonUtility.ToJson(value)),
                Is.EqualTo(before));
            foreach (MountPointDefinition definition in fixture.Definitions.Values)
            {
                MountPointDefinition source = Fixture.LoadDefinition(definition.DefinitionId);
                Assert.That(definition.Fasteners, Is.EqualTo(source.Fasteners));
                Assert.That(JsonUtility.ToJson(definition.FastenerGroup),
                    Is.EqualTo(JsonUtility.ToJson(source.FastenerGroup)));
                Assert.That(JsonUtility.ToJson(definition.Constraint),
                    Is.EqualTo(JsonUtility.ToJson(source.Constraint)));
                Assert.That(definition.AcceptedPartDefinitionIds, Is.EqualTo(source.AcceptedPartDefinitionIds));
                Assert.That(definition.OwnerPartDefinitionId, Is.EqualTo(source.OwnerPartDefinitionId));
                Assert.That(definition.ReferenceCandidateRadiusMeters,
                    Is.EqualTo(source.ReferenceCandidateRadiusMeters));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public const string UnreviewedMount = "mount.test.engine-block.unreviewed";
            private readonly GameObject root = new GameObject("Engine compound assembly fixture");
            private readonly List<ScriptableObject> disposableDefinitions = new List<ScriptableObject>();
            private readonly Dictionary<string, PartInstance> parts = new Dictionary<string, PartInstance>();
            public readonly Dictionary<string, MountPointDefinition> Definitions =
                new Dictionary<string, MountPointDefinition>();
            public readonly Dictionary<string, MountPointAuthoring> Mounts =
                new Dictionary<string, MountPointAuthoring>();
            public VehicleAssemblyController Assembly { get; }

            public Fixture(bool engineToCar = false, string[] initiallyInstalledMounts = null,
                bool addUnreviewedSocket = false)
            {
                string[] ids = { Rules.GasketMount, Rules.PlateMount, Rules.HeadMount, Rules.GearboxMount,
                    Rules.FlywheelMount, Rules.CoverMount, Rules.PressureMount, Rules.DiscMount,
                    Rules.TimingChainMount, Rules.TimingCoverMount };
                if (engineToCar)
                {
                    ids = ids.Concat(Rules.EngineToCarReferencedMountIds).Distinct(StringComparer.Ordinal).ToArray();
                }
                foreach (string id in ids)
                {
                    MountPointDefinition definition = Object.Instantiate(LoadDefinition(id));
                    disposableDefinitions.Add(definition);
                    Definitions.Add(id, definition);
                }

                Rules.ApplyEngineCompoundRules(Definitions.Values.ToArray());
                if (engineToCar)
                {
                    Rules.ApplyEngineToCarRules(Definitions.Values.ToArray());
                }

                if (addUnreviewedSocket)
                {
                    MountPointDefinition extra = ScriptableObject.CreateInstance<MountPointDefinition>();
                    disposableDefinitions.Add(extra);
                    extra.Configure(UnreviewedMount, "Unreviewed child", "test.socket.unreviewed",
                        "vehicle.satsuma.part.engine-block", new[] { "test.part.unreviewed" },
                        new MountConstraint(0.2f, 40f, 1f, 0f), 0f, Array.Empty<FastenerDefinition>());
                    Definitions.Add(UnreviewedMount, extra);
                }

                // Set up already connected external parts without testing their
                // separate suspension/exhaust installation sequence here.
                var initialByPart = (initiallyInstalledMounts ?? Array.Empty<string>())
                    .ToDictionary(id => AcceptedPartId(id), id => id, StringComparer.Ordinal);
                string[] partIds = Definitions.Values.SelectMany(value => value.AcceptedPartDefinitionIds)
                    .Concat(Definitions.Values.Select(value => value.OwnerPartDefinitionId))
                    .Distinct(StringComparer.Ordinal).ToArray();
                foreach (string id in partIds)
                {
                    PartDefinition definition = ScriptableObject.CreateInstance<PartDefinition>();
                    disposableDefinitions.Add(definition);
                    definition.Configure(id, id, PartCategory.Engine, 1f, null,
                        Definitions.Values.Where(value => value.AcceptsPart(id))
                            .Select(value => PartCompatibilityRule.Create(value.SocketType)).ToArray());
                    var go = new GameObject(id);
                    go.transform.SetParent(root.transform);
                    StableEntityIdAuthoring identity = go.AddComponent<StableEntityIdAuthoring>();
                    identity.InitializeExplicitRuntimeId(StableEntityId.New());
                    Rigidbody body = go.AddComponent<Rigidbody>();
                    PartInstance part = go.AddComponent<PartInstance>();
                    part.Configure(definition, identity, body, null,
                        id == "vehicle.satsuma.part.body-shell",
                        initialByPart.TryGetValue(id, out string initial) ? initial : string.Empty);
                    parts.Add(id, part);
                }

                foreach (MountPointDefinition definition in Definitions.Values)
                {
                    PartInstance owner = parts[definition.OwnerPartDefinitionId];
                    var go = new GameObject(definition.DefinitionId);
                    go.transform.SetParent(owner.transform, false);
                    var mount = go.AddComponent<MountPointAuthoring>();
                    mount.Configure(definition, definition.DefinitionId, go.transform, 0);
                    go.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                    Mounts.Add(mount.MountId, mount);
                }

                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.Values.ToArray(), Mounts.Values.ToArray(),
                    Rules.FilterEngineDependencies(LegacyDependencies()), Array.Empty<ToolDefinition>(),
                    root.transform);
            }

            public static MountPointDefinition LoadDefinition(string id)
            {
                MountPointDefinition definition = AssetDatabase.LoadAssetAtPath<MountPointDefinition>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MountDefinitions/" + id + ".asset");
                Assert.That(definition, Is.Not.Null, id);
                return definition;
            }

            public static AssemblyDependency[] LegacyDependencies() => new[]
            {
                AssemblyDependency.Create("vehicle.satsuma.part.clutch", "vehicle.satsuma.part.clutch-cover-plate",
                    AssemblyDependencyKind.InstallRequiresInstalled),
                AssemblyDependency.Create("vehicle.satsuma.part.clutch-cover-plate", "vehicle.satsuma.part.clutch",
                    AssemblyDependencyKind.RemovalBlockedWhileInstalled),
                AssemblyDependency.Create("vehicle.satsuma.part.timing-chain", "vehicle.satsuma.part.timing-cover",
                    AssemblyDependencyKind.InstallRequiresInstalled),
                AssemblyDependency.Create("vehicle.satsuma.part.timing-cover", "vehicle.satsuma.part.timing-chain",
                    AssemblyDependencyKind.RemovalBlockedWhileInstalled),
            };

            private string AcceptedPartId(string mountId) => mountId == Rules.HalfshaftRightMount
                ? Definitions[mountId].AcceptedPartDefinitionIds.Last()
                : Definitions[mountId].AcceptedPartDefinitionIds[0];

            public PartInstance PartAt(string mountId) => parts[AcceptedPartId(mountId)];

            public void Install(string mountId)
            {
                // The successful cover path assumes a secured flywheel; the
                // separate donor failed-attempt/drop policy is not approximated
                // by these access-rule tests.
                if (mountId == Rules.CoverMount && !PartAt(Rules.FlywheelMount).IsInstalled)
                {
                    Install(Rules.FlywheelMount);
                    SetFastened(Rules.FlywheelMount, true);
                }

                PartInstance part = PartAt(mountId);
                MountPointAuthoring mount = Mounts[mountId];
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, mountId + ": " + result.Message);
            }

            public void Remove(string mountId)
            {
                AssemblyOperationResult result = Assembly.TryRemove(PartAt(mountId));
                Assert.That(result.Succeeded, Is.True, mountId + ": " + result.Message);
            }

            public void AssertInstallBlocked(string mountId)
            {
                PartInstance part = PartAt(mountId);
                MountPointAuthoring mount = Mounts[mountId];
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                AssemblyOperationResult result = Assembly.EvaluateHandoffInstall(part, mount);
                Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.MissingPrerequisite), result.Message);
            }

            public void AssertRemovalBlocked(string mountId)
            {
                AssemblyOperationResult result = Assembly.EvaluateRemoval(PartAt(mountId));
                Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.RemovalBlocked), result.Message);
            }

            public void SetFastened(string mountId, bool tighten)
            {
                foreach (FastenerDefinition fastener in Definitions[mountId].Fasteners)
                {
                    ToolDefinition tool = ScriptableObject.CreateInstance<ToolDefinition>();
                    disposableDefinitions.Add(tool);
                    tool.Configure("test.tool." + fastener.DefinitionId, "Test tool",
                        fastener.ToolRule.ToolType, fastener.ToolRule.FastenerSize);
                    for (int stage = 0; stage < fastener.MaximumStage; stage++)
                    {
                        AssemblyOperationResult result = Assembly.TryOperateFastener(mountId,
                            fastener.DefinitionId, tool, tighten);
                        Assert.That(result.Succeeded, Is.True, result.Message);
                    }
                }
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (ScriptableObject definition in disposableDefinitions)
                {
                    Object.DestroyImmediate(definition);
                }
            }
        }
    }
}
