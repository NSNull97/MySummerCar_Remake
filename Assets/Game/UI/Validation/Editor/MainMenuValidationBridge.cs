using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.UI.Validation.EditorTools
{
    /// <summary>
    /// Narrow local UI test/capture entry point for an already open Editor.
    /// Requests cannot execute arbitrary code or choose output paths. Only an
    /// explicitly identified owned Editor may discard its empty untitled scene.
    /// Test requests refuse an active play session or unsaved scene. Capture only
    /// reads the currently visible main menu; it never routes or changes settings.
    /// An explicit owned-process review request may open clean Bootstrap and play it.
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuValidationBridge
    {
        private const string ActiveKey = "MSC.MainMenuValidation.Active";
        private const string StartedKey = "MSC.MainMenuValidation.Started";
        private const string CurrentTestKey = "MSC.MainMenuValidation.Test";
        private static readonly string OutputDirectory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Artifacts/MainMenuRedesign"));
        private static readonly Regex SafeId = new Regex("^[A-Za-z0-9_-]{1,80}$");
        private static readonly Callbacks TestCallbacks = new Callbacks();
        private static double nextPoll;
        private static double loadedAt;

        static MainMenuValidationBridge()
        {
            loadedAt = EditorApplication.timeSinceStartup;
            TestRunnerApi.RegisterTestCallback(TestCallbacks);
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }

        private static void BeforeReload()
        {
            EditorApplication.update -= Poll;
            TestRunnerApi.UnregisterTestCallback(TestCallbacks);
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2d;
            try
            {
                WriteStatus();
                Request active = GetActive();
                if (active != null)
                {
                    if (active.action == "capture-main-menu")
                    {
                        string capture = CapturePath(active);
                        if (File.Exists(capture) && new FileInfo(capture).Length > 0)
                            Finish(active, "completed", "Visible main-menu screenshot captured without changing the session.");
                        else if (DateTime.TryParse(SessionState.GetString(StartedKey, ""), out DateTime started) &&
                                 DateTime.UtcNow - started.ToUniversalTime() > TimeSpan.FromSeconds(30))
                            Finish(active, "failed", "Unity did not write the screenshot before the 30 second timeout.");
                    }
                    else if (!IsTestActive() && !EditorApplication.isPlayingOrWillChangePlaymode &&
                             !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                             EditorApplication.timeSinceStartup - loadedAt > 45d)
                        Finish(active, "interrupted", "No active test run after domain reload; no request was repeated.");
                    return;
                }

                string requestPath = Path.Combine(OutputDirectory, "editor-request.json");
                if (!File.Exists(requestPath)) return;
                Request request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
                if (request == null || !SafeId.IsMatch(request.id ?? "")) return;
                File.Delete(requestPath);
                if (File.Exists(ResultPath(request))) return;
                if (request.action == "status")
                {
                    Finish(request, "completed", "Read-only Editor status captured.");
                    return;
                }
                if (request.action == "cleanup-owned-empty-scene")
                {
                    CleanupOwnedEmptyScene(request);
                    return;
                }
                if (request.action == "open-owned-menu-review")
                {
                    OpenOwnedMenuReview(request);
                    return;
                }
                bool captureRequest = request.action == "capture-main-menu";
                if (!captureRequest && request.action != "edit-tests" && request.action != "play-tests")
                {
                    Finish(request, "rejected", "Only status, UI tests, capture-main-menu, owned menu review and owned-empty-scene cleanup are allowed.");
                    return;
                }
                string blocker = GetBlocker(captureRequest);
                if (!string.IsNullOrEmpty(blocker))
                {
                    Finish(request, "blocked", blocker);
                    return;
                }
                SessionState.SetString(ActiveKey, JsonUtility.ToJson(request));
                SessionState.SetString(StartedKey, DateTime.UtcNow.ToString("O"));
                SessionState.SetString(CurrentTestKey, "");
                loadedAt = EditorApplication.timeSinceStartup;
                WriteResult(request, "running", "Accepted by the open Unity Editor.");
                if (captureRequest)
                    ScreenCapture.CaptureScreenshot(CapturePath(request));
                else
                    ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
                    {
                        testMode = request.action == "play-tests" ? TestMode.PlayMode : TestMode.EditMode,
                        groupNames = request.action == "play-tests"
                            ? new[] { "^MSC\\.Tests\\.PlayMode\\.UIPresentation\\." }
                            : new[] { "^MSC\\.Tests\\.EditMode\\.(UIRuntime|UIEditor)\\." }
                    }));
            }
            catch (Exception exception)
            {
                Request active = GetActive();
                if (active != null) Finish(active, "failed", exception.ToString());
                else Debug.LogError("MSC_MAIN_MENU_VALIDATION " + exception.Message);
            }
        }

        private static string GetBlocker(bool capture)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "Unity is compiling/importing. Submit a new ID after it becomes idle.";
            if (EditorUtility.scriptCompilationFailed) return "Unity reports script compilation errors.";
            if (IsTestActive()) return "Another Unity Test Runner job is active.";
            if (capture)
            {
                if (!EditorApplication.isPlaying) return "A visible main menu in Play mode is required for a read-only capture.";
                GameUiRoot[] roots = UnityEngine.Object.FindObjectsByType<GameUiRoot>(FindObjectsSortMode.None);
                if (roots.Length != 1 || roots[0].CurrentRoute != UiRouteId.MainMenu)
                    return "Exactly one active GameUiRoot must already show MainMenu. The bridge will not navigate.";
                return string.Empty;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Unity is in or entering Play mode. The bridge will not stop the user's session.";
            string[] dirty = DirtyScenes();
            return dirty.Length == 0 ? string.Empty : "Unsaved scenes require their owner: " + string.Join(" | ", dirty);
        }

        private static void OpenOwnedMenuReview(Request request)
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            string blocker = GetBlocker(capture: false);
            if (request.ownedProcessId != process.Id ||
                request.ownedProcessStartedUtc != process.StartTime.ToUniversalTime().ToString("O") ||
                !string.IsNullOrEmpty(blocker))
            {
                Finish(request, "blocked", "Owned idle Editor with clean scenes is required. " + blocker);
                return;
            }
            const string bootstrap = "Assets/Game/Bootstrap/Bootstrap.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrap) == null)
            {
                Finish(request, "blocked", "Canonical Bootstrap scene is missing.");
                return;
            }
            EditorSceneManager.OpenScene(bootstrap, OpenSceneMode.Single);
            // The owned review explicitly shows the result. A closed Game
            // tab has no backbuffer for the subsequent read-only screenshot.
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                gameView.Show();
                gameView.Focus();
            }
            Finish(request, "completed", "Opened clean Bootstrap and requested Play Mode for menu review; no scene was saved.");
            EditorApplication.EnterPlaymode();
        }

        private static void CleanupOwnedEmptyScene(Request request)
        {
            // A failed test can leave an empty untitled scene marked dirty.
            // This is an explicit cleanup of an agent-owned Editor, never a
            // relaxation of the normal test/capture dirty-scene guard.
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            string started = process.StartTime.ToUniversalTime().ToString("O");
            if (request.ownedProcessId != process.Id || request.ownedProcessStartedUtc != started ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || IsTestActive() || SceneManager.sceneCount != 1)
            {
                Finish(request, "blocked", "Owned process identity and idle single-scene state are required.");
                return;
            }
            Scene scene = SceneManager.GetSceneAt(0);
            GameObject[] roots = scene.GetRootGameObjects();
            if (!string.IsNullOrEmpty(scene.path) || roots.Length != 0)
            {
                Finish(request, "blocked", "Only a genuinely empty untitled test scene may be discarded. Roots: " +
                    string.Join(", ", Array.ConvertAll(roots, item => item.name)));
                return;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Finish(request, "completed", "Discarded the explicitly owned empty test scene; no asset or named scene was saved.");
        }


        private static bool IsTestActive()
        {
            MethodInfo method = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
            return method == null || (bool)method.Invoke(null, null);
        }

        private static string[] DirtyScenes()
        {
            var result = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) result.Add(string.IsNullOrEmpty(scene.path) ? "(untitled) " + scene.name : scene.path);
            }
            return result.ToArray();
        }

        private static Request GetActive()
        {
            string json = SessionState.GetString(ActiveKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Request>(json);
        }

        private static string ResultPath(Request request) => Path.Combine(OutputDirectory, "result-" + request.id + ".json");
        private static string CapturePath(Request request) => Path.Combine(OutputDirectory, "MainMenu-" + request.id + ".png");

        private static void Finish(Request request, string state, string message, ITestResultAdaptor tests = null)
        {
            WriteResult(request, state, message, tests);
            if (GetActive()?.id == request.id)
            {
                SessionState.EraseString(ActiveKey);
                SessionState.EraseString(StartedKey);
                SessionState.EraseString(CurrentTestKey);
            }
            WriteStatus();
        }

        private static void WriteResult(Request request, string state, string message, ITestResultAdaptor tests = null)
        {
            WriteJson(ResultPath(request), new Result
            {
                id = request.id, action = request.action, state = state, message = message,
                updatedUtc = DateTime.UtcNow.ToString("O"),
                passed = tests?.PassCount ?? 0, failed = tests?.FailCount ?? 0,
                skipped = tests?.SkipCount ?? 0, inconclusive = tests?.InconclusiveCount ?? 0,
                durationSeconds = tests?.Duration ?? 0d,
                outputPath = tests != null ? Path.Combine(OutputDirectory, "tests-" + request.id + ".xml") :
                    request.action == "capture-main-menu" ? CapturePath(request) : string.Empty,
                width = Screen.width, height = Screen.height
            });
        }

        private static void WriteStatus()
        {
            Request active = GetActive();
            WriteJson(Path.Combine(OutputDirectory, "editor-status.json"), new Status
            {
                timestampUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                isPlaying = EditorApplication.isPlaying, isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating, compilationFailed = EditorUtility.scriptCompilationFailed,
                activeId = active?.id ?? "", activeAction = active?.action ?? "",
                currentTest = SessionState.GetString(CurrentTestKey, ""), dirtyScenes = DirtyScenes()
            });
        }

        private static void WriteJson(string path, object data)
        {
            Directory.CreateDirectory(OutputDirectory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private sealed class Callbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test)
            {
                if (GetActive() != null) SessionState.SetString(CurrentTestKey, test.FullName);
            }
            public void TestFinished(ITestResultAdaptor result) { }
            public void OnError(string message)
            {
                Request request = GetActive();
                if (request != null) Finish(request, "failed", message);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                Request request = GetActive();
                if (request == null || request.action == "capture-main-menu") return;
                TestRunnerApi.SaveResultToFile(result, Path.Combine(OutputDirectory, "tests-" + request.id + ".xml"));
                bool passed = result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0;
                Finish(request, passed ? "completed" : "failed", result.ResultState + ": " + result.Message, result);
            }
        }

        [Serializable] private sealed class Request
        {
            public string id, action, ownedProcessStartedUtc;
            public int ownedProcessId;
        }
        [Serializable] private sealed class Result
        {
            public string id, action, state, message, updatedUtc, outputPath;
            public int passed, failed, skipped, inconclusive, width, height;
            public double durationSeconds;
        }
        [Serializable] private sealed class Status
        {
            public string timestampUtc, unityVersion, activeId, activeAction, currentTest;
            public bool isPlaying, isCompiling, isUpdating, compilationFailed;
            public string[] dirtyScenes;
        }
    }
}
