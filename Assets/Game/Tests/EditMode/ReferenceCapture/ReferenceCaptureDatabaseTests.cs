using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.ReferenceCapture;
using MSC.Editor.ReferenceCapture;
using NUnit.Framework;

namespace MSC.Tests.EditMode.ReferenceCapture
{
    public sealed class ReferenceCaptureDatabaseTests
    {
        [Test]
        public void ProjectDatabase_ValidatesWithoutStructuralErrors()
        {
            ReferenceCaptureValidationResult result = ValidateProjectData();

            Assert.That(result.IsValid, Is.True, FormatErrors(result));
            Assert.That(result.ErrorCount, Is.Zero);
        }

        [Test]
        public void DatabaseSerialization_IsDeterministicAndRoundTrips()
        {
            ReferenceCaptureDatabase first = LoadDatabase();
            string serialized = first.ToDeterministicJson();
            ReferenceCaptureDatabase second = ReferenceCaptureDatabase.FromJson(serialized);

            Assert.That(second.ToDeterministicJson(), Is.EqualTo(serialized));
            Assert.That(second.Measurements.Length, Is.EqualTo(first.Measurements.Length));
            Assert.That(second.BehaviorFixtures.Length, Is.EqualTo(first.BehaviorFixtures.Length));
            Assert.That(second.Requirements.Length, Is.EqualTo(first.Requirements.Length));
        }

        [Test]
        public void SchemaVersioning_MigratesZeroAndRejectsUnknownFutureSchema()
        {
            string current = LoadDatabaseJson();
            string legacy = ReplaceFirst(current, "\"schemaVersion\": 1", "\"schemaVersion\": 0");
            string future = ReplaceFirst(current, "\"schemaVersion\": 1", "\"schemaVersion\": 99");

            Assert.That(ReferenceCaptureDatabaseMigration.TryMigrate(legacy, out string migrated, out string legacyError), Is.True, legacyError);
            Assert.That(ReferenceCaptureDatabase.FromJson(migrated).Version.SchemaVersion, Is.EqualTo(ReferenceCaptureDatabaseVersion.CurrentSchemaVersion));
            Assert.That(ReferenceCaptureDatabaseMigration.TryMigrate(future, out _, out string futureError), Is.False);
            Assert.That(futureError, Does.Contain("Unsupported"));
        }

        [Test]
        public void StableRecordIds_AreCanonicalDeterministicAndKeySensitive()
        {
            ReferenceStableId first = ReferenceStableIdUtility.Create("04b.2|vehicle|rear-drum|pivot");
            ReferenceStableId second = ReferenceStableIdUtility.Create("04B.2|VEHICLE|REAR-DRUM|PIVOT");
            ReferenceStableId other = ReferenceStableIdUtility.Create("04b.2|vehicle|rear-drum|mount");

            Assert.That(ReferenceStableId.IsCanonical(first.Value), Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(other));
            Assert.That(LoadDatabase().Records.All(record => ReferenceStableId.IsCanonical(record.StableId)), Is.True);
        }

        [Test]
        public void DuplicateDetection_ReportsDuplicateSourceId()
        {
            string invalidJson = LoadDatabaseJson().Replace(
                "\"stableId\": \"dd1f1d55df54bc87a1882f364e30ed21\"",
                "\"stableId\": \"b4c800a2b1afe2fbffaec16f127cff5b\"");

            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(ReferenceCaptureDatabase.FromJson(invalidJson));

            Assert.That(result.Issues.Any(issue => issue.Code == "SOURCE_DUPLICATE"), Is.True, FormatErrors(result));
        }

        [Test]
        public void UnitConversion_ConvertsCompatibleFamiliesAndRejectsMismatches()
        {
            Assert.That(ReferenceUnitConversion.TryConvert(36d, ReferenceUnit.KilometerPerHour, ReferenceUnit.MeterPerSecond, out double speed), Is.True);
            Assert.That(speed, Is.EqualTo(10d).Within(0.000001d));
            Assert.That(ReferenceUnitConversion.TryConvert(1000d, ReferenceUnit.Millimeter, ReferenceUnit.Meter, out double length), Is.True);
            Assert.That(length, Is.EqualTo(1d).Within(0.000001d));
            Assert.That(ReferenceUnitConversion.TryConvert(1d, ReferenceUnit.Kilogram, ReferenceUnit.Meter, out _), Is.False);
        }

        [Test]
        public void CoordinateSpaceValidation_RejectsUnknownVectorSpace()
        {
            string invalidJson = ReplaceFirst(
                LoadDatabaseJson(),
                "\"coordinateSpace\": \"DonorWorld\"",
                "\"coordinateSpace\": \"Unknown\"");

            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(ReferenceCaptureDatabase.FromJson(invalidJson));

            Assert.That(result.Issues.Any(issue => issue.Code == "COORDINATE_SPACE"), Is.True, FormatErrors(result));
        }

        [Test]
        public void ProvenanceValidation_RejectsEvidenceWithUnknownSource()
        {
            string invalidJson = ReplaceFirst(
                LoadDatabaseJson(),
                "\"sourceId\": \"dccf28be1d702773b59b4db29a0084d0\", \"rootKind\"",
                "\"sourceId\": \"00000000000000000000000000000000\", \"rootKind\"");

            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(ReferenceCaptureDatabase.FromJson(invalidJson));

            Assert.That(result.Issues.Any(issue => issue.Code == "EVIDENCE_SOURCE"), Is.True, FormatErrors(result));
        }

        [Test]
        public void ConfidenceRules_RejectHighConfidenceApproximation()
        {
            string invalidJson = ReplaceFirst(
                LoadDatabaseJson(),
                "\"captureMethod\": \"AssetMetadata\"",
                "\"captureMethod\": \"Approximation\"");

            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(ReferenceCaptureDatabase.FromJson(invalidJson));

            Assert.That(result.Issues.Any(issue => issue.Code == "CONFIDENCE"), Is.True, FormatErrors(result));
        }

        [Test]
        public void DerivedCalculation_RejectsUnresolvedDependency()
        {
            string invalidJson = ReplaceFirst(
                LoadDatabaseJson(),
                "\"dependencyRecordIds\": [\"d9cca0c1e9333bb80820b86eaaa7afbb\"]",
                "\"dependencyRecordIds\": [\"00000000000000000000000000000000\"]");

            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(ReferenceCaptureDatabase.FromJson(invalidJson));

            Assert.That(result.Issues.Any(issue => issue.Code == "DERIVED_DEPENDENCY"), Is.True, FormatErrors(result));
        }

        [Test]
        public void MeasuredAndTunedValues_AreStoredAndValidatedSeparately()
        {
            string measuredJson = LoadDatabaseJson();
            ReferenceCaptureDatabase database = ReferenceCaptureDatabase.FromJson(measuredJson);
            ReferenceTuningOverrideSet tuning = LoadTuning();
            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(database, tuning);

            Assert.That(measuredJson, Does.Not.Contain("tunedValue"));
            Assert.That(tuning.Overrides.Length, Is.GreaterThan(0));
            Assert.That(tuning.Overrides.All(item => database.FindRecord(item.MeasuredRecordId) != null), Is.True);
            Assert.That(result.IsValid, Is.True, FormatErrors(result));
        }

        [Test]
        public void MissingP0Reporting_ReturnsExplicitCaptureQueue()
        {
            ReferenceCaptureValidationResult result = ValidateProjectData();

            Assert.That(result.MissingP0Requirements.Count, Is.EqualTo(20));
            Assert.That(result.MissingP1Requirements.Count, Is.EqualTo(6));
            Assert.That(result.MissingP0Requirements.Any(item => item.RequirementId == "P0-ASSEMBLY-MOUNT-RULE"), Is.False);
            Assert.That(result.MissingP0Requirements.Any(item => item.RequirementId == "P0-ASSEMBLY-FASTENER-SEMANTICS"), Is.False);
            Assert.That(result.MissingP0Requirements.Any(item => item.RequirementId == "P0-ENGINE-IDLE"), Is.True);
        }

        [Test]
        public void RearDrumCombinedEvidence_CoversBothMilestone05AssemblyGates()
        {
            ReferenceCaptureDatabase database = LoadDatabase();
            ReferenceRequirementRecord mountRule = database.Requirements.Single(
                item => item.RequirementId == "P0-ASSEMBLY-MOUNT-RULE");
            ReferenceRequirementRecord fastenerRule = database.Requirements.Single(
                item => item.RequirementId == "P0-ASSEMBLY-FASTENER-SEMANTICS");
            ReferenceRecord triggerRadius = database.FindRecord("9c6544a9f3852df5e34ffe12d5248867");
            ReferenceRecord stageContract = database.FindRecord("670da877660d6dfe7b0639bf7c1fe0c2");
            BehaviorFixture fixture = database.BehaviorFixtures.Single(
                item => item.FixtureId == "representative-part-mount-pivot-v1");

            Assert.That(mountRule.Status, Is.EqualTo(ReferenceRequirementStatus.Covered));
            Assert.That(fastenerRule.Status, Is.EqualTo(ReferenceRequirementStatus.Covered));
            Assert.That(triggerRadius, Is.Not.Null);
            Assert.That(triggerRadius.NormalizedValue.NumericValue, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(stageContract, Is.Not.Null);
            Assert.That(stageContract.NormalizedValue.NumericValue, Is.EqualTo(8f));
            Assert.That(stageContract.ValidationStatus, Is.EqualTo(ReferenceValidationStatus.Validated));
            Assert.That(database.FindRecord("e7aedfd32241bd4ab85987282e4073d7"), Is.Not.Null);
            Assert.That(database.FindRecord("b9af6c2398d63e5f3f84fe5f939b5a09"), Is.Not.Null);
            Assert.That(fixture.Status, Is.EqualTo(ReferenceFixtureStatus.Ready));
            Assert.That(fixture.RepeatedTrialsRequired, Is.EqualTo(3));
        }

        [Test]
        public void CalibrationFixtures_AllRequiredFilesLoadAndResolveRecords()
        {
            ReferenceCaptureDatabase database = LoadDatabase();
            string fixtureRoot = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.FixtureAssetRoot);
            string[] files = Directory.GetFiles(fixtureRoot, "*.json", SearchOption.TopDirectoryOnly);
            var fixtureIds = new HashSet<string>(StringComparer.Ordinal);

            Assert.That(files.Length, Is.EqualTo(11));
            foreach (string file in files)
            {
                ReferenceCalibrationFixture fixture = ReferenceCalibrationFixture.FromJson(File.ReadAllText(file));
                Assert.That(fixture.SchemaVersion, Is.EqualTo(ReferenceCaptureDatabaseVersion.CurrentSchemaVersion), file);
                Assert.That(fixture.DatasetVersion, Is.EqualTo(ReferenceCaptureDatabaseVersion.CurrentDatasetVersion), file);
                Assert.That(fixtureIds.Add(fixture.FixtureId), Is.True, "Duplicate fixture ID: " + fixture.FixtureId);
                Assert.That(fixture.Inputs, Is.Not.Empty, file);
                Assert.That(fixture.Expected, Is.Not.Empty, file);
                Assert.That(fixture.RecordIds.All(id => database.FindRecord(id) != null), Is.True, file);
            }
        }

        private static ReferenceCaptureValidationResult ValidateProjectData() =>
            ReferenceCaptureValidator.Validate(LoadDatabase(), LoadTuning());

        private static ReferenceCaptureDatabase LoadDatabase() =>
            ReferenceCaptureDatabase.FromJson(LoadDatabaseJson());

        private static ReferenceTuningOverrideSet LoadTuning() => ReferenceTuningOverrideSet.FromJson(
            File.ReadAllText(ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.TuningOverrideAssetPath)));

        private static string LoadDatabaseJson() =>
            File.ReadAllText(ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.DatabaseAssetPath));

        private static string ReplaceFirst(string input, string oldValue, string newValue)
        {
            int index = input.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Test mutation token was not found: " + oldValue);
            return input.Substring(0, index) + newValue + input.Substring(index + oldValue.Length);
        }

        private static string FormatErrors(ReferenceCaptureValidationResult result) =>
            string.Join(Environment.NewLine, result.Issues.Select(issue => issue.ToString()));
    }
}
