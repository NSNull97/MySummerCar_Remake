using System;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Lighting.Production
{
    /// <summary>
    /// Event-driven adapter from the project-owned environment read model.
    /// Enviro remains presentation-only and is never queried or written here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnviroLightingBridge : MonoBehaviour,
        ILightingAtmosphereProvider
    {
        private ProductionEnvironmentController environment;
        private LightingAtmosphereState current =
            LightingAtmosphereState.Clear;
        private bool initialized;

        public LightingAtmosphereState Current => current;

        public void Initialize(ProductionEnvironmentController controller)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Lighting environment bridge is already initialized.");
            }

            environment = controller ??
                throw new ArgumentNullException(nameof(controller));
            environment.EnvironmentOutputsChanged += HandleOutputs;
            initialized = true;
            if (environment.CurrentOutputs.IsValid)
            {
                HandleOutputs(environment.CurrentOutputs);
            }
        }

        public float GetSunElevationDegrees()
        {
            if (environment == null ||
                environment.AuthoritativeGameTime == null)
            {
                return -10f;
            }

            GameTimeSnapshot clock =
                environment.AuthoritativeGameTime.Snapshot;
            double time = clock.NormalizedTimeOfDay01;
            double sunrise = clock.SunriseNormalized01;
            double sunset = clock.SunsetNormalized01;
            if (time >= sunrise && time < sunset)
            {
                double dayProgress = (time - sunrise) /
                    Math.Max(0.0001d, sunset - sunrise);
                return Mathf.Sin((float)(dayProgress * Math.PI)) * 45f;
            }

            double nightLength = 1d - sunset + sunrise;
            double nightElapsed = time >= sunset
                ? time - sunset
                : 1d - sunset + time;
            double nightProgress = nightElapsed /
                Math.Max(0.0001d, nightLength);
            return -Mathf.Sin((float)(nightProgress * Math.PI)) * 18f;
        }

        private void HandleOutputs(WeatherEnvironmentOutputs outputs)
        {
            if (!outputs.IsValid)
            {
                return;
            }

            WeatherState weather = outputs.Weather;
            current = new LightingAtmosphereState(
                weather.FogIntensity01,
                weather.PrecipitationIntensity01,
                Mathf.Clamp01(Mathf.Max(
                    weather.CloudCoverage01 * 0.8f,
                    weather.PrecipitationIntensity01)),
                false);
        }

        private void OnDestroy()
        {
            if (environment != null)
            {
                environment.EnvironmentOutputsChanged -= HandleOutputs;
            }

            environment = null;
            initialized = false;
        }
    }
}
