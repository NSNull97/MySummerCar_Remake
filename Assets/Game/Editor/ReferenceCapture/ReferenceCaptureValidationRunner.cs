using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.ReferenceCapture;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.ReferenceCapture
{
    public static class ReferenceCaptureValidationRunner
    {
        private static readonly string[] RequiredFixtureFiles =
        {
            "player_movement_interaction.json",
            "garage_dimensions_clearance.json",
            "vehicle_body_wheel_geometry.json",
            "representative_part_mount_pivot.json",
            "engine_idle_start_stall.json",
            "gearbox_final_drive.json",
            "steering_suspension.json",
            "braking_acceleration.json",
            "time_progression.json",
            "weather_transition.json",
            "audio_state_mapping.json"
        };

        private static readonly string[] RequiredDocumentationFiles =
        {
            "REFERENCE_DATA_FORMAT.md", "REFERENCE_DATABASE_INDEX.csv", "REFERENCE_SOURCE_MAP.md",
            "MANUAL_CAPTURE_GUIDE.md", "PLAYER_INTERACTION_CAPTURE.md", "VEHICLE_ASSEMBLY_CAPTURE.md",
            "VEHICLE_BEHAVIOR_CAPTURE.md", "WORLD_LANDMARK_CAPTURE.md", "TIME_WEATHER_CAPTURE.md",
            "AUDIO_REFERENCE_CAPTURE.md", "UI_STATE_CAPTURE.md", "MISSING_REFERENCE_DATA.csv",
            "CAPTURE_SESSION_LOG.csv"
        };

        [MenuItem("Tools/MSC Remake/Reference Capture/Validate Database")]
        public static void ValidateInteractive()
        {
            ReferenceCaptureValidationResult result = ValidateProject(out List<string> projectErrors);
            string message = result.IsValid && projectErrors.Count == 0
                ? $"Reference capture validation passed. Records: {ReferenceCaptureDatabaseStore.LoadDatabase().Records.Count()}, missing P0: {result.MissingP0Requirements.Count}, missing P1: {result.MissingP1Requirements.Count}."
                : "Reference capture validation failed:\n" + string.Join("\n", projectErrors.Concat(result.Issues.Where(issue => issue.Severity == ReferenceValidationSeverity.Error).Select(issue => issue.ToString())));
            EditorUtility.DisplayDialog("Reference Capture Validation", message, "OK");
        }

        public static ReferenceCaptureValidationResult ValidateProject(out List<string> projectErrors)
        {
            projectErrors = new List<string>();
            ReferenceCaptureDatabase database = ReferenceCaptureDatabaseStore.LoadDatabase();
            ReferenceTuningOverrideSet tuning = ReferenceCaptureDatabaseStore.LoadTuningOverrides();
            ReferenceCaptureValidationResult result = ReferenceCaptureValidator.Validate(database, tuning);
            if (File.ReadAllText(ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.DatabaseAssetPath))
                .Contains("tunedValue", StringComparison.Ordinal))
                projectErrors.Add("Measured database contains a tunedValue field; tuning must remain in the separate override file.");

            string fixtureRoot = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.FixtureAssetRoot);
            foreach (string file in RequiredFixtureFiles)
            {
                string path = Path.Combine(fixtureRoot, file);
                if (!File.Exists(path)) projectErrors.Add("Missing calibration fixture: " + file);
                else
                {
                    ReferenceCalibrationFixture fixture = ReferenceCalibrationFixture.FromJson(File.ReadAllText(path));
                    if (fixture.SchemaVersion != ReferenceCaptureDatabaseVersion.CurrentSchemaVersion)
                        projectErrors.Add("Fixture has unsupported schema: " + file);
                    foreach (string recordId in fixture.RecordIds)
                        if (database.FindRecord(recordId) == null) projectErrors.Add($"Fixture {file} references unknown record {recordId}.");
                }
            }

            string docsRoot = ReferenceCapturePaths.ToAbsoluteProjectPath(ReferenceCapturePaths.DocumentationRoot);
            foreach (string file in RequiredDocumentationFiles)
                if (!File.Exists(Path.Combine(docsRoot, file))) projectErrors.Add("Missing reference-capture document: " + file);
            return result;
        }

        public static void RunBatch()
        {
            ReferenceCaptureValidationResult result = ValidateProject(out List<string> projectErrors);
            foreach (ReferenceValidationIssue warning in result.Issues.Where(issue => issue.Severity == ReferenceValidationSeverity.Warning))
                Debug.LogWarning("M04B_REFERENCE_WARNING " + warning);
            if (!result.IsValid || projectErrors.Count > 0)
                throw new InvalidOperationException("M04B validation failed:\n" + string.Join("\n", projectErrors.Concat(result.Issues.Where(issue => issue.Severity == ReferenceValidationSeverity.Error).Select(issue => issue.ToString()))));
            Debug.Log($"M04B_REFERENCE_CAPTURE_VALIDATION_OK records={ReferenceCaptureDatabaseStore.LoadDatabase().Records.Count()} missingP0={result.MissingP0Requirements.Count} missingP1={result.MissingP1Requirements.Count}");
        }
    }
}
