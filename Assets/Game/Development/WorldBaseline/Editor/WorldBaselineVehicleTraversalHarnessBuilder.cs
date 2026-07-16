using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Development.WorldBaseline.Editor
{
    public static class WorldBaselineVehicleTraversalHarnessBuilder
    {
        public const string BuilderVersion = "1.0.0";
        public const string MenuRoot =
            "Tools/MSC Remake/World Baseline/06B3/";
        public const string ColliderAllowlistPath =
            "Assets/Game/LegacyImport/Manifests/WorldBaseline06B2SafeColliderAllowlist.csv";

        private static readonly HashSet<string> AllowedTraversalReasons =
            new HashSet<string>(StringComparer.Ordinal)
        {
            "Continuous roadside traversal",
            "Continuous pavement traversal",
            "Continuous road traversal",
            "Continuous asphalt traversal",
            "Continuous dirt road traversal",
            "Continuous gravel traversal"
        };

        [MenuItem(MenuRoot + "Build Vehicle Traversal Harness")]
        public static void Build()
        {
            EnsureAssetFolder(
                Path.GetDirectoryName(
                        WorldBaselineVehicleTraversalHarness
                            .HarnessScenePath)
                    ?.Replace('\\', '/'));
            AssertHarnessSceneIsExcludedFromBuildSettings();

            string[] traversalColliderIds =
                ReadTraversalColliderIds();
            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    Application.isBatchMode
                        ? NewSceneMode.Single
                        : NewSceneMode.Additive);
            try
            {
                GameObject root =
                    new GameObject(
                        "M06B3_VehicleTraversalHarness");
                SceneManager.MoveGameObjectToScene(root, scene);
                WorldBaselineVehicleTraversalHarness harness =
                    root.AddComponent<
                        WorldBaselineVehicleTraversalHarness>();
                harness.ConfigureForAuthoring(
                    traversalColliderIds);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(
                        scene,
                        WorldBaselineVehicleTraversalHarness
                            .HarnessScenePath))
                {
                    throw new InvalidOperationException(
                        "Could not save the 06B3 vehicle traversal " +
                        "harness scene.");
                }
            }
            finally
            {
                if (!Application.isBatchMode &&
                    scene.IsValid() &&
                    scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(
                        scene,
                        removeScene: true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssertHarnessSceneIsExcludedFromBuildSettings();
            Debug.Log(
                "M06B3_VEHICLE_TRAVERSAL_HARNESS_BUILD_OK " +
                $"version={BuilderVersion} " +
                $"colliderIds={traversalColliderIds.Length} " +
                "buildSettings=excluded");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "06B3 harness batch build requires batch mode.");
            }

            Build();
        }

        [MenuItem(MenuRoot + "Open Vehicle Traversal Harness")]
        public static void OpenAndPlay()
        {
            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(
                    "06B3 vehicle traversal harness open cancelled.");
                return;
            }

            Build();
            RequireSceneAsset(
                WorldBaselineVehicleTraversalHarness
                    .BootstrapScenePath);
            RequireSceneAsset(
                WorldBaselineVehicleTraversalHarness
                    .VehiclePrototypeScenePath);
            RequireSceneAsset(
                WorldBaselineVehicleTraversalHarness
                    .HarnessScenePath);

            Scene bootstrap =
                EditorSceneManager.OpenScene(
                    WorldBaselineVehicleTraversalHarness
                        .BootstrapScenePath,
                    OpenSceneMode.Single);
            EditorSceneManager.OpenScene(
                WorldBaselineVehicleTraversalHarness
                    .VehiclePrototypeScenePath,
                OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(
                WorldBaselineVehicleTraversalHarness
                    .HarnessScenePath,
                OpenSceneMode.Additive);
            SceneManager.SetActiveScene(bootstrap);

            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPlaying = true;
                }
            };
        }

        public static string[] ReadTraversalColliderIds()
        {
            string absolutePath =
                Path.GetFullPath(ColliderAllowlistPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "06B3 collider allowlist is missing.",
                    absolutePath);
            }

            string[] lines =
                File.ReadAllLines(absolutePath);
            if (lines.Length < 2 ||
                !string.Equals(
                    lines[0],
                    "ColliderStableId,EntityStableId,ExpectedColliderType,ExpectedMeshGuid,OwnershipPolicy,Reason",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "06B3 collider allowlist header is invalid.");
            }

            var result =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                string[] columns = lines[index].Split(',');
                if (columns.Length != 6)
                {
                    throw new InvalidDataException(
                        "06B3 collider allowlist row " +
                        (index + 1) +
                        " must contain exactly six columns.");
                }

                if (AllowedTraversalReasons.Contains(columns[5]))
                {
                    result.Add(columns[0]);
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException(
                    "06B3 collider allowlist contains no " +
                    "road/asphalt/dirt/gravel/pavement rows.");
            }

            return result
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AssertHarnessSceneIsExcludedFromBuildSettings()
        {
            bool included =
                EditorBuildSettings.scenes.Any(scene =>
                    string.Equals(
                        scene.path,
                        WorldBaselineVehicleTraversalHarness
                            .HarnessScenePath,
                        StringComparison.Ordinal));
            if (included)
            {
                throw new InvalidOperationException(
                    "The development-only 06B3 vehicle traversal harness " +
                    "scene must remain excluded from Build Settings.");
            }
        }

        private static void RequireSceneAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                throw new FileNotFoundException(
                    "Required 06B3 harness scene is missing.",
                    path);
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent =
                Path.GetDirectoryName(folder)
                    ?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            string name = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
