using System;
using System.IO;
using System.Text;
using MSC.UI.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class UiSettingsJsonStoreTests
    {
        private string directory;
        private string settingsPath;

        [SetUp]
        public void SetUp()
        {
            directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MSC_UI_Settings_Tests",
                Guid.NewGuid().ToString("N"));
            settingsPath = System.IO.Path.Combine(directory, "ui-settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingFile_CreatesValidatedDefaults()
        {
            UiSettingsJsonStore store = new UiSettingsJsonStore(settingsPath);

            UiSettingsLoadResult result = store.LoadOrCreate();

            Assert.That(result.Status, Is.EqualTo(UiSettingsLoadStatus.MissingCreatedDefaults));
            Assert.That(File.Exists(settingsPath), Is.True);
            Assert.DoesNotThrow(result.Document.Validate);
            Assert.That(File.Exists(settingsPath + ".tmp"), Is.False);
        }

        [Test]
        public void SaveAndLoad_RoundTripsBothBindingOverridePayloads()
        {
            UiSettingsJsonStore store = new UiSettingsJsonStore(settingsPath);
            UiSettingsDocument document = UiSettingsDefaults.Create();
            document.Audio.Engine01 = 0.42f;
            document.Controls.PlayerBindingOverridesJson = "[{\"id\":\"player\"}]";
            document.Controls.VehicleBindingOverridesJson = "[{\"id\":\"vehicle\"}]";

            store.Save(document);
            UiSettingsLoadResult result = store.LoadOrCreate();

            Assert.That(result.Status, Is.EqualTo(UiSettingsLoadStatus.Loaded));
            Assert.That(result.Document.ContentEquals(document), Is.True);
            Assert.That(File.Exists(settingsPath + ".tmp"), Is.False);
        }

        [Test]
        public void CorruptFile_IsQuarantinedAndReplacedWithDefaults()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(settingsPath, "{ definitely-not-json", new UTF8Encoding(false));
            UiSettingsJsonStore store = new UiSettingsJsonStore(settingsPath);

            UiSettingsLoadResult result = store.LoadOrCreate();

            Assert.That(result.Status, Is.EqualTo(UiSettingsLoadStatus.CorruptQuarantinedDefaultsCreated));
            Assert.That(result.QuarantinedPath, Is.Not.Empty);
            Assert.That(File.Exists(result.QuarantinedPath), Is.True);
            Assert.That(File.ReadAllText(result.QuarantinedPath), Does.Contain("definitely-not-json"));
            Assert.That(File.Exists(settingsPath), Is.True);
            Assert.That(result.Document.ContentEquals(UiSettingsDefaults.Create()), Is.True);
        }

        [Test]
        public void VersionOne_MigratesSingleBindingPayloadAndAddsAccessibilityDefaults()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(settingsPath, CreateVersionOneJson(), new UTF8Encoding(false));
            UiSettingsJsonStore store = new UiSettingsJsonStore(settingsPath);

            UiSettingsLoadResult result = store.LoadOrCreate();

            Assert.That(result.Status, Is.EqualTo(UiSettingsLoadStatus.MigratedAndSaved));
            Assert.That(result.Document.SchemaVersion, Is.EqualTo(UiSettingsDocument.CurrentSchemaVersion));
            Assert.That(result.Document.Controls.PlayerBindingOverridesJson, Is.EqualTo("[]"));
            Assert.That(result.Document.Controls.VehicleBindingOverridesJson, Is.Empty);
            Assert.That(result.Document.Accessibility.ContentEquals(UiSettingsDefaults.Create().Accessibility), Is.True);

            UiSettingsDocument persisted = JsonUtility.FromJson<UiSettingsDocument>(File.ReadAllText(settingsPath));
            Assert.That(persisted.SchemaVersion, Is.EqualTo(UiSettingsDocument.CurrentSchemaVersion));
        }

        private static string CreateVersionOneJson()
        {
            return @"{
  ""SchemaVersion"": 1,
  ""Graphics"": {
    ""DisplayMode"": 1,
    ""ResolutionWidth"": 1920,
    ""ResolutionHeight"": 1080,
    ""RefreshRateNumerator"": 60,
    ""RefreshRateDenominator"": 1,
    ""VSync"": true,
    ""QualityLevel"": 2,
    ""MotionBlur"": false,
    ""DepthOfField"": false
  },
  ""Audio"": {
    ""Master01"": 1.0,
    ""Engine01"": 0.9,
    ""Environment01"": 0.8,
    ""Effects01"": 0.7,
    ""Music01"": 0.6,
    ""Ui01"": 0.5,
    ""DynamicRange"": 1,
    ""MuteWhenUnfocused"": false,
    ""SubtitlesEnabled"": true,
    ""CaptionsEnabled"": false,
    ""ReducedLoudSounds"": false,
    ""OutputDeviceId"": ""system.default""
  },
  ""Controls"": {
    ""MouseSensitivity"": 1.0,
    ""InvertMouseY"": false,
    ""GamepadSensitivity"": 1.0,
    ""InvertGamepadY"": false,
    ""GamepadDeadzone"": 0.125,
    ""VibrationEnabled"": true,
    ""BindingOverridesJson"": ""[]""
  },
  ""Gameplay"": {
    ""HudMode"": 2,
    ""Units"": 0,
    ""LanguageId"": ""ru-RU"",
    ""FatigueVisualIntensity01"": 1.0,
    ""AlcoholVisualIntensity01"": 1.0,
    ""ContextualHints"": true,
    ""InteractionOutlines"": true,
    ""CameraShakeIntensity01"": 0.75,
    ""DevelopmentUiVisible"": false
  }
}";
        }
    }
}
