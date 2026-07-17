using System;

namespace MSC.Weather.Domain
{
    public readonly struct WeatherFront
    {
        public WeatherFront(
            WeatherStateId from,
            WeatherStateId to,
            double durationSeconds,
            double transitionDurationSeconds,
            long cursor)
        {
            if (from.IsEmpty || to.IsEmpty)
            {
                throw new ArgumentException("A weather front requires stable source and target IDs.");
            }

            ValidateDurations(durationSeconds, transitionDurationSeconds);
            if (cursor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cursor));
            }

            From = from;
            To = to;
            DurationSeconds = durationSeconds;
            TransitionDurationSeconds = transitionDurationSeconds;
            Cursor = cursor;
        }

        public WeatherStateId From { get; }

        public WeatherStateId To { get; }

        public double DurationSeconds { get; }

        public double TransitionDurationSeconds { get; }

        public long Cursor { get; }

        internal static void ValidateDurations(double durationSeconds, double transitionDurationSeconds)
        {
            if (!WeatherState.IsFinite(durationSeconds) || durationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            if (!WeatherState.IsFinite(transitionDurationSeconds) ||
                transitionDurationSeconds < 0d ||
                transitionDurationSeconds > durationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(transitionDurationSeconds));
            }
        }
    }

    public readonly struct WeatherTransition
    {
        public WeatherTransition(WeatherFront front, double elapsedSeconds)
        {
            if (!WeatherState.IsFinite(elapsedSeconds) || elapsedSeconds < 0d || elapsedSeconds > front.DurationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            Front = front;
            ElapsedSeconds = elapsedSeconds;
        }

        public WeatherFront Front { get; }

        public double ElapsedSeconds { get; }

        public float Progress01
        {
            get
            {
                if (Front.TransitionDurationSeconds <= 0d)
                {
                    return 1f;
                }

                return (float)Math.Min(1d, ElapsedSeconds / Front.TransitionDurationSeconds);
            }
        }
    }

    public readonly struct WeatherTimeline
    {
        public WeatherTimeline(WeatherTransition transition, long completedFrontCount)
        {
            if (completedFrontCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedFrontCount));
            }

            Transition = transition;
            CompletedFrontCount = completedFrontCount;
        }

        public WeatherTransition Transition { get; }

        public long CompletedFrontCount { get; }
    }

    public readonly struct WeatherScheduleSnapshot
    {
        public WeatherScheduleSnapshot(
            string configId,
            WeatherStateId currentProfileId,
            WeatherStateId targetProfileId,
            double frontDurationSeconds,
            double transitionDurationSeconds,
            double elapsedSeconds,
            long cursor,
            WeatherRandomState randomState)
        {
            ConfigId = configId;
            CurrentProfileId = currentProfileId;
            TargetProfileId = targetProfileId;
            FrontDurationSeconds = frontDurationSeconds;
            TransitionDurationSeconds = transitionDurationSeconds;
            ElapsedSeconds = elapsedSeconds;
            Cursor = cursor;
            RandomState = randomState;
        }

        public string ConfigId { get; }

        public WeatherStateId CurrentProfileId { get; }

        public WeatherStateId TargetProfileId { get; }

        public double FrontDurationSeconds { get; }

        public double TransitionDurationSeconds { get; }

        public double ElapsedSeconds { get; }

        public long Cursor { get; }

        public WeatherRandomState RandomState { get; }
    }

    /// <summary>
    /// A seeded continuous front schedule. It only chooses among declared successor edges.
    /// </summary>
    public sealed class WeatherSchedule
    {
        private const int MaximumFrontsPerAdvance = 100000;

        private readonly WeatherProfileCatalog catalog;
        private readonly WeatherRandom random;

        private WeatherStateId currentProfileId;
        private WeatherStateId targetProfileId;
        private double frontDurationSeconds;
        private double transitionDurationSeconds;
        private double elapsedSeconds;
        private long cursor;

        public WeatherSchedule(
            WeatherProfileCatalog catalog,
            WeatherSeed seed,
            WeatherStateId initialProfileId)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!catalog.Contains(initialProfileId))
            {
                throw new ArgumentException("Initial weather profile is not present in the catalog.", nameof(initialProfileId));
            }

            random = new WeatherRandom(seed);
            currentProfileId = initialProfileId;
            cursor = 0;
            BeginNextFront();
        }

        public string ConfigId => catalog.ConfigId;

        public WeatherFront CurrentFront => new WeatherFront(
            currentProfileId,
            targetProfileId,
            frontDurationSeconds,
            transitionDurationSeconds,
            cursor);

        public WeatherTransition CurrentTransition => new WeatherTransition(CurrentFront, elapsedSeconds);

        public WeatherTimeline Timeline => new WeatherTimeline(CurrentTransition, cursor);

        public WeatherState CurrentState
        {
            get
            {
                WeatherState from = catalog.Get(currentProfileId).TargetState;
                WeatherState to = catalog.Get(targetProfileId).TargetState;
                return WeatherState.Lerp(from, to, CurrentTransition.Progress01);
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

            double remaining = deltaSeconds;
            int completed = 0;
            while (remaining > 0d)
            {
                double available = frontDurationSeconds - elapsedSeconds;
                if (remaining < available)
                {
                    elapsedSeconds += remaining;
                    return;
                }

                remaining -= available;
                currentProfileId = targetProfileId;
                checked
                {
                    cursor++;
                }

                BeginNextFront();
                completed++;
                if (completed > MaximumFrontsPerAdvance)
                {
                    throw new InvalidOperationException("Weather schedule advance exceeded its safety bound.");
                }
            }
        }

        public WeatherScheduleSnapshot CaptureSnapshot() => new WeatherScheduleSnapshot(
            catalog.ConfigId,
            currentProfileId,
            targetProfileId,
            frontDurationSeconds,
            transitionDurationSeconds,
            elapsedSeconds,
            cursor,
            random.CaptureState());

        public void ValidateSnapshot(in WeatherScheduleSnapshot snapshot)
        {
            if (!string.Equals(snapshot.ConfigId, catalog.ConfigId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Weather schedule config ID does not match the active catalog.", nameof(snapshot));
            }

            if (!catalog.Contains(snapshot.CurrentProfileId) || !catalog.Contains(snapshot.TargetProfileId))
            {
                throw new ArgumentException("Weather schedule snapshot references an unknown profile.", nameof(snapshot));
            }

            if (!catalog.Get(snapshot.CurrentProfileId).AllowsSuccessor(snapshot.TargetProfileId))
            {
                throw new ArgumentException("Weather schedule snapshot contains a forbidden transition.", nameof(snapshot));
            }

            WeatherFront.ValidateDurations(snapshot.FrontDurationSeconds, snapshot.TransitionDurationSeconds);
            if (!WeatherState.IsFinite(snapshot.ElapsedSeconds) ||
                snapshot.ElapsedSeconds < 0d ||
                snapshot.ElapsedSeconds > snapshot.FrontDurationSeconds)
            {
                throw new ArgumentException("Weather schedule elapsed time is invalid.", nameof(snapshot));
            }

            if (snapshot.Cursor < 0)
            {
                throw new ArgumentException("Weather schedule cursor must not be negative.", nameof(snapshot));
            }

            if (!snapshot.RandomState.IsValid)
            {
                throw new ArgumentException("Weather schedule RNG state is invalid.", nameof(snapshot));
            }
        }

        public void Restore(in WeatherScheduleSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);

            currentProfileId = snapshot.CurrentProfileId;
            targetProfileId = snapshot.TargetProfileId;
            frontDurationSeconds = snapshot.FrontDurationSeconds;
            transitionDurationSeconds = snapshot.TransitionDurationSeconds;
            elapsedSeconds = snapshot.ElapsedSeconds;
            cursor = snapshot.Cursor;
            random.RestoreState(snapshot.RandomState);
        }

        private void BeginNextFront()
        {
            WeatherProfile current = catalog.Get(currentProfileId);
            int successorIndex = random.NextInt(current.AllowedSuccessors.Count);
            targetProfileId = current.AllowedSuccessors[successorIndex];

            WeatherProfile target = catalog.Get(targetProfileId);
            frontDurationSeconds = RandomRange(target.MinimumDurationSeconds, target.MaximumDurationSeconds);
            transitionDurationSeconds = RandomRange(target.MinimumTransitionSeconds, target.MaximumTransitionSeconds);
            if (transitionDurationSeconds > frontDurationSeconds)
            {
                transitionDurationSeconds = frontDurationSeconds;
            }

            elapsedSeconds = 0d;
        }

        private double RandomRange(float minimum, float maximum)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }

            return minimum + ((maximum - minimum) * random.NextDouble01());
        }
    }
}
