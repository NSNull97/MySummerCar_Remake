using System;
using MSC.Characters;
using MSC.Vehicle.Simulation;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Converts project-owned story-traffic drive intent into inputs for the
    /// existing project powertrain, whose per-wheel commands are applied by
    /// NWH. It intentionally does not own the route or write transform poses
    /// during ordinary driving.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NwhStoryTrafficVehicleMotionBackend : MonoBehaviour,
        IStoryTrafficVehicleMotionBackend,
        IStoryTrafficWheelPoseBackend,
        IStoryTrafficTerminalMotionControl,
        IStoryTrafficHillDriveAssist
    {
        private const float MinimumForwardTargetDistanceMeters = 0.5f;
        private const float HillMinimumThrottle01 = 0.62f;
        private const float HillLaunchClutchPedal01 = 0.5f;

        [SerializeField] private Rigidbody chassis;
        [SerializeField] private NwhWheelPhysicsBackend wheelBackend;
        [SerializeField] private VehicleSimulationConfig simulationConfig;
        [SerializeField, Range(0.1f, 2f)] private float speedResponseGain = 0.32f;
        [SerializeField, Range(0f, 1f)] private float rollingThrottle01 = 0.12f;
        [SerializeField, Range(0.1f, 1f)] private float launchClutchPedal01 = 0.58f;
        [SerializeField, Min(0.1f)] private float launchClutchReleaseSpeedMetersPerSecond = 5f;
        [SerializeField, Min(0.1f)] private float gearChangeCooldownSeconds = 0.25f;
        [SerializeField, Min(0.02f)] private float gearChangeClutchSeconds = 0.12f;
        [SerializeField, Min(1000f)] private float upshiftRpm = 6900f;
        [SerializeField, Min(500f)] private float downshiftRpm = 2500f;
        [SerializeField, Min(0.1f)] private float steeringLookAheadMeters = 4f;
        [SerializeField, Min(0.1f)]
        private float maximumDepenetrationVelocityMetersPerSecond = 3f;
        [SerializeField, Min(1f)]
        private float maximumChassisVelocityMetersPerSecond = 70f;
        [Header("Handbrake drift")]
        [SerializeField, Range(0.2f, 1f)]
        private float handbrakeRearLateralGripScale = 0.46f;
        [SerializeField, Min(0.1f)]
        private float handbrakeGripReleaseRatePerSecond = 12f;
        [SerializeField, Min(0.1f)]
        private float handbrakeGripRestoreRatePerSecond = 5f;

        private VehicleSimulationRoot simulation;
        private float gearChangeCooldown;
        private float gearChangeClutchRemaining;
        private bool initializationFailed;
        private bool terminallyDisabled;
        private bool hillDriveAssistActive;
        private bool drivenWheelGripCaptured;
        private float leftDrivenWheelAuthoredLateralGrip;
        private float rightDrivenWheelAuthoredLateralGrip;
        private float drivenWheelLateralGripScale = 1f;

        public bool IsOperational =>
            enabled && !initializationFailed && simulation != null &&
            chassis != null && wheelBackend != null;

        public float SpeedMetersPerSecond => wheelBackend != null
            ? wheelBackend.VehicleSpeedMetersPerSecond
            : 0f;

        public Vector3 VelocityMetersPerSecond => chassis != null
            ? chassis.linearVelocity
            : Vector3.zero;

        public bool HasGroundContact
        {
            get
            {
                if (wheelBackend?.Wheels == null)
                {
                    return false;
                }

                foreach (WheelController wheel in wheelBackend.Wheels)
                {
                    if (wheel != null && wheel.IsGrounded)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public float EngineRpm => simulation?.State.EngineRpm ?? 0f;
        public float EngineRedlineRpm => simulationConfig != null
            ? simulationConfig.Engine.RedlineRpm
            : 0f;
        public float EngineLoad01 => simulation?.State.EngineLoad01 ?? 0f;
        public int SelectedGear => simulation?.State.SelectedGear ?? 0;
        public bool IsTerminallyDisabled => terminallyDisabled;
        public bool IsHillDriveAssistActive => hillDriveAssistActive;

        private void Awake()
        {
            ApplyChassisSafetyLimits();
            TryInitialize(logFailure: true);
        }

        private void OnDisable()
        {
            RestoreDrivenWheelLateralGrip();
        }

        public void Configure(
            Rigidbody configuredChassis,
            NwhWheelPhysicsBackend configuredWheelBackend,
            VehicleSimulationConfig configuredSimulationConfig)
        {
            RestoreDrivenWheelLateralGrip();
            chassis = configuredChassis;
            wheelBackend = configuredWheelBackend;
            simulationConfig = configuredSimulationConfig;
            simulation = null;
            initializationFailed = false;
            terminallyDisabled = false;
            hillDriveAssistActive = false;
            drivenWheelGripCaptured = false;
            drivenWheelLateralGripScale = 1f;
            ApplyChassisSafetyLimits();
            TryInitialize(logFailure: false);
        }

        public void SetHillDriveAssist(bool active)
        {
            hillDriveAssistActive = active;
        }

        public void ConfigureChassisSafetyLimits(
            float configuredMaximumDepenetrationVelocityMetersPerSecond,
            float configuredMaximumChassisVelocityMetersPerSecond)
        {
            if (!float.IsFinite(
                    configuredMaximumDepenetrationVelocityMetersPerSecond) ||
                configuredMaximumDepenetrationVelocityMetersPerSecond <= 0f ||
                !float.IsFinite(
                    configuredMaximumChassisVelocityMetersPerSecond) ||
                configuredMaximumChassisVelocityMetersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredMaximumDepenetrationVelocityMetersPerSecond));
            }

            maximumDepenetrationVelocityMetersPerSecond =
                configuredMaximumDepenetrationVelocityMetersPerSecond;
            maximumChassisVelocityMetersPerSecond =
                configuredMaximumChassisVelocityMetersPerSecond;
            ApplyChassisSafetyLimits();
        }

        public void ConfigureTrafficControlTuning(
            float configuredSpeedResponseGain,
            float configuredRollingThrottle01,
            float configuredLaunchClutchPedal01,
            float configuredLaunchClutchReleaseSpeedMetersPerSecond,
            float configuredUpshiftRpm,
            float configuredDownshiftRpm,
            float configuredSteeringLookAheadMeters)
        {
            if (!float.IsFinite(configuredSpeedResponseGain) ||
                configuredSpeedResponseGain <= 0f ||
                !float.IsFinite(configuredRollingThrottle01) ||
                configuredRollingThrottle01 < 0f ||
                configuredRollingThrottle01 > 1f ||
                !float.IsFinite(configuredLaunchClutchPedal01) ||
                configuredLaunchClutchPedal01 < 0f ||
                configuredLaunchClutchPedal01 > 1f ||
                !float.IsFinite(
                    configuredLaunchClutchReleaseSpeedMetersPerSecond) ||
                configuredLaunchClutchReleaseSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(configuredUpshiftRpm) ||
                configuredUpshiftRpm < 1000f ||
                !float.IsFinite(configuredDownshiftRpm) ||
                configuredDownshiftRpm < 500f ||
                configuredDownshiftRpm >= configuredUpshiftRpm ||
                !float.IsFinite(configuredSteeringLookAheadMeters) ||
                configuredSteeringLookAheadMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredSpeedResponseGain));
            }

            speedResponseGain = configuredSpeedResponseGain;
            rollingThrottle01 = configuredRollingThrottle01;
            launchClutchPedal01 = configuredLaunchClutchPedal01;
            launchClutchReleaseSpeedMetersPerSecond =
                configuredLaunchClutchReleaseSpeedMetersPerSecond;
            upshiftRpm = configuredUpshiftRpm;
            downshiftRpm = configuredDownshiftRpm;
            steeringLookAheadMeters = configuredSteeringLookAheadMeters;
        }

        public bool TryValidate(out string failure)
        {
            if (chassis == null)
            {
                failure = "Chassis Rigidbody is not assigned.";
                return false;
            }

            if (chassis.isKinematic || !chassis.useGravity)
            {
                failure = "Story-traffic chassis must be dynamic and use gravity.";
                return false;
            }

            if (wheelBackend == null)
            {
                failure = "NWH wheel backend is not assigned.";
                return false;
            }

            if (!wheelBackend.TryValidate(out string backendFailure))
            {
                failure = "NWH wheel backend is invalid: " + backendFailure;
                return false;
            }

            if (simulationConfig == null)
            {
                failure = "Vehicle simulation configuration is not assigned.";
                return false;
            }

            if (!simulationConfig.Validate(out string configFailure))
            {
                failure = "Vehicle simulation configuration is invalid: " +
                          configFailure;
                return false;
            }

            if (simulationConfig.WheelCount != wheelBackend.WheelCount)
            {
                failure = "Simulation configuration and NWH wheel counts differ.";
                return false;
            }

            if (!float.IsFinite(
                    maximumDepenetrationVelocityMetersPerSecond) ||
                maximumDepenetrationVelocityMetersPerSecond <= 0f ||
                !float.IsFinite(maximumChassisVelocityMetersPerSecond) ||
                maximumChassisVelocityMetersPerSecond <= 0f)
            {
                failure = "Chassis runtime safety limits are invalid.";
                return false;
            }

            if (!float.IsFinite(handbrakeRearLateralGripScale) ||
                handbrakeRearLateralGripScale < 0.2f ||
                handbrakeRearLateralGripScale > 1f ||
                !float.IsFinite(handbrakeGripReleaseRatePerSecond) ||
                handbrakeGripReleaseRatePerSecond <= 0f ||
                !float.IsFinite(handbrakeGripRestoreRatePerSecond) ||
                handbrakeGripRestoreRatePerSecond <= 0f)
            {
                failure = "Handbrake drift tire tuning is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public void Step(
            float fixedDeltaSeconds,
            in StoryTrafficVehicleDriveCommand command)
        {
            if (!(fixedDeltaSeconds > 0f) || !float.IsFinite(fixedDeltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
            }

            if (!TryInitialize(logFailure: true))
            {
                return;
            }

            if (terminallyDisabled)
            {
                UpdateDrivenWheelLateralGrip(
                    handbrakeRequested: false,
                    fixedDeltaSeconds);
                VehicleSimulationState terminalState = simulation.State;
                simulation.Tick(
                    fixedDeltaSeconds,
                    new VehicleInputState(
                        throttle01: 0f,
                        clutchPedal01: 1f,
                        brake01: 0.8f,
                        steeringMinusOneToOne: 0f,
                        ignitionOn: false,
                        starterRequested: false,
                        gearChangeRequested:
                            terminalState.SelectedGear != 1,
                        requestedGear: 1));
                return;
            }

            gearChangeCooldown = Mathf.Max(
                0f,
                gearChangeCooldown - fixedDeltaSeconds);
            gearChangeClutchRemaining = Mathf.Max(
                0f,
                gearChangeClutchRemaining - fixedDeltaSeconds);

            float steering = CalculateSteeringInput(command);
            VehicleSimulationState state = simulation.State;
            bool engineRunning = state.EngineStatus == VehicleEngineStatus.Running;
            if (!engineRunning)
            {
                UpdateDrivenWheelLateralGrip(
                    handbrakeRequested: false,
                    fixedDeltaSeconds);
                simulation.Tick(
                    fixedDeltaSeconds,
                    new VehicleInputState(
                        throttle01: 0.18f,
                        clutchPedal01: 1f,
                        brake01: command.FullBrake ? 1f : 0f,
                        steeringMinusOneToOne: steering,
                        ignitionOn: true,
                        starterRequested: true,
                        gearChangeRequested: false,
                        requestedGear: 0));
                return;
            }

            int desiredGear = command.Reverse
                ? -1
                : SelectForwardGear(
                    command.DesiredSpeedMetersPerSecond,
                    SpeedMetersPerSecond,
                    simulationConfig.Gearbox.ForwardGearCount,
                    state.SelectedGear,
                    state.EngineRpm,
                    Mathf.Min(
                        upshiftRpm,
                        simulationConfig.Engine.RedlineRpm * 0.9f),
                    Mathf.Min(
                        downshiftRpm,
                        simulationConfig.Engine.RedlineRpm * 0.42f),
                    ResolveMinimumRpmUpshiftSpeed(
                        state.SelectedGear,
                        Mathf.Min(
                            upshiftRpm,
                            simulationConfig.Engine.RedlineRpm * 0.9f)));
            if (hillDriveAssistActive && !command.Reverse)
            {
                desiredGear = SelectHillClimbGear(
                    SpeedMetersPerSecond,
                    simulationConfig.Dynamics.WheelRadiusMeters,
                    simulationConfig.Gearbox,
                    state.SelectedGear,
                    desiredGear,
                    Mathf.Min(
                        downshiftRpm,
                        simulationConfig.Engine.RedlineRpm * 0.42f));
            }

            bool requestGearChange =
                state.SelectedGear != desiredGear &&
                gearChangeCooldown <= 0f &&
                gearChangeClutchRemaining <= 0f;
            if (requestGearChange)
            {
                gearChangeCooldown = gearChangeCooldownSeconds;
                gearChangeClutchRemaining = gearChangeClutchSeconds;
            }

            float speedError = command.DesiredSpeedMetersPerSecond -
                               SpeedMetersPerSecond;
            bool clutchingForShift = requestGearChange ||
                                     gearChangeClutchRemaining > 0f;
            float throttle = command.FullBrake || clutchingForShift
                ? 0f
                : Mathf.Clamp01(
                    speedError * speedResponseGain + rollingThrottle01);
            if (hillDriveAssistActive &&
                !command.Reverse &&
                !command.FullBrake &&
                !clutchingForShift &&
                speedError > 0.2f)
            {
                throttle = Mathf.Max(throttle, HillMinimumThrottle01);
            }

            float brake = command.FullBrake
                ? 1f
                : Mathf.Clamp01(-speedError * speedResponseGain);
            float clutchPedal = clutchingForShift
                ? 1f
                : CalculateLaunchClutchPedal(
                    SpeedMetersPerSecond,
                    command.DesiredSpeedMetersPerSecond);

            simulation.Tick(
                fixedDeltaSeconds,
                new VehicleInputState(
                    throttle,
                    clutchPedal,
                    brake,
                    steering,
                    ignitionOn: true,
                    starterRequested: false,
                    gearChangeRequested: requestGearChange,
                    requestedGear: desiredGear));
            UpdateDrivenWheelLateralGrip(
                command.Handbrake,
                fixedDeltaSeconds);
            if (command.Handbrake)
            {
                wheelBackend.ApplyHandbrake(
                    simulationConfig.LeftDrivenWheelIndex,
                    simulationConfig.RightDrivenWheelIndex,
                    simulationConfig.Dynamics.MaximumHandbrakeTorqueNewtonMeters);
            }
        }

        public void SnapToPose(
            Vector3 position,
            Quaternion rotation,
            float forwardSpeedMetersPerSecond)
        {
            if (chassis == null)
            {
                return;
            }

            chassis.position = position;
            chassis.rotation = rotation;
            chassis.linearVelocity = rotation * Vector3.forward *
                                     Mathf.Max(0f, forwardSpeedMetersPerSecond);
            chassis.angularVelocity = Vector3.zero;
            wheelBackend?.Reset();
            RestoreDrivenWheelLateralGrip();
            chassis.WakeUp();
        }

        public void ResetMotion()
        {
            simulation?.Reset();
            wheelBackend?.Reset();
            RestoreDrivenWheelLateralGrip();
            gearChangeCooldown = 0f;
            gearChangeClutchRemaining = 0f;
            if (chassis != null)
            {
                chassis.linearVelocity = Vector3.zero;
                chassis.angularVelocity = Vector3.zero;
            }
        }

        public void SetTerminallyDisabled(bool disabled)
        {
            terminallyDisabled = disabled;
            gearChangeCooldown = 0f;
            gearChangeClutchRemaining = 0f;
            if (disabled)
            {
                RestoreDrivenWheelLateralGrip();
            }
        }

        private void UpdateDrivenWheelLateralGrip(
            bool handbrakeRequested,
            float fixedDeltaSeconds)
        {
            if (!TryCaptureDrivenWheelLateralGrip(
                    out WheelController leftDrivenWheel,
                    out WheelController rightDrivenWheel))
            {
                return;
            }

            float targetScale = handbrakeRequested
                ? Mathf.Clamp(handbrakeRearLateralGripScale, 0.2f, 1f)
                : 1f;
            float rate = handbrakeRequested
                ? handbrakeGripReleaseRatePerSecond
                : handbrakeGripRestoreRatePerSecond;
            drivenWheelLateralGripScale = Mathf.MoveTowards(
                drivenWheelLateralGripScale,
                targetScale,
                Mathf.Max(0.1f, rate) * fixedDeltaSeconds);
            leftDrivenWheel.sideFriction.grip =
                leftDrivenWheelAuthoredLateralGrip *
                drivenWheelLateralGripScale;
            rightDrivenWheel.sideFriction.grip =
                rightDrivenWheelAuthoredLateralGrip *
                drivenWheelLateralGripScale;
        }

        private bool TryCaptureDrivenWheelLateralGrip(
            out WheelController leftDrivenWheel,
            out WheelController rightDrivenWheel)
        {
            leftDrivenWheel = null;
            rightDrivenWheel = null;
            WheelController[] wheels = wheelBackend?.Wheels;
            if (simulationConfig == null || wheels == null)
            {
                return false;
            }

            int leftIndex = simulationConfig.LeftDrivenWheelIndex;
            int rightIndex = simulationConfig.RightDrivenWheelIndex;
            if (leftIndex < 0 || leftIndex >= wheels.Length ||
                rightIndex < 0 || rightIndex >= wheels.Length ||
                leftIndex == rightIndex ||
                wheels[leftIndex] == null || wheels[rightIndex] == null)
            {
                return false;
            }

            leftDrivenWheel = wheels[leftIndex];
            rightDrivenWheel = wheels[rightIndex];
            if (!drivenWheelGripCaptured)
            {
                leftDrivenWheelAuthoredLateralGrip =
                    leftDrivenWheel.sideFriction.grip;
                rightDrivenWheelAuthoredLateralGrip =
                    rightDrivenWheel.sideFriction.grip;
                drivenWheelGripCaptured = true;
                drivenWheelLateralGripScale = 1f;
            }

            return true;
        }

        private void RestoreDrivenWheelLateralGrip()
        {
            if (!drivenWheelGripCaptured ||
                !TryCaptureDrivenWheelLateralGrip(
                    out WheelController leftDrivenWheel,
                    out WheelController rightDrivenWheel))
            {
                drivenWheelLateralGripScale = 1f;
                return;
            }

            leftDrivenWheel.sideFriction.grip =
                leftDrivenWheelAuthoredLateralGrip;
            rightDrivenWheel.sideFriction.grip =
                rightDrivenWheelAuthoredLateralGrip;
            drivenWheelLateralGripScale = 1f;
        }

        public bool TryGetWheelVisualState(
            int wheelIndex,
            out float steeringAngleDegrees,
            out float axleAngleDegrees)
        {
            WheelController[] wheels = wheelBackend?.Wheels;
            if (wheels == null || wheelIndex < 0 ||
                wheelIndex >= wheels.Length || wheels[wheelIndex] == null)
            {
                steeringAngleDegrees = 0f;
                axleAngleDegrees = 0f;
                return false;
            }

            WheelController wheel = wheels[wheelIndex];
            steeringAngleDegrees = wheel.SteerAngle;
            axleAngleDegrees = wheel.wheel.axleAngle;
            return true;
        }

        public bool TryGetWheelSuspensionOffset(
            int wheelIndex,
            out Vector3 localPositionOffset)
        {
            WheelController[] wheels = wheelBackend?.Wheels;
            if (chassis == null || simulationConfig == null ||
                wheels == null || wheelIndex < 0 ||
                wheelIndex >= wheels.Length || wheels[wheelIndex] == null)
            {
                localPositionOffset = Vector3.zero;
                return false;
            }

            WheelController wheel = wheels[wheelIndex];
            Vector3 localSuspensionUp = chassis.transform
                .InverseTransformDirection(wheel.transform.up);
            localPositionOffset = CalculateSuspensionLocalOffset(
                localSuspensionUp,
                simulationConfig.Dynamics.SuspensionRestLengthMeters,
                wheel.SpringLength);
            return IsFinite(localPositionOffset);
        }

        internal static Vector3 CalculateSuspensionLocalOffset(
            Vector3 localSuspensionUp,
            float restLengthMeters,
            float currentLengthMeters)
        {
            if (!IsFinite(localSuspensionUp) ||
                localSuspensionUp.sqrMagnitude <= 0.0001f ||
                !float.IsFinite(restLengthMeters) ||
                !float.IsFinite(currentLengthMeters))
            {
                return Vector3.zero;
            }

            return localSuspensionUp.normalized *
                   (restLengthMeters - currentLengthMeters);
        }

        internal static int SelectForwardGear(
            float desiredSpeedMetersPerSecond,
            float currentSpeedMetersPerSecond,
            int forwardGearCount)
        {
            int available = Mathf.Max(1, forwardGearCount);
            // Shift from actual wheel speed. Using the requested cruising speed
            // selected second/third gear while stationary, loaded the clutch
            // against a tall ratio and repeatedly stalled the traffic engine.
            float referenceSpeed = Mathf.Max(0f, currentSpeedMetersPerSecond);
            int gear = referenceSpeed switch
            {
                < 11f => 1,
                < 20f => 2,
                < 30f => 3,
                < 41f => 4,
                _ => 5,
            };
            return Mathf.Clamp(gear, 1, available);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        internal static int SelectForwardGear(
            float desiredSpeedMetersPerSecond,
            float currentSpeedMetersPerSecond,
            int forwardGearCount,
            int currentGear,
            float engineRpm,
            float configuredUpshiftRpm,
            float configuredDownshiftRpm,
            float minimumRpmUpshiftSpeedMetersPerSecond = 1.5f)
        {
            int available = Mathf.Max(1, forwardGearCount);
            if (currentGear <= 0 || currentGear > available)
            {
                return 1;
            }

            int speedSuggested = SelectForwardGear(
                desiredSpeedMetersPerSecond,
                currentSpeedMetersPerSecond,
                available);
            float rpm = Mathf.Max(0f, engineRpm);
            float upshift = Mathf.Max(1000f, configuredUpshiftRpm);
            float downshift = Mathf.Clamp(
                configuredDownshiftRpm,
                500f,
                upshift - 250f);

            // The donor controllers shifted from engine landmarks, not from a
            // single road-speed band. Keep changes sequential so a spinning
            // driven axle cannot jump several ratios in one physics tick.
            if (currentGear < available &&
                currentSpeedMetersPerSecond > Mathf.Max(
                    1.5f,
                    minimumRpmUpshiftSpeedMetersPerSecond) &&
                (speedSuggested > currentGear ||
                 rpm >= upshift * 0.82f))
            {
                return currentGear + 1;
            }

            // A shift briefly disconnects the engine from the driven axle, so
            // RPM naturally falls towards idle. That free-rev dip must not
            // immediately undo an otherwise correct road-speed upshift. Only
            // downshift when actual wheel speed asks for a lower ratio.
            if (currentGear > 1 && speedSuggested < currentGear &&
                (rpm <= downshift ||
                 currentSpeedMetersPerSecond < 1.5f))
            {
                return currentGear - 1;
            }

            return currentGear;
        }

        internal static int SelectHillClimbGear(
            float currentSpeedMetersPerSecond,
            float wheelRadiusMeters,
            GearboxSimulationConfig gearbox,
            int currentGear,
            int ordinaryDesiredGear,
            float minimumLoadedRpm)
        {
            if (gearbox == null || gearbox.ForwardGearCount < 1 ||
                !float.IsFinite(currentSpeedMetersPerSecond) ||
                !float.IsFinite(wheelRadiusMeters) ||
                wheelRadiusMeters <= 0f ||
                !float.IsFinite(minimumLoadedRpm) ||
                minimumLoadedRpm <= 0f)
            {
                return Mathf.Max(1, ordinaryDesiredGear);
            }

            int available = gearbox.ForwardGearCount;
            int selected = Mathf.Clamp(currentGear, 1, available);
            if (currentSpeedMetersPerSecond < 0.25f)
            {
                return 1;
            }

            float wheelRadiansPerSecond =
                Mathf.Max(0f, currentSpeedMetersPerSecond) /
                wheelRadiusMeters;
            int tallestLoadedGear = 1;
            for (int gear = 1; gear <= available; gear++)
            {
                if (!gearbox.TryGetRatio(gear, out float ratio))
                {
                    continue;
                }

                float synchronousRpm = wheelRadiansPerSecond *
                    Mathf.Abs(ratio) * gearbox.FinalDriveRatio *
                    VehicleSimulationMath.RadiansPerSecondToRpm;
                if (synchronousRpm >= minimumLoadedRpm)
                {
                    tallestLoadedGear = gear;
                }
            }

            // Preserve the sequential gearbox contract. A bus arriving in
            // fifth at the 9% wastewater climb must step down through fourth
            // rather than jumping directly to the computed loaded ratio.
            if (selected > tallestLoadedGear)
            {
                return selected - 1;
            }

            return Mathf.Clamp(
                Mathf.Min(ordinaryDesiredGear, tallestLoadedGear),
                1,
                available);
        }

        private float ResolveMinimumRpmUpshiftSpeed(
            int currentGear,
            float targetRpm)
        {
            if (simulationConfig == null || currentGear <= 0 ||
                !simulationConfig.Gearbox.TryGetRatio(
                    currentGear,
                    out float gearRatio) ||
                Mathf.Abs(gearRatio) <= 0.0001f)
            {
                return 1.5f;
            }

            float totalRatio = Mathf.Abs(gearRatio) *
                               simulationConfig.Gearbox.FinalDriveRatio;
            float wheelRadiansPerSecond = targetRpm *
                VehicleSimulationMath.RpmToRadiansPerSecond /
                Mathf.Max(0.0001f, totalRatio);
            // Permit the shift once the road speed is reasonably close to the
            // synchronous speed. This still allows clutch slip, but prevents
            // a free-revving heavy vehicle from walking through several gears
            // while it has barely left the stop.
            return wheelRadiansPerSecond *
                   simulationConfig.Dynamics.WheelRadiusMeters * 0.6f;
        }

        private float CalculateSteeringInput(
            in StoryTrafficVehicleDriveCommand command)
        {
            Vector3 gravity = Physics.gravity;
            Vector3 up = gravity.sqrMagnitude > 0.0001f
                ? -gravity.normalized
                : Vector3.up;
            Vector3 vehicleForward = Vector3.ProjectOnPlane(
                chassis.transform.forward,
                up);
            Vector3 steeringReferenceDirection = command.Reverse
                ? -vehicleForward
                : vehicleForward;
            Vector3 desiredDirection = Vector3.ProjectOnPlane(
                command.TravelDirection,
                up);

            Vector3 toTarget = Vector3.ProjectOnPlane(
                command.TargetPosition - chassis.worldCenterOfMass,
                up);
            if (toTarget.sqrMagnitude >=
                MinimumForwardTargetDistanceMeters *
                MinimumForwardTargetDistanceMeters)
            {
                float blend = Mathf.Clamp01(
                    toTarget.magnitude / Mathf.Max(0.1f, steeringLookAheadMeters));
                desiredDirection = Vector3.Slerp(
                    desiredDirection.normalized,
                    toTarget.normalized,
                    blend);
            }

            if (steeringReferenceDirection.sqrMagnitude <= 0.0001f ||
                desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            float angle = Vector3.SignedAngle(
                steeringReferenceDirection.normalized,
                desiredDirection.normalized,
                up);
            float steering = angle / Mathf.Max(
                1f,
                simulationConfig.Dynamics.MaximumSteeringAngleDegrees);
            return Mathf.Clamp(
                command.Reverse ? -steering : steering,
                -1f,
                1f);
        }

        private float CalculateLaunchClutchPedal(
            float speedMetersPerSecond,
            float desiredSpeedMetersPerSecond)
        {
            if (desiredSpeedMetersPerSecond <= 0.05f)
            {
                return 1f;
            }

            float release01 = Mathf.Clamp01(
                speedMetersPerSecond /
                Mathf.Max(0.1f, launchClutchReleaseSpeedMetersPerSecond));
            float initialPedal = hillDriveAssistActive
                ? Mathf.Min(
                    launchClutchPedal01,
                    HillLaunchClutchPedal01)
                : launchClutchPedal01;
            return Mathf.Lerp(initialPedal, 0f, release01);
        }

        private bool TryInitialize(bool logFailure)
        {
            if (simulation != null)
            {
                return true;
            }

            if (initializationFailed)
            {
                return false;
            }

            if (!TryValidate(out string failure))
            {
                initializationFailed = true;
                if (logFailure)
                {
                    Debug.LogError(
                        "NWH story-traffic motion backend is invalid: " + failure,
                        this);
                }

                return false;
            }

            simulation = new VehicleSimulationRoot(
                simulationConfig,
                wheelBackend,
                AlwaysReadyPrerequisites.Instance);
            ApplyChassisSafetyLimits();
            return true;
        }

        private void ApplyChassisSafetyLimits()
        {
            if (chassis == null)
            {
                return;
            }

            // Unity does not serialize these two Rigidbody runtime properties
            // into the generated prefab YAML. Applying them only in the Editor
            // importer therefore left instantiated traffic at PhysX defaults,
            // allowing a road-seam depenetration impulse to launch a chassis
            // and gravity to accelerate it indefinitely after contact was lost.
            chassis.maxDepenetrationVelocity = Mathf.Max(
                0.1f,
                maximumDepenetrationVelocityMetersPerSecond);
            chassis.maxLinearVelocity = Mathf.Max(
                1f,
                maximumChassisVelocityMetersPerSecond);
        }

        private sealed class AlwaysReadyPrerequisites :
            IVehicleSimulationPrerequisiteSource
        {
            public static readonly AlwaysReadyPrerequisites Instance = new();

            public void Evaluate(
                in VehicleInputState input,
                ref VehicleSimulationPrerequisites result)
            {
                if (!input.IgnitionOn)
                {
                    result.Add(VehicleSimulationPrerequisiteFailure.IgnitionOff);
                }
            }
        }
    }
}
