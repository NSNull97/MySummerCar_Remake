using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Traffic
{
    public enum TrafficRouteSurface
    {
        Paved = 0,
        Gravel = 1,
        Dirt = 2,
    }

    public enum TrafficTransportKind
    {
        Bus = 0,
        Train = 1,
        Boat = 2,
    }

    public enum TrafficDrivingProfile
    {
        Standard = 0,
        DrunkCousin = 1,
    }

    public enum TrafficRoutePolicy
    {
        LoopOnly = 0,
        TrafficExpansionTownExcursions = 1,
    }

    [Serializable]
    public sealed class TrafficBusDepartureDefinition
    {
        [SerializeField, Range(0, 23)] private int hour;
        [SerializeField, Min(0)] private int routeStartPointIndex;
        [SerializeField] private string donorOriginId = string.Empty;

        public TrafficBusDepartureDefinition(
            int configuredHour,
            int configuredRouteStartPointIndex,
            string configuredDonorOriginId)
        {
            hour = configuredHour;
            routeStartPointIndex = configuredRouteStartPointIndex;
            donorOriginId = configuredDonorOriginId?.Trim() ?? string.Empty;
        }

        public int Hour => hour;
        public int RouteStartPointIndex => routeStartPointIndex;
        public string DonorOriginId => donorOriginId;
    }

    [Serializable]
    public sealed class TrafficRouteStopDefinition
    {
        [SerializeField] private string stopId = string.Empty;
        [SerializeField, Min(0f)] private float routeDistanceMeters;
        [SerializeField, Min(0f)] private float dwellSimulationSeconds = 5f;

        public TrafficRouteStopDefinition(
            string configuredStopId,
            float configuredRouteDistanceMeters,
            float configuredDwellSimulationSeconds)
        {
            stopId = configuredStopId?.Trim() ?? string.Empty;
            routeDistanceMeters = configuredRouteDistanceMeters;
            dwellSimulationSeconds = configuredDwellSimulationSeconds;
        }

        public string StopId => stopId;
        public float RouteDistanceMeters => routeDistanceMeters;
        public float DwellSimulationSeconds => dwellSimulationSeconds;
    }

    [Serializable]
    public sealed class TrafficTransportDefinition
    {
        [SerializeField] private string transportId = string.Empty;
        [SerializeField] private string stableInstanceId = string.Empty;
        [SerializeField] private string featureId = string.Empty;
        [SerializeField] private string presentationId = string.Empty;
        [SerializeField] private TrafficTransportKind kind;
        [SerializeField] private string primaryRouteId = string.Empty;
        [SerializeField] private string secondaryRouteId = string.Empty;
        [SerializeField, Min(0.1f)] private float speedMetersPerSecond = 10f;
        [SerializeField, Min(10f)] private float materializeDistanceMeters = 700f;
        [SerializeField, Min(10f)] private float dematerializeDistanceMeters = 850f;
        [SerializeField, Min(0f)] private float endpointDelaySimulationSeconds;
        // Kept under the original serialized name for save/asset compatibility.
        // Semantically these are donor BUS Setup/WAKEUP phase anchors, not a
        // recurring departure schedule.
        [SerializeField] private TrafficBusDepartureDefinition[] busDepartures =
            Array.Empty<TrafficBusDepartureDefinition>();
        [SerializeField] private TrafficRouteStopDefinition[] routeStops =
            Array.Empty<TrafficRouteStopDefinition>();

        public TrafficTransportDefinition(
            string configuredTransportId,
            string configuredStableInstanceId,
            string configuredFeatureId,
            string configuredPresentationId,
            TrafficTransportKind configuredKind,
            string configuredPrimaryRouteId,
            string configuredSecondaryRouteId,
            float configuredSpeedMetersPerSecond,
            float configuredMaterializeDistanceMeters,
            float configuredDematerializeDistanceMeters,
            float configuredEndpointDelaySimulationSeconds,
            IEnumerable<TrafficBusDepartureDefinition> configuredBusDepartures,
            IEnumerable<TrafficRouteStopDefinition> configuredRouteStops)
        {
            transportId = configuredTransportId?.Trim() ?? string.Empty;
            stableInstanceId = configuredStableInstanceId?.Trim() ?? string.Empty;
            featureId = configuredFeatureId?.Trim() ?? string.Empty;
            presentationId = configuredPresentationId?.Trim() ?? string.Empty;
            kind = configuredKind;
            primaryRouteId = configuredPrimaryRouteId?.Trim() ?? string.Empty;
            secondaryRouteId = configuredSecondaryRouteId?.Trim() ?? string.Empty;
            speedMetersPerSecond = configuredSpeedMetersPerSecond;
            materializeDistanceMeters = configuredMaterializeDistanceMeters;
            dematerializeDistanceMeters = configuredDematerializeDistanceMeters;
            endpointDelaySimulationSeconds =
                configuredEndpointDelaySimulationSeconds;
            busDepartures = (configuredBusDepartures ??
                    Enumerable.Empty<TrafficBusDepartureDefinition>())
                .ToArray();
            routeStops = (configuredRouteStops ??
                    Enumerable.Empty<TrafficRouteStopDefinition>())
                .ToArray();
        }

        public string TransportId => transportId;
        public string StableInstanceId => stableInstanceId;
        public string FeatureId => featureId;
        public string PresentationId => presentationId;
        public TrafficTransportKind Kind => kind;
        public string PrimaryRouteId => primaryRouteId;
        public string SecondaryRouteId => secondaryRouteId;
        public float SpeedMetersPerSecond => speedMetersPerSecond;
        public float MaterializeDistanceMeters => materializeDistanceMeters;
        public float DematerializeDistanceMeters => dematerializeDistanceMeters;
        public float EndpointDelaySimulationSeconds =>
            endpointDelaySimulationSeconds;
        public IReadOnlyList<TrafficBusDepartureDefinition> BusDepartures =>
            busDepartures;
        public IReadOnlyList<TrafficRouteStopDefinition> RouteStops =>
            routeStops;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(transportId) ||
                !transportId.StartsWith("traffic.transport.", StringComparison.Ordinal) ||
                !StableEntityId.TryParse(stableInstanceId, out _) ||
                string.IsNullOrWhiteSpace(featureId) ||
                !featureId.StartsWith("P1.TRAFFIC.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(presentationId) ||
                !presentationId.StartsWith("presentation.traffic.", StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(TrafficTransportKind), kind) ||
                string.IsNullOrWhiteSpace(primaryRouteId) ||
                !float.IsFinite(speedMetersPerSecond) ||
                speedMetersPerSecond <= 0f ||
                !float.IsFinite(materializeDistanceMeters) ||
                materializeDistanceMeters < 10f ||
                !float.IsFinite(dematerializeDistanceMeters) ||
                dematerializeDistanceMeters <= materializeDistanceMeters ||
                !float.IsFinite(endpointDelaySimulationSeconds) ||
                endpointDelaySimulationSeconds < 0f ||
                busDepartures == null || routeStops == null)
            {
                failure = $"Transport actor '{transportId}' is invalid.";
                return false;
            }

            if (kind == TrafficTransportKind.Train &&
                string.IsNullOrWhiteSpace(secondaryRouteId))
            {
                failure = $"Train actor '{transportId}' has no return route.";
                return false;
            }

            if (kind == TrafficTransportKind.Bus &&
                (busDepartures.Length != 12 ||
                 busDepartures.Select(value => value.Hour).Distinct().Count() != 12 ||
                 busDepartures.Any(value => value == null ||
                    value.Hour < 0 || value.Hour > 23 ||
                    value.RouteStartPointIndex < 0 ||
                    string.IsNullOrWhiteSpace(value.DonorOriginId))))
            {
                failure = $"Bus actor '{transportId}' has an invalid timetable.";
                return false;
            }

            if (routeStops.Any(value => value == null ||
                    string.IsNullOrWhiteSpace(value.StopId) ||
                    !float.IsFinite(value.RouteDistanceMeters) ||
                    value.RouteDistanceMeters < 0f ||
                    !float.IsFinite(value.DwellSimulationSeconds) ||
                    value.DwellSimulationSeconds < 0f))
            {
                failure = $"Transport actor '{transportId}' has invalid stops.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class TrafficRouteDefinition
    {
        // Imported authored routes can legitimately contain sub-millimetre
        // samples. Only reject effectively equal consecutive points; geometry
        // queries already ignore numerically degenerate individual segments.
        private const float ExactDuplicatePointDistanceSquared = 1e-12f;

        [SerializeField] private string routeId = string.Empty;
        [SerializeField] private string donorHierarchyPath = string.Empty;
        [SerializeField] private bool closesLoop;
        [SerializeField] private TrafficRouteSurface surface;
        [SerializeField, Min(2f)] private float nominalRoadWidthMeters = 6.4f;
        [SerializeField] private Vector3[] worldPoints = Array.Empty<Vector3>();

        public TrafficRouteDefinition(
            string configuredRouteId,
            string configuredDonorHierarchyPath,
            bool configuredClosesLoop,
            TrafficRouteSurface configuredSurface,
            float configuredNominalRoadWidthMeters,
            IEnumerable<Vector3> configuredWorldPoints)
        {
            routeId = configuredRouteId?.Trim() ?? string.Empty;
            donorHierarchyPath =
                configuredDonorHierarchyPath?.Trim() ?? string.Empty;
            closesLoop = configuredClosesLoop;
            surface = configuredSurface;
            nominalRoadWidthMeters = configuredNominalRoadWidthMeters;
            worldPoints = (configuredWorldPoints ??
                    throw new ArgumentNullException(nameof(configuredWorldPoints)))
                .ToArray();
        }

        public string RouteId => routeId;
        public string DonorHierarchyPath => donorHierarchyPath;
        public bool ClosesLoop => closesLoop;
        public TrafficRouteSurface Surface => surface;
        public float NominalRoadWidthMeters => nominalRoadWidthMeters;
        public IReadOnlyList<Vector3> WorldPoints => worldPoints;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(routeId) ||
                !routeId.StartsWith("route.traffic.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(donorHierarchyPath) ||
                !Enum.IsDefined(typeof(TrafficRouteSurface), surface) ||
                !float.IsFinite(nominalRoadWidthMeters) ||
                nominalRoadWidthMeters < 2f ||
                worldPoints == null ||
                worldPoints.Length < 2 ||
                worldPoints.Any(point => !IsFinite(point)))
            {
                failure = $"Traffic route '{routeId}' is invalid.";
                return false;
            }

            for (int index = 1; index < worldPoints.Length; index++)
            {
                if ((worldPoints[index] - worldPoints[index - 1]).sqrMagnitude <
                    ExactDuplicatePointDistanceSquared)
                {
                    failure = $"Traffic route '{routeId}' contains a duplicate consecutive point at {index}.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    [Serializable]
    public sealed class TrafficRoadConnectionDefinition
    {
        [SerializeField] private string connectionId = string.Empty;
        [SerializeField] private string fromRouteId = string.Empty;
        [SerializeField, Min(0)] private int fromPointIndex;
        [SerializeField] private string toRouteId = string.Empty;
        [SerializeField, Min(0)] private int toPointIndex;
        [SerializeField, Min(0f)] private float separationMeters;

        public TrafficRoadConnectionDefinition(
            string configuredConnectionId,
            string configuredFromRouteId,
            int configuredFromPointIndex,
            string configuredToRouteId,
            int configuredToPointIndex,
            float configuredSeparationMeters)
        {
            connectionId = configuredConnectionId?.Trim() ?? string.Empty;
            fromRouteId = configuredFromRouteId?.Trim() ?? string.Empty;
            fromPointIndex = configuredFromPointIndex;
            toRouteId = configuredToRouteId?.Trim() ?? string.Empty;
            toPointIndex = configuredToPointIndex;
            separationMeters = configuredSeparationMeters;
        }

        public string ConnectionId => connectionId;
        public string FromRouteId => fromRouteId;
        public int FromPointIndex => fromPointIndex;
        public string ToRouteId => toRouteId;
        public int ToPointIndex => toPointIndex;
        public float SeparationMeters => separationMeters;
    }

    [Serializable]
    public sealed class TrafficActorDefinition
    {
        [SerializeField] private string actorId = string.Empty;
        [SerializeField] private string stableInstanceId = string.Empty;
        [SerializeField] private string vehicleFeatureId = string.Empty;
        [SerializeField] private string driverFeatureId = string.Empty;
        [SerializeField] private string presentationId = string.Empty;
        [SerializeField] private string routeId = string.Empty;
        [SerializeField, Range(0f, 1f)] private float initialProgress01;
        [SerializeField] private bool travelsForward = true;
        [SerializeField, Min(1f)] private float minimumSpeedMetersPerSecond = 18f;
        [SerializeField, Min(1f)] private float maximumSpeedMetersPerSecond = 29f;
        [SerializeField, Range(0f, 1f)] private float weekdaySpawnProbability01 = 1f;
        [SerializeField, Range(0f, 1f)] private float weekendSpawnProbability01 = 1f;
        [SerializeField, Min(1f)] private float inactiveRetryGameSeconds = 60f;
        [SerializeField] private int deterministicSeed;
        [SerializeField] private TrafficDrivingProfile drivingProfile;
        [SerializeField] private TrafficRoutePolicy routePolicy;
        [SerializeField] private float baseLaneOffsetMeters = 1.65f;
        [SerializeField] private float passingLaneOffsetMeters = -1.65f;

        public TrafficActorDefinition(
            string configuredActorId,
            string configuredStableInstanceId,
            string configuredVehicleFeatureId,
            string configuredDriverFeatureId,
            string configuredPresentationId,
            string configuredRouteId,
            float configuredInitialProgress01,
            bool configuredTravelsForward,
            float configuredMinimumSpeedMetersPerSecond,
            float configuredMaximumSpeedMetersPerSecond,
            float configuredWeekdaySpawnProbability01,
            float configuredWeekendSpawnProbability01,
            float configuredInactiveRetryGameSeconds,
            int configuredDeterministicSeed,
            TrafficDrivingProfile configuredDrivingProfile =
                TrafficDrivingProfile.Standard,
            TrafficRoutePolicy configuredRoutePolicy =
                TrafficRoutePolicy.LoopOnly,
            float configuredBaseLaneOffsetMeters = 1.65f,
            float configuredPassingLaneOffsetMeters = -1.65f)
        {
            actorId = configuredActorId?.Trim() ?? string.Empty;
            stableInstanceId = configuredStableInstanceId?.Trim() ?? string.Empty;
            vehicleFeatureId = configuredVehicleFeatureId?.Trim() ?? string.Empty;
            driverFeatureId = configuredDriverFeatureId?.Trim() ?? string.Empty;
            presentationId = configuredPresentationId?.Trim() ?? string.Empty;
            routeId = configuredRouteId?.Trim() ?? string.Empty;
            initialProgress01 = configuredInitialProgress01;
            travelsForward = configuredTravelsForward;
            minimumSpeedMetersPerSecond = configuredMinimumSpeedMetersPerSecond;
            maximumSpeedMetersPerSecond = configuredMaximumSpeedMetersPerSecond;
            weekdaySpawnProbability01 = configuredWeekdaySpawnProbability01;
            weekendSpawnProbability01 = configuredWeekendSpawnProbability01;
            inactiveRetryGameSeconds = configuredInactiveRetryGameSeconds;
            deterministicSeed = configuredDeterministicSeed;
            drivingProfile = configuredDrivingProfile;
            routePolicy = configuredRoutePolicy;
            baseLaneOffsetMeters = configuredBaseLaneOffsetMeters;
            passingLaneOffsetMeters = configuredPassingLaneOffsetMeters;
        }

        public string ActorId => actorId;
        public string StableInstanceId => stableInstanceId;
        public string VehicleFeatureId => vehicleFeatureId;
        public string DriverFeatureId => driverFeatureId;
        public string PresentationId => presentationId;
        public string RouteId => routeId;
        public float InitialProgress01 => initialProgress01;
        public bool TravelsForward => travelsForward;
        public float MinimumSpeedMetersPerSecond => minimumSpeedMetersPerSecond;
        public float MaximumSpeedMetersPerSecond => maximumSpeedMetersPerSecond;
        public float WeekdaySpawnProbability01 => weekdaySpawnProbability01;
        public float WeekendSpawnProbability01 => weekendSpawnProbability01;
        public float InactiveRetryGameSeconds => inactiveRetryGameSeconds;
        public int DeterministicSeed => deterministicSeed;
        public TrafficDrivingProfile DrivingProfile => drivingProfile;
        public TrafficRoutePolicy RoutePolicy => routePolicy;
        public float BaseLaneOffsetMeters => baseLaneOffsetMeters;
        public float PassingLaneOffsetMeters => passingLaneOffsetMeters;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(actorId) ||
                !actorId.StartsWith("traffic.ambient.", StringComparison.Ordinal) ||
                !StableEntityId.TryParse(stableInstanceId, out _) ||
                string.IsNullOrWhiteSpace(vehicleFeatureId) ||
                !vehicleFeatureId.StartsWith("P1.VEHICLE.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(driverFeatureId) ||
                !driverFeatureId.StartsWith("P1.NPC.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(presentationId) ||
                !presentationId.StartsWith("presentation.traffic.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(routeId) ||
                !float.IsFinite(initialProgress01) ||
                initialProgress01 < 0f || initialProgress01 > 1f ||
                !float.IsFinite(minimumSpeedMetersPerSecond) ||
                minimumSpeedMetersPerSecond <= 0f ||
                !float.IsFinite(maximumSpeedMetersPerSecond) ||
                maximumSpeedMetersPerSecond < minimumSpeedMetersPerSecond ||
                !float.IsFinite(weekdaySpawnProbability01) ||
                weekdaySpawnProbability01 < 0f ||
                weekdaySpawnProbability01 > 1f ||
                !float.IsFinite(weekendSpawnProbability01) ||
                weekendSpawnProbability01 < 0f ||
                weekendSpawnProbability01 > 1f ||
                !float.IsFinite(inactiveRetryGameSeconds) ||
                inactiveRetryGameSeconds <= 0f ||
                !Enum.IsDefined(typeof(TrafficDrivingProfile), drivingProfile) ||
                !Enum.IsDefined(typeof(TrafficRoutePolicy), routePolicy) ||
                !float.IsFinite(baseLaneOffsetMeters) ||
                Mathf.Abs(baseLaneOffsetMeters) > 4f ||
                !float.IsFinite(passingLaneOffsetMeters) ||
                Mathf.Abs(passingLaneOffsetMeters) > 4f ||
                Mathf.Abs(passingLaneOffsetMeters - baseLaneOffsetMeters) < 1f)
            {
                failure = $"Ambient traffic actor '{actorId}' is invalid.";
                return false;
            }

            if (routePolicy ==
                    TrafficRoutePolicy.TrafficExpansionTownExcursions &&
                !string.Equals(
                    routeId,
                    "route.traffic.highway",
                    StringComparison.Ordinal) ||
                drivingProfile == TrafficDrivingProfile.DrunkCousin &&
                !string.Equals(
                    routeId,
                    "route.traffic.dirt-road",
                    StringComparison.Ordinal))
            {
                failure =
                    $"Ambient traffic actor '{actorId}' has incompatible behavior and route.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "TrafficRoadNetworkCatalog",
        menuName = "My Summer Car/Traffic/Road Network Catalog")]
    public sealed class TrafficRoadNetworkCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = string.Empty;
        [SerializeField] private string sourceSceneSha256 = string.Empty;
        [SerializeField] private TrafficRouteDefinition[] routes =
            Array.Empty<TrafficRouteDefinition>();
        [SerializeField] private TrafficRoadConnectionDefinition[] connections =
            Array.Empty<TrafficRoadConnectionDefinition>();
        [SerializeField] private TrafficActorDefinition[] actors =
            Array.Empty<TrafficActorDefinition>();
        [SerializeField] private TrafficTransportDefinition[] transports =
            Array.Empty<TrafficTransportDefinition>();
        [SerializeField] private TrafficEventActorDefinition[] eventActors =
            Array.Empty<TrafficEventActorDefinition>();

        private Dictionary<string, TrafficRouteDefinition> routesById;
        private Dictionary<string, TrafficActorDefinition> actorsById;
        private Dictionary<string, TrafficTransportDefinition> transportsById;
        private Dictionary<string, TrafficEventActorDefinition> eventActorsById;

        public string CatalogId => catalogId;
        public string SourceSceneSha256 => sourceSceneSha256;
        public IReadOnlyList<TrafficRouteDefinition> Routes => routes;
        public IReadOnlyList<TrafficRoadConnectionDefinition> Connections =>
            connections;
        public IReadOnlyList<TrafficActorDefinition> Actors => actors;
        public IReadOnlyList<TrafficTransportDefinition> Transports => transports;
        public IReadOnlyList<TrafficEventActorDefinition> EventActors =>
            eventActors;

        public bool TryGetRoute(
            string routeId,
            out TrafficRouteDefinition definition)
        {
            EnsureIndexes();
            return routesById.TryGetValue(routeId ?? string.Empty, out definition);
        }

        public bool TryGetActor(
            string actorId,
            out TrafficActorDefinition definition)
        {
            EnsureIndexes();
            return actorsById.TryGetValue(actorId ?? string.Empty, out definition);
        }

        public bool TryGetTransport(
            string transportId,
            out TrafficTransportDefinition definition)
        {
            EnsureIndexes();
            return transportsById.TryGetValue(
                transportId ?? string.Empty,
                out definition);
        }

        public bool TryGetEventActor(
            string actorId,
            out TrafficEventActorDefinition definition)
        {
            EnsureIndexes();
            return eventActorsById.TryGetValue(
                actorId ?? string.Empty,
                out definition);
        }

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(catalogId) ||
                !catalogId.StartsWith("catalog.traffic.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(sourceSceneSha256) ||
                sourceSceneSha256.Length != 64 ||
                routes == null || routes.Length == 0 ||
                connections == null || actors == null || actors.Length == 0 ||
                transports == null || eventActors == null)
            {
                failure = "Traffic road-network catalog header is invalid.";
                return false;
            }

            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficRouteDefinition route in routes)
            {
                if (route == null)
                {
                    failure = "Traffic route catalog contains a null route.";
                    return false;
                }

                if (!route.TryValidate(out failure))
                {
                    return false;
                }

                if (!routeIds.Add(route.RouteId))
                {
                    failure = "Traffic route IDs are not unique.";
                    return false;
                }
            }

            var connectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficRoadConnectionDefinition connection in connections)
            {
                if (connection == null ||
                    string.IsNullOrWhiteSpace(connection.ConnectionId) ||
                    !connectionIds.Add(connection.ConnectionId) ||
                    !TryFindRoute(routes, connection.FromRouteId, out var from) ||
                    !TryFindRoute(routes, connection.ToRouteId, out var to) ||
                    connection.FromPointIndex < 0 ||
                    connection.FromPointIndex >= from.WorldPoints.Count ||
                    connection.ToPointIndex < 0 ||
                    connection.ToPointIndex >= to.WorldPoints.Count ||
                    !float.IsFinite(connection.SeparationMeters) ||
                    connection.SeparationMeters < 0f)
                {
                    failure = "Traffic road connection is invalid.";
                    return false;
                }
            }

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficActorDefinition actor in actors)
            {
                if (actor == null)
                {
                    failure = "Ambient traffic actor catalog contains null.";
                    return false;
                }

                if (!actor.TryValidate(out failure))
                {
                    return false;
                }

                if (!actorIds.Add(actor.ActorId) ||
                    !stableIds.Add(actor.StableInstanceId) ||
                    !routeIds.Contains(actor.RouteId))
                {
                    failure =
                        "Ambient traffic actor identities or routes are invalid.";
                    return false;
                }
            }

            var transportIds = new HashSet<string>(StringComparer.Ordinal);
            failure = string.Empty;
            foreach (TrafficTransportDefinition transport in transports)
            {
                if (transport == null ||
                    !transport.TryValidate(out failure) ||
                    !transportIds.Add(transport.TransportId) ||
                    !stableIds.Add(transport.StableInstanceId) ||
                    !routeIds.Contains(transport.PrimaryRouteId) ||
                    !string.IsNullOrWhiteSpace(transport.SecondaryRouteId) &&
                    !routeIds.Contains(transport.SecondaryRouteId))
                {
                    failure = string.IsNullOrWhiteSpace(failure)
                        ? "Transport actor identities or routes are invalid."
                        : failure;
                    return false;
                }

                if (transport.Kind == TrafficTransportKind.Bus &&
                    (transport.BusDepartures.Any(value =>
                        value.RouteStartPointIndex >= routes
                            .Single(route => route.RouteId ==
                                transport.PrimaryRouteId)
                            .WorldPoints.Count) ||
                     transport.RouteStops.Any(value =>
                        value.RouteDistanceMeters > new TrafficRouteGeometry(
                            routes.Single(route => route.RouteId ==
                                transport.PrimaryRouteId))
                            .TotalLengthMeters + 0.1f)))
                {
                    failure = "Bus timetable or stop lies outside its route.";
                    return false;
                }
            }

            var eventActorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficEventActorDefinition eventActor in eventActors)
            {
                if (eventActor == null ||
                    !eventActor.TryValidate(out failure) ||
                    !eventActorIds.Add(eventActor.ActorId) ||
                    !stableIds.Add(eventActor.StableInstanceId) ||
                    !routeIds.Contains(eventActor.RouteId))
                {
                    failure = string.IsNullOrWhiteSpace(failure)
                        ? "Traffic event actor identities or routes are invalid."
                        : failure;
                    return false;
                }

                TrafficRouteDefinition eventRoute = routes.Single(value =>
                    value.RouteId == eventActor.RouteId);
                int pointCount = eventRoute.WorldPoints.Count;
                if (eventActor.PrimaryStartPointIndex >= pointCount ||
                    eventActor.PrimaryEndPointIndex >= pointCount ||
                    eventActor.AlternateStartPointIndex >= pointCount ||
                    eventActor.AlternateEndPointIndex >= pointCount)
                {
                    failure = $"Traffic event actor '{eventActor.ActorId}' " +
                              "references a point outside its route.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryFindRoute(
            IEnumerable<TrafficRouteDefinition> source,
            string routeId,
            out TrafficRouteDefinition route)
        {
            route = source.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.RouteId, routeId, StringComparison.Ordinal));
            return route != null;
        }

        private void EnsureIndexes()
        {
            routesById ??= (routes ?? Array.Empty<TrafficRouteDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.RouteId, StringComparer.Ordinal);
            actorsById ??= (actors ?? Array.Empty<TrafficActorDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.ActorId, StringComparer.Ordinal);
            transportsById ??= (transports ??
                    Array.Empty<TrafficTransportDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.TransportId, StringComparer.Ordinal);
            eventActorsById ??= (eventActors ??
                    Array.Empty<TrafficEventActorDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.ActorId, StringComparer.Ordinal);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredCatalogId,
            string configuredSourceSceneSha256,
            IEnumerable<TrafficRouteDefinition> configuredRoutes,
            IEnumerable<TrafficRoadConnectionDefinition> configuredConnections,
            IEnumerable<TrafficActorDefinition> configuredActors,
            IEnumerable<TrafficTransportDefinition> configuredTransports = null,
            IEnumerable<TrafficEventActorDefinition> configuredEventActors = null)
        {
            catalogId = configuredCatalogId?.Trim() ?? string.Empty;
            sourceSceneSha256 = configuredSourceSceneSha256?.Trim() ?? string.Empty;
            routes = (configuredRoutes ?? Enumerable.Empty<TrafficRouteDefinition>())
                .ToArray();
            connections = (configuredConnections ??
                    Enumerable.Empty<TrafficRoadConnectionDefinition>())
                .ToArray();
            actors = (configuredActors ?? Enumerable.Empty<TrafficActorDefinition>())
                .ToArray();
            transports = (configuredTransports ??
                    Enumerable.Empty<TrafficTransportDefinition>())
                .ToArray();
            eventActors = (configuredEventActors ??
                    Enumerable.Empty<TrafficEventActorDefinition>())
                .ToArray();
            routesById = null;
            actorsById = null;
            transportsById = null;
            eventActorsById = null;
        }
#endif
    }
}
