using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.GaragePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Editor.GaragePrototype
{
    public static class GaragePrototypeValidator
    {
        private static readonly string[] RequiredProductionAssets =
        {
            GaragePrototypePaths.ProductionScene,
            GaragePrototypePaths.GarageRoofPrefab,
            GaragePrototypePaths.GarageShellPrefab,
            GaragePrototypePaths.GarageInteriorPrefab,
            GaragePrototypePaths.RoadPrefab,
            GaragePrototypePaths.TerrainPrefab,
            GaragePrototypePaths.TreePrefab,
            GaragePrototypePaths.VegetationPrefab,
            GaragePrototypePaths.NeutralLightingPrefab,
            GaragePrototypePaths.LateDayLightingPrefab,
            GaragePrototypePaths.ScalePivotRecord
        };

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ValidateRequiredAssets(errors);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GaragePrototypePaths.ProductionScene) != null)
            {
                ValidateProductionScene(errors);
            }

            ValidateScalePivotRecord(errors);
            ValidateMaterialLibrary(errors);
            ValidateLightingProfiles(errors);
            ValidateBuildSettings(errors);
            ValidateStaticMetrics(errors);
            return errors;
        }

        [MenuItem("Tools/My Summer Car/Milestone 3/Validate Garage Art Prototype")]
        public static void ValidateMenu()
        {
            ThrowIfInvalid();
            Debug.Log("M3_GARAGE_VALIDATION_OK");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 3 batch validation requires batch mode.");
            }

            ThrowIfInvalid();
            Debug.Log("M3_GARAGE_VALIDATION_OK");
        }

        private static void ThrowIfInvalid()
        {
            IReadOnlyList<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Milestone 3 validation failed:\n- " + string.Join("\n- ", errors));
            }
        }

        private static void ValidateRequiredAssets(List<string> errors)
        {
            foreach (string path in RequiredProductionAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    errors.Add("Missing required production asset: " + path);
                }
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GaragePrototypePaths.ComparisonScene) == null)
            {
                errors.Add("Missing reference-only comparison scene: " + GaragePrototypePaths.ComparisonScene);
            }
        }

        private static void ValidateProductionScene(List<string> errors)
        {
            Scene scene = EditorSceneManager.OpenScene(
                GaragePrototypePaths.ProductionScene,
                OpenSceneMode.Single);
            GaragePrototypeSceneMarker marker = FindInScene<GaragePrototypeSceneMarker>(scene);
            if (marker == null)
            {
                errors.Add("Production scene has no GaragePrototypeSceneMarker.");
            }
            else
            {
                if (!marker.ProductionContentOnly)
                {
                    errors.Add("Production scene is not marked production-content-only.");
                }

                if (marker.ReconstructedRoadLengthMeters < 100f ||
                    marker.ReconstructedRoadLengthMeters > 500f)
                {
                    errors.Add("Road length must remain inside the Milestone 3 range of 100-500 m.");
                }
            }

            if (FindInScene<Camera>(scene) == null)
            {
                errors.Add("Production scene has no presentation camera.");
            }

            Volume volume = FindInScene<Volume>(scene);
            if (volume == null || !volume.isGlobal || volume.sharedProfile == null)
            {
                errors.Add("Production scene has no configured global HDRP volume.");
            }

            Light sun = FindInScene<Light>(scene);
            if (sun == null || sun.type != LightType.Directional || sun.intensity <= 0f)
            {
                errors.Add("Production scene has no physically configured directional sun.");
            }

            foreach (string dependency in AssetDatabase.GetDependencies(
                         GaragePrototypePaths.ProductionScene,
                         recursive: true))
            {
                if (IsDonorDependency(dependency))
                {
                    errors.Add("Production scene depends on donor/reference content: " + dependency);
                }
            }

            GameObject garage = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.GarageShellPrefab);
            ValidateNamedChild(garage, "VehicleDoor_Left", errors);
            ValidateNamedChild(garage, "VehicleDoor_Right", errors);
            ValidateNamedChild(garage, "SideDoor", errors);
            GameObject interior = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.GarageInteriorPrefab);
            ValidateNamedChild(interior, "Workbench", errors);
            ValidateNamedChild(interior, "Shelves", errors);
            ValidateNamedChild(interior, "ToolCabinet", errors);
        }

        private static void ValidateScalePivotRecord(List<string> errors)
        {
            GarageScalePivotRecord record =
                AssetDatabase.LoadAssetAtPath<GarageScalePivotRecord>(GaragePrototypePaths.ScalePivotRecord);
            if (record == null)
            {
                return;
            }

            if (record.ProductionRoofPrefab == null)
            {
                errors.Add("Scale/pivot record has no production roof prefab.");
            }

            if (!record.IsWithinTolerance)
            {
                errors.Add(
                    $"Roof scale/pivot comparison exceeds {record.DimensionalToleranceMeters:F4} m: " +
                    $"pivot={record.PivotDeltaMeters:F6}, bounds={record.MaximumBoundsDeltaMeters:F6}.");
            }

            if (string.IsNullOrWhiteSpace(record.SourceContainerSha256) ||
                string.IsNullOrWhiteSpace(record.NormalizedReferenceSha256))
            {
                errors.Add("Scale/pivot record is missing donor source hashes.");
            }
        }

        private static void ValidateMaterialLibrary(List<string> errors)
        {
            string[] materialGuids = AssetDatabase.FindAssets(
                "t:Material",
                new[] { GaragePrototypePaths.MaterialRoot });
            if (materialGuids.Length < 10)
            {
                errors.Add("Material library must contain at least ten reusable HDRP materials.");
            }

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null ||
                    !material.shader.name.StartsWith("HDRP/", StringComparison.Ordinal))
                {
                    errors.Add("Non-HDRP material in Milestone 3 library: " + path);
                    continue;
                }

                if (material.GetTexture("_BaseColorMap") == null ||
                    material.GetTexture("_NormalMap") == null ||
                    material.GetTexture("_MaskMap") == null)
                {
                    errors.Add("Incomplete PBR texture set on material: " + path);
                }
            }
        }

        private static void ValidateLightingProfiles(List<string> errors)
        {
            ValidateLightingProfile(GaragePrototypePaths.NeutralVolumeProfile, errors);
            ValidateLightingProfile(GaragePrototypePaths.LateDayVolumeProfile, errors);
        }

        private static void ValidateLightingProfile(string path, List<string> errors)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                errors.Add("Missing HDRP volume profile: " + path);
                return;
            }

            if (!profile.TryGet(out VisualEnvironment environment) ||
                environment.skyType.value != (int)SkyType.PhysicallyBased ||
                !profile.TryGet(out PhysicallyBasedSky _) ||
                !profile.TryGet(out Exposure exposure) || exposure.mode.value != ExposureMode.Fixed ||
                !profile.TryGet(out Fog fog) || !fog.enabled.value || !fog.enableVolumetricFog.value ||
                !profile.TryGet(out Tonemapping tonemapping) || tonemapping.mode.value != TonemappingMode.ACES)
            {
                errors.Add("Incomplete restrained HDRP lighting profile: " + path);
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int productionIndex = Array.FindIndex(
                scenes,
                entry => string.Equals(entry.path, GaragePrototypePaths.ProductionScene, StringComparison.Ordinal));
            if (productionIndex < 0 || !scenes[productionIndex].enabled)
            {
                errors.Add("Production garage scene is not enabled in Build Settings.");
            }

            if (scenes.Any(entry => string.Equals(
                    entry.path,
                    GaragePrototypePaths.ComparisonScene,
                    StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Reference-only comparison scene must not be in Build Settings.");
            }
        }

        private static void ValidateStaticMetrics(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GaragePrototypePaths.ProductionScene) == null)
            {
                return;
            }

            GaragePrototypeMetricsCapture capture =
                GaragePrototypeMetrics.CollectProductionScene(writeCapture: true);
            if (capture.triangles > GaragePrototypeMetrics.TriangleBudget)
            {
                errors.Add($"Triangle budget exceeded: {capture.triangles}/{GaragePrototypeMetrics.TriangleBudget}.");
            }

            if (capture.renderers > GaragePrototypeMetrics.RendererBudget)
            {
                errors.Add($"Renderer budget exceeded: {capture.renderers}/{GaragePrototypeMetrics.RendererBudget}.");
            }

            if (capture.lights > GaragePrototypeMetrics.LightBudget)
            {
                errors.Add($"Light budget exceeded: {capture.lights}/{GaragePrototypeMetrics.LightBudget}.");
            }

            if (capture.estimatedTextureBytes > GaragePrototypeMetrics.EstimatedTextureBudgetBytes)
            {
                errors.Add("Estimated resident texture budget exceeded.");
            }

            if (capture.colliders < 12)
            {
                errors.Add("Prototype has too few collision proxies for garage, road and vegetation.");
            }

            if (capture.lodGroups < 3)
            {
                errors.Add("Prototype must contain LOD groups for roof, road and vegetation.");
            }

            if (!File.Exists(Path.GetFullPath(GaragePrototypePaths.StaticCapturePath)))
            {
                errors.Add("Static performance capture was not written.");
            }
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void ValidateNamedChild(
            GameObject prefab,
            string name,
            List<string> errors)
        {
            if (prefab == null ||
                !prefab.GetComponentsInChildren<Transform>(true).Any(
                    transform => string.Equals(transform.name, name, StringComparison.Ordinal)))
            {
                errors.Add($"Required rebuilt content '{name}' is missing.");
            }
        }

        private static bool IsDonorDependency(string path)
        {
            return path.StartsWith(
                       "Assets/Game/LegacyImport/ReferenceOnly/",
                       StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(
                       "Assets/Game/Imported/DonorGenerated/",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
