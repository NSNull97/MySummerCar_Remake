using System;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;
using MSC.World.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public sealed class WorldValidationDashboard : EditorWindow
    {
        private enum DetailView
        {
            BlockingIssues,
            Coverage,
            Dependencies,
            PerformanceLocations,
            ValidatorRuns
        }

        private WorldValidationGate selectedGate = WorldValidationGate.PilotGate;
        private string selectedZone = WorldRemasterPaths.PilotZoneId;
        private string stableWorldId = string.Empty;
        private Vector2 scroll;
        private DetailView detailView;
        private WorldValidationResult result;
        private string[] zoneIds = Array.Empty<string>();
        private string stableIdDetails = string.Empty;

        [MenuItem("Tools/MSC Remake/World Validation/Open Dashboard")]
        public static void Open()
        {
            WorldValidationDashboard window = GetWindow<WorldValidationDashboard>("World Validation");
            window.minSize = new Vector2(760f, 620f);
            window.RefreshZones();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshZones();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Milestone 05B — World Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Dashboard validates current facts. An expected gate failure is an exported finding, not a tool failure.",
                MessageType.Info);

            selectedGate = (WorldValidationGate)EditorGUILayout.EnumPopup("Selected gate", selectedGate);
            int zoneIndex = Math.Max(0, Array.IndexOf(zoneIds, selectedZone));
            if (zoneIds.Length > 0)
            {
                zoneIndex = EditorGUILayout.Popup("Selected zone", zoneIndex, zoneIds);
                selectedZone = zoneIds[zoneIndex];
            }
            else
            {
                selectedZone = EditorGUILayout.TextField("Selected zone", selectedZone);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run all world validations"))
            {
                result = WorldValidationRunner.ValidateAll();
                detailView = DetailView.BlockingIssues;
            }
            if (GUILayout.Button("Validate selected gate"))
            {
                result = WorldValidationRunner.ValidateGate(selectedGate);
                detailView = DetailView.BlockingIssues;
            }
            if (GUILayout.Button("Validate selected zone"))
            {
                result = WorldValidationRunner.ValidateZone(selectedZone, selectedGate);
                detailView = DetailView.BlockingIssues;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export full machine results"))
            {
                result = WorldValidationRunner.ValidateAll();
                WorldValidationRunner.Export(result);
                detailView = DetailView.ValidatorRuns;
            }
            if (GUILayout.Button("Show blocking issues")) detailView = DetailView.BlockingIssues;
            if (GUILayout.Button("Show coverage")) detailView = DetailView.Coverage;
            if (GUILayout.Button("Show donor dependency")) detailView = DetailView.Dependencies;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reference / production overlay")) WorldRemasterCommands.ShowOverlayComparison();
            if (GUILayout.Button("Performance capture locations")) detailView = DetailView.PerformanceLocations;
            if (GUILayout.Button("Validator runs")) detailView = DetailView.ValidatorRuns;
            if (GUILayout.Button("Open latest report")) Reveal(WorldValidationPaths.Summary);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stable-ID navigation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            stableWorldId = EditorGUILayout.TextField(stableWorldId);
            if (GUILayout.Button("Jump to stable ID", GUILayout.Width(160f))) JumpToStableId(stableWorldId);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(stableIdDetails))
            {
                EditorGUILayout.HelpBox(stableIdDetails, MessageType.None);
            }

            if (result == null)
            {
                EditorGUILayout.HelpBox("Run a validation action to populate the dashboard.", MessageType.None);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                string.Equals(result.scope, "Zone", StringComparison.Ordinal)
                    ? "Scoped zone gate (issue-filtered)"
                    : "Achieved gate",
                result.achievedGate);
            EditorGUILayout.LabelField("Scope", result.scope);
            if (!string.IsNullOrWhiteSpace(result.selectedZone))
            {
                EditorGUILayout.LabelField("Selected zone", result.selectedZone);
            }
            if (string.Equals(result.scope, "Zone", StringComparison.Ordinal))
            {
                WorldValidationCoverageRow zoneCoverage = result.coverage.FirstOrDefault(row =>
                    string.Equals(row.scope, "Zone", StringComparison.Ordinal) &&
                    string.Equals(row.scopeId, result.selectedZone, StringComparison.Ordinal));
                if (zoneCoverage != null)
                {
                    EditorGUILayout.LabelField(
                        "Zone production bindings",
                        $"{zoneCoverage.covered}/{zoneCoverage.total} ({zoneCoverage.coveragePercent:0.000000}%)");
                }
            }
            EditorGUILayout.LabelField(
                "Project eligible production bindings",
                $"{result.productionBindingCount}/{result.eligibleWorldRecordCount} " +
                $"({WorldValidationGateCalculator.CoveragePercent(result.productionBindingCount, result.eligibleWorldRecordCount):0.000000}%)");
            EditorGUILayout.LabelField("Project production-bound cells", $"{result.productionBoundCellCount}/{result.concreteCellCount}");
            foreach (WorldValidationGateResult gate in result.gates)
            {
                EditorGUILayout.LabelField(
                    gate.gate,
                    gate.achieved ? "PASS" : "FAIL — " + string.Join(", ", gate.blockingIssueIds));
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (detailView)
            {
                case DetailView.Coverage:
                    DrawCoverage();
                    break;
                case DetailView.Dependencies:
                    DrawDependencies();
                    break;
                case DetailView.PerformanceLocations:
                    DrawPerformanceLocations();
                    break;
                case DetailView.ValidatorRuns:
                    DrawValidatorRuns();
                    break;
                default:
                    DrawBlockingIssues();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBlockingIssues()
        {
            EditorGUILayout.LabelField("Open issues", EditorStyles.boldLabel);
            foreach (WorldValidationIssue issue in result.issues.Where(issue => issue.IsOpen))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(issue.issueId + " — " + issue.severity, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(issue.summary, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Blocks", string.IsNullOrWhiteSpace(issue.blockingGates) ? "None (warning)" : issue.blockingGates);
                if (!string.IsNullOrWhiteSpace(issue.zoneId)) EditorGUILayout.LabelField("Zone", issue.zoneId);
                EditorGUILayout.LabelField("Evidence", issue.evidence, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Action", issue.requiredAction, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCoverage()
        {
            EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);
            foreach (WorldValidationCoverageRow row in result.coverage)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(row.scope, GUILayout.Width(145f));
                EditorGUILayout.LabelField(row.scopeId, GUILayout.Width(210f));
                EditorGUILayout.LabelField($"{row.covered}/{row.total}", GUILayout.Width(85f));
                EditorGUILayout.LabelField($"{row.coveragePercent:0.000000}%", GUILayout.Width(100f));
                EditorGUILayout.LabelField(row.status);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDependencies()
        {
            WorldValidationDependencyAudit audit = result.dependencyAudit;
            EditorGUILayout.LabelField("Production dependency graph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Seed assets", audit.seedAssets.ToString());
            EditorGUILayout.LabelField("Visited assets", audit.visitedAssets.ToString());
            EditorGUILayout.LabelField("Dependency edges", audit.dependencyEdges.ToString());
            EditorGUILayout.LabelField("Violations", audit.violations.Length.ToString());
            EditorGUILayout.LabelField(
                "Current-player Editor DLL audit",
                audit.editorAssemblyPlayerAuditAvailable ? "Available" : "Unavailable — no current-world standalone build");
            foreach (string violation in audit.violations)
            {
                EditorGUILayout.HelpBox(violation, MessageType.Error);
            }
        }

        private void DrawPerformanceLocations()
        {
            EditorGUILayout.LabelField("Required capture locations", EditorStyles.boldLabel);
            foreach (WorldValidationPerformanceLocation location in result.performanceLocations)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(location.locationId + " — " + location.status, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Zone", string.IsNullOrWhiteSpace(location.zoneId) ? "Unavailable" : location.zoneId);
                EditorGUILayout.LabelField("Available", location.availableMetrics, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Unavailable", location.unavailableMetrics, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawValidatorRuns()
        {
            EditorGUILayout.LabelField("Executed project validators", EditorStyles.boldLabel);
            foreach (WorldValidationValidatorRun validator in result.validatorRuns)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(validator.validatorId + " — " + validator.status, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Executed", validator.executed ? "Yes" : "No");
                EditorGUILayout.LabelField("Passed", validator.passed ? "Yes" : "No");
                EditorGUILayout.LabelField("Errors / warnings", $"{validator.errorCount} / {validator.warningCount}");
                EditorGUILayout.LabelField("Evidence", validator.evidence, EditorStyles.wordWrappedLabel);
                MessageType errorType = string.Equals(
                    validator.status,
                    "KnownProvenanceDrift",
                    StringComparison.Ordinal)
                    ? MessageType.Warning
                    : MessageType.Error;
                foreach (string error in validator.errors)
                {
                    EditorGUILayout.HelpBox(error, errorType);
                }
                foreach (string warning in validator.warnings)
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void RefreshZones()
        {
            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            zoneIds = registry == null
                ? Array.Empty<string>()
                : registry.Zones
                    .Where(zone => zone.ZoneId != "excluded")
                    .Select(zone => zone.ZoneId)
                    .OrderBy(zone => zone, StringComparer.Ordinal)
                    .ToArray();
            if (zoneIds.Length > 0 && !zoneIds.Contains(selectedZone, StringComparer.Ordinal))
            {
                selectedZone = zoneIds[0];
            }
        }

        private void JumpToStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                EditorUtility.DisplayDialog("World Validation", "Enter a stable world ID.", "OK");
                return;
            }
            stableId = stableId.Trim();

            WorldReferenceEntity loadedReference = UnityEngine.Object.FindObjectsByType<WorldReferenceEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(entity => string.Equals(entity.StableId, stableId, StringComparison.Ordinal));
            if (loadedReference != null)
            {
                Selection.activeObject = loadedReference.gameObject;
                EditorGUIUtility.PingObject(loadedReference.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
                stableIdDetails = DescribeReference(loadedReference);
                return;
            }

            WorldEntityPlacement entity = WorldRemasterRegistryBuilder.LoadEntities()
                .FirstOrDefault(candidate => string.Equals(candidate.StableId, stableId, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(entity.StableId))
            {
                stableIdDetails = DescribeEntity(entity);
                if (entity.ReferenceWorldEligible && !string.Equals(entity.CellId, "excluded", StringComparison.Ordinal))
                {
                    string referenceScenePath = string.Equals(entity.CellId, "global", StringComparison.Ordinal)
                        ? WorldTransferPaths.GlobalScene
                        : WorldTransferPaths.CellScene(entity.CellId);
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(referenceScenePath) == null)
                    {
                        stableIdDetails += "\nGenerated reference scene is missing: " + referenceScenePath;
                        PingEntityTable();
                        return;
                    }

                    Scene referenceScene = SceneManager.GetSceneByPath(referenceScenePath);
                    if (!referenceScene.IsValid() || !referenceScene.isLoaded)
                    {
                        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            stableIdDetails += "\nNavigation cancelled because modified scenes were not saved.";
                            return;
                        }

                        referenceScene = EditorSceneManager.OpenScene(referenceScenePath, OpenSceneMode.Additive);
                    }

                    WorldReferenceEntity reference = referenceScene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<WorldReferenceEntity>(true))
                        .FirstOrDefault(candidate =>
                            string.Equals(candidate.StableId, stableId, StringComparison.Ordinal));
                    if (reference != null)
                    {
                        Selection.activeObject = reference.gameObject;
                        EditorGUIUtility.PingObject(reference.gameObject);
                        SceneView.lastActiveSceneView?.FrameSelected();
                        stableIdDetails = DescribeReference(reference);
                        return;
                    }

                    stableIdDetails += "\nGenerated reference scene contains no matching WorldReferenceEntity.";
                    PingEntityTable();
                    return;
                }

                stableIdDetails += "\nThis record is excluded from the generated reference world; the source table row is selected instead.";
                PingEntityTable();
                return;
            }

            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            WorldProductionAssetRecord record = registry?.Records.FirstOrDefault(candidate =>
                string.Equals(candidate.StableWorldId, stableId, StringComparison.Ordinal));
            if (record != null && record.HasProductionReplacement)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(record.ProductionPrefab);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                stableIdDetails =
                    $"Stable ID: {stableId}\nProduction zone: {record.ProductionZone}\n" +
                    $"Replacement: {record.ReplacementStatus}\nAsset: {record.ProductionPrefab}";
                return;
            }

            stableIdDetails = "Stable ID was not found: " + stableId;
            EditorUtility.DisplayDialog("World Validation", "Stable ID was not found: " + stableId, "OK");
        }

        private static string DescribeReference(WorldReferenceEntity reference) =>
            $"Stable ID: {reference.StableId}\nCategory: {reference.SemanticCategory}\n" +
            $"Replacement: {reference.ReplacementStatus}\nTransfer: {reference.TransferStatus}\n" +
            $"Donor path: {reference.DonorHierarchyPath}";

        private static string DescribeEntity(WorldEntityPlacement entity) =>
            $"Stable ID: {entity.StableId}\nCell: {entity.CellId}\nCategory: {entity.Category}\n" +
            $"Eligible: {entity.ReferenceWorldEligible}\nReplacement: {entity.ReplacementStatus}\n" +
            $"Transfer: {entity.TransferStatus}\nPosition: {entity.Position}\nDonor path: {entity.HierarchyPath}";

        private static void PingEntityTable()
        {
            UnityEngine.Object table = AssetDatabase.LoadMainAssetAtPath(WorldTransferPaths.EntityTableAssetPath);
            if (table != null)
            {
                Selection.activeObject = table;
                EditorGUIUtility.PingObject(table);
                return;
            }

            Reveal(WorldTransferPaths.EntityTableAssetPath);
        }

        private static void Reveal(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute))
            {
                EditorUtility.DisplayDialog("World Validation", "File not found: " + path, "OK");
                return;
            }

            EditorUtility.RevealInFinder(absolute);
        }
    }
}
