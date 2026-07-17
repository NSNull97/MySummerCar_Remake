using System.Collections.Generic;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Vendor-neutral boundary implemented by one narrow presentation backend.
    /// Gameplay and weather-domain code must never query a vendor manager directly.
    /// </summary>
    public interface IEnvironmentPresentationAdapter
    {
        bool IsAttached { get; }

        EnvironmentPresentationCapabilities Capabilities { get; }

        EnvironmentPresentationStatus Status { get; }

        IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics { get; }

        EnvironmentPresentationStatus Attach();

        EnvironmentPresentationStatus Present(in EnvironmentPresentationFrame frame);

        void Detach();
    }
}
