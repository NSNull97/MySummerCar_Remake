using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.Wwise
{
    /// <summary>
    /// Official Wwise implementation of the project-owned audio boundary. All
    /// gameplay identifiers are resolved through project maps before a string
    /// name is sent to Wwise.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WwiseAudioBackend : MonoBehaviour, IAudioBackend
    {
        private const string RuntimeBackendId = "wwise.official.2025.1.9.4241";
        private const string GlobalProfilerName = "MSC.WwiseAudioBackend.Global";
        private const float MaximumFadeSeconds = 10f;

        [Header("Project-owned maps")]
        [SerializeField] private AudioEventMap eventMap;
        [SerializeField] private AudioParameterMap parameterMap;
        [SerializeField] private WwiseBackendNameMap backendNameMap;

        [Header("Optional per-event volume RTPC")]
        [SerializeField] private string perEventVolumeRtpcName = string.Empty;
        [SerializeField, Min(0f)] private float perEventVolumeRtpcMaximum = 100f;

        [Header("Emitter updates")]
        [SerializeField] private bool updateRegisteredEmitterPositions = true;

        [Header("Production bank readiness")]
        [SerializeField] private bool requireReportedBanks;
        [SerializeField] private string[] requiredBankNames = Array.Empty<string>();

        private readonly Dictionary<string, EmitterRegistration> emitters =
            new Dictionary<string, EmitterRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, Playback> playbacks =
            new Dictionary<ulong, Playback>();
        private readonly Dictionary<string, float> lastParameterValues =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> loadedBanks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> emitterRemovalBuffer = new List<string>();
        private readonly List<ulong> playbackRemovalBuffer = new List<ulong>();

        private IWwiseSoundEngineApi soundEngineApi;
        private AudioListenerContext listenerContext;
        private AudioSettingsState settings = AudioSettingsState.Default;
        private bool settingsWereApplied;
        private bool backendGameObjectRegistered;
        private bool isReady;
        private string failureReason = "Wwise audio backend has not been enabled.";
        private string lastOperationalFailure = string.Empty;
        private ulong nextHandleId = 1UL;

        public string BackendId => RuntimeBackendId;

        public AudioBackendKind Kind => AudioBackendKind.Wwise;

        public bool IsReady => isReady;

        public string FailureReason => failureReason;

        private void Awake()
        {
            if (soundEngineApi == null)
            {
                soundEngineApi = new AkUnitySoundEngineApi();
            }

            perEventVolumeRtpcMaximum = Mathf.Max(0f, perEventVolumeRtpcMaximum);
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            TryActivate();
        }

        private void LateUpdate()
        {
            if (soundEngineApi == null || !soundEngineApi.IsInitialized)
            {
                if (isReady || backendGameObjectRegistered)
                {
                    MarkSoundEngineUnavailable();
                }

                return;
            }

            if (!isReady)
            {
                TryActivate();
            }

            if (isReady && updateRegisteredEmitterPositions)
            {
                UpdateEmitterPositions();
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            StopAll();
            UnregisterAllEmitters();
            UnregisterBackendGameObject();
            lastParameterValues.Clear();
            isReady = false;
            failureReason = "Wwise audio backend is disabled.";
        }

        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            if (!isReady)
            {
                failure = failureReason;
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

            Transform audioTransform = emitter.AudioTransform;
            if (audioTransform == null)
            {
                failure = $"Audio emitter {stableId} has no Transform.";
                return false;
            }

            if (emitters.ContainsKey(stableId))
            {
                failure = $"Audio emitter {stableId} is already registered.";
                return false;
            }

            GameObject emitterObject = audioTransform.gameObject;
            if (!soundEngineApi.RegisterGameObject(
                    emitterObject,
                    stableId,
                    out failure))
            {
                RecordOperationalFailure(failure);
                return false;
            }

            if (!soundEngineApi.SetObjectPosition(
                    emitterObject,
                    audioTransform,
                    out failure))
            {
                soundEngineApi.UnregisterGameObject(emitterObject, out _);
                RecordOperationalFailure(failure);
                return false;
            }

            emitters.Add(
                stableId,
                new EmitterRegistration(stableId, emitter, emitterObject));
            failure = string.Empty;
            lastOperationalFailure = string.Empty;
            return true;
        }

        public bool UnregisterEmitter(IAudioEmitter emitter)
        {
            if (IsNullOrDestroyed(emitter))
            {
                return false;
            }

            string registeredId = FindRegisteredEmitterId(emitter);
            return registeredId != null && UnregisterEmitterById(registeredId);
        }

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            if (!isReady)
            {
                RecordOperationalFailure(failureReason);
                return AudioEventHandles.Invalid;
            }

            if (eventMap == null ||
                !eventMap.TryGet(request.EventId, out AudioEventMapEntry mapping) ||
                mapping == null ||
                string.IsNullOrWhiteSpace(mapping.BackendEventName))
            {
                RecordOperationalFailure(
                    $"Wwise event {request.EventId} has no backend-name mapping.");
                return AudioEventHandles.Invalid;
            }

            if (request.Emitter != null && !IsRegisteredEmitter(request.Emitter))
            {
                RecordOperationalFailure(
                    $"Emitter {request.Emitter.StableId} must be registered before posting {request.EventId}.");
                return AudioEventHandles.Invalid;
            }

            if (!request.AllowMultiple || !mapping.AllowMultiple)
            {
                Playback existing = FindPlayback(request.EventId, request.Emitter);
                if (existing != null)
                {
                    return new WwiseAudioEventHandle(this, existing.HandleId, existing.EventId);
                }
            }

            ulong handleId = AllocateHandleId();
            var playback = new Playback(
                handleId,
                request.EventId,
                request.Emitter,
                mapping,
                request.ResolveWorldPosition(),
                request.Volume01);
            playbacks.Add(handleId, playback);

            if (request.DelaySeconds > 0d)
            {
                playback.DelayCoroutine = StartCoroutine(
                    PostAfterDelay(playback, request.DelaySeconds));
            }
            else if (!TryPostPlayback(playback))
            {
                ReleasePlayback(playback);
                return AudioEventHandles.Invalid;
            }

            lastOperationalFailure = string.Empty;
            return new WwiseAudioEventHandle(this, handleId, request.EventId);
        }

        public bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter emitter = null)
        {
            if (!isReady || parameterId.IsEmpty)
            {
                return false;
            }

            if (emitter != null && !IsRegisteredEmitter(emitter))
            {
                RecordOperationalFailure(
                    $"Emitter {emitter.StableId} must be registered before setting {parameterId}.");
                return false;
            }

            if (parameterMap == null ||
                !parameterMap.TryGet(parameterId, out AudioParameterMapEntry mapping) ||
                mapping == null ||
                string.IsNullOrWhiteSpace(mapping.BackendParameterName))
            {
                RecordOperationalFailure(
                    $"Wwise parameter {parameterId} has no backend-name mapping.");
                return false;
            }

            float mappedValue = mapping.Clamp(value);
            string scopeKey = CreateParameterScopeKey(parameterId, emitter);
            if (lastParameterValues.TryGetValue(scopeKey, out float previous) &&
                Mathf.Abs(previous - mappedValue) <= mapping.UpdateDeadband)
            {
                return true;
            }

            GameObject emitterObject = emitter == null
                ? null
                : GetRegisteredEmitter(emitter).GameObject;
            if (!soundEngineApi.SetRtpc(
                    mapping.BackendParameterName.Trim(),
                    mappedValue,
                    emitterObject,
                    out string failure))
            {
                RecordOperationalFailure(failure);
                return false;
            }

            lastParameterValues[scopeKey] = mappedValue;
            lastOperationalFailure = string.Empty;
            return true;
        }

        public bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter emitter = null)
        {
            if (!isReady || switchGroupId.IsEmpty || switchValueId.IsEmpty)
            {
                return false;
            }

            if (emitter != null && !IsRegisteredEmitter(emitter))
            {
                RecordOperationalFailure(
                    $"Emitter {emitter.StableId} must be registered before setting {switchGroupId}.");
                return false;
            }

            if (backendNameMap == null ||
                !backendNameMap.TryGet(switchGroupId, out string groupName) ||
                !backendNameMap.TryGet(switchValueId, out string valueName))
            {
                RecordOperationalFailure(
                    $"Wwise switch {switchGroupId}/{switchValueId} has no backend-name mapping.");
                return false;
            }

            GameObject target = emitter == null
                ? gameObject
                : GetRegisteredEmitter(emitter).GameObject;
            if (!soundEngineApi.SetSwitch(
                    groupName,
                    valueName,
                    target,
                    out string failure))
            {
                RecordOperationalFailure(failure);
                return false;
            }

            lastOperationalFailure = string.Empty;
            return true;
        }

        public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId)
        {
            if (!isReady || stateGroupId.IsEmpty || stateValueId.IsEmpty)
            {
                return false;
            }

            if (backendNameMap == null ||
                !backendNameMap.TryGet(stateGroupId, out string groupName) ||
                !backendNameMap.TryGet(stateValueId, out string valueName))
            {
                RecordOperationalFailure(
                    $"Wwise state {stateGroupId}/{stateValueId} has no backend-name mapping.");
                return false;
            }

            if (!soundEngineApi.SetState(groupName, valueName, out string failure))
            {
                RecordOperationalFailure(failure);
                return false;
            }

            lastOperationalFailure = string.Empty;
            return true;
        }

        public void SetListenerContext(in AudioListenerContext context)
        {
            listenerContext = context;
            if (isReady && settingsWereApplied)
            {
                ApplyNormalizedSetting(
                    AudioProjectIds.Parameters.Master,
                    ResolveEffectiveMaster(settings));
            }
        }

        public void ApplySettings(in AudioSettingsState value)
        {
            settings = value;
            settingsWereApplied = true;
            if (!isReady)
            {
                return;
            }

            ApplyNormalizedSetting(
                AudioProjectIds.Parameters.Master,
                ResolveEffectiveMaster(value));
            ApplyNormalizedSetting(AudioProjectIds.Parameters.Vehicle, value.Vehicle01);
            ApplyNormalizedSetting(AudioProjectIds.Parameters.Effects, value.Effects01);
            ApplyNormalizedSetting(AudioProjectIds.Parameters.Ambience, value.Ambience01);
            ApplyNormalizedSetting(AudioProjectIds.Parameters.Music, value.Music01);
            ApplyNormalizedSetting(AudioProjectIds.Parameters.Ui, value.Ui01);
        }

        public void StopAll(float fadeSeconds = 0f)
        {
            playbackRemovalBuffer.Clear();
            foreach (KeyValuePair<ulong, Playback> pair in playbacks)
            {
                playbackRemovalBuffer.Add(pair.Key);
            }

            for (int index = 0; index < playbackRemovalBuffer.Count; index++)
            {
                StopPlayback(playbackRemovalBuffer[index], fadeSeconds);
            }
        }

        public AudioRuntimeSnapshot CaptureSnapshot()
        {
            var missingBanks = new List<string>();
            var observedRequiredBanks = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (eventMap != null)
            {
                IReadOnlyList<AudioEventMapEntry> entries = eventMap.Entries;
                for (int index = 0; index < entries.Count; index++)
                {
                    AudioEventMapEntry entry = entries[index];
                    string bankName = NormalizeBankName(entry?.RequiredBankName);
                    if (bankName.Length > 0 &&
                        observedRequiredBanks.Add(bankName) &&
                        !loadedBanks.Contains(bankName))
                    {
                        missingBanks.Add(bankName);
                    }
                }
            }

            missingBanks.Sort(StringComparer.OrdinalIgnoreCase);
            return new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                isReady,
                isFallback: false,
                emitters.Count,
                CountPlayingVoices(),
                loadedBanks.Count,
                missingBanks.ToArray(),
                listenerContext,
                lastOperationalFailure.Length > 0
                    ? lastOperationalFailure
                    : failureReason);
        }

        /// <summary>
        /// Bank ownership stays with the official integration/composition root.
        /// The owner reports completed load/unload operations so diagnostics can
        /// compare them with AudioEventMap requirements without loading banks here.
        /// </summary>
        public void ReportBankLoaded(string bankName)
        {
            string normalized = NormalizeBankName(bankName);
            if (normalized.Length > 0)
            {
                loadedBanks.Add(normalized);
                if (!isReady)
                {
                    TryActivate();
                }
            }
        }

        public void ReportBankUnloaded(string bankName)
        {
            string normalized = NormalizeBankName(bankName);
            if (normalized.Length > 0)
            {
                loadedBanks.Remove(normalized);
                if (requireReportedBanks && IsRequiredBank(normalized))
                {
                    StopAll(0f);
                    isReady = false;
                    failureReason = $"Required Wwise SoundBank '{normalized}' is not loaded.";
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureForTests(
            AudioEventMap configuredEventMap,
            AudioParameterMap configuredParameterMap,
            WwiseBackendNameMap configuredNameMap,
            IWwiseSoundEngineApi configuredSoundEngineApi,
            string configuredEventVolumeRtpcName = "")
        {
            if (isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    "ConfigureForTests must be called while the backend GameObject is inactive.");
            }

            eventMap = configuredEventMap;
            parameterMap = configuredParameterMap;
            backendNameMap = configuredNameMap;
            soundEngineApi = configuredSoundEngineApi;
            perEventVolumeRtpcName = configuredEventVolumeRtpcName?.Trim() ?? string.Empty;
        }

        public void ConfigureBankReadinessForAuthoring(
            bool requireBanks,
            params string[] configuredRequiredBanks)
        {
            requireReportedBanks = requireBanks;
            requiredBankNames = configuredRequiredBanks ?? Array.Empty<string>();
        }

        public void ConfigureForAuthoring(
            AudioEventMap configuredEventMap,
            AudioParameterMap configuredParameterMap,
            WwiseBackendNameMap configuredNameMap,
            bool requireBanks,
            params string[] configuredRequiredBanks)
        {
            eventMap = configuredEventMap;
            parameterMap = configuredParameterMap;
            backendNameMap = configuredNameMap;
            ConfigureBankReadinessForAuthoring(
                requireBanks,
                configuredRequiredBanks);
        }

        public void ActivateForTests()
        {
            TryActivate();
        }
#endif

        private void TryActivate()
        {
            isReady = false;
            if (!isActiveAndEnabled)
            {
                failureReason = "Wwise audio backend is disabled.";
                return;
            }

            if (eventMap == null)
            {
                failureReason = "AudioEventMap is not assigned to WwiseAudioBackend.";
                return;
            }

            if (parameterMap == null)
            {
                failureReason = "AudioParameterMap is not assigned to WwiseAudioBackend.";
                return;
            }

            if (backendNameMap == null)
            {
                failureReason = "WwiseBackendNameMap is not assigned to WwiseAudioBackend.";
                return;
            }

            if (soundEngineApi == null || !soundEngineApi.IsInitialized)
            {
                failureReason = "The official Wwise SoundEngine is not initialized.";
                return;
            }

            if (requireReportedBanks && !TryValidateRequiredBanks(out failureReason))
            {
                return;
            }

            if (!backendGameObjectRegistered &&
                !soundEngineApi.RegisterGameObject(
                    gameObject,
                    GlobalProfilerName,
                    out failureReason))
            {
                return;
            }

            backendGameObjectRegistered = true;
            failureReason = string.Empty;
            isReady = true;
            ReregisterKnownEmitters();
            if (settingsWereApplied)
            {
                ApplySettings(settings);
            }
        }

        private bool TryValidateRequiredBanks(out string failure)
        {
            string[] banks = requiredBankNames ?? Array.Empty<string>();
            for (int index = 0; index < banks.Length; index++)
            {
                string bankName = NormalizeBankName(banks[index]);
                if (bankName.Length == 0)
                {
                    failure = $"Required Wwise SoundBank entry {index} is empty.";
                    return false;
                }

                if (!loadedBanks.Contains(bankName))
                {
                    failure = $"Required Wwise SoundBank '{bankName}' is not loaded.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private bool IsRequiredBank(string normalizedBankName)
        {
            string[] banks = requiredBankNames ?? Array.Empty<string>();
            for (int index = 0; index < banks.Length; index++)
            {
                if (string.Equals(
                        NormalizeBankName(banks[index]),
                        normalizedBankName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkSoundEngineUnavailable()
        {
            ReleaseAllPlaybacksWithoutStop();
            isReady = false;
            backendGameObjectRegistered = false;
            failureReason = "The official Wwise SoundEngine is not initialized.";
            lastParameterValues.Clear();
            foreach (KeyValuePair<string, EmitterRegistration> pair in emitters)
            {
                pair.Value.RegisteredWithEngine = false;
            }
        }

        private void ReregisterKnownEmitters()
        {
            foreach (KeyValuePair<string, EmitterRegistration> pair in emitters)
            {
                EmitterRegistration registration = pair.Value;
                if (registration.GameObject == null ||
                    IsNullOrDestroyed(registration.Emitter) ||
                    registration.Emitter.AudioTransform == null)
                {
                    continue;
                }

                if (soundEngineApi.RegisterGameObject(
                        registration.GameObject,
                        registration.StableId,
                        out string failure) &&
                    soundEngineApi.SetObjectPosition(
                        registration.GameObject,
                        registration.Emitter.AudioTransform,
                        out failure))
                {
                    registration.RegisteredWithEngine = true;
                }
                else
                {
                    registration.RegisteredWithEngine = false;
                    RecordOperationalFailure(failure);
                }
            }
        }

        private IEnumerator PostAfterDelay(Playback playback, double delaySeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + delaySeconds;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!playbacks.ContainsKey(playback.HandleId))
                {
                    yield break;
                }

                yield return null;
            }

            playback.DelayCoroutine = null;
            if (!playbacks.ContainsKey(playback.HandleId))
            {
                yield break;
            }

            if (!TryPostPlayback(playback))
            {
                ReleasePlayback(playback);
            }
        }

        private bool TryPostPlayback(Playback playback)
        {
            GameObject target;
            if (playback.Emitter != null)
            {
                EmitterRegistration registration = GetRegisteredEmitter(playback.Emitter);
                if (registration == null || !registration.RegisteredWithEngine)
                {
                    RecordOperationalFailure(
                        $"Emitter for {playback.EventId} is no longer registered with Wwise.");
                    return false;
                }

                target = registration.GameObject;
                soundEngineApi.SetObjectPosition(
                    target,
                    playback.Emitter.AudioTransform,
                    out _);
            }
            else if (playback.Mapping.Spatialized)
            {
                target = CreateTransientGameObject(playback);
                if (!soundEngineApi.RegisterGameObject(
                        target,
                        $"MSC.Transient.{playback.EventId.Value}",
                        out string registrationFailure))
                {
                    DestroyOwnedGameObject(target);
                    RecordOperationalFailure(registrationFailure);
                    return false;
                }

                playback.Target = target;
                playback.OwnsTarget = true;
                soundEngineApi.SetObjectPosition(target, target.transform, out _);
            }
            else
            {
                target = gameObject;
            }

            uint playingId = soundEngineApi.PostEvent(
                playback.Mapping.BackendEventName.Trim(),
                target,
                endedPlayingId => OnEventEnded(playback.HandleId, endedPlayingId),
                out string failure);
            if (playingId == soundEngineApi.InvalidPlayingId)
            {
                RecordOperationalFailure(failure);
                return false;
            }

            playback.Target = target;
            playback.PlayingId = playingId;
            if (!string.IsNullOrWhiteSpace(perEventVolumeRtpcName))
            {
                float volumeRtpcValue = playback.Volume01 * perEventVolumeRtpcMaximum;
                if (!soundEngineApi.SetRtpcByPlayingId(
                        perEventVolumeRtpcName.Trim(),
                        volumeRtpcValue,
                        playingId,
                        out string volumeFailure))
                {
                    RecordOperationalFailure(volumeFailure);
                }
            }

            return true;
        }

        private GameObject CreateTransientGameObject(Playback playback)
        {
            var transient = new GameObject($"WwiseTransient_{playback.HandleId}")
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave,
            };
            transient.transform.SetParent(transform, false);
            transient.transform.position = playback.WorldPosition;
            return transient;
        }

        private void OnEventEnded(ulong handleId, uint playingId)
        {
            if (!playbacks.TryGetValue(handleId, out Playback playback) ||
                playback.PlayingId != playingId)
            {
                return;
            }

            ReleasePlayback(playback);
        }

        private void StopPlayback(ulong handleId, float fadeSeconds)
        {
            if (!playbacks.TryGetValue(handleId, out Playback playback))
            {
                return;
            }

            if (playback.PlayingId != soundEngineApi.InvalidPlayingId &&
                soundEngineApi.IsInitialized)
            {
                int fadeMilliseconds = Mathf.RoundToInt(
                    Mathf.Clamp(FiniteOrZero(fadeSeconds), 0f, MaximumFadeSeconds) *
                    1000f);
                if (!soundEngineApi.StopPlayingId(
                        playback.PlayingId,
                        fadeMilliseconds,
                        out string failure))
                {
                    RecordOperationalFailure(failure);
                }
            }

            ReleasePlayback(playback);
        }

        private void ReleasePlayback(Playback playback)
        {
            if (playback.DelayCoroutine != null)
            {
                StopCoroutine(playback.DelayCoroutine);
                playback.DelayCoroutine = null;
            }

            playbacks.Remove(playback.HandleId);
            if (playback.OwnsTarget && playback.Target != null)
            {
                if (soundEngineApi != null && soundEngineApi.IsInitialized)
                {
                    soundEngineApi.UnregisterGameObject(playback.Target, out _);
                }

                DestroyOwnedGameObject(playback.Target);
            }

            playback.Target = null;
            playback.OwnsTarget = false;
            playback.PlayingId = soundEngineApi?.InvalidPlayingId ?? 0U;
        }

        private void ApplyNormalizedSetting(AudioParameterId parameterId, float value01)
        {
            if (parameterMap == null ||
                !parameterMap.TryGet(parameterId, out AudioParameterMapEntry mapping) ||
                mapping == null)
            {
                RecordOperationalFailure(
                    $"Wwise settings RTPC {parameterId} has no mapping.");
                return;
            }

            float normalized = Mathf.Clamp01(FiniteOrZero(value01));
            float mapped = mapping.MaximumValue < mapping.MinimumValue
                ? mapping.MinimumValue
                : Mathf.Lerp(mapping.MinimumValue, mapping.MaximumValue, normalized);
            SetParameter(parameterId, mapped);
        }

        private float ResolveEffectiveMaster(in AudioSettingsState value)
        {
            if (value.MuteOnFocusLoss &&
                listenerContext.IsValid &&
                !listenerContext.HasFocus)
            {
                return 0f;
            }

            return value.Master01;
        }

        private void UpdateEmitterPositions()
        {
            emitterRemovalBuffer.Clear();
            foreach (KeyValuePair<string, EmitterRegistration> pair in emitters)
            {
                EmitterRegistration registration = pair.Value;
                if (IsNullOrDestroyed(registration.Emitter) ||
                    registration.Emitter.AudioTransform == null ||
                    registration.GameObject == null)
                {
                    emitterRemovalBuffer.Add(pair.Key);
                    continue;
                }

                if (registration.RegisteredWithEngine &&
                    registration.Emitter.IsAudioEmitterActive)
                {
                    soundEngineApi.SetObjectPosition(
                        registration.GameObject,
                        registration.Emitter.AudioTransform,
                        out _);
                }
            }

            for (int index = 0; index < emitterRemovalBuffer.Count; index++)
            {
                UnregisterEmitterById(emitterRemovalBuffer[index]);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            emitterRemovalBuffer.Clear();
            foreach (KeyValuePair<string, EmitterRegistration> pair in emitters)
            {
                if (IsNullOrDestroyed(pair.Value.Emitter) ||
                    pair.Value.Emitter.OwningSceneHandle == scene.handle)
                {
                    emitterRemovalBuffer.Add(pair.Key);
                }
            }

            for (int index = 0; index < emitterRemovalBuffer.Count; index++)
            {
                UnregisterEmitterById(emitterRemovalBuffer[index]);
            }
        }

        private bool UnregisterEmitterById(string stableId)
        {
            if (!emitters.TryGetValue(stableId, out EmitterRegistration registration))
            {
                return false;
            }

            StopPlaybacksForEmitter(registration.Emitter);
            emitters.Remove(stableId);
            lastParameterValues.Clear();
            if (registration.RegisteredWithEngine &&
                registration.GameObject != null &&
                soundEngineApi != null &&
                soundEngineApi.IsInitialized &&
                !soundEngineApi.UnregisterGameObject(
                    registration.GameObject,
                    out string failure))
            {
                RecordOperationalFailure(failure);
            }

            return true;
        }

        private void UnregisterAllEmitters()
        {
            emitterRemovalBuffer.Clear();
            foreach (string stableId in emitters.Keys)
            {
                emitterRemovalBuffer.Add(stableId);
            }

            for (int index = 0; index < emitterRemovalBuffer.Count; index++)
            {
                UnregisterEmitterById(emitterRemovalBuffer[index]);
            }
        }

        private void UnregisterBackendGameObject()
        {
            if (!backendGameObjectRegistered)
            {
                return;
            }

            if (soundEngineApi != null && soundEngineApi.IsInitialized)
            {
                soundEngineApi.UnregisterGameObject(gameObject, out _);
            }

            backendGameObjectRegistered = false;
        }

        private void StopPlaybacksForEmitter(IAudioEmitter emitter)
        {
            playbackRemovalBuffer.Clear();
            foreach (KeyValuePair<ulong, Playback> pair in playbacks)
            {
                if (ReferenceEquals(pair.Value.Emitter, emitter))
                {
                    playbackRemovalBuffer.Add(pair.Key);
                }
            }

            for (int index = 0; index < playbackRemovalBuffer.Count; index++)
            {
                StopPlayback(playbackRemovalBuffer[index], 0f);
            }
        }

        private void ReleaseAllPlaybacksWithoutStop()
        {
            playbackRemovalBuffer.Clear();
            foreach (ulong handleId in playbacks.Keys)
            {
                playbackRemovalBuffer.Add(handleId);
            }

            for (int index = 0; index < playbackRemovalBuffer.Count; index++)
            {
                if (playbacks.TryGetValue(
                        playbackRemovalBuffer[index],
                        out Playback playback))
                {
                    ReleasePlayback(playback);
                }
            }
        }

        private string FindRegisteredEmitterId(IAudioEmitter emitter)
        {
            foreach (KeyValuePair<string, EmitterRegistration> pair in emitters)
            {
                if (ReferenceEquals(pair.Value.Emitter, emitter))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private bool IsRegisteredEmitter(IAudioEmitter emitter) =>
            GetRegisteredEmitter(emitter) != null;

        private EmitterRegistration GetRegisteredEmitter(IAudioEmitter emitter)
        {
            if (IsNullOrDestroyed(emitter))
            {
                return null;
            }

            string stableId = emitter.StableId?.Trim() ?? string.Empty;
            return emitters.TryGetValue(stableId, out EmitterRegistration registration) &&
                   ReferenceEquals(registration.Emitter, emitter)
                ? registration
                : null;
        }

        private Playback FindPlayback(AudioEventId eventId, IAudioEmitter emitter)
        {
            foreach (KeyValuePair<ulong, Playback> pair in playbacks)
            {
                Playback playback = pair.Value;
                if (playback.EventId == eventId &&
                    ReferenceEquals(playback.Emitter, emitter))
                {
                    return playback;
                }
            }

            return null;
        }

        private bool IsPlaybackValid(ulong handleId, AudioEventId eventId) =>
            playbacks.TryGetValue(handleId, out Playback playback) &&
            playback.EventId == eventId;

        private bool IsPlaybackPlaying(ulong handleId, AudioEventId eventId) =>
            IsPlaybackValid(handleId, eventId) &&
            playbacks[handleId].PlayingId != soundEngineApi.InvalidPlayingId;

        private int CountPlayingVoices()
        {
            int count = 0;
            foreach (KeyValuePair<ulong, Playback> pair in playbacks)
            {
                if (soundEngineApi != null &&
                    pair.Value.PlayingId != soundEngineApi.InvalidPlayingId)
                {
                    count++;
                }
            }

            return count;
        }

        private ulong AllocateHandleId()
        {
            ulong handleId = nextHandleId++;
            if (nextHandleId == 0UL)
            {
                nextHandleId = 1UL;
            }

            return handleId;
        }

        private string CreateParameterScopeKey(
            AudioParameterId parameterId,
            IAudioEmitter emitter) =>
            emitter == null
                ? parameterId.Value
                : parameterId.Value + "\n" + (emitter.StableId?.Trim() ?? string.Empty);

        private void RecordOperationalFailure(string reason)
        {
            lastOperationalFailure = string.IsNullOrWhiteSpace(reason)
                ? "Wwise audio operation failed."
                : reason.Trim();
        }

        private static string NormalizeBankName(string bankName)
        {
            string normalized = bankName?.Trim() ?? string.Empty;
            return normalized.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(0, normalized.Length - 4)
                : normalized;
        }

        private static float FiniteOrZero(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        private static bool IsNullOrDestroyed(IAudioEmitter emitter)
        {
            if (ReferenceEquals(emitter, null))
            {
                return true;
            }

            return emitter is UnityEngine.Object unityObject && unityObject == null;
        }

        private static void DestroyOwnedGameObject(GameObject ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }

        private sealed class EmitterRegistration
        {
            public EmitterRegistration(
                string stableId,
                IAudioEmitter emitter,
                GameObject gameObject)
            {
                StableId = stableId;
                Emitter = emitter;
                GameObject = gameObject;
                RegisteredWithEngine = true;
            }

            public string StableId { get; }
            public IAudioEmitter Emitter { get; }
            public GameObject GameObject { get; }
            public bool RegisteredWithEngine { get; set; }
        }

        private sealed class Playback
        {
            public Playback(
                ulong handleId,
                AudioEventId eventId,
                IAudioEmitter emitter,
                AudioEventMapEntry mapping,
                Vector3 worldPosition,
                float volume01)
            {
                HandleId = handleId;
                EventId = eventId;
                Emitter = emitter;
                Mapping = mapping;
                WorldPosition = worldPosition;
                Volume01 = volume01;
            }

            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public IAudioEmitter Emitter { get; }
            public AudioEventMapEntry Mapping { get; }
            public Vector3 WorldPosition { get; }
            public float Volume01 { get; }
            public uint PlayingId { get; set; }
            public GameObject Target { get; set; }
            public bool OwnsTarget { get; set; }
            public Coroutine DelayCoroutine { get; set; }
        }

        private sealed class WwiseAudioEventHandle : IAudioEventHandle
        {
            private WwiseAudioBackend backend;

            public WwiseAudioEventHandle(
                WwiseAudioBackend backend,
                ulong handleId,
                AudioEventId eventId)
            {
                this.backend = backend;
                HandleId = handleId;
                EventId = eventId;
            }

            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public bool IsValid => backend != null &&
                                   backend.IsPlaybackValid(HandleId, EventId);
            public bool IsPlaying => backend != null &&
                                     backend.IsPlaybackPlaying(HandleId, EventId);

            public void Stop(float fadeSeconds = 0f)
            {
                backend?.StopPlayback(HandleId, fadeSeconds);
            }

            public void Dispose()
            {
                Stop();
                backend = null;
            }
        }
    }
}
