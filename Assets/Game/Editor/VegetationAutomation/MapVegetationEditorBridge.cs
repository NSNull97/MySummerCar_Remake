using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Editor.Vegetation;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VegetationAutomation
{
    /// <summary>
    /// Executes explicit local vegetation requests inside the already open
    /// Editor. It never saves/discards user scenes, stops Play mode or accepts
    /// arbitrary methods, code, scene paths or output paths from a request.
    /// </summary>
    [InitializeOnLoad]
    public static class MapVegetationEditorBridge
    {
        private const string ActiveKey = "MSC.VegetationBridge.ActiveRequest";
        private const string TestKey = "MSC.VegetationBridge.CurrentTest";
        private const string StartedKey = "MSC.VegetationBridge.StartedUtc";
        private static readonly Regex SafeId = new Regex("^[A-Za-z0-9_-]{1,96}$");
        private static readonly string OutputFolder = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Artifacts/VegetationRebuild"));
        private static readonly string RequestPath = Path.Combine(OutputFolder, "editor-request.json");
        private static readonly CallbackHandler Callbacks = new CallbackHandler();
        private static TestRunnerApi testApi;
        private static double nextPoll;
        private static double restoredAt;
        private static bool executing;
        private static string lastBridgeError = string.Empty;

        static MapVegetationEditorBridge()
        {
            restoredAt = EditorApplication.timeSinceStartup;
            TestRunnerApi.RegisterTestCallback(Callbacks);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.update += Poll;
        }

        private static void BeforeReload()
        {
            TestRunnerApi.UnregisterTestCallback(Callbacks);
            EditorApplication.update -= Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 3d;
            try
            {
                Directory.CreateDirectory(OutputFolder);
                WriteStatus();
                Request active = ActiveRequest();
                if (active != null)
                {
                    // Domain reload must not repeat a mutation or test launch.
                    // A lost run is reported, never resumed by guessing.
                    if (!executing && !EditorApplication.isPlayingOrWillChangePlaymode &&
                        !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                        !IsAnyTestRunActive() && EditorApplication.timeSinceStartup - restoredAt > 45d)
                        Complete(active, "interrupted", "Request did not resume after a domain reload; no automatic retry was performed.");
                    return;
                }

                if (!File.Exists(RequestPath)) return;
                string text = File.ReadAllText(RequestPath);
                Request request = JsonUtility.FromJson<Request>(text);
                if (request == null || string.IsNullOrWhiteSpace(request.id) || !SafeId.IsMatch(request.id))
                {
                    lastBridgeError = "Request must contain an id of 1-96 letters, digits, underscores or hyphens.";
                    WriteStatus();
                    return;
                }

                // A completed ID is idempotent even across Editor restarts.
                if (File.Exists(ResultPath(request.id)))
                {
                    File.Delete(RequestPath);
                    return;
                }

                if (!IsAllowedAction(request.action))
                {
                    File.Delete(RequestPath);
                    Complete(request, "rejected", "Action is not on the vegetation bridge whitelist.");
                    return;
                }

                File.Delete(RequestPath);
                if (request.action == "status")
                {
                    Complete(request, "completed", "Editor status captured without changing scenes.");
                    return;
                }

                string blocker = GetBlocker();
                if (!string.IsNullOrEmpty(blocker))
                {
                    Complete(request, "blocked", blocker);
                    return;
                }

                SessionState.SetString(ActiveKey, JsonUtility.ToJson(request));
                SessionState.SetString(StartedKey, DateTime.UtcNow.ToString("O"));
                SessionState.SetString(TestKey, string.Empty);
                restoredAt = EditorApplication.timeSinceStartup;
                WriteResult(request, "running", "Request accepted by the open Unity Editor.");
                WriteStatus();
                Execute(request);
            }
            catch (Exception exception)
            {
                lastBridgeError = exception.ToString();
                Request active = ActiveRequest();
                if (active != null) Complete(active, "failed", exception.ToString());
                else Debug.LogError("MAP_VEGETATION_EDITOR_BRIDGE " + exception.Message);
            }
        }

        private static void Execute(Request request)
        {
            executing = true;
            try
            {
                if (request.action == "edit-tests" || request.action == "play-tests")
                {
                    testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                    var filter = new Filter
                    {
                        testMode = request.action == "play-tests" ? TestMode.PlayMode : TestMode.EditMode,
                        groupNames = request.action == "play-tests"
                            ? new[] { "^MSC\\.Tests\\.PlayMode\\.WorldRemaster\\.ProductionWorldCellLayerPlayModeTests\\." }
                            : new[] { "^MSC\\.Tests\\.EditMode\\..*\\.(MapVegetation[^.]*|PackedWoody[^.]*|ProductionWorldCellLayer[^.]*|VegetationSystemTests|Phase1TreePlacementPolicyEditModeTests)\\." }
                    };
                    testApi.Execute(new ExecutionSettings(filter));
                    return;
                }

                switch (request.action)
                {
                    case "pilot": MapVegetationRebuild.RunPilotBatch(); break;
                    case "all": MapVegetationRebuild.RunAllBatch(); break;
                    case "validate": MapVegetationRebuild.ValidateAllBatch(); break;
                    case "capture": InvokeCapture(); break;
                }
                Complete(request, "completed", "Whitelisted vegetation action finished.");
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException : exception;
                Complete(request, "failed", cause.ToString());
                Debug.LogError("MAP_VEGETATION_EDITOR_BRIDGE " + cause.Message);
            }
            finally { executing = false; }
        }

        private static void InvokeCapture()
        {
            // This exact helper is the only reflection-dispatched operation.
            const string typeName = "MSC.Editor.Vegetation.MapVegetationVisualAudit";
            Type type = typeof(MapVegetationRebuild).Assembly.GetType(typeName);
            MethodInfo method = type?.GetMethod("CapturePilotBatch", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (method == null)
                throw new InvalidOperationException("The vegetation capture helper is not available: " + typeName + ".CapturePilotBatch");
            method.Invoke(null, null);
        }

        private static string GetBlocker()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Unity is in or entering Play mode. The bridge will not stop the user's session.";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "Unity is compiling/importing. Submit a new request after the Editor becomes idle.";
            if (EditorUtility.scriptCompilationFailed)
                return "Unity reports script compilation errors. Fix the errors before executing vegetation actions.";
            if (IsAnyTestRunActive()) return "Another Unity Test Runner job is already active.";
            var dirty = DirtyScenes();
            if (dirty.Count > 0) return "Unsaved scenes must be saved or discarded by their owner: " + string.Join(" | ", dirty);
            return string.Empty;
        }

        private static bool IsAnyTestRunActive()
        {
            MethodInfo method = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
            // Fail closed if the installed package no longer exposes the known
            // read-only query: never collide with another test job unknowingly.
            return method == null || (bool)method.Invoke(null, null);
        }

        private static List<string> DirtyScenes()
        {
            var result = new List<string>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty) result.Add(string.IsNullOrEmpty(scene.path) ? "(untitled) " + scene.name : scene.path);
            }
            return result;
        }

        private static bool IsAllowedAction(string action) => action == "status" || action == "edit-tests" ||
            action == "play-tests" || action == "pilot" || action == "all" || action == "validate" || action == "capture";

        private static Request ActiveRequest()
        {
            string text = SessionState.GetString(ActiveKey, string.Empty);
            return string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<Request>(text);
        }

        private static string ResultPath(string id) => Path.Combine(OutputFolder, "result-" + id + ".json");

        private static void Complete(Request request, string state, string message,
            ITestResultAdaptor tests = null, string xmlPath = "")
        {
            WriteResult(request, state, message, tests, xmlPath);
            Request active = ActiveRequest();
            if (active != null && active.id == request.id)
            {
                SessionState.EraseString(ActiveKey);
                SessionState.EraseString(TestKey);
                SessionState.EraseString(StartedKey);
            }
            WriteStatus();
        }

        private static void WriteResult(Request request, string state, string message,
            ITestResultAdaptor tests = null, string xmlPath = "")
        {
            var result = new Result
            {
                id = request.id, action = request.action, state = state, message = message,
                updatedUtc = DateTime.UtcNow.ToString("O"), startedUtc = SessionState.GetString(StartedKey, string.Empty),
                passCount = tests?.PassCount ?? 0, failCount = tests?.FailCount ?? 0,
                skipCount = tests?.SkipCount ?? 0, inconclusiveCount = tests?.InconclusiveCount ?? 0,
                durationSeconds = tests?.Duration ?? 0d, testsXml = xmlPath
            };
            WriteJson(ResultPath(request.id), result);
        }

        private static void WriteStatus()
        {
            Request active = ActiveRequest();
            var scenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i).path);
            WriteJson(Path.Combine(OutputFolder, "editor-status.json"), new Status
            {
                timestampUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                isPlaying = EditorApplication.isPlaying, isPlayingOrChanging = EditorApplication.isPlayingOrWillChangePlaymode,
                isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                compilationFailed = EditorUtility.scriptCompilationFailed,
                activeRequestId = active?.id ?? string.Empty, activeAction = active?.action ?? string.Empty,
                currentTest = SessionState.GetString(TestKey, string.Empty), lastBridgeError = lastBridgeError,
                openScenes = scenes.ToArray(), dirtyScenes = DirtyScenes().ToArray()
            });
        }

        private static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(OutputFolder);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private sealed class CallbackHandler : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { if (ActiveRequest() != null) WriteStatus(); }
            public void TestStarted(ITestAdaptor test)
            { if (ActiveRequest() != null) SessionState.SetString(TestKey, test.FullName); }
            public void TestFinished(ITestResultAdaptor result) { }
            public void OnError(string message)
            {
                Request request = ActiveRequest();
                if (request != null) Complete(request, "failed", message);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                Request request = ActiveRequest();
                if (request == null) return;
                string xml = Path.Combine(OutputFolder, "tests-" + request.id + ".xml");
                TestRunnerApi.SaveResultToFile(result, xml);
                bool passed = result.FailCount == 0 && result.InconclusiveCount == 0 && result.PassCount > 0;
                Complete(request, passed ? "completed" : "failed", result.ResultState + ": " + result.Message, result, xml);
            }
        }

        [Serializable] private sealed class Request
        {
            public string id = string.Empty;
            public string action = string.Empty;
        }
        [Serializable] private sealed class Result
        {
            public string id, action, state, message, updatedUtc, startedUtc, testsXml;
            public int passCount, failCount, skipCount, inconclusiveCount;
            public double durationSeconds;
        }
        [Serializable] private sealed class Status
        {
            public string timestampUtc, unityVersion, activeRequestId, activeAction, currentTest, lastBridgeError;
            public int processId;
            public bool isPlaying, isPlayingOrChanging, isCompiling, isUpdating, compilationFailed;
            public string[] openScenes, dirtyScenes;
        }
    }
}
