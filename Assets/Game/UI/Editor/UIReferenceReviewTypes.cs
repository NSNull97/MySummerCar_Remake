using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public enum UIReferenceScreen
    {
        MainMenu,
        Graphics,
        Audio,
        Controls,
        Gameplay,
        Hud,
    }

    public enum UIReferenceDisplayMode
    {
        ReferenceOnly,
        ImplementationOnly,
        Blended,
    }

    public readonly struct UIReferenceScreenDescriptor
    {
        public UIReferenceScreenDescriptor(
            UIReferenceScreen screen,
            string role,
            string referenceFileName,
            string reviewStem)
        {
            Screen = screen;
            Role = role;
            ReferenceFileName = referenceFileName;
            ReviewStem = reviewStem;
        }

        public UIReferenceScreen Screen { get; }

        public string Role { get; }

        public string ReferenceFileName { get; }

        public string ReviewStem { get; }
    }

    public static class UIReferenceCatalog
    {
        public const int CanonicalWidth = 1672;
        public const int CanonicalHeight = 941;

        private static readonly UIReferenceScreenDescriptor[] Descriptors =
        {
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.MainMenu,
                "MainMenu",
                "01_MAIN_MENU_APPROVED.png",
                "MainMenu"),
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.Graphics,
                "GraphicsSettings",
                "02_GRAPHICS_SETTINGS_APPROVED.png",
                "Graphics"),
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.Audio,
                "AudioSettings",
                "03_AUDIO_SETTINGS_APPROVED.png",
                "Audio"),
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.Controls,
                "ControlsSettings",
                "04_CONTROLS_SETTINGS_APPROVED.png",
                "Controls"),
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.Gameplay,
                "GameplaySettings",
                "05_GAMEPLAY_SETTINGS_APPROVED.png",
                "Gameplay"),
            new UIReferenceScreenDescriptor(
                UIReferenceScreen.Hud,
                "DefaultInGameHUD",
                "06_INGAME_HUD_APPROVED.png",
                "HUD"),
        };

        public static IReadOnlyList<UIReferenceScreenDescriptor> All => Descriptors;

        public static UIReferenceScreenDescriptor Get(UIReferenceScreen screen)
        {
            for (var index = 0; index < Descriptors.Length; index++)
            {
                if (Descriptors[index].Screen == screen)
                {
                    return Descriptors[index];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(screen), screen, "Unknown UI reference screen.");
        }
    }

    public static class UIReferencePaths
    {
        public const string ReferenceDirectoryRelative = "References/UI/Approved/08A";
        public const string ManifestFileName = "REFERENCE_MANIFEST.json";
        public const string ReviewDirectoryRelative = "Docs/UI/Review/08A";

        public static string GetProjectRoot()
        {
            var assetsPath = Path.GetFullPath(Application.dataPath);
            var assetsDirectory = new DirectoryInfo(assetsPath);
            if (assetsDirectory.Parent == null)
            {
                throw new InvalidOperationException($"Cannot resolve project root from '{assetsPath}'.");
            }

            return assetsDirectory.Parent.FullName;
        }

        public static string GetReferenceDirectory(string projectRoot)
        {
            return CombineUnderProject(projectRoot, ReferenceDirectoryRelative);
        }

        public static string GetManifestPath(string projectRoot)
        {
            return Path.Combine(GetReferenceDirectory(projectRoot), ManifestFileName);
        }

        public static string GetReferencePath(string projectRoot, UIReferenceScreen screen)
        {
            return Path.Combine(
                GetReferenceDirectory(projectRoot),
                UIReferenceCatalog.Get(screen).ReferenceFileName);
        }

        public static string GetReviewDirectory(string projectRoot)
        {
            return CombineUnderProject(projectRoot, ReviewDirectoryRelative);
        }

        public static string GetImplementationPath(string projectRoot, UIReferenceScreen screen)
        {
            return Path.Combine(
                GetReviewDirectory(projectRoot),
                $"{UIReferenceCatalog.Get(screen).ReviewStem}_Implementation.png");
        }

        public static string GetReferenceCapturePath(string projectRoot, UIReferenceScreen screen)
        {
            return Path.Combine(
                GetReviewDirectory(projectRoot),
                $"{UIReferenceCatalog.Get(screen).ReviewStem}_ReferenceOnly.png");
        }

        public static string GetBlendedCapturePath(string projectRoot, UIReferenceScreen screen)
        {
            return Path.Combine(
                GetReviewDirectory(projectRoot),
                $"{UIReferenceCatalog.Get(screen).ReviewStem}_Blended50.png");
        }

        private static string CombineUnderProject(string projectRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root must not be empty.", nameof(projectRoot));
            }

            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }
    }

    public sealed class UIReferenceValidationRecord
    {
        public UIReferenceValidationRecord(
            UIReferenceScreen screen,
            string role,
            string filePath,
            string sha256,
            long sizeBytes,
            int width,
            int height)
        {
            Screen = screen;
            Role = role;
            FilePath = filePath;
            Sha256 = sha256;
            SizeBytes = sizeBytes;
            Width = width;
            Height = height;
        }

        public UIReferenceScreen Screen { get; }

        public string Role { get; }

        public string FilePath { get; }

        public string Sha256 { get; }

        public long SizeBytes { get; }

        public int Width { get; }

        public int Height { get; }
    }

    public sealed class UIReferenceValidationResult
    {
        private readonly List<string> issues = new List<string>();
        private readonly List<UIReferenceValidationRecord> records =
            new List<UIReferenceValidationRecord>();

        public bool IsValid => issues.Count == 0 && records.Count == UIReferenceCatalog.All.Count;

        public IReadOnlyList<string> Issues => issues;

        public IReadOnlyList<UIReferenceValidationRecord> Records => records;

        internal void AddIssue(string issue)
        {
            issues.Add(issue);
        }

        internal void AddRecord(UIReferenceValidationRecord record)
        {
            records.Add(record);
        }

        public bool TryGetRecord(UIReferenceScreen screen, out UIReferenceValidationRecord record)
        {
            for (var index = 0; index < records.Count; index++)
            {
                if (records[index].Screen == screen)
                {
                    record = records[index];
                    return true;
                }
            }

            record = null;
            return false;
        }
    }

    public sealed class UIReferenceCaptureResult
    {
        private readonly List<string> outputPaths = new List<string>();
        private readonly List<string> errors = new List<string>();

        public bool Succeeded => errors.Count == 0;

        public IReadOnlyList<string> OutputPaths => outputPaths;

        public IReadOnlyList<string> Errors => errors;

        internal void AddOutput(string path)
        {
            outputPaths.Add(path);
        }

        internal void AddError(string error)
        {
            errors.Add(error);
        }
    }
}
