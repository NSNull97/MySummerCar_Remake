using System.Collections;
using MSC.Tests.PlayMode.VehicleAssembly;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class VehiclePlayerCollisionFollowerPlayModeTests
    {
        [UnityTest]
        public IEnumerator IsolatedPlayerActorFollowsMovingAndRelocatedChassis()
        {
            var root = new GameObject("Moving follower test");
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var child = new GameObject("Player-only actor");
            child.transform.SetParent(root.transform, false);
            Vector3 offset = new Vector3(.2f, -.3f, .4f);
            child.transform.localPosition = offset;
            var proxy = child.AddComponent<Rigidbody>();
            proxy.isKinematic = true; proxy.useGravity = false;
            var shape = child.AddComponent<BoxCollider>();
            shape.excludeLayers = ~(1 << LayerMask.NameToLayer("Player"));
            root.AddComponent<VehiclePlayerCollisionFollower>().Configure(body, proxy);
            float maxError = 0;
            int samples = 0;
            var probe = root.AddComponent<SatsumaRoadAuditProbe>();
            probe.AfterLateUpdate = () =>
            {
                samples++;
                maxError = Mathf.Max(maxError, Vector3.Distance(offset, root.transform.InverseTransformPoint(child.transform.position)));
            };
            try
            {
                body.linearVelocity = new Vector3(0, 0, 30.556f);
                for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
                Assert.That(body.position.z, Is.GreaterThan(10));
                body.position = new Vector3(100, 20, -100);
                body.rotation = Quaternion.Euler(4, 75, 2);
                for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(samples, Is.GreaterThan(1));
                Assert.That(maxError, Is.LessThan(.001f));
                Assert.That(proxy.isKinematic, Is.True);
                Assert.That(shape.excludeLayers.value, Is.EqualTo(~(1 << LayerMask.NameToLayer("Player"))));
            }
            finally { Object.Destroy(root); }
        }
    }
}
