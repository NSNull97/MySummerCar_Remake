using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;
using UnityEngine.Serialization;

namespace MSC.Player
{
    public readonly struct PlayerLeanImpact
    {
        public PlayerLeanImpact(
            Vector3 point,
            Vector3 normal,
            float forwardSpeedMetersPerSecond,
            Collider collider)
        {
            Point = point;
            Normal = normal;
            ForwardSpeedMetersPerSecond = forwardSpeedMetersPerSecond;
            Collider = collider;
        }

        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float ForwardSpeedMetersPerSecond { get; }
        public Collider Collider { get; }
    }

    /// <summary>
    /// Presentation-safe landing evidence. The legacy float event remains
    /// available, while this value also identifies the jump impulse that
    /// produced the airborne arc.
    /// </summary>
    public readonly struct PlayerLandingImpact
    {
        public PlayerLandingImpact(
            float impactSpeedMetersPerSecond,
            float jumpForceMetersPerSecond)
        {
            ImpactSpeedMetersPerSecond = Mathf.Max(
                0f,
                impactSpeedMetersPerSecond);
            JumpForceMetersPerSecond = Mathf.Max(
                0f,
                jumpForceMetersPerSecond);
        }

        public float ImpactSpeedMetersPerSecond { get; }
        public float JumpForceMetersPerSecond { get; }
        public bool OriginatedFromJump => JumpForceMetersPerSecond > 0f;
    }

    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour,
        IRestrictedInteriorPostureParticipant
    {
        private const float GroundNormalMinimumY = 0.01f;
        private const float MinimumUsefulLeanAngle = 0.5f;
        private const float SupportSideConflictMaximumNormalY = 0.85f;

        private readonly Collider[] headroomBuffer = new Collider[12];
        private readonly RaycastHit[] leanHitBuffer = new RaycastHit[8];
        private readonly RaycastHit[] groundHitBuffer = new RaycastHit[8];
        private readonly RaycastHit[] supportHitBuffer = new RaycastHit[16];
        private readonly Rigidbody[] sideContactBodyBuffer = new Rigidbody[8];

        [SerializeField]
        private CharacterController characterController;

        [Header("View rig")]
        [SerializeField]
        private Transform leanPivot;

        [SerializeField]
        private Transform cameraPivot;

        [SerializeField]
        private Transform impactPivot;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float walkingSpeedMetersPerSecond = 2.9f;

        [SerializeField, Min(0f)]
        private float walkingBackwardSpeedMetersPerSecond = 2.5f;

        [SerializeField, Min(0f)]
        private float walkingStrafeSpeedMetersPerSecond = 2.6f;

        [SerializeField, Min(0f)]
        private float runningSpeedMetersPerSecond = 6.4f;

        [SerializeField, Min(0f)]
        private float runningBackwardSpeedMetersPerSecond = 3.1f;

        [SerializeField, Min(0f)]
        private float runningStrafeSpeedMetersPerSecond = 5.1f;

        [SerializeField, Min(0f)]
        private float crouchingSpeedMetersPerSecond = 1.85f;

        [SerializeField, Min(0f)]
        private float crouchingBackwardSpeedMetersPerSecond = 1.7f;

        [SerializeField, Min(0f)]
        private float crouchingStrafeSpeedMetersPerSecond = 1.75f;

        [SerializeField, Min(0f)]
        private float deepCrouchingSpeedMetersPerSecond = 0.7f;

        [FormerlySerializedAs("groundAccelerationMetersPerSecondSquared")]
        [SerializeField, Min(0f)]
        private float groundAccelerationResponsePerSecond = 8f;

        [FormerlySerializedAs("groundDecelerationMetersPerSecondSquared")]
        [SerializeField, Min(0f)]
        private float groundDecelerationResponsePerSecond = 10f;

        [SerializeField, Min(0f)]
        private float airAccelerationMetersPerSecondSquared = 4f;

        [SerializeField, Min(0f)]
        private float minimumRunningEntrySpeedMetersPerSecond = 1.7f;

        [SerializeField, Range(0f, 1f)]
        private float maximumRunningStrafeInput = 0.9f;

        [Header("Traversal capsule")]
        [SerializeField, Min(0.01f)]
        private float controllerRadiusMeters = 0.12f;

        [SerializeField, Min(0.1f)]
        private float standingHeightMeters = 0.5f;

        [SerializeField, Min(0.1f)]
        private float crouchingHeightMeters = 0.5f;

        [SerializeField, Min(0.1f)]
        private float deepCrouchingHeightMeters = 0.5f;

        [SerializeField, Range(0f, 90f)]
        private float slopeLimitDegrees = 90f;

        [SerializeField, Min(0.001f)]
        private float controllerSkinWidthMeters = 0.03f;

        [SerializeField, Min(0f)]
        private float controllerMinimumMoveDistanceMeters;

        [Header("Posture")]
        [SerializeField, Min(0.1f)]
        private float standingEyeHeightMeters = 1.48f;

        [SerializeField, Min(0.1f)]
        private float crouchingEyeHeightMeters = 0.93f;

        [SerializeField, Min(0.1f)]
        private float deepCrouchingEyeHeightMeters = 0.38f;

        [SerializeField, Min(0f)]
        private float postureTransitionMetersPerSecond = 1.7f;

        [SerializeField, Min(0f)]
        private float postureRiseTransitionMetersPerSecond = 6f;

        [SerializeField, Min(0.01f)]
        private float headClearanceRadiusMeters = 0.11f;

        [SerializeField]
        private LayerMask headroomMask = ~0;

        [Header("Grounding")]
        [SerializeField, Min(0f)]
        private float gravityMetersPerSecondSquared = 21f;

        [SerializeField, Min(0f)]
        private float jumpHeightMeters = 1.05f;

        [Tooltip(
            "Authoritative launch velocity. A non-positive value keeps the " +
            "legacy jump-height fallback for serialized compatibility.")]
        [SerializeField, Min(0f)]
        private float jumpForceMetersPerSecond = 6.65f;

        [SerializeField, Min(0f)]
        private float jumpPreparationSeconds = 0.085f;

        [SerializeField, Min(0f)]
        private float coyoteTimeSeconds = 0.1f;

        [SerializeField, Min(0f)]
        private float groundStickSpeedMetersPerSecond = 3f;

        [SerializeField, Min(0f)]
        private float stepOffsetMeters = 0.4f;

        [SerializeField, Min(0f)]
        private float groundSnapDistanceMeters = 0.4f;

        [SerializeField, Min(0.001f)]
        private float groundProbeLiftMeters = 0.05f;

        [Header("Forward lean")]
        [SerializeField, Range(0f, 80f)]
        private float forwardLeanAngleDegrees = 40f;

        [SerializeField, Min(0f)]
        private float forwardLeanAngularSpeedDegreesPerSecond = 150f;

        [SerializeField, Min(0f)]
        private float forwardLeanReturnAngularSpeedDegreesPerSecond = 120f;

        [SerializeField, Min(0.01f)]
        private float leanArcRadiusMeters = 1.7f;

        [SerializeField, Min(0.01f)]
        private float leanCollisionRadiusMeters = 0.11f;

        [SerializeField, Min(0f)]
        private float leanCollisionClearanceMeters = 0.02f;

        [Header("Forward lean impact")]
        [SerializeField, Min(0f)]
        private float leanImpactMinimumForwardSpeedMetersPerSecond = 3f;

        [SerializeField, Range(0f, 90f)]
        private float leanImpactMaximumWallAngleDegrees = 30f;

        [SerializeField, Min(0f)]
        private float leanImpactCooldownSeconds = 0.75f;

        [SerializeField, Min(0f)]
        private float leanImpactKickDegrees = 8f;

        [SerializeField, Min(0f)]
        private float leanImpactKickReturnDegreesPerSecond = 28f;

        [SerializeField, Range(0f, 1f)]
        private float leanImpactRetainedSpeed = 0.35f;

        [Header("Physical presence")]
        [SerializeField, Range(1f, 500f)]
        private float bodyMassKilograms = 83f;

        [SerializeField, Range(0f, 2f)]
        private float supportLoadScale = 1f;

        [Tooltip(
            "Minimum upward-facing normal used by the dedicated feet probe. " +
            "This is intentionally independent from the legacy 90 degree " +
            "CharacterController slope limit so vehicle sides cannot become " +
            "weight-bearing contacts.")]
        [SerializeField, Range(0.1f, 1f)]
        private float minimumSupportSurfaceNormalY = 0.55f;

        [SerializeField, Min(0.01f)]
        private float supportProbeLiftMeters = 0.08f;

        [SerializeField, Min(0.01f)]
        private float supportProbeDepthMeters = 0.35f;

        [Tooltip(
            "Requires a stable patch under the controller footprint before " +
            "body weight can reach a dynamic support. This rejects a narrow " +
            "rocker caught only by the centre ray.")]
        [SerializeField, Range(0f, 1f)]
        private float supportFootprintRadiusScale = 0.8f;

        [Tooltip(
            "Delay before a newly observed dynamic support receives weight. " +
            "A player crossing a rocker reaches the blocking vehicle side " +
            "before this interval and is rejected as a support contact.")]
        [SerializeField, Min(0f)]
        private float supportConfirmationSeconds = 0.2f;

        [Tooltip(
            "Ramps a newly acquired load in fixed time so sprung vehicle " +
            "bodies do not receive an instantaneous step impulse.")]
        [SerializeField, Min(0f)]
        private float supportLoadRampSeconds = 0.15f;

        [Header("Rigidbody contact")]
        [SerializeField, Min(0f)]
        private float rigidbodyPushForceNewtons = 18f;

        [SerializeField, Min(0f)]
        private float maximumPushableMassKilograms = 35f;

        private Vector2 moveInput;
        private Vector3 horizontalVelocity;
        private Vector3 groundNormal = Vector3.up;
        private Vector3 contactGroundNormal = Vector3.up;
        private PlayerPosture posture = PlayerPosture.Standing;
        private bool runRequested;
        private bool jumpRequested;
        private bool forwardLeanRequested;
        private bool grounded;
        private bool receivedGroundContact;
        private Rigidbody supportingRigidbody;
        private Vector3 supportingContactPointLocal;
        private float supportingContactObservedTime = float.NegativeInfinity;
        private float supportingContactAcquiredTime = float.NegativeInfinity;
        private float supportLoadBlend;
        private int sideContactBodyCount;
        private bool jumpAvailableSinceGroundContact;
        private bool preparingJump;
        private float verticalSpeed;
        private float activeAirborneJumpForceMetersPerSecond;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpPreparationRemainingSeconds;
        private float currentLeanAngleDegrees;
        private float actualHorizontalSpeed;
        private float leanImpactCooldownRemaining;
        private float impactPitchDegrees;
        private float impactRollDegrees;
        private Vector3 leanPivotBaseLocalPosition;
        private Vector3 cameraPivotBaseLocalPosition;
        private Quaternion leanPivotBaseLocalRotation = Quaternion.identity;
        private Quaternion impactPivotBaseLocalRotation = Quaternion.identity;
        private Vector3 headOffsetFromLeanPivotLocal = Vector3.up * 1.7f;
        private float viewRigBaseEyeHeightMeters = 1.4f;
        private bool viewRigBaseCaptured;
        private int restrictedInteriorDepth;
        private PlayerPosture postureBeforeRestrictedInterior =
            PlayerPosture.Standing;
        private bool hasDeferredPostureRestore;
        private PlayerPosture deferredPostureRestore = PlayerPosture.Standing;

        public event Action<PlayerLeanImpact> ForwardLeanImpactOccurred;

        public event Action Jumped;

        public event Action<float> JumpPreparationStarted;

        public event Action JumpPreparationCancelled;

        public event Action<float> Landed;

        public event Action<PlayerLandingImpact> LandingImpactOccurred;

        public PlayerPosture Posture => posture;
        public bool IsCrouching => posture != PlayerPosture.Standing;
        public bool IsDeepCrouching => posture == PlayerPosture.DeepCrouch;
        public bool IsInsideRestrictedInterior => restrictedInteriorDepth > 0;
        public bool IsRunning =>
            runRequested &&
            posture == PlayerPosture.Standing &&
            IsGrounded &&
            FirstPersonMovementMath.CanEnterRun(
                moveInput,
                actualHorizontalSpeed,
                minimumRunningEntrySpeedMetersPerSecond,
                maximumRunningStrafeInput);
        public bool IsGrounded =>
            characterController != null &&
            (grounded || characterController.isGrounded);
        public bool IsForwardLeaning =>
            currentLeanAngleDegrees > MinimumUsefulLeanAngle;
        public float CurrentLeanAngleDegrees => currentLeanAngleDegrees;
        public float HorizontalSpeedMetersPerSecond => actualHorizontalSpeed;
        public float VerticalSpeedMetersPerSecond => verticalSpeed;
        public float JumpForceMetersPerSecond =>
            ResolveJumpForceMetersPerSecond();
        public float BodyMassKilograms => bodyMassKilograms;
        public bool IsPreparingJump => preparingJump;
        public Rigidbody LastSupportedRigidbody { get; private set; }
        public Vector3 LastSupportLoadImpulseNewtonSeconds { get; private set; }
        public int LeanImpactCount { get; private set; }
        public bool HasLeanImpact => LeanImpactCount > 0;
        public PlayerLeanImpact LastLeanImpact { get; private set; }
        public PlayerLandingImpact LastLandingImpact { get; private set; }
        public PlayerLocomotionState LocomotionState =>
            new PlayerLocomotionState(
                posture,
                IsRunning,
                IsGrounded,
                IsForwardLeaning,
                actualHorizontalSpeed,
                verticalSpeed);

        public FirstPersonMotorSaveDto CaptureSaveState() =>
            FirstPersonMotorSaveDto.Create(posture, verticalSpeed);

        public bool CanRestoreSaveState(
            FirstPersonMotorSaveDto state,
            out string failure)
        {
            if (state == null)
            {
                failure = "Player motor state is missing.";
                return false;
            }

            if (!state.TryValidate(out failure))
            {
                return false;
            }

            if (characterController == null || GetLeanPivot() == null)
            {
                failure = "Player motor authoring references are incomplete.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRestoreSaveState(
            FirstPersonMotorSaveDto state,
            out string failure)
        {
            if (!CanRestoreSaveState(state, out failure))
            {
                return false;
            }

            ResetInputIntent();
            restrictedInteriorDepth = 0;
            hasDeferredPostureRestore = false;
            posture = state.Posture;
            verticalSpeed = state.VerticalSpeedMetersPerSecond;
            activeAirborneJumpForceMetersPerSecond = 0f;
            horizontalVelocity = Vector3.zero;
            actualHorizontalSpeed = 0f;
            grounded = characterController.isGrounded;
            jumpAvailableSinceGroundContact = grounded;
            lastGroundedTime = grounded
                ? Time.time
                : float.NegativeInfinity;
            currentLeanAngleDegrees = 0f;
            impactPitchDegrees = 0f;
            impactRollDegrees = 0f;

            ApplyControllerGeometry();
            CaptureViewRigBaseIfNeeded();
            SetEyeHeightImmediate(EyeHeightFor(posture));
            ApplyLeanRotation();
            ApplyImpactRotation();
            failure = string.Empty;
            return true;
        }

        public bool TryRestoreWorldPose(
            Vector3 position,
            Quaternion rotation,
            out string failure)
        {
            if (!PlayerSaveDto.IsFinite(position) ||
                !PlayerSaveDto.IsValidRotation(rotation))
            {
                failure = "Player world pose is invalid.";
                return false;
            }

            if (characterController == null)
            {
                failure = "Player CharacterController is missing.";
                return false;
            }

            bool wasEnabled = characterController.enabled;
            Vector3 previousPosition = transform.position;
            Quaternion previousRotation = transform.rotation;
            try
            {
                characterController.enabled = false;
                transform.SetPositionAndRotation(
                    position,
                    PlayerSaveDto.Normalize(rotation));
                Physics.SyncTransforms();
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                transform.SetPositionAndRotation(previousPosition, previousRotation);
                failure = "Player pose restore failed: " + exception.Message;
                return false;
            }
            finally
            {
                characterController.enabled = wasEnabled;
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>
        /// Compatibility boundary for the former binary crouch input.
        /// New input should call CyclePosture once per press.
        /// </summary>
        public void SetCrouchRequested(bool requested)
        {
            if (requested)
            {
                if (posture == PlayerPosture.Standing)
                {
                    posture = PlayerPosture.Crouch;
                }
            }
            else
            {
                TrySetPosture(PlayerPosture.Standing);
            }
        }

        public void CyclePosture()
        {
            if (IsInsideRestrictedInterior)
            {
                posture = posture == PlayerPosture.Crouch
                    ? PlayerPosture.DeepCrouch
                    : PlayerPosture.Crouch;
                return;
            }

            switch (posture)
            {
                case PlayerPosture.Standing:
                    posture = PlayerPosture.Crouch;
                    break;
                case PlayerPosture.Crouch:
                    posture = PlayerPosture.DeepCrouch;
                    break;
                default:
                    if (!TrySetPosture(PlayerPosture.Standing))
                    {
                        TrySetPosture(PlayerPosture.Crouch);
                    }

                    break;
            }
        }

        public bool TrySetPosture(PlayerPosture requestedPosture)
        {
            if (!PlayerLocomotionState.IsValidPosture(requestedPosture))
            {
                return false;
            }

            if (IsInsideRestrictedInterior &&
                requestedPosture == PlayerPosture.Standing)
            {
                return false;
            }

            float requestedEyeHeight = EyeHeightFor(requestedPosture);
            if (requestedEyeHeight > CurrentEyeHeightMeters() + 0.01f &&
                !HasHeadroom(requestedEyeHeight))
            {
                return false;
            }

            posture = requestedPosture;
            return true;
        }

        public void EnterRestrictedInteriorPosture()
        {
            if (restrictedInteriorDepth++ > 0)
            {
                return;
            }

            postureBeforeRestrictedInterior = posture;
            hasDeferredPostureRestore = false;
            posture = posture == PlayerPosture.Standing
                ? PlayerPosture.Crouch
                : PlayerPosture.DeepCrouch;
        }

        public void ExitRestrictedInteriorPosture()
        {
            if (restrictedInteriorDepth <= 0)
            {
                return;
            }

            restrictedInteriorDepth--;
            if (restrictedInteriorDepth > 0)
            {
                return;
            }

            if (TrySetPosture(postureBeforeRestrictedInterior))
            {
                hasDeferredPostureRestore = false;
                return;
            }

            deferredPostureRestore = postureBeforeRestrictedInterior;
            hasDeferredPostureRestore = true;
        }

        public void SetRunRequested(bool requested)
        {
            runRequested = requested;
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public bool TrySetJumpForceMetersPerSecond(float forceMetersPerSecond)
        {
            if (!float.IsFinite(forceMetersPerSecond) ||
                forceMetersPerSecond <= 0f ||
                forceMetersPerSecond > 20f)
            {
                return false;
            }

            jumpForceMetersPerSecond = forceMetersPerSecond;
            return true;
        }

        public bool TrySetBodyMassKilograms(float massKilograms)
        {
            if (!float.IsFinite(massKilograms) ||
                massKilograms < 1f ||
                massKilograms > 500f)
            {
                return false;
            }

            bodyMassKilograms = massKilograms;
            return true;
        }

        public void SetForwardLeanRequested(bool requested)
        {
            forwardLeanRequested = requested;
        }

        public void ResetInputIntent()
        {
            moveInput = Vector2.zero;
            runRequested = false;
            jumpRequested = false;
            forwardLeanRequested = false;
            CancelJumpPreparation(restoreGroundedJump: true);
        }

        public void Configure(CharacterController controller, Transform pivot)
        {
            Configure(controller, pivot, pivot, null);
        }

        public void Configure(
            CharacterController controller,
            Transform authoredLeanPivot,
            Transform authoredCameraPivot,
            Transform authoredImpactPivot)
        {
            characterController = controller;
            leanPivot = authoredLeanPivot;
            cameraPivot = authoredCameraPivot;
            impactPivot = authoredImpactPivot;
            viewRigBaseCaptured = false;
            ApplyControllerGeometry();
            CaptureViewRigBaseIfNeeded();
        }

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            ApplyControllerGeometry();
            CaptureViewRigBaseIfNeeded();
            grounded = characterController != null && characterController.isGrounded;
            jumpAvailableSinceGroundContact = grounded;
            lastGroundedTime = grounded ? Time.time : float.NegativeInfinity;
        }

        private void OnDisable()
        {
            ResetInputIntent();
            restrictedInteriorDepth = 0;
            hasDeferredPostureRestore = false;
            currentLeanAngleDegrees = 0f;
            impactPitchDegrees = 0f;
            impactRollDegrees = 0f;
            activeAirborneJumpForceMetersPerSecond = 0f;
            sideContactBodyCount = 0;
            ClearBodyWeightSupport();
            ApplyLeanRotation();
            ApplyImpactRotation();
        }

        private void FixedUpdate()
        {
            ApplyBodyWeightToSupport(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (!IsInsideRestrictedInterior && hasDeferredPostureRestore &&
                TrySetPosture(deferredPostureRestore))
            {
                hasDeferredPostureRestore = false;
            }

            UpdateControllerPosture(deltaTime);

            bool groundedBeforeMove = IsGrounded;
            if (groundedBeforeMove && !preparingJump)
            {
                lastGroundedTime = Time.time;
                jumpAvailableSinceGroundContact = true;
            }

            Vector3 desiredDirection =
                transform.right * moveInput.x +
                transform.forward * moveInput.y;
            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            if (groundedBeforeMove && groundNormal.y > GroundNormalMinimumY)
            {
                desiredDirection = ProjectDirectionOntoGround(
                    desiredDirection,
                    groundNormal);
            }

            float targetSpeed = SpeedForCurrentState();
            Vector3 targetHorizontalVelocity = desiredDirection * targetSpeed;
            float acceleration = groundedBeforeMove
                ? (moveInput.sqrMagnitude > 0.0001f
                    ? groundAccelerationResponsePerSecond
                    : groundDecelerationResponsePerSecond)
                : airAccelerationMetersPerSecondSquared;
            horizontalVelocity = groundedBeforeMove
                ? FirstPersonMovementMath.ApplyPerFrameResponse(
                    horizontalVelocity,
                    targetHorizontalVelocity,
                    acceleration,
                    deltaTime)
                : Vector3.MoveTowards(
                    horizontalVelocity,
                    targetHorizontalVelocity,
                    acceleration * deltaTime);

            bool canPrepareJump = jumpRequested &&
                !preparingJump &&
                FirstPersonMovementMath.CanUseJump(
                    groundedBeforeMove,
                    jumpAvailableSinceGroundContact,
                    Time.time - lastGroundedTime,
                    coyoteTimeSeconds);
            if (canPrepareJump &&
                posture != PlayerPosture.Standing &&
                !TrySetPosture(PlayerPosture.Standing))
            {
                // A crouched jump commits to standing. If the head cannot fit,
                // rejecting the launch is safer than clipping through a roof.
                canPrepareJump = false;
            }

            if (canPrepareJump)
            {
                preparingJump = true;
                jumpPreparationRemainingSeconds = jumpPreparationSeconds;
                jumpAvailableSinceGroundContact = false;
                JumpPreparationStarted?.Invoke(jumpPreparationSeconds);
            }

            bool jumpedThisFrame = false;
            if (preparingJump)
            {
                jumpPreparationRemainingSeconds -= deltaTime;
                if (jumpPreparationRemainingSeconds <= 0f)
                {
                    preparingJump = false;
                    jumpPreparationRemainingSeconds = 0f;
                    jumpedThisFrame = true;
                }
            }

            if (jumpedThisFrame)
            {
                verticalSpeed = ResolveJumpForceMetersPerSecond();
                activeAirborneJumpForceMetersPerSecond = verticalSpeed;
                grounded = false;
                Jumped?.Invoke();
            }
            else if (groundedBeforeMove && verticalSpeed < 0f)
            {
                verticalSpeed = -groundStickSpeedMetersPerSecond;
            }
            else
            {
                verticalSpeed -= gravityMetersPerSecondSquared * deltaTime;
            }

            jumpRequested = false;
            float forwardApproachSpeed = Vector3.Dot(
                horizontalVelocity,
                transform.forward);
            Vector3 previousPosition = transform.position;
            receivedGroundContact = false;
            contactGroundNormal = Vector3.up;
            sideContactBodyCount = 0;
            CollisionFlags flags = characterController.Move(
                (horizontalVelocity + Vector3.up * verticalSpeed) * deltaTime);

            bool below = (flags & CollisionFlags.Below) != 0;
            if (!below &&
                groundedBeforeMove &&
                !jumpedThisFrame &&
                verticalSpeed <= 0f)
            {
                below = TrySnapToGround();
            }

            grounded = below || characterController.isGrounded;
            bool landedThisFrame =
                !groundedBeforeMove &&
                grounded &&
                verticalSpeed < 0f;
            if (landedThisFrame)
            {
                float impactSpeedMetersPerSecond = -verticalSpeed;
                LastLandingImpact = new PlayerLandingImpact(
                    impactSpeedMetersPerSecond,
                    activeAirborneJumpForceMetersPerSecond);
                LandingImpactOccurred?.Invoke(LastLandingImpact);
                Landed?.Invoke(impactSpeedMetersPerSecond);
                activeAirborneJumpForceMetersPerSecond = 0f;
            }
            if (receivedGroundContact)
            {
                groundNormal = contactGroundNormal;
            }
            else if (!grounded)
            {
                groundNormal = Vector3.up;
            }

            if (jumpedThisFrame)
            {
                ClearBodyWeightSupport();
            }
            else if (grounded)
            {
                ObserveBodyWeightSupport();
            }

            if (grounded && verticalSpeed < 0f)
            {
                verticalSpeed = -groundStickSpeedMetersPerSecond;
            }
            else if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            {
                verticalSpeed = 0f;
            }

            Vector3 displacement = transform.position - previousPosition;
            displacement.y = 0f;
            actualHorizontalSpeed = displacement.magnitude / deltaTime;

            UpdateViewRigAndLean(deltaTime, forwardApproachSpeed);
            UpdateImpactFeedback(deltaTime);
        }

        private void CancelJumpPreparation(bool restoreGroundedJump)
        {
            if (!preparingJump)
            {
                return;
            }

            preparingJump = false;
            jumpPreparationRemainingSeconds = 0f;
            if (restoreGroundedJump && IsGrounded)
            {
                jumpAvailableSinceGroundContact = true;
                lastGroundedTime = Time.time;
            }

            JumpPreparationCancelled?.Invoke();
        }

        private void UpdateControllerPosture(float deltaTime)
        {
            float targetHeight = HeightFor(posture);
            float height = Mathf.MoveTowards(
                characterController.height,
                targetHeight,
                PostureTransitionSpeed(
                    characterController.height,
                    targetHeight) * deltaTime);
            characterController.height = height;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
            UpdateStepOffset(height);
        }

        private void UpdateViewRigAndLean(
            float deltaTime,
            float forwardApproachSpeed)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null)
            {
                return;
            }

            CaptureViewRigBaseIfNeeded();
            float targetEyeHeight = EyeHeightFor(posture);
            float currentEyeHeight = CurrentEyeHeightMeters();
            if (targetEyeHeight > currentEyeHeight + 0.001f &&
                !HasHeadroom(targetEyeHeight))
            {
                targetEyeHeight = currentEyeHeight;
            }

            MoveEyeHeightTowards(
                targetEyeHeight,
                PostureTransitionSpeed(
                    currentEyeHeight,
                    targetEyeHeight) * deltaTime);

            LeanResolution resolution = forwardLeanRequested
                ? ResolveForwardLean(forwardLeanAngleDegrees)
                : LeanResolution.Unblocked(0f);
            float targetAngle = forwardLeanRequested
                ? resolution.AllowedAngleDegrees
                : 0f;

            bool collisionRequiresImmediateClamp =
                forwardLeanRequested &&
                resolution.Blocked &&
                targetAngle < currentLeanAngleDegrees;
            if (collisionRequiresImmediateClamp)
            {
                currentLeanAngleDegrees = targetAngle;
            }
            else
            {
                float angularSpeed = forwardLeanRequested
                    ? forwardLeanAngularSpeedDegreesPerSecond
                    : forwardLeanReturnAngularSpeedDegreesPerSecond;
                currentLeanAngleDegrees = Mathf.MoveTowards(
                    currentLeanAngleDegrees,
                    targetAngle,
                    angularSpeed * deltaTime);
            }

            ApplyLeanRotation();
            if (forwardLeanRequested &&
                resolution.Blocked &&
                forwardApproachSpeed >
                    leanImpactMinimumForwardSpeedMetersPerSecond &&
                Vector3.Angle(
                    resolution.HitNormal,
                    -transform.forward) <
                    leanImpactMaximumWallAngleDegrees)
            {
                TriggerLeanImpact(resolution, forwardApproachSpeed);
            }
        }

        private LeanResolution ResolveForwardLean(float requestedAngleDegrees)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (requestedAngleDegrees <= 0f || resolvedLeanPivot == null)
            {
                return LeanResolution.Unblocked(0f);
            }

            Vector3 pivotWorldPosition = resolvedLeanPivot.position;
            Quaternion baseWorldRotation = GetLeanBaseWorldRotation();
            Vector3 headOffset = GetCurrentHeadOffsetFromLeanPivotLocal();
            if (headOffset.sqrMagnitude < 0.0001f)
            {
                headOffset = Vector3.up * leanArcRadiusMeters;
            }

            Vector3 start =
                pivotWorldPosition + baseWorldRotation * headOffset;
            Vector3 target =
                pivotWorldPosition +
                baseWorldRotation *
                (Quaternion.Euler(requestedAngleDegrees, 0f, 0f) * headOffset);
            Vector3 chord = target - start;
            float chordDistance = chord.magnitude;
            if (chordDistance <= 0.0001f)
            {
                return LeanResolution.Unblocked(requestedAngleDegrees);
            }

            int count = Physics.SphereCastNonAlloc(
                start,
                leanCollisionRadiusMeters,
                chord / chordDistance,
                leanHitBuffer,
                chordDistance + leanCollisionClearanceMeters,
                headroomMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestHit = default;
            for (int index = 0; index < count; index++)
            {
                Collider candidate = leanHitBuffer[index].collider;
                if (ShouldIgnoreCollider(candidate) ||
                    leanHitBuffer[index].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = leanHitBuffer[index].distance;
                nearestHit = leanHitBuffer[index];
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                return LeanResolution.Unblocked(requestedAngleDegrees);
            }

            float allowedChordDistance = Mathf.Clamp(
                nearestDistance - leanCollisionClearanceMeters,
                0f,
                chordDistance);
            Vector3 allowedCenter =
                start + chord / chordDistance * allowedChordDistance;
            Vector3 allowedOffsetLocal =
                Quaternion.Inverse(baseWorldRotation) *
                (allowedCenter - pivotWorldPosition);
            float allowedAngle = Mathf.Clamp(
                Mathf.Atan2(
                    allowedOffsetLocal.z,
                    allowedOffsetLocal.y) * Mathf.Rad2Deg,
                0f,
                requestedAngleDegrees);

            return new LeanResolution(
                allowedAngle,
                nearestHit.point,
                nearestHit.normal,
                nearestHit.collider);
        }

        private bool HasHeadroom(float targetEyeHeight)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null)
            {
                return false;
            }

            CaptureViewRigBaseIfNeeded();
            Vector3 currentHeadCenter = GetUnleanedHeadCenterWorld(
                CurrentEyeHeightMeters());
            Vector3 targetHeadCenter = GetUnleanedHeadCenterWorld(
                targetEyeHeight);
            Vector3 path = targetHeadCenter - currentHeadCenter;
            float pathDistance = path.magnitude;

            if (pathDistance > 0.0001f)
            {
                int castCount = Physics.SphereCastNonAlloc(
                    currentHeadCenter,
                    headClearanceRadiusMeters,
                    path / pathDistance,
                    leanHitBuffer,
                    pathDistance,
                    headroomMask,
                    QueryTriggerInteraction.Ignore);
                for (int index = 0; index < castCount; index++)
                {
                    if (!ShouldIgnoreCollider(leanHitBuffer[index].collider))
                    {
                        return false;
                    }
                }
            }

            int overlapCount = Physics.OverlapSphereNonAlloc(
                targetHeadCenter,
                headClearanceRadiusMeters,
                headroomBuffer,
                headroomMask,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlapCount; index++)
            {
                if (!ShouldIgnoreCollider(headroomBuffer[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TrySnapToGround()
        {
            float radius = Mathf.Max(
                0.01f,
                characterController.radius - characterController.skinWidth);
            Vector3 origin =
                transform.position +
                Vector3.up * (radius + groundProbeLiftMeters);
            float probeDistance =
                groundSnapDistanceMeters + groundProbeLiftMeters;
            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                groundHitBuffer,
                probeDistance,
                headroomMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestNormal = Vector3.up;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = groundHitBuffer[index];
                if (ShouldIgnoreCollider(hit.collider) ||
                    !IsWalkableGround(hit.normal) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestNormal = hit.normal;
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                return false;
            }

            float downwardDistance = Mathf.Max(
                0f,
                nearestDistance - groundProbeLiftMeters);
            if (downwardDistance > groundSnapDistanceMeters + 0.001f)
            {
                return false;
            }

            CollisionFlags flags = downwardDistance > 0.0001f
                ? characterController.Move(Vector3.down * downwardDistance)
                : CollisionFlags.Below;
            groundNormal = nearestNormal;
            return (flags & CollisionFlags.Below) != 0 ||
                characterController.isGrounded ||
                downwardDistance <= 0.0001f;
        }

        private void TriggerLeanImpact(
            in LeanResolution resolution,
            float forwardSpeedMetersPerSecond)
        {
            if (leanImpactCooldownRemaining > 0f)
            {
                return;
            }

            leanImpactCooldownRemaining = leanImpactCooldownSeconds;
            impactPitchDegrees = Mathf.Max(
                impactPitchDegrees,
                leanImpactKickDegrees);
            impactRollDegrees = (LeanImpactCount & 1) == 0
                ? leanImpactKickDegrees * 0.2f
                : -leanImpactKickDegrees * 0.2f;
            horizontalVelocity *= leanImpactRetainedSpeed;

            LastLeanImpact = new PlayerLeanImpact(
                resolution.HitPoint,
                resolution.HitNormal,
                forwardSpeedMetersPerSecond,
                resolution.HitCollider);
            LeanImpactCount++;
            ApplyImpactRotation();
            ForwardLeanImpactOccurred?.Invoke(LastLeanImpact);
        }

        private void UpdateImpactFeedback(float deltaTime)
        {
            leanImpactCooldownRemaining = Mathf.Max(
                0f,
                leanImpactCooldownRemaining - deltaTime);
            impactPitchDegrees = Mathf.MoveTowards(
                impactPitchDegrees,
                0f,
                leanImpactKickReturnDegreesPerSecond * deltaTime);
            impactRollDegrees = Mathf.MoveTowards(
                impactRollDegrees,
                0f,
                leanImpactKickReturnDegreesPerSecond * deltaTime);
            ApplyImpactRotation();
        }

        private float SpeedForCurrentState()
        {
            return posture switch
            {
                PlayerPosture.Crouch =>
                    FirstPersonMovementMath.ResolveDirectionalSpeed(
                        moveInput,
                        crouchingSpeedMetersPerSecond,
                        crouchingBackwardSpeedMetersPerSecond,
                        crouchingStrafeSpeedMetersPerSecond),
                PlayerPosture.DeepCrouch =>
                    deepCrouchingSpeedMetersPerSecond,
                _ => IsRunning
                    ? FirstPersonMovementMath.ResolveDirectionalSpeed(
                        moveInput,
                        runningSpeedMetersPerSecond,
                        runningBackwardSpeedMetersPerSecond,
                        runningStrafeSpeedMetersPerSecond)
                    : FirstPersonMovementMath.ResolveDirectionalSpeed(
                        moveInput,
                        walkingSpeedMetersPerSecond,
                        walkingBackwardSpeedMetersPerSecond,
                        walkingStrafeSpeedMetersPerSecond),
            };
        }

        private float HeightFor(PlayerPosture value)
        {
            return value switch
            {
                PlayerPosture.Crouch => crouchingHeightMeters,
                PlayerPosture.DeepCrouch => deepCrouchingHeightMeters,
                _ => standingHeightMeters,
            };
        }

        private float EyeHeightFor(PlayerPosture value)
        {
            return value switch
            {
                PlayerPosture.Crouch => crouchingEyeHeightMeters,
                PlayerPosture.DeepCrouch =>
                    deepCrouchingEyeHeightMeters,
                _ => standingEyeHeightMeters,
            };
        }

        private float PostureTransitionSpeed(
            float currentHeightMeters,
            float targetHeightMeters) =>
            targetHeightMeters > currentHeightMeters + 0.001f
                ? postureRiseTransitionMetersPerSecond
                : postureTransitionMetersPerSecond;

        private float ResolveJumpForceMetersPerSecond()
        {
            if (float.IsFinite(jumpForceMetersPerSecond) &&
                jumpForceMetersPerSecond > 0f)
            {
                return jumpForceMetersPerSecond;
            }

            return Mathf.Sqrt(
                2f * gravityMetersPerSecondSquared * jumpHeightMeters);
        }

        private void ApplyControllerGeometry()
        {
            if (characterController == null)
            {
                return;
            }

            float height = Mathf.Max(
                controllerRadiusMeters * 2f,
                HeightFor(posture));
            characterController.radius = controllerRadiusMeters;
            characterController.height = height;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
            characterController.slopeLimit = slopeLimitDegrees;
            characterController.skinWidth = Mathf.Min(
                controllerSkinWidthMeters,
                controllerRadiusMeters);
            characterController.minMoveDistance =
                controllerMinimumMoveDistanceMeters;
            UpdateStepOffset(height);
        }

        private void UpdateStepOffset(float currentHeight)
        {
            if (characterController == null)
            {
                return;
            }

            characterController.stepOffset = Mathf.Min(
                stepOffsetMeters,
                Mathf.Max(0f, currentHeight - 0.001f));
        }

        private void CaptureViewRigBaseIfNeeded()
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (viewRigBaseCaptured || resolvedLeanPivot == null)
            {
                return;
            }

            leanPivotBaseLocalPosition = resolvedLeanPivot.localPosition;
            leanPivotBaseLocalRotation = resolvedLeanPivot.localRotation;
            if (cameraPivot != null && cameraPivot != resolvedLeanPivot)
            {
                cameraPivotBaseLocalPosition = cameraPivot.localPosition;
                headOffsetFromLeanPivotLocal =
                    resolvedLeanPivot.InverseTransformPoint(cameraPivot.position);
            }
            else
            {
                cameraPivotBaseLocalPosition = Vector3.zero;
                headOffsetFromLeanPivotLocal = Vector3.up * leanArcRadiusMeters;
            }

            viewRigBaseEyeHeightMeters =
                leanPivotBaseLocalPosition.y +
                headOffsetFromLeanPivotLocal.y;

            if (impactPivot != null)
            {
                impactPivotBaseLocalRotation = impactPivot.localRotation;
            }

            viewRigBaseCaptured = true;
        }

        private void SetEyeHeightImmediate(float eyeHeightMeters)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null)
            {
                return;
            }

            if (HasSeparateCameraPivot(resolvedLeanPivot))
            {
                resolvedLeanPivot.localPosition = leanPivotBaseLocalPosition;
                Vector3 cameraPosition = cameraPivot.localPosition;
                cameraPosition.x = cameraPivotBaseLocalPosition.x;
                cameraPosition.y = CameraPivotLocalYForEyeHeight(
                    eyeHeightMeters);
                cameraPosition.z = cameraPivotBaseLocalPosition.z;
                cameraPivot.localPosition = cameraPosition;
                return;
            }

            Vector3 pivotPosition = resolvedLeanPivot.localPosition;
            pivotPosition.x = leanPivotBaseLocalPosition.x;
            pivotPosition.y =
                eyeHeightMeters - headOffsetFromLeanPivotLocal.y;
            pivotPosition.z = leanPivotBaseLocalPosition.z;
            resolvedLeanPivot.localPosition = pivotPosition;
        }

        private void MoveEyeHeightTowards(
            float eyeHeightMeters,
            float maximumDeltaMeters)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null)
            {
                return;
            }

            if (HasSeparateCameraPivot(resolvedLeanPivot))
            {
                // The donor Reach pivot stays below the body. Posture changes
                // only the camera offset so leaning remains a full-body arc.
                resolvedLeanPivot.localPosition = leanPivotBaseLocalPosition;
                Vector3 cameraPosition = cameraPivot.localPosition;
                cameraPosition.x = cameraPivotBaseLocalPosition.x;
                cameraPosition.y = Mathf.MoveTowards(
                    cameraPosition.y,
                    CameraPivotLocalYForEyeHeight(eyeHeightMeters),
                    maximumDeltaMeters);
                cameraPosition.z = cameraPivotBaseLocalPosition.z;
                cameraPivot.localPosition = cameraPosition;
                return;
            }

            Vector3 pivotPosition = resolvedLeanPivot.localPosition;
            pivotPosition.x = leanPivotBaseLocalPosition.x;
            pivotPosition.y = Mathf.MoveTowards(
                pivotPosition.y,
                eyeHeightMeters - headOffsetFromLeanPivotLocal.y,
                maximumDeltaMeters);
            pivotPosition.z = leanPivotBaseLocalPosition.z;
            resolvedLeanPivot.localPosition = pivotPosition;
        }

        private float CurrentEyeHeightMeters()
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null)
            {
                return EyeHeightFor(posture);
            }

            CaptureViewRigBaseIfNeeded();
            if (HasSeparateCameraPivot(resolvedLeanPivot))
            {
                return viewRigBaseEyeHeightMeters +
                    cameraPivot.localPosition.y -
                    cameraPivotBaseLocalPosition.y;
            }

            return resolvedLeanPivot.localPosition.y +
                headOffsetFromLeanPivotLocal.y;
        }

        private Vector3 GetUnleanedHeadCenterWorld(float eyeHeightMeters)
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            Transform parent = resolvedLeanPivot.parent;
            Vector3 headOffset =
                headOffsetFromLeanPivotLocal +
                Vector3.up *
                (eyeHeightMeters - viewRigBaseEyeHeightMeters);
            Vector3 headPositionInParent =
                leanPivotBaseLocalPosition +
                leanPivotBaseLocalRotation * headOffset;
            return parent != null
                ? parent.TransformPoint(headPositionInParent)
                : headPositionInParent;
        }

        private Vector3 GetCurrentHeadOffsetFromLeanPivotLocal()
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (HasSeparateCameraPivot(resolvedLeanPivot))
            {
                return resolvedLeanPivot.InverseTransformPoint(
                    cameraPivot.position);
            }

            return Vector3.up * leanArcRadiusMeters;
        }

        private float CameraPivotLocalYForEyeHeight(float eyeHeightMeters) =>
            cameraPivotBaseLocalPosition.y +
            eyeHeightMeters -
            viewRigBaseEyeHeightMeters;

        private bool HasSeparateCameraPivot(Transform resolvedLeanPivot) =>
            cameraPivot != null && cameraPivot != resolvedLeanPivot;

        private Quaternion GetLeanBaseWorldRotation()
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            return resolvedLeanPivot.parent != null
                ? resolvedLeanPivot.parent.rotation * leanPivotBaseLocalRotation
                : leanPivotBaseLocalRotation;
        }

        private Transform GetLeanPivot() => leanPivot != null
            ? leanPivot
            : cameraPivot;

        private void ApplyLeanRotation()
        {
            Transform resolvedLeanPivot = GetLeanPivot();
            if (resolvedLeanPivot == null || !viewRigBaseCaptured)
            {
                return;
            }

            resolvedLeanPivot.localRotation =
                leanPivotBaseLocalRotation *
                Quaternion.Euler(currentLeanAngleDegrees, 0f, 0f);
        }

        private void ApplyImpactRotation()
        {
            if (impactPivot == null || !viewRigBaseCaptured)
            {
                return;
            }

            impactPivot.localRotation =
                impactPivotBaseLocalRotation *
                Quaternion.Euler(
                    impactPitchDegrees,
                    0f,
                    impactRollDegrees);
        }

        private bool ShouldIgnoreCollider(Collider candidate) =>
            candidate == null ||
            candidate == characterController ||
            candidate.transform == transform ||
            candidate.transform.IsChildOf(transform);

        private bool IsWalkableGround(Vector3 normal) =>
            normal.y > GroundNormalMinimumY &&
            Vector3.Angle(normal, Vector3.up) <=
                characterController.slopeLimit + 0.01f;

        private static Vector3 ProjectDirectionOntoGround(
            Vector3 direction,
            Vector3 normal)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 projected = Vector3.ProjectOnPlane(direction, normal);
            return projected.sqrMagnitude > 0.0001f
                ? projected.normalized * direction.magnitude
                : Vector3.zero;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.rigidbody;
            if (IsWalkableGround(hit.normal))
            {
                receivedGroundContact = true;
                if (hit.normal.y < contactGroundNormal.y ||
                    contactGroundNormal == Vector3.up)
                {
                    contactGroundNormal = hit.normal;
                }

            }

            if (hit.normal.y < SupportSideConflictMaximumNormalY)
            {
                RecordSideContactBody(body);
            }

            if (body == null || body.isKinematic)
            {
                return;
            }

            bool canReceivePlayerPush =
                body.mass <= maximumPushableMassKilograms &&
                CanReceivePlayerPush(hit, body);
            if (!canReceivePlayerPush)
            {
                return;
            }

            Vector3 pushDirection = new Vector3(
                hit.moveDirection.x,
                0f,
                hit.moveDirection.z);
            if (pushDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float movementScale = Mathf.Clamp01(hit.moveLength / 0.04f);
            body.AddForceAtPosition(
                pushDirection.normalized *
                (rigidbodyPushForceNewtons * movementScale),
                hit.point,
                ForceMode.Force);
            body.WakeUp();
        }

        private bool CanReceivePlayerPush(
            ControllerColliderHit hit,
            Rigidbody body)
        {
            if (hit.collider == null)
            {
                return false;
            }

            var interactionContext = new InteractionContext(
                gameObject,
                transform.position,
                hit.moveDirection);
            Transform candidate = hit.collider.transform;
            while (candidate != null)
            {
                InteractionTargetHost host =
                    candidate.GetComponent<InteractionTargetHost>();
                if (host != null &&
                    host.TryGetCapability(out IPickupTarget pickupTarget) &&
                    pickupTarget.Body == body &&
                    pickupTarget.CanPickup(interactionContext))
                {
                    return true;
                }

                if (candidate == body.transform)
                {
                    break;
                }

                candidate = candidate.parent;
            }

            return false;
        }

        private void RecordSideContactBody(Rigidbody body)
        {
            Rigidbody aggregate = ResolveDynamicAggregateRoot(body);
            if (aggregate == null)
            {
                return;
            }

            for (int index = 0; index < sideContactBodyCount; index++)
            {
                if (sideContactBodyBuffer[index] == aggregate)
                {
                    return;
                }
            }

            if (sideContactBodyCount < sideContactBodyBuffer.Length)
            {
                sideContactBodyBuffer[sideContactBodyCount++] = aggregate;
            }

            if (BelongsToDynamicAggregate(supportingRigidbody, aggregate))
            {
                // A rocker or bumper may sit under the feet ray while another
                // collider on the same vehicle blocks the capsule from the
                // side. Keeping the previous support grace here turns body
                // weight into a sustained off-centre vehicle force.
                ClearBodyWeightSupport();
            }
        }

        private void ObserveBodyWeightSupport()
        {
            if (!TryResolveBodyWeightSupport(
                    out Rigidbody body,
                    out Vector3 contactPoint))
            {
                return;
            }

            if (supportingRigidbody != body)
            {
                supportLoadBlend = 0f;
                supportingContactAcquiredTime = Time.time;
            }

            supportingRigidbody = body;
            supportingContactPointLocal =
                body.transform.InverseTransformPoint(contactPoint);
            supportingContactObservedTime = Time.time;
        }

        private bool TryResolveBodyWeightSupport(
            out Rigidbody body,
            out Vector3 contactPoint)
        {
            body = null;
            contactPoint = Vector3.zero;
            Vector3 centerOrigin =
                transform.position + Vector3.up * supportProbeLiftMeters;
            if (!TryResolveNearestSupportSurface(
                    centerOrigin,
                    out RaycastHit nearestHit) ||
                nearestHit.normal.y < minimumSupportSurfaceNormalY)
            {
                return false;
            }

            Rigidbody candidate = ResolveDynamicSupportBody(
                nearestHit.rigidbody);
            if (candidate == null)
            {
                // A static floor is an occluder, not a transparent window to
                // any vehicle collider that happens to sit deeper in the ray.
                return false;
            }


            if (HasSideContactWithDynamicAggregate(candidate))
            {
                return false;
            }

            Rigidbody aggregate = ResolveDynamicAggregateRoot(candidate);
            float footprintRadius = Mathf.Max(
                0f,
                characterController != null
                    ? characterController.radius
                    : controllerRadiusMeters) *
                supportFootprintRadiusScale;
            if (aggregate == null ||
                !HasConsistentSupportFootprint(
                    centerOrigin,
                    footprintRadius,
                    aggregate))
            {
                return false;
            }

            body = candidate;
            contactPoint = nearestHit.point;
            return true;
        }

        private bool HasConsistentSupportFootprint(
            Vector3 centerOrigin,
            float footprintRadius,
            Rigidbody expectedAggregate)
        {
            if (footprintRadius <= 0f)
            {
                return true;
            }

            Vector3 right = transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f
                ? right.normalized
                : Vector3.right;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;

            for (int index = 0; index < 4; index++)
            {
                Vector3 direction;
                switch (index)
                {
                    case 0:
                        direction = right;
                        break;
                    case 1:
                        direction = -right;
                        break;
                    case 2:
                        direction = forward;
                        break;
                    default:
                        direction = -forward;
                        break;
                }

                if (!TryResolveNearestSupportSurface(
                        centerOrigin + direction * footprintRadius,
                        out RaycastHit footprintHit) ||
                    footprintHit.normal.y < minimumSupportSurfaceNormalY)
                {
                    return false;
                }

                Rigidbody footprintBody = ResolveDynamicSupportBody(
                    footprintHit.rigidbody);
                if (ResolveDynamicAggregateRoot(footprintBody) !=
                    expectedAggregate)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryResolveNearestSupportSurface(
            Vector3 origin,
            out RaycastHit nearestHit)
        {
            float distance = supportProbeLiftMeters + supportProbeDepthMeters;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                supportHitBuffer,
                distance,
                headroomMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = supportHitBuffer[index];
                if (ShouldIgnoreCollider(hit.collider) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }

        private static Rigidbody ResolveDynamicSupportBody(
            Rigidbody nearestBody)
        {
            if (nearestBody == null)
            {
                return null;
            }

            if (!nearestBody.isKinematic)
            {
                return nearestBody;
            }

            // Installed kinematic panels and player-only proxy bodies remain
            // legitimate surfaces only when they are structurally nested
            // under a dynamic chassis. Unrelated kinematic world geometry
            // must not make the probe tunnel through to another body.
            Transform ancestor = nearestBody.transform.parent;
            while (ancestor != null)
            {
                Rigidbody ancestorBody = ancestor.GetComponent<Rigidbody>();
                if (ancestorBody != null && !ancestorBody.isKinematic)
                {
                    return ancestorBody;
                }

                ancestor = ancestor.parent;
            }

            return null;
        }

        private bool HasSideContactWithDynamicAggregate(Rigidbody body)
        {
            Rigidbody aggregate = ResolveDynamicAggregateRoot(body);
            if (aggregate == null)
            {
                return false;
            }

            for (int index = 0; index < sideContactBodyCount; index++)
            {
                if (sideContactBodyBuffer[index] == aggregate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BelongsToDynamicAggregate(
            Rigidbody body,
            Rigidbody aggregate)
        {
            return aggregate != null &&
                ResolveDynamicAggregateRoot(body) == aggregate;
        }

        private static Rigidbody ResolveDynamicAggregateRoot(Rigidbody body)
        {
            Rigidbody resolved = ResolveDynamicSupportBody(body);
            if (resolved == null)
            {
                return null;
            }

            Rigidbody aggregate = resolved;
            Transform ancestor = resolved.transform.parent;
            while (ancestor != null)
            {
                Rigidbody ancestorBody = ancestor.GetComponent<Rigidbody>();
                if (ancestorBody != null && !ancestorBody.isKinematic)
                {
                    aggregate = ancestorBody;
                }

                ancestor = ancestor.parent;
            }

            return aggregate;
        }

        private void ApplyBodyWeightToSupport(float fixedDeltaTime)
        {
            float observationGraceSeconds = Mathf.Max(
                0.12f,
                Time.fixedDeltaTime * 3f);
            if (supportingRigidbody == null ||
                supportingRigidbody.isKinematic ||
                Time.time - supportingContactObservedTime >
                    observationGraceSeconds ||
                fixedDeltaTime <= 0f ||
                supportLoadScale <= 0f)
            {
                if (supportingRigidbody != null &&
                    Time.time - supportingContactObservedTime >
                        observationGraceSeconds)
                {
                    ClearBodyWeightSupport();
                }
                else
                {
                    LastSupportedRigidbody = null;
                    LastSupportLoadImpulseNewtonSeconds = Vector3.zero;
                }

                return;
            }

            if (Time.time - supportingContactAcquiredTime <
                supportConfirmationSeconds)
            {
                LastSupportedRigidbody = null;
                LastSupportLoadImpulseNewtonSeconds = Vector3.zero;
                return;
            }

            supportLoadBlend = supportLoadRampSeconds <= 0f
                ? 1f
                : Mathf.MoveTowards(
                    supportLoadBlend,
                    1f,
                    fixedDeltaTime / supportLoadRampSeconds);
            Vector3 loadForceNewtons =
                Physics.gravity *
                bodyMassKilograms *
                supportLoadScale *
                supportLoadBlend;
            if (loadForceNewtons.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 contactPoint = supportingRigidbody.transform
                .TransformPoint(supportingContactPointLocal);
            supportingRigidbody.AddForceAtPosition(
                loadForceNewtons,
                contactPoint,
                ForceMode.Force);
            supportingRigidbody.WakeUp();
            LastSupportedRigidbody = supportingRigidbody;
            LastSupportLoadImpulseNewtonSeconds =
                loadForceNewtons * fixedDeltaTime;
        }

        private void ClearBodyWeightSupport()
        {
            supportingRigidbody = null;
            supportingContactPointLocal = Vector3.zero;
            supportingContactObservedTime = float.NegativeInfinity;
            supportingContactAcquiredTime = float.NegativeInfinity;
            supportLoadBlend = 0f;
            LastSupportedRigidbody = null;
            LastSupportLoadImpulseNewtonSeconds = Vector3.zero;
        }

        private readonly struct LeanResolution
        {
            public LeanResolution(
                float allowedAngleDegrees,
                Vector3 hitPoint,
                Vector3 hitNormal,
                Collider hitCollider)
            {
                AllowedAngleDegrees = allowedAngleDegrees;
                HitPoint = hitPoint;
                HitNormal = hitNormal;
                HitCollider = hitCollider;
                Blocked = hitCollider != null;
            }

            public float AllowedAngleDegrees { get; }
            public Vector3 HitPoint { get; }
            public Vector3 HitNormal { get; }
            public Collider HitCollider { get; }
            public bool Blocked { get; }

            public static LeanResolution Unblocked(float angleDegrees) =>
                new LeanResolution(
                    angleDegrees,
                    Vector3.zero,
                    Vector3.zero,
                    null);
        }
    }
}
