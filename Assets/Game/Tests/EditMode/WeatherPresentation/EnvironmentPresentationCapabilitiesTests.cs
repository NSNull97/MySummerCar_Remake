using System.Collections.Generic;
using MSC.Weather.Presentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class EnvironmentPresentationCapabilitiesTests
    {
        [Test]
        public void CapabilityFlags_ComposeWithoutVendorTypes()
        {
            EnvironmentPresentationCapabilities capabilities =
                EnvironmentPresentationCapabilities.TimeOfDay |
                EnvironmentPresentationCapabilities.Clouds |
                EnvironmentPresentationCapabilities.Fog;

            Assert.That((capabilities & EnvironmentPresentationCapabilities.TimeOfDay) != 0, Is.True);
            Assert.That((capabilities & EnvironmentPresentationCapabilities.Clouds) != 0, Is.True);
            Assert.That((capabilities & EnvironmentPresentationCapabilities.Fog) != 0, Is.True);
            Assert.That((capabilities & EnvironmentPresentationCapabilities.Precipitation) != 0, Is.False);
        }

        [TestCase(EnvironmentPresentationState.Ready, 0, true)]
        [TestCase(EnvironmentPresentationState.Degraded, 0, true)]
        [TestCase(EnvironmentPresentationState.Detached, 0, false)]
        [TestCase(EnvironmentPresentationState.Disabled, 0, false)]
        [TestCase(EnvironmentPresentationState.Faulted, 1, false)]
        [TestCase(EnvironmentPresentationState.Ready, 1, false)]
        public void Status_IsOperational_RequiresReadyOrDegradedWithoutErrors(
            EnvironmentPresentationState state,
            int errorCount,
            bool expected)
        {
            var status = new EnvironmentPresentationStatus(
                state,
                EnvironmentPresentationCapabilities.Sky,
                4,
                warningCount: 1,
                errorCount: errorCount);

            Assert.That(status.IsOperational, Is.EqualTo(expected));
            Assert.That(status.WarningCount, Is.EqualTo(1));
            Assert.That(status.ErrorCount, Is.EqualTo(errorCount));
        }

        [Test]
        public void BindingDefinitions_UniqueKnownDefinitions_Pass()
        {
            var definitions = new[]
            {
                Definition("weather.clear", EnvironmentPresentationPresetKind.Clear),
                Definition("weather.overcast", EnvironmentPresentationPresetKind.Overcast),
                Definition("weather.rain", EnvironmentPresentationPresetKind.Rain),
                Definition("weather.storm", EnvironmentPresentationPresetKind.Storm),
                Definition("weather.night", EnvironmentPresentationPresetKind.Night),
                Definition("weather.mist", EnvironmentPresentationPresetKind.Mist)
            };

            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.ValidateBindingDefinitions(definitions);

            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void BindingDefinitions_DuplicateInvalidAndUnknownValues_ReportStableCodes()
        {
            var definitions = new EnvironmentPresentationBindingDefinition[]
            {
                Definition("weather.clear", EnvironmentPresentationPresetKind.Clear),
                Definition("weather.clear", EnvironmentPresentationPresetKind.Clear),
                new EnvironmentPresentationBindingDefinition(
                    "INVALID DISPLAY NAME",
                    (EnvironmentPresentationPresetKind)999,
                    (EnvironmentPresentationCapabilities)(1 << 20)),
                null
            };

            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.ValidateBindingDefinitions(definitions);

            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.DuplicateBindingDefinition), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidBindingDefinition), Is.True);
        }

        [Test]
        public void BindingDefinitions_KnownIdWithUnknownPresetAndCapabilities_ReportsBothCodes()
        {
            var definitions = new[]
            {
                new EnvironmentPresentationBindingDefinition(
                    "weather.clear",
                    (EnvironmentPresentationPresetKind)999,
                    (EnvironmentPresentationCapabilities)(1 << 20))
            };

            IReadOnlyList<EnvironmentPresentationDiagnostic> diagnostics =
                EnvironmentPresentationValidator.ValidateBindingDefinitions(definitions);

            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidPresetKind), Is.True);
            Assert.That(HasCode(diagnostics, EnvironmentPresentationDiagnosticCodes.InvalidCapabilities), Is.True);
        }

        private static EnvironmentPresentationBindingDefinition Definition(
            string id,
            EnvironmentPresentationPresetKind kind)
        {
            return new EnvironmentPresentationBindingDefinition(
                id,
                kind,
                EnvironmentPresentationCapabilities.TimeOfDay |
                EnvironmentPresentationCapabilities.Sky);
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
    }
}
