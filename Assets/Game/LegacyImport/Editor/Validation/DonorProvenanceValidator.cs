using System;
using System.Collections.Generic;
using MSC.LegacyImport.Editor.Pipeline;

namespace MSC.LegacyImport.Editor.Validation
{
    public static class DonorProvenanceValidator
    {
        public static IReadOnlyList<DonorPipelineValidationIssue> FindMissingProvenance(
            IEnumerable<string> referenceAssetPaths,
            IEnumerable<DonorAssetRecord> records)
        {
            if (referenceAssetPaths == null)
            {
                throw new ArgumentNullException(nameof(referenceAssetPaths));
            }

            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            var registeredPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (DonorAssetRecord record in records)
            {
                if (record == null)
                {
                    continue;
                }

                if (DonorImportPathPolicy.TryNormalizeReferenceAssetPath(
                        record.ReferenceAssetPath,
                        out string normalized,
                        out _))
                {
                    registeredPaths.Add(normalized);
                }
            }

            var issues = new List<DonorPipelineValidationIssue>();
            foreach (string assetPath in referenceAssetPaths)
            {
                string normalizedCandidate = assetPath.Replace('\\', '/');
                string comparisonRoot = DonorImportPathPolicy.ReferenceOnlyRoot + "/Comparison";
                string generatedWorldRoot = DonorImportPathPolicy.ReferenceOnlyRoot + "/World/Generated";
                if (DonorImportPathPolicy.IsUnderAssetRoot(normalizedCandidate, comparisonRoot) ||
                    DonorImportPathPolicy.IsUnderAssetRoot(normalizedCandidate, generatedWorldRoot))
                {
                    continue;
                }

                if (!DonorImportPathPolicy.TryNormalizeReferenceAssetPath(
                        normalizedCandidate,
                        out string normalized,
                        out _))
                {
                    continue;
                }

                if (!registeredPaths.Contains(normalized))
                {
                    issues.Add(new DonorPipelineValidationIssue(
                        DonorPipelineValidationSeverity.Error,
                        "MissingProvenance",
                        "Reference-only asset has no matching donor registry record.",
                        normalized));
                }
            }

            return issues;
        }

        public static IReadOnlyList<DonorPipelineValidationIssue> FindProductionReferenceLeaks(
            IEnumerable<DonorAssetDependencyRecord> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            var issues = new List<DonorPipelineValidationIssue>();
            foreach (DonorAssetDependencyRecord asset in assets)
            {
                foreach (string dependency in asset.Dependencies)
                {
                    string normalizedDependency = dependency.Replace('\\', '/');
                    if (DonorImportPathPolicy.IsUnderAssetRoot(
                            normalizedDependency,
                            DonorImportPathPolicy.ReferenceOnlyRoot) ||
                        DonorImportPathPolicy.IsUnderAssetRoot(
                            normalizedDependency,
                            DonorImportPathPolicy.DonorGeneratedRoot))
                    {
                        issues.Add(new DonorPipelineValidationIssue(
                            DonorPipelineValidationSeverity.Error,
                            "ProductionDonorDependency",
                            $"Production asset depends on donor reference '{normalizedDependency}'.",
                            asset.AssetPath));
                    }
                }
            }

            return issues;
        }
    }
}
