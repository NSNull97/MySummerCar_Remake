using System;
using UnityEngine;

namespace MSC.Weather.Production
{
    public enum WeatherZoneKind
    {
        ClosedInterior = 0,
        SemiOpenInterior = 1,
        Shelter = 2,
        VehicleCabin = 3,
    }

    /// <summary>
    /// Independent local exposure factors. A shelter is deliberately not a
    /// binary indoor flag: rain, fog, wind, ambience and thunder can differ.
    /// </summary>
    public readonly struct WeatherExposureState : IEquatable<WeatherExposureState>
    {
        public WeatherExposureState(
            float enclosureFactor,
            float shelterFactor,
            float precipitationExposure,
            float fogExposure,
            float windExposure,
            float weatherAudioExposure,
            float thunderExposure,
            float portalExposure,
            float indoorFactor)
        {
            Validate01(enclosureFactor, nameof(enclosureFactor));
            Validate01(shelterFactor, nameof(shelterFactor));
            Validate01(precipitationExposure, nameof(precipitationExposure));
            Validate01(fogExposure, nameof(fogExposure));
            Validate01(windExposure, nameof(windExposure));
            Validate01(weatherAudioExposure, nameof(weatherAudioExposure));
            Validate01(thunderExposure, nameof(thunderExposure));
            Validate01(portalExposure, nameof(portalExposure));
            Validate01(indoorFactor, nameof(indoorFactor));

            EnclosureFactor = enclosureFactor;
            ShelterFactor = shelterFactor;
            PrecipitationExposure = precipitationExposure;
            FogExposure = fogExposure;
            WindExposure = windExposure;
            WeatherAudioExposure = weatherAudioExposure;
            ThunderExposure = thunderExposure;
            PortalExposure = portalExposure;
            IndoorFactor = indoorFactor;
        }

        public float EnclosureFactor { get; }
        public float ShelterFactor { get; }
        public float PrecipitationExposure { get; }
        public float FogExposure { get; }
        public float WindExposure { get; }
        public float WeatherAudioExposure { get; }
        public float ThunderExposure { get; }
        public float PortalExposure { get; }
        public float IndoorFactor { get; }

        public static WeatherExposureState Exterior => new WeatherExposureState(
            0f,
            0f,
            1f,
            1f,
            1f,
            1f,
            1f,
            0f,
            0f);

        public bool Equals(WeatherExposureState other) =>
            EnclosureFactor.Equals(other.EnclosureFactor) &&
            ShelterFactor.Equals(other.ShelterFactor) &&
            PrecipitationExposure.Equals(other.PrecipitationExposure) &&
            FogExposure.Equals(other.FogExposure) &&
            WindExposure.Equals(other.WindExposure) &&
            WeatherAudioExposure.Equals(other.WeatherAudioExposure) &&
            ThunderExposure.Equals(other.ThunderExposure) &&
            PortalExposure.Equals(other.PortalExposure) &&
            IndoorFactor.Equals(other.IndoorFactor);

        public override bool Equals(object obj) =>
            obj is WeatherExposureState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                EnclosureFactor,
                ShelterFactor,
                PrecipitationExposure,
                FogExposure,
                WindExposure,
                WeatherAudioExposure,
                ThunderExposure,
                PortalExposure);

        public static WeatherExposureState Lerp(
            in WeatherExposureState from,
            in WeatherExposureState to,
            float t) => new WeatherExposureState(
                Mathf.LerpUnclamped(from.EnclosureFactor, to.EnclosureFactor, t),
                Mathf.LerpUnclamped(from.ShelterFactor, to.ShelterFactor, t),
                Mathf.LerpUnclamped(
                    from.PrecipitationExposure,
                    to.PrecipitationExposure,
                    t),
                Mathf.LerpUnclamped(from.FogExposure, to.FogExposure, t),
                Mathf.LerpUnclamped(from.WindExposure, to.WindExposure, t),
                Mathf.LerpUnclamped(
                    from.WeatherAudioExposure,
                    to.WeatherAudioExposure,
                    t),
                Mathf.LerpUnclamped(from.ThunderExposure, to.ThunderExposure, t),
                Mathf.LerpUnclamped(from.PortalExposure, to.PortalExposure, t),
                Mathf.LerpUnclamped(from.IndoorFactor, to.IndoorFactor, t));

        private static void Validate01(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    public static class WeatherExposureMath
    {
        public static float NormalizeSignedAngle(float degrees)
        {
            if (!float.IsFinite(degrees))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees));
            }

            return Mathf.DeltaAngle(0f, degrees);
        }

        public static float CalculatePortalOpenness(
            float currentAngleDegrees,
            float closedAngleDegrees,
            float fullyOpenAngleDegrees)
        {
            float fullDelta = Mathf.DeltaAngle(
                closedAngleDegrees,
                fullyOpenAngleDegrees);
            if (Mathf.Abs(fullDelta) < 0.001f)
            {
                return 0f;
            }

            float currentDelta = Mathf.DeltaAngle(
                closedAngleDegrees,
                currentAngleDegrees);
            if (Mathf.Sign(currentDelta) != Mathf.Sign(fullDelta))
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Abs(currentDelta / fullDelta));
        }

        public static float CombinePortalInfluence(
            float combined,
            float next)
        {
            if (!float.IsFinite(combined) || !float.IsFinite(next))
            {
                throw new ArgumentOutOfRangeException(nameof(next));
            }

            combined = Mathf.Clamp01(combined);
            next = Mathf.Clamp01(next);
            return 1f - ((1f - combined) * (1f - next));
        }

        public static float ExponentialBlendFactor(
            float deltaSeconds,
            float smoothingSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f ||
                !float.IsFinite(smoothingSeconds) || smoothingSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            return smoothingSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-deltaSeconds / smoothingSeconds);
        }

        public static bool ShouldRetainZoneDuringExit(
            float secondsOutside,
            float hysteresisSeconds)
        {
            if (!float.IsFinite(secondsOutside) || secondsOutside < 0f ||
                !float.IsFinite(hysteresisSeconds) || hysteresisSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(secondsOutside));
            }

            return secondsOutside < hysteresisSeconds;
        }

        public static WeatherExposureState ApplyPortal(
            in WeatherExposureState closedState,
            float portalInfluence01)
        {
            float influence = Mathf.Clamp01(portalInfluence01);
            return new WeatherExposureState(
                closedState.EnclosureFactor * (1f - influence),
                closedState.ShelterFactor,
                Mathf.Lerp(
                    closedState.PrecipitationExposure,
                    1f,
                    influence * 0.85f),
                Mathf.Lerp(closedState.FogExposure, 1f, influence),
                Mathf.Lerp(closedState.WindExposure, 1f, influence),
                Mathf.Lerp(
                    closedState.WeatherAudioExposure,
                    1f,
                    influence),
                Mathf.Lerp(
                    closedState.ThunderExposure,
                    1f,
                    influence * 0.5f),
                influence,
                closedState.IndoorFactor * (1f - influence * 0.75f));
        }
    }

}
