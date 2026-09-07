using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MSC.UI.Runtime.Settings
{
    public enum UiSettingsLoadStatus
    {
        Loaded = 0,
        MissingCreatedDefaults = 1,
        MigratedAndSaved = 2,
        CorruptQuarantinedDefaultsCreated = 3,
    }

    public sealed class UiSettingsLoadResult
    {
        public UiSettingsLoadResult(
            UiSettingsDocument document,
            UiSettingsLoadStatus status,
            string quarantinedPath)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Status = status;
            QuarantinedPath = quarantinedPath ?? string.Empty;
        }

        public UiSettingsDocument Document { get; }

        public UiSettingsLoadStatus Status { get; }

        public string QuarantinedPath { get; }
    }

    public sealed class UiSettingsJsonStore
    {
        private readonly string path;

        public UiSettingsJsonStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A settings path is required.", nameof(path));
            }

            this.path = System.IO.Path.GetFullPath(path);
        }

        public string Path => path;

        public UiSettingsLoadResult LoadOrCreate()
        {
            if (!File.Exists(path))
            {
                UiSettingsDocument defaults = UiSettingsDefaults.Create();
                Save(defaults);
                return new UiSettingsLoadResult(
                    defaults.DeepClone(),
                    UiSettingsLoadStatus.MissingCreatedDefaults,
                    string.Empty);
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                UiSettingsDocument document = UiSettingsMigrator.DeserializeCurrent(json, out bool migrated);
                document.Validate();
                if (migrated)
                {
                    Save(document);
                }

                return new UiSettingsLoadResult(
                    document.DeepClone(),
                    migrated ? UiSettingsLoadStatus.MigratedAndSaved : UiSettingsLoadStatus.Loaded,
                    string.Empty);
            }
            catch (Exception exception) when (IsRecoverableDocumentFailure(exception))
            {
                string quarantinedPath = QuarantineCorruptFile();
                UiSettingsDocument defaults = UiSettingsDefaults.Create();
                Save(defaults);
                return new UiSettingsLoadResult(
                    defaults.DeepClone(),
                    UiSettingsLoadStatus.CorruptQuarantinedDefaultsCreated,
                    quarantinedPath);
            }
        }

        public void Save(UiSettingsDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            UiSettingsDocument snapshot = document.DeepClone();
            snapshot.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
            snapshot.Validate();

            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            byte[] payload = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(snapshot, true) + Environment.NewLine);

            try
            {
                WriteThrough(temporaryPath, payload);
                CommitTemporary(temporaryPath, backupPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static bool IsRecoverableDocumentFailure(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is FormatException ||
                   exception is InvalidDataException ||
                   exception is NotSupportedException ||
                   exception is OverflowException;
        }

        private static void WriteThrough(string destination, byte[] payload)
        {
            using (FileStream stream = new FileStream(
                       destination,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(true);
            }
        }

        private void CommitTemporary(string temporaryPath, string backupPath)
        {
            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }

            try
            {
                File.Replace(temporaryPath, path, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                CopyFallback(temporaryPath);
            }
            catch (IOException)
            {
                CopyFallback(temporaryPath);
            }
        }

        private void CopyFallback(string temporaryPath)
        {
            File.Copy(temporaryPath, path, true);
            File.Delete(temporaryPath);
        }

        private string QuarantineCorruptFile()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
            string candidate = path + ".corrupt." + timestamp;
            int suffix = 0;
            while (File.Exists(candidate))
            {
                suffix++;
                candidate = path + ".corrupt." + timestamp + "." + suffix;
            }

            File.Move(path, candidate);
            return candidate;
        }
    }

    internal static class UiSettingsMigrator
    {
        public static UiSettingsDocument DeserializeCurrent(string json, out bool migrated)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("UI settings JSON is empty.");
            }

            UiSettingsSchemaProbe probe = JsonUtility.FromJson<UiSettingsSchemaProbe>(json);
            if (probe == null)
            {
                throw new InvalidDataException("UI settings JSON did not produce a document.");
            }

            if (probe.SchemaVersion == UiSettingsDocument.CurrentSchemaVersion)
            {
                UiSettingsDocument current = JsonUtility.FromJson<UiSettingsDocument>(json);
                if (current == null)
                {
                    throw new InvalidDataException("UI settings JSON did not produce a current document.");
                }

                migrated = false;
                return current;
            }

            if (probe.SchemaVersion == 1)
            {
                UiSettingsDocumentV1 versionOne = JsonUtility.FromJson<UiSettingsDocumentV1>(json);
                if (versionOne == null)
                {
                    throw new InvalidDataException("UI settings JSON did not produce a version-one document.");
                }

                migrated = true;
                return MigrateVersionOne(versionOne);
            }

            if (probe.SchemaVersion == 2)
            {
                UiSettingsDocumentV2 versionTwo =
                    JsonUtility.FromJson<UiSettingsDocumentV2>(json);
                if (versionTwo == null)
                {
                    throw new InvalidDataException(
                        "UI settings JSON did not produce a version-two document.");
                }

                migrated = true;
                return MigrateVersionTwo(versionTwo);
            }

            if (probe.SchemaVersion == 3)
            {
                UiSettingsDocument versionThree =
                    JsonUtility.FromJson<UiSettingsDocument>(json);
                if (versionThree == null)
                {
                    throw new InvalidDataException(
                        "UI settings JSON did not produce a version-three document.");
                }
                if (versionThree.Graphics == null)
                {
                    throw new InvalidDataException(
                        "Version-three UI settings have no graphics category.");
                }

                versionThree.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
                versionThree.Graphics.DlssEnabled = false;
                versionThree.Graphics.DlssQuality = UiDlssQuality.Balanced;
                ApplyCameraDefaults(versionThree.Graphics);
                ApplyAntiAliasingDefaultsFromLegacy(versionThree.Graphics);
                versionThree.Validate();
                migrated = true;
                return versionThree;
            }

            if (probe.SchemaVersion == 4)
            {
                UiSettingsDocument versionFour =
                    JsonUtility.FromJson<UiSettingsDocument>(json);
                if (versionFour == null)
                {
                    throw new InvalidDataException(
                        "UI settings JSON did not produce a version-four document.");
                }
                if (versionFour.Graphics == null)
                {
                    throw new InvalidDataException(
                        "Version-four UI settings have no graphics category.");
                }

                versionFour.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
                ApplyCameraDefaults(versionFour.Graphics);
                ApplyAntiAliasingDefaultsFromLegacy(versionFour.Graphics);
                versionFour.Validate();
                migrated = true;
                return versionFour;
            }

            if (probe.SchemaVersion == 5)
            {
                UiSettingsDocument versionFive =
                    JsonUtility.FromJson<UiSettingsDocument>(json);
                if (versionFive == null)
                {
                    throw new InvalidDataException(
                        "UI settings JSON did not produce a version-five document.");
                }
                if (versionFive.Graphics == null)
                {
                    throw new InvalidDataException(
                        "Version-five UI settings have no graphics category.");
                }

                versionFive.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
                ApplyAntiAliasingDefaultsFromLegacy(versionFive.Graphics);
                versionFive.Validate();
                migrated = true;
                return versionFive;
            }

            if (probe.SchemaVersion == 6)
            {
                UiSettingsDocument versionSix =
                    JsonUtility.FromJson<UiSettingsDocument>(json);
                if (versionSix == null)
                {
                    throw new InvalidDataException(
                        "UI settings JSON did not produce a version-six document.");
                }

                versionSix.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
                versionSix.MainMenuCarColourIndex = 0;
                versionSix.Validate();
                migrated = true;
                return versionSix;
            }

            throw new NotSupportedException($"Unsupported UI settings schema {probe.SchemaVersion}.");
        }

        private static UiSettingsDocument MigrateVersionOne(UiSettingsDocumentV1 source)
        {
            UiSettingsDocument result = UiSettingsDefaults.Create();
            if (source.Graphics != null)
            {
                result.Graphics = source.Graphics.DeepClone();
                ApplyCameraDefaults(result.Graphics);
                ApplyAntiAliasingDefaultsFromLegacy(result.Graphics);
            }

            if (source.Audio != null)
            {
                result.Audio = source.Audio.DeepClone();
            }

            if (source.Gameplay != null)
            {
                result.Gameplay = source.Gameplay.DeepClone();
                result.Gameplay.ShowFpsCounter = true;
            }

            if (source.Controls != null)
            {
                result.Controls = new ControlsSettingsDto
                {
                    MouseSensitivity = source.Controls.MouseSensitivity,
                    InvertMouseY = source.Controls.InvertMouseY,
                    GamepadSensitivity = source.Controls.GamepadSensitivity,
                    InvertGamepadY = source.Controls.InvertGamepadY,
                    GamepadDeadzone = source.Controls.GamepadDeadzone,
                    VibrationEnabled = source.Controls.VibrationEnabled,
                    PlayerBindingOverridesJson = source.Controls.BindingOverridesJson ?? string.Empty,
                    VehicleBindingOverridesJson = string.Empty,
                };
            }

            result.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
            result.Validate();
            return result;
        }

        private static UiSettingsDocument MigrateVersionTwo(
            UiSettingsDocumentV2 source)
        {
            UiSettingsDocument result = UiSettingsDefaults.Create();
            if (source.Graphics != null)
            {
                result.Graphics = source.Graphics.DeepClone();
                ApplyCameraDefaults(result.Graphics);
                ApplyAntiAliasingDefaultsFromLegacy(result.Graphics);
            }

            if (source.Audio != null)
            {
                result.Audio = source.Audio.DeepClone();
            }

            if (source.Controls != null)
            {
                result.Controls = source.Controls.DeepClone();
            }

            if (source.Gameplay != null)
            {
                result.Gameplay = new GameplaySettingsDto
                {
                    HudMode = source.Gameplay.HudMode,
                    Units = source.Gameplay.Units,
                    LanguageId = source.Gameplay.LanguageId,
                    FatigueVisualIntensity01 =
                        source.Gameplay.FatigueVisualIntensity01,
                    AlcoholVisualIntensity01 =
                        source.Gameplay.AlcoholVisualIntensity01,
                    ContextualHints = source.Gameplay.ContextualHints,
                    InteractionOutlines =
                        source.Gameplay.InteractionOutlines,
                    CameraShakeIntensity01 =
                        source.Gameplay.CameraShakeIntensity01,
                    DevelopmentUiVisible =
                        source.Gameplay.DevelopmentUiVisible,
                    ShowFpsCounter = true,
                };
            }

            if (source.Accessibility != null)
            {
                result.Accessibility = source.Accessibility.DeepClone();
            }

            result.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
            result.Validate();
            return result;
        }

        private static void ApplyCameraDefaults(GraphicsSettingsDto graphics)
        {
            graphics.HorizontalFieldOfViewDegrees =
                GraphicsSettingsDto.DefaultHorizontalFieldOfViewDegrees;
            graphics.CameraFarClipMeters =
                GraphicsSettingsDto.DefaultCameraFarClipMeters;
        }

        private static void ApplyAntiAliasingDefaultsFromLegacy(
            GraphicsSettingsDto graphics)
        {
            if (graphics.DlssEnabled)
            {
                bool maximumQuality =
                    graphics.DlssQuality == UiDlssQuality.Dlaa;
                graphics.AntiAliasingPreset = UiAntiAliasingPreset.Custom;
                graphics.AntiAliasingMode =
                    maximumQuality
                        ? UiAntiAliasingMode.MaximumQuality
                        : UiAntiAliasingMode.TemporalUpscaler;
                graphics.DlssEnabled = !maximumQuality;
                graphics.AntiAliasingSharpening =
                    GraphicsSettingsDto.DefaultAntiAliasingSharpening;
                return;
            }

            UiSettingsDefaults.ApplyAntiAliasingPreset(
                graphics,
                UiAntiAliasingPreset.High);
        }

        [Serializable]
        private sealed class UiSettingsSchemaProbe
        {
            public int SchemaVersion;
        }

        [Serializable]
        private sealed class UiSettingsDocumentV1
        {
            public int SchemaVersion;
            public GraphicsSettingsDto Graphics;
            public AudioSettingsDto Audio;
            public ControlsSettingsV1Dto Controls;
            public GameplaySettingsDto Gameplay;
        }

        [Serializable]
        private sealed class UiSettingsDocumentV2
        {
            public int SchemaVersion;
            public GraphicsSettingsDto Graphics;
            public AudioSettingsDto Audio;
            public ControlsSettingsDto Controls;
            public GameplaySettingsV2Dto Gameplay;
            public AccessibilitySettingsDto Accessibility;
        }

        [Serializable]
        private sealed class GameplaySettingsV2Dto
        {
            public UiHudMode HudMode;
            public UiUnitSystem Units;
            public string LanguageId;
            public float FatigueVisualIntensity01;
            public float AlcoholVisualIntensity01;
            public bool ContextualHints;
            public bool InteractionOutlines;
            public float CameraShakeIntensity01;
            public bool DevelopmentUiVisible;
        }

        [Serializable]
        private sealed class ControlsSettingsV1Dto
        {
            public float MouseSensitivity;
            public bool InvertMouseY;
            public float GamepadSensitivity;
            public bool InvertGamepadY;
            public float GamepadDeadzone;
            public bool VibrationEnabled;
            public string BindingOverridesJson;
        }
    }
}
