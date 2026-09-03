using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.System
{
    public readonly struct LocalWeatherDebugData
    {
        public LocalWeatherDebugData(
            string zoneId,
            float outdoorExposure01,
            float precipitationExposure01,
            float fogExposure01,
            float windExposure01,
            float audioLeak01,
            float interiorDepth01,
            float roofExposure01,
            string nearestPortalId,
            float nearestPortalOpenness01)
        {
            ZoneId = zoneId ?? string.Empty;
            OutdoorExposure01 = Mathf.Clamp01(outdoorExposure01);
            PrecipitationExposure01 = Mathf.Clamp01(precipitationExposure01);
            FogExposure01 = Mathf.Clamp01(fogExposure01);
            WindExposure01 = Mathf.Clamp01(windExposure01);
            AudioLeak01 = Mathf.Clamp01(audioLeak01);
            InteriorDepth01 = Mathf.Clamp01(interiorDepth01);
            RoofExposure01 = Mathf.Clamp01(roofExposure01);
            NearestPortalId = nearestPortalId ?? string.Empty;
            NearestPortalOpenness01 = Mathf.Clamp01(nearestPortalOpenness01);
        }

        public string ZoneId { get; }
        public float OutdoorExposure01 { get; }
        public float PrecipitationExposure01 { get; }
        public float FogExposure01 { get; }
        public float WindExposure01 { get; }
        public float AudioLeak01 { get; }
        public float InteriorDepth01 { get; }
        public float RoofExposure01 { get; }
        public string NearestPortalId { get; }
        public float NearestPortalOpenness01 { get; }
    }

    public interface ILocalWeatherDebugSource
    {
        LocalWeatherDebugData DebugData { get; }
    }

    [DisallowMultipleComponent]
    public sealed class WeatherDebugController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour localContextSourceComponent;
        [SerializeField] private bool showRuntimeOverlay;
        [SerializeField] private Vector2 overlayPosition = new Vector2(12f, 12f);

        private ILocalWeatherDebugSource localSource;
        private WeatherDirector director;
        private EnvironmentPresentationFrame frame;
        private EnvironmentPresentationStatus status;
        private WeatherBackendType backend;
        private float lastAudioRain01;
        private float lastAudioWind01;
        private float lastAudioThunder01;
        private float lastAudioShelter01;

        public WeatherBackendType Backend => backend;
        public EnvironmentPresentationStatus BackendStatus => status;
        public EnvironmentPresentationFrame LastFrame => frame;
        public LocalWeatherDebugData Local =>
            localSource?.DebugData ?? default;

        private void Awake()
        {
            localSource = localContextSourceComponent as ILocalWeatherDebugSource;
        }

        public void BindDirector(WeatherDirector weatherDirector)
        {
            director = weatherDirector;
        }

        public void RecordFrame(
            in EnvironmentPresentationFrame presentationFrame,
            WeatherBackendType backendType,
            in EnvironmentPresentationStatus presentationStatus)
        {
            frame = presentationFrame;
            backend = backendType;
            status = presentationStatus;
        }

        public void RecordBackend(
            WeatherBackendType backendType,
            in EnvironmentPresentationStatus presentationStatus)
        {
            backend = backendType;
            status = presentationStatus;
        }

        public void RecordAudio(
            float rain01,
            float wind01,
            float shelter01,
            float thunder01 = 0f)
        {
            lastAudioRain01 = Mathf.Clamp01(rain01);
            lastAudioWind01 = Mathf.Clamp01(wind01);
            lastAudioShelter01 = Mathf.Clamp01(shelter01);
            lastAudioThunder01 = Mathf.Clamp01(thunder01);
        }

        private void OnGUI()
        {
            if (!showRuntimeOverlay)
            {
                return;
            }

            LocalWeatherDebugData local = Local;
            string current = director?.CurrentPresetId ?? frame.BindingId.Value;
            string target = director?.TargetPresetId ?? frame.BindingId.Value;
            float progress = director?.TransitionProgress01 ?? 0f;
            string wetness = director != null
                ? $"{director.CurrentState.SurfaceWetness01:F2} / " +
                  $"{director.CurrentState.PuddleAmount01:F2}"
                : "n/a";
            string text =
                $"Weather backend: {backend} ({status.State})\n" +
                $"Current: {current}\nTarget: {target}\nTransition: {progress:F3}\n" +
                $"Clouds: {frame.CloudCoverage01:F2}  Rain: {frame.PrecipitationIntensity01:F2}\n" +
                $"Fog: {frame.FogMistIntensity01:F2} / {frame.VisibilityMeters:F0} m\n" +
                $"Wind: {frame.WindSpeedMetersPerSecond:F1} m/s\n" +
                $"Wetness / puddles: {wetness}\n" +
                $"Zone: {local.ZoneId}  Depth: {local.InteriorDepth01:F2}\n" +
                $"Exposure O/P/F/W/A: {local.OutdoorExposure01:F2} / " +
                $"{local.PrecipitationExposure01:F2} / {local.FogExposure01:F2} / " +
                $"{local.WindExposure01:F2} / {local.AudioLeak01:F2}\n" +
                $"Roof: {local.RoofExposure01:F2}  Portal: {local.NearestPortalId} " +
                $"({local.NearestPortalOpenness01:F2})\n" +
                $"Audio rain/wind/thunder/shelter: {lastAudioRain01:F2} / " +
                $"{lastAudioWind01:F2} / {lastAudioThunder01:F2} / " +
                $"{lastAudioShelter01:F2}";
            GUI.Box(
                new Rect(overlayPosition.x, overlayPosition.y, 500f, 292f),
                text);
        }
    }
}
