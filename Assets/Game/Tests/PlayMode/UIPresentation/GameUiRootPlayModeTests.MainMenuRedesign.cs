using System.Collections;
using System.IO;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        [UnityTest]
        public IEnumerator StationaryPointerReveal_PreservesRestoredFocusUntilPointerMoves()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;
            Transform menu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Button credits = FindRequired(menu, "Credits").GetComponent<Button>();
            MainMenuActionButton colour = FindRequired(menu, "Colour9").GetComponent<MainMenuActionButton>();
            EventSystem.current.SetSelectedGameObject(credits.gameObject);
            var pointer = new PointerEventData(EventSystem.current) { delta = Vector2.zero };
            colour.OnPointerEnter(pointer);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(credits.gameObject),
                "Revealing a route beneath a stationary cursor must preserve restored navigation focus.");
            pointer.delta = Vector2.right;
            colour.OnPointerExit(pointer);
            colour.OnPointerEnter(pointer);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(colour.gameObject),
                "Moving the pointer explicitly hands navigation focus back to the hovered control.");
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator MainMenuVerticalNavigation_SkipsUnavailableSavesAndStaysOutOfPaint()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;
            Transform menu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Button newGame = FindRequired(menu, "NewGame").GetComponent<Button>();
            Button credits = FindRequired(menu, "Credits").GetComponent<Button>();
            Button quit = FindRequired(menu, "Quit").GetComponent<Button>();
            Assert.That(newGame.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(newGame.FindSelectableOnDown(), Is.EqualTo(credits));
            Assert.That(credits.FindSelectableOnUp(), Is.EqualTo(newGame));
            Assert.That(credits.FindSelectableOnDown(), Is.EqualTo(quit));

            EventSystem.current.SetSelectedGameObject(newGame.gameObject);
            var movement = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down };
            ExecuteEvents.Execute(newGame.gameObject, movement, ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(credits.gameObject));
            ExecuteEvents.Execute(credits.gameObject, movement, ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quit.gameObject));
            Assert.That(
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(FindRequired(menu, "CarColourCard")),
                Is.False);
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator PagedPaintChoice_IsPersistedAndRestoredBeforeNewGame()
        {
            string settingsPath = Path.Combine(temporaryDirectory, "persistent-menu-paint.json");
            UiFixture fixture = CreateFixture(startInMainMenu: true, settingsPath: settingsPath);
            yield return null;
            Transform menu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            FindRequired(menu, "ColourPage1").GetComponent<Button>().onClick.Invoke();
            Button colour11 = FindRequired(menu, "Colour11").GetComponent<Button>();
            Assert.That(colour11.gameObject.activeInHierarchy, Is.True);
            colour11.onClick.Invoke();
            Assert.That(colour11.GetComponent<MainMenuActionButton>().PersistentSelection, Is.True);
            yield return DestroyFixture(fixture);

            int appliedPaletteIndex = -1;
            UiFixture reopened = CreateFixture(
                startInMainMenu: true,
                settingsPath: settingsPath,
                configureNewGameVehiclePaint: (int index, Color bodyColor, out string failure) =>
                {
                    appliedPaletteIndex = index;
                    failure = string.Empty;
                    return true;
                });
            yield return null;
            Transform restoredMenu = FindRequired(reopened.Root.transform, UiRouteId.MainMenu.ToString());
            MainMenuActionButton restored = FindRequired(restoredMenu, "Colour11").GetComponent<MainMenuActionButton>();
            Assert.That(restored.gameObject.activeInHierarchy, Is.True, "The selected paint page must reopen automatically.");
            Assert.That(restored.PersistentSelection, Is.True);
            Assert.That(FindRequired(restoredMenu, "Colour0").gameObject.activeInHierarchy, Is.False);
            FindRequired(restoredMenu, "NewGame").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(appliedPaletteIndex, Is.EqualTo(11));
            yield return DestroyFixture(reopened);
        }

        [UnityTest]
        public IEnumerator PalettePageChange_DoesNotLeaveFocusOnAnInvisibleSwatch()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;
            Transform menu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            GameObject firstPageLastSwatch = FindRequired(menu, "Colour9").gameObject;
            EventSystem.current.SetSelectedGameObject(firstPageLastSwatch);
            FindRequired(menu, "ColourPage1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(firstPageLastSwatch.activeInHierarchy, Is.False);
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.activeInHierarchy, Is.True);
            Assert.That(selected.GetComponent<Selectable>().IsInteractable(), Is.True);
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator MenuButtonFeedback_UsesUnscaledTimeAndSettlesWithoutIdleAnimation()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            Transform menu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            MainMenuActionButton button = FindRequired(menu, "Credits").GetComponent<MainMenuActionButton>();
            Transform visual = FindRequired(button.transform, "Visual");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(MainMenuStyle.HoverDurationSeconds + 0.08f);
            Assert.That(visual.localScale.x, Is.EqualTo(MainMenuStyle.HoverScale).Within(0.001f));
            Assert.That(button.IsAnimating, Is.False);

            button.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(MainMenuStyle.PressDurationSeconds + 0.08f);
            Assert.That(visual.localScale.x, Is.EqualTo(MainMenuStyle.PressedScale).Within(0.001f));
            button.OnPointerUp(pointer);
            button.OnPointerExit(pointer);
            button.interactable = false;
            yield return new WaitForSecondsRealtime(MainMenuStyle.HoverDurationSeconds + 0.08f);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(button.IsAnimating, Is.False);
            yield return DestroyFixture(fixture);
        }
    }
}
