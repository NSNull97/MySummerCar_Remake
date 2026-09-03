using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio;
using MSC.Characters;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using MSC.NPC;
using MSC.World.Streaming;
using UnityEngine;

namespace MSC.Traffic
{
    /// <summary>
    /// Project-owned ambient road traffic. The donor's paired road gates switch
    /// one complete physical traffic root at a time; actors in the inactive root
    /// retain their exact physical pose and motion state. No donor controller,
    /// PlayMaker state or hierarchy lookup is used at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class TrafficWorldRuntime : MonoBehaviour, IGameSessionLifetime
    {
        private const float GameSecondsPerSimulationSecond = 12f;
        private const float ReconcileIntervalSeconds = 0.25f;
        private const float MaximumPhysicalRouteSeparationMeters = 80f;
        private const float OrdinaryFittanRejoinMaximumDistanceMeters = 28f;
        private const float OrdinaryFittanRejoinMinimumAlignment = -0.15f;
        private const float OrdinaryFittanRejoinEnterDistanceMeters = 1.65f;
        private const float OrdinaryFittanRejoinExitDistanceMeters = 0.8f;
        private const int OrdinaryFittanFirstDirtRoadPointIndex = 16;
        private const int OrdinaryFittanLastDirtRoadPointIndex = 3718;
        internal const float AmbientMaterializeDistanceMeters = 440f;
        internal const float AmbientDematerializeDistanceMeters = 520f;
        internal const int MaximumMaterializedOrdinaryAmbientActors = 6;
        private const float TrafficGateRearmDistanceMeters = 35f;
        private const float MaximumGateTravelPerFrameMeters = 120f;
        private const string HighwayRouteId = "route.traffic.highway";
        private const string DirtRoadRouteId = "route.traffic.dirt-road";
        private const string TownEntryRouteId =
            "route.traffic.mod-town-entry";
        private const string TownLoopRouteId =
            "route.traffic.mod-town-loop";
        private const string GasPumpRouteId =
            "route.traffic.mod-gas-pump";

        // Route evidence is translated into the project world during import.
        // The donor gate centres must use the same translation; keeping the
        // raw donor coordinates here left both traffic roots dormant because
        // the player could never cross a gate in the active world.
        internal static readonly Vector3 SourceToProjectTrafficTranslation =
            new(169.98f, 1.611f, -1040.625f);

        private readonly Dictionary<string, ActorRuntimeState> states =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, TrafficRouteGeometry> routes =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            StoryTrafficVehiclePresentationBinding> presentations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, StoryTrafficVehicleAudioPresenter>
            audioOwners = new(StringComparer.Ordinal);
        private readonly HashSet<string> desiredMaterialized =
            new(StringComparer.Ordinal);
        private readonly List<AmbientResidencyCandidate>
            ambientResidencyCandidates = new(16);
        private readonly HashSet<string> cellRetentionOwners =
            new(StringComparer.Ordinal);

        // Donor VehiclesHighway / VehiclesDirtroad TriggerManagers. Out enables
        // the complete highway root and In enables the complete dirt-road root.
        // The two planes in a pair are intentionally 5.7 m apart.
        private static readonly AmbientTrafficGatePair[] AmbientTrafficGates =
        {
            new(
                new Vector3(1584.000f, 10.600f, 882.500f) +
                    SourceToProjectTrafficTranslation,
                new Vector3(1585.340f, 10.600f, 888.040f) +
                    SourceToProjectTrafficTranslation,
                -166.408f,
                50f,
                6f),
            new(
                new Vector3(1869.800f, 4.900f, -991.400f) +
                    SourceToProjectTrafficTranslation,
                new Vector3(1870.270f, 4.900f, -997.112f) +
                    SourceToProjectTrafficTranslation,
                -10.717f,
                60f,
                6f),
            new(
                new Vector3(-1606.100f, 5.460f, 1100.800f) +
                    SourceToProjectTrafficTranslation,
                new Vector3(-1609.383f, 5.460f, 1096.114f) +
                    SourceToProjectTrafficTranslation,
                30f,
                26f,
                6f),
            new(
                new Vector3(-1514.500f, 14.900f, -557.800f) +
                    SourceToProjectTrafficTranslation,
                new Vector3(-1517.137f, 14.900f, -552.711f) +
                    SourceToProjectTrafficTranslation,
                146.6f,
                40f,
                6f),
            new(
                new Vector3(1433.500f, -1.500f, 963.600f) +
                    SourceToProjectTrafficTranslation,
                new Vector3(1433.645f, -1.483f, 969.298f) +
                    SourceToProjectTrafficTranslation,
                -178.539f,
                90f,
                9f),
        };

        private TrafficRoadNetworkCatalog catalog;
        private TrafficPresentationCatalog presentationCatalog;
        private IGameTimeService gameTime;
        private IDisposable gameTimeSubscription;
        private Transform player;
        private MonoBehaviour audioBackendComponent;
        private ProductionWorldStreamingService worldStreaming;
        private float reconcileSeconds;
        private AmbientTrafficRoot activeAmbientRoot;
        private Vector3 previousPlayerPosition;
        private Vector3 lockedGateCenter;
        private int lockedGatePairIndex = -1;
        private bool hasPreviousPlayerPosition;
        // Unity hot reload serializes private fields even without
        // [SerializeField], but cannot restore interface-backed session
        // services such as gameTime. Never let a restored true value turn this
        // component into a partially initialized player-loop zombie.
        [NonSerialized] private bool initialized;

        public bool IsInitialized => initialized;
        public int ActorCount => states.Count;
        public int MaterializedActorCount => presentations.Count;

        public void Initialize(
            TrafficRoadNetworkCatalog configuredCatalog,
            TrafficPresentationCatalog configuredPresentationCatalog,
            IGameTimeService configuredGameTime,
            Transform configuredPlayer,
            MonoBehaviour configuredAudioBackend = null,
            ProductionWorldStreamingService configuredWorldStreaming = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Traffic runtime is already initialized.");
            }

            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            presentationCatalog = configuredPresentationCatalog ??
                throw new ArgumentNullException(
                    nameof(configuredPresentationCatalog));
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            player = configuredPlayer ??
                throw new ArgumentNullException(nameof(configuredPlayer));
            previousPlayerPosition = player.position;
            hasPreviousPlayerPosition = true;
            // The donor scene serializes both traffic roots disabled and lets
            // paired gates select one during continuous travel. The project
            // save envelope does not persist that transient root identity,
            // though, so starting at a loaded/authoring position could leave
            // every highway actor dormant forever. Bootstrap the common road
            // root deterministically; exact gates remain authoritative after
            // the session starts.
            activeAmbientRoot = ResolveInitialAmbientRoot();
            lockedGatePairIndex = -1;
            if (!catalog.TryValidate(out string catalogFailure))
            {
                throw new ArgumentException(
                    "Traffic catalog is invalid: " + catalogFailure,
                    nameof(configuredCatalog));
            }

            if (!presentationCatalog.TryValidate(out string presentationFailure))
            {
                throw new ArgumentException(
                    "Traffic presentation catalog is invalid: " +
                    presentationFailure,
                    nameof(configuredPresentationCatalog));
            }

            if (configuredAudioBackend != null &&
                configuredAudioBackend is not IAudioBackend)
            {
                throw new ArgumentException(
                    "Traffic audio backend must implement IAudioBackend.",
                    nameof(configuredAudioBackend));
            }

            audioBackendComponent = configuredAudioBackend;
            worldStreaming = configuredWorldStreaming;
            foreach (TrafficRouteDefinition definition in catalog.Routes)
            {
                routes.Add(
                    definition.RouteId,
                    new TrafficRouteGeometry(definition));
            }
            InitializeTownExcursionGeometry();

            GameTimeSnapshot snapshot = gameTime.Snapshot;
            foreach (TrafficActorDefinition definition in catalog.Actors)
            {
                var state = ActorRuntimeState.CreateInitial(
                    definition,
                    snapshot.DayIndex);
                InitializeCousinState(state, snapshot);
                states.Add(definition.ActorId, state);
                if (audioBackendComponent != null)
                {
                    CreateAudioOwner(definition, state);
                }
            }

            InitializeTransportStates(snapshot);
            InitializeEventStates(snapshot);

            gameTimeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            initialized = true;
            enabled = true;
            // EditMode domain tests exercise the persistent simulation without
            // entering Unity's player loop. In that context prefab Awake has
            // not initialized the NWH backend yet, so physical presentation is
            // neither useful nor valid. Runtime/PlayMode still materializes the
            // selected root immediately and keeps it globally simulated.
            if (Application.isPlaying)
            {
                ReconcileMaterialization();
            }
        }

        public StoryTrafficStateDto AttachAmbientState(
            StoryTrafficStateDto trafficState)
        {
            StoryTrafficStateDto result = trafficState?.DeepClone() ??
                throw new ArgumentNullException(nameof(trafficState));
            result.ambientActors = CaptureAmbientActors();
            result.transportActors = CaptureTransportActors();
            result.eventActors = CaptureEventActors();
            result.eventGroups = CaptureEventGroups();
            return result;
        }

        public AmbientTrafficActorStateDto[] CaptureAmbientActors()
        {
            EnsureInitialized();
            foreach (KeyValuePair<string,
                         StoryTrafficVehiclePresentationBinding> pair in
                     presentations)
            {
                if (pair.Value != null && states.TryGetValue(
                        pair.Key,
                        out ActorRuntimeState state))
                {
                    state.CapturePhysical(pair.Value);
                    TrafficRouteSample logical = ResolveLogicalSample(state);
                    if (!IsPhysicalPoseCompatibleWithRoute(
                            state.WorldPosition,
                            logical.Position))
                    {
                        // Never persist a finite-but-under-map Rigidbody pose.
                        // Route progress remains authoritative and will
                        // rematerialize on supported world collision.
                        state.HasPhysicalPose = false;
                        state.HasSafePose = false;
                    }
                }
            }

            return catalog.Actors
                .Select(definition => states[definition.ActorId].ToDto())
                .ToArray();
        }

        public bool TryValidateAmbientActors(
            IReadOnlyList<AmbientTrafficActorStateDto> actors,
            out string failure)
        {
            failure = string.Empty;
            if (!initialized)
            {
                failure = "Traffic runtime is not initialized.";
                return false;
            }

            IReadOnlyList<AmbientTrafficActorStateDto> source = actors ??
                Array.Empty<AmbientTrafficActorStateDto>();
            if (source.Count == 0)
            {
                // Backward-compatible pre-11B-T1 traffic.state payload.
                failure = string.Empty;
                return true;
            }

            if (source.Count > catalog.Actors.Count)
            {
                failure =
                    $"Ambient traffic save contains {source.Count} actors; catalog has only {catalog.Actors.Count}.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                AmbientTrafficActorStateDto dto = source[index];
                if (dto == null ||
                    !catalog.TryGetActor(dto.actorId, out var definition) ||
                    !ids.Add(dto.actorId) ||
                    !string.Equals(
                        dto.stableInstanceId,
                        definition.StableInstanceId,
                        StringComparison.Ordinal) ||
                    !IsAllowedRuntimeRoute(definition, dto.routeId) ||
                    !ActorRuntimeState.TryValidateDto(dto, out failure))
                {
                    if (string.IsNullOrWhiteSpace(failure))
                    {
                        failure = $"Ambient traffic save row {index} is invalid.";
                    }

                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRestoreAmbientActors(
            IReadOnlyList<AmbientTrafficActorStateDto> actors,
            out string failure)
        {
            if (!TryValidateAmbientActors(actors, out failure))
            {
                return false;
            }

            RemoveAllPresentations();
            IReadOnlyList<AmbientTrafficActorStateDto> source = actors ??
                Array.Empty<AmbientTrafficActorStateDto>();
            states.Clear();
            long day = gameTime.Snapshot.DayIndex;
            foreach (TrafficActorDefinition definition in catalog.Actors)
            {
                ActorRuntimeState initial =
                    ActorRuntimeState.CreateInitial(definition, day);
                InitializeCousinState(initial, gameTime.Snapshot);
                states.Add(
                    definition.ActorId,
                    initial);
            }

            foreach (AmbientTrafficActorStateDto dto in source)
            {
                TrafficActorDefinition definition =
                    catalog.Actors.First(value => string.Equals(
                        value.ActorId,
                        dto.actorId,
                        StringComparison.Ordinal));
                if (string.Equals(
                        definition.ActorId,
                        TrafficCousinBehaviorRules.ActorId,
                        StringComparison.Ordinal) &&
                    dto.cousinStateVersion == 0)
                {
                    // Pre-lifecycle saves used this stable actor for the old
                    // KUSKI fixture. Keep the freshly initialized ordinary
                    // FITTAN state instead of restoring its obsolete pose.
                    continue;
                }

                states[definition.ActorId] =
                    ActorRuntimeState.FromDto(definition, dto);
            }

            ReconcileAllAudioOwners();
            ReconcileMaterialization();
            ReconcileTransportMaterialization();
            failure = string.Empty;
            return true;
        }

        public bool TryGetActorState(
            string actorId,
            out AmbientTrafficActorStateDto state)
        {
            if (states.TryGetValue(
                    actorId ?? string.Empty,
                    out ActorRuntimeState runtimeState))
            {
                state = runtimeState.ToDto();
                return true;
            }

            state = null;
            return false;
        }

        private void Update()
        {
            if (!TryEnterPlayerLoop())
            {
                return;
            }

            bool rootChanged = UpdateAmbientTrafficRoot();
            float realDeltaSeconds = Mathf.Max(0f, Time.deltaTime);
            if (rootChanged)
            {
                // Give a newly selected root one residency pass before its
                // distant members switch from their frozen physical snapshot
                // to logical route authority. Nearby actors therefore resume
                // the exact captured pose; distant actors resume continuously
                // without requiring a physical wrapper.
                ReconcileMaterialization();
            }

            UpdateTransportEligibility(realDeltaSeconds);
            UpdateEventTraffic(realDeltaSeconds);
            UpdateCousinLifecycle(realDeltaSeconds);
            AdvanceLogicalAmbientActors(realDeltaSeconds);
            if (activeAmbientRoot == AmbientTrafficRoot.Highway)
            {
                long dayIndex = gameTime.Snapshot.DayIndex;
                foreach (ActorRuntimeState state in states.Values)
                {
                    AdvanceTownDecision(state, realDeltaSeconds, dayIndex);
                }
            }

            reconcileSeconds += Time.deltaTime;
            if (!rootChanged && reconcileSeconds < ReconcileIntervalSeconds)
            {
                return;
            }

            reconcileSeconds = 0f;
            ReconcileMaterialization();
            ReconcileTransportMaterialization();
            ReconcileAllAudioOwners();
        }

        private void FixedUpdate()
        {
            if (!TryEnterPlayerLoop())
            {
                return;
            }

            foreach (KeyValuePair<string,
                         StoryTrafficVehiclePresentationBinding> pair in
                     presentations)
            {
                StoryTrafficVehiclePresentationBinding motion = pair.Value;
                if (motion == null ||
                    !states.TryGetValue(pair.Key, out ActorRuntimeState state) ||
                    !state.Active ||
                    !routes.TryGetValue(
                        state.CurrentRouteId,
                        out TrafficRouteGeometry geometry))
                {
                    continue;
                }

                if (IsPersistentNamedTrafficActor(state.Definition) ||
                    IsCousinSaturdayContext(state))
                {
                    RetainPhysicalTrafficCell(
                        "ambient:" + pair.Key + ":current",
                        motion.PhysicalWorldPosition);
                }

                bool ordinaryFittan = IsOrdinaryFittanDirtRoute(state);
                bool townExcursionRoute = IsTownExcursionRoute(
                    state.CurrentRouteId);
                float lookAhead = ordinaryFittan
                    ? ResolveOrdinaryFittanLookAheadMeters(
                        motion.CurrentSpeedMetersPerSecond)
                    : townExcursionRoute
                        ? ResolveTownExcursionLookAheadMeters(
                            state.CurrentRouteId,
                            motion.CurrentSpeedMetersPerSecond,
                            motion.IsRouteRejoinActive)
                    : ResolveAmbientLookAheadMeters(
                        motion.CurrentSpeedMetersPerSecond);
                int hint = state.SegmentHint;
                bool projected = ordinaryFittan
                    ? geometry.TryProjectAndResolveAheadWithinClosedPointRange(
                        motion.PhysicalWorldPosition,
                        state.RouteProgress01,
                        state.CurrentTravelsForward,
                        lookAhead,
                        OrdinaryFittanFirstDirtRoadPointIndex,
                        OrdinaryFittanLastDirtRoadPointIndex,
                        ref hint,
                        out TrafficRouteSample projection,
                        out TrafficRouteSample guidance)
                    : geometry.TryProjectAndResolveAhead(
                        motion.PhysicalWorldPosition,
                        state.RouteProgress01,
                        state.CurrentTravelsForward,
                        lookAhead,
                        ref hint,
                        out projection,
                        out guidance);
                if (!projected)
                {
                    continue;
                }

                float initialProjectionSeparation = Vector3.Distance(
                    motion.PhysicalWorldPosition,
                    projection.Position);
                if (ordinaryFittan)
                {
                    bool routeRejoinActive = motion.IsRouteRejoinActive
                        ? initialProjectionSeparation >
                          OrdinaryFittanRejoinExitDistanceMeters
                        : initialProjectionSeparation >
                          OrdinaryFittanRejoinEnterDistanceMeters;
                    bool enteringRouteRejoin = routeRejoinActive &&
                                                !motion.IsRouteRejoinActive;
                    motion.SetRouteRejoinActive(routeRejoinActive);
                    if (enteringRouteRejoin)
                    {
                        motion.ResetRoadLaneStateToBase();
                    }
                }

                bool townRouteRejoinActive = false;
                if (townExcursionRoute)
                {
                    float headingDot = ResolveHorizontalHeadingDot(
                        motion.PhysicalWorldRotation,
                        projection.Rotation);
                    townRouteRejoinActive = ResolveTownRouteRejoinActive(
                        motion.IsRouteRejoinActive,
                        initialProjectionSeparation,
                        headingDot);
                    bool enteringRouteRejoin = townRouteRejoinActive &&
                                                !motion.IsRouteRejoinActive;
                    motion.SetRouteRejoinActive(townRouteRejoinActive);
                    if (enteringRouteRejoin)
                    {
                        motion.ResetRoadLaneStateToBase();
                    }

                    if (townRouteRejoinActive)
                    {
                        float rejoinProgress = geometry.AdvanceProgress(
                            projection.Progress01,
                            state.CurrentTravelsForward,
                            TownRouteRejoinLookAheadMeters,
                            out _);
                        guidance = geometry.Resolve(
                            rejoinProgress,
                            state.CurrentTravelsForward);
                        lookAhead = TownRouteRejoinLookAheadMeters;
                    }
                }

                float previousProgress = state.RouteProgress01;
                bool rejoinedOrdinaryFittan =
                    TryRejoinOrdinaryFittanAlignedBranch(
                        state,
                        motion,
                        geometry,
                        lookAhead,
                        ref hint,
                        ref projection,
                        ref guidance);
                state.SegmentHint = hint;
                if (rejoinedOrdinaryFittan)
                {
                    state.RouteProgress01 = projection.Progress01;
                }
                else
                {
                    float projectionSeparation = initialProjectionSeparation;
                    if (!townRouteRejoinActive &&
                        projectionSeparation <= 10f &&
                        geometry.CanCommitPhysicalProjection(
                            state.RouteProgress01,
                            projection.Progress01,
                            state.CurrentTravelsForward))
                    {
                        state.RouteProgress01 = projection.Progress01;
                        bool wrapped = state.CurrentTravelsForward
                            ? state.RouteProgress01 + 0.5f < previousProgress
                            : state.RouteProgress01 > previousProgress + 0.5f;
                        if (wrapped)
                        {
                            state.CompletedCircuits++;
                        }
                    }
                }
                state.CurrentSpeedMetersPerSecond =
                    motion.CurrentSpeedMetersPerSecond;
                if (!rejoinedOrdinaryFittan &&
                    TryResetOrdinaryFittanRoute(
                        state,
                        motion,
                        previousProgress))
                {
                    continue;
                }
                ConfigureOrdinaryFittanPhysicalGuidance(
                    state,
                    motion,
                    projection,
                    guidance,
                    lookAhead);
                if (string.Equals(
                        state.CurrentRouteId,
                        GasPumpRouteId,
                        StringComparison.Ordinal))
                {
                    ConfigureActorMotionForCurrentRoute(state, motion);
                }
                if (townExcursionRoute)
                {
                    motion.SetRouteSpeedCap(
                        ResolveTownRouteSpeedCapMetersPerSecond(
                            state.CurrentRouteId,
                            projection,
                            guidance,
                            lookAhead,
                            townRouteRejoinActive));
                    motion.SetHillDriveAssist(false);
                }
                motion.SetPhysicalRouteGuidanceTarget(
                    guidance.Position,
                    guidance.Rotation,
                    state.RouteProgress01);
                if (IsPersistentNamedTrafficActor(state.Definition) ||
                    IsCousinSaturdayContext(state))
                {
                    float preloadDistanceMeters = Mathf.Max(
                        96f,
                        motion.CurrentSpeedMetersPerSecond * 3.5f);
                    TrafficRouteSample preload;
                    if (ordinaryFittan &&
                        geometry.TryResolveAheadWithinClosedPointRange(
                            state.RouteProgress01,
                            state.CurrentTravelsForward,
                            preloadDistanceMeters,
                            OrdinaryFittanFirstDirtRoadPointIndex,
                            OrdinaryFittanLastDirtRoadPointIndex,
                            out TrafficRouteSample rangedPreload))
                    {
                        preload = rangedPreload;
                    }
                    else
                    {
                        float preloadProgress = geometry.AdvanceProgress(
                            state.RouteProgress01,
                            state.CurrentTravelsForward,
                            preloadDistanceMeters,
                            out _);
                        preload = geometry.Resolve(
                            preloadProgress,
                            state.CurrentTravelsForward);
                    }
                    RetainPhysicalTrafficCell(
                        "ambient:" + pair.Key + ":ahead",
                        preload.Position);
                }
                if (TryAdvanceCousinRouteProgram(state, motion))
                {
                    continue;
                }
                TryTransitionTownRoute(
                    state,
                    motion,
                    endpointCompleted: false);
            }

            FixedUpdateTransports();
            FixedUpdateEvents();
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            // Donor traffic roots, bus, train and boats are driven by real
            // runtime frames. Clock/sleep jumps must not move, respawn, reroll
            // or dematerialize them. The subscription remains so that this
            // invariant is explicit and protected by tests.
        }

        private void ReconcileMaterialization()
        {
            desiredMaterialized.Clear();
            ambientResidencyCandidates.Clear();
            foreach (TrafficActorDefinition definition in catalog.Actors)
            {
                ActorRuntimeState state = states[definition.ActorId];
                bool persistentNamedActor = IsPersistentNamedTrafficActor(
                    definition);
                bool persistentContext = persistentNamedActor ||
                                         IsCousinSaturdayContext(state);
                if (!state.Active)
                {
                    ReleasePhysicalTrafficCells(
                        "ambient:" + definition.ActorId);
                    continue;
                }

                if (persistentContext)
                {
                    // Pena stays physical globally. Kuski's separately
                    // authored Saturday context is also retained, and neither
                    // is charged against the ordinary ambient budget.
                    desiredMaterialized.Add(definition.ActorId);
                    continue;
                }

                if (!IsActorMemberOfRoot(definition, activeAmbientRoot))
                {
                    continue;
                }

                bool alreadyMaterialized = presentations.TryGetValue(
                    definition.ActorId,
                    out StoryTrafficVehiclePresentationBinding motion) &&
                    motion != null;
                Vector3 actorPosition = alreadyMaterialized
                    ? motion.PhysicalWorldPosition
                    : state.HasPhysicalPose
                        ? state.WorldPosition
                        : ResolveLanePosition(
                            ResolveLogicalSample(state),
                            definition.BaseLaneOffsetMeters);
                float maximumDistance = alreadyMaterialized
                    ? AmbientDematerializeDistanceMeters
                    : AmbientMaterializeDistanceMeters;
                float distanceSquared =
                    (actorPosition - player.position).sqrMagnitude;
                if (distanceSquared <= maximumDistance * maximumDistance)
                {
                    ambientResidencyCandidates.Add(
                        new AmbientResidencyCandidate(
                            definition.ActorId,
                            distanceSquared,
                            alreadyMaterialized));
                }
            }

            ambientResidencyCandidates.Sort(
                AmbientResidencyCandidateComparer.Instance);
            int ordinaryCount = Mathf.Min(
                MaximumMaterializedOrdinaryAmbientActors,
                ambientResidencyCandidates.Count);
            for (int index = 0; index < ordinaryCount; index++)
            {
                desiredMaterialized.Add(
                    ambientResidencyCandidates[index].ActorId);
            }

            string[] existing = presentations.Keys.ToArray();
            foreach (string actorId in existing)
            {
                if (!desiredMaterialized.Contains(actorId))
                {
                    RemovePresentation(actorId);
                }
            }

            foreach (string actorId in desiredMaterialized)
            {
                if (!presentations.ContainsKey(actorId))
                {
                    ActorRuntimeState state = states[actorId];
                    bool persistentContext =
                        IsPersistentNamedTrafficActor(state.Definition) ||
                        IsCousinSaturdayContext(state);
                    if (!persistentContext ||
                        IsPhysicalTrafficSpawnCellReady(
                            "ambient:" + actorId,
                            ResolveSupportedAmbientSpawnPosition(state)))
                    {
                        Materialize(actorId);
                    }
                }
            }
        }

        /// <summary>
        /// Advances only ordinary, non-resident ambient actors using real
        /// runtime seconds. Game-clock jumps remain deliberately irrelevant.
        /// Named story traffic is always physical and therefore excluded.
        /// </summary>
        internal void AdvanceLogicalAmbientActors(float realDeltaSeconds)
        {
            if (!initialized || !float.IsFinite(realDeltaSeconds) ||
                realDeltaSeconds <= 0f ||
                activeAmbientRoot == AmbientTrafficRoot.None)
            {
                return;
            }

            foreach (TrafficActorDefinition definition in catalog.Actors)
            {
                ActorRuntimeState state = states[definition.ActorId];
                if (!state.Active ||
                    presentations.ContainsKey(definition.ActorId) ||
                    IsPersistentNamedTrafficActor(definition) ||
                    IsCousinSaturdayContext(state) ||
                    !IsActorMemberOfRoot(definition, activeAmbientRoot) ||
                    !routes.TryGetValue(
                        state.CurrentRouteId,
                        out TrafficRouteGeometry geometry))
                {
                    continue;
                }

                BeginLogicalAmbientSimulation(state);
                float logicalSpeed = Mathf.Max(
                    0.1f,
                    ResolveLogicalSpeedForCurrentRoute(state));
                state.RouteProgress01 = geometry.AdvanceProgress(
                    state.RouteProgress01,
                    state.CurrentTravelsForward,
                    logicalSpeed * realDeltaSeconds,
                    out bool endpointOrCircuitCompleted);
                if (endpointOrCircuitCompleted && geometry.ClosesLoop)
                {
                    state.CompletedCircuits++;
                }

                state.CurrentSpeedMetersPerSecond = logicalSpeed;
                state.CruiseSpeedMetersPerSecond = logicalSpeed;
                TrafficRouteSample sample = geometry.Resolve(
                    state.RouteProgress01,
                    state.CurrentTravelsForward);
                state.SegmentHint = sample.SegmentIndex;
                TryTransitionTownRoute(
                    state,
                    motion: null,
                    endpointCompleted: endpointOrCircuitCompleted);
            }
        }

        private static void BeginLogicalAmbientSimulation(
            ActorRuntimeState state)
        {
            if (!state.HasPhysicalPose &&
                state.ManeuverState == StoryTrafficManeuverState.Cruise &&
                !state.HasSafePose)
            {
                return;
            }

            // A physical pose and recovery/pass state are presentation state.
            // Retaining them after route progress advances would respawn the
            // actor at its old world position or in a stale evasive manoeuvre.
            state.HasPhysicalPose = false;
            state.HasSafePose = false;
            state.ManeuverState = StoryTrafficManeuverState.Cruise;
            state.ManeuverStateSeconds = 0f;
            state.DriftSlipDegrees = 0f;
            state.LaneOffsetMeters = state.Definition.BaseLaneOffsetMeters;
        }

        private bool UpdateAmbientTrafficRoot()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 current = player.position;
            if (!hasPreviousPlayerPosition)
            {
                previousPlayerPosition = current;
                hasPreviousPlayerPosition = true;
                return false;
            }

            if (lockedGatePairIndex >= 0)
            {
                if (IsInsideGateRearmDistance(current, lockedGateCenter))
                {
                    previousPlayerPosition = current;
                    return false;
                }

                lockedGatePairIndex = -1;
            }

            Vector3 previous = previousPlayerPosition;
            previousPlayerPosition = current;
            if ((current - previous).sqrMagnitude >
                MaximumGateTravelPerFrameMeters *
                MaximumGateTravelPerFrameMeters)
            {
                // A load/debug teleport is not a real traversal and must not
                // manufacture an arbitrary gate crossing. It can, however,
                // repair the pre-fix/legacy state where no root was selected.
                // Preserve an already known Highway/DirtRoad root.
                lockedGatePairIndex = -1;
                AmbientTrafficRoot recoveredRoot =
                    ResolveAmbientRootAfterDiscontinuousMove(
                        activeAmbientRoot);
                if (recoveredRoot == activeAmbientRoot)
                {
                    return false;
                }

                activeAmbientRoot = recoveredRoot;
                return true;
            }

            if (!TryResolveAmbientRootTransition(
                    previous,
                    current,
                    out AmbientTrafficRoot nextRoot,
                    out int pairIndex,
                    out Vector3 gateCenter))
            {
                return false;
            }

            lockedGatePairIndex = pairIndex;
            lockedGateCenter = gateCenter;
            if (nextRoot == activeAmbientRoot)
            {
                return false;
            }

            activeAmbientRoot = nextRoot;
            return true;
        }

        private static AmbientTrafficRoot ResolveInitialAmbientRoot() =>
            AmbientTrafficRoot.Highway;

        private static AmbientTrafficRoot
            ResolveAmbientRootAfterDiscontinuousMove(
                AmbientTrafficRoot currentRoot) =>
            currentRoot == AmbientTrafficRoot.None
                ? AmbientTrafficRoot.Highway
                : currentRoot;

        private static bool IsInsideGateRearmDistance(
            Vector3 position,
            Vector3 gateCenter) =>
            (position - gateCenter).sqrMagnitude <=
            TrafficGateRearmDistanceMeters * TrafficGateRearmDistanceMeters;

        private static bool TryResolveAmbientRootTransition(
            Vector3 previous,
            Vector3 current,
            out AmbientTrafficRoot targetRoot,
            out int pairIndex,
            out Vector3 gateCenter)
        {
            AmbientTrafficRoot resolvedRoot = AmbientTrafficRoot.None;
            int resolvedPairIndex = -1;
            Vector3 resolvedGateCenter = default;
            float earliestFraction = float.PositiveInfinity;
            for (int index = 0; index < AmbientTrafficGates.Length; index++)
            {
                AmbientTrafficGatePair pair = AmbientTrafficGates[index];
                Consider(pair.OutCenter, AmbientTrafficRoot.Highway);
                Consider(pair.InCenter, AmbientTrafficRoot.DirtRoad);

                void Consider(Vector3 center, AmbientTrafficRoot root)
                {
                    if (!TryCrossGatePlane(
                            previous,
                            current,
                            center,
                            pair.YawDegrees,
                            pair.WidthMeters,
                            pair.HeightMeters,
                            out float fraction) ||
                        fraction >= earliestFraction)
                    {
                        return;
                    }

                    earliestFraction = fraction;
                    resolvedRoot = root;
                    resolvedPairIndex = index;
                    resolvedGateCenter = center;
                }
            }

            targetRoot = resolvedRoot;
            pairIndex = resolvedPairIndex;
            gateCenter = resolvedGateCenter;
            return resolvedPairIndex >= 0;
        }

        private static bool TryCrossGatePlane(
            Vector3 previous,
            Vector3 current,
            Vector3 center,
            float yawDegrees,
            float widthMeters,
            float heightMeters,
            out float fraction)
        {
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 normal = rotation * Vector3.forward;
            float previousSide = Vector3.Dot(previous - center, normal);
            float currentSide = Vector3.Dot(current - center, normal);
            float denominator = previousSide - currentSide;
            if (Mathf.Abs(denominator) <= 0.0001f ||
                previousSide * currentSide > 0f)
            {
                fraction = 0f;
                return false;
            }

            fraction = previousSide / denominator;
            if (fraction < 0f || fraction > 1f)
            {
                return false;
            }

            Vector3 crossing = Vector3.LerpUnclamped(
                previous,
                current,
                fraction);
            Vector3 right = rotation * Vector3.right;
            return Mathf.Abs(Vector3.Dot(crossing - center, right)) <=
                       widthMeters * 0.5f &&
                   Mathf.Abs(crossing.y - center.y) <= heightMeters * 0.5f;
        }

        private static bool IsActorMemberOfRoot(
            TrafficActorDefinition definition,
            AmbientTrafficRoot root)
        {
            if (definition == null || root == AmbientTrafficRoot.None)
            {
                return false;
            }

            bool dirtRoad = string.Equals(
                definition.RouteId,
                DirtRoadRouteId,
                StringComparison.Ordinal);
            return root == AmbientTrafficRoot.DirtRoad
                ? dirtRoad
                : !dirtRoad;
        }

        internal static bool IsPersistentNamedTrafficActor(
            TrafficActorDefinition definition) =>
            definition != null && string.Equals(
                definition.ActorId,
                TrafficCousinBehaviorRules.ActorId,
                StringComparison.Ordinal);

        private Vector3 ResolveSupportedAmbientSpawnPosition(
            ActorRuntimeState state)
        {
            TrafficRouteSample sample = ResolveLogicalSample(state);
            if (state.HasPhysicalPose &&
                IsPhysicalPoseCompatibleWithRoute(
                    state.WorldPosition,
                    sample.Position))
            {
                return state.WorldPosition;
            }

            state.HasPhysicalPose = false;
            state.HasSafePose = false;
            return ResolveLanePosition(
                sample,
                state.Definition.BaseLaneOffsetMeters);
        }

        private bool IsPhysicalTrafficSpawnCellReady(
            string actorOwnerId,
            Vector3 worldPosition)
        {
            bool currentCellLoaded = RetainPhysicalTrafficCell(
                actorOwnerId + ":current",
                worldPosition);
            if (worldStreaming == null)
            {
                // Tests and deliberately non-streamed fixtures keep the legacy
                // immediate-materialization behavior. Production always
                // supplies the streaming service through the composition root.
                return currentCellLoaded;
            }

            ProductionWorldStreamingManifest manifest =
                worldStreaming.Manifest;
            return IsPhysicalTrafficMaterializationReady(
                currentCellLoaded,
                manifest != null ? manifest.GlobalScenes : null,
                worldStreaming.IsGlobalSceneLoaded);
        }

        private bool RetainPhysicalTrafficCell(
            string ownerSuffix,
            Vector3 worldPosition)
        {
            if (worldStreaming == null)
            {
                return true;
            }

            if (!worldStreaming.TryGetCellIdForPosition(
                    worldPosition,
                    out string cellId))
            {
                // A streamed physical actor must never materialize at a pose
                // for which the active manifest cannot provide a support cell.
                return false;
            }

            string ownerId = "traffic.runtime:" + ownerSuffix;
            worldStreaming.RetainCell(ownerId, cellId);
            cellRetentionOwners.Add(ownerId);
            return worldStreaming.IsCellLoaded(cellId);
        }

        private static bool IsPhysicalTrafficMaterializationReady(
            bool currentCellLoaded,
            IReadOnlyList<ProductionWorldGlobalScene> globalScenes,
            Func<string, bool> isGlobalSceneLoaded)
        {
            if (!currentCellLoaded || globalScenes == null ||
                isGlobalSceneLoaded == null)
            {
                return false;
            }

            for (int index = 0; index < globalScenes.Count; index++)
            {
                if (!isGlobalSceneLoaded(globalScenes[index].SceneId))
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleasePhysicalTrafficCells(string actorOwnerId)
        {
            ReleasePhysicalTrafficCell(actorOwnerId + ":current");
            ReleasePhysicalTrafficCell(actorOwnerId + ":ahead");
        }

        private void ReleasePhysicalTrafficCell(string ownerSuffix)
        {
            if (worldStreaming == null)
            {
                return;
            }

            string ownerId = "traffic.runtime:" + ownerSuffix;
            worldStreaming.ReleaseCellRetention(ownerId);
            cellRetentionOwners.Remove(ownerId);
        }

        private void ReleaseAllPhysicalTrafficCells()
        {
            if (worldStreaming != null)
            {
                foreach (string ownerId in cellRetentionOwners)
                {
                    worldStreaming.ReleaseCellRetention(ownerId);
                }
            }

            cellRetentionOwners.Clear();
        }

        private static bool IsPhysicalPoseCompatibleWithRoute(
            Vector3 physicalPosition,
            Vector3 logicalPosition) =>
            IsFiniteWorldPosition(physicalPosition) &&
            IsFiniteWorldPosition(logicalPosition) &&
            Mathf.Abs(physicalPosition.y - logicalPosition.y) <= 24f &&
            Vector3.Distance(physicalPosition, logicalPosition) <=
                MaximumPhysicalRouteSeparationMeters;

        private static bool IsFiniteWorldPosition(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static float ResolveAmbientLookAheadMeters(
            float speedMetersPerSecond)
        {
            // Donor road samples are dense and contain tight village and
            // interchange bends. A 25-44 m preview made pure pursuit cut the
            // inside shoulder before obstacle sensing could react. Keep a
            // speed-scaled preview, but within the geometry actually visible
            // to the driver's forward probes.
            float speed = Mathf.Max(0f, speedMetersPerSecond);
            return Mathf.Clamp(14f + speed * 0.45f, 14f, 30f);
        }

        private static float ResolveOrdinaryFittanLookAheadMeters(
            float speedMetersPerSecond)
        {
            // FITTAN follows the donor's narrow 5.6 m dirt road, including
            // short-radius bends and unusually steep sampled grades. A shorter
            // pure-pursuit preview prevents the physical chassis from cutting
            // across the inside shoulder while retaining the locked 95-105
            // km/h envelope on genuinely straight sections.
            float speed = Mathf.Max(0f, speedMetersPerSecond);
            return Mathf.Clamp(8.5f + speed * 0.34f, 9f, 18.5f);
        }

        private static bool TryRejoinOrdinaryFittanAlignedBranch(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion,
            TrafficRouteGeometry geometry,
            float lookAheadMeters,
            ref int segmentHint,
            ref TrafficRouteSample projection,
            ref TrafficRouteSample guidance)
        {
            if (!IsCousin(state) ||
                state.CousinContext != CousinTrafficContext.OrdinaryFittan ||
                !string.Equals(
                    state.CurrentRouteId,
                    DirtRoadRouteId,
                    StringComparison.Ordinal) ||
                motion.ManeuverState == StoryTrafficManeuverState.Crashed)
            {
                return false;
            }

            Vector3 physicalForward =
                motion.PhysicalWorldRotation * Vector3.forward;
            physicalForward.y = 0f;
            Vector3 currentTargetDirection =
                guidance.Position - motion.PhysicalWorldPosition;
            currentTargetDirection.y = 0f;
            bool currentTargetIsAhead =
                physicalForward.sqrMagnitude > 0.000001f &&
                currentTargetDirection.sqrMagnitude > 0.000001f &&
                Vector3.Dot(
                    physicalForward.normalized,
                    currentTargetDirection.normalized) >= 0f;
            if (physicalForward.sqrMagnitude <= 0.000001f ||
                (!motion.IsRouteRejoinActive && currentTargetIsAhead) ||
                !geometry.TryProjectNearestAlignedWithinClosedPointRange(
                    motion.PhysicalWorldPosition,
                    physicalForward,
                    state.CurrentTravelsForward,
                    OrdinaryFittanRejoinMaximumDistanceMeters,
                    OrdinaryFittanRejoinMinimumAlignment,
                    lookAheadMeters,
                    OrdinaryFittanFirstDirtRoadPointIndex,
                    OrdinaryFittanLastDirtRoadPointIndex,
                    out TrafficRouteSample alignedProjection,
                    out TrafficRouteSample alignedGuidance))
            {
                return false;
            }
            Vector3 alignedTargetDirection =
                alignedGuidance.Position - motion.PhysicalWorldPosition;
            alignedTargetDirection.y = 0f;
            if (alignedTargetDirection.sqrMagnitude <= 0.000001f ||
                Vector3.Dot(
                    physicalForward.normalized,
                    alignedTargetDirection.normalized) <= 0f)
            {
                return false;
            }

            projection = alignedProjection;
            guidance = alignedGuidance;
            segmentHint = alignedProjection.SegmentIndex;
            return true;
        }

        private static void ConfigureOrdinaryFittanPhysicalGuidance(
            ActorRuntimeState state,
            StoryTrafficVehiclePresentationBinding motion,
            in TrafficRouteSample projection,
            in TrafficRouteSample guidance,
            float lookAheadMeters)
        {
            if (!IsOrdinaryFittanDirtRoute(state))
            {
                return;
            }

            Vector3 projectionForward =
                projection.Rotation * Vector3.forward;
            Vector3 guidanceForward = guidance.Rotation * Vector3.forward;
            float headingChangeDegrees = Vector3.Angle(
                projectionForward,
                guidanceForward);
            float horizontalDistance = Vector2.Distance(
                new Vector2(projection.Position.x, projection.Position.z),
                new Vector2(guidance.Position.x, guidance.Position.z));
            float grade = horizontalDistance > 0.1f
                ? Mathf.Abs(guidance.Position.y - projection.Position.y) /
                  horizontalDistance
                : 0f;

            float routeSpeedCap = float.PositiveInfinity;
            if (headingChangeDegrees > 3f)
            {
                float headingRadians = headingChangeDegrees * Mathf.Deg2Rad;
                float estimatedRadius = Mathf.Max(
                    4f,
                    lookAheadMeters / Mathf.Max(0.05f, headingRadians));
                routeSpeedCap = Mathf.Max(
                    10.5f,
                    Mathf.Sqrt(3.4f * estimatedRadius));
            }

            if (grade > 0.08f)
            {
                float gradeCap = Mathf.Lerp(
                    23f,
                    11.5f,
                    Mathf.InverseLerp(0.08f, 0.34f, grade));
                routeSpeedCap = Mathf.Min(routeSpeedCap, gradeCap);
            }

            if (motion.IsRouteRejoinActive)
            {
                routeSpeedCap = Mathf.Min(routeSpeedCap, 11.5f);
            }

            motion.SetRouteSpeedCap(routeSpeedCap);
            motion.SetHillDriveAssist(grade > 0.1f);
        }

        private static bool IsOrdinaryFittanDirtRoute(
            ActorRuntimeState state) =>
            IsCousin(state) &&
            state.CousinContext == CousinTrafficContext.OrdinaryFittan &&
            string.Equals(
                state.CurrentRouteId,
                DirtRoadRouteId,
                StringComparison.Ordinal);

        private void Materialize(string actorId)
        {
            ActorRuntimeState state = states[actorId];
            TrafficActorDefinition definition = state.Definition;
            string presentationId = ResolveActorPresentationId(state);
            if (!presentationCatalog.TryGet(
                    presentationId,
                    out TrafficPresentationCatalogEntry entry) ||
                entry.WrapperPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Traffic presentation '{presentationId}' is unavailable.");
            }

            TrafficRouteSample routeSample = ResolveLogicalSample(state);
            state.SegmentHint = routeSample.SegmentIndex;
            Vector3 position = state.HasPhysicalPose
                ? state.WorldPosition
                : ResolveLanePosition(
                    routeSample,
                    definition.BaseLaneOffsetMeters);
            Quaternion rotation = state.HasPhysicalPose
                ? state.WorldRotation
                : routeSample.Rotation;
            if (state.HasPhysicalPose &&
                !IsPhysicalPoseCompatibleWithRoute(
                    position,
                    routeSample.Position))
            {
                // Repairs saves captured by the old bootstrap-origin
                // materialization bug without discarding logical route progress.
                state.HasPhysicalPose = false;
                position = ResolveLanePosition(
                    routeSample,
                    definition.BaseLaneOffsetMeters);
                rotation = routeSample.Rotation;
            }

            GameObject wrapper = Instantiate(
                entry.WrapperPrefab,
                position,
                rotation,
                transform);
            wrapper.name = "Traffic_Presentation_" + actorId;
            StoryTrafficVehiclePresentationBinding motion =
                wrapper.GetComponent<StoryTrafficVehiclePresentationBinding>();
            string failure = string.Empty;
            if (motion == null || !motion.TryValidate(out failure) ||
                !motion.HasPhysicalMotionBackend)
            {
                Destroy(wrapper);
                throw new InvalidOperationException(
                    $"Traffic presentation '{presentationId}' is not a physical wrapper: {failure}");
            }

            motion.SnapToRoutePoseTarget(position, rotation);
            motion.RestoreRuntimeState(state.CreateMotionState());
            ConfigureActorMotionForCurrentRoute(state, motion);
            // Snap uses the already lane-offset spawn pose. Guidance remains
            // centreline-based because the presentation applies its lane once.
            // Without this first target the initial physics tick offsets the
            // spawn a second time and starts a needless shoulder correction.
            motion.SetPhysicalRouteGuidanceTarget(
                routeSample.Position,
                routeSample.Rotation,
                state.RouteProgress01);
            presentations.Add(actorId, motion);
            if (audioOwners.TryGetValue(
                    actorId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null)
            {
                audio.BindMotion(motion);
                audio.SetDrivingActive(true);
            }
        }

        private void RemovePresentation(string actorId)
        {
            if (!presentations.TryGetValue(
                    actorId,
                    out StoryTrafficVehiclePresentationBinding motion))
            {
                return;
            }

            presentations.Remove(actorId);
            if (motion != null && states.TryGetValue(
                    actorId,
                    out ActorRuntimeState state))
            {
                state.CapturePhysical(motion);
            }

            if (audioOwners.TryGetValue(
                    actorId,
                    out StoryTrafficVehicleAudioPresenter audio) &&
                audio != null && motion != null)
            {
                audio.UnbindMotion(motion);
            }

            if (motion != null)
            {
                Destroy(motion.gameObject);
            }
        }

        private void RemoveAllPresentations()
        {
            foreach (string actorId in presentations.Keys.ToArray())
            {
                RemovePresentation(actorId);
            }
        }

        private TrafficRouteSample ResolveLogicalSample(ActorRuntimeState state)
        {
            return routes[state.CurrentRouteId].Resolve(
                state.RouteProgress01,
                state.CurrentTravelsForward);
        }

        private static bool IsLegacyBootstrapOrigin(
            Vector3 physicalPosition,
            Vector3 logicalPosition)
        {
            return physicalPosition.sqrMagnitude <= 4f &&
                   logicalPosition.sqrMagnitude >
                   MaximumPhysicalRouteSeparationMeters *
                   MaximumPhysicalRouteSeparationMeters;
        }

        private static Vector3 ResolveLanePosition(
            in TrafficRouteSample sample,
            float laneOffsetMeters)
        {
            return sample.Position +
                   sample.Rotation * Vector3.right * laneOffsetMeters;
        }

        private void CreateAudioOwner(
            TrafficActorDefinition definition,
            ActorRuntimeState state)
        {
            var owner = new GameObject("Traffic_Audio_" + definition.ActorId);
            owner.transform.SetParent(transform, false);
            StoryTrafficVehicleAudioPresenter audio =
                owner.AddComponent<StoryTrafficVehicleAudioPresenter>();
            audio.ConfigurePersistent(
                audioBackendComponent,
                definition.ActorId,
                player,
                "petteri");
            TrafficRouteSample sample = ResolveLogicalSample(state);
            audio.SetLogicalPose(
                sample.Position,
                sample.Rotation,
                state.Active);
            audioOwners.Add(definition.ActorId, audio);
        }

        private void ReconcileAllAudioOwners()
        {
            foreach (TrafficActorDefinition definition in catalog.Actors)
            {
                if (!audioOwners.TryGetValue(
                        definition.ActorId,
                        out StoryTrafficVehicleAudioPresenter audio) ||
                    audio == null)
                {
                    continue;
                }

                ActorRuntimeState state = states[definition.ActorId];
                TrafficRouteSample sample = ResolveLogicalSample(state);
                audio.SetLogicalPose(
                    state.HasPhysicalPose
                        ? state.WorldPosition
                        : sample.Position,
                    state.HasPhysicalPose
                        ? state.WorldRotation
                        : sample.Rotation,
                    state.Active);
            }
        }

        private void OnDestroy()
        {
            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
            RemoveAllPresentations();
            RemoveAllTransportPresentations();
            RemoveAllEventPresentations();
            RemoveAllTransportAudioOwners();
            foreach (StoryTrafficVehicleAudioPresenter audio in
                     audioOwners.Values)
            {
                if (audio != null)
                {
                    Destroy(audio.gameObject);
                }
            }

            audioOwners.Clear();
            ReleaseAllPhysicalTrafficCells();
            worldStreaming = null;
            initialized = false;
        }

        public void EndGameSession()
        {
            if (this == null)
            {
                return;
            }

            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
            RemoveAllPresentations();
            RemoveAllTransportPresentations();
            RemoveAllEventPresentations();
            RemoveAllTransportAudioOwners();
            foreach (StoryTrafficVehicleAudioPresenter audio in
                     audioOwners.Values)
            {
                if (audio != null)
                {
                    Destroy(audio.gameObject);
                }
            }

            audioOwners.Clear();
            ReleaseAllPhysicalTrafficCells();
            worldStreaming = null;
            initialized = false;
            enabled = false;
        }

        private void EnsureInitialized()
        {
            if (!initialized || !HasLiveSessionDependencies())
            {
                throw new InvalidOperationException(
                    "Traffic runtime is not initialized.");
            }
        }

        private bool TryEnterPlayerLoop()
        {
            if (!initialized)
            {
                return false;
            }

            if (HasLiveSessionDependencies())
            {
                return true;
            }

            // A script/domain reload can restore Unity-serializable state while
            // dropping plain C# service references. Fail closed once instead
            // of advancing a half-restored simulation and throwing every frame.
            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
            initialized = false;
            enabled = false;
            Debug.LogWarning(
                "Traffic runtime lost live session dependencies and was " +
                "disabled. Restart Play Mode to rebuild the session.",
                this);
            return false;
        }

        private bool HasLiveSessionDependencies()
        {
            return catalog != null &&
                   presentationCatalog != null &&
                   gameTime != null &&
                   player != null;
        }

        private enum AmbientTrafficRoot
        {
            None = 0,
            Highway = 1,
            DirtRoad = 2,
        }

        private readonly struct AmbientTrafficGatePair
        {
            public AmbientTrafficGatePair(
                Vector3 outCenter,
                Vector3 inCenter,
                float yawDegrees,
                float widthMeters,
                float heightMeters)
            {
                OutCenter = outCenter;
                InCenter = inCenter;
                YawDegrees = yawDegrees;
                WidthMeters = widthMeters;
                HeightMeters = heightMeters;
            }

            public Vector3 OutCenter { get; }
            public Vector3 InCenter { get; }
            public float YawDegrees { get; }
            public float WidthMeters { get; }
            public float HeightMeters { get; }
        }

        private readonly struct AmbientResidencyCandidate
        {
            public AmbientResidencyCandidate(
                string actorId,
                float distanceSquared,
                bool retainedPhysicalActor)
            {
                ActorId = actorId;
                DistanceSquared = distanceSquared;
                RetainedPhysicalActor = retainedPhysicalActor;
            }

            public string ActorId { get; }
            public float DistanceSquared { get; }
            public bool RetainedPhysicalActor { get; }
        }

        private sealed class AmbientResidencyCandidateComparer :
            IComparer<AmbientResidencyCandidate>
        {
            public static readonly AmbientResidencyCandidateComparer Instance =
                new();

            public int Compare(
                AmbientResidencyCandidate left,
                AmbientResidencyCandidate right)
            {
                // Hysteresis is an ownership rule, not just a larger radius:
                // an existing body inside 520 m keeps its slot. Otherwise a
                // slightly nearer logical actor can evict it every reconcile,
                // causing repeated spawn/despawn and overlapping chassis.
                int retained = right.RetainedPhysicalActor.CompareTo(
                    left.RetainedPhysicalActor);
                if (retained != 0)
                {
                    return retained;
                }

                int distance = left.DistanceSquared.CompareTo(
                    right.DistanceSquared);
                return distance != 0
                    ? distance
                    : string.CompareOrdinal(left.ActorId, right.ActorId);
            }
        }

        private sealed class ActorRuntimeState
        {
            private ActorRuntimeState(TrafficActorDefinition definition)
            {
                Definition = definition;
            }

            public TrafficActorDefinition Definition { get; }
            public string CurrentRouteId { get; set; } = string.Empty;
            public bool CurrentTravelsForward { get; set; }
            public float RouteProgress01 { get; set; }
            public bool Active { get; set; }
            public float DesiredSpeedMetersPerSecond { get; set; }
            public int CompletedCircuits { get; set; }
            public int SpawnAttemptCount { get; set; }
            public float InactiveRetryGameSeconds { get; set; }
            public int SegmentHint { get; set; } = -1;
            public bool HasPhysicalPose { get; set; }
            public Vector3 WorldPosition { get; set; }
            public Quaternion WorldRotation { get; set; } = Quaternion.identity;
            public float CurrentSpeedMetersPerSecond { get; set; }
            public float CruiseSpeedMetersPerSecond { get; set; }
            public float LaneOffsetMeters { get; set; }
            public StoryTrafficManeuverState ManeuverState { get; set; }
            public float ManeuverStateSeconds { get; set; }
            public float DriftSlipDegrees { get; set; }
            public int RecoveryCount { get; set; }
            public bool HasSafePose { get; set; }
            public Vector3 SafePosition { get; set; }
            public Quaternion SafeRotation { get; set; } = Quaternion.identity;
            public bool TownExcursionPending { get; set; }
            public float TownDecisionGameSecondsRemaining { get; set; }
            public int TownDecisionAttemptCount { get; set; }
            public int CompletedTownExcursions { get; set; }
            public TownExcursionDestination TownExcursionDestination {
                get; set;
            }
            public CousinTrafficContext CousinContext { get; set; }
            public CousinRouteStage CousinRouteStage { get; set; }
            public int CousinActivationOrdinal { get; set; }
            public float CousinScheduleCheckRealSecondsRemaining { get; set; }

            public static ActorRuntimeState CreateInitial(
                TrafficActorDefinition definition,
                long dayIndex)
            {
                var state = new ActorRuntimeState(definition)
                {
                    CurrentRouteId = definition.RouteId,
                    CurrentTravelsForward = definition.TravelsForward,
                    RouteProgress01 = definition.InitialProgress01,
                    DesiredSpeedMetersPerSecond = Mathf.Lerp(
                        definition.MinimumSpeedMetersPerSecond,
                        definition.MaximumSpeedMetersPerSecond,
                        Hash01(definition.DeterministicSeed, 0, dayIndex)),
                    CruiseSpeedMetersPerSecond =
                        definition.MinimumSpeedMetersPerSecond,
                    LaneOffsetMeters = definition.BaseLaneOffsetMeters,
                    TownDecisionGameSecondsRemaining = 90f,
                };
                state.Active = state.RollSpawn(dayIndex);
                if (!state.Active)
                {
                    state.InactiveRetryGameSeconds =
                        definition.InactiveRetryGameSeconds;
                }

                return state;
            }

            public static ActorRuntimeState FromDto(
                TrafficActorDefinition definition,
                AmbientTrafficActorStateDto dto)
            {
                return new ActorRuntimeState(definition)
                {
                    CurrentRouteId = dto.routeId,
                    CurrentTravelsForward = dto.travelsForward,
                    RouteProgress01 = dto.routeProgress01,
                    Active = dto.active,
                    DesiredSpeedMetersPerSecond =
                        dto.desiredSpeedMetersPerSecond,
                    CompletedCircuits = dto.completedCircuits,
                    SpawnAttemptCount = dto.spawnAttemptCount,
                    InactiveRetryGameSeconds = dto.inactiveRetryGameSeconds,
                    HasPhysicalPose = dto.hasPhysicalPose,
                    WorldPosition = dto.worldPosition,
                    WorldRotation = dto.worldRotation,
                    CurrentSpeedMetersPerSecond =
                        dto.currentSpeedMetersPerSecond,
                    CruiseSpeedMetersPerSecond =
                        dto.cruiseSpeedMetersPerSecond,
                    LaneOffsetMeters = dto.laneOffsetMeters,
                    ManeuverState = Enum.IsDefined(
                        typeof(StoryTrafficManeuverState),
                        dto.maneuverState)
                            ? (StoryTrafficManeuverState)dto.maneuverState
                            : StoryTrafficManeuverState.Cruise,
                    ManeuverStateSeconds = dto.maneuverStateSeconds,
                    DriftSlipDegrees = dto.driftSlipDegrees,
                    RecoveryCount = dto.recoveryCount,
                    HasSafePose = dto.hasSafePose,
                    SafePosition = dto.safePosition,
                    SafeRotation = dto.safeRotation,
                    TownExcursionPending = dto.townExcursionPending,
                    TownDecisionGameSecondsRemaining =
                        dto.townDecisionGameSecondsRemaining,
                    TownDecisionAttemptCount = dto.townDecisionAttemptCount,
                    CompletedTownExcursions = dto.completedTownExcursions,
                    TownExcursionDestination =
                        (TownExcursionDestination)
                        dto.townExcursionDestination,
                    CousinContext = (CousinTrafficContext)dto.cousinContext,
                    CousinRouteStage = (CousinRouteStage)dto.cousinRouteStage,
                    CousinActivationOrdinal = dto.cousinActivationOrdinal,
                    CousinScheduleCheckRealSecondsRemaining =
                        dto.cousinScheduleCheckRealSecondsRemaining,
                };
            }

            public bool RollSpawn(long dayIndex)
            {
                long projectDay = PositiveModulo(dayIndex, 7L);
                // The authoritative calendar starts on Tuesday 1995-08-01;
                // indices 4/5 are Saturday/Sunday.
                bool weekend = projectDay == 4L || projectDay == 5L;
                float probability = weekend
                    ? Definition.WeekendSpawnProbability01
                    : Definition.WeekdaySpawnProbability01;
                if (probability <= 0f)
                {
                    return false;
                }

                if (probability >= 1f)
                {
                    return true;
                }

                float roll = Hash01(
                    Definition.DeterministicSeed,
                    SpawnAttemptCount,
                    dayIndex);
                SpawnAttemptCount++;
                return roll < probability;
            }

            public void CapturePhysical(
                StoryTrafficVehiclePresentationBinding motion)
            {
                HasPhysicalPose = true;
                WorldPosition = motion.PhysicalWorldPosition;
                WorldRotation = motion.PhysicalWorldRotation;
                CurrentSpeedMetersPerSecond =
                    motion.CurrentSpeedMetersPerSecond;
                StoryTrafficMotionRuntimeState state =
                    motion.CaptureRuntimeState();
                CruiseSpeedMetersPerSecond =
                    state.CruiseSpeedMetersPerSecond;
                LaneOffsetMeters = state.LaneOffsetMeters;
                ManeuverState = state.ManeuverState;
                ManeuverStateSeconds = state.ManeuverStateSeconds;
                DriftSlipDegrees = state.DriftSlipDegrees;
                RecoveryCount = state.RecoveryCount;
                HasSafePose = state.HasSafePose;
                SafePosition = state.SafePosition;
                SafeRotation = state.SafeRotation;
                RouteProgress01 = state.HasPhysicalRouteProgress
                    ? state.LastPhysicalRouteProgress01
                    : RouteProgress01;
            }

            public StoryTrafficMotionRuntimeState CreateMotionState() =>
                new(
                    CurrentSpeedMetersPerSecond,
                    CruiseSpeedMetersPerSecond > 0f
                        ? CruiseSpeedMetersPerSecond
                        : DesiredSpeedMetersPerSecond,
                    LaneOffsetMeters,
                    ManeuverState,
                    ManeuverStateSeconds,
                    DriftSlipDegrees,
                    RecoveryCount,
                    HasSafePose,
                    SafePosition,
                    SafeRotation,
                    socialStopSecondsRemaining: 0f,
                    lastPhysicalRouteProgress01: RouteProgress01,
                    hasPhysicalRouteProgress: true,
                    storyIncidentHold: false);

            public AmbientTrafficActorStateDto ToDto() =>
                new()
                {
                    actorId = Definition.ActorId,
                    stableInstanceId = Definition.StableInstanceId,
                    routeId = CurrentRouteId,
                    routeProgress01 = Mathf.Clamp01(RouteProgress01),
                    travelsForward = CurrentTravelsForward,
                    active = Active,
                    desiredSpeedMetersPerSecond =
                        DesiredSpeedMetersPerSecond,
                    completedCircuits = CompletedCircuits,
                    spawnAttemptCount = SpawnAttemptCount,
                    inactiveRetryGameSeconds = InactiveRetryGameSeconds,
                    hasPhysicalPose = HasPhysicalPose,
                    worldPosition = WorldPosition,
                    worldRotation = WorldRotation,
                    currentSpeedMetersPerSecond =
                        Mathf.Max(0f, CurrentSpeedMetersPerSecond),
                    cruiseSpeedMetersPerSecond =
                        Mathf.Max(0f, CruiseSpeedMetersPerSecond),
                    laneOffsetMeters = LaneOffsetMeters,
                    maneuverState = (int)ManeuverState,
                    maneuverStateSeconds = Mathf.Max(0f, ManeuverStateSeconds),
                    driftSlipDegrees = DriftSlipDegrees,
                    recoveryCount = Mathf.Max(0, RecoveryCount),
                    hasSafePose = HasSafePose,
                    safePosition = SafePosition,
                    safeRotation = SafeRotation,
                    townExcursionPending = TownExcursionPending,
                    townDecisionGameSecondsRemaining =
                        Mathf.Max(0f, TownDecisionGameSecondsRemaining),
                    townDecisionAttemptCount =
                        Mathf.Max(0, TownDecisionAttemptCount),
                    completedTownExcursions =
                        Mathf.Max(0, CompletedTownExcursions),
                    townExcursionDestination =
                        (int)TownExcursionDestination,
                    cousinStateVersion = 1,
                    cousinContext = (int)CousinContext,
                    cousinRouteStage = (int)CousinRouteStage,
                    cousinActivationOrdinal =
                        Mathf.Max(0, CousinActivationOrdinal),
                    cousinScheduleCheckRealSecondsRemaining = Mathf.Max(
                        0f,
                        CousinScheduleCheckRealSecondsRemaining),
                };

            public static bool TryValidateDto(
                AmbientTrafficActorStateDto dto,
                out string failure)
            {
                if (dto == null ||
                    !float.IsFinite(dto.routeProgress01) ||
                    dto.routeProgress01 < 0f || dto.routeProgress01 > 1f ||
                    !float.IsFinite(dto.desiredSpeedMetersPerSecond) ||
                    dto.desiredSpeedMetersPerSecond <= 0f ||
                    dto.completedCircuits < 0 ||
                    !float.IsFinite(dto.inactiveRetryGameSeconds) ||
                    dto.inactiveRetryGameSeconds < 0f ||
                    !IsFinite(dto.worldPosition) ||
                    !IsFinite(dto.worldRotation) ||
                    !float.IsFinite(dto.currentSpeedMetersPerSecond) ||
                    dto.currentSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(dto.cruiseSpeedMetersPerSecond) ||
                    dto.cruiseSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(dto.laneOffsetMeters) ||
                    !Enum.IsDefined(
                        typeof(StoryTrafficManeuverState),
                        dto.maneuverState) ||
                    !float.IsFinite(dto.maneuverStateSeconds) ||
                    dto.maneuverStateSeconds < 0f ||
                    !float.IsFinite(dto.driftSlipDegrees) ||
                    dto.recoveryCount < 0 ||
                    !float.IsFinite(
                        dto.townDecisionGameSecondsRemaining) ||
                    dto.townDecisionGameSecondsRemaining < 0f ||
                    dto.townDecisionAttemptCount < 0 ||
                    dto.completedTownExcursions < 0 ||
                    !Enum.IsDefined(
                        typeof(TownExcursionDestination),
                        dto.townExcursionDestination) ||
                    dto.cousinStateVersion < 0 ||
                    dto.cousinStateVersion > 1 ||
                    !Enum.IsDefined(
                        typeof(CousinTrafficContext),
                        dto.cousinContext) ||
                    !Enum.IsDefined(
                        typeof(CousinRouteStage),
                        dto.cousinRouteStage) ||
                    dto.cousinActivationOrdinal < 0 ||
                    !float.IsFinite(
                        dto.cousinScheduleCheckRealSecondsRemaining) ||
                    dto.cousinScheduleCheckRealSecondsRemaining < 0f ||
                    !IsFinite(dto.safePosition) ||
                    !IsFinite(dto.safeRotation))
                {
                    failure = $"Ambient actor '{dto?.actorId}' has invalid state.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            private static float Hash01(int seed, int circuit, long dayIndex)
            {
                unchecked
                {
                    uint value = (uint)seed;
                    value ^= (uint)circuit * 0x9e3779b9u;
                    value ^= (uint)dayIndex * 0x85ebca6bu;
                    value ^= value >> 16;
                    value *= 0x7feb352du;
                    value ^= value >> 15;
                    value *= 0x846ca68bu;
                    value ^= value >> 16;
                    return (value & 0x00ffffffu) / 16777215f;
                }
            }

            private static long PositiveModulo(long value, long divisor)
            {
                long result = value % divisor;
                return result < 0L ? result + divisor : result;
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
