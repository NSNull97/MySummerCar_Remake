using UnityEngine;

namespace MSC.Player
{
    /// <summary>
    /// Restrained presentation feedback for look and traversal. It deliberately
    /// has no cyclic walking bob and never owns gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraMotion : MonoBehaviour
    {
        private const float MaximumPresentationDeltaTime = 1f / 30f;

        [SerializeField]
        private FirstPersonMotor motor;

        [SerializeField]
        private FirstPersonLook look;

        [SerializeField]
        private Transform motionPivot;

        [SerializeField, Range(0f, 2f)]
        private float maximumLookRollDegrees = 0.4f;

        [SerializeField, Min(0.1f)]
        private float fullLookRollInputDegrees = 4f;

        [SerializeField, Min(0.001f)]
        private float lookReturnSeconds = 0.06f;

        [SerializeField, Range(0f, 0.5f)]
        private float lookInertiaPerInputDegree = 0.18f;

        [SerializeField, Range(0f, 3f)]
        private float maximumYawInertiaDegrees = 1.25f;

        [SerializeField, Range(0f, 2f)]
        private float maximumPitchInertiaDegrees = 0.85f;

        [SerializeField, Min(0.001f)]
        private float lookInertiaReturnSeconds = 0.08f;

        [SerializeField, Range(0f, 0.02f)]
        private float turnSwayMetersPerInputDegree = 0.003f;

        [SerializeField, Range(0f, 0.05f)]
        private float maximumTurnSwayMeters = 0.018f;

        [SerializeField, Min(0.001f)]
        private float turnSwayReturnSeconds = 0.09f;

        [SerializeField, Min(0f)]
        private float jumpDipMeters = 0.055f;

        [SerializeField, Min(0f)]
        private float jumpPitchDegrees = 1.15f;

        [SerializeField, Min(0f)]
        private float jumpPreparationDipMeters = 0.07f;

        [SerializeField, Min(0f)]
        private float jumpPreparationPitchDegrees = 0.35f;

        [SerializeField, Min(0.001f)]
        private float impulseAttackSeconds = 0.04f;

        [SerializeField, Min(0f)]
        private float minimumLandingSpeedMetersPerSecond = 4f;

        [SerializeField, Min(0f)]
        private float maximumLandingSpeedMetersPerSecond = 11f;

        [SerializeField, Min(0f)]
        private float minimumJumpForceMetersPerSecond = 4f;

        [SerializeField, Min(0f)]
        private float maximumJumpForceMetersPerSecond = 9f;

        [SerializeField, Range(0f, 1f)]
        private float jumpForceLandingInfluence = 0.35f;

        [SerializeField, Min(0f)]
        private float maximumLandingDipMeters = 0.14f;

        [SerializeField, Min(0f)]
        private float maximumLandingPitchDegrees = 3f;

        [SerializeField, Min(0.001f)]
        private float impulseReturnSeconds = 0.15f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation = Quaternion.identity;
        private float verticalOffsetMeters;
        private float verticalOffsetVelocity;
        private float targetVerticalOffsetMeters;
        private float pitchOffsetDegrees;
        private float pitchOffsetVelocity;
        private float targetPitchOffsetDegrees;
        private float impulseAttackRemainingSeconds;
        private float rollOffsetDegrees;
        private float rollOffsetVelocity;
        private float targetLookRollDegrees;
        private float yawInertiaDegrees;
        private float yawInertiaVelocity;
        private float pitchInertiaDegrees;
        private float pitchInertiaVelocity;
        private float turnSwayMeters;
        private float turnSwayVelocity;
        private bool basePoseCaptured;
        private bool subscribed;

        public float CurrentVerticalOffsetMeters => verticalOffsetMeters;

        public float CurrentPitchOffsetDegrees => pitchOffsetDegrees;

        public float CurrentYawInertiaDegrees => yawInertiaDegrees;

        public float CurrentPitchInertiaDegrees => pitchInertiaDegrees;

        public float CurrentTurnSwayMeters => turnSwayMeters;

        public float LastLandingStrength { get; private set; }

        public void Configure(
            FirstPersonMotor movement,
            FirstPersonLook playerLook,
            Transform authoredMotionPivot)
        {
            Unsubscribe();
            motor = movement;
            look = playerLook;
            motionPivot = authoredMotionPivot;
            basePoseCaptured = false;
            CaptureBasePose();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Reset()
        {
            motionPivot = transform;
        }

        private void Awake()
        {
            if (motionPivot == null)
            {
                motionPivot = transform;
            }

            CaptureBasePose();
        }

        private void OnEnable()
        {
            CaptureBasePose();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetMotion();
        }

        private void LateUpdate()
        {
            float deltaTime = Mathf.Min(
                Time.unscaledDeltaTime,
                MaximumPresentationDeltaTime);
            if (deltaTime <= 0f || motionPivot == null)
            {
                return;
            }

            verticalOffsetMeters = Mathf.SmoothDamp(
                verticalOffsetMeters,
                targetVerticalOffsetMeters,
                ref verticalOffsetVelocity,
                impulseAttackRemainingSeconds > 0f
                    ? impulseAttackSeconds
                    : impulseReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            pitchOffsetDegrees = Mathf.SmoothDamp(
                pitchOffsetDegrees,
                targetPitchOffsetDegrees,
                ref pitchOffsetVelocity,
                impulseAttackRemainingSeconds > 0f
                    ? impulseAttackSeconds
                    : impulseReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            impulseAttackRemainingSeconds = Mathf.Max(
                0f,
                impulseAttackRemainingSeconds - deltaTime);
            if (impulseAttackRemainingSeconds <= 0f)
            {
                targetVerticalOffsetMeters = 0f;
                targetPitchOffsetDegrees = 0f;
            }
            rollOffsetDegrees = Mathf.SmoothDamp(
                rollOffsetDegrees,
                targetLookRollDegrees,
                ref rollOffsetVelocity,
                lookReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            yawInertiaDegrees = Mathf.SmoothDamp(
                yawInertiaDegrees,
                0f,
                ref yawInertiaVelocity,
                lookInertiaReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            pitchInertiaDegrees = Mathf.SmoothDamp(
                pitchInertiaDegrees,
                0f,
                ref pitchInertiaVelocity,
                lookInertiaReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            turnSwayMeters = Mathf.SmoothDamp(
                turnSwayMeters,
                0f,
                ref turnSwayVelocity,
                turnSwayReturnSeconds,
                Mathf.Infinity,
                deltaTime);
            targetLookRollDegrees = Mathf.MoveTowards(
                targetLookRollDegrees,
                0f,
                maximumLookRollDegrees * 12f * deltaTime);

            ApplyMotion();
        }

        private void OnJumped()
        {
            BeginImpulse(
                -jumpDipMeters,
                -jumpPitchDegrees,
                impulseAttackSeconds);
        }

        private void OnJumpPreparationStarted(float preparationSeconds)
        {
            BeginImpulse(
                -jumpPreparationDipMeters,
                jumpPreparationPitchDegrees,
                preparationSeconds);
        }

        private void OnJumpPreparationCancelled()
        {
            targetVerticalOffsetMeters = 0f;
            targetPitchOffsetDegrees = 0f;
            impulseAttackRemainingSeconds = 0f;
        }

        private void OnLanded(PlayerLandingImpact impact)
        {
            float strength = CalculateLandingStrength(
                impact.ImpactSpeedMetersPerSecond,
                impact.JumpForceMetersPerSecond,
                minimumLandingSpeedMetersPerSecond,
                maximumLandingSpeedMetersPerSecond,
                minimumJumpForceMetersPerSecond,
                maximumJumpForceMetersPerSecond,
                jumpForceLandingInfluence);
            LastLandingStrength = strength;
            if (strength <= 0f)
            {
                return;
            }

            BeginImpulse(
                -maximumLandingDipMeters * strength,
                maximumLandingPitchDegrees * strength,
                impulseAttackSeconds);
        }

        public static float CalculateLandingStrength(
            float impactSpeedMetersPerSecond,
            float jumpForceMetersPerSecond,
            float minimumLandingSpeedMetersPerSecond,
            float maximumLandingSpeedMetersPerSecond,
            float minimumJumpForceMetersPerSecond,
            float maximumJumpForceMetersPerSecond,
            float jumpForceInfluence)
        {
            float impactRange = Mathf.Max(
                0.001f,
                maximumLandingSpeedMetersPerSecond -
                minimumLandingSpeedMetersPerSecond);
            float impactStrength = Mathf.Clamp01(
                (impactSpeedMetersPerSecond -
                    minimumLandingSpeedMetersPerSecond) /
                impactRange);
            if (jumpForceMetersPerSecond <= 0f ||
                jumpForceInfluence <= 0f)
            {
                return impactStrength;
            }

            float jumpForceRange = Mathf.Max(
                0.001f,
                maximumJumpForceMetersPerSecond -
                minimumJumpForceMetersPerSecond);
            float jumpStrength = Mathf.Clamp01(
                (jumpForceMetersPerSecond -
                    minimumJumpForceMetersPerSecond) /
                jumpForceRange);
            return Mathf.Lerp(
                impactStrength,
                1f,
                jumpStrength * Mathf.Clamp01(jumpForceInfluence));
        }

        private void OnLookApplied(Vector2 rotationDegrees)
        {
            float normalizedYaw = Mathf.Clamp(
                rotationDegrees.x / fullLookRollInputDegrees,
                -1f,
                1f);
            targetLookRollDegrees = -normalizedYaw * maximumLookRollDegrees;
            yawInertiaDegrees = Mathf.Clamp(
                yawInertiaDegrees -
                    rotationDegrees.x * lookInertiaPerInputDegree,
                -maximumYawInertiaDegrees,
                maximumYawInertiaDegrees);
            pitchInertiaDegrees = Mathf.Clamp(
                pitchInertiaDegrees +
                    rotationDegrees.y * lookInertiaPerInputDegree,
                -maximumPitchInertiaDegrees,
                maximumPitchInertiaDegrees);
            turnSwayMeters = Mathf.Clamp(
                turnSwayMeters -
                    rotationDegrees.x * turnSwayMetersPerInputDegree,
                -maximumTurnSwayMeters,
                maximumTurnSwayMeters);
            ApplyMotion();
        }

        private void CaptureBasePose()
        {
            if (basePoseCaptured || motionPivot == null)
            {
                return;
            }

            baseLocalPosition = motionPivot.localPosition;
            baseLocalRotation = motionPivot.localRotation;
            basePoseCaptured = true;
        }

        private void ApplyMotion()
        {
            if (motionPivot == null || !basePoseCaptured)
            {
                return;
            }

            motionPivot.localPosition =
                baseLocalPosition +
                new Vector3(turnSwayMeters, verticalOffsetMeters, 0f);
            motionPivot.localRotation =
                baseLocalRotation *
                Quaternion.Euler(
                    pitchOffsetDegrees + pitchInertiaDegrees,
                    yawInertiaDegrees,
                    rollOffsetDegrees);
        }

        private void BeginImpulse(
            float verticalTargetMeters,
            float pitchTargetDegrees,
            float holdSeconds)
        {
            targetVerticalOffsetMeters = verticalTargetMeters;
            targetPitchOffsetDegrees = pitchTargetDegrees;
            impulseAttackRemainingSeconds = Mathf.Max(0f, holdSeconds);
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (motor != null)
            {
                motor.JumpPreparationStarted += OnJumpPreparationStarted;
                motor.JumpPreparationCancelled += OnJumpPreparationCancelled;
                motor.Jumped += OnJumped;
                motor.LandingImpactOccurred += OnLanded;
            }

            if (look != null)
            {
                look.LookApplied += OnLookApplied;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (motor != null)
            {
                motor.JumpPreparationStarted -= OnJumpPreparationStarted;
                motor.JumpPreparationCancelled -= OnJumpPreparationCancelled;
                motor.Jumped -= OnJumped;
                motor.LandingImpactOccurred -= OnLanded;
            }

            if (look != null)
            {
                look.LookApplied -= OnLookApplied;
            }

            subscribed = false;
        }

        private void ResetMotion()
        {
            verticalOffsetMeters = 0f;
            verticalOffsetVelocity = 0f;
            targetVerticalOffsetMeters = 0f;
            pitchOffsetDegrees = 0f;
            pitchOffsetVelocity = 0f;
            targetPitchOffsetDegrees = 0f;
            impulseAttackRemainingSeconds = 0f;
            rollOffsetDegrees = 0f;
            rollOffsetVelocity = 0f;
            targetLookRollDegrees = 0f;
            yawInertiaDegrees = 0f;
            yawInertiaVelocity = 0f;
            pitchInertiaDegrees = 0f;
            pitchInertiaVelocity = 0f;
            turnSwayMeters = 0f;
            turnSwayVelocity = 0f;
            LastLandingStrength = 0f;
            ApplyMotion();
        }
    }
}
