using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Idempotency and ownership marker for the applied hybrid environment graph.
    /// Legacy and hybrid builders use this component to avoid simultaneous modes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HybridEnvironmentMarker : MonoBehaviour
    {
        public const int CurrentMigrationVersion = 8;

        [SerializeField] private int migrationVersion = CurrentMigrationVersion;
        [SerializeField] private ProductionWeatherStateSource weatherStateSource;
        [SerializeField] private WeatherZoneRegistry zoneRegistry;
        [SerializeField] private WeatherExposureResolver exposureResolver;
        [SerializeField] private NativeHdrpWeatherBridge nativeHdrpBridge;
        [SerializeField] private WeatherZoneStreamingBinder zoneStreamingBinder;

        public int MigrationVersion => migrationVersion;
        public ProductionWeatherStateSource WeatherStateSource => weatherStateSource;
        public WeatherZoneRegistry ZoneRegistry => zoneRegistry;
        public WeatherExposureResolver ExposureResolver => exposureResolver;
        public NativeHdrpWeatherBridge NativeHdrpBridge => nativeHdrpBridge;
        public WeatherZoneStreamingBinder ZoneStreamingBinder =>
            zoneStreamingBinder;

        public bool IsComplete =>
            migrationVersion == CurrentMigrationVersion &&
            weatherStateSource != null && zoneRegistry != null &&
            exposureResolver != null && nativeHdrpBridge != null &&
            zoneStreamingBinder != null &&
            zoneStreamingBinder.Catalog != null;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ProductionWeatherStateSource authoredSource,
            WeatherZoneRegistry authoredRegistry,
            WeatherExposureResolver authoredResolver,
            NativeHdrpWeatherBridge authoredHdrpBridge,
            WeatherZoneStreamingBinder authoredZoneStreamingBinder)
        {
            migrationVersion = CurrentMigrationVersion;
            weatherStateSource = authoredSource;
            zoneRegistry = authoredRegistry;
            exposureResolver = authoredResolver;
            nativeHdrpBridge = authoredHdrpBridge;
            zoneStreamingBinder = authoredZoneStreamingBinder;
        }
#endif
    }
}
