using System;
using System.Collections.Generic;

namespace MSC.UI.Runtime.Routing
{
    public enum UiRouteId
    {
        Boot = 0,
        Loading = 1,
        MainMenu = 2,
        SettingsGraphics = 3,
        SettingsAudio = 4,
        SettingsControls = 5,
        SettingsGameplay = 6,
        SettingsAccessibility = 7,
        SettingsMods = 8,
        Pause = 9,
        ConfirmationDialog = 10,
        SaveStatus = 11,
        InGameHud = 12,
    }

    public readonly struct UiRouteDefinition
    {
        public UiRouteDefinition(
            UiRouteId id,
            string localizationKey,
            bool hasLockedVisualReference,
            bool overlaysGameplay)
        {
            if (string.IsNullOrWhiteSpace(localizationKey))
            {
                throw new ArgumentException("A route localization key is required.", nameof(localizationKey));
            }

            Id = id;
            LocalizationKey = localizationKey;
            HasLockedVisualReference = hasLockedVisualReference;
            OverlaysGameplay = overlaysGameplay;
        }

        public UiRouteId Id { get; }

        public string LocalizationKey { get; }

        public bool HasLockedVisualReference { get; }

        public bool OverlaysGameplay { get; }
    }

    public static class UiRouteCatalog
    {
        private static readonly UiRouteDefinition[] Definitions =
        {
            new UiRouteDefinition(UiRouteId.Boot, "ui.route.boot", false, false),
            new UiRouteDefinition(UiRouteId.Loading, "ui.route.loading", false, false),
            new UiRouteDefinition(UiRouteId.MainMenu, "ui.route.main_menu", true, false),
            new UiRouteDefinition(UiRouteId.SettingsGraphics, "ui.route.settings.graphics", true, false),
            new UiRouteDefinition(UiRouteId.SettingsAudio, "ui.route.settings.audio", true, false),
            new UiRouteDefinition(UiRouteId.SettingsControls, "ui.route.settings.controls", true, false),
            new UiRouteDefinition(UiRouteId.SettingsGameplay, "ui.route.settings.gameplay", true, false),
            new UiRouteDefinition(UiRouteId.SettingsAccessibility, "ui.route.settings.accessibility", false, false),
            new UiRouteDefinition(UiRouteId.SettingsMods, "ui.route.settings.mods", false, false),
            new UiRouteDefinition(UiRouteId.Pause, "ui.route.pause", false, true),
            new UiRouteDefinition(UiRouteId.ConfirmationDialog, "ui.route.confirmation", false, true),
            new UiRouteDefinition(UiRouteId.SaveStatus, "ui.route.save_status", false, true),
            new UiRouteDefinition(UiRouteId.InGameHud, "ui.route.hud", true, true),
        };

        public static IReadOnlyList<UiRouteDefinition> All => Definitions;

        public static UiRouteDefinition Get(UiRouteId id)
        {
            for (int index = 0; index < Definitions.Length; index++)
            {
                if (Definitions[index].Id == id)
                {
                    return Definitions[index];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown UI route.");
        }
    }
}
