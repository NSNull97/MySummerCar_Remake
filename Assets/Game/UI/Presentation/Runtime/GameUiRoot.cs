using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSC.Audio;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using MSC.UI.Runtime.Capabilities;
using MSC.UI.Runtime.Localization;
using MSC.UI.Runtime.Routing;
using MSC.UI.Runtime.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed partial class GameUiRoot : MonoBehaviour, IGameSessionLifetime
    {
        private readonly Dictionary<UiRouteId, GameObject> routes =
            new Dictionary<UiRouteId, GameObject>();
        private readonly Dictionary<UiRouteId, GameObject> routeSelections =
            new Dictionary<UiRouteId, GameObject>();
        private readonly Dictionary<IGameplayInputGate, bool> savedInputGateStates =
            new Dictionary<IGameplayInputGate, bool>();
        private readonly List<IUiVisibilityGate> visibilityGates =
            new List<IUiVisibilityGate>();
        private readonly Queue<float> frameSamples = new Queue<float>(240);

        private GameUiDependencies dependencies;
        private UiVisualAssets visualAssets;
        private UiFactory factory;
        private UiTextCatalog textCatalog;
        private UiCapabilitySet capabilities;
        private UiSettingsJsonStore settingsStore;
        private UiSettingsTransactionService settings;
        private Canvas canvas;
        private CanvasScaler canvasScaler;
        private RectTransform referenceFrame;
        private RectTransform scaleRoot;
        private EventSystem ownedEventSystem;
        private InputAction pauseAction;
        private UiRouteId currentRoute = UiRouteId.Boot;
        private UiRouteId settingsReturnRoute = UiRouteId.MainMenu;
        private UiRouteId confirmationReturnRoute = UiRouteId.MainMenu;
        private UiRouteId saveStatusReturnRoute = UiRouteId.MainMenu;
        private bool initialized;
        private bool worldReadyHandled;
        private bool gameplayStarted;
        private Coroutine gameplayActivationCoroutine;
        private bool gameplaySuspended;
        private bool reviewDataEnabled;
        private bool sessionEnded;
        private float savedTimeScale = 1f;
        private bool savedClockPaused;
        private float noticeUntilUnscaledTime;
        private GameObject noticeRoot;
        private Text noticeText;
        private Text hudClockText;
        private Text hudDayText;
        private Text hudDateText;
        private Text hudMoneyText;
        private readonly List<NeedHudBinding> needHudBindings =
            new List<NeedHudBinding>();
        private ulong lastClockRevision = ulong.MaxValue;
        private UiLocaleFormatter localeFormatter;
        private GameObject lastSelectedUiObject;

        public bool IsInitialized => initialized;

        public UiRouteId CurrentRoute => currentRoute;

        public bool ReviewDataEnabled => reviewDataEnabled;

        public bool UsesPixelPerfectCanvas => canvas != null && canvas.pixelPerfect;

        public bool ProceduralAssetsUseFractionalAlphaCoverage =>
            visualAssets != null && visualAssets.UsesFractionalAlphaCoverage;

        internal bool IsWorldReadyForCapture =>
            initialized && dependencies != null && dependencies.IsWorldReady();

        public UiSettingsDocument AppliedSettings => settings?.Applied;

        public void Initialize(GameUiDependencies configuredDependencies)
        {
            if (initialized)
            {
                throw new InvalidOperationException("Game UI has already been initialized.");
            }

            dependencies = configuredDependencies ??
                throw new ArgumentNullException(nameof(configuredDependencies));
            visualAssets = new UiVisualAssets();
            factory = new UiFactory(visualAssets, PostUiConfirm);
            textCatalog = new UiTextCatalog();
            capabilities = UiCapabilitySet.CreateBounded08ADefaults(
                Debug.isDebugBuild || Application.isEditor);

            settingsStore = new UiSettingsJsonStore(dependencies.SettingsPath);
            UiSettingsLoadResult loaded = settingsStore.LoadOrCreate();
            settings = new UiSettingsTransactionService(loaded.Document);
            textCatalog.LocaleId = loaded.Document.Gameplay.LanguageId;
            BuildCanvas();
            ApplyDocument(loaded.Document, persist: false);
            LoadBindingOverrides(loaded.Document.Controls);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long routeAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            var routeBuildTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
            BuildAllRoutes();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            routeBuildTimer.Stop();
            Debug.Log(
                $"M08A UI PERF initial route build: {routeBuildTimer.Elapsed.TotalMilliseconds:F3} ms; " +
                $"thread allocations={GC.GetAllocatedBytesForCurrentThread() - routeAllocationStart} bytes; " +
                $"routes={routes.Count}.",
                this);
#endif
            BindPauseAction();
            ShowRoute(UiRouteId.Loading);
            initialized = true;
            Ui08AReviewCaptureProbe.TryAttach(this);
        }

        public void EndGameSession()
        {
            if (sessionEnded)
            {
                return;
            }

            sessionEnded = true;
            if (gameplayActivationCoroutine != null)
            {
                StopCoroutine(gameplayActivationCoroutine);
                gameplayActivationCoroutine = null;
            }
            UnbindPauseAction();
            RestoreGameplayState();
        }

        public void SetReviewDataEnabled(bool enabled)
        {
            reviewDataEnabled = enabled;
            lastClockRevision = ulong.MaxValue;
            RefreshHud(force: true);
        }

        public void ShowReviewScreen(UiRouteId route)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!UiRouteCatalog.Get(route).HasLockedVisualReference)
            {
                throw new ArgumentException("The requested route has no locked 08A reference.", nameof(route));
            }

            reviewDataEnabled = true;
            textCatalog.LocaleId = "en-US";
            if (scaleRoot != null)
            {
                scaleRoot.localScale = Vector3.one;
            }

            RebuildLocalizedRoutes();
            ShowRoute(route);
            SetGameplaySuspended(route != UiRouteId.InGameHud);
#else
            throw new InvalidOperationException("Review routes are not available in release builds.");
#endif
        }

        public void ShowReferencePendingReviewScreen(UiRouteId route)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UiRouteDefinition definition = UiRouteCatalog.Get(route);
            if (definition.HasLockedVisualReference || !IsReferencePendingRoute(route))
            {
                throw new ArgumentException(
                    "The requested route is not an approved 08A ReferencePending review target.",
                    nameof(route));
            }

            reviewDataEnabled = true;
            textCatalog.LocaleId = "en-US";
            if (scaleRoot != null)
            {
                scaleRoot.localScale = Vector3.one;
            }

            RebuildLocalizedRoutes();
            ShowRoute(route);
            SetGameplaySuspended(route != UiRouteId.InGameHud);
#else
            throw new InvalidOperationException("Review routes are not available in release builds.");
#endif
        }

        private void Update()
        {
            if (!initialized || sessionEnded)
            {
                return;
            }

            RecordFrameSample();
            TrackUiSelectionAudio();
            if (!worldReadyHandled && dependencies.IsWorldReady())
            {
                worldReadyHandled = true;
                if (dependencies.StartInMainMenu)
                {
                    EnterMainMenu();
                }
                else
                {
                    EnterGameplay();
                }
            }

            if (currentRoute == UiRouteId.InGameHud)
            {
                RefreshHud(force: false);
            }

            if (noticeRoot != null &&
                noticeRoot.activeSelf &&
                Time.unscaledTime >= noticeUntilUnscaledTime)
            {
                noticeRoot.SetActive(false);
                if (noticeText != null)
                {
                    noticeText.text = string.Empty;
                }
            }
        }

        private void OnDestroy()
        {
            DisposeActiveRebindForShutdown();
            DisposeBlurredBackdropTexture();
            EndGameSession();
            if (ownedEventSystem != null)
            {
                Destroy(ownedEventSystem.gameObject);
            }

            visualAssets?.Dispose();
            visualAssets = null;
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject(
                "M08A_GameUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, worldPositionStays: false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            canvas.pixelPerfect = true;
            canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(
                UiThemeTokens.ReferenceWidth,
                UiThemeTokens.ReferenceHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            RectTransform glassReferenceFrame = CreateBackdropLayer(canvasObject.transform);

            GameObject frameObject = factory.CreateObject("ReferenceFrame_1672x941", canvasObject.transform);
            referenceFrame = factory.Stretch(frameObject);
            var fitter = frameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = UiThemeTokens.ReferenceAspect;

            GameObject scaleObject = factory.CreateObject("AccessibleScaleRoot", referenceFrame);
            scaleRoot = factory.Stretch(scaleObject);
            factory.ConfigureGlass(glassReferenceFrame, RegisterGlassSurface);

            if (EventSystem.current == null)
            {
                GameObject eventObject = new GameObject(
                    "M08A_EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, worldPositionStays: false);
                ownedEventSystem = eventObject.GetComponent<EventSystem>();
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void BuildAllRoutes()
        {
            BuildLoadingRoute();
            BuildMainMenuRoute();
            BuildGraphicsRoute();
            BuildAudioRoute();
            BuildControlsRoute();
            BuildGameplayRoute();
            BuildAccessibilityRoute();
            BuildModsRoute();
            BuildPauseRoute();
            BuildConfirmationDialogRoute();
            BuildSaveStatusRoute();
            BuildHudRoute();
            BuildNotice();
        }

        private GameObject CreateRoute(UiRouteId id)
        {
            GameObject route = factory.CreateObject(id.ToString(), scaleRoot);
            factory.Stretch(route);

            route.SetActive(false);
            routes[id] = route;
            return route;
        }

        private void ShowRoute(UiRouteId route)
        {
            RememberCurrentRouteSelection();
            foreach (KeyValuePair<UiRouteId, GameObject> item in routes)
            {
                if (item.Value != null)
                {
                    item.Value.SetActive(item.Key == route);
                }
            }

            currentRoute = route;
            UpdateBlurredBackdrop(route);
            if (routes.TryGetValue(route, out GameObject root))
            {
                if (!TryRestoreRouteSelection(route))
                {
                    UiFactory.SelectFirst(root);
                }
            }
        }

        private void RememberCurrentRouteSelection()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null ||
                !routes.TryGetValue(currentRoute, out GameObject route) ||
                route == null ||
                (selected.transform != route.transform && !selected.transform.IsChildOf(route.transform)))
            {
                return;
            }

            routeSelections[currentRoute] = selected;
        }

        private bool TryRestoreRouteSelection(UiRouteId route)
        {
            if (EventSystem.current == null ||
                !routeSelections.TryGetValue(route, out GameObject selected) ||
                selected == null ||
                !selected.activeInHierarchy)
            {
                routeSelections.Remove(route);
                return false;
            }

            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable == null || !selectable.IsInteractable())
            {
                routeSelections.Remove(route);
                return false;
            }

            EventSystem.current.SetSelectedGameObject(selected);
            return true;
        }

        private void EnterMainMenu()
        {
            settingsReturnRoute = UiRouteId.MainMenu;
            SetGameplaySuspended(true);
            ShowRoute(UiRouteId.MainMenu);
        }

        private void EnterGameplay()
        {
            if (!gameplayStarted && !TryActivateGameplaySession())
            {
                return;
            }

            gameplayStarted = true;
            SetGameplaySuspended(false);
            ShowRoute(UiRouteId.InGameHud);
            RefreshHud(force: true);
        }

        private bool TryActivateGameplaySession()
        {
            IGameplaySessionGate gate = dependencies.GameplaySessionGate;
            if (gate == null || gate.IsGameplayActive)
            {
                return true;
            }

            if (gate.TryActivateGameplay(out string failure))
            {
                return true;
            }

            Debug.LogError(
                "M08A could not activate the prepared gameplay session: " + failure,
                this);
            ShowNotice("ui.notice.gameplay_activation_failed");
            return false;
        }

        private void BeginBoundedNewGameSession()
        {
            // Milestone 08A has no persistent save/profile creation provider.
            // The main menu is shown only over a freshly composed Bootstrap
            // world (and Return to Main Menu reloads that scene), so this is the
            // explicit bounded new-session boundary rather than a fake save flow.
            if (gameplayStarted || gameplayActivationCoroutine != null)
            {
                Debug.LogWarning(
                    "M08A ignored a duplicate New Game request in the active local session.",
                    this);
                return;
            }

            gameplayActivationCoroutine = StartCoroutine(
                ActivateNewGameAfterLoadingFrame());
        }

        private IEnumerator ActivateNewGameAfterLoadingFrame()
        {
            SetGameplaySuspended(true);
            ShowRoute(UiRouteId.Loading);

            // Let the loading route reach the backbuffer before the bounded,
            // synchronous production-world activation can occupy the main
            // thread. This avoids an unexplained frozen menu after New Game.
            yield return null;

            if (!sessionEnded)
            {
                EnterGameplay();
                if (!gameplayStarted)
                {
                    EnterMainMenu();
                }
            }

            gameplayActivationCoroutine = null;
        }

        private void EnterPause()
        {
            if (!gameplayStarted)
            {
                return;
            }

            settingsReturnRoute = UiRouteId.Pause;
            SetGameplaySuspended(true);
            ShowRoute(UiRouteId.Pause);
        }

        private void TogglePause(InputAction.CallbackContext context)
        {
            if (!context.performed || !worldReadyHandled)
            {
                return;
            }

            if (currentRoute == UiRouteId.InGameHud)
            {
                EnterPause();
            }
            else if (currentRoute == UiRouteId.Pause)
            {
                EnterGameplay();
            }
            else if (IsSettingsRoute(currentRoute) && settingsReturnRoute == UiRouteId.Pause)
            {
                ShowRoute(UiRouteId.Pause);
            }
        }

        private void BindPauseAction()
        {
            pauseAction = dependencies.PlayerActions?
                .FindAction("System/Pause", throwIfNotFound: false);
            if (pauseAction == null)
            {
                Debug.LogWarning("M08A System/Pause action is unavailable; pause navigation is disabled.", this);
                return;
            }

            pauseAction.performed += TogglePause;
            pauseAction.Enable();
        }

        private void UnbindPauseAction()
        {
            if (pauseAction == null)
            {
                return;
            }

            pauseAction.performed -= TogglePause;
            pauseAction.Disable();
            pauseAction = null;
        }

        private void SetGameplaySuspended(bool suspended)
        {
            if (gameplaySuspended == suspended)
            {
                SetCursorForUi(suspended);
                SetVisibilitySuppressed(suspended);
                return;
            }

            if (suspended)
            {
                savedInputGateStates.Clear();
                MonoBehaviour[] behaviours = dependencies.GameplayRoot == null
                    ? Array.Empty<MonoBehaviour>()
                    : dependencies.GameplayRoot.GetComponentsInChildren<MonoBehaviour>(
                        includeInactive: true);
                visibilityGates.Clear();
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour is IGameplayInputGate inputGate)
                    {
                        savedInputGateStates[inputGate] = inputGate.IsGameplayInputEnabled;
                        inputGate.SetGameplayInputEnabled(false);
                    }

                    if (behaviour is IUiVisibilityGate visibilityGate)
                    {
                        visibilityGates.Add(visibilityGate);
                        visibilityGate.SetUiSuppressed(true);
                    }
                }

                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                if (dependencies.GameTime != null)
                {
                    savedClockPaused = dependencies.GameTime.Snapshot.IsPaused;
                    dependencies.GameTime.SetPaused(true);
                }
            }
            else
            {
                RestoreGameplayState();
            }

            gameplaySuspended = suspended;
            SetCursorForUi(suspended);
        }

        private void RestoreGameplayState()
        {
            foreach (KeyValuePair<IGameplayInputGate, bool> item in savedInputGateStates)
            {
                try
                {
                    item.Key?.SetGameplayInputEnabled(item.Value);
                }
                catch (MissingReferenceException)
                {
                }
            }

            savedInputGateStates.Clear();
            SetVisibilitySuppressed(false);
            Time.timeScale = Mathf.Max(0f, savedTimeScale);
            if (dependencies?.GameTime != null)
            {
                dependencies.GameTime.SetPaused(savedClockPaused);
            }

            gameplaySuspended = false;
        }

        private void SetVisibilitySuppressed(bool suppressed)
        {
            for (int index = visibilityGates.Count - 1; index >= 0; index--)
            {
                try
                {
                    visibilityGates[index]?.SetUiSuppressed(suppressed);
                }
                catch (MissingReferenceException)
                {
                    visibilityGates.RemoveAt(index);
                }
            }
        }

        private static void SetCursorForUi(bool uiActive)
        {
            Cursor.lockState = uiActive ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiActive;
        }

        private void OpenSettings(UiRouteId route, UiRouteId returnRoute)
        {
            settingsReturnRoute = returnRoute;
            ShowRoute(route);
        }

        private void ReturnFromSettings()
        {
            settings.RevertPending();
            RebuildSettingsRoutes();
            ShowRoute(settingsReturnRoute);
        }

        private void ApplyPendingSettings()
        {
            UiRouteId routeToRestore = currentRoute;
            UiSettingsDocument applied = settings.ApplyPending();
            ApplyDocument(applied, persist: true);
            RebuildLocalizedRoutes();
            ShowRoute(routeToRestore);
            ShowNotice("ui.notice.saved");
        }

        private void ResetPendingSettings()
        {
            settings.ResetPendingToDefaults();
            RebuildSettingsRoutes();
            ShowNotice("ui.notice.defaults");
        }

        private void RevertPendingSettings()
        {
            settings.RevertPending();
            RebuildSettingsRoutes();
            ShowNotice("ui.notice.reverted");
        }

        private void ApplyDocument(UiSettingsDocument document, bool persist)
        {
            if (document == null)
            {
                return;
            }

            GraphicsSettingsDto graphics = document.Graphics;
            FullScreenMode fullScreenMode = graphics.DisplayMode == UiDisplayMode.Windowed
                ? FullScreenMode.Windowed
                : graphics.DisplayMode == UiDisplayMode.ExclusiveFullscreen
                    ? FullScreenMode.ExclusiveFullScreen
                    : FullScreenMode.FullScreenWindow;
            var refreshRate = new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, graphics.RefreshRateNumerator),
                denominator = (uint)Mathf.Max(1, graphics.RefreshRateDenominator),
            };
            if (persist || !Application.isEditor)
            {
                Screen.SetResolution(
                    Mathf.Max(640, graphics.ResolutionWidth),
                    Mathf.Max(360, graphics.ResolutionHeight),
                    fullScreenMode,
                    refreshRate);
            }
            QualitySettings.vSyncCount = graphics.VSync ? 1 : 0;
            if (graphics.QualityLevel >= 0 &&
                graphics.QualityLevel < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(graphics.QualityLevel, applyExpensiveChanges: true);
            }

            AudioSettingsDto audio = document.Audio;
            dependencies.Audio?.ApplySettings(new AudioSettingsState(
                audio.Master01,
                audio.Engine01,
                audio.Effects01,
                audio.Environment01,
                audio.Music01,
                audio.Ui01,
                (AudioDynamicRangeMode)(int)audio.DynamicRange,
                audio.MuteWhenUnfocused,
                audio.SubtitlesEnabled,
                audio.CaptionsEnabled,
                audio.ReducedLoudSounds,
                audio.OutputDeviceId));

            ControlsSettingsDto controls = document.Controls;
            InputSystem.settings.defaultDeadzoneMin = Mathf.Clamp(
                controls.GamepadDeadzone,
                0f,
                Mathf.Max(0f, InputSystem.settings.defaultDeadzoneMax - 0.001f));
            ApplyLookSettings(controls);

            textCatalog.LocaleId = document.Gameplay.LanguageId;
            if (scaleRoot != null)
            {
                float scale = Mathf.Clamp(document.Accessibility.UiScale, 0.85f, 1.25f);
                scaleRoot.localScale = new Vector3(scale, scale, 1f);
                RefreshGlassSurfaces();
            }

            if (persist)
            {
                SaveBindingOverrides(document.Controls);
                settingsStore.Save(document);
            }
        }

        private void ApplyLookSettings(ControlsSettingsDto controls)
        {
            if (dependencies.GameplayRoot == null || controls == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = dependencies.GameplayRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IPlayerLookSettingsSink sink)
                {
                    sink.ApplyLookSettings(
                        controls.MouseSensitivity,
                        controls.InvertMouseY,
                        controls.GamepadSensitivity,
                        controls.InvertGamepadY);
                }
            }
        }

        private void LoadBindingOverrides(ControlsSettingsDto controls)
        {
            if (controls == null)
            {
                return;
            }

            TryLoadOverrides(dependencies.PlayerActions, controls.PlayerBindingOverridesJson);
            TryLoadOverrides(dependencies.VehicleActions, controls.VehicleBindingOverridesJson);
        }

        private void SaveBindingOverrides(ControlsSettingsDto controls)
        {
            if (controls == null)
            {
                return;
            }

            controls.PlayerBindingOverridesJson =
                dependencies.PlayerActions?.SaveBindingOverridesAsJson() ?? string.Empty;
            controls.VehicleBindingOverridesJson =
                dependencies.VehicleActions?.SaveBindingOverridesAsJson() ?? string.Empty;
        }

        private static void TryLoadOverrides(InputActionAsset asset, string json)
        {
            if (asset == null || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                asset.LoadBindingOverridesFromJson(json, removeExisting: true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("M08A ignored invalid input binding overrides: " + exception.Message);
            }
        }

        private void BuildNotice()
        {
            noticeRoot = factory.Panel(
                "SettingsNotice",
                referenceFrame,
                626f,
                26f,
                420f,
                48f,
                UiThemeTokens.Card);
            noticeText = factory.Text(
                "NoticeText",
                noticeRoot.transform,
                string.Empty,
                14f,
                0f,
                392f,
                48f,
                14,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            noticeRoot.SetActive(false);
        }

        private void ShowNotice(string key)
        {
            if (noticeRoot == null || noticeText == null)
            {
                return;
            }

            noticeText.text = textCatalog.Get(key);
            noticeUntilUnscaledTime = Time.unscaledTime + 2.5f;
            noticeRoot.SetActive(true);
        }

        private void RecordFrameSample()
        {
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f || delta > 1f)
            {
                return;
            }

            if (frameSamples.Count == 240)
            {
                frameSamples.Dequeue();
            }

            frameSamples.Enqueue(delta);
        }

        private string AverageFpsText()
        {
            if (frameSamples.Count < 30)
            {
                return textCatalog.Get("ui.main.awaiting_sample");
            }

            double total = 0d;
            foreach (float sample in frameSamples)
            {
                total += sample;
            }

            return (frameSamples.Count / total).ToString("0", CultureInfo.InvariantCulture) + " FPS";
        }

        private UiLocaleFormatter GetLocaleFormatter()
        {
            if (localeFormatter == null ||
                !string.Equals(
                    localeFormatter.Culture.Name,
                    textCatalog.LocaleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                localeFormatter = new UiLocaleFormatter(textCatalog.LocaleId);
            }

            return localeFormatter;
        }

        private void TrackUiSelectionAudio()
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == lastSelectedUiObject)
            {
                return;
            }

            bool shouldPost = lastSelectedUiObject != null && selected != null;
            lastSelectedUiObject = selected;
            if (shouldPost)
            {
                PostUiEvent(AudioProjectIds.Events.UiNavigate);
            }
        }

        private void PostUiConfirm()
        {
            PostUiEvent(AudioProjectIds.Events.UiConfirm);
        }

        private void PostUiEvent(AudioEventId eventId)
        {
            IAudioBackend backend = dependencies?.Audio;
            if (backend == null || !backend.IsReady)
            {
                return;
            }

            var request = new AudioEventRequest(
                eventId,
                worldPosition: Vector3.zero,
                volume01: 1f,
                allowMultiple: false);
            backend.PostEvent(in request);
        }

        private void RebuildLocalizedRoutes()
        {
            RebuildSettingsRoutes();
            RebuildRoute(UiRouteId.Loading, BuildLoadingRoute);
            RebuildRoute(UiRouteId.MainMenu, BuildMainMenuRoute);
            RebuildRoute(UiRouteId.Pause, BuildPauseRoute);
            RebuildRoute(UiRouteId.ConfirmationDialog, BuildConfirmationDialogRoute);
            RebuildRoute(UiRouteId.SaveStatus, BuildSaveStatusRoute);
            RebuildRoute(UiRouteId.InGameHud, BuildHudRoute);
        }

        private void RebuildSettingsRoutes()
        {
            UiRouteId routeToRestore = currentRoute;
            RebuildRoute(UiRouteId.SettingsGraphics, BuildGraphicsRoute);
            RebuildRoute(UiRouteId.SettingsAudio, BuildAudioRoute);
            RebuildRoute(UiRouteId.SettingsControls, BuildControlsRoute);
            RebuildRoute(UiRouteId.SettingsGameplay, BuildGameplayRoute);
            RebuildRoute(UiRouteId.SettingsAccessibility, BuildAccessibilityRoute);
            RebuildRoute(UiRouteId.SettingsMods, BuildModsRoute);
            if (routes.TryGetValue(routeToRestore, out GameObject route))
            {
                route.SetActive(true);
                currentRoute = routeToRestore;
            }
        }

        private void RebuildRoute(UiRouteId id, Action builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var rebuildTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
            bool wasActive = routes.TryGetValue(id, out GameObject existing) && existing.activeSelf;
            if (existing != null)
            {
                routes.Remove(id);
                routeSelections.Remove(id);
                Destroy(existing);
            }

            builder();
            if (routes.TryGetValue(id, out GameObject rebuilt))
            {
                rebuilt.SetActive(wasActive);
                if (wasActive && id == currentRoute)
                {
                    UiFactory.SelectFirst(rebuilt);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            rebuildTimer.Stop();
            Debug.Log(
                $"M08A UI PERF route rebuild {id}: {rebuildTimer.Elapsed.TotalMilliseconds:F3} ms; " +
                $"thread allocations={GC.GetAllocatedBytesForCurrentThread() - allocationStart} bytes.",
                this);
#endif
        }

        private static bool IsSettingsRoute(UiRouteId route)
        {
            return route >= UiRouteId.SettingsGraphics && route <= UiRouteId.SettingsMods;
        }

        private static bool IsReferencePendingRoute(UiRouteId route)
        {
            return route == UiRouteId.Loading ||
                route == UiRouteId.Pause ||
                route == UiRouteId.ConfirmationDialog ||
                route == UiRouteId.SaveStatus ||
                route == UiRouteId.SettingsAccessibility ||
                route == UiRouteId.SettingsMods;
        }

        private void OpenQuitConfirmation(UiRouteId returnRoute)
        {
            confirmationReturnRoute = returnRoute;
            ShowRoute(UiRouteId.ConfirmationDialog);
        }

        private void CancelQuitConfirmation()
        {
            ShowRoute(confirmationReturnRoute);
        }

        private void OpenSaveStatus(UiRouteId returnRoute)
        {
            saveStatusReturnRoute = returnRoute;
            ShowRoute(UiRouteId.SaveStatus);
        }

        private void ReturnFromSaveStatus()
        {
            ShowRoute(saveStatusReturnRoute);
        }

        private void ReturnToFreshMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0, LoadSceneMode.Single);
        }

        private void QuitApplication()
        {
#if UNITY_EDITOR
            Debug.Log("M08A Quit requested. Application.Quit is ignored by the Editor.", this);
#else
            Application.Quit(0);
#endif
        }

        private void BuildLoadingRoute()
        {
            GameObject route = CreateRoute(UiRouteId.Loading);
            GameObject panel = factory.Panel("LoadingCard", route.transform, 586f, 397f, 500f, 146f);
            factory.Heading(panel.transform, textCatalog.Get("ui.loading.title"), 28f, 24f, 444f, 24);
            factory.Text(
                "LoadingDetail",
                panel.transform,
                textCatalog.Get("ui.loading.detail"),
                28f,
                76f,
                444f,
                42f,
                14,
                UiThemeTokens.TextMuted,
                TextAnchor.UpperLeft);
            factory.Text(
                "LoadingWait",
                panel.transform,
                textCatalog.Get("ui.loading.wait"),
                28f,
                112f,
                444f,
                18f,
                10,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
        }

        private void BuildConfirmationDialogRoute()
        {
            GameObject route = CreateRoute(UiRouteId.ConfirmationDialog);
            GameObject panel = factory.Panel(
                "ConfirmationCard",
                route.transform,
                576f,
                284f,
                520f,
                372f);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.confirmation.title"),
                34f,
                28f,
                452f,
                27);
            factory.Text(
                "ConfirmationBody",
                panel.transform,
                textCatalog.Get("ui.confirmation.quit_body"),
                34f,
                96f,
                452f,
                74f,
                17,
                UiThemeTokens.TextPrimary,
                TextAnchor.UpperLeft);
            factory.Text(
                "ConfirmationReferenceState",
                panel.transform,
                textCatalog.Get("ui.common.reference_pending"),
                34f,
                184f,
                452f,
                22f,
                10,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            factory.CompactButton(
                "ConfirmationCancel",
                panel.transform,
                textCatalog.Get("ui.confirmation.cancel"),
                34f,
                266f,
                214f,
                58f,
                CancelQuitConfirmation,
                primary: true);
            factory.CompactButton(
                "ConfirmationAccept",
                panel.transform,
                textCatalog.Get("ui.confirmation.quit"),
                272f,
                266f,
                214f,
                58f,
                QuitApplication,
                destructive: true);
        }

        private void BuildSaveStatusRoute()
        {
            GameObject route = CreateRoute(UiRouteId.SaveStatus);
            GameObject panel = factory.Panel(
                "SaveStatusCard",
                route.transform,
                536f,
                252f,
                600f,
                436f);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.save_status.title"),
                34f,
                28f,
                532f,
                27);
            factory.Text(
                "SaveStatusState",
                panel.transform,
                textCatalog.Get("ui.main.no_save"),
                34f,
                104f,
                532f,
                48f,
                18,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            factory.Text(
                "SaveStatusDetail",
                panel.transform,
                textCatalog.Get("ui.save_status.detail"),
                34f,
                168f,
                532f,
                82f,
                14,
                UiThemeTokens.TextMuted,
                TextAnchor.UpperLeft);
            factory.Text(
                "SaveStatusReferenceState",
                panel.transform,
                textCatalog.Get("ui.common.reference_pending"),
                34f,
                270f,
                532f,
                22f,
                10,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            factory.CompactButton(
                "SaveStatusBack",
                panel.transform,
                textCatalog.Get("ui.common.back"),
                174f,
                334f,
                252f,
                54f,
                ReturnFromSaveStatus,
                primary: true);
        }

        partial void BuildMainMenuRoute();
        partial void BuildGraphicsRoute();
        partial void BuildAudioRoute();
        partial void BuildControlsRoute();
        partial void BuildGameplayRoute();
        partial void BuildAccessibilityRoute();
        partial void BuildModsRoute();
        partial void BuildPauseRoute();
        partial void BuildHudRoute();
        partial void RefreshHud(bool force);

        private readonly struct NeedHudBinding
        {
            public NeedHudBinding(Text valueText, Image fill, float reviewValue)
            {
                ValueText = valueText;
                Fill = fill;
                ReviewValue = reviewValue;
            }

            public Text ValueText { get; }

            public Image Fill { get; }

            public float ReviewValue { get; }
        }

    }
}
