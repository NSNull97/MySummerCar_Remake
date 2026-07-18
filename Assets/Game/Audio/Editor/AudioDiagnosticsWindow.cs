using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.Audio.Editor
{
    public sealed class AudioDiagnosticsWindow : EditorWindow
    {
        private readonly List<IAudioEmitter> emitterSnapshot = new List<IAudioEmitter>();
        private AudioBackendRouter router;
        private AudioEventMap eventMap;
        private AudioParameterMap parameterMap;
        private AudioEmitterAuthoring auditionEmitter;
        private AudioValidationReport validationReport;
        private string auditionEventId = "audio.event.interaction.pickup";
        private Vector2 scroll;

        [MenuItem("Tools/MSC Remake/Audio/Diagnostics")]
        private static void Open()
        {
            GetWindow<AudioDiagnosticsWindow>("MSC Audio");
        }

        private void OnEnable()
        {
            FindRuntimeRouter();
            FindDefaultMaps();
        }

        private void OnGUI()
        {
            using (var view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                DrawConfiguration();
                EditorGUILayout.Space();
                DrawRuntimeSnapshot();
                EditorGUILayout.Space();
                DrawValidation();
                EditorGUILayout.Space();
                DrawAudition();
                EditorGUILayout.Space();
                DrawVehicleParameters();
                EditorGUILayout.Space();
                DrawWorldAuthoring();
            }
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            router = (AudioBackendRouter)EditorGUILayout.ObjectField(
                "Backend Router",
                router,
                typeof(AudioBackendRouter),
                true);
            eventMap = (AudioEventMap)EditorGUILayout.ObjectField(
                "Event Map",
                eventMap,
                typeof(AudioEventMap),
                false);
            parameterMap = (AudioParameterMap)EditorGUILayout.ObjectField(
                "Parameter Map",
                parameterMap,
                typeof(AudioParameterMap),
                false);
            if (GUILayout.Button("Refresh scene and assets"))
            {
                FindRuntimeRouter();
                FindDefaultMaps();
                Repaint();
            }
        }

        private void DrawRuntimeSnapshot()
        {
            EditorGUILayout.LabelField("Active runtime", EditorStyles.boldLabel);
            if (router == null)
            {
                EditorGUILayout.HelpBox(
                    "No AudioBackendRouter is active in the loaded scenes.",
                    MessageType.Warning);
                return;
            }

            AudioRuntimeSnapshot snapshot = router.CaptureSnapshot();
            EditorGUILayout.LabelField("Backend", snapshot.BackendId);
            EditorGUILayout.LabelField("Kind", snapshot.Kind.ToString());
            EditorGUILayout.LabelField("Ready", snapshot.IsReady.ToString());
            EditorGUILayout.LabelField("Fallback active", snapshot.IsFallback.ToString());
            EditorGUILayout.LabelField("Emitters", snapshot.RegisteredEmitterCount.ToString());
            EditorGUILayout.LabelField("Active voices", snapshot.ActiveVoiceCount.ToString());
            EditorGUILayout.LabelField("Loaded banks", snapshot.LoadedBankCount.ToString());
            EditorGUILayout.LabelField(
                "Listener",
                snapshot.Listener.IsValid
                    ? snapshot.Listener.StableListenerId + " / " +
                      snapshot.Listener.Environment.ListenerSpace
                    : "not bound");
            if (!string.IsNullOrWhiteSpace(snapshot.LastFailure))
            {
                EditorGUILayout.HelpBox(snapshot.LastFailure, MessageType.Warning);
            }

            if (snapshot.MissingBanks.Length > 0)
            {
                EditorGUILayout.LabelField("Missing SoundBanks", EditorStyles.boldLabel);
                for (int index = 0; index < snapshot.MissingBanks.Length; index++)
                {
                    EditorGUILayout.LabelField("- " + snapshot.MissingBanks[index]);
                }
            }

            router.CopyRegisteredEmitters(emitterSnapshot);
            if (emitterSnapshot.Count > 0)
            {
                EditorGUILayout.LabelField("Registered emitters", EditorStyles.boldLabel);
                for (int index = 0; index < emitterSnapshot.Count; index++)
                {
                    IAudioEmitter emitter = emitterSnapshot[index];
                    EditorGUILayout.LabelField(
                        emitter == null
                            ? "- destroyed"
                            : $"- {emitter.StableId} / scene {emitter.OwningSceneHandle} / " +
                              $"{(emitter.IsAudioEmitterActive ? "active" : "inactive")}");
                }
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(eventMap == null || parameterMap == null))
            {
                if (GUILayout.Button("Validate IDs, backend and SoundBanks"))
                {
                    validationReport = AudioValidationService.Validate(
                        eventMap,
                        parameterMap,
                        router);
                }
            }

            if (validationReport == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                "Result",
                validationReport.Passed
                    ? $"PASS ({validationReport.WarningCount} warnings)"
                    : $"FAIL ({validationReport.ErrorCount} errors, " +
                      $"{validationReport.WarningCount} warnings)");
            for (int index = 0; index < validationReport.Issues.Count; index++)
            {
                AudioValidationIssue issue = validationReport.Issues[index];
                EditorGUILayout.HelpBox(
                    issue.Code + ": " + issue.Message,
                    MapMessageType(issue.Severity));
            }

            if (GUILayout.Button("Export validation report"))
            {
                ExportValidationReport(validationReport);
            }
        }

        private void DrawAudition()
        {
            EditorGUILayout.LabelField("Mapped event audition", EditorStyles.boldLabel);
            auditionEmitter = (AudioEmitterAuthoring)EditorGUILayout.ObjectField(
                "Emitter",
                auditionEmitter,
                typeof(AudioEmitterAuthoring),
                true);
            auditionEventId = EditorGUILayout.TextField("AudioEventId", auditionEventId);
            using (new EditorGUI.DisabledScope(router == null || !router.IsReady))
            {
                if (GUILayout.Button("Audition event"))
                {
                    if (!AudioIdValidation.TryValidate(auditionEventId, out string failure))
                    {
                        Debug.LogError("Cannot audition invalid AudioEventId: " + failure);
                    }
                    else
                    {
                        var request = new AudioEventRequest(
                            new AudioEventId(auditionEventId),
                            auditionEmitter);
                        IAudioEventHandle handle = router.PostEvent(in request);
                        if (!handle.IsValid)
                        {
                            Debug.LogWarning(
                                "The active backend rejected or could not resolve " +
                                auditionEventId + ".");
                        }
                    }
                }
            }
        }

        private static void DrawVehicleParameters()
        {
            EditorGUILayout.LabelField("Vehicle parameters", EditorStyles.boldLabel);
            VehicleAudioEmitterBackend[] vehicles =
                FindObjectsByType<VehicleAudioEmitterBackend>(FindObjectsSortMode.None);
            if (vehicles.Length == 0)
            {
                EditorGUILayout.LabelField("No VehicleAudioEmitterBackend in loaded scenes.");
                return;
            }

            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleAudioParameters value = vehicles[index].LastVehicleParameters;
                EditorGUILayout.LabelField(
                    vehicles[index].name,
                    $"{value.EngineState}, {value.EngineRpm:0} rpm, " +
                    $"gear {value.SelectedGear}, {value.VehicleSpeedMetersPerSecond:0.0} m/s, " +
                    $"slip {value.WheelSlip01:0.00}");
            }
        }

        private static void DrawWorldAuthoring()
        {
            EditorGUILayout.LabelField("Zones and portals", EditorStyles.boldLabel);
            AudioEnvironmentZone[] zones =
                FindObjectsByType<AudioEnvironmentZone>(FindObjectsSortMode.None);
            AudioPortalAuthoring[] portals =
                FindObjectsByType<AudioPortalAuthoring>(FindObjectsSortMode.None);
            EditorGUILayout.LabelField("Loaded zones", zones.Length.ToString());
            for (int index = 0; index < zones.Length; index++)
            {
                EditorGUILayout.LabelField(
                    $"- {zones[index].StableZoneId} / priority {zones[index].Priority} / " +
                    $"{(zones[index].IsUsable ? "valid" : "invalid/inactive")}");
            }

            EditorGUILayout.LabelField("Loaded portals", portals.Length.ToString());
            for (int index = 0; index < portals.Length; index++)
            {
                EditorGUILayout.LabelField(
                    $"- {portals[index].StablePortalId} / openness " +
                    $"{portals[index].Openness01:0.00}");
            }
        }

        private void FindRuntimeRouter()
        {
            router = FindFirstObjectByType<AudioBackendRouter>(FindObjectsInactive.Include);
        }

        private void FindDefaultMaps()
        {
            if (eventMap == null)
            {
                eventMap = LoadFirstAsset<AudioEventMap>();
            }

            if (parameterMap == null)
            {
                parameterMap = LoadFirstAsset<AudioParameterMap>();
            }
        }

        private static T LoadFirstAsset<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static MessageType MapMessageType(AudioValidationSeverity severity)
        {
            switch (severity)
            {
                case AudioValidationSeverity.Error:
                    return MessageType.Error;
                case AudioValidationSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }

        private static void ExportValidationReport(AudioValidationReport report)
        {
            string path = EditorUtility.SaveFilePanel(
                "Export MSC audio validation report",
                Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")),
                "MSC_AudioValidationReport.txt",
                "txt");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("MSC Remake Audio Validation");
            builder.AppendLine("GeneratedUtc=" + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("Passed=" + report.Passed);
            builder.AppendLine("Errors=" + report.ErrorCount);
            builder.AppendLine("Warnings=" + report.WarningCount);
            for (int index = 0; index < report.Issues.Count; index++)
            {
                AudioValidationIssue issue = report.Issues[index];
                builder.AppendLine($"{issue.Severity}|{issue.Code}|{issue.Message}");
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }
    }
}
