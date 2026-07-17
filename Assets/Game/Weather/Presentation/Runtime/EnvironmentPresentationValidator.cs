using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Pure validation for authoring, tests, and revision-bound adapter updates.
    /// It is not intended to allocate or run on every rendered frame.
    /// </summary>
    public static class EnvironmentPresentationValidator
    {
        private const float DirectionLengthTolerance = 0.02f;

        private const EnvironmentPresentationCapabilities AllCapabilities =
            EnvironmentPresentationCapabilities.TimeOfDay |
            EnvironmentPresentationCapabilities.Sky |
            EnvironmentPresentationCapabilities.SunMoonLighting |
            EnvironmentPresentationCapabilities.Clouds |
            EnvironmentPresentationCapabilities.Precipitation |
            EnvironmentPresentationCapabilities.Fog |
            EnvironmentPresentationCapabilities.Wind |
            EnvironmentPresentationCapabilities.LightningVisual |
            EnvironmentPresentationCapabilities.EnvironmentRefresh |
            EnvironmentPresentationCapabilities.QualityTiers;

        private const EnvironmentRefreshTarget AllRefreshTargets =
            EnvironmentRefreshTarget.Sky |
            EnvironmentRefreshTarget.Ambient |
            EnvironmentRefreshTarget.Reflections;

        public static IReadOnlyList<EnvironmentPresentationDiagnostic> Validate(
            in EnvironmentPresentationFrame frame)
        {
            var diagnostics = new List<EnvironmentPresentationDiagnostic>();

            if (!frame.Enabled)
            {
                if (frame.LightningVisual.IsRequested ||
                    frame.EnvironmentRefresh.Targets != EnvironmentRefreshTarget.None)
                {
                    AddError(
                        diagnostics,
                        EnvironmentPresentationDiagnosticCodes.DisabledFrameRequest,
                        "A disabled presentation frame cannot issue lightning or environment refresh requests.",
                        frame.BindingId);
                }

                return diagnostics;
            }

            if (!frame.BindingId.IsValid)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidBindingId,
                    "An enabled presentation frame requires a valid stable binding ID.");
            }

            if (frame.Revision == 0)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidRevision,
                    "An enabled presentation frame requires a non-zero revision.",
                    frame.BindingId);
            }

            if (!IsValidDate(frame.Year, frame.Month, frame.Day))
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidDate,
                    "The presentation date is outside the supported Gregorian calendar range.",
                    frame.BindingId);
            }

            if (!IsFinite(frame.NormalizedTimeOfDay01) ||
                frame.NormalizedTimeOfDay01 < 0f ||
                frame.NormalizedTimeOfDay01 >= 1f)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidTime,
                    "Normalized time of day must be finite and in the range [0, 1).",
                    frame.BindingId);
            }

            if (!Enum.IsDefined(typeof(EnvironmentCloudType), frame.CloudType) ||
                !IsNormalized(frame.CloudCoverage01) ||
                !IsNormalized(frame.CloudIntensity01))
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidClouds,
                    "Cloud type must be known and cloud targets must be finite normalized values.",
                    frame.BindingId);
            }

            if (!Enum.IsDefined(typeof(EnvironmentPrecipitationType), frame.PrecipitationType) ||
                !IsNormalized(frame.PrecipitationIntensity01) ||
                frame.PrecipitationType == EnvironmentPrecipitationType.None &&
                frame.PrecipitationIntensity01 > 0f)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidPrecipitation,
                    "Precipitation type and normalized intensity are inconsistent.",
                    frame.BindingId);
            }

            if (!IsNormalized(frame.FogMistIntensity01))
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidFog,
                    "Fog or mist target must be a finite normalized value.",
                    frame.BindingId);
            }

            ValidateWind(frame, diagnostics);

            if (!IsFinite(frame.TransitionDurationSeconds) || frame.TransitionDurationSeconds < 0f)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidTransition,
                    "Transition duration must be finite and non-negative.",
                    frame.BindingId);
            }

            if (!Enum.IsDefined(typeof(EnvironmentQualityTier), frame.QualityTier))
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidQualityTier,
                    "The requested environment quality tier is unknown.",
                    frame.BindingId);
            }

            ValidateLightning(frame, diagnostics);
            ValidateRefresh(frame, diagnostics);
            return diagnostics;
        }

        public static IReadOnlyList<EnvironmentPresentationDiagnostic> ValidateBindingDefinitions(
            IEnumerable<EnvironmentPresentationBindingDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var diagnostics = new List<EnvironmentPresentationDiagnostic>();
            var seenIds = new HashSet<EnvironmentBindingId>();

            foreach (EnvironmentPresentationBindingDefinition definition in definitions)
            {
                if (definition == null || !definition.TryGetBindingId(out EnvironmentBindingId bindingId))
                {
                    AddError(
                        diagnostics,
                        EnvironmentPresentationDiagnosticCodes.InvalidBindingDefinition,
                        "Every presentation binding definition requires a valid stable ID.");
                    continue;
                }

                if (!seenIds.Add(bindingId))
                {
                    AddError(
                        diagnostics,
                        EnvironmentPresentationDiagnosticCodes.DuplicateBindingDefinition,
                        "Presentation binding IDs must be unique.",
                        bindingId);
                }

                if (!Enum.IsDefined(typeof(EnvironmentPresentationPresetKind), definition.PresetKind))
                {
                    AddError(
                        diagnostics,
                        EnvironmentPresentationDiagnosticCodes.InvalidPresetKind,
                        "The presentation preset kind is unknown.",
                        bindingId);
                }

                if ((definition.RequiredCapabilities & ~AllCapabilities) != 0)
                {
                    AddError(
                        diagnostics,
                        EnvironmentPresentationDiagnosticCodes.InvalidCapabilities,
                        "Required capabilities contain unknown flags.",
                        bindingId);
                }
            }

            return diagnostics;
        }

        private static void ValidateWind(
            in EnvironmentPresentationFrame frame,
            ICollection<EnvironmentPresentationDiagnostic> diagnostics)
        {
            bool finite = IsFinite(frame.WindDirectionXZ.x) &&
                          IsFinite(frame.WindDirectionXZ.y) &&
                          IsFinite(frame.WindSpeedMetersPerSecond) &&
                          IsFinite(frame.WindGustSpeedMetersPerSecond);
            bool speedsValid = frame.WindSpeedMetersPerSecond >= 0f &&
                               frame.WindGustSpeedMetersPerSecond >= frame.WindSpeedMetersPerSecond;
            bool directionValid = true;

            if (finite && frame.WindGustSpeedMetersPerSecond > 0f)
            {
                directionValid = Math.Abs(frame.WindDirectionXZ.sqrMagnitude - 1f) <=
                                 DirectionLengthTolerance;
            }

            if (!finite || !speedsValid || !directionValid)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidWind,
                    "Wind values must be finite; gust speed must be at least base speed, and active wind requires a normalized X/Z direction.",
                    frame.BindingId);
            }
        }

        private static void ValidateLightning(
            in EnvironmentPresentationFrame frame,
            ICollection<EnvironmentPresentationDiagnostic> diagnostics)
        {
            EnvironmentLightningVisualRequest request = frame.LightningVisual;
            if (!request.IsRequested)
            {
                return;
            }

            if (request.Sequence == 0 ||
                !IsFinite(request.WorldPosition.x) ||
                !IsFinite(request.WorldPosition.y) ||
                !IsFinite(request.WorldPosition.z) ||
                !IsNormalized(request.Intensity01))
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidLightningRequest,
                    "A lightning visual request requires a non-zero sequence, finite position, and normalized intensity.",
                    frame.BindingId);
            }
        }

        private static void ValidateRefresh(
            in EnvironmentPresentationFrame frame,
            ICollection<EnvironmentPresentationDiagnostic> diagnostics)
        {
            EnvironmentRefreshRequest request = frame.EnvironmentRefresh;
            bool containsUnknownTarget = (request.Targets & ~AllRefreshTargets) != 0;
            bool missingSequence = request.Targets != EnvironmentRefreshTarget.None && request.Sequence == 0;
            if (containsUnknownTarget || missingSequence)
            {
                AddError(
                    diagnostics,
                    EnvironmentPresentationDiagnosticCodes.InvalidRefreshRequest,
                    "An environment refresh request requires known targets and a non-zero sequence.",
                    frame.BindingId);
            }
        }

        private static bool IsValidDate(int year, int month, int day)
        {
            if (year < 1 || year > 9999 || month < 1 || month > 12)
            {
                return false;
            }

            return day >= 1 && day <= DateTime.DaysInMonth(year, month);
        }

        private static bool IsNormalized(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void AddError(
            ICollection<EnvironmentPresentationDiagnostic> diagnostics,
            string code,
            string message,
            EnvironmentBindingId bindingId = default)
        {
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                code,
                message,
                bindingId));
        }
    }
}
