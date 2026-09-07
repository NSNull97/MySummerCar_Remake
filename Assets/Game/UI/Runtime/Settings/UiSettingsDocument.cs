using System;

namespace MSC.UI.Runtime.Settings
{
    public enum UiDisplayMode
    {
        Windowed = 0,
        FullscreenWindow = 1,
        ExclusiveFullscreen = 2,
    }

    public enum UiDynamicRangeMode
    {
        Night = 0,
        Standard = 1,
        Wide = 2,
    }

    public enum UiDlssQuality
    {
        Balanced = 0,
        Quality = 1,
        Performance = 2,
        UltraPerformance = 3,
        Dlaa = 4,
    }

    public enum UiAntiAliasingMode
    {
        Off = 0,
        Fxaa = 1,
        Smaa = 2,
        Taa = 3,
        TemporalUpscaler = 4,
        MaximumQuality = 5,
    }

    public enum UiAntiAliasingPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3,
        Custom = 4,
    }

    public enum UiHudMode
    {
        Off = 0,
        Contextual = 1,
        Full = 2,
    }

    public enum UiUnitSystem
    {
        Metric = 0,
        Imperial = 1,
    }

    [Serializable]
    public sealed class UiSettingsDocument
    {
        public const int CurrentSchemaVersion = 7;

        public int SchemaVersion = CurrentSchemaVersion;
        // A menu preference, separate from the paint state of a saved vehicle.
        public int MainMenuCarColourIndex;
        public GraphicsSettingsDto Graphics = new GraphicsSettingsDto();
        public AudioSettingsDto Audio = new AudioSettingsDto();
        public ControlsSettingsDto Controls = new ControlsSettingsDto();
        public GameplaySettingsDto Gameplay = new GameplaySettingsDto();
        public AccessibilitySettingsDto Accessibility = new AccessibilitySettingsDto();

        public UiSettingsDocument DeepClone()
        {
            return new UiSettingsDocument
            {
                SchemaVersion = SchemaVersion,
                MainMenuCarColourIndex = MainMenuCarColourIndex,
                Graphics = Graphics == null ? null : Graphics.DeepClone(),
                Audio = Audio == null ? null : Audio.DeepClone(),
                Controls = Controls == null ? null : Controls.DeepClone(),
                Gameplay = Gameplay == null ? null : Gameplay.DeepClone(),
                Accessibility = Accessibility == null ? null : Accessibility.DeepClone(),
            };
        }

        public bool ContentEquals(UiSettingsDocument other)
        {
            return other != null &&
                   SchemaVersion == other.SchemaVersion &&
                   MainMenuCarColourIndex == other.MainMenuCarColourIndex &&
                   Graphics != null && Graphics.ContentEquals(other.Graphics) &&
                   Audio != null && Audio.ContentEquals(other.Audio) &&
                   Controls != null && Controls.ContentEquals(other.Controls) &&
                   Gameplay != null && Gameplay.ContentEquals(other.Gameplay) &&
                   Accessibility != null && Accessibility.ContentEquals(other.Accessibility);
        }

        public void Validate()
        {
            UiSettingsValidator.Validate(this);
        }
    }

    [Serializable]
    public sealed class GraphicsSettingsDto
    {
        public const float DefaultHorizontalFieldOfViewDegrees = 120f;
        public const float MinimumHorizontalFieldOfViewDegrees = 60f;
        public const float MaximumHorizontalFieldOfViewDegrees = 140f;
        public const float DefaultCameraFarClipMeters = 500f;
        public const float MinimumCameraFarClipMeters = 100f;
        public const float MaximumCameraFarClipMeters = 5000f;
        public const float DefaultAntiAliasingSharpening = 0.28f;
        public const float MinimumAntiAliasingSharpening = 0f;
        public const float MaximumAntiAliasingSharpening = 0.75f;

        public UiDisplayMode DisplayMode;
        public int ResolutionWidth;
        public int ResolutionHeight;
        public int RefreshRateNumerator;
        public int RefreshRateDenominator;
        public bool VSync;
        public int QualityLevel;
        public bool MotionBlur;
        public bool DepthOfField;
        public bool DlssEnabled;
        public UiDlssQuality DlssQuality;
        public UiAntiAliasingMode AntiAliasingMode;
        public UiAntiAliasingPreset AntiAliasingPreset;
        public float AntiAliasingSharpening;
        public float HorizontalFieldOfViewDegrees;
        public float CameraFarClipMeters;

        public GraphicsSettingsDto DeepClone()
        {
            return (GraphicsSettingsDto)MemberwiseClone();
        }

        public bool ContentEquals(GraphicsSettingsDto other)
        {
            return other != null &&
                   DisplayMode == other.DisplayMode &&
                   ResolutionWidth == other.ResolutionWidth &&
                   ResolutionHeight == other.ResolutionHeight &&
                   RefreshRateNumerator == other.RefreshRateNumerator &&
                   RefreshRateDenominator == other.RefreshRateDenominator &&
                   VSync == other.VSync &&
                   QualityLevel == other.QualityLevel &&
                   MotionBlur == other.MotionBlur &&
                   DepthOfField == other.DepthOfField &&
                   DlssEnabled == other.DlssEnabled &&
                   DlssQuality == other.DlssQuality &&
                   AntiAliasingMode == other.AntiAliasingMode &&
                   AntiAliasingPreset == other.AntiAliasingPreset &&
                   AntiAliasingSharpening.Equals(
                       other.AntiAliasingSharpening) &&
                   HorizontalFieldOfViewDegrees.Equals(
                       other.HorizontalFieldOfViewDegrees) &&
                   CameraFarClipMeters.Equals(other.CameraFarClipMeters);
        }
    }

    [Serializable]
    public sealed class AudioSettingsDto
    {
        public float Master01;
        public float Engine01;
        public float Environment01;
        public float Effects01;
        public float Music01;
        public float Ui01;
        public UiDynamicRangeMode DynamicRange;
        public bool MuteWhenUnfocused;
        public bool SubtitlesEnabled;
        public bool CaptionsEnabled;
        public bool ReducedLoudSounds;
        public string OutputDeviceId = string.Empty;

        public AudioSettingsDto DeepClone()
        {
            return (AudioSettingsDto)MemberwiseClone();
        }

        public bool ContentEquals(AudioSettingsDto other)
        {
            return other != null &&
                   Master01.Equals(other.Master01) &&
                   Engine01.Equals(other.Engine01) &&
                   Environment01.Equals(other.Environment01) &&
                   Effects01.Equals(other.Effects01) &&
                   Music01.Equals(other.Music01) &&
                   Ui01.Equals(other.Ui01) &&
                   DynamicRange == other.DynamicRange &&
                   MuteWhenUnfocused == other.MuteWhenUnfocused &&
                   SubtitlesEnabled == other.SubtitlesEnabled &&
                   CaptionsEnabled == other.CaptionsEnabled &&
                   ReducedLoudSounds == other.ReducedLoudSounds &&
                   string.Equals(OutputDeviceId, other.OutputDeviceId, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class ControlsSettingsDto
    {
        public float MouseSensitivity;
        public bool InvertMouseY;
        public float GamepadSensitivity;
        public bool InvertGamepadY;
        public float GamepadDeadzone;
        public bool VibrationEnabled;
        public string PlayerBindingOverridesJson = string.Empty;
        public string VehicleBindingOverridesJson = string.Empty;

        public ControlsSettingsDto DeepClone()
        {
            return (ControlsSettingsDto)MemberwiseClone();
        }

        public bool ContentEquals(ControlsSettingsDto other)
        {
            return other != null &&
                   MouseSensitivity.Equals(other.MouseSensitivity) &&
                   InvertMouseY == other.InvertMouseY &&
                   GamepadSensitivity.Equals(other.GamepadSensitivity) &&
                   InvertGamepadY == other.InvertGamepadY &&
                   GamepadDeadzone.Equals(other.GamepadDeadzone) &&
                   VibrationEnabled == other.VibrationEnabled &&
                   string.Equals(PlayerBindingOverridesJson, other.PlayerBindingOverridesJson, StringComparison.Ordinal) &&
                   string.Equals(VehicleBindingOverridesJson, other.VehicleBindingOverridesJson, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class GameplaySettingsDto
    {
        public UiHudMode HudMode;
        public UiUnitSystem Units;
        public string LanguageId = string.Empty;
        public float FatigueVisualIntensity01;
        public float AlcoholVisualIntensity01;
        public bool ContextualHints;
        public bool InteractionOutlines;
        public float CameraShakeIntensity01;
        public bool DevelopmentUiVisible;
        public bool ShowFpsCounter;

        public GameplaySettingsDto DeepClone()
        {
            return (GameplaySettingsDto)MemberwiseClone();
        }

        public bool ContentEquals(GameplaySettingsDto other)
        {
            return other != null &&
                   HudMode == other.HudMode &&
                   Units == other.Units &&
                   string.Equals(LanguageId, other.LanguageId, StringComparison.Ordinal) &&
                   FatigueVisualIntensity01.Equals(other.FatigueVisualIntensity01) &&
                   AlcoholVisualIntensity01.Equals(other.AlcoholVisualIntensity01) &&
                   ContextualHints == other.ContextualHints &&
                   InteractionOutlines == other.InteractionOutlines &&
                   CameraShakeIntensity01.Equals(other.CameraShakeIntensity01) &&
                   DevelopmentUiVisible == other.DevelopmentUiVisible &&
                   ShowFpsCounter == other.ShowFpsCounter;
        }
    }

    [Serializable]
    public sealed class AccessibilitySettingsDto
    {
        public float UiScale;
        public bool HighContrast;
        public bool ReducedMotion;
        public bool ToggleHoldActions;
        public bool ColorIndependentCues;

        public AccessibilitySettingsDto DeepClone()
        {
            return (AccessibilitySettingsDto)MemberwiseClone();
        }

        public bool ContentEquals(AccessibilitySettingsDto other)
        {
            return other != null &&
                   UiScale.Equals(other.UiScale) &&
                   HighContrast == other.HighContrast &&
                   ReducedMotion == other.ReducedMotion &&
                   ToggleHoldActions == other.ToggleHoldActions &&
                   ColorIndependentCues == other.ColorIndependentCues;
        }
    }
}
