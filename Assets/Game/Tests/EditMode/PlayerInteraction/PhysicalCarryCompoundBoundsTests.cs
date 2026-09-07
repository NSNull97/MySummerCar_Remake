using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Carrying;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.PlayerInteraction
{
    public sealed class PhysicalCarryCompoundBoundsTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PlacementUsesOnlyLiveSolidsOfTheHeldBody(bool fallbackOnly)
        {
            var player = new GameObject("compound carry test player");
            var item = new GameObject("compound carry test body");
            try
            {
                var body = item.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.position = new Vector3(100f, 20f, 100f);
                var identity = item.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var pickup = item.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "test", 120f);
                item.AddComponent<BoxCollider>().enabled = !fallbackOnly;
                var disabled = AddShape(item.transform, "disabled installed source", Vector3.zero);
                disabled.enabled = false;
                var query = AddShape(item.transform, "large interaction query", Vector3.zero);
                query.isTrigger = true;
                query.size = Vector3.one * 100f;
                var foreign = AddShape(item.transform, "separate nested body", Vector3.up * 40f);
                foreign.gameObject.AddComponent<Rigidbody>().isKinematic = true;
                var compound = AddShape(item.transform, "actual compound contact", Vector3.up * 2f);
                compound.enabled = !fallbackOnly;
                Physics.SyncTransforms();

                var carry = player.AddComponent<PhysicalCarryController>();
                carry.Configure(player.transform, null);
                Assert.That(carry.TryPickup(pickup, new InteractionContext(
                    player, player.transform.position, Vector3.forward)), Is.True);
                Assert.That(carry.TryPlace(new Vector3(100f, 6f, 100f), Vector3.up, 0), Is.True);
                float expectedExtent = fallbackOnly ? .125f : 1.5f;
                Assert.That(body.position.y, Is.EqualTo(6f + expectedExtent + .02f).Within(.0001f));
                Assert.That(carry.HasHeldObject, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(item);
            }
        }

        private static BoxCollider AddShape(Transform parent, string label, Vector3 localPosition)
        {
            var child = new GameObject(label);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.AddComponent<BoxCollider>();
        }
    }
}
