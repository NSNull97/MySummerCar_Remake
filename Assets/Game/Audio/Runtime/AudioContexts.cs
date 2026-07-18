using System;
using UnityEngine;

namespace MSC.Audio
{
    public enum AudioListenerSpace
    {
        Exterior = 0,
        Sheltered = 1,
        Interior = 2,
        VehicleInterior = 3,
    }

    public readonly struct AudioEnvironmentContext
    {
        public AudioEnvironmentContext(
            AudioListenerSpace listenerSpace,
            float shelter01,
            float obstruction01,
            float reverbSend01,
            float precipitation01,
            float wind01,
            float normalizedDayTime01)
        {
            if (!Enum.IsDefined(typeof(AudioListenerSpace), listenerSpace))
            {
                throw new ArgumentOutOfRangeException(nameof(listenerSpace));
            }

            ListenerSpace = listenerSpace;
            Shelter01 = AudioMath.Clamp01(shelter01);
            Obstruction01 = AudioMath.Clamp01(obstruction01);
            ReverbSend01 = AudioMath.Clamp01(reverbSend01);
            Precipitation01 = AudioMath.Clamp01(precipitation01);
            Wind01 = AudioMath.Clamp01(wind01);
            NormalizedDayTime01 = AudioMath.Clamp01(normalizedDayTime01);
        }

        public AudioListenerSpace ListenerSpace { get; }
        public float Shelter01 { get; }
        public float Obstruction01 { get; }
        public float ReverbSend01 { get; }
        public float Precipitation01 { get; }
        public float Wind01 { get; }
        public float NormalizedDayTime01 { get; }

        public static AudioEnvironmentContext Exterior => new AudioEnvironmentContext(
            AudioListenerSpace.Exterior,
            0f,
            0f,
            0f,
            0f,
            0f,
            0.5f);
    }

    public readonly struct AudioSurfaceContext
    {
        public AudioSurfaceContext(
            AudioSwitchId surfaceId,
            float wetness01,
            float roughness01)
        {
            SurfaceId = surfaceId;
            Wetness01 = AudioMath.Clamp01(wetness01);
            Roughness01 = AudioMath.Clamp01(roughness01);
        }

        public AudioSwitchId SurfaceId { get; }
        public float Wetness01 { get; }
        public float Roughness01 { get; }
        public bool IsKnown => !SurfaceId.IsEmpty;

        public static AudioSurfaceContext Unknown => default;
    }

    public readonly struct AudioListenerContext
    {
        public AudioListenerContext(
            string stableListenerId,
            Vector3 worldPosition,
            Vector3 forward,
            Vector3 up,
            AudioEnvironmentContext environment,
            bool hasFocus = true)
        {
            StableListenerId = AudioStableId.Validate(
                stableListenerId,
                nameof(stableListenerId));
            WorldPosition = AudioMath.FiniteVector(worldPosition);
            Forward = NormalizeDirection(forward, Vector3.forward);
            Up = NormalizeDirection(up, Vector3.up);
            Environment = environment;
            HasFocus = hasFocus;
        }

        public string StableListenerId { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 Forward { get; }
        public Vector3 Up { get; }
        public AudioEnvironmentContext Environment { get; }
        public bool HasFocus { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(StableListenerId);

        private static Vector3 NormalizeDirection(Vector3 value, Vector3 fallback)
        {
            value = AudioMath.FiniteVector(value);
            return value.sqrMagnitude < 0.000001f ? fallback : value.normalized;
        }
    }

    internal static class AudioMath
    {
        public static float FiniteOrZero(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        public static float Clamp01(float value) => Mathf.Clamp01(FiniteOrZero(value));

        public static float NonNegative(float value) => Mathf.Max(0f, FiniteOrZero(value));

        public static Vector3 FiniteVector(Vector3 value) => new Vector3(
            FiniteOrZero(value.x),
            FiniteOrZero(value.y),
            FiniteOrZero(value.z));
    }
}
