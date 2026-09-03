using System;
using System.Collections.Generic;

namespace MSC.Weather.Domain
{
    public enum WeatherProfileRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Exceptional = 3,
    }

    /// <summary>
    /// Optional climate-aware selection metadata. It stays in the project-owned
    /// domain so the authoritative scheduler does not reference presentation or
    /// Enviro types.
    /// </summary>
    public readonly struct WeatherSelectionSettings
    {
        public WeatherSelectionSettings(
            float baseProbability,
            WeatherProfileRarity rarity,
            int allowedMonthMask,
            float allowedTimeStart01,
            float allowedTimeEnd01,
            float preferredTemperatureCelsius,
            float preferredHumidity01,
            bool isPrecipitation)
        {
            if (!WeatherState.IsFinite(baseProbability) || baseProbability <= 0f ||
                allowedMonthMask < 0 || allowedMonthMask > 0xFFF ||
                !WeatherState.IsFinite(allowedTimeStart01) ||
                !WeatherState.IsFinite(allowedTimeEnd01) ||
                !WeatherState.IsFinite(preferredTemperatureCelsius) ||
                !WeatherState.IsFinite(preferredHumidity01) ||
                preferredHumidity01 < 0f || preferredHumidity01 > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(baseProbability));
            }

            BaseProbability = baseProbability;
            Rarity = rarity;
            AllowedMonthMask = allowedMonthMask;
            AllowedTimeStart01 = NormalizeTime(allowedTimeStart01);
            AllowedTimeEnd01 = allowedTimeEnd01 >= 1f
                ? 1f
                : NormalizeTime(allowedTimeEnd01);
            PreferredTemperatureCelsius = preferredTemperatureCelsius;
            PreferredHumidity01 = preferredHumidity01;
            IsPrecipitation = isPrecipitation;
        }

        public float BaseProbability { get; }

        public WeatherProfileRarity Rarity { get; }

        public int AllowedMonthMask { get; }

        public float AllowedTimeStart01 { get; }

        public float AllowedTimeEnd01 { get; }

        public float PreferredTemperatureCelsius { get; }

        public float PreferredHumidity01 { get; }

        public bool IsPrecipitation { get; }

        public bool AllowsMonth(int month) => month >= 1 && month <= 12 &&
            (AllowedMonthMask & (1 << (month - 1))) != 0;

        public bool AllowsTime(float normalizedTime01)
        {
            float value = NormalizeTime(normalizedTime01);
            if (AllowedTimeStart01 <= 0.000001f &&
                AllowedTimeEnd01 >= 0.999999f)
            {
                return true;
            }

            return AllowedTimeStart01 <= AllowedTimeEnd01
                ? value >= AllowedTimeStart01 && value <= AllowedTimeEnd01
                : value >= AllowedTimeStart01 || value <= AllowedTimeEnd01;
        }

        public double RarityMultiplier
        {
            get
            {
                switch (Rarity)
                {
                    case WeatherProfileRarity.Uncommon:
                        return 0.65d;
                    case WeatherProfileRarity.Rare:
                        return 0.32d;
                    case WeatherProfileRarity.Exceptional:
                        return 0.09d;
                    default:
                        return 1d;
                }
            }
        }

        public static WeatherSelectionSettings Uniform(in WeatherState state) =>
            new WeatherSelectionSettings(
                1f,
                WeatherProfileRarity.Common,
                0xFFF,
                0f,
                1f,
                state.TemperatureCelsius,
                Math.Min(1f, Math.Max(
                    state.CloudCoverage01 * 0.8f,
                    state.PrecipitationIntensity01)),
                state.PrecipitationType != WeatherPrecipitationType.None);

        private static float NormalizeTime(float value)
        {
            double repeated = value - Math.Floor(value);
            return (float)(repeated < 0d ? repeated + 1d : repeated);
        }
    }

    public sealed class WeatherProfile
    {
        private readonly WeatherStateId[] allowedPredecessors;
        private readonly WeatherStateId[] allowedSuccessors;

        public WeatherProfile(
            WeatherState targetState,
            float minimumDurationSeconds,
            float maximumDurationSeconds,
            float minimumTransitionSeconds,
            float maximumTransitionSeconds,
            WeatherStateId[] allowedPredecessors,
            WeatherStateId[] allowedSuccessors,
            string provenance)
            : this(
                targetState,
                minimumDurationSeconds,
                maximumDurationSeconds,
                minimumTransitionSeconds,
                maximumTransitionSeconds,
                allowedPredecessors,
                allowedSuccessors,
                provenance,
                WeatherSelectionSettings.Uniform(targetState))
        {
        }

        public WeatherProfile(
            WeatherState targetState,
            float minimumDurationSeconds,
            float maximumDurationSeconds,
            float minimumTransitionSeconds,
            float maximumTransitionSeconds,
            WeatherStateId[] allowedPredecessors,
            WeatherStateId[] allowedSuccessors,
            string provenance,
            WeatherSelectionSettings selection)
        {
            WeatherState.ValidateFinitePositive(minimumDurationSeconds, nameof(minimumDurationSeconds));
            WeatherState.ValidateFinitePositive(maximumDurationSeconds, nameof(maximumDurationSeconds));
            WeatherState.ValidateFiniteNonNegative(minimumTransitionSeconds, nameof(minimumTransitionSeconds));
            WeatherState.ValidateFiniteNonNegative(maximumTransitionSeconds, nameof(maximumTransitionSeconds));
            if (maximumDurationSeconds < minimumDurationSeconds)
            {
                throw new ArgumentException("Maximum duration must not be shorter than minimum duration.");
            }

            if (maximumTransitionSeconds < minimumTransitionSeconds || maximumTransitionSeconds > maximumDurationSeconds)
            {
                throw new ArgumentException("Transition range must be ordered and fit inside the front duration.");
            }

            if (allowedSuccessors == null || allowedSuccessors.Length == 0)
            {
                throw new ArgumentException("A weather profile must declare at least one successor.", nameof(allowedSuccessors));
            }

            if (string.IsNullOrWhiteSpace(provenance))
            {
                throw new ArgumentException("Profile provenance is required.", nameof(provenance));
            }

            TargetState = targetState;
            MinimumDurationSeconds = minimumDurationSeconds;
            MaximumDurationSeconds = maximumDurationSeconds;
            MinimumTransitionSeconds = minimumTransitionSeconds;
            MaximumTransitionSeconds = maximumTransitionSeconds;
            this.allowedPredecessors = CopyIds(allowedPredecessors);
            this.allowedSuccessors = CopyIds(allowedSuccessors);
            Provenance = provenance;
            Selection = selection;
        }

        public WeatherStateId Id => TargetState.Id;

        public WeatherState TargetState { get; }

        public float MinimumDurationSeconds { get; }

        public float MaximumDurationSeconds { get; }

        public float MinimumTransitionSeconds { get; }

        public float MaximumTransitionSeconds { get; }

        public IReadOnlyList<WeatherStateId> AllowedPredecessors => allowedPredecessors;

        public IReadOnlyList<WeatherStateId> AllowedSuccessors => allowedSuccessors;

        public string Provenance { get; }

        public WeatherSelectionSettings Selection { get; }

        public bool AllowsSuccessor(WeatherStateId id) => Contains(allowedSuccessors, id);

        public bool AllowsPredecessor(WeatherStateId id) => Contains(allowedPredecessors, id);

        private static WeatherStateId[] CopyIds(WeatherStateId[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<WeatherStateId>();
            }

            var result = new WeatherStateId[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index].IsEmpty)
                {
                    throw new ArgumentException("Allowed weather IDs must not be empty.", nameof(source));
                }

                result[index] = source[index];
            }

            return result;
        }

        private static bool Contains(WeatherStateId[] values, WeatherStateId id)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == id)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class WeatherProfileCatalog
    {
        public const string RemakeDesignTargetConfigId = "weather.profiles.remake_design_target.v1";
        public const string RemakeDesignTargetProvenance = "RemakeDesignTarget";

        private readonly Dictionary<WeatherStateId, WeatherProfile> profiles;
        private readonly WeatherProfile[] orderedProfiles;

        public WeatherProfileCatalog(string configId, IReadOnlyList<WeatherProfile> sourceProfiles)
            : this(configId, sourceProfiles, false, 1, 1f, 1f)
        {
        }

        public WeatherProfileCatalog(
            string configId,
            IReadOnlyList<WeatherProfile> sourceProfiles,
            bool climateAwareScheduling,
            int historyLength,
            float immediateRepeatMultiplier,
            float recentRepeatMultiplier)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("A weather profile catalog requires a stable config ID.", nameof(configId));
            }

            if (sourceProfiles == null || sourceProfiles.Count == 0)
            {
                throw new ArgumentException("At least one weather profile is required.", nameof(sourceProfiles));
            }

            if (historyLength < 1 || historyLength > 16 ||
                !WeatherState.IsFinite(immediateRepeatMultiplier) ||
                !WeatherState.IsFinite(recentRepeatMultiplier) ||
                immediateRepeatMultiplier < 0f || immediateRepeatMultiplier > 1f ||
                recentRepeatMultiplier < 0f || recentRepeatMultiplier > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(historyLength));
            }

            ConfigId = configId;
            ClimateAwareScheduling = climateAwareScheduling;
            HistoryLength = historyLength;
            ImmediateRepeatMultiplier = immediateRepeatMultiplier;
            RecentRepeatMultiplier = recentRepeatMultiplier;
            profiles = new Dictionary<WeatherStateId, WeatherProfile>(sourceProfiles.Count);
            orderedProfiles = new WeatherProfile[sourceProfiles.Count];
            for (int index = 0; index < sourceProfiles.Count; index++)
            {
                WeatherProfile profile = sourceProfiles[index] ??
                    throw new ArgumentException("Weather profile entries must not be null.", nameof(sourceProfiles));
                if (profiles.ContainsKey(profile.Id))
                {
                    throw new ArgumentException($"Duplicate weather profile ID '{profile.Id}'.", nameof(sourceProfiles));
                }

                profiles.Add(profile.Id, profile);
                orderedProfiles[index] = profile;
            }

            ValidateTransitionGraph();
        }

        public string ConfigId { get; }

        public bool ClimateAwareScheduling { get; }

        public int HistoryLength { get; }

        public float ImmediateRepeatMultiplier { get; }

        public float RecentRepeatMultiplier { get; }

        public IReadOnlyList<WeatherProfile> Profiles => orderedProfiles;

        public bool Contains(WeatherStateId id) => profiles.ContainsKey(id);

        public WeatherProfile Get(WeatherStateId id)
        {
            if (!profiles.TryGetValue(id, out WeatherProfile profile))
            {
                throw new KeyNotFoundException($"Unknown logical weather state '{id}'.");
            }

            return profile;
        }

        /// <summary>
        /// Accepts current graph edges and the exact nine-state v1 edges that
        /// may still be in an in-progress saved front. Restore-only edges are
        /// never considered by the scheduler when it chooses a new front.
        /// </summary>
        public bool AllowsSnapshotTransition(WeatherStateId from, WeatherStateId to)
        {
            if (!profiles.TryGetValue(from, out WeatherProfile source) ||
                !profiles.ContainsKey(to))
            {
                return false;
            }

            if (source.AllowsSuccessor(to))
            {
                return true;
            }

            return string.Equals(
                       ConfigId,
                       RemakeDesignTargetConfigId,
                       StringComparison.Ordinal) &&
                   IsRemakeDesignTargetV1Transition(from, to);
        }

        public static WeatherProfileCatalog CreateRemakeDesignTargets()
        {
            WeatherStateId clear = WeatherStateIds.Clear;
            WeatherStateId partly = WeatherStateIds.PartlyCloudy;
            WeatherStateId overcast = WeatherStateIds.Overcast;
            WeatherStateId drizzle = WeatherStateIds.Drizzle;
            WeatherStateId rain = WeatherStateIds.SteadyRain;
            WeatherStateId heavy = WeatherStateIds.HeavyRain;
            WeatherStateId storm = WeatherStateIds.Thunderstorm;
            WeatherStateId mist = WeatherStateIds.MorningMist;
            WeatherStateId denseFog = WeatherStateIds.DenseFog;

            // These values are explicit design targets because donor measurements are still missing.
            var result = new[]
            {
                Create(clear, "weather.clear", 0.08f, WeatherPrecipitationType.None, 0f, 0.02f,
                    20000f, 225f, 2f, 0.08f, 18f, 1f, 0f, 0f, 0f, 1.2f,
                    1200f, 3000f, 180f, 480f,
                    new[] { partly, mist }, new[] { partly, mist }),
                Create(partly, "weather.partly_cloudy", 0.38f, WeatherPrecipitationType.None, 0f, 0.04f,
                    16000f, 235f, 3f, 0.16f, 17f, 0.95f, 0f, 0f, 0f, 1.05f,
                    900f, 2400f, 180f, 420f,
                    new[] { clear, overcast, mist }, new[] { clear, overcast, mist }),
                Create(overcast, "weather.overcast", 0.78f, WeatherPrecipitationType.None, 0f, 0.12f,
                    11000f, 245f, 4f, 0.28f, 15f, 0.82f, 0.04f, 0.1f, 0f, 0.8f,
                    900f, 2100f, 180f, 480f,
                    new[] { partly, drizzle, rain, storm, mist, denseFog },
                    new[] { partly, drizzle, rain, mist, denseFog }),
                Create(drizzle, "weather.drizzle", 0.88f, WeatherPrecipitationType.Drizzle, 0.22f, 0.18f,
                    7500f, 250f, 4.5f, 0.32f, 14f, 0.76f, 0.03f, 0.08f, 0.25f, 0.62f,
                    600f, 1500f, 120f, 360f,
                    new[] { overcast, rain }, new[] { overcast, rain }),
                Create(rain, "weather.rain", 0.95f, WeatherPrecipitationType.Rain, 0.55f, 0.22f,
                    5500f, 255f, 6f, 0.45f, 13f, 0.68f, 0.12f, 0.3f, 0.62f, 0.42f,
                    720f, 1800f, 120f, 300f,
                    new[] { overcast, drizzle, heavy, storm }, new[] { overcast, drizzle, heavy, storm }),
                Create(heavy, "weather.heavy_rain", 1f, WeatherPrecipitationType.Rain, 0.86f, 0.32f,
                    3200f, 260f, 9f, 0.66f, 12f, 0.55f, 0.42f, 0.65f, 0.9f, 0.25f,
                    420f, 1200f, 90f, 240f,
                    new[] { rain, storm }, new[] { rain, storm }),
                Create(storm, "weather.storm_visual", 1f, WeatherPrecipitationType.Rain, 1f, 0.42f,
                    2200f, 265f, 12f, 0.84f, 11f, 0.48f, 0.78f, 0.92f, 1f, 0.18f,
                    300f, 900f, 60f, 180f,
                    new[] { overcast, rain, heavy }, new[] { overcast, rain, heavy }),
                Create(mist, "weather.fog", 0.52f, WeatherPrecipitationType.None, 0f, 0.76f,
                    1200f, 210f, 1.5f, 0.08f, 10f, 0.72f, 0f, 0f, 0f, 0.48f,
                    420f, 1200f, 120f, 300f,
                    new[] { clear, partly, overcast, denseFog },
                    new[] { clear, partly, overcast, denseFog }),
                Create(denseFog, "weather.dense_fog", 0.68f,
                    WeatherPrecipitationType.None, 0f, 1f,
                    80f, 215f, 1f, 0.05f, 8f, 0.22f,
                    0f, 0f, 0f, 0.32f,
                    300f, 900f, 120f, 300f,
                    new[] { mist, overcast }, new[] { mist, overcast }),
            };

            return new WeatherProfileCatalog(RemakeDesignTargetConfigId, result);
        }

        private static WeatherProfile Create(
            WeatherStateId id,
            string bindingId,
            float cloud,
            WeatherPrecipitationType precipitationType,
            float precipitation,
            float fog,
            float visibility,
            float windDirection,
            float windSpeed,
            float gust,
            float temperature,
            float readability,
            float lightningRisk,
            float lightningIntensity,
            float wetnessInput,
            float dryingModifier,
            float minimumDuration,
            float maximumDuration,
            float minimumTransition,
            float maximumTransition,
            WeatherStateId[] predecessors,
            WeatherStateId[] successors)
        {
            return new WeatherProfile(
                new WeatherState(
                    id,
                    bindingId,
                    cloud,
                    precipitationType,
                    precipitation,
                    fog,
                    visibility,
                    windDirection,
                    windSpeed,
                    gust,
                    temperature,
                    readability,
                    lightningRisk,
                    lightningIntensity,
                    wetnessInput,
                    dryingModifier),
                minimumDuration,
                maximumDuration,
                minimumTransition,
                maximumTransition,
                predecessors,
                successors,
                RemakeDesignTargetProvenance);
        }

        private void ValidateTransitionGraph()
        {
            for (int profileIndex = 0; profileIndex < orderedProfiles.Length; profileIndex++)
            {
                WeatherProfile profile = orderedProfiles[profileIndex];
                for (int successorIndex = 0; successorIndex < profile.AllowedSuccessors.Count; successorIndex++)
                {
                    WeatherStateId successorId = profile.AllowedSuccessors[successorIndex];
                    if (!profiles.TryGetValue(successorId, out WeatherProfile successor))
                    {
                        throw new ArgumentException($"Profile '{profile.Id}' references missing successor '{successorId}'.");
                    }

                    if (!successor.AllowsPredecessor(profile.Id))
                    {
                        throw new ArgumentException(
                            $"Transition '{profile.Id}' -> '{successor.Id}' is not accepted by the successor.");
                    }
                }
            }
        }

        private static bool IsRemakeDesignTargetV1Transition(
            WeatherStateId from,
            WeatherStateId to)
        {
            if (from == WeatherStateIds.Clear)
            {
                return to == WeatherStateIds.PartlyCloudy ||
                       to == WeatherStateIds.MorningMist;
            }

            if (from == WeatherStateIds.PartlyCloudy)
            {
                return to == WeatherStateIds.Clear ||
                       to == WeatherStateIds.Overcast ||
                       to == WeatherStateIds.MorningMist;
            }

            if (from == WeatherStateIds.Overcast)
            {
                return to == WeatherStateIds.PartlyCloudy ||
                       to == WeatherStateIds.Drizzle ||
                       to == WeatherStateIds.SteadyRain ||
                       to == WeatherStateIds.MorningMist ||
                       to == WeatherStateIds.DenseFog;
            }

            if (from == WeatherStateIds.Drizzle)
            {
                return to == WeatherStateIds.Overcast ||
                       to == WeatherStateIds.SteadyRain;
            }

            if (from == WeatherStateIds.SteadyRain)
            {
                return to == WeatherStateIds.Overcast ||
                       to == WeatherStateIds.Drizzle ||
                       to == WeatherStateIds.HeavyRain ||
                       to == WeatherStateIds.Thunderstorm;
            }

            if (from == WeatherStateIds.HeavyRain)
            {
                return to == WeatherStateIds.SteadyRain ||
                       to == WeatherStateIds.Thunderstorm;
            }

            if (from == WeatherStateIds.Thunderstorm)
            {
                return to == WeatherStateIds.Overcast ||
                       to == WeatherStateIds.SteadyRain ||
                       to == WeatherStateIds.HeavyRain;
            }

            if (from == WeatherStateIds.MorningMist)
            {
                return to == WeatherStateIds.Clear ||
                       to == WeatherStateIds.PartlyCloudy ||
                       to == WeatherStateIds.Overcast ||
                       to == WeatherStateIds.DenseFog;
            }

            return from == WeatherStateIds.DenseFog &&
                   (to == WeatherStateIds.MorningMist ||
                    to == WeatherStateIds.Overcast);
        }
    }
}
