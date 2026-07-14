using System;
using System.Collections.Generic;
using System.IO;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class DonorImportPlanner
    {
        public static DonorImportPlan CreatePlan(
            DonorAssetManifest manifest,
            string stagingRoot,
            string projectRoot)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var operations = new List<DonorImportPlanOperation>();
            var errors = new List<string>();
            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            var destinationPaths = new HashSet<string>(StringComparer.Ordinal);

            if (manifest.SchemaVersion != DonorAssetManifest.CurrentSchemaVersion)
            {
                errors.Add(
                    $"Manifest schema {manifest.SchemaVersion} is unsupported; expected " +
                    $"{DonorAssetManifest.CurrentSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(manifest.ManifestId))
            {
                errors.Add("Manifest ID is missing.");
            }

            if (manifest.Records.Count == 0)
            {
                errors.Add("Manifest contains no reviewed asset records.");
            }

            if (!string.Equals(
                    manifest.PipelineVersion,
                    DonorImportPipelineInfo.CurrentVersion,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Manifest pipeline version '{manifest.PipelineVersion}' does not match " +
                    $"'{DonorImportPipelineInfo.CurrentVersion}'.");
            }

            foreach (DonorAssetRecord record in manifest.Records)
            {
                PlanRecord(record, stagingRoot, projectRoot, recordIds, destinationPaths, operations, errors);
            }

            return new DonorImportPlan(manifest, operations, errors);
        }

        private static void PlanRecord(
            DonorAssetRecord record,
            string stagingRoot,
            string projectRoot,
            HashSet<string> recordIds,
            HashSet<string> destinationPaths,
            List<DonorImportPlanOperation> operations,
            List<string> errors)
        {
            if (record == null)
            {
                errors.Add("Manifest contains a null record.");
                return;
            }

            string context = string.IsNullOrWhiteSpace(record.RecordId) ? "<missing-record-id>" : record.RecordId;
            if (string.IsNullOrWhiteSpace(record.RecordId) || !recordIds.Add(record.RecordId))
            {
                errors.Add($"Record ID is missing or duplicated: {context}.");
                return;
            }

            if (!string.IsNullOrEmpty(record.SourceSha256) &&
                !Sha256Digest.IsCanonical(record.SourceSha256))
            {
                errors.Add($"Record {context} has an invalid donor-container SHA-256.");
                return;
            }

            if (!Sha256Digest.IsCanonical(record.StagedFileSha256))
            {
                errors.Add($"Record {context} has an invalid staged-file SHA-256.");
                return;
            }

            if (!DonorImportPathPolicy.TryNormalizeRelativePath(
                    record.SourceRelativePath,
                    out _,
                    out string sourcePathError))
            {
                errors.Add($"Record {context} has an invalid donor-relative source path: {sourcePathError}");
                return;
            }

            if (string.IsNullOrWhiteSpace(record.SourceObjectName))
            {
                errors.Add($"Record {context} is missing its donor object name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(record.ImporterId) ||
                string.IsNullOrWhiteSpace(record.ImporterVersion))
            {
                errors.Add($"Record {context} is missing importer ID/version provenance.");
                return;
            }

            if (!DonorImportPathPolicy.TryNormalizeReferenceAssetPath(
                    record.ReferenceAssetPath,
                    out string referenceAssetPath,
                    out string destinationError))
            {
                errors.Add($"Record {context}: {destinationError}");
                return;
            }

            if (!destinationPaths.Add(referenceAssetPath))
            {
                errors.Add($"Reference destination is duplicated: {referenceAssetPath}.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(record.ProductionReplacementPath) &&
                !DonorImportPathPolicy.TryNormalizeProductionAssetPath(
                    record.ProductionReplacementPath,
                    out _,
                    out string productionPathError))
            {
                errors.Add($"Record {context}: {productionPathError}");
                return;
            }

            if (IsProductionClassification(record.Classification))
            {
                errors.Add(
                    $"Record {context} uses production classification {record.Classification} " +
                    "for a reference-only import.");
                return;
            }

            if (record.Classification == DonorTransferClassification.Blocked ||
                record.Classification == DonorTransferClassification.Rejected ||
                record.Status == DonorAssetStatus.Blocked ||
                record.Status == DonorAssetStatus.Rejected)
            {
                errors.Add($"Record {context} is classified or marked as non-importable.");
                return;
            }

            string sourceFile;
            string destinationFile;
            try
            {
                sourceFile = DonorImportPathPolicy.ResolveStagingFile(stagingRoot, record.StagedRelativePath);
                destinationFile = DonorImportPathPolicy.ResolveProjectAssetFile(projectRoot, referenceAssetPath);
            }
            catch (ArgumentException exception)
            {
                errors.Add($"Record {context}: {exception.Message}");
                return;
            }

            if (!File.Exists(sourceFile))
            {
                operations.Add(new DonorImportPlanOperation(
                    record,
                    sourceFile,
                    destinationFile,
                    referenceAssetPath,
                    DonorImportAction.Blocked,
                    "Staged source file is missing."));
                return;
            }

            if (!Sha256FileHasher.Matches(sourceFile, record.StagedFileSha256))
            {
                operations.Add(new DonorImportPlanOperation(
                    record,
                    sourceFile,
                    destinationFile,
                    referenceAssetPath,
                    DonorImportAction.Blocked,
                    "Staged source hash does not match the manifest."));
                return;
            }

            if (!File.Exists(destinationFile))
            {
                operations.Add(new DonorImportPlanOperation(
                    record,
                    sourceFile,
                    destinationFile,
                    referenceAssetPath,
                    DonorImportAction.Copy,
                    "Reference file will be copied from external staging."));
                return;
            }

            bool destinationMatches = Sha256FileHasher.Matches(destinationFile, record.StagedFileSha256);
            operations.Add(new DonorImportPlanOperation(
                record,
                sourceFile,
                destinationFile,
                referenceAssetPath,
                destinationMatches ? DonorImportAction.UpToDate : DonorImportAction.Conflict,
                destinationMatches
                    ? "Reference file is already up to date."
                    : "Destination exists with a different hash; overwrite is prohibited."));
        }

        private static bool IsProductionClassification(DonorTransferClassification classification)
        {
            return classification == DonorTransferClassification.ProductionReady ||
                   classification == DonorTransferClassification.ReauthoredGeometry ||
                   classification == DonorTransferClassification.ReauthoredMaterial ||
                   classification == DonorTransferClassification.ReauthoredTexture ||
                   classification == DonorTransferClassification.Reimplemented ||
                   classification == DonorTransferClassification.CodePorted ||
                   classification == DonorTransferClassification.ConfigurationTransferred;
        }
    }
}
