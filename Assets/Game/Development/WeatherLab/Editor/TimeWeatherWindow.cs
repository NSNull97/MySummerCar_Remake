using System;
using System.IO;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using UnityEditor;
using UnityEngine;

namespace MSC.Development.WeatherLab.Editor
{
    public sealed class TimeWeatherWindow : EditorWindow
    {
        private int year = 1995;
        private int month = 8;
        private int day = 1;
        private int hour = 12;
        private int minute;
        private double timeScale = 1d;
        private double advanceGameSeconds = 3600d;
        private string logicalWeatherId = "weather.clear";
        private float transitionSeconds = 120f;
        private ulong seed = 19950801UL;
        private float groundWetness;
        private float roadWetness;
        private float puddles;
        private float vegetationWetness;
        private WeatherExposureContext exposure = WeatherExposureContext.Exterior;
        private Vector2 scroll;
        private string lastAction = "Ready.";

        [MenuItem("Tools/MSC Remake/Time and Weather")]
        public static void Open()
        {
            GetWindow<TimeWeatherWindow>("Time and Weather");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Project-owned time/weather authority. Enviro is presentation-only. " +
                "Controls are active only while WeatherLab is in Play Mode.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode in WeatherLab before reading or changing runtime domain state.",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            WeatherLabStateController controller = FindController();

            if (controller == null)
            {
                EditorGUILayout.HelpBox(
                    "Open WeatherLab and enter Play Mode. No active WeatherLabStateController was found.",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawStatus(controller);
            DrawTime(controller);
            DrawWeather(controller);
            DrawWetness(controller);
            DrawLightning(controller);
            DrawPresentation(controller);
            DrawEvidence(controller);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last action", lastAction, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatus(WeatherLabStateController controller)
        {
            EditorGUILayout.LabelField("Authoritative state", EditorStyles.boldLabel);
            GameTimeSnapshot clock = controller.GameTime.Snapshot;
            WeatherState weather = controller.CurrentLogicalWeather;
            EditorGUILayout.LabelField("Date", clock.Date.ToString());
            EditorGUILayout.LabelField("Time", FormatTime(clock.SecondsOfDay));
            EditorGUILayout.LabelField("Day index", clock.DayIndex.ToString());
            EditorGUILayout.LabelField("Paused / scale", $"{clock.IsPaused} / {clock.TimeScale:F2}");
            EditorGUILayout.LabelField("Logical weather", weather.Id.Value);
            EditorGUILayout.LabelField("Presentation binding", weather.PresentationBindingId ?? string.Empty);
            EditorGUILayout.LabelField("Timeline cursor", controller.CurrentTimeline.CompletedFrontCount.ToString());
            EditorGUILayout.LabelField("Schedule frozen", controller.IsScheduleFrozen.ToString());
            EditorGUILayout.LabelField("Adapter", controller.Status.State.ToString());
            EditorGUILayout.LabelField("Presented revision", controller.Status.LastAppliedRevision.ToString());
            if (!string.IsNullOrEmpty(controller.LastMappingFailure))
            {
                EditorGUILayout.HelpBox(controller.LastMappingFailure, MessageType.Error);
            }
        }

        private void DrawTime(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Time/date", EditorStyles.boldLabel);
            year = EditorGUILayout.IntField("Year", year);
            month = EditorGUILayout.IntField("Month", month);
            day = EditorGUILayout.IntField("Day", day);
            hour = EditorGUILayout.IntSlider("Hour", hour, 0, 23);
            minute = EditorGUILayout.IntSlider("Minute", minute, 0, 59);
            if (GUILayout.Button("Set date and time"))
            {
                Run(() =>
                {
                    if (!GameDate.TryCreate(year, month, day, out GameDate date))
                    {
                        throw new InvalidOperationException("Invalid Gregorian date.");
                    }

                    if (!controller.TrySetDateAndTime(
                            date,
                            hour * 3600d + minute * 60d,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Date/time applied.");
            }

            timeScale = EditorGUILayout.DoubleField("Time scale", timeScale);
            if (GUILayout.Button("Apply time scale"))
            {
                Run(() => controller.SetTimeScale(timeScale), "Time scale applied.");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(controller.GameTime.Snapshot.IsPaused ? "Unpause" : "Pause"))
            {
                Run(
                    () => controller.SetPaused(!controller.GameTime.Snapshot.IsPaused),
                    "Pause state toggled.");
            }

            advanceGameSeconds = EditorGUILayout.DoubleField(advanceGameSeconds);
            if (GUILayout.Button("Advance game seconds"))
            {
                Run(() =>
                {
                    if (!controller.TryAdvanceGameSeconds(advanceGameSeconds, out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Game time advanced.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeather(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Weather/front", EditorStyles.boldLabel);
            logicalWeatherId = EditorGUILayout.TextField("Logical state ID", logicalWeatherId);
            transitionSeconds = EditorGUILayout.FloatField("Transition seconds", transitionSeconds);
            if (GUILayout.Button("Apply logical weather override"))
            {
                Run(() =>
                {
                    if (!controller.TryApplyLogicalWeather(
                            logicalWeatherId,
                            transitionSeconds,
                            out string failure))
                    {
                        throw new InvalidOperationException(failure);
                    }
                }, "Logical weather override applied.");
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove override"))
            {
                Run(
                    () => controller.RemoveManualWeatherOverride(),
                    "Manual override removed; underlying schedule exposed.");
            }

            if (GUILayout.Button(controller.IsScheduleFrozen ? "Unfreeze schedule" : "Freeze schedule"))
            {
                Run(
                    () => controller.SetScheduleFrozen(!controller.IsScheduleFrozen),
                    "Schedule freeze toggled.");
            }
            EditorGUILayout.EndHorizontal();

            seed = DrawUlongField("Seed", seed);
            if (GUILayout.Button("Reset deterministic schedule with seed"))
            {
                Run(() => controller.ResetWeatherSeed(seed), "Weather seed applied.");
            }
        }

        private void DrawWetness(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wetness/exposure", EditorStyles.boldLabel);
            groundWetness = EditorGUILayout.Slider("Ground", groundWetness, 0f, 1f);
            roadWetness = EditorGUILayout.Slider("Road", roadWetness, 0f, 1f);
            puddles = EditorGUILayout.Slider("Puddles", puddles, 0f, 1f);
            vegetationWetness = EditorGUILayout.Slider("Vegetation", vegetationWetness, 0f, 1f);
            if (GUILayout.Button("Apply wetness state"))
            {
                Run(
                    () => controller.SetWetness(
                        groundWetness,
                        roadWetness,
                        puddles,
                        vegetationWetness),
                    "Wetness state applied.");
            }

            exposure = (WeatherExposureContext)EditorGUILayout.EnumPopup("Exposure", exposure);
            if (GUILayout.Button("Apply exposure"))
            {
                Run(() => controller.SetExposure(exposure), "Exposure applied.");
            }
            EditorGUILayout.LabelField("Listener exposure", controller.ExposureContext.ToString());
            EditorGUILayout.LabelField(
                "Global surface exposure",
                controller.Wetness.ExposureProfileId ?? string.Empty);
        }

        private void DrawLightning(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lightning", EditorStyles.boldLabel);
            bool nonLethal = EditorGUILayout.Toggle("Non-lethal", controller.NonLethalLightning);
            if (nonLethal != controller.NonLethalLightning)
            {
                controller.SetNonLethalLightning(nonLethal);
            }

            EditorGUILayout.LabelField("Candidates/cooldowns", controller.BuildLightningDiagnostics(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ambient visual"))
            {
                Run(() => controller.TriggerAmbientLightning(), "Ambient visual request emitted.");
            }

            if (GUILayout.Button("Gameplay strike"))
            {
                Run(() =>
                {
                    if (!controller.TriggerGameplayLightningAtTarget(out var result))
                    {
                        throw new InvalidOperationException(
                            "Strike blocked by spawn/restore grace, cooldown or missing candidates.");
                    }

                    lastAction =
                        $"Gameplay strike {result.StrikeEvent.CandidateId}; " +
                        $"thunder delay={result.Thunder.DelaySeconds:F2}s; " +
                        $"nonLethal={result.GameplayEffect.NonLethal}.";
                }, preserveActionText: true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPresentation(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Low"))
            {
                Run(() => controller.ApplyQuality(EnvironmentQualityTier.Low), "Low quality applied.");
            }

            if (GUILayout.Button("Medium"))
            {
                Run(
                    () => controller.ApplyQuality(EnvironmentQualityTier.Medium),
                    "Medium quality applied.");
            }

            if (GUILayout.Button("High"))
            {
                Run(() => controller.ApplyQuality(EnvironmentQualityTier.High), "High quality applied.");
            }

            if (GUILayout.Button("Validate/present now"))
            {
                Run(() => controller.RefreshPresentation(), "Domain and binding presentation refreshed.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEvidence(WeatherLabStateController controller)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostics/evidence", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export diagnostics"))
            {
                Run(() => ExportDiagnostics(controller), "Diagnostics exported to Logs/M07B_TimeWeatherDiagnostics.json.");
            }

            if (GUILayout.Button("Capture Game View"))
            {
                Run(CaptureGameView, "Screenshot request queued under References/Weather/Milestone07B.");
            }
            EditorGUILayout.EndHorizontal();
        }

        private static WeatherLabStateController FindController()
        {
            WeatherLabStateController[] controllers = FindObjectsByType<WeatherLabStateController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return controllers.Length == 1 ? controllers[0] : null;
        }

        private static void ExportDiagnostics(WeatherLabStateController controller)
        {
            Directory.CreateDirectory("Logs");
            GameTimeSnapshot time = controller.GameTime.Snapshot;
            WeatherState weather = controller.CurrentLogicalWeather;
            var dto = new DiagnosticsDto
            {
                schemaVersion = 1,
                date = time.Date.ToString(),
                secondsOfDay = time.SecondsOfDay,
                dayIndex = time.DayIndex,
                timeScale = time.TimeScale,
                paused = time.IsPaused,
                weatherStateId = weather.Id.Value,
                bindingId = weather.PresentationBindingId,
                timelineCursor = controller.CurrentTimeline.CompletedFrontCount,
                scheduleFrozen = controller.IsScheduleFrozen,
                weatherSeed = controller.WeatherSeed.ToString(),
                groundWetness = controller.Wetness.GroundWetness01,
                roadWetness = controller.Wetness.RoadWetness01,
                puddles = controller.Wetness.PuddleAmount01,
                vegetationWetness = controller.Wetness.VegetationWetness01,
                adapterState = controller.Status.State.ToString(),
                presentedRevision = controller.Status.LastAppliedRevision.ToString(),
                lightning = controller.BuildLightningDiagnostics()
            };
            File.WriteAllText(
                "Logs/M07B_TimeWeatherDiagnostics.json",
                JsonUtility.ToJson(dto, true));
        }

        private static void CaptureGameView()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Game View capture requires Play Mode.");
            }

            string directory = Path.GetFullPath("References/Weather/Milestone07B");
            Directory.CreateDirectory(directory);
            string name = "M07B_WeatherLab_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name));
        }

        private void Run(Action action, string success = null, bool preserveActionText = false)
        {
            try
            {
                action();
                if (!preserveActionText)
                {
                    lastAction = success ?? "Completed.";
                }
            }
            catch (Exception exception)
            {
                lastAction = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private static string FormatTime(double secondsOfDay)
        {
            int totalSeconds = Mathf.Clamp((int)secondsOfDay, 0, 86399);
            return $"{totalSeconds / 3600:D2}:{totalSeconds % 3600 / 60:D2}:{totalSeconds % 60:D2}";
        }

        private static ulong DrawUlongField(string label, ulong value)
        {
            string text = EditorGUILayout.TextField(label, value.ToString());
            return ulong.TryParse(text, out ulong parsed) ? parsed : value;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            Repaint();
        }

        [Serializable]
        private sealed class DiagnosticsDto
        {
            public int schemaVersion;
            public string date;
            public double secondsOfDay;
            public long dayIndex;
            public double timeScale;
            public bool paused;
            public string weatherStateId;
            public string bindingId;
            public long timelineCursor;
            public bool scheduleFrozen;
            public string weatherSeed;
            public float groundWetness;
            public float roadWetness;
            public float puddles;
            public float vegetationWetness;
            public string adapterState;
            public string presentedRevision;
            public string lightning;
        }
    }
}
