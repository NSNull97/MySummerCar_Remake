using System;
using Unity.Profiling;

namespace MSC.Weather.System
{
    [Serializable]
    public sealed class WeatherDirectorSnapshot
    {
        public const int CurrentSchemaVersion = 1;
        public const string StableConfigId = "weather.system.director.v1";

        public int SchemaVersion = CurrentSchemaVersion;
        public string ConfigId = StableConfigId;
        public ulong Revision;
        public WeatherSchedulerSnapshot Scheduler;
        public SurfaceWetnessSnapshot SurfaceWetness;
        public WeatherState GlobalState;
        public bool ThunderActive;
        public double ThunderStateElapsedGameSeconds;
    }

    /// <summary>
    /// High-level continuous climate domain. Existing production weather can run
    /// beside it during migration; only the selected authority may drive a backend.
    /// </summary>
    public sealed class WeatherDirector
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Weather.Update");

        private readonly WeatherClimateCatalog catalog;
        private readonly WeatherScheduler scheduler;
        private readonly SurfaceWetnessController wetness;
        private WeatherState currentState;
        private ulong revision = 1UL;
        private bool thunderActive;
        private double thunderStateElapsedGameSeconds;

        public WeatherDirector(
            WeatherClimateCatalog climateCatalog,
            ulong seed,
            in WeatherClimateContext initialContext,
            SurfaceWetnessConfig? wetnessConfig = null)
        {
            catalog = climateCatalog ?? throw new ArgumentNullException(nameof(climateCatalog));
            scheduler = new WeatherScheduler(
                catalog,
                seed,
                0x46534DUL,
                initialContext);
            WeatherState initial = scheduler.CurrentState;
            wetness = new SurfaceWetnessController(
                wetnessConfig ?? SurfaceWetnessConfig.FinnishSummerDefault,
                initial.SurfaceWetness01,
                initial.PuddleAmount01);
            currentState = wetness.ApplyTo(initial);
            thunderActive = currentState.ThunderIntensity01 > 0.05f;
        }

        public event Action<WeatherState> StateChanged;

        public WeatherState CurrentState => currentState;
        public string PreviousPresetId => scheduler.PreviousPresetId;
        public string CurrentPresetId => scheduler.CurrentPresetId;
        public string TargetPresetId => scheduler.TargetPresetId;
        public float TransitionProgress01 => scheduler.TransitionProgress01;
        public ulong Revision => revision;
        public bool ThunderActive => thunderActive;
        public double ThunderStateElapsedGameSeconds =>
            thunderStateElapsedGameSeconds;
        public SurfaceWetnessController SurfaceWetness => wetness;

        public void Advance(
            double deltaGameSeconds,
            in WeatherClimateContext context)
        {
            if (double.IsNaN(deltaGameSeconds) ||
                double.IsInfinity(deltaGameSeconds) ||
                deltaGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaGameSeconds));
            }

            if (deltaGameSeconds == 0d)
            {
                return;
            }

            using (UpdateMarker.Auto())
            {
                scheduler.Advance(deltaGameSeconds, context);
                WeatherState atmosphere = scheduler.CurrentState;
                wetness.Advance(
                    deltaGameSeconds,
                    atmosphere,
                    scheduler.CurrentPreset.DryingRateMultiplier);
                WeatherState next = wetness.ApplyTo(atmosphere);
                bool nextThunder = next.ThunderIntensity01 > 0.05f;
                if (nextThunder == thunderActive)
                {
                    thunderStateElapsedGameSeconds += deltaGameSeconds;
                }
                else
                {
                    thunderActive = nextThunder;
                    thunderStateElapsedGameSeconds = 0d;
                }

                currentState = next;
                revision = revision == ulong.MaxValue ? 1UL : revision + 1UL;
                StateChanged?.Invoke(currentState);
            }
        }

        public WeatherDirectorSnapshot CaptureSnapshot() =>
            new WeatherDirectorSnapshot
            {
                Revision = revision,
                Scheduler = scheduler.CaptureSnapshot(),
                SurfaceWetness = wetness.CaptureSnapshot(),
                GlobalState = currentState,
                ThunderActive = thunderActive,
                ThunderStateElapsedGameSeconds = thunderStateElapsedGameSeconds,
            };

        public void ValidateSnapshot(WeatherDirectorSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.SchemaVersion != WeatherDirectorSnapshot.CurrentSchemaVersion ||
                !string.Equals(
                    snapshot.ConfigId,
                    WeatherDirectorSnapshot.StableConfigId,
                    StringComparison.Ordinal) ||
                snapshot.Revision == 0UL ||
                !snapshot.GlobalState.IsValid ||
                double.IsNaN(snapshot.ThunderStateElapsedGameSeconds) ||
                double.IsInfinity(snapshot.ThunderStateElapsedGameSeconds) ||
                snapshot.ThunderStateElapsedGameSeconds < 0d)
            {
                throw new ArgumentException("Weather director snapshot is invalid.");
            }

            scheduler.ValidateSnapshot(snapshot.Scheduler);
            wetness.ValidateSnapshot(snapshot.SurfaceWetness);
        }

        public void Restore(WeatherDirectorSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            WeatherDirectorSnapshot checkpoint = CaptureSnapshot();
            try
            {
                scheduler.Restore(snapshot.Scheduler);
                wetness.Restore(snapshot.SurfaceWetness);
                WeatherState derived = wetness.ApplyTo(scheduler.CurrentState);
                if (!Approximately(derived, snapshot.GlobalState))
                {
                    throw new ArgumentException(
                        "Saved global weather does not match its scheduler and wetness state.");
                }

                currentState = snapshot.GlobalState;
                revision = snapshot.Revision;
                thunderActive = snapshot.ThunderActive;
                thunderStateElapsedGameSeconds =
                    snapshot.ThunderStateElapsedGameSeconds;
                StateChanged?.Invoke(currentState);
            }
            catch
            {
                scheduler.Restore(checkpoint.Scheduler);
                wetness.Restore(checkpoint.SurfaceWetness);
                currentState = checkpoint.GlobalState;
                revision = checkpoint.Revision;
                thunderActive = checkpoint.ThunderActive;
                thunderStateElapsedGameSeconds =
                    checkpoint.ThunderStateElapsedGameSeconds;
                throw;
            }
        }

        private static bool Approximately(
            in WeatherState left,
            in WeatherState right)
        {
            const float tolerance = 0.0001f;
            return Math.Abs(left.CloudCoverage01 - right.CloudCoverage01) < tolerance &&
                   Math.Abs(left.CloudDensity01 - right.CloudDensity01) < tolerance &&
                   Math.Abs(left.CloudErosion01 - right.CloudErosion01) < tolerance &&
                   Math.Abs(left.CloudShadowStrength01 -
                            right.CloudShadowStrength01) < tolerance &&
                   Math.Abs(left.Precipitation01 - right.Precipitation01) < tolerance &&
                   Math.Abs(left.Drizzle01 - right.Drizzle01) < tolerance &&
                   Math.Abs(left.ThunderIntensity01 -
                            right.ThunderIntensity01) < tolerance &&
                   Math.Abs(left.FogDensity01 - right.FogDensity01) < tolerance &&
                   Math.Abs(left.FogDistanceMeters -
                            right.FogDistanceMeters) < tolerance &&
                   Math.Abs(left.AtmosphericHaze01 -
                            right.AtmosphericHaze01) < tolerance &&
                   Math.Abs(left.WindSpeedMetersPerSecond -
                            right.WindSpeedMetersPerSecond) < tolerance &&
                   Math.Abs(left.WindDirectionXZ.x -
                            right.WindDirectionXZ.x) < tolerance &&
                   Math.Abs(left.WindDirectionXZ.y -
                            right.WindDirectionXZ.y) < tolerance &&
                   Math.Abs(left.WindGustiness01 -
                            right.WindGustiness01) < tolerance &&
                   Math.Abs(left.SurfaceWetness01 - right.SurfaceWetness01) < tolerance &&
                   Math.Abs(left.PuddleAmount01 - right.PuddleAmount01) < tolerance &&
                   Math.Abs(left.SunVisibility01 -
                            right.SunVisibility01) < tolerance &&
                   Math.Abs(left.SkyBrightnessMultiplier -
                            right.SkyBrightnessMultiplier) < tolerance &&
                   Math.Abs(left.AmbientLightMultiplier -
                            right.AmbientLightMultiplier) < tolerance &&
                   Math.Abs(left.TemperatureCelsius - right.TemperatureCelsius) < tolerance &&
                   Math.Abs(left.Humidity01 - right.Humidity01) < tolerance;
        }
    }
}
