using System;
using System.Collections.Generic;
using System.Globalization;
using MSC.UI.Runtime.Capabilities;
using MSC.UI.Runtime.Routing;
using MSC.UI.Runtime.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private InputActionRebindingExtensions.RebindingOperation activeRebindOperation;
        private InputAction activeRebindAction;
        private int activeRebindBindingIndex = -1;
        private string activeRebindPreviousOverridePath;
        private bool activeRebindActionWasEnabled;
        private bool activeRebindPauseWasEnabled;
        private bool suppressRebindRouteRebuild;

        partial void BuildGraphicsRoute()
        {
            GraphicsSettingsDto pending = settings.Pending.Graphics;
            GameObject route = CreateSettingsShell(UiRouteId.SettingsGraphics);

            GameObject panel = factory.GlassPanel(
                "GraphicsSettingsPanel",
                route.transform,
                370f,
                135f,
                618f,
                716f,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.category.graphics"),
                24f,
                10f,
                570f,
                23);
            factory.Divider(panel.transform, 24f, 57f, 570f);

            float rowY = 64f;
            Text displayMode = null;
            displayMode = AddStepperRow(
                panel.transform,
                "DisplayMode",
                textCatalog.Get("ui.graphics.display_mode"),
                rowY,
                GraphicsDisplayModeText(pending.DisplayMode),
                true,
                direction =>
                {
                    UiDisplayMode current = settings.Pending.Graphics.DisplayMode;
                    UiDisplayMode next = (UiDisplayMode)WrapIndex((int)current + direction, 3);
                    settings.EditPending(document => document.Graphics.DisplayMode = next);
                    displayMode.text = GraphicsDisplayModeText(next);
                });

            rowY += 34f;
            List<Vector2Int> resolutions = CollectResolutionChoices(pending);
            Text resolution = null;
            resolution = AddStepperRow(
                panel.transform,
                "Resolution",
                textCatalog.Get("ui.graphics.resolution"),
                rowY,
                ResolutionText(pending.ResolutionWidth, pending.ResolutionHeight),
                true,
                direction =>
                {
                    GraphicsSettingsDto current = settings.Pending.Graphics;
                    int index = IndexOfResolution(resolutions, current.ResolutionWidth, current.ResolutionHeight);
                    if (index < 0)
                    {
                        index = 0;
                    }

                    Vector2Int next = resolutions[WrapIndex(index + direction, resolutions.Count)];
                    settings.EditPending(document =>
                    {
                        document.Graphics.ResolutionWidth = next.x;
                        document.Graphics.ResolutionHeight = next.y;
                    });
                    resolution.text = ResolutionText(next.x, next.y);
                });

            rowY += 34f;
            List<int> refreshRates = CollectRefreshRateChoices(pending);
            Text refreshRate = null;
            refreshRate = AddStepperRow(
                panel.transform,
                "RefreshRate",
                textCatalog.Get("ui.graphics.refresh"),
                rowY,
                RefreshRateText(pending.RefreshRateNumerator, pending.RefreshRateDenominator),
                true,
                direction =>
                {
                    GraphicsSettingsDto current = settings.Pending.Graphics;
                    int currentHz = RoundedRefreshRate(current.RefreshRateNumerator, current.RefreshRateDenominator);
                    int index = refreshRates.IndexOf(currentHz);
                    int next = refreshRates[WrapIndex(index + direction, refreshRates.Count)];
                    settings.EditPending(document =>
                    {
                        document.Graphics.RefreshRateNumerator = next;
                        document.Graphics.RefreshRateDenominator = 1;
                    });
                    refreshRate.text = next.ToString(CultureInfo.InvariantCulture) + " Hz";
                });

            rowY += 34f;
            AddToggleRow(
                panel.transform,
                "VSync",
                textCatalog.Get("ui.graphics.vsync"),
                rowY,
                pending.VSync,
                value => settings.EditPending(document => document.Graphics.VSync = value));

            rowY += 40f;
            AddUnavailableRow(
                panel.transform,
                "Upscaling",
                textCatalog.Get("ui.graphics.upscaling"),
                rowY,
                CapabilityText(UiCapabilityId.GraphicsUpscaler));
            rowY += 34f;
            AddUnavailableRow(
                panel.transform,
                "UpscalerQuality",
                textCatalog.Get("ui.graphics.upscaler_quality"),
                rowY,
                CapabilityText(UiCapabilityId.GraphicsUpscaler));
            rowY += 34f;
            AddUnavailableRow(
                panel.transform,
                "Sharpening",
                textCatalog.Get("ui.graphics.sharpening"),
                rowY,
                CapabilityText(UiCapabilityId.GraphicsUpscaler));
            rowY += 34f;
            AddUnavailableRow(
                panel.transform,
                "FrameGeneration",
                textCatalog.Get("ui.graphics.frame_generation"),
                rowY,
                CapabilityText(UiCapabilityId.GraphicsFrameGeneration));

            rowY += 40f;
            string[] qualityNames = QualitySettings.names;
            Text quality = null;
            quality = AddStepperRow(
                panel.transform,
                "QualityPreset",
                textCatalog.Get("ui.graphics.quality"),
                rowY,
                QualityText(pending.QualityLevel, qualityNames),
                qualityNames != null && qualityNames.Length > 0,
                direction =>
                {
                    int count = qualityNames == null ? 0 : qualityNames.Length;
                    if (count == 0)
                    {
                        return;
                    }

                    int next = WrapIndex(settings.Pending.Graphics.QualityLevel + direction, count);
                    settings.EditPending(document => document.Graphics.QualityLevel = next);
                    quality.text = QualityText(next, qualityNames);
                });

            rowY += 34f;
            AddUnavailableRow(panel.transform, "TextureQuality", textCatalog.Get("ui.graphics.texture"), rowY, CapabilityText(UiCapabilityId.GraphicsAdvancedQuality));
            rowY += 34f;
            AddUnavailableRow(panel.transform, "ShadowQuality", textCatalog.Get("ui.graphics.shadow"), rowY, CapabilityText(UiCapabilityId.GraphicsAdvancedQuality));
            rowY += 34f;
            AddUnavailableRow(panel.transform, "ReflectionQuality", textCatalog.Get("ui.graphics.reflection"), rowY, CapabilityText(UiCapabilityId.GraphicsAdvancedQuality));
            rowY += 34f;
            AddUnavailableRow(panel.transform, "VolumetricQuality", textCatalog.Get("ui.graphics.volumetric"), rowY, CapabilityText(UiCapabilityId.GraphicsAdvancedQuality));
            rowY += 34f;
            AddUnavailableRow(panel.transform, "VegetationDensity", textCatalog.Get("ui.graphics.vegetation"), rowY, CapabilityText(UiCapabilityId.GraphicsAdvancedQuality));
            rowY += 34f;
            AddUnavailableRow(panel.transform, "PostProcessing", textCatalog.Get("ui.graphics.post"), rowY, CapabilityText(UiCapabilityId.GraphicsPostProcessAdapter));

            rowY += 40f;
            AddPendingToggleRow(panel.transform, "MotionBlur", textCatalog.Get("ui.graphics.motion_blur"), rowY, pending.MotionBlur, UiCapabilityId.GraphicsPostProcessAdapter);
            rowY += 34f;
            AddPendingToggleRow(panel.transform, "DepthOfField", textCatalog.Get("ui.graphics.depth_of_field"), rowY, pending.DepthOfField, UiCapabilityId.GraphicsPostProcessAdapter);
            rowY += 34f;
            AddUnavailableRow(panel.transform, "RayTracing", textCatalog.Get("ui.graphics.ray_tracing"), rowY, CapabilityText(UiCapabilityId.GraphicsRayTracing));

            BuildStandardSettingsActions(route.transform);
            BuildVersionLabel(route.transform);
        }

        partial void BuildAudioRoute()
        {
            AudioSettingsDto pending = settings.Pending.Audio;
            GameObject route = CreateSettingsShell(UiRouteId.SettingsAudio);

            GameObject panel = factory.GlassPanel(
                "AudioSettingsPanel",
                route.transform,
                370f,
                181f,
                678f,
                681f,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.audio.title"),
                22f,
                12f,
                634f,
                22);

            float rowY = 54f;
            AddAudioSliderRow(panel.transform, "Master", textCatalog.Get("ui.audio.master"), rowY, pending.Master01, value => settings.EditPending(document => document.Audio.Master01 = value));
            rowY += 44f;
            AddAudioSliderRow(panel.transform, "Engine", textCatalog.Get("ui.audio.engine"), rowY, pending.Engine01, value => settings.EditPending(document => document.Audio.Engine01 = value));
            rowY += 44f;
            AddAudioSliderRow(panel.transform, "Environment", textCatalog.Get("ui.audio.environment"), rowY, pending.Environment01, value => settings.EditPending(document => document.Audio.Environment01 = value));
            rowY += 44f;
            AddUnavailableAudioRow(panel.transform, "Voice", textCatalog.Get("ui.audio.voice"), rowY, UiCapabilityId.AudioIndependentVoiceBus);
            rowY += 44f;
            AddAudioSliderRow(panel.transform, "Effects", textCatalog.Get("ui.audio.effects"), rowY, pending.Effects01, value => settings.EditPending(document => document.Audio.Effects01 = value));
            rowY += 44f;
            AddAudioSliderRow(panel.transform, "Music", textCatalog.Get("ui.audio.music"), rowY, pending.Music01, value => settings.EditPending(document => document.Audio.Music01 = value));
            rowY += 44f;
            AddUnavailableAudioRow(panel.transform, "Radio", textCatalog.Get("ui.audio.radio"), rowY, UiCapabilityId.AudioIndependentRadioBus);
            rowY += 44f;
            AddAudioSliderRow(panel.transform, "Ui", textCatalog.Get("ui.audio.ui"), rowY, pending.Ui01, value => settings.EditPending(document => document.Audio.Ui01 = value));

            rowY += 50f;
            AddPendingToggleRow(panel.transform, "Weather", textCatalog.Get("ui.audio.weather"), rowY, true, UiCapabilityId.AudioIndependentWeatherBus, 654f);
            rowY += 39f;
            AddPendingToggleRow(panel.transform, "Thunder", textCatalog.Get("ui.audio.thunder"), rowY, true, UiCapabilityId.AudioIndependentThunderBus, 654f);
            rowY += 39f;
            AddUnavailableRow(panel.transform, "Reverb", textCatalog.Get("ui.audio.reverb"), rowY, CapabilityText(UiCapabilityId.AudioReverbProfile), 654f);
            rowY += 39f;

            Text dynamicRange = null;
            dynamicRange = AddStepperRow(
                panel.transform,
                "DynamicRange",
                textCatalog.Get("ui.audio.dynamic"),
                rowY,
                DynamicRangeText(pending.DynamicRange),
                true,
                direction =>
                {
                    UiDynamicRangeMode current = settings.Pending.Audio.DynamicRange;
                    UiDynamicRangeMode next = (UiDynamicRangeMode)WrapIndex((int)current + direction, 3);
                    settings.EditPending(document => document.Audio.DynamicRange = next);
                    dynamicRange.text = DynamicRangeText(next);
                },
                654f);

            rowY += 39f;
            AddToggleRow(
                panel.transform,
                "Subtitles",
                textCatalog.Get("ui.audio.subtitles"),
                rowY,
                pending.SubtitlesEnabled,
                value => settings.EditPending(document => document.Audio.SubtitlesEnabled = value),
                654f);
            rowY += 39f;
            AddUnavailableRow(
                panel.transform,
                "Output",
                textCatalog.Get("ui.audio.output"),
                rowY,
                textCatalog.Get("ui.common.system_default"),
                654f);

            BuildAudioProfileCard(route.transform);
            BuildStandardSettingsActions(route.transform);
            BuildVersionLabel(route.transform);
        }

        partial void BuildControlsRoute()
        {
            ControlsSettingsDto pending = settings.Pending.Controls;
            GameObject route = CreateSettingsShell(UiRouteId.SettingsControls);

            GameObject bindings = factory.GlassPanel(
                "BindingsPanel",
                route.transform,
                370f,
                119f,
                643f,
                741f,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            factory.Heading(bindings.transform, textCatalog.Get("ui.category.controls"), 26f, 12f, 590f, 25);
            factory.Text(
                "KeyboardHeading",
                bindings.transform,
                textCatalog.Get("ui.controls.keyboard"),
                26f,
                51f,
                590f,
                28f,
                17,
                UiThemeTokens.Accent,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            factory.Text("ActionColumn", bindings.transform, textCatalog.Get("ui.controls.action"), 36f, 82f, 230f, 24f, 11, UiThemeTokens.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            factory.Text("PrimaryColumn", bindings.transform, textCatalog.Get("ui.controls.primary"), 290f, 82f, 142f, 24f, 11, UiThemeTokens.TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            factory.Text("SecondaryColumn", bindings.transform, textCatalog.Get("ui.controls.secondary"), 448f, 82f, 160f, 24f, 11, UiThemeTokens.TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);

            List<InputBindingRow> rows = CreateInputBindingRows();
            for (int index = 0; index < rows.Count; index++)
            {
                BuildInputBindingRow(bindings.transform, rows[index], index, 112f + index * 38f);
            }

            BuildMouseSettingsCard(route.transform, pending);
            BuildGamepadSettingsCard(route.transform, pending);
            BuildStandardSettingsActions(route.transform);
            BuildVersionLabel(route.transform);
        }

        partial void BuildGameplayRoute()
        {
            GameplaySettingsDto pending = settings.Pending.Gameplay;
            GameObject route = CreateSettingsShell(UiRouteId.SettingsGameplay);

            GameObject panel = factory.GlassPanel(
                "GameplaySettingsPanel",
                route.transform,
                370f,
                55f,
                791f,
                807f,
                UiGlassKind.MenuTinted,
                CurrentMenuGlassTint);
            factory.Heading(panel.transform, textCatalog.Get("ui.category.gameplay"), 36f, 16f, 720f, 27);
            factory.Text(
                "Subtitle",
                panel.transform,
                textCatalog.Get("ui.gameplay.subtitle"),
                36f,
                53f,
                720f,
                28f,
                13,
                UiThemeTokens.TextMuted,
                TextAnchor.MiddleLeft);

            float rowY = 96f;
            AddUnavailableRow(panel.transform, "Autosave", textCatalog.Get("ui.gameplay.autosave"), rowY, CapabilityText(UiCapabilityId.Autosave), 748f);
            rowY += 47f;
            AddUnavailableRow(panel.transform, "SaveConfirmation", textCatalog.Get("ui.gameplay.save_confirmation"), rowY, CapabilityText(UiCapabilityId.SaveStorage), 748f);
            rowY += 47f;
            AddUnavailableRow(panel.transform, "PauseBehaviour", textCatalog.Get("ui.gameplay.pause"), rowY, textCatalog.Get("ui.gameplay.pause_freeze"), 748f);
            rowY += 47f;

            AddStepperRow(
                panel.transform,
                "HudMode",
                textCatalog.Get("ui.gameplay.hud"),
                rowY,
                textCatalog.Get("ui.common.adapter_pending"),
                false,
                null,
                748f);
            rowY += 47f;

            AddStepperRow(
                panel.transform,
                "Units",
                textCatalog.Get("ui.gameplay.units"),
                rowY,
                textCatalog.Get("ui.common.adapter_pending"),
                false,
                null,
                748f);
            rowY += 47f;

            Text language = null;
            language = AddStepperRow(
                panel.transform,
                "Language",
                textCatalog.Get("ui.gameplay.language"),
                rowY,
                LanguageText(pending.LanguageId),
                true,
                direction =>
                {
                    string next = settings.Pending.Gameplay.LanguageId.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                        ? "en-US"
                        : "ru-RU";
                    settings.EditPending(document => document.Gameplay.LanguageId = next);
                    language.text = LanguageText(next);
                },
                748f);
            rowY += 52f;

            AddUnavailableRow(panel.transform, "Difficulty", textCatalog.Get("ui.gameplay.difficulty"), rowY, CapabilityText(UiCapabilityId.DifficultyPresets), 748f);
            rowY += 47f;
            AddUnavailableRow(panel.transform, "Fatigue", textCatalog.Get("ui.gameplay.fatigue"), rowY, textCatalog.Get("ui.common.adapter_pending"), 748f);
            rowY += 47f;
            AddUnavailableRow(panel.transform, "Alcohol", textCatalog.Get("ui.gameplay.alcohol"), rowY, textCatalog.Get("ui.common.adapter_pending"), 748f);
            rowY += 47f;
            AddToggleRow(panel.transform, "Hints", textCatalog.Get("ui.gameplay.hints"), rowY, pending.ContextualHints, null, 748f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 47f;
            AddUnavailableRow(panel.transform, "TutorialPrompts", textCatalog.Get("ui.gameplay.tutorial"), rowY, textCatalog.Get("ui.common.unavailable"), 748f);
            rowY += 47f;
            AddToggleRow(panel.transform, "Outlines", textCatalog.Get("ui.gameplay.outlines"), rowY, pending.InteractionOutlines, null, 748f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 47f;
            AddUnavailableRow(panel.transform, "CameraShake", textCatalog.Get("ui.gameplay.camera_shake"), rowY, textCatalog.Get("ui.common.adapter_pending"), 748f);
            rowY += 47f;

            AddToggleRow(
                panel.transform,
                "DevelopmentUi",
                textCatalog.Get("ui.gameplay.developer"),
                rowY,
                pending.DevelopmentUiVisible,
                null,
                748f,
                false,
                textCatalog.Get("ui.common.adapter_pending"));

            BuildStandardSettingsActions(route.transform);
            BuildVersionLabel(route.transform);
        }

        partial void BuildAccessibilityRoute()
        {
            UiSettingsDocument pending = settings.Pending;
            GameObject route = CreateSettingsShell(UiRouteId.SettingsAccessibility);

            GameObject panel = factory.GlassPanel("AccessibilityPanel", route.transform, 370f, 135f, 760f, 716f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Heading(panel.transform, textCatalog.Get("ui.access.title"), 28f, 18f, 704f, 25);
            factory.Text("ReferenceState", panel.transform, textCatalog.Get("ui.common.reference_pending"), 28f, 56f, 704f, 24f, 11, UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);

            float rowY = 104f;
            AddRangeSliderRow(panel.transform, "UiScale", textCatalog.Get("ui.access.scale"), rowY, pending.Accessibility.UiScale, 0.85f, 1.25f, value => settings.EditPending(document => document.Accessibility.UiScale = value), value => Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture) + "%", 704f);
            rowY += 60f;
            AddToggleRow(panel.transform, "HighContrast", textCatalog.Get("ui.access.contrast"), rowY, pending.Accessibility.HighContrast, null, 704f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 60f;
            AddToggleRow(panel.transform, "ReducedMotion", textCatalog.Get("ui.access.motion"), rowY, pending.Accessibility.ReducedMotion, null, 704f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 60f;
            AddToggleRow(panel.transform, "ToggleHold", textCatalog.Get("ui.access.toggle_hold"), rowY, pending.Accessibility.ToggleHoldActions, null, 704f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 60f;
            AddToggleRow(panel.transform, "ColorCues", textCatalog.Get("ui.access.color_cues"), rowY, pending.Accessibility.ColorIndependentCues, null, 704f, false, textCatalog.Get("ui.common.adapter_pending"));
            rowY += 60f;
            AddToggleRow(panel.transform, "AccessSubtitles", textCatalog.Get("ui.audio.subtitles"), rowY, pending.Audio.SubtitlesEnabled, value => settings.EditPending(document => document.Audio.SubtitlesEnabled = value), 704f);
            rowY += 60f;
            AddToggleRow(panel.transform, "AccessCaptions", textCatalog.Get("ui.access.captions"), rowY, pending.Audio.CaptionsEnabled, value => settings.EditPending(document => document.Audio.CaptionsEnabled = value), 704f);

            BuildStandardSettingsActions(route.transform);
            BuildVersionLabel(route.transform);
        }

        partial void BuildModsRoute()
        {
            GameObject route = CreateSettingsShell(UiRouteId.SettingsMods);
            GameObject panel = factory.GlassPanel("ModsPanel", route.transform, 370f, 184f, 760f, 430f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Icon("ModsIcon", panel.transform, UiIconKind.Wrench, 40f, 42f, 64f, UiThemeTokens.Disabled);
            factory.Heading(panel.transform, textCatalog.Get("ui.mods.title"), 128f, 42f, 584f, 30);
            factory.Text("ReferenceState", panel.transform, textCatalog.Get("ui.common.reference_pending"), 128f, 92f, 584f, 24f, 12, UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            factory.Divider(panel.transform, 40f, 142f, 680f);
            factory.Text("Unavailable", panel.transform, textCatalog.Get("ui.mods.unavailable"), 40f, 176f, 680f, 132f, 20, UiThemeTokens.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            factory.Text("BuildState", panel.transform, CapabilityText(UiCapabilityId.Mods), 40f, 324f, 680f, 40f, 13, UiThemeTokens.TextMuted, TextAnchor.MiddleCenter);
            BuildVersionLabel(route.transform);
        }

        private GameObject CreateSettingsShell(UiRouteId selectedRoute)
        {
            // Settings chrome is deliberately invariant between categories. Only
            // the selected state and the central/right content are allowed to
            // change, preventing the left column from jumping between routes.
            const float logoX = 83f;
            const float logoY = 44f;
            const float logoWidth = 238f;
            const float logoHeight = 175f;
            const float navigationX = 84f;
            const float navigationY = 242f;
            const float navigationWidth = 235f;
            const float navigationHeight = 406f;
            const float backX = 84f;
            const float backY = 779f;
            const float backWidth = 235f;
            const float backHeight = 59f;

            GameObject route = CreateRoute(selectedRoute);
            CreateLogo(route.transform, logoX, logoY, logoWidth, logoHeight, large: false);
            BuildSettingsNavigation(route.transform, selectedRoute, navigationX, navigationY, navigationWidth, navigationHeight);
            factory.Button(
                "Back",
                route.transform,
                textCatalog.Get("ui.common.back"),
                UiIconKind.Back,
                backX,
                backY,
                backWidth,
                backHeight,
                ReturnFromSettingsWithBindings,
                destructive: true,
                glass: true);
            CreateGreeting(route.transform, 1352f, 39f, 210f, 68f);

            return route;
        }

        private void BuildSettingsNavigation(
            Transform parent,
            UiRouteId selectedRoute,
            float x,
            float y,
            float width,
            float height)
        {
            UiRouteId[] routeIds =
            {
                UiRouteId.SettingsGraphics,
                UiRouteId.SettingsAudio,
                UiRouteId.SettingsControls,
                UiRouteId.SettingsGameplay,
                UiRouteId.SettingsAccessibility,
                UiRouteId.SettingsMods,
            };
            string[] labelKeys =
            {
                "ui.category.graphics",
                "ui.category.audio",
                "ui.category.controls",
                "ui.category.gameplay",
                "ui.category.accessibility",
                "ui.category.mods",
            };
            UiIconKind[] icons =
            {
                UiIconKind.Display,
                UiIconKind.Speaker,
                UiIconKind.Gamepad,
                UiIconKind.Gear,
                UiIconKind.Person,
                UiIconKind.Wrench,
            };

            const float gap = UiThemeTokens.SpacingCompact;
            float buttonHeight = (height - gap * (routeIds.Length - 1)) / routeIds.Length;
            for (int index = 0; index < routeIds.Length; index++)
            {
                UiRouteId target = routeIds[index];
                factory.Button(
                    "Navigate" + target,
                    parent,
                    textCatalog.Get(labelKeys[index]),
                    icons[index],
                    x,
                    y + index * (buttonHeight + gap),
                    width,
                    buttonHeight,
                    () => NavigateSettings(target),
                    selected: target == selectedRoute,
                    glass: true);
            }
        }

        private void NavigateSettings(UiRouteId target)
        {
            CancelActiveRebind();
            ShowRoute(target);
        }

        private void BuildVersionLabel(Transform parent)
        {
            factory.Text(
                "Version",
                parent,
                "MSC REMAKE " + Application.version,
                20f,
                898f,
                260f,
                24f,
                11,
                UiThemeTokens.Disabled,
                TextAnchor.MiddleLeft);
        }

        private Text AddStepperRow(
            Transform parent,
            string name,
            string label,
            float y,
            string value,
            bool interactable,
            Action<int> onStep,
            float rowWidth = 570f)
        {
            float rowX = RowOffset(parent, rowWidth);
            AddRowSurface(parent, name + "Row", y, rowWidth);
            factory.Text(name + "Label", parent, label, rowX + 10f, y, rowWidth * 0.46f, 32f, 13, interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            float widgetX = rowX + rowWidth * 0.48f;
            float widgetWidth = rowWidth * 0.50f;
            factory.CompactButton(name + "Previous", parent, "‹", widgetX, y + 3f, 36f, 26f, () => onStep?.Invoke(-1), interactable: interactable);
            GameObject valuePanel = factory.Panel(name + "ValuePanel", parent, widgetX + 40f, y + 3f, widgetWidth - 80f, 26f, UiThemeTokens.RowAlternate);
            Text valueText = factory.Text(name + "Value", valuePanel.transform, value, 6f, 0f, widgetWidth - 92f, 26f, 12, interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled, TextAnchor.MiddleCenter);
            factory.CompactButton(name + "Next", parent, "›", widgetX + widgetWidth - 36f, y + 3f, 36f, 26f, () => onStep?.Invoke(1), interactable: interactable);
            return valueText;
        }

        private void AddToggleRow(
            Transform parent,
            string name,
            string label,
            float y,
            bool value,
            Action<bool> onChanged,
            float rowWidth = 570f,
            bool interactable = true,
            string status = "")
        {
            float rowX = RowOffset(parent, rowWidth);
            AddRowSurface(parent, name + "Row", y, rowWidth);
            factory.Text(name + "Label", parent, label, rowX + 10f, y, rowWidth * 0.62f, 32f, 13, interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!string.IsNullOrEmpty(status))
            {
                factory.Text(name + "State", parent, status, rowX + rowWidth - 220f, y, 130f, 32f, 10, UiThemeTokens.Disabled, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            factory.Toggle(name + "Toggle", parent, rowX + rowWidth - 80f, y + 2f, value, changed => onChanged?.Invoke(changed), interactable);
        }

        private void AddPendingToggleRow(
            Transform parent,
            string name,
            string label,
            float y,
            bool value,
            UiCapabilityId capability,
            float rowWidth = 570f)
        {
            AddToggleRow(parent, name, label, y, value, null, rowWidth, false, CapabilityText(capability));
        }

        private void AddUnavailableRow(
            Transform parent,
            string name,
            string label,
            float y,
            string state,
            float rowWidth = 570f)
        {
            float rowX = RowOffset(parent, rowWidth);
            AddRowSurface(parent, name + "Row", y, rowWidth);
            factory.Text(name + "Label", parent, label, rowX + 10f, y, rowWidth * 0.58f, 32f, 13, UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject statePanel = factory.Panel(name + "StatePanel", parent, rowX + rowWidth * 0.60f, y + 3f, rowWidth * 0.38f, 26f, UiThemeTokens.RowAlternate);
            factory.Text(name + "State", statePanel.transform, state, 6f, 0f, rowWidth * 0.38f - 12f, 26f, 10, UiThemeTokens.Disabled, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void AddReadOnlyRow(
            Transform parent,
            string name,
            string label,
            float y,
            string value,
            float rowWidth = 570f)
        {
            float rowX = RowOffset(parent, rowWidth);
            AddRowSurface(parent, name + "Row", y, rowWidth);
            factory.Text(name + "Label", parent, label, rowX + 10f, y, rowWidth * 0.58f, 32f, 13, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject valuePanel = factory.Panel(name + "StatePanel", parent, rowX + rowWidth * 0.60f, y + 3f, rowWidth * 0.38f, 26f, UiThemeTokens.RowAlternate);
            factory.Text(name + "State", valuePanel.transform, value, 6f, 0f, rowWidth * 0.38f - 12f, 26f, 10, UiThemeTokens.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void AddNormalizedSliderRow(
            Transform parent,
            string name,
            string label,
            float y,
            float value,
            Action<float> onChanged,
            float rowWidth = 570f)
        {
            AddRangeSliderRow(
                parent,
                name,
                label,
                y,
                value,
                0f,
                1f,
                onChanged,
                normalized => Mathf.RoundToInt(normalized * 100f).ToString(CultureInfo.InvariantCulture) + "%",
                rowWidth);
        }

        private void AddRangeSliderRow(
            Transform parent,
            string name,
            string label,
            float y,
            float value,
            float minimum,
            float maximum,
            Action<float> onChanged,
            Func<float, string> format,
            float rowWidth = 570f,
            bool interactable = true)
        {
            float rowX = RowOffset(parent, rowWidth);
            AddRowSurface(parent, name + "Row", y, rowWidth);
            factory.Text(name + "Label", parent, label, rowX + 10f, y, rowWidth * 0.46f, 32f, 13, interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text valueText = factory.Text(name + "Value", parent, format(value), rowX + rowWidth - 64f, y, 54f, 32f, 11, interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled, TextAnchor.MiddleRight);
            float normalized = Mathf.InverseLerp(minimum, maximum, value);
            factory.Slider(
                name + "Slider",
                parent,
                rowX + rowWidth * 0.49f,
                y + 5f,
                rowWidth * 0.38f,
                normalized,
                sliderValue =>
                {
                    float mapped = Mathf.Lerp(minimum, maximum, sliderValue);
                    valueText.text = format(mapped);
                    onChanged?.Invoke(mapped);
                },
                interactable);
        }

        private void AddRowSurface(Transform parent, string name, float y, float rowWidth)
        {
            GameObject row = factory.CreateObject(name, parent);
            factory.Place(row, RowOffset(parent, rowWidth), y, rowWidth, 32f);
            factory.AddSurface(row, UiThemeTokens.RowAlternate);
        }

        private static float RowOffset(Transform parent, float rowWidth)
        {
            RectTransform rect = parent as RectTransform;
            float parentWidth = rect == null ? rowWidth : rect.sizeDelta.x;
            return Mathf.Max(0f, (parentWidth - rowWidth) * 0.5f);
        }

        private string CapabilityText(UiCapabilityId capability)
        {
            UiCapabilityAvailability availability = capabilities.Get(capability).Availability;
            switch (availability)
            {
                case UiCapabilityAvailability.AdapterPending:
                    return textCatalog.Get("ui.common.adapter_pending");
                case UiCapabilityAvailability.ReferencePending:
                    return textCatalog.Get("ui.common.reference_pending");
                case UiCapabilityAvailability.DevelopmentOnly:
                    return textCatalog.Get("ui.common.development_only");
                default:
                    return textCatalog.Get("ui.common.unavailable");
            }
        }

        private void AddAudioSliderRow(
            Transform parent,
            string name,
            string label,
            float y,
            float value,
            Action<float> onChanged)
        {
            GameObject row = factory.CreateObject(name + "Row", parent);
            factory.Place(row, 12f, y, 654f, 40f);
            factory.AddSurface(row, UiThemeTokens.RowAlternate);
            factory.Text(name + "Label", row.transform, label, 12f, 0f, 244f, 40f, 12, UiThemeTokens.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text valueText = factory.Text(name + "Value", row.transform, PercentText(value), 574f, 0f, 64f, 40f, 12, UiThemeTokens.TextPrimary, TextAnchor.MiddleRight);
            factory.Slider(name + "Slider", row.transform, 278f, 9f, 276f, value, sliderValue =>
            {
                valueText.text = PercentText(sliderValue);
                onChanged?.Invoke(sliderValue);
            });
        }

        private void AddUnavailableAudioRow(
            Transform parent,
            string name,
            string label,
            float y,
            UiCapabilityId capability)
        {
            GameObject row = factory.CreateObject(name + "Row", parent);
            factory.Place(row, 12f, y, 654f, 40f);
            factory.AddSurface(row, UiThemeTokens.RowAlternate);
            factory.Text(name + "Label", row.transform, label, 12f, 0f, 244f, 40f, 12, UiThemeTokens.Disabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            factory.Text(name + "State", row.transform, CapabilityText(capability), 278f, 0f, 360f, 40f, 10, UiThemeTokens.Disabled, TextAnchor.MiddleRight, FontStyle.Bold);
        }

        private void BuildAudioProfileCard(Transform parent)
        {
            GameObject card = factory.GlassPanel("AudioProfileCard", parent, 1200f, 489f, 391f, 224f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Heading(card.transform, textCatalog.Get("ui.audio.profile"), 22f, 14f, 347f, 20);
            string backendId = dependencies.Audio == null
                ? textCatalog.Get("ui.common.unavailable")
                : dependencies.Audio.BackendId;
            string backendState = dependencies.Audio != null && dependencies.Audio.IsReady
                ? textCatalog.Get("ui.audio.backend_ready")
                : textCatalog.Get("ui.audio.backend_unavailable");
            factory.Text("Backend", card.transform, textCatalog.Get("ui.audio.backend") + "\n" + textCatalog.Get("ui.audio.state"), 22f, 58f, 126f, 56f, 11, UiThemeTokens.TextMuted, TextAnchor.UpperLeft);
            factory.Text("BackendValue", card.transform, backendId + "\n" + backendState, 152f, 58f, 217f, 56f, 11, UiThemeTokens.TextPrimary, TextAnchor.UpperRight, FontStyle.Bold);
            factory.Divider(card.transform, 22f, 126f, 347f);
            factory.Text("Tip", card.transform, textCatalog.Get("ui.audio.profile_tip"), 22f, 143f, 347f, 62f, 11, UiThemeTokens.TextMuted, TextAnchor.UpperLeft);
        }

        private void BuildMouseSettingsCard(Transform parent, ControlsSettingsDto pending)
        {
            GameObject card = factory.GlassPanel("MouseSettings", parent, 1027f, 121f, 538f, 174f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Heading(card.transform, textCatalog.Get("ui.controls.mouse"), 26f, 12f, 486f, 20);
            AddRangeSliderRow(card.transform, "MouseSensitivity", textCatalog.Get("ui.controls.look_sensitivity"), 54f, pending.MouseSensitivity, 0.05f, 3f, value =>
            {
                settings.EditPending(document => document.Controls.MouseSensitivity = value);
            }, LocalizedDecimal, 486f);
            AddToggleRow(card.transform, "InvertMouse", textCatalog.Get("ui.controls.invert_y"), 111f, pending.InvertMouseY, value =>
            {
                settings.EditPending(document => document.Controls.InvertMouseY = value);
            }, 486f);
        }

        private void BuildGamepadSettingsCard(Transform parent, ControlsSettingsDto pending)
        {
            GameObject card = factory.GlassPanel("GamepadSettings", parent, 1027f, 311f, 538f, 282f, UiGlassKind.MenuTinted, CurrentMenuGlassTint);
            factory.Heading(card.transform, textCatalog.Get("ui.controls.gamepad"), 26f, 12f, 486f, 20);
            AddRangeSliderRow(card.transform, "GamepadSensitivity", textCatalog.Get("ui.controls.gamepad_sensitivity"), 50f, pending.GamepadSensitivity, 0.05f, 3f, value =>
            {
                settings.EditPending(document => document.Controls.GamepadSensitivity = value);
            }, LocalizedDecimal, 486f);
            AddRangeSliderRow(card.transform, "GamepadDeadzone", textCatalog.Get("ui.controls.deadzone"), 96f, pending.GamepadDeadzone, 0f, 0.95f, value =>
            {
                settings.EditPending(document => document.Controls.GamepadDeadzone = value);
            }, LocalizedDecimal, 486f);
            AddToggleRow(card.transform, "Vibration", textCatalog.Get("ui.controls.vibration"), 146f, pending.VibrationEnabled, null, 486f, false, CapabilityText(UiCapabilityId.GamepadVibrationAdapter));
            AddReadOnlyRow(card.transform, "ControllerLayout", textCatalog.Get("ui.controls.controller_layout"), 196f, textCatalog.Get("ui.controls.default_rebind"), 486f);
            AddReadOnlyRow(card.transform, "InputMode", textCatalog.Get("ui.controls.input_mode"), 236f, textCatalog.Get("ui.controls.auto_detect"), 486f);
        }

        private List<InputBindingRow> CreateInputBindingRows()
        {
            return new List<InputBindingRow>
            {
                InputBindingRow.Available("ui.controls.steer_left", dependencies.VehicleActions, "66bfd821-5205-47af-ad3b-578ae1e93ba2", "2b4f8211-deb2-4f27-bd23-23ab95238cf1", "c219e42d-9e3d-4282-bcb0-8ac45244f7dd"),
                InputBindingRow.Available("ui.controls.steer_right", dependencies.VehicleActions, "66bfd821-5205-47af-ad3b-578ae1e93ba2", "db12261e-10f5-46f8-b003-45076b736483", "43d60047-3e50-4a90-a082-e9b94740c0aa"),
                InputBindingRow.Available("ui.controls.throttle", dependencies.VehicleActions, "29de3953-39c3-4528-9da1-5c324cc22c81", "eac5f56e-9c1a-43fb-a47b-fe51456cdb4b", "e232e352-ce5f-4fc0-83a4-e2372695adcd"),
                InputBindingRow.Available("ui.controls.brake", dependencies.VehicleActions, "9f849c9f-c33c-4973-8181-c758bf841ea3", "16b60199-9620-472c-9f5e-242e39fd9ff8", "cbbbc136-7a4f-452c-bb89-c58cfc28262b"),
                InputBindingRow.Available("ui.controls.clutch", dependencies.VehicleActions, "e0473ac2-e385-437c-9587-4af8dcff791a", "e91e6daa-3ec6-4cb6-abe9-8f17a5bb611b", null),
                InputBindingRow.Available("ui.controls.gear_up", dependencies.VehicleActions, "1723286a-a28c-4094-a5ff-a02510b4f6e5", "f895c189-47cd-4892-b705-3bbc66d53b69", null),
                InputBindingRow.Available("ui.controls.gear_down", dependencies.VehicleActions, "c87b57c5-4dd2-4220-8a31-22b821a14f4e", "8ccb00c8-714b-4d8d-b5e7-14de7fd5ae08", null),
                InputBindingRow.Available("ui.controls.ignition", dependencies.VehicleActions, "0ded8771-2738-471f-8d69-cfa80d83afa5", "9b1ff1b2-4327-4b5b-88d0-2519d0999d40", null),
                InputBindingRow.Missing("ui.controls.handbrake", UiCapabilityId.InputHandbrakeAction),
                InputBindingRow.Available("ui.controls.interact", dependencies.PlayerActions, "cf684f16-2e5e-4c3a-a5d7-73ae2cb61914", "9dd4ca49-0bb4-4fca-b1c1-c43e6fa489ea", null),
                InputBindingRow.Missing("ui.controls.inventory", UiCapabilityId.InputInventoryAction),
                InputBindingRow.Missing("ui.controls.map", UiCapabilityId.InputMapAction),
                InputBindingRow.Missing("ui.controls.journal", UiCapabilityId.InputJournalAction),
                InputBindingRow.Available("ui.controls.pause", dependencies.PlayerActions, "bfc0c808-99f3-46dc-96d6-9475fc19cb55", "aa4ef7e7-4547-4daf-9d75-a6f8dadfa74b", null),
            };
        }

        private void BuildInputBindingRow(Transform parent, InputBindingRow definition, int index, float y)
        {
            GameObject row = factory.CreateObject("BindingRow" + index, parent);
            factory.Place(row, 26f, y, 591f, 35f);
            factory.AddSurface(row, index % 2 == 0 ? UiThemeTokens.Row : UiThemeTokens.RowAlternate);
            bool actionAvailable = TryResolveBinding(definition.Asset, definition.ActionId, definition.PrimaryBindingId, out InputAction primaryAction, out int primaryIndex);
            Color labelColor = actionAvailable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled;
            factory.Text("Action", row.transform, textCatalog.Get(definition.LabelKey), 10f, 0f, 244f, 35f, 13, labelColor, TextAnchor.MiddleLeft);

            string primaryLabel = actionAvailable
                ? BindingDisplayString(primaryAction, primaryIndex)
                : CapabilityText(definition.MissingCapability);
            Button primary = factory.CompactButton("Primary", row.transform, primaryLabel, 254f, 3f, 147f, 29f, null, interactable: actionAvailable);
            if (actionAvailable)
            {
                primary.onClick.AddListener(() => BeginInteractiveRebind(definition.Asset, definition.ActionId, definition.PrimaryBindingId, primary));
            }

            bool secondaryAvailable = TryResolveBinding(definition.Asset, definition.ActionId, definition.SecondaryBindingId, out InputAction secondaryAction, out int secondaryIndex);
            string secondaryLabel = secondaryAvailable
                ? BindingDisplayString(secondaryAction, secondaryIndex)
                : textCatalog.Get("ui.controls.none");
            Button secondary = factory.CompactButton("Secondary", row.transform, secondaryLabel, 411f, 3f, 170f, 29f, null, interactable: secondaryAvailable);
            if (secondaryAvailable)
            {
                secondary.onClick.AddListener(() => BeginInteractiveRebind(definition.Asset, definition.ActionId, definition.SecondaryBindingId, secondary));
            }
        }

        private void BeginInteractiveRebind(
            InputActionAsset asset,
            Guid actionId,
            Guid bindingId,
            Button sourceButton)
        {
            CancelActiveRebind();
            if (!TryResolveBinding(asset, actionId, bindingId, out InputAction action, out int bindingIndex))
            {
                ShowNotice("ui.common.unavailable");
                return;
            }

            InputBinding binding = action.bindings[bindingIndex];
            activeRebindAction = action;
            activeRebindBindingIndex = bindingIndex;
            activeRebindPreviousOverridePath = binding.overridePath;
            activeRebindActionWasEnabled = action.enabled;
            activeRebindPauseWasEnabled = pauseAction != null && pauseAction != action && pauseAction.enabled;
            if (activeRebindActionWasEnabled)
            {
                action.Disable();
            }

            if (activeRebindPauseWasEnabled)
            {
                pauseAction.Disable();
            }

            Text label = sourceButton == null ? null : sourceButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = textCatalog.Get("ui.controls.rebinding");
            }

            InputActionRebindingExtensions.RebindingOperation operation = action
                .PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .OnCancel(_ => FinishInteractiveRebind(accepted: false))
                .OnComplete(_ => FinishInteractiveRebind(accepted: true));

            if (BindingUsesGroup(binding.groups, "Keyboard&Mouse"))
            {
                operation.WithControlsExcluding("<Gamepad>");
            }
            else if (BindingUsesGroup(binding.groups, "Gamepad"))
            {
                operation.WithControlsExcluding("<Keyboard>");
                operation.WithControlsExcluding("<Mouse>");
            }

            activeRebindOperation = operation;
            operation.Start();
        }

        private void FinishInteractiveRebind(bool accepted)
        {
            InputAction action = activeRebindAction;
            int bindingIndex = activeRebindBindingIndex;
            string previousOverridePath = activeRebindPreviousOverridePath;
            bool actionWasEnabled = activeRebindActionWasEnabled;
            bool pauseWasEnabled = activeRebindPauseWasEnabled;
            InputActionRebindingExtensions.RebindingOperation operation = activeRebindOperation;

            activeRebindOperation = null;
            activeRebindAction = null;
            activeRebindBindingIndex = -1;
            activeRebindPreviousOverridePath = null;
            activeRebindActionWasEnabled = false;
            activeRebindPauseWasEnabled = false;
            operation?.Dispose();

            bool conflict = accepted && action != null && HasBindingConflict(action, bindingIndex);
            if ((!accepted || conflict) && action != null && bindingIndex >= 0)
            {
                if (string.IsNullOrEmpty(previousOverridePath))
                {
                    action.RemoveBindingOverride(bindingIndex);
                }
                else
                {
                    action.ApplyBindingOverride(bindingIndex, previousOverridePath);
                }
            }

            if (actionWasEnabled && action != null)
            {
                action.Enable();
            }

            if (pauseWasEnabled && pauseAction != null && pauseAction != action)
            {
                pauseAction.Enable();
            }

            if (accepted && !conflict)
            {
                CaptureBindingOverridesIntoPending();
            }
            else if (conflict)
            {
                ShowNotice("ui.controls.conflict");
            }

            if (!suppressRebindRouteRebuild && routes.ContainsKey(UiRouteId.SettingsControls))
            {
                RebuildRoute(UiRouteId.SettingsControls, BuildControlsRoute);
            }
        }

        private void CancelActiveRebind()
        {
            activeRebindOperation?.Cancel();
        }

        private void DisposeActiveRebindForShutdown()
        {
            suppressRebindRouteRebuild = true;
            try
            {
                CancelActiveRebind();
            }
            finally
            {
                suppressRebindRouteRebuild = false;
            }
        }

        private bool HasBindingConflict(InputAction changedAction, int changedBindingIndex)
        {
            if (changedAction == null || changedBindingIndex < 0 || changedBindingIndex >= changedAction.bindings.Count)
            {
                return false;
            }

            InputBinding changed = changedAction.bindings[changedBindingIndex];
            string changedPath = changed.effectivePath;
            if (string.IsNullOrWhiteSpace(changedPath))
            {
                return false;
            }

            InputActionAsset[] assets = { dependencies.PlayerActions, dependencies.VehicleActions };
            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                InputActionAsset asset = assets[assetIndex];
                if (asset == null)
                {
                    continue;
                }

                foreach (InputActionMap map in asset.actionMaps)
                {
                    foreach (InputAction candidateAction in map.actions)
                    {
                        for (int bindingIndex = 0; bindingIndex < candidateAction.bindings.Count; bindingIndex++)
                        {
                            InputBinding candidate = candidateAction.bindings[bindingIndex];
                            if (candidate.isComposite ||
                                candidate.id == changed.id ||
                                !BindingGroupsOverlap(changed.groups, candidate.groups))
                            {
                                continue;
                            }

                            if (string.Equals(changedPath, candidate.effectivePath, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool BindingGroupsOverlap(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return true;
            }

            string[] leftGroups = left.Split(';');
            string[] rightGroups = right.Split(';');
            for (int leftIndex = 0; leftIndex < leftGroups.Length; leftIndex++)
            {
                for (int rightIndex = 0; rightIndex < rightGroups.Length; rightIndex++)
                {
                    if (string.Equals(leftGroups[leftIndex].Trim(), rightGroups[rightIndex].Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool BindingUsesGroup(string groups, string expected)
        {
            if (string.IsNullOrWhiteSpace(groups))
            {
                return false;
            }

            string[] values = groups.Split(';');
            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index].Trim(), expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveBinding(
            InputActionAsset asset,
            Guid actionId,
            Guid bindingId,
            out InputAction action,
            out int bindingIndex)
        {
            action = null;
            bindingIndex = -1;
            if (asset == null || actionId == Guid.Empty || bindingId == Guid.Empty)
            {
                return false;
            }

            action = asset.FindAction(actionId.ToString("D"), throwIfNotFound: false);
            if (action == null)
            {
                return false;
            }

            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (action.bindings[index].id == bindingId)
                {
                    bindingIndex = index;
                    return true;
                }
            }

            action = null;
            return false;
        }

        private string BindingDisplayString(InputAction action, int bindingIndex)
        {
            string effectivePath = action.bindings[bindingIndex].effectivePath;
            string physicalKeyboardLabel = PhysicalKeyboardDisplayString(effectivePath);
            if (!string.IsNullOrEmpty(physicalKeyboardLabel))
            {
                return physicalKeyboardLabel;
            }

            string value = action.GetBindingDisplayString(bindingIndex);
            return string.IsNullOrWhiteSpace(value) ? textCatalog.Get("ui.controls.none") : value;
        }

        private string PhysicalKeyboardDisplayString(string effectivePath)
        {
            const string keyboardPrefix = "<Keyboard>/";
            if (string.IsNullOrWhiteSpace(effectivePath) ||
                !effectivePath.StartsWith(keyboardPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string control = effectivePath.Substring(keyboardPrefix.Length);
            if (control.Length == 1)
            {
                return control.ToUpperInvariant();
            }

            switch (control)
            {
                case "escape": return textCatalog.Get("ui.controls.key.escape");
                case "space": return textCatalog.Get("ui.controls.key.space");
                case "leftShift":
                case "rightShift": return textCatalog.Get("ui.controls.key.shift");
                case "leftCtrl":
                case "rightCtrl": return textCatalog.Get("ui.controls.key.control");
                case "enter": return textCatalog.Get("ui.controls.key.enter");
                case "backspace": return textCatalog.Get("ui.controls.key.backspace");
                case "leftArrow": return textCatalog.Get("ui.controls.key.left");
                case "rightArrow": return textCatalog.Get("ui.controls.key.right");
                case "upArrow": return textCatalog.Get("ui.controls.key.up");
                case "downArrow": return textCatalog.Get("ui.controls.key.down");
                case "pageUp": return textCatalog.Get("ui.controls.key.page_up");
                case "pageDown": return textCatalog.Get("ui.controls.key.page_down");
                default: return control;
            }
        }

        private void CaptureBindingOverridesIntoPending()
        {
            settings.EditPending(document =>
            {
                document.Controls.PlayerBindingOverridesJson = dependencies.PlayerActions?.SaveBindingOverridesAsJson() ?? string.Empty;
                document.Controls.VehicleBindingOverridesJson = dependencies.VehicleActions?.SaveBindingOverridesAsJson() ?? string.Empty;
            });
        }

        private void BuildStandardSettingsActions(Transform parent)
        {
            const float x = 1151f;
            const float y = 884f;
            const float gap = UiThemeTokens.SpacingCompact;
            float secondX = UiThemeTokens.UtilityActionFirstWidth + gap;
            float thirdX = secondX + UiThemeTokens.UtilityActionSecondWidth + gap;

            GameObject actions = factory.CreateObject("SettingsActions", parent);
            factory.Place(actions, x, y, 455f, UiThemeTokens.UtilityActionButtonHeight);
            factory.CompactButton("Apply", actions.transform, textCatalog.Get("ui.common.apply"), 0f, 0f, UiThemeTokens.UtilityActionFirstWidth, UiThemeTokens.UtilityActionButtonHeight, ApplySettingsWithBindings, primary: true);
            factory.CompactButton("Reset", actions.transform, textCatalog.Get("ui.common.reset"), secondX, 0f, UiThemeTokens.UtilityActionSecondWidth, UiThemeTokens.UtilityActionButtonHeight, ResetAllPendingSettings, glass: true);
            factory.CompactButton("Cancel", actions.transform, textCatalog.Get("ui.common.cancel"), thirdX, 0f, UiThemeTokens.UtilityActionThirdWidth, UiThemeTokens.UtilityActionButtonHeight, RevertAllPendingSettings, glass: true);
        }

        private void ApplySettingsWithBindings()
        {
            CancelActiveRebind();
            CaptureBindingOverridesIntoPending();
            ApplyPendingSettings();
        }

        private void ResetAllPendingSettings()
        {
            CancelActiveRebind();
            dependencies.PlayerActions?.RemoveAllBindingOverrides();
            dependencies.VehicleActions?.RemoveAllBindingOverrides();
            ResetPendingSettings();
        }

        private void RevertAllPendingSettings()
        {
            CancelActiveRebind();
            RestoreAppliedBindingOverrides();
            RevertPendingSettings();
        }

        private void ReturnFromSettingsWithBindings()
        {
            CancelActiveRebind();
            RestoreAppliedBindingOverrides();
            ReturnFromSettings();
        }

        private void RestoreAppliedBindingOverrides()
        {
            dependencies.PlayerActions?.RemoveAllBindingOverrides();
            dependencies.VehicleActions?.RemoveAllBindingOverrides();
            ControlsSettingsDto applied = settings.Applied.Controls;
            TryLoadOverrides(dependencies.PlayerActions, applied.PlayerBindingOverridesJson);
            TryLoadOverrides(dependencies.VehicleActions, applied.VehicleBindingOverridesJson);
        }

        private string GraphicsDisplayModeText(UiDisplayMode mode)
        {
            switch (mode)
            {
                case UiDisplayMode.Windowed:
                    return textCatalog.Get("ui.value.display.windowed");
                case UiDisplayMode.ExclusiveFullscreen:
                    return textCatalog.Get("ui.value.display.exclusive");
                default:
                    return textCatalog.Get("ui.value.display.borderless");
            }
        }

        private string DynamicRangeText(UiDynamicRangeMode mode)
        {
            switch (mode)
            {
                case UiDynamicRangeMode.Night:
                    return textCatalog.Get("ui.value.dynamic.night");
                case UiDynamicRangeMode.Wide:
                    return textCatalog.Get("ui.value.dynamic.wide");
                default:
                    return textCatalog.Get("ui.value.dynamic.standard");
            }
        }

        private string HudModeText(UiHudMode mode)
        {
            switch (mode)
            {
                case UiHudMode.Off:
                    return textCatalog.Get("ui.value.hud.off");
                case UiHudMode.Contextual:
                    return textCatalog.Get("ui.value.hud.contextual");
                default:
                    return textCatalog.Get("ui.value.hud.full");
            }
        }

        private string UnitSystemText(UiUnitSystem units)
        {
            return units == UiUnitSystem.Imperial
                ? textCatalog.Get("ui.value.units.imperial")
                : textCatalog.Get("ui.value.units.metric");
        }

        private string LanguageText(string localeId)
        {
            return localeId != null && localeId.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                ? textCatalog.Get("ui.value.language.russian")
                : textCatalog.Get("ui.value.language.english");
        }

        private static string ResolutionText(int width, int height)
        {
            return width.ToString(CultureInfo.InvariantCulture) + " × " + height.ToString(CultureInfo.InvariantCulture);
        }

        private static string RefreshRateText(int numerator, int denominator)
        {
            return RoundedRefreshRate(numerator, denominator).ToString(CultureInfo.InvariantCulture) + " Hz";
        }

        private static string PercentText(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private string QualityText(int level, string[] names)
        {
            if (names == null || names.Length == 0 || level < 0 || level >= names.Length)
            {
                return textCatalog.Get("ui.common.unavailable");
            }

            switch (names[level])
            {
                case "High Fidelity":
                    return textCatalog.Get("ui.graphics.quality.high_fidelity");
                case "Balanced":
                    return textCatalog.Get("ui.graphics.quality.balanced");
                case "Performant":
                    return textCatalog.Get("ui.graphics.quality.performant");
                default:
                    return names[level].ToUpperInvariant();
            }
        }

        private string LocalizedDecimal(float value)
        {
            return GetLocaleFormatter().Format("{0:0.00}", value);
        }

        private static int RoundedRefreshRate(int numerator, int denominator)
        {
            return Mathf.Max(1, Mathf.RoundToInt(numerator / (float)Mathf.Max(1, denominator)));
        }

        private static int WrapIndex(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }

        private static int IndexOfResolution(List<Vector2Int> values, int width, int height)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index].x == width && values[index].y == height)
                {
                    return index;
                }
            }

            return -1;
        }

        private static List<Vector2Int> CollectResolutionChoices(GraphicsSettingsDto current)
        {
            var result = new List<Vector2Int>();
            AddResolution(result, current.ResolutionWidth, current.ResolutionHeight);
            Resolution[] available = Screen.resolutions;
            for (int index = 0; index < available.Length; index++)
            {
                AddResolution(result, available[index].width, available[index].height);
            }

            AddResolution(result, 1280, 720);
            AddResolution(result, 1920, 1080);
            AddResolution(result, 2560, 1440);
            result.Sort((left, right) =>
            {
                int pixelComparison = (left.x * left.y).CompareTo(right.x * right.y);
                return pixelComparison != 0 ? pixelComparison : left.x.CompareTo(right.x);
            });
            return result;
        }

        private static void AddResolution(List<Vector2Int> values, int width, int height)
        {
            if (width < 640 || height < 480 || IndexOfResolution(values, width, height) >= 0)
            {
                return;
            }

            values.Add(new Vector2Int(width, height));
        }

        private static List<int> CollectRefreshRateChoices(GraphicsSettingsDto current)
        {
            var result = new List<int>();
            AddUnique(result, RoundedRefreshRate(current.RefreshRateNumerator, current.RefreshRateDenominator));
            Resolution[] available = Screen.resolutions;
            for (int index = 0; index < available.Length; index++)
            {
                if (available[index].width == current.ResolutionWidth && available[index].height == current.ResolutionHeight)
                {
                    int hz = Mathf.RoundToInt((float)available[index].refreshRateRatio.value);
                    AddUnique(result, hz);
                }
            }

            AddUnique(result, 60);
            result.Sort();
            return result;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (value > 0 && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private readonly struct InputBindingRow
        {
            private InputBindingRow(
                string labelKey,
                InputActionAsset asset,
                Guid actionId,
                Guid primaryBindingId,
                Guid secondaryBindingId,
                UiCapabilityId missingCapability)
            {
                LabelKey = labelKey;
                Asset = asset;
                ActionId = actionId;
                PrimaryBindingId = primaryBindingId;
                SecondaryBindingId = secondaryBindingId;
                MissingCapability = missingCapability;
            }

            public string LabelKey { get; }

            public InputActionAsset Asset { get; }

            public Guid ActionId { get; }

            public Guid PrimaryBindingId { get; }

            public Guid SecondaryBindingId { get; }

            public UiCapabilityId MissingCapability { get; }

            public static InputBindingRow Available(
                string labelKey,
                InputActionAsset asset,
                string actionId,
                string primaryBindingId,
                string secondaryBindingId)
            {
                return new InputBindingRow(
                    labelKey,
                    asset,
                    Guid.Parse(actionId),
                    Guid.Parse(primaryBindingId),
                    string.IsNullOrEmpty(secondaryBindingId) ? Guid.Empty : Guid.Parse(secondaryBindingId),
                    UiCapabilityId.SaveStorage);
            }

            public static InputBindingRow Missing(string labelKey, UiCapabilityId capability)
            {
                return new InputBindingRow(labelKey, null, Guid.Empty, Guid.Empty, Guid.Empty, capability);
            }
        }
    }
}
