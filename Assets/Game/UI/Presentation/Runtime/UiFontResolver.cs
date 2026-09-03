using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.UI.Presentation
{
    internal readonly struct UiFontResolution
    {
        public UiFontResolution(
            Font font,
            string sourceLabel,
            bool ownsFont,
            bool usesLegacyRuntime)
        {
            Font = font != null ? font : throw new ArgumentNullException(nameof(font));
            SourceLabel = sourceLabel ?? throw new ArgumentNullException(nameof(sourceLabel));
            OwnsFont = ownsFont;
            UsesLegacyRuntime = usesLegacyRuntime;
        }

        public Font Font { get; }

        public string SourceLabel { get; }

        public bool OwnsFont { get; }

        public bool UsesLegacyRuntime { get; }
    }

    /// <summary>
    /// Resolves the project-bundled Latin/Cyrillic UI font. The Windows font
    /// list remains a compatibility fallback for a missing or failed import.
    /// </summary>
    internal static class UiFontResolver
    {
        private const string ProjectFontResourcePath = "Fonts/HelveticaNeueRoman";

        private const string RequiredUiGlyphs =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        private static readonly string[] PreferredWindowsFamilies =
        {
            "Helvetica Neue",
            "Segoe UI",
            "Arial",
            "Bahnschrift",
        };

        public static UiFontResolution Resolve()
        {
            Font projectFont = Resources.Load<Font>(ProjectFontResourcePath);
            if (projectFont != null && SupportsRequiredUiGlyphs(projectFont))
            {
                return new UiFontResolution(
                    projectFont,
                    "Bundled font: Helvetica Neue Roman (user-supplied)",
                    ownsFont: false,
                    usesLegacyRuntime: false);
            }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            UiFontResolution windowsResolution;
            if (TryResolveWindowsFont(out windowsResolution))
            {
                return windowsResolution;
            }
#endif

            Font legacyRuntime = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyRuntime == null)
            {
                throw new InvalidOperationException(
                    "Neither a supported Windows UI font nor LegacyRuntime.ttf is available.");
            }

            return new UiFontResolution(
                legacyRuntime,
                "Unity built-in: LegacyRuntime.ttf",
                ownsFont: false,
                usesLegacyRuntime: true);
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static bool TryResolveWindowsFont(out UiFontResolution resolution)
        {
            resolution = default;
            string[] installedFamilies;
            try
            {
                installedFamilies = Font.GetOSInstalledFontNames();
            }
            catch (Exception)
            {
                return false;
            }

            var installed = new HashSet<string>(
                installedFamilies ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < PreferredWindowsFamilies.Length; index++)
            {
                string candidate = PreferredWindowsFamilies[index];
                if (!installed.Contains(candidate))
                {
                    continue;
                }

                Font font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(candidate, UiThemeTokens.RowSize);
                }
                catch (Exception)
                {
                    continue;
                }

                if (font == null)
                {
                    continue;
                }

                if (!SupportsRequiredUiGlyphs(font))
                {
                    ReleaseRejectedFont(font);
                    continue;
                }

                font.name = "M08A Windows UI Font (" + candidate + ")";
                font.hideFlags = HideFlags.HideAndDontSave;
                resolution = new UiFontResolution(
                    font,
                    "Windows OS font: " + candidate,
                    ownsFont: true,
                    usesLegacyRuntime: false);
                return true;
            }

            return false;
        }
#endif

        private static bool SupportsRequiredUiGlyphs(Font font)
        {
            for (int index = 0; index < RequiredUiGlyphs.Length; index++)
            {
                if (!font.HasCharacter(RequiredUiGlyphs[index]))
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static void ReleaseRejectedFont(Font font)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(font);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(font);
            }
        }
#endif
    }
}
