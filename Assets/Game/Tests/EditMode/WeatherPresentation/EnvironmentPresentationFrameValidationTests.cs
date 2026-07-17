using System.Collections.Generic;
using MSC.Weather.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class EnvironmentPresentationFrameValidationTests
    {
        [TestCase("weather.clear", EnvironmentCloudType.Clear, EnvironmentPrecipitationType.None, 0f, 0f, 0.5f)]
        [TestCase("weather.overcast", EnvironmentCloudType.Overcast, EnvironmentPrecipitationType.None, 0f, 0f, 0.5f)]
        [TestCase("weather.rain", EnvironmentCloudType.Overcast, EnvironmentPrecipitationType.Rain, 0.7f, 0.2f, 0.5f)]
        [TestCase("weather.storm", EnvironmentCloudType.Storm, EnvironmentPrecipitationType.Rain, 1f, 0.5f, 0.5f)]
        [TestCase("weather.night", EnvironmentCloudType.Clear, EnvironmentPrecipitationType.None, 0f, 0f, 0.05f)]
        [TestCase("weather.mist", EnvironmentCloudType.Scattered, EnvironmentPrecipitationType.None, 0f, 1f, 0.35f)]
        public void Validate_FixedSmokeStates_Pass(
            string binding,
            EnvironmentCloudType cloudType,
            EnvironmentPrecipitationType precipitationType,
            float precipitationIntensity,
            float fogIntensity,
            float normalizedTime)
        {
            EnvironmentPresentationFrame frame = CreateValidFrame(
                bindingValue: binding,
                cloudType: cloudType,
                cloudCoverage: cloudType == EnvironmentCloudType.Clear ? 0f : 0.8f,
                cloudIntensity: cloudType == EnvironmentCloudType.Clear ? 0f : 0.9f,
                precipitationType: precipitationType,
                precipitationIntensity: precipitationIntensity,
                fogIntensity: fogIntensity,
                normalizedTime: normalizedTime);

            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        [Test]
        public void Validate_DisabledDefaultFrame_PassesWithoutFakeBinding()
        {
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(EnvironmentPresentationFrame.Disabled);

            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void Validate_DisabledFrameWithVisualRequest_Rejects()
        {
            var lightning = new EnvironmentLightningVisualRequest(
                true,
                1,
                Vector3.zero,
                1f);
            EnvironmentPresentationFrame frame = CreateValidFrame(
                enabled: false,
                lightning: lightning);

            AssertCode(frame, EnvironmentPresentationDiagnosticCodes.DisabledFrameRequest);
        }

        [Test]
        public void Validate_InvalidIdentityAndRevision_ReportStableCodes()
        {
            EnvironmentPresentationFrame frame = CreateValidFrame(
                revision: 0,
                bindingId: default(EnvironmentBindingId));
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidBindingId), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidRevision), Is.True);
        }

        [Test]
        public void Validate_InvalidDateAndTime_ReportStableCodes()
        {
            EnvironmentPresentationFrame frame = CreateValidFrame(
                year: 2025,
                month: 2,
                day: 29,
                normalizedTime: float.NaN);
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidDate), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidTime), Is.True);
        }

        [Test]
        public void Validate_InvalidCloudPrecipitationAndFog_ReportStableCodes()
        {
            EnvironmentPresentationFrame frame = CreateValidFrame(
                cloudType: (EnvironmentCloudType)999,
                cloudCoverage: 1.1f,
                precipitationType: EnvironmentPrecipitationType.None,
                precipitationIntensity: 0.5f,
                fogIntensity: float.PositiveInfinity);
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidClouds), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidPrecipitation), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidFog), Is.True);
        }

        [Test]
        public void Validate_ActiveWindRequiresNormalizedDirectionAndOrderedSpeeds()
        {
            EnvironmentPresentationFrame frame = CreateValidFrame(
                windDirection: Vector2.zero,
                windSpeed: 5f,
                windGustSpeed: 4f);

            AssertCode(frame, EnvironmentPresentationDiagnosticCodes.InvalidWind);
        }

        [Test]
        public void Validate_NonFiniteOrNegativeTransition_Rejects()
        {
            AssertCode(
                CreateValidFrame(transitionDuration: float.NaN),
                EnvironmentPresentationDiagnosticCodes.InvalidTransition);
            AssertCode(
                CreateValidFrame(transitionDuration: -0.1f),
                EnvironmentPresentationDiagnosticCodes.InvalidTransition);
        }

        [Test]
        public void Validate_UnknownQualityTier_Rejects()
        {
            AssertCode(
                CreateValidFrame(qualityTier: (EnvironmentQualityTier)999),
                EnvironmentPresentationDiagnosticCodes.InvalidQualityTier);
        }

        [Test]
        public void Validate_LightningRequestRequiresSequenceFinitePositionAndIntensity()
        {
            var lightning = new EnvironmentLightningVisualRequest(
                true,
                0,
                new Vector3(float.NaN, 0f, 0f),
                1.1f);

            AssertCode(
                CreateValidFrame(lightning: lightning),
                EnvironmentPresentationDiagnosticCodes.InvalidLightningRequest);
        }

        [Test]
        public void Validate_RefreshRequestRequiresKnownTargetsAndSequence()
        {
            var refresh = new EnvironmentRefreshRequest(
                EnvironmentRefreshTarget.Reflections | (EnvironmentRefreshTarget)(1 << 10),
                0);

            AssertCode(
                CreateValidFrame(refresh: refresh),
                EnvironmentPresentationDiagnosticCodes.InvalidRefreshRequest);
        }

        [Test]
        public void Validate_ValidEventRequests_Pass()
        {
            var lightning = new EnvironmentLightningVisualRequest(
                true,
                9,
                new Vector3(10f, 2f, -4f),
                0.8f);
            var refresh = new EnvironmentRefreshRequest(
                EnvironmentRefreshTarget.Sky | EnvironmentRefreshTarget.Reflections,
                4);
            EnvironmentPresentationFrame frame = CreateValidFrame(
                lightning: lightning,
                refresh: refresh);

            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
        }

        private static EnvironmentPresentationFrame CreateValidFrame(
            string bindingValue = "weather.clear",
            EnvironmentBindingId? bindingId = null,
            ulong revision = 1,
            bool enabled = true,
            int year = 1995,
            int month = 7,
            int day = 12,
            float normalizedTime = 0.5f,
            EnvironmentCloudType cloudType = EnvironmentCloudType.Clear,
            float cloudCoverage = 0f,
            float cloudIntensity = 0f,
            EnvironmentPrecipitationType precipitationType = EnvironmentPrecipitationType.None,
            float precipitationIntensity = 0f,
            float fogIntensity = 0f,
            Vector2? windDirection = null,
            float windSpeed = 0f,
            float windGustSpeed = 0f,
            EnvironmentLightningVisualRequest? lightning = null,
            EnvironmentRefreshRequest? refresh = null,
            EnvironmentQualityTier qualityTier = EnvironmentQualityTier.High,
            float transitionDuration = 1f)
        {
            EnvironmentBindingId resolvedBinding;
            if (bindingId.HasValue)
            {
                resolvedBinding = bindingId.Value;
            }
            else
            {
                Assert.That(EnvironmentBindingId.TryParse(bindingValue, out resolvedBinding), Is.True);
            }

            Vector2 resolvedWindDirection = windDirection ?? Vector2.right;
            return new EnvironmentPresentationFrame(
                revision,
                enabled,
                year,
                month,
                day,
                normalizedTime,
                resolvedBinding,
                cloudType,
                cloudCoverage,
                cloudIntensity,
                precipitationType,
                precipitationIntensity,
                fogIntensity,
                resolvedWindDirection,
                windSpeed,
                windGustSpeed,
                lightning ?? EnvironmentLightningVisualRequest.None,
                refresh ?? EnvironmentRefreshRequest.None,
                qualityTier,
                transitionDuration);
        }

        private static void AssertCode(EnvironmentPresentationFrame frame, string expectedCode)
        {
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.Validate(frame);

            Assert.That(HasCode(diagnostics, expectedCode), Is.True, Describe(diagnostics));
        }

        private static bool HasCode(
            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics,
            string code)
        {
            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Describe(IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                return "No diagnostics.";
            }

            string result = string.Empty;
            for (int index = 0; index < diagnostics.Count; index++)
            {
                result += (index == 0 ? string.Empty : " | ") + diagnostics[index];
            }

            return result;
        }
    }
}
