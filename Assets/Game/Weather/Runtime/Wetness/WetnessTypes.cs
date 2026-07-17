using System;

namespace MSC.Weather.Wetness
{
    public readonly struct WetnessConfig
    {
        public const string RemakeDesignTargetConfigId = "weather.wetness.remake_design_target.v1";
        public const string RemakeDesignTargetProvenance = "RemakeDesignTarget";

        public WetnessConfig(
            string configId,
            float groundAccumulationPerSecond,
            float roadAccumulationPerSecond,
            float puddleAccumulationPerSecond,
            float vegetationAccumulationPerSecond,
            float baseDryingPerSecond,
            float puddleDrainagePerSecond,
            float windDryingPerMeterPerSecond,
            float warmTemperatureDryingPerDegree,
            float sunDryingMultiplier,
            float referenceTemperatureCelsius,
            string provenance)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("Wetness config ID is required.", nameof(configId));
            }

            ValidateNonNegative(groundAccumulationPerSecond, nameof(groundAccumulationPerSecond));
            ValidateNonNegative(roadAccumulationPerSecond, nameof(roadAccumulationPerSecond));
            ValidateNonNegative(puddleAccumulationPerSecond, nameof(puddleAccumulationPerSecond));
            ValidateNonNegative(vegetationAccumulationPerSecond, nameof(vegetationAccumulationPerSecond));
            ValidateNonNegative(baseDryingPerSecond, nameof(baseDryingPerSecond));
            ValidateNonNegative(puddleDrainagePerSecond, nameof(puddleDrainagePerSecond));
            ValidateNonNegative(windDryingPerMeterPerSecond, nameof(windDryingPerMeterPerSecond));
            ValidateNonNegative(warmTemperatureDryingPerDegree, nameof(warmTemperatureDryingPerDegree));
            ValidateNonNegative(sunDryingMultiplier, nameof(sunDryingMultiplier));
            ValidateFinite(referenceTemperatureCelsius, nameof(referenceTemperatureCelsius));
            if (string.IsNullOrWhiteSpace(provenance))
            {
                throw new ArgumentException("Wetness config provenance is required.", nameof(provenance));
            }

            ConfigId = configId;
            GroundAccumulationPerSecond = groundAccumulationPerSecond;
            RoadAccumulationPerSecond = roadAccumulationPerSecond;
            PuddleAccumulationPerSecond = puddleAccumulationPerSecond;
            VegetationAccumulationPerSecond = vegetationAccumulationPerSecond;
            BaseDryingPerSecond = baseDryingPerSecond;
            PuddleDrainagePerSecond = puddleDrainagePerSecond;
            WindDryingPerMeterPerSecond = windDryingPerMeterPerSecond;
            WarmTemperatureDryingPerDegree = warmTemperatureDryingPerDegree;
            SunDryingMultiplier = sunDryingMultiplier;
            ReferenceTemperatureCelsius = referenceTemperatureCelsius;
            Provenance = provenance;
        }

        public string ConfigId { get; }
        public float GroundAccumulationPerSecond { get; }
        public float RoadAccumulationPerSecond { get; }
        public float PuddleAccumulationPerSecond { get; }
        public float VegetationAccumulationPerSecond { get; }
        public float BaseDryingPerSecond { get; }
        public float PuddleDrainagePerSecond { get; }
        public float WindDryingPerMeterPerSecond { get; }
        public float WarmTemperatureDryingPerDegree { get; }
        public float SunDryingMultiplier { get; }
        public float ReferenceTemperatureCelsius { get; }
        public string Provenance { get; }

        public static WetnessConfig CreateRemakeDesignTarget() => new WetnessConfig(
            RemakeDesignTargetConfigId,
            groundAccumulationPerSecond: 0.0045f,
            roadAccumulationPerSecond: 0.0055f,
            puddleAccumulationPerSecond: 0.0022f,
            vegetationAccumulationPerSecond: 0.008f,
            baseDryingPerSecond: 0.00028f,
            puddleDrainagePerSecond: 0.00008f,
            windDryingPerMeterPerSecond: 0.045f,
            warmTemperatureDryingPerDegree: 0.018f,
            sunDryingMultiplier: 1.5f,
            referenceTemperatureCelsius: 8f,
            provenance: RemakeDesignTargetProvenance);

        internal static void ValidateNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static void Validate01(float value, string name)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static void ValidateFinite(float value, string name)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public readonly struct WetnessState : IEquatable<WetnessState>
    {
        public WetnessState(float groundWetness01, float roadWetness01, float puddleAmount01, float vegetationWetness01)
        {
            WetnessConfig.Validate01(groundWetness01, nameof(groundWetness01));
            WetnessConfig.Validate01(roadWetness01, nameof(roadWetness01));
            WetnessConfig.Validate01(puddleAmount01, nameof(puddleAmount01));
            WetnessConfig.Validate01(vegetationWetness01, nameof(vegetationWetness01));
            GroundWetness01 = groundWetness01;
            RoadWetness01 = roadWetness01;
            PuddleAmount01 = puddleAmount01;
            VegetationWetness01 = vegetationWetness01;
        }

        public float GroundWetness01 { get; }
        public float RoadWetness01 { get; }
        public float PuddleAmount01 { get; }
        public float VegetationWetness01 { get; }

        public bool Equals(WetnessState other) =>
            GroundWetness01.Equals(other.GroundWetness01) &&
            RoadWetness01.Equals(other.RoadWetness01) &&
            PuddleAmount01.Equals(other.PuddleAmount01) &&
            VegetationWetness01.Equals(other.VegetationWetness01);

        public override bool Equals(object obj) => obj is WetnessState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = GroundWetness01.GetHashCode();
                hash = (hash * 397) ^ RoadWetness01.GetHashCode();
                hash = (hash * 397) ^ PuddleAmount01.GetHashCode();
                hash = (hash * 397) ^ VegetationWetness01.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct SurfaceExposureProfile
    {
        public SurfaceExposureProfile(
            string stableId,
            float precipitationExposure01,
            float windExposure01,
            float sunExposure01,
            float dryingMultiplier)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Exposure profile ID is required.", nameof(stableId));
            }

            WetnessConfig.Validate01(precipitationExposure01, nameof(precipitationExposure01));
            WetnessConfig.Validate01(windExposure01, nameof(windExposure01));
            WetnessConfig.Validate01(sunExposure01, nameof(sunExposure01));
            WetnessConfig.ValidateNonNegative(dryingMultiplier, nameof(dryingMultiplier));
            StableId = stableId;
            PrecipitationExposure01 = precipitationExposure01;
            WindExposure01 = windExposure01;
            SunExposure01 = sunExposure01;
            DryingMultiplier = dryingMultiplier;
        }

        public string StableId { get; }
        public float PrecipitationExposure01 { get; }
        public float WindExposure01 { get; }
        public float SunExposure01 { get; }
        public float DryingMultiplier { get; }

        public static SurfaceExposureProfile Exterior =>
            new SurfaceExposureProfile("exposure.exterior", 1f, 1f, 1f, 1f);

        public static SurfaceExposureProfile Sheltered =>
            new SurfaceExposureProfile("exposure.sheltered", 0.12f, 0.35f, 0.2f, 0.55f);

        public static SurfaceExposureProfile Interior =>
            new SurfaceExposureProfile("exposure.interior", 0f, 0.05f, 0f, 0.15f);
    }

    public readonly struct WetnessEnvironmentInputs
    {
        public WetnessEnvironmentInputs(
            double deltaSeconds,
            float precipitationIntensity01,
            float windSpeedMetersPerSecond,
            float temperatureCelsius,
            float sunIntensity01,
            float weatherDryingModifier,
            SurfaceExposureProfile exposure)
        {
            if (!WetnessConfig.IsFinite(deltaSeconds) || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            WetnessConfig.Validate01(precipitationIntensity01, nameof(precipitationIntensity01));
            WetnessConfig.ValidateNonNegative(windSpeedMetersPerSecond, nameof(windSpeedMetersPerSecond));
            WetnessConfig.ValidateFinite(temperatureCelsius, nameof(temperatureCelsius));
            WetnessConfig.Validate01(sunIntensity01, nameof(sunIntensity01));
            WetnessConfig.ValidateNonNegative(weatherDryingModifier, nameof(weatherDryingModifier));
            if (string.IsNullOrEmpty(exposure.StableId))
            {
                throw new ArgumentException("A valid exposure profile is required.", nameof(exposure));
            }

            DeltaSeconds = deltaSeconds;
            PrecipitationIntensity01 = precipitationIntensity01;
            WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
            TemperatureCelsius = temperatureCelsius;
            SunIntensity01 = sunIntensity01;
            WeatherDryingModifier = weatherDryingModifier;
            Exposure = exposure;
        }

        public double DeltaSeconds { get; }
        public float PrecipitationIntensity01 { get; }
        public float WindSpeedMetersPerSecond { get; }
        public float TemperatureCelsius { get; }
        public float SunIntensity01 { get; }
        public float WeatherDryingModifier { get; }
        public SurfaceExposureProfile Exposure { get; }
    }

    public readonly struct WetnessEnvironmentOutputs
    {
        public WetnessEnvironmentOutputs(WetnessState state, string exposureProfileId, uint revision)
        {
            if (string.IsNullOrWhiteSpace(exposureProfileId))
            {
                throw new ArgumentException("Exposure profile ID is required.", nameof(exposureProfileId));
            }

            State = state;
            ExposureProfileId = exposureProfileId;
            Revision = revision;
        }

        public WetnessState State { get; }
        public float GroundWetness01 => State.GroundWetness01;
        public float RoadWetness01 => State.RoadWetness01;
        public float PuddleAmount01 => State.PuddleAmount01;
        public float VegetationWetness01 => State.VegetationWetness01;
        public string ExposureProfileId { get; }
        public uint Revision { get; }
    }

    public readonly struct WetnessSnapshot
    {
        public WetnessSnapshot(string configId, WetnessState state, uint revision)
        {
            ConfigId = configId;
            State = state;
            Revision = revision;
        }

        public string ConfigId { get; }
        public WetnessState State { get; }
        public uint Revision { get; }
    }

    public interface IWetnessShaderBridge
    {
        void Apply(in WetnessEnvironmentOutputs outputs);
    }
}
