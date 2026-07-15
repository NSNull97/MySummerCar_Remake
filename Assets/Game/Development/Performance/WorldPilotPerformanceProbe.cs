#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Development.Performance
{
    [Serializable]
    public struct WorldPilotSourceDirtyEntry
    {
        [SerializeField] private string porcelainStatus;
        [SerializeField] private string path;
        [SerializeField] private string sha256;

        public WorldPilotSourceDirtyEntry(string status, string repoRelativePath, string currentFileSha256)
        {
            porcelainStatus = status ?? string.Empty;
            path = repoRelativePath ?? string.Empty;
            sha256 = currentFileSha256 ?? string.Empty;
        }

        public string PorcelainStatus => porcelainStatus;
        public string Path => path;
        public string Sha256 => sha256;
    }

    /// <summary>
    /// Opt-in standalone capture for the bounded Milestone 05B.1 world pilot.
    /// The probe is injected into the build copy of Bootstrap and is inert unless
    /// -msc-05b1-capture is followed by a JSON destination.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldPilotPerformanceProbe : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        public const string CaptureArgument = "-msc-05b1-capture";

        private const int CameraTimeoutFrames = 900;
        private const int CellLoadTimeoutFrames = 1800;
        private const int WarmupFrameCount = 120;
        private const int SampleFrameCount = 300;
        private const float SixtyFramesPerSecondBudgetMilliseconds = 1000f / 60f;
        private const int ScreenshotSampleStridePixels = 8;
        private const int MinimumScreenshotColorBuckets = 6;
        private const float MaximumScreenshotDominantColorFraction = 0.985f;
        private const float MinimumScreenshotLuminanceRange = 0.05f;

        private static readonly CaptureLocationDefinition[] Locations =
        {
            new CaptureLocationDefinition(
                "pilot-home",
                CellRole.Pilot,
                new Vector3(153.495f, 1.10f, -1028.03f),
                new Vector3(0f, 180f, 0f),
                "GarageDoorLeft",
                targetNameIsPrefix: false),
            new CaptureLocationDefinition(
                "dense-vegetation",
                CellRole.Pilot,
                new Vector3(198.495f, 4f, -1068.23f),
                new Vector3(0f, 225f, 0f),
                "Spruce_",
                targetNameIsPrefix: true),
            new CaptureLocationDefinition(
                "interior-transition",
                CellRole.Pilot,
                new Vector3(138.545f, 1.15f, -1037.73f),
                new Vector3(0f, 180f, 0f),
                "LivingTable",
                targetNameIsPrefix: false),
            new CaptureLocationDefinition(
                "water-shoreline",
                CellRole.Next,
                new Vector3(177.5f, 1.2f, -925f),
                new Vector3(0f, 0f, 0f),
                "PierDeck",
                targetNameIsPrefix: false)
        };

        [SerializeField] private int pilotSceneBuildIndex = -1;
        [SerializeField] private int nextSceneBuildIndex = -1;
        [SerializeField] private string sourceRevision = "unavailable";
        [SerializeField] private string sourceWorkingTreeState = "unavailable";
        [SerializeField] private string[] sourceDirtyPaths = Array.Empty<string>();
        [SerializeField] private WorldPilotSourceDirtyEntry[] sourceDirtyEntries =
            Array.Empty<WorldPilotSourceDirtyEntry>();
        [SerializeField] private string buildUtc = string.Empty;
        [SerializeField] private string[] enabledBuildScenePaths = Array.Empty<string>();

        private readonly FrameTiming[] latestFrameTiming = new FrameTiming[1];
        private readonly List<CounterRecorder> counters = new List<CounterRecorder>();

        private string capturePath = string.Empty;
        private Camera playerCamera;
        private CharacterController playerController;
        private RenderTexture verificationTarget;
        private RenderPipeline.StandardRequest verificationRenderRequest;
        private Quaternion heldCameraRotation = Quaternion.identity;
        private bool hasHeldCameraRotation;
        private bool captureCompleted;

        public void Configure(
            int pilotBuildIndex,
            int nextBuildIndex,
            string revision,
            string workingTreeState,
            string[] dirtyPaths,
            WorldPilotSourceDirtyEntry[] dirtyEntries,
            string buildTimestampUtc,
            string[] buildScenePaths)
        {
            pilotSceneBuildIndex = pilotBuildIndex;
            nextSceneBuildIndex = nextBuildIndex;
            sourceRevision = string.IsNullOrWhiteSpace(revision) ? "unavailable" : revision;
            sourceWorkingTreeState = string.IsNullOrWhiteSpace(workingTreeState)
                ? "unavailable"
                : workingTreeState;
            sourceDirtyPaths = dirtyPaths ?? Array.Empty<string>();
            sourceDirtyEntries = dirtyEntries ?? Array.Empty<WorldPilotSourceDirtyEntry>();
            buildUtc = buildTimestampUtc ?? string.Empty;
            enabledBuildScenePaths = buildScenePaths ?? Array.Empty<string>();
        }

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
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            StartCounters();
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            for (int frame = 0; frame < CameraTimeoutFrames; frame++)
            {
                playerCamera = Camera.main;
                if (playerCamera != null)
                {
                    playerController = playerCamera.GetComponentInParent<CharacterController>();
                    if (playerController != null)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (playerCamera == null || playerController == null)
            {
                WriteFailureAndQuit("M4 Main Camera/CharacterController was not available before timeout.");
                yield break;
            }

            if (!TryCreateVerificationTarget(out string targetFailure))
            {
                WriteFailureAndQuit(targetFailure);
                yield break;
            }

            var capture = CreateCaptureEnvelope();
            var locationResults = new List<LocationCapture>(Locations.Length);
            for (int index = 0; index < Locations.Length; index++)
            {
                CaptureLocationDefinition location = Locations[index];
                int expectedBuildIndex = location.Cell == CellRole.Pilot
                    ? pilotSceneBuildIndex
                    : nextSceneBuildIndex;
                if (expectedBuildIndex < 0)
                {
                    WriteFailureAndQuit("A production cell build index was not configured.");
                    yield break;
                }

                LocationCapture result = CreateLocationResult(location, expectedBuildIndex);
                yield return MoveAndWaitForCell(location, expectedBuildIndex, result);
                if (!result.expectedCellLoaded)
                {
                    WriteFailureAndQuit(
                        $"Scene build index {expectedBuildIndex} did not load for {location.Id} before timeout.");
                    yield break;
                }

                if (!TryAimAtRepresentativeGeometry(location, expectedBuildIndex, result, out string aimFailure))
                {
                    locationResults.Add(result);
                    WriteFailureAndQuit(aimFailure, locationResults);
                    yield break;
                }

                yield return SampleLocation(result);
                if (!TryValidateRenderingMetrics(result, out string renderingFailure))
                {
                    locationResults.Add(result);
                    WriteFailureAndQuit(renderingFailure, locationResults);
                    yield break;
                }

                yield return WriteScreenshot(result);
                if (!result.screenshotContentValid)
                {
                    locationResults.Add(result);
                    WriteFailureAndQuit(
                        $"Verification screenshot for {location.Id} was visually blank or near-uniform: " +
                        $"buckets={result.screenshotDistinctColorBuckets}, " +
                        $"dominant={result.screenshotDominantColorFraction:F4}, " +
                        $"luminanceRange={result.screenshotLuminanceRange:F4}.",
                        locationResults);
                    yield break;
                }

                locationResults.Add(result);
            }

            var screenshotSignatures = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < locationResults.Count; index++)
            {
                screenshotSignatures.Add(locationResults[index].screenshotSampleSignature);
            }

            if (screenshotSignatures.Count != locationResults.Count)
            {
                WriteFailureAndQuit(
                    "Representative screenshots are not distinct across all four capture locations.",
                    locationResults);
                yield break;
            }

            capture.completed = true;
            capture.failure = string.Empty;
            capture.capturedUtc = DateTime.UtcNow.ToString("O");
            capture.locations = locationResults.ToArray();
            capture.unavailableScope = new[]
            {
                "road-driving-speed: no validated production driving route exists",
                "vertical-slice-route: no production service destination/continuous route exists",
                "GPU VRAM usage: device capacity is recorded, but resident/allocated usage is unavailable",
                "Present cost is not isolated from ordinary backbuffer frame timing"
            };
            WriteJson(capture);
            captureCompleted = true;
            Debug.Log("M05B1_WORLD_PERFORMANCE_CAPTURE_OK path=" + Path.GetFullPath(capturePath));
            Application.Quit(0);
        }

        private IEnumerator MoveAndWaitForCell(
            CaptureLocationDefinition location,
            int expectedBuildIndex,
            LocationCapture result)
        {
            result.sceneWasLoadedBeforeMove = IsSceneLoaded(expectedBuildIndex);
            bool controllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
            playerController.transform.SetPositionAndRotation(
                location.Position,
                Quaternion.Euler(location.EulerAngles));
            playerController.enabled = controllerWasEnabled;
            Physics.SyncTransforms();

            float start = Time.realtimeSinceStartup;
            float maximumFrameMilliseconds = 0f;
            int frameCount = 0;
            for (; frameCount < CellLoadTimeoutFrames; frameCount++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                maximumFrameMilliseconds = Mathf.Max(
                    maximumFrameMilliseconds,
                    Time.unscaledDeltaTime * 1000f);
                if (IsSceneLoaded(expectedBuildIndex))
                {
                    break;
                }
            }

            result.expectedCellLoaded = IsSceneLoaded(expectedBuildIndex);
            result.loadTransitionFrames = frameCount + 1;
            result.loadTransitionMilliseconds = (Time.realtimeSinceStartup - start) * 1000f;
            result.loadTransitionMaximumFrameTimeMilliseconds = maximumFrameMilliseconds;
            result.loadedSceneBuildIndicesAfterMove = GetLoadedSceneBuildIndices();
        }

        private bool TryAimAtRepresentativeGeometry(
            CaptureLocationDefinition location,
            int expectedBuildIndex,
            LocationCapture result,
            out string failure)
        {
            failure = string.Empty;
            Scene scene = SceneManager.GetSceneByBuildIndex(expectedBuildIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                failure = $"Cannot aim {location.Id}: scene build index {expectedBuildIndex} is not loaded.";
                return false;
            }

            Transform selectedTarget = null;
            float nearestSquaredDistance = float.PositiveInfinity;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(includeInactive: false);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform candidate = transforms[transformIndex];
                    bool nameMatches = location.TargetNameIsPrefix
                        ? candidate.name.StartsWith(location.TargetObjectName, StringComparison.Ordinal)
                        : string.Equals(candidate.name, location.TargetObjectName, StringComparison.Ordinal);
                    if (!nameMatches)
                    {
                        continue;
                    }

                    float squaredDistance = (candidate.position - playerCamera.transform.position).sqrMagnitude;
                    if (squaredDistance < nearestSquaredDistance)
                    {
                        selectedTarget = candidate;
                        nearestSquaredDistance = squaredDistance;
                    }
                }
            }

            if (selectedTarget == null)
            {
                failure = $"Cannot aim {location.Id}: representative object " +
                          $"'{location.TargetObjectName}' was not found in scene build index {expectedBuildIndex}.";
                return false;
            }

            Renderer[] renderers = selectedTarget.GetComponentsInChildren<Renderer>(includeInactive: false);
            Bounds targetBounds = default;
            int rendererCount = 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (rendererCount == 0)
                {
                    targetBounds = renderer.bounds;
                }
                else
                {
                    targetBounds.Encapsulate(renderer.bounds);
                }

                rendererCount++;
            }

            if (rendererCount == 0)
            {
                failure = $"Cannot aim {location.Id}: representative object '{selectedTarget.name}' " +
                          "has no enabled renderers.";
                return false;
            }

            Vector3 cameraPosition = playerCamera.transform.position;
            Vector3 targetPoint = targetBounds.center;
            Vector3 lookDirection = targetPoint - cameraPosition;
            if (lookDirection.sqrMagnitude < 0.25f)
            {
                failure = $"Cannot aim {location.Id}: representative object '{selectedTarget.name}' " +
                          "is too close to the player camera.";
                return false;
            }

            heldCameraRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            hasHeldCameraRotation = true;
            HoldCameraPose();
            result.cameraTargetObject = selectedTarget.name;
            result.cameraTargetRendererCount = rendererCount;
            result.cameraTargetWorldPosition = targetPoint;
            result.actualCameraPosition = cameraPosition;
            result.actualCameraForward = playerCamera.transform.forward;
            return true;
        }

        private IEnumerator SampleLocation(LocationCapture result)
        {
            for (int frame = 0; frame < WarmupFrameCount; frame++)
            {
                HoldCameraPose();
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }

            ResetCounterSamples();
            var frameTimes = new List<float>(SampleFrameCount);
            var cpuTimes = new List<float>(SampleFrameCount);
            var gpuTimes = new List<float>(SampleFrameCount);
            for (int frame = 0; frame < SampleFrameCount; frame++)
            {
                HoldCameraPose();
                FrameTimingManager.CaptureFrameTimings();
                yield return null;

                float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
                uint timingCount = FrameTimingManager.GetLatestTimings(1, latestFrameTiming);
                if (timingCount > 0)
                {
                    float cpuMilliseconds = (float)latestFrameTiming[0].cpuFrameTime;
                    float gpuMilliseconds = (float)latestFrameTiming[0].gpuFrameTime;
                    if (cpuMilliseconds > 0f)
                    {
                        cpuTimes.Add(cpuMilliseconds);
                        frameMilliseconds = Mathf.Max(frameMilliseconds, cpuMilliseconds);
                    }

                    if (gpuMilliseconds > 0f)
                    {
                        gpuTimes.Add(gpuMilliseconds);
                        frameMilliseconds = Mathf.Max(frameMilliseconds, gpuMilliseconds);
                    }
                }

                frameTimes.Add(frameMilliseconds);
                SampleCounters();
            }

            frameTimes.Sort();
            cpuTimes.Sort();
            gpuTimes.Sort();
            result.actualPlayerPosition = playerController.transform.position;
            result.actualWidth = Screen.width;
            result.actualHeight = Screen.height;
            result.warmupFrames = WarmupFrameCount;
            result.sampledFrames = SampleFrameCount;
            result.meanFrameTimeMilliseconds = Mean(frameTimes);
            result.medianFrameTimeMilliseconds = Percentile(frameTimes, 0.5f);
            result.percentile95FrameTimeMilliseconds = Percentile(frameTimes, 0.95f);
            result.percentile99FrameTimeMilliseconds = Percentile(frameTimes, 0.99f);
            result.worstFrameTimeMilliseconds = Percentile(frameTimes, 1f);
            result.estimatedMeanFramesPerSecond = result.meanFrameTimeMilliseconds > 0.0001f
                ? 1000f / result.meanFrameTimeMilliseconds
                : 0f;
            result.cpuFrameTimingSamples = cpuTimes.Count;
            result.meanCpuFrameTimeMilliseconds = Mean(cpuTimes);
            result.percentile95CpuFrameTimeMilliseconds = Percentile(cpuTimes, 0.95f);
            result.gpuFrameTimingSamples = gpuTimes.Count;
            result.meanGpuFrameTimeMilliseconds = Mean(gpuTimes);
            result.percentile95GpuFrameTimeMilliseconds = Percentile(gpuTimes, 0.95f);
            result.percentile95Within60FpsBudget =
                result.percentile95FrameTimeMilliseconds <= SixtyFramesPerSecondBudgetMilliseconds;
            result.profilerCounters = BuildCounterResults();
            result.unavailableMetrics = BuildUnavailableMetrics(result);
            result.actualCameraPosition = playerCamera.transform.position;
            result.actualCameraForward = playerCamera.transform.forward;
        }

        private IEnumerator WriteScreenshot(LocationCapture result)
        {
            HoldCameraPose();
            yield return new WaitForEndOfFrame();
            string fullCapturePath = Path.GetFullPath(capturePath);
            string directory = Path.GetDirectoryName(fullCapturePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Performance capture path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            string filename = Path.GetFileNameWithoutExtension(fullCapturePath) +
                              "_" + result.locationId + ".png";
            string screenshotPath = Path.Combine(directory, filename);
            RenderPipeline.SubmitRenderRequest(playerCamera, verificationRenderRequest);
            var screenshot = new Texture2D(
                verificationTarget.width,
                verificationTarget.height,
                TextureFormat.RGB24,
                mipChain: false,
                linear: false);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = verificationTarget;
                screenshot.ReadPixels(
                    new Rect(0f, 0f, verificationTarget.width, verificationTarget.height),
                    0,
                    0,
                    recalculateMipMaps: false);
                screenshot.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                ScreenshotAnalysis analysis = AnalyzeScreenshot(screenshot);
                result.screenshotSampleCount = analysis.SampleCount;
                result.screenshotDistinctColorBuckets = analysis.DistinctColorBuckets;
                result.screenshotDominantColorFraction = analysis.DominantColorFraction;
                result.screenshotLuminanceRange = analysis.LuminanceRange;
                result.screenshotSampleSignature = analysis.SampleSignature;
                result.screenshotContentValid =
                    analysis.DistinctColorBuckets >= MinimumScreenshotColorBuckets &&
                    analysis.DominantColorFraction <= MaximumScreenshotDominantColorFraction &&
                    analysis.LuminanceRange >= MinimumScreenshotLuminanceRange;
                File.WriteAllBytes(screenshotPath, screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                Destroy(screenshot);
            }

            result.screenshotFile = filename;
            result.screenshotCaptureMethod = "hdrp-standard-request-rendertexture-argb32-srgb";
        }

        private bool TryCreateVerificationTarget(out string failure)
        {
            failure = string.Empty;
            verificationTarget = new RenderTexture(
                1920,
                1080,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "M05B1_WorldPilotVerification_1080p",
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!verificationTarget.Create())
            {
                failure = "Could not create the HDRP verification RenderTexture.";
                return false;
            }

            verificationRenderRequest = new RenderPipeline.StandardRequest
            {
                destination = verificationTarget,
                mipLevel = 0,
                slice = 0,
                face = CubemapFace.Unknown
            };
            if (!RenderPipeline.SupportsRenderRequest(playerCamera, verificationRenderRequest))
            {
                failure = "The active HDRP does not support StandardRequest verification capture.";
                return false;
            }

            return true;
        }

        private static ScreenshotAnalysis AnalyzeScreenshot(Texture2D screenshot)
        {
            Color32[] pixels = screenshot.GetPixels32();
            int minimumX = screenshot.width / 10;
            int maximumX = screenshot.width - minimumX;
            int minimumY = screenshot.height / 10;
            int maximumY = screenshot.height - minimumY;
            var bucketCounts = new Dictionary<int, int>();
            int sampleCount = 0;
            int dominantBucketCount = 0;
            byte minimumLuminance = byte.MaxValue;
            byte maximumLuminance = byte.MinValue;
            ulong signature = 14695981039346656037UL;

            for (int y = minimumY; y < maximumY; y += ScreenshotSampleStridePixels)
            {
                int row = y * screenshot.width;
                for (int x = minimumX; x < maximumX; x += ScreenshotSampleStridePixels)
                {
                    Color32 pixel = pixels[row + x];
                    int bucket = ((pixel.r >> 4) << 8) | ((pixel.g >> 4) << 4) | (pixel.b >> 4);
                    bucketCounts.TryGetValue(bucket, out int count);
                    count++;
                    bucketCounts[bucket] = count;
                    dominantBucketCount = Math.Max(dominantBucketCount, count);

                    byte luminance = (byte)((54 * pixel.r + 183 * pixel.g + 19 * pixel.b) >> 8);
                    minimumLuminance = Math.Min(minimumLuminance, luminance);
                    maximumLuminance = Math.Max(maximumLuminance, luminance);
                    signature ^= pixel.r;
                    signature *= 1099511628211UL;
                    signature ^= pixel.g;
                    signature *= 1099511628211UL;
                    signature ^= pixel.b;
                    signature *= 1099511628211UL;
                    sampleCount++;
                }
            }

            return new ScreenshotAnalysis(
                sampleCount,
                bucketCounts.Count,
                sampleCount > 0 ? dominantBucketCount / (float)sampleCount : 1f,
                sampleCount > 0 ? (maximumLuminance - minimumLuminance) / 255f : 0f,
                signature.ToString("x16"));
        }

        private void HoldCameraPose()
        {
            if (hasHeldCameraRotation && playerCamera != null)
            {
                playerCamera.transform.rotation = heldCameraRotation;
            }
        }

        private CaptureEnvelope CreateCaptureEnvelope()
        {
            return new CaptureEnvelope
            {
                schemaVersion = CurrentSchemaVersion,
                captureId = "m05b1-bounded-world-pilot-1080p",
                milestone = "05B.1",
                completed = false,
                buildUtc = buildUtc,
                sourceRevision = sourceRevision,
                sourceWorkingTreeState = sourceWorkingTreeState,
                sourceDirtyPaths = sourceDirtyPaths ?? Array.Empty<string>(),
                sourceDirtyEntries = sourceDirtyEntries ?? Array.Empty<WorldPilotSourceDirtyEntry>(),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMegabytes = SystemInfo.systemMemorySize,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsMemoryMegabytes = SystemInfo.graphicsMemorySize,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
                requestedWidth = 1920,
                requestedHeight = 1080,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                frameTimingStatsExpected = true,
                renderMethod = "ordinary-player-backbuffer",
                verificationScreenshotMethod =
                    "hdrp-standard-request-rendertexture-argb32-srgb-after-measured-frames",
                pilotSceneBuildIndex = pilotSceneBuildIndex,
                nextSceneBuildIndex = nextSceneBuildIndex,
                enabledBuildScenePaths = enabledBuildScenePaths ?? Array.Empty<string>()
            };
        }

        private LocationCapture CreateLocationResult(
            CaptureLocationDefinition location,
            int expectedBuildIndex)
        {
            return new LocationCapture
            {
                locationId = location.Id,
                expectedCell = location.Cell == CellRole.Pilot ? "cell_0_-3" : "cell_0_-2",
                expectedSceneBuildIndex = expectedBuildIndex,
                requestedPlayerPosition = location.Position,
                requestedPlayerEulerAngles = location.EulerAngles
            };
        }

        private void StartCounters()
        {
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Memory,
                "Total Used Memory",
                "bytes"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Memory,
                "Total Reserved Memory",
                "bytes"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Memory,
                "GC Used Memory",
                "bytes"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Render,
                "Draw Calls Count",
                "count"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Render,
                "Batches Count",
                "count"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Render,
                "SetPass Calls Count",
                "count"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Internal,
                "Main Thread",
                "nanoseconds"));
            counters.Add(CounterRecorder.TryCreate(
                ProfilerCategory.Physics,
                "Physics.Processing",
                "nanoseconds"));
        }

        private void ResetCounterSamples()
        {
            for (int index = 0; index < counters.Count; index++)
            {
                counters[index].ResetSamples();
            }
        }

        private void SampleCounters()
        {
            for (int index = 0; index < counters.Count; index++)
            {
                counters[index].Sample();
            }
        }

        private ProfilerCounterCapture[] BuildCounterResults()
        {
            var results = new ProfilerCounterCapture[counters.Count];
            for (int index = 0; index < counters.Count; index++)
            {
                results[index] = counters[index].BuildResult();
            }

            return results;
        }

        private string[] BuildUnavailableMetrics(LocationCapture result)
        {
            var unavailable = new List<string>();
            if (result.cpuFrameTimingSamples == 0)
            {
                unavailable.Add("CPU FrameTiming");
            }

            if (result.gpuFrameTimingSamples == 0)
            {
                unavailable.Add("GPU FrameTiming (driver/pipeline returned no positive samples)");
            }

            for (int index = 0; index < result.profilerCounters.Length; index++)
            {
                ProfilerCounterCapture counter = result.profilerCounters[index];
                if (!counter.available)
                {
                    unavailable.Add("ProfilerRecorder: " + counter.markerName);
                }
            }

            return unavailable.ToArray();
        }

        private static bool TryValidateRenderingMetrics(
            LocationCapture result,
            out string failure)
        {
            failure = string.Empty;
            if (result.gpuFrameTimingSamples <= 0 || result.meanGpuFrameTimeMilliseconds <= 0f)
            {
                failure = $"GPU FrameTiming returned no positive rendered samples for {result.locationId}.";
                return false;
            }

            string[] requiredCounters =
            {
                "Draw Calls Count",
                "Batches Count",
                "SetPass Calls Count"
            };
            for (int requiredIndex = 0; requiredIndex < requiredCounters.Length; requiredIndex++)
            {
                string requiredName = requiredCounters[requiredIndex];
                ProfilerCounterCapture found = null;
                for (int counterIndex = 0; counterIndex < result.profilerCounters.Length; counterIndex++)
                {
                    ProfilerCounterCapture candidate = result.profilerCounters[counterIndex];
                    if (string.Equals(candidate.markerName, requiredName, StringComparison.Ordinal))
                    {
                        found = candidate;
                        break;
                    }
                }

                if (found == null || !found.available || found.sampleCount <= 0 ||
                    found.mean <= 0d || found.peak <= 0d)
                {
                    failure = $"Required render counter '{requiredName}' was not positive for " +
                              $"{result.locationId}.";
                    return false;
                }
            }

            return true;
        }

        private void WriteFailureAndQuit(string failure)
        {
            WriteFailureAndQuit(failure, Array.Empty<LocationCapture>());
        }

        private void WriteFailureAndQuit(
            string failure,
            IReadOnlyList<LocationCapture> partialLocations)
        {
            CaptureEnvelope capture = CreateCaptureEnvelope();
            capture.completed = false;
            capture.failure = failure;
            capture.capturedUtc = DateTime.UtcNow.ToString("O");
            capture.locations = new LocationCapture[partialLocations.Count];
            for (int index = 0; index < partialLocations.Count; index++)
            {
                capture.locations[index] = partialLocations[index];
            }
            capture.unavailableScope = new[] { "Capture aborted before all bounded locations completed." };
            WriteJson(capture);
            captureCompleted = true;
            Debug.LogError("M05B1_WORLD_PERFORMANCE_CAPTURE_FAILED " + failure);
            Application.Quit(2);
        }

        private void WriteJson(CaptureEnvelope capture)
        {
            string fullPath = Path.GetFullPath(capturePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Performance capture path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(capture, true));
        }

        private static bool IsSceneLoaded(int buildIndex)
        {
            Scene scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            return scene.IsValid() && scene.isLoaded;
        }

        private static int[] GetLoadedSceneBuildIndices()
        {
            var indices = new List<int>(SceneManager.loadedSceneCount);
            for (int index = 0; index < SceneManager.loadedSceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene.buildIndex >= 0)
                {
                    indices.Add(scene.buildIndex);
                }
            }

            return indices.ToArray();
        }

        private static float Mean(IReadOnlyList<float> samples)
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

        private static float Percentile(IReadOnlyList<float> sortedSamples, float percentile)
        {
            if (sortedSamples.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(
                Mathf.CeilToInt(sortedSamples.Count * percentile) - 1,
                0,
                sortedSamples.Count - 1);
            return sortedSamples[index];
        }

        private static string ResolveCapturePath(IReadOnlyList<string> arguments)
        {
            for (int index = 0; index < arguments.Count - 1; index++)
            {
                if (string.Equals(arguments[index], CaptureArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < counters.Count; index++)
            {
                counters[index].Dispose();
            }

            counters.Clear();
            if (verificationTarget != null)
            {
                verificationTarget.Release();
                Destroy(verificationTarget);
                verificationTarget = null;
            }

            if (!captureCompleted && !string.IsNullOrWhiteSpace(capturePath))
            {
                Debug.LogWarning("M05B1 performance probe was destroyed before capture completion.");
            }
        }

        private enum CellRole
        {
            Pilot,
            Next
        }

        private readonly struct CaptureLocationDefinition
        {
            public CaptureLocationDefinition(
                string id,
                CellRole cell,
                Vector3 position,
                Vector3 eulerAngles,
                string targetObjectName,
                bool targetNameIsPrefix)
            {
                Id = id;
                Cell = cell;
                Position = position;
                EulerAngles = eulerAngles;
                TargetObjectName = targetObjectName;
                TargetNameIsPrefix = targetNameIsPrefix;
            }

            public string Id { get; }
            public CellRole Cell { get; }
            public Vector3 Position { get; }
            public Vector3 EulerAngles { get; }
            public string TargetObjectName { get; }
            public bool TargetNameIsPrefix { get; }
        }

        private readonly struct ScreenshotAnalysis
        {
            public ScreenshotAnalysis(
                int sampleCount,
                int distinctColorBuckets,
                float dominantColorFraction,
                float luminanceRange,
                string sampleSignature)
            {
                SampleCount = sampleCount;
                DistinctColorBuckets = distinctColorBuckets;
                DominantColorFraction = dominantColorFraction;
                LuminanceRange = luminanceRange;
                SampleSignature = sampleSignature;
            }

            public int SampleCount { get; }
            public int DistinctColorBuckets { get; }
            public float DominantColorFraction { get; }
            public float LuminanceRange { get; }
            public string SampleSignature { get; }
        }

        private sealed class CounterRecorder : IDisposable
        {
            private readonly ProfilerRecorder recorder;
            private readonly string markerName;
            private readonly string sourceUnit;
            private long total;
            private long maximum;
            private int samples;

            private CounterRecorder(
                ProfilerRecorder recorder,
                string markerName,
                string sourceUnit)
            {
                this.recorder = recorder;
                this.markerName = markerName;
                this.sourceUnit = sourceUnit;
            }

            public static CounterRecorder TryCreate(
                ProfilerCategory category,
                string markerName,
                string unit)
            {
                try
                {
                    return new CounterRecorder(
                        ProfilerRecorder.StartNew(category, markerName, 1),
                        markerName,
                        unit);
                }
                catch (Exception)
                {
                    return new CounterRecorder(default, markerName, unit);
                }
            }

            public void ResetSamples()
            {
                total = 0;
                maximum = 0;
                samples = 0;
            }

            public void Sample()
            {
                if (!recorder.Valid || recorder.Count == 0)
                {
                    return;
                }

                long value = recorder.LastValue;
                total += value;
                maximum = Math.Max(maximum, value);
                samples++;
            }

            public ProfilerCounterCapture BuildResult()
            {
                bool available = recorder.Valid && samples > 0;
                double divisor = string.Equals(sourceUnit, "nanoseconds", StringComparison.Ordinal)
                    ? 1_000_000d
                    : 1d;
                return new ProfilerCounterCapture
                {
                    markerName = markerName,
                    available = available,
                    unit = string.Equals(sourceUnit, "nanoseconds", StringComparison.Ordinal)
                        ? "milliseconds"
                        : sourceUnit,
                    sampleCount = samples,
                    mean = available ? total / (double)samples / divisor : 0d,
                    peak = available ? maximum / divisor : 0d
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
        private sealed class CaptureEnvelope
        {
            public int schemaVersion;
            public string captureId = string.Empty;
            public string milestone = string.Empty;
            public bool completed;
            public string failure = string.Empty;
            public string buildUtc = string.Empty;
            public string capturedUtc = string.Empty;
            public string sourceRevision = string.Empty;
            public string sourceWorkingTreeState = string.Empty;
            public string[] sourceDirtyPaths = Array.Empty<string>();
            public WorldPilotSourceDirtyEntry[] sourceDirtyEntries =
                Array.Empty<WorldPilotSourceDirtyEntry>();
            public string unityVersion = string.Empty;
            public string operatingSystem = string.Empty;
            public string processor = string.Empty;
            public int processorCount;
            public int systemMemoryMegabytes;
            public string graphicsDevice = string.Empty;
            public int graphicsMemoryMegabytes;
            public string graphicsApi = string.Empty;
            public string qualityLevel = string.Empty;
            public int requestedWidth;
            public int requestedHeight;
            public int vSyncCount;
            public int targetFrameRate;
            public bool frameTimingStatsExpected;
            public string renderMethod = string.Empty;
            public string verificationScreenshotMethod = string.Empty;
            public int pilotSceneBuildIndex;
            public int nextSceneBuildIndex;
            public string[] enabledBuildScenePaths = Array.Empty<string>();
            public LocationCapture[] locations = Array.Empty<LocationCapture>();
            public string[] unavailableScope = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LocationCapture
        {
            public string locationId = string.Empty;
            public string expectedCell = string.Empty;
            public int expectedSceneBuildIndex;
            public Vector3 requestedPlayerPosition;
            public Vector3 requestedPlayerEulerAngles;
            public Vector3 actualPlayerPosition;
            public string cameraTargetObject = string.Empty;
            public int cameraTargetRendererCount;
            public Vector3 cameraTargetWorldPosition;
            public Vector3 actualCameraPosition;
            public Vector3 actualCameraForward;
            public bool sceneWasLoadedBeforeMove;
            public bool expectedCellLoaded;
            public int loadTransitionFrames;
            public float loadTransitionMilliseconds;
            public float loadTransitionMaximumFrameTimeMilliseconds;
            public int[] loadedSceneBuildIndicesAfterMove = Array.Empty<int>();
            public int actualWidth;
            public int actualHeight;
            public int warmupFrames;
            public int sampledFrames;
            public float meanFrameTimeMilliseconds;
            public float medianFrameTimeMilliseconds;
            public float percentile95FrameTimeMilliseconds;
            public float percentile99FrameTimeMilliseconds;
            public float worstFrameTimeMilliseconds;
            public float estimatedMeanFramesPerSecond;
            public int cpuFrameTimingSamples;
            public float meanCpuFrameTimeMilliseconds;
            public float percentile95CpuFrameTimeMilliseconds;
            public int gpuFrameTimingSamples;
            public float meanGpuFrameTimeMilliseconds;
            public float percentile95GpuFrameTimeMilliseconds;
            public bool percentile95Within60FpsBudget;
            public ProfilerCounterCapture[] profilerCounters = Array.Empty<ProfilerCounterCapture>();
            public string[] unavailableMetrics = Array.Empty<string>();
            public string screenshotFile = string.Empty;
            public string screenshotCaptureMethod = string.Empty;
            public int screenshotSampleCount;
            public int screenshotDistinctColorBuckets;
            public float screenshotDominantColorFraction;
            public float screenshotLuminanceRange;
            public string screenshotSampleSignature = string.Empty;
            public bool screenshotContentValid;
        }

        [Serializable]
        private sealed class ProfilerCounterCapture
        {
            public string markerName = string.Empty;
            public bool available;
            public string unit = string.Empty;
            public int sampleCount;
            public double mean;
            public double peak;
        }
    }
}
#else
using UnityEngine;

namespace MSC.Development.Performance
{
    /// <summary>
    /// Release-player placeholder; capture, profiling and file output are not shipped.
    /// </summary>
    public sealed class WorldPilotPerformanceProbe : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }
    }
}
#endif
