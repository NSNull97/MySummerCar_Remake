using Enviro;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration
{
    /// <summary>
    /// Authoring-only bridge from project-owned stable IDs to exact Enviro assets.
    /// Weather and quality resolution is reference-based; the explicitly assigned
    /// Effects source isolates Enviro's unavoidable exact effect-key contract.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Enviro3EnvironmentBindings",
        menuName = "MSC Remake/Weather/Enviro 3 Environment Bindings")]
    public sealed class Enviro3EnvironmentBindings : ScriptableObject
    {
        public const string ClearIdValue = "weather.clear";
        public const string OvercastIdValue = "weather.overcast";
        public const string RainIdValue = "weather.rain";
        public const string StormIdValue = "weather.storm_visual";
        public const string FogIdValue = "weather.fog";
        public const string NightIdValue = "weather.night";
        public const string LowQualityIdValue = "quality.low";
        public const string HighQualityIdValue = "quality.high";

        private static readonly EnvironmentBindingId ClearId = ParseKnownId(ClearIdValue);
        private static readonly EnvironmentBindingId OvercastId = ParseKnownId(OvercastIdValue);
        private static readonly EnvironmentBindingId RainId = ParseKnownId(RainIdValue);
        private static readonly EnvironmentBindingId StormId = ParseKnownId(StormIdValue);
        private static readonly EnvironmentBindingId FogId = ParseKnownId(FogIdValue);
        private static readonly EnvironmentBindingId NightId = ParseKnownId(NightIdValue);

        [Header("Runtime-isolated configuration source")]
        [SerializeField] private EnviroConfiguration sourceConfiguration;

        [Header("Explicit effects source")]
        [SerializeField] private EnviroEffectsModule effectsSource;

        [Header("Direct weather asset references")]
        [SerializeField] private EnviroWeatherType clear;
        [SerializeField] private EnviroWeatherType overcast;
        [SerializeField] private EnviroWeatherType rain;
        [SerializeField] private EnviroWeatherType storm;
        [SerializeField] private EnviroWeatherType fog;

        [Header("Direct quality asset references")]
        [SerializeField] private EnviroQuality low;
        [SerializeField] private EnviroQuality high;

        public EnviroConfiguration SourceConfiguration => sourceConfiguration;

        public EnviroEffectsModule EffectsSource => effectsSource;

        public EnviroWeatherType Clear => clear;

        public EnviroWeatherType Overcast => overcast;

        public EnviroWeatherType Rain => rain;

        public EnviroWeatherType Storm => storm;

        public EnviroWeatherType Fog => fog;

        public EnviroQuality Low => low;

        public EnviroQuality High => high;

        public void ConfigureForAuthoring(
            EnviroConfiguration authoredSourceConfiguration,
            EnviroEffectsModule authoredEffectsSource,
            EnviroWeatherType authoredClear,
            EnviroWeatherType authoredOvercast,
            EnviroWeatherType authoredRain,
            EnviroWeatherType authoredStorm,
            EnviroWeatherType authoredFog,
            EnviroQuality authoredLow,
            EnviroQuality authoredHigh)
        {
            sourceConfiguration = authoredSourceConfiguration;
            effectsSource = authoredEffectsSource;
            clear = authoredClear;
            overcast = authoredOvercast;
            rain = authoredRain;
            storm = authoredStorm;
            fog = authoredFog;
            low = authoredLow;
            high = authoredHigh;
        }

        public bool TryResolveWeather(
            EnvironmentBindingId bindingId,
            out EnviroWeatherType weatherType,
            out EnvironmentPresentationPresetKind presetKind)
        {
            if (bindingId == ClearId)
            {
                weatherType = clear;
                presetKind = EnvironmentPresentationPresetKind.Clear;
                return weatherType != null;
            }

            if (bindingId == OvercastId)
            {
                weatherType = overcast;
                presetKind = EnvironmentPresentationPresetKind.Overcast;
                return weatherType != null;
            }

            if (bindingId == RainId)
            {
                weatherType = rain;
                presetKind = EnvironmentPresentationPresetKind.Rain;
                return weatherType != null;
            }

            if (bindingId == StormId)
            {
                weatherType = storm;
                presetKind = EnvironmentPresentationPresetKind.Storm;
                return weatherType != null;
            }

            if (bindingId == FogId)
            {
                weatherType = fog;
                presetKind = EnvironmentPresentationPresetKind.Mist;
                return weatherType != null;
            }

            if (bindingId == NightId)
            {
                // Night is a project-owned time state over the clear visual preset.
                weatherType = clear;
                presetKind = EnvironmentPresentationPresetKind.Night;
                return weatherType != null;
            }

            weatherType = null;
            presetKind = default;
            return false;
        }

        public bool TryResolveQuality(
            EnvironmentQualityTier qualityTier,
            out EnviroQuality quality)
        {
            switch (qualityTier)
            {
                case EnvironmentQualityTier.Low:
                    quality = low;
                    return quality != null;
                case EnvironmentQualityTier.High:
                    quality = high;
                    return quality != null;
                default:
                    quality = null;
                    return false;
            }
        }

        private static EnvironmentBindingId ParseKnownId(string value)
        {
            EnvironmentBindingId.TryParse(value, out EnvironmentBindingId bindingId);
            return bindingId;
        }
    }
}
