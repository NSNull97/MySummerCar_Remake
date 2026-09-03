using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using MSC.Core.Time;
using MSC.NPC;
using UnityEngine;

namespace MSC.Traffic
{
    public sealed partial class TrafficWorldRuntime
    {
        private const string RallyGroupId = "traffic.event.group.rally";
        private const string DragGroupId = "traffic.event.group.drag";
        private const string PoliceGroupId = "traffic.event.group.police";

        private static readonly Vector3 RallySaturdayRoot =
            new(-1258.81f, -0.37f, 1281.19f);
        private static readonly Vector3 RallySundayRoot =
            new(-1282.29f, -1.77f, -691.25f);
        private static readonly Vector3 DragRoot =
            new(-735.0635f, 2.705f, -900.5302f);
        private static readonly Vector3[] PoliceSiteRoots =
        {
            new(339.7f, -1.1f, -1413.1f),
            new(-169.61f, 0.25f, 603.31f),
            new(-134f, 0f, 182f),
        };

        private static readonly Vector3[,] PoliceCarAnchors =
        {
            {
                new Vector3(345.7076f, -0.857f, -1418.6848f),
                new Vector3(367.9433f, -0.905f, -1405.8883f),
            },
            {
                new Vector3(-1611.9026f, -0.55f, 366.4197f),
                new Vector3(-1599.4359f, -0.47f, 405.3204f),
            },
            {
                new Vector3(-24.6243f, 9.85f, 1606.7385f),
                new Vector3(-41.9863f, 9.16f, 1594.7474f),
            },
        };

        private readonly Dictionary<string, EventActorRuntimeState>
            eventStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            StoryTrafficVehiclePresentationBinding> eventPresentations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, EventGroupRuntimeState>
            eventGroups = new(StringComparer.Ordinal);

        public int EventActorCount => eventStates.Count;
        public int MaterializedEventActorCount => eventPresentations.Count;

        private void InitializeEventStates(GameTimeSnapshot snapshot)
        {
            eventStates.Clear();
            foreach (TrafficEventActorDefinition definition in
                     catalog.EventActors)
            {
                eventStates.Add(
                    definition.ActorId,
                    EventActorRuntimeState.CreateInitial(definition));
            }

            eventGroups.Clear();
            eventGroups.Add(RallyGroupId, new EventGroupRuntimeState(RallyGroupId));
            eventGroups.Add(DragGroupId, new EventGroupRuntimeState(DragGroupId));
            eventGroups.Add(
                PoliceGroupId,
                new EventGroupRuntimeState(PoliceGroupId)
                {
                    RealSecondsRemaining = TrafficEventBehaviorRules
                        .PoliceInitialEvaluationDelayRealSeconds,
                });
            ResetEventDay(snapshot.DayIndex);
        }

        private void UpdateEventTraffic(float realDeltaSeconds)
        {
            if (catalog.EventActors.Count == 0)
            {
                return;
            }

            GameTimeSnapshot snapshot = gameTime.Snapshot;
            DayOfWeek day = new DateTime(
                    snapshot.Date.Year,
                    snapshot.Date.Month,
                    snapshot.Date.Day)
                .DayOfWeek;
            int hour = Mathf.Clamp(
                Mathf.FloorToInt((float)(snapshot.SecondsOfDay / 3600d)),
                0,
                23);
            UpdateRallyGroup(snapshot, day, hour, realDeltaSeconds);
            UpdateDragGroup(snapshot, day, hour, realDeltaSeconds);
            UpdatePoliceGroup(snapshot, day, realDeltaSeconds);
            ReconcileEventPresentations();
        }

        private void UpdateRallyGroup(
            GameTimeSnapshot snapshot,
            DayOfWeek day,
            int hour,
            float deltaSeconds)
        {
            EventGroupRuntimeState group = eventGroups[RallyGroupId];
            bool schedule = TrafficEventBehaviorRules.IsRallyOpen(day, hour);
            if (group.EvaluatedDayIndex != snapshot.DayIndex)
            {
                group.EvaluatedDayIndex = snapshot.DayIndex;
                group.SequenceOrdinal = 0;
                group.RealSecondsRemaining = 0f;
                ResetActors(TrafficEventKind.OfficialRally, snapshot.DayIndex);
            }

            group.ScheduleActive = schedule;
            Vector3 root = day == DayOfWeek.Sunday
                ? RallySundayRoot
                : RallySaturdayRoot;
            group.Resident = UpdateResidency(
                group.Resident,
                root,
                materializeDistance: 200f,
                dematerializeDistance: 400f,
                schedule);
            SetKindResidency(TrafficEventKind.OfficialRally, group.Resident);
            if (!schedule)
            {
                SetKindPhase(TrafficEventKind.OfficialRally,
                    TrafficEventActorPhase.Inactive);
                return;
            }

            foreach (EventActorRuntimeState state in StatesOfKind(
                         TrafficEventKind.OfficialRally))
            {
                if (state.Phase == TrafficEventActorPhase.Inactive)
                {
                    state.Phase = TrafficEventActorPhase.Staged;
                }
            }

            if (!group.Resident)
            {
                return;
            }

            group.RealSecondsRemaining = Mathf.Max(
                0f,
                group.RealSecondsRemaining - deltaSeconds);
            if (group.RealSecondsRemaining > 0f)
            {
                return;
            }

            EventActorRuntimeState[] cars = StatesOfKind(
                    TrafficEventKind.OfficialRally)
                .OrderBy(value => value.Definition.ActorId,
                    StringComparer.Ordinal)
                .ToArray();
            int dayOffset = Mathf.FloorToInt(
                TrafficEventBehaviorRules.Deterministic01(
                    61205,
                    0,
                    snapshot.DayIndex) * cars.Length) % cars.Length;
            EventActorRuntimeState selected = cars[
                (group.SequenceOrdinal + dayOffset) % cars.Length];
            StartRouteRun(selected, snapshot, alternate: day == DayOfWeek.Sunday);
            group.SequenceOrdinal++;
            group.RealSecondsRemaining = TrafficEventBehaviorRules
                .RallyDispatchIntervalRealSeconds;
        }

        private void UpdateDragGroup(
            GameTimeSnapshot snapshot,
            DayOfWeek day,
            int hour,
            float deltaSeconds)
        {
            EventGroupRuntimeState group = eventGroups[DragGroupId];
            bool schedule = TrafficEventBehaviorRules.IsDragOpen(day, hour);
            bool newDay = group.EvaluatedDayIndex != snapshot.DayIndex;
            if (newDay)
            {
                group.EvaluatedDayIndex = snapshot.DayIndex;
                group.SequenceOrdinal = 0;
                ResetActors(TrafficEventKind.DragRace, snapshot.DayIndex);
            }

            group.ScheduleActive = schedule;
            group.Resident = UpdateResidency(
                group.Resident,
                DragRoot,
                materializeDistance: 720f,
                dematerializeDistance: 800f,
                schedule);
            SetKindResidency(TrafficEventKind.DragRace, group.Resident);
            if (!schedule)
            {
                SetKindPhase(TrafficEventKind.DragRace,
                    TrafficEventActorPhase.Inactive);
                return;
            }

            EventActorRuntimeState[] cars = StatesOfKind(
                    TrafficEventKind.DragRace).ToArray();
            if (group.Resident && cars.All(value =>
                    value.Phase == TrafficEventActorPhase.Inactive ||
                    value.Phase == TrafficEventActorPhase.Staged))
            {
                foreach (EventActorRuntimeState car in cars)
                {
                    car.Phase = TrafficEventActorPhase.Crawl;
                    car.PhaseRealSecondsRemaining = 1.5f;
                    car.EventDayIndex = snapshot.DayIndex;
                }
            }

            if (!group.Resident)
            {
                return;
            }

            foreach (EventActorRuntimeState car in cars)
            {
                AdvanceDragStage(car, snapshot, deltaSeconds);
            }
        }

        private static void AdvanceDragStage(
            EventActorRuntimeState state,
            GameTimeSnapshot snapshot,
            float deltaSeconds)
        {
            if (state.Phase is TrafficEventActorPhase.Running or
                TrafficEventActorPhase.Finished or
                TrafficEventActorPhase.Inactive or
                TrafficEventActorPhase.Staged)
            {
                return;
            }

            state.PhaseRealSecondsRemaining = Mathf.Max(
                0f,
                state.PhaseRealSecondsRemaining - deltaSeconds);
            if (state.PhaseRealSecondsRemaining > 0f)
            {
                return;
            }

            switch (state.Phase)
            {
                case TrafficEventActorPhase.Crawl:
                    state.Phase = TrafficEventActorPhase.Burnout;
                    state.PhaseRealSecondsRemaining = 3f;
                    break;
                case TrafficEventActorPhase.Burnout:
                    state.Phase = TrafficEventActorPhase.Reverse;
                    state.PhaseRealSecondsRemaining = 1.5f;
                    break;
                case TrafficEventActorPhase.Reverse:
                    state.Phase = TrafficEventActorPhase.LineUp;
                    state.PhaseRealSecondsRemaining = 1f;
                    break;
                case TrafficEventActorPhase.LineUp:
                    state.Phase = TrafficEventActorPhase.RevUp;
                    state.PhaseRealSecondsRemaining = TrafficEventBehaviorRules
                        .ResolveDragLaunchDelayRealSeconds(
                            state.Definition.DeterministicSeed,
                            state.CompletedRuns,
                            snapshot.DayIndex);
                    break;
                case TrafficEventActorPhase.RevUp:
                    state.Phase = TrafficEventActorPhase.Running;
                    state.RouteProgress01 = state.StartProgress01;
                    state.SegmentHint = state.StartPointIndex;
                    break;
            }
        }

        private void UpdatePoliceGroup(
            GameTimeSnapshot snapshot,
            DayOfWeek day,
            float deltaSeconds)
        {
            EventGroupRuntimeState group = eventGroups[PoliceGroupId];
            if (group.EvaluatedDayIndex != snapshot.DayIndex)
            {
                group.EvaluatedDayIndex = snapshot.DayIndex;
                group.DailyChanceEvaluated = false;
                group.ScheduleActive = false;
                group.SelectedSiteIndex = -1;
                group.RealSecondsRemaining = TrafficEventBehaviorRules
                    .PoliceInitialEvaluationDelayRealSeconds;
                group.ChaseRequested = false;
                ResetActors(TrafficEventKind.PoliceCheckpoint,
                    snapshot.DayIndex);
            }

            if (!group.DailyChanceEvaluated)
            {
                group.RealSecondsRemaining = Mathf.Max(
                    0f,
                    group.RealSecondsRemaining - deltaSeconds);
                if (group.RealSecondsRemaining <= 0f)
                {
                    float chance = TrafficEventBehaviorRules
                        .PoliceActivationProbability(day);
                    group.ScheduleActive =
                        TrafficEventBehaviorRules.Deterministic01(
                            11890,
                            0,
                            snapshot.DayIndex) < chance;
                    group.SelectedSiteIndex = Mathf.Clamp(
                        Mathf.FloorToInt(
                            TrafficEventBehaviorRules.Deterministic01(
                                11890,
                                1,
                                snapshot.DayIndex) * 3f),
                        0,
                        2);
                    group.DailyChanceEvaluated = true;
                }
            }

            bool eligible = group.ScheduleActive && snapshot.IsDaylight &&
                            group.SelectedSiteIndex >= 0;
            Vector3 root = group.SelectedSiteIndex >= 0
                ? PoliceSiteRoots[group.SelectedSiteIndex]
                : Vector3.zero;
            group.Resident = eligible &&
                             Vector3.Distance(player.position, root) <= 1200f;
            SetKindResidency(TrafficEventKind.PoliceCheckpoint, group.Resident);
            foreach (EventActorRuntimeState car in StatesOfKind(
                         TrafficEventKind.PoliceCheckpoint))
            {
                if (!eligible)
                {
                    car.Phase = TrafficEventActorPhase.Inactive;
                }
                else if (group.ChaseRequested)
                {
                    if (car.Phase != TrafficEventActorPhase.Chase)
                    {
                        StartRouteRun(car, snapshot, alternate: false);
                        car.Phase = TrafficEventActorPhase.Chase;
                        car.DesiredSpeedMetersPerSecond = Mathf.Lerp(
                            car.Definition.ChaseMinimumSpeedMetersPerSecond,
                            car.Definition.ChaseMaximumSpeedMetersPerSecond,
                            TrafficEventBehaviorRules.Deterministic01(
                                car.Definition.DeterministicSeed,
                                car.CompletedRuns,
                                snapshot.DayIndex));
                    }
                }
                else
                {
                    car.Phase = TrafficEventActorPhase.Checkpoint;
                }
            }
        }

        public bool RequestPoliceChase()
        {
            if (!initialized || !eventGroups.TryGetValue(
                    PoliceGroupId,
                    out EventGroupRuntimeState group) ||
                !group.ScheduleActive || !group.Resident)
            {
                return false;
            }

            group.ChaseRequested = true;
            return true;
        }

        public void CancelPoliceChase()
        {
            if (eventGroups.TryGetValue(
                    PoliceGroupId,
                    out EventGroupRuntimeState group))
            {
                group.ChaseRequested = false;
            }
        }

        private void FixedUpdateEvents()
        {
            foreach (KeyValuePair<string,
                         StoryTrafficVehiclePresentationBinding> pair in
                     eventPresentations)
            {
                if (pair.Value == null ||
                    !eventStates.TryGetValue(pair.Key, out var state))
                {
                    continue;
                }

                bool driving = state.Phase == TrafficEventActorPhase.Running ||
                               state.Phase == TrafficEventActorPhase.Chase;
                pair.Value.SetServiceStopHold(!driving);
                if (!driving || !routes.TryGetValue(
                        state.Definition.RouteId,
                        out TrafficRouteGeometry geometry))
                {
                    continue;
                }

                int hint = state.SegmentHint;
                float lookAhead = Mathf.Clamp(
                    14f + pair.Value.CurrentSpeedMetersPerSecond * 0.45f,
                    14f,
                    state.Definition.Kind == TrafficEventKind.OfficialRally
                        ? 34f
                        : 30f);
                if (!geometry.TryProjectAndResolveAhead(
                        pair.Value.transform.position,
                        state.RouteProgress01,
                        state.TravelsForward,
                        lookAhead,
                        ref hint,
                        out TrafficRouteSample projection,
                        out TrafficRouteSample guidance))
                {
                    continue;
                }

                state.SegmentHint = hint;
                if (geometry.CanCommitPhysicalProjection(
                        state.RouteProgress01,
                        projection.Progress01,
                        state.TravelsForward))
                {
                    state.RouteProgress01 = projection.Progress01;
                }

                state.CurrentSpeedMetersPerSecond =
                    pair.Value.CurrentSpeedMetersPerSecond;
                pair.Value.SetPhysicalRouteGuidanceTarget(
                    guidance.Position,
                    guidance.Rotation,
                    state.RouteProgress01);
                if (state.Definition.Kind !=
                        TrafficEventKind.PoliceCheckpoint &&
                    HasReachedEventEnd(state, projection.SegmentIndex))
                {
                    state.Phase = TrafficEventActorPhase.Finished;
                    state.CompletedRuns++;
                    pair.Value.SetServiceStopHold(true);
                }
            }
        }

        private static bool HasReachedEventEnd(
            EventActorRuntimeState state,
            int projectedSegment) =>
            state.TravelsForward
                ? projectedSegment >= state.EndPointIndex
                : projectedSegment <= state.EndPointIndex;

        private void ReconcileEventPresentations()
        {
            var wanted = new HashSet<string>(
                eventStates.Values.Where(value => value.Resident &&
                    value.Phase != TrafficEventActorPhase.Inactive)
                    .Select(value => value.Definition.ActorId),
                StringComparer.Ordinal);
            foreach (string actorId in eventPresentations.Keys.ToArray())
            {
                if (!wanted.Contains(actorId))
                {
                    RemoveEventPresentation(actorId);
                }
            }

            foreach (string actorId in wanted)
            {
                if (!eventPresentations.ContainsKey(actorId))
                {
                    MaterializeEventPresentation(eventStates[actorId]);
                }
            }
        }

        private void MaterializeEventPresentation(EventActorRuntimeState state)
        {
            TrafficEventActorDefinition definition = state.Definition;
            if (!presentationCatalog.TryGet(
                    definition.PresentationId,
                    out TrafficPresentationCatalogEntry entry) ||
                entry.WrapperPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Traffic event presentation '{definition.PresentationId}' is unavailable.");
            }

            ResolveEventPose(state, out Vector3 position, out Quaternion rotation);
            GameObject wrapper = Instantiate(
                entry.WrapperPrefab,
                position,
                rotation,
                transform);
            wrapper.name = "Traffic_Event_Presentation_" + definition.ActorId;
            StoryTrafficVehiclePresentationBinding motion = wrapper
                .GetComponent<StoryTrafficVehiclePresentationBinding>();
            string failure = string.Empty;
            if (motion == null || !motion.TryValidate(out failure) ||
                !motion.HasPhysicalMotionBackend)
            {
                Destroy(wrapper);
                throw new InvalidOperationException(
                    $"Traffic event presentation '{definition.PresentationId}' is not physical: {failure}");
            }

            motion.ConfigureDrivingProfile(
                state.Phase == TrafficEventActorPhase.Chase
                    ? definition.ChaseMinimumSpeedMetersPerSecond
                    : definition.MinimumSpeedMetersPerSecond,
                state.Phase == TrafficEventActorPhase.Chase
                    ? definition.ChaseMaximumSpeedMetersPerSecond
                    : definition.MaximumSpeedMetersPerSecond,
                definition.Kind == TrafficEventKind.DragRace ? 0.4f : 4.8f,
                configuredBrakingMetersPerSecond2: 20f,
                configuredTurnRateDegreesPerSecond: 125f,
                configuredPassingLaneOffsetMeters:
                    Mathf.Approximately(definition.LaneOffsetMeters, -2f)
                        ? 2f
                        : -2f);
            float passingLane = Mathf.Approximately(
                definition.LaneOffsetMeters,
                -2f) ? 2f : -2f;
            motion.ConfigureRoadLanePolicy(
                definition.LaneOffsetMeters,
                passingLane,
                migrateLegacyCenterLane: true);
            motion.SnapToRoutePoseTarget(position, rotation);
            motion.SetServiceStopHold(
                state.Phase != TrafficEventActorPhase.Running &&
                state.Phase != TrafficEventActorPhase.Chase);
            eventPresentations.Add(definition.ActorId, motion);
        }

        private void ResolveEventPose(
            EventActorRuntimeState state,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (state.HasPhysicalPose)
            {
                position = state.WorldPosition;
                rotation = state.WorldRotation;
                return;
            }

            if (state.Phase == TrafficEventActorPhase.Running ||
                state.Phase == TrafficEventActorPhase.Chase)
            {
                TrafficRouteSample sample = routes[state.Definition.RouteId]
                    .Resolve(state.RouteProgress01, state.TravelsForward);
                position = sample.Position + sample.Rotation * Vector3.right *
                    state.Definition.LaneOffsetMeters;
                rotation = sample.Rotation;
                return;
            }

            position = state.Definition.StagingWorldPosition;
            rotation = state.Definition.StagingWorldRotation;
            if (state.Definition.Kind == TrafficEventKind.PoliceCheckpoint &&
                eventGroups[PoliceGroupId].SelectedSiteIndex >= 0)
            {
                int carIndex = state.Definition.ActorId.EndsWith(
                    "2",
                    StringComparison.Ordinal) ? 1 : 0;
                position = PoliceCarAnchors[
                    eventGroups[PoliceGroupId].SelectedSiteIndex,
                    carIndex];
            }
        }

        private void RemoveEventPresentation(string actorId)
        {
            if (!eventPresentations.TryGetValue(
                    actorId,
                    out StoryTrafficVehiclePresentationBinding motion))
            {
                return;
            }

            eventPresentations.Remove(actorId);
            if (motion != null && eventStates.TryGetValue(
                    actorId,
                    out EventActorRuntimeState state))
            {
                state.HasPhysicalPose = true;
                state.WorldPosition = motion.PhysicalWorldPosition;
                state.WorldRotation = motion.PhysicalWorldRotation;
                state.CurrentSpeedMetersPerSecond =
                    motion.CurrentSpeedMetersPerSecond;
                StoryTrafficMotionRuntimeState runtime =
                    motion.CaptureRuntimeState();
                if (runtime.HasPhysicalRouteProgress)
                {
                    state.RouteProgress01 =
                        runtime.LastPhysicalRouteProgress01;
                }
            }

            if (motion != null)
            {
                Destroy(motion.gameObject);
            }
        }

        private void RemoveAllEventPresentations()
        {
            foreach (string actorId in eventPresentations.Keys.ToArray())
            {
                RemoveEventPresentation(actorId);
            }
        }

        public TrafficEventActorStateDto[] CaptureEventActors()
        {
            foreach (string actorId in eventPresentations.Keys.ToArray())
            {
                StoryTrafficVehiclePresentationBinding motion =
                    eventPresentations[actorId];
                if (motion != null)
                {
                    EventActorRuntimeState state = eventStates[actorId];
                    state.HasPhysicalPose = true;
                    state.WorldPosition = motion.PhysicalWorldPosition;
                    state.WorldRotation = motion.PhysicalWorldRotation;
                    state.CurrentSpeedMetersPerSecond =
                        motion.CurrentSpeedMetersPerSecond;
                }
            }

            return catalog.EventActors.Select(value =>
                    eventStates[value.ActorId].ToDto())
                .ToArray();
        }

        public TrafficEventGroupStateDto[] CaptureEventGroups() =>
            eventGroups.Values.OrderBy(value => value.GroupId,
                    StringComparer.Ordinal)
                .Select(value => value.ToDto())
                .ToArray();

        public bool TryValidateEventState(
            IReadOnlyList<TrafficEventActorStateDto> actors,
            IReadOnlyList<TrafficEventGroupStateDto> groups,
            out string failure)
        {
            failure = string.Empty;
            if (!initialized)
            {
                failure = "Traffic runtime is not initialized.";
                return false;
            }

            IReadOnlyList<TrafficEventActorStateDto> actorSource = actors ??
                Array.Empty<TrafficEventActorStateDto>();
            IReadOnlyList<TrafficEventGroupStateDto> groupSource = groups ??
                Array.Empty<TrafficEventGroupStateDto>();
            if (actorSource.Count == 0 && groupSource.Count == 0)
            {
                return true;
            }

            if (actorSource.Count != catalog.EventActors.Count ||
                groupSource.Count != 3)
            {
                failure = "Traffic event save has the wrong actor/group count.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficEventActorStateDto dto in actorSource)
            {
                if (dto == null ||
                    !catalog.TryGetEventActor(dto.actorId, out var definition) ||
                    !ids.Add(dto.actorId) ||
                    !string.Equals(dto.stableInstanceId,
                        definition.StableInstanceId,
                        StringComparison.Ordinal) ||
                    dto.kind != (int)definition.Kind ||
                    dto.phase < 0 || dto.phase > 10 ||
                    !float.IsFinite(dto.routeProgress01) ||
                    dto.routeProgress01 < 0f || dto.routeProgress01 > 1f ||
                    !float.IsFinite(dto.desiredSpeedMetersPerSecond) ||
                    dto.desiredSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(dto.phaseRealSecondsRemaining) ||
                    dto.phaseRealSecondsRemaining < 0f)
                {
                    failure = $"Traffic event save actor '{dto?.actorId}' is invalid.";
                    return false;
                }
            }

            string[] expected = { RallyGroupId, DragGroupId, PoliceGroupId };
            if (groupSource.Any(value => value == null) ||
                !expected.All(id => groupSource.Count(value =>
                    string.Equals(value.groupId, id,
                        StringComparison.Ordinal)) == 1))
            {
                failure = "Traffic event save groups are invalid.";
                return false;
            }

            return true;
        }

        public bool TryRestoreEventState(
            IReadOnlyList<TrafficEventActorStateDto> actors,
            IReadOnlyList<TrafficEventGroupStateDto> groups,
            out string failure)
        {
            if (!TryValidateEventState(actors, groups, out failure))
            {
                return false;
            }

            RemoveAllEventPresentations();
            if (actors == null || actors.Count == 0)
            {
                InitializeEventStates(gameTime.Snapshot);
            }
            else
            {
                eventStates.Clear();
                foreach (TrafficEventActorStateDto dto in actors)
                {
                    eventStates.Add(
                        dto.actorId,
                        EventActorRuntimeState.FromDto(
                            catalog.EventActors.Single(value =>
                                value.ActorId == dto.actorId),
                            dto,
                            routes));
                }

                eventGroups.Clear();
                foreach (TrafficEventGroupStateDto dto in groups)
                {
                    eventGroups.Add(dto.groupId,
                        EventGroupRuntimeState.FromDto(dto));
                }
            }

            ReconcileEventPresentations();
            failure = string.Empty;
            return true;
        }

        private void StartRouteRun(
            EventActorRuntimeState state,
            GameTimeSnapshot snapshot,
            bool alternate)
        {
            TrafficRouteGeometry geometry = routes[state.Definition.RouteId];
            state.StartPointIndex = alternate
                ? state.Definition.AlternateStartPointIndex
                : state.Definition.PrimaryStartPointIndex;
            state.EndPointIndex = alternate
                ? state.Definition.AlternateEndPointIndex
                : state.Definition.PrimaryEndPointIndex;
            state.StartProgress01 = geometry.ProgressAtPointIndex(
                state.StartPointIndex);
            state.RouteProgress01 = state.StartProgress01;
            state.SegmentHint = state.StartPointIndex;
            state.TravelsForward = state.Definition.TravelsForward;
            state.DesiredSpeedMetersPerSecond = Mathf.Lerp(
                state.Definition.MinimumSpeedMetersPerSecond,
                state.Definition.MaximumSpeedMetersPerSecond,
                TrafficEventBehaviorRules.Deterministic01(
                    state.Definition.DeterministicSeed,
                    state.CompletedRuns,
                    snapshot.DayIndex));
            state.EventDayIndex = snapshot.DayIndex;
            state.HasPhysicalPose = false;
            state.Phase = TrafficEventActorPhase.Running;
            if (eventPresentations.TryGetValue(
                    state.Definition.ActorId,
                    out StoryTrafficVehiclePresentationBinding motion) &&
                motion != null)
            {
                TrafficRouteSample sample = geometry.Resolve(
                    state.RouteProgress01,
                    state.TravelsForward);
                Vector3 position = sample.Position +
                    sample.Rotation * Vector3.right *
                    state.Definition.LaneOffsetMeters;
                motion.SnapToRoutePoseTarget(position, sample.Rotation);
                motion.SetServiceStopHold(false);
            }
        }

        private bool UpdateResidency(
            bool currentlyResident,
            Vector3 root,
            float materializeDistance,
            float dematerializeDistance,
            bool eligible)
        {
            if (!eligible || player == null)
            {
                return false;
            }

            float distance = Vector3.Distance(player.position, root);
            return currentlyResident
                ? distance <= dematerializeDistance
                : distance <= materializeDistance;
        }

        private IEnumerable<EventActorRuntimeState> StatesOfKind(
            TrafficEventKind kind) =>
            eventStates.Values.Where(value => value.Definition.Kind == kind);

        private void SetKindResidency(TrafficEventKind kind, bool resident)
        {
            foreach (EventActorRuntimeState state in StatesOfKind(kind))
            {
                state.Resident = resident;
            }
        }

        private void SetKindPhase(
            TrafficEventKind kind,
            TrafficEventActorPhase phase)
        {
            foreach (EventActorRuntimeState state in StatesOfKind(kind))
            {
                state.Phase = phase;
            }
        }

        private void ResetActors(TrafficEventKind kind, long dayIndex)
        {
            foreach (EventActorRuntimeState state in StatesOfKind(kind))
            {
                state.Reset(dayIndex, routes[state.Definition.RouteId]);
            }
        }

        private void ResetEventDay(long dayIndex)
        {
            foreach (EventActorRuntimeState state in eventStates.Values)
            {
                state.Reset(dayIndex, routes[state.Definition.RouteId]);
            }
        }

        private sealed class EventActorRuntimeState
        {
            private EventActorRuntimeState(
                TrafficEventActorDefinition definition)
            {
                Definition = definition;
                TravelsForward = definition.TravelsForward;
            }

            public TrafficEventActorDefinition Definition { get; }
            public TrafficEventActorPhase Phase { get; set; }
            public float RouteProgress01 { get; set; }
            public bool TravelsForward { get; set; }
            public bool Resident { get; set; }
            public float DesiredSpeedMetersPerSecond { get; set; }
            public float PhaseRealSecondsRemaining { get; set; }
            public int CompletedRuns { get; set; }
            public long EventDayIndex { get; set; } = -1L;
            public int StartPointIndex { get; set; }
            public int EndPointIndex { get; set; }
            public float StartProgress01 { get; set; }
            public int SegmentHint { get; set; } = -1;
            public bool HasPhysicalPose { get; set; }
            public Vector3 WorldPosition { get; set; }
            public Quaternion WorldRotation { get; set; } = Quaternion.identity;
            public float CurrentSpeedMetersPerSecond { get; set; }

            public static EventActorRuntimeState CreateInitial(
                TrafficEventActorDefinition definition) => new(definition);

            public void Reset(long dayIndex, TrafficRouteGeometry geometry)
            {
                Phase = TrafficEventActorPhase.Inactive;
                Resident = false;
                EventDayIndex = dayIndex;
                StartPointIndex = Definition.PrimaryStartPointIndex;
                EndPointIndex = Definition.PrimaryEndPointIndex;
                StartProgress01 = geometry.ProgressAtPointIndex(StartPointIndex);
                RouteProgress01 = StartProgress01;
                TravelsForward = Definition.TravelsForward;
                SegmentHint = StartPointIndex;
                HasPhysicalPose = false;
                CurrentSpeedMetersPerSecond = 0f;
                PhaseRealSecondsRemaining = 0f;
            }

            public TrafficEventActorStateDto ToDto() => new()
            {
                actorId = Definition.ActorId,
                stableInstanceId = Definition.StableInstanceId,
                kind = (int)Definition.Kind,
                phase = (int)Phase,
                routeId = Definition.RouteId,
                routeProgress01 = Mathf.Clamp01(RouteProgress01),
                travelsForward = TravelsForward,
                resident = Resident,
                desiredSpeedMetersPerSecond =
                    Mathf.Max(0f, DesiredSpeedMetersPerSecond),
                phaseRealSecondsRemaining =
                    Mathf.Max(0f, PhaseRealSecondsRemaining),
                completedRuns = Mathf.Max(0, CompletedRuns),
                eventDayIndex = EventDayIndex,
                hasPhysicalPose = HasPhysicalPose,
                worldPosition = WorldPosition,
                worldRotation = WorldRotation,
                currentSpeedMetersPerSecond =
                    Mathf.Max(0f, CurrentSpeedMetersPerSecond),
            };

            public static EventActorRuntimeState FromDto(
                TrafficEventActorDefinition definition,
                TrafficEventActorStateDto dto,
                IReadOnlyDictionary<string, TrafficRouteGeometry> geometries)
            {
                var state = new EventActorRuntimeState(definition)
                {
                    Phase = (TrafficEventActorPhase)dto.phase,
                    RouteProgress01 = dto.routeProgress01,
                    TravelsForward = dto.travelsForward,
                    Resident = dto.resident,
                    DesiredSpeedMetersPerSecond =
                        dto.desiredSpeedMetersPerSecond,
                    PhaseRealSecondsRemaining =
                        dto.phaseRealSecondsRemaining,
                    CompletedRuns = dto.completedRuns,
                    EventDayIndex = dto.eventDayIndex,
                    HasPhysicalPose = dto.hasPhysicalPose,
                    WorldPosition = dto.worldPosition,
                    WorldRotation = dto.worldRotation,
                    CurrentSpeedMetersPerSecond =
                        dto.currentSpeedMetersPerSecond,
                    StartPointIndex = definition.PrimaryStartPointIndex,
                    EndPointIndex = definition.PrimaryEndPointIndex,
                    SegmentHint = -1,
                };
                state.StartProgress01 = geometries[definition.RouteId]
                    .ProgressAtPointIndex(state.StartPointIndex);
                return state;
            }
        }

        private sealed class EventGroupRuntimeState
        {
            public EventGroupRuntimeState(string groupId)
            {
                GroupId = groupId;
            }

            public string GroupId { get; }
            public bool ScheduleActive { get; set; }
            public bool Resident { get; set; }
            public long EvaluatedDayIndex { get; set; } = -1L;
            public float RealSecondsRemaining { get; set; }
            public int SequenceOrdinal { get; set; }
            public int SelectedSiteIndex { get; set; } = -1;
            public bool DailyChanceEvaluated { get; set; }
            public bool ChaseRequested { get; set; }

            public TrafficEventGroupStateDto ToDto() => new()
            {
                groupId = GroupId,
                scheduleActive = ScheduleActive,
                resident = Resident,
                evaluatedDayIndex = EvaluatedDayIndex,
                realSecondsRemaining = Mathf.Max(0f, RealSecondsRemaining),
                sequenceOrdinal = Mathf.Max(0, SequenceOrdinal),
                selectedSiteIndex = SelectedSiteIndex,
                dailyChanceEvaluated = DailyChanceEvaluated,
                chaseRequested = ChaseRequested,
            };

            public static EventGroupRuntimeState FromDto(
                TrafficEventGroupStateDto dto) => new(dto.groupId)
            {
                ScheduleActive = dto.scheduleActive,
                Resident = dto.resident,
                EvaluatedDayIndex = dto.evaluatedDayIndex,
                RealSecondsRemaining = dto.realSecondsRemaining,
                SequenceOrdinal = dto.sequenceOrdinal,
                SelectedSiteIndex = dto.selectedSiteIndex,
                DailyChanceEvaluated = dto.dailyChanceEvaluated,
                ChaseRequested = dto.chaseRequested,
            };
        }
    }
}
