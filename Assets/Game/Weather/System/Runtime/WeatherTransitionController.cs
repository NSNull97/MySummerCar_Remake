using System;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System
{
    [Serializable]
    public struct WeatherTransitionSnapshot
    {
        public WeatherState FromState;
        public WeatherState TargetState;
        public float DurationGameSeconds;
        public float ElapsedGameSeconds;
    }

    /// <summary>
    /// Blends all weather channels continuously. It contains no rendering or
    /// vendor logic and is safe to restore mid-transition.
    /// </summary>
    public sealed class WeatherTransitionController
    {
        private static readonly ProfilerMarker TransitionMarker =
            new ProfilerMarker("Weather.Transition");

        private WeatherState from;
        private WeatherState target;
        private WeatherTransitionCurves curves;
        private float durationGameSeconds;
        private float elapsedGameSeconds;

        public bool IsInitialized { get; private set; }
        public float Progress01 => durationGameSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(elapsedGameSeconds / durationGameSeconds);
        public bool IsComplete => IsInitialized && Progress01 >= 1f;
        public WeatherState CurrentState => IsInitialized
            ? Blend(from, target, Progress01, curves)
            : throw new InvalidOperationException("Weather transition is not initialized.");

        public void Begin(
            in WeatherState source,
            in WeatherState destination,
            float durationSeconds,
            WeatherTransitionCurves transitionCurves)
        {
            if (!source.IsValid || !destination.IsValid ||
                !WeatherState.IsFinite(durationSeconds) || durationSeconds < 0f)
            {
                throw new ArgumentException("Weather transition configuration is invalid.");
            }

            from = source;
            target = destination;
            durationGameSeconds = durationSeconds;
            elapsedGameSeconds = 0f;
            curves = transitionCurves ?? new WeatherTransitionCurves();
            IsInitialized = true;
        }

        public WeatherState Advance(float deltaGameSeconds)
        {
            if (!IsInitialized || !WeatherState.IsFinite(deltaGameSeconds) ||
                deltaGameSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaGameSeconds));
            }

            elapsedGameSeconds = Mathf.Min(
                durationGameSeconds,
                elapsedGameSeconds + deltaGameSeconds);
            return CurrentState;
        }

        public WeatherTransitionSnapshot CaptureSnapshot()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Weather transition is not initialized.");
            }

            return new WeatherTransitionSnapshot
            {
                FromState = from,
                TargetState = target,
                DurationGameSeconds = durationGameSeconds,
                ElapsedGameSeconds = elapsedGameSeconds,
            };
        }

        public void Restore(
            in WeatherTransitionSnapshot snapshot,
            WeatherTransitionCurves transitionCurves)
        {
            if (!snapshot.FromState.IsValid || !snapshot.TargetState.IsValid ||
                !WeatherState.IsFinite(snapshot.DurationGameSeconds) ||
                snapshot.DurationGameSeconds < 0f ||
                !WeatherState.IsFinite(snapshot.ElapsedGameSeconds) ||
                snapshot.ElapsedGameSeconds < 0f ||
                snapshot.ElapsedGameSeconds > snapshot.DurationGameSeconds)
            {
                throw new ArgumentException("Weather transition snapshot is invalid.");
            }

            from = snapshot.FromState;
            target = snapshot.TargetState;
            durationGameSeconds = snapshot.DurationGameSeconds;
            elapsedGameSeconds = snapshot.ElapsedGameSeconds;
            curves = transitionCurves ?? new WeatherTransitionCurves();
            IsInitialized = true;
        }

        public static WeatherState Blend(
            in WeatherState source,
            in WeatherState destination,
            float progress01,
            WeatherTransitionCurves transitionCurves)
        {
            using (TransitionMarker.Auto())
            {
                return WeatherState.Lerp(
                    source,
                    destination,
                    Mathf.Clamp01(progress01),
                    transitionCurves);
            }
        }
    }
}
