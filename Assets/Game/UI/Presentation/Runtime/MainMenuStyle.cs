using UnityEngine;

namespace MSC.UI.Presentation
{
    /// <summary>Opt-in shared menu styling in the existing 1672 x 941 reference canvas.</summary>
    public static class MainMenuStyle
    {
        public const int CornerRadius = 18;
        public const float HoverDurationSeconds = 0.15f;
        public const float PressDurationSeconds = 0.09f;
        public const float AppearanceDurationSeconds = 0.32f;
        public const float HoverScale = 1.012f;
        public const float PressedScale = 0.988f;
        public const int TitleSize = 22;
        public const int HelperSize = 15;
        public const int CompactTitleSize = 14;

        public static readonly Color Surface = new Color(0.025f, 0.027f, 0.029f, 0.74f);
        public static readonly Color PrimaryText = new Color32(241, 241, 239, 255);
        public static Color TextPrimary => PrimaryText;
        public static readonly Color SecondaryText = new Color32(167, 167, 163, 255);
        public static readonly Color Border = new Color(0.94f, 0.94f, 0.92f, 0.12f);
        public static readonly Color HoverBorder = new Color(0.94f, 0.94f, 0.92f, 0.26f);
        public static readonly Color Accent = new Color32(255, 157, 0, 255);
        public static readonly Color Destructive = new Color32(255, 85, 69, 255);
        public static readonly Color Shadow = new Color(0.005f, 0.005f, 0.004f, 0.12f);
        public static readonly Color GreetingIcon = new Color32(243, 207, 33, 255);
    }
}
