using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class AssemblyDynamicRestoreHandoffPlayModeTests
    {
        private readonly List<Object> ownedObjects = new List<Object>();

        [UnityTearDown]
        public IEnumerator DestroyOwnedFixtures()
        {
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
        public IEnumerator ValidMembershipRemovalCancelsHandoffAndReleasesItsReservation()
        {
            Fixture fixture = CreateFixture();
            PartInstance wrapper = fixture.Incoming;
            Rigidbody body = wrapper.Body;
            Vector3 originalPosition = body.position;
            Quaternion originalRotation = body.rotation;
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            AssemblyOperationResult started = fixture.Assembly.BeginInstallFromHandoff(wrapper.PickupTarget, fixture.Mount);
            Assert.That(started.Succeeded, Is.True, started.Message);
            Assert.That(wrapper.IsInstalled, Is.False, "Exercise the actual coroutine, not the immediate EditMode path.");
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.detectCollisions, Is.False);
            PartInstance probe = RegisterPart(fixture);
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(probe, fixture.Mount).FailureReason,
                Is.EqualTo(AssemblyFailureReason.MountOccupied));

            Assert.That(fixture.Assembly.TrySetDynamicPartRegistrationsForRestore(
                new[] { new DynamicPartRegistration(probe, Fixture.ItemDefinitionId) }, out string failure), Is.True, failure);
            Assert.That(wrapper.Body, Is.SameAs(body));
            Assert.That(wrapper.IsInstalled, Is.False);
            Assert.That(fixture.Assembly.Graph.TryGetPartByStableId(wrapper.StableId.Value, out _), Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.detectCollisions, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
            Assert.That(Vector3.Distance(body.position, originalPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(body.rotation, originalRotation), Is.LessThan(.001f));
            Assert.That(wrapper.PickupTarget.CanPickup(default), Is.True);
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(probe, fixture.Mount).Succeeded, Is.True,
                "A different registered part must be able to use the released reservation.");

            yield return new WaitForSecondsRealtime(VehicleAssemblyController.InstallTransitionDurationSeconds + .1f);

            Assert.That(fixture.Assembly.ResolveMount(fixture.Mount).IsOccupied, Is.False,
                "A stopped coroutine must not install its now-unregistered wrapper on a later frame.");
            Assert.That(wrapper.IsInstalled, Is.False);
            Assert.That(wrapper.Body, Is.SameAs(body));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.detectCollisions, Is.True);
            Assert.That(wrapper.PickupTarget.CanPickup(default), Is.True);
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(probe, fixture.Mount).Succeeded, Is.True);
            Assert.That(actions, Is.Empty, "Restore cancellation is not an ordinary installation action.");
        }

        [UnityTest]
        public IEnumerator InvalidMembershipReplacementLeavesAcceptedHandoffRunning()
        {
            Fixture fixture = CreateFixture();
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;
            Assert.That(fixture.Assembly.BeginInstallFromHandoff(fixture.Incoming.PickupTarget, fixture.Mount).Succeeded,
                Is.True);
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            Assert.That(fixture.Incoming.Body.isKinematic, Is.True);
            int mutationCount = fixture.Assembly.GraphMutationCount;
            var registration = new DynamicPartRegistration(fixture.Incoming, Fixture.ItemDefinitionId);

            Assert.That(fixture.Assembly.TrySetDynamicPartRegistrationsForRestore(
                new[] { registration, registration }, out _), Is.False,
                "Duplicate membership must fail before cancellation, body restoration or graph mutation.");
            Assert.That(fixture.Assembly.GraphMutationCount, Is.EqualTo(mutationCount));
            Assert.That(fixture.Incoming.Body.isKinematic, Is.True);
            Assert.That(fixture.Incoming.Body.detectCollisions, Is.False);
            Assert.That(fixture.Incoming.PickupTarget.CanPickup(default), Is.False);
            Assert.That(fixture.Assembly.Graph.TryGetPartByStableId(fixture.Incoming.StableId.Value,
                out PartInstance registered), Is.True);
            Assert.That(registered, Is.SameAs(fixture.Incoming));
            PartInstance probe = RegisterPart(fixture);
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(probe, fixture.Mount).FailureReason,
                Is.EqualTo(AssemblyFailureReason.MountOccupied), "The still-running handoff retains its reservation.");

            yield return new WaitForSecondsRealtime(VehicleAssemblyController.InstallTransitionDurationSeconds + .1f);

            Assert.That(fixture.Incoming.IsInstalled, Is.True);
            Assert.That(fixture.Assembly.ResolveMount(fixture.Mount).InstalledPart, Is.SameAs(fixture.Incoming));
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartInstalled));
            Assert.That(actions[0].Part, Is.SameAs(fixture.Incoming));
        }

        private Fixture CreateFixture()
        {
            var root = new GameObject("Dynamic restore handoff fixture");
            root.transform.position = new Vector3(100f, 100f, 100f);
            ownedObjects.Add(root);
            PartDefinition rootDefinition = Definition("test.handoff.body");
            PartInstance chassis = CreatePart(root, rootDefinition, true);
            PartDefinition consumable = Definition("test.handoff.consumable");
            var socket = new GameObject("Socket");
            socket.transform.SetParent(root.transform, false);
            var mountDefinition = NewDefinition<MountPointDefinition>();
            mountDefinition.Configure("test.handoff.mount", "socket", "test.handoff.socket", rootDefinition.DefinitionId,
                new[] { consumable.DefinitionId }, new MountConstraint(.05f, 30f, 1f, 0f),
                0f, Array.Empty<FastenerDefinition>());
            var mount = socket.AddComponent<MountPointAuthoring>();
            mount.Configure(mountDefinition, mountDefinition.DefinitionId, socket.transform, 0);
            var assembly = root.AddComponent<VehicleAssemblyController>();
            assembly.Configure(new[] { chassis }, new[] { mount }, Array.Empty<AssemblyDependency>(),
                Array.Empty<ToolDefinition>(), root.transform);
            assembly.ConfigureDynamicPartDefinitions(new[] { consumable });
            var fixture = new Fixture(root, assembly, mount, consumable);
            fixture.Incoming = RegisterPart(fixture);
            return fixture;
        }

        private PartInstance RegisterPart(Fixture fixture)
        {
            var owner = new GameObject("Purchased wrapper");
            owner.transform.SetParent(fixture.Root.transform, false);
            owner.transform.position = fixture.Mount.Pose.position + Vector3.right * .2f;
            PartInstance part = CreatePart(owner, fixture.ConsumableDefinition, false);
            Assert.That(fixture.Assembly.TryRegisterDynamicPart(part, Fixture.ItemDefinitionId, out string failure),
                Is.True, failure);
            Physics.SyncTransforms();
            return part;
        }

        private PartDefinition Definition(string id)
        {
            PartDefinition definition = NewDefinition<PartDefinition>();
            definition.Configure(id, id, PartCategory.Engine, 1f, null,
                new[] { PartCompatibilityRule.Create("test.handoff.socket", "test.handoff.body") });
            return definition;
        }

        private static PartInstance CreatePart(GameObject owner, PartDefinition definition, bool isRoot)
        {
            var identity = owner.AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(StableEntityId.New());
            var body = owner.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = isRoot;
            body.interpolation = RigidbodyInterpolation.None;
            if (!isRoot)
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                owner.AddComponent<BoxCollider>().size = Vector3.one * .02f;
            }
            var pickup = owner.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Test part", 35f, useGravityWhenLoose: false);
            var part = owner.AddComponent<PartInstance>();
            part.Configure(definition, identity, body, pickup, isRoot, string.Empty);
            return part;
        }

        private T NewDefinition<T>() where T : ScriptableObject
        {
            T definition = ScriptableObject.CreateInstance<T>();
            ownedObjects.Add(definition);
            return definition;
        }

        private sealed class Fixture
        {
            public const string ItemDefinitionId = "item.test-handoff-consumable";
            public Fixture(GameObject root, VehicleAssemblyController assembly, MountPointAuthoring mount,
                PartDefinition consumableDefinition)
            {
                Root = root; Assembly = assembly; Mount = mount; ConsumableDefinition = consumableDefinition;
            }
            public GameObject Root { get; }
            public VehicleAssemblyController Assembly { get; }
            public MountPointAuthoring Mount { get; }
            public PartDefinition ConsumableDefinition { get; }
            public PartInstance Incoming { get; set; }
        }
    }
}
