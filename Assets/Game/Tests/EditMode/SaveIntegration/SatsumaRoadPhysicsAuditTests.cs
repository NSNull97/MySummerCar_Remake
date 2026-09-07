using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        // Opt-in read-only native diagnostic. Initial coast velocity is an
        // explicit test stimulus, NOT evidence of powertrain acceleration/FPS.
        public static IEnumerator RunNativeRoadPhysicsAudit()
        {
            SaveDocument native = null;
            WithReadOnlyNativeEngine(document => native = document);
            float previousScale = Time.timeScale;
            Time.timeScale = 1f;
            Fixture fixture = null;
            GameObject solidBarrier = null;
            Scene world = default;
            try
            {
                yield return SceneManager.LoadSceneAsync("World_Global_Legacy", LoadSceneMode.Additive);
                world = SceneManager.GetSceneByName("World_Global_Legacy");
                // The native fixture injects its own purchased-item provider.
                // Keep the real world's geometry, remove only this duplicate
                // composition component in the disposable test scene.
                foreach (var provider in world.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ItemPresentationProvider>(true)))
                    Object.DestroyImmediate(provider);
                fixture = new Fixture("item.spark-plug", "mount.satsuma.cylinder-head.spark-plug-1",
                    usePreviewScene: false, allEngineConsumables: true);
                fixture.Assembly.transform.parent.gameObject.SetActive(true);
                RestoreEngineDocument(fixture, native);
                foreach (var part in fixture.Assembly.AllRuntimeParts.Where(p => !p.IsInstalled && !p.IsAssemblyRoot))
                    part.gameObject.SetActive(false);
                var backend = fixture.Assembly.GetComponent<NwhWheelPhysicsBackend>();
                var front = fixture.Assembly.GetComponent<SatsumaFrontSuspensionController>();
                var body = backend.Chassis;
                using var poseProfiler = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "MSC.Vehicle.InstalledPoseSync", 1);
                SatsumaWheelContactPolicy contactPolicy = body.GetComponent<SatsumaWheelContactPolicy>();
                if (Environment.GetCommandLineArgs().Contains("-roadAuditEntityContactPolicy"))
                {
                    contactPolicy = contactPolicy != null ? contactPolicy : body.gameObject.AddComponent<SatsumaWheelContactPolicy>();
                    contactPolicy.Configure(backend);
                    foreach (var wheel in backend.Wheels) wheel.useContactModification = false;
                }
                var playerProxy = front.ChassisColliders.First(c => c != null && c.excludeLayers.value == ~(1 << LayerMask.NameToLayer("Player")))
                    .attachedRigidbody;
                Vector3 proxyLocal = body.transform.InverseTransformPoint(playerProxy.transform.position);
                if (Environment.GetCommandLineArgs().Contains("-roadAuditProxyFollower") && body.GetComponent<VehiclePlayerCollisionFollower>() == null)
                    body.gameObject.AddComponent<VehiclePlayerCollisionFollower>().Configure(body, playerProxy);
                if (Environment.GetCommandLineArgs().Contains("-roadAuditProxyNoInterpolation"))
                    playerProxy.interpolation = RigidbodyInterpolation.None;
                bool stockContact = Environment.GetCommandLineArgs().Contains("-roadAuditStockContact");
                bool noContact = Environment.GetCommandLineArgs().Contains("-roadAuditNoContactModifier");
                if (stockContact || noContact)
                {
                    if (contactPolicy != null) contactPolicy.enabled = false;
                    foreach (var wheel in backend.Wheels) wheel.useContactModification = stockContact;
                }
                TestContext.WriteLine("ROAD_CONTACT_EXPERIMENT stock=" + stockContact + " none=" + noContact);
                bool continuousDynamic = Environment.GetCommandLineArgs().Contains("-roadAuditContinuousDynamic");
                bool speculative = Environment.GetCommandLineArgs().Contains("-roadAuditSpeculative");
                foreach (var actor in body.GetComponentsInChildren<Rigidbody>(true).Where(a => !a.isKinematic))
                {
                    TestContext.WriteLine("ROAD_ACTOR " + actor.name + " CCD=" + actor.collisionDetectionMode + " mass=" + actor.mass);
                    if (continuousDynamic) actor.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    if (speculative) actor.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                }
                TestContext.WriteLine("ROAD_CCD_EXPERIMENT continuousDynamic=" + continuousDynamic);
                var probeType = Type.GetType("MSC.Tests.PlayMode.VehicleAssembly.SatsumaRoadAuditProbe, MSC.Tests.PlayMode", true);
                var contacts = new Dictionary<string, RoadContactStats>();
                bool collecting = false;
                foreach (var actor in body.GetComponentsInChildren<Rigidbody>(true))
                {
                    var probe = actor.gameObject.AddComponent(probeType);
                    probeType.GetField("CollisionObserved").SetValue(probe, (Action<Collision>)(collision =>
                    {
                        if (!collecting || collision.contactCount == 0) return;
                        var contact = collision.GetContact(0);
                        string key = contact.thisCollider.name + " -> " + contact.otherCollider.name;
                        if (!contacts.TryGetValue(key, out var stats)) contacts.Add(key, stats = new RoadContactStats());
                        stats.Count++;
                        if (collision.impulse.magnitude > stats.MaxImpulse)
                        {
                            stats.MaxImpulse = collision.impulse.magnitude;
                            stats.PeakPoint = contact.point;
                            stats.PeakNormal = contact.normal;
                            stats.PeakSeparation = contact.separation;
                            stats.PeakBodyPosition = body.position;
                        }
                        stats.MaxSpeed = Mathf.Max(stats.MaxSpeed, collision.relativeVelocity.magnitude);
                    }));
                }
                fixture.Assembly.GetComponent<SatsumaHandbrakeController>().TryRestore(new SatsumaHandbrakeSaveDto(), out _);
                fixture.Assembly.GetComponent<VehicleSimulationHost>().enabled = false;
                foreach (var wheel in backend.Wheels) { wheel.MotorTorque = 0; wheel.BrakeTorque = 0; }
                TestContext.WriteLine("ROAD_AUDIT nativeWrites=false handbrakeReleased=true coastStimulus=true body=" + body.position);
                foreach (var collider in body.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger))
                    TestContext.WriteLine("ROAD_COLLIDER name=" + collider.name + " layer=" + collider.gameObject.layer +
                        " actor=" + collider.attachedRigidbody?.name + " active=" + collider.gameObject.activeInHierarchy +
                        " local=" + body.transform.InverseTransformPoint(collider.bounds.center).ToString("R") + " size=" + collider.bounds.size);

                var roads = world.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true))
                    .Where(c => c.enabled && c.gameObject.activeInHierarchy &&
                        (c.transform.parent != null && (c.transform.parent.name.Contains("DirtRoad") ||
                         c.transform.parent.name.Contains("Road · Road ·")))).ToArray();
                TestContext.WriteLine("ROAD_AUDIT roadColliders=" + roads.Length);
                Assert.That(roads.Length, Is.GreaterThanOrEqualTo(2));
                var routes = new List<RoadAuditRoute>();
                foreach (var road in roads)
                {
                    Assert.That(TryFindStraightRoad(road, out Vector3 roadStart, out Vector3 direction), Is.True, road.name);
                    routes.Add(new RoadAuditRoute { Name = road.transform.parent.name, Start = roadStart, Forward = direction });
                }
                var rail = world.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true))
                    .First(c => c.transform.parent != null && c.transform.parent.name.Contains("RailCol"));
                Assert.That(TryFindRailCrossing(roads, rail, out var crossing), Is.True, "Actual dirt-road / rail crossing must be found.");
                routes.Add(crossing);
                if (Environment.GetCommandLineArgs().Contains("-roadAuditRailOnly")) routes.RemoveAll(r => !r.Rail);
                var gateFailures = new List<string>();
                foreach (var route in routes)
                {
                    Vector3 roadStart = route.Start, direction = route.Forward;
                    TestContext.WriteLine("ROAD_ROUTE " + route.Name + " start=" + roadStart.ToString("R") + " forward=" + direction.ToString("R"));
                    int routeSample = 0;
                    bool sweep = route.Rail && Environment.GetCommandLineArgs().Contains("-roadAuditRailSweep");
                    bool dense = route.Rail && Environment.GetCommandLineArgs().Contains("-roadAuditRailDense");
                    foreach (float speed in route.Rail ? (dense ? Enumerable.Range(0, 24).Select(i => i % 2 == 0 ? 8.333f : 16.667f).ToArray() : sweep ? new[] { 8.333f, 16.667f, 8.333f, 16.667f, 8.333f, 16.667f, 8.333f, 16.667f } : new[] { 8.333f, 16.667f }) : new[] { 0f, 8.333f, 16.667f, 30.556f })
                    {
                        if (Environment.GetCommandLineArgs().Contains("-roadAuditFreshRestore"))
                        {
                            var restoreReport = new UnresolvedContentReport();
                            var prepared = fixture.Registry.PrepareRestore(native, restoreReport, fixture.Deferred);
                            fixture.Registry.ApplyRestore(prepared, restoreReport, fixture.Deferred);
                            fixture.Assembly.GetComponent<SatsumaHandbrakeController>().TryRestore(new SatsumaHandbrakeSaveDto(), out _);
                            fixture.Assembly.GetComponent<VehicleSimulationHost>().enabled = false;
                            foreach (var wheel in backend.Wheels) { wheel.MotorTorque = 0; wheel.BrakeTorque = 0; }
                        }
                        RelocateRoadProbe(body, roadStart + direction * (sweep ? routeSample++ * .07f : 0) + Vector3.up * .85f, Quaternion.LookRotation(direction, Vector3.up));
                        fixture.Assembly.SynchronizeInstalledParts();
                        // Relocation resets BOTH chassis and rotor energy.
                        // Leaving the previous 110 km/h wheel spin alive can
                        // launch the car into the crossing during warmup.
                        foreach (var wheel in backend.Wheels) { wheel.wheel.angularVelocity = 0; wheel.Initialize(); }
                        Physics.SyncTransforms();
                        for (int i = 0; i < 70; i++) yield return new WaitForFixedUpdate();
                        TestContext.WriteLine("ROAD_SETTLED route=" + route.Name + " startOffset=" + Vector3.Dot(body.position - roadStart, direction) +
                            " speed=" + body.linearVelocity.magnitude + " tilt=" + Vector3.Angle(body.transform.up, Vector3.up));
                        contacts.Clear(); collecting = true;
                        SetRoadCoastVelocity(body, direction * speed);
                        foreach (var wheel in backend.Wheels) wheel.wheel.angularVelocity = speed / wheel.Radius;
                        float maxPoseError = 0, maxRoll = 0, maxAngular = 0, maxProxyError = 0;
                        double syncMs = 0; int samples = 0;
                        var chassisProbe = body.GetComponent(probeType);
                        probeType.GetField("AfterLateUpdate").SetValue(chassisProbe, (Action)(() =>
                        {
                            maxProxyError = Mathf.Max(maxProxyError, Vector3.Distance(proxyLocal,
                                body.transform.InverseTransformPoint(playerProxy.transform.position)));
                            foreach (var corner in front.Corners)
                            {
                                var wheel = corner.Wheel;
                                Vector3 expected = wheel.transform.position - wheel.transform.up * wheel.SpringLength;
                                Vector3 actual = corner.SpindleMount.transform.position -
                                    corner.Wheel.wheel.nonRotatingContainer.rotation * corner.HubToSpindleMeshLocalOffset;
                                maxPoseError = Mathf.Max(maxPoseError, Vector3.Distance(expected, actual));
                            }
                        }));
                        float endTime = Time.time + 1.5f;
                        while (Time.time < endTime)
                        {
                            yield return null;
                            maxRoll = Mathf.Max(maxRoll, Vector3.Angle(body.transform.up, Vector3.up));
                            maxAngular = Mathf.Max(maxAngular, body.angularVelocity.magnitude);
                            if (poseProfiler.Valid) syncMs += poseProfiler.LastValue * .000001;
                            samples++;
                        }
                        TestContext.WriteLine("ROAD_BAND road=" + route.Name + " initialKph=" + speed * 3.6f +
                            " finalKph=" + body.linearVelocity.magnitude * 3.6f + " maxHubErrorM=" + maxPoseError +
                            " maxTiltDeg=" + maxRoll + " maxAngular=" + maxAngular + " framePoseSyncMeanMs=" + (poseProfiler.Valid ? syncMs / samples : -1));
                        TestContext.WriteLine("ROAD_PROXY maxLocalErrorM=" + maxProxyError + " travelM=" + Vector3.Dot(body.position - roadStart, direction));
                        foreach (var entry in contacts.OrderByDescending(p => p.Value.MaxImpulse).Take(12))
                            TestContext.WriteLine("ROAD_PREASSERT_CONTACT " + entry.Key + " impulse=" + entry.Value.MaxImpulse +
                                " normal=" + entry.Value.PeakNormal + " point=" + entry.Value.PeakPoint.ToString("R"));
                        if (contactPolicy != null) TestContext.WriteLine("ROAD_POLICY seen=" + contactPolicy.SeenContacts + " changed=" + contactPolicy.ChangedContacts + " " + contactPolicy.ContactDiagnostics);
                        Assert.That(maxPoseError, Is.LessThan(.002f), "Rendered hub pose must follow the same chassis frame.");
                        if (!speculative)
                        {
                            if (maxRoll >= 15) gateFailures.Add(route.Name + " speed=" + speed + " tilt=" + maxRoll);
                            if (speed > 1 && body.linearVelocity.magnitude <= speed * .55f) gateFailures.Add(route.Name + " speed=" + speed + " severe coast loss=" + body.linearVelocity.magnitude);
                            if (route.Rail && Vector3.Dot(body.position - roadStart, direction) <= 8) gateFailures.Add("Whole vehicle did not clear both rails.");
                        }
                        collecting = false;
                        probeType.GetField("AfterLateUpdate").SetValue(chassisProbe, null);
                        foreach (var contact in contacts.OrderByDescending(p => p.Value.MaxImpulse).Take(12))
                            TestContext.WriteLine("ROAD_CONTACT " + contact.Key + " count=" + contact.Value.Count +
                                " maxImpulse=" + contact.Value.MaxImpulse + " maxRelativeSpeed=" + contact.Value.MaxSpeed);
                        foreach (var contact in contacts.Where(p => p.Value.MaxImpulse > 100))
                            TestContext.WriteLine("ROAD_PEAK " + contact.Key + " point=" + contact.Value.PeakPoint.ToString("R") +
                                " normal=" + contact.Value.PeakNormal.ToString("R") + " separation=" + contact.Value.PeakSeparation +
                                " body=" + contact.Value.PeakBodyPosition.ToString("R"));
                    }
                }
                if (!speculative)
                {
                    Assert.That(TryFindStraightRoad(roads[0], out var wallStart, out var wallForward), Is.True);
                    solidBarrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    solidBarrier.name = "Road audit solid barrier";
                    solidBarrier.layer = LayerMask.NameToLayer("WorldSolid");
                    solidBarrier.transform.SetPositionAndRotation(wallStart + wallForward * 10 + Vector3.up * 1.5f, Quaternion.LookRotation(wallForward));
                    solidBarrier.transform.localScale = new Vector3(8, 3, .3f);
                    RelocateRoadProbe(body, wallStart + Vector3.up * .85f, Quaternion.LookRotation(wallForward));
                    foreach (var wheel in backend.Wheels) { wheel.wheel.angularVelocity = 0; wheel.Initialize(); }
                    Physics.SyncTransforms();
                    for (int i = 0; i < 70; i++) yield return new WaitForFixedUpdate();
                    contacts.Clear(); collecting = true;
                    SetRoadCoastVelocity(body, wallForward * 8.333f);
                    foreach (var wheel in backend.Wheels) wheel.wheel.angularVelocity = 8.333f / wheel.Radius;
                    float furthest = 0;
                    for (int i = 0; i < 90; i++)
                    {
                        yield return new WaitForFixedUpdate();
                        furthest = Mathf.Max(furthest, Vector3.Dot(body.position - wallStart, wallForward));
                    }
                    collecting = false;
                    bool hitWall = contacts.Any(p => p.Key.Contains("Road audit solid barrier") && p.Value.MaxImpulse > 1);
                    TestContext.WriteLine("ROAD_WALL_GUARD hit=" + hitWall + " furthestM=" + furthest + " finalKph=" + body.linearVelocity.magnitude * 3.6f);
                    if (!hitWall || furthest >= 10) gateFailures.Add("Solid wall must block the car; lower-tread policy is not wall ghosting.");
                }
                Assert.That(gateFailures, Is.Empty, string.Join("\n", gateFailures));
            }
            finally
            {
                fixture?.Dispose();
                if (solidBarrier != null) Object.DestroyImmediate(solidBarrier);
                if (world.IsValid() && world.isLoaded) SceneManager.UnloadSceneAsync(world);
                Time.timeScale = previousScale;
            }
        }

        private sealed class RoadAuditRoute
        {
            public string Name;
            public Vector3 Start, Forward;
            public bool Rail;
        }

        private static bool TryFindRailCrossing(MeshCollider[] roads, BoxCollider rail, out RoadAuditRoute route)
        {
            Vector3 railDirection = Vector3.ProjectOnPlane(rail.transform.right, Vector3.up).normalized;
            int nearCandidates = 0;
            float nearestHeight = float.MaxValue;
            TestContext.WriteLine("ROAD_RAIL_GEOMETRY centre=" + rail.transform.TransformPoint(rail.center).ToString("R") + " axis=" + railDirection);
            for (float x = -rail.size.x * .5f + 10; x < rail.size.x * .5f - 10; x += .25f)
            {
                Vector3 railPoint = rail.transform.TransformPoint(rail.center + Vector3.right * x);
                foreach (var road in roads)
                {
                    // The road mesh has an intentional cut at the track bed:
                    // search its approaches, not an impossible road/rail overlap.
                    Vector3 across = Vector3.Cross(Vector3.up, railDirection);
                    if (!road.Raycast(new Ray(railPoint + across * 5 + Vector3.up * 10, Vector3.down), out var hit, 20) &&
                        !road.Raycast(new Ray(railPoint - across * 5 + Vector3.up * 10, Vector3.down), out hit, 20)) continue;
                    nearestHeight = Mathf.Min(nearestHeight, Mathf.Abs(hit.point.y - railPoint.y));
                    if (Mathf.Abs(hit.point.y - railPoint.y) > .4f) continue;
                    nearCandidates++;
                    for (int yaw = 0; yaw < 180; yaw += 5)
                    {
                        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        if (Mathf.Abs(Vector3.Dot(forward, railDirection)) > .8f) continue;
                        Vector3 side = Vector3.Cross(Vector3.up, forward);
                        bool valid = true;
                        for (int step = -5; step <= 10 && valid; step += 2)
                        for (int lateral = -1; lateral <= 1 && valid; lateral++)
                        {
                            Vector3 sample = railPoint + forward * step + side * (lateral * .8f);
                            valid = (Mathf.Abs(step) <= 3
                                ? Physics.Raycast(new Ray(sample + Vector3.up * 5, Vector3.down), out var ground, 10,
                                    LayerMask.GetMask("WorldSurface", "WorldSolid"), QueryTriggerInteraction.Ignore)
                                : road.Raycast(new Ray(sample + Vector3.up * 5, Vector3.down), out ground, 10)) &&
                                Mathf.Abs(ground.point.y - hit.point.y) < 1f;
                        }
                        if (!valid) continue;
                        road.Raycast(new Ray(railPoint - forward * 5 + Vector3.up * 5, Vector3.down), out var startHit, 10);
                        route = new RoadAuditRoute { Name = "Rail crossing " + road.transform.parent.name, Rail = true,
                            Start = startHit.point, Forward = forward };
                        TestContext.WriteLine("ROAD_RAIL_CROSSING point=" + hit.point.ToString("R") + " railHeight=" + (railPoint.y - hit.point.y));
                        return true;
                    }
                }
            }
            TestContext.WriteLine("ROAD_RAIL_SEARCH_FAILED nearCandidates=" + nearCandidates + " nearestHeight=" + nearestHeight);
            route = null; return false;
        }

        private sealed class RoadContactStats
        {
            public int Count;
            public float MaxImpulse;
            public float MaxSpeed;
            public Vector3 PeakPoint, PeakNormal, PeakBodyPosition;
            public float PeakSeparation;
        }

        private static void RelocateRoadProbe(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            var actors = body.GetComponentsInChildren<Rigidbody>(true);
            Vector3 origin = body.position;
            Quaternion delta = rotation * Quaternion.Inverse(body.rotation);
            var positions = actors.Select(a => position + delta * (a.position - origin)).ToArray();
            var rotations = actors.Select(a => delta * a.rotation).ToArray();
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i].position = positions[i]; actors[i].rotation = rotations[i];
                actors[i].transform.SetPositionAndRotation(positions[i], rotations[i]);
                if (!actors[i].isKinematic) { actors[i].linearVelocity = Vector3.zero; actors[i].angularVelocity = Vector3.zero; }
            }
        }

        private static void SetRoadCoastVelocity(Rigidbody body, Vector3 velocity)
        {
            // A rolling vehicle has one translational velocity, including its
            // jointed subframe and panels. Stimulating the chassis alone adds
            // an artificial initial impulse through all those joints.
            foreach (var actor in body.GetComponentsInChildren<Rigidbody>(true))
                if (!actor.isKinematic) { actor.linearVelocity = velocity; actor.angularVelocity = Vector3.zero; }
        }

        private static bool TryFindStraightRoad(MeshCollider road, out Vector3 start, out Vector3 forward)
        {
            var vertices = road.sharedMesh.vertices;
            var triangles = road.sharedMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = road.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = road.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = road.transform.TransformPoint(vertices[triangles[i + 2]]);
                Vector3 centre = (a + b + c) / 3;
                Vector3[] edges = { b - a, c - b, a - c };
                foreach (var edge in edges.OrderByDescending(e => e.sqrMagnitude))
                {
                    Vector3 direction = Vector3.ProjectOnPlane(edge, Vector3.up).normalized;
                    if (direction.sqrMagnitude < .5f) continue;
                    Vector3 right = Vector3.Cross(Vector3.up, direction);
                    bool valid = true;
                    for (int step = -2; step <= 14 && valid; step++)
                    for (int side = -1; side <= 1 && valid; side++)
                    {
                        Vector3 p = centre + direction * (step * 5) + right * (side * 1.2f);
                        valid = road.Raycast(new Ray(p + Vector3.up * 10, Vector3.down), out var hit, 20) &&
                            Vector3.Dot(hit.normal, Vector3.up) > .97f && Mathf.Abs(hit.point.y - centre.y) < 2;
                    }
                    if (valid) { start = centre; forward = direction; return true; }
                }
            }
            start = forward = default;
            return false;
        }
    }
}
