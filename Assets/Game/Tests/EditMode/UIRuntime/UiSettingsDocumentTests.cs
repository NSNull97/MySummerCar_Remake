using System;
using MSC.UI.Runtime.Settings;
using NUnit.Framework;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class UiSettingsDocumentTests
    {
        [Test]
        public void Defaults_AreCurrentIndependentAndValid()
        {
            UiSettingsDocument first = UiSettingsDefaults.Create();
            UiSettingsDocument second = UiSettingsDefaults.Create();

            Assert.DoesNotThrow(first.Validate);
            Assert.That(first.SchemaVersion, Is.EqualTo(UiSettingsDocument.CurrentSchemaVersion));
            Assert.That(first.Gameplay.ShowFpsCounter, Is.True);
            Assert.That(first.Graphics.DlssEnabled, Is.False);
            Assert.That(first.Graphics.DlssQuality, Is.EqualTo(UiDlssQuality.Balanced));
            Assert.That(
                first.Graphics.AntiAliasingMode,
                Is.EqualTo(UiAntiAliasingMode.Taa));
            Assert.That(
                first.Graphics.AntiAliasingPreset,
                Is.EqualTo(UiAntiAliasingPreset.High));
            Assert.That(
                first.Graphics.AntiAliasingSharpening,
                Is.EqualTo(
                    GraphicsSettingsDto.DefaultAntiAliasingSharpening));
            Assert.That(
                first.Graphics.HorizontalFieldOfViewDegrees,
                Is.EqualTo(
                    GraphicsSettingsDto.DefaultHorizontalFieldOfViewDegrees));
            Assert.That(
                first.Graphics.CameraFarClipMeters,
                Is.EqualTo(GraphicsSettingsDto.DefaultCameraFarClipMeters));
            Assert.That(first.ContentEquals(second), Is.True);

            first.Audio.Master01 = 0.25f;
            first.Controls.PlayerBindingOverridesJson = "[]";

            Assert.That(second.Audio.Master01, Is.EqualTo(1f));
            Assert.That(second.Controls.PlayerBindingOverridesJson, Is.Empty);
            Assert.That(first.ContentEquals(second), Is.False);
        }

        [Test]
        public void DeepClone_CopiesEveryCategoryAndOpaqueBindingPayload()
        {
            UiSettingsDocument source = UiSettingsDefaults.Create();
            source.Controls.PlayerBindingOverridesJson = "[{\"id\":\"player\"}]";
            source.Controls.VehicleBindingOverridesJson = "[{\"id\":\"vehicle\"}]";
            source.Accessibility.HighContrast = true;

            UiSettingsDocument clone = source.DeepClone();

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.Graphics, Is.Not.SameAs(source.Graphics));
            Assert.That(clone.Audio, Is.Not.SameAs(source.Audio));
            Assert.That(clone.Controls, Is.Not.SameAs(source.Controls));
            Assert.That(clone.Gameplay, Is.Not.SameAs(source.Gameplay));
            Assert.That(clone.Accessibility, Is.Not.SameAs(source.Accessibility));
            Assert.That(clone.ContentEquals(source), Is.True);

            clone.Controls.PlayerBindingOverridesJson = string.Empty;
            Assert.That(source.Controls.PlayerBindingOverridesJson, Is.Not.Empty);
        }

        [Test]
        public void Validate_RejectsNonFiniteOutOfRangeAndMalformedOpaquePayloads()
        {
            UiSettingsDocument invalidVolume = UiSettingsDefaults.Create();
            invalidVolume.Audio.Master01 = float.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(invalidVolume.Validate);

            UiSettingsDocument invalidScale = UiSettingsDefaults.Create();
            invalidScale.Accessibility.UiScale = 2f;
            Assert.Throws<ArgumentOutOfRangeException>(invalidScale.Validate);

            UiSettingsDocument invalidBinding = UiSettingsDefaults.Create();
            invalidBinding.Controls.VehicleBindingOverridesJson = "not-json-shaped";
            Assert.Throws<ArgumentException>(invalidBinding.Validate);

            UiSettingsDocument unsupportedLanguage = UiSettingsDefaults.Create();
            unsupportedLanguage.Gameplay.LanguageId = "not-a-culture";
            Assert.Throws<ArgumentException>(unsupportedLanguage.Validate);

            UiSettingsDocument invalidDlssQuality = UiSettingsDefaults.Create();
            invalidDlssQuality.Graphics.DlssQuality = (UiDlssQuality)999;
            Assert.Throws<ArgumentOutOfRangeException>(
                invalidDlssQuality.Validate);

            UiSettingsDocument invalidAntiAliasingMode =
                UiSettingsDefaults.Create();
            invalidAntiAliasingMode.Graphics.AntiAliasingMode =
                (UiAntiAliasingMode)999;
            Assert.Throws<ArgumentOutOfRangeException>(
                invalidAntiAliasingMode.Validate);

            UiSettingsDocument invalidAntiAliasingPreset =
                UiSettingsDefaults.Create();
            invalidAntiAliasingPreset.Graphics.AntiAliasingPreset =
                (UiAntiAliasingPreset)999;
            Assert.Throws<ArgumentOutOfRangeException>(
                invalidAntiAliasingPreset.Validate);

            UiSettingsDocument invalidAntiAliasingSharpening =
                UiSettingsDefaults.Create();
            invalidAntiAliasingSharpening.Graphics.AntiAliasingSharpening = 1f;
            Assert.Throws<ArgumentOutOfRangeException>(
                invalidAntiAliasingSharpening.Validate);

            UiSettingsDocument invalidFieldOfView = UiSettingsDefaults.Create();
            invalidFieldOfView.Graphics.HorizontalFieldOfViewDegrees = 179f;
            Assert.Throws<ArgumentOutOfRangeException>(
                invalidFieldOfView.Validate);

            UiSettingsDocument invalidFarClip = UiSettingsDefaults.Create();
            invalidFarClip.Graphics.CameraFarClipMeters = float.PositiveInfinity;
            Assert.Throws<ArgumentOutOfRangeException>(invalidFarClip.Validate);
        }
    }
}
