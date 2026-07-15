using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Core.ReferenceCapture
{
    public enum ReferenceValidationSeverity { Warning, Error }

    public readonly struct ReferenceValidationIssue
    {
        public ReferenceValidationIssue(ReferenceValidationSeverity severity, string code, string recordId, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ReferenceValidationSeverity Severity { get; }
        public string Code { get; }
        public string RecordId { get; }
        public string Message { get; }
        public override string ToString() => $"{Severity} {Code} {RecordId}: {Message}";
    }

    public sealed class ReferenceCaptureValidationResult
    {
        private readonly List<ReferenceValidationIssue> issues = new List<ReferenceValidationIssue>();
        private readonly List<ReferenceRequirementRecord> missingP0 = new List<ReferenceRequirementRecord>();
        private readonly List<ReferenceRequirementRecord> missingP1 = new List<ReferenceRequirementRecord>();

        public IReadOnlyList<ReferenceValidationIssue> Issues => issues;
        public IReadOnlyList<ReferenceRequirementRecord> MissingP0Requirements => missingP0;
        public IReadOnlyList<ReferenceRequirementRecord> MissingP1Requirements => missingP1;
        public bool IsValid => issues.All(issue => issue.Severity != ReferenceValidationSeverity.Error);
        public int ErrorCount => issues.Count(issue => issue.Severity == ReferenceValidationSeverity.Error);
        public int WarningCount => issues.Count(issue => issue.Severity == ReferenceValidationSeverity.Warning);

        internal void AddError(string code, string recordId, string message) =>
            issues.Add(new ReferenceValidationIssue(ReferenceValidationSeverity.Error, code, recordId, message));

        internal void AddWarning(string code, string recordId, string message) =>
            issues.Add(new ReferenceValidationIssue(ReferenceValidationSeverity.Warning, code, recordId, message));

        internal void AddMissing(ReferenceRequirementRecord requirement)
        {
            if (requirement.Priority == ReferencePriority.P0) missingP0.Add(requirement);
            else if (requirement.Priority == ReferencePriority.P1) missingP1.Add(requirement);
        }
    }

    public static class ReferenceCaptureValidator
    {
        public static ReferenceCaptureValidationResult Validate(
            ReferenceCaptureDatabase database,
            ReferenceTuningOverrideSet tuningOverrides = null)
        {
            var result = new ReferenceCaptureValidationResult();
            if (database == null)
            {
                result.AddError("DB_NULL", string.Empty, "Reference database is null.");
                return result;
            }

            if (database.Version.SchemaVersion != ReferenceCaptureDatabaseVersion.CurrentSchemaVersion)
                result.AddError("SCHEMA", string.Empty, $"Unsupported schema {database.Version.SchemaVersion}.");
            if (string.IsNullOrWhiteSpace(database.Version.DatasetVersion))
                result.AddError("DATASET_VERSION", string.Empty, "Dataset version is missing.");

            Dictionary<string, ReferenceSourceRecord> sources = BuildUniqueMap(
                database.Sources, source => source.StableId, "SOURCE_DUPLICATE", result);
            Dictionary<string, ReferenceEvidenceRecord> evidence = BuildUniqueMap(
                database.Evidence, item => item.StableId, "EVIDENCE_DUPLICATE", result);

            foreach (ReferenceSourceRecord source in database.Sources)
                ValidateSource(source, result);
            foreach (ReferenceEvidenceRecord item in database.Evidence)
                ValidateEvidence(item, sources, result);

            ReferenceRecord[] records = database.Records.ToArray();
            Dictionary<string, ReferenceRecord> recordsById = BuildUniqueMap(
                records, record => record.StableId, "RECORD_DUPLICATE", result);

            foreach (ReferenceRecord record in records)
                ValidateRecord(record, sources, evidence, recordsById, result);

            ValidateDerivedDependencyCycles(recordsById, result);
            ValidateRequirements(database.Requirements, recordsById, result);
            ValidateBehaviorFixtures(database.BehaviorFixtures, recordsById, result);
            if (tuningOverrides != null) ValidateTuningOverrides(tuningOverrides, recordsById, result);
            return result;
        }

        private static void ValidateSource(ReferenceSourceRecord source, ReferenceCaptureValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(source.SourceKind) || string.IsNullOrWhiteSpace(source.LogicalPath))
                result.AddError("SOURCE_METADATA", source.StableId, "Source kind and logical path are required.");

            bool projectProcedure = string.Equals(source.SourceKind, "ProjectCaptureProcedure", StringComparison.Ordinal);
            if (!projectProcedure && (source.Sha256.Length != 64 || source.Sha256.Any(character => !Uri.IsHexDigit(character))))
                result.AddError("SOURCE_HASH", source.StableId, "Non-project source requires a 64-character SHA-256 hash.");
        }

        private static void ValidateEvidence(
            ReferenceEvidenceRecord evidence,
            IReadOnlyDictionary<string, ReferenceSourceRecord> sources,
            ReferenceCaptureValidationResult result)
        {
            if (!sources.ContainsKey(evidence.SourceId))
                result.AddError("EVIDENCE_SOURCE", evidence.StableId, "Evidence source ID does not resolve.");
            if (string.IsNullOrWhiteSpace(evidence.RootKind) ||
                string.IsNullOrWhiteSpace(evidence.LogicalPath) ||
                string.IsNullOrWhiteSpace(evidence.EvidenceType))
                result.AddError("EVIDENCE_METADATA", evidence.StableId, "Evidence root, logical path and type are required.");
        }

        private static Dictionary<string, T> BuildUniqueMap<T>(
            IEnumerable<T> values,
            Func<T, string> idSelector,
            string duplicateCode,
            ReferenceCaptureValidationResult result)
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T value in values ?? Enumerable.Empty<T>())
            {
                string id = idSelector(value) ?? string.Empty;
                if (!ReferenceStableId.IsCanonical(id))
                {
                    result.AddError("STABLE_ID", id, "Stable ID is not canonical.");
                    continue;
                }
                if (!map.TryAdd(id, value)) result.AddError(duplicateCode, id, "Duplicate stable ID.");
            }
            return map;
        }

        private static void ValidateRecord(
            ReferenceRecord record,
            IReadOnlyDictionary<string, ReferenceSourceRecord> sources,
            IReadOnlyDictionary<string, ReferenceEvidenceRecord> evidence,
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ReferenceCaptureValidationResult result)
        {
            string id = record.StableId;
            if (string.IsNullOrWhiteSpace(record.Subcategory) || string.IsNullOrWhiteSpace(record.Name))
                result.AddError("IDENTITY", id, "Subcategory and name are required.");
            if (record.Unit == ReferenceUnit.Unknown)
                result.AddError("UNIT", id, "Unit must be explicit.");
            if (record.NormalizedValue.Kind == ReferenceValueKind.Vector3 &&
                (record.CoordinateSpace == ReferenceCoordinateSpace.Unknown || record.CoordinateSpace == ReferenceCoordinateSpace.NotApplicable))
                result.AddError("COORDINATE_SPACE", id, "Vector records require an explicit coordinate space.");
            if (record.NormalizedValue.Kind != ReferenceValueKind.Vector3 && record.CoordinateSpace == ReferenceCoordinateSpace.Unknown)
                result.AddError("COORDINATE_SPACE", id, "Coordinate space must be explicit, including NotApplicable.");

            if (!IsFinite(record.NormalizedValue.NumericValue) || !IsFinite(record.NormalizedValue.VectorValue))
                result.AddError("NON_FINITE", id, "Normalized value contains NaN or infinity.");
            if (record.Tolerance.Absolute < 0f || record.Tolerance.RelativePercent < 0f)
                result.AddError("TOLERANCE", id, "Tolerance values cannot be negative.");
            if (string.IsNullOrWhiteSpace(record.RawObservation))
                result.AddError("RAW_OBSERVATION", id, "Raw observation is required.");
            if (!sources.ContainsKey(record.SourceId))
                result.AddError("SOURCE", id, "Source ID does not resolve.");
            if (record.EvidenceIds.Length == 0)
                result.AddError("EVIDENCE", id, "At least one evidence record is required.");
            foreach (string evidenceId in record.EvidenceIds)
                if (!evidence.ContainsKey(evidenceId)) result.AddError("EVIDENCE", id, "Evidence ID does not resolve: " + evidenceId);

            ValidateConfidence(record, result);
            if (record.CaptureMethod == ReferenceCaptureMethod.DerivedCalculation)
            {
                if (record.DependencyRecordIds.Length == 0)
                    result.AddError("DERIVED_DEPENDENCY", id, "Derived calculations require dependencies.");
                foreach (string dependency in record.DependencyRecordIds)
                    if (!records.ContainsKey(dependency)) result.AddError("DERIVED_DEPENDENCY", id, "Dependency does not resolve: " + dependency);
            }
            else if (record.DependencyRecordIds.Length > 0)
            {
                result.AddWarning("UNUSED_DEPENDENCY", id, "Non-derived record declares dependencies.");
            }
        }

        private static void ValidateConfidence(ReferenceRecord record, ReferenceCaptureValidationResult result)
        {
            string id = record.StableId;
            if (record.ValidationStatus == ReferenceValidationStatus.Missing)
            {
                if (record.Confidence != ReferenceConfidence.Unknown || record.CaptureMethod != ReferenceCaptureMethod.Unknown)
                    result.AddError("MISSING_CONFIDENCE", id, "Missing records must use Unknown method and confidence.");
                return;
            }

            if (record.Confidence == ReferenceConfidence.Unknown)
                result.AddError("CONFIDENCE", id, "Observed records require a non-Unknown confidence.");
            if (record.CaptureMethod == ReferenceCaptureMethod.Approximation &&
                (record.Confidence == ReferenceConfidence.High || record.Confidence == ReferenceConfidence.Exact))
                result.AddError("CONFIDENCE", id, "Approximation cannot have High or Exact confidence.");
            if (record.Confidence == ReferenceConfidence.Exact &&
                (record.CaptureMethod == ReferenceCaptureMethod.ManualMeasurement ||
                 record.CaptureMethod == ReferenceCaptureMethod.VideoTiming ||
                 record.CaptureMethod == ReferenceCaptureMethod.ScreenshotMeasurement ||
                 record.CaptureMethod == ReferenceCaptureMethod.RuntimeObservation ||
                 record.CaptureMethod == ReferenceCaptureMethod.DerivedCalculation))
                result.AddError("CONFIDENCE", id, "This capture method cannot claim Exact confidence.");
        }

        private static void ValidateDerivedDependencyCycles(
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ReferenceCaptureValidationResult result)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in records.Keys) Visit(id, records, visiting, visited, result);
        }

        private static void Visit(
            string id,
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ISet<string> visiting,
            ISet<string> visited,
            ReferenceCaptureValidationResult result)
        {
            if (visited.Contains(id) || !records.TryGetValue(id, out ReferenceRecord record)) return;
            if (!visiting.Add(id))
            {
                result.AddError("DERIVED_CYCLE", id, "Derived-calculation dependency cycle detected.");
                return;
            }
            if (record.CaptureMethod == ReferenceCaptureMethod.DerivedCalculation)
                foreach (string dependency in record.DependencyRecordIds) Visit(dependency, records, visiting, visited, result);
            visiting.Remove(id);
            visited.Add(id);
        }

        private static void ValidateRequirements(
            IEnumerable<ReferenceRequirementRecord> requirements,
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ReferenceCaptureValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReferenceRequirementRecord requirement in requirements ?? Enumerable.Empty<ReferenceRequirementRecord>())
            {
                if (string.IsNullOrWhiteSpace(requirement.RequirementId) || !ids.Add(requirement.RequirementId))
                    result.AddError("REQUIREMENT_ID", requirement.RequirementId, "Requirement ID is missing or duplicated.");
                if (requirement.Status != ReferenceRequirementStatus.Covered) result.AddMissing(requirement);
                if (requirement.Status == ReferenceRequirementStatus.Covered && requirement.CoveredByRecordIds.Length == 0)
                    result.AddError("REQUIREMENT_COVERAGE", requirement.RequirementId, "Covered requirement has no record IDs.");
                foreach (string recordId in requirement.CoveredByRecordIds)
                    if (!records.ContainsKey(recordId)) result.AddError("REQUIREMENT_COVERAGE", requirement.RequirementId, "Coverage record does not resolve: " + recordId);
            }
        }

        private static void ValidateBehaviorFixtures(
            IEnumerable<BehaviorFixture> fixtures,
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ReferenceCaptureValidationResult result)
        {
            var fixtureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BehaviorFixture fixture in fixtures ?? Enumerable.Empty<BehaviorFixture>())
            {
                if (string.IsNullOrWhiteSpace(fixture.FixtureId) || !fixtureIds.Add(fixture.FixtureId))
                    result.AddError("FIXTURE_ID", fixture.FixtureId, "Fixture ID is missing or duplicated.");
                foreach (string recordId in fixture.RelatedRecordIds)
                    if (!records.ContainsKey(recordId)) result.AddError("FIXTURE_RECORD", fixture.FixtureId, "Fixture record does not resolve: " + recordId);
                if (fixture.Status == ReferenceFixtureStatus.Ready && fixture.MissingRequirementIds.Length > 0)
                    result.AddError("FIXTURE_STATUS", fixture.FixtureId, "Ready fixture still declares missing requirements.");
            }
        }

        private static void ValidateTuningOverrides(
            ReferenceTuningOverrideSet tuning,
            IReadOnlyDictionary<string, ReferenceRecord> records,
            ReferenceCaptureValidationResult result)
        {
            if (tuning.SchemaVersion != ReferenceCaptureDatabaseVersion.CurrentSchemaVersion)
                result.AddError("TUNING_SCHEMA", string.Empty, "Tuning override schema is unsupported.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var measuredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReferenceTuningOverride item in tuning.Overrides)
            {
                if (!ReferenceStableId.IsCanonical(item.StableId) || !ids.Add(item.StableId))
                    result.AddError("TUNING_ID", item.StableId, "Tuning ID is invalid or duplicated.");
                if (!records.ContainsKey(item.MeasuredRecordId))
                    result.AddError("TUNING_TARGET", item.StableId, "Measured record does not resolve.");
                if (!measuredIds.Add(item.MeasuredRecordId))
                    result.AddError("TUNING_TARGET", item.StableId, "Multiple overrides target the same measured record.");
                if (item.Unit == ReferenceUnit.Unknown)
                    result.AddError("TUNING_UNIT", item.StableId, "Tuning unit must be explicit.");
                if (records.TryGetValue(item.MeasuredRecordId, out ReferenceRecord measured) && item.Unit != measured.Unit)
                    result.AddError("TUNING_UNIT", item.StableId, "Tuning unit must match the measured record unit.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
