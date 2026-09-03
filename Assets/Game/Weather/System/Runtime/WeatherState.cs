using System;
using UnityEngine;

namespace MSC.Weather.System
{
    /// <summary>
    /// Vendor-neutral continuous atmosphere state. Units are metres, seconds,
    /// metres per second, degrees Celsius and normalized 0..1 factors unless
    /// explicitly stated otherwise.
    /// </summary>
    [Serializable]
    public struct WeatherState : IEquatable<WeatherState>
    {
        [SerializeField, Range(0f, 1f)] private float cloudCoverage01;
        [SerializeField, Range(0f, 1f)] private float cloudDensity01;
        [SerializeField, Range(0f, 1f)] private float cloudErosion01;
        [SerializeField, Range(0f, 1f)] private float cloudShadowStrength01;
        [SerializeField, Range(0f, 1f)] private float precipitation01;
        [SerializeField, Range(0f, 1f)] private float drizzle01;
        [SerializeField, Range(0f, 1f)] private float thunderIntensity01;
        [SerializeField, Range(0f, 1f)] private float fogDensity01;
        [SerializeField, Min(1f)] private float fogDistanceMeters;
        [SerializeField, Range(0f, 1f)] private float atmosphericHaze01;
        [SerializeField, Min(0f)] private float windSpeedMetersPerSecond;
        [SerializeField] private Vector2 windDirectionXZ;
        [SerializeField, Range(0f, 1f)] private float windGustiness01;
        [SerializeField, Range(0f, 1f)] private float surfaceWetness01;
        [SerializeField, Range(0f, 1f)] private float puddleAmount01;
        [SerializeField, Range(0f, 1f)] private float sunVisibility01;
        [SerializeField, Range(0f, 2f)] private float skyBrightnessMultiplier;
        [SerializeField, Range(0f, 2f)] private float ambientLightMultiplier;
        [SerializeField] private float temperatureCelsius;
        [SerializeField, Range(0f, 1f)] private float humidity01;

        public WeatherState(
            float cloudCoverage01,
            float cloudDensity01,
            float cloudErosion01,
            float cloudShadowStrength01,
            float precipitation01,
            float drizzle01,
            float thunderIntensity01,
            float fogDensity01,
            float fogDistanceMeters,
            float atmosphericHaze01,
            float windSpeedMetersPerSecond,
            Vector2 windDirectionXZ,
            float windGustiness01,
            float surfaceWetness01,
            float puddleAmount01,
            float sunVisibility01,
            float skyBrightnessMultiplier,
            float ambientLightMultiplier,
            float temperatureCelsius,
            float humidity01)
        {
            this.cloudCoverage01 = Require01(cloudCoverage01, nameof(cloudCoverage01));
            this.cloudDensity01 = Require01(cloudDensity01, nameof(cloudDensity01));
            this.cloudErosion01 = Require01(cloudErosion01, nameof(cloudErosion01));
            this.cloudShadowStrength01 = Require01(
                cloudShadowStrength01,
                nameof(cloudShadowStrength01));
            this.precipitation01 = Require01(precipitation01, nameof(precipitation01));
            this.drizzle01 = Require01(drizzle01, nameof(drizzle01));
            this.thunderIntensity01 = Require01(
                thunderIntensity01,
                nameof(thunderIntensity01));
            this.fogDensity01 = Require01(fogDensity01, nameof(fogDensity01));
            this.fogDistanceMeters = RequirePositive(
                fogDistanceMeters,
                nameof(fogDistanceMeters));
            this.atmosphericHaze01 = Require01(
                atmosphericHaze01,
                nameof(atmosphericHaze01));
            this.windSpeedMetersPerSecond = RequireNonNegative(
                windSpeedMetersPerSecond,
                nameof(windSpeedMetersPerSecond));
            this.windDirectionXZ = NormalizeDirection(windDirectionXZ);
            this.windGustiness01 = Require01(
                windGustiness01,
                nameof(windGustiness01));
            this.surfaceWetness01 = Require01(surfaceWetness01, nameof(surfaceWetness01));
            this.puddleAmount01 = Require01(puddleAmount01, nameof(puddleAmount01));
            this.sunVisibility01 = Require01(sunVisibility01, nameof(sunVisibility01));
            this.skyBrightnessMultiplier = RequireRange(
                skyBrightnessMultiplier,
                0f,
                2f,
                nameof(skyBrightnessMultiplier));
            this.ambientLightMultiplier = RequireRange(
                ambientLightMultiplier,
                0f,
                2f,
                nameof(ambientLightMultiplier));
            this.temperatureCelsius = RequireFinite(
                temperatureCelsius,
                nameof(temperatureCelsius));
            this.humidity01 = Require01(humidity01, nameof(humidity01));
        }

        public float CloudCoverage01 => cloudCoverage01;
        public float CloudDensity01 => cloudDensity01;
        public float CloudErosion01 => cloudErosion01;
        public float CloudShadowStrength01 => cloudShadowStrength01;
        public float Precipitation01 => precipitation01;
        public float Drizzle01 => drizzle01;
        public float ThunderIntensity01 => thunderIntensity01;
        public float FogDensity01 => fogDensity01;
        public float FogDistanceMeters => fogDistanceMeters;
        public float AtmosphericHaze01 => atmosphericHaze01;
        public float WindSpeedMetersPerSecond => windSpeedMetersPerSecond;
        public Vector2 WindDirectionXZ => windDirectionXZ;
        public float WindGustiness01 => windGustiness01;
        public float SurfaceWetness01 => surfaceWetness01;
        public float PuddleAmount01 => puddleAmount01;
        public float SunVisibility01 => sunVisibility01;
        public float SkyBrightnessMultiplier => skyBrightnessMultiplier;
        public float AmbientLightMultiplier => ambientLightMultiplier;
        public float TemperatureCelsius => temperatureCelsius;
        public float Humidity01 => humidity01;

        public bool IsValid =>
            Is01(cloudCoverage01) &&
            Is01(cloudDensity01) &&
            Is01(cloudErosion01) &&
            Is01(cloudShadowStrength01) &&
            Is01(precipitation01) &&
            Is01(drizzle01) &&
            Is01(thunderIntensity01) &&
            Is01(fogDensity01) &&
            IsFinite(fogDistanceMeters) && fogDistanceMeters > 0f &&
            Is01(atmosphericHaze01) &&
            IsFinite(windSpeedMetersPerSecond) && windSpeedMetersPerSecond >= 0f &&
            IsFinite(windDirectionXZ.x) && IsFinite(windDirectionXZ.y) &&
            Is01(windGustiness01) &&
            Is01(surfaceWetness01) &&
            Is01(puddleAmount01) &&
            Is01(sunVisibility01) &&
            IsFinite(skyBrightnessMultiplier) &&
            skyBrightnessMultiplier >= 0f && skyBrightnessMultiplier <= 2f &&
            IsFinite(ambientLightMultiplier) &&
            ambientLightMultiplier >= 0f && ambientLightMultiplier <= 2f &&
            IsFinite(temperatureCelsius) &&
            Is01(humidity01);

        public WeatherState WithSurface(float wetness01, float puddles01) =>
            new WeatherState(
                cloudCoverage01,
                cloudDensity01,
                cloudErosion01,
                cloudShadowStrength01,
                precipitation01,
                drizzle01,
                thunderIntensity01,
                fogDensity01,
                fogDistanceMeters,
                atmosphericHaze01,
                windSpeedMetersPerSecond,
                windDirectionXZ,
                windGustiness01,
                wetness01,
                puddles01,
                sunVisibility01,
                skyBrightnessMultiplier,
                ambientLightMultiplier,
                temperatureCelsius,
                humidity01);

        public static WeatherState Lerp(
            in WeatherState from,
            in WeatherState to,
            float progress01,
            WeatherTransitionCurves curves = null)
        {
            if (!from.IsValid || !to.IsValid)
            {
                throw new ArgumentException("Weather blend endpoints must be valid.");
            }

            float t = Mathf.Clamp01(progress01);
            float clouds = curves?.EvaluateClouds(t) ?? t;
            float precipitation = curves?.EvaluatePrecipitation(t) ?? t;
            float fog = curves?.EvaluateFog(t) ?? t;
            float wind = curves?.EvaluateWind(t) ?? t;
            float light = curves?.EvaluateLighting(t) ?? t;
            float climate = curves?.EvaluateClimate(t) ?? t;
            float surface = curves?.EvaluateSurface(t) ?? t;

            Vector2 direction = Vector2.Lerp(from.windDirectionXZ, to.windDirectionXZ, wind);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = t < 0.5f ? from.windDirectionXZ : to.windDirectionXZ;
            }
            else
            {
                direction.Normalize();
            }

            return new WeatherState(
                Mathf.Lerp(from.cloudCoverage01, to.cloudCoverage01, clouds),
                Mathf.Lerp(from.cloudDensity01, to.cloudDensity01, clouds),
                Mathf.Lerp(from.cloudErosion01, to.cloudErosion01, clouds),
                Mathf.Lerp(
                    from.cloudShadowStrength01,
                    to.cloudShadowStrength01,
                    clouds),
                Mathf.Lerp(from.precipitation01, to.precipitation01, precipitation),
                Mathf.Lerp(from.drizzle01, to.drizzle01, precipitation),
                Mathf.Lerp(
                    from.thunderIntensity01,
                    to.thunderIntensity01,
                    precipitation),
                Mathf.Lerp(from.fogDensity01, to.fogDensity01, fog),
                Mathf.Lerp(from.fogDistanceMeters, to.fogDistanceMeters, fog),
                Mathf.Lerp(from.atmosphericHaze01, to.atmosphericHaze01, fog),
                Mathf.Lerp(
                    from.windSpeedMetersPerSecond,
                    to.windSpeedMetersPerSecond,
                    wind),
                direction,
                Mathf.Lerp(from.windGustiness01, to.windGustiness01, wind),
                Mathf.Lerp(from.surfaceWetness01, to.surfaceWetness01, surface),
                Mathf.Lerp(from.puddleAmount01, to.puddleAmount01, surface),
                Mathf.Lerp(from.sunVisibility01, to.sunVisibility01, light),
                Mathf.Lerp(
                    from.skyBrightnessMultiplier,
                    to.skyBrightnessMultiplier,
                    light),
                Mathf.Lerp(
                    from.ambientLightMultiplier,
                    to.ambientLightMultiplier,
                    light),
                Mathf.Lerp(from.temperatureCelsius, to.temperatureCelsius, climate),
                Mathf.Lerp(from.humidity01, to.humidity01, climate));
        }

        public bool Equals(WeatherState other) =>
            cloudCoverage01.Equals(other.cloudCoverage01) &&
            cloudDensity01.Equals(other.cloudDensity01) &&
            cloudErosion01.Equals(other.cloudErosion01) &&
            cloudShadowStrength01.Equals(other.cloudShadowStrength01) &&
            precipitation01.Equals(other.precipitation01) &&
            drizzle01.Equals(other.drizzle01) &&
            thunderIntensity01.Equals(other.thunderIntensity01) &&
            fogDensity01.Equals(other.fogDensity01) &&
            fogDistanceMeters.Equals(other.fogDistanceMeters) &&
            atmosphericHaze01.Equals(other.atmosphericHaze01) &&
            windSpeedMetersPerSecond.Equals(other.windSpeedMetersPerSecond) &&
            windDirectionXZ.Equals(other.windDirectionXZ) &&
            windGustiness01.Equals(other.windGustiness01) &&
            surfaceWetness01.Equals(other.surfaceWetness01) &&
            puddleAmount01.Equals(other.puddleAmount01) &&
            sunVisibility01.Equals(other.sunVisibility01) &&
            skyBrightnessMultiplier.Equals(other.skyBrightnessMultiplier) &&
            ambientLightMultiplier.Equals(other.ambientLightMultiplier) &&
            temperatureCelsius.Equals(other.temperatureCelsius) &&
            humidity01.Equals(other.humidity01);

        public override bool Equals(object obj) =>
            obj is WeatherState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = cloudCoverage01.GetHashCode();
                hash = (hash * 397) ^ precipitation01.GetHashCode();
                hash = (hash * 397) ^ fogDensity01.GetHashCode();
                hash = (hash * 397) ^ windSpeedMetersPerSecond.GetHashCode();
                hash = (hash * 397) ^ surfaceWetness01.GetHashCode();
                hash = (hash * 397) ^ temperatureCelsius.GetHashCode();
                hash = (hash * 397) ^ humidity01.GetHashCode();
                return hash;
            }
        }

        public static WeatherState FinnishSummerBaseline => new WeatherState(
            0.35f,
            0.32f,
            0.72f,
            0.25f,
            0f,
            0f,
            0f,
            0.04f,
            18000f,
            0.12f,
            3f,
            new Vector2(-0.7f, -0.7f),
            0.15f,
            0.1f,
            0.02f,
            0.82f,
            0.96f,
            0.92f,
            15f,
            0.58f);

        private static Vector2 NormalizeDirection(Vector2 value)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return value.sqrMagnitude > 0.000001f ? value.normalized : Vector2.up;
        }

        private static float Require01(float value, string name) =>
            RequireRange(value, 0f, 1f, name);

        private static float RequirePositive(float value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static float RequireNonNegative(float value, string name)
        {
            RequireFinite(value, name);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static float RequireRange(float value, float minimum, float maximum, string name)
        {
            RequireFinite(value, name);
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static float RequireFinite(float value, string name)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return value;
        }

        private static bool Is01(float value) =>
            IsFinite(value) && value >= 0f && value <= 1f;

        internal static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
