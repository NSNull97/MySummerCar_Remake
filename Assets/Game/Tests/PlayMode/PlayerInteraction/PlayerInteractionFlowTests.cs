using System.Collections;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
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
    public sealed class PlayerInteractionFlowTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator PickupKeepsSelectedSurfacePointAtPhysicalCarryAnchor()
        {
            InteractionRig rig = InteractionRig.Create();
            try
            {
                rig.Item.transform.position = Vector3.forward * 2f;
                rig.ItemBody.position = rig.Item.transform.position;
                Physics.SyncTransforms();
                rig.Interaction.RefreshCandidate();
                Vector3 selectedPoint = rig.Interaction.CurrentCandidate.Point;
                InteractionActionSnapshot pickupSnapshot =
                    rig.Interaction.CurrentActionSnapshot;
                Assert.That(
                    pickupSnapshot.Reticle,
                    Is.EqualTo(InteractionReticleKind.Pickup));
                Assert.That(pickupSnapshot.First.Label, Is.EqualTo("ВЗЯТЬ"));
                Vector3 localGrabPoint =
                    Quaternion.Inverse(rig.ItemBody.rotation) *
                    (selectedPoint - rig.ItemBody.position);

                Assert.That(rig.Interaction.TryPickupOrPlace(), Is.True);
                Assert.That(rig.ItemBody.isKinematic, Is.False);
                Assert.That(rig.ItemBody.detectCollisions, Is.True);
                Assert.That(
                    rig.ItemBody.collisionDetectionMode,
                    Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));

                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Vector3 currentGrabPoint = rig.ItemBody.position +
                    rig.ItemBody.rotation * localGrabPoint;
                Assert.That(
                    Vector3.Distance(currentGrabPoint, rig.Anchor.position),
                    Is.LessThan(0.08f));
            }
            finally
            {
                rig.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator NearGrabKeepsItsDistanceWhileOwnerRunsAndStops()
        {
            InteractionRig rig = InteractionRig.Create();
            try
            {
                Physics.SyncTransforms();
                rig.Interaction.RefreshCandidate();
                Vector3 selectedPoint = rig.Interaction.CurrentCandidate.Point;
                Vector3 localGrabPoint =
                    Quaternion.Inverse(rig.ItemBody.rotation) *
                    (selectedPoint - rig.ItemBody.position);
                float selectedLocalDistance =
                    rig.Owner.transform.InverseTransformPoint(selectedPoint).z;

                Assert.That(selectedLocalDistance, Is.LessThan(1f));
                Assert.That(rig.Interaction.TryPickupOrPlace(), Is.True);

                for (int i = 0; i < 10; i++)
                {
                    rig.Owner.transform.position += Vector3.forward * 0.1f;
                    yield return new WaitForFixedUpdate();
                }

                Vector3 movingGrabPoint = rig.ItemBody.position +
                    rig.ItemBody.rotation * localGrabPoint;
                float movingLocalDistance =
                    rig.Owner.transform.InverseTransformPoint(movingGrabPoint).z;
                Assert.That(
                    movingLocalDistance,
                    Is.EqualTo(selectedLocalDistance).Within(0.08f));
                Assert.That(
                    movingLocalDistance,
                    Is.LessThan(rig.Anchor.localPosition.z - 0.15f));

                yield return new WaitForFixedUpdate();

                Assert.That(
                    Mathf.Abs(rig.ItemBody.linearVelocity.z),
                    Is.LessThan(1f));
            }
            finally
            {
                rig.Dispose();
            }
        }

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
                InteractionActionSnapshot installSnapshot =
                    rig.Interaction.CurrentActionSnapshot;
                Assert.That(
                    installSnapshot.Reticle,
                    Is.EqualTo(InteractionReticleKind.Install));
                Assert.That(
                    installSnapshot.First.Label,
                    Is.EqualTo("УСТАНОВИТЬ"));
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
        public IEnumerator RemovalCapabilityUsesTheDedicatedCrossReticle()
        {
            InteractionRig rig = InteractionRig.Create();
            GameObject removalObject = null;
            try
            {
                rig.Item.SetActive(false);
                removalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                removalObject.name = "RuntimeRemovalTarget";
                removalObject.transform.position = Vector3.forward;
                removalObject.transform.localScale = Vector3.one * 0.5f;
                var removalTarget =
                    removalObject.AddComponent<TestRemovalInteractionTarget>();
                removalObject.AddComponent<InteractionTargetHost>()
                    .Configure(removalTarget);

                Physics.SyncTransforms();
                rig.Interaction.RefreshCandidate();
                InteractionActionSnapshot snapshot =
                    rig.Interaction.CurrentActionSnapshot;

                Assert.That(snapshot.Reticle,
                    Is.EqualTo(InteractionReticleKind.Remove));
                Assert.That(snapshot.First.Binding,
                    Is.EqualTo(InteractionActionBinding.Throw));
                Assert.That(snapshot.First.Label, Is.EqualTo("СНЯТЬ"));
                Assert.That(
                    rig.Interaction.CurrentDisplayName,
                    Is.EqualTo("Тестовая деталь"));
                yield return null;
            }
            finally
            {
                if (removalObject != null)
                {
                    Object.DestroyImmediate(removalObject);
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
        public IEnumerator ThrowRepairsLegacyLooseGravityAndFallsAfterLaunch()
        {
            InteractionRig rig = InteractionRig.Create();
            try
            {
                rig.Item.transform.position = new Vector3(0f, 2f, 1f);
                rig.ItemBody.position = rig.Item.transform.position;
                rig.ItemBody.useGravity = false;

                Assert.That(
                    rig.Carry.TryPickup(rig.PickupTarget, rig.CreateContext()),
                    Is.True);
                Assert.That(rig.Carry.Throw(Vector3.forward), Is.True);
                Assert.That(rig.ItemBody.useGravity, Is.True);
                Assert.That(rig.ItemBody.isKinematic, Is.False);
                Assert.That(rig.ItemBody.detectCollisions, Is.True);
                float launchVerticalVelocity = rig.ItemBody.linearVelocity.y;

                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                Assert.That(
                    rig.ItemBody.linearVelocity.y,
                    Is.LessThan(launchVerticalVelocity - 0.1f));
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
                Assert.That(ReadField<bool>(motor, "runRequested"), Is.False);
                Assert.That(ReadField<bool>(motor, "forwardLeanRequested"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
            }
        }

        [UnityTest]
        public IEnumerator HoldingAlt_ExposesAlternativeActionLayerState()
        {
            var root = new GameObject("AlternativeActionInputTest");
            root.SetActive(false);
            InputActionAsset actions =
                ScriptableObject.CreateInstance<InputActionAsset>();
            Keyboard keyboard = null;

            try
            {
                AddRequiredPlayerActions(actions);
                PlayerInputRouter router = root.AddComponent<PlayerInputRouter>();
                router.Configure(actions, null, null, null);
                keyboard = InputSystem.AddDevice<Keyboard>();

                root.SetActive(true);
                yield return null;

                Press(keyboard.leftAltKey);
                yield return null;
                Assert.That(router.IsAlternativeActionsHeld, Is.True);

                Release(keyboard.leftAltKey);
                yield return null;
                Assert.That(router.IsAlternativeActionsHeld, Is.False);
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

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
                "Run",
                "Jump",
                "Zoom",
                "ForwardLean",
                "Interact",
                "Drop",
                "Place",
                "Throw",
                "RotateModifier",
                "RotateAxis",
                "ToolActivate",
                "Urinate",
                "Wave",
                "MiddleFinger",
                "Swear",
                "AlternativeActions"
            };
            foreach (string name in names)
            {
                map.AddAction(
                    name,
                    name == "AlternativeActions"
                        ? InputActionType.Button
                        : InputActionType.Value);
            }

            map.FindAction("AlternativeActions")
                .AddBinding("<Keyboard>/leftAlt", groups: "Keyboard&Mouse");
            map.FindAction("AlternativeActions")
                .AddBinding("<Keyboard>/rightAlt", groups: "Keyboard&Mouse");
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

            public Transform Anchor { get; private set; }

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
                rig.Anchor = anchorObject.transform;

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
                rig.PickupTarget.Configure(
                    rig.ItemBody,
                    identity,
                    "Поднять",
                    35f,
                    useGravityWhenLoose: true);
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

    public sealed class TestRemovalInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        ISecondaryInteractionOnlyTarget,
        IRemovalInteractionTarget,
        IInteractionDisplayTarget
    {
        public string InteractionPrompt => "СНЯТЬ";

        public string InteractionDisplayName => "Тестовая деталь";

        public bool CanInteract(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy;

        public void Interact(in InteractionContext context)
        {
        }
    }
}
