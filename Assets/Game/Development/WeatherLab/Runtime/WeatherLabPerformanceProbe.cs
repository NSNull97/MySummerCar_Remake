using UnityEngine;

namespace MSC.Development.WeatherLab
{
    public sealed class WeatherLabPerformanceProbe : MonoBehaviour
    {
        [SerializeField, Min(30)] private int targetSampleCount = 600;

        private bool isCapturing;
        private int sampleCount;
        private double accumulatedMilliseconds;
        private float maximumMilliseconds;

        public bool IsCapturing => isCapturing;

        public int SampleCount => sampleCount;

        public float AverageFrameMilliseconds => sampleCount == 0
            ? 0f
            : (float)(accumulatedMilliseconds / sampleCount);

        public float MaximumFrameMilliseconds => maximumMilliseconds;

        public void BeginCapture()
        {
            sampleCount = 0;
            accumulatedMilliseconds = 0d;
            maximumMilliseconds = 0f;
            isCapturing = true;
        }

        public void StopCapture()
        {
            isCapturing = false;
        }

        private void LateUpdate()
        {
            if (!isCapturing)
            {
                return;
            }

            float milliseconds = Time.unscaledDeltaTime * 1000f;
            sampleCount++;
            accumulatedMilliseconds += milliseconds;
            maximumMilliseconds = Mathf.Max(maximumMilliseconds, milliseconds);
            if (sampleCount >= targetSampleCount)
            {
                isCapturing = false;
            }
        }
    }
}
