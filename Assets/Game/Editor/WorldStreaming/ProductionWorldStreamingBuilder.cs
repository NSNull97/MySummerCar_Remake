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
    /// <summary>
    /// Preserves the accepted 05B.1 two-cell world as an explicit regression
    /// fixture. Milestone 06B2 owns the active Bootstrap profile.
    /// </summary>
    public static class ProductionWorldStreamingBuilder
    {
        public const string ActiveBootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string BootstrapScenePath =
            "Assets/Game/World/Debug/Streaming/" +
            "PrototypeWorldStreamingFixture.unity";
        public const string ManifestAssetPath =
            "Assets/Game/World/Content/Streaming/" +
            "PrototypeWorldStreamingManifest.asset";
        public const string PlayerPrefabPath = "Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab";
        public const string PilotCellId = "cell_0_-3";
        public const string PilotCellScenePath =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity";
        public const string NextCellId = "cell_0_-2";
        public const string NextCellScenePath =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity";

        public const float CellSizeMeters = 512f;
        public const int LoadingRadiusCells = 0;
        public const int UnloadingRadiusCells = 1;

        public static readonly Vector3 PlayerSpawnPosition = new Vector3(153.495f, 1.1f, -1028.03f);
        public static readonly Quaternion PlayerSpawnRotation = Quaternion.Euler(0f, 180f, 0f);

        [MenuItem(
            "Tools/MSC Remake/World Streaming/" +
            "Build 05B.1 Prototype Fixture")]
        public static void Build()
        {
            RequireAsset<SceneAsset>(ActiveBootstrapScenePath);
            RequireAsset<SceneAsset>(PilotCellScenePath);
            RequireAsset<SceneAsset>(NextCellScenePath);
            GameObject playerPrefab = RequireAsset<GameObject>(PlayerPrefabPath);

            EnsurePrototypeFixtureScene();
            EnsureBuildSettings();
            ProductionWorldStreamingManifest manifest = CreateOrUpdateManifest();
            WireBootstrapScene(manifest, playerPrefab);
            AssetDatabase.SaveAssets();

            WorldPilotGateRemediationValidationResult result =
                WorldPilotGateRemediationValidator.Validate(logResult: false);
            if (!result.Passed)
            {
                throw new InvalidOperationException(
                    "05B.1 production streaming build failed validation: " +
                    string.Join(" | ", result.Errors));
            }

            Debug.Log(
                "WORLD_STREAMING_05B1_FIXTURE_BUILD_OK " +
                result.Evidence);
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("05B.1 production streaming batch build requires batch mode.");
            }

            Build();
        }

        public static int GetEnabledBuildIndex(string scenePath)
        {
            int enabledIndex = 0;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (!scene.enabled)
                {
                    continue;
                }

                if (string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                {
                    return enabledIndex;
                }

                enabledIndex++;
            }

            return -1;
        }

        private static ProductionWorldStreamingManifest CreateOrUpdateManifest()
        {
            EnsureAssetFolder(ManifestAssetPath);
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(ManifestAssetPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
                manifest.name = "ProductionWorldStreamingManifest";
                AssetDatabase.CreateAsset(manifest, ManifestAssetPath);
            }

            int pilotBuildIndex = GetEnabledBuildIndex(PilotCellScenePath);
            int nextBuildIndex = GetEnabledBuildIndex(NextCellScenePath);
            if (pilotBuildIndex < 0 || nextBuildIndex < 0)
            {
                throw new InvalidOperationException("Both bounded production cells must be enabled in Build Settings.");
            }

            manifest.ConfigureForAuthoring(
                CellSizeMeters,
                LoadingRadiusCells,
                UnloadingRadiusCells,
                new[]
                {
                    new ProductionWorldCellScene(PilotCellId, 0, -3, pilotBuildIndex, PilotCellScenePath),
                    new ProductionWorldCellScene(NextCellId, 0, -2, nextBuildIndex, NextCellScenePath)
                });
            EditorUtility.SetDirty(manifest);
            return manifest;
        }

        private static void EnsurePrototypeFixtureScene()
        {
            EnsureAssetFolder(BootstrapScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    BootstrapScenePath) != null)
            {
                return;
            }

            if (!AssetDatabase.CopyAsset(
                    ActiveBootstrapScenePath,
                    BootstrapScenePath))
            {
                throw new InvalidOperationException(
                    "Could not create the 05B.1 prototype streaming fixture " +
                    "from the project Bootstrap scene.");
            }

            AssetDatabase.ImportAsset(
                BootstrapScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void WireBootstrapScene(
            ProductionWorldStreamingManifest manifest,
            GameObject playerPrefab)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Bootstrap wiring was cancelled because open scene changes were not saved.");
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            bool restoreSetup = !Application.isBatchMode && previousSetup.Length > 0;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
                GameCompositionRoot compositionRoot = RequireSingleSceneComponent<GameCompositionRoot>(scene);
                ProductionWorldStreamingService worldStreaming =
                    GetOrAddSingleSceneComponent<ProductionWorldStreamingService>(scene, compositionRoot.gameObject);
                ProductionWorldStreamingInstaller installer =
                    GetOrAddSingleSceneComponent<ProductionWorldStreamingInstaller>(scene, compositionRoot.gameObject);

                if (worldStreaming.gameObject != compositionRoot.gameObject ||
                    installer.gameObject != compositionRoot.gameObject)
                {
                    throw new InvalidOperationException(
                        "Existing production streaming components must be on the Game Composition Root object.");
                }

                worldStreaming.ConfigureForAuthoring(manifest);
                installer.ConfigureForAuthoring(
                    compositionRoot,
                    worldStreaming,
                    playerPrefab,
                    PlayerSpawnPosition,
                    PlayerSpawnRotation);
                installer.ConfigureWorldOnlyForAuthoring();

                EditorUtility.SetDirty(worldStreaming);
                EditorUtility.SetDirty(installer);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
                {
                    throw new InvalidOperationException("Could not save production streaming wiring to Bootstrap scene.");
                }
            }
            finally
            {
                if (restoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        private static T GetOrAddSingleSceneComponent<T>(Scene scene, GameObject owner)
            where T : Component
        {
            T[] components = GetSceneComponents<T>(scene);
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Bootstrap scene contains {components.Length} {typeof(T).Name} components; exactly one is allowed.");
            }

            return components.Length == 1 ? components[0] : owner.AddComponent<T>();
        }

        private static T RequireSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = GetSceneComponents<T>(scene);
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Bootstrap scene must contain exactly one {typeof(T).Name}; found {components.Length}.");
            }

            return components[0];
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
                .ToArray();
        }

        private static void EnsureBuildSettings()
        {
            var updated = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ActiveBootstrapScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (string.Equals(
                        existing.path,
                        ActiveBootstrapScenePath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool requiredCell = string.Equals(existing.path, PilotCellScenePath, StringComparison.Ordinal) ||
                                    string.Equals(existing.path, NextCellScenePath, StringComparison.Ordinal);
                updated.Add(new EditorBuildSettingsScene(existing.path, requiredCell || existing.enabled));
            }

            AddBuildSceneIfMissing(updated, BootstrapScenePath);
            AddBuildSceneIfMissing(updated, PilotCellScenePath);
            AddBuildSceneIfMissing(updated, NextCellScenePath);
            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static void AddBuildSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (scenes.Any(scene => string.Equals(scene.path, path, StringComparison.Ordinal)))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Required 05B.1 asset is missing: " + path);
            }

            return asset;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string current = "Assets";
            string[] segments = directory.Split('/');
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
