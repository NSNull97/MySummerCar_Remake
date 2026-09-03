using UnityEngine;

namespace MSC.Weather.System.Local
{
    [CreateAssetMenu(
        fileName = "EnvironmentZoneProfile",
        menuName = "MSC Remake/Environment/Weather System/Interior Zone Profile")]
    public sealed class EnvironmentZoneProfile : ScriptableObject
    {
        [SerializeField] private string stableId =
            "environment.weather_zone.closed_interior";
        [SerializeField, Range(0f, 1f)] private float outdoorIsolation = 0.96f;
        [SerializeField, Range(0f, 1f)] private float precipitationIsolation = 1f;
        [SerializeField, Range(0f, 1f)] private float fogIsolation = 0.96f;
        [SerializeField, Range(0f, 1f)] private float windIsolation = 0.96f;
        [SerializeField, Range(0f, 1f)] private float rainAudioIsolation = 0.88f;
        [SerializeField, Range(0f, 1f)] private float exteriorAudioIsolation = 0.84f;
        [SerializeField, Range(0f, 1f)] private float thunderIsolation = 0.42f;
        [SerializeField, Range(-2f, 4f)] private float exposureCompensation = 1.1f;
        [SerializeField, Min(0.1f)] private float transitionDistance = 2.2f;

        public string StableId => stableId;
        public float FogIsolation => fogIsolation;
        public float WindIsolation => windIsolation;
        public float RainAudioIsolation => rainAudioIsolation;
        public float ExteriorAudioIsolation => exteriorAudioIsolation;
        public float ExposureCompensation => exposureCompensation;
        public float TransitionDistance => transitionDistance;

        public LocalWeatherZoneSettings CreateSettings() =>
            new LocalWeatherZoneSettings(
                1f - outdoorIsolation,
                1f - precipitationIsolation,
                1f - fogIsolation,
                1f - windIsolation,
                1f - rainAudioIsolation,
                1f - exteriorAudioIsolation,
                1f - thunderIsolation,
                exposureCompensation,
                transitionDistance);

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredStableId,
            float authoredOutdoorIsolation,
            float authoredPrecipitationIsolation,
            float authoredFogIsolation,
            float authoredWindIsolation,
            float authoredRainAudioIsolation,
            float authoredExteriorAudioIsolation,
            float authoredThunderIsolation,
            float authoredExposureCompensation,
            float authoredTransitionDistance)
        {
            stableId = authoredStableId;
            outdoorIsolation = Mathf.Clamp01(authoredOutdoorIsolation);
            precipitationIsolation = Mathf.Clamp01(
                authoredPrecipitationIsolation);
            fogIsolation = Mathf.Clamp01(authoredFogIsolation);
            windIsolation = Mathf.Clamp01(authoredWindIsolation);
            rainAudioIsolation = Mathf.Clamp01(authoredRainAudioIsolation);
            exteriorAudioIsolation = Mathf.Clamp01(
                authoredExteriorAudioIsolation);
            thunderIsolation = Mathf.Clamp01(authoredThunderIsolation);
            exposureCompensation = Mathf.Clamp(
                authoredExposureCompensation,
                -2f,
                4f);
            transitionDistance = Mathf.Max(0.1f, authoredTransitionDistance);
        }
#endif
    }
}
