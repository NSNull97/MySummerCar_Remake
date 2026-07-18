using System;
using System.Linq;
using MSC.Development.WeatherLab;
using MSC.Weather.Presentation;
using UnityEditor;
using UnityEngine;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public sealed class Enviro3PreflightWindow : EditorWindow
    {
        private Vector2 scroll;
        private Enviro3PreflightValidator.Result lastResult;
        private string lastMessage = "Проверка ещё не запускалась.";

        [MenuItem(WeatherLabBuilder.MenuRoot + "Open Dashboard", priority = -100)]
        public static void Open()
        {
            GetWindow<Enviro3PreflightWindow>("Enviro 3 Preflight");
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Show Local Version Evidence")]
        public static void ShowVersionEvidence()
        {
            Debug.Log(
                "M07A_ENVIRO_VERSION_EVIDENCE " +
                "version.txt-first-line='Enviro 3.0.0' changelog-through='v3.0.8' " +
                "exact-version-confidence=low unity=6000.3.11f1 hdrp=17.3.0");
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "List Project Assemblies Referencing Enviro")]
        public static void ListAssemblies()
        {
            Enviro3PreflightValidator.Result result = Enviro3PreflightValidator.Validate();
            Debug.Log(
                "M07A_ENVIRO_REFERENCING_ASSEMBLIES\n" +
                string.Join("\n", result.enviroReferencingAssemblies));
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Scan Duplicate Environment Owners")]
        public static void ScanOwners()
        {
            Enviro3PreflightValidator.ValidateOrThrow();
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Verify WeatherLab Excluded From Build")]
        public static void VerifyExcluded()
        {
            WeatherLabBuilder.AssertExcludedFromBuildSettings();
            Debug.Log("M07A_WEATHERLAB_BUILD_EXCLUSION_OK");
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Show Vendor File Changes")]
        public static void ShowVendorChanges()
        {
            Enviro3PreflightValidator.VendorSnapshot snapshot =
                Enviro3PreflightValidator.ComputeVendorSnapshot();
            bool unchanged = snapshot.FileCount == Enviro3PreflightValidator.BaselineFileCount &&
                             snapshot.TotalBytes == Enviro3PreflightValidator.BaselineTotalBytes &&
                             string.Equals(
                                 snapshot.Fingerprint,
                                 Enviro3PreflightValidator.BaselineFingerprint,
                                 StringComparison.Ordinal);
            Debug.Log(
                "M07A_ENVIRO_VENDOR_CHANGES " +
                $"unchanged={unchanged} files={snapshot.FileCount} bytes={snapshot.TotalBytes} " +
                $"fingerprint={snapshot.Fingerprint}");
        }

        [MenuItem(WeatherLabBuilder.MenuRoot + "Export Diagnostics")]
        public static void ExportDiagnostics()
        {
            Enviro3PreflightValidator.ExportDiagnostics();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Enviro 3 / Milestone 07A", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enviro — только presentation backend. Время, выбор состояния и gameplay остаются проектными. " +
                "WeatherLab не входит в Build Settings.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Локальная версия", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("version.txt: первая строка Enviro 3.0.0");
            EditorGUILayout.LabelField("changelog: записи до v3.0.8; точная версия — низкая уверенность");
            EditorGUILayout.LabelField("Unity 6000.3.11f1 / HDRP 17.3.0 / URP 17.3.0 compatibility-only");

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build/Rebuild WeatherLab"))
            {
                RunSafely(WeatherLabBuilder.Build);
            }

            if (GUILayout.Button("Open WeatherLab"))
            {
                RunSafely(WeatherLabBuilder.OpenWeatherLab);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate all"))
            {
                RunValidation();
            }

            if (GUILayout.Button("Export diagnostics"))
            {
                RunSafely(() => Enviro3PreflightValidator.ExportDiagnostics());
            }

            if (GUILayout.Button("Vendor diff"))
            {
                RunSafely(ShowVendorChanges);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(lastMessage, ResolveMessageType());
            DrawValidationResult();
            DrawSmokeControls();
            EditorGUILayout.EndScrollView();
        }

        private void DrawValidationResult()
        {
            if (lastResult == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capability / boundary matrix", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Vendor fingerprint",
                lastResult.vendorFingerprint ?? string.Empty);
            EditorGUILayout.LabelField(
                "Vendor files / bytes",
                $"{lastResult.vendorFileCount} / {lastResult.vendorTotalBytes}");
            EditorGUILayout.LabelField(
                "Enviro referencing assemblies",
                string.Join(", ", lastResult.enviroReferencingAssemblies ?? Array.Empty<string>()));

            foreach (string warning in lastResult.warnings ?? Array.Empty<string>())
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            foreach (string error in lastResult.errors ?? Array.Empty<string>())
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void DrawSmokeControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Explicit WeatherLab smoke states", EditorStyles.boldLabel);
            WeatherLabStateController controller = FindController();
            if (!EditorApplication.isPlaying || controller == null)
            {
                EditorGUILayout.HelpBox(
                    "Откройте WeatherLab и войдите в Play Mode. Состояния не запускают автономный scheduler.",
                    MessageType.Info);
                return;
            }

            EnvironmentPresentationStatus status = controller.Status;
            EditorGUILayout.LabelField("State", status.State.ToString());
            EditorGUILayout.LabelField("Capabilities", status.Capabilities.ToString());
            EditorGUILayout.LabelField("Last revision", status.LastAppliedRevision.ToString());

            EditorGUILayout.BeginHorizontal();
            DrawPresetButton(controller, "Clear", EnvironmentPresentationPresetKind.Clear);
            DrawPresetButton(controller, "Overcast", EnvironmentPresentationPresetKind.Overcast);
            DrawPresetButton(controller, "Rain", EnvironmentPresentationPresetKind.Rain);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawPresetButton(controller, "Storm", EnvironmentPresentationPresetKind.Storm);
            DrawPresetButton(controller, "Night", EnvironmentPresentationPresetKind.Night);
            DrawPresetButton(controller, "Fog/Mist", EnvironmentPresentationPresetKind.Mist);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Quality Low"))
            {
                LogStatus(controller.ApplyQuality(EnvironmentQualityTier.Low));
            }
            if (GUILayout.Button("Quality Medium"))
            {
                LogStatus(controller.ApplyQuality(EnvironmentQualityTier.Medium));
            }
            if (GUILayout.Button("Quality High"))
            {
                LogStatus(controller.ApplyQuality(EnvironmentQualityTier.High));
            }
            if (GUILayout.Button("Ambient Lightning"))
            {
                LogStatus(controller.TriggerAmbientLightning());
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Exterior Camera")) controller.SwitchCamera(0);
            if (GUILayout.Button("Interior Camera")) controller.SwitchCamera(1);
            if (GUILayout.Button("Vehicle Camera")) controller.SwitchCamera(2);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPresetButton(
            WeatherLabStateController controller,
            string label,
            EnvironmentPresentationPresetKind preset)
        {
            if (GUILayout.Button(label))
            {
                LogStatus(controller.ApplyPreset(preset));
            }
        }

        private static WeatherLabStateController FindController()
        {
            return FindObjectsByType<WeatherLabStateController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault();
        }

        private static void LogStatus(EnvironmentPresentationStatus status)
        {
            Debug.Log(
                "M07A_WEATHERLAB_COMMAND " +
                $"state={status.State} revision={status.LastAppliedRevision} " +
                $"warnings={status.WarningCount} errors={status.ErrorCount}");
        }

        private void RunValidation()
        {
            try
            {
                lastResult = Enviro3PreflightValidator.Validate();
                lastMessage = lastResult.passed
                    ? "PASS: compile-time boundary, vendor fingerprint, owners and shipping exclusion are valid."
                    : "FAIL: " + string.Join(" | ", lastResult.errors);
            }
            catch (Exception exception)
            {
                lastMessage = "FAIL: " + exception.Message;
            }
        }

        private void RunSafely(Action action)
        {
            try
            {
                action();
                lastMessage = "Команда завершена. Подробности — в Console.";
            }
            catch (Exception exception)
            {
                lastMessage = "FAIL: " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private MessageType ResolveMessageType()
        {
            if (lastMessage.StartsWith("FAIL", StringComparison.Ordinal))
            {
                return MessageType.Error;
            }

            return lastResult != null && lastResult.passed
                ? MessageType.Info
                : MessageType.None;
        }
    }
}
