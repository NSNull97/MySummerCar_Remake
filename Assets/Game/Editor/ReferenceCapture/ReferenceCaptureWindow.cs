using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Core.ReferenceCapture;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.ReferenceCapture
{
    public sealed class ReferenceCaptureWindow : EditorWindow
    {
        private ReferenceCaptureDatabase database;
        private ReferenceTuningOverrideSet tuning;
        private ReferenceCaptureValidationResult validation;
        private IReadOnlyList<(string AssetPath, ReferenceCalibrationFixture Fixture)> fixtures = Array.Empty<(string, ReferenceCalibrationFixture)>();
        private ReferenceRecord[] filteredRecords = Array.Empty<ReferenceRecord>();
        private Vector2 scroll;
        private string statusMessage = string.Empty;
        private ReferenceCategory categoryFilter = ReferenceCategory.WorldLandmarks;
        private ReferencePriority priorityFilter = ReferencePriority.P0;
        private ReferenceValidationStatus statusFilter = ReferenceValidationStatus.Validated;
        private bool filterCategory;
        private bool filterPriority;
        private bool filterStatus;
        private int selectedRecordIndex;
        private int selectedFixtureIndex;

        private ReferenceCategory manualCategory = ReferenceCategory.PlayerInteraction;
        private ReferencePriority manualPriority = ReferencePriority.P1;
        private ReferenceUnit manualUnit = ReferenceUnit.Meter;
        private ReferenceCoordinateSpace manualCoordinateSpace = ReferenceCoordinateSpace.NotApplicable;
        private ReferenceConfidence manualConfidence = ReferenceConfidence.Medium;
        private string manualSubcategory = "manual";
        private string manualName = string.Empty;
        private float manualValue;
        private string manualRawObservation = string.Empty;
        private string manualEvidenceRoot = "ReferenceMedia";
        private string manualEvidencePath = string.Empty;
        private string manualNotes = string.Empty;

        private double conversionValue = 1d;
        private ReferenceUnit conversionFrom = ReferenceUnit.Meter;
        private ReferenceUnit conversionTo = ReferenceUnit.Centimeter;
        private string conversionResult = string.Empty;

        [MenuItem("Tools/MSC Remake/Reference Capture")]
        public static void Open()
        {
            var window = GetWindow<ReferenceCaptureWindow>("Reference Capture");
            window.minSize = new Vector2(720f, 600f);
            window.Refresh();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Milestone 04B — Reference Capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Project-owned measurements and behavioral fixtures. Donor sources are read-only; missing observations stay explicit and remake tuning lives in a separate file.",
                MessageType.Info);
            DrawDashboard();
            EditorGUILayout.Space();
            DrawFiltersAndRecords();
            EditorGUILayout.Space();
            DrawMeasuredVersusTuned();
            EditorGUILayout.Space();
            DrawImportAndManualObservation();
            EditorGUILayout.Space();
            DrawUnitConversion();
            EditorGUILayout.Space();
            DrawFixturesAndChecklist();
            if (!string.IsNullOrWhiteSpace(statusMessage)) EditorGUILayout.HelpBox(statusMessage, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawDashboard()
        {
            if (database == null)
            {
                EditorGUILayout.HelpBox("Database is not loaded.", MessageType.Error);
                if (GUILayout.Button("Reload")) Refresh();
                return;
            }

            EditorGUILayout.LabelField("Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Dataset", database.Version.DatasetVersion);
            EditorGUILayout.LabelField("Measurements", database.Measurements.Length.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Behavior fixtures", database.BehaviorFixtures.Length.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Sources / evidence", $"{database.Sources.Length} / {database.Evidence.Length}");
            EditorGUILayout.LabelField("Validation", validation == null ? "not run" : validation.IsValid ? "valid" : "invalid");
            if (validation != null)
            {
                EditorGUILayout.LabelField("Missing P0 / P1", $"{validation.MissingP0Requirements.Count} / {validation.MissingP1Requirements.Count}");
                foreach (ReferenceValidationIssue issue in validation.Issues)
                    EditorGUILayout.HelpBox(issue.ToString(), issue.Severity == ReferenceValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh")) Refresh();
            if (GUILayout.Button("Validate units / IDs / confidence")) Validate();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFiltersAndRecords()
        {
            EditorGUILayout.LabelField("Records", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            filterCategory = EditorGUILayout.ToggleLeft("Category", filterCategory, GUILayout.Width(80f));
            categoryFilter = (ReferenceCategory)EditorGUILayout.EnumPopup(categoryFilter);
            filterPriority = EditorGUILayout.ToggleLeft("Priority", filterPriority, GUILayout.Width(75f));
            priorityFilter = (ReferencePriority)EditorGUILayout.EnumPopup(priorityFilter);
            filterStatus = EditorGUILayout.ToggleLeft("Status", filterStatus, GUILayout.Width(65f));
            statusFilter = (ReferenceValidationStatus)EditorGUILayout.EnumPopup(statusFilter);
            if (GUILayout.Button("Apply", GUILayout.Width(60f))) ApplyFilters();
            EditorGUILayout.EndHorizontal();

            if (filteredRecords.Length == 0)
            {
                EditorGUILayout.LabelField("No matching records.");
                return;
            }
            string[] labels = filteredRecords.Select(record => $"{record.Priority} | {record.Category} | {record.Name}").ToArray();
            selectedRecordIndex = Mathf.Clamp(selectedRecordIndex, 0, labels.Length - 1);
            selectedRecordIndex = EditorGUILayout.Popup("Selected", selectedRecordIndex, labels);
            ReferenceRecord selected = filteredRecords[selectedRecordIndex];
            EditorGUILayout.LabelField("Stable ID", selected.StableId);
            EditorGUILayout.LabelField("Method / confidence", $"{selected.CaptureMethod} / {selected.Confidence}");
            EditorGUILayout.LabelField("Unit / coordinates", $"{selected.Unit} / {selected.CoordinateSpace}");
            EditorGUILayout.LabelField("Source locator", selected.SourceLocator);
            EditorGUILayout.LabelField("Raw", selected.RawObservation, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Normalized", FormatValue(selected.NormalizedValue));
            if (GUILayout.Button("Open related donor/reference evidence")) OpenSelectedEvidence(selected);
        }

        private void DrawMeasuredVersusTuned()
        {
            EditorGUILayout.LabelField("Measured vs tuned", EditorStyles.boldLabel);
            if (filteredRecords.Length == 0 || tuning == null) return;
            ReferenceRecord selected = filteredRecords[Mathf.Clamp(selectedRecordIndex, 0, filteredRecords.Length - 1)];
            ReferenceTuningOverride matched = tuning.Overrides.FirstOrDefault(item => item.MeasuredRecordId == selected.StableId);
            EditorGUILayout.LabelField("Measured", $"{FormatValue(selected.NormalizedValue)} {selected.Unit}");
            EditorGUILayout.LabelField("Tuned", matched == null ? "no override" : $"{FormatValue(matched.TunedValue)} {matched.Unit}");
            if (matched != null) EditorGUILayout.LabelField("Rationale", matched.Rationale, EditorStyles.wordWrappedLabel);
        }

        private void DrawImportAndManualObservation()
        {
            EditorGUILayout.LabelField("Import and manual observation", EditorStyles.boldLabel);
            if (GUILayout.Button("Import structured donor measurements (JSON)..."))
            {
                string path = EditorUtility.OpenFilePanel("Structured Reference Import", string.Empty, "json");
                if (!string.IsNullOrWhiteSpace(path)) RunAction(() => ReferenceCaptureDatabaseStore.ImportStructuredMeasurements(File.ReadAllText(path)));
            }

            manualCategory = (ReferenceCategory)EditorGUILayout.EnumPopup("Category", manualCategory);
            manualPriority = (ReferencePriority)EditorGUILayout.EnumPopup("Priority", manualPriority);
            manualSubcategory = EditorGUILayout.TextField("Subcategory", manualSubcategory);
            manualName = EditorGUILayout.TextField("Name", manualName);
            manualValue = EditorGUILayout.FloatField("Normalized numeric value", manualValue);
            manualUnit = (ReferenceUnit)EditorGUILayout.EnumPopup("Unit", manualUnit);
            manualCoordinateSpace = (ReferenceCoordinateSpace)EditorGUILayout.EnumPopup("Coordinate space", manualCoordinateSpace);
            manualConfidence = (ReferenceConfidence)EditorGUILayout.EnumPopup("Confidence", manualConfidence);
            manualRawObservation = EditorGUILayout.TextField("Raw observation", manualRawObservation);
            manualEvidenceRoot = EditorGUILayout.TextField("Evidence root kind", manualEvidenceRoot);
            manualEvidencePath = EditorGUILayout.TextField("Evidence logical path", manualEvidencePath);
            manualNotes = EditorGUILayout.TextField("Notes", manualNotes);
            if (GUILayout.Button("Add manual observation"))
            {
                RunAction(() => "Added " + ReferenceCaptureDatabaseStore.AddManualNumericObservation(
                    manualCategory, manualPriority, manualSubcategory, manualName, manualValue, manualUnit,
                    manualCoordinateSpace, manualConfidence, manualRawObservation, manualEvidenceRoot,
                    manualEvidencePath, manualNotes));
            }
        }

        private void DrawUnitConversion()
        {
            EditorGUILayout.LabelField("Unit conversion", EditorStyles.boldLabel);
            conversionValue = EditorGUILayout.DoubleField("Value", conversionValue);
            conversionFrom = (ReferenceUnit)EditorGUILayout.EnumPopup("From", conversionFrom);
            conversionTo = (ReferenceUnit)EditorGUILayout.EnumPopup("To", conversionTo);
            if (GUILayout.Button("Convert"))
            {
                conversionResult = ReferenceUnitConversion.TryConvert(conversionValue, conversionFrom, conversionTo, out double converted)
                    ? converted.ToString("G17", CultureInfo.InvariantCulture)
                    : "incompatible units";
            }
            EditorGUILayout.LabelField("Result", conversionResult);
        }

        private void DrawFixturesAndChecklist()
        {
            EditorGUILayout.LabelField("Calibration fixtures and capture queue", EditorStyles.boldLabel);
            if (fixtures.Count > 0)
            {
                selectedFixtureIndex = Mathf.Clamp(selectedFixtureIndex, 0, fixtures.Count - 1);
                selectedFixtureIndex = EditorGUILayout.Popup("Fixture", selectedFixtureIndex, fixtures.Select(item => item.Fixture.Title).ToArray());
                ReferenceCalibrationFixture selected = fixtures[selectedFixtureIndex].Fixture;
                EditorGUILayout.LabelField("Status", selected.Status.ToString());
                EditorGUILayout.LabelField("Missing fields", selected.MissingRequirementIds.Length.ToString(CultureInfo.InvariantCulture));
                if (GUILayout.Button("Export selected calibration fixture..."))
                {
                    string destination = EditorUtility.SaveFilePanel("Export Calibration Fixture", string.Empty,
                        Path.GetFileName(fixtures[selectedFixtureIndex].AssetPath), "json");
                    RunAction(() => { ReferenceCaptureDatabaseStore.ExportFixture(fixtures[selectedFixtureIndex].AssetPath, destination); return "Fixture exported."; });
                }
            }

            if (validation != null)
            {
                EditorGUILayout.LabelField("Missing P0", validation.MissingP0Requirements.Count.ToString(CultureInfo.InvariantCulture));
                foreach (ReferenceRequirementRecord item in validation.MissingP0Requirements.Take(12))
                    EditorGUILayout.LabelField($"{item.RequirementId}: {item.Name} ({item.Status})", EditorStyles.wordWrappedLabel);
            }
            if (GUILayout.Button("Generate capture checklist"))
                RunAction(() => "Generated " + ReferenceCaptureDatabaseStore.GenerateChecklist(database));
        }

        private void Refresh()
        {
            try
            {
                database = ReferenceCaptureDatabaseStore.LoadDatabase();
                tuning = ReferenceCaptureDatabaseStore.LoadTuningOverrides();
                fixtures = ReferenceCaptureDatabaseStore.LoadFixtures();
                validation = ReferenceCaptureValidator.Validate(database, tuning);
                ApplyFilters();
                statusMessage = string.Empty;
            }
            catch (Exception exception)
            {
                statusMessage = exception.Message;
            }
            Repaint();
        }

        private void Validate()
        {
            try
            {
                validation = ReferenceCaptureValidationRunner.ValidateProject(out List<string> projectErrors);
                statusMessage = validation.IsValid && projectErrors.Count == 0
                    ? "Validation passed. Missing P0/P1 remain an explicit capture queue."
                    : string.Join("\n", projectErrors);
            }
            catch (Exception exception) { statusMessage = exception.Message; }
        }

        private void ApplyFilters()
        {
            if (database == null) return;
            IEnumerable<ReferenceRecord> records = database.Records;
            if (filterCategory) records = records.Where(record => record.Category == categoryFilter);
            if (filterPriority) records = records.Where(record => record.Priority == priorityFilter);
            if (filterStatus) records = records.Where(record => record.ValidationStatus == statusFilter);
            filteredRecords = records.OrderBy(record => record.Category).ThenBy(record => record.Priority)
                .ThenBy(record => record.Name, StringComparer.Ordinal).ToArray();
            selectedRecordIndex = 0;
        }

        private void OpenSelectedEvidence(ReferenceRecord selected)
        {
            ReferenceEvidenceRecord evidence = selected.EvidenceIds.Select(id => database.Evidence.FirstOrDefault(item => item.StableId == id))
                .FirstOrDefault(item => item != null);
            if (evidence == null || !ReferenceCaptureDatabaseStore.TryResolveEvidencePath(evidence, out string path))
            {
                statusMessage = "Evidence path cannot be resolved with the local path configuration.";
                return;
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                statusMessage = "Evidence path is currently unavailable: " + path;
                return;
            }
            EditorUtility.RevealInFinder(path);
            statusMessage = path;
        }

        private void RunAction(Func<string> action)
        {
            try { string message = action(); Refresh(); statusMessage = message; }
            catch (Exception exception) { statusMessage = exception.Message; }
        }

        private static string FormatValue(ReferenceValue value)
        {
            switch (value.Kind)
            {
                case ReferenceValueKind.Numeric: return value.NumericValue.ToString("G9", CultureInfo.InvariantCulture);
                case ReferenceValueKind.Vector3: return $"({value.VectorValue.x:G9}, {value.VectorValue.y:G9}, {value.VectorValue.z:G9})";
                default: return value.TextValue;
            }
        }
    }
}
