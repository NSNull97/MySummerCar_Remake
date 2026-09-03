using MSC.Weather.Presentation;

namespace MSC.Weather.System
{
    public enum WeatherBackendType
    {
        EnviroLegacy = 0,
        NativeHDRP = 1,
    }

    [global::System.Flags]
    public enum WeatherPresentationOwnership
    {
        None = 0,
        Time = 1 << 0,
        Sky = 1 << 1,
        Clouds = 1 << 2,
        Precipitation = 1 << 3,
        Fog = 1 << 4,
        Exposure = 1 << 5,
        Color = 1 << 6,
        IndirectLighting = 1 << 7,
        Sun = 1 << 8,
        Wind = 1 << 9,
        LightningPresentation = 1 << 10,
    }

    /// <summary>
    /// The only boundary visible to the backend router. Implementations may
    /// reference Enviro or HDRP in their own isolated assemblies.
    /// </summary>
    public interface IWeatherBackend :
        IEnvironmentPresentationAdapter,
        IEnvironmentPresentationCameraTarget,
        IEnvironmentPresentationSceneOwnershipGuard
    {
        WeatherBackendType BackendType { get; }
        WeatherPresentationOwnership Ownership { get; }
    }
}
