using System;
using MSC.Vehicle.Simulation;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Adapts NWH WheelController3D contact physics to the project-owned
    /// powertrain boundary. NWH never owns vehicle input, route decisions or
    /// persistent state; it receives per-wheel torque, braking and steering.
    /// </summary>
    // WheelController simulates at execution order 100. Axle-stability forces
    // must consume the contact/compression values produced by that step instead
    // of the previous fixed frame.
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class NwhWheelPhysicsBackend : MonoBehaviour, IWheelPhysicsBackend
    {
        private const float StationaryPlanarSpeedMetersPerSecond = 0.03f;
        private const float StationaryChassisAngularSpeedRadiansPerSecond = 0.05f;
        private const float StationaryWheelAngularSpeedRadiansPerSecond = 0.75f;
        private const float StationaryWheelMinimumLoadNewtons = 1f;
        private const float StationaryWheelMaximumDriveTorqueNewtonMeters = 0.01f;

        [SerializeField] private Rigidbody chassis;
        [SerializeField] private WheelController[] wheels =
            Array.Empty<WheelController>();
        [SerializeField, Min(0f)] private float frontAntiRollForceNewtons;
        [SerializeField, Min(0f)] private float rearAntiRollForceNewtons;
        [SerializeField] private NwhAssemblyWheelSupportController assemblySupport;
        [SerializeField] private SatsumaFrontSteeringController steeringController;

        public Rigidbody Chassis => chassis;

        public WheelController[] Wheels => wheels;

        public float FrontAntiRollForceNewtons => frontAntiRollForceNewtons;

        public float RearAntiRollForceNewtons => rearAntiRollForceNewtons;

        public int WheelCount => wheels?.Length ?? 0;

        public float VehicleSpeedMetersPerSecond
        {
            get
            {
                if (chassis == null)
                {
                    return 0f;
                }

                Vector3 gravity = Physics.gravity;
                return gravity.sqrMagnitude > 0.0001f
                    ? Vector3.ProjectOnPlane(
                        chassis.linearVelocity,
                        gravity.normalized).magnitude
                    : chassis.linearVelocity.magnitude;
            }
        }

        private void Awake()
        {
            if (!TryValidate(out string failure))
            {
                Debug.LogError(
                    "NWH wheel backend configuration is invalid: " + failure,
                    this);
                enabled = false;
                return;
            }

            ConfigureWheelExecution();
        }

        public void Configure(
            Rigidbody targetChassis,
            WheelController[] configuredWheels)
        {
            chassis = targetChassis;
            wheels = configuredWheels ?? Array.Empty<WheelController>();
            if (TryValidate(out _))
            {
                ConfigureWheelExecution();
            }
        }

        public void ConfigureAxleStability(
            float configuredFrontAntiRollForceNewtons,
            float configuredRearAntiRollForceNewtons)
        {
            if (!float.IsFinite(configuredFrontAntiRollForceNewtons) ||
                configuredFrontAntiRollForceNewtons < 0f ||
                !float.IsFinite(configuredRearAntiRollForceNewtons) ||
                configuredRearAntiRollForceNewtons < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredFrontAntiRollForceNewtons));
            }

            frontAntiRollForceNewtons =
                configuredFrontAntiRollForceNewtons;
            rearAntiRollForceNewtons =
                configuredRearAntiRollForceNewtons;
        }

        public void ConfigureAssemblySupport(
            NwhAssemblyWheelSupportController support)
        {
            assemblySupport = support;
        }

        public void ConfigureSteeringController(SatsumaFrontSteeringController controller)
        {
            steeringController = controller;
        }

        public bool TryValidate(out string failure)
        {
            if (chassis == null)
            {
                failure = "Chassis Rigidbody is not assigned.";
                return false;
            }

            if (chassis.isKinematic)
            {
                failure = "Chassis Rigidbody must be dynamic.";
                return false;
            }

            if (wheels == null || wheels.Length == 0)
            {
                failure = "At least one NWH wheel is required.";
                return false;
            }

            if (!float.IsFinite(frontAntiRollForceNewtons) ||
                frontAntiRollForceNewtons < 0f ||
                !float.IsFinite(rearAntiRollForceNewtons) ||
                rearAntiRollForceNewtons < 0f)
            {
                failure = "NWH axle-stability forces are invalid.";
                return false;
            }

            for (int index = 0; index < wheels.Length; index++)
            {
                WheelController wheel = wheels[index];
                if (wheel == null)
                {
                    failure = $"NWH wheel {index} is not assigned.";
                    return false;
                }

                if (!wheel.transform.IsChildOf(chassis.transform))
                {
                    failure = $"NWH wheel '{wheel.name}' is not a child of the chassis.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public void Sample(
            float fixedDeltaSeconds,
            WheelPhysicsSample[] destination)
        {
            EnsureDestination(destination);
            for (int index = 0; index < wheels.Length; index++)
            {
                WheelController wheel = wheels[index];
                if (wheel == null || !wheel.enabled ||
                    !wheel.gameObject.activeInHierarchy ||
                    !wheel.IsGrounded)
                {
                    destination[index] = WheelPhysicsSample.NoContact;
                    continue;
                }

                destination[index] = new WheelPhysicsSample(
                    true,
                    wheel.HitPoint,
                    wheel.HitNormal,
                    Mathf.Max(0f, wheel.Load),
                    wheel.LongitudinalSlip,
                    wheel.LateralSlip,
                    wheel.AngularVelocity,
                    Mathf.Clamp01(wheel.SpringCompression),
                    VehicleSurfaceType.Unknown);
            }
        }

        public void Apply(
            float fixedDeltaSeconds,
            WheelPhysicsCommand[] commands)
        {
            if (commands == null || commands.Length < WheelCount)
            {
                throw new ArgumentException(
                    "Wheel command buffer is smaller than the configured NWH wheel count.",
                    nameof(commands));
            }

            for (int index = 0; index < wheels.Length; index++)
            {
                WheelController wheel = wheels[index];
                if (wheel == null || !wheel.enabled ||
                    !wheel.gameObject.activeInHierarchy)
                {
                    continue;
                }

                WheelPhysicsCommand command = commands[index];
                wheel.MotorTorque = command.DriveTorqueNewtonMeters;
                wheel.BrakeTorque = command.BrakeTorqueNewtonMeters;
                wheel.SteerAngle = steeringController != null
                    ? steeringController.ResolveSteerAngle(wheel, command.SteeringAngleDegrees)
                    : command.SteeringAngleDegrees;
                if (Mathf.Abs(command.DriveTorqueNewtonMeters) > 0.01f ||
                    command.BrakeTorqueNewtonMeters > 0.01f)
                {
                    wheel.WakeFromSleep();
                }
            }
        }

        public void Reset()
        {
            if (wheels == null)
            {
                return;
            }

            foreach (WheelController wheel in wheels)
            {
                if (wheel == null)
                {
                    continue;
                }

                wheel.MotorTorque = 0f;
                wheel.BrakeTorque = 0f;
                wheel.SteerAngle = steeringController != null
                    ? steeringController.ResolveSteerAngle(wheel, 0f)
                    : 0f;
                wheel.wheel.angularVelocity = 0f;
                wheel.wheel.prevAngularVelocity = 0f;
                wheel.WakeFromSleep();
            }
        }

        public void ApplyHandbrake(
            int leftRearWheelIndex,
            int rightRearWheelIndex,
            float torqueNewtonMeters)
        {
            float torque = Mathf.Max(0f, torqueNewtonMeters);
            ApplyAdditionalBrake(leftRearWheelIndex, torque);
            if (rightRearWheelIndex != leftRearWheelIndex)
            {
                ApplyAdditionalBrake(rightRearWheelIndex, torque);
            }
        }

        private void ConfigureWheelExecution()
        {
            foreach (WheelController wheel in wheels)
            {
                if (wheel != null)
                {
                    wheel.AutoSimulate = true;
                }
            }
        }

        private void FixedUpdate()
        {
            if (chassis == null || wheels == null || wheels.Length < 4)
            {
                return;
            }

            ApplyAntiRoll(0, 1, frontAntiRollForceNewtons);
            ApplyAntiRoll(2, 3, rearAntiRollForceNewtons);
            ClampGroundedWheelRestState();
        }

        private void ClampGroundedWheelRestState()
        {
            Vector3 gravity = Physics.gravity;
            Vector3 planarVelocity = gravity.sqrMagnitude > 0.0001f
                ? Vector3.ProjectOnPlane(
                    chassis.linearVelocity,
                    gravity.normalized)
                : chassis.linearVelocity;
            float chassisPlanarSpeed = planarVelocity.magnitude;
            float chassisAngularSpeed = chassis.angularVelocity.magnitude;

            for (int index = 0; index < wheels.Length; index++)
            {
                WheelController wheel = wheels[index];
                if (wheel == null || !wheel.enabled ||
                    !wheel.gameObject.activeInHierarchy ||
                    !ShouldClampStationaryWheel(
                        chassisPlanarSpeed,
                        chassisAngularSpeed,
                        wheel.IsGrounded,
                        wheel.Load,
                        wheel.MotorTorque,
                        wheel.wheel.angularVelocity))
                {
                    continue;
                }

                // WheelController's low-speed contact lock holds the contact
                // point, but it deliberately does not latch the wheel's own
                // angular state. At rest that leaves the imported wheel mesh
                // slowly rotating while the chassis is perfectly stationary.
                // Run after NWH's simulation step and clear both integration
                // samples so the next frame cannot resurrect the residual spin.
                wheel.wheel.angularVelocity = 0f;
                wheel.wheel.prevAngularVelocity = 0f;
            }
        }

        private void ApplyAntiRoll(
            int leftIndex,
            int rightIndex,
            float forceNewtons)
        {
            if (forceNewtons <= 0f || leftIndex >= wheels.Length ||
                rightIndex >= wheels.Length)
            {
                return;
            }

            WheelController left = wheels[leftIndex];
            WheelController right = wheels[rightIndex];
            if (left == null || right == null)
            {
                return;
            }

            if (!left.enabled || !right.enabled ||
                !left.gameObject.activeInHierarchy ||
                !right.gameObject.activeInHierarchy)
            {
                return;
            }

            // Incomplete corners now probe the ground too. They must not
            // acquire the previously complete-only axle-stability force merely
            // because the contact solver is running without a physical strut.
            if (assemblySupport != null &&
                (!assemblySupport.AllowsAxleStability(left) ||
                 !assemblySupport.AllowsAxleStability(right)))
            {
                return;
            }

            Vector2 upwardForces = CalculateAntiRollUpwardForces(
                left.IsGrounded,
                left.SpringCompression,
                right.IsGrounded,
                right.SpringCompression,
                forceNewtons);
            Vector3 up = chassis.transform.up;
            if (left.IsGrounded)
            {
                chassis.AddForceAtPosition(
                    up * upwardForces.x,
                    left.HitPoint,
                    ForceMode.Force);
            }

            if (right.IsGrounded)
            {
                chassis.AddForceAtPosition(
                    up * upwardForces.y,
                    right.HitPoint,
                    ForceMode.Force);
            }
        }

        /// <summary>
        /// Returns signed forces along the chassis up axis for the left (x)
        /// and right (y) wheel contact. Positive is upward. The more compressed
        /// side is lifted while the opposite side is pushed down.
        /// </summary>
        internal static Vector2 CalculateAntiRollUpwardForces(
            bool leftGrounded,
            float leftCompression,
            bool rightGrounded,
            float rightCompression,
            float forceNewtons)
        {
            float groundedLeftCompression = leftGrounded
                ? Mathf.Clamp01(leftCompression)
                : 0f;
            float groundedRightCompression = rightGrounded
                ? Mathf.Clamp01(rightCompression)
                : 0f;
            float antiRoll =
                (groundedLeftCompression - groundedRightCompression) *
                Mathf.Max(0f, forceNewtons);

            return new Vector2(
                leftGrounded ? antiRoll : 0f,
                rightGrounded ? -antiRoll : 0f);
        }

        internal static bool ShouldClampStationaryWheel(
            float chassisPlanarSpeedMetersPerSecond,
            float chassisAngularSpeedRadiansPerSecond,
            bool grounded,
            float loadNewtons,
            float driveTorqueNewtonMeters,
            float wheelAngularSpeedRadiansPerSecond)
        {
            return grounded &&
                   float.IsFinite(chassisPlanarSpeedMetersPerSecond) &&
                   chassisPlanarSpeedMetersPerSecond <=
                   StationaryPlanarSpeedMetersPerSecond &&
                   float.IsFinite(chassisAngularSpeedRadiansPerSecond) &&
                   chassisAngularSpeedRadiansPerSecond <=
                   StationaryChassisAngularSpeedRadiansPerSecond &&
                   float.IsFinite(loadNewtons) &&
                   loadNewtons >= StationaryWheelMinimumLoadNewtons &&
                   float.IsFinite(driveTorqueNewtonMeters) &&
                   Mathf.Abs(driveTorqueNewtonMeters) <=
                   StationaryWheelMaximumDriveTorqueNewtonMeters &&
                   float.IsFinite(wheelAngularSpeedRadiansPerSecond) &&
                   Mathf.Abs(wheelAngularSpeedRadiansPerSecond) <=
                   StationaryWheelAngularSpeedRadiansPerSecond;
        }


        private void ApplyAdditionalBrake(int wheelIndex, float torque)
        {
            if (wheels == null || wheelIndex < 0 ||
                wheelIndex >= wheels.Length || wheels[wheelIndex] == null)
            {
                return;
            }

            WheelController wheel = wheels[wheelIndex];
            wheel.BrakeTorque = Mathf.Max(wheel.BrakeTorque, torque);
            wheel.WakeFromSleep();
        }

        private void EnsureDestination(WheelPhysicsSample[] destination)
        {
            if (destination == null || destination.Length < WheelCount)
            {
                throw new ArgumentException(
                    "Wheel sample buffer is smaller than the configured NWH wheel count.",
                    nameof(destination));
            }
        }
    }
}
