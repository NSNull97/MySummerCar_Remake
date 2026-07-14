using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport
{
    [CreateAssetMenu(fileName = "DonorAssetRegistry", menuName = "My Summer Car/Legacy Import/Donor Asset Registry")]
    public sealed class DonorAssetRegistry : ScriptableObject
    {
        [SerializeField] private int manifestSchemaVersion;
        [SerializeField] private string manifestId = string.Empty;
        [SerializeField] private string pipelineVersion = string.Empty;
        [SerializeField] private List<DonorAssetRecord> records = new List<DonorAssetRecord>();

        public int ManifestSchemaVersion => manifestSchemaVersion;
        public string ManifestId => manifestId;
        public string PipelineVersion => pipelineVersion;
        public IReadOnlyList<DonorAssetRecord> Records => records;

        public bool TryGetRecord(string recordId, out DonorAssetRecord record)
        {
            foreach (DonorAssetRecord candidate in records)
            {
                if (candidate != null && candidate.RecordId == recordId)
                {
                    record = candidate;
                    return true;
                }
            }

            record = null;
            return false;
        }

        public void ApplyManifest(DonorAssetManifest manifest)
        {
            if (manifest == null)
            {
                throw new System.ArgumentNullException(nameof(manifest));
            }

            manifestSchemaVersion = manifest.SchemaVersion;
            manifestId = manifest.ManifestId;
            pipelineVersion = manifest.PipelineVersion;
            records = new List<DonorAssetRecord>(manifest.Records);
        }
    }
}
