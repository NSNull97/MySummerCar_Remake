using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Bootstrap;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldStreaming
{
    public sealed class WorldPilotGateRemediationValidationResult
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool Passed => errors.Count == 0;
        public string Evidence { get; internal set; } = string.Empty;

        internal void AddError(string message) => errors.Add(message);
        internal void AddWarning(string message) => warnings.Add(message);
    }

    /// <summary>
    /// Strict regression-fixture validation for the accepted bounded 05B.1
    /// two-cell streaming composition.
    /// </summary>
    public static class WorldPilotGateRemediationValidator
    {
        public const string ValidatorId = "pilot-gate-remediation";

        private static readonly string[] ForbiddenDependencySegments =
        {
            "/LegacyImport/ReferenceOnly/",
            "/Imported/DonorGenerated/"
        };

        [MenuItem("Tools/MSC Remake/World Streaming/Validate 05B.1 Pilot Gate Remediation")]
        public static void ValidateFromMenu()
        {
            Validate();
        }

        public static WorldPilotGateRemediationValidationResult Validate(bool logResult = true)
        {
            var result = new WorldPilotGateRemediationValidationResult();
            ValidateBuildSettings(result);
            ValidateManifest(result);
            ValidateBootstrapWiring(result);
            ValidateProductionDependencies(result);
            result.Evidence =
                $"{ValidatorId}: exactCells=2; cells={ProductionWorldStreamingBuilder.PilotCellId}|" +
                $"{ProductionWorldStreamingBuilder.NextCellId}; explicitM4Focus=true; loadAddress=buildIndex; " +
                $"errors={result.Errors.Count}; warnings={result.Warnings.Count}";

            if (logResult)
            {
                if (result.Passed)
                {
                    Debug.Log("WORLD_STREAMING_05B1_VALIDATION_OK " + result.Evidence);
                }
                else
                {
                    Debug.LogError(
                        "WORLD_STREAMING_05B1_VALIDATION_FAILED " + result.Evidence + Environment.NewLine +
                        string.Join(Environment.NewLine, result.Errors));
                }
            }

            return result;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("05B.1 streaming validation batch entry requires batch mode.");
            }

            WorldPilotGateRemediationValidationResult result = Validate();
            if (!result.Passed)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));
            }
        }

        private static void ValidateBuildSettings(WorldPilotGateRemediationValidationResult result)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            string[] enabledPaths = scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (enabledPaths.Length == 0 ||
                !string.Equals(
                    enabledPaths[0],
                    ProductionWorldStreamingBuilder.ActiveBootstrapScenePath,
                    StringComparison.Ordinal))
            {
                result.AddError("Bootstrap scene must be the first enabled Build Settings scene.");
            }

            RequireEnabledBuildScene(
                result,
                ProductionWorldStreamingBuilder.BootstrapScenePath);
            RequireEnabledBuildScene(result, ProductionWorldStreamingBuilder.PilotCellScenePath);
            RequireEnabledBuildScene(result, ProductionWorldStreamingBuilder.NextCellScenePath);
        }

        private static void ValidateManifest(WorldPilotGateRemediationValidationResult result)
        {
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    ProductionWorldStreamingBuilder.ManifestAssetPath);
            if (manifest == null)
            {
                result.AddError(
                    "Production streaming manifest is missing: " +
                    ProductionWorldStreamingBuilder.ManifestAssetPath);
                return;
            }

            foreach (string error in manifest.ValidateConfiguration())
            {
                result.AddError("Manifest: " + error);
            }

            if (!Mathf.Approximately(manifest.CellSizeMeters, ProductionWorldStreamingBuilder.CellSizeMeters) ||
                manifest.LoadingRadiusCells != ProductionWorldStreamingBuilder.LoadingRadiusCells ||
                manifest.UnloadingRadiusCells != ProductionWorldStreamingBuilder.UnloadingRadiusCells)
            {
                result.AddError("Manifest cell size or bounded load/unload radii differ from the accepted 05B.1 policy.");
            }

            if (manifest.Cells.Count != 2)
            {
                result.AddError($"Manifest must contain exactly two accepted production cells; found {manifest.Cells.Count}.");
                return;
            }

            ValidateCellEntry(
                result,
                manifest.Cells[0],
                ProductionWorldStreamingBuilder.PilotCellId,
                0,
                -3,
                ProductionWorldStreamingBuilder.PilotCellScenePath);
            ValidateCellEntry(
                result,
                manifest.Cells[1],
                ProductionWorldStreamingBuilder.NextCellId,
                0,
                -2,
                ProductionWorldStreamingBuilder.NextCellScenePath);
        }

        private static void ValidateCellEntry(
            WorldPilotGateRemediationValidationResult result,
            ProductionWorldCellScene cell,
            string expectedId,
            int expectedX,
            int expectedZ,
            string expectedPath)
        {
            if (!string.Equals(cell.CellId, expectedId, StringComparison.Ordinal) ||
                cell.Index.X != expectedX ||
                cell.Index.Z != expectedZ ||
                !string.Equals(cell.ScenePath, expectedPath, StringComparison.Ordinal))
            {
                result.AddError($"Manifest cell {expectedId} does not match its accepted ID, coordinates, and scene path.");
            }

            int currentBuildIndex = ProductionWorldStreamingBuilder.GetEnabledBuildIndex(expectedPath);
            if (currentBuildIndex < 0 || cell.BuildIndex != currentBuildIndex)
            {
                result.AddError(
                    $"Manifest cell {expectedId} build index {cell.BuildIndex} does not match enabled Build Settings index {currentBuildIndex}.");
            }
        }

        private static void ValidateBootstrapWiring(WorldPilotGateRemediationValidationResult result)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProductionWorldStreamingBuilder.BootstrapScenePath) == null)
            {
                result.AddError(
                    "05B.1 prototype fixture scene asset is missing.");
                return;
            }

            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(ProductionWorldStreamingBuilder.BootstrapScenePath);
                GameCompositionRoot[] roots = GetSceneComponents<GameCompositionRoot>(scene);
                ProductionWorldStreamingService[] services =
                    GetSceneComponents<ProductionWorldStreamingService>(scene);
                ProductionWorldStreamingInstaller[] installers =
                    GetSceneComponents<ProductionWorldStreamingInstaller>(scene);
                WorldReferenceCellLoader[] referenceLoaders = GetSceneComponents<WorldReferenceCellLoader>(scene);

                if (roots.Length != 1 || services.Length != 1 || installers.Length != 1)
                {
                    result.AddError(
                        $"Prototype fixture requires exactly one composition root, production streaming service, and installer; " +
                        $"found {roots.Length}/{services.Length}/{installers.Length}.");
                    return;
                }

                GameCompositionRoot root = roots[0];
                ProductionWorldStreamingService service = services[0];
                ProductionWorldStreamingInstaller installer = installers[0];
                if (referenceLoaders.Length != 0)
                {
                    result.AddError(
                        "Prototype fixture must not contain the reference-only WorldReferenceCellLoader.");
                }

                if (root.gameObject != service.gameObject || root.gameObject != installer.gameObject)
                {
                    result.AddError(
                        "Prototype fixture composition root, production streaming service, and installer must share one object.");
                }

                if (!root.gameObject.activeInHierarchy || !service.enabled || !installer.enabled ||
                    HasEditorOnlyAncestor(root.transform))
                {
                    result.AddError(
                        "Prototype fixture streaming composition must be active, enabled, and build-visible.");
                }

                ProductionWorldStreamingManifest expectedManifest =
                    AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                        ProductionWorldStreamingBuilder.ManifestAssetPath);
                GameObject expectedPlayer =
                    AssetDatabase.LoadAssetAtPath<GameObject>(ProductionWorldStreamingBuilder.PlayerPrefabPath);
                if (service.Manifest != expectedManifest || installer.WorldStreaming != service ||
                    installer.CompositionRoot != root)
                {
                    result.AddError(
                        "Prototype fixture serialized service/root/manifest references are not wired explicitly.");
                }

                if (installer.PlayerPrefab != expectedPlayer)
                {
                    result.AddError("Bootstrap installer does not reference the accepted M4 player prefab.");
                }

                if (Vector3.Distance(
                        installer.PlayerSpawnPosition,
                        ProductionWorldStreamingBuilder.PlayerSpawnPosition) > 0.001f ||
                    Quaternion.Angle(
                        installer.PlayerSpawnRotation,
                        ProductionWorldStreamingBuilder.PlayerSpawnRotation) > 0.01f)
                {
                    result.AddError("Bootstrap M4 spawn transform differs from the accepted home-zone traversal start.");
                }

                if (service.Focus != null)
                {
                    result.AddError("Bootstrap service focus must be runtime-bound to the explicitly spawned M4 player.");
                }
            }
            catch (Exception exception)
            {
                result.AddError(
                    "Could not inspect the prototype streaming fixture: " +
                    exception.Message);
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void ValidateProductionDependencies(WorldPilotGateRemediationValidationResult result)
        {
            string[] assets =
            {
                ProductionWorldStreamingBuilder.BootstrapScenePath,
                ProductionWorldStreamingBuilder.ManifestAssetPath,
                ProductionWorldStreamingBuilder.PlayerPrefabPath,
                ProductionWorldStreamingBuilder.PilotCellScenePath,
                ProductionWorldStreamingBuilder.NextCellScenePath
            };

            foreach (string asset in assets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(asset) == null)
                {
                    result.AddError("Required production streaming dependency seed is missing: " + asset);
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(asset, recursive: true))
                {
                    string normalized = dependency.Replace('\\', '/');
                    for (int segmentIndex = 0; segmentIndex < ForbiddenDependencySegments.Length; segmentIndex++)
                    {
                        if (normalized.IndexOf(
                                ForbiddenDependencySegments[segmentIndex],
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.AddError(asset + " has forbidden donor/reference dependency: " + normalized);
                        }
                    }
                }
            }
        }

        private static void RequireEnabledBuildScene(
            WorldPilotGateRemediationValidationResult result,
            string scenePath)
        {
            if (ProductionWorldStreamingBuilder.GetEnabledBuildIndex(scenePath) < 0)
            {
                result.AddError("Required production streaming scene is not enabled in Build Settings: " + scenePath);
            }
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
                .ToArray();
        }

        private static bool HasEditorOnlyAncestor(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (string.Equals(current.gameObject.tag, "EditorOnly", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
