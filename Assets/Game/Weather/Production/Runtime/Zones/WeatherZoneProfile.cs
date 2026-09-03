using UnityEngine;

namespace MSC.Weather.Production
{
    [CreateAssetMenu(
        fileName = "WeatherZoneProfile",
        menuName = "MSC Remake/Environment/Weather Zone Profile")]
    public sealed class WeatherZoneProfile : ScriptableObject
    {
        [SerializeField] private string stableId = "environment.zone.closed_interior";
        [SerializeField] private WeatherZoneKind kind = WeatherZoneKind.ClosedInterior;
        [SerializeField, Range(0f, 1f)] private float enclosureFactor = 1f;
        [SerializeField, Range(0f, 1f)] private float shelterFactor = 1f;
        [SerializeField, Range(0f, 1f)] private float precipitationExposure = 0f;
        [SerializeField, Range(0f, 1f)] private float fogExposure = 0.05f;
        [SerializeField, Range(0f, 1f)] private float windExposure = 0.05f;
        [SerializeField, Range(0f, 1f)] private float weatherAudioExposure = 0.15f;
        [SerializeField, Range(0f, 1f)] private float thunderExposure = 0.65f;
        [SerializeField, Range(0f, 1f)] private float indoorFactor = 1f;
        [SerializeField, Min(0f)] private float fogVoidBlendDistanceMeters = 0.8f;

        public string StableId => stableId;
        public WeatherZoneKind Kind => kind;
        public float FogVoidBlendDistanceMeters => fogVoidBlendDistanceMeters;

        public WeatherExposureState CreateClosedExposure() =>
            new WeatherExposureState(
                enclosureFactor,
                shelterFactor,
                precipitationExposure,
                fogExposure,
                windExposure,
                weatherAudioExposure,
                thunderExposure,
                0f,
                indoorFactor);

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredStableId,
            WeatherZoneKind authoredKind,
            in WeatherExposureState exposure,
            float fogBlendDistanceMeters)
        {
            stableId = authoredStableId;
            kind = authoredKind;
            enclosureFactor = exposure.EnclosureFactor;
            shelterFactor = exposure.ShelterFactor;
            precipitationExposure = exposure.PrecipitationExposure;
            fogExposure = exposure.FogExposure;
            windExposure = exposure.WindExposure;
            weatherAudioExposure = exposure.WeatherAudioExposure;
            thunderExposure = exposure.ThunderExposure;
            indoorFactor = exposure.IndoorFactor;
            fogVoidBlendDistanceMeters = Mathf.Max(0f, fogBlendDistanceMeters);
        }
#endif
    }
}
