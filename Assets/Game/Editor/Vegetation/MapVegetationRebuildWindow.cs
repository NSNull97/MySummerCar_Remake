using System;
using System.Collections.Generic;
using System.Linq;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    public sealed class MapVegetationRebuildWindow : EditorWindow
    {
        private MapVegetationRebuildOptions options;
        private MapVegetationCellPlan preview;
        private List<MapVegetationExclusionDebugBounds> exclusions;
        private UnityEditor.Editor placementEditor;
        private Vector2 scroll;
        private bool settingsExpanded = true, issuesExpanded = true;
        private string status = "Read-only preview first; the pilot must pass before full-map generation.";

        [MenuItem("Tools/MSC Remake/Vegetation/Map Vegetation Rebuild", priority = 1890)]
        public static void Open() => GetWindow<MapVegetationRebuildWindow>("Map Vegetation");

        private void OnEnable() { options = MapVegetationRebuild.LoadOptions(); SceneView.duringSceneGui += DrawPreview; }
        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            if (placementEditor != null) DestroyImmediate(placementEditor);
        }

        private void OnGUI()
        {
            if (options == null) { if (GUILayout.Button("Load settings")) options = MapVegetationRebuild.LoadOptions(); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Source Data / Coordinate Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The frozen donor meshes are already mapped into project coordinates. Their recovered roots keep X/Z exactly; height is sampled from allowed current ground. Unpaired source cards are reported with lower confidence.", MessageType.Info);
            var serialized = new SerializedObject(options);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("selectedCell"), new GUIContent("Streaming cell"));
            EditorGUILayout.PropertyField(serialized.FindProperty("categories"), new GUIContent("Categories to replace"));
            settingsExpanded = EditorGUILayout.Foldout(settingsExpanded, "Surface Detection / Exclusion Rules / Original Trees", true);
            if (settingsExpanded)
            {
                if (placementEditor == null || placementEditor.target != options.Placement)
                {
                    if (placementEditor != null) DestroyImmediate(placementEditor);
                    placementEditor = UnityEditor.Editor.CreateEditor(options.Placement);
                }
                placementEditor.OnInspectorGUI();
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Boundary Forest", EditorStyles.boldLabel);
            foreach (string field in new[] { "sprucePercent", "pinePercent", "birchPercent", "aspenPercent", "boundaryDepthMeters", "boundaryCandidateSpacingMeters", "boundaryFrontDensity", "boundaryBackDensity", "boundaryFrontHeightRange", "boundaryBackHeightRange", "undergrowthSpacingMeters", "undergrowthDensity" })
                EditorGUILayout.PropertyField(serialized.FindProperty(field));
            EditorGUILayout.LabelField("Grass", EditorStyles.boldLabel);
            foreach (string field in new[] { "grassProfiles", "grassSpacingMeters", "grassDensity" }) EditorGUILayout.PropertyField(serialized.FindProperty(field));
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            foreach (string field in new[] { "forestLoadingRadiusCells", "createTreeColliders", "createBoundaryColliders" }) EditorGUILayout.PropertyField(serialized.FindProperty(field));
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            foreach (string field in new[] { "showPreview", "showExclusions", "maximumPreviewSamples" }) EditorGUILayout.PropertyField(serialized.FindProperty(field));
            if (serialized.ApplyModifiedProperties()) SceneView.RepaintAll();
            if (GUILayout.Button("Preview selected cell — no generated content is saved")) Execute(Preview);
            EditorGUILayout.LabelField("Generate / Clear", EditorStyles.boldLabel);
            if (GUILayout.Button("Prepare existing art: HDRP materials and detailed grass profiles")) Execute(MapVegetationRebuild.PrepareApprovedArtBatch);
            EditorGUILayout.HelpBox("Each run backs up replaced generated files. Regeneration replaces selected category roots; it never creates a second renderer system. Existing painter masks remain untouched.", MessageType.None);
            if (GUILayout.Button("Generate + validate pilot cell")) Execute(() => MapVegetationRebuild.Run(options, false, false));
            if (GUILayout.Button("Capture saved pilot in HDRP — inspect all PNGs")) Execute(MapVegetationVisualAudit.CapturePilotBatch);
            if (GUILayout.Button("Generate + validate all cells (requires matching pilot)")) Execute(() => MapVegetationRebuild.Run(options, true, false));
            if (GUILayout.Button("Validate all saved cells")) Execute(() => MapVegetationRebuild.Run(options, true, true));
            if (GUILayout.Button("Clear selected generated categories in selected cell")) Execute(() => MapVegetationRebuild.Clear(options, false));
            if (GUILayout.Button("Clear selected generated categories in all cells")) Execute(() => MapVegetationRebuild.Clear(options, true));
            if (GUILayout.Button("Show reports and reversible backups")) EditorUtility.RevealInFinder(MapVegetationRebuild.Reports);
            EditorGUILayout.LabelField("Validation Report", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            if (preview != null)
            {
                EditorGUILayout.LabelField($"{preview.Cell.Id}: woody {preview.Woody.Count:N0}, grass {preview.Grass.Count:N0}, rejected reasons {preview.Rejections.Count}");
                issuesExpanded = EditorGUILayout.Foldout(issuesExpanded, "Skipped points (first 100; full CSV in reports)", true);
                if (issuesExpanded)
                    foreach (MapVegetationIssue issue in preview.Issues.Take(100))
                        if (GUILayout.Button(issue.category + " — " + issue.reason + " at " + issue.position.ToString("F1"), EditorStyles.miniButton))
                            Frame(issue.position);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Execute(Action action)
        {
            try { AssetDatabase.SaveAssets(); action(); status = "Completed. See Artifacts/VegetationRebuild for exact counts and skipped-source reasons."; }
            catch (Exception exception) { status = exception.Message; Debug.LogException(exception); }
            Repaint(); SceneView.RepaintAll();
        }

        private void Preview()
        {
            using var context = new MapVegetationContext(options);
            preview = context.CellPlan(MapVegetationRebuild.ParseCell(options.SelectedCell), true);
            exclusions = context.Surfaces.ExclusionDebugBounds.ToList();
            Vector3 center = preview.Woody.Count > 0 ? preview.Woody[0].position : preview.Grass.Count > 0 ? preview.Grass[0].position : Vector3.zero;
            Frame(center);
        }

        private static void Frame(Vector3 position)
        {
            SceneView.lastActiveSceneView?.Frame(new Bounds(position, Vector3.one * 30f), false);
        }

        private void DrawPreview(SceneView view)
        {
            if (options == null || !options.ShowPreview || preview == null) return;
            int maximum = options.MaximumPreviewSamples;
            int stride = Math.Max(1, preview.Grass.Count / Math.Max(1, maximum / 2));
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = new Color(0.25f, 1f, 0.3f, 0.75f);
            for (int i = 0; i < preview.Grass.Count && i / stride < maximum / 2; i += stride)
                Handles.DotHandleCap(0, preview.Grass[i].position, Quaternion.identity, 0.3f, EventType.Repaint);
            foreach (MapVegetationPlacement point in preview.Woody.Take(maximum / 2))
            {
                Handles.color = point.category == MapVegetationCategories.BoundaryForest ? Color.cyan : new Color(0.02f, 0.45f, 0.1f);
                Handles.DrawLine(point.position, point.position + Vector3.up * Mathf.Clamp(point.height, 1f, 20f), 2f);
                Handles.DrawWireDisc(point.sourcePosition, Vector3.up, 0.7f);
            }
            Handles.color = Color.red;
            foreach (MapVegetationIssue issue in preview.Issues.Take(maximum / 4))
            {
                Handles.DrawLine(issue.position - Vector3.right, issue.position + Vector3.right);
                Handles.DrawLine(issue.position - Vector3.forward, issue.position + Vector3.forward);
            }
            if (!options.ShowExclusions || exclusions == null) return;
            float size = options.Placement.CellSizeMeters;
            Bounds cellBounds = new Bounds(new Vector3((preview.Cell.X + 0.5f) * size, 0f, (preview.Cell.Z + 0.5f) * size), new Vector3(size, 10000f, size));
            int shown = 0;
            foreach (MapVegetationExclusionDebugBounds exclusion in exclusions)
            {
                if (!cellBounds.Intersects(exclusion.Bounds) || shown++ >= maximum) continue;
                Handles.color = ExclusionColor(exclusion.Category);
                Handles.DrawWireCube(exclusion.Bounds.center, exclusion.Bounds.size);
                Bounds margin = exclusion.Bounds; margin.Expand(exclusion.MaximumMargin * 2f);
                Handles.color = new Color(1f, 0.85f, 0.1f, 0.4f); Handles.DrawWireCube(margin.center, margin.size);
            }
        }

        private static Color ExclusionColor(MapVegetationExclusionKind category)
        {
            if (category == MapVegetationExclusionKind.Water) return Color.blue;
            if (category == MapVegetationExclusionKind.AsphaltRoad || category == MapVegetationExclusionKind.DirtRoad || category == MapVegetationExclusionKind.Driveway) return new Color(1f, 0.45f, 0f);
            if (category == MapVegetationExclusionKind.AgriculturalField || category == MapVegetationExclusionKind.OpenSpace) return new Color(0.7f, 0.1f, 1f);
            return Color.red;
        }
    }
}
