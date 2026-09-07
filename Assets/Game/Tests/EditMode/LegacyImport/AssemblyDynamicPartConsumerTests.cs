using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class AssemblyDynamicPartConsumerTests
    {
        [Test]
        public void PurchasedReplacementFulfillsBaseChecklistWithoutCountingLooseSparesAsMissing()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            PartInstance replacement = fixture.Purchase();
            fixture.Install(replacement, fixture.ConsumableMount);
            IReadOnlyList<PartInstance> beforeSpare = fixture.Assembly.Query.GetMissingParts();
            Assert.That(beforeSpare, Has.No.Member(fixture.BaseConsumable));
            Assert.That(beforeSpare, Does.Contain(fixture.Block));
            float completeness = fixture.Assembly.Query.GetCompleteness01();

            PartInstance spare = fixture.Purchase();
            Assert.That(fixture.Assembly.Parts, Has.Length.EqualTo(4));
            Assert.That(fixture.Assembly.AllRuntimeParts, Has.Length.EqualTo(6));
            Assert.That(fixture.Assembly.Query.GetMissingParts(), Is.EquivalentTo(beforeSpare));
            Assert.That(fixture.Assembly.Query.GetCompleteness01(), Is.EqualTo(completeness));
            Assert.That(fixture.Assembly.Query.GetMissingParts(), Has.No.Member(spare));
        }

        [Test]
        public void RuntimeChildRetainsPickupContactAndMassAcrossEngineInstallAndRemoval()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            AssemblyCompoundColliderProxy middleProxy = fixture.ActiveProxies().Single();
            AssemblyCompoundShapeBinding[] authoredBindings = fixture.Physics.Bindings;
            AssemblyGraph graph = fixture.Assembly.Graph;
            PartInstance replacement = fixture.Purchase();
            fixture.Install(replacement, fixture.ConsumableMount);

            Assert.That(fixture.Assembly.Graph, Is.SameAs(graph));
            Assert.That(fixture.Physics.Bindings, Is.SameAs(authoredBindings));
            Assert.That(fixture.ActiveProxies(), Does.Contain(middleProxy));
            Assert.That(fixture.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
            Assert.That(fixture.Physics.ActiveProxyCount, Is.EqualTo(2));
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(.0001f));
            var pickup = replacement.GetComponent<AssemblySubassemblyPickupTarget>();
            Assert.That(pickup.Body, Is.SameAs(fixture.Block.Body));
            Assert.That(pickup.CanPickup(default), Is.True);

            fixture.Install(fixture.Block, fixture.BlockMount);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(127f).Within(.0001f));
            Assert.That(fixture.Physics.ActiveProxyCount, Is.Zero);
            Assert.That(pickup.Body, Is.Null);
            Assert.That(fixture.Assembly.TryRemove(fixture.Block).Succeeded, Is.True);
            Assert.That(replacement.IsInstalled, Is.True);
            Assert.That(fixture.Chassis.Body.mass, Is.EqualTo(100f).Within(.0001f));
            Assert.That(fixture.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
            Assert.That(pickup.Body, Is.SameAs(fixture.Block.Body));
            Assert.That(fixture.Physics.ActiveProxyCount, Is.EqualTo(2));
        }

        [Test]
        public void InstalledDynamicDefinitionCanSatisfyExistingSimulationRequirement()
        {
            using var fixture = new Fixture();
            var adapter = fixture.Root.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
            adapter.Configure(fixture.Assembly);
            string[] required = { fixture.BaseConsumable.Definition.DefinitionId };
            adapter.ConfigureRequirements(required, required, required, required, required, required);
            var prerequisites = new VehicleSimulationPrerequisites();
            adapter.Evaluate(default, ref prerequisites);
            Assert.That(adapter.CachedAssemblyFailures & VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing,
                Is.Not.EqualTo(VehicleSimulationPrerequisiteFailure.None));

            fixture.Install(fixture.Middle, fixture.MiddleMount);
            PartInstance replacement = fixture.Purchase();
            fixture.Install(replacement, fixture.ConsumableMount);
            adapter.Evaluate(default, ref prerequisites);
            Assert.That(adapter.CachedAssemblyFailures & VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing,
                Is.EqualTo(VehicleSimulationPrerequisiteFailure.None));
            Assert.That(fixture.Assembly.TryRemove(replacement).Succeeded, Is.True);
            adapter.Evaluate(default, ref prerequisites);
            Assert.That(adapter.CachedAssemblyFailures & VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing,
                Is.Not.EqualTo(VehicleSimulationPrerequisiteFailure.None));
        }

        [Test]
        public void RuntimeShapeResizePreservesExistingProxyAndPartState()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            PartInstance replacement = fixture.Purchase();
            fixture.Install(replacement, fixture.ConsumableMount);
            AssemblyCompoundColliderProxy proxy = fixture.ActiveProxies().Single(value => value.SourcePart == replacement);
            var source = replacement.GetComponent<BoxCollider>();
            source.size = new Vector3(.1f, .2f, .3f);
            source.center = Vector3.up * .025f;

            Assert.That(fixture.Physics.TryRefreshRuntimeBindingShapes(replacement, out string error), Is.True, error);
            var shape = proxy.GetComponent<BoxCollider>();
            Assert.That(shape.size, Is.EqualTo(source.size));
            Assert.That(shape.center, Is.EqualTo(source.center));
            Assert.That(replacement.IsInstalled, Is.True);
            Assert.That(shape.attachedRigidbody, Is.SameAs(fixture.Block.Body));
            Assert.That(fixture.Physics.ActiveProxyCount, Is.EqualTo(2));
            Assert.That(fixture.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
        }

        [Test]
        public void InvalidDuplicateBindingDoesNotMutateExistingContactsOrMass()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            PartInstance replacement = fixture.Purchase();
            fixture.Install(replacement, fixture.ConsumableMount);
            AssemblyCompoundColliderProxy[] proxies = fixture.AllProxies();
            var duplicate = new AssemblyCompoundShapeBinding(replacement,
                new Collider[] { replacement.GetComponent<BoxCollider>() });
            Assert.That(fixture.Physics.TryRegisterRuntimeBinding(duplicate, out _), Is.False);
            Assert.That(fixture.AllProxies(), Is.EquivalentTo(proxies));
            Assert.That(fixture.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
            Assert.That(fixture.Physics.TryUnregisterRuntimeBinding(replacement, out _), Is.False,
                "Installed state must be detached by the assembly operation, not by physical cleanup.");
            Assert.That(replacement.IsInstalled, Is.True);
        }

        [Test]
        public void DetachedRuntimeBindingCanBeRemovedAndRestoredWithoutOrphanContactsOrMassDrift()
        {
            using var fixture = new Fixture();
            fixture.Install(fixture.Middle, fixture.MiddleMount);
            PartInstance replacement = fixture.Purchase();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                fixture.Install(replacement, fixture.ConsumableMount);
                Assert.That(fixture.Block.Body.mass, Is.EqualTo(27f).Within(.0001f));
                Assert.That(fixture.Assembly.TryRemove(replacement).Succeeded, Is.True);
                Assert.That(fixture.Physics.TryUnregisterRuntimeBinding(replacement, out string error), Is.True, error);
                Assert.That(fixture.Physics.HasRuntimeBinding(replacement), Is.False);
                Assert.That(fixture.AllProxies().Any(value => value.SourcePart == replacement), Is.False);
                Assert.That(fixture.Block.Body.mass, Is.EqualTo(25f).Within(.0001f));
                Assert.That(fixture.Assembly.TryUnregisterDynamicPart(replacement, out error), Is.True, error);
                Assert.That(fixture.Assembly.TryRegisterDynamicPart(replacement, "item.test-consumable", out error), Is.True, error);
                fixture.RegisterPhysics(replacement);
                Assert.That(fixture.AllProxies().Count(value => value.SourcePart == replacement), Is.EqualTo(1));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("dynamic consumer fixture");
            private readonly GameObject loose = new GameObject("loose consumer fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyLooseCompoundPhysics Physics;
            public readonly PartInstance Chassis, Block, Middle, BaseConsumable;
            public readonly MountPointAuthoring BlockMount, MiddleMount, ConsumableMount;

            public Fixture()
            {
                Chassis = CreatePart(Definition("chassis", 100f), Root, true);
                Block = CreatePart(Definition("block", 20f));
                Middle = CreatePart(Definition("middle", 5f));
                BaseConsumable = CreatePart(Definition("consumable", 2f));
                BlockMount = Mount("mount.block", Chassis, Block, Vector3.forward * 2f);
                MiddleMount = Mount("mount.middle", Block, Middle, Vector3.forward);
                ConsumableMount = Mount("mount.consumable", Middle, BaseConsumable, Vector3.up);
                BlockMount.Definition.ConfigureRetainedRemovalChildren(new[] { MiddleMount.MountId });
                MiddleMount.Definition.ConfigureRetainedRemovalChildren(new[] { ConsumableMount.MountId });
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { Chassis, Block, Middle, BaseConsumable },
                    new[] { BlockMount, MiddleMount, ConsumableMount }, Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), loose.transform);
                Assembly.ConfigureDynamicPartDefinitions(new[] { BaseConsumable.Definition });
                foreach (PartInstance part in new[] { Block, Middle, BaseConsumable })
                    part.gameObject.AddComponent<AssemblySubassemblyPickupTarget>().Configure(Assembly, part);
                var mass = Root.AddComponent<AssemblyChassisMassController>();
                mass.Configure(Chassis.Body, Assembly, 100f);
                Physics = Root.AddComponent<AssemblyLooseCompoundPhysics>();
                Physics.Configure(Assembly, new[] { Block, Middle, BaseConsumable }.Select(part =>
                    new AssemblyCompoundShapeBinding(part, new Collider[] { part.GetComponent<BoxCollider>() })).ToArray());
                Physics.Refresh(true);
            }

            public PartInstance Purchase()
            {
                PartInstance part = CreatePart(BaseConsumable.Definition);
                Assert.That(Assembly.TryRegisterDynamicPart(part, "item.test-consumable", out string error), Is.True, error);
                part.gameObject.AddComponent<AssemblySubassemblyPickupTarget>().Configure(Assembly, part);
                RegisterPhysics(part);
                return part;
            }

            public void RegisterPhysics(PartInstance part)
            {
                Assert.That(Physics.TryRegisterRuntimeBinding(new AssemblyCompoundShapeBinding(part,
                    new Collider[] { part.GetComponent<BoxCollider>() }), out string error), Is.True, error);
            }

            public AssemblyCompoundColliderProxy[] AllProxies() => loose.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true)
                .Concat(Root.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true)).ToArray();

            public AssemblyCompoundColliderProxy[] ActiveProxies() => AllProxies().Where(proxy => proxy.gameObject.activeInHierarchy).ToArray();

            public void Install(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            private PartDefinition Definition(string id, float mass)
            {
                var definition = ScriptableObject.CreateInstance<PartDefinition>();
                definitions.Add(definition);
                definition.Configure("test.consumer." + id, id, PartCategory.Engine, mass, Root,
                    new[] { PartCompatibilityRule.Create("test.consumer.socket", string.Empty) });
                return definition;
            }

            private PartInstance CreatePart(PartDefinition definition, GameObject existing = null, bool isRoot = false)
            {
                GameObject go = existing != null ? existing : new GameObject(definition.DefinitionId);
                if (!isRoot) go.transform.SetParent(loose.transform);
                var identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>();
                body.mass = definition.MassKilograms; body.useGravity = false; body.centerOfMass = Vector3.zero;
                go.AddComponent<BoxCollider>().size = Vector3.one * .2f;
                var pickup = go.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "pickup", 100f);
                var part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, isRoot, string.Empty);
                return part;
            }

            private MountPointAuthoring Mount(string id, PartInstance owner, PartInstance child, Vector3 localPosition)
            {
                var definition = ScriptableObject.CreateInstance<MountPointDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, "test.consumer.socket", owner.Definition.DefinitionId,
                    new[] { child.Definition.DefinitionId }, new MountConstraint(1f, 180f, 1f, 0f),
                    0f, Array.Empty<FastenerDefinition>());
                var socket = new GameObject(id);
                socket.transform.SetParent(owner.transform); socket.transform.localPosition = localPosition;
                var mount = socket.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, socket.transform, 0);
                socket.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(loose);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
