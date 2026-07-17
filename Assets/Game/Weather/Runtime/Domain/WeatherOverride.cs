using System;

namespace MSC.Weather.Domain
{
    public enum WeatherOverrideSerializationPolicy
    {
        Transient = 0,
        Save = 1,
    }

    /// <summary>
    /// A scoped logical override. Schedule state continues underneath it.
    /// </summary>
    public readonly struct WeatherOverride
    {
        public WeatherOverride(
            string overrideId,
            string owner,
            string reason,
            int priority,
            double startSimulationSeconds,
            double endSimulationSeconds,
            WeatherStateId requestedProfileId,
            WeatherOverrideSerializationPolicy serializationPolicy,
            ulong sequence = 0UL)
        {
            if (string.IsNullOrWhiteSpace(overrideId))
            {
                throw new ArgumentException("Override ID is required.", nameof(overrideId));
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("Override owner is required.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Override reason is required.", nameof(reason));
            }

            WeatherState.ValidateFinite(startSimulationSeconds, nameof(startSimulationSeconds));
            WeatherState.ValidateFinite(endSimulationSeconds, nameof(endSimulationSeconds));
            if (startSimulationSeconds < 0d || endSimulationSeconds <= startSimulationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(endSimulationSeconds));
            }

            if (requestedProfileId.IsEmpty)
            {
                throw new ArgumentException("Override requires a logical weather profile.", nameof(requestedProfileId));
            }

            if (!Enum.IsDefined(typeof(WeatherOverrideSerializationPolicy), serializationPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(serializationPolicy));
            }

            OverrideId = overrideId;
            Owner = owner;
            Reason = reason;
            Priority = priority;
            StartSimulationSeconds = startSimulationSeconds;
            EndSimulationSeconds = endSimulationSeconds;
            RequestedProfileId = requestedProfileId;
            SerializationPolicy = serializationPolicy;
            Sequence = sequence;
        }

        public string OverrideId { get; }

        public string Owner { get; }

        public string Reason { get; }

        public int Priority { get; }

        public double StartSimulationSeconds { get; }

        public double EndSimulationSeconds { get; }

        public WeatherStateId RequestedProfileId { get; }

        public WeatherOverrideSerializationPolicy SerializationPolicy { get; }

        /// <summary>Monotonic project-owned tie breaker. Higher sequence wins equal priority.</summary>
        public ulong Sequence { get; }

        public bool IsActiveAt(double simulationSeconds) =>
            simulationSeconds >= StartSimulationSeconds && simulationSeconds < EndSimulationSeconds;

        public WeatherOverride WithSequence(ulong sequence) => new WeatherOverride(
            OverrideId,
            Owner,
            Reason,
            Priority,
            StartSimulationSeconds,
            EndSimulationSeconds,
            RequestedProfileId,
            SerializationPolicy,
            sequence);
    }
}
