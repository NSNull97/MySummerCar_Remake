using System;
using System.Collections.Generic;
using DomainPrecipitationType = MSC.Weather.Domain.WeatherPrecipitationType;
using DomainWeatherProfile = MSC.Weather.Domain.WeatherProfile;
using DomainWeatherState = MSC.Weather.Domain.WeatherState;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.System
{
    /// <summary>
    /// Compatibility projection from the richer Finnish-summer data model into
    /// the accepted production weather domain. Existing save IDs and developer
    /// commands remain valid while all sixteen climate states become schedulable.
    /// </summary>
    public static class FinnishSummerProductionCatalogAdapter
    {
        public const string Provenance =
            "Reimplemented:FinnishSummerClimateProfile.v1";

        private static readonly Dictionary<string, WeatherStateId> IdMap =
            new Dictionary<string, WeatherStateId>(StringComparer.Ordinal)
            {
                { FinnishSummerWeatherIds.ClearCool, WeatherStateIds.Clear },
                { FinnishSummerWeatherIds.PartlyCloudy, WeatherStateIds.PartlyCloudy },
                { FinnishSummerWeatherIds.MostlyCloudy, WeatherStateIds.Overcast },
                { FinnishSummerWeatherIds.BrightOvercast, WeatherStateIds.BrightOvercast },
                { FinnishSummerWeatherIds.HeavyOvercast, WeatherStateIds.HeavyOvercast },
                { FinnishSummerWeatherIds.MorningMist, WeatherStateIds.MorningMist },
                { FinnishSummerWeatherIds.LakeMist, WeatherStateIds.DenseFog },
                { FinnishSummerWeatherIds.LightDrizzle, WeatherStateIds.Drizzle },
                { FinnishSummerWeatherIds.LightRain, WeatherStateIds.LightRain },
                { FinnishSummerWeatherIds.SteadyRain, WeatherStateIds.SteadyRain },
                { FinnishSummerWeatherIds.HeavyRain, WeatherStateIds.HeavyRain },
                { FinnishSummerWeatherIds.RareThunderstorm, WeatherStateIds.Thunderstorm },
                { FinnishSummerWeatherIds.PostRainWet, WeatherStateIds.PostRainWet },
                { FinnishSummerWeatherIds.ClearingAfterRain, WeatherStateIds.ClearingAfterRain },
                { FinnishSummerWeatherIds.ColdClearEvening, WeatherStateIds.ColdClearEvening },
                { FinnishSummerWeatherIds.BlueHour, WeatherStateIds.BlueHour },
            };

        public static WeatherProfileCatalog Create(
            WeatherClimateCatalog climateCatalog,
            out WeatherStateId initialStateId)
        {
            if (climateCatalog == null)
            {
                throw new ArgumentNullException(nameof(climateCatalog));
            }

            if (climateCatalog.Presets.Count != IdMap.Count)
            {
                throw new ArgumentException(
                    $"Finnish summer production projection requires exactly {IdMap.Count} presets.",
                    nameof(climateCatalog));
            }

            initialStateId = MapId(climateCatalog.InitialPresetId);
            var predecessors = BuildPredecessors(climateCatalog);
            var profiles = new DomainWeatherProfile[climateCatalog.Presets.Count];
            for (int index = 0; index < climateCatalog.Presets.Count; index++)
            {
                WeatherPresetDefinition preset = climateCatalog.Presets[index];
                WeatherStateId id = MapId(preset.StableId);
                profiles[index] = new DomainWeatherProfile(
                    ProjectState(id, preset),
                    preset.DurationGameSeconds.x,
                    preset.DurationGameSeconds.y,
                    preset.TransitionGameSeconds.x,
                    preset.TransitionGameSeconds.y,
                    predecessors[id].ToArray(),
                    MapIds(preset.AllowedSuccessorIds),
                    Provenance,
                    new WeatherSelectionSettings(
                        preset.BaseProbability,
                        MapRarity(preset.Rarity),
                        (int)preset.AllowedMonths,
                        preset.AllowedTimeStart01,
                        preset.AllowedTimeEnd01,
                        preset.TargetState.TemperatureCelsius,
                        preset.TargetState.Humidity01,
                        preset.TargetState.Precipitation01 > 0.001f ||
                        preset.TargetState.Drizzle01 > 0.001f));
            }

            // Retain the accepted config ID so v1 saves remain loadable. Stable
            // IDs shared with the original nine-state catalog are intentionally
            // preserved; the seven additional IDs are additive.
            return new WeatherProfileCatalog(
                WeatherProfileCatalog.RemakeDesignTargetConfigId,
                profiles,
                true,
                climateCatalog.HistoryLength,
                climateCatalog.ImmediateRepeatMultiplier,
                climateCatalog.RecentRepeatMultiplier);
        }

        public static WeatherStateId MapId(string finnishSummerId)
        {
            if (string.IsNullOrWhiteSpace(finnishSummerId) ||
                !IdMap.TryGetValue(finnishSummerId, out WeatherStateId id))
            {
                throw new ArgumentException(
                    $"Unknown Finnish summer weather ID '{finnishSummerId}'.",
                    nameof(finnishSummerId));
            }

            return id;
        }

        private static Dictionary<WeatherStateId, List<WeatherStateId>>
            BuildPredecessors(WeatherClimateCatalog catalog)
        {
            var result = new Dictionary<WeatherStateId, List<WeatherStateId>>(
                catalog.Presets.Count);
            for (int index = 0; index < catalog.Presets.Count; index++)
            {
                result.Add(MapId(catalog.Presets[index].StableId),
                    new List<WeatherStateId>(4));
            }

            for (int sourceIndex = 0;
                 sourceIndex < catalog.Presets.Count;
                 sourceIndex++)
            {
                WeatherPresetDefinition source = catalog.Presets[sourceIndex];
                WeatherStateId sourceId = MapId(source.StableId);
                for (int successorIndex = 0;
                     successorIndex < source.AllowedSuccessorIds.Count;
                     successorIndex++)
                {
                    WeatherStateId successorId =
                        MapId(source.AllowedSuccessorIds[successorIndex]);
                    result[successorId].Add(sourceId);
                }
            }

            return result;
        }

        private static WeatherStateId[] MapIds(IReadOnlyList<string> source)
        {
            var result = new WeatherStateId[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = MapId(source[index]);
            }

            return result;
        }

        private static DomainWeatherState ProjectState(
            WeatherStateId id,
            WeatherPresetDefinition preset)
        {
            WeatherState state = preset.TargetState;
            float rain = Mathf.Clamp01(state.Precipitation01);
            float drizzle = Mathf.Clamp01(state.Drizzle01);
            DomainPrecipitationType precipitationType;
            float precipitationIntensity;
            if (rain <= 0.001f && drizzle <= 0.001f)
            {
                precipitationType = DomainPrecipitationType.None;
                precipitationIntensity = 0f;
            }
            else if (drizzle >= rain)
            {
                precipitationType = DomainPrecipitationType.Drizzle;
                precipitationIntensity = Mathf.Max(rain, drizzle);
            }
            else
            {
                precipitationType = DomainPrecipitationType.Rain;
                precipitationIntensity = Mathf.Max(rain, drizzle * 0.45f);
            }

            Vector2 direction = state.WindDirectionXZ;
            float windDirectionDegrees = Mathf.Repeat(
                Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg,
                360f);
            float lightningRisk = Mathf.Max(
                state.ThunderIntensity01,
                preset.Lightning.Probability01);
            float lightningIntensity = Mathf.Max(
                state.ThunderIntensity01,
                preset.Lightning.MaximumIntensity01);

            return new DomainWeatherState(
                id,
                preset.LegacyPresentationBindingId,
                state.CloudCoverage01,
                precipitationType,
                precipitationIntensity,
                state.FogDensity01,
                state.FogDistanceMeters,
                windDirectionDegrees,
                state.WindSpeedMetersPerSecond,
                state.WindGustiness01,
                state.TemperatureCelsius,
                Mathf.Clamp01(state.AmbientLightMultiplier),
                Mathf.Clamp01(lightningRisk),
                Mathf.Clamp01(lightningIntensity),
                Mathf.Clamp01(rain + drizzle * 0.35f),
                preset.DryingRateMultiplier,
                state.Humidity01);
        }

        private static WeatherProfileRarity MapRarity(WeatherRarity rarity)
        {
            switch (rarity)
            {
                case WeatherRarity.Uncommon:
                    return WeatherProfileRarity.Uncommon;
                case WeatherRarity.Rare:
                    return WeatherProfileRarity.Rare;
                case WeatherRarity.Exceptional:
                    return WeatherProfileRarity.Exceptional;
                default:
                    return WeatherProfileRarity.Common;
            }
        }
    }
}
