using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineAdjustmentTests
    {
        [TestCase(SatsumaEngineAdjustmentKind.Alternator, 2f, 0.5f)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor, 15f, 0.2f)]
        [TestCase(SatsumaEngineAdjustmentKind.CarburetorMixture, 15f, 0.2f)]
        public void PositiveScrollUsesOneDonorDecreasingStep(SatsumaEngineAdjustmentKind kind, float initial, float step)
        {
            Assert.That(SatsumaEngineAdjustmentRules.InitialValue(kind), Is.EqualTo(initial));
            Assert.That(SatsumaEngineAdjustmentRules.TryStep(kind, initial, 99f, out float next), Is.True);
            Assert.That(next, Is.EqualTo(initial - step).Within(0.0001f));
            Assert.That(SatsumaEngineAdjustmentRules.TryStep(kind, next, -1f, out float previous), Is.True);
            Assert.That(previous, Is.EqualTo(initial).Within(0.0001f));
        }

        [TestCase(SatsumaEngineAdjustmentKind.Alternator, 0f, 8f)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor, 0f, 20f)]
        [TestCase(SatsumaEngineAdjustmentKind.CarburetorMixture, 10f, 22f)]
        public void DonorLimitsClampWithoutWraparound(SatsumaEngineAdjustmentKind kind, float min, float max)
        {
            SatsumaEngineAdjustmentRules.TryStep(kind, min, 1f, out float lower);
            SatsumaEngineAdjustmentRules.TryStep(kind, max, -1f, out float upper);
            Assert.That(lower, Is.EqualTo(min));
            Assert.That(upper, Is.EqualTo(max));
        }

        [TestCase(SatsumaEngineAdjustmentKind.Alternator)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor)]
        public void ClampLocksOnlyAtStageEightNotAtEarlierBoltedThreshold(SatsumaEngineAdjustmentKind kind)
        {
            using var f = new Fixture(kind);
            for (int stage = 0; stage <= 8; stage++)
            {
                Assert.That(f.Mount.Fasteners[0].TryRestore(true, true, stage), Is.True);
                f.Mount.FastenerGroup.Reevaluate(true);
                Assert.That(f.State.IsAvailable, Is.EqualTo(stage < 8), "Stage " + stage);
            }
        }

        [TestCase(SatsumaEngineAdjustmentKind.Alternator, true)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor, false)]
        [TestCase(SatsumaEngineAdjustmentKind.CarburetorMixture, false)]
        [TestCase(SatsumaEngineAdjustmentKind.OilFilter, false)]
        public void LoosePartsRespectDifferentDonorActivation(SatsumaEngineAdjustmentKind kind, bool expected)
        {
            using var f = new Fixture(kind, false);
            Assert.That(f.State.IsAvailable, Is.EqualTo(expected));
        }

        [Test]
        public void MixtureOnlyAcceptsTypedScrewdriverAndNeverFOrHandAdjustment()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.CarburetorMixture);
            Assert.That(f.Target.CanAdjust(default), Is.False);
            Assert.That(f.Target.CanActivateTool(default), Is.False);
            Assert.That(f.Target.TryAdjust(default, 1f), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(new Tool("Wrench", "6"), default, 1f), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(new Tool("Screwdriver", "1"), default, 1f), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(new Tool("Screwdriver", "0"), default, 1f), Is.True);
            Assert.That(f.State.Setting, Is.EqualTo(14.8f).Within(0.0001f));
            Assert.That(f.Target.GetHeldToolScrollPrompt(InteractionScrollDirection.Positive), Is.EqualTo("ОБЕДНИТЬ СМЕСЬ"));
            Assert.That(f.Target.GetHeldToolScrollPrompt(InteractionScrollDirection.Negative), Is.EqualTo("ОБОГАТИТЬ СМЕСЬ"));
        }

        [Test]
        public void OilFilterHasEightHandStagesAndAxialSeatingWithoutInventedBankRotation()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.OilFilter);
            Vector3 rootPosition = f.Part.transform.position;
            Quaternion rootRotation = f.Part.transform.rotation;
            for (int stage = 1; stage <= 8; stage++)
            {
                Assert.That(f.State.TryAdjust(1f), Is.True);
                Assert.That(f.State.Setting, Is.EqualTo(stage));
                Assert.That(f.State.BlocksRemoval, Is.True);
                Assert.That(f.Visual.localPosition.z, Is.EqualTo(-0.0025f * stage).Within(0.00001f));
            }
            Assert.That(f.State.TryAdjust(1f), Is.False);
            Assert.That(f.Visual.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(f.Part.transform.position, Is.EqualTo(rootPosition));
            Assert.That(f.Part.transform.rotation, Is.EqualTo(rootRotation));
            for (int stage = 0; stage < 8; stage++) Assert.That(f.State.TryAdjust(-1f), Is.True);
            Assert.That(f.State.BlocksRemoval, Is.False);
        }

        [Test]
        public void ForcedFilterDetachResetsHandTightness()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.OilFilter);
            f.State.TryAdjust(1f);
            f.State.RefreshPresentation();
            f.Part.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
            f.State.RefreshPresentation();
            Assert.That(f.State.Setting, Is.Zero);
            Assert.That(f.State.BlocksRemoval, Is.False);
            Assert.That(f.Visual.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void FilterRemovalGuardAndAssemblySaveRestoreUseLogicalHandTightness()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.OilFilter);
            Assert.That(f.Assembly.EvaluateRemoval(f.Part).Succeeded, Is.True);
            f.State.TryAdjust(1f);
            Assert.That(f.Assembly.EvaluateRemoval(f.Part).Succeeded, Is.False);
            VehicleAssemblySaveData data = f.Assembly.CaptureSaveData();
            PartSaveDto dto = data.parts.Single(value => value.partDefinitionId == f.Part.Definition.DefinitionId);
            Assert.That(dto.hasEngineAdjustment, Is.True);
            Assert.That(dto.engineAdjustment.value, Is.EqualTo(1f));
            f.State.TryAdjust(-1f);
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.State.BlocksRemoval, Is.True);
            dto.hasEngineAdjustment = false;
            dto.engineAdjustment = null;
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.State.Setting, Is.Zero);
            Assert.That(f.Assembly.EvaluateRemoval(f.Part).Succeeded, Is.True);
        }

        [TestCase(SatsumaEngineAdjustmentKind.Alternator)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor)]
        [TestCase(SatsumaEngineAdjustmentKind.CarburetorMixture)]
        [TestCase(SatsumaEngineAdjustmentKind.OilFilter)]
        public void OptionalVersionedStateRoundTripsAndMissingOldFieldUsesDonorDefault(SatsumaEngineAdjustmentKind kind)
        {
            using var f = new Fixture(kind);
            f.State.TryAdjust(1f);
            float savedValue = f.State.Setting;
            var dto = JsonUtility.FromJson<AssemblyEngineAdjustmentSaveDto>(JsonUtility.ToJson(f.State.CaptureSaveData()));
            f.State.TryAdjust(-1f);
            f.State.RestoreValidated(dto);
            Assert.That(f.State.Setting, Is.EqualTo(savedValue));
            Assert.That(dto.IsValidFor(kind), Is.True);
            f.State.RestoreValidated(null);
            Assert.That(f.State.Setting, Is.EqualTo(SatsumaEngineAdjustmentRules.InitialValue(kind)));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(8.5f)]
        public void InvalidFilterSaveRejectsBeforeChangingState(float value)
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.OilFilter);
            Assert.Throws<ArgumentException>(() => f.State.RestoreValidated(new AssemblyEngineAdjustmentSaveDto
                { kind = SatsumaEngineAdjustmentKind.OilFilter, value = value }));
            Assert.That(f.State.Setting, Is.Zero);
        }

        [Test]
        public void WrongKindOrFutureVersionRejectsRatherThanApplyingAnotherPartsSetting()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.Alternator);
            Assert.Throws<ArgumentException>(() => f.State.RestoreValidated(new AssemblyEngineAdjustmentSaveDto
                { kind = SatsumaEngineAdjustmentKind.Distributor, value = 5f }));
            Assert.Throws<ArgumentException>(() => f.State.RestoreValidated(new AssemblyEngineAdjustmentSaveDto
                { schemaVersion = 2, kind = SatsumaEngineAdjustmentKind.Alternator, value = 5f }));
            Assert.That(f.State.Setting, Is.EqualTo(2f));
        }

        [Test]
        public void AdjustmentNeverRotatesPhysicsPartOrChangesMountAndBoltStages()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.Distributor);
            f.Part.transform.rotation = Quaternion.Euler(87f, -34f, 72f);
            Quaternion before = f.Part.transform.rotation;
            f.State.TryAdjust(1f);
            Assert.That(f.Part.transform.rotation, Is.EqualTo(before));
            Assert.That(f.Mount.Fasteners[0].Stage, Is.Zero);
            Assert.That(f.Part.IsInstalled, Is.True);
            Assert.That(f.Visual.localEulerAngles.z, Is.EqualTo(14.8f).Within(0.01f));
        }

        [TestCase(false)] [TestCase(true)]
        public void ThrottleIsLmbHoldAndOnlyInstalledCarbRequestsRealInput(bool installed)
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.CarburetorMixture, installed);
            AssemblyCarburetorThrottleTarget throttle = f.CreateThrottle();
            Assert.That(throttle.CanBeginContinuousInteraction(default, ContinuousContextInteractionDirection.Secondary), Is.False);
            throttle.BeginContinuousInteraction(default, ContinuousContextInteractionDirection.Primary);
            Assert.That(throttle.IsHeld, Is.True);
            Assert.That(f.Visual.localEulerAngles.x, Is.EqualTo(40f).Within(0.01f));
            var adapter = f.Assembly.gameObject.AddComponent<SatsumaIgnitionInputAdapter>();
            adapter.ConfigureCarburetorThrottle(throttle);
            Assert.That(adapter.ConsumeFixedInput(0).Throttle01, Is.EqualTo(installed ? 1f : 0f));
            Assert.That(adapter.ConsumeFixedInput(0).StarterRequested, Is.False);
            throttle.EndContinuousInteraction();
            Assert.That(adapter.ConsumeFixedInput(0).Throttle01, Is.Zero);
            Assert.That(f.Visual.localEulerAngles.x, Is.Zero);
        }

        [Test]
        public void DetachOrWalkingAwayCancelsThrottleBeforeNextPhysicsInput()
        {
            using var f = new Fixture(SatsumaEngineAdjustmentKind.CarburetorMixture);
            AssemblyCarburetorThrottleTarget throttle = f.CreateThrottle();
            GameObject player = new GameObject("Throttle tester");
            try
            {
                var context = new InteractionContext(player, player.transform.position, Vector3.forward);
                throttle.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
                player.transform.position = Vector3.one * 10f;
                Assert.That(throttle.RequestedThrottle01, Is.Zero);
                Assert.That(throttle.IsHeld, Is.False);
                player.transform.position = Vector3.zero;
                throttle.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
                f.Part.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
                Assert.That(throttle.RequestedThrottle01, Is.Zero);
                Assert.That(throttle.IsHeld, Is.False);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void GeneratedBindingsHaveFourSettingsAndRetiredMixtureIsNotMountingFastener()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            var assembly = prefab.GetComponent<VehicleAssemblyController>();
            var states = prefab.GetComponentsInChildren<AssemblyEngineAdjustmentState>(true);
            Assert.That(states.Length, Is.EqualTo(4), "Run scoped engine adjustment refresh first.");
            foreach (var state in states)
            {
                Assert.That(state.Assembly, Is.SameAs(assembly));
                Assert.That(state.Part.Definition.DefinitionId, Is.EqualTo(SatsumaEngineAdjustmentRules.PartId(state.Kind)));
                Assert.That(state.Presentations.All(value => value.Target != state.Part.transform), Is.True);
            }
            Assert.That(prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Any(value =>
                value.FastenerDefinitionId == Phase1SatsumaEngineAdjustmentsAuthoring.ObsoleteMixtureFastenerId), Is.False);
            MountPointDefinition mount = assembly.MountPoints.Single(value => value.MountId ==
                SatsumaEngineAdjustmentRules.MountId(SatsumaEngineAdjustmentKind.CarburetorMixture)).Definition;
            Assert.That(mount.Fasteners.Length, Is.EqualTo(4));
            Assert.That(mount.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(32));
            Assert.That(mount.FastenerGroup.BoltedOnThreshold, Is.EqualTo(8));
            Assert.That(mount.FastenerGroup.BoltedOffThreshold, Is.Zero);
            var throttle = prefab.GetComponentInChildren<AssemblyCarburetorThrottleTarget>(true);
            Assert.That(throttle, Is.Not.Null);
            Assert.That(prefab.GetComponent<SatsumaIgnitionInputAdapter>().CarburetorThrottle, Is.SameAs(throttle));
        }

        private sealed class Tool : IHeldToolIdentity
        {
            public Tool(string type, string variant) { ToolType = type; ToolVariant = variant; }
            public string ToolType { get; }
            public string ToolVariant { get; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("Engine adjustment fixture");
            private readonly List<ScriptableObject> assets = new();
            public PartInstance Part { get; }
            public VehicleAssemblyController Assembly { get; }
            public MountPointRuntime Mount { get; }
            public AssemblyEngineAdjustmentState State { get; }
            public AssemblyEngineAdjustmentTarget Target { get; }
            public Transform Visual { get; }
            public Fixture(SatsumaEngineAdjustmentKind kind, bool installed = true)
            {
                string mountId = SatsumaEngineAdjustmentRules.MountId(kind);
                PartInstance body = CreatePart("test.body", true, "");
                Part = CreatePart(SatsumaEngineAdjustmentRules.PartId(kind), false, installed ? mountId : "");
                string clampId = SatsumaEngineAdjustmentRules.ClampFastenerId(kind);
                var fasteners = new List<FastenerDefinition>();
                if (clampId.Length > 0)
                {
                    var bolt = Asset<FastenerDefinition>();
                    bolt.Configure(clampId, "Clamp screw", FastenerSize.Millimeter6, 8,
                        FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Screwdriver", FastenerSize.None));
                    fasteners.Add(bolt);
                }
                var definition = Asset<MountPointDefinition>();
                definition.Configure(mountId, mountId, "test.socket", "test.body",
                    new[] { Part.Definition.DefinitionId }, new MountConstraint(.1f, 30f, 1f, 0f), 0f, fasteners.ToArray());
                definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(fasteners.ToArray()));
                var mountObject = new GameObject("Mount");
                mountObject.transform.SetParent(root.transform, false);
                var authoring = mountObject.AddComponent<MountPointAuthoring>();
                authoring.Configure(definition, mountId, mountObject.transform, 0);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                var screwdriver = Asset<ToolDefinition>();
                screwdriver.Configure("test.screwdriver", "Screwdriver", "Screwdriver", FastenerSize.None);
                Assembly.Configure(new[] { body, Part }, new[] { authoring }, Array.Empty<AssemblyDependency>(),
                    new[] { screwdriver }, root.transform);
                Mount = Assembly.ResolveMount(authoring);
                foreach (var fastener in Mount.Fasteners) fastener.TryRestore(installed, true, 0);
                Visual = new GameObject("Explicit visual").transform;
                Visual.SetParent(Part.transform, false);
                var renderer = Visual.gameObject.AddComponent<MeshRenderer>();
                State = Part.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
                State.Configure(kind, Part, Assembly, Vector3.zero, Vector3.forward, 0f,
                    new[] { new AssemblyEngineAdjustmentPresentation(Visual, Vector3.zero, Quaternion.identity) });
                State.RestoreValidated(null);
                var targetObject = new GameObject("Explicit adjustment");
                targetObject.transform.SetParent(Part.transform, false);
                targetObject.AddComponent<SphereCollider>().isTrigger = true;
                Target = targetObject.AddComponent<AssemblyEngineAdjustmentTarget>();
                Target.Configure(State, renderer);
            }
            public AssemblyCarburetorThrottleTarget CreateThrottle()
            {
                State.enabled = false;
                var target = Part.gameObject.AddComponent<AssemblyCarburetorThrottleTarget>();
                target.Configure(Part, Visual, null, null, Vector3.zero, Vector3.zero, Quaternion.identity);
                return target;
            }
            private PartInstance CreatePart(string id, bool isRoot, string mount)
            {
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                var definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = owner.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = owner.AddComponent<Rigidbody>();
                var pickup = owner.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, id, 35f);
                var part = owner.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, isRoot, mount);
                return part;
            }
            private T Asset<T>() where T : ScriptableObject
            {
                var value = ScriptableObject.CreateInstance<T>();
                assets.Add(value);
                return value;
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (var asset in assets) Object.DestroyImmediate(asset);
            }
        }
    }
}
