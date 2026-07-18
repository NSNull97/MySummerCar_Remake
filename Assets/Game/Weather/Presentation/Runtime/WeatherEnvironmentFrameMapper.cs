using System;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Pure boundary from the project-owned environment read model to a vendor-neutral
    /// presentation command. It never reads presentation state back into gameplay.
    /// </summary>
    public static class WeatherEnvironmentFrameMapper
    {
        public static bool TryMap(
            in WeatherEnvironmentOutputs outputs,
            ulong presentationRevision,
            EnvironmentQualityTier qualityTier,
            float transitionDurationSeconds,
            in EnvironmentLightningVisualRequest lightningVisual,
            in EnvironmentRefreshRequest environmentRefresh,
            out EnvironmentPresentationFrame frame,
            out string failure)
        {
            frame = default;
            if (presentationRevision == 0UL)
            {
                failure = "Presentation revision must be non-zero.";
                return false;
            }

            WeatherState weather = outputs.Weather;
            if (weather.Id.IsEmpty ||
                !EnvironmentBindingId.TryParse(
                    weather.PresentationBindingId,
                    out EnvironmentBindingId bindingId))
            {
                failure = "Weather output has no valid stable presentation binding ID.";
                return false;
            }

            float normalizedTime = outputs.Clock.NormalizedDayTime01;
            if (!float.IsFinite(normalizedTime) || normalizedTime < 0f || normalizedTime >= 1f)
            {
                failure = "Weather clock output must be finite and normalized to [0, 1).";
                return false;
            }

            if (!float.IsFinite(transitionDurationSeconds) || transitionDurationSeconds < 0f)
            {
                failure = "Presentation transition duration must be finite and non-negative.";
                return false;
            }

            float directionRadians = weather.WindDirectionDegrees * Mathf.Deg2Rad;
            Vector2 windDirection = weather.WindSpeedMetersPerSecond > 0f
                ? new Vector2(Mathf.Sin(directionRadians), Mathf.Cos(directionRadians)).normalized
                : Vector2.zero;
            float gustSpeed = weather.WindSpeedMetersPerSecond * (1f + weather.WindGust01);

            frame = new EnvironmentPresentationFrame(
                presentationRevision,
                enabled: true,
                outputs.Clock.Year,
                outputs.Clock.Month,
                outputs.Clock.Day,
                normalizedTime,
                bindingId,
                MapCloudType(weather.Id),
                weather.CloudCoverage01,
                weather.AmbientReadability01,
                weather.PrecipitationType == WeatherPrecipitationType.None
                    ? EnvironmentPrecipitationType.None
                    : EnvironmentPrecipitationType.Rain,
                weather.PrecipitationIntensity01,
                weather.FogIntensity01,
                weather.VisibilityMeters,
                outputs.ExposureContext,
                windDirection,
                weather.WindSpeedMetersPerSecond,
                gustSpeed,
                lightningVisual,
                environmentRefresh,
                qualityTier,
                transitionDurationSeconds);

            var diagnostics = EnvironmentPresentationValidator.Validate(frame);
            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].Severity == EnvironmentDiagnosticSeverity.Error)
                {
                    frame = default;
                    failure = diagnostics[index].Message;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static EnvironmentCloudType MapCloudType(WeatherStateId stateId)
        {
            if (stateId == WeatherStateIds.Clear)
            {
                return EnvironmentCloudType.Clear;
            }

            if (stateId == WeatherStateIds.PartlyCloudy ||
                stateId == WeatherStateIds.MorningMist)
            {
                return EnvironmentCloudType.Scattered;
            }

            if (stateId == WeatherStateIds.Thunderstorm)
            {
                return EnvironmentCloudType.Storm;
            }

            return EnvironmentCloudType.Overcast;
        }
    }
}
