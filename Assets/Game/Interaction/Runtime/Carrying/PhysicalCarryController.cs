using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Carrying
{
    [DisallowMultipleComponent]
    public sealed class PhysicalCarryController : MonoBehaviour
    {
        private readonly Collider[] overlapBuffer = new Collider[24];

        [SerializeField]
        private Transform carryAnchor;

        [SerializeField]
        private Collider playerCollider;

        [SerializeField, Min(0.1f)]
        private float followAcceleration = 55f;

        [SerializeField, Min(0.1f)]
        private float maximumFollowSpeed = 12f;

        [SerializeField, Min(0.1f)]
        private float maximumCarrySeparation = 3.5f;

        [SerializeField, Min(0f)]
        private float throwImpulse = 8f;

        [SerializeField, Min(0f)]
        private float placementClearance = 0.02f;

        private IPickupTarget heldTarget;
        private Rigidbody heldBody;
        private Collider[] heldColliders;
        private RigidbodyState originalState;
        private Quaternion targetRotation;

        public bool HasHeldObject => heldBody != null && IsAlive(heldTarget);

        public IPickupTarget HeldTarget => HasHeldObject ? heldTarget : null;

        public Rigidbody HeldBody => HasHeldObject ? heldBody : null;

        public string HeldStableId => HasHeldObject ? heldTarget.StableId.Value : string.Empty;

        public bool TryPickup(IPickupTarget target, in InteractionContext context)
        {
            if (HasHeldObject || target == null || !target.CanPickup(context) || target.Body == null)
            {
                return false;
            }

            heldTarget = target;
            heldBody = target.Body;
            originalState = RigidbodyState.Capture(heldBody);
            targetRotation = heldBody.rotation;
            heldColliders = heldBody.GetComponentsInChildren<Collider>(includeInactive: false);

            heldBody.isKinematic = false;
            heldBody.useGravity = false;
            heldBody.interpolation = RigidbodyInterpolation.Interpolate;
            heldBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            heldBody.linearDamping = Mathf.Max(heldBody.linearDamping, 8f);
            heldBody.angularDamping = Mathf.Max(heldBody.angularDamping, 8f);

            SetPlayerCollisionIgnored(true);
            target.NotifyPickedUp(context);
            return true;
        }

        public bool Drop(PickupReleaseReason reason = PickupReleaseReason.Dropped)
        {
            if (!HasHeldObject)
            {
                return false;
            }

            Release(reason);
            return true;
        }

        public bool Throw(Vector3 direction)
        {
            if (!HasHeldObject)
            {
                return false;
            }

            Rigidbody body = heldBody;
            Release(PickupReleaseReason.Thrown);
            body.AddForce(direction.normalized * throwImpulse, ForceMode.Impulse);
            return true;
        }

        public bool TryPlace(Vector3 surfacePoint, Vector3 surfaceNormal, int collisionMask)
        {
            if (!HasHeldObject)
            {
                return false;
            }

            Bounds bounds = CalculateHeldBounds();
            Vector3 normal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector3.up;
            float normalExtent = Mathf.Abs(normal.x) * bounds.extents.x +
                Mathf.Abs(normal.y) * bounds.extents.y +
                Mathf.Abs(normal.z) * bounds.extents.z;
            Vector3 position = surfacePoint + normal * (normalExtent + placementClearance);
            Vector3 offsetFromBody = bounds.center - heldBody.position;
            Vector3 overlapCenter = position + offsetFromBody;

            int count = Physics.OverlapBoxNonAlloc(
                overlapCenter,
                bounds.extents * 0.92f,
                overlapBuffer,
                targetRotation,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider overlap = overlapBuffer[i];
                if (overlap != null && !IsHeldCollider(overlap) && overlap != playerCollider)
                {
                    return false;
                }
            }

            heldBody.position = position;
            heldBody.rotation = targetRotation;
            Release(PickupReleaseReason.Placed);
            return true;
        }

        public bool TryHandoff(IMountHandoffTarget mountTarget, in InteractionContext context)
        {
            if (!HasHeldObject || mountTarget == null || !mountTarget.CanAccept(heldTarget, context))
            {
                return false;
            }

            IPickupTarget releasedTarget = heldTarget;
            Release(PickupReleaseReason.MountHandoff);
            mountTarget.Accept(releasedTarget, context);
            return true;
        }

        public void RotateHeld(Vector2 degrees)
        {
            if (!HasHeldObject)
            {
                return;
            }

            Quaternion yaw = Quaternion.AngleAxis(degrees.x, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(-degrees.y, Vector3.right);
            targetRotation = yaw * targetRotation * pitch;
        }

        public CarriedObjectSaveState CaptureSaveState()
        {
            if (!HasHeldObject || carryAnchor == null || !heldTarget.StableId.IsValid)
            {
                return CarriedObjectSaveState.Empty();
            }

            return CarriedObjectSaveState.Create(
                heldTarget.StableId,
                carryAnchor.InverseTransformPoint(heldBody.position),
                Quaternion.Inverse(carryAnchor.rotation) * heldBody.rotation);
        }

        public void Configure(Transform anchor, Collider ownerCollider)
        {
            carryAnchor = anchor;
            playerCollider = ownerCollider;
        }

        private void FixedUpdate()
        {
            if (!HasHeldObject && (heldTarget != null || heldBody != null))
            {
                Release(PickupReleaseReason.TargetLost);
                return;
            }

            if (!HasHeldObject)
            {
                return;
            }

            if (carryAnchor == null || heldBody == null || heldTarget == null)
            {
                Drop(PickupReleaseReason.TargetLost);
                return;
            }

            Vector3 offset = carryAnchor.position - heldBody.position;
            if (offset.sqrMagnitude > maximumCarrySeparation * maximumCarrySeparation)
            {
                Drop(PickupReleaseReason.TargetLost);
                return;
            }

            Vector3 desiredVelocity = Vector3.ClampMagnitude(offset * followAcceleration, maximumFollowSpeed);
            heldBody.linearVelocity = Vector3.MoveTowards(
                heldBody.linearVelocity,
                desiredVelocity,
                followAcceleration * Time.fixedDeltaTime);

            Quaternion delta = targetRotation * Quaternion.Inverse(heldBody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (axis.sqrMagnitude > 0.0001f && !float.IsNaN(axis.x))
            {
                heldBody.angularVelocity = Vector3.ClampMagnitude(
                    axis.normalized * (angleDegrees * Mathf.Deg2Rad * 10f),
                    20f);
            }
        }

        private void OnDisable()
        {
            ReleaseOwnedState();
        }

        private void OnDestroy()
        {
            ReleaseOwnedState();
        }

        private void Release(PickupReleaseReason reason)
        {
            IPickupTarget releasedTarget = heldTarget;
            Rigidbody releasedBody = heldBody;

            SetPlayerCollisionIgnored(false);
            originalState.Restore(releasedBody);
            if (IsAlive(releasedTarget))
            {
                releasedTarget.NotifyReleased(reason);
            }

            heldTarget = null;
            heldBody = null;
            heldColliders = null;
            originalState = default;
            targetRotation = Quaternion.identity;
        }

        private void ReleaseOwnedState()
        {
            if (heldTarget == null && heldBody == null && heldColliders == null)
            {
                return;
            }

            Release(PickupReleaseReason.TargetLost);
        }

        private Bounds CalculateHeldBounds()
        {
            if (heldColliders == null || heldColliders.Length == 0)
            {
                return new Bounds(heldBody.position, Vector3.one * 0.25f);
            }

            Bounds bounds = heldColliders[0].bounds;
            for (int i = 1; i < heldColliders.Length; i++)
            {
                if (heldColliders[i] != null)
                {
                    bounds.Encapsulate(heldColliders[i].bounds);
                }
            }

            return bounds;
        }

        private bool IsHeldCollider(Collider candidate)
        {
            if (heldColliders == null)
            {
                return false;
            }

            for (int i = 0; i < heldColliders.Length; i++)
            {
                if (heldColliders[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetPlayerCollisionIgnored(bool ignored)
        {
            if (playerCollider == null || heldColliders == null)
            {
                return;
            }

            for (int i = 0; i < heldColliders.Length; i++)
            {
                Collider heldCollider = heldColliders[i];
                if (heldCollider != null && heldCollider != playerCollider)
                {
                    Physics.IgnoreCollision(playerCollider, heldCollider, ignored);
                }
            }
        }

        private static bool IsAlive(IPickupTarget target)
        {
            return target != null && (!(target is UnityEngine.Object unityObject) || unityObject != null);
        }

        private readonly struct RigidbodyState
        {
            private RigidbodyState(
                bool useGravity,
                bool isKinematic,
                CollisionDetectionMode collisionDetectionMode,
                RigidbodyInterpolation interpolation,
                float linearDamping,
                float angularDamping)
            {
                UseGravity = useGravity;
                IsKinematic = isKinematic;
                CollisionDetectionMode = collisionDetectionMode;
                Interpolation = interpolation;
                LinearDamping = linearDamping;
                AngularDamping = angularDamping;
            }

            private bool UseGravity { get; }

            private bool IsKinematic { get; }

            private CollisionDetectionMode CollisionDetectionMode { get; }

            private RigidbodyInterpolation Interpolation { get; }

            private float LinearDamping { get; }

            private float AngularDamping { get; }

            public static RigidbodyState Capture(Rigidbody body)
            {
                return new RigidbodyState(
                    body.useGravity,
                    body.isKinematic,
                    body.collisionDetectionMode,
                    body.interpolation,
                    body.linearDamping,
                    body.angularDamping);
            }

            public void Restore(Rigidbody body)
            {
                if (body == null)
                {
                    return;
                }

                body.useGravity = UseGravity;
                body.isKinematic = IsKinematic;
                body.collisionDetectionMode = CollisionDetectionMode;
                body.interpolation = Interpolation;
                body.linearDamping = LinearDamping;
                body.angularDamping = AngularDamping;
            }
        }
    }
}
