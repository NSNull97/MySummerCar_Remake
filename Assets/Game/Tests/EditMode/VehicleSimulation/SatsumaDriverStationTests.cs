using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaDriverStationTests
    {
        private GameObject car, player;
        private SatsumaDriverStation station;
        private CharacterController capsule;
        private PartInstance seat;

        [SetUp]
        public void SetUp()
        {
            car = new GameObject("TEST ONLY driver station");
            var body = car.AddComponent<Rigidbody>(); body.isKinematic = true;
            var seatObject = new GameObject("TEST ONLY installed seat");
            seatObject.transform.SetParent(car.transform, false);
            seat = seatObject.AddComponent<PartInstance>();
            seat.RuntimeState.SetInstalled(SatsumaDriverStation.StockSeatMountId, false);
            var trigger = new GameObject("TEST ONLY capsule"); trigger.transform.SetParent(car.transform, false);
            trigger.transform.localPosition = new Vector3(-.282f,.05692613284f,-.06712156f);
            var activation = trigger.AddComponent<CapsuleCollider>();
            activation.isTrigger = true; activation.direction = 2; activation.radius = .03f; activation.height = .3f;
            var eyes = new GameObject("TEST ONLY eyes"); eyes.transform.SetParent(car.transform, false);
            station = car.AddComponent<SatsumaDriverStation>();
            station.Configure("323d9fece916469ea30c705ebfcf68df", activation, eyes.transform, seat, body);
            player = new GameObject("TEST ONLY traversal capsule"); capsule = player.AddComponent<CharacterController>();
            capsule.height = .5f; capsule.radius = .12f; capsule.center = Vector3.up * .25f;
            PlaceAtLocalOffset(Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(car);
        }

        private void PlaceAtLocalOffset(Vector3 offset)
        {
            capsule.enabled = false;
            player.transform.SetPositionAndRotation(car.transform.TransformPoint(
                station.ActivationTrigger.transform.localPosition + offset - Vector3.up * .25f), car.transform.rotation);
            capsule.enabled = true;
        }

        [Test]
        public void InteriorOverlapRequiresActualBodyNotDistantDoorInteraction()
        {
            Assert.That(station.ContainsPlayer(capsule), Is.True);
            Assert.That(station.IsAvailable, Is.True);
            PlaceAtLocalOffset(Vector3.left * .151f);
            Assert.That(station.ContainsPlayer(capsule), Is.False);
            PlaceAtLocalOffset(Vector3.right * .151f);
            Assert.That(station.ContainsPlayer(capsule), Is.False);
            PlaceAtLocalOffset(Vector3.up * 1.5f);
            Assert.That(station.ContainsPlayer(capsule), Is.False, "Standing on roof is not boarding.");
            PlaceAtLocalOffset(Vector3.down * 1.5f);
            Assert.That(station.ContainsPlayer(capsule), Is.False, "Standing below the car is not boarding.");
        }

        [Test]
        public void CapsuleOverlapFollowsCarTransformAndDoesNotChangeGeometry()
        {
            car.transform.SetPositionAndRotation(new Vector3(80,4,-32), Quaternion.Euler(12,128,9));
            PlaceAtLocalOffset(Vector3.zero);
            Assert.That(station.ContainsPlayer(capsule), Is.True);
            PlaceAtLocalOffset(Vector3.forward * .35f);
            Assert.That(station.ContainsPlayer(capsule), Is.False);
            Assert.That(capsule.height, Is.EqualTo(.5f));
            Assert.That(capsule.radius, Is.EqualTo(.12f));
        }

        [Test]
        public void DisabledTriggerPlayerAndMissingSeatFailClosed()
        {
            station.ActivationTrigger.enabled = false;
            Assert.That(station.ContainsPlayer(capsule), Is.False);
            station.ActivationTrigger.enabled = true;
            capsule.enabled = false;
            Assert.That(station.ContainsPlayer(capsule), Is.False);
            capsule.enabled = true;
            seat.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
            Assert.That(station.IsAvailable, Is.False);
            seat.RuntimeState.SetInstalled(SatsumaDriverStation.StockSeatMountId, false);
            Assert.That(station.IsAvailable, Is.True, "Donor entry requires Installed, not the fastening latch.");
        }

        [TestCase(0f, true)]
        [TestCase(.15f, true)]
        [TestCase(.151f, false)]
        [TestCase(-.151f, false)]
        [TestCase(float.NaN, false)]
        [TestCase(float.PositiveInfinity, false)]
        public void ReleaseChecksEachWorldVelocityComponent(float component, bool allowed)
        {
            Assert.That(SatsumaDriverStation.IsWithinExitVelocity(new Vector3(component,0,0),.15f), Is.EqualTo(allowed));
            Assert.That(SatsumaDriverStation.IsWithinExitVelocity(new Vector3(0,component,0),.15f), Is.EqualTo(allowed));
            Assert.That(SatsumaDriverStation.IsWithinExitVelocity(new Vector3(0,0,component),.15f), Is.EqualTo(allowed));
        }

        [Test]
        public void SegmentDistanceHandlesParallelCrossingAndPointCapsules()
        {
            Assert.That(SatsumaDriverStation.SegmentDistanceSquared(Vector3.zero,Vector3.right,
                Vector3.up, Vector3.up+Vector3.right), Is.EqualTo(1f).Within(.00001f));
            Assert.That(SatsumaDriverStation.SegmentDistanceSquared(-Vector3.right,Vector3.right,
                -Vector3.up,Vector3.up), Is.Zero.Within(.00001f));
            Assert.That(SatsumaDriverStation.SegmentDistanceSquared(Vector3.zero,Vector3.zero,
                Vector3.one,Vector3.one), Is.EqualTo(3f));
        }

        [Test]
        public void OptionalDrivingSaveStateRoundTripsAndOldPlayerRemainsWalking()
        {
            var motor = FirstPersonMotorSaveDto.Create(PlayerPosture.Crouch,0);
            var look = FirstPersonLookSaveDto.Create(-12);
            var walking = PlayerSaveDto.Create(Vector3.one,Quaternion.identity,motor,look);
            var walkingCopy = JsonUtility.FromJson<PlayerSaveDto>(JsonUtility.ToJson(walking));
            Assert.That(walkingCopy.TryValidate(out _), Is.True);
            Assert.That(walkingCopy.HasDrivingState, Is.False);
            Assert.That(walkingCopy.DrivingState, Is.Null);
            var driving = PlayerDrivingSaveDto.Create(station.StationId, -125,
                station.transform.InverseTransformPoint(player.transform.position), PlayerPosture.DeepCrouch);
            var seated = PlayerSaveDto.Create(Vector3.one,Quaternion.identity,motor,look,driving);
            var copy = JsonUtility.FromJson<PlayerSaveDto>(JsonUtility.ToJson(seated));
            Assert.That(copy.TryValidate(out _), Is.True);
            Assert.That(copy.HasDrivingState, Is.True);
            Assert.That(copy.DrivingState.LocalYawDegrees, Is.EqualTo(-125));
            Assert.That(copy.DrivingState.ReleasePosture, Is.EqualTo(PlayerPosture.DeepCrouch));
            Assert.That(copy.SchemaVersion, Is.EqualTo(walking.SchemaVersion));
        }

        [Test]
        public void MalformedDriverStateIsRejectedBeforeUse()
        {
            Assert.Throws<System.ArgumentException>(() => PlayerDrivingSaveDto.Create("",0,Vector3.zero,PlayerPosture.Crouch));
            Assert.Throws<System.ArgumentException>(() => PlayerDrivingSaveDto.Create(station.StationId,float.NaN,Vector3.zero,PlayerPosture.Crouch));
            Assert.Throws<System.ArgumentException>(() => PlayerDrivingSaveDto.Create(station.StationId,0,Vector3.one*100,PlayerPosture.Crouch));
            var unknown = JsonUtility.FromJson<PlayerDrivingSaveDto>("{\"schemaVersion\":99,\"stationId\":\"test\"}");
            Assert.That(unknown.TryValidate(out _), Is.False);
        }

        [Test]
        public void CanonicalStationReferencesAndRepeatedScopedAuthoringAreStable()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var authored = root.GetComponent<SatsumaDriverStation>();
                Assert.That(authored, Is.Not.Null);
                Assert.That(authored.VehicleStableId, Is.EqualTo("323d9fece916469ea30c705ebfcf68df"));
                Assert.That(authored.DriverSeat.Definition.DefinitionId, Is.EqualTo("vehicle.satsuma.part.seat-driver"));
                Assert.That(authored.ActivationTrigger.gameObject.layer, Is.EqualTo(2));
                Assert.That(authored.DriverEyeAnchor.localPosition.y, Is.EqualTo(.5608089f).Within(.00001f));
                Assert.That(root.GetComponent<VehicleInputRouter>().enabled, Is.False,
                    "The prefab remains parked; only runtime composition grants driver-session authority.");
                Assert.That(Phase1SatsumaDriverStationAuthoring.ApplyToInstance(root.GetComponent<VehicleAssemblyController>()), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
