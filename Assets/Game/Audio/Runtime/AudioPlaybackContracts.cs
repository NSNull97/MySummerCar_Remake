using System;
using UnityEngine;

namespace MSC.Audio
{
    public enum AudioBackendKind
    {
        Silent = 0,
        Unity = 1,
        Wwise = 2,
    }

    public enum AudioDynamicRangeMode
    {
        Night = 0,
        Balanced = 1,
        Wide = 2,
    }

    public interface IAudioEmitter
    {
        string StableId { get; }

        Transform AudioTransform { get; }

        int OwningSceneHandle { get; }

        bool IsAudioEmitterActive { get; }

        AudioSurfaceContext SurfaceContext { get; }

        AudioEnvironmentContext EnvironmentContext { get; }
    }

    public interface IAudioEventHandle : IDisposable
    {
        ulong HandleId { get; }

        AudioEventId EventId { get; }

        bool IsValid { get; }

        bool IsPlaying { get; }

        void Stop(float fadeSeconds = 0f);
    }

    /// <summary>
    /// Shared non-playing handle for rejected or unavailable audio events. This
    /// keeps callers null-free without pretending that playback succeeded.
    /// </summary>
    public static class AudioEventHandles
    {
        public static IAudioEventHandle Invalid => InvalidAudioEventHandle.Instance;
    }

    public readonly struct AudioEventRequest
    {
        public AudioEventRequest(
            AudioEventId eventId,
            IAudioEmitter emitter = null,
            Vector3 worldPosition = default,
            float volume01 = 1f,
            double delaySeconds = 0d,
            bool allowMultiple = true)
        {
            if (eventId.IsEmpty)
            {
                throw new ArgumentException("Audio event ID is required.", nameof(eventId));
            }

            if (double.IsNaN(delaySeconds) ||
                double.IsInfinity(delaySeconds) ||
                delaySeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            }

            EventId = eventId;
            Emitter = emitter;
            WorldPosition = AudioMath.FiniteVector(worldPosition);
            Volume01 = AudioMath.Clamp01(volume01);
            DelaySeconds = delaySeconds;
            AllowMultiple = allowMultiple;
        }

        public AudioEventId EventId { get; }
        public IAudioEmitter Emitter { get; }
        public Vector3 WorldPosition { get; }
        public float Volume01 { get; }
        public double DelaySeconds { get; }
        public bool AllowMultiple { get; }

        public Vector3 ResolveWorldPosition() =>
            Emitter?.AudioTransform == null
                ? WorldPosition
                : Emitter.AudioTransform.position;
    }

    public readonly struct AudioSettingsState
    {
        public AudioSettingsState(
            float master01,
            float vehicle01,
            float effects01,
            float ambience01,
            float music01,
            float ui01,
            AudioDynamicRangeMode dynamicRange,
            bool muteOnFocusLoss,
            bool subtitlesEnabled,
            bool captionsEnabled,
            bool reduceLoudSounds,
            string outputDeviceId = "")
        {
            if (!Enum.IsDefined(typeof(AudioDynamicRangeMode), dynamicRange))
            {
                throw new ArgumentOutOfRangeException(nameof(dynamicRange));
            }

            Master01 = AudioMath.Clamp01(master01);
            Vehicle01 = AudioMath.Clamp01(vehicle01);
            Effects01 = AudioMath.Clamp01(effects01);
            Ambience01 = AudioMath.Clamp01(ambience01);
            Music01 = AudioMath.Clamp01(music01);
            Ui01 = AudioMath.Clamp01(ui01);
            DynamicRange = dynamicRange;
            MuteOnFocusLoss = muteOnFocusLoss;
            SubtitlesEnabled = subtitlesEnabled;
            CaptionsEnabled = captionsEnabled;
            ReduceLoudSounds = reduceLoudSounds;
            OutputDeviceId = outputDeviceId?.Trim() ?? string.Empty;
        }

        public float Master01 { get; }
        public float Vehicle01 { get; }
        public float Effects01 { get; }
        public float Ambience01 { get; }
        public float Music01 { get; }
        public float Ui01 { get; }
        public AudioDynamicRangeMode DynamicRange { get; }
        public bool MuteOnFocusLoss { get; }
        public bool SubtitlesEnabled { get; }
        public bool CaptionsEnabled { get; }
        public bool ReduceLoudSounds { get; }
        public string OutputDeviceId { get; }

        public static AudioSettingsState Default => new AudioSettingsState(
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            AudioDynamicRangeMode.Balanced,
            muteOnFocusLoss: true,
            subtitlesEnabled: false,
            captionsEnabled: false,
            reduceLoudSounds: false);
    }

    public readonly struct AudioRuntimeSnapshot
    {
        public AudioRuntimeSnapshot(
            string backendId,
            AudioBackendKind kind,
            bool isReady,
            bool isFallback,
            int registeredEmitterCount,
            int activeVoiceCount,
            int loadedBankCount,
            string[] missingBanks,
            AudioListenerContext listener,
            string lastFailure)
        {
            BackendId = backendId ?? string.Empty;
            Kind = kind;
            IsReady = isReady;
            IsFallback = isFallback;
            RegisteredEmitterCount = Math.Max(0, registeredEmitterCount);
            ActiveVoiceCount = Math.Max(0, activeVoiceCount);
            LoadedBankCount = Math.Max(0, loadedBankCount);
            MissingBanks = missingBanks ?? Array.Empty<string>();
            Listener = listener;
            LastFailure = lastFailure ?? string.Empty;
        }

        public string BackendId { get; }
        public AudioBackendKind Kind { get; }
        public bool IsReady { get; }
        public bool IsFallback { get; }
        public int RegisteredEmitterCount { get; }
        public int ActiveVoiceCount { get; }
        public int LoadedBankCount { get; }
        public string[] MissingBanks { get; }
        public AudioListenerContext Listener { get; }
        public string LastFailure { get; }
    }

    internal sealed class InvalidAudioEventHandle : IAudioEventHandle
    {
        public static readonly InvalidAudioEventHandle Instance = new InvalidAudioEventHandle();

        private InvalidAudioEventHandle()
        {
        }

        public ulong HandleId => 0UL;
        public AudioEventId EventId => default;
        public bool IsValid => false;
        public bool IsPlaying => false;
        public void Stop(float fadeSeconds = 0f)
        {
        }

        public void Dispose()
        {
        }
    }
}
