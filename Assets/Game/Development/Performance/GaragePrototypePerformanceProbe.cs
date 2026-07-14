#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.GaragePrototype
{
    /// <summary>
    /// Opt-in development capture used by the Milestone 3 standalone performance build.
    /// The implementation is compiled only for the Editor or development players and remains
    /// disabled unless a destination follows the -msc-m3-capture argument.
    /// </summary>
    public sealed class GaragePrototypePerformanceProbe : MonoBehaviour
    {
        private const string CaptureArgument = "-msc-m3-capture";
        private const int WarmupFrameCount = 180;
        private const int SampleFrameCount = 600;

        private readonly List<float> frameTimesMilliseconds =
            new List<float>(SampleFrameCount);
        private readonly List<float> cpuFrameTimesMilliseconds =
            new List<float>(SampleFrameCount);
        private readonly List<float> gpuFrameTimesMilliseconds =
            new List<float>(SampleFrameCount);
        private readonly FrameTiming[] latestFrameTiming = new FrameTiming[1];
        private readonly System.Diagnostics.Stopwatch renderStopwatch =
            new System.Diagnostics.Stopwatch();

        private string capturePath = string.Empty;
        private int observedFrameCount;
        private Camera captureCamera;
        private RenderTexture captureTarget;
        private RenderPipeline.StandardRequest renderRequest;

        private void Awake()
        {
            capturePath = ResolveCapturePath(Environment.GetCommandLineArgs());
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                enabled = false;
                return;
            }

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            captureCamera = Camera.main;
            if (captureCamera == null)
            {
                throw new InvalidOperationException("Milestone 3 performance scene has no Main Camera.");
            }

            captureTarget = new RenderTexture(
                1920,
                1080,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "M3_PerformanceCapture_1080p",
                useMipMap = false,
                autoGenerateMips = false
            };
            captureTarget.Create();
            renderRequest = new RenderPipeline.StandardRequest
            {
                destination = captureTarget,
                mipLevel = 0,
                slice = 0,
                face = CubemapFace.Unknown
            };
            if (!RenderPipeline.SupportsRenderRequest(captureCamera, renderRequest))
            {
                throw new InvalidOperationException(
                    "The active render pipeline does not support StandardRequest capture.");
            }

            FrameTimingManager.CaptureFrameTimings();
        }

        private void Update()
        {
            observedFrameCount++;
            renderStopwatch.Restart();
            RenderPipeline.SubmitRenderRequest(captureCamera, renderRequest);
            renderStopwatch.Stop();
            float submittedRenderMilliseconds = (float)renderStopwatch.Elapsed.TotalMilliseconds;
            if (observedFrameCount <= WarmupFrameCount)
            {
                FrameTimingManager.CaptureFrameTimings();
                return;
            }

            uint timingCount = FrameTimingManager.GetLatestTimings(1, latestFrameTiming);
            float frameTime = Mathf.Max(
                Time.unscaledDeltaTime * 1000f,
                submittedRenderMilliseconds);
            if (timingCount > 0)
            {
                float cpuTime = (float)latestFrameTiming[0].cpuFrameTime;
                float gpuTime = (float)latestFrameTiming[0].gpuFrameTime;
                if (cpuTime > 0f)
                {
                    cpuFrameTimesMilliseconds.Add(cpuTime);
                    frameTime = Mathf.Max(frameTime, cpuTime);
                }

                if (gpuTime > 0f)
                {
                    gpuFrameTimesMilliseconds.Add(gpuTime);
                    frameTime = Mathf.Max(frameTime, gpuTime);
                }
            }

            frameTimesMilliseconds.Add(frameTime);
            FrameTimingManager.CaptureFrameTimings();
            if (frameTimesMilliseconds.Count < SampleFrameCount)
            {
                return;
            }

            WriteCaptureAndQuit();
            enabled = false;
        }

        private void WriteCaptureAndQuit()
        {
            frameTimesMilliseconds.Sort();
            float total = 0f;
            for (int index = 0; index < frameTimesMilliseconds.Count; index++)
            {
                total += frameTimesMilliseconds[index];
            }

            float mean = total / frameTimesMilliseconds.Count;
            cpuFrameTimesMilliseconds.Sort();
            gpuFrameTimesMilliseconds.Sort();
            var capture = new PerformanceCapture
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsMemoryMegabytes = SystemInfo.graphicsMemorySize,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
                width = Screen.width,
                height = Screen.height,
                warmupFrames = WarmupFrameCount,
                sampledFrames = SampleFrameCount,
                meanFrameTimeMilliseconds = mean,
                medianFrameTimeMilliseconds = Percentile(0.5f),
                percentile95FrameTimeMilliseconds = Percentile(0.95f),
                percentile99FrameTimeMilliseconds = Percentile(0.99f),
                worstFrameTimeMilliseconds = frameTimesMilliseconds[frameTimesMilliseconds.Count - 1],
                estimatedMeanFramesPerSecond = mean > 0.0001f ? 1000f / mean : 0f,
                frameTimingSamples = Mathf.Max(
                    cpuFrameTimesMilliseconds.Count,
                    gpuFrameTimesMilliseconds.Count),
                meanCpuFrameTimeMilliseconds = Mean(cpuFrameTimesMilliseconds),
                percentile95CpuFrameTimeMilliseconds = Percentile(cpuFrameTimesMilliseconds, 0.95f),
                meanGpuFrameTimeMilliseconds = Mean(gpuFrameTimesMilliseconds),
                percentile95GpuFrameTimeMilliseconds = Percentile(gpuFrameTimesMilliseconds, 0.95f),
                offscreenRenderWidth = captureTarget.width,
                offscreenRenderHeight = captureTarget.height
            };

            string fullPath = Path.GetFullPath(capturePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Performance capture path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(capture, true));
            WriteVerificationScreenshot(Path.ChangeExtension(fullPath, ".png"));
            Debug.Log("M3_PERFORMANCE_CAPTURE_OK path=" + fullPath);
            Application.Quit(0);
        }

        private float Percentile(float percentile)
        {
            return Percentile(frameTimesMilliseconds, percentile);
        }

        private static float Percentile(List<float> samples, float percentile)
        {
            if (samples.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(
                Mathf.CeilToInt(samples.Count * percentile) - 1,
                0,
                samples.Count - 1);
            return samples[index];
        }

        private static float Mean(List<float> samples)
        {
            if (samples.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int index = 0; index < samples.Count; index++)
            {
                total += samples[index];
            }

            return total / samples.Count;
        }

        private void WriteVerificationScreenshot(string path)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = captureTarget;
            var image = new Texture2D(
                captureTarget.width,
                captureTarget.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: false);
            image.ReadPixels(
                new Rect(0f, 0f, captureTarget.width, captureTarget.height),
                0,
                0,
                recalculateMipMaps: false);
            image.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
            RenderTexture.active = previous;
        }

        private void OnDestroy()
        {
            if (captureTarget != null)
            {
                captureTarget.Release();
                Destroy(captureTarget);
            }
        }

        private static string ResolveCapturePath(string[] arguments)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], CaptureArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        [Serializable]
        private sealed class PerformanceCapture
        {
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string operatingSystem = string.Empty;
            public string processor = string.Empty;
            public int processorCount;
            public string graphicsDevice = string.Empty;
            public int graphicsMemoryMegabytes;
            public string graphicsApi = string.Empty;
            public string qualityLevel = string.Empty;
            public int width;
            public int height;
            public int warmupFrames;
            public int sampledFrames;
            public float meanFrameTimeMilliseconds;
            public float medianFrameTimeMilliseconds;
            public float percentile95FrameTimeMilliseconds;
            public float percentile99FrameTimeMilliseconds;
            public float worstFrameTimeMilliseconds;
            public float estimatedMeanFramesPerSecond;
            public int frameTimingSamples;
            public float meanCpuFrameTimeMilliseconds;
            public float percentile95CpuFrameTimeMilliseconds;
            public float meanGpuFrameTimeMilliseconds;
            public float percentile95GpuFrameTimeMilliseconds;
            public int offscreenRenderWidth;
            public int offscreenRenderHeight;
        }
    }
}
#else
using UnityEngine;

namespace MSC.World.GaragePrototype
{
    /// <summary>
    /// Release-player placeholder that preserves the serialized scene reference without shipping
    /// profiling, file-output or synchronous render-capture behavior.
    /// </summary>
    public sealed class GaragePrototypePerformanceProbe : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }
    }
}
#endif
