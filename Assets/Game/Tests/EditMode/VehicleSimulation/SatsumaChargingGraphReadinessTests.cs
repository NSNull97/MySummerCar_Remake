using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaChargingGraphReadinessTests
    {
        [Test]
        public void ChargingRequiresActualBatteryAlternatorAndBeltOccupancyNotBoltedLatch()
        {
            using var f = new Fixture();
            Assert.That(f.CanCharge(), Is.True);
            Assert.That(f.Assembly.Graph.IsPartDefinitionBolted("vehicle.satsuma.part.alternator"), Is.False);
            Assert.That(f.Assembly.Graph.IsPartDefinitionBolted("vehicle.satsuma.part.battery"), Is.False);
            foreach (string key in new[] { "battery", "alternator", "belt" })
            {
                f.Remove(key);
                Assert.That(f.CanCharge(), Is.False, "Loose " + key + " must not satisfy an installed-part gate.");
                f.Install(key);
                Assert.That(f.CanCharge(), Is.True, key);
            }
        }

        [Test]
        public void OnlyAlternatorAndRegulatorWiresAreChargingRequirementsNotCoilHarness()
        {
            using var f = new Fixture();
            Assert.That(f.Electrics.IsConnectionInstalled(SatsumaElectricalConnection.CoilHarness), Is.False);
            Assert.That(f.CanCharge(), Is.True);
            foreach (SatsumaElectricalConnection missing in new[]
                { SatsumaElectricalConnection.Alternator, SatsumaElectricalConnection.RegulatorHarness })
            {
                f.SetWires(missing == SatsumaElectricalConnection.Alternator
                    ? new[] { SatsumaElectricalConnection.RegulatorHarness }
                    : new[] { SatsumaElectricalConnection.Alternator });
                Assert.That(f.CanCharge(), Is.False, missing.ToString());
                f.SetWires(SatsumaElectricalConnection.Alternator, SatsumaElectricalConnection.RegulatorHarness);
                Assert.That(f.CanCharge(), Is.True);
            }
        }

        [Test]
        public void BeltConditionBoundaryIsLiveAndRejectsBrokenOrNonFiniteHealth()
        {
            using var f = new Fixture();
            int mutation = f.Assembly.GraphMutationCount;
            foreach (float rejected in new[] { 0f, .99f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                f.Condition.Set(rejected);
                Assert.That(f.CanCharge(), Is.False, "Remaining belt condition " + rejected);
            }
            f.Condition.Set(1f);
            Assert.That(f.CanCharge(), Is.True, "Donor Wear99 (remaining condition1) is still usable.");
            f.Condition.Set(100f, true);
            Assert.That(f.CanCharge(), Is.False);
            f.Condition.Set(100f);
            Assert.That(f.CanCharge(), Is.True);
            Assert.That(f.Assembly.GraphMutationCount, Is.EqualTo(mutation));
        }

        [Test]
        public void InstalledBeltWithoutItemConditionIsNotAssumedHealthy()
        {
            using var f = new Fixture();
            Object.DestroyImmediate(f.Condition);
            Assert.That(f.CanCharge(), Is.False);
            f.Parts["belt"].gameObject.AddComponent<SatsumaReadinessItemConditionProbe>();
            Assert.That(f.CanCharge(), Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("charging graph fixture");
            private readonly GameObject loose = new("charging loose parts");
            private readonly List<Object> definitions = new();
            private readonly Dictionary<string, MountPointAuthoring> mounts = new();
            public readonly Dictionary<string, PartInstance> Parts = new();
            public readonly VehicleAssemblyController Assembly;
            public readonly SatsumaElectricalSystem Electrics;
            public readonly SatsumaReadinessItemConditionProbe Condition;
            private readonly AssemblyVehiclePrerequisiteAdapter adapter;

            public Fixture()
            {
                var chassis = Part("vehicle.satsuma.part.body-shell", root, true);
                Parts.Add("block", Part(SatsumaEngineAssemblyReadiness.BlockId));
                Parts.Add("battery", Part(SatsumaElectricalSystem.BatteryPartDefinitionId));
                Parts.Add("alternator", Part("vehicle.satsuma.part.alternator"));
                Parts.Add("belt", Part(SatsumaConsumableAssemblyRules.BeltPartId));
                AddMount("block", "mount.charging.block", chassis, true);
                AddMount("battery", "mount.charging.battery", chassis, true);
                AddMount("alternator", SatsumaConsumableAssemblyRules.AlternatorMountId, Parts["block"], true);
                AddMount("belt", SatsumaConsumableAssemblyRules.BeltMountId, Parts["block"], false);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { chassis, Parts["block"], Parts["battery"], Parts["alternator"] },
                    mounts.Values.ToArray(), Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), loose.transform);
                Assembly.ConfigureDynamicPartDefinitions(new[] { Parts["belt"].Definition });
                Assert.That(Assembly.TryRegisterDynamicPart(Parts["belt"], "item.alternator-belt", out string failure), Is.True, failure);
                Condition = Parts["belt"].gameObject.AddComponent<SatsumaReadinessItemConditionProbe>();
                Transform visual = new GameObject("alternator presentation").transform;
                visual.SetParent(Parts["alternator"].transform, false);
                Parts["alternator"].gameObject.AddComponent<AssemblyEngineAdjustmentState>().Configure(
                    SatsumaEngineAdjustmentKind.Alternator, Parts["alternator"], Assembly, Vector3.zero, Vector3.up, 0f,
                    new[] { new AssemblyEngineAdjustmentPresentation(visual, Vector3.zero, Quaternion.identity) });
                adapter = root.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
                adapter.Configure(Assembly);
                Electrics = root.AddComponent<SatsumaElectricalSystem>();
                // A host reference is required by Electrical.Configure, but its
                // simulation is deliberately not initialized or ticked here.
                var hostObject = new GameObject("inactive charging host"); hostObject.SetActive(false);
                hostObject.transform.SetParent(root.transform, false);
                Electrics.Configure(Assembly, hostObject.AddComponent<VehicleSimulationHost>(), adapter,
                    Array.Empty<SatsumaElectricalConnectionBinding>());
                adapter.ConfigureSatsumaRequirements(Electrics);
                SetWires(SatsumaElectricalConnection.Alternator, SatsumaElectricalConnection.RegulatorHarness);
                Install("battery"); Install("alternator"); Install("belt");
            }

            public bool CanCharge()
            {
                var result = new VehicleSimulationPrerequisites();
                adapter.Evaluate(VehicleInputState.Neutral(true), ref result);
                return !result.HasAny(VehicleSimulationPrerequisiteFailure.AlternatorUnavailable);
            }
            public void SetWires(params SatsumaElectricalConnection[] connections)
            {
                Assert.That(Electrics.TryRestore(new SatsumaElectricalSaveDto
                    { installedConnectionIds = connections.Select(value => value.ToString()).ToArray() }, out string failure), Is.True, failure);
            }
            public void Install(string key)
            {
                PartInstance part = Parts[key]; Transform pose = mounts[key].Pose;
                part.transform.SetPositionAndRotation(pose.position, pose.rotation);
                part.Body.position = pose.position; part.Body.rotation = pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mounts[key]);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            public void Remove(string key)
            {
                AssemblyOperationResult result = Assembly.TryRemove(Parts[key]);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            private PartInstance Part(string id, GameObject existing = null, bool isRoot = false)
            {
                var definition = New<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1f, null, new[] { PartCompatibilityRule.Create("charging-fixture") });
                GameObject go = existing != null ? existing : new GameObject(id);
                if (!isRoot) go.transform.SetParent(loose.transform, false);
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, body, null, isRoot, string.Empty);
                return part;
            }
            private void AddMount(string key, string id, PartInstance owner, bool hasBolt)
            {
                FastenerDefinition[] bolts = Array.Empty<FastenerDefinition>();
                if (hasBolt)
                {
                    var bolt = New<FastenerDefinition>();
                    bolt.Configure("fastener." + id, "fixture bolt", FastenerSize.Millimeter10, 8,
                        FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
                    bolts = new[] { bolt };
                }
                var definition = New<MountPointDefinition>();
                definition.Configure(id, id, "charging-fixture", owner.Definition.DefinitionId,
                    new[] { Parts[key].Definition.DefinitionId }, new MountConstraint(.2f, 45f, 1f, 0f), 0f, bolts);
                definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(bolts));
                var go = new GameObject(id); go.transform.SetParent(owner.transform, false);
                var mount = go.AddComponent<MountPointAuthoring>(); mount.Configure(definition, id, go.transform, 0);
                go.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                mounts.Add(key, mount);
            }
            private T New<T>() where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>(); definitions.Add(value); return value;
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(loose);
                foreach (Object definition in definitions) if (definition != null) Object.DestroyImmediate(definition);
            }
        }
    }
}
