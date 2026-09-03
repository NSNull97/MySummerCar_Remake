using System;
using System.Linq;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    [EditorTool("MSC Mesh Vegetation Painter")]
    public sealed class VegetationPainterTool : EditorTool
    {
        private const float PanelWidth = 310f;

        private readonly VegetationMaskBrush brush = new VegetationMaskBrush();
        private VegetationCellCatalog catalog;
        private VegetationPainterSettings settings;
        private Vector3 lastPaintPosition;
        private bool painting;
        private int hotControl;

        public override GUIContent toolbarIcon =>
            EditorGUIUtility.IconContent(
                "TerrainInspector.TerrainToolPlants",
                "|MSC mesh vegetation painter");

        [MenuItem(
            "Tools/MSC Remake/Vegetation/Activate Mesh Painter",
            priority = 1902)]
        private static void ActivateFromMenu()
        {
            ToolManager.SetActiveTool<VegetationPainterTool>();
        }

        public override void OnActivated()
        {
            settings = VegetationPainterSettings.instance;
            catalog = FindCatalog();
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (painting)
            {
                brush.CancelStroke();
            }

            painting = false;
            if (GUIUtility.hotControl == hotControl)
            {
                GUIUtility.hotControl = 0;
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView))
            {
                return;
            }

            if (settings == null)
            {
                settings = VegetationPainterSettings.instance;
            }

            DrawSettingsPanel();
            Event currentEvent = Event.current;
            if (catalog == null || catalog.Profiles.Count == 0)
            {
                Handles.BeginGUI();
                GUI.Label(
                    new Rect(12f, 340f, PanelWidth, 40f),
                    "Создайте VegetationCellCatalog через Tools > MSC Remake > Vegetation.");
                Handles.EndGUI();
                return;
            }

            int profileIndex = Mathf.Clamp(
                settings.ProfileIndex,
                0,
                catalog.Profiles.Count - 1);
            VegetationProfile profile = catalog.Profiles[profileIndex];
            if (profile == null)
            {
                return;
            }

            Ray sceneRay = HandleUtility.GUIPointToWorldRay(
                currentEvent.mousePosition);
            bool hasHit = VegetationSceneRaycaster.TryResolve(
                sceneRay,
                catalog.CandidateRaycastDistance,
                catalog.RelevantRaycastLayers,
                profile.DensityChannel,
                out VegetationResolvedHit resolved,
                out _);
            if (hasHit)
            {
                Handles.color = settings.Operation ==
                                VegetationBrushOperation.Erase ||
                                currentEvent.shift
                    ? new Color(1f, 0.25f, 0.1f, 0.95f)
                    : new Color(0.2f, 1f, 0.35f, 0.95f);
                Handles.DrawWireDisc(
                    resolved.Hit.point,
                    resolved.Hit.normal,
                    settings.Radius);
                Handles.color = new Color(
                    Handles.color.r,
                    Handles.color.g,
                    Handles.color.b,
                    0.12f);
                Handles.DrawSolidDisc(
                    resolved.Hit.point,
                    resolved.Hit.normal,
                    settings.Radius * settings.Hardness);
            }

            HandleUtility.AddDefaultControl(
                GUIUtility.GetControlID(FocusType.Passive));
            HandlePaintingInput(currentEvent, hasHit, resolved, profile);
            sceneView.Repaint();
        }

        private void HandlePaintingInput(
            Event currentEvent,
            bool hasHit,
            VegetationResolvedHit resolved,
            VegetationProfile profile)
        {
            if (currentEvent.alt)
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 0 &&
                hasHit)
            {
                hotControl = GUIUtility.GetControlID(FocusType.Passive);
                GUIUtility.hotControl = hotControl;
                VegetationBrushOperation operation = currentEvent.shift
                    ? VegetationBrushOperation.Erase
                    : settings.Operation;
                brush.BeginStroke(
                    catalog,
                    profile.DensityChannel,
                    operation);
                painting = true;
                lastPaintPosition = resolved.Hit.point;
                brush.ApplyDab(
                    lastPaintPosition,
                    settings.Radius,
                    settings.Strength,
                    settings.Hardness);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDrag &&
                currentEvent.button == 0 &&
                painting &&
                hasHit)
            {
                PaintSpacedSegment(lastPaintPosition, resolved.Hit.point);
                lastPaintPosition = resolved.Hit.point;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseUp &&
                currentEvent.button == 0 &&
                painting)
            {
                brush.EndStroke();
                painting = false;
                if (GUIUtility.hotControl == hotControl)
                {
                    GUIUtility.hotControl = 0;
                }

                currentEvent.Use();
            }

            if (currentEvent.type == EventType.KeyDown &&
                currentEvent.keyCode == KeyCode.Escape &&
                painting)
            {
                brush.CancelStroke();
                painting = false;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }
        }

        private void PaintSpacedSegment(Vector3 from, Vector3 to)
        {
            float dabSpacing = Mathf.Max(
                0.05f,
                settings.Radius * settings.Spacing);
            float distance = Vector3.Distance(from, to);
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(distance / dabSpacing));
            for (int step = 1; step <= stepCount; step++)
            {
                brush.ApplyDab(
                    Vector3.Lerp(from, to, step / (float)stepCount),
                    settings.Radius,
                    settings.Strength,
                    settings.Hardness);
            }
        }

        private void DrawSettingsPanel()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(
                new Rect(12f, 42f, PanelWidth, 292f),
                "Mesh Vegetation Painter",
                GUI.skin.window);

            EditorGUI.BeginChangeCheck();
            catalog = (VegetationCellCatalog)EditorGUILayout.ObjectField(
                "Каталог",
                catalog,
                typeof(VegetationCellCatalog),
                false);
            if (catalog != null && catalog.Profiles.Count > 0)
            {
                string[] profileNames = catalog.Profiles
                    .Select(profile => profile != null
                        ? $"{profile.ProfileId} [{profile.DensityChannel}]"
                        : "<missing>")
                    .ToArray();
                settings.ProfileIndex = EditorGUILayout.Popup(
                    "Профиль / канал",
                    Mathf.Clamp(
                        settings.ProfileIndex,
                        0,
                        profileNames.Length - 1),
                    profileNames);
            }

            settings.Operation = (VegetationBrushOperation)EditorGUILayout.EnumPopup(
                "Операция",
                settings.Operation);
            settings.Radius = EditorGUILayout.Slider(
                "Радиус, м",
                settings.Radius,
                0.25f,
                64f);
            settings.Strength = EditorGUILayout.Slider(
                "Сила",
                settings.Strength,
                0f,
                1f);
            settings.Hardness = EditorGUILayout.Slider(
                "Жёсткость",
                settings.Hardness,
                0f,
                1f);
            settings.Spacing = EditorGUILayout.Slider(
                "Шаг × радиус",
                settings.Spacing,
                0.05f,
                1f);

            EditorGUILayout.HelpBox(
                "ЛКМ — рисовать; Shift+ЛКМ — стирать; Esc — отменить штрих. " +
                "Перестраиваются только dirty-тайлы после отпускания ЛКМ.",
                MessageType.Info);
            if (EditorGUI.EndChangeCheck())
            {
                settings.Persist();
                SceneView.RepaintAll();
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void OnUndoRedo()
        {
            if (catalog == null)
            {
                return;
            }

            VegetationWorldRenderer[] renderers =
                UnityEngine.Object.FindObjectsByType<VegetationWorldRenderer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].Catalog == catalog)
                {
                    renderers[index].RebuildGpuResources();
                }
            }

            SceneView.RepaintAll();
        }

        private static VegetationCellCatalog FindCatalog()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:VegetationCellCatalog",
                new[] { "Assets/Game/World/Content/Vegetation" });
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<VegetationCellCatalog>(
                    AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }
    }
}
