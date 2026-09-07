using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class AssemblySubassemblyPickupTests
    {
        [Test]
        public void EnclosedChildSuppressesItsOutlineWithoutLosingWholeEnginePickup()
        {
            using var fixture = new Fixture();
            fixture.Adapter.Configure(fixture.Assembly, fixture.Middle);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.Middle).Succeeded, Is.False);
            Assert.That(fixture.Adapter.ShouldShowOutline, Is.False);
            Assert.That(fixture.Adapter.CanPickup(default), Is.True);
            Assert.That(fixture.Adapter.Body, Is.SameAs(fixture.Block.Body));
            Assert.That(fixture.Assembly.TryRemove(fixture.Leaf).Succeeded, Is.True);
            Assert.That(fixture.Adapter.ShouldShowOutline, Is.True);
            fixture.Adapter.Configure(fixture.Assembly, fixture.Block);
            Assert.That(fixture.Adapter.ShouldShowOutline, Is.True);
        }

        [Test]
        public void InstalledChildCarriesOutermostLooseOwnerWithoutRemovingChildren()
        {
            using var fixture = new Fixture();
            var context = default(InteractionContext);
            Assert.That(fixture.Adapter.CanPickup(context), Is.True);
            Assert.That(fixture.Adapter.Body, Is.SameAs(fixture.Block.Body));
            Assert.That(fixture.Adapter.StableId, Is.EqualTo(fixture.Block.StableId));
            Assert.That(fixture.Assembly.ResolvePart(fixture.Adapter), Is.SameAs(fixture.Block));
            Assert.That(fixture.Leaf.PickupTarget.CanPickup(context), Is.False);

            fixture.Adapter.NotifyPickedUp(context);
            Assert.That(fixture.Block.PickupTarget.IsCarried, Is.True);
            Assert.That(fixture.Leaf.IsInstalled, Is.True);
            Assert.That(fixture.Middle.IsInstalled, Is.True);
            Assert.That(fixture.Adapter.CanPickup(context), Is.False);
            fixture.Adapter.NotifyReleased(PickupReleaseReason.TargetLost);
            Assert.That(fixture.Block.PickupTarget.IsCarried, Is.False);
            Assert.That(fixture.Adapter.CanPickup(context), Is.True);
        }

        [Test]
        public void InstalledEngineCannotLiftChassisThroughAChildSurface()
        {
            using var fixture = new Fixture(blockInstalled: true);
            Assert.That(fixture.Adapter.CanPickup(default), Is.False);
            Assert.That(fixture.Adapter.Body, Is.Null);
        }

        [Test]
        public void RealCarrySaveAndHandoffKeepOwnerIdentityAndInstalledChildren()
        {
            using var fixture = new Fixture();
            var host = fixture.Leaf.gameObject.AddComponent<InteractionTargetHost>();
            host.Configure(fixture.Adapter);
            var carry = fixture.Assembly.gameObject.AddComponent<PhysicalCarryController>();
            carry.Configure(fixture.Assembly.transform, null);
            Assert.That(carry.TryPickup(fixture.Adapter, default), Is.True);
            Assert.That(carry.CaptureSaveState().StableEntityId, Is.EqualTo(fixture.Block.StableId.Value));
            var target = fixture.BlockMount.gameObject.AddComponent<AssemblyMountHandoffTarget>();
            target.Configure(fixture.Assembly, fixture.BlockMount);
            Assert.That(carry.TryHandoff(target, default), Is.True, target.HandoffPrompt);
            Assert.That(carry.HasHeldObject, Is.False);
            Assert.That(fixture.Block.IsInstalled, Is.True);
            Assert.That(fixture.Middle.IsInstalled, Is.True);
            Assert.That(fixture.Leaf.IsInstalled, Is.True);
            Assert.That(fixture.Block.PickupTarget.IsCarried, Is.False);
        }

        [Test]
        public void StandalonePartDelegatesToItsOwnPickupAndRetainsMassLimit()
        {
            using var fixture = new Fixture();
            fixture.Adapter.Configure(fixture.Assembly, fixture.Block);
            Assert.That(fixture.Adapter.Body, Is.SameAs(fixture.Block.Body));
            fixture.Block.Body.mass = 101f;
            Assert.That(fixture.Adapter.CanPickup(default), Is.False);
        }

        [Test]
        public void BrokenOwnerBindingNeverFallsBackToTransformAncestor()
        {
            using var fixture = new Fixture();
            fixture.LeafMount.GetComponent<AssemblyOwnedMountAuthoring>().Configure(null);
            Assert.That(fixture.Adapter.CanPickup(default), Is.False);
            Assert.That(fixture.Adapter.Body, Is.Null);
        }

        [Test]
        public void ReleaseUsesOriginalOwnerEvenIfMountBindingChangesWhileCarried()
        {
            using var fixture = new Fixture();
            fixture.Adapter.NotifyPickedUp(default);
            fixture.LeafMount.GetComponent<AssemblyOwnedMountAuthoring>().Configure(fixture.Chassis);
            Assert.That(fixture.Adapter.Body, Is.SameAs(fixture.Block.Body));
            fixture.Adapter.NotifyReleased(PickupReleaseReason.TargetLost);
            Assert.That(fixture.Block.PickupTarget.IsCarried, Is.False);
            Assert.That(fixture.Adapter.Body, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LooseEngineSocketRayCanPassOnlyThroughItsRegisteredOwner(bool optIn)
        {
            using var fixture = new Fixture();
            fixture.Block.transform.SetParent(null);
            fixture.Block.transform.position = Vector3.forward;
            var collider = fixture.Block.gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.5f;
            var ownerHost = fixture.Block.gameObject.AddComponent<InteractionTargetHost>();
            ownerHost.Configure(fixture.Block.PickupTarget);
            var target = fixture.LeafMount.gameObject.AddComponent<AssemblyMountHandoffTarget>();
            target.Configure(fixture.Assembly, fixture.LeafMount);
            var host = fixture.LeafMount.gameObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureSelectionPriority(30);
            var trigger = fixture.LeafMount.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.03f;
            if (optIn)
            {
                var bypass = fixture.LeafMount.gameObject.AddComponent<AssemblyLooseOwnerMountOcclusionTarget>();
                bypass.Configure(fixture.Assembly, fixture.LeafMount, target);
                host.AddCapabilityFirst(bypass);
                Assert.That(bypass.CanBypassParentCollider(ownerHost), Is.True);
            }
            Assert.That(target.CanBypassParentCollider(ownerHost), Is.False,
                "This reproduces a loose owner outside the controller Transform hierarchy.");
            var ray = fixture.Assembly.gameObject.AddComponent<RaycastInteractionCandidateSource>();
            ray.Configure(fixture.Assembly.transform, 2f, ~0);
            ray.SetCarriedObjectTarget(fixture.Leaf.PickupTarget);
            Physics.SyncTransforms();
            Assert.That(ray.Query().Host, optIn ? Is.SameAs(host) : Is.Not.SameAs(host));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("subassembly fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly VehicleAssemblyController Assembly;
            public readonly PartInstance Chassis;
            public readonly PartInstance Block;
            public readonly PartInstance Middle;
            public readonly PartInstance Leaf;
            public readonly MountPointAuthoring LeafMount;
            public readonly MountPointAuthoring BlockMount;
            public readonly AssemblySubassemblyPickupTarget Adapter;

            public Fixture(bool blockInstalled = false)
            {
                Chassis = CreatePart("body", true, string.Empty);
                Block = CreatePart("block", false, blockInstalled ? "mount.block" : string.Empty);
                Middle = CreatePart("middle", false, "mount.middle");
                Leaf = CreatePart("leaf", false, "mount.leaf");
                MountPointAuthoring blockMount = CreateMount("mount.block", Chassis, Block);
                BlockMount = blockMount;
                MountPointAuthoring middleMount = CreateMount("mount.middle", Block, Middle);
                LeafMount = CreateMount("mount.leaf", Middle, Leaf);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { Chassis, Block, Middle, Leaf },
                    new[] { blockMount, middleMount, LeafMount },
                    Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
                Adapter = Leaf.gameObject.AddComponent<AssemblySubassemblyPickupTarget>();
                Adapter.Configure(Assembly, Leaf);
            }

            private PartInstance CreatePart(string id, bool assemblyRoot, string mount)
            {
                PartDefinition definition = ScriptableObject.CreateInstance<PartDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, PartCategory.Engine, 1f, root,
                    new[] { PartCompatibilityRule.Create("test.socket", string.Empty) });
                var go = new GameObject(id);
                go.transform.SetParent(root.transform);
                var identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>();
                var pickup = go.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, id, 100f);
                var part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, assemblyRoot, mount);
                return part;
            }

            private MountPointAuthoring CreateMount(string id, PartInstance owner, PartInstance child)
            {
                MountPointDefinition definition = ScriptableObject.CreateInstance<MountPointDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, "test.socket", owner.Definition.DefinitionId,
                    new[] { child.Definition.DefinitionId },
                    new MountConstraint(1f, 180f, 1f, 0f), 0f, Array.Empty<FastenerDefinition>());
                var go = new GameObject(id);
                go.transform.SetParent(owner.transform);
                var mount = go.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, go.transform, 0);
                go.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }

            public void Dispose()
            {
                if (Block != null && !Block.transform.IsChildOf(root.transform))
                {
                    Object.DestroyImmediate(Block.gameObject);
                }
                Object.DestroyImmediate(root);
                foreach (ScriptableObject definition in definitions)
                {
                    Object.DestroyImmediate(definition);
                }
            }
        }
    }
}
