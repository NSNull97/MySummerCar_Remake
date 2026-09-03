using System;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaRearSuspensionTravelTests
    {
        [Test]
        public void GeneratedRearArmsStartWithTheNoSpringEnvelope()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            if (prefab == null)
            {
                Assert.Ignore("Private Satsuma baseline is unavailable.");
            }

            VehicleAssemblyController assembly = prefab.GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            foreach (string cornerId in new[] { "rl", "rr" })
            {
                PartInstance arm = assembly.Parts.Single(part => part.Definition != null &&
                    part.Definition.DefinitionId == "vehicle.satsuma.part.trail-arm-" + cornerId);
                AssemblyInstalledPhysicsLink link = arm.GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(link, Is.Not.Null, cornerId);
                using var serialized = new SerializedObject(link);
                Assert.That(serialized.FindProperty("linkMode").intValue,
                    Is.EqualTo((int)AssemblyInstalledPhysicsLinkMode.TrailingArmHinge), cornerId);
                // Independent frozen measurements: never ask ResolveArmLimits
                // for the expected values of the builder that calls it.
                Assert.That(serialized.FindProperty("hingeMinimumDegrees").floatValue,
                    Is.EqualTo(-13.54817f).Within(0.0002f), cornerId);
                Assert.That(serialized.FindProperty("hingeMaximumDegrees").floatValue,
                    Is.EqualTo(11.89519f).Within(0.0002f), cornerId);
            }
        }

        [Test]
        public void ProfilesKeepFrozenDonorWheelAndSuspensionValues()
        {
            Assert.That(SatsumaRearSuspensionTravel.DonorWheelTargetZ, Is.EqualTo(-1.167f));
            Assert.That(SatsumaRearSuspensionTravel.NoSpringWheelRootY, Is.EqualTo(-0.15f));
            Assert.That(SatsumaRearSuspensionTravel.StockWheelRootY, Is.EqualTo(-0.165f));
            Assert.That(SatsumaRearSuspensionTravel.LongWheelRootY, Is.EqualTo(-0.18f));
            Assert.That(SatsumaRearSuspensionTravel.StockSuspensionTravel, Is.EqualTo(0.14f));
            Assert.That(SatsumaRearSuspensionTravel.LongSuspensionTravel, Is.EqualTo(0.17f));
        }

        // Independent atan2 measurements of frozen pivots 36568 and 67486.
        // Do not derive expected angles from the helper under test or use asin
        // to force the accepted physical arm arc through the donor target.
        [TestCase(-0.42300025f, -0.8570004f, false, false, -13.5481663f, 11.8951931f)]
        [TestCase(-0.42300025f, -0.8570004f, true, false, -16.1380754f, 9.2163922f)]
        [TestCase(-0.42300025f, -0.8570004f, false, true, -23.4857507f, 6.4963521f)]
        [TestCase(0.42300043f, -0.8570003f, false, false, -13.5481621f, 11.8951894f)]
        [TestCase(0.42300043f, -0.8570003f, true, false, -16.1380705f, 9.2163892f)]
        [TestCase(0.42300043f, -0.8570003f, false, true, -23.4857439f, 6.4963500f)]
        public void FrozenArmPivotsProduceDonorIkEnvelope(float pivotX, float pivotZ,
            bool hasStockSpring, bool hasLongSpring, float minimum, float maximum)
        {
            Vector2 actual = SatsumaRearSuspensionTravel.ResolveArmLimits(
                new Vector3(pivotX, -0.2153f, pivotZ), hasStockSpring, hasLongSpring);

            Assert.That(actual.x, Is.EqualTo(minimum).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(maximum).Within(0.0001f));
            Assert.That(actual.x, Is.LessThan(actual.y));
        }

        // Independent endpoints from frozen Wheel.cs target heights and the
        // donor rear SimpleIKSolver pivots. These are deliberately not obtained
        // from ResolveArmLimits, so a shared endpoint regression cannot make
        // both methods agree on the same wrong angle.
        [TestCase(-0.42300025f, -0.8570004f, false, false, 0.14f, -13.5481663f, 11.8951931f)]
        [TestCase(-0.42300025f, -0.8570004f, true, false, 0.14f, -16.1380754f, 9.2163922f)]
        [TestCase(-0.42300025f, -0.8570004f, false, true, 0.17f, -23.4857507f, 6.4963521f)]
        [TestCase(0.42300043f, -0.8570003f, false, false, 0.14f, -13.5481621f, 11.8951894f)]
        [TestCase(0.42300043f, -0.8570003f, true, false, 0.14f, -16.1380705f, 9.2163892f)]
        [TestCase(0.42300043f, -0.8570003f, false, true, 0.17f, -23.4857439f, 6.4963500f)]
        public void ResolveArmAngleMapsDonorCompressionEndpoints(float pivotX,
            float pivotZ, bool hasStockSpring, bool hasLongSpring, float travel,
            float expectedDroop, float expectedBump)
        {
            var pivot = new Vector3(pivotX, -0.2153f, pivotZ);

            Assert.That(
                SatsumaRearSuspensionTravel.ResolveArmAngle(
                    pivot, 0f, hasStockSpring, hasLongSpring),
                Is.EqualTo(expectedDroop).Within(0.0001f));
            Assert.That(
                SatsumaRearSuspensionTravel.ResolveArmAngle(
                    pivot, travel, hasStockSpring, hasLongSpring),
                Is.EqualTo(expectedBump).Within(0.0001f));
        }

        [TestCase(false, false, -1f, -13.5481663f)]
        [TestCase(false, false, 1f, 11.8951931f)]
        [TestCase(true, false, -1f, -16.1380754f)]
        [TestCase(true, false, 1f, 9.2163922f)]
        [TestCase(false, true, -1f, -23.4857507f)]
        [TestCase(false, true, 1f, 6.4963521f)]
        public void ResolveArmAngleClampsOutsideDonorTravel(bool hasStockSpring,
            bool hasLongSpring, float compression, float expected)
        {
            Assert.That(
                SatsumaRearSuspensionTravel.ResolveArmAngle(
                    new Vector3(-0.42300025f, -0.2153f, -0.8570004f),
                    compression,
                    hasStockSpring,
                    hasLongSpring),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(false, false, 0f)]
        [TestCase(false, false, 0.037f)]
        [TestCase(false, false, 0.14f)]
        [TestCase(true, false, 0f)]
        [TestCase(true, false, 0.046273585f)]
        [TestCase(true, false, 0.14f)]
        [TestCase(false, true, 0f)]
        [TestCase(false, true, 0.075f)]
        [TestCase(false, true, 0.17f)]
        public void ArmAngleAndCompressionConversionRoundTrip(bool hasStockSpring,
            bool hasLongSpring, float compression)
        {
            float angle = SatsumaRearSuspensionTravel.ResolveArmAngle(
                new Vector3(-0.42300025f, -0.2153f, -0.8570004f),
                compression,
                hasStockSpring,
                hasLongSpring);
            float restored = SatsumaRearSuspensionForce.ResolveCompression(
                new Vector3(-0.42300025f, -0.2153f, -0.8570004f),
                angle,
                hasStockSpring,
                hasLongSpring);

            Assert.That(restored, Is.EqualTo(compression).Within(0.000001f));
        }

        [Test]
        public void LongSpringTakesPriorityLikeTheExistingForceController()
        {
            var pivot = new Vector3(-0.42300025f, -0.2153f, -0.8570004f);
            Vector2 longOnly = SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, false, true);
            Vector2 both = SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, true, true);

            Assert.That(both, Is.EqualTo(longOnly));
        }

        [Test]
        public void RemovingALongSpringRestoresTheExplicitNoSpringEnvelope()
        {
            var pivot = new Vector3(-0.42300025f, -0.2153f, -0.8570004f);
            Vector2 before = SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, false, false);
            Vector2 installed = SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, false, true);
            Vector2 removed = SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, false, false);

            Assert.That(installed.x, Is.LessThan(before.x));
            Assert.That(removed, Is.EqualTo(before));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void LateralPivotPositionDoesNotChangeTheSagittalEnvelope(
            bool hasStockSpring, bool hasLongSpring)
        {
            Vector2 left = SatsumaRearSuspensionTravel.ResolveArmLimits(
                new Vector3(-0.423f, -0.2153f, -0.857f), hasStockSpring, hasLongSpring);
            Vector2 right = SatsumaRearSuspensionTravel.ResolveArmLimits(
                new Vector3(0.423f, -0.2153f, -0.857f), hasStockSpring, hasLongSpring);

            Assert.That(right, Is.EqualTo(left));
        }

        [TestCase(0, float.NaN)]
        [TestCase(0, float.PositiveInfinity)]
        [TestCase(0, float.NegativeInfinity)]
        [TestCase(1, float.NaN)]
        [TestCase(1, float.PositiveInfinity)]
        [TestCase(1, float.NegativeInfinity)]
        [TestCase(2, float.NaN)]
        [TestCase(2, float.PositiveInfinity)]
        [TestCase(2, float.NegativeInfinity)]
        public void NonFinitePivotCoordinatesAreRejected(int coordinate, float value)
        {
            var pivot = new Vector3(-0.423f, -0.2153f, -0.857f);
            pivot[coordinate] = value;

            ArgumentOutOfRangeException limitsException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, true, false));
            ArgumentOutOfRangeException angleException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionTravel.ResolveArmAngle(pivot, 0.05f, true, false));

            Assert.That(limitsException.ParamName, Is.EqualTo("armPivotCarLocalPosition"));
            Assert.That(angleException.ParamName, Is.EqualTo("armPivotCarLocalPosition"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ResolveArmAngleRejectsNonFiniteCompression(float compression)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionTravel.ResolveArmAngle(
                    new Vector3(-0.423f, -0.2153f, -0.857f),
                    compression,
                    true,
                    false));

            Assert.That(exception.ParamName, Is.EqualTo("compressionMeters"));
        }

        [TestCase(-1.167f)]
        [TestCase(-2f)]
        public void PivotMustBeAheadOfTheWheelTarget(float pivotZ)
        {
            var pivot = new Vector3(-0.423f, -0.2153f, pivotZ);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionTravel.ResolveArmLimits(pivot, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SatsumaRearSuspensionTravel.ResolveArmAngle(pivot, 0f, false, false));
        }
    }
}
