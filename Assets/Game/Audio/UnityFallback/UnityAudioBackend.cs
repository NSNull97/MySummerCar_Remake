using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine.Networking;
#endif
using MSC.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.UnityFallback
{
    /// <summary>
    /// Operational Unity Audio fallback for project-owned events, explicit
    /// ignored Phase 1 supplemental libraries and the private M06 vehicle
    /// diagnostic. Stable event IDs remain the runtime contract.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityAudioBackend : MonoBehaviour,
        IVehicleAudioBackend,
        IAudioSupplementalContentBackend,
        IAudioOverrideContentBackend
    {
        private const string RuntimeBackendId = "unity.fallback";
        private const string DisabledFailure = "Unity Audio fallback is disabled.";
        private const string NotInitializedFailure =
            "Unity Audio fallback has not been initialized.";
        private const float ProjectMixHeadroomGain = 1.5848932f; // +4 dB

        [Header("Project-owned fallback events")]
        [SerializeField] private UnityAudioEventLibrary eventLibrary;
        [SerializeField] private UnityAudioEventLibrary[] supplementalEventLibraries =
            Array.Empty<UnityAudioEventLibrary>();
        [SerializeField] private UnityAudioEventLibrary[] overrideEventLibraries =
            Array.Empty<UnityAudioEventLibrary>();
        [SerializeField, Min(1)] private int maximumGenericVoices = 32;

        [Header("Private M06 vehicle diagnostic")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.65f;
        [SerializeField, Min(0.1f)] private float volumeResponsePerSecond = 4f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.9f;
        [SerializeField, Min(0.1f)] private float minimumDistanceMeters = 1.5f;
        [SerializeField, Min(1f)] private float maximumDistanceMeters = 45f;

        private readonly Dictionary<string, IAudioEmitter> emitters =
            new Dictionary<string, IAudioEmitter>(StringComparer.Ordinal);
        private readonly Dictionary<AudioParameterId, float> parametersById =
            new Dictionary<AudioParameterId, float>();
        private readonly Dictionary<string, Dictionary<AudioParameterId, float>>
            parametersByEmitterId =
                new Dictionary<string, Dictionary<AudioParameterId, float>>(
                    StringComparer.Ordinal);
        private readonly Dictionary<AudioSwitchId, AudioSwitchId> switchesByGroup =
            new Dictionary<AudioSwitchId, AudioSwitchId>();
        private readonly Dictionary<AudioStateId, AudioStateId> statesByGroup =
            new Dictionary<AudioStateId, AudioStateId>();
        private readonly List<UnityVoice> voices = new List<UnityVoice>();
        private readonly List<string> emitterRemovalBuffer = new List<string>();

        private AudioSource idleSource;
        private AudioSource middleSource;
        private AudioSource highSource;
        private AudioSource starterMotorLoopSource;
        private AudioSource starterWhineLoopSource;
        private AudioSource starterOneShotSource;
        private AudioClip[] loadedDiagnosticClips;
        private VehicleAudioParameters vehicleParameters = VehicleAudioParameters.Silent;
        private AudioSettingsState settings = AudioSettingsState.Default;
        private AudioListenerContext listenerContext;
        private bool pendingStarterEngaged;
        private bool pendingEngineStarted;
        private bool starterSequenceActive;
        private bool applicationHasFocus = true;
        private bool initialized;
        private bool sceneCallbacksRegistered;
        private string lastOperationalFailure = string.Empty;
        private ulong nextHandleId = 1UL;

        public string BackendId => RuntimeBackendId;
        public AudioBackendKind Kind => AudioBackendKind.Unity;

        /// <summary>
        /// Unity Audio remains a usable backend even when the optional local
        /// donor diagnostic or the project-owned event library is unavailable.
        /// </summary>
        public bool IsReady => initialized && isActiveAndEnabled;

        public string FailureReason => !initialized
            ? NotInitializedFailure
            : isActiveAndEnabled
                ? string.Empty
                : DisabledFailure;

        public bool IsLocalDiagnosticReady { get; private set; }
        public bool IsLocalDiagnosticLoadComplete { get; private set; }
        public string LocalDiagnosticFailureReason { get; private set; } = string.Empty;
        public int ActiveGenericVoiceCount => CountActiveGenericVoices();

        public bool TryLoadSupplementalEventLibrary(
            string resourcesPath,
            out string failure)
        {
            string normalizedPath = resourcesPath?.Trim() ?? string.Empty;
            if (normalizedPath.Length == 0 ||
                normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
                normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                failure =
                    "A Resources-relative supplemental audio library path without an extension is required.";
                return false;
            }

            UnityAudioEventLibrary library =
                Resources.Load<UnityAudioEventLibrary>(normalizedPath);
            if (library == null)
            {
                failure =
                    $"Supplemental Unity audio library was not found at Resources/{normalizedPath}.";
                return false;
            }

            if (!library.Validate(out string[] validationFailures))
            {
                failure =
                    "Supplemental Unity audio library is invalid: " +
                    string.Join(" | ", validationFailures);
                return false;
            }

            if (ReferenceEquals(eventLibrary, library))
            {
                failure = string.Empty;
                return true;
            }

            UnityAudioEventLibrary[] current = supplementalEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < current.Length; index++)
            {
                if (ReferenceEquals(current[index], library))
                {
                    failure = string.Empty;
                    return true;
                }
            }

            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            CollectEventIds(eventLibrary, eventIds);
            for (int index = 0; index < current.Length; index++)
            {
                CollectEventIds(current[index], eventIds);
            }

            foreach (UnityAudioEventDefinition definition in library.Definitions)
            {
                if (definition != null && !eventIds.Add(definition.EventId))
                {
                    failure =
                        $"Supplemental Unity audio event ID '{definition.EventId}' is already registered.";
                    return false;
                }
            }

            var updated = new UnityAudioEventLibrary[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[current.Length] = library;
            supplementalEventLibraries = updated;
            lastOperationalFailure = string.Empty;
            failure = string.Empty;
            return true;
        }

        public bool TryLoadOverrideEventLibrary(
            string resourcesPath,
            out string failure)
        {
            string normalizedPath = resourcesPath?.Trim() ?? string.Empty;
            if (normalizedPath.Length == 0 ||
                normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
                normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                failure =
                    "A Resources-relative override audio library path without an extension is required.";
                return false;
            }

            UnityAudioEventLibrary library =
                Resources.Load<UnityAudioEventLibrary>(normalizedPath);
            if (library == null)
            {
                failure =
                    $"Override Unity audio library was not found at Resources/{normalizedPath}.";
                return false;
            }

            if (!library.Validate(out string[] validationFailures))
            {
                failure =
                    "Override Unity audio library is invalid: " +
                    string.Join(" | ", validationFailures);
                return false;
            }

            UnityAudioEventLibrary[] current = overrideEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < current.Length; index++)
            {
                if (ReferenceEquals(current[index], library))
                {
                    failure = string.Empty;
                    return true;
                }

                CollectEventIds(current[index], eventIds);
            }

            foreach (UnityAudioEventDefinition definition in library.Definitions)
            {
                if (definition != null && !eventIds.Add(definition.EventId))
                {
                    failure =
                        $"Override Unity audio event ID '{definition.EventId}' is already overridden.";
                    return false;
                }
            }

            var updated = new UnityAudioEventLibrary[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[current.Length] = library;
            overrideEventLibraries = updated;
            lastOperationalFailure = string.Empty;
            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(UnityAudioEventLibrary configuredLibrary)
        {
            eventLibrary = configuredLibrary;
        }

        public void ConfigureSupplementalEventLibrariesForAuthoring(
            params UnityAudioEventLibrary[] configuredLibraries)
        {
            supplementalEventLibraries = configuredLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
        }

        public void ConfigureOverrideEventLibrariesForAuthoring(
            params UnityAudioEventLibrary[] configuredLibraries)
        {
            overrideEventLibraries = configuredLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
        }
#endif

        private void Awake()
        {
            maximumGenericVoices = Mathf.Max(1, maximumGenericVoices);
            idleSource = CreateDiagnosticSource(true);
            middleSource = CreateDiagnosticSource(true);
            highSource = CreateDiagnosticSource(true);
            starterMotorLoopSource = CreateDiagnosticSource(true);
            starterWhineLoopSource = CreateDiagnosticSource(true);
            starterOneShotSource = CreateDiagnosticSource(false);

            initialized = true;
            ValidateConfiguredLibraries();
        }

        private void OnEnable()
        {
            RegisterSceneCallbacks();
            RemoveDestroyedEmitterRegistrations();
            if (IsLocalDiagnosticReady)
            {
                StartDiagnosticLoops();
            }
        }

#if UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return LoadLocalDiagnosticClips();
        }
#else
        private void Start()
        {
            FailLocalDiagnostic(
                "Local donor diagnostic clips are intentionally disabled outside the Unity Editor.");
        }
#endif

        private void Update()
        {
            RemoveDestroyedEmitterRegistrations();
            UpdateGenericVoices();
            UpdateDiagnosticVehicleMix();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationHasFocus = hasFocus;
        }

        private void OnDisable()
        {
            UnregisterSceneCallbacks();
            StopAll(0f);
        }

        private void OnDestroy()
        {
            StopAll(0f);
            initialized = false;
            if (loadedDiagnosticClips == null)
            {
                return;
            }

            for (int index = 0; index < loadedDiagnosticClips.Length; index++)
            {
                if (loadedDiagnosticClips[index] != null)
                {
                    Destroy(loadedDiagnosticClips[index]);
                }
            }
        }

        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            RemoveDestroyedEmitterRegistrations();
            if (!IsReady)
            {
                failure = FailureReason;
                return false;
            }

            if (IsNullOrDestroyed(emitter))
            {
                failure = "Audio emitter is required.";
                return false;
            }

            string stableId = emitter.StableId?.Trim() ?? string.Empty;
            if (stableId.Length == 0)
            {
                failure = "Audio emitter stable ID is required.";
                return false;
            }

            if (emitter.AudioTransform == null)
            {
                failure = $"Audio emitter {stableId} has no Transform.";
                return false;
            }

            if (emitters.ContainsKey(stableId))
            {
                failure = $"Audio emitter {stableId} is already registered.";
                return false;
            }

            emitters.Add(stableId, emitter);
            failure = string.Empty;
            return true;
        }

        public bool UnregisterEmitter(IAudioEmitter emitter)
        {
            if (ReferenceEquals(emitter, null))
            {
                return false;
            }

            string registeredId = null;
            foreach (KeyValuePair<string, IAudioEmitter> pair in emitters)
            {
                if (ReferenceEquals(pair.Value, emitter))
                {
                    registeredId = pair.Key;
                    break;
                }
            }

            if (registeredId == null)
            {
                return false;
            }

            StopVoicesForEmitter(emitter);
            parametersByEmitterId.Remove(registeredId);
            return emitters.Remove(registeredId);
        }

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            if (!IsReady)
            {
                RecordOperationalFailure(FailureReason);
                return AudioEventHandles.Invalid;
            }

            if (!HasAnyConfiguredLibrary())
            {
                RecordOperationalFailure(
                    $"Unity fallback event library is not assigned; event {request.EventId} was not played.");
                return AudioEventHandles.Invalid;
            }

            if (!TryResolveDefinition(request.EventId, out UnityAudioEventDefinition definition) ||
                definition == null || definition.Clip == null)
            {
                RecordOperationalFailure(
                    $"Unity fallback event {request.EventId} has no playable mapping.");
                return AudioEventHandles.Invalid;
            }

            if (request.Emitter != null && !IsRegisteredEmitter(request.Emitter))
            {
                RecordOperationalFailure(
                    $"Emitter {request.Emitter.StableId} must be registered before posting {request.EventId}.");
                return AudioEventHandles.Invalid;
            }

            if (!request.AllowMultiple)
            {
                UnityVoice existing = FindActiveVoice(request.EventId, request.Emitter);
                if (existing != null)
                {
                    return CreateHandle(existing);
                }
            }

            UnityVoice voice = AcquireVoice();
            if (voice == null)
            {
                RecordOperationalFailure(
                    $"Unity fallback voice limit ({maximumGenericVoices}) reached; event {request.EventId} was rejected.");
                return AudioEventHandles.Invalid;
            }

            ConfigureAndPlay(voice, definition, request);
            lastOperationalFailure = string.Empty;
            return CreateHandle(voice);
        }

        public bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter emitter = null)
        {
            if (!IsReady || parameterId.IsEmpty ||
                emitter != null && !IsRegisteredEmitter(emitter))
            {
                return false;
            }

            float safeValue = FiniteOrZero(value);
            if (emitter == null)
            {
                parametersById[parameterId] = safeValue;
            }
            else
            {
                string stableId = emitter.StableId;
                if (!parametersByEmitterId.TryGetValue(
                        stableId,
                        out Dictionary<AudioParameterId, float> scoped))
                {
                    scoped = new Dictionary<AudioParameterId, float>();
                    parametersByEmitterId.Add(stableId, scoped);
                }

                scoped[parameterId] = safeValue;
            }

            return true;
        }

        public bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter emitter = null)
        {
            if (!IsReady || switchGroupId.IsEmpty || switchValueId.IsEmpty ||
                emitter != null && !IsRegisteredEmitter(emitter))
            {
                return false;
            }

            switchesByGroup[switchGroupId] = switchValueId;
            return true;
        }

        public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId)
        {
            if (!IsReady || stateGroupId.IsEmpty || stateValueId.IsEmpty)
            {
                return false;
            }

            statesByGroup[stateGroupId] = stateValueId;
            return true;
        }

        public void SetListenerContext(in AudioListenerContext context)
        {
            listenerContext = context;
        }

        public void ApplySettings(in AudioSettingsState value)
        {
            settings = value;
        }

        public void StopAll(float fadeSeconds = 0f)
        {
            float safeFadeSeconds = Mathf.Clamp(FiniteOrZero(fadeSeconds), 0f, 10f);
            for (int index = 0; index < voices.Count; index++)
            {
                UnityVoice voice = voices[index];
                if (voice.Active)
                {
                    RequestVoiceStop(voice, safeFadeSeconds);
                }
            }

            vehicleParameters = VehicleAudioParameters.Silent;
            starterSequenceActive = false;
            pendingStarterEngaged = false;
            pendingEngineStarted = false;
            StopDiagnosticImmediately();
        }

        public AudioRuntimeSnapshot CaptureSnapshot()
        {
            RemoveDestroyedEmitterRegistrations();
            return new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                IsReady,
                isFallback: true,
                emitters.Count,
                CountActiveGenericVoices() + CountPlayingDiagnosticVoices(),
                loadedBankCount: 0,
                Array.Empty<string>(),
                listenerContext,
                string.IsNullOrWhiteSpace(lastOperationalFailure)
                    ? FailureReason
                    : lastOperationalFailure);
        }

        public void SetVehicleParameters(in VehicleAudioParameters value)
        {
            if (!IsReady)
            {
                return;
            }

            vehicleParameters = value;
        }

        public void PostVehicleEvent(VehicleAudioEvent audioEvent)
        {
            if (!IsReady)
            {
                return;
            }

            switch (audioEvent)
            {
                case VehicleAudioEvent.StarterEngaged:
                    starterSequenceActive = true;
                    pendingEngineStarted = false;
                    if (IsLocalDiagnosticReady)
                    {
                        PlayStarterOneShot(3);
                    }
                    else
                    {
                        pendingStarterEngaged = true;
                    }
                    break;
                case VehicleAudioEvent.StarterDisengaged:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    break;
                case VehicleAudioEvent.EngineStarted:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    if (IsLocalDiagnosticReady)
                    {
                        PlayStarterOneShot(5);
                    }
                    else
                    {
                        pendingEngineStarted = true;
                    }
                    break;
                case VehicleAudioEvent.EngineStopped:
                case VehicleAudioEvent.EngineStalled:
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    pendingEngineStarted = false;
                    break;
                case VehicleAudioEvent.Reset:
                    vehicleParameters = VehicleAudioParameters.Silent;
                    starterSequenceActive = false;
                    pendingStarterEngaged = false;
                    pendingEngineStarted = false;
                    StopDiagnosticImmediately();
                    break;
            }
        }

        private bool HasAnyConfiguredLibrary()
        {
            if (eventLibrary != null)
            {
                return true;
            }

            UnityAudioEventLibrary[] supplemental = supplementalEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < supplemental.Length; index++)
            {
                if (supplemental[index] != null)
                {
                    return true;
                }
            }

            UnityAudioEventLibrary[] overrides = overrideEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < overrides.Length; index++)
            {
                if (overrides[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveDefinition(
            AudioEventId eventId,
            out UnityAudioEventDefinition definition)
        {
            UnityAudioEventLibrary[] overrides = overrideEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = overrides.Length - 1; index >= 0; index--)
            {
                UnityAudioEventLibrary library = overrides[index];
                if (library != null && library.TryResolve(eventId, out definition))
                {
                    return true;
                }
            }

            if (eventLibrary != null && eventLibrary.TryResolve(eventId, out definition))
            {
                return true;
            }

            UnityAudioEventLibrary[] supplemental = supplementalEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < supplemental.Length; index++)
            {
                UnityAudioEventLibrary library = supplemental[index];
                if (library != null && library.TryResolve(eventId, out definition))
                {
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private void ValidateConfiguredLibraries()
        {
            var failures = new List<string>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateLibrary(eventLibrary, "primary", failures, eventIds);

            UnityAudioEventLibrary[] supplemental = supplementalEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < supplemental.Length; index++)
            {
                ValidateLibrary(
                    supplemental[index],
                    $"supplemental[{index}]",
                    failures,
                    eventIds);
            }

            // Overrides deliberately reuse primary stable IDs. They only need
            // to remain internally valid and unique relative to one another.
            var overrideIds = new HashSet<string>(StringComparer.Ordinal);
            UnityAudioEventLibrary[] overrides = overrideEventLibraries ??
                Array.Empty<UnityAudioEventLibrary>();
            for (int index = 0; index < overrides.Length; index++)
            {
                ValidateLibrary(
                    overrides[index],
                    $"override[{index}]",
                    failures,
                    overrideIds);
            }

            if (failures.Count == 0)
            {
                return;
            }

            lastOperationalFailure = string.Join(" ", failures);
            Debug.LogWarning(
                "Unity fallback event libraries contain invalid mappings: " +
                lastOperationalFailure,
                this);
        }

        private static void CollectEventIds(
            UnityAudioEventLibrary library,
            ISet<string> eventIds)
        {
            if (library == null)
            {
                return;
            }

            foreach (UnityAudioEventDefinition definition in library.Definitions)
            {
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.EventId))
                {
                    eventIds.Add(definition.EventId);
                }
            }
        }

        private static void ValidateLibrary(
            UnityAudioEventLibrary library,
            string role,
            ICollection<string> failures,
            ISet<string> eventIds)
        {
            if (library == null)
            {
                return;
            }

            if (!library.Validate(out string[] libraryFailures))
            {
                for (int index = 0; index < libraryFailures.Length; index++)
                {
                    failures.Add($"{role}: {libraryFailures[index]}");
                }
            }

            foreach (UnityAudioEventDefinition definition in library.Definitions)
            {
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.EventId) &&
                    !eventIds.Add(definition.EventId))
                {
                    failures.Add(
                        $"{role}: duplicate event ID across libraries: {definition.EventId}.");
                }
            }
        }

        private bool IsRegisteredEmitter(IAudioEmitter emitter)
        {
            if (IsNullOrDestroyed(emitter))
            {
                return false;
            }

            string stableId = emitter.StableId?.Trim() ?? string.Empty;
            return emitters.TryGetValue(stableId, out IAudioEmitter registered) &&
                   ReferenceEquals(registered, emitter);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            emitterRemovalBuffer.Clear();
            foreach (KeyValuePair<string, IAudioEmitter> pair in emitters)
            {
                if (IsNullOrDestroyed(pair.Value) ||
                    pair.Value.OwningSceneHandle == scene.handle)
                {
                    emitterRemovalBuffer.Add(pair.Key);
                }
            }

            for (int index = 0; index < emitterRemovalBuffer.Count; index++)
            {
                string stableId = emitterRemovalBuffer[index];
                if (emitters.TryGetValue(stableId, out IAudioEmitter emitter))
                {
                    StopVoicesForEmitter(emitter);
                }

                emitters.Remove(stableId);
                parametersByEmitterId.Remove(stableId);
            }
        }

        private void RemoveDestroyedEmitterRegistrations()
        {
            if (emitters.Count == 0)
            {
                return;
            }

            emitterRemovalBuffer.Clear();
            foreach (KeyValuePair<string, IAudioEmitter> pair in emitters)
            {
                if (IsNullOrDestroyed(pair.Value))
                {
                    emitterRemovalBuffer.Add(pair.Key);
                }
            }

            for (int index = 0; index < emitterRemovalBuffer.Count; index++)
            {
                string stableId = emitterRemovalBuffer[index];
                if (emitters.TryGetValue(stableId, out IAudioEmitter emitter))
                {
                    StopVoicesForEmitter(emitter);
                }

                emitters.Remove(stableId);
                parametersByEmitterId.Remove(stableId);
            }
        }

        private void RegisterSceneCallbacks()
        {
            if (sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            sceneCallbacksRegistered = true;
        }

        private void UnregisterSceneCallbacks()
        {
            if (!sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            sceneCallbacksRegistered = false;
        }

        private UnityVoice AcquireVoice()
        {
            for (int index = 0; index < voices.Count; index++)
            {
                if (!voices[index].Active)
                {
                    return voices[index];
                }
            }

            if (voices.Count >= maximumGenericVoices)
            {
                return null;
            }

            var voiceObject = new GameObject($"UnityAudioVoice_{voices.Count:00}");
            voiceObject.hideFlags = HideFlags.DontSave;
            voiceObject.transform.SetParent(transform, false);
            AudioSource source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0.15f;
            source.rolloffMode = AudioRolloffMode.Linear;
            UnityDialogueGainFilter dialogueGain =
                voiceObject.AddComponent<UnityDialogueGainFilter>();
            dialogueGain.enabled = false;
            var voice = new UnityVoice(source, dialogueGain);
            voices.Add(voice);
            return voice;
        }

        private void ConfigureAndPlay(
            UnityVoice voice,
            UnityAudioEventDefinition definition,
            in AudioEventRequest request)
        {
            voice.Generation++;
            voice.HandleId = nextHandleId++;
            if (nextHandleId == 0UL)
            {
                nextHandleId = 1UL;
            }

            voice.Active = true;
            voice.EventId = request.EventId;
            voice.Emitter = request.Emitter;
            voice.Definition = definition;
            voice.RequestVolume01 = request.Volume01;
            voice.FadingOut = false;
            bool dialogue =
                definition.Category == UnityAudioCategory.Dialogue ||
                request.EventId.Value.StartsWith(
                    "audio.npc.",
                    StringComparison.Ordinal);
            voice.DialogueGain.Configure(
                UnityDialogueGainFilter.DefaultDialogueGain);
            voice.DialogueGain.enabled = dialogue;

            AudioSource source = voice.Source;
            source.Stop();
            source.clip = definition.Clip;
            source.loop = definition.Loop;
            source.pitch = definition.Pitch;
            source.spatialBlend = definition.SpatialBlend;
            source.minDistance = definition.MinimumDistanceMeters;
            source.maxDistance = definition.MaximumDistanceMeters;
            source.transform.position = request.ResolveWorldPosition();
            source.volume = ResolveVoiceVolume(voice);

            double startDspTime = UnityEngine.AudioSettings.dspTime + request.DelaySeconds;
            voice.ScheduledStartDspTime = startDspTime;
            voice.ScheduledEndDspTime = definition.Loop
                ? double.PositiveInfinity
                : startDspTime + Math.Max(0.01d, definition.Clip.length / definition.Pitch);
            source.PlayScheduled(startDspTime);
        }

        private IAudioEventHandle CreateHandle(UnityVoice voice)
        {
            return new UnityAudioEventHandle(this, voice, voice.Generation, voice.HandleId);
        }

        private UnityVoice FindActiveVoice(AudioEventId eventId, IAudioEmitter emitter)
        {
            for (int index = 0; index < voices.Count; index++)
            {
                UnityVoice voice = voices[index];
                if (voice.Active && voice.EventId == eventId &&
                    ReferenceEquals(voice.Emitter, emitter))
                {
                    return voice;
                }
            }

            return null;
        }

        private void UpdateGenericVoices()
        {
            double currentDspTime = UnityEngine.AudioSettings.dspTime;
            for (int index = 0; index < voices.Count; index++)
            {
                UnityVoice voice = voices[index];
                if (!voice.Active)
                {
                    continue;
                }

                if (voice.Emitter != null)
                {
                    if (IsNullOrDestroyed(voice.Emitter) ||
                        !voice.Emitter.IsAudioEmitterActive ||
                        voice.Emitter.AudioTransform == null)
                    {
                        ReleaseVoice(voice);
                        continue;
                    }

                    voice.Source.transform.position = voice.Emitter.AudioTransform.position;
                }

                UpdateStoryTrafficVoicePitch(voice);

                if (voice.FadingOut)
                {
                    if (currentDspTime >= voice.FadeEndDspTime)
                    {
                        ReleaseVoice(voice);
                        continue;
                    }

                    double duration = Math.Max(0.0001d, voice.FadeEndDspTime - voice.FadeStartDspTime);
                    float remaining01 = Mathf.Clamp01(
                        (float)((voice.FadeEndDspTime - currentDspTime) / duration));
                    voice.Source.volume = ResolveVoiceVolume(voice) * remaining01;
                }
                else
                {
                    voice.Source.volume = ResolveVoiceVolume(voice);
                }

                if (!voice.Definition.Loop && currentDspTime >= voice.ScheduledEndDspTime)
                {
                    ReleaseVoice(voice);
                }
            }
        }

        private float ResolveVoiceVolume(UnityVoice voice)
        {
            float categoryGain;
            switch (voice.Definition.Category)
            {
                case UnityAudioCategory.Vehicle:
                    categoryGain = settings.Vehicle01;
                    break;
                case UnityAudioCategory.Ambience:
                    categoryGain = settings.Ambience01;
                    break;
                case UnityAudioCategory.Music:
                    categoryGain = settings.Music01;
                    break;
                case UnityAudioCategory.UserInterface:
                    categoryGain = settings.Ui01;
                    break;
                case UnityAudioCategory.Dialogue:
                    categoryGain = settings.Effects01;
                    break;
                default:
                    categoryGain = settings.Effects01;
                    break;
            }

            float focusGain = settings.MuteOnFocusLoss &&
                              (!applicationHasFocus ||
                               listenerContext.IsValid && !listenerContext.HasFocus)
                ? 0f
                : 1f;
            float loudnessGain = settings.ReduceLoudSounds ? 0.8f : 1f;
            float weatherExposureGain = ResolveWeatherExposureGain(voice);
            return Mathf.Clamp01(
                voice.RequestVolume01 *
                voice.Definition.Volume *
                ProjectMixHeadroomGain *
                settings.Master01 *
                categoryGain *
                focusGain *
                loudnessGain *
                weatherExposureGain);
        }

        private float ResolveWeatherExposureGain(UnityVoice voice)
        {
            bool isWeatherVoice =
                voice.EventId.Equals(AudioProjectIds.Events.WeatherRainExterior) ||
                voice.EventId.Equals(AudioProjectIds.Events.WeatherRainSheltered) ||
                voice.EventId.Equals(AudioProjectIds.Events.WeatherRainInterior) ||
                voice.EventId.Equals(AudioProjectIds.Events.WeatherWind) ||
                voice.EventId.Equals(AudioProjectIds.Events.WeatherThunder);
            if (!isWeatherVoice)
            {
                return 1f;
            }

            AudioParameterId parameterId =
                AudioProjectIds.Parameters.EnvironmentShelter;

            if (voice.Emitter != null &&
                parametersByEmitterId.TryGetValue(
                    voice.Emitter.StableId,
                    out Dictionary<AudioParameterId, float> scoped) &&
                scoped.TryGetValue(parameterId, out float scopedValue))
            {
                return 1f - Mathf.Clamp01(scopedValue);
            }

            return parametersById.TryGetValue(parameterId, out float globalValue)
                ? 1f - Mathf.Clamp01(globalValue)
                : 1f;
        }

        private void UpdateStoryTrafficVoicePitch(UnityVoice voice)
        {
            if (voice.Emitter == null ||
                !voice.EventId.Value.StartsWith(
                    "audio.event.traffic.",
                    StringComparison.Ordinal) ||
                !voice.EventId.Value.EndsWith(
                    ".engine.loop",
                    StringComparison.Ordinal) ||
                !parametersByEmitterId.TryGetValue(
                    voice.Emitter.StableId,
                    out Dictionary<AudioParameterId, float> scoped) ||
                !scoped.TryGetValue(
                    AudioProjectIds.Parameters.VehicleRpmNormalized,
                    out float normalizedRpm))
            {
                return;
            }

            float targetPitch = Mathf.Lerp(
                0.68f,
                1.72f,
                Mathf.Pow(Mathf.Clamp01(normalizedRpm), 0.72f));
            voice.Source.pitch = Mathf.MoveTowards(
                voice.Source.pitch,
                targetPitch,
                2.8f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
        }

        private void RequestVoiceStop(UnityVoice voice, float fadeSeconds)
        {
            if (!voice.Active)
            {
                return;
            }

            if (fadeSeconds <= 0f)
            {
                ReleaseVoice(voice);
                return;
            }

            double currentDspTime = UnityEngine.AudioSettings.dspTime;
            voice.FadingOut = true;
            voice.FadeStartDspTime = currentDspTime;
            voice.FadeEndDspTime = currentDspTime + fadeSeconds;
        }

        private void ReleaseVoice(UnityVoice voice)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Active = false;
            voice.Emitter = null;
            voice.Definition = null;
            voice.EventId = default;
            voice.RequestVolume01 = 0f;
            voice.FadingOut = false;
            voice.DialogueGain.enabled = false;
        }

        private void StopVoicesForEmitter(IAudioEmitter emitter)
        {
            for (int index = 0; index < voices.Count; index++)
            {
                UnityVoice voice = voices[index];
                if (voice.Active && ReferenceEquals(voice.Emitter, emitter))
                {
                    ReleaseVoice(voice);
                }
            }
        }

        private bool IsVoiceValid(UnityVoice voice, uint generation, ulong handleId)
        {
            return voice != null && voice.Active && voice.Generation == generation &&
                   voice.HandleId == handleId;
        }

        private bool IsVoicePlaying(UnityVoice voice, uint generation, ulong handleId)
        {
            return IsVoiceValid(voice, generation, handleId);
        }

        private void StopVoice(
            UnityVoice voice,
            uint generation,
            ulong handleId,
            float fadeSeconds)
        {
            if (!IsVoiceValid(voice, generation, handleId))
            {
                return;
            }

            RequestVoiceStop(voice, Mathf.Clamp(FiniteOrZero(fadeSeconds), 0f, 10f));
        }

        private int CountActiveGenericVoices()
        {
            int count = 0;
            for (int index = 0; index < voices.Count; index++)
            {
                if (voices[index].Active)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountPlayingDiagnosticVoices()
        {
            if (!IsLocalDiagnosticReady)
            {
                return 0;
            }

            int count = 0;
            count += idleSource != null && idleSource.isPlaying ? 1 : 0;
            count += middleSource != null && middleSource.isPlaying ? 1 : 0;
            count += highSource != null && highSource.isPlaying ? 1 : 0;
            count += starterMotorLoopSource != null && starterMotorLoopSource.isPlaying ? 1 : 0;
            count += starterWhineLoopSource != null && starterWhineLoopSource.isPlaying ? 1 : 0;
            count += starterOneShotSource != null && starterOneShotSource.isPlaying ? 1 : 0;
            return count;
        }

        private void RecordOperationalFailure(string reason)
        {
            lastOperationalFailure = string.IsNullOrWhiteSpace(reason)
                ? "Unity fallback audio operation failed."
                : reason;
            Debug.LogWarning(lastOperationalFailure, this);
        }

        private static float FiniteOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static bool IsNullOrDestroyed(IAudioEmitter emitter)
        {
            if (ReferenceEquals(emitter, null))
            {
                return true;
            }

            return emitter is UnityEngine.Object unityObject && unityObject == null;
        }

        private AudioSource CreateDiagnosticSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0.15f;
            source.minDistance = minimumDistanceMeters;
            source.maxDistance = Mathf.Max(minimumDistanceMeters, maximumDistanceMeters);
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private void UpdateDiagnosticVehicleMix()
        {
            if (!IsLocalDiagnosticReady)
            {
                return;
            }

            float deltaTime = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            bool running = vehicleParameters.EngineState == VehicleAudioEngineState.Running;
            bool cranking = starterSequenceActive &&
                            vehicleParameters.EngineState == VehicleAudioEngineState.Cranking;
            float rpm01 = Mathf.Clamp01(
                vehicleParameters.EngineRpm / vehicleParameters.RedlineRpm);
            float lowWeight = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.18f, 0.46f, rpm01));
            float highWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.48f, 0.82f, rpm01));
            float middleWeight = Mathf.Max(0f, 1f - lowWeight - highWeight);
            float load = Mathf.Max(
                vehicleParameters.EngineLoad01,
                vehicleParameters.Throttle01 * 0.7f);
            float settingsGain = settings.Master01 * settings.Vehicle01;
            if (settings.MuteOnFocusLoss &&
                (!applicationHasFocus || listenerContext.IsValid && !listenerContext.HasFocus))
            {
                settingsGain = 0f;
            }

            float engineGain = running
                ? masterVolume * settingsGain * Mathf.Lerp(0.55f, 1f, load)
                : 0f;

            SetDiagnosticVolume(idleSource, engineGain * lowWeight, deltaTime);
            SetDiagnosticVolume(middleSource, engineGain * middleWeight, deltaTime);
            SetDiagnosticVolume(highSource, engineGain * highWeight, deltaTime);
            SetDiagnosticVolume(
                starterMotorLoopSource,
                cranking ? masterVolume * settingsGain * 0.56f : 0f,
                deltaTime);
            SetDiagnosticVolume(
                starterWhineLoopSource,
                cranking ? masterVolume * settingsGain * 0.38f : 0f,
                deltaTime);

            idleSource.pitch = Mathf.Lerp(
                0.72f,
                1.35f,
                Mathf.InverseLerp(500f, 1900f, vehicleParameters.EngineRpm));
            middleSource.pitch = Mathf.Lerp(
                0.72f,
                1.38f,
                Mathf.InverseLerp(1200f, 4800f, vehicleParameters.EngineRpm));
            highSource.pitch = Mathf.Lerp(
                0.72f,
                1.4f,
                Mathf.InverseLerp(
                    3200f,
                    vehicleParameters.RedlineRpm,
                    vehicleParameters.EngineRpm));
            float starterPitch = Mathf.Lerp(
                0.9f,
                1.08f,
                Mathf.InverseLerp(0f, 700f, vehicleParameters.EngineRpm));
            starterMotorLoopSource.pitch = starterPitch;
            starterWhineLoopSource.pitch = starterPitch;
        }

        private void SetDiagnosticVolume(AudioSource source, float target, float deltaTime)
        {
            target = Mathf.Clamp01(target);
            if (target > 0.0001f && !source.isPlaying)
            {
                source.Play();
            }

            source.volume = Mathf.MoveTowards(
                source.volume,
                target,
                volumeResponsePerSecond * deltaTime);
            if (target <= 0.0001f && source.volume <= 0.0001f && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void PlayStarterOneShot(int clipIndex)
        {
            if (!IsReady || !IsLocalDiagnosticReady || loadedDiagnosticClips == null)
            {
                return;
            }

            starterOneShotSource.PlayOneShot(
                loadedDiagnosticClips[clipIndex],
                masterVolume * settings.Master01 * settings.Vehicle01 * 0.8f);
        }

        private void StopDiagnosticImmediately()
        {
            if (idleSource == null)
            {
                return;
            }

            idleSource.volume = 0f;
            middleSource.volume = 0f;
            highSource.volume = 0f;
            starterMotorLoopSource.volume = 0f;
            starterWhineLoopSource.volume = 0f;
            idleSource.Stop();
            middleSource.Stop();
            highSource.Stop();
            starterMotorLoopSource.Stop();
            starterWhineLoopSource.Stop();
            starterOneShotSource.Stop();
        }

        private void StartDiagnosticLoops()
        {
            if (loadedDiagnosticClips == null || idleSource == null)
            {
                return;
            }

            idleSource.Play();
            middleSource.Play();
            highSource.Play();
            starterMotorLoopSource.Play();
            starterWhineLoopSource.Play();
        }

        private void FailLocalDiagnostic(string reason)
        {
            IsLocalDiagnosticReady = false;
            IsLocalDiagnosticLoadComplete = true;
            LocalDiagnosticFailureReason = reason ?? string.Empty;
            StopDiagnosticImmediately();
            Debug.LogWarning(
                "M06 local diagnostic vehicle audio is unavailable: " + reason +
                " The operational Unity fallback remains active.",
                this);
        }

#if UNITY_EDITOR
        private const string DonorPathConfiguration = "Config/DonorPaths.local.json";
        private const string AudioRootRelative =
            "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/AudioClip";

        private static readonly DiagnosticClipSpec[] ClipSpecs =
        {
            new DiagnosticClipSpec("850_idle5.ogg", "41296478D8828AF8E7840FE66F9FE2C9A0F05D78127B2BFB20ED35C403894CF9"),
            new DiagnosticClipSpec("850_mid3.ogg", "EF0F7E94F7FB08B1D8F1EA16AEE7BDDE5A27F54FB12FB6357A26DB11493F1FA5"),
            new DiagnosticClipSpec("850_mid13.ogg", "464DA9DBCE2219040522A18B481B4E5C1C285303152F56975D167E5F54044480"),
            new DiagnosticClipSpec("motor_start_1.ogg", "5F9EEB889C660E7AE1F08F9474951ECB3938878AE2A5C13038952A6DDD5892AE"),
            new DiagnosticClipSpec("motor_start_2.ogg", "854EA1EECC2E9DBFC37674CA0968F3FFACE2F75AE8B85A73F06C40FD1198F5AB"),
            new DiagnosticClipSpec("motor_start_3.ogg", "9D9ABE53A8C253E8C571FE1E99B8B34509B2944D6428FBFCA8412413FE551CD2"),
            new DiagnosticClipSpec("starter_whine.ogg", "215329D05D5C5C1BE5F0AE1B831AA20E01A02DD0418856C56808DE52CD310CE3")
        };

        private IEnumerator LoadLocalDiagnosticClips()
        {
            IsLocalDiagnosticLoadComplete = false;
            string[] resolvedFiles;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string configurationPath = ResolveContainedPath(
                    projectRoot,
                    DonorPathConfiguration);
                if (!File.Exists(configurationPath))
                {
                    FailLocalDiagnostic("Config/DonorPaths.local.json is missing.");
                    yield break;
                }

                DonorPathsData localPaths = JsonUtility.FromJson<DonorPathsData>(
                    File.ReadAllText(configurationPath));
                if (localPaths == null ||
                    string.IsNullOrWhiteSpace(localPaths.DonorStagingDirectory))
                {
                    FailLocalDiagnostic(
                        "DonorStagingDirectory is missing from the local path configuration.");
                    yield break;
                }

                string stagingRoot = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(
                        localPaths.DonorStagingDirectory.Trim()));
                if (!Directory.Exists(stagingRoot))
                {
                    FailLocalDiagnostic(
                        "The configured donor staging directory does not exist.");
                    yield break;
                }

                string audioRoot = ResolveContainedPath(stagingRoot, AudioRootRelative);
                resolvedFiles = new string[ClipSpecs.Length];
                for (int index = 0; index < ClipSpecs.Length; index++)
                {
                    DiagnosticClipSpec spec = ClipSpecs[index];
                    string clipPath = ResolveContainedPath(audioRoot, spec.FileName);
                    if (!File.Exists(clipPath))
                    {
                        FailLocalDiagnostic(
                            "Required staged clip is missing: " + spec.FileName + ".");
                        yield break;
                    }

                    string actualHash = ComputeSha256(clipPath);
                    if (!string.Equals(
                            actualHash,
                            spec.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        FailLocalDiagnostic(
                            "SHA-256 mismatch for staged clip: " + spec.FileName + ".");
                        yield break;
                    }

                    resolvedFiles[index] = clipPath;
                }
            }
            catch (Exception exception)
            {
                FailLocalDiagnostic(
                    "Local path or hash validation failed: " + exception.Message);
                yield break;
            }

            loadedDiagnosticClips = new AudioClip[ClipSpecs.Length];
            for (int index = 0; index < resolvedFiles.Length; index++)
            {
                string clipUri = new Uri(resolvedFiles[index]).AbsoluteUri;
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                           clipUri,
                           AudioType.OGGVORBIS))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        FailLocalDiagnostic(
                            "OGG loading failed for " + ClipSpecs[index].FileName +
                            ": " + request.error);
                        yield break;
                    }

                    AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(request);
                    if (loadedClip == null)
                    {
                        FailLocalDiagnostic(
                            "OGG decoding returned no clip for " +
                            ClipSpecs[index].FileName + ".");
                        yield break;
                    }

                    loadedDiagnosticClips[index] = loadedClip;
                    loadedClip.name =
                        "LocalDiagnostic_" + ClipSpecs[index].FileName;
                }
            }

            idleSource.clip = loadedDiagnosticClips[0];
            middleSource.clip = loadedDiagnosticClips[1];
            highSource.clip = loadedDiagnosticClips[2];
            starterMotorLoopSource.clip = loadedDiagnosticClips[4];
            starterWhineLoopSource.clip = loadedDiagnosticClips[6];
            LocalDiagnosticFailureReason = string.Empty;
            IsLocalDiagnosticReady = true;
            IsLocalDiagnosticLoadComplete = true;
            if (isActiveAndEnabled)
            {
                StartDiagnosticLoops();
            }
            Debug.Log(
                $"M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips={ClipSpecs.Length} " +
                "source=ExternalDonorStaging hashes=Verified",
                this);
            if (pendingStarterEngaged)
            {
                pendingStarterEngaged = false;
                PlayStarterOneShot(3);
            }
            else if (pendingEngineStarted)
            {
                pendingEngineStarted = false;
                PlayStarterOneShot(5);
            }
        }

        private static string ResolveContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root) ||
                string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException(
                    "A contained path must use a non-empty relative path.");
            }

            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string portableRelative = relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(
                Path.Combine(normalizedRoot, portableRelative));
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A local diagnostic path resolves outside its configured root.");
            }

            return candidate;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        [Serializable]
        private sealed class DonorPathsData
        {
            public string DonorStagingDirectory = string.Empty;
        }

        private readonly struct DiagnosticClipSpec
        {
            public DiagnosticClipSpec(string fileName, string sha256)
            {
                FileName = fileName;
                Sha256 = sha256;
            }

            public string FileName { get; }
            public string Sha256 { get; }
        }
#endif

        private sealed class UnityVoice
        {
            public UnityVoice(
                AudioSource source,
                UnityDialogueGainFilter dialogueGain)
            {
                Source = source;
                DialogueGain = dialogueGain;
            }

            public AudioSource Source { get; }
            public UnityDialogueGainFilter DialogueGain { get; }
            public bool Active { get; set; }
            public uint Generation { get; set; }
            public ulong HandleId { get; set; }
            public AudioEventId EventId { get; set; }
            public IAudioEmitter Emitter { get; set; }
            public UnityAudioEventDefinition Definition { get; set; }
            public float RequestVolume01 { get; set; }
            public double ScheduledStartDspTime { get; set; }
            public double ScheduledEndDspTime { get; set; }
            public bool FadingOut { get; set; }
            public double FadeStartDspTime { get; set; }
            public double FadeEndDspTime { get; set; }
        }

        private sealed class UnityAudioEventHandle : IAudioEventHandle
        {
            private UnityAudioBackend backend;
            private readonly UnityVoice voice;
            private readonly uint generation;

            public UnityAudioEventHandle(
                UnityAudioBackend backend,
                UnityVoice voice,
                uint generation,
                ulong handleId)
            {
                this.backend = backend;
                this.voice = voice;
                this.generation = generation;
                HandleId = handleId;
                EventId = voice.EventId;
            }

            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public bool IsValid => backend != null &&
                                   backend.IsVoiceValid(voice, generation, HandleId);
            public bool IsPlaying => backend != null &&
                                     backend.IsVoicePlaying(voice, generation, HandleId);

            public void Stop(float fadeSeconds = 0f)
            {
                backend?.StopVoice(voice, generation, HandleId, fadeSeconds);
            }

            public void Dispose()
            {
                Stop();
                backend = null;
            }
        }

    }
}
