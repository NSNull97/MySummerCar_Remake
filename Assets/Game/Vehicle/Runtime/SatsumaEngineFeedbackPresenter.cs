using System;
using MSC.Audio;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>Real simulation telemetry to replaceable audio; no simulation writes.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class SatsumaEngineFeedbackPresenter : MonoBehaviour
    {
        public const string FallbackResourcesPath =
            "Phase1SatsumaEngineAudio/Phase1SatsumaEngineAudioEventLibrary";
        public const float DonorStarterLeadInSeconds = 0.316f;

        [SerializeField] private VehicleSimulationHost simulation;
        [SerializeField] private SatsumaIgnitionController ignition;
        [SerializeField] private PartInstance enginePart;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring engineEmitter;
        [SerializeField] private AudioEmitterAuthoring keyEmitter;
        [SerializeField] private SatsumaEngineExhaustBinding exhaustBinding;
        [SerializeField] private AudioEmitterAuthoring intakeEmitter;
        [SerializeField] private AudioEmitterAuthoring radiatorEmitter;
        private SatsumaEngineSymptomAudio symptoms;
        private IAudioBackend backend;
        private bool subscribed;
        private bool seeded;
        private bool playbackPaused;
        private bool combustionRundownActive;
        private VehicleEngineStatus previousStatus;
        private float crankTime;
        private float starterLeadInSeconds = DonorStarterLeadInSeconds;
        private float starterRetry;
        private float throttleRetry;
        private float coastRetry;
        private float exhaustRetry;
        private IAudioEventHandle starterLead = AudioEventHandles.Invalid;
        private IAudioEventHandle starterLoop = AudioEventHandles.Invalid;
        private IAudioEventHandle catchHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle throttleLoop = AudioEventHandles.Invalid;
        private IAudioEventHandle coastLoop = AudioEventHandles.Invalid;
        private IAudioEventHandle exhaustLoop = AudioEventHandles.Invalid;
        private IAudioEventHandle keyIn = AudioEventHandles.Invalid;
        private IAudioEventHandle keyOut = AudioEventHandles.Invalid;

        public VehicleSimulationHost Simulation => simulation;
        public PartInstance EnginePart => enginePart;
        public SatsumaIgnitionController Ignition => ignition;
        public AudioEmitterAuthoring EngineEmitter => engineEmitter;
        public AudioEmitterAuthoring KeyEmitter => keyEmitter;
        public SatsumaEngineExhaustBinding ExhaustBinding => exhaustBinding;
        public AudioEmitterAuthoring IntakeEmitter => intakeEmitter;
        public AudioEmitterAuthoring RadiatorEmitter => radiatorEmitter;
        public bool IsBound => subscribed && backend != null;
        public string LastFailure { get; private set; } = string.Empty;

        // Optional for existing generic fixtures/prefabs; the Satsuma authorer
        // always provides it. Audio backend registration remains one owner's job.
        public void ConfigureExhaust(SatsumaEngineExhaustBinding binding)
        {
            Unbind();
            exhaustBinding = binding;
        }

        public void ConfigureSymptomEmitters(AudioEmitterAuthoring intake, AudioEmitterAuthoring radiator)
        {
            Unbind();
            intakeEmitter = intake;
            radiatorEmitter = radiator;
        }

        public void ConfigureReferences(VehicleSimulationHost host, SatsumaIgnitionController lockController,
            PartInstance block, AudioEmitterAuthoring authoredEngineEmitter, AudioEmitterAuthoring authoredKeyEmitter)
        {
            Unbind();
            simulation = host; ignition = lockController; enginePart = block;
            engineEmitter = authoredEngineEmitter; keyEmitter = authoredKeyEmitter;
        }

        public bool ConfigureBackend(MonoBehaviour audioBackend)
        {
            if (backendComponent == audioBackend && IsBound) return true;
            Unbind(); backendComponent = audioBackend;
            return Bind();
        }

        public bool Configure(VehicleSimulationHost host, SatsumaIgnitionController lockController,
            PartInstance block, MonoBehaviour audioBackend,
            AudioEmitterAuthoring authoredEngineEmitter, AudioEmitterAuthoring authoredKeyEmitter)
        {
            Unbind();
            simulation = host;
            ignition = lockController;
            enginePart = block;
            backendComponent = audioBackend;
            engineEmitter = authoredEngineEmitter;
            keyEmitter = authoredKeyEmitter;
            return Bind();
        }

        private void OnEnable()
        {
            if (simulation != null && backendComponent != null) Bind();
        }

        private bool Bind()
        {
            if (subscribed) return true;
            backend = backendComponent as IAudioBackend;
            starterLeadInSeconds = backend is IAudioEventTimingSource timing &&
                timing.TryGetEventDurationSeconds(SatsumaEngineAudioIds.StarterEngaged, out float duration)
                ? duration : DonorStarterLeadInSeconds;
            if (simulation == null || ignition == null || enginePart == null ||
                backend == null || engineEmitter == null || keyEmitter == null ||
                exhaustBinding != null && !exhaustBinding.IsConfigured)
            {
                LastFailure = "Satsuma engine feedback requires explicit simulation, lock, block, backend and emitters.";
                return false;
            }
            // The presenter is the single registration owner; emitter authoring
            // has no explicit backend to avoid duplicate scene-scan registration.
            if (!backend.RegisterEmitter(engineEmitter, out string failure))
            {
                LastFailure = failure;
                return false;
            }
            if (!backend.RegisterEmitter(keyEmitter, out failure))
            {
                backend.UnregisterEmitter(engineEmitter);
                LastFailure = failure;
                return false;
            }
            if (exhaustBinding != null && !backend.RegisterEmitter(exhaustBinding.Emitter, out failure))
            {
                backend.UnregisterEmitter(engineEmitter);
                backend.UnregisterEmitter(keyEmitter);
                LastFailure = failure;
                return false;
            }
            if (intakeEmitter != null && !backend.RegisterEmitter(intakeEmitter, out failure))
            {
                UnregisterBaseEmitters(); LastFailure = failure; return false;
            }
            if (radiatorEmitter != null && !backend.RegisterEmitter(radiatorEmitter, out failure))
            {
                if (intakeEmitter != null) backend.UnregisterEmitter(intakeEmitter);
                UnregisterBaseEmitters(); LastFailure = failure; return false;
            }
            if (simulation.SatsumaOperatingSourceComponent != null)
                symptoms = new SatsumaEngineSymptomAudio(backend, engineEmitter,
                    exhaustBinding?.Emitter, intakeEmitter, radiatorEmitter);
            simulation.SimulationReset += ResetTransientFeedback;
            simulation.SimulationRestored += ResetTransientFeedback;
            ignition.KeySoundRequested += PlayKeyGesture;
            subscribed = true;
            SeedCurrentState();
            LastFailure = string.Empty;
            return true;
        }

        private void LateUpdate()
        {
            if (!IsBound || simulation.State == null || simulation.Telemetry == null) return;
            Tick(simulation.State.EngineStatus, simulation.Telemetry.EngineRpm,
                simulation.Telemetry.Throttle01, simulation.Telemetry.EngineLoad01, Time.deltaTime);
        }

        public void Tick(VehicleEngineStatus status, float rpm, float throttle, float load, float deltaSeconds)
        {
            if (!isActiveAndEnabled || !IsBound || !float.IsFinite(deltaSeconds) || deltaSeconds < 0f) return;
            if (AudioPausePolicy.ShouldPausePlayback(false, Time.timeScale))
            {
                if (!playbackPaused) StopAllHandles();
                playbackPaused = true;
                previousStatus = status;
                seeded = true;
                return;
            }

            if (playbackPaused)
            {
                // Resume the current continuous sound, not a historical key,
                // starter lead-in or engine-catch transition from before pause.
                playbackPaused = false;
                previousStatus = status;
                seeded = true;
                crankTime = status == VehicleEngineStatus.Cranking ? starterLeadInSeconds : 0f;
            }

            if (!seeded) { previousStatus = status; seeded = true; }
            bool changed = status != previousStatus;
            bool installed = enginePart.IsInstalled;
            if (status == VehicleEngineStatus.Running && installed) combustionRundownActive = true;
            else if (!installed || status == VehicleEngineStatus.Cranking || rpm <= 0f) combustionRundownActive = false;
            if (changed && installed)
            {
                if (status == VehicleEngineStatus.Cranking)
                {
                    crankTime = 0f;
                    Stop(ref catchHandle);
                    starterLead = Post(SatsumaEngineAudioIds.StarterEngaged, engineEmitter);
                }
                else if (status == VehicleEngineStatus.Running && previousStatus == VehicleEngineStatus.Cranking)
                {
                    catchHandle = Post(SatsumaEngineAudioIds.EngineCaught, engineEmitter);
                }
            }
            exhaustBinding?.RefreshConfiguration();
            VehicleEngineStatus audibleStatus = installed ? status : VehicleEngineStatus.Off;
            float audibleRpm = installed ? rpm : 0f;
            SatsumaEngineFeedbackMix mix = exhaustBinding != null
                ? SatsumaEngineFeedbackRules.Evaluate(audibleStatus, audibleRpm, throttle, exhaustBinding.CurrentOutlet, combustionRundownActive)
                : SatsumaEngineFeedbackRules.Evaluate(audibleStatus, audibleRpm, throttle, combustionRundownActive);
            if (mix.Cranking) crankTime += deltaSeconds;
            else { crankTime = 0f; Stop(ref starterLead); }
            SetLoop(ref starterLoop, ref starterRetry, SatsumaEngineAudioIds.StarterLoop,
                mix.Cranking && crankTime >= starterLeadInSeconds, deltaSeconds);
            backend.SetParameter(SatsumaEngineAudioIds.ThrottleGain, mix.ThrottleGain, engineEmitter);
            backend.SetParameter(SatsumaEngineAudioIds.ThrottlePitch, mix.ThrottlePitch, engineEmitter);
            backend.SetParameter(SatsumaEngineAudioIds.CoastGain, mix.CoastGain, engineEmitter);
            backend.SetParameter(SatsumaEngineAudioIds.CoastPitch, mix.CoastPitch, engineEmitter);
            backend.SetParameter(AudioProjectIds.Parameters.VehicleRpm, rpm, engineEmitter);
            backend.SetParameter(AudioProjectIds.Parameters.VehicleThrottle, throttle, engineEmitter);
            backend.SetParameter(AudioProjectIds.Parameters.VehicleEngineLoad, load, engineEmitter);
            SetLoop(ref throttleLoop, ref throttleRetry, SatsumaEngineAudioIds.EngineThrottleLoop, mix.EngineLoopsActive, deltaSeconds);
            SetLoop(ref coastLoop, ref coastRetry, SatsumaEngineAudioIds.EngineCoastLoop, mix.EngineLoopsActive, deltaSeconds);
            if (exhaustBinding != null)
            {
                bool audible = SatsumaExhaustFeedbackRules.IsAudible(status, rpm, installed, combustionRundownActive);
                AudioEmitterAuthoring exhaustEmitter = exhaustBinding.Emitter;
                backend.SetParameter(SatsumaEngineAudioIds.ExhaustGain, audible ?
                    SatsumaExhaustFeedbackRules.OutletVolume(exhaustBinding.CurrentOutlet) : 0f, exhaustEmitter);
                backend.SetParameter(SatsumaEngineAudioIds.ExhaustPitch, SatsumaExhaustFeedbackRules.Pitch(audibleRpm), exhaustEmitter);
                SetLoop(ref exhaustLoop, ref exhaustRetry, SatsumaEngineAudioIds.ExhaustLoop,
                    audible, deltaSeconds, exhaustEmitter);
            }
            if (!mix.Running) Stop(ref catchHandle);
            if (symptoms != null && simulation.Root?.SatsumaOperatingModel != null && simulation.State != null)
                symptoms.Tick(simulation.Root.SatsumaOperatingModel.LastPoint, status, installed,
                    simulation.State.SatsumaOperating?.RadiatorFanRunning == true, rpm, deltaSeconds);
            previousStatus = status;
        }

        private void PlayKeyGesture(bool inserted)
        {
            if (!isActiveAndEnabled || !IsBound ||
                AudioPausePolicy.ShouldPausePlayback(false, Time.timeScale)) return;
            if (inserted)
            {
                Stop(ref keyIn);
                keyIn = Post(SatsumaEngineAudioIds.KeyInserted, keyEmitter);
            }
            else
            {
                Stop(ref keyOut);
                keyOut = Post(SatsumaEngineAudioIds.KeyRemoved, keyEmitter);
            }
        }

        private IAudioEventHandle Post(AudioEventId id, IAudioEmitter source)
        {
            if (backend == null || !backend.IsReady) return AudioEventHandles.Invalid;
            var request = new AudioEventRequest(id, source, allowMultiple: false);
            return backend.PostEvent(in request) ?? AudioEventHandles.Invalid;
        }

        private void SetLoop(ref IAudioEventHandle handle, ref float retry, AudioEventId id, bool play, float deltaSeconds,
            AudioEmitterAuthoring source = null)
        {
            if (!play) { Stop(ref handle); retry = 0f; return; }
            retry = Mathf.Max(0f, retry - deltaSeconds);
            if ((handle == null || !handle.IsValid || !handle.IsPlaying) && retry <= 0f)
            {
                Stop(ref handle);
                handle = Post(id, source != null ? source : engineEmitter);
                // Missing media or a backend loading a bank must not generate
                // an error/event allocation on every render frame.
                retry = 1f;
            }
        }

        private static void Stop(ref IAudioEventHandle handle)
        {
            handle?.Stop();
            handle?.Dispose();
            handle = AudioEventHandles.Invalid;
        }

        private void ResetTransientFeedback()
        {
            StopAllHandles();
            SeedCurrentState();
        }

        private void SeedCurrentState()
        {
            exhaustBinding?.Invalidate();
            seeded = simulation != null && simulation.State != null;
            previousStatus = seeded ? simulation.State.EngineStatus : VehicleEngineStatus.Off;
            combustionRundownActive = seeded && (simulation.State.CombustionRundownActive ||
                previousStatus == VehicleEngineStatus.Running);
            crankTime = previousStatus == VehicleEngineStatus.Cranking ? starterLeadInSeconds : 0f;
        }

        private void StopAllHandles()
        {
            Stop(ref starterLead); Stop(ref starterLoop); Stop(ref catchHandle);
            Stop(ref throttleLoop); Stop(ref coastLoop); Stop(ref keyIn); Stop(ref keyOut);
            Stop(ref exhaustLoop);
            symptoms?.Reset();
            starterRetry = throttleRetry = coastRetry = exhaustRetry = 0f;
        }

        private void OnDisable() => Unbind();

        private void Unbind()
        {
            StopAllHandles();
            if (subscribed)
            {
                if (simulation != null)
                {
                    simulation.SimulationReset -= ResetTransientFeedback;
                    simulation.SimulationRestored -= ResetTransientFeedback;
                }
                if (ignition != null) ignition.KeySoundRequested -= PlayKeyGesture;
                UnregisterBaseEmitters();
                if (intakeEmitter != null) backend?.UnregisterEmitter(intakeEmitter);
                if (radiatorEmitter != null) backend?.UnregisterEmitter(radiatorEmitter);
            }
            symptoms?.Dispose(); symptoms = null;
            subscribed = false;
            seeded = false;
            playbackPaused = false;
            backend = null;
        }

        private void UnregisterBaseEmitters()
        {
            backend?.UnregisterEmitter(engineEmitter);
            backend?.UnregisterEmitter(keyEmitter);
            if (exhaustBinding != null) backend?.UnregisterEmitter(exhaustBinding.Emitter);
        }
    }
}
