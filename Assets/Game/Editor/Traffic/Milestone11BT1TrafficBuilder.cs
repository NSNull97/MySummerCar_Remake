using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Bootstrap;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Traffic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Traffic
{
    public static class Milestone11BT1TrafficBuilder
    {
        private const string ContentRoot =
            "Assets/Game/Traffic/Content/Phase1";
        private const string CatalogPath = ContentRoot +
            "/TrafficRoadNetworkCatalog.asset";
        private const string PresentationCatalogPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters/" +
            "StoryTraffic/Generated/Resources/Phase1Traffic/" +
            "TrafficPresentationCatalog.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string BuildReportPath =
            "Docs/Milestones/MILESTONE_11B_T2_BUILD_REPORT.json";
        private const string HighwayRouteId = "route.traffic.highway";

        private static readonly string[] RoadRouteIds =
        {
            "route.traffic.highway",
            "route.traffic.dirt-road",
            "route.traffic.village",
            "route.traffic.home-road",
            "route.traffic.dancehall",
            "route.traffic.drag-race",
            "route.traffic.road-race",
            "route.traffic.track-field",
            "route.traffic.mod-town-entry",
            "route.traffic.mod-town-loop",
            "route.traffic.mod-gas-pump",
        };

        private static readonly string[] TransportRouteIds =
        {
            "route.traffic.bus",
            "route.traffic.boat-1",
            "route.traffic.boat-2",
            "route.traffic.train-east-to-west",
            "route.traffic.train-west-to-east",
        };

        [MenuItem(
            "Tools/My Summer Car/Phase 1/Build Complete Traffic (11B-T2)")]
        public static void BuildFromMenu()
        {
            Build(includePrivatePresentation: true);
            EditorUtility.DisplayDialog(
                "Milestone 11B-T2",
                "Road traffic, public bus, train and lake traffic were rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() =>
            Build(includePrivatePresentation: true);

        public static void BuildCatalogFromBatch() =>
            Build(includePrivatePresentation: false);

        public static void Build(bool includePrivatePresentation)
        {
            if (includePrivatePresentation)
            {
                Phase1StoryTrafficPresentationImporter.Build();
            }

            LockedTrafficRouteSet evidence =
                Phase1TrafficRouteEvidence.LoadLockedRouteSet();
            IReadOnlyList<LockedAmbientTrafficActorEvidence> actorEvidence =
                Phase1TrafficRouteEvidence
                    .LoadLockedAmbientTrafficEvidence();
            LockedTrafficTransportEvidence transportEvidence =
                Phase1TrafficRouteEvidence.LoadLockedTransportEvidence();
            TrafficRouteDefinition[] routes = CreateRoutes(evidence);
            TrafficRoadConnectionDefinition[] connections =
                CreateConnections(routes);
            TrafficActorDefinition[] actors = CreateActors(
                routes,
                actorEvidence);
            TrafficTransportDefinition[] transports = CreateTransports(
                transportEvidence);
            TrafficEventActorDefinition[] eventActors = CreateEventActors(
                routes);

            EnsureFolder(ContentRoot);
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                    TrafficRoadNetworkCatalog>(CatalogPath) ??
                ScriptableObject.CreateInstance<TrafficRoadNetworkCatalog>();
            bool isNew = string.IsNullOrWhiteSpace(
                AssetDatabase.GetAssetPath(catalog));
            catalog.name = "Phase1TrafficRoadNetworkCatalog";
            catalog.ConfigureForAuthoring(
                "catalog.traffic.phase1.11b-t2.v1",
                evidence.SourceSceneSha256,
                routes,
                connections,
                actors,
                transports,
                eventActors);
            if (!catalog.TryValidate(out string failure))
            {
                if (isNew)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }

                throw new InvalidOperationException(
                    "Generated traffic road network is invalid: " + failure);
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            TrafficPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<TrafficPresentationCatalog>(
                    PresentationCatalogPath) ??
                throw new InvalidOperationException(
                    "Private ambient traffic presentation catalog is missing. " +
                    "Run the story/traffic presentation importer first.");
            if (!presentation.TryValidate(out failure))
            {
                throw new InvalidOperationException(
                    "Generated ambient traffic presentation is invalid: " +
                    failure);
            }

            BindBootstrap(catalog, presentation);
            WriteReport(
                evidence,
                actorEvidence,
                routes,
                connections,
                actors,
                transports,
                presentation);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"Milestone 11B-T2 built: {routes.Length} exact donor " +
                $"routes, {connections.Length} graph connections, " +
                $"{actors.Length} persistent highway actors and " +
                $"{transports.Length} route transports with " +
                $"{presentation.Entries.Count} physical presentation archetypes.");
        }

        private static TrafficRouteDefinition[] CreateRoutes(
            LockedTrafficRouteSet evidence)
        {
            IReadOnlyDictionary<string, LockedTrafficRoute> expansionRoutes =
                Phase1TrafficRouteEvidence
                    .LoadTrafficExpansionRouteEvidence()
                    .ToDictionary(value => value.RouteId,
                        StringComparer.Ordinal);
            return RoadRouteIds.Concat(TransportRouteIds).Select(routeId =>
            {
                LockedTrafficRoute donor = expansionRoutes.TryGetValue(
                    routeId,
                    out LockedTrafficRoute expansion)
                        ? expansion
                        : evidence.RequireRoute(routeId);
                return new TrafficRouteDefinition(
                    routeId,
                    donor.DonorHierarchyPath,
                    ClosesLoop(routeId),
                    ResolveSurface(routeId),
                    ResolveRoadWidth(routeId),
                    donor.ProjectWorldPoints);
            }).ToArray();
        }

        private static TrafficTransportDefinition[] CreateTransports(
            LockedTrafficTransportEvidence evidence)
        {
            TrafficBusDepartureDefinition[] departures = evidence.BusDepartures
                .Select(value => new TrafficBusDepartureDefinition(
                    value.Hour,
                    value.RouteStartPointIndex,
                    value.OriginId))
                .ToArray();
            TrafficRouteStopDefinition[] stops = evidence.BusStops
                .OrderBy(value => value.RouteDistanceMeters)
                .Select(value => new TrafficRouteStopDefinition(
                    value.StopId,
                    value.RouteDistanceMeters,
                    // Donor BUS/Start FSM `StoppingWait` is 25 seconds.
                    configuredDwellSimulationSeconds: 25f))
                .ToArray();
            return new[]
            {
                new TrafficTransportDefinition(
                    "traffic.transport.bus",
                    DeriveStableGuid("traffic.transport.bus"),
                    "P1.TRAFFIC.004",
                    "presentation.traffic.bus",
                    TrafficTransportKind.Bus,
                    "route.traffic.bus",
                    string.Empty,
                    evidence.BusSpeedMetersPerSecond,
                    configuredMaterializeDistanceMeters: 650f,
                    configuredDematerializeDistanceMeters: 800f,
                    configuredEndpointDelaySimulationSeconds: 0f,
                    departures,
                    stops),
                new TrafficTransportDefinition(
                    "traffic.transport.train",
                    DeriveStableGuid("traffic.transport.train"),
                    "P1.TRAFFIC.005",
                    "presentation.traffic.train",
                    TrafficTransportKind.Train,
                    "route.traffic.train-east-to-west",
                    "route.traffic.train-west-to-east",
                    evidence.TrainSpeedMetersPerSecond,
                    configuredMaterializeDistanceMeters: 1400f,
                    configuredDematerializeDistanceMeters: 1650f,
                    evidence.TrainEndpointDelaySimulationSeconds,
                    Array.Empty<TrafficBusDepartureDefinition>(),
                    Array.Empty<TrafficRouteStopDefinition>()),
                new TrafficTransportDefinition(
                    "traffic.transport.boat-1",
                    DeriveStableGuid("traffic.transport.boat-1"),
                    "P1.TRAFFIC.006",
                    "presentation.traffic.boat-1",
                    TrafficTransportKind.Boat,
                    "route.traffic.boat-1",
                    string.Empty,
                    evidence.BoatSpeedMetersPerSecond,
                    configuredMaterializeDistanceMeters: 900f,
                    configuredDematerializeDistanceMeters: 1050f,
                    configuredEndpointDelaySimulationSeconds: 0f,
                    Array.Empty<TrafficBusDepartureDefinition>(),
                    Array.Empty<TrafficRouteStopDefinition>()),
                new TrafficTransportDefinition(
                    "traffic.transport.boat-2",
                    DeriveStableGuid("traffic.transport.boat-2"),
                    "P1.TRAFFIC.006",
                    "presentation.traffic.boat-2",
                    TrafficTransportKind.Boat,
                    "route.traffic.boat-2",
                    // Donor AIboat2 only uses Waypoints2 for its randomized
                    // spawn. It then targets the matching Waypoints1 index and
                    // remains on that common loop.
                    "route.traffic.boat-1",
                    evidence.BoatSpeedMetersPerSecond,
                    configuredMaterializeDistanceMeters: 900f,
                    configuredDematerializeDistanceMeters: 1050f,
                    configuredEndpointDelaySimulationSeconds: 0f,
                    Array.Empty<TrafficBusDepartureDefinition>(),
                    Array.Empty<TrafficRouteStopDefinition>()),
            };
        }

        private static TrafficRoadConnectionDefinition[] CreateConnections(
            IReadOnlyList<TrafficRouteDefinition> routes)
        {
            var definitions = new List<TrafficRoadConnectionDefinition>();
            TrafficRouteDefinition[] graphRoutes = routes
                .Where(value => RoadRouteIds.Contains(
                    value.RouteId,
                    StringComparer.Ordinal))
                .ToArray();
            AddBidirectional(
                definitions,
                "village-road-race",
                routes,
                "route.traffic.village",
                22,
                "route.traffic.road-race",
                622);
            AddBidirectional(
                definitions,
                "road-race-highway",
                routes,
                "route.traffic.road-race",
                248,
                "route.traffic.highway",
                464);

            // Discover the remaining donor-authored junctions by the nearest
            // samples. The dense route sets are the authority; the 3 m guard
            // prevents unrelated crossings from becoming graph edges.
            for (int leftIndex = 0; leftIndex < graphRoutes.Length; leftIndex++)
            for (int rightIndex = leftIndex + 1;
                 rightIndex < graphRoutes.Length;
                 rightIndex++)
            {
                TrafficRouteDefinition left = graphRoutes[leftIndex];
                TrafficRouteDefinition right = graphRoutes[rightIndex];
                if (definitions.Any(value =>
                        value.FromRouteId == left.RouteId &&
                        value.ToRouteId == right.RouteId))
                {
                    continue;
                }

                FindNearestPair(
                    left.WorldPoints,
                    right.WorldPoints,
                    out int leftPoint,
                    out int rightPoint,
                    out float separation);
                if (separation <= 3f)
                {
                    string id = Slug(left.RouteId) + "-" + Slug(right.RouteId);
                    AddBidirectional(
                        definitions,
                        id,
                        routes,
                        left.RouteId,
                        leftPoint,
                        right.RouteId,
                        rightPoint);
                }
            }

            return definitions
                .OrderBy(value => value.ConnectionId, StringComparer.Ordinal)
                .ToArray();
        }

        private static TrafficActorDefinition[] CreateActors(
            IReadOnlyList<TrafficRouteDefinition> routes,
            IReadOnlyList<LockedAmbientTrafficActorEvidence> evidence)
        {
            TrafficRouteDefinition highway = routes.Single(value =>
                value.RouteId == HighwayRouteId);
            var actors = evidence.Select(actor =>
            {
                ProjectOntoRoute(
                    highway,
                    actor.ProjectPosition,
                    out float progress,
                    out Vector3 tangent);
                Vector3 donorForward = actor.ProjectRotation * Vector3.forward;
                donorForward.y = 0f;
                tangent.y = 0f;
                bool forward = donorForward.sqrMagnitude <= 0.0001f ||
                               tangent.sqrMagnitude <= 0.0001f ||
                               Vector3.Dot(
                                   donorForward.normalized,
                                   tangent.normalized) >= 0f;
                ResolveActorTuning(
                    actor.ArchetypeId,
                    out string vehicleFeatureId,
                    out string driverFeatureId,
                    out float minimumSpeed,
                    out float maximumSpeed);
                bool menace = actor.ArchetypeId == "menace";
                bool beerTruck = actor.ArchetypeId == "truck";
                string actorId = "traffic.ambient.highway." +
                                 actor.InstanceSuffix;
                return new TrafficActorDefinition(
                    actorId,
                    DeriveStableGuid(actorId),
                    vehicleFeatureId,
                    driverFeatureId,
                    "presentation.traffic." + actor.ArchetypeId,
                    HighwayRouteId,
                    progress,
                    forward,
                    minimumSpeed,
                    maximumSpeed,
                    menace ? 0.1f : 1f,
                    menace ? 0.4f : 1f,
                    configuredInactiveRetryGameSeconds: 60f,
                    configuredDeterministicSeed: unchecked((int)
                        actor.DonorTransformFileId),
                    configuredDrivingProfile:
                        TrafficDrivingProfile.Standard,
                    configuredRoutePolicy: beerTruck
                        ? TrafficRoutePolicy.LoopOnly
                        : TrafficRoutePolicy
                            .TrafficExpansionTownExcursions,
                    configuredBaseLaneOffsetMeters: 2f,
                    configuredPassingLaneOffsetMeters: -2f);
            }).ToList();

            LockedCousinTrafficEvidence cousin = Phase1TrafficRouteEvidence
                .LoadLockedCousinTrafficEvidence();
            TrafficRouteDefinition dirtRoad = routes.Single(value =>
                value.RouteId == "route.traffic.dirt-road");
            ProjectOntoRoute(
                dirtRoad,
                cousin.ProjectPosition,
                out float cousinProgress,
                out Vector3 cousinTangent);
            Vector3 cousinForward = cousin.ProjectRotation * Vector3.forward;
            cousinForward.y = 0f;
            cousinTangent.y = 0f;
            bool cousinTravelsForward = cousinForward.sqrMagnitude <= 0.0001f ||
                                        cousinTangent.sqrMagnitude <= 0.0001f ||
                                        Vector3.Dot(
                                            cousinForward.normalized,
                                            cousinTangent.normalized) >= 0f;
            const string cousinActorId = "traffic.ambient.dirt-road.pena";
            actors.Add(new TrafficActorDefinition(
                cousinActorId,
                DeriveStableGuid(cousinActorId),
                "P1.VEHICLE.112",
                "P1.NPC.102",
                TrafficCousinBehaviorRules.OrdinaryPresentationId,
                "route.traffic.dirt-road",
                cousinProgress,
                cousinTravelsForward,
                configuredMinimumSpeedMetersPerSecond: 95f / 3.6f,
                configuredMaximumSpeedMetersPerSecond: 105f / 3.6f,
                configuredWeekdaySpawnProbability01: 1f,
                configuredWeekendSpawnProbability01: 1f,
                configuredInactiveRetryGameSeconds: 60f,
                configuredDeterministicSeed: unchecked((int)
                    cousin.DonorTransformFileId),
                configuredDrivingProfile: TrafficDrivingProfile.DrunkCousin,
                configuredRoutePolicy: TrafficRoutePolicy.LoopOnly,
                configuredBaseLaneOffsetMeters: 0f,
                configuredPassingLaneOffsetMeters: -2f));
            return actors.ToArray();
        }

        private static TrafficEventActorDefinition[] CreateEventActors(
            IReadOnlyList<TrafficRouteDefinition> routes)
        {
            int highwayLastPoint = routes.Single(value =>
                    value.RouteId == "route.traffic.highway")
                .WorldPoints.Count - 1;
            const float rallyMinimumSpeed = 135f / 3.6f;
            const float rallyMaximumSpeed = 145f / 3.6f;
            const float normalPoliceMinimumSpeed = 95f / 3.6f;
            const float normalPoliceMaximumSpeed = 105f / 3.6f;
            const float chaseMinimumSpeed = 130f / 3.6f;
            const float chaseMaximumSpeed = 170f / 3.6f;
            return new[]
            {
                CreateRallyActor(
                    1,
                    "P1.VEHICLE.107",
                    new Vector3(-1292.79f, 0.119f, 1267.68f),
                    new Quaternion(0f, 0.51053506f, 0f, 0.859857f),
                    875f,
                    63136,
                    rallyMinimumSpeed,
                    rallyMaximumSpeed),
                CreateRallyActor(
                    2,
                    "P1.VEHICLE.108",
                    new Vector3(-1276.13f, -0.32f, 1275.77f),
                    new Quaternion(0f, 0.5634408f, 0f, 0.8261565f),
                    1050f,
                    44034,
                    rallyMinimumSpeed - 3f / 3.6f,
                    rallyMaximumSpeed - 3f / 3.6f),
                CreateRallyActor(
                    3,
                    "P1.VEHICLE.109",
                    new Vector3(-1283.8f, -0.15f, 1272.29f),
                    new Quaternion(0f, 0.52820814f, 0f, 0.84911495f),
                    850f,
                    45376,
                    rallyMinimumSpeed - 2f / 3.6f,
                    rallyMaximumSpeed - 2f / 3.6f),
                CreateDragActor(
                    1,
                    "P1.VEHICLE.110",
                    new Vector3(-742.154f, 2.72f, -903.2053f),
                    new Quaternion(0f, -0.44949248f, 0f, -0.89328414f),
                    laneOffsetMeters: -3f,
                    massKilograms: 1150f,
                    deterministicSeed: 42666),
                CreateDragActor(
                    2,
                    "P1.VEHICLE.111",
                    new Vector3(-727.9729f, 2.69f, -897.855f),
                    new Quaternion(0f, -0.42403346f, 0f, -0.9056465f),
                    laneOffsetMeters: 0f,
                    massKilograms: 950f,
                    deterministicSeed: 55575),
                CreatePoliceActor(
                    1,
                    primaryStartPointIndex: 422,
                    primaryEndPointIndex: 0,
                    travelsForward: false,
                    stagingPosition: new Vector3(
                        -24.624329f,
                        9.85f,
                        1606.7386f),
                    stagingRotation: new Quaternion(
                        0f,
                        0.796679f,
                        0f,
                        -0.6044027f),
                    deterministicSeed: 62988,
                    normalPoliceMinimumSpeed,
                    normalPoliceMaximumSpeed,
                    chaseMinimumSpeed,
                    chaseMaximumSpeed),
                CreatePoliceActor(
                    2,
                    primaryStartPointIndex: 416,
                    primaryEndPointIndex: highwayLastPoint,
                    travelsForward: true,
                    stagingPosition: new Vector3(
                        -41.98636f,
                        9.16f,
                        1594.7474f),
                    stagingRotation: new Quaternion(
                        0f,
                        -0.56569946f,
                        0f,
                        -0.8246115f),
                    deterministicSeed: 67990,
                    normalPoliceMinimumSpeed,
                    normalPoliceMaximumSpeed,
                    chaseMinimumSpeed,
                    chaseMaximumSpeed),
            };
        }

        private static TrafficEventActorDefinition CreateRallyActor(
            int ordinal,
            string featureId,
            Vector3 stagingPosition,
            Quaternion stagingRotation,
            float massKilograms,
            int deterministicSeed,
            float minimumSpeed,
            float maximumSpeed)
        {
            string actorId = $"traffic.event.rally.car-{ordinal}";
            return new TrafficEventActorDefinition(
                actorId,
                DeriveStableGuid(actorId),
                featureId,
                $"presentation.traffic.event.rally-car-{ordinal}",
                TrafficEventKind.OfficialRally,
                "route.traffic.dirt-road",
                configuredPrimaryStartPointIndex: 2168,
                configuredPrimaryEndPointIndex: 3640,
                configuredAlternateStartPointIndex: 56,
                configuredAlternateEndPointIndex: 1875,
                configuredTravelsForward: true,
                stagingPosition,
                stagingRotation,
                configuredLaneOffsetMeters: 0f,
                minimumSpeed,
                maximumSpeed,
                configuredChaseMinimumSpeedMetersPerSecond: minimumSpeed,
                configuredChaseMaximumSpeedMetersPerSecond: maximumSpeed,
                configuredMaterializeDistanceMeters: 200f,
                configuredDematerializeDistanceMeters: 400f,
                configuredRequiredDistanceMeters: 9f,
                configuredEmergencyDistanceMeters: 35f,
                configuredBrakeDistanceMeters: 101f,
                massKilograms,
                deterministicSeed);
        }

        private static TrafficEventActorDefinition CreateDragActor(
            int ordinal,
            string featureId,
            Vector3 stagingPosition,
            Quaternion stagingRotation,
            float laneOffsetMeters,
            float massKilograms,
            int deterministicSeed)
        {
            string actorId = $"traffic.event.drag.car-{ordinal}";
            // Donor MaximumSpeed is 500 km/h. It is a limiter, not a claim
            // that either car immediately or constantly reaches that speed.
            const float limiterMetersPerSecond = 500f / 3.6f;
            return new TrafficEventActorDefinition(
                actorId,
                DeriveStableGuid(actorId),
                featureId,
                $"presentation.traffic.event.drag-car-{ordinal}",
                TrafficEventKind.DragRace,
                "route.traffic.drag-race",
                configuredPrimaryStartPointIndex: 0,
                configuredPrimaryEndPointIndex: 165,
                configuredAlternateStartPointIndex: 0,
                configuredAlternateEndPointIndex: 165,
                configuredTravelsForward: true,
                stagingPosition,
                stagingRotation,
                laneOffsetMeters,
                configuredMinimumSpeedMetersPerSecond: 1f,
                configuredMaximumSpeedMetersPerSecond: limiterMetersPerSecond,
                configuredChaseMinimumSpeedMetersPerSecond: 1f,
                configuredChaseMaximumSpeedMetersPerSecond:
                    limiterMetersPerSecond,
                configuredMaterializeDistanceMeters: 720f,
                configuredDematerializeDistanceMeters: 800f,
                configuredRequiredDistanceMeters: 9f,
                configuredEmergencyDistanceMeters: 35f,
                configuredBrakeDistanceMeters: 35f,
                massKilograms,
                deterministicSeed);
        }

        private static TrafficEventActorDefinition CreatePoliceActor(
            int ordinal,
            int primaryStartPointIndex,
            int primaryEndPointIndex,
            bool travelsForward,
            Vector3 stagingPosition,
            Quaternion stagingRotation,
            int deterministicSeed,
            float minimumSpeed,
            float maximumSpeed,
            float chaseMinimumSpeed,
            float chaseMaximumSpeed)
        {
            string actorId = $"traffic.event.police.car-{ordinal}";
            return new TrafficEventActorDefinition(
                actorId,
                DeriveStableGuid(actorId),
                "P1.VEHICLE.028",
                $"presentation.traffic.event.police-car-{ordinal}",
                TrafficEventKind.PoliceCheckpoint,
                "route.traffic.highway",
                primaryStartPointIndex,
                primaryEndPointIndex,
                primaryStartPointIndex,
                primaryEndPointIndex,
                travelsForward,
                stagingPosition,
                stagingRotation,
                configuredLaneOffsetMeters: -2f,
                minimumSpeed,
                maximumSpeed,
                chaseMinimumSpeed,
                chaseMaximumSpeed,
                configuredMaterializeDistanceMeters: 1200f,
                configuredDematerializeDistanceMeters: 1200f,
                configuredRequiredDistanceMeters: 12f,
                configuredEmergencyDistanceMeters: 40f,
                configuredBrakeDistanceMeters: 40f,
                configuredMassKilograms: 1330f,
                deterministicSeed);
        }

        private static void ProjectOntoRoute(
            TrafficRouteDefinition route,
            Vector3 position,
            out float progress01,
            out Vector3 tangent)
        {
            float nearestSqr = float.PositiveInfinity;
            float distanceBefore = 0f;
            float nearestDistance = 0f;
            tangent = Vector3.forward;
            float total = 0f;
            int segmentCount = route.ClosesLoop
                ? route.WorldPoints.Count
                : route.WorldPoints.Count - 1;
            for (int index = 0; index < segmentCount; index++)
            {
                total += Vector3.Distance(
                    route.WorldPoints[index],
                    route.WorldPoints[(index + 1) %
                                      route.WorldPoints.Count]);
            }

            for (int index = 0; index < segmentCount; index++)
            {
                Vector3 a = route.WorldPoints[index];
                Vector3 delta = route.WorldPoints[(index + 1) %
                                                  route.WorldPoints.Count] - a;
                float length = delta.magnitude;
                float t = length > 0.0001f
                    ? Mathf.Clamp01(Vector3.Dot(position - a, delta) /
                                    (length * length))
                    : 0f;
                float sqr = (position - (a + delta * t)).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearestDistance = distanceBefore + length * t;
                    tangent = delta;
                }

                distanceBefore += length;
            }

            if (!float.IsFinite(nearestSqr) || nearestSqr > 100f * 100f)
            {
                throw new InvalidOperationException(
                    $"Ambient donor vehicle at {position} is not on route " +
                    $"'{route.RouteId}' (distance " +
                    $"{Mathf.Sqrt(nearestSqr):F2} m).");
            }

            progress01 = Mathf.Clamp01(nearestDistance /
                                       Mathf.Max(0.001f, total));
        }

        private static void AddBidirectional(
            ICollection<TrafficRoadConnectionDefinition> target,
            string id,
            IReadOnlyList<TrafficRouteDefinition> routes,
            string leftRouteId,
            int leftPoint,
            string rightRouteId,
            int rightPoint)
        {
            TrafficRouteDefinition left = routes.Single(value =>
                value.RouteId == leftRouteId);
            TrafficRouteDefinition right = routes.Single(value =>
                value.RouteId == rightRouteId);
            float separation = Vector3.Distance(
                left.WorldPoints[leftPoint],
                right.WorldPoints[rightPoint]);
            target.Add(new TrafficRoadConnectionDefinition(
                "connection.traffic." + id + ".a-to-b",
                leftRouteId,
                leftPoint,
                rightRouteId,
                rightPoint,
                separation));
            target.Add(new TrafficRoadConnectionDefinition(
                "connection.traffic." + id + ".b-to-a",
                rightRouteId,
                rightPoint,
                leftRouteId,
                leftPoint,
                separation));
        }

        private static void FindNearestPair(
            IReadOnlyList<Vector3> left,
            IReadOnlyList<Vector3> right,
            out int leftIndex,
            out int rightIndex,
            out float separation)
        {
            leftIndex = 0;
            rightIndex = 0;
            float nearestSqr = float.PositiveInfinity;
            for (int a = 0; a < left.Count; a++)
            for (int b = 0; b < right.Count; b++)
            {
                float sqr = (left[a] - right[b]).sqrMagnitude;
                if (sqr >= nearestSqr)
                {
                    continue;
                }

                nearestSqr = sqr;
                leftIndex = a;
                rightIndex = b;
            }

            separation = Mathf.Sqrt(nearestSqr);
        }

        private static void ResolveActorTuning(
            string archetypeId,
            out string vehicleFeatureId,
            out string driverFeatureId,
            out float minimumSpeed,
            out float maximumSpeed)
        {
            string suffix;
            switch (archetypeId)
            {
                case "victro":
                    suffix = "014";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "lamore":
                    suffix = "015";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "truck":
                    suffix = "016";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "polsa":
                    suffix = "017";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "fittan":
                    suffix = "018";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "svoboda":
                    suffix = "019";
                    minimumSpeed = 95f / 3.6f;
                    maximumSpeed = 105f / 3.6f;
                    break;
                case "menace":
                    suffix = "020";
                    minimumSpeed = 115f / 3.6f;
                    maximumSpeed = 185f / 3.6f;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown ambient traffic archetype '{archetypeId}'.");
            }

            vehicleFeatureId = "P1.VEHICLE." + suffix;
            driverFeatureId = "P1.NPC.TRAFFIC." + suffix;
        }

        private static bool ClosesLoop(string routeId) =>
            routeId == "route.traffic.highway" ||
            routeId == "route.traffic.dirt-road" ||
            routeId == "route.traffic.road-race" ||
            routeId == "route.traffic.track-field" ||
            routeId == "route.traffic.bus" ||
            routeId == "route.traffic.boat-1" ||
            routeId == "route.traffic.boat-2";

        private static TrafficRouteSurface ResolveSurface(string routeId) =>
            routeId == "route.traffic.dirt-road" ||
            routeId == "route.traffic.home-road" ||
            routeId == "route.traffic.dancehall" ||
            routeId == "route.traffic.track-field"
                ? TrafficRouteSurface.Gravel
                : TrafficRouteSurface.Paved;

        private static float ResolveRoadWidth(string routeId) => routeId switch
        {
            "route.traffic.highway" => 7.5f,
            "route.traffic.village" => 7f,
            "route.traffic.drag-race" => 8f,
            "route.traffic.road-race" => 6.4f,
            "route.traffic.mod-town-entry" => 6.4f,
            "route.traffic.mod-town-loop" => 6.4f,
            "route.traffic.mod-gas-pump" => 5.5f,
            _ => 5.6f,
        };

        private static string DeriveStableGuid(string value)
        {
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(
                Encoding.UTF8.GetBytes("msc-remake:" + value));
            var bytes = new byte[16];
            Array.Copy(digest, bytes, bytes.Length);
            return new Guid(bytes).ToString("N");
        }

        private static string Slug(string routeId) =>
            routeId.Replace("route.traffic.", string.Empty)
                .Replace('.', '-');

        private static void BindBootstrap(
            TrafficRoadNetworkCatalog catalog,
            TrafficPresentationCatalog presentation)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            // Opening a scene can unload the ignored private asset object.
            presentation = AssetDatabase.LoadAssetAtPath<
                TrafficPresentationCatalog>(PresentationCatalogPath) ??
                throw new InvalidOperationException(
                    "Private traffic presentation catalog disappeared while " +
                    "binding Bootstrap.");
            ProductionWorldStreamingInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include) ??
                throw new InvalidOperationException(
                    "Bootstrap scene has no production streaming installer.");
            installer.ConfigureTrafficForAuthoring(catalog, presentation);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WriteReport(
            LockedTrafficRouteSet evidence,
            IReadOnlyList<LockedAmbientTrafficActorEvidence> actorEvidence,
            IReadOnlyList<TrafficRouteDefinition> routes,
            IReadOnlyList<TrafficRoadConnectionDefinition> connections,
            IReadOnlyList<TrafficActorDefinition> actors,
            IReadOnlyList<TrafficTransportDefinition> transports,
            TrafficPresentationCatalog presentation)
        {
            var report = new BuildReport
            {
                schemaVersion = 1,
                milestoneId = "11B-T2",
                classification = "Reimplemented",
                sourceSceneSha256 = evidence.SourceSceneSha256,
                roadRouteCount = routes.Count,
                roadPointCount = routes.Sum(value =>
                    value.WorldPoints.Count),
                graphConnectionCount = connections.Count,
                donorHighwayInstanceCount = actorEvidence.Count,
                persistentActorCount = actors.Count,
                routeTransportActorCount = transports.Count,
                presentationArchetypeCount = presentation.Entries.Count,
                exactDonorMultiplicityPreserved = actorEvidence.Count == 10 &&
                    actors.Count(value => value.RouteId == HighwayRouteId) ==
                    actorEvidence.Count,
                usesPhysicalNwhNearPlayer = true,
                usesPersistentOffscreenState = true,
                donorControllersExcluded = true,
                donorHierarchyRuntimeDependency = false,
                ambientAudioProfile =
                    "temporary petteri fallback profile; per-model Wwise events pending",
            };
            string fullPath = Path.GetFullPath(BuildReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(
                fullPath,
                JsonUtility.ToJson(report, prettyPrint: true) +
                Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        [Serializable]
        private sealed class BuildReport
        {
            public int schemaVersion;
            public string milestoneId;
            public string classification;
            public string sourceSceneSha256;
            public int roadRouteCount;
            public int roadPointCount;
            public int graphConnectionCount;
            public int donorHighwayInstanceCount;
            public int persistentActorCount;
            public int routeTransportActorCount;
            public int presentationArchetypeCount;
            public bool exactDonorMultiplicityPreserved;
            public bool usesPhysicalNwhNearPlayer;
            public bool usesPersistentOffscreenState;
            public bool donorControllersExcluded;
            public bool donorHierarchyRuntimeDependency;
            public string ambientAudioProfile;
        }
    }
}
