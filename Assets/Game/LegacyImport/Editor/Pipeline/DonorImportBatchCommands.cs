using System;
using System.IO;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class DonorImportBatchCommands
    {
        private const string LocalConfigurationPath = "Config/DonorPaths.local.json";
        private const string ManifestArgument = "-donorManifest";
        private const string ConfirmationArgument = "-confirmDonorImport";

        public static void PlanBatch()
        {
            Run(execute: false);
        }

        public static void ExecuteBatch()
        {
            Run(execute: true);
        }

        private static void Run(bool execute)
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Donor batch commands require Unity batch mode.");
            }

            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(LocalConfigurationPath);
            DonorImportCommands.EnsureConfiguredRootsAreSeparated(configuration);
            string manifestPath = RequireArgument(ManifestArgument, normalizeAsPath: true);
            DonorImportCommands.EnsureManifestInsideStaging(
                manifestPath,
                configuration.DonorStagingDirectory);

            DonorAssetManifest manifest = DonorAssetManifest.FromJson(File.ReadAllText(manifestPath));
            DonorImportPlan plan = DonorImportPlanner.CreatePlan(
                manifest,
                configuration.DonorStagingDirectory,
                configuration.UnityProjectDirectory);
            DonorImportCommands.LogPlan(plan);
            if (!plan.CanExecute)
            {
                throw new InvalidOperationException("Controlled donor import plan is not executable.");
            }

            if (!execute)
            {
                Debug.Log($"DONOR_PLAN_OK manifest={manifest.ManifestId} operations={plan.Operations.Count}");
                return;
            }

            string confirmation = RequireArgument(ConfirmationArgument, normalizeAsPath: false);
            if (!string.Equals(confirmation, manifest.ManifestId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Batch execution confirmation must exactly match the manifest ID.");
            }

            int copiedCount = DonorImportExecutor.Execute(plan).Count;
            DonorAssetManifest importedManifest = DonorImportCommands.CreateImportedManifest(plan);
            DonorImportCommands.WriteRegistry(importedManifest);
            DonorImportCommands.UpdateLedger(importedManifest);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"DONOR_EXECUTION_OK manifest={manifest.ManifestId} copied={copiedCount} " +
                $"operations={plan.Operations.Count}");
        }

        private static string RequireArgument(string argumentName, bool normalizeAsPath)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                {
                    string value = arguments[index + 1];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return normalizeAsPath ? Path.GetFullPath(value) : value;
                    }
                }
            }

            throw new ArgumentException($"Required batch argument is missing: {argumentName}.");
        }
    }
}
