using System;
using UnityEngine;

namespace MSC.Weather.Domain
{
    /// <summary>
    /// Vendor-neutral, normalized environment read model. Enviro, HDRP, Wwise,
    /// gameplay presentation and future adapters consume this state instead of
    /// querying one another.
    /// </summary>
    public readonly struct WeatherRuntimeState : IEquatable<WeatherRuntimeState>
    {
        public WeatherRuntimeState(
            string currentWeatherId,
            string targetWeatherId,
            float weatherTransition01,
            float timeOfDay01,
            bool isDay,
            float cloudCoverage01,
            float precipitation01,
            float rain01,
            float snow01,
            float storm01,
            float lightningActivity01,
            float fogIntensity01,
            float visibilityMeters,
            float humidity01,
            bool humidityIsFallback,
            float wetness01,
            float windSpeed01,
            float windSpeedMetersPerSecond,
            Vector3 windDirection,
            float temperatureCelsius,
            bool temperatureIsAvailable,
            float sunVisibility01,
            float ambientDarkness01)
        {
            if (!WeatherStateId.IsValid(currentWeatherId))
            {
                throw new ArgumentException(
                    "Current weather requires a stable project ID.",
                    nameof(currentWeatherId));
            }

            if (!WeatherStateId.IsValid(targetWeatherId))
            {
                throw new ArgumentException(
                    "Target weather requires a stable project ID.",
                    nameof(targetWeatherId));
            }

            Validate01(weatherTransition01, nameof(weatherTransition01));
            Validate01(timeOfDay01, nameof(timeOfDay01));
            Validate01(cloudCoverage01, nameof(cloudCoverage01));
            Validate01(precipitation01, nameof(precipitation01));
            Validate01(rain01, nameof(rain01));
            Validate01(snow01, nameof(snow01));
            Validate01(storm01, nameof(storm01));
            Validate01(lightningActivity01, nameof(lightningActivity01));
            Validate01(fogIntensity01, nameof(fogIntensity01));
            Validate01(humidity01, nameof(humidity01));
            Validate01(wetness01, nameof(wetness01));
            Validate01(windSpeed01, nameof(windSpeed01));
            Validate01(sunVisibility01, nameof(sunVisibility01));
            Validate01(ambientDarkness01, nameof(ambientDarkness01));
            if (!float.IsFinite(visibilityMeters) || visibilityMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibilityMeters));
            }

            if (!float.IsFinite(windSpeedMetersPerSecond) ||
                windSpeedMetersPerSecond < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(windSpeedMetersPerSecond));
            }

            if (!IsFinite(windDirection))
            {
                throw new ArgumentOutOfRangeException(nameof(windDirection));
            }

            if (!float.IsFinite(temperatureCelsius))
            {
                throw new ArgumentOutOfRangeException(nameof(temperatureCelsius));
            }

            CurrentWeatherId = currentWeatherId;
            TargetWeatherId = targetWeatherId;
            WeatherTransition01 = weatherTransition01;
            TimeOfDay01 = timeOfDay01;
            IsDay = isDay;
            CloudCoverage01 = cloudCoverage01;
            Precipitation01 = precipitation01;
            Rain01 = rain01;
            Snow01 = snow01;
            Storm01 = storm01;
            LightningActivity01 = lightningActivity01;
            FogIntensity01 = fogIntensity01;
            VisibilityMeters = visibilityMeters;
            Humidity01 = humidity01;
            HumidityIsFallback = humidityIsFallback;
            Wetness01 = wetness01;
            WindSpeed01 = windSpeed01;
            WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
            WindDirection = windDirection.sqrMagnitude > 0.000001f
                ? windDirection.normalized
                : Vector3.forward;
            TemperatureCelsius = temperatureCelsius;
            TemperatureIsAvailable = temperatureIsAvailable;
            SunVisibility01 = sunVisibility01;
            AmbientDarkness01 = ambientDarkness01;
        }

        public string CurrentWeatherId { get; }
        public string TargetWeatherId { get; }
        public float WeatherTransition01 { get; }
        public float TimeOfDay01 { get; }
        public bool IsDay { get; }
        public bool IsNight => !IsDay;
        public float CloudCoverage01 { get; }
        public float Precipitation01 { get; }
        public float Rain01 { get; }
        public float Snow01 { get; }
        public float Storm01 { get; }
        public float LightningActivity01 { get; }
        public float FogIntensity01 { get; }
        public float VisibilityMeters { get; }
        public float Humidity01 { get; }
        public bool HumidityIsFallback { get; }
        public float Wetness01 { get; }
        public float WindSpeed01 { get; }
        public float WindSpeedMetersPerSecond { get; }
        public Vector3 WindDirection { get; }
        public float TemperatureCelsius { get; }
        public bool TemperatureIsAvailable { get; }
        public float SunVisibility01 { get; }
        public float AmbientDarkness01 { get; }

        public bool IsValid =>
            WeatherStateId.IsValid(CurrentWeatherId) &&
            WeatherStateId.IsValid(TargetWeatherId) &&
            float.IsFinite(VisibilityMeters) && VisibilityMeters > 0f;

        public bool Equals(WeatherRuntimeState other) =>
            string.Equals(
                CurrentWeatherId,
                other.CurrentWeatherId,
                StringComparison.Ordinal) &&
            string.Equals(
                TargetWeatherId,
                other.TargetWeatherId,
                StringComparison.Ordinal) &&
            WeatherTransition01.Equals(other.WeatherTransition01) &&
            TimeOfDay01.Equals(other.TimeOfDay01) &&
            IsDay == other.IsDay &&
            CloudCoverage01.Equals(other.CloudCoverage01) &&
            Precipitation01.Equals(other.Precipitation01) &&
            Rain01.Equals(other.Rain01) &&
            Snow01.Equals(other.Snow01) &&
            Storm01.Equals(other.Storm01) &&
            LightningActivity01.Equals(other.LightningActivity01) &&
            FogIntensity01.Equals(other.FogIntensity01) &&
            VisibilityMeters.Equals(other.VisibilityMeters) &&
            Humidity01.Equals(other.Humidity01) &&
            Wetness01.Equals(other.Wetness01) &&
            WindSpeedMetersPerSecond.Equals(
                other.WindSpeedMetersPerSecond) &&
            WindDirection.Equals(other.WindDirection) &&
            TemperatureCelsius.Equals(other.TemperatureCelsius) &&
            SunVisibility01.Equals(other.SunVisibility01) &&
            AmbientDarkness01.Equals(other.AmbientDarkness01);

        public override bool Equals(object obj) =>
            obj is WeatherRuntimeState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                CurrentWeatherId,
                TargetWeatherId,
                WeatherTransition01,
                TimeOfDay01,
                CloudCoverage01,
                Precipitation01,
                FogIntensity01,
                VisibilityMeters);

        private static void Validate01(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "Value must be finite and normalized to [0, 1].");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
