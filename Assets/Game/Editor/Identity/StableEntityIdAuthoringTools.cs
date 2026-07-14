using System.Collections.Generic;
using MSC.Core.Identity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.Editor.Identity
{
    public static class StableEntityIdAuthoringTools
    {
        private const string StableIdPropertyName = "stableId";

        [MenuItem("Tools/My Summer Car/Stable IDs/Assign Missing IDs In Open Scenes")]
        public static void AssignMissingIdsInOpenScenes()
        {
            StableEntityIdAuthoring[] authoringComponents = Object.FindObjectsByType<StableEntityIdAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int assignedCount = 0;
            foreach (StableEntityIdAuthoring authoring in authoringComponents)
            {
                if (!authoring.gameObject.scene.IsValid() || !authoring.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(authoring.SerializedId))
                {
                    continue;
                }

                SetStableId(authoring, StableEntityId.New());
                assignedCount++;
            }

            Debug.Log($"Stable ID authoring: assigned {assignedCount} missing ID(s) in loaded scenes.");
        }

        [MenuItem("Tools/My Summer Car/Stable IDs/Regenerate IDs On Selected Objects")]
        public static void RegenerateIdsOnSelectedObjects()
        {
            var selectedAuthoring = new List<StableEntityIdAuthoring>();
            foreach (GameObject selectedObject in Selection.gameObjects)
            {
                StableEntityIdAuthoring authoring = selectedObject.GetComponent<StableEntityIdAuthoring>();
                if (authoring != null)
                {
                    selectedAuthoring.Add(authoring);
                }
            }

            if (selectedAuthoring.Count == 0)
            {
                Debug.LogWarning("Stable ID authoring: no selected object has StableEntityIdAuthoring.");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Regenerate stable IDs?",
                $"This will explicitly replace {selectedAuthoring.Count} persistent ID(s). " +
                "Existing save records may require migration.",
                "Regenerate",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            foreach (StableEntityIdAuthoring authoring in selectedAuthoring)
            {
                SetStableId(authoring, StableEntityId.New());
            }

            Debug.Log($"Stable ID authoring: regenerated {selectedAuthoring.Count} selected ID(s).");
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, StableEntityId stableId)
        {
            Undo.RecordObject(authoring, "Assign stable entity ID");

            var serializedObject = new SerializedObject(authoring);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(StableIdPropertyName);
            property.stringValue = stableId.Value;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
        }
    }
}
