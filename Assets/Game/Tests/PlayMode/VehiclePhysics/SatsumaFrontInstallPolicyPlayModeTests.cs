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

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaFrontInstallPolicyPlayModeTests
    {
        private readonly List<Object> ownedObjects = new();

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
        public IEnumerator UnboltedSupportCompletesRealAnimationThenCollapsesOnBothCorners()
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                Fixture fixture = CreateFixture(corner);
                Vector3 initialPosition = fixture.Incoming.Body.position;
                var actions = new List<AssemblyActionCompleted>();
                bool incomingWasInstalledBeforeCollapse = false;
                fixture.Assembly.ActionCompleted += action =>
                {
                    actions.Add(action);
                    if (action.Action == AssemblyActionKind.PartInstalled)
                    {
                        incomingWasInstalledBeforeCollapse = fixture.Incoming.IsInstalled &&
                            fixture.Support.IsInstalled &&
                            fixture.Assembly.ResolveMount(fixture.Target).InstalledPart == fixture.Incoming;
                        Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                            Is.LessThan(0.0001f));
                    }
                };

                AssemblyOperationResult result = fixture.Assembly.BeginInstallFromHandoff(
                    fixture.Pickup, fixture.Target);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(fixture.Incoming.Body.isKinematic, Is.True,
                    "The incoming item must enter the real handoff even while support is unbolted.");
                Assert.That(fixture.Incoming.IsInstalled, Is.False);
                Assert.That(fixture.Support.IsInstalled, Is.True);
                Assert.That(actions, Is.Empty, "Collapse is a post-install outcome, not a pre-animation rejection.");

                yield return new WaitForSecondsRealtime(
                    VehicleAssemblyController.InstallTransitionDurationSeconds + 0.1f);

                Assert.That(fixture.Assembly.LastOperationResult.Succeeded, Is.True);
                Assert.That(incomingWasInstalledBeforeCollapse, Is.True);
                Assert.That(Vector3.Distance(fixture.Incoming.Body.position, initialPosition), Is.GreaterThan(0.1f));
                Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                    Is.LessThan(0.0001f), "Collapsed item stays at its completed mount pose, not at the old carry origin.");
                Assert.That(fixture.Incoming.IsInstalled, Is.False);
                Assert.That(fixture.Support.IsInstalled, Is.False);
                Assert.That(fixture.Incoming.Body.isKinematic, Is.False);
                Assert.That(fixture.Assembly.ResolveMount(fixture.Target).IsOccupied, Is.False);
                AssertInstalledThenCollapsed(actions, fixture);
            }
        }

        [UnityTest]
        public IEnumerator BoltedSupportCompletesRealAnimationAndInstallsOnceOnBothCorners()
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                Fixture fixture = CreateFixture(corner);
                SetSupportStage(fixture, 2);
                var actions = new List<AssemblyActionCompleted>();
                fixture.Assembly.ActionCompleted += actions.Add;

                Assert.That(fixture.Assembly.BeginInstallFromHandoff(fixture.Pickup, fixture.Target).Succeeded,
                    Is.True);
                Assert.That(fixture.Incoming.IsInstalled, Is.False,
                    "PlayMode must exercise the asynchronous transition, not the EditMode shortcut.");
                Assert.That(fixture.Incoming.Body.isKinematic, Is.True);
                Assert.That(actions, Is.Empty);

                yield return new WaitForSecondsRealtime(
                    VehicleAssemblyController.InstallTransitionDurationSeconds + 0.1f);

                Assert.That(fixture.Incoming.IsInstalled, Is.True);
                Assert.That(fixture.Support.IsInstalled, Is.True);
                Assert.That(fixture.Assembly.ResolveMount(fixture.Target).InstalledPart,
                    Is.SameAs(fixture.Incoming));
                Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                    Is.LessThan(0.0001f));
                Assert.That(actions.Count, Is.EqualTo(1));
                Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartInstalled));
                Assert.That(actions[0].Part, Is.SameAs(fixture.Incoming));
            }
        }

        [UnityTest]
        public IEnumerator SupportLoosenedDuringAnimationInstallsThenCollapsesAtCommitOnBothCorners()
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                Fixture fixture = CreateFixture(corner);
                SetSupportStage(fixture, 2);
                Vector3 initialPosition = fixture.Incoming.Body.position;
                var actions = new List<AssemblyActionCompleted>();
                fixture.Assembly.ActionCompleted += actions.Add;
                Assert.That(fixture.Assembly.BeginInstallFromHandoff(fixture.Pickup, fixture.Target).Succeeded,
                    Is.True);
                Assert.That(fixture.Incoming.IsInstalled, Is.False);
                Assert.That(fixture.Incoming.Body.isKinematic, Is.True);

                // Mutate the authoritative latch after the accepted start but
                // before the asynchronous transition commits its occupancy.
                SetSupportStage(fixture, 0);
                yield return new WaitForSecondsRealtime(
                    VehicleAssemblyController.InstallTransitionDurationSeconds + 0.1f);

                Assert.That(fixture.Assembly.LastOperationResult.Succeeded, Is.True);
                Assert.That(fixture.Support.IsInstalled, Is.False);
                Assert.That(fixture.Incoming.IsInstalled, Is.False);
                Assert.That(fixture.Assembly.ResolveMount(fixture.Target).IsOccupied, Is.False);
                Assert.That(fixture.Incoming.Body.isKinematic, Is.False);
                Assert.That(Vector3.Distance(fixture.Incoming.Body.position, initialPosition), Is.GreaterThan(0.1f));
                Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                    Is.LessThan(0.0001f));
                AssertInstalledThenCollapsed(actions, fixture);
            }
        }

        [UnityTest]
        public IEnumerator LooseningAfterCompletedInstallationDoesNotContinuouslyCollapseAssembly()
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                Fixture fixture = CreateFixture(corner);
                SetSupportStage(fixture, 2);
                Assert.That(fixture.Assembly.BeginInstallFromHandoff(fixture.Pickup, fixture.Target).Succeeded,
                    Is.True);
                yield return new WaitForSecondsRealtime(
                    VehicleAssemblyController.InstallTransitionDurationSeconds + 0.1f);
                Assert.That(fixture.Incoming.IsInstalled, Is.True);
                var actions = new List<AssemblyActionCompleted>();
                fixture.Assembly.ActionCompleted += actions.Add;

                SetSupportStage(fixture, 0);
                yield return new WaitForSecondsRealtime(0.1f);
                yield return new WaitForFixedUpdate();

                Assert.That(fixture.Assembly.ResolveMount(fixture.SupportMount).FastenerGroup.IsBolted,
                    Is.False);
                Assert.That(fixture.Support.IsInstalled, Is.True);
                Assert.That(fixture.Incoming.IsInstalled, Is.True);
                Assert.That(fixture.Assembly.ResolveMount(fixture.Target).InstalledPart,
                    Is.SameAs(fixture.Incoming));
                Assert.That(actions, Is.Empty,
                    "The configured collapse belongs to a new installation, not every later T=0 frame.");
            }
        }

        private static void AssertInstalledThenCollapsed(List<AssemblyActionCompleted> actions, Fixture fixture)
        {
            Assert.That(actions.Count, Is.EqualTo(3));
            Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartInstalled));
            Assert.That(actions[0].Part, Is.SameAs(fixture.Incoming));
            for (int index = 1; index < actions.Count; index++)
                Assert.That(actions[index].Action, Is.EqualTo(AssemblyActionKind.PartBrokenLoose));
            Assert.That(actions.FindAll(action => action.Action == AssemblyActionKind.PartBrokenLoose &&
                action.Part == fixture.Incoming).Count, Is.EqualTo(1));
            Assert.That(actions.FindAll(action => action.Action == AssemblyActionKind.PartBrokenLoose &&
                action.Part == fixture.Support).Count, Is.EqualTo(1));
        }

        private Fixture CreateFixture(string corner)
        {
            var root = new GameObject("PlayMode front attempt fixture " + corner);
            root.transform.position = new Vector3(corner == "fl" ? 100f : 110f, 100f, 0f);
            ownedObjects.Add(root);
            PartInstance body = CreatePart(root, "test.play.body", true, string.Empty);
            body.Body.constraints = RigidbodyConstraints.FreezeAll;
            var supportObject = new GameObject("Support " + corner);
            supportObject.transform.SetParent(root.transform, false);
            string supportId = "test.play.support." + corner;
            PartInstance support = CreatePart(supportObject, supportId, false, supportId);
            var incomingObject = new GameObject("Incoming " + corner);
            incomingObject.transform.SetParent(root.transform, false);
            PartInstance incoming = CreatePart(incomingObject, "test.play.incoming." + corner,
                false, string.Empty);
            MountPointAuthoring supportMount = CreateMount(root.transform, supportId, support);
            MountPointAuthoring target = CreateMount(root.transform, "test.play.target." + corner, incoming);
            target.Definition.ConfigureSequence(new[] { supportId }, Array.Empty<string>(), Array.Empty<string>());
            target.Definition.ConfigureInstallationChecks(supportId, Array.Empty<string>());
            var assembly = root.AddComponent<VehicleAssemblyController>();
            assembly.Configure(new[] { body, support, incoming }, new[] { supportMount, target },
                Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
            incoming.transform.position = target.Pose.position + Vector3.right * 0.2f;
            incoming.Body.position = incoming.transform.position;
            incoming.Body.useGravity = false;
            Physics.SyncTransforms();
            return new Fixture(assembly, support, incoming, supportMount, target);
        }

        private PartInstance CreatePart(GameObject owner, string id, bool isRoot, string initialMount)
        {
            PartDefinition definition = NewDefinition<PartDefinition>();
            definition.Configure(id, id, PartCategory.Suspension, 1f, null,
                new[] { PartCompatibilityRule.Create("test.play.socket", "test.play.body") });
            var identity = owner.AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(StableEntityId.New());
            var body = owner.AddComponent<Rigidbody>();
            body.useGravity = false;
            var pickup = owner.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Test part", 35f, useGravityWhenLoose: false);
            var part = owner.AddComponent<PartInstance>();
            part.Configure(definition, identity, body, pickup, isRoot, initialMount);
            return part;
        }

        private MountPointAuthoring CreateMount(Transform root, string id, PartInstance part)
        {
            FastenerDefinition bolt = NewDefinition<FastenerDefinition>();
            bolt.Configure(id + ".bolt", id, FastenerSize.Millimeter8, 8,
                FastenerDirection.ClockwiseToTighten, true, true,
                ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8));
            MountPointDefinition definition = NewDefinition<MountPointDefinition>();
            definition.Configure(id, id, "test.play.socket", "test.play.body",
                new[] { part.Definition.DefinitionId }, new MountConstraint(0.1f, 30f, 1f, 0f),
                0f, new[] { bolt });
            var group = new FastenerGroupDefinition();
            group.Configure(new[] { bolt.DefinitionId }, 8, 2, 0);
            definition.ConfigureFastenerGroup(group);
            var owner = new GameObject(id);
            owner.transform.SetParent(root, false);
            var mount = owner.AddComponent<MountPointAuthoring>();
            mount.Configure(definition, id, owner.transform, 0);
            return mount;
        }

        private T NewDefinition<T>() where T : ScriptableObject
        {
            T definition = ScriptableObject.CreateInstance<T>();
            ownedObjects.Add(definition);
            return definition;
        }

        private static void SetSupportStage(Fixture fixture, int stage)
        {
            MountPointRuntime mount = fixture.Assembly.ResolveMount(fixture.SupportMount);
            Assert.That(mount.Fasteners[0].TryRestore(true, true, stage), Is.True);
            mount.FastenerGroup.Reevaluate(true);
        }

        private sealed class Fixture
        {
            public Fixture(VehicleAssemblyController assembly, PartInstance support,
                PartInstance incoming, MountPointAuthoring supportMount, MountPointAuthoring target)
            {
                Assembly = assembly;
                Support = support;
                Incoming = incoming;
                SupportMount = supportMount;
                Target = target;
            }

            public VehicleAssemblyController Assembly { get; }
            public PartInstance Support { get; }
            public PartInstance Incoming { get; }
            public PhysicsPickupTarget Pickup => Incoming.GetComponent<PhysicsPickupTarget>();
            public MountPointAuthoring SupportMount { get; }
            public MountPointAuthoring Target { get; }
        }
    }
}
