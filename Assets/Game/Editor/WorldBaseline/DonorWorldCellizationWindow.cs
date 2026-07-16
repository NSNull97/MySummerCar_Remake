using System;
using System.Linq;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public sealed class DonorWorldCellizationWindow : EditorWindow
    {
        private DonorWorldCellizationPlan plan;
        private string[] cellIds = Array.Empty<string>();
        private int selectedCellIndex;
        private bool drawCellBounds = true;

        [MenuItem(
            "Tools/MSC Remake/World Baseline 06B2/" +
            "Cell Ownership & Profile Window")]
        public static void Open()
        {
            GetWindow<DonorWorldCellizationWindow>(
                "World Baseline 06B2");
        }

        private void OnEnable()
        {
            RefreshPlan();
            SceneView.duringSceneGui += DrawSceneView;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneView;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Milestone 06B2 — Donor Feature-Parity Profile",
                EditorStyles.boldLabel);
            if (plan == null)
            {
                EditorGUILayout.HelpBox(
                    "The deterministic plan could not be loaded. Check the " +
                    "Console and frozen 06B1 inputs.",
                    MessageType.Error);
                if (GUILayout.Button("Retry"))
                {
                    RefreshPlan();
                }
                return;
            }

            EditorGUILayout.LabelField(
                "Ownership fingerprint",
                plan.OwnershipFingerprintSha256);
            EditorGUILayout.LabelField(
                "Entities",
                plan.Assignments.Count.ToString());
            EditorGUILayout.LabelField(
                "Global / cells",
                plan.GetOwnerEntries("global").Count + " / " +
                plan.CellIds.Count);
            EditorGUILayout.LabelField(
                "Safe colliders",
                plan.SafeColliders.Count.ToString());

            EditorGUILayout.Space();
            drawCellBounds = EditorGUILayout.Toggle(
                "Draw occupied cell bounds",
                drawCellBounds);
            if (cellIds.Length > 0)
            {
                selectedCellIndex = EditorGUILayout.Popup(
                    "Selected cell",
                    Mathf.Clamp(
                        selectedCellIndex,
                        0,
                        cellIds.Length - 1),
                    cellIds);
                string cellId = cellIds[selectedCellIndex];
                EditorGUILayout.LabelField(
                    "Owned entities",
                    plan.GetOwnerEntries(cellId).Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open cell additive"))
                    {
                        OpenCellAdditive(cellId);
                    }
                    if (GUILayout.Button("Unload cell"))
                    {
                        CloseCell(cellId);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Generation and validation",
                EditorStyles.boldLabel);
            if (GUILayout.Button("Build active donor streaming profile"))
            {
                DonorWorldCellizationBuilder.Build();
                RefreshPlan();
            }
            if (GUILayout.Button("Dry run + export manifest"))
            {
                DonorWorldCellizationBuilder
                    .DryRunAndExportOwnership();
                RefreshPlan();
            }
            if (GUILayout.Button("Duplicate / forbidden / profile scan"))
            {
                DonorWorldCellizationValidator.ValidateFromMenu();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "One-click editor visibility modes",
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Legacy"))
                {
                    OpenAndShowLegacy();
                }
                if (GUILayout.Button("Prototype fixture"))
                {
                    OpenSingleScene(
                        WorldBaseline06B2Paths.PrototypeFixtureScene);
                }
                if (GUILayout.Button("Production override"))
                {
                    OpenAndHideLegacy();
                }
            }
            EditorGUILayout.HelpBox(
                "Production override mode hides loaded legacy roots only in " +
                "the Editor visibility state. No production replacement art " +
                "is authored in 06B2.",
                MessageType.Info);

            DonorWorldBaselineEntityMetadata selected =
                Selection.activeGameObject == null
                    ? null
                    : Selection.activeGameObject
                        .GetComponentInParent<
                            DonorWorldBaselineEntityMetadata>();
            if (selected != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "Selected replacement key",
                    EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(
                    selected.ReplacementKey,
                    GUILayout.Height(
                        EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField(
                    "Source cell",
                    selected.SourceCellId);
                EditorGUILayout.LabelField(
                    "Category",
                    selected.SemanticCategory);
            }
        }

        private void DrawSceneView(SceneView sceneView)
        {
            if (!drawCellBounds || plan == null)
            {
                return;
            }

            Handles.color = new Color(0.15f, 0.8f, 1f, 0.85f);
            foreach (string cellId in plan.CellIds)
            {
                MSC.World.Partition.WorldCellIndex cell =
                    DonorWorldCellizationPlan.ParseCellId(cellId);
                Vector3 center = new Vector3(
                    (cell.X + 0.5f) *
                    WorldBaseline06B2Paths.CellSizeMeters,
                    0f,
                    (cell.Z + 0.5f) *
                    WorldBaseline06B2Paths.CellSizeMeters);
                Handles.DrawWireCube(
                    center,
                    new Vector3(
                        WorldBaseline06B2Paths.CellSizeMeters,
                        80f,
                        WorldBaseline06B2Paths.CellSizeMeters));
                Handles.Label(
                    center + Vector3.up * 42f,
                    cellId);
            }
        }

        private void RefreshPlan()
        {
            try
            {
                plan = DonorWorldCellizationPlan.Load();
                cellIds = plan.CellIds.ToArray();
                selectedCellIndex = Mathf.Clamp(
                    selectedCellIndex,
                    0,
                    Mathf.Max(0, cellIds.Length - 1));
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                plan = null;
                cellIds = Array.Empty<string>();
                Debug.LogException(exception);
            }
        }

        private static void OpenCellAdditive(string cellId)
        {
            string path =
                WorldBaseline06B2Paths.CellScene(cellId);
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (!loaded.IsValid() || !loaded.isLoaded)
            {
                EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
            }
        }

        private static void CloseCell(string cellId)
        {
            Scene scene = SceneManager.GetSceneByPath(
                WorldBaseline06B2Paths.CellScene(cellId));
            if (scene.IsValid() && scene.isLoaded)
            {
                if (!EditorSceneManager
                        .SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }
                EditorSceneManager.CloseScene(
                    scene,
                    removeScene: true);
            }
        }

        private static void OpenAndShowLegacy()
        {
            Scene scene = OpenSingleScene(
                WorldBaseline06B2Paths.GlobalScene);
            if (!scene.IsValid())
            {
                return;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneVisibilityManager.instance.Show(
                    root,
                    includeDescendants: true);
            }
        }

        private static void OpenAndHideLegacy()
        {
            Scene scene = OpenSingleScene(
                WorldBaseline06B2Paths.GlobalScene);
            if (!scene.IsValid())
            {
                return;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneVisibilityManager.instance.Hide(
                    root,
                    includeDescendants: true);
            }
        }

        private static Scene OpenSingleScene(string path)
        {
            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);
        }
    }
}
