using System;
using System.Collections.Generic;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Audio.Composition
{
    public enum WorldAmbientPhase
    {
        Night = 0,
        Morning = 1,
        Day = 2,
        Evening = 3,
        All = 4,
    }

    /// <summary>
    /// Project-owned runtime definition built from the locked donor ambience
    /// hierarchy. SourcePosition is retained for audit; WorldPosition applies
    /// the approved M04A1 garage-anchored translation.
    /// </summary>
    public readonly struct WorldAmbientLayerDefinition
    {
        public WorldAmbientLayerDefinition(
            string layerId,
            AudioEventId eventId,
            WorldAmbientPhase phase,
            Vector3 sourcePosition,
            float baseVolume01,
            float minimumDistanceMeters,
            float maximumDistanceMeters,
            float rainSuppression01,
            float windSuppression01)
        {
            LayerId = layerId;
            EventId = eventId;
            Phase = phase;
            SourcePosition = sourcePosition;
            WorldPosition = sourcePosition +
                WorldAmbientAudioPresenter.SourceToProjectTranslation;
            BaseVolume01 = Mathf.Clamp01(baseVolume01);
            MinimumDistanceMeters = Mathf.Max(0f, minimumDistanceMeters);
            MaximumDistanceMeters = Mathf.Max(
                MinimumDistanceMeters + 0.01f,
                maximumDistanceMeters);
            RainSuppression01 = Mathf.Clamp01(rainSuppression01);
            WindSuppression01 = Mathf.Clamp01(windSuppression01);
        }

        public string LayerId { get; }
        public AudioEventId EventId { get; }
        public WorldAmbientPhase Phase { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 WorldPosition { get; }
        public float BaseVolume01 { get; }
        public float MinimumDistanceMeters { get; }
        public float MaximumDistanceMeters { get; }
        public float RainSuppression01 { get; }
        public float WindSuppression01 { get; }
    }

    /// <summary>
    /// Time/weather-aware recreation of MAP/SoundAmbience plus the three
    /// donor lake-shore emitters. Donor PlayMaker and hierarchy names are
    /// evidence only; runtime uses project stable IDs and explicit positions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldAmbientAudioPresenter : MonoBehaviour
    {
        private sealed class LayerRuntime
        {
            public WorldAmbientLayerDefinition Definition;
            public AudioEmitterAuthoring Emitter;
            public IAudioEventHandle Handle = AudioEventHandles.Invalid;
            public float PostedVolume = -1f;
        }

        private const float EnvironmentRefreshSeconds = 3f;
        private const float MinimumAudibleGain = 0.025f;
        private const float LayerRestartGainDelta = 0.12f;

        // Frozen M04A1 donor-world -> project garage-anchored coordinates.
        public static readonly Vector3 SourceToProjectTranslation =
            new Vector3(169.98f, 1.611f, -1040.625f);

        // Explicit user acceptance correction: one rare source in the forest
        // near the player home, instead of a listener-relative one-shot.
        private static readonly Vector3 ChainsawWorldPosition =
            new Vector3(105f, 1.8f, -1075f);

        private static readonly WorldAmbientLayerDefinition[] Definitions =
        {
            new WorldAmbientLayerDefinition(
                "morning.birds",
                AudioProjectIds.Events.WorldBirdsMorning,
                WorldAmbientPhase.Morning,
                new Vector3(-143f, 13f, 940f),
                0.92f, 1f, 1100f, 0.92f, 0.38f),
            new WorldAmbientLayerDefinition(
                "morning.swamp",
                AudioProjectIds.Events.WorldBirdsSwamp,
                WorldAmbientPhase.Morning,
                new Vector3(1037f, 13f, 11f),
                0.72f, 1f, 1200f, 0.9f, 0.32f),
            new WorldAmbientLayerDefinition(
                "day.birds",
                AudioProjectIds.Events.WorldBirdsDay,
                WorldAmbientPhase.Day,
                new Vector3(-128f, 13f, 893f),
                0.9f, 1f, 1100f, 0.92f, 0.38f),
            new WorldAmbientLayerDefinition(
                "day.meadow",
                AudioProjectIds.Events.WorldMeadow,
                WorldAmbientPhase.Day,
                new Vector3(-658f, 13f, -620f),
                0.88f, 1f, 1100f, 0.86f, 0.3f),
            new WorldAmbientLayerDefinition(
                "evening.birds",
                AudioProjectIds.Events.WorldBirdsEvening,
                WorldAmbientPhase.Evening,
                new Vector3(-356f, 13f, 908f),
                0.9f, 1f, 1100f, 0.9f, 0.34f),
            new WorldAmbientLayerDefinition(
                "evening.swamp",
                AudioProjectIds.Events.WorldBirdsSwamp,
                WorldAmbientPhase.Evening,
                new Vector3(978f, 13f, 56f),
                0.7f, 1f, 1200f, 0.88f, 0.3f),
            new WorldAmbientLayerDefinition(
                "night.birds",
                AudioProjectIds.Events.WorldBirdsNight,
                WorldAmbientPhase.Night,
                new Vector3(-28f, 13f, 1015f),
                0.9f, 1f, 800f, 0.86f, 0.28f),
            new WorldAmbientLayerDefinition(
                "dog",
                AudioProjectIds.Events.WorldDog,
                WorldAmbientPhase.All,
                new Vector3(-487f, 0f, -412f),
                0.44f, 1f, 900f, 0.62f, 0.15f),
            new WorldAmbientLayerDefinition(
                "lake.cottage",
                AudioProjectIds.Events.WorldLakeAmbience,
                WorldAmbientPhase.All,
                // Nested COTTAGE/LOD/LakeSound transform resolved read-only.
                new Vector3(-794.2458f, -3.695f, 563.254f),
                0.32f, 1f, 150f, 0f, 0f),
            new WorldAmbientLayerDefinition(
                "lake.east",
                AudioProjectIds.Events.WorldLakeAmbience,
                WorldAmbientPhase.All,
                new Vector3(18f, -4f, 212f),
                0.32f, 1f, 150f, 0f, 0f),
            new WorldAmbientLayerDefinition(
                "lake.village",
                AudioProjectIds.Events.WorldLakeAmbience,
                WorldAmbientPhase.All,
                new Vector3(-1335.7971f, -4.01f, 979.8961f),
                0.32f, 1f, 150f, 0f, 0f),
        };

        private readonly List<LayerRuntime> layers = new List<LayerRuntime>();
        private IAudioBackend backend;
        private AudioEmitterAuthoring ambienceEmitter;
        private AudioEmitterAuthoring chainsawEmitter;
        private IGameTimeService gameTime;
        private ProductionEnvironmentController environment;
        private IAudioEventHandle chainsawHandle = AudioEventHandles.Invalid;
        private float nextEnvironmentRefreshAt;
        private float nextChainsawAt;
        private float pauseStartedAt;
        private uint scheduleState;
        private bool playbackPaused;

        public bool IsConfigured => backend != null &&
            ambienceEmitter != null && gameTime != null && environment != null;
        public static Vector3 ChainsawSourceWorldPosition =>
            ChainsawWorldPosition;
        public static IReadOnlyList<WorldAmbientLayerDefinition>
            LayerDefinitions => Definitions;

        public bool Configure(
            IAudioBackend configuredBackend,
            AudioEmitterAuthoring configuredAmbienceEmitter,
            IGameTimeService configuredGameTime,
            ProductionEnvironmentController configuredEnvironment,
            uint sessionSeed = 0x4D534341u)
        {
            StopPlayback();
            DestroyLayerEmitters();
            backend = configuredBackend;
            ambienceEmitter = configuredAmbienceEmitter;
            gameTime = configuredGameTime;
            environment = configuredEnvironment;
            scheduleState = sessionSeed == 0u ? 0x4D534341u : sessionSeed;

            if (!IsConfigured)
            {
                return false;
            }

            EnsureEmitters();
            nextEnvironmentRefreshAt = Time.unscaledTime;
            nextChainsawAt = Time.unscaledTime +
                CalculateNextInterval(ref scheduleState, initial: true);
            playbackPaused = ShouldPausePlayback(
                gameTime.Snapshot.IsPaused,
                Time.timeScale);
            pauseStartedAt = playbackPaused ? Time.unscaledTime : 0f;
            if (!playbackPaused)
            {
                RefreshEnvironment(forceRestart: true);
            }

            return true;
        }

        public static float CalculateNextInterval(ref uint state, bool initial)
        {
            state = (state * 1664525u) + 1013904223u;
            float normalized = (state & 0x00FFFFFFu) / 16777215f;
            return initial
                ? Mathf.Lerp(300f, 900f, normalized)
                : Mathf.Lerp(900f, 2100f, normalized);
        }

        public static WorldAmbientPhase ResolvePhase(
            double normalizedTimeOfDay01)
        {
            float hour = Mathf.Repeat(
                (float)normalizedTimeOfDay01,
                1f) * 24f;
            if (hour < 6f)
            {
                return WorldAmbientPhase.Night;
            }

            if (hour < 12f)
            {
                return WorldAmbientPhase.Morning;
            }

            return hour < 18f
                ? WorldAmbientPhase.Day
                : WorldAmbientPhase.Evening;
        }

        public static float CalculateDistanceGain(
            float distanceMeters,
            float minimumDistanceMeters,
            float maximumDistanceMeters)
        {
            if (!float.IsFinite(distanceMeters) || distanceMeters < 0f)
            {
                return 0f;
            }

            float minimum = Mathf.Max(0f, minimumDistanceMeters);
            float maximum = Mathf.Max(minimum + 0.01f, maximumDistanceMeters);
            float linear = 1f - Mathf.InverseLerp(minimum, maximum, distanceMeters);
            // The reviewed donor sources use broad custom curves. Square-root
            // falloff preserves their distant presence without making them 2D.
            return Mathf.Sqrt(Mathf.Clamp01(linear));
        }

        public static float CalculateLayerGain(
            in WorldAmbientLayerDefinition definition,
            double normalizedTimeOfDay01,
            bool paused,
            float precipitation01,
            float wind01,
            Vector3 listenerPosition)
        {
            if (paused ||
                (definition.Phase != WorldAmbientPhase.All &&
                 definition.Phase != ResolvePhase(normalizedTimeOfDay01)))
            {
                return 0f;
            }

            float rainGain = 1f - Mathf.Clamp01(precipitation01) *
                definition.RainSuppression01;
            float windGain = 1f - Mathf.Clamp01(wind01) *
                definition.WindSuppression01;
            float distanceGain = CalculateDistanceGain(
                Vector3.Distance(listenerPosition, definition.WorldPosition),
                definition.MinimumDistanceMeters,
                definition.MaximumDistanceMeters);
            return Mathf.Clamp01(
                definition.BaseVolume01 * rainGain * windGain * distanceGain);
        }

        // Retained compatibility policy for callers/tests from the bounded
        // first ambience pass. Individual parity layers use CalculateLayerGain.
        public static float CalculateAmbientGain(
            double normalizedTimeOfDay01,
            float precipitation01,
            float wind01)
        {
            float hour = Mathf.Repeat(
                (float)normalizedTimeOfDay01,
                1f) * 24f;
            float morning = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(5.25f, 7f, hour));
            float evening = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(19.5f, 22f, hour));
            float daylight = Mathf.Clamp01(morning * evening);
            float rainSuppression = 1f -
                Mathf.Clamp01(precipitation01) * 0.94f;
            float windSuppression = 1f -
                Mathf.Clamp01(wind01) * 0.42f;
            return Mathf.Clamp01(
                daylight * rainSuppression * windSuppression);
        }

        public static bool IsChainsawEligible(
            double normalizedTimeOfDay01,
            bool paused,
            float precipitation01,
            float wind01,
            float lightningRisk01)
        {
            float hour = Mathf.Repeat(
                (float)normalizedTimeOfDay01,
                1f) * 24f;
            return !paused &&
                   hour >= 8f && hour < 18.5f &&
                   precipitation01 < 0.12f &&
                   wind01 < 0.72f &&
                   lightningRisk01 < 0.2f;
        }

        public static bool ShouldPausePlayback(
            bool gameTimePaused,
            float unityTimeScale) =>
            AudioPausePolicy.ShouldPausePlayback(
                gameTimePaused,
                unityTimeScale);

        private void Update()
        {
            if (!IsConfigured)
            {
                return;
            }

            bool shouldPause = ShouldPausePlayback(
                gameTime.Snapshot.IsPaused,
                Time.timeScale);
            if (shouldPause)
            {
                if (!playbackPaused)
                {
                    playbackPaused = true;
                    pauseStartedAt = Time.unscaledTime;
                    StopPlayback();
                }

                return;
            }

            if (playbackPaused)
            {
                float pausedDuration = Mathf.Max(
                    0f,
                    Time.unscaledTime - pauseStartedAt);
                nextChainsawAt += pausedDuration;
                playbackPaused = false;
                pauseStartedAt = 0f;
                nextEnvironmentRefreshAt = Time.unscaledTime;
                if (backend.IsReady)
                {
                    RefreshEnvironment(forceRestart: true);
                }
            }

            if (!backend.IsReady)
            {
                return;
            }

            if (Time.unscaledTime >= nextEnvironmentRefreshAt)
            {
                nextEnvironmentRefreshAt =
                    Time.unscaledTime + EnvironmentRefreshSeconds;
                RefreshEnvironment(forceRestart: false);
            }

            if (Time.unscaledTime < nextChainsawAt)
            {
                return;
            }

            WeatherAudioOutput weather = environment.CurrentOutputs.Audio;
            GameTimeSnapshot clock = gameTime.Snapshot;
            if (!IsChainsawEligible(
                    clock.NormalizedTimeOfDay01,
                    clock.IsPaused,
                    weather.PrecipitationIntensity01,
                    weather.WindIntensity01,
                    weather.ThunderRisk01))
            {
                StopHandle(ref chainsawHandle, 0.2f);
                nextChainsawAt = Time.unscaledTime +
                    CalculateSuppressedRetry(ref scheduleState);
                return;
            }

            chainsawHandle.Dispose();
            chainsawHandle = backend.PostEvent(new AudioEventRequest(
                AudioProjectIds.Events.WorldChainsaw,
                chainsawEmitter,
                volume01: 0.62f,
                allowMultiple: false));
            nextChainsawAt = Time.unscaledTime +
                CalculateNextInterval(ref scheduleState, initial: false);
        }

        private void RefreshEnvironment(bool forceRestart)
        {
            WeatherAudioOutput weather = environment.CurrentOutputs.Audio;
            GameTimeSnapshot clock = gameTime.Snapshot;
            Vector3 listenerPosition = ambienceEmitter.AudioTransform != null
                ? ambienceEmitter.AudioTransform.position
                : ambienceEmitter.transform.position;

            for (int index = 0; index < layers.Count; index++)
            {
                LayerRuntime layer = layers[index];
                float targetVolume = CalculateLayerGain(
                    layer.Definition,
                    clock.NormalizedTimeOfDay01,
                    clock.IsPaused,
                    weather.PrecipitationIntensity01,
                    weather.WindIntensity01,
                    listenerPosition);
                RefreshLayer(layer, targetVolume, forceRestart);
            }

            if (!IsChainsawEligible(
                    clock.NormalizedTimeOfDay01,
                    clock.IsPaused,
                    weather.PrecipitationIntensity01,
                    weather.WindIntensity01,
                    weather.ThunderRisk01) &&
                chainsawHandle.IsPlaying)
            {
                StopHandle(ref chainsawHandle, 0.5f);
            }
        }

        private void RefreshLayer(
            LayerRuntime layer,
            float targetVolume,
            bool forceRestart)
        {
            if (targetVolume <= MinimumAudibleGain)
            {
                StopHandle(ref layer.Handle, 0.8f);
                layer.PostedVolume = -1f;
                return;
            }

            if (!forceRestart && layer.Handle.IsPlaying &&
                Mathf.Abs(targetVolume - layer.PostedVolume) <
                LayerRestartGainDelta)
            {
                return;
            }

            StopHandle(ref layer.Handle, 0.6f);
            layer.Handle = backend.PostEvent(new AudioEventRequest(
                layer.Definition.EventId,
                layer.Emitter,
                volume01: targetVolume,
                allowMultiple: false));
            layer.PostedVolume = targetVolume;
        }

        private static float CalculateSuppressedRetry(ref uint state)
        {
            state = (state * 1664525u) + 1013904223u;
            float normalized = (state & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(180f, 420f, normalized);
        }

        private void OnDisable()
        {
            StopPlayback();
        }

        private void OnDestroy()
        {
            StopPlayback();
            DestroyLayerEmitters();
            if (chainsawEmitter != null)
            {
                Destroy(chainsawEmitter.gameObject);
            }
        }

        private void EnsureEmitters()
        {
            for (int index = 0; index < Definitions.Length; index++)
            {
                WorldAmbientLayerDefinition definition = Definitions[index];
                GameObject emitterObject = new GameObject(
                    $"World Ambient - {definition.LayerId}");
                AudioEmitterAuthoring emitter =
                    emitterObject.AddComponent<AudioEmitterAuthoring>();
                emitter.transform.position = definition.WorldPosition;
                emitter.Configure(
                    $"audio.emitter.world.ambience.{definition.LayerId}",
                    backend as MonoBehaviour,
                    emitter.transform);
                layers.Add(new LayerRuntime
                {
                    Definition = definition,
                    Emitter = emitter,
                });
            }

            GameObject chainsawObject = new GameObject(
                "World Ambient - Home Forest Chainsaw");
            chainsawEmitter =
                chainsawObject.AddComponent<AudioEmitterAuthoring>();
            chainsawEmitter.transform.position = ChainsawWorldPosition;
            chainsawEmitter.Configure(
                "audio.emitter.world.chainsaw.home-forest",
                backend as MonoBehaviour,
                chainsawEmitter.transform);
        }

        private void StopPlayback()
        {
            for (int index = 0; index < layers.Count; index++)
            {
                LayerRuntime layer = layers[index];
                StopHandle(ref layer.Handle, 0.25f);
                layer.PostedVolume = -1f;
            }

            StopHandle(ref chainsawHandle, 0.1f);
        }

        private void DestroyLayerEmitters()
        {
            for (int index = 0; index < layers.Count; index++)
            {
                if (layers[index].Emitter != null)
                {
                    Destroy(layers[index].Emitter.gameObject);
                }
            }

            layers.Clear();
            if (chainsawEmitter != null)
            {
                Destroy(chainsawEmitter.gameObject);
                chainsawEmitter = null;
            }
        }

        private static void StopHandle(
            ref IAudioEventHandle handle,
            float fadeSeconds)
        {
            handle.Stop(fadeSeconds);
            handle.Dispose();
            handle = AudioEventHandles.Invalid;
        }
    }
}
