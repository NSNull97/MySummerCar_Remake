using System;
using MSC.Characters;
using MSC.Core.Time;
using UnityEngine;

namespace MSC.Traffic
{
    public sealed partial class TrafficWorldRuntime
    {
        private const float CousinRouteTargetDistanceMeters = 6f;

        private bool cousinRallyDay;
        private bool cousinInJail;
        private bool playerWanted;

        /// <summary>
        /// Project-owned story/event bridge. The caller reports authoritative
        /// progression state; this runtime only applies donor traffic residency.
        /// </summary>
        public void SetCousinTrafficConditions(
            bool rallyDay,
            bool isCousinInJail,
            bool isPlayerWanted)
        {
            cousinRallyDay = rallyDay;
            cousinInJail = isCousinInJail;
            playerWanted = isPlayerWanted;
        }

        private void InitializeCousinState(
            ActorRuntimeState state,
            GameTimeSnapshot snapshot)
        {
            if (!IsCousin(state))
            {
                return;
            }

            state.CousinContext = CousinTrafficContext.OrdinaryFittan;
            state.CousinRouteStage = CousinRouteStage.DancehallDeparture;
            state.CousinScheduleCheckRealSecondsRemaining =
                TrafficCousinBehaviorRules.SchedulePollRealSeconds;
            PlaceOrdinaryFittanAtAuthoredSpawn(state, snapshot.DayIndex);
        }

        private void UpdateCousinLifecycle(float realDeltaSeconds)
        {
            if (!states.TryGetValue(
                    TrafficCousinBehaviorRules.ActorId,
                    out ActorRuntimeState state))
            {
                return;
            }

            state.CousinScheduleCheckRealSecondsRemaining = Mathf.Max(
                0f,
                state.CousinScheduleCheckRealSecondsRemaining -
                Mathf.Max(0f, realDeltaSeconds));
            if (state.CousinScheduleCheckRealSecondsRemaining > 0f)
            {
                return;
            }

            state.CousinScheduleCheckRealSecondsRemaining =
                TrafficCousinBehaviorRules.SchedulePollRealSeconds;
            GameTimeSnapshot snapshot = gameTime.Snapshot;
            bool saturdayWindow =
                TrafficCousinBehaviorRules.IsSaturdayKuskiWindow(
                    snapshot.DayIndex,
                    snapshot.SecondsOfDay) &&
                !cousinRallyDay &&
                !cousinInJail;

            if (state.CousinContext == CousinTrafficContext.OrdinaryFittan)
            {
                if (!saturdayWindow)
                {
                    state.Active = !cousinRallyDay && !cousinInJail;
                    // CheckRally also reads PlayerWanted in the donor. Its exact
                    // crime-state branch is not guessed here; keep the input
                    // explicit until the authority system supplies that mapping.
                    _ = playerWanted;
                    return;
                }

                Vector3 ordinaryPosition = ResolveActorWorldPosition(state);
                CousinRouteLeg firstLeg = TrafficCousinBehaviorRules.ResolveLeg(
                    CousinRouteStage.DancehallDeparture);
                TrafficRouteGeometry dancehall = routes[firstLeg.RouteId];
                Vector3 saturdaySpawn = dancehall.Resolve(
                    dancehall.ProgressAtPointIndex(firstLeg.StartPointIndex),
                    firstLeg.TravelsForward).Position;
                if (Vector3.Distance(player.position, ordinaryPosition) <=
                        TrafficCousinBehaviorRules
                            .OrdinaryRetirementDistanceMeters ||
                    Vector3.Distance(player.position, saturdaySpawn) <=
                        TrafficCousinBehaviorRules
                            .SaturdayActivationDistanceMeters)
                {
                    return;
                }

                SwitchCousinContext(
                    state,
                    CousinTrafficContext.SaturdayKuski,
                    snapshot.DayIndex);
                return;
            }

            if (saturdayWindow)
            {
                return;
            }

            // The donor delays the rally-day context replacement until KUSKI
            // is off-screen. The ordinary 04:00 schedule shutdown is immediate
            // and must not leave the Saturday car resident beside the player.
            if (cousinRallyDay &&
                Vector3.Distance(
                    player.position,
                    ResolveActorWorldPosition(state)) <=
                TrafficCousinBehaviorRules.SaturdayRetirementDistanceMeters)
            {
                return;
            }

            SwitchCousinContext(
                state,
                CousinTrafficContext.OrdinaryFittan,
                snapshot.DayIndex);
        }

        private void SwitchCousinContext(
            ActorRuntimeState state,
            CousinTrafficContext context,
            long dayIndex)
        {
            if (presentations.ContainsKey(state.Definition.ActorId))
            {
                RemovePresentation(state.Definition.ActorId);
            }

            state.CousinContext = context;
            state.CousinActivationOrdinal++;
            state.HasPhysicalPose = false;
            state.HasSafePose = false;
            state.CurrentSpeedMetersPerSecond = 0f;
            state.CruiseSpeedMetersPerSecond =
                state.DesiredSpeedMetersPerSecond;
            state.ManeuverState = StoryTrafficManeuverState.Cruise;
            state.ManeuverStateSeconds = 0f;
            state.DriftSlipDegrees = 0f;
            state.Active = context == CousinTrafficContext.SaturdayKuski ||
                           !cousinRallyDay && !cousinInJail;

            if (context == CousinTrafficContext.OrdinaryFittan)
            {
                PlaceOrdinaryFittanAtAuthoredSpawn(state, dayIndex);
                return;
            }

            state.CousinRouteStage = CousinRouteStage.DancehallDeparture;
            ApplyCousinRouteLeg(state, motion: null);
        }

        private void PlaceOrdinaryFittanAtAuthoredSpawn(
            ActorRuntimeState state,
            long dayIndex)
        {
            int spawnIndex =
                TrafficCousinBehaviorRules.ResolveOrdinarySpawnAnchorIndex(
                    state.Definition.DeterministicSeed,
                    state.CousinActivationOrdinal,
                    dayIndex);
            TrafficRouteGeometry dirt = routes[DirtRoadRouteId];
            TrafficRouteSample projected = dirt.ProjectNearest(
                TrafficCousinBehaviorRules.OrdinarySpawnAnchors[spawnIndex],
                forward: true);
            state.CurrentRouteId = DirtRoadRouteId;
            state.CurrentTravelsForward = true;
            state.RouteProgress01 = projected.Progress01;
            state.SegmentHint = projected.SegmentIndex;
            state.LaneOffsetMeters = 0f;
            state.HasPhysicalPose = false;
        }

        private bool TryAdvanceCousinRouteProgram(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion)
        {
            if (!IsCousinSaturdayContext(state))
            {
                return false;
            }

            CousinRouteLeg leg = TrafficCousinBehaviorRules.ResolveLeg(
                state.CousinRouteStage);
            if (!string.Equals(
                    state.CurrentRouteId,
                    leg.RouteId,
                    StringComparison.Ordinal))
            {
                ApplyCousinRouteLeg(state, motion);
                return true;
            }

            TrafficRouteGeometry geometry = routes[leg.RouteId];
            float endpointProgress = geometry.ProgressAtPointIndex(
                leg.EndPointIndex);
            TrafficRouteSample endpoint = geometry.Resolve(
                endpointProgress,
                leg.TravelsForward);
            bool crossed = leg.TravelsForward
                ? state.RouteProgress01 >= endpointProgress - 0.0005f
                : state.RouteProgress01 <= endpointProgress + 0.0005f;
            if (!crossed &&
                Vector3.Distance(motion.PhysicalWorldPosition, endpoint.Position) >
                CousinRouteTargetDistanceMeters)
            {
                return false;
            }

            state.CousinRouteStage =
                TrafficCousinBehaviorRules.ResolveNextStage(
                    state.CousinRouteStage);
            ApplyCousinRouteLeg(state, motion);
            return true;
        }

        private bool TryResetOrdinaryFittanRoute(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion,
            float previousProgress)
        {
            if (!IsCousin(state) ||
                state.CousinContext != CousinTrafficContext.OrdinaryFittan ||
                !string.Equals(
                    state.CurrentRouteId,
                    DirtRoadRouteId,
                    StringComparison.Ordinal) ||
                previousProgress < 0.9f ||
                state.RouteProgress01 > 0.1f)
            {
                return false;
            }

            TrafficRouteGeometry dirt = routes[DirtRoadRouteId];
            SwitchActorRoute(
                state,
                DirtRoadRouteId,
                dirt.ProgressAtPointIndex(
                    OrdinaryFittanFirstDirtRoadPointIndex),
                travelsForward: true,
                motion);
            state.SegmentHint = OrdinaryFittanFirstDirtRoadPointIndex;
            return true;
        }

        private void ApplyCousinRouteLeg(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion)
        {
            CousinRouteLeg leg = TrafficCousinBehaviorRules.ResolveLeg(
                state.CousinRouteStage);
            TrafficRouteGeometry geometry = routes[leg.RouteId];
            state.LaneOffsetMeters = leg.LaneOffsetMeters;
            SwitchActorRoute(
                state,
                leg.RouteId,
                geometry.ProgressAtPointIndex(leg.StartPointIndex),
                leg.TravelsForward,
                motion);
        }

        private Vector3 ResolveActorWorldPosition(ActorRuntimeState state)
        {
            if (presentations.TryGetValue(
                    state.Definition.ActorId,
                    out StoryTrafficVehiclePresentationBinding motion) &&
                motion != null)
            {
                return motion.PhysicalWorldPosition;
            }

            if (state.HasPhysicalPose)
            {
                return state.WorldPosition;
            }

            return ResolveLanePosition(
                ResolveLogicalSample(state),
                state.LaneOffsetMeters);
        }

        private static bool IsCousin(ActorRuntimeState state) =>
            state?.Definition != null &&
            string.Equals(
                state.Definition.ActorId,
                TrafficCousinBehaviorRules.ActorId,
                StringComparison.Ordinal);

        private static bool IsCousinSaturdayContext(
            ActorRuntimeState state) =>
            IsCousin(state) &&
            state.CousinContext == CousinTrafficContext.SaturdayKuski;

        private static string ResolveActorPresentationId(
            ActorRuntimeState state) =>
            IsCousin(state)
                ? state.CousinContext == CousinTrafficContext.SaturdayKuski
                    ? TrafficCousinBehaviorRules.SaturdayPresentationId
                    : TrafficCousinBehaviorRules.OrdinaryPresentationId
                : state.Definition.PresentationId;

        private static bool IsCousinRuntimeRoute(string routeId) =>
            string.Equals(routeId, DirtRoadRouteId, StringComparison.Ordinal) ||
            string.Equals(
                routeId,
                "route.traffic.dancehall",
                StringComparison.Ordinal) ||
            string.Equals(
                routeId,
                "route.traffic.road-race",
                StringComparison.Ordinal) ||
            string.Equals(
                routeId,
                "route.traffic.track-field",
                StringComparison.Ordinal) ||
            string.Equals(
                routeId,
                "route.traffic.home-road",
                StringComparison.Ordinal);
    }
}
