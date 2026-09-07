using System.Collections;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
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
        public IEnumerator MainMenuKeyboardActions_MoveSubmitAndCancelThroughInputModule()
        {
            InputSettings.UpdateMode originalMode = InputSystem.settings.updateMode;
            InputSettings.BackgroundBehavior originalBackground = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode originalEditorRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard originalKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse originalMouse = Mouse.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            UiFixture fixture = default;
            InputActionAsset uiActions = null;
            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? originalUiDevices = null;
            try
            {
                // A paused menu has no FixedUpdate. Exercise the real dynamic
                // input/player loop, without directly calling UI event handlers.
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                // Match the installed package's InputTestFixture: a batch
                // Editor has no focused Game View and otherwise diverts only
                // keyboard/pointer events away from the player action loop.
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                fixture = CreateFixture(startInMainMenu: true);
                InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                Assert.That(module, Is.Not.Null);
                uiActions = module.actionsAsset;
                originalUiDevices = uiActions.devices;
                uiActions.devices = new InputDevice[] { keyboard, mouse };
                fixture.PlayerActions.devices = new InputDevice[] { keyboard };
                // Filter before the first player-loop update: a live device
                // must not seed the UI module's cached press/navigation state.
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("NewGame"));

                PrepareMenuPointerChecks(fixture);
                MainMenuActionButton credits = FindRequired(fixture.Root.transform, "Credits")
                    .GetComponent<MainMenuActionButton>();
                MainMenuActionButton newGame = FindRequired(fixture.Root.transform, "NewGame")
                    .GetComponent<MainMenuActionButton>();
                yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                yield return SendMenuMouseState(mouse, new MouseState { position = MenuPointerPosition(credits.transform) });
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(credits.gameObject));
                AssertMenuBorder(credits, MainMenuStyle.HoverBorder);
                yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(credits.gameObject),
                    "Leaving hover must retain the logical navigation anchor without asking root to restore it.");
                AssertMenuBorder(credits, MainMenuStyle.Border);
                Assert.That(FindRequired(credits.transform, "SelectionGlow").GetComponent<Image>().color.a, Is.Zero);
                yield return SendMenuKeyboardState(keyboard, Key.UpArrow);
                AssertMenuBorder(newGame, MainMenuStyle.Accent);
                // Real movement over the already-selected item also switches
                // its visual focus to pointer hover. Keep the cursor here
                // while opening/closing routes to test stationary re-entry.
                yield return SendMenuMouseState(mouse, new MouseState { position = MenuPointerPosition(newGame.transform) });
                AssertMenuBorder(newGame, MainMenuStyle.HoverBorder);

                yield return SendMenuKeyboardState(keyboard, Key.S);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Credits"),
                    "WASD must skip unavailable save actions and stay in the main column.");
                AssertMenuBorder(credits, MainMenuStyle.Accent);
                yield return SendMenuKeyboardState(keyboard, Key.Space);
                Assert.That(FindRequired(fixture.Root.transform, "CreditsOverlay").gameObject.activeInHierarchy, Is.True,
                    "Space must submit through InputSystemUIInputModule.");
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("CreditsBack"));

                yield return SendMenuKeyboardState(keyboard, Key.Escape);
                Assert.That(FindOptional(fixture.Root.transform, "CreditsOverlay"), Is.Null);
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Credits"));
                AssertMenuBorder(credits, MainMenuStyle.Accent);
                yield return SendMenuKeyboardState(keyboard, Key.UpArrow);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("NewGame"));
                yield return SendMenuKeyboardState(keyboard, Key.RightArrow);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Settings"));
                yield return SendMenuKeyboardState(keyboard, Key.Enter);
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGraphics));
                yield return SendMenuKeyboardState(keyboard, Key.Escape);
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Settings"));
                // Persistent paint/page choice is independent from pointer
                // focus: click real controls, exit, then hover another colour.
                MainMenuActionButton page = FindRequired(fixture.Root.transform, "ColourPage1")
                    .GetComponent<MainMenuActionButton>();
                yield return ClickMenuMouse(mouse, page.transform);
                MainMenuActionButton chosenPaint = FindRequired(fixture.Root.transform, "Colour11")
                    .GetComponent<MainMenuActionButton>();
                yield return ClickMenuMouse(mouse, chosenPaint.transform);
                yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                Assert.That(page.PersistentSelection, Is.True);
                Assert.That(FindRequired(page.transform, "Dot").GetComponent<Image>().color, Is.EqualTo(MainMenuStyle.Accent));
                Assert.That(chosenPaint.PersistentSelection, Is.True);
                AssertMenuBorder(chosenPaint, MainMenuStyle.Accent);
                MainMenuActionButton otherPaint = FindRequired(fixture.Root.transform, "Colour10")
                    .GetComponent<MainMenuActionButton>();
                yield return SendMenuMouseState(mouse, new MouseState { position = MenuPointerPosition(otherPaint.transform) });
                yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                Assert.That(otherPaint.PersistentSelection, Is.False);
                Assert.That(FindRequired(otherPaint.transform, "Border").GetComponent<Image>().color.a, Is.Zero);
                AssertMenuBorder(chosenPaint, MainMenuStyle.Accent);
                yield return DestroyFixture(fixture);
            }
            finally
            {
                if (uiActions != null) uiActions.devices = originalUiDevices;
                DestroyFailedMenuInputFixture(fixture);
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
                InputSystem.settings.updateMode = originalMode;
                InputSystem.settings.backgroundBehavior = originalBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorRouting;
            }
        }

        [UnityTest]
        public IEnumerator MainMenuGamepadActions_UseDpadStickSubmitAndBackWithoutLosingFocus()
        {
            InputSettings.UpdateMode originalMode = InputSystem.settings.updateMode;
            InputSettings.BackgroundBehavior originalBackground = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode originalEditorRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            Gamepad originalGamepad = Gamepad.current;
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Mouse originalMouse = Mouse.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            UiFixture fixture = default;
            InputActionAsset uiActions = null;
            UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? originalUiDevices = null;
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                fixture = CreateFixture(startInMainMenu: true);
                InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                Assert.That(module, Is.Not.Null);
                uiActions = module.actionsAsset;
                originalUiDevices = uiActions.devices;
                uiActions.devices = new InputDevice[] { gamepad, mouse };
                fixture.PlayerActions.devices = new InputDevice[] { gamepad };
                // Apply the same isolation before the first update as the
                // keyboard case, rather than accepting one frame of live input.
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("NewGame"));

                PrepareMenuPointerChecks(fixture);
                MainMenuActionButton newGame = FindRequired(fixture.Root.transform, "NewGame")
                    .GetComponent<MainMenuActionButton>();
                yield return SendMenuMouseState(mouse, new MouseState { position = MenuPointerPosition(newGame.transform) });
                yield return SendMenuMouseState(mouse, new MouseState { position = Vector2.one });
                AssertMenuBorder(newGame, MainMenuStyle.Border);
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu),
                    "Pointer movement without a press must not activate New Game.");
                Assert.That(EventSystem.current, Is.EqualTo(module.GetComponent<EventSystem>()));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(newGame.gameObject),
                    "Leaving pointer hover must retain New Game as the gamepad navigation anchor.");

                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu),
                    "Dpad movement must navigate without submitting or changing routes.");
                Assert.That(EventSystem.current, Is.EqualTo(module.GetComponent<EventSystem>()));
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(FindRequired(fixture.Root.transform, "Credits").gameObject));
                AssertMenuBorder(FindRequired(fixture.Root.transform, "Credits").GetComponent<MainMenuActionButton>(),
                    MainMenuStyle.Accent);
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.South));
                Assert.That(FindRequired(fixture.Root.transform, "CreditsOverlay").gameObject.activeInHierarchy, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("CreditsBack"));
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("CreditsBack"),
                    "Modal navigation must not select the covered main menu.");
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.East));
                Assert.That(FindOptional(fixture.Root.transform, "CreditsOverlay"), Is.Null);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Credits"));

                yield return SendMenuGamepadState(gamepad, new GamepadState { leftStick = Vector2.up });
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("NewGame"));
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Settings"));
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.South));
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGraphics));
                yield return SendMenuGamepadState(gamepad, new GamepadState().WithButton(GamepadButton.East));
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Settings"));
                Assert.That(EventSystem.current.currentSelectedGameObject.activeInHierarchy, Is.True);
                yield return DestroyFixture(fixture);
            }
            finally
            {
                if (uiActions != null) uiActions.devices = originalUiDevices;
                DestroyFailedMenuInputFixture(fixture);
                if (gamepad.added) InputSystem.RemoveDevice(gamepad);
                if (originalGamepad != null && originalGamepad.added) originalGamepad.MakeCurrent();
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
                InputSystem.settings.updateMode = originalMode;
                InputSystem.settings.backgroundBehavior = originalBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorRouting;
            }
        }

        private static IEnumerator SendMenuKeyboardState(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static void PrepareMenuPointerChecks(UiFixture fixture)
        {
            foreach (MainMenuActionButton button in fixture.Root.GetComponentsInChildren<MainMenuActionButton>(true))
                button.ReducedMotion = true;
            Canvas.ForceUpdateCanvases();
        }

        private static Vector2 MenuPointerPosition(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)target;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static void AssertMenuBorder(MainMenuActionButton button, Color expected) =>
            Assert.That(FindRequired(button.transform, "Border").GetComponent<Image>().color, Is.EqualTo(expected));

        private static IEnumerator SendMenuMouseState(Mouse mouse, MouseState state)
        {
            InputSystem.QueueStateEvent(mouse, state);
            yield return null;
            yield return null;
        }

        private static IEnumerator ClickMenuMouse(Mouse mouse, Transform target)
        {
            Vector2 position = MenuPointerPosition(target);
            yield return SendMenuMouseState(mouse, new MouseState { position = position });
            yield return SendMenuMouseState(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return SendMenuMouseState(mouse, new MouseState { position = position });
        }

        private static IEnumerator SendMenuGamepadState(Gamepad gamepad, GamepadState state)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;
        }

        private static void DestroyFailedMenuInputFixture(UiFixture fixture)
        {
            // Normal completion uses the shared asynchronous teardown; this
            // path also restores isolation when an assertion ends a test early.
            if (fixture.Root != null) Object.DestroyImmediate(fixture.Root.gameObject);
            if (fixture.GameplayRoot != null) Object.DestroyImmediate(fixture.GameplayRoot);
            if (fixture.PlayerActions != null) Object.DestroyImmediate(fixture.PlayerActions);
            if (fixture.VehicleActions != null) Object.DestroyImmediate(fixture.VehicleActions);
        }
    }
}
