using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Immutable project-owned presentation command. Values are intentionally not
    /// clamped by the constructor; invalid commands are rejected by the validator.
    /// </summary>
    public readonly struct EnvironmentPresentationFrame
    {
        public EnvironmentPresentationFrame(
            ulong revision,
            bool enabled,
            int year,
            int month,
            int day,
            float normalizedTimeOfDay01,
            EnvironmentBindingId bindingId,
            EnvironmentCloudType cloudType,
            float cloudCoverage01,
            float cloudIntensity01,
            EnvironmentPrecipitationType precipitationType,
            float precipitationIntensity01,
            float fogMistIntensity01,
            float visibilityMeters,
            WeatherExposureContext exposureContext,
            Vector2 windDirectionXZ,
            float windSpeedMetersPerSecond,
            float windGustSpeedMetersPerSecond,
            EnvironmentLightningVisualRequest lightningVisual,
            EnvironmentRefreshRequest environmentRefresh,
            EnvironmentQualityTier qualityTier,
            float transitionDurationSeconds)
        {
            Revision = revision;
            Enabled = enabled;
            Year = year;
            Month = month;
            Day = day;
            NormalizedTimeOfDay01 = normalizedTimeOfDay01;
            BindingId = bindingId;
            CloudType = cloudType;
            CloudCoverage01 = cloudCoverage01;
            CloudIntensity01 = cloudIntensity01;
            PrecipitationType = precipitationType;
            PrecipitationIntensity01 = precipitationIntensity01;
            FogMistIntensity01 = fogMistIntensity01;
            VisibilityMeters = visibilityMeters;
            ExposureContext = exposureContext;
            WindDirectionXZ = windDirectionXZ;
            WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
            WindGustSpeedMetersPerSecond = windGustSpeedMetersPerSecond;
            LightningVisual = lightningVisual;
            EnvironmentRefresh = environmentRefresh;
            QualityTier = qualityTier;
            TransitionDurationSeconds = transitionDurationSeconds;
        }

        public ulong Revision { get; }

        public bool Enabled { get; }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        public float NormalizedTimeOfDay01 { get; }

        public EnvironmentBindingId BindingId { get; }

        public EnvironmentCloudType CloudType { get; }

        public float CloudCoverage01 { get; }

        /// <summary>
        /// Optical cloud density where zero is transparent/readable sky and one
        /// is the strongest authored light-blocking cloud state.
        /// </summary>
        public float CloudIntensity01 { get; }

        public EnvironmentPrecipitationType PrecipitationType { get; }

        public float PrecipitationIntensity01 { get; }

        public float FogMistIntensity01 { get; }

        /// <summary>
        /// Project-domain meteorological visibility target in metres. Presentation
        /// backends convert this value to their native fog-density representation.
        /// </summary>
        public float VisibilityMeters { get; }

        public WeatherExposureContext ExposureContext { get; }

        /// <summary>Normalized horizontal direction in project X/Z coordinates.</summary>
        public Vector2 WindDirectionXZ { get; }

        public float WindSpeedMetersPerSecond { get; }

        public float WindGustSpeedMetersPerSecond { get; }

        public EnvironmentLightningVisualRequest LightningVisual { get; }

        public EnvironmentRefreshRequest EnvironmentRefresh { get; }

        public EnvironmentQualityTier QualityTier { get; }

        public float TransitionDurationSeconds { get; }

        public static EnvironmentPresentationFrame Disabled => default;
    }
}
