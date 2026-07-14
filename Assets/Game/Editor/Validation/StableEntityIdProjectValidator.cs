using System.Collections.Generic;
using MSC.Core.Identity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Validation
{
    public static class StableEntityIdProjectValidator
    {
        public static IReadOnlyList<StableEntityIdIssue> ValidateProjectContent()
        {
            var candidates = new List<StableEntityIdCandidate>();
            AddProjectSceneCandidates(candidates);
            AddUnsavedOpenSceneCandidates(candidates);
            AddPrefabCandidates(candidates);
            return StableEntityIdValidation.Validate(candidates);
        }

        private static void AddProjectSceneCandidates(List<StableEntityIdCandidate> candidates)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Game" });
            foreach (string sceneGuid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                AddSceneCandidates(scene, candidates);

                if (openedForValidation)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static void AddUnsavedOpenSceneCandidates(List<StableEntityIdCandidate> candidates)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (string.IsNullOrEmpty(scene.path))
                {
                    AddSceneCandidates(scene, candidates);
                }
            }
        }

        private static void AddSceneCandidates(Scene scene, List<StableEntityIdCandidate> candidates)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                StableEntityIdAuthoring[] components =
                    rootObject.GetComponentsInChildren<StableEntityIdAuthoring>(true);
                foreach (StableEntityIdAuthoring component in components)
                {
                    string scenePath = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                    string context = $"Scene:{scenePath}:{GetHierarchyPath(component.transform)}";
                    candidates.Add(new StableEntityIdCandidate(context, component.SerializedId));
                }
            }
        }

        private static void AddPrefabCandidates(List<StableEntityIdCandidate> candidates)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game" });
            foreach (string prefabGuid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                StableEntityIdAuthoring[] components = prefab.GetComponentsInChildren<StableEntityIdAuthoring>(true);
                foreach (StableEntityIdAuthoring component in components)
                {
                    string context = $"Prefab:{prefabPath}:{GetHierarchyPath(component.transform)}";
                    candidates.Add(new StableEntityIdCandidate(context, component.SerializedId));
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
