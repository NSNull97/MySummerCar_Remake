using System;
using MSC.Weather.Wetness;

namespace MSC.Weather.Domain
{
    public enum WeatherExposureContext
    {
        Exterior = 0,
        Sheltered = 1,
        Interior = 2,
    }

    public enum WeatherPresentationHealth
    {
        Unknown = 0,
        Ready = 1,
        Degraded = 2,
        Unavailable = 3,
    }

    public readonly struct WeatherClockOutput
    {
        public WeatherClockOutput(int year, int month, int day, long dayIndex, float normalizedDayTime01)
        {
            if (year < 1 || month < 1 || month > 12 || day < 1 || day > 31 || dayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(day));
            }

            if (!WeatherState.IsFinite(normalizedDayTime01) ||
                normalizedDayTime01 < 0f ||
                normalizedDayTime01 >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedDayTime01));
            }
            Year = year;
            Month = month;
            Day = day;
            DayIndex = dayIndex;
            NormalizedDayTime01 = normalizedDayTime01;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        public long DayIndex { get; }

        public float NormalizedDayTime01 { get; }
    }

    public readonly struct WeatherAudioOutput
    {
        public WeatherAudioOutput(
            WeatherPrecipitationType precipitationType,
            float precipitationIntensity01,
            float windIntensity01,
            float thunderRisk01)
        {
            WeatherState.Validate01(precipitationIntensity01, nameof(precipitationIntensity01));
            WeatherState.Validate01(windIntensity01, nameof(windIntensity01));
            WeatherState.Validate01(thunderRisk01, nameof(thunderRisk01));
            PrecipitationType = precipitationType;
            PrecipitationIntensity01 = precipitationIntensity01;
            WindIntensity01 = windIntensity01;
            ThunderRisk01 = thunderRisk01;
        }

        public WeatherPrecipitationType PrecipitationType { get; }

        public float PrecipitationIntensity01 { get; }

        public float WindIntensity01 { get; }

        public float ThunderRisk01 { get; }
    }

    public readonly struct WeatherUiSummary
    {
        public WeatherUiSummary(
            WeatherStateId stateId,
            float normalizedDayTime01,
            float temperatureCelsius,
            float precipitationIntensity01)
        {
            if (stateId.IsEmpty)
            {
                throw new ArgumentException("UI summary requires a stable logical weather state.", nameof(stateId));
            }

            WeatherState.Validate01(normalizedDayTime01, nameof(normalizedDayTime01));
            WeatherState.ValidateFinite(temperatureCelsius, nameof(temperatureCelsius));
            WeatherState.Validate01(precipitationIntensity01, nameof(precipitationIntensity01));
            StateId = stateId;
            NormalizedDayTime01 = normalizedDayTime01;
            TemperatureCelsius = temperatureCelsius;
            PrecipitationIntensity01 = precipitationIntensity01;
        }

        public WeatherStateId StateId { get; }

        public float NormalizedDayTime01 { get; }

        public float TemperatureCelsius { get; }

        public float PrecipitationIntensity01 { get; }
    }

    public readonly struct WeatherPresentationStatusOutput
    {
        public WeatherPresentationStatusOutput(
            string qualityTierId,
            WeatherPresentationHealth health,
            uint presentedRevision)
        {
            if (string.IsNullOrWhiteSpace(qualityTierId))
            {
                throw new ArgumentException("Presentation quality tier ID is required.", nameof(qualityTierId));
            }

            if (!Enum.IsDefined(typeof(WeatherPresentationHealth), health))
            {
                throw new ArgumentOutOfRangeException(nameof(health));
            }

            QualityTierId = qualityTierId;
            Health = health;
            PresentedRevision = presentedRevision;
        }

        public string QualityTierId { get; }

        public WeatherPresentationHealth Health { get; }

        public uint PresentedRevision { get; }
    }

    public readonly struct WeatherEnvironmentOutputContext
    {
        public WeatherEnvironmentOutputContext(
            WeatherClockOutput clock,
            WetnessEnvironmentOutputs wetness,
            WeatherExposureContext exposureContext,
            WeatherPresentationStatusOutput presentationStatus)
        {
            Clock = clock;
            Wetness = wetness;
            ExposureContext = exposureContext;
            PresentationStatus = presentationStatus;
        }

        public WeatherClockOutput Clock { get; }

        public WetnessEnvironmentOutputs Wetness { get; }

        public WeatherExposureContext ExposureContext { get; }

        public WeatherPresentationStatusOutput PresentationStatus { get; }

    }

    /// <summary>
    /// Single vendor-neutral read model for gameplay, audio, UI and presentation consumers.
    /// Consumers must not query Enviro directly.
    /// </summary>
    public readonly struct WeatherEnvironmentOutputs
    {
        private WeatherEnvironmentOutputs(
            WeatherState weather,
            uint logicalRevision,
            WeatherEnvironmentOutputContext context,
            WeatherAudioOutput audio,
            WeatherUiSummary ui)
        {
            Weather = weather;
            LogicalRevision = logicalRevision;
            Clock = context.Clock;
            Wetness = context.Wetness;
            ExposureContext = context.ExposureContext;
            Audio = audio;
            Ui = ui;
            PresentationStatus = context.PresentationStatus;
        }

        public WeatherState Weather { get; }

        public uint LogicalRevision { get; }

        public WeatherClockOutput Clock { get; }

        public WetnessEnvironmentOutputs Wetness { get; }

        public WeatherExposureContext ExposureContext { get; }

        public WeatherAudioOutput Audio { get; }

        public WeatherUiSummary Ui { get; }

        public WeatherPresentationStatusOutput PresentationStatus { get; }

        public bool IsValid =>
            !Weather.Id.IsEmpty &&
            !string.IsNullOrWhiteSpace(Weather.PresentationBindingId) &&
            LogicalRevision != 0U &&
            Clock.Year > 0 &&
            Clock.Month >= 1 && Clock.Month <= 12 &&
            Clock.Day >= 1 && Clock.Day <= 31 &&
            Clock.DayIndex >= 0 &&
            !string.IsNullOrWhiteSpace(Wetness.ExposureProfileId) &&
            Enum.IsDefined(typeof(WeatherExposureContext), ExposureContext) &&
            !Ui.StateId.IsEmpty &&
            !string.IsNullOrWhiteSpace(PresentationStatus.QualityTierId) &&
            Enum.IsDefined(typeof(WeatherPresentationHealth), PresentationStatus.Health);

        public void Validate()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Weather environment outputs are incomplete or invalid.");
            }
        }

        public static WeatherEnvironmentOutputs Compose(
            in WeatherState state,
            uint logicalRevision,
            in WeatherEnvironmentOutputContext context)
        {
            float windIntensity01 = Math.Min(1f, state.WindSpeedMetersPerSecond / 20f);
            var audio = new WeatherAudioOutput(
                state.PrecipitationType,
                state.PrecipitationIntensity01,
                windIntensity01,
                state.LightningRisk01);
            var ui = new WeatherUiSummary(
                state.Id,
                context.Clock.NormalizedDayTime01,
                state.TemperatureCelsius,
                state.PrecipitationIntensity01);
            var result = new WeatherEnvironmentOutputs(state, logicalRevision, context, audio, ui);
            result.Validate();
            return result;
        }
    }
}
