using UnityEngine;

namespace MSC.UI.Presentation
{
    public static class UiThemeTokens
    {
        public const float ReferenceWidth = 1672f;
        public const float ReferenceHeight = 941f;
        public const float ReferenceAspect = ReferenceWidth / ReferenceHeight;

        public static readonly Color MenuBackdropFallback = new Color(0.018f, 0.015f, 0.013f, 1f);
        public static readonly Color PauseBackdropDim = new Color(0.006f, 0.005f, 0.004f, 0.74f);
        public static readonly Color MenuGlassNeutral = new Color(0.055f, 0.050f, 0.045f, 0.60f);
        public static readonly Color HudGlassTint = new Color(0.008f, 0.009f, 0.009f, 0.70f);
        public static readonly Color Panel = new Color(0.055f, 0.047f, 0.04f, 0.88f);
        public static readonly Color Card = new Color(0.08f, 0.07f, 0.06f, 0.78f);
        public static readonly Color Row = new Color(0.15f, 0.13f, 0.115f, 0.72f);
        public static readonly Color RowAlternate = new Color(0.11f, 0.10f, 0.09f, 0.72f);
        public static readonly Color Border = new Color(0.72f, 0.68f, 0.62f, 0.26f);
        public const float EmphasizedBorderAlpha = 0.66f;
        public static readonly Color Accent = new Color32(245, 160, 25, 255);
        public static readonly Color AccentSoft = new Color(0.96f, 0.57f, 0.08f, 0.18f);
        public static readonly Color Destructive = new Color32(241, 84, 64, 255);
        public static readonly Color TextPrimary = new Color32(245, 241, 232, 255);
        public static readonly Color TextMuted = new Color32(168, 163, 157, 255);
        public static readonly Color Disabled = new Color(0.62f, 0.60f, 0.57f, 0.76f);
        public static readonly Color Focus = new Color32(255, 174, 35, 255);
        public static readonly Color Positive = new Color32(108, 210, 64, 255);
        public static readonly Color ControlNormal = Color.white;
        public static readonly Color ActionHighlighted = new Color(0.72f, 0.66f, 0.58f, 1f);
        public static readonly Color ActionPressed = new Color(0.56f, 0.49f, 0.41f, 1f);
        public static readonly Color ActionSelected = new Color(0.42f, 0.36f, 0.30f, 1f);
        public static readonly Color CompactHighlighted = new Color(1f, 0.79f, 0.48f, 1f);
        public static readonly Color CompactPressed = new Color(0.78f, 0.52f, 0.22f, 1f);
        public static readonly Color CompactSelected = new Color(1f, 0.74f, 0.34f, 1f);
        public static readonly Color ControlDisabled = new Color(0.52f, 0.50f, 0.48f, 0.6f);
        public static readonly Color PrimaryControlForeground = new Color(0.12f, 0.08f, 0.03f, 1f);
        public static readonly Color SliderTrack = new Color(0.15f, 0.14f, 0.13f, 0.96f);
        public static readonly Color SurvivalThirst = new Color32(0, 155, 255, 255);
        public static readonly Color SurvivalHunger = new Color32(225, 115, 0, 255);
        public static readonly Color SurvivalStress = new Color32(252, 187, 0, 255);
        public static readonly Color SurvivalUrine = new Color32(255, 207, 6, 255);
        public static readonly Color SurvivalFatigue = new Color32(112, 193, 111, 255);
        public static readonly Color SurvivalDirtiness = new Color32(246, 245, 243, 255);

        // Reference-scale spacing. Exact screen placement remains in the locked
        // layouts; reusable widgets consume this shared rhythm.
        public const float SpacingXSmall = 4f;
        public const float SpacingSmall = 8f;
        public const float SpacingCompact = 10f;
        public const float SpacingMedium = 12f;
        public const float SpacingStandard = 14f;
        public const float SpacingLarge = 18f;
        public const float SpacingXLarge = 24f;
        public const float SpacingXXLarge = 32f;

        // Procedural sprite radii are pixels in their source texture. They are
        // centralized here so surfaces keep one corner-radius family.
        public const int ProceduralSurfaceResolution = 32;
        public const int ProceduralTrackResolution = 24;
        public const int ProceduralSupersampleFactor = 4;
        public const int CornerRadiusSmallPixels = 7;
        public const int CornerRadiusMediumPixels = 12;
        public const int CornerRadiusCircularPixels = 16;
        public const float BorderStrokePixels = 1.75f;

        public const int ProceduralIconResolution = 32;
        public const float IconSizeCompact = 28f;
        public const float IconSizeStandard = 32f;

        public const float RowHeightDense = 22f;
        public const float RowHeightStandard = 28f;
        public const float RowHeightComfortable = 34f;
        public const float UtilityActionButtonHeight = 35f;
        public const float UtilityActionFirstWidth = 139f;
        public const float UtilityActionSecondWidth = 123f;
        public const float UtilityActionThirdWidth = 173f;

        public const float TransitionImmediateSeconds = 0f;
        public const float TransitionFastSeconds = 0.08f;
        public const float TransitionStandardSeconds = 0.16f;
        public const float TransitionSlowSeconds = 0.25f;

        public const int LogoPrimarySize = 44;
        public const int LogoSecondarySize = 22;
        public const int HeadingSize = 28;
        public const int SectionSize = 19;
        public const int ActionSize = 21;
        public const int RowSize = 15;
        public const int CaptionSize = 12;

        public static Color MenuGlassTint(Color selectedCarColour)
        {
            Color mutedVehicleColour = Color.Lerp(
                new Color(
                    MenuGlassNeutral.r,
                    MenuGlassNeutral.g,
                    MenuGlassNeutral.b,
                    1f),
                selectedCarColour,
                0.08f);
            mutedVehicleColour.a = MenuGlassNeutral.a;
            return mutedVehicleColour;
        }
    }
}
