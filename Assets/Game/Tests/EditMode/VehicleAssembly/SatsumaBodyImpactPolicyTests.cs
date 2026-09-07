using System.Reflection;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaBodyImpactPolicyTests
    {
        [Test]
        public void TangentialHighwaySpeedDoesNotBecomeAHardBodyImpact()
        {
            var method = typeof(VehicleAssemblyAudioPresenter).GetMethod("NormalImpactSpeed", BindingFlags.NonPublic | BindingFlags.Static);
            float Speed(Vector3 velocity, Vector3 normal) => (float)method.Invoke(null, new object[] { velocity, normal });
            Assert.That(Speed(Vector3.forward * 30.556f, Vector3.up), Is.Zero);
            Assert.That(Speed(new Vector3(0, -1, 30.556f), Vector3.up), Is.EqualTo(1));
            Assert.That(Speed(Vector3.forward * 10, Vector3.back), Is.EqualTo(10));
        }

        [Test]
        public void WorldImpactUsesDonorTimeGateEvenWithOldSerializedCooldown()
        {
            var car = new GameObject("Impact policy car");
            var road = new GameObject("World surface");
            try
            {
                var presenter = car.AddComponent<VehicleAssemblyAudioPresenter>();
                Set(presenter, "controller", car.AddComponent<VehicleAssemblyController>());
                Set(presenter, "bodyImpactWorldLayers", LayerMask.GetMask("Default", "WorldSurface", "WorldSolid"));
                Set(presenter, "impactCooldownSeconds", .08f);
                Collider target = road.AddComponent<BoxCollider>();
                Assert.That(Accept(presenter, target, 5, 10), Is.True);
                Assert.That(Accept(presenter, target, 30, 10.1f), Is.False);
                Assert.That(Accept(presenter, target, 30, 11.49f), Is.False);
                Assert.That(Accept(presenter, target, 30, 11.5f), Is.True);
                Assert.That(Accept(presenter, target, float.NaN, 13), Is.False);
                Assert.That(Accept(presenter, target, .1f, 13), Is.False);
                Assert.That(Accept(presenter, target, 30, 13), Is.True,
                    "Rejected weak/invalid events must not consume the gate.");
            }
            finally { Object.DestroyImmediate(road); Object.DestroyImmediate(car); }
        }

        [Test]
        public void OwnAssemblyPlayerItemsAndTriggersCannotEmitWorldBodyImpact()
        {
            var car = new GameObject("Impact policy car");
            var other = new GameObject("Other collider");
            var own = new GameObject("Own wheel proxy");
            try
            {
                own.transform.SetParent(car.transform);
                var presenter = car.AddComponent<VehicleAssemblyAudioPresenter>();
                Set(presenter, "controller", car.AddComponent<VehicleAssemblyController>());
                Set(presenter, "bodyImpactWorldLayers", LayerMask.GetMask("Default", "WorldSurface", "WorldSolid"));
                Collider target = other.AddComponent<BoxCollider>();
                Assert.That(Accept(presenter, own.AddComponent<BoxCollider>(), 30, 1), Is.False);
                foreach (string layer in new[] { "Player", "WorldItem", "Ignore Raycast" })
                {
                    other.layer = LayerMask.NameToLayer(layer);
                    Assert.That(Accept(presenter, target, 30, 1), Is.False, layer);
                }
                other.layer = 0; target.isTrigger = true;
                Assert.That(Accept(presenter, target, 30, 1), Is.False);
                target.isTrigger = false;
                Assert.That(Accept(presenter, target, 30, 1), Is.True);
            }
            finally { Object.DestroyImmediate(other); Object.DestroyImmediate(car); }
        }

        private static bool Accept(VehicleAssemblyAudioPresenter presenter, Collider target, float speed, float now) =>
            (bool)typeof(VehicleAssemblyAudioPresenter).GetMethod("TryAcceptBodyImpact", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(presenter, new object[] { target, speed, now });

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
