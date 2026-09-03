using MSC.Audio;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Production;
using MSC.Weather.System.Local;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System.Audio
{
    /// <summary>
    /// Vendor-neutral, rate-limited weather audio owner. Global weather remains
    /// authoritative; the local context only attenuates the listener result.
    /// </summary>
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    public sealed class WeatherAudioController : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Weather.WwiseUpdate");

        private const float RainStartThreshold = 0.01f;
        private const float RainStopThreshold = 0.005f;
        private const float WindStartThreshold = 0.18f;
        private const float WindStopThreshold = 0.12f;

        [Header("Sources")]
        [SerializeField] private ProductionEnvironmentController environmentController;
        [SerializeField] private WeatherEnvironmentResolver environmentResolver;
        [SerializeField] private VehicleLocalWeatherContext vehicleContext;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring ambienceEmitter;
        [SerializeField] private AudioListenerContextPresenter listenerPresenter;
        [SerializeField] private WeatherDebugController debugController;

        [Header("Compatible hand-off")]
        [Tooltip("Optional existing weather presenter. It is restored when this owner disables.")]
        [SerializeField] private Behaviour legacyWeatherPresenter;
        [SerializeField] private bool suspendLegacyPresenterWhenActive;

        [Header("Update policy")]
        [SerializeField, Range(2f, 20f)] private float updatesPerSecond = 8f;
        [SerializeField, Min(0.01f)] private float smoothingSeconds = 0.28f;
        [SerializeField, Range(0.001f, 0.1f)] private float parameterDeadband = 0.008f;
        [SerializeField, Range(0f, 1f)] private float enterInteriorExposure01 = 0.42f;
        [SerializeField, Range(0f, 1f)] private float exitInteriorExposure01 = 0.62f;
        [SerializeField, Range(0f, 1f)] private float enterShelteredRoofExposure01 = 0.45f;
        [SerializeField, Range(0f, 1f)] private float exitShelteredRoofExposure01 = 0.7f;

        private IAudioBackend backend;
        private IAudioEventHandle rainHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle windHandle = AudioEventHandles.Invalid;
        private LocalWeatherContext localContext = LocalWeatherContext.Outdoor;
        private WeatherPrecipitationType precipitationType;
        private AudioListenerSpace listenerSpace = AudioListenerSpace.Exterior;
        private AudioSwitchId lastPrecipitationSwitch;
        private AudioStateId lastEnvironmentState;
        private AudioStateId lastDayPhaseState;
        private string activeBackendId = string.Empty;
        private float globalRain01;
        private float globalWind01;
        private float globalThunder01;
        private float normalizedDayTime01 = 0.5f;
        private float smoothedRain01;
        private float smoothedWind01;
        private float smoothedThunder01;
        private float smoothedShelter01;
        private float lastSentRain01 = float.NaN;
        private float lastSentWind01 = float.NaN;
        private float lastSentThunder01 = float.NaN;
        private float lastSentShelter01 = float.NaN;
        private float nextUpdateTime;
        private float previousUpdateTime;
        private uint latestLightningSequence;
        private uint latestThunderSequence;
        private bool legacyPresenterWasEnabled;
        private bool legacyPresenterSuspended;
        private bool subscribed;

        public float AudibleRain01 => smoothedRain01;
        public float AudibleWind01 => smoothedWind01;
        public float AudibleThunder01 => smoothedThunder01;
        public float Shelter01 => smoothedShelter01;
        public AudioListenerSpace ListenerSpace => listenerSpace;

        public void ConfigureForAuthoring(
            ProductionEnvironmentController controller,
            WeatherEnvironmentResolver resolver,
            MonoBehaviour audioBackend,
            AudioEmitterAuthoring emitter,
            AudioListenerContextPresenter listener,
            WeatherDebugController debug = null,
            Behaviour compatibleLegacyPresenter = null,
            VehicleLocalWeatherContext authoredVehicleContext = null)
        {
            Unsubscribe();
            RestoreLegacyPresenter();
            environmentController = controller;
            environmentResolver = resolver;
            vehicleContext = authoredVehicleContext;
            backendComponent = audioBackend;
            ambienceEmitter = emitter;
            listenerPresenter = listener;
            debugController = debug;
            legacyWeatherPresenter = compatibleLegacyPresenter;
            ResolveBackend();
            if (isActiveAndEnabled)
            {
                SuspendLegacyPresenter();
                Subscribe();
                CaptureCurrentState();
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

            if (exitInteriorExposure01 < enterInteriorExposure01 ||
                exitShelteredRoofExposure01 < enterShelteredRoofExposure01)
            {
                failure = "Audio state hysteresis exit thresholds must not be below enter thresholds.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void Awake()
        {
            ResolveBackend();
            if (!TryValidate(out string failure))
            {
                Debug.LogError("Weather audio controller disabled: " + failure, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ResolveBackend();
            SuspendLegacyPresenter();
            Subscribe();
            CaptureCurrentState();
            previousUpdateTime = Time.unscaledTime;
            nextUpdateTime = previousUpdateTime;
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopLoop(ref rainHandle);
            StopLoop(ref windHandle);
            RestoreLegacyPresenter();
            activeBackendId = string.Empty;
            InvalidateSentState();
        }

        private void Update()
        {
            if (backend == null || !backend.IsReady ||
                Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            float now = Time.unscaledTime;
            float delta = Mathf.Max(0f, now - previousUpdateTime);
            previousUpdateTime = now;
            nextUpdateTime = now + 1f / Mathf.Max(2f, updatesPerSecond);

            using (UpdateMarker.Auto())
            {
                ReconcileBackendIdentity();
                SmoothAndApply(delta);
            }
        }

        private void Subscribe()
        {
            if (subscribed || environmentController == null)
            {
                return;
            }

            environmentController.EnvironmentOutputsChanged += HandleOutputs;
            environmentController.LightningOccurred += HandleLightning;
            environmentController.ThunderRequested += HandleThunder;
            if (environmentResolver != null)
            {
                environmentResolver.ContextChanged += HandleLocalContext;
            }

            if (vehicleContext != null)
            {
                vehicleContext.ContextChanged += HandleVehicleContext;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (environmentController != null)
            {
                environmentController.EnvironmentOutputsChanged -= HandleOutputs;
                environmentController.LightningOccurred -= HandleLightning;
                environmentController.ThunderRequested -= HandleThunder;
            }

            if (environmentResolver != null)
            {
                environmentResolver.ContextChanged -= HandleLocalContext;
            }

            if (vehicleContext != null)
            {
                vehicleContext.ContextChanged -= HandleVehicleContext;
            }

            subscribed = false;
        }

        private void CaptureCurrentState()
        {
            if (environmentResolver != null)
            {
                localContext = environmentResolver.Current;
            }

            if (environmentController != null)
            {
                WeatherEnvironmentOutputs outputs = environmentController.CurrentOutputs;
                if (outputs.IsValid)
                {
                    HandleOutputs(outputs);
                }
            }
        }

        private void HandleOutputs(WeatherEnvironmentOutputs outputs)
        {
            if (!outputs.IsValid)
            {
                return;
            }

            globalRain01 = FiniteClamp01(outputs.Audio.PrecipitationIntensity01);
            globalWind01 = FiniteClamp01(outputs.Audio.WindIntensity01);
            globalThunder01 = FiniteClamp01(outputs.Audio.ThunderRisk01);
            normalizedDayTime01 = FiniteClamp01(outputs.Clock.NormalizedDayTime01);
            precipitationType = outputs.Audio.PrecipitationType;
        }

        private void HandleLocalContext(LocalWeatherContext context)
        {
            localContext = context;
        }

        private void HandleVehicleContext(LocalWeatherContext context)
        {
            // The value is read directly from the provider on the bounded audio
            // tick. This callback intentionally performs no backend call.
        }

        private void SmoothAndApply(float deltaSeconds)
        {
            LocalWeatherContext effectiveContext = ResolveEffectiveContext();
            float targetRain = globalRain01 *
                               Mathf.Max(
                                   effectiveContext.AudioLeak01,
                                   effectiveContext.ExteriorAudioExposure01);
            float targetWind = globalWind01 * effectiveContext.WindExposure01;
            float targetThunder = globalThunder01 *
                                  effectiveContext.ThunderExposure01;
            float targetShelter = 1f -
                                  effectiveContext.ExteriorAudioExposure01;
            float blend = smoothingSeconds <= 0.01f
                ? 1f
                : 1f - Mathf.Exp(-deltaSeconds / smoothingSeconds);

            smoothedRain01 = Mathf.Lerp(smoothedRain01, targetRain, blend);
            smoothedWind01 = Mathf.Lerp(smoothedWind01, targetWind, blend);
            smoothedThunder01 = Mathf.Lerp(smoothedThunder01, targetThunder, blend);
            smoothedShelter01 = Mathf.Lerp(smoothedShelter01, targetShelter, blend);

            ResolveListenerSpace(effectiveContext);
            SendParameter(
                AudioProjectIds.Parameters.WeatherPrecipitation,
                smoothedRain01,
                ref lastSentRain01,
                ambienceEmitter);
            SendParameter(
                AudioProjectIds.Parameters.WeatherWind,
                smoothedWind01,
                ref lastSentWind01,
                ambienceEmitter);
            SendParameter(
                AudioProjectIds.Parameters.WeatherThunderRisk,
                smoothedThunder01,
                ref lastSentThunder01,
                ambienceEmitter);
            SendParameter(
                AudioProjectIds.Parameters.EnvironmentShelter,
                smoothedShelter01,
                ref lastSentShelter01,
                ambienceEmitter);
            SendPrecipitationSwitch();
            SendStates();
            UpdateLoops();

            listenerPresenter?.SetWeatherExposureContext(
                smoothedRain01,
                smoothedWind01,
                normalizedDayTime01,
                listenerSpace,
                smoothedShelter01,
                1f - effectiveContext.AudioLeak01,
                effectiveContext.IsInside
                    ? Mathf.Lerp(0.1f, 0.45f, effectiveContext.InteriorDepth01)
                    : 0f);
            debugController?.RecordAudio(
                smoothedRain01,
                smoothedWind01,
                smoothedShelter01,
                smoothedThunder01);
        }

        private LocalWeatherContext ResolveEffectiveContext() =>
            listenerPresenter != null && vehicleContext != null &&
            listenerPresenter.CurrentEnvironment.ListenerSpace ==
            AudioListenerSpace.VehicleInterior
                ? vehicleContext.Current
                : localContext;

        private void ResolveListenerSpace(in LocalWeatherContext context)
        {
            if (listenerPresenter != null &&
                listenerPresenter.CurrentEnvironment.ListenerSpace ==
                AudioListenerSpace.VehicleInterior)
            {
                listenerSpace = AudioListenerSpace.VehicleInterior;
                return;
            }

            if (listenerSpace == AudioListenerSpace.Interior)
            {
                if (context.IsInside &&
                    context.OutdoorExposure01 < exitInteriorExposure01)
                {
                    return;
                }
            }
            else if (context.IsInside &&
                     context.OutdoorExposure01 <= enterInteriorExposure01)
            {
                listenerSpace = AudioListenerSpace.Interior;
                return;
            }

            if (listenerSpace == AudioListenerSpace.Sheltered)
            {
                if (context.HasRoofCover &&
                    context.RoofExposure01 < exitShelteredRoofExposure01)
                {
                    return;
                }
            }
            else if (context.HasRoofCover &&
                     context.RoofExposure01 <=
                     enterShelteredRoofExposure01)
            {
                listenerSpace = AudioListenerSpace.Sheltered;
                return;
            }

            listenerSpace = AudioListenerSpace.Exterior;
        }

        private void SendParameter(
            AudioParameterId id,
            float value,
            ref float previous,
            IAudioEmitter emitter)
        {
            float normalized = FiniteClamp01(value);
            if (!float.IsNaN(previous) &&
                Mathf.Abs(normalized - previous) < parameterDeadband)
            {
                return;
            }

            if (backend.SetParameter(id, normalized, emitter))
            {
                previous = normalized;
            }
        }

        private void SendPrecipitationSwitch()
        {
            AudioSwitchId value = MapPrecipitation(precipitationType);
            if (value.Equals(lastPrecipitationSwitch))
            {
                return;
            }

            if (backend.SetSwitch(
                    AudioProjectIds.Switches.WeatherPrecipitationGroup,
                    value,
                    ambienceEmitter))
            {
                lastPrecipitationSwitch = value;
            }
        }

        private void SendStates()
        {
            AudioStateId environmentState = MapEnvironmentState(listenerSpace);
            if (!environmentState.Equals(lastEnvironmentState) &&
                backend.SetState(
                    AudioProjectIds.States.EnvironmentGroup,
                    environmentState))
            {
                lastEnvironmentState = environmentState;
            }

            AudioStateId dayState = MapDayPhase(normalizedDayTime01);
            if (!dayState.Equals(lastDayPhaseState) &&
                backend.SetState(AudioProjectIds.States.DayPhaseGroup, dayState))
            {
                lastDayPhaseState = dayState;
            }
        }

        private void UpdateLoops()
        {
            bool rainPlaying = IsPlaying(rainHandle);
            if (ShouldPlay(
                    smoothedRain01,
                    rainPlaying,
                    RainStartThreshold,
                    RainStopThreshold))
            {
                if (!rainPlaying)
                {
                    var request = new AudioEventRequest(
                        AudioProjectIds.Events.WeatherRainExterior,
                        ambienceEmitter,
                        allowMultiple: false);
                    rainHandle = backend.PostEvent(in request) ??
                                 AudioEventHandles.Invalid;
                }
            }
            else
            {
                StopLoop(ref rainHandle);
            }

            bool windPlaying = IsPlaying(windHandle);
            if (ShouldPlay(
                    smoothedWind01,
                    windPlaying,
                    WindStartThreshold,
                    WindStopThreshold))
            {
                if (!windPlaying)
                {
                    var request = new AudioEventRequest(
                        AudioProjectIds.Events.WeatherWind,
                        ambienceEmitter,
                        allowMultiple: false);
                    windHandle = backend.PostEvent(in request) ??
                                 AudioEventHandles.Invalid;
                }
            }
            else
            {
                StopLoop(ref windHandle);
            }
        }

        private void HandleLightning(LightningStrikeEvent strike)
        {
            if (backend == null || !backend.IsReady ||
                strike.Sequence <= latestLightningSequence)
            {
                return;
            }

            latestLightningSequence = strike.Sequence;
            LocalWeatherContext context = ResolveEffectiveContext();
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningIntensity,
                strike.Intensity01 * context.ThunderExposure01);
        }

        private void HandleThunder(ThunderAudioRequest thunder)
        {
            if (backend == null || !backend.IsReady ||
                thunder.Sequence <= latestThunderSequence)
            {
                return;
            }

            latestThunderSequence = thunder.Sequence;
            AudioRuntimeSnapshot snapshot = backend.CaptureSnapshot();
            float distance = snapshot.Listener.IsValid
                ? Vector3.Distance(snapshot.Listener.WorldPosition, thunder.WorldPosition)
                : 0f;
            LocalWeatherContext context = ResolveEffectiveContext();
            float intensity = FiniteClamp01(
                thunder.Intensity01 * context.ThunderExposure01);
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningIntensity,
                intensity);
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningDistance,
                distance);
            backend.SetParameter(
                AudioProjectIds.Parameters.LightningDelay,
                (float)thunder.DelaySeconds);
            var request = new AudioEventRequest(
                AudioProjectIds.Events.WeatherThunder,
                worldPosition: thunder.WorldPosition,
                volume01: intensity,
                delaySeconds: thunder.DelaySeconds);
            backend.PostEvent(in request);
        }

        private void ReconcileBackendIdentity()
        {
            string current = backend?.BackendId ?? string.Empty;
            if (string.Equals(
                    current,
                    activeBackendId,
                    global::System.StringComparison.Ordinal))
            {
                return;
            }

            activeBackendId = current;
            StopLoop(ref rainHandle);
            StopLoop(ref windHandle);
            InvalidateSentState();
        }

        private void ResolveBackend()
        {
            backend = backendComponent as IAudioBackend;
        }

        private void SuspendLegacyPresenter()
        {
            if (!suspendLegacyPresenterWhenActive ||
                legacyWeatherPresenter == null ||
                legacyWeatherPresenter == this)
            {
                return;
            }

            legacyPresenterWasEnabled = legacyWeatherPresenter.enabled;
            legacyWeatherPresenter.enabled = false;
            legacyPresenterSuspended = true;
        }

        private void RestoreLegacyPresenter()
        {
            if (!legacyPresenterSuspended || legacyWeatherPresenter == null)
            {
                return;
            }

            legacyWeatherPresenter.enabled = legacyPresenterWasEnabled;
            legacyPresenterSuspended = false;
        }

        private void InvalidateSentState()
        {
            lastSentRain01 = float.NaN;
            lastSentWind01 = float.NaN;
            lastSentThunder01 = float.NaN;
            lastSentShelter01 = float.NaN;
            lastPrecipitationSwitch = default;
            lastEnvironmentState = default;
            lastDayPhaseState = default;
        }

        private static bool IsPlaying(IAudioEventHandle handle) =>
            handle != null && handle.IsValid && handle.IsPlaying;

        private static bool ShouldPlay(
            float intensity01,
            bool currentlyPlaying,
            float startThreshold,
            float stopThreshold) =>
            currentlyPlaying
                ? FiniteClamp01(intensity01) > stopThreshold
                : FiniteClamp01(intensity01) >= startThreshold;

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

        private static AudioSwitchId MapPrecipitation(
            WeatherPrecipitationType type)
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

        private static AudioStateId MapEnvironmentState(
            AudioListenerSpace space)
        {
            switch (space)
            {
                case AudioListenerSpace.Interior:
                    return AudioProjectIds.States.EnvironmentInterior;
                case AudioListenerSpace.Sheltered:
                    return AudioProjectIds.States.EnvironmentSheltered;
                case AudioListenerSpace.VehicleInterior:
                    return AudioProjectIds.States.EnvironmentVehicleInterior;
                default:
                    return AudioProjectIds.States.EnvironmentExterior;
            }
        }

        private static AudioStateId MapDayPhase(float normalizedTime01)
        {
            float time = Mathf.Repeat(FiniteClamp01(normalizedTime01), 1f);
            if (time >= 0.18f && time < 0.25f)
            {
                return AudioProjectIds.States.DayPhaseDawn;
            }

            if (time >= 0.25f && time < 0.75f)
            {
                return AudioProjectIds.States.DayPhaseDay;
            }

            if (time >= 0.75f && time < 0.86f)
            {
                return AudioProjectIds.States.DayPhaseEvening;
            }

            return AudioProjectIds.States.DayPhaseNight;
        }

        private static float FiniteClamp01(float value) =>
            float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Clamp01(value);
    }
}
