using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSC.Bootstrap;
using MSC.Player;
using MSC.UI.Runtime.Routing;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.Performance
{
    /// <summary>
    /// Local, evidence-producing audit of the real Bootstrap world at the player
    /// house. This is intentionally not a CI gate: Editor timing and GPU counters
    /// depend on the host and must be compared as paired captures on one machine.
    /// </summary>
    public sealed class FullGameHomePerformanceAuditPlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string EvidenceRelativePath =
            "PerformanceCaptures/FullGameAudit/" +
            "HOME_EDITOR_PLAYMODE_BASELINE.json";
        private const int WarmupFrameCount = 90;
        private const int SampleFrameCount = 180;
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        private static readonly Vector3 HomeCameraPosition =
            new Vector3(146.3196f, 2.72054f, -1046.6018f);
        private static readonly Vector3 HomeLookTarget =
            new Vector3(159f, 3.2f, -1030f);

        [Category("LocalPerformanceAudit")]
        [UnityTest]
        public IEnumerator Bootstrap_HomeView_RecordsPairedPerformanceEvidence()
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            int originalVSync = QualitySettings.vSyncCount;
            int originalTargetFrameRate = Application.targetFrameRate;
            RenderTexture target = null;
            Camera playerCamera = null;
            float configuredCameraFarClip = 0f;
            CameraState[] cameraStates = Array.Empty<CameraState>();
            ComponentState<PackedWoodyCellRenderer>[] woodyStates =
                Array.Empty<ComponentState<PackedWoodyCellRenderer>>();
            ComponentState<VegetationWorldRenderer>[] groundVegetationStates =
                Array.Empty<ComponentState<VegetationWorldRenderer>>();

            try
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;

                yield return LoadSingle(BootstrapScenePath);
                ProductionWorldStreamingInstaller installer =
                    Object.FindFirstObjectByType<
                        ProductionWorldStreamingInstaller>(
                        FindObjectsInactive.Include);
                Assert.That(installer, Is.Not.Null);
                yield return PrepareAndActivate(installer);

                DisablePlayerControl(installer.SpawnedPlayer);
                installer.SpawnedPlayer.transform.position =
                    HomeCameraPosition - Vector3.up;
                installer.WorldStreaming.ReportFocusSpeedMetersPerSecond(0f);
                yield return installer.WorldStreaming.RefreshNow();
                yield return WaitForStreaming(installer.WorldStreaming, 120d);
                // The capture compares renderer configurations, not streaming
                // transitions. Freeze the settled radius so an automatic layer
                // refresh cannot replace sampled components between A/B cases.
                installer.WorldStreaming.enabled = false;

                playerCamera = installer.SpawnedPlayer
                    .GetComponentInChildren<Camera>(true);
                Assert.That(playerCamera, Is.Not.Null);
                cameraStates = CaptureAndDisableOtherCameras(playerCamera);
                playerCamera.transform.SetPositionAndRotation(
                    HomeCameraPosition,
                    LookAt(HomeCameraPosition, HomeLookTarget));
                playerCamera.enabled = true;

                target = new RenderTexture(
                    CaptureWidth,
                    CaptureHeight,
                    24,
                    RenderTextureFormat.DefaultHDR)
                {
                    name = "FullGameHomePerformanceAudit_1920x1080"
                };
                Assert.That(target.Create(), Is.True);
                playerCamera.targetTexture = target;

                ProductionUiInstaller uiInstaller =
                    Object.FindFirstObjectByType<ProductionUiInstaller>(
                        FindObjectsInactive.Include);
                if (uiInstaller != null && uiInstaller.UiRoot != null)
                {
                    uiInstaller.UiRoot.ShowReviewScreen(UiRouteId.InGameHud);
                }
                int configuredVSyncCount = QualitySettings.vSyncCount;
                configuredCameraFarClip = playerCamera.farClipPlane;
                // GameUiRoot applies persisted user settings during scene load,
                // after the test's initial override. Reassert the uncapped audit
                // contract only after the real settings path has completed.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;

                PackedWoodyCellRenderer[] woody =
                    Object.FindObjectsByType<PackedWoodyCellRenderer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                VegetationWorldRenderer[] groundVegetation =
                    Object.FindObjectsByType<VegetationWorldRenderer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                woodyStates = CaptureStates(woody);
                groundVegetationStates = CaptureStates(groundVegetation);

                var evidence = new AuditEvidence
                {
                    schemaVersion = 1,
                    capturedUtc = DateTime.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture),
                    unityVersion = Application.unityVersion,
                    executionMode = Application.isEditor
                        ? "EditorPlayMode"
                        : "DevelopmentPlayer",
                    operatingSystem = SystemInfo.operatingSystem,
                    processorType = SystemInfo.processorType,
                    graphicsDeviceName = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    captureWidth = CaptureWidth,
                    captureHeight = CaptureHeight,
                    configuredVSyncCount = configuredVSyncCount,
                    captureVSyncCount = QualitySettings.vSyncCount,
                    configuredCameraFarClipMeters = configuredCameraFarClip,
                    activeWorldProfile = installer.WorldStreaming.Manifest.ProfileId,
                    loadedSceneCount = SceneManager.sceneCount,
                    loadedOwnedSceneCount =
                        installer.WorldStreaming.OwnedLoadedSceneCount,
                    activeRendererCount = Object.FindObjectsByType<Renderer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length,
                    activeColliderCount = Object.FindObjectsByType<Collider>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length,
                    activeLightCount = Object.FindObjectsByType<Light>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length,
                    packedWoodyRendererCount = woody.Length,
                    packedWoodySourceBatchCount = SumWoodyBatches(woody),
                    packedWoodySourceInstanceCount = SumWoodyInstances(woody),
                    groundVegetationRendererCount = groundVegetation.Length,
                    cases = new List<AuditCase>(7)
                };

                AuditCase capture = null;
                RestoreStates(woodyStates);
                RestoreStates(groundVegetationStates);
                yield return CaptureCase(
                    "home-facing-high-full",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeLookTarget),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);

                yield return CaptureCase(
                    "home-away-high-full",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeCameraPosition -
                        (HomeLookTarget - HomeCameraPosition)),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);

                playerCamera.farClipPlane = 500f;
                yield return CaptureCase(
                    "home-facing-high-far500",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeLookTarget),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);
                playerCamera.farClipPlane = configuredCameraFarClip;

                SetEnabled(woodyStates, false);
                RestoreStates(groundVegetationStates);
                yield return CaptureCase(
                    "home-facing-high-no-packed-woody",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeLookTarget),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);

                RestoreStates(woodyStates);
                SetEnabled(groundVegetationStates, false);
                yield return CaptureCase(
                    "home-facing-high-no-ground-vegetation",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeLookTarget),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);

                SetEnabled(woodyStates, false);
                SetEnabled(groundVegetationStates, false);
                yield return CaptureCase(
                    "home-facing-high-no-vegetation",
                    playerCamera,
                    LookAt(HomeCameraPosition, HomeLookTarget),
                    woody,
                    () => capture = null,
                    value => capture = value);
                evidence.cases.Add(capture);

                RestoreStates(woodyStates);
                RestoreStates(groundVegetationStates);
                int balancedQuality = Array.IndexOf(
                    QualitySettings.names,
                    "Balanced");
                if (balancedQuality >= 0)
                {
                    QualitySettings.SetQualityLevel(
                        balancedQuality,
                        applyExpensiveChanges: true);
                    QualitySettings.vSyncCount = 0;
                    yield return CaptureCase(
                        "home-facing-balanced-full",
                        playerCamera,
                        LookAt(HomeCameraPosition, HomeLookTarget),
                        woody,
                        () => capture = null,
                        value => capture = value);
                    evidence.cases.Add(capture);
                }

                string evidencePath = WriteEvidence(evidence);
                Debug.Log(BuildSummary(evidence, evidencePath));
                Assert.That(File.Exists(evidencePath), Is.True);
                Assert.That(evidence.cases.Count, Is.GreaterThanOrEqualTo(5));
            }
            finally
            {
                RestoreStates(woodyStates);
                RestoreStates(groundVegetationStates);
                RestoreCameras(cameraStates);
                if (playerCamera != null)
                {
                    playerCamera.targetTexture = null;
                    if (configuredCameraFarClip > 0f)
                        playerCamera.farClipPlane = configuredCameraFarClip;
                }
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                QualitySettings.SetQualityLevel(
                    originalQuality,
                    applyExpensiveChanges: true);
                QualitySettings.vSyncCount = originalVSync;
                Application.targetFrameRate = originalTargetFrameRate;
            }
        }

        private static IEnumerator CaptureCase(
            string id,
            Camera camera,
            Quaternion rotation,
            IReadOnlyList<PackedWoodyCellRenderer> woody,
            Action begin,
            Action<AuditCase> completed)
        {
            begin();
            camera.transform.SetPositionAndRotation(HomeCameraPosition, rotation);
            yield return WaitFrames(WarmupFrameCount, camera, rotation);

            var frame = new SampleRecorder("Frame", "milliseconds");
            var packedDraws = new SampleRecorder(
                "Packed woody submitted draws",
                "count");
            var packedVisibleBatches = new SampleRecorder(
                "Packed woody visible batches",
                "count");
            using (var main = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "Main Thread",
                       "milliseconds",
                       0.000001d))
            using (var render = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "Render Thread",
                       "milliseconds",
                       0.000001d))
            using (var gpu = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Internal,
                       "GPU Frame Time",
                       "milliseconds",
                       0.000001d))
            using (var gc = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Memory,
                       "GC Allocated In Frame",
                       "bytes",
                       1d))
            using (var draws = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Render,
                       "Draw Calls Count",
                       "count",
                       1d))
            using (var batches = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Render,
                       "Batches Count",
                       "count",
                       1d))
            using (var setPass = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Render,
                       "SetPass Calls Count",
                       "count",
                       1d))
            using (var packedCullAndDraw = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Scripts,
                       "MSC.Vegetation.PackedWoodyCullAndDraw",
                       "milliseconds",
                       0.000001d))
            using (var groundCullAndDraw = ProfilerSampleRecorder.TryCreate(
                       ProfilerCategory.Scripts,
                       "MSC.Vegetation.CullAndDraw",
                       "milliseconds",
                       0.000001d))
            {
                for (int index = 0; index < SampleFrameCount; index++)
                {
                    camera.transform.SetPositionAndRotation(
                        HomeCameraPosition,
                        rotation);
                    double started = Time.realtimeSinceStartupAsDouble;
                    yield return null;
                    frame.Add(
                        (Time.realtimeSinceStartupAsDouble - started) * 1000d);
                    main.Sample();
                    render.Sample();
                    gpu.Sample();
                    gc.Sample();
                    draws.Sample();
                    batches.Sample();
                    setPass.Sample();
                    packedCullAndDraw.Sample();
                    groundCullAndDraw.Sample();
                    packedDraws.Add(SumLastWoodyDraws(woody));
                    packedVisibleBatches.Add(SumLastVisibleWoodyBatches(woody));
                }

                completed(new AuditCase
                {
                    id = id,
                    qualityName = QualitySettings.names[
                        QualitySettings.GetQualityLevel()],
                    frame = frame.Build(),
                    mainThread = main.Build(),
                    renderThread = render.Build(),
                    gpuFrame = gpu.Build(),
                    gcAllocatedInFrame = gc.Build(),
                    drawCalls = draws.Build(),
                    batches = batches.Build(),
                    setPassCalls = setPass.Build(),
                    packedWoodyCullAndDraw = packedCullAndDraw.Build(),
                    groundVegetationCullAndDraw = groundCullAndDraw.Build(),
                    packedWoodySubmittedDraws = packedDraws.Build(),
                    packedWoodyVisibleBatches = packedVisibleBatches.Build(),
                    totalAllocatedMemoryBytes =
                        Profiler.GetTotalAllocatedMemoryLong()
                });
            }
        }

        private static IEnumerator PrepareAndActivate(
            ProductionWorldStreamingInstaller installer)
        {
            if (!installer.IsGameplayPrepared &&
                !installer.IsGameplayPreparationRunning)
            {
                Assert.That(
                    installer.TryBeginGameplayPreparation(out string failure),
                    Is.True,
                    failure);
            }

            double deadline = Time.realtimeSinceStartupAsDouble + 180d;
            while (!installer.IsGameplayPrepared &&
                   Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }
            Assert.That(installer.IsGameplayPrepared, Is.True,
                installer.LastGameplayPreparationFailure);
            Assert.That(
                installer.TryActivateGameplay(out string activationFailure),
                Is.True,
                activationFailure);
            yield return null;
        }

        private static IEnumerator WaitForStreaming(
            ProductionWorldStreamingService streaming,
            double timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (streaming.IsStreaming &&
                   Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }
            Assert.That(streaming.IsStreaming, Is.False,
                "Production world streaming did not settle before capture.");
        }

        private static IEnumerator WaitFrames(
            int count,
            Camera camera,
            Quaternion rotation)
        {
            for (int index = 0; index < count; index++)
            {
                camera.transform.SetPositionAndRotation(
                    HomeCameraPosition,
                    rotation);
                yield return null;
            }
        }

        private static IEnumerator LoadSingle(string scenePath)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                scenePath,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null,
                "Bootstrap scene is missing from Build Settings.");
            yield return load;
            yield return null;
        }

        private static void DisablePlayerControl(GameObject player)
        {
            Assert.That(player, Is.Not.Null);
            FirstPersonMotor motor = player.GetComponent<FirstPersonMotor>();
            PlayerInputRouter input = player.GetComponent<PlayerInputRouter>();
            if (motor != null) motor.enabled = false;
            if (input != null) input.enabled = false;
        }

        private static Quaternion LookAt(Vector3 from, Vector3 target) =>
            Quaternion.LookRotation((target - from).normalized, Vector3.up);

        private static CameraState[] CaptureAndDisableOtherCameras(
            Camera playerCamera)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var states = new CameraState[cameras.Length];
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                states[index] = new CameraState(camera, camera.enabled);
                if (camera != playerCamera)
                {
                    camera.enabled = false;
                }
            }
            return states;
        }

        private static void RestoreCameras(IReadOnlyList<CameraState> states)
        {
            for (int index = 0; index < states.Count; index++)
            {
                if (states[index].Camera != null)
                {
                    states[index].Camera.enabled = states[index].Enabled;
                }
            }
        }

        private static ComponentState<T>[] CaptureStates<T>(T[] components)
            where T : Behaviour
        {
            var states = new ComponentState<T>[components.Length];
            for (int index = 0; index < components.Length; index++)
            {
                states[index] = new ComponentState<T>(
                    components[index],
                    components[index].enabled);
            }
            return states;
        }

        private static void RestoreStates<T>(
            IReadOnlyList<ComponentState<T>> states)
            where T : Behaviour
        {
            for (int index = 0; index < states.Count; index++)
            {
                if (states[index].Component != null)
                {
                    states[index].Component.enabled = states[index].Enabled;
                }
            }
        }

        private static void SetEnabled<T>(
            IReadOnlyList<ComponentState<T>> states,
            bool enabled)
            where T : Behaviour
        {
            for (int index = 0; index < states.Count; index++)
            {
                if (states[index].Component != null && states[index].Enabled)
                {
                    states[index].Component.enabled = enabled;
                }
            }
        }

        private static int SumWoodyBatches(
            IReadOnlyList<PackedWoodyCellRenderer> renderers)
        {
            int total = 0;
            for (int index = 0; index < renderers.Count; index++)
            {
                if (renderers[index] != null)
                    total += renderers[index].SourceBatchCount;
            }
            return total;
        }

        private static int SumWoodyInstances(
            IReadOnlyList<PackedWoodyCellRenderer> renderers)
        {
            int total = 0;
            for (int index = 0; index < renderers.Count; index++)
            {
                if (renderers[index] != null)
                    total += renderers[index].SourceInstanceCount;
            }
            return total;
        }

        private static int SumLastWoodyDraws(
            IReadOnlyList<PackedWoodyCellRenderer> renderers)
        {
            int total = 0;
            for (int index = 0; index < renderers.Count; index++)
            {
                if (renderers[index] != null && renderers[index].enabled)
                    total += renderers[index].LastDrawCallCount;
            }
            return total;
        }

        private static int SumLastVisibleWoodyBatches(
            IReadOnlyList<PackedWoodyCellRenderer> renderers)
        {
            int total = 0;
            for (int index = 0; index < renderers.Count; index++)
            {
                if (renderers[index] != null && renderers[index].enabled)
                    total += renderers[index].LastVisibleBatchCount;
            }
            return total;
        }

        private static string WriteEvidence(AuditEvidence evidence)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            string path = Path.Combine(
                projectRoot,
                EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ?? projectRoot);
            File.WriteAllText(
                path,
                JsonUtility.ToJson(evidence, prettyPrint: true) +
                Environment.NewLine);
            return path;
        }

        private static string BuildSummary(
            AuditEvidence evidence,
            string evidencePath)
        {
            var lines = new List<string>
            {
                "FULL_GAME_HOME_PERFORMANCE_AUDIT " + evidencePath
            };
            for (int index = 0; index < evidence.cases.Count; index++)
            {
                AuditCase item = evidence.cases[index];
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: frame p95={1:F3} ms, main p95={2:F3} ms, " +
                    "GPU p95={3:F3} ms, draws mean={4:F1}, woody draws mean={5:F1}",
                    item.id,
                    item.frame.p95,
                    item.mainThread.p95,
                    item.gpuFrame.p95,
                    item.drawCalls.mean,
                    item.packedWoodySubmittedDraws.mean));
            }
            return string.Join(Environment.NewLine, lines);
        }

        [Serializable]
        private sealed class AuditEvidence
        {
            public int schemaVersion;
            public string capturedUtc;
            public string unityVersion;
            public string executionMode;
            public string operatingSystem;
            public string processorType;
            public string graphicsDeviceName;
            public string graphicsApi;
            public int captureWidth;
            public int captureHeight;
            public int configuredVSyncCount;
            public int captureVSyncCount;
            public float configuredCameraFarClipMeters;
            public string activeWorldProfile;
            public int loadedSceneCount;
            public int loadedOwnedSceneCount;
            public int activeRendererCount;
            public int activeColliderCount;
            public int activeLightCount;
            public int packedWoodyRendererCount;
            public int packedWoodySourceBatchCount;
            public int packedWoodySourceInstanceCount;
            public int groundVegetationRendererCount;
            public List<AuditCase> cases;
        }

        [Serializable]
        private sealed class AuditCase
        {
            public string id;
            public string qualityName;
            public Distribution frame;
            public Distribution mainThread;
            public Distribution renderThread;
            public Distribution gpuFrame;
            public Distribution gcAllocatedInFrame;
            public Distribution drawCalls;
            public Distribution batches;
            public Distribution setPassCalls;
            public Distribution packedWoodyCullAndDraw;
            public Distribution groundVegetationCullAndDraw;
            public Distribution packedWoodySubmittedDraws;
            public Distribution packedWoodyVisibleBatches;
            public long totalAllocatedMemoryBytes;
        }

        [Serializable]
        private sealed class Distribution
        {
            public string marker;
            public string unit;
            public bool available;
            public int sampleCount;
            public double mean;
            public double p50;
            public double p95;
            public double maximum;
            public string reason;
        }

        private sealed class SampleRecorder
        {
            private readonly string marker;
            private readonly string unit;
            private readonly List<double> values =
                new List<double>(SampleFrameCount);

            public SampleRecorder(string marker, string unit)
            {
                this.marker = marker;
                this.unit = unit;
            }

            public void Add(double value)
            {
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                    values.Add(value);
            }

            public Distribution Build()
            {
                if (values.Count == 0)
                {
                    return new Distribution
                    {
                        marker = marker,
                        unit = unit,
                        available = false,
                        reason = "No samples were recorded."
                    };
                }

                values.Sort();
                double total = 0d;
                for (int index = 0; index < values.Count; index++)
                    total += values[index];
                return new Distribution
                {
                    marker = marker,
                    unit = unit,
                    available = true,
                    sampleCount = values.Count,
                    mean = total / values.Count,
                    p50 = Percentile(values, 0.50d),
                    p95 = Percentile(values, 0.95d),
                    maximum = values[values.Count - 1],
                    reason = string.Empty
                };
            }

            private static double Percentile(
                IReadOnlyList<double> sorted,
                double percentile)
            {
                int index = Mathf.Clamp(
                    Mathf.CeilToInt((float)(sorted.Count * percentile)) - 1,
                    0,
                    sorted.Count - 1);
                return sorted[index];
            }
        }

        private sealed class ProfilerSampleRecorder : IDisposable
        {
            private readonly ProfilerRecorder recorder;
            private readonly SampleRecorder samples;
            private readonly double multiplier;

            private ProfilerSampleRecorder(
                ProfilerRecorder recorder,
                string marker,
                string unit,
                double multiplier)
            {
                this.recorder = recorder;
                samples = new SampleRecorder(marker, unit);
                this.multiplier = multiplier;
            }

            public static ProfilerSampleRecorder TryCreate(
                ProfilerCategory category,
                string marker,
                string unit,
                double multiplier)
            {
                try
                {
                    return new ProfilerSampleRecorder(
                        ProfilerRecorder.StartNew(category, marker, 64),
                        marker,
                        unit,
                        multiplier);
                }
                catch (Exception)
                {
                    return new ProfilerSampleRecorder(
                        default,
                        marker,
                        unit,
                        multiplier);
                }
            }

            public void Sample()
            {
                if (recorder.Valid && recorder.Count > 0)
                    samples.Add(recorder.LastValue * multiplier);
            }

            public Distribution Build()
            {
                Distribution distribution = samples.Build();
                if (!recorder.Valid)
                {
                    distribution.available = false;
                    distribution.reason =
                        "ProfilerRecorder marker is unavailable on this host.";
                }
                else if (string.Equals(
                             distribution.unit,
                             "milliseconds",
                             StringComparison.Ordinal) &&
                         distribution.maximum <= 0d)
                {
                    distribution.available = false;
                    distribution.reason =
                        "ProfilerRecorder returned zero for every timing sample.";
                }
                return distribution;
            }

            public void Dispose()
            {
                recorder.Dispose();
            }
        }

        private readonly struct CameraState
        {
            public CameraState(Camera camera, bool enabled)
            {
                Camera = camera;
                Enabled = enabled;
            }

            public Camera Camera { get; }
            public bool Enabled { get; }
        }

        private readonly struct ComponentState<T> where T : Behaviour
        {
            public ComponentState(T component, bool enabled)
            {
                Component = component;
                Enabled = enabled;
            }

            public T Component { get; }
            public bool Enabled { get; }
        }
    }
}
