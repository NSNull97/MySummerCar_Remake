using System.Collections.Generic;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public enum DonorImportAction
    {
        Copy,
        UpToDate,
        Conflict,
        Blocked
    }

    public sealed class DonorImportPlanOperation
    {
        public DonorImportPlanOperation(
            DonorAssetRecord record,
            string sourceFile,
            string destinationFile,
            string destinationAssetPath,
            DonorImportAction action,
            string message)
        {
            Record = record;
            SourceFile = sourceFile;
            DestinationFile = destinationFile;
            DestinationAssetPath = destinationAssetPath;
            Action = action;
            Message = message;
        }

        public DonorAssetRecord Record { get; }
        public string SourceFile { get; }
        public string DestinationFile { get; }
        public string DestinationAssetPath { get; }
        public DonorImportAction Action { get; }
        public string Message { get; }
    }

    public sealed class DonorImportPlan
    {
        public DonorImportPlan(
            DonorAssetManifest manifest,
            IEnumerable<DonorImportPlanOperation> operations,
            IEnumerable<string> errors)
        {
            Manifest = manifest;
            Operations = new List<DonorImportPlanOperation>(operations);
            Errors = new List<string>(errors);
        }

        public DonorAssetManifest Manifest { get; }
        public IReadOnlyList<DonorImportPlanOperation> Operations { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool CanExecute => Errors.Count == 0 && !ContainsBlockedOperation();

        private bool ContainsBlockedOperation()
        {
            foreach (DonorImportPlanOperation operation in Operations)
            {
                if (operation.Action == DonorImportAction.Blocked ||
                    operation.Action == DonorImportAction.Conflict)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
