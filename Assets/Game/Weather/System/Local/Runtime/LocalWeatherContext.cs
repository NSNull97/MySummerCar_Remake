using System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    public interface ILocalWeatherContextSource
    {
        LocalWeatherContext Current { get; }
        event Action<LocalWeatherContext> ContextChanged;
    }

    public readonly struct LocalWeatherZoneSettings
    {
        public LocalWeatherZoneSettings(
            float outdoorExposure01,
            float precipitationExposure01,
            float fogExposure01,
            float windExposure01,
            float audioLeak01,
            float exteriorAudioExposure01,
            float thunderExposure01,
            float exposureCompensationEv,
            float transitionDistanceMeters)
        {
            OutdoorExposure01 = Clamp01(outdoorExposure01);
            PrecipitationExposure01 = Clamp01(precipitationExposure01);
            FogExposure01 = Clamp01(fogExposure01);
            WindExposure01 = Clamp01(windExposure01);
            AudioLeak01 = Clamp01(audioLeak01);
            ExteriorAudioExposure01 = Clamp01(exteriorAudioExposure01);
            ThunderExposure01 = Clamp01(thunderExposure01);
            ExposureCompensationEv = IsFinite(exposureCompensationEv)
                ? exposureCompensationEv
                : 0f;
            TransitionDistanceMeters = IsFinite(transitionDistanceMeters)
                ? Mathf.Max(0.1f, transitionDistanceMeters)
                : 1f;
        }

        public float OutdoorExposure01 { get; }
        public float PrecipitationExposure01 { get; }
        public float FogExposure01 { get; }
        public float WindExposure01 { get; }
        public float AudioLeak01 { get; }
        public float ExteriorAudioExposure01 { get; }
        public float ThunderExposure01 { get; }
        public float ExposureCompensationEv { get; }
        public float TransitionDistanceMeters { get; }

        public static LocalWeatherZoneSettings ClosedInterior =>
            new LocalWeatherZoneSettings(
                0.04f,
                0f,
                0.04f,
                0.04f,
                0.12f,
                0.12f,
                0.58f,
                1.1f,
                2.2f);

        private static float Clamp01(float value) =>
            IsFinite(value) ? Mathf.Clamp01(value) : 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IInteriorZone
    {
        string StableId { get; }
        int Priority { get; }
        LocalWeatherZoneSettings Settings { get; }
        bool IsAvailable { get; }
        bool Contains(Vector3 worldPosition);
    }

    [Serializable]
    public struct LocalWeatherContext : IEquatable<LocalWeatherContext>
    {
        [NonSerialized] private IInteriorZone currentZone;
        [SerializeField] private string currentZoneId;
        [SerializeField] private bool isInside;
        [SerializeField] private bool hasRoofCover;
        [SerializeField, Range(0f, 1f)] private float outdoorExposure01;
        [SerializeField, Range(0f, 1f)] private float precipitationExposure01;
        [SerializeField, Range(0f, 1f)] private float fogExposure01;
        [SerializeField, Range(0f, 1f)] private float windExposure01;
        [SerializeField, Range(0f, 1f)] private float audioLeak01;
        [SerializeField, Range(0f, 1f)] private float exteriorAudioExposure01;
        [SerializeField, Range(0f, 1f)] private float thunderExposure01;
        [SerializeField, Range(0f, 1f)] private float interiorDepth01;
        [SerializeField, Range(0f, 1f)] private float roofExposure01;
        [SerializeField, Range(0f, 1f)] private float doorTransmission01;
        [SerializeField] private float exposureCompensationEv;

        public LocalWeatherContext(
            IInteriorZone zone,
            bool inside,
            bool roofCover,
            float outdoorExposure01,
            float precipitationExposure01,
            float fogExposure01,
            float windExposure01,
            float audioLeak01,
            float exteriorAudioExposure01,
            float thunderExposure01,
            float interiorDepth01,
            float roofExposure01,
            float doorTransmission01,
            float exposureCompensationEv)
        {
            currentZone = zone;
            currentZoneId = zone?.StableId ?? WeatherPortalSystem.OutdoorZoneId;
            isInside = inside;
            hasRoofCover = roofCover;
            this.outdoorExposure01 = Clamp01(outdoorExposure01);
            this.precipitationExposure01 = Clamp01(precipitationExposure01);
            this.fogExposure01 = Clamp01(fogExposure01);
            this.windExposure01 = Clamp01(windExposure01);
            this.audioLeak01 = Clamp01(audioLeak01);
            this.exteriorAudioExposure01 = Clamp01(exteriorAudioExposure01);
            this.thunderExposure01 = Clamp01(thunderExposure01);
            this.interiorDepth01 = Clamp01(interiorDepth01);
            this.roofExposure01 = Clamp01(roofExposure01);
            this.doorTransmission01 = Clamp01(doorTransmission01);
            this.exposureCompensationEv = IsFinite(exposureCompensationEv)
                ? exposureCompensationEv
                : 0f;
        }

        public IInteriorZone CurrentZone => currentZone;
        public string CurrentZoneId => currentZoneId ?? string.Empty;
        public bool IsInside => isInside;
        public bool HasRoofCover => hasRoofCover;
        public float OutdoorExposure01 => outdoorExposure01;
        public float PrecipitationExposure01 => precipitationExposure01;
        public float FogExposure01 => fogExposure01;
        public float WindExposure01 => windExposure01;
        public float AudioLeak01 => audioLeak01;
        public float ExteriorAudioExposure01 => exteriorAudioExposure01;
        public float ThunderExposure01 => thunderExposure01;
        public float InteriorDepth01 => interiorDepth01;
        public float RoofExposure01 => roofExposure01;
        public float DoorTransmission01 => doorTransmission01;
        public float ExposureCompensationEv => exposureCompensationEv;

        public static LocalWeatherContext Outdoor => new LocalWeatherContext(
            null,
            false,
            false,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1f,
            0f);

        public static LocalWeatherContext Lerp(
            in LocalWeatherContext from,
            in LocalWeatherContext to,
            float progress01)
        {
            float t = Mathf.Clamp01(progress01);
            IInteriorZone zone = t < 0.5f ? from.currentZone : to.currentZone;
            return new LocalWeatherContext(
                zone,
                t < 0.5f ? from.isInside : to.isInside,
                t < 0.5f ? from.hasRoofCover : to.hasRoofCover,
                Mathf.Lerp(from.outdoorExposure01, to.outdoorExposure01, t),
                Mathf.Lerp(
                    from.precipitationExposure01,
                    to.precipitationExposure01,
                    t),
                Mathf.Lerp(from.fogExposure01, to.fogExposure01, t),
                Mathf.Lerp(from.windExposure01, to.windExposure01, t),
                Mathf.Lerp(from.audioLeak01, to.audioLeak01, t),
                Mathf.Lerp(
                    from.exteriorAudioExposure01,
                    to.exteriorAudioExposure01,
                    t),
                Mathf.Lerp(from.thunderExposure01, to.thunderExposure01, t),
                Mathf.Lerp(from.interiorDepth01, to.interiorDepth01, t),
                Mathf.Lerp(from.roofExposure01, to.roofExposure01, t),
                Mathf.Lerp(from.doorTransmission01, to.doorTransmission01, t),
                Mathf.Lerp(
                    from.exposureCompensationEv,
                    to.exposureCompensationEv,
                    t));
        }

        public bool Equals(LocalWeatherContext other) =>
            string.Equals(currentZoneId, other.currentZoneId, StringComparison.Ordinal) &&
            isInside == other.isInside &&
            hasRoofCover == other.hasRoofCover &&
            outdoorExposure01.Equals(other.outdoorExposure01) &&
            precipitationExposure01.Equals(other.precipitationExposure01) &&
            fogExposure01.Equals(other.fogExposure01) &&
            windExposure01.Equals(other.windExposure01) &&
            audioLeak01.Equals(other.audioLeak01) &&
            exteriorAudioExposure01.Equals(other.exteriorAudioExposure01) &&
            thunderExposure01.Equals(other.thunderExposure01) &&
            interiorDepth01.Equals(other.interiorDepth01) &&
            roofExposure01.Equals(other.roofExposure01) &&
            doorTransmission01.Equals(other.doorTransmission01) &&
            exposureCompensationEv.Equals(other.exposureCompensationEv);

        public override bool Equals(object obj) =>
            obj is LocalWeatherContext other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(currentZoneId ?? string.Empty);
                hash = (hash * 397) ^ outdoorExposure01.GetHashCode();
                hash = (hash * 397) ^ precipitationExposure01.GetHashCode();
                hash = (hash * 397) ^ fogExposure01.GetHashCode();
                hash = (hash * 397) ^ windExposure01.GetHashCode();
                hash = (hash * 397) ^ audioLeak01.GetHashCode();
                hash = (hash * 397) ^ interiorDepth01.GetHashCode();
                return hash;
            }
        }

        private static float Clamp01(float value) =>
            IsFinite(value) ? Mathf.Clamp01(value) : 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
