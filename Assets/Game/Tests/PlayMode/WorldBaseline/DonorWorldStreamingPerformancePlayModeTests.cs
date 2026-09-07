using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Player;
using MSC.World.Streaming;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.Tests.PlayMode.WorldBaseline
{
    public sealed class DonorWorldStreamingPerformancePlayModeTests
    {
        private const string EvidenceRelativePath =
            "PerformanceCaptures/Milestone06B2/" +
            "M06B2_STREAMING_PERFORMANCE.json";

        [Category("LocalDonorBaseline")]
        [UnityTest]
        public IEnumerator ActiveDonorProfile_RecordsStreamingPerformanceBaseline()
        {
#if UNITY_EDITOR
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GlobalScenePath) == null ||
                !HasSceneInBuildSettings("World_Global_Legacy"))
            {
                Assert.Ignore(
                    "Generate the local 06B2 RuntimeBaseline before running " +
                    "the streaming performance capture.");
            }
#else
            Assert.Ignore(
                "The local donor RuntimeBaseline performance capture is " +
                "Editor-only.");
#endif

            PerformanceSnapshot prototype = null;
            yield return LoadSingle("PrototypeWorldStreamingFixture");
            ProductionWorldStreamingInstaller prototypeInstaller =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>();
            ProductionWorldStreamingService prototypeStreaming =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingService>();
            yield return WaitForReady(
                prototypeInstaller,
                prototypeStreaming,
                60d);
            yield return WaitFrames(15, null);
            prototype = CaptureSnapshot(
                "prototype-fixture",
                prototypeStreaming);
            prototypeStreaming.enabled = false;
            yield return prototypeStreaming.UnloadOwnedScenes();
            Object.Destroy(
                prototypeInstaller.CompositionRoot.gameObject);
            yield return null;

            var sceneLoadMilliseconds =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);
            var bootstrapStopwatch = Stopwatch.StartNew();
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (!string.IsNullOrWhiteSpace(scene.path))
                {
                    sceneLoadMilliseconds[scene.path] =
                        bootstrapStopwatch.Elapsed.TotalMilliseconds;
                }
            }

            SceneManager.sceneLoaded += Loaded;
            ProductionWorldStreamingInstaller installer = null;
            ProductionWorldStreamingService streaming = null;
            try
            {
                yield return LoadSingle("Bootstrap");
                installer =
                    Object.FindFirstObjectByType<
                        ProductionWorldStreamingInstaller>();
                streaming =
                    Object.FindFirstObjectByType<
                        ProductionWorldStreamingService>();
                yield return WaitForReady(
                    installer,
                    streaming,
                    120d);
            }
            finally
            {
                bootstrapStopwatch.Stop();
                SceneManager.sceneLoaded -= Loaded;
            }

            Assert.That(streaming.Manifest.ProfileKind,
                Is.EqualTo(ProductionWorldProfileKind.DonorFeatureParity));
            DisablePlayer(installer.SpawnedPlayer);
            streaming.enabled = false;

            using CounterRecorder mainThread =
                CounterRecorder.TryCreate(
                    ProfilerCategory.Internal,
                    "Main Thread");
            using CounterRecorder renderThread =
                CounterRecorder.TryCreate(
                    ProfilerCategory.Internal,
                    "Render Thread");

            var frameMilliseconds = new List<float>();
            yield return WaitFrames(
                30,
                value =>
                {
                    frameMilliseconds.Add(value);
                    mainThread.Sample();
                    renderThread.Sample();
                });
            PerformanceSnapshot initial = CaptureSnapshot(
                "donor-initial",
                streaming);
            long peakUsedMemory = initial.totalUsedMemoryBytes;
            long peakReservedMemory =
                initial.totalReservedMemoryBytes;
            var transitions = new List<StreamingTransitionCapture>();

            Vector3[] route =
            {
                new Vector3(153.495f, 10f, -800f),
                new Vector3(-1280f, 10f, 256f),
                new Vector3(1792f, 10f, -1792f),
                new Vector3(153.495f, 10f, -1280f)
            };
            foreach (Vector3 position in route)
            {
                installer.SpawnedPlayer.transform.position = position;
                streaming.ReportFocusSpeedMetersPerSecond(0f);
                var transitionFrames = new List<float>();
                var refresh = Stopwatch.StartNew();
                yield return RefreshWithSampling(
                    streaming,
                    frameMilliseconds,
                    mainThread,
                    renderThread,
                    transitionFrames);
                refresh.Stop();
                yield return WaitFrames(
                    20,
                    value =>
                    {
                        transitionFrames.Add(value);
                        frameMilliseconds.Add(value);
                        mainThread.Sample();
                        renderThread.Sample();
                    });
                PerformanceSnapshot snapshot =
                    CaptureSnapshot(
                        "route-" + transitions.Count,
                        streaming);
                peakUsedMemory = Math.Max(
                    peakUsedMemory,
                    snapshot.totalUsedMemoryBytes);
                peakReservedMemory = Math.Max(
                    peakReservedMemory,
                    snapshot.totalReservedMemoryBytes);
                transitions.Add(new StreamingTransitionCapture
                {
                    focusX = position.x,
                    focusY = position.y,
                    focusZ = position.z,
                    refreshMilliseconds =
                        refresh.Elapsed.TotalMilliseconds,
                    maximumFrameMilliseconds =
                        transitionFrames.Count == 0
                            ? 0f
                            : transitionFrames.Max(),
                    loadedOwnedSceneCount =
                        streaming.OwnedLoadedSceneCount,
                    rendererCount = snapshot.rendererCount,
                    colliderCount = snapshot.colliderCount,
                    generatedMaterialCount =
                        snapshot.generatedMaterialCount,
                    runtimeMaterialInstanceCount =
                        snapshot.runtimeMaterialInstanceCount,
                    generatedTextureCount =
                        snapshot.generatedTextureCount,
                    generatedTextureMemoryBytes =
                        snapshot.generatedTextureMemoryBytes,
                    usedMemoryBytes =
                        snapshot.totalUsedMemoryBytes,
                    reservedMemoryBytes =
                        snapshot.totalReservedMemoryBytes
                });
            }

            installer.SpawnedPlayer.transform.position =
                new Vector3(153.495f, 10f, -1280f);
            streaming.ReportFocusSpeedMetersPerSecond(13f);
            var preload = Stopwatch.StartNew();
            yield return RefreshWithSampling(
                streaming,
                frameMilliseconds,
                mainThread,
                renderThread,
                null);
            preload.Stop();
            Assert.That(
                streaming.EffectiveLoadingRadiusCells,
                Is.EqualTo(2));
            Assert.That(
                streaming.IsCellLoaded("cell_2_-4"),
                Is.True,
                "Vehicle-speed preload did not load a known radius-two cell.");
            yield return WaitFrames(
                20,
                value =>
                {
                    frameMilliseconds.Add(value);
                    mainThread.Sample();
                    renderThread.Sample();
                });
            PerformanceSnapshot preloadSnapshot = CaptureSnapshot(
                "vehicle-preload",
                streaming);
            peakUsedMemory = Math.Max(
                peakUsedMemory,
                preloadSnapshot.totalUsedMemoryBytes);
            peakReservedMemory = Math.Max(
                peakReservedMemory,
                preloadSnapshot.totalReservedMemoryBytes);

            streaming.ReportFocusSpeedMetersPerSecond(0f);
            installer.SpawnedPlayer.transform.position =
                new Vector3(-1280f, 10f, 256f);
            yield return RefreshWithSampling(
                streaming,
                frameMilliseconds,
                mainThread,
                renderThread,
                null);
            installer.SpawnedPlayer.transform.position =
                new Vector3(153.495f, 10f, -1280f);
            yield return RefreshWithSampling(
                streaming,
                frameMilliseconds,
                mainThread,
                renderThread,
                null);
            yield return WaitFrames(
                30,
                value =>
                {
                    frameMilliseconds.Add(value);
                    mainThread.Sample();
                    renderThread.Sample();
                });
            PerformanceSnapshot recovered = CaptureSnapshot(
                "donor-recovered",
                streaming);
            peakUsedMemory = Math.Max(
                peakUsedMemory,
                recovered.totalUsedMemoryBytes);
            peakReservedMemory = Math.Max(
                peakReservedMemory,
                recovered.totalReservedMemoryBytes);

            foreach (Vector3 position in route)
            {
                installer.SpawnedPlayer.transform.position = position;
                streaming.ReportFocusSpeedMetersPerSecond(0f);
                yield return RefreshWithSampling(
                    streaming,
                    frameMilliseconds,
                    mainThread,
                    renderThread,
                    null);
            }
            yield return WaitFrames(
                30,
                value =>
                {
                    frameMilliseconds.Add(value);
                    mainThread.Sample();
                    renderThread.Sample();
                });
            PerformanceSnapshot warmedRecovered = CaptureSnapshot(
                "donor-warmed-recovered",
                streaming);
            peakUsedMemory = Math.Max(
                peakUsedMemory,
                warmedRecovered.totalUsedMemoryBytes);
            peakReservedMemory = Math.Max(
                peakReservedMemory,
                warmedRecovered.totalReservedMemoryBytes);

            var evidence = new PerformanceEvidence
            {
                schemaVersion = 2,
                validatorId =
                    "donor-world-streaming-performance-06b2",
                capturedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                executionMode = Application.isEditor
                    ? "EditorPlayMode"
                    : "DevelopmentPlayer",
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                graphicsDeviceName =
                    SystemInfo.graphicsDeviceName,
                activeProfileId = streaming.Manifest.ProfileId,
                cellSizeMeters =
                    streaming.Manifest.CellSizeMeters,
                normalLoadingRadius =
                    streaming.Manifest.LoadingRadiusCells,
                vehicleLoadingRadius =
                    streaming.Manifest.VehiclePreloadRadiusCells,
                bootstrapReadyMilliseconds =
                    bootstrapStopwatch.Elapsed.TotalMilliseconds,
                globalSceneLoadedMilliseconds =
                    FindSceneLoadTime(
                        sceneLoadMilliseconds,
                        "World_Global_Legacy.unity"),
                firstFocusCellLoadedMilliseconds =
                    FindSceneLoadTime(
                        sceneLoadMilliseconds,
                        "World_Cell_0_-3_Legacy.unity"),
                prototype = prototype,
                initial = initial,
                vehiclePreload = preloadSnapshot,
                recovered = recovered,
                warmedRecovered = warmedRecovered,
                peakUsedMemoryBytes = peakUsedMemory,
                peakReservedMemoryBytes =
                    peakReservedMemory,
                vehiclePreloadRefreshMilliseconds =
                    preload.Elapsed.TotalMilliseconds,
                frameTimeMilliseconds = BuildTiming(
                    frameMilliseconds),
                mainThread = mainThread.Build(),
                renderThread = renderThread.Build(),
                transitions = transitions.ToArray(),
                limitations = new[]
                {
                    "Capture was produced in Editor PlayMode; standalone " +
                    "Development Player validation remains a 06B3 task.",
                    "Render Thread ProfilerRecorder is reported unavailable " +
                    "when the current Unity backend exposes no matching marker.",
                    "Legacy global static-batch aggregates are intentionally " +
                    "unsplit and dominate resident memory."
                }
            };

            Assert.That(
                initial.rendererCount,
                Is.GreaterThan(prototype.rendererCount));
            Assert.That(
                initial.colliderCount,
                Is.GreaterThanOrEqualTo(32));
            Assert.That(
                streaming.IsGlobalSceneLoaded("global-legacy"),
                Is.True);
            Assert.That(
                initial.generatedMaterialCount,
                Is.GreaterThan(0));
            Assert.That(
                initial.generatedTextureCount,
                Is.GreaterThan(0));
            Assert.That(
                initial.runtimeMaterialInstanceCount,
                Is.Zero);
            Assert.That(
                preloadSnapshot.runtimeMaterialInstanceCount,
                Is.Zero);
            Assert.That(
                recovered.runtimeMaterialInstanceCount,
                Is.Zero);
            Assert.That(
                warmedRecovered.runtimeMaterialInstanceCount,
                Is.Zero);
            Assert.That(
                transitions.All(transition =>
                    transition.runtimeMaterialInstanceCount == 0),
                Is.True);
            Assert.That(
                warmedRecovered.generatedMaterialCount,
                Is.EqualTo(recovered.generatedMaterialCount));
            Assert.That(
                warmedRecovered.generatedTextureCount,
                Is.EqualTo(recovered.generatedTextureCount));
            Assert.That(
                warmedRecovered.generatedTextureMemoryBytes,
                Is.EqualTo(recovered.generatedTextureMemoryBytes));
            WriteEvidence(evidence);
            Debug.Log(
                "M06B2_STREAMING_PERFORMANCE_EVIDENCE_WRITTEN path=" +
                EvidenceRelativePath);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ProductionWorldStreamingService[] services =
                Object.FindObjectsByType<
                    ProductionWorldStreamingService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (ProductionWorldStreamingService service in services)
            {
                service.enabled = false;
                double timeout =
                    Time.realtimeSinceStartupAsDouble + 30d;
                while (service.IsStreaming &&
                       Time.realtimeSinceStartupAsDouble < timeout)
                {
                    yield return null;
                }
                if (!service.IsStreaming)
                {
                    yield return service.UnloadOwnedScenes();
                }
            }

            GameCompositionRoot[] roots =
                Object.FindObjectsByType<GameCompositionRoot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (GameCompositionRoot root in roots)
            {
                Object.Destroy(root.gameObject);
            }
            yield return null;
        }

        private static IEnumerator WaitForReady(
            ProductionWorldStreamingInstaller installer,
            ProductionWorldStreamingService streaming,
            double timeoutSeconds)
        {
            Assert.That(installer, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);
            if (!installer.IsReady &&
                !installer.IsGameplayPreparationRunning)
            {
                Assert.That(
                    installer.TryBeginGameplayPreparation(
                        out string preparationFailure),
                    Is.True,
                    preparationFailure);
            }

            double timeout =
                Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while ((!installer.IsReady || streaming.IsStreaming) &&
                   Time.realtimeSinceStartupAsDouble < timeout)
            {
                yield return null;
            }
            Assert.That(
                Time.realtimeSinceStartupAsDouble,
                Is.LessThan(timeout),
                "Streaming bootstrap timed out.");
            Assert.That(installer.IsReady, Is.True);
        }

        private static IEnumerator WaitFrames(
            int frames,
            Action<float> sample)
        {
            for (int index = 0; index < frames; index++)
            {
                yield return null;
                sample?.Invoke(Time.unscaledDeltaTime * 1000f);
            }
        }

        private static IEnumerator RefreshWithSampling(
            ProductionWorldStreamingService streaming,
            ICollection<float> frameMilliseconds,
            CounterRecorder mainThread,
            CounterRecorder renderThread,
            ICollection<float> transitionFrameMilliseconds)
        {
            bool completed = false;
            streaming.StartCoroutine(
                RunAndSignal(
                    streaming.RefreshNow(),
                    () => completed = true));
            double timeout =
                Time.realtimeSinceStartupAsDouble + 120d;
            while (!completed &&
                   Time.realtimeSinceStartupAsDouble < timeout)
            {
                float milliseconds =
                    Time.unscaledDeltaTime * 1000f;
                frameMilliseconds.Add(milliseconds);
                transitionFrameMilliseconds?.Add(milliseconds);
                mainThread.Sample();
                renderThread.Sample();
                yield return null;
            }

            Assert.That(
                Time.realtimeSinceStartupAsDouble,
                Is.LessThan(timeout),
                "Streaming refresh timed out during performance capture.");
        }

        private static IEnumerator RunAndSignal(
            IEnumerator operation,
            Action completed)
        {
            try
            {
                yield return operation;
            }
            finally
            {
                completed();
            }
        }

        private static IEnumerator LoadSingle(string sceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single);
            Assert.That(
                load,
                Is.Not.Null,
                "Scene is missing from Build Settings: " + sceneName);
            yield return load;
            yield return null;
        }

        private static bool HasSceneInBuildSettings(string sceneName) =>
            SceneUtility.GetBuildIndexByScenePath(
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
                "Streaming/Scenes/" + sceneName + ".unity") >= 0;

        private const string GlobalScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/World_Global_Legacy.unity";

        private static void DisablePlayer(GameObject player)
        {
            FirstPersonMotor motor =
                player.GetComponent<FirstPersonMotor>();
            PlayerInputRouter input =
                player.GetComponent<PlayerInputRouter>();
            if (motor != null)
            {
                motor.enabled = false;
            }
            if (input != null)
            {
                input.enabled = false;
            }
        }

        private static PerformanceSnapshot CaptureSnapshot(
            string id,
            ProductionWorldStreamingService streaming)
        {
            Material[] generatedMaterials =
                Resources.FindObjectsOfTypeAll<Material>()
                    .Where(material =>
                        material.name.StartsWith(
                            "M06B2_",
                            StringComparison.Ordinal))
                    .ToArray();
            Texture[] generatedTextures =
                Resources.FindObjectsOfTypeAll<Texture>()
                    .Where(texture =>
                        texture.name.StartsWith(
                            "M06B2_",
                            StringComparison.Ordinal))
                    .ToArray();
            return new PerformanceSnapshot
            {
                id = id,
                totalUsedMemoryBytes =
                    Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedMemoryBytes =
                    Profiler.GetTotalReservedMemoryLong(),
                monoUsedMemoryBytes =
                    Profiler.GetMonoUsedSizeLong(),
                loadedSceneCount = SceneManager.sceneCount,
                ownedSceneCount =
                    streaming == null
                        ? 0
                        : streaming.OwnedLoadedSceneCount,
                rendererCount =
                    Object.FindObjectsByType<Renderer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length,
                colliderCount =
                    Object.FindObjectsByType<Collider>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None).Length,
                generatedMaterialCount =
                    generatedMaterials.Select(material =>
                            material.GetEntityId())
                        .Distinct()
                        .Count(),
                runtimeMaterialInstanceCount =
                    generatedMaterials.Count(material =>
                        material.name.EndsWith(
                            " (Instance)",
                            StringComparison.Ordinal)),
                generatedTextureCount =
                    generatedTextures.Select(texture =>
                            texture.GetEntityId())
                        .Distinct()
                        .Count(),
                generatedTextureMemoryBytes =
                    generatedTextures.Sum(texture =>
                        Profiler.GetRuntimeMemorySizeLong(texture))
            };
        }

        private static double FindSceneLoadTime(
            IReadOnlyDictionary<string, double> times,
            string suffix)
        {
            foreach (KeyValuePair<string, double> pair in times)
            {
                if (pair.Key.EndsWith(
                        suffix,
                        StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }
            return -1d;
        }

        private static TimingCapture BuildTiming(
            IEnumerable<float> values)
        {
            float[] ordered = values
                .Where(value =>
                    !float.IsNaN(value) &&
                    !float.IsInfinity(value))
                .OrderBy(value => value)
                .ToArray();
            if (ordered.Length == 0)
            {
                return new TimingCapture();
            }

            int p95Index = Mathf.Clamp(
                Mathf.CeilToInt(ordered.Length * 0.95f) - 1,
                0,
                ordered.Length - 1);
            return new TimingCapture
            {
                sampleCount = ordered.Length,
                meanMilliseconds = ordered.Average(),
                p95Milliseconds = ordered[p95Index],
                maximumMilliseconds = ordered[^1]
            };
        }

        private static void WriteEvidence(PerformanceEvidence evidence)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "Project root cannot be resolved.");
            string path = Path.Combine(
                projectRoot,
                EvidenceRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ??
                throw new InvalidOperationException(
                    "Performance evidence path has no directory."));
            string temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                JsonUtility.ToJson(evidence, prettyPrint: true) +
                Environment.NewLine,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
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
            public string operatingSystem = string.Empty;
            public string processorType = string.Empty;
            public string graphicsDeviceName = string.Empty;
            public string activeProfileId = string.Empty;
            public float cellSizeMeters;
            public int normalLoadingRadius;
            public int vehicleLoadingRadius;
            public double bootstrapReadyMilliseconds;
            public double globalSceneLoadedMilliseconds;
            public double firstFocusCellLoadedMilliseconds;
            public PerformanceSnapshot prototype =
                new PerformanceSnapshot();
            public PerformanceSnapshot initial =
                new PerformanceSnapshot();
            public PerformanceSnapshot vehiclePreload =
                new PerformanceSnapshot();
            public PerformanceSnapshot recovered =
                new PerformanceSnapshot();
            public PerformanceSnapshot warmedRecovered =
                new PerformanceSnapshot();
            public long peakUsedMemoryBytes;
            public long peakReservedMemoryBytes;
            public double vehiclePreloadRefreshMilliseconds;
            public TimingCapture frameTimeMilliseconds =
                new TimingCapture();
            public CounterCapture mainThread =
                new CounterCapture();
            public CounterCapture renderThread =
                new CounterCapture();
            public StreamingTransitionCapture[] transitions =
                Array.Empty<StreamingTransitionCapture>();
            public string[] limitations = Array.Empty<string>();
        }

        [Serializable]
        private sealed class PerformanceSnapshot
        {
            public string id = string.Empty;
            public long totalUsedMemoryBytes;
            public long totalReservedMemoryBytes;
            public long monoUsedMemoryBytes;
            public int loadedSceneCount;
            public int ownedSceneCount;
            public int rendererCount;
            public int colliderCount;
            public int generatedMaterialCount;
            public int runtimeMaterialInstanceCount;
            public int generatedTextureCount;
            public long generatedTextureMemoryBytes;
        }

        [Serializable]
        private sealed class StreamingTransitionCapture
        {
            public float focusX;
            public float focusY;
            public float focusZ;
            public double refreshMilliseconds;
            public float maximumFrameMilliseconds;
            public int loadedOwnedSceneCount;
            public int rendererCount;
            public int colliderCount;
            public int generatedMaterialCount;
            public int runtimeMaterialInstanceCount;
            public int generatedTextureCount;
            public long generatedTextureMemoryBytes;
            public long usedMemoryBytes;
            public long reservedMemoryBytes;
        }

        [Serializable]
        private sealed class TimingCapture
        {
            public int sampleCount;
            public double meanMilliseconds;
            public double p95Milliseconds;
            public double maximumMilliseconds;
        }

        [Serializable]
        private sealed class CounterCapture
        {
            public string markerName = string.Empty;
            public bool available;
            public int sampleCount;
            public double lastMilliseconds;
            public double maximumMilliseconds;
        }

        private sealed class CounterRecorder : IDisposable
        {
            private readonly ProfilerRecorder recorder;
            private readonly string markerName;
            private long maximum;
            private long last;
            private int sampleCount;

            private CounterRecorder(
                ProfilerRecorder value,
                string name)
            {
                recorder = value;
                markerName = name;
            }

            public static CounterRecorder TryCreate(
                ProfilerCategory category,
                string markerName)
            {
                try
                {
                    return new CounterRecorder(
                        ProfilerRecorder.StartNew(
                            category,
                            markerName,
                            256),
                        markerName);
                }
                catch (Exception)
                {
                    return new CounterRecorder(
                        default,
                        markerName);
                }
            }

            public CounterCapture Build()
            {
                if (!recorder.Valid || sampleCount == 0)
                {
                    return new CounterCapture
                    {
                        markerName = markerName,
                        available = false
                    };
                }

                return new CounterCapture
                {
                    markerName = markerName,
                    available = true,
                    sampleCount = sampleCount,
                    lastMilliseconds = last / 1_000_000d,
                    maximumMilliseconds =
                        maximum / 1_000_000d
                };
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

            public void Dispose()
            {
                if (recorder.Valid)
                {
                    recorder.Dispose();
                }
            }
        }
    }
}
