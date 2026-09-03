using System;
using UnityEngine;

namespace MSC.Weather.System
{
    public readonly struct WeatherClimateContext
    {
        public WeatherClimateContext(
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
            NormalizedTimeOfDay01 = Mathf.Repeat(normalizedTimeOfDay01, 1f);
            TemperatureCelsius = temperatureCelsius;
            Humidity01 = humidity01;
        }

        public int Month { get; }
        public float NormalizedTimeOfDay01 { get; }
        public float TemperatureCelsius { get; }
        public float Humidity01 { get; }

        public static WeatherClimateContext FinnishSummerDefault =>
            new WeatherClimateContext(8, 0.5f, 15f, 0.62f);
    }

    [Serializable]
    public struct WeatherRandomSnapshot
    {
        public int Version;
        public long StateBits;
        public long IncrementBits;

        public bool IsValid => Version == 1 && IncrementBits != 0L;
    }

    [Serializable]
    public sealed class WeatherSchedulerSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId;
        public string PreviousPresetId;
        public string CurrentPresetId;
        public string TargetPresetId;
        public double FrontDurationGameSeconds;
        public double TransitionDurationGameSeconds;
        public double ElapsedGameSeconds;
        public long CompletedFrontCount;
        public WeatherRandomSnapshot Random;
        public string[] RecentPresetIds = Array.Empty<string>();
        public int HistoryCount;
        public int HistoryWriteIndex;
    }

    internal sealed class DeterministicWeatherRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong state;
        private ulong increment;

        public DeterministicWeatherRandom(ulong seed, ulong stream)
        {
            state = 0UL;
            increment = (stream << 1) | 1UL;
            NextUInt();
            state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong previous = state;
            state = unchecked(previous * Multiplier + increment);
            uint xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (xorShifted >> rotation) |
                   (xorShifted << ((-rotation) & 31));
        }

        public double NextDouble01() => NextUInt() / 4294967296d;

        public WeatherRandomSnapshot Capture() => new WeatherRandomSnapshot
        {
            Version = 1,
            StateBits = unchecked((long)state),
            IncrementBits = unchecked((long)increment),
        };

        public void Restore(in WeatherRandomSnapshot snapshot)
        {
            if (!snapshot.IsValid || (unchecked((ulong)snapshot.IncrementBits) & 1UL) == 0UL)
            {
                throw new ArgumentException("Weather RNG snapshot is invalid.");
            }

            state = unchecked((ulong)snapshot.StateBits);
            increment = unchecked((ulong)snapshot.IncrementBits);
        }
    }

    /// <summary>
    /// Seeded graph scheduler. Candidate weights combine authored probability,
    /// month/time eligibility, rarity, temperature, humidity and recent history.
    /// No allocations occur while advancing an unchanged catalog.
    /// </summary>
    public sealed class WeatherScheduler
    {
        private const int MaximumFrontsPerAdvance = 10000;

        private readonly WeatherClimateCatalog catalog;
        private readonly DeterministicWeatherRandom random;
        private readonly string[] history;

        private string previousPresetId;
        private string currentPresetId;
        private string targetPresetId;
        private double frontDurationGameSeconds;
        private double transitionDurationGameSeconds;
        private double elapsedGameSeconds;
        private long completedFrontCount;
        private int historyCount;
        private int historyWriteIndex;

        public WeatherScheduler(
            WeatherClimateCatalog climateCatalog,
            ulong seed,
            ulong stream,
            in WeatherClimateContext initialContext)
        {
            catalog = climateCatalog ?? throw new ArgumentNullException(nameof(climateCatalog));
            random = new DeterministicWeatherRandom(seed, stream);
            history = new string[catalog.HistoryLength];
            previousPresetId = catalog.InitialPresetId;
            currentPresetId = catalog.InitialPresetId;
            AddHistory(currentPresetId);
            BeginNextFront(initialContext);
        }

        public string ConfigId => catalog.ConfigId;
        public string PreviousPresetId => previousPresetId;
        public string CurrentPresetId => currentPresetId;
        public string TargetPresetId => targetPresetId;
        public double FrontDurationGameSeconds => frontDurationGameSeconds;
        public double TransitionDurationGameSeconds => transitionDurationGameSeconds;
        public double ElapsedGameSeconds => elapsedGameSeconds;
        public long CompletedFrontCount => completedFrontCount;
        public float TransitionProgress01
        {
            get
            {
                double transitionStart = Math.Max(
                    0d,
                    frontDurationGameSeconds - transitionDurationGameSeconds);
                if (elapsedGameSeconds <= transitionStart)
                {
                    return 0f;
                }

                if (transitionDurationGameSeconds <= 0d)
                {
                    return 1f;
                }

                return (float)Math.Min(
                    1d,
                    (elapsedGameSeconds - transitionStart) /
                    transitionDurationGameSeconds);
            }
        }

        public WeatherState CurrentState
        {
            get
            {
                WeatherPresetDefinition current = catalog.Get(currentPresetId);
                WeatherPresetDefinition target = catalog.Get(targetPresetId);
                return WeatherTransitionController.Blend(
                    current.TargetState,
                    target.TargetState,
                    TransitionProgress01,
                    target.TransitionCurves);
            }
        }

        public WeatherPresetDefinition CurrentPreset => catalog.Get(currentPresetId);
        public WeatherPresetDefinition TargetPreset => catalog.Get(targetPresetId);

        public void Advance(double deltaGameSeconds, in WeatherClimateContext context)
        {
            if (double.IsNaN(deltaGameSeconds) ||
                double.IsInfinity(deltaGameSeconds) ||
                deltaGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaGameSeconds));
            }

            double remaining = deltaGameSeconds;
            int completed = 0;
            while (remaining > 0d)
            {
                double available = frontDurationGameSeconds - elapsedGameSeconds;
                if (remaining < available)
                {
                    elapsedGameSeconds += remaining;
                    return;
                }

                remaining -= available;
                previousPresetId = currentPresetId;
                currentPresetId = targetPresetId;
                completedFrontCount = checked(completedFrontCount + 1L);
                AddHistory(currentPresetId);
                BeginNextFront(context);
                completed++;
                if (completed > MaximumFrontsPerAdvance)
                {
                    throw new InvalidOperationException(
                        "Weather scheduler exceeded its bounded catch-up budget.");
                }
            }
        }

        public WeatherSchedulerSnapshot CaptureSnapshot()
        {
            var historyCopy = new string[history.Length];
            Array.Copy(history, historyCopy, history.Length);
            return new WeatherSchedulerSnapshot
            {
                ConfigId = catalog.ConfigId,
                PreviousPresetId = previousPresetId,
                CurrentPresetId = currentPresetId,
                TargetPresetId = targetPresetId,
                FrontDurationGameSeconds = frontDurationGameSeconds,
                TransitionDurationGameSeconds = transitionDurationGameSeconds,
                ElapsedGameSeconds = elapsedGameSeconds,
                CompletedFrontCount = completedFrontCount,
                Random = random.Capture(),
                RecentPresetIds = historyCopy,
                HistoryCount = historyCount,
                HistoryWriteIndex = historyWriteIndex,
            };
        }

        public void ValidateSnapshot(WeatherSchedulerSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.SchemaVersion != WeatherSchedulerSnapshot.CurrentSchemaVersion ||
                !string.Equals(snapshot.ConfigId, catalog.ConfigId, StringComparison.Ordinal) ||
                !catalog.Contains(snapshot.PreviousPresetId) ||
                !catalog.Contains(snapshot.CurrentPresetId) ||
                !catalog.Contains(snapshot.TargetPresetId) ||
                !catalog.Get(snapshot.CurrentPresetId).AllowsSuccessor(snapshot.TargetPresetId) ||
                double.IsNaN(snapshot.FrontDurationGameSeconds) ||
                double.IsInfinity(snapshot.FrontDurationGameSeconds) ||
                snapshot.FrontDurationGameSeconds <= 0d ||
                double.IsNaN(snapshot.TransitionDurationGameSeconds) ||
                double.IsInfinity(snapshot.TransitionDurationGameSeconds) ||
                snapshot.TransitionDurationGameSeconds < 0d ||
                snapshot.TransitionDurationGameSeconds > snapshot.FrontDurationGameSeconds ||
                double.IsNaN(snapshot.ElapsedGameSeconds) ||
                double.IsInfinity(snapshot.ElapsedGameSeconds) ||
                snapshot.ElapsedGameSeconds < 0d ||
                snapshot.ElapsedGameSeconds > snapshot.FrontDurationGameSeconds ||
                snapshot.CompletedFrontCount < 0L ||
                !snapshot.Random.IsValid ||
                snapshot.RecentPresetIds == null ||
                snapshot.RecentPresetIds.Length != history.Length ||
                snapshot.HistoryCount < 0 || snapshot.HistoryCount > history.Length ||
                snapshot.HistoryWriteIndex < 0 ||
                snapshot.HistoryWriteIndex >= history.Length)
            {
                throw new ArgumentException("Weather scheduler snapshot is invalid.");
            }

            for (int index = 0; index < snapshot.HistoryCount; index++)
            {
                if (!catalog.Contains(snapshot.RecentPresetIds[index]))
                {
                    throw new ArgumentException(
                        "Weather scheduler history references an unknown preset.");
                }
            }
        }

        public void Restore(WeatherSchedulerSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            previousPresetId = snapshot.PreviousPresetId;
            currentPresetId = snapshot.CurrentPresetId;
            targetPresetId = snapshot.TargetPresetId;
            frontDurationGameSeconds = snapshot.FrontDurationGameSeconds;
            transitionDurationGameSeconds = snapshot.TransitionDurationGameSeconds;
            elapsedGameSeconds = snapshot.ElapsedGameSeconds;
            completedFrontCount = snapshot.CompletedFrontCount;
            historyCount = snapshot.HistoryCount;
            historyWriteIndex = snapshot.HistoryWriteIndex;
            Array.Copy(snapshot.RecentPresetIds, history, history.Length);
            random.Restore(snapshot.Random);
        }

        private void BeginNextFront(in WeatherClimateContext context)
        {
            WeatherPresetDefinition current = catalog.Get(currentPresetId);
            targetPresetId = SelectSuccessor(current, context);
            WeatherPresetDefinition target = catalog.Get(targetPresetId);
            frontDurationGameSeconds = RandomRange(target.DurationGameSeconds);
            transitionDurationGameSeconds = Math.Min(
                frontDurationGameSeconds,
                RandomRange(target.TransitionGameSeconds));
            elapsedGameSeconds = 0d;
        }

        private string SelectSuccessor(
            WeatherPresetDefinition current,
            in WeatherClimateContext context)
        {
            double total = 0d;
            for (int index = 0; index < current.AllowedSuccessorIds.Count; index++)
            {
                WeatherPresetDefinition candidate =
                    catalog.Get(current.AllowedSuccessorIds[index]);
                total += CalculateWeight(candidate, context, enforceAvailability: true);
            }

            bool enforceAvailability = total > 0d;
            if (!enforceAvailability)
            {
                for (int index = 0; index < current.AllowedSuccessorIds.Count; index++)
                {
                    total += CalculateWeight(
                        catalog.Get(current.AllowedSuccessorIds[index]),
                        context,
                        enforceAvailability: false);
                }
            }

            if (total <= 0d)
            {
                return current.AllowedSuccessorIds[0];
            }

            double choice = random.NextDouble01() * total;
            double cumulative = 0d;
            for (int index = 0; index < current.AllowedSuccessorIds.Count; index++)
            {
                string id = current.AllowedSuccessorIds[index];
                cumulative += CalculateWeight(
                    catalog.Get(id),
                    context,
                    enforceAvailability);
                if (choice <= cumulative)
                {
                    return id;
                }
            }

            return current.AllowedSuccessorIds[current.AllowedSuccessorIds.Count - 1];
        }

        private double CalculateWeight(
            WeatherPresetDefinition candidate,
            in WeatherClimateContext context,
            bool enforceAvailability)
        {
            if (enforceAvailability &&
                (!candidate.AllowsMonth(context.Month) ||
                 !candidate.AllowsTime(context.NormalizedTimeOfDay01)))
            {
                return 0d;
            }

            double weight = candidate.BaseProbability * RarityMultiplier(candidate.Rarity);
            float humidityDifference = Mathf.Abs(
                candidate.TargetState.Humidity01 - context.Humidity01);
            float temperatureDifference = Mathf.Abs(
                candidate.TargetState.TemperatureCelsius -
                context.TemperatureCelsius);
            weight *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(humidityDifference));
            weight *= Mathf.Lerp(
                1f,
                0.4f,
                Mathf.Clamp01(temperatureDifference / 18f));

            int age = HistoryAge(candidate.StableId);
            if (age == 0)
            {
                weight *= catalog.ImmediateRepeatMultiplier;
            }
            else if (age > 0)
            {
                float normalizedAge = history.Length <= 1
                    ? 1f
                    : Mathf.Clamp01(age / (float)(history.Length - 1));
                weight *= Mathf.Lerp(
                    catalog.RecentRepeatMultiplier,
                    1f,
                    normalizedAge);
            }

            if (candidate.TargetState.Precipitation01 > 0f)
            {
                weight *= Mathf.Lerp(0.55f, 1.35f, context.Humidity01);
            }

            return Math.Max(0d, weight);
        }

        private int HistoryAge(string id)
        {
            for (int age = 0; age < historyCount; age++)
            {
                int index = historyWriteIndex - 1 - age;
                if (index < 0)
                {
                    index += history.Length;
                }

                if (string.Equals(history[index], id, StringComparison.Ordinal))
                {
                    return age;
                }
            }

            return -1;
        }

        private void AddHistory(string id)
        {
            history[historyWriteIndex] = id;
            historyWriteIndex = (historyWriteIndex + 1) % history.Length;
            historyCount = Math.Min(history.Length, historyCount + 1);
        }

        private double RandomRange(Vector2 range) => range.y <= range.x
            ? range.x
            : range.x + (range.y - range.x) * random.NextDouble01();

        private static double RarityMultiplier(WeatherRarity rarity)
        {
            switch (rarity)
            {
                case WeatherRarity.Uncommon:
                    return 0.65d;
                case WeatherRarity.Rare:
                    return 0.32d;
                case WeatherRarity.Exceptional:
                    return 0.09d;
                default:
                    return 1d;
            }
        }
    }
}
