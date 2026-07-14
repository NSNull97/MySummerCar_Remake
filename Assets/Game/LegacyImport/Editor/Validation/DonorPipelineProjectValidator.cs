using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.LegacyImport.Editor.Proof;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Validation
{
    public static class DonorPipelineProjectValidator
    {
        private const string LocalConfigurationPath = "Config/DonorPaths.local.json";

        public static IReadOnlyList<DonorPipelineValidationIssue> ValidateProject()
        {
            var issues = new List<DonorPipelineValidationIssue>();
            var records = new List<DonorAssetRecord>();
            var recordIds = new HashSet<string>(StringComparer.Ordinal);

            ValidateConfiguredRoots(issues);
            LoadAndValidateRegistries(records, recordIds, issues);
            ValidateReferenceProvenance(records, issues);
            ValidateProductionPrefabs(issues);
            ValidateBuildScenes(issues);
            issues.AddRange(ControlledProofValidator.Validate());
            return issues;
        }

        public static IReadOnlyList<DonorPipelineValidationIssue> ValidateBuildSafety()
        {
            var issues = new List<DonorPipelineValidationIssue>();
            ValidateProductionPrefabs(issues);
            ValidateBuildScenes(issues);
            return issues;
        }

        private static void ValidateConfiguredRoots(List<DonorPipelineValidationIssue> issues)
        {
            if (!File.Exists(LocalConfigurationPath))
            {
                issues.Add(Error(
                    "MissingLocalConfiguration",
                    "Ignored donor path configuration is missing.",
                    LocalConfigurationPath));
                return;
            }

            try
            {
                DonorPathConfiguration configuration =
                    DonorPathConfiguration.LoadFromFile(LocalConfigurationPath);
                foreach (string error in DonorPipelineRootValidator.Validate(
                             configuration.OriginalGameDirectory,
                             configuration.DonorStagingDirectory,
                             configuration.UnityProjectDirectory))
                {
                    issues.Add(Error("UnsafeConfiguredRoots", error, LocalConfigurationPath));
                }
            }
            catch (Exception exception)
            {
                issues.Add(Error(
                    "InvalidLocalConfiguration",
                    exception.Message,
                    LocalConfigurationPath));
            }
        }

        public static bool HasErrors(IReadOnlyList<DonorPipelineValidationIssue> issues)
        {
            foreach (DonorPipelineValidationIssue issue in issues)
            {
                if (issue.Severity == DonorPipelineValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LoadAndValidateRegistries(
            List<DonorAssetRecord> allRecords,
            HashSet<string> recordIds,
            List<DonorPipelineValidationIssue> issues)
        {
            string[] registryGuids = AssetDatabase.FindAssets(
                "t:DonorAssetRegistry",
                new[] { "Assets/Game/LegacyImport/Manifests" });

            foreach (string registryGuid in registryGuids)
            {
                string registryPath = AssetDatabase.GUIDToAssetPath(registryGuid);
                DonorAssetRegistry registry = AssetDatabase.LoadAssetAtPath<DonorAssetRegistry>(registryPath);
                if (registry == null)
                {
                    issues.Add(Error("InvalidRegistry", "Registry asset could not be loaded.", registryPath));
                    continue;
                }

                if (registry.ManifestSchemaVersion != DonorAssetManifest.CurrentSchemaVersion)
                {
                    issues.Add(Error(
                        "RegistrySchema",
                        $"Registry schema {registry.ManifestSchemaVersion} is unsupported.",
                        registryPath));
                }

                if (!string.Equals(
                        registry.PipelineVersion,
                        DonorImportPipelineInfo.CurrentVersion,
                        StringComparison.Ordinal))
                {
                    issues.Add(Error(
                        "RegistryPipelineVersion",
                        $"Registry pipeline version '{registry.PipelineVersion}' is unsupported.",
                        registryPath));
                }

                foreach (DonorAssetRecord record in registry.Records)
                {
                    ValidateRecord(record, registryPath, recordIds, issues);
                    if (record != null)
                    {
                        allRecords.Add(record);
                    }
                }
            }
        }

        private static void ValidateRecord(
            DonorAssetRecord record,
            string registryPath,
            HashSet<string> recordIds,
            List<DonorPipelineValidationIssue> issues)
        {
            if (record == null)
            {
                issues.Add(Error("NullRecord", "Registry contains a null record.", registryPath));
                return;
            }

            if (string.IsNullOrWhiteSpace(record.RecordId) || !recordIds.Add(record.RecordId))
            {
                issues.Add(Error(
                    "DuplicateRecordId",
                    $"Record ID is missing or duplicated: '{record.RecordId}'.",
                    registryPath));
            }

            if (!string.IsNullOrEmpty(record.SourceSha256) &&
                !Sha256Digest.IsCanonical(record.SourceSha256))
            {
                issues.Add(Error(
                    "InvalidSourceHash",
                    $"Record '{record.RecordId}' has an invalid donor-container SHA-256.",
                    registryPath));
            }

            if (!Sha256Digest.IsCanonical(record.StagedFileSha256))
            {
                issues.Add(Error(
                    "InvalidStagedHash",
                    $"Record '{record.RecordId}' has an invalid staged-file SHA-256.",
                    registryPath));
            }

            if (!DonorImportPathPolicy.TryNormalizeRelativePath(
                    record.SourceRelativePath,
                    out _,
                    out string sourcePathError))
            {
                issues.Add(Error(
                    "InvalidDonorSourcePath",
                    $"Record '{record.RecordId}': {sourcePathError}",
                    registryPath));
            }

            if (string.IsNullOrWhiteSpace(record.SourceObjectName))
            {
                issues.Add(Error(
                    "MissingDonorObjectName",
                    $"Record '{record.RecordId}' has no donor object name.",
                    registryPath));
            }

            if (string.IsNullOrWhiteSpace(record.ImporterId) ||
                string.IsNullOrWhiteSpace(record.ImporterVersion))
            {
                issues.Add(Error(
                    "MissingImporterVersion",
                    $"Record '{record.RecordId}' is missing importer ID/version.",
                    registryPath));
            }

            if (!DonorImportPathPolicy.TryNormalizeReferenceAssetPath(
                    record.ReferenceAssetPath,
                    out string referencePath,
                    out string referenceError))
            {
                issues.Add(Error(
                    "InvalidReferenceDestination",
                    $"Record '{record.RecordId}': {referenceError}",
                    registryPath));
            }
            else if (RequiresImportedReference(record.Status))
            {
                ValidateImportedReferenceFile(record, referencePath, registryPath, issues);
            }

            if (!string.IsNullOrWhiteSpace(record.ProductionReplacementPath))
            {
                if (!DonorImportPathPolicy.TryNormalizeProductionAssetPath(
                        record.ProductionReplacementPath,
                        out string productionPath,
                        out string productionError))
                {
                    issues.Add(Error(
                        "InvalidProductionDestination",
                        $"Record '{record.RecordId}': {productionError}",
                        registryPath));
                }
                else if (record.Status == DonorAssetStatus.ReplacementReady &&
                         AssetDatabase.LoadMainAssetAtPath(productionPath) == null)
                {
                    issues.Add(Error(
                        "MissingProductionReplacement",
                        $"Replacement-ready record '{record.RecordId}' has no production asset.",
                        productionPath));
                }
            }
        }

        private static void ValidateImportedReferenceFile(
            DonorAssetRecord record,
            string referencePath,
            string registryPath,
            List<DonorPipelineValidationIssue> issues)
        {
            UnityEngine.Object referenceAsset = AssetDatabase.LoadMainAssetAtPath(referencePath);
            if (referenceAsset == null)
            {
                issues.Add(Error(
                    "MissingReferenceAsset",
                    $"Imported record '{record.RecordId}' has no reference asset.",
                    referencePath));
                return;
            }

            string filePath = Path.GetFullPath(referencePath);
            if (File.Exists(filePath) &&
                !Sha256FileHasher.Matches(filePath, record.StagedFileSha256))
            {
                issues.Add(Error(
                    "ReferenceHashMismatch",
                    $"Imported reference hash differs from record '{record.RecordId}'.",
                    registryPath));
            }
        }

        private static void ValidateReferenceProvenance(
            List<DonorAssetRecord> records,
            List<DonorPipelineValidationIssue> issues)
        {
            var referenceAssets = new List<string>();
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (DonorImportPathPolicy.IsUnderAssetRoot(
                        assetPath,
                        DonorImportPathPolicy.ReferenceOnlyRoot) &&
                    !AssetDatabase.IsValidFolder(assetPath) &&
                    !assetPath.EndsWith("/.gitkeep", StringComparison.Ordinal))
                {
                    referenceAssets.Add(assetPath);
                }
            }

            issues.AddRange(DonorProvenanceValidator.FindMissingProvenance(referenceAssets, records));
        }

        private static void ValidateProductionPrefabs(List<DonorPipelineValidationIssue> issues)
        {
            var dependencyRecords = new List<DonorAssetDependencyRecord>();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game" });
            foreach (string prefabGuid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (IsReferencePath(prefabPath))
                {
                    continue;
                }

                dependencyRecords.Add(new DonorAssetDependencyRecord(
                    prefabPath,
                    AssetDatabase.GetDependencies(prefabPath, recursive: true)));

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null && prefab.GetComponentInChildren<LegacyAssetReference>(true) != null)
                {
                    issues.Add(Error(
                        "ProductionLegacyMarker",
                        "Production prefab contains LegacyAssetReference.",
                        prefabPath));
                }
            }

            issues.AddRange(DonorProvenanceValidator.FindProductionReferenceLeaks(dependencyRecords));
        }

        private static void ValidateBuildScenes(List<DonorPipelineValidationIssue> issues)
        {
            var dependencyRecords = new List<DonorAssetDependencyRecord>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                dependencyRecords.Add(new DonorAssetDependencyRecord(
                    scene.path,
                    AssetDatabase.GetDependencies(scene.path, recursive: true)));
            }

            issues.AddRange(DonorProvenanceValidator.FindProductionReferenceLeaks(dependencyRecords));
        }

        private static bool RequiresImportedReference(DonorAssetStatus status)
        {
            return status == DonorAssetStatus.ImportedReference ||
                   status == DonorAssetStatus.ReplacementInProgress ||
                   status == DonorAssetStatus.ReplacementReady;
        }

        private static bool IsReferencePath(string assetPath)
        {
            return DonorImportPathPolicy.IsUnderAssetRoot(
                       assetPath,
                       DonorImportPathPolicy.ReferenceOnlyRoot) ||
                   DonorImportPathPolicy.IsUnderAssetRoot(
                       assetPath,
                       DonorImportPathPolicy.DonorGeneratedRoot);
        }

        private static DonorPipelineValidationIssue Error(string code, string message, string assetPath)
        {
            return new DonorPipelineValidationIssue(
                DonorPipelineValidationSeverity.Error,
                code,
                message,
                assetPath);
        }
    }
}
