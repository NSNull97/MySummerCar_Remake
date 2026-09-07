using System;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaWiringReachAndOcclusionTests
    {
        [Test]
        public void ClusterUsesActualSpoolPositionNotSelectedEndpointPosition()
        {
            using var f = new Fixture();
            var first = f.Endpoint(0, new Vector3(0, 0, 1));
            var second = f.Endpoint(1, new Vector3(.08f, 0, 1));
            f.Tool.transform.position = first.transform.position + Vector3.left * .09f;
            var context = new InteractionContext(f.Root, Vector3.zero, Vector3.forward);
            Assert.That(first.CanActivateHeldTool(f.Tool, context), Is.True);
            first.ActivateHeldTool(f.Tool, context);
            Assert.That(f.System.IsEndpointArmed(first.Connection, 0), Is.True);
            Assert.That(f.System.IsEndpointArmed(first.Connection, 1), Is.False);
            Assert.That(f.System.IsConnectionInstalled(first.Connection), Is.False,
                "The second point is 17cm from the spool, though only8cm from the selected point.");
            f.Tool.transform.position = second.transform.position;
            second.ActivateHeldTool(f.Tool, context);
            Assert.That(f.System.IsConnectionInstalled(first.Connection), Is.True);
        }

        [Test]
        public void DistantSpoolCannotArmAndRealOverlappingEndsStillCompleteInOneUse()
        {
            using var f = new Fixture();
            var first = f.Endpoint(0, new Vector3(0, 0, 1));
            f.Endpoint(1, new Vector3(.05f, 0, 1));
            var context = new InteractionContext(f.Root, Vector3.zero, Vector3.forward);
            f.Tool.transform.position = first.transform.position - Vector3.forward * .2f;
            Assert.That(first.CanActivateHeldTool(f.Tool, context), Is.False);
            first.ActivateHeldTool(f.Tool, context);
            Assert.That(f.System.IsEndpointArmed(first.Connection, 0), Is.False);
            f.Tool.transform.position = first.transform.position;
            first.ActivateHeldTool(f.Tool, context);
            Assert.That(f.System.IsConnectionInstalled(first.Connection), Is.True);
        }

        [TestCase(.4f, false)]
        [TestCase(.4f, true)]
        [TestCase(0f, false)]
        [TestCase(0f, true)]
        public void UnreachableNearerCameraEndpointDoesNotMaskReachableEndpointOrBypassWall(
            float nearerEndpointDepth, bool foreignWall)
        {
            using var f = new Fixture();
            var nearer = f.Endpoint(0, Vector3.forward * nearerEndpointDepth);
            var reachable = f.Endpoint(1, Vector3.forward);
            f.Tool.transform.position = reachable.transform.position + Vector3.right * .05f;
            if (foreignWall) f.Solid(null, .7f, false);
            Physics.SyncTransforms();
            var context = new InteractionContext(f.Root, f.Root.transform.position, Vector3.forward);

            Assert.That(nearer.IsEndpointAvailable, Is.True);
            Assert.That(nearer.CanSelectForCarriedObject(f.Tool, context), Is.False,
                "The nearer trigger (including origin overlap) must be rejected before ray ranking.");
            Assert.That(reachable.CanSelectForCarriedObject(f.Tool, context), Is.True);
            Assert.That(reachable.CanActivateHeldTool(f.Tool, context), Is.True);

            var candidate = f.Query.Query();
            bool selected = candidate.TryGetCapability(out SatsumaWiringConnectorInteractionTarget actual);
            Assert.That(selected, Is.EqualTo(!foreignWall),
                "Filtering an unreachable trigger must not make an unrelated solid transparent.");
            if (selected)
            {
                Assert.That(actual, Is.SameAs(reachable));
                actual.ActivateHeldTool(f.Tool, context);
                Assert.That(f.System.IsEndpointArmed(reachable.Connection, 1), Is.True);
                Assert.That(f.System.IsEndpointArmed(reachable.Connection, 0), Is.False);
                Assert.That(f.System.IsConnectionInstalled(reachable.Connection), Is.False);
            }
        }

        [TestCase(.09999f, true)]
        [TestCase(.1f, true)]
        [TestCase(.10001f, false)]
        public void SelectionAndActivationShareExactActualSpoolReachBoundary(float spoolDistance, bool expected)
        {
            // Keep the tested displacement on an axis whose origin is exactly zero:
            // adding 0.1 to a 2000m coordinate would test float quantization instead.
            using var f = new Fixture(verticalOrigin: 0f);
            var endpoint = f.Endpoint(0, Vector3.forward);
            f.Tool.transform.position = endpoint.transform.position + Vector3.up * spoolDistance;
            Physics.SyncTransforms();
            var context = new InteractionContext(f.Root, f.Root.transform.position, Vector3.forward);

            Assert.That(endpoint.CanSelectForCarriedObject(f.Tool, context), Is.EqualTo(expected));
            Assert.That(endpoint.CanActivateHeldTool(f.Tool, context), Is.EqualTo(expected));
            var candidate = f.Query.Query();
            bool selected = candidate.TryGetCapability(out SatsumaWiringConnectorInteractionTarget actual);
            Assert.That(selected, Is.EqualTo(expected));
            if (selected) Assert.That(actual, Is.SameAs(endpoint));
            Assert.That(f.System.IsEndpointArmed(endpoint.Connection, endpoint.Endpoint), Is.False,
                "Selection and availability queries must not arm a wire.");
        }

        [TestCase("own-parent", true)]
        [TestCase("unrelated-wall", false)]
        [TestCase("sibling-part", false)]
        [TestCase("wall-behind-own-parent", false)]
        public void BypassIsLimitedToOwnRegisteredAncestor(string scenario, bool expected)
        {
            using var f = new Fixture();
            var endpoint = f.Endpoint(0, new Vector3(0, 0, 1));
            f.Root.AddComponent<InteractionTargetHost>().Configure(f.System);
            if (scenario != "unrelated-wall") f.Solid(f.Root.transform, .35f, false);
            if (scenario == "unrelated-wall") f.Solid(null, .35f, false);
            if (scenario == "sibling-part") f.Solid(f.Root.transform, .65f, true);
            if (scenario == "wall-behind-own-parent") f.Solid(null, .65f, false);
            f.Tool.transform.position = endpoint.transform.position;
            Physics.SyncTransforms();
            var candidate = f.Query.Query();
            bool selected = candidate.TryGetCapability(out SatsumaWiringConnectorInteractionTarget actual) && actual == endpoint;
            Assert.That(selected, Is.EqualTo(expected));
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new GameObject("wiring owner fixture");
            public readonly SatsumaElectricalSystem System;
            public readonly WiringReachTestTool Tool;
            public readonly RaycastInteractionCandidateSource Query;
            private readonly GameObject toolObject = new GameObject("real spool fixture");
            private readonly GameObject ray = new GameObject("query fixture");
            private readonly System.Collections.Generic.List<GameObject> unrelated = new System.Collections.Generic.List<GameObject>();
            public Fixture(float verticalOrigin = 100f)
            {
                Root.transform.position = new Vector3(2000, verticalOrigin, 2000);
                System = Root.AddComponent<SatsumaElectricalSystem>();
                toolObject.AddComponent<Rigidbody>().isKinematic = true;
                Tool = toolObject.AddComponent<WiringReachTestTool>();
                toolObject.AddComponent<InteractionTargetHost>().Configure(Tool);
                ray.transform.position = Root.transform.position;
                Query = ray.AddComponent<RaycastInteractionCandidateSource>();
                Query.Configure(ray.transform, 2, ~0);
                Query.SetCarriedObjectTarget(Tool);
            }
            public SatsumaWiringConnectorInteractionTarget Endpoint(int end, Vector3 position)
            {
                var go = new GameObject("endpoint fixture"); go.transform.SetParent(Root.transform, false);
                go.transform.localPosition = position;
                var collider = go.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = .1f;
                var target = go.AddComponent<SatsumaWiringConnectorInteractionTarget>();
                target.Configure(System, SatsumaElectricalConnection.FrontLightsHarness, end, "connector");
                var host = go.AddComponent<InteractionTargetHost>(); host.Configure(target); host.ConfigureSelectionPriority(35);
                return target;
            }
            public void Solid(Transform parent, float z, bool registeredSibling)
            {
                var go = new GameObject("occluder fixture");
                if (parent != null) go.transform.SetParent(parent, false); else unrelated.Add(go);
                go.transform.position = Root.transform.position + Vector3.forward * z;
                var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(.4f, .4f, .1f);
                if (registeredSibling) go.AddComponent<InteractionTargetHost>();
            }
            public void Dispose()
            { Object.DestroyImmediate(Root); Object.DestroyImmediate(ray); Object.DestroyImmediate(toolObject);
                foreach (var go in unrelated) Object.DestroyImmediate(go); }
        }
    }

    public sealed class WiringReachTestTool : MonoBehaviour, IPickupTarget, IHeldToolIdentity
    {
        public string ToolType => "Wiring";
        public string ToolVariant => "mess";
        public string PickupPrompt => "test";
        public Rigidbody Body => GetComponent<Rigidbody>();
        public StableEntityId StableId => default;
        public bool CanPickup(in InteractionContext context) => true;
        public void NotifyPickedUp(in InteractionContext context) { }
        public void NotifyReleased(PickupReleaseReason reason) { }
    }
}
