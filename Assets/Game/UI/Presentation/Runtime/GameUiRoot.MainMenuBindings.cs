using System;
using MSC.Audio;
using MSC.UI.Runtime.Routing;
using MSC.UI.Runtime.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private Button[] mainPrimaryNavigation = Array.Empty<Button>();
        private Button[] mainUtilityNavigation = Array.Empty<Button>();
        private Button[] mainSwatchNavigation = Array.Empty<Button>();
        private Button[] mainPageNavigation = Array.Empty<Button>();
        private int rememberedMainGroup;
        private int rememberedMainIndex = 1;
        private InputSystemUIInputModule uiInputModule;
        private InputAction menuCancelAction;
        private bool menuCancelRequested;
        private int lastPauseHandledFrame = -1;
        private GameObject mainMenuModal;
        private Button mainMenuModalBack;

        private void InitializeMainMenuColour()
        {
            selectedCarColourIndex = Mathf.Clamp(
                settings.Applied.MainMenuCarColourIndex, 0, CarColours.Length - 1);
        }

        private void PersistMainMenuColourSelection(int index)
        {
            UiSettingsDocument applied = settings.Applied;
            int bounded = Mathf.Clamp(index, 0, CarColours.Length - 1);
            if (applied.MainMenuCarColourIndex == bounded)
            {
                return;
            }

            UiSettingsDocument pending = settings.Pending;
            applied.MainMenuCarColourIndex = bounded;
            pending.MainMenuCarColourIndex = bounded;
            try
            {
                // Commit only the applied snapshot, retaining unrelated edits
                // in the existing settings transaction for Apply/Cancel.
                settingsStore.Save(applied);
                settings.ReplaceApplied(applied);
                settings.ReplacePending(pending);
            }
            catch (Exception exception)
            {
                Debug.LogError("Menu colour preference could not be saved: " + exception.Message, this);
                ShowNotice("ui.notice.save_failed");
            }
        }

        private void ConfigureMainMenuNavigation(
            Button[] primary, Button[] utility, Button[] swatches, Button[] pages)
        {
            mainPrimaryNavigation = primary ?? Array.Empty<Button>();
            mainUtilityNavigation = utility ?? Array.Empty<Button>();
            mainSwatchNavigation = swatches ?? Array.Empty<Button>();
            mainPageNavigation = pages ?? Array.Empty<Button>();
            RefreshMainMenuNavigation();
            Button remembered = RememberedMainButton();
            if (remembered != null)
            {
                routeSelections[UiRouteId.MainMenu] = remembered.gameObject;
            }
        }

        private void RefreshMainMenuNavigation()
        {
            Button primaryDefault = MainDefaultButton();
            Button swatchEntry = AvailableAt(mainSwatchNavigation, selectedCarColourIndex)
                ?? FirstAvailable(mainSwatchNavigation);
            Button utilityEntry = FirstAvailable(mainUtilityNavigation);
            Button pageEntry = FirstAvailable(mainPageNavigation);
            for (int index = 0; index < mainPrimaryNavigation.Length; index++)
            {
                SetMenuNavigation(mainPrimaryNavigation[index],
                    RelativeAvailable(mainPrimaryNavigation, index, -1),
                    RelativeAvailable(mainPrimaryNavigation, index, 1),
                    swatchEntry, utilityEntry);
            }

            for (int index = 0; index < mainUtilityNavigation.Length; index++)
            {
                SetMenuNavigation(mainUtilityNavigation[index], primaryDefault, primaryDefault,
                    index == 0 ? swatchEntry : RelativeAvailable(mainUtilityNavigation, index, -1),
                    RelativeAvailable(mainUtilityNavigation, index, 1));
            }

            for (int index = 0; index < mainSwatchNavigation.Length; index++)
            {
                if (!IsMenuAvailable(mainSwatchNavigation[index]))
                {
                    continue;
                }

                int local = index % 10;
                Button up = local >= 5 ? AvailableAt(mainSwatchNavigation, index - 5) : null;
                Button down = local < 5 ? AvailableAt(mainSwatchNavigation, index + 5) : null;
                Button left = local % 5 > 0 ? AvailableAt(mainSwatchNavigation, index - 1) : null;
                Button right = local % 5 < 4 ? AvailableAt(mainSwatchNavigation, index + 1) : null;
                SetMenuNavigation(mainSwatchNavigation[index], up ?? primaryDefault,
                    down ?? pageEntry ?? utilityEntry,
                    left ?? mainSwatchNavigation[index], right ?? primaryDefault);
            }

            for (int index = 0; index < mainPageNavigation.Length; index++)
            {
                SetMenuNavigation(mainPageNavigation[index], swatchEntry, utilityEntry,
                    RelativeAvailable(mainPageNavigation, index, -1),
                    RelativeAvailable(mainPageNavigation, index, 1));
            }

            if (currentRoute == UiRouteId.MainMenu)
            {
                EnsureVisibleUiSelection();
            }
        }

        private static void SetMenuNavigation(
            Button button, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (button == null)
            {
                return;
            }

            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right,
            };
        }

        private static bool IsMenuAvailable(Button button) =>
            button != null && button.enabled && button.gameObject.activeSelf && button.IsInteractable();

        private static Button AvailableAt(Button[] buttons, int index) =>
            index >= 0 && index < buttons.Length && IsMenuAvailable(buttons[index]) ? buttons[index] : null;

        private static Button FirstAvailable(Button[] buttons)
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                if (IsMenuAvailable(buttons[index]))
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static Button RelativeAvailable(Button[] buttons, int index, int direction)
        {
            for (int offset = 1; offset <= buttons.Length; offset++)
            {
                int candidate = (index + direction * offset + buttons.Length) % buttons.Length;
                if (IsMenuAvailable(buttons[candidate]))
                {
                    return buttons[candidate];
                }
            }

            return null;
        }

        private Button MainDefaultButton() =>
            AvailableAt(mainPrimaryNavigation, 1) ?? FirstAvailable(mainPrimaryNavigation);

        private Button RememberedMainButton()
        {
            Button[] group = rememberedMainGroup == 1 ? mainUtilityNavigation
                : rememberedMainGroup == 2 ? mainSwatchNavigation
                : rememberedMainGroup == 3 ? mainPageNavigation : mainPrimaryNavigation;
            return AvailableAt(group, rememberedMainIndex) ?? MainDefaultButton();
        }

        private void RememberMainMenuSelection(GameObject selected)
        {
            if (selected == null)
            {
                return;
            }

            if (RememberMainGroup(mainPrimaryNavigation, 0, selected) ||
                RememberMainGroup(mainUtilityNavigation, 1, selected) ||
                RememberMainGroup(mainSwatchNavigation, 2, selected))
            {
                return;
            }

            RememberMainGroup(mainPageNavigation, 3, selected);
        }

        private bool RememberMainGroup(Button[] buttons, int group, GameObject selected)
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index] != null && buttons[index].gameObject == selected)
                {
                    rememberedMainGroup = group;
                    rememberedMainIndex = index;
                    return true;
                }
            }

            return false;
        }

        private bool SelectMainMenuDefault()
        {
            Button target = RememberedMainButton();
            if (target == null || !target.gameObject.activeInHierarchy || EventSystem.current == null)
            {
                return false;
            }

            EventSystem.current.SetSelectedGameObject(target.gameObject);
            return true;
        }

        private void ConfigureMenuInputModule()
        {
            uiInputModule = EventSystem.current == null
                ? null : EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (uiInputModule == null)
            {
                return;
            }

            uiInputModule.deselectOnBackgroundClick = false;
            InputAction submit = uiInputModule.submit?.action;
            if (submit == null)
            {
                return;
            }

            // Input System's default */{Submit} covers Enter and controller
            // south, but Keyboard.space has no Submit usage in this version.
            for (int index = 0; index < submit.bindings.Count; index++)
            {
                if (string.Equals(submit.bindings[index].effectivePath, "<Keyboard>/space", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            bool wasEnabled = submit.enabled;
            submit.Disable();
            submit.AddBinding("<Keyboard>/space");
            if (wasEnabled)
            {
                submit.Enable();
            }
        }

        private void BindMenuCancelAction()
        {
            menuCancelAction = uiInputModule?.cancel?.action;
            if (menuCancelAction != null)
            {
                menuCancelAction.performed += OnMenuCancelRequested;
            }
        }

        private void UnbindMenuCancelAction()
        {
            if (menuCancelAction != null)
            {
                menuCancelAction.performed -= OnMenuCancelRequested;
                menuCancelAction = null;
            }
        }

        private void OnMenuCancelRequested(InputAction.CallbackContext context)
        {
            if (activeRebindOperation == null)
            {
                menuCancelRequested = true;
            }
        }

        private void UpdateMenuNavigation()
        {
            if (menuCancelRequested)
            {
                menuCancelRequested = false;
                if (lastPauseHandledFrame != Time.frameCount && activeRebindOperation == null)
                {
                    HandleMenuBack();
                }
            }

            EnsureVisibleUiSelection();
        }

        private void EnsureVisibleUiSelection()
        {
            EventSystem events = EventSystem.current;
            if (events == null || !routes.TryGetValue(currentRoute, out GameObject route) || route == null)
            {
                return;
            }

            GameObject selected = events.currentSelectedGameObject;
            if (mainMenuModal != null && mainMenuModal.activeInHierarchy &&
                currentRoute == UiRouteId.MainMenu)
            {
                if (selected == null || !selected.transform.IsChildOf(mainMenuModal.transform))
                {
                    events.SetSelectedGameObject(mainMenuModalBack.gameObject);
                }
                return;
            }

            if (selected != null && selected.activeInHierarchy &&
                selected.TryGetComponent(out Selectable selectable) && selectable.IsInteractable())
            {
                return;
            }

            events.SetSelectedGameObject(null);
            if (currentRoute == UiRouteId.InGameHud || currentRoute == UiRouteId.Loading)
            {
                return;
            }

            if (!TryRestoreRouteSelection(currentRoute) &&
                !(currentRoute == UiRouteId.MainMenu && SelectMainMenuDefault()))
            {
                UiFactory.SelectFirst(route);
            }
        }

        private void HandleMenuBack()
        {
            if (currentRoute == UiRouteId.Loading || currentRoute == UiRouteId.InGameHud ||
                (currentRoute == UiRouteId.MainMenu && mainMenuModal == null))
            {
                return;
            }

            PostUiEvent(AudioProjectIds.Events.UiCancel);
            if (mainMenuModal != null && currentRoute == UiRouteId.MainMenu)
            {
                CloseCredits(mainMenuModal, RememberedMainButton()?.gameObject);
            }
            else if (IsSettingsRoute(currentRoute))
            {
                ReturnFromSettingsWithBindings();
            }
            else if (currentRoute == UiRouteId.SaveStatus)
            {
                ReturnFromSaveStatus();
            }
            else if (currentRoute == UiRouteId.ConfirmationDialog)
            {
                CancelQuitConfirmation();
            }
            else if (currentRoute == UiRouteId.Pause)
            {
                EnterGameplay();
            }
        }
    }
}
