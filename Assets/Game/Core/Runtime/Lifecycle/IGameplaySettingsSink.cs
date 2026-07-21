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
}
