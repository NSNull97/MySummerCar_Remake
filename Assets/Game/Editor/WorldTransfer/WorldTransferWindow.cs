using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldTransferWindow : EditorWindow
    {
        private string selectedCell = "cell_0_0";
        private Vector2 scroll;
        private WorldTransferValidationResult validation;
        private IReadOnlyList<KeyValuePair<string, int>> categoryCounts = Array.Empty<KeyValuePair<string, int>>();
        private int missingReferenceCount;
        private int unsupportedTypeCount;
        private int generatedCellCount;
        private string lastBuildTimestamp = "never";

        public static void Open()
        {
            var window = GetWindow<WorldTransferWindow>("World Transfer");
            window.minSize = new Vector2(520f, 480f);
            window.Refresh();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Milestone 04A1 — Full World Geometry Transfer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Reference-only donor geometry and project-owned metadata. This tool does not create production art or gameplay systems.", MessageType.Info);
            DrawConfiguration();
            EditorGUILayout.Space();
            DrawStatus();
            EditorGUILayout.Space();
            selectedCell = EditorGUILayout.TextField("Selected cell", selectedCell);
            EditorPrefs.SetString("MSC.WorldTransfer.SelectedCell", selectedCell);
            if (GUILayout.Button("Validate paths and database")) Refresh();
            if (GUILayout.Button("Dry run extraction plan")) WorldTransferCommands.DryRun();
            if (GUILayout.Button("Generate selected cell")) WorldPartitionBuilder.GenerateSelected(selectedCell);
            if (GUILayout.Button("Generate all cells (confirmation)")) WorldTransferCommands.GenerateAll();
            if (GUILayout.Button("Validate generated scenes")) validation = WorldTransferValidator.Validate(requireGeneratedScenes: true);
            if (GUILayout.Button("Show missing references")) WorldTransferCommands.ShowMissing();
            if (GUILayout.Button("Show unsupported objects")) WorldTransferCommands.ShowUnsupported();
            if (GUILayout.Button("Open reference overview")) WorldPartitionBuilder.OpenReferenceOverview();
            if (GUILayout.Button("Open reports")) WorldTransferCommands.OpenReports();
            if (GUILayout.Button("Clear generated content")) WorldTransferCommands.ClearGenerated();
            if (GUILayout.Button("Rebuild generated content")) WorldTransferCommands.Rebuild();
            EditorGUILayout.EndScrollView();
        }

        private void DrawConfiguration()
        {
            try
            {
                WorldTransferEditorConfiguration config = WorldTransferEditorConfiguration.Load();
                EditorGUILayout.LabelField("Donor", config.DonorGamePath);
                EditorGUILayout.LabelField("Staging", config.DonorStagingPath);
                EditorGUILayout.LabelField("Raw export", config.RawExtractionPath);
                EditorGUILayout.LabelField("Normalized", config.NormalizedDataPath);
                EditorGUILayout.LabelField("AssetRipper export", Directory.Exists(config.RawExtractionPath) ? "available" : "missing");
                EditorGUILayout.LabelField("Cell size", config.PartitionCellSizeMeters + " m");
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        private void DrawStatus()
        {
            if (validation == null)
            {
                EditorGUILayout.LabelField("Validation", "not run in this window session");
                return;
            }

            EditorGUILayout.LabelField("Database version", WorldPartitionBuilder.DatabaseVersion);
            EditorGUILayout.LabelField("Object records", validation.EntityCount.ToString());
            EditorGUILayout.LabelField("Reference entities", validation.EligibleEntityCount.ToString());
            EditorGUILayout.LabelField("Generated-cell plan", validation.CellCount.ToString());
            EditorGUILayout.LabelField("Generated cells on disk", generatedCellCount.ToString());
            EditorGUILayout.LabelField("Missing/review records", missingReferenceCount.ToString());
            EditorGUILayout.LabelField("Unsupported class IDs", unsupportedTypeCount.ToString());
            EditorGUILayout.LabelField("Last generated build", lastBuildTimestamp);
            EditorGUILayout.LabelField("Validation", validation.IsValid ? "valid" : "invalid");
            EditorGUILayout.LabelField("Warnings", validation.Warnings.Count.ToString());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference-world categories", EditorStyles.boldLabel);
            foreach (KeyValuePair<string, int> category in categoryCounts)
                EditorGUILayout.LabelField(category.Key, category.Value.ToString());
            foreach (string error in validation.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void Refresh()
        {
            selectedCell = EditorPrefs.GetString("MSC.WorldTransfer.SelectedCell", "cell_0_0");
            validation = WorldTransferValidator.Validate(requireGeneratedScenes: false);
            string entityPath = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath);
            IReadOnlyList<WorldEntityPlacement> records = WorldEntityTable.Parse(File.ReadAllText(entityPath));
            categoryCounts = records.Where(record => record.ReferenceWorldEligible)
                .GroupBy(record => record.Category, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).ToArray();
            WorldTransferEditorConfiguration config = WorldTransferEditorConfiguration.Load();
            missingReferenceCount = CountCsvRecords(Path.Combine(config.NormalizedDataPath, "WorldMissingReferences.csv"));
            unsupportedTypeCount = CountCsvRecords(Path.Combine(config.NormalizedDataPath, "WorldUnsupportedObjects.csv"));
            string sceneRoot = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedSceneRoot);
            generatedCellCount = Directory.Exists(sceneRoot) ? Directory.EnumerateFiles(sceneRoot, "World_cell_*.unity").Count() : 0;
            string bootstrap = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.BootstrapScene);
            lastBuildTimestamp = File.Exists(bootstrap) ? File.GetLastWriteTime(bootstrap).ToString("u") : "never";
            Repaint();
        }

        private static int CountCsvRecords(string path) => File.Exists(path) ? Math.Max(0, File.ReadLines(path).Count() - 1) : 0;
    }
}
