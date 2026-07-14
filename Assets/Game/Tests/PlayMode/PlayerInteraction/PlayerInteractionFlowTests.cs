using System.Collections;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class PlayerInteractionFlowTests
    {
        [UnityTest]
        public IEnumerator HeldBodyDoesNotOccludeMountHandoffThroughPlayerController()
        {
            InteractionRig rig = InteractionRig.Create();
            GameObject mountObject = null;
            try
            {
                Physics.SyncTransforms();
                rig.Interaction.RefreshCandidate();
                Assert.That(rig.Interaction.TryPrimaryInteraction(), Is.True);
                Assert.That(rig.Carry.HasHeldObject, Is.True);

                mountObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mountObject.name = "RuntimeMount";
                mountObject.transform.position = Vector3.forward * 2f;
                mountObject.transform.localScale = Vector3.one * 0.5f;
                var poseObject = new GameObject("MountPose");
                poseObject.transform.SetParent(mountObject.transform, false);
                var mount = mountObject.AddComponent<PrototypeMountHandoffTarget>();
                mount.Configure(poseObject.transform);
                mountObject.AddComponent<InteractionTargetHost>().Configure(mount);

                Physics.SyncTransforms();
                rig.Interaction.RefreshCandidate();

                Assert.That(rig.Interaction.CurrentCandidate.Host.gameObject, Is.SameAs(mountObject));
                Assert.That(rig.Interaction.TryPrimaryInteraction(), Is.True);
                Assert.That(rig.Carry.HasHeldObject, Is.False);
                Assert.That(mount.HasMountedTarget, Is.True);
                yield return null;
            }
            finally
            {
                if (mountObject != null)
                {
                    Object.DestroyImmediate(mountObject);
                }

                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator DestroyingCarryOwnerRestoresPersistentHeldBody()
        {
            InteractionRig rig = InteractionRig.Create();
            Rigidbody itemBody = rig.ItemBody;
            PhysicsPickupTarget pickupTarget = rig.PickupTarget;

            Assert.That(rig.Carry.TryPickup(pickupTarget, rig.CreateContext()), Is.True);
            Assert.That(itemBody.useGravity, Is.False);

            Object.Destroy(rig.Owner);
            rig.Owner = null;
            yield return null;

            Assert.That(itemBody, Is.Not.Null);
            Assert.That(itemBody.useGravity, Is.True);
            Assert.That(itemBody.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.Continuous));
            Assert.That(pickupTarget.IsCarried, Is.False);
            rig.Dispose();
        }

        [UnityTest]
        public IEnumerator DisablingCarryControllerRestoresPhysicsAndCollisionOwnership()
        {
            InteractionRig rig = InteractionRig.Create();
            try
            {
                Assert.That(rig.Carry.TryPickup(rig.PickupTarget, rig.CreateContext()), Is.True);
                Assert.That(Physics.GetIgnoreCollision(rig.OwnerCollider, rig.ItemCollider), Is.True);

                rig.Carry.enabled = false;
                yield return null;

                Assert.That(rig.ItemBody.useGravity, Is.True);
                Assert.That(
                    rig.ItemBody.collisionDetectionMode,
                    Is.EqualTo(CollisionDetectionMode.Continuous));
                Assert.That(Physics.GetIgnoreCollision(rig.OwnerCollider, rig.ItemCollider), Is.False);
                Assert.That(rig.PickupTarget.IsCarried, Is.False);
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator DisablingInputRouterClearsMotorIntent()
        {
            var root = new GameObject("RuntimeInputRouterTest");
            root.SetActive(false);
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();

            try
            {
                CharacterController characterController = root.AddComponent<CharacterController>();
                FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();
                motor.Configure(characterController, root.transform);
                PlayerInputRouter router = root.AddComponent<PlayerInputRouter>();
                AddRequiredPlayerActions(actions);
                router.Configure(actions, motor, null, null);

                root.SetActive(true);
                yield return null;
                motor.SetMoveInput(Vector2.one);
                motor.SetCrouchRequested(true);

                router.enabled = false;

                Assert.That(ReadField<Vector2>(motor, "moveInput"), Is.EqualTo(Vector2.zero));
                Assert.That(ReadField<bool>(motor, "crouchRequested"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
            }
        }

        private static void AddRequiredPlayerActions(InputActionAsset actions)
        {
            var map = new InputActionMap("Player");
            actions.AddActionMap(map);
            string[] names =
            {
                "Move",
                "Look",
                "Crouch",
                "Interact",
                "Drop",
                "Place",
                "Throw",
                "RotateModifier",
                "ToolActivate"
            };
            foreach (string name in names)
            {
                map.AddAction(name);
            }
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private sealed class InteractionRig
        {
            private const string TestStableId = "bb7a6d91f74042069d4052a0dd8bcb89";

            public GameObject Owner { get; set; }

            public GameObject Item { get; private set; }

            public Collider OwnerCollider { get; private set; }

            public Collider ItemCollider { get; private set; }

            public Rigidbody ItemBody { get; private set; }

            public PhysicsPickupTarget PickupTarget { get; private set; }

            public PhysicalCarryController Carry { get; private set; }

            public PlayerInteractionController Interaction { get; private set; }

            public static InteractionRig Create()
            {
                var rig = new InteractionRig
                {
                    Owner = new GameObject("RuntimeInteractionOwner")
                };
                BoxCollider ownerCollider = rig.Owner.AddComponent<BoxCollider>();
                ownerCollider.center = new Vector3(0f, -2f, 0f);
                rig.OwnerCollider = ownerCollider;
                var anchorObject = new GameObject("CarryAnchor");
                anchorObject.transform.SetParent(rig.Owner.transform, false);
                anchorObject.transform.localPosition = Vector3.forward;

                rig.Carry = rig.Owner.AddComponent<PhysicalCarryController>();
                rig.Carry.Configure(anchorObject.transform, ownerCollider);
                RaycastInteractionCandidateSource query =
                    rig.Owner.AddComponent<RaycastInteractionCandidateSource>();
                query.Configure(rig.Owner.transform, 3f, ~0);
                rig.Interaction = rig.Owner.AddComponent<PlayerInteractionController>();
                rig.Interaction.Configure(query, rig.Carry, rig.Owner.transform, ~0);

                rig.Item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rig.Item.name = "RuntimePickup";
                rig.Item.transform.position = Vector3.forward;
                rig.Item.transform.localScale = Vector3.one * 0.5f;
                rig.ItemCollider = rig.Item.GetComponent<Collider>();
                rig.ItemBody = rig.Item.AddComponent<Rigidbody>();
                rig.ItemBody.mass = 3f;
                rig.ItemBody.useGravity = true;
                rig.ItemBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                StableEntityIdAuthoring identity = rig.Item.AddComponent<StableEntityIdAuthoring>();
                SetStableId(identity, TestStableId);
                rig.PickupTarget = rig.Item.AddComponent<PhysicsPickupTarget>();
                rig.PickupTarget.Configure(rig.ItemBody, identity, "Поднять", 35f);
                rig.Item.AddComponent<InteractionTargetHost>().Configure(rig.PickupTarget);
                return rig;
            }

            public MSC.Interaction.InteractionContext CreateContext()
            {
                return new MSC.Interaction.InteractionContext(
                    Owner,
                    Owner.transform.position,
                    Owner.transform.forward);
            }

            public void Dispose()
            {
                if (Item != null)
                {
                    Object.DestroyImmediate(Item);
                    Item = null;
                }

                if (Owner != null)
                {
                    Object.DestroyImmediate(Owner);
                    Owner = null;
                }
            }

            private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
            {
                FieldInfo field = typeof(StableEntityIdAuthoring).GetField(
                    "stableId",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(authoring, stableId);
            }
        }
    }
}
