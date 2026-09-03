using System;
using MSC.Characters;
using UnityEngine;

namespace MSC.Traffic
{
    public sealed partial class TrafficWorldRuntime
    {
        private const float TownDecisionIntervalSimulationSeconds = 90f;
        private const float TownEntryChance01 = 0.04f;
        private const float TownLoopChance01 = 0.65f;
        private const float TownEntryCaptureDistanceMeters = 38f;
        private const float TownRouteRejoinEnterDistanceMeters = 4.5f;
        private const float TownRouteRejoinExitDistanceMeters = 2.5f;
        private const float TownRouteRejoinEnterHeadingDot = 0.2f;
        private const float TownRouteRejoinExitHeadingDot = 0.75f;
        private const float TownRouteRejoinLookAheadMeters = 6f;
        private const float TownRouteRejoinSpeedMetersPerSecond = 6.5f;

        private enum TownExcursionDestination
        {
            Undecided = 0,
            TownLoop = 1,
            GasPump = 2,
        }

        private float townHighwayEntryProgress01;
        private float townLoopEntryProgress01;
        private float gasPumpEntryProgress01;
        private float townLoopHighwayReturnProgress01;
        private float gasPumpHighwayReturnProgress01;
        private Vector3 townHighwayEntryPosition;
        private bool gasPumpExternallyOccupied;

        public bool IsGasPumpExternallyOccupied =>
            gasPumpExternallyOccupied;

        /// <summary>
        /// Reservation hook for the future player/Satsuma vehicle domain. The
        /// traffic runtime owns the branch and waiting behavior; the vehicle
        /// domain only reports whether a player-owned car occupies the pumps.
        /// </summary>
        public void SetGasPumpExternalOccupancy(bool occupied)
        {
            gasPumpExternallyOccupied = occupied;
        }

        private void InitializeTownExcursionGeometry()
        {
            if (!catalog.TryGetRoute(HighwayRouteId, out _) ||
                !catalog.TryGetRoute(TownEntryRouteId, out var townEntry) ||
                !catalog.TryGetRoute(TownLoopRouteId, out var townLoop) ||
                !catalog.TryGetRoute(GasPumpRouteId, out var gasPump) ||
                townEntry.WorldPoints.Count < 2 ||
                gasPump.WorldPoints.Count < 2)
            {
                throw new InvalidOperationException(
                    "Traffic town-excursion routes are missing.");
            }

            TrafficRouteGeometry highway = routes[HighwayRouteId];
            TrafficRouteSample entry = highway.ProjectNearest(
                townEntry.WorldPoints[0],
                forward: false);
            townHighwayEntryProgress01 = entry.Progress01;
            townHighwayEntryPosition = entry.Position;

            Vector3 entryEndpoint =
                townEntry.WorldPoints[townEntry.WorldPoints.Count - 1];
            townLoopEntryProgress01 = ResolveTownRouteJoinProgress(
                routes[TownLoopRouteId],
                entryEndpoint,
                forward: true);
            gasPumpEntryProgress01 = ResolveTownRouteJoinProgress(
                routes[GasPumpRouteId],
                entryEndpoint,
                forward: true);
            townLoopHighwayReturnProgress01 = ResolveTownRouteJoinProgress(
                highway,
                townLoop.WorldPoints[townLoop.WorldPoints.Count - 1],
                forward: false);
            gasPumpHighwayReturnProgress01 = ResolveTownRouteJoinProgress(
                highway,
                gasPump.WorldPoints[gasPump.WorldPoints.Count - 1],
                forward: false);
        }

        private void AdvanceTownDecision(
            ActorRuntimeState state,
            float deltaRealSeconds,
            long dayIndex)
        {
            TrafficActorDefinition definition = state.Definition;
            if (!state.Active ||
                definition.RoutePolicy != TrafficRoutePolicy
                    .TrafficExpansionTownExcursions ||
                definition.TravelsForward ||
                !string.Equals(
                    state.CurrentRouteId,
                    HighwayRouteId,
                    StringComparison.Ordinal) ||
                state.TownExcursionPending ||
                HasAnotherTownExcursion(state))
            {
                return;
            }

            state.TownDecisionGameSecondsRemaining = AdvanceRealTimeCountdown(
                state.TownDecisionGameSecondsRemaining,
                deltaRealSeconds);
            if (state.TownDecisionGameSecondsRemaining > 0f)
            {
                return;
            }

            state.TownDecisionGameSecondsRemaining =
                TownDecisionIntervalSimulationSeconds;
            float roll = ResolveExcursionRoll(
                definition.DeterministicSeed,
                state.CompletedTownExcursions,
                state.TownDecisionAttemptCount,
                dayIndex,
                salt: 0x31d);
            state.TownDecisionAttemptCount++;
            if (roll < TownEntryChance01)
            {
                state.TownExcursionPending = true;
            }
        }

        private static float AdvanceRealTimeCountdown(
            float remainingSeconds,
            float deltaRealSeconds) =>
            Mathf.Max(
                0f,
                Mathf.Max(0f, remainingSeconds) -
                Mathf.Max(0f, deltaRealSeconds));

        private bool TryTransitionTownRoute(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion,
            bool endpointCompleted)
        {
            if (state.Definition.RoutePolicy != TrafficRoutePolicy
                .TrafficExpansionTownExcursions)
            {
                return false;
            }

            if (string.Equals(
                    state.CurrentRouteId,
                    HighwayRouteId,
                    StringComparison.Ordinal))
            {
                if (!state.TownExcursionPending ||
                    HasAnotherTownExcursion(state))
                {
                    return false;
                }

                TrafficRouteSample current = routes[HighwayRouteId].Resolve(
                    state.RouteProgress01,
                    state.CurrentTravelsForward);
                if (Vector3.Distance(
                        current.Position,
                        townHighwayEntryPosition) >
                    TownEntryCaptureDistanceMeters)
                {
                    return false;
                }

                SwitchActorRoute(
                    state,
                    TownEntryRouteId,
                    progress01: 0f,
                    travelsForward: true,
                    motion: motion);
                return true;
            }

            if (string.Equals(
                    state.CurrentRouteId,
                    TownEntryRouteId,
                    StringComparison.Ordinal) &&
                (endpointCompleted || state.RouteProgress01 >= 0.995f))
            {
                if (state.TownExcursionDestination ==
                    TownExcursionDestination.Undecided)
                {
                    float choice = ResolveExcursionRoll(
                        state.Definition.DeterministicSeed,
                        state.CompletedTownExcursions,
                        state.SpawnAttemptCount,
                        gameTime.Snapshot.DayIndex,
                        salt: 0x713);
                    state.TownExcursionDestination =
                        choice < TownLoopChance01
                            ? TownExcursionDestination.TownLoop
                            : TownExcursionDestination.GasPump;
                }

                if (state.TownExcursionDestination ==
                        TownExcursionDestination.GasPump &&
                    gasPumpExternallyOccupied)
                {
                    state.RouteProgress01 = 1f;
                    motion?.SetServiceStopHold(true);
                    return true;
                }

                motion?.SetServiceStopHold(false);
                bool townLoopDestination =
                    state.TownExcursionDestination ==
                    TownExcursionDestination.TownLoop;
                SwitchActorRoute(
                    state,
                    townLoopDestination
                            ? TownLoopRouteId
                            : GasPumpRouteId,
                    progress01: townLoopDestination
                        ? townLoopEntryProgress01
                        : gasPumpEntryProgress01,
                    travelsForward: true,
                    motion: motion);
                return true;
            }

            bool finishedTownLoop = string.Equals(
                state.CurrentRouteId,
                TownLoopRouteId,
                StringComparison.Ordinal);
            bool finishedGasPump = string.Equals(
                state.CurrentRouteId,
                GasPumpRouteId,
                StringComparison.Ordinal);
            if ((finishedTownLoop || finishedGasPump) &&
                (endpointCompleted || state.RouteProgress01 >= 0.995f))
            {
                state.CompletedTownExcursions++;
                state.TownExcursionPending = false;
                state.TownExcursionDestination =
                    TownExcursionDestination.Undecided;
                state.TownDecisionGameSecondsRemaining =
                    TownDecisionIntervalSimulationSeconds;
                SwitchActorRoute(
                    state,
                    HighwayRouteId,
                    finishedTownLoop
                        ? townLoopHighwayReturnProgress01
                        : gasPumpHighwayReturnProgress01,
                    travelsForward: false,
                    motion: motion);
                return true;
            }

            return false;
        }

        private void SwitchActorRoute(
            ActorRuntimeState state,
            string routeId,
            float progress01,
            bool travelsForward,
            StoryTrafficVehiclePresentationBinding motion)
        {
            state.CurrentRouteId = routeId;
            state.CurrentTravelsForward = travelsForward;
            state.RouteProgress01 = Mathf.Clamp01(progress01);
            state.SegmentHint = -1;
            motion?.SetServiceStopHold(false);
            motion?.SetRouteRejoinActive(false);
            if (motion == null)
            {
                state.HasPhysicalPose = false;
                return;
            }

            motion.RestoreRuntimeState(state.CreateMotionState());
            ConfigureActorMotionForCurrentRoute(state, motion);
            TrafficRouteSample sample = routes[routeId].Resolve(
                state.RouteProgress01,
                travelsForward);
            motion.SetPhysicalRouteGuidanceTarget(
                sample.Position,
                sample.Rotation,
                state.RouteProgress01);
        }

        internal static float ResolveTownRouteJoinProgress(
            TrafficRouteGeometry destination,
            Vector3 sourceEndpoint,
            bool forward)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            return destination.ProjectNearest(sourceEndpoint, forward)
                .Progress01;
        }

        internal static bool IsTownExcursionRoute(string routeId) =>
            string.Equals(routeId, TownEntryRouteId,
                StringComparison.Ordinal) ||
            string.Equals(routeId, TownLoopRouteId,
                StringComparison.Ordinal) ||
            string.Equals(routeId, GasPumpRouteId,
                StringComparison.Ordinal);

        internal static float ResolveTownExcursionLookAheadMeters(
            string routeId,
            float speedMetersPerSecond,
            bool routeRejoinActive)
        {
            if (routeRejoinActive)
            {
                return TownRouteRejoinLookAheadMeters;
            }

            float speed01 = Mathf.InverseLerp(
                0f,
                55f / 3.6f,
                Mathf.Max(0f, speedMetersPerSecond));
            if (string.Equals(routeId, GasPumpRouteId,
                    StringComparison.Ordinal))
            {
                return Mathf.Lerp(4f, 6.5f, speed01);
            }

            if (string.Equals(routeId, TownLoopRouteId,
                    StringComparison.Ordinal))
            {
                return Mathf.Lerp(5.5f, 9f, speed01);
            }

            return Mathf.Lerp(6.5f, 10f, speed01);
        }

        internal static bool ResolveTownRouteRejoinActive(
            bool currentlyActive,
            float routeSeparationMeters,
            float headingDot)
        {
            if (!float.IsFinite(routeSeparationMeters) ||
                !float.IsFinite(headingDot))
            {
                return true;
            }

            return currentlyActive
                ? routeSeparationMeters >
                  TownRouteRejoinExitDistanceMeters ||
                  headingDot < TownRouteRejoinExitHeadingDot
                : routeSeparationMeters >
                  TownRouteRejoinEnterDistanceMeters ||
                  headingDot < TownRouteRejoinEnterHeadingDot;
        }

        internal static float ResolveTownRouteSpeedCapMetersPerSecond(
            string routeId,
            in TrafficRouteSample projection,
            in TrafficRouteSample guidance,
            float lookAheadMeters,
            bool routeRejoinActive)
        {
            if (routeRejoinActive)
            {
                return TownRouteRejoinSpeedMetersPerSecond;
            }

            float routeMaximum = 55f / 3.6f;
            if (string.Equals(routeId, GasPumpRouteId,
                    StringComparison.Ordinal))
            {
                ResolveGasPumpSpeedEnvelope(
                    projection.Progress01,
                    out _,
                    out routeMaximum);
            }

            Vector3 projectionForward =
                projection.Rotation * Vector3.forward;
            Vector3 guidanceForward =
                guidance.Rotation * Vector3.forward;
            float headingChangeDegrees = Vector3.Angle(
                projectionForward,
                guidanceForward);
            if (headingChangeDegrees <= 3f)
            {
                return routeMaximum;
            }

            float headingRadians = headingChangeDegrees * Mathf.Deg2Rad;
            float estimatedRadius = Mathf.Max(
                4f,
                lookAheadMeters / Mathf.Max(0.05f, headingRadians));
            float curveCap = Mathf.Sqrt(3.1f * estimatedRadius);
            float minimumCurveSpeed = string.Equals(
                routeId,
                GasPumpRouteId,
                StringComparison.Ordinal)
                    ? 3.2f
                    : 5.5f;
            return Mathf.Clamp(
                curveCap,
                minimumCurveSpeed,
                routeMaximum);
        }

        private void ConfigureActorMotionForCurrentRoute(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion)
        {
            TrafficActorDefinition definition = state.Definition;
            bool ordinaryFittan = IsOrdinaryFittanDirtRoute(state);
            float minimumSpeed = definition.MinimumSpeedMetersPerSecond;
            float maximumSpeed = definition.MaximumSpeedMetersPerSecond;
            float acceleration = 4.8f;
            float braking = 17f;
            float turnRate = 125f;
            float wander = 0.18f;
            float driftSpeed = 32f;
            float driftEntry = 35f;
            float driftExit = 10f;
            float driftSlip = 12f;
            float baseLane = definition.BaseLaneOffsetMeters;
            float passingLane = definition.PassingLaneOffsetMeters;

            if (definition.DrivingProfile == TrafficDrivingProfile.DrunkCousin)
            {
                acceleration = 3.5f;
                braking = ordinaryFittan ? 16f : 12f;
                turnRate = ordinaryFittan ? 135f : 105f;
                wander = ordinaryFittan ? 0.18f : 1.05f;
                driftSpeed = 1000f;
                driftEntry = 35f;
                driftExit = 8f;
                driftSlip = 14f;
                baseLane = state.CousinContext ==
                    CousinTrafficContext.SaturdayKuski
                        ? state.LaneOffsetMeters
                        : 0f;
                passingLane = -2f;
            }
            else if (string.Equals(
                         state.CurrentRouteId,
                         TownEntryRouteId,
                         StringComparison.Ordinal))
            {
                minimumSpeed = 15f / 3.6f;
                maximumSpeed = 55f / 3.6f;
                wander = 0.12f;
                baseLane = 0f;
                passingLane = -1.65f;
            }
            else if (string.Equals(
                         state.CurrentRouteId,
                         TownLoopRouteId,
                         StringComparison.Ordinal))
            {
                minimumSpeed = 25f / 3.6f;
                maximumSpeed = 55f / 3.6f;
                wander = 0.14f;
                baseLane = 0f;
                passingLane = -1.65f;
            }
            else if (string.Equals(
                         state.CurrentRouteId,
                         GasPumpRouteId,
                         StringComparison.Ordinal))
            {
                ResolveGasPumpSpeedEnvelope(
                    state.RouteProgress01,
                    out minimumSpeed,
                    out maximumSpeed);
                wander = 0.08f;
                baseLane = 0f;
                passingLane = -1.65f;
            }

            if (ordinaryFittan)
            {
                // Apply the gravel tire/curve policy first, then restore the
                // locked FITTAN 95-105 km/h donor envelope below. The profile
                // must not silently reduce straight-road top speed.
                motion.SetRoadBehaviorProfile(
                    StoryTrafficRoadBehaviorProfile.Gravel,
                    insideDonorHandbrakeZone: false);
            }

            motion.SetConfirmedSupportColliderObstacleRejection(
                ordinaryFittan);
            if (!ordinaryFittan &&
                !IsTownExcursionRoute(state.CurrentRouteId))
            {
                motion.SetRouteSpeedCap(float.PositiveInfinity);
                motion.SetHillDriveAssist(false);
            }
            motion.ConfigureDrivingProfile(
                minimumSpeed,
                maximumSpeed,
                acceleration,
                braking,
                turnRate,
                passingLane);
            motion.ConfigureHooliganBehavior(
                wander,
                driftSpeed,
                driftEntry,
                driftExit,
                driftSlip);
            motion.ConfigureRoadLanePolicy(
                baseLane,
                passingLane,
                migrateLegacyCenterLane: true);
        }

        private static void ResolveGasPumpSpeedEnvelope(
            float progress01,
            out float minimumSpeed,
            out float maximumSpeed)
        {
            if (progress01 < 0.28f)
            {
                minimumSpeed = 10f / 3.6f;
                maximumSpeed = 25f / 3.6f;
            }
            else if (progress01 < 0.68f)
            {
                minimumSpeed = 5f / 3.6f;
                maximumSpeed = 10f / 3.6f;
            }
            else
            {
                minimumSpeed = 25f / 3.6f;
                maximumSpeed = 55f / 3.6f;
            }
        }

        private static float ResolveLogicalSpeedForCurrentRoute(
            ActorRuntimeState state)
        {
            if (string.Equals(
                    state.CurrentRouteId,
                    TownEntryRouteId,
                    StringComparison.Ordinal))
            {
                return 45f / 3.6f;
            }

            if (string.Equals(
                    state.CurrentRouteId,
                    TownLoopRouteId,
                    StringComparison.Ordinal))
            {
                return 48f / 3.6f;
            }

            if (string.Equals(
                    state.CurrentRouteId,
                    GasPumpRouteId,
                    StringComparison.Ordinal))
            {
                ResolveGasPumpSpeedEnvelope(
                    state.RouteProgress01,
                    out float minimum,
                    out float maximum);
                return (minimum + maximum) * 0.5f;
            }

            return state.DesiredSpeedMetersPerSecond;
        }

        private bool HasAnotherTownExcursion(ActorRuntimeState candidate)
        {
            foreach (ActorRuntimeState state in states.Values)
            {
                if (ReferenceEquals(state, candidate) || !state.Active)
                {
                    continue;
                }

                if (state.TownExcursionPending ||
                    !string.Equals(
                        state.CurrentRouteId,
                        HighwayRouteId,
                        StringComparison.Ordinal) &&
                    state.Definition.RoutePolicy == TrafficRoutePolicy
                        .TrafficExpansionTownExcursions)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllowedRuntimeRoute(
            TrafficActorDefinition definition,
            string routeId)
        {
            if (string.Equals(
                    routeId,
                    definition.RouteId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (definition.DrivingProfile ==
                    TrafficDrivingProfile.DrunkCousin &&
                IsCousinRuntimeRoute(routeId))
            {
                return true;
            }

            return definition.RoutePolicy == TrafficRoutePolicy
                       .TrafficExpansionTownExcursions &&
                   (string.Equals(routeId, TownEntryRouteId,
                        StringComparison.Ordinal) ||
                    string.Equals(routeId, TownLoopRouteId,
                        StringComparison.Ordinal) ||
                    string.Equals(routeId, GasPumpRouteId,
                        StringComparison.Ordinal));
        }

        private static float ResolveExcursionRoll(
            int seed,
            int completedExcursions,
            int attempt,
            long dayIndex,
            int salt)
        {
            unchecked
            {
                uint value = (uint)(seed ^ salt);
                value ^= (uint)completedExcursions * 0x9e3779b9u;
                value ^= (uint)attempt * 0x85ebca6bu;
                value ^= (uint)dayIndex * 0xc2b2ae35u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
