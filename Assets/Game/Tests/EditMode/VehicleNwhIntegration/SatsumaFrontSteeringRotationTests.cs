using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleNwhIntegration
{
    public sealed class SatsumaFrontSteeringRotationTests
    {
        [TestCase(1f)]
        [TestCase(1.0002f)]
        [TestCase(0.85f)]
        [TestCase(-3f)]
        [TestCase(1e20f)]
        public void ValidScaledRotationBecomesUnitWithoutChangingOrientation(float scale)
        {
            Quaternion expected = Quaternion.Euler(13f, 179f, -7f);
            Quaternion input = Scale(expected, scale);
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(input,
                out Quaternion actual), Is.True);
            Assert.That(Quaternion.Dot(actual, actual), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(Vector3.Distance(actual * Vector3.forward, expected * Vector3.forward),
                Is.LessThan(0.000002f));
            Assert.That(Vector3.Distance(actual * Vector3.up, expected * Vector3.up),
                Is.LessThan(0.000002f));
        }

        [TestCase(0f)]
        [TestCase(0.00001f)]
        [TestCase(-0.00001f)]
        public void ZeroAndNearZeroRotationsAreRejectedWithoutIdentityFallback(float value)
        {
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                new Quaternion(0f, 0f, 0f, value), out Quaternion result), Is.False);
            Assert.That(result, Is.EqualTo(default(Quaternion)));
            Assert.That(result, Is.Not.EqualTo(Quaternion.identity));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteComponentsAreRejectedWithoutIdentityFallback(float invalid)
        {
            for (int component = 0; component < 4; component++)
            {
                Quaternion input = Quaternion.identity;
                input[component] = invalid;
                Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                    input, out Quaternion result), Is.False, "component " + component);
                Assert.That(result, Is.EqualTo(default(Quaternion)));
            }
        }

        [Test]
        public void NormalizedFrameCompositionPreservesDonorYawAndChassisOrientation()
        {
            Quaternion chassis = Quaternion.Euler(12f, 180f, -9f);
            Quaternion local = Quaternion.Euler(0f, 0f, 1.4f);
            Quaternion carrierYaw = Quaternion.Euler(0f,
                SatsumaFrontSteeringController.FreeYawLimitDegrees, 0f);
            Quaternion expected = chassis * local * carrierYaw;
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                Scale(chassis, 1.0002f) * Scale(local, 0.9997f),
                out Quaternion normalizedFrame), Is.True);
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                normalizedFrame * carrierYaw, out Quaternion actual), Is.True);
            Assert.That(Quaternion.Dot(actual, actual), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(Vector3.Distance(actual * Vector3.forward, expected * Vector3.forward),
                Is.LessThan(0.000002f));
            Assert.That(SatsumaFrontSteeringController.FreeYawLimitDegrees, Is.EqualTo(33f));
        }

        [Test]
        public void CapturedNoisyChassisFrameRecomposesToOriginalWheelOrientation()
        {
            Quaternion noisyChassis = new Quaternion(
                -6.05455952e-9f, 1.91231209e-8f, 2.0850166e-10f, 1.00005126f);
            Assert.That(Quaternion.Dot(noisyChassis, noisyChassis), Is.GreaterThan(1.0001f));
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                noisyChassis, out Quaternion chassis), Is.True);
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                Quaternion.Inverse(chassis) * Quaternion.identity, out Quaternion local), Is.True);
            Assert.That(SatsumaFrontSteeringController.TryNormalizePhysicsRotation(
                noisyChassis * local, out Quaternion anchor), Is.True);
            Assert.That(Quaternion.Dot(anchor, anchor), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(Vector3.Distance(anchor * Vector3.forward, Vector3.forward),
                Is.LessThan(0.000002f));
            Assert.That(Vector3.Distance(anchor * Vector3.up, Vector3.up),
                Is.LessThan(0.000002f));
        }

        private static Quaternion Scale(Quaternion value, float scale) => new(
            value.x * scale, value.y * scale, value.z * scale, value.w * scale);
    }
}
