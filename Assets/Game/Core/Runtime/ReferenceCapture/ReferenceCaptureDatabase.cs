using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Core.ReferenceCapture
{
    public static class ReferenceCaptureDatabaseVersion
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentDatasetVersion = "04B.4";
    }

    [Serializable]
    public sealed class ReferenceCaptureDatabase
    {
        [SerializeField] private ReferenceDatasetVersion version = new ReferenceDatasetVersion();
        [SerializeField] private ReferenceSourceRecord[] sources = Array.Empty<ReferenceSourceRecord>();
        [SerializeField] private ReferenceEvidenceRecord[] evidence = Array.Empty<ReferenceEvidenceRecord>();
        [SerializeField] private MeasurementRecord[] measurements = Array.Empty<MeasurementRecord>();
        [SerializeField] private BehaviorFixture[] behaviorFixtures = Array.Empty<BehaviorFixture>();
        [SerializeField] private ReferenceRequirementRecord[] requirements = Array.Empty<ReferenceRequirementRecord>();

        public ReferenceDatasetVersion Version => version ?? new ReferenceDatasetVersion();
        public ReferenceSourceRecord[] Sources => sources ?? Array.Empty<ReferenceSourceRecord>();
        public ReferenceEvidenceRecord[] Evidence => evidence ?? Array.Empty<ReferenceEvidenceRecord>();
        public MeasurementRecord[] Measurements => measurements ?? Array.Empty<MeasurementRecord>();
        public BehaviorFixture[] BehaviorFixtures => behaviorFixtures ?? Array.Empty<BehaviorFixture>();
        public ReferenceRequirementRecord[] Requirements => requirements ?? Array.Empty<ReferenceRequirementRecord>();
        public IEnumerable<ReferenceRecord> Records => Measurements.Select(item => item.Record)
            .Concat(BehaviorFixtures.Select(item => item.Record));

        public IEnumerable<ReferenceRecord> Query(ReferenceCategory category) => Records.Where(record => record.Category == category);

        public ReferenceRecord FindRecord(string stableId) =>
            Records.FirstOrDefault(record => string.Equals(record.StableId, stableId, StringComparison.Ordinal));

        public static ReferenceCaptureDatabase FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Reference capture database JSON must not be empty.", nameof(json));
            ReferenceCaptureDatabase database = JsonUtility.FromJson<ReferenceCaptureDatabase>(json);
            return database ?? throw new FormatException("Reference capture database JSON could not be parsed.");
        }

        public static ReferenceCaptureDatabase Create(
            ReferenceDatasetVersion datasetVersion,
            ReferenceSourceRecord[] sourceRecords,
            ReferenceEvidenceRecord[] evidenceRecords,
            MeasurementRecord[] measurementRecords,
            BehaviorFixture[] fixtures,
            ReferenceRequirementRecord[] requirementRecords) => new ReferenceCaptureDatabase
        {
            version = datasetVersion ?? new ReferenceDatasetVersion(),
            sources = sourceRecords ?? Array.Empty<ReferenceSourceRecord>(),
            evidence = evidenceRecords ?? Array.Empty<ReferenceEvidenceRecord>(),
            measurements = measurementRecords ?? Array.Empty<MeasurementRecord>(),
            behaviorFixtures = fixtures ?? Array.Empty<BehaviorFixture>(),
            requirements = requirementRecords ?? Array.Empty<ReferenceRequirementRecord>()
        };

        public ReferenceCaptureDatabase Merge(ReferenceEvidenceRecord[] additionalEvidence, MeasurementRecord[] additionalMeasurements)
        {
            ReferenceEvidenceRecord[] mergedEvidence = Evidence.Concat(additionalEvidence ?? Array.Empty<ReferenceEvidenceRecord>())
                .GroupBy(item => item.StableId, StringComparer.Ordinal).Select(group => group.Last()).ToArray();
            MeasurementRecord[] mergedMeasurements = Measurements.Concat(additionalMeasurements ?? Array.Empty<MeasurementRecord>())
                .GroupBy(item => item.Record.StableId, StringComparer.Ordinal).Select(group => group.Last()).ToArray();
            return Create(Version, Sources, mergedEvidence, mergedMeasurements, BehaviorFixtures, Requirements);
        }

        public string ToDeterministicJson(bool prettyPrint = true)
        {
            ReferenceCaptureDatabase ordered = Create(
                Version,
                Sources.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray(),
                Evidence.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray(),
                Measurements.OrderBy(item => item.Record.StableId, StringComparer.Ordinal).ToArray(),
                BehaviorFixtures.OrderBy(item => item.FixtureId, StringComparer.Ordinal).ToArray(),
                Requirements.OrderBy(item => item.RequirementId, StringComparer.Ordinal).ToArray());
            return JsonUtility.ToJson(ordered, prettyPrint) + "\n";
        }
    }

    public static class ReferenceCaptureDatabaseMigration
    {
        public static bool TryMigrate(string json, out string migratedJson, out string error)
        {
            migratedJson = string.Empty;
            error = string.Empty;
            try
            {
                ReferenceCaptureDatabase database = ReferenceCaptureDatabase.FromJson(json);
                if (database.Version.SchemaVersion == ReferenceCaptureDatabaseVersion.CurrentSchemaVersion)
                {
                    migratedJson = json;
                    return true;
                }

                if (database.Version.SchemaVersion == 0)
                {
                    migratedJson = json.Replace("\"schemaVersion\": 0", "\"schemaVersion\": 1")
                        .Replace("\"schemaVersion\":0", "\"schemaVersion\":1");
                    return true;
                }

                error = $"Unsupported reference database schema {database.Version.SchemaVersion}.";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }

    [Serializable]
    public sealed class ReferenceTuningOverrideSet
    {
        [SerializeField] private int schemaVersion = ReferenceCaptureDatabaseVersion.CurrentSchemaVersion;
        [SerializeField] private string datasetVersion = ReferenceCaptureDatabaseVersion.CurrentDatasetVersion;
        [SerializeField] private ReferenceTuningOverride[] overrides = Array.Empty<ReferenceTuningOverride>();

        public int SchemaVersion => schemaVersion;
        public string DatasetVersion => datasetVersion ?? string.Empty;
        public ReferenceTuningOverride[] Overrides => overrides ?? Array.Empty<ReferenceTuningOverride>();

        public static ReferenceTuningOverrideSet FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Tuning override JSON must not be empty.", nameof(json));
            return JsonUtility.FromJson<ReferenceTuningOverrideSet>(json) ?? throw new FormatException("Tuning override JSON could not be parsed.");
        }
    }

    [Serializable]
    public sealed class ReferenceCalibrationFixture
    {
        [SerializeField] private int schemaVersion = ReferenceCaptureDatabaseVersion.CurrentSchemaVersion;
        [SerializeField] private string datasetVersion = ReferenceCaptureDatabaseVersion.CurrentDatasetVersion;
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string category = nameof(ReferenceCategory.WorldLandmarks);
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string priority = nameof(ReferencePriority.P0);
        [SerializeField] private string status = nameof(ReferenceFixtureStatus.Missing);
        [SerializeField] private string[] recordIds = Array.Empty<string>();
        [SerializeField] private string[] missingRequirementIds = Array.Empty<string>();
        [SerializeField] private string[] inputs = Array.Empty<string>();
        [SerializeField] private string[] expected = Array.Empty<string>();
        [SerializeField] private string toleranceNotes = string.Empty;

        public int SchemaVersion => schemaVersion;
        public string DatasetVersion => datasetVersion ?? string.Empty;
        public string FixtureId => fixtureId ?? string.Empty;
        public ReferenceCategory Category => Enum.TryParse(category, true, out ReferenceCategory parsed) ? parsed : ReferenceCategory.WorldLandmarks;
        public string Title => title ?? string.Empty;
        public ReferencePriority Priority => Enum.TryParse(priority, true, out ReferencePriority parsed) ? parsed : ReferencePriority.P0;
        public ReferenceFixtureStatus Status => Enum.TryParse(status, true, out ReferenceFixtureStatus parsed) ? parsed : ReferenceFixtureStatus.Missing;
        public string[] RecordIds => recordIds ?? Array.Empty<string>();
        public string[] MissingRequirementIds => missingRequirementIds ?? Array.Empty<string>();
        public string[] Inputs => inputs ?? Array.Empty<string>();
        public string[] Expected => expected ?? Array.Empty<string>();
        public string ToleranceNotes => toleranceNotes ?? string.Empty;

        public static ReferenceCalibrationFixture FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Calibration fixture JSON must not be empty.", nameof(json));
            return JsonUtility.FromJson<ReferenceCalibrationFixture>(json) ?? throw new FormatException("Calibration fixture JSON could not be parsed.");
        }
    }

    [Serializable]
    public sealed class ReferenceCaptureImportEnvelope
    {
        [SerializeField] private int schemaVersion = ReferenceCaptureDatabaseVersion.CurrentSchemaVersion;
        [SerializeField] private string datasetVersion = ReferenceCaptureDatabaseVersion.CurrentDatasetVersion;
        [SerializeField] private ReferenceEvidenceRecord[] evidence = Array.Empty<ReferenceEvidenceRecord>();
        [SerializeField] private MeasurementRecord[] measurements = Array.Empty<MeasurementRecord>();

        public int SchemaVersion => schemaVersion;
        public string DatasetVersion => datasetVersion ?? string.Empty;
        public ReferenceEvidenceRecord[] Evidence => evidence ?? Array.Empty<ReferenceEvidenceRecord>();
        public MeasurementRecord[] Measurements => measurements ?? Array.Empty<MeasurementRecord>();

        public static ReferenceCaptureImportEnvelope FromJson(string json) =>
            JsonUtility.FromJson<ReferenceCaptureImportEnvelope>(json) ?? throw new FormatException("Structured reference import JSON could not be parsed.");
    }
}
