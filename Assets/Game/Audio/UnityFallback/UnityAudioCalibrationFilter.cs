using System;
using UnityEngine;

namespace MSC.Audio.UnityFallback
{
    /// <summary>
    /// Only the calibrated gain exceeding AudioSource's 0..1 volume range.
    /// Ordinary voices and low-level calibrated voices bypass this stage.
    /// This is a per-voice sample-peak ceiling, not a final-bus true-peak limiter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class UnityAudioCalibrationFilter : MonoBehaviour
    {
        public const float PeakCeiling = .95f;
        private const float Knee = .8f;
        private volatile float overflowGain = 1f;
        public float OverflowGain => overflowGain;

        public void Configure(float gain)
        {
            overflowGain = float.IsFinite(gain) ? Mathf.Clamp(gain, 1f, 8f) : 1f;
            enabled = overflowGain > 1f;
        }

        public static float ApplyOverflow(float sample, float gain)
        {
            if (!float.IsFinite(sample)) return 0f;
            if (!float.IsFinite(gain) || gain <= 1f) return sample;
            float amplified = sample * Math.Min(gain, 8f);
            float absolute = Math.Abs(amplified);
            if (absolute <= Knee) return amplified;
            // Unit slope at the knee, continuous and symmetric. No per-sample
            // Unity API calls, allocations or locks on the audio thread.
            float limited = Knee + (PeakCeiling - Knee) *
                (float)Math.Tanh((absolute - Knee) / (PeakCeiling - Knee));
            return amplified < 0f ? -limited : limited;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = overflowGain;
            if (data == null || gain <= 1f) return;
            for (int i = 0; i < data.Length; i++) data[i] = ApplyOverflow(data[i], gain);
        }
    }
}
