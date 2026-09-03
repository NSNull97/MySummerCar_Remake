using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting
{
    [CreateAssetMenu(
        menuName = "MSC/Lighting/Light Fixture Profile",
        fileName = "LightFixtureProfile")]
    public sealed class LightFixtureProfile : ScriptableObject
    {
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private LightFixtureCategory category;
        [SerializeField] private LightFixtureShape shape;
        [SerializeField] private LightUnit lightUnit = LightUnit.Lumen;
        [SerializeField, Min(0f)] private float intensity = 800f;
        [SerializeField, Min(0.05f)] private float rangeMeters = 10f;
        [SerializeField, Range(1000f, 20000f)] private float colorTemperatureKelvin = 2700f;
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField, Min(0f)] private float sourceRadiusMeters = 0.025f;
        [SerializeField, Min(0f)] private float sourceWidthMeters = 0.2f;
        [SerializeField, Min(0f)] private float sourceHeightMeters = 0.1f;
        [SerializeField, Range(0.1f, 179f)] private float innerSpotAngle = 45f;
        [SerializeField, Range(0.1f, 179f)] private float outerSpotAngle = 65f;
        [SerializeField] private bool enableSpotReflector = true;
        [SerializeField] private Texture cookie;
        [SerializeField] private Texture iesProfile;
        [SerializeField, Min(0f)] private float shadowFadeDistanceMeters = 25f;
        [SerializeField, Range(0f, 4f)] private float nativeVolumetricMultiplier = 0.15f;
        [SerializeField, Range(0f, 8f)] private float beamClearAirIntensity = 0.05f;
        [SerializeField, Range(0f, 8f)] private float beamFogIntensity = 0.8f;
        [SerializeField, Range(0f, 8f)] private float beamRainIntensity = 0.45f;
        [SerializeField, Range(0f, 8f)] private float beamSnowIntensity = 0.6f;
        [SerializeField] private Color emissiveColor = Color.white;
        [SerializeField, Min(0f)] private float emissiveIntensity = 6f;
        [SerializeField, Range(0f, 4f)] private float bloomContribution = 0.25f;
        [SerializeField, Min(0f)] private float turnOnSeconds = 0.08f;
        [SerializeField, Min(0f)] private float turnOffSeconds = 0.12f;
        [SerializeField] private LightPowerPolicyKind powerPolicy;
        [SerializeField] private LightFixtureQualitySettings low;
        [SerializeField] private LightFixtureQualitySettings medium;
        [SerializeField] private LightFixtureQualitySettings high;
        [SerializeField] private LightFixtureQualitySettings ultra;

        public string ProfileId => profileId;
        public LightFixtureCategory Category => category;
        public LightFixtureShape Shape => shape;
        public LightUnit LightUnit => lightUnit;
        public float Intensity => intensity;
        public float RangeMeters => rangeMeters;
        public float ColorTemperatureKelvin => colorTemperatureKelvin;
        public Color LightColor => lightColor;
        public float SourceRadiusMeters => sourceRadiusMeters;
        public float SourceWidthMeters => sourceWidthMeters;
        public float SourceHeightMeters => sourceHeightMeters;
        public float InnerSpotAngle => innerSpotAngle;
        public float OuterSpotAngle => outerSpotAngle;
        public bool EnableSpotReflector => enableSpotReflector;
        public Texture Cookie => cookie;
        public Texture IesProfile => iesProfile;
        public float ShadowFadeDistanceMeters => shadowFadeDistanceMeters;
        public float NativeVolumetricMultiplier => nativeVolumetricMultiplier;
        public float BeamClearAirIntensity => beamClearAirIntensity;
        public float BeamFogIntensity => beamFogIntensity;
        public float BeamRainIntensity => beamRainIntensity;
        public float BeamSnowIntensity => beamSnowIntensity;
        public Color EmissiveColor => emissiveColor;
        public float EmissiveIntensity => emissiveIntensity;
        public float BloomContribution => bloomContribution;
        public float TurnOnSeconds => turnOnSeconds;
        public float TurnOffSeconds => turnOffSeconds;
        public LightPowerPolicyKind PowerPolicy => powerPolicy;

        public LightFixtureQualitySettings GetQuality(
            LightingQualityTier tier)
        {
            return tier switch
            {
                LightingQualityTier.Low => low,
                LightingQualityTier.Medium => medium,
                LightingQualityTier.High => high,
                LightingQualityTier.Ultra => ultra,
                _ => high,
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new InvalidOperationException(
                    $"Lighting profile '{name}' has no stable profile ID.");
            }

            if (intensity <= 0f || rangeMeters <= 0f ||
                outerSpotAngle < innerSpotAngle)
            {
                throw new InvalidOperationException(
                    $"Lighting profile '{profileId}' has invalid photometry.");
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            LightFixtureCategory configuredCategory,
            LightFixtureShape configuredShape,
            LightUnit configuredLightUnit,
            float configuredIntensity,
            float configuredRange,
            float configuredTemperature,
            Color configuredColor,
            float configuredSourceRadius,
            Vector2 configuredAreaSize,
            Vector2 configuredSpotAngles,
            bool configuredReflector,
            float configuredShadowFadeDistance,
            float configuredNativeVolumetric,
            Vector4 beamAtmosphereIntensities,
            Color configuredEmissiveColor,
            float configuredEmissiveIntensity,
            float configuredBloom,
            Vector2 switchingSeconds,
            LightPowerPolicyKind configuredPowerPolicy,
            LightFixtureQualitySettings configuredLow,
            LightFixtureQualitySettings configuredMedium,
            LightFixtureQualitySettings configuredHigh,
            LightFixtureQualitySettings configuredUltra)
        {
            profileId = id ?? string.Empty;
            category = configuredCategory;
            shape = configuredShape;
            lightUnit = configuredLightUnit;
            intensity = configuredIntensity;
            rangeMeters = configuredRange;
            colorTemperatureKelvin = configuredTemperature;
            lightColor = configuredColor;
            sourceRadiusMeters = configuredSourceRadius;
            sourceWidthMeters = configuredAreaSize.x;
            sourceHeightMeters = configuredAreaSize.y;
            innerSpotAngle = configuredSpotAngles.x;
            outerSpotAngle = configuredSpotAngles.y;
            enableSpotReflector = configuredReflector;
            shadowFadeDistanceMeters = configuredShadowFadeDistance;
            nativeVolumetricMultiplier = configuredNativeVolumetric;
            beamClearAirIntensity = beamAtmosphereIntensities.x;
            beamFogIntensity = beamAtmosphereIntensities.y;
            beamRainIntensity = beamAtmosphereIntensities.z;
            beamSnowIntensity = beamAtmosphereIntensities.w;
            emissiveColor = configuredEmissiveColor;
            emissiveIntensity = configuredEmissiveIntensity;
            bloomContribution = configuredBloom;
            turnOnSeconds = switchingSeconds.x;
            turnOffSeconds = switchingSeconds.y;
            powerPolicy = configuredPowerPolicy;
            low = configuredLow;
            medium = configuredMedium;
            high = configuredHigh;
            ultra = configuredUltra;
            Validate();
        }
#endif
    }
}
