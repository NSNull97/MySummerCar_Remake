using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MSC.Core.Lifecycle;
using MSC.Economy;
using MSC.Presentation.AntiAliasing;
using MSC.Save;
using MSC.UI.Presentation;
using MSC.UI.Runtime.Routing;
using MSC.UI.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed class GameUiRootPlayModeTests
    {
        private string temporaryDirectory;
        private float originalTimeScale;
        private float originalDeadzone;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "MSC_UI_PlayMode_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            originalTimeScale = Time.timeScale;
            originalDeadzone = InputSystem.settings.defaultDeadzoneMin;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            InputSystem.settings.defaultDeadzoneMin = originalDeadzone;
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator BootMainMenuSettingsBackAndLockedRoutes_AreNavigableWithFocus()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("NewGame"));
            Selectable firstSelection = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
            Assert.That(firstSelection.FindSelectableOnDown(), Is.Not.Null, "Main menu has a controller focus trap.");

            GameObject initialSelection = EventSystem.current.currentSelectedGameObject;
            var controllerMove = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Down,
                moveVector = Vector2.down,
            };
            ExecuteEvents.Execute(initialSelection, controllerMove, ExecuteEvents.moveHandler);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.Not.EqualTo(initialSelection),
                "Controller-style directional navigation did not advance focus.");
            EventSystem.current.SetSelectedGameObject(initialSelection);

            Button settingsButton = FindRequired(fixture.Root.transform, "Settings").GetComponent<Button>();
            settingsButton.onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGraphics));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);

            UiRouteId[] lockedRoutes =
            {
                UiRouteId.MainMenu,
                UiRouteId.SettingsGraphics,
                UiRouteId.SettingsAudio,
                UiRouteId.SettingsControls,
                UiRouteId.SettingsGameplay,
                UiRouteId.InGameHud,
            };
            foreach (UiRouteId route in lockedRoutes)
            {
                fixture.Root.ShowReviewScreen(route);
                yield return null;
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(route));
                AssertActiveGraphicsInsideViewport(fixture.Root, route);
            }

            UiRouteId[] referencePendingRoutes =
            {
                UiRouteId.Loading,
                UiRouteId.Pause,
                UiRouteId.ConfirmationDialog,
                UiRouteId.SaveStatus,
                UiRouteId.SettingsAccessibility,
                UiRouteId.SettingsMods,
            };
            foreach (UiRouteId route in referencePendingRoutes)
            {
                fixture.Root.ShowReferencePendingReviewScreen(route);
                yield return null;
                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(route));
                AssertActiveGraphicsInsideViewport(fixture.Root, route);
                if (route != UiRouteId.Loading)
                {
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null, route.ToString());
                }
            }

            Transform saveStatus = FindRequired(fixture.Root.transform, UiRouteId.SaveStatus.ToString());
            Assert.That(
                FindRequired(saveStatus, "SaveStatusState").GetComponent<Text>().text,
                Does.Contain("unavailable").IgnoreCase);

            fixture.Root.ShowReferencePendingReviewScreen(UiRouteId.ConfirmationDialog);
            yield return null;
            FindRequired(fixture.Root.transform, "ConfirmationCancel").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            Button back = FindRequired(fixture.Root.transform, "Back").GetComponent<Button>();
            back.onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));

            Transform mainMenu = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Button mods = FindRequired(mainMenu, "Mods").GetComponent<Button>();
            Assert.That(mods.interactable, Is.True);
            mods.onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsMods));
            FindRequired(fixture.Root.transform, "Back").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator MainMenuDeveloperTools_InvokesComposedDevelopmentMenu()
        {
            bool invoked = false;
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                openDeveloperTools: () => invoked = true);
            yield return null;

            Transform main = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            Button developerTools = FindRequired(main, "DevTools")
                .GetComponent<Button>();
            developerTools.onClick.Invoke();

            Assert.That(invoked, Is.True);
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator NewGame_AppliesSelectedSatsumaColourBeforeActivation()
        {
            int appliedIndex = -1;
            Color appliedColor = Color.clear;
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                configureNewGameVehiclePaint: (
                    int paletteIndex,
                    Color bodyColor,
                    out string failure) =>
                {
                    appliedIndex = paletteIndex;
                    appliedColor = bodyColor;
                    failure = string.Empty;
                    return true;
                });
            yield return null;

            Transform main = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            Transform colourCard = FindRequired(main, "CarColourCard");
            FindRequired(colourCard, "Colour7")
                .GetComponent<Button>()
                .onClick.Invoke();
            FindRequired(main, "NewGame")
                .GetComponent<Button>()
                .onClick.Invoke();

            Assert.That(appliedIndex, Is.EqualTo(7));
            Color expected = new Color32(206, 196, 61, 255);
            Assert.That(appliedColor.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(appliedColor.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(appliedColor.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(appliedColor.a, Is.EqualTo(expected.a).Within(0.0001f));
            yield return null;
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.True);
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator MainMenuActionsAndHud_MatchBoundedApprovedPersistentContract()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            Assert.That(fixture.SessionGate.IsGameplayActive, Is.False);
            Assert.That(fixture.SessionGate.ActivationAttemptCount, Is.Zero);

            string[] orderedActions = { "Continue", "NewGame", "LoadGame", "Credits", "Quit" };
            Transform mainRoute = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            int previousSiblingIndex = -1;
            for (int index = 0; index < orderedActions.Length; index++)
            {
                Transform action = FindRequired(mainRoute, orderedActions[index]);
                Assert.That(
                    action.parent,
                    Is.EqualTo(mainRoute),
                    "Main-menu action left the locked top-level stack: " + orderedActions[index]);
                Assert.That(
                    action.GetSiblingIndex(),
                    Is.GreaterThan(previousSiblingIndex),
                    "Main-menu action order drifted for " + orderedActions[index]);
                previousSiblingIndex = action.GetSiblingIndex();
            }

            Assert.That(FindRequired(mainRoute, "Continue").GetComponent<Button>().interactable, Is.False);
            Assert.That(FindRequired(mainRoute, "LoadGame").GetComponent<Button>().interactable, Is.False);
            Assert.That(FindRequired(mainRoute, "NewGame").GetComponent<Button>().interactable, Is.True);

            Button credits = FindRequired(mainRoute, "Credits").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(credits.gameObject);
            credits.onClick.Invoke();
            yield return null;
            Button creditsBack = FindRequired(mainRoute, "CreditsBack").GetComponent<Button>();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(creditsBack.gameObject));
            creditsBack.onClick.Invoke();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(credits.gameObject));

            Button quit = FindRequired(mainRoute, "Quit").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            quit.onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.ConfirmationDialog));
            FindRequired(fixture.Root.transform, "ConfirmationCancel").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quit.gameObject));

            Transform colourCard = FindRequired(mainRoute, "CarColourCard");
            Button[] colourButtons = colourCard.GetComponentsInChildren<Button>(includeInactive: false);
            Assert.That(colourButtons, Has.Length.EqualTo(12));
            Assert.That(FindRequired(colourCard, "Colour0").GetComponent<Outline>().enabled, Is.True);
            colourButtons[1].onClick.Invoke();
            Assert.That(FindRequired(colourCard, "Colour0").GetComponent<Outline>().enabled, Is.False);
            Assert.That(FindRequired(colourCard, "Colour1").GetComponent<Outline>().enabled, Is.True);
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));

            Button newGame = FindRequired(mainRoute, "NewGame").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(newGame.gameObject);
            newGame.onClick.Invoke();
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Loading));
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.False);
            Transform loading = FindRequired(
                fixture.Root.transform,
                UiRouteId.Loading.ToString());
            string loadingWait =
                FindRequired(loading, "LoadingWait").GetComponent<Text>().text;
            Assert.That(
                new[] { "PLEASE WAIT…", "ПОЖАЛУЙСТА, ПОДОЖДИТЕ…" },
                Does.Contain(loadingWait));
            Assert.That(FindOptional(loading, "LoadingReferenceState"), Is.Null);
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.True);
            Assert.That(fixture.SessionGate.ActivationAttemptCount, Is.EqualTo(1));
            Assert.That(fixture.Gate.GameplayInputEnabled, Is.True);
            Assert.That(fixture.Gate.UiSuppressed, Is.False);

            fixture.Root.ShowReviewScreen(UiRouteId.InGameHud);
            yield return null;
            Transform hud = FindRequired(fixture.Root.transform, UiRouteId.InGameHud.ToString());
            Transform needs = FindRequired(hud, "Needs");
            RectTransform needsRect = (RectTransform)needs;
            RectTransform clockRect = (RectTransform)FindRequired(hud, "Clock");
            RectTransform moneyRect = (RectTransform)FindRequired(hud, "Money");
            RectTransform dayRect = (RectTransform)FindRequired(clockRect, "Day");
            RectTransform dateRect = (RectTransform)FindRequired(clockRect, "Date");
            Transform fpsCounter = FindRequired(hud, "FpsCounter");
            RectTransform fpsRect = (RectTransform)fpsCounter;
            Assert.That(
                needsRect.anchoredPosition.x,
                Is.LessThan(clockRect.anchoredPosition.x));
            Assert.That(
                needsRect.anchoredPosition.y,
                Is.EqualTo(clockRect.anchoredPosition.y).Within(0.01f));
            Assert.That(
                moneyRect.anchoredPosition.x,
                Is.EqualTo(clockRect.anchoredPosition.x).Within(0.01f));
            Assert.That(
                moneyRect.anchoredPosition.y,
                Is.LessThan(clockRect.anchoredPosition.y));
            Assert.That(dayRect.gameObject.activeSelf, Is.False);
            Assert.That(dateRect.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(dateRect.rect.width, Is.EqualTo(180f).Within(0.01f));
            Assert.That(dateRect.GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(dateRect.GetComponent<Text>().text, Is.EqualTo("FRI, 27 JUN"));
            Assert.That(fpsCounter.gameObject.activeSelf, Is.True);
            Assert.That(
                fpsRect.anchoredPosition.x,
                Is.GreaterThan(clockRect.anchoredPosition.x));
            Assert.That(
                fpsRect.anchoredPosition.y,
                Is.LessThan(clockRect.anchoredPosition.y));
            Text fpsLabel = FindRequired(fpsCounter, "FpsLabel").GetComponent<Text>();
            Text fpsValue = FindRequired(fpsCounter, "FpsValue").GetComponent<Text>();
            Assert.That(fpsLabel.text, Is.EqualTo("FPS"));
            Assert.That(fpsLabel.color, Is.EqualTo(UiThemeTokens.Accent));
            Assert.That(fpsValue.color, Is.EqualTo(Color.white));
            Assert.That(fpsLabel.fontSize, Is.EqualTo(17));
            Assert.That(fpsValue.fontSize, Is.EqualTo(17));
            Assert.That(fpsLabel.fontSize, Is.EqualTo(fpsValue.fontSize));
            Assert.That(
                ((RectTransform)fpsValue.transform).anchoredPosition.x,
                Is.LessThan(((RectTransform)fpsLabel.transform).anchoredPosition.x));
            Assert.That(fpsLabel.GetComponent<Shadow>(), Is.Not.Null);
            Assert.That(fpsValue.GetComponent<Shadow>(), Is.Not.Null);
            Assert.That(fpsCounter.GetComponent<Mask>(), Is.Null);
            Assert.That(FindOptional(fpsCounter, "BackdropSlice"), Is.Null);
            Assert.That(FindOptional(fpsCounter, "Divider"), Is.Null);
            FieldInfo frameSamplesField = typeof(GameUiRoot).GetField(
                "frameSamples",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(frameSamplesField, Is.Not.Null);
            var frameSamples = (Queue<float>)frameSamplesField.GetValue(fixture.Root);
            frameSamples.Clear();
            for (int index = 0; index < 30; index++)
            {
                frameSamples.Enqueue(0.01f);
            }

            MethodInfo refreshFps = typeof(GameUiRoot).GetMethod(
                "RefreshHudFpsCounter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshFps, Is.Not.Null);
            refreshFps.Invoke(fixture.Root, new object[] { true });
            Assert.That(fpsValue.text, Is.EqualTo("100"));
            Assert.That(fpsValue.text, Does.Not.Contain("FPS"));
            string[] needLabels =
            {
                "NeedLabel0", "NeedLabel1", "NeedLabel2",
                "NeedLabel3", "NeedLabel4", "NeedLabel5",
            };
            float previousNeedY = float.PositiveInfinity;
            foreach (string label in needLabels)
            {
                Assert.That(FindRequired(needs, label).GetComponent<Text>().text, Is.Not.Empty);
                RectTransform group = (RectTransform)FindRequired(
                    needs,
                    "Need" + Array.IndexOf(needLabels, label));
                Assert.That(group.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(group.anchoredPosition.y, Is.LessThan(previousNeedY));
                previousNeedY = group.anchoredPosition.y;
            }

            Transform moneyValue = FindRequired(FindRequired(hud, "Money"), "MoneyValue");
            Transform moneyCurrency = FindRequired(FindRequired(hud, "Money"), "MoneyCurrency");
            Assert.That(moneyValue.GetComponent<Text>().text, Is.EqualTo("12,345"));
            Assert.That(moneyCurrency.GetComponent<Text>().text, Is.EqualTo("MK"));
            Assert.That(moneyValue.GetComponent<Text>().fontSize, Is.EqualTo(17));
            Assert.That(moneyCurrency.GetComponent<Text>().fontSize, Is.EqualTo(17));
            Assert.That(
                moneyValue.GetComponent<Text>().font.name,
                Does.Contain("HelveticaNeue"));
            Assert.That(
                ((RectTransform)moneyValue).anchoredPosition.x,
                Is.LessThan(((RectTransform)moneyCurrency).anchoredPosition.x));

            Text[] allRouteTexts = fixture.Root.GetComponentsInChildren<Text>(includeInactive: true);
            Assert.That(allRouteTexts, Is.Not.Empty);
            foreach (Text routeText in allRouteTexts)
            {
                Assert.That(routeText.font, Is.Not.Null, routeText.name);
                Assert.That(routeText.font.name, Does.Contain("HelveticaNeue"), routeText.name);
            }

            Text[] needValues = needs.GetComponentsInChildren<Text>(includeInactive: false)
                .Where(value => value.name.StartsWith("NeedValue", StringComparison.Ordinal))
                .ToArray();
            Assert.That(needValues, Is.Empty);
            Assert.That(
                needs.GetComponentsInChildren<Text>(includeInactive: false)
                    .Any(value => value.text.Contains("%", StringComparison.Ordinal)),
                Is.False);
            Assert.That(FindRequired(hud, "Clock").GetComponentInChildren<Shadow>(), Is.Not.Null);
            Assert.That(FindRequired(hud, "Money").GetComponentInChildren<Shadow>(), Is.Not.Null);
            float[] reviewValues = { 0.18f, 0.36f, 0.22f, 0.28f, 0.44f, 0.71f };
            for (int index = 0; index < 6; index++)
            {
                Transform group = FindRequired(needs, "Need" + index);
                Assert.That(FindRequired(group, "NeedLabel" + index).GetComponent<Text>().fontSize, Is.EqualTo(14));
                Assert.That(FindOptional(group, "NeedValue" + index), Is.Null);
                RectTransform track = (RectTransform)FindRequired(group, "NeedTrack" + index);
                Assert.That(track.rect.width, Is.EqualTo(112f).Within(0.01f));
                Assert.That(track.rect.height, Is.EqualTo(3f).Within(0.01f));
                Image needIcon = FindRequired(group, "NeedIcon" + index).GetComponent<Image>();
                Assert.That(((RectTransform)needIcon.transform).rect.width, Is.EqualTo(28f).Within(0.01f));
                Assert.That(needIcon.color, Is.EqualTo(Color.white));
                Assert.That(needIcon.sprite.name, Does.StartWith("UI08A_Lucide_"));
                Assert.That(needIcon.sprite.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(needIcon.sprite.texture.width, Is.EqualTo(64));
                Assert.That(needIcon.GetComponent<Shadow>(), Is.Not.Null);
                Assert.That(FindRequired(group, "NeedTrack" + index).GetComponent<Shadow>(), Is.Not.Null);
                Image fill = FindRequired(group, "NeedFill" + index).GetComponent<Image>();
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
                Assert.That(fill.fillAmount, Is.EqualTo(reviewValues[index]).Within(0.001f));
                Assert.That(fill.sprite, Is.Not.Null);
                Assert.That(fill.GetComponent<Shadow>(), Is.Not.Null);
                Image indicator = FindRequired(group, "NeedIndicator" + index).GetComponent<Image>();
                Assert.That(((RectTransform)indicator.transform).rect.width, Is.EqualTo(7f).Within(0.01f));
                Assert.That(indicator.color, Is.EqualTo(Color.white));
                Assert.That(indicator.GetComponent<Shadow>(), Is.Not.Null);
            }

            string[] forbiddenNames =
            {
                "gps", "minimap", "quest", "telemetry", "hotbar",
                "speedometer", "tachometer", "gear", "fuel",
            };
            string[] hudNames = hud.GetComponentsInChildren<Transform>(includeInactive: true)
                .Select(value => value.name.ToLowerInvariant())
                .ToArray();
            foreach (string forbidden in forbiddenNames)
            {
                Assert.That(hudNames.Any(value => value.Contains(forbidden)), Is.False, forbidden);
            }

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator NewGame_WaitsForGameplayWorldPreparationBeforeActivation()
        {
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                gameplayPrepared: false);
            yield return null;

            Transform mainRoute = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            FindRequired(mainRoute, "NewGame").GetComponent<Button>()
                .onClick.Invoke();

            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Loading));
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.False);
            yield return null;

            Assert.That(fixture.SessionGate.PreparationAttemptCount, Is.EqualTo(1));
            Assert.That(fixture.SessionGate.IsGameplayPreparationRunning, Is.True);
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Loading));
            Assert.That(fixture.SessionGate.ActivationAttemptCount, Is.Zero);

            fixture.SessionGate.CompletePreparation();
            yield return null;

            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
            Assert.That(fixture.SessionGate.IsGameplayPrepared, Is.True);
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.True);
            Assert.That(fixture.SessionGate.ActivationAttemptCount, Is.EqualTo(1));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator NativeSaveSlots_EnableContinueAndRequestCleanSessionLoad()
        {
            var service = new SaveServiceProbe(
                CreateSlot("slot-old", "Old save", "2026-07-20T08:00:00.0000000Z"),
                CreateSlot("slot-new", "Latest save", "2026-07-21T08:00:00.0000000Z"));
            string requestedSlot = null;
            bool RequestLoad(string slotId, out string failure)
            {
                requestedSlot = slotId;
                failure = string.Empty;
                return true;
            }

            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                saveService: service,
                requestLoad: RequestLoad);
            yield return null;

            Transform main = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Button continueButton = FindRequired(main, "Continue").GetComponent<Button>();
            Button loadButton = FindRequired(main, "LoadGame").GetComponent<Button>();
            Assert.That(continueButton.interactable, Is.True);
            Assert.That(loadButton.interactable, Is.True);
            Assert.That(
                FindRequired(continueButton.transform, "ContinueHelper").GetComponent<Text>().text,
                Does.Contain("Latest save"));

            continueButton.onClick.Invoke();
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Loading));
            yield return null;

            Assert.That(requestedSlot, Is.EqualTo("slot-new"));
            Assert.That(service.LoadCallCount, Is.Zero, "UI must not restore into an already revealed world.");
            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator CorruptOnlySlot_IsVisibleButCannotBeLoaded()
        {
            var service = new SaveServiceProbe(
                new SaveSlotSummary(
                    "slot-corrupt",
                    null,
                    string.Empty,
                    isValid: false,
                    message: "Integrity validation failed."));
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                saveService: service,
                requestLoad: AcceptLoadRequest);
            yield return null;

            Transform main = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            Button continueButton =
                FindRequired(main, "Continue").GetComponent<Button>();
            Button loadButton =
                FindRequired(main, "LoadGame").GetComponent<Button>();
            Assert.That(continueButton.interactable, Is.False);
            Assert.That(loadButton.interactable, Is.True);

            loadButton.onClick.Invoke();
            yield return null;

            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SaveStatus));
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusState")
                    .GetComponent<Text>().text,
                Does.Match("(?i)(invalid|поврежден)"));
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusLoad")
                    .GetComponent<Button>().interactable,
                Is.False);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator StartupRecoveryStatus_IsPresentedAfterUiBinding()
        {
            var service = new SaveServiceProbe(
                CreateSlot(
                    "slot-recovered",
                    "Recovered save",
                    "2026-07-21T08:00:00.0000000Z"));
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                saveService: service,
                requestLoad: AcceptLoadRequest,
                initialSaveReadStatus:
                    SaveReadStatus.RecoveredFromInterruptedWrite);
            yield return null;

            fixture.Root.ShowReferencePendingReviewScreen(UiRouteId.SaveStatus);
            yield return null;

            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusOperation")
                    .GetComponent<Text>().text,
                Does.Match("(?i)(interrupted|прерванн)"));
            Transform notice = FindRequired(
                fixture.Root.transform,
                "SettingsNotice");
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(
                FindRequired(notice, "NoticeText").GetComponent<Text>().text,
                Does.Match("(?i)(recovered|восстанов)"));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator StartupLoadFailure_IsPresentedAfterUiBinding()
        {
            var service = new SaveServiceProbe(
                CreateSlot(
                    "slot-failed",
                    "Failed save",
                    "2026-07-21T08:00:00.0000000Z"));
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                saveService: service,
                requestLoad: AcceptLoadRequest,
                initialSaveStatus: "Integrity validation failed.");
            yield return null;

            Transform notice = FindRequired(
                fixture.Root.transform,
                "SettingsNotice");
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(
                FindRequired(notice, "NoticeText").GetComponent<Text>().text,
                Does.Match("(?i)(load|загруз)"));

            fixture.Root.ShowReferencePendingReviewScreen(UiRouteId.SaveStatus);
            yield return null;
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusOperation")
                    .GetComponent<Text>().text,
                Does.Contain("Integrity validation failed."));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator PauseSaveStatus_WritesThroughRealServiceAndRefreshesSlotMetadata()
        {
            var service = new SaveServiceProbe();
            UiFixture fixture = CreateFixture(
                startInMainMenu: false,
                saveService: service,
                requestLoad: AcceptLoadRequest,
                createSaveRequest: slotId => new SaveRequest
                {
                    SlotId = slotId,
                    BuildId = "ui-test",
                    Metadata = new SaveMetadata
                    {
                        DisplayName = "Garage save",
                        GameTimestamp = "SAT 14:37",
                        LocationStableId = "home.garage",
                    },
                },
                preferredSaveSlotId: "slot-active");
            yield return null;

            InvokePrivate(fixture.Root, "EnterPause");
            FindRequired(fixture.Root.transform, "PauseSaveStatus")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;

            Button saveButton = FindRequired(fixture.Root.transform, "SaveStatusSave")
                .GetComponent<Button>();
            Assert.That(saveButton.interactable, Is.True);
            saveButton.onClick.Invoke();

            Assert.That(service.LastSaveRequest, Is.Not.Null);
            Assert.That(service.LastSaveRequest.SlotId, Is.EqualTo("slot-active"));
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusState").GetComponent<Text>().text,
                Is.EqualTo("Garage save"));
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusDetail").GetComponent<Text>().text,
                Does.Contain("home.garage"));
            Assert.That(
                FindRequired(fixture.Root.transform, "SaveStatusLoad").GetComponent<Button>().interactable,
                Is.True);
            Transform main = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Assert.That(FindRequired(main, "Continue").GetComponent<Button>().interactable, Is.True);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator SettingsChromeGeometry_IsInvariantAcrossCategories()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            yield return null;

            UiRouteId[] settingsRoutes =
            {
                UiRouteId.SettingsGraphics,
                UiRouteId.SettingsAudio,
                UiRouteId.SettingsControls,
                UiRouteId.SettingsGameplay,
                UiRouteId.SettingsAccessibility,
                UiRouteId.SettingsMods,
            };
            string[] chromeNames =
            {
                "ProjectOwnedLogo",
                "NavigateSettingsGraphics",
                "NavigateSettingsAudio",
                "NavigateSettingsControls",
                "NavigateSettingsGameplay",
                "NavigateSettingsAccessibility",
                "NavigateSettingsMods",
                "Back",
                "Greeting",
            };

            Transform graphicsRoute = FindRequired(
                fixture.Root.transform,
                UiRouteId.SettingsGraphics.ToString());
            RectTransform[] baseline = chromeNames
                .Select(name => (RectTransform)FindRequired(graphicsRoute, name))
                .ToArray();

            for (int routeIndex = 1; routeIndex < settingsRoutes.Length; routeIndex++)
            {
                UiRouteId target = settingsRoutes[routeIndex];
                Transform currentRoute = FindRequired(
                    fixture.Root.transform,
                    fixture.Root.CurrentRoute.ToString());
                FindRequired(currentRoute, "Navigate" + target)
                    .GetComponent<Button>()
                    .onClick
                    .Invoke();
                yield return null;

                Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(target));
                Transform targetRoute = FindRequired(fixture.Root.transform, target.ToString());
                for (int chromeIndex = 0; chromeIndex < chromeNames.Length; chromeIndex++)
                {
                    AssertRectGeometryEqual(
                        baseline[chromeIndex],
                        (RectTransform)FindRequired(targetRoute, chromeNames[chromeIndex]),
                        target + "/" + chromeNames[chromeIndex]);
                }
            }

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator RoundedSurfacesAndSpacing_FollowRemediatedVisualContract()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            Transform main = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Transform colour = FindRequired(main, "CarColourCard");
            Transform interior = FindRequired(main, "InteriorCard");
            Transform performance = FindRequired(main, "PerformanceCard");
            Transform music = FindRequired(main, "MusicCard");
            AssertRoundedSurface(FindRequired(main, "NewGame"), 0.33f);
            AssertCrispBorder(FindRequired(main, "NewGame"));
            Assert.That(
                FindRequired(FindRequired(main, "NewGame"), "Border")
                    .GetComponent<Image>().color.a,
                Is.EqualTo(UiThemeTokens.EmphasizedBorderAlpha).Within(0.001f));
            AssertRoundedSurface(colour, 0.33f);
            Assert.That(HorizontalGap(colour, interior), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f));
            Assert.That(HorizontalGap(interior, performance), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f));
            Assert.That(VerticalGap(colour, music), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingMedium - 0.01f));

            Transform utility = FindRequired(main, "UtilityStrip");
            Transform utilitySettings = FindRequired(utility, "Settings");
            Transform utilityMods = FindRequired(utility, "Mods");
            Assert.That(HorizontalGap(utilitySettings, utilityMods), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f));
            Transform utilityDev = utility.Find("DevTools");
            if (utilityDev != null)
            {
                Assert.That(HorizontalGap(utilityMods, utilityDev), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f));
            }

            string[] mainActionNames = { "Continue", "NewGame", "LoadGame", "Credits", "Quit" };
            RectTransform firstMainAction = (RectTransform)FindRequired(main, mainActionNames[0]);
            foreach (string actionName in mainActionNames.Skip(1))
            {
                RectTransform action = (RectTransform)FindRequired(main, actionName);
                Assert.That(action.rect.size, Is.EqualTo(firstMainAction.rect.size), actionName);
            }

            RectTransform greeting = (RectTransform)FindRequired(main, "Greeting");
            Assert.That(
                greeting.anchoredPosition.x + greeting.rect.width,
                Is.EqualTo(firstMainAction.anchoredPosition.x + firstMainAction.rect.width)
                    .Within(0.01f),
                "Greeting must share the main-action right edge.");

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            yield return null;
            Transform graphics = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
            Transform graphicsPanel = FindRequired(graphics, "GraphicsSettingsPanel");
            AssertRoundedSurface(graphicsPanel, 0.33f);
            AssertCrispBorder(graphicsPanel);
            Assert.That(
                FindRequired(graphicsPanel, "Border").GetComponent<Image>().color.a,
                Is.EqualTo(UiThemeTokens.Border.a).Within(0.001f));
            Assert.That(FindOptional(graphics, "GraphicsPreview"), Is.Null);
            Assert.That(HorizontalGap(FindRequired(graphics, "NavigateSettingsGraphics"), graphicsPanel), Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f));
            AssertStandardSettingsActions(graphics);

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsAudio);
            yield return null;
            Transform audio = FindRequired(fixture.Root.transform, UiRouteId.SettingsAudio.ToString());
            AssertStandardSettingsActions(audio);
            AssertRoundedSurface(FindRequired(FindRequired(audio, "MasterSlider"), "Track"), 0.25f);
            Transform handle = FindRequired(FindRequired(audio, "MasterSlider"), "Handle");
            var handleRect = (RectTransform)handle;
            Assert.That(handleRect.rect.width, Is.EqualTo(handleRect.rect.height).Within(0.01f));
            Assert.That(handleRect.rect.width, Is.EqualTo(16f).Within(0.01f));
            Assert.That(handleRect.anchorMin.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(handleRect.anchorMax.y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(handle.GetComponent<Image>().type, Is.EqualTo(Image.Type.Simple));

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsControls);
            yield return null;
            Transform controls = FindRequired(fixture.Root.transform, UiRouteId.SettingsControls.ToString());
            AssertStandardSettingsActions(controls);

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGameplay);
            yield return null;
            Transform gameplay = FindRequired(fixture.Root.transform, UiRouteId.SettingsGameplay.ToString());
            AssertStandardSettingsActions(gameplay);

            fixture.Root.ShowReferencePendingReviewScreen(UiRouteId.SettingsAccessibility);
            yield return null;
            Transform accessibility = FindRequired(fixture.Root.transform, UiRouteId.SettingsAccessibility.ToString());
            AssertStandardSettingsActions(accessibility);

            fixture.Root.ShowReviewScreen(UiRouteId.MainMenu);
            yield return null;
            main = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            FindRequired(main, "NewGame").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Transform hud = FindRequired(fixture.Root.transform, UiRouteId.InGameHud.ToString());
            Assert.That(FindRequired(hud, "Clock").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "Money").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "Needs").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "FpsCounter").GetComponent<Mask>(), Is.Null);

            InvokePrivate(fixture.Root, "EnterPause");
            yield return null;
            Transform pause = FindRequired(fixture.Root.transform, UiRouteId.Pause.ToString());
            AssertRoundedSurface(FindRequired(pause, "PausePanel"), 0.33f);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator RemediatedBackdropGlassAndRasterizationContracts_AreActive()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            Assert.That(fixture.Root.ActiveBackdropModeName, Is.EqualTo("MenuStatic"));
            Assert.That(fixture.Root.UsesPixelPerfectCanvas, Is.True);
            Assert.That(fixture.Root.ProceduralAssetsUseFractionalAlphaCoverage, Is.True);
            CanvasScaler responsiveScaler = FindRequired(
                    fixture.Root.transform,
                    "M08A_GameUI")
                .GetComponent<CanvasScaler>();
            Assert.That(responsiveScaler, Is.Not.Null);
            Assert.That(
                responsiveScaler.screenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            RectTransform responsiveFrame = (RectTransform)FindRequired(
                fixture.Root.transform,
                "ReferenceFrame_1672x941");
            Canvas.ForceUpdateCanvases();
            Assert.That(
                responsiveFrame.rect.size,
                Is.EqualTo(new Vector2(
                    UiThemeTokens.ReferenceWidth,
                    UiThemeTokens.ReferenceHeight)));
            Assert.That(responsiveFrame.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(responsiveFrame.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(responsiveFrame.anchoredPosition, Is.EqualTo(Vector2.zero));
            var frameCorners = new Vector3[4];
            responsiveFrame.GetWorldCorners(frameCorners);
            Assert.That(frameCorners[0].x, Is.GreaterThanOrEqualTo(-0.5f));
            Assert.That(frameCorners[0].y, Is.GreaterThanOrEqualTo(-0.5f));
            Assert.That(frameCorners[2].x, Is.LessThanOrEqualTo(Screen.width + 0.5f));
            Assert.That(frameCorners[2].y, Is.LessThanOrEqualTo(Screen.height + 0.5f));
            Assert.That(fixture.Root.RegisteredGlassSurfaceCount, Is.GreaterThan(12));
            Transform backdropLayer = FindRequired(fixture.Root.transform, "BackdropLayer");
            Transform accessibleScaleRoot = FindRequired(
                fixture.Root.transform,
                "AccessibleScaleRoot");
            Assert.That(
                backdropLayer.IsChildOf(accessibleScaleRoot),
                Is.False,
                "The menu backdrop must not shrink with accessibility UI scale.");
            Assert.That(backdropLayer.localScale, Is.EqualTo(Vector3.one));

            Transform mainMenu = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            Transform newGame = FindRequired(mainMenu, "NewGame");
            Assert.That(newGame.GetComponent<Mask>(), Is.Not.Null);
            RawImage mainButtonSlice = FindRequired(newGame, "BackdropSlice")
                .GetComponent<RawImage>();
            Assert.That(mainButtonSlice, Is.Not.Null);
            Color initialTint = FindRequired(newGame, "GlassTint").GetComponent<Image>().color;

            FindRequired(mainMenu, "Colour1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Color changedTint = FindRequired(newGame, "GlassTint").GetComponent<Image>().color;
            Assert.That(changedTint, Is.Not.EqualTo(initialTint));
            float maximumTintDelta = Mathf.Max(
                Mathf.Abs(changedTint.r - initialTint.r),
                Mathf.Abs(changedTint.g - initialTint.g),
                Mathf.Abs(changedTint.b - initialTint.b));
            Assert.That(
                maximumTintDelta,
                Is.LessThan(0.10f),
                "Car colour should remain a restrained glass tint, not a saturated fill.");

            FindRequired(mainMenu, "NewGame").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.ActiveBackdropModeName, Is.EqualTo("HudLiveGlass"));
            Transform hud = FindRequired(
                fixture.Root.transform,
                UiRouteId.InGameHud.ToString());
            Assert.That(FindRequired(hud, "Clock").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "Money").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "Needs").GetComponent<Mask>(), Is.Null);
            Assert.That(FindRequired(hud, "FpsCounter").GetComponent<Mask>(), Is.Null);
            Assert.That(FindOptional(FindRequired(hud, "Clock"), "BackdropSlice"), Is.Null);
            Assert.That(FindOptional(FindRequired(hud, "Money"), "BackdropSlice"), Is.Null);
            Assert.That(FindOptional(FindRequired(hud, "Needs"), "BackdropSlice"), Is.Null);
            Assert.That(FindOptional(FindRequired(hud, "FpsCounter"), "BackdropSlice"), Is.Null);
            Assert.That(FindOptional(FindRequired(hud, "FpsCounter"), "Divider"), Is.Null);

            InvokePrivate(fixture.Root, "EnterPause");
            yield return null;
            Assert.That(fixture.Root.ActiveBackdropModeName, Is.EqualTo("PauseFrozen"));
            Assert.That(
                FindRequired(fixture.Root.transform, "PauseBackdropDim").GetComponent<Image>().enabled,
                Is.True);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator NoticePanel_DisappearsTogetherWithItsMessage()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            Transform notice = FindRequired(fixture.Root.transform, "SettingsNotice");
            Assert.That(notice.gameObject.activeSelf, Is.False);

            MethodInfo showNotice = typeof(GameUiRoot).GetMethod(
                "ShowNotice",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showNotice, Is.Not.Null);
            showNotice.Invoke(fixture.Root, new object[] { "ui.notice.saved" });
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(
                FindRequired(notice, "NoticeText").GetComponent<Text>().text,
                Is.Not.Empty);

            SetPrivateField(
                fixture.Root,
                "noticeUntilUnscaledTime",
                Time.unscaledTime - 0.01f);
            yield return null;

            Assert.That(notice.gameObject.activeSelf, Is.False);
            Assert.That(
                FindRequired(notice, "NoticeText").GetComponent<Text>().text,
                Is.Empty);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator PauseInput_SuspendsAndRestoresGameplayGates()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: false);
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
            Assert.That(fixture.Gate.GameplayInputEnabled, Is.True);
            InputAction pause = fixture.PlayerActions.FindAction("System/Pause", throwIfNotFound: true);
            Assert.That(pause.enabled, Is.True);
            Assert.That(pause.bindings.Any(binding => binding.effectivePath == "<Keyboard>/escape"), Is.True);

            InvokePrivate(fixture.Root, "EnterPause");
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Pause));
            Assert.That(fixture.Gate.GameplayInputEnabled, Is.False);
            Assert.That(fixture.SecondaryGate.GameplayInputEnabled, Is.False);
            Assert.That(fixture.Gate.UiSuppressed, Is.True);
            Assert.That(fixture.SecondaryGate.UiSuppressed, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            Button saveStatus = FindRequired(fixture.Root.transform, "PauseSaveStatus").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(saveStatus.gameObject);
            saveStatus.onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SaveStatus));
            FindRequired(fixture.Root.transform, "SaveStatusBack").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Pause));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(saveStatus.gameObject));

            InvokePrivate(fixture.Root, "EnterGameplay");
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
            Assert.That(fixture.Gate.GameplayInputEnabled, Is.True);
            Assert.That(fixture.SecondaryGate.GameplayInputEnabled, Is.True);
            Assert.That(fixture.Gate.UiSuppressed, Is.False);
            Assert.That(fixture.SecondaryGate.UiSuppressed, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator NewGame_RestoresInputGateActivatedAfterMainMenuSuspension()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            var deferredPlayer = new GameObject("DeferredPlayerInput");
            deferredPlayer.transform.SetParent(
                fixture.GameplayRoot.transform,
                worldPositionStays: false);
            DeferredInputGateProbe deferredGate =
                deferredPlayer.AddComponent<DeferredInputGateProbe>();
            deferredPlayer.SetActive(false);
            var intentionallyDisabledPlayer = new GameObject(
                "IntentionallyDisabledPlayerInput");
            intentionallyDisabledPlayer.transform.SetParent(
                fixture.GameplayRoot.transform,
                worldPositionStays: false);
            DeferredInputGateProbe intentionallyDisabledGate =
                intentionallyDisabledPlayer.AddComponent<DeferredInputGateProbe>();
            intentionallyDisabledGate.enabled = false;
            intentionallyDisabledPlayer.SetActive(false);

            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.MainMenu));
            Assert.That(deferredGate.enabled, Is.False);
            Assert.That(deferredGate.IsGameplayInputEnabled, Is.False);

            Transform mainMenu = FindRequired(
                fixture.Root.transform,
                UiRouteId.MainMenu.ToString());
            FindRequired(mainMenu, "NewGame").GetComponent<Button>().onClick.Invoke();
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.Loading));

            // ProductionWorldStreamingInstaller activates the prepared player
            // before GameUiRoot restores the state captured behind Main Menu.
            deferredPlayer.SetActive(true);
            intentionallyDisabledPlayer.SetActive(true);
            yield return null;

            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.InGameHud));
            Assert.That(fixture.SessionGate.IsGameplayActive, Is.True);
            Assert.That(deferredGate.enabled, Is.True);
            Assert.That(deferredGate.IsGameplayInputEnabled, Is.True);
            Assert.That(intentionallyDisabledGate.enabled, Is.False);
            Assert.That(
                intentionallyDisabledGate.IsGameplayInputEnabled,
                Is.False);

            InvokePrivate(fixture.Root, "EnterPause");
            yield return null;
            Assert.That(deferredGate.IsGameplayInputEnabled, Is.False);

            InvokePrivate(fixture.Root, "EnterGameplay");
            yield return null;
            Assert.That(deferredGate.IsGameplayInputEnabled, Is.True);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator LiveMoneyProvider_RefreshesAcceptedHudWithoutReviewData()
        {
            var money = new PlayerMoneyServiceProbe(300_000);
            UiFixture fixture = CreateFixture(
                startInMainMenu: false,
                playerMoney: money);
            yield return null;

            Transform hud = FindRequired(
                fixture.Root.transform,
                UiRouteId.InGameHud.ToString());
            Text moneyValue = FindRequired(
                    FindRequired(hud, "Money"),
                    "MoneyValue")
                .GetComponent<Text>();
            Assert.That(moneyValue.text, Is.EqualTo("3,000"));

            money.SetBalance(123_456);
            yield return null;
            Assert.That(moneyValue.text, Is.EqualTo("1,235"));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator PersistedAccessibilityScaleAndControls_AreAppliedAtBoot()
        {
            string settingsPath = Path.Combine(temporaryDirectory, "ui-settings.json");
            UiSettingsDocument document = UiSettingsDefaults.Create();
            document.Accessibility.UiScale = 0.85f;
            document.Controls.MouseSensitivity = 1.75f;
            document.Controls.GamepadDeadzone = 0.2f;
            document.Graphics.HorizontalFieldOfViewDegrees = 100f;
            document.Graphics.CameraFarClipMeters = 1800f;
            document.Gameplay.LanguageId = "ru-RU";
            new UiSettingsJsonStore(settingsPath).Save(document);

            UiFixture fixture = CreateFixture(startInMainMenu: true, settingsPath: settingsPath);
            yield return null;

            Transform scaleRoot = FindRequired(fixture.Root.transform, "AccessibleScaleRoot");
            Assert.That(scaleRoot.localScale.x, Is.EqualTo(0.85f).Within(0.001f));
            Transform backdrop = FindRequired(fixture.Root.transform, "BackdropLayer");
            Assert.That(backdrop.localScale, Is.EqualTo(Vector3.one));
            Assert.That(backdrop.IsChildOf(scaleRoot), Is.False);
            Assert.That(fixture.LookSink.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.LookSink.MouseSensitivity, Is.EqualTo(1.75f).Within(0.001f));
            Assert.That(fixture.LookSink.CameraApplyCount, Is.EqualTo(1));
            Assert.That(
                fixture.LookSink.HorizontalFieldOfViewDegrees,
                Is.EqualTo(100f).Within(0.001f));
            Assert.That(
                fixture.LookSink.CameraFarClipMeters,
                Is.EqualTo(1800f).Within(0.001f));
            Assert.That(InputSystem.settings.defaultDeadzoneMin, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(fixture.LookSink.LocaleApplyCount, Is.EqualTo(1));
            Assert.That(fixture.LookSink.LocaleId, Is.EqualTo("ru-RU"));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator CameraGraphicsSliders_ApplyLiveAndPersist()
        {
            string settingsPath = Path.Combine(
                temporaryDirectory,
                "camera-settings.json");
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                settingsPath: settingsPath);
            yield return null;

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            yield return null;
            Transform graphics = FindRequired(
                fixture.Root.transform,
                UiRouteId.SettingsGraphics.ToString());
            Slider fieldOfView = FindRequired(
                    graphics,
                    "HorizontalFovSlider")
                .GetComponent<Slider>();
            Slider farClip = FindRequired(
                    graphics,
                    "CameraFarClipSlider")
                .GetComponent<Slider>();

            fieldOfView.value = Mathf.InverseLerp(
                GraphicsSettingsDto.MinimumHorizontalFieldOfViewDegrees,
                GraphicsSettingsDto.MaximumHorizontalFieldOfViewDegrees,
                96f);
            farClip.value = Mathf.InverseLerp(
                GraphicsSettingsDto.MinimumCameraFarClipMeters,
                GraphicsSettingsDto.MaximumCameraFarClipMeters,
                2400f);
            FindRequired(graphics, "Apply")
                .GetComponent<Button>()
                .onClick.Invoke();
            yield return null;

            Assert.That(fixture.LookSink.CameraApplyCount, Is.EqualTo(2));
            Assert.That(
                fixture.LookSink.HorizontalFieldOfViewDegrees,
                Is.EqualTo(96f).Within(0.001f));
            Assert.That(
                fixture.LookSink.CameraFarClipMeters,
                Is.EqualTo(2400f).Within(0.001f));

            UiSettingsLoadResult reloaded =
                new UiSettingsJsonStore(settingsPath).LoadOrCreate();
            Assert.That(
                reloaded.Document.Graphics.HorizontalFieldOfViewDegrees,
                Is.EqualTo(96f).Within(0.001f));
            Assert.That(
                reloaded.Document.Graphics.CameraFarClipMeters,
                Is.EqualTo(2400f).Within(0.001f));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator AntiAliasingGraphicsSettings_ApplyRuntimeAndPersist()
        {
            string settingsPath = Path.Combine(
                temporaryDirectory,
                "anti-aliasing-settings.json");
            UiFixture fixture = CreateFixture(
                startInMainMenu: true,
                settingsPath: settingsPath);
            yield return null;

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            yield return null;
            Transform graphics = FindRequired(
                fixture.Root.transform,
                UiRouteId.SettingsGraphics.ToString());

            Slider sharpening = FindRequired(
                    graphics,
                    "SharpeningSlider")
                .GetComponent<Slider>();
            FindRequired(graphics, "AntiAliasingPresetNext")
                .GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(
                sharpening.value,
                Is.EqualTo(Mathf.InverseLerp(
                    GraphicsSettingsDto.MinimumAntiAliasingSharpening,
                    GraphicsSettingsDto.MaximumAntiAliasingSharpening,
                    0.34f)).Within(0.001f));
            FindRequired(graphics, "AntiAliasingPrevious")
                .GetComponent<Button>()
                .onClick.Invoke();
            sharpening.value = Mathf.InverseLerp(
                GraphicsSettingsDto.MinimumAntiAliasingSharpening,
                GraphicsSettingsDto.MaximumAntiAliasingSharpening,
                0.19f);
            FindRequired(graphics, "Apply")
                .GetComponent<Button>()
                .onClick.Invoke();
            yield return null;

            Assert.That(
                fixture.Root.AppliedSettings.Graphics.AntiAliasingMode,
                Is.EqualTo(UiAntiAliasingMode.Smaa));
            Assert.That(
                fixture.Root.AppliedSettings.Graphics.AntiAliasingPreset,
                Is.EqualTo(UiAntiAliasingPreset.Custom));
            Assert.That(
                fixture.Root.AppliedSettings.Graphics.AntiAliasingSharpening,
                Is.EqualTo(0.19f).Within(0.001f));
            Assert.That(
                AntiAliasingController.Instance.SelectedMode,
                Is.EqualTo(AntiAliasingMode.Smaa));
            Assert.That(
                AntiAliasingController.Instance.SelectedPreset,
                Is.EqualTo(AntiAliasingPreset.Custom));

            UiSettingsLoadResult reloaded =
                new UiSettingsJsonStore(settingsPath).LoadOrCreate();
            Assert.That(
                reloaded.Document.Graphics.AntiAliasingMode,
                Is.EqualTo(UiAntiAliasingMode.Smaa));
            Assert.That(
                reloaded.Document.Graphics.AntiAliasingSharpening,
                Is.EqualTo(0.19f).Within(0.001f));

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator BindingConflictDetection_RejectsDuplicateEffectivePathWithinControlGroup()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            InputActionMap player = fixture.PlayerActions.FindActionMap("Player", throwIfNotFound: true);
            InputAction first = player.FindAction("ConflictFirst", throwIfNotFound: true);
            yield return null;

            first.ApplyBindingOverride(0, "<Keyboard>/enter");
            MethodInfo detector = typeof(GameUiRoot).GetMethod(
                "HasBindingConflict",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(detector, Is.Not.Null);
            bool conflict = (bool)detector.Invoke(fixture.Root, new object[] { first, 0 });
            Assert.That(conflict, Is.True);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator UnsupportedSettings_AreTruthfullyDisabledAndRequiredRowsExist()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGraphics);
            yield return null;
            Transform graphics = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
            Transform upscalerQuality =
                FindOptional(graphics, "UpscalerQualityState") ??
                FindOptional(graphics, "UpscalerQualityValue");
            Assert.That(
                upscalerQuality,
                Is.Not.Null,
                "DLSS capability must expose either a truthful unavailable state or the supported quality value.");
            Assert.That(upscalerQuality.GetComponent<Text>().text, Is.Not.Empty);
            Assert.That(
                FindRequired(graphics, "AntiAliasingValue")
                    .GetComponent<Text>().text,
                Is.Not.Empty);
            Assert.That(
                FindRequired(graphics, "AntiAliasingPresetValue")
                    .GetComponent<Text>().text,
                Is.Not.Empty);
            Assert.That(
                FindRequired(graphics, "SharpeningSlider")
                    .GetComponent<Slider>().interactable,
                Is.True);
            Assert.That(FindRequired(graphics, "CameraSettingsPanel"), Is.Not.Null);
            Assert.That(
                FindRequired(graphics, "HorizontalFovSlider")
                    .GetComponent<Slider>().interactable,
                Is.True);
            Assert.That(
                FindRequired(graphics, "CameraFarClipSlider")
                    .GetComponent<Slider>().interactable,
                Is.True);
            Assert.That(FindOptional(graphics, "GraphicsPreview"), Is.Null);
            AssertStandardSettingsActions(graphics);
            Button graphicsReset = FindRequired(graphics, "Reset").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(graphicsReset.gameObject);
            graphicsReset.onClick.Invoke();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            graphics = FindRequired(fixture.Root.transform, UiRouteId.SettingsGraphics.ToString());
            Button graphicsCancel = FindRequired(graphics, "Cancel").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(graphicsCancel.gameObject);
            graphicsCancel.onClick.Invoke();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsAudio);
            yield return null;
            Transform audio = FindRequired(fixture.Root.transform, UiRouteId.SettingsAudio.ToString());
            Assert.That(FindOptional(audio, "AudioTest"), Is.Null);
            Assert.That(FindOptional(audio, "AudioTestAction"), Is.Null);
            Assert.That(FindRequired(audio, "AudioProfileCard"), Is.Not.Null);
            AssertStandardSettingsActions(audio);

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsControls);
            yield return null;
            Transform controls = FindRequired(fixture.Root.transform, UiRouteId.SettingsControls.ToString());
            Assert.That(FindRequired(controls, "ControllerLayoutState").GetComponent<Text>().text, Does.Contain("REBIND").IgnoreCase);
            Assert.That(FindRequired(controls, "InputModeState").GetComponent<Text>().text, Does.Contain("AUTO").IgnoreCase);
            Assert.That(FindOptional(controls, "InputPreview"), Is.Null);
            Assert.That(FindOptional(controls, "DeviceValue"), Is.Null);
            Assert.That(FindOptional(controls, "ResetBindings"), Is.Null);
            AssertStandardSettingsActions(controls);

            fixture.Root.ShowReviewScreen(UiRouteId.SettingsGameplay);
            yield return null;
            Transform gameplay = FindRequired(fixture.Root.transform, UiRouteId.SettingsGameplay.ToString());
            Assert.That(FindRequired(gameplay, "HudModePrevious").GetComponent<Button>().interactable, Is.False);
            Assert.That(FindRequired(gameplay, "HudModeNext").GetComponent<Button>().interactable, Is.False);
            Assert.That(FindRequired(gameplay, "HintsToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindRequired(gameplay, "OutlinesToggle").GetComponent<Toggle>().interactable, Is.False);
            Toggle fpsCounter =
                FindRequired(gameplay, "ShowFpsCounterToggle")
                    .GetComponent<Toggle>();
            Assert.That(fpsCounter.interactable, Is.True);
            Assert.That(fpsCounter.isOn, Is.True);
            Assert.That(FindRequired(gameplay, "DevelopmentUiToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindOptional(gameplay, "ManageProfiles"), Is.Null);
            Assert.That(FindOptional(gameplay, "ProfileCard"), Is.Null);
            Assert.That(FindOptional(gameplay, "ImmersionCard"), Is.Null);
            Assert.That(FindOptional(gameplay, "SummaryCard"), Is.Null);
            AssertStandardSettingsActions(gameplay);
            Assert.That(
                FindRequired(gameplay, "FatigueState").GetComponent<Text>().text,
                Does.Contain("ADAPTER").IgnoreCase);

            Button languageNext = FindRequired(gameplay, "LanguageNext").GetComponent<Button>();
            Assert.That(
                FindRequired(gameplay, "LanguageValue").GetComponent<Text>().text,
                Is.EqualTo("RUSSIAN"));
            languageNext.onClick.Invoke();
            Assert.That(
                FindRequired(gameplay, "LanguageValue").GetComponent<Text>().text,
                Is.EqualTo("ENGLISH"));
            languageNext.onClick.Invoke();
            Assert.That(
                FindRequired(gameplay, "LanguageValue").GetComponent<Text>().text,
                Is.EqualTo("RUSSIAN"));
            fpsCounter.isOn = false;
            FindRequired(gameplay, "Apply").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(fixture.Root.CurrentRoute, Is.EqualTo(UiRouteId.SettingsGameplay));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(fixture.Root.AppliedSettings.Gameplay.LanguageId, Is.EqualTo("ru-RU"));
            Assert.That(
                fixture.Root.AppliedSettings.Gameplay.ShowFpsCounter,
                Is.False);
            gameplay = FindRequired(fixture.Root.transform, UiRouteId.SettingsGameplay.ToString());
            Assert.That(
                FindRequired(gameplay, "LanguageValue").GetComponent<Text>().text,
                Is.EqualTo("РУССКИЙ"));
            FindRequired(gameplay, "Back").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Transform localizedMain = FindRequired(fixture.Root.transform, UiRouteId.MainMenu.ToString());
            Assert.That(
                FindRequired(localizedMain, "NewGame").GetComponentInChildren<Text>().text,
                Is.EqualTo("НОВАЯ ИГРА"));

            fixture.Root.ShowReferencePendingReviewScreen(UiRouteId.SettingsAccessibility);
            yield return null;
            Transform accessibility = FindRequired(
                fixture.Root.transform,
                UiRouteId.SettingsAccessibility.ToString());
            Assert.That(FindRequired(accessibility, "UiScaleSlider").GetComponent<Slider>().interactable, Is.True);
            Assert.That(FindRequired(accessibility, "HighContrastToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindRequired(accessibility, "ReducedMotionToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindRequired(accessibility, "ToggleHoldToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindRequired(accessibility, "ColorCuesToggle").GetComponent<Toggle>().interactable, Is.False);
            Assert.That(FindRequired(accessibility, "AccessSubtitlesToggle").GetComponent<Toggle>().interactable, Is.True);
            Assert.That(FindRequired(accessibility, "AccessCaptionsToggle").GetComponent<Toggle>().interactable, Is.True);
            AssertStandardSettingsActions(accessibility);

            yield return DestroyFixture(fixture);
        }

        [UnityTest]
        public IEnumerator CancelledInteractiveRebind_RestoresPreviousOverride()
        {
            UiFixture fixture = CreateFixture(startInMainMenu: true);
            yield return null;

            InputAction action = fixture.PlayerActions
                .FindActionMap("Player", throwIfNotFound: true)
                .FindAction("ConflictFirst", throwIfNotFound: true);
            action.ApplyBindingOverride(0, "<Keyboard>/enter");
            SetPrivateField(fixture.Root, "activeRebindAction", action);
            SetPrivateField(fixture.Root, "activeRebindBindingIndex", 0);
            SetPrivateField(fixture.Root, "activeRebindPreviousOverridePath", "<Keyboard>/space");
            SetPrivateField(fixture.Root, "suppressRebindRouteRebuild", true);

            MethodInfo finish = typeof(GameUiRoot).GetMethod(
                "FinishInteractiveRebind",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(finish, Is.Not.Null);
            finish.Invoke(fixture.Root, new object[] { false });

            Assert.That(action.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
            yield return DestroyFixture(fixture);
        }

        private UiFixture CreateFixture(
            bool startInMainMenu,
            string settingsPath = null,
            ISaveService saveService = null,
            SaveLoadRequestHandler requestLoad = null,
            Func<string, SaveRequest> createSaveRequest = null,
            string preferredSaveSlotId = "manual-01",
            string initialSaveStatus = null,
            SaveReadStatus? initialSaveReadStatus = null,
            bool gameplayPrepared = true,
            IPlayerMoneyService playerMoney = null,
            Action openDeveloperTools = null,
            NewGameVehiclePaintHandler configureNewGameVehiclePaint = null)
        {
            var gameplay = new GameObject("GameplayRoot");
            GateProbe gate = gameplay.AddComponent<GateProbe>();
            var secondaryGateObject = new GameObject("VehicleSiblingGate");
            secondaryGateObject.transform.SetParent(gameplay.transform, worldPositionStays: false);
            GateProbe secondaryGate = secondaryGateObject.AddComponent<GateProbe>();
            LookSettingsProbe lookSink = gameplay.AddComponent<LookSettingsProbe>();
            InputActionAsset playerActions = CreatePlayerActions();
            InputActionAsset vehicleActions = ScriptableObject.CreateInstance<InputActionAsset>();
            vehicleActions.AddActionMap("Vehicle");

            var rootObject = new GameObject("GameUiRootTest");
            GameUiRoot root = rootObject.AddComponent<GameUiRoot>();
            var sessionGate = new GameplaySessionGateProbe(
                initiallyActive: !startInMainMenu,
                initiallyPrepared: gameplayPrepared);
            root.Initialize(new GameUiDependencies(
                () => true,
                gameTime: null,
                audio: null,
                playerActions,
                vehicleActions,
                gameplay,
                settingsPath ?? Path.Combine(temporaryDirectory, "ui-settings.json"),
                startInMainMenu,
                gameplaySessionGate: sessionGate,
                saveService: saveService,
                requestLoad: requestLoad,
                createSaveRequest: createSaveRequest,
                preferredSaveSlotId: preferredSaveSlotId,
                initialSaveStatus: initialSaveStatus,
                initialSaveReadStatus: initialSaveReadStatus,
                playerMoney: playerMoney,
                openDeveloperTools: openDeveloperTools,
                configureNewGameVehiclePaint:
                    configureNewGameVehiclePaint));
            return new UiFixture(
                root,
                gameplay,
                gate,
                secondaryGate,
                lookSink,
                sessionGate,
                playerActions,
                vehicleActions);
        }

        private static InputActionAsset CreatePlayerActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap player = asset.AddActionMap("Player");
            player.AddAction("ConflictFirst", InputActionType.Button)
                .AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            player.AddAction("ConflictSecond", InputActionType.Button)
                .AddBinding("<Keyboard>/enter", groups: "Keyboard&Mouse");
            InputActionMap system = asset.AddActionMap("System");
            system.AddAction("Pause", InputActionType.Button).AddBinding("<Keyboard>/escape");
            return asset;
        }

        private static bool AcceptLoadRequest(string slotId, out string failure)
        {
            failure = string.Empty;
            return true;
        }

        private static SaveSlotSummary CreateSlot(string slotId, string displayName, string updatedUtc)
        {
            return new SaveSlotSummary(
                slotId,
                new SaveMetadata
                {
                    DisplayName = displayName,
                    GameTimestamp = "SAT 14:37",
                    LocationStableId = "home.garage",
                },
                updatedUtc,
                isValid: true,
                message: string.Empty);
        }

        private static IEnumerator DestroyFixture(UiFixture fixture)
        {
            UnityEngine.Object.Destroy(fixture.Root.gameObject);
            UnityEngine.Object.Destroy(fixture.GameplayRoot);
            UnityEngine.Object.Destroy(fixture.PlayerActions);
            UnityEngine.Object.Destroy(fixture.VehicleActions);
            yield return null;
        }

        private static void InvokePrivate(GameUiRoot root, string methodName)
        {
            MethodInfo method = typeof(GameUiRoot).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing private UI transition: " + methodName);
            method.Invoke(root, parameters: null);
        }

        private static void AssertRectGeometryEqual(
            RectTransform expected,
            RectTransform actual,
            string context)
        {
            Assert.That(actual.anchorMin.x, Is.EqualTo(expected.anchorMin.x).Within(0.01f), context + " anchorMin.x");
            Assert.That(actual.anchorMin.y, Is.EqualTo(expected.anchorMin.y).Within(0.01f), context + " anchorMin.y");
            Assert.That(actual.anchorMax.x, Is.EqualTo(expected.anchorMax.x).Within(0.01f), context + " anchorMax.x");
            Assert.That(actual.anchorMax.y, Is.EqualTo(expected.anchorMax.y).Within(0.01f), context + " anchorMax.y");
            Assert.That(actual.pivot.x, Is.EqualTo(expected.pivot.x).Within(0.01f), context + " pivot.x");
            Assert.That(actual.pivot.y, Is.EqualTo(expected.pivot.y).Within(0.01f), context + " pivot.y");
            Assert.That(actual.anchoredPosition.x, Is.EqualTo(expected.anchoredPosition.x).Within(0.01f), context + " position.x");
            Assert.That(actual.anchoredPosition.y, Is.EqualTo(expected.anchoredPosition.y).Within(0.01f), context + " position.y");
            Assert.That(actual.sizeDelta.x, Is.EqualTo(expected.sizeDelta.x).Within(0.01f), context + " size.x");
            Assert.That(actual.sizeDelta.y, Is.EqualTo(expected.sizeDelta.y).Within(0.01f), context + " size.y");
        }

        private static void AssertActiveGraphicsInsideViewport(
            GameUiRoot root,
            UiRouteId route)
        {
            Canvas.ForceUpdateCanvases();
            Transform routeRoot = FindRequired(root.transform, route.ToString());
            Graphic[] graphics = routeRoot.GetComponentsInChildren<Graphic>(
                includeInactive: false);
            var corners = new Vector3[4];
            foreach (Graphic graphic in graphics)
            {
                RectTransform rect = graphic.rectTransform;
                rect.GetWorldCorners(corners);
                Assert.That(
                    corners[0].x,
                    Is.GreaterThanOrEqualTo(-0.5f),
                    route + "/" + graphic.name + " left");
                Assert.That(
                    corners[0].y,
                    Is.GreaterThanOrEqualTo(-0.5f),
                    route + "/" + graphic.name + " bottom");
                Assert.That(
                    corners[2].x,
                    Is.LessThanOrEqualTo(Screen.width + 0.5f),
                    route + "/" + graphic.name + " right");
                Assert.That(
                    corners[2].y,
                    Is.LessThanOrEqualTo(Screen.height + 0.5f),
                    route + "/" + graphic.name + " top");
            }
        }

        private static void AssertRoundedSurface(Transform value, float minimumRadiusRatio)
        {
            Image image = value.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, value.name + " has no Image surface.");
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), value.name + " is not sliced.");
            Assert.That(image.sprite, Is.Not.Null, value.name + " has no rounded sprite.");
            Assert.That(
                image.sprite.border.x / image.sprite.rect.width,
                Is.GreaterThanOrEqualTo(minimumRadiusRatio),
                value.name + " corner radius is too subtle.");
        }

        private static void AssertCrispBorder(Transform value)
        {
            Assert.That(
                value.GetComponent<Outline>(),
                Is.Null,
                value.name + " still uses a clipped uGUI Outline.");
            Transform border = FindRequired(value, "Border");
            Image borderImage = border.GetComponent<Image>();
            Assert.That(borderImage, Is.Not.Null, value.name + " border has no Image.");
            Assert.That(borderImage.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(borderImage.raycastTarget, Is.False);
        }

        private static void AssertStandardSettingsActions(Transform route)
        {
            Transform group = FindRequired(route, "SettingsActions");
            var groupRect = (RectTransform)group;
            Assert.That(groupRect.anchoredPosition.x, Is.EqualTo(1151f).Within(0.01f));
            Assert.That(groupRect.anchoredPosition.y, Is.EqualTo(-884f).Within(0.01f));
            Assert.That(groupRect.rect.width, Is.EqualTo(455f).Within(0.01f));
            Assert.That(
                groupRect.rect.height,
                Is.EqualTo(UiThemeTokens.UtilityActionButtonHeight).Within(0.01f));
            string[] names = { "Apply", "Reset", "Cancel" };
            string[] labels = { "APPLY", "RESET", "CANCEL" };
            float[] widths =
            {
                UiThemeTokens.UtilityActionFirstWidth,
                UiThemeTokens.UtilityActionSecondWidth,
                UiThemeTokens.UtilityActionThirdWidth,
            };
            float[] xPositions =
            {
                0f,
                UiThemeTokens.UtilityActionFirstWidth + UiThemeTokens.SpacingCompact,
                UiThemeTokens.UtilityActionFirstWidth +
                    UiThemeTokens.UtilityActionSecondWidth +
                    UiThemeTokens.SpacingCompact * 2f,
            };
            AssertActionGaps(group, names);

            for (int index = 0; index < names.Length; index++)
            {
                Transform action = FindRequired(group, names[index]);
                RectTransform actionRect = (RectTransform)action;
                Assert.That(
                    actionRect.rect.width,
                    Is.EqualTo(widths[index]).Within(0.01f),
                    route.name + "/" + names[index] + " width");
                Assert.That(
                    actionRect.rect.height,
                    Is.EqualTo(UiThemeTokens.UtilityActionButtonHeight).Within(0.01f),
                    route.name + "/" + names[index] + " height");
                Assert.That(
                    actionRect.anchoredPosition.x,
                    Is.EqualTo(xPositions[index]).Within(0.01f),
                    route.name + "/" + names[index] + " position");
                Assert.That(
                    action.GetComponentInChildren<Text>().text,
                    Is.EqualTo(labels[index]),
                    route.name + "/" + names[index] + " label");
                AssertRoundedSurface(action, 0.33f);
                AssertCrispBorder(action);
            }

        }

        private static void AssertActionGaps(Transform group, params string[] orderedButtons)
        {
            for (int index = 1; index < orderedButtons.Length; index++)
            {
                Transform previous = FindRequired(group, orderedButtons[index - 1]);
                Transform current = FindRequired(group, orderedButtons[index]);
                Assert.That(
                    HorizontalGap(previous, current),
                    Is.GreaterThanOrEqualTo(UiThemeTokens.SpacingSmall - 0.01f),
                    group.name + ": " + orderedButtons[index - 1] + " -> " + orderedButtons[index]);
            }
        }

        private static float HorizontalGap(Transform left, Transform right)
        {
            var leftRect = (RectTransform)left;
            var rightRect = (RectTransform)right;
            return rightRect.anchoredPosition.x -
                   (leftRect.anchoredPosition.x + leftRect.rect.width);
        }

        private static float VerticalGap(Transform upper, Transform lower)
        {
            var upperRect = (RectTransform)upper;
            var lowerRect = (RectTransform)lower;
            float upperTop = -upperRect.anchoredPosition.y;
            float lowerTop = -lowerRect.anchoredPosition.y;
            return lowerTop - (upperTop + upperRect.rect.height);
        }

        private static void SetPrivateField(GameUiRoot root, string fieldName, object value)
        {
            FieldInfo field = typeof(GameUiRoot).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private UI field: " + fieldName);
            field.SetValue(root, value);
        }

        private static Transform FindRequired(Transform root, string name)
        {
            Transform[] values = root.GetComponentsInChildren<Transform>(includeInactive: true);
            Transform match = values.FirstOrDefault(value => value.name == name);
            Assert.That(match, Is.Not.Null, "Missing UI object: " + name);
            return match;
        }

        private static Transform FindOptional(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(value => value.name == name);
        }

        private readonly struct UiFixture
        {
            public UiFixture(
                GameUiRoot root,
                GameObject gameplayRoot,
                GateProbe gate,
                GateProbe secondaryGate,
                LookSettingsProbe lookSink,
                GameplaySessionGateProbe sessionGate,
                InputActionAsset playerActions,
                InputActionAsset vehicleActions)
            {
                Root = root;
                GameplayRoot = gameplayRoot;
                Gate = gate;
                SecondaryGate = secondaryGate;
                LookSink = lookSink;
                SessionGate = sessionGate;
                PlayerActions = playerActions;
                VehicleActions = vehicleActions;
            }

            public GameUiRoot Root { get; }
            public GameObject GameplayRoot { get; }
            public GateProbe Gate { get; }
            public GateProbe SecondaryGate { get; }
            public LookSettingsProbe LookSink { get; }
            public GameplaySessionGateProbe SessionGate { get; }
            public InputActionAsset PlayerActions { get; }
            public InputActionAsset VehicleActions { get; }
        }

        private sealed class GateProbe : MonoBehaviour, IGameplayInputGate, IUiVisibilityGate
        {
            public bool GameplayInputEnabled { get; private set; } = true;
            public bool UiSuppressed { get; private set; }
            public bool IsGameplayInputEnabled => GameplayInputEnabled;
            public bool IsUiSuppressed => UiSuppressed;
            public void SetGameplayInputEnabled(bool enabled) => GameplayInputEnabled = enabled;
            public void SetUiSuppressed(bool suppressed) => UiSuppressed = suppressed;
        }

        private sealed class DeferredInputGateProbe :
            MonoBehaviour,
            IGameplayInputGate
        {
            public bool IsGameplayInputEnabled =>
                enabled && gameObject.activeInHierarchy;

            public void SetGameplayInputEnabled(bool inputEnabled)
            {
                enabled = inputEnabled;
            }
        }

        private sealed class LookSettingsProbe :
            MonoBehaviour,
            IPlayerLookSettingsSink,
            IPlayerCameraSettingsSink,
            IGameplayLocaleSettingsSink
        {
            public int ApplyCount { get; private set; }
            public float MouseSensitivity { get; private set; }
            public int CameraApplyCount { get; private set; }
            public float HorizontalFieldOfViewDegrees { get; private set; }
            public float CameraFarClipMeters { get; private set; }
            public int LocaleApplyCount { get; private set; }
            public string LocaleId { get; private set; } = string.Empty;

            public void ApplyLookSettings(
                float mouseSensitivityMultiplier,
                bool invertMouseY,
                float gamepadSensitivityMultiplier,
                bool invertGamepadY)
            {
                ApplyCount++;
                MouseSensitivity = mouseSensitivityMultiplier;
            }

            public void ApplyCameraSettings(
                float horizontalFieldOfViewDegrees,
                float farClipPlaneMeters)
            {
                CameraApplyCount++;
                HorizontalFieldOfViewDegrees = horizontalFieldOfViewDegrees;
                CameraFarClipMeters = farClipPlaneMeters;
            }

            public void ApplyGameplayLocale(string localeId)
            {
                LocaleApplyCount++;
                LocaleId = localeId ?? string.Empty;
            }
        }

        private sealed class GameplaySessionGateProbe :
            IGameplaySessionPreparationGate
        {
            public GameplaySessionGateProbe(
                bool initiallyActive,
                bool initiallyPrepared)
            {
                IsGameplayActive = initiallyActive;
                IsGameplayPrepared = initiallyPrepared;
            }

            public bool IsGameplayPrepared { get; private set; }

            public bool IsGameplayActive { get; private set; }

            public bool IsGameplayPreparationRunning { get; private set; }

            public string LastGameplayPreparationFailure { get; private set; } =
                string.Empty;

            public int PreparationAttemptCount { get; private set; }

            public int ActivationAttemptCount { get; private set; }

            public bool TryBeginGameplayPreparation(out string failure)
            {
                PreparationAttemptCount++;
                IsGameplayPreparationRunning = !IsGameplayPrepared;
                LastGameplayPreparationFailure = string.Empty;
                failure = string.Empty;
                return true;
            }

            public void CompletePreparation()
            {
                IsGameplayPrepared = true;
                IsGameplayPreparationRunning = false;
            }

            public bool TryActivateGameplay(out string failure)
            {
                ActivationAttemptCount++;
                IsGameplayActive = true;
                failure = string.Empty;
                return true;
            }
        }

        private sealed class SaveServiceProbe : ISaveService
        {
            private readonly List<SaveSlotSummary> slots;

            public SaveServiceProbe(params SaveSlotSummary[] initialSlots)
            {
                slots = new List<SaveSlotSummary>(initialSlots ?? Array.Empty<SaveSlotSummary>());
            }

            public bool IsOperationInProgress { get; private set; }

            public int LoadCallCount { get; private set; }

            public SaveRequest LastSaveRequest { get; private set; }

            public event EventHandler<SaveOperationEventArgs> OperationStarted;
            public event EventHandler<SaveOperationEventArgs> OperationCompleted;
            public event EventHandler<SaveOperationEventArgs> OperationFailed;

            public event EventHandler<SaveOperationEventArgs> RecoveryPerformed
            {
                add { }
                remove { }
            }

            public SaveWriteResult Save(SaveRequest request)
            {
                IsOperationInProgress = true;
                OperationStarted?.Invoke(
                    this,
                    new SaveOperationEventArgs(SaveOperationKind.Save, request.SlotId, "Saving."));
                try
                {
                    LastSaveRequest = request;
                    string updatedUtc = "2026-07-21T09:00:00.0000000Z";
                    slots.RemoveAll(slot => string.Equals(
                        slot.SlotId,
                        request.SlotId,
                        StringComparison.Ordinal));
                    slots.Add(new SaveSlotSummary(
                        request.SlotId,
                        request.Metadata,
                        updatedUtc,
                        isValid: true,
                        message: string.Empty));
                    var document = new SaveDocument
                    {
                        Header = new SaveHeader
                        {
                            SlotId = request.SlotId,
                            UpdatedUtc = updatedUtc,
                        },
                        Metadata = request.Metadata.DeepClone(),
                    };
                    SaveWriteResult result = new SaveWriteResult(request.SlotId, "probe", document);
                    OperationCompleted?.Invoke(
                        this,
                        new SaveOperationEventArgs(SaveOperationKind.Save, request.SlotId, "Saved."));
                    return result;
                }
                catch (Exception exception)
                {
                    OperationFailed?.Invoke(
                        this,
                        new SaveOperationEventArgs(
                            SaveOperationKind.Save,
                            request?.SlotId,
                            exception.Message,
                            exception));
                    throw;
                }
                finally
                {
                    IsOperationInProgress = false;
                }
            }

            public SaveLoadResult Load(string slotId)
            {
                LoadCallCount++;
                throw new InvalidOperationException("UI must request a clean-session load instead.");
            }

            public IReadOnlyList<SaveSlotSummary> EnumerateSlots()
            {
                return slots.ToArray();
            }
        }

        private sealed class PlayerMoneyServiceProbe : IPlayerMoneyService
        {
            private ulong revision;
            private long balanceMinorUnits;

            public PlayerMoneyServiceProbe(long initialBalanceMinorUnits)
            {
                balanceMinorUnits = initialBalanceMinorUnits;
            }

            public EconomySnapshot Snapshot => new EconomySnapshot(
                revision,
                balanceMinorUnits,
                0);

            public event Action<EconomySnapshot> StateChanged;

            public void SetBalance(long configuredBalanceMinorUnits)
            {
                balanceMinorUnits = configuredBalanceMinorUnits;
                revision++;
                StateChanged?.Invoke(Snapshot);
            }
        }
    }
}
