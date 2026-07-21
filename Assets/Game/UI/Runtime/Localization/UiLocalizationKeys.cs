using System.Collections.Generic;

namespace MSC.UI.Runtime.Localization
{
    public static class UiLocalizationKeys
    {
        public const string MainMenuContinue = "ui.main.continue";
        public const string MainMenuNewGame = "ui.main.new_game";
        public const string MainMenuLoadGame = "ui.main.load_game";
        public const string MainMenuCredits = "ui.main.credits";
        public const string MainMenuQuit = "ui.main.quit";
        public const string MainMenuSettings = "ui.main.settings";
        public const string MainMenuMods = "ui.main.mods";
        public const string MainMenuDeveloperTools = "ui.main.dev_tools";

        public const string SettingsGraphics = "ui.category.graphics";
        public const string SettingsAudio = "ui.category.audio";
        public const string SettingsControls = "ui.category.controls";
        public const string SettingsGameplay = "ui.category.gameplay";
        public const string SettingsAccessibility = "ui.category.accessibility";
        public const string SettingsMods = "ui.category.mods";
        public const string SettingsApply = "ui.common.apply";
        public const string SettingsReset = "ui.common.reset";
        public const string SettingsCancel = "ui.common.cancel";
        public const string SettingsDefaults = "ui.common.defaults";
        public const string SettingsRevert = "ui.common.revert";
        public const string SettingsBack = "ui.common.back";

        public const string HudTime = "ui.hud.time";
        public const string HudDay = "ui.hud.day";
        public const string HudDate = "ui.hud.date";
        public const string HudMoney = "ui.hud.money";
        public const string HudThirst = "ui.hud.thirst";
        public const string HudHunger = "ui.hud.hunger";
        public const string HudStress = "ui.hud.stress";
        public const string HudUrine = "ui.hud.urine";
        public const string HudFatigue = "ui.hud.fatigue";
        public const string HudDirtiness = "ui.hud.dirtiness";

        public const string StateUnavailable = "ui.common.unavailable";
        public const string StateDevelopmentOnly = "ui.common.development_only";
        public const string StateAdapterPending = "ui.common.adapter_pending";
        public const string StateReferencePending = "ui.common.reference_pending";

        private static readonly string[] KnownKeys =
        {
            MainMenuContinue,
            MainMenuNewGame,
            MainMenuLoadGame,
            MainMenuCredits,
            MainMenuQuit,
            MainMenuSettings,
            MainMenuMods,
            MainMenuDeveloperTools,
            SettingsGraphics,
            SettingsAudio,
            SettingsControls,
            SettingsGameplay,
            SettingsAccessibility,
            SettingsMods,
            SettingsApply,
            SettingsReset,
            SettingsCancel,
            SettingsDefaults,
            SettingsRevert,
            SettingsBack,
            HudTime,
            HudDay,
            HudDate,
            HudMoney,
            HudThirst,
            HudHunger,
            HudStress,
            HudUrine,
            HudFatigue,
            HudDirtiness,
            StateUnavailable,
            StateDevelopmentOnly,
            StateAdapterPending,
            StateReferencePending,
        };

        public static IReadOnlyList<string> All => KnownKeys;
    }
}
