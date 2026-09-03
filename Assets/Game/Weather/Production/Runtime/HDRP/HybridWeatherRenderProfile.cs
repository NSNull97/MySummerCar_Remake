using System;
using UnityEngine;

namespace MSC.Weather.Production
{
    [Serializable]
    public struct HybridWeatherRenderSettings
    {
        [SerializeField] private string weatherId;
        [SerializeField] private Color fogTint;
        [SerializeField, Min(0f)] private float baseHeightMeters;
        [SerializeField, Min(1f)] private float maximumHeightMeters;
        [SerializeField, Min(1f)] private float maximumFogDistanceMeters;
        [SerializeField, Range(-1f, 1f)] private float anisotropy;
        [SerializeField] private Color singleScatteringAlbedo;
        [SerializeField, Range(-2f, 2f)] private float exposureCompensationEv;
        [SerializeField, Range(0f, 1f)] private float volumetricLightDimmer;
        [SerializeField] private Color daytimeColorFilter;
        [SerializeField, Range(-100f, 100f)] private float daytimeTemperature;
        [SerializeField, Range(-100f, 100f)] private float daytimeTint;
        [SerializeField, Range(-100f, 100f)] private float daytimeContrast;
        [SerializeField, Range(-100f, 100f)] private float daytimeSaturation;

        public string WeatherId => weatherId;
        public Color FogTint => fogTint;
        public float BaseHeightMeters => baseHeightMeters;
        public float MaximumHeightMeters => maximumHeightMeters;
        public float MaximumFogDistanceMeters => maximumFogDistanceMeters;
        public float Anisotropy => anisotropy;
        public Color SingleScatteringAlbedo => singleScatteringAlbedo;
        public float ExposureCompensationEv => exposureCompensationEv;
        public float VolumetricLightDimmer => volumetricLightDimmer;
        public Color DaytimeColorFilter => daytimeColorFilter;
        public float DaytimeTemperature => daytimeTemperature;
        public float DaytimeTint => daytimeTint;
        public float DaytimeContrast => daytimeContrast;
        public float DaytimeSaturation => daytimeSaturation;

        public static HybridWeatherRenderSettings Default => Create(
            "weather.clear",
            Color.white,
            0f,
            80f,
            6000f,
            0f,
            Color.white,
            0f,
            1f,
            new Color(0.93f, 0.97f, 1f, 1f),
            -10f,
            -2f,
            -4f,
            -10f);

        public static HybridWeatherRenderSettings Create(
            string weatherId,
            Color fogTint,
            float baseHeightMeters,
            float maximumHeightMeters,
            float maximumFogDistanceMeters,
            float anisotropy,
            Color singleScatteringAlbedo,
            float exposureCompensationEv,
            float volumetricLightDimmer)
        {
            return Create(
                weatherId,
                fogTint,
                baseHeightMeters,
                maximumHeightMeters,
                maximumFogDistanceMeters,
                anisotropy,
                singleScatteringAlbedo,
                exposureCompensationEv,
                volumetricLightDimmer,
                Color.white,
                0f,
                0f,
                0f,
                0f);
        }

        public static HybridWeatherRenderSettings Create(
            string weatherId,
            Color fogTint,
            float baseHeightMeters,
            float maximumHeightMeters,
            float maximumFogDistanceMeters,
            float anisotropy,
            Color singleScatteringAlbedo,
            float exposureCompensationEv,
            float volumetricLightDimmer,
            Color daytimeColorFilter,
            float daytimeTemperature,
            float daytimeTint,
            float daytimeContrast,
            float daytimeSaturation)
        {
            return new HybridWeatherRenderSettings
            {
                weatherId = weatherId,
                fogTint = fogTint,
                baseHeightMeters = baseHeightMeters,
                maximumHeightMeters = maximumHeightMeters,
                maximumFogDistanceMeters = maximumFogDistanceMeters,
                anisotropy = anisotropy,
                singleScatteringAlbedo = singleScatteringAlbedo,
                exposureCompensationEv = exposureCompensationEv,
                volumetricLightDimmer = volumetricLightDimmer,
                daytimeColorFilter = daytimeColorFilter,
                daytimeTemperature = daytimeTemperature,
                daytimeTint = daytimeTint,
                daytimeContrast = daytimeContrast,
                daytimeSaturation = daytimeSaturation,
            };
        }

        public static HybridWeatherRenderSettings Lerp(
            in HybridWeatherRenderSettings from,
            in HybridWeatherRenderSettings to,
            float progress01) => Create(
                progress01 < 0.5f ? from.WeatherId : to.WeatherId,
                Color.Lerp(from.FogTint, to.FogTint, progress01),
                Mathf.Lerp(from.BaseHeightMeters, to.BaseHeightMeters, progress01),
                Mathf.Lerp(
                    from.MaximumHeightMeters,
                    to.MaximumHeightMeters,
                    progress01),
                Mathf.Lerp(
                    from.MaximumFogDistanceMeters,
                    to.MaximumFogDistanceMeters,
                    progress01),
                Mathf.Lerp(from.Anisotropy, to.Anisotropy, progress01),
                Color.Lerp(
                    from.SingleScatteringAlbedo,
                    to.SingleScatteringAlbedo,
                    progress01),
                Mathf.Lerp(
                    from.ExposureCompensationEv,
                    to.ExposureCompensationEv,
                    progress01),
                Mathf.Lerp(
                    from.VolumetricLightDimmer,
                    to.VolumetricLightDimmer,
                    progress01),
                Color.Lerp(
                    from.DaytimeColorFilter,
                    to.DaytimeColorFilter,
                    progress01),
                Mathf.Lerp(
                    from.DaytimeTemperature,
                    to.DaytimeTemperature,
                    progress01),
                Mathf.Lerp(from.DaytimeTint, to.DaytimeTint, progress01),
                Mathf.Lerp(
                    from.DaytimeContrast,
                    to.DaytimeContrast,
                    progress01),
                Mathf.Lerp(
                    from.DaytimeSaturation,
                    to.DaytimeSaturation,
                    progress01));
    }

    [CreateAssetMenu(
        fileName = "HybridWeatherRenderProfile",
        menuName = "MSC Remake/Environment/Hybrid HDRP Weather Profile")]
    public sealed class HybridWeatherRenderProfile : ScriptableObject
    {
        [SerializeField] private HybridWeatherRenderSettings fallback =
            HybridWeatherRenderSettings.Default;
        [SerializeField] private HybridWeatherRenderSettings[] entries =
            Array.Empty<HybridWeatherRenderSettings>();

        public HybridWeatherRenderSettings Resolve(string weatherId)
        {
            if (entries != null)
            {
                for (int index = 0; index < entries.Length; index++)
                {
                    if (string.Equals(
                            entries[index].WeatherId,
                            weatherId,
                            StringComparison.Ordinal))
                    {
                        return entries[index];
                    }
                }
            }

            return fallback;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            HybridWeatherRenderSettings authoredFallback,
            HybridWeatherRenderSettings[] authoredEntries)
        {
            fallback = authoredFallback;
            entries = authoredEntries ?? Array.Empty<HybridWeatherRenderSettings>();
        }
#endif
    }

    public static class NativeHdrpFogMath
    {
        private const float VisibilityExtinctionCoefficient = 3.912f;

        public static float MeanFreePathFromVisibility(float visibilityMeters)
        {
            if (!float.IsFinite(visibilityMeters) || visibilityMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibilityMeters));
            }

            return Mathf.Clamp(
                visibilityMeters / VisibilityExtinctionCoefficient,
                1f,
                1000000f);
        }

        public static float BlendMeanFreePathByDensity(
            float fromMeanFreePath,
            float toMeanFreePath,
            float progress01)
        {
            if (!float.IsFinite(fromMeanFreePath) || fromMeanFreePath <= 0f ||
                !float.IsFinite(toMeanFreePath) || toMeanFreePath <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fromMeanFreePath));
            }

            float fromDensity = 1f / fromMeanFreePath;
            float toDensity = 1f / toMeanFreePath;
            float density = Mathf.Lerp(
                fromDensity,
                toDensity,
                Mathf.Clamp01(progress01));
            return 1f / Mathf.Max(density, 0.000001f);
        }
    }

    public static class NativeHdrpExposureMath
    {
        // The donor reference keeps roads, tree trunks and the ground readable
        // even under a broad grey sky. The fixed camera baseline preserves that
        // response without tying exposure to whatever is in front of the camera;
        // donor ambient is transferred separately and stays neutral here.
        public const float FinnishSummerDaylightFixedExposureEv = 12f;
        private const float AmbientDarknessReadabilityLiftEv = 0.5f;
        private const float DawnStart01 = 3f / 24f;
        private const float FullDaylightStart01 = 7f / 24f;
        // Enviro's accepted Finnish sky is already visibly dim before the
        // project clock reaches sunset. Starting adaptation at 19:30 held the
        // camera at daylight EV while the rendered world was in dusk, making
        // every electric light look inert until a late multi-stop jump.
        private const float DuskStart01 = 17f / 24f;
        private const float FullNightStart01 = 22.5f / 24f;
        // Donor RenderSettings used ambient intensity 1. Keep HDRP's indirect
        // controller neutral so it does not brighten the transferred flat fill.
        private const float MinimumExteriorIndirectDiffuse = 1f;
        private const float MaximumExteriorIndirectDiffuse = 1f;

        public static float CalculateFixedExposureEv(
            float daylightFixedExposureEv,
            float readableNightFixedExposureEv,
            float timeOfDay01,
            float weatherCompensationEv,
            float ambientDarkness01,
            Vector2 limitsEv)
        {
            if (!float.IsFinite(daylightFixedExposureEv) ||
                !float.IsFinite(readableNightFixedExposureEv) ||
                !float.IsFinite(timeOfDay01) ||
                !float.IsFinite(weatherCompensationEv) ||
                !float.IsFinite(ambientDarkness01) ||
                !float.IsFinite(limitsEv.x) ||
                !float.IsFinite(limitsEv.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(daylightFixedExposureEv));
            }

            float dailyExposureEv = CalculateDailyExposureEv(
                daylightFixedExposureEv,
                readableNightFixedExposureEv,
                timeOfDay01);
            return Mathf.Clamp(
                dailyExposureEv - weatherCompensationEv -
                Mathf.Clamp01(ambientDarkness01) *
                AmbientDarknessReadabilityLiftEv,
                Mathf.Min(limitsEv.x, limitsEv.y),
                Mathf.Max(limitsEv.x, limitsEv.y));
        }

        /// <summary>
        /// Returns a camera-independent fixed EV for the project clock. Midnight
        /// through 03:00 stays on a deliberately restrained visibility floor;
        /// smooth dawn/dusk ramps avoid brightness steps when game time is fast.
        /// Higher HDRP Fixed Exposure EV values darken the rendered image.
        /// </summary>
        public static float CalculateDailyExposureEv(
            float daylightFixedExposureEv,
            float readableNightFixedExposureEv,
            float timeOfDay01)
        {
            if (!float.IsFinite(daylightFixedExposureEv) ||
                !float.IsFinite(readableNightFixedExposureEv) ||
                !float.IsFinite(timeOfDay01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(daylightFixedExposureEv));
            }

            float wrappedTime = Mathf.Repeat(timeOfDay01, 1f);
            float daylightWeight;
            if (wrappedTime < DawnStart01)
            {
                daylightWeight = 0f;
            }
            else if (wrappedTime < FullDaylightStart01)
            {
                daylightWeight = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        DawnStart01,
                        FullDaylightStart01,
                        wrappedTime));
            }
            else if (wrappedTime < DuskStart01)
            {
                daylightWeight = 1f;
            }
            else if (wrappedTime < FullNightStart01)
            {
                daylightWeight = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        DuskStart01,
                        FullNightStart01,
                        wrappedTime));
            }
            else
            {
                daylightWeight = 0f;
            }

            return Mathf.Lerp(
                readableNightFixedExposureEv,
                daylightFixedExposureEv,
                daylightWeight);
        }

        public static float ApplyIndoorExposureLift(
            float exteriorExposureEv,
            float indoorFactor01,
            float maximumLiftEv,
            Vector2 limitsEv)
        {
            if (!float.IsFinite(exteriorExposureEv) ||
                !float.IsFinite(indoorFactor01) ||
                !float.IsFinite(maximumLiftEv) ||
                maximumLiftEv < 0f ||
                !float.IsFinite(limitsEv.x) ||
                !float.IsFinite(limitsEv.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exteriorExposureEv));
            }

            // Higher HDRP Fixed Exposure values darken the image. Use the
            // already smoothed enclosure signal so entering a donor-baseline
            // interior brightens gradually without camera histogram pumping.
            return Mathf.Clamp(
                exteriorExposureEv -
                Mathf.Clamp01(indoorFactor01) * maximumLiftEv,
                Mathf.Min(limitsEv.x, limitsEv.y),
                Mathf.Max(limitsEv.x, limitsEv.y));
        }

        public static float CalculateIndirectDiffuseMultiplier(
            float sunVisibility01,
            float indoorFactor01,
            float minimumIndoorMultiplier)
        {
            if (!float.IsFinite(sunVisibility01) ||
                !float.IsFinite(indoorFactor01) ||
                !float.IsFinite(minimumIndoorMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(sunVisibility01));
            }

            float exterior = Mathf.Lerp(
                MinimumExteriorIndirectDiffuse,
                MaximumExteriorIndirectDiffuse,
                Mathf.Clamp01(sunVisibility01));
            return Mathf.Lerp(
                exterior,
                Mathf.Clamp01(minimumIndoorMultiplier),
                Mathf.Clamp01(indoorFactor01));
        }
    }

    public static class NativeHdrpColorGradingMath
    {
        private const float InteriorGradeFraction = 0.35f;

        public static float CalculateGradeWeight(
            float timeOfDay01,
            float indoorFactor01)
        {
            if (!float.IsFinite(timeOfDay01) ||
                !float.IsFinite(indoorFactor01))
            {
                throw new ArgumentOutOfRangeException(nameof(timeOfDay01));
            }

            float daylightWeight = NativeHdrpExposureMath
                .CalculateDailyExposureEv(1f, 0f, timeOfDay01);
            float contextWeight = Mathf.Lerp(
                1f,
                InteriorGradeFraction,
                Mathf.Clamp01(indoorFactor01));
            return Mathf.Clamp01(daylightWeight) * contextWeight;
        }

        public static Color ApplyWeight(Color daytimeFilter, float weight01)
        {
            return Color.LerpUnclamped(
                Color.white,
                daytimeFilter,
                Mathf.Clamp(weight01, 0f, 2f));
        }

        public static float ApplyWeight(float daytimeValue, float weight01)
        {
            if (!float.IsFinite(daytimeValue) || !float.IsFinite(weight01))
            {
                throw new ArgumentOutOfRangeException(nameof(daytimeValue));
            }

            return daytimeValue * Mathf.Clamp(weight01, 0f, 2f);
        }
    }
}
