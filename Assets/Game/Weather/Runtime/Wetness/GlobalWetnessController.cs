using System;

namespace MSC.Weather.Wetness
{
    public sealed class GlobalWetnessController
    {
        private readonly WetnessConfig config;
        private WetnessState state;
        private string lastExposureProfileId = SurfaceExposureProfile.Exterior.StableId;
        private uint revision = 1U;

        public GlobalWetnessController(WetnessConfig config, WetnessState initialState = default)
        {
            this.config = config;
            if (string.IsNullOrWhiteSpace(config.ConfigId))
            {
                throw new ArgumentException("A valid wetness config is required.", nameof(config));
            }

            state = initialState;
        }

        public string ConfigId => config.ConfigId;
        public WetnessState State => state;
        public uint Revision => revision;
        public WetnessEnvironmentOutputs Outputs => new WetnessEnvironmentOutputs(state, lastExposureProfileId, revision);

        public WetnessEnvironmentOutputs Advance(in WetnessEnvironmentInputs inputs)
        {
            lastExposureProfileId = inputs.Exposure.StableId;
            if (inputs.DeltaSeconds == 0d)
            {
                return Outputs;
            }

            float delta = (float)inputs.DeltaSeconds;
            float precipitation = inputs.PrecipitationIntensity01 * inputs.Exposure.PrecipitationExposure01;
            float warmDegrees = Math.Max(0f, inputs.TemperatureCelsius - config.ReferenceTemperatureCelsius);
            float dryingMultiplier = inputs.WeatherDryingModifier * inputs.Exposure.DryingMultiplier *
                (1f +
                 (inputs.WindSpeedMetersPerSecond * inputs.Exposure.WindExposure01 * config.WindDryingPerMeterPerSecond) +
                 (warmDegrees * config.WarmTemperatureDryingPerDegree) +
                 (inputs.SunIntensity01 * inputs.Exposure.SunExposure01 * config.SunDryingMultiplier));
            float commonDrying = config.BaseDryingPerSecond * dryingMultiplier * delta;

            var next = new WetnessState(
                Evolve(state.GroundWetness01, precipitation, config.GroundAccumulationPerSecond, commonDrying, delta),
                Evolve(state.RoadWetness01, precipitation, config.RoadAccumulationPerSecond, commonDrying * 1.12f, delta),
                Evolve(
                    state.PuddleAmount01,
                    precipitation,
                    config.PuddleAccumulationPerSecond,
                    commonDrying + (config.PuddleDrainagePerSecond * delta),
                    delta),
                Evolve(state.VegetationWetness01, precipitation, config.VegetationAccumulationPerSecond, commonDrying * 1.35f, delta));

            if (!next.Equals(state))
            {
                state = next;
                IncrementRevision();
            }

            return Outputs;
        }

        public void SetState(WetnessState value)
        {
            if (value.Equals(state))
            {
                return;
            }

            state = value;
            IncrementRevision();
        }

        public WetnessSnapshot CaptureSnapshot() => new WetnessSnapshot(config.ConfigId, state, revision);

        public void ValidateSnapshot(in WetnessSnapshot snapshot)
        {
            if (!string.Equals(snapshot.ConfigId, config.ConfigId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Wetness snapshot config ID does not match the active config.", nameof(snapshot));
            }

            if (snapshot.Revision == 0U)
            {
                throw new ArgumentException("Wetness snapshot revision must be non-zero.", nameof(snapshot));
            }
        }

        public void Restore(in WetnessSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            state = snapshot.State;
            revision = snapshot.Revision;
        }

        private static float Evolve(
            float current,
            float precipitation,
            float accumulationPerSecond,
            float dryingAmount,
            float deltaSeconds)
        {
            float dried = Math.Max(0f, current - dryingAmount);
            if (precipitation <= 0f || accumulationPerSecond <= 0f)
            {
                return dried;
            }

            double accumulationFraction = 1d - Math.Exp(-accumulationPerSecond * precipitation * deltaSeconds);
            return Math.Min(1f, dried + ((1f - dried) * (float)accumulationFraction));
        }

        private void IncrementRevision()
        {
            unchecked
            {
                revision++;
                if (revision == 0U)
                {
                    revision = 1U;
                }
            }
        }
    }
}
