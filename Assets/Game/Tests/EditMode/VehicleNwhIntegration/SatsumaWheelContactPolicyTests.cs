using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleNwhIntegration
{
    public sealed class SatsumaWheelContactPolicyTests
    {
        private static readonly Vector3 LowerTread = new Vector3(0, -.26f, .2f);
        private static readonly Vector3 EdgeNormal = new Vector3(0, .4f, -.91f).normalized;

        [Test]
        public void RailEdgeWithMeasuredRemainingTravelUsesSuspensionContact()
        {
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(true, .01892396f, .3f,
                LowerTread, EdgeNormal), Is.True);
        }

        [Test]
        public void BottomedEdgeRetainsContactWithAuthoredForceTimesStepImpulseBudget()
        {
            Assert.That(SatsumaWheelContactPolicy.BottomedEdgeImpulseLimit(5000, .02f), Is.EqualTo(100));
            Assert.That(SatsumaWheelContactPolicy.BottomedEdgeImpulseLimit(5000, .01f), Is.EqualTo(50));
            Assert.That(SatsumaWheelContactPolicy.BottomedEdgeImpulseLimit(0, .02f), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void AirborneBottomedOutSidewallUpperRimAndFlatGroundRetainSolidNormals()
        {
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(false, .04f, .3f, LowerTread, EdgeNormal), Is.False);
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(true, 0, .3f, LowerTread, EdgeNormal), Is.False);
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(true, .04f, .3f, LowerTread, Vector3.right), Is.False);
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(true, .04f, .3f, Vector3.zero, Vector3.forward), Is.False);
            Assert.That(SatsumaWheelContactPolicy.HasCompliantLowerTreadContact(true, .04f, .3f, LowerTread, Vector3.up), Is.False);
        }
    }
}
