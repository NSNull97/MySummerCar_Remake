using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    public interface IVehicleSurfaceMetadataProvider
    {
        VehicleSurfaceType SurfaceType { get; }
    }

    /// <summary>
    /// Explicit semantic surface marker for vehicle contacts. It deliberately
    /// does not infer gameplay behavior from renderer materials or terrain layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleSurfaceMetadataAuthoring : MonoBehaviour,
        IVehicleSurfaceMetadataProvider
    {
        [SerializeField] private VehicleSurfaceType surfaceType = VehicleSurfaceType.Unknown;

        public VehicleSurfaceType SurfaceType => surfaceType;

        public void Configure(VehicleSurfaceType type)
        {
            surfaceType = type;
        }
    }
}
