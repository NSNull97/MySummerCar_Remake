using System;
using System.IO;
using UnityEngine;

namespace MSC.Editor.ReferenceCapture
{
    public static class ReferenceCapturePaths
    {
        public const string DatabaseAssetPath = "Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureDatabase.json";
        public const string TuningOverrideAssetPath = "Assets/Game/Core/Configuration/ReferenceCapture/ReferenceTuningOverrides.json";
        public const string ImportTemplateAssetPath = "Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureImportTemplate.json";
        public const string FixtureAssetRoot = "Assets/Game/Core/Configuration/ReferenceCapture/Fixtures";
        public const string DocumentationRoot = "Docs/ReferenceCapture";
        public const string GeneratedChecklistPath = DocumentationRoot + "/GENERATED_CAPTURE_CHECKLIST.md";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root could not be resolved.");

        public static string ToAbsoluteProjectPath(string relativePath)
        {
            string candidate = Path.GetFullPath(Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string root = Path.GetFullPath(ProjectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path escapes the Unity project root.", nameof(relativePath));
            return candidate;
        }
    }
}
