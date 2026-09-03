#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class VegetationCellActivationBenchmarkPlayModeTests
    {
        private const string SceneFolder = "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Scenes/";
        private const string StreamingBaseScenePath =
            "Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity";
        private const string BenchmarkPreparationGatePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/" +
            "Benchmark/ThreeCellActivationBenchmarkGate.json";
        private const string BenchmarkPreparationProviderType =
            "MSC.Editor.Vegetation." +
            "MapVegetationActivationBenchmarkPreparation, MSC.Editor";
        private const double MaximumMainThreadMilliseconds = 100d;
        private const double MaximumYieldIntervalMilliseconds = 110d;
        private const double MaximumP95YieldIntervalMilliseconds = 33.3d;
        private const string PackedCellAssetBudgetPolicy =
            "Per saved cell, the three category-local Packed*.asset files " +
            "share one 256 KiB base allowance plus 1024 bytes for each " +
            "serialized placement metadata record. The base allowance is " +
            "intentionally applied once per cell, not once per category.";
        private const string PackedPresentationVersion =
            "msc.map-packed-woody.v1";
        private const string VegetationGeneratorId =
            "msc.map-vegetation-rebuild.v1";
        private readonly List<string> ownedScenePaths = new List<string>();
        private readonly List<VegetationWorldRenderer> renderers = new List<VegetationWorldRenderer>();
        private readonly List<PackedWoodyCellRenderer> packedWoodyRenderers =
            new List<PackedWoodyCellRenderer>();
        private readonly List<GraphicsBuffer> capturedBuffers = new List<GraphicsBuffer>();
        private GameObject cameraOwner;
        private RenderTexture target;
        private AsyncOperation inFlight;
        private ProfilerRecorder mainThreadRecorder;
        private ProductionWorldStreamingManifest streamingManifest;
        private ProductionWorldStreamingService streamingService;
        private GameObject streamingServiceOwner;
        private readonly List<NativeRecorder> nativeRecorders = new List<NativeRecorder>();
        private readonly HashSet<string> nativeRecorderKeys = new HashSet<string>();

        [UnityTest, Explicit("Measures real saved vegetation-scene activation and release. Run MapVegetationActivationBenchmarkPreparation.PrepareBenchmarkCells first; requires graphics.")]
        public IEnumerator SavedVegetationScenes_ActivationAndReleaseBenchmark()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsInstancing)
                Assert.Fail("Acceptance benchmark requires real graphics and instancing; -nographics is a failure.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null,
                "Acceptance benchmark requires the configured HDRP asset.");
            Assert.That(SystemInfo.graphicsDeviceName, Is.Not.Empty,
                "Acceptance benchmark requires a named graphics device.");
            if (Camera.allCamerasCount > 0)
                Assert.Fail("An existing enabled camera would contaminate the isolated benchmark; run in the empty PlayMode test scene.");

            string[] paths = {
                SceneFolder + "World_Cell_0_-3_Vegetation.unity",
                SceneFolder + "World_Cell_1_-3_Vegetation.unity",
                SceneFolder + "World_Cell_2_-3_Vegetation.unity"
            };
            foreach (string path in paths)
            {
                if (!File.Exists(Path.Combine(Application.dataPath, "..", path)) || SceneUtility.GetBuildIndexByScenePath(path) < 0)
                    Assert.Fail("Acceptance output is missing or disabled in Build Settings: " + path);
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                    Assert.Fail("The benchmark must not adopt or unload an already loaded scene: " + path);
            }
            BenchmarkPrerequisiteCapture prerequisite =
                CaptureBenchmarkPrerequisite(paths);
            Assert.That(prerequisite.passed, Is.True,
                BenchmarkPreparationGatePath + ": " +
                string.Join("; ", prerequisite.errors));

            var report = new ActivationReport
            {
                schemaVersion = "msc.vegetation-activation.post-packed.v3",
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                cameraPosition = new Vector3(146.3195953f, 2.7205393f, -1046.6018066f),
                cameraEulerAngles = Vector3.zero,
                initialSceneCount = SceneManager.sceneCount,
                backgroundLoadingPriority = Application.backgroundLoadingPriority.ToString(),
                method = "Two passes in the same process without Resources.UnloadUnusedAssets; first-pass cache state uncontrolled, second pass is a warm repeat. Sequential SceneManager.LoadSceneAsync(Additive) for three freshly sealed saved packed-woody vegetation scenes; the hard gate includes every load plus the deferred grass metadata/GPU upload settlement phase. Automatic Graphics.RenderMeshInstanced woody path plus indirect grass path; 256x256 HDRP camera, 30 empty-camera warmup frames. No world-source scenes or geometry mutations.",
                timingNotes = "Observed yield interval is wall time between coroutine samples, including native scene integration, scripts, rendering, waits and Editor overhead; it is not isolated CPU time. Load/unload async wall time includes frame scheduling. Request-call time measures only the synchronous API call. Main Thread recorder is a separate completed-frame counter and may be offset by one frame; zero/no samples means unavailable.",
                limitations = "Direct scene loading does not invoke ProductionWorldStreamingService.OwnedSceneLoaded, so save-registration notification cost is NOT measured. Cache state and shader/texture cold starts are uncontrolled. No source world/gameplay, representative resolution, GPU timing or 60 FPS acceptance. Scene unload verifies GPU buffer disposal, not unloading of shared CPU assets; no Resources.UnloadUnusedAssets scan is requested.",
                packedAssetBudgetPolicy = PackedCellAssetBudgetPolicy,
                prerequisite = prerequisite,
                thresholds = NewPerformanceThresholds(),
                comparisonBaseline = CaptureComparisonBaseline()
            };
            foreach (string path in paths)
                report.savedScenes.Add(CaptureSavedFile(path));
            var measuredLoadPhases = new List<PhaseReport>();
            try { mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 256); }
            catch (Exception exception) { report.mainThreadRecorderFailure = exception.GetType().Name + ": " + exception.Message; }

            CreateCamera(report.cameraPosition);
            var warmup = new PhaseReport { phase = "empty-camera-warmup" };
            report.warmup = warmup;
            yield return ObserveFrames(warmup, 30);
            CompletePhase(warmup);

            for (int passIndex = 0; passIndex < 2; passIndex++)
            {
                var pass = new ActivationPassReport
                {
                    passIndex = passIndex,
                    cacheDescription = passIndex == 0 ? "First pass, cache state uncontrolled" : "Same-process warm repeat; no unused-asset scan between passes"
                };
                report.passes.Add(pass);
                DiscoverNativeMarkers(report.discoveredNativeMarkers,
                    passIndex);
                // Discovery/allocation occurs outside all timed phases.
                for (int frame = 0; frame < 3; frame++) yield return null;
                renderers.Clear(); packedWoodyRenderers.Clear();
                capturedBuffers.Clear(); ownedScenePaths.Clear();
                foreach (string path in paths)
                {
                    var phase = new PhaseReport { passIndex = passIndex, phase = "load", scenePath = path, buildIndex = SceneUtility.GetBuildIndexByScenePath(path) };
                    report.phases.Add(phase);
                    measuredLoadPhases.Add(phase);
                    Debug.Log("[Vegetation activation benchmark] Loading " + path);
                    ResetNativeRecorders();
                    phase.startedFrame = Time.frameCount;
                    long started = Stopwatch.GetTimestamp();
                    inFlight = SceneManager.LoadSceneAsync(phase.buildIndex, LoadSceneMode.Additive);
                    phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                    Assert.That(inFlight, Is.Not.Null, path);
                    ownedScenePaths.Add(path);
                    yield return ObserveOperation(phase, inFlight, started);
                    inFlight = null;
                    // Retain the just-completed activation frame in this phase before the next request.
                    yield return ObserveFrames(phase, 3);
                    Scene scene = SceneManager.GetSceneByPath(path);
                    Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "The exact requested scene must load: " + path);
                    Assert.That(scene.buildIndex, Is.EqualTo(phase.buildIndex));
                    GameObject[] roots = scene.GetRootGameObjects();
                    phase.rootCount = roots.Length;
                    foreach (GameObject root in roots)
                    {
                        VegetationWorldRenderer[] found = root.GetComponentsInChildren<VegetationWorldRenderer>(true);
                        renderers.AddRange(found);
                        phase.rendererCount += found.Length;
                        PackedWoodyCellRenderer[] foundPacked =
                            root.GetComponentsInChildren<PackedWoodyCellRenderer>(true);
                        packedWoodyRenderers.AddRange(foundPacked);
                        phase.packedWoodyRendererCount += foundPacked.Length;
                        foreach (PackedWoodyCellRenderer packed in foundPacked)
                            phase.packedWoodySourceInstanceCount +=
                                packed.SourceInstanceCount;
                    }
                    if (phase.rendererCount != 1)
                        pass.errors.Add(
                            "Expected exactly one saved grass renderer: " +
                            path);
                    if (phase.packedWoodyRendererCount != 3)
                        pass.errors.Add(
                            "Expected one packed renderer for each woody " +
                            "category: " + path);
                    if (phase.packedWoodySourceInstanceCount <= 0)
                        pass.errors.Add(
                            "Packed woody source population is empty: " +
                            path);
                    CompletePhase(phase);
                }

                // Observe the automatic upload path without forcing a synchronous rebuild.
                // Reflection/buffer enumeration happens only after this timed phase.
                var settle = new PhaseReport
                {
                    passIndex = passIndex,
                    phase = "automatic-upload-settle",
                    scenePath = "cell_0_-3..cell_2_-3"
                };
                report.phases.Add(settle);
                measuredLoadPhases.Add(settle);
                ResetNativeRecorders();
                settle.startedFrame = Time.frameCount;
                long settleStarted = Stopwatch.GetTimestamp();
                pass.uploadsSettled = true;
                for (int cellIndex = 0;
                     cellIndex < paths.Length;
                     cellIndex++)
                {
                    cameraOwner.transform.position = report.cameraPosition +
                        Vector3.right * (512f * cellIndex);
                    cameraOwner.transform.rotation = Quaternion.Euler(
                        15f, 180f, 0f);
                    int quietFrames = 0;
                    long previousUploads = -1;
                    for (int frame = 0;
                         frame < 600 && quietFrames < 12;
                         frame++)
                    {
                        yield return ObserveFrames(settle, 1);
                        long uploaded = 0;
                        int targetRendererCount = 0;
                        bool metadataComplete = true;
                        foreach (VegetationWorldRenderer renderer in renderers)
                        {
                            if (renderer == null ||
                                renderer.gameObject.scene.path !=
                                paths[cellIndex])
                                continue;
                            targetRendererCount++;
                            if (renderer.MetadataBuildInProgress)
                                metadataComplete = false;
                            else
                                uploaded += renderer.UploadedInstanceCount;
                        }
                        metadataComplete &= targetRendererCount == 1;
                        quietFrames = metadataComplete &&
                            uploaded == previousUploads
                                ? quietFrames + 1
                                : 0;
                        previousUploads = uploaded;
                    }
                    pass.uploadsSettled &= quietFrames >= 12;
                }
                settle.asyncWallMilliseconds =
                    ElapsedMilliseconds(settleStarted);
                settle.completedFrame = Time.frameCount;
                settle.operationCompletionSampleCount =
                    settle.observedYieldIntervalsMilliseconds.Count;
                yield return ObserveFrames(settle, 3);
                CompletePhase(settle);
                foreach (VegetationWorldRenderer renderer in renderers)
                {
                    GeneratedVegetationGroup group = renderer != null
                        ? renderer.GetComponent<GeneratedVegetationGroup>()
                        : null;
                    var capture = new RendererCapture
                    {
                        scenePath = renderer != null
                            ? renderer.gameObject.scene.path
                            : string.Empty,
                        category = group != null ? group.Category : "missing",
                        fingerprint = group != null ? group.Fingerprint : string.Empty,
                        enabled = renderer != null && renderer.enabled,
                        activeInHierarchy = renderer != null &&
                            renderer.gameObject.activeInHierarchy,
                        metadataBuildComplete = renderer != null &&
                            !renderer.MetadataBuildInProgress,
                        sourceBatchCount = renderer != null
                            ? renderer.SourceBatchCount : 0,
                        readyBatchCount = renderer != null
                            ? renderer.GpuBatchCount : 0,
                        residentGpuBuffers = renderer != null
                            ? renderer.ResidentGpuBufferCount : 0,
                        residentInstances = renderer != null
                            ? renderer.ResidentInstanceCount : 0,
                        uploadedInstances = renderer != null
                            ? renderer.UploadedInstanceCount : 0,
                        cameraRenderCallbackCount = renderer != null
                            ? renderer.CameraRenderCallbackCount : 0,
                        uploadCpuMilliseconds = renderer != null
                            ? renderer.UploadCpuMilliseconds : 0d
                    };
                    capture.intentionallyDeferred =
                        GrassResidencyStateValid(
                            capture.sourceBatchCount,
                            capture.readyBatchCount,
                            capture.residentGpuBuffers,
                            capture.residentInstances,
                            capture.uploadedInstances) &&
                        capture.readyBatchCount == 0;
                    capture.passed = capture.enabled &&
                        capture.activeInHierarchy &&
                        capture.metadataBuildComplete &&
                        capture.category == "GrassCoverage" &&
                        !string.IsNullOrWhiteSpace(capture.fingerprint) &&
                        capture.sourceBatchCount > 0 &&
                        GrassResidencyStateValid(
                            capture.sourceBatchCount,
                            capture.readyBatchCount,
                            capture.residentGpuBuffers,
                            capture.residentInstances,
                            capture.uploadedInstances) &&
                        capture.cameraRenderCallbackCount > 0;
                    pass.renderers.Add(capture);
                    pass.loadedRendererCount++;
                    pass.residentInstanceCountBeforeUnload += renderer.ResidentInstanceCount;
                    pass.residentGpuBufferCountBeforeUnload += renderer.ResidentGpuBufferCount;
                    pass.uploadedInstanceCount += renderer.UploadedInstanceCount;
                    pass.readyBatchCountBeforeUnload += renderer.GpuBatchCount;
                    pass.cameraRenderCallbackCount += renderer.CameraRenderCallbackCount;
                    pass.uploadCpuMilliseconds += renderer.UploadCpuMilliseconds;
                    pass.maximumUploadStepCpuMilliseconds = Math.Max(pass.maximumUploadStepCpuMilliseconds, renderer.MaxUploadCpuMilliseconds);
                    CaptureBuffers(renderer);
                }
                foreach (PackedWoodyCellRenderer renderer in packedWoodyRenderers)
                {
                    GeneratedVegetationGroup group = renderer != null
                        ? renderer.GetComponent<GeneratedVegetationGroup>()
                        : null;
                    PackedWoodyCellAsset asset = renderer != null
                        ? renderer.CellAsset
                        : null;
                    var capture = new PackedRendererCapture
                    {
                        scenePath = renderer != null
                            ? renderer.gameObject.scene.path
                            : string.Empty,
                        category = asset != null
                            ? asset.Category.ToString()
                            : "missing",
                        groupCategory = group != null
                            ? group.Category
                            : "missing",
                        cellId = asset != null ? asset.CellId : string.Empty,
                        groupFingerprint = group != null
                            ? group.Fingerprint
                            : string.Empty,
                        assetFingerprint = asset != null
                            ? asset.PlanFingerprint
                            : string.Empty,
                        generatorId = asset != null
                            ? asset.GeneratorId
                            : string.Empty,
                        presentationVersion = asset != null
                            ? asset.PresentationVersion
                            : string.Empty,
                        enabled = renderer != null && renderer.enabled,
                        activeInHierarchy = renderer != null &&
                            renderer.gameObject.activeInHierarchy,
                        sourceBatchCount = renderer != null
                            ? renderer.SourceBatchCount : 0,
                        sourceInstanceCount = renderer != null
                            ? renderer.SourceInstanceCount : 0,
                        placementMetadataCount = asset != null
                            ? asset.InstanceCount : 0,
                        cameraRenderCallbackCount =
                            renderer != null
                                ? renderer.CameraRenderCallbackCount : 0,
                        visibleBatchCount = renderer != null
                            ? renderer.LastVisibleBatchCount : 0,
                        drawCallCount = renderer != null
                            ? renderer.LastDrawCallCount : 0
                    };
                    if (asset != null)
                    {
                        capture.configurationErrors.AddRange(
                            asset.ValidateConfiguration());
                        capture.assetPath = AssetDatabase.GetAssetPath(asset);
                        if (!string.IsNullOrWhiteSpace(capture.assetPath) &&
                            File.Exists(ProjectPath(capture.assetPath)))
                        {
                            capture.assetBytes = new FileInfo(
                                ProjectPath(capture.assetPath)).Length;
                            capture.assetSha256 = HashScene(capture.assetPath);
                        }
                    }
                    capture.emptyCategory = PackedPopulationStateValid(
                        capture.sourceBatchCount,
                        capture.sourceInstanceCount,
                        capture.placementMetadataCount) &&
                        capture.sourceInstanceCount == 0;
                    capture.passed = capture.enabled &&
                        capture.activeInHierarchy && asset != null &&
                        capture.configurationErrors.Count == 0 &&
                        PackedPopulationStateValid(
                            capture.sourceBatchCount,
                            capture.sourceInstanceCount,
                            capture.placementMetadataCount) &&
                        capture.cameraRenderCallbackCount > 0 &&
                        capture.groupFingerprint == capture.assetFingerprint &&
                        !string.IsNullOrWhiteSpace(capture.assetFingerprint) &&
                        capture.generatorId == VegetationGeneratorId &&
                        capture.presentationVersion == PackedPresentationVersion &&
                        capture.assetBytes > 0 &&
                        CategoryMatches(capture.groupCategory, asset.Category);
                    pass.packedWoodyRenderers.Add(capture);
                    pass.loadedPackedWoodyRendererCount++;
                    pass.packedWoodyBatchCount += renderer.SourceBatchCount;
                    pass.packedWoodyInstanceCount += renderer.SourceInstanceCount;
                    pass.packedWoodyCameraRenderCallbackCount +=
                        renderer.CameraRenderCallbackCount;
                    pass.packedWoodyVisibleBatchCount +=
                        renderer.LastVisibleBatchCount;
                    pass.packedWoodyDrawCallCount +=
                        renderer.LastDrawCallCount;
                }
                foreach (string path in paths)
                {
                    SavedSceneAcceptanceCapture saved =
                        InspectSavedScene(path);
                    ApplyPreparedCellExpectation(saved, prerequisite);
                    pass.savedScenes.Add(saved);
                }
                pass.capturedGpuBufferCount = capturedBuffers.Count;

                for (int index = paths.Length - 1; index >= 0; index--)
                {
                    string path = paths[index];
                    var phase = new PhaseReport { passIndex = passIndex, phase = "unload", scenePath = path };
                    report.phases.Add(phase);
                    Debug.Log("[Vegetation activation benchmark] Unloading " + path);
                    ResetNativeRecorders();
                    phase.startedFrame = Time.frameCount;
                    long started = Stopwatch.GetTimestamp();
                    inFlight = SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(path));
                    phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                    Assert.That(inFlight, Is.Not.Null, path);
                    yield return ObserveOperation(phase, inFlight, started);
                    inFlight = null;
                    yield return ObserveFrames(phase, 3);
                    CompletePhase(phase);
                }
                var release = new PhaseReport { passIndex = passIndex, phase = "post-unload-release" };
                report.phases.Add(release);
                ResetNativeRecorders();
                yield return ObserveFrames(release, 3);
                CompletePhase(release);

                foreach (string path in paths)
                {
                    Scene scene = SceneManager.GetSceneByPath(path);
                    if (scene.IsValid() && scene.isLoaded) pass.remainingLoadedSceneCount++;
                }
                foreach (VegetationWorldRenderer renderer in renderers)
                    if (renderer != null) pass.remainingRendererCount++;
                foreach (PackedWoodyCellRenderer renderer in packedWoodyRenderers)
                    if (renderer != null)
                        pass.remainingPackedWoodyRendererCount++;
                foreach (GraphicsBuffer buffer in capturedBuffers)
                    if (buffer.IsValid()) pass.remainingValidGpuBufferCount++;

                pass.functionalPassed = pass.errors.Count == 0 &&
                    pass.uploadsSettled &&
                    pass.loadedRendererCount == paths.Length &&
                    AllRendererCapturesPassed(pass.renderers) &&
                    pass.cameraRenderCallbackCount > 0 && pass.residentInstanceCountBeforeUnload > 0 &&
                    pass.capturedGpuBufferCount == pass.residentGpuBufferCountBeforeUnload && pass.capturedGpuBufferCount > 0 &&
                    pass.loadedPackedWoodyRendererCount == paths.Length * 3 &&
                    AllPackedRendererCapturesPassed(
                        pass.packedWoodyRenderers) &&
                    AllSavedSceneCapturesPassed(pass.savedScenes) &&
                    pass.packedWoodyInstanceCount > 0 &&
                    pass.packedWoodyCameraRenderCallbackCount > 0 &&
                    pass.packedWoodyDrawCallCount > 0 &&
                    pass.remainingLoadedSceneCount == 0 && pass.remainingRendererCount == 0 &&
                    pass.remainingPackedWoodyRendererCount == 0 &&
                    pass.remainingValidGpuBufferCount == 0;
                pass.passed = pass.functionalPassed;
                if (!pass.passed) break;
            }

            report.performance = EvaluatePerformance(
                measuredLoadPhases, (paths.Length + 1) * 2);
            report.comparison = CompareWithBaseline(
                report.comparisonBaseline,
                report.performance.maximumMainThreadMilliseconds);
            report.observedFrameSampleCount =
                report.performance.observedYieldSampleCount;
            report.maximumObservedYieldIntervalMilliseconds =
                report.performance.maximumObservedYieldIntervalMilliseconds;
            report.p95ObservedYieldIntervalMilliseconds =
                report.performance.p95ObservedYieldIntervalMilliseconds;
            report.functionalPassed = report.passes.Count == 2 &&
                report.passes[0].functionalPassed &&
                report.passes[1].functionalPassed;
            report.measurementValid = report.performance.measurementValid;
            report.performancePassed = report.performance.performancePassed;
            report.passed = report.functionalPassed &&
                report.measurementValid && report.performancePassed;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "VegetationRebuild", "Performance", "cell-activation-benchmark.post-packed.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[Vegetation activation benchmark] Wrote " + output);
            Assert.That(report.passed, Is.True, output);
        }

        [UnityTest, Explicit("Measures the three saved vegetation cells through ProductionWorldStreamingService automatic low-priority activation. Run MapVegetationActivationBenchmarkPreparation.PrepareBenchmarkCells first; requires graphics.")]
        public IEnumerator SavedVegetationScenes_ProductionStreamingRouteBenchmark()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                !SystemInfo.supportsInstancing)
                Assert.Fail("Acceptance benchmark requires real graphics and instancing; -nographics is a failure.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null,
                "Acceptance benchmark requires the configured HDRP asset.");
            Assert.That(SystemInfo.graphicsDeviceName, Is.Not.Empty,
                "Acceptance benchmark requires a named graphics device.");
            if (Camera.allCamerasCount > 0)
                Assert.Fail("An existing enabled camera would contaminate the isolated benchmark; run in the empty PlayMode test scene.");

            string[] paths =
            {
                SceneFolder + "World_Cell_0_-3_Vegetation.unity",
                SceneFolder + "World_Cell_1_-3_Vegetation.unity",
                SceneFolder + "World_Cell_2_-3_Vegetation.unity"
            };
            var cells = new[]
            {
                new WorldCellIndex(0, -3),
                new WorldCellIndex(1, -3),
                new WorldCellIndex(2, -3)
            };
            foreach (string path in paths)
                AssertSavedAcceptanceOutput(path);
            AssertSavedAcceptanceOutput(StreamingBaseScenePath);
            BenchmarkPrerequisiteCapture prerequisite =
                CaptureBenchmarkPrerequisite(paths);
            Assert.That(prerequisite.passed, Is.True,
                BenchmarkPreparationGatePath + ": " +
                string.Join("; ", prerequisite.errors));

            var report = new ProductionServiceActivationReport
            {
                schemaVersion =
                    "msc.vegetation-activation.production-service.v2",
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                backgroundLoadingPriority =
                    Application.backgroundLoadingPriority.ToString(),
                presentationAsyncOperationPriority =
                    ProductionWorldStreamingService
                        .PresentationLayerAsyncOperationPriority,
                method = "One automatic ProductionWorldStreamingService refresh per camera/focus crossing. A radius-zero PrototypeFixture manifest contains one never-loaded far base cell and the three freshly sealed saved vegetation scenes as presentation layers. The service owns low-priority preparation, its deliberate activation gate, OwnedSceneLoaded notification and stale-layer unload. The hard gate includes each automatic crossing plus its complete deferred grass metadata/GPU upload settlement and three recorder-drain frames; only structural inspection remains outside timing.",
                timingNotes = "The Main Thread ProfilerRecorder and wall-clock yield intervals use the same method and hard Editor thresholds as the direct post-packed benchmark. Async wall time is diagnostic only.",
                limitations = "This isolated route measures the real production service but not the complete donor world, gameplay/save subscribers, representative display resolution, GPU frame time or a player build. Final Resources.UnloadUnusedAssets cleanup is verified after timing and excluded from activation gates.",
                packedAssetBudgetPolicy = PackedCellAssetBudgetPolicy,
                prerequisite = prerequisite,
                thresholds = NewPerformanceThresholds(),
                cameraStartPosition = new Vector3(
                    146.3195953f, 2.7205393f, -1046.6018066f)
            };
            foreach (string path in paths)
                report.savedScenes.Add(CaptureSavedFile(path));

            string output = ProjectPath(
                "Artifacts/VegetationRebuild/Performance/" +
                "cell-activation-benchmark.production-service.json");
            try
            {
                try
                {
                    mainThreadRecorder = ProfilerRecorder.StartNew(
                        ProfilerCategory.Internal, "Main Thread", 256);
                }
                catch (Exception exception)
                {
                    report.mainThreadRecorderFailure =
                        exception.GetType().Name + ": " + exception.Message;
                }

                CreateCamera(report.cameraStartPosition);
                var warmup = new PhaseReport
                {
                    phase = "empty-camera-warmup"
                };
                report.warmup = warmup;
                yield return ObserveFrames(warmup, 30);
                CompletePhase(warmup);
                DiscoverNativeMarkers(
                    report.discoveredNativeMarkers, 0);
                for (int frame = 0; frame < 3; frame++) yield return null;

                streamingManifest = ScriptableObject.CreateInstance<
                    ProductionWorldStreamingManifest>();
                streamingManifest.ConfigureForAuthoring(
                    512f,
                    0,
                    0,
                    new[]
                    {
                        new ProductionWorldCellScene(
                            "cell_99_99",
                            99,
                            99,
                            SceneUtility.GetBuildIndexByScenePath(
                                StreamingBaseScenePath),
                            StreamingBaseScenePath)
                    });
                var layers = new ProductionWorldCellLayerScene[paths.Length];
                for (int index = 0; index < paths.Length; index++)
                {
                    layers[index] = new ProductionWorldCellLayerScene(
                        "vegetation",
                        cells[index],
                        SceneUtility.GetBuildIndexByScenePath(paths[index]),
                        paths[index],
                        0,
                        0);
                    ownedScenePaths.Add(paths[index]);
                }
                streamingManifest.ConfigureCellLayersForAuthoring(layers);
                report.manifestErrors.AddRange(
                    streamingManifest.ValidateConfiguration());

                streamingServiceOwner = new GameObject(
                    "Vegetation production streaming benchmark service");
                streamingService = streamingServiceOwner.AddComponent<
                    ProductionWorldStreamingService>();
                streamingService.enabled = false;
                streamingService.ConfigureForAuthoring(streamingManifest);
                streamingService.BindFocus(cameraOwner.transform);

                streamingService.OwnedSceneLoaded += scene =>
                {
                    if (Array.IndexOf(paths, scene.path) < 0) return;
                    report.loadedOrder.Add(scene.path);
                    report.loadedFrames.Add(Time.frameCount);
                };
                streamingService.OwnedSceneWillUnload += scene =>
                {
                    if (Array.IndexOf(paths, scene.path) < 0) return;
                    report.unloadOrder.Add(scene.path);
                    report.unloadFrames.Add(Time.frameCount);
                };

                var measuredPhases = new List<PhaseReport>(
                    paths.Length * 2);
                for (int index = 0; index < paths.Length; index++)
                {
                    var step = new ProductionServiceStepReport
                    {
                        index = index,
                        cellId = cells[index].Id,
                        scenePath = paths[index]
                    };
                    report.steps.Add(step);
                    var phase = new PhaseReport
                    {
                        phase = "automatic-service-crossing",
                        passIndex = index,
                        scenePath = paths[index],
                        buildIndex = SceneUtility.GetBuildIndexByScenePath(
                            paths[index]),
                        startedFrame = Time.frameCount
                    };
                    step.timing = phase;
                    report.phases.Add(phase);
                    measuredPhases.Add(phase);

                    int completedLoadsBefore =
                        streamingService.CompletedSceneLoadCount;
                    int completedRefreshesBefore =
                        streamingService.CompletedAutomaticRefreshCount;
                    int cleanupCountBefore =
                        streamingService.CompletedUnusedAssetCleanupCount;
                    cameraOwner.transform.position =
                        report.cameraStartPosition + Vector3.right *
                        (512f * index);
                    cameraOwner.transform.rotation = Quaternion.identity;
                    ResetNativeRecorders();
                    long started = Stopwatch.GetTimestamp();
                    if (index == 0) streamingService.enabled = true;

                    while ((report.loadedOrder.Count <= index ||
                            streamingService.IsStreaming) &&
                           ElapsedMilliseconds(started) < 180000d)
                        yield return ObserveFrames(phase, 1);

                    phase.asyncWallMilliseconds =
                        ElapsedMilliseconds(started);
                    phase.completedFrame = Time.frameCount;
                    phase.operationCompletionSampleCount =
                        phase.observedYieldIntervalsMilliseconds.Count;
                    yield return ObserveFrames(phase, 3);
                    CompletePhase(phase);

                    step.completedSceneLoadDelta =
                        streamingService.CompletedSceneLoadCount -
                        completedLoadsBefore;
                    step.completedAutomaticRefreshDelta =
                        streamingService.CompletedAutomaticRefreshCount -
                        completedRefreshesBefore;
                    step.startedLayerLoadCount =
                        streamingService.LastRefreshStartedLayerLoadCount;
                    step.loadStartedFrame =
                        streamingService.LastSceneLoadStartedFrame;
                    step.activationPreparedFrame =
                        streamingService.LastSceneActivationPreparedFrame;
                    step.loadCompletedFrame =
                        streamingService.LastSceneLoadCompletedFrame;
                    step.loadWallMilliseconds =
                        streamingService.LastSceneLoadElapsedMilliseconds;
                    step.notificationCpuMilliseconds =
                        streamingService.LastSceneNotificationCpuMilliseconds;
                    step.unusedAssetCleanupCountDelta =
                        streamingService.CompletedUnusedAssetCleanupCount -
                        cleanupCountBefore;
                    step.ownedLoadedSceneCount =
                        streamingService.OwnedLoadedSceneCount;
                    step.loadedOrderExactSoFar =
                        SequencePrefixEquals(
                            report.loadedOrder, paths, index + 1);
                    step.currentLayerLoaded =
                        streamingService.IsCellLayerLoaded(
                            cells[index].Id, "vegetation");
                    step.previousLayersUnloaded = true;
                    for (int previous = 0; previous < index; previous++)
                    {
                        if (SceneLoaded(paths[previous]))
                            step.previousLayersUnloaded = false;
                    }

                    if (report.loadedOrder.Count <= index)
                    {
                        step.errors.Add(
                            "Automatic service refresh timed out before the " +
                            "expected layer loaded.");
                    }
                    Scene loadedScene = SceneManager.GetSceneByPath(
                        paths[index]);
                    if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                    {
                        step.errors.Add(
                            "Expected exact saved layer is not loaded.");
                    }
                    else
                    {
                        cameraOwner.transform.rotation = Quaternion.Euler(
                            15f, 180f, 0f);
                        var settlement = new OutputSettlementReport();
                        step.outputSettlement = settlement;
                        var settlementPhase = new PhaseReport
                        {
                            phase = "automatic-output-settle",
                            passIndex = index,
                            scenePath = paths[index],
                            buildIndex = SceneUtility
                                .GetBuildIndexByScenePath(paths[index]),
                            startedFrame = Time.frameCount
                        };
                        step.settlementTiming = settlementPhase;
                        report.phases.Add(settlementPhase);
                        measuredPhases.Add(settlementPhase);
                        ResetNativeRecorders();
                        long settlementStarted = Stopwatch.GetTimestamp();
                        yield return WaitForSceneOutputs(
                            loadedScene, settlement, settlementPhase);
                        settlementPhase.asyncWallMilliseconds =
                            ElapsedMilliseconds(settlementStarted);
                        settlementPhase.completedFrame = Time.frameCount;
                        settlementPhase.operationCompletionSampleCount =
                            settlementPhase
                                .observedYieldIntervalsMilliseconds.Count;
                        yield return ObserveFrames(settlementPhase, 3);
                        CompletePhase(settlementPhase);
                        step.savedScene = InspectSavedScene(paths[index]);
                        ApplyPreparedCellExpectation(
                            step.savedScene, prerequisite);
                        foreach (GameObject root in
                                 loadedScene.GetRootGameObjects())
                        {
                            foreach (VegetationWorldRenderer renderer in
                                     root.GetComponentsInChildren<
                                         VegetationWorldRenderer>(true))
                            {
                                renderers.Add(renderer);
                                CaptureBuffers(renderer);
                            }
                            packedWoodyRenderers.AddRange(
                                root.GetComponentsInChildren<
                                    PackedWoodyCellRenderer>(true));
                        }
                    }

                    step.functionalPassed = step.errors.Count == 0 &&
                        step.completedSceneLoadDelta == 1 &&
                        step.completedAutomaticRefreshDelta == 1 &&
                        step.startedLayerLoadCount == 1 &&
                        streamingService.MaximumAutomaticRefreshLayerLoadCount <=
                        ProductionWorldStreamingService
                            .MaximumAutomaticLayerLoadsPerRefresh &&
                        step.loadStartedFrame >= phase.startedFrame &&
                        step.activationPreparedFrame > step.loadStartedFrame &&
                        step.loadCompletedFrame >=
                        step.activationPreparedFrame &&
                        step.unusedAssetCleanupCountDelta == 0 &&
                        step.ownedLoadedSceneCount == 1 &&
                        step.loadedOrderExactSoFar &&
                        step.currentLayerLoaded &&
                        step.previousLayersUnloaded &&
                        step.outputSettlement != null &&
                        step.outputSettlement.settled &&
                        step.savedScene != null && step.savedScene.passed;
                    if (!step.functionalPassed) break;
                }

                streamingService.enabled = false;
                report.performance = EvaluatePerformance(
                    measuredPhases, paths.Length * 2);
                report.functionalPassed = report.manifestErrors.Count == 0 &&
                    report.presentationAsyncOperationPriority == -1 &&
                    report.steps.Count == paths.Length &&
                    AllProductionStepsPassed(report.steps) &&
                    SequenceEquals(report.loadedOrder, paths) &&
                    streamingService.MaximumAutomaticRefreshLayerLoadCount <=
                    ProductionWorldStreamingService
                        .MaximumAutomaticLayerLoadsPerRefresh &&
                    streamingService.CompletedUnusedAssetCleanupCount == 0;
                report.measurementValid =
                    report.performance.measurementValid;
                report.performancePassed =
                    report.performance.performancePassed;

                yield return streamingService.UnloadOwnedScenes();
                yield return ObserveFrames(
                    new PhaseReport { phase = "post-service-cleanup" }, 3);
                report.cleanup.completedUnusedAssetCleanupCount =
                    streamingService.CompletedUnusedAssetCleanupCount;
                report.cleanup.pendingUnusedAssetCleanup =
                    streamingService.HasPendingUnusedAssetCleanup;
                report.cleanup.ownedLoadedSceneCount =
                    streamingService.OwnedLoadedSceneCount;
                foreach (string path in paths)
                    if (SceneLoaded(path))
                        report.cleanup.remainingLoadedSceneCount++;
                foreach (VegetationWorldRenderer renderer in renderers)
                    if (renderer != null)
                        report.cleanup.remainingRendererCount++;
                foreach (PackedWoodyCellRenderer renderer in
                         packedWoodyRenderers)
                    if (renderer != null)
                        report.cleanup.remainingPackedRendererCount++;
                foreach (GraphicsBuffer buffer in capturedBuffers)
                    if (buffer != null && buffer.IsValid())
                        report.cleanup.remainingValidGpuBufferCount++;
                report.cleanup.unloadOrderExact =
                    SequenceEquals(report.unloadOrder, paths);
                report.cleanup.passed =
                    report.cleanup.completedUnusedAssetCleanupCount == 1 &&
                    !report.cleanup.pendingUnusedAssetCleanup &&
                    report.cleanup.ownedLoadedSceneCount == 0 &&
                    report.cleanup.remainingLoadedSceneCount == 0 &&
                    report.cleanup.remainingRendererCount == 0 &&
                    report.cleanup.remainingPackedRendererCount == 0 &&
                    report.cleanup.remainingValidGpuBufferCount == 0 &&
                    report.cleanup.unloadOrderExact;
                report.passed = report.functionalPassed &&
                    report.measurementValid && report.performancePassed &&
                    report.cleanup.passed;
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log(
                    "[Vegetation production streaming benchmark] Wrote " +
                    output);
            }
            Assert.That(report.passed, Is.True, output);
        }

        [UnityTest, Explicit("Compares exact saved woody population with diagnostic inactive copies, then measures bounded individual activation. Run MapVegetationActivationExperiment.PrepareBatch first.")]
        [PrebuildSetup(typeof(WoodyExperimentBuildSetup)), PostBuildCleanup(typeof(WoodyExperimentBuildSetup))]
        public IEnumerator SavedWoodyPopulation_ActiveVersusInactiveAndBudgetedActivationBenchmark()
        {
            const string fixturePath = "Artifacts/VegetationRebuild/Performance/woody-activation-experiment-fixture.json";
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsInstancing)
                Assert.Ignore("This benchmark requires graphics; do not use -nographics.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null);
            if (Camera.allCamerasCount > 0) Assert.Ignore("An existing camera would contaminate this isolated benchmark.");
            string fixtureFile = ProjectPath(fixturePath);
            if (!File.Exists(fixtureFile)) Assert.Ignore("Prepare the explicit inactive-scene experiment first.");
            WoodyFixture fixture = JsonUtility.FromJson<WoodyFixture>(File.ReadAllText(fixtureFile));
            Assert.That(fixture.version, Is.EqualTo("msc.woody-activation-experiment.v1"));
            Assert.That(fixture.passed && fixture.sourceUnchanged, Is.True);
            string prefix = "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/";
            Assert.That(fixture.sourceScene, Is.EqualTo(SceneFolder + "World_Cell_1_-3_Vegetation.unity"));
            Assert.That(fixture.categoryScene, Is.EqualTo(prefix + "Cell_1_-3_CategoryInactive.unity"));
            Assert.That(fixture.individualScene, Is.EqualTo(prefix + "Cell_1_-3_IndividualInactive.unity"));
            string[] paths = { fixture.sourceScene, fixture.categoryScene, fixture.individualScene };
            string[] hashes = { fixture.sourceSha256, fixture.categorySha256, fixture.individualSha256 };
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.That(HashScene(paths[i]), Is.EqualTo(hashes[i]), "The diagnostic population changed after preparation: " + paths[i]);
                if (SceneUtility.GetBuildIndexByScenePath(paths[i]) < 0) Assert.Ignore("Prepare enabled build entries before the test runner snapshots them: " + paths[i]);
                if (SceneManager.GetSceneByPath(paths[i]).isLoaded) Assert.Ignore("Will not adopt/unload an already loaded scene: " + paths[i]);
            }

            var report = new WoodyExperimentReport
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                fixture = fixture, maximumPrefabsPerFrame = 64, softCpuBudgetMilliseconds = 2,
                cameraPosition = new Vector3(146.3195953f, 2.7205393f, -1046.6018066f),
                method = "Prime all three exact-population scene variants, unloading without Resources.UnloadUnusedAssets; then warm-load active, category-inactive and individual-prefab-inactive variants sequentially. Only the last variant is activated, one direct prefab at a time, at most 64 prefabs and a soft 2ms synchronous CPU budget per frame. Hierarchy, transforms, IDs, mesh/material references and grass are unchanged.",
                limitations = "Diagnostic Editor PlayMode experiment, not production scheduling or 60 FPS acceptance. Shared assets are primed but OS/driver caches remain uncontrolled. The fixed home camera may allocate no GPU grass for cell_1_-3; zero buffers is valid. SetActive CPU is synchronous call time; subsequent native/render/physics work appears separately in frame intervals and native markers. A single prefab activation may exceed the soft time budget. No source geometry or scene files are saved by this test."
            };
            try { mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 256); }
            catch (Exception exception) { report.native.mainThreadRecorderFailure = exception.GetType().Name + ": " + exception.Message; }
            CreateCamera(report.cameraPosition);
            yield return ObserveFrames(new PhaseReport(), 30);

            for (int passIndex = 0; passIndex < 2; passIndex++)
            for (int variant = 0; variant < paths.Length; variant++)
            {
                string path = paths[variant];
                var result = new WoodyCaseReport { priming = passIndex == 0, variant = variant == 0 ? "active" : variant == 1 ? "category-inactive" : "individual-inactive", scenePath = path };
                report.cases.Add(result);
                DiscoverNativeMarkers(
                    report.native.discoveredNativeMarkers, passIndex);
                for (int frame = 0; frame < 3; frame++) yield return null;
                renderers.Clear(); capturedBuffers.Clear();
                var load = new PhaseReport { passIndex = passIndex, phase = "load-" + result.variant, scenePath = path,
                    buildIndex = SceneUtility.GetBuildIndexByScenePath(path), startedFrame = Time.frameCount };
                result.load = load;
                ResetNativeRecorders();
                long started = Stopwatch.GetTimestamp();
                inFlight = SceneManager.LoadSceneAsync(load.buildIndex, LoadSceneMode.Additive);
                load.requestCallMilliseconds = ElapsedMilliseconds(started);
                Assert.That(inFlight, Is.Not.Null, path);
                ownedScenePaths.Add(path);
                yield return ObserveOperation(load, inFlight, started);
                inFlight = null;
                yield return ObserveFrames(load, 3);
                CompletePhase(load);
                Scene scene = SceneManager.GetSceneByPath(path);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True, path);
                Assert.That(scene.buildIndex, Is.EqualTo(load.buildIndex));
                List<GameObject> prefabs = CollectWoodyPopulation(scene, result.variant, renderers);
                Assert.That(prefabs.Count, Is.EqualTo(fixture.woodyPrefabCount), "No saved tree/shrub may disappear in the experiment.");
                result.before = InspectNativePopulation(scene);
                Assert.That(result.before.gameObjects, Is.EqualTo(fixture.gameObjectCount));
                Assert.That(renderers.Count, Is.EqualTo(1), "Grass renderer must remain unchanged and active.");
                Assert.That(renderers[0].isActiveAndEnabled, Is.True);

                if (passIndex == 1 && variant == 2)
                {
                    // Gather references and allocate report storage before the timed
                    // phase; retain the generated category/direct-child layout.
                    result.batches.Capacity = prefabs.Count;
                    var activation = new PhaseReport { phase = "individual-prefab-budgeted-activation", scenePath = path, startedFrame = Time.frameCount };
                    result.activation = activation;
                    yield return null;
                    ResetNativeRecorders();
                    int next = 0;
                    while (next < prefabs.Count)
                    {
                        int count = 0;
                        long batchStarted = Stopwatch.GetTimestamp();
                        double maximumSingle = 0;
                        while (next < prefabs.Count && count < report.maximumPrefabsPerFrame)
                        {
                            long singleStarted = Stopwatch.GetTimestamp();
                            prefabs[next++].SetActive(true);
                            maximumSingle = Math.Max(maximumSingle, ElapsedMilliseconds(singleStarted));
                            count++;
                            if (ElapsedMilliseconds(batchStarted) >= report.softCpuBudgetMilliseconds) break;
                        }
                        double cpu = ElapsedMilliseconds(batchStarted);
                        result.batches.Add(new ActivationBatch { frame = Time.frameCount, prefabs = count, synchronousCpuMilliseconds = cpu, maximumSingleSetActiveMilliseconds = maximumSingle });
                        result.activatedPrefabCount += count;
                        result.maximumBatchCpuMilliseconds = Math.Max(result.maximumBatchCpuMilliseconds, cpu);
                        result.maximumSingleSetActiveMilliseconds = Math.Max(result.maximumSingleSetActiveMilliseconds, maximumSingle);
                        yield return null;
                        // The interval begins BEFORE SetActive, so activation work is
                        // not accidentally omitted from the observed frame gap.
                        Sample(activation, batchStarted);
                    }
                    yield return ObserveFrames(activation, 3);
                    activation.completedFrame = Time.frameCount;
                    CompletePhase(activation);
                    foreach (GameObject prefab in prefabs) Assert.That(prefab.activeInHierarchy, Is.True);
                    Assert.That(result.activatedPrefabCount, Is.EqualTo(fixture.woodyPrefabCount));
                    foreach (ActivationBatch batch in result.batches) Assert.That(batch.prefabs, Is.InRange(1, 64));
                }
                result.after = InspectNativePopulation(scene);
                foreach (VegetationWorldRenderer renderer in renderers)
                {
                    result.residentGpuBuffers += renderer.ResidentGpuBufferCount;
                    result.uploadedGrassInstances += renderer.UploadedInstanceCount;
                    CaptureBuffers(renderer);
                }
                Assert.That(capturedBuffers.Count, Is.EqualTo(result.residentGpuBuffers));
                var unload = new PhaseReport { passIndex = passIndex, phase = "unload-" + result.variant, scenePath = path, startedFrame = Time.frameCount };
                result.unload = unload;
                yield return null;
                ResetNativeRecorders();
                started = Stopwatch.GetTimestamp();
                inFlight = SceneManager.UnloadSceneAsync(scene);
                unload.requestCallMilliseconds = ElapsedMilliseconds(started);
                Assert.That(inFlight, Is.Not.Null, path);
                yield return ObserveOperation(unload, inFlight, started);
                inFlight = null;
                yield return ObserveFrames(unload, 3);
                CompletePhase(unload);
                Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False);
                foreach (GameObject prefab in prefabs) Assert.That(prefab == null, Is.True, "All experiment-owned prefab objects must be destroyed.");
                foreach (VegetationWorldRenderer renderer in renderers) Assert.That(renderer == null, Is.True);
                foreach (GraphicsBuffer buffer in capturedBuffers) Assert.That(buffer.IsValid(), Is.False);
                result.unloaded = true;
                ownedScenePaths.Remove(path);
            }

            NativePopulation reference = report.cases[3].after;
            foreach (WoodyCaseReport result in report.cases)
            {
                Assert.That(result.after.gameObjects, Is.EqualTo(reference.gameObjects));
                Assert.That(result.after.renderers, Is.EqualTo(reference.renderers));
                Assert.That(result.after.colliders, Is.EqualTo(reference.colliders));
            }
            NativePopulation activated = report.cases[5].after;
            Assert.That(activated.activeGameObjects, Is.EqualTo(reference.activeGameObjects));
            Assert.That(activated.activeRenderers, Is.EqualTo(reference.activeRenderers));
            Assert.That(activated.activeEnabledColliders, Is.EqualTo(reference.activeEnabledColliders));
            for (int i = 0; i < paths.Length; i++) Assert.That(HashScene(paths[i]), Is.EqualTo(hashes[i]), "Benchmark must not save scene changes.");
            report.passed = true;
            string output = ProjectPath("Artifacts/VegetationRebuild/Performance/woody-activation-experiment.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[Vegetation woody activation experiment] Wrote " + output);
        }

        private static List<GameObject> CollectWoodyPopulation(Scene scene, string variant, List<VegetationWorldRenderer> grassRenderers)
        {
            var prefabs = new List<GameObject>();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.GeneratorId, Is.EqualTo("msc.map-vegetation-rebuild.v1"));
                Assert.That(group.CellId, Is.EqualTo("cell_1_-3"));
                Assert.That(categories.Add(group.Category), Is.True);
                if (group.Category == "GrassCoverage")
                {
                    Assert.That(root.activeSelf, Is.True);
                    grassRenderers.AddRange(root.GetComponentsInChildren<VegetationWorldRenderer>(true));
                    continue;
                }
                Assert.That(new[] { "OriginalTrees", "BoundaryForest", "ShrubsAndUndergrowth" }, Does.Contain(group.Category));
                Assert.That(root.activeSelf, Is.EqualTo(variant != "category-inactive"));
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    GameObject prefab = root.transform.GetChild(i).gameObject;
                    Assert.That(prefab.activeSelf, Is.EqualTo(variant != "individual-inactive"));
                    prefabs.Add(prefab);
                }
            }
            Assert.That(categories.Count, Is.EqualTo(4));
            return prefabs;
        }

        private static NativePopulation InspectNativePopulation(Scene scene)
        {
            var result = new NativePopulation();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                { result.gameObjects++; if (transform.gameObject.activeInHierarchy) result.activeGameObjects++; }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                { result.renderers++; if (renderer.gameObject.activeInHierarchy) result.activeRenderers++; }
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                { result.colliders++; if (collider.gameObject.activeInHierarchy && collider.enabled) result.activeEnabledColliders++; }
            }
            return result;
        }

        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        private static string HashScene(string path)
        {
            using var stream = File.OpenRead(ProjectPath(path));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public sealed class WoodyExperimentBuildSetup : IPrebuildSetup, IPostBuildCleanup
        {
            private const string SessionKey = "MSC.Vegetation.WoodyActivationExperiment.BuildEntries";
            private static readonly string[] CopyPaths =
            {
                "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/Cell_1_-3_CategoryInactive.unity",
                "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/Cell_1_-3_IndividualInactive.unity"
            };

            public void Setup()
            {
                if (UnityEditor.SessionState.GetBool(SessionKey, false))
                    throw new InvalidOperationException("The previous woody experiment build-entry cleanup is pending.");
                // Missing local licensed fixtures make the Explicit test skip;
                // they must never cause unrelated tests to create donor assets.
                foreach (string path in CopyPaths) if (!File.Exists(ProjectPath(path))) return;
                var scenes = new List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                foreach (string path in CopyPaths)
                {
                    if (scenes.Exists(scene => scene.path == path))
                        throw new InvalidOperationException("Remove stale diagnostic build entries before this test: " + path);
                    scenes.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                }
                UnityEditor.SessionState.SetBool(SessionKey, true);
                try { UnityEditor.EditorBuildSettings.scenes = scenes.ToArray(); }
                catch { UnityEditor.SessionState.EraseBool(SessionKey); throw; }
            }

            public void Cleanup()
            {
                if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
                // Only remove our two appended entries. Other fixture setup or
                // cleanup may also have changed the build list in this test run.
                var scenes = new List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                scenes.RemoveAll(scene => Array.IndexOf(CopyPaths, scene.path) >= 0);
                UnityEditor.EditorBuildSettings.scenes = scenes.ToArray();
                UnityEditor.SessionState.EraseBool(SessionKey);
            }
        }

        [Serializable]
        private sealed class WoodyFixture
        {
            public string version, utc, sourceScene, sourceSha256, categoryScene, categorySha256, individualScene, individualSha256;
            public string transformPrefabFingerprint, grassFingerprint;
            public int woodyPrefabCount, woodyCategoryCount, gameObjectCount;
            public bool sourceUnchanged, passed;
        }

        [Serializable]
        private sealed class WoodyExperimentReport
        {
            public string utc, unityVersion, graphicsDevice, method, limitations;
            public bool passed;
            public WoodyFixture fixture;
            public Vector3 cameraPosition;
            public int maximumPrefabsPerFrame;
            public double softCpuBudgetMilliseconds;
            public ActivationReport native = new ActivationReport();
            public List<WoodyCaseReport> cases = new List<WoodyCaseReport>();
        }

        [Serializable]
        private sealed class WoodyCaseReport
        {
            public string variant, scenePath;
            public bool priming, unloaded;
            public NativePopulation before, after;
            public PhaseReport load, activation, unload;
            public int activatedPrefabCount, residentGpuBuffers;
            public long uploadedGrassInstances;
            public double maximumBatchCpuMilliseconds, maximumSingleSetActiveMilliseconds;
            public List<ActivationBatch> batches = new List<ActivationBatch>();
        }

        [Serializable]
        private sealed class NativePopulation
        {
            public int gameObjects, activeGameObjects, renderers, activeRenderers, colliders, activeEnabledColliders;
        }

        [Serializable]
        private sealed class ActivationBatch
        {
            public int frame, prefabs;
            public double synchronousCpuMilliseconds, maximumSingleSetActiveMilliseconds;
        }

        [UnityTest, Explicit("Measures full prefab versus full unpacked and representative 64/128-prefab scene copies. PrepareAlternativeBatch must have validated the private fixtures first.")]
        [PrebuildSetup(typeof(AlternativeExperimentBuildSetup)), PostBuildCleanup(typeof(AlternativeExperimentBuildSetup))]
        public IEnumerator SavedWoodyPopulation_UnpackedVersusSmallSceneBenchmark()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsInstancing)
                Assert.Ignore("This benchmark requires graphics.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null);
            if (Camera.allCamerasCount > 0) Assert.Ignore("An existing camera would contaminate this benchmark.");
            string fixturePath = ProjectPath("Artifacts/VegetationRebuild/Performance/woody-alternative-experiment-fixture.json");
            if (!File.Exists(fixturePath)) Assert.Ignore("Run the explicit PrepareAlternativeBatch first.");
            AlternativeFixture fixture = JsonUtility.FromJson<AlternativeFixture>(File.ReadAllText(fixturePath));
            Assert.That(fixture.version, Is.EqualTo("msc.woody-alternative-experiment.v1"));
            Assert.That(fixture.passed, Is.True);
            Assert.That(fixture.sourceScene, Is.EqualTo(SceneFolder + "World_Cell_1_-3_Vegetation.unity"));
            Assert.That(fixture.variants.Count, Is.EqualTo(3));
            Assert.That(fixture.source.scenePath, Is.EqualTo(fixture.sourceScene));
            Assert.That(fixture.source.sha256, Is.EqualTo(fixture.sourceSha256));
            var variants = new List<AlternativeVariant> { fixture.source };
            variants.AddRange(fixture.variants);
            for (int i = 0; i < variants.Count; i++)
            {
                AlternativeVariant variant = variants[i];
                if (i > 0) Assert.That(variant.scenePath, Is.EqualTo(AlternativeExperimentBuildSetup.CopyPaths[i - 1]));
                Assert.That(HashScene(variant.scenePath), Is.EqualTo(variant.sha256));
                Assert.That(variant.selected.Count, Is.EqualTo(variant.woodyPrefabCount));
                if (SceneUtility.GetBuildIndexByScenePath(variant.scenePath) < 0) Assert.Ignore("Missing enabled prebuild scene: " + variant.scenePath);
                if (SceneManager.GetSceneByPath(variant.scenePath).isLoaded) Assert.Ignore("Cannot adopt/unload an existing scene: " + variant.scenePath);
            }
            Assert.That(variants[1].woodyPrefabCount, Is.EqualTo(fixture.source.woodyPrefabCount));
            Assert.That(variants[1].connectedPrefabCount, Is.Zero);
            Assert.That(variants[2].woodyPrefabCount, Is.EqualTo(64));
            Assert.That(variants[3].woodyPrefabCount, Is.EqualTo(128));
            Assert.That(variants[2].connectedPrefabCount, Is.EqualTo(64));
            Assert.That(variants[3].connectedPrefabCount, Is.EqualTo(128));

            var report = new AlternativeExperimentReport
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                fixture = fixture, cameraPosition = new Vector3(146.3195953f, 2.7205393f, -1046.6018066f), initialSceneCount = SceneManager.sceneCount,
                method = "Prime active-full, fully-unpacked-full, representative64 and representative128 scene variants, unloading each without an unused-asset scan. Repeat all four warm in the same process. Identical grass catalog and fixed 256px home camera; all selected woody roots remain active. The smaller scenes intentionally contain subsets, not all trees. No runtime mutation of the scene populations.",
                limitations = "Editor PlayMode diagnostic, one measured warm repeat per variant in fixed order; not statistical or player/60 FPS acceptance. Unpacking is restricted to copies and is not a proposed production migration. Larger serialized unpacked files may trade background load work for a smaller integration spike. Native marker totals are nested/parallel and cannot be added. Far grass may legitimately allocate zero GPU buffers."
            };
            try { mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 256); }
            catch (Exception exception) { report.native.mainThreadRecorderFailure = exception.GetType().Name + ": " + exception.Message; }
            CreateCamera(report.cameraPosition);
            yield return ObserveFrames(new PhaseReport(), 30);

            for (int passIndex = 0; passIndex < 2; passIndex++)
            foreach (AlternativeVariant variant in variants)
            {
                var result = new WoodyCaseReport { priming = passIndex == 0, variant = variant.name, scenePath = variant.scenePath };
                report.cases.Add(result);
                DiscoverNativeMarkers(
                    report.native.discoveredNativeMarkers, passIndex);
                for (int frame = 0; frame < 3; frame++) yield return null;
                renderers.Clear(); capturedBuffers.Clear();
                var phase = new PhaseReport { phase = "load-" + variant.name, passIndex = passIndex, scenePath = variant.scenePath,
                    buildIndex = SceneUtility.GetBuildIndexByScenePath(variant.scenePath), startedFrame = Time.frameCount };
                result.load = phase;
                ResetNativeRecorders();
                long started = Stopwatch.GetTimestamp();
                inFlight = SceneManager.LoadSceneAsync(phase.buildIndex, LoadSceneMode.Additive);
                phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                Assert.That(inFlight, Is.Not.Null);
                ownedScenePaths.Add(variant.scenePath);
                yield return ObserveOperation(phase, inFlight, started);
                inFlight = null;
                yield return ObserveFrames(phase, 3);
                CompletePhase(phase);
                Scene scene = SceneManager.GetSceneByPath(variant.scenePath);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                Assert.That(scene.buildIndex, Is.EqualTo(phase.buildIndex));
                List<GameObject> prefabs = CollectWoodyPopulation(scene, "active", renderers);
                Assert.That(prefabs.Count, Is.EqualTo(variant.woodyPrefabCount));
                var identities = new HashSet<string>(StringComparer.Ordinal);
                foreach (AlternativeRecord record in variant.selected) Assert.That(identities.Add(record.key), Is.True);
                foreach (GameObject prefab in prefabs)
                {
                    string key = prefab.transform.parent.GetComponent<GeneratedVegetationGroup>().Category + "/" + prefab.name;
                    Assert.That(identities.Remove(key), Is.True, "Exact selected identities must survive scene loading.");
                    Assert.That(prefab.activeInHierarchy, Is.True);
                }
                Assert.That(identities.Count, Is.Zero);
                result.before = result.after = InspectNativePopulation(scene);
                Assert.That(result.before.gameObjects, Is.EqualTo(variant.gameObjects));
                Assert.That(result.before.renderers, Is.EqualTo(variant.renderers));
                Assert.That(result.before.colliders, Is.EqualTo(variant.colliders));
                Assert.That(renderers.Count, Is.EqualTo(1));
                Assert.That(renderers[0].isActiveAndEnabled, Is.True);
                phase.rootCount = scene.rootCount; phase.rendererCount = renderers.Count;
                foreach (VegetationWorldRenderer renderer in renderers)
                {
                    result.residentGpuBuffers += renderer.ResidentGpuBufferCount;
                    result.uploadedGrassInstances += renderer.UploadedInstanceCount;
                    CaptureBuffers(renderer);
                }
                Assert.That(capturedBuffers.Count, Is.EqualTo(result.residentGpuBuffers));
                phase = new PhaseReport { phase = "unload-" + variant.name, passIndex = passIndex, scenePath = variant.scenePath, startedFrame = Time.frameCount };
                result.unload = phase;
                yield return null;
                ResetNativeRecorders();
                started = Stopwatch.GetTimestamp();
                inFlight = SceneManager.UnloadSceneAsync(scene);
                phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                Assert.That(inFlight, Is.Not.Null);
                yield return ObserveOperation(phase, inFlight, started);
                inFlight = null;
                yield return ObserveFrames(phase, 3);
                CompletePhase(phase);
                Assert.That(SceneManager.GetSceneByPath(variant.scenePath).isLoaded, Is.False);
                foreach (GameObject prefab in prefabs) Assert.That(prefab == null, Is.True);
                foreach (VegetationWorldRenderer renderer in renderers) Assert.That(renderer == null, Is.True);
                foreach (GraphicsBuffer buffer in capturedBuffers) Assert.That(buffer.IsValid(), Is.False);
                result.unloaded = true;
                ownedScenePaths.Remove(variant.scenePath);
            }
            // Full unpacked copy must retain the native population and enabled
            // colliders, in addition to the Editor's per-ID geometric proof.
            for (int pass = 0; pass < 2; pass++)
            {
                NativePopulation active = report.cases[pass * 4].after, unpacked = report.cases[pass * 4 + 1].after;
                Assert.That(JsonUtility.ToJson(unpacked), Is.EqualTo(JsonUtility.ToJson(active)));
            }
            foreach (AlternativeVariant variant in variants) Assert.That(HashScene(variant.scenePath), Is.EqualTo(variant.sha256));
            report.finalSceneCount = SceneManager.sceneCount;
            Assert.That(report.finalSceneCount, Is.EqualTo(report.initialSceneCount));
            report.passed = true;
            string output = ProjectPath("Artifacts/VegetationRebuild/Performance/woody-alternative-experiment.json");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[Vegetation alternative scene experiment] Wrote " + output);
        }

        public sealed class AlternativeExperimentBuildSetup : IPrebuildSetup, IPostBuildCleanup
        {
            private const string SessionKey = "MSC.Vegetation.AlternativeExperiment.BuildEntries";
            public static readonly string[] CopyPaths =
            {
                "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/Cell_1_-3_Unpacked.unity",
                "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/Cell_1_-3_Slice64.unity",
                "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Experiments/SceneActivation/Cell_1_-3_Slice128.unity"
            };
            public void Setup()
            {
                if (UnityEditor.SessionState.GetBool(SessionKey, false)) throw new InvalidOperationException("Previous alternative experiment cleanup is pending.");
                foreach (string path in CopyPaths) if (!File.Exists(ProjectPath(path))) return;
                var scenes = new List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                foreach (string path in CopyPaths)
                {
                    if (scenes.Exists(scene => scene.path == path)) throw new InvalidOperationException("Stale alternative diagnostic build entry: " + path);
                    scenes.Add(new UnityEditor.EditorBuildSettingsScene(path, true));
                }
                UnityEditor.SessionState.SetBool(SessionKey, true);
                try { UnityEditor.EditorBuildSettings.scenes = scenes.ToArray(); }
                catch { UnityEditor.SessionState.EraseBool(SessionKey); throw; }
            }
            public void Cleanup()
            {
                if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
                var scenes = new List<UnityEditor.EditorBuildSettingsScene>(UnityEditor.EditorBuildSettings.scenes);
                scenes.RemoveAll(scene => Array.IndexOf(CopyPaths, scene.path) >= 0);
                UnityEditor.EditorBuildSettings.scenes = scenes.ToArray();
                UnityEditor.SessionState.EraseBool(SessionKey);
            }
        }

        [Serializable] private sealed class AlternativeRecord { public string key; }
        [Serializable] private sealed class AlternativeVariant
        {
            public string name, scenePath, sha256, grassFingerprint;
            public int woodyPrefabCount, connectedPrefabCount, gameObjects, renderers, colliders;
            public List<AlternativeRecord> selected;
        }
        [Serializable] private sealed class AlternativeFixture
        {
            public string version, utc, sourceScene, sourceSha256, selection; public bool passed;
            public AlternativeVariant source; public List<AlternativeVariant> variants;
        }
        [Serializable] private sealed class AlternativeExperimentReport
        {
            public string utc, unityVersion, graphicsDevice, method, limitations; public bool passed;
            public int initialSceneCount, finalSceneCount; public Vector3 cameraPosition;
            public AlternativeFixture fixture;
            public ActivationReport native = new ActivationReport();
            public List<WoodyCaseReport> cases = new List<WoodyCaseReport>();
        }


        private static PerformanceThresholds NewPerformanceThresholds() =>
            new PerformanceThresholds
            {
                maximumMainThreadMilliseconds =
                    MaximumMainThreadMilliseconds,
                maximumYieldIntervalMilliseconds =
                    MaximumYieldIntervalMilliseconds,
                maximumP95YieldIntervalMilliseconds =
                    MaximumP95YieldIntervalMilliseconds
            };

        private static BenchmarkPrerequisiteCapture
            CaptureBenchmarkPrerequisite(IReadOnlyList<string> paths)
        {
            var result = new BenchmarkPrerequisiteCapture
            {
                gatePath = BenchmarkPreparationGatePath,
                prerequisiteExecuteMethod =
                    "MSC.Editor.Vegetation." +
                    "MapVegetationActivationBenchmarkPreparation." +
                    "PrepareBenchmarkCells",
                sourceFreshnessPolicy =
                    "The gate records the semantic source fingerprint produced " +
                    "by MapVegetationContext. At benchmark start, every " +
                    "configured source scene and all AssetDatabase dependencies " +
                    "are rehashed and must match the sealed source dependency " +
                    "fingerprint; settings and packed project policy are also " +
                    "recomputed. Each loaded scene plan fingerprint and file " +
                    "SHA-256 must match the gate. Every generated vegetation " +
                    "dependency below the generated root is sealed by exact " +
                    "path, byte count and SHA-256; the current transitive " +
                    "dependency set and aggregate fingerprint must still match."
            };
            try
            {
                if (!File.Exists(ProjectPath(BenchmarkPreparationGatePath)))
                {
                    result.errors.Add(
                        "Preparation gate is missing. Run the prerequisite " +
                        "executeMethod.");
                    return result;
                }
                result.gateSha256 = HashScene(
                    BenchmarkPreparationGatePath);
                BenchmarkGateDocument gate = JsonUtility.FromJson<
                    BenchmarkGateDocument>(File.ReadAllText(
                        ProjectPath(BenchmarkPreparationGatePath)));
                if (gate == null)
                {
                    result.errors.Add("Preparation gate JSON is invalid.");
                    return result;
                }
                result.gatePassed = gate.passed;
                result.schemaVersion = gate.schemaVersion;
                result.generatedUtc = gate.utc;
                result.generatorId = gate.generatorId;
                result.packedPresentationVersion =
                    gate.packedPresentationVersion;
                result.settingsHash = gate.settingsHash;
                result.sourceFingerprint = gate.sourceFingerprint;
                result.sourceDependencyFingerprint =
                    gate.sourceDependencyFingerprint;
                result.projectPolicySignature =
                    gate.projectPolicySignature;
                if (gate.errors != null)
                    foreach (string error in gate.errors)
                        result.errors.Add("Preparation: " + error);

                Type provider = Type.GetType(
                    BenchmarkPreparationProviderType, false);
                MethodInfo capture = provider?.GetMethod(
                    "CaptureCurrentPrerequisitesJson",
                    BindingFlags.Public | BindingFlags.Static);
                if (capture == null)
                    throw new MissingMethodException(
                        BenchmarkPreparationProviderType,
                        "CaptureCurrentPrerequisitesJson");
                string currentJson = capture.Invoke(
                    null, null) as string;
                CurrentBenchmarkPrerequisites current = JsonUtility.FromJson<
                    CurrentBenchmarkPrerequisites>(currentJson);
                if (current == null)
                    throw new InvalidDataException(
                        "Current benchmark prerequisite JSON is invalid.");
                result.currentSettingsHash = current.settingsHash;
                result.currentSourceDependencyFingerprint =
                    current.sourceDependencyFingerprint;
                result.currentProjectPolicySignature =
                    current.projectPolicySignature;

                if (!gate.passed)
                    result.errors.Add(
                        "Preparation gate did not pass.");
                if (gate.schemaVersion !=
                    "msc.vegetation-activation-preparation.v1")
                    result.errors.Add(
                        "Preparation gate schema is stale: " +
                        gate.schemaVersion);
                if (gate.generatorId != VegetationGeneratorId)
                    result.errors.Add(
                        "Preparation generator ID is stale.");
                if (gate.packedPresentationVersion !=
                    PackedPresentationVersion)
                    result.errors.Add(
                        "Packed presentation policy is stale.");
                if (string.IsNullOrWhiteSpace(gate.sourceFingerprint))
                    result.errors.Add(
                        "Semantic source fingerprint is missing.");
                if (gate.settingsHash != current.settingsHash)
                    result.errors.Add(
                        "Current vegetation settings differ from the sealed " +
                        "benchmark cells.");
                if (gate.sourceDependencyFingerprint !=
                    current.sourceDependencyFingerprint)
                    result.errors.Add(
                        "Current source scenes/dependencies differ from the " +
                        "sealed benchmark cells.");
                if (gate.projectPolicySignature !=
                    current.projectPolicySignature)
                    result.errors.Add(
                        "Current packed project policy differs from the sealed " +
                        "benchmark cells.");

                List<PreparedBenchmarkCell> prepared = gate.cells ??
                    new List<PreparedBenchmarkCell>();
                List<CurrentPreparedBenchmarkCell> currentCells =
                    current.cells ??
                    new List<CurrentPreparedBenchmarkCell>();
                if (prepared.Count != paths.Count)
                    result.errors.Add(
                        "Preparation gate must contain exactly " + paths.Count +
                        " benchmark cells.");
                if (currentCells.Count != paths.Count)
                    result.errors.Add(
                        "Current prerequisite capture must contain exactly " +
                        paths.Count + " benchmark cells.");
                for (int index = 0; index < paths.Count; index++)
                {
                    PreparedBenchmarkCell cell = index < prepared.Count
                        ? prepared[index] : null;
                    if (cell == null)
                    {
                        result.errors.Add(
                            "Missing prepared cell at index " + index + ".");
                        continue;
                    }
                    CurrentPreparedBenchmarkCell currentCell = currentCells
                        .FirstOrDefault(candidate => string.Equals(
                            candidate.cellId,
                            cell.cellId,
                            StringComparison.Ordinal));
                    var captured = new PreparedBenchmarkCell
                    {
                        cellId = cell.cellId,
                        scenePath = cell.scenePath,
                        sceneSha256 = cell.sceneSha256,
                        planFingerprint = cell.planFingerprint,
                        generatedDependencyFingerprint =
                            cell.generatedDependencyFingerprint,
                        generatedDependencies =
                            cell.generatedDependencies ??
                            new List<PreparedBenchmarkDependency>(),
                        currentGeneratedDependencyFingerprint =
                            currentCell?.generatedDependencyFingerprint,
                        currentGeneratedDependencies =
                            currentCell?.generatedDependencies ??
                            new List<PreparedBenchmarkDependency>(),
                        currentSceneSha256 = File.Exists(
                                ProjectPath(paths[index]))
                            ? HashScene(paths[index]) : string.Empty
                    };
                    result.cells.Add(captured);
                    string expectedCell = ExpectedCellId(paths[index]);
                    if (cell.cellId != expectedCell ||
                        cell.scenePath != paths[index])
                        result.errors.Add(
                            "Preparation gate cell order/path differs at " +
                            index + ".");
                    if (string.IsNullOrWhiteSpace(cell.planFingerprint))
                        result.errors.Add(
                            "Prepared plan fingerprint is missing for " +
                            expectedCell + ".");
                    if (string.IsNullOrWhiteSpace(
                            cell.generatedDependencyFingerprint) ||
                        cell.generatedDependencies == null ||
                        cell.generatedDependencies.Count == 0)
                    {
                        result.errors.Add(
                            "Prepared generated dependency seal is missing " +
                            "for " + expectedCell + ". Run the prerequisite " +
                            "again.");
                    }
                    if (currentCell == null)
                    {
                        result.errors.Add(
                            "Current generated dependency capture is missing " +
                            "for " + expectedCell + ".");
                    }
                    else
                    {
                        if (currentCell.scenePath != paths[index])
                        {
                            result.errors.Add(
                                "Current generated dependency capture path " +
                                "differs for " + expectedCell + ".");
                        }
                        if (cell.generatedDependencyFingerprint !=
                            currentCell.generatedDependencyFingerprint)
                        {
                            result.errors.Add(
                                "Generated vegetation dependencies changed " +
                                "after preparation: " + expectedCell);
                        }
                        CompareGeneratedDependencySets(
                            expectedCell,
                            cell.generatedDependencies,
                            currentCell.generatedDependencies,
                            result.errors);
                    }
                    if (cell.sceneSha256 != captured.currentSceneSha256)
                        result.errors.Add(
                            "Saved scene changed after preparation: " +
                            paths[index]);
                }
                result.passed = result.errors.Count == 0;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                result.errors.Add(cause.GetType().Name + ": " +
                    cause.Message);
            }
            catch (Exception exception)
            {
                result.errors.Add(exception.GetType().Name + ": " +
                    exception.Message);
            }
            return result;
        }

        private static void CompareGeneratedDependencySets(
            string cellId,
            IReadOnlyList<PreparedBenchmarkDependency> sealedDependencies,
            IReadOnlyList<PreparedBenchmarkDependency> currentDependencies,
            ICollection<string> errors)
        {
            sealedDependencies ??= Array.Empty<PreparedBenchmarkDependency>();
            currentDependencies ??= Array.Empty<PreparedBenchmarkDependency>();
            if (sealedDependencies.Count != currentDependencies.Count)
            {
                errors.Add(
                    "Generated dependency count changed after preparation " +
                    "for " + cellId + ": " + sealedDependencies.Count +
                    " != " + currentDependencies.Count + ".");
            }
            int count = Math.Min(
                sealedDependencies.Count, currentDependencies.Count);
            for (int index = 0; index < count; index++)
            {
                PreparedBenchmarkDependency expected =
                    sealedDependencies[index];
                PreparedBenchmarkDependency actual =
                    currentDependencies[index];
                if (expected == null || actual == null)
                {
                    errors.Add(
                        "Generated dependency entry is null for " + cellId +
                        " at index " + index + ".");
                    continue;
                }
                if (expected.path == actual.path &&
                    expected.bytes == actual.bytes &&
                    expected.sha256 == actual.sha256)
                    continue;
                errors.Add(
                    "Generated dependency changed for " + cellId +
                    " at index " + index + ": " +
                    (expected.path ?? "<null>") + ".");
            }
        }

        private static void ApplyPreparedCellExpectation(
            SavedSceneAcceptanceCapture saved,
            BenchmarkPrerequisiteCapture prerequisite)
        {
            PreparedBenchmarkCell prepared = prerequisite.cells
                .FirstOrDefault(cell => string.Equals(
                    cell.scenePath,
                    saved.scenePath,
                    StringComparison.Ordinal));
            if (prepared == null)
                saved.errors.Add(
                    "Saved scene is absent from the preparation gate.");
            else if (saved.planFingerprint != prepared.planFingerprint)
                saved.errors.Add(
                    "Loaded scene plan fingerprint differs from the current " +
                    "sealed plan: " + saved.planFingerprint + " != " +
                    prepared.planFingerprint);
            saved.passed = saved.errors.Count == 0;
        }

        private static SavedFileCapture CaptureSavedFile(string path)
        {
            string fullPath = ProjectPath(path);
            return new SavedFileCapture
            {
                path = path,
                buildIndex = SceneUtility.GetBuildIndexByScenePath(path),
                bytes = new FileInfo(fullPath).Length,
                sha256 = HashScene(path)
            };
        }

        private static ComparisonBaseline CaptureComparisonBaseline()
        {
            const string path =
                "Artifacts/VegetationRebuild/Performance/" +
                "cell-activation-benchmark.json";
            var result = new ComparisonBaseline
            {
                path = path,
                expectedSha256 =
                    "bc75ed37f558a6e2a6748c35caf729b488c584f0b7ed78302ce0a84eba8b1f47",
                coldCellOneMaximumMainThreadMilliseconds = 204.2011d,
                warmCellOneMaximumMainThreadMilliseconds = 247.9387d,
                worstMaximumMainThreadMilliseconds = 247.9387d,
                comparisonScope = "Same direct additive scene paths, camera position, two-pass cache policy and completed-frame Main Thread recorder as the verified old artifact. The new maximum additionally includes deferred grass metadata/GPU settlement, so it is a conservative superset rather than a phase-identical A/B. Async wall time remains diagnostic only.",
                nonComparableEvidenceNote = "The separately reported approximately 870 ms end-to-end observation was not emitted by this direct benchmark schema and is not treated as a numeric A/B baseline."
            };
            if (!File.Exists(ProjectPath(path))) return result;
            result.bytes = new FileInfo(ProjectPath(path)).Length;
            result.sha256 = HashScene(path);
            result.expectedHashMatches = string.Equals(
                result.sha256,
                result.expectedSha256,
                StringComparison.OrdinalIgnoreCase);
            return result;
        }

        private static ComparisonResult CompareWithBaseline(
            ComparisonBaseline baseline,
            double measuredMaximumMainThreadMilliseconds)
        {
            var result = new ComparisonResult
            {
                baselineMaximumMainThreadMilliseconds =
                    baseline.worstMaximumMainThreadMilliseconds,
                measuredMaximumMainThreadMilliseconds =
                    measuredMaximumMainThreadMilliseconds,
                baselineHashVerified = baseline.expectedHashMatches
            };
            if (baseline.worstMaximumMainThreadMilliseconds > 0d)
            {
                result.reductionPercent =
                    (baseline.worstMaximumMainThreadMilliseconds -
                     measuredMaximumMainThreadMilliseconds) * 100d /
                    baseline.worstMaximumMainThreadMilliseconds;
            }
            result.old204To248MillisecondClassEliminated =
                measuredMaximumMainThreadMilliseconds <
                MaximumMainThreadMilliseconds;
            return result;
        }

        private static void AssertSavedAcceptanceOutput(string path)
        {
            Assert.That(File.Exists(ProjectPath(path)), Is.True,
                "Acceptance output is missing: " + path);
            Assert.That(SceneUtility.GetBuildIndexByScenePath(path),
                Is.GreaterThanOrEqualTo(0),
                "Acceptance output is disabled in Build Settings: " + path);
            Scene scene = SceneManager.GetSceneByPath(path);
            Assert.That(!scene.IsValid() || !scene.isLoaded, Is.True,
                "Acceptance benchmark must own the scene: " + path);
        }

        private static PerformanceGateReport EvaluatePerformance(
            IReadOnlyList<PhaseReport> phases,
            int expectedPhaseCount)
        {
            var result = new PerformanceGateReport
            {
                expectedPhaseCount = expectedPhaseCount,
                measuredPhaseCount = phases.Count,
                thresholds = NewPerformanceThresholds()
            };
            var yields = new List<double>();
            var mainThread = new List<double>();
            for (int index = 0; index < phases.Count; index++)
            {
                PhaseReport phase = phases[index];
                yields.AddRange(phase.observedYieldIntervalsMilliseconds);
                mainThread.AddRange(phase.mainThreadMilliseconds);
                if (phase.observedYieldIntervalsMilliseconds.Count == 0)
                    result.errors.Add(
                        "No observed yield samples for " + phase.scenePath);
                if (!phase.mainThreadRecorderAvailable ||
                    phase.mainThreadMilliseconds.Count == 0)
                    result.errors.Add(
                        "No positive Main Thread recorder samples for " +
                        phase.scenePath);
            }
            if (phases.Count != expectedPhaseCount)
                result.errors.Add(
                    "Expected " + expectedPhaseCount +
                    " measured activation phases, observed " + phases.Count);
            result.observedYieldSampleCount = yields.Count;
            result.mainThreadSampleCount = mainThread.Count;
            result.maximumObservedYieldIntervalMilliseconds = Maximum(yields);
            result.p95ObservedYieldIntervalMilliseconds =
                Percentile95(yields);
            result.maximumMainThreadMilliseconds = Maximum(mainThread);
            result.p95MainThreadMilliseconds = Percentile95(mainThread);
            result.measurementValid = result.errors.Count == 0 &&
                yields.Count > 0 && mainThread.Count > 0;
            if (result.maximumMainThreadMilliseconds >=
                MaximumMainThreadMilliseconds)
                result.errors.Add(
                    "Maximum Main Thread frame exceeded the strict Editor " +
                    "gate: " + result.maximumMainThreadMilliseconds +
                    " >= " + MaximumMainThreadMilliseconds + " ms");
            if (result.maximumObservedYieldIntervalMilliseconds >=
                MaximumYieldIntervalMilliseconds)
                result.errors.Add(
                    "Maximum observed yield interval exceeded the strict " +
                    "Editor gate: " +
                    result.maximumObservedYieldIntervalMilliseconds +
                    " >= " + MaximumYieldIntervalMilliseconds + " ms");
            if (result.p95ObservedYieldIntervalMilliseconds >=
                MaximumP95YieldIntervalMilliseconds)
                result.errors.Add(
                    "P95 observed yield interval exceeded the strict Editor " +
                    "gate: " +
                    result.p95ObservedYieldIntervalMilliseconds +
                    " >= " + MaximumP95YieldIntervalMilliseconds + " ms");
            result.performancePassed = result.measurementValid &&
                result.maximumMainThreadMilliseconds <
                    MaximumMainThreadMilliseconds &&
                result.maximumObservedYieldIntervalMilliseconds <
                    MaximumYieldIntervalMilliseconds &&
                result.p95ObservedYieldIntervalMilliseconds <
                    MaximumP95YieldIntervalMilliseconds;
            return result;
        }

        private static SavedSceneAcceptanceCapture InspectSavedScene(
            string path)
        {
            var result = new SavedSceneAcceptanceCapture
            {
                scenePath = path,
                expectedCellId = ExpectedCellId(path)
            };
            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.errors.Add("Exact saved scene is not loaded.");
                return result;
            }

            var categories = new HashSet<string>(StringComparer.Ordinal);
            int colliderCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.rootCount++;
                GeneratedVegetationGroup group =
                    root.GetComponent<GeneratedVegetationGroup>();
                if (group == null)
                {
                    result.errors.Add(
                        "Root has no GeneratedVegetationGroup: " + root.name);
                    continue;
                }
                result.generatedGroupCount++;
                if (!categories.Add(group.Category))
                    result.errors.Add(
                        "Duplicate vegetation category: " + group.Category);
                if (group.GeneratorId != VegetationGeneratorId)
                    result.errors.Add(
                        "Unexpected generator for " + group.Category);
                if (group.CellId != result.expectedCellId)
                    result.errors.Add(
                        "Unexpected cell ID for " + group.Category + ": " +
                        group.CellId);
                if (string.IsNullOrWhiteSpace(group.Fingerprint))
                    result.errors.Add(
                        "Missing plan fingerprint for " + group.Category);
                else if (string.IsNullOrEmpty(result.planFingerprint))
                    result.planFingerprint = group.Fingerprint;
                else if (result.planFingerprint != group.Fingerprint)
                    result.errors.Add(
                        "Category plan fingerprints differ inside the scene.");
                if (!root.activeSelf || !root.activeInHierarchy)
                    result.errors.Add(
                        "Generated category is disabled: " + group.Category);

                foreach (Transform transform in
                         root.GetComponentsInChildren<Transform>(true))
                {
                    result.gameObjectCount++;
                    if (PrefabUtility.IsPartOfPrefabInstance(
                            transform.gameObject))
                        result.legacyPrefabInstanceGameObjects++;
                }
                result.directRendererComponents += root
                    .GetComponentsInChildren<Renderer>(true).Length;
                result.serializedLodGroupComponents += root
                    .GetComponentsInChildren<LODGroup>(true).Length;
                colliderCount += root
                    .GetComponentsInChildren<Collider>(true).Length;
                foreach (PackedWoodyCollisionPool pool in root
                             .GetComponentsInChildren<
                                 PackedWoodyCollisionPool>(true))
                    result.runtimePooledColliderComponents +=
                        pool.CreatedColliderCount;

                VegetationWorldRenderer[] grass = root
                    .GetComponentsInChildren<VegetationWorldRenderer>(true);
                PackedWoodyCellRenderer[] woody = root
                    .GetComponentsInChildren<PackedWoodyCellRenderer>(true);
                if (group.Category == "GrassCoverage")
                {
                    if (grass.Length != 1 || woody.Length != 0)
                        result.errors.Add(
                            "GrassCoverage must own exactly one grass renderer " +
                            "and no packed woody renderer.");
                    foreach (VegetationWorldRenderer renderer in grass)
                        result.grassRenderers.Add(
                            CaptureGrassRenderer(renderer, group));
                    continue;
                }

                if (!KnownWoodyGroupCategory(group.Category) ||
                    grass.Length != 0 || woody.Length != 1)
                {
                    result.errors.Add(
                        "Woody category must own exactly one packed renderer: " +
                        group.Category);
                }
                foreach (PackedWoodyCellRenderer renderer in woody)
                    result.packedRenderers.Add(
                        CapturePackedRenderer(renderer, group));
            }

            var packedAssetPaths = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (PackedRendererCapture capture in
                     result.packedRenderers)
            {
                if (string.IsNullOrWhiteSpace(capture.assetPath) ||
                    !packedAssetPaths.Add(capture.assetPath))
                    result.errors.Add(
                        "Packed woody categories must reference three unique " +
                        "cell assets.");
                result.packedAssetBytes += capture.assetBytes;
                result.packedPlacementMetadataCount +=
                    capture.placementMetadataCount;
            }
            result.packedAssetByteBudget = PackedWoodyCellAsset
                .SerializedAssetByteBudget(
                    result.packedPlacementMetadataCount);
            result.packedAssetBytesPerPlacement =
                result.packedPlacementMetadataCount > 0
                    ? (double)result.packedAssetBytes /
                      result.packedPlacementMetadataCount
                    : 0d;
            result.packedAssetBudgetPassed =
                result.packedRenderers.Count == 3 &&
                packedAssetPaths.Count == 3 &&
                result.packedAssetBytes > 0 &&
                PackedWoodyCellAsset.SerializedAssetBytesFitBudget(
                    result.packedAssetBytes,
                    result.packedPlacementMetadataCount);
            if (!result.packedAssetBudgetPassed)
                result.errors.Add(
                    "Per-cell packed asset byte budget exceeded or could not " +
                    "be measured across exactly three assets: " +
                    result.packedAssetBytes + " > " +
                    result.packedAssetByteBudget + " bytes for " +
                    result.packedPlacementMetadataCount +
                    " placement records.");

            result.serializedColliderComponents = Math.Max(
                0, colliderCount - result.runtimePooledColliderComponents);
            foreach (string expected in new[]
                     {
                         "GrassCoverage",
                         "OriginalTrees",
                         "BoundaryForest",
                         "ShrubsAndUndergrowth"
                     })
                if (!categories.Contains(expected))
                    result.errors.Add(
                        "Missing generated category: " + expected);
            if (categories.Count != 4 || result.generatedGroupCount != 4 ||
                result.rootCount != 4)
                result.errors.Add(
                    "Saved scene must contain exactly four generated category " +
                    "roots and no legacy roots.");
            if (result.legacyPrefabInstanceGameObjects != 0)
                result.errors.Add(
                    "Saved packed scene retained legacy prefab instances: " +
                    result.legacyPrefabInstanceGameObjects);
            if (result.directRendererComponents != 0)
                result.errors.Add(
                    "Saved packed scene retained direct Renderer components: " +
                    result.directRendererComponents);
            if (result.serializedLodGroupComponents != 0)
                result.errors.Add(
                    "Saved packed scene retained serialized LODGroups: " +
                    result.serializedLodGroupComponents);
            if (result.serializedColliderComponents != 0)
                result.errors.Add(
                    "Saved packed scene retained serialized Collider " +
                    "components: " + result.serializedColliderComponents);
            if (result.grassRenderers.Count != 1 ||
                !AllRendererCapturesPassed(result.grassRenderers))
                result.errors.Add(
                    "Grass renderer output is missing or incomplete.");
            if (result.packedRenderers.Count != 3 ||
                !AllPackedRendererCapturesPassed(result.packedRenderers))
                result.errors.Add(
                    "Packed woody renderer output is missing or incomplete.");
            result.passed = result.errors.Count == 0;
            return result;
        }

        private static RendererCapture CaptureGrassRenderer(
            VegetationWorldRenderer renderer,
            GeneratedVegetationGroup group)
        {
            var capture = new RendererCapture
            {
                scenePath = renderer.gameObject.scene.path,
                category = group.Category,
                fingerprint = group.Fingerprint,
                enabled = renderer.enabled,
                activeInHierarchy = renderer.gameObject.activeInHierarchy,
                metadataBuildComplete = !renderer.MetadataBuildInProgress,
                sourceBatchCount = renderer.SourceBatchCount,
                readyBatchCount = renderer.GpuBatchCount,
                residentGpuBuffers = renderer.ResidentGpuBufferCount,
                residentInstances = renderer.ResidentInstanceCount,
                uploadedInstances = renderer.UploadedInstanceCount,
                cameraRenderCallbackCount =
                    renderer.CameraRenderCallbackCount,
                uploadCpuMilliseconds = renderer.UploadCpuMilliseconds
            };
            capture.intentionallyDeferred = GrassResidencyStateValid(
                    capture.sourceBatchCount,
                    capture.readyBatchCount,
                    capture.residentGpuBuffers,
                    capture.residentInstances,
                    capture.uploadedInstances) &&
                capture.readyBatchCount == 0;
            capture.passed = capture.enabled &&
                capture.activeInHierarchy &&
                capture.metadataBuildComplete &&
                capture.category == "GrassCoverage" &&
                !string.IsNullOrWhiteSpace(capture.fingerprint) &&
                capture.sourceBatchCount > 0 &&
                GrassResidencyStateValid(
                    capture.sourceBatchCount,
                    capture.readyBatchCount,
                    capture.residentGpuBuffers,
                    capture.residentInstances,
                    capture.uploadedInstances) &&
                capture.cameraRenderCallbackCount > 0;
            return capture;
        }

        private static PackedRendererCapture CapturePackedRenderer(
            PackedWoodyCellRenderer renderer,
            GeneratedVegetationGroup group)
        {
            PackedWoodyCellAsset asset = renderer.CellAsset;
            var capture = new PackedRendererCapture
            {
                scenePath = renderer.gameObject.scene.path,
                groupCategory = group.Category,
                groupFingerprint = group.Fingerprint,
                category = asset != null
                    ? asset.Category.ToString()
                    : "missing",
                cellId = asset != null ? asset.CellId : string.Empty,
                assetFingerprint = asset != null
                    ? asset.PlanFingerprint
                    : string.Empty,
                generatorId = asset != null
                    ? asset.GeneratorId
                    : string.Empty,
                presentationVersion = asset != null
                    ? asset.PresentationVersion
                    : string.Empty,
                enabled = renderer.enabled,
                activeInHierarchy = renderer.gameObject.activeInHierarchy,
                sourceBatchCount = renderer.SourceBatchCount,
                sourceInstanceCount = renderer.SourceInstanceCount,
                placementMetadataCount = asset != null
                    ? asset.InstanceCount : 0,
                cameraRenderCallbackCount =
                    renderer.CameraRenderCallbackCount,
                visibleBatchCount = renderer.LastVisibleBatchCount,
                drawCallCount = renderer.LastDrawCallCount
            };
            if (asset != null)
            {
                capture.configurationErrors.AddRange(
                    asset.ValidateConfiguration());
                capture.assetPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrWhiteSpace(capture.assetPath) &&
                    File.Exists(ProjectPath(capture.assetPath)))
                {
                    capture.assetBytes = new FileInfo(
                        ProjectPath(capture.assetPath)).Length;
                    capture.assetSha256 = HashScene(capture.assetPath);
                }
            }
            capture.emptyCategory = PackedPopulationStateValid(
                    capture.sourceBatchCount,
                    capture.sourceInstanceCount,
                    capture.placementMetadataCount) &&
                capture.sourceInstanceCount == 0;
            capture.passed = capture.enabled &&
                capture.activeInHierarchy && asset != null &&
                capture.configurationErrors.Count == 0 &&
                PackedPopulationStateValid(
                    capture.sourceBatchCount,
                    capture.sourceInstanceCount,
                    capture.placementMetadataCount) &&
                capture.cameraRenderCallbackCount > 0 &&
                capture.groupFingerprint == capture.assetFingerprint &&
                !string.IsNullOrWhiteSpace(capture.assetFingerprint) &&
                capture.generatorId == VegetationGeneratorId &&
                capture.presentationVersion == PackedPresentationVersion &&
                capture.assetBytes > 0 &&
                capture.cellId == group.CellId &&
                CategoryMatches(capture.groupCategory, asset.Category);
            return capture;
        }

        private IEnumerator WaitForSceneOutputs(
            Scene scene,
            OutputSettlementReport result,
            PhaseReport timing)
        {
            var grass = new List<VegetationWorldRenderer>();
            var woody = new List<PackedWoodyCellRenderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                grass.AddRange(root.GetComponentsInChildren<
                    VegetationWorldRenderer>(true));
                woody.AddRange(root.GetComponentsInChildren<
                    PackedWoodyCellRenderer>(true));
            }
            long previousUploaded = -1;
            int quietFrames = 0;
            for (int frame = 0; frame < 600 && quietFrames < 12; frame++)
            {
                yield return ObserveFrames(timing, 1);
                result.observedFrames++;
                long uploaded = 0;
                bool metadataComplete = grass.Count == 1;
                foreach (VegetationWorldRenderer renderer in grass)
                {
                    if (renderer == null || renderer.MetadataBuildInProgress)
                        metadataComplete = false;
                    else
                        uploaded += renderer.UploadedInstanceCount;
                }
                quietFrames = metadataComplete &&
                    uploaded == previousUploaded
                        ? quietFrames + 1
                        : 0;
                previousUploaded = uploaded;
            }
            result.quietFrames = quietFrames;
            result.grassRendererCount = grass.Count;
            result.packedRendererCount = woody.Count;
            foreach (VegetationWorldRenderer renderer in grass)
            {
                if (renderer == null) continue;
                result.grassSourceBatchCount += renderer.SourceBatchCount;
                result.grassReadyBatchCount += renderer.GpuBatchCount;
                result.grassUploadedInstanceCount +=
                    renderer.UploadedInstanceCount;
                result.grassCameraRenderCallbackCount +=
                    renderer.CameraRenderCallbackCount;
            }
            foreach (PackedWoodyCellRenderer renderer in woody)
            {
                if (renderer == null) continue;
                result.packedSourceBatchCount += renderer.SourceBatchCount;
                result.packedSourceInstanceCount +=
                    renderer.SourceInstanceCount;
                result.packedCameraRenderCallbackCount +=
                    renderer.CameraRenderCallbackCount;
                result.packedDrawCallCount += renderer.LastDrawCallCount;
            }
            bool grassResidencyValid =
                result.grassReadyBatchCount > 0
                    ? result.grassUploadedInstanceCount > 0
                    : result.grassUploadedInstanceCount == 0;
            result.grassIntentionallyDeferred =
                result.grassReadyBatchCount == 0 &&
                result.grassUploadedInstanceCount == 0;
            result.settled = quietFrames >= 12 &&
                result.grassRendererCount == 1 &&
                result.packedRendererCount == 3 &&
                result.grassSourceBatchCount > 0 &&
                grassResidencyValid &&
                result.grassCameraRenderCallbackCount > 0 &&
                result.packedSourceBatchCount > 0 &&
                result.packedSourceInstanceCount > 0 &&
                result.packedCameraRenderCallbackCount > 0 &&
                result.packedDrawCallCount > 0;
        }

        [Test]
        public void Acceptance_AllowsDeferredGrassAndEmptyPackedCategories()
        {
            Assert.That(GrassResidencyStateValid(
                48, 0, 0, 0, 0), Is.True);
            Assert.That(GrassResidencyStateValid(
                48, 3, 6, 120, 120), Is.True);
            Assert.That(GrassResidencyStateValid(
                48, 0, 2, 0, 0), Is.False);
            Assert.That(GrassResidencyStateValid(
                48, 3, 6, 120, 0), Is.False);

            Assert.That(PackedPopulationStateValid(0, 0, 0), Is.True);
            Assert.That(PackedPopulationStateValid(12, 20, 20), Is.True);
            Assert.That(PackedPopulationStateValid(0, 20, 20), Is.False);
            Assert.That(PackedPopulationStateValid(12, 20, 19), Is.False);
        }

        private static bool GrassResidencyStateValid(
            int sourceBatchCount,
            int readyBatchCount,
            int residentGpuBuffers,
            long residentInstances,
            long uploadedInstances)
        {
            if (sourceBatchCount <= 0) return false;
            bool deferred = readyBatchCount == 0 &&
                residentGpuBuffers == 0 && residentInstances == 0 &&
                uploadedInstances == 0;
            bool resident = readyBatchCount > 0 &&
                residentGpuBuffers > 0 && residentInstances > 0 &&
                uploadedInstances == residentInstances;
            return deferred || resident;
        }

        private static bool PackedPopulationStateValid(
            int sourceBatchCount,
            int sourceInstanceCount,
            int placementMetadataCount)
        {
            bool empty = sourceBatchCount == 0 &&
                sourceInstanceCount == 0 && placementMetadataCount == 0;
            bool populated = sourceBatchCount > 0 &&
                sourceInstanceCount > 0 &&
                placementMetadataCount == sourceInstanceCount;
            return empty || populated;
        }

        private static bool KnownWoodyGroupCategory(string category) =>
            category == "OriginalTrees" ||
            category == "BoundaryForest" ||
            category == "ShrubsAndUndergrowth";

        private static bool CategoryMatches(
            string groupCategory,
            PackedWoodyCategory category) =>
            groupCategory == "OriginalTrees" &&
            category == PackedWoodyCategory.OriginalTree ||
            groupCategory == "BoundaryForest" &&
            category == PackedWoodyCategory.BoundaryForest ||
            groupCategory == "ShrubsAndUndergrowth" &&
            category == PackedWoodyCategory.ShrubOrUndergrowth;

        private static string ExpectedCellId(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            const string prefix = "World_";
            const string suffix = "_Vegetation";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
                !name.EndsWith(suffix, StringComparison.Ordinal))
                return string.Empty;
            return name.Substring(
                    prefix.Length,
                    name.Length - prefix.Length - suffix.Length)
                .ToLowerInvariant();
        }

        private static bool SceneLoaded(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool SequencePrefixEquals(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected,
            int count)
        {
            if (actual.Count != count || expected.Count < count) return false;
            for (int index = 0; index < count; index++)
                if (!string.Equals(actual[index], expected[index],
                        StringComparison.Ordinal))
                    return false;
            return true;
        }

        private static bool SequenceEquals(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected) =>
            SequencePrefixEquals(actual, expected, expected.Count);

        private static bool AllRendererCapturesPassed(
            IReadOnlyList<RendererCapture> captures)
        {
            if (captures.Count == 0) return false;
            for (int index = 0; index < captures.Count; index++)
                if (!captures[index].passed) return false;
            return true;
        }

        private static bool AllPackedRendererCapturesPassed(
            IReadOnlyList<PackedRendererCapture> captures)
        {
            if (captures.Count == 0) return false;
            for (int index = 0; index < captures.Count; index++)
                if (!captures[index].passed) return false;
            return true;
        }

        private static bool AllSavedSceneCapturesPassed(
            IReadOnlyList<SavedSceneAcceptanceCapture> captures)
        {
            if (captures.Count == 0) return false;
            for (int index = 0; index < captures.Count; index++)
                if (!captures[index].passed) return false;
            return true;
        }

        private static bool AllProductionStepsPassed(
            IReadOnlyList<ProductionServiceStepReport> steps)
        {
            if (steps.Count == 0) return false;
            for (int index = 0; index < steps.Count; index++)
                if (!steps[index].functionalPassed) return false;
            return true;
        }

        private IEnumerator ObserveOperation(PhaseReport phase, AsyncOperation operation, long started)
        {
            long previous = started;
            do
            {
                yield return null;
                Sample(phase, previous);
                previous = Stopwatch.GetTimestamp();
                Assert.That(ElapsedMilliseconds(started), Is.LessThan(180000d), "Scene operation exceeded the benchmark's three-minute timeout: " + phase.scenePath);
            } while (!operation.isDone);
            phase.asyncWallMilliseconds = ElapsedMilliseconds(started);
            phase.completedFrame = Time.frameCount;
            phase.operationCompletionSampleCount = phase.observedYieldIntervalsMilliseconds.Count;
        }

        private IEnumerator ObserveFrames(PhaseReport phase, int count)
        {
            long previous = Stopwatch.GetTimestamp();
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
                Sample(phase, previous);
                previous = Stopwatch.GetTimestamp();
            }
        }

        private void Sample(PhaseReport phase, long previous)
        {
            phase.observedYieldIntervalsMilliseconds.Add(ElapsedMilliseconds(previous));
            if (mainThreadRecorder.Valid && mainThreadRecorder.Count > 0 && mainThreadRecorder.LastValue > 0)
                phase.mainThreadMilliseconds.Add(mainThreadRecorder.LastValue / 1000000d);
            VegetationUploadFrameStatistics frame = VegetationWorldRenderer.AutomaticUploadFrameStatistics;
            // beginCameraRendering belongs to the previous completed render frame here.
            if (frame.FrameIndex >= Time.frameCount - 1 && frame.FrameIndex <= Time.frameCount)
            {
                phase.maximumUploadedInstancesInFrame = Math.Max(phase.maximumUploadedInstancesInFrame, frame.UploadedInstances);
                phase.maximumUploadOperationsInFrame = Math.Max(phase.maximumUploadOperationsInFrame, frame.UploadOperations);
                phase.maximumUploadCpuMillisecondsInFrame = Math.Max(phase.maximumUploadCpuMillisecondsInFrame, frame.CpuMilliseconds);
            }
        }

        private void CompletePhase(PhaseReport phase)
        {
            foreach (NativeRecorder recorder in nativeRecorders) phase.nativeMarkers.Add(recorder.Capture());
            phase.sampleCount = phase.observedYieldIntervalsMilliseconds.Count;
            phase.maximumObservedYieldIntervalMilliseconds = Maximum(phase.observedYieldIntervalsMilliseconds);
            phase.p95ObservedYieldIntervalMilliseconds = Percentile95(phase.observedYieldIntervalsMilliseconds);
            phase.mainThreadRecorderAvailable = phase.mainThreadMilliseconds.Count > 0;
            phase.maximumMainThreadMilliseconds = Maximum(phase.mainThreadMilliseconds);
            phase.p95MainThreadMilliseconds = Percentile95(phase.mainThreadMilliseconds);
        }

        private static double Maximum(List<double> samples)
        {
            double maximum = 0;
            foreach (double sample in samples) maximum = Math.Max(maximum, sample);
            return maximum;
        }

        private static double Percentile95(List<double> samples)
        {
            if (samples.Count == 0) return 0;
            double[] sorted = samples.ToArray();
            Array.Sort(sorted);
            return sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .95d) - 1)];
        }

        private void DiscoverNativeMarkers(
            List<NativeMarkerDescription> discoveredNativeMarkers,
            int passIndex)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = new List<ProfilerRecorderDescription>();
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.UnitType.ToString() == "TimeNanoseconds" && NativeMarkerPriority(description) < 10)
                    descriptions.Add(description);
            }
            descriptions.Sort((a, b) =>
            {
                int priority = NativeMarkerPriority(a).CompareTo(NativeMarkerPriority(b));
                return priority != 0 ? priority : StringComparer.Ordinal.Compare(a.Category.Name + "/" + a.Name, b.Category.Name + "/" + b.Name);
            });
            foreach (ProfilerRecorderDescription description in descriptions)
            {
                string key = description.Category.Name + "/" + description.Name;
                if (!nativeRecorderKeys.Add(key)) continue;
                var entry = new NativeMarkerDescription { name = description.Name, category = description.Category.Name,
                    unit = description.UnitType.ToString(), discoveredBeforePass = passIndex };
                discoveredNativeMarkers.Add(entry);
                if (nativeRecorders.Count >= 128) { entry.failure = "64-marker capture cap; discovered but not recorded"; continue; }
                try
                {
                    nativeRecorders.Add(new NativeRecorder(description, false));
                    nativeRecorders.Add(new NativeRecorder(description, true));
                    entry.selected = true;
                }
                catch (Exception exception) { entry.failure = exception.GetType().Name + ": " + exception.Message; }
            }
        }

        // These names were observed in the prior GetAvailable report, not assumed
        // to exist: selection still only runs against enumerated valid handles.
        private static readonly HashSet<string> primaryLoadingMarkers = new HashSet<string>(StringComparer.Ordinal)
        {
            "LoadSceneOperation", "LoadSceneOperation.CompleteAwakeSequence", "GameObject.ActivateAwakeRecursively",
            "Loading.AwakeFromLoad", "Loading.ReadObject", "Loading.ReadObjectThreaded", "ReadObjectFromSerializedFile",
            "Loading.RegisterAndAwakeAssetsTimeSliced", "AwakeScriptedObjects", "IntegrateAllThreadedObjects",
            "Application.LoadLevelAsync Integrate", "Application.Integrate Assets in Background"
        };

        private static readonly HashSet<string> primaryResourceMarkers = new HashSet<string>(StringComparer.Ordinal)
        {
            "Render/CreateGpuProgram", "Render/Shader.CompileGPUProgram", "Render/Shader.CreateGPUProgram",
            "Render/Shader.DynamicLoadGPUProgram", "Render/Shader.EditorCompileVariant", "Render/Shader.EditorLoadVariant",
            "Render/Gfx.UploadTexture", "Render/Gfx.UploadTextureData", "Render/Mesh.CreateMesh",
            "Physics/Physics.SyncTransforms", "Physics/Physics.CalculateChangedColliderTransforms",
            "Physics/Physics.HandleColliderHierarchyChanges", "Physics/Physics.WriteColliderPoses"
        };

        private static int NativeMarkerPriority(ProfilerRecorderDescription description)
        {
            string category = description.Category.Name, name = description.Name;
            if (name == "Main Thread") return 0;
            if (category == "Loading" && primaryLoadingMarkers.Contains(name)) return 0;
            if (primaryResourceMarkers.Contains(category + "/" + name) || name.StartsWith("MSC.Vegetation.", StringComparison.Ordinal)) return 1;
            if (category != "Loading") return 10;
            if (name.StartsWith("AssetBundle.", StringComparison.Ordinal) || name.IndexOf("BackupRestoration", StringComparison.OrdinalIgnoreCase) >= 0) return 10;
            if (name.StartsWith("Loading.", StringComparison.Ordinal) || name.StartsWith("LoadScene", StringComparison.Ordinal)) return 2;
            if (name.IndexOf("Awake", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("ReadObject", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Integrat", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("LoadScene", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            return 10;
        }

        private void ResetNativeRecorders()
        {
            foreach (NativeRecorder recorder in nativeRecorders) recorder.Reset();
        }

        [Serializable]
        private sealed class NativeMarkerDescription
        {
            public string name, category, unit, failure;
            public int discoveredBeforePass;
            public bool selected;
        }

        [Serializable]
        private sealed class NativeMarkerCapture
        {
            public string name, category, threadScope;
            public bool recorderValid, recorderRunning, positiveSamplesAvailable, bufferFilled;
            public int recordedSampleCount, positiveSampleCount;
            public long markerInvocationCount;
            public double sumRecordedMilliseconds, maximumRecordedFrameMilliseconds;
        }

        private sealed class NativeRecorder : IDisposable
        {
            private readonly ProfilerRecorder recorder;
            private readonly string name, category, threadScope;
            private readonly List<ProfilerRecorderSample> samples = new List<ProfilerRecorderSample>(2048);

            public NativeRecorder(ProfilerRecorderDescription description, bool mainThreadOnly)
            {
                name = description.Name; category = description.Category.Name;
                threadScope = mainThreadOnly ? "current-main-thread" : "all-threads";
                ProfilerRecorderOptions options = ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately;
                if (mainThreadOnly) options |= ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                recorder = ProfilerRecorder.StartNew(description.Category, description.Name, 2048, options);
            }

            public void Reset() { if (recorder.Valid) { recorder.Reset(); recorder.Start(); } }

            public NativeMarkerCapture Capture()
            {
                var capture = new NativeMarkerCapture { name = name, category = category, threadScope = threadScope, recorderValid = recorder.Valid, recorderRunning = recorder.Valid && recorder.IsRunning };
                if (!recorder.Valid) return capture;
                samples.Clear(); recorder.CopyTo(samples);
                capture.recordedSampleCount = samples.Count;
                capture.bufferFilled = samples.Count >= 2048;
                foreach (ProfilerRecorderSample sample in samples)
                {
                    if (sample.Value <= 0) continue;
                    capture.positiveSampleCount++;
                    capture.markerInvocationCount += sample.Count;
                    double milliseconds = sample.Value / 1000000d;
                    capture.sumRecordedMilliseconds += milliseconds;
                    capture.maximumRecordedFrameMilliseconds = Math.Max(capture.maximumRecordedFrameMilliseconds, milliseconds);
                }
                capture.positiveSamplesAvailable = capture.positiveSampleCount > 0;
                return capture;
            }

            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }

        private void CaptureBuffers(VegetationWorldRenderer renderer)
        {
            var batches = (IList)typeof(VegetationWorldRenderer).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            foreach (object batch in batches)
            foreach (string property in new[] { "InstanceBuffer", "Arguments" })
            {
                var buffer = (GraphicsBuffer)batch.GetType().GetProperty(property).GetValue(batch);
                if (buffer != null) capturedBuffers.Add(buffer);
            }
        }

        private void CreateCamera(Vector3 position)
        {
            cameraOwner = new GameObject("Vegetation scene activation benchmark camera");
            Camera camera = cameraOwner.AddComponent<Camera>();
            camera.enabled = false;
            cameraOwner.AddComponent<HDAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(position, Quaternion.identity);
            camera.fieldOfView = 90f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1500f;
            camera.cullingMask = ~0;
            target = new RenderTexture(256, 256, 24);
            target.Create();
            camera.targetTexture = target;
            camera.enabled = true;
        }

        private static double ElapsedMilliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (inFlight != null) yield return inFlight;
            inFlight = null;
            if (streamingService != null)
            {
                streamingService.enabled = false;
                yield return streamingService.UnloadOwnedScenes();
            }
            foreach (string path in ownedScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload != null) yield return unload;
                }
            }
            if (cameraOwner != null)
            {
                Camera camera = cameraOwner.GetComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = null;
                Object.Destroy(cameraOwner);
            }
            yield return null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (streamingServiceOwner != null)
                Object.Destroy(streamingServiceOwner);
            if (streamingManifest != null)
                Object.Destroy(streamingManifest);
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
            mainThreadRecorder = default;
            foreach (NativeRecorder recorder in nativeRecorders) recorder.Dispose();
            nativeRecorders.Clear(); nativeRecorderKeys.Clear();
            ownedScenePaths.Clear(); renderers.Clear();
            packedWoodyRenderers.Clear(); capturedBuffers.Clear();
            cameraOwner = null; target = null;
            streamingServiceOwner = null;
            streamingService = null;
            streamingManifest = null;
        }

        [Serializable]
        private sealed class ActivationReport
        {
            public string schemaVersion, utc, unityVersion, graphicsDevice,
                backgroundLoadingPriority, method, timingNotes, limitations,
                mainThreadRecorderFailure, packedAssetBudgetPolicy;
            public string nativeMarkerNotes = "Enumerated GetAvailable handles only. TimeNanoseconds markers prioritized by observed scene activation/loading names and resource/physics categories, capped at 64; unrelated AI/audio/lightmapper markers are excluded. Reset stops collection, so every phase explicitly restarts recorders. Each selected marker records all-thread and current-main-thread scope separately; both sum calls within a frame. Nested or parallel marker totals must NOT be added together. Phases include three post-completion frame observations to drain recorder delay. Missing/zero samples are not proof of zero work. Markers first discovered for warm pass cannot diagnose first-pass cold work.";
            public bool passed, functionalPassed, measurementValid,
                performancePassed;
            public Vector3 cameraPosition, cameraEulerAngles;
            public int initialSceneCount, observedFrameSampleCount;
            public double maximumObservedYieldIntervalMilliseconds, p95ObservedYieldIntervalMilliseconds;
            public PhaseReport warmup;
            public BenchmarkPrerequisiteCapture prerequisite;
            public PerformanceThresholds thresholds;
            public PerformanceGateReport performance;
            public ComparisonBaseline comparisonBaseline;
            public ComparisonResult comparison;
            public List<SavedFileCapture> savedScenes =
                new List<SavedFileCapture>();
            public List<ActivationPassReport> passes = new List<ActivationPassReport>();
            public List<NativeMarkerDescription> discoveredNativeMarkers = new List<NativeMarkerDescription>();
            public List<PhaseReport> phases = new List<PhaseReport>();
        }

        [Serializable]
        private sealed class ActivationPassReport
        {
            public int passIndex;
            public string cacheDescription;
            public bool passed, functionalPassed, uploadsSettled;
            public int loadedRendererCount, readyBatchCountBeforeUnload, residentGpuBufferCountBeforeUnload, capturedGpuBufferCount;
            public int remainingLoadedSceneCount, remainingRendererCount, remainingValidGpuBufferCount, cameraRenderCallbackCount;
            public int loadedPackedWoodyRendererCount, remainingPackedWoodyRendererCount;
            public int packedWoodyBatchCount, packedWoodyInstanceCount,
                packedWoodyCameraRenderCallbackCount,
                packedWoodyVisibleBatchCount, packedWoodyDrawCallCount;
            public long residentInstanceCountBeforeUnload, uploadedInstanceCount;
            public double uploadCpuMilliseconds, maximumUploadStepCpuMilliseconds;
            public List<RendererCapture> renderers = new List<RendererCapture>();
            public List<PackedRendererCapture> packedWoodyRenderers =
                new List<PackedRendererCapture>();
            public List<SavedSceneAcceptanceCapture> savedScenes =
                new List<SavedSceneAcceptanceCapture>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class RendererCapture
        {
            public string scenePath, category, fingerprint;
            public bool passed, enabled, activeInHierarchy,
                metadataBuildComplete, intentionallyDeferred;
            public int sourceBatchCount, readyBatchCount, residentGpuBuffers,
                cameraRenderCallbackCount;
            public long residentInstances, uploadedInstances;
            public double uploadCpuMilliseconds;
        }

        [Serializable]
        private sealed class PackedRendererCapture
        {
            public string scenePath, category, groupCategory, cellId,
                groupFingerprint, assetFingerprint, generatorId,
                presentationVersion, assetPath, assetSha256;
            public bool passed, enabled, activeInHierarchy, emptyCategory;
            public long assetBytes;
            public int sourceBatchCount, sourceInstanceCount,
                placementMetadataCount,
                cameraRenderCallbackCount, visibleBatchCount, drawCallCount;
            public List<string> configurationErrors = new List<string>();
        }

        [Serializable]
        private sealed class SavedFileCapture
        {
            public string path, sha256;
            public int buildIndex;
            public long bytes;
        }

        [Serializable]
        private sealed class PreparedBenchmarkCell
        {
            public string cellId, scenePath, sceneSha256,
                currentSceneSha256, planFingerprint,
                generatedDependencyFingerprint,
                currentGeneratedDependencyFingerprint;
            public List<PreparedBenchmarkDependency> generatedDependencies =
                new List<PreparedBenchmarkDependency>();
            public List<PreparedBenchmarkDependency>
                currentGeneratedDependencies =
                    new List<PreparedBenchmarkDependency>();
        }

        [Serializable]
        private sealed class CurrentPreparedBenchmarkCell
        {
            public string cellId, scenePath,
                generatedDependencyFingerprint;
            public List<PreparedBenchmarkDependency> generatedDependencies =
                new List<PreparedBenchmarkDependency>();
        }

        [Serializable]
        private sealed class PreparedBenchmarkDependency
        {
            public string path, sha256;
            public long bytes;
        }

        [Serializable]
        private sealed class BenchmarkGateDocument
        {
            public string schemaVersion, utc, generatorId,
                packedPresentationVersion, settingsHash, sourceFingerprint,
                sourceDependencyFingerprint, projectPolicySignature;
            public bool passed;
            public List<PreparedBenchmarkCell> cells =
                new List<PreparedBenchmarkCell>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class CurrentBenchmarkPrerequisites
        {
            public string settingsHash, sourceDependencyFingerprint,
                projectPolicySignature;
            public List<CurrentPreparedBenchmarkCell> cells =
                new List<CurrentPreparedBenchmarkCell>();
        }

        [Serializable]
        private sealed class BenchmarkPrerequisiteCapture
        {
            public string gatePath, gateSha256, schemaVersion, generatedUtc,
                generatorId, packedPresentationVersion, settingsHash,
                currentSettingsHash, sourceFingerprint,
                sourceDependencyFingerprint,
                currentSourceDependencyFingerprint,
                projectPolicySignature,
                currentProjectPolicySignature,
                prerequisiteExecuteMethod, sourceFreshnessPolicy;
            public bool passed, gatePassed;
            public List<PreparedBenchmarkCell> cells =
                new List<PreparedBenchmarkCell>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class ComparisonBaseline
        {
            public string path, sha256, expectedSha256, comparisonScope,
                nonComparableEvidenceNote;
            public bool expectedHashMatches;
            public long bytes;
            public double coldCellOneMaximumMainThreadMilliseconds,
                warmCellOneMaximumMainThreadMilliseconds,
                worstMaximumMainThreadMilliseconds;
        }

        [Serializable]
        private sealed class ComparisonResult
        {
            public bool baselineHashVerified,
                old204To248MillisecondClassEliminated;
            public double baselineMaximumMainThreadMilliseconds,
                measuredMaximumMainThreadMilliseconds, reductionPercent;
        }

        [Serializable]
        private sealed class PerformanceThresholds
        {
            public double maximumMainThreadMilliseconds,
                maximumYieldIntervalMilliseconds,
                maximumP95YieldIntervalMilliseconds;
        }

        [Serializable]
        private sealed class PerformanceGateReport
        {
            public bool measurementValid, performancePassed;
            public int expectedPhaseCount, measuredPhaseCount,
                observedYieldSampleCount, mainThreadSampleCount;
            public double maximumObservedYieldIntervalMilliseconds,
                p95ObservedYieldIntervalMilliseconds,
                maximumMainThreadMilliseconds,
                p95MainThreadMilliseconds;
            public PerformanceThresholds thresholds;
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class SavedSceneAcceptanceCapture
        {
            public string scenePath, expectedCellId, planFingerprint;
            public bool passed, packedAssetBudgetPassed;
            public int rootCount, generatedGroupCount, gameObjectCount,
                legacyPrefabInstanceGameObjects,
                directRendererComponents,
                serializedLodGroupComponents,
                serializedColliderComponents,
                runtimePooledColliderComponents,
                packedPlacementMetadataCount;
            public long packedAssetBytes, packedAssetByteBudget;
            public double packedAssetBytesPerPlacement;
            public List<RendererCapture> grassRenderers =
                new List<RendererCapture>();
            public List<PackedRendererCapture> packedRenderers =
                new List<PackedRendererCapture>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class OutputSettlementReport
        {
            public bool settled, grassIntentionallyDeferred;
            public int observedFrames, quietFrames, grassRendererCount,
                packedRendererCount, grassSourceBatchCount,
                grassReadyBatchCount, grassCameraRenderCallbackCount,
                packedSourceBatchCount, packedSourceInstanceCount,
                packedCameraRenderCallbackCount, packedDrawCallCount;
            public long grassUploadedInstanceCount;
        }

        [Serializable]
        private sealed class ProductionServiceStepReport
        {
            public int index, completedSceneLoadDelta,
                completedAutomaticRefreshDelta, startedLayerLoadCount,
                loadStartedFrame, activationPreparedFrame,
                loadCompletedFrame, unusedAssetCleanupCountDelta,
                ownedLoadedSceneCount;
            public string cellId, scenePath;
            public bool functionalPassed, loadedOrderExactSoFar,
                currentLayerLoaded, previousLayersUnloaded;
            public double loadWallMilliseconds,
                notificationCpuMilliseconds;
            public PhaseReport timing;
            public PhaseReport settlementTiming;
            public OutputSettlementReport outputSettlement;
            public SavedSceneAcceptanceCapture savedScene;
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class ProductionServiceCleanupReport
        {
            public bool passed, pendingUnusedAssetCleanup,
                unloadOrderExact;
            public int completedUnusedAssetCleanupCount,
                ownedLoadedSceneCount, remainingLoadedSceneCount,
                remainingRendererCount, remainingPackedRendererCount,
                remainingValidGpuBufferCount;
        }

        [Serializable]
        private sealed class ProductionServiceActivationReport
        {
            public string schemaVersion, utc, unityVersion, graphicsDevice,
                backgroundLoadingPriority, method, timingNotes, limitations,
                mainThreadRecorderFailure, packedAssetBudgetPolicy;
            public bool passed, functionalPassed, measurementValid,
                performancePassed;
            public int presentationAsyncOperationPriority;
            public Vector3 cameraStartPosition;
            public PhaseReport warmup;
            public BenchmarkPrerequisiteCapture prerequisite;
            public PerformanceThresholds thresholds;
            public PerformanceGateReport performance;
            public ProductionServiceCleanupReport cleanup =
                new ProductionServiceCleanupReport();
            public List<SavedFileCapture> savedScenes =
                new List<SavedFileCapture>();
            public List<string> manifestErrors = new List<string>();
            public List<string> loadedOrder = new List<string>();
            public List<int> loadedFrames = new List<int>();
            public List<string> unloadOrder = new List<string>();
            public List<int> unloadFrames = new List<int>();
            public List<ProductionServiceStepReport> steps =
                new List<ProductionServiceStepReport>();
            public List<NativeMarkerDescription> discoveredNativeMarkers =
                new List<NativeMarkerDescription>();
            public List<PhaseReport> phases = new List<PhaseReport>();
        }

        [Serializable]
        private sealed class PhaseReport
        {
            public string phase, scenePath;
            public int operationCompletionSampleCount;
            public int passIndex, buildIndex, startedFrame, completedFrame,
                rootCount, rendererCount, packedWoodyRendererCount,
                packedWoodySourceInstanceCount, sampleCount,
                maximumUploadedInstancesInFrame, maximumUploadOperationsInFrame;
            public double requestCallMilliseconds, asyncWallMilliseconds, maximumObservedYieldIntervalMilliseconds, p95ObservedYieldIntervalMilliseconds, maximumUploadCpuMillisecondsInFrame;
            public bool mainThreadRecorderAvailable;
            public double maximumMainThreadMilliseconds, p95MainThreadMilliseconds;
            public List<double> observedYieldIntervalsMilliseconds = new List<double>();
            public List<double> mainThreadMilliseconds = new List<double>();
            public List<NativeMarkerCapture> nativeMarkers = new List<NativeMarkerCapture>();
        }
    }
}
#endif
