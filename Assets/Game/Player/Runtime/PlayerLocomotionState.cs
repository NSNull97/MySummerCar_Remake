using System;

namespace MSC.Player
{
    public enum PlayerPosture
    {
        Standing = 0,
        Crouch = 1,
        DeepCrouch = 2,
    }

    /// <summary>
    /// Project-owned locomotion output consumed by needs, audio, UI and save
    /// adapters. No consumer needs to inspect input actions or donor FSM state.
    /// </summary>
    public readonly struct PlayerLocomotionState : IEquatable<PlayerLocomotionState>
    {
        public PlayerLocomotionState(
            PlayerPosture posture,
            bool running,
            bool grounded,
            bool forwardLeaning,
            float horizontalSpeedMetersPerSecond,
            float verticalSpeedMetersPerSecond)
        {
            if (!IsValidPosture(posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture));
            }

            Posture = posture;
            Running = running;
            Grounded = grounded;
            ForwardLeaning = forwardLeaning;
            HorizontalSpeedMetersPerSecond = horizontalSpeedMetersPerSecond;
            VerticalSpeedMetersPerSecond = verticalSpeedMetersPerSecond;
        }

        public PlayerPosture Posture { get; }
        public bool Running { get; }
        public bool Grounded { get; }
        public bool ForwardLeaning { get; }
        public float HorizontalSpeedMetersPerSecond { get; }
        public float VerticalSpeedMetersPerSecond { get; }

        public static bool IsValidPosture(PlayerPosture posture) =>
            posture >= PlayerPosture.Standing &&
            posture <= PlayerPosture.DeepCrouch;

        public bool Equals(PlayerLocomotionState other) =>
            Posture == other.Posture &&
            Running == other.Running &&
            Grounded == other.Grounded &&
            ForwardLeaning == other.ForwardLeaning &&
            HorizontalSpeedMetersPerSecond.Equals(
                other.HorizontalSpeedMetersPerSecond) &&
            VerticalSpeedMetersPerSecond.Equals(
                other.VerticalSpeedMetersPerSecond);

        public override bool Equals(object obj) =>
            obj is PlayerLocomotionState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                (int)Posture,
                Running,
                Grounded,
                ForwardLeaning,
                HorizontalSpeedMetersPerSecond,
                VerticalSpeedMetersPerSecond);
    }
}
