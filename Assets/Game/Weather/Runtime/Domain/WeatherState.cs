using System;

namespace MSC.Weather.Domain
{
    public enum WeatherPrecipitationType
    {
        None = 0,
        Drizzle = 1,
        Rain = 2,
    }

    /// <summary>
    /// Immutable logical weather values. Units are metres, seconds, degrees Celsius and m/s.
    /// </summary>
    public readonly struct WeatherState : IEquatable<WeatherState>
    {
        public WeatherState(
            WeatherStateId id,
            string presentationBindingId,
            float cloudCoverage01,
            WeatherPrecipitationType precipitationType,
            float precipitationIntensity01,
            float fogIntensity01,
            float visibilityMeters,
            float windDirectionDegrees,
            float windSpeedMetersPerSecond,
            float windGust01,
            float temperatureCelsius,
            float ambientReadability01,
            float lightningRisk01,
            float lightningIntensity01,
            float wetnessInput01,
            float dryingModifier)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A weather state requires a stable ID.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(presentationBindingId))
            {
                throw new ArgumentException("A weather state requires a stable presentation binding ID.", nameof(presentationBindingId));
            }

            Validate01(cloudCoverage01, nameof(cloudCoverage01));
            Validate01(precipitationIntensity01, nameof(precipitationIntensity01));
            Validate01(fogIntensity01, nameof(fogIntensity01));
            ValidateFinitePositive(visibilityMeters, nameof(visibilityMeters));
            ValidateFinite(windDirectionDegrees, nameof(windDirectionDegrees));
            ValidateFiniteNonNegative(windSpeedMetersPerSecond, nameof(windSpeedMetersPerSecond));
            Validate01(windGust01, nameof(windGust01));
            ValidateFinite(temperatureCelsius, nameof(temperatureCelsius));
            Validate01(ambientReadability01, nameof(ambientReadability01));
            Validate01(lightningRisk01, nameof(lightningRisk01));
            Validate01(lightningIntensity01, nameof(lightningIntensity01));
            Validate01(wetnessInput01, nameof(wetnessInput01));
            ValidateFiniteNonNegative(dryingModifier, nameof(dryingModifier));

            if (precipitationType == WeatherPrecipitationType.None && precipitationIntensity01 > 0f)
            {
                throw new ArgumentException("Precipitation intensity must be zero when precipitation type is None.");
            }

            Id = id;
            PresentationBindingId = presentationBindingId;
            CloudCoverage01 = cloudCoverage01;
            PrecipitationType = precipitationType;
            PrecipitationIntensity01 = precipitationIntensity01;
            FogIntensity01 = fogIntensity01;
            VisibilityMeters = visibilityMeters;
            WindDirectionDegrees = NormalizeDegrees(windDirectionDegrees);
            WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
            WindGust01 = windGust01;
            TemperatureCelsius = temperatureCelsius;
            AmbientReadability01 = ambientReadability01;
            LightningRisk01 = lightningRisk01;
            LightningIntensity01 = lightningIntensity01;
            WetnessInput01 = wetnessInput01;
            DryingModifier = dryingModifier;
        }

        public WeatherStateId Id { get; }

        public string PresentationBindingId { get; }

        public float CloudCoverage01 { get; }

        public WeatherPrecipitationType PrecipitationType { get; }

        public float PrecipitationIntensity01 { get; }

        public float FogIntensity01 { get; }

        public float VisibilityMeters { get; }

        public float WindDirectionDegrees { get; }

        public float WindSpeedMetersPerSecond { get; }

        public float WindGust01 { get; }

        public float TemperatureCelsius { get; }

        public float AmbientReadability01 { get; }

        public float LightningRisk01 { get; }

        public float LightningIntensity01 { get; }

        public float WetnessInput01 { get; }

        public float DryingModifier { get; }

        public static WeatherState Lerp(in WeatherState from, in WeatherState to, float progress01)
        {
            Validate01(progress01, nameof(progress01));
            float precipitationIntensity = LerpValue(
                from.PrecipitationIntensity01,
                to.PrecipitationIntensity01,
                progress01);
            WeatherPrecipitationType precipitationType;
            if (precipitationIntensity <= 0f)
            {
                precipitationType = WeatherPrecipitationType.None;
                precipitationIntensity = 0f;
            }
            else if (from.PrecipitationType == WeatherPrecipitationType.None)
            {
                precipitationType = to.PrecipitationType;
            }
            else if (to.PrecipitationType == WeatherPrecipitationType.None)
            {
                precipitationType = from.PrecipitationType;
            }
            else
            {
                precipitationType = progress01 < 0.5f
                    ? from.PrecipitationType
                    : to.PrecipitationType;
            }

            return new WeatherState(
                progress01 < 0.5f ? from.Id : to.Id,
                progress01 < 0.5f ? from.PresentationBindingId : to.PresentationBindingId,
                LerpValue(from.CloudCoverage01, to.CloudCoverage01, progress01),
                precipitationType,
                precipitationIntensity,
                LerpValue(from.FogIntensity01, to.FogIntensity01, progress01),
                LerpValue(from.VisibilityMeters, to.VisibilityMeters, progress01),
                LerpAngle(from.WindDirectionDegrees, to.WindDirectionDegrees, progress01),
                LerpValue(from.WindSpeedMetersPerSecond, to.WindSpeedMetersPerSecond, progress01),
                LerpValue(from.WindGust01, to.WindGust01, progress01),
                LerpValue(from.TemperatureCelsius, to.TemperatureCelsius, progress01),
                LerpValue(from.AmbientReadability01, to.AmbientReadability01, progress01),
                LerpValue(from.LightningRisk01, to.LightningRisk01, progress01),
                LerpValue(from.LightningIntensity01, to.LightningIntensity01, progress01),
                LerpValue(from.WetnessInput01, to.WetnessInput01, progress01),
                LerpValue(from.DryingModifier, to.DryingModifier, progress01));
        }

        public bool Equals(WeatherState other) =>
            Id.Equals(other.Id) &&
            string.Equals(PresentationBindingId, other.PresentationBindingId, StringComparison.Ordinal) &&
            CloudCoverage01.Equals(other.CloudCoverage01) &&
            PrecipitationType == other.PrecipitationType &&
            PrecipitationIntensity01.Equals(other.PrecipitationIntensity01) &&
            FogIntensity01.Equals(other.FogIntensity01) &&
            VisibilityMeters.Equals(other.VisibilityMeters) &&
            WindDirectionDegrees.Equals(other.WindDirectionDegrees) &&
            WindSpeedMetersPerSecond.Equals(other.WindSpeedMetersPerSecond) &&
            WindGust01.Equals(other.WindGust01) &&
            TemperatureCelsius.Equals(other.TemperatureCelsius) &&
            AmbientReadability01.Equals(other.AmbientReadability01) &&
            LightningRisk01.Equals(other.LightningRisk01) &&
            LightningIntensity01.Equals(other.LightningIntensity01) &&
            WetnessInput01.Equals(other.WetnessInput01) &&
            DryingModifier.Equals(other.DryingModifier);

        public override bool Equals(object obj) => obj is WeatherState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(PresentationBindingId ?? string.Empty);
                hash = (hash * 397) ^ CloudCoverage01.GetHashCode();
                hash = (hash * 397) ^ (int)PrecipitationType;
                hash = (hash * 397) ^ PrecipitationIntensity01.GetHashCode();
                hash = (hash * 397) ^ FogIntensity01.GetHashCode();
                hash = (hash * 397) ^ VisibilityMeters.GetHashCode();
                hash = (hash * 397) ^ WindDirectionDegrees.GetHashCode();
                hash = (hash * 397) ^ WindSpeedMetersPerSecond.GetHashCode();
                hash = (hash * 397) ^ WindGust01.GetHashCode();
                return hash;
            }
        }

        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static void ValidateFinite(double value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        internal static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        internal static void ValidateFiniteNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
            }
        }

        internal static void ValidateFinitePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and positive.");
            }
        }

        internal static void Validate01(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and normalized to [0, 1].");
            }
        }

        private static float LerpValue(float from, float to, float progress01) =>
            from + ((to - from) * progress01);

        private static float LerpAngle(float from, float to, float progress01)
        {
            float delta = NormalizeDegrees(to - from);
            if (delta > 180f)
            {
                delta -= 360f;
            }

            return NormalizeDegrees(from + (delta * progress01));
        }

        private static float NormalizeDegrees(float degrees)
        {
            float normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }
    }
}
