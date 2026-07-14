using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Validation
{
    public readonly struct AssemblyBoundaryIssue
    {
        public AssemblyBoundaryIssue(string assemblyName, string referencedAssembly, string assetPath)
        {
            AssemblyName = assemblyName;
            ReferencedAssembly = referencedAssembly;
            AssetPath = assetPath;
        }

        public string AssemblyName { get; }

        public string ReferencedAssembly { get; }

        public string AssetPath { get; }
    }

    public static class AssemblyDefinitionValidator
    {
        public static IReadOnlyList<AssemblyBoundaryIssue> ValidateRuntimeToEditorReferences()
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            var definitions = new List<AssemblyDefinitionData>();
            var editorAssemblyNames = new HashSet<string>(StringComparer.Ordinal);
            var editorAssemblyGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string assetPath in assetPaths)
            {
                if (!assetPath.StartsWith("Assets/Game/", StringComparison.Ordinal) ||
                    !assetPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AssemblyDefinitionJson json = JsonUtility.FromJson<AssemblyDefinitionJson>(File.ReadAllText(assetPath));
                if (json == null || string.IsNullOrWhiteSpace(json.name))
                {
                    continue;
                }

                bool isEditorOnly = IsEditorOnly(json);
                definitions.Add(new AssemblyDefinitionData(assetPath, json, isEditorOnly));
                if (isEditorOnly)
                {
                    editorAssemblyNames.Add(json.name);
                    editorAssemblyGuids.Add(AssetDatabase.AssetPathToGUID(assetPath));
                }
            }

            var issues = new List<AssemblyBoundaryIssue>();
            foreach (AssemblyDefinitionData definition in definitions)
            {
                if (definition.IsEditorOnly)
                {
                    continue;
                }

                foreach (string reference in definition.Json.references)
                {
                    string referencedGuid = reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase)
                        ? reference.Substring("GUID:".Length)
                        : string.Empty;
                    bool referencesEditorAssembly =
                        editorAssemblyNames.Contains(reference) ||
                        reference.EndsWith(".Editor", StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(referencedGuid) && editorAssemblyGuids.Contains(referencedGuid));

                    if (referencesEditorAssembly)
                    {
                        issues.Add(new AssemblyBoundaryIssue(
                            definition.Json.name,
                            reference,
                            definition.AssetPath));
                    }
                }
            }

            return issues;
        }

        private static bool IsEditorOnly(AssemblyDefinitionJson definition)
        {
            if (definition.name.EndsWith(".Editor", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (string platform in definition.includePlatforms)
            {
                if (string.Equals(platform, "Editor", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
            public string[] includePlatforms = Array.Empty<string>();
        }

        private readonly struct AssemblyDefinitionData
        {
            public AssemblyDefinitionData(string assetPath, AssemblyDefinitionJson json, bool isEditorOnly)
            {
                AssetPath = assetPath;
                Json = json;
                IsEditorOnly = isEditorOnly;
            }

            public string AssetPath { get; }

            public AssemblyDefinitionJson Json { get; }

            public bool IsEditorOnly { get; }
        }
    }
}
