using System;
using MSC.Weather.System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Final local context composer. Zone leakage and direct roof exposure remain
    /// separate, so shelters, doorways, interiors and vehicle cabins can share it.
    /// </summary>
    [DefaultExecutionOrder(-340)]
    [DisallowMultipleComponent]
    public sealed class WeatherEnvironmentResolver : MonoBehaviour,
        ILocalWeatherDebugSource,
        ILocalWeatherContextSource
    {
        [SerializeField] private InteriorZoneResolver interiorResolver;
        [SerializeField] private RoofExposureResolver roofResolver;
        [SerializeField, Min(0.02f)] private float composeIntervalSeconds = 0.1f;

        private float nextComposeTime;

        public event Action<LocalWeatherContext> ContextChanged;

        public LocalWeatherContext Current { get; private set; } =
            LocalWeatherContext.Outdoor;

        public LocalWeatherDebugData DebugData
        {
            get
            {
                IEnvironmentPortal nearest = interiorResolver != null
                    ? interiorResolver.NearestPortal
                    : null;
                return new LocalWeatherDebugData(
                    Current.CurrentZoneId,
                    Current.OutdoorExposure01,
                    Current.PrecipitationExposure01,
                    Current.FogExposure01,
                    Current.WindExposure01,
                    Current.AudioLeak01,
                    Current.InteriorDepth01,
                    Current.RoofExposure01,
                    nearest?.StableId ?? string.Empty,
                    nearest?.Openness01 ?? 0f);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextComposeTime)
            {
                return;
            }

            nextComposeTime = Time.unscaledTime + composeIntervalSeconds;
            Compose();
        }

        public LocalWeatherContext ComposeImmediately()
        {
            Compose();
            return Current;
        }

        private void Compose()
        {
            LocalWeatherContext interior = interiorResolver != null
                ? interiorResolver.Current
                : LocalWeatherContext.Outdoor;
            float roofExposure = roofResolver != null
                ? roofResolver.RoofExposure01
                : interior.IsInside ? 0f : 1f;
            bool roofCover = roofExposure <= 0.4f || interior.IsInside;
            if (interior.IsInside)
            {
                roofExposure = Mathf.Min(roofExposure, 0.05f);
            }

            LocalWeatherContext composed = new LocalWeatherContext(
                interior.CurrentZone,
                interior.IsInside,
                roofCover,
                interior.OutdoorExposure01,
                interior.PrecipitationExposure01 * roofExposure,
                interior.FogExposure01,
                interior.WindExposure01,
                interior.AudioLeak01,
                interior.ExteriorAudioExposure01,
                interior.ThunderExposure01,
                interior.InteriorDepth01,
                roofExposure,
                interior.DoorTransmission01,
                interior.ExposureCompensationEv);
            if (!Approximately(Current, composed))
            {
                Current = composed;
                ContextChanged?.Invoke(Current);
            }
            else
            {
                Current = composed;
            }
        }

        private static bool Approximately(
            in LocalWeatherContext left,
            in LocalWeatherContext right)
        {
            const float threshold = 0.001f;
            return string.Equals(
                       left.CurrentZoneId,
                       right.CurrentZoneId,
                       StringComparison.Ordinal) &&
                   left.IsInside == right.IsInside &&
                   left.HasRoofCover == right.HasRoofCover &&
                   Mathf.Abs(left.OutdoorExposure01 -
                             right.OutdoorExposure01) < threshold &&
                   Mathf.Abs(left.PrecipitationExposure01 -
                             right.PrecipitationExposure01) < threshold &&
                   Mathf.Abs(left.FogExposure01 - right.FogExposure01) < threshold &&
                   Mathf.Abs(left.WindExposure01 - right.WindExposure01) < threshold &&
                   Mathf.Abs(left.AudioLeak01 - right.AudioLeak01) < threshold &&
                   Mathf.Abs(left.ExteriorAudioExposure01 -
                             right.ExteriorAudioExposure01) < threshold &&
                   Mathf.Abs(left.ThunderExposure01 -
                             right.ThunderExposure01) < threshold &&
                   Mathf.Abs(left.InteriorDepth01 -
                             right.InteriorDepth01) < threshold &&
                   Mathf.Abs(left.RoofExposure01 -
                             right.RoofExposure01) < threshold &&
                   Mathf.Abs(left.DoorTransmission01 -
                             right.DoorTransmission01) < threshold &&
                   Mathf.Abs(left.ExposureCompensationEv -
                             right.ExposureCompensationEv) < threshold;
        }
    }
}
