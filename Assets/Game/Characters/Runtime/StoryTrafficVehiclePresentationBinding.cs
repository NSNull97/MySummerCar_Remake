using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Characters
{
    public enum StoryTrafficManeuverState
    {
        Cruise = 0,
        Braking = 1,
        PassingOut = 2,
        Passing = 3,
        Returning = 4,
        Reversing = 5,
        Drifting = 6,
        Crashed = 7,
        Recovering = 8,
    }

    public enum StoryTrafficRoadBehaviorProfile
    {
        Perajarvi = 0,
        RoadRace = 1,
        Gravel = 2,
        Bus = 3,
    }

    public readonly struct StoryTrafficCollisionEvent
    {
        public StoryTrafficCollisionEvent(
            string driverFeatureId,
            Collider other,
            Vector3 worldPoint,
            float speedMetersPerSecond,
            bool isCrash = false)
        {
            DriverFeatureId = driverFeatureId ?? string.Empty;
            Other = other;
            WorldPoint = worldPoint;
            SpeedMetersPerSecond = Mathf.Max(0f, speedMetersPerSecond);
            IsCrash = isCrash;
        }

        public string DriverFeatureId { get; }
        public Collider Other { get; }
        public Vector3 WorldPoint { get; }
        public float SpeedMetersPerSecond { get; }
        public bool IsCrash { get; }
    }

    public readonly struct StoryTrafficTerminalCrashEvent
    {
        public StoryTrafficTerminalCrashEvent(
            string driverFeatureId,
            Vector3 worldPoint,
            float speedMetersPerSecond)
        {
            DriverFeatureId = driverFeatureId ?? string.Empty;
            WorldPoint = worldPoint;
            SpeedMetersPerSecond = Mathf.Max(0f, speedMetersPerSecond);
        }

        public string DriverFeatureId { get; }
        public Vector3 WorldPoint { get; }
        public float SpeedMetersPerSecond { get; }
    }

    /// <summary>
    /// Small presentation-state capsule retained by the NPC composition root
    /// while a streamed wrapper is absent. Route authority remains in the NPC
    /// simulation; this state only prevents a crash, recovery or gear-driving
    /// motion from resetting when a cell boundary is crossed.
    /// </summary>
    public readonly struct StoryTrafficMotionRuntimeState
    {
        public StoryTrafficMotionRuntimeState(
            float speedMetersPerSecond,
            float cruiseSpeedMetersPerSecond,
            float laneOffsetMeters,
            StoryTrafficManeuverState maneuverState,
            float maneuverStateSeconds,
            float driftSlipDegrees,
            int recoveryCount,
            bool hasSafePose,
            Vector3 safePosition,
            Quaternion safeRotation,
            float socialStopSecondsRemaining = 0f,
            float lastPhysicalRouteProgress01 = 0f,
            bool hasPhysicalRouteProgress = false,
            bool storyIncidentHold = false)
        {
            SpeedMetersPerSecond = Mathf.Max(0f, speedMetersPerSecond);
            CruiseSpeedMetersPerSecond = Mathf.Max(0f, cruiseSpeedMetersPerSecond);
            LaneOffsetMeters = laneOffsetMeters;
            ManeuverState = maneuverState;
            ManeuverStateSeconds = Mathf.Max(0f, maneuverStateSeconds);
            DriftSlipDegrees = driftSlipDegrees;
            RecoveryCount = Mathf.Max(0, recoveryCount);
            HasSafePose = hasSafePose;
            SafePosition = safePosition;
            SafeRotation = safeRotation;
            SocialStopSecondsRemaining = Mathf.Max(
                0f,
                socialStopSecondsRemaining);
            LastPhysicalRouteProgress01 = Mathf.Clamp01(
                lastPhysicalRouteProgress01);
            HasPhysicalRouteProgress = hasPhysicalRouteProgress;
            StoryIncidentHold = storyIncidentHold;
        }

        public float SpeedMetersPerSecond { get; }
        public float CruiseSpeedMetersPerSecond { get; }
        public float LaneOffsetMeters { get; }
        public StoryTrafficManeuverState ManeuverState { get; }
        public float ManeuverStateSeconds { get; }
        public float DriftSlipDegrees { get; }
        public int RecoveryCount { get; }
        public bool HasSafePose { get; }
        public Vector3 SafePosition { get; }
        public Quaternion SafeRotation { get; }
        public float SocialStopSecondsRemaining { get; }
        public float LastPhysicalRouteProgress01 { get; }
        public bool HasPhysicalRouteProgress { get; }
        public bool StoryIncidentHold { get; }
    }

    /// <summary>
    /// Presentation-only metadata and wheel motion for a sanitized story car.
    /// Route, crew state and persistence remain owned by project NPC systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryTrafficVehiclePresentationBinding : MonoBehaviour
    {
        private const int MaximumPendingRouteSamples = 8192;
        private const float RouteSampleMergeDistanceMeters = 0.04f;
        private const float RouteSampleArrivalDistanceMeters = 0.12f;
        // The donor traffic controller reserves roughly two seconds of a
        // 200-metre forward ray before committing to the opposing lane. A
        // stopping-distance-only probe cannot see a 50+ m/s closing vehicle
        // early enough and produced head-on passing collisions.
        private const float MinimumPassingSightDistanceMeters = 200f;
        private const float StaticObstaclePassingSightDistanceMeters = 55f;
        private const float DonorEmergencyObstacleRangeMeters = 40f;
        // Persistent story traffic can outlive the player's streamed cell. A
        // missing supporting collider must never turn into an unbounded fall
        // which then poisons route projection and save state. The streaming
        // owners retain the current/ahead cells; this guard is the final,
        // bounded safety net for a collider race or a corrupt restored pose.
        private const float CatastrophicFallDepthMeters = 14f;
        private const float CatastrophicFallConfirmationSeconds = 0.3f;
        private const float MaximumRetainedSafePoseSeparationMeters = 120f;
        private const float MaximumRetainedSafePoseVerticalDeltaMeters = 24f;
        private const float MaximumPassingAttemptSeconds = 6f;
        private const float MaximumPassingAttemptDistanceMeters = 55f;
        private const float PassingRetryCooldownSeconds = 3f;
        // The donor HANDBRAKE state must outlive the initial audio transient.
        // 0.48 s restored rear grip before the chassis developed visible yaw;
        // the bounded 0.88 s pulse stays inside the requested 0.8-0.9 s window.
        private const float PhysicalHandbrakePulseSeconds = 0.88f;

        [SerializeField] private string driverFeatureId = string.Empty;
        [SerializeField] private string[] passengerFeatureIds =
            Array.Empty<string>();
        [SerializeField] private GameObject[] passengerPresentationRoots =
            Array.Empty<GameObject>();
        [SerializeField] private Transform[] wheelTransforms =
            Array.Empty<Transform>();
        [SerializeField] private float wheelDegreesPerMeter = 190f;
        [SerializeField] private float groundContactCalibrationMeters;
        [SerializeField] private MonoBehaviour motionBackendComponent;
        [SerializeField] private StoryTrafficInCarRagdollBinding
            inCarRagdollBinding;

        [Header("Project-owned route motion")]
        [SerializeField, Min(1f)] private float minimumCruiseSpeedMetersPerSecond =
            12f;
        [SerializeField, Min(1f)] private float maximumSpeedMetersPerSecond =
            15f;
        [SerializeField, Min(0.1f)] private float accelerationMetersPerSecond2 =
            6.5f;
        [SerializeField, Min(0.1f)] private float brakingMetersPerSecond2 =
            20f;
        [SerializeField, Min(1f)] private float turnRateDegreesPerSecond =
            150f;
        [SerializeField, Min(2f)] private float teleportDistanceMeters = 80f;
        [SerializeField, Min(0.1f)] private float obstacleProbeDistanceMeters =
            7f;
        [SerializeField, Min(0.1f)] private float obstacleProbeRadiusMeters =
            0.65f;
        [SerializeField] private float passingLaneOffsetMeters = -3.2f;
        [SerializeField] private float baseLaneOffsetMeters;
        [SerializeField, Min(0.1f)] private float laneChangeMetersPerSecond =
            2.8f;
        [SerializeField, Min(0.1f)] private float passingClearanceMeters = 6f;
        [SerializeField, Min(0.1f)] private float blockedBeforeReverseSeconds =
            2.5f;
        [SerializeField, Min(0.1f)] private float reverseDurationSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float reverseSpeedMetersPerSecond =
            4f;
        [SerializeField, Min(0.1f)] private float minimumFollowingGapMeters =
            5f;
        [SerializeField, Min(0.1f)] private float followingHeadwaySeconds =
            1.35f;
        [SerializeField, Min(0.1f)] private float movingVehiclePassDelaySeconds =
            4f;
        [SerializeField, Min(0.5f)] private float routeDeadlockSeconds = 3.75f;
        [SerializeField, Range(0f, 1.5f)] private float laneWanderAmplitudeMeters =
            0.55f;
        [SerializeField, Min(1f)] private float minimumTeimoStopSeconds = 18f;
        [SerializeField, Min(1f)] private float maximumTeimoStopSeconds = 32f;

        [Header("Donor-evidenced high-speed behavior")]
        [SerializeField, Min(1f)] private float minimumDriftSpeedMetersPerSecond =
            22f;
        [SerializeField, Range(1f, 60f)] private float driftEntryAngleDegrees =
            11f;
        [SerializeField, Range(0.1f, 30f)] private float driftExitAngleDegrees =
            4f;
        [SerializeField, Range(1f, 45f)] private float maximumDriftSlipDegrees =
            18f;
        [SerializeField, Min(0.1f)] private float minimumDriftSeconds = 0.35f;
        [SerializeField, Min(0.2f)] private float maximumDriftSeconds = 1.45f;
        [SerializeField, Min(0.1f)] private float crashSpeedThresholdMetersPerSecond =
            5f;
        [SerializeField, Min(0.1f)] private float crashHoldSeconds = 2.25f;
        [SerializeField, Min(0.1f)] private float recoveryDurationSeconds = 1.6f;
        [SerializeField, Min(0.1f)] private float recoverySpeedMetersPerSecond =
            4.5f;
        [SerializeField, Min(0.1f)] private float safePoseSpacingMeters = 4f;

        private Vector3 previousPosition;
        private bool hasPreviousPosition;
        private Vector3 routeTargetPosition;
        private Quaternion routeTargetRotation = Quaternion.identity;
        private Vector3 lastReceivedRoutePosition;
        private Vector3 lastQueuedRoutePosition;
        private Vector3 currentGroundNormal = Vector3.up;
        private bool hasRouteTarget;
        private bool hasLastReceivedRoutePosition;
        private bool hasLastQueuedRoutePosition;
        private float currentSpeedMetersPerSecond;
        private int groundLayerMask;
        private int obstacleLayerMask;
        private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        private readonly RaycastHit[] supportHits = new RaycastHit[8];
        private Rigidbody routeBody;
        private BoxCollider routeCollider;
        private IStoryTrafficVehicleMotionBackend motionBackend;
        private IStoryTrafficWheelPoseBackend wheelPoseBackend;
        private IStoryTrafficTerminalMotionControl terminalMotionControl;
        private IStoryTrafficHillDriveAssist hillDriveAssist;
        private StoryTrafficStructuralFailureSensor structuralFailureSensor;
        private int structuralSensorSuspensionFixedSteps;
        private float collisionEventCooldown;
        private float cruiseSpeedMetersPerSecond;
        private float currentLaneOffsetMeters;
        private float targetLaneOffsetMeters;
        private float activePassingLaneOffsetMeters;
        private float maneuverStateSeconds;
        private float driftSlipDegrees;
        private float lastImpactSpeedMetersPerSecond;
        private Vector3 lastImpactWorldPoint;
        private bool hasLastImpactWorldPoint;
        private bool hasSafePose;
        private Vector3 safePosition;
        private Quaternion safeRotation = Quaternion.identity;
        private int recoveryCount;
        private Collider trackedObstacle;
        private float behaviorClockSeconds;
        private float behaviorSeed01;
        private float routeDeadlockElapsedSeconds;
        private float catastrophicFallSeconds;
        private Vector3 reverseEscapeTarget;
        private bool hasReverseEscapeTarget;
        private float recoveryLaneOffsetMeters;
        private float recoverySideSign = 1f;
        private float socialStopSecondsRemaining;
        private float lastPhysicalRouteProgress01;
        private bool hasPhysicalRouteProgress;
        private bool storyIncidentHold;
        private bool terminalAbandonmentHold;
        private bool routeFullStopHold;
        private bool serviceStopHold;
        private bool automaticDriftEntryEnabled = true;
        private bool donorHandbrakeZoneActive;
        private bool donorHandbrakeZoneConsumed;
        private bool donorHandbrakeDriftActive;
        private float routeSpeedCapMetersPerSecond = float.PositiveInfinity;
        private bool routeRejoinActive;
        private bool passingSuppressed;
        private bool passingAttemptActive;
        private bool passingAttemptExpired;
        private float passingAttemptElapsedSeconds;
        private float passingRetryCooldownSeconds;
        private Vector3 passingAttemptStartPosition;
        private bool ignoreConfirmedSupportColliderInObstacleProbes;
        private StoryTrafficRoadBehaviorProfile roadBehaviorProfile =
            StoryTrafficRoadBehaviorProfile.RoadRace;
        private Quaternion[] wheelBaseLocalRotations =
            Array.Empty<Quaternion>();
        private Vector3[] wheelBaseLocalPositions =
            Array.Empty<Vector3>();
        private StoryTrafficManeuverState maneuverState =
            StoryTrafficManeuverState.Cruise;
        private readonly Queue<RoutePoseTarget> pendingRouteTargets =
            new Queue<RoutePoseTarget>(256);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float stationaryPhysicalSeconds;
        private float nextPhysicalStallDiagnosticTime;
#endif

        public string DriverFeatureId => driverFeatureId;
        public IReadOnlyList<string> PassengerFeatureIds =>
            passengerFeatureIds ?? Array.Empty<string>();
        public bool TryGetLowBeamMounts(
            out Vector3 leftWorldPosition,
            out Vector3 rightWorldPosition,
            out Vector3 worldDirection)
        {
            leftWorldPosition = transform.position;
            rightWorldPosition = transform.position;
            worldDirection = transform.forward;

            // The locked GAME scene contains explicit BeamShortAI transforms
            // for both story cars. Preserve those donor-local fascia anchors
            // exactly; their generated presentation intentionally excludes the
            // donor light components, but not the measured configuration.
            if (driverFeatureId == "P1.NPC.049")
            {
                leftWorldPosition = transform.TransformPoint(
                    new Vector3(-0.579979f, 0.21898432f, 1.8f));
                rightWorldPosition = transform.TransformPoint(
                    new Vector3(0.579979f, 0.21898432f, 1.8f));
                worldDirection = transform.TransformDirection(
                    Quaternion.Euler(3f, 0f, 0f) * Vector3.forward);
                return true;
            }

            if (driverFeatureId == "P1.NPC.050")
            {
                leftWorldPosition = transform.TransformPoint(
                    new Vector3(-0.53000474f, 0.16999996f, 1.92f));
                rightWorldPosition = transform.TransformPoint(
                    new Vector3(0.5300047f, 0.16999996f, 1.92f));
                worldDirection = transform.TransformDirection(
                    Quaternion.Euler(3f, 0f, 0f) * Vector3.forward);
                return true;
            }

            // Import order is the locked FL, FR, RL, RR donor order. These
            // physical wheel anchors survive every body-mesh/LOD variation and
            // are a much safer vehicle-space reference than renderer bounds,
            // which also include occupants, open doors and oversized skins.
            Transform[] wheels = wheelTransforms ?? Array.Empty<Transform>();
            if (wheels.Length != 4 || wheels.Any(wheel => wheel == null))
            {
                return false;
            }

            Vector3 up = transform.up.normalized;
            Vector3 frontAxle = (wheels[0].position + wheels[1].position) * 0.5f;
            Vector3 rearAxle = (wheels[2].position + wheels[3].position) * 0.5f;
            Vector3 forward = Vector3.ProjectOnPlane(
                frontAxle - rearAxle,
                up);
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, up);
            }

            forward.Normalize();
            if (Vector3.Dot(forward, transform.forward) < 0f)
            {
                forward = -forward;
            }

            Vector3 right = Vector3.ProjectOnPlane(
                wheels[0].position - wheels[1].position,
                up);
            right -= forward * Vector3.Dot(right, forward);
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.ProjectOnPlane(transform.right, up);
            }

            right.Normalize();
            if (Vector3.Dot(right, transform.right) < 0f)
            {
                right = -right;
            }

            float wheelbase = Mathf.Abs(Vector3.Dot(
                frontAxle - rearAxle,
                forward));
            float halfTrack = Mathf.Abs(Vector3.Dot(
                wheels[0].position - frontAxle,
                right));
            float wheelRadius = 360f /
                (2f * Mathf.PI * Mathf.Max(1f, wheelDegreesPerMeter));
            Vector3 lampCenter = frontAxle +
                forward * Mathf.Clamp(wheelbase * 0.27f, 0.45f, 0.85f) +
                up * Mathf.Clamp(wheelRadius * 0.68f, 0.16f, 0.3f);
            float lampHalfSpacing = Mathf.Clamp(
                halfTrack * 0.84f,
                0.28f,
                0.82f);
            leftWorldPosition = lampCenter - right * lampHalfSpacing;
            rightWorldPosition = lampCenter + right * lampHalfSpacing;

            // Ambient donor cars use the same three-degree low-beam pitch.
            worldDirection = Quaternion.AngleAxis(3f, right) * forward;
            return true;
        }
        public float GroundContactCalibrationMeters =>
            groundContactCalibrationMeters;
        public float CurrentSpeedMetersPerSecond =>
            currentSpeedMetersPerSecond;
        public Vector3 PhysicalWorldPosition => routeBody != null
            ? routeBody.position
            : transform.position;
        public Quaternion PhysicalWorldRotation => routeBody != null
            ? routeBody.rotation
            : transform.rotation;
        public bool IsObstacleBraking { get; private set; }
        public int PendingRouteSampleCount => pendingRouteTargets.Count;
        public float LastDesiredSpeedMetersPerSecond { get; private set; }
        public float LastPhysicalTargetDistanceMeters { get; private set; }
        public bool HasPhysicalGroundContact =>
            motionBackend != null && motionBackend.IsOperational &&
            motionBackend.HasGroundContact;
        public Vector3 PhysicalVelocityMetersPerSecond =>
            motionBackend != null && motionBackend.IsOperational
                ? motionBackend.VelocityMetersPerSecond
                : Vector3.zero;
        public string TrackedObstacleName => trackedObstacle != null
            ? trackedObstacle.name
            : string.Empty;
        public StoryTrafficManeuverState ManeuverState => maneuverState;
        public float CurrentLaneOffsetMeters => currentLaneOffsetMeters;
        public float BaseLaneOffsetMeters => baseLaneOffsetMeters;
        public float CruiseSpeedMetersPerSecond =>
            cruiseSpeedMetersPerSecond;
        public bool IsReversing =>
            maneuverState == StoryTrafficManeuverState.Reversing;
        public bool IsDrifting =>
            maneuverState == StoryTrafficManeuverState.Drifting;
        public bool HandbrakeActive =>
            IsDrifting && maneuverStateSeconds <= PhysicalHandbrakePulseSeconds;
        public float Drift01 => Mathf.Clamp01(
            Mathf.Abs(driftSlipDegrees) /
            Mathf.Max(1f, maximumDriftSlipDegrees));
        public bool IsCrashed =>
            maneuverState == StoryTrafficManeuverState.Crashed;
        public float LastImpactSpeedMetersPerSecond =>
            lastImpactSpeedMetersPerSecond;
        public int RecoveryCount => recoveryCount;
        public bool HasPhysicalMotionBackend =>
            motionBackend != null && motionBackend.IsOperational;
        public float PhysicalEngineRpm => HasPhysicalMotionBackend
            ? motionBackend.EngineRpm
            : 0f;
        public float PhysicalEngineRedlineRpm => HasPhysicalMotionBackend
            ? motionBackend.EngineRedlineRpm
            : 0f;
        public float PhysicalEngineLoad01 => HasPhysicalMotionBackend
            ? motionBackend.EngineLoad01
            : 0f;
        public int PhysicalSelectedGear => HasPhysicalMotionBackend
            ? motionBackend.SelectedGear
            : 0;
        public bool IsAtTeimoSocialStop => socialStopSecondsRemaining > 0f;
        public float SocialStopSecondsRemaining =>
            socialStopSecondsRemaining;
        public bool IsStoryIncidentHeld => storyIncidentHold;
        public bool IsTerminalAbandonmentHeld => terminalAbandonmentHold;
        public bool IsRouteFullStopHeld => routeFullStopHold;
        public bool HasIntentionalStationaryHold =>
            socialStopSecondsRemaining > 0f ||
            storyIncidentHold || routeFullStopHold ||
            terminalAbandonmentHold || serviceStopHold;
        public bool HasNonServiceIntentionalStationaryHold =>
            socialStopSecondsRemaining > 0f ||
            storyIncidentHold || routeFullStopHold ||
            terminalAbandonmentHold;
        public StoryTrafficInCarRagdollBinding InCarRagdollBinding =>
            inCarRagdollBinding;
        public StoryTrafficRoadBehaviorProfile RoadBehaviorProfile =>
            roadBehaviorProfile;
        public bool IsInsideDonorHandbrakeZone =>
            donorHandbrakeZoneActive;
        public bool IsRouteRejoinActive => routeRejoinActive;
        public bool IsHillDriveAssistActive =>
            hillDriveAssist?.IsHillDriveAssistActive ?? false;

        /// <summary>
        /// Ignores an obstacle-probe hit only when the exact same collider is
        /// also confirmed by a downward chassis-support query this frame. This
        /// is an opt-in compatibility policy for donor routes whose continuous
        /// terrain mesh exposes steep edge triangles to a forward sphere cast;
        /// it deliberately does not ignore the WorldSurface layer as a whole.
        /// </summary>
        public void SetConfirmedSupportColliderObstacleRejection(bool active)
        {
            ignoreConfirmedSupportColliderInObstacleProbes = active;
        }

        public event Action<StoryTrafficCollisionEvent> CollisionIncident;
        public event Action<StoryTrafficTerminalCrashEvent> TerminalCrash;

        /// <summary>
        /// Keeps a donor-evidenced terminal story crash physically at its
        /// incident pose. Route simulation may continue to exist for save and
        /// streaming purposes, but the vehicle backend remains fully braked
        /// until the project-owned story state explicitly releases it.
        /// </summary>
        public void SetStoryIncidentHold(bool held)
        {
            storyIncidentHold = held;
            terminalMotionControl?.SetTerminallyDisabled(
                held || terminalAbandonmentHold || routeFullStopHold);
            inCarRagdollBinding?.SetTerminalCrashActive(held);
            if (structuralFailureSensor != null)
            {
                structuralFailureSensor.SetSuspended(
                    held || structuralSensorSuspensionFixedSteps > 0);
            }
            if (!held)
            {
                return;
            }

            currentSpeedMetersPerSecond = 0f;
            IsObstacleBraking = true;
            if (maneuverState != StoryTrafficManeuverState.Crashed)
            {
                EnterManeuver(StoryTrafficManeuverState.Crashed);
            }

            if (motionBackend != null && motionBackend.IsOperational)
            {
                motionBackend.Step(
                    Mathf.Max(Time.fixedDeltaTime, 0.001f),
                    StoryTrafficVehicleDriveCommand.Stop(
                        PhysicalWorldPosition,
                        PhysicalWorldRotation * Vector3.forward));
            }
        }

        /// <summary>
        /// Applies the donor bus DrivingIssue/QUIT terminal stop without
        /// classifying the vehicle as a crash or activating occupant ragdolls.
        /// Unlike a timetable dwell this turns the physical backend off, which
        /// leaves the drivetrain neutral, ignition silent and brakes held.
        /// </summary>
        public void SetTerminalAbandonmentHold(bool held)
        {
            terminalAbandonmentHold = held;
            terminalMotionControl?.SetTerminallyDisabled(
                storyIncidentHold || terminalAbandonmentHold ||
                routeFullStopHold);
            if (!held)
            {
                return;
            }

            currentSpeedMetersPerSecond = 0f;
            IsObstacleBraking = true;
            if (motionBackend != null && motionBackend.IsOperational)
            {
                motionBackend.Step(
                    Mathf.Max(Time.fixedDeltaTime, 0.001f),
                    StoryTrafficVehicleDriveCommand.Stop(
                        PhysicalWorldPosition,
                        PhysicalWorldRotation * Vector3.forward));
            }
        }

        /// <summary>
        /// Holds the donor Navigation/FULLSTOP terminal formation without
        /// classifying either racer as crashed. The route remains project-owned
        /// and persisted, while drivetrain, reverse recovery and passing are
        /// disabled idempotently at the separated authored stop slots.
        /// </summary>
        public void SetRouteFullStopHold(bool held)
        {
            routeFullStopHold = held;
            terminalMotionControl?.SetTerminallyDisabled(
                storyIncidentHold || terminalAbandonmentHold ||
                routeFullStopHold);
            if (!held)
            {
                return;
            }

            CancelPassingAttempt(PassingRetryCooldownSeconds);
            currentSpeedMetersPerSecond = 0f;
            IsObstacleBraking = true;
            trackedObstacle = null;
            hasReverseEscapeTarget = false;
            driftSlipDegrees = 0f;
            if (maneuverState != StoryTrafficManeuverState.Cruise)
            {
                EnterManeuver(StoryTrafficManeuverState.Cruise);
            }

            if (motionBackend != null && motionBackend.IsOperational)
            {
                motionBackend.Step(
                    Mathf.Max(Time.fixedDeltaTime, 0.001f),
                    StoryTrafficVehicleDriveCommand.Stop(
                        PhysicalWorldPosition,
                        PhysicalWorldRotation * Vector3.forward));
            }
        }

        /// <summary>
        /// Starts or restores the requested short, one-shot Teimo social dwell.
        /// The composition root owns whether the event was consumed; this
        /// presenter only owns the real-time physical hold.
        /// </summary>
        public void BeginTeimoSocialStop(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 0f || seconds > 8f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    "Teimo social dwell must be between zero and eight real seconds.");
            }

            socialStopSecondsRemaining = Mathf.Max(
                socialStopSecondsRemaining,
                seconds);
            if (seconds <= 0f)
            {
                return;
            }

            CancelPassingAttempt(PassingRetryCooldownSeconds);
            trackedObstacle = null;
            hasReverseEscapeTarget = false;
            driftSlipDegrees = 0f;
            if (maneuverState != StoryTrafficManeuverState.Cruise)
            {
                EnterManeuver(StoryTrafficManeuverState.Cruise);
            }
        }

        /// <summary>
        /// Disallows an opposing-lane manoeuvre inside progress-directed route
        /// corridors, route rejoin, dwell and terminal states. A pass already
        /// in progress is returned to the authored lane instead of being left
        /// indefinitely oncoming.
        /// </summary>
        public void SetPassingSuppressed(bool suppressed)
        {
            passingSuppressed = suppressed;
            if (!suppressed)
            {
                return;
            }

            if (maneuverState == StoryTrafficManeuverState.PassingOut ||
                maneuverState == StoryTrafficManeuverState.Passing)
            {
                passingAttemptExpired = true;
                passingRetryCooldownSeconds = Mathf.Max(
                    passingRetryCooldownSeconds,
                    PassingRetryCooldownSeconds);
                EnterManeuver(StoryTrafficManeuverState.Returning);
            }
        }

        /// <summary>
        /// Brakes a route vehicle for a scheduled service dwell without
        /// classifying the stop as a crash or changing persistent incident
        /// state. Used by the project-owned public-bus timetable.
        /// </summary>
        public void SetServiceStopHold(bool held)
        {
            serviceStopHold = held;
            if (!held || motionBackend == null || !motionBackend.IsOperational)
            {
                return;
            }

            currentSpeedMetersPerSecond = 0f;
            IsObstacleBraking = true;
            motionBackend.Step(
                Mathf.Max(Time.fixedDeltaTime, 0.001f),
                StoryTrafficVehicleDriveCommand.Stop(
                    PhysicalWorldPosition,
                    PhysicalWorldRotation * Vector3.forward));
        }

        /// <summary>
        /// Enables a backend-owned heavy-vehicle launch/downshift policy on a
        /// route-authored steep corridor. Ordinary traffic backends may omit
        /// the optional capability.
        /// </summary>
        public void SetHillDriveAssist(bool active)
        {
            hillDriveAssist?.SetHillDriveAssist(active);
        }

        public void ConfigureDrivingProfile(
            float configuredMinimumCruiseSpeedMetersPerSecond,
            float configuredMaximumSpeedMetersPerSecond,
            float configuredAccelerationMetersPerSecond2,
            float configuredBrakingMetersPerSecond2,
            float configuredTurnRateDegreesPerSecond,
            float configuredPassingLaneOffsetMeters)
        {
            if (!float.IsFinite(
                    configuredMinimumCruiseSpeedMetersPerSecond) ||
                configuredMinimumCruiseSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(configuredMaximumSpeedMetersPerSecond) ||
                configuredMaximumSpeedMetersPerSecond <
                configuredMinimumCruiseSpeedMetersPerSecond ||
                !float.IsFinite(configuredAccelerationMetersPerSecond2) ||
                configuredAccelerationMetersPerSecond2 <= 0f ||
                !float.IsFinite(configuredBrakingMetersPerSecond2) ||
                configuredBrakingMetersPerSecond2 <= 0f ||
                !float.IsFinite(configuredTurnRateDegreesPerSecond) ||
                configuredTurnRateDegreesPerSecond <= 0f ||
                !float.IsFinite(configuredPassingLaneOffsetMeters) ||
                Mathf.Abs(configuredPassingLaneOffsetMeters) < 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredMinimumCruiseSpeedMetersPerSecond),
                    "Story-traffic driving profile values are invalid.");
            }

            minimumCruiseSpeedMetersPerSecond =
                configuredMinimumCruiseSpeedMetersPerSecond;
            maximumSpeedMetersPerSecond =
                configuredMaximumSpeedMetersPerSecond;
            accelerationMetersPerSecond2 =
                configuredAccelerationMetersPerSecond2;
            brakingMetersPerSecond2 =
                configuredBrakingMetersPerSecond2;
            turnRateDegreesPerSecond =
                configuredTurnRateDegreesPerSecond;
            passingLaneOffsetMeters = configuredPassingLaneOffsetMeters;
            cruiseSpeedMetersPerSecond = ResolveInitialCruiseSpeed();
        }

        /// <summary>
        /// Configures an explicit right-hand traffic lane and its opposing
        /// passing lane. TrafficCarExpansion is used only as behavioral
        /// evidence for this lane contract; route ownership and driving code
        /// remain project-owned.
        /// </summary>
        public void ConfigureRoadLanePolicy(
            float configuredBaseLaneOffsetMeters,
            float configuredPassingLaneOffsetMeters,
            bool migrateLegacyCenterLane)
        {
            if (!float.IsFinite(configuredBaseLaneOffsetMeters) ||
                Mathf.Abs(configuredBaseLaneOffsetMeters) > 4f ||
                !float.IsFinite(configuredPassingLaneOffsetMeters) ||
                Mathf.Abs(configuredPassingLaneOffsetMeters) > 4f ||
                Mathf.Abs(configuredPassingLaneOffsetMeters -
                          configuredBaseLaneOffsetMeters) < 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredBaseLaneOffsetMeters),
                    "Road-traffic lane offsets are invalid.");
            }

            float previousBase = baseLaneOffsetMeters;
            baseLaneOffsetMeters = configuredBaseLaneOffsetMeters;
            passingLaneOffsetMeters = configuredPassingLaneOffsetMeters;
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
            if (migrateLegacyCenterLane &&
                maneuverState == StoryTrafficManeuverState.Cruise &&
                Mathf.Abs(currentLaneOffsetMeters - previousBase) <= 0.2f)
            {
                currentLaneOffsetMeters = baseLaneOffsetMeters;
                targetLaneOffsetMeters = baseLaneOffsetMeters;
            }
        }

        /// <summary>
        /// Re-establishes the authored travel lane after a streamed pose or a
        /// route-recovery projection is restored. The Rigidbody is not
        /// teleported: only the steering state is reset, so a vehicle whose
        /// body is laterally displaced drives back onto its own lane.
        /// </summary>
        public void ResetRoadLaneStateToBase()
        {
            currentLaneOffsetMeters = baseLaneOffsetMeters;
            targetLaneOffsetMeters = baseLaneOffsetMeters;
            recoveryLaneOffsetMeters = baseLaneOffsetMeters;
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
            trackedObstacle = null;
            hasReverseEscapeTarget = false;
            routeDeadlockElapsedSeconds = 0f;
            driftSlipDegrees = 0f;
            if (!storyIncidentHold &&
                maneuverState != StoryTrafficManeuverState.Crashed)
            {
                EnterManeuver(StoryTrafficManeuverState.Cruise);
            }
        }

        public void ConfigureHooliganBehavior(
            float configuredLaneWanderAmplitudeMeters,
            float configuredMinimumDriftSpeedMetersPerSecond,
            float configuredDriftEntryAngleDegrees,
            float configuredDriftExitAngleDegrees,
            float configuredMaximumDriftSlipDegrees)
        {
            if (!float.IsFinite(configuredLaneWanderAmplitudeMeters) ||
                configuredLaneWanderAmplitudeMeters < 0f ||
                !float.IsFinite(configuredMinimumDriftSpeedMetersPerSecond) ||
                configuredMinimumDriftSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(configuredDriftEntryAngleDegrees) ||
                configuredDriftEntryAngleDegrees <= 0f ||
                !float.IsFinite(configuredDriftExitAngleDegrees) ||
                configuredDriftExitAngleDegrees <= 0f ||
                configuredDriftExitAngleDegrees >= configuredDriftEntryAngleDegrees ||
                !float.IsFinite(configuredMaximumDriftSlipDegrees) ||
                configuredMaximumDriftSlipDegrees <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredLaneWanderAmplitudeMeters),
                    "Story-traffic hooligan behavior values are invalid.");
            }

            laneWanderAmplitudeMeters = configuredLaneWanderAmplitudeMeters;
            minimumDriftSpeedMetersPerSecond =
                configuredMinimumDriftSpeedMetersPerSecond;
            driftEntryAngleDegrees = configuredDriftEntryAngleDegrees;
            driftExitAngleDegrees = configuredDriftExitAngleDegrees;
            maximumDriftSlipDegrees = configuredMaximumDriftSlipDegrees;
        }

        /// <summary>
        /// Applies the donor route's distinct road behavior without replacing
        /// physical steering authority. The original Navigation FSM requests
        /// HANDBRAKE only near four authored Perajarvi points; RoadRace keeps
        /// the full 115-185 km/h throttle range, while unpaved field travel is
        /// deliberately calmer.
        /// </summary>
        public void SetRoadBehaviorProfile(
            StoryTrafficRoadBehaviorProfile profile,
            bool insideDonorHandbrakeZone)
        {
            if (!insideDonorHandbrakeZone)
            {
                donorHandbrakeZoneConsumed = false;
            }

            donorHandbrakeZoneActive = insideDonorHandbrakeZone;
            if (roadBehaviorProfile == profile)
            {
                return;
            }

            roadBehaviorProfile = profile;
            switch (profile)
            {
                case StoryTrafficRoadBehaviorProfile.Perajarvi:
                    minimumCruiseSpeedMetersPerSecond = 55f / 3.6f;
                    maximumSpeedMetersPerSecond = 90f / 3.6f;
                    laneWanderAmplitudeMeters = 0.24f;
                    minimumDriftSpeedMetersPerSecond = 34f / 3.6f;
                    driftEntryAngleDegrees = 5f;
                    driftExitAngleDegrees = 2.5f;
                    maximumDriftSlipDegrees = 10f;
                    automaticDriftEntryEnabled = false;
                    break;

                case StoryTrafficRoadBehaviorProfile.RoadRace:
                    minimumCruiseSpeedMetersPerSecond = 115f / 3.6f;
                    maximumSpeedMetersPerSecond = 185f / 3.6f;
                    laneWanderAmplitudeMeters = 0.55f;
                    minimumDriftSpeedMetersPerSecond = 18f;
                    driftEntryAngleDegrees = 7.5f;
                    driftExitAngleDegrees = 4f;
                    maximumDriftSlipDegrees = 22f;
                    automaticDriftEntryEnabled = true;
                    break;

                case StoryTrafficRoadBehaviorProfile.Gravel:
                    minimumCruiseSpeedMetersPerSecond = 55f / 3.6f;
                    maximumSpeedMetersPerSecond = 95f / 3.6f;
                    laneWanderAmplitudeMeters = 0.16f;
                    minimumDriftSpeedMetersPerSecond = 1000f;
                    driftEntryAngleDegrees = 20f;
                    driftExitAngleDegrees = 5f;
                    maximumDriftSlipDegrees = 7f;
                    automaticDriftEntryEnabled = false;
                    break;

                case StoryTrafficRoadBehaviorProfile.Bus:
                    // Locked BUS/Throttle is 80..85 km/h. The transport
                    // definition intentionally owns the 82.5 km/h midpoint
                    // until the donor randomizer is modelled. Unlike compact
                    // cars, the 9.8-tonne long chassis may still fall below
                    // that target through the route-level tight-bend cap.
                    minimumCruiseSpeedMetersPerSecond = 82.5f / 3.6f;
                    maximumSpeedMetersPerSecond = 82.5f / 3.6f;
                    laneWanderAmplitudeMeters = 0f;
                    minimumDriftSpeedMetersPerSecond = 1000f;
                    driftEntryAngleDegrees = 20f;
                    driftExitAngleDegrees = 5f;
                    maximumDriftSlipDegrees = 7f;
                    automaticDriftEntryEnabled = false;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }

            cruiseSpeedMetersPerSecond = Mathf.Clamp(
                cruiseSpeedMetersPerSecond,
                minimumCruiseSpeedMetersPerSecond,
                maximumSpeedMetersPerSecond);
        }

        /// <summary>
        /// Applies a route-level anticipatory ceiling without changing the
        /// current road profile. This lets a RoadRace car brake before the
        /// profile boundary instead of discovering the Perajarvi/Trackfield
        /// return bend only after crossing it.
        /// </summary>
        public void SetRouteSpeedCap(float maximumMetersPerSecond)
        {
            if (float.IsPositiveInfinity(maximumMetersPerSecond))
            {
                routeSpeedCapMetersPerSecond = float.PositiveInfinity;
                return;
            }

            if (!float.IsFinite(maximumMetersPerSecond) ||
                maximumMetersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMetersPerSecond),
                    "Route speed cap must be positive or +infinity.");
            }

            routeSpeedCapMetersPerSecond = maximumMetersPerSecond;
        }

        /// <summary>
        /// Marks guidance as an explicit return to the retained route branch.
        /// Passing and drift are suppressed until the chassis is back inside
        /// the accepted corridor; obstacle recovery remains fully physical.
        /// </summary>
        public void SetRouteRejoinActive(bool active)
        {
            if (routeRejoinActive == active)
            {
                return;
            }

            routeRejoinActive = active;
            if (!active)
            {
                return;
            }

            driftSlipDegrees = 0f;
            donorHandbrakeDriftActive = false;
            trackedObstacle = null;
            passingAttemptExpired = true;
            passingRetryCooldownSeconds = Mathf.Max(
                passingRetryCooldownSeconds,
                PassingRetryCooldownSeconds);
            switch (maneuverState)
            {
                case StoryTrafficManeuverState.PassingOut:
                case StoryTrafficManeuverState.Passing:
                case StoryTrafficManeuverState.Returning:
                case StoryTrafficManeuverState.Drifting:
                    EnterManeuver(StoryTrafficManeuverState.Returning);
                    break;
            }
        }

        public StoryTrafficMotionRuntimeState CaptureRuntimeState() =>
            new StoryTrafficMotionRuntimeState(
                currentSpeedMetersPerSecond,
                cruiseSpeedMetersPerSecond,
                currentLaneOffsetMeters,
                maneuverState,
                maneuverStateSeconds,
                driftSlipDegrees,
                recoveryCount,
                hasSafePose,
                safePosition,
                safeRotation,
                socialStopSecondsRemaining,
                lastPhysicalRouteProgress01,
                hasPhysicalRouteProgress,
                storyIncidentHold);

        public void RestoreRuntimeState(
            in StoryTrafficMotionRuntimeState state)
        {
            currentSpeedMetersPerSecond = state.SpeedMetersPerSecond;
            if (state.CruiseSpeedMetersPerSecond > 0f)
            {
                cruiseSpeedMetersPerSecond = Mathf.Clamp(
                    state.CruiseSpeedMetersPerSecond,
                    minimumCruiseSpeedMetersPerSecond,
                    maximumSpeedMetersPerSecond);
            }

            currentLaneOffsetMeters = state.LaneOffsetMeters;
            targetLaneOffsetMeters = state.LaneOffsetMeters;
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
            maneuverState = Enum.IsDefined(
                typeof(StoryTrafficManeuverState),
                state.ManeuverState)
                    ? state.ManeuverState
                    : StoryTrafficManeuverState.Cruise;
            // A streamed/save boundary must never restore a half-completed
            // opposing-lane pass without its obstacle and elapsed budget.
            // Normalize the old transient state into a bounded return.
            if (maneuverState == StoryTrafficManeuverState.PassingOut ||
                maneuverState == StoryTrafficManeuverState.Passing ||
                maneuverState == StoryTrafficManeuverState.Returning)
            {
                maneuverState = StoryTrafficManeuverState.Returning;
                passingAttemptExpired = true;
                passingRetryCooldownSeconds = PassingRetryCooldownSeconds;
            }
            maneuverStateSeconds = state.ManeuverStateSeconds;
            driftSlipDegrees = Mathf.Clamp(
                state.DriftSlipDegrees,
                -maximumDriftSlipDegrees,
                maximumDriftSlipDegrees);
            recoveryCount = state.RecoveryCount;
            hasSafePose = state.HasSafePose &&
                          IsFinite(state.SafePosition) &&
                          IsFinite(state.SafeRotation) &&
                          IsRetainedSafePosePlausible(
                              state.SafePosition,
                              PhysicalWorldPosition);
            safePosition = hasSafePose
                ? state.SafePosition
                : PhysicalWorldPosition;
            safeRotation = hasSafePose
                ? state.SafeRotation
                : PhysicalWorldRotation;
            trackedObstacle = null;
            routeDeadlockElapsedSeconds = 0f;
            catastrophicFallSeconds = 0f;
            hasReverseEscapeTarget = false;
            recoveryLaneOffsetMeters = baseLaneOffsetMeters;
            socialStopSecondsRemaining = state.SocialStopSecondsRemaining;
            lastPhysicalRouteProgress01 =
                state.LastPhysicalRouteProgress01;
            hasPhysicalRouteProgress = state.HasPhysicalRouteProgress;
            storyIncidentHold = state.StoryIncidentHold;
            terminalMotionControl?.SetTerminallyDisabled(
                storyIncidentHold || terminalAbandonmentHold ||
                routeFullStopHold);
            inCarRagdollBinding?.SetTerminalCrashActive(
                storyIncidentHold);
            if (storyIncidentHold && structuralFailureSensor != null)
            {
                structuralFailureSensor.SetSuspended(true);
            }
            if (motionBackend != null && motionBackend.IsOperational)
            {
                // SnapToRoutePoseTarget writes the Rigidbody immediately, while an
                // interpolated Transform can still expose the prefab's pre-snap pose
                // until the next physics step.  Restoring from that stale Transform
                // used to push newly materialized traffic back below the road.
                Vector3 physicalPosition = routeBody != null
                    ? routeBody.position
                    : transform.position;
                Quaternion physicalRotation = routeBody != null
                    ? routeBody.rotation
                    : transform.rotation;
                motionBackend.SnapToPose(
                    physicalPosition,
                    physicalRotation,
                    currentSpeedMetersPerSecond);
            }
        }

        public bool TryValidate(out string failure)
        {
            if (motionBackendComponent != null &&
                motionBackendComponent is not IStoryTrafficVehicleMotionBackend)
            {
                failure =
                    "Assigned story-traffic motion backend does not implement the required project boundary.";
                return false;
            }

            if (inCarRagdollBinding != null &&
                (!inCarRagdollBinding.TryValidate(out failure) ||
                 (inCarRagdollBinding.transform != transform &&
                  !inCarRagdollBinding.transform.IsChildOf(transform))))
            {
                failure = string.IsNullOrWhiteSpace(failure)
                    ? "Assigned in-car ragdoll does not belong to this story-traffic wrapper."
                    : failure;
                return false;
            }

            if (string.IsNullOrWhiteSpace(driverFeatureId) ||
                !driverFeatureId.StartsWith("P1.NPC.", StringComparison.Ordinal))
            {
                failure = "Story-traffic car has no valid driver FeatureId.";
                return false;
            }

            string[] passengers = passengerFeatureIds ?? Array.Empty<string>();
            GameObject[] passengerRoots = passengerPresentationRoots ??
                Array.Empty<GameObject>();
            if (passengers.Any(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    !value.StartsWith("P1.NPC.", StringComparison.Ordinal)) ||
                passengers.Distinct(StringComparer.Ordinal).Count() !=
                passengers.Length)
            {
                failure =
                    $"Story-traffic car '{driverFeatureId}' has invalid passenger identity.";
                return false;
            }

            if (passengerRoots.Length != passengers.Length ||
                passengerRoots.Any(root => root == null) ||
                passengerRoots.Distinct().Count() != passengerRoots.Length)
            {
                failure =
                    $"Story-traffic car '{driverFeatureId}' passenger identity/presentation counts differ.";
                return false;
            }

            Transform[] wheels = wheelTransforms ?? Array.Empty<Transform>();
            if (wheels.Length != 4 ||
                wheels.Any(wheel => wheel == null) ||
                wheels.Distinct().Count() != wheels.Length ||
                !float.IsFinite(wheelDegreesPerMeter) ||
                wheelDegreesPerMeter <= 0f ||
                !float.IsFinite(groundContactCalibrationMeters) ||
                Mathf.Abs(groundContactCalibrationMeters) > 1f ||
                !float.IsFinite(minimumCruiseSpeedMetersPerSecond) ||
                minimumCruiseSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(maximumSpeedMetersPerSecond) ||
                maximumSpeedMetersPerSecond <
                minimumCruiseSpeedMetersPerSecond ||
                !float.IsFinite(accelerationMetersPerSecond2) ||
                accelerationMetersPerSecond2 <= 0f ||
                !float.IsFinite(brakingMetersPerSecond2) ||
                brakingMetersPerSecond2 <= 0f ||
                !float.IsFinite(turnRateDegreesPerSecond) ||
                turnRateDegreesPerSecond <= 0f ||
                !float.IsFinite(teleportDistanceMeters) ||
                teleportDistanceMeters <= 1f ||
                !float.IsFinite(obstacleProbeDistanceMeters) ||
                obstacleProbeDistanceMeters <= 0f ||
                !float.IsFinite(obstacleProbeRadiusMeters) ||
                obstacleProbeRadiusMeters <= 0f ||
                !float.IsFinite(passingLaneOffsetMeters) ||
                Mathf.Abs(passingLaneOffsetMeters) < 1f ||
                !float.IsFinite(baseLaneOffsetMeters) ||
                Mathf.Abs(baseLaneOffsetMeters) > 4f ||
                !float.IsFinite(laneChangeMetersPerSecond) ||
                laneChangeMetersPerSecond <= 0f ||
                !float.IsFinite(passingClearanceMeters) ||
                passingClearanceMeters <= 0f ||
                !float.IsFinite(blockedBeforeReverseSeconds) ||
                blockedBeforeReverseSeconds <= 0f ||
                !float.IsFinite(reverseDurationSeconds) ||
                reverseDurationSeconds <= 0f ||
                !float.IsFinite(reverseSpeedMetersPerSecond) ||
                reverseSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(minimumFollowingGapMeters) ||
                minimumFollowingGapMeters <= 0f ||
                !float.IsFinite(followingHeadwaySeconds) ||
                followingHeadwaySeconds <= 0f ||
                !float.IsFinite(movingVehiclePassDelaySeconds) ||
                movingVehiclePassDelaySeconds <= 0f ||
                !float.IsFinite(routeDeadlockSeconds) ||
                routeDeadlockSeconds <= 0f ||
                !float.IsFinite(minimumTeimoStopSeconds) ||
                minimumTeimoStopSeconds <= 0f ||
                !float.IsFinite(maximumTeimoStopSeconds) ||
                maximumTeimoStopSeconds < minimumTeimoStopSeconds ||
                !float.IsFinite(minimumDriftSpeedMetersPerSecond) ||
                minimumDriftSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(driftEntryAngleDegrees) ||
                driftEntryAngleDegrees <= 0f ||
                !float.IsFinite(driftExitAngleDegrees) ||
                driftExitAngleDegrees <= 0f ||
                driftExitAngleDegrees >= driftEntryAngleDegrees ||
                !float.IsFinite(maximumDriftSlipDegrees) ||
                maximumDriftSlipDegrees <= 0f ||
                !float.IsFinite(minimumDriftSeconds) ||
                minimumDriftSeconds <= 0f ||
                !float.IsFinite(maximumDriftSeconds) ||
                maximumDriftSeconds < minimumDriftSeconds ||
                !float.IsFinite(crashSpeedThresholdMetersPerSecond) ||
                crashSpeedThresholdMetersPerSecond <= 0f ||
                !float.IsFinite(crashHoldSeconds) ||
                crashHoldSeconds <= 0f ||
                !float.IsFinite(recoveryDurationSeconds) ||
                recoveryDurationSeconds <= 0f ||
                !float.IsFinite(recoverySpeedMetersPerSecond) ||
                recoverySpeedMetersPerSecond <= 0f ||
                !float.IsFinite(safePoseSpacingMeters) ||
                safePoseSpacingMeters <= 0f)
            {
                failure =
                    $"Story-traffic car '{driverFeatureId}' requires four distinct wheel transforms, positive wheel motion and a bounded ground-contact calibration.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool SetPassengerVisible(string featureId, bool visible)
        {
            int index = Array.IndexOf(
                passengerFeatureIds ?? Array.Empty<string>(),
                featureId ?? string.Empty);
            if (index < 0 || passengerPresentationRoots == null ||
                index >= passengerPresentationRoots.Length ||
                passengerPresentationRoots[index] == null)
            {
                return false;
            }

            passengerPresentationRoots[index].SetActive(visible);
            return true;
        }

        public bool TryGetPassengerVisibility(
            string featureId,
            out bool visible)
        {
            int index = Array.IndexOf(
                passengerFeatureIds ?? Array.Empty<string>(),
                featureId ?? string.Empty);
            if (index < 0 || passengerPresentationRoots == null ||
                index >= passengerPresentationRoots.Length ||
                passengerPresentationRoots[index] == null)
            {
                visible = false;
                return false;
            }

            visible = passengerPresentationRoots[index].activeSelf;
            return true;
        }

        public bool TryGetPassengerWorldPose(
            string featureId,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            int index = Array.IndexOf(
                passengerFeatureIds ?? Array.Empty<string>(),
                featureId ?? string.Empty);
            if (index < 0 || passengerPresentationRoots == null ||
                index >= passengerPresentationRoots.Length ||
                passengerPresentationRoots[index] == null)
            {
                worldPosition = default;
                worldRotation = Quaternion.identity;
                return false;
            }

            if (inCarRagdollBinding != null &&
                inCarRagdollBinding.IsTerminalCrashActive &&
                inCarRagdollBinding.TryGetPrimaryBody(
                    featureId,
                    out Rigidbody passengerBody))
            {
                worldPosition = passengerBody.position;
                worldRotation = passengerBody.rotation;
                return true;
            }

            Transform passenger = passengerPresentationRoots[index].transform;
            worldPosition = passenger.position;
            worldRotation = passenger.rotation;
            return true;
        }

        /// <summary>
        /// Installs the donor-evidenced Jani DeathForce proof mass on this
        /// project-owned wrapper. The NPC composition root decides which
        /// driver owns this specialized terminal-crash contract.
        /// </summary>
        public void ConfigureStructuralFailureSensor(Transform player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            EnsureRoutePhysics();
            if (routeBody == null)
            {
                throw new InvalidOperationException(
                    "Structural failure sensing requires a chassis Rigidbody.");
            }

            // A restored terminal wreck must not receive a fresh proof mass.
            // This also prevents a synchronous reconciliation triggered from
            // OnJointBreak from trying to configure the just-broken joint
            // again before Unity destroys its component at frame end.
            if (storyIncidentHold)
            {
                structuralFailureSensor?.SetSuspended(true);
                return;
            }

            StoryTrafficStructuralFailureSensor sensor =
                GetComponentInChildren<
                    StoryTrafficStructuralFailureSensor>(true);
            if (sensor == null)
            {
                var proofMassObject = new GameObject(
                    "StoryTraffic_DeathForce");
                proofMassObject.transform.SetParent(transform, false);
                proofMassObject.transform.localPosition = new Vector3(
                    0.0000383f,
                    0.629999f,
                    -0.0290164f);
                proofMassObject.AddComponent<Rigidbody>();
                proofMassObject.AddComponent<FixedJoint>();
                sensor = proofMassObject.AddComponent<
                    StoryTrafficStructuralFailureSensor>();
            }

            if (structuralFailureSensor != null)
            {
                structuralFailureSensor.StructuralFailure -=
                    HandleStructuralFailure;
            }

            structuralFailureSensor = sensor;
            structuralFailureSensor.Configure(routeBody, player);
            structuralFailureSensor.StructuralFailure +=
                HandleStructuralFailure;
            structuralFailureSensor.SetSuspended(
                storyIncidentHold ||
                structuralSensorSuspensionFixedSteps > 0);
        }

        private void OnEnable()
        {
            ResolveMotionBackend();
            previousPosition = transform.position;
            hasPreviousPosition = false;
            hasRouteTarget = false;
            hasLastReceivedRoutePosition = false;
            hasLastQueuedRoutePosition = false;
            pendingRouteTargets.Clear();
            currentGroundNormal = Vector3.up;
            currentSpeedMetersPerSecond = 0f;
            IsObstacleBraking = false;
            collisionEventCooldown = 0f;
            currentLaneOffsetMeters = baseLaneOffsetMeters;
            targetLaneOffsetMeters = baseLaneOffsetMeters;
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
            maneuverStateSeconds = 0f;
            driftSlipDegrees = 0f;
            donorHandbrakeDriftActive = false;
            lastImpactSpeedMetersPerSecond = 0f;
            lastImpactWorldPoint = transform.position;
            hasLastImpactWorldPoint = false;
            hasSafePose = false;
            safePosition = transform.position;
            safeRotation = transform.rotation;
            recoveryCount = 0;
            maneuverState = StoryTrafficManeuverState.Cruise;
            trackedObstacle = null;
            cruiseSpeedMetersPerSecond = ResolveInitialCruiseSpeed();
            behaviorSeed01 = ResolveIdentity01();
            behaviorClockSeconds = behaviorSeed01 * 37f;
            routeDeadlockElapsedSeconds = 0f;
            catastrophicFallSeconds = 0f;
            hasReverseEscapeTarget = false;
            recoveryLaneOffsetMeters = baseLaneOffsetMeters;
            recoverySideSign = behaviorSeed01 >= 0.5f ? 1f : -1f;
            socialStopSecondsRemaining = 0f;
            passingSuppressed = false;
            passingAttemptActive = false;
            passingAttemptExpired = false;
            passingAttemptElapsedSeconds = 0f;
            passingRetryCooldownSeconds = 0f;
            passingAttemptStartPosition = transform.position;
            routeSpeedCapMetersPerSecond = float.PositiveInfinity;
            lastPhysicalRouteProgress01 = 0f;
            hasPhysicalRouteProgress = false;
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            int worldSolidLayer = LayerMask.NameToLayer("WorldSolid");
            groundLayerMask = LayerBit(worldSurfaceLayer) |
                              LayerBit(worldSolidLayer);
            // Roadside props and poles in the donor baseline can legitimately
            // share WorldSurface with the road mesh. Excluding the complete
            // layer made those obstacles invisible until the chassis collider
            // touched them. Supporting surfaces are already rejected below by
            // their upward-facing hit normal, so keep every default physics
            // layer in the forward perception query.
            obstacleLayerMask = Physics.DefaultRaycastLayers;
            EnsureRoutePhysics();
            CaptureWheelBaseTransforms();
        }

        private void OnDestroy()
        {
            if (structuralFailureSensor != null)
            {
                structuralFailureSensor.StructuralFailure -=
                    HandleStructuralFailure;
            }
        }

        public void SetRoutePoseTarget(
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                throw new ArgumentException(
                    "Story-traffic target pose must be finite.");
            }

            Vector3 conformedPosition = ConformToGround(
                worldPosition,
                out Vector3 surfaceNormal);
            Quaternion conformedRotation = AlignToSurface(
                worldRotation,
                surfaceNormal);
            bool isDiscontinuity = hasLastReceivedRoutePosition &&
                Vector3.Distance(
                    lastReceivedRoutePosition,
                    conformedPosition) >= teleportDistanceMeters;
            lastReceivedRoutePosition = conformedPosition;
            hasLastReceivedRoutePosition = true;

            if (!hasRouteTarget || isDiscontinuity)
            {
                pendingRouteTargets.Clear();
                hasLastQueuedRoutePosition = false;
                routeTargetPosition = conformedPosition;
                routeTargetRotation = conformedRotation;
                currentGroundNormal = surfaceNormal;
                currentSpeedMetersPerSecond = 0f;
                hasRouteTarget = true;
                SnapWorldPose(routeTargetPosition, routeTargetRotation);
                RecordSafePose(routeTargetPosition, routeTargetRotation, force: true);
                return;
            }

            Vector3 comparisonPosition = hasLastQueuedRoutePosition
                ? lastQueuedRoutePosition
                : routeTargetPosition;
            if (Vector3.Distance(
                    comparisonPosition,
                    conformedPosition) < RouteSampleMergeDistanceMeters)
            {
                return;
            }

            if (pendingRouteTargets.Count >= MaximumPendingRouteSamples)
            {
                // Preserve the already queued road shape. Dropping the oldest
                // point would let a blocked car cut directly across the map or
                // appear beyond an obstacle. The newest logical position will
                // be sampled again after the backlog starts draining.
                return;
            }

            pendingRouteTargets.Enqueue(new RoutePoseTarget(
                conformedPosition,
                conformedRotation,
                surfaceNormal));
            lastQueuedRoutePosition = conformedPosition;
            hasLastQueuedRoutePosition = true;
        }

        /// <summary>
        /// Establishes an authoritative physical pose before a newly streamed
        /// vehicle is allowed to feed Rigidbody state back into route progress.
        /// Unlike an ordinary guidance update this always clears the retained
        /// pursuit queue and snaps the chassis, even when the requested route
        /// sample happens to match the last logical target.
        /// </summary>
        public void SnapToRoutePoseTarget(
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            // Rejoin is derived from the live chassis-to-route separation.
            // A streamed respawn or an explicit safe-pose recovery establishes
            // a new authoritative route pose, so retaining the previous
            // off-route flag would suppress normal drift/passing for one or
            // more physics steps after the snap.
            routeRejoinActive = false;
            hasRouteTarget = false;
            hasLastReceivedRoutePosition = false;
            hasLastQueuedRoutePosition = false;
            pendingRouteTargets.Clear();
            SetRoutePoseTarget(worldPosition, worldRotation);
        }

        public void SetPhysicalRouteGuidanceTarget(
            Vector3 worldPosition,
            Quaternion worldRotation,
            float physicalRouteProgress01)
        {
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                throw new ArgumentException(
                    "Physical story-traffic guidance pose must be finite.");
            }

            Vector3 conformedPosition = ConformToGround(
                worldPosition,
                out Vector3 surfaceNormal);
            routeTargetPosition = conformedPosition;
            routeTargetRotation = AlignToSurface(
                worldRotation,
                surfaceNormal);
            currentGroundNormal = surfaceNormal;
            hasRouteTarget = true;
            pendingRouteTargets.Clear();
            hasLastQueuedRoutePosition = false;
            lastReceivedRoutePosition = conformedPosition;
            hasLastReceivedRoutePosition = true;

            // The old 0.992 progress trigger was only a prototype shortcut.
            // On the complete 2,088-point donor route that progress belongs to
            // the TrackField circuit, not Teimo's shop, so it caused cars to
            // stop at an unrelated point. A Teimo dwell must be started by a
            // concrete route anchor/event once that donor event is imported.
            lastPhysicalRouteProgress01 = Mathf.Clamp01(
                physicalRouteProgress01);
            hasPhysicalRouteProgress = true;
        }

        private void FixedUpdate()
        {
            if (structuralSensorSuspensionFixedSteps > 0)
            {
                structuralSensorSuspensionFixedSteps--;
                if (structuralSensorSuspensionFixedSteps == 0 &&
                    structuralFailureSensor != null &&
                    !storyIncidentHold)
                {
                    structuralFailureSensor.SetSuspended(false);
                }
            }

            // Rigidbody interpolation deliberately leaves Transform one
            // rendered frame behind the physics pose. Route advancement,
            // obstacle planning and steering all run in FixedUpdate, so they
            // must observe the Rigidbody pose that MovePosition/PhysX owns.
            Vector3 currentPosition = routeBody != null
                ? routeBody.position
                : transform.position;
            Quaternion currentRotation = routeBody != null
                ? routeBody.rotation
                : transform.rotation;

            if (TryRecoverCatastrophicFall(
                    currentPosition,
                    currentRotation,
                    Time.fixedDeltaTime,
                    out Vector3 recoveredPosition,
                    out Quaternion recoveredRotation))
            {
                currentPosition = recoveredPosition;
                currentRotation = recoveredRotation;
            }

            if (!hasRouteTarget)
            {
                if (motionBackend != null && motionBackend.IsOperational)
                {
                    motionBackend.Step(
                        Time.fixedDeltaTime,
                        StoryTrafficVehicleDriveCommand.Stop(
                            currentPosition,
                            currentRotation * Vector3.forward));
                    currentSpeedMetersPerSecond =
                        motionBackend.SpeedMetersPerSecond;
                }

                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            behaviorClockSeconds += deltaTime;
            socialStopSecondsRemaining = Mathf.Max(
                0f,
                socialStopSecondsRemaining - deltaTime);
            passingRetryCooldownSeconds = Mathf.Max(
                0f,
                passingRetryCooldownSeconds - deltaTime);
            if (motionBackend != null && motionBackend.IsOperational)
            {
                currentSpeedMetersPerSecond =
                    motionBackend.SpeedMetersPerSecond;
            }

            collisionEventCooldown = Mathf.Max(
                0f,
                collisionEventCooldown - deltaTime);
            PromoteReachedRouteTargets(currentPosition);
            Vector3 routeForward = ResolveRouteForward(currentPosition);
            Vector3 routeUp = currentGroundNormal.sqrMagnitude > 0.5f
                ? currentGroundNormal.normalized
                : Vector3.up;
            Vector3 routeRight = Vector3.Cross(
                routeUp,
                routeForward).normalized;
            Vector3 vehicleForward = Vector3.ProjectOnPlane(
                currentRotation * Vector3.forward,
                routeUp).normalized;
            if (vehicleForward.sqrMagnitude <= 0.0001f)
            {
                vehicleForward = routeForward;
            }
            Vector3 vehicleRight = Vector3.Cross(
                routeUp,
                vehicleForward).normalized;
            float actualLaneOffsetMeters = ResolveActualLaneOffset(
                currentPosition,
                routeTargetPosition,
                routeForward,
                routeRight);
            // A long cast along the current chassis heading cuts across the
            // inside of a bend. At the Perajarvi bus origin it pointed at a
            // fence roughly 24 metres ahead even though the authored route
            // curved past it with more than 14 metres of clearance. Probe the
            // swept route heading for normal anticipation and retain only a
            // short current-heading cast for a genuinely immediate obstacle.
            bool immediateLaneBlocked = TryFindBlockingObstacle(
                currentPosition,
                vehicleForward,
                Mathf.Min(
                    obstacleProbeDistanceMeters,
                    DonorEmergencyObstacleRangeMeters),
                out RaycastHit immediateObstacleHit);
            Vector3 sweptRouteProbeDirection =
                ResolveObstacleProbeDirection(
                    vehicleForward,
                    routeForward,
                    routeUp);
            bool sweptRouteBlocked = TryFindBlockingObstacle(
                currentPosition,
                sweptRouteProbeDirection,
                ResolveObstacleProbeDistance(
                    vehicleForward,
                    routeForward),
                out RaycastHit sweptRouteObstacleHit);
            bool centerLaneBlocked = immediateLaneBlocked ||
                                     sweptRouteBlocked;
            RaycastHit obstacleHit = immediateLaneBlocked &&
                                     (!sweptRouteBlocked ||
                                      immediateObstacleHit.distance <=
                                      sweptRouteObstacleHit.distance)
                ? immediateObstacleHit
                : sweptRouteObstacleHit;
            UpdateManeuver(
                currentPosition,
                vehicleForward,
                vehicleRight,
                actualLaneOffsetMeters,
                centerLaneBlocked,
                obstacleHit,
                deltaTime);
            if (routeRejoinActive &&
                (maneuverState == StoryTrafficManeuverState.Cruise ||
                 maneuverState == StoryTrafficManeuverState.Returning ||
                 maneuverState == StoryTrafficManeuverState.Drifting))
            {
                targetLaneOffsetMeters = baseLaneOffsetMeters;
            }
            else if ((maneuverState == StoryTrafficManeuverState.Cruise ||
                      maneuverState == StoryTrafficManeuverState.Drifting) &&
                socialStopSecondsRemaining <= 0f)
            {
                targetLaneOffsetMeters = ResolveNaturalLaneOffset();
            }
            currentLaneOffsetMeters = Mathf.MoveTowards(
                currentLaneOffsetMeters,
                targetLaneOffsetMeters,
                laneChangeMetersPerSecond * deltaTime);

            Vector3 motionTarget = ResolveMotionTarget(
                routeTargetPosition,
                routeTargetRotation,
                currentGroundNormal);
            if (maneuverState == StoryTrafficManeuverState.Reversing &&
                hasReverseEscapeTarget)
            {
                motionTarget = reverseEscapeTarget;
            }
            Vector3 toTarget = motionTarget - currentPosition;
            float targetDistance = toTarget.magnitude;

            Vector3 desiredDirection = targetDistance > 0.001f
                ? toTarget / targetDistance
                : routeForward;
            if (maneuverState == StoryTrafficManeuverState.Reversing)
            {
                desiredDirection = targetDistance > 0.001f
                    ? toTarget / targetDistance
                    : -routeForward;
            }

            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                desiredDirection.Normalize();
            }

            UpdateDriftState(
                vehicleForward,
                desiredDirection,
                centerLaneBlocked,
                deltaTime);

            RaycastHit maneuverHit = default;
            bool canProbeManeuverPath =
                maneuverState != StoryTrafficManeuverState.Reversing &&
                maneuverState != StoryTrafficManeuverState.Crashed;
            Vector3 maneuverProbeDirection = ResolveObstacleProbeDirection(
                vehicleForward,
                desiredDirection,
                routeUp);
            bool maneuverPathBlocked = canProbeManeuverPath &&
                TryFindBlockingObstacle(
                    currentPosition,
                    maneuverProbeDirection,
                    ResolveObstacleProbeDistance(
                        vehicleForward,
                        desiredDirection),
                    out maneuverHit);
            if (maneuverPathBlocked &&
                maneuverState != StoryTrafficManeuverState.Braking)
            {
                obstacleHit = maneuverHit;
            }

            bool socialStop = socialStopSecondsRemaining > 0f ||
                              storyIncidentHold ||
                              routeFullStopHold ||
                              terminalAbandonmentHold ||
                              serviceStopHold;
            bool passingAroundBlockedPath =
                (centerLaneBlocked || maneuverPathBlocked) &&
                (maneuverState == StoryTrafficManeuverState.PassingOut ||
                 maneuverState == StoryTrafficManeuverState.Passing ||
                 maneuverState == StoryTrafficManeuverState.Returning ||
                 maneuverState == StoryTrafficManeuverState.Recovering);
            bool followingRoadVehicle =
                maneuverState == StoryTrafficManeuverState.Braking &&
                centerLaneBlocked &&
                IsStoryTrafficVehicleObstacle(obstacleHit.collider);
            float followingSpeed = followingRoadVehicle
                ? ResolveFollowingSpeed(obstacleHit, routeForward)
                : 0f;
            bool pathBlockedByFollowedVehicle = maneuverPathBlocked &&
                IsStoryTrafficVehicleObstacle(maneuverHit.collider) &&
                (trackedObstacle == null ||
                 maneuverHit.collider == trackedObstacle ||
                 maneuverHit.collider.transform.IsChildOf(
                     trackedObstacle.transform));
            IsObstacleBraking = socialStop ||
                                maneuverState ==
                                StoryTrafficManeuverState.Crashed ||
                                maneuverState ==
                                StoryTrafficManeuverState.Braking &&
                                !followingRoadVehicle ||
                                followingRoadVehicle &&
                                followingSpeed <= 0.05f ||
                                maneuverPathBlocked &&
                                !passingAroundBlockedPath &&
                                !pathBlockedByFollowedVehicle;
            float desiredSpeed;
            if (socialStop ||
                maneuverState == StoryTrafficManeuverState.Crashed)
            {
                desiredSpeed = 0f;
            }
            else if (maneuverState == StoryTrafficManeuverState.Recovering)
            {
                desiredSpeed = recoverySpeedMetersPerSecond;
            }
            else if (maneuverState == StoryTrafficManeuverState.Reversing)
            {
                desiredSpeed = reverseSpeedMetersPerSecond;
            }
            else if (followingRoadVehicle)
            {
                desiredSpeed = followingSpeed;
            }
            else if (IsObstacleBraking)
            {
                desiredSpeed = 0f;
            }
            else if (passingAroundBlockedPath)
            {
                // Steering needs a little longitudinal motion. Full braking
                // while the passing arc still intersects the obstacle creates
                // a deadlock because tire forces cannot move a stationary car
                // sideways into the clear lane.
                desiredSpeed = Mathf.Min(
                    9f,
                    ResolveLiveCruiseSpeed(
                        desiredDirection,
                        routeForward,
                        targetDistance));
            }
            else
            {
                float liveCruiseSpeed = ResolveLiveCruiseSpeed(
                    desiredDirection,
                    routeForward,
                    targetDistance);
                cruiseSpeedMetersPerSecond = liveCruiseSpeed;
                desiredSpeed = Mathf.Min(
                    maneuverState == StoryTrafficManeuverState.Drifting
                        ? liveCruiseSpeed * 0.82f
                        : liveCruiseSpeed,
                    targetDistance / Mathf.Max(deltaTime, 0.0001f));
            }

            if (motionBackend != null && motionBackend.IsOperational)
            {
                LastDesiredSpeedMetersPerSecond = desiredSpeed;
                LastPhysicalTargetDistanceMeters = targetDistance;
                motionBackend.Step(
                    deltaTime,
                    new StoryTrafficVehicleDriveCommand(
                        motionTarget,
                        desiredDirection,
                        desiredSpeed,
                        reverse: maneuverState ==
                                 StoryTrafficManeuverState.Reversing,
                         fullBrake: IsObstacleBraking,
                         handbrake: HandbrakeActive));
                currentSpeedMetersPerSecond =
                    motionBackend.SpeedMetersPerSecond;
                UpdatePhysicalRouteDeadlock(
                    desiredSpeed,
                    targetDistance,
                    socialStop,
                    centerLaneBlocked || maneuverPathBlocked,
                    currentPosition,
                    routeForward,
                    routeRight,
                    deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ReportPhysicalStallDiagnostic(
                    deltaTime,
                    desiredSpeed,
                    targetDistance,
                    centerLaneBlocked,
                    maneuverPathBlocked);
#endif

                if (maneuverState == StoryTrafficManeuverState.Cruise &&
                    !centerLaneBlocked &&
                    !maneuverPathBlocked &&
                    Mathf.Abs(currentLaneOffsetMeters -
                              baseLaneOffsetMeters) <= 0.15f &&
                    motionBackend.HasGroundContact)
                {
                    RecordSafePose(
                        currentPosition,
                        currentRotation,
                        force: false);
                }

                return;
            }

            float rate = desiredSpeed < currentSpeedMetersPerSecond
                ? brakingMetersPerSecond2
                : accelerationMetersPerSecond2;
            currentSpeedMetersPerSecond = Mathf.MoveTowards(
                currentSpeedMetersPerSecond,
                desiredSpeed,
                rate * deltaTime);
            LastDesiredSpeedMetersPerSecond = desiredSpeed;
            LastPhysicalTargetDistanceMeters = targetDistance;

            bool unrestrictedRecoveryStep =
                maneuverState == StoryTrafficManeuverState.Reversing;
            float step = unrestrictedRecoveryStep
                ? currentSpeedMetersPerSecond * deltaTime
                : Mathf.Min(
                    targetDistance,
                    currentSpeedMetersPerSecond * deltaTime);
            Vector3 nextPosition = currentPosition + desiredDirection * step;
            nextPosition = ConformToGround(
                nextPosition,
                out Vector3 surfaceNormal);
            currentGroundNormal = surfaceNormal;
            Quaternion desiredRotation;
            if (maneuverState == StoryTrafficManeuverState.Crashed)
            {
                desiredRotation = currentRotation;
            }
            else
            {
                desiredRotation = desiredDirection.sqrMagnitude > 0.0001f
                    ? LookAlongSurface(desiredDirection, surfaceNormal)
                    : routeTargetRotation;
                if (maneuverState == StoryTrafficManeuverState.Drifting)
                {
                    desiredRotation = Quaternion.AngleAxis(
                        driftSlipDegrees,
                        surfaceNormal) * desiredRotation;
                }
            }

            Quaternion nextRotation = Quaternion.RotateTowards(
                currentRotation,
                desiredRotation,
                turnRateDegreesPerSecond * deltaTime);
            SetWorldPose(nextPosition, nextRotation);

            if (maneuverState == StoryTrafficManeuverState.Cruise &&
                !centerLaneBlocked &&
                !maneuverPathBlocked &&
                Mathf.Abs(currentLaneOffsetMeters -
                          baseLaneOffsetMeters) <= 0.15f)
            {
                RecordSafePose(nextPosition, nextRotation, force: false);
            }
        }

        private void UpdateManeuver(
            Vector3 currentPosition,
            Vector3 routeForward,
            Vector3 routeRight,
            float actualLaneOffsetMeters,
            bool centerLaneBlocked,
            RaycastHit centerLaneHit,
            float deltaTime)
        {
            maneuverStateSeconds += deltaTime;
            if (socialStopSecondsRemaining > 0f ||
                routeFullStopHold || terminalAbandonmentHold ||
                serviceStopHold)
            {
                trackedObstacle = null;
                hasReverseEscapeTarget = false;
                driftSlipDegrees = 0f;
                CancelPassingAttempt(PassingRetryCooldownSeconds);
                if (maneuverState != StoryTrafficManeuverState.Cruise)
                {
                    EnterManeuver(StoryTrafficManeuverState.Cruise);
                }

                targetLaneOffsetMeters = baseLaneOffsetMeters;
                return;
            }

            UpdatePassingAttemptBudget(currentPosition, deltaTime);
            switch (maneuverState)
            {
                case StoryTrafficManeuverState.Cruise:
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    if (!centerLaneBlocked)
                    {
                        return;
                    }

                    trackedObstacle = centerLaneHit.collider;
                    if (IsStoryTrafficVehicleObstacle(trackedObstacle))
                    {
                        // The rear racer first opens a following gap. This
                        // prevents both cars from selecting the same lateral
                        // escape while leaving their Perajarvi formation.
                        EnterManeuver(StoryTrafficManeuverState.Braking);
                    }
                    else if (!routeRejoinActive &&
                             !passingSuppressed &&
                             passingRetryCooldownSeconds <= 0f &&
                             TryCanEnterPassingLane(
                            currentPosition,
                            routeForward,
                            routeRight))
                    {
                        EnterManeuver(
                            StoryTrafficManeuverState.PassingOut);
                    }
                    else
                    {
                        EnterManeuver(StoryTrafficManeuverState.Braking);
                    }

                    break;

                case StoryTrafficManeuverState.Braking:
                    targetLaneOffsetMeters = currentLaneOffsetMeters;
                    bool blockedByStoryTraffic = centerLaneBlocked &&
                        IsStoryTrafficVehicleObstacle(centerLaneHit.collider);
                    if (!centerLaneBlocked)
                    {
                        EnterManeuver(
                            Mathf.Abs(currentLaneOffsetMeters -
                                      baseLaneOffsetMeters) > 0.2f
                                ? StoryTrafficManeuverState.Returning
                                : StoryTrafficManeuverState.Cruise);
                    }
                    else if (blockedByStoryTraffic &&
                             maneuverStateSeconds <
                             movingVehiclePassDelaySeconds)
                    {
                        // Yield briefly to the leading racer. If it remains
                        // stopped, the normal passing check below is allowed.
                    }
                    else if (!routeRejoinActive &&
                             !passingSuppressed &&
                             passingRetryCooldownSeconds <= 0f &&
                             TryCanEnterPassingLane(
                                 currentPosition,
                                 routeForward,
                                 routeRight))
                    {
                        trackedObstacle = centerLaneHit.collider;
                        EnterManeuver(
                            StoryTrafficManeuverState.PassingOut);
                    }
                    else if (!blockedByStoryTraffic &&
                             maneuverStateSeconds >=
                             blockedBeforeReverseSeconds &&
                             currentSpeedMetersPerSecond <= 0.75f)
                    {
                        PrepareReverseEscape(
                            currentPosition,
                            routeForward,
                            routeRight,
                            centerLaneHit.collider,
                            invertRetainedSide: false);
                        EnterManeuver(
                            StoryTrafficManeuverState.Reversing);
                    }
                    else if (blockedByStoryTraffic &&
                             maneuverStateSeconds >=
                             blockedBeforeReverseSeconds +
                             movingVehiclePassDelaySeconds &&
                             currentSpeedMetersPerSecond <= 0.75f)
                    {
                        // At Teimo's narrow turnaround two racers can see each
                        // other while neither passing lane is physically clear.
                        // After a bounded yield, the rear actor backs out; it
                        // must never wait forever or teleport past the leader.
                        PrepareReverseEscape(
                            currentPosition,
                            routeForward,
                            routeRight,
                            centerLaneHit.collider,
                            invertRetainedSide: false);
                        EnterManeuver(
                            StoryTrafficManeuverState.Reversing);
                    }

                    break;

                case StoryTrafficManeuverState.PassingOut:
                    targetLaneOffsetMeters = activePassingLaneOffsetMeters;
                    if (Mathf.Abs(
                            actualLaneOffsetMeters -
                            activePassingLaneOffsetMeters) <= 0.18f)
                    {
                        EnterManeuver(StoryTrafficManeuverState.Passing);
                    }

                    break;

                case StoryTrafficManeuverState.Passing:
                    targetLaneOffsetMeters = activePassingLaneOffsetMeters;
                    if (HasPassedTrackedObstacle(
                            currentPosition,
                            routeForward))
                    {
                        EnterManeuver(StoryTrafficManeuverState.Returning);
                    }

                    break;

                case StoryTrafficManeuverState.Returning:
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    if (routeRejoinActive && centerLaneBlocked)
                    {
                        trackedObstacle = centerLaneHit.collider;
                        EnterManeuver(StoryTrafficManeuverState.Braking);
                    }
                    else if (centerLaneBlocked &&
                        !HasPassedTrackedObstacle(
                            currentPosition,
                            routeForward))
                    {
                        if (passingAttemptExpired || passingSuppressed ||
                            routeRejoinActive)
                        {
                            EnterManeuver(
                                StoryTrafficManeuverState.Braking);
                        }
                        else
                        {
                            EnterManeuver(
                                StoryTrafficManeuverState.Passing);
                        }
                    }
                    else if (Mathf.Abs(actualLaneOffsetMeters -
                                      baseLaneOffsetMeters) <= 0.18f)
                    {
                        trackedObstacle = null;
                        ClearPassingAttempt();
                        EnterManeuver(StoryTrafficManeuverState.Cruise);
                    }

                    break;

                case StoryTrafficManeuverState.Reversing:
                    targetLaneOffsetMeters = recoveryLaneOffsetMeters;
                    if (maneuverStateSeconds >= reverseDurationSeconds ||
                        hasReverseEscapeTarget &&
                        Vector3.Distance(
                            currentPosition,
                            reverseEscapeTarget) <= 0.8f)
                    {
                        currentSpeedMetersPerSecond = 0f;
                        EnterManeuver(StoryTrafficManeuverState.Recovering);
                    }

                    break;

                case StoryTrafficManeuverState.Drifting:
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    if (centerLaneBlocked)
                    {
                        driftSlipDegrees = 0f;
                        trackedObstacle = centerLaneHit.collider;
                        EnterManeuver(StoryTrafficManeuverState.Braking);
                    }

                    break;

                case StoryTrafficManeuverState.Crashed:
                    targetLaneOffsetMeters = currentLaneOffsetMeters;
                    if (!storyIncidentHold &&
                        maneuverStateSeconds >= crashHoldSeconds)
                    {
                        recoveryCount++;
                        PrepareReverseEscape(
                            currentPosition,
                            routeForward,
                            routeRight,
                            trackedObstacle,
                            invertRetainedSide: false);
                        EnterManeuver(StoryTrafficManeuverState.Reversing);
                    }

                    break;

                case StoryTrafficManeuverState.Recovering:
                    // Rejoin forwards on the lateral escape corridor selected
                    // while reversing. Returning directly to the centre target
                    // recreates the same collision and the old back/forward
                    // deadlock loop.
                    targetLaneOffsetMeters = recoveryLaneOffsetMeters;
                    if (!centerLaneBlocked &&
                        maneuverStateSeconds >= 0.45f)
                    {
                        driftSlipDegrees = 0f;
                        trackedObstacle = null;
                        EnterManeuver(StoryTrafficManeuverState.Returning);
                    }
                    else if (maneuverStateSeconds >=
                             recoveryDurationSeconds * 1.5f)
                    {
                        // The first side was genuinely boxed in. Back out on
                        // the opposite side instead of repeating the same arc.
                        PrepareReverseEscape(
                            currentPosition,
                            routeForward,
                            routeRight,
                            centerLaneHit.collider,
                            invertRetainedSide: true);
                        EnterManeuver(StoryTrafficManeuverState.Reversing);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EnterManeuver(StoryTrafficManeuverState next)
        {
            maneuverState = next;
            maneuverStateSeconds = 0f;
            if (next == StoryTrafficManeuverState.PassingOut &&
                !passingAttemptActive)
            {
                passingAttemptActive = true;
                passingAttemptExpired = false;
                passingAttemptElapsedSeconds = 0f;
                passingAttemptStartPosition = PhysicalWorldPosition;
            }
            switch (next)
            {
                case StoryTrafficManeuverState.Cruise:
                    hasReverseEscapeTarget = false;
                    recoveryLaneOffsetMeters = baseLaneOffsetMeters;
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    break;
                case StoryTrafficManeuverState.Returning:
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    break;
                case StoryTrafficManeuverState.PassingOut:
                case StoryTrafficManeuverState.Passing:
                    targetLaneOffsetMeters = activePassingLaneOffsetMeters;
                    break;
                case StoryTrafficManeuverState.Crashed:
                case StoryTrafficManeuverState.Braking:
                    targetLaneOffsetMeters = currentLaneOffsetMeters;
                    break;
                case StoryTrafficManeuverState.Reversing:
                case StoryTrafficManeuverState.Recovering:
                    targetLaneOffsetMeters = recoveryLaneOffsetMeters;
                    break;
                case StoryTrafficManeuverState.Drifting:
                    targetLaneOffsetMeters = baseLaneOffsetMeters;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(next));
            }
        }

        private void UpdatePassingAttemptBudget(
            Vector3 currentPosition,
            float deltaTime)
        {
            if (!passingAttemptActive)
            {
                return;
            }

            passingAttemptElapsedSeconds += Mathf.Max(0f, deltaTime);
            float travelledMeters = Vector3.Distance(
                passingAttemptStartPosition,
                currentPosition);
            if (!passingAttemptExpired &&
                (passingAttemptElapsedSeconds >= MaximumPassingAttemptSeconds ||
                 travelledMeters >= MaximumPassingAttemptDistanceMeters))
            {
                passingAttemptExpired = true;
                passingRetryCooldownSeconds = Mathf.Max(
                    passingRetryCooldownSeconds,
                    PassingRetryCooldownSeconds);
                if (maneuverState == StoryTrafficManeuverState.PassingOut ||
                    maneuverState == StoryTrafficManeuverState.Passing)
                {
                    EnterManeuver(StoryTrafficManeuverState.Returning);
                }
            }
        }

        private void CancelPassingAttempt(float retryCooldownSeconds)
        {
            passingAttemptActive = false;
            passingAttemptExpired = false;
            passingAttemptElapsedSeconds = 0f;
            passingRetryCooldownSeconds = Mathf.Max(
                passingRetryCooldownSeconds,
                Mathf.Max(0f, retryCooldownSeconds));
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
        }

        private void ClearPassingAttempt()
        {
            passingAttemptActive = false;
            passingAttemptExpired = false;
            passingAttemptElapsedSeconds = 0f;
            activePassingLaneOffsetMeters = passingLaneOffsetMeters;
        }

        private void UpdateDriftState(
            Vector3 vehicleForward,
            Vector3 desiredDirection,
            bool centerLaneBlocked,
            float deltaTime)
        {
            Vector3 up = currentGroundNormal.sqrMagnitude > 0.5f
                ? currentGroundNormal.normalized
                : Vector3.up;
            Vector3 currentForward = Vector3.ProjectOnPlane(
                vehicleForward,
                up).normalized;
            Vector3 routeDirection = Vector3.ProjectOnPlane(
                desiredDirection,
                up).normalized;
            float signedTurnAngle = currentForward.sqrMagnitude > 0.0001f &&
                                    routeDirection.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(currentForward, routeDirection, up)
                : 0f;
            float absoluteTurnAngle = Mathf.Abs(signedTurnAngle);

            bool donorHandbrakeRequested = donorHandbrakeZoneActive &&
                !donorHandbrakeZoneConsumed;
            bool driftEntryRequested = automaticDriftEntryEnabled
                ? absoluteTurnAngle >= driftEntryAngleDegrees
                // The four donor HandbrakeZone FSMs are the authority for
                // village slides. Requiring a second heading threshold made a
                // correctly aligned car pass straight through the trigger.
                : donorHandbrakeRequested;
            if (!routeRejoinActive &&
                maneuverState == StoryTrafficManeuverState.Cruise &&
                !centerLaneBlocked &&
                currentSpeedMetersPerSecond >=
                minimumDriftSpeedMetersPerSecond &&
                driftEntryRequested)
            {
                donorHandbrakeZoneConsumed |= donorHandbrakeRequested;
                donorHandbrakeDriftActive = donorHandbrakeRequested;
                EnterManeuver(StoryTrafficManeuverState.Drifting);
            }

            if (maneuverState != StoryTrafficManeuverState.Drifting)
            {
                donorHandbrakeDriftActive = false;
                driftSlipDegrees = Mathf.MoveTowards(
                    driftSlipDegrees,
                    0f,
                    maximumDriftSlipDegrees * 4f * deltaTime);
                return;
            }

            float targetSlip = Mathf.Clamp(
                signedTurnAngle * 0.62f,
                -maximumDriftSlipDegrees,
                maximumDriftSlipDegrees);
            driftSlipDegrees = Mathf.MoveTowards(
                driftSlipDegrees,
                targetSlip,
                maximumDriftSlipDegrees * 5f * deltaTime);

            float requiredMinimumSeconds = donorHandbrakeDriftActive
                ? Mathf.Max(
                    minimumDriftSeconds,
                    PhysicalHandbrakePulseSeconds)
                : minimumDriftSeconds;
            bool minimumElapsed = maneuverStateSeconds >=
                                  requiredMinimumSeconds;
            if (maneuverStateSeconds >= maximumDriftSeconds ||
                minimumElapsed && absoluteTurnAngle <= driftExitAngleDegrees)
            {
                EnterManeuver(StoryTrafficManeuverState.Cruise);
                donorHandbrakeDriftActive = false;
            }
        }

        private void UpdatePhysicalRouteDeadlock(
            float desiredSpeedMetersPerSecond,
            float targetDistanceMeters,
            bool intentionalStop,
            bool obstacleBlocked,
            Vector3 currentPosition,
            Vector3 routeForward,
            Vector3 routeRight,
            float deltaTime)
        {
            bool canWatch = maneuverState == StoryTrafficManeuverState.Cruise ||
                            maneuverState == StoryTrafficManeuverState.Drifting ||
                            maneuverState == StoryTrafficManeuverState.PassingOut ||
                            maneuverState == StoryTrafficManeuverState.Passing ||
                            maneuverState == StoryTrafficManeuverState.Returning;
            if (!canWatch || intentionalStop || obstacleBlocked ||
                desiredSpeedMetersPerSecond < 2.5f ||
                targetDistanceMeters < 4f ||
                currentSpeedMetersPerSecond > 0.45f ||
                !motionBackend.HasGroundContact)
            {
                routeDeadlockElapsedSeconds = 0f;
                return;
            }

            routeDeadlockElapsedSeconds += Mathf.Max(0f, deltaTime);
            float actorStaggerSeconds = behaviorSeed01 * 0.9f;
            if (routeDeadlockElapsedSeconds <
                routeDeadlockSeconds + actorStaggerSeconds)
            {
                return;
            }

            // This is a physical reverse recovery. It changes no route progress
            // directly and never restores/snapshots a pose, so a tight donor
            // waypoint cannot turn into the old behind-the-obstacle teleport.
            routeDeadlockElapsedSeconds = 0f;
            trackedObstacle = null;
            recoveryCount++;
            PrepareReverseEscape(
                currentPosition,
                routeForward,
                routeRight,
                obstacle: null,
                invertRetainedSide: false);
            EnterManeuver(StoryTrafficManeuverState.Reversing);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;
            if (collision.contactCount > 0 &&
                Mathf.Abs(contact.normal.y) >= 0.78f)
            {
                // Ground contact is continuous support, not a traffic crash.
                return;
            }

            Vector3 point = collision.contactCount > 0
                ? contact.point
                : ResolveSafeClosestPoint(
                    collision.collider,
                    transform.position);
            // A glancing wheel/terrain contact at road speed is not a 30 m/s
            // impact. The previous max(vehicle speed, relative velocity)
            // classified ordinary cornering contacts as terminal crashes and
            // ejected Suski. Only closing velocity along the actual contact
            // normal represents collision severity here.
            float impactSpeed = collision.contactCount > 0
                ? Mathf.Abs(Vector3.Dot(
                    collision.relativeVelocity,
                    contact.normal))
                : collision.relativeVelocity.magnitude;
            ReportImpact(collision.collider, point, impactSpeed);
        }

        /// <summary>
        /// Entry point for the PhysX callback and future custom traffic-physics
        /// backends. Returns true only when the project-owned recoverable
        /// contact threshold is crossed and the vehicle enters recovery. The
        /// donor terminal story crash is signalled independently by the
        /// structural-failure sensor.
        /// </summary>
        public bool ReportImpact(
            Collider other,
            Vector3 worldPoint,
            float impactSpeedMetersPerSecond)
        {
            if (other == null ||
                other.transform == transform ||
                other.transform.IsChildOf(transform) ||
                !IsFinite(worldPoint) ||
                !float.IsFinite(impactSpeedMetersPerSecond) ||
                collisionEventCooldown > 0f)
            {
                return false;
            }

            float impactSpeed = Mathf.Max(0f, impactSpeedMetersPerSecond);
            bool crash = impactSpeed >= crashSpeedThresholdMetersPerSecond;
            bool entersCrashState = crash &&
                maneuverState != StoryTrafficManeuverState.Crashed &&
                maneuverState != StoryTrafficManeuverState.Recovering;
            collisionEventCooldown = 0.35f;
            lastImpactSpeedMetersPerSecond = impactSpeed;
            lastImpactWorldPoint = worldPoint;
            hasLastImpactWorldPoint = true;
            if (entersCrashState)
            {
                // Enter before publishing. The NPC story subscriber may turn
                // this into a persistent incident hold synchronously; doing
                // so must not make this method report that no crash occurred.
                trackedObstacle = other;
                driftSlipDegrees = 0f;
                EnterManeuver(StoryTrafficManeuverState.Crashed);
            }

            CollisionIncident?.Invoke(new StoryTrafficCollisionEvent(
                driverFeatureId,
                other,
                worldPoint,
                impactSpeed,
                crash));
            return entersCrashState;
        }

        private void HandleStructuralFailure()
        {
            float speed = Mathf.Max(
                lastImpactSpeedMetersPerSecond,
                currentSpeedMetersPerSecond,
                routeBody != null ? routeBody.linearVelocity.magnitude : 0f);
            Vector3 point = hasLastImpactWorldPoint
                ? lastImpactWorldPoint
                : routeBody != null
                    ? routeBody.worldCenterOfMass
                    : transform.position;

            // OnJointBreak publishes synchronously. Drop the failed sensor
            // before the NPC owner reconciles presentations, otherwise that
            // pass can find this sensor while its FixedJoint is already gone.
            if (structuralFailureSensor != null)
            {
                structuralFailureSensor.StructuralFailure -=
                    HandleStructuralFailure;
                structuralFailureSensor = null;
            }

            SetStoryIncidentHold(true);
            TerminalCrash?.Invoke(new StoryTrafficTerminalCrashEvent(
                driverFeatureId,
                point,
                speed));
        }

        private void RecordSafePose(
            Vector3 position,
            Quaternion rotation,
            bool force)
        {
            if (!force && hasSafePose &&
                Vector3.Distance(position, safePosition) < safePoseSpacingMeters)
            {
                return;
            }

            hasSafePose = true;
            safePosition = position;
            safeRotation = rotation;
        }

        private void PrepareReverseEscape(
            Vector3 currentPosition,
            Vector3 routeForward,
            Vector3 routeRight,
            Collider obstacle,
            bool invertRetainedSide)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                routeForward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(
                    PhysicalWorldRotation * Vector3.forward,
                    Vector3.up).normalized;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 right = Vector3.ProjectOnPlane(
                routeRight,
                Vector3.up).normalized;
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }

            float side = recoverySideSign;
            if (obstacle != null)
            {
                Vector3 nearest = ResolveSafeClosestPoint(
                    obstacle,
                    currentPosition);
                float obstacleSide = Vector3.Dot(
                    nearest - currentPosition,
                    right);
                if (Mathf.Abs(obstacleSide) >= 0.2f)
                {
                    side = -Mathf.Sign(obstacleSide);
                }
            }

            if (invertRetainedSide)
            {
                side = -Mathf.Sign(Mathf.Abs(side) > 0.01f ? side : 1f);
            }
            else if (Mathf.Abs(side) <= 0.01f)
            {
                side = behaviorSeed01 >= 0.5f ? 1f : -1f;
            }

            recoverySideSign = side;
            float lateralEscape = Mathf.Clamp(
                Mathf.Abs(passingLaneOffsetMeters) * 0.65f,
                1.6f,
                3.2f);
            recoveryLaneOffsetMeters = baseLaneOffsetMeters +
                                       side * lateralEscape;
            float reverseDistance = Mathf.Max(
                5.5f,
                reverseSpeedMetersPerSecond * reverseDurationSeconds * 1.25f);
            reverseEscapeTarget = currentPosition -
                                  forward * reverseDistance +
                                  right * side * Mathf.Min(2.2f, lateralEscape);
            hasReverseEscapeTarget = true;
        }

        private static Vector3 ResolveSafeClosestPoint(
            Collider obstacle,
            Vector3 worldPosition)
        {
            if (obstacle == null)
            {
                return worldPosition;
            }

            // Unity rejects Collider.ClosestPoint for non-convex mesh
            // colliders. Imported road/terrain support meshes intentionally
            // remain non-convex, so reverse-side selection uses their bounds
            // approximation instead of emitting a PhysX error every retry.
            return obstacle is MeshCollider meshCollider &&
                   !meshCollider.convex
                ? obstacle.ClosestPointOnBounds(worldPosition)
                : obstacle.ClosestPoint(worldPosition);
        }

        private bool TryRecoverCatastrophicFall(
            Vector3 currentPosition,
            Quaternion currentRotation,
            float deltaTime,
            out Vector3 recoveredPosition,
            out Quaternion recoveredRotation)
        {
            recoveredPosition = currentPosition;
            recoveredRotation = currentRotation;
            if (motionBackend == null || !motionBackend.IsOperational ||
                storyIncidentHold || routeFullStopHold ||
                terminalAbandonmentHold || !hasRouteTarget)
            {
                catastrophicFallSeconds = 0f;
                return false;
            }

            bool invalidPose = !IsFinite(currentPosition) ||
                               !IsFinite(currentRotation);
            Vector3 referencePosition = hasSafePose &&
                                        IsFinite(safePosition)
                ? safePosition
                : routeTargetPosition;
            Quaternion referenceRotation = hasSafePose &&
                                           IsFinite(safeRotation)
                ? safeRotation
                : routeTargetRotation;
            if (!IsFinite(referencePosition) ||
                !IsFinite(referenceRotation))
            {
                catastrophicFallSeconds = 0f;
                return false;
            }

            bool deepUnsupportedFall = !motionBackend.HasGroundContact &&
                IsFinite(currentPosition) &&
                currentPosition.y <
                referencePosition.y - CatastrophicFallDepthMeters;
            if (!invalidPose && !deepUnsupportedFall)
            {
                catastrophicFallSeconds = 0f;
                return false;
            }

            catastrophicFallSeconds += Mathf.Max(0f, deltaTime);
            if (!invalidPose && catastrophicFallSeconds <
                CatastrophicFallConfirmationSeconds)
            {
                return false;
            }

            recoveredPosition = ConformToGround(
                referencePosition,
                out Vector3 surfaceNormal);
            recoveredRotation = AlignToSurface(
                referenceRotation,
                surfaceNormal);
            currentGroundNormal = surfaceNormal;
            currentSpeedMetersPerSecond = 0f;
            motionBackend.ResetMotion();
            SnapWorldPose(recoveredPosition, recoveredRotation);
            motionBackend.ResetMotion();
            previousPosition = recoveredPosition;
            hasPreviousPosition = true;
            trackedObstacle = null;
            routeDeadlockElapsedSeconds = 0f;
            catastrophicFallSeconds = 0f;
            currentLaneOffsetMeters = baseLaneOffsetMeters;
            targetLaneOffsetMeters = baseLaneOffsetMeters;
            recoveryLaneOffsetMeters = baseLaneOffsetMeters;
            hasReverseEscapeTarget = false;
            driftSlipDegrees = 0f;
            recoveryCount++;
            EnterManeuver(StoryTrafficManeuverState.Cruise);
            RecordSafePose(
                recoveredPosition,
                recoveredRotation,
                force: true);
            return true;
        }

        private static bool IsRetainedSafePosePlausible(
            Vector3 safePose,
            Vector3 physicalPose)
        {
            if (!IsFinite(safePose) || !IsFinite(physicalPose))
            {
                return false;
            }

            return Mathf.Abs(safePose.y - physicalPose.y) <=
                       MaximumRetainedSafePoseVerticalDeltaMeters &&
                   Vector3.Distance(safePose, physicalPose) <=
                       MaximumRetainedSafePoseSeparationMeters;
        }

        private bool TryCanEnterPassingLane(
            Vector3 currentPosition,
            Vector3 routeForward,
            Vector3 routeRight)
        {
            float preferred = passingLaneOffsetMeters;
            if (TryCanUsePassingLane(
                    currentPosition,
                    routeForward,
                    routeRight,
                    preferred))
            {
                activePassingLaneOffsetMeters = preferred;
                return true;
            }

            return false;
        }

        private float ResolveFollowingSpeed(
            in RaycastHit obstacleHit,
            Vector3 routeForward)
        {
            float obstacleSpeed = 0f;
            Rigidbody otherBody = obstacleHit.rigidbody;
            if (otherBody != null)
            {
                obstacleSpeed = Mathf.Max(
                    0f,
                    Vector3.Dot(otherBody.linearVelocity, routeForward));
            }

            float clearance = Mathf.Max(0f, obstacleHit.distance);
            float desiredGap = minimumFollowingGapMeters +
                               currentSpeedMetersPerSecond *
                               followingHeadwaySeconds;
            float closingSpeed = Mathf.Max(
                0f,
                currentSpeedMetersPerSecond - obstacleSpeed);
            float emergencyDistance = minimumFollowingGapMeters * 0.55f +
                                      closingSpeed * closingSpeed /
                                      (2f * Mathf.Max(
                                          0.1f,
                                          brakingMetersPerSecond2));
            if (clearance <= emergencyDistance)
            {
                return 0f;
            }

            float gapCorrection = (clearance - desiredGap) /
                                  Mathf.Max(0.1f, followingHeadwaySeconds);
            return Mathf.Clamp(
                obstacleSpeed + gapCorrection,
                0f,
                cruiseSpeedMetersPerSecond > 0f
                    ? cruiseSpeedMetersPerSecond
                    : maximumSpeedMetersPerSecond);
        }

        private bool TryCanUsePassingLane(
            Vector3 currentPosition,
            Vector3 routeForward,
            Vector3 routeRight,
            float laneOffsetMeters)
        {
            Vector3 lateral = routeRight *
                              (laneOffsetMeters - currentLaneOffsetMeters);
            Vector3 laneEntry = lateral + routeForward *
                Mathf.Max(5f, Mathf.Abs(laneOffsetMeters) * 1.5f);
            if (TryFindBlockingObstacle(
                    currentPosition,
                    laneEntry.normalized,
                    laneEntry.magnitude,
                    out _))
            {
                return false;
            }

            Vector3 passingLanePosition = currentPosition + lateral;
            Rigidbody obstacleBody = trackedObstacle != null
                ? trackedObstacle.attachedRigidbody
                : null;
            bool movingObstacle = obstacleBody != null &&
                                  (!obstacleBody.isKinematic ||
                                   obstacleBody.linearVelocity.sqrMagnitude >
                                   0.25f);
            float requiredSightDistance = movingObstacle ||
                                          IsStoryTrafficVehicleObstacle(
                                              trackedObstacle)
                ? MinimumPassingSightDistanceMeters
                : StaticObstaclePassingSightDistanceMeters;
            bool forwardClear = !TryFindBlockingObstacle(
                passingLanePosition,
                routeForward,
                Mathf.Max(
                    CalculateForwardProbeDistance() +
                    passingClearanceMeters,
                    requiredSightDistance),
                out _);
            bool rearClear = !TryFindBlockingObstacle(
                passingLanePosition,
                -routeForward,
                Mathf.Max(5f, passingClearanceMeters),
                out _);
            return forwardClear && rearClear;
        }

        private bool HasPassedTrackedObstacle(
            Vector3 currentPosition,
            Vector3 routeForward)
        {
            if (trackedObstacle == null)
            {
                return maneuverStateSeconds >= 1f;
            }

            Bounds bounds = trackedObstacle.bounds;
            Vector3 extents = bounds.extents;
            float projectedExtent =
                Mathf.Abs(routeForward.x) * extents.x +
                Mathf.Abs(routeForward.y) * extents.y +
                Mathf.Abs(routeForward.z) * extents.z;
            return Vector3.Dot(
                       currentPosition - bounds.center,
                       routeForward) >=
                   projectedExtent + passingClearanceMeters;
        }

        private float CalculateForwardProbeDistance()
        {
            float stoppingDistance =
                currentSpeedMetersPerSecond * currentSpeedMetersPerSecond /
                (2f * Mathf.Max(0.1f, brakingMetersPerSecond2));
            float laneChangeSeconds = Mathf.Abs(
                    activePassingLaneOffsetMeters - currentLaneOffsetMeters) /
                Mathf.Max(0.1f, laneChangeMetersPerSecond);
            float laneChangeDistance = currentSpeedMetersPerSecond *
                                       laneChangeSeconds + 3f;
            float minimumPassingSetupDistance =
                Mathf.Abs(activePassingLaneOffsetMeters) * 5f + 5f;
            return Mathf.Max(
                obstacleProbeDistanceMeters,
                stoppingDistance + 3f,
                laneChangeDistance,
                minimumPassingSetupDistance);
        }

        private static float ResolveActualLaneOffset(
            Vector3 currentPosition,
            Vector3 routeTarget,
            Vector3 routeForward,
            Vector3 routeRight)
        {
            Vector3 fromTarget = currentPosition - routeTarget;
            Vector3 projectedCenter = routeTarget + routeForward *
                Vector3.Dot(fromTarget, routeForward);
            return Vector3.Dot(
                currentPosition - projectedCenter,
                routeRight);
        }

        private Vector3 ResolveRouteForward(Vector3 currentPosition)
        {
            Vector3 forward = routeTargetRotation * Vector3.forward;
            forward = Vector3.ProjectOnPlane(
                forward,
                currentGroundNormal.sqrMagnitude > 0.5f
                    ? currentGroundNormal
                    : Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                Vector3 toTarget = routeTargetPosition - currentPosition;
                forward = toTarget.sqrMagnitude > 0.01f
                    ? toTarget.normalized
                    : PhysicalWorldRotation * Vector3.forward;
            }

            return forward.normalized;
        }

        private Vector3 ResolveMotionTarget(
            Vector3 position,
            Quaternion rotation,
            Vector3 surfaceNormal)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.5f
                ? surfaceNormal.normalized
                : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(
                rotation * Vector3.forward,
                normal).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = PhysicalWorldRotation * Vector3.forward;
            }

            Vector3 right = Vector3.Cross(normal, forward).normalized;
            return position + right * currentLaneOffsetMeters;
        }

        private float ResolveInitialCruiseSpeed()
        {
            return Mathf.Lerp(
                minimumCruiseSpeedMetersPerSecond,
                maximumSpeedMetersPerSecond,
                0.35f + ResolveIdentity01() * 0.65f);
        }

        private float ResolveIdentity01()
        {
            unchecked
            {
                uint hash = 2166136261u;
                string identity = driverFeatureId ?? string.Empty;
                for (int index = 0; index < identity.Length; index++)
                {
                    hash = (hash ^ identity[index]) * 16777619u;
                }

                return (hash & 0xffffu) / 65535f;
            }
        }

        private float ResolveNaturalLaneOffset()
        {
            float primary = Mathf.Sin(
                behaviorClockSeconds * (0.31f + behaviorSeed01 * 0.08f));
            float secondary = Mathf.Sin(
                behaviorClockSeconds * 0.127f + behaviorSeed01 * 11f);
            return baseLaneOffsetMeters +
                   (primary * 0.68f + secondary * 0.32f) *
                   laneWanderAmplitudeMeters;
        }

        private float ResolveLiveCruiseSpeed(
            Vector3 desiredDirection,
            Vector3 targetRouteForward,
            float targetDistanceMeters)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(
                behaviorClockSeconds * (0.21f + behaviorSeed01 * 0.04f) +
                behaviorSeed01 * 8f);
            float aggression = Mathf.Clamp01(
                0.55f + behaviorSeed01 * 0.25f + pulse * 0.25f);
            float speed = Mathf.Lerp(
                minimumCruiseSpeedMetersPerSecond,
                maximumSpeedMetersPerSecond,
                aggression);
            Vector3 up = currentGroundNormal.sqrMagnitude > 0.5f
                ? currentGroundNormal.normalized
                : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(
                PhysicalWorldRotation * Vector3.forward,
                up).normalized;
            Vector3 desired = Vector3.ProjectOnPlane(
                desiredDirection,
                up).normalized;
            Vector3 futureForward = Vector3.ProjectOnPlane(
                targetRouteForward,
                up).normalized;
            float pursuitAngle = forward.sqrMagnitude > 0.0001f &&
                                 desired.sqrMagnitude > 0.0001f
                ? Mathf.Abs(Vector3.SignedAngle(forward, desired, up))
                : 0f;
            float futureHeadingAngle = forward.sqrMagnitude > 0.0001f &&
                                       futureForward.sqrMagnitude > 0.0001f
                ? Mathf.Abs(Vector3.SignedAngle(
                    forward,
                    futureForward,
                    up))
                : 0f;
            float turnAngle = Mathf.Max(pursuitAngle, futureHeadingAngle);
            float curveReduction = Mathf.Lerp(
                1f,
                0.68f,
                Mathf.Clamp01((turnAngle - 5f) / 42f));
            float lateralAcceleration = roadBehaviorProfile switch
            {
                StoryTrafficRoadBehaviorProfile.Perajarvi => 5.25f,
                StoryTrafficRoadBehaviorProfile.RoadRace => 7.5f,
                StoryTrafficRoadBehaviorProfile.Bus => 3.25f,
                _ => 4.5f,
            };
            float minimumCurveSpeed = roadBehaviorProfile switch
            {
                // Stay above the locked village handbrake-entry threshold so
                // the four donor zones can still produce their small slides.
                StoryTrafficRoadBehaviorProfile.Perajarvi => 10.5f,
                StoryTrafficRoadBehaviorProfile.RoadRace => 14f,
                StoryTrafficRoadBehaviorProfile.Bus => 6.5f,
                _ => 8f,
            };
            float turnRadians = turnAngle * Mathf.Deg2Rad;
            float curvatureSpeed = turnRadians > 0.015f
                ? Mathf.Sqrt(
                    lateralAcceleration *
                    Mathf.Max(
                        2f,
                        Mathf.Max(2f, targetDistanceMeters) /
                        turnRadians))
                : maximumSpeedMetersPerSecond;
            float curveLimitedSpeed = Mathf.Min(
                speed * curveReduction,
                Mathf.Max(minimumCurveSpeed, curvatureSpeed));
            return Mathf.Min(
                routeSpeedCapMetersPerSecond,
                curveLimitedSpeed);
        }

        private float ResolveObstacleProbeDistance(
            Vector3 vehicleForward,
            Vector3 desiredDirection)
        {
            float angle = vehicleForward.sqrMagnitude > 0.0001f &&
                          desiredDirection.sqrMagnitude > 0.0001f
                ? Mathf.Abs(Vector3.Angle(vehicleForward, desiredDirection))
                : 0f;
            float curvedPathScale = Mathf.Lerp(
                1f,
                0.42f,
                Mathf.Clamp01((angle - 8f) / 52f));
            return Mathf.Max(
                obstacleProbeDistanceMeters,
                Mathf.Min(
                    CalculateForwardProbeDistance(),
                    DonorEmergencyObstacleRangeMeters) *
                curvedPathScale);
        }

        private static Vector3 ResolveObstacleProbeDirection(
            Vector3 vehicleForward,
            Vector3 desiredDirection,
            Vector3 up)
        {
            Vector3 from = Vector3.ProjectOnPlane(
                vehicleForward,
                up).normalized;
            Vector3 to = Vector3.ProjectOnPlane(
                desiredDirection,
                up).normalized;
            if (from.sqrMagnitude <= 0.0001f)
            {
                return to;
            }

            if (to.sqrMagnitude <= 0.0001f)
            {
                return from;
            }

            float angle = Vector3.Angle(from, to);
            // A straight cast towards a far pure-pursuit target cuts through
            // the inside of Teimo's U-turn and mistakes pumps/poles for lane
            // blockers. Follow the current swept heading on sharp bends while
            // retaining the desired direction on ordinary road curvature.
            float blend = angle <= 20f
                ? 1f
                : Mathf.Clamp01(20f / angle);
            return Vector3.Slerp(from, to, blend).normalized;
        }

        private void LateUpdate()
        {
            if (motionBackend != null && motionBackend.IsOperational)
            {
                ApplyPhysicalWheelVisuals();
                previousPosition = transform.position;
                hasPreviousPosition = true;
                return;
            }

            Vector3 current = transform.position;
            if (!hasPreviousPosition)
            {
                previousPosition = current;
                hasPreviousPosition = true;
                return;
            }

            float distance = Vector3.Distance(current, previousPosition);
            previousPosition = current;
            if (distance <= 0.0001f)
            {
                return;
            }

            float degrees = distance * wheelDegreesPerMeter;
            Transform[] wheels = wheelTransforms ?? Array.Empty<Transform>();
            EnsureWheelBaseTransforms(wheels);
            for (int index = 0; index < wheels.Length; index++)
            {
                Transform wheel = wheels[index];
                if (wheel != null)
                {
                    wheel.localPosition = wheelBaseLocalPositions[index];
                    wheel.Rotate(Vector3.right, degrees, Space.Self);
                }
            }
        }

        private void CaptureWheelBaseTransforms()
        {
            Transform[] wheels = wheelTransforms ?? Array.Empty<Transform>();
            wheelBaseLocalRotations = new Quaternion[wheels.Length];
            wheelBaseLocalPositions = new Vector3[wheels.Length];
            for (int index = 0; index < wheels.Length; index++)
            {
                wheelBaseLocalRotations[index] = wheels[index] != null
                    ? wheels[index].localRotation
                    : Quaternion.identity;
                wheelBaseLocalPositions[index] = wheels[index] != null
                    ? wheels[index].localPosition
                    : Vector3.zero;
            }
        }

        private void EnsureWheelBaseTransforms(Transform[] wheels)
        {
            if (wheelBaseLocalRotations == null ||
                wheelBaseLocalRotations.Length != wheels.Length ||
                wheelBaseLocalPositions == null ||
                wheelBaseLocalPositions.Length != wheels.Length)
            {
                CaptureWheelBaseTransforms();
            }
        }

        private void ApplyPhysicalWheelVisuals()
        {
            Transform[] wheels = wheelTransforms ?? Array.Empty<Transform>();
            EnsureWheelBaseTransforms(wheels);

            for (int index = 0; index < wheels.Length; index++)
            {
                Transform visual = wheels[index];
                if (visual == null ||
                    !motionBackend.TryGetWheelVisualState(
                        index,
                        out float steeringAngle,
                        out float axleAngle))
                {
                    continue;
                }

                Vector3 suspensionOffset = Vector3.zero;
                if (wheelPoseBackend != null &&
                    !wheelPoseBackend.TryGetWheelSuspensionOffset(
                        index,
                        out suspensionOffset))
                {
                    suspensionOffset = Vector3.zero;
                }

                visual.localPosition = wheelBaseLocalPositions[index] +
                                       suspensionOffset;
                visual.localRotation = wheelBaseLocalRotations[index] *
                    Quaternion.AngleAxis(steeringAngle, Vector3.up) *
                    Quaternion.AngleAxis(axleAngle, Vector3.right);
            }
        }

        private bool TryFindBlockingObstacle(
            Vector3 currentPosition,
            Vector3 direction,
            float distanceMeters,
            out RaycastHit closestHit)
        {
            closestHit = default;
            if (direction.sqrMagnitude <= 0.0001f ||
                obstacleLayerMask == 0)
            {
                return false;
            }

            Vector3 probeUp = currentGroundNormal.sqrMagnitude > 0.5f
                ? currentGroundNormal.normalized
                : Vector3.up;
            // Keep the cast volume clear of the supporting road. Starting a
            // sphere at a fixed 0.65 m height made its lower hemisphere overlap
            // the ground whenever the imported vehicle pivot sat a few
            // centimetres below the road, so NWH cars applied full brakes while
            // otherwise correctly grounded.
            Vector3 castDirection = Vector3.ProjectOnPlane(
                direction,
                probeUp).normalized;
            if (castDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 origin = currentPosition + probeUp *
                (obstacleProbeRadiusMeters + 0.25f);
            Collider confirmedSupport =
                ignoreConfirmedSupportColliderInObstacleProbes
                    ? ResolveConfirmedSupportCollider(
                        currentPosition,
                        probeUp)
                    : null;
            Vector3 probeRight = Vector3.Cross(probeUp, castDirection).normalized;
            float projectedHalfWidth = obstacleProbeRadiusMeters;
            if (routeCollider != null)
            {
                Vector3 extents = routeCollider.bounds.extents;
                projectedHalfWidth =
                    Mathf.Abs(probeRight.x) * extents.x +
                    Mathf.Abs(probeRight.y) * extents.y +
                    Mathf.Abs(probeRight.z) * extents.z;
            }

            float sideOffset = Mathf.Clamp(
                projectedHalfWidth - obstacleProbeRadiusMeters * 0.55f,
                obstacleProbeRadiusMeters * 0.65f,
                1.1f);
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            for (int laneSample = -1; laneSample <= 1; laneSample++)
            {
                if (!TrySphereCastBlockingObstacle(
                        origin + probeRight * (laneSample * sideOffset),
                        probeUp,
                        castDirection,
                        distanceMeters,
                        confirmedSupport,
                        out RaycastHit sampleHit) ||
                    sampleHit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = sampleHit.distance;
                closestHit = sampleHit;
                found = true;
            }

            return found;
        }

        private bool TrySphereCastBlockingObstacle(
            Vector3 origin,
            Vector3 probeUp,
            Vector3 direction,
            float distanceMeters,
            Collider confirmedSupport,
            out RaycastHit closestHit)
        {
            closestHit = default;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                obstacleProbeRadiusMeters,
                direction,
                obstacleHits,
                Mathf.Max(0.1f, distanceMeters),
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = obstacleHits[index];
                Collider candidate = hit.collider;
                if (candidate == null ||
                    candidate == confirmedSupport ||
                    candidate.transform == transform ||
                    candidate.transform.IsChildOf(transform) ||
                    IsAdjacentStoryTrafficLane(
                        candidate,
                        origin,
                        direction,
                        probeUp) ||
                    // Ignore only an actual supporting surface. A sphere cast
                    // can report a strongly upward diagonal normal when it
                    // reaches the lower/front edge of a box; treating that as
                    // road made simple road blocks invisible to traffic.
                    Vector3.Dot(hit.normal, probeUp) > 0.92f ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }

            return found;
        }

        private Collider ResolveConfirmedSupportCollider(
            Vector3 currentPosition,
            Vector3 probeUp)
        {
            if (groundLayerMask == 0)
            {
                return null;
            }

            Vector3 up = probeUp.sqrMagnitude > 0.5f
                ? probeUp.normalized
                : Vector3.up;
            int hitCount = Physics.RaycastNonAlloc(
                currentPosition + up * 2.5f,
                -up,
                supportHits,
                6f,
                groundLayerMask,
                QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = supportHits[index];
                Collider candidate = hit.collider;
                if (candidate == null ||
                    candidate.transform == transform ||
                    candidate.transform.IsChildOf(transform) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = hit.distance;
            }

            return nearest;
        }

        private bool IsAdjacentStoryTrafficLane(
            Collider candidate,
            Vector3 probeOrigin,
            Vector3 travelDirection,
            Vector3 probeUp)
        {
            if (!IsStoryTrafficVehicleObstacle(candidate))
            {
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                travelDirection,
                probeUp).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(probeUp, forward).normalized;
            // Passing-lane clearance probes originate at the hypothetical
            // target lane, not at the current chassis centre. Measuring from
            // the chassis made an oncoming car look "adjacent" and filtered
            // it out precisely while deciding whether to cross the centreline.
            Vector3 separation = candidate.bounds.center - probeOrigin;
            float lateralSeparation = Mathf.Abs(
                Vector3.Dot(separation, right));
            float ownHalfWidth = routeCollider != null
                ? ProjectBoundsExtent(routeCollider.bounds.extents, right)
                : 0.9f;
            float otherHalfWidth = ProjectBoundsExtent(
                candidate.bounds.extents,
                right);

            // The three-ray fan spans the full body width. Without this lane
            // check a racer staggered in the neighbouring lane is classified
            // as a wall even though the two physical envelopes do not overlap.
            // Actual overlap/collision still remains authoritative in PhysX.
            return lateralSeparation >
                   ownHalfWidth + otherHalfWidth + 0.2f;
        }

        private static float ProjectBoundsExtent(
            Vector3 extents,
            Vector3 direction)
        {
            return Mathf.Abs(direction.x) * extents.x +
                   Mathf.Abs(direction.y) * extents.y +
                   Mathf.Abs(direction.z) * extents.z;
        }

        private bool IsStoryTrafficVehicleObstacle(Collider candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            StoryTrafficVehiclePresentationBinding other =
                candidate.GetComponentInParent<
                    StoryTrafficVehiclePresentationBinding>();
            return other != null && other != this;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ReportPhysicalStallDiagnostic(
            float deltaTime,
            float desiredSpeedMetersPerSecond,
            float targetDistanceMeters,
            bool centerLaneBlocked,
            bool maneuverPathBlocked)
        {
            bool expectedToTravel = targetDistanceMeters > 2f &&
                                    hasRouteTarget;
            bool stationary = currentSpeedMetersPerSecond < 0.2f;
            bool intentionalHold = socialStopSecondsRemaining > 0f ||
                                   storyIncidentHold ||
                                   routeFullStopHold ||
                                   terminalAbandonmentHold ||
                                   serviceStopHold;
            if (!expectedToTravel || !stationary || intentionalHold)
            {
                stationaryPhysicalSeconds = 0f;
                return;
            }

            stationaryPhysicalSeconds += deltaTime;
            if (stationaryPhysicalSeconds < 2f ||
                Time.unscaledTime < nextPhysicalStallDiagnosticTime)
            {
                return;
            }

            nextPhysicalStallDiagnosticTime = Time.unscaledTime + 5f;
            string obstacleName = trackedObstacle != null
                ? trackedObstacle.name
                : "none";
            string bodyState = routeBody != null
                ? $"velocity={routeBody.linearVelocity}, " +
                  $"velocityMagnitude={routeBody.linearVelocity.magnitude:F2}, " +
                  $"sleeping={routeBody.IsSleeping()}, " +
                  $"kinematic={routeBody.isKinematic}"
                : "body=none";
            Debug.LogWarning(
                "Story traffic physical stall: " +
                $"driver={driverFeatureId}, state={maneuverState}, " +
                $"speed={currentSpeedMetersPerSecond:F2}, " +
                $"desired={desiredSpeedMetersPerSecond:F2}, " +
                $"targetDistance={targetDistanceMeters:F2}, " +
                $"target={routeTargetPosition}, " +
                $"position={transform.position}, " +
                $"forward={transform.forward}, " +
                $"up={transform.up}, " +
                $"braking={IsObstacleBraking}, " +
                $"centerBlocked={centerLaneBlocked}, " +
                $"pathBlocked={maneuverPathBlocked}, " +
                $"obstacle={obstacleName}, " +
                $"grounded={motionBackend.HasGroundContact}, " +
                $"rpm={motionBackend.EngineRpm:F0}, " +
                $"gear={motionBackend.SelectedGear}, {bodyState}.",
                this);
        }
#endif

        private void PromoteReachedRouteTargets(Vector3 currentPosition)
        {
            float arrivalDistance = HasPhysicalMotionBackend
                ? Mathf.Clamp(
                    currentSpeedMetersPerSecond * 0.35f,
                    2f,
                    8f)
                : RouteSampleArrivalDistanceMeters;
            while (maneuverState != StoryTrafficManeuverState.Reversing &&
                   maneuverState != StoryTrafficManeuverState.Crashed &&
                   maneuverState != StoryTrafficManeuverState.Recovering &&
                   pendingRouteTargets.Count > 0 &&
                   Vector3.Distance(
                       currentPosition,
                       ResolveMotionTarget(
                           routeTargetPosition,
                           routeTargetRotation,
                           currentGroundNormal)) <=
                   arrivalDistance)
            {
                RoutePoseTarget next = pendingRouteTargets.Dequeue();
                routeTargetPosition = next.Position;
                routeTargetRotation = next.Rotation;
                currentGroundNormal = next.SurfaceNormal;
            }

            if (pendingRouteTargets.Count == 0)
            {
                hasLastQueuedRoutePosition = false;
            }
        }

        private Vector3 ConformToGround(
            Vector3 position,
            out Vector3 surfaceNormal)
        {
            surfaceNormal = SanitizeRoadSurfaceNormal(
                currentGroundNormal.sqrMagnitude > 0.5f
                    ? currentGroundNormal
                    : Vector3.up);
            if (groundLayerMask == 0)
            {
                return position;
            }

            Vector3 origin = position + Vector3.up * 3f;
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    8f,
                    groundLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                // The importer already raises the fixture inside the wrapper
                // by groundContactCalibrationMeters, placing the lowest wheel
                // renderer at wrapper-local Y=0. Adding it here again caused
                // the complete car to hover by exactly that correction.
                position.y = hit.point.y;
                surfaceNormal = SanitizeRoadSurfaceNormal(hit.normal);
            }

            return position;
        }

        private static Vector3 SanitizeRoadSurfaceNormal(Vector3 normal)
        {
            if (!IsFinite(normal) || normal.sqrMagnitude <= 0.5f)
            {
                return Vector3.up;
            }

            Vector3 normalized = normal.normalized;
            // The temporary donor world contains thin remediation meshes and
            // roadside faces on the same support layers as the carriageway.
            // A downward or near-vertical triangle normal is not a driveable
            // road plane. Aligning a freshly materialized Rigidbody to such a
            // face rotates all NWH suspension casts sideways and lets the car
            // fall through the actual road. Finnish public-road grades in the
            // locked map remain comfortably inside this 45-degree envelope.
            return Vector3.Dot(normalized, Vector3.up) >= 0.7071068f
                ? normalized
                : Vector3.up;
        }

        private static Quaternion AlignToSurface(
            Quaternion rotation,
            Vector3 surfaceNormal)
        {
            Vector3 forward = rotation * Vector3.forward;
            return LookAlongSurface(forward, surfaceNormal);
        }

        private static Quaternion LookAlongSurface(
            Vector3 forward,
            Vector3 surfaceNormal)
        {
            Vector3 up = surfaceNormal.sqrMagnitude > 0.5f
                ? surfaceNormal.normalized
                : Vector3.up;
            Vector3 planarForward = Vector3.ProjectOnPlane(forward, up);
            if (planarForward.sqrMagnitude <= 0.0001f)
            {
                planarForward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            return Quaternion.LookRotation(planarForward.normalized, up);
        }

        private void EnsureRoutePhysics()
        {
            routeBody = GetComponent<Rigidbody>();
            if (routeBody == null)
            {
                routeBody = gameObject.AddComponent<Rigidbody>();
            }

            if (motionBackend != null && motionBackend.IsOperational)
            {
                EnsureRouteCollider();
                return;
            }

            routeBody.isKinematic = true;
            routeBody.useGravity = false;
            routeBody.interpolation = RigidbodyInterpolation.Interpolate;
            routeBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;

            EnsureRouteCollider();
        }

        private void EnsureRouteCollider()
        {
            routeCollider = GetComponent<BoxCollider>();
            if (routeCollider != null)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                worldBounds.Encapsulate(renderers[index].bounds);
            }

            routeCollider = gameObject.AddComponent<BoxCollider>();
            routeCollider.center = transform.InverseTransformPoint(
                worldBounds.center);
            Vector3 scale = transform.lossyScale;
            routeCollider.size = new Vector3(
                worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                worldBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private void SetWorldPose(Vector3 position, Quaternion rotation)
        {
            if (routeBody != null && routeBody.isKinematic)
            {
                routeBody.MovePosition(position);
                routeBody.MoveRotation(rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void SnapWorldPose(Vector3 position, Quaternion rotation)
        {
            SuspendStructuralFailureSensorForPoseChange();
            if (motionBackend != null && motionBackend.IsOperational)
            {
                motionBackend.SnapToPose(
                    position,
                    rotation,
                    currentSpeedMetersPerSecond);
                return;
            }

            if (routeBody != null && routeBody.isKinematic)
            {
                // Initial materialization and genuine streaming discontinuities
                // are authoritative pose changes, not physics-frame motion.
                // MovePosition is intentionally deferred by PhysX and left a
                // newly materialized car at its old coordinates for one tick.
                routeBody.position = position;
                routeBody.rotation = rotation;
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void SuspendStructuralFailureSensorForPoseChange()
        {
            structuralSensorSuspensionFixedSteps = Mathf.Max(
                structuralSensorSuspensionFixedSteps,
                2);
            structuralFailureSensor?.SetSuspended(true);
        }

        private static int LayerBit(int layer) =>
            layer >= 0 ? 1 << layer : 0;

        private void ResolveMotionBackend()
        {
            motionBackend = motionBackendComponent as
                IStoryTrafficVehicleMotionBackend;
            wheelPoseBackend = motionBackendComponent as
                IStoryTrafficWheelPoseBackend;
            terminalMotionControl = motionBackendComponent as
                IStoryTrafficTerminalMotionControl;
            hillDriveAssist = motionBackendComponent as
                IStoryTrafficHillDriveAssist;
            if (motionBackend != null)
            {
                terminalMotionControl?.SetTerminallyDisabled(
                    storyIncidentHold || terminalAbandonmentHold ||
                    routeFullStopHold);
                return;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IStoryTrafficVehicleMotionBackend candidate)
                {
                    motionBackendComponent = component;
                    motionBackend = candidate;
                    wheelPoseBackend = component as
                        IStoryTrafficWheelPoseBackend;
                    terminalMotionControl = component as
                        IStoryTrafficTerminalMotionControl;
                    hillDriveAssist = component as
                        IStoryTrafficHillDriveAssist;
                    terminalMotionControl?.SetTerminallyDisabled(
                        storyIncidentHold || terminalAbandonmentHold ||
                        routeFullStopHold);
                    return;
                }
            }

            wheelPoseBackend = null;
            terminalMotionControl = null;
            hillDriveAssist = null;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);

        private readonly struct RoutePoseTarget
        {
            public RoutePoseTarget(
                Vector3 position,
                Quaternion rotation,
                Vector3 surfaceNormal)
            {
                Position = position;
                Rotation = rotation;
                SurfaceNormal = surfaceNormal;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 SurfaceNormal { get; }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredDriverFeatureId,
            IEnumerable<string> configuredPassengerFeatureIds,
            IEnumerable<GameObject> configuredPassengerPresentationRoots,
            IEnumerable<Transform> configuredWheelTransforms,
            float configuredWheelDegreesPerMeter,
            float configuredGroundContactCalibrationMeters)
        {
            driverFeatureId = configuredDriverFeatureId ?? string.Empty;
            passengerFeatureIds = (configuredPassengerFeatureIds ??
                    Enumerable.Empty<string>())
                .ToArray();
            passengerPresentationRoots =
                (configuredPassengerPresentationRoots ??
                    Enumerable.Empty<GameObject>())
                .ToArray();
            wheelTransforms = (configuredWheelTransforms ??
                    Enumerable.Empty<Transform>())
                .ToArray();
            wheelDegreesPerMeter = configuredWheelDegreesPerMeter;
            groundContactCalibrationMeters =
                configuredGroundContactCalibrationMeters;
        }

        public void ConfigureMotionBackendForAuthoring(
            MonoBehaviour configuredMotionBackend)
        {
            if (configuredMotionBackend != null &&
                configuredMotionBackend is not
                    IStoryTrafficVehicleMotionBackend)
            {
                throw new ArgumentException(
                    "Story-traffic motion backend must implement IStoryTrafficVehicleMotionBackend.",
                    nameof(configuredMotionBackend));
            }

            motionBackendComponent = configuredMotionBackend;
            motionBackend = configuredMotionBackend as
                IStoryTrafficVehicleMotionBackend;
            wheelPoseBackend = configuredMotionBackend as
                IStoryTrafficWheelPoseBackend;
            terminalMotionControl = configuredMotionBackend as
                IStoryTrafficTerminalMotionControl;
            terminalMotionControl?.SetTerminallyDisabled(
                storyIncidentHold || terminalAbandonmentHold ||
                routeFullStopHold);
        }

        public void ConfigureInCarRagdollForAuthoring(
            StoryTrafficInCarRagdollBinding configuredRagdoll)
        {
            if (configuredRagdoll != null &&
                configuredRagdoll.transform != transform &&
                !configuredRagdoll.transform.IsChildOf(transform))
            {
                throw new ArgumentException(
                    "In-car ragdoll must belong to the story-traffic wrapper.",
                    nameof(configuredRagdoll));
            }

            inCarRagdollBinding = configuredRagdoll;
        }
#endif
    }
}
