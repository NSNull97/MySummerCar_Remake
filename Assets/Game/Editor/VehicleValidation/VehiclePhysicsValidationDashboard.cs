using System.IO;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.Editor.VehicleValidation
{
    public sealed class VehiclePhysicsValidationDashboard : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem(VehiclePhysicsValidationPaths.MenuRoot + "Dashboard")]
        public static void Open()
        {
            GetWindow<VehiclePhysicsValidationDashboard>("Vehicle Physics 06A");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Milestone 06A", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Validation/tuning gate. Dynamic values remain ProvisionalProjectTuning; " +
                "missing donor targets remain Unknown.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build / Rebuild"))
                {
                    VehiclePhysicsValidationBuilder.Build();
                }

                if (GUILayout.Button("Validate"))
                {
                    VehiclePhysicsValidationValidator.ValidateOrThrow();
                }

                if (GUILayout.Button("Performance Audit"))
                {
                    VehiclePhysicsValidationPerformanceAudit.Run();
                }

                if (GUILayout.Button("Export CSV"))
                {
                    VehiclePhysicsValidationEvidenceExporter.Run();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Validation Scene"))
                {
                    EditorSceneManager.OpenScene(VehiclePhysicsValidationPaths.Scene);
                }

                if (GUILayout.Button("Open Validation Docs"))
                {
                    string path = Path.GetFullPath(
                        VehiclePhysicsValidationPaths.DocumentationRoot + "/VALIDATION_PLAN.md");
                    EditorUtility.RevealInFinder(path);
                }
            }

            VehicleCalibrationProfile profile =
                AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                    VehiclePhysicsValidationPaths.Profile);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Calibration profile has not been built.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(profile, typeof(VehicleCalibrationProfile), false);
            EditorGUILayout.LabelField("ID", profile.ProfileId);
            EditorGUILayout.LabelField("Revision", profile.Revision);
            EditorGUILayout.LabelField("Fixtures", profile.Fixtures.Length.ToString());
            EditorGUILayout.LabelField("Metrics", profile.MetricDefinitions.Length.ToString());
            EditorGUILayout.LabelField("Targets", profile.ReferenceTargets.Length.ToString());

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < profile.Fixtures.Length; index++)
            {
                VehicleCalibrationFixture fixture = profile.Fixtures[index];
                if (fixture == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(fixture.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ID", fixture.FixtureId);
                EditorGUILayout.LabelField("Kind / status", $"{fixture.Kind} / {fixture.Status}");
                EditorGUILayout.LabelField(
                    "Trials / duration",
                    $"{fixture.TrialCount} / {fixture.DurationSeconds:0.###} s");
                EditorGUILayout.LabelField("Route", fixture.Environment.SceneOrRouteId);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
