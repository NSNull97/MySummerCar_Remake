namespace MSC.Core.Lifecycle
{
    /// <summary>
    /// Narrow project-owned boundary for applying persisted look controls without
    /// coupling the UI assembly to the concrete player module.
    /// </summary>
    public interface IPlayerLookSettingsSink
    {
        void ApplyLookSettings(
            float mouseSensitivityMultiplier,
            bool invertMouseY,
            float gamepadSensitivityMultiplier,
            bool invertGamepadY);
    }

    /// <summary>
    /// Narrow project-owned boundary for applying persisted gameplay-camera
    /// settings without coupling UI to the concrete player module.
    /// </summary>
    public interface IPlayerCameraSettingsSink
    {
        void ApplyCameraSettings(
            float horizontalFieldOfViewDegrees,
            float farClipPlaneMeters);
    }

    /// <summary>
    /// Applies the player-selected gameplay locale to presentation adapters
    /// without coupling the settings UI to concrete gameplay assemblies.
    /// </summary>
    public interface IGameplayLocaleSettingsSink
    {
        void ApplyGameplayLocale(string localeId);
    }
}
