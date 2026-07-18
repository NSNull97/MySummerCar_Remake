namespace MSC.Audio
{
    /// <summary>
    /// Project-owned runtime boundary shared by Unity Audio and the optional
    /// official Wwise adapter. Gameplay code must never call vendor APIs.
    /// </summary>
    public interface IAudioBackend
    {
        string BackendId { get; }

        AudioBackendKind Kind { get; }

        bool IsReady { get; }

        string FailureReason { get; }

        bool RegisterEmitter(IAudioEmitter emitter, out string failure);

        bool UnregisterEmitter(IAudioEmitter emitter);

        IAudioEventHandle PostEvent(in AudioEventRequest request);

        bool SetParameter(
            AudioParameterId parameterId,
            float value,
            IAudioEmitter emitter = null);

        bool SetSwitch(
            AudioSwitchId switchGroupId,
            AudioSwitchId switchValueId,
            IAudioEmitter emitter = null);

        bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId);

        void SetListenerContext(in AudioListenerContext context);

        void ApplySettings(in AudioSettingsState settings);

        void StopAll(float fadeSeconds = 0f);

        AudioRuntimeSnapshot CaptureSnapshot();
    }
}
