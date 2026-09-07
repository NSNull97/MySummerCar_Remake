using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class WheelFastenerRetentionCadencePlayModeTests
    {
        private static readonly MethodInfo EvaluateAt = typeof(VehicleAssemblyController).GetMethod(
            "EvaluateFastenerRetentionPoliciesAt", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AttemptTick = typeof(VehicleAssemblyController).GetField(
            "retentionPolicyTick", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<Object> ownedObjects = new();
        private float originalTimeScale;

        [SetUp]
        public void CaptureTimeScale()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator DestroyOwnedFixtures()
        {
            Time.timeScale = originalTimeScale;
            foreach (Object owned in ownedObjects)
                if (owned is GameObject gameObject && gameObject != null) gameObject.SetActive(false);
            foreach (Object owned in ownedObjects)
                if (owned != null) Object.Destroy(owned);
            ownedObjects.Clear();
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator FiftyTicksAndPausedRealtimeGapDoNotMultiplyAttemptsAcrossFourWheels()
        {
            Fixture fixture = CreateFixture();
            // T=32 still goes through zero-risk Chance -> Wait in the donor.
            // This makes the actual controller trial count deterministic.
            for (int tick = 0; tick < 50; tick++) Advance(fixture, 50f, tick * 0.02);
            Assert.That(Attempts(fixture), Is.EqualTo(4u));
            Advance(fixture, 50f, 1.0);
            Assert.That(Attempts(fixture), Is.EqualTo(8u));
            Assert.That(fixture.Mounts.All(value => value.IsOccupied), Is.True);

            Time.timeScale = 0f;
            yield return null;
            Advance(fixture, 50f, 100.0);
            Assert.That(Attempts(fixture), Is.EqualTo(12u),
                "One resumed opportunity per mount, not 99 missed opportunities.");
            for (int tick = 0; tick < 50; tick++) Advance(fixture, 50f, 100.0 + tick * 0.01);
            Assert.That(Attempts(fixture), Is.EqualTo(12u));
            Advance(fixture, 50f, 101.0);
            Assert.That(Attempts(fixture), Is.EqualTo(16u));
            Assert.That(fixture.Mounts.All(value => value.IsOccupied), Is.True);
        }

        [UnityTest]
        public IEnumerator WheelLoosenedInsideWaitBreaksOnlyAfterDeadlineAndDoesNotAffectOtherCorners()
        {
            Fixture fixture = CreateFixture();
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;
            Advance(fixture, 50f, 0.0);
            SetTightness(fixture.Mounts[2], 0);
            Advance(fixture, 50f, 0.99);
            Assert.That(Attempts(fixture), Is.EqualTo(4u));
            Assert.That(fixture.Mounts.All(value => value.IsOccupied), Is.True);
            Assert.That(actions, Is.Empty);

            Advance(fixture, 5f, 1.0);
            Assert.That(Attempts(fixture), Is.EqualTo(4u));
            Advance(fixture, 6f, 1.01);
            Assert.That(Attempts(fixture), Is.EqualTo(5u));
            Assert.That(fixture.Mounts[2].IsOccupied, Is.False);
            Assert.That(fixture.Parts[2].IsInstalled, Is.False);
            for (int i = 0; i < fixture.Mounts.Length; i++)
                if (i != 2) Assert.That(fixture.Mounts[i].IsOccupied, Is.True);
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartBrokenLoose));
            Assert.That(actions[0].Part, Is.SameAs(fixture.Parts[2]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReinstallRestoreAndDisableDoNotCarryAFormerRuntimeDeadline()
        {
            Fixture fixture = CreateFixture();
            Advance(fixture, 50f, 20.0);
            Assert.That(Attempts(fixture), Is.EqualTo(4u));
            SetTightness(fixture.Mounts[0], 0);
            AssertSuccess(fixture.Assembly.TryRemove(fixture.Parts[0]));
            PartInstance wheel = fixture.Parts[0];
            MountPointAuthoring mount = fixture.Mounts[0].Authoring;
            wheel.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            wheel.Body.position = mount.Pose.position;
            wheel.Body.rotation = mount.Pose.rotation;
            AssertSuccess(fixture.Assembly.TryInstall(wheel, mount));
            SetTightness(fixture.Mounts[0], 32);
            Advance(fixture, 50f, 20.1);
            Assert.That(Attempts(fixture), Is.EqualTo(5u),
                "Only the reinstalled wheel's cadence is fresh.");

            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
            uint beforeRestore = Attempts(fixture);
            AssertSuccess(fixture.Assembly.RestoreSaveData(saved));
            Advance(fixture, 50f, 20.2);
            Assert.That(Attempts(fixture), Is.EqualTo(beforeRestore + 4u));

            uint beforeDisable = Attempts(fixture);
            fixture.Assembly.enabled = false;
            fixture.Assembly.enabled = true;
            Advance(fixture, 50f, 20.3);
            Assert.That(Attempts(fixture), Is.EqualTo(beforeDisable + 4u),
                "OnDisable must reset schedules even without active install animations.");
            Assert.That(fixture.Mounts.All(value => value.IsOccupied), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitBreakPolicyCommandIsImmediateEvenWhileAutomaticScheduleWaits()
        {
            Fixture fixture = CreateFixture();
            Advance(fixture, 50f, 10.0);
            SetTightness(fixture.Mounts[1], 0);
            AssertSuccess(fixture.Assembly.TryApplyFastenerBreakPolicy(
                fixture.Mounts[1].MountId, 6f, 0u));
            Assert.That(fixture.Mounts[1].IsOccupied, Is.False);
            Assert.That(Attempts(fixture), Is.EqualTo(4u),
                "The explicit command is not another automatic random trial.");
            Advance(fixture, 50f, 10.1);
            Assert.That(Attempts(fixture), Is.EqualTo(4u));
            yield return null;
        }

        private static void AssertSuccess(AssemblyOperationResult result) =>
            Assert.That(result.Succeeded, Is.True, result.Message);

        private static void Advance(Fixture fixture, float speedKph, double realtimeSeconds)
        {
            Assert.That(EvaluateAt, Is.Not.Null, "Keep the controller's deterministic-time test seam.");
            EvaluateAt.Invoke(fixture.Assembly, new object[] { speedKph, realtimeSeconds });
        }

        private static uint Attempts(Fixture fixture)
        {
            Assert.That(AttemptTick, Is.Not.Null);
            return (uint)AttemptTick.GetValue(fixture.Assembly);
        }

        private Fixture CreateFixture()
        {
            // No generated car, NWH, contacts, or external scene objects are
            // needed to exercise the real assembly controller's timing policy.
            var root = new GameObject("Wheel cadence fixture");
            root.transform.position = new Vector3(100f, 100f, 100f);
            ownedObjects.Add(root);
            PartInstance body = CreatePart(root, "test.cadence.body", true, string.Empty);
            body.Body.constraints = RigidbodyConstraints.FreezeAll;
            var wheels = new PartInstance[4];
            var mounts = new MountPointAuthoring[4];
            string[] corners = { "fl", "fr", "rl", "rr" };
            for (int i = 0; i < corners.Length; i++)
            {
                string mountId = "test.cadence.mount." + corners[i];
                var owner = new GameObject("Wheel " + corners[i]);
                owner.transform.SetParent(root.transform, false);
                wheels[i] = CreatePart(owner, "test.cadence.wheel." + corners[i], false, mountId);
                mounts[i] = CreateMount(root.transform, mountId, wheels[i], i);
            }
            var assembly = root.AddComponent<VehicleAssemblyController>();
            assembly.Configure(new[] { body }.Concat(wheels).ToArray(), mounts,
                Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform,
                configuredRetentionSpeedSource: body.Body);
            var runtimeMounts = mounts.Select(assembly.ResolveMount).ToArray();
            foreach (MountPointRuntime mount in runtimeMounts) SetTightness(mount, 32);
            return new Fixture(assembly, wheels, runtimeMounts);
        }

        private PartInstance CreatePart(GameObject owner, string id, bool isRoot, string initialMount)
        {
            PartDefinition definition = NewDefinition<PartDefinition>();
            definition.Configure(id, id, PartCategory.Wheel, 1f, null,
                new[] { PartCompatibilityRule.Create("test.cadence.socket", "test.cadence.body") });
            var identity = owner.AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(StableEntityId.New());
            var body = owner.AddComponent<Rigidbody>();
            body.useGravity = false;
            var pickup = owner.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Test wheel", 35f, useGravityWhenLoose: false);
            var part = owner.AddComponent<PartInstance>();
            part.Configure(definition, identity, body, pickup, isRoot, initialMount);
            return part;
        }

        private MountPointAuthoring CreateMount(Transform root, string id, PartInstance part, int corner)
        {
            var fasteners = new FastenerDefinition[4];
            for (int i = 0; i < fasteners.Length; i++)
            {
                fasteners[i] = NewDefinition<FastenerDefinition>();
                fasteners[i].Configure(id + ".bolt." + i, "Wheel bolt", FastenerSize.Millimeter13,
                    8, FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter13));
            }
            MountPointDefinition definition = NewDefinition<MountPointDefinition>();
            definition.Configure(id, id, "test.cadence.socket", "test.cadence.body",
                new[] { part.Definition.DefinitionId }, new MountConstraint(0.1f, 30f, 1f, 0f),
                0f, fasteners);
            var group = new FastenerGroupDefinition();
            group.Configure(fasteners.Select(value => value.DefinitionId).ToArray(), 32, 1, 0,
                FastenerSpeedRetentionPolicy.DonorWheelBoltCheck, 5f, 33f, 100f,
                FastenerBreakAction.DetachInstalledPart);
            definition.ConfigureFastenerGroup(group);
            var mountObject = new GameObject(id);
            mountObject.transform.SetParent(root, false);
            mountObject.transform.localPosition = new Vector3(corner, 0f, 0f);
            var mount = mountObject.AddComponent<MountPointAuthoring>();
            mount.Configure(definition, id, mountObject.transform, 0);
            return mount;
        }

        private T NewDefinition<T>() where T : ScriptableObject
        {
            T definition = ScriptableObject.CreateInstance<T>();
            ownedObjects.Add(definition);
            return definition;
        }

        private static void SetTightness(MountPointRuntime mount, int tightness)
        {
            int remaining = tightness;
            foreach (FastenerInstance fastener in mount.Fasteners)
            {
                int stage = Mathf.Min(remaining, fastener.Definition.MaximumStage);
                Assert.That(fastener.TryRestore(true, true, stage), Is.True);
                remaining -= stage;
            }
            Assert.That(remaining, Is.Zero);
            mount.FastenerGroup.Reevaluate(true);
        }

        private sealed class Fixture
        {
            public Fixture(VehicleAssemblyController assembly, PartInstance[] parts, MountPointRuntime[] mounts)
            {
                Assembly = assembly;
                Parts = parts;
                Mounts = mounts;
            }
            public VehicleAssemblyController Assembly { get; }
            public PartInstance[] Parts { get; }
            public MountPointRuntime[] Mounts { get; }
        }
    }
}
