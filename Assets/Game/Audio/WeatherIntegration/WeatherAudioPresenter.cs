using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Production;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Audio.WeatherIntegration
{
    /// <summary>
    /// Vendor-neutral consumer of project weather outputs. Enviro remains a
    /// visual backend and is never queried from this audio layer.
    /// </summary>
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class WeatherAudioPresenter : MonoBehaviour
    {
        private static readonly ProfilerMarker BridgeMarker =
            new ProfilerMarker("MSC.WwiseWeatherBridge");

        public const float RainLoopStartThreshold = 0.01f;
        public const float RainLoopStopThreshold = 0.005f;
        public const float WindLoopStartThreshold = 0.18f;
        public const float WindLoopStopThreshold = 0.12f;

        [SerializeField] private ProductionEnvironmentController environmentController;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring ambienceEmitter;
        [SerializeField] private AudioListenerContextPresenter listenerPresenter;
        [SerializeField] private WeatherExposureResolver exposureResolver;

        private IAudioBackend backend;
        private IAudioEventHandle rainHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle windHandle = AudioEventHandles.Invalid;
        private AudioListenerSpace activeRainSpace = (AudioListenerSpace)(-1);
        private string activeBackendId = string.Empty;
        private uint latestLightningSequence;
        private uint latestThunderSequence;
        private bool subscribed;
        private bool playbackPaused;
        private float precipitationIntensity01;
        private float windIntensity01;
        private float normalizedDayTime01 = 0.5f;

        public bool IsInitialized => backend != null && environmentController != null;

        public void Configure(
            ProductionEnvironmentController controller,
            MonoBehaviour audioBackend,
            AudioEmitterAuthoring weatherEmitter,
            AudioListenerContextPresenter listener = null,
            WeatherExposureResolver weatherExposureResolver = null)
        {
            Unsubscribe();
            StopLoop(ref rainHandle);
            StopLoop(ref windHandle);
            activeRainSpace = (AudioListenerSpace)(-1);
            activeBackendId = string.Empty;
            environmentController = controller;
            backendComponent = audioBackend;
            ambienceEmitter = weatherEmitter;
            listenerPresenter = listener;
            exposureResolver = weatherExposureResolver;
            backend = backendComponent as IAudioBackend;
            playbackPaused = IsPlaybackPaused();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public bool TryValidate(out string failure)
        {
            if (environmentController == null)
            {
                failure = "ProductionEnvironmentController is not assigned.";
                return false;
            }

            if (!(backendComponent is IAudioBackend))
            {
                failure = "Weather audio backend does not implement IAudioBackend.";
                return false;
            }

            if (ambienceEmitter != null && !ambienceEmitter.TryValidate(out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void Awake()
        {
            backend = backendComponent as IAudioBackend;
            if (!TryValidate(out string failure))
            {
                Debug.LogError("Weather audio presenter disabled: " + failure, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            if (backend == null)
            {
                return;
            }

            bool shouldPause = IsPlaybackPaused();
            if (shouldPause)
            {
                if (!playbackPaused)
                {
                    playbackPaused = true;
                    StopLoop(ref rainHandle);
                    StopLoop(ref windHandle);
                    activeRainSpace = (AudioListenerSpace)(-1);
                }

                return;
            }

            if (playbackPaused)
            {
                playbackPaused = false;
                activeRainSpace = (AudioListenerSpace)(-1);
                if (backend.IsReady)
                {
                    WeatherEnvironmentOutputs resumed =
                        environmentController.CurrentOutputs;
                    if (resumed.IsValid)
                    {
                        ApplyEnvironmentOutputs(resumed);
                        return;
                    }
                }
            }

            if (!backend.IsReady)
            {
                return;
            }

            if (ReconcileBackendIdentity())
            {
                WeatherEnvironmentOutputs current = environmentController.CurrentOutputs;
                if (current.IsValid)
                {
                    ApplyEnvironmentOutputs(current);
                    return;
                }
            }

            AudioListenerSpace space = listenerPresenter != null
                ? listenerPresenter.CurrentEnvironment.ListenerSpace
                : activeRainSpace == (AudioListenerSpace)(-1)
                    ? AudioListenerSpace.Exterior
                    : activeRainSpace;
            UpdateRainLoop(space);
            UpdateWindLoop();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopLoop(ref rainHandle);
            StopLoop(ref windHandle);
            activeRainSpace = (AudioListenerSpace)(-1);
            activeBackendId = string.Empty;
            playbackPaused = false;
        }

        private void Subscribe()
        {
            if (subscribed || environmentController == null)
            {
                return;
            }

            environmentController.EnvironmentOutputsChanged += HandleEnvironmentOutputs;
            environmentController.LightningOccurred += HandleLightning;
            environmentController.ThunderRequested += HandleThunder;
            if (exposureResolver != null)
            {
                exposureResolver.ExposureChanged += HandleExposureChanged;
            }
            subscribed = true;

            WeatherEnvironmentOutputs current = environmentController.CurrentOutputs;
            if (current.IsValid)
            {
                HandleEnvironmentOutputs(current);
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed || environmentController == null)
            {
                return;
            }

            environmentController.EnvironmentOutputsChanged -= HandleEnvironmentOutputs;
            environmentController.LightningOccurred -= HandleLightning;
            environmentController.ThunderRequested -= HandleThunder;
            if (exposureResolver != null)
            {
                exposureResolver.ExposureChanged -= HandleExposureChanged;
            }
            subscribed = false;
        }

        private void HandleEnvironmentOutputs(WeatherEnvironmentOutputs outputs)
        {
            if (backend == null || !backend.IsReady || !outputs.IsValid ||
                IsPlaybackPaused())
            {
                return;
            }

            ReconcileBackendIdentity();
            ApplyEnvironmentOutputs(outputs);
        }

        private void ApplyEnvironmentOutputs(WeatherEnvironmentOutputs outputs)
        {
            using (BridgeMarker.Auto())
            {
                ApplyEnvironmentOutputsUnprofiled(outputs);
            }
        }

        private void ApplyEnvironmentOutputsUnprofiled(
            WeatherEnvironmentOutputs outputs)
        {
            // This method deliberately replays the complete weather state. The
            // router can switch from Unity fallback to Wwise after SoundBanks
            // become ready, and backend-local RTPC/switch state must not be
            // assumed to survive that handoff.

            backend.SetParameter(
                AudioProjectIds.Parameters.WeatherPrecipitation,
                outputs.Audio.PrecipitationIntensity01,
                ambienceEmitter);
            backend.SetParameter(
                AudioProjectIds.Parameters.WeatherWind,
                outputs.Audio.WindIntensity01,
                ambienceEmitter);
            backend.SetParameter(
                AudioProjectIds.Parameters.WeatherThunderRisk,
                outputs.Audio.ThunderRisk01,
                ambienceEmitter);
            backend.SetSwitch(
                AudioProjectIds.Switches.WeatherPrecipitationGroup,
                MapPrecipitation(outputs.Audio.PrecipitationType),
                ambienceEmitter);

            precipitationIntensity01 =
                FiniteClamp01(outputs.Audio.PrecipitationIntensity01);
            windIntensity01 = FiniteClamp01(outputs.Audio.WindIntensity01);
            normalizedDayTime01 = FiniteClamp01(outputs.Clock.NormalizedDayTime01);

            if (exposureResolver != null)
            {
                ApplyExposureParameters(exposureResolver.Current);
            }
            else
            {
                listenerPresenter?.SetWeatherContext(
                    precipitationIntensity01,
                    windIntensity01,
                    normalizedDayTime01,
                    MapExposure(outputs.ExposureContext));
            }

            // Use the current project-owned production shelter output
            // immediately. AudioListenerContextPresenter applies the same value
            // during LateUpdate and lets explicit authored audio zones override
            // it when present.
            AudioListenerSpace space = MapExposure(outputs.ExposureContext);
            UpdateRainLoop(space);
            UpdateWindLoop();
        }

        private void HandleExposureChanged(WeatherExposureState exposure)
        {
            if (backend == null || !backend.IsReady || IsPlaybackPaused())
            {
                return;
            }

            ApplyExposureParameters(exposure);
            UpdateRainLoop(MapExposure(exposure));
        }

        private void ApplyExposureParameters(in WeatherExposureState exposure)
        {
            float audioShelter = CalculateExistingShelterParameter(exposure);
            float obstruction =
                exposure.EnclosureFactor * (1f - exposure.PortalExposure);
            backend.SetParameter(
                AudioProjectIds.Parameters.WeatherPrecipitation,
                CalculateAudiblePrecipitation(
                    precipitationIntensity01,
                    exposure),
                ambienceEmitter);
            backend.SetParameter(
                AudioProjectIds.Parameters.EnvironmentShelter,
                audioShelter,
                ambienceEmitter);
            listenerPresenter?.SetWeatherExposureContext(
                precipitationIntensity01,
                windIntensity01,
                normalizedDayTime01,
                MapExposure(exposure),
                audioShelter,
                obstruction,
                exposure.EnclosureFactor * 0.35f);
        }

        private void HandleLightning(LightningStrikeEvent strike)
        {
            if (backend == null || !backend.IsReady || IsPlaybackPaused() ||
                strike.Sequence <= latestLightningSequence)
            {
                return;
            }

            ReplayCurrentOutputsAfterBackendChange();

            latestLightningSequence = strike.Sequence;
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningIntensity,
                strike.Intensity01);
        }

        private void HandleThunder(ThunderAudioRequest thunder)
        {
            if (backend == null || !backend.IsReady || IsPlaybackPaused() ||
                thunder.Sequence <= latestThunderSequence)
            {
                return;
            }

            ReplayCurrentOutputsAfterBackendChange();

            latestThunderSequence = thunder.Sequence;
            AudioRuntimeSnapshot snapshot = backend.CaptureSnapshot();
            float distance = snapshot.Listener.IsValid
                ? Vector3.Distance(snapshot.Listener.WorldPosition, thunder.WorldPosition)
                : 0f;
            backend.SetParameter(AudioProjectIds.Parameters.LightningIntensity, thunder.Intensity01);
            backend.SetParameter(AudioProjectIds.Parameters.LightningDistance, distance);
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningDelay,
                (float)thunder.DelaySeconds);
            AudioEventRequest request = MapThunderRequest(in thunder);
            backend.PostEvent(in request);
        }

        private void UpdateRainLoop(AudioListenerSpace space)
        {
            bool isActive = rainHandle != null && rainHandle.IsValid && rainHandle.IsPlaying;
            if (!ShouldKeepLoopActive(
                    precipitationIntensity01,
                    isActive,
                    RainLoopStartThreshold,
                    RainLoopStopThreshold))
            {
                StopLoop(ref rainHandle);
                activeRainSpace = (AudioListenerSpace)(-1);
                return;
            }

            AudioListenerSpace eventSpace = exposureResolver != null
                ? AudioListenerSpace.Exterior
                : space;
            if (activeRainSpace == eventSpace && isActive)
            {
                return;
            }

            StopLoop(ref rainHandle);
            activeRainSpace = eventSpace;
            AudioEventId eventId;
            switch (eventSpace)
            {
                case AudioListenerSpace.Interior:
                case AudioListenerSpace.VehicleInterior:
                    eventId = AudioProjectIds.Events.WeatherRainInterior;
                    break;
                case AudioListenerSpace.Sheltered:
                    eventId = AudioProjectIds.Events.WeatherRainSheltered;
                    break;
                default:
                    eventId = AudioProjectIds.Events.WeatherRainExterior;
                    break;
            }

            var request = new AudioEventRequest(
                eventId,
                ambienceEmitter,
                allowMultiple: false);
            rainHandle = backend.PostEvent(in request) ?? AudioEventHandles.Invalid;
        }

        private void UpdateWindLoop()
        {
            bool isActive = windHandle != null && windHandle.IsValid && windHandle.IsPlaying;
            if (!ShouldKeepLoopActive(
                    windIntensity01,
                    isActive,
                    WindLoopStartThreshold,
                    WindLoopStopThreshold))
            {
                StopLoop(ref windHandle);
                return;
            }

            if (isActive)
            {
                return;
            }

            var request = new AudioEventRequest(
                AudioProjectIds.Events.WeatherWind,
                ambienceEmitter,
                allowMultiple: false);
            windHandle = backend.PostEvent(in request) ?? AudioEventHandles.Invalid;
        }

        private void ReplayCurrentOutputsAfterBackendChange()
        {
            if (!ReconcileBackendIdentity())
            {
                return;
            }

            WeatherEnvironmentOutputs current = environmentController.CurrentOutputs;
            if (current.IsValid)
            {
                ApplyEnvironmentOutputs(current);
            }
        }

        private bool ReconcileBackendIdentity()
        {
            string currentBackendId = backend?.BackendId ?? string.Empty;
            if (string.Equals(activeBackendId, currentBackendId, System.StringComparison.Ordinal))
            {
                return false;
            }

            StopLoop(ref rainHandle);
            StopLoop(ref windHandle);
            activeRainSpace = (AudioListenerSpace)(-1);
            activeBackendId = currentBackendId;
            return true;
        }

        public static bool ShouldKeepLoopActive(
            float intensity01,
            bool isCurrentlyActive,
            float startThreshold = RainLoopStartThreshold,
            float stopThreshold = RainLoopStopThreshold)
        {
            float intensity = FiniteClamp01(intensity01);
            float start = FiniteClamp01(startThreshold);
            float stop = Mathf.Min(start, FiniteClamp01(stopThreshold));
            return isCurrentlyActive ? intensity > stop : intensity >= start;
        }

        private static float FiniteClamp01(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);

        private bool IsPlaybackPaused() =>
            AudioPausePolicy.ShouldPausePlayback(
                environmentController != null &&
                environmentController.GameTime != null &&
                environmentController.GameTime.Snapshot.IsPaused,
                Time.timeScale);

        private static void StopLoop(ref IAudioEventHandle handle)
        {
            if (handle == null)
            {
                handle = AudioEventHandles.Invalid;
                return;
            }

            handle.Stop(0.1f);
            handle.Dispose();
            handle = AudioEventHandles.Invalid;
        }

        public static AudioSwitchId MapPrecipitation(WeatherPrecipitationType type)
        {
            switch (type)
            {
                case WeatherPrecipitationType.Drizzle:
                    return AudioProjectIds.Switches.WeatherPrecipitationDrizzle;
                case WeatherPrecipitationType.Rain:
                    return AudioProjectIds.Switches.WeatherPrecipitationRain;
                default:
                    return AudioProjectIds.Switches.WeatherPrecipitationNone;
            }
        }

        public static AudioListenerSpace MapExposure(WeatherExposureContext context)
        {
            switch (context)
            {
                case WeatherExposureContext.Sheltered:
                    return AudioListenerSpace.Sheltered;
                case WeatherExposureContext.Interior:
                    return AudioListenerSpace.Interior;
                default:
                    return AudioListenerSpace.Exterior;
            }
        }

        public static AudioListenerSpace MapExposure(
            in WeatherExposureState exposure)
        {
            if (exposure.IndoorFactor >= 0.65f)
            {
                return AudioListenerSpace.Interior;
            }

            if (exposure.ShelterFactor >= 0.5f)
            {
                return AudioListenerSpace.Sheltered;
            }

            return AudioListenerSpace.Exterior;
        }

        public static float CalculateExistingShelterParameter(
            in WeatherExposureState exposure) =>
            1f - Mathf.Clamp01(exposure.WeatherAudioExposure);

        public static float CalculateAudiblePrecipitation(
            float precipitationIntensity01,
            in WeatherExposureState exposure) =>
            FiniteClamp01(precipitationIntensity01) *
            Mathf.Clamp01(exposure.WeatherAudioExposure);

        public static AudioEventRequest MapThunderRequest(in ThunderAudioRequest thunder) =>
            new AudioEventRequest(
                AudioProjectIds.Events.WeatherThunder,
                worldPosition: thunder.WorldPosition,
                volume01: thunder.Intensity01,
                delaySeconds: thunder.DelaySeconds);
    }
}
