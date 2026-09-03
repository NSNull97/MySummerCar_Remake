using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MSC.Presentation.AntiAliasing.Tests.PlayMode
{
    public sealed class AntiAliasingPerformancePlayModeTests
    {
        private const int OutputWidth = 1280;
        private const int OutputHeight = 720;
        private const int WarmupFrames = 12;
        private const int SampleFrames = 24;

        private static readonly ScenarioDefinition[] Scenarios =
        {
            new ScenarioDefinition("01_StaticOutdoor", ScenarioKind.StaticOutdoor),
            new ScenarioDefinition("02_SlowWalk", ScenarioKind.SlowWalk),
            new ScenarioDefinition("03_FastCameraTurn", ScenarioKind.FastCameraTurn),
            new ScenarioDefinition("04_FastVehicle", ScenarioKind.FastVehicle),
            new ScenarioDefinition("05_ForestGrassFoliage", ScenarioKind.ForestGrassFoliage),
            new ScenarioDefinition("06_ThinGeometryAgainstSky", ScenarioKind.ThinGeometryAgainstSky),
            new ScenarioDefinition("07_NightHeadlightsEmissive", ScenarioKind.NightHeadlightsEmissive),
            new ScenarioDefinition("08_InteriorContrastDetails", ScenarioKind.InteriorContrastDetails),
            new ScenarioDefinition("09_FieldOfViewChange", ScenarioKind.FieldOfViewChange),
            new ScenarioDefinition("10_ResolutionChange", ScenarioKind.ResolutionChange),
            new ScenarioDefinition("11_RuntimeAaSwitch", ScenarioKind.RuntimeAaSwitch),
            new ScenarioDefinition("12_TeleportOrSaveLoad", ScenarioKind.TeleportOrSaveLoad),
            new ScenarioDefinition("13_HardCameraCut", ScenarioKind.HardCameraCut),
            new ScenarioDefinition("14_MultipleActiveCameras", ScenarioKind.MultipleActiveCameras),
        };

        private static readonly AntiAliasingMode[] Modes =
        {
            AntiAliasingMode.Off,
            AntiAliasingMode.Fxaa,
            AntiAliasingMode.Smaa,
            AntiAliasingMode.Taa,
            AntiAliasingMode.TemporalUpscaler,
            AntiAliasingMode.MaximumQuality,
        };

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureFourteenScenarioPerformanceMatrix()
        {
            int originalVSync = QualitySettings.vSyncCount;
            int originalTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            AntiAliasingController controller = AntiAliasingController.Instance;
            controller.SetExternalDlssRequest(false, 1u);
            PerformanceSceneRig rig = PerformanceSceneRig.Create();
            PerformanceReport report = CreateReport(controller, rig);
            ProfilerRecorder postProcessingRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "PostProcessing",
                32);
            ProfilerRecorder motionVectorsRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "CameraMotionVectors",
                32);
            ProfilerRecorder objectMotionVectorsRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "ObjectsMotionVector",
                32);
            ProfilerRecorder gpuFrameRecorder = TryStartRecorder(
                ProfilerCategory.Internal,
                "GPU Frame Time",
                32);

            string configuredReportRoot =
                Environment.GetEnvironmentVariable("MSC_AA_REPORT_ROOT");
            string projectRoot = string.IsNullOrWhiteSpace(configuredReportRoot)
                ? Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty
                : configuredReportRoot;
            string reportDirectory = Path.Combine(projectRoot, "Reports", "AntiAliasing");
            string captureDirectory = Path.Combine(reportDirectory, "VisualCaptures");
            Directory.CreateDirectory(captureDirectory);

            try
            {
                yield return null;

                for (int modeIndex = 0; modeIndex < Modes.Length; modeIndex++)
                {
                    AntiAliasingMode measuredMode = Modes[modeIndex];
                    controller.SetMode(measuredMode, persist: false);
                    rig.ResetScenario();
                    yield return RenderFrames(WarmupFrames);

                    for (int scenarioIndex = 0; scenarioIndex < Scenarios.Length; scenarioIndex++)
                    {
                        ScenarioDefinition scenario = Scenarios[scenarioIndex];
                        controller.SetMode(measuredMode, persist: false);
                        rig.ResetScenario();
                        ConfigureScenarioStart(rig, scenario.Kind);
                        yield return RenderFrames(WarmupFrames);

                        double wallTimeTotalMs = 0d;
                        double cpuTimingTotalMs = 0d;
                        double gpuTimingTotalMs = 0d;
                        int cpuTimingSamples = 0;
                        int gpuTimingSamples = 0;
                        int frameTimingGpuSamples = 0;
                        int profilerGpuSamples = 0;
                        double postProcessingTotalMs = 0d;
                        double motionVectorsTotalMs = 0d;
                        int postProcessingSamples = 0;
                        int motionVectorSamples = 0;
                        FrameTiming[] frameTimings = new FrameTiming[1];
                        long captureStartQpc = Stopwatch.GetTimestamp();

                        for (int frame = 0; frame < SampleFrames; frame++)
                        {
                            UpdateScenario(rig, controller, scenario.Kind, measuredMode, frame);
                            FrameTimingManager.CaptureFrameTimings();
                            Stopwatch stopwatch = Stopwatch.StartNew();
                            yield return null;
                            stopwatch.Stop();
                            wallTimeTotalMs += stopwatch.Elapsed.TotalMilliseconds;

                            uint timingCount = FrameTimingManager.GetLatestTimings(1u, frameTimings);
                            if (timingCount > 0u)
                            {
                                if (frameTimings[0].cpuFrameTime > 0d)
                                {
                                    cpuTimingTotalMs += frameTimings[0].cpuFrameTime;
                                    cpuTimingSamples++;
                                }
                                if (frameTimings[0].gpuFrameTime > 0d)
                                {
                                    gpuTimingTotalMs += frameTimings[0].gpuFrameTime;
                                    gpuTimingSamples++;
                                    frameTimingGpuSamples++;
                                }
                            }

                            if (frameTimingGpuSamples == 0 &&
                                gpuFrameRecorder.Valid &&
                                gpuFrameRecorder.LastValue > 0L)
                            {
                                gpuTimingTotalMs += gpuFrameRecorder.LastValue * 0.000001d;
                                gpuTimingSamples++;
                                profilerGpuSamples++;
                            }

                            if (postProcessingRecorder.Valid && postProcessingRecorder.LastValue > 0L)
                            {
                                postProcessingTotalMs += postProcessingRecorder.LastValue * 0.000001d;
                                postProcessingSamples++;
                            }
                            if (motionVectorsRecorder.Valid && motionVectorsRecorder.LastValue > 0L)
                            {
                                motionVectorsTotalMs += motionVectorsRecorder.LastValue * 0.000001d;
                                motionVectorSamples++;
                            }
                            if (objectMotionVectorsRecorder.Valid &&
                                objectMotionVectorsRecorder.LastValue > 0L)
                            {
                                motionVectorsTotalMs +=
                                    objectMotionVectorsRecorder.LastValue * 0.000001d;
                                if (!(motionVectorsRecorder.Valid && motionVectorsRecorder.LastValue > 0L))
                                {
                                    motionVectorSamples++;
                                }
                            }
                        }
                        long captureEndQpc = Stopwatch.GetTimestamp();

                        controller.SetMode(measuredMode, persist: false);
                        AntiAliasingTelemetry telemetry = controller.GetTelemetry(rig.MainCamera);
                        PerformanceRecord record = new PerformanceRecord
                        {
                            RequestedMode = measuredMode.ToString(),
                            EffectiveMode = telemetry.EffectiveMode.ToString(),
                            Scenario = scenario.Name,
                            CpuFrameTimeMs = cpuTimingSamples > 0
                                ? cpuTimingTotalMs / cpuTimingSamples
                                : wallTimeTotalMs / SampleFrames,
                            CpuTimingSource = cpuTimingSamples > 0
                                ? "FrameTimingManager"
                                : "wall-clock coroutine frame",
                            GpuFrameTimeMs = gpuTimingSamples > 0
                                ? gpuTimingTotalMs / gpuTimingSamples
                                : -1d,
                            GpuTimingAvailable = gpuTimingSamples > 0,
                            GpuTimingSource = frameTimingGpuSamples > 0
                                ? "FrameTimingManager"
                                : profilerGpuSamples > 0
                                    ? "ProfilerRecorder: GPU Frame Time"
                                    : "unavailable in Editor batch mode",
                            PostProcessingCpuMarkerMs = postProcessingSamples > 0
                                ? postProcessingTotalMs / postProcessingSamples
                                : -1d,
                            PostProcessingMarkerAvailable = postProcessingSamples > 0,
                            MotionVectorsCpuMarkerMs = motionVectorSamples > 0
                                ? motionVectorsTotalMs / motionVectorSamples
                                : -1d,
                            MotionVectorsMarkerAvailable = motionVectorSamples > 0,
                            OutputResolution =
                                $"{telemetry.OutputResolution.x}x{telemetry.OutputResolution.y}",
                            InternalResolution =
                                $"{telemetry.InternalResolution.x}x{telemetry.InternalResolution.y}",
                            DynamicResolutionScale = telemetry.DynamicResolutionScale,
                            DynamicResolutionActive =
                                telemetry.Upscaler != TemporalUpscalerKind.None,
                            Upscaler = telemetry.Upscaler.ToString(),
                            UpscalerQuality = telemetry.UpscalerQuality,
                            MotionVectorsActive = telemetry.MotionVectors,
                            ManagedCameraCount = telemetry.ManagedCameraCount,
                            FallbackReason = telemetry.FallbackReason,
                            VisualCheck = VisualCheckDescription(scenario.Kind, measuredMode),
                            CaptureStartQpc = captureStartQpc,
                            CaptureEndQpc = captureEndQpc,
                        };

                        if (ShouldCaptureVisual(scenario.Kind, measuredMode))
                        {
                            string fileName = measuredMode + "_" + scenario.Name + ".png";
                            string absoluteCapturePath = Path.Combine(captureDirectory, fileName);
                            CaptureRenderTexture(rig.MainTarget, absoluteCapturePath);
                            record.VisualCapture = "Reports/AntiAliasing/VisualCaptures/" + fileName;
                        }

                        report.Records.Add(record);
                    }
                }

                report.CompletedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                WriteReport(reportDirectory, report);
                Assert.That(report.Records.Count, Is.EqualTo(Modes.Length * Scenarios.Length));
                Assert.That(report.Records.Exists(record => record.CpuFrameTimeMs > 0d), Is.True);
            }
            finally
            {
                postProcessingRecorder.Dispose();
                motionVectorsRecorder.Dispose();
                objectMotionVectorsRecorder.Dispose();
                gpuFrameRecorder.Dispose();
                controller.SetExternalDlssRequest(false, 1u);
                controller.SetPreset(AntiAliasingPreset.High, persist: false);
                rig.Dispose();
                QualitySettings.vSyncCount = originalVSync;
                Application.targetFrameRate = originalTargetFrameRate;
            }
        }

        private static PerformanceReport CreateReport(
            AntiAliasingController controller,
            PerformanceSceneRig rig)
        {
            AntiAliasingCapabilities capabilities = controller.Capabilities;
            return new PerformanceReport
            {
                StartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityVersion = Application.unityVersion,
                GraphicsDevice = SystemInfo.graphicsDeviceName,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                HdrpActive = capabilities.HdrpActive,
                MotionVectorsSupported = capabilities.MotionVectorsSupported,
                DlssConfigured = capabilities.DlssConfigured,
                DlssHardwareSupported = capabilities.DlssHardwareSupported,
                DlssAvailable = capabilities.DlssAvailable,
                DynamicResolutionConfigured = capabilities.DynamicResolutionConfigured,
                BaseResolution = $"{rig.MainTarget.width}x{rig.MainTarget.height}",
                WarmupFrames = WarmupFrames,
                SampleFrames = SampleFrames,
                QpcFrequency = Stopwatch.Frequency,
            };
        }

        private static void ConfigureScenarioStart(PerformanceSceneRig rig, ScenarioKind scenario)
        {
            switch (scenario)
            {
                case ScenarioKind.ForestGrassFoliage:
                    rig.MainCamera.transform.SetPositionAndRotation(
                        new Vector3(0f, 2f, -8f),
                        Quaternion.Euler(2f, 0f, 0f));
                    break;
                case ScenarioKind.ThinGeometryAgainstSky:
                    rig.MainCamera.transform.SetPositionAndRotation(
                        new Vector3(0f, 3f, -10f),
                        Quaternion.Euler(-2f, 0f, 0f));
                    break;
                case ScenarioKind.NightHeadlightsEmissive:
                    rig.KeyLight.intensity = 0.04f;
                    rig.MainCamera.backgroundColor = new Color(0.003f, 0.005f, 0.012f, 1f);
                    break;
                case ScenarioKind.InteriorContrastDetails:
                    rig.MainCamera.transform.SetPositionAndRotation(
                        new Vector3(-4f, 1.6f, -4f),
                        Quaternion.Euler(4f, 35f, 0f));
                    break;
                case ScenarioKind.MultipleActiveCameras:
                    rig.AuxiliaryCamera.enabled = true;
                    break;
            }
        }

        private static void UpdateScenario(
            PerformanceSceneRig rig,
            AntiAliasingController controller,
            ScenarioKind scenario,
            AntiAliasingMode measuredMode,
            int frame)
        {
            float phase = frame / (float)Math.Max(1, SampleFrames - 1);
            switch (scenario)
            {
                case ScenarioKind.SlowWalk:
                    rig.MainCamera.transform.position += Vector3.forward * 0.045f;
                    rig.MainCamera.transform.Rotate(Vector3.up, 0.15f, Space.World);
                    break;
                case ScenarioKind.FastCameraTurn:
                    rig.MainCamera.transform.Rotate(Vector3.up, 7f, Space.World);
                    break;
                case ScenarioKind.FastVehicle:
                    rig.MainCamera.transform.position += Vector3.forward * 0.55f;
                    for (int index = 0; index < rig.Wheels.Count; index++)
                    {
                        rig.Wheels[index].Rotate(Vector3.right, 34f, Space.Self);
                    }
                    break;
                case ScenarioKind.ForestGrassFoliage:
                    for (int index = 0; index < rig.Foliage.Count; index++)
                    {
                        float angle = Mathf.Sin((phase * 8f) + index * 0.31f) * 6f;
                        rig.Foliage[index].localRotation = Quaternion.Euler(0f, angle, angle * 0.4f);
                    }
                    rig.MainCamera.transform.Rotate(Vector3.up, 0.7f, Space.World);
                    break;
                case ScenarioKind.ThinGeometryAgainstSky:
                    rig.MainCamera.transform.Rotate(Vector3.up, 1.3f, Space.World);
                    break;
                case ScenarioKind.NightHeadlightsEmissive:
                    rig.EmissiveMover.position = new Vector3(
                        Mathf.Lerp(-5f, 5f, phase),
                        1.2f,
                        3f);
                    rig.MainCamera.transform.Rotate(Vector3.up, 0.55f, Space.World);
                    break;
                case ScenarioKind.InteriorContrastDetails:
                    rig.MainCamera.transform.position += Vector3.right * 0.025f;
                    break;
                case ScenarioKind.FieldOfViewChange:
                    if (frame == SampleFrames / 2)
                    {
                        rig.MainCamera.fieldOfView = 96f;
                    }
                    break;
                case ScenarioKind.ResolutionChange:
                    if (frame == SampleFrames / 2)
                    {
                        rig.SetMainResolution(960, 540);
                    }
                    break;
                case ScenarioKind.RuntimeAaSwitch:
                    if (frame == SampleFrames / 3)
                    {
                        controller.SetMode(
                            measuredMode == AntiAliasingMode.Smaa
                                ? AntiAliasingMode.Taa
                                : AntiAliasingMode.Smaa,
                            persist: false);
                    }
                    else if (frame == (SampleFrames * 2) / 3)
                    {
                        controller.SetMode(measuredMode, persist: false);
                    }
                    break;
                case ScenarioKind.TeleportOrSaveLoad:
                    if (frame == SampleFrames / 2)
                    {
                        rig.MainCamera.transform.position += new Vector3(12f, 0f, 3f);
                    }
                    break;
                case ScenarioKind.HardCameraCut:
                    if (frame == SampleFrames / 2)
                    {
                        rig.MainCamera.transform.rotation *= Quaternion.Euler(0f, 105f, 0f);
                    }
                    break;
                case ScenarioKind.MultipleActiveCameras:
                    rig.AuxiliaryCamera.transform.Rotate(Vector3.up, 1.5f, Space.World);
                    break;
            }
        }

        private static IEnumerator RenderFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }

        private static bool ShouldCaptureVisual(
            ScenarioKind scenario,
            AntiAliasingMode mode)
        {
            bool comparisonMode = mode == AntiAliasingMode.Smaa || mode == AntiAliasingMode.Taa;
            bool visualScenario = scenario == ScenarioKind.ForestGrassFoliage ||
                                  scenario == ScenarioKind.ThinGeometryAgainstSky ||
                                  scenario == ScenarioKind.NightHeadlightsEmissive ||
                                  scenario == ScenarioKind.InteriorContrastDetails;
            return comparisonMode && visualScenario;
        }

        private static string VisualCheckDescription(
            ScenarioKind scenario,
            AntiAliasingMode mode)
        {
            switch (scenario)
            {
                case ScenarioKind.FieldOfViewChange:
                case ScenarioKind.ResolutionChange:
                case ScenarioKind.TeleportOrSaveLoad:
                case ScenarioKind.HardCameraCut:
                    return "Temporal discontinuity path executed; controller state is covered by PlayMode assertions.";
                case ScenarioKind.RuntimeAaSwitch:
                    return "Runtime mode transition executed without scene restart.";
                case ScenarioKind.MultipleActiveCameras:
                    return "Gameplay plus lightweight auxiliary camera rendered concurrently.";
                default:
                    return mode == AntiAliasingMode.Taa || mode == AntiAliasingMode.Smaa
                        ? "Representative comparison capture is produced for selected high-risk scenes."
                        : "Timing coverage only; no subjective artifact verdict is inferred from metrics.";
            }
        }

        private static void CaptureRenderTexture(RenderTexture source, string absolutePath)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            Texture2D image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            image.Apply(false, false);
            File.WriteAllBytes(absolutePath, image.EncodeToPNG());
            RenderTexture.active = previous;
            Object.Destroy(image);
        }

        private static ProfilerRecorder TryStartRecorder(
            ProfilerCategory category,
            string markerName,
            int capacity)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, markerName, capacity);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AA performance recorder '{markerName}' is unavailable: {exception.Message}");
                return default;
            }
        }

        private static void WriteReport(string directory, PerformanceReport report)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "PerformanceMatrix.json"),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            StringBuilder csv = new StringBuilder(32768);
            csv.AppendLine(
                "RequestedMode,EffectiveMode,Scenario,CpuFrameTimeMs,CpuTimingSource," +
                "GpuFrameTimeMs,GpuTimingAvailable,GpuTimingSource,PostProcessingCpuMarkerMs," +
                "MotionVectorsCpuMarkerMs,OutputResolution,InternalResolution," +
                "DynamicResolutionScale,Upscaler,MotionVectorsActive,ManagedCameraCount,FallbackReason");
            for (int index = 0; index < report.Records.Count; index++)
            {
                PerformanceRecord record = report.Records[index];
                csv.Append(Csv(record.RequestedMode)).Append(',')
                    .Append(Csv(record.EffectiveMode)).Append(',')
                    .Append(Csv(record.Scenario)).Append(',')
                    .Append(record.CpuFrameTimeMs.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(record.CpuTimingSource)).Append(',')
                    .Append(record.GpuFrameTimeMs.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.GpuTimingAvailable).Append(',')
                    .Append(Csv(record.GpuTimingSource)).Append(',')
                    .Append(record.PostProcessingCpuMarkerMs.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.MotionVectorsCpuMarkerMs.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(record.OutputResolution)).Append(',')
                    .Append(Csv(record.InternalResolution)).Append(',')
                    .Append(record.DynamicResolutionScale.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(record.Upscaler)).Append(',')
                    .Append(record.MotionVectorsActive).Append(',')
                    .Append(record.ManagedCameraCount).Append(',')
                    .Append(Csv(record.FallbackReason)).AppendLine();
            }

            File.WriteAllText(
                Path.Combine(directory, "PerformanceMatrix.csv"),
                csv.ToString(),
                new UTF8Encoding(false));
        }

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }

        private enum ScenarioKind
        {
            StaticOutdoor,
            SlowWalk,
            FastCameraTurn,
            FastVehicle,
            ForestGrassFoliage,
            ThinGeometryAgainstSky,
            NightHeadlightsEmissive,
            InteriorContrastDetails,
            FieldOfViewChange,
            ResolutionChange,
            RuntimeAaSwitch,
            TeleportOrSaveLoad,
            HardCameraCut,
            MultipleActiveCameras,
        }

        private readonly struct ScenarioDefinition
        {
            public ScenarioDefinition(string name, ScenarioKind kind)
            {
                Name = name;
                Kind = kind;
            }

            public string Name { get; }
            public ScenarioKind Kind { get; }
        }

        [Serializable]
        private sealed class PerformanceReport
        {
            public string StartedUtc;
            public string CompletedUtc;
            public string UnityVersion;
            public string GraphicsDevice;
            public string GraphicsApi;
            public bool HdrpActive;
            public bool MotionVectorsSupported;
            public bool DlssConfigured;
            public bool DlssHardwareSupported;
            public bool DlssAvailable;
            public bool DynamicResolutionConfigured;
            public string BaseResolution;
            public int WarmupFrames;
            public int SampleFrames;
            public long QpcFrequency;
            public List<PerformanceRecord> Records = new List<PerformanceRecord>();
        }

        [Serializable]
        private sealed class PerformanceRecord
        {
            public string RequestedMode;
            public string EffectiveMode;
            public string Scenario;
            public double CpuFrameTimeMs;
            public string CpuTimingSource;
            public double GpuFrameTimeMs;
            public bool GpuTimingAvailable;
            public string GpuTimingSource;
            public double PostProcessingCpuMarkerMs;
            public bool PostProcessingMarkerAvailable;
            public double MotionVectorsCpuMarkerMs;
            public bool MotionVectorsMarkerAvailable;
            public string OutputResolution;
            public string InternalResolution;
            public float DynamicResolutionScale;
            public bool DynamicResolutionActive;
            public string Upscaler;
            public uint UpscalerQuality;
            public bool MotionVectorsActive;
            public int ManagedCameraCount;
            public string FallbackReason;
            public string VisualCheck;
            public string VisualCapture;
            public long CaptureStartQpc;
            public long CaptureEndQpc;
        }

        private sealed class PerformanceSceneRig : IDisposable
        {
            private readonly List<Object> ownedAssets = new List<Object>();
            private RenderTexture auxiliaryTarget;

            private PerformanceSceneRig() { }

            public GameObject Root { get; private set; }
            public Camera MainCamera { get; private set; }
            public Camera AuxiliaryCamera { get; private set; }
            public RenderTexture MainTarget { get; private set; }
            public Light KeyLight { get; private set; }
            public Transform EmissiveMover { get; private set; }
            public List<Transform> Foliage { get; } = new List<Transform>();
            public List<Transform> Wheels { get; } = new List<Transform>();

            public static PerformanceSceneRig Create()
            {
                PerformanceSceneRig rig = new PerformanceSceneRig
                {
                    Root = new GameObject("AA Performance Scene"),
                };
                rig.Build();
                return rig;
            }

            public void ResetScenario()
            {
                MainCamera.transform.SetPositionAndRotation(
                    new Vector3(0f, 2f, -12f),
                    Quaternion.identity);
                MainCamera.fieldOfView = 65f;
                MainCamera.backgroundColor = new Color(0.28f, 0.48f, 0.72f, 1f);
                KeyLight.intensity = 2.5f;
                AuxiliaryCamera.enabled = false;
                SetMainResolution(OutputWidth, OutputHeight);
                EmissiveMover.position = new Vector3(-5f, 1.2f, 3f);
                for (int index = 0; index < Foliage.Count; index++)
                {
                    Foliage[index].localRotation = Quaternion.identity;
                }
            }

            public void SetMainResolution(int width, int height)
            {
                if (MainTarget != null && MainTarget.width == width && MainTarget.height == height)
                {
                    return;
                }

                if (MainTarget != null)
                {
                    MainCamera.targetTexture = null;
                    MainTarget.Release();
                    Object.Destroy(MainTarget);
                }

                MainTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"AA Performance Main {width}x{height}",
                    useMipMap = false,
                };
                MainTarget.Create();
                MainCamera.targetTexture = MainTarget;
            }

            public void Dispose()
            {
                if (MainCamera != null)
                {
                    MainCamera.targetTexture = null;
                }
                if (AuxiliaryCamera != null)
                {
                    AuxiliaryCamera.targetTexture = null;
                }
                if (MainTarget != null)
                {
                    MainTarget.Release();
                    Object.Destroy(MainTarget);
                }
                if (auxiliaryTarget != null)
                {
                    auxiliaryTarget.Release();
                    Object.Destroy(auxiliaryTarget);
                }
                for (int index = 0; index < ownedAssets.Count; index++)
                {
                    if (ownedAssets[index] != null)
                    {
                        Object.Destroy(ownedAssets[index]);
                    }
                }
                if (Root != null)
                {
                    Object.Destroy(Root);
                }
            }

            private void Build()
            {
                Shader litShader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                Material groundMaterial = Own(new Material(litShader));
                SetBaseColor(groundMaterial, new Color(0.16f, 0.24f, 0.11f, 1f));
                Material metalMaterial = Own(new Material(litShader));
                SetBaseColor(metalMaterial, new Color(0.26f, 0.29f, 0.32f, 1f));
                if (metalMaterial.HasProperty("_Metallic"))
                {
                    metalMaterial.SetFloat("_Metallic", 0.75f);
                }
                if (metalMaterial.HasProperty("_Smoothness"))
                {
                    metalMaterial.SetFloat("_Smoothness", 0.72f);
                }

                Material brightMaterial = Own(new Material(litShader));
                SetBaseColor(brightMaterial, new Color(0.7f, 0.12f, 0.035f, 1f));
                if (brightMaterial.HasProperty("_EmissiveColor"))
                {
                    brightMaterial.SetColor("_EmissiveColor", new Color(12f, 2.2f, 0.4f, 1f));
                }

                Material foliageMaterial = CreateFoliageMaterial(litShader);
                ownedAssets.Add(foliageMaterial);

                GameObject ground = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Ground",
                    new Vector3(0f, -0.3f, 8f),
                    new Vector3(30f, 0.5f, 42f),
                    groundMaterial);
                ground.transform.SetParent(Root.transform, true);

                for (int index = 0; index < 48; index++)
                {
                    float x = ((index % 12) - 5.5f) * 1.25f;
                    float z = 2f + (index / 12) * 2.1f;
                    GameObject detail = CreatePrimitive(
                        index % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube,
                        "ContrastDetail_" + index,
                        new Vector3(x, 0.25f + (index % 4) * 0.16f, z),
                        new Vector3(0.16f + (index % 5) * 0.05f, 0.25f, 0.16f),
                        index % 4 == 0 ? brightMaterial : metalMaterial);
                    detail.transform.SetParent(Root.transform, true);
                }

                for (int index = 0; index < 28; index++)
                {
                    GameObject wire = CreatePrimitive(
                        PrimitiveType.Cylinder,
                        "ThinWire_" + index,
                        new Vector3(-8f + index * 0.58f, 2.2f + (index % 3) * 0.28f, 8f),
                        new Vector3(0.018f, 3.5f, 0.018f),
                        metalMaterial);
                    wire.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    wire.transform.SetParent(Root.transform, true);
                }

                for (int index = 0; index < 54; index++)
                {
                    GameObject leaf = CreatePrimitive(
                        PrimitiveType.Quad,
                        "AlphaFoliage_" + index,
                        new Vector3(
                            -8f + (index % 18) * 0.9f,
                            0.65f + (index % 4) * 0.25f,
                            4f + (index / 18) * 2f),
                        new Vector3(0.75f, 1.2f, 1f),
                        foliageMaterial);
                    leaf.transform.SetParent(Root.transform, true);
                    Foliage.Add(leaf.transform);
                }

                for (int index = 0; index < 4; index++)
                {
                    GameObject wheel = CreatePrimitive(
                        PrimitiveType.Cylinder,
                        "MovingWheel_" + index,
                        new Vector3(-2f + index * 1.3f, 0.7f, 0.5f),
                        new Vector3(0.48f, 0.18f, 0.48f),
                        metalMaterial);
                    wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    wheel.transform.SetParent(Root.transform, true);
                    Wheels.Add(wheel.transform);
                }

                GameObject emissive = CreatePrimitive(
                    PrimitiveType.Sphere,
                    "MovingEmissiveHeadlight",
                    new Vector3(-5f, 1.2f, 3f),
                    Vector3.one * 0.45f,
                    brightMaterial);
                emissive.transform.SetParent(Root.transform, true);
                EmissiveMover = emissive.transform;

                GameObject lightOwner = new GameObject("AA Performance Sun", typeof(Light));
                lightOwner.transform.SetParent(Root.transform, false);
                lightOwner.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                KeyLight = lightOwner.GetComponent<Light>();
                KeyLight.type = LightType.Directional;
                KeyLight.intensity = 2.5f;

                // AA is measured without Motion Blur or Depth of Field masking
                // temporal artifacts. This profile belongs only to the generated
                // test rig and never mutates project Volume assets.
                GameObject volumeOwner = new GameObject("AA Performance Neutral Volume", typeof(Volume));
                volumeOwner.transform.SetParent(Root.transform, false);
                Volume volume = volumeOwner.GetComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10000f;
                VolumeProfile neutralProfile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
                MotionBlur motionBlur = neutralProfile.Add<MotionBlur>(true);
                motionBlur.intensity.Override(0f);
                DepthOfField depthOfField = neutralProfile.Add<DepthOfField>(true);
                depthOfField.focusMode.Override(DepthOfFieldMode.Off);
                volume.sharedProfile = neutralProfile;

                GameObject mainCameraOwner = new GameObject("AA Performance Main Camera", typeof(Camera));
                mainCameraOwner.transform.SetParent(Root.transform, false);
                MainCamera = mainCameraOwner.GetComponent<Camera>();
                MainCamera.tag = "MainCamera";
                MainCamera.clearFlags = CameraClearFlags.SolidColor;
                MainCamera.nearClipPlane = 0.1f;
                MainCamera.farClipPlane = 250f;
                mainCameraOwner.AddComponent<AntiAliasingCameraPolicy>().Configure(
                    AntiAliasingCameraRole.Gameplay);
                SetMainResolution(OutputWidth, OutputHeight);

                GameObject auxiliaryOwner = new GameObject("AA Performance Auxiliary Camera", typeof(Camera));
                auxiliaryOwner.transform.SetParent(Root.transform, false);
                AuxiliaryCamera = auxiliaryOwner.GetComponent<Camera>();
                AuxiliaryCamera.transform.SetPositionAndRotation(
                    new Vector3(8f, 4f, -6f),
                    Quaternion.Euler(12f, -25f, 0f));
                auxiliaryTarget = new RenderTexture(640, 360, 16, RenderTextureFormat.ARGB32);
                auxiliaryTarget.Create();
                AuxiliaryCamera.targetTexture = auxiliaryTarget;
                auxiliaryOwner.AddComponent<AntiAliasingCameraPolicy>().Configure(
                    AntiAliasingCameraRole.Auxiliary);
                AuxiliaryCamera.enabled = false;

                ResetScenario();
            }

            private Material CreateFoliageMaterial(Shader shader)
            {
                Texture2D alphaTexture = Own(new Texture2D(16, 16, TextureFormat.RGBA32, true));
                alphaTexture.name = "AA Performance Alpha Foliage";
                for (int y = 0; y < alphaTexture.height; y++)
                {
                    for (int x = 0; x < alphaTexture.width; x++)
                    {
                        float centeredX = (x + 0.5f) / alphaTexture.width * 2f - 1f;
                        float centeredY = (y + 0.5f) / alphaTexture.height * 2f - 1f;
                        bool visible = Mathf.Abs(centeredX) + Mathf.Abs(centeredY * 0.72f) < 0.92f;
                        alphaTexture.SetPixel(x, y, new Color(0.16f, 0.5f, 0.12f, visible ? 1f : 0f));
                    }
                }
                alphaTexture.Apply(updateMipmaps: true, makeNoLongerReadable: false);

                Material material = new Material(shader);
                SetBaseColor(material, Color.white);
                if (material.HasProperty("_BaseColorMap"))
                {
                    material.SetTexture("_BaseColorMap", alphaTexture);
                }
                if (material.HasProperty("_AlphaCutoffEnable"))
                {
                    material.SetFloat("_AlphaCutoffEnable", 1f);
                }
                if (material.HasProperty("_AlphaCutoff"))
                {
                    material.SetFloat("_AlphaCutoff", 0.45f);
                }
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
                return material;
            }

            private T Own<T>(T asset) where T : Object
            {
                ownedAssets.Add(asset);
                return asset;
            }

            private static GameObject CreatePrimitive(
                PrimitiveType type,
                string name,
                Vector3 position,
                Vector3 scale,
                Material material)
            {
                GameObject instance = GameObject.CreatePrimitive(type);
                instance.name = name;
                instance.transform.position = position;
                instance.transform.localScale = scale;
                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                }
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
                return instance;
            }

            private static void SetBaseColor(Material material, Color color)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else
                {
                    material.color = color;
                }
            }
        }
    }
}
