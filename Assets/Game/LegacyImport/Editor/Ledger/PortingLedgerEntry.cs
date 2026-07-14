using System.Collections.Generic;
using MSC.LegacyImport.Editor.Pipeline;

namespace MSC.LegacyImport.Editor.Ledger
{
    public sealed class PortingLedgerEntry
    {
        public PortingLedgerEntry(
            string sourceRelativePath,
            string sourceObjectOrSymbol,
            string sourceHash,
            string type,
            string classification,
            string destinationPath,
            string status,
            string dependencies,
            string toolVersion,
            string knownDifferences,
            string notes)
        {
            SourceRelativePath = sourceRelativePath ?? string.Empty;
            SourceObjectOrSymbol = sourceObjectOrSymbol ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            Type = type ?? string.Empty;
            Classification = classification ?? string.Empty;
            DestinationPath = destinationPath ?? string.Empty;
            Status = status ?? string.Empty;
            Dependencies = dependencies ?? string.Empty;
            ToolVersion = toolVersion ?? string.Empty;
            KnownDifferences = knownDifferences ?? string.Empty;
            Notes = notes ?? string.Empty;
        }

        public string SourceRelativePath { get; }
        public string SourceObjectOrSymbol { get; }
        public string SourceHash { get; }
        public string Type { get; }
        public string Classification { get; }
        public string DestinationPath { get; }
        public string Status { get; }
        public string Dependencies { get; }
        public string ToolVersion { get; }
        public string KnownDifferences { get; }
        public string Notes { get; }

        public string Key => SourceRelativePath + "\u001f" + SourceObjectOrSymbol + "\u001f" + DestinationPath;

        public static PortingLedgerEntry FromImportedReference(DonorAssetRecord record)
        {
            var dependencies = new List<string>(record.Dependencies);
            return new PortingLedgerEntry(
                record.SourceRelativePath,
                record.SourceObjectName,
                record.SourceSha256,
                record.Kind.ToString(),
                record.Classification.ToString(),
                record.ReferenceAssetPath,
                DonorAssetStatus.ImportedReference.ToString(),
                string.Join("; ", dependencies),
                record.ImporterId + " " + record.ImporterVersion +
                "; pipeline " + DonorImportPipelineInfo.CurrentVersion,
                "Reference-only donor data; production replacement must remain independent.",
                record.Notes);
        }

        public string[] ToColumns()
        {
            return new[]
            {
                SourceRelativePath,
                SourceObjectOrSymbol,
                SourceHash,
                Type,
                Classification,
                DestinationPath,
                Status,
                Dependencies,
                ToolVersion,
                KnownDifferences,
                Notes
            };
        }
    }
}
