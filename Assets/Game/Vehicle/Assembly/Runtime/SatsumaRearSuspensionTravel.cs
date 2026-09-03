using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Maps the frozen donor's rear wheel travel to the existing arm hinge.
    /// This is an IK-derived angular envelope, not donor HingeJoint limits:
    /// GAME SimpleIKSolver 104501/106870 aims the arm's local -Z segment at
    /// the spindle target, with no explicit angle restrictions. Wheel.cs sets
    /// the target height from compression minus suspension travel.
    /// </summary>
    public static class SatsumaRearSuspensionTravel
    {
        // Frozen GAME Suspension FSM 108170: Not installed 5/6, Stock 5/6,
        // Long1/2. Parent transforms 59720/41159 supply the rear target Z;
        // target transforms 63031/36429 offset only X toward the arm pivot.
        // SI metres; the sub-micrometre RL/RR Z difference is normalized here.
        public const float DonorWheelTargetZ = -1.167f;
        public const float NoSpringWheelRootY = -0.15f;
        public const float StockWheelRootY = -0.165f;
        public const float LongWheelRootY = -0.18f;
        public const float StockSuspensionTravel = 0.14f;
        public const float LongSuspensionTravel = 0.17f;

        /// <summary>
        /// Returns (full-droop minimum, full-compression maximum), in degrees
        /// about chassis +X from the donor's neutral arm orientation. The
        /// physical arm keeps its accepted radius; the donor IK aims toward
        /// a target rather than forcing that radius onto the wheel centre.
        /// </summary>
        public static Vector2 ResolveArmLimits(Vector3 armPivotCarLocalPosition,
            bool hasStockSpring, bool hasLongSpring)
        {
            float longitudinalSpan = armPivotCarLocalPosition.z - DonorWheelTargetZ;
            if (!IsFinite(armPivotCarLocalPosition.x) ||
                !IsFinite(armPivotCarLocalPosition.y) ||
                !IsFinite(armPivotCarLocalPosition.z) || longitudinalSpan <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(armPivotCarLocalPosition),
                    armPivotCarLocalPosition,
                    "The rear arm pivot must be finite and ahead of its donor wheel target.");
            }

            // Match the existing force controller's long-spring priority.
            // With neither spring installed, use the explicit no-spring profile.
            float wheelRootY = hasLongSpring ? LongWheelRootY :
                hasStockSpring ? StockWheelRootY : NoSpringWheelRootY;
            float travel = hasLongSpring ? LongSuspensionTravel : StockSuspensionTravel;
            float fullCompressionY = wheelRootY - armPivotCarLocalPosition.y;
            float fullDroopY = fullCompressionY - travel;
            return new Vector2(
                Mathf.Atan2(fullDroopY, longitudinalSpan) * Mathf.Rad2Deg,
                Mathf.Atan2(fullCompressionY, longitudinalSpan) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Converts donor wheel compression in metres into the matching visible
        /// trailing-arm angle. The donor Wheel owns the vertical contact solve;
        /// SimpleIKSolver only aims the arm at that resulting target.
        /// </summary>
        public static float ResolveArmAngle(
            Vector3 armPivotCarLocalPosition,
            float compressionMeters,
            bool hasStockSpring,
            bool hasLongSpring)
        {
            if (!IsFinite(compressionMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(compressionMeters),
                    compressionMeters, "Rear wheel compression must be finite.");
            }

            float longitudinalSpan = armPivotCarLocalPosition.z -
                DonorWheelTargetZ;
            ResolveArmLimits(
                armPivotCarLocalPosition,
                hasStockSpring,
                hasLongSpring);
            float travel = hasLongSpring
                ? LongSuspensionTravel
                : StockSuspensionTravel;
            float wheelRootY = hasLongSpring
                ? LongWheelRootY
                : hasStockSpring
                    ? StockWheelRootY
                    : NoSpringWheelRootY;
            float targetY = wheelRootY - travel +
                Mathf.Clamp(compressionMeters, 0f, travel);
            return Mathf.Atan2(
                targetY - armPivotCarLocalPosition.y,
                longitudinalSpan) * Mathf.Rad2Deg;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
