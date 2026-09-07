using System;
using MSC.Audio;
using MSC.Characters;
using UnityEngine;

namespace MSC.NPC
{
    /// <summary>
    /// Persistent, project-owned story-traffic audio owner. NpcWorldRuntime
    /// keeps this component alive while the visible car wrapper streams in and
    /// out, so loop handles and the current music item are not restarted by a
    /// cell boundary. Its emitter always follows the physical or logical car
    /// pose and therefore remains a real 3D source for both Unity Audio and
    /// Wwise.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryTrafficVehicleAudioPresenter : MonoBehaviour
    {
        private StoryTrafficVehiclePresentationBinding motion;
        private Transform listener;
        private MonoBehaviour backendComponent;
        private IAudioBackend backend;
        private AudioEmitterAuthoring emitter;
        private VehicleAudioEmitterBackend vehicleAudio;
        private IAudioEventHandle engineLoopHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle skidLoopHandle = AudioEventHandles.Invalid;
        private IAudioEventHandle musicHandle = AudioEventHandles.Invalid;
        private AudioEventId engineLoopEventId;
        private AudioEventId skidLoopEventId;
        private AudioEventId crashEventId;
        private AudioEventId musicEventId;
        private string configuredCharacterDefinitionId = string.Empty;
        private string configuredAudioProfileSuffix = string.Empty;
        private bool configured;
        private bool drivingActive;
        private bool requestedDrivingActive;
        private bool terminallyDisabled;
        private bool initialized;
        private bool musicAttemptedForActiveCycle;
        private bool hasSpeedSample;
        private float previousSpeedMetersPerSecond;
        private float filteredAccelerationMetersPerSecond2;
        private float simulatedEngineRpm = 950f;
        private int simulatedGear = 1;
        private float shiftSecondsRemaining;
        private bool wasDrifting;

        public bool IsInitialized => initialized;
        public bool IsDrivingActive => drivingActive;
        public bool IsTerminallyDisabled => terminallyDisabled;
        public bool HasBoundMotion => motion != null;
        public bool OwnsMusicSession =>
            musicHandle != null && musicHandle.IsValid;
        public float SimulatedEngineRpm => simulatedEngineRpm;
        public int SimulatedGear => simulatedGear;
        public AudioEventId MusicEventId => musicEventId;
        public IAudioEmitter Emitter => emitter;

        /// <summary>
        /// Compatibility entry point used by focused presentation tests. The
        /// production composition uses ConfigurePersistent and keeps this
        /// component outside the streamed wrapper.
        /// </summary>
        public void Configure(
            StoryTrafficVehiclePresentationBinding configuredMotion,
            MonoBehaviour configuredBackendComponent,
            string characterDefinitionId)
        {
            ConfigurePersistent(
                configuredBackendComponent,
                characterDefinitionId,
                null);
            BindMotion(configuredMotion);
            SetDrivingActive(true);
        }

        public void ConfigurePersistent(
            MonoBehaviour configuredBackendComponent,
            string characterDefinitionId,
            Transform configuredListener)
        {
            ConfigurePersistent(
                configuredBackendComponent,
                characterDefinitionId,
                configuredListener,
                audioProfileSuffix: null);
        }

        /// <summary>
        /// Allows a uniquely identified ambient emitter to use an existing
        /// temporary Phase 1 vehicle-audio profile. Identity still owns the 3D
        /// emitter and lifetime; only event selection is shared until the
        /// model-specific Wwise events are authored.
        /// </summary>
        public void ConfigurePersistent(
            MonoBehaviour configuredBackendComponent,
            string characterDefinitionId,
            Transform configuredListener,
            string audioProfileSuffix)
        {
            IAudioBackend configuredBackend =
                configuredBackendComponent as IAudioBackend;
            if (configuredBackend == null)
            {
                throw new ArgumentException(
                    "Story-traffic audio requires an explicit IAudioBackend component.",
                    nameof(configuredBackendComponent));
            }

            string normalizedDefinitionId =
                characterDefinitionId?.Trim() ?? string.Empty;
            string normalizedProfile = string.IsNullOrWhiteSpace(
                    audioProfileSuffix)
                ? ResolveAudioSuffix(normalizedDefinitionId)
                : ResolveAudioSuffix(audioProfileSuffix);
            if (configured &&
                ReferenceEquals(backendComponent, configuredBackendComponent) &&
                string.Equals(
                    configuredCharacterDefinitionId,
                    normalizedDefinitionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    configuredAudioProfileSuffix,
                    normalizedProfile,
                    StringComparison.Ordinal))
            {
                listener = configuredListener;
                enabled = true;
                return;
            }

            StopDrivingSession();
            backendComponent = configuredBackendComponent;
            backend = configuredBackend;
            listener = configuredListener;
            configuredCharacterDefinitionId = normalizedDefinitionId;
            configuredAudioProfileSuffix = normalizedProfile;

            string ownerSuffix = ResolveAudioSuffix(normalizedDefinitionId);
            string suffix = normalizedProfile;
            emitter = GetComponent<AudioEmitterAuthoring>() ??
                gameObject.AddComponent<AudioEmitterAuthoring>();
            // VehicleAudioEmitterBackend owns this emitter registration. Giving
            // AudioEmitterAuthoring the same explicit backend here registers it
            // immediately and makes the vehicle adapter's registration fail as
            // a duplicate, leaving the traffic presenter permanently silent.
            emitter.Configure(
                "audio.emitter.traffic." + ownerSuffix,
                backendComponent: null,
                transform);
            emitter.SetSurface(new AudioSurfaceContext(
                AudioProjectIds.Switches.SurfacePaved,
                wetness01: 0f,
                roughness01: 0.25f));

            vehicleAudio = GetComponent<VehicleAudioEmitterBackend>() ??
                gameObject.AddComponent<VehicleAudioEmitterBackend>();
            vehicleAudio.Configure(backendComponent, emitter);
            vehicleAudio.ConfigureContinuousEventPlayback(false);
            vehicleAudio.enabled = true;
            engineLoopEventId = new AudioEventId(
                "audio.event.traffic." + suffix + ".engine.loop");
            skidLoopEventId = new AudioEventId(
                "audio.event.traffic." + suffix + ".tire.skid");
            crashEventId = new AudioEventId(
                "audio.event.traffic." + suffix + ".crash");
            musicEventId = string.Equals(
                suffix,
                "jani",
                StringComparison.Ordinal)
                    ? new AudioEventId("audio.event.traffic.jani.music")
                    : default;
            configured = true;
            enabled = true;
        }

        public void BindMotion(
            StoryTrafficVehiclePresentationBinding configuredMotion)
        {
            StoryTrafficVehiclePresentationBinding nextMotion =
                configuredMotion ??
                throw new ArgumentNullException(nameof(configuredMotion));

            if (ReferenceEquals(motion, nextMotion))
            {
                SynchronizeBoundMotionPose();
                return;
            }

            UnsubscribeFromMotionIncidents();
            motion = nextMotion;
            motion.CollisionIncident += HandleCollisionIncident;
            SynchronizeBoundMotionPose();
        }

        public void UnbindMotion(
            StoryTrafficVehiclePresentationBinding expectedMotion)
        {
            if (ReferenceEquals(motion, expectedMotion))
            {
                UnsubscribeFromMotionIncidents();
                motion = null;
            }
        }

        public void SetLogicalPose(
            Vector3 worldPosition,
            Quaternion worldRotation,
            bool activeDriving)
        {
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                throw new ArgumentException(
                    "Story-traffic audio pose must be finite.");
            }

            if (motion == null)
            {
                transform.SetPositionAndRotation(worldPosition, worldRotation);
            }

            SetDrivingActive(activeDriving);
        }

        public void SetDrivingActive(bool activeDriving)
        {
            requestedDrivingActive = activeDriving;
            ApplyRequestedDrivingState();
        }

        public void SetTerminallyDisabled(bool disabled)
        {
            if (terminallyDisabled == disabled)
            {
                return;
            }

            terminallyDisabled = disabled;
            ApplyRequestedDrivingState();
        }

        private void ApplyRequestedDrivingState()
        {
            bool effectiveDriving =
                requestedDrivingActive && !terminallyDisabled;
            if (drivingActive == effectiveDriving)
            {
                return;
            }

            drivingActive = effectiveDriving;
            if (!drivingActive)
            {
                StopDrivingSession();
                return;
            }

            musicAttemptedForActiveCycle = false;
            TryInitializeAudio();
            TryStartMusic();
        }

        private void Update()
        {
            // The Unity presentation backend pauses voices without discarding
            // their cursor. Do not advance the radio cycle or restart driving
            // while the owning gameplay clock is suspended by the menu.
            if (AudioPausePolicy.ShouldPausePlayback(false, Time.timeScale)) return;
            if (motion != null)
            {
                SynchronizeBoundMotionPose();
            }

            if (!configured || !drivingActive)
            {
                return;
            }

            if (!initialized)
            {
                TryInitializeAudio();
                if (!initialized)
                {
                    return;
                }
            }

            TryStartMusic();
            float speed = motion != null
                ? motion.CurrentSpeedMetersPerSecond
                : previousSpeedMetersPerSecond;
            float deltaTime = Mathf.Clamp(Time.deltaTime, 0.001f, 0.1f);
            float rawAcceleration = hasSpeedSample && motion != null
                ? (speed - previousSpeedMetersPerSecond) / deltaTime
                : 0f;
            previousSpeedMetersPerSecond = speed;
            hasSpeedSample = true;
            filteredAccelerationMetersPerSecond2 = Mathf.Lerp(
                filteredAccelerationMetersPerSecond2,
                rawAcceleration,
                1f - Mathf.Exp(-8f * deltaTime));

            bool obstacleBraking = motion != null &&
                                   motion.IsObstacleBraking;
            bool crashBraking = motion != null && motion.IsCrashed;
            bool reversing = motion != null && motion.IsReversing;
            float signedSpeed = reversing ? -speed : speed;
            bool braking = obstacleBraking ||
                           crashBraking ||
                           filteredAccelerationMetersPerSecond2 < -0.7f;
            bool physicalTelemetry = motion != null &&
                                     motion.HasPhysicalMotionBackend &&
                                     motion.PhysicalEngineRpm > 0f;
            if (physicalTelemetry)
            {
                simulatedGear = motion.PhysicalSelectedGear;
                shiftSecondsRemaining = 0f;
            }
            else
            {
                ReconcileGear(speed, reversing);
            }
            bool shifting = shiftSecondsRemaining > 0f;
            shiftSecondsRemaining = Mathf.Max(
                0f,
                shiftSecondsRemaining - deltaTime);

            float targetRpm = physicalTelemetry
                ? motion.PhysicalEngineRpm
                : ResolveTargetRpm(speed, simulatedGear);
            float activeRedlineRpm = physicalTelemetry
                ? Mathf.Max(1000f, motion.PhysicalEngineRedlineRpm)
                : 6000f;
            if (shifting)
            {
                targetRpm = Mathf.Max(1700f, simulatedEngineRpm - 1900f);
            }
            else if (!physicalTelemetry && braking)
            {
                targetRpm = Mathf.Max(1050f, targetRpm - 850f);
            }

            float rpmRate = physicalTelemetry
                ? 14000f
                : targetRpm >= simulatedEngineRpm
                    ? 7200f
                    : 5200f;
            simulatedEngineRpm = Mathf.MoveTowards(
                simulatedEngineRpm,
                targetRpm,
                rpmRate * deltaTime);

            float positiveAcceleration = Mathf.Max(
                0f,
                filteredAccelerationMetersPerSecond2);
            float throttle = braking
                ? 0f
                : Mathf.Clamp01(0.2f + positiveAcceleration / 5f * 0.72f);
            if (shifting)
            {
                throttle = 0.06f;
            }

            float engineLoad = physicalTelemetry
                ? motion.PhysicalEngineLoad01
                : braking
                    ? 0.08f
                    : Mathf.Clamp01(
                        0.24f + positiveAcceleration / 5f * 0.7f);
            float drift = motion != null ? motion.Drift01 : 0f;
            float impact = motion != null && motion.IsCrashed
                ? Mathf.Clamp01(
                    motion.LastImpactSpeedMetersPerSecond / 20f)
                : 0f;
            ReconcileSkidAudio(motion != null && motion.IsDrifting);
            var supplemental = new VehicleAudioSupplementalParameters(
                ignitionOn: true,
                starterRequested: false,
                starterActive: false,
                brake01: braking ||
                         motion != null && motion.HandbrakeActive
                    ? 1f
                    : 0f,
                aggregateWheelSpeedRadiansPerSecond: speed / 0.31f,
                signedVehicleSpeedMetersPerSecond: signedSpeed,
                suspensionImpact01: impact);
            var parameters = new VehicleAudioParameters(
                VehicleAudioEngineState.Running,
                simulatedEngineRpm,
                redlineRpm: activeRedlineRpm,
                engineLoad01: engineLoad,
                throttle01: throttle,
                selectedGear: physicalTelemetry
                    ? motion.PhysicalSelectedGear
                    : reversing ? -1 : simulatedGear,
                clutchSlipRpm: shifting ? 950f : 0f,
                vehicleSpeedMetersPerSecond: speed,
                wheelSlip01: drift,
                VehicleAudioSurface.Paved,
                batteryVoltage: 13.8f,
                engineTemperatureCelsius: 88f,
                supplemental);
            vehicleAudio.SetVehicleParameters(in parameters);
        }

        private void SynchronizeBoundMotionPose()
        {
            if (motion == null)
            {
                return;
            }

            // Rigidbody interpolation can deliberately leave the wrapper's
            // Transform one rendered frame behind its authoritative physics
            // pose. Keeping the persistent audio owner on that stale Transform
            // separates engine/music audio from the visible vehicle and can
            // make it look like a second traffic actor in the hierarchy.
            Vector3 worldPosition = motion.PhysicalWorldPosition;
            Quaternion worldRotation = motion.PhysicalWorldRotation;
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
            {
                return;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private void OnDisable()
        {
            requestedDrivingActive = false;
            terminallyDisabled = false;
            StopDrivingSession();
            UnsubscribeFromMotionIncidents();
            motion = null;
            configured = false;
        }

        private void TryInitializeAudio()
        {
            if (initialized || !drivingActive || backend == null ||
                !backend.IsReady || vehicleAudio == null || emitter == null ||
                !vehicleAudio.TryInitialize(out _))
            {
                return;
            }

            initialized = true;
            engineLoopHandle = PostPersistentEvent(
                engineLoopEventId,
                volume01: 0.78f);
            // Starting a loop from an already-running logical car is not an
            // ignition transition. Never post VehicleEngineStarted here.
        }

        private void TryStartMusic()
        {
            if (musicAttemptedForActiveCycle || !initialized ||
                backend == null || emitter == null || musicEventId.IsEmpty)
            {
                return;
            }

            musicAttemptedForActiveCycle = true;
            musicHandle = PostPersistentEvent(
                musicEventId,
                volume01: 0.62f);
        }

        private void ReconcileSkidAudio(bool drifting)
        {
            if (drifting && !wasDrifting)
            {
                skidLoopHandle = PostPersistentEvent(
                    skidLoopEventId,
                    volume01: 0.72f);
            }
            else if (!drifting && wasDrifting)
            {
                StopHandle(ref skidLoopHandle, 0.1f);
            }

            wasDrifting = drifting;
        }

        private void HandleCollisionIncident(StoryTrafficCollisionEvent incident)
        {
            // The locked traffic reference (and the reviewed TCE extension)
            // treats an ordinary impact as feedback, not as terminal story
            // authority. Report every accepted contact above 1 m/s exactly
            // once; StoryTrafficVehiclePresentationBinding already applies a
            // short contact cooldown before publishing this event.
            if (!configured || !drivingActive || backend == null ||
                emitter == null || incident.SpeedMetersPerSecond <= 1f)
            {
                return;
            }

            if (!initialized)
            {
                TryInitializeAudio();
            }

            if (!initialized)
            {
                return;
            }

            float impact01 = Mathf.Clamp01(
                incident.SpeedMetersPerSecond / 20f);
            vehicleAudio?.SetParameter(
                AudioProjectIds.Parameters.VehicleSuspensionImpact,
                impact01);
            var crashRequest = new AudioEventRequest(
                crashEventId,
                emitter,
                volume01: ResolveImpactVolume01(
                    incident.SpeedMetersPerSecond),
                allowMultiple: true);
            backend.PostEvent(in crashRequest);
        }

        private void UnsubscribeFromMotionIncidents()
        {
            if (motion != null)
            {
                motion.CollisionIncident -= HandleCollisionIncident;
            }
        }

        private static float ResolveImpactVolume01(float speedMetersPerSecond)
        {
            // Mirrors the reviewed first-contact TCE response while respecting
            // AudioEventRequest's normalized project contract.
            return Mathf.Clamp(speedMetersPerSecond / 5f, 0.25f, 1f);
        }

        private IAudioEventHandle PostPersistentEvent(
            AudioEventId eventId,
            float volume01)
        {
            if (backend == null || emitter == null || eventId.IsEmpty)
            {
                return AudioEventHandles.Invalid;
            }

            var request = new AudioEventRequest(
                eventId,
                emitter,
                volume01: volume01,
                allowMultiple: false);
            return backend.PostEvent(in request) ??
                   AudioEventHandles.Invalid;
        }

        private void StopDrivingSession()
        {
            StopHandle(ref engineLoopHandle, 0.12f);
            StopHandle(ref skidLoopHandle, 0.08f);
            StopMusic();
            initialized = false;
            drivingActive = false;
            musicAttemptedForActiveCycle = false;
            hasSpeedSample = false;
            filteredAccelerationMetersPerSecond2 = 0f;
            shiftSecondsRemaining = 0f;
            wasDrifting = false;
        }

        private void StopMusic()
        {
            StopHandle(ref musicHandle, 0.12f);
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

        private void ReconcileGear(
            float speedMetersPerSecond,
            bool reversing)
        {
            if (reversing)
            {
                simulatedGear = 1;
                shiftSecondsRemaining = 0f;
                return;
            }

            int nextGear = simulatedGear;
            switch (simulatedGear)
            {
                case 1 when speedMetersPerSecond >= 11f:
                    nextGear = 2;
                    break;
                case 2 when speedMetersPerSecond >= 22f:
                    nextGear = 3;
                    break;
                case 2 when speedMetersPerSecond <= 8f:
                    nextGear = 1;
                    break;
                case 3 when speedMetersPerSecond >= 35f:
                    nextGear = 4;
                    break;
                case 3 when speedMetersPerSecond <= 18f:
                    nextGear = 2;
                    break;
                case 4 when speedMetersPerSecond <= 30f:
                    nextGear = 3;
                    break;
            }

            if (nextGear == simulatedGear)
            {
                return;
            }

            simulatedGear = nextGear;
            shiftSecondsRemaining = 0.24f;
        }

        private static float ResolveTargetRpm(
            float speedMetersPerSecond,
            int gear)
        {
            if (speedMetersPerSecond < 0.25f)
            {
                return 950f;
            }

            float lowerSpeed;
            float upperSpeed;
            switch (gear)
            {
                case 1:
                    lowerSpeed = 0f;
                    upperSpeed = 12f;
                    break;
                case 2:
                    lowerSpeed = 8f;
                    upperSpeed = 23f;
                    break;
                case 3:
                    lowerSpeed = 18f;
                    upperSpeed = 36f;
                    break;
                default:
                    lowerSpeed = 30f;
                    upperSpeed = 52f;
                    break;
            }

            float revProgress = Mathf.InverseLerp(
                lowerSpeed,
                upperSpeed,
                speedMetersPerSecond);
            return Mathf.Lerp(1650f, 5650f, revProgress);
        }

        private static string ResolveAudioSuffix(string definitionId)
        {
            const string prefix = "character.";
            string candidate = definitionId ?? string.Empty;
            if (candidate.StartsWith(prefix, StringComparison.Ordinal))
            {
                candidate = candidate.Substring(prefix.Length);
            }

            candidate = candidate.Trim().ToLowerInvariant();
            if (!AudioIdValidation.TryValidate(candidate, out _))
            {
                throw new ArgumentException(
                    $"Story-traffic audio suffix '{candidate}' is invalid.",
                    nameof(definitionId));
            }

            return candidate;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}
