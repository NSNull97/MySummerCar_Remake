using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport
{
    [Serializable]
    public sealed class DonorAssetManifest
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string manifestId = string.Empty;
        [SerializeField] private string generatedUtc = string.Empty;
        [SerializeField] private string pipelineVersion = string.Empty;
        [SerializeField] private List<DonorAssetRecord> records = new List<DonorAssetRecord>();

        public DonorAssetManifest(
            string manifestId,
            string generatedUtc,
            string pipelineVersion,
            IEnumerable<DonorAssetRecord> records)
        {
            schemaVersion = CurrentSchemaVersion;
            this.manifestId = manifestId ?? string.Empty;
            this.generatedUtc = generatedUtc ?? string.Empty;
            this.pipelineVersion = pipelineVersion ?? string.Empty;
            this.records = records == null
                ? new List<DonorAssetRecord>()
                : new List<DonorAssetRecord>(records);
        }

        public int SchemaVersion => schemaVersion;
        public string ManifestId => manifestId;
        public string GeneratedUtc => generatedUtc;
        public string PipelineVersion => pipelineVersion;
        public IReadOnlyList<DonorAssetRecord> Records => records;

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static DonorAssetManifest FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Manifest JSON must not be empty.", nameof(json));
            }

            DonorAssetManifest manifest = JsonUtility.FromJson<DonorAssetManifest>(json);
            if (manifest == null)
            {
                throw new FormatException("Manifest JSON could not be parsed.");
            }

            return manifest;
        }

        public DonorAssetManifest WithRecords(IEnumerable<DonorAssetRecord> replacementRecords)
        {
            return new DonorAssetManifest(
                ManifestId,
                GeneratedUtc,
                PipelineVersion,
                replacementRecords);
        }
    }
}
