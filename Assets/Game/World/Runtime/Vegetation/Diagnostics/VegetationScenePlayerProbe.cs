#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.World.Vegetation.Diagnostics
{
    /// <summary>Opt-in private Development-player diagnostic; never attached to gameplay scenes.</summary>
    [DisallowMultipleComponent]
    public sealed class VegetationScenePlayerProbe : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string descriptorJson;
        private readonly List<string> ownedScenePaths = new List<string>();
        private readonly List<NativeRecorder> nativeRecorders = new List<NativeRecorder>();
        private readonly HashSet<string> nativeRecorderKeys = new HashSet<string>();
        private readonly List<GraphicsBuffer> capturedBuffers = new List<GraphicsBuffer>();
        private ProbeReport report;
        private string outputPath;
        private bool quitAfterCapture = true;
        private GameObject cameraOwner;
        private RenderTexture target;
        private AsyncOperation inFlight;

        private void Start()
        {
            if (Application.isEditor) { enabled = false; return; }
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i] == "-msc-vegetation-report" && i + 1 < arguments.Length) outputPath = arguments[++i];
                    else if (arguments[i] == "-msc-vegetation-keep-open") quitAfterCapture = false;
                }
                Require(!string.IsNullOrWhiteSpace(outputPath) && Path.IsPathRooted(outputPath), "Pass -msc-vegetation-report followed by an absolute JSON output path.");
                outputPath = Path.GetFullPath(outputPath);
                BuildDescriptor descriptor = JsonUtility.FromJson<BuildDescriptor>(descriptorJson);
                Require(descriptor != null && descriptor.version == "msc.vegetation-player-probe.v1" && descriptor.scenes.Length == 4 && descriptor.privateDevelopmentBuild, "Invalid embedded build descriptor.");
                report = new ProbeReport { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                    graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                    buildGuid = Application.buildGUID, isEditor = Application.isEditor,
                    developmentBuild = Debug.isDebugBuild, descriptor = descriptor, initialSceneCount = SceneManager.sceneCount,
                    cameraPosition = new Vector3(146.3195953f, 2.7205393f, -1046.6018066f),
                    backgroundLoadingPriority = Application.backgroundLoadingPriority.ToString(),
                    method = "Standalone Development player: four sequential priming loads followed by four same-process warm loads, unloading between scenes without Resources.UnloadUnusedAssets. Active full, full unpacked, selected64, selected128; 256x256 HDRP render target and fixed home camera. No gameplay/global world.",
                    limitations = "First-pass driver/OS cache state is uncontrolled. One warm sample in fixed order, diagnostic resolution and no representative world/GPU frame budget; not 60 FPS acceptance. Unity Player scene serialization can differ from Editor prefab loading. Source SHA/dependency validation occurred in Editor at build time; the runtime never reads project source files. Frame gaps include scheduling/rendering; native marker totals overlap and must not be added."
                };
                Require(Debug.isDebugBuild, "The private diagnostic requires a Development player.");
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsInstancing, "Graphics and instancing are required; do not use -nographics.");
                Require(GraphicsSettings.currentRenderPipeline != null && Camera.allCamerasCount == 0, "Expected HDRP and no existing camera.");
                Application.runInBackground = true;
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
                StartCoroutine(RunGuarded(RunProbe()));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (report != null) { report.failure = exception.ToString(); WriteReport(); }
                if (quitAfterCapture) Application.Quit(2);
                enabled = false;
            }
        }

        private IEnumerator RunGuarded(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(work);
            while (stack.Count > 0)
            {
                bool moved = false; object current = null; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { report.failure = failure.ToString(); Debug.LogException(failure); break; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator child) stack.Push(child);
                else yield return current;
            }
            // Failure still drains any owned load, then unloads only owned scenes.
            if (inFlight != null)
            {
                long wait = Stopwatch.GetTimestamp();
                while (!inFlight.isDone && ElapsedMilliseconds(wait) < 180000d) yield return null;
                if (!inFlight.isDone) report.failure += "\nPending scene operation did not finish during cleanup.";
                inFlight = null;
            }
            foreach (string path in ownedScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                AsyncOperation unload = null;
                try { unload = SceneManager.UnloadSceneAsync(scene); }
                catch (Exception exception) { report.failure += "\nCleanup: " + exception; }
                if (unload != null)
                {
                    long wait = Stopwatch.GetTimestamp();
                    while (!unload.isDone && ElapsedMilliseconds(wait) < 180000d) yield return null;
                }
            }
            yield return null;
            report.finalSceneCount = SceneManager.sceneCount;
            report.passed = string.IsNullOrEmpty(report.failure) && report.cases.Count == 8 && report.finalSceneCount == report.initialSceneCount;
            foreach (CaseReport result in report.cases) report.passed &= result.unloaded;
            ReleaseCameraAndRecorders();
            bool written = WriteReport();
            if (quitAfterCapture) Application.Quit(report.passed && written ? 0 : 2);
            enabled = false;
        }

        private IEnumerator RunProbe()
        {
            foreach (SceneDescriptor descriptor in report.descriptor.scenes)
            {
                Require(SceneUtility.GetBuildIndexByScenePath(descriptor.scenePath) == descriptor.buildIndex, "Exact scene build index/path mismatch: " + descriptor.scenePath);
                Require(!SceneManager.GetSceneByPath(descriptor.scenePath).isLoaded, "Refusing an already loaded scene.");
            }
            CreateCamera();
            for (int i = 0; i < 30; i++) yield return null;
            for (int pass = 0; pass < 2; pass++)
            foreach (SceneDescriptor descriptor in report.descriptor.scenes)
            {
                DiscoverNativeMarkers(report, pass);
                for (int i = 0; i < 3; i++) yield return null;
                var result = new CaseReport { priming = pass == 0, variant = descriptor.name, scenePath = descriptor.scenePath };
                report.cases.Add(result);
                capturedBuffers.Clear();
                var phase = new PhaseReport { phase = "load", scenePath = descriptor.scenePath, startedFrame = Time.frameCount };
                result.load = phase; ResetNativeRecorders();
                long started = Stopwatch.GetTimestamp();
                inFlight = SceneManager.LoadSceneAsync(descriptor.buildIndex, LoadSceneMode.Additive);
                phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                Require(inFlight != null, "LoadSceneAsync returned null.");
                ownedScenePaths.Add(descriptor.scenePath);
                yield return ObserveOperation(phase, inFlight, started); inFlight = null;
                yield return ObserveFrames(phase, 3); CompletePhase(phase);
                Scene scene = SceneManager.GetSceneByPath(descriptor.scenePath);
                Require(scene.IsValid() && scene.isLoaded && scene.buildIndex == descriptor.buildIndex, "Wrong scene loaded.");
                result.sceneCountWhileLoaded = SceneManager.sceneCount;
                var identities = new HashSet<string>(descriptor.selectedIds, StringComparer.Ordinal);
                Require(identities.Count == descriptor.woodyPrefabCount, "Invalid embedded population identities.");
                var trees = new List<GameObject>();
                var grass = new List<VegetationWorldRenderer>();
                var categories = new HashSet<string>(StringComparer.Ordinal);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                    Require(group != null && group.GeneratorId == "msc.map-vegetation-rebuild.v1" && group.CellId == "cell_1_-3" && root.activeSelf && categories.Add(group.Category), "Unexpected category ownership/state.");
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) { result.gameObjects++; if (node.gameObject.activeInHierarchy) result.activeGameObjects++; }
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) { result.renderers++; if (renderer.gameObject.activeInHierarchy) result.activeRenderers++; }
                    foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) { result.colliders++; if (collider.enabled && collider.gameObject.activeInHierarchy) result.activeEnabledColliders++; }
                    if (group.Category == "GrassCoverage") grass.AddRange(root.GetComponentsInChildren<VegetationWorldRenderer>(true));
                    else
                    {
                        Require(group.Category == "OriginalTrees" || group.Category == "BoundaryForest" || group.Category == "ShrubsAndUndergrowth", "Unknown woody category.");
                        for (int i = 0; i < root.transform.childCount; i++)
                        {
                            GameObject tree = root.transform.GetChild(i).gameObject;
                            Require(tree.activeInHierarchy && identities.Remove(group.Category + "/" + tree.name), "Missing/duplicate/unexpected selected tree identity.");
                            trees.Add(tree);
                        }
                    }
                }
                Require(categories.Count == 4 && identities.Count == 0 && trees.Count == descriptor.woodyPrefabCount, "Selected population did not survive the player build.");
                Require(result.gameObjects == descriptor.gameObjects && result.renderers == descriptor.renderers && result.colliders == descriptor.colliders, "Built native population differs from the validated fixture.");
                Require(grass.Count == 1 && grass[0].isActiveAndEnabled, "Expected one unchanged active grass renderer.");
                foreach (VegetationWorldRenderer renderer in grass)
                {
                    result.residentGpuBuffers += renderer.ResidentGpuBufferCount;
                    result.uploadedGrassInstances += renderer.UploadedInstanceCount;
                    CaptureBuffers(renderer);
                }
                Require(capturedBuffers.Count == result.residentGpuBuffers, "GPU buffer ownership count differs.");
                phase = new PhaseReport { phase = "unload", scenePath = descriptor.scenePath, startedFrame = Time.frameCount };
                result.unload = phase;
                yield return null; ResetNativeRecorders();
                started = Stopwatch.GetTimestamp(); inFlight = SceneManager.UnloadSceneAsync(scene);
                phase.requestCallMilliseconds = ElapsedMilliseconds(started);
                Require(inFlight != null, "UnloadSceneAsync returned null.");
                yield return ObserveOperation(phase, inFlight, started); inFlight = null;
                yield return ObserveFrames(phase, 3); CompletePhase(phase);
                Require(!SceneManager.GetSceneByPath(descriptor.scenePath).isLoaded, "Scene did not unload.");
                foreach (GameObject tree in trees) Require(tree == null, "An owned tree survived unload.");
                foreach (VegetationWorldRenderer renderer in grass) Require(renderer == null, "An owned renderer survived unload.");
                foreach (GraphicsBuffer buffer in capturedBuffers) Require(!buffer.IsValid(), "A captured GPU buffer survived unload.");
                result.unloaded = true; ownedScenePaths.Remove(descriptor.scenePath);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                CaseReport active = report.cases[pass * 4], unpacked = report.cases[pass * 4 + 1];
                Require(active.activeGameObjects == unpacked.activeGameObjects && active.activeRenderers == unpacked.activeRenderers && active.activeEnabledColliders == unpacked.activeEnabledColliders, "Full unpacked population differs from active control.");
            }
        }

        private IEnumerator ObserveOperation(PhaseReport phase, AsyncOperation operation, long started)
        {
            long previous = started;
            do { yield return null; phase.intervals.Add(ElapsedMilliseconds(previous)); previous = Stopwatch.GetTimestamp(); Require(ElapsedMilliseconds(started) < 180000d, "Scene operation timed out."); } while (!operation.isDone);
            phase.asyncWallMilliseconds = ElapsedMilliseconds(started); phase.completedFrame = Time.frameCount;
        }
        private IEnumerator ObserveFrames(PhaseReport phase, int count)
        {
            long previous = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) { yield return null; phase.intervals.Add(ElapsedMilliseconds(previous)); previous = Stopwatch.GetTimestamp(); }
        }
        private void CompletePhase(PhaseReport phase)
        {
            foreach (NativeRecorder recorder in nativeRecorders) phase.nativeMarkers.Add(recorder.Capture());
            double[] values = phase.intervals.ToArray(); Array.Sort(values);
            phase.sampleCount = values.Length;
            if (values.Length > 0) { phase.maximumObservedYieldIntervalMilliseconds = values[values.Length - 1]; phase.p95ObservedYieldIntervalMilliseconds = values[Math.Max(0, (int)Math.Ceiling(values.Length * .95d) - 1)]; }
        }
        private void CaptureBuffers(VegetationWorldRenderer renderer)
        {
            var batches = (IList)typeof(VegetationWorldRenderer).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            foreach (object batch in batches)
            foreach (string property in new[] { "InstanceBuffer", "Arguments" })
            { var buffer = (GraphicsBuffer)batch.GetType().GetProperty(property).GetValue(batch); if (buffer != null) capturedBuffers.Add(buffer); }
        }
        private void CreateCamera()
        {
            cameraOwner = new GameObject("Standalone vegetation diagnostic camera");
            Camera camera = cameraOwner.AddComponent<Camera>(); camera.enabled = false;
            cameraOwner.AddComponent<HDAdditionalCameraData>();
            camera.transform.SetPositionAndRotation(report.cameraPosition, Quaternion.identity);
            camera.fieldOfView = 90; camera.nearClipPlane = .1f; camera.farClipPlane = 1500; camera.cullingMask = ~0;
            target = new RenderTexture(256, 256, 24); target.Create(); camera.targetTexture = target; camera.enabled = true;
        }
        private bool WriteReport()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(outputPath)); File.WriteAllText(outputPath, JsonUtility.ToJson(report, true)); Debug.Log("MSC_VEGETATION_PLAYER_PROBE_RESULT passed=" + report.passed + " path=" + outputPath); return true; }
            catch (Exception exception) { Debug.LogException(exception); return false; }
        }
        private void ReleaseCameraAndRecorders()
        {
            if (cameraOwner != null) { Camera camera = cameraOwner.GetComponent<Camera>(); camera.enabled = false; camera.targetTexture = null; Destroy(cameraOwner); cameraOwner = null; }
            if (target != null) { target.Release(); Destroy(target); target = null; }
            foreach (NativeRecorder recorder in nativeRecorders) recorder.Dispose(); nativeRecorders.Clear(); nativeRecorderKeys.Clear();
        }
        private void OnDestroy() => ReleaseCameraAndRecorders();
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static double ElapsedMilliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        [Serializable] private sealed class BuildDescriptor
        {
            public string version, buildUtc, fixtureSha256, sourceSceneSha256, scriptingBackend;
            public bool privateDevelopmentBuild; public SceneDescriptor[] scenes;
        }
        [Serializable] private sealed class SceneDescriptor
        {
            public string name, scenePath, sourceSha256, dependencyHash; public int buildIndex, woodyPrefabCount, gameObjects, renderers, colliders; public string[] selectedIds;
        }
        [Serializable] private sealed class ProbeReport
        {
            public string utc, unityVersion, graphicsDevice, graphicsApi, graphicsDeviceVersion, buildGuid, backgroundLoadingPriority, method, limitations, failure;
            public bool passed, isEditor, developmentBuild; public Vector3 cameraPosition; public int initialSceneCount, finalSceneCount;
            public BuildDescriptor descriptor;
            public List<CaseReport> cases = new List<CaseReport>();
            public List<NativeMarkerDescription> discoveredNativeMarkers = new List<NativeMarkerDescription>();
        }
        [Serializable] private sealed class CaseReport
        {
            public string variant, scenePath; public bool priming, unloaded; public PhaseReport load, unload;
            public int sceneCountWhileLoaded, gameObjects, activeGameObjects, renderers, activeRenderers, colliders, activeEnabledColliders, residentGpuBuffers;
            public long uploadedGrassInstances;
        }
        [Serializable] private sealed class PhaseReport
        {
            public string phase, scenePath; public int startedFrame, completedFrame, sampleCount;
            public double requestCallMilliseconds, asyncWallMilliseconds, maximumObservedYieldIntervalMilliseconds, p95ObservedYieldIntervalMilliseconds;
            public List<double> intervals = new List<double>();
            public List<NativeMarkerCapture> nativeMarkers = new List<NativeMarkerCapture>();
        }
        private void DiscoverNativeMarkers(ProbeReport report, int passIndex)
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
                report.discoveredNativeMarkers.Add(entry);
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
    }
}
#endif
