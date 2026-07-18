using System;
using System.Collections.Generic;
using MSC.Core.Lifecycle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio
{
    /// <summary>
    /// Session-owned backend selector. Gameplay talks only to this project
    /// boundary; an official Wwise adapter may be preferred while Unity Audio
    /// remains an explicit development fallback.
    /// </summary>
    [DefaultExecutionOrder(-25000)]
    [DisallowMultipleComponent]
    public sealed class AudioBackendRouter : MonoBehaviour, IAudioBackend, IGameSessionLifetime
    {
        [SerializeField] private MonoBehaviour preferredBackendComponent;
        [SerializeField] private MonoBehaviour fallbackBackendComponent;
        [SerializeField] private bool registerEmittersFromLoadedScenes = true;
        [SerializeField, Min(0f)] private float parameterUpdateDeadband = 0.0001f;

        private readonly Dictionary<string, IAudioEmitter> emitters =
            new Dictionary<string, IAudioEmitter>(StringComparer.Ordinal);
        private readonly Dictionary<AudioParameterCacheKey, float> parameterValues =
            new Dictionary<AudioParameterCacheKey, float>();
        private readonly Dictionary<AudioSwitchCacheKey, AudioSwitchId> switchValues =
            new Dictionary<AudioSwitchCacheKey, AudioSwitchId>();
        private readonly Dictionary<AudioStateId, AudioStateId> stateValues =
            new Dictionary<AudioStateId, AudioStateId>();
        private readonly List<string> sceneRemovalKeys = new List<string>();

        private IAudioBackend preferredBackend;
        private IAudioBackend fallbackBackend;
        private IAudioBackend activeBackend;
        private AudioSettingsState settings = AudioSettingsState.Default;
        private AudioListenerContext listenerContext;
        private bool hasListenerContext;
        private bool sceneCallbacksRegistered;
        private bool sessionEnded;
        private string lastFailure = string.Empty;

        public string BackendId
        {
            get
            {
                EnsureActiveBackend();
                return activeBackend?.BackendId ?? "audio.router.unavailable";
            }
        }

        public AudioBackendKind Kind
        {
            get
            {
                EnsureActiveBackend();
                return activeBackend?.Kind ?? AudioBackendKind.Silent;
            }
        }

        public bool IsReady
        {
            get
            {
                EnsureActiveBackend();
                return activeBackend != null && activeBackend.IsReady;
            }
        }

        public string FailureReason
        {
            get
            {
                EnsureActiveBackend();
                if (activeBackend == null)
                {
                    return string.IsNullOrWhiteSpace(lastFailure)
                        ? "No audio backend is configured."
                        : lastFailure;
                }

                return activeBackend.IsReady
                    ? string.Empty
                    : activeBackend.FailureReason;
            }
        }

        public bool IsUsingFallback
        {
            get
            {
                EnsureActiveBackend();
                return activeBackend != null && ReferenceEquals(activeBackend, fallbackBackend);
            }
        }

        public int RegisteredEmitterCount
        {
            get
            {
                RemoveInvalidEmitterRegistrations();
                return emitters.Count;
            }
        }

        public void CopyRegisteredEmitters(List<IAudioEmitter> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            RemoveInvalidEmitterRegistrations();
            foreach (IAudioEmitter emitter in emitters.Values)
            {
                destination.Add(emitter);
            }
        }

        private void Awake()
        {
            ResolveSerializedBackends();
            EnsureActiveBackend();
        }

        private void OnEnable()
        {
            if (sessionEnded)
            {
                return;
            }

            EnsureActiveBackend();
            RemoveInvalidEmitterRegistrations();
            RegisterSceneCallbacks();
            if (registerEmittersFromLoadedScenes)
            {
                RegisterEmittersFromAllLoadedScenes();
            }
        }

        private void Update()
        {
            RemoveInvalidEmitterRegistrations();
        }

        private void OnDisable()
        {
            UnregisterSceneCallbacks();
        }

        private void OnDestroy()
        {
            EndGameSession();
        }

        public void Configure(
            MonoBehaviour preferredBackendBehaviour,
            MonoBehaviour fallbackBackendBehaviour,
            bool scanLoadedScenes = true)
        {
            if (ReferenceEquals(preferredBackendBehaviour, this) ||
                ReferenceEquals(fallbackBackendBehaviour, this))
            {
                throw new ArgumentException("The audio router cannot route to itself.");
            }

            preferredBackendComponent = preferredBackendBehaviour;
            fallbackBackendComponent = fallbackBackendBehaviour;
            registerEmittersFromLoadedScenes = scanLoadedScenes;
            sessionEnded = false;
            ResolveSerializedBackends();
            EnsureActiveBackend(forceRebind: true);
            if (isActiveAndEnabled)
            {
                RegisterSceneCallbacks();
                if (registerEmittersFromLoadedScenes)
                {
                    RegisterEmittersFromAllLoadedScenes();
                }
            }
        }

        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            if (sessionEnded)
            {
                failure = "The audio session has ended.";
                return false;
            }

            RemoveInvalidEmitterRegistrations();
            if (!IsEmitterAlive(emitter))
            {
                failure = "Audio emitter is required.";
                return false;
            }

            if (!AudioStableId.TryValidate(emitter.StableId, out failure))
            {
                failure = "Invalid audio emitter stable ID: " + failure;
                return false;
            }

            if (emitter.AudioTransform == null)
            {
                failure = $"Audio emitter '{emitter.StableId}' has no transform.";
                return false;
            }

            if (emitters.TryGetValue(emitter.StableId, out IAudioEmitter existing))
            {
                if (ReferenceEquals(existing, emitter))
                {
                    failure = string.Empty;
                    return true;
                }

                failure = $"Duplicate audio emitter stable ID '{emitter.StableId}'.";
                return false;
            }

            emitters.Add(emitter.StableId, emitter);
            EnsureActiveBackend();
            if (activeBackend != null &&
                !activeBackend.RegisterEmitter(emitter, out string backendFailure))
            {
                emitters.Remove(emitter.StableId);
                lastFailure = backendFailure;
                failure = backendFailure;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool UnregisterEmitter(IAudioEmitter emitter)
        {
            if (!IsEmitterAlive(emitter) || string.IsNullOrWhiteSpace(emitter.StableId))
            {
                return false;
            }

            if (!emitters.TryGetValue(emitter.StableId, out IAudioEmitter existing) ||
                !ReferenceEquals(existing, emitter))
            {
                return false;
            }

            emitters.Remove(emitter.StableId);
            RemoveCachedEmitterValues(emitter.StableId);
            EnsureActiveBackend();
            activeBackend?.UnregisterEmitter(emitter);
            return true;
        }

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            EnsureActiveBackend();
            if (activeBackend == null || !activeBackend.IsReady)
            {
                lastFailure = FailureReason;
                return AudioEventHandles.Invalid;
            }

            if (request.Emitter != null)
            {
                if (emitters.TryGetValue(
                        request.Emitter.StableId,
                        out IAudioEmitter registered))
                {
                    if (!ReferenceEquals(registered, request.Emitter))
                    {
                        lastFailure =
                            $"Audio emitter ID '{request.Emitter.StableId}' belongs to another instance.";
                        return AudioEventHandles.Invalid;
                    }
                }
                else if (!RegisterEmitter(request.Emitter, out string failure))
                {
                    lastFailure = failure;
                    return AudioEventHandles.Invalid;
                }
            }

            IAudioEventHandle handle = activeBackend.PostEvent(in request);
            return handle ?? AudioEventHandles.Invalid;
        }

        public bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter emitter = null)
        {
            if (parameterId.IsEmpty)
            {
                lastFailure = "Audio parameter ID is required.";
                return false;
            }

            EnsureActiveBackend();
            if (activeBackend == null || !activeBackend.IsReady)
            {
                lastFailure = FailureReason;
                return false;
            }

            value = AudioMath.FiniteOrZero(value);
            var key = new AudioParameterCacheKey(parameterId, emitter?.StableId);
            if (parameterValues.TryGetValue(key, out float previous) &&
                Mathf.Abs(previous - value) <= parameterUpdateDeadband)
            {
                return true;
            }

            if (!activeBackend.SetParameter(parameterId, value, emitter))
            {
                lastFailure = activeBackend.FailureReason;
                return false;
            }

            parameterValues[key] = value;
            return true;
        }

        public bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter emitter = null)
        {
            if (switchGroupId.IsEmpty || switchValueId.IsEmpty)
            {
                lastFailure = "Audio switch group and value IDs are required.";
                return false;
            }

            EnsureActiveBackend();
            if (activeBackend == null || !activeBackend.IsReady)
            {
                lastFailure = FailureReason;
                return false;
            }

            var key = new AudioSwitchCacheKey(switchGroupId, emitter?.StableId);
            if (switchValues.TryGetValue(key, out AudioSwitchId previous) &&
                previous == switchValueId)
            {
                return true;
            }

            if (!activeBackend.SetSwitch(switchGroupId, switchValueId, emitter))
            {
                lastFailure = activeBackend.FailureReason;
                return false;
            }

            switchValues[key] = switchValueId;
            return true;
        }

        public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId)
        {
            if (stateGroupId.IsEmpty || stateValueId.IsEmpty)
            {
                lastFailure = "Audio state group and value IDs are required.";
                return false;
            }

            EnsureActiveBackend();
            if (activeBackend == null || !activeBackend.IsReady)
            {
                lastFailure = FailureReason;
                return false;
            }

            if (stateValues.TryGetValue(stateGroupId, out AudioStateId previous) &&
                previous == stateValueId)
            {
                return true;
            }

            if (!activeBackend.SetState(stateGroupId, stateValueId))
            {
                lastFailure = activeBackend.FailureReason;
                return false;
            }

            stateValues[stateGroupId] = stateValueId;
            return true;
        }

        public void SetListenerContext(in AudioListenerContext context)
        {
            listenerContext = context;
            hasListenerContext = context.IsValid;
            EnsureActiveBackend();
            if (activeBackend != null && activeBackend.IsReady)
            {
                activeBackend.SetListenerContext(in context);
            }
        }

        public void ApplySettings(in AudioSettingsState value)
        {
            settings = value;
            EnsureActiveBackend();
            if (activeBackend != null && activeBackend.IsReady)
            {
                activeBackend.ApplySettings(in value);
            }
        }

        public void StopAll(float fadeSeconds = 0f)
        {
            EnsureActiveBackend();
            activeBackend?.StopAll(AudioMath.NonNegative(fadeSeconds));
        }

        public AudioRuntimeSnapshot CaptureSnapshot()
        {
            RemoveInvalidEmitterRegistrations();
            EnsureActiveBackend();
            if (activeBackend == null)
            {
                return new AudioRuntimeSnapshot(
                    "audio.router.unavailable",
                    AudioBackendKind.Silent,
                    false,
                    false,
                    emitters.Count,
                    0,
                    0,
                    Array.Empty<string>(),
                    listenerContext,
                    FailureReason);
            }

            AudioRuntimeSnapshot backendSnapshot = activeBackend.CaptureSnapshot();
            string[] missingBanks = backendSnapshot.MissingBanks;
            if (ReferenceEquals(activeBackend, fallbackBackend) &&
                IsBackendAlive(preferredBackend))
            {
                AudioRuntimeSnapshot preferredSnapshot =
                    preferredBackend.CaptureSnapshot();
                if (preferredSnapshot.MissingBanks.Length > 0)
                {
                    // Diagnostics must retain the structured reason why the
                    // preferred backend could not take ownership. Otherwise a
                    // healthy Unity fallback would hide missing Wwise banks
                    // from validation/export tools.
                    missingBanks = preferredSnapshot.MissingBanks;
                }
            }

            return new AudioRuntimeSnapshot(
                backendSnapshot.BackendId,
                backendSnapshot.Kind,
                backendSnapshot.IsReady,
                ReferenceEquals(activeBackend, fallbackBackend),
                emitters.Count,
                backendSnapshot.ActiveVoiceCount,
                backendSnapshot.LoadedBankCount,
                missingBanks,
                hasListenerContext ? listenerContext : backendSnapshot.Listener,
                string.IsNullOrWhiteSpace(lastFailure)
                    ? backendSnapshot.LastFailure
                    : lastFailure);
        }

        public void EndGameSession()
        {
            if (sessionEnded)
            {
                return;
            }

            sessionEnded = true;
            UnregisterSceneCallbacks();
            if (IsBackendAlive(activeBackend))
            {
                activeBackend.StopAll(0f);
                foreach (IAudioEmitter emitter in emitters.Values)
                {
                    activeBackend.UnregisterEmitter(emitter);
                }
            }

            emitters.Clear();
            parameterValues.Clear();
            switchValues.Clear();
            stateValues.Clear();
            activeBackend = null;
        }

        private void ResolveSerializedBackends()
        {
            preferredBackend = ResolveBackend(preferredBackendComponent, "preferred");
            fallbackBackend = ResolveBackend(fallbackBackendComponent, "fallback");
        }

        private IAudioBackend ResolveBackend(MonoBehaviour component, string role)
        {
            if (component == null)
            {
                return null;
            }

            if (ReferenceEquals(component, this))
            {
                lastFailure = $"The {role} backend points to the audio router itself.";
                return null;
            }

            if (component is IAudioBackend backend)
            {
                return backend;
            }

            lastFailure = $"The {role} backend component does not implement IAudioBackend.";
            return null;
        }

        private void EnsureActiveBackend(bool forceRebind = false)
        {
            if (sessionEnded)
            {
                return;
            }

            IAudioBackend desired = null;
            if (IsBackendAliveAndReady(preferredBackend))
            {
                desired = preferredBackend;
            }
            else if (IsBackendAliveAndReady(fallbackBackend))
            {
                desired = fallbackBackend;
            }
            else if (IsBackendAlive(preferredBackend))
            {
                desired = preferredBackend;
            }
            else if (IsBackendAlive(fallbackBackend))
            {
                desired = fallbackBackend;
            }

            if (!forceRebind && ReferenceEquals(desired, activeBackend))
            {
                return;
            }

            RebindBackend(desired);
        }

        private void RebindBackend(IAudioBackend desired)
        {
            IAudioBackend previous = activeBackend;
            if (IsBackendAlive(previous))
            {
                previous.StopAll(0.05f);
                foreach (IAudioEmitter emitter in emitters.Values)
                {
                    previous.UnregisterEmitter(emitter);
                }
            }

            activeBackend = desired;
            parameterValues.Clear();
            switchValues.Clear();
            stateValues.Clear();
            if (activeBackend == null)
            {
                lastFailure = "No usable audio backend is configured.";
                return;
            }

            foreach (IAudioEmitter emitter in emitters.Values)
            {
                if (!activeBackend.RegisterEmitter(emitter, out string failure))
                {
                    lastFailure = failure;
                }
            }

            activeBackend.ApplySettings(in settings);
            if (hasListenerContext)
            {
                activeBackend.SetListenerContext(in listenerContext);
            }

            if (ReferenceEquals(activeBackend, fallbackBackend) &&
                IsBackendAlive(preferredBackend) && !preferredBackend.IsReady)
            {
                lastFailure = string.IsNullOrWhiteSpace(preferredBackend.FailureReason)
                    ? "Preferred audio backend is unavailable; Unity fallback is active."
                    : preferredBackend.FailureReason;
            }
            else
            {
                lastFailure = string.Empty;
            }
        }

        private static bool IsBackendAlive(IAudioBackend backend)
        {
            if (backend == null)
            {
                return false;
            }

            return !(backend is UnityEngine.Object unityObject) || unityObject != null;
        }

        private static bool IsBackendAliveAndReady(IAudioBackend backend) =>
            IsBackendAlive(backend) && backend.IsReady;

        private void RegisterSceneCallbacks()
        {
            if (sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            sceneCallbacksRegistered = true;
        }

        private void UnregisterSceneCallbacks()
        {
            if (!sceneCallbacksRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            sceneCallbacksRegistered = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (registerEmittersFromLoadedScenes)
            {
                RegisterEmittersFromScene(scene);
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            RemoveInvalidEmitterRegistrations(scene.handle);
        }

        private void RemoveInvalidEmitterRegistrations(int? unloadedSceneHandle = null)
        {
            if (emitters.Count == 0)
            {
                return;
            }

            sceneRemovalKeys.Clear();
            foreach (KeyValuePair<string, IAudioEmitter> pair in emitters)
            {
                if (!IsEmitterAlive(pair.Value) ||
                    unloadedSceneHandle.HasValue &&
                    pair.Value.OwningSceneHandle == unloadedSceneHandle.Value)
                {
                    sceneRemovalKeys.Add(pair.Key);
                }
            }

            for (int index = 0; index < sceneRemovalKeys.Count; index++)
            {
                string key = sceneRemovalKeys[index];
                if (!emitters.TryGetValue(key, out IAudioEmitter emitter))
                {
                    continue;
                }

                if (IsBackendAlive(activeBackend))
                {
                    activeBackend.UnregisterEmitter(emitter);
                }

                emitters.Remove(key);
                RemoveCachedEmitterValues(key);
            }
        }

        private void RegisterEmittersFromAllLoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                RegisterEmittersFromScene(SceneManager.GetSceneAt(index));
            }
        }

        private void RegisterEmittersFromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                AudioEmitterAuthoring[] authoredEmitters =
                    roots[rootIndex].GetComponentsInChildren<AudioEmitterAuthoring>(true);
                for (int emitterIndex = 0; emitterIndex < authoredEmitters.Length; emitterIndex++)
                {
                    AudioEmitterAuthoring authoredEmitter = authoredEmitters[emitterIndex];
                    if (emitters.TryGetValue(authoredEmitter.StableId, out IAudioEmitter existing) &&
                        !ReferenceEquals(existing, authoredEmitter))
                    {
                        // Additive loading can briefly instantiate a second bootstrap before its
                        // duplicate persistent owner destroys itself. Keep the incumbent emitter;
                        // explicit registration still rejects real duplicate IDs for validation.
                        continue;
                    }

                    if (!RegisterEmitter(authoredEmitter, out string failure))
                    {
                        Debug.LogError(failure, authoredEmitter);
                    }
                }
            }
        }

        private void RemoveCachedEmitterValues(string stableEmitterId)
        {
            if (string.IsNullOrEmpty(stableEmitterId))
            {
                return;
            }

            var parameterKeys = new List<AudioParameterCacheKey>();
            foreach (AudioParameterCacheKey key in parameterValues.Keys)
            {
                if (string.Equals(key.EmitterId, stableEmitterId, StringComparison.Ordinal))
                {
                    parameterKeys.Add(key);
                }
            }

            for (int index = 0; index < parameterKeys.Count; index++)
            {
                parameterValues.Remove(parameterKeys[index]);
            }

            var switchKeys = new List<AudioSwitchCacheKey>();
            foreach (AudioSwitchCacheKey key in switchValues.Keys)
            {
                if (string.Equals(key.EmitterId, stableEmitterId, StringComparison.Ordinal))
                {
                    switchKeys.Add(key);
                }
            }

            for (int index = 0; index < switchKeys.Count; index++)
            {
                switchValues.Remove(switchKeys[index]);
            }
        }

        private readonly struct AudioParameterCacheKey : IEquatable<AudioParameterCacheKey>
        {
            public AudioParameterCacheKey(AudioParameterId parameterId, string emitterId)
            {
                ParameterId = parameterId;
                EmitterId = emitterId ?? string.Empty;
            }

            public AudioParameterId ParameterId { get; }
            public string EmitterId { get; }

            public bool Equals(AudioParameterCacheKey other) =>
                ParameterId == other.ParameterId &&
                string.Equals(EmitterId, other.EmitterId, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is AudioParameterCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ParameterId.GetHashCode() * 397) ^
                           StringComparer.Ordinal.GetHashCode(EmitterId);
                }
            }
        }

        private static bool IsEmitterAlive(IAudioEmitter emitter)
        {
            if (emitter == null)
            {
                return false;
            }

            return !(emitter is UnityEngine.Object unityObject) || unityObject != null;
        }

        private readonly struct AudioSwitchCacheKey : IEquatable<AudioSwitchCacheKey>
        {
            public AudioSwitchCacheKey(AudioSwitchId switchGroupId, string emitterId)
            {
                SwitchGroupId = switchGroupId;
                EmitterId = emitterId ?? string.Empty;
            }

            public AudioSwitchId SwitchGroupId { get; }
            public string EmitterId { get; }

            public bool Equals(AudioSwitchCacheKey other) =>
                SwitchGroupId == other.SwitchGroupId &&
                string.Equals(EmitterId, other.EmitterId, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is AudioSwitchCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SwitchGroupId.GetHashCode() * 397) ^
                           StringComparer.Ordinal.GetHashCode(EmitterId);
                }
            }
        }
    }
}
