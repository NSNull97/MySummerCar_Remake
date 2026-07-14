using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class DonorImportExecutor
    {
        public static IReadOnlyList<string> Execute(DonorImportPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (!plan.CanExecute)
            {
                throw new InvalidOperationException("Import plan contains errors, conflicts, or blocked operations.");
            }

            var copiedAssetPaths = new List<string>();
            foreach (DonorImportPlanOperation operation in plan.Operations)
            {
                if (operation.Action != DonorImportAction.Copy)
                {
                    continue;
                }

                string destinationDirectory = Path.GetDirectoryName(operation.DestinationFile);
                if (string.IsNullOrEmpty(destinationDirectory))
                {
                    throw new InvalidOperationException(
                        $"Destination has no directory: {operation.DestinationFile}");
                }

                Directory.CreateDirectory(destinationDirectory);
                File.Copy(operation.SourceFile, operation.DestinationFile, overwrite: false);

                if (!Sha256FileHasher.Matches(
                        operation.DestinationFile,
                        operation.Record.StagedFileSha256))
                {
                    File.Delete(operation.DestinationFile);
                    throw new IOException(
                        $"Post-copy hash verification failed for {operation.DestinationAssetPath}.");
                }

                copiedAssetPaths.Add(operation.DestinationAssetPath);
            }

            if (copiedAssetPaths.Count > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            return copiedAssetPaths;
        }
    }
}
