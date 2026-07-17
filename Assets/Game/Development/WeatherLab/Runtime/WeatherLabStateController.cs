using System;
using System.Collections;
using MSC.Core.Time;
using MSC.Weather;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Development.WeatherLab
{
    /// <summary>
    /// Thin DEV facade over the authoritative 07B domains. Enviro remains a write-only
    /// presentation backend and is never queried as game state.
    /// </summary>
    public sealed class WeatherLabStateController : MonoBehaviour
    {
        private const string ManualOverrideId = "weatherlab.manual";
        private const int ManualOverridePriority = 1000;
        private const double ManualOverrideLifetimeSeconds = 1_000_000_000d;
        private const double MaximumManualAdvanceGameSeconds = 30d * 86400d;
        private const float PresentationIntervalSeconds = 0.25f;
        private const float MaximumNormalizedTimeBelowOne = 0.99999994f;

        [SerializeField] private MonoBehaviour adapterBehaviour;
        [SerializeField] private MonoBehaviour wetnessBridgeBehaviour;
        [SerializeField] private Camera[] presentationCameras = Array.Empty<Camera>();
        [SerializeField] private Transform lightningTarget;
        [SerializeField] private EnvironmentPresentationPresetKind initialPreset =
            EnvironmentPresentationPresetKind.Clear;
        [SerializeField] private EnvironmentQualityTier initialQuality =
            EnvironmentQualityTier.High;
        [SerializeField] private ulong initialWeatherSeed = 19950801UL;

        private IEnvironmentPresentationAdapter adapter;
        private IWetnessShaderBridge wetnessBridge;
        private WeatherProfileCatalog weatherCatalog;
        private GameTimeService gameTime;
        private WeatherDirector weatherDirector;
        private GlobalWetnessController wetnessController;
        private LightningStrikeDirector lightningDirector;
        private EnvironmentPresentationPresetKind currentPreset;
        private EnvironmentQualityTier currentQuality;
        private WeatherExposureContext exposureContext;
        private ulong presentationRevision;
        private uint refreshSequence;
        private float nextPresentationTime;
        private float pendingTransitionDurationSeconds;
        private EnvironmentLightningVisualRequest pendingLightningVisual;
        private EnvironmentRefreshRequest pendingRefresh;
        private string lastMappingFailure = string.Empty;
        private bool domainsInitialized;

        public EnvironmentPresentationPresetKind CurrentPreset => currentPreset;

        public EnvironmentQualityTier CurrentQuality => currentQuality;

        public EnvironmentPresentationStatus Status => adapter?.Status ??
            EnvironmentPresentationStatus.Detached;

        public IGameTimeService GameTime => gameTime;

        public IWeatherService Weather => weatherDirector;

        public WeatherState CurrentLogicalWeather => weatherDirector?.CurrentState ?? default;

        public WeatherTimeline CurrentTimeline => weatherDirector?.Timeline ?? default;

        public WetnessEnvironmentOutputs Wetness =>
            wetnessController?.Outputs ?? default;

        public bool IsScheduleFrozen => weatherDirector != null && weatherDirector.IsScheduleFrozen;

        public bool NonLethalLightning => lightningDirector == null || lightningDirector.NonLethalMode;

        public bool IsGameplayLightningAllowed =>
            lightningDirector != null && lightningDirector.IsGameplayStrikeAllowed;

        public ulong WeatherSeed => initialWeatherSeed;

        public WeatherExposureContext ExposureContext => exposureContext;

        public string LastMappingFailure => lastMappingFailure;

        public string BuildLightningDiagnostics()
        {
            EnsureInitialized();
            if (lightningTarget == null)
            {
                return "target=missing";
            }

            Vector3 target = lightningTarget.position;
            var protection = new[]
            {
                new LightningProtectionVolume(
                    "weatherlab.protection.building",
                    new Vector3(-10f, 1.5f, 10f),
                    new Vector3(5f, 2f, 4.5f),
                    0.05f)
            };
            var attractor = new LightningStrikeCandidate(
                "weatherlab.lightning.target",
                target,
                1f,
                12f,
                0.9f,
                false,
                LightningCandidateKind.Attractor);
            var openGround = new LightningStrikeCandidate(
                "weatherlab.lightning.open_ground",
                target + new Vector3(8f, 0f, 6f),
                1f,
                0f,
                0f,
                false);
            return
                $"allowed={lightningDirector.IsGameplayStrikeAllowed}; " +
                $"sequence={lightningDirector.Sequence}; " +
                $"targetWeight={lightningDirector.EvaluateCandidateWeight(attractor, protection):F3}; " +
                $"openGroundWeight={lightningDirector.EvaluateCandidateWeight(openGround, protection):F3}; " +
                $"nonLethal={lightningDirector.NonLethalMode}";
        }

        public void ConfigureForAuthoring(
            MonoBehaviour authoredAdapter,
            Camera[] authoredCameras,
            Transform authoredLightningTarget,
            MonoBehaviour authoredWetnessBridge = null)
        {
            adapterBehaviour = authoredAdapter;
            presentationCameras = authoredCameras ?? Array.Empty<Camera>();
            lightningTarget = authoredLightningTarget;
            wetnessBridgeBehaviour = authoredWetnessBridge;
            initialPreset = EnvironmentPresentationPresetKind.Clear;
            initialQuality = EnvironmentQualityTier.High;
            initialWeatherSeed = 19950801UL;
        }

        public EnvironmentPresentationStatus ApplyPreset(
            EnvironmentPresentationPresetKind preset)
        {
            EnsureInitialized();
            currentPreset = preset;
            WeatherStateId stateId = MapPresetToLogicalState(preset);
            if (preset == EnvironmentPresentationPresetKind.Night)
            {
                TrySetDateAndTime(
                    gameTime.Snapshot.Date,
                    secondsOfDay: 23d * 3600d + 2d * 60d,
                    out _);
            }
            else if (preset == EnvironmentPresentationPresetKind.Mist)
            {
                TrySetDateAndTime(
                    gameTime.Snapshot.Date,
                    secondsOfDay: 6d * 3600d + 43d * 60d,
                    out _);
            }

            SetManualWeather(stateId, transitionDurationSeconds: 0f);
            return PresentCurrentState();
        }

        public bool TryApplyLogicalWeather(
            string stableStateId,
            float transitionDurationSeconds,
            out string failure)
        {
            EnsureInitialized();
            if (!WeatherStateId.TryCreate(stableStateId, out WeatherStateId stateId) ||
                !weatherCatalog.Contains(stateId))
            {
                failure = "Unknown logical weather state ID.";
                return false;
            }

            if (!float.IsFinite(transitionDurationSeconds) || transitionDurationSeconds < 0f)
            {
                failure = "Transition duration must be finite and non-negative.";
                return false;
            }

            SetManualWeather(stateId, transitionDurationSeconds);
            PresentCurrentState();
            failure = string.Empty;
            return true;
        }

        public bool RemoveManualWeatherOverride()
        {
            EnsureInitialized();
            WeatherState before = weatherDirector.CurrentState;
            bool removed = weatherDirector.RemoveOverride(ManualOverrideId);
            if (removed)
            {
                QueueAutomaticTransitionIfBindingChanged(
                    before,
                    weatherDirector.CurrentState);
                QueueRefresh(EnvironmentRefreshTarget.Sky | EnvironmentRefreshTarget.Ambient);
                PresentCurrentState();
            }

            return removed;
        }

        public EnvironmentPresentationStatus ApplyQuality(EnvironmentQualityTier quality)
        {
            EnsureInitialized();
            currentQuality = quality;
            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient |
                EnvironmentRefreshTarget.Reflections);
            return PresentCurrentState();
        }

        public EnvironmentPresentationStatus TriggerAmbientLightning()
        {
            EnsureInitialized();
            if (lightningTarget == null)
            {
                throw new InvalidOperationException("WeatherLab lightning target is not assigned.");
            }

            AmbientLightningResult result = lightningDirector.CreateAmbientLightning(
                lightningTarget.position,
                1f);
            pendingLightningVisual = ToPresentationRequest(result.Presentation);
            return PresentCurrentState();
        }

        public bool TriggerGameplayLightningAtTarget(out GameplayLightningResult result)
        {
            EnsureInitialized();
            if (lightningTarget == null)
            {
                result = default;
                return false;
            }

            Vector3 target = lightningTarget.position;
            var candidates = new[]
            {
                new LightningStrikeCandidate(
                    "weatherlab.lightning.target",
                    target,
                    1f,
                    12f,
                    0.9f,
                    isProtected: false,
                    LightningCandidateKind.Attractor),
                new LightningStrikeCandidate(
                    "weatherlab.lightning.open_ground",
                    target + new Vector3(8f, 0f, 6f),
                    1f,
                    0f,
                    0f,
                    isProtected: false),
                new LightningStrikeCandidate(
                    "weatherlab.lightning.vehicle_proxy",
                    target + new Vector3(-5f, 0f, -9f),
                    0.82f,
                    1.6f,
                    0.25f,
                    isProtected: false)
            };
            var protection = new[]
            {
                new LightningProtectionVolume(
                    "weatherlab.protection.building",
                    new Vector3(-10f, 1.5f, 10f),
                    new Vector3(5f, 2f, 4.5f),
                    0.05f)
            };
            Vector3 listener = GetActiveCameraPosition();
            if (!lightningDirector.TryCreateGameplayStrike(
                    candidates,
                    protection,
                    listener,
                    1f,
                    out result))
            {
                return false;
            }

            pendingLightningVisual = ToPresentationRequest(result.Presentation);
            PresentCurrentState();
            return true;
        }

        public void SetNonLethalLightning(bool enabled)
        {
            EnsureInitialized();
            lightningDirector.SetNonLethalMode(enabled);
        }

        public void SetScheduleFrozen(bool frozen)
        {
            EnsureInitialized();
            weatherDirector.SetScheduleFrozen(frozen);
        }

        public void SetPaused(bool paused)
        {
            EnsureInitialized();
            gameTime.SetPaused(paused);
            PresentCurrentState();
        }

        public void SetTimeScale(double timeScale)
        {
            EnsureInitialized();
            gameTime.SetTimeScale(timeScale);
        }

        public bool TryAdvanceGameSeconds(double gameSeconds, out string failure)
        {
            EnsureInitialized();
            if (!double.IsFinite(gameSeconds) || gameSeconds <= 0d)
            {
                failure = "Advance amount must be finite and positive.";
                return false;
            }

            if (gameSeconds > MaximumManualAdvanceGameSeconds)
            {
                failure = "A single DEV advance is limited to 30 game days.";
                return false;
            }

            double gameSecondsPerSimulationSecond =
                86400d / gameTime.Config.DayLengthSimulationSeconds *
                gameTime.Snapshot.TimeScale;
            double simulationDelta = gameSeconds / gameSecondsPerSimulationSecond;
            if (!TryValidateManualAdvance(gameSeconds, simulationDelta, out failure))
            {
                return false;
            }

            try
            {
                gameTime.AdvanceWhileRetainingPause(simulationDelta);
            }
            catch (GameTimeNotificationException exception)
            {
                failure = exception.Message;
                return false;
            }

            WeatherState previousWeather = weatherDirector.CurrentState;
            // Every remaining step was executed successfully on isolated checkpoints above.
            weatherDirector.Advance(gameSeconds);
            lightningDirector.Advance(gameSeconds);
            WeatherState weather = weatherDirector.CurrentState;
            QueueAutomaticTransitionIfBindingChanged(previousWeather, weather);
            AdvanceWetness(gameSeconds, weather);

            PresentCurrentState();
            failure = string.Empty;
            return true;
        }

        public EnvironmentPresentationStatus RefreshPresentation()
        {
            EnsureInitialized();
            return PresentCurrentState();
        }

        public bool TrySetDateAndTime(
            GameDate date,
            double secondsOfDay,
            out string failure)
        {
            EnsureInitialized();
            if (!GameDate.TryCreate(date.Year, date.Month, date.Day, out _))
            {
                failure = "Game date must be a valid Gregorian date.";
                return false;
            }

            if (!double.IsFinite(secondsOfDay) || secondsOfDay < 0d || secondsOfDay >= 86400d)
            {
                failure = "Seconds of day must be finite and in [0, 86400).";
                return false;
            }

            long days = gameTime.Config.StartDate.DaysUntil(date);
            long timeOfDayTicks = checked((long)Math.Round(
                secondsOfDay * GameTimeConfig.TicksPerGameSecond,
                MidpointRounding.AwayFromZero));
            long elapsedTicks;
            try
            {
                elapsedTicks = checked(
                    days * GameTimeConfig.TicksPerGameDay +
                    timeOfDayTicks -
                    gameTime.Config.StartTimeOfDayTicks);
            }
            catch (OverflowException)
            {
                failure = "Requested game date is outside the supported range.";
                return false;
            }

            if (elapsedTicks < 0 || elapsedTicks > gameTime.Config.MaximumElapsedGameTicks)
            {
                failure = "Requested date/time is before the configured start or outside the supported range.";
                return false;
            }

            GameTimeSaveDto dto = gameTime.CaptureDto();
            dto.elapsedGameTicks = elapsedTicks;
            dto.fractionalGameTickRemainder = 0d;
            dto.dayIndex = (gameTime.Config.StartTimeOfDayTicks + elapsedTicks) /
                           GameTimeConfig.TicksPerGameDay;
            dto.year = date.Year;
            dto.month = date.Month;
            dto.day = date.Day;
            dto.timeOfDayTicks = timeOfDayTicks;
            if (!gameTime.TryRestoreDto(dto, out failure))
            {
                return false;
            }

            QueueRefresh(EnvironmentRefreshTarget.Sky | EnvironmentRefreshTarget.Ambient);
            PresentCurrentState();
            return true;
        }

        public void SetWetness(
            float ground01,
            float road01,
            float puddle01,
            float vegetation01)
        {
            EnsureInitialized();
            wetnessController.SetState(new WetnessState(
                ground01,
                road01,
                puddle01,
                vegetation01));
            wetnessBridge?.Apply(wetnessController.Outputs);
        }

        public void SetExposure(WeatherExposureContext context)
        {
            EnsureInitialized();
            switch (context)
            {
                case WeatherExposureContext.Exterior:
                case WeatherExposureContext.Sheltered:
                case WeatherExposureContext.Interior:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context), context, null);
            }

            // This context describes the listener/capture point only. The one global
            // WeatherLab surface state remains exterior and cannot be frozen merely by
            // switching to the indoor camera.
            exposureContext = context;
        }

        public void ResetWeatherSeed(ulong seed)
        {
            EnsureInitialized();
            WeatherStateId visibleState = weatherDirector.CurrentState.Id;
            weatherDirector = new WeatherDirector(
                weatherCatalog,
                new WeatherSeed(seed, 7UL),
                WeatherStateIds.Clear);
            initialWeatherSeed = seed;
            SetManualWeather(visibleState, 0f);
            QueueRefresh(EnvironmentRefreshTarget.Sky | EnvironmentRefreshTarget.Ambient);
            PresentCurrentState();
        }

        public bool SwitchCamera(int cameraIndex)
        {
            EnsureInitialized();
            if (presentationCameras == null || cameraIndex < 0 ||
                cameraIndex >= presentationCameras.Length ||
                presentationCameras[cameraIndex] == null)
            {
                return false;
            }

            for (int index = 0; index < presentationCameras.Length; index++)
            {
                if (presentationCameras[index] != null)
                {
                    presentationCameras[index].enabled = index == cameraIndex;
                }
            }

            SetExposure(cameraIndex == 0
                ? WeatherExposureContext.Exterior
                : WeatherExposureContext.Interior);
            return adapter is IEnvironmentPresentationCameraTarget cameraTarget &&
                   cameraTarget.TrySetPresentationCamera(presentationCameras[cameraIndex]);
        }

        private void Awake()
        {
            EnsureInitialized();
            currentPreset = initialPreset;
            currentQuality = initialQuality;
        }

        private IEnumerator Start()
        {
            const int maximumStartupFrames = 8;
            for (int attempt = 0; attempt < maximumStartupFrames && !adapter.IsAttached; attempt++)
            {
                adapter.Attach();
                if (!adapter.IsAttached)
                {
                    yield return null;
                }
            }

            if (adapter.IsAttached)
            {
                SwitchCamera(0);
                SetManualWeather(MapPresetToLogicalState(initialPreset), 0f);
                PresentCurrentState();
            }
        }

        private void Update()
        {
            if (!domainsInitialized)
            {
                return;
            }

            double previousGameSeconds = gameTime.CurrentGameTimeSeconds;
            gameTime.Advance(UnityEngine.Time.unscaledDeltaTime);
            double gameDeltaSeconds = gameTime.CurrentGameTimeSeconds - previousGameSeconds;
            if (gameDeltaSeconds <= 0d)
            {
                return;
            }

            WeatherState previousWeather = weatherDirector.CurrentState;
            weatherDirector.Advance(gameDeltaSeconds);
            lightningDirector.Advance(gameDeltaSeconds);
            WeatherState weather = weatherDirector.CurrentState;
            QueueAutomaticTransitionIfBindingChanged(previousWeather, weather);
            AdvanceWetness(gameDeltaSeconds, weather);

            if (UnityEngine.Time.unscaledTime >= nextPresentationTime)
            {
                nextPresentationTime = UnityEngine.Time.unscaledTime + PresentationIntervalSeconds;
                PresentCurrentState();
            }
        }

        private void OnDisable()
        {
            adapter?.Detach();
        }

        private void EnsureInitialized()
        {
            if (domainsInitialized)
            {
                return;
            }

            adapter = adapterBehaviour as IEnvironmentPresentationAdapter;
            if (adapter == null)
            {
                throw new InvalidOperationException(
                    "WeatherLab adapter must implement IEnvironmentPresentationAdapter.");
            }

            wetnessBridge = wetnessBridgeBehaviour as IWetnessShaderBridge;
            weatherCatalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            gameTime = new GameTimeService();
            weatherDirector = new WeatherDirector(
                weatherCatalog,
                new WeatherSeed(initialWeatherSeed, 7UL),
                WeatherStateIds.Clear);
            wetnessController = new GlobalWetnessController(
                WetnessConfig.CreateRemakeDesignTarget());
            lightningDirector = new LightningStrikeDirector(
                LightningStrikeConfig.CreateRemakeDesignTarget(),
                new WeatherSeed(initialWeatherSeed ^ 0xA17E57UL, 11UL));
            exposureContext = WeatherExposureContext.Exterior;
            currentQuality = initialQuality;
            currentPreset = initialPreset;
            domainsInitialized = true;
        }

        private bool TryValidateManualAdvance(
            double gameSeconds,
            double simulationDelta,
            out string failure)
        {
            try
            {
                // Validate every bounded domain step against isolated checkpoints before
                // mutating the live WeatherLab services. This prevents the schedule safety
                // bound or calendar range from producing a partial DEV time jump.
                var timeProbe = new GameTimeService(gameTime.Config);
                if (!timeProbe.TryRestoreDto(gameTime.CaptureDto(), out failure))
                {
                    return false;
                }

                timeProbe.AdvanceWhileRetainingPause(simulationDelta);

                var weatherProbe = new WeatherDirector(
                    weatherCatalog,
                    new WeatherSeed(initialWeatherSeed, 7UL),
                    WeatherStateIds.Clear);
                weatherProbe.Restore(weatherDirector.CaptureSnapshot());
                weatherProbe.Advance(gameSeconds);
                if (!double.IsFinite(lightningDirector.SimulationSeconds + gameSeconds))
                {
                    failure = "Lightning simulation time would overflow.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                failure = exception.Message;
                return false;
            }
        }

        private void AdvanceWetness(double gameDeltaSeconds, in WeatherState weather)
        {
            float sunIntensity = gameTime.Snapshot.IsDaylight ? 1f : 0f;
            WetnessEnvironmentOutputs wetness = wetnessController.Advance(
                new WetnessEnvironmentInputs(
                    gameDeltaSeconds,
                    weather.WetnessInput01,
                    weather.WindSpeedMetersPerSecond,
                    weather.TemperatureCelsius,
                    sunIntensity,
                    weather.DryingModifier,
                    SurfaceExposureProfile.Exterior));
            wetnessBridge?.Apply(wetness);
        }

        private void QueueAutomaticTransitionIfBindingChanged(
            in WeatherState before,
            in WeatherState after)
        {
            if (string.Equals(
                    before.PresentationBindingId,
                    after.PresentationBindingId,
                    StringComparison.Ordinal))
            {
                return;
            }

            WeatherTransition transition = weatherDirector.Timeline.Transition;
            double remainingGameSeconds = Math.Max(
                0d,
                transition.Front.TransitionDurationSeconds - transition.ElapsedSeconds);
            // A coarse DEV advance may jump across the complete logical blend in
            // one step. Keep the adapter on its smooth path for at least one
            // presentation interval instead of issuing a visible instant switch.
            pendingTransitionDurationSeconds = Math.Max(
                PresentationIntervalSeconds,
                ConvertGameDurationToSimulationSeconds(remainingGameSeconds));
        }

        private float ConvertGameDurationToSimulationSeconds(double gameDurationSeconds)
        {
            if (gameDurationSeconds <= 0d)
            {
                return 0f;
            }

            double gameSecondsPerSimulationSecond =
                gameTime.Config.GameTicksPerSimulationSecondAtScaleOne /
                (double)GameTimeConfig.TicksPerGameSecond *
                gameTime.Snapshot.TimeScale;
            double simulationDurationSeconds =
                gameDurationSeconds / gameSecondsPerSimulationSecond;
            return simulationDurationSeconds >= float.MaxValue
                ? float.MaxValue
                : (float)simulationDurationSeconds;
        }

        private void SetManualWeather(
            WeatherStateId stateId,
            float transitionDurationSeconds)
        {
            weatherDirector.RemoveOverride(ManualOverrideId);
            double now = weatherDirector.SimulationSeconds;
            weatherDirector.AddOverride(new WeatherOverride(
                ManualOverrideId,
                "WeatherLab DEV",
                "Explicit logical state selected for validation/capture.",
                ManualOverridePriority,
                now,
                now + ManualOverrideLifetimeSeconds,
                stateId,
                WeatherOverrideSerializationPolicy.Transient));
            pendingTransitionDurationSeconds = transitionDurationSeconds;
            QueueRefresh(EnvironmentRefreshTarget.Sky | EnvironmentRefreshTarget.Ambient);
        }

        private EnvironmentPresentationStatus PresentCurrentState()
        {
            if (!adapter.IsAttached)
            {
                EnvironmentPresentationStatus attached = adapter.Attach();
                if (!attached.IsOperational || !adapter.IsAttached)
                {
                    return attached;
                }
            }

            GameTimeSnapshot clock = gameTime.Snapshot;
            EnvironmentPresentationStatus currentStatus = adapter.Status;
            var context = new WeatherEnvironmentOutputContext(
                new WeatherClockOutput(
                    clock.Date.Year,
                    clock.Date.Month,
                    clock.Date.Day,
                    clock.DayIndex,
                    ToPresentationNormalizedTime(clock.NormalizedTimeOfDay01)),
                wetnessController.Outputs,
                exposureContext,
                new WeatherPresentationStatusOutput(
                    currentQuality == EnvironmentQualityTier.Low
                        ? "quality.low"
                        : "quality.high",
                    MapPresentationHealth(currentStatus.State),
                    currentStatus.LastAppliedRevision > uint.MaxValue
                        ? uint.MaxValue
                        : (uint)currentStatus.LastAppliedRevision));
            WeatherEnvironmentOutputs outputs = weatherDirector.CreateEnvironmentOutputs(context);

            presentationRevision = NextRevision(presentationRevision);
            if (!WeatherEnvironmentFrameMapper.TryMap(
                    outputs,
                    presentationRevision,
                    currentQuality,
                    pendingTransitionDurationSeconds,
                    pendingLightningVisual,
                    pendingRefresh,
                    out EnvironmentPresentationFrame frame,
                    out lastMappingFailure))
            {
                throw new InvalidOperationException(
                    "WeatherLab could not create a presentation frame: " + lastMappingFailure);
            }

            EnvironmentPresentationStatus result = adapter.Present(frame);
            pendingTransitionDurationSeconds = 0f;
            pendingLightningVisual = EnvironmentLightningVisualRequest.None;
            pendingRefresh = EnvironmentRefreshRequest.None;
            return result;
        }

        private void QueueRefresh(EnvironmentRefreshTarget targets)
        {
            refreshSequence = NextSequence(refreshSequence);
            pendingRefresh = new EnvironmentRefreshRequest(targets, refreshSequence);
        }

        private Vector3 GetActiveCameraPosition()
        {
            if (presentationCameras != null)
            {
                for (int index = 0; index < presentationCameras.Length; index++)
                {
                    if (presentationCameras[index] != null && presentationCameras[index].enabled)
                    {
                        return presentationCameras[index].transform.position;
                    }
                }
            }

            return lightningTarget != null ? lightningTarget.position : Vector3.zero;
        }

        private static EnvironmentLightningVisualRequest ToPresentationRequest(
            in LightningPresentationRequest request) =>
            new EnvironmentLightningVisualRequest(
                true,
                request.Sequence,
                request.TargetWorldPosition,
                request.Intensity01);

        private static float ToPresentationNormalizedTime(double normalizedTimeOfDay01)
        {
            float value = (float)normalizedTimeOfDay01;
            if (value >= 1f)
            {
                return MaximumNormalizedTimeBelowOne;
            }

            return Mathf.Max(0f, value);
        }

        private static WeatherStateId MapPresetToLogicalState(
            EnvironmentPresentationPresetKind preset)
        {
            switch (preset)
            {
                case EnvironmentPresentationPresetKind.Clear:
                case EnvironmentPresentationPresetKind.Night:
                    return WeatherStateIds.Clear;
                case EnvironmentPresentationPresetKind.Overcast:
                    return WeatherStateIds.Overcast;
                case EnvironmentPresentationPresetKind.Rain:
                    return WeatherStateIds.SteadyRain;
                case EnvironmentPresentationPresetKind.Storm:
                    return WeatherStateIds.Thunderstorm;
                case EnvironmentPresentationPresetKind.Mist:
                    return WeatherStateIds.MorningMist;
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
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
                case EnvironmentPresentationState.Disabled:
                    return WeatherPresentationHealth.Unavailable;
                default:
                    return WeatherPresentationHealth.Unknown;
            }
        }

        private static ulong NextRevision(ulong value)
        {
            if (value == ulong.MaxValue)
            {
                throw new InvalidOperationException("WeatherLab presentation revision space is exhausted.");
            }

            return value + 1UL;
        }

        private static uint NextSequence(uint value)
        {
            if (value == uint.MaxValue)
            {
                throw new InvalidOperationException("WeatherLab request sequence space is exhausted.");
            }

            return value + 1U;
        }
    }
}
