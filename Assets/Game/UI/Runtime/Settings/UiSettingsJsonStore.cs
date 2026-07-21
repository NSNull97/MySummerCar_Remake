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

            throw new NotSupportedException($"Unsupported UI settings schema {probe.SchemaVersion}.");
        }

        private static UiSettingsDocument MigrateVersionOne(UiSettingsDocumentV1 source)
        {
            UiSettingsDocument result = UiSettingsDefaults.Create();
            if (source.Graphics != null)
            {
                result.Graphics = source.Graphics.DeepClone();
            }

            if (source.Audio != null)
            {
                result.Audio = source.Audio.DeepClone();
            }

            if (source.Gameplay != null)
            {
                result.Gameplay = source.Gameplay.DeepClone();
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
