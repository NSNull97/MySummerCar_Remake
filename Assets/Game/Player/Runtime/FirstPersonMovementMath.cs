using UnityEngine;

namespace MSC.Player
{
    /// <summary>
    /// Frame-rate-aware calculations shared by the first-person motor and tests.
    /// </summary>
    public static class FirstPersonMovementMath
    {
        private const float MinimumInput = 0.0001f;

        public static float ResolveDirectionalSpeed(
            Vector2 input,
            float forwardSpeed,
            float backwardSpeed,
            float strafeSpeed)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            if (clampedInput.sqrMagnitude <= MinimumInput)
            {
                return 0f;
            }

            float forwardWeight = Mathf.Abs(clampedInput.y);
            float strafeWeight = Mathf.Abs(clampedInput.x);
            float longitudinalSpeed = clampedInput.y >= 0f
                ? Mathf.Max(0f, forwardSpeed)
                : Mathf.Max(0f, backwardSpeed);
            float directionBlend = strafeWeight /
                Mathf.Max(MinimumInput, forwardWeight + strafeWeight);

            return Mathf.Lerp(
                longitudinalSpeed,
                Mathf.Max(0f, strafeSpeed),
                directionBlend);
        }

        public static Vector3 ApplyPerFrameResponse(
            Vector3 currentVelocity,
            Vector3 targetVelocity,
            float responsePerSecond,
            float deltaTime)
        {
            float response = Mathf.Clamp01(
                Mathf.Max(0f, responsePerSecond) * Mathf.Max(0f, deltaTime));
            return Vector3.Lerp(currentVelocity, targetVelocity, response);
        }

        public static bool CanEnterRun(
            Vector2 input,
            float horizontalSpeed,
            float minimumEntrySpeed,
            float maximumStrafeInput)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            return clampedInput.y > 0.01f &&
                Mathf.Abs(clampedInput.x) <= Mathf.Clamp01(maximumStrafeInput) &&
                horizontalSpeed > Mathf.Max(0f, minimumEntrySpeed);
        }

        public static bool CanUseJump(
            bool grounded,
            bool jumpAvailableSinceGroundContact,
            float secondsSinceGrounded,
            float coyoteTimeSeconds)
        {
            return jumpAvailableSinceGroundContact &&
                (grounded ||
                    Mathf.Max(0f, secondsSinceGrounded) <=
                    Mathf.Max(0f, coyoteTimeSeconds));
        }
    }
}
