using System;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    [Serializable]
    public struct RaycastWheelBinding
    {
        [SerializeField] private string wheelId;
        [SerializeField] private Transform suspensionAnchor;

        public RaycastWheelBinding(string id, Transform anchor)
        {
            wheelId = id ?? string.Empty;
            suspensionAnchor = anchor;
        }

        public string WheelId => wheelId;

        public Transform SuspensionAnchor => suspensionAnchor;
    }

    public readonly struct RaycastWheelVisualState
    {
        public RaycastWheelVisualState(
            Vector3 center,
            Quaternion steeringRotation,
            float spinDegrees,
            bool grounded)
        {
            WorldCenter = center;
            WorldSteeringRotation = steeringRotation;
            SpinDegrees = spinDegrees;
            HasContact = grounded;
        }

        public Vector3 WorldCenter { get; }

        public Quaternion WorldSteeringRotation { get; }

        public float SpinDegrees { get; }

        public bool HasContact { get; }
    }

    /// <summary>
    /// Bounded four-wheel raycast backend for the M06 proxy Rigidbody. It samples
    /// contacts once and applies one force set per Unity fixed tick; simulation
    /// substeps never multiply PhysX forces.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class PrototypeRaycastWheelPhysicsBackend : MonoBehaviour, IWheelPhysicsBackend
    {
        private const int RaycastBufferSize = 16;
        private const float MinimumSpeedForSlip = 1f;
        private const float MaximumWheelAngularSpeed = 500f;
        private const float LowSpeedRestThresholdMetersPerSecond = 0.15f;
        private const float LowSpeedRestAngularThresholdRadiansPerSecond = 1f;

        [SerializeField] private Rigidbody chassis;
        [SerializeField] private VehicleSimulationConfig config;
        [SerializeField] private RaycastWheelBinding[] wheels = Array.Empty<RaycastWheelBinding>();
        [SerializeField] private LayerMask contactMask = ~0;

        private readonly RaycastHit[] raycastBuffer = new RaycastHit[RaycastBufferSize];
        private readonly SuspensionSimulation suspension = new SuspensionSimulation();
        private WheelRuntimeState[] runtime = Array.Empty<WheelRuntimeState>();
        private bool configured;
        private bool hasSampleHistory;
        private bool restStabilizationArmed = true;

        public Rigidbody Chassis => chassis;

        public VehicleSimulationConfig Config => config;

        public RaycastWheelBinding[] Wheels => wheels;

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
                    ? Vector3.ProjectOnPlane(chassis.linearVelocity, gravity.normalized).magnitude
                    : chassis.linearVelocity.magnitude;
            }
        }

        public bool IsConfigured => configured;

        private void Awake()
        {
            configured = ValidateConfiguration(logFailure: true);
            EnsureRuntimeStorage();
        }

        public void Configure(
            Rigidbody targetChassis,
            VehicleSimulationConfig simulationConfig,
            RaycastWheelBinding[] bindings)
        {
            chassis = targetChassis;
            config = simulationConfig;
            wheels = bindings ?? Array.Empty<RaycastWheelBinding>();
            configured = ValidateConfiguration(logFailure: false);
            EnsureRuntimeStorage();
            Reset();
        }

        public void SetContactMask(LayerMask mask)
        {
            contactMask = mask;
        }

        public void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination)
        {
            EnsureReady(destination);
            float deltaSeconds = Mathf.Max(0.0001f, fixedDeltaSeconds);
            VehicleDynamicsConfig dynamics = config.Dynamics;
            Vector3 suspensionDirection = -chassis.transform.up;
            float maximumRayDistance = dynamics.SuspensionRestLengthMeters +
                                       dynamics.SuspensionTravelMeters +
                                       dynamics.WheelRadiusMeters;
            float maximumNormalLoad = dynamics.ProvisionalMassKilograms *
                                      Mathf.Abs(Physics.gravity.y) /
                                      Mathf.Max(1, WheelCount) * 3f;

            for (int index = 0; index < wheels.Length; index++)
            {
                WheelRuntimeState state = runtime[index];
                Transform anchor = wheels[index].SuspensionAnchor;
                if (!TryGetContact(
                        anchor.position,
                        suspensionDirection,
                        maximumRayDistance,
                        out RaycastHit hit))
                {
                    state.HasContact = false;
                    state.ContactCollider = null;
                    state.SurfaceProvider = null;
                    state.Surface = VehicleSurfaceType.Unknown;
                    state.NormalLoadNewtons = 0f;
                    state.LongitudinalSlip = 0f;
                    state.LateralSlip = 0f;
                    state.Compression01 = 0f;
                    state.SuspensionLengthMeters = dynamics.SuspensionRestLengthMeters +
                                                   dynamics.SuspensionTravelMeters;
                    runtime[index] = state;
                    destination[index] = WheelPhysicsSample.NoContact;
                    continue;
                }

                float suspensionLength = Mathf.Max(
                    0f,
                    hit.distance - dynamics.WheelRadiusMeters);
                float compressionMeters = Mathf.Clamp(
                    dynamics.SuspensionRestLengthMeters - suspensionLength,
                    0f,
                    dynamics.SuspensionTravelMeters);
                float compression01 = compressionMeters /
                                      Mathf.Max(0.0001f, dynamics.SuspensionTravelMeters);
                float compressionVelocity = hasSampleHistory
                    ? (compression01 - state.Compression01) *
                      dynamics.SuspensionTravelMeters /
                      deltaSeconds
                    : 0f;
                float normalLoad = suspension.CalculateForceNewtons(
                    dynamics,
                    compressionMeters,
                    compressionVelocity);
                normalLoad = Mathf.Clamp(normalLoad, 0f, maximumNormalLoad);

                Quaternion steeringRotation = Quaternion.AngleAxis(
                    state.SteeringAngleDegrees,
                    hit.normal);
                Vector3 wheelForward = Vector3.ProjectOnPlane(
                    steeringRotation * chassis.transform.forward,
                    hit.normal);
                if (wheelForward.sqrMagnitude < 0.0001f)
                {
                    wheelForward = Vector3.ProjectOnPlane(chassis.transform.forward, hit.normal);
                }

                wheelForward.Normalize();
                Vector3 wheelRight = Vector3.Cross(hit.normal, wheelForward).normalized;
                Vector3 pointVelocity = chassis.GetPointVelocity(hit.point);
                float longitudinalSpeed = Vector3.Dot(pointVelocity, wheelForward);
                float lateralSpeed = Vector3.Dot(pointVelocity, wheelRight);
                float wheelSurfaceSpeed = state.AngularSpeedRadiansPerSecond *
                                          dynamics.WheelRadiusMeters;
                float slipDenominator = Mathf.Max(
                    MinimumSpeedForSlip,
                    Mathf.Abs(longitudinalSpeed));

                state.HasContact = true;
                state.ContactPoint = hit.point;
                state.ContactNormal = hit.normal;
                state.WheelForward = wheelForward;
                state.WheelRight = wheelRight;
                state.LongitudinalSpeed = longitudinalSpeed;
                state.LateralSpeed = lateralSpeed;
                state.LongitudinalSlip = (wheelSurfaceSpeed - longitudinalSpeed) /
                                         slipDenominator;
                state.LateralSlip = lateralSpeed / slipDenominator;
                state.NormalLoadNewtons = normalLoad;
                state.Compression01 = compression01;
                state.SuspensionLengthMeters = suspensionLength;
                state.Surface = ResolveSurface(hit.collider, ref state);
                runtime[index] = state;

                destination[index] = new WheelPhysicsSample(
                    true,
                    hit.point,
                    hit.normal,
                    normalLoad,
                    state.LongitudinalSlip,
                    state.LateralSlip,
                    state.AngularSpeedRadiansPerSecond,
                    compression01,
                    state.Surface);
            }

            hasSampleHistory = true;
        }

        public void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands)
        {
            if (commands == null || commands.Length < WheelCount)
            {
                throw new ArgumentException("Wheel command buffer is smaller than the configured wheel count.");
            }

            if (!configured || chassis == null || config == null)
            {
                return;
            }

            bool hasDriveTorque = HasDriveTorque(commands);
            if (hasDriveTorque)
            {
                restStabilizationArmed = false;
                if (chassis.IsSleeping())
                {
                    chassis.WakeUp();
                }
            }
            else if (chassis.IsSleeping())
            {
                UpdateSteeringOnly(commands);
                return;
            }

            float deltaSeconds = Mathf.Max(0.0001f, fixedDeltaSeconds);
            VehicleDynamicsConfig dynamics = config.Dynamics;
            float wheelRadius = dynamics.WheelRadiusMeters;
            float wheelInertia = Mathf.Max(0.001f, dynamics.WheelInertiaKilogramSquareMeters);
            int groundedCount = CountGroundedWheels();

            for (int index = 0; index < wheels.Length; index++)
            {
                WheelRuntimeState state = runtime[index];
                WheelPhysicsCommand command = commands[index];
                state.SteeringAngleDegrees = config.IsSteeredWheel(index)
                    ? command.SteeringAngleDegrees
                    : 0f;

                float motionSign = Mathf.Abs(state.AngularSpeedRadiansPerSecond) > 0.05f
                    ? Mathf.Sign(state.AngularSpeedRadiansPerSecond)
                    : Mathf.Sign(state.LongitudinalSpeed);
                float netWheelTorque = command.DriveTorqueNewtonMeters -
                                       command.BrakeTorqueNewtonMeters * motionSign;

                if (state.HasContact)
                {
                    VehicleSurfaceResponse surface = config.GetSurfaceResponse(state.Surface);
                    float frictionLimit = state.NormalLoadNewtons * surface.FrictionMultiplier;
                    float driveForce = command.DriveTorqueNewtonMeters / wheelRadius;
                    float maximumNonReversingBrakeForce = Mathf.Abs(state.LongitudinalSpeed) *
                                                         dynamics.ProvisionalMassKilograms /
                                                         deltaSeconds /
                                                         Mathf.Max(1, groundedCount);
                    float brakeForce = Mathf.Min(
                        command.BrakeTorqueNewtonMeters / wheelRadius,
                        maximumNonReversingBrakeForce);
                    float rollingForce = state.NormalLoadNewtons *
                                         dynamics.RollingResistanceCoefficient *
                                         surface.RollingResistanceMultiplier;
                    float supportedMassPerWheel = dynamics.ProvisionalMassKilograms /
                                                  Mathf.Max(1, groundedCount);
                    float wheelSurfaceSpeed = state.AngularSpeedRadiansPerSecond * wheelRadius;
                    float relativeSurfaceSpeed = wheelSurfaceSpeed -
                                                 state.LongitudinalSpeed;
                    float requestedSlipForce = state.LongitudinalSlip *
                                               dynamics.LongitudinalStiffness;
                    float inverseCoupledMass = 1f / supportedMassPerWheel +
                                               wheelRadius * wheelRadius / wheelInertia;
                    float maximumNonOvershootingSlipForce =
                        Mathf.Abs(relativeSurfaceSpeed) /
                        Mathf.Max(0.0001f, deltaSeconds * inverseCoupledMass);
                    float slipForce = Mathf.Sign(requestedSlipForce) *
                                      Mathf.Min(
                                          Mathf.Abs(requestedSlipForce),
                                          maximumNonOvershootingSlipForce);
                    float longitudinalForce = driveForce -
                                              motionSign * (brakeForce + rollingForce) +
                                              slipForce;
                    float lateralForce = -state.LateralSpeed * dynamics.LateralStiffness;
                    if (Mathf.Abs(command.DriveTorqueNewtonMeters) < 0.001f &&
                        longitudinalForce * state.LongitudinalSpeed < 0f)
                    {
                        float maximumStoppingForce = Mathf.Abs(state.LongitudinalSpeed) *
                                                     supportedMassPerWheel /
                                                     deltaSeconds;
                        longitudinalForce = Mathf.Sign(longitudinalForce) *
                                            Mathf.Min(
                                                Mathf.Abs(longitudinalForce),
                                                maximumStoppingForce);
                    }

                    float maximumLateralStoppingForce = Mathf.Abs(state.LateralSpeed) *
                                                        supportedMassPerWheel /
                                                        deltaSeconds;
                    lateralForce = Mathf.Sign(lateralForce) *
                                   Mathf.Min(
                                       Mathf.Abs(lateralForce),
                                       maximumLateralStoppingForce);

                    Vector2 tireForces = Vector2.ClampMagnitude(
                        new Vector2(longitudinalForce, lateralForce),
                        frictionLimit);
                    Vector3 totalForce = state.ContactNormal * state.NormalLoadNewtons +
                                         state.WheelForward * tireForces.x +
                                         state.WheelRight * tireForces.y;
                    chassis.AddForceAtPosition(totalForce, state.ContactPoint, ForceMode.Force);
                    netWheelTorque -= tireForces.x * wheelRadius;
                }

                float nextAngularSpeed = state.AngularSpeedRadiansPerSecond +
                                         netWheelTorque / wheelInertia * deltaSeconds;
                if (command.BrakeTorqueNewtonMeters >=
                        Mathf.Abs(command.DriveTorqueNewtonMeters) &&
                    motionSign != 0f && nextAngularSpeed * motionSign < 0f)
                {
                    // A service brake may lock a wheel, but it must not numerically
                    // drive that wheel backwards after crossing zero in one tick.
                    nextAngularSpeed = 0f;
                }

                state.AngularSpeedRadiansPerSecond = nextAngularSpeed;
                if (state.HasContact)
                {
                    float rollingAngularSpeed = state.LongitudinalSpeed / wheelRadius;
                    float relaxation = Mathf.Clamp01(
                        deltaSeconds * 8f * config.GetSurfaceResponse(state.Surface).FrictionMultiplier);
                    state.AngularSpeedRadiansPerSecond = Mathf.Lerp(
                        state.AngularSpeedRadiansPerSecond,
                        rollingAngularSpeed,
                        relaxation);
                }

                state.AngularSpeedRadiansPerSecond = Mathf.Clamp(
                    VehicleSimulationMath.Sanitize(state.AngularSpeedRadiansPerSecond),
                    -MaximumWheelAngularSpeed,
                    MaximumWheelAngularSpeed);
                state.SpinDegrees = Mathf.Repeat(
                    state.SpinDegrees +
                    state.AngularSpeedRadiansPerSecond * Mathf.Rad2Deg * deltaSeconds,
                    360f);
                runtime[index] = state;
            }

            if (!hasDriveTorque && groundedCount >= 2 && restStabilizationArmed)
            {
                ApplyLowSpeedRestStability();
            }
        }

        public void Reset()
        {
            EnsureRuntimeStorage();
            Array.Clear(runtime, 0, runtime.Length);
            hasSampleHistory = false;
            restStabilizationArmed = true;
        }

        public bool TryGetVisualState(int wheelIndex, out RaycastWheelVisualState visualState)
        {
            if (wheelIndex < 0 || wheelIndex >= runtime.Length ||
                wheelIndex >= wheels.Length || wheels[wheelIndex].SuspensionAnchor == null ||
                chassis == null || config == null)
            {
                visualState = default;
                return false;
            }

            WheelRuntimeState state = runtime[wheelIndex];
            Transform anchor = wheels[wheelIndex].SuspensionAnchor;
            Vector3 center = state.HasContact
                ? state.ContactPoint + state.ContactNormal * config.Dynamics.WheelRadiusMeters
                : anchor.position - chassis.transform.up *
                  (config.Dynamics.SuspensionRestLengthMeters +
                   config.Dynamics.SuspensionTravelMeters);
            Quaternion steering = Quaternion.AngleAxis(
                state.SteeringAngleDegrees,
                chassis.transform.up) * chassis.rotation;
            visualState = new RaycastWheelVisualState(
                center,
                steering,
                state.SpinDegrees,
                state.HasContact);
            return true;
        }

        private bool TryGetContact(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out RaycastHit nearestHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                raycastBuffer,
                distance,
                contactMask,
                QueryTriggerInteraction.Ignore);
            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = raycastBuffer[index];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(chassis.transform) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestHit = hit;
                nearestDistance = hit.distance;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        private static VehicleSurfaceType ResolveSurface(
            Collider collider,
            ref WheelRuntimeState state)
        {
            if (collider == null)
            {
                state.ContactCollider = null;
                state.SurfaceProvider = null;
                return VehicleSurfaceType.Unknown;
            }

            if (state.ContactCollider != collider)
            {
                state.ContactCollider = collider;
                state.SurfaceProvider =
                    collider.GetComponentInParent<VehicleSurfaceMetadataAuthoring>();
            }

            return state.SurfaceProvider != null
                ? state.SurfaceProvider.SurfaceType
                : VehicleSurfaceType.Unknown;
        }

        private int CountGroundedWheels()
        {
            int count = 0;
            for (int index = 0; index < runtime.Length; index++)
            {
                if (runtime[index].HasContact)
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplyLowSpeedRestStability()
        {
            Vector3 gravity = Physics.gravity;
            if (!HasNearlyLevelSupport(gravity))
            {
                restStabilizationArmed = false;
                return;
            }

            Vector3 planarVelocity = gravity.sqrMagnitude > 0.0001f
                ? Vector3.ProjectOnPlane(chassis.linearVelocity, gravity.normalized)
                : chassis.linearVelocity;
            if (planarVelocity.magnitude >= LowSpeedRestThresholdMetersPerSecond ||
                Mathf.Abs(Vector3.Dot(
                    chassis.linearVelocity,
                    gravity.sqrMagnitude > 0.0001f ? gravity.normalized : Vector3.up)) >=
                LowSpeedRestThresholdMetersPerSecond ||
                chassis.angularVelocity.magnitude >=
                LowSpeedRestAngularThresholdRadiansPerSecond)
            {
                restStabilizationArmed = false;
                return;
            }

            // PhysX does not need the custom suspension forces while a supported
            // body is asleep. Sleeping here also clears the just-accumulated
            // balanced force set, preventing tiny numerical torque asymmetries
            // from becoming visible start-up creep. Any drive torque or external
            // collision wakes the Rigidbody again.
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            for (int index = 0; index < runtime.Length; index++)
            {
                WheelRuntimeState state = runtime[index];
                state.AngularSpeedRadiansPerSecond = 0f;
                runtime[index] = state;
            }

            chassis.Sleep();
            restStabilizationArmed = false;
        }

        private bool HasNearlyLevelSupport(Vector3 gravity)
        {
            if (gravity.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 supportUp = -gravity.normalized;
            const float minimumLevelDot = 0.99985f; // Approximately one degree.
            for (int index = 0; index < runtime.Length; index++)
            {
                WheelRuntimeState state = runtime[index];
                if (state.HasContact &&
                    Vector3.Dot(state.ContactNormal, supportUp) < minimumLevelDot)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasDriveTorque(WheelPhysicsCommand[] commands)
        {
            for (int index = 0; index < commands.Length; index++)
            {
                if (Mathf.Abs(commands[index].DriveTorqueNewtonMeters) >= 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateSteeringOnly(WheelPhysicsCommand[] commands)
        {
            for (int index = 0; index < runtime.Length; index++)
            {
                WheelRuntimeState state = runtime[index];
                state.SteeringAngleDegrees = config.IsSteeredWheel(index)
                    ? commands[index].SteeringAngleDegrees
                    : 0f;
                runtime[index] = state;
            }
        }

        private void EnsureReady(WheelPhysicsSample[] destination)
        {
            if (!configured || chassis == null || config == null)
            {
                throw new InvalidOperationException("M06 raycast wheel backend is not configured.");
            }

            if (destination == null || destination.Length < WheelCount)
            {
                throw new ArgumentException("Wheel sample buffer is smaller than the configured wheel count.");
            }

            EnsureRuntimeStorage();
        }

        private bool ValidateConfiguration(bool logFailure)
        {
            bool valid = chassis != null && config != null && wheels != null &&
                         wheels.Length == config.WheelCount;
            if (valid)
            {
                for (int index = 0; index < wheels.Length; index++)
                {
                    if (wheels[index].SuspensionAnchor == null ||
                        string.IsNullOrWhiteSpace(wheels[index].WheelId))
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid && logFailure)
            {
                Debug.LogError(
                    "M06 raycast wheel backend requires a chassis, config, and four explicit wheel anchors.",
                    this);
            }

            return valid;
        }

        private void EnsureRuntimeStorage()
        {
            int count = WheelCount;
            if (runtime.Length != count)
            {
                runtime = new WheelRuntimeState[count];
            }
        }

        private struct WheelRuntimeState
        {
            public bool HasContact;
            public Collider ContactCollider;
            public VehicleSurfaceMetadataAuthoring SurfaceProvider;
            public Vector3 ContactPoint;
            public Vector3 ContactNormal;
            public Vector3 WheelForward;
            public Vector3 WheelRight;
            public float LongitudinalSpeed;
            public float LateralSpeed;
            public float LongitudinalSlip;
            public float LateralSlip;
            public float NormalLoadNewtons;
            public float Compression01;
            public float SuspensionLengthMeters;
            public float AngularSpeedRadiansPerSecond;
            public float SteeringAngleDegrees;
            public float SpinDegrees;
            public VehicleSurfaceType Surface;
        }
    }
}
