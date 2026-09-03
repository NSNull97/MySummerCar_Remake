using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.System
{
    [CreateAssetMenu(
        fileName = "FinnishSummerClimateProfile",
        menuName = "MSC Remake/Environment/Weather System/Finnish Summer Climate")]
    public sealed class FinnishSummerClimateProfile : ScriptableObject
    {
        public const string DefaultConfigId = "weather.climate.finnish_summer.v1";

        [SerializeField] private string configId = DefaultConfigId;
        [SerializeField] private WeatherPreset[] presets = Array.Empty<WeatherPreset>();
        [SerializeField] private string initialPresetId =
            FinnishSummerWeatherIds.PartlyCloudy;
        [SerializeField, Range(1, 16)] private int historyLength = 6;
        [SerializeField, Range(0f, 1f)] private float immediateRepeatMultiplier = 0.12f;
        [SerializeField, Range(0f, 1f)] private float recentRepeatMultiplier = 0.5f;

        public string ConfigId => configId;
        public IReadOnlyList<WeatherPreset> Presets => presets;
        public string InitialPresetId => initialPresetId;
        public int HistoryLength => historyLength;
        public float ImmediateRepeatMultiplier => immediateRepeatMultiplier;
        public float RecentRepeatMultiplier => recentRepeatMultiplier;

        public WeatherClimateCatalog CreateCatalog()
        {
            if (presets == null || presets.Length == 0)
            {
                return FinnishSummerClimateDefaults.CreateCatalog();
            }

            var definitions = new WeatherPresetDefinition[presets.Length];
            for (int index = 0; index < presets.Length; index++)
            {
                if (presets[index] == null)
                {
                    throw new InvalidOperationException(
                        $"Climate profile '{name}' contains an empty preset slot.");
                }

                definitions[index] = presets[index].ToDefinition();
            }

            return new WeatherClimateCatalog(
                configId,
                definitions,
                initialPresetId,
                historyLength,
                immediateRepeatMultiplier,
                recentRepeatMultiplier);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredConfigId,
            WeatherPreset[] authoredPresets,
            string authoredInitialPresetId,
            int authoredHistoryLength = 6,
            float authoredImmediateRepeatMultiplier = 0.12f,
            float authoredRecentRepeatMultiplier = 0.5f)
        {
            configId = authoredConfigId;
            presets = authoredPresets ?? Array.Empty<WeatherPreset>();
            initialPresetId = authoredInitialPresetId;
            historyLength = Mathf.Clamp(authoredHistoryLength, 1, 16);
            immediateRepeatMultiplier = Mathf.Clamp01(
                authoredImmediateRepeatMultiplier);
            recentRepeatMultiplier = Mathf.Clamp01(
                authoredRecentRepeatMultiplier);
        }

        private void OnValidate()
        {
            configId = string.IsNullOrWhiteSpace(configId)
                ? DefaultConfigId
                : configId.Trim();
            initialPresetId = initialPresetId?.Trim() ?? string.Empty;
            presets ??= Array.Empty<WeatherPreset>();
            historyLength = Mathf.Clamp(historyLength, 1, 16);
            immediateRepeatMultiplier = Mathf.Clamp01(immediateRepeatMultiplier);
            recentRepeatMultiplier = Mathf.Clamp01(recentRepeatMultiplier);
        }
#endif
    }

    public static class FinnishSummerWeatherIds
    {
        public const string ClearCool = "weather.finnish_summer.clear_cool";
        public const string PartlyCloudy = "weather.finnish_summer.partly_cloudy";
        public const string MostlyCloudy = "weather.finnish_summer.mostly_cloudy";
        public const string BrightOvercast = "weather.finnish_summer.bright_overcast";
        public const string HeavyOvercast = "weather.finnish_summer.heavy_overcast";
        public const string MorningMist = "weather.finnish_summer.morning_mist";
        public const string LakeMist = "weather.finnish_summer.lake_mist";
        public const string LightDrizzle = "weather.finnish_summer.light_drizzle";
        public const string LightRain = "weather.finnish_summer.light_rain";
        public const string SteadyRain = "weather.finnish_summer.steady_rain";
        public const string HeavyRain = "weather.finnish_summer.heavy_rain";
        public const string RareThunderstorm =
            "weather.finnish_summer.rare_thunderstorm";
        public const string PostRainWet = "weather.finnish_summer.post_rain_wet";
        public const string ClearingAfterRain =
            "weather.finnish_summer.clearing_after_rain";
        public const string ColdClearEvening =
            "weather.finnish_summer.cold_clear_evening";
        public const string BlueHour = "weather.finnish_summer.blue_hour";
    }

    public sealed class WeatherClimateCatalog
    {
        private readonly Dictionary<string, WeatherPresetDefinition> byId;
        private readonly WeatherPresetDefinition[] ordered;

        public WeatherClimateCatalog(
            string configId,
            IReadOnlyList<WeatherPresetDefinition> presets,
            string initialPresetId,
            int historyLength,
            float immediateRepeatMultiplier,
            float recentRepeatMultiplier)
        {
            if (string.IsNullOrWhiteSpace(configId) ||
                presets == null || presets.Count == 0 ||
                string.IsNullOrWhiteSpace(initialPresetId))
            {
                throw new ArgumentException("Climate catalog configuration is incomplete.");
            }

            ConfigId = configId.Trim();
            InitialPresetId = initialPresetId.Trim();
            HistoryLength = Mathf.Clamp(historyLength, 1, 16);
            ImmediateRepeatMultiplier = Mathf.Clamp01(immediateRepeatMultiplier);
            RecentRepeatMultiplier = Mathf.Clamp01(recentRepeatMultiplier);
            ordered = new WeatherPresetDefinition[presets.Count];
            byId = new Dictionary<string, WeatherPresetDefinition>(
                presets.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < presets.Count; index++)
            {
                WeatherPresetDefinition preset = presets[index] ??
                    throw new ArgumentException("Climate preset entries must not be null.");
                if (!byId.TryAdd(preset.StableId, preset))
                {
                    throw new ArgumentException(
                        $"Duplicate climate preset ID '{preset.StableId}'.");
                }

                ordered[index] = preset;
            }

            if (!byId.ContainsKey(InitialPresetId))
            {
                throw new ArgumentException(
                    $"Initial climate preset '{InitialPresetId}' is missing.");
            }

            ValidateGraph();
        }

        public string ConfigId { get; }
        public string InitialPresetId { get; }
        public int HistoryLength { get; }
        public float ImmediateRepeatMultiplier { get; }
        public float RecentRepeatMultiplier { get; }
        public IReadOnlyList<WeatherPresetDefinition> Presets => ordered;

        public WeatherPresetDefinition Get(string id)
        {
            if (!byId.TryGetValue(id, out WeatherPresetDefinition preset))
            {
                throw new KeyNotFoundException($"Unknown climate preset '{id}'.");
            }

            return preset;
        }

        public bool Contains(string id) =>
            !string.IsNullOrWhiteSpace(id) && byId.ContainsKey(id);

        private void ValidateGraph()
        {
            for (int presetIndex = 0; presetIndex < ordered.Length; presetIndex++)
            {
                WeatherPresetDefinition preset = ordered[presetIndex];
                for (int successorIndex = 0;
                     successorIndex < preset.AllowedSuccessorIds.Count;
                     successorIndex++)
                {
                    string successor = preset.AllowedSuccessorIds[successorIndex];
                    if (!byId.ContainsKey(successor))
                    {
                        throw new ArgumentException(
                            $"Preset '{preset.StableId}' references missing successor '{successor}'.");
                    }
                }
            }
        }
    }

    public static class FinnishSummerClimateDefaults
    {
        private static readonly WeatherMonthMask Summer =
            WeatherMonthMask.FinnishSummer;

        public static WeatherClimateCatalog CreateCatalog() =>
            new WeatherClimateCatalog(
                FinnishSummerClimateProfile.DefaultConfigId,
                CreateDefinitions(),
                FinnishSummerWeatherIds.PartlyCloudy,
                6,
                0.12f,
                0.5f);

        public static WeatherPresetDefinition[] CreateDefinitions()
        {
            string clear = FinnishSummerWeatherIds.ClearCool;
            string partly = FinnishSummerWeatherIds.PartlyCloudy;
            string mostly = FinnishSummerWeatherIds.MostlyCloudy;
            string bright = FinnishSummerWeatherIds.BrightOvercast;
            string heavyCloud = FinnishSummerWeatherIds.HeavyOvercast;
            string morningMist = FinnishSummerWeatherIds.MorningMist;
            string lakeMist = FinnishSummerWeatherIds.LakeMist;
            string drizzle = FinnishSummerWeatherIds.LightDrizzle;
            string lightRain = FinnishSummerWeatherIds.LightRain;
            string rain = FinnishSummerWeatherIds.SteadyRain;
            string heavyRain = FinnishSummerWeatherIds.HeavyRain;
            string storm = FinnishSummerWeatherIds.RareThunderstorm;
            string wet = FinnishSummerWeatherIds.PostRainWet;
            string clearing = FinnishSummerWeatherIds.ClearingAfterRain;
            string evening = FinnishSummerWeatherIds.ColdClearEvening;
            string blue = FinnishSummerWeatherIds.BlueHour;

            return new[]
            {
                Preset(clear, "weather.clear", State(
                    .10f,.16f,.82f,.12f, 0f,0f,0f, .015f,24000f,.08f,
                    2.2f,225f,.10f, .05f,.01f, .94f,1.00f,.96f,16f,.48f),
                    1200f,3000f,240f,600f,1.05f,WeatherRarity.Common,0f,1f,1.25f,
                    partly, evening, blue),
                Preset(partly, "weather.partly_cloudy", State(
                    .36f,.32f,.72f,.28f, 0f,0f,0f, .025f,19000f,.13f,
                    3.3f,235f,.18f, .08f,.015f, .82f,.98f,.94f,15f,.56f),
                    900f,2700f,240f,600f,1.35f,WeatherRarity.Common,0f,1f,1.05f,
                    clear, mostly, bright, morningMist, blue),
                Preset(mostly, "weather.partly_cloudy", State(
                    .62f,.54f,.59f,.48f, 0f,0f,0f, .045f,14500f,.20f,
                    4.2f,242f,.26f, .10f,.02f, .62f,.93f,.91f,14f,.64f),
                    900f,2400f,240f,660f,1.2f,WeatherRarity.Common,0f,1f,.9f,
                    partly, bright, heavyCloud, lakeMist),
                Preset(bright, "weather.overcast", State(
                    .78f,.62f,.50f,.42f, 0f,0f,0f, .065f,12000f,.25f,
                    4.5f,248f,.30f, .12f,.025f, .48f,.91f,.92f,14f,.69f),
                    900f,2400f,240f,720f,1.1f,WeatherRarity.Common,0f,1f,.78f,
                    mostly, heavyCloud, drizzle, clearing),
                Preset(heavyCloud, "weather.overcast", State(
                    .94f,.82f,.38f,.67f, 0f,0f,.04f, .10f,8500f,.34f,
                    5.5f,252f,.42f, .16f,.035f, .22f,.84f,.86f,13f,.77f),
                    720f,2100f,180f,600f,1.0f,WeatherRarity.Common,0f,1f,.6f,
                    bright, drizzle, lightRain, heavyRain),
                Preset(morningMist, "weather.fog", State(
                    .42f,.34f,.70f,.18f, 0f,0f,0f, .72f,700f,.62f,
                    1.0f,210f,.06f, .14f,.015f, .58f,.88f,.90f,9f,.91f),
                    420f,1200f,180f,480f,.62f,WeatherRarity.Uncommon,.10f,.36f,.42f,
                    partly, mostly, lakeMist),
                Preset(lakeMist, "weather.fog", State(
                    .58f,.46f,.64f,.22f, 0f,0f,0f, .84f,80f,.76f,
                    .8f,205f,.05f, .16f,.02f, .44f,.84f,.88f,10f,.95f),
                    360f,1050f,180f,420f,.42f,WeatherRarity.Rare,.80f,.38f,.34f,
                    morningMist, mostly, bright),
                Preset(drizzle, "weather.drizzle", State(
                    .92f,.76f,.42f,.58f, .18f,.72f,.02f, .16f,6500f,.40f,
                    4.2f,250f,.30f, .26f,.05f, .20f,.82f,.84f,12f,.86f),
                    540f,1500f,120f,420f,.9f,WeatherRarity.Common,0f,1f,.34f,
                    heavyCloud, lightRain, wet),
                Preset(lightRain, "weather.rain", State(
                    .97f,.84f,.34f,.72f, .36f,.24f,.05f, .20f,5200f,.46f,
                    5.2f,255f,.40f, .48f,.15f, .13f,.78f,.80f,12f,.90f),
                    540f,1500f,120f,360f,.82f,WeatherRarity.Common,0f,1f,.25f,
                    drizzle, rain, wet),
                Preset(rain, "weather.rain", State(
                    1f,.92f,.28f,.82f, .62f,.05f,.12f, .26f,3600f,.54f,
                    6.3f,258f,.54f, .68f,.34f, .08f,.73f,.76f,11f,.94f),
                    600f,1800f,120f,360f,.72f,WeatherRarity.Common,0f,1f,.18f,
                    lightRain, heavyRain, wet),
                Preset(heavyRain, "weather.heavy_rain", State(
                    1f,1f,.20f,.90f, .88f,0f,.42f, .34f,2200f,.62f,
                    8.5f,263f,.72f, .90f,.62f, .04f,.67f,.70f,10f,.98f),
                    360f,1050f,90f,300f,.36f,WeatherRarity.Uncommon,0f,1f,.10f,
                    rain, storm, wet),
                Preset(storm, "weather.storm_visual", State(
                    1f,1f,.14f,1f, 1f,0f,.95f, .42f,1500f,.72f,
                    11.5f,268f,.92f, 1f,.82f, .02f,.60f,.64f,10f,1f),
                    240f,720f,60f,180f,.08f,WeatherRarity.Exceptional,.38f,.82f,.06f,
                    new WeatherLightningSettings(.85f,.55f,1f,45f,150f),
                    heavyRain, rain),
                Preset(wet, "weather.overcast", State(
                    .84f,.65f,.46f,.46f, 0f,0f,.02f, .18f,6000f,.48f,
                    3.8f,245f,.24f, .96f,.72f, .30f,.86f,.88f,12f,.93f),
                    600f,1800f,180f,540f,1.05f,WeatherRarity.Common,0f,1f,.18f,
                    clearing, bright, lakeMist),
                Preset(clearing, "weather.partly_cloudy", State(
                    .58f,.46f,.61f,.32f, 0f,0f,0f, .10f,10500f,.30f,
                    4.6f,238f,.28f, .82f,.46f, .58f,.94f,.93f,13f,.78f),
                    600f,1500f,180f,540f,1.1f,WeatherRarity.Common,0f,1f,.72f,
                    partly, mostly, clear),
                Preset(evening, "weather.clear", State(
                    .18f,.22f,.78f,.16f, 0f,0f,0f, .035f,18000f,.16f,
                    1.8f,220f,.08f, .16f,.025f, .62f,.88f,.90f,8f,.62f),
                    600f,1800f,180f,480f,.72f,WeatherRarity.Uncommon,.66f,.96f,.72f,
                    blue, clear, partly),
                Preset(blue, "weather.clear", State(
                    .28f,.30f,.72f,.14f, 0f,0f,0f, .055f,14000f,.26f,
                    1.5f,215f,.08f, .18f,.03f, .24f,.70f,.82f,7f,.70f),
                    420f,1200f,180f,420f,.88f,WeatherRarity.Common,.78f,.10f,.44f,
                    evening, clear, morningMist),
            };
        }

        private static WeatherPresetDefinition Preset(
            string id,
            string binding,
            in WeatherState state,
            float minimumDuration,
            float maximumDuration,
            float minimumTransition,
            float maximumTransition,
            float probability,
            WeatherRarity rarity,
            float timeStart,
            float timeEnd,
            float dryingMultiplier,
            params string[] successors) => Preset(
                id,
                binding,
                state,
                minimumDuration,
                maximumDuration,
                minimumTransition,
                maximumTransition,
                probability,
                rarity,
                timeStart,
                timeEnd,
                dryingMultiplier,
                default,
                successors);

        private static WeatherPresetDefinition Preset(
            string id,
            string binding,
            in WeatherState state,
            float minimumDuration,
            float maximumDuration,
            float minimumTransition,
            float maximumTransition,
            float probability,
            WeatherRarity rarity,
            float timeStart,
            float timeEnd,
            float dryingMultiplier,
            in WeatherLightningSettings lightning,
            params string[] successors) => new WeatherPresetDefinition(
                id,
                binding,
                state,
                new Vector2(minimumDuration, maximumDuration),
                new Vector2(minimumTransition, maximumTransition),
                probability,
                rarity,
                Summer,
                timeStart,
                timeEnd,
                successors,
                new WeatherTransitionCurves(),
                lightning,
                dryingMultiplier);

        private static WeatherState State(
            float cloudCoverage,
            float cloudDensity,
            float cloudErosion,
            float cloudShadow,
            float precipitation,
            float drizzle,
            float thunder,
            float fog,
            float fogDistance,
            float haze,
            float windSpeed,
            float windDirectionDegrees,
            float gustiness,
            float wetness,
            float puddles,
            float sunVisibility,
            float skyBrightness,
            float ambient,
            float temperature,
            float humidity)
        {
            float radians = windDirectionDegrees * Mathf.Deg2Rad;
            return new WeatherState(
                cloudCoverage,
                cloudDensity,
                cloudErosion,
                cloudShadow,
                precipitation,
                drizzle,
                thunder,
                fog,
                fogDistance,
                haze,
                windSpeed,
                new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)),
                gustiness,
                wetness,
                puddles,
                sunVisibility,
                skyBrightness,
                ambient,
                temperature,
                humidity);
        }
    }
}
