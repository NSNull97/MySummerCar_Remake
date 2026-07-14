using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport
{
    [Serializable]
    public sealed class DonorAssetRecord
    {
        [SerializeField] private string recordId = string.Empty;
        [SerializeField] private string sourceRelativePath = string.Empty;
        [SerializeField] private string sourceObjectName = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private string stagedFileSha256 = string.Empty;
        [SerializeField] private DonorAssetKind kind = DonorAssetKind.Unknown;
        [SerializeField] private DonorTransferClassification classification =
            DonorTransferClassification.ReferenceOnly;
        [SerializeField] private string stagedRelativePath = string.Empty;
        [SerializeField] private string referenceAssetPath = string.Empty;
        [SerializeField] private string productionReplacementPath = string.Empty;
        [SerializeField] private DonorAssetStatus status = DonorAssetStatus.Planned;
        [SerializeField] private string importerId = string.Empty;
        [SerializeField] private string importerVersion = string.Empty;
        [SerializeField] private List<string> dependencies = new List<string>();
        [SerializeField] private string notes = string.Empty;

        public DonorAssetRecord(
            string recordId,
            string sourceRelativePath,
            string sourceObjectName,
            string sourceSha256,
            string stagedFileSha256,
            DonorAssetKind kind,
            DonorTransferClassification classification,
            string stagedRelativePath,
            string referenceAssetPath,
            string productionReplacementPath,
            DonorAssetStatus status,
            string importerId,
            string importerVersion,
            IEnumerable<string> dependencies,
            string notes)
        {
            this.recordId = recordId ?? string.Empty;
            this.sourceRelativePath = sourceRelativePath ?? string.Empty;
            this.sourceObjectName = sourceObjectName ?? string.Empty;
            this.sourceSha256 = sourceSha256 ?? string.Empty;
            this.stagedFileSha256 = stagedFileSha256 ?? string.Empty;
            this.kind = kind;
            this.classification = classification;
            this.stagedRelativePath = stagedRelativePath ?? string.Empty;
            this.referenceAssetPath = referenceAssetPath ?? string.Empty;
            this.productionReplacementPath = productionReplacementPath ?? string.Empty;
            this.status = status;
            this.importerId = importerId ?? string.Empty;
            this.importerVersion = importerVersion ?? string.Empty;
            this.dependencies = dependencies == null
                ? new List<string>()
                : new List<string>(dependencies);
            this.notes = notes ?? string.Empty;
        }

        public string RecordId => recordId;
        public string SourceRelativePath => sourceRelativePath;
        public string SourceObjectName => sourceObjectName;
        public string SourceSha256 => sourceSha256;
        public string StagedFileSha256 => stagedFileSha256;
        public DonorAssetKind Kind => kind;
        public DonorTransferClassification Classification => classification;
        public string StagedRelativePath => stagedRelativePath;
        public string ReferenceAssetPath => referenceAssetPath;
        public string ProductionReplacementPath => productionReplacementPath;
        public DonorAssetStatus Status => status;
        public string ImporterId => importerId;
        public string ImporterVersion => importerVersion;
        public IReadOnlyList<string> Dependencies => dependencies;
        public string Notes => notes;

        public DonorAssetRecord WithStatus(DonorAssetStatus newStatus)
        {
            return new DonorAssetRecord(
                RecordId,
                SourceRelativePath,
                SourceObjectName,
                SourceSha256,
                StagedFileSha256,
                Kind,
                Classification,
                StagedRelativePath,
                ReferenceAssetPath,
                ProductionReplacementPath,
                newStatus,
                ImporterId,
                ImporterVersion,
                Dependencies,
                Notes);
        }
    }
}
