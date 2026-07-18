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

    /// <summary>
    /// Optional integration-boundary guard invoked after additive scene topology
    /// changes. The contract stays vendor-neutral; the adapter is responsible for
    /// detecting its own concrete manager/owner types and failing closed.
    /// </summary>
    public interface IEnvironmentPresentationSceneOwnershipGuard
    {
        EnvironmentPresentationStatus RevalidateSceneOwnership();
    }
}
