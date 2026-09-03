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

    /// <summary>
    /// Optional backend capability for removable, Resources-backed Phase 1
    /// presentation libraries. Runtime gameplay remains coupled only to stable
    /// audio event IDs and never receives the concrete library asset.
    /// </summary>
    public interface IAudioSupplementalContentBackend
    {
        bool TryLoadSupplementalEventLibrary(
            string resourcesPath,
            out string failure);
    }

    /// <summary>
    /// Optional fallback capability for hash-locked, removable Phase 1 media
    /// that intentionally replaces a primary placeholder while retaining the
    /// same project-owned stable event ID.
    /// </summary>
    public interface IAudioOverrideContentBackend
    {
        bool TryLoadOverrideEventLibrary(
            string resourcesPath,
            out string failure);
    }
}
