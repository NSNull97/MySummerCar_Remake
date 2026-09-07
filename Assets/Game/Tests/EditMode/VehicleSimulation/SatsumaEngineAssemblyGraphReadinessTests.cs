using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaReadinessItemConditionProbe : MonoBehaviour, IAssemblyItemCondition
    {
        public float ConditionPercent { get; private set; } = 100f;
        public bool IsBroken { get; private set; }
        public void Set(float condition, bool broken = false)
        {
            ConditionPercent = condition;
            IsBroken = broken;
        }
    }

    /// <summary>
    /// Graph integration, not donor geometry/physics or an all-parts assembly
    /// recipe. Fixture bolts deliberately use ON2/OFF0/MAX8 to distinguish an
    /// installed part, a latched group and a completely tightened fastener.
    /// </summary>
    public sealed class SatsumaEngineAssemblyGraphReadinessTests
    {
        [Test]
        public void InstalledPowertrainAndElectricsNeedPresenceNotFullFastening()
        {
            using var f = new Fixture();
            f.InstallStructure();
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.True);
            foreach (string suffix in Fixture.PresenceOnlyStructure)
            {
                Assert.That(f.Graph.IsPartDefinitionBolted(Fixture.Id(suffix)), Is.False, suffix);
                f.Remove(suffix);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.False, suffix);
                f.Install(suffix);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.True, suffix);
            }
        }

        [Test]
        public void CylinderHeadAndDistributorRequireBoltedLatchButNotMaximumStage()
        {
            using var f = new Fixture();
            f.InstallStructure(boltHeadAndDistributor: false);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.False);
            f.SetStage("cylinder-head", 1);
            f.SetStage("distributor", 2);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.False);
            f.SetStage("cylinder-head", 2);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.True);
            Assert.That(f.Runtime("cylinder-head").Fasteners[0].Stage, Is.EqualTo(2));
            Assert.That(f.Runtime("distributor").Fasteners[0].Stage, Is.EqualTo(2));
            foreach (string suffix in new[] { "cylinder-head", "distributor" })
            {
                f.SetStage(suffix, 0);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.False, suffix);
                f.SetStage(suffix, 2);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(f.Graph), Is.True, suffix);
            }
        }

        [Test]
        public void FourDynamicPlugsAreMatchedToTheirActualCylinderSocketsAndPistons()
        {
            using var f = new Fixture();
            f.InstallStructure();
            f.InstallPistons();
            int baseCount = f.Assembly.Parts.Length;
            PartInstance[] plugs = f.PurchaseFourPlugs();
            Assert.That(f.Assembly.Parts.Length, Is.EqualTo(baseCount));
            Assert.That(f.Assembly.AllRuntimeParts.Length, Is.EqualTo(baseCount + 4));
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.Zero,
                "Owned, healthy loose purchases are not installed spark plugs.");
            for (int i = 0; i < 4; i++)
            {
                Assert.That(plugs[i].Definition, Is.SameAs(plugs[0].Definition));
                if (i != 0) Assert.That(plugs[i].StableId, Is.Not.EqualTo(plugs[0].StableId));
                f.InstallPlug(plugs[i], i + 1);
                Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo((1 << (i + 1)) - 1));
            }
            f.Remove(plugs[1]);
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(0b1101));
            f.InstallPlug(plugs[1], 2);
            f.Remove("piston3");
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(0b1011));
            f.Install("piston3");
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(0b1111));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(8)]
        public void PlugThreadStageDoesNotBecomeAnAllOrNothingFiringGate(int stage)
        {
            using var f = new Fixture();
            f.InstallStructure();
            f.InstallPistons();
            PartInstance[] plugs = f.PurchaseFourPlugs();
            for (int i = 0; i < 4; i++)
            {
                f.InstallPlug(plugs[i], i + 1);
                f.SetPlugStage(i + 1, stage);
                Assert.That(f.PlugRuntime(i + 1).Fasteners[0].Stage, Is.EqualTo(stage));
            }
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(15));
            Assert.That(SatsumaEngineAssemblyReadiness.HasFiringPairs(15), Is.True);
        }

        [TestCase(0f, false, false)]
        [TestCase(.99f, false, false)]
        [TestCase(1f, false, true)]
        [TestCase(9f, false, true)]
        [TestCase(100f, false, true)]
        [TestCase(100f, true, false)]
        [TestCase(float.NaN, false, false)]
        [TestCase(float.PositiveInfinity, false, false)]
        public void InstalledPurchasedPlugUsesItsLiveCondition(float condition, bool broken, bool usable)
        {
            using var f = new Fixture();
            f.Install("piston1");
            PartInstance plug = f.PurchasePlug();
            f.InstallPlug(plug, 1);
            plug.GetComponent<SatsumaReadinessItemConditionProbe>().Set(condition, broken);
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(usable ? 1 : 0));
        }

        [Test]
        public void CorrectDefinitionWithoutItemConditionIsNotAnImplicitlyNewPlug()
        {
            using var f = new Fixture();
            f.Install("piston1");
            PartInstance plug = f.PurchasePlug(withCondition: false);
            f.InstallPlug(plug, 1);
            int mutation = f.Assembly.GraphMutationCount;
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.Zero);
            plug.gameObject.AddComponent<SatsumaReadinessItemConditionProbe>();
            Assert.That(f.Assembly.GraphMutationCount, Is.EqualTo(mutation));
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(f.Graph), Is.EqualTo(1));
        }

        [Test]
        public void FuelNeedsInstalledPumpAndBoltedCarbButNotTightLineOrAirFilter()
        {
            using var f = new Fixture();
            f.InstallFuel(boltCarb: false);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.False);
            f.SetStage("carburetor", 1);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.False);
            f.SetStage("carburetor", 2);
            Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.True);
            foreach (string suffix in new[] { "fuel-tank", "fuel-strainer", "fuel-pump", "timing-chain" })
            {
                Assert.That(f.Graph.IsPartDefinitionBolted(Fixture.Id(suffix)), Is.False, suffix);
                f.Remove(suffix);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.False, suffix);
                f.Install(suffix);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.True, suffix);
            }
            foreach (string suffix in new[] { "fuel-line", "airfilter" })
            {
                f.Install(suffix);
                Assert.That(f.Runtime(suffix).Fasteners[0].Stage, Is.Zero);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.True, suffix);
                f.Remove(suffix);
                Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.True, suffix);
            }
            f.SetStage("carburetor", 0);
            f.Remove("carburetor");
            Assert.That(SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(f.Graph), Is.False);
        }

        [Test]
        public void AdapterRefreshesConditionsWithoutMutationAndStructureAfterActualRemoval()
        {
            using var f = new Fixture();
            f.InstallStructure();
            f.InstallPistons();
            PartInstance[] plugs = f.PurchaseFourPlugs();
            for (int i = 0; i < plugs.Length; i++) f.InstallPlug(plugs[i], i + 1);
            AssemblyVehiclePrerequisiteAdapter adapter = f.CreateAdapter();
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.CombustionUnavailable), Is.False);
            int mutation = f.Assembly.GraphMutationCount;
            plugs[1].GetComponent<SatsumaReadinessItemConditionProbe>().Set(.99f);
            plugs[2].GetComponent<SatsumaReadinessItemConditionProbe>().Set(100f, true);
            Assert.That(f.Assembly.GraphMutationCount, Is.EqualTo(mutation));
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.CombustionUnavailable), Is.True);
            Assert.That(adapter.CachedGraphMutationCount, Is.EqualTo(mutation));
            plugs[1].GetComponent<SatsumaReadinessItemConditionProbe>().Set(1f);
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.CombustionUnavailable), Is.False);
            f.Remove("camshaft");
            Assert.That(f.Assembly.GraphMutationCount, Is.GreaterThan(mutation));
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.CombustionUnavailable), Is.True);
            f.Install("camshaft");
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.CombustionUnavailable), Is.False);
        }

        [Test]
        public void AdapterFluidOverrideDoesNotInstallMissingFuelPumpOrMaskItsRemoval()
        {
            using var f = new Fixture();
            f.InstallFuel();
            AssemblyVehiclePrerequisiteAdapter adapter = f.CreateAdapter();
            adapter.SetPrototypeAvailability(false, false, false, 12.6f);
            adapter.SetFluidReadinessTestOverride(true);
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.False);
            f.Remove("fuel-pump");
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.True);
            f.Install("fuel-pump");
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.False);
            adapter.SetFluidReadinessTestOverride(false);
            Assert.That(f.Evaluate(adapter).HasAny(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            public static readonly string[] PresenceOnlyStructure =
                { "crankshaft", "camshaft", "timing-chain", "rocker-shaft", "flywheel", "electrics" };
            private readonly GameObject root = new("readiness graph fixture");
            private readonly GameObject loose = new("readiness loose parts");
            private readonly List<Object> definitions = new();
            private readonly Dictionary<string, PartInstance> parts = new();
            private readonly Dictionary<string, MountPointAuthoring> mounts = new();
            private readonly List<PartInstance> baseParts = new();
            private readonly MountPointAuthoring[] plugMounts = new MountPointAuthoring[4];
            private readonly PartDefinition plugDefinition;
            private readonly ToolDefinition wrench;
            private readonly ToolDefinition plugWrench;
            public readonly VehicleAssemblyController Assembly;
            public AssemblyGraph Graph => Assembly.Graph;

            public Fixture()
            {
                wrench = NewDefinition<ToolDefinition>();
                wrench.Configure("test.readiness.wrench", "Test wrench", "Wrench", FastenerSize.Millimeter10);
                plugWrench = SatsumaAuxiliaryAssemblyTools.CreateSparkPlugWrench();
                definitions.Add(plugWrench);
                PartInstance chassis = CreatePart(NewPartDefinition(Id("body-shell")), root, true);
                AddPart("engine-block", chassis);
                PartInstance block = parts["engine-block"];
                foreach (string suffix in PresenceOnlyStructure) AddPart(suffix, block);
                AddPart("cylinder-head", block);
                AddPart("distributor", block);
                for (int i = 1; i <= 4; i++) AddPart("piston" + i, block);
                foreach (string suffix in new[] { "fuel-tank", "fuel-strainer", "fuel-pump", "carburetor", "fuel-line", "airfilter" })
                    AddPart(suffix, block);
                plugDefinition = NewPartDefinition(SatsumaConsumableAssemblyRules.SparkPlugPartId);
                var allMounts = new List<MountPointAuthoring>(mounts.Values);
                for (int i = 0; i < 4; i++)
                {
                    plugMounts[i] = AddMount(SatsumaConsumableAssemblyRules.SparkPlugMountId(i + 1),
                        parts["cylinder-head"], plugDefinition, plugWrench);
                    allMounts.Add(plugMounts[i]);
                }
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(baseParts.ToArray(), allMounts.ToArray(), Array.Empty<AssemblyDependency>(),
                    new[] { wrench, plugWrench }, loose.transform);
                Assembly.ConfigureDynamicPartDefinitions(new[] { plugDefinition });
            }

            public static string Id(string suffix) => "vehicle.satsuma.part." + suffix;
            public MountPointRuntime Runtime(string suffix) => Runtime(mounts[suffix]);
            public MountPointRuntime PlugRuntime(int cylinder) => Runtime(plugMounts[cylinder - 1]);
            private MountPointRuntime Runtime(MountPointAuthoring mount)
            {
                Assert.That(Graph.TryGetMount(mount.MountId, out MountPointRuntime runtime), Is.True);
                return runtime;
            }
            public void InstallStructure(bool boltHeadAndDistributor = true)
            {
                foreach (string suffix in PresenceOnlyStructure) Install(suffix);
                Install("cylinder-head"); Install("distributor");
                if (boltHeadAndDistributor) { SetStage("cylinder-head", 2); SetStage("distributor", 2); }
            }
            public void InstallPistons() { for (int i = 1; i <= 4; i++) Install("piston" + i); }
            public void InstallFuel(bool boltCarb = true)
            {
                foreach (string suffix in new[] { "fuel-tank", "fuel-strainer", "fuel-pump", "carburetor", "timing-chain" }) Install(suffix);
                if (boltCarb) SetStage("carburetor", 2);
            }
            public void Install(string suffix) => Install(parts[suffix], mounts[suffix]);
            public void InstallPlug(PartInstance part, int cylinder) => Install(part, plugMounts[cylinder - 1]);
            private void Install(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            public void Remove(string suffix) => Remove(parts[suffix]);
            public void Remove(PartInstance part)
            {
                AssemblyOperationResult result = Assembly.TryRemove(part);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(part.IsInstalled, Is.False);
            }
            public void SetStage(string suffix, int stage) => SetStage(mounts[suffix], wrench, stage);
            public void SetPlugStage(int cylinder, int stage) => SetStage(plugMounts[cylinder - 1], plugWrench, stage);
            private void SetStage(MountPointAuthoring mount, ToolDefinition tool, int stage)
            {
                MountPointRuntime runtime = Runtime(mount);
                while (runtime.Fasteners[0].Stage != stage)
                {
                    AssemblyOperationResult result = Assembly.TryTurnFastener(mount.MountId,
                        runtime.Fasteners[0].Definition.DefinitionId, tool, runtime.Fasteners[0].Stage < stage
                            ? FastenerRotationDirection.Clockwise : FastenerRotationDirection.CounterClockwise);
                    Assert.That(result.Succeeded, Is.True, result.Message);
                }
            }
            public PartInstance[] PurchaseFourPlugs() => new[] { PurchasePlug(), PurchasePlug(), PurchasePlug(), PurchasePlug() };
            public PartInstance PurchasePlug(bool withCondition = true)
            {
                PartInstance part = CreatePart(plugDefinition, addBase: false);
                if (withCondition) part.gameObject.AddComponent<SatsumaReadinessItemConditionProbe>();
                Assert.That(Assembly.TryRegisterDynamicPart(part, "item.spark-plug", out string failure), Is.True, failure);
                return part;
            }
            public AssemblyVehiclePrerequisiteAdapter CreateAdapter()
            {
                var adapter = root.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
                adapter.Configure(Assembly);
                // This fixture checks individual structural/condition flags, not
                // electrical supply. An unpowered circuit must remain a failure.
                adapter.ConfigureSatsumaRequirements(root.AddComponent<SatsumaElectricalSystem>());
                return adapter;
            }
            public VehicleSimulationPrerequisites Evaluate(AssemblyVehiclePrerequisiteAdapter adapter)
            {
                var result = new VehicleSimulationPrerequisites();
                adapter.Evaluate(VehicleInputState.Neutral(true), ref result);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.StarterMissing), Is.True);
                return result;
            }
            private void AddPart(string suffix, PartInstance owner)
            {
                // FuelLine is a separate donor fastening record, not a claim
                // that the fixed roster contains a loose fuel-line item.
                string id = suffix == "fuel-line" ? "test.readiness.fuel-line-presentation" : Id(suffix);
                PartInstance part = CreatePart(NewPartDefinition(id));
                parts.Add(suffix, part);
                mounts.Add(suffix, AddMount("mount.readiness." + suffix, owner, part.Definition, wrench));
            }
            private MountPointAuthoring AddMount(string id, PartInstance owner, PartDefinition accepted, ToolDefinition tool)
            {
                var bolt = NewDefinition<FastenerDefinition>();
                bolt.Configure("fastener." + id, "Fixture thread", tool.Size, 8,
                    FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create(tool.ToolType, tool.Size));
                var definition = NewDefinition<MountPointDefinition>();
                definition.Configure(id, id, "readiness-fixture", owner.Definition.DefinitionId,
                    new[] { accepted.DefinitionId }, new MountConstraint(.2f, 45f, 1f, 0f), .03f, new[] { bolt });
                var group = new FastenerGroupDefinition();
                group.Configure(new[] { bolt.DefinitionId }, 8, 2, 0);
                definition.ConfigureFastenerGroup(group);
                var go = new GameObject(id); go.transform.SetParent(owner.transform, false);
                var mount = go.AddComponent<MountPointAuthoring>(); mount.Configure(definition, id, go.transform, 0);
                go.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }
            private PartDefinition NewPartDefinition(string id)
            {
                var definition = NewDefinition<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, .2f, null,
                    new[] { PartCompatibilityRule.Create("readiness-fixture") });
                return definition;
            }
            private T NewDefinition<T>() where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>(); definitions.Add(value); return value;
            }
            private PartInstance CreatePart(PartDefinition definition, GameObject existing = null, bool isRoot = false, bool addBase = true)
            {
                GameObject go = existing != null ? existing : new GameObject(definition.DefinitionId);
                if (!isRoot) go.transform.SetParent(loose.transform, false);
                var id = go.AddComponent<StableEntityIdAuthoring>(); id.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                var pickup = go.AddComponent<PhysicsPickupTarget>(); pickup.Configure(body, id, "fixture", 120f);
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, id, body, pickup, isRoot, string.Empty);
                if (addBase) baseParts.Add(part);
                return part;
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(loose);
                foreach (Object definition in definitions) if (definition != null) Object.DestroyImmediate(definition);
            }
        }
    }
}
