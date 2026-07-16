using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    public sealed class VehicleSimulationPowertrainWindow : EditorWindow
    {
        private bool comparisonMode;
        private Vector2 scroll;

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Open Powertrain Graph")]
        public static void Open()
        {
            OpenInternal(comparison: false);
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Compare Reference/Tuned Curves")]
        public static void CompareReferenceAndTunedCurves()
        {
            OpenInternal(comparison: true);
        }

        private static void OpenInternal(bool comparison)
        {
            VehicleSimulationPowertrainWindow window =
                GetWindow<VehicleSimulationPowertrainWindow>("M06 Powertrain");
            window.comparisonMode = comparison;
            window.Show();
        }

        private void OnGUI()
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "Build the M06 prototype before inspecting its powertrain graph.",
                    MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Explicit powertrain graph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Engine -> Clutch -> Gearbox -> Final drive / open differential -> rear wheels");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", config.ConfigurationId);
            EditorGUILayout.LabelField("Tuning label", config.DynamicTuning.Label);
            EditorGUILayout.LabelField("Classification", config.DynamicTuning.Classification.ToString());
            EditorGUILayout.LabelField("Substeps", config.SubstepCount.ToString());
            EditorGUILayout.LabelField("Final drive", config.Gearbox.FinalDriveRatio.ToString("0.###"));
            EditorGUILayout.LabelField("Reverse", config.Gearbox.ReverseRatio.ToString("0.###"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Forward ratios", EditorStyles.boldLabel);
            float[] ratios = config.Gearbox.ForwardRatios;
            for (int index = 0; index < ratios.Length; index++)
            {
                EditorGUILayout.LabelField($"Gear {index + 1}", ratios[index].ToString("0.###"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Project-tuned torque curve", EditorStyles.boldLabel);
            VehicleTorqueSample[] samples = config.Engine.TorqueCurve;
            for (int index = 0; index < samples.Length; index++)
            {
                EditorGUILayout.LabelField(
                    $"{samples[index].Rpm:0} rpm",
                    $"{samples[index].TorqueNewtonMeters:0.###} N*m");
            }

            if (comparisonMode)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "A measured donor dynamic torque curve is not present in the reviewed 04B.4 fixtures. " +
                    "The window therefore shows only ProvisionalProjectTuning and does not fabricate a reference curve.",
                    MessageType.Info);
            }

            if (GUILayout.Button("Ping config asset"))
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
