using System;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class DashboardTestBulbCondition : MonoBehaviour, IAssemblyItemCondition
    {
        public float Wear = 100f;
        public bool Broken;
        public float ConditionPercent => Wear;
        public bool IsBroken => Broken;
    }

    public sealed class SatsumaDashboardControlsTests
    {
        private Fixture f;
        [SetUp] public void SetUp() => f = new Fixture();
        [TearDown] public void TearDown() => f?.Dispose();

        [Test]
        public void RestoreIsQuietAtomicAndClearsTransientHold()
        {
            int requests = 0;
            f.Controls.ActionRequested += _ => requests++;
            Assert.That(f.Controls.TryRestore(new SatsumaDashboardControlsSaveDto
                { choke01 = .6f, headlightsMode = 2, hazardsOn = true }, out _), Is.True);
            Assert.That(f.Controls.Choke01, Is.EqualTo(.6f));
            Assert.That(f.Controls.TryRestore(new SatsumaDashboardControlsSaveDto
                { choke01 = float.NaN }, out _), Is.False);
            Assert.That(f.Controls.Choke01, Is.EqualTo(.6f));
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Headlights));
            Assert.That(f.Controls.TryRestore(null, out _), Is.True);
            Assert.That(f.Controls.Choke01, Is.Zero);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Off));
            Assert.That(f.Controls.HazardsOn, Is.False);
            Assert.That(f.Controls.ChokeHeldDirection, Is.Zero);
            Assert.That(requests, Is.Zero);
        }

        [TestCase(-.01f, 0, 1)]
        [TestCase(1.01f, 0, 1)]
        [TestCase(0f, -1, 1)]
        [TestCase(0f, 3, 1)]
        [TestCase(0f, 0, 2)]
        public void InvalidPayloadRejected(float choke, int mode, int schema) =>
            Assert.That(new SatsumaDashboardControlsSaveDto
                { choke01 = choke, headlightsMode = mode, schemaVersion = schema }.TryValidate(out _), Is.False);

        [Test]
        public void InstalledBoltedDashboardAllowsPhysicalControlsWithoutBattery()
        {
            Assert.That(f.Controls.TryCycleLights(), Is.False);
            f.InstallDashboard();
            Assert.That(f.Power.ElectricsOk, Is.False);
            Assert.That(f.Controls.CanOperate, Is.True);
            Assert.That(f.Controls.TryCycleLights(), Is.True);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Parking));
            Assert.That(f.Controls.TryCycleLights(), Is.True);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Headlights));
            Assert.That(f.Controls.TryCycleLights(), Is.True);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Off));
            Assert.That(f.Controls.TryToggleHazards(), Is.True);
        }

        [Test]
        public void ChokeUsesOwnTiltedAxisAndReleasesWithoutReturningToRest()
        {
            f.InstallDashboard();
            Vector3 rest = f.Controls.ChokeKnob.localPosition;
            Quaternion rotation = f.Controls.ChokeKnob.localRotation;
            Assert.That(f.Controls.TrySetChokeHeldDirection(1), Is.True);
            f.Controls.Simulate(.25f);
            float level = .25f * SatsumaDashboardControlsController.ChokeUnitsPerSecond;
            Assert.That(f.Controls.Choke01, Is.EqualTo(level).Within(.00001f));
            Assert.That(Vector3.Distance(f.Controls.ChokeKnob.localPosition,
                rest + rotation * new Vector3(0, -.03f * level, 0)), Is.LessThan(.00001f));
            f.Controls.TrySetChokeHeldDirection(0);
            f.Controls.Simulate(10f);
            Assert.That(f.Controls.Choke01, Is.EqualTo(level).Within(.00001f));
            f.Controls.TrySetChokeHeldDirection(-1);
            f.Controls.Simulate(1f);
            Assert.That(f.Controls.Choke01, Is.Zero);
        }

        [Test]
        public void PhysicalTargetsUseMouseHoldNotFAndToggleOnlyOnPress()
        {
            f.InstallDashboard();
            var context = new InteractionContext(f.Root, f.Root.transform.position, f.Root.transform.forward);
            var choke = f.Target(SatsumaDashboardControlKind.Choke);
            var lights = f.Target(SatsumaDashboardControlKind.Lights);
            var hazard = f.Target(SatsumaDashboardControlKind.Hazards);
            Assert.That(choke, Is.Not.InstanceOf<IContextInteractionTarget>());
            Assert.That(choke.UsesDirectionalHold, Is.True);
            Assert.That(choke.CanBeginContinuousInteraction(context, ContinuousContextInteractionDirection.Secondary), Is.True);
            Assert.That(lights.CanBeginContinuousInteraction(context, ContinuousContextInteractionDirection.Secondary), Is.False);
            Assert.That(hazard.CanBeginContinuousInteraction(context, ContinuousContextInteractionDirection.Secondary), Is.False);
            lights.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            lights.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            lights.ContinueContinuousInteraction(.1f);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Parking));
            lights.EndContinuousInteraction();
            lights.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            Assert.That(f.Controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Headlights));
            choke.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            f.Controls.Simulate(.1f);
            choke.EndContinuousInteraction();
            Assert.That(f.Controls.ChokeHeldDirection, Is.Zero);
            Assert.That(f.Controls.Choke01, Is.GreaterThan(0));
        }

        [Test]
        public void HeadlampsRequireTheirBulbHealthBoltsAndSideWiring()
        {
            f.InstallDashboard();
            f.InstallPart("headlight-left", true);
            f.InstallPart("headlight-right", true);
            f.PreparePower();
            f.Wire(SatsumaElectricalConnection.FrontLightsHarness, SatsumaElectricalConnection.SwitchLights,
                SatsumaElectricalConnection.HeadlightLeft);
            f.Controls.TryRestore(new SatsumaDashboardControlsSaveDto { headlightsMode = 2 }, out _);
            var left = f.Lamp("presentation.satsuma.headlight.left");
            var right = f.Lamp("presentation.satsuma.headlight.right");
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False, "An empty socket is not a working bulb.");
            var bulb = f.InstallBulb("left");
            Assert.That(f.Power.ElectricsOk, Is.True, "Installing a bulb must preserve the test's power circuit.");
            Assert.That(f.Assembly.Graph.TryGetMount(left.OwnerMountId, out var readyHeadMount), Is.True);
            Assert.That(readyHeadMount.Fasteners.Length, Is.GreaterThan(0),
                "Authored headlamp fastening is required: donor BeamsShort106670 reads Data.Bolted.");
            Assert.That(readyHeadMount.FastenerGroup.IsBolted, Is.True, "Headlamp mounting bolts must be tightened before light output.");
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.True);
            Assert.That(right.Light.enabled, Is.False);
            Assert.That(left.IsLensEmitting, Is.True);
            Assert.That(right.IsLensEmitting, Is.False);
            bulb.Wear = 6.99f;
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False);
            bulb.Wear = 7f;
            Assert.That(left.IsLensEmitting, Is.False);
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.True, "Source FloatCompare admits wear exactly 7.");
            Assert.That(left.IsLensEmitting, Is.True);
            bulb.Broken = true;
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False);
            bulb.Broken = false;
            Assert.That(left.IsLensEmitting, Is.False);
            f.Controls.TryRestore(new SatsumaDashboardControlsSaveDto { headlightsMode = 1 }, out _);
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False, "Selection 1 is parking lights, not dipped beams.");
            f.Controls.TryRestore(new SatsumaDashboardControlsSaveDto { headlightsMode = 2 }, out _);
            Assert.That(f.Assembly.Graph.TryGetMount(left.OwnerMountId, out var headMount), Is.True);
            foreach (var bolt in headMount.Fasteners) Assert.That(bolt.TryRestore(true, true, 0), Is.True);
            headMount.FastenerGroup.Reevaluate(true);
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False, "An unbolted headlamp is not enabled by wiring alone.");
            foreach (var bolt in headMount.Fasteners) Assert.That(bolt.TryRestore(true, true, bolt.Definition.MaximumStage), Is.True);
            headMount.FastenerGroup.Reevaluate(true);
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.True);
            f.Power.TryTurnFastener(SatsumaElectricalFastener.BatteryNegativeTerminal, -1f);
            f.Lighting.RefreshOutputs();
            Assert.That(left.Light.enabled, Is.False);
            Assert.That(left.IsLensEmitting, Is.False);
        }

        [Test]
        public void HazardBlinkUsesPointFourSecondPhasesAndEitherDashboardWire()
        {
            f.InstallDashboard();
            f.InstallPart("fender-left", true);
            f.InstallPart("rear-light-left", true);
            f.PreparePower();
            f.Wire(SatsumaElectricalConnection.HeadlightLeft, SatsumaElectricalConnection.FrontLightsHarness,
                SatsumaElectricalConnection.RearlightLeft);
            var front = f.Lamp("presentation.satsuma.hazard.front.left");
            var rear = f.Lamp("presentation.satsuma.hazard.rear.left");
            Assert.That(f.Controls.TryToggleHazards(), Is.True);
            f.Lighting.RefreshOutputs();
            Assert.That(front.Light.enabled, Is.False, "Dashboard wiring is still absent.");
            f.Wire(SatsumaElectricalConnection.Dash2);
            f.Lighting.RefreshOutputs();
            Assert.That(front.Light.enabled, Is.True);
            Assert.That(rear.Light.enabled, Is.True);
            f.Controls.Simulate(.399f); f.Lighting.RefreshOutputs();
            Assert.That(front.Light.enabled, Is.True);
            f.Controls.Simulate(.002f); f.Lighting.RefreshOutputs();
            Assert.That(front.Light.enabled, Is.False);
            Assert.That(rear.Light.enabled, Is.False);
            f.Controls.Simulate(.4f); f.Lighting.RefreshOutputs();
            Assert.That(front.Light.enabled, Is.True);
            f.Lighting.enabled = false;
            // Ordinary MonoBehaviours do not receive their Play lifecycle in
            // EditMode. Exercise this unit cleanup explicitly; the cabin
            // PlayMode fixture separately verifies a real disable callback.
            typeof(SatsumaDashboardLightingPresenter).GetMethod("OnDisable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(f.Lighting, null);
            Assert.That(front.Light.enabled, Is.False);
        }

        [Test]
        public void RefreshedContentHasThreeExactKnobsAndStableLampBindings()
        {
            Assert.That(f.Root.GetComponentsInChildren<SatsumaDashboardControlInteractionTarget>(true), Has.Length.EqualTo(3));
            Assert.That(f.Lighting.Lamps.Select(lamp => lamp.StableLampId).Distinct().Count(), Is.EqualTo(f.Lighting.Lamps.Length));
            Assert.That(f.Lighting.Lamps.Count(lamp => lamp.Function == SatsumaDashboardLampFunction.Headlights), Is.EqualTo(2));
            Assert.That(f.Lighting.Lamps.Count(lamp => lamp.Function == SatsumaDashboardLampFunction.Hazard), Is.EqualTo(4));
            Assert.That(Phase1SatsumaDashboardControlsAuthoring.ApplyToInstance(f.Assembly), Is.Zero);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly VehicleAssemblyController Assembly;
            public readonly SatsumaElectricalSystem Power;
            public readonly SatsumaDashboardControlsController Controls;
            public readonly SatsumaDashboardLightingPresenter Lighting;
            private readonly PartInstance[] parts;
            private readonly System.Collections.Generic.List<Object> extra = new();
            public Fixture()
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                Assert.That(prefab, Is.Not.Null);
                Root = Object.Instantiate(prefab);
                Assembly = Root.GetComponent<VehicleAssemblyController>();
                parts = Assembly.Parts.ToArray();
                Assembly.Initialize();
                Power = Root.GetComponent<SatsumaElectricalSystem>();
                Controls = Root.GetComponent<SatsumaDashboardControlsController>();
                Lighting = Root.GetComponent<SatsumaDashboardLightingPresenter>();
                Assert.That(Controls, Is.Not.Null, "Run the scoped dashboard/consumable refresh first.");
                Assert.That(Lighting, Is.Not.Null);
            }
            public void InstallDashboard() { InstallPart("dashboard", true); InstallPart("dashboard-meters", true); }
            public void InstallPart(string suffix, bool bolted)
            {
                var data = Assembly.CaptureSaveData();
                var part = data.parts.Single(value => value.partDefinitionId == "vehicle.satsuma.part." + suffix);
                var mount = Assembly.MountPoints.Single(value => value.Definition.AcceptedPartDefinitionIds.Contains(part.partDefinitionId));
                part.lifecycleState = PartLifecycleState.Installed; part.installedMountId = mount.MountId;
                part.worldPosition = mount.Pose.position; part.worldRotation = mount.Pose.rotation;
                data.mounts.Single(value => value.mountId == mount.MountId).installedPartStableEntityId = part.stableEntityId;
                foreach (var fastener in data.fasteners.Where(value => value.mountId == mount.MountId))
                {
                    fastener.inserted = true; fastener.seated = true;
                    fastener.stage = bolted ? mount.Definition.Fasteners.Single(value => value.DefinitionId == fastener.fastenerDefinitionId).MaximumStage : 0;
                }
                var group = data.fastenerGroups.SingleOrDefault(value => value.mountId == mount.MountId);
                if (group != null) group.isBolted = bolted && mount.Definition.Fasteners.Length > 0;
                var restored = Assembly.RestoreSaveData(data);
                Assert.That(restored.Succeeded, Is.True, restored.Message);
            }
            public void PreparePower()
            {
                Assert.That(Root.GetComponent<VehicleSimulationHost>().TryInitialize(out var failure), Is.True, failure);
                parts.Single(part => part.Definition.DefinitionId == SatsumaElectricalSystem.BatteryPartDefinitionId)
                    .RuntimeState.SetInstalled("test.battery", false);
                Wire(SatsumaElectricalConnection.BatteryHarness, SatsumaElectricalConnection.GroundBattery, SatsumaElectricalConnection.Ignition);
                for (int i = 0; i < 8; i++)
                {
                    Assert.That(Power.TryTurnFastener(SatsumaElectricalFastener.BatteryPositiveTerminal, 1), Is.True);
                    Assert.That(Power.TryTurnFastener(SatsumaElectricalFastener.BatteryNegativeTerminal, 1), Is.True);
                }
                Assert.That(Power.ElectricsOk, Is.True);
            }
            public void Wire(params SatsumaElectricalConnection[] wires)
            { foreach (var wire in wires) Assert.That(Power.TryInstallConnection(wire), Is.True); }
            public SatsumaDashboardControlInteractionTarget Target(SatsumaDashboardControlKind kind) =>
                Root.GetComponentsInChildren<SatsumaDashboardControlInteractionTarget>(true).Single(target => target.Kind == kind);
            public SatsumaDashboardLampBinding Lamp(string id) => Lighting.Lamps.Single(lamp => lamp.StableLampId == id);
            public DashboardTestBulbCondition InstallBulb(string side)
            {
                var mount = Assembly.MountPoints.Single(value => value.MountId == "mount.satsuma.headlight-" + side + ".light-bulb");
                var definition = ScriptableObject.CreateInstance<PartDefinition>(); extra.Add(definition);
                definition.Configure(mount.Definition.AcceptedPartDefinitionIds.Single(), "test bulb", PartCategory.Electrical,
                    .2f, null, new[] { PartCompatibilityRule.Create(mount.Definition.SocketType, mount.Definition.OwnerPartDefinitionId) });
                var go = new GameObject("test bulb"); extra.Add(go);
                var part = go.AddComponent<PartInstance>();
                var identity = go.GetComponent<StableEntityIdAuthoring>();
                Assert.That(StableEntityId.TryParse(Guid.NewGuid().ToString("N"), out var stable), Is.True);
                identity.InitializeExplicitRuntimeId(stable);
                part.Configure(definition, identity, null, null, false, "");
                var condition = go.AddComponent<DashboardTestBulbCondition>();
                Assert.That(Assembly.Graph.TryRegisterDynamicPart(part, out var failure), Is.True, failure);
                go.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                var result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
                return condition;
            }
            public void Dispose()
            {
                foreach (var part in parts) if (part != null && !part.transform.IsChildOf(Root.transform)) Object.DestroyImmediate(part.gameObject);
                foreach (var item in extra) if (item != null) Object.DestroyImmediate(item);
                Object.DestroyImmediate(Root);
            }
        }
    }
}
