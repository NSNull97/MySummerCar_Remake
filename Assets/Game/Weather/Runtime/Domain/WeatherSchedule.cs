using System;

namespace MSC.Weather.Domain
{
    public readonly struct WeatherScheduleContext
    {
        public WeatherScheduleContext(
            int month,
            float normalizedTimeOfDay01,
            float temperatureCelsius,
            float humidity01)
        {
            if (month < 1 || month > 12 ||
                !WeatherState.IsFinite(normalizedTimeOfDay01) ||
                !WeatherState.IsFinite(temperatureCelsius) ||
                !WeatherState.IsFinite(humidity01) ||
                humidity01 < 0f || humidity01 > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(month));
            }

            Month = month;
            NormalizedTimeOfDay01 = NormalizeTime(normalizedTimeOfDay01);
            TemperatureCelsius = temperatureCelsius;
            Humidity01 = humidity01;
        }

        public int Month { get; }

        public float NormalizedTimeOfDay01 { get; }

        public float TemperatureCelsius { get; }

        public float Humidity01 { get; }

        public static WeatherScheduleContext Default =>
            new WeatherScheduleContext(8, 0.5f, 15f, 0.62f);

        private static float NormalizeTime(float value)
        {
            double repeated = value - Math.Floor(value);
            return (float)(repeated < 0d ? repeated + 1d : repeated);
        }
    }

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
            : this(
                configId,
                currentProfileId,
                targetProfileId,
                frontDurationSeconds,
                transitionDurationSeconds,
                elapsedSeconds,
                cursor,
                randomState,
                currentProfileId,
                Array.Empty<WeatherStateId>(),
                0,
                0)
        {
        }

        public WeatherScheduleSnapshot(
            string configId,
            WeatherStateId currentProfileId,
            WeatherStateId targetProfileId,
            double frontDurationSeconds,
            double transitionDurationSeconds,
            double elapsedSeconds,
            long cursor,
            WeatherRandomState randomState,
            WeatherStateId previousProfileId,
            WeatherStateId[] recentProfileIds,
            int historyCount,
            int historyWriteIndex)
        {
            ConfigId = configId;
            CurrentProfileId = currentProfileId;
            TargetProfileId = targetProfileId;
            FrontDurationSeconds = frontDurationSeconds;
            TransitionDurationSeconds = transitionDurationSeconds;
            ElapsedSeconds = elapsedSeconds;
            Cursor = cursor;
            RandomState = randomState;
            PreviousProfileId = previousProfileId;
            RecentProfileIds = recentProfileIds ?? Array.Empty<WeatherStateId>();
            HistoryCount = historyCount;
            HistoryWriteIndex = historyWriteIndex;
        }

        public string ConfigId { get; }

        public WeatherStateId CurrentProfileId { get; }

        public WeatherStateId TargetProfileId { get; }

        public double FrontDurationSeconds { get; }

        public double TransitionDurationSeconds { get; }

        public double ElapsedSeconds { get; }

        public long Cursor { get; }

        public WeatherRandomState RandomState { get; }

        public WeatherStateId PreviousProfileId { get; }

        public WeatherStateId[] RecentProfileIds { get; }

        public int HistoryCount { get; }

        public int HistoryWriteIndex { get; }

        public bool HasHistory => RecentProfileIds != null &&
                                  RecentProfileIds.Length > 0;
    }

    /// <summary>
    /// A seeded continuous front schedule. It only chooses among declared successor edges.
    /// </summary>
    public sealed class WeatherSchedule
    {
        private const int MaximumFrontsPerAdvance = 100000;

        private readonly WeatherProfileCatalog catalog;
        private readonly WeatherRandom random;
        private readonly WeatherStateId[] history;

        private WeatherStateId previousProfileId;
        private WeatherStateId currentProfileId;
        private WeatherStateId targetProfileId;
        private double frontDurationSeconds;
        private double transitionDurationSeconds;
        private double elapsedSeconds;
        private long cursor;
        private int historyCount;
        private int historyWriteIndex;

        public WeatherSchedule(
            WeatherProfileCatalog catalog,
            WeatherSeed seed,
            WeatherStateId initialProfileId)
            : this(catalog, seed, initialProfileId, WeatherScheduleContext.Default)
        {
        }

        public WeatherSchedule(
            WeatherProfileCatalog catalog,
            WeatherSeed seed,
            WeatherStateId initialProfileId,
            in WeatherScheduleContext initialContext)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!catalog.Contains(initialProfileId))
            {
                throw new ArgumentException("Initial weather profile is not present in the catalog.", nameof(initialProfileId));
            }

            random = new WeatherRandom(seed);
            history = new WeatherStateId[catalog.HistoryLength];
            previousProfileId = initialProfileId;
            currentProfileId = initialProfileId;
            cursor = 0;
            AddHistory(initialProfileId);
            BeginNextFront(initialContext);
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
            Advance(deltaSeconds, WeatherScheduleContext.Default);
        }

        public void Advance(
            double deltaSeconds,
            in WeatherScheduleContext context)
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
                previousProfileId = currentProfileId;
                currentProfileId = targetProfileId;
                checked
                {
                    cursor++;
                }

                AddHistory(currentProfileId);
                BeginNextFront(context);
                completed++;
                if (completed > MaximumFrontsPerAdvance)
                {
                    throw new InvalidOperationException("Weather schedule advance exceeded its safety bound.");
                }
            }
        }

        public WeatherScheduleSnapshot CaptureSnapshot()
        {
            var historyCopy = new WeatherStateId[history.Length];
            return CaptureSnapshot(historyCopy);
        }

        internal WeatherScheduleSnapshot CaptureSnapshot(
            WeatherStateId[] historyBuffer)
        {
            if (historyBuffer == null || historyBuffer.Length != history.Length)
            {
                throw new ArgumentException(
                    "Weather schedule checkpoint buffer has the wrong size.",
                    nameof(historyBuffer));
            }

            Array.Copy(history, historyBuffer, history.Length);
            return new WeatherScheduleSnapshot(
                catalog.ConfigId,
                currentProfileId,
                targetProfileId,
                frontDurationSeconds,
                transitionDurationSeconds,
                elapsedSeconds,
                cursor,
                random.CaptureState(),
                previousProfileId,
                historyBuffer,
                historyCount,
                historyWriteIndex);
        }

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

            if (!catalog.AllowsSnapshotTransition(
                    snapshot.CurrentProfileId,
                    snapshot.TargetProfileId))
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

            if (snapshot.HasHistory)
            {
                if (snapshot.PreviousProfileId.IsEmpty ||
                    !catalog.Contains(snapshot.PreviousProfileId) ||
                    snapshot.RecentProfileIds.Length != history.Length ||
                    snapshot.HistoryCount < 0 ||
                    snapshot.HistoryCount > history.Length ||
                    snapshot.HistoryWriteIndex < 0 ||
                    snapshot.HistoryWriteIndex >= history.Length)
                {
                    throw new ArgumentException(
                        "Weather schedule history metadata is invalid.",
                        nameof(snapshot));
                }

                for (int index = 0; index < snapshot.HistoryCount; index++)
                {
                    if (snapshot.RecentProfileIds[index].IsEmpty ||
                        !catalog.Contains(snapshot.RecentProfileIds[index]))
                    {
                        throw new ArgumentException(
                            "Weather schedule history references an unknown profile.",
                            nameof(snapshot));
                    }
                }
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
            Array.Clear(history, 0, history.Length);
            if (snapshot.HasHistory)
            {
                previousProfileId = snapshot.PreviousProfileId;
                Array.Copy(snapshot.RecentProfileIds, history, history.Length);
                historyCount = snapshot.HistoryCount;
                historyWriteIndex = snapshot.HistoryWriteIndex;
            }
            else
            {
                // v1 saves did not persist recent history. Preserve the active
                // front and RNG, then seed the repeat guard from the current
                // profile so all v2 saves reproduce exactly from this point on.
                previousProfileId = currentProfileId;
                historyCount = 0;
                historyWriteIndex = 0;
                AddHistory(currentProfileId);
            }
        }

        private void BeginNextFront(in WeatherScheduleContext context)
        {
            WeatherProfile current = catalog.Get(currentProfileId);
            targetProfileId = catalog.ClimateAwareScheduling
                ? SelectSuccessor(current, context)
                : current.AllowedSuccessors[
                    random.NextInt(current.AllowedSuccessors.Count)];

            WeatherProfile target = catalog.Get(targetProfileId);
            frontDurationSeconds = RandomRange(target.MinimumDurationSeconds, target.MaximumDurationSeconds);
            transitionDurationSeconds = RandomRange(target.MinimumTransitionSeconds, target.MaximumTransitionSeconds);
            if (transitionDurationSeconds > frontDurationSeconds)
            {
                transitionDurationSeconds = frontDurationSeconds;
            }

            elapsedSeconds = 0d;
        }

        private WeatherStateId SelectSuccessor(
            WeatherProfile current,
            in WeatherScheduleContext context)
        {
            double total = 0d;
            for (int index = 0; index < current.AllowedSuccessors.Count; index++)
            {
                WeatherProfile candidate = catalog.Get(
                    current.AllowedSuccessors[index]);
                total += CalculateWeight(candidate, context, true);
            }

            bool enforceAvailability = total > 0d;
            if (!enforceAvailability)
            {
                for (int index = 0;
                     index < current.AllowedSuccessors.Count;
                     index++)
                {
                    total += CalculateWeight(
                        catalog.Get(current.AllowedSuccessors[index]),
                        context,
                        false);
                }
            }

            if (total <= 0d)
            {
                return current.AllowedSuccessors[0];
            }

            double choice = random.NextDouble01() * total;
            double cumulative = 0d;
            for (int index = 0; index < current.AllowedSuccessors.Count; index++)
            {
                WeatherStateId id = current.AllowedSuccessors[index];
                cumulative += CalculateWeight(
                    catalog.Get(id),
                    context,
                    enforceAvailability);
                if (choice <= cumulative)
                {
                    return id;
                }
            }

            return current.AllowedSuccessors[current.AllowedSuccessors.Count - 1];
        }

        private double CalculateWeight(
            WeatherProfile candidate,
            in WeatherScheduleContext context,
            bool enforceAvailability)
        {
            WeatherSelectionSettings selection = candidate.Selection;
            if (enforceAvailability &&
                (!selection.AllowsMonth(context.Month) ||
                 !selection.AllowsTime(context.NormalizedTimeOfDay01)))
            {
                return 0d;
            }

            double weight = selection.BaseProbability *
                            selection.RarityMultiplier;
            float humidityDifference = Math.Abs(
                selection.PreferredHumidity01 - context.Humidity01);
            float temperatureDifference = Math.Abs(
                selection.PreferredTemperatureCelsius -
                context.TemperatureCelsius);
            weight *= Lerp(1f, 0.35f, Clamp01(humidityDifference));
            weight *= Lerp(
                1f,
                0.4f,
                Clamp01(temperatureDifference / 18f));

            int age = HistoryAge(candidate.Id);
            if (age == 0)
            {
                weight *= catalog.ImmediateRepeatMultiplier;
            }
            else if (age > 0)
            {
                float normalizedAge = history.Length <= 1
                    ? 1f
                    : Clamp01(age / (float)(history.Length - 1));
                weight *= Lerp(
                    catalog.RecentRepeatMultiplier,
                    1f,
                    normalizedAge);
            }

            if (selection.IsPrecipitation)
            {
                weight *= Lerp(0.55f, 1.35f, context.Humidity01);
            }

            return Math.Max(0d, weight);
        }

        private int HistoryAge(WeatherStateId id)
        {
            for (int age = 0; age < historyCount; age++)
            {
                int index = historyWriteIndex - 1 - age;
                if (index < 0)
                {
                    index += history.Length;
                }

                if (history[index] == id)
                {
                    return age;
                }
            }

            return -1;
        }

        private void AddHistory(WeatherStateId id)
        {
            history[historyWriteIndex] = id;
            historyWriteIndex = (historyWriteIndex + 1) % history.Length;
            historyCount = Math.Min(history.Length, historyCount + 1);
        }

        private static float Clamp01(float value) =>
            value <= 0f ? 0f : value >= 1f ? 1f : value;

        private static float Lerp(float from, float to, float t) =>
            from + (to - from) * Clamp01(t);

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
