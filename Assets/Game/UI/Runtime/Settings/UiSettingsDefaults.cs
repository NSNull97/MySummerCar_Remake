namespace MSC.UI.Runtime.Settings
{
    public static class UiSettingsDefaults
    {
        public static UiSettingsDocument Create()
        {
            return new UiSettingsDocument
            {
                SchemaVersion = UiSettingsDocument.CurrentSchemaVersion,
                Graphics = new GraphicsSettingsDto
                {
                    DisplayMode = UiDisplayMode.FullscreenWindow,
                    ResolutionWidth = 1920,
                    ResolutionHeight = 1080,
                    RefreshRateNumerator = 60,
                    RefreshRateDenominator = 1,
                    VSync = true,
                    QualityLevel = 2,
                    MotionBlur = false,
                    DepthOfField = false,
                },
                Audio = new AudioSettingsDto
                {
                    Master01 = 1f,
                    Engine01 = 1f,
                    Environment01 = 1f,
                    Effects01 = 1f,
                    Music01 = 0.65f,
                    Ui01 = 0.8f,
                    DynamicRange = UiDynamicRangeMode.Standard,
                    MuteWhenUnfocused = false,
                    SubtitlesEnabled = false,
                    CaptionsEnabled = false,
                    ReducedLoudSounds = false,
                    OutputDeviceId = "system.default",
                },
                Controls = new ControlsSettingsDto
                {
                    MouseSensitivity = 1f,
                    InvertMouseY = false,
                    GamepadSensitivity = 1f,
                    InvertGamepadY = false,
                    GamepadDeadzone = 0.125f,
                    VibrationEnabled = true,
                    PlayerBindingOverridesJson = string.Empty,
                    VehicleBindingOverridesJson = string.Empty,
                },
                Gameplay = new GameplaySettingsDto
                {
                    HudMode = UiHudMode.Full,
                    Units = UiUnitSystem.Metric,
                    LanguageId = "ru-RU",
                    FatigueVisualIntensity01 = 1f,
                    AlcoholVisualIntensity01 = 1f,
                    ContextualHints = true,
                    InteractionOutlines = true,
                    CameraShakeIntensity01 = 0.75f,
                    DevelopmentUiVisible = false,
                },
                Accessibility = new AccessibilitySettingsDto
                {
                    UiScale = 1f,
                    HighContrast = false,
                    ReducedMotion = false,
                    ToggleHoldActions = false,
                    ColorIndependentCues = true,
                },
            };
        }
    }
}
