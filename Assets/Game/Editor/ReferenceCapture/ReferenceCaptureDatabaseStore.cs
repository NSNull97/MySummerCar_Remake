using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Core.ReferenceCapture;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.ReferenceCapture
{
    public static class ReferenceCaptureDatabaseStore
    {
        public static ReferenceCaptureDatabase LoadDatabase() => ReferenceCaptureDatabase.FromJson(
            File.ReadAllText(ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.DatabaseAssetPath)));

        public static ReferenceTuningOverrideSet LoadTuningOverrides() => ReferenceTuningOverrideSet.FromJson(
            File.ReadAllText(ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.TuningOverrideAssetPath)));

        public static IReadOnlyList<(string AssetPath, ReferenceCalibrationFixture Fixture)> LoadFixtures()
        {
            string root = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.FixtureAssetRoot);
            return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => (
                    Path.GetRelativePath(ReferenceCapturePaths.ProjectRoot, path).Replace('\\', '/'),
                    ReferenceCalibrationFixture.FromJson(File.ReadAllText(path))))
                .ToArray();
        }

        public static void SaveDatabase(ReferenceCaptureDatabase database)
        {
            string path = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.DatabaseAssetPath);
            File.WriteAllText(path, database.ToDeterministicJson(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReferenceCapturePaths.DatabaseAssetPath, ImportAssetOptions.ForceUpdate);
        }

        public static string ImportStructuredMeasurements(string json)
        {
            ReferenceCaptureImportEnvelope envelope = ReferenceCaptureImportEnvelope.FromJson(json);
            if (envelope.SchemaVersion != ReferenceCaptureDatabaseVersion.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported import schema {envelope.SchemaVersion}.");
            if (!string.Equals(envelope.DatasetVersion, ReferenceCaptureDatabaseVersion.CurrentDatasetVersion, StringComparison.Ordinal))
                throw new InvalidOperationException($"Import dataset '{envelope.DatasetVersion}' does not match '{ReferenceCaptureDatabaseVersion.CurrentDatasetVersion}'.");

            ReferenceCaptureDatabase merged = LoadDatabase().Merge(envelope.Evidence, envelope.Measurements);
            ReferenceCaptureValidationResult validation = ReferenceCaptureValidator.Validate(merged, LoadTuningOverrides());
            if (!validation.IsValid)
                throw new InvalidOperationException("Structured import failed validation:\n" + string.Join("\n", validation.Issues.Where(issue => issue.Severity == ReferenceValidationSeverity.Error)));
            SaveDatabase(merged);
            return $"Imported/updated {envelope.Measurements.Length} measurement(s) and {envelope.Evidence.Length} evidence record(s).";
        }

        public static string AddManualNumericObservation(
            ReferenceCategory category,
            ReferencePriority priority,
            string subcategory,
            string name,
            float normalizedValue,
            ReferenceUnit unit,
            ReferenceCoordinateSpace coordinateSpace,
            ReferenceConfidence confidence,
            string rawObservation,
            string evidenceRootKind,
            string evidenceLogicalPath,
            string notes)
        {
            if (string.IsNullOrWhiteSpace(subcategory) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Subcategory and name are required.");
            if (string.IsNullOrWhiteSpace(evidenceLogicalPath))
                throw new ArgumentException("A logical evidence path is required.");

            string evidenceId = ReferenceStableIdUtility.Create($"04b.1|evidence|{evidenceRootKind}|{evidenceLogicalPath}").Value;
            string recordId = ReferenceStableIdUtility.Create($"04b.1|manual|{category}|{subcategory}|{name}|{evidenceId}").Value;
            ReferenceEvidenceRecord evidence = ReferenceEvidenceRecord.Create(
                evidenceId, "e5398e3eeb622a7ee7eea3ddac195ba9", evidenceRootKind, evidenceLogicalPath,
                "ManualCapture", string.Empty, !string.Equals(evidenceRootKind, "Project", StringComparison.OrdinalIgnoreCase),
                "Added through the Reference Capture dashboard; evidence payload remains outside Git when externalToGit is true.");
            ReferenceRecord record = ReferenceRecord.Create(
                recordId, category, subcategory, name, priority, unit, coordinateSpace,
                "e5398e3eeb622a7ee7eea3ddac195ba9", evidenceLogicalPath,
                ReferenceCaptureMethod.ManualMeasurement, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                confidence, ReferenceTolerance.Create(0f, 0f, unit, "Enter measured uncertainty before validation."),
                rawObservation, ReferenceValue.Numeric(normalizedValue), notes, new[] { evidenceId }, Array.Empty<string>(),
                ReferenceValidationStatus.NeedsReview);

            ReferenceCaptureDatabase merged = LoadDatabase().Merge(new[] { evidence }, new[] { MeasurementRecord.Create(record) });
            SaveDatabase(merged);
            return recordId;
        }

        public static string GenerateChecklist(ReferenceCaptureDatabase database)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Generated Reference Capture Checklist");
            builder.AppendLine();
            builder.AppendLine($"Dataset: `{database.Version.DatasetVersion}`. Generated: `{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}`.");
            builder.AppendLine();
            foreach (IGrouping<ReferenceCategory, ReferenceRequirementRecord> category in database.Requirements
                         .Where(item => item.Status != ReferenceRequirementStatus.Covered)
                         .OrderBy(item => item.Priority).ThenBy(item => item.Category).ThenBy(item => item.RequirementId, StringComparer.Ordinal)
                         .GroupBy(item => item.Category))
            {
                builder.AppendLine("## " + category.Key);
                builder.AppendLine();
                foreach (ReferenceRequirementRecord item in category)
                    builder.AppendLine($"- [ ] `{item.Priority}` `{item.RequirementId}` — {item.Name} ({item.Status}). {item.Notes}");
                builder.AppendLine();
            }

            string path = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.GeneratedChecklistPath);
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return ReferenceCapturePaths.GeneratedChecklistPath;
        }

        public static void ExportFixture(string fixtureAssetPath, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) return;
            string source = ReferenceCapturePaths.ToAbsoluteProjectPath(fixtureAssetPath);
            File.Copy(source, destination, overwrite: true);
        }

        public static bool TryResolveEvidencePath(ReferenceEvidenceRecord evidence, out string absolutePath)
        {
            absolutePath = string.Empty;
            if (evidence == null || string.IsNullOrWhiteSpace(evidence.LogicalPath)) return false;
            DonorPathConfiguration config = DonorPathConfiguration.LoadFromFile(
                ReferenceCapturePaths.ToAbsoluteProjectPath("Config/DonorPaths.local.json"));
            string root;
            switch (evidence.RootKind)
            {
                case "Project": root = ReferenceCapturePaths.ProjectRoot; break;
                case "DonorStaging": root = config.DonorStagingDirectory; break;
                case "LegacyReference": root = config.LegacyReferenceDirectory; break;
                case "ReferenceMedia": root = config.ReferenceMediaDirectory; break;
                case "Donor": root = config.OriginalGameDirectory; break;
                default: return false;
            }
            absolutePath = Path.GetFullPath(Path.Combine(root, evidence.LogicalPath.Replace('/', Path.DirectorySeparatorChar)));
            string canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return absolutePath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
