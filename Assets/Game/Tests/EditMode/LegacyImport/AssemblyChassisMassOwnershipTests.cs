using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class AssemblyChassisMassOwnershipTests
    {
        [Test]
        public void FloorAssemblyDoesNotMoveChassisMassOrCenterOfMass()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            fixture.Install(fixture.Leaf, fixture.LeafMount);
            Assert.That(fixture.Middle.IsInstalled && fixture.Leaf.IsInstalled, Is.True);
            Assert.That(fixture.Block.IsInstalled, Is.False);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(0.001f));
            Assert.That(fixture.Chassis.Body.centerOfMass, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void FullBlockInstallAndRetainedRemovalTransferAllChildMassInSameRefresh()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            fixture.Install(fixture.Leaf, fixture.LeafMount);
            fixture.Install(fixture.Block, fixture.BlockMount);
            // No manual RefreshMass: the installation event must update the
            // complete aggregate, although neither child was reinstalled.
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(127f).Within(0.001f));
            Vector3 expected = (fixture.Block.Body.worldCenterOfMass * 20f +
                fixture.Middle.Body.worldCenterOfMass * 5f +
                fixture.Leaf.Body.worldCenterOfMass * 2f) / 127f;
            Assert.That(Vector3.Distance(fixture.Chassis.Body.centerOfMass, expected), Is.LessThan(0.001f));

            Assert.That(fixture.Assembly.TryRemove(fixture.Block).Succeeded, Is.True);
            Assert.That(fixture.Middle.IsInstalled && fixture.Leaf.IsInstalled, Is.True);
            Assert.That(fixture.Leaf.transform.IsChildOf(fixture.Block.transform), Is.True);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(0.001f));
            Assert.That(fixture.Chassis.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            fixture.Mass.RefreshMass(force: true);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void InstallingMovingPartsOnFloorDoesNotApplyChassisImpulse()
        {
            using var fixture = new Fixture();
            fixture.Middle.Body.linearVelocity = Vector3.right * 4f;
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            fixture.Leaf.Body.linearVelocity = Vector3.forward * 3f;
            fixture.Install(fixture.Leaf, fixture.LeafMount);
            Assert.That(fixture.Chassis.Body.GetAccumulatedForce(0.02f), Is.EqualTo(Vector3.zero));
            Assert.That(fixture.Chassis.Body.GetAccumulatedTorque(0.02f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DirectChassisMountStillTransfersMassAndImpulseWithoutLooseOwnerComponent()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.BlockMount.GetComponent<AssemblyOwnedMountAuthoring>(), Is.Null);
            fixture.Block.Body.linearVelocity = Vector3.right * 2f;
            fixture.Install(fixture.Block, fixture.BlockMount);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(120f).Within(0.001f));
            Assert.That(fixture.Chassis.Body.GetAccumulatedForce(0.02f).x, Is.GreaterThan(0f));
        }

        [Test]
        public void RealJointedSuspensionBodyKeepsItsOwnMass()
        {
            using var fixture = new Fixture();
            fixture.Block.gameObject.AddComponent<AssemblyInstalledPhysicsLink>()
                .Configure(fixture.Block.Body, AssemblyInstalledPhysicsLinkMode.Fixed);
            fixture.Install(fixture.Block, fixture.BlockMount);
            Assert.That(fixture.Block.UsesDynamicInstalledPhysics, Is.True);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(0.001f));
            Assert.That(fixture.Block.Body.mass, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void BrokenExplicitOwnerBindingDoesNotFallBackToChassisTransform()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            fixture.Install(fixture.Block, fixture.BlockMount);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(125f).Within(0.001f));
            fixture.MiddleMount.GetComponent<AssemblyOwnedMountAuthoring>().Configure(null);
            fixture.Mass.RefreshMass(force: true);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(120f).Within(0.001f));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("chassis mass ownership fixture");
            private readonly GameObject looseRoot = new GameObject("loose engine fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyChassisMassController Mass;
            public readonly PartInstance Chassis;
            public readonly PartInstance Block;
            public readonly PartInstance Middle;
            public readonly PartInstance Leaf;
            public readonly MountPointAuthoring BlockMount;
            public readonly MountPointAuthoring MiddleMount;
            public readonly MountPointAuthoring LeafMount;

            public Fixture()
            {
                Chassis = CreatePart("chassis", 100f, root, true);
                Block = CreatePart("block", 20f);
                Middle = CreatePart("middle", 5f);
                Leaf = CreatePart("leaf", 2f);
                Block.transform.position = new Vector3(10f, 0f, 0f);
                Block.Body.position = Block.transform.position;
                BlockMount = CreateMount("mount.block", Chassis, Block, new Vector3(0f, 0f, 2f), false);
                MiddleMount = CreateMount("mount.middle", Block, Middle, new Vector3(0f, 0f, 1f));
                LeafMount = CreateMount("mount.leaf", Middle, Leaf, Vector3.up);
                BlockMount.Definition.ConfigureRetainedRemovalChildren(new[] { MiddleMount.MountId });
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { Chassis, Block, Middle, Leaf },
                    new[] { BlockMount, MiddleMount, LeafMount }, Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), looseRoot.transform);
                Mass = root.AddComponent<AssemblyChassisMassController>();
                Mass.Configure(Chassis.Body, Assembly, 100f);
            }

            public void Install(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            private PartInstance CreatePart(string id, float mass, GameObject existing = null, bool isRoot = false)
            {
                PartDefinition definition = ScriptableObject.CreateInstance<PartDefinition>();
                definitions.Add(definition);
                definition.Configure("test.part." + id, id, PartCategory.Engine, mass, root,
                    new[] { PartCompatibilityRule.Create("test.socket", string.Empty) });
                GameObject go = existing != null ? existing : new GameObject(id);
                if (!isRoot) go.transform.SetParent(looseRoot.transform);
                var identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>();
                body.mass = mass;
                body.useGravity = false;
                body.centerOfMass = Vector3.zero;
                var part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, null, isRoot, string.Empty);
                return part;
            }

            private MountPointAuthoring CreateMount(string id, PartInstance owner, PartInstance child,
                Vector3 localPosition, bool explicitOwner = true)
            {
                MountPointDefinition definition = ScriptableObject.CreateInstance<MountPointDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, "test.socket", owner.Definition.DefinitionId,
                    new[] { child.Definition.DefinitionId }, new MountConstraint(1f, 180f, 1f, 0f),
                    0f, Array.Empty<FastenerDefinition>());
                var socket = new GameObject(id);
                socket.transform.SetParent(owner.transform);
                socket.transform.localPosition = localPosition;
                var mount = socket.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, socket.transform, 0);
                if (explicitOwner) socket.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(looseRoot);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
