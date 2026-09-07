#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator SettingsAndPause_StyledGlassCapturesAndPausedInputPreserveContext()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Artifacts/MainMenuRedesign/Captures/SettingsPause"));
            Directory.CreateDirectory(directory);
            InputSettings.UpdateMode oldMode = InputSystem.settings.updateMode;
            InputSettings.BackgroundBehavior oldBackground = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse previousMouse = Mouse.current;
            Keyboard previousKeyboard = Keyboard.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputActionAsset actions = null;
            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? oldDevices = null;
            UiFixture fixture = default;
            GameObject sourceObject = null;
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                using (var resolution = new MainMenuGameViewResolutionScope())
                {
                    fixture = CreateMenuVehicleFixture(LoadMenuVehiclePrefab());
                    actions = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;
                    oldDevices = actions.devices;
                    actions.devices = new InputDevice[] { mouse, keyboard };
                    fixture.PlayerActions.devices = new InputDevice[] { keyboard };
                    MainMenuVehiclePreview preview = RequireMenuVehiclePreview(fixture);
                    yield return AwaitMenuVehicleFrame(preview, 1);
                    yield return ResizeMenuCaptureViewport(resolution, preview, new Vector2Int(1920, 1080));
                    FindRequired(fixture.Root.transform, "Settings").GetComponent<Button>().onClick.Invoke();
                    yield return CaptureStyledUiRoute(fixture.Root, UiRouteId.SettingsGraphics, directory,
                        "Settings-Graphics-1920x1080.png");
                    Transform graphics = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
                    FindRequired(graphics, "NavigateSettingsGameplay").GetComponent<Button>().onClick.Invoke();
                    yield return CaptureStyledUiRoute(fixture.Root, UiRouteId.SettingsGameplay, directory,
                        "Settings-General-1920x1080.png");
                    Assert.That(preview.Model.gameObject.activeInHierarchy, Is.False,
                        "Settings must retain the cached menu output without running the menu stage.");
                    if (actions != null) actions.devices = oldDevices;
                    actions = null;
                    yield return DestroyFixture(fixture);
                    fixture = default;
                    yield return null;

                    // A second, separately owned mesh-only stage supplies an
                    // actual camera for the production PauseFrozen capture path.
                    // It is created after the menu fixture is destroyed so their
                    // private light/volume layers never overlap while active.
                    sourceObject = new GameObject("Paused UI capture fixture source") { hideFlags = HideFlags.DontSave };
                    ownedFixtureObjects.Add(sourceObject);
                    MainMenuVehiclePreview source = sourceObject.AddComponent<MainMenuVehiclePreview>();
                    source.Initialize(LoadMenuVehiclePrefab(), LoadMenuEnvironmentPrefab(), FixedMenuFixtureLocalTime);
                    source.SetPaint(new Color32(3, 38, 69, 255));
                    yield return AwaitMenuVehicleFrame(source, 1);
                    Shader blur = AssetDatabase.LoadAssetAtPath<Shader>(
                        "Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader");
                    Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png");
                    fixture = CreateFixture(startInMainMenu: false, uiBlurShader: blur,
                        menuLogoTexture: logo, backdropCamera: source.PreviewCamera);
                    actions = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;
                    oldDevices = actions.devices;
                    actions.devices = new InputDevice[] { mouse, keyboard };
                    fixture.PlayerActions.devices = new InputDevice[] { keyboard };
                    yield return null;
                    InvokePrivate(fixture.Root, "EnterPause");
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    RawImage frozenImage = FindRequired(fixture.Root.transform, "FrozenPauseBackdrop").GetComponent<RawImage>();
                    float deadline = Time.realtimeSinceStartup + 10f;
                    while ((!frozenImage.enabled || frozenImage.texture == null) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(frozenImage.enabled, Is.True);
                    var frozen = frozenImage.texture as RenderTexture;
                    Assert.That(frozen, Is.Not.Null, "Pause must use a real captured/blurred camera texture.");
                    Color[] frozenPixels = ReadMenuPreviewLinearPixels(frozen);
                    AssertMenuPreviewCropHasColour(frozenPixels, frozen.width, frozen.height,
                        new Rect(0f, 0f, 1f, 1f), "Actual frozen pause backdrop");
                    yield return CaptureStyledUiRoute(fixture.Root, UiRouteId.Pause, directory,
                        "Pause-ActualFrozenFixture-1920x1080.png");
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(fixture.Gate.GameplayInputEnabled, Is.False);

                    int changedSourceFrame = source.RenderCount + 1;
                    source.SetPaint(new Color32(202, 9, 0, 255));
                    yield return AwaitMenuVehicleFrame(source, changedSourceFrame);
                    Assert.That(frozenImage.texture, Is.SameAs(frozen));
                    Assert.That(ReadMenuPreviewLinearPixels(frozen), Is.EqualTo(frozenPixels),
                        "A later render of the source must not change an already frozen pause backdrop.");

                    Transform pause = FindRequired(fixture.Root.transform, UiRouteId.Pause.ToString());
                    MainMenuActionButton resume = FindRequired(pause, "Resume").GetComponent<MainMenuActionButton>();
                    Assert.That(resume, Is.Not.Null);
                    resume.ReducedMotion = false;
                    yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                    Vector2 resumePoint = MenuPointerPosition(resume.transform);
                    yield return SendMenuMouseState(mouse,
                        new MouseState { position = resumePoint, delta = resumePoint - Vector2.one });
                    yield return new WaitForSecondsRealtime(MainMenuStyle.HoverDurationSeconds + 0.05f);
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(FindRequired(resume.transform, "Visual").localScale.x, Is.GreaterThan(1.005f),
                        "The new pause control must animate using unscaled time.");

                    Transform settingsButton = FindRequired(pause, "PauseSettings");
                    yield return ClickMenuMouse(mouse, settingsButton);
                    yield return CaptureStyledUiRoute(fixture.Root, UiRouteId.SettingsGraphics, directory,
                        "Settings-FromPause-ActualFrozenFixture-1920x1080.png");
                    Assert.That(Time.timeScale, Is.Zero);
                    Assert.That(fixture.Root.ActiveBackdropModeName, Is.EqualTo("PauseFrozen"));
                    Assert.That(frozenImage.texture, Is.SameAs(frozen));
                    Transform pausedSettings = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
                    FindRequired(pausedSettings, "Back").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                    Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Pause));
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(settingsButton.gameObject));
                    yield return SendMenuKeyboardState(keyboard, Key.UpArrow);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(resume.gameObject));
                    yield return SendMenuKeyboardState(keyboard, Key.Enter);
                    Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
                    Assert.That(fixture.Gate.GameplayInputEnabled, Is.True);
                    Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
                    if (actions != null) actions.devices = oldDevices;
                    actions = null;
                    yield return DestroyFixture(fixture);
                    fixture = default;
                    UnityEngine.Object.Destroy(sourceObject);
                    sourceObject = null;
                    yield return null;
                }
                File.WriteAllText(Path.Combine(directory, "SETTINGS_PAUSE_CONTEXT.txt"),
                    "Executed native isolated UI fixtures; not a capture of a player's gameplay session.\n" +
                    "Graphics/General settings retain the real mesh-only menu render. Pause and its settings\n" +
                    "use the production StandardRequest + one-shot Gaussian PauseFrozen path with a separate\n" +
                    "owned mesh-only home/car camera. The two stages are never active together.\n" +
                    "Frozen pixels remain unchanged after a later source paint render; actual mouse hover and\n" +
                    "keyboard submit work at timeScale=0, Back preserves pause context and Resume restores input/time.\n" +
                    "No user saves/settings or production scenes touched. Captured UTC: " + DateTime.UtcNow.ToString("O") + "\n");
            }
            finally
            {
                if (actions != null) actions.devices = oldDevices;
                DestroyFailedMenuInputFixture(fixture);
                if (sourceObject != null) UnityEngine.Object.DestroyImmediate(sourceObject);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                InputSystem.settings.updateMode = oldMode;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldRouting;
            }
        }

        private static IEnumerator CaptureStyledUiRoute(GameUiRoot root, UiRouteId route, string directory, string name)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(root.CurrentRoute, Is.EqualTo(route));
            Canvas.ForceUpdateCanvases();
            AssertActiveGraphicsInsideViewport(root, route);
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(screenshot.width, Is.EqualTo(1920));
                Assert.That(screenshot.height, Is.EqualTo(1080));
                File.WriteAllBytes(Path.Combine(directory, name), screenshot.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }
    }
}
#endif
