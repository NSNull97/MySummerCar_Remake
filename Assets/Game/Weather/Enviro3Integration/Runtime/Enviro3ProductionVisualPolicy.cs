using System;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration
{
    /// <summary>
    /// Project-owned production tuning at the Enviro/HDRP boundary. Gameplay sends
    /// physical visibility and an exposure context; this policy converts them to
    /// the installed backend's native values without mutating vendor presets.
    /// </summary>
    public static class Enviro3ProductionVisualPolicy
    {
        public const float MinimumExposureEv = 0f;
        public const float MaximumExposureEv = 14f;
        public const float ExposureAdaptationSeconds = 0.5f;
        public const float DaylightExposureBiasEv = 0.25f;
        public const float MinimumNightExposureEv = 7.5f;
        public const float FullNightSolarTime = 0.43f;
        public const float DaylightSolarTime = 0.5f;

        // Art-directed solar calibration for the approved summer clock. These
        // values target the visible cycle and are not an in-world GPS location.
        public const float ProductionLatitudeDegrees = 60f;
        public const float ProductionLongitudeDegrees = 27.3f;
        public const int ProductionUtcOffsetHours = 3;
        public const float TemporaryBaselineReflectionIntensity = 0.6f;
        public const float MinimumFogMeanFreePathMeters = 300f;
        public const float MaximumFogMeanFreePathMeters = 6000f;
        public const float FogBaseHeightMeters = 0f;
        public const float FogMaximumHeightMeters = 50f;
        public const float VolumetricLightDimmer = 1f;
        public const float MinimumReadableRainParticleScreenSize = 0.01f;

        // Koschmieder's law at the conventional 2% contrast threshold.
        private const float VisibilityExtinctionCoefficient = 3.912f;

        public static float CalculateExposureEv(
            AnimationCurve sceneExposure,
            float solarTime,
            WeatherExposureContext context)
        {
            if (sceneExposure == null)
            {
                throw new ArgumentNullException(nameof(sceneExposure));
            }

            if (!float.IsFinite(solarTime))
            {
                throw new ArgumentOutOfRangeException(nameof(solarTime));
            }

            float baseExposure = sceneExposure.Evaluate(solarTime);
            if (!float.IsFinite(baseExposure))
            {
                throw new InvalidOperationException(
                    "The production exposure curve returned a non-finite value.");
            }

            float calibratedExposure =
                baseExposure + DaylightExposureBiasEv;

            // The authored Enviro curve drops by more than one EV when the
            // art-directed solar latitude is lowered to obtain the approved
            // 05:00 dawn. Preserve a readable but restrained night by blending
            // toward a project-owned minimum Fixed Exposure value. Higher HDRP
            // Fixed Exposure EV values darken the image; daylight is unchanged.
            float daylightBlend = Mathf.InverseLerp(
                FullNightSolarTime,
                DaylightSolarTime,
                solarTime);
            float nightWeight =
                1f - Mathf.SmoothStep(0f, 1f, daylightBlend);
            float nightFloor = Mathf.Max(
                calibratedExposure,
                MinimumNightExposureEv);
            calibratedExposure = Mathf.Lerp(
                calibratedExposure,
                nightFloor,
                nightWeight);

            return Mathf.Clamp(
                calibratedExposure + GetExposureOffsetEv(context),
                MinimumExposureEv,
                MaximumExposureEv);
        }

        public static float CalculateFogMeanFreePathMeters(
            float visibilityMeters)
        {
            if (!float.IsFinite(visibilityMeters) || visibilityMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibilityMeters));
            }

            return Mathf.Clamp(
                visibilityMeters / VisibilityExtinctionCoefficient,
                MinimumFogMeanFreePathMeters,
                MaximumFogMeanFreePathMeters);
        }

        public static float GetExposureOffsetEv(
            WeatherExposureContext context)
        {
            switch (context)
            {
                case WeatherExposureContext.Exterior:
                    return 0f;
                case WeatherExposureContext.Sheltered:
                    return -0.15f;
                case WeatherExposureContext.Interior:
                    return -0.25f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context));
            }
        }

        public static float ResolveRainParticleMaxScreenSize(
            float authoredMaximum)
        {
            if (!float.IsFinite(authoredMaximum) || authoredMaximum < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredMaximum));
            }

            return Mathf.Max(
                authoredMaximum,
                MinimumReadableRainParticleScreenSize);
        }
    }
}
