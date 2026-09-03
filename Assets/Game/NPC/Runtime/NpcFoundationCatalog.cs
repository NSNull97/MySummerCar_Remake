using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using UnityEngine;

namespace MSC.NPC
{
    public enum NpcRouteTraversalMode
    {
        Once = 0,
        Loop = 1,
        PingPong = 2
    }

    public enum NpcRouteInterpolationMode
    {
        Linear = 0,
        CatmullRom = 1
    }

    public enum NpcRouteSurfaceMode
    {
        TerrainConformed = 0,
        AuthoredHeight = 1
    }

    public readonly struct NpcRouteTraversalSample
    {
        public NpcRouteTraversalSample(double progress01, bool isForward)
        {
            Progress01 = progress01;
            IsForward = isForward;
        }

        public double Progress01 { get; }
        public bool IsForward { get; }
    }

    [Serializable]
    public sealed class NpcAnchorDefinition
    {
        [SerializeField] private string anchorId = string.Empty;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 eulerAngles;
        [SerializeField] private string vehicleBindingId = string.Empty;

        public NpcAnchorDefinition(
            string id,
            string cell,
            Vector3 worldPosition,
            Vector3 worldEulerAngles,
            string vehicleId = "")
        {
            anchorId = id ?? string.Empty;
            cellId = cell ?? string.Empty;
            position = worldPosition;
            eulerAngles = worldEulerAngles;
            vehicleBindingId = vehicleId ?? string.Empty;
        }

        public string AnchorId => anchorId;
        public string CellId => cellId;
        public Vector3 Position => position;
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
        public string VehicleBindingId => vehicleBindingId;

        public bool TryValidate(out string failure)
        {
            if (!CharacterStableId.TryValidate(anchorId, "anchor.", out failure))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(cellId) ||
                !IsFinite(position) || !IsFinite(eulerAngles))
            {
                failure = $"NPC anchor '{anchorId}' has invalid cell or transform data.";
                return false;
            }

            if (!string.IsNullOrEmpty(vehicleBindingId) &&
                !CharacterStableId.TryValidate(
                    vehicleBindingId,
                    "vehicle.",
                    out failure))
            {
                return false;
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
    public sealed class NpcRouteDefinition
    {
        [SerializeField] private string routeId = string.Empty;
        [SerializeField] private string fromAnchorId = string.Empty;
        [SerializeField] private string toAnchorId = string.Empty;
        [SerializeField] private string[] waypointAnchorIds = Array.Empty<string>();
        [SerializeField] private double traversalGameSeconds = 60d;
        [SerializeField] private NpcRouteTraversalMode traversalMode =
            NpcRouteTraversalMode.Once;
        [SerializeField] private NpcRouteInterpolationMode interpolationMode =
            NpcRouteInterpolationMode.Linear;
        [SerializeField] private NpcRouteSurfaceMode surfaceMode =
            NpcRouteSurfaceMode.TerrainConformed;
        [SerializeField] private double[] waypointProgress01 =
            Array.Empty<double>();

        public NpcRouteDefinition(
            string id,
            string from,
            string to,
            double durationGameSeconds,
            NpcRouteTraversalMode configuredTraversalMode =
                NpcRouteTraversalMode.Once,
            NpcRouteInterpolationMode configuredInterpolationMode =
                NpcRouteInterpolationMode.Linear,
            IEnumerable<double> configuredWaypointProgress01 = null,
            NpcRouteSurfaceMode configuredSurfaceMode =
                NpcRouteSurfaceMode.TerrainConformed)
        {
            routeId = id ?? string.Empty;
            fromAnchorId = from ?? string.Empty;
            toAnchorId = to ?? string.Empty;
            waypointAnchorIds = new[] { fromAnchorId, toAnchorId };
            traversalGameSeconds = durationGameSeconds;
            traversalMode = configuredTraversalMode;
            interpolationMode = configuredInterpolationMode;
            surfaceMode = configuredSurfaceMode;
            waypointProgress01 = (configuredWaypointProgress01 ??
                    Enumerable.Empty<double>())
                .ToArray();
        }

        public NpcRouteDefinition(
            string id,
            IEnumerable<string> configuredWaypointAnchorIds,
            double durationGameSeconds,
            NpcRouteTraversalMode configuredTraversalMode =
                NpcRouteTraversalMode.Once,
            NpcRouteInterpolationMode configuredInterpolationMode =
                NpcRouteInterpolationMode.Linear,
            IEnumerable<double> configuredWaypointProgress01 = null,
            NpcRouteSurfaceMode configuredSurfaceMode =
                NpcRouteSurfaceMode.TerrainConformed)
        {
            routeId = id ?? string.Empty;
            waypointAnchorIds = (configuredWaypointAnchorIds ??
                    Enumerable.Empty<string>())
                .ToArray();
            fromAnchorId = waypointAnchorIds.FirstOrDefault() ?? string.Empty;
            toAnchorId = waypointAnchorIds.LastOrDefault() ?? string.Empty;
            traversalGameSeconds = durationGameSeconds;
            traversalMode = configuredTraversalMode;
            interpolationMode = configuredInterpolationMode;
            surfaceMode = configuredSurfaceMode;
            waypointProgress01 = (configuredWaypointProgress01 ??
                    Enumerable.Empty<double>())
                .ToArray();
        }

        public string RouteId => routeId;
        public string FromAnchorId => fromAnchorId;
        public string ToAnchorId => toAnchorId;
        public IReadOnlyList<string> WaypointAnchorIds =>
            waypointAnchorIds == null || waypointAnchorIds.Length == 0
                ? new[] { fromAnchorId, toAnchorId }
                : waypointAnchorIds;
        public double TraversalGameSeconds => traversalGameSeconds;
        public NpcRouteTraversalMode TraversalMode => traversalMode;
        public NpcRouteInterpolationMode InterpolationMode =>
            interpolationMode;
        public NpcRouteSurfaceMode SurfaceMode => surfaceMode;
        public IReadOnlyList<double> WaypointProgress01 =>
            waypointProgress01 ?? Array.Empty<double>();

        public NpcRouteTraversalSample ResolveTraversal(
            double secondsSinceStart)
        {
            if (!double.IsFinite(secondsSinceStart) || secondsSinceStart < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(secondsSinceStart));
            }

            switch (traversalMode)
            {
                case NpcRouteTraversalMode.Once:
                    return new NpcRouteTraversalSample(
                        Math.Clamp(
                            secondsSinceStart / traversalGameSeconds,
                            0d,
                            1d),
                        isForward: true);

                case NpcRouteTraversalMode.Loop:
                    return new NpcRouteTraversalSample(
                        secondsSinceStart % traversalGameSeconds /
                        traversalGameSeconds,
                        isForward: true);

                case NpcRouteTraversalMode.PingPong:
                    double cycleSeconds = traversalGameSeconds * 2d;
                    double cycleProgress = secondsSinceStart % cycleSeconds;
                    bool isForward = cycleProgress <= traversalGameSeconds;
                    return new NpcRouteTraversalSample(
                        isForward
                            ? cycleProgress / traversalGameSeconds
                            : 2d - cycleProgress / traversalGameSeconds,
                        isForward);

                default:
                    throw new InvalidOperationException(
                        $"NPC route '{routeId}' has unsupported traversal mode '{traversalMode}'.");
            }
        }

        public double ResolveProgress01(double secondsSinceStart) =>
            ResolveTraversal(secondsSinceStart).Progress01;

        public bool TryValidate(out string failure)
        {
            if (!CharacterStableId.TryValidate(routeId, "route.", out failure))
            {
                return false;
            }

            IReadOnlyList<string> waypoints = WaypointAnchorIds;
            if (waypoints.Count < 2)
            {
                failure = $"NPC route '{routeId}' requires at least two waypoint anchors.";
                return false;
            }

            for (int index = 0; index < waypoints.Count; index++)
            {
                if (!CharacterStableId.TryValidate(
                        waypoints[index],
                        "anchor.",
                        out failure) ||
                    index > 0 && string.Equals(
                        waypoints[index - 1],
                        waypoints[index],
                        StringComparison.Ordinal))
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? $"NPC route '{routeId}' repeats consecutive waypoint '{waypoints[index]}'."
                        : failure;
                    return false;
                }
            }

            if (!double.IsFinite(traversalGameSeconds) ||
                traversalGameSeconds <= 0d ||
                string.Equals(
                    waypoints[0],
                    waypoints[waypoints.Count - 1],
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(NpcRouteTraversalMode), traversalMode) ||
                !Enum.IsDefined(
                    typeof(NpcRouteInterpolationMode),
                    interpolationMode) ||
                !Enum.IsDefined(typeof(NpcRouteSurfaceMode), surfaceMode))
            {
                failure = $"NPC route '{routeId}' has invalid endpoints or duration.";
                return false;
            }

            IReadOnlyList<double> waypointProgress = WaypointProgress01;
            if (waypointProgress.Count > 0)
            {
                if (waypointProgress.Count != waypoints.Count ||
                    Math.Abs(waypointProgress[0]) > 0.000001d ||
                    Math.Abs(waypointProgress[waypointProgress.Count - 1] - 1d) >
                    0.000001d)
                {
                    failure =
                        $"NPC route '{routeId}' has invalid waypoint timing endpoints.";
                    return false;
                }

                for (int index = 0; index < waypointProgress.Count; index++)
                {
                    if (!double.IsFinite(waypointProgress[index]) ||
                        waypointProgress[index] < 0d ||
                        waypointProgress[index] > 1d ||
                        index > 0 && waypointProgress[index] <=
                        waypointProgress[index - 1])
                    {
                        failure =
                            $"NPC route '{routeId}' has non-monotonic waypoint timing.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class NpcScheduleBlock
    {
        [SerializeField] private string scheduleBlockId = string.Empty;
        [SerializeField] private string characterDefinitionId = string.Empty;
        [SerializeField, Range(0, 127)] private int dayMask = 127;
        [SerializeField, Range(0f, 86400f)] private double startSecondsOfDay;
        [SerializeField, Range(0f, 86400f)] private double endSecondsOfDay = 86400d;
        [SerializeField] private string anchorId = string.Empty;
        [SerializeField] private string routeId = string.Empty;
        [SerializeField] private string presentationBindingIdOverride =
            string.Empty;
        [SerializeField] private CharacterActivityState activityState =
            CharacterActivityState.Idle;

        public NpcScheduleBlock(
            string id,
            string characterId,
            int configuredDayMask,
            double startSeconds,
            double endSeconds,
            string configuredAnchorId,
            string configuredRouteId,
            CharacterActivityState activity,
            string configuredPresentationBindingIdOverride = "")
        {
            scheduleBlockId = id ?? string.Empty;
            characterDefinitionId = characterId ?? string.Empty;
            dayMask = configuredDayMask;
            startSecondsOfDay = startSeconds;
            endSecondsOfDay = endSeconds;
            anchorId = configuredAnchorId ?? string.Empty;
            routeId = configuredRouteId ?? string.Empty;
            presentationBindingIdOverride =
                configuredPresentationBindingIdOverride ?? string.Empty;
            activityState = activity;
        }

        public string ScheduleBlockId => scheduleBlockId;
        public string CharacterDefinitionId => characterDefinitionId;
        public int DayMask => dayMask;
        public double StartSecondsOfDay => startSecondsOfDay;
        public double EndSecondsOfDay => endSecondsOfDay;
        public string AnchorId => anchorId;
        public string RouteId => routeId;
        public string PresentationBindingIdOverride =>
            presentationBindingIdOverride;
        public CharacterActivityState ActivityState => activityState;

        public bool IsActive(long dayIndex, double secondsOfDay)
        {
            long owningDayIndex = dayIndex;
            if (startSecondsOfDay > endSecondsOfDay &&
                secondsOfDay < endSecondsOfDay)
            {
                // The after-midnight tail belongs to the day on which the
                // overnight block started. This keeps Saturday-night service
                // active into Sunday without inventing a Sunday-night shift.
                owningDayIndex--;
            }

            int day = (int)((owningDayIndex % 7L + 7L) % 7L);
            if ((dayMask & (1 << day)) == 0)
            {
                return false;
            }

            return startSecondsOfDay <= endSecondsOfDay
                ? secondsOfDay >= startSecondsOfDay && secondsOfDay < endSecondsOfDay
                : secondsOfDay >= startSecondsOfDay || secondsOfDay < endSecondsOfDay;
        }

        public double SecondsSinceStart(double secondsOfDay) =>
            secondsOfDay >= startSecondsOfDay
                ? secondsOfDay - startSecondsOfDay
                : 86400d - startSecondsOfDay + secondsOfDay;

        public bool TryValidate(out string failure)
        {
            if (!CharacterStableId.TryValidate(
                    scheduleBlockId,
                    "schedule.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    characterDefinitionId,
                    "character.",
                    out failure) ||
                !CharacterStableId.TryValidate(anchorId, "anchor.", out failure) ||
                !string.IsNullOrEmpty(routeId) &&
                !CharacterStableId.TryValidate(routeId, "route.", out failure) ||
                !string.IsNullOrEmpty(presentationBindingIdOverride) &&
                !CharacterStableId.TryValidate(
                    presentationBindingIdOverride,
                    "presentation.character.",
                    out failure))
            {
                return false;
            }

            if (dayMask <= 0 || dayMask > 127 ||
                !double.IsFinite(startSecondsOfDay) ||
                !double.IsFinite(endSecondsOfDay) ||
                startSecondsOfDay < 0d || startSecondsOfDay >= 86400d ||
                endSecondsOfDay <= 0d || endSecondsOfDay > 86400d ||
                startSecondsOfDay.Equals(endSecondsOfDay) ||
                !Enum.IsDefined(typeof(CharacterActivityState), activityState))
            {
                failure = $"NPC schedule block '{scheduleBlockId}' is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "NpcFoundationCatalog",
        menuName = "MSC/NPC/Foundation Catalog")]
    public sealed class NpcFoundationCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string catalogId = "npc.foundation.phase1.v1";
        [SerializeField] private NpcAnchorDefinition[] anchors =
            Array.Empty<NpcAnchorDefinition>();
        [SerializeField] private NpcRouteDefinition[] routes =
            Array.Empty<NpcRouteDefinition>();
        [SerializeField] private NpcScheduleBlock[] scheduleBlocks =
            Array.Empty<NpcScheduleBlock>();

        public IReadOnlyList<NpcAnchorDefinition> Anchors =>
            anchors ?? Array.Empty<NpcAnchorDefinition>();
        public IReadOnlyList<NpcRouteDefinition> Routes =>
            routes ?? Array.Empty<NpcRouteDefinition>();
        public IReadOnlyList<NpcScheduleBlock> ScheduleBlocks =>
            scheduleBlocks ?? Array.Empty<NpcScheduleBlock>();

        public bool TryGetAnchor(string id, out NpcAnchorDefinition anchor) =>
            TryFind(Anchors, candidate => candidate.AnchorId, id, out anchor);

        public bool TryGetRoute(string id, out NpcRouteDefinition route) =>
            TryFind(Routes, candidate => candidate.RouteId, id, out route);

        public IReadOnlyList<NpcScheduleBlock> GetSchedule(string characterId) =>
            ScheduleBlocks
                .Where(block => string.Equals(
                    block.CharacterDefinitionId,
                    characterId,
                    StringComparison.Ordinal))
                .OrderBy(block => block.StartSecondsOfDay)
                .ThenBy(block => block.ScheduleBlockId, StringComparer.Ordinal)
                .ToArray();

        public bool TryGetScheduleBlock(
            string scheduleBlockId,
            out NpcScheduleBlock scheduleBlock) =>
            TryFind(
                ScheduleBlocks,
                candidate => candidate.ScheduleBlockId,
                scheduleBlockId,
                out scheduleBlock);

        public IReadOnlyList<string> ValidateConfiguration(
            CharacterDefinitionCatalog characters)
        {
            var failures = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                failures.Add("NPC foundation schema version is unsupported.");
            }

            if (!CharacterStableId.TryValidate(
                    catalogId,
                    "npc.",
                    out string failure))
            {
                failures.Add(failure);
            }

            ValidateUnique(Anchors, item => item.AnchorId, item => item.TryValidate(out _), "anchor", failures);
            ValidateUnique(Routes, item => item.RouteId, item => item.TryValidate(out _), "route", failures);
            ValidateUnique(ScheduleBlocks, item => item.ScheduleBlockId, item => item.TryValidate(out _), "schedule", failures);

            foreach (NpcRouteDefinition route in Routes)
            {
                if (route.WaypointAnchorIds.Any(anchorId =>
                        !TryGetAnchor(anchorId, out _)))
                {
                    failures.Add($"NPC route '{route.RouteId}' references an unknown anchor.");
                }
            }

            foreach (NpcScheduleBlock block in ScheduleBlocks)
            {
                if (characters == null ||
                    !characters.TryGet(
                        block.CharacterDefinitionId,
                        out CharacterDefinition character) ||
                    !TryGetAnchor(block.AnchorId, out _) ||
                    !string.IsNullOrEmpty(block.RouteId) &&
                    !TryGetRoute(block.RouteId, out _))
                {
                    failures.Add($"NPC schedule '{block.ScheduleBlockId}' has an unresolved project-owned reference.");
                }
                else if (character.StateOnly)
                {
                    failures.Add(
                        $"NPC schedule '{block.ScheduleBlockId}' cannot materialize state-only character '{character.DefinitionId}'.");
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            IEnumerable<NpcAnchorDefinition> configuredAnchors,
            IEnumerable<NpcRouteDefinition> configuredRoutes,
            IEnumerable<NpcScheduleBlock> configuredScheduleBlocks)
        {
            schemaVersion = CurrentSchemaVersion;
            catalogId = id ?? string.Empty;
            anchors = Order(configuredAnchors, item => item.AnchorId);
            routes = Order(configuredRoutes, item => item.RouteId);
            scheduleBlocks = Order(
                configuredScheduleBlocks,
                item => item.ScheduleBlockId);
        }
#endif

        private static bool TryFind<T>(
            IReadOnlyList<T> values,
            Func<T, string> idSelector,
            string id,
            out T result)
            where T : class
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(idSelector(values[index]), id, StringComparison.Ordinal))
                {
                    result = values[index];
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static void ValidateUnique<T>(
            IReadOnlyList<T> values,
            Func<T, string> idSelector,
            Func<T, bool> validator,
            string label,
            ICollection<string> failures)
            where T : class
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                T value = values[index];
                if (value == null || !validator(value))
                {
                    failures.Add($"NPC {label} entry {index} is invalid.");
                    continue;
                }

                if (!ids.Add(idSelector(value)))
                {
                    failures.Add($"Duplicate NPC {label} ID '{idSelector(value)}'.");
                }
            }
        }

#if UNITY_EDITOR
        private static T[] Order<T>(IEnumerable<T> values, Func<T, string> selector) =>
            (values ?? Enumerable.Empty<T>())
                .OrderBy(selector, StringComparer.Ordinal)
                .ToArray();
#endif
    }
}
