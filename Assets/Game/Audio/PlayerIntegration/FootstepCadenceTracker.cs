using UnityEngine;

namespace MSC.Audio.PlayerIntegration
{
    /// <summary>
    /// Distance-based cadence driven by observed world displacement. Input
    /// intent is intentionally irrelevant: blocked movement produces no step.
    /// </summary>
    public sealed class FootstepCadenceTracker
    {
        private readonly float walkingStepDistanceMeters;
        private readonly float crouchingStepDistanceMeters;
        private readonly float teleportThresholdMeters;

        private Vector3 lastPosition;
        private bool hasPosition;
        private float accumulatedDistanceMeters;

        public FootstepCadenceTracker(
            float walkingStepDistanceMeters,
            float crouchingStepDistanceMeters,
            float teleportThresholdMeters)
        {
            this.walkingStepDistanceMeters = RequirePositive(
                walkingStepDistanceMeters,
                nameof(walkingStepDistanceMeters));
            this.crouchingStepDistanceMeters = RequirePositive(
                crouchingStepDistanceMeters,
                nameof(crouchingStepDistanceMeters));
            this.teleportThresholdMeters = RequirePositive(
                teleportThresholdMeters,
                nameof(teleportThresholdMeters));
        }

        public float AccumulatedDistanceMeters => accumulatedDistanceMeters;

        public void Reset(Vector3 worldPosition)
        {
            lastPosition = IsFinite(worldPosition) ? worldPosition : Vector3.zero;
            hasPosition = true;
            accumulatedDistanceMeters = 0f;
        }

        public bool Advance(
            Vector3 worldPosition,
            bool isGrounded,
            bool isCrouching)
        {
            if (!IsFinite(worldPosition))
            {
                hasPosition = false;
                accumulatedDistanceMeters = 0f;
                return false;
            }

            if (!hasPosition)
            {
                Reset(worldPosition);
                return false;
            }

            float deltaX = worldPosition.x - lastPosition.x;
            float deltaZ = worldPosition.z - lastPosition.z;
            float planarDistance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            lastPosition = worldPosition;

            if (planarDistance > teleportThresholdMeters)
            {
                accumulatedDistanceMeters = 0f;
                return false;
            }

            if (!isGrounded)
            {
                accumulatedDistanceMeters = 0f;
                return false;
            }

            accumulatedDistanceMeters += planarDistance;
            float requiredDistance = isCrouching
                ? crouchingStepDistanceMeters
                : walkingStepDistanceMeters;
            if (accumulatedDistanceMeters < requiredDistance)
            {
                return false;
            }

            // At most one sound is emitted per rendered frame. Preserve the
            // remainder so a short hitch does not permanently alter cadence.
            accumulatedDistanceMeters %= requiredDistance;
            return true;
        }

        private static float RequirePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
