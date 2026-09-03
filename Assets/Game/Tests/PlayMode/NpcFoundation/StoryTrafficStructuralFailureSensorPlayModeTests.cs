using System.Collections;
using MSC.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.NPC.Tests.PlayMode
{
    public sealed class StoryTrafficStructuralFailureSensorPlayModeTests
    {
        [UnityTest]
        public IEnumerator Sensor_UsesDonorDistanceAndSpeedArmingContract()
        {
            var chassisObject = new GameObject("JaniCrashSensor_Chassis");
            var playerObject = new GameObject("JaniCrashSensor_Player");
            var proofMassObject = new GameObject("JaniCrashSensor_DeathForce");
            try
            {
                Rigidbody chassis = chassisObject.AddComponent<Rigidbody>();
                chassis.useGravity = false;
                playerObject.transform.position = Vector3.forward * 20f;
                proofMassObject.transform.SetParent(
                    chassisObject.transform,
                    false);
                proofMassObject.transform.localPosition =
                    new Vector3(0.0000383f, 0.629999f, -0.0290164f);
                Rigidbody proofMass =
                    proofMassObject.AddComponent<Rigidbody>();
                proofMass.useGravity = false;
                FixedJoint joint = proofMassObject.AddComponent<FixedJoint>();
                StoryTrafficStructuralFailureSensor sensor = proofMassObject
                    .AddComponent<StoryTrafficStructuralFailureSensor>();
                sensor.Configure(chassis, playerObject.transform);

                chassis.linearVelocity = Vector3.forward *
                    (StoryTrafficStructuralFailureSensor
                        .DonorArmSpeedKilometersPerHour / 3.6f - 0.01f);
                proofMass.linearVelocity = chassis.linearVelocity;
                yield return new WaitForFixedUpdate();
                Assert.That(sensor.IsArmed, Is.False);
                Assert.That(joint.breakForce, Is.EqualTo(Mathf.Infinity));

                chassis.linearVelocity = Vector3.forward *
                    (StoryTrafficStructuralFailureSensor
                        .DonorArmSpeedKilometersPerHour / 3.6f + 0.01f);
                proofMass.linearVelocity = chassis.linearVelocity;
                yield return new WaitForFixedUpdate();
                Assert.That(sensor.IsArmed, Is.True);
                Assert.That(joint.breakForce, Is.EqualTo(600f));
                Assert.That(joint.breakTorque, Is.EqualTo(600f));

                playerObject.transform.position = Vector3.forward * 1501f;
                yield return new WaitForFixedUpdate();
                Assert.That(sensor.IsArmed, Is.False);
                Assert.That(joint.breakForce, Is.EqualTo(Mathf.Infinity));
            }
            finally
            {
                Object.Destroy(chassisObject);
                Object.Destroy(playerObject);
            }
        }

        [UnityTest]
        public IEnumerator Sensor_ReportsStructuralFailureOnlyOnce()
        {
            var chassisObject = new GameObject("JaniCrashSensor_Chassis");
            var playerObject = new GameObject("JaniCrashSensor_Player");
            var proofMassObject = new GameObject("JaniCrashSensor_DeathForce");
            try
            {
                Rigidbody chassis = chassisObject.AddComponent<Rigidbody>();
                chassis.useGravity = false;
                proofMassObject.transform.SetParent(
                    chassisObject.transform,
                    false);
                Rigidbody proofMass =
                    proofMassObject.AddComponent<Rigidbody>();
                proofMass.useGravity = false;
                proofMassObject.AddComponent<FixedJoint>();
                StoryTrafficStructuralFailureSensor sensor = proofMassObject
                    .AddComponent<StoryTrafficStructuralFailureSensor>();
                sensor.Configure(chassis, playerObject.transform);
                int failures = 0;
                sensor.StructuralFailure += () => failures++;

                chassis.linearVelocity = Vector3.forward *
                    (StoryTrafficStructuralFailureSensor
                        .DonorArmSpeedKilometersPerHour / 3.6f + 1f);
                proofMass.linearVelocity = chassis.linearVelocity;
                sensor.SetSuspended(true);
                sensor.SendMessage(
                    "OnJointBreak",
                    StoryTrafficStructuralFailureSensor
                        .DonorBreakForceNewtons,
                    SendMessageOptions.RequireReceiver);
                Assert.That(failures, Is.Zero,
                    "A suspended/unarmed joint was treated as a story crash.");

                sensor.SetSuspended(false);
                yield return new WaitForFixedUpdate();
                Assert.That(sensor.IsArmed, Is.True);

                sensor.SendMessage(
                    "OnJointBreak",
                    StoryTrafficStructuralFailureSensor
                        .DonorBreakForceNewtons,
                    SendMessageOptions.RequireReceiver);
                sensor.SendMessage(
                    "OnJointBreak",
                    StoryTrafficStructuralFailureSensor
                        .DonorBreakForceNewtons,
                    SendMessageOptions.RequireReceiver);

                Assert.That(failures, Is.EqualTo(1));
                yield return null;
            }
            finally
            {
                Object.Destroy(chassisObject);
                Object.Destroy(playerObject);
            }
        }
    }
}
