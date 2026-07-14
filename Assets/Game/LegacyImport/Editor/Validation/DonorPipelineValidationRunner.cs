using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Validation
{
    public static class DonorPipelineValidationRunner
    {
        [MenuItem("Tools/My Summer Car/Legacy Import/Validate Donor Pipeline")]
        public static void RunFromMenu()
        {
            Run(throwOnFailure: false);
        }

        public static void RunBatch()
        {
            Run(throwOnFailure: true);
        }

        private static void Run(bool throwOnFailure)
        {
            IReadOnlyList<DonorPipelineValidationIssue> issues =
                DonorPipelineProjectValidator.ValidateProject();
            foreach (DonorPipelineValidationIssue issue in issues)
            {
                string message = $"[{issue.Code}] {issue.Message} {issue.AssetPath}".TrimEnd();
                if (issue.Severity == DonorPipelineValidationSeverity.Error)
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.LogWarning(message);
                }
            }

            bool hasErrors = DonorPipelineProjectValidator.HasErrors(issues);
            if (!hasErrors)
            {
                Debug.Log($"Donor pipeline validation passed with {issues.Count} warning(s).");
                return;
            }

            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    $"Donor pipeline validation failed with {issues.Count} issue(s).");
            }
        }
    }
}
