using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using UnityEngine;

namespace MSC.NPC
{
    public readonly struct NpcPose
    {
        public NpcPose(
            Vector3 position,
            Quaternion rotation,
            string cellId,
            bool shouldConformToGround = false)
        {
            Position = position;
            Rotation = rotation;
            CellId = cellId ?? string.Empty;
            ShouldConformToGround = shouldConformToGround;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public string CellId { get; }
        public bool ShouldConformToGround { get; }
    }

    public readonly struct NpcTraceEntry
    {
        public NpcTraceEntry(
            double gameSeconds,
            string characterId,
            string eventId,
            string details)
        {
            GameSeconds = gameSeconds;
            CharacterId = characterId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public double GameSeconds { get; }
        public string CharacterId { get; }
        public string EventId { get; }
        public string Details { get; }
    }

    /// <summary>
    /// Deterministic project-owned schedule and off-screen route simulation.
    /// It has no dependency on loaded scenes, NavMesh state, animation or donor
    /// hierarchy names.
    /// </summary>
    public sealed class NpcSimulation
    {
        private const int MaximumTraceEntries = 128;
        private const float PhysicalRouteCommitCorridorMeters = 5f;

        private readonly NpcFoundationCatalog foundation;
        private readonly Dictionary<string, CharacterInstance> instances;
        private readonly Dictionary<string, bool> routeTraversalForward;
        private readonly Dictionary<string, ResolvedRouteGeometry>
            routeGeometry;
        private readonly HashSet<string> physicalRouteAuthorities =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> physicalRouteDrivers =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> physicalRouteClocks =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> physicalRouteSampleHints =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> physicalRouteSampleRoutes =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<NpcTraceEntry> trace =
            new Queue<NpcTraceEntry>();

        public NpcSimulation(
            CharacterDefinitionCatalog characterCatalog,
            NpcFoundationCatalog foundationCatalog)
        {
            if (characterCatalog == null)
            {
                throw new ArgumentNullException(nameof(characterCatalog));
            }

            foundation = foundationCatalog ??
                throw new ArgumentNullException(nameof(foundationCatalog));
            IReadOnlyList<string> characterFailures =
                characterCatalog.ValidateConfiguration();
            IReadOnlyList<string> foundationFailures =
                foundation.ValidateConfiguration(characterCatalog);
            if (characterFailures.Count > 0 || foundationFailures.Count > 0)
            {
                throw new ArgumentException(
                    string.Join(" | ", characterFailures.Concat(foundationFailures)));
            }

            instances = characterCatalog.Definitions
                .ToDictionary(
                    definition => definition.DefinitionId,
                    definition => new CharacterInstance(definition),
                    StringComparer.Ordinal);
            routeTraversalForward = instances.Keys.ToDictionary(
                characterId => characterId,
                _ => true,
                StringComparer.Ordinal);
            routeGeometry = foundation.Routes.ToDictionary(
                route => route.RouteId,
                route => new ResolvedRouteGeometry(foundation, route),
                StringComparer.Ordinal);
        }

        public IReadOnlyCollection<CharacterInstance> Instances =>
            instances.Values;

        public IReadOnlyList<NpcTraceEntry> Trace => trace.ToArray();

        public bool TryGetInstance(
            string characterDefinitionId,
            out CharacterInstance instance) =>
            instances.TryGetValue(
                characterDefinitionId ?? string.Empty,
                out instance);

        public void Evaluate(
            long dayIndex,
            double secondsOfDay,
            double elapsedGameSeconds)
        {
            if (!double.IsFinite(secondsOfDay) ||
                secondsOfDay < 0d || secondsOfDay >= 86400d ||
                !double.IsFinite(elapsedGameSeconds) ||
                elapsedGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(secondsOfDay));
            }

            foreach (CharacterInstance instance in instances.Values)
            {
                NpcScheduleBlock block = foundation
                    .GetSchedule(instance.Definition.DefinitionId)
                    .FirstOrDefault(candidate =>
                        candidate.IsActive(dayIndex, secondsOfDay));
                if (block == null)
                {
                    routeTraversalForward[instance.Definition.DefinitionId] =
                        true;
                    ApplyIfChanged(
                        instance,
                        string.Empty,
                        instance.Definition.HomeAnchorId,
                        string.Empty,
                        0d,
                        CharacterActivityState.Hidden,
                        elapsedGameSeconds);
                    continue;
                }

                bool physicalRouteDriver = physicalRouteDrivers.Contains(
                    instance.Definition.DefinitionId);
                string effectiveRouteId = block.RouteId;
                if (physicalRouteDriver &&
                    string.IsNullOrEmpty(effectiveRouteId) &&
                    !string.IsNullOrEmpty(instance.CurrentRouteId) &&
                    block.ActivityState == CharacterActivityState.VehicleSeated)
                {
                    // Story traffic owns its active race/dancehall route. The
                    // donor schedule block only selects availability and has
                    // an intentionally blank RouteId; applying that blank on
                    // every clock event reset Jani and Petteri to progress 0.
                    effectiveRouteId = instance.CurrentRouteId;
                }

                double progress = 0d;
                bool isRouteForward = true;
                if (!string.IsNullOrEmpty(effectiveRouteId))
                {
                    if (!foundation.TryGetRoute(
                            effectiveRouteId,
                            out NpcRouteDefinition route))
                    {
                        throw new InvalidOperationException(
                            $"Schedule '{block.ScheduleBlockId}' lost route '{effectiveRouteId}'.");
                    }

                    bool sameRoute = string.Equals(
                        instance.CurrentRouteId,
                        effectiveRouteId,
                        StringComparison.Ordinal);
                    bool preservePhysicalProgress =
                        physicalRouteAuthorities.Contains(
                            instance.Definition.DefinitionId) &&
                        sameRoute;
                    if (preservePhysicalProgress)
                    {
                        progress = instance.RouteProgress01;
                        isRouteForward =
                            !routeTraversalForward.TryGetValue(
                                instance.Definition.DefinitionId,
                                out bool retainedDirection) ||
                            retainedDirection;
                    }
                    else if (physicalRouteDriver)
                    {
                        if (!sameRoute)
                        {
                            // A newly activated physical trip starts at its
                            // authored formation. The clock selects the
                            // scenario but must not derive the first loaded
                            // chassis position from time since 16:00.
                            progress = 0d;
                            isRouteForward = true;
                        }
                        else
                        {
                            // Donor Amikset are disabled outside the player's
                            // 500 m residency bubble. Disable preserves both
                            // transform and waypoint; clock/time skips never
                            // integrate an off-screen route distance. The car
                            // resumes physically from its last retained pose.
                            progress = instance.RouteProgress01;
                            isRouteForward =
                                !routeTraversalForward.TryGetValue(
                                    instance.Definition.DefinitionId,
                                    out bool retainedDirection) ||
                                retainedDirection;
                        }
                    }
                    else
                    {
                        NpcRouteTraversalSample traversal =
                            route.ResolveTraversal(
                                block.SecondsSinceStart(secondsOfDay));
                        progress = traversal.Progress01;
                        isRouteForward = traversal.IsForward;
                    }
                }

                routeTraversalForward[instance.Definition.DefinitionId] =
                    isRouteForward;

                ApplyIfChanged(
                    instance,
                    block.ScheduleBlockId,
                    block.AnchorId,
                    effectiveRouteId,
                    progress,
                    block.ActivityState,
                    elapsedGameSeconds);
                if (physicalRouteDrivers.Contains(
                        instance.Definition.DefinitionId))
                {
                    physicalRouteClocks[instance.Definition.DefinitionId] =
                        elapsedGameSeconds;
                }
            }
        }

        public void RegisterPhysicalRouteDriver(
            string characterDefinitionId,
            double elapsedGameSeconds)
        {
            ValidatePhysicalRouteDriverArguments(
                characterDefinitionId,
                elapsedGameSeconds);
            physicalRouteDrivers.Add(characterDefinitionId);
            physicalRouteClocks[characterDefinitionId] = elapsedGameSeconds;
        }

        public void SynchronizePhysicalRouteDriverClocks(
            double elapsedGameSeconds)
        {
            if (!double.IsFinite(elapsedGameSeconds) ||
                elapsedGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedGameSeconds));
            }

            foreach (string characterId in physicalRouteDrivers)
            {
                physicalRouteClocks[characterId] = elapsedGameSeconds;
            }
        }

        public bool TryResolvePose(CharacterInstance instance, out NpcPose pose)
        {
            if (instance == null ||
                !foundation.TryGetAnchor(
                    instance.CurrentAnchorId,
                    out NpcAnchorDefinition anchor))
            {
                pose = default;
                return false;
            }

            if (string.IsNullOrEmpty(instance.CurrentRouteId))
            {
                pose = new NpcPose(
                    anchor.Position,
                    anchor.Rotation,
                    anchor.CellId,
                    instance.ActivityState == CharacterActivityState.Walking);
                return true;
            }

            if (!foundation.TryGetRoute(
                    instance.CurrentRouteId,
                    out NpcRouteDefinition route))
            {
                pose = default;
                return false;
            }

            if (!routeGeometry.TryGetValue(
                    route.RouteId,
                    out ResolvedRouteGeometry geometry))
            {
                pose = default;
                return false;
            }

            bool isForward = !routeTraversalForward.TryGetValue(
                                 instance.Definition.DefinitionId,
                                 out bool storedDirection) ||
                             storedDirection;
            if (!geometry.TryResolve(
                    (float)instance.RouteProgress01,
                    isForward,
                    out Vector3 position,
                    out Vector3 routeDirection,
                    out string cellId))
            {
                pose = default;
                return false;
            }

            routeDirection.y = 0f;
            Quaternion rotation = routeDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(routeDirection.normalized, Vector3.up)
                : anchor.Rotation;
            pose = new NpcPose(
                position,
                rotation,
                cellId,
                (instance.ActivityState == CharacterActivityState.Walking &&
                 route.SurfaceMode == NpcRouteSurfaceMode.TerrainConformed) ||
                geometry.HasVehicleBinding);
            return true;
        }

        public void SetPhysicalRouteAuthority(
            string characterDefinitionId,
            bool authoritative)
        {
            SetPhysicalRouteAuthority(
                characterDefinitionId,
                authoritative,
                null);
        }

        public void SetPhysicalRouteAuthority(
            string characterDefinitionId,
            bool authoritative,
            double elapsedGameSeconds)
        {
            SetPhysicalRouteAuthority(
                characterDefinitionId,
                authoritative,
                (double?)elapsedGameSeconds);
        }

        private void SetPhysicalRouteAuthority(
            string characterDefinitionId,
            bool authoritative,
            double? elapsedGameSeconds)
        {
            if (!instances.ContainsKey(characterDefinitionId ?? string.Empty))
            {
                throw new ArgumentException(
                    "Physical route authority references an unknown character.",
                    nameof(characterDefinitionId));
            }

            if (elapsedGameSeconds.HasValue &&
                (!double.IsFinite(elapsedGameSeconds.Value) ||
                 elapsedGameSeconds.Value < 0d))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedGameSeconds));
            }

            if (elapsedGameSeconds.HasValue &&
                physicalRouteDrivers.Contains(characterDefinitionId))
            {
                physicalRouteClocks[characterDefinitionId] =
                    elapsedGameSeconds.Value;
            }

            if (authoritative)
            {
                physicalRouteAuthorities.Add(characterDefinitionId);
                return;
            }

            physicalRouteAuthorities.Remove(characterDefinitionId);
            physicalRouteSampleHints.Remove(characterDefinitionId);
            physicalRouteSampleRoutes.Remove(characterDefinitionId);
        }

        private void ValidatePhysicalRouteDriverArguments(
            string characterDefinitionId,
            double elapsedGameSeconds)
        {
            if (!instances.ContainsKey(characterDefinitionId ?? string.Empty))
            {
                throw new ArgumentException(
                    "Physical route driver references an unknown character.",
                    nameof(characterDefinitionId));
            }

            if (!double.IsFinite(elapsedGameSeconds) ||
                elapsedGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedGameSeconds));
            }
        }

        public bool TryUpdatePhysicalRoute(
            CharacterInstance instance,
            Vector3 physicalPosition,
            float lookAheadMeters,
            out NpcPose guidancePose,
            out float physicalProgress01) =>
            TryUpdatePhysicalRoute(
                instance,
                physicalPosition,
                Vector3.zero,
                lookAheadMeters,
                out guidancePose,
                out physicalProgress01,
                out _);

        public bool TryUpdatePhysicalRoute(
            CharacterInstance instance,
            Vector3 physicalPosition,
            Vector3 physicalForward,
            float lookAheadMeters,
            out NpcPose guidancePose,
            out float physicalProgress01) =>
            TryUpdatePhysicalRoute(
                instance,
                physicalPosition,
                physicalForward,
                lookAheadMeters,
                out guidancePose,
                out physicalProgress01,
                out _);

        public bool TryUpdatePhysicalRoute(
            CharacterInstance instance,
            Vector3 physicalPosition,
            Vector3 physicalForward,
            float lookAheadMeters,
            out NpcPose guidancePose,
            out float physicalProgress01,
            out float routeDeviationMeters)
        {
            guidancePose = default;
            physicalProgress01 = 0f;
            routeDeviationMeters = float.PositiveInfinity;
            if (instance == null ||
                !physicalRouteAuthorities.Contains(
                    instance.Definition.DefinitionId) ||
                string.IsNullOrEmpty(instance.CurrentRouteId) ||
                !routeGeometry.TryGetValue(
                    instance.CurrentRouteId,
                    out ResolvedRouteGeometry geometry))
            {
                return false;
            }

            bool isForward = !routeTraversalForward.TryGetValue(
                                 instance.Definition.DefinitionId,
                                 out bool storedDirection) ||
                             storedDirection;
            string characterId = instance.Definition.DefinitionId;
            bool sameRoute = physicalRouteSampleRoutes.TryGetValue(
                                 characterId,
                                 out string retainedRouteId) &&
                             string.Equals(
                                 retainedRouteId,
                                 instance.CurrentRouteId,
                                 StringComparison.Ordinal);
            int sampleHint = sameRoute &&
                             physicalRouteSampleHints.TryGetValue(
                                 characterId,
                                 out int retainedHint)
                ? retainedHint
                : -1;
            int retainedSampleHint = sampleHint;
            if (!geometry.TryProjectAndResolveAhead(
                    physicalPosition,
                    physicalForward,
                    (float)instance.RouteProgress01,
                    isForward,
                    Mathf.Max(2f, lookAheadMeters),
                     ref sampleHint,
                     out float physicalProgress,
                     out routeDeviationMeters,
                    out Vector3 targetPosition,
                    out Vector3 targetDirection,
                    out string targetCellId))
            {
                return false;
            }

            physicalRouteSampleRoutes[characterId] = instance.CurrentRouteId;
            bool projectionIsOnRoad = routeDeviationMeters <=
                                      PhysicalRouteCommitCorridorMeters;
            bool nonRegressive = IsNonRegressivePhysicalProgress(
                (float)instance.RouteProgress01,
                physicalProgress,
                isForward,
                geometry.ClosesLoop);
            if (projectionIsOnRoad && nonRegressive)
            {
                physicalRouteSampleHints[characterId] = sampleHint;
                float committedProgress = ResolveCommittedPhysicalProgress(
                    (float)instance.RouteProgress01,
                    physicalProgress,
                    isForward,
                    geometry.ClosesLoop);
                instance.ApplyPhysicalRouteProgress(committedProgress);
                physicalProgress01 = committedProgress;
            }
            else
            {
                physicalProgress01 = (float)instance.RouteProgress01;
                if (nonRegressive)
                {
                    // A car which has left the pavement may still advance its
                    // local projection hint. This lets guidance follow the
                    // nearby forward road while progress remains frozen until
                    // the chassis is back within the accepted 5 m corridor.
                    physicalRouteSampleHints[characterId] = sampleHint;
                }
                else
                {
                    // Never walk the retained hint backwards after a spin or
                    // reverse recovery. Repeated/overlapping Trackfield laps
                    // make a regressive nearest segment especially dangerous:
                    // the next frame would continue searching the old lap and
                    // the car would chase a target it has already passed.
                    if (retainedSampleHint >= 0)
                    {
                        physicalRouteSampleHints[characterId] =
                            retainedSampleHint;
                    }

                    if (!geometry.TryResolveAheadFromProgress(
                            physicalProgress01,
                            isForward,
                            Mathf.Clamp(lookAheadMeters, 2f, 8f),
                            out targetPosition,
                            out targetDirection,
                            out targetCellId))
                    {
                        return false;
                    }
                }
            }
            targetDirection.y = 0f;
            Quaternion targetRotation =
                targetDirection.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(
                        targetDirection.normalized,
                        Vector3.up)
                    : Quaternion.identity;
            guidancePose = new NpcPose(
                targetPosition,
                targetRotation,
                targetCellId,
                shouldConformToGround: true);
            return true;
        }

        private static bool IsNonRegressivePhysicalProgress(
            float retained,
            float projected,
            bool forward,
            bool closesLoop)
        {
            const float tolerance = 0.001f;
            if (IsPhysicalRouteLoopWrap(
                    retained,
                    projected,
                    forward,
                    closesLoop))
            {
                return true;
            }

            return forward
                ? projected + tolerance >= retained
                : projected - tolerance <= retained;
        }

        public bool TryResolvePhysicalRoutePreloadPose(
            CharacterInstance instance,
            float lookAheadMeters,
            out NpcPose preloadPose)
        {
            preloadPose = default;
            if (instance == null ||
                !float.IsFinite(lookAheadMeters) ||
                lookAheadMeters <= 0f ||
                string.IsNullOrEmpty(instance.CurrentRouteId) ||
                !routeGeometry.TryGetValue(
                    instance.CurrentRouteId,
                    out ResolvedRouteGeometry geometry))
            {
                return false;
            }

            bool isForward = !routeTraversalForward.TryGetValue(
                                 instance.Definition.DefinitionId,
                                 out bool storedDirection) ||
                             storedDirection;
            if (!geometry.TryResolveAheadFromProgress(
                    (float)instance.RouteProgress01,
                    isForward,
                    lookAheadMeters,
                    out Vector3 position,
                    out Vector3 direction,
                    out string cellId))
            {
                return false;
            }

            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            preloadPose = new NpcPose(
                position,
                rotation,
                cellId,
                shouldConformToGround: true);
            return true;
        }

        private static float ResolveCommittedPhysicalProgress(
            float retained,
            float projected,
            bool forward,
            bool closesLoop)
        {
            if (IsPhysicalRouteLoopWrap(
                    retained,
                    projected,
                    forward,
                    closesLoop))
            {
                return projected;
            }

            // Projection tolerance suppresses segment-boundary jitter, but it
            // must never be committed as backwards simulation progress.
            return forward
                ? Mathf.Max(retained, projected)
                : Mathf.Min(retained, projected);
        }

        private static bool IsPhysicalRouteLoopWrap(
            float retained,
            float projected,
            bool forward,
            bool closesLoop) =>
            closesLoop &&
            (forward && retained >= 0.9f && projected <= 0.1f ||
             !forward && retained <= 0.1f && projected >= 0.9f);

        public NpcStateDto CaptureDto() =>
            NpcStateDto.FromInstances(instances.Values);

        public bool TryRestoreDto(NpcStateDto dto, out string failure)
        {
            if (!TryValidateDto(dto, out failure))
            {
                return false;
            }

            Dictionary<string, CharacterInstanceSnapshot> byDefinition =
                dto.characters.ToDictionary(
                    snapshot => snapshot.definitionId,
                    StringComparer.Ordinal);

            NpcStateDto checkpoint = CaptureDto();
            foreach (KeyValuePair<string, CharacterInstance> pair in instances)
            {
                if (!pair.Value.TryRestoreSnapshot(
                        byDefinition[pair.Key],
                        out failure))
                {
                    RestoreUnchecked(checkpoint);
                    return false;
                }
            }

            AddTrace(0d, string.Empty, "save.restore", "NPC logical state restored before presentation reconciliation.");
            failure = string.Empty;
            return true;
        }

        public bool TryValidateDto(NpcStateDto dto, out string failure)
        {
            if (dto == null)
            {
                failure = "NPC-state payload is null.";
                return false;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            Dictionary<string, CharacterInstanceSnapshot> byDefinition =
                dto.characters.ToDictionary(
                    snapshot => snapshot.definitionId,
                    StringComparer.Ordinal);
            if (byDefinition.Count != instances.Count ||
                instances.Keys.Any(id => !byDefinition.ContainsKey(id)))
            {
                failure =
                    "NPC save roster does not match the configured Phase 1 foundation catalog.";
                return false;
            }

            foreach (KeyValuePair<string, CharacterInstance> pair in instances)
            {
                if (!CharacterInstance.TryValidateSnapshot(
                        byDefinition[pair.Key],
                        pair.Value.Definition,
                        out failure))
                {
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private void RestoreUnchecked(NpcStateDto dto)
        {
            foreach (CharacterInstanceSnapshot snapshot in dto.characters)
            {
                instances[snapshot.definitionId].TryRestoreSnapshot(snapshot, out _);
            }
        }

        private void ApplyIfChanged(
            CharacterInstance instance,
            string blockId,
            string anchorId,
            string routeId,
            double progress,
            CharacterActivityState activity,
            double gameSeconds)
        {
            bool changed =
                !string.Equals(instance.ActiveScheduleBlockId, blockId, StringComparison.Ordinal) ||
                !string.Equals(instance.CurrentAnchorId, anchorId, StringComparison.Ordinal) ||
                !string.Equals(instance.CurrentRouteId, routeId, StringComparison.Ordinal) ||
                Math.Abs(instance.RouteProgress01 - progress) > 0.000001d ||
                instance.ActivityState != activity;
            instance.ApplyScheduleState(
                blockId,
                anchorId,
                routeId,
                progress,
                activity);
            if (changed)
            {
                AddTrace(
                    gameSeconds,
                    instance.Definition.DefinitionId,
                    "schedule.transition",
                    $"{blockId} -> {activity} at {anchorId}, route={routeId}, t={progress:F3}");
            }
        }

        private void AddTrace(
            double gameSeconds,
            string characterId,
            string eventId,
            string details)
        {
            while (trace.Count >= MaximumTraceEntries)
            {
                trace.Dequeue();
            }

            trace.Enqueue(new NpcTraceEntry(
                gameSeconds,
                characterId,
                eventId,
                details));
        }

        private sealed class ResolvedRouteGeometry
        {
            private const int CurvedSamplesPerSegment = 12;

            private readonly Vector3[] samples;
            private readonly float[] cumulativeDistances;
            private readonly float[] traversalProgress;
            private readonly int[] sampleSegmentIndices;
            private readonly Vector3[] controlPoints;
            private readonly string[] controlCellIds;
            private readonly bool closesLoop;
            private readonly int samplesPerSegment;
            private readonly NpcRouteInterpolationMode interpolationMode;
            // The fast path stays close to the retained route hint. If a car
            // runs wide, a bounded larger window reacquires the adjacent
            // forward road without performing a global nearest-route jump.
            // Even the recovery window is far shorter than one repeated
            // Trackfield lap, so it cannot silently skip to a later circuit.
            private const int PhysicalProjectionControlPointRadius = 4;
            private const int PhysicalRecoveryProjectionControlPointRadius =
                36;
            private const float ProjectionRecoveryThresholdMeters = 8f;
            // Recovery may search a wider spatial neighbourhood, but it must
            // remain on the retained directed branch. Teimo, inspection and
            // Trackfield all contain adjacent outbound/return geometry; a
            // distance-only wide search can otherwise select the other leg
            // and make the car chase a target behind or across a building.
            private const float RecoveryMaximumBackwardMeters = 35f;
            private const float RecoveryMaximumForwardMeters = 42f;
            private const float OffRoutePursuitThresholdMeters = 5f;
            private const float OffRoutePursuitLookAheadMeters = 8f;

            public ResolvedRouteGeometry(
                NpcFoundationCatalog catalog,
                NpcRouteDefinition route)
            {
                IReadOnlyList<string> waypointIds = route.WaypointAnchorIds;
                controlPoints = new Vector3[waypointIds.Count];
                controlCellIds = new string[waypointIds.Count];
                interpolationMode = route.InterpolationMode;
                HasVehicleBinding = false;
                for (int index = 0; index < waypointIds.Count; index++)
                {
                    if (!catalog.TryGetAnchor(
                            waypointIds[index],
                            out NpcAnchorDefinition control))
                    {
                        throw new InvalidOperationException(
                            $"NPC route '{route.RouteId}' lost waypoint " +
                            $"'{waypointIds[index]}'.");
                    }

                    controlPoints[index] = control.Position;
                    controlCellIds[index] = control.CellId;
                    HasVehicleBinding |=
                        !string.IsNullOrEmpty(control.VehicleBindingId);
                }

                closesLoop = route.TraversalMode == NpcRouteTraversalMode.Loop;
                int segmentCount = closesLoop
                    ? controlPoints.Length
                    : controlPoints.Length - 1;
                samplesPerSegment = route.InterpolationMode ==
                                    NpcRouteInterpolationMode.CatmullRom
                    ? CurvedSamplesPerSegment
                    : 1;
                samples = new Vector3[segmentCount * samplesPerSegment + 1];
                sampleSegmentIndices = new int[samples.Length];
                for (int sampleIndex = 0;
                     sampleIndex < samples.Length;
                     sampleIndex++)
                {
                    int segment = Mathf.Min(
                        sampleIndex / samplesPerSegment,
                        segmentCount - 1);
                    float segmentProgress = sampleIndex == samples.Length - 1
                        ? 1f
                        : sampleIndex % samplesPerSegment /
                          (float)samplesPerSegment;
                    samples[sampleIndex] = route.InterpolationMode ==
                                           NpcRouteInterpolationMode.CatmullRom
                        ? EvaluateCatmullRom(
                            controlPoints,
                            segment,
                            segmentProgress,
                            closesLoop)
                        : EvaluateLinear(
                            controlPoints,
                            segment,
                            segmentProgress,
                            closesLoop);
                    sampleSegmentIndices[sampleIndex] = segment;
                }

                cumulativeDistances = new float[samples.Length];
                for (int index = 1; index < samples.Length; index++)
                {
                    cumulativeDistances[index] =
                        cumulativeDistances[index - 1] +
                        Vector3.Distance(samples[index - 1], samples[index]);
                }

                traversalProgress = new float[samples.Length];
                IReadOnlyList<double> waypointProgress =
                    route.WaypointProgress01;
                if (waypointProgress.Count == controlPoints.Length)
                {
                    for (int sampleIndex = 0;
                         sampleIndex < samples.Length;
                         sampleIndex++)
                    {
                        int segment = sampleSegmentIndices[sampleIndex];
                        float segmentProgress =
                            sampleIndex == samples.Length - 1
                                ? 1f
                                : sampleIndex % samplesPerSegment /
                                  (float)samplesPerSegment;
                        int next = closesLoop
                            ? (segment + 1) % waypointProgress.Count
                            : segment + 1;
                        traversalProgress[sampleIndex] = Mathf.LerpUnclamped(
                            (float)waypointProgress[segment],
                            next == 0
                                ? 1f
                                : (float)waypointProgress[next],
                            segmentProgress);
                    }
                }
                else
                {
                    float totalDistance = cumulativeDistances[
                        cumulativeDistances.Length - 1];
                    for (int index = 1; index < samples.Length; index++)
                    {
                        traversalProgress[index] = totalDistance > 0.0001f
                            ? cumulativeDistances[index] / totalDistance
                            : 1f;
                    }
                }
            }

            public bool HasVehicleBinding { get; }

            public bool TryResolve(
                float progress01,
                bool isForward,
                out Vector3 position,
                out Vector3 direction,
                out string cellId)
            {
                float totalDistance =
                    cumulativeDistances[cumulativeDistances.Length - 1];
                if (totalDistance <= 0.0001f)
                {
                    position = default;
                    direction = default;
                    cellId = string.Empty;
                    return false;
                }

                float targetProgress = Mathf.Clamp01(progress01);
                int upper = Array.BinarySearch(
                    traversalProgress,
                    targetProgress);
                if (upper < 0)
                {
                    upper = ~upper;
                }

                upper = Mathf.Clamp(upper, 1, samples.Length - 1);
                int lower = upper - 1;
                float progressSpan = traversalProgress[upper] -
                                     traversalProgress[lower];
                float sampleProgress = progressSpan > 0.000001f
                    ? (targetProgress - traversalProgress[lower]) /
                      progressSpan
                    : 1f;
                int segment;
                float segmentProgress;
                if (sampleProgress >= 0.999999f)
                {
                    segment = sampleSegmentIndices[upper];
                    segmentProgress = upper == samples.Length - 1
                        ? 1f
                        : (upper - segment * samplesPerSegment) /
                          (float)samplesPerSegment;
                }
                else
                {
                    segment = sampleSegmentIndices[lower];
                    segmentProgress =
                        (lower - segment * samplesPerSegment +
                         sampleProgress) / samplesPerSegment;
                }

                position = interpolationMode ==
                           NpcRouteInterpolationMode.CatmullRom
                    ? EvaluateCatmullRom(
                        controlPoints,
                        segment,
                        segmentProgress,
                        closesLoop)
                    : EvaluateLinear(
                        controlPoints,
                        segment,
                        segmentProgress,
                        closesLoop);
                direction = interpolationMode ==
                            NpcRouteInterpolationMode.CatmullRom
                    ? EvaluateCatmullRomTangent(
                        controlPoints,
                        segment,
                        segmentProgress,
                        closesLoop)
                    : samples[upper] - samples[lower];
                if (direction.sqrMagnitude <= 0.000001f)
                {
                    int before = Mathf.Max(0, lower - 1);
                    int after = Mathf.Min(samples.Length - 1, upper + 1);
                    direction = samples[after] - samples[before];
                }
                if (!isForward)
                {
                    direction = -direction;
                }

                int cellIndex = segment;
                if (!closesLoop && progress01 >= 0.999999f)
                {
                    cellIndex = controlCellIds.Length - 1;
                }

                cellId = controlCellIds[
                    Mathf.Clamp(cellIndex, 0, controlCellIds.Length - 1)];
                return true;
            }

            public bool TryProjectAndResolveAhead(
                Vector3 physicalPosition,
                Vector3 physicalForward,
                float previousProgress01,
                bool isForward,
                float lookAheadMeters,
                ref int sampleHint,
                out float physicalProgress01,
                out float projectionDistanceMeters,
                out Vector3 targetPosition,
                out Vector3 targetDirection,
                out string targetCellId)
            {
                int segmentCount = samples.Length - 1;
                if (segmentCount <= 0)
                {
                    physicalProgress01 = 0f;
                    projectionDistanceMeters = float.PositiveInfinity;
                    targetPosition = default;
                    targetDirection = default;
                    targetCellId = string.Empty;
                    return false;
                }

                if (sampleHint < 0 || sampleHint >= segmentCount)
                {
                    int progressIndex = Array.BinarySearch(
                        traversalProgress,
                        Mathf.Clamp01(previousProgress01));
                    sampleHint = Mathf.Clamp(
                        progressIndex >= 0 ? progressIndex : ~progressIndex,
                        0,
                        segmentCount - 1);
                }

                FindNearestProjection(
                    physicalPosition,
                    physicalForward,
                    isForward,
                    previousProgress01,
                    sampleHint,
                    PhysicalProjectionControlPointRadius *
                    samplesPerSegment,
                    constrainDirectedProgress: false,
                    out int nearestSegment,
                    out float segmentProgress,
                    out float nearestSqrDistance);
                if (nearestSqrDistance >
                    ProjectionRecoveryThresholdMeters *
                    ProjectionRecoveryThresholdMeters)
                {
                    FindNearestProjection(
                        physicalPosition,
                        physicalForward,
                        isForward,
                        previousProgress01,
                        sampleHint,
                        PhysicalRecoveryProjectionControlPointRadius *
                        samplesPerSegment,
                        constrainDirectedProgress: true,
                        out int recoverySegment,
                        out float recoveryProgress,
                        out float recoverySqrDistance);
                    if (recoverySqrDistance < nearestSqrDistance)
                    {
                        nearestSegment = recoverySegment;
                        segmentProgress = recoveryProgress;
                        nearestSqrDistance = recoverySqrDistance;
                    }
                }
                projectionDistanceMeters = Mathf.Sqrt(nearestSqrDistance);

                sampleHint = nearestSegment;
                physicalProgress01 = Mathf.LerpUnclamped(
                    traversalProgress[nearestSegment],
                    traversalProgress[nearestSegment + 1],
                    segmentProgress);
                float currentDistance = Mathf.LerpUnclamped(
                    cumulativeDistances[nearestSegment],
                    cumulativeDistances[nearestSegment + 1],
                    segmentProgress);
                float totalDistance =
                    cumulativeDistances[cumulativeDistances.Length - 1];
                float effectiveLookAheadMeters =
                    projectionDistanceMeters >
                    OffRoutePursuitThresholdMeters
                        ? Mathf.Min(
                            lookAheadMeters,
                            OffRoutePursuitLookAheadMeters)
                        : lookAheadMeters;
                float targetDistance = currentDistance +
                    (isForward
                        ? effectiveLookAheadMeters
                        : -effectiveLookAheadMeters);
                if (closesLoop)
                {
                    targetDistance %= totalDistance;
                    if (targetDistance < 0f)
                    {
                        targetDistance += totalDistance;
                    }
                }
                else
                {
                    targetDistance = Mathf.Clamp(
                        targetDistance,
                        0f,
                        totalDistance);
                }

                int upper = Array.BinarySearch(
                    cumulativeDistances,
                    targetDistance);
                if (upper < 0)
                {
                    upper = ~upper;
                }

                upper = Mathf.Clamp(upper, 1, samples.Length - 1);
                int lower = upper - 1;
                float distanceSpan = cumulativeDistances[upper] -
                                     cumulativeDistances[lower];
                float distanceProgress = distanceSpan > 0.000001f
                    ? (targetDistance - cumulativeDistances[lower]) /
                      distanceSpan
                    : 1f;
                float targetProgress = Mathf.LerpUnclamped(
                    traversalProgress[lower],
                    traversalProgress[upper],
                    distanceProgress);
                return TryResolve(
                    targetProgress,
                    isForward,
                    out targetPosition,
                    out targetDirection,
                    out targetCellId);
            }

            public bool TryResolveAheadFromProgress(
                float progress01,
                bool isForward,
                float lookAheadMeters,
                out Vector3 targetPosition,
                out Vector3 targetDirection,
                out string targetCellId)
            {
                float retainedProgress = Mathf.Clamp01(progress01);
                int upper = Array.BinarySearch(
                    traversalProgress,
                    retainedProgress);
                if (upper < 0)
                {
                    upper = ~upper;
                }

                upper = Mathf.Clamp(upper, 1, samples.Length - 1);
                int lower = upper - 1;
                float progressSpan = traversalProgress[upper] -
                                     traversalProgress[lower];
                float progressWithinSample = progressSpan > 0.000001f
                    ? (retainedProgress - traversalProgress[lower]) /
                      progressSpan
                    : 1f;
                float currentDistance = Mathf.LerpUnclamped(
                    cumulativeDistances[lower],
                    cumulativeDistances[upper],
                    progressWithinSample);
                float totalDistance =
                    cumulativeDistances[cumulativeDistances.Length - 1];
                float targetDistance = currentDistance +
                    (isForward ? lookAheadMeters : -lookAheadMeters);
                if (closesLoop)
                {
                    targetDistance %= totalDistance;
                    if (targetDistance < 0f)
                    {
                        targetDistance += totalDistance;
                    }
                }
                else
                {
                    targetDistance = Mathf.Clamp(
                        targetDistance,
                        0f,
                        totalDistance);
                }

                int targetUpper = Array.BinarySearch(
                    cumulativeDistances,
                    targetDistance);
                if (targetUpper < 0)
                {
                    targetUpper = ~targetUpper;
                }

                targetUpper = Mathf.Clamp(
                    targetUpper,
                    1,
                    samples.Length - 1);
                int targetLower = targetUpper - 1;
                float distanceSpan = cumulativeDistances[targetUpper] -
                                     cumulativeDistances[targetLower];
                float distanceProgress = distanceSpan > 0.000001f
                    ? (targetDistance - cumulativeDistances[targetLower]) /
                      distanceSpan
                    : 1f;
                float targetProgress = Mathf.LerpUnclamped(
                    traversalProgress[targetLower],
                    traversalProgress[targetUpper],
                    distanceProgress);
                return TryResolve(
                    targetProgress,
                    isForward,
                    out targetPosition,
                    out targetDirection,
                    out targetCellId);
            }

            public bool ClosesLoop => closesLoop;

            public bool CanUseBoundedRecoveryProjection(
                float retainedProgress01,
                float projectedProgress01,
                bool isForward,
                float maximumBackwardMeters)
            {
                if (!float.IsFinite(retainedProgress01) ||
                    !float.IsFinite(projectedProgress01) ||
                    !float.IsFinite(maximumBackwardMeters) ||
                    maximumBackwardMeters < 0f)
                {
                    return false;
                }

                float delta = projectedProgress01 - retainedProgress01;
                if (closesLoop)
                {
                    if (isForward && delta < -0.5f)
                    {
                        delta += 1f;
                    }
                    else if (!isForward && delta > 0.5f)
                    {
                        delta -= 1f;
                    }
                }

                float totalDistance =
                    cumulativeDistances[cumulativeDistances.Length - 1];
                float directedMeters = delta * totalDistance *
                                       (isForward ? 1f : -1f);
                return directedMeters >= -maximumBackwardMeters;
            }

            private void FindNearestProjection(
                Vector3 position,
                Vector3 physicalForward,
                bool isForward,
                float retainedProgress01,
                int sampleHint,
                int searchRadius,
                bool constrainDirectedProgress,
                out int nearestSegment,
                out float nearestProgress,
                out float nearestSqrDistance)
            {
                int segmentCount = samples.Length - 1;
                nearestSegment = Mathf.Clamp(
                    sampleHint,
                    0,
                    segmentCount - 1);
                nearestProgress = 0f;
                nearestSqrDistance = float.PositiveInfinity;
                float nearestScore = float.PositiveInfinity;
                Vector3 horizontalPhysicalForward = physicalForward;
                horizontalPhysicalForward.y = 0f;
                bool hasHeading =
                    horizontalPhysicalForward.sqrMagnitude > 0.25f;
                if (hasHeading)
                {
                    horizontalPhysicalForward.Normalize();
                }
                int retainedSegment = Mathf.Clamp(
                    sampleHint,
                    0,
                    segmentCount - 1);
                Vector3 retainedRouteForward =
                    samples[retainedSegment + 1] -
                    samples[retainedSegment];
                retainedRouteForward.y = 0f;
                if (!isForward)
                {
                    retainedRouteForward = -retainedRouteForward;
                }

                if (retainedRouteForward.sqrMagnitude > 0.0001f)
                {
                    retainedRouteForward.Normalize();
                }
                int count = Mathf.Min(
                    segmentCount,
                    searchRadius >= segmentCount
                        ? segmentCount
                        : searchRadius * 2 + 1);
                int start = searchRadius >= segmentCount
                    ? 0
                    : sampleHint - searchRadius;
                for (int offset = 0; offset < count; offset++)
                {
                    int segment = start + offset;
                    if (closesLoop)
                    {
                        segment %= segmentCount;
                        if (segment < 0)
                        {
                            segment += segmentCount;
                        }
                    }
                    else if (segment < 0 || segment >= segmentCount)
                    {
                        continue;
                    }

                    Vector3 a = samples[segment];
                    Vector3 delta = samples[segment + 1] - a;
                    Vector3 horizontalDelta = delta;
                    horizontalDelta.y = 0f;
                    Vector3 fromA = position - a;
                    fromA.y = 0f;
                    float denominator = horizontalDelta.sqrMagnitude;
                    float progress = denominator > 0.000001f
                        ? Mathf.Clamp01(
                            Vector3.Dot(fromA, horizontalDelta) / denominator)
                        : 0f;
                    if (constrainDirectedProgress)
                    {
                        float candidateProgress = Mathf.LerpUnclamped(
                            traversalProgress[segment],
                            traversalProgress[segment + 1],
                            progress);
                        float directedDistanceMeters =
                            ResolveDirectedProgressDistanceMeters(
                                retainedProgress01,
                                candidateProgress,
                                isForward);
                        if (directedDistanceMeters <
                                -RecoveryMaximumBackwardMeters ||
                            directedDistanceMeters >
                                RecoveryMaximumForwardMeters)
                        {
                            continue;
                        }
                    }

                    Vector3 horizontalError =
                        position - (a + delta * progress);
                    horizontalError.y = 0f;
                    float sqrDistance = horizontalError.sqrMagnitude;
                    Vector3 candidateDirection = horizontalDelta;
                    if (!isForward)
                    {
                        candidateDirection = -candidateDirection;
                    }

                    if (constrainDirectedProgress &&
                        retainedRouteForward.sqrMagnitude > 0.5f &&
                        candidateDirection.sqrMagnitude > 0.0001f &&
                        Vector3.Dot(
                            retainedRouteForward,
                            candidateDirection.normalized) < -0.25f)
                    {
                        // A stopped car has no trustworthy physical heading.
                        // Preserve the retained route tangent so an adjacent
                        // opposing leg cannot become the new authority merely
                        // because it is spatially closer.
                        continue;
                    }

                    float headingPenalty = 0f;
                    if (hasHeading &&
                        candidateDirection.sqrMagnitude > 0.0001f)
                    {
                        float alignment = Vector3.Dot(
                            horizontalPhysicalForward,
                            candidateDirection.normalized);
                        // Teimo and inspection contain adjacent outbound and
                        // return legs. Distance-only projection selected the
                        // opposite branch and made cars turn early. Preserve
                        // the retained branch by making an opposed candidate
                        // pay roughly ten metres of projection cost.
                        headingPenalty = Mathf.Max(0f, 1f - alignment) * 50f;
                    }

                    float score = sqrDistance + headingPenalty;
                    if (score >= nearestScore)
                    {
                        continue;
                    }

                    nearestScore = score;
                    nearestSegment = segment;
                    nearestProgress = progress;
                    nearestSqrDistance = sqrDistance;
                }
            }

            private float ResolveDirectedProgressDistanceMeters(
                float retainedProgress01,
                float candidateProgress01,
                bool isForward)
            {
                float delta = candidateProgress01 - retainedProgress01;
                if (closesLoop)
                {
                    if (isForward && delta < -0.5f)
                    {
                        delta += 1f;
                    }
                    else if (!isForward && delta > 0.5f)
                    {
                        delta -= 1f;
                    }
                }

                float totalDistance =
                    cumulativeDistances[cumulativeDistances.Length - 1];
                return delta * totalDistance * (isForward ? 1f : -1f);
            }

            private static Vector3 EvaluateLinear(
                IReadOnlyList<Vector3> points,
                int segment,
                float progress,
                bool loop)
            {
                int next = loop
                    ? (segment + 1) % points.Count
                    : segment + 1;
                return Vector3.LerpUnclamped(
                    points[segment],
                    points[next],
                    progress);
            }

            // Directly adapted from the locked donor SWS WaypointManager
            // Catmull-Rom calculation. Route ownership and runtime state remain
            // project-owned; no donor movement component is used.
            private static Vector3 EvaluateCatmullRom(
                IReadOnlyList<Vector3> points,
                int segment,
                float progress,
                bool loop)
            {
                int count = points.Count;
                int p1 = segment;
                int p2 = loop ? (segment + 1) % count : segment + 1;
                int p0 = loop
                    ? (segment - 1 + count) % count
                    : segment == 0 ? 0 : segment - 1;
                int p3 = loop
                    ? (segment + 2) % count
                    : segment + 2 < count ? segment + 2 : p2;
                Vector3 a = points[p0];
                Vector3 b = points[p1];
                Vector3 c = points[p2];
                Vector3 d = points[p3];
                float squared = progress * progress;
                float cubed = squared * progress;
                return 0.5f *
                       ((-a + 3f * b - 3f * c + d) * cubed +
                        (2f * a - 5f * b + 4f * c - d) * squared +
                        (-a + c) * progress + 2f * b);
            }

            private static Vector3 EvaluateCatmullRomTangent(
                IReadOnlyList<Vector3> points,
                int segment,
                float progress,
                bool loop)
            {
                int count = points.Count;
                int p1 = segment;
                int p2 = loop ? (segment + 1) % count : segment + 1;
                int p0 = loop
                    ? (segment - 1 + count) % count
                    : segment == 0 ? 0 : segment - 1;
                int p3 = loop
                    ? (segment + 2) % count
                    : segment + 2 < count ? segment + 2 : p2;
                Vector3 a = points[p0];
                Vector3 b = points[p1];
                Vector3 c = points[p2];
                Vector3 d = points[p3];
                float squared = progress * progress;
                return 0.5f *
                       (3f * (-a + 3f * b - 3f * c + d) * squared +
                        2f * (2f * a - 5f * b + 4f * c - d) *
                        progress +
                        (-a + c));
            }
        }
    }
}
