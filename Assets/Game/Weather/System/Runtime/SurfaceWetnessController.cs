using System;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System
{
    [Serializable]
    public struct SurfaceWetnessConfig
    {
        [SerializeField, Min(0f)] private float wetnessAccumulationPerGameSecond;
        [SerializeField, Min(0f)] private float puddleAccumulationPerGameSecond;
        [SerializeField, Min(0f)] private float baseDryingPerGameSecond;
        [SerializeField, Min(0f)] private float puddleDryingPerGameSecond;
        [SerializeField, Range(0f, 1f)] private float puddleWetnessThreshold01;

        public SurfaceWetnessConfig(
            float wetnessAccumulationPerGameSecond,
            float puddleAccumulationPerGameSecond,
            float baseDryingPerGameSecond,
            float puddleDryingPerGameSecond,
            float puddleWetnessThreshold01)
        {
            this.wetnessAccumulationPerGameSecond = RequireNonNegative(
                wetnessAccumulationPerGameSecond);
            this.puddleAccumulationPerGameSecond = RequireNonNegative(
                puddleAccumulationPerGameSecond);
            this.baseDryingPerGameSecond = RequireNonNegative(
                baseDryingPerGameSecond);
            this.puddleDryingPerGameSecond = RequireNonNegative(
                puddleDryingPerGameSecond);
            this.puddleWetnessThreshold01 = Mathf.Clamp01(puddleWetnessThreshold01);
        }

        public float WetnessAccumulationPerGameSecond =>
            wetnessAccumulationPerGameSecond;
        public float PuddleAccumulationPerGameSecond =>
            puddleAccumulationPerGameSecond;
        public float BaseDryingPerGameSecond => baseDryingPerGameSecond;
        public float PuddleDryingPerGameSecond => puddleDryingPerGameSecond;
        public float PuddleWetnessThreshold01 => puddleWetnessThreshold01;

        public bool IsValid =>
            IsNonNegative(wetnessAccumulationPerGameSecond) &&
            IsNonNegative(puddleAccumulationPerGameSecond) &&
            IsNonNegative(baseDryingPerGameSecond) &&
            IsNonNegative(puddleDryingPerGameSecond) &&
            WeatherState.IsFinite(puddleWetnessThreshold01) &&
            puddleWetnessThreshold01 >= 0f && puddleWetnessThreshold01 <= 1f;

        public static SurfaceWetnessConfig FinnishSummerDefault =>
            new SurfaceWetnessConfig(
                1f / 1500f,
                1f / 3600f,
                1f / 14400f,
                1f / 24000f,
                0.55f);

        private static float RequireNonNegative(float value)
        {
            if (!IsNonNegative(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return value;
        }

        private static bool IsNonNegative(float value) =>
            WeatherState.IsFinite(value) && value >= 0f;
    }

    [Serializable]
    public struct SurfaceWetnessSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion;
        public float Wetness01;
        public float PuddleAmount01;
        public double ContinuousRainGameSeconds;
        public double TimeSinceRainGameSeconds;
    }

    public interface ISurfaceWetnessOutput
    {
        void Apply(float wetness01, float puddleAmount01);
    }

    /// <summary>
    /// Persistent water-neutral accumulation domain. It never touches renderers
    /// and can feed shader globals, MPBs, decals or a future water integration.
    /// </summary>
    public sealed class SurfaceWetnessController
    {
        private static readonly ProfilerMarker WetnessMarker =
            new ProfilerMarker("Weather.SurfaceWetness");

        private readonly SurfaceWetnessConfig config;
        private float wetness01;
        private float puddleAmount01;
        private double continuousRainGameSeconds;
        private double timeSinceRainGameSeconds;

        public SurfaceWetnessController(
            in SurfaceWetnessConfig wetnessConfig,
            float initialWetness01 = 0f,
            float initialPuddleAmount01 = 0f)
        {
            if (!wetnessConfig.IsValid)
            {
                throw new ArgumentException("Surface wetness config is invalid.");
            }

            config = wetnessConfig;
            wetness01 = Mathf.Clamp01(initialWetness01);
            puddleAmount01 = Mathf.Clamp01(initialPuddleAmount01);
        }

        public float Wetness01 => wetness01;
        public float PuddleAmount01 => puddleAmount01;
        public double ContinuousRainGameSeconds => continuousRainGameSeconds;
        public double TimeSinceRainGameSeconds => timeSinceRainGameSeconds;

        public void Advance(
            double deltaGameSeconds,
            in WeatherState atmosphere,
            float dryingRateMultiplier)
        {
            if (double.IsNaN(deltaGameSeconds) ||
                double.IsInfinity(deltaGameSeconds) ||
                deltaGameSeconds < 0d ||
                !atmosphere.IsValid ||
                !WeatherState.IsFinite(dryingRateMultiplier) ||
                dryingRateMultiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaGameSeconds));
            }

            using (WetnessMarker.Auto())
            {
                float delta = (float)Math.Min(deltaGameSeconds, float.MaxValue);
                float rain = Mathf.Clamp01(
                    atmosphere.Precipitation01 + atmosphere.Drizzle01 * 0.35f);
                if (rain > 0.001f)
                {
                    continuousRainGameSeconds += deltaGameSeconds;
                    timeSinceRainGameSeconds = 0d;
                    wetness01 = Mathf.Clamp01(
                        wetness01 +
                        config.WetnessAccumulationPerGameSecond * rain * delta);

                    float puddleEligibility = Mathf.InverseLerp(
                        config.PuddleWetnessThreshold01,
                        1f,
                        wetness01);
                    float durationFactor = Mathf.Clamp01(
                        (float)(continuousRainGameSeconds / 900d));
                    puddleAmount01 = Mathf.Clamp01(
                        puddleAmount01 +
                        config.PuddleAccumulationPerGameSecond *
                        rain * puddleEligibility * durationFactor * delta);
                    return;
                }

                continuousRainGameSeconds = 0d;
                timeSinceRainGameSeconds += deltaGameSeconds;
                float temperatureFactor = Mathf.Lerp(
                    0.2f,
                    1.25f,
                    Mathf.InverseLerp(2f, 25f, atmosphere.TemperatureCelsius));
                float windFactor = 1f + Mathf.Clamp01(
                    atmosphere.WindSpeedMetersPerSecond / 12f) * 0.8f;
                float sunFactor = Mathf.Lerp(0.35f, 1.25f, atmosphere.SunVisibility01);
                float cloudFactor = Mathf.Lerp(1f, 0.62f, atmosphere.CloudCoverage01);
                float humidityFactor = Mathf.Lerp(1f, 0.28f, atmosphere.Humidity01);
                float dryingFactor = temperatureFactor * windFactor * sunFactor *
                                     cloudFactor * humidityFactor *
                                     dryingRateMultiplier;
                wetness01 = Mathf.Clamp01(
                    wetness01 -
                    config.BaseDryingPerGameSecond * dryingFactor * delta);
                float puddleFactor = Mathf.Lerp(0.22f, 1f, dryingFactor / 2.5f);
                puddleAmount01 = Mathf.Clamp01(
                    puddleAmount01 -
                    config.PuddleDryingPerGameSecond * puddleFactor * delta);
            }
        }

        public WeatherState ApplyTo(in WeatherState atmosphere) =>
            atmosphere.WithSurface(wetness01, puddleAmount01);

        public void PublishTo(ISurfaceWetnessOutput output) =>
            output?.Apply(wetness01, puddleAmount01);

        public SurfaceWetnessSnapshot CaptureSnapshot() =>
            new SurfaceWetnessSnapshot
            {
                SchemaVersion = SurfaceWetnessSnapshot.CurrentSchemaVersion,
                Wetness01 = wetness01,
                PuddleAmount01 = puddleAmount01,
                ContinuousRainGameSeconds = continuousRainGameSeconds,
                TimeSinceRainGameSeconds = timeSinceRainGameSeconds,
            };

        public void ValidateSnapshot(in SurfaceWetnessSnapshot snapshot)
        {
            if (snapshot.SchemaVersion !=
                    SurfaceWetnessSnapshot.CurrentSchemaVersion ||
                !WeatherState.IsFinite(snapshot.Wetness01) ||
                snapshot.Wetness01 < 0f || snapshot.Wetness01 > 1f ||
                !WeatherState.IsFinite(snapshot.PuddleAmount01) ||
                snapshot.PuddleAmount01 < 0f || snapshot.PuddleAmount01 > 1f ||
                double.IsNaN(snapshot.ContinuousRainGameSeconds) ||
                double.IsInfinity(snapshot.ContinuousRainGameSeconds) ||
                snapshot.ContinuousRainGameSeconds < 0d ||
                double.IsNaN(snapshot.TimeSinceRainGameSeconds) ||
                double.IsInfinity(snapshot.TimeSinceRainGameSeconds) ||
                snapshot.TimeSinceRainGameSeconds < 0d)
            {
                throw new ArgumentException("Surface wetness snapshot is invalid.");
            }
        }

        public void Restore(in SurfaceWetnessSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            wetness01 = snapshot.Wetness01;
            puddleAmount01 = snapshot.PuddleAmount01;
            continuousRainGameSeconds = snapshot.ContinuousRainGameSeconds;
            timeSinceRainGameSeconds = snapshot.TimeSinceRainGameSeconds;
        }
    }
}
