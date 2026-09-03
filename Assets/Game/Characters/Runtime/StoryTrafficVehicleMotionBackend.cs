using UnityEngine;

namespace MSC.Characters
{
    /// <summary>
    /// Project-owned command passed from story/route authority to a replaceable
    /// physical vehicle backend. The command describes intent; it never grants
    /// the backend ownership of schedules, routes or persistence.
    /// </summary>
    public readonly struct StoryTrafficVehicleDriveCommand
    {
        public StoryTrafficVehicleDriveCommand(
            Vector3 targetPosition,
            Vector3 travelDirection,
            float desiredSpeedMetersPerSecond,
            bool reverse,
            bool fullBrake,
            bool handbrake)
        {
            TargetPosition = targetPosition;
            TravelDirection = travelDirection.sqrMagnitude > 0.0001f
                ? travelDirection.normalized
                : Vector3.forward;
            DesiredSpeedMetersPerSecond = Mathf.Max(
                0f,
                desiredSpeedMetersPerSecond);
            Reverse = reverse;
            FullBrake = fullBrake;
            Handbrake = handbrake;
        }

        public Vector3 TargetPosition { get; }
        public Vector3 TravelDirection { get; }
        public float DesiredSpeedMetersPerSecond { get; }
        public bool Reverse { get; }
        public bool FullBrake { get; }
        public bool Handbrake { get; }

        public static StoryTrafficVehicleDriveCommand Stop(
            Vector3 position,
            Vector3 forward) =>
            new StoryTrafficVehicleDriveCommand(
                position,
                forward,
                0f,
                reverse: false,
                fullBrake: true,
                handbrake: false);
    }

    /// <summary>
    /// Narrow runtime boundary between the story-traffic presenter and a
    /// physical vehicle implementation such as NWH Vehicle Physics 2.
    /// </summary>
    public interface IStoryTrafficVehicleMotionBackend
    {
        bool IsOperational { get; }
        float SpeedMetersPerSecond { get; }
        Vector3 VelocityMetersPerSecond { get; }
        bool HasGroundContact { get; }
        float EngineRpm { get; }
        float EngineRedlineRpm { get; }
        float EngineLoad01 { get; }
        int SelectedGear { get; }

        void Step(
            float fixedDeltaSeconds,
            in StoryTrafficVehicleDriveCommand command);

        void SnapToPose(
            Vector3 position,
            Quaternion rotation,
            float forwardSpeedMetersPerSecond);

        void ResetMotion();

        bool TryGetWheelVisualState(
            int wheelIndex,
            out float steeringAngleDegrees,
            out float axleAngleDegrees);
    }

    /// <summary>
    /// Optional presentation extension for physical backends that can expose
    /// suspension travel. The offset is expressed in the vehicle/presentation
    /// root's local space and is relative to the authored wheel rest centre.
    /// </summary>
    public interface IStoryTrafficWheelPoseBackend
    {
        bool TryGetWheelSuspensionOffset(
            int wheelIndex,
            out Vector3 localPositionOffset);
    }

    /// <summary>
    /// Optional terminal-incident capability. A terminally disabled vehicle
    /// must keep ignition and starter off while retaining physical braking;
    /// ordinary recoverable collision states do not use this boundary.
    /// </summary>
    public interface IStoryTrafficTerminalMotionControl
    {
        bool IsTerminallyDisabled { get; }

        void SetTerminallyDisabled(bool disabled);
    }

    /// <summary>
    /// Optional heavy-vehicle capability. Route authority may enable it on a
    /// known steep corridor; the backend remains responsible for obtaining
    /// wheel torque through its real gearbox and clutch rather than applying
    /// an artificial force or changing the vehicle pose.
    /// </summary>
    public interface IStoryTrafficHillDriveAssist
    {
        bool IsHillDriveAssistActive { get; }

        void SetHillDriveAssist(bool active);
    }
}
