using MSC.Lighting.Production;
using UnityEditor;
using UnityEngine;

namespace MSC.Lighting.Editor
{
    public sealed class LightingValidationWindow : EditorWindow
    {
        private double hour = 22d;
        private string weatherId = "weather.clear";
        private LightingQualityTier quality = LightingQualityTier.High;
        private string caseId = "home-night";
        private bool captureScreenshot = true;
        private Vector3 teleportPosition;
        private Vector3 teleportEuler;
        private string circuitId = "grid.home.kitchen";
        private string switchId = "switch.home.kitchen";
        private bool electricalState = true;
        private string businessId = "service.teimo.shop";
        private bool businessPresent = true;

        [MenuItem("Tools/Lighting/Lighting Validation Runner")]
        public static void Open()
        {
            GetWindow<LightingValidationWindow>(
                "Lighting Validation Runner");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode in Bootstrap. This runner uses the authoritative game clock/weather dev API, electrical grid and central quality budgets; it does not write Enviro directly.",
                MessageType.Info);
            hour = EditorGUILayout.DoubleField("Hour [0..24)", hour);
            weatherId = EditorGUILayout.TextField("Weather ID", weatherId);
            quality = (LightingQualityTier)EditorGUILayout.EnumPopup(
                "Quality",
                quality);
            caseId = EditorGUILayout.TextField("Case ID", caseId);
            captureScreenshot = EditorGUILayout.Toggle(
                "Capture screenshot",
                captureScreenshot);
            teleportPosition = EditorGUILayout.Vector3Field(
                "Teleport position",
                teleportPosition);
            teleportEuler = EditorGUILayout.Vector3Field(
                "Teleport rotation",
                teleportEuler);
            circuitId = EditorGUILayout.TextField("Circuit ID", circuitId);
            switchId = EditorGUILayout.TextField("Switch ID", switchId);
            electricalState = EditorGUILayout.Toggle(
                "Electrical state",
                electricalState);
            businessId = EditorGUILayout.TextField("Business ID", businessId);
            businessPresent = EditorGUILayout.Toggle(
                "Owner present + open",
                businessPresent);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Apply Environment + Quality"))
                {
                    LightingValidationRunner runner = FindRunner();
                    if (!runner.TrySetEnvironment(
                            hour,
                            weatherId,
                            out string failure))
                    {
                        Debug.LogError(failure);
                    }
                    else
                    {
                        runner.SetQuality(quality);
                    }
                }

                if (GUILayout.Button("Capture Evidence"))
                {
                    string path = FindRunner().CaptureEvidence(
                        caseId,
                        captureScreenshot);
                    Debug.Log("[Lighting] Validation evidence: " + path);
                }

                if (GUILayout.Button("Teleport Player Camera"))
                {
                    FindRunner().TeleportPlayer(
                        teleportPosition,
                        teleportEuler);
                }

                if (GUILayout.Button("Apply Circuit + Switch"))
                {
                    LightingValidationRunner runner = FindRunner();
                    runner.SetCircuit(circuitId, electricalState);
                    runner.SetSwitch(switchId, electricalState);
                }

                if (GUILayout.Button("Apply Business Presence"))
                {
                    FindRunner().SetBusinessPresence(
                        businessId,
                        businessPresent);
                }

                if (GUILayout.Button("Use Real NPC Presence"))
                {
                    FindRunner().ClearBusinessPresenceOverrides();
                }
            }
        }

        private static LightingValidationRunner FindRunner()
        {
            LightingValidationRunner runner =
                Object.FindFirstObjectByType<LightingValidationRunner>();
            if (runner == null || !runner.IsReady)
            {
                throw new System.InvalidOperationException(
                    "Lighting validation runner is not ready in Play Mode.");
            }

            return runner;
        }
    }
}
