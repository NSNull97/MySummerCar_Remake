using MSC.Audio;
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
        IVehicleSurfaceMetadataProvider,
        IAudioSurfaceMetadataProvider
    {
        [SerializeField] private VehicleSurfaceType surfaceType = VehicleSurfaceType.Unknown;

        public VehicleSurfaceType SurfaceType => surfaceType;

        public AudioSurfaceMetadata AudioSurface => new AudioSurfaceMetadata(
            MapAudioSurface(surfaceType),
            surfaceType == VehicleSurfaceType.MudWet ? 1f : 0f,
            ResolveRoughness(surfaceType));

        public void Configure(VehicleSurfaceType type)
        {
            surfaceType = type;
        }

        private static AudioSurfaceKind MapAudioSurface(VehicleSurfaceType type)
        {
            switch (type)
            {
                case VehicleSurfaceType.Paved:
                    return AudioSurfaceKind.Paved;
                case VehicleSurfaceType.Gravel:
                    return AudioSurfaceKind.Gravel;
                case VehicleSurfaceType.Dirt:
                    return AudioSurfaceKind.Dirt;
                case VehicleSurfaceType.Grass:
                    return AudioSurfaceKind.Grass;
                case VehicleSurfaceType.MudWet:
                    return AudioSurfaceKind.Wet;
                case VehicleSurfaceType.Unknown:
                default:
                    return AudioSurfaceKind.Unknown;
            }
        }

        private static float ResolveRoughness(VehicleSurfaceType type)
        {
            switch (type)
            {
                case VehicleSurfaceType.Paved:
                    return 0.3f;
                case VehicleSurfaceType.Gravel:
                    return 0.9f;
                case VehicleSurfaceType.Dirt:
                    return 0.75f;
                case VehicleSurfaceType.Grass:
                    return 0.65f;
                case VehicleSurfaceType.MudWet:
                    return 0.6f;
                case VehicleSurfaceType.Unknown:
                default:
                    return 0.5f;
            }
        }
    }
}
