using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.LegacyImport.Editor.Configuration;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Read-only extraction of the locked donor road, bus, boat and train
    /// centerlines. Only transforms and configuration are transferred; donor
    /// route components and PlayMaker state machines are never instantiated.
    /// </summary>
    public static class Phase1TrafficRouteEvidence
    {
        public const string LockedSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";

        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string SceneRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets/_Scenes/GAME.unity";
        private const string TrafficExpansionEvidenceManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "TrafficCarExpansionBehaviorEvidence.json";
        private const string TrafficExpansionDllSha256 =
            "7b626ca489a4abd7acdd34ca987199e6a27dd5bcd6cd398bab441360471313f5";
        // Routes_Modded contains two intentional authored samples only
        // 0.23 mm apart at the fuel pump. Reject equal points without
        // discarding source resolution merely because a segment is tiny.
        private const float ExactDuplicatePointDistanceSquared = 1e-12f;

        private const long JaniVehicleTransformId = 43020;
        private const long PetteriVehicleTransformId = 48255;
        private const long StoryTrafficFormationTransformId = 61904;
        private const long HighwayTrafficGroupTransformId = 62206;
        private const long BusTransformId = 65181;
        private const long TrainTransformId = 55611;
        private const long BoatOneTransformId = 57586;
        private const long BoatTwoTransformId = 48730;
        private const long CousinVehicleTransformId = 58170;

        private static readonly Vector3 SourceToProjectTranslation =
            new Vector3(169.98f, 1.611f, -1040.625f);

        public static LockedTrafficRouteSet LoadLockedRouteSet()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireLockedScene(scenePath);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            var routes = new[]
            {
                ExtractNumericRoute(
                    scene,
                    "route.traffic.highway",
                    1889,
                    "TRAFFIC",
                    "Routes",
                    "Highway"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.bus",
                    1084,
                    "TRAFFIC",
                    "Routes",
                    "BusRoute"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.dirt-road",
                    3719,
                    "TRAFFIC",
                    "Routes",
                    "DirtRoad"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.village",
                    295,
                    "TRAFFIC",
                    "Routes",
                    "Village"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.home-road",
                    462,
                    "TRAFFIC",
                    "Routes",
                    "HomeRoad"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.dancehall",
                    269,
                    "TRAFFIC",
                    "Routes",
                    "Dancehall"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.drag-race",
                    166,
                    "TRAFFIC",
                    "Routes",
                    "Dragrace"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.road-race",
                    624,
                    "TRAFFIC",
                    "Routes",
                    "RoadRace"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.track-field",
                    291,
                    "TRAFFIC",
                    "Routes",
                    "Trackfield"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.boat-1",
                    8,
                    "TRAFFIC",
                    "Lake",
                    "Waypoints1"),
                ExtractNumericRoute(
                    scene,
                    "route.traffic.boat-2",
                    8,
                    "TRAFFIC",
                    "Lake",
                    "Waypoints2"),
                ExtractPointRoute(
                    scene,
                    "route.traffic.train-east-to-west",
                    new[] { "TRAIN", "SpawnEast" },
                    new[] { "TRAIN", "TargetWest" }),
                ExtractPointRoute(
                    scene,
                    "route.traffic.train-west-to-east",
                    new[] { "TRAIN", "SpawnWest" },
                    new[] { "TRAIN", "TargetEast" }),
            };

            return new LockedTrafficRouteSet(
                LockedSceneSha256,
                SceneRelativePath,
                SourceToProjectTranslation,
                routes);
        }

        /// <summary>
        /// Loads only the three road branches authored by TrafficCarExpansion
        /// as configuration evidence. The supplied DLL and extracted bundle
        /// are never loaded by the editor build or by the game runtime.
        /// </summary>
        public static IReadOnlyList<LockedTrafficRoute>
            LoadTrafficExpansionRouteEvidence()
        {
            string absolutePath = Path.GetFullPath(
                TrafficExpansionEvidenceManifestPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "TrafficCarExpansion behavior-evidence manifest is missing.",
                    absolutePath);
            }

            TrafficExpansionEvidenceManifest manifest = JsonUtility.FromJson<
                TrafficExpansionEvidenceManifest>(
                File.ReadAllText(absolutePath));
            if (manifest == null || manifest.schemaVersion != 1 ||
                manifest.source == null ||
                !string.Equals(
                    manifest.source.sha256,
                    TrafficExpansionDllSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                manifest.routes == null || manifest.routes.Length != 3 ||
                !IsFinite(manifest.sourceToProjectTranslation))
            {
                throw new InvalidOperationException(
                    "TrafficCarExpansion behavior-evidence manifest header is invalid.");
            }

            var expectedCounts = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["route.traffic.mod-town-entry"] = 68,
                ["route.traffic.mod-town-loop"] = 321,
                ["route.traffic.mod-gas-pump"] = 112,
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<LockedTrafficRoute>(manifest.routes.Length);
            foreach (TrafficExpansionRouteEvidence route in manifest.routes)
            {
                if (route == null ||
                    !expectedCounts.TryGetValue(route.routeId, out int count) ||
                    !ids.Add(route.routeId) ||
                    string.IsNullOrWhiteSpace(route.sourceObjectName) ||
                    route.closesLoop ||
                    route.sourcePointCount != count ||
                    route.sourcePointStride != 1 ||
                    route.sourceWorldPoints == null ||
                    route.sourceWorldPoints.Length != count ||
                    route.sourceWorldPoints.Any(point => !IsFinite(point)))
                {
                    throw new InvalidOperationException(
                        "TrafficCarExpansion route evidence is invalid.");
                }

                for (int index = 1;
                     index < route.sourceWorldPoints.Length;
                     index++)
                {
                    if ((route.sourceWorldPoints[index] -
                         route.sourceWorldPoints[index - 1]).sqrMagnitude <
                        ExactDuplicatePointDistanceSquared)
                    {
                        throw new InvalidOperationException(
                            $"TrafficCarExpansion route '{route.routeId}' " +
                            $"contains duplicate point {index}.");
                    }
                }

                result.Add(new LockedTrafficRoute(
                    route.routeId,
                    "TrafficCarExpansion/Routes_Modded/" +
                    route.sourceObjectName,
                    donorRootTransformFileId: 0L,
                    route.sourceWorldPoints,
                    manifest.sourceToProjectTranslation));
            }

            return result;
        }

        public static LockedStoryTrafficSpawnEvidence
            LoadLockedStoryTrafficSpawnEvidence()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireLockedScene(scenePath);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            DonorTransformRecord jani =
                scene.GetTransform(JaniVehicleTransformId);
            DonorTransformRecord petteri =
                scene.GetTransform(PetteriVehicleTransformId);
            if (jani.FatherTransformId != StoryTrafficFormationTransformId ||
                petteri.FatherTransformId != StoryTrafficFormationTransformId ||
                !string.Equals(
                    scene.GetGameObjectName(jani.GameObjectId),
                    "KYLAJANI",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    scene.GetGameObjectName(petteri.GameObjectId),
                    "AMIS2",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Locked donor Jani/Petteri formation evidence changed.");
            }

            return new LockedStoryTrafficSpawnEvidence(
                StoryTrafficFormationTransformId,
                JaniVehicleTransformId,
                PetteriVehicleTransformId,
                scene.GetWorldPosition(JaniVehicleTransformId) +
                    SourceToProjectTranslation,
                scene.GetWorldPosition(PetteriVehicleTransformId) +
                    SourceToProjectTranslation);
        }

        public static IReadOnlyList<LockedAmbientTrafficActorEvidence>
            LoadLockedAmbientTrafficEvidence()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireLockedScene(scenePath);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            DonorTransformRecord highwayGroup = scene.GetTransform(
                HighwayTrafficGroupTransformId);
            if (!string.Equals(
                    scene.GetGameObjectName(highwayGroup.GameObjectId),
                    "VehiclesHighway",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Locked donor ambient highway traffic group changed.");
            }

            var specs = new[]
            {
                new AmbientEvidenceSpec("truck.01", "truck", "TRUCK", 57659L),
                new AmbientEvidenceSpec("svoboda.01", "svoboda", "SVOBODA", 66128L),
                new AmbientEvidenceSpec("lamore.01", "lamore", "LAMORE", 51149L),
                new AmbientEvidenceSpec("lamore.02", "lamore", "LAMORE", 71200L),
                new AmbientEvidenceSpec("victro.01", "victro", "VICTRO", 63484L),
                new AmbientEvidenceSpec("victro.02", "victro", "VICTRO", 59256L),
                new AmbientEvidenceSpec("victro.03", "victro", "VICTRO", 47143L),
                new AmbientEvidenceSpec("menace.01", "menace", "MENACE", 67193L),
                new AmbientEvidenceSpec("fittan.01", "fittan", "FITTAN", 67911L),
                new AmbientEvidenceSpec("polsa.01", "polsa", "POLSA", 44840L),
            };
            return specs.Select(spec =>
            {
                DonorTransformRecord donor = scene.GetTransform(
                    spec.TransformFileId);
                string donorName = scene.GetGameObjectName(donor.GameObjectId);
                if (donor.FatherTransformId != HighwayTrafficGroupTransformId ||
                    !string.Equals(
                        donorName,
                        spec.DonorName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Locked donor ambient traffic instance " +
                        $"{spec.TransformFileId} changed.");
                }

                return new LockedAmbientTrafficActorEvidence(
                    spec.InstanceSuffix,
                    spec.ArchetypeId,
                    donorName,
                    spec.TransformFileId,
                    scene.GetWorldPosition(spec.TransformFileId) +
                        SourceToProjectTranslation,
                    scene.GetWorldRotation(spec.TransformFileId));
            }).ToArray();
        }

        public static LockedCousinTrafficEvidence
            LoadLockedCousinTrafficEvidence()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireLockedScene(scenePath);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            RequireTransformName(scene, CousinVehicleTransformId, "FITTAN");
            return new LockedCousinTrafficEvidence(
                CousinVehicleTransformId,
                scene.GetWorldPosition(CousinVehicleTransformId) +
                    SourceToProjectTranslation,
                scene.GetWorldRotation(CousinVehicleTransformId));
        }

        public static LockedTrafficTransportEvidence
            LoadLockedTransportEvidence()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireLockedScene(scenePath);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            RequireTransformName(scene, BusTransformId, "BUS");
            RequireTransformName(scene, TrainTransformId, "TRAIN");
            RequireTransformName(scene, BoatOneTransformId, "AIboat1");
            RequireTransformName(scene, BoatTwoTransformId, "AIboat2");
            RequireTransformName(scene, 56918L, "BusStopLoppe");
            RequireTransformName(scene, 44529L, "BusStopKesseli");
            RequireTransformName(scene, 66787L, "BusStopRykipohja");

            // These values are decoded from the locked BUS Setup/Route Start,
            // Bus Stop, TRAIN Move and boat steering PlayMaker records. The
            // records remain evidence only and never enter the new runtime.
            return new LockedTrafficTransportEvidence(
                BusTransformId,
                TrainTransformId,
                BoatOneTransformId,
                BoatTwoTransformId,
                // BUS Setup/WAKEUP phase table. These are not recurring
                // departures: the selected index is applied once when the bus
                // is bootstrapped, then Navigation loops BusRoute forever.
                new[]
                {
                    new LockedBusDepartureEvidence(0, 3, "perajarvi"),
                    new LockedBusDepartureEvidence(2, 890, "rykipohja"),
                    new LockedBusDepartureEvidence(4, 482, "loppe"),
                    new LockedBusDepartureEvidence(6, 3, "perajarvi"),
                    new LockedBusDepartureEvidence(8, 890, "rykipohja"),
                    new LockedBusDepartureEvidence(10, 482, "loppe"),
                    new LockedBusDepartureEvidence(12, 3, "perajarvi"),
                    new LockedBusDepartureEvidence(14, 890, "rykipohja"),
                    new LockedBusDepartureEvidence(16, 482, "loppe"),
                    new LockedBusDepartureEvidence(18, 3, "perajarvi"),
                    new LockedBusDepartureEvidence(20, 890, "rykipohja"),
                    new LockedBusDepartureEvidence(22, 482, "loppe"),
                },
                // Projected cumulative distances of the three locked donor
                // BusStop transforms on the 13,698.389 m BusRoute. The old
                // 61/2038/3598 values were mutable GetDistance FSM outputs,
                // not route distances, and placed service stops at unrelated
                // world locations (including the sewage-disposal station).
                new[]
                {
                    new LockedTrafficStopEvidence(
                        "stop.traffic.bus.loppe",
                        5207.049f),
                    new LockedTrafficStopEvidence(
                        "stop.traffic.bus.kesseli",
                        7774.381f),
                    new LockedTrafficStopEvidence(
                        "stop.traffic.bus.rykipohja",
                        12027.058f),
                },
                trainSpeedMetersPerSecond: 30f,
                trainEndpointDelaySimulationSeconds: 250f,
                // BUS/Throttle uses SpeedMin=80 and SpeedMax=85 km/h.
                // The current transport definition stores one cruise target,
                // so use the midpoint until its donor randomizer is modelled.
                busSpeedMetersPerSecond: 82.5f / 3.6f,
                boatSpeedMetersPerSecond: 8f);
        }

        private static void RequireTransformName(
            DonorUnitySceneModel scene,
            long transformId,
            string expectedName)
        {
            DonorTransformRecord transform = scene.GetTransform(transformId);
            string actualName = scene.GetGameObjectName(transform.GameObjectId);
            if (!string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Locked donor transform {transformId} was expected to be " +
                    $"'{expectedName}', got '{actualName}'.");
            }
        }

        private static LockedTrafficRoute ExtractNumericRoute(
            DonorUnitySceneModel scene,
            string routeId,
            int expectedPointCount,
            params string[] hierarchyPath)
        {
            DonorTransformRecord routeRoot =
                scene.GetUniqueTransformByPath(hierarchyPath);
            var indexed = new List<KeyValuePair<int, DonorTransformRecord>>();
            foreach (DonorTransformRecord child in
                     scene.GetDirectChildren(routeRoot.TransformId))
            {
                string name = scene.GetGameObjectName(child.GameObjectId);
                if (int.TryParse(
                        name,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int index))
                {
                    indexed.Add(new KeyValuePair<int, DonorTransformRecord>(
                        index,
                        child));
                }
            }

            indexed.Sort((left, right) => left.Key.CompareTo(right.Key));
            int firstIndex = indexed.Count > 0 ? indexed[0].Key : -1;
            if (indexed.Count != expectedPointCount ||
                indexed.Count < 2 ||
                firstIndex < 0 || firstIndex > 1 ||
                indexed.Where((pair, index) =>
                    pair.Key != firstIndex + index).Any())
            {
                throw new InvalidOperationException(
                    $"Locked donor route '{string.Join("/", hierarchyPath)}' " +
                    $"does not contain its expected contiguous " +
                    $"{expectedPointCount}-waypoint set.");
            }

            Vector3[] sourcePoints = indexed
                .Select(pair => scene.GetWorldPosition(
                    pair.Value.TransformId))
                .ToArray();
            return new LockedTrafficRoute(
                routeId,
                string.Join("/", hierarchyPath),
                routeRoot.TransformId,
                sourcePoints,
                SourceToProjectTranslation);
        }

        private static LockedTrafficRoute ExtractPointRoute(
            DonorUnitySceneModel scene,
            string routeId,
            string[] startPath,
            string[] endPath)
        {
            DonorTransformRecord start =
                scene.GetUniqueTransformByPath(startPath);
            DonorTransformRecord end =
                scene.GetUniqueTransformByPath(endPath);
            return new LockedTrafficRoute(
                routeId,
                string.Join("/", startPath) + " -> " +
                string.Join("/", endPath),
                start.TransformId,
                new[]
                {
                    scene.GetWorldPosition(start.TransformId),
                    scene.GetWorldPosition(end.TransformId),
                },
                SourceToProjectTranslation);
        }

        private static void RequireLockedScene(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The locked donor GAME scene is missing.",
                    path);
            }

            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string actual = string.Concat(
                sha.ComputeHash(stream).Select(value =>
                    value.ToString("x2", CultureInfo.InvariantCulture)));
            if (!string.Equals(
                    actual,
                    LockedSceneSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Locked donor GAME scene hash mismatch. Expected " +
                    $"{LockedSceneSha256}, found {actual}.");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        [Serializable]
        private sealed class TrafficExpansionEvidenceManifest
        {
            public int schemaVersion;
            public TrafficExpansionSourceEvidence source;
            public Vector3 sourceToProjectTranslation;
            public TrafficExpansionRouteEvidence[] routes =
                Array.Empty<TrafficExpansionRouteEvidence>();
        }

        [Serializable]
        private sealed class TrafficExpansionSourceEvidence
        {
            public string sha256 = string.Empty;
        }

        [Serializable]
        private sealed class TrafficExpansionRouteEvidence
        {
            public string routeId = string.Empty;
            public string sourceObjectName = string.Empty;
            public bool closesLoop;
            public int sourcePointCount;
            public int sourcePointStride;
            public Vector3[] sourceWorldPoints = Array.Empty<Vector3>();
        }

        private readonly struct AmbientEvidenceSpec
        {
            public AmbientEvidenceSpec(
                string instanceSuffix,
                string archetypeId,
                string donorName,
                long transformFileId)
            {
                InstanceSuffix = instanceSuffix;
                ArchetypeId = archetypeId;
                DonorName = donorName;
                TransformFileId = transformFileId;
            }

            public string InstanceSuffix { get; }
            public string ArchetypeId { get; }
            public string DonorName { get; }
            public long TransformFileId { get; }
        }
    }

    public sealed class LockedTrafficRouteSet
    {
        private readonly Dictionary<string, LockedTrafficRoute> byId;

        internal LockedTrafficRouteSet(
            string sourceSceneSha256,
            string sourceSceneRelativePath,
            Vector3 sourceToProjectTranslation,
            IEnumerable<LockedTrafficRoute> routes)
        {
            SourceSceneSha256 = sourceSceneSha256 ?? string.Empty;
            SourceSceneRelativePath = sourceSceneRelativePath ?? string.Empty;
            SourceToProjectTranslation = sourceToProjectTranslation;
            Routes = (routes ?? throw new ArgumentNullException(nameof(routes)))
                .ToArray();
            byId = Routes.ToDictionary(
                route => route.RouteId,
                StringComparer.Ordinal);
        }

        public string SourceSceneSha256 { get; }
        public string SourceSceneRelativePath { get; }
        public Vector3 SourceToProjectTranslation { get; }
        public IReadOnlyList<LockedTrafficRoute> Routes { get; }

        public LockedTrafficRoute RequireRoute(string routeId)
        {
            if (!byId.TryGetValue(
                    routeId ?? string.Empty,
                    out LockedTrafficRoute route))
            {
                throw new InvalidOperationException(
                    $"Locked traffic route '{routeId}' is unavailable.");
            }

            return route;
        }
    }

    public sealed class LockedStoryTrafficSpawnEvidence
    {
        internal LockedStoryTrafficSpawnEvidence(
            long formationTransformId,
            long janiVehicleTransformId,
            long petteriVehicleTransformId,
            Vector3 janiProjectPosition,
            Vector3 petteriProjectPosition)
        {
            FormationTransformId = formationTransformId;
            JaniVehicleTransformId = janiVehicleTransformId;
            PetteriVehicleTransformId = petteriVehicleTransformId;
            JaniProjectPosition = janiProjectPosition;
            PetteriProjectPosition = petteriProjectPosition;
        }

        public long FormationTransformId { get; }
        public long JaniVehicleTransformId { get; }
        public long PetteriVehicleTransformId { get; }
        public Vector3 JaniProjectPosition { get; }
        public Vector3 PetteriProjectPosition { get; }
    }

    public sealed class LockedAmbientTrafficActorEvidence
    {
        internal LockedAmbientTrafficActorEvidence(
            string instanceSuffix,
            string archetypeId,
            string donorName,
            long donorTransformFileId,
            Vector3 projectPosition,
            Quaternion projectRotation)
        {
            InstanceSuffix = instanceSuffix ?? string.Empty;
            ArchetypeId = archetypeId ?? string.Empty;
            DonorName = donorName ?? string.Empty;
            DonorTransformFileId = donorTransformFileId;
            ProjectPosition = projectPosition;
            ProjectRotation = projectRotation;
        }

        public string InstanceSuffix { get; }
        public string ArchetypeId { get; }
        public string DonorName { get; }
        public long DonorTransformFileId { get; }
        public Vector3 ProjectPosition { get; }
        public Quaternion ProjectRotation { get; }
    }

    public sealed class LockedCousinTrafficEvidence
    {
        internal LockedCousinTrafficEvidence(
            long donorTransformFileId,
            Vector3 projectPosition,
            Quaternion projectRotation)
        {
            DonorTransformFileId = donorTransformFileId;
            ProjectPosition = projectPosition;
            ProjectRotation = projectRotation;
        }

        public long DonorTransformFileId { get; }
        public Vector3 ProjectPosition { get; }
        public Quaternion ProjectRotation { get; }
    }

    public sealed class LockedTrafficRoute
    {
        internal LockedTrafficRoute(
            string routeId,
            string donorHierarchyPath,
            long donorRootTransformFileId,
            IEnumerable<Vector3> sourceWorldPoints,
            Vector3 sourceToProjectTranslation)
        {
            RouteId = routeId ?? string.Empty;
            DonorHierarchyPath = donorHierarchyPath ?? string.Empty;
            DonorRootTransformFileId = donorRootTransformFileId;
            SourceWorldPoints = (sourceWorldPoints ??
                    throw new ArgumentNullException(nameof(sourceWorldPoints)))
                .ToArray();
            ProjectWorldPoints = SourceWorldPoints
                .Select(point => point + sourceToProjectTranslation)
                .ToArray();
        }

        public string RouteId { get; }
        public string DonorHierarchyPath { get; }
        public long DonorRootTransformFileId { get; }
        public IReadOnlyList<Vector3> SourceWorldPoints { get; }
        public IReadOnlyList<Vector3> ProjectWorldPoints { get; }
    }

    public sealed class LockedTrafficTransportEvidence
    {
        internal LockedTrafficTransportEvidence(
            long busTransformFileId,
            long trainTransformFileId,
            long boatOneTransformFileId,
            long boatTwoTransformFileId,
            IEnumerable<LockedBusDepartureEvidence> busDepartures,
            IEnumerable<LockedTrafficStopEvidence> busStops,
            float trainSpeedMetersPerSecond,
            float trainEndpointDelaySimulationSeconds,
            float busSpeedMetersPerSecond,
            float boatSpeedMetersPerSecond)
        {
            BusTransformFileId = busTransformFileId;
            TrainTransformFileId = trainTransformFileId;
            BoatOneTransformFileId = boatOneTransformFileId;
            BoatTwoTransformFileId = boatTwoTransformFileId;
            BusDepartures = busDepartures.ToArray();
            BusStops = busStops.ToArray();
            TrainSpeedMetersPerSecond = trainSpeedMetersPerSecond;
            TrainEndpointDelaySimulationSeconds =
                trainEndpointDelaySimulationSeconds;
            BusSpeedMetersPerSecond = busSpeedMetersPerSecond;
            BoatSpeedMetersPerSecond = boatSpeedMetersPerSecond;
        }

        public long BusTransformFileId { get; }
        public long TrainTransformFileId { get; }
        public long BoatOneTransformFileId { get; }
        public long BoatTwoTransformFileId { get; }
        public IReadOnlyList<LockedBusDepartureEvidence> BusDepartures { get; }
        public IReadOnlyList<LockedTrafficStopEvidence> BusStops { get; }
        public float TrainSpeedMetersPerSecond { get; }
        public float TrainEndpointDelaySimulationSeconds { get; }
        public float BusSpeedMetersPerSecond { get; }
        public float BoatSpeedMetersPerSecond { get; }
    }

    public readonly struct LockedBusDepartureEvidence
    {
        internal LockedBusDepartureEvidence(
            int hour,
            int routeStartPointIndex,
            string originId)
        {
            Hour = hour;
            RouteStartPointIndex = routeStartPointIndex;
            OriginId = originId ?? string.Empty;
        }

        public int Hour { get; }
        public int RouteStartPointIndex { get; }
        public string OriginId { get; }
    }

    public readonly struct LockedTrafficStopEvidence
    {
        internal LockedTrafficStopEvidence(
            string stopId,
            float routeDistanceMeters)
        {
            StopId = stopId ?? string.Empty;
            RouteDistanceMeters = routeDistanceMeters;
        }

        public string StopId { get; }
        public float RouteDistanceMeters { get; }
    }
}
