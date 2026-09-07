using System;
using System.Collections.Generic;
using System.Globalization;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Needs;
using MSC.UI.Runtime.Localization;
using MSC.UI.Runtime.Routing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private const int ColoursPerPage = 10;
        private Button[] colourSwatchButtons;
        private Button[] colourPageButtons;
        private Button[] primaryMenuButtons;
        private Button[] utilityMenuButtons;
        private int colourPageIndex;
        private static readonly Color[] CarColours =
        {
            new Color32(50, 58, 38, 255), new Color32(234, 217, 134, 255),
            new Color32(246, 246, 246, 255), new Color32(202, 9, 0, 255),
            new Color32(202, 201, 195, 255), new Color32(118, 75, 50, 255),
            new Color32(3, 38, 69, 255), new Color32(206, 196, 61, 255),
            new Color32(158, 173, 177, 255), new Color32(114, 104, 23, 255),
            new Color32(195, 132, 35, 255), new Color32(41, 165, 195, 255),
        };
        private int selectedCarColourIndex;

        private Color CurrentMenuGlassTint => UiThemeTokens.MenuGlassTint(
            CarColours[Mathf.Clamp(selectedCarColourIndex, 0, CarColours.Length - 1)]);

        partial void BuildMainMenuRoute()
        {
            GameObject route = CreateRoute(UiRouteId.MainMenu);
            GameObject workspace = factory.CreateObject("MainMenuWorkspace", route.transform);
            var workspaceRect = workspace.GetComponent<RectTransform>();
            workspaceRect.anchorMin = workspaceRect.anchorMax = new Vector2(0.5f, 0.5f);
            workspaceRect.pivot = new Vector2(0.5f, 0.5f);
            workspaceRect.sizeDelta = new Vector2(UiThemeTokens.ReferenceWidth, UiThemeTokens.ReferenceHeight);
            workspace.AddComponent<MainMenuWorkspace>().Initialize(
                settings.Applied.Accessibility.ReducedMotion || reviewDataEnabled);
            Transform parent = workspace.transform;
            CreateLogo(parent, 40f, 24f, 414f, 276f, large: true,
                sourceUv: new Rect(0f, 0f, 1f, 1f));
            GameObject greeting = factory.MainMenuGreeting("Greeting", parent, textCatalog.Get("ui.greeting"),
                1350f, 56f, 238f, 62f);
            AnchorMainMenuBlock(greeting.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(-84f, -56f));

            const float width = 300f;
            const float height = 72f;
            const float spacing = 10f;
            GameObject actions = factory.CreateObject("PrimaryActions", parent);
            RectTransform actionRect = factory.Place(actions, 1288f, 196f, width, height * 5f + spacing * 4f);
            AnchorMainMenuBlock(actionRect, new Vector2(1f, 1f), new Vector2(-84f, -196f));
            var layout = actions.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            mainContinueButton = factory.MainMenuButton(
                "Continue",
                actions.transform,
                textCatalog.Get("ui.main.continue"),
                UiIconKind.Play,
                0f, 0f, width, height,
                BeginContinueLoad,
                interactable: CanRequestLatestLoad,
                helper: MainSaveHelperText());
            Button newGame = factory.MainMenuButton(
                "NewGame",
                actions.transform,
                textCatalog.Get("ui.main.new_game"),
                UiIconKind.Plus,
                0f, 0f, width, height,
                BeginBoundedNewGameSession);
            mainLoadButton = factory.MainMenuButton(
                "LoadGame",
                actions.transform,
                textCatalog.Get("ui.main.load_game"),
                UiIconKind.Folder,
                0f, 0f, width, height,
                OpenLoadGame,
                interactable: CanOpenLoadGame,
                helper: MainSaveHelperText());
            Button credits = factory.MainMenuButton(
                "Credits",
                actions.transform,
                textCatalog.Get("ui.main.credits"),
                UiIconKind.Credits,
                0f, 0f, width, height,
                () => ShowCredits(route.transform));
            Button quit = factory.MainMenuButton(
                "Quit",
                actions.transform,
                textCatalog.Get("ui.main.quit"),
                UiIconKind.Cross,
                0f, 0f, width, height,
                () => OpenQuitConfirmation(UiRouteId.MainMenu),
                destructive: true);
            primaryMenuButtons = new[] { mainContinueButton, newGame, mainLoadButton, credits, quit };
            for (int index = 0; index < primaryMenuButtons.Length; index++)
            {
                var element = primaryMenuButtons[index].gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = width;
                element.preferredHeight = height;
                element.minHeight = height;
            }

            BuildColourCard(parent);
            BuildMainUtilityStrip(parent);
            BuildMenuOrbitHint(parent);
            ConfigureMainMenuNavigation(primaryMenuButtons, utilityMenuButtons, colourSwatchButtons, colourPageButtons);
            bool reducedMotion = settings.Applied.Accessibility.ReducedMotion || reviewDataEnabled;
            ConfigureMainMenuMotion(primaryMenuButtons, reducedMotion);
            ConfigureMainMenuMotion(utilityMenuButtons, reducedMotion);
            ConfigureMainMenuMotion(colourSwatchButtons, reducedMotion);
            ConfigureMainMenuMotion(colourPageButtons, reducedMotion);
            factory.Text(
                "Version",
                parent,
                "MSC REMAKE " + Application.version,
                82f,
                910f,
                350f,
                18f,
                12,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft);
        }

        private static void AnchorMainMenuBlock(RectTransform rect, Vector2 anchor, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
        }

        private static void ConfigureMainMenuMotion(Button[] buttons, bool reducedMotion)
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index] is MainMenuActionButton action)
                {
                    action.ReducedMotion = reducedMotion;
                }
            }
        }

        partial void BuildPauseRoute()
        {
            GameObject route = CreateRoute(UiRouteId.Pause);
            GameObject panel = factory.MainMenuPanel("PausePanel", route.transform, 636f, 150f, 400f, 640f);
            Text heading = factory.Heading(
                panel.transform,
                textCatalog.Get("ui.pause.title"),
                34f,
                30f,
                332f,
                31);
            heading.color = MainMenuStyle.PrimaryText;
            heading.fontStyle = FontStyle.Normal;
            Button resume = factory.MainMenuButton(
                "Resume",
                panel.transform,
                textCatalog.Get("ui.pause.resume"),
                UiIconKind.Play,
                34f,
                104f,
                332f,
                66f,
                EnterGameplay);
            Button pauseSettings = factory.MainMenuButton(
                "PauseSettings",
                panel.transform,
                textCatalog.Get("ui.main.settings"),
                UiIconKind.Gear,
                34f,
                184f,
                332f,
                66f,
                () => OpenSettings(UiRouteId.SettingsGraphics, UiRouteId.Pause));
            Button saveStatus = factory.MainMenuButton(
                "PauseSaveStatus",
                panel.transform,
                textCatalog.Get("ui.pause.save_status"),
                UiIconKind.Folder,
                34f,
                264f,
                332f,
                66f,
                () => OpenSaveStatus(UiRouteId.Pause));
            Button returnMenu = factory.MainMenuButton(
                "ReturnMenu",
                panel.transform,
                textCatalog.Get("ui.pause.main_menu"),
                UiIconKind.Back,
                34f,
                344f,
                332f,
                66f,
                ReturnToFreshMainMenu);
            Button quit = factory.MainMenuButton(
                "PauseQuit",
                panel.transform,
                textCatalog.Get("ui.main.quit"),
                UiIconKind.Cross,
                34f,
                424f,
                332f,
                66f,
                () => OpenQuitConfirmation(UiRouteId.Pause),
                destructive: true);
            ConfigureMainMenuMotion(new[] { resume, pauseSettings, saveStatus, returnMenu, quit },
                settings.Applied.Accessibility.ReducedMotion || reviewDataEnabled);
        }

        partial void BuildHudRoute()
        {
            GameObject route = CreateRoute(UiRouteId.InGameHud);
            GameObject clockPanel = factory.CreateObject(
                "Clock",
                route.transform);
            factory.Place(clockPanel, 1450f, 38f, 180f, 62f);
            hudDayText = factory.Text(
                "Day",
                clockPanel.transform,
                string.Empty,
                0f,
                30f,
                0f,
                20f,
                15,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            hudDayText.gameObject.SetActive(false);
            hudClockText = factory.Text(
                "Clock",
                clockPanel.transform,
                string.Empty,
                0f,
                0f,
                180f,
                30f,
                25,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleRight);
            hudDateText = factory.Text(
                "Date",
                clockPanel.transform,
                string.Empty,
                0f,
                30f,
                180f,
                20f,
                15,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleRight);
            AddHudShadow(hudDayText);
            AddHudShadow(hudClockText);
            AddHudShadow(hudDateText);
            factory.Divider(
                clockPanel.transform,
                156f,
                58f,
                24f,
                UiThemeTokens.Accent);

            GameObject moneyPanel = factory.CreateObject(
                "Money",
                route.transform);
            factory.Place(moneyPanel, 1450f, 118f, 180f, 34f);
            hudMoneyText = factory.Text(
                "MoneyValue",
                moneyPanel.transform,
                "—",
                0f,
                0f,
                142f,
                30f,
                17,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleRight);
            Text moneyCurrency = factory.Text(
                "MoneyCurrency",
                moneyPanel.transform,
                "MK",
                148f,
                0f,
                32f,
                30f,
                17,
                UiThemeTokens.Accent,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            AddHudShadow(moneyCurrency);
            AddHudShadow(hudMoneyText);

            GameObject needsPanel = factory.CreateObject(
                "Needs",
                route.transform);
            factory.Place(needsPanel, 42f, 38f, 152f, 310f);
            needHudBindings.Clear();
            AddNeedRow(needsPanel.transform, 0, "ui.hud.thirst", UiIconKind.Thirst, 0.18f);
            AddNeedRow(needsPanel.transform, 1, "ui.hud.hunger", UiIconKind.Hunger, 0.36f);
            AddNeedRow(needsPanel.transform, 2, "ui.hud.stress", UiIconKind.Stress, 0.22f);
            AddNeedRow(needsPanel.transform, 3, "ui.hud.urine", UiIconKind.Urine, 0.28f);
            AddNeedRow(needsPanel.transform, 4, "ui.hud.fatigue", UiIconKind.Fatigue, 0.44f);
            AddNeedRow(needsPanel.transform, 5, "ui.hud.dirtiness", UiIconKind.Dirtiness, 0.71f);
            hudFpsPanel = factory.CreateObject(
                "FpsCounter",
                route.transform);
            factory.Place(hudFpsPanel, 1490f, 842f, 140f, 42f);
            hudFpsText = factory.Text(
                "FpsValue",
                hudFpsPanel.transform,
                "—",
                0f,
                0f,
                96f,
                30f,
                17,
                Color.white,
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            Text fpsLabel = factory.Text(
                "FpsLabel",
                hudFpsPanel.transform,
                "FPS",
                102f,
                0f,
                38f,
                30f,
                17,
                UiThemeTokens.Accent,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            AddHudShadow(fpsLabel);
            AddHudShadow(hudFpsText);
            hudFpsPanel.SetActive(
                settings.Applied.Gameplay.ShowFpsCounter);
            nextHudFpsRefreshTime = 0f;
            RefreshHudFpsCounter(force: true);
            RefreshHud(force: true);
        }

        partial void RefreshHud(bool force)
        {
            if (hudClockText == null)
            {
                return;
            }

            if (reviewDataEnabled)
            {
                hudDayText.text = string.Empty;
                hudClockText.text = "19:47";
                hudDateText.text = "FRI, 27 JUN";
                hudMoneyText.text = "12,345";
                for (int index = 0; index < needHudBindings.Count; index++)
                {
                    NeedHudBinding binding = needHudBindings[index];
                    binding.SetNormalized(binding.ReviewValue);
                }

                return;
            }

            if (dependencies.GameTime != null)
            {
                GameTimeSnapshot snapshot = dependencies.GameTime.Snapshot;
                if (force || snapshot.Revision != lastClockRevision)
                {
                    lastClockRevision = snapshot.Revision;
                    double seconds = snapshot.SecondsOfDay;
                    int hour = Mathf.FloorToInt((float)(seconds / 3600d)) % 24;
                    int minute = Mathf.FloorToInt((float)(seconds / 60d)) % 60;
                    UiLocaleFormatter formatter = GetLocaleFormatter();
                    hudClockText.text = formatter.Format("{0:00}:{1:00}", hour, minute);
                    var date = new DateTime(
                        snapshot.Date.Year,
                        snapshot.Date.Month,
                        snapshot.Date.Day);
                    hudDayText.text = string.Empty;
                    hudDateText.text = formatter.Culture.TextInfo.ToUpper(
                        formatter.Format("{0:ddd}, {0:dd MMM}", date));
                }
            }
            else
            {
                hudDayText.text = string.Empty;
                hudClockText.text = "--:--";
                hudDateText.text = "---, -- ---";
            }

            if (dependencies.PlayerMoney != null)
            {
                EconomySnapshot money = dependencies.PlayerMoney.Snapshot;
                if (force || money.Revision != lastMoneyRevision)
                {
                    lastMoneyRevision = money.Revision;
                    hudMoneyText.text = money.BalanceMarkka.ToString(
                        "N0",
                        CultureInfo.InvariantCulture);
                }
            }
            else
            {
                hudMoneyText.text = "—";
            }
            if (dependencies.PlayerNeeds != null)
            {
                PlayerNeedsSnapshot needs = dependencies.PlayerNeeds.Snapshot;
                if (force || needs.Revision != lastNeedsRevision)
                {
                    lastNeedsRevision = needs.Revision;
                    for (int index = 0;
                         index < needHudBindings.Count;
                         index++)
                    {
                        float normalized = needs.GetNormalized(index);
                        NeedHudBinding binding = needHudBindings[index];
                        binding.SetNormalized(normalized);
                    }
                }
            }
            else
            {
                for (int index = 0;
                     index < needHudBindings.Count;
                     index++)
                {
                    NeedHudBinding binding = needHudBindings[index];
                    binding.SetNormalized(0f);
                }
            }
        }

        private void CreateLogo(
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            bool large,
            Rect? sourceUv = null)
        {
            GameObject logo = factory.CreateObject("ProjectOwnedLogo", parent);
            factory.Place(logo, x, y, width, height);
            Texture2D logoTexture = dependencies.MenuLogoTexture;
            if (logoTexture != null && logoTexture.width > 0 && logoTexture.height > 0)
            {
                // The supplied project logo deliberately keeps transparent
                // source padding. Present only its padded alpha bounds so the
                // artwork, rather than the empty canvas, owns the layout box.
                // Historical settings keep their accepted crop; the current
                // main-menu logo presents the full newer artwork explicitly.
                var artworkUv = sourceUv ?? new Rect(
                    52f / 600f,
                    76f / 337f,
                    524f / 600f,
                    221f / 337f);
                float aspect =
                    (logoTexture.width * artworkUv.width) /
                    (logoTexture.height * artworkUv.height);
                float artworkWidth = width;
                float artworkHeight = artworkWidth / aspect;
                if (artworkHeight > height)
                {
                    artworkHeight = height;
                    artworkWidth = artworkHeight * aspect;
                }

                GameObject artwork = factory.CreateObject("Artwork", logo.transform);
                factory.Place(
                    artwork,
                    (width - artworkWidth) * 0.5f,
                    (height - artworkHeight) * 0.5f,
                    artworkWidth,
                    artworkHeight);
                RawImage image = artwork.AddComponent<RawImage>();
                image.texture = logoTexture;
                image.uvRect = artworkUv;
                image.color = Color.white;
                image.raycastTarget = false;
                return;
            }

            GameObject orange = factory.CreateObject("SunDisc", logo.transform);
            factory.Place(orange, width * 0.10f, height * 0.05f, width * 0.55f, height * 0.73f);
            Image orangeImage = factory.AddSurface(orange, new Color32(244, 105, 34, 245));
            orangeImage.sprite = visualAssets.CircleSprite;
            GameObject green = factory.CreateObject("GreenDisc", logo.transform);
            factory.Place(green, width * 0.24f, height * 0.28f, width * 0.52f, height * 0.62f);
            Image greenImage = factory.AddSurface(green, new Color32(122, 221, 43, 245));
            greenImage.sprite = visualAssets.CircleSprite;
            factory.Text(
                "LogoMySummer",
                logo.transform,
                "MY SUMMER",
                0f,
                height * 0.10f,
                width,
                height * 0.30f,
                large ? 40 : 31,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            factory.Text(
                "LogoCar",
                logo.transform,
                "CAR",
                0f,
                height * 0.34f,
                width,
                height * 0.37f,
                large ? 70 : 52,
                new Color32(161, 244, 48, 255),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            factory.Text(
                "LogoRemake",
                logo.transform,
                "REMAKE",
                width * 0.42f,
                height * 0.70f,
                width * 0.48f,
                height * 0.20f,
                large ? 25 : 19,
                UiThemeTokens.Accent,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
        }

        private void CreateGreeting(
            Transform parent,
            float x,
            float y,
            float width,
            float height)
        {
            GameObject greeting = factory.GlassPanel(
                "Greeting",
                parent,
                x,
                y,
                width,
                height,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            GameObject smile = factory.CreateObject("Smile", greeting.transform);
            factory.Place(smile, 14f, 10f, 48f, 48f);
            Image disc = factory.AddSurface(smile, new Color32(246, 236, 0, 255));
            disc.sprite = visualAssets.CircleSprite;
            factory.Text(
                "SmileGlyph",
                smile.transform,
                ":)",
                0f,
                0f,
                48f,
                44f,
                20,
                Color.black,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            factory.Text(
                "GreetingText",
                greeting.transform,
                textCatalog.Get("ui.greeting"),
                72f,
                0f,
                width - 84f,
                height,
                14,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Italic);
        }

        private void BuildColourCard(Transform parent)
        {
            GameObject card = factory.MainMenuPanel(
                "CarColourCard", parent, 80f, 690f, 350f, 210f);
            AnchorMainMenuBlock(card.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(80f, 41f));
            factory.Text("Title", card.transform, textCatalog.Get("ui.main.car_color"),
                22f, 14f, 306f, 24f, 14, MainMenuStyle.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Normal);
            colourSwatchButtons = new Button[CarColours.Length];
            int pageCount = (CarColours.Length + ColoursPerPage - 1) / ColoursPerPage;
            colourPageButtons = new Button[pageCount > 1 ? pageCount : 0];
            colourPageIndex = selectedCarColourIndex / ColoursPerPage;
            for (int index = 0; index < CarColours.Length; index++)
            {
                int slot = index % ColoursPerPage;
                int colourIndex = index;
                MainMenuActionButton button = factory.MainMenuSwatch(
                    "Colour" + index, card.transform, CarColours[index],
                    22f + slot % 5 * 64f, 50f + slot / 5 * 62f, 50f,
                    selectedCarColourIndex == index, () => SelectCarColour(colourIndex));
                colourSwatchButtons[index] = button;
                button.gameObject.SetActive(index / ColoursPerPage == colourPageIndex);
            }

            for (int index = 0; index < colourPageButtons.Length; index++)
            {
                int page = index;
                colourPageButtons[index] = factory.MainMenuPageDot(
                    "ColourPage" + index, card.transform,
                    (350f - pageCount * 28f) * 0.5f + index * 28f, 174f,
                    index == colourPageIndex, () => ShowColourPage(page));
            }
        }

        private void SelectCarColour(int index)
        {
            selectedCarColourIndex = Mathf.Clamp(index, 0, CarColours.Length - 1);
            for (int swatchIndex = 0; swatchIndex < colourSwatchButtons.Length; swatchIndex++)
            {
                if (colourSwatchButtons[swatchIndex] is MainMenuActionButton swatch)
                {
                    swatch.SetPersistentSelection(swatchIndex == selectedCarColourIndex);
                }
            }

            PersistMainMenuColourSelection(selectedCarColourIndex);
            menuVehiclePreview?.SetPaint(CarColours[selectedCarColourIndex]);
            RefreshMainMenuNavigation();
            RefreshGlassSurfaces();
        }

        private void ShowColourPage(int page)
        {
            int pageCount = (CarColours.Length + ColoursPerPage - 1) / ColoursPerPage;
            colourPageIndex = Mathf.Clamp(page, 0, pageCount - 1);
            for (int index = 0; index < colourSwatchButtons.Length; index++)
            {
                colourSwatchButtons[index].gameObject.SetActive(index / ColoursPerPage == colourPageIndex);
            }

            for (int index = 0; index < colourPageButtons.Length; index++)
            {
                ((MainMenuActionButton)colourPageButtons[index]).SetPersistentSelection(index == colourPageIndex);
            }

            ConfigureMainMenuNavigation(primaryMenuButtons, utilityMenuButtons, colourSwatchButtons, colourPageButtons);
        }

        private void BuildMainUtilityStrip(Transform parent)
        {
            GameObject strip = factory.CreateObject("UtilityStrip", parent);
            const float height = 54f;
            bool dev = capabilities.Get(MSC.UI.Runtime.Capabilities.UiCapabilityId.DeveloperTools).IsInteractive &&
                dependencies.OpenDeveloperTools != null;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            dev = false;
#endif
            float width = dev ? 568f : 352f;
            RectTransform rect = factory.Place(strip, 1588f - width, 838f, width, height);
            AnchorMainMenuBlock(rect, new Vector2(1f, 0f), new Vector2(-84f, 41f));
            var layout = strip.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            utilityMenuButtons = new Button[dev ? 3 : 2];
            utilityMenuButtons[0] = factory.MainMenuButton(
                "Settings", strip.transform, textCatalog.Get("ui.main.settings"), UiIconKind.Gear,
                0f, 0f, 180f, height,
                () => OpenSettings(UiRouteId.SettingsGraphics, UiRouteId.MainMenu), compact: true);
            utilityMenuButtons[1] = factory.MainMenuButton(
                "Mods", strip.transform, textCatalog.Get("ui.main.mods"), UiIconKind.Puzzle,
                0f, 0f, 160f, height,
                () => OpenSettings(UiRouteId.SettingsMods, UiRouteId.MainMenu), compact: true);
            if (dev)
            {
                utilityMenuButtons[2] = factory.MainMenuButton(
                    "DevTools", strip.transform, textCatalog.Get("ui.main.dev_tools"), UiIconKind.Braces,
                    0f, 0f, 204f, height, () => dependencies.OpenDeveloperTools(), compact: true);
            }

            for (int index = 0; index < utilityMenuButtons.Length; index++)
            {
                var element = utilityMenuButtons[index].gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = index == 0 ? 180f : index == 1 ? 160f : 204f;
                element.preferredHeight = height;
            }
        }
        private void ShowCredits(Transform parent)
        {
            if (mainMenuModal != null)
            {
                return;
            }

            GameObject returnSelection = EventSystem.current?.currentSelectedGameObject;
            RememberMainMenuSelection(returnSelection);
            GameObject overlay = factory.CreateObject("CreditsOverlay", parent);
            factory.Stretch(overlay);
            factory.AddSurface(overlay, new Color(0f, 0f, 0f, 0.62f), rounded: false);
            GameObject panel = factory.Panel("CreditsPanel", overlay.transform, 536f, 214f, 600f, 500f);
            factory.Heading(panel.transform, textCatalog.Get("ui.main.credits"), 34f, 24f, 532f, 28);
            factory.Text(
                "CreditsText",
                panel.transform,
                textCatalog.Get("ui.credits.body"),
                34f,
                90f,
                532f,
                312f,
                16,
                UiThemeTokens.TextPrimary,
                TextAnchor.UpperCenter);
            Button back = factory.CompactButton(
                "CreditsBack",
                panel.transform,
                textCatalog.Get("ui.common.back"),
                174f,
                424f,
                252f,
                48f,
                () => CloseCredits(overlay, returnSelection),
                primary: true);
            mainMenuModal = overlay;
            RefreshMenuOrbitInteraction();
            mainMenuModalBack = back;
            SetMenuNavigation(back, back, back, back, back);
            UiFactory.SelectFirst(overlay);
        }

        private void CloseCredits(GameObject overlay, GameObject returnSelection)
        {
            mainMenuModal = null;
            RefreshMenuOrbitInteraction();
            mainMenuModalBack = null;
            overlay.SetActive(false);
            Destroy(overlay);
            if (EventSystem.current == null)
            {
                return;
            }

            Selectable selectable = returnSelection == null
                ? null
                : returnSelection.GetComponent<Selectable>();
            if (selectable != null &&
                returnSelection.activeInHierarchy &&
                selectable.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(returnSelection);
                return;
            }

            if (routes.TryGetValue(UiRouteId.MainMenu, out GameObject mainMenu))
            {
                UiFactory.SelectFirst(mainMenu);
            }
        }

        private void AddNeedRow(
            Transform parent,
            int index,
            string labelKey,
            UiIconKind icon,
            float reviewValue)
        {
            const float trackX = 40f;
            const float trackWidth = 112f;
            const float groupWidth = trackX + trackWidth;
            const float groupStride = 52f;
            GameObject group = factory.CreateObject(
                "Need" + index,
                parent);
            factory.Place(
                group,
                0f,
                index * groupStride,
                groupWidth,
                44f);

            Image iconImage = factory.Icon(
                "NeedIcon" + index,
                group.transform,
                icon,
                0f,
                4f,
                28f,
                Color.white);
            AddHudShadow(iconImage);
            Text label = factory.Text(
                "NeedLabel" + index,
                group.transform,
                textCatalog.Get(labelKey),
                trackX,
                0f,
                trackWidth,
                20f,
                14,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            AddHudShadow(label);

            GameObject track = factory.CreateObject(
                "NeedTrack" + index,
                group.transform);
            factory.Place(track, trackX, 31f, trackWidth, 3f);
            Image trackImage = factory.AddSurface(
                track,
                UiThemeTokens.HudMinimalTrack,
                rounded: false);
            AddHudShadow(trackImage);
            GameObject fillObject = factory.CreateObject("NeedFill" + index, track.transform);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            Image fillImage = factory.AddSurface(
                fillObject,
                Color.white,
                rounded: false);
            fillImage.sprite = visualAssets.NeedGradientSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            AddHudShadow(fillImage);

            GameObject indicatorObject = factory.CreateObject(
                "NeedIndicator" + index,
                track.transform);
            RectTransform indicatorRect =
                indicatorObject.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.anchoredPosition = Vector2.zero;
            indicatorRect.sizeDelta = new Vector2(7f, 7f);
            Image indicatorImage = factory.AddSurface(
                indicatorObject,
                Color.white);
            indicatorImage.sprite = visualAssets.CircleSprite;
            indicatorImage.type = Image.Type.Simple;
            indicatorImage.preserveAspect = true;
            AddHudShadow(indicatorImage);
            fillObject.SetActive(false);
            indicatorObject.SetActive(false);

            needHudBindings.Add(
                new NeedHudBinding(
                    fillImage,
                    indicatorRect,
                    trackWidth,
                    reviewValue: reviewValue));
        }

        private static void AddHudShadow(Graphic graphic)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UiThemeTokens.HudMinimalShadow;
            shadow.effectDistance = new Vector2(0.65f, -0.65f);
            shadow.useGraphicAlpha = true;
        }
    }
}
