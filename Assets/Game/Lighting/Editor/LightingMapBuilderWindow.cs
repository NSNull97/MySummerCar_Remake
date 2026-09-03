using UnityEditor;
using UnityEngine;

namespace MSC.Lighting.Editor
{
    public sealed class LightingMapBuilderWindow : EditorWindow
    {
        private LightingAuditDocument lastAudit;

        [MenuItem("Tools/Lighting/Lighting Map Builder")]
        public static void Open()
        {
            var window = GetWindow<LightingMapBuilderWindow>();
            window.titleContent = new GUIContent("Lighting Map Builder");
            window.minSize = new Vector2(460f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "My Summer Car — Local Lighting Map",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Audit scans all project game scenes and prefabs. Safe apply only binds existing, confidently classified Lights in project-owned assets. Ambiguous or donor-baseline candidates remain in LightingManualReview.md.",
                MessageType.Info);

            if (GUILayout.Button("1. Build / Update Profiles"))
            {
                LightingContentBuilder.BuildProfilesMenu();
            }

            if (GUILayout.Button("2. Audit Only"))
            {
                lastAudit = LightingMapBuilder.ScanProject(false);
                Debug.Log(
                    $"[Lighting] Audit complete: {lastAudit.candidates} " +
                    $"candidates, {lastAudit.manualReview} manual.");
            }

            if (GUILayout.Button("3. Build Full Lighting Map"))
            {
                LightingMapBuilder.BuildFromBatch();
                lastAudit = LightingMapBuilder.ScanProject(false);
            }

            if (lastAudit != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Scenes / Prefabs",
                    $"{lastAudit.scenesScanned} / {lastAudit.prefabsScanned}");
                EditorGUILayout.LabelField(
                    "Candidates / Configured / Manual",
                    $"{lastAudit.candidates} / " +
                    $"{lastAudit.configuredFixtures} / " +
                    $"{lastAudit.manualReview}");
            }
        }
    }
}
