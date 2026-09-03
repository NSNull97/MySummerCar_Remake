using System.Collections;
using System.Collections.Generic;
using MSC.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using InCarRagdollShape =
    MSC.Characters.StoryTrafficInCarRagdollBinding.ColliderShape;

namespace MSC.NPC.Tests.PlayMode
{
    public sealed class StoryTrafficInCarRagdollPlayModeTests
    {
        private readonly List<Object> cleanup = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    Object.DestroyImmediate(cleanup[index]);
                }
            }

            cleanup.Clear();
        }

        [UnityTest]
        public IEnumerator TerminalCrash_DisablesSeatedPoseAndRestrainsBothOccupants()
        {
            var vehicle = new GameObject("StoryTraffic_Ragdoll_Test");
            cleanup.Add(vehicle);
            Rigidbody chassis = vehicle.AddComponent<Rigidbody>();
            chassis.isKinematic = true;
            vehicle.AddComponent<BoxCollider>().size =
                new Vector3(1.8f, 1.2f, 4.2f);

            TestOccupant driver = CreateOccupant(
                vehicle.transform,
                "Driver",
                new Vector3(-0.35f, 0.75f, 0.25f));
            TestOccupant passenger = CreateOccupant(
                vehicle.transform,
                "Passenger",
                new Vector3(0.35f, 0.75f, 0.25f));

            var ragdoll = vehicle.AddComponent<
                StoryTrafficInCarRagdollBinding>();
            ragdoll.ConfigureForAuthoring(
                chassis,
                new[]
                {
                    CreateDefinition("P1.NPC.TEST_DRIVER", driver),
                    CreateDefinition("P1.NPC.TEST_PASSENGER", passenger),
                });

            var presentation = vehicle.AddComponent<
                StoryTrafficVehiclePresentationBinding>();
            Transform[] wheels = new Transform[4];
            for (int index = 0; index < wheels.Length; index++)
            {
                var wheel = new GameObject($"Wheel_{index}");
                wheel.transform.SetParent(vehicle.transform, false);
                wheels[index] = wheel.transform;
            }

            presentation.ConfigureForAuthoring(
                "P1.NPC.TEST_DRIVER",
                new[] { "P1.NPC.TEST_PASSENGER" },
                new[] { passenger.Root },
                wheels,
                configuredWheelDegreesPerMeter: 190f,
                configuredGroundContactCalibrationMeters: 0f);
            presentation.ConfigureInCarRagdollForAuthoring(ragdoll);

            Assert.That(presentation.TryValidate(out string failure),
                Is.True, failure);
            Assert.That(driver.PoseAnimation.enabled, Is.True);
            Assert.That(passenger.PoseAnimation.enabled, Is.True);

            presentation.SetStoryIncidentHold(true);
            presentation.SetStoryIncidentHold(true);
            yield return new WaitForFixedUpdate();

            Assert.That(ragdoll.IsTerminalCrashActive, Is.True);
            Assert.That(ragdoll.ActivationCount, Is.EqualTo(1),
                "Repeated terminal-state reconciliation must not duplicate bodies.");
            Assert.That(ragdoll.OccupantCount, Is.EqualTo(2));
            Assert.That(ragdoll.ActiveBodyCount, Is.EqualTo(6));
            Assert.That(driver.PoseAnimation.enabled, Is.False);
            Assert.That(passenger.PoseAnimation.enabled, Is.False);
            Assert.That(passenger.Root.activeSelf, Is.True,
                "The terminal crash must not eject or hide the passenger.");
            Assert.That(ragdoll.IsPickupAvailable, Is.False,
                "Pickup becomes available only after explicit Suski extraction.");

            Assert.That(ragdoll.TryGetPrimaryBody(
                "P1.NPC.TEST_PASSENGER",
                out Rigidbody passengerPrimary), Is.True);
            Assert.That(presentation.TryGetPassengerWorldPose(
                "P1.NPC.TEST_PASSENGER",
                out Vector3 passengerWorldPosition,
                out Quaternion passengerWorldRotation), Is.True);
            Assert.That(passengerWorldPosition,
                Is.EqualTo(passengerPrimary.position));
            Assert.That(passengerWorldRotation,
                Is.EqualTo(passengerPrimary.rotation));

            AssertSeatRestraint(ragdoll, "P1.NPC.TEST_DRIVER", chassis);
            AssertSeatRestraint(ragdoll, "P1.NPC.TEST_PASSENGER", chassis);
            foreach (Rigidbody body in vehicle.GetComponentsInChildren<
                         Rigidbody>(true))
            {
                if (body == chassis)
                {
                    continue;
                }

                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.detectCollisions, Is.True);
            }

            yield return new WaitForFixedUpdate();
            AssertPrimaryRemainsAtSeat(
                ragdoll,
                "P1.NPC.TEST_DRIVER");
            AssertPrimaryRemainsAtSeat(
                ragdoll,
                "P1.NPC.TEST_PASSENGER");
        }

        private TestOccupant CreateOccupant(
            Transform vehicle,
            string name,
            Vector3 seatPosition)
        {
            var root = new GameObject(name);
            root.transform.SetParent(vehicle, false);
            root.transform.localPosition = seatPosition;
            Animation animation = root.AddComponent<Animation>();
            var clip = new AnimationClip
            {
                legacy = true,
                name = name + "_SeatedPose",
            };
            cleanup.Add(clip);
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.Play();

            Transform pelvis = CreateBone(root.transform, "Pelvis", Vector3.zero);
            Transform spine = CreateBone(
                pelvis,
                "Spine",
                Vector3.up * 0.3f);
            Transform head = CreateBone(
                spine,
                "Head",
                Vector3.up * 0.3f);
            return new TestOccupant(root, animation, pelvis, spine, head);
        }

        private static Transform CreateBone(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            var bone = new GameObject(name);
            bone.transform.SetParent(parent, false);
            bone.transform.localPosition = localPosition;
            return bone.transform;
        }

        private static StoryTrafficInCarRagdollBinding.OccupantDefinition
            CreateDefinition(string featureId, TestOccupant occupant)
        {
            return new StoryTrafficInCarRagdollBinding.OccupantDefinition(
                featureId,
                occupant.Root,
                new Behaviour[] { occupant.PoseAnimation },
                new[]
                {
                    new StoryTrafficInCarRagdollBinding.BodyDefinition(
                        occupant.Pelvis,
                        null,
                        -1,
                        InCarRagdollShape.Box,
                        new Vector3(0.3f, 0.2f, 0.24f),
                        0.1f,
                        12f,
                        25f,
                        35f),
                    new StoryTrafficInCarRagdollBinding.BodyDefinition(
                        occupant.Spine,
                        occupant.Head,
                        0,
                        InCarRagdollShape.Capsule,
                        Vector3.zero,
                        0.11f,
                        8f,
                        25f,
                        35f),
                    new StoryTrafficInCarRagdollBinding.BodyDefinition(
                        occupant.Head,
                        null,
                        1,
                        InCarRagdollShape.Sphere,
                        Vector3.zero,
                        0.12f,
                        4f,
                        25f,
                        30f),
                });
        }

        private static void AssertSeatRestraint(
            StoryTrafficInCarRagdollBinding ragdoll,
            string featureId,
            Rigidbody chassis)
        {
            Assert.That(ragdoll.TryGetSeatRestraint(
                featureId,
                out ConfigurableJoint restraint), Is.True);
            Assert.That(restraint.connectedBody, Is.SameAs(chassis));
            Assert.That(restraint.xMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(restraint.yMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(restraint.zMotion,
                Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(restraint.linearLimit.limit,
                Is.LessThanOrEqualTo(0.3f));
        }

        private static void AssertPrimaryRemainsAtSeat(
            StoryTrafficInCarRagdollBinding ragdoll,
            string featureId)
        {
            Assert.That(ragdoll.TryGetPrimaryBody(featureId, out Rigidbody body),
                Is.True);
            Assert.That(ragdoll.TryGetSeatRestraint(
                featureId,
                out ConfigurableJoint restraint), Is.True);
            Vector3 seatAnchor = restraint.connectedBody.transform
                .TransformPoint(restraint.connectedAnchor);
            Assert.That(Vector3.Distance(body.position, seatAnchor),
                Is.LessThan(0.5f));
        }

        private readonly struct TestOccupant
        {
            public TestOccupant(
                GameObject root,
                Animation poseAnimation,
                Transform pelvis,
                Transform spine,
                Transform head)
            {
                Root = root;
                PoseAnimation = poseAnimation;
                Pelvis = pelvis;
                Spine = spine;
                Head = head;
            }

            public GameObject Root { get; }
            public Animation PoseAnimation { get; }
            public Transform Pelvis { get; }
            public Transform Spine { get; }
            public Transform Head { get; }
        }
    }
}
