using System.Collections.Generic;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.System.EnviroLegacy
{
    /// <summary>
    /// Compatibility wrapper. Every call is delegated to the already accepted
    /// Enviro adapter, so selecting EnviroLegacy does not change scene behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnviroWeatherBackend : MonoBehaviour, IWeatherBackend
    {
        [SerializeField] private Enviro3EnvironmentAdapter adapter;

        private readonly List<EnvironmentPresentationDiagnostic> diagnostics =
            new List<EnvironmentPresentationDiagnostic>(1);
        private EnvironmentPresentationStatus missingStatus =
            EnvironmentPresentationStatus.Detached;

        public WeatherBackendType BackendType => WeatherBackendType.EnviroLegacy;
        public WeatherPresentationOwnership Ownership
        {
            get
            {
                WeatherPresentationOwnership ownership =
                    WeatherPresentationOwnership.Time |
                    WeatherPresentationOwnership.Sky |
                    WeatherPresentationOwnership.Clouds |
                    WeatherPresentationOwnership.Precipitation |
                    WeatherPresentationOwnership.Color |
                    WeatherPresentationOwnership.Sun |
                    WeatherPresentationOwnership.Wind |
                    WeatherPresentationOwnership.LightningPresentation;
                if (adapter == null || !adapter.HybridNativeHdrpOwnership)
                {
                    ownership |= WeatherPresentationOwnership.Fog |
                                 WeatherPresentationOwnership.Exposure |
                                 WeatherPresentationOwnership.IndirectLighting;
                }

                return ownership;
            }
        }
        public bool IsAttached => adapter != null && adapter.IsAttached;
        public EnvironmentPresentationCapabilities Capabilities =>
            adapter?.Capabilities ?? EnvironmentPresentationCapabilities.None;
        public EnvironmentPresentationStatus Status =>
            adapter?.Status ?? missingStatus;
        public IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics =>
            adapter?.Diagnostics ?? diagnostics;

        public EnvironmentPresentationStatus Attach() =>
            adapter != null
                ? adapter.Attach()
                : MissingAdapterStatus();

        public EnvironmentPresentationStatus Present(
            in EnvironmentPresentationFrame frame) =>
            adapter != null
                ? adapter.Present(frame)
                : MissingAdapterStatus();

        public void Detach()
        {
            adapter?.Detach();
        }

        public bool TrySetPresentationCamera(Camera camera) =>
            adapter != null && adapter.TrySetPresentationCamera(camera);

        public EnvironmentPresentationStatus RevalidateSceneOwnership() =>
            adapter != null
                ? adapter.RevalidateSceneOwnership()
                : MissingAdapterStatus();

        public void ConfigureForAuthoring(
            Enviro3EnvironmentAdapter authoredAdapter)
        {
            adapter = authoredAdapter;
        }

        private EnvironmentPresentationStatus MissingAdapterStatus()
        {
            diagnostics.Clear();
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                "WEATHER-ENVIRO-001",
                "EnviroWeatherBackend has no Enviro3EnvironmentAdapter reference."));
            missingStatus = new EnvironmentPresentationStatus(
                EnvironmentPresentationState.Faulted,
                EnvironmentPresentationCapabilities.None,
                0UL,
                0,
                1);
            return missingStatus;
        }
    }
}
