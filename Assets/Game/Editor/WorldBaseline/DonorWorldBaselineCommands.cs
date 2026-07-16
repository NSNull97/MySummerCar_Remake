using System;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldBaselineCommands
    {
        private const string MenuRoot =
            "Tools/My Summer Car/Milestone 06B1/";

        [MenuItem(MenuRoot + "Build Canonical Sanitized Baseline")]
        public static void Build()
        {
            DonorWorldBaselineBuilder.Build();
        }

        [MenuItem(MenuRoot + "Validate Canonical Sanitized Baseline")]
        public static void Validate()
        {
            DonorWorldBaselineValidationResult result =
                DonorWorldBaselineValidator.Validate(
                    verifySourceHashes: true);
            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning("DONOR_WORLD_BASELINE_WARNING " + warning);
            }

            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "Donor world baseline validation failed:\n- " +
                    string.Join("\n- ", result.Errors));
            }

            Debug.Log(
                "DONOR_WORLD_BASELINE_VALIDATION_OK " +
                $"entities={result.EntityCount} renderers={result.RendererCount} " +
                $"metadataOnly={result.MetadataOnlyCount} " +
                $"colliders={result.ColliderCount}");
        }

        [MenuItem(MenuRoot + "Open Canonical Sanitized Baseline")]
        public static void Open()
        {
            DonorWorldBaselineBuilder.OpenCanonicalScene();
        }
    }
}
