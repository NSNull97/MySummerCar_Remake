using System;

namespace MSC.UI.Runtime.Settings
{
    public static class UiSettingsValidator
    {
        private const int MaximumOpaqueJsonLength = 1024 * 1024;

        public static void Validate(UiSettingsDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.SchemaVersion != UiSettingsDocument.CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(document.SchemaVersion),
                    document.SchemaVersion,
                    "Only the current UI settings schema may enter runtime state.");
            }

            ValidateGraphics(document.Graphics);
            ValidateAudio(document.Audio);
            ValidateControls(document.Controls);
            ValidateGameplay(document.Gameplay);
            ValidateAccessibility(document.Accessibility);
        }

        private static void ValidateGraphics(GraphicsSettingsDto value)
        {
            RequireCategory(value, nameof(UiSettingsDocument.Graphics));
            RequireEnum(value.DisplayMode, nameof(value.DisplayMode));
            RequireRange(value.ResolutionWidth, 640, 16384, nameof(value.ResolutionWidth));
            RequireRange(value.ResolutionHeight, 480, 8640, nameof(value.ResolutionHeight));
            RequireRange(value.RefreshRateNumerator, 1, 1000000, nameof(value.RefreshRateNumerator));
            RequireRange(value.RefreshRateDenominator, 1, 10000, nameof(value.RefreshRateDenominator));
            RequireRange(value.QualityLevel, 0, 64, nameof(value.QualityLevel));
            RequireEnum(value.DlssQuality, nameof(value.DlssQuality));
            RequireEnum(value.AntiAliasingMode, nameof(value.AntiAliasingMode));
            RequireEnum(value.AntiAliasingPreset, nameof(value.AntiAliasingPreset));
            RequireFloatRange(
                value.AntiAliasingSharpening,
                GraphicsSettingsDto.MinimumAntiAliasingSharpening,
                GraphicsSettingsDto.MaximumAntiAliasingSharpening,
                nameof(value.AntiAliasingSharpening));
            RequireFloatRange(
                value.HorizontalFieldOfViewDegrees,
                GraphicsSettingsDto.MinimumHorizontalFieldOfViewDegrees,
                GraphicsSettingsDto.MaximumHorizontalFieldOfViewDegrees,
                nameof(value.HorizontalFieldOfViewDegrees));
            RequireFloatRange(
                value.CameraFarClipMeters,
                GraphicsSettingsDto.MinimumCameraFarClipMeters,
                GraphicsSettingsDto.MaximumCameraFarClipMeters,
                nameof(value.CameraFarClipMeters));
        }

        private static void ValidateAudio(AudioSettingsDto value)
        {
            RequireCategory(value, nameof(UiSettingsDocument.Audio));
            RequireNormalized(value.Master01, nameof(value.Master01));
            RequireNormalized(value.Engine01, nameof(value.Engine01));
            RequireNormalized(value.Environment01, nameof(value.Environment01));
            RequireNormalized(value.Effects01, nameof(value.Effects01));
            RequireNormalized(value.Music01, nameof(value.Music01));
            RequireNormalized(value.Ui01, nameof(value.Ui01));
            RequireEnum(value.DynamicRange, nameof(value.DynamicRange));
            RequireIdentifier(value.OutputDeviceId, 256, nameof(value.OutputDeviceId));
        }

        private static void ValidateControls(ControlsSettingsDto value)
        {
            RequireCategory(value, nameof(UiSettingsDocument.Controls));
            RequireFloatRange(value.MouseSensitivity, 0.05f, 10f, nameof(value.MouseSensitivity));
            RequireFloatRange(value.GamepadSensitivity, 0.05f, 10f, nameof(value.GamepadSensitivity));
            RequireFloatRange(value.GamepadDeadzone, 0f, 0.95f, nameof(value.GamepadDeadzone));
            RequireOpaqueJson(value.PlayerBindingOverridesJson, nameof(value.PlayerBindingOverridesJson));
            RequireOpaqueJson(value.VehicleBindingOverridesJson, nameof(value.VehicleBindingOverridesJson));
        }

        private static void ValidateGameplay(GameplaySettingsDto value)
        {
            RequireCategory(value, nameof(UiSettingsDocument.Gameplay));
            RequireEnum(value.HudMode, nameof(value.HudMode));
            RequireEnum(value.Units, nameof(value.Units));
            RequireSupportedLanguage(value.LanguageId, nameof(value.LanguageId));
            RequireNormalized(value.FatigueVisualIntensity01, nameof(value.FatigueVisualIntensity01));
            RequireNormalized(value.AlcoholVisualIntensity01, nameof(value.AlcoholVisualIntensity01));
            RequireNormalized(value.CameraShakeIntensity01, nameof(value.CameraShakeIntensity01));
        }

        private static void ValidateAccessibility(AccessibilitySettingsDto value)
        {
            RequireCategory(value, nameof(UiSettingsDocument.Accessibility));
            RequireFloatRange(value.UiScale, 0.75f, 1.5f, nameof(value.UiScale));
        }

        private static void RequireCategory(object category, string name)
        {
            if (category == null)
            {
                throw new ArgumentException($"UI settings category '{name}' is missing.", name);
            }
        }

        private static void RequireNormalized(float value, string name)
        {
            RequireFloatRange(value, 0f, 1f, name);
        }

        private static void RequireFloatRange(float value, float minimum, float maximum, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(name, value, $"Expected a finite value in [{minimum}, {maximum}].");
            }
        }

        private static void RequireRange(int value, int minimum, int maximum, string name)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(name, value, $"Expected a value in [{minimum}, {maximum}].");
            }
        }

        private static void RequireIdentifier(string value, int maximumLength, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.IndexOf('\0') >= 0)
            {
                throw new ArgumentException($"'{name}' must be a non-empty bounded identifier.", name);
            }
        }

        private static void RequireSupportedLanguage(string value, string name)
        {
            RequireIdentifier(value, 64, name);
            if (!string.Equals(value, "en-US", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, "ru-RU", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"'{name}' must identify a supported UI locale (en-US or ru-RU).",
                    name);
            }
        }

        private static void RequireOpaqueJson(string value, string name)
        {
            if (value == null || value.Length > MaximumOpaqueJsonLength || value.IndexOf('\0') >= 0)
            {
                throw new ArgumentException($"'{name}' is not a valid bounded opaque JSON payload.", name);
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            bool objectShape = trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
            bool arrayShape = trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']';
            if (!objectShape && !arrayShape)
            {
                throw new ArgumentException($"'{name}' must be empty or contain a JSON object/array payload.", name);
            }
        }

        private static void RequireEnum<T>(T value, string name) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(name, value, "Unknown persisted enum value.");
            }
        }
    }
}
