using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public sealed class WorldRemasterDashboard : EditorWindow
    {
        private Vector2 scroll;
        private WorldProductionAssetRegistry registry;

        [MenuItem("Tools/MSC Remake/World Remaster/Open World Remaster Dashboard")]
        public static void Open()
        {
            WorldRemasterDashboard window = GetWindow<WorldRemasterDashboard>("World Remaster");
            window.minSize = new Vector2(620f, 520f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable() => Refresh();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Milestone 05A — World Remaster", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Production layer отделён от donor/reference. Первый pass ограничен home/garage pilot cell_0_-3; manual visual validation pending.",
                MessageType.Info);
            if (registry == null)
            {
                EditorGUILayout.HelpBox("Production registry не найден. Сначала выполните Rebuild Generated Production Cells.", MessageType.Warning);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            int mapped = registry.Records.Count(record => record.HasProductionReplacement);
            int approved = registry.Records.Count(record => record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified);
            int missing = registry.Records.Count - mapped;
            int blocked = registry.Records.Count(record => record.ReplacementStatus == WorldReplacementStatus.Blocked);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Registry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reference records", registry.Records.Count.ToString());
            EditorGUILayout.LabelField("Production bindings", mapped.ToString());
            EditorGUILayout.LabelField("Approved / Verified", approved.ToString());
            EditorGUILayout.LabelField("Missing replacements", missing.ToString());
            EditorGUILayout.LabelField("Blocked assets", blocked.ToString());
            EditorGUILayout.LabelField("Manual-art tasks", registry.ArtTasks.Count.ToString());
            EditorGUILayout.LabelField("Pilot", registry.PilotZoneId);

            GameObject pilot = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            int materials = AssetDatabase.FindAssets("t:Material", new[] { WorldRemasterPaths.MaterialRoot }).Length;
            int lodGroups = pilot != null ? pilot.GetComponentsInChildren<LODGroup>(true).Length : 0;
            int colliders = pilot != null ? pilot.GetComponentsInChildren<Collider>(true).Length : 0;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation snapshot", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Material assets", materials.ToString());
            EditorGUILayout.LabelField("Pilot LOD groups", lodGroups.ToString());
            EditorGUILayout.LabelField("Pilot colliders", colliders.ToString());
            EditorGUILayout.LabelField("Estimated scene memory", "See WORLD_REMASTER_PERFORMANCE_REPORT.md");
            EditorGUILayout.LabelField("Production-cell build", AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldRemasterPaths.PilotCellScene) != null ? "Built" : "Missing");
            EditorGUILayout.LabelField("Last validation", File.Exists(WorldRemasterPaths.ValidationReport) ? File.GetLastWriteTime(WorldRemasterPaths.ValidationReport).ToString("u") : "Never");

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build pilot")) ProductionWorldCellBuilder.BuildAll();
            if (GUILayout.Button("Validate")) ProductionCellValidationTool.ValidateSelectedReplacement();
            if (GUILayout.Button("Refresh")) Refresh();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open ledger")) WorldRemasterCommands.OpenReplacementLedger();
            if (GUILayout.Button("Open performance")) WorldRemasterCommands.OpenPerformanceReport();
            if (GUILayout.Button("Overlay comparison")) WorldRemasterCommands.ShowOverlayComparison();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Zone status", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (WorldProductionZoneRecord zone in registry.Zones)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(zone.ZoneId, GUILayout.Width(110f));
                EditorGUILayout.LabelField(zone.Status.ToString(), GUILayout.Width(145f));
                EditorGUILayout.LabelField($"{zone.MappedRecordCount}/{zone.ReferenceRecordCount} ({zone.CoveragePercent:0.000}%)", GUILayout.Width(170f));
                EditorGUILayout.LabelField(zone.ManualValidation);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            registry = AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            Repaint();
        }
    }

    public static class WorldRemasterCommands
    {
        [MenuItem("Tools/MSC Remake/World Remaster/Select Pilot Zone")]
        public static void SelectPilotZone()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PilotZonePrefab);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Show Donor Reference")]
        public static void ShowDonorReference() => OpenComparison(WorldComparisonMode.ReferenceOnly);

        [MenuItem("Tools/MSC Remake/World Remaster/Show Production Replacement")]
        public static void ShowProductionReplacement() => OpenComparison(WorldComparisonMode.ProductionOnly);

        [MenuItem("Tools/MSC Remake/World Remaster/Show Overlay Comparison")]
        public static void ShowOverlayComparison() => OpenComparison(WorldComparisonMode.OverlayComparison);

        [MenuItem("Tools/MSC Remake/World Remaster/Show Missing Replacements")]
        public static void ShowMissingReplacements()
        {
            WorldProductionAssetRegistry registry = LoadRegistry();
            int missing = registry != null ? registry.Records.Count(record => !record.HasProductionReplacement) : 0;
            Debug.Log($"WORLD_REMASTER_MISSING_REPLACEMENTS count={missing}; ledger={WorldRemasterPaths.ReplacementLedger}");
            OpenReplacementLedger();
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Show Donor Dependencies")]
        public static void ShowDonorDependencies()
        {
            var dependencies = ProductionCellValidationTool.FindDonorDependencies();
            Debug.Log(dependencies.Count == 0
                ? "WORLD_REMASTER_DONOR_DEPENDENCIES none"
                : string.Join(Environment.NewLine, dependencies));
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Show Bounds Deviations")]
        public static void ShowBoundsDeviations() => ProductionCellValidationTool.ValidateSelectedReplacement();

        [MenuItem("Tools/MSC Remake/World Remaster/Show Pivot Deviations")]
        public static void ShowPivotDeviations() => ProductionCellValidationTool.ValidateSelectedReplacement();

        [MenuItem("Tools/MSC Remake/World Remaster/Show Road Deviations")]
        public static void ShowRoadDeviations() => ProductionCellValidationTool.ValidateSelectedReplacement();

        [MenuItem("Tools/MSC Remake/World Remaster/Show Collision Deviations")]
        public static void ShowCollisionDeviations() => ProductionCellValidationTool.ValidateSelectedReplacement();

        [MenuItem("Tools/MSC Remake/World Remaster/Generate Art Backlog")]
        public static void GenerateArtBacklog()
        {
            WorldRemasterRegistryBuilder.BuildRegistryAndMachineReadableFiles();
            Debug.Log("WORLD_REMASTER_ART_BACKLOG_GENERATED " + WorldRemasterPaths.ArtBacklog);
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Open Replacement Ledger")]
        public static void OpenReplacementLedger() => Reveal(WorldRemasterPaths.ReplacementLedger);

        [MenuItem("Tools/MSC Remake/World Remaster/Open Performance Report")]
        public static void OpenPerformanceReport() => Reveal(WorldRemasterPaths.PerformanceReport);

        private static void OpenComparison(WorldComparisonMode mode)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldRemasterPaths.ComparisonScene) == null)
            {
                EditorUtility.DisplayDialog("World Remaster", "Comparison scene отсутствует. Выполните build pilot.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(WorldRemasterPaths.ComparisonScene, OpenSceneMode.Single);
            WorldRemasterModeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterModeController>(true))
                .FirstOrDefault();
            if (controller != null)
            {
                controller.ApplyMode(mode);
                Selection.activeObject = controller.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private static WorldProductionAssetRegistry LoadRegistry() =>
            AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);

        private static void Reveal(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute))
            {
                EditorUtility.DisplayDialog("World Remaster", "File not found: " + path, "OK");
                return;
            }

            EditorUtility.RevealInFinder(absolute);
        }
    }
}
