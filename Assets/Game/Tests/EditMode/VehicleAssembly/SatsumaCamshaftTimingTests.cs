using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaCamshaftTimingTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(4)] [TestCase(5)] [TestCase(7)]
        public void TimingAdjustmentCannotBypassNormalBoltStages(int stage)
        {
            using var f = new Fixture(stage);
            Assert.That(f.Timing.CanApply(f.Target), Is.False);
            Assert.That(f.Timing.TryApply(f.Target), Is.False);
            Assert.That(f.Target.CanActivateHeldTool(f.Key, default,
                InteractionScrollDirection.Positive), Is.True);
            Assert.That(f.Bolt.Stage, Is.EqualTo(stage));
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
        }

        [Test]
        public void ExtraPositiveTurnAdvancesMeshAndSavedSettingWithoutBoltOrRootMotion()
        {
            using var f = new Fixture(8);
            Vector3 position = f.Gear.transform.position;
            Quaternion rotation = f.Gear.transform.rotation;
            Assert.That(f.Target.GetHeldToolScrollPrompt(InteractionScrollDirection.Positive),
                Is.EqualTo("ВЫСТАВИТЬ МЕТКУ"));
            Assert.That(f.Target.CanActivateHeldTool(f.Key, default,
                InteractionScrollDirection.Positive), Is.True);
            Assert.That(f.Target.TryActivateHeldTool(f.Key, default, 1f), Is.True);
            Assert.That(f.Timing.AngleDegrees, Is.EqualTo(5f));
            Assert.That(f.Timing.CaptureSaveData().angleDegrees, Is.EqualTo(5f));
            Assert.That(Quaternion.Angle(f.Mesh.localRotation, Quaternion.Euler(5f, 0f, 0f)),
                Is.LessThan(0.01f));
            Assert.That(f.Bolt.Stage, Is.EqualTo(8));
            Assert.That(f.Mount.FastenerGroup.Tightness, Is.EqualTo(8f));
            Assert.That(f.Gear.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(f.Gear.transform.rotation, rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void InstalledChainBlocksAdjustmentButNegativeTurnStillLoosensBolt()
        {
            using var f = new Fixture(8, chainInstalled: true);
            Assert.That(f.Timing.CanApply(f.Target), Is.False);
            Assert.That(f.Target.CanActivateHeldTool(f.Key, default,
                InteractionScrollDirection.Positive), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(f.Key, default, 1f), Is.False);
            Assert.That(f.Target.CanActivateHeldTool(f.Key, default,
                InteractionScrollDirection.Negative), Is.True);
            Assert.That(f.Target.TryActivateHeldTool(f.Key, default, -1f), Is.True);
            Assert.That(f.Bolt.Stage, Is.EqualTo(7));
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
        }

        [Test]
        public void WrongToolAndOrdinaryUnboundFastenerHaveNoPostMaximumAction()
        {
            using var f = new Fixture(8);
            var wrong = new KeyIdentity("9");
            Assert.That(f.Target.CanActivateHeldTool(wrong, default,
                InteractionScrollDirection.Positive), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(wrong, default, 1f), Is.False);
            f.Target.ConfigurePostTighteningAction(null);
            Assert.That(f.Target.CanActivateHeldTool(f.Key, default,
                InteractionScrollDirection.Positive), Is.False);
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
        }

        [Test]
        public void InstalledSaveRoundTripAndMissingOldFieldPreserveDocumentedSettings()
        {
            using var f = new Fixture(8);
            f.Timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 75f });
            string json = JsonUtility.ToJson(f.Timing.CaptureSaveData());
            Assert.That(f.Timing.TryApply(f.Target), Is.True);
            f.Timing.RestoreValidated(JsonUtility.FromJson<AssemblyCamshaftTimingSaveDto>(json));
            Assert.That(f.Timing.AngleDegrees, Is.EqualTo(75f));
            Assert.That(Quaternion.Angle(f.Mesh.localRotation, Quaternion.Euler(75f, 0f, 0f)),
                Is.LessThan(0.01f));
            f.Timing.RestoreValidated(null);
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
            Assert.That(f.Timing.CaptureSaveData().IsValid, Is.True);
        }

        [TestCase(-1f)] [TestCase(361f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidSavedAngleRejectsBeforeChangingState(float angle)
        {
            using var f = new Fixture(8);
            Assert.Throws<ArgumentException>(() => f.Timing.RestoreValidated(
                new AssemblyCamshaftTimingSaveDto { angleDegrees = angle }));
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
        }

        [Test]
        public void LooseLoadUsesDonorDiscreteRandomRangeButOrdinaryPoseChangesDoNot()
        {
            using var f = new Fixture(8);
            UnityEngine.Random.State previous = UnityEngine.Random.state;
            try
            {
                f.Gear.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
                UnityEngine.Random.InitState(842);
                for (int i = 0; i < 64; i++)
                {
                    f.Timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 72f });
                    Assert.That(f.Timing.AngleDegrees, Is.InRange(0f, 350f));
                    Assert.That(f.Timing.AngleDegrees % 5f, Is.Zero);
                }
                float retained = f.Timing.AngleDegrees;
                f.Gear.transform.rotation = Quaternion.Euler(174f, 93f, 22f);
                f.Gear.RuntimeState.SetInstalled(AssemblyCamshaftTimingState.GearMountId, false);
                Assert.That(f.Timing.AngleDegrees, Is.EqualTo(retained),
                    "World/camera pose and ordinary installation cannot reroll timing.");
            }
            finally { UnityEngine.Random.state = previous; }
        }

        [Test]
        public void RepeatedAdjustmentRetainsLiveQuaternionAndDonorEulerReadout()
        {
            using var f = new Fixture(8);
            f.Timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 85f });
            Quaternion expected = Quaternion.Euler(85f, 0f, 0f);
            for (int i = 0; i < 72; i++)
            {
                expected *= Quaternion.Euler(5f, 0f, 0f);
                Assert.That(f.Timing.TryApply(f.Target), Is.True);
                Assert.That(Quaternion.Angle(f.Mesh.localRotation, expected), Is.LessThan(0.01f));
                Assert.That(f.Timing.AngleDegrees, Is.EqualTo(Mathf.RoundToInt(expected.eulerAngles.x)));
            }
            Assert.That(f.Bolt.Stage, Is.EqualTo(8));
        }

        [Test]
        public void AssemblySaveRoundTripCarriesOptionalTimingWithoutChangingOtherParts()
        {
            using var f = new Fixture(8);
            f.Timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 35f });
            VehicleAssemblySaveData data = f.Assembly.CaptureSaveData();
            PartSaveDto gear = data.parts.Single(value => value.partDefinitionId ==
                AssemblyCamshaftTimingState.PartDefinitionId);
            Assert.That(gear.hasCamshaftTiming, Is.True);
            Assert.That(gear.camshaftTiming.angleDegrees, Is.EqualTo(35f));
            Assert.That(data.parts.Where(value => value != gear).All(value => !value.hasCamshaftTiming), Is.True);
            f.Timing.TryApply(f.Target);
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.Timing.AngleDegrees, Is.EqualTo(35f));
            gear.hasCamshaftTiming = false;
            gear.camshaftTiming = null;
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.Timing.AngleDegrees, Is.Zero);
        }

        [Test]
        public void GeneratedGearHasExactMeshBindingToolTargetAndDonorThreshold()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            VehicleAssemblyController assembly = prefab.GetComponent<VehicleAssemblyController>();
            PartInstance gear = assembly.Parts.Single(value => value.Definition.DefinitionId ==
                AssemblyCamshaftTimingState.PartDefinitionId);
            AssemblyCamshaftTimingState timing = gear.GetComponent<AssemblyCamshaftTimingState>();
            Assert.That(timing, Is.Not.Null, "Run scoped camshaft-timing refresh first.");
            Assert.That(timing.Part, Is.SameAs(gear));
            Assert.That(timing.Assembly, Is.SameAs(assembly));
            Assert.That(timing.GearMesh.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>(Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot +
                    "/Meshes/" + Phase1SatsumaCamshaftTimingAuthoring.GearMeshSourceGuid + ".asset")));
            AssemblyFastenerInteractionTarget target = prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Single(value => value.MountId == AssemblyCamshaftTimingState.GearMountId &&
                    value.FastenerDefinitionId == AssemblyCamshaftTimingState.GearFastenerId);
            Assert.That(target.PostTighteningAction, Is.SameAs(timing));
            MountPointDefinition mount = assembly.MountPoints.Single(value =>
                value.MountId == AssemblyCamshaftTimingState.GearMountId).Definition;
            Assert.That(mount.FastenerGroup.BoltedOnThreshold, Is.EqualTo(5));
            Assert.That(mount.FastenerGroup.BoltedOffThreshold, Is.Zero);
            Assert.That(mount.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(8));
        }

        private sealed class KeyIdentity : IHeldToolIdentity
        {
            public KeyIdentity(string size) { ToolVariant = size; }
            public string ToolType => "Wrench";
            public string ToolVariant { get; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Camshaft timing fixture");
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            public Fixture(int stage, bool chainInstalled = false)
            {
                PartInstance body = Part("test.body", true, "");
                Gear = Part(AssemblyCamshaftTimingState.PartDefinitionId, false,
                    AssemblyCamshaftTimingState.GearMountId);
                PartInstance chain = Part("test.chain", false,
                    chainInstalled ? AssemblyCamshaftTimingState.ChainMountId : "");
                var bolt = Asset<FastenerDefinition>();
                bolt.Configure(AssemblyCamshaftTimingState.GearFastenerId, "Gear bolt",
                    FastenerSize.Millimeter10, 8, FastenerDirection.ClockwiseToTighten,
                    true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
                MountPointAuthoring gearMount = CreateMount(AssemblyCamshaftTimingState.GearMountId,
                    Gear, new[] { bolt });
                MountPointAuthoring chainMount = CreateMount(AssemblyCamshaftTimingState.ChainMountId,
                    chain, Array.Empty<FastenerDefinition>());
                var tool = Asset<ToolDefinition>();
                tool.Configure("test.key10", "Key10", "Wrench", FastenerSize.Millimeter10);
                var assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly = assembly;
                assembly.Configure(new[] { body, Gear, chain }, new[] { gearMount, chainMount },
                    Array.Empty<AssemblyDependency>(), new[] { tool }, root.transform);
                Mount = assembly.ResolveMount(gearMount);
                Bolt = Mount.Fasteners[0];
                Assert.That(Bolt.TryRestore(true, true, stage), Is.True);
                Mount.FastenerGroup.Reevaluate(true);
                Mesh = new GameObject("Explicit gear mesh").transform;
                Mesh.SetParent(Gear.transform, false);
                Timing = Gear.gameObject.AddComponent<AssemblyCamshaftTimingState>();
                Timing.Configure(Gear, assembly, Mesh, Quaternion.identity);
                Timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 0f });
                var targetObject = new GameObject("Exact gear bolt target");
                targetObject.transform.SetParent(root.transform, false);
                Target = targetObject.AddComponent<AssemblyFastenerInteractionTarget>();
                Target.Configure(assembly, AssemblyCamshaftTimingState.GearMountId,
                    AssemblyCamshaftTimingState.GearFastenerId, tool, false);
                Target.ConfigurePostTighteningAction(Timing);
            }

            public PartInstance Gear { get; }
            public VehicleAssemblyController Assembly { get; }
            public Transform Mesh { get; }
            public AssemblyCamshaftTimingState Timing { get; }
            public AssemblyFastenerInteractionTarget Target { get; }
            public MountPointRuntime Mount { get; }
            public FastenerInstance Bolt { get; }
            public IHeldToolIdentity Key { get; } = new KeyIdentity("10");

            private PartInstance Part(string id, bool isRoot, string mount)
            {
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                PartDefinition definition = Asset<PartDefinition>();
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

            private MountPointAuthoring CreateMount(string id, PartInstance part, FastenerDefinition[] fasteners)
            {
                var definition = Asset<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", "test.body",
                    new[] { part.Definition.DefinitionId }, new MountConstraint(0.1f, 30f, 1f, 0f),
                    0f, fasteners);
                if (fasteners.Length > 0)
                {
                    var group = new FastenerGroupDefinition();
                    group.Configure(new[] { fasteners[0].DefinitionId }, 8, 5, 0);
                    definition.ConfigureFastenerGroup(group);
                }
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                var mount = owner.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, owner.transform, 0);
                return mount;
            }

            private T Asset<T>() where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>();
                assets.Add(value);
                return value;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset);
            }
        }
    }
}
