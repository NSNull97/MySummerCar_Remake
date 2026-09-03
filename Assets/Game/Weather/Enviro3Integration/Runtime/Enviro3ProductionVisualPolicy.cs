using System;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration
{
    public enum Enviro3CloudVisualKind
    {
        Clear = 0,
        PartlyCloudy = 1,
        Overcast = 2,
        Drizzle = 3,
        Rain = 4,
        HeavyRain = 5,
        Storm = 6,
        Fog = 7,
        BrightOvercast = 8,
    }

    public readonly struct Enviro3CloudVisualCalibration
    {
        public Enviro3CloudVisualCalibration(
            float coverageLayer1,
            float densityLayer1,
            float densitySmoothnessLayer1,
            float cirrusAlpha,
            float cirrusCoverage,
            float ambientLightIntensity)
        {
            CoverageLayer1 = coverageLayer1;
            DensityLayer1 = densityLayer1;
            DensitySmoothnessLayer1 = densitySmoothnessLayer1;
            CirrusAlpha = cirrusAlpha;
            CirrusCoverage = cirrusCoverage;
            AmbientLightIntensity = ambientLightIntensity;
        }

        public float CoverageLayer1 { get; }

        public float DensityLayer1 { get; }

        public float DensitySmoothnessLayer1 { get; }

        public float CirrusAlpha { get; }

        public float CirrusCoverage { get; }

        public float AmbientLightIntensity { get; }
    }

    public readonly struct Enviro3CloudShapeCalibration
    {
        public Enviro3CloudShapeCalibration(
            float dilateCoverageLayer1,
            float dilateTypeLayer1,
            float typeModifierLayer1,
            float scatteringIntensityLayer1,
            float powderIntensityLayer1,
            float silverLiningSpreadLayer1,
            float lightAbsorptionLayer1,
            float baseErosionIntensityLayer1,
            float detailErosionIntensityLayer1,
            float curlIntensityLayer1)
        {
            DilateCoverageLayer1 = dilateCoverageLayer1;
            DilateTypeLayer1 = dilateTypeLayer1;
            TypeModifierLayer1 = typeModifierLayer1;
            ScatteringIntensityLayer1 = scatteringIntensityLayer1;
            PowderIntensityLayer1 = powderIntensityLayer1;
            SilverLiningSpreadLayer1 = silverLiningSpreadLayer1;
            LightAbsorptionLayer1 = lightAbsorptionLayer1;
            BaseErosionIntensityLayer1 = baseErosionIntensityLayer1;
            DetailErosionIntensityLayer1 = detailErosionIntensityLayer1;
            CurlIntensityLayer1 = curlIntensityLayer1;
        }

        public float DilateCoverageLayer1 { get; }

        public float DilateTypeLayer1 { get; }

        public float TypeModifierLayer1 { get; }

        public float ScatteringIntensityLayer1 { get; }

        public float PowderIntensityLayer1 { get; }

        public float SilverLiningSpreadLayer1 { get; }

        public float LightAbsorptionLayer1 { get; }

        public float BaseErosionIntensityLayer1 { get; }

        public float DetailErosionIntensityLayer1 { get; }

        public float CurlIntensityLayer1 { get; }
    }

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
        public const float MinimumNightExposureEv = 7.25f;
        public const float FullNightSolarTime = 0.43f;
        public const float DaylightSolarTime = 0.5f;
        public const float NeutralIndirectLightingMultiplier = 1f;
        public const float ExteriorDaylightIndirectDiffuseMultiplier = 1.35f;
        public const float ShelteredIndirectDiffuseUpliftFraction = 0.5f;
        public const float IndirectReflectionLightingMultiplier = 1f;
        public const float IndirectReflectionProbeIntensityMultiplier = 1f;

        // Art-directed solar calibration for the approved summer clock. These
        // values target the visible cycle and are not an in-world GPS location.
        public const float ProductionLatitudeDegrees = 60f;
        public const float ProductionLongitudeDegrees = 27.3f;
        public const int ProductionUtcOffsetHours = 3;
        public const float TemporaryBaselineReflectionIntensity = 0.6f;
        public const float MinimumVisibleMoonScale = 9.5f;
        // Fixed exposure 7.25 EV makes the physical ~0.35 lux full moon
        // imperceptible in this game camera. This remains a restrained,
        // art-directed gameplay value, not a claim of physical moon illuminance.
        public const float MaximumFullMoonIlluminanceLux = 8f;
        public const float MoonlightColorTemperatureKelvin = 8500f;
        public const float MaximumMoonShadowStrength = 0.28f;
        public const float MoonFullVisibilityLocalHeight = 0.24f;
        public const float ClearSkyDirectSunlightMultiplier = 1f;
        public const float MinimumCloudedDirectSunlightMultiplier = 0.22f;
        public const float ClearSkySunShadowStrength =
            DonorWorldLightingPolicy.DonorSunShadowStrength;
        public const float MinimumCloudedSunShadowStrength = 0.08f;
        public const float FinnishDaylightColorTemperatureKelvin = 6500f;
        public const float MinimumFogMeanFreePathMeters = 20f;
        public const float MaximumFogMeanFreePathMeters = 6000f;
        public const float FogBaseHeightMeters = 0f;
        public const float FogMaximumHeightMeters = 50f;
        public const float VolumetricLightDimmer = 1f;
        public const float MinimumReadableRainParticleScreenSize = 0.0035f;
        public const float RainEmissionDensityMultiplier = 6f;
        public const float MaximumRainEmissionPerSecond = 8000f;
        public const int MaximumRainParticleBudget = 8000;
        public const int MaximumRainSplashParticleBudget = 512;
        public const float MinimumRainParticleSizeMultiplier = 0.25f;
        public const float MaximumRainParticleSizeMultiplier = 0.48f;
        public const float MinimumRainStreakLengthMultiplier = 0.65f;
        public const float MaximumRainStreakLengthMultiplier = 1f;
        public const float MaximumRainSplashParticleScreenSize = 0.015f;
        public const float MinimumRainSplashSizeMultiplier = 0.75f;
        public const float MaximumRainSplashSizeMultiplier = 1.35f;
        public const float RainCollisionRadiusScale = 0.1f;
        public const float RainCollisionVoxelSize = 2f;
        public const int RainCollisionShapeBudget = 128;
        public const int RainCollisionLayerMask =
            (1 << 0) | (1 << 6) | (1 << 7);
        public const float RainSplashCollisionEmissionProbability = 0.2f;
        public const float CloudFieldWindSpeedModifier = 0.012f;
        public const float CloudFieldTravelSpeed = 0.35f;
        public const float CloudFieldOffsetExtent = 2f;

        // Koschmieder's law at the conventional 2% contrast threshold.
        private const float VisibilityExtinctionCoefficient = 3.912f;
        private const float FullNightPaletteSolarTime = 0.46f;
        private const float DaylightPaletteSolarTime = 0.54f;
        private const float StarsFadeStartSolarTime = 0.44f;
        private const float StarsFadeEndSolarTime = 0.56f;

        public static Enviro3CloudVisualCalibration ResolveCloudCalibration(
            Enviro3CloudVisualKind kind)
        {
            switch (kind)
            {
                case Enviro3CloudVisualKind.Clear:
                    return new Enviro3CloudVisualCalibration(
                        -0.92f, 0.32f, 1.25f, 0.015f, 0.12f, 1f);
                case Enviro3CloudVisualKind.PartlyCloudy:
                    return new Enviro3CloudVisualCalibration(
                        -0.08f, 0.95f, 1.3f, 0.05f, 0.26f, 0.94f);
                case Enviro3CloudVisualKind.BrightOvercast:
                    return new Enviro3CloudVisualCalibration(
                        0.34f, 0.62f, 1.32f, 0.03f, 0.2f, 0.9f);
                case Enviro3CloudVisualKind.Overcast:
                    return new Enviro3CloudVisualCalibration(
                        0.58f, 0.55f, 1.5f, 0.02f, 0.16f, 0.72f);
                case Enviro3CloudVisualKind.Drizzle:
                    return new Enviro3CloudVisualCalibration(
                        0.6f, 0.36f, 1.65f, 0f, 0f, 0.68f);
                case Enviro3CloudVisualKind.Rain:
                    return new Enviro3CloudVisualCalibration(
                        0.63f, 0.38f, 1.65f, 0f, 0f, 0.62f);
                case Enviro3CloudVisualKind.HeavyRain:
                    return new Enviro3CloudVisualCalibration(
                        0.66f, 0.4f, 1.68f, 0f, 0f, 0.54f);
                case Enviro3CloudVisualKind.Storm:
                    return new Enviro3CloudVisualCalibration(
                        0.7f, 0.45f, 1.72f, 0.05f, 0.18f, 0.42f);
                case Enviro3CloudVisualKind.Fog:
                    return new Enviro3CloudVisualCalibration(
                        0.12f, 0.55f, 1.5f, 0.08f, 0.2f, 0.7f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static Enviro3CloudShapeCalibration ResolveCloudShapeCalibration(
            Enviro3CloudVisualKind kind)
        {
            switch (kind)
            {
                case Enviro3CloudVisualKind.Clear:
                    return new Enviro3CloudShapeCalibration(
                        0.75f, 0.55f, 0.45f, 1.05f, 0.72f,
                        0.8f, 0.28f, 0.35f, 0.3f, 0.25f);
                case Enviro3CloudVisualKind.PartlyCloudy:
                    return new Enviro3CloudShapeCalibration(
                        0.08f, 0.08f, 0.78f, 1.08f, 0.78f,
                        0.72f, 0.28f, 0.42f, 0.28f, 0.45f);
                case Enviro3CloudVisualKind.BrightOvercast:
                    return new Enviro3CloudShapeCalibration(
                        0.3f, 0.15f, 0.55f, 1.35f, 0.68f,
                        0.72f, 0.24f, 0.3f, 0.58f, 0.35f);
                case Enviro3CloudVisualKind.Overcast:
                    return new Enviro3CloudShapeCalibration(
                        0.52f, 0.1f, 0.62f, 0.85f, 0.5f,
                        0.82f, 0.52f, 0.12f, 0.58f, 0.22f);
                case Enviro3CloudVisualKind.Drizzle:
                    return new Enviro3CloudShapeCalibration(
                        0.58f, 0.08f, 0.65f, 0.7f, 0.45f,
                        0.84f, 0.62f, 0.08f, 0.68f, 0.2f);
                case Enviro3CloudVisualKind.Rain:
                    return new Enviro3CloudShapeCalibration(
                        0.65f, 0.08f, 0.68f, 0.55f, 0.38f,
                        0.86f, 0.72f, 0.05f, 0.75f, 0.18f);
                case Enviro3CloudVisualKind.HeavyRain:
                    return new Enviro3CloudShapeCalibration(
                        0.72f, 0.05f, 0.72f, 0.42f, 0.32f,
                        0.88f, 0.82f, 0.03f, 0.82f, 0.16f);
                case Enviro3CloudVisualKind.Storm:
                    return new Enviro3CloudShapeCalibration(
                        0.8f, 0.03f, 0.78f, 0.3f, 0.25f,
                        0.9f, 0.92f, 0f, 0.9f, 0.2f);
                case Enviro3CloudVisualKind.Fog:
                    return new Enviro3CloudShapeCalibration(
                        0.32f, 0.25f, 0.45f, 1.2f, 0.6f,
                        0.72f, 0.42f, 0.5f, 0.72f, 0.48f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static float CalculateEnviroCloudCoverage(
            float projectCloudCoverage01)
        {
            if (!float.IsFinite(projectCloudCoverage01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectCloudCoverage01));
            }

            float coverage = Mathf.Clamp01(projectCloudCoverage01);
            if (coverage <= 0.1f)
            {
                return Mathf.Lerp(-1f, -0.86f, coverage / 0.1f);
            }

            if (coverage <= 0.36f)
            {
                return Mathf.Lerp(
                    -0.86f,
                    -0.08f,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.1f, 0.36f, coverage)));
            }

            if (coverage <= 0.62f)
            {
                return Mathf.Lerp(
                    -0.08f,
                    0.14f,
                    Mathf.InverseLerp(0.36f, 0.62f, coverage));
            }

            if (coverage <= 0.78f)
            {
                return Mathf.Lerp(
                    0.14f,
                    0.34f,
                    Mathf.InverseLerp(0.62f, 0.78f, coverage));
            }

            if (coverage <= 0.94f)
            {
                return Mathf.Lerp(
                    0.34f,
                    0.58f,
                    Mathf.InverseLerp(0.78f, 0.94f, coverage));
            }

            return Mathf.Lerp(
                0.58f,
                0.68f,
                Mathf.InverseLerp(0.94f, 1f, coverage));
        }

        public static Color CalibrateNightSkyColor(
            Color authoredColor,
            float solarTime)
        {
            ValidateSolarTime(solarTime);
            float daylightProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    FullNightPaletteSolarTime,
                    DaylightPaletteSolarTime,
                    solarTime));
            float nightWeight = 1f - daylightProgress;
            float peak = authoredColor.maxColorComponent;
            Color restrainedNight = new Color(
                peak * 0.12f,
                peak * 0.18f,
                peak * 0.3f,
                authoredColor.a);
            return Color.Lerp(authoredColor, restrainedNight, nightWeight);
        }

        public static Color CalibrateFinnishSkyColor(
            Color authoredColor,
            float solarTime)
        {
            ValidateSolarTime(solarTime);
            Color nightCalibrated = CalibrateNightSkyColor(
                authoredColor,
                solarTime);
            float daylightWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.56f, 0.85f, solarTime));
            float peak = Mathf.Max(
                nightCalibrated.r,
                Mathf.Max(nightCalibrated.g, nightCalibrated.b));
            Color steelBlue = new Color(
                peak * 0.68f,
                peak * 0.82f,
                peak,
                authoredColor.a);
            return Color.Lerp(
                nightCalibrated,
                steelBlue,
                daylightWeight * 0.45f);
        }

        public static Color CalibrateFinnishDaylightColor(
            Color authoredColor,
            float solarTime)
        {
            ValidateSolarTime(solarTime);
            float daylightWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.56f, 0.85f, solarTime));
            float peak = Mathf.Max(
                authoredColor.r,
                Mathf.Max(authoredColor.g, authoredColor.b));
            Color coolNeutral = new Color(
                peak * 0.9f,
                peak * 0.96f,
                peak,
                authoredColor.a);
            return Color.Lerp(
                authoredColor,
                coolNeutral,
                daylightWeight * 0.86f);
        }

        public static Color CalibrateFinnishAmbientDaylightColor(
            Color authoredColor,
            float solarTime)
        {
            ValidateSolarTime(solarTime);
            float daylightWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.56f, 0.85f, solarTime));
            float peak = Mathf.Max(
                authoredColor.r,
                Mathf.Max(authoredColor.g, authoredColor.b));
            Color coolSkyFill = new Color(
                peak * 0.82f,
                peak * 0.91f,
                peak,
                authoredColor.a);
            return Color.Lerp(
                authoredColor,
                coolSkyFill,
                daylightWeight * 0.9f);
        }

        public static float CalibrateFinnishDaylightColorTemperature(
            float authoredKelvin,
            float solarTime)
        {
            if (!float.IsFinite(authoredKelvin) || authoredKelvin <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredKelvin));
            }

            ValidateSolarTime(solarTime);
            float daylightWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.56f, 0.85f, solarTime));
            return Mathf.Lerp(
                authoredKelvin,
                Mathf.Max(
                    authoredKelvin,
                    FinnishDaylightColorTemperatureKelvin),
                daylightWeight * 0.8f);
        }

        public static float CalculateCloudOpticalOcclusion01(
            float cloudCoverage01,
            float cloudOpticalDensity01)
        {
            ValidateNormalized(cloudCoverage01, nameof(cloudCoverage01));
            ValidateNormalized(
                cloudOpticalDensity01,
                nameof(cloudOpticalDensity01));

            float coverageOcclusion = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.75f, cloudCoverage01)) *
                0.94f;
            float densityOcclusion = Mathf.SmoothStep(
                0f,
                1f,
                cloudOpticalDensity01) * 0.75f;
            return Mathf.Clamp01(
                1f -
                (1f - coverageOcclusion) *
                (1f - densityOcclusion));
        }

        public static float CalculateDirectSunlightMultiplier(
            float cloudCoverage01,
            float cloudOpticalDensity01)
        {
            float occlusion = CalculateCloudOpticalOcclusion01(
                cloudCoverage01,
                cloudOpticalDensity01);
            return Mathf.Lerp(
                ClearSkyDirectSunlightMultiplier,
                MinimumCloudedDirectSunlightMultiplier,
                occlusion);
        }

        public static float CalculateSunShadowStrength(
            float cloudCoverage01,
            float cloudOpticalDensity01)
        {
            float occlusion = CalculateCloudOpticalOcclusion01(
                cloudCoverage01,
                cloudOpticalDensity01);
            return Mathf.Lerp(
                ClearSkySunShadowStrength,
                MinimumCloudedSunShadowStrength,
                occlusion);
        }

        public static float CalculateStarIntensity(
            float authoredIntensity,
            float solarTime)
        {
            if (!float.IsFinite(authoredIntensity) || authoredIntensity < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoredIntensity));
            }

            ValidateSolarTime(solarTime);
            float visibility = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    StarsFadeStartSolarTime,
                    StarsFadeEndSolarTime,
                    solarTime));
            return Mathf.Max(
                authoredIntensity * 1.35f,
                2.4f * visibility);
        }

        public static float CalculateMoonIlluminationFraction(float moonPhase)
        {
            if (!float.IsFinite(moonPhase))
            {
                throw new ArgumentOutOfRangeException(nameof(moonPhase));
            }

            float normalizedPhase = Mathf.Clamp01(Mathf.Abs(moonPhase) * 0.5f);
            return 0.5f * (1f + Mathf.Cos(Mathf.PI * normalizedPhase));
        }

        public static float CalculateMoonHorizonVisibility01(
            float moonLocalHeight)
        {
            if (!float.IsFinite(moonLocalHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(moonLocalHeight));
            }

            if (moonLocalHeight <= 0f)
            {
                return 0f;
            }

            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    0f,
                    MoonFullVisibilityLocalHeight,
                    moonLocalHeight));
        }

        public static float CalculateMoonlightIlluminanceLux(
            float moonLocalHeight,
            float moonPhase,
            float cloudCoverage01,
            float cloudOpticalDensity01)
        {
            float horizonVisibility = CalculateMoonHorizonVisibility01(
                moonLocalHeight);
            float phaseIllumination = CalculateMoonIlluminationFraction(
                moonPhase);
            float cloudOcclusion = CalculateCloudOpticalOcclusion01(
                cloudCoverage01,
                cloudOpticalDensity01);
            float cloudTransmission = Mathf.Lerp(1f, 0.06f, cloudOcclusion);
            return MaximumFullMoonIlluminanceLux *
                   horizonVisibility *
                   phaseIllumination *
                   cloudTransmission;
        }

        public static float CalculateMoonShadowStrength(
            float moonlightIlluminanceLux)
        {
            if (!float.IsFinite(moonlightIlluminanceLux) ||
                moonlightIlluminanceLux < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moonlightIlluminanceLux));
            }

            float illumination01 = Mathf.Clamp01(
                moonlightIlluminanceLux / MaximumFullMoonIlluminanceLux);
            return MaximumMoonShadowStrength * Mathf.Sqrt(illumination01);
        }

        public static Color CalibrateMoonlightColor(Color authoredColor)
        {
            float peak = Mathf.Max(0.001f, authoredColor.maxColorComponent);
            Color coolNeutral = new Color(
                peak * 0.82f,
                peak * 0.91f,
                peak,
                authoredColor.a);
            return Color.Lerp(authoredColor, coolNeutral, 0.85f);
        }

        public static Vector2 CalculateCloudFieldOffset(uint seed)
        {
            uint x = MixBits(seed ^ 0x9E3779B9u);
            uint y = MixBits(seed ^ 0x85EBCA6Bu);
            float normalizedX = (x & 0x00FFFFFFu) / 16777215f;
            float normalizedY = (y & 0x00FFFFFFu) / 16777215f;
            return new Vector2(
                Mathf.Lerp(-CloudFieldOffsetExtent, CloudFieldOffsetExtent, normalizedX),
                Mathf.Lerp(-CloudFieldOffsetExtent, CloudFieldOffsetExtent, normalizedY));
        }

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

        public static float CalculateIndirectDiffuseMultiplier(
            float solarTime,
            WeatherExposureContext context)
        {
            if (!float.IsFinite(solarTime))
            {
                throw new ArgumentOutOfRangeException(nameof(solarTime));
            }

            float daylightBlend = Mathf.InverseLerp(
                FullNightSolarTime,
                DaylightSolarTime,
                solarTime);
            float daylightWeight = Mathf.SmoothStep(
                0f,
                1f,
                daylightBlend);
            float contextUpliftFraction;
            switch (context)
            {
                case WeatherExposureContext.Exterior:
                    contextUpliftFraction = 1f;
                    break;
                case WeatherExposureContext.Sheltered:
                    contextUpliftFraction =
                        ShelteredIndirectDiffuseUpliftFraction;
                    break;
                case WeatherExposureContext.Interior:
                    contextUpliftFraction = 0f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context));
            }

            float daylightUplift =
                ExteriorDaylightIndirectDiffuseMultiplier -
                NeutralIndirectLightingMultiplier;
            return NeutralIndirectLightingMultiplier +
                   daylightUplift *
                   contextUpliftFraction *
                   daylightWeight;
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

        public static float ResolveRainMaximumEmission(float authoredMaximum)
        {
            if (!float.IsFinite(authoredMaximum) || authoredMaximum <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredMaximum));
            }

            return Mathf.Min(
                authoredMaximum * RainEmissionDensityMultiplier,
                MaximumRainEmissionPerSecond);
        }

        public static int ResolveRainParticleBudget(int authoredMaximum)
        {
            if (authoredMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredMaximum));
            }

            return Mathf.Min(authoredMaximum, MaximumRainParticleBudget);
        }

        public static int ResolveRainSplashParticleBudget(int authoredMaximum)
        {
            if (authoredMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredMaximum));
            }

            return Mathf.Min(
                authoredMaximum,
                MaximumRainSplashParticleBudget);
        }

        public static float CalculateRainParticleSizeMultiplier(
            float precipitationIntensity01)
        {
            if (!float.IsFinite(precipitationIntensity01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(precipitationIntensity01));
            }

            return Mathf.Lerp(
                MinimumRainParticleSizeMultiplier,
                MaximumRainParticleSizeMultiplier,
                Mathf.Clamp01(precipitationIntensity01));
        }

        public static float CalculateRainStreakLengthMultiplier(
            float precipitationIntensity01)
        {
            if (!float.IsFinite(precipitationIntensity01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(precipitationIntensity01));
            }

            return Mathf.Lerp(
                MinimumRainStreakLengthMultiplier,
                MaximumRainStreakLengthMultiplier,
                Mathf.Clamp01(precipitationIntensity01));
        }

        public static float ResolveRainSplashMaxScreenSize(
            float authoredMaximum)
        {
            if (!float.IsFinite(authoredMaximum) || authoredMaximum < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredMaximum));
            }

            return Mathf.Min(
                authoredMaximum,
                MaximumRainSplashParticleScreenSize);
        }

        public static float CalculateRainSplashSizeMultiplier(
            float precipitationIntensity01)
        {
            if (!float.IsFinite(precipitationIntensity01))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(precipitationIntensity01));
            }

            return Mathf.Lerp(
                MinimumRainSplashSizeMultiplier,
                MaximumRainSplashSizeMultiplier,
                Mathf.Clamp01(precipitationIntensity01));
        }

        private static void ValidateSolarTime(float solarTime)
        {
            if (!float.IsFinite(solarTime) || solarTime < 0f || solarTime > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(solarTime));
            }
        }

        private static void ValidateNormalized(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static uint MixBits(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
