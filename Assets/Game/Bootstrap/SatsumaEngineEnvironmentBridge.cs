using System;
using MSC.Vehicle;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class SatsumaEngineEnvironmentBridge : MonoBehaviour
    {
        private ProductionEnvironmentController environment;
        private SatsumaEngineOperatingSource source;
        public void Bind(ProductionEnvironmentController owner, SatsumaEngineOperatingSource target)
        {
            if (owner == null || target == null) throw new ArgumentNullException();
            Unbind(); environment = owner; source = target;
            environment.EnvironmentOutputsChanged += Apply;
            Apply(environment.CurrentOutputs);
        }
        private void Apply(WeatherEnvironmentOutputs outputs)
        { if (source != null && outputs.IsValid) source.SetAmbientTemperature(outputs.Weather.TemperatureCelsius); }
        private void Unbind()
        {
            if (environment != null) environment.EnvironmentOutputsChanged -= Apply;
            if (source != null) source.ClearEnvironmentBinding();
            environment = null; source = null;
        }
        private void OnDestroy() => Unbind();
    }
}
