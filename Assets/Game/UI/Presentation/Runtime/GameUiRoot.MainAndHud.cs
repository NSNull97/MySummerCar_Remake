using System;
using System.Collections.Generic;
using System.Globalization;
using MSC.Core.Time;
using MSC.UI.Runtime.Localization;
using MSC.UI.Runtime.Routing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private readonly List<Outline> carColourOutlines = new List<Outline>();
        private static readonly Color[] CarColours =
        {
            new Color32(173, 78, 31, 255), new Color32(234, 224, 173, 255),
            new Color32(237, 237, 235, 255), new Color32(166, 20, 20, 255),
            new Color32(54, 58, 57, 255), new Color32(127, 137, 139, 255),
            new Color32(113, 63, 45, 255), new Color32(142, 149, 0, 255),
            new Color32(230, 191, 0, 255), new Color32(34, 174, 191, 255),
        };
        private int selectedCarColourIndex;

        private Color CurrentMenuGlassTint => UiThemeTokens.MenuGlassTint(
            CarColours[Mathf.Clamp(selectedCarColourIndex, 0, CarColours.Length - 1)]);

        partial void BuildMainMenuRoute()
        {
            const float primaryButtonX = 1308f;
            const float primaryButtonWidth = 254f;
            const float primaryButtonHeight = 68f;
            const float primaryButtonGap = 14f;
            const float primaryButtonStartY = 182f;
            const float primaryColumnRight = primaryButtonX + primaryButtonWidth;
            const float greetingWidth = 210f;

            GameObject route = CreateRoute(UiRouteId.MainMenu);
            CreateLogo(route.transform, 88f, 51f, 354f, 260f, large: true);
            CreateGreeting(
                route.transform,
                primaryColumnRight - greetingWidth,
                40f,
                greetingWidth,
                68f);

            factory.Button(
                "Continue",
                route.transform,
                textCatalog.Get("ui.main.continue"),
                UiIconKind.Play,
                primaryButtonX,
                primaryButtonStartY,
                primaryButtonWidth,
                primaryButtonHeight,
                null,
                selected: false,
                interactable: false,
                helper: textCatalog.Get("ui.main.no_save"),
                glass: true);
            factory.Button(
                "NewGame",
                route.transform,
                textCatalog.Get("ui.main.new_game"),
                UiIconKind.Plus,
                primaryButtonX,
                primaryButtonStartY + primaryButtonHeight + primaryButtonGap,
                primaryButtonWidth,
                primaryButtonHeight,
                BeginBoundedNewGameSession,
                selected: true,
                glass: true);
            factory.Button(
                "LoadGame",
                route.transform,
                textCatalog.Get("ui.main.load_game"),
                UiIconKind.Folder,
                primaryButtonX,
                primaryButtonStartY + (primaryButtonHeight + primaryButtonGap) * 2f,
                primaryButtonWidth,
                primaryButtonHeight,
                null,
                interactable: false,
                helper: textCatalog.Get("ui.main.no_save"),
                glass: true);
            factory.Button(
                "Credits",
                route.transform,
                textCatalog.Get("ui.main.credits"),
                UiIconKind.Credits,
                primaryButtonX,
                primaryButtonStartY + (primaryButtonHeight + primaryButtonGap) * 3f,
                primaryButtonWidth,
                primaryButtonHeight,
                () => ShowCredits(route.transform),
                glass: true);
            factory.Button(
                "Quit",
                route.transform,
                textCatalog.Get("ui.main.quit"),
                UiIconKind.Cross,
                primaryButtonX,
                primaryButtonStartY + (primaryButtonHeight + primaryButtonGap) * 4f,
                primaryButtonWidth,
                primaryButtonHeight,
                () => OpenQuitConfirmation(UiRouteId.MainMenu),
                destructive: true,
                glass: true);

            BuildColourCard(route.transform);
            BuildPreviewCard(route.transform);
            BuildPerformanceCard(route.transform);
            BuildMusicCard(route.transform);
            BuildMainUtilityStrip(route.transform);
            factory.Text(
                "Version",
                route.transform,
                "MSC REMAKE " + Application.version,
                20f,
                898f,
                260f,
                24f,
                11,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft);
        }

        partial void BuildPauseRoute()
        {
            GameObject route = CreateRoute(UiRouteId.Pause);
            GameObject panel = factory.Panel("PausePanel", route.transform, 636f, 150f, 400f, 640f);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.pause.title"),
                34f,
                30f,
                332f,
                31);
            factory.Button(
                "Resume",
                panel.transform,
                textCatalog.Get("ui.pause.resume"),
                UiIconKind.Play,
                34f,
                104f,
                332f,
                66f,
                EnterGameplay,
                selected: true);
            factory.Button(
                "PauseSettings",
                panel.transform,
                textCatalog.Get("ui.main.settings"),
                UiIconKind.Gear,
                34f,
                184f,
                332f,
                66f,
                () => OpenSettings(UiRouteId.SettingsGraphics, UiRouteId.Pause));
            factory.Button(
                "PauseSaveStatus",
                panel.transform,
                textCatalog.Get("ui.pause.save_status"),
                UiIconKind.Folder,
                34f,
                264f,
                332f,
                66f,
                () => OpenSaveStatus(UiRouteId.Pause));
            factory.Button(
                "ReturnMenu",
                panel.transform,
                textCatalog.Get("ui.pause.main_menu"),
                UiIconKind.Back,
                34f,
                344f,
                332f,
                66f,
                ReturnToFreshMainMenu);
            factory.Button(
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
            factory.Text(
                "PauseReferenceState",
                panel.transform,
                textCatalog.Get("ui.common.reference_pending"),
                34f,
                576f,
                332f,
                22f,
                10,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleCenter);
        }

        partial void BuildHudRoute()
        {
            GameObject route = CreateRoute(UiRouteId.InGameHud);
            GameObject clockPanel = factory.GlassPanel(
                "ClockMoney",
                route.transform,
                30f,
                37f,
                196f,
                165f,
                UiGlassKind.HudDark,
                UiThemeTokens.HudGlassTint);
            factory.Icon("Weather", clockPanel.transform, UiIconKind.Sun, 15f, 18f, 30f, new Color32(252, 187, 0, 255));
            hudDayText = factory.Text(
                "Day",
                clockPanel.transform,
                string.Empty,
                67f,
                9f,
                118f,
                22f,
                11,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            hudClockText = factory.Text(
                "Clock",
                clockPanel.transform,
                string.Empty,
                67f,
                31f,
                118f,
                34f,
                24,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft);
            hudDateText = factory.Text(
                "Date",
                clockPanel.transform,
                string.Empty,
                67f,
                66f,
                118f,
                18f,
                12,
                UiThemeTokens.TextMuted,
                TextAnchor.MiddleLeft);
            factory.Divider(clockPanel.transform, 0f, 93f, 196f);
            factory.Icon("MoneyIcon", clockPanel.transform, UiIconKind.Wallet, 15f, 111f, 30f);
            factory.Text(
                "MoneyLabel",
                clockPanel.transform,
                textCatalog.Get("ui.hud.money"),
                67f,
                101f,
                118f,
                22f,
                11,
                UiThemeTokens.TextMuted,
                TextAnchor.MiddleLeft);
            hudMoneyText = factory.Text(
                "MoneyValue",
                clockPanel.transform,
                "— MK",
                67f,
                124f,
                118f,
                27f,
                19,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft);

            GameObject needsPanel = factory.GlassPanel(
                "Needs",
                route.transform,
                30f,
                226f,
                196f,
                270f,
                UiGlassKind.HudDark,
                UiThemeTokens.HudGlassTint);
            needHudBindings.Clear();
            AddNeedRow(needsPanel.transform, 0, "ui.hud.thirst", UiIconKind.Thirst, UiThemeTokens.SurvivalThirst, 0.18f);
            AddNeedRow(needsPanel.transform, 1, "ui.hud.hunger", UiIconKind.Hunger, UiThemeTokens.SurvivalHunger, 0.36f);
            AddNeedRow(needsPanel.transform, 2, "ui.hud.stress", UiIconKind.Stress, UiThemeTokens.SurvivalStress, 0.22f);
            AddNeedRow(needsPanel.transform, 3, "ui.hud.urine", UiIconKind.Urine, UiThemeTokens.SurvivalUrine, 0.28f);
            AddNeedRow(needsPanel.transform, 4, "ui.hud.fatigue", UiIconKind.Fatigue, UiThemeTokens.SurvivalFatigue, 0.44f);
            AddNeedRow(needsPanel.transform, 5, "ui.hud.dirtiness", UiIconKind.Dirtiness, UiThemeTokens.SurvivalDirtiness, 0.71f);
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
                hudDayText.text = "SATURDAY";
                hudClockText.text = "14:37";
                hudDateText.text = "27.06.1992";
                hudMoneyText.text = "9 137,95 MK";
                for (int index = 0; index < needHudBindings.Count; index++)
                {
                    NeedHudBinding binding = needHudBindings[index];
                    binding.ValueText.text = Mathf.RoundToInt(binding.ReviewValue * 100f) + "%";
                    binding.Fill.fillAmount = binding.ReviewValue;
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
                    hudDayText.text = formatter.Culture.TextInfo.ToUpper(
                        formatter.Format("{0:dddd}", date));
                    hudDateText.text = formatter.Format("{0:dd.MM.yyyy}", date);
                }
            }
            else
            {
                hudDayText.text = "—";
                hudClockText.text = "--:--";
                hudDateText.text = "--.--.----";
            }

            hudMoneyText.text = "— MK";
            for (int index = 0; index < needHudBindings.Count; index++)
            {
                NeedHudBinding binding = needHudBindings[index];
                binding.ValueText.text = "—";
                binding.Fill.fillAmount = 0f;
            }
        }

        private void CreateLogo(
            Transform parent,
            float x,
            float y,
            float width,
            float height,
            bool large)
        {
            GameObject logo = factory.CreateObject("ProjectOwnedLogo", parent);
            factory.Place(logo, x, y, width, height);
            Texture2D logoTexture = dependencies.MenuLogoTexture;
            if (logoTexture != null && logoTexture.width > 0 && logoTexture.height > 0)
            {
                // The supplied project logo deliberately keeps transparent
                // source padding. Present only its padded alpha bounds so the
                // artwork, rather than the empty canvas, owns the layout box.
                var artworkUv = new Rect(
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
            GameObject card = factory.GlassPanel(
                "CarColourCard",
                parent,
                93f,
                613f,
                327f,
                178f,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            factory.Text("Title", card.transform, textCatalog.Get("ui.main.car_color"), 24f, 8f, 286f, 30f, 14, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            carColourOutlines.Clear();
            for (int index = 0; index < CarColours.Length; index++)
            {
                int column = index % 5;
                int row = index / 5;
                GameObject swatch = factory.CreateObject("Colour" + index, card.transform);
                factory.Place(swatch, 25f + column * 57f, 50f + row * 57f, 48f, 48f);
                Image surface = factory.AddSurface(swatch, CarColours[index]);
                var outline = swatch.AddComponent<Outline>();
                outline.effectColor = UiThemeTokens.Accent;
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = false;
                outline.enabled = index == selectedCarColourIndex;
                carColourOutlines.Add(outline);

                int colourIndex = index;
                Button button = swatch.AddComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.ColorTint;
                button.onClick.AddListener(() => SelectCarColour(colourIndex));
            }
        }

        private void SelectCarColour(int index)
        {
            selectedCarColourIndex = Mathf.Clamp(index, 0, carColourOutlines.Count - 1);
            for (int outlineIndex = 0; outlineIndex < carColourOutlines.Count; outlineIndex++)
            {
                Outline outline = carColourOutlines[outlineIndex];
                if (outline != null)
                {
                    outline.enabled = outlineIndex == selectedCarColourIndex;
                }
            }

            RefreshGlassSurfaces();
        }

        private void BuildPreviewCard(Transform parent)
        {
            GameObject card = factory.GlassPanel("InteriorCard", parent, 430f, 613f, 325f, 178f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Text("Title", card.transform, textCatalog.Get("ui.main.interior"), 24f, 8f, 286f, 30f, 14, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject preview = factory.Panel("Preview", card.transform, 22f, 46f, 288f, 112f, new Color(0.08f, 0.045f, 0.032f, 0.92f));
            factory.Text("Status", preview.transform, textCatalog.Get("ui.main.preview_unavailable"), 16f, 12f, 256f, 88f, 12, UiThemeTokens.TextMuted, TextAnchor.MiddleCenter);
        }

        private void BuildPerformanceCard(Transform parent)
        {
            GameObject card = factory.GlassPanel("PerformanceCard", parent, 765f, 613f, 371f, 178f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Text("Title", card.transform, textCatalog.Get("ui.main.performance"), 24f, 8f, 329f, 30f, 14, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject graph = factory.Panel("Graph", card.transform, 24f, 46f, 329f, 112f, new Color(0.045f, 0.05f, 0.052f, 0.9f));
            for (int line = 1; line < 5; line++)
            {
                factory.Divider(graph.transform, 0f, line * 22f, 329f, new Color(1f, 1f, 1f, 0.08f));
            }
            factory.Text("Fps", graph.transform, AverageFpsText(), 12f, 22f, 305f, 55f, 22, UiThemeTokens.Positive, TextAnchor.MiddleCenter, FontStyle.Bold);
            factory.Text("Measured", graph.transform, textCatalog.Get("ui.main.live_frame_sample"), 12f, 77f, 305f, 22f, 10, UiThemeTokens.TextMuted, TextAnchor.MiddleCenter);
        }

        private void BuildMusicCard(Transform parent)
        {
            GameObject card = factory.GlassPanel("MusicCard", parent, 93f, 805f, 1043f, 78f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Icon("Music", card.transform, UiIconKind.Music, 18f, 18f, 46f, UiThemeTokens.Disabled);
            factory.Text("Title", card.transform, textCatalog.Get("ui.main.music_import"), 78f, 9f, 310f, 30f, 15, UiThemeTokens.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            factory.Text("State", card.transform, textCatalog.Get("ui.main.import_unavailable"), 78f, 41f, 520f, 25f, 12, UiThemeTokens.TextMuted, TextAnchor.MiddleLeft);
            factory.CompactButton("ImportUnavailable", card.transform, textCatalog.Get("ui.common.unavailable"), 807f, 20f, 210f, 42f, null, interactable: false);
        }

        private void BuildMainUtilityStrip(Transform parent)
        {
            GameObject strip = factory.CreateObject("UtilityStrip", parent);
            float firstX = 0f;
            float secondX = UiThemeTokens.UtilityActionFirstWidth + UiThemeTokens.SpacingCompact;
            float thirdX = secondX + UiThemeTokens.UtilityActionSecondWidth + UiThemeTokens.SpacingCompact;
            factory.Place(strip, 1151f, 849f, 455f, UiThemeTokens.UtilityActionButtonHeight);
            factory.CompactButton("Settings", strip.transform, textCatalog.Get("ui.main.settings"), firstX, 0f, UiThemeTokens.UtilityActionFirstWidth, UiThemeTokens.UtilityActionButtonHeight, () => OpenSettings(UiRouteId.SettingsGraphics, UiRouteId.MainMenu), glass: true);
            factory.CompactButton("Mods", strip.transform, textCatalog.Get("ui.main.mods"), secondX, 0f, UiThemeTokens.UtilityActionSecondWidth, UiThemeTokens.UtilityActionButtonHeight, () => OpenSettings(UiRouteId.SettingsMods, UiRouteId.MainMenu), glass: true);
            bool dev = capabilities.Get(MSC.UI.Runtime.Capabilities.UiCapabilityId.DeveloperTools).IsInteractive;
            if (dev)
            {
                factory.CompactButton("DevTools", strip.transform, textCatalog.Get("ui.main.dev_tools"), thirdX, 0f, UiThemeTokens.UtilityActionThirdWidth, UiThemeTokens.UtilityActionButtonHeight, () => ShowNotice("ui.common.adapter_pending"), glass: true);
            }
        }

        private void ShowCredits(Transform parent)
        {
            GameObject returnSelection = EventSystem.current?.currentSelectedGameObject;
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
            factory.CompactButton(
                "CreditsBack",
                panel.transform,
                textCatalog.Get("ui.common.back"),
                174f,
                424f,
                252f,
                48f,
                () => CloseCredits(overlay, returnSelection),
                primary: true);
            UiFactory.SelectFirst(overlay);
        }

        private void CloseCredits(GameObject overlay, GameObject returnSelection)
        {
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
            Color colour,
            float reviewValue)
        {
            float y = 4f + index * 43f;
            factory.Icon("NeedIcon" + index, parent, icon, 14f, y + 8f, 26f, colour);
            factory.Text("NeedLabel" + index, parent, textCatalog.Get(labelKey), 51f, y, 92f, 22f, 11, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text value = factory.Text("NeedValue" + index, parent, "—", 144f, y, 38f, 22f, 10, UiThemeTokens.TextPrimary, TextAnchor.MiddleRight);
            GameObject track = factory.CreateObject("NeedTrack" + index, parent);
            factory.Place(track, 51f, y + 27f, 131f, 6f);
            factory.AddSurface(track, new Color(0.25f, 0.25f, 0.24f, 0.86f));
            GameObject fillObject = factory.CreateObject("NeedFill" + index, track.transform);
            RectTransform fillRect = factory.Stretch(fillObject);
            Image fill = factory.AddSurface(fillObject, colour);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            needHudBindings.Add(new NeedHudBinding(value, fill, reviewValue));
        }
    }
}
