using System.Reflection;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class InstalledPhysicsCollisionPolicyTests
    {
        [TestCase(CollisionDetectionMode.ContinuousDynamic, CollisionDetectionMode.ContinuousDynamic)]
        [TestCase(CollisionDetectionMode.ContinuousSpeculative, CollisionDetectionMode.ContinuousSpeculative)]
        [TestCase(CollisionDetectionMode.Discrete, CollisionDetectionMode.ContinuousSpeculative)]
        public void InstalledLinkInheritsOnlyExplicitSweepPolicy(CollisionDetectionMode ownerPolicy, CollisionDetectionMode expected)
        {
            var root = new GameObject("Policy owner");
            var part = new GameObject("Physical child");
            try
            {
                var owner = root.AddComponent<Rigidbody>();
                owner.collisionDetectionMode = ownerPolicy;
                var mount = root.AddComponent<MountPointAuthoring>();
                var child = part.AddComponent<Rigidbody>();
                var link = part.AddComponent<AssemblyInstalledPhysicsLink>();
                link.Configure(child, AssemblyInstalledPhysicsLinkMode.Fixed, preferredConnectedBody: owner);
                bool attached = (bool)typeof(AssemblyInstalledPhysicsLink).GetMethod("TryAttach", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(link, new object[] { mount });
                Assert.That(attached, Is.True);
                Assert.That(child.collisionDetectionMode, Is.EqualTo(expected));
                Assert.That(link.ConnectedBody, Is.EqualTo(owner));
                Assert.That(link.InstalledWeld.projectionDistance, Is.EqualTo(.001f));
            }
            finally { Object.DestroyImmediate(part); Object.DestroyImmediate(root); }
        }
    }
}
