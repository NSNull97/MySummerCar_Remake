using System;
using UnityEngine;

namespace MSC.Audio.UnityFallback
{
    /// <summary>
    /// Small allocation-free gain stage for quiet temporary Phase 1 dialogue
    /// clips. Math.Tanh provides a soft ceiling instead of hard digital clips.
    /// Production Wwise dialogue will own loudness on its dedicated bus.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class UnityDialogueGainFilter : MonoBehaviour
    {
        public const float DefaultDialogueGain = 2.65f; // about +8.5 dB

        [SerializeField, Min(1f)] private float gain = DefaultDialogueGain;

        public float Gain => gain;

        public void Configure(float configuredGain)
        {
            gain = float.IsFinite(configuredGain)
                ? Mathf.Clamp(configuredGain, 1f, 6f)
                : DefaultDialogueGain;
        }

        public static float ApplySoftGain(float sample, float configuredGain)
        {
            if (!float.IsFinite(sample))
            {
                return 0f;
            }

            float safeGain = float.IsFinite(configuredGain)
                ? Mathf.Clamp(configuredGain, 1f, 6f)
                : DefaultDialogueGain;
            return (float)Math.Tanh(sample * safeGain);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
            {
                return;
            }

            float currentGain = gain;
            for (int index = 0; index < data.Length; index++)
            {
                data[index] = ApplySoftGain(data[index], currentGain);
            }
        }
    }
}
