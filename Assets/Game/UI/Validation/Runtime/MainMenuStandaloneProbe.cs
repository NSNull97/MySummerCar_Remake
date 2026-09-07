using System;
using System.Collections;
using System.IO;
using MSC.Save;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using MSC.UI.Runtime.Settings;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MSC.UI.StandaloneValidation
{
    /// <summary>
    /// Explicit UI-only fixture scene. Never attached by production composition.
    /// It uses real widgets/assets and an empty native save store in a fresh
    /// evidence directory; no production world or player save is loaded.
    /// </summary>
    public sealed class MainMenuStandaloneProbe : MonoBehaviour
    {
        [SerializeField] private Texture2D backdrop;
        [SerializeField] private Texture2D logo;
        [SerializeField] private Shader blur;
        [SerializeField] private MainMenuVehicleModel menuVehiclePrefab;
        [SerializeField] private MainMenuEnvironmentModel menuEnvironmentPrefab;
        [SerializeField] private Shader menuPreviewBackdropShader;
        [SerializeField] private InputActionAsset playerActions;
        [SerializeField] private InputActionAsset vehicleActions;
        private const int Samples = 300;
        private readonly double[] frameMilliseconds = new double[Samples];
        private readonly long[] frameAllocations = new long[Samples];
        private GameUiRoot root;
        private string output;
        private InputActionAsset playerClone;
        private InputActionAsset vehicleClone;
        private int errorCount;
        private int warningCount;
        private int cameraRenderEvents;
        private readonly Result result = new Result();

#if UNITY_EDITOR
        public void Configure(Texture2D configuredBackdrop, Texture2D configuredLogo,
            Shader configuredBlur, InputActionAsset configuredPlayer, InputActionAsset configuredVehicle,
            MainMenuVehicleModel configuredMenuVehiclePrefab = null, Shader configuredMenuPreviewBackdropShader = null,
            MainMenuEnvironmentModel configuredMenuEnvironmentPrefab = null)
        {
            backdrop = configuredBackdrop;
            logo = configuredLogo;
            blur = configuredBlur;
            menuVehiclePrefab = configuredMenuVehiclePrefab;
            menuEnvironmentPrefab = configuredMenuEnvironmentPrefab;
            menuPreviewBackdropShader = configuredMenuPreviewBackdropShader;
            playerActions = configuredPlayer;
            vehicleActions = configuredVehicle;
        }
#endif

        private void Awake()
        {
            Application.logMessageReceived += RecordLog;
            RenderPipelineManager.endCameraRendering += RecordCameraRender;
        }

        private IEnumerator Start()
        {
            string requestedOutput = Argument("-msc-main-menu-probe");
            if (string.IsNullOrEmpty(requestedOutput))
            {
                Debug.LogError("UI fixture requires -msc-main-menu-probe <evidence-directory>.");
                Application.Quit(2);
                yield break;
            }

            output = Path.Combine(Path.GetFullPath(requestedOutput),
                "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            bool initialized = TryInitialize();
            if (!initialized)
            {
                Finish(false);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 30f;
            while ((root.CurrentRoute != UiRouteId.MainMenu || Screen.width != 1920 || Screen.height != 1080) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (root.CurrentRoute != UiRouteId.MainMenu || Screen.width != 1920 || Screen.height != 1080)
            {
                result.failure = "MainMenu or 1920x1080 backbuffer did not become ready.";
                Finish(false);
                yield break;
            }

            // High-FPS players can render 120 frames before the menu fade has
            // settled. Require both a frame warmup and real elapsed time.
            double warmupEnds = Time.realtimeSinceStartupAsDouble + 1d;
            for (int frame = 0; frame < 120 || Time.realtimeSinceStartupAsDouble < warmupEnds; frame++)
                yield return null;
            // Capture before profiling; screenshot readback/PNG encoding must
            // not inflate the idle observation window.
            result.screenshot = Path.Combine(output, "MainMenu-1920x1080.png");
            ScreenCapture.CaptureScreenshot(result.screenshot);
            deadline = Time.realtimeSinceStartup + 30f;
            while (!File.Exists(result.screenshot) && Time.realtimeSinceStartup < deadline) yield return null;
            InspectCapture();
            if (!result.screenshotContainsMenu)
            {
                result.failure = "The backbuffer lacks visible logo/action content; rendering and performance evidence are invalid.";
                Finish(false);
                yield break;
            }
            for (int frame = 0; frame < 60; frame++) yield return null;

            ProfilerRecorder gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            result.gcCounterAvailable = gc.Valid;
            double started = Time.realtimeSinceStartupAsDouble;
            int cameraEventsAtStart = cameraRenderEvents;
            for (int sample = 0; sample < Samples; sample++)
            {
                yield return null;
                frameMilliseconds[sample] = Time.unscaledDeltaTime * 1000d;
                frameAllocations[sample] = gc.Valid ? gc.LastValue : -1;
            }
            result.sampledSeconds = Time.realtimeSinceStartupAsDouble - started;
            result.cameraRenderEventsDuringSamples = cameraRenderEvents - cameraEventsAtStart;
            gc.Dispose();

            double sum = 0d;
            long allocationSum = 0;
            for (int sample = 0; sample < Samples; sample++)
            {
                sum += frameMilliseconds[sample];
                if (frameAllocations[sample] > 0) allocationSum += frameAllocations[sample];
            }
            Array.Sort(frameMilliseconds);
            Array.Sort(frameAllocations);
            result.sampleCount = Samples;
            result.meanFrameMilliseconds = sum / Samples;
            result.p95FrameMilliseconds = frameMilliseconds[(int)(Samples * 0.95f) - 1];
            result.maximumFrameMilliseconds = frameMilliseconds[Samples - 1];
            result.totalGcAllocatedBytes = result.gcCounterAvailable ? allocationSum : -1;
            result.medianGcAllocatedBytesPerFrame = frameAllocations[Samples / 2];
            result.maximumGcAllocatedBytesPerFrame = frameAllocations[Samples - 1];
            result.developerButtonPresent = HasActiveButton("DevTools");
            result.developerGateMatchesBuild = result.developerButtonPresent == Debug.isDebugBuild;
            result.continueDisabledForEmptyStore = IsButtonDisabled("Continue");
            result.loadBrowserEnabledForEmptyStore = !IsButtonDisabled("LoadGame");
            result.backdropMode = root.ActiveBackdropModeName;
            if (result.cameraRenderEventsDuringSamples < Samples - 1)
                result.failure = "The fixture camera did not render throughout the sample window; timings are not valid rendering evidence.";
            Finish(errorCount == 0 && result.developerGateMatchesBuild &&
                result.continueDisabledForEmptyStore && result.loadBrowserEnabledForEmptyStore &&
                root.CurrentRoute == UiRouteId.MainMenu && File.Exists(result.screenshot) &&
                result.cameraRenderEventsDuringSamples >= Samples - 1);
        }

        private bool TryInitialize()
        {
            try
            {
                if (backdrop == null || logo == null || blur == null || playerActions == null || vehicleActions == null)
                    throw new InvalidOperationException("Canonical UI fixture assets are missing.");
                string settingsPath = Path.Combine(output, "isolated-preferences.json");
                UiSettingsDocument preferences = UiSettingsDefaults.Create();
                preferences.Gameplay.LanguageId = "ru-RU";
                preferences.Graphics.DisplayMode = UiDisplayMode.Windowed;
                preferences.Graphics.ResolutionWidth = 1920;
                preferences.Graphics.ResolutionHeight = 1080;
                preferences.Graphics.VSync = false;
                new UiSettingsJsonStore(settingsPath).Save(preferences);
                var saves = new SaveCoordinator(new FileSystemSaveStorage(Path.Combine(output, "empty-native-saves")),
                    new SaveParticipantRegistry(Array.Empty<ISaveParticipant>()),
                    new CurrentVersionSaveMigrationPipeline(), new DeferredStableEntityStore());
                playerClone = Instantiate(playerActions);
                vehicleClone = Instantiate(vehicleActions);
                root = new GameObject("UI fixture: actual GameUiRoot").AddComponent<GameUiRoot>();
                root.transform.SetParent(transform, false);
                root.Initialize(new GameUiDependencies(() => true, null, null, playerClone, vehicleClone,
                    null, settingsPath, true, menuBackdropTexture: backdrop, menuLogoTexture: logo,
                    uiBlurShader: blur, saveService: saves, openDeveloperTools: WriteUiDiagnostics,
                    menuVehiclePrefab: menuVehiclePrefab, menuPreviewBackdropShader: menuPreviewBackdropShader,
                    menuEnvironmentPrefab: menuEnvironmentPrefab));
                Application.targetFrameRate = -1;
                Application.runInBackground = true;
                result.fixture = "Actual GameUiRoot/assets; no gameplay world, no audio backend, empty native store.";
                result.developerBackend = "Fixture diagnostics writer; production console is not instantiated.";
                result.preferencesPath = settingsPath;
                result.emptySaveStore = saves.EnumerateSlots().Count == 0;
                return true;
            }
            catch (Exception exception)
            {
                result.failure = exception.ToString();
                Debug.LogError("UI fixture startup failed: " + exception.Message);
                return false;
            }
        }

        private void WriteUiDiagnostics()
        {
            File.WriteAllText(Path.Combine(output, "developer-ui-diagnostics.json"), JsonUtility.ToJson(new Diagnostics
            {
                route = root.CurrentRoute.ToString(), width = Screen.width, height = Screen.height,
                registeredGlassSurfaces = root.RegisteredGlassSurfaceCount,
                managedHeapBytes = GC.GetTotalMemory(false)
            }, true));
        }

        private void InspectCapture()
        {
            result.focusedAtCapture = Application.isFocused;
            result.renderPipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "null";
            result.cameraRenderEventsBeforeCapture = cameraRenderEvents;
            result.renderedFrameCountAtCapture = Time.renderedFrameCount;
            result.batchMode = Application.isBatchMode;
            result.runInBackground = Application.runInBackground;
            Camera[] cameras = Camera.allCameras;
            result.activeCameras = new string[cameras.Length];
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                result.activeCameras[index] = camera.name + " enabled=" + camera.isActiveAndEnabled +
                    " mask=" + camera.cullingMask + " target=" + camera.targetDisplay +
                    " pixels=" + camera.pixelWidth + "x" + camera.pixelHeight +
                    " texture=" + (camera.targetTexture != null);
            }
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(false);
            result.activeCanvases = new string[canvases.Length];
            for (int index = 0; index < canvases.Length; index++)
                result.activeCanvases[index] = canvases[index].name + " enabled=" + canvases[index].isActiveAndEnabled +
                    " mode=" + canvases[index].renderMode + " target=" + canvases[index].targetDisplay;

            if (!File.Exists(result.screenshot)) return;
            var captured = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!captured.LoadImage(File.ReadAllBytes(result.screenshot))) return;
                Color32[] pixels = captured.GetPixels32();
                result.logoBrightSamples = CountBrightSamples(pixels, captured.width, captured.height,
                    new Rect(0.04f, 0.03f, 0.22f, 0.28f));
                result.actionBrightSamples = CountBrightSamples(pixels, captured.width, captured.height,
                    new Rect(0.73f, 0.20f, 0.22f, 0.57f));
                result.screenshotContainsMenu = result.logoBrightSamples > 20 && result.actionBrightSamples > 8;
            }
            finally { Destroy(captured); }
        }

        private void RecordCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != null && camera.transform.IsChildOf(transform)) cameraRenderEvents++;
        }

        private static int CountBrightSamples(Color32[] pixels, int width, int height, Rect topLeftRegion)
        {
            int count = 0;
            for (int y = (int)(height * topLeftRegion.yMin); y < height * topLeftRegion.yMax; y += 4)
                for (int x = (int)(width * topLeftRegion.xMin); x < width * topLeftRegion.xMax; x += 4)
                {
                    Color32 pixel = pixels[(height - 1 - y) * width + x];
                    if (Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) > 120) count++;
                }
            return count;
        }

        private bool HasActiveButton(string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name) return true;
            return false;
        }

        private bool IsButtonDisabled(string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name) return !buttons[index].IsInteractable();
            return true;
        }

        private void Finish(bool passed)
        {
            result.passed = passed;
            result.developmentBuild = Debug.isDebugBuild;
            result.width = Screen.width;
            result.height = Screen.height;
            result.errorCount = errorCount;
            result.warningCount = warningCount;
            result.unityVersion = Application.unityVersion;
            result.timestampUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("MSC_MAIN_MENU_STANDALONE_RESULT " + output + " passed=" + passed);
            Application.Quit(passed ? 0 : 2);
        }

        private void RecordLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorCount++;
            if (type == LogType.Warning) warningCount++;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
                if (args[index] == name) return args[index + 1];
            return string.Empty;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= RecordLog;
            RenderPipelineManager.endCameraRendering -= RecordCameraRender;
            if (root != null) root.EndGameSession();
            if (playerClone != null) Destroy(playerClone);
            if (vehicleClone != null) Destroy(vehicleClone);
        }

        [Serializable] private sealed class Diagnostics
        {
            public string route;
            public int width, height, registeredGlassSurfaces;
            public long managedHeapBytes;
        }

        [Serializable] private sealed class Result
        {
            public string fixture, developerBackend, preferencesPath, timestampUtc, unityVersion, screenshot, backdropMode, failure, renderPipeline;
            public bool passed, developmentBuild, gcCounterAvailable, emptySaveStore, developerButtonPresent,
                developerGateMatchesBuild, continueDisabledForEmptyStore, loadBrowserEnabledForEmptyStore,
                screenshotContainsMenu, focusedAtCapture, batchMode, runInBackground;
            public string[] activeCameras, activeCanvases;
            public int logoBrightSamples, actionBrightSamples, cameraRenderEventsBeforeCapture,
                cameraRenderEventsDuringSamples, renderedFrameCountAtCapture;
            public int width, height, errorCount, warningCount, sampleCount;
            public double sampledSeconds, meanFrameMilliseconds, p95FrameMilliseconds, maximumFrameMilliseconds;
            public long totalGcAllocatedBytes, medianGcAllocatedBytesPerFrame, maximumGcAllocatedBytesPerFrame;
        }
    }
}
