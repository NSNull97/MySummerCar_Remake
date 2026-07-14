using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldTransferCommands
    {
        private const string Root = "Tools/MSC Remake/World Transfer/";

        [MenuItem(Root + "Open World Transfer Window")]
        public static void OpenWindow() => WorldTransferWindow.Open();

        [MenuItem(Root + "Validate Paths")]
        public static void ValidatePaths() => Show(WorldTransferValidator.Validate(requireGeneratedScenes: false));

        [MenuItem(Root + "Scan Donor World")]
        public static void ScanDonorWorld() => ValidatePaths();

        [MenuItem(Root + "Dry Run Extraction Plan")]
        public static void DryRun() => Show(WorldTransferValidator.Validate(requireGeneratedScenes: false));

        [MenuItem(Root + "Build Intermediate World Database")]
        public static void BuildDatabase() => Show(WorldTransferValidator.Validate(requireGeneratedScenes: false));

        [MenuItem(Root + "Import Selected Zone")]
        public static void ImportSelectedZone() => WorldPartitionBuilder.GenerateSelected(EditorPrefs.GetString("MSC.WorldTransfer.SelectedCell", "cell_0_0"));

        [MenuItem(Root + "Import All World Geometry")]
        public static void ImportAll() => ConfirmAndGenerateAll();

        [MenuItem(Root + "Generate Selected World Cells")]
        public static void GenerateSelected() => ImportSelectedZone();

        [MenuItem(Root + "Generate All World Cells")]
        public static void GenerateAll() => ConfirmAndGenerateAll();

        [MenuItem(Root + "Validate World Database")]
        public static void ValidateDatabase() => Show(WorldTransferValidator.Validate(requireGeneratedScenes: false));

        [MenuItem(Root + "Validate Generated Scenes")]
        public static void ValidateScenes() => Show(WorldTransferValidator.Validate(requireGeneratedScenes: true));

        [MenuItem(Root + "Compare Landmark Fixtures")]
        public static void CompareLandmarks() => RevealReport("WORLD_FIDELITY_REPORT.md");

        [MenuItem(Root + "Show Missing References")]
        public static void ShowMissing() => RevealExternal("WorldMissingReferences.csv");

        [MenuItem(Root + "Show Unsupported Objects")]
        public static void ShowUnsupported() => RevealExternal("WorldUnsupportedObjects.csv");

        [MenuItem(Root + "Open World Transfer Reports")]
        public static void OpenReports() => EditorUtility.RevealInFinder(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.ReportsRelativePath));

        [MenuItem(Root + "Open Reference Overview")]
        public static void OpenOverview() => WorldPartitionBuilder.OpenReferenceOverview();

        [MenuItem(Root + "Clear Generated World Content")]
        public static void ClearGenerated()
        {
            if (EditorUtility.DisplayDialog("Clear generated world reference?", "This removes only the ignored ReferenceOnly/World/Generated tree. Project-owned database and source code remain.", "Clear", "Cancel"))
                WorldPartitionBuilder.ClearGenerated();
        }

        [MenuItem(Root + "Rebuild Generated World Content")]
        public static void Rebuild()
        {
            if (EditorUtility.DisplayDialog("Rebuild generated world reference?", "The ignored generated reference tree will be cleared and deterministically regenerated.", "Rebuild", "Cancel"))
            {
                WorldPartitionBuilder.ClearGenerated();
                WorldPartitionBuilder.GenerateAll();
            }
        }

        private static void ConfirmAndGenerateAll()
        {
            if (Application.isBatchMode || EditorUtility.DisplayDialog("Generate all world cells?", "This creates the full ignored reference proxy world from the project-owned database.", "Generate", "Cancel"))
                WorldPartitionBuilder.GenerateAll();
        }

        private static void RevealExternal(string file)
        {
            string path = Path.Combine(WorldTransferEditorConfiguration.Load().NormalizedDataPath, file);
            if (!File.Exists(path)) throw new FileNotFoundException("World transfer output is missing.", path);
            EditorUtility.RevealInFinder(path);
        }

        private static void RevealReport(string file)
        {
            string path = WorldTransferPaths.ToAbsoluteProjectPath(Path.Combine(WorldTransferPaths.ReportsRelativePath, file));
            if (!File.Exists(path)) throw new FileNotFoundException("World transfer report is missing.", path);
            EditorUtility.RevealInFinder(path);
        }

        private static void Show(WorldTransferValidationResult result)
        {
            string message = result.IsValid
                ? $"Valid. Entities: {result.EntityCount}; reference world: {result.EligibleEntityCount}; cells: {result.CellCount}; warnings: {result.Warnings.Count}."
                : "Errors:\n" + string.Join("\n", result.Errors);
            UnityEngine.Debug.Log((result.IsValid ? "WORLD_TRANSFER_VALID " : "WORLD_TRANSFER_INVALID ") + message);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("World Transfer Validation", message, "OK");
        }
    }
}
