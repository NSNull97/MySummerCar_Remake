using System;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class AssemblyCarryDebugOverrideTests
    {
        [Test]
        public void NormalEngineLimitAccepts120AndRejectsAnythingHeavier()
        {
            using var f = new Fixture();
            f.Target.ConfigureAssemblyCarryLimit(120f);
            f.Body.mass = 120f;
            Assert.That(f.Target.CanPickup(f.Context), Is.True);
            f.Body.mass = 120.01f;
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
            Assert.That(f.Debug.IgnoreAssemblyMassLimit, Is.False);
        }

        [Test]
        public void OptInOverrideIsScopedToItsPlayerIncludingThePlayersInteractorChild()
        {
            using var f = new Fixture();
            f.Target.ConfigureAssemblyCarryLimit(120f);
            f.Debug.SetIgnoreAssemblyMassLimit(true);
            Assert.That(f.Target.CanPickup(f.Context), Is.True);
            Assert.That(f.Target.CanPickup(new InteractionContext(f.OtherPlayer, Vector3.zero, Vector3.forward)), Is.False);
            Assert.That(f.Target.CanPickup(default), Is.False);
            var child = new GameObject("player ray interactor"); child.transform.SetParent(f.Player.transform);
            Assert.That(f.Target.CanPickup(new InteractionContext(child, Vector3.zero, Vector3.forward)), Is.True);
            f.Debug.SetIgnoreAssemblyMassLimit(false);
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
        }

        [Test]
        public void OrdinaryHeavyObjectDoesNotGainTheAssemblyOverride()
        {
            using var f = new Fixture();
            f.Debug.SetIgnoreAssemblyMassLimit(true);
            Assert.That(f.Target.MaximumCarryMassKilograms, Is.EqualTo(35f));
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
        }

        [Test]
        public void OverrideDoesNotBypassDisabledKinematicOrAlreadyCarriedGuards()
        {
            using var f = new Fixture();
            f.Target.ConfigureAssemblyCarryLimit(120f);
            f.Debug.SetIgnoreAssemblyMassLimit(true);
            f.Target.SetPickupEnabled(false);
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
            f.Target.SetPickupEnabled(true);
            f.Body.isKinematic = true;
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
            f.Body.isKinematic = false;
            f.Target.NotifyPickedUp(f.Context);
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
            f.Target.NotifyReleased(PickupReleaseReason.Dropped);
            Assert.That(f.Target.CanPickup(f.Context), Is.True);
        }

        [Test]
        public void InvalidPersistentIdentityIsStillRejectedWithOverride()
        {
            using var f = new Fixture();
            f.Target.Configure(f.Body, null, "test", 35f);
            f.Target.ConfigureAssemblyCarryLimit(120f);
            f.Debug.SetIgnoreAssemblyMassLimit(true);
            Assert.That(f.Target.CanPickup(f.Context), Is.False);
        }

        [Test]
        public void DebugPermissionNeverChangesPhysicsOrSerializedSessionState()
        {
            using var f = new Fixture();
            f.Target.ConfigureAssemblyCarryLimit(120f);
            f.Body.useGravity = true;
            f.Body.centerOfMass = new Vector3(.1f, .2f, .3f);
            f.Body.linearVelocity = new Vector3(1f, 2f, 3f);
            string before = JsonUtility.ToJson(f.Debug);
            f.Debug.SetIgnoreAssemblyMassLimit(true);
            Assert.That(f.Target.CanPickup(f.Context), Is.True);
            Assert.That(f.Body.mass, Is.EqualTo(177.6f));
            Assert.That(f.Body.useGravity, Is.True);
            Assert.That(f.Body.isKinematic, Is.False);
            Assert.That(f.Body.centerOfMass, Is.EqualTo(new Vector3(.1f, .2f, .3f)));
            Assert.That(f.Body.linearVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(f.Target.MaximumCarryMassKilograms, Is.EqualTo(120f));
            Assert.That(JsonUtility.ToJson(f.Debug), Is.EqualTo(before));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Player = new GameObject("player A");
            public readonly GameObject OtherPlayer = new GameObject("player B");
            private readonly GameObject item = new GameObject("carry permission fixture");
            public readonly Rigidbody Body;
            public readonly PhysicsPickupTarget Target;
            public readonly AssemblyCarryDebugOverride Debug;
            public InteractionContext Context => new InteractionContext(Player, Vector3.zero, Vector3.forward);

            public Fixture()
            {
                Debug = Player.AddComponent<AssemblyCarryDebugOverride>();
                OtherPlayer.AddComponent<AssemblyCarryDebugOverride>();
                var identity = item.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                Body = item.AddComponent<Rigidbody>(); Body.mass = 177.6f; Body.useGravity = false;
                Target = item.AddComponent<PhysicsPickupTarget>(); Target.Configure(Body, identity, "test", 35f);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(item); Object.DestroyImmediate(Player); Object.DestroyImmediate(OtherPlayer);
            }
        }
    }
}
