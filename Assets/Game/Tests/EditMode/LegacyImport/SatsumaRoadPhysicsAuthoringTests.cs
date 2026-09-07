using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaRoadPhysicsAuthoringTests
    {
        [Test]
        public void ScopedRoadRefreshIsIdempotentAndKeepsPlayerActorIsolated()
        {
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                Assert.Ignore("Private canonical Satsuma is not generated.");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Assert.That(Phase1SatsumaRoadPhysicsAuthoring.ApplyToInstance(assembly), Is.Zero,
                    "Canonical generated content must already have the scoped road policy.");
                Assert.That(Phase1SatsumaRoadPhysicsAuthoring.ApplyToInstance(assembly), Is.Zero);
                Assert.That(root.GetComponent<Rigidbody>().collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                var follower = root.GetComponent<VehiclePlayerCollisionFollower>();
                Assert.That(follower, Is.Not.Null);
                Assert.That(follower.Chassis, Is.EqualTo(root.GetComponent<Rigidbody>()));
                Assert.That(follower.PlayerCollisionBody.isKinematic, Is.True);
                Assert.That(follower.PlayerCollisionBody.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
                var backend = root.GetComponent<NwhWheelPhysicsBackend>();
                Assert.That(root.GetComponent<SatsumaWheelContactPolicy>().Backend, Is.EqualTo(backend));
                foreach (var wheel in backend.Wheels) Assert.That(wheel.useContactModification, Is.False);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
