using System;
using System.IO;
using System.Linq;
using MSC.Editor.Validation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.SaveCoverage
{
    public sealed class FullGameSaveCoverageTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "msc-save-coverage-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void AuthoritativeProjectCoverage_IsOneToOneAndValid()
        {
            var issues = FullGameSaveCoverageValidator.ValidateProjectCoverage();

            Assert.That(issues, Is.Empty, string.Join(Environment.NewLine, issues));
        }

        [Test]
        public void MissingParityFeature_IsRejected()
        {
            var paths = WriteFixture(
                MatrixHeader + "\n" +
                MatrixRow("P1.TEST.001") + "\n" +
                MatrixRow("P1.TEST.002"),
                CoverageHeader + "\n" +
                CoverageRow("P1.TEST.001") + "\n" +
                ContractRow());

            var issues = FullGameSaveCoverageValidator.Validate(paths.Matrix, paths.Coverage);

            Assert.That(issues.Any(issue => issue.Contains("1:1 coverage missing parity FeatureId 'P1.TEST.002'")), Is.True);
        }

        [Test]
        public void DuplicateFeatureAndInvalidDomainAndStatus_AreRejected()
        {
            var invalid = CoverageRow("P1.TEST.001", "Invalid Domain", "Persistent", "Verified");
            var paths = WriteFixture(
                MatrixHeader + "\n" + MatrixRow("P1.TEST.001"),
                CoverageHeader + "\n" + invalid + "\n" + invalid + "\n" + ContractRow());

            var issues = FullGameSaveCoverageValidator.Validate(paths.Matrix, paths.Coverage);

            Assert.That(issues.Any(issue => issue.Contains("duplicate FeatureId 'P1.TEST.001'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("invalid SaveDomainId 'Invalid Domain'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("invalid CoverageStatus 'Verified'")), Is.True);
        }

        [Test]
        public void NonPersistentRowWithoutReason_IsRejected()
        {
            var row = Csv(
                "P1.TEST.001", "test.state", "NonPersistent", "NotRequired", "Test.Owner",
                "Implemented", string.Empty, "NotApplicable", "NotApplicable", "09A", "Fixture");
            var paths = WriteFixture(
                MatrixHeader + "\n" + MatrixRow("P1.TEST.001"),
                CoverageHeader + "\n" + row + "\n" + ContractRow("NonPersistent", "NotRequired"));

            var issues = FullGameSaveCoverageValidator.Validate(paths.Matrix, paths.Coverage);

            Assert.That(issues.Any(issue => issue.Contains("requires an explicit evidence-backed reason")), Is.True);
        }

        [Test]
        public void MissingDomainContract_IsRejected()
        {
            var paths = WriteFixture(
                MatrixHeader + "\n" + MatrixRow("P1.TEST.001"),
                CoverageHeader + "\n" + CoverageRow("P1.TEST.001"));

            var issues = FullGameSaveCoverageValidator.Validate(paths.Matrix, paths.Coverage);

            Assert.That(issues.Any(issue => issue.Contains("no explicit contract row")), Is.True);
        }

        private (string Matrix, string Coverage) WriteFixture(string matrix, string coverage)
        {
            var matrixPath = Path.Combine(temporaryDirectory, "matrix.csv");
            var coveragePath = Path.Combine(temporaryDirectory, "coverage.csv");
            File.WriteAllText(matrixPath, matrix);
            File.WriteAllText(coveragePath, coverage);
            return (matrixPath, coveragePath);
        }

        private static string MatrixRow(string featureId)
        {
            return Csv(featureId, "09A");
        }

        private static string CoverageRow(
            string featureId,
            string domainId = "test.state",
            string expectation = "Persistent",
            string status = "Planned")
        {
            return Csv(
                featureId, domainId, expectation, status, "Test.Owner", "Domain not implemented yet",
                "Future owner must implement the complete save-domain registration contract.",
                "Immediate", "NotApplicable", "09A", "Fixture");
        }

        private static string ContractRow(string expectation = "Persistent", string status = "Planned")
        {
            var reason = expectation == "NonPersistent"
                ? "Presentation-only fixture has no mutable gameplay authority and therefore is intentionally not persisted."
                : "Mandatory registration contract: stable state identity; versioned DTO schema; capture and restore order; unloaded-cell and missing-content behavior; migration impact; deterministic round-trip test; corruption and recovery behavior.";
            return Csv(
                "CONTRACT.test.state", "test.state", expectation, status, "Test.Owner",
                status == "Planned" ? "Domain not implemented yet" : "Test contract", reason,
                expectation == "NonPersistent" ? "NotApplicable" : "Immediate",
                "NotApplicable", "09A", "Fixture contract");
        }

        private static string Csv(params string[] values)
        {
            return string.Join(",", values.Select(value => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));
        }

        private const string MatrixHeader = "\"FeatureId\",\"OwnerMilestone\"";
        private const string CoverageHeader =
            "\"FeatureId\",\"SaveDomainId\",\"PersistentExpectation\",\"CoverageStatus\",\"SchemaOwner\",\"CurrentImplementation\",\"EvidenceOrReason\",\"UnloadedCellPolicy\",\"ReplacementPolicy\",\"OwningMilestone\",\"Notes\"";
    }
}
