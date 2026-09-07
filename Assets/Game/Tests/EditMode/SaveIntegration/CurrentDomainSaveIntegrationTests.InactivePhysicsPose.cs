using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CurrentDomainSaveIntegrationTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void PhysicsPose_WorldRegistrationAndCapturePreserveActiveAndInactiveBodies(
            bool disableSelf,
            bool disableParent)
        {
            var parent = new GameObject("World pose inactive-parent fixture");
            try
            {
                item.transform.SetParent(parent.transform, true);
                item.SetActive(!disableSelf);
                parent.SetActive(!disableParent);
                var expected = new Pose(
                    new Vector3(37f, 6f, -19f),
                    Quaternion.Euler(17f, 63f, -8f));
                SetPhysicsPoseForCapture(body, expected);
                bool activeBefore = item.activeInHierarchy;

                var inactiveParticipant = new WorldEntitySaveParticipant(
                    new DeferredStableEntityStore());
                inactiveParticipant.RegisterScene(item.scene);
                AssertPhysicsPose(item.transform.position, item.transform.rotation, expected);
                WorldEntityStateDto saved = SaveParticipantJson
                    .Deserialize<WorldEntityDomainSaveDto>(inactiveParticipant.CapturePayload())
                    .entities.Single(value => value.stableEntityId == EntityId);

                AssertPhysicsPose(saved.worldPosition, saved.worldRotation, expected);
                Assert.That(saved.activeSelf, Is.EqualTo(!disableSelf));
                Assert.That(item.activeSelf, Is.EqualTo(!disableSelf));
                Assert.That(item.activeInHierarchy, Is.EqualTo(activeBefore));
                AssertPhysicsPose(item.transform.position, item.transform.rotation, expected);
            }
            finally
            {
                item.transform.SetParent(null, true);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void PhysicsPose_VehicleBindingCapturesActiveAndInactiveChassis(
            bool disableSelf,
            bool disableParent)
        {
            using var vehicle = SyntheticVehicleFixture.Create();
            var parent = new GameObject("Vehicle pose inactive-parent fixture");
            try
            {
                vehicle.Root.transform.SetParent(parent.transform, true);
                vehicle.Root.SetActive(!disableSelf);
                parent.SetActive(!disableParent);
                var expected = new Pose(
                    new Vector3(-27f, 4f, 41f),
                    Quaternion.Euler(-9f, 107f, 6f));
                SetPhysicsPoseForCapture(vehicle.Chassis, expected);
                bool activeBefore = vehicle.Root.activeInHierarchy;

                Assert.That(vehicle.Binding.TryCapture(
                    out VehicleSaveRecordDto saved,
                    out string failure), Is.True, failure);

                AssertPhysicsPose(saved.physics.worldPosition, saved.physics.worldRotation, expected);
                Assert.That(vehicle.Root.activeSelf, Is.EqualTo(!disableSelf));
                Assert.That(vehicle.Root.activeInHierarchy, Is.EqualTo(activeBefore));
                AssertPhysicsPose(vehicle.Root.transform.position, vehicle.Root.transform.rotation, expected);
            }
            finally
            {
                vehicle.Root.transform.SetParent(null, true);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PhysicsPose_VehicleStartupGuardPreservesChassisAndLooseParts(bool inactive)
        {
            using var vehicle = SyntheticVehicleFixture.Create();
            vehicle.Root.SetActive(!inactive);
            vehicle.LoosePartsRoot.SetActive(!inactive);
            var chassisPose = new Pose(
                new Vector3(31f, 5f, -23f),
                Quaternion.Euler(8f, 76f, -4f));
            var loosePose = new Pose(
                new Vector3(36f, 7f, -28f),
                Quaternion.Euler(23f, -44f, 19f));
            SetPhysicsPoseForCapture(vehicle.Chassis, chassisPose);
            SetPhysicsPoseForCapture(vehicle.LooseEngineABody, loosePose);
            var vehicles = new VehicleSaveParticipant(new DeferredStableEntityStore());
            vehicles.RegisterHierarchy(vehicle.Root);

            vehicles.GuardPersistentVehiclesUntilWorldReady();
            try
            {
                AssertPhysicsPose(vehicle.Root.transform.position, vehicle.Root.transform.rotation, chassisPose);
                AssertPhysicsPose(vehicle.LooseEngineA.transform.position, vehicle.LooseEngineA.transform.rotation, loosePose);
                VehicleSaveRecordDto saved = SaveParticipantJson
                    .Deserialize<VehicleDomainSaveDto>(vehicles.CapturePayload())
                    .vehicles.Single();
                PartSaveDto loose = saved.assembly.parts.Single(
                    value => value.stableEntityId == SyntheticVehicleFixture.LooseEngineAStableId);
                AssertPhysicsPose(saved.physics.worldPosition, saved.physics.worldRotation, chassisPose);
                AssertPhysicsPose(loose.worldPosition, loose.worldRotation, loosePose);
                Assert.That(vehicle.Root.activeInHierarchy, Is.EqualTo(!inactive));
                Assert.That(vehicle.LooseEngineA.gameObject.activeSelf, Is.True);
                Assert.That(vehicle.LooseEngineA.gameObject.activeInHierarchy, Is.EqualTo(!inactive));
            }
            finally
            {
                vehicles.ReleasePersistentVehicleStartupGuard();
            }

            AssertPhysicsPose(vehicle.Root.transform.position, vehicle.Root.transform.rotation, chassisPose);
            AssertPhysicsPose(vehicle.LooseEngineA.transform.position, vehicle.LooseEngineA.transform.rotation, loosePose);
            Assert.That(vehicle.Root.activeSelf, Is.EqualTo(!inactive));
            Assert.That(vehicle.LoosePartsRoot.activeSelf, Is.EqualTo(!inactive));
            Assert.That(vehicle.LooseEngineA.gameObject.activeInHierarchy, Is.EqualTo(!inactive));
        }

        private static void SetPhysicsPoseForCapture(Rigidbody target, Pose pose)
        {
            target.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (target.gameObject.activeInHierarchy)
            {
                target.position = pose.position;
                target.rotation = pose.rotation;
                AssertPhysicsPose(target.position, target.rotation, pose);
            }
        }

        private static void AssertPhysicsPose(Vector3 position, Quaternion rotation, Pose expected)
        {
            Assert.That(Vector3.Distance(position, expected.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(rotation * Vector3.forward, expected.rotation * Vector3.forward), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(rotation * Vector3.up, expected.rotation * Vector3.up), Is.LessThan(0.0001f));
        }
    }
}
