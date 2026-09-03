using System;
using UnityEngine;

namespace MSC.Audio
{
    /// <summary>
    /// Per-vehicle adapter from the typed simulation contract to project audio
    /// IDs. It keeps vehicle code vendor-neutral and scopes RTPC/switch updates
    /// to one explicit emitter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleAudioEmitterBackend : MonoBehaviour, IVehicleAudioBackend
    {
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring emitter;

        private IAudioBackend backend;
        private IAudioBackend registeredBackend;
        private IAudioEmitter registeredEmitter;
        private IAudioEventHandle intakeHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle exhaustHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle mechanicalHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle tireRollHandle = AudioEventHandles.Invalid;
        private bool engineLayersRequested;
        private bool tireLayerRequested;
        private bool continuousEventPlaybackEnabled = true;
        private string activeBackendId = string.Empty;

        public VehicleAudioParameters LastVehicleParameters { get; private set; } =
            VehicleAudioParameters.Silent;

        public string BackendId => backend?.BackendId ?? "audio.vehicle.unavailable";
        public AudioBackendKind Kind => backend?.Kind ?? AudioBackendKind.Silent;
        public bool IsReady => backend != null && backend.IsReady;
        public string FailureReason => backend?.FailureReason ??
            "Vehicle audio has no configured IAudioBackend.";

        private void Awake()
        {
            // Development compositions may add this adapter after loading a
            // vehicle scene and configure it in the same frame. Treat the
            // completely empty state as awaiting explicit composition; a
            // partially authored state is still reported as an error.
            if (backendComponent == null && emitter == null)
            {
                enabled = false;
                return;
            }

            if (!TryInitialize(out string failure))
            {
                Debug.LogError(failure, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (backend != null && emitter != null &&
                !RegisterVehicleEmitter(out string failure))
            {
                Debug.LogError(failure, this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (backend != null && emitter != null &&
                (engineLayersRequested || tireLayerRequested))
            {
                PostVehicleEvent(VehicleAudioEvent.Reset);
            }

            StopContinuousEvents(0f);
            UnregisterVehicleEmitter();
        }

        public void Configure(MonoBehaviour audioBackend, AudioEmitterAuthoring vehicleEmitter)
        {
            UnregisterVehicleEmitter();
            backendComponent = audioBackend;
            emitter = vehicleEmitter;
            backend = null;
            StopContinuousEvents(0f);
            activeBackendId = string.Empty;
        }

        /// <summary>
        /// Allows a specialized vehicle presenter to retain the typed RTPC,
        /// switch and state mapping while owning dedicated loop events. The
        /// production default remains enabled for ordinary vehicles.
        /// </summary>
        public void ConfigureContinuousEventPlayback(bool enabledPlayback)
        {
            continuousEventPlaybackEnabled = enabledPlayback;
            if (!continuousEventPlaybackEnabled)
            {
                StopContinuousEvents(0.08f);
            }
        }

        public bool TryInitialize(out string failure)
        {
            backend = backendComponent as IAudioBackend;
            if (backend == null)
            {
                failure = "VehicleAudioEmitterBackend requires an explicit IAudioBackend component.";
                return false;
            }

            if (emitter == null)
            {
                failure = "VehicleAudioEmitterBackend requires an AudioEmitterAuthoring component.";
                return false;
            }

            if (!emitter.TryValidate(out failure))
            {
                return false;
            }

            return RegisterVehicleEmitter(out failure);
        }

        public void SetVehicleParameters(in VehicleAudioParameters parameters)
        {
            LastVehicleParameters = parameters;
            if (!IsReady || emitter == null)
            {
                return;
            }

            ReconcileBackendIdentity();
            UpdateContinuousEvents(in parameters);
            SetParameter(AudioProjectIds.Parameters.VehicleRpm, parameters.EngineRpm);
            SetParameter(
                AudioProjectIds.Parameters.VehicleRpmNormalized,
                parameters.EngineRpm / Mathf.Max(1f, parameters.RedlineRpm));
            SetParameter(AudioProjectIds.Parameters.VehicleEngineLoad, parameters.EngineLoad01);
            SetParameter(AudioProjectIds.Parameters.VehicleThrottle, parameters.Throttle01);
            SetParameter(AudioProjectIds.Parameters.VehicleGear, parameters.SelectedGear);
            SetParameter(AudioProjectIds.Parameters.VehicleClutchSlipRpm, parameters.ClutchSlipRpm);
            SetParameter(AudioProjectIds.Parameters.VehicleSpeed, parameters.VehicleSpeedMetersPerSecond);
            SetParameter(
                AudioProjectIds.Parameters.VehicleWheelSpeed,
                parameters.AggregateWheelSpeedRadiansPerSecond);
            SetParameter(AudioProjectIds.Parameters.VehicleWheelSlip, parameters.WheelSlip01);
            SetParameter(AudioProjectIds.Parameters.VehicleBrake, parameters.Brake01);
            SetParameter(
                AudioProjectIds.Parameters.VehicleSuspensionImpact,
                parameters.SuspensionImpact01);
            SetParameter(
                AudioProjectIds.Parameters.VehicleBatteryVoltage,
                parameters.BatteryVoltage);
            SetParameter(
                AudioProjectIds.Parameters.VehicleEngineTemperature,
                parameters.EngineTemperatureCelsius);

            if (parameters.DamageAvailable)
            {
                SetParameter(AudioProjectIds.Parameters.VehicleDamage, parameters.Damage01);
            }

            if (parameters.InteriorContextAvailable)
            {
                SetParameter(
                    AudioProjectIds.Parameters.VehicleInteriorBlend,
                    parameters.InteriorBlend01);
            }

            if (parameters.OpeningsContextAvailable)
            {
                SetParameter(
                    AudioProjectIds.Parameters.VehicleDoorOpenness,
                    parameters.DoorOpenness01);
                SetParameter(
                    AudioProjectIds.Parameters.VehicleWindowOpenness,
                    parameters.WindowOpenness01);
            }

            SetSwitch(
                AudioProjectIds.Switches.SurfaceGroup,
                MapSurfaceSwitch(parameters.Surface));
            SetState(
                AudioProjectIds.States.VehicleEngineGroup,
                MapEngineStateId(parameters.EngineState));
        }

        public void PostVehicleEvent(VehicleAudioEvent audioEvent)
        {
            if (!IsReady || emitter == null)
            {
                return;
            }

            AudioEventId eventId;
            switch (audioEvent)
            {
                case VehicleAudioEvent.StarterEngaged:
                    eventId = AudioProjectIds.Events.VehicleStarterEngaged;
                    break;
                case VehicleAudioEvent.StarterDisengaged:
                    eventId = AudioProjectIds.Events.VehicleStarterDisengaged;
                    break;
                case VehicleAudioEvent.EngineStarted:
                    eventId = AudioProjectIds.Events.VehicleEngineStarted;
                    break;
                case VehicleAudioEvent.EngineStopped:
                    eventId = AudioProjectIds.Events.VehicleEngineStopped;
                    break;
                case VehicleAudioEvent.EngineStalled:
                    eventId = AudioProjectIds.Events.VehicleEngineStalled;
                    break;
                case VehicleAudioEvent.Reset:
                    eventId = AudioProjectIds.Events.VehicleReset;
                    StopContinuousEvents(0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(audioEvent), audioEvent, null);
            }

            var request = new AudioEventRequest(eventId, emitter);
            backend.PostEvent(in request);
        }

        public bool RegisterEmitter(IAudioEmitter audioEmitter, out string failure) =>
            backend != null
                ? backend.RegisterEmitter(audioEmitter, out failure)
                : FailRegistration(out failure);

        public bool UnregisterEmitter(IAudioEmitter audioEmitter) =>
            backend != null && backend.UnregisterEmitter(audioEmitter);

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            if (backend == null)
            {
                return AudioEventHandles.Invalid;
            }

            if (request.Emitter != null || emitter == null)
            {
                return backend.PostEvent(in request);
            }

            var scoped = new AudioEventRequest(
                request.EventId,
                emitter,
                request.WorldPosition,
                request.Volume01,
                request.DelaySeconds,
                request.AllowMultiple);
            return backend.PostEvent(in scoped);
        }

        public bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter audioEmitter = null) =>
            backend != null && backend.SetParameter(
                parameterId,
                value,
                audioEmitter ?? emitter);

        public bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter audioEmitter = null) =>
            backend != null && backend.SetSwitch(
                switchGroupId,
                switchValueId,
                audioEmitter ?? emitter);

        public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId) =>
            backend != null && backend.SetState(stateGroupId, stateValueId);

        public void SetListenerContext(in AudioListenerContext context)
        {
            backend?.SetListenerContext(in context);
        }

        public void ApplySettings(in AudioSettingsState settings)
        {
            backend?.ApplySettings(in settings);
        }

        public void StopAll(float fadeSeconds = 0f)
        {
            backend?.StopAll(fadeSeconds);
        }

        public AudioRuntimeSnapshot CaptureSnapshot() =>
            backend?.CaptureSnapshot() ?? new AudioRuntimeSnapshot(
                "audio.vehicle.unavailable",
                AudioBackendKind.Silent,
                false,
                false,
                0,
                0,
                0,
                Array.Empty<string>(),
                default,
                FailureReason);

        private void ReconcileBackendIdentity()
        {
            string currentBackendId = backend?.BackendId ?? string.Empty;
            if (string.Equals(activeBackendId, currentBackendId, StringComparison.Ordinal))
            {
                return;
            }

            StopContinuousEvents(0f);
            activeBackendId = currentBackendId;
        }

        private void UpdateContinuousEvents(in VehicleAudioParameters parameters)
        {
            if (!continuousEventPlaybackEnabled)
            {
                return;
            }

            bool shouldPlayEngine =
                parameters.EngineState == VehicleAudioEngineState.Cranking ||
                parameters.EngineState == VehicleAudioEngineState.Running;
            if (shouldPlayEngine && !engineLayersRequested)
            {
                engineLayersRequested = true;
                intakeHandle = PostScopedEvent(
                    AudioProjectIds.Events.VehicleEngineIntake,
                    allowMultiple: false);
                exhaustHandle = PostScopedEvent(
                    AudioProjectIds.Events.VehicleEngineExhaust,
                    allowMultiple: false);
                mechanicalHandle = PostScopedEvent(
                    AudioProjectIds.Events.VehicleEngineMechanical,
                    allowMultiple: false);
            }
            else if (!shouldPlayEngine && engineLayersRequested)
            {
                StopEngineLayers(0.08f);
            }

            bool shouldPlayTire =
                Mathf.Abs(parameters.SignedVehicleSpeedMetersPerSecond) >= 0.2f ||
                parameters.AggregateWheelSpeedRadiansPerSecond >= 1f;
            if (shouldPlayTire && !tireLayerRequested)
            {
                tireLayerRequested = true;
                tireRollHandle = PostScopedEvent(
                    AudioProjectIds.Events.VehicleTireRoll,
                    allowMultiple: false);
            }
            else if (!shouldPlayTire && tireLayerRequested)
            {
                StopHandle(ref tireRollHandle, 0.08f);
                tireLayerRequested = false;
            }
        }

        private IAudioEventHandle PostScopedEvent(
            AudioEventId eventId,
            bool allowMultiple)
        {
            var request = new AudioEventRequest(
                eventId,
                emitter,
                allowMultiple: allowMultiple);
            return backend.PostEvent(in request) ?? AudioEventHandles.Invalid;
        }

        private void StopContinuousEvents(float fadeSeconds)
        {
            StopEngineLayers(fadeSeconds);
            StopHandle(ref tireRollHandle, fadeSeconds);
            tireLayerRequested = false;
        }

        private void StopEngineLayers(float fadeSeconds)
        {
            StopHandle(ref intakeHandle, fadeSeconds);
            StopHandle(ref exhaustHandle, fadeSeconds);
            StopHandle(ref mechanicalHandle, fadeSeconds);
            engineLayersRequested = false;
        }

        private static void StopHandle(
            ref IAudioEventHandle handle,
            float fadeSeconds)
        {
            if (handle != null)
            {
                handle.Stop(fadeSeconds);
                handle.Dispose();
            }

            handle = AudioEventHandles.Invalid;
        }

        public static AudioSwitchId MapSurfaceSwitch(VehicleAudioSurface surface)
        {
            switch (surface)
            {
                case VehicleAudioSurface.Paved:
                    return AudioProjectIds.Switches.SurfacePaved;
                case VehicleAudioSurface.Gravel:
                    return AudioProjectIds.Switches.SurfaceGravel;
                case VehicleAudioSurface.Dirt:
                    return AudioProjectIds.Switches.SurfaceDirt;
                case VehicleAudioSurface.Grass:
                    return AudioProjectIds.Switches.SurfaceGrass;
                case VehicleAudioSurface.MudWet:
                    return AudioProjectIds.Switches.SurfaceMudWet;
                default:
                    return AudioProjectIds.Switches.SurfaceUnknown;
            }
        }

        public static AudioStateId MapEngineStateId(VehicleAudioEngineState state)
        {
            switch (state)
            {
                case VehicleAudioEngineState.Cranking:
                    return AudioProjectIds.States.VehicleEngineCranking;
                case VehicleAudioEngineState.Running:
                    return AudioProjectIds.States.VehicleEngineRunning;
                case VehicleAudioEngineState.Stalled:
                    return AudioProjectIds.States.VehicleEngineStalled;
                default:
                    return AudioProjectIds.States.VehicleEngineOff;
            }
        }

        private static bool FailRegistration(out string failure)
        {
            failure = "Vehicle audio backend is not initialized.";
            return false;
        }

        private bool RegisterVehicleEmitter(out string failure)
        {
            if (ReferenceEquals(registeredBackend, backend) &&
                ReferenceEquals(registeredEmitter, emitter))
            {
                failure = string.Empty;
                return true;
            }

            UnregisterVehicleEmitter();
            if (!backend.RegisterEmitter(emitter, out failure))
            {
                return false;
            }

            registeredBackend = backend;
            registeredEmitter = emitter;
            return true;
        }

        private void UnregisterVehicleEmitter()
        {
            if (registeredBackend != null && registeredEmitter != null)
            {
                registeredBackend.UnregisterEmitter(registeredEmitter);
            }

            registeredBackend = null;
            registeredEmitter = null;
        }
    }
}
