using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Process-lifetime production owner for authoritative environment domains.
    /// Vendor presentation is write-only and reachable solely through the narrow
    /// adapter interfaces.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed partial class ProductionEnvironmentController : MonoBehaviour
    {
        private const float PresentationIntervalSeconds = 0.25f;
        private const float MaximumNormalizedTimeBelowOne = 0.99999994f;
        private const int MaximumStartupFrames = 16;
        private const double EnvironmentRefreshGameTimeThresholdSeconds = 600d;
        private const float EnvironmentRefreshListenerDistanceMeters = 25f;
        private const double AmbientLightningSpawnGraceGameSeconds = 45d;
        private const double MinimumAmbientLightningCooldownGameSeconds = 90d;
        private const double MaximumAmbientLightningCooldownGameSeconds = 240d;
        private const float MinimumAmbientLightningRadiusMeters = 180f;
        private const float MaximumAmbientLightningRadiusMeters = 450f;

        [SerializeField] private MonoBehaviour adapterBehaviour;
        [SerializeField] private MonoBehaviour wetnessBridgeBehaviour;
        [SerializeField] private EnvironmentQualityTier initialQuality =
            EnvironmentQualityTier.Medium;
        [SerializeField] private ulong initialWeatherSeed = 19950801UL;

        private static ProductionEnvironmentController activeOwner;

        private readonly GlobalWetnessShaderBridge globalWetnessBridge =
            new GlobalWetnessShaderBridge();
        private readonly List<ShelterVolume> shelterBuffer =
            new List<ShelterVolume>(16);
        private readonly HashSet<string> shelterIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<uint> publishedLightningSequences =
            new HashSet<uint>();
        private readonly HashSet<uint> publishedThunderSequences =
            new HashSet<uint>();

        private IEnvironmentPresentationAdapter adapter;
        private IEnvironmentPresentationSceneOwnershipGuard sceneOwnershipGuard;
        private IWetnessShaderBridge materialWetnessBridge;
        private WeatherProfileCatalog weatherCatalog;
        private GameTimeService gameTime;
        private WeatherDirector weather;
        private GlobalWetnessController wetness;
        private LightningStrikeDirector lightning;
        private ProductionEnvironmentSaveDto pendingRestore;
        private ShelterVolume[] shelterVolumes = Array.Empty<ShelterVolume>();
        private Transform listener;
        private EnvironmentQualityTier qualityTier;
        private WeatherEnvironmentOutputs currentOutputs;
        private WeatherExposureContext exposureContext;
        private EnvironmentLightningVisualRequest pendingLightningVisual;
        private EnvironmentRefreshRequest pendingRefresh;
        private float pendingTransitionDurationSeconds = float.NaN;
        private ulong presentationRevision;
        private uint refreshSequence;
        private string refreshBindingId = string.Empty;
        private double refreshGameTimeSeconds;
        private Vector3 refreshListenerPosition;
        private double ambientLightningGraceUntilGameSeconds;
        private double nextAmbientLightningGameSeconds = double.PositiveInfinity;
        private uint ambientLightningOrdinal;
        private float nextPresentationTime;
        private bool hasEnvironmentRefreshAnchor;
        private bool hasRefreshListenerPosition;
        private bool wasAmbientLightningEligible;
        private bool domainsInitialized;
        private bool simulationActive;
        private bool isDuplicateOwner;
        private Coroutine topologyRevalidationRoutine;

        /// <summary>
        /// Vendor-neutral notification emitted after a coherent environment
        /// output has been composed and presented. Subscriber failures are
        /// isolated and cannot interrupt the authoritative environment owner.
        /// </summary>
        public event Action<WeatherEnvironmentOutputs>
            EnvironmentOutputsChanged;

        /// <summary>
        /// Project-owned lightning notification. Each strike sequence is
        /// published at most once for the lifetime of this controller.
        /// </summary>
        public event Action<LightningStrikeEvent> LightningOccurred;

        /// <summary>
        /// Project-owned thunder request. Each request sequence is published at
        /// most once and Enviro remains presentation-only.
        /// </summary>
        public event Action<ThunderAudioRequest> ThunderRequested;

        public static ProductionEnvironmentController ActiveOwner => activeOwner;

        public bool IsPrimaryOwner =>
            !Application.isPlaying ||
            (!isDuplicateOwner && activeOwner == this);

        public bool AreDomainsInitialized => domainsInitialized;

        public bool IsWorldRevealReady { get; private set; }

        public bool WasRestoreAppliedBeforeReveal { get; private set; }

        public bool IsSimulationActive => simulationActive;

        public string LastFailure { get; private set; } = string.Empty;

        public IGameTimeService GameTime => gameTime;

        public IWeatherService Weather => weather;

        public GameTimeService AuthoritativeGameTime => gameTime;

        public WeatherDirector AuthoritativeWeather => weather;

        public GlobalWetnessController AuthoritativeWetness => wetness;

        public LightningStrikeDirector AuthoritativeLightning => lightning;

        public EnvironmentQualityTier QualityTier => qualityTier;

        public WeatherEnvironmentOutputs CurrentOutputs => currentOutputs;

        public WeatherExposureContext ExposureContext => exposureContext;

        public ProductionRoadWetnessOutput RoadWetnessOutput =>
            new ProductionRoadWetnessOutput(
                wetness == null ? 0f : wetness.State.RoadWetness01);

        public int ActiveShelterVolumeCount => shelterVolumes.Length;

        public EnvironmentPresentationStatus PresentationStatus =>
            adapter?.Status ?? EnvironmentPresentationStatus.Detached;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            MonoBehaviour authoredAdapter,
            MonoBehaviour authoredWetnessBridge,
            EnvironmentQualityTier authoredInitialQuality,
            ulong authoredWeatherSeed)
        {
            adapterBehaviour = authoredAdapter;
            wetnessBridgeBehaviour = authoredWetnessBridge;
            initialQuality = authoredInitialQuality;
            initialWeatherSeed = authoredWeatherSeed;
        }

        public void InitializeForEditorValidation()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "Editor validation initialization is unavailable in Play Mode.");
            }

            if (domainsInitialized)
            {
                return;
            }

            InitializeDomains();
            RebuildShelterRegistry();
        }
#endif

        public bool BindPresentationCamera(Camera camera)
        {
            if (camera == null)
            {
                LastFailure = "Production environment received no player camera.";
                return false;
            }

            listener = camera.transform;
            if (!(adapter is IEnvironmentPresentationCameraTarget cameraTarget))
            {
                LastFailure =
                    "Production environment adapter has no explicit camera target contract.";
                return false;
            }

            bool bound = cameraTarget.TrySetPresentationCamera(camera);
            if (!bound)
            {
                LastFailure = "Production environment adapter rejected the player camera.";
            }

            return bound;
        }

        public void StageRestore(ProductionEnvironmentSaveDto dto)
        {
            EnsurePrimaryAndInitialized();
            if (IsWorldRevealReady || simulationActive)
            {
                throw new InvalidOperationException(
                    "Production environment restore must be staged before world reveal.");
            }

            ProductionEnvironmentPersistence.Validate(
                dto,
                gameTime,
                weather,
                wetness,
                lightning);
            pendingRestore = dto;
        }

        public ProductionEnvironmentSaveDto CaptureState()
        {
            EnsurePrimaryAndInitialized();
            return ProductionEnvironmentPersistence.Capture(
                gameTime,
                weather,
                wetness,
                lightning,
                qualityTier);
        }

        public IEnumerator InitializeBeforeWorldReveal()
        {
            EnsurePrimaryAndInitialized();
            if (IsWorldRevealReady)
            {
                yield break;
            }

            if (pendingRestore != null)
            {
                qualityTier = ProductionEnvironmentPersistence.RestoreAtomic(
                    pendingRestore,
                    gameTime,
                    weather,
                    wetness,
                    lightning);
                pendingRestore = null;
                WasRestoreAppliedBeforeReveal = true;
                ResetAutomaticAmbientLightningGate();
            }

            ApplyWetnessOutputs();
            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient |
                EnvironmentRefreshTarget.Reflections);
            // The first frame after a new game or restore must be coherent and
            // immediate. A scheduled front duration is used only after reveal.
            pendingTransitionDurationSeconds = 0f;

            for (int attempt = 0;
                 attempt < MaximumStartupFrames && !adapter.IsAttached;
                 attempt++)
            {
                EnvironmentPresentationStatus attachStatus = adapter.Attach();
                if (attachStatus.State == EnvironmentPresentationState.Faulted)
                {
                    LastFailure = BuildAdapterFailure(
                        "Production Enviro adapter failed to attach.");
                    throw new InvalidOperationException(LastFailure);
                }

                if (!adapter.IsAttached)
                {
                    yield return null;
                }
            }

            if (!adapter.IsAttached)
            {
                LastFailure = BuildAdapterFailure(
                    "Production Enviro adapter did not attach before the startup deadline.");
                throw new InvalidOperationException(LastFailure);
            }

            EnvironmentPresentationStatus status = PresentCurrentState();
            if (!status.IsOperational)
            {
                LastFailure = BuildAdapterFailure(
                    "Production environment initial presentation sync failed.");
                throw new InvalidOperationException(LastFailure);
            }

            IsWorldRevealReady = true;
            LastFailure = string.Empty;
        }

        public void BeginSimulationAfterWorldReveal()
        {
            EnsurePrimaryAndInitialized();
            if (!IsWorldRevealReady)
            {
                throw new InvalidOperationException(
                    "Production environment cannot simulate before its coherent reveal sync.");
            }

            simulationActive = true;
            nextPresentationTime = UnityEngine.Time.unscaledTime +
                                   PresentationIntervalSeconds;
        }

        public void StopSimulation()
        {
            simulationActive = false;
        }

        public EnvironmentPresentationStatus SetQuality(
            EnvironmentQualityTier requestedQuality)
        {
            EnsurePrimaryAndInitialized();
            _ = ProductionEnvironmentQuality.GetStableId(requestedQuality);
            qualityTier = requestedQuality;
            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient |
                EnvironmentRefreshTarget.Reflections);
            return IsWorldRevealReady
                ? PresentCurrentState()
                : PresentationStatus;
        }

        public EnvironmentPresentationStatus TriggerAmbientLightning(
            Vector3 worldPosition,
            float intensity01 = 1f)
        {
            EnsurePrimaryAndInitialized();
            AmbientLightningResult result = lightning.CreateAmbientLightning(
                worldPosition,
                intensity01);
            pendingLightningVisual = new EnvironmentLightningVisualRequest(
                true,
                result.Presentation.Sequence,
                result.Presentation.TargetWorldPosition,
                result.Presentation.Intensity01);
            EnvironmentPresentationStatus status = IsWorldRevealReady
                ? PresentCurrentState()
                : PresentationStatus;
            PublishAmbientLightning(result);
            return status;
        }

        public bool TryCreateGameplayLightning(
            IReadOnlyList<LightningStrikeCandidate> candidates,
            IReadOnlyList<LightningProtectionVolume> protectionVolumes,
            Vector3 listenerWorldPosition,
            float intensity01,
            out GameplayLightningResult result)
        {
            EnsurePrimaryAndInitialized();
            if (!lightning.TryCreateGameplayStrike(
                    candidates,
                    protectionVolumes,
                    listenerWorldPosition,
                    intensity01,
                    out result))
            {
                return false;
            }

            pendingLightningVisual = new EnvironmentLightningVisualRequest(
                true,
                result.Presentation.Sequence,
                result.Presentation.TargetWorldPosition,
                result.Presentation.Intensity01);
            if (IsWorldRevealReady)
            {
                PresentCurrentState();
            }

            PublishLightningOccurred(result.StrikeEvent);
            PublishThunderRequested(result.Thunder);

            return true;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                InitializeDomains();
                RebuildShelterRegistry();
                return;
            }

            if (activeOwner != null && activeOwner != this)
            {
                isDuplicateOwner = true;
                enabled = false;
                Debug.LogError(
                    "Duplicate production environment owner was disabled before startup.",
                    this);
                return;
            }

            activeOwner = this;
            InitializeDomains();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RebuildShelterRegistry();
        }

        private void Update()
        {
            if (!simulationActive || !domainsInitialized)
            {
                return;
            }

            double previousGameSeconds = gameTime.CurrentGameTimeSeconds;
            gameTime.Advance(UnityEngine.Time.unscaledDeltaTime);
            double gameDeltaSeconds =
                gameTime.CurrentGameTimeSeconds - previousGameSeconds;
            if (gameDeltaSeconds <= 0d)
            {
                return;
            }

            weather.Advance(gameDeltaSeconds);
            lightning.Advance(gameDeltaSeconds);
            AdvanceWetness(gameDeltaSeconds);

            if (UnityEngine.Time.unscaledTime >= nextPresentationTime)
            {
                nextPresentationTime = UnityEngine.Time.unscaledTime +
                                       PresentationIntervalSeconds;
                PresentCurrentState();
            }
        }

        private void OnDestroy()
        {
            CancelTopologyRevalidation();

            if (!Application.isPlaying)
            {
                simulationActive = false;
                adapter?.Detach();
                globalWetnessBridge.Reset();
                return;
            }

            if (activeOwner != this)
            {
                return;
            }

            simulationActive = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            adapter?.Detach();
            globalWetnessBridge.Reset();
            activeOwner = null;
        }

        private void InitializeDomains()
        {
            adapter = adapterBehaviour as IEnvironmentPresentationAdapter;
            if (adapter == null)
            {
                throw new InvalidOperationException(
                    "Production environment adapter must implement " +
                    "IEnvironmentPresentationAdapter.");
            }

            sceneOwnershipGuard = adapterBehaviour as
                IEnvironmentPresentationSceneOwnershipGuard;
            if (sceneOwnershipGuard == null)
            {
                throw new InvalidOperationException(
                    "Production environment adapter must implement the " +
                    "additive scene ownership guard contract.");
            }

            materialWetnessBridge =
                wetnessBridgeBehaviour as IWetnessShaderBridge;
            weatherCatalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            gameTime = new GameTimeService();
            weather = new WeatherDirector(
                weatherCatalog,
                new WeatherSeed(initialWeatherSeed, 7UL),
                WeatherStateIds.Clear);
            wetness = new GlobalWetnessController(
                WetnessConfig.CreateRemakeDesignTarget());
            lightning = new LightningStrikeDirector(
                LightningStrikeConfig.CreateRemakeDesignTarget(),
                new WeatherSeed(initialWeatherSeed ^ 0xA17E57UL, 11UL));
            ResetAutomaticAmbientLightningGate();
            qualityTier = initialQuality;
            _ = ProductionEnvironmentQuality.GetStableId(qualityTier);
            exposureContext = WeatherExposureContext.Exterior;
            domainsInitialized = true;
        }

        private void AdvanceWetness(double gameDeltaSeconds)
        {
            WeatherState state = weather.CurrentState;
            wetness.Advance(new WetnessEnvironmentInputs(
                gameDeltaSeconds,
                state.WetnessInput01,
                state.WindSpeedMetersPerSecond,
                state.TemperatureCelsius,
                gameTime.Snapshot.IsDaylight ? 1f : 0f,
                state.DryingModifier,
                SurfaceExposureProfile.Exterior));
            ApplyWetnessOutputs();
        }

        private void ApplyWetnessOutputs()
        {
            WetnessEnvironmentOutputs outputs = wetness.Outputs;
            globalWetnessBridge.Apply(outputs);
            materialWetnessBridge?.Apply(outputs);
        }

        private EnvironmentPresentationStatus PresentCurrentState()
        {
            if (!adapter.IsAttached)
            {
                return adapter.Attach();
            }

            GameTimeSnapshot clock = gameTime.Snapshot;
            exposureContext = listener == null
                ? WeatherExposureContext.Exterior
                : ProductionShelterResolver.Resolve(
                    listener.position,
                    shelterVolumes);
            EnvironmentPresentationStatus adapterStatus = adapter.Status;
            var context = new WeatherEnvironmentOutputContext(
                new WeatherClockOutput(
                    clock.Date.Year,
                    clock.Date.Month,
                    clock.Date.Day,
                    clock.DayIndex,
                    ToNormalizedPresentationTime(
                        clock.NormalizedTimeOfDay01)),
                wetness.Outputs,
                exposureContext,
                new WeatherPresentationStatusOutput(
                    ProductionEnvironmentQuality.GetStableId(qualityTier),
                    MapPresentationHealth(adapterStatus.State),
                    adapterStatus.LastAppliedRevision > uint.MaxValue
                        ? uint.MaxValue
                        : (uint)adapterStatus.LastAppliedRevision));
            currentOutputs = weather.CreateEnvironmentOutputs(context);
            QueueCadencedEnvironmentRefresh(
                currentOutputs.Weather.PresentationBindingId);
            AmbientLightningResult? automaticLightning =
                QueueAutomaticAmbientLightningIfDue(
                    currentOutputs.Weather);

            float transitionDurationSeconds =
                ResolvePresentationTransitionDurationSeconds();

            presentationRevision = NextRevision(presentationRevision);
            if (!WeatherEnvironmentFrameMapper.TryMap(
                    currentOutputs,
                    presentationRevision,
                    qualityTier,
                    transitionDurationSeconds,
                    pendingLightningVisual,
                    pendingRefresh,
                    out EnvironmentPresentationFrame frame,
                    out string failure))
            {
                LastFailure =
                    "Production environment frame mapping failed: " + failure;
                throw new InvalidOperationException(LastFailure);
            }

            EnvironmentPresentationStatus result = adapter.Present(frame);
            if (result.IsOperational &&
                frame.EnvironmentRefresh.Targets !=
                EnvironmentRefreshTarget.None)
            {
                RecordEnvironmentRefreshAnchor(
                    currentOutputs.Weather.PresentationBindingId);
            }

            pendingTransitionDurationSeconds = float.NaN;
            pendingLightningVisual = EnvironmentLightningVisualRequest.None;
            pendingRefresh = EnvironmentRefreshRequest.None;
            DispatchSafely(
                EnvironmentOutputsChanged,
                currentOutputs,
                nameof(EnvironmentOutputsChanged));
            if (automaticLightning.HasValue)
            {
                PublishAmbientLightning(automaticLightning.Value);
            }

            return result;
        }

        private void QueueRefresh(EnvironmentRefreshTarget targets)
        {
            if (targets == EnvironmentRefreshTarget.None)
            {
                return;
            }

            targets |= pendingRefresh.Targets;
            refreshSequence = NextSequence(refreshSequence);
            pendingRefresh = new EnvironmentRefreshRequest(
                targets,
                refreshSequence);
        }

        private float ResolvePresentationTransitionDurationSeconds()
        {
            if (float.IsFinite(pendingTransitionDurationSeconds))
            {
                return pendingTransitionDurationSeconds;
            }

            // Overrides are explicit commands. Their one requested duration is
            // consumed on the frame that applies the override; subsequent frames
            // must not borrow the unrelated background schedule duration.
            if (weather.TryGetActiveOverride(out _))
            {
                return 0f;
            }

            WeatherTransition transition = weather.Timeline.Transition;
            double remainingGameSeconds = Math.Max(
                0d,
                transition.Front.TransitionDurationSeconds -
                transition.ElapsedSeconds);
            double gameSecondsPerSimulationSecond =
                86400d /
                gameTime.Config.DayLengthSimulationSeconds *
                gameTime.Snapshot.TimeScale;
            double simulationSeconds =
                remainingGameSeconds / gameSecondsPerSimulationSecond;
            return (float)Math.Min(simulationSeconds, float.MaxValue);
        }

        private void QueueCadencedEnvironmentRefresh(string bindingId)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                throw new InvalidOperationException(
                    "Production weather produced no presentation binding ID.");
            }

            if (!hasEnvironmentRefreshAnchor)
            {
                if (pendingRefresh.Targets == EnvironmentRefreshTarget.None)
                {
                    QueueRefresh(
                        EnvironmentRefreshTarget.Sky |
                        EnvironmentRefreshTarget.Ambient |
                        EnvironmentRefreshTarget.Reflections);
                }

                return;
            }

            EnvironmentRefreshTarget targets =
                EnvironmentRefreshTarget.None;
            if (!string.Equals(
                    bindingId,
                    refreshBindingId,
                    StringComparison.Ordinal))
            {
                targets |= EnvironmentRefreshTarget.Sky |
                           EnvironmentRefreshTarget.Ambient |
                           EnvironmentRefreshTarget.Reflections;
            }

            if (gameTime.CurrentGameTimeSeconds - refreshGameTimeSeconds >=
                EnvironmentRefreshGameTimeThresholdSeconds)
            {
                targets |= EnvironmentRefreshTarget.Sky |
                           EnvironmentRefreshTarget.Ambient |
                           EnvironmentRefreshTarget.Reflections;
            }

            if (listener != null &&
                (!hasRefreshListenerPosition ||
                 (listener.position - refreshListenerPosition).sqrMagnitude >=
                 EnvironmentRefreshListenerDistanceMeters *
                 EnvironmentRefreshListenerDistanceMeters))
            {
                targets |= EnvironmentRefreshTarget.Ambient |
                           EnvironmentRefreshTarget.Reflections;
            }

            QueueRefresh(targets);
        }

        private void RecordEnvironmentRefreshAnchor(string bindingId)
        {
            refreshBindingId = bindingId;
            refreshGameTimeSeconds = gameTime.CurrentGameTimeSeconds;
            hasEnvironmentRefreshAnchor = true;
            if (listener == null)
            {
                hasRefreshListenerPosition = false;
                return;
            }

            refreshListenerPosition = listener.position;
            hasRefreshListenerPosition = true;
        }

        private void ResetAutomaticAmbientLightningGate()
        {
            ambientLightningGraceUntilGameSeconds =
                lightning.SimulationSeconds +
                AmbientLightningSpawnGraceGameSeconds;
            nextAmbientLightningGameSeconds = double.PositiveInfinity;
            ambientLightningOrdinal = 0U;
            wasAmbientLightningEligible = false;
        }

        private AmbientLightningResult? QueueAutomaticAmbientLightningIfDue(
            in WeatherState state)
        {
            double now = lightning.SimulationSeconds;
            bool eligible =
                listener != null &&
                state.Id.Equals(WeatherStateIds.Thunderstorm) &&
                state.LightningRisk01 > 0f &&
                now >= ambientLightningGraceUntilGameSeconds &&
                lightning.IsGameplayStrikeAllowed;
            if (!eligible)
            {
                wasAmbientLightningEligible = false;
                nextAmbientLightningGameSeconds = double.PositiveInfinity;
                return null;
            }

            if (!wasAmbientLightningEligible)
            {
                wasAmbientLightningEligible = true;
                nextAmbientLightningGameSeconds = now +
                    CalculateAmbientLightningCooldownSeconds(
                        state.LightningRisk01,
                        ambientLightningOrdinal);
                return null;
            }

            if (now < nextAmbientLightningGameSeconds)
            {
                return null;
            }

            if (pendingLightningVisual.IsRequested)
            {
                nextAmbientLightningGameSeconds = now +
                    CalculateAmbientLightningCooldownSeconds(
                        state.LightningRisk01,
                        ambientLightningOrdinal);
                return null;
            }

            Vector3 target = CalculateAmbientLightningTarget(
                listener.position,
                ambientLightningOrdinal);
            AmbientLightningResult result = lightning.CreateAmbientLightning(
                target,
                state.LightningIntensity01);
            pendingLightningVisual = new EnvironmentLightningVisualRequest(
                true,
                result.Presentation.Sequence,
                result.Presentation.TargetWorldPosition,
                result.Presentation.Intensity01);
            ambientLightningOrdinal = NextSequence(ambientLightningOrdinal);
            nextAmbientLightningGameSeconds = now +
                CalculateAmbientLightningCooldownSeconds(
                    state.LightningRisk01,
                    ambientLightningOrdinal);
            return result;
        }

        private void PublishAmbientLightning(
            in AmbientLightningResult result)
        {
            PublishLightningOccurred(result.StrikeEvent);
            if (listener == null)
            {
                return;
            }

            double delaySeconds = lightning.CalculateThunderDelaySeconds(
                result.StrikeEvent.WorldPosition,
                listener.position);
            PublishThunderRequested(new ThunderAudioRequest(
                result.StrikeEvent.Sequence,
                result.StrikeEvent.WorldPosition,
                result.StrikeEvent.Intensity01,
                delaySeconds));
        }

        private void PublishLightningOccurred(
            in LightningStrikeEvent strikeEvent)
        {
            if (!publishedLightningSequences.Add(strikeEvent.Sequence))
            {
                return;
            }

            DispatchSafely(
                LightningOccurred,
                strikeEvent,
                nameof(LightningOccurred));
        }

        private void PublishThunderRequested(
            in ThunderAudioRequest request)
        {
            if (!publishedThunderSequences.Add(request.Sequence))
            {
                return;
            }

            DispatchSafely(
                ThunderRequested,
                request,
                nameof(ThunderRequested));
        }

        private void DispatchSafely<T>(
            Action<T> subscribers,
            T payload,
            string eventName)
        {
            if (subscribers == null)
            {
                return;
            }

            Delegate[] invocationList = subscribers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<T>)invocationList[index]).Invoke(payload);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Production environment subscriber failed for " +
                        eventName + ": " + exception.Message,
                        this);
                }
            }
        }

        private double CalculateAmbientLightningCooldownSeconds(
            float lightningRisk01,
            uint ordinal)
        {
            double riskWeighted =
                MaximumAmbientLightningCooldownGameSeconds -
                (MaximumAmbientLightningCooldownGameSeconds -
                 MinimumAmbientLightningCooldownGameSeconds) *
                Mathf.Clamp01(lightningRisk01);
            double jitter = 0.8d +
                            0.4d * DeterministicUnit01(ordinal, 0xC001D00DUL);
            return Math.Min(
                MaximumAmbientLightningCooldownGameSeconds,
                Math.Max(
                    MinimumAmbientLightningCooldownGameSeconds,
                    riskWeighted * jitter));
        }

        private Vector3 CalculateAmbientLightningTarget(
            Vector3 listenerWorldPosition,
            uint ordinal)
        {
            double angle =
                DeterministicUnit01(ordinal, 0xA17E57UL) *
                Math.PI * 2d;
            float radius = Mathf.Lerp(
                MinimumAmbientLightningRadiusMeters,
                MaximumAmbientLightningRadiusMeters,
                (float)DeterministicUnit01(ordinal, 0x51A7EUL));
            return listenerWorldPosition + new Vector3(
                (float)Math.Cos(angle) * radius,
                0f,
                (float)Math.Sin(angle) * radius);
        }

        private double DeterministicUnit01(uint ordinal, ulong salt)
        {
            ulong value = unchecked(
                initialWeatherSeed ^
                salt ^
                ((ulong)ordinal + 1UL) * 0x9E3779B97F4A7C15UL);
            value ^= value >> 30;
            value = unchecked(value * 0xBF58476D1CE4E5B9UL);
            value ^= value >> 27;
            value = unchecked(value * 0x94D049BB133111EBUL);
            value ^= value >> 31;
            return (value >> 11) * (1d / 9007199254740992d);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScheduleTopologyRevalidation();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            ScheduleTopologyRevalidation();
        }

        private void ScheduleTopologyRevalidation()
        {
            if (!Application.isPlaying)
            {
                RevalidatePresentationSceneOwnership();
                RebuildShelterRegistry();
                return;
            }

            if (!IsPrimaryOwner || !isActiveAndEnabled ||
                !gameObject.activeInHierarchy)
            {
                return;
            }

            if (topologyRevalidationRoutine == null)
            {
                topologyRevalidationRoutine = StartCoroutine(
                    RevalidateTopologyAfterSceneLifecycleSettles());
            }
        }

        private IEnumerator RevalidateTopologyAfterSceneLifecycleSettles()
        {
            // Scene callbacks run before deferred destruction and before all
            // newly loaded vendor components have completed Start. Validate
            // after the two following frames so rejected additive roots and
            // old-session WindZones are gone and the deferred production
            // backend has completed its first activation.
            yield return null;
            yield return null;
            topologyRevalidationRoutine = null;
            if (!IsPrimaryOwner || !domainsInitialized ||
                !gameObject.activeInHierarchy)
            {
                yield break;
            }

            RevalidatePresentationSceneOwnership();
            RebuildShelterRegistry();
        }

        private void CancelTopologyRevalidation()
        {
            if (topologyRevalidationRoutine == null)
            {
                return;
            }

            StopCoroutine(topologyRevalidationRoutine);
            topologyRevalidationRoutine = null;
        }

        private void RevalidatePresentationSceneOwnership()
        {
            EnvironmentPresentationStatus status =
                sceneOwnershipGuard.RevalidateSceneOwnership();
            if (status.State != EnvironmentPresentationState.Faulted)
            {
                return;
            }

            simulationActive = false;
            LastFailure = BuildAdapterFailure(
                "Production environment ownership revalidation failed after " +
                "an additive scene topology change.");
            Debug.LogError(LastFailure, this);
        }

        private void RebuildShelterRegistry()
        {
            shelterBuffer.Clear();
            shelterIds.Clear();
            var visitedRoots = new HashSet<int>();

            // GameCompositionRoot moves this controller and its authored
            // Bootstrap shelters into the special DontDestroyOnLoad scene.
            // That scene is not exposed through SceneManager.sceneCount, so
            // scan the explicit project-owned session root first.
            CollectSheltersFromRoot(
                transform.root.gameObject,
                visitedRoots);

            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0;
                     rootIndex < roots.Length;
                     rootIndex++)
                {
                    CollectSheltersFromRoot(
                        roots[rootIndex],
                        visitedRoots);
                }
            }

            shelterVolumes = shelterBuffer.ToArray();
        }

        private void CollectSheltersFromRoot(
            GameObject root,
            ISet<int> visitedRoots)
        {
            if (root == null || !visitedRoots.Add(root.GetInstanceID()))
            {
                return;
            }

            ProductionShelterVolumeAuthoring[] authored =
                root.GetComponentsInChildren<
                    ProductionShelterVolumeAuthoring>(true);
            for (int volumeIndex = 0;
                 volumeIndex < authored.Length;
                 volumeIndex++)
            {
                ProductionShelterVolumeAuthoring authoring =
                    authored[volumeIndex];
                if (!authoring.isActiveAndEnabled)
                {
                    continue;
                }

                if (!authoring.TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure))
                {
                    Debug.LogError(failure, authoring);
                    continue;
                }

                if (!shelterIds.Add(volume.StableId))
                {
                    Debug.LogError(
                        "Duplicate production shelter stable ID: " +
                        volume.StableId,
                        authoring);
                    continue;
                }

                shelterBuffer.Add(volume);
            }
        }

        private string BuildAdapterFailure(string prefix)
        {
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                adapter.Diagnostics;
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return prefix;
            }

            EnvironmentPresentationDiagnostic last =
                diagnostics[diagnostics.Count - 1];
            return prefix + " " + last.Code + ": " + last.Message;
        }

        private void EnsurePrimaryAndInitialized()
        {
            if (!IsPrimaryOwner)
            {
                throw new InvalidOperationException(
                    "The production environment instance is not the active owner.");
            }

            if (!domainsInitialized)
            {
                throw new InvalidOperationException(
                    "Production environment domains are not initialized.");
            }
        }

        private static float ToNormalizedPresentationTime(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException(
                    "Game clock produced non-finite normalized time.");
            }

            return Mathf.Clamp(
                (float)value,
                0f,
                MaximumNormalizedTimeBelowOne);
        }

        private static WeatherPresentationHealth MapPresentationHealth(
            EnvironmentPresentationState state)
        {
            switch (state)
            {
                case EnvironmentPresentationState.Ready:
                    return WeatherPresentationHealth.Ready;
                case EnvironmentPresentationState.Degraded:
                    return WeatherPresentationHealth.Degraded;
                case EnvironmentPresentationState.Detached:
                case EnvironmentPresentationState.Faulted:
                case EnvironmentPresentationState.Disabled:
                    return WeatherPresentationHealth.Unavailable;
                default:
                    return WeatherPresentationHealth.Unknown;
            }
        }

        private static ulong NextRevision(ulong current)
        {
            if (current == ulong.MaxValue)
            {
                throw new InvalidOperationException(
                    "Production presentation revision space is exhausted.");
            }

            return current + 1UL;
        }

        private static uint NextSequence(uint current)
        {
            if (current == uint.MaxValue)
            {
                throw new InvalidOperationException(
                    "Production refresh sequence space is exhausted.");
            }

            return current + 1U;
        }
    }
}
