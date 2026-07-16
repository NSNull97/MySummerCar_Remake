using System;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VehicleSimulation
{
    public sealed class VehicleSimulationToolsWindow : EditorWindow
    {
        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Dashboard")]
        public static void Open()
        {
            GetWindow<VehicleSimulationToolsWindow>("M06 Vehicle Simulation");
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Start/Stop Telemetry Capture")]
        public static void StartOrStopTelemetryCapture()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Telemetry capture requires Play Mode in VehicleSimulationPrototype.");
            }

            VehicleTelemetryRecorder recorder = FindSingleInActiveScene<VehicleTelemetryRecorder>();
            if (recorder == null)
            {
                throw new InvalidOperationException("The active scene has no M06 telemetry recorder.");
            }

            if (!recorder.IsCapturing)
            {
                if (!recorder.StartCapture())
                {
                    throw new InvalidOperationException("M06 telemetry recorder could not start.");
                }

                Debug.Log($"M06_TELEMETRY_CAPTURE_STARTED capacity={recorder.Capacity}");
                return;
            }

            string path = recorder.StopCaptureAndExport();
            Debug.Log(
                $"M06_TELEMETRY_CAPTURE_STOPPED frames={recorder.Count} " +
                $"capacityReached={recorder.CapacityReached} path={path}");
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Show Missing Prerequisites")]
        public static void ShowMissingPrerequisites()
        {
            GetWindow<VehicleSimulationPrerequisitesWindow>("M06 Prerequisites");
        }

        private void OnGUI()
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            EditorGUILayout.LabelField("Milestone 06", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene", VehicleSimulationPrototypePaths.PrototypeScene);
            EditorGUILayout.LabelField(
                "Config",
                config != null ? config.ConfigurationId : "Missing");
            EditorGUILayout.LabelField(
                "Tuning",
                config != null ? config.DynamicTuning.Label : "Missing");
            EditorGUILayout.Space();

            if (GUILayout.Button("Build / rebuild prototype"))
            {
                VehicleSimulationPrototypeBuilder.Build();
            }

            if (GUILayout.Button("Validate configs and composition"))
            {
                VehicleSimulationPrototypeValidator.ValidateMenu();
            }

            if (GUILayout.Button("Open powertrain graph"))
            {
                VehicleSimulationPowertrainWindow.Open();
            }

            if (GUILayout.Button("Run calibration fixture"))
            {
                VehicleSimulationCalibrationRunner.Run();
            }

            if (GUILayout.Button("Spawn / reset prototype"))
            {
                VehicleSimulationPrototypeBuilder.SpawnOrResetPrototype();
            }

            if (GUILayout.Button("Start / stop telemetry capture"))
            {
                StartOrStopTelemetryCapture();
            }

            if (GUILayout.Button("Compare reference / tuned curves"))
            {
                VehicleSimulationPowertrainWindow.CompareReferenceAndTunedCurves();
            }

            if (GUILayout.Button("Show missing prerequisites"))
            {
                ShowMissingPrerequisites();
            }

            if (GUILayout.Button("Run performance audit"))
            {
                VehicleSimulationPerformanceAudit.Run();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Controls: I ignition, Enter starter, W/S throttle/brake, Left Shift clutch, " +
                "A/D steering, Q/E gears, Backspace reset.",
                MessageType.Info);
        }

        private static T FindSingleInActiveScene<T>()
            where T : Component
        {
            T[] values = VehicleSimulationEditorUtility.FindAllInScene<T>(
                SceneManager.GetActiveScene());
            return values.Length == 1 ? values[0] : null;
        }
    }

    public sealed class VehicleSimulationPrerequisitesWindow : EditorWindow
    {
        private Vector2 scroll;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            VehicleSimulationHost[] hosts =
                VehicleSimulationEditorUtility.FindAllInScene<VehicleSimulationHost>(
                    SceneManager.GetActiveScene());
            if (hosts.Length != 1)
            {
                EditorGUILayout.HelpBox(
                    "Open VehicleSimulationPrototype to inspect prerequisites.",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            VehicleSimulationHost host = hosts[0];
            if (!EditorApplication.isPlaying || host.Telemetry == null)
            {
                EditorGUILayout.HelpBox(
                    "Authored assembly/config wiring is available in Edit Mode, but actual prerequisite " +
                    "state is evaluated in Play Mode after the installed+tight fixture initializer runs.",
                    MessageType.Info);
                string[] errors = VehicleSimulationPrototypeValidator.Validate().ToArray();
                EditorGUILayout.LabelField("Static validation", errors.Length == 0 ? "PASS" : "FAIL");
                for (int index = 0; index < errors.Length; index++)
                {
                    EditorGUILayout.LabelField("- " + errors[index], EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.EndScrollView();
                return;
            }

            VehicleSimulationPrerequisiteFailure failures =
                host.Telemetry.PrerequisiteFailures;
            EditorGUILayout.LabelField(
                "Current prerequisite result",
                failures == VehicleSimulationPrerequisiteFailure.None ? "READY" : failures.ToString());
            foreach (VehicleSimulationPrerequisiteFailure value in
                     Enum.GetValues(typeof(VehicleSimulationPrerequisiteFailure)))
            {
                if (value == VehicleSimulationPrerequisiteFailure.None)
                {
                    continue;
                }

                bool missing = (failures & value) != 0;
                EditorGUILayout.LabelField(value.ToString(), missing ? "MISSING/BLOCKED" : "OK");
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
