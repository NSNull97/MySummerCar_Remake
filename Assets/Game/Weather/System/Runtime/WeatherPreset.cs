using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.System
{
    public enum WeatherRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Exceptional = 3,
    }

    [Flags]
    public enum WeatherMonthMask
    {
        None = 0,
        January = 1 << 0,
        February = 1 << 1,
        March = 1 << 2,
        April = 1 << 3,
        May = 1 << 4,
        June = 1 << 5,
        July = 1 << 6,
        August = 1 << 7,
        September = 1 << 8,
        October = 1 << 9,
        November = 1 << 10,
        December = 1 << 11,
        All = (1 << 12) - 1,
        FinnishSummer = May | June | July | August | September,
    }

    [Serializable]
    public sealed class WeatherTransitionCurves
    {
        [SerializeField] private AnimationCurve clouds =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve precipitation =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fog =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve wind =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve lighting =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve climate =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve surface =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float EvaluateClouds(float t) => Evaluate(clouds, t);
        public float EvaluatePrecipitation(float t) => Evaluate(precipitation, t);
        public float EvaluateFog(float t) => Evaluate(fog, t);
        public float EvaluateWind(float t) => Evaluate(wind, t);
        public float EvaluateLighting(float t) => Evaluate(lighting, t);
        public float EvaluateClimate(float t) => Evaluate(climate, t);
        public float EvaluateSurface(float t) => Evaluate(surface, t);

        private static float Evaluate(AnimationCurve curve, float t) =>
            Mathf.Clamp01(curve == null ? t : curve.Evaluate(Mathf.Clamp01(t)));
    }

    [Serializable]
    public struct WeatherLightningSettings
    {
        [SerializeField, Range(0f, 1f)] private float probability01;
        [SerializeField, Range(0f, 1f)] private float minimumIntensity01;
        [SerializeField, Range(0f, 1f)] private float maximumIntensity01;
        [SerializeField, Min(1f)] private float minimumIntervalGameSeconds;
        [SerializeField, Min(1f)] private float maximumIntervalGameSeconds;

        public WeatherLightningSettings(
            float probability01,
            float minimumIntensity01,
            float maximumIntensity01,
            float minimumIntervalGameSeconds,
            float maximumIntervalGameSeconds)
        {
            this.probability01 = Mathf.Clamp01(probability01);
            this.minimumIntensity01 = Mathf.Clamp01(minimumIntensity01);
            this.maximumIntensity01 = Mathf.Clamp01(
                Mathf.Max(minimumIntensity01, maximumIntensity01));
            this.minimumIntervalGameSeconds = Mathf.Max(
                1f,
                minimumIntervalGameSeconds);
            this.maximumIntervalGameSeconds = Mathf.Max(
                this.minimumIntervalGameSeconds,
                maximumIntervalGameSeconds);
        }

        public float Probability01 => probability01;
        public float MinimumIntensity01 => minimumIntensity01;
        public float MaximumIntensity01 => maximumIntensity01;
        public float MinimumIntervalGameSeconds => minimumIntervalGameSeconds;
        public float MaximumIntervalGameSeconds => maximumIntervalGameSeconds;
    }

    [CreateAssetMenu(
        fileName = "WeatherPreset",
        menuName = "MSC Remake/Environment/Weather System/Weather Preset")]
    public sealed class WeatherPreset : ScriptableObject
    {
        [SerializeField] private string stableId = "weather.finnish_summer.clear_cool";
        [SerializeField] private string legacyPresentationBindingId = "weather.clear";
        [SerializeField] private WeatherState targetState = default;
        [SerializeField] private Vector2 durationGameSeconds = new Vector2(900f, 2400f);
        [SerializeField] private Vector2 transitionGameSeconds = new Vector2(180f, 480f);
        [SerializeField, Min(0.0001f)] private float baseProbability = 1f;
        [SerializeField] private WeatherRarity rarity = WeatherRarity.Common;
        [SerializeField] private WeatherMonthMask allowedMonths =
            WeatherMonthMask.FinnishSummer;
        [SerializeField, Range(0f, 1f)] private float allowedTimeStart01;
        [SerializeField, Range(0f, 1f)] private float allowedTimeEnd01 = 1f;
        [SerializeField] private string[] allowedSuccessorIds = Array.Empty<string>();
        [SerializeField] private WeatherTransitionCurves transitionCurves =
            new WeatherTransitionCurves();
        [SerializeField] private WeatherLightningSettings lightning;
        [SerializeField, Min(0f)] private float dryingRateMultiplier = 1f;

        public string StableId => stableId;
        public string LegacyPresentationBindingId => legacyPresentationBindingId;
        public WeatherState TargetState => targetState;
        public Vector2 DurationGameSeconds => durationGameSeconds;
        public Vector2 TransitionGameSeconds => transitionGameSeconds;
        public float BaseProbability => baseProbability;
        public WeatherRarity Rarity => rarity;
        public WeatherMonthMask AllowedMonths => allowedMonths;
        public float AllowedTimeStart01 => allowedTimeStart01;
        public float AllowedTimeEnd01 => allowedTimeEnd01;
        public IReadOnlyList<string> AllowedSuccessorIds => allowedSuccessorIds;
        public WeatherTransitionCurves TransitionCurves => transitionCurves;
        public WeatherLightningSettings Lightning => lightning;
        public float DryingRateMultiplier => dryingRateMultiplier;

        public WeatherPresetDefinition ToDefinition()
        {
            if (string.IsNullOrWhiteSpace(stableId) || !targetState.IsValid)
            {
                throw new InvalidOperationException(
                    $"Weather preset '{name}' is not configured.");
            }

            return new WeatherPresetDefinition(
                stableId.Trim(),
                legacyPresentationBindingId?.Trim() ?? string.Empty,
                targetState,
                durationGameSeconds,
                transitionGameSeconds,
                baseProbability,
                rarity,
                allowedMonths,
                allowedTimeStart01,
                allowedTimeEnd01,
                allowedSuccessorIds,
                transitionCurves,
                lightning,
                dryingRateMultiplier);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(in WeatherPresetDefinition definition)
        {
            stableId = definition.StableId;
            legacyPresentationBindingId = definition.LegacyPresentationBindingId;
            targetState = definition.TargetState;
            durationGameSeconds = definition.DurationGameSeconds;
            transitionGameSeconds = definition.TransitionGameSeconds;
            baseProbability = definition.BaseProbability;
            rarity = definition.Rarity;
            allowedMonths = definition.AllowedMonths;
            allowedTimeStart01 = definition.AllowedTimeStart01;
            allowedTimeEnd01 = definition.AllowedTimeEnd01;
            allowedSuccessorIds = new string[definition.AllowedSuccessorIds.Count];
            for (int index = 0; index < allowedSuccessorIds.Length; index++)
            {
                allowedSuccessorIds[index] = definition.AllowedSuccessorIds[index];
            }

            transitionCurves = definition.TransitionCurves;
            lightning = definition.Lightning;
            dryingRateMultiplier = definition.DryingRateMultiplier;
        }

        private void OnValidate()
        {
            stableId = stableId?.Trim() ?? string.Empty;
            legacyPresentationBindingId =
                legacyPresentationBindingId?.Trim() ?? string.Empty;
            durationGameSeconds.x = Mathf.Max(1f, durationGameSeconds.x);
            durationGameSeconds.y = Mathf.Max(
                durationGameSeconds.x,
                durationGameSeconds.y);
            transitionGameSeconds.x = Mathf.Clamp(
                transitionGameSeconds.x,
                0f,
                durationGameSeconds.y);
            transitionGameSeconds.y = Mathf.Clamp(
                transitionGameSeconds.y,
                transitionGameSeconds.x,
                durationGameSeconds.y);
            baseProbability = Mathf.Max(0.0001f, baseProbability);
            dryingRateMultiplier = Mathf.Max(0f, dryingRateMultiplier);
            allowedSuccessorIds ??= Array.Empty<string>();
            transitionCurves ??= new WeatherTransitionCurves();
        }
#endif
    }

    public sealed class WeatherPresetDefinition
    {
        private readonly string[] allowedSuccessorIds;

        public WeatherPresetDefinition(
            string stableId,
            string legacyPresentationBindingId,
            in WeatherState targetState,
            Vector2 durationGameSeconds,
            Vector2 transitionGameSeconds,
            float baseProbability,
            WeatherRarity rarity,
            WeatherMonthMask allowedMonths,
            float allowedTimeStart01,
            float allowedTimeEnd01,
            IReadOnlyList<string> successorIds,
            WeatherTransitionCurves transitionCurves,
            in WeatherLightningSettings lightning,
            float dryingRateMultiplier)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A weather preset requires a stable ID.", nameof(stableId));
            }

            if (!targetState.IsValid)
            {
                throw new ArgumentException("A weather preset target state is invalid.", nameof(targetState));
            }

            if (durationGameSeconds.x <= 0f ||
                durationGameSeconds.y < durationGameSeconds.x ||
                transitionGameSeconds.x < 0f ||
                transitionGameSeconds.y < transitionGameSeconds.x ||
                transitionGameSeconds.y > durationGameSeconds.y ||
                !WeatherState.IsFinite(baseProbability) || baseProbability <= 0f ||
                !WeatherState.IsFinite(dryingRateMultiplier) || dryingRateMultiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationGameSeconds));
            }

            StableId = stableId.Trim();
            LegacyPresentationBindingId = legacyPresentationBindingId?.Trim() ?? string.Empty;
            TargetState = targetState;
            DurationGameSeconds = durationGameSeconds;
            TransitionGameSeconds = transitionGameSeconds;
            BaseProbability = baseProbability;
            Rarity = rarity;
            AllowedMonths = allowedMonths;
            AllowedTimeStart01 = Mathf.Repeat(allowedTimeStart01, 1f);
            AllowedTimeEnd01 = allowedTimeEnd01 >= 1f
                ? 1f
                : Mathf.Repeat(allowedTimeEnd01, 1f);
            allowedSuccessorIds = CopySuccessors(successorIds);
            TransitionCurves = transitionCurves ?? new WeatherTransitionCurves();
            Lightning = lightning;
            DryingRateMultiplier = dryingRateMultiplier;
        }

        public string StableId { get; }
        public string LegacyPresentationBindingId { get; }
        public WeatherState TargetState { get; }
        public Vector2 DurationGameSeconds { get; }
        public Vector2 TransitionGameSeconds { get; }
        public float BaseProbability { get; }
        public WeatherRarity Rarity { get; }
        public WeatherMonthMask AllowedMonths { get; }
        public float AllowedTimeStart01 { get; }
        public float AllowedTimeEnd01 { get; }
        public IReadOnlyList<string> AllowedSuccessorIds => allowedSuccessorIds;
        public WeatherTransitionCurves TransitionCurves { get; }
        public WeatherLightningSettings Lightning { get; }
        public float DryingRateMultiplier { get; }

        public bool AllowsMonth(int month)
        {
            if (month < 1 || month > 12)
            {
                return false;
            }

            var bit = (WeatherMonthMask)(1 << (month - 1));
            return (AllowedMonths & bit) != 0;
        }

        public bool AllowsTime(float normalizedTime01)
        {
            float value = Mathf.Repeat(normalizedTime01, 1f);
            if (Mathf.Approximately(AllowedTimeStart01, 0f) &&
                Mathf.Approximately(AllowedTimeEnd01, 1f))
            {
                return true;
            }

            return AllowedTimeStart01 <= AllowedTimeEnd01
                ? value >= AllowedTimeStart01 && value <= AllowedTimeEnd01
                : value >= AllowedTimeStart01 || value <= AllowedTimeEnd01;
        }

        public bool AllowsSuccessor(string stableId)
        {
            for (int index = 0; index < allowedSuccessorIds.Length; index++)
            {
                if (string.Equals(
                        allowedSuccessorIds[index],
                        stableId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] CopySuccessors(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                throw new ArgumentException(
                    "A weather preset requires at least one successor.",
                    nameof(source));
            }

            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                {
                    throw new ArgumentException(
                        "Weather successor IDs must not be empty.",
                        nameof(source));
                }

                result[index] = source[index].Trim();
            }

            return result;
        }
    }
}
