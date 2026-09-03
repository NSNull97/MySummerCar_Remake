using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Validation
{
    public static class FullGameSaveCoverageValidator
    {
        private const string MatrixRelativePath = "Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv";
        private const string CoverageRelativePath = "Docs/Save/FULL_GAME_SAVE_COVERAGE.csv";

        private static readonly string[] RequiredCoverageColumns =
        {
            "FeatureId",
            "SaveDomainId",
            "PersistentExpectation",
            "CoverageStatus",
            "SchemaOwner",
            "CurrentImplementation",
            "EvidenceOrReason",
            "UnloadedCellPolicy",
            "ReplacementPolicy",
            "OwningMilestone",
            "Notes"
        };

        private static readonly HashSet<string> AllowedExpectations = new HashSet<string>(StringComparer.Ordinal)
        {
            "Persistent",
            "NonPersistent",
            "PendingDecision"
        };

        private static readonly HashSet<string> AllowedCoverageStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Covered",
            "Partial",
            "Planned",
            "Uncovered",
            "NotRequired",
            "PendingDecision"
        };

        private static readonly HashSet<string> AllowedUnloadedCellPolicies = new HashSet<string>(StringComparer.Ordinal)
        {
            "Immediate",
            "DeferredByStableId",
            "NotApplicable",
            "PendingDesign"
        };

        private static readonly HashSet<string> AllowedReplacementPolicies = new HashSet<string>(StringComparer.Ordinal)
        {
            "StableIdPreserved",
            "NotApplicable",
            "PendingDesign"
        };

        private static readonly Regex SaveDomainIdPattern = new Regex(
            "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$",
            RegexOptions.CultureInvariant);

        [MenuItem("Tools/MSC/Validation/Validate Full-Game Save Coverage")]
        public static void ValidateProjectCoverageFromMenu()
        {
            var issues = ValidateProjectCoverage();
            if (issues.Count == 0)
            {
                Debug.Log("Full-game save coverage validation passed.");
                return;
            }

            Debug.LogError("Full-game save coverage validation failed:\n" + string.Join("\n", issues));
        }

        public static IReadOnlyList<string> ValidateProjectCoverage()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Validate(
                Path.Combine(projectRoot, MatrixRelativePath),
                Path.Combine(projectRoot, CoverageRelativePath));
        }

        public static IReadOnlyList<string> Validate(string matrixPath, string coveragePath)
        {
            var issues = new List<string>();
            var matrix = ReadCsv(matrixPath, "parity matrix", issues);
            var coverage = ReadCsv(coveragePath, "save coverage", issues);
            if (matrix == null || coverage == null)
            {
                return issues;
            }

            RequireColumns(matrix, new[] { "FeatureId", "OwnerMilestone" }, "parity matrix", issues);
            RequireColumns(coverage, RequiredCoverageColumns, "save coverage", issues);
            if (issues.Count != 0)
            {
                return issues;
            }

            var matrixRows = matrix.Rows.Where(row => !string.IsNullOrWhiteSpace(row["FeatureId"])).ToArray();
            var featureRows = coverage.Rows.Where(row => !IsContractRow(row)).ToArray();
            var contractRows = coverage.Rows.Where(IsContractRow).ToArray();

            ValidateUniqueIds(matrixRows, "parity matrix", issues);
            ValidateUniqueIds(featureRows, "save coverage feature rows", issues);
            ValidateFeatureCoverage(matrixRows, featureRows, issues);
            ValidateRows(featureRows, false, issues);
            ValidateRows(contractRows, true, issues);
            ValidateContractCoverage(featureRows, contractRows, issues);
            ValidateMilestoneOwnership(matrixRows, featureRows, issues);

            return issues;
        }

        private static void ValidateRows(IEnumerable<CsvRow> rows, bool contractRows, ICollection<string> issues)
        {
            foreach (var row in rows)
            {
                var featureId = row["FeatureId"];
                var domainId = row["SaveDomainId"];
                var expectation = row["PersistentExpectation"];
                var coverageStatus = row["CoverageStatus"];
                var reason = row["EvidenceOrReason"];
                var unloadedCellPolicy = row["UnloadedCellPolicy"];
                var replacementPolicy = row["ReplacementPolicy"];

                if (!SaveDomainIdPattern.IsMatch(domainId))
                {
                    issues.Add($"{featureId}: invalid SaveDomainId '{domainId}'.");
                }

                ValidateAllowed(featureId, "PersistentExpectation", expectation, AllowedExpectations, issues);
                ValidateAllowed(featureId, "CoverageStatus", coverageStatus, AllowedCoverageStatuses, issues);
                ValidateAllowed(featureId, "UnloadedCellPolicy", unloadedCellPolicy, AllowedUnloadedCellPolicies, issues);
                ValidateAllowed(featureId, "ReplacementPolicy", replacementPolicy, AllowedReplacementPolicies, issues);

                if (string.IsNullOrWhiteSpace(row["SchemaOwner"]))
                {
                    issues.Add($"{featureId}: SchemaOwner is required.");
                }

                if (coverageStatus.Equals("Verified", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add($"{featureId}: CoverageStatus must describe save coverage, not donor verification.");
                }

                if (expectation == "NonPersistent")
                {
                    if (coverageStatus != "NotRequired")
                    {
                        issues.Add($"{featureId}: NonPersistent rows must use CoverageStatus NotRequired.");
                    }

                    if (!IsExplicitReason(reason))
                    {
                        issues.Add($"{featureId}: NonPersistent state requires an explicit evidence-backed reason.");
                    }
                }

                if (expectation == "PendingDecision" && coverageStatus != "PendingDecision")
                {
                    issues.Add($"{featureId}: PendingDecision expectation must have PendingDecision coverage status.");
                }

                if (coverageStatus == "Covered" &&
                    row["CurrentImplementation"].IndexOf("not implemented", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    issues.Add($"{featureId}: an unimplemented domain cannot be marked Covered.");
                }

                if (!contractRows)
                {
                    continue;
                }

                if (!featureId.Equals("CONTRACT." + domainId, StringComparison.Ordinal))
                {
                    issues.Add($"{featureId}: contract FeatureId must be CONTRACT.<SaveDomainId>.");
                }

                if (expectation == "Persistent" && !ContainsContractChecklist(reason))
                {
                    issues.Add($"{featureId}: persistent contract row is missing one or more mandatory registration checklist items.");
                }
            }
        }

        private static void ValidateFeatureCoverage(
            IReadOnlyCollection<CsvRow> matrixRows,
            IReadOnlyCollection<CsvRow> coverageRows,
            ICollection<string> issues)
        {
            var matrixIds = new HashSet<string>(matrixRows.Select(row => row["FeatureId"]), StringComparer.Ordinal);
            var coverageIds = new HashSet<string>(coverageRows.Select(row => row["FeatureId"]), StringComparer.Ordinal);

            foreach (var missing in matrixIds.Except(coverageIds).OrderBy(id => id, StringComparer.Ordinal))
            {
                issues.Add($"1:1 coverage missing parity FeatureId '{missing}'.");
            }

            foreach (var extra in coverageIds.Except(matrixIds).OrderBy(id => id, StringComparer.Ordinal))
            {
                issues.Add($"1:1 coverage contains unknown parity FeatureId '{extra}'.");
            }
        }

        private static void ValidateContractCoverage(
            IEnumerable<CsvRow> featureRows,
            IReadOnlyCollection<CsvRow> contractRows,
            ICollection<string> issues)
        {
            var contractsByDomain = contractRows
                .GroupBy(row => row["SaveDomainId"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            foreach (var duplicate in contractsByDomain.Where(pair => pair.Value != 1))
            {
                issues.Add($"Save domain '{duplicate.Key}' has {duplicate.Value} contract rows; exactly one is required.");
            }

            foreach (var domainId in featureRows.Select(row => row["SaveDomainId"]).Distinct(StringComparer.Ordinal))
            {
                if (!contractsByDomain.ContainsKey(domainId))
                {
                    issues.Add($"Save domain '{domainId}' has feature rows but no explicit contract row.");
                }
            }
        }

        private static void ValidateMilestoneOwnership(
            IEnumerable<CsvRow> matrixRows,
            IEnumerable<CsvRow> coverageRows,
            ICollection<string> issues)
        {
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var matrixRow in matrixRows)
            {
                if (!owners.ContainsKey(matrixRow["FeatureId"]))
                {
                    owners.Add(matrixRow["FeatureId"], matrixRow["OwnerMilestone"]);
                }
            }
            foreach (var row in coverageRows)
            {
                if (owners.TryGetValue(row["FeatureId"], out var owner) &&
                    !owner.Equals(row["OwningMilestone"], StringComparison.Ordinal))
                {
                    issues.Add($"{row["FeatureId"]}: OwningMilestone '{row["OwningMilestone"]}' does not match parity owner '{owner}'.");
                }
            }
        }

        private static void ValidateUniqueIds(IEnumerable<CsvRow> rows, string source, ICollection<string> issues)
        {
            foreach (var duplicate in rows.GroupBy(row => row["FeatureId"], StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                issues.Add($"{source} contains duplicate FeatureId '{duplicate.Key}'.");
            }
        }

        private static void ValidateAllowed(
            string featureId,
            string column,
            string value,
            ISet<string> allowed,
            ICollection<string> issues)
        {
            if (!allowed.Contains(value))
            {
                issues.Add($"{featureId}: invalid {column} '{value}'. Allowed: {string.Join(", ", allowed.OrderBy(item => item, StringComparer.Ordinal))}.");
            }
        }

        private static bool IsExplicitReason(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length >= 24 &&
                   value.IndexOf("TBD", StringComparison.OrdinalIgnoreCase) < 0 &&
                   value.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool ContainsContractChecklist(string value)
        {
            var requiredPhrases = new[]
            {
                "stable state identity",
                "DTO schema",
                "capture and restore order",
                "unloaded-cell",
                "missing-content",
                "migration impact",
                "round-trip test",
                "corruption and recovery"
            };

            return requiredPhrases.All(phrase => value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsContractRow(CsvRow row)
        {
            return row["FeatureId"].StartsWith("CONTRACT.", StringComparison.Ordinal);
        }

        private static void RequireColumns(CsvTable table, IEnumerable<string> required, string label, ICollection<string> issues)
        {
            foreach (var column in required)
            {
                if (!table.Headers.Contains(column, StringComparer.Ordinal))
                {
                    issues.Add($"{label} is missing required column '{column}'.");
                }
            }
        }

        private static CsvTable ReadCsv(string path, string label, ICollection<string> issues)
        {
            if (!File.Exists(path))
            {
                issues.Add($"{label} file does not exist: {path}");
                return null;
            }

            try
            {
                return CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                issues.Add($"Could not parse {label} '{path}': {exception.Message}");
                return null;
            }
        }

        private sealed class CsvTable
        {
            public string[] Headers { get; }
            public CsvRow[] Rows { get; }

            private CsvTable(string[] headers, CsvRow[] rows)
            {
                Headers = headers;
                Rows = rows;
            }

            public static CsvTable Parse(string text)
            {
                var records = ParseRecords(text);
                if (records.Count == 0)
                {
                    throw new InvalidDataException("CSV has no header row.");
                }

                var headers = records[0].ToArray();
                if (headers.Length > 0)
                {
                    headers[0] = headers[0].TrimStart('\uFEFF');
                }

                if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Length)
                {
                    throw new InvalidDataException("CSV contains duplicate column names.");
                }

                var rows = new List<CsvRow>();
                for (var index = 1; index < records.Count; index++)
                {
                    var fields = records[index];
                    if (fields.Count == 1 && fields[0].Length == 0)
                    {
                        continue;
                    }

                    if (fields.Count != headers.Length)
                    {
                        throw new InvalidDataException($"Row {index + 1} has {fields.Count} fields; expected {headers.Length}.");
                    }

                    rows.Add(new CsvRow(headers, fields));
                }

                return new CsvTable(headers, rows.ToArray());
            }

            private static List<List<string>> ParseRecords(string text)
            {
                var records = new List<List<string>>();
                var record = new List<string>();
                var field = new StringBuilder();
                var quoted = false;

                for (var index = 0; index < text.Length; index++)
                {
                    var character = text[index];
                    if (quoted)
                    {
                        if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else if (character == '"')
                        {
                            quoted = false;
                        }
                        else
                        {
                            field.Append(character);
                        }

                        continue;
                    }

                    if (character == '"' && field.Length == 0)
                    {
                        quoted = true;
                    }
                    else if (character == ',')
                    {
                        record.Add(field.ToString());
                        field.Clear();
                    }
                    else if (character == '\r' || character == '\n')
                    {
                        if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        {
                            index++;
                        }

                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                    }
                    else
                    {
                        field.Append(character);
                    }
                }

                if (quoted)
                {
                    throw new InvalidDataException("CSV ends inside a quoted field.");
                }

                if (field.Length > 0 || record.Count > 0)
                {
                    record.Add(field.ToString());
                    records.Add(record);
                }

                return records;
            }
        }

        private sealed class CsvRow
        {
            private readonly Dictionary<string, string> fields;

            public CsvRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
            {
                fields = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var index = 0; index < headers.Count; index++)
                {
                    fields.Add(headers[index], values[index]);
                }
            }

            public string this[string column] => fields.TryGetValue(column, out var value) ? value : string.Empty;
        }
    }
}
