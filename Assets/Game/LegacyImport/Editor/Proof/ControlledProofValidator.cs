using System;
using System.Collections.Generic;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.LegacyImport.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Proof
{
    public static class ControlledProofValidator
    {
        public const string ComparisonScenePath =
            "Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity";

        public static IReadOnlyList<DonorPipelineValidationIssue> Validate()
        {
            var issues = new List<DonorPipelineValidationIssue>();
            string[] provenanceGuids = AssetDatabase.FindAssets(
                "t:ReauthoredAssetProvenance",
                new[] { "Assets/Game/LegacyImport/Manifests" });

            if (provenanceGuids.Length == 0)
            {
                issues.Add(new DonorPipelineValidationIssue(
                    DonorPipelineValidationSeverity.Warning,
                    "ControlledProofNotConfigured",
                    "No reauthored asset proof is configured; donor mesh extraction remains required."));
                return issues;
            }

            bool hasEnvironment = false;
            bool hasVehiclePart = false;
            foreach (string provenanceGuid in provenanceGuids)
            {
                string provenancePath = AssetDatabase.GUIDToAssetPath(provenanceGuid);
                ReauthoredAssetProvenance provenance =
                    AssetDatabase.LoadAssetAtPath<ReauthoredAssetProvenance>(provenancePath);
                if (provenance == null)
                {
                    issues.Add(Error("InvalidProofAsset", "Proof asset cannot be loaded.", provenancePath));
                    continue;
                }

                hasEnvironment |= provenance.Role == DonorProofRole.Environment;
                hasVehiclePart |= provenance.Role == DonorProofRole.VehiclePart;
                ValidateProvenance(provenance, provenancePath, issues);
            }

            if (!hasEnvironment)
            {
                issues.Add(Error(
                    "MissingEnvironmentProof",
                    "Controlled proof requires one donor environment mesh and independent replacement."));
            }

            if (!hasVehiclePart)
            {
                issues.Add(Error(
                    "MissingVehiclePartProof",
                    "Controlled proof requires one donor vehicle-part mesh and independent replacement."));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ComparisonScenePath) == null)
            {
                issues.Add(Error(
                    "MissingComparisonScene",
                    "Configured controlled proof requires its dedicated comparison scene.",
                    ComparisonScenePath));
            }

            return issues;
        }

        private static void ValidateProvenance(
            ReauthoredAssetProvenance provenance,
            string provenancePath,
            List<DonorPipelineValidationIssue> issues)
        {
            if (provenance.Registry == null ||
                !provenance.Registry.TryGetRecord(provenance.DonorRecordId, out DonorAssetRecord record))
            {
                issues.Add(Error(
                    "MissingProofRecord",
                    "Proof does not resolve to a donor registry record.",
                    provenancePath));
                return;
            }

            if (record.Kind != DonorAssetKind.Mesh ||
                record.Status != DonorAssetStatus.ReplacementReady)
            {
                issues.Add(Error(
                    "ProofRecordState",
                    "Proof donor record must be a Mesh with ReplacementReady status.",
                    provenancePath));
            }

            GameObject prefab = provenance.ProductionPrefab;
            string prefabPath = prefab == null ? string.Empty : AssetDatabase.GetAssetPath(prefab);
            if (prefab == null || string.IsNullOrEmpty(prefabPath))
            {
                issues.Add(Error(
                    "MissingProofPrefab",
                    "Proof has no production prefab.",
                    provenancePath));
                return;
            }

            if (!string.Equals(prefabPath, record.ProductionReplacementPath, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "ProofReplacementMismatch",
                    "Proof prefab path differs from the donor record replacement path.",
                    provenancePath));
            }

            if (!DonorImportPathPolicy.TryNormalizeProductionAssetPath(prefabPath, out _, out string pathError))
            {
                issues.Add(Error("InvalidProofPrefabPath", pathError, prefabPath));
            }

            if (prefab.GetComponentInChildren<LegacyAssetReference>(true) != null)
            {
                issues.Add(Error(
                    "ProofPrefabLegacyMarker",
                    "Production proof prefab contains LegacyAssetReference.",
                    prefabPath));
            }

            if (prefab.GetComponentInChildren<Collider>(true) == null)
            {
                issues.Add(Error("MissingCollisionProxy", "Production proof has no Collider.", prefabPath));
            }

            LODGroup lodGroup = prefab.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null || lodGroup.lodCount < 2)
            {
                issues.Add(Error(
                    "MissingLodGroup",
                    "Production proof requires an LODGroup with at least two levels.",
                    prefabPath));
            }

            float pivotDelta = Vector3.Distance(
                provenance.ReferencePivotMeters,
                provenance.ProductionPivotMeters);
            if (pivotDelta > provenance.DimensionalToleranceMeters)
            {
                issues.Add(Error(
                    "PivotTolerance",
                    $"Pivot delta {pivotDelta:F6} m exceeds tolerance " +
                    $"{provenance.DimensionalToleranceMeters:F6} m.",
                    provenancePath));
            }

            if (provenance.Role == DonorProofRole.VehiclePart && provenance.MountPoints.Count == 0)
            {
                issues.Add(Error(
                    "MissingMountPoint",
                    "Vehicle-part proof requires at least one measured mount point.",
                    provenancePath));
            }

            foreach (MountPointComparison mountPoint in provenance.MountPoints)
            {
                if (mountPoint == null || string.IsNullOrWhiteSpace(mountPoint.Name))
                {
                    issues.Add(Error(
                        "InvalidMountPoint",
                        "Mount-point proof has no name.",
                        provenancePath));
                    continue;
                }

                if (mountPoint.DeltaMeters > provenance.DimensionalToleranceMeters)
                {
                    issues.Add(Error(
                        "MountPointTolerance",
                        $"Mount point '{mountPoint.Name}' exceeds dimensional tolerance.",
                        provenancePath));
                }
            }

            ValidateMaterialsAndTextures(provenance, prefab, prefabPath, issues);
        }

        private static void ValidateMaterialsAndTextures(
            ReauthoredAssetProvenance provenance,
            GameObject prefab,
            string prefabPath,
            List<DonorPipelineValidationIssue> issues)
        {
            bool hasHdrpMaterial = false;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (IsDonorPath(materialPath))
                    {
                        issues.Add(Error(
                            "DonorMaterialDependency",
                            "Production proof uses a donor/reference material.",
                            prefabPath));
                    }

                    if (material.shader != null &&
                        material.shader.name.StartsWith("HDRP/", StringComparison.Ordinal))
                    {
                        hasHdrpMaterial = true;
                    }
                }
            }

            if (!hasHdrpMaterial)
            {
                issues.Add(Error(
                    "MissingHdrpMaterial",
                    "Production proof uses no HDRP material.",
                    prefabPath));
            }

            if (provenance.AuthoredTextures.Count < 3)
            {
                issues.Add(Error(
                    "IncompleteTextureSet",
                    "Proof requires at least three newly authored texture assets.",
                    prefabPath));
            }

            foreach (Texture2D texture in provenance.AuthoredTextures)
            {
                string texturePath = texture == null ? string.Empty : AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(texturePath) || IsDonorPath(texturePath))
                {
                    issues.Add(Error(
                        "InvalidAuthoredTexture",
                        "Authored texture is missing or located in a donor/reference root.",
                        prefabPath));
                }
            }
        }

        private static bool IsDonorPath(string assetPath)
        {
            return DonorImportPathPolicy.IsUnderAssetRoot(
                       assetPath,
                       DonorImportPathPolicy.ReferenceOnlyRoot) ||
                   DonorImportPathPolicy.IsUnderAssetRoot(
                       assetPath,
                       DonorImportPathPolicy.DonorGeneratedRoot);
        }

        private static DonorPipelineValidationIssue Error(
            string code,
            string message,
            string assetPath = "")
        {
            return new DonorPipelineValidationIssue(
                DonorPipelineValidationSeverity.Error,
                code,
                message,
                assetPath);
        }
    }
}
