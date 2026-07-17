using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSC.Development.WeatherLab;
using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using NUnit.Framework;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WeatherLab
{
    public sealed class WeatherLabPerformanceCapturePlayModeTests
    {
        private const string EvidenceRelativePath =
            "PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json";
        private const string ScreenshotOptInVariable =
            "MSC_WEATHERLAB_CAPTURE_SCREENSHOTS";
        private const string ScreenshotRelativeDirectory =
            "References/Weather/Milestone07B/Automated";
        private const int WarmupFrameCount = 15;
        private const int SampleFrameCount = 40;
        private const int ScreenshotWidth = 1920;
        private const int ScreenshotHeight = 1080;

        [Category("PerformanceCapture")]
        [UnityTest]
        public IEnumerator WeatherLab_RecordsMatchedAutomatedPerformanceEvidence()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                WeatherLabSceneMarker.SceneAssetPath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null);
            yield return load;
#else
            Assert.Ignore("WeatherLab capture requires the Unity Editor scene database.");
            yield break;
#endif

            WeatherLabStateController controller =
                Object.FindFirstObjectByType<WeatherLabStateController>();
            WeatherLabSceneMarker marker =
                Object.FindFirstObjectByType<WeatherLabSceneMarker>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.TryValidate(out string markerFailure), Is.True, markerFailure);

            yield return WaitForOperational(controller, 120);
            Assert.That(controller.Status.IsOperational, Is.True);
            Assert.That(controller.SwitchCamera(0), Is.True);
            controller.SetExposure(WeatherExposureContext.Exterior);
            controller.SetScheduleFrozen(true);
            controller.SetPaused(true);

            ScreenshotAvailability screenshotAvailability =
                BuildScreenshotAvailability(marker);
            CaptureSpec[] specs = BuildCaptureSpecs();
            var captures = new List<StateCapture>(specs.Length);
            for (int index = 0; index < specs.Length; index++)
            {
                CaptureSpec spec = specs[index];
                ApplySpec(controller, spec);
                yield return WaitFrames(WarmupFrameCount);

                if (spec.TriggerLightning)
                {
                    EnvironmentPresentationStatus lightningStatus =
                        controller.TriggerAmbientLightning();
                    Assert.That(lightningStatus.IsOperational, Is.True);
                }

                StateCapture stateCapture = null;
                yield return SampleState(controller, spec, value => stateCapture = value);
                Assert.That(stateCapture, Is.Not.Null);
                Assert.That(stateCapture.presentationOperational, Is.True, spec.Id);

                if (screenshotAvailability.available)
                {
                    stateCapture.screenshot = CaptureScreenshot(
                        marker.Cameras[0],
                        spec.Id);
                }

                captures.Add(stateCapture);
            }

            var evidence = new PerformanceEvidence
            {
                schemaVersion = 1,
                validatorId = "m07b-weatherlab-performance-v1",
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                executionMode = Application.isEditor ? "EditorPlayMode" : "Player",
                automatedEvidenceOnly = true,
                manualValidationClaimed = false,
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                batchMode = Application.isBatchMode,
                sampledViewportWidth = Screen.width,
                sampledViewportHeight = Screen.height,
                warmupFramesPerState = WarmupFrameCount,
                sampledFramesPerState = SampleFrameCount,
                domainOnly = new AvailabilityCapture
                {
                    available = false,
                    reason =
                        "WeatherLab public DEV commands intentionally submit presentation " +
                        "with each domain mutation; this harness does not mislabel that path " +
                        "as domain-only. Pure-domain coverage remains in EditMode tests."
                },
                screenshots = screenshotAvailability,
                states = captures.ToArray(),
                limitations = new[]
                {
                    "Editor PlayMode sampling is an automated regression baseline, not a " +
                    "standalone-player performance sign-off.",
                    "No numeric performance threshold is asserted because host load, editor " +
                    "overhead and graphics backend are capture metadata, not constants.",
                    "ProfilerRecorder counters are reported unavailable when Unity exposes " +
                    "no matching marker; unavailable values are never replaced with zero.",
                    "Optional matched screenshots are automated Camera RenderTexture captures " +
                    "and never count as manual user validation."
                }
            };

            string evidencePath = WriteEvidence(evidence);
            Assert.That(File.Exists(evidencePath), Is.True);
            Assert.That(captures.Count, Is.EqualTo(specs.Length));
            Debug.Log("M07B_WEATHERLAB_PERFORMANCE_OK path=" + evidencePath);
        }

        private static CaptureSpec[] BuildCaptureSpecs() => new[]
        {
            CaptureSpec.ForPreset("clear-high", EnvironmentPresentationPresetKind.Clear),
            CaptureSpec.ForPreset("overcast-high", EnvironmentPresentationPresetKind.Overcast),
            CaptureSpec.ForPreset("rain-high", EnvironmentPresentationPresetKind.Rain),
            CaptureSpec.ForLogical("heavy-rain-high", WeatherStateIds.HeavyRain.Value),
            CaptureSpec.ForPreset(
                "storm-lightning-high",
                EnvironmentPresentationPresetKind.Storm,
                triggerLightning: true),
            CaptureSpec.ForPreset("mist-high", EnvironmentPresentationPresetKind.Mist),
            CaptureSpec.ForPreset("night-high", EnvironmentPresentationPresetKind.Night),
            CaptureSpec.ForWetness("wetness-puddles-high"),
            CaptureSpec.ForPreset(
                "clear-low",
                EnvironmentPresentationPresetKind.Clear,
                EnvironmentQualityTier.Low),
            CaptureSpec.ForPreset(
                "clear-high-recovery",
                EnvironmentPresentationPresetKind.Clear,
                EnvironmentQualityTier.High)
        };

        private static void ApplySpec(
            WeatherLabStateController controller,
            CaptureSpec spec)
        {
            controller.SetWetness(0f, 0f, 0f, 0f);
            EnvironmentPresentationStatus qualityStatus =
                controller.ApplyQuality(spec.Quality);
            Assert.That(qualityStatus.IsOperational, Is.True, spec.Id + " quality");

            if (spec.Preset != EnvironmentPresentationPresetKind.Night &&
                spec.Preset != EnvironmentPresentationPresetKind.Mist)
            {
                bool timeAccepted = controller.TrySetDateAndTime(
                    controller.GameTime.Snapshot.Date,
                    12d * 60d * 60d,
                    out string timeFailure);
                Assert.That(timeAccepted, Is.True, timeFailure);
            }

            if (!string.IsNullOrEmpty(spec.LogicalWeatherId))
            {
                bool applied = controller.TryApplyLogicalWeather(
                    spec.LogicalWeatherId,
                    0f,
                    out string logicalFailure);
                Assert.That(applied, Is.True, logicalFailure);
            }
            else
            {
                EnvironmentPresentationStatus presetStatus =
                    controller.ApplyPreset(spec.Preset);
                Assert.That(presetStatus.IsOperational, Is.True, spec.Id + " preset");
            }

            if (spec.MaximumWetness)
            {
                controller.SetWetness(1f, 1f, 1f, 1f);
            }

            Assert.That(controller.Status.IsOperational, Is.True, spec.Id);
        }

        private static IEnumerator SampleState(
            WeatherLabStateController controller,
            CaptureSpec spec,
            Action<StateCapture> completed)
        {
            var frameMilliseconds = new List<double>(SampleFrameCount);
            long peakAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            CounterCapture mainThread;
            CounterCapture renderThread;
            CounterCapture gpuFrame;
            CounterCapture gcAllocated;

            using (CounterRecorder main = CounterRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "Main Thread",
                       "nanoseconds"))
            using (CounterRecorder render = CounterRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "Render Thread",
                       "nanoseconds"))
            using (CounterRecorder gpu = CounterRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "GPU Frame Time",
                       "nanoseconds"))
            using (CounterRecorder gc = CounterRecorder.TryCreate(
                       ProfilerCategory.Memory,
                       "GC Allocated In Frame",
                       "bytes"))
            {
                for (int frame = 0; frame < SampleFrameCount; frame++)
                {
                    double started = Time.realtimeSinceStartupAsDouble;
                    yield return null;
                    frameMilliseconds.Add(
                        (Time.realtimeSinceStartupAsDouble - started) * 1000d);
                    main.Sample();
                    render.Sample();
                    gpu.Sample();
                    gc.Sample();
                    peakAllocatedMemory = Math.Max(
                        peakAllocatedMemory,
                        Profiler.GetTotalAllocatedMemoryLong());
                }

                mainThread = main.Build();
                renderThread = render.Build();
                gpuFrame = gpu.Build();
                gcAllocated = gc.Build();
            }

            WetnessEnvironmentOutputs wetness = controller.Wetness;
            EnvironmentPresentationStatus status = controller.Status;
            completed(new StateCapture
            {
                id = spec.Id,
                quality = controller.CurrentQuality.ToString(),
                logicalWeatherId = controller.CurrentLogicalWeather.Id.Value,
                precipitationIntensity01 =
                    controller.CurrentLogicalWeather.PrecipitationIntensity01,
                fogIntensity01 = controller.CurrentLogicalWeather.FogIntensity01,
                wetnessGround01 = wetness.GroundWetness01,
                wetnessRoad01 = wetness.RoadWetness01,
                puddleAmount01 = wetness.PuddleAmount01,
                wetnessVegetation01 = wetness.VegetationWetness01,
                presentationState = status.State.ToString(),
                presentationOperational = status.IsOperational,
                presentationWarningCount = status.WarningCount,
                presentationErrorCount = status.ErrorCount,
                frameTimeMilliseconds = TimingCapture.Build(frameMilliseconds),
                mainThread = mainThread,
                renderThread = renderThread,
                gpuFrame = gpuFrame,
                gcAllocatedInFrame = gcAllocated,
                allocatedMemoryBytesAtEnd = Profiler.GetTotalAllocatedMemoryLong(),
                peakAllocatedMemoryBytes = peakAllocatedMemory,
                screenshot = ScreenshotCapture.NotAttempted(
                    "Screenshot capture was not requested or is unavailable.")
            });
        }

        private static IEnumerator WaitForOperational(
            WeatherLabStateController controller,
            int maximumFrames)
        {
            for (int frame = 0;
                 frame < maximumFrames && !controller.Status.IsOperational;
                 frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static ScreenshotAvailability BuildScreenshotAvailability(
            WeatherLabSceneMarker marker)
        {
            bool requested = string.Equals(
                Environment.GetEnvironmentVariable(ScreenshotOptInVariable),
                "1",
                StringComparison.Ordinal);
            if (!requested)
            {
                return new ScreenshotAvailability
                {
                    requested = false,
                    available = false,
                    outputDirectory = ScreenshotRelativeDirectory,
                    width = ScreenshotWidth,
                    height = ScreenshotHeight,
                    reason = "Opt-in disabled; set " + ScreenshotOptInVariable + "=1."
                };
            }

            string reason = string.Empty;
            bool available = true;
            if (Application.isBatchMode)
            {
                available = false;
                reason = "Disabled in batch mode to avoid claiming headless output as visual evidence.";
            }
            else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                available = false;
                reason = "No graphics device is available.";
            }
            else if (marker.Cameras.Count == 0 || marker.Cameras[0] == null)
            {
                available = false;
                reason = "WeatherLab exterior capture camera is unavailable.";
            }

            return new ScreenshotAvailability
            {
                requested = true,
                available = available,
                outputDirectory = ScreenshotRelativeDirectory,
                width = ScreenshotWidth,
                height = ScreenshotHeight,
                reason = reason
            };
        }

        private static ScreenshotCapture CaptureScreenshot(Camera camera, string stateId)
        {
            string projectRoot = GetProjectRoot();
            string relativePath = ScreenshotRelativeDirectory + "/" + stateId + ".png";
            string absolutePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? projectRoot);

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                target = new RenderTexture(
                    ScreenshotWidth,
                    ScreenshotHeight,
                    24,
                    RenderTextureFormat.ARGB32);
                target.Create();
                image = new Texture2D(
                    ScreenshotWidth,
                    ScreenshotHeight,
                    TextureFormat.RGB24,
                    mipChain: false,
                    linear: QualitySettings.activeColorSpace == ColorSpace.Linear);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(
                    new Rect(0f, 0f, ScreenshotWidth, ScreenshotHeight),
                    0,
                    0,
                    recalculateMipMaps: false);
                image.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(absolutePath, image.EncodeToPNG());
                return new ScreenshotCapture
                {
                    attempted = true,
                    captured = File.Exists(absolutePath),
                    relativePath = relativePath,
                    failure = string.Empty,
                    automatedOnly = true,
                    manualValidationClaimed = false
                };
            }
            catch (Exception exception)
            {
                return new ScreenshotCapture
                {
                    attempted = true,
                    captured = false,
                    relativePath = relativePath,
                    failure = exception.GetType().Name + ": " + exception.Message,
                    automatedOnly = true,
                    manualValidationClaimed = false
                };
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null)
                {
                    Object.Destroy(image);
                }

                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
            }
        }

        private static string WriteEvidence(PerformanceEvidence evidence)
        {
            string projectRoot = GetProjectRoot();
            string path = Path.Combine(
                projectRoot,
                EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
            File.WriteAllText(
                path,
                JsonUtility.ToJson(evidence, prettyPrint: true) + Environment.NewLine);
            return path;
        }

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root is unavailable.");

        private sealed class CaptureSpec
        {
            public string Id { get; private set; } = string.Empty;
            public EnvironmentPresentationPresetKind Preset { get; private set; }
            public EnvironmentQualityTier Quality { get; private set; }
            public string LogicalWeatherId { get; private set; } = string.Empty;
            public bool MaximumWetness { get; private set; }
            public bool TriggerLightning { get; private set; }

            public static CaptureSpec ForPreset(
                string id,
                EnvironmentPresentationPresetKind preset,
                EnvironmentQualityTier quality = EnvironmentQualityTier.High,
                bool triggerLightning = false) => new CaptureSpec
            {
                Id = id,
                Preset = preset,
                Quality = quality,
                TriggerLightning = triggerLightning
            };

            public static CaptureSpec ForLogical(
                string id,
                string logicalWeatherId) => new CaptureSpec
            {
                Id = id,
                Preset = EnvironmentPresentationPresetKind.Clear,
                Quality = EnvironmentQualityTier.High,
                LogicalWeatherId = logicalWeatherId
            };

            public static CaptureSpec ForWetness(string id) => new CaptureSpec
            {
                Id = id,
                Preset = EnvironmentPresentationPresetKind.Clear,
                Quality = EnvironmentQualityTier.High,
                MaximumWetness = true
            };
        }

        private sealed class CounterRecorder : IDisposable
        {
            private readonly ProfilerRecorder recorder;
            private readonly string markerName;
            private readonly string unit;
            private long maximum;
            private long last;
            private int sampleCount;

            private CounterRecorder(
                ProfilerRecorder recorder,
                string markerName,
                string unit)
            {
                this.recorder = recorder;
                this.markerName = markerName;
                this.unit = unit;
            }

            public static CounterRecorder TryCreate(
                ProfilerCategory category,
                string markerName,
                string unit)
            {
                try
                {
                    return new CounterRecorder(
                        ProfilerRecorder.StartNew(category, markerName, 64),
                        markerName,
                        unit);
                }
                catch (Exception)
                {
                    return new CounterRecorder(default, markerName, unit);
                }
            }

            public void Sample()
            {
                if (!recorder.Valid || recorder.Count == 0)
                {
                    return;
                }

                last = recorder.LastValue;
                maximum = Math.Max(maximum, last);
                sampleCount++;
            }

            public CounterCapture Build()
            {
                if (!recorder.Valid || sampleCount == 0)
                {
                    return new CounterCapture
                    {
                        markerName = markerName,
                        unit = unit,
                        available = false,
                        reason = "ProfilerRecorder marker is unavailable or returned no samples."
                    };
                }

                if (string.Equals(unit, "nanoseconds", StringComparison.Ordinal) &&
                    maximum <= 0)
                {
                    return new CounterCapture
                    {
                        markerName = markerName,
                        unit = unit,
                        available = false,
                        reason =
                            "ProfilerRecorder returned zero for every timing sample; " +
                            "the timing is treated as unavailable."
                    };
                }

                return new CounterCapture
                {
                    markerName = markerName,
                    unit = unit,
                    available = true,
                    sampleCount = sampleCount,
                    lastValue = last,
                    maximumValue = maximum,
                    reason = string.Empty
                };
            }

            public void Dispose()
            {
                if (recorder.Valid)
                {
                    recorder.Dispose();
                }
            }
        }

        [Serializable]
        private sealed class PerformanceEvidence
        {
            public int schemaVersion;
            public string validatorId = string.Empty;
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string executionMode = string.Empty;
            public bool automatedEvidenceOnly;
            public bool manualValidationClaimed;
            public string operatingSystem = string.Empty;
            public string processorType = string.Empty;
            public string graphicsDeviceName = string.Empty;
            public string graphicsDeviceType = string.Empty;
            public string graphicsDeviceVersion = string.Empty;
            public string activeColorSpace = string.Empty;
            public bool batchMode;
            public int sampledViewportWidth;
            public int sampledViewportHeight;
            public int warmupFramesPerState;
            public int sampledFramesPerState;
            public AvailabilityCapture domainOnly = new AvailabilityCapture();
            public ScreenshotAvailability screenshots = new ScreenshotAvailability();
            public StateCapture[] states = Array.Empty<StateCapture>();
            public string[] limitations = Array.Empty<string>();
        }

        [Serializable]
        private sealed class StateCapture
        {
            public string id = string.Empty;
            public string quality = string.Empty;
            public string logicalWeatherId = string.Empty;
            public float precipitationIntensity01;
            public float fogIntensity01;
            public float wetnessGround01;
            public float wetnessRoad01;
            public float puddleAmount01;
            public float wetnessVegetation01;
            public string presentationState = string.Empty;
            public bool presentationOperational;
            public int presentationWarningCount;
            public int presentationErrorCount;
            public TimingCapture frameTimeMilliseconds = new TimingCapture();
            public CounterCapture mainThread = new CounterCapture();
            public CounterCapture renderThread = new CounterCapture();
            public CounterCapture gpuFrame = new CounterCapture();
            public CounterCapture gcAllocatedInFrame = new CounterCapture();
            public long allocatedMemoryBytesAtEnd;
            public long peakAllocatedMemoryBytes;
            public ScreenshotCapture screenshot = new ScreenshotCapture();
        }

        [Serializable]
        private sealed class TimingCapture
        {
            public int sampleCount;
            public double meanMilliseconds;
            public double p95Milliseconds;
            public double maximumMilliseconds;

            public static TimingCapture Build(List<double> samples)
            {
                if (samples == null || samples.Count == 0)
                {
                    return new TimingCapture();
                }

                double sum = 0d;
                double maximum = 0d;
                double[] sorted = samples.ToArray();
                for (int index = 0; index < sorted.Length; index++)
                {
                    sum += sorted[index];
                    maximum = Math.Max(maximum, sorted[index]);
                }

                Array.Sort(sorted);
                int p95Index = Math.Max(
                    0,
                    Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95d) - 1));
                return new TimingCapture
                {
                    sampleCount = sorted.Length,
                    meanMilliseconds = sum / sorted.Length,
                    p95Milliseconds = sorted[p95Index],
                    maximumMilliseconds = maximum
                };
            }
        }

        [Serializable]
        private sealed class CounterCapture
        {
            public string markerName = string.Empty;
            public string unit = string.Empty;
            public bool available;
            public int sampleCount;
            public long lastValue;
            public long maximumValue;
            public string reason = string.Empty;
        }

        [Serializable]
        private sealed class AvailabilityCapture
        {
            public bool available;
            public string reason = string.Empty;
        }

        [Serializable]
        private sealed class ScreenshotAvailability
        {
            public bool requested;
            public bool available;
            public string outputDirectory = string.Empty;
            public int width;
            public int height;
            public string reason = string.Empty;
        }

        [Serializable]
        private sealed class ScreenshotCapture
        {
            public bool attempted;
            public bool captured;
            public string relativePath = string.Empty;
            public string failure = string.Empty;
            public bool automatedOnly;
            public bool manualValidationClaimed;

            public static ScreenshotCapture NotAttempted(string reason) =>
                new ScreenshotCapture
                {
                    attempted = false,
                    captured = false,
                    failure = reason,
                    automatedOnly = true,
                    manualValidationClaimed = false
                };
        }
    }
}
