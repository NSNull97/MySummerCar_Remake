using System;
using System.IO;
using MSC.UI.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class MainMenuPreferencePersistenceTests
    {
        private string directory;
        private string settingsPath;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MSC_MainMenuPreference_Tests", Guid.NewGuid().ToString("N"));
            settingsPath = Path.Combine(directory, "ui-settings.json");
            Directory.CreateDirectory(directory);
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
        public void VersionSix_MigratesMenuPreferenceWithoutChangingExistingSettings()
        {
            UiSettingsDocument previous = UiSettingsDefaults.Create();
            previous.Graphics.HorizontalFieldOfViewDegrees = 98f;
            previous.Audio.Master01 = 0.37f;
            previous.Controls.PlayerBindingOverridesJson = "{\"bindings\":[]}";
            previous.Gameplay.LanguageId = "ru-RU";
            previous.Accessibility.UiScale = 1.2f;
            previous.SchemaVersion = 6;
            string legacyJson = JsonUtility.ToJson(previous, true)
                .Replace("    \"MainMenuCarColourIndex\": 0,\n", string.Empty);
            File.WriteAllText(settingsPath, legacyJson);

            UiSettingsLoadResult result = new UiSettingsJsonStore(settingsPath).LoadOrCreate();

            Assert.That(result.Status, Is.EqualTo(UiSettingsLoadStatus.MigratedAndSaved));
            Assert.That(result.Document.MainMenuCarColourIndex, Is.Zero);
            previous.SchemaVersion = UiSettingsDocument.CurrentSchemaVersion;
            Assert.That(result.Document.ContentEquals(previous), Is.True);
            Assert.That(new UiSettingsJsonStore(settingsPath).LoadOrCreate().Status,
                Is.EqualTo(UiSettingsLoadStatus.Loaded));
        }

        [Test]
        public void ColourPreference_RoundTripsAndRemainsIndependentFromPendingSettings()
        {
            var service = new UiSettingsTransactionService(UiSettingsDefaults.Create());
            service.EditPending(document => document.Audio.Music01 = 0.23f);
            UiSettingsDocument pending = service.Pending;
            UiSettingsDocument applied = service.Applied;
            applied.MainMenuCarColourIndex = 11;
            pending.MainMenuCarColourIndex = 11;
            var store = new UiSettingsJsonStore(settingsPath);

            store.Save(applied);
            service.ReplaceApplied(applied);
            service.ReplacePending(pending);

            UiSettingsDocument loaded = store.LoadOrCreate().Document;
            Assert.That(loaded.MainMenuCarColourIndex, Is.EqualTo(11));
            Assert.That(loaded.Audio.Music01, Is.EqualTo(UiSettingsDefaults.Create().Audio.Music01));
            Assert.That(service.Pending.Audio.Music01, Is.EqualTo(0.23f));
            Assert.That(service.HasPendingChanges, Is.True);
            service.RevertPending();
            Assert.That(service.Pending.MainMenuCarColourIndex, Is.EqualTo(11));
            Assert.That(service.Pending.ContentEquals(loaded), Is.True);
        }
    }
}
