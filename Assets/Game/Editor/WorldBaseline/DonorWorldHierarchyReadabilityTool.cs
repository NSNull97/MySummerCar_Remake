using System;
using System.Linq;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldHierarchyReadabilityTool
    {
        private const string GeneratedWorldFolder =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World";

        [MenuItem(
            "MSC/World/Legacy Baseline/Rename Generated Entities For Readability")]
        public static void RenameFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Generated hierarchy names cannot be changed in Play Mode.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            RenameGeneratedEntities();
        }

        public static void RenameFromCommandLine()
        {
            RenameGeneratedEntities();
        }

        private static void RenameGeneratedEntities()
        {
            string[] scenePaths = AssetDatabase
                .FindAssets("t:Scene", new[] { GeneratedWorldFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            int renamedEntities = 0;
            int changedScenes = 0;

            for (int sceneIndex = 0;
                 sceneIndex < scenePaths.Length;
                 sceneIndex++)
            {
                Scene scene = EditorSceneManager.OpenScene(
                    scenePaths[sceneIndex],
                    OpenSceneMode.Single);
                DonorWorldBaselineEntityMetadata[] entities =
                    scene.GetRootGameObjects()
                        .SelectMany(root =>
                            root.GetComponentsInChildren<
                                DonorWorldBaselineEntityMetadata>(true))
                        .ToArray();
                int sceneRenamed = 0;
                for (int entityIndex = 0;
                     entityIndex < entities.Length;
                     entityIndex++)
                {
                    DonorWorldBaselineEntityMetadata entity =
                        entities[entityIndex];
                    string displayName =
                        DonorWorldBaselineDisplayName.Create(
                            entity.SourceHierarchyPath,
                            entity.SourceObjectId,
                            entity.SemanticCategory);
                    if (string.Equals(
                            entity.name,
                            displayName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entity.name = displayName;
                    EditorUtility.SetDirty(entity.gameObject);
                    sceneRenamed++;
                }

                if (sceneRenamed <= 0)
                {
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                renamedEntities += sceneRenamed;
                changedScenes++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Readable donor hierarchy names applied: " +
                $"{renamedEntities} entities in {changedScenes} scenes.");
        }
    }
}
