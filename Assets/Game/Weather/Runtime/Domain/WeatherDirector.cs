using System;
using System.Collections.Generic;

namespace MSC.Weather.Domain
{
    public readonly struct WeatherSnapshot
    {
        public WeatherSnapshot(
            double simulationSeconds,
            bool isScheduleFrozen,
            WeatherScheduleSnapshot schedule,
            WeatherOverride[] overrides,
            ulong nextOverrideSequence,
            uint revision)
        {
            SimulationSeconds = simulationSeconds;
            IsScheduleFrozen = isScheduleFrozen;
            Schedule = schedule;
            Overrides = overrides ?? Array.Empty<WeatherOverride>();
            NextOverrideSequence = nextOverrideSequence;
            Revision = revision;
        }

        public double SimulationSeconds { get; }

        public bool IsScheduleFrozen { get; }

        public WeatherScheduleSnapshot Schedule { get; }

        public WeatherOverride[] Overrides { get; }

        public ulong NextOverrideSequence { get; }

        public uint Revision { get; }
    }

    public sealed class WeatherDirector : IWeatherService
    {
        private readonly WeatherProfileCatalog catalog;
        private readonly WeatherSchedule schedule;
        private readonly List<WeatherOverride> overrides = new List<WeatherOverride>(4);

        private double simulationSeconds;
        private bool isScheduleFrozen;
        private ulong nextOverrideSequence = 1UL;
        private uint revision = 1U;

        public WeatherDirector(
            WeatherProfileCatalog catalog,
            WeatherSeed seed,
            WeatherStateId initialProfileId)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            schedule = new WeatherSchedule(catalog, seed, initialProfileId);
        }

        public string ConfigId => catalog.ConfigId;

        public double SimulationSeconds => simulationSeconds;

        public bool IsScheduleFrozen => isScheduleFrozen;

        public WeatherTimeline Timeline => schedule.Timeline;

        public WeatherState ScheduledState => schedule.CurrentState;

        public WeatherState CurrentState
        {
            get
            {
                WeatherOverride? activeOverride = GetActiveOverride();
                return activeOverride.HasValue
                    ? catalog.Get(activeOverride.Value.RequestedProfileId).TargetState
                    : schedule.CurrentState;
            }
        }

        public WeatherSnapshot Snapshot => CaptureSnapshot();

        public uint Revision => revision;

        public float RainIntensity
        {
            get
            {
                WeatherState state = CurrentState;
                return state.PrecipitationType == WeatherPrecipitationType.None
                    ? 0f
                    : state.PrecipitationIntensity01;
            }
        }

        public void Advance(double deltaSeconds)
        {
            if (!WeatherState.IsFinite(deltaSeconds) || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (deltaSeconds == 0d)
            {
                return;
            }

            double nextSimulationSeconds = simulationSeconds + deltaSeconds;
            if (!WeatherState.IsFinite(nextSimulationSeconds))
            {
                throw new OverflowException("Weather simulation time exceeded its finite range.");
            }

            WeatherState before = CurrentState;
            if (!isScheduleFrozen)
            {
                WeatherScheduleSnapshot checkpoint = schedule.CaptureSnapshot();
                try
                {
                    schedule.Advance(deltaSeconds);
                }
                catch
                {
                    schedule.Restore(checkpoint);
                    throw;
                }
            }

            simulationSeconds = nextSimulationSeconds;
            RemoveExpiredOverrides();
            if (!before.Equals(CurrentState))
            {
                IncrementRevision();
            }
        }

        public void SetScheduleFrozen(bool frozen)
        {
            if (isScheduleFrozen == frozen)
            {
                return;
            }

            isScheduleFrozen = frozen;
            IncrementRevision();
        }

        public ulong AddOverride(WeatherOverride weatherOverride)
        {
            if (!catalog.Contains(weatherOverride.RequestedProfileId))
            {
                throw new ArgumentException("Override references an unknown logical weather profile.", nameof(weatherOverride));
            }

            for (int index = 0; index < overrides.Count; index++)
            {
                if (string.Equals(overrides[index].OverrideId, weatherOverride.OverrideId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Override ID '{weatherOverride.OverrideId}' is already active.", nameof(weatherOverride));
                }
            }

            ulong assignedSequence = weatherOverride.Sequence;
            if (assignedSequence == 0UL)
            {
                assignedSequence = nextOverrideSequence;
            }

            if (assignedSequence >= nextOverrideSequence)
            {
                nextOverrideSequence = checked(assignedSequence + 1UL);
            }

            overrides.Add(weatherOverride.WithSequence(assignedSequence));
            IncrementRevision();
            return assignedSequence;
        }

        public bool RemoveOverride(string overrideId)
        {
            if (string.IsNullOrWhiteSpace(overrideId))
            {
                return false;
            }

            for (int index = 0; index < overrides.Count; index++)
            {
                if (!string.Equals(overrides[index].OverrideId, overrideId, StringComparison.Ordinal))
                {
                    continue;
                }

                overrides.RemoveAt(index);
                IncrementRevision();
                return true;
            }

            return false;
        }

        public bool TryGetActiveOverride(out WeatherOverride weatherOverride)
        {
            WeatherOverride? result = GetActiveOverride();
            if (result.HasValue)
            {
                weatherOverride = result.Value;
                return true;
            }

            weatherOverride = default;
            return false;
        }

        public WeatherSnapshot CaptureSnapshot()
        {
            var overrideCopy = new WeatherOverride[overrides.Count];
            overrides.CopyTo(overrideCopy);
            return new WeatherSnapshot(
                simulationSeconds,
                isScheduleFrozen,
                schedule.CaptureSnapshot(),
                overrideCopy,
                nextOverrideSequence,
                revision);
        }

        public void ValidateSnapshot(in WeatherSnapshot snapshot)
        {
            if (!WeatherState.IsFinite(snapshot.SimulationSeconds) || snapshot.SimulationSeconds < 0d)
            {
                throw new ArgumentException("Weather simulation time is invalid.", nameof(snapshot));
            }

            schedule.ValidateSnapshot(snapshot.Schedule);
            if (snapshot.NextOverrideSequence == 0UL)
            {
                throw new ArgumentException("Next override sequence must be non-zero.", nameof(snapshot));
            }

            if (snapshot.NextOverrideSequence == ulong.MaxValue || snapshot.Revision == 0U)
            {
                throw new ArgumentException("Weather snapshot sequence/revision cannot be advanced safely.", nameof(snapshot));
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            ulong maximumSequence = 0UL;
            WeatherOverride[] sourceOverrides = snapshot.Overrides ?? Array.Empty<WeatherOverride>();
            for (int index = 0; index < sourceOverrides.Length; index++)
            {
                WeatherOverride value = sourceOverrides[index];
                if (!catalog.Contains(value.RequestedProfileId))
                {
                    throw new ArgumentException("Weather snapshot contains an override with an unknown profile.", nameof(snapshot));
                }

                if (!seenIds.Add(value.OverrideId) || value.Sequence == 0UL)
                {
                    throw new ArgumentException("Weather snapshot contains duplicate IDs or invalid override sequences.", nameof(snapshot));
                }

                maximumSequence = Math.Max(maximumSequence, value.Sequence);
            }

            if (snapshot.NextOverrideSequence <= maximumSequence)
            {
                throw new ArgumentException("Next override sequence must be greater than all restored sequences.", nameof(snapshot));
            }
        }

        public void Restore(in WeatherSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);

            schedule.Restore(snapshot.Schedule);
            simulationSeconds = snapshot.SimulationSeconds;
            isScheduleFrozen = snapshot.IsScheduleFrozen;
            overrides.Clear();
            WeatherOverride[] sourceOverrides = snapshot.Overrides ?? Array.Empty<WeatherOverride>();
            for (int index = 0; index < sourceOverrides.Length; index++)
            {
                overrides.Add(sourceOverrides[index]);
            }

            nextOverrideSequence = snapshot.NextOverrideSequence;
            revision = snapshot.Revision;
        }

        public WeatherEnvironmentOutputs CreateEnvironmentOutputs(in WeatherEnvironmentOutputContext context) =>
            WeatherEnvironmentOutputs.Compose(CurrentState, revision, context);

        private WeatherOverride? GetActiveOverride()
        {
            WeatherOverride? best = null;
            for (int index = 0; index < overrides.Count; index++)
            {
                WeatherOverride candidate = overrides[index];
                if (!candidate.IsActiveAt(simulationSeconds))
                {
                    continue;
                }

                if (!best.HasValue || IsHigherPriority(candidate, best.Value))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsHigherPriority(in WeatherOverride candidate, in WeatherOverride current)
        {
            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            if (candidate.Sequence != current.Sequence)
            {
                return candidate.Sequence > current.Sequence;
            }

            return string.Compare(candidate.OverrideId, current.OverrideId, StringComparison.Ordinal) > 0;
        }

        private void RemoveExpiredOverrides()
        {
            for (int index = overrides.Count - 1; index >= 0; index--)
            {
                if (simulationSeconds >= overrides[index].EndSimulationSeconds)
                {
                    overrides.RemoveAt(index);
                }
            }
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
