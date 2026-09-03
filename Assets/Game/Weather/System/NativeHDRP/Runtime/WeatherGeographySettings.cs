using System;
using UnityEngine;

namespace MSC.Weather.System.NativeHDRP
{
    [CreateAssetMenu(
        fileName = "WeatherGeographySettings",
        menuName = "MSC Remake/Environment/Weather System/Geography")]
    public sealed class WeatherGeographySettings : ScriptableObject
    {
        [SerializeField, Range(-90f, 90f)] private float latitude = 62.4f;
        [SerializeField, Range(-180f, 180f)] private float longitude = 25.7f;
        [SerializeField, Range(1, 366)] private int fallbackDayOfYear = 220;
        [SerializeField, Range(-12f, 14f)] private float timeZoneHours = 3f;
        [SerializeField, Range(0f, 360f)] private float northDirectionDegrees;

        public float Latitude => latitude;
        public float Longitude => longitude;
        public int FallbackDayOfYear => fallbackDayOfYear;
        public float TimeZoneHours => timeZoneHours;
        public float NorthDirectionDegrees => northDirectionDegrees;

        public SolarPosition Calculate(
            int year,
            int month,
            int day,
            float normalizedTimeOfDay01)
        {
            int dayOfYear = fallbackDayOfYear;
            if (year >= 1 && year <= 9999 && month >= 1 && month <= 12 &&
                day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                dayOfYear = new DateTime(year, month, day).DayOfYear;
            }

            return SolarPositionCalculator.Calculate(
                latitude,
                longitude,
                dayOfYear,
                timeZoneHours,
                normalizedTimeOfDay01,
                northDirectionDegrees);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            float authoredLatitude,
            float authoredLongitude,
            int authoredFallbackDayOfYear,
            float authoredTimeZoneHours,
            float authoredNorthDirectionDegrees)
        {
            latitude = Mathf.Clamp(authoredLatitude, -90f, 90f);
            longitude = Mathf.Clamp(authoredLongitude, -180f, 180f);
            fallbackDayOfYear = Mathf.Clamp(authoredFallbackDayOfYear, 1, 366);
            timeZoneHours = Mathf.Clamp(authoredTimeZoneHours, -12f, 14f);
            northDirectionDegrees = Mathf.Repeat(
                authoredNorthDirectionDegrees,
                360f);
        }
#endif
    }

    public readonly struct SolarPosition
    {
        public SolarPosition(float elevationDegrees, float azimuthDegrees)
        {
            ElevationDegrees = elevationDegrees;
            AzimuthDegrees = Mathf.Repeat(azimuthDegrees, 360f);
        }

        public float ElevationDegrees { get; }

        /// <summary>Clockwise degrees from the authored project north.</summary>
        public float AzimuthDegrees { get; }

        /// <summary>
        /// Unity directional lights emit along their forward axis. Solar
        /// azimuth describes the opposite vector: the direction from the
        /// observer towards the sun. A zero-degree elevation must therefore
        /// produce a horizontal light and a ninety-degree elevation a light
        /// pointing straight down.
        /// </summary>
        public Quaternion ToDirectionalLightRotation() =>
            Quaternion.Euler(
                ElevationDegrees,
                AzimuthDegrees + 180f,
                0f);
    }

    public static class SolarPositionCalculator
    {
        /// <summary>
        /// NOAA-style fractional-year approximation. Accuracy is ample for a
        /// game sun while keeping the calculation deterministic and testable.
        /// </summary>
        public static SolarPosition Calculate(
            float latitudeDegrees,
            float longitudeDegrees,
            int dayOfYear,
            float timeZoneHours,
            float normalizedTimeOfDay01,
            float northDirectionDegrees)
        {
            if (!IsFinite(latitudeDegrees) || latitudeDegrees < -90f ||
                latitudeDegrees > 90f ||
                !IsFinite(longitudeDegrees) || longitudeDegrees < -180f ||
                longitudeDegrees > 180f ||
                dayOfYear < 1 || dayOfYear > 366 ||
                !IsFinite(timeZoneHours) ||
                !IsFinite(normalizedTimeOfDay01) ||
                !IsFinite(northDirectionDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(latitudeDegrees));
            }

            double hour = Mathf.Repeat(normalizedTimeOfDay01, 1f) * 24d;
            double gamma = 2d * Math.PI / 365d *
                (dayOfYear - 1d + (hour - 12d) / 24d);
            double equationOfTime = 229.18d *
                (0.000075d + 0.001868d * Math.Cos(gamma) -
                 0.032077d * Math.Sin(gamma) -
                 0.014615d * Math.Cos(2d * gamma) -
                 0.040849d * Math.Sin(2d * gamma));
            double declination =
                0.006918d -
                0.399912d * Math.Cos(gamma) +
                0.070257d * Math.Sin(gamma) -
                0.006758d * Math.Cos(2d * gamma) +
                0.000907d * Math.Sin(2d * gamma) -
                0.002697d * Math.Cos(3d * gamma) +
                0.00148d * Math.Sin(3d * gamma);
            double timeOffsetMinutes = equationOfTime +
                4d * longitudeDegrees - 60d * timeZoneHours;
            double trueSolarMinutes = hour * 60d + timeOffsetMinutes;
            double hourAngleDegrees = trueSolarMinutes / 4d - 180d;
            double hourAngle = hourAngleDegrees * Math.PI / 180d;
            double latitude = latitudeDegrees * Math.PI / 180d;
            double cosZenith = Math.Sin(latitude) * Math.Sin(declination) +
                Math.Cos(latitude) * Math.Cos(declination) *
                Math.Cos(hourAngle);
            cosZenith = Math.Max(-1d, Math.Min(1d, cosZenith));
            double zenith = Math.Acos(cosZenith);
            double elevation = 90d - zenith * 180d / Math.PI;
            double azimuth = Math.Atan2(
                Math.Sin(hourAngle),
                Math.Cos(hourAngle) * Math.Sin(latitude) -
                Math.Tan(declination) * Math.Cos(latitude));
            double azimuthDegrees = azimuth * 180d / Math.PI + 180d +
                northDirectionDegrees;
            return new SolarPosition(
                (float)elevation,
                (float)azimuthDegrees);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
