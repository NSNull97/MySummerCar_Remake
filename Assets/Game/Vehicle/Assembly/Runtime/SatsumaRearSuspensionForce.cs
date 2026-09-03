using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Wheel-referenced rear suspension force for the frozen donor profile.
    /// GAME Suspension FSM 108170 supplies the rates; Wheel.cs:1074-1084
    /// supplies the spring and signed slow/fast damping curve. The physical
    /// arm's spring-seat leverage is deliberately outside this calculation.
    /// </summary>
    public static class SatsumaRearSuspensionForce
    {
        public const float StockWheelRate = 21200f;
        public const float LongWheelRate = 29000f;
        public const float NoSpringWheelRate = 2f;
        public const float StockShockDamper = 1000f;
        public const float NoShockDamper = 2f;
        public const float FastDamperSpeed = 0.3f;
        public const float FastDamperFactor = 0.3f;

        /// <summary>
        /// Inverts the accepted IK-derived arm angle into donor wheel travel
        /// in metres. This is not the physical hub's vertical arc displacement.
        /// Full droop is zero compression, with no spring-seat preload.
        /// </summary>
        public static float ResolveCompression(Vector3 pivotCarLocal, float angleDegrees,
            bool hasStockSpring, bool hasLongSpring)
        {
            RequireFinite(angleDegrees, nameof(angleDegrees));
            Vector2 limits = SatsumaRearSuspensionTravel.ResolveArmLimits(
                pivotCarLocal, hasStockSpring, hasLongSpring);
            ResolveProfile(hasStockSpring, hasLongSpring, out float rootY, out float travel);

            // Exact end values prevent round-off at atan2/tan inversion from
            // reintroducing a small static force at the accepted droop stop.
            if (angleDegrees <= limits.x)
            {
                return 0f;
            }
            if (angleDegrees >= limits.y)
            {
                return travel;
            }

            float span = pivotCarLocal.z - SatsumaRearSuspensionTravel.DonorWheelTargetZ;
            float targetY = pivotCarLocal.y + span * Mathf.Tan(angleDegrees * Mathf.Deg2Rad);
            return Mathf.Clamp(targetY - (rootY - travel), 0f, travel);
        }

        /// <summary>
        /// Instantaneous donor compression speed in metres/second. Angular
        /// speed is radians/second about chassis +X, positive in compression.
        /// Clamp only the geometry, not velocity: a limit contact still needs
        /// damping during a transient. No frame-difference or timestep state.
        /// </summary>
        public static float ResolveCompressionSpeed(Vector3 pivotCarLocal, float angleDegrees,
            float angularSpeedRadians, bool hasStockSpring, bool hasLongSpring)
        {
            RequireFinite(angleDegrees, nameof(angleDegrees));
            RequireFinite(angularSpeedRadians, nameof(angularSpeedRadians));
            Vector2 limits = SatsumaRearSuspensionTravel.ResolveArmLimits(
                pivotCarLocal, hasStockSpring, hasLongSpring);
            float angleRadians = Mathf.Clamp(angleDegrees, limits.x, limits.y) * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angleRadians);
            float span = pivotCarLocal.z - SatsumaRearSuspensionTravel.DonorWheelTargetZ;
            return span / (cosine * cosine) * angularSpeedRadians;
        }

        /// <summary>
        /// Returns the non-negative donor wheel suspension force in newtons.
        /// Positive speed is bump/compression and negative speed is rebound.
        /// Shock selection is independent of spring selection; damping may
        /// reduce spring force but the donor never pulls the chassis downward.
        /// </summary>
        public static float ResolveWheelForce(float compression, float compressionSpeed,
            bool hasStockSpring, bool hasLongSpring, bool hasShock)
        {
            RequireFinite(compression, nameof(compression));
            RequireFinite(compressionSpeed, nameof(compressionSpeed));
            float wheelRate = hasLongSpring ? LongWheelRate :
                hasStockSpring ? StockWheelRate : NoSpringWheelRate;
            float damper = hasShock ? StockShockDamper : NoShockDamper;
            float speed = Mathf.Abs(compressionSpeed);
            float dampingMagnitude = damper * (Mathf.Min(speed, FastDamperSpeed) +
                Mathf.Max(0f, speed - FastDamperSpeed) * FastDamperFactor);
            float damping = compressionSpeed < 0f ? -dampingMagnitude : dampingMagnitude;
            return Mathf.Max(0f, wheelRate * Mathf.Max(0f, compression) + damping);
        }

        private static void ResolveProfile(bool hasStockSpring, bool hasLongSpring,
            out float rootY, out float travel)
        {
            rootY = hasLongSpring ? SatsumaRearSuspensionTravel.LongWheelRootY :
                hasStockSpring ? SatsumaRearSuspensionTravel.StockWheelRootY :
                SatsumaRearSuspensionTravel.NoSpringWheelRootY;
            travel = hasLongSpring ? SatsumaRearSuspensionTravel.LongSuspensionTravel :
                SatsumaRearSuspensionTravel.StockSuspensionTravel;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value,
                    "Suspension inputs must be finite SI values.");
            }
        }
    }
}
