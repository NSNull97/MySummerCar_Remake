using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
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
    public sealed class SatsumaServiceCapTests
    {
        [Test]
        public void CapNeedsElevenTurnsToOpen_AndOnePositiveNotchCloses()
        {
            float angle = 359f;
            for (int i = 0; i < 10; i++)
            { Assert.That(AssemblyServiceCapState.TryStep(angle, -900f, out angle), Is.True); Assert.That(angle, Is.GreaterThan(1f)); }
            Assert.That(AssemblyServiceCapState.TryStep(angle, -1f, out angle), Is.True);
            Assert.That(angle, Is.EqualTo(1f));
            Assert.That(AssemblyServiceCapState.TryStep(angle, -1f, out _), Is.False);
            Assert.That(AssemblyServiceCapState.TryStep(angle, 1f, out angle), Is.True);
            Assert.That(angle, Is.EqualTo(34f));
            Assert.That(AssemblyServiceCapState.TryStep(angle, float.NaN, out _), Is.False);
        }

        [Test]
        public void TwoCapsPersistIndependently_OldSaveDefaultsClosed_WithoutAliasing()
        {
            using var f = new Fixture();
            f.Open(0); Assert.That(f.Caps.IsOpen(0), Is.True); Assert.That(f.Caps.IsOpen(1), Is.False);
            VehicleAssemblySaveData save = f.Assembly.CaptureSaveData();
            PartSaveDto part = save.parts.Single(value => value.hasServiceCaps);
            Assert.That(part.serviceCaps.angles, Is.EqualTo(new[] { 1f, 359f }));
            f.Open(1); Assert.That(part.serviceCaps.angles[1], Is.EqualTo(359f));
            Assert.That(f.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            part.serviceCaps.angles[0] = 200f;
            Assert.That(f.Caps.Angle(0), Is.EqualTo(1f));
            part.hasServiceCaps = false;
            Assert.That(f.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(f.Caps.Angle(0), Is.EqualTo(359f));
            Assert.That(f.Caps.Angle(1), Is.EqualTo(359f));
        }

        [TestCase("nan")] [TestCase("unknown")] [TestCase("duplicate")]
        [TestCase("reorder")] [TestCase("wrong-part")] [TestCase("missing")]
        public void BadCapsRejectBeforeAnyStateMutation(string corrupt)
        {
            using var f = new Fixture(); f.Open(0);
            VehicleAssemblySaveData save = f.Assembly.CaptureSaveData();
            string before = JsonUtility.ToJson(save);
            PartSaveDto part = save.parts.Single(value => value.hasServiceCaps);
            switch (corrupt)
            {
                case "nan": part.serviceCaps.angles[0] = float.NaN; break;
                case "unknown": part.serviceCaps.kinds[0] = (SatsumaServiceCapKind)99; break;
                case "duplicate": part.serviceCaps.kinds[1] = part.serviceCaps.kinds[0]; break;
                case "reorder": Array.Reverse(part.serviceCaps.kinds); break;
                case "wrong-part":
                    PartSaveDto other = save.parts.Single(value => !value.hasServiceCaps);
                    other.hasServiceCaps = true; other.serviceCaps = part.serviceCaps.Clone(); break;
                case "missing": part.serviceCaps = null; break;
            }
            Assert.That(f.Assembly.RestoreSaveData(save).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void CapInteractionRotatesOnlyRenderLeaf_NoFAndNoPausedTurns()
        {
            using var f = new Fixture(); Vector3 position = f.Part.transform.position;
            Quaternion rotation = f.Part.transform.rotation;
            Assert.That(f.Target.CanActivateTool(default), Is.False);
            Assert.That(f.Target.TryAdjust(default, -1f), Is.True);
            Assert.That(Quaternion.Angle(f.Renderer.transform.localRotation, Quaternion.identity), Is.EqualTo(33f).Within(.001f));
            Assert.That(f.Part.transform.position, Is.EqualTo(position)); Assert.That(f.Part.transform.rotation, Is.EqualTo(rotation));
            float oldScale = Time.timeScale;
            try { Time.timeScale = 0f; Assert.That(f.Target.TryAdjust(default, -1f), Is.False); }
            finally { Time.timeScale = oldScale; }
            f.Open(0); f.Target.RefreshPresentation(); Assert.That(f.Renderer.enabled, Is.False);
            f.Caps.RestoreValidated(null); f.Target.RefreshPresentation(); Assert.That(f.Renderer.enabled, Is.True);
        }

        [Test]
        public void PassiveReceiverRejectsClosedAndWrongFluid_ClampsActualReservoir_AndDoesNotFillAnotherCircuit()
        {
            using var f = new Fixture();
            Assert.That(f.Receiver is ILiquidContainerTarget, Is.True);
            Assert.That((object)f.Receiver is ILiquidReceiverTarget, Is.False, "No instant held F fill path.");
            Assert.That(f.Receiver.TryAcceptLiquid("liquid.brake-fluid", 1f, out _), Is.False);
            f.Open(0);
            Assert.That(f.Receiver.TryAcceptLiquid("liquid.motor-oil", 1f, out _), Is.False);
            Assert.That(f.Receiver.TryAcceptLiquid("liquid.brake-fluid", float.PositiveInfinity, out _), Is.False);
            Assert.That(f.Receiver.TryAcceptLiquid("liquid.brake-fluid", 2f, out float accepted), Is.True);
            Assert.That(accepted, Is.EqualTo(1f)); Assert.That(f.Receiver.ContentLiters, Is.EqualTo(1f));
            Assert.That(f.Host.State.GetServiceFluidLiters(SatsumaServiceFluid.BrakeRear), Is.Zero);
            Assert.That(f.Receiver.TryAcceptLiquid("liquid.brake-fluid", .1f, out _), Is.False);
            VehicleSimulationStateDto saved = f.Host.State.CaptureDto();
            Assert.That(f.Host.Root.TryRestoreState(saved, out _), Is.True);
            Assert.That(f.Receiver.ContentLiters, Is.EqualTo(1f));
        }

        [Test]
        public void CanonicalHasFiveUniqueCapsAndPassiveReceivers_AndAuthoringIsIdempotent()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var targets = root.GetComponentsInChildren<AssemblyServiceCapTarget>(true);
                Assert.That(targets, Has.Length.EqualTo(5));
                Assert.That(targets.Select(value => value.State.Kind(value.CapIndex)), Is.EquivalentTo(Enum.GetValues(typeof(SatsumaServiceCapKind))));
                Assert.That(targets.Select(value => value.CapRenderer).Distinct().Count(), Is.EqualTo(5));
                Assert.That(root.GetComponentsInChildren<SatsumaServiceFluidReceiver>(true), Has.Length.EqualTo(5));
                Assert.That(Phase1SatsumaServiceCapsAuthoring.ApplyToInstance(root.GetComponent<VehicleAssemblyController>()), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("Service caps test");
            private readonly List<ScriptableObject> assets = new();
            public PartInstance Part { get; }
            public VehicleAssemblyController Assembly { get; }
            public AssemblyServiceCapState Caps { get; }
            public AssemblyServiceCapTarget Target { get; }
            public Renderer Renderer { get; }
            public SatsumaServiceFluidReceiver Receiver { get; }
            public VehicleSimulationHost Host { get; }
            public Fixture()
            {
                root.SetActive(false);
                PartInstance body = MakePart("test.body", true, "");
                Part = MakePart("vehicle.satsuma.part.brake-master-cylinder", false, "test.cap.mount");
                var definition = Asset<MountPointDefinition>();
                definition.Configure("test.cap.mount", "Master", "test.socket", "test.body", new[] { Part.Definition.DefinitionId },
                    new MountConstraint(.1f, 30f, 1f, 0f), 0f, Array.Empty<FastenerDefinition>());
                var mountObject = new GameObject("Mount"); mountObject.transform.SetParent(root.transform, false);
                var mount = mountObject.AddComponent<MountPointAuthoring>(); mount.Configure(definition, "test.cap.mount", mountObject.transform, 0);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, Part }, new[] { mount }, Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
                Caps = Part.gameObject.AddComponent<AssemblyServiceCapState>(); Caps.Configure(Part, SatsumaServiceCapKind.BrakeFront, SatsumaServiceCapKind.BrakeRear);
                var visual = new GameObject("Separate cap"); visual.transform.SetParent(Part.transform, false); Renderer = visual.AddComponent<MeshRenderer>();
                var control = new GameObject("Cap target"); control.transform.SetParent(Part.transform, false); control.AddComponent<SphereCollider>().isTrigger = true;
                Target = control.AddComponent<AssemblyServiceCapTarget>(); Target.Configure(Caps, 0, Renderer);
                Host = root.AddComponent<VehicleSimulationHost>();
                var prerequisites = root.AddComponent<AssemblyVehiclePrerequisiteAdapter>(); prerequisites.Configure(Assembly);
                var config = Asset<VehicleSimulationConfig>(); config.ApplyProvisionalPrototypeDefaults();
                Host.Configure(config, root.AddComponent<CapTestWheels>(), prerequisites, null);
                Host.ConfigureSatsumaOperatingSource(root.AddComponent<CapTestSource>());
                Assert.That(Host.TryInitialize(out string failure), Is.True, failure);
                Receiver = control.AddComponent<SatsumaServiceFluidReceiver>(); Receiver.Configure(Host, Caps, 0);
                // EditMode fixtures have no player loop/Awake; enable only the cap
                // components' local hierarchy, with the host disabled to avoid ticks.
                Host.enabled = false; root.SetActive(true);
            }
            public void Open(int index) { for (int i = 0; i < 11; i++) Caps.TryAdjust(index, -1f); }
            private PartInstance MakePart(string id, bool isRoot, string mount)
            {
                var owner = new GameObject(id); owner.transform.SetParent(root.transform, false);
                var definition = Asset<PartDefinition>(); definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = owner.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = owner.AddComponent<Rigidbody>(); var pickup = owner.AddComponent<PhysicsPickupTarget>(); pickup.Configure(body, identity, id, 35f);
                var part = owner.AddComponent<PartInstance>(); part.Configure(definition, identity, body, pickup, isRoot, mount); return part;
            }
            private T Asset<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
            public void Dispose() { Object.DestroyImmediate(root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }

    public sealed class CapTestSource : MonoBehaviour, ISatsumaOperatingConditionSource
    { public SatsumaOperatingInputs CaptureConditions() => default; public void ApplyWear(in SatsumaWearDelta wear) { } }
    public sealed class CapTestWheels : MonoBehaviour, IWheelPhysicsBackend
    {
        public int WheelCount => 4;
        public float VehicleSpeedMetersPerSecond => 0f;
        public void Sample(float dt, WheelPhysicsSample[] samples)
        { for (int i = 0; i < samples.Length; i++) samples[i] = WheelPhysicsSample.NoContact; }
        public void Apply(float dt, WheelPhysicsCommand[] commands) { }
        public void Reset() { }
    }
}
