using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Bootstrap;
using MSC.Characters;
using MSC.Core.Time;
using MSC.NPC;
using MSC.Traffic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSC.Tests.PlayMode.Traffic
{
    public sealed class TrafficRoadAiPlayModeTests
    {
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ConfirmedSupportMesh_IsNotAnObstacle_ButPropIs()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var support = new GameObject("Pena_ContinuousSlopeSupport");
            support.layer = worldSurfaceLayer;
            var supportMesh = new Mesh
            {
                name = "Pena_ContinuousSlopeSupport_Mesh",
                vertices = new[]
                {
                    new Vector3(-4f, 0f, -10f),
                    new Vector3(4f, 0f, -10f),
                    new Vector3(-4f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(-4f, 3f, 4f),
                    new Vector3(4f, 3f, 4f),
                    new Vector3(-4f, 3f, 20f),
                    new Vector3(4f, 3f, 20f),
                },
                triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,
                    2, 4, 3, 3, 4, 5,
                    4, 6, 5, 5, 6, 7,
                },
            };
            supportMesh.RecalculateNormals();
            var supportCollider = support.AddComponent<MeshCollider>();
            supportCollider.sharedMesh = supportMesh;

            var vehicle = new GameObject("Pena_SupportProbe_TestVehicle");
            vehicle.transform.position = new Vector3(0f, 0.8f, -3f);
            var backend = vehicle.AddComponent<StationaryMotionBackend>();
            StoryTrafficVehiclePresentationBinding binding =
                vehicle.AddComponent<
                    StoryTrafficVehiclePresentationBinding>();
            binding.ConfigureMotionBackendForAuthoring(backend);
            binding.SetConfirmedSupportColliderObstacleRejection(true);
            binding.ConfigureDrivingProfile(
                8f,
                12f,
                4f,
                12f,
                120f,
                -2f);
            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            body.isKinematic = true;
            Physics.SyncTransforms();
            binding.SetPhysicalRouteGuidanceTarget(
                new Vector3(0f, 3f, 12f),
                Quaternion.identity,
                0.25f);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(binding.TrackedObstacleName, Is.Empty,
                "The exact non-convex mesh supporting the chassis was " +
                "misclassified as a forward obstacle on its slope face.");
            Assert.That(binding.ManeuverState,
                Is.EqualTo(StoryTrafficManeuverState.Cruise));

            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "Pena_RealRoadsideProp";
            prop.layer = worldSurfaceLayer;
            prop.transform.position = new Vector3(0f, 1.35f, -0.25f);
            prop.transform.localScale = new Vector3(1.5f, 2.7f, 1.2f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(binding.TrackedObstacleName,
                Is.EqualTo(prop.name),
                "Support rejection must remain exact-collider scoped; a " +
                "separate WorldSurface prop still has to block the car.");
            Assert.That(binding.ManeuverState,
                Is.Not.EqualTo(StoryTrafficManeuverState.Cruise));

            Object.Destroy(vehicle);
            Object.Destroy(prop);
            Object.Destroy(support);
            Object.Destroy(supportMesh);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BusHeadingRejoin_HoldsProgressAuthorityUntilFullyRecovered()
        {
            bool rejoinActive = TrafficWorldRuntime
                .ResolveBusRouteRejoinActive(
                    wasActive: false,
                    routeSeparationMeters: 0.5f,
                    horizontalHeadingDot: 0f);
            Assert.That(rejoinActive, Is.True,
                "A centred bus facing across/against BusRoute must enter " +
                "physical route recovery.");
            Assert.That(TrafficWorldRuntime
                    .CanCommitBusPhysicalProjection(
                        rejoinActive,
                        insideRouteCorridor: true,
                        projectionCommitAllowed: true),
                Is.False,
                "Logical progress must not follow a wrong-way chassis while " +
                "the physical bus is rejoining.");
            Assert.That(TrafficWorldRuntime
                    .ResolveBusRouteSpeedCapMetersPerSecond(
                        segmentIndex: 100,
                        routeSeparationMeters: 0.5f,
                        routeRejoinActive: rejoinActive),
                Is.EqualTo(6.5f).Within(0.001f));

            yield return new WaitForFixedUpdate();

            rejoinActive = TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: rejoinActive,
                routeSeparationMeters: 3.5f,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinExitHeadingDot);
            Assert.That(rejoinActive, Is.True,
                "Being back inside the corridor is insufficient while the " +
                "bus heading is still on the exit threshold.");
            Assert.That(TrafficWorldRuntime.CanCommitBusPhysicalProjection(
                rejoinActive,
                insideRouteCorridor: true,
                projectionCommitAllowed: true), Is.False);

            yield return new WaitForFixedUpdate();

            rejoinActive = TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: rejoinActive,
                routeSeparationMeters: 3.5f,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinExitHeadingDot + 0.01f);
            Assert.That(rejoinActive, Is.False);
            Assert.That(TrafficWorldRuntime.CanCommitBusPhysicalProjection(
                rejoinActive,
                insideRouteCorridor: true,
                projectionCommitAllowed: true), Is.True,
                "Progress authority may resume only after both separation " +
                "and heading satisfy the exit gate.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NwhAmbientPrefabs_RestOnFlatWorldSurface()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Traffic_NwhFlatGround";
            // Keep the authored logical route deliberately below the support
            // surface. This reproduces production 4tie, where materialization
            // must ground-conform the Rigidbody before restoring streamed
            // motion state.
            ground.transform.position = new Vector3(0f, 0.55f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            if (worldSurfaceLayer >= 0)
            {
                ground.layer = worldSurfaceLayer;
            }

            string prefabRoot =
                "Assets/Game/LegacyImport/RuntimeBaseline/Characters/" +
                "StoryTraffic/Generated/AmbientPrefabs/";
            var instances = new List<StoryTrafficVehiclePresentationBinding>();
            foreach (string prefabName in new[]
                     {
                         "traffic-lamore",
                         "traffic-victro",
                     })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabRoot + prefabName + ".prefab");
                Assert.That(prefab, Is.Not.Null, prefabName);
                GameObject instance = Object.Instantiate(prefab);
                instance.name = prefabName + "_FlatGroundTest";
                float x = instances.Count * 5f;
                instance.transform.SetPositionAndRotation(
                    new Vector3(x, 0f, 0f),
                    Quaternion.identity);
                StoryTrafficVehiclePresentationBinding presentation =
                    instance.GetComponent<
                        StoryTrafficVehiclePresentationBinding>();
                Assert.That(presentation, Is.Not.Null, prefabName);
                Rigidbody body = instance.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null, prefabName);
                StoryTrafficMotionRuntimeState retainedState =
                    presentation.CaptureRuntimeState();
                Physics.SyncTransforms();
                presentation.SnapToRoutePoseTarget(
                    new Vector3(x, 0f, 0f),
                    Quaternion.identity);
                float groundedSnapY = body.position.y;
                Assert.That(groundedSnapY, Is.GreaterThan(0.9f),
                    prefabName + " did not conform to the elevated support.");
                presentation.RestoreRuntimeState(retainedState);
                Assert.That(body.position.y,
                    Is.EqualTo(groundedSnapY).Within(0.01f),
                    prefabName + " restored the stale pre-snap Transform pose.");
                instances.Add(presentation);
            }

            for (int step = 0; step < 300; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            foreach (StoryTrafficVehiclePresentationBinding instance in
                     instances)
            {
                Assert.That(instance.transform.position.y,
                    Is.InRange(-0.15f, 1.5f),
                    instance.name + " did not remain on a flat road surface.");
                Assert.That(instance.HasPhysicalGroundContact, Is.True,
                    instance.name + " lost all NWH wheel contacts.");
                Object.Destroy(instance.gameObject);
            }

            Object.Destroy(ground);
            yield return null;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator HighwayTraffic_UsesSeparateLanesWithoutTeleporting()
        {
            const string bootstrapScenePath =
                "Assets/Game/Bootstrap/Bootstrap.unity";
            var initialScenes = new HashSet<SceneHandle>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                initialScenes.Add(SceneManager.GetSceneAt(index).handle);
            }

            yield return SceneManager.LoadSceneAsync(
                bootstrapScenePath,
                LoadSceneMode.Additive);
            yield return null;
            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);
            if (!installer.IsGameplayPrepared)
            {
                Assert.That(installer.TryBeginGameplayPreparation(
                    out string preparationFailure), Is.True,
                    preparationFailure);
                for (int frame = 0;
                     frame < 900 && !installer.IsGameplayPrepared;
                     frame++)
                {
                    yield return null;
                }
            }

            Assert.That(installer.IsGameplayPrepared, Is.True,
                installer.LastGameplayPreparationFailure);
            Time.timeScale = 1f;
            TrafficWorldRuntime traffic = Object.FindFirstObjectByType<
                TrafficWorldRuntime>(FindObjectsInactive.Exclude);
            Assert.That(traffic, Is.Not.Null);
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(
                "Assets/Game/Traffic/Content/Phase1/" +
                "TrafficRoadNetworkCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var carShopOutCenter = new Vector3(
                1584f,
                10.6f,
                882.5f) +
                TrafficWorldRuntime.SourceToProjectTrafficTranslation;
            Vector3 carShopGateNormal =
                Quaternion.Euler(0f, -166.408f, 0f) * Vector3.forward;
            installer.SpawnedPlayer.transform.position =
                carShopOutCenter - carShopGateNormal * 2f;
            yield return null;
            installer.SpawnedPlayer.transform.position =
                carShopOutCenter + carShopGateNormal * 2f;
            yield return null;

            TrafficRouteDefinition route = catalog.Routes.Single(value =>
                value.RouteId == "route.traffic.highway");
            var geometry = new TrafficRouteGeometry(route);
            var highwayPresentationNames = new HashSet<string>(
                catalog.Actors
                    .Where(value => string.Equals(
                        value.RouteId,
                        route.RouteId,
                        System.StringComparison.Ordinal))
                    .Select(value =>
                        "Traffic_Presentation_" + value.ActorId));
            TrafficRouteSample playerSample = geometry.Resolve(0.405f, true);
            installer.SpawnedPlayer.transform.position =
                playerSample.Position +
                playerSample.Rotation * Vector3.right * 25f +
                Vector3.up * 2f;
            yield return installer.WorldStreaming.RefreshNow();

            AmbientTrafficActorStateDto[] actors =
                traffic.CaptureAmbientActors();
            int forwardIndex = 0;
            int reverseIndex = 0;
            foreach (AmbientTrafficActorStateDto actor in actors)
            {
                TrafficActorDefinition definition = catalog.Actors.Single(
                    value => value.ActorId == actor.actorId);
                // Pena is intentionally persistent and has his own dirt-road
                // phase below. Keep this phase limited to the donor's ten
                // VehiclesHighway actors so its +2 m lane contract is exact.
                actor.active = string.Equals(
                    definition.RouteId,
                    route.RouteId,
                    System.StringComparison.Ordinal);
                actor.hasPhysicalPose = false;
                actor.routeId = definition.RouteId;
                actor.travelsForward = definition.TravelsForward;
                actor.routeProgress01 = actor.travelsForward
                    ? 0.397f + forwardIndex++ * 0.003f
                    : 0.407f + reverseIndex++ * 0.003f;
                actor.currentSpeedMetersPerSecond = 0f;
                actor.cruiseSpeedMetersPerSecond =
                    actor.desiredSpeedMetersPerSecond;
                actor.laneOffsetMeters = definition.BaseLaneOffsetMeters;
                actor.maneuverState =
                    (int)StoryTrafficManeuverState.Cruise;
                actor.maneuverStateSeconds = 0f;
                actor.hasSafePose = false;
                actor.worldRotation = Quaternion.identity;
                actor.safeRotation = Quaternion.identity;
            }

            Assert.That(traffic.TryRestoreAmbientActors(
                actors, out string restoreFailure), Is.True, restoreFailure);
            yield return new WaitForFixedUpdate();

            StoryTrafficVehiclePresentationBinding[] initialHighwayResidents =
                Object.FindObjectsByType<
                        StoryTrafficVehiclePresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(value => highwayPresentationNames.Contains(
                        value.name))
                    .ToArray();
            Assert.That(initialHighwayResidents.Length,
                Is.EqualTo(
                    TrafficWorldRuntime
                        .MaximumMaterializedOrdinaryAmbientActors),
                "Ordinary highway traffic must use bounded physical " +
                "residency instead of instantiating the complete root.");
            var initialResidentNames = new HashSet<string>(
                initialHighwayResidents.Select(value => value.name));
            AmbientTrafficActorStateDto logicalActorBefore = traffic
                .CaptureAmbientActors()
                .First(value =>
                    highwayPresentationNames.Contains(
                        "Traffic_Presentation_" + value.actorId) &&
                    !initialResidentNames.Contains(
                        "Traffic_Presentation_" + value.actorId));

            var firstPositions = new Dictionary<string, Vector3>();
            var previousPositions = new Dictionary<string, Vector3>();
            var maximumSteps = new Dictionary<string, float>();
            var maximumPhysicalSpeeds = new Dictionary<string, float>();
            var consecutiveAirborneSteps = new Dictionary<string, int>();
            var maximumAirborneSteps = new Dictionary<string, int>();
            var lastGroundContact = new Dictionary<string, bool>();
            var subscribed = new HashSet<string>();
            var crashes = new List<string>();
            int maximumSimultaneousHighwayResidents = 0;
            for (int step = 0; step < 600; step++)
            {
                yield return new WaitForFixedUpdate();
                StoryTrafficVehiclePresentationBinding[] residents = Object
                    .FindObjectsByType<
                              StoryTrafficVehiclePresentationBinding>(
                              FindObjectsInactive.Exclude,
                              FindObjectsSortMode.None)
                              .Where(value =>
                                  highwayPresentationNames.Contains(
                                      value.name))
                              .ToArray();
                maximumSimultaneousHighwayResidents = Mathf.Max(
                    maximumSimultaneousHighwayResidents,
                    residents.Length);
                foreach (StoryTrafficVehiclePresentationBinding motion in
                         residents)
                {
                    string id = motion.name;
                    if (!firstPositions.ContainsKey(id))
                    {
                        firstPositions.Add(id, motion.transform.position);
                        previousPositions.Add(id, motion.transform.position);
                        maximumSteps.Add(id, 0f);
                        maximumPhysicalSpeeds.Add(id, 0f);
                        consecutiveAirborneSteps.Add(id, 0);
                        maximumAirborneSteps.Add(id, 0);
                        lastGroundContact.Add(
                            id,
                            motion.HasPhysicalGroundContact);
                    }

                    if (subscribed.Add(id))
                    {
                        motion.CollisionIncident += incident =>
                        {
                            if (!incident.IsCrash)
                            {
                                return;
                            }

                            string detail = id + " hit " +
                                (incident.Other != null
                                    ? incident.Other.name
                                    : "null") + " at " +
                                incident.SpeedMetersPerSecond.ToString("F2") +
                                " m/s, position=" + motion.transform.position +
                                ", lane=" +
                                motion.CurrentLaneOffsetMeters.ToString("F2");
                            crashes.Add(detail);
                            Debug.Log("Traffic AI test crash: " + detail);
                        };
                    }

                    maximumSteps[id] = Mathf.Max(
                        maximumSteps[id],
                        Vector3.Distance(previousPositions[id],
                            motion.transform.position));
                    previousPositions[id] = motion.transform.position;
                    maximumPhysicalSpeeds[id] = Mathf.Max(
                        maximumPhysicalSpeeds[id],
                        motion.PhysicalVelocityMetersPerSecond.magnitude);
                    bool hasGroundContact = motion.HasPhysicalGroundContact;
                    if (lastGroundContact[id] && !hasGroundContact)
                    {
                        Debug.Log(
                            "Traffic AI test left ground: " + id +
                            ", position=" + motion.transform.position +
                            ", velocity=" +
                            motion.PhysicalVelocityMetersPerSecond +
                            ", lane=" +
                            motion.CurrentLaneOffsetMeters.ToString("F2") +
                            ", targetDistance=" +
                            motion.LastPhysicalTargetDistanceMeters.ToString(
                                "F2"));
                    }

                    lastGroundContact[id] = hasGroundContact;
                    if (step >= 100 && !hasGroundContact)
                    {
                        consecutiveAirborneSteps[id]++;
                        maximumAirborneSteps[id] = Mathf.Max(
                            maximumAirborneSteps[id],
                            consecutiveAirborneSteps[id]);
                    }
                    else
                    {
                        consecutiveAirborneSteps[id] = 0;
                    }

                    Assert.That(motion.BaseLaneOffsetMeters,
                        Is.EqualTo(2f).Within(0.001f));
                }
            }

            int movingActors = previousPositions.Count(pair =>
                Vector3.Distance(firstPositions[pair.Key], pair.Value) > 10f);
            Assert.That(maximumSimultaneousHighwayResidents,
                Is.LessThanOrEqualTo(
                    TrafficWorldRuntime
                        .MaximumMaterializedOrdinaryAmbientActors),
                "Ordinary traffic exceeded its physical residency budget.");
            string movementDetail = string.Join(
                " | ",
                firstPositions.Select(pair =>
                    pair.Key + ": travel=" +
                    Vector3.Distance(pair.Value, previousPositions[pair.Key])
                        .ToString("F1") +
                    ", maxSpeed=" +
                    maximumPhysicalSpeeds[pair.Key].ToString("F1") +
                    ", maxAirborne=" + maximumAirborneSteps[pair.Key]));
            Assert.That(movingActors, Is.GreaterThanOrEqualTo(3),
                movementDetail);
            Assert.That(crashes, Is.Empty,
                string.Join(" | ", crashes));
            Assert.That(maximumSteps.Values.Max(), Is.LessThan(5f),
                "Ambient traffic teleported instead of recovering physically.");
            Assert.That(maximumPhysicalSpeeds.Values.Max(), Is.LessThan(45f),
                "Ambient traffic was launched beyond a plausible road speed. " +
                movementDetail);
            Assert.That(maximumAirborneSteps.Values.Max(), Is.LessThan(75),
                "Ambient traffic remained airborne for at least 1.5 seconds. " +
                movementDetail);

            AmbientTrafficActorStateDto logicalActorAfter = traffic
                .CaptureAmbientActors()
                .Single(value => value.actorId ==
                    logicalActorBefore.actorId);
            Assert.That(Mathf.Abs(
                    logicalActorAfter.routeProgress01 -
                    logicalActorBefore.routeProgress01),
                Is.GreaterThan(0.000001f),
                "A non-resident ordinary actor did not advance in real time.");

            // Vacate the retained-residency bubble first. Otherwise the six
            // lawful hysteresis residents still own the physical budget while
            // the player is moved directly to a seventh logical actor.
            installer.SpawnedPlayer.transform.position =
                new Vector3(50000f, 50000f, 50000f);
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            logicalActorAfter = traffic
                .CaptureAmbientActors()
                .Single(value => value.actorId ==
                    logicalActorBefore.actorId);
            TrafficActorDefinition logicalDefinition = catalog.Actors.Single(
                value => value.ActorId == logicalActorAfter.actorId);
            TrafficRouteSample logicalSample = geometry.Resolve(
                logicalActorAfter.routeProgress01,
                logicalActorAfter.travelsForward);
            installer.SpawnedPlayer.transform.position =
                logicalSample.Position + Vector3.up * 2f;
            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                if (Object.FindObjectsByType<
                        StoryTrafficVehiclePresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Any(value => value.name ==
                        "Traffic_Presentation_" +
                        logicalDefinition.ActorId))
                {
                    break;
                }
            }

            StoryTrafficVehiclePresentationBinding rematerialized = Object
                .FindObjectsByType<
                    StoryTrafficVehiclePresentationBinding>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .SingleOrDefault(value => value.name ==
                    "Traffic_Presentation_" + logicalDefinition.ActorId);
            Assert.That(rematerialized, Is.Not.Null,
                "Ordinary traffic did not materialize when the player " +
                "approached its current logical route pose.");
            Assert.That(Vector3.Distance(
                    rematerialized.PhysicalWorldPosition,
                    logicalSample.Position), Is.LessThan(45f),
                "Ordinary traffic rematerialized at a stale physical pose.");

            TrafficRouteDefinition busRoute = catalog.Routes.Single(value =>
                value.RouteId == "route.traffic.bus");
            var busGeometry = new TrafficRouteGeometry(busRoute);
            float busStartProgress = busGeometry.ProgressAtPointIndex(3);
            TrafficRouteSample busStart = busGeometry.Resolve(
                busStartProgress,
                true);
            Vector3 busSavedPhysicalPosition = busStart.Position +
                busStart.Rotation * Vector3.right * 1.25f;
            Quaternion busSavedWrongWayRotation = busStart.Rotation *
                Quaternion.Euler(0f, 180f, 0f);
            installer.SpawnedPlayer.transform.position =
                busStart.Position +
                busStart.Rotation * Vector3.right * 25f +
                Vector3.up * 2f;
            yield return installer.WorldStreaming.RefreshNow();

            TransportTrafficActorStateDto[] transports =
                traffic.CaptureTransportActors();
            foreach (TransportTrafficActorStateDto transport in transports)
            {
                transport.active = false;
                transport.hasPhysicalPose = false;
                transport.currentSpeedMetersPerSecond = 0f;
                if (transport.transportId != "traffic.transport.bus")
                {
                    continue;
                }

                transport.active = true;
                transport.routeId = busRoute.RouteId;
                transport.routeProgress01 = busStartProgress;
                transport.dwellGameSecondsRemaining = 0f;
                transport.nextStopIndex = 0;
                transport.lastDepartureAbsoluteHour = 0;
                transport.hasPhysicalPose = true;
                transport.worldPosition = busSavedPhysicalPosition;
                transport.worldRotation = busSavedWrongWayRotation;
            }

            Assert.That(traffic.TryRestoreTransportActors(
                transports, out string transportFailure), Is.True,
                transportFailure);
            TrafficTransportPresentationBinding bus = null;
            for (int frame = 0; frame < 120 && bus == null; frame++)
            {
                yield return new WaitForFixedUpdate();
                bus = Object.FindObjectsByType<
                        TrafficTransportPresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(value =>
                        value.Kind == TrafficTransportKind.Bus);
            }

            Assert.That(bus, Is.Not.Null,
                "The active bus did not materialize near its Perajarvi origin.");
            Vector3 busMaterializedPosition =
                bus.RoadMotion.PhysicalWorldPosition;
            Assert.That(Vector2.Distance(
                    new Vector2(
                        busMaterializedPosition.x,
                        busMaterializedPosition.z),
                    new Vector2(
                        busSavedPhysicalPosition.x,
                        busSavedPhysicalPosition.z)),
                Is.LessThan(1.5f),
                "Wrong-way pose recovery moved a valid near-route bus " +
                "position instead of canonicalizing only its rotation.");
            Assert.That(TrafficWorldRuntime.ResolveHorizontalHeadingDot(
                    bus.RoadMotion.PhysicalWorldRotation,
                    busStart.Rotation),
                Is.GreaterThan(0.8f),
                "The materialized bus retained the saved yaw+180 heading.");
            Vector3 busInitialPosition = bus.transform.position;
            Vector3 busPreviousPosition = busInitialPosition;
            float busMaximumStep = 0f;
            float busMaximumSpeed = 0f;
            float busMaximumRoadSpeed = 0f;
            int busMaximumGear = 0;
            int busConsecutiveAirborneSteps = 0;
            int busMaximumAirborneSteps = 0;
            var busCrashes = new List<string>();
            bus.RoadMotion.CollisionIncident += incident =>
            {
                if (incident.IsCrash)
                {
                    busCrashes.Add(incident.Other != null
                        ? incident.Other.name
                        : "unknown obstacle");
                }
            };

            // Donor BUS/Start StoppingWait is 25 real seconds. Keep another
            // 25 physical seconds after that dwell: the 9.8-tonne provisional
            // chassis should reach road speed without being tuned like a car.
            for (int step = 0; step < 2500; step++)
            {
                yield return new WaitForFixedUpdate();
                busMaximumStep = Mathf.Max(
                    busMaximumStep,
                    Vector3.Distance(busPreviousPosition,
                        bus.transform.position));
                busPreviousPosition = bus.transform.position;
                busMaximumSpeed = Mathf.Max(
                    busMaximumSpeed,
                    bus.Body.linearVelocity.magnitude);
                busMaximumRoadSpeed = Mathf.Max(
                    busMaximumRoadSpeed,
                    bus.RoadMotion.CurrentSpeedMetersPerSecond);
                busMaximumGear = Mathf.Max(
                    busMaximumGear,
                    bus.RoadMotion.PhysicalSelectedGear);
                if (step >= 100 &&
                    !bus.RoadMotion.HasPhysicalGroundContact)
                {
                    busConsecutiveAirborneSteps++;
                    busMaximumAirborneSteps = Mathf.Max(
                        busMaximumAirborneSteps,
                        busConsecutiveAirborneSteps);
                }
                else
                {
                    busConsecutiveAirborneSteps = 0;
                }
            }

            float busTravel = Vector3.Distance(
                busInitialPosition,
                bus.transform.position);
            TransportTrafficActorStateDto busState = traffic
                .CaptureTransportActors()
                .Single(value => value.transportId ==
                    "traffic.transport.bus");
            string busStateDetail =
                $"progress={busState.routeProgress01:F6}, " +
                $"dwell={busState.dwellGameSecondsRemaining:F2}, " +
                $"nextStop={busState.nextStopIndex}, " +
                $"roadSpeed={bus.RoadMotion.CurrentSpeedMetersPerSecond:F2}, " +
                $"gear={bus.RoadMotion.PhysicalSelectedGear}";
            Assert.That(busCrashes, Is.Empty,
                "Bus crashed near the sewage-disposal station: " +
                string.Join(", ", busCrashes) + ". " + busStateDetail);
            Assert.That(busTravel, Is.GreaterThan(100f),
                "Bus remained behind the sewage-disposal station after its " +
                "scheduled stop. " + busStateDetail);
            Assert.That(bus.RoadMotion.CurrentSpeedMetersPerSecond,
                Is.GreaterThan(2f),
                "Bus did not resume driving after its scheduled stop.");
            Assert.That(busState.routeProgress01,
                Is.GreaterThan(busStartProgress + 0.0001f),
                "The physically materialized bus never advanced forward " +
                "from its canonicalized saved pose.");
            Assert.That(busMaximumRoadSpeed, Is.GreaterThanOrEqualTo(12f),
                "The bus did not exceed an urban crawl on the first short " +
                "Perajarvi leg. Its separate donor speed-cap assertion " +
                "protects 82.5 km/h for the open-road section.");
            Assert.That(busMaximumGear, Is.GreaterThanOrEqualTo(3),
                "The bus never advanced beyond its low ratios.");
            Assert.That(busMaximumStep, Is.LessThan(5f),
                "Bus teleported while leaving Perajarvi.");
            Assert.That(busMaximumSpeed, Is.LessThan(30f),
                "Bus was launched beyond a plausible road speed.");
            Assert.That(busMaximumAirborneSteps, Is.LessThan(75),
                "Bus remained airborne for at least 1.5 seconds.");

            // Regression for the repeatable inside-shoulder stall behind the
            // sewage-disposal station. Start from a retained passing-lane
            // pose as well: materialization must deterministically restore
            // the forward donor lane instead of continuing oncoming traffic.
            const int busBendStartPointIndex =
                TrafficWorldRuntime.BusWastewaterBendEntryPointIndex;
            const int busBendEndPointIndex =
                TrafficWorldRuntime.BusWastewaterBendExitPointIndex;
            const int busHillRecoveryExitPointIndex =
                TrafficWorldRuntime.BusHillDriveAssistEndPointIndex + 2;
            float busBendStartProgress = busGeometry.ProgressAtPointIndex(
                busBendStartPointIndex);
            float busBendEndProgress = busGeometry.ProgressAtPointIndex(
                busBendEndPointIndex);
            float busHillRecoveryExitProgress =
                busGeometry.ProgressAtPointIndex(
                    busHillRecoveryExitPointIndex);
            TrafficRouteSample busBendStart = busGeometry.Resolve(
                busBendStartProgress,
                true);
            installer.SpawnedPlayer.transform.position =
                busBendStart.Position +
                busBendStart.Rotation * Vector3.right * 25f +
                Vector3.up * 2f;
            yield return installer.WorldStreaming.RefreshNow();

            transports = traffic.CaptureTransportActors();
            foreach (TransportTrafficActorStateDto transport in transports)
            {
                transport.active = false;
                transport.hasPhysicalPose = false;
                transport.currentSpeedMetersPerSecond = 0f;
                if (transport.transportId != "traffic.transport.bus")
                {
                    continue;
                }

                transport.active = true;
                transport.routeId = busRoute.RouteId;
                transport.routeProgress01 = busBendStartProgress;
                transport.dwellGameSecondsRemaining = 0f;
                transport.nextStopIndex = 0;
                transport.hasPhysicalPose = true;
                transport.worldPosition = busBendStart.Position +
                    busBendStart.Rotation * Vector3.right * -4f;
                transport.worldRotation = busBendStart.Rotation;
            }

            Assert.That(traffic.TryRestoreTransportActors(
                transports, out transportFailure), Is.True,
                transportFailure);
            bus = null;
            for (int frame = 0; frame < 120 && bus == null; frame++)
            {
                yield return new WaitForFixedUpdate();
                bus = Object.FindObjectsByType<
                        TrafficTransportPresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(value =>
                        value.Kind == TrafficTransportKind.Bus);
            }

            Assert.That(bus, Is.Not.Null,
                "The bus did not materialize at bend waypoint B44.");
            Assert.That(bus.RoadMotion.RoadBehaviorProfile,
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.Bus));
            Assert.That(bus.RoadMotion.BaseLaneOffsetMeters,
                Is.EqualTo(0f).Within(0.01f));

            int busBendHint = busBendStartPointIndex;
            float busMaximumRouteSeparation = 0f;
            int busConsecutiveStallSteps = 0;
            int busMaximumStallSteps = 0;
            int busConsecutiveReverseSteps = 0;
            int busMaximumReverseSteps = 0;
            int busReverseEntries = 0;
            bool busWasReversing = false;
            bool busReachedDrivingSpeed = false;
            bool busPassedWastewaterBend = false;
            bool busPassedHillRecovery = false;
            bool busObservedHillDriveAssist = false;
            bool busObservedRouteRejoin = false;
            bool busExitedRouteRejoin = false;
            bool busCommittedProgressDuringRouteRejoin = false;
            int busMaximumSteepHillGear = 0;
            float busMinimumSteepHillSpeed = float.PositiveInfinity;
            float busMinimumDirectionAlignment = 1f;
            for (int step = 0; step < 3000; step++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 physical = bus.RoadMotion.PhysicalWorldPosition;
                Assert.That(busGeometry.TryProjectAndResolveAhead(
                    physical,
                    busBendStartProgress,
                    true,
                    6f,
                    ref busBendHint,
                    out TrafficRouteSample bendProjection,
                    out _), Is.True);
                float separation = Vector3.Distance(
                    physical,
                    bendProjection.Position);
                busMaximumRouteSeparation = Mathf.Max(
                    busMaximumRouteSeparation,
                    separation);
                Vector3 busForward = Vector3.ProjectOnPlane(
                    bus.RoadMotion.PhysicalWorldRotation * Vector3.forward,
                    Vector3.up).normalized;
                Vector3 routeForward = Vector3.ProjectOnPlane(
                    bendProjection.Rotation * Vector3.forward,
                    Vector3.up).normalized;
                busMinimumDirectionAlignment = Mathf.Min(
                    busMinimumDirectionAlignment,
                    Vector3.Dot(busForward, routeForward));

                float speed = bus.RoadMotion.CurrentSpeedMetersPerSecond;
                busReachedDrivingSpeed |= speed >= 2f;
                if (busReachedDrivingSpeed && speed < 0.35f)
                {
                    busConsecutiveStallSteps++;
                    busMaximumStallSteps = Mathf.Max(
                        busMaximumStallSteps,
                        busConsecutiveStallSteps);
                }
                else
                {
                    busConsecutiveStallSteps = 0;
                }

                bool reversing = bus.RoadMotion.IsReversing;
                if (reversing)
                {
                    busConsecutiveReverseSteps++;
                    busMaximumReverseSteps = Mathf.Max(
                        busMaximumReverseSteps,
                        busConsecutiveReverseSteps);
                    if (!busWasReversing)
                    {
                        busReverseEntries++;
                    }
                }
                else
                {
                    busConsecutiveReverseSteps = 0;
                }

                busWasReversing = reversing;
                if (bendProjection.SegmentIndex >=
                        TrafficWorldRuntime.BusHillDriveAssistStartPointIndex &&
                    bendProjection.SegmentIndex <=
                        TrafficWorldRuntime.BusHillDriveAssistEndPointIndex)
                {
                    busObservedHillDriveAssist |=
                        bus.RoadMotion.IsHillDriveAssistActive;
                }

                if (bendProjection.SegmentIndex >=
                        TrafficWorldRuntime.BusSteepHillStartPointIndex &&
                    bendProjection.SegmentIndex <=
                        TrafficWorldRuntime.BusSteepHillEndPointIndex)
                {
                    busMaximumSteepHillGear = Mathf.Max(
                        busMaximumSteepHillGear,
                        bus.RoadMotion.PhysicalSelectedGear);
                    busMinimumSteepHillSpeed = Mathf.Min(
                        busMinimumSteepHillSpeed,
                        speed);
                }

                Assert.That(traffic.TryGetTransportState(
                    "traffic.transport.bus",
                    out TransportTrafficActorStateDto bendState), Is.True);
                if (bus.RoadMotion.IsRouteRejoinActive)
                {
                    busObservedRouteRejoin = true;
                    busCommittedProgressDuringRouteRejoin |= Mathf.Abs(
                        bendState.routeProgress01 -
                        busBendStartProgress) > 0.000001f;
                }
                else if (busObservedRouteRejoin)
                {
                    busExitedRouteRejoin = true;
                }

                if (bendState.routeProgress01 >= busBendEndProgress)
                {
                    busPassedWastewaterBend = true;
                }

                if (bendState.routeProgress01 >=
                    busHillRecoveryExitProgress)
                {
                    busPassedHillRecovery = true;
                    break;
                }
            }

            Assert.That(busPassedWastewaterBend, Is.True,
                "Bus did not physically pass B72 on the wastewater bend.");
            Assert.That(busPassedHillRecovery, Is.True,
                "Bus did not physically pass the climb and leave the B52-B80 " +
                "hill-drive corridor.");
            Assert.That(busObservedHillDriveAssist, Is.True,
                "The NWH bus drivetrain never received the authored hill " +
                "drive-assist state.");
            Assert.That(busObservedRouteRejoin, Is.True,
                "The 4 m retained physical offset never entered live bus " +
                "route rejoin.");
            Assert.That(busExitedRouteRejoin, Is.True,
                "The bus did not physically regain its route corridor and " +
                "forward heading.");
            Assert.That(busCommittedProgressDuringRouteRejoin, Is.False,
                "Logical bus progress advanced while its physical chassis " +
                "was still outside the accepted route corridor.");
            Assert.That(busMaximumSteepHillGear,
                Is.InRange(1, 4),
                "The bus retained a torque-starved tall ratio on the " +
                "6.4-9% B61-B68 climb.");
            Assert.That(busMinimumSteepHillSpeed,
                Is.GreaterThan(0.35f),
                "The bus lost all road speed on the steepest authored climb.");
            Assert.That(bus.RoadMotion.IsHillDriveAssistActive, Is.False,
                "The bus kept its hill-specific drivetrain policy after " +
                "leaving B80.");
            Assert.That(busMaximumRouteSeparation,
                Is.LessThanOrEqualTo(4.5f),
                "Bus left the authored 3.75 m recovery corridor.");
            Assert.That(busMaximumStallSteps, Is.LessThan(250),
                "Bus stalled for five seconds on the wastewater bend.");
            Assert.That(busReverseEntries, Is.LessThanOrEqualTo(1),
                "Bus entered a repeated forward/reverse recovery loop.");
            Assert.That(busMaximumReverseSteps, Is.LessThan(250),
                "Bus remained in reverse for five seconds.");
            Assert.That(busMinimumDirectionAlignment, Is.GreaterThan(0f),
                "Bus restored against the donor route direction.");
            Assert.That(Mathf.Abs(
                    bus.RoadMotion.CurrentLaneOffsetMeters -
                    bus.RoadMotion.BaseLaneOffsetMeters),
                Is.LessThan(0.5f),
                "Bus did not return from the retained passing lane.");

            // A route-perfect rolling pass alone does not protect the live
            // failure: a tall-gear bus could lose all inertia at the 9% ramp
            // and its former launch-clutch tuning could not move 9.8 tonnes
            // again. Restart at B66 with zero road speed and require the real
            // gearbox, clutch and driven wheels to clear the crest at B69.
            const int busHillRestartPointIndex = 66;
            float busHillRestartProgress = busGeometry.ProgressAtPointIndex(
                busHillRestartPointIndex);
            TrafficRouteSample busHillRestart = busGeometry.Resolve(
                busHillRestartProgress,
                true);
            installer.SpawnedPlayer.transform.position =
                busHillRestart.Position +
                busHillRestart.Rotation * Vector3.right * 25f +
                Vector3.up * 2f;
            yield return installer.WorldStreaming.RefreshNow();

            transports = traffic.CaptureTransportActors();
            foreach (TransportTrafficActorStateDto transport in transports)
            {
                transport.active = false;
                transport.hasPhysicalPose = false;
                transport.currentSpeedMetersPerSecond = 0f;
                if (transport.transportId != "traffic.transport.bus")
                {
                    continue;
                }

                transport.active = true;
                transport.routeId = busRoute.RouteId;
                transport.routeProgress01 = busHillRestartProgress;
                transport.dwellGameSecondsRemaining = 0f;
                transport.nextStopIndex = 0;
                transport.hasPhysicalPose = true;
                transport.worldPosition = busHillRestart.Position;
                transport.worldRotation = busHillRestart.Rotation;
            }

            Assert.That(traffic.TryRestoreTransportActors(
                transports, out transportFailure), Is.True,
                transportFailure);
            bus = null;
            for (int frame = 0; frame < 120 && bus == null; frame++)
            {
                yield return new WaitForFixedUpdate();
                bus = Object.FindObjectsByType<
                        TrafficTransportPresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(value =>
                        value.Kind == TrafficTransportKind.Bus);
            }

            Assert.That(bus, Is.Not.Null,
                "The bus did not materialize for the stopped B66 hill " +
                "recovery regression.");
            bool busRestartClearedHill = false;
            bool busRestartEngineRunning = false;
            int busRestartReverseEntries = 0;
            bool busRestartWasReversing = false;
            int busRestartStationarySteps = 0;
            int busRestartMaximumStationarySteps = 0;
            float busRestartMaximumStep = 0f;
            Vector3 busRestartPreviousPosition =
                bus.RoadMotion.PhysicalWorldPosition;
            for (int step = 0; step < 2000; step++)
            {
                yield return new WaitForFixedUpdate();
                busRestartEngineRunning |=
                    bus.RoadMotion.PhysicalEngineRpm > 500f;
                float restartSpeed =
                    bus.RoadMotion.CurrentSpeedMetersPerSecond;
                if (busRestartEngineRunning && restartSpeed < 0.35f)
                {
                    busRestartStationarySteps++;
                    busRestartMaximumStationarySteps = Mathf.Max(
                        busRestartMaximumStationarySteps,
                        busRestartStationarySteps);
                }
                else
                {
                    busRestartStationarySteps = 0;
                }

                bool restartReversing = bus.RoadMotion.IsReversing;
                if (restartReversing && !busRestartWasReversing)
                {
                    busRestartReverseEntries++;
                }

                busRestartWasReversing = restartReversing;
                Vector3 restartPosition =
                    bus.RoadMotion.PhysicalWorldPosition;
                busRestartMaximumStep = Mathf.Max(
                    busRestartMaximumStep,
                    Vector3.Distance(
                        busRestartPreviousPosition,
                        restartPosition));
                busRestartPreviousPosition = restartPosition;
                Assert.That(traffic.TryGetTransportState(
                    "traffic.transport.bus",
                    out TransportTrafficActorStateDto restartState), Is.True);
                if (restartState.routeProgress01 >=
                    busHillRecoveryExitProgress)
                {
                    busRestartClearedHill = true;
                    break;
                }
            }

            Assert.That(busRestartEngineRunning, Is.True,
                "The B66 hill restart never established a running diesel.");
            Assert.That(busRestartClearedHill, Is.True,
                "The stopped bus did not restart physically at B66 and " +
                "clear the B69 crest/B82 recovery exit.");
            Assert.That(busRestartMaximumStationarySteps, Is.LessThan(250),
                "The bus remained torque-stalled on the B66 climb for five " +
                "seconds after its engine was running.");
            Assert.That(busRestartReverseEntries, Is.Zero,
                "The hill drivetrain fix fell back to route reversing instead " +
                "of restarting through its driven wheels.");
            Assert.That(busRestartMaximumStep, Is.LessThan(5f),
                "The stopped B66 bus escaped the hill by teleporting.");

            const string penaActorId =
                "traffic.ambient.dirt-road.pena";
            TrafficActorDefinition penaDefinition = catalog.Actors.Single(
                value => value.ActorId == penaActorId);
            TrafficRouteDefinition dirtRoute = catalog.Routes.Single(value =>
                value.RouteId == "route.traffic.dirt-road");
            var dirtGeometry = new TrafficRouteGeometry(dirtRoute);
            AmbientTrafficActorStateDto[] cousinStates =
                traffic.CaptureAmbientActors();
            const int penaSlopeRegressionStartPointIndex = 48;
            float penaSlopeRegressionStartProgress =
                dirtGeometry.ProgressAtPointIndex(
                    penaSlopeRegressionStartPointIndex);
            foreach (AmbientTrafficActorStateDto actor in cousinStates)
            {
                actor.active = actor.actorId == penaActorId;
                actor.hasPhysicalPose = false;
                actor.currentSpeedMetersPerSecond = 0f;
                actor.cruiseSpeedMetersPerSecond =
                    actor.desiredSpeedMetersPerSecond;
                actor.hasSafePose = false;
                if (actor.actorId != penaActorId)
                {
                    continue;
                }

                actor.cousinStateVersion = 1;
                actor.cousinContext =
                    (int)CousinTrafficContext.OrdinaryFittan;
                actor.routeId = dirtRoute.RouteId;
                actor.routeProgress01 = penaSlopeRegressionStartProgress;
                actor.travelsForward = penaDefinition.TravelsForward;
                actor.laneOffsetMeters = penaDefinition.BaseLaneOffsetMeters;
                actor.maneuverState =
                    (int)StoryTrafficManeuverState.Cruise;
                actor.maneuverStateSeconds = 0f;
                actor.worldRotation = Quaternion.identity;
                actor.safeRotation = Quaternion.identity;
            }

            TrafficRouteSample penaStart = dirtGeometry.Resolve(
                penaSlopeRegressionStartProgress,
                penaDefinition.TravelsForward);
            var carShopInCenter = new Vector3(
                1585.34f,
                10.6f,
                888.04f) +
                TrafficWorldRuntime.SourceToProjectTrafficTranslation;
            installer.SpawnedPlayer.transform.position =
                carShopInCenter - carShopGateNormal * 2f;
            yield return null;
            installer.SpawnedPlayer.transform.position =
                carShopInCenter + carShopGateNormal * 2f;
            yield return null;
            installer.SpawnedPlayer.transform.position =
                penaStart.Position +
                penaStart.Rotation * Vector3.right * 24f +
                Vector3.up * 2f;
            yield return installer.WorldStreaming.RefreshNow();
            Assert.That(traffic.TryRestoreAmbientActors(
                cousinStates, out restoreFailure), Is.True, restoreFailure);

            StoryTrafficVehiclePresentationBinding pena = null;
            for (int frame = 0; frame < 180 && pena == null; frame++)
            {
                yield return new WaitForFixedUpdate();
                pena = Object.FindObjectsByType<
                        StoryTrafficVehiclePresentationBinding>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(value => value.name ==
                        "Traffic_Presentation_" + penaActorId);
            }

            Assert.That(pena, Is.Not.Null,
                "Pena/KUSKI did not materialize on the dirt road.");
            Assert.That(pena.DriverFeatureId, Is.EqualTo("P1.NPC.102"),
                "The ordinary dirt-road actor did not use Pena's wrapper.");
            Vector3 penaPreviousPosition = pena.PhysicalWorldPosition;
            float penaMaximumStep = 0f;
            float penaMaximumSpeed = 0f;
            float penaMinimumLane = pena.CurrentLaneOffsetMeters;
            float penaMaximumLane = pena.CurrentLaneOffsetMeters;
            int penaAirborneSteps = 0;
            int penaMaximumAirborneSteps = 0;
            int penaStationarySteps = 0;
            int penaMaximumStationarySteps = 0;
            int penaProjectionHint = penaSlopeRegressionStartPointIndex;
            float penaMaximumRouteSeparation = 0f;
            float penaMinimumUprightDot = 1f;
            float penaTravelMeters = 0f;
            bool penaEngineRunning = false;
            var penaCrashes = new List<string>();
            pena.CollisionIncident += incident =>
            {
                if (incident.IsCrash)
                {
                    penaCrashes.Add(incident.Other != null
                        ? incident.Other.name
                        : "unknown obstacle");
                }
            };
            for (int step = 0; step < 900; step++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 penaPosition = pena.PhysicalWorldPosition;
                float penaStepDistance = Vector3.Distance(
                    penaPreviousPosition,
                    penaPosition);
                penaMaximumStep = Mathf.Max(
                    penaMaximumStep,
                    penaStepDistance);
                penaTravelMeters += penaStepDistance;
                penaPreviousPosition = penaPosition;
                penaMaximumSpeed = Mathf.Max(
                    penaMaximumSpeed,
                    pena.PhysicalVelocityMetersPerSecond.magnitude);
                penaEngineRunning |= pena.PhysicalEngineRpm > 500f;
                penaMinimumLane = Mathf.Min(
                    penaMinimumLane,
                    pena.CurrentLaneOffsetMeters);
                penaMaximumLane = Mathf.Max(
                    penaMaximumLane,
                    pena.CurrentLaneOffsetMeters);
                if (step >= 100 && !pena.HasPhysicalGroundContact)
                {
                    penaAirborneSteps++;
                    penaMaximumAirborneSteps = Mathf.Max(
                        penaMaximumAirborneSteps,
                        penaAirborneSteps);
                }
                else
                {
                    penaAirborneSteps = 0;
                }

                if (step >= 150 && penaEngineRunning &&
                    pena.CurrentSpeedMetersPerSecond < 0.35f)
                {
                    penaStationarySteps++;
                    penaMaximumStationarySteps = Mathf.Max(
                        penaMaximumStationarySteps,
                        penaStationarySteps);
                }
                else
                {
                    penaStationarySteps = 0;
                }

                if (step >= 100 &&
                    dirtGeometry.TryProjectAndResolveAheadWithinClosedPointRange(
                        penaPosition,
                        penaSlopeRegressionStartProgress,
                        forward: true,
                        lookAheadMeters: 9f,
                        firstPointIndex: 16,
                        lastPointIndex: 3718,
                        ref penaProjectionHint,
                        out TrafficRouteSample penaProjection,
                        out _))
                {
                    penaMaximumRouteSeparation = Mathf.Max(
                        penaMaximumRouteSeparation,
                        Vector3.Distance(
                            penaPosition,
                            penaProjection.Position));
                }

                penaMinimumUprightDot = Mathf.Min(
                    penaMinimumUprightDot,
                    Vector3.Dot(
                        pena.PhysicalWorldRotation * Vector3.up,
                        Vector3.up));
            }

            Assert.That(penaTravelMeters, Is.GreaterThan(80f),
                "Pena stalled on the long winding/slope dirt-road pass.");
            Assert.That(penaCrashes, Is.Empty,
                "Pena crashed during the baseline dirt-road pass: " +
                string.Join(", ", penaCrashes));
            Assert.That(penaMaximumStep, Is.LessThan(5f),
                "Pena teleported instead of driving physically.");
            Assert.That(penaMaximumSpeed, Is.LessThan(30f),
                "Pena was launched beyond a plausible dirt-road speed.");
            Assert.That(penaMaximumAirborneSteps, Is.LessThan(75),
                "Pena remained airborne for at least 1.5 seconds.");
            Assert.That(penaMaximumStationarySteps, Is.LessThan(250),
                "Pena remained drivetrain-stalled for at least five seconds " +
                "on the steep dirt-road corridor.");
            Assert.That(penaMaximumRouteSeparation, Is.LessThan(4f),
                "Pena left the retained 5.6 m dirt-road corridor.");
            Assert.That(penaMinimumUprightDot, Is.GreaterThan(0.35f),
                "Pena rolled over during physical slope/rejoin handling.");
            Assert.That(penaMaximumLane - penaMinimumLane,
                Is.GreaterThan(0.15f),
                "Pena did not use the configured drunk lane wander.");

            GameCompositionRoot compositionRoot = GameCompositionRoot.ActiveRoot;
            if (compositionRoot != null)
            {
                DisableRegistryReactiveWeatherBridgeBeforeTeardown(
                    compositionRoot);
                compositionRoot.SendMessage(
                    "EndSessionAndDestroy",
                    SendMessageOptions.RequireReceiver);
                yield return null;
            }

            var addedScenes = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && !initialScenes.Contains(scene.handle))
                {
                    addedScenes.Add(scene);
                }
            }

            foreach (Scene scene in addedScenes)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
        }

        private static void DisableRegistryReactiveWeatherBridgeBeforeTeardown(
            GameCompositionRoot compositionRoot)
        {
            const string bridgeTypeName =
                "MSC.Weather.Enviro3Integration." +
                "Enviro3WeatherZoneRemovalBridge";
            Behaviour[] behaviours = compositionRoot.GetComponentsInChildren<
                Behaviour>(includeInactive: true);
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour != null &&
                    behaviour.enabled &&
                    string.Equals(
                        behaviour.GetType().FullName,
                        bridgeTypeName,
                        System.StringComparison.Ordinal))
                {
                    // The bridge subscribes to WeatherZoneRegistry.Changed.
                    // Disable it while the composition root is still active
                    // so its OnDisable detaches first; otherwise zone teardown
                    // asks it to create children while the parent is itself
                    // being deactivated, which Unity correctly rejects. This
                    // is test cleanup ordering only, not traffic behavior.
                    behaviour.enabled = false;
                }
            }
        }

        private sealed class StationaryMotionBackend : MonoBehaviour,
            IStoryTrafficVehicleMotionBackend
        {
            public bool IsOperational => true;
            public float SpeedMetersPerSecond => 0f;
            public Vector3 VelocityMetersPerSecond => Vector3.zero;
            public bool HasGroundContact => true;
            public float EngineRpm => 900f;
            public float EngineRedlineRpm => 6000f;
            public float EngineLoad01 => 0f;
            public int SelectedGear => 1;

            public void Step(
                float fixedDeltaSeconds,
                in StoryTrafficVehicleDriveCommand command)
            {
            }

            public void SnapToPose(
                Vector3 position,
                Quaternion rotation,
                float forwardSpeedMetersPerSecond)
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            public void ResetMotion()
            {
            }

            public bool TryGetWheelVisualState(
                int wheelIndex,
                out float steeringAngleDegrees,
                out float axleAngleDegrees)
            {
                steeringAngleDegrees = 0f;
                axleAngleDegrees = 0f;
                return false;
            }
        }
#endif
    }
}
