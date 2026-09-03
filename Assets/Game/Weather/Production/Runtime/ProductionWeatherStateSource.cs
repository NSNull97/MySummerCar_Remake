using System;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Wetness;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Adapts the accepted project-owned weather/time authority to the normalized
    /// hybrid environment contract. It never advances time or chooses weather.
    /// </summary>
    [DefaultExecutionOrder(210)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProductionEnvironmentController))]
    public sealed class ProductionWeatherStateSource : MonoBehaviour,
        IWeatherStateSource
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("MSC.WeatherStateSource");

        [SerializeField] private ProductionEnvironmentController controller;
        [SerializeField, Min(1f)] private float maximumWindSpeedMetersPerSecond = 20f;

        private WeatherRuntimeState current;
        private uint revision;
        private bool subscribed;

        public WeatherRuntimeState Current => current;
        public uint Revision => revision;
        public bool IsReady => current.IsValid && controller != null;

        public event Action<WeatherRuntimeState> StateChanged;

        public void ConfigureForAuthoring(
            ProductionEnvironmentController authoredController)
        {
            controller = authoredController;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<ProductionEnvironmentController>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
            if (controller != null && controller.CurrentOutputs.IsValid)
            {
                Publish(controller.CurrentOutputs);
            }
        }

        private void Update()
        {
            if (controller == null || !controller.AreDomainsInitialized)
            {
                return;
            }

            PublishCurrentAuthoritativeState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || controller == null)
            {
                return;
            }

            controller.EnvironmentOutputsChanged += Publish;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || controller == null)
            {
                return;
            }

            controller.EnvironmentOutputsChanged -= Publish;
            subscribed = false;
        }

        private void Publish(WeatherEnvironmentOutputs outputs)
        {
            using (UpdateMarker.Auto())
            {
                if (!outputs.IsValid || controller == null ||
                    !controller.AreDomainsInitialized)
                {
                    return;
                }

                PublishCurrentAuthoritativeState();
            }
        }

        private void PublishCurrentAuthoritativeState()
        {
            using (UpdateMarker.Auto())
            {
                WeatherDirector director = controller.AuthoritativeWeather;
                GameTimeService time = controller.AuthoritativeGameTime;
                GlobalWetnessController wetness = controller.AuthoritativeWetness;
                if (director == null || time == null || wetness == null)
                {
                    return;
                }

                WeatherState weather = director.CurrentState;
                WeatherTimeline timeline = director.Timeline;
                GameTimeSnapshot clock = time.Snapshot;
                WetnessEnvironmentOutputs wetnessOutputs = wetness.Outputs;
                float windSpeed01 = Mathf.Clamp01(
                    weather.WindSpeedMetersPerSecond /
                    Mathf.Max(1f, maximumWindSpeedMetersPerSecond));
                float rain01 = weather.PrecipitationType ==
                    WeatherPrecipitationType.None
                    ? 0f
                    : weather.PrecipitationIntensity01;
                float storm01 = Mathf.Max(
                    weather.LightningRisk01,
                    weather.LightningIntensity01);
                Vector3 windDirection = Quaternion.Euler(
                    0f,
                    weather.WindDirectionDegrees,
                    0f) * Vector3.forward;

                string currentWeatherId;
                string targetWeatherId;
                float transition01;
                if (director.TryGetActiveOverride(out WeatherOverride weatherOverride))
                {
                    currentWeatherId = weatherOverride.RequestedProfileId.Value;
                    targetWeatherId = currentWeatherId;
                    transition01 = 1f;
                }
                else
                {
                    currentWeatherId = timeline.Transition.Front.From.Value;
                    targetWeatherId = timeline.Transition.Front.To.Value;
                    transition01 = timeline.Transition.Progress01;
                }

                WeatherRuntimeState next = new WeatherRuntimeState(
                    currentWeatherId,
                    targetWeatherId,
                    transition01,
                    (float)clock.NormalizedTimeOfDay01,
                    clock.IsDaylight,
                    weather.CloudCoverage01,
                    weather.PrecipitationIntensity01,
                    rain01,
                    0f,
                    storm01,
                    weather.LightningRisk01,
                    weather.FogIntensity01,
                    weather.VisibilityMeters,
                    weather.Humidity01,
                    humidityIsFallback: !weather.HumidityIsAuthored,
                    Mathf.Max(
                        wetnessOutputs.GroundWetness01,
                        wetnessOutputs.VegetationWetness01),
                    windSpeed01,
                    weather.WindSpeedMetersPerSecond,
                    windDirection,
                    weather.TemperatureCelsius,
                    temperatureIsAvailable: true,
                    Mathf.Clamp01(1f - weather.CloudCoverage01),
                    Mathf.Clamp01(1f - weather.AmbientReadability01));

                if (current.Equals(next))
                {
                    return;
                }

                current = next;
                revision = revision == uint.MaxValue ? 1U : revision + 1U;
                StateChanged?.Invoke(current);
            }
        }
    }
}
