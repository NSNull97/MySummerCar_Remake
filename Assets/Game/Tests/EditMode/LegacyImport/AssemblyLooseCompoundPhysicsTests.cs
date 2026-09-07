using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class AssemblyLooseCompoundPhysicsTests
    {
        [Test]
        public void NestedFloorAssemblyHasOneSolidOwnerAndNoMassDrift()
        {
            using var f = new Fixture();
            f.Install(f.Middle, f.MiddleMount);
            f.Install(f.Leaf, f.LeafMount);
            for (int index = 0; index < 12; index++) f.Physics.Refresh(true);
            Assert.That(f.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
            Assert.That(f.Middle.Body.mass, Is.EqualTo(5f));
            Assert.That(f.Leaf.Body.mass, Is.EqualTo(2f));
            Vector3 expected = (Vector3.forward * 5f + (Vector3.forward + Vector3.up) * 2f) / 27f;
            Assert.That(Vector3.Distance(f.Block.Body.centerOfMass, expected), Is.LessThan(.00001f));
            Assert.That(f.Physics.ActiveProxyCount, Is.EqualTo(2));
            foreach (AssemblyCompoundColliderProxy proxy in f.ActiveProxies())
            {
                Assert.That(proxy.GetComponent<Collider>().attachedRigidbody, Is.SameAs(f.Block.Body));
                Assert.That(proxy.LooseOwner, Is.SameAs(f.Block));
                Assert.That(proxy.SourceShape.enabled, Is.False);
                Assert.That(proxy.GetComponent<Collider>().isTrigger, Is.False);
                Assert.That(proxy.gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
            }
            Assert.That(f.Chassis.Body.mass, Is.EqualTo(100f));
            Assert.That(f.Chassis.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            Assert.That(f.Chassis.Body.GetAccumulatedForce(.02f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RemovingRetainedSubassemblyTransfersProxyAndMassToItsNewLooseRoot()
        {
            using var f = new Fixture();
            f.Install(f.Middle, f.MiddleMount);
            f.Install(f.Leaf, f.LeafMount);
            Assert.That(f.Assembly.TryRemove(f.Middle).Succeeded, Is.True);
            Assert.That(f.Leaf.IsInstalled, Is.True);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Block.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            Assert.That(f.Middle.Body.mass, Is.EqualTo(7f));
            Assert.That(Vector3.Distance(f.Middle.Body.centerOfMass, Vector3.up * (2f / 7f)), Is.LessThan(.00001f));
            Assert.That(f.Physics.ActiveProxyCount, Is.EqualTo(1));
            Assert.That(f.ActiveProxies().Single().GetComponent<Collider>().attachedRigidbody, Is.SameAs(f.Middle.Body));
            Assert.That(f.Middle.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(f.Assembly.TryRemove(f.Leaf).Succeeded, Is.True);
            Assert.That(f.Middle.Body.mass, Is.EqualTo(5f));
            Assert.That(f.Middle.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            Assert.That(f.Physics.ActiveProxyCount, Is.Zero);
        }

        [Test]
        public void EngineToChassisDisablesLooseContactsAndRetainedRemovalRestoresThem()
        {
            using var f = new Fixture();
            f.Install(f.Middle, f.MiddleMount);
            f.Install(f.Leaf, f.LeafMount);
            f.Install(f.Block, f.BlockMount);
            Assert.That(f.Physics.ActiveProxyCount, Is.Zero);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Chassis.Body.mass, Is.EqualTo(100f), "Chassis consumer owns chassis mass; this packet never writes it.");
            Assert.That(f.Chassis.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            Assert.That(f.Assembly.TryRemove(f.Block).Succeeded, Is.True);
            Assert.That(f.Middle.IsInstalled && f.Leaf.IsInstalled, Is.True);
            Assert.That(f.Block.Body.mass, Is.EqualTo(27f));
            Assert.That(f.Physics.ActiveProxyCount, Is.EqualTo(2));
            Assert.That(f.ActiveProxies().All(proxy => proxy.LooseOwner == f.Block), Is.True);
        }

        [Test]
        public void DisableAndReenableRestoreOwnPropertiesWithoutRecapturingAggregate()
        {
            using var f = new Fixture();
            f.Install(f.Middle, f.MiddleMount);
            f.Physics.enabled = false;
            // This is an ordinary runtime MonoBehaviour, not ExecuteAlways;
            // drive its lifecycle explicitly in an EditMode-only fixture.
            typeof(AssemblyLooseCompoundPhysics).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(f.Physics, null);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Block.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            Assert.That(f.Physics.ActiveProxyCount, Is.Zero);
            f.Physics.enabled = true;
            f.Physics.Refresh();
            Assert.That(f.Block.Body.mass, Is.EqualTo(25f));
            Assert.That(f.Physics.ActiveProxyCount, Is.EqualTo(1));
        }

        [Test]
        public void BrokenExplicitOwnerDoesNotUseTransformParentAsFallback()
        {
            using var f = new Fixture();
            f.Install(f.Middle, f.MiddleMount);
            f.MiddleMount.GetComponent<AssemblyOwnedMountAuthoring>().Configure(null);
            f.Physics.Refresh(true);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Physics.ActiveProxyCount, Is.Zero);
        }

        [Test]
        public void QueryProxyAndNestedSourceBodiesAreNotCapturedAsOwnShapes()
        {
            using var f = new Fixture(false);
            var query = new GameObject("query"); query.transform.SetParent(f.Block.transform);
            query.AddComponent<BoxCollider>().isTrigger = true;
            f.Install(f.Middle, f.MiddleMount);
            // Explicit authoring is performed on loose assets, not a live graph;
            // inspect only the authored binding exclusion using the floor fixture.
            Assert.That(f.Bindings[0].Shapes, Has.Length.EqualTo(1));
            Assert.That(f.Bindings[0].Shapes[0], Is.SameAs(f.Block.GetComponent<BoxCollider>()));
            f.Physics.Refresh(true);
            Assert.That(f.Physics.ActiveProxyCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidDuplicateShapeFailsBeforeProxyCreationOrMassMutation()
        {
            using var f = new Fixture(false);
            f.Physics.Configure(f.Assembly, new[] { new AssemblyCompoundShapeBinding(f.Block,
                new Collider[] { f.Block.GetComponent<BoxCollider>(), f.Block.GetComponent<BoxCollider>() }) });
            Assert.Throws<InvalidOperationException>(() => f.Physics.Refresh(true));
            Assert.That(f.Root.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true), Is.Empty);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForeignNestedRigidbodyShapeRejectsEvenIfSourceIsDisabled(bool enabled)
        {
            using var f = new Fixture(false);
            var foreignBody = new GameObject("foreign nested rigidbody");
            foreignBody.transform.SetParent(f.Block.transform);
            foreignBody.AddComponent<Rigidbody>();
            var shape = foreignBody.AddComponent<BoxCollider>();
            shape.enabled = enabled;
            Assert.That(shape.GetComponentInParent<PartInstance>(), Is.SameAs(f.Block));
            f.Physics.Configure(f.Assembly, new[]
            {
                new AssemblyCompoundShapeBinding(f.Block, new Collider[] { shape }),
            });

            Assert.Throws<InvalidOperationException>(() => f.Physics.Refresh(true));
            Assert.That(f.Root.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true), Is.Empty);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
        }

        [Test]
        public void EngineAuthoringIsExplicitIdempotentAndLeavesChassisCarryLimitAlone()
        {
            using var f = new Fixture(false);
            Assert.That(Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(f.Assembly), Is.GreaterThan(0));
            Assert.That(Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(f.Assembly), Is.Zero);
            Assert.That(f.Physics.Bindings.Select(binding => binding.Part), Is.EqualTo(new[] { f.Block, f.Middle, f.Leaf }));
            foreach (PartInstance part in new[] { f.Block, f.Middle, f.Leaf })
            {
                Assert.That(part.PickupTarget.MaximumCarryMassKilograms, Is.EqualTo(120f));
                Assert.That(new SerializedObject(part.PickupTarget).FindProperty("allowAssemblyMassDebugOverride").boolValue, Is.True);
            }
            Assert.That(f.Chassis.PickupTarget.MaximumCarryMassKilograms, Is.EqualTo(35f));
        }

        [Test]
        public void MissingEnginePickupBindingFailsBeforeAnyCarryLimitsChange()
        {
            using var f = new Fixture(false);
            Object.DestroyImmediate(f.Leaf.GetComponent<AssemblySubassemblyPickupTarget>());
            Assert.Throws<InvalidDataException>(() => Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(f.Assembly));
            Assert.That(f.Block.PickupTarget.MaximumCarryMassKilograms, Is.EqualTo(35f));
            Assert.That(f.Physics.Bindings.Length, Is.EqualTo(3));
        }

        [Test]
        public void StartupUsesAuthoredOwnCenterInsteadOfPreviouslyAccumulatedBodyCenter()
        {
            using var f = new Fixture(false);
            f.Block.Body.mass = 500f;
            f.Block.Body.centerOfMass = Vector3.one * 5f;
            f.Physics.Refresh(true);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Block.Body.centerOfMass, Is.EqualTo(Vector3.zero));
            f.Install(f.Middle, f.MiddleMount);
            Assert.That(f.Block.Body.mass, Is.EqualTo(25f));
            Assert.That(Vector3.Distance(f.Block.Body.centerOfMass, Vector3.forward / 5f), Is.LessThan(.00001f));
        }

        [Test]
        public void RealJointedChildKeepsIndependentContactAndMass()
        {
            using var f = new Fixture();
            f.Middle.gameObject.AddComponent<AssemblyInstalledPhysicsLink>()
                .Configure(f.Middle.Body, AssemblyInstalledPhysicsLinkMode.Fixed);
            f.Install(f.Middle, f.MiddleMount);
            Assert.That(f.Middle.UsesDynamicInstalledPhysics, Is.True);
            Assert.That(f.Middle.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
            Assert.That(f.Physics.ActiveProxyCount, Is.Zero);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("compound fixture");
            private readonly GameObject loose = new GameObject("loose fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyLooseCompoundPhysics Physics;
            public readonly PartInstance Chassis, Block, Middle, Leaf;
            public readonly MountPointAuthoring BlockMount, MiddleMount, LeafMount;
            public readonly AssemblyCompoundShapeBinding[] Bindings;

            public Fixture(bool initialize = true)
            {
                Chassis = Part("vehicle.satsuma.test.chassis", 100f, Root, true);
                Block = Part("vehicle.satsuma.part.engine-block", 20f);
                Middle = Part("vehicle.satsuma.test.middle", 5f);
                Leaf = Part("vehicle.satsuma.test.leaf", 2f);
                Block.transform.position = Vector3.right * 10f; Block.Body.position = Block.transform.position;
                BlockMount = Mount("test.mount.block", Chassis, Block, Vector3.forward * 2f);
                MiddleMount = Mount("test.mount.middle", Block, Middle, Vector3.forward);
                LeafMount = Mount("test.mount.leaf", Middle, Leaf, Vector3.up);
                BlockMount.Definition.ConfigureRetainedRemovalChildren(new[] { MiddleMount.MountId });
                MiddleMount.Definition.ConfigureRetainedRemovalChildren(new[] { LeafMount.MountId });
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { Chassis, Block, Middle, Leaf },
                    new[] { BlockMount, MiddleMount, LeafMount }, Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), loose.transform);
                foreach (PartInstance part in new[] { Block, Middle, Leaf })
                    part.gameObject.AddComponent<AssemblySubassemblyPickupTarget>().Configure(Assembly, part);
                Bindings = new[] { Block, Middle, Leaf }.Select(part => new AssemblyCompoundShapeBinding(part,
                    new Collider[] { part.GetComponent<BoxCollider>() })).ToArray();
                Physics = Root.AddComponent<AssemblyLooseCompoundPhysics>(); Physics.Configure(Assembly, Bindings);
                if (initialize) Physics.Refresh(true);
            }

            public AssemblyCompoundColliderProxy[] ActiveProxies() => loose.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true)
                .Concat(Root.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true))
                .Where(proxy => proxy.gameObject.activeInHierarchy).ToArray();

            public void Install(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            private PartInstance Part(string id, float mass, GameObject existing = null, bool isRoot = false)
            {
                var definition = ScriptableObject.CreateInstance<PartDefinition>(); definitions.Add(definition);
                definition.Configure(id, id, PartCategory.Engine, mass, Root,
                    new[] { PartCompatibilityRule.Create("test.socket", string.Empty) });
                GameObject go = existing != null ? existing : new GameObject(id);
                if (!isRoot) go.transform.SetParent(loose.transform);
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.mass = mass; body.useGravity = false; body.centerOfMass = Vector3.zero;
                go.AddComponent<BoxCollider>().size = Vector3.one * .2f;
                var pickup = go.AddComponent<PhysicsPickupTarget>(); pickup.Configure(body, identity, "pickup", 35f);
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, body, pickup, isRoot, string.Empty);
                return part;
            }

            private MountPointAuthoring Mount(string id, PartInstance owner, PartInstance child, Vector3 localPosition)
            {
                var definition = ScriptableObject.CreateInstance<MountPointDefinition>(); definitions.Add(definition);
                definition.Configure(id, id, "test.socket", owner.Definition.DefinitionId, new[] { child.Definition.DefinitionId },
                    new MountConstraint(1f, 180f, 1f, 0f), 0f, Array.Empty<FastenerDefinition>());
                var socket = new GameObject(id); socket.transform.SetParent(owner.transform); socket.transform.localPosition = localPosition;
                var mount = socket.AddComponent<MountPointAuthoring>(); mount.Configure(definition, id, socket.transform, 0);
                socket.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root); Object.DestroyImmediate(loose);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
