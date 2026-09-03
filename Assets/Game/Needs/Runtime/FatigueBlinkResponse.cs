using UnityEngine;

namespace MSC.Needs
{
    /// <summary>
    /// Deterministic presentation curve derived from the donor fatigue-eye
    /// cadence. It contains no simulation state and does not depend on donor
    /// animations, textures or runtime code.
    /// </summary>
    public static class FatigueBlinkResponse
    {
        public const float MinimumIntervalSeconds = 20f;
        public const float MaximumIntervalSeconds = 60f;
        public const float BlinkDurationSeconds = 1.05f;
        private const float ClosingSeconds = 0.683f;

        public static float EvaluateClosure(float elapsedSeconds)
        {
            if (!float.IsFinite(elapsedSeconds) ||
                elapsedSeconds < 0f ||
                elapsedSeconds >= BlinkDurationSeconds)
            {
                return 0f;
            }

            if (elapsedSeconds <= ClosingSeconds)
            {
                return Smooth01(elapsedSeconds / ClosingSeconds);
            }

            return 1f - Smooth01(
                (elapsedSeconds - ClosingSeconds) /
                (BlinkDurationSeconds - ClosingSeconds));
        }

        public static float GetIntervalSeconds(uint sequence)
        {
            uint hash = sequence + 0x9E3779B9u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            float normalized = (hash & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(
                MinimumIntervalSeconds,
                MaximumIntervalSeconds,
                normalized);
        }

        private static float Smooth01(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }
    }
}
