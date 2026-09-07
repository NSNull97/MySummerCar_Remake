using System;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Notifications;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Interaction.Carrying
{
    public enum CarryRotationAxis
    {
        Yaw = 0,
        Pitch = 1,
        Roll = 2,
    }

    [DisallowMultipleComponent]
    public sealed class PhysicalCarryController : MonoBehaviour, IInteractionActionSource
    {
        private readonly Collider[] overlapBuffer = new Collider[24];

        [SerializeField]
        private Transform carryAnchor;

        [SerializeField]
        private Collider playerCollider;

        [SerializeField, Min(0.1f)]
        private float followAcceleration = 110f;

        [SerializeField, Range(0f, 1f)]
        private float followVelocityRetention = 0.82f;

        [SerializeField, Min(0.1f)]
        private float maximumFollowSpeed = 18f;

        [SerializeField, Range(0f, 1f)]
        private float ownerMotionInheritance = 0.96f;

        [SerializeField, Min(0.1f)]
        private float maximumInheritedOwnerSpeed = 12f;

        [SerializeField, Min(0.05f)]
        private float minimumHoldDistance = 0.32f;

        [SerializeField, Min(0f)]
        private float heldLinearDamping = 1f;

        [SerializeField, Min(0f)]
        private float heldAngularDamping = 2.5f;

        [SerializeField, Min(0.1f)]
        private float rotationResponsePerSecond = 14f;

        [SerializeField, Min(0.1f)]
        private float maximumAngularSpeed = 24f;

        [SerializeField, Min(0.1f)]
        private float maximumCarrySeparation = 3.5f;

        [SerializeField, Min(0f)]
        private float throwImpulse = 8f;

        [SerializeField, Min(0f)]
        private float placementClearance = 0.02f;

        private IPickupTarget heldTarget;
        private Rigidbody heldBody;
        private Collider[] heldColliders;
        private Collider[] heldScopeColliders;
        private InteractionTargetHost heldCapabilityHost;
        private RigidbodyState originalState;
        private Quaternion targetLocalRotation;
        private Vector3 heldGrabLocalOffset;
        private bool hasHeldGrabPoint;
        private Vector3 adaptiveCarryAnchorLocalPosition;
        private bool hasAdaptiveCarryAnchorPosition;
        private Vector3 previousOwnerPosition;
        private Vector3 previousInheritedOwnerVelocity;
        private bool hasOwnerMotionSample;
        private IContinuousHeldActivationTarget activeContinuousActivation;
        private bool heldUseReady;
        private Transform presentationAnchor;
        private Vector3 presentationLocalOffset;
        private Quaternion presentationRotationOffset = Quaternion.identity;
        private float presentationPoseWeight;
        private RigidbodyState presentationHoldState;
        private bool hardPresentationHoldActive;

        public event Action<InteractionActionCompleted> ActionCompleted;
        public event Action<bool> HeldUseReadyChanged;

        public bool HasHeldObject => heldBody != null && IsAlive(heldTarget);

        public IPickupTarget HeldTarget => HasHeldObject ? heldTarget : null;

        public Rigidbody HeldBody => HasHeldObject ? heldBody : null;

        public string HeldStableId => HasHeldObject ? heldTarget.StableId.Value : string.Empty;

        public bool HasActiveContinuousHeldActivation =>
            activeContinuousActivation != null &&
            activeContinuousActivation.IsContinuousHeldActivationActive;

        public bool IsHeldUseReady => HasHeldObject && heldUseReady;

        /// <summary>
        /// Returns the world-authoritative physics flags that existed before
        /// this controller took ownership of the body. Carry and first-person
        /// presentation physics are transient and must not leak into saves.
        /// </summary>
        public bool TryGetHeldWorldPersistencePhysics(
            Rigidbody candidate,
            out bool isKinematic,
            out bool useGravity)
        {
            if (!HasHeldObject || candidate == null || candidate != heldBody)
            {
                isKinematic = false;
                useGravity = false;
                return false;
            }

            isKinematic = originalState.IsKinematic;
            useGravity = originalState.UseGravity;
            return true;
        }

        public bool TryPickup(IPickupTarget target, in InteractionContext context)
        {
            return TryPickupInternal(
                target,
                context,
                Vector3.zero,
                captureGrabPoint: false);
        }

        /// <summary>
        /// Picks the body up by the exact world point selected by the player.
        /// The point remains attached to the carry anchor while the body
        /// rotates, avoiding the common "teleport the pivot to the camera"
        /// snap on long or asymmetrical objects.
        /// </summary>
        public bool TryPickup(
            IPickupTarget target,
            in InteractionContext context,
            Vector3 worldGrabPoint)
        {
            return TryPickupInternal(
                target,
                context,
                worldGrabPoint,
                captureGrabPoint: IsFinite(worldGrabPoint));
        }

        private bool TryPickupInternal(
            IPickupTarget target,
            in InteractionContext context,
            Vector3 worldGrabPoint,
            bool captureGrabPoint)
        {
            if (HasHeldObject || target == null || !target.CanPickup(context) || target.Body == null)
            {
                return false;
            }

            heldTarget = target;
            heldBody = target.Body;
            heldCapabilityHost = FindCapabilityHost(target);
            originalState = RigidbodyState.Capture(heldBody);
            ClearHeldPresentationPose();
            targetLocalRotation = carryAnchor != null
                ? Quaternion.Inverse(carryAnchor.rotation) * heldBody.rotation
                : heldBody.rotation;
            hasHeldGrabPoint = captureGrabPoint;
            heldGrabLocalOffset = captureGrabPoint
                ? Quaternion.Inverse(heldBody.rotation) *
                  (worldGrabPoint - heldBody.position)
                : Vector3.zero;
            ConfigureAdaptiveCarryAnchor(
                worldGrabPoint,
                captureGrabPoint);
            ResetOwnerMotionTracking();
            heldColliders = heldBody.GetComponentsInChildren<Collider>(includeInactive: false);

            heldBody.isKinematic = false;
            heldBody.useGravity = false;
            heldBody.interpolation = RigidbodyInterpolation.Interpolate;
            heldBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            heldBody.linearDamping = heldLinearDamping;
            heldBody.angularDamping = heldAngularDamping;

            SetPlayerCollisionIgnored(true);
            SetOwnerScopeCollisionIgnored(true);
            target.NotifyPickedUp(context);
            PublishCompletedAction(
                InteractionActionKind.Pickup,
                target.StableId,
                heldBody.position);
            return true;
        }

        public bool Drop(PickupReleaseReason reason = PickupReleaseReason.Dropped)
        {
            if (!HasHeldObject)
            {
                return false;
            }

            StableEntityId targetStableId = heldTarget.StableId;
            Vector3 worldPosition = heldBody.position;
            Release(reason);
            if (reason == PickupReleaseReason.Dropped)
            {
                PublishCompletedAction(
                    InteractionActionKind.Drop,
                    targetStableId,
                    worldPosition);
            }

            return true;
        }

        public bool Throw(Vector3 direction)
        {
            if (!HasHeldObject)
            {
                return false;
            }

            Rigidbody body = heldBody;
            StableEntityId targetStableId = heldTarget.StableId;
            Release(PickupReleaseReason.Thrown);
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
            Vector3 launchDirection =
                (normalizedDirection + Vector3.up * 0.08f).normalized;
            body.AddForce(launchDirection * throwImpulse, ForceMode.Impulse);
            PublishCompletedAction(
                InteractionActionKind.Throw,
                targetStableId,
                body.position);
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

            Quaternion placementRotation = GetTargetWorldRotation();
            int count = Physics.OverlapBoxNonAlloc(
                overlapCenter,
                bounds.extents * 0.92f,
                overlapBuffer,
                placementRotation,
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

            Rigidbody placedBody = heldBody;
            StableEntityId targetStableId = heldTarget.StableId;
            placedBody.position = position;
            placedBody.rotation = placementRotation;
            Release(PickupReleaseReason.Placed);
            PublishCompletedAction(
                InteractionActionKind.Place,
                targetStableId,
                placedBody.position);
            return true;
        }

        public bool TryHandoff(IMountHandoffTarget mountTarget, in InteractionContext context)
        {
            if (!HasHeldObject || mountTarget == null ||
                !mountTarget.CanAccept(heldTarget, context))
            {
                return false;
            }

            IPickupTarget releasedTarget = heldTarget;
            if (mountTarget is IMountHandoffPreReleaseTarget preReleaseTarget &&
                !preReleaseTarget.TryPrepareHandoff(releasedTarget, context))
            {
                // The attempted mount may fail with a mechanical side effect.
                // Consume the click, not the held item; do not fall through to
                // ordinary LMB drop or publish a successful handoff notification.
                return true;
            }

            StableEntityId targetStableId = releasedTarget.StableId;
            Vector3 releasePosition = heldBody.position;
            Release(PickupReleaseReason.MountHandoff);
            mountTarget.Accept(releasedTarget, context);
            Rigidbody acceptedBody = releasedTarget.Body;
            PublishCompletedAction(
                InteractionActionKind.MountHandoff,
                targetStableId,
                acceptedBody != null ? acceptedBody.position : releasePosition);
            return true;
        }

        public bool TryActivateHeld(in InteractionContext context)
        {
            if (!HasHeldObject ||
                heldCapabilityHost == null ||
                !heldCapabilityHost.TryGetCapability(out IHeldActivationTarget target) ||
                !target.CanActivateHeld(context))
            {
                return false;
            }

            target.ActivateHeld(context);
            if (target is IHeldActivationReleaseRequest releaseRequest)
            {
                switch (releaseRequest.ConsumeHeldActivationReleaseRequest())
                {
                    case HeldActivationReleaseMode.Release:
                        Drop(PickupReleaseReason.Consumed);
                        break;

                    case HeldActivationReleaseMode.Throw:
                        Throw(context.Direction);
                        break;
                }
            }

            return true;
        }

        public bool TryBeginContinuousHeldActivation(
            in InteractionContext context)
        {
            if (HasActiveContinuousHeldActivation)
            {
                return true;
            }

            if (!IsHeldUseReady ||
                heldCapabilityHost == null ||
                !heldCapabilityHost.TryGetCapability(
                    out IContinuousHeldActivationTarget target) ||
                !target.CanBeginContinuousHeldActivation(context))
            {
                return false;
            }

            target.BeginContinuousHeldActivation(context);
            if (!target.IsContinuousHeldActivationActive)
            {
                return false;
            }

            activeContinuousActivation = target;
            return true;
        }

        /// <summary>
        /// Picks up the real item and prepares it for a later held activation.
        /// No content is consumed until a subsequent activation begins.
        /// </summary>
        public bool TryPickupForContinuousHeldActivation(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            return TryPickupForContinuousHeldActivationInternal(
                pickupTarget,
                context,
                Vector3.zero,
                captureGrabPoint: false);
        }

        public bool TryPickupForContinuousHeldActivation(
            IPickupTarget pickupTarget,
            in InteractionContext context,
            Vector3 worldGrabPoint)
        {
            return TryPickupForContinuousHeldActivationInternal(
                pickupTarget,
                context,
                worldGrabPoint,
                captureGrabPoint: IsFinite(worldGrabPoint));
        }

        private bool TryPickupForContinuousHeldActivationInternal(
            IPickupTarget pickupTarget,
            in InteractionContext context,
            Vector3 worldGrabPoint,
            bool captureGrabPoint)
        {
            if (pickupTarget == null)
            {
                return false;
            }

            InteractionTargetHost capabilityHost =
                FindCapabilityHost(pickupTarget);
            if (capabilityHost == null ||
                !capabilityHost.TryGetCapability(
                    out IContinuousActivationPickupTarget pickupActivation) ||
                !pickupActivation.CanPickupForContinuousActivation(context) ||
                !capabilityHost.TryGetCapability(
                    out IContinuousHeldActivationTarget activationTarget) ||
                !activationTarget.CanBeginContinuousHeldActivation(context) ||
                !TryPickupInternal(
                    pickupTarget,
                    context,
                    worldGrabPoint,
                    captureGrabPoint))
            {
                return false;
            }

            SetHeldUseReady(true);
            return true;
        }

        /// <summary>
        /// Moves an ordinarily carried continuous-use item into its transient
        /// use-ready state without consuming it.
        /// </summary>
        public bool TryPrepareHeldContinuousActivation(
            in InteractionContext context)
        {
            if (IsHeldUseReady)
            {
                return true;
            }

            if (!HasHeldObject ||
                heldCapabilityHost == null ||
                !heldCapabilityHost.TryGetCapability(
                    out IContinuousHeldActivationTarget target) ||
                !target.CanBeginContinuousHeldActivation(context))
            {
                return false;
            }

            SetHeldUseReady(true);
            return true;
        }

        /// <summary>
        /// Performs the Phase 1 use-pickup contract as one bounded operation.
        /// The continuous capability is preflighted before pickup so invalid
        /// targets cannot emit a transient pickup notification.
        /// </summary>
        public bool TryPickupAndBeginContinuousHeldActivation(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (!TryPickupForContinuousHeldActivation(
                    pickupTarget,
                    context))
            {
                return false;
            }

            if (TryBeginContinuousHeldActivation(context))
            {
                return true;
            }

            Drop(PickupReleaseReason.TargetLost);
            return false;
        }

        public bool ContinueContinuousHeldActivation(
            float unscaledDeltaTime)
        {
            if (!HasActiveContinuousHeldActivation)
            {
                activeContinuousActivation = null;
                return false;
            }

            bool canContinue =
                activeContinuousActivation.ContinueContinuousHeldActivation(
                    unscaledDeltaTime);
            if (canContinue)
            {
                return true;
            }

            EndContinuousHeldActivation();
            return false;
        }

        public void EndContinuousHeldActivation()
        {
            IContinuousHeldActivationTarget target =
                activeContinuousActivation;
            activeContinuousActivation = null;
            if (target != null &&
                target.IsContinuousHeldActivationActive)
            {
                target.EndContinuousHeldActivation();
            }
        }

        /// <summary>
        /// Applies a transient camera-local action pose to the real carried
        /// Rigidbody. It never reparents, clones or replaces the item.
        /// </summary>
        public void SetHeldPresentationPose(
            Vector3 localPositionOffset,
            Quaternion localRotationOffset,
            float weight)
        {
            if (!HasHeldObject ||
                carryAnchor == null ||
                !IsFinite(localPositionOffset) ||
                !IsFinite(localRotationOffset) ||
                !float.IsFinite(weight))
            {
                ClearHeldPresentationPose();
                return;
            }

            presentationAnchor = null;
            presentationLocalOffset = localPositionOffset;
            presentationRotationOffset =
                localRotationOffset.normalized;
            presentationPoseWeight = Mathf.Clamp01(weight);
            BeginHardPresentationHold();
            ApplyHardPresentationPose();
        }

        /// <summary>
        /// Drives the real carried Rigidbody toward a project-owned animated
        /// grip transform. The item is never reparented or duplicated.
        /// </summary>
        public void SetHeldPresentationAnchor(
            Transform anchor,
            float weight)
        {
            if (!HasHeldObject ||
                carryAnchor == null ||
                anchor == null ||
                !float.IsFinite(weight))
            {
                ClearHeldPresentationPose();
                return;
            }

            presentationAnchor = anchor;
            presentationLocalOffset = Vector3.zero;
            presentationRotationOffset = Quaternion.identity;
            presentationPoseWeight = Mathf.Clamp01(weight);
            BeginHardPresentationHold();
            ApplyHardPresentationPose();
        }

        public void ClearHeldPresentationPose()
        {
            EndHardPresentationHold();
            presentationAnchor = null;
            presentationLocalOffset = Vector3.zero;
            presentationRotationOffset = Quaternion.identity;
            presentationPoseWeight = 0f;
        }

        public bool TryGetHeldCapability<TCapability>(
            out TCapability capability)
            where TCapability : class
        {
            if (HasHeldObject &&
                heldCapabilityHost != null &&
                heldCapabilityHost.TryGetCapability(out capability))
            {
                return true;
            }

            capability = null;
            return false;
        }

        public bool TryActivateHeldOn(
            IContextInteractionTarget target,
            in InteractionContext context)
        {
            if (target == null ||
                !TryGetHeldCapability(
                    out IHeldTargetActivationSource source) ||
                !source.CanActivateHeldOn(target, context))
            {
                return false;
            }

            source.ActivateHeldOn(target, context);
            return true;
        }

        public void RotateHeld(Vector2 degrees)
        {
            if (!HasHeldObject ||
                IsHeldUseReady ||
                HasActiveContinuousHeldActivation)
            {
                return;
            }

            Quaternion yaw = Quaternion.AngleAxis(degrees.x, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(-degrees.y, Vector3.right);
            targetLocalRotation = yaw * pitch * targetLocalRotation;
        }

        public void RotateHeld(float degrees, CarryRotationAxis axis)
        {
            if (!HasHeldObject ||
                IsHeldUseReady ||
                HasActiveContinuousHeldActivation ||
                !float.IsFinite(degrees))
            {
                return;
            }

            Vector3 rotationAxis = axis switch
            {
                CarryRotationAxis.Pitch => Vector3.right,
                CarryRotationAxis.Roll => Vector3.forward,
                _ => Vector3.up,
            };
            targetLocalRotation =
                Quaternion.AngleAxis(degrees, rotationAxis) *
                targetLocalRotation;
        }

        public CarriedObjectSaveState CaptureSaveState()
        {
            if (!HasHeldObject || carryAnchor == null ||
                !heldTarget.StableId.IsValid)
            {
                return CarriedObjectSaveState.Empty();
            }

            return CarriedObjectSaveState.Create(
                heldTarget.StableId,
                carryAnchor.InverseTransformPoint(heldBody.position),
                Quaternion.Inverse(carryAnchor.rotation) * heldBody.rotation);
        }

        /// <summary>
        /// Restores a pre-resolved carried object. Stable-ID resolution belongs
        /// to the save/world integration layer; this component never searches
        /// scenes or donor hierarchy names.
        /// </summary>
        public bool TryRestoreSaveState(
            CarriedObjectSaveState state,
            IPickupTarget resolvedTarget,
            in InteractionContext context,
            out string failure)
        {
            if (state == null)
            {
                failure = "Carried-object state is missing.";
                return false;
            }

            if (!state.TryValidate(out failure))
            {
                return false;
            }

            if (!state.HasCarriedObject)
            {
                if (HasHeldObject)
                {
                    Drop(PickupReleaseReason.TargetLost);
                }

                failure = string.Empty;
                return true;
            }

            if (carryAnchor == null)
            {
                failure = "Carry anchor is missing.";
                return false;
            }

            if (resolvedTarget == null || resolvedTarget.Body == null ||
                !resolvedTarget.StableId.IsValid ||
                !string.Equals(
                    resolvedTarget.StableId.Value,
                    state.StableEntityId,
                    StringComparison.Ordinal))
            {
                failure = "Resolved carried object does not match the saved stable ID.";
                return false;
            }

            bool targetAlreadyHeld = HasHeldObject &&
                ReferenceEquals(heldTarget, resolvedTarget);
            if (HasHeldObject && !targetAlreadyHeld)
            {
                failure = "A different object is already carried.";
                return false;
            }

            if (!targetAlreadyHeld && !resolvedTarget.CanPickup(context))
            {
                failure = "Resolved carried object cannot be picked up in the restored context.";
                return false;
            }

            Vector3 worldPosition = carryAnchor.TransformPoint(
                state.AnchorLocalPosition);
            Quaternion worldRotation = carryAnchor.rotation *
                                       state.AnchorLocalRotation;
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                failure = "Resolved carried-object pose is invalid.";
                return false;
            }

            if (!targetAlreadyHeld && !TryPickup(resolvedTarget, context))
            {
                failure = "Resolved carried object rejected restore pickup.";
                return false;
            }

            EndContinuousHeldActivation();
            SetHeldUseReady(false);
            ClearHeldPresentationPose();
            SetAdaptiveCarryAnchorWorldPosition(worldPosition);
            ResetOwnerMotionTracking();
            heldBody.position = worldPosition;
            heldBody.rotation = worldRotation.normalized;
            heldBody.linearVelocity = Vector3.zero;
            heldBody.angularVelocity = Vector3.zero;
            targetLocalRotation = carryAnchor != null
                ? Quaternion.Inverse(carryAnchor.rotation) * heldBody.rotation
                : heldBody.rotation;
            Physics.SyncTransforms();
            failure = string.Empty;
            return true;
        }

        public void Configure(Transform anchor, Collider ownerCollider)
        {
            carryAnchor = anchor;
            playerCollider = ownerCollider;
            hasAdaptiveCarryAnchorPosition = false;
        }

        /// <summary>
        /// Atomically realigns the carried body after an external owner
        /// teleport. Without this handoff, the next physics tick interprets
        /// the intentional displacement as a lost target and drops the item.
        /// </summary>
        public bool SynchronizeAfterOwnerTeleport()
        {
            if (!HasHeldObject || carryAnchor == null)
            {
                return false;
            }

            Vector3 position = GetTargetWorldPosition();
            Quaternion rotation = GetTargetWorldRotation();
            if (!IsFinite(position) || !IsFinite(rotation))
            {
                return false;
            }

            heldBody.transform.SetPositionAndRotation(
                position,
                rotation.normalized);
            heldBody.position = position;
            heldBody.rotation = rotation.normalized;
            heldBody.linearVelocity = Vector3.zero;
            heldBody.angularVelocity = Vector3.zero;
            ResetOwnerMotionTracking();
            Physics.SyncTransforms();
            return true;
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

            Vector3 inheritedOwnerVelocity =
                SampleInheritedOwnerVelocity(Time.fixedDeltaTime);
            if (hardPresentationHoldActive)
            {
                previousInheritedOwnerVelocity = inheritedOwnerVelocity;
                return;
            }

            Vector3 targetPosition = GetTargetWorldPosition();
            Vector3 offset = targetPosition - heldBody.position;
            if (offset.sqrMagnitude > maximumCarrySeparation * maximumCarrySeparation)
            {
                Drop(PickupReleaseReason.TargetLost);
                return;
            }

            heldBody.linearVelocity = CalculateMotionCompensatedFollowVelocity(
                heldBody.linearVelocity,
                previousInheritedOwnerVelocity,
                inheritedOwnerVelocity,
                offset,
                followAcceleration,
                followVelocityRetention,
                maximumFollowSpeed,
                Time.fixedDeltaTime);
            previousInheritedOwnerVelocity = inheritedOwnerVelocity;

            Quaternion targetWorldRotation = GetTargetWorldRotation();
            Quaternion delta =
                targetWorldRotation * Quaternion.Inverse(heldBody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (axis.sqrMagnitude > 0.0001f && !float.IsNaN(axis.x))
            {
                heldBody.angularVelocity = Vector3.ClampMagnitude(
                    axis.normalized *
                    (angleDegrees * Mathf.Deg2Rad * rotationResponsePerSecond),
                    maximumAngularSpeed);
            }
        }

        public static Vector3 CalculateFollowVelocity(
            Vector3 currentVelocity,
            Vector3 targetOffset,
            float springForce,
            float velocityRetention,
            float maximumSpeed,
            float fixedDeltaTime)
        {
            if (!IsFinite(currentVelocity) ||
                !IsFinite(targetOffset) ||
                !float.IsFinite(springForce) ||
                !float.IsFinite(velocityRetention) ||
                !float.IsFinite(maximumSpeed) ||
                !float.IsFinite(fixedDeltaTime) ||
                fixedDeltaTime <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 velocity = currentVelocity +
                targetOffset * Mathf.Max(0f, springForce) * fixedDeltaTime;
            velocity *= Mathf.Clamp01(velocityRetention);
            return Vector3.ClampMagnitude(
                velocity,
                Mathf.Max(0f, maximumSpeed));
        }

        public static Vector3 CalculateMotionCompensatedFollowVelocity(
            Vector3 currentBodyVelocity,
            Vector3 previousInheritedVelocity,
            Vector3 currentInheritedVelocity,
            Vector3 targetOffset,
            float springForce,
            float velocityRetention,
            float maximumRelativeSpeed,
            float fixedDeltaTime)
        {
            if (!IsFinite(previousInheritedVelocity) ||
                !IsFinite(currentInheritedVelocity))
            {
                return Vector3.zero;
            }

            Vector3 relativeVelocity =
                currentBodyVelocity - previousInheritedVelocity;
            return currentInheritedVelocity + CalculateFollowVelocity(
                relativeVelocity,
                targetOffset,
                springForce,
                velocityRetention,
                maximumRelativeSpeed,
                fixedDeltaTime);
        }

        public static float CalculateAdaptiveHoldDistance(
            float selectedForwardDistance,
            float minimumDistance,
            float authoredMaximumDistance)
        {
            float minimum = Mathf.Max(0.05f, minimumDistance);
            float maximum = Mathf.Max(minimum, authoredMaximumDistance);
            if (!float.IsFinite(selectedForwardDistance))
            {
                return maximum;
            }

            return Mathf.Clamp(
                selectedForwardDistance,
                minimum,
                maximum);
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

            EndContinuousHeldActivation();
            SetHeldUseReady(false);
            EndHardPresentationHold();
            SetOwnerScopeCollisionIgnored(false);
            SetPlayerCollisionIgnored(false);
            originalState.Restore(releasedBody);
            if (IsAlive(releasedTarget))
            {
                releasedTarget.NotifyReleased(reason);
            }

            heldTarget = null;
            heldBody = null;
            heldColliders = null;
            heldScopeColliders = null;
            heldCapabilityHost = null;
            originalState = default;
            targetLocalRotation = Quaternion.identity;
            heldGrabLocalOffset = Vector3.zero;
            hasHeldGrabPoint = false;
            adaptiveCarryAnchorLocalPosition = Vector3.zero;
            hasAdaptiveCarryAnchorPosition = false;
            previousOwnerPosition = Vector3.zero;
            previousInheritedOwnerVelocity = Vector3.zero;
            hasOwnerMotionSample = false;
            ClearHeldPresentationPose();
        }

        private void ReleaseOwnedState()
        {
            if (heldTarget == null && heldBody == null && heldColliders == null)
            {
                return;
            }

            Release(PickupReleaseReason.TargetLost);
        }

        private void BeginHardPresentationHold()
        {
            if (hardPresentationHoldActive || heldBody == null)
            {
                return;
            }

            presentationHoldState = RigidbodyState.Capture(heldBody);
            heldBody.linearVelocity = Vector3.zero;
            heldBody.angularVelocity = Vector3.zero;
            heldBody.useGravity = false;
            heldBody.interpolation = RigidbodyInterpolation.None;
            heldBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            heldBody.detectCollisions = false;
            heldBody.isKinematic = true;
            hardPresentationHoldActive = true;
        }

        private void EndHardPresentationHold()
        {
            if (!hardPresentationHoldActive)
            {
                return;
            }

            Rigidbody body = heldBody;
            hardPresentationHoldActive = false;
            presentationHoldState.Restore(body);
            presentationHoldState = default;
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyHardPresentationPose()
        {
            if (!hardPresentationHoldActive || heldBody == null)
            {
                return;
            }

            Vector3 position = GetTargetWorldPosition();
            Quaternion rotation = GetTargetWorldRotation();
            if (!IsFinite(position) || !IsFinite(rotation))
            {
                return;
            }

            heldBody.transform.SetPositionAndRotation(
                position,
                rotation.normalized);
            heldBody.position = position;
            heldBody.rotation = rotation.normalized;
        }

        private void ConfigureAdaptiveCarryAnchor(
            Vector3 worldGrabPoint,
            bool captureGrabPoint)
        {
            hasAdaptiveCarryAnchorPosition = carryAnchor != null;
            adaptiveCarryAnchorLocalPosition = carryAnchor != null
                ? carryAnchor.localPosition
                : Vector3.zero;
            if (!captureGrabPoint ||
                carryAnchor == null ||
                carryAnchor.parent == null)
            {
                return;
            }

            Vector3 selectedPointInCarrySpace =
                carryAnchor.parent.InverseTransformPoint(worldGrabPoint);
            if (!IsFinite(selectedPointInCarrySpace))
            {
                return;
            }

            adaptiveCarryAnchorLocalPosition.z =
                CalculateAdaptiveHoldDistance(
                    selectedPointInCarrySpace.z,
                    minimumHoldDistance,
                    carryAnchor.localPosition.z);
        }

        private void SetAdaptiveCarryAnchorWorldPosition(
            Vector3 worldPosition)
        {
            if (carryAnchor == null || !IsFinite(worldPosition))
            {
                hasAdaptiveCarryAnchorPosition = false;
                return;
            }

            if (carryAnchor.parent == null)
            {
                adaptiveCarryAnchorLocalPosition = carryAnchor.localPosition;
                hasAdaptiveCarryAnchorPosition = true;
                return;
            }

            adaptiveCarryAnchorLocalPosition =
                carryAnchor.parent.InverseTransformPoint(worldPosition);
            hasAdaptiveCarryAnchorPosition =
                IsFinite(adaptiveCarryAnchorLocalPosition);
        }

        private Vector3 GetPhysicalCarryAnchorPosition()
        {
            if (!hasAdaptiveCarryAnchorPosition || carryAnchor == null)
            {
                return carryAnchor != null
                    ? carryAnchor.position
                    : transform.position;
            }

            return carryAnchor.parent != null
                ? carryAnchor.parent.TransformPoint(
                    adaptiveCarryAnchorLocalPosition)
                : carryAnchor.position;
        }

        private void ResetOwnerMotionTracking()
        {
            previousOwnerPosition = transform.position;
            previousInheritedOwnerVelocity = Vector3.zero;
            hasOwnerMotionSample = true;
        }

        private Vector3 SampleInheritedOwnerVelocity(float fixedDeltaTime)
        {
            Vector3 currentOwnerPosition = transform.position;
            if (playerCollider is CharacterController characterController &&
                characterController.enabled &&
                characterController.gameObject.activeInHierarchy)
            {
                previousOwnerPosition = currentOwnerPosition;
                hasOwnerMotionSample = true;
                return ClampInheritedOwnerVelocity(
                    characterController.velocity);
            }

            if (!hasOwnerMotionSample ||
                !float.IsFinite(fixedDeltaTime) ||
                fixedDeltaTime <= 0f)
            {
                previousOwnerPosition = currentOwnerPosition;
                hasOwnerMotionSample = true;
                return Vector3.zero;
            }

            Vector3 ownerVelocity =
                (currentOwnerPosition - previousOwnerPosition) /
                fixedDeltaTime;
            previousOwnerPosition = currentOwnerPosition;
            if (!IsFinite(ownerVelocity))
            {
                return Vector3.zero;
            }

            return ClampInheritedOwnerVelocity(ownerVelocity);
        }

        private Vector3 ClampInheritedOwnerVelocity(Vector3 ownerVelocity)
        {
            if (!IsFinite(ownerVelocity))
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(
                ownerVelocity * Mathf.Clamp01(ownerMotionInheritance),
                Mathf.Max(0f, maximumInheritedOwnerSpeed));
        }

        private Vector3 GetTargetWorldPosition()
        {
            Vector3 physicalAnchorPosition =
                GetPhysicalCarryAnchorPosition();
            Vector3 targetPosition;
            if (presentationAnchor != null)
            {
                targetPosition = Vector3.Lerp(
                    physicalAnchorPosition,
                    presentationAnchor.position,
                    presentationPoseWeight);
            }
            else
            {
                Vector3 authoredPresentationPosition =
                    carryAnchor.TransformPoint(presentationLocalOffset);
                targetPosition = Vector3.Lerp(
                    physicalAnchorPosition,
                    authoredPresentationPosition,
                    presentationPoseWeight);
            }

            float physicalGrabWeight = hasHeldGrabPoint
                ? 1f - presentationPoseWeight
                : 0f;
            if (physicalGrabWeight > 0f)
            {
                targetPosition -= GetTargetWorldRotation() *
                    heldGrabLocalOffset * physicalGrabWeight;
            }

            return targetPosition;
        }

        private Quaternion GetTargetWorldRotation()
        {
            Quaternion baseWorldRotation = carryAnchor != null
                ? carryAnchor.rotation * targetLocalRotation
                : targetLocalRotation;
            if (presentationAnchor != null)
            {
                return Quaternion.Slerp(
                    baseWorldRotation,
                    presentationAnchor.rotation,
                    presentationPoseWeight);
            }

            Quaternion localRotation = Quaternion.Slerp(
                targetLocalRotation,
                presentationRotationOffset * targetLocalRotation,
                presentationPoseWeight);
            return carryAnchor != null
                ? carryAnchor.rotation * localRotation
                : localRotation;
        }

        private void SetHeldUseReady(bool value)
        {
            bool resolvedValue = value && HasHeldObject;
            if (heldUseReady == resolvedValue)
            {
                return;
            }

            heldUseReady = resolvedValue;
            Action<bool> handler = HeldUseReadyChanged;
            if (handler == null)
            {
                return;
            }

            Delegate[] subscribers = handler.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<bool>)subscribers[index]).Invoke(resolvedValue);
                }
                catch (Exception exception)
                {
                    // Presentation listeners must not invalidate an already
                    // completed pickup or release.
                    Debug.LogException(exception, this);
                }
            }
        }

        private void PublishCompletedAction(
            InteractionActionKind action,
            StableEntityId targetStableId,
            Vector3 worldPosition)
        {
            Action<InteractionActionCompleted> handler = ActionCompleted;
            if (handler == null)
            {
                return;
            }

            var notification = new InteractionActionCompleted(
                action,
                targetStableId,
                worldPosition);
            Delegate[] subscribers = handler.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<InteractionActionCompleted>)subscribers[index]).Invoke(notification);
                }
                catch (Exception exception)
                {
                    // Presentation listeners must never roll back a completed
                    // interaction or suppress later observers.
                    Debug.LogException(exception, this);
                }
            }
        }

        private Bounds CalculateHeldBounds()
        {
            Bounds bounds = new Bounds(heldBody.position, Vector3.one * 0.25f);
            bool hasSolid = false;
            for (int i = 0; heldColliders != null && i < heldColliders.Length; i++)
            {
                Collider shape = heldColliders[i];
                // Installed children keep disabled/query shapes beside the
                // actual compound contacts. Empty bounds would include world
                // zero; query volumes and nested bodies aren't carried solids.
                if (shape == null || !shape.enabled || !shape.gameObject.activeInHierarchy ||
                    shape.isTrigger || shape.attachedRigidbody != heldBody)
                {
                    continue;
                }

                if (hasSolid) bounds.Encapsulate(shape.bounds);
                else bounds = shape.bounds;
                hasSolid = true;
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

        private void SetOwnerScopeCollisionIgnored(bool ignored)
        {
            if (ignored)
            {
                CarryCollisionBypassScope scope = FindCollisionBypassScope(
                    heldBody != null ? heldBody.transform : null);
                heldScopeColliders = scope != null
                    ? scope.ScopeColliders
                    : null;
            }

            if (heldColliders == null || heldScopeColliders == null)
            {
                if (!ignored)
                {
                    heldScopeColliders = null;
                }

                return;
            }

            for (int heldIndex = 0;
                 heldIndex < heldColliders.Length;
                 heldIndex++)
            {
                Collider heldCollider = heldColliders[heldIndex];
                if (heldCollider == null)
                {
                    continue;
                }

                for (int scopeIndex = 0;
                     scopeIndex < heldScopeColliders.Length;
                     scopeIndex++)
                {
                    Collider scopeCollider =
                        heldScopeColliders[scopeIndex];
                    if (scopeCollider != null &&
                        scopeCollider != heldCollider)
                    {
                        Physics.IgnoreCollision(
                            heldCollider,
                            scopeCollider,
                            ignored);
                    }
                }
            }

            if (!ignored)
            {
                heldScopeColliders = null;
            }
        }

        private static CarryCollisionBypassScope FindCollisionBypassScope(
            Transform start)
        {
            for (Transform current = start;
                 current != null;
                 current = current.parent)
            {
                CarryCollisionBypassScope scope = current
                    .GetComponent<CarryCollisionBypassScope>();
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }

        private static bool IsAlive(IPickupTarget target)
        {
            if (target == null ||
                target is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            return !(target is Component component) ||
                   component.gameObject.activeInHierarchy;
        }

        private static InteractionTargetHost FindCapabilityHost(IPickupTarget target)
        {
            if (target is Component targetComponent)
            {
                InteractionTargetHost host = FindCapabilityHost(targetComponent.transform, target);
                if (host != null)
                {
                    return host;
                }
            }

            return target.Body != null
                ? FindCapabilityHost(target.Body.transform, target)
                : null;
        }

        private static InteractionTargetHost FindCapabilityHost(
            Transform start,
            IPickupTarget target)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                InteractionTargetHost host = current.GetComponent<InteractionTargetHost>();
                if (host != null &&
                    host.TryGetCapability(out IPickupTarget registeredTarget) &&
                    ReferenceEquals(registeredTarget, target))
                {
                    return host;
                }
            }

            return null;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w) &&
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w > 0.000001f;

        private readonly struct RigidbodyState
        {
            private RigidbodyState(
                bool useGravity,
                bool isKinematic,
                bool detectCollisions,
                CollisionDetectionMode collisionDetectionMode,
                RigidbodyInterpolation interpolation,
                float linearDamping,
                float angularDamping)
            {
                UseGravity = useGravity;
                IsKinematic = isKinematic;
                DetectCollisions = detectCollisions;
                CollisionDetectionMode = collisionDetectionMode;
                Interpolation = interpolation;
                LinearDamping = linearDamping;
                AngularDamping = angularDamping;
            }

            public bool UseGravity { get; }

            public bool IsKinematic { get; }

            private bool DetectCollisions { get; }

            private CollisionDetectionMode CollisionDetectionMode { get; }

            private RigidbodyInterpolation Interpolation { get; }

            private float LinearDamping { get; }

            private float AngularDamping { get; }

            public static RigidbodyState Capture(Rigidbody body)
            {
                return new RigidbodyState(
                    body.useGravity,
                    body.isKinematic,
                    body.detectCollisions,
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
                body.detectCollisions = DetectCollisions;
            }
        }
    }
}
