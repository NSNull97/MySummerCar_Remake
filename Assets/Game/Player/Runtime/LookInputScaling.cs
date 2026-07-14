using UnityEngine;

namespace MSC.Player
{
    /// <summary>
    /// Converts pointer deltas and rate-based stick input to rotation degrees using explicit units.
    /// </summary>
    public static class LookInputScaling
    {
        public static Vector2 ToRotationDegrees(
            Vector2 input,
            bool isPointerDelta,
            float pointerDegreesPerPixel,
            float rateDegreesPerSecond,
            float unscaledDeltaTime)
        {
            float scale = isPointerDelta
                ? Mathf.Max(0f, pointerDegreesPerPixel)
                : Mathf.Max(0f, rateDegreesPerSecond) * Mathf.Max(0f, unscaledDeltaTime);
            return input * scale;
        }
    }
}
