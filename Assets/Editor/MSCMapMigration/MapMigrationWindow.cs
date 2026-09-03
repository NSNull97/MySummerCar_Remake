using System;
using UnityEditor;
using UnityEngine;

namespace MSCMapMigration
{
    public sealed class MapMigrationWindow : EditorWindow
    {
        private MapMigrationSettings settings;
        private UnityEditor.Editor settingsEditor;
        private Vector2 scroll;

        [MenuItem("Tools/MSC Remake/Map Migration/Open Migration Window")]
        public static void Open()
        {
            GetWindow<MapMigrationWindow>("Map Migration");
        }

        private void OnEnable()
        {
            settings = MapMigrationSettings.LoadOrCreate();
            settingsEditor = UnityEditor.Editor.CreateEditor(settings);
        }

        private void OnDisable()
        {
            if (settingsEditor != null)
            {
                DestroyImmediate(settingsEditor);
            }
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                settings = MapMigrationSettings.LoadOrCreate();
            }

            EditorGUILayout.HelpBox(
                "The tool never edits the source scene. It writes a separate scene, " +
                "Assets/_Generated/MapTerrainMigration, and a BlenderWork export outside Assets.",
                MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            settingsEditor?.OnInspectorGUI();
            EditorGUILayout.Space(8f);
            DrawButton("1. Scan Source Map", MapMigrationBatch.ScanSourceMap);
            DrawButton("2. Export Source Meshes for Blender", MapMigrationBatch.ExportSourceMeshesForBlender);
            DrawButton("3. Build Terrain Preview", MapMigrationBatch.BuildTerrainPreview);
            DrawButton("4. Validate Migration", MapMigrationBatch.ValidateMigration);
            DrawButton("5. Commit Terrain Replacement", MapMigrationBatch.CommitTerrainReplacement);
            DrawButton("6. Run Full Pipeline", MapMigrationBatch.RunFullPipeline);
            DrawButton("7. Open Export Folder", MapMigrationBatch.OpenExportFolder);
            DrawButton("8. Open Generated Scene", MapMigrationBatch.OpenGeneratedScene);
            DrawButton("9. Generate Side-by-Side QA", MapMigrationBatch.GenerateVisualComparison);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawButton(string label, Action action)
        {
            if (!GUILayout.Button(label, GUILayout.Height(28f)))
            {
                return;
            }

            try
            {
                action();
            }
            catch (MapMigrationCancelledException)
            {
                Debug.LogWarning("Map migration operation cancelled. Source content was not modified.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Map Migration Failed",
                    exception.GetBaseException().Message,
                    "OK");
            }
            finally
            {
                MapMigrationProgress.Clear();
            }
        }
    }
}
