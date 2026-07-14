using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.Ledger;
using MSC.LegacyImport.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class DonorImportCommands
    {
        private const string LocalConfigurationPath = "Config/DonorPaths.local.json";
        private const string LedgerPath = "Docs/Porting/PORTING_LEDGER.csv";
        private const string RegistryRoot = "Assets/Game/LegacyImport/Manifests";

        private static DonorImportPlan lastPlan;

        [MenuItem("Tools/My Summer Car/Legacy Import/Plan Manifest...")]
        public static void PlanManifest()
        {
            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(LocalConfigurationPath);
            EnsureConfiguredRootsAreSeparated(configuration);
            string initialDirectory = Path.Combine(configuration.DonorStagingDirectory, "manifests");
            string manifestPath = EditorUtility.OpenFilePanel(
                "Select controlled donor manifest",
                initialDirectory,
                "json");

            if (string.IsNullOrEmpty(manifestPath))
            {
                return;
            }

            EnsureManifestInsideStaging(manifestPath, configuration.DonorStagingDirectory);
            DonorAssetManifest manifest = DonorAssetManifest.FromJson(File.ReadAllText(manifestPath));
            lastPlan = DonorImportPlanner.CreatePlan(
                manifest,
                configuration.DonorStagingDirectory,
                configuration.UnityProjectDirectory);
            LogPlan(lastPlan);
        }

        [MenuItem("Tools/My Summer Car/Legacy Import/Execute Last Plan", true)]
        public static bool CanExecuteLastPlan()
        {
            return lastPlan != null && lastPlan.CanExecute;
        }

        [MenuItem("Tools/My Summer Car/Legacy Import/Execute Last Plan")]
        public static void ExecuteLastPlan()
        {
            if (lastPlan == null || !lastPlan.CanExecute)
            {
                throw new InvalidOperationException("There is no executable donor import plan.");
            }

            int copyCount = CountActions(lastPlan, DonorImportAction.Copy);
            int upToDateCount = CountActions(lastPlan, DonorImportAction.UpToDate);
            bool confirmed = EditorUtility.DisplayDialog(
                "Execute controlled donor import?",
                $"Copy {copyCount} reference file(s); keep {upToDateCount} up-to-date file(s). " +
                "Existing files are never overwritten. Registry and porting ledger will be updated.",
                "Execute",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            IReadOnlyList<string> copiedAssets = DonorImportExecutor.Execute(lastPlan);
            DonorAssetManifest importedManifest = CreateImportedManifest(lastPlan);
            WriteRegistry(importedManifest);
            UpdateLedger(importedManifest);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Controlled donor import completed: {copiedAssets.Count} copied, " +
                $"{upToDateCount} already up to date. Manifest '{importedManifest.ManifestId}'.");
            lastPlan = null;
        }

        internal static DonorAssetManifest CreateImportedManifest(DonorImportPlan plan)
        {
            var importedRecords = new List<DonorAssetRecord>(plan.Manifest.Records.Count);
            foreach (DonorAssetRecord record in plan.Manifest.Records)
            {
                DonorAssetStatus status = record.Status;
                if (status == DonorAssetStatus.Planned || status == DonorAssetStatus.Staged)
                {
                    status = DonorAssetStatus.ImportedReference;
                }

                importedRecords.Add(record.WithStatus(status));
            }

            return plan.Manifest.WithRecords(importedRecords);
        }

        internal static void WriteRegistry(DonorAssetManifest manifest)
        {
            ValidateManifestIdForAssetName(manifest.ManifestId);
            string registryPath = RegistryRoot + "/" + manifest.ManifestId + ".asset";
            DonorAssetRegistry registry = AssetDatabase.LoadAssetAtPath<DonorAssetRegistry>(registryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<DonorAssetRegistry>();
                registry.name = manifest.ManifestId;
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            registry.ApplyManifest(manifest);
            EditorUtility.SetDirty(registry);
        }

        internal static void UpdateLedger(DonorAssetManifest manifest)
        {
            var entries = new List<PortingLedgerEntry>(manifest.Records.Count);
            foreach (DonorAssetRecord record in manifest.Records)
            {
                entries.Add(PortingLedgerEntry.FromImportedReference(record));
            }

            string existingCsv = File.Exists(LedgerPath) ? File.ReadAllText(LedgerPath) : string.Empty;
            PortingLedgerUpdatePlan updatePlan = PortingLedgerUpdater.CreatePlan(existingCsv, entries);
            PortingLedgerUpdater.ExecuteFile(LedgerPath, updatePlan);
            Debug.Log(
                $"Porting ledger update: {updatePlan.AddedCount} added, " +
                $"{updatePlan.UpdatedCount} updated.");
        }

        internal static void LogPlan(DonorImportPlan plan)
        {
            foreach (string error in plan.Errors)
            {
                Debug.LogError("Donor import plan: " + error);
            }

            foreach (DonorImportPlanOperation operation in plan.Operations)
            {
                string message =
                    $"Donor import plan [{operation.Action}] {operation.Record.RecordId}: " +
                    $"{operation.DestinationAssetPath}. {operation.Message}";
                if (operation.Action == DonorImportAction.Blocked ||
                    operation.Action == DonorImportAction.Conflict)
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }

            Debug.Log(
                $"Donor import plan complete. Executable: {plan.CanExecute}; " +
                $"operations: {plan.Operations.Count}; errors: {plan.Errors.Count}.");
        }

        private static int CountActions(DonorImportPlan plan, DonorImportAction action)
        {
            int count = 0;
            foreach (DonorImportPlanOperation operation in plan.Operations)
            {
                if (operation.Action == action)
                {
                    count++;
                }
            }

            return count;
        }

        internal static void EnsureManifestInsideStaging(string manifestPath, string stagingRoot)
        {
            string normalizedManifest = Path.GetFullPath(manifestPath);
            string normalizedRoot = DonorPathConfiguration.NormalizeDirectoryPath(stagingRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!normalizedManifest.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Controlled manifests must be stored inside the configured external donor staging root.");
            }
        }

        internal static void EnsureConfiguredRootsAreSeparated(DonorPathConfiguration configuration)
        {
            IReadOnlyList<string> errors = DonorPipelineRootValidator.Validate(
                configuration.OriginalGameDirectory,
                configuration.DonorStagingDirectory,
                configuration.UnityProjectDirectory);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", errors));
            }
        }

        private static void ValidateManifestIdForAssetName(string manifestId)
        {
            if (string.IsNullOrWhiteSpace(manifestId) ||
                manifestId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                manifestId.Contains("/") ||
                manifestId.Contains("\\"))
            {
                throw new FormatException("Manifest ID is not a safe registry asset name.");
            }
        }
    }
}
