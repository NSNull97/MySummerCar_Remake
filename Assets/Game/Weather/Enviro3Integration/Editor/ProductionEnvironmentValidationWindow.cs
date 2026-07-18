using System;
using System.IO;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Enviro3Integration.Editor
{
    /// <summary>
    /// Editor-only production Bootstrap validation console. It talks solely to
    /// the project-owned production owner; vendor APIs never escape the adapter.
    /// </summary>
    public sealed class ProductionEnvironmentValidationWindow : EditorWindow
    {
        private const string DiagnosticsPath =
            "Logs/M07C_ProductionWeatherDiagnostics.json";
        private const string CaptureDirectory =
            "References/Weather/Milestone07C/ProductionCaptures";

        private static readonly string[] LogicalWeatherIds =
            CreateLogicalWeatherIds();

        private int controllerInstanceId;
        private int year = 1995;
        private int month = 8;
        private int day = 1;
        private int hour = 12;
        private int minute;
        private double timeScale = 1d;
        private double advanceGameSeconds = 3600d;
        private int logicalWeatherIndex;
        private float transitionSeconds = 30f;
        private ulong weatherSeed = 19950801UL;
        private float groundWetness;
        private float roadWetness;
        private float puddleAmount;
        private float vegetationWetness;
        private bool drawShelterOverlay;
        private Vector2 scroll;
        private string lastAction = "Ready.";

        [MenuItem(
            "Tools/MSC Remake/Production Weather/Runtime Validation")]
        public static void Open()
        {
            GetWindow<ProductionEnvironmentValidationWindow>(
                "Production Weather");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -=
                OnPlayModeStateChanged;
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
                if (drawShelterOverlay)
                {
                    SceneView.RepaintAll();
                }
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Production Bootstrap validation only. Project time, weather, " +
                "wetness and lightning remain authoritative; Enviro is " +
                "presentation-only.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                DrawEditModeValidation();
                EditorGUILayout.EndScrollView();
                return;
            }

            ProductionEnvironmentController controller =
                ProductionEnvironmentController.ActiveOwner;
            if (controller == null || !controller.IsPrimaryOwner)
            {
                EditorGUILayout.HelpBox(
                    "No active primary ProductionEnvironmentController. " +
                    "Enter Play Mode from Assets/Game/Bootstrap/Bootstrap.unity.",
                    MessageType.Error);
                DrawOwnerAndSceneScan(null);
                EditorGUILayout.EndScrollView();
                return;
            }

            SyncEditableFields(controller, force: false);
            DrawAuthoritativeAndPresentedState(controller);
            DrawTime(controller);
            DrawWeather(controller);
            DrawWetness(controller);
            DrawQualityAndLightning(controller);
            DrawOwnerAndSceneScan(controller);
            DrawEvidence(controller);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Last action",
                lastAction,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEditModeValidation()
        {
            EditorGUILayout.HelpBox(
                "Open the production Bootstrap and enter Play Mode to use " +
                "runtime controls. The authored content audit can be run now.",
                MessageType.Warning);
            if (GUILayout.Button("Validate authored Bootstrap environment"))
            {
                Run(
                    ProductionEnvironmentValidator.ValidateOrThrow,
                    "Authored Bootstrap environment validation passed.");
            }
        }

        private void DrawAuthoritativeAndPresentedState(
            ProductionEnvironmentController controller)
        {
            EditorGUILayout.LabelField(
                "Authoritative state",
                EditorStyles.boldLabel);
            GameTimeSnapshot clock = controller.AuthoritativeGameTime.Snapshot;
            WeatherState weather = controller.AuthoritativeWeather.CurrentState;
            WetnessEnvironmentOutputs wetness =
                controller.AuthoritativeWetness.Outputs;
            EditorGUILayout.LabelField("Date", clock.Date.ToString());
            EditorGUILayout.LabelField("Time", FormatTime(clock.SecondsOfDay));
            EditorGUILayout.LabelField(
                "Paused / scale",
                $"{clock.IsPaused} / {clock.TimeScale:F2}");
            EditorGUILayout.LabelField(
                "Logical weather",
                weather.Id.Value);
            EditorGUILayout.LabelField(
                "Binding ID",
                weather.PresentationBindingId ?? string.Empty);
            EditorGUILayout.LabelField(
                "Schedule cursor / frozen",
                $"{controller.DevTimeline.CompletedFrontCount} / " +
                controller.DevIsScheduleFrozen);
            EditorGUILayout.LabelField(
                "Manual override",
                controller.DevManualWeatherOverrideActive.ToString());
            EditorGUILayout.LabelField(
                "Wetness G/R/P/V",
                $"{wetness.GroundWetness01:F2} / " +
                $"{wetness.RoadWetness01:F2} / " +
                $"{wetness.PuddleAmount01:F2} / " +
                $"{wetness.VegetationWetness01:F2}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Presented state",
                EditorStyles.boldLabel);
            EnvironmentPresentationStatus status =
                controller.PresentationStatus;
            WeatherEnvironmentOutputs outputs = controller.CurrentOutputs;
            EditorGUILayout.LabelField("Adapter state", status.State.ToString());
            EditorGUILayout.LabelField(
                "Capabilities",
                status.Capabilities.ToString(),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Revision / warnings / errors",
                $"{status.LastAppliedRevision} / " +
                $"{status.WarningCount} / {status.ErrorCount}");
            EditorGUILayout.LabelField(
                "Presented weather",
                outputs.IsValid
                    ? outputs.Weather.Id.Value
                    : "not synchronized");
            EditorGUILayout.LabelField(
                "Quality / exposure",
                $"{ProductionEnvironmentQuality.GetStableId(controller.QualityTier)} / " +
                controller.ExposureContext);
            if (!string.IsNullOrEmpty(controller.LastFailure))
            {
                EditorGUILayout.HelpBox(
                    controller.LastFailure,
                    MessageType.Error);
            }
        }

        private void DrawTime(ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Time/date", EditorStyles.boldLabel);
            year = EditorGUILayout.IntField("Year", year);
            month = EditorGUILayout.IntField("Month", month);
            day = EditorGUILayout.IntField("Day", day);
            hour = EditorGUILayout.IntSlider("Hour", hour, 0, 23);
            minute = EditorGUILayout.IntSlider("Minute", minute, 0, 59);
            if (GUILayout.Button("Set production date and time"))
            {
                Run(() =>
                {
                    if (!GameDate.TryCreate(
                            year,
                            month,
                            day,
                            out GameDate date))
                    {
                        throw new InvalidOperationException(
                            "Invalid Gregorian date.");
                    }

                    if (!controller.DevTrySetDateAndTime(
                            date,
                            hour * 3600d + minute * 60d,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Production date/time applied.");
            }

            timeScale = EditorGUILayout.DoubleField(
                "Time scale",
                timeScale);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply time scale"))
            {
                Run(() =>
                {
                    if (!controller.DevTrySetTimeScale(
                            timeScale,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Time scale applied.");
            }

            if (GUILayout.Button(
                    controller.AuthoritativeGameTime.Snapshot.IsPaused
                        ? "Unpause"
                        : "Pause"))
            {
                Run(
                    () => controller.DevSetPaused(
                        !controller.AuthoritativeGameTime.Snapshot.IsPaused),
                    "Pause state toggled.");
            }
            EditorGUILayout.EndHorizontal();

            advanceGameSeconds = EditorGUILayout.DoubleField(
                "Advance game seconds",
                advanceGameSeconds);
            if (GUILayout.Button("Advance all authoritative domains"))
            {
                Run(() =>
                {
                    if (!controller.DevTryAdvanceGameSeconds(
                            advanceGameSeconds,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Time/weather/wetness/lightning advanced coherently.");
            }
        }

        private void DrawWeather(ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Logical weather/front",
                EditorStyles.boldLabel);
            logicalWeatherIndex = EditorGUILayout.Popup(
                "Known profile",
                logicalWeatherIndex,
                LogicalWeatherIds);
            transitionSeconds = EditorGUILayout.FloatField(
                "Transition seconds",
                transitionSeconds);
            if (GUILayout.Button("Force transient logical profile"))
            {
                Run(() =>
                {
                    if (!controller.DevTryApplyWeatherOverride(
                            LogicalWeatherIds[logicalWeatherIndex],
                            transitionSeconds,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Transient production weather override applied.");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove DEV override"))
            {
                Run(
                    () => controller.DevRemoveWeatherOverride(),
                    "DEV override removed; schedule is visible again.");
            }

            if (GUILayout.Button(
                    controller.DevIsScheduleFrozen
                        ? "Unfreeze schedule"
                        : "Freeze schedule"))
            {
                Run(
                    () => controller.DevSetScheduleFrozen(
                        !controller.DevIsScheduleFrozen),
                    "Schedule freeze toggled.");
            }
            EditorGUILayout.EndHorizontal();

            weatherSeed = DrawUlongField(
                "Weather schedule seed",
                weatherSeed);
            if (GUILayout.Button("Reset schedule with seed"))
            {
                Run(() =>
                {
                    if (!controller.DevTryResetWeatherSeed(
                            weatherSeed,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Deterministic weather schedule reset; visible state retained by DEV override.");
            }
        }

        private void DrawWetness(
            ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Wetness/puddles",
                EditorStyles.boldLabel);
            groundWetness = EditorGUILayout.Slider(
                "Ground",
                groundWetness,
                0f,
                1f);
            roadWetness = EditorGUILayout.Slider(
                "Road",
                roadWetness,
                0f,
                1f);
            puddleAmount = EditorGUILayout.Slider(
                "Puddles",
                puddleAmount,
                0f,
                1f);
            vegetationWetness = EditorGUILayout.Slider(
                "Vegetation",
                vegetationWetness,
                0f,
                1f);
            if (GUILayout.Button("Apply project-owned wetness override"))
            {
                Run(() =>
                {
                    if (!controller.DevTrySetWetness(
                            groundWetness,
                            roadWetness,
                            puddleAmount,
                            vegetationWetness,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Wetness/puddle state applied.");
            }
        }

        private void DrawQualityAndLightning(
            ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Quality/lightning",
                EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Low"))
            {
                Run(
                    () => controller.SetQuality(
                        EnvironmentQualityTier.Low),
                    "Low quality applied.");
            }

            if (GUILayout.Button("Medium"))
            {
                Run(
                    () => controller.SetQuality(
                        EnvironmentQualityTier.Medium),
                    "Medium quality applied.");
            }

            if (GUILayout.Button("High"))
            {
                Run(
                    () => controller.SetQuality(
                        EnvironmentQualityTier.High),
                    "High quality applied.");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Ambient lightning at listener"))
            {
                Run(() =>
                {
                    if (!controller.DevTryTriggerAmbientLightningAtListener(
                            out _,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Ambient presentation lightning requested at listener.");
            }
        }

        private void DrawOwnerAndSceneScan(
            ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Owner/lifecycle/shelter diagnostics",
                EditorStyles.boldLabel);
            ScanOwners(
                out int totalOwners,
                out int enabledOwners,
                out int primaryOwners);
            EditorGUILayout.LabelField(
                "Environment owners total/enabled/primary",
                $"{totalOwners} / {enabledOwners} / {primaryOwners}");
            EditorGUILayout.LabelField(
                "Loaded scenes",
                SceneManager.sceneCount.ToString());
            if (controller != null)
            {
                EditorGUILayout.LabelField(
                    "Domains/reveal/simulation",
                    $"{controller.AreDomainsInitialized} / " +
                    $"{controller.IsWorldRevealReady} / " +
                    controller.IsSimulationActive);
                EditorGUILayout.LabelField(
                    "Restore-before-reveal",
                    controller.WasRestoreAppliedBeforeReveal.ToString());
                EditorGUILayout.LabelField(
                    "Shelter volumes / listener exposure",
                    $"{controller.ActiveShelterVolumeCount} / " +
                    controller.ExposureContext);
            }

            drawShelterOverlay = EditorGUILayout.Toggle(
                "Scene shelter overlay",
                drawShelterOverlay);
            if (GUILayout.Button("Refresh editable fields from authority") &&
                controller != null)
            {
                SyncEditableFields(controller, force: true);
                lastAction = "Editable values refreshed from authority.";
            }
        }

        private void DrawEvidence(
            ProductionEnvironmentController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Binding/evidence",
                EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate/present now"))
            {
                Run(() =>
                {
                    EnvironmentPresentationStatus status =
                        controller.DevRefreshPresentation();
                    if (controller.IsWorldRevealReady &&
                        !status.IsOperational)
                    {
                        throw new InvalidOperationException(
                            "Production presentation is not operational: " +
                            status.State);
                    }
                }, "Authoritative state synchronized to presentation.");
            }

            if (GUILayout.Button("Export diagnostics"))
            {
                Run(
                    () => ExportDiagnostics(controller),
                    "Diagnostics exported to " + DiagnosticsPath + ".");
            }

            if (GUILayout.Button("Capture Game View"))
            {
                Run(
                    () => CaptureGameView(controller),
                    "Canonical production screenshot request queued.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SyncEditableFields(
            ProductionEnvironmentController controller,
            bool force)
        {
            int instanceId = controller.GetInstanceID();
            if (!force && controllerInstanceId == instanceId)
            {
                return;
            }

            controllerInstanceId = instanceId;
            GameTimeSnapshot time = controller.AuthoritativeGameTime.Snapshot;
            year = time.Date.Year;
            month = time.Date.Month;
            day = time.Date.Day;
            hour = Mathf.Clamp((int)(time.SecondsOfDay / 3600d), 0, 23);
            minute = Mathf.Clamp(
                (int)(time.SecondsOfDay % 3600d / 60d),
                0,
                59);
            timeScale = time.TimeScale;
            logicalWeatherIndex = FindLogicalWeatherIndex(
                controller.AuthoritativeWeather.CurrentState.Id.Value);
            weatherSeed = controller.DevWeatherSeed;
            WetnessState wetness =
                controller.AuthoritativeWetness.State;
            groundWetness = wetness.GroundWetness01;
            roadWetness = wetness.RoadWetness01;
            puddleAmount = wetness.PuddleAmount01;
            vegetationWetness = wetness.VegetationWetness01;
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!drawShelterOverlay || !EditorApplication.isPlaying)
            {
                return;
            }

            ProductionShelterVolumeAuthoring[] authored =
                UnityEngine.Object.FindObjectsByType<
                    ProductionShelterVolumeAuthoring>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            Color previousColor = Handles.color;
            for (int index = 0; index < authored.Length; index++)
            {
                ProductionShelterVolumeAuthoring authoring = authored[index];
                if (!authoring.TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure))
                {
                    Handles.color = Color.red;
                    Handles.Label(
                        authoring.transform.position,
                        failure);
                    continue;
                }

                Handles.color = authoring.ShelterKind ==
                                ProductionShelterKind.Interior
                    ? new Color(0.2f, 0.75f, 1f, 1f)
                    : new Color(1f, 0.75f, 0.15f, 1f);
                Handles.DrawWireCube(
                    volume.Center,
                    volume.Extents * 2f);
                Handles.Label(
                    volume.Center,
                    volume.StableId + " (" + authoring.ShelterKind + ")");
            }

            ProductionEnvironmentController controller =
                ProductionEnvironmentController.ActiveOwner;
            if (controller != null &&
                controller.DevTryGetListenerWorldPosition(
                    out Vector3 listenerPosition))
            {
                Handles.color = Color.white;
                Handles.SphereHandleCap(
                    0,
                    listenerPosition,
                    Quaternion.identity,
                    0.35f,
                    EventType.Repaint);
                Handles.Label(
                    listenerPosition,
                    "Weather listener: " + controller.ExposureContext);
            }

            Handles.color = previousColor;
        }

        private static void ExportDiagnostics(
            ProductionEnvironmentController controller)
        {
            Directory.CreateDirectory("Logs");
            ScanOwners(
                out int totalOwners,
                out int enabledOwners,
                out int primaryOwners);
            GameTimeSnapshot time = controller.AuthoritativeGameTime.Snapshot;
            WeatherState weather =
                controller.AuthoritativeWeather.CurrentState;
            WetnessState wetness = controller.AuthoritativeWetness.State;
            WeatherEnvironmentOutputs outputs = controller.CurrentOutputs;
            EnvironmentPresentationStatus status =
                controller.PresentationStatus;
            var dto = new DiagnosticsDto
            {
                schemaVersion = 1,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                date = time.Date.ToString(),
                secondsOfDay = time.SecondsOfDay,
                dayIndex = time.DayIndex,
                timeScale = time.TimeScale,
                paused = time.IsPaused,
                authoritativeWeatherStateId = weather.Id.Value,
                authoritativeBindingId = weather.PresentationBindingId,
                presentedWeatherStateId = outputs.IsValid
                    ? outputs.Weather.Id.Value
                    : string.Empty,
                timelineCursor =
                    controller.DevTimeline.CompletedFrontCount,
                scheduleFrozen = controller.DevIsScheduleFrozen,
                manualOverrideActive =
                    controller.DevManualWeatherOverrideActive,
                weatherSeed = controller.DevWeatherSeed.ToString(),
                groundWetness = wetness.GroundWetness01,
                roadWetness = wetness.RoadWetness01,
                puddles = wetness.PuddleAmount01,
                vegetationWetness = wetness.VegetationWetness01,
                qualityTierId =
                    ProductionEnvironmentQuality.GetStableId(
                        controller.QualityTier),
                exposure = controller.ExposureContext.ToString(),
                shelterVolumeCount =
                    controller.ActiveShelterVolumeCount,
                adapterState = status.State.ToString(),
                adapterCapabilities = status.Capabilities.ToString(),
                presentedRevision =
                    status.LastAppliedRevision.ToString(),
                adapterWarnings = status.WarningCount,
                adapterErrors = status.ErrorCount,
                totalOwners = totalOwners,
                enabledOwners = enabledOwners,
                primaryOwners = primaryOwners,
                domainsInitialized = controller.AreDomainsInitialized,
                revealReady = controller.IsWorldRevealReady,
                restoreAppliedBeforeReveal =
                    controller.WasRestoreAppliedBeforeReveal,
                simulationActive = controller.IsSimulationActive,
                loadedScenes = GetLoadedSceneNames(),
                lastFailure = controller.LastFailure,
            };
            File.WriteAllText(
                DiagnosticsPath,
                JsonUtility.ToJson(dto, true));
        }

        private static void CaptureGameView(
            ProductionEnvironmentController controller)
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "Game View capture requires Play Mode.");
            }

            string directory = Path.GetFullPath(CaptureDirectory);
            Directory.CreateDirectory(directory);
            GameTimeSnapshot time = controller.AuthoritativeGameTime.Snapshot;
            string weather = controller.AuthoritativeWeather.CurrentState.Id.Value
                .Replace('.', '_');
            string name =
                "M07C_Production_" +
                time.Date + "_" +
                ((int)(time.SecondsOfDay / 3600d)).ToString("D2") +
                ((int)(time.SecondsOfDay % 3600d / 60d)).ToString("D2") +
                "_" + weather + "_" +
                ProductionEnvironmentQuality.GetStableId(
                        controller.QualityTier)
                    .Replace('.', '_') + "_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(
                Path.Combine(directory, name));
        }

        private static void ScanOwners(
            out int total,
            out int enabled,
            out int primary)
        {
            ProductionEnvironmentController[] controllers =
                UnityEngine.Object.FindObjectsByType<
                    ProductionEnvironmentController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            total = controllers.Length;
            enabled = 0;
            primary = 0;
            for (int index = 0; index < controllers.Length; index++)
            {
                ProductionEnvironmentController controller =
                    controllers[index];
                if (controller.isActiveAndEnabled)
                {
                    enabled++;
                }

                if (controller.IsPrimaryOwner)
                {
                    primary++;
                }
            }
        }

        private static string[] GetLoadedSceneNames()
        {
            var values = new string[SceneManager.sceneCount];
            for (int index = 0; index < values.Length; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                values[index] = scene.name + "|" + scene.path;
            }

            return values;
        }

        private void Run(Action action, string success)
        {
            try
            {
                action();
                lastAction = success;
            }
            catch (Exception exception)
            {
                lastAction =
                    exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private static string FormatTime(double secondsOfDay)
        {
            int totalSeconds = Mathf.Clamp(
                (int)secondsOfDay,
                0,
                86399);
            return $"{totalSeconds / 3600:D2}:" +
                   $"{totalSeconds % 3600 / 60:D2}:" +
                   $"{totalSeconds % 60:D2}";
        }

        private static ulong DrawUlongField(
            string label,
            ulong value)
        {
            string text = EditorGUILayout.TextField(
                label,
                value.ToString());
            return ulong.TryParse(text, out ulong parsed)
                ? parsed
                : value;
        }

        private static int FindLogicalWeatherIndex(string stableId)
        {
            for (int index = 0;
                 index < LogicalWeatherIds.Length;
                 index++)
            {
                if (string.Equals(
                        LogicalWeatherIds[index],
                        stableId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return 0;
        }

        private static string[] CreateLogicalWeatherIds()
        {
            var values = new string[WeatherStateIds.All.Count];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = WeatherStateIds.All[index].Value;
            }

            return values;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            controllerInstanceId = 0;
            Repaint();
        }

        [Serializable]
        private sealed class DiagnosticsDto
        {
            public int schemaVersion;
            public string capturedUtc;
            public string date;
            public double secondsOfDay;
            public long dayIndex;
            public double timeScale;
            public bool paused;
            public string authoritativeWeatherStateId;
            public string authoritativeBindingId;
            public string presentedWeatherStateId;
            public long timelineCursor;
            public bool scheduleFrozen;
            public bool manualOverrideActive;
            public string weatherSeed;
            public float groundWetness;
            public float roadWetness;
            public float puddles;
            public float vegetationWetness;
            public string qualityTierId;
            public string exposure;
            public int shelterVolumeCount;
            public string adapterState;
            public string adapterCapabilities;
            public string presentedRevision;
            public int adapterWarnings;
            public int adapterErrors;
            public int totalOwners;
            public int enabledOwners;
            public int primaryOwners;
            public bool domainsInitialized;
            public bool revealReady;
            public bool restoreAppliedBeforeReveal;
            public bool simulationActive;
            public string[] loadedScenes;
            public string lastFailure;
        }
    }
}
