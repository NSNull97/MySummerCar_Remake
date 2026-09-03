using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using MSC.Core.Time;
using MSC.NPC;
using UnityEngine;

namespace MSC.Traffic
{
    public delegate void TrafficSubtitleFeedbackHandler(
        string stableLineId,
        string fallbackSubtitle,
        Vector3 worldPosition);

    public sealed partial class TrafficWorldRuntime
    {
        private const string BoatTwoTransportId = "traffic.transport.boat-2";
        public const string BusDriverStuckCurseLineId =
            "dialogue.npc.latanen.bus-stuck-curse";
        public const string BusDriverStuckCurseFallbackSubtitle =
            "Fucking fucking fuck, fuck. Fuck. Fucking fuck! Oh fuck!";
        private const float BoatInitialEligibilityDelayRealSeconds = 2f;
        private const float BoatEligibilityPollRealSeconds = 8f;
        private const float BoatJoinDistanceMeters = 12f;
        internal const int BusWastewaterBendPreviewStartPointIndex = 40;
        internal const int BusWastewaterBendEntryPointIndex = 44;
        internal const int BusWastewaterBendExitPointIndex = 72;
        internal const int BusWastewaterBendPreviewEndPointIndex = 80;
        internal const int BusHillDriveAssistStartPointIndex = 52;
        internal const int BusHillDriveAssistEndPointIndex = 80;
        internal const int BusSteepHillStartPointIndex = 61;
        internal const int BusSteepHillEndPointIndex = 68;
        internal const float BusRouteCommitCorridorMeters = 3.75f;
        internal const float BusSavedPoseMinimumForwardHeadingDot = 0.2f;
        internal const float BusRouteRejoinEnterHeadingDot = 0.35f;
        internal const float BusRouteRejoinExitHeadingDot = 0.75f;
        private const float BusOffRouteRejoinPreviewMeters = 6f;
        private const float BusOffRouteRejoinSpeedMetersPerSecond = 6.5f;

        private readonly Dictionary<string, TransportRuntimeState>
            transportStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TrafficTransportPresentationBinding>
            transportPresentations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, StoryTrafficVehicleAudioPresenter>
            transportAudioOwners = new(StringComparer.Ordinal);
        private TrafficSubtitleFeedbackHandler busDriverSubtitleFeedback;

        public void ConfigureBusDriverSubtitleFeedback(
            TrafficSubtitleFeedbackHandler configuredFeedback)
        {
            busDriverSubtitleFeedback = configuredFeedback;
        }

        private void InitializeTransportStates(GameTimeSnapshot snapshot)
        {
            foreach (TrafficTransportDefinition definition in catalog.Transports)
            {
                var state = TransportRuntimeState.CreateInitial(definition);
                transportStates.Add(definition.TransportId, state);
                if (IsBoatTwo(definition))
                {
                    PrepareBoatTwoDormant(state, snapshot,
                        BoatInitialEligibilityDelayRealSeconds);
                }
                if (definition.Kind == TrafficTransportKind.Bus)
                {
                    TrafficBusDepartureDefinition departure =
                        FindLatestDepartureAtOrBefore(
                            definition,
                            snapshot,
                            out int departureAbsoluteHour);
                    if (departure != null)
                    {
                        ActivateBusDeparture(
                            state,
                            departure,
                            departureAbsoluteHour);
                    }
                }

                if (audioBackendComponent != null)
                {
                    CreateTransportAudioOwner(state);
                }
            }
        }

        public TransportTrafficActorStateDto[] CaptureTransportActors()
        {
            EnsureInitialized();
            foreach (KeyValuePair<string,
                         TrafficTransportPresentationBinding> pair in
                     transportPresentations)
            {
                if (pair.Value != null && transportStates.TryGetValue(
                        pair.Key,
                        out TransportRuntimeState state))
                {
                    CaptureTransportPhysical(state, pair.Value);
                    TrafficRouteSample logical = ResolveTransportSample(state);
                    if (state.Definition.Kind == TrafficTransportKind.Bus &&
                        !TrafficTransportBehaviorRules
                            .IsTerminalBusPhysicalPoseAuthoritative(
                                state.BusAbandonmentPhase) &&
                        !IsPhysicalPoseCompatibleWithRoute(
                            state.WorldPosition,
                            logical.Position))
                    {
                        state.HasPhysicalPose = false;
                    }
                }
            }

            return catalog.Transports
                .Select(value => transportStates[value.TransportId].ToDto())
                .ToArray();
        }

        public bool TryValidateTransportActors(
            IReadOnlyList<TransportTrafficActorStateDto> actors,
            out string failure)
        {
            failure = string.Empty;
            if (!initialized)
            {
                failure = "Traffic runtime is not initialized.";
                return false;
            }

            IReadOnlyList<TransportTrafficActorStateDto> source = actors ??
                Array.Empty<TransportTrafficActorStateDto>();
            if (source.Count == 0)
            {
                return true;
            }

            if (source.Count != catalog.Transports.Count)
            {
                failure = $"Transport save contains {source.Count} actors; " +
                          $"expected {catalog.Transports.Count}.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TransportTrafficActorStateDto dto in source)
            {
                if (dto == null ||
                    !catalog.TryGetTransport(dto.transportId, out var definition) ||
                    !ids.Add(dto.transportId) ||
                    !string.Equals(dto.stableInstanceId,
                        definition.StableInstanceId,
                        StringComparison.Ordinal) ||
                    dto.kind != (int)definition.Kind ||
                    !IsDefinitionRoute(definition, dto.routeId) ||
                    !TransportRuntimeState.TryValidateDto(
                        definition,
                        dto,
                        out failure))
                {
                    if (string.IsNullOrWhiteSpace(failure))
                    {
                        failure = $"Transport actor '{dto?.transportId}' is invalid.";
                    }

                    return false;
                }
            }

            return true;
        }

        public bool TryRestoreTransportActors(
            IReadOnlyList<TransportTrafficActorStateDto> actors,
            out string failure)
        {
            if (!TryValidateTransportActors(actors, out failure))
            {
                return false;
            }

            RemoveAllTransportPresentations();
            transportStates.Clear();
            IReadOnlyList<TransportTrafficActorStateDto> source = actors ??
                Array.Empty<TransportTrafficActorStateDto>();
            if (source.Count == 0)
            {
                InitializeTransportStatesWithoutAudio(gameTime.Snapshot);
            }
            else
            {
                foreach (TransportTrafficActorStateDto dto in source)
                {
                    TrafficTransportDefinition definition = catalog.Transports
                        .Single(value => value.TransportId == dto.transportId);
                    transportStates.Add(
                        definition.TransportId,
                        TransportRuntimeState.FromDto(definition, dto));
                }
            }

            ReconcileTransportAudioOwners();
            ReconcileTransportMaterialization();
            failure = string.Empty;
            return true;
        }

        public bool TryGetTransportState(
            string transportId,
            out TransportTrafficActorStateDto state)
        {
            if (transportStates.TryGetValue(
                    transportId ?? string.Empty,
                    out TransportRuntimeState runtimeState))
            {
                state = runtimeState.ToDto();
                return true;
            }

            state = null;
            return false;
        }

        private void InitializeTransportStatesWithoutAudio(
            GameTimeSnapshot snapshot)
        {
            foreach (TrafficTransportDefinition definition in catalog.Transports)
            {
                var state = TransportRuntimeState.CreateInitial(definition);
                transportStates.Add(definition.TransportId, state);
                if (IsBoatTwo(definition))
                {
                    PrepareBoatTwoDormant(state, snapshot,
                        BoatInitialEligibilityDelayRealSeconds);
                }
                if (definition.Kind != TrafficTransportKind.Bus)
                {
                    continue;
                }

                TrafficBusDepartureDefinition departure =
                    FindLatestDepartureAtOrBefore(
                        definition,
                        snapshot,
                        out int departureAbsoluteHour);
                if (departure == null)
                {
                    continue;
                }

                ActivateBusDeparture(
                    state,
                    departure,
                    departureAbsoluteHour);
            }
        }

        private void ActivateBusDeparture(
            TransportRuntimeState state,
            TrafficBusDepartureDefinition departure,
            int absoluteHour)
        {
            TrafficRouteGeometry geometry =
                routes[state.Definition.PrimaryRouteId];
            state.RouteId = state.Definition.PrimaryRouteId;
            state.RouteProgress01 = geometry.ProgressAtPointIndex(
                departure.RouteStartPointIndex);
            state.Active = true;
            state.LastDepartureAbsoluteHour = absoluteHour;
            state.DwellGameSecondsRemaining = 0f;
            state.NextStopIndex = ResolveNextStopIndex(state);
            state.HasPhysicalPose = false;
            state.SegmentHint = departure.RouteStartPointIndex;
            ResetBusAbandonmentForNewDeparture(state);
        }

        private static void ResetBusAbandonmentForNewDeparture(
            TransportRuntimeState state)
        {
            BusTerminalAbandonmentState reset =
                TrafficTransportBehaviorRules
                    .ResetBusAbandonmentForNewDeparture();
            state.BusAbandonmentPhase = reset.Phase;
            state.BusStallMonitorArmed = reset.MonitorArmed;
            state.BusStuckRealSeconds = reset.StuckRealSeconds;
            state.BusShutdownDelayRealSecondsRemaining =
                reset.ShutdownDelayRealSecondsRemaining;
            state.BusHasForwardProgressAnchor = false;
            state.BusForwardProgressAnchor01 = 0f;
            state.BusForwardProgressAnchorCompletedTrips = 0;
            state.BusRecoveryCountAtForwardProgress = 0;
            state.BusRecoveryEpisodeActive = false;
            state.BusRecoveryExhausted = false;
            state.HasBusDriverPose = false;
            state.BusDriverWorldPosition = Vector3.zero;
            state.BusDriverWorldRotation = Quaternion.identity;
            state.BusDriverCurseCooldownRealSeconds = 0f;
        }

        private bool TryEnterBusStop(
            TransportRuntimeState state,
            float previousProgress,
            float nextProgress)
        {
            IReadOnlyList<TrafficRouteStopDefinition> stops =
                state.Definition.RouteStops;
            if (stops.Count == 0)
            {
                return false;
            }

            int index = Mathf.Clamp(state.NextStopIndex, 0, stops.Count - 1);
            TrafficRouteStopDefinition stop = stops[index];
            float stopProgress = stop.RouteDistanceMeters /
                routes[state.RouteId].TotalLengthMeters;
            bool crossed = nextProgress >= previousProgress
                ? stopProgress > previousProgress && stopProgress <= nextProgress
                : stopProgress > previousProgress || stopProgress <= nextProgress;
            if (!crossed)
            {
                return false;
            }

            state.RouteProgress01 = Mathf.Clamp01(stopProgress);
            state.DwellGameSecondsRemaining =
                stop.DwellSimulationSeconds * GameSecondsPerSimulationSecond;
            state.NextStopIndex = (index + 1) % stops.Count;
            state.CurrentSpeedMetersPerSecond = 0f;
            return true;
        }

        private int ResolveNextStopIndex(TransportRuntimeState state)
        {
            float distance = state.RouteProgress01 *
                             routes[state.RouteId].TotalLengthMeters;
            for (int index = 0;
                 index < state.Definition.RouteStops.Count;
                 index++)
            {
                if (state.Definition.RouteStops[index].RouteDistanceMeters >
                    distance + 0.1f)
                {
                    return index;
                }
            }

            return 0;
        }

        private static void DecrementDwell(
            TransportRuntimeState state,
            float deltaGameSeconds)
        {
            state.DwellGameSecondsRemaining = Mathf.Max(
                0f,
                state.DwellGameSecondsRemaining - deltaGameSeconds);
        }

        private void AdvanceTrain(
            TransportRuntimeState state,
            float deltaGameSeconds)
        {
            const float minimumStepGameSeconds = 0.0001f;
            while (deltaGameSeconds > minimumStepGameSeconds)
            {
                if (state.DwellGameSecondsRemaining > 0f)
                {
                    float consumed = Mathf.Min(
                        state.DwellGameSecondsRemaining,
                        deltaGameSeconds);
                    state.DwellGameSecondsRemaining -= consumed;
                    deltaGameSeconds -= consumed;
                    if (state.DwellGameSecondsRemaining >
                        minimumStepGameSeconds)
                    {
                        state.CurrentSpeedMetersPerSecond = 0f;
                        break;
                    }

                    SwitchTrainDirection(state);
                }

                TrafficRouteGeometry geometry = routes[state.RouteId];
                float remainingDistance =
                    (1f - Mathf.Clamp01(state.RouteProgress01)) *
                    geometry.TotalLengthMeters;
                float gameSecondsToEndpoint = remainingDistance /
                                              state.Definition
                                                  .SpeedMetersPerSecond *
                                              GameSecondsPerSimulationSecond;
                if (gameSecondsToEndpoint > deltaGameSeconds)
                {
                    float distance = state.Definition.SpeedMetersPerSecond /
                                     GameSecondsPerSimulationSecond *
                                     deltaGameSeconds;
                    state.RouteProgress01 = geometry.AdvanceProgress(
                        state.RouteProgress01,
                        true,
                        distance,
                        out _);
                    state.CurrentSpeedMetersPerSecond =
                        state.Definition.SpeedMetersPerSecond;
                    state.HasPhysicalPose = false;
                    break;
                }

                state.RouteProgress01 = 1f;
                state.CompletedTrips++;
                state.CurrentSpeedMetersPerSecond = 0f;
                state.HasPhysicalPose = false;
                deltaGameSeconds = Mathf.Max(
                    0f,
                    deltaGameSeconds - gameSecondsToEndpoint);
                state.DwellGameSecondsRemaining =
                    state.Definition.EndpointDelaySimulationSeconds *
                    GameSecondsPerSimulationSecond;
                if (state.DwellGameSecondsRemaining <=
                    minimumStepGameSeconds)
                {
                    SwitchTrainDirection(state);
                }
            }
        }

        private static void SwitchTrainDirection(
            TransportRuntimeState state)
        {
            state.DwellGameSecondsRemaining = 0f;
            state.UsingSecondaryRoute = !state.UsingSecondaryRoute;
            state.RouteId = state.UsingSecondaryRoute
                ? state.Definition.SecondaryRouteId
                : state.Definition.PrimaryRouteId;
            state.RouteProgress01 = 0f;
            state.HasPhysicalPose = false;
        }

        private void ReconcileTransportMaterialization()
        {
            if (player == null)
            {
                return;
            }

            foreach (TrafficTransportDefinition definition in catalog.Transports)
            {
                TransportRuntimeState state =
                    transportStates[definition.TransportId];
                bool exists = transportPresentations.TryGetValue(
                    definition.TransportId,
                    out TrafficTransportPresentationBinding presentation);
                if (!state.Active)
                {
                    if (definition.Kind == TrafficTransportKind.Bus)
                    {
                        ReleasePhysicalTrafficCells(
                            "transport:" + definition.TransportId);
                    }
                    if (exists &&
                        (definition.Kind != TrafficTransportKind.Bus ||
                         !presentation.PassengerAboard))
                    {
                        RemoveTransportPresentation(definition.TransportId);
                    }

                    continue;
                }

                // Donor transport roots stay alive globally. Distance only
                // controls presentation detail inside a wrapper; it never
                // swaps a moving Rigidbody for clock-driven logical motion.
                if (!exists)
                {
                    if (definition.Kind != TrafficTransportKind.Bus ||
                        IsPhysicalTrafficSpawnCellReady(
                            "transport:" + definition.TransportId,
                            ResolveSupportedTransportSpawnPosition(state)))
                    {
                        MaterializeTransport(definition.TransportId);
                    }
                }
            }
        }

        private void MaterializeTransport(string transportId)
        {
            TransportRuntimeState state = transportStates[transportId];
            TrafficTransportDefinition definition = state.Definition;
            if (!presentationCatalog.TryGet(
                    definition.PresentationId,
                    out TrafficPresentationCatalogEntry entry) ||
                entry.WrapperPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Transport presentation '{definition.PresentationId}' is unavailable.");
            }

            TrafficRouteSample sample = ResolveTransportSample(state);
            state.SegmentHint = sample.SegmentIndex;
            Vector3 position = state.HasPhysicalPose
                ? state.WorldPosition
                : definition.Kind == TrafficTransportKind.Bus
                    ? ResolveLanePosition(sample, 0f)
                    : sample.Position;
            Quaternion rotation = state.HasPhysicalPose
                ? state.WorldRotation
                : sample.Rotation;
            if (state.HasPhysicalPose &&
                !TrafficTransportBehaviorRules
                    .IsTerminalBusPhysicalPoseAuthoritative(
                        state.BusAbandonmentPhase) &&
                !IsPhysicalPoseCompatibleWithRoute(
                    position,
                    sample.Position))
            {
                state.HasPhysicalPose = false;
                position = definition.Kind == TrafficTransportKind.Bus
                    ? ResolveLanePosition(sample, 0f)
                    : sample.Position;
                rotation = sample.Rotation;
            }
            else if (state.HasPhysicalPose &&
                     definition.Kind == TrafficTransportKind.Bus &&
                     !TrafficTransportBehaviorRules
                         .IsTerminalBusPhysicalPoseAuthoritative(
                             state.BusAbandonmentPhase))
            {
                rotation = ResolveBusMaterializationRotation(
                    rotation,
                    sample.Rotation);
                state.WorldRotation = rotation;
            }

            GameObject wrapper = Instantiate(
                entry.WrapperPrefab,
                position,
                rotation,
                transform);
            wrapper.name = "Traffic_Transport_" + transportId;
            TrafficTransportPresentationBinding binding = wrapper.GetComponent<
                TrafficTransportPresentationBinding>();
            string failure = string.Empty;
            if (binding == null || !binding.TryValidate(out failure) ||
                binding.Kind != definition.Kind)
            {
                Destroy(wrapper);
                throw new InvalidOperationException(
                    $"Transport presentation '{definition.PresentationId}' is invalid: {failure}");
            }

            wrapper.transform.SetPositionAndRotation(position, rotation);
            if (definition.Kind == TrafficTransportKind.Bus)
            {
                StoryTrafficVehiclePresentationBinding motion =
                    binding.RoadMotion;
                motion.SnapToRoutePoseTarget(position, rotation);
                motion.ConfigureDrivingProfile(
                    60f / 3.6f,
                    definition.SpeedMetersPerSecond,
                    configuredAccelerationMetersPerSecond2: 2.2f,
                    configuredBrakingMetersPerSecond2: 8f,
                    configuredTurnRateDegreesPerSecond: 75f,
                    configuredPassingLaneOffsetMeters: -4f);
                motion.ConfigureHooliganBehavior(
                    configuredLaneWanderAmplitudeMeters: 0f,
                    configuredMinimumDriftSpeedMetersPerSecond: 1000f,
                    configuredDriftEntryAngleDegrees: 20f,
                    configuredDriftExitAngleDegrees: 5f,
                    configuredMaximumDriftSlipDegrees: 7f);
                motion.ConfigureRoadLanePolicy(
                    configuredBaseLaneOffsetMeters: 0f,
                    configuredPassingLaneOffsetMeters: -4f,
                    migrateLegacyCenterLane: true);
                motion.ResetRoadLaneStateToBase();
                motion.SetRoadBehaviorProfile(
                    StoryTrafficRoadBehaviorProfile.Bus,
                    insideDonorHandbrakeZone: false);
                motion.SetHillDriveAssist(
                    IsBusHillDriveAssistSegment(sample.SegmentIndex));
                motion.SetServiceStopHold(
                    state.DwellGameSecondsRemaining > 0f ||
                    state.BusRecoveryExhausted ||
                    state.BusAbandonmentPhase >=
                    BusTerminalAbandonmentPhase.ShutdownDelay);
                motion.SetPhysicalRouteGuidanceTarget(
                    sample.Position,
                    sample.Rotation,
                    state.RouteProgress01);
                binding.ConfigureBusDriverRuntime(
                    audioBackendComponent,
                    state.BusDriverCurseCooldownRealSeconds,
                    transform);
                ApplyBusAbandonmentPresentation(state, binding);
            }

            transportPresentations.Add(transportId, binding);
            if (transportAudioOwners.TryGetValue(
                    transportId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null && binding.RoadMotion != null)
            {
                audio.BindMotion(binding.RoadMotion);
            }
        }

        internal static float ResolveBusLookAheadMeters(
            int segmentIndex,
            float speedMetersPerSecond)
        {
            float speed = Mathf.Max(0f, speedMetersPerSecond);
            float ordinary = Mathf.Clamp(14f + speed * 0.45f, 14f, 28f);
            if (segmentIndex < BusWastewaterBendPreviewStartPointIndex ||
                segmentIndex > BusWastewaterBendPreviewEndPointIndex)
            {
                return ordinary;
            }

            // The long wheelbase cuts the inside shoulder when the ordinary
            // car preview looks through this complete rising bend. Blend to a
            // local short preview before B44 and only restore it after B72.
            float bend = Mathf.Clamp(8.5f + speed * 0.18f, 9f, 12.5f);
            if (segmentIndex < BusWastewaterBendEntryPointIndex)
            {
                float t = Mathf.InverseLerp(
                    BusWastewaterBendPreviewStartPointIndex,
                    BusWastewaterBendEntryPointIndex,
                    segmentIndex);
                return Mathf.Lerp(ordinary, bend, t);
            }

            if (segmentIndex > BusWastewaterBendExitPointIndex)
            {
                float t = Mathf.InverseLerp(
                    BusWastewaterBendExitPointIndex,
                    BusWastewaterBendPreviewEndPointIndex,
                    segmentIndex);
                return Mathf.Lerp(bend, ordinary, t);
            }

            return bend;
        }

        internal static float ResolveBusRouteSpeedCapMetersPerSecond(
            int segmentIndex,
            float routeSeparationMeters,
            bool routeRejoinActive = false)
        {
            if (routeRejoinActive ||
                routeSeparationMeters > BusRouteCommitCorridorMeters)
            {
                return BusOffRouteRejoinSpeedMetersPerSecond;
            }

            if (segmentIndex < BusWastewaterBendPreviewStartPointIndex ||
                segmentIndex > BusWastewaterBendPreviewEndPointIndex)
            {
                return float.PositiveInfinity;
            }

            const float approachSpeedMetersPerSecond = 18f;
            const float bendSpeedMetersPerSecond = 10f;
            if (segmentIndex < BusWastewaterBendEntryPointIndex)
            {
                float t = Mathf.InverseLerp(
                    BusWastewaterBendPreviewStartPointIndex,
                    BusWastewaterBendEntryPointIndex,
                    segmentIndex);
                return Mathf.Lerp(
                    approachSpeedMetersPerSecond,
                    bendSpeedMetersPerSecond,
                    t);
            }

            if (segmentIndex > BusWastewaterBendExitPointIndex)
            {
                float t = Mathf.InverseLerp(
                    BusWastewaterBendExitPointIndex,
                    BusWastewaterBendPreviewEndPointIndex,
                    segmentIndex);
                return Mathf.Lerp(
                    bendSpeedMetersPerSecond,
                    approachSpeedMetersPerSecond,
                    t);
            }

            return bendSpeedMetersPerSecond;
        }

        internal static float ResolveHorizontalHeadingDot(
            Quaternion physicalRotation,
            Quaternion routeRotation)
        {
            Vector3 physicalForward = Vector3.ProjectOnPlane(
                physicalRotation * Vector3.forward,
                Vector3.up);
            Vector3 routeForward = Vector3.ProjectOnPlane(
                routeRotation * Vector3.forward,
                Vector3.up);
            if (physicalForward.sqrMagnitude <= 0.0001f ||
                routeForward.sqrMagnitude <= 0.0001f)
            {
                return -1f;
            }

            return Mathf.Clamp(
                Vector3.Dot(
                    physicalForward.normalized,
                    routeForward.normalized),
                -1f,
                1f);
        }

        internal static Quaternion ResolveBusMaterializationRotation(
            Quaternion savedRotation,
            Quaternion routeRotation) =>
            ResolveHorizontalHeadingDot(savedRotation, routeRotation) <=
                BusSavedPoseMinimumForwardHeadingDot
                ? routeRotation
                : savedRotation;

        internal static bool ResolveBusRouteRejoinActive(
            bool wasActive,
            float routeSeparationMeters,
            float horizontalHeadingDot)
        {
            if (wasActive)
            {
                return routeSeparationMeters >
                           BusRouteCommitCorridorMeters ||
                       horizontalHeadingDot <=
                           BusRouteRejoinExitHeadingDot;
            }

            return routeSeparationMeters > BusRouteCommitCorridorMeters ||
                   horizontalHeadingDot < BusRouteRejoinEnterHeadingDot;
        }

        internal static bool CanCommitBusPhysicalProjection(
            bool routeRejoinActive,
            bool insideRouteCorridor,
            bool projectionCommitAllowed) =>
            !routeRejoinActive &&
            insideRouteCorridor &&
            projectionCommitAllowed;

        internal static bool IsBusHillDriveAssistSegment(int segmentIndex) =>
            segmentIndex >= BusHillDriveAssistStartPointIndex &&
            segmentIndex <= BusHillDriveAssistEndPointIndex;

        private void FixedUpdateTransports()
        {
            float scaledRealDelta = Mathf.Max(0f, Time.fixedDeltaTime) *
                                    GameSecondsPerSimulationSecond;
            foreach (TransportRuntimeState state in transportStates.Values)
            {
                if (!state.Active)
                {
                    continue;
                }

                if (state.Definition.Kind == TrafficTransportKind.Train)
                {
                    // AdvanceTrain stores dwell in the existing scaled save
                    // field, so multiplying real seconds by 12 preserves the
                    // donor's 30 m/s and 250 real-second endpoint wait.
                    AdvanceTrain(state, scaledRealDelta);
                }
                else if (state.Definition.Kind == TrafficTransportKind.Bus)
                {
                    // Bus stop definitions contain donor real seconds. The
                    // persisted field keeps its historical scaled-unit format.
                    DecrementDwell(state, scaledRealDelta);
                }
            }

            foreach (KeyValuePair<string,
                         TrafficTransportPresentationBinding> pair in
                     transportPresentations)
            {
                if (pair.Value == null ||
                    !transportStates.TryGetValue(
                        pair.Key,
                        out TransportRuntimeState state))
                {
                    continue;
                }

                TrafficTransportDefinition definition = state.Definition;
                TrafficTransportPresentationBinding binding = pair.Value;
                if (!state.Active)
                {
                    if (definition.Kind == TrafficTransportKind.Bus)
                    {
                        binding.RoadMotion.SetHillDriveAssist(false);
                        binding.RoadMotion.SetServiceStopHold(true);
                    }
                    else if (binding.Body != null &&
                             !binding.Body.isKinematic)
                    {
                        binding.Body.linearVelocity = Vector3.zero;
                        binding.Body.angularVelocity = Vector3.zero;
                    }

                    continue;
                }

                if (definition.Kind == TrafficTransportKind.Train)
                {
                    TrafficRouteSample sample = ResolveTransportSample(state);
                    binding.SetKinematicPose(sample.Position, sample.Rotation);
                    continue;
                }

                if (definition.Kind == TrafficTransportKind.Bus)
                {
                    float realFixedDeltaSeconds = Mathf.Max(
                        0f,
                        Time.fixedUnscaledDeltaTime);
                    state.CurrentSpeedMetersPerSecond =
                        binding.RoadMotion.CurrentSpeedMetersPerSecond;
                    AdvanceBusTerminalAbandonment(
                        state,
                        binding,
                        realFixedDeltaSeconds);
                    if (state.BusAbandonmentPhase >=
                        BusTerminalAbandonmentPhase.ShutdownDelay)
                    {
                        binding.RoadMotion.SetHillDriveAssist(false);
                        binding.RoadMotion.SetServiceStopHold(true);
                        state.CurrentSpeedMetersPerSecond = 0f;
                        RetainPhysicalTrafficCell(
                            "transport:" + pair.Key + ":current",
                            binding.RoadMotion.PhysicalWorldPosition);
                        if (state.BusAbandonmentPhase ==
                            BusTerminalAbandonmentPhase.Abandoned)
                        {
                            bool cursePlayed =
                                binding.AdvanceAbandonedBusDriver(
                                    realFixedDeltaSeconds,
                                    player);
                            CaptureBusDriverPresentation(state, binding);
                            if (cursePlayed)
                            {
                                busDriverSubtitleFeedback?.Invoke(
                                    BusDriverStuckCurseLineId,
                                    BusDriverStuckCurseFallbackSubtitle,
                                    state.BusDriverWorldPosition);
                            }
                            RetainPhysicalTrafficCell(
                                "transport:" + pair.Key + ":driver",
                                state.BusDriverWorldPosition);
                        }

                        continue;
                    }

                    Vector3 physicalPosition =
                        binding.RoadMotion.PhysicalWorldPosition;
                    RetainPhysicalTrafficCell(
                        "transport:" + pair.Key + ":current",
                        physicalPosition);
                    int hint = state.SegmentHint;
                    TrafficRouteGeometry geometry = routes[state.RouteId];
                    int retainedSegment = geometry.Resolve(
                            state.RouteProgress01,
                            true)
                        .SegmentIndex;
                    binding.RoadMotion.SetHillDriveAssist(
                        IsBusHillDriveAssistSegment(retainedSegment));
                    float lookAhead = ResolveBusLookAheadMeters(
                        retainedSegment,
                        binding.RoadMotion.CurrentSpeedMetersPerSecond);
                    binding.RoadMotion.SetRouteSpeedCap(
                        float.PositiveInfinity);
                    if (geometry.TryProjectAndResolveAhead(
                            physicalPosition,
                            state.RouteProgress01,
                            true,
                            lookAhead,
                            ref hint,
                            out TrafficRouteSample projection,
                            out TrafficRouteSample guidance))
                    {
                        float previous = state.RouteProgress01;
                        state.SegmentHint = hint;
                        binding.RoadMotion.SetHillDriveAssist(
                            IsBusHillDriveAssistSegment(
                                projection.SegmentIndex));
                        float projectionSeparation = Vector3.Distance(
                            physicalPosition,
                            projection.Position);
                        float headingDot = ResolveHorizontalHeadingDot(
                            binding.RoadMotion.PhysicalWorldRotation,
                            projection.Rotation);
                        bool insideRouteCorridor = projectionSeparation <=
                                                   BusRouteCommitCorridorMeters;
                        bool wasRouteRejoining =
                            binding.RoadMotion.IsRouteRejoinActive;
                        bool routeRejoinActive =
                            ResolveBusRouteRejoinActive(
                                wasRouteRejoining,
                                projectionSeparation,
                                headingDot);
                        bool projectionCommitAllowed =
                            geometry.CanCommitPhysicalProjection(
                                state.RouteProgress01,
                                projection.Progress01,
                                forward: true);
                        if (CanCommitBusPhysicalProjection(
                                routeRejoinActive,
                                insideRouteCorridor,
                                projectionCommitAllowed))
                        {
                            state.RouteProgress01 = projection.Progress01;
                            if (state.RouteProgress01 + 0.5f < previous)
                            {
                                state.CompletedTrips++;
                                state.NextStopIndex = ResolveNextStopIndex(state);
                            }
                        }

                        if (routeRejoinActive)
                        {
                            // Pursue a short target on the donor centreline.
                            // This is also used when the body is centred but
                            // facing against BusRoute. Continuing to look 20+
                            // metres ahead in either case makes the long body
                            // cross the opposing lane and can commit the wrong
                            // branch before it has physically recovered.
                            float rejoinProgress = geometry.AdvanceProgress(
                                projection.Progress01,
                                true,
                                BusOffRouteRejoinPreviewMeters,
                                out _);
                            guidance = geometry.Resolve(
                                rejoinProgress,
                                true);
                            if (!wasRouteRejoining ||
                                Mathf.Abs(
                                    binding.RoadMotion.CurrentLaneOffsetMeters -
                                    binding.RoadMotion.BaseLaneOffsetMeters) >
                                0.2f)
                            {
                                binding.RoadMotion.ResetRoadLaneStateToBase();
                            }
                        }

                        binding.RoadMotion.SetRouteRejoinActive(
                            routeRejoinActive);

                        binding.RoadMotion.SetRouteSpeedCap(
                            ResolveBusRouteSpeedCapMetersPerSecond(
                                projection.SegmentIndex,
                                projectionSeparation,
                                routeRejoinActive));
                        state.CurrentSpeedMetersPerSecond =
                            binding.RoadMotion.CurrentSpeedMetersPerSecond;
                        if (state.DwellGameSecondsRemaining <= 0f)
                        {
                            TryEnterBusStop(
                                state,
                                previous,
                                state.RouteProgress01);
                        }

                        binding.RoadMotion.SetServiceStopHold(
                            state.DwellGameSecondsRemaining > 0f ||
                            state.BusRecoveryExhausted);
                        binding.RoadMotion.SetPhysicalRouteGuidanceTarget(
                            guidance.Position,
                            guidance.Rotation,
                            state.RouteProgress01);
                        float preloadDistanceMeters = Mathf.Max(
                            96f,
                            binding.RoadMotion.CurrentSpeedMetersPerSecond *
                            3.5f);
                        float preloadProgress = geometry.AdvanceProgress(
                            state.RouteProgress01,
                            true,
                            preloadDistanceMeters,
                            out _);
                        TrafficRouteSample preload = geometry.Resolve(
                            preloadProgress,
                            true);
                        RetainPhysicalTrafficCell(
                            "transport:" + pair.Key + ":ahead",
                            preload.Position);
                    }

                    continue;
                }

                if (IsBoatTwo(definition) && !state.UsingSecondaryRoute)
                {
                    TrafficRouteGeometry commonGeometry =
                        routes[definition.SecondaryRouteId];
                    int joinIndex = Mathf.Clamp(
                        state.NextStopIndex,
                        0,
                        commonGeometry.PointCount - 1);
                    float joinProgress =
                        commonGeometry.ProgressAtPointIndex(joinIndex);
                    TrafficRouteSample joinTarget = commonGeometry.Resolve(
                        joinProgress,
                        true);
                    state.CurrentSpeedMetersPerSecond =
                        binding.Body.linearVelocity.magnitude;
                    binding.DriveBoatTowards(
                        joinTarget.Position,
                        joinTarget.Rotation,
                        definition.SpeedMetersPerSecond);
                    if ((binding.transform.position - joinTarget.Position)
                        .sqrMagnitude <=
                        BoatJoinDistanceMeters * BoatJoinDistanceMeters)
                    {
                        state.UsingSecondaryRoute = true;
                        state.RouteId = definition.SecondaryRouteId;
                        state.RouteProgress01 = joinProgress;
                        state.SegmentHint = joinIndex;
                    }

                    continue;
                }

                TrafficRouteGeometry boatGeometry = routes[state.RouteId];
                int boatHint = state.SegmentHint;
                if (boatGeometry.TryProjectAndResolveAhead(
                        binding.transform.position,
                        state.RouteProgress01,
                        true,
                        22f,
                        ref boatHint,
                        out TrafficRouteSample boatProjection,
                        out TrafficRouteSample boatGuidance))
                {
                    float previous = state.RouteProgress01;
                    state.SegmentHint = boatHint;
                    state.RouteProgress01 = boatProjection.Progress01;
                    if (boatProjection.Progress01 + 0.5f < previous)
                    {
                        state.CompletedTrips++;
                    }

                    state.CurrentSpeedMetersPerSecond =
                        binding.Body.linearVelocity.magnitude;
                    binding.DriveBoatTowards(
                        boatGuidance.Position,
                        boatGuidance.Rotation,
                        definition.SpeedMetersPerSecond);
                }
            }
        }

        private void AdvanceBusTerminalAbandonment(
            TransportRuntimeState state,
            TrafficTransportPresentationBinding binding,
            float realDeltaSeconds)
        {
            if (!binding.SupportsBusTerminalAbandonment)
            {
                // A pre-terminal-abandonment generated bus wrapper is allowed
                // to keep the existing route playable until the deterministic
                // Story Traffic importer is rerun. Do not arm a terminal state
                // that the legacy wrapper cannot present. A terminal state
                // restored from save remains authoritative and stopped.
                if (state.BusAbandonmentPhase <
                    BusTerminalAbandonmentPhase.ShutdownDelay)
                {
                    ResetBusAbandonmentForNewDeparture(state);
                }

                return;
            }

            TrafficRouteGeometry geometry = routes[state.RouteId];
            int recoveryCount = binding.RoadMotion.RecoveryCount;
            if (!state.BusHasForwardProgressAnchor)
            {
                state.BusHasForwardProgressAnchor = true;
                state.BusForwardProgressAnchor01 = state.RouteProgress01;
                state.BusForwardProgressAnchorCompletedTrips =
                    state.CompletedTrips;
                state.BusRecoveryCountAtForwardProgress = recoveryCount;
            }

            float forwardProgressMeters = TrafficTransportBehaviorRules
                .ResolveBusForwardProgressMeters(
                    state.BusForwardProgressAnchor01,
                    state.BusForwardProgressAnchorCompletedTrips,
                    state.RouteProgress01,
                    state.CompletedTrips,
                    geometry.TotalLengthMeters);
            bool meaningfulForwardProgress = forwardProgressMeters >=
                                             TrafficTransportBehaviorRules
                                                 .BusMeaningfulForwardProgressMeters;
            if (meaningfulForwardProgress)
            {
                state.BusForwardProgressAnchor01 = state.RouteProgress01;
                state.BusForwardProgressAnchorCompletedTrips =
                    state.CompletedTrips;
                state.BusRecoveryCountAtForwardProgress = recoveryCount;
                state.BusRecoveryEpisodeActive = false;
                state.BusRecoveryExhausted = false;
            }
            else if (recoveryCount > state.BusRecoveryCountAtForwardProgress)
            {
                state.BusRecoveryEpisodeActive = true;
                state.BusRecoveryExhausted = TrafficTransportBehaviorRules
                    .IsBusRecoveryExhausted(
                        state.BusRecoveryCountAtForwardProgress,
                        recoveryCount);
            }

            var previous = new BusTerminalAbandonmentState(
                state.BusAbandonmentPhase,
                state.BusStallMonitorArmed,
                state.BusStuckRealSeconds,
                state.BusShutdownDelayRealSecondsRemaining);
            BusTerminalAbandonmentState next =
                TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                    previous,
                    binding.RoadMotion.CurrentSpeedMetersPerSecond,
                    state.DwellGameSecondsRemaining > 0f ||
                    binding.RoadMotion
                        .HasNonServiceIntentionalStationaryHold ||
                    !state.BusRecoveryExhausted &&
                    binding.RoadMotion.HasIntentionalStationaryHold,
                    realDeltaSeconds,
                    meaningfulForwardProgress,
                    state.BusRecoveryEpisodeActive);
            state.BusAbandonmentPhase = next.Phase;
            state.BusStallMonitorArmed = next.MonitorArmed;
            state.BusStuckRealSeconds = next.StuckRealSeconds;
            state.BusShutdownDelayRealSecondsRemaining =
                next.ShutdownDelayRealSecondsRemaining;
            ApplyBusAbandonmentPresentation(state, binding);
        }

        private void ApplyBusAbandonmentPresentation(
            TransportRuntimeState state,
            TrafficTransportPresentationBinding binding)
        {
            binding.ApplyBusAbandonmentPhase(
                state.BusAbandonmentPhase,
                state.BusShutdownDelayRealSecondsRemaining,
                state.HasBusDriverPose,
                state.BusDriverWorldPosition,
                state.BusDriverWorldRotation);
            if (transportAudioOwners.TryGetValue(
                    state.Definition.TransportId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null)
            {
                audio.SetTerminallyDisabled(
                    state.BusAbandonmentPhase >=
                    BusTerminalAbandonmentPhase.ShutdownDelay);
            }
        }

        private static void CaptureBusDriverPresentation(
            TransportRuntimeState state,
            TrafficTransportPresentationBinding binding)
        {
            if (!binding.HasBusDriverWorldPose)
            {
                return;
            }

            state.HasBusDriverPose = true;
            state.BusDriverWorldPosition = binding.BusDriverWorldPosition;
            state.BusDriverWorldRotation = binding.BusDriverWorldRotation;
            state.BusDriverCurseCooldownRealSeconds =
                binding.DriverCurseCooldownRealSeconds;
        }

        private Vector3 ResolveSupportedTransportSpawnPosition(
            TransportRuntimeState state)
        {
            TrafficRouteSample sample = ResolveTransportSample(state);
            if (state.HasPhysicalPose &&
                (TrafficTransportBehaviorRules
                     .IsTerminalBusPhysicalPoseAuthoritative(
                         state.BusAbandonmentPhase) ||
                 IsPhysicalPoseCompatibleWithRoute(
                     state.WorldPosition,
                     sample.Position)))
            {
                return state.WorldPosition;
            }

            state.HasPhysicalPose = false;
            return state.Definition.Kind == TrafficTransportKind.Bus
                ? ResolveLanePosition(sample, 0f)
                : sample.Position;
        }

        private void CaptureTransportPhysical(
            TransportRuntimeState state,
            TrafficTransportPresentationBinding binding)
        {
            state.HasPhysicalPose = true;
            if (binding.Kind == TrafficTransportKind.Bus &&
                binding.RoadMotion != null)
            {
                state.WorldPosition = binding.RoadMotion.PhysicalWorldPosition;
                state.WorldRotation = binding.RoadMotion.PhysicalWorldRotation;
                StoryTrafficMotionRuntimeState motion =
                    binding.RoadMotion.CaptureRuntimeState();
                if (motion.HasPhysicalRouteProgress)
                {
                    state.RouteProgress01 =
                        motion.LastPhysicalRouteProgress01;
                }

                state.CurrentSpeedMetersPerSecond =
                    binding.RoadMotion.CurrentSpeedMetersPerSecond;
                CaptureBusDriverPresentation(state, binding);
            }
            else if (binding.Body != null)
            {
                state.WorldPosition = binding.Body.position;
                state.WorldRotation = binding.Body.rotation;
                state.CurrentSpeedMetersPerSecond =
                    binding.Body.linearVelocity.magnitude;
            }
            else
            {
                state.WorldPosition = binding.transform.position;
                state.WorldRotation = binding.transform.rotation;
            }
        }

        private void RemoveTransportPresentation(string transportId)
        {
            if (!transportPresentations.TryGetValue(
                    transportId,
                    out TrafficTransportPresentationBinding binding))
            {
                return;
            }

            if (binding != null && binding.PassengerAboard)
            {
                return;
            }

            transportPresentations.Remove(transportId);
            if (binding != null && transportStates.TryGetValue(
                    transportId,
                    out TransportRuntimeState state))
            {
                CaptureTransportPhysical(state, binding);
            }

            if (transportAudioOwners.TryGetValue(
                    transportId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null && binding != null && binding.RoadMotion != null)
            {
                audio.UnbindMotion(binding.RoadMotion);
            }

            if (binding != null)
            {
                Destroy(binding.gameObject);
            }
        }

        private void RemoveAllTransportPresentations()
        {
            foreach (string transportId in transportPresentations.Keys.ToArray())
            {
                TrafficTransportPresentationBinding binding =
                    transportPresentations[transportId];
                if (binding != null)
                {
                    binding.PassengerAboard = false;
                }

                RemoveTransportPresentation(transportId);
            }
        }

        private void RemoveAllTransportAudioOwners()
        {
            foreach (StoryTrafficVehicleAudioPresenter audio in
                     transportAudioOwners.Values)
            {
                if (audio != null)
                {
                    Destroy(audio.gameObject);
                }
            }

            transportAudioOwners.Clear();
        }

        private TrafficRouteSample ResolveTransportSample(
            TransportRuntimeState state) =>
            routes[state.RouteId].Resolve(state.RouteProgress01, true);

        private void UpdateTransportEligibility(float realDeltaSeconds)
        {
            if (!transportStates.TryGetValue(
                    BoatTwoTransportId,
                    out TransportRuntimeState state) ||
                player == null)
            {
                return;
            }

            state.DwellGameSecondsRemaining = Mathf.Max(
                0f,
                state.DwellGameSecondsRemaining -
                realDeltaSeconds * GameSecondsPerSimulationSecond);
            if (state.DwellGameSecondsRemaining > 0f)
            {
                return;
            }

            state.DwellGameSecondsRemaining =
                BoatEligibilityPollRealSeconds *
                GameSecondsPerSimulationSecond;
            GameTimeSnapshot snapshot = gameTime.Snapshot;
            TrafficRouteSample dormantSample = routes[
                    state.Definition.PrimaryRouteId]
                .Resolve(state.RouteProgress01, true);
            Vector3 boatPosition = state.Active &&
                                   transportPresentations.TryGetValue(
                                       BoatTwoTransportId,
                                       out TrafficTransportPresentationBinding
                                           presentation) &&
                                   presentation != null
                ? presentation.transform.position
                : dormantSample.Position;
            bool eligible = TrafficTransportBehaviorRules.IsBoatTwoEligible(
                snapshot.DayIndex,
                snapshot.IsDaylight,
                player.position,
                boatPosition);
            if (eligible == state.Active)
            {
                return;
            }

            if (eligible)
            {
                state.Active = true;
                state.CurrentSpeedMetersPerSecond =
                    state.Definition.SpeedMetersPerSecond;
                state.HasPhysicalPose = false;
                return;
            }

            state.Active = false;
            state.CurrentSpeedMetersPerSecond = 0f;
            state.CompletedTrips++;
            PrepareBoatTwoDormant(
                state,
                snapshot,
                BoatEligibilityPollRealSeconds);
        }

        private void PrepareBoatTwoDormant(
            TransportRuntimeState state,
            GameTimeSnapshot snapshot,
            float nextCheckRealSeconds)
        {
            TrafficRouteGeometry spawnGeometry =
                routes[state.Definition.PrimaryRouteId];
            int startIndex =
                TrafficTransportBehaviorRules.ResolveBoatTwoStartPointIndex(
                    snapshot.DayIndex,
                    state.CompletedTrips,
                    spawnGeometry.PointCount);
            state.Active = false;
            state.UsingSecondaryRoute = false;
            state.RouteId = state.Definition.PrimaryRouteId;
            state.NextStopIndex = startIndex;
            state.RouteProgress01 =
                spawnGeometry.ProgressAtPointIndex(startIndex);
            state.SegmentHint = startIndex;
            state.HasPhysicalPose = false;
            state.DwellGameSecondsRemaining = Mathf.Max(
                0f,
                nextCheckRealSeconds * GameSecondsPerSimulationSecond);
        }

        private static bool IsBoatTwo(TrafficTransportDefinition definition) =>
            definition != null &&
            string.Equals(
                definition.TransportId,
                BoatTwoTransportId,
                StringComparison.Ordinal);

        private void CreateTransportAudioOwner(TransportRuntimeState state)
        {
            var owner = new GameObject(
                "Traffic_Transport_Audio_" + state.Definition.TransportId);
            owner.transform.SetParent(transform, false);
            StoryTrafficVehicleAudioPresenter audio =
                owner.AddComponent<StoryTrafficVehicleAudioPresenter>();
            audio.ConfigurePersistent(
                audioBackendComponent,
                state.Definition.TransportId,
                player,
                "petteri");
            TrafficRouteSample sample = ResolveTransportSample(state);
            audio.SetLogicalPose(sample.Position, sample.Rotation, state.Active);
            audio.SetTerminallyDisabled(
                state.BusAbandonmentPhase >=
                BusTerminalAbandonmentPhase.ShutdownDelay);
            transportAudioOwners.Add(state.Definition.TransportId, audio);
        }

        private void ReconcileTransportAudioOwners()
        {
            foreach (TrafficTransportDefinition definition in catalog.Transports)
            {
                if (!transportAudioOwners.TryGetValue(
                        definition.TransportId,
                        out StoryTrafficVehicleAudioPresenter audio) ||
                    audio == null)
                {
                    continue;
                }

                TransportRuntimeState state =
                    transportStates[definition.TransportId];
                TrafficRouteSample sample = ResolveTransportSample(state);
                audio.SetLogicalPose(
                    state.HasPhysicalPose ? state.WorldPosition : sample.Position,
                    state.HasPhysicalPose ? state.WorldRotation : sample.Rotation,
                    state.Active);
                audio.SetTerminallyDisabled(
                    state.BusAbandonmentPhase >=
                    BusTerminalAbandonmentPhase.ShutdownDelay);
            }
        }

        private static TrafficBusDepartureDefinition FindDeparture(
            TrafficTransportDefinition definition,
            int absoluteHour)
        {
            int hourOfDay = absoluteHour % 24;
            if (hourOfDay < 0)
            {
                hourOfDay += 24;
            }

            return definition.BusDepartures.FirstOrDefault(value =>
                value.Hour == hourOfDay);
        }

        private static TrafficBusDepartureDefinition
            FindLatestDepartureAtOrBefore(
                TrafficTransportDefinition definition,
                GameTimeSnapshot snapshot,
                out int departureAbsoluteHour)
        {
            int currentAbsoluteHour = AbsoluteHour(snapshot);
            for (int offset = 0; offset < 24; offset++)
            {
                int candidateAbsoluteHour = currentAbsoluteHour - offset;
                TrafficBusDepartureDefinition departure = FindDeparture(
                    definition,
                    candidateAbsoluteHour);
                if (departure != null)
                {
                    departureAbsoluteHour = candidateAbsoluteHour;
                    return departure;
                }
            }

            departureAbsoluteHour = int.MinValue;
            return null;
        }

        private static int AbsoluteHour(GameTimeSnapshot snapshot)
        {
            long value = snapshot.DayIndex * 24L +
                         (long)Math.Floor(snapshot.SecondsOfDay / 3600d);
            return value > int.MaxValue
                ? int.MaxValue
                : value < int.MinValue
                    ? int.MinValue
                    : (int)value;
        }

        private static bool IsDefinitionRoute(
            TrafficTransportDefinition definition,
            string routeId) =>
            string.Equals(
                definition.PrimaryRouteId,
                routeId,
                StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(definition.SecondaryRouteId) &&
            string.Equals(
                definition.SecondaryRouteId,
                routeId,
                StringComparison.Ordinal);

        private sealed class TransportRuntimeState
        {
            private TransportRuntimeState(TrafficTransportDefinition definition)
            {
                Definition = definition;
                RouteId = definition.PrimaryRouteId;
            }

            public TrafficTransportDefinition Definition { get; }
            public string RouteId { get; set; }
            public float RouteProgress01 { get; set; }
            public bool Active { get; set; }
            public bool UsingSecondaryRoute { get; set; }
            public int CompletedTrips { get; set; }
            public int LastDepartureAbsoluteHour { get; set; } = int.MinValue;
            public float DwellGameSecondsRemaining { get; set; }
            public int NextStopIndex { get; set; }
            public int SegmentHint { get; set; } = -1;
            public bool HasPhysicalPose { get; set; }
            public Vector3 WorldPosition { get; set; }
            public Quaternion WorldRotation { get; set; } = Quaternion.identity;
            public float CurrentSpeedMetersPerSecond { get; set; }
            public BusTerminalAbandonmentPhase BusAbandonmentPhase { get; set; }
            public bool BusStallMonitorArmed { get; set; }
            public float BusStuckRealSeconds { get; set; }
            public float BusShutdownDelayRealSecondsRemaining { get; set; }
            public bool BusHasForwardProgressAnchor { get; set; }
            public float BusForwardProgressAnchor01 { get; set; }
            public int BusForwardProgressAnchorCompletedTrips { get; set; }
            public int BusRecoveryCountAtForwardProgress { get; set; }
            public bool BusRecoveryEpisodeActive { get; set; }
            public bool BusRecoveryExhausted { get; set; }
            public bool HasBusDriverPose { get; set; }
            public Vector3 BusDriverWorldPosition { get; set; }
            public Quaternion BusDriverWorldRotation { get; set; } =
                Quaternion.identity;
            public float BusDriverCurseCooldownRealSeconds { get; set; }

            public static TransportRuntimeState CreateInitial(
                TrafficTransportDefinition definition) =>
                new(definition)
                {
                    Active = definition.Kind != TrafficTransportKind.Bus,
                    CurrentSpeedMetersPerSecond = definition.Kind ==
                        TrafficTransportKind.Bus
                            ? 0f
                            : definition.SpeedMetersPerSecond,
                };

            public static TransportRuntimeState FromDto(
                TrafficTransportDefinition definition,
                TransportTrafficActorStateDto dto) =>
                new(definition)
                {
                    RouteId = dto.routeId,
                    RouteProgress01 = dto.routeProgress01,
                    Active = dto.active,
                    UsingSecondaryRoute = dto.usingSecondaryRoute,
                    CompletedTrips = dto.completedTrips,
                    LastDepartureAbsoluteHour = dto.lastDepartureAbsoluteHour,
                    DwellGameSecondsRemaining = dto.dwellGameSecondsRemaining,
                    NextStopIndex = dto.nextStopIndex,
                    HasPhysicalPose = dto.hasPhysicalPose,
                    WorldPosition = dto.worldPosition,
                    WorldRotation = dto.worldRotation,
                    CurrentSpeedMetersPerSecond =
                        dto.currentSpeedMetersPerSecond,
                    BusAbandonmentPhase =
                        (BusTerminalAbandonmentPhase)
                        dto.busAbandonmentPhase,
                    BusStallMonitorArmed = dto.busStallMonitorArmed,
                    BusStuckRealSeconds = dto.busStuckRealSeconds,
                    BusShutdownDelayRealSecondsRemaining =
                        dto.busShutdownDelayRealSecondsRemaining,
                    BusHasForwardProgressAnchor =
                        dto.busHasForwardProgressAnchor,
                    BusForwardProgressAnchor01 =
                        dto.busForwardProgressAnchor01,
                    BusForwardProgressAnchorCompletedTrips =
                        dto.busForwardProgressAnchorCompletedTrips,
                    BusRecoveryCountAtForwardProgress =
                        dto.busRecoveryCountAtForwardProgress,
                    BusRecoveryEpisodeActive =
                        dto.busRecoveryEpisodeActive,
                    BusRecoveryExhausted = dto.busRecoveryExhausted,
                    HasBusDriverPose = dto.hasBusDriverPose,
                    BusDriverWorldPosition = dto.busDriverWorldPosition,
                    BusDriverWorldRotation = dto.busDriverWorldRotation,
                    BusDriverCurseCooldownRealSeconds =
                        dto.busDriverCurseCooldownRealSeconds,
                };

            public TransportTrafficActorStateDto ToDto() =>
                new()
                {
                    transportId = Definition.TransportId,
                    stableInstanceId = Definition.StableInstanceId,
                    kind = (int)Definition.Kind,
                    routeId = RouteId,
                    routeProgress01 = Mathf.Clamp01(RouteProgress01),
                    active = Active,
                    usingSecondaryRoute = UsingSecondaryRoute,
                    completedTrips = Mathf.Max(0, CompletedTrips),
                    lastDepartureAbsoluteHour = LastDepartureAbsoluteHour,
                    dwellGameSecondsRemaining = Mathf.Max(
                        0f,
                        DwellGameSecondsRemaining),
                    nextStopIndex = Mathf.Max(0, NextStopIndex),
                    hasPhysicalPose = HasPhysicalPose,
                    worldPosition = WorldPosition,
                    worldRotation = WorldRotation,
                    currentSpeedMetersPerSecond = Mathf.Max(
                        0f,
                        CurrentSpeedMetersPerSecond),
                    busAbandonmentPhase = (int)BusAbandonmentPhase,
                    busStallMonitorArmed = BusStallMonitorArmed,
                    busStuckRealSeconds = Mathf.Max(
                        0f,
                        BusStuckRealSeconds),
                    busShutdownDelayRealSecondsRemaining = Mathf.Max(
                        0f,
                        BusShutdownDelayRealSecondsRemaining),
                    busHasForwardProgressAnchor = BusHasForwardProgressAnchor,
                    busForwardProgressAnchor01 = Mathf.Clamp01(
                        BusForwardProgressAnchor01),
                    busForwardProgressAnchorCompletedTrips = Mathf.Max(
                        0,
                        BusForwardProgressAnchorCompletedTrips),
                    busRecoveryCountAtForwardProgress = Mathf.Max(
                        0,
                        BusRecoveryCountAtForwardProgress),
                    busRecoveryEpisodeActive = BusRecoveryEpisodeActive,
                    busRecoveryExhausted = BusRecoveryExhausted,
                    hasBusDriverPose = HasBusDriverPose,
                    busDriverWorldPosition = BusDriverWorldPosition,
                    busDriverWorldRotation = BusDriverWorldRotation,
                    busDriverCurseCooldownRealSeconds = Mathf.Max(
                        0f,
                        BusDriverCurseCooldownRealSeconds),
                };

            public static bool TryValidateDto(
                TrafficTransportDefinition definition,
                TransportTrafficActorStateDto dto,
                out string failure)
            {
                if (definition == null || dto == null ||
                    !float.IsFinite(dto.routeProgress01) ||
                    dto.routeProgress01 < 0f || dto.routeProgress01 > 1f ||
                    dto.completedTrips < 0 ||
                    !float.IsFinite(dto.dwellGameSecondsRemaining) ||
                    dto.dwellGameSecondsRemaining < 0f ||
                    dto.nextStopIndex < 0 ||
                    !float.IsFinite(dto.currentSpeedMetersPerSecond) ||
                    dto.currentSpeedMetersPerSecond < 0f ||
                    !Enum.IsDefined(
                        typeof(BusTerminalAbandonmentPhase),
                        dto.busAbandonmentPhase) ||
                    !float.IsFinite(dto.busStuckRealSeconds) ||
                    dto.busStuckRealSeconds < 0f ||
                    !float.IsFinite(
                        dto.busShutdownDelayRealSecondsRemaining) ||
                     dto.busShutdownDelayRealSecondsRemaining < 0f ||
                    !float.IsFinite(dto.busForwardProgressAnchor01) ||
                    dto.busForwardProgressAnchor01 < 0f ||
                    dto.busForwardProgressAnchor01 > 1f ||
                    dto.busForwardProgressAnchorCompletedTrips < 0 ||
                    dto.busRecoveryCountAtForwardProgress < 0 ||
                    !float.IsFinite(
                        dto.busDriverCurseCooldownRealSeconds) ||
                    dto.busDriverCurseCooldownRealSeconds < 0f ||
                    !IsFinite(dto.worldPosition) ||
                    !IsFinite(dto.worldRotation) ||
                    !IsFinite(dto.busDriverWorldPosition) ||
                    !IsFinite(dto.busDriverWorldRotation) ||
                    !HasValidDefinitionSpecificState(definition, dto))
                {
                    failure = $"Transport actor '{dto?.transportId}' has invalid state.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            private static bool HasValidDefinitionSpecificState(
                TrafficTransportDefinition definition,
                TransportTrafficActorStateDto dto)
            {
                const float tolerance = 0.001f;
                if (definition.Kind != TrafficTransportKind.Bus)
                {
                    return dto.busAbandonmentPhase ==
                               (int)BusTerminalAbandonmentPhase.Monitoring &&
                           !dto.busStallMonitorArmed &&
                           dto.busStuckRealSeconds <= tolerance &&
                           dto.busShutdownDelayRealSecondsRemaining <= tolerance &&
                           !dto.busHasForwardProgressAnchor &&
                           dto.busForwardProgressAnchor01 <= tolerance &&
                           dto.busForwardProgressAnchorCompletedTrips == 0 &&
                           dto.busRecoveryCountAtForwardProgress == 0 &&
                           !dto.busRecoveryEpisodeActive &&
                           !dto.busRecoveryExhausted &&
                           !dto.hasBusDriverPose &&
                           dto.busDriverWorldPosition.sqrMagnitude <=
                           tolerance * tolerance &&
                           Quaternion.Angle(
                               dto.busDriverWorldRotation,
                               Quaternion.identity) <= tolerance &&
                           dto.busDriverCurseCooldownRealSeconds <= tolerance;
                }

                BusTerminalAbandonmentPhase phase =
                    (BusTerminalAbandonmentPhase)dto.busAbandonmentPhase;
                if (dto.hasBusDriverPose &&
                    phase != BusTerminalAbandonmentPhase.Abandoned)
                {
                    return false;
                }

                return phase switch
                {
                    BusTerminalAbandonmentPhase.Monitoring =>
                        dto.busStuckRealSeconds <= tolerance &&
                        dto.busShutdownDelayRealSecondsRemaining <= tolerance,
                    BusTerminalAbandonmentPhase.StuckConfirmation =>
                        dto.busStallMonitorArmed &&
                        dto.busStuckRealSeconds > 0f &&
                        dto.busStuckRealSeconds <
                        TrafficTransportBehaviorRules
                            .BusStuckConfirmationRealSeconds &&
                        dto.busShutdownDelayRealSecondsRemaining <= tolerance,
                    BusTerminalAbandonmentPhase.ShutdownDelay =>
                        dto.busStallMonitorArmed &&
                        dto.busStuckRealSeconds + tolerance >=
                        TrafficTransportBehaviorRules
                            .BusStuckConfirmationRealSeconds &&
                        dto.busShutdownDelayRealSecondsRemaining > 0f &&
                        dto.busShutdownDelayRealSecondsRemaining <=
                        TrafficTransportBehaviorRules
                            .BusDriverExitDelayRealSeconds + tolerance,
                    BusTerminalAbandonmentPhase.Abandoned =>
                        dto.busStallMonitorArmed &&
                        dto.busStuckRealSeconds + tolerance >=
                        TrafficTransportBehaviorRules
                            .BusStuckConfirmationRealSeconds &&
                        dto.busShutdownDelayRealSecondsRemaining <= tolerance,
                    _ => false,
                };
            }

            private static bool IsFinite(Vector3 value) =>
                float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z);

            private static bool IsFinite(Quaternion value) =>
                float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z) &&
                float.IsFinite(value.w) &&
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w > 0.0001f;
        }
    }
}
