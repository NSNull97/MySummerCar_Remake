using System;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaRearSuspensionForceTests
    {
        private static readonly Vector3 LeftPivot = new Vector3(-0.42300025f, -0.2153f, -0.8570004f);

        [Test]
        public void ConstantsMatchFrozenSuspensionFsmAndWheelDamping()
        {
            Assert.That(SatsumaRearSuspensionForce.StockWheelRate, Is.EqualTo(21200f));
            Assert.That(SatsumaRearSuspensionForce.LongWheelRate, Is.EqualTo(29000f));
            Assert.That(SatsumaRearSuspensionForce.NoSpringWheelRate, Is.EqualTo(2f));
            Assert.That(SatsumaRearSuspensionForce.StockShockDamper, Is.EqualTo(1000f));
            Assert.That(SatsumaRearSuspensionForce.NoShockDamper, Is.EqualTo(2f));
            Assert.That(SatsumaRearSuspensionForce.FastDamperSpeed, Is.EqualTo(0.3f));
            Assert.That(SatsumaRearSuspensionForce.FastDamperFactor, Is.EqualTo(0.3f));
        }

        [TestCase(false, false, 0.14f)]
        [TestCase(true, false, 0.14f)]
        [TestCase(false, true, 0.17f)]
        public void AcceptedStopsMapToZeroAndFullTravel(bool stock, bool longer, float travel)
        {
            Vector2 limits = SatsumaRearSuspensionTravel.ResolveArmLimits(LeftPivot, stock, longer);
            float droop = SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, limits.x, stock, longer);
            float bump = SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, limits.y, stock, longer);

            Assert.That(droop, Is.Zero);
            Assert.That(bump, Is.EqualTo(travel));
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(droop, 0f, stock, longer, true), Is.Zero);
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(droop, 0f, stock, longer, false), Is.Zero);
            Assert.That(SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, -180f, stock, longer), Is.Zero);
            Assert.That(SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, 180f, stock, longer),
                Is.EqualTo(travel));
        }

        // Independent frozen root Y + compression - travel target positions.
        [TestCase(false, false, 0f, 0.0747f)]
        [TestCase(true, false, 0f, 0.0897f)]
        [TestCase(false, true, 0f, 0.1347f)]
        [TestCase(true, false, 5f, 0.11682145f)]
        [TestCase(true, false, -10f, 0.0350387065f)]
        public void AngleUsesDonorTargetTravelRatherThanHubArc(bool stock, bool longer,
            float angle, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, angle, stock, longer),
                Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void MirroredPivotsHaveTheSameCompressionAndSpeed(bool stock, bool longer)
        {
            var rightPivot = new Vector3(0.42300043f, -0.2153f, -0.8570003f);
            float leftCompression = SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, -5f, stock, longer);
            float rightCompression = SatsumaRearSuspensionForce.ResolveCompression(rightPivot, -5f, stock, longer);
            float leftSpeed = SatsumaRearSuspensionForce.ResolveCompressionSpeed(LeftPivot, -5f, 0.8f, stock, longer);
            float rightSpeed = SatsumaRearSuspensionForce.ResolveCompressionSpeed(rightPivot, -5f, 0.8f, stock, longer);

            Assert.That(rightCompression, Is.EqualTo(leftCompression).Within(0.000001f));
            Assert.That(rightSpeed, Is.EqualTo(leftSpeed).Within(0.000001f));
        }

        [Test]
        public void LongProfileWinsAndRemovalDoesNotRetainPreloadOrTravel()
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, 0f, true, true),
                Is.EqualTo(0.1347f).Within(0.000001f));
            Assert.That(SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, 0f, false, false),
                Is.EqualTo(0.0747f).Within(0.000001f));
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(0.05f, 0f, true, true, false),
                Is.EqualTo(1450f).Within(0.0001f));
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(0.05f, 0f, false, false, false),
                Is.EqualTo(0.1f).Within(0.0001f));
        }

        [TestCase(0f, 2f, 0.6199992f)]
        [TestCase(5f, 1f, 0.31237242f)]
        [TestCase(-10f, -1f, -0.31963786f)]
        [TestCase(-180f, 1f, 0.33595476f)]
        [TestCase(180f, -1f, -0.31816119f)]
        [TestCase(0f, 0f, 0f)]
        public void AngularVelocityMapsToSignedInstantaneousCompressionSpeed(float angle,
            float radiansPerSecond, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveCompressionSpeed(
                    LeftPivot, angle, radiansPerSecond, true, false),
                Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(false, false, -180f, 0.32799991f)]
        [TestCase(false, false, 180f, 0.32375475f)]
        [TestCase(false, true, -180f, 0.368529f)]
        [TestCase(false, true, 180f, 0.31401925f)]
        public void EachProfileKeepsDampingVelocityAtItsLimits(bool stock, bool longer,
            float angle, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveCompressionSpeed(LeftPivot, angle, 1f, stock, longer),
                Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(0.0001f)]
        [TestCase(0.001f)]
        [TestCase(0.01f)]
        public void CompressionSpeedIsIndependentOfThePhysicsTimestep(float timestep)
        {
            // Independent central difference of the donor target equation;
            // the implementation receives angular velocity, never timestep.
            double angle = 5d * Math.PI / 180d;
            const double radiansPerSecond = 0.5d;
            double before = 0.3099996d * Math.Tan(angle - radiansPerSecond * timestep);
            double after = 0.3099996d * Math.Tan(angle + radiansPerSecond * timestep);
            double measuredSpeed = (after - before) / (2d * timestep);
            float actual = SatsumaRearSuspensionForce.ResolveCompressionSpeed(LeftPivot, 5f,
                (float)radiansPerSecond, true, false);

            Assert.That(actual, Is.EqualTo(0.15618621f).Within(0.000001f));
            Assert.That(actual, Is.EqualTo(measuredSpeed).Within(0.00001d));
        }

        [TestCase(true, false, 0.05f, 1060f)]
        [TestCase(true, false, 0.1f, 2120f)]
        [TestCase(false, true, 0.05f, 1450f)]
        [TestCase(false, true, 0.1f, 2900f)]
        [TestCase(false, false, 0.05f, 0.1f)]
        public void StaticForceUsesTheDonorWheelRate(bool stock, bool longer,
            float compression, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(compression, 0f, stock, longer, false),
                Is.EqualTo(expected).Within(0.0001f));
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(compression, 0f, stock, longer, true),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void OneHundredKilogramsAtTheWheelNeedsFortySixMillimetresStockTravel()
        {
            const float compression = 0.046273585f;
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(compression, 0f, true, false, true),
                Is.EqualTo(981f).Within(0.0002f));
        }

        // At 50 mm the static stock term is independently 1060 N. Beyond
        // 0.3 m/s, only the excess speed gets the 0.3 fast-damper factor.
        [TestCase(0.1f, true, 1160f)]
        [TestCase(-0.1f, true, 960f)]
        [TestCase(0.3f, true, 1360f)]
        [TestCase(-0.3f, true, 760f)]
        [TestCase(0.6f, true, 1450f)]
        [TestCase(-0.6f, true, 670f)]
        [TestCase(0.3f, false, 1060.6f)]
        [TestCase(-0.3f, false, 1059.4f)]
        [TestCase(0.6f, false, 1060.78f)]
        [TestCase(-0.6f, false, 1059.22f)]
        public void DonorBumpAndReboundCurveHasTheCorrectSignAndFastBranch(
            float speed, bool shock, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(0.05f, speed, true, false, shock),
                Is.EqualTo(expected).Within(0.0002f));
        }

        [TestCase(false, true, true, 0.6f, 1840f)]
        [TestCase(false, true, true, -0.6f, 1060f)]
        [TestCase(false, false, true, 0.3f, 300.1f)]
        [TestCase(false, false, false, 0.3f, 0.7f)]
        [TestCase(false, false, true, -0.3f, 0f)]
        public void ShockSelectionIsIndependentOfSpringSelection(bool stock, bool longer,
            bool shock, float speed, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(0.05f, speed, stock, longer, shock),
                Is.EqualTo(expected).Within(0.0002f));
        }

        [TestCase(-0.1f, 0f, 0f)]
        [TestCase(-0.1f, 0.3f, 300f)]
        [TestCase(0f, 0.3f, 300f)]
        [TestCase(0f, -0.3f, 0f)]
        [TestCase(0.001f, -0.3f, 0f)]
        public void CompressionAndTotalForceCannotBecomeNegative(float compression,
            float speed, float expected)
        {
            Assert.That(SatsumaRearSuspensionForce.ResolveWheelForce(compression, speed, true, false, true),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteScalarInputsAreRejected(float invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveCompression(LeftPivot, invalid, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveCompressionSpeed(LeftPivot, invalid, 0f, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveCompressionSpeed(LeftPivot, 0f, invalid, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveWheelForce(invalid, 0f, true, false, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveWheelForce(0f, invalid, true, false, true));
        }

        [TestCase(0, float.NaN)]
        [TestCase(1, float.PositiveInfinity)]
        [TestCase(2, float.NegativeInfinity)]
        [TestCase(2, -1.167f)]
        [TestCase(2, -2f)]
        public void InvalidPivotsAreRejectedByBothGeometryMethods(int coordinate, float value)
        {
            Vector3 pivot = LeftPivot;
            pivot[coordinate] = value;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveCompression(pivot, 0f, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionForce.ResolveCompressionSpeed(pivot, 0f, 0f, true, false));
        }

    }
}
