namespace MSC.UI.Runtime.Settings
{
    public static class UiSettingsDefaults
    {
        public static UiSettingsDocument Create()
        {
            var document = new UiSettingsDocument
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
                    DlssEnabled = false,
                    DlssQuality = UiDlssQuality.Balanced,
                    AntiAliasingMode = UiAntiAliasingMode.Taa,
                    AntiAliasingPreset = UiAntiAliasingPreset.High,
                    AntiAliasingSharpening =
                        GraphicsSettingsDto.DefaultAntiAliasingSharpening,
                    HorizontalFieldOfViewDegrees =
                        GraphicsSettingsDto.DefaultHorizontalFieldOfViewDegrees,
                    CameraFarClipMeters =
                        GraphicsSettingsDto.DefaultCameraFarClipMeters,
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
                    ShowFpsCounter = true,
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

            ApplyAntiAliasingPreset(
                document.Graphics,
                UiAntiAliasingPreset.High);
            return document;
        }

        public static void ApplyAntiAliasingPreset(
            GraphicsSettingsDto graphics,
            UiAntiAliasingPreset preset)
        {
            if (graphics == null)
            {
                throw new System.ArgumentNullException(nameof(graphics));
            }

            graphics.AntiAliasingPreset = preset;
            if (preset == UiAntiAliasingPreset.Custom)
            {
                return;
            }

            graphics.DlssEnabled = false;
            if (graphics.DlssQuality == UiDlssQuality.Dlaa)
            {
                graphics.DlssQuality = UiDlssQuality.Balanced;
            }
            switch (preset)
            {
                case UiAntiAliasingPreset.Low:
                    graphics.AntiAliasingMode = UiAntiAliasingMode.Fxaa;
                    graphics.AntiAliasingSharpening = 0f;
                    break;
                case UiAntiAliasingPreset.Medium:
                    graphics.AntiAliasingMode = UiAntiAliasingMode.Smaa;
                    graphics.AntiAliasingSharpening = 0f;
                    break;
                case UiAntiAliasingPreset.Ultra:
                    graphics.AntiAliasingMode = UiAntiAliasingMode.Taa;
                    graphics.AntiAliasingSharpening = 0.34f;
                    break;
                default:
                    graphics.AntiAliasingMode = UiAntiAliasingMode.Taa;
                    graphics.AntiAliasingSharpening =
                        GraphicsSettingsDto.DefaultAntiAliasingSharpening;
                    break;
            }
        }
    }
}
