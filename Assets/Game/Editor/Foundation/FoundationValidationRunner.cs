using System;
using System.Collections.Generic;
using System.IO;
using MSC.Core.Identity;
using MSC.Editor.Validation;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Foundation
{
    public static class FoundationValidationRunner
    {
        private const string LocalPathConfiguration = "Config/DonorPaths.local.json";

        [MenuItem("Tools/My Summer Car/Validation/Run Foundation Validation")]
        public static void RunFromMenu()
        {
            Run(throwOnFailure: false);
        }

        public static void RunBatch()
        {
            FoundationSceneBuilder.EnsureBootstrapContent();
            Run(throwOnFailure: true);
        }

        private static void Run(bool throwOnFailure)
        {
            var failures = new List<string>();

            ValidateStableIds(failures);
            ValidateAssemblyBoundaries(failures);
            ValidateLocalPaths(failures);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FoundationSceneBuilder.BootstrapScenePath) == null)
            {
                failures.Add($"Bootstrap scene is missing: {FoundationSceneBuilder.BootstrapScenePath}");
            }

            if (failures.Count == 0)
            {
                Debug.Log("Foundation validation passed: stable IDs, assembly boundaries, local paths, and Bootstrap content are valid.");
                return;
            }

            foreach (string failure in failures)
            {
                Debug.LogError(failure);
            }

            if (throwOnFailure)
            {
                throw new InvalidOperationException($"Foundation validation failed with {failures.Count} issue(s).");
            }
        }

        private static void ValidateStableIds(List<string> failures)
        {
            IReadOnlyList<StableEntityIdIssue> issues = StableEntityIdProjectValidator.ValidateProjectContent();
            foreach (StableEntityIdIssue issue in issues)
            {
                string conflict = string.IsNullOrEmpty(issue.ConflictingContext)
                    ? string.Empty
                    : $" First occurrence: {issue.ConflictingContext}.";
                failures.Add(
                    $"Stable ID {issue.Kind}: {issue.Context}; value '{issue.SerializedId}'.{conflict}");
            }
        }

        private static void ValidateAssemblyBoundaries(List<string> failures)
        {
            IReadOnlyList<AssemblyBoundaryIssue> issues =
                AssemblyDefinitionValidator.ValidateRuntimeToEditorReferences();
            foreach (AssemblyBoundaryIssue issue in issues)
            {
                failures.Add(
                    $"Runtime assembly {issue.AssemblyName} references Editor assembly " +
                    $"{issue.ReferencedAssembly} in {issue.AssetPath}.");
            }
        }

        private static void ValidateLocalPaths(List<string> failures)
        {
            if (!File.Exists(LocalPathConfiguration))
            {
                failures.Add($"Ignored local path configuration is missing: {LocalPathConfiguration}");
                return;
            }

            try
            {
                DonorPathConfiguration configuration =
                    DonorPathConfiguration.LoadFromFile(LocalPathConfiguration);
                string projectRoot = DonorPathConfiguration.NormalizeDirectoryPath(Directory.GetCurrentDirectory());

                if (!string.Equals(
                        configuration.UnityProjectDirectory,
                        projectRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        "UnityProjectDirectory in the ignored local configuration does not match the open project: " +
                        configuration.UnityProjectDirectory);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"Local path configuration is invalid: {exception.Message}");
            }
        }
    }
}
