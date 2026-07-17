using UnityEngine;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Optional vendor-neutral camera hand-off for isolated presentation labs.
    /// Gameplay does not use this interface to discover or own cameras.
    /// </summary>
    public interface IEnvironmentPresentationCameraTarget
    {
        bool TrySetPresentationCamera(Camera camera);
    }
}
