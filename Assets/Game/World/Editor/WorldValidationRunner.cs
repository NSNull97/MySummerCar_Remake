using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Core.Identity;
using MSC.Editor.WorldStreaming;
using MSC.Editor.WorldTransfer;
using MSC.World;
using MSC.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public static class WorldValidationPaths
    {
        public const string DocumentationRoot = "Docs/WorldValidation";
        public const string Summary = DocumentationRoot + "/WORLD_VALIDATION_SUMMARY.md";
        public const string Issues = DocumentationRoot + "/WORLD_VALIDATION_ISSUES.csv";
        public const string Coverage = DocumentationRoot + "/WORLD_COVERAGE.csv";
        public const string SpatialDeviation = DocumentationRoot + "/SPATIAL_DEVIATION.csv";
        public const string ProductionDependencyAudit = DocumentationRoot + "/PRODUCTION_DEPENDENCY_AUDIT.md";
        public const string StreamingValidation = DocumentationRoot + "/STREAMING_VALIDATION.md";
        public const string TraversalAndClearance = DocumentationRoot + "/TRAVERSAL_AND_CLEARANCE.md";
        public const string PerformanceValidation = DocumentationRoot + "/PERFORMANCE_VALIDATION.md";
        public const string MachineResult = DocumentationRoot + "/WORLD_VALIDATION_RESULT.json";
        public const string MilestoneReport = "Docs/Milestones/MILESTONE_05B_REPORT.md";

        public const string SupplementalVoidRegions =
            "Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv";
        public const string GeometryDefects = "Docs/WorldTransfer/M05C_GEOMETRY_DEFECTS.csv";
        public const string ReplacementLedger = "Docs/WorldRemaster/WORLD_REPLACEMENT_LEDGER.csv";

        public const string VoidFillWestScene =
            "Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-4_0.unity";
        public const string VoidFillEastScene =
            "Assets/Game/World/Generated/VoidFillCells/VoidFill_cell_-3_0.unity";
        public const string VoidFillWestMesh =
            "Assets/Game/World/Production/Terrain/VoidFill/M05C1_TeimoVoidFill_cell_-4_0.asset";
        public const string VoidFillEastMesh =
            "Assets/Game/World/Production/Terrain/VoidFill/M05C1_TeimoVoidFill_cell_-3_0.asset";
    }

    public static class WorldValidationRunner
    {
        private const string ValidatorVersion = "05B.1";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static ValidationSnapshot cachedValidationSnapshot;

        public static WorldValidationResult ValidateAll() =>
            Validate(WorldValidationGate.None, string.Empty, forceValidatorRefresh: true);

        public static WorldValidationResult ValidateAllCached() =>
            Validate(WorldValidationGate.None, string.Empty, forceValidatorRefresh: false);

        public static WorldValidationResult ValidateGate(WorldValidationGate gate) =>
            Validate(gate, string.Empty, forceValidatorRefresh: true);

        public static WorldValidationResult ValidateZone(string zoneId, WorldValidationGate gate) =>
            Validate(gate, zoneId ?? string.Empty, forceValidatorRefresh: true);

        public static WorldValidationResult ValidateZoneCached(string zoneId, WorldValidationGate gate) =>
            Validate(gate, zoneId ?? string.Empty, forceValidatorRefresh: false);

        public static WorldValidationResult Validate(WorldValidationGate gate, string zoneId)
            => Validate(gate, zoneId, forceValidatorRefresh: true);

        private static WorldValidationResult Validate(
            WorldValidationGate gate,
            string zoneId,
            bool forceValidatorRefresh)
        {
            WorldProductionAssetRegistry registry =
                AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(WorldRemasterPaths.RegistryAsset);
            if (registry == null)
            {
                throw new InvalidOperationException("World production registry is missing: " + WorldRemasterPaths.RegistryAsset);
            }

            ValidationSnapshot validation = GetValidationSnapshot(forceValidatorRefresh);
            IReadOnlyList<WorldEntityPlacement> entities = WorldRemasterRegistryBuilder.LoadEntities();
            WorldEntityPlacement[] eligible = entities.Where(entity => entity.ReferenceWorldEligible).ToArray();
            HashSet<string> eligibleIds = eligible.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            WorldProductionAssetRecord[] mapped = registry.Records
                .Where(record => record.HasProductionReplacement && eligibleIds.Contains(record.StableWorldId))
                .ToArray();
            string[] supplementalIds = LoadSupplementalStableIds();
            WorldValidationDependencyAudit dependencyAudit = BuildDependencyAudit();
            WorldValidationCoverageRow[] coverage = BuildCoverage(
                entities,
                eligible,
                registry,
                mapped,
                supplementalIds,
                validation);
            WorldValidationSpatialRow[] spatial = BuildSpatialDeviation(entities, validation.StaticMetrics);
            WorldValidationPerformanceLocation[] performance = BuildPerformanceLocations(
                validation.StaticMetrics,
                eligible,
                mapped,
                validation);
            WorldValidationIssue[] issues = BuildIssues(
                entities,
                eligible,
                registry,
                mapped,
                supplementalIds,
                dependencyAudit,
                validation,
                spatial);
            WorldValidationGateResult[] gates = BuildGates(issues, eligible.Length, mapped.Length, registry, validation);

            var result = new WorldValidationResult
            {
                validatorVersion = ValidatorVersion,
                sourceDatabaseVersion = registry.SourceDatabaseVersion,
                scope = string.IsNullOrWhiteSpace(zoneId) ? "Project" : "Zone",
                evaluatedGate = gate.ToString(),
                selectedZone = zoneId,
                sourceRecordCount = entities.Count,
                eligibleWorldRecordCount = eligible.Length,
                productionBindingCount = mapped.Length,
                concreteCellCount = eligible
                    .Where(entity => entity.CellId != "global" && entity.CellId != "excluded")
                    .Select(entity => entity.CellId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                productionBoundCellCount = mapped
                    .Where(record => record.ProductionZone != "global" && record.ProductionZone != "excluded")
                    .Select(record => record.ProductionZone)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                supplementalSafetyPieceCount = supplementalIds.Length,
                approvedReplacementCount = registry.Records.Count(record =>
                    record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified),
                gates = gates,
                issues = issues,
                coverage = coverage,
                spatialDeviation = spatial,
                performanceLocations = performance,
                dependencyAudit = dependencyAudit,
                validatorRuns = validation.ValidatorRuns
            };
            result.achievedGate = result.HighestAchievedGate.ToString();

            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                result.issues = issues
                    .Where(issue => IssueAppliesToZone(issue, zoneId))
                    .ToArray();
                result.coverage = coverage
                    .Where(row => !string.Equals(row.scope, "Zone", StringComparison.Ordinal) ||
                                  string.Equals(row.scopeId, zoneId, StringComparison.Ordinal))
                    .ToArray();
                result.spatialDeviation = spatial
                    .Where(row => ZoneFieldApplies(row.zoneId, zoneId))
                    .ToArray();
                result.performanceLocations = performance
                    .Where(location => string.IsNullOrWhiteSpace(location.zoneId) ||
                                       string.Equals(location.zoneId, zoneId, StringComparison.Ordinal))
                    .ToArray();
                result.gates = BuildGates(result.issues, eligible.Length, mapped.Length, registry, validation);
                result.achievedGate = result.HighestAchievedGate.ToString();
            }

            return result;
        }

        [MenuItem("Tools/MSC Remake/World Validation/Run All And Export")]
        public static void RunAllAndExport()
        {
            WorldValidationResult result = ValidateAll();
            Export(result);
            Debug.Log(
                $"WORLD_VALIDATION_05B_COMPLETED achieved={result.achievedGate} " +
                $"bindings={result.productionBindingCount}/{result.eligibleWorldRecordCount} " +
                $"openIssues={result.issues.Count(issue => issue.IsOpen)}");
        }

        public static void RunBatch() => RunAllAndExport();

        public static void Export(WorldValidationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (!string.Equals(result.scope, "Project", StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(result.selectedZone) ||
                !string.Equals(result.evaluatedGate, WorldValidationGate.None.ToString(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Canonical world-validation files accept only a full Project result from ValidateAll().");
            }

            Directory.CreateDirectory(WorldValidationPaths.DocumentationRoot);
            AtomicWrite(WorldValidationPaths.MachineResult, JsonUtility.ToJson(result, true) + Environment.NewLine);
            AtomicWrite(WorldValidationPaths.Issues, BuildIssueCsv(result.issues));
            AtomicWrite(WorldValidationPaths.Coverage, BuildCoverageCsv(result.coverage));
            AtomicWrite(WorldValidationPaths.SpatialDeviation, BuildSpatialCsv(result.spatialDeviation));
            AssetDatabase.Refresh();
        }

        private static ValidationSnapshot GetValidationSnapshot(bool forceRefresh)
        {
            if (forceRefresh || cachedValidationSnapshot == null)
            {
                cachedValidationSnapshot = RunValidationSnapshot();
            }

            return cachedValidationSnapshot;
        }

        private static ValidationSnapshot RunValidationSnapshot()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("World validation was cancelled because modified scenes were not saved.");
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var production = new WorldRemasterValidationResult();
            var geometry = new WorldMapGeometryEvaluationValidationResult();
            var ground = new WorldContinuousGroundBaselineValidationResult();
            var transfer = new WorldTransferValidationResult();
            WorldRemasterStaticMetrics metrics = null;
            CellIdentityAudit identityAudit = null;
            bool productionStreamerWired = false;
            string streamingEvidence = string.Empty;
            string[] streamingErrors = Array.Empty<string>();
            string[] streamingWarnings = Array.Empty<string>();
            try
            {
                try
                {
                    production = ProductionCellValidationTool.Validate(writeReports: false);
                }
                catch (Exception exception)
                {
                    production = new WorldRemasterValidationResult();
                    production.AddError("Production validator threw: " + exception.Message);
                }

                try
                {
                    geometry = WorldMapGeometryEvaluationValidator.Validate(requireGeneratedScenes: true);
                }
                catch (Exception exception)
                {
                    geometry = new WorldMapGeometryEvaluationValidationResult();
                    geometry.Errors.Add("Geometry validator threw: " + exception.Message);
                }

                try
                {
                    ground = WorldContinuousGroundBaselineValidator.Validate();
                }
                catch (Exception exception)
                {
                    ground = new WorldContinuousGroundBaselineValidationResult();
                    ground.Errors.Add("Continuous-ground validator threw: " + exception.Message);
                }

                try
                {
                    transfer = WorldTransferValidator.Validate(requireGeneratedScenes: true);
                }
                catch (Exception exception)
                {
                    transfer = new WorldTransferValidationResult();
                    transfer.Errors.Add("World-transfer validator threw: " + exception.Message);
                }

                try
                {
                    metrics = ProductionCellValidationTool.MeasureStaticContent();
                }
                catch (Exception exception)
                {
                    metrics = new WorldRemasterStaticMetrics();
                    production.AddError("Static production metrics failed: " + exception.Message);
                }

                identityAudit = ValidateProductionCellIdentitySets();
                foreach (string error in identityAudit.Errors)
                {
                    production.AddError(error);
                }

                try
                {
                    WorldPilotGateRemediationValidationResult streamingValidation =
                        WorldPilotGateRemediationValidator.Validate(logResult: false);
                    productionStreamerWired = streamingValidation.Passed;
                    streamingEvidence = streamingValidation.Evidence;
                    streamingErrors = streamingValidation.Errors.ToArray();
                    streamingWarnings = streamingValidation.Warnings.ToArray();
                }
                catch (Exception exception)
                {
                    streamingEvidence = "PilotGate remediation validator threw: " + exception.Message;
                    streamingErrors = new[] { streamingEvidence };
                }
            }
            finally
            {
                if (originalSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
            }

            bool streamingLifecycleExecuted;
            bool streamingLifecyclePassed;
            string streamingLifecycleEvidence;
            string[] streamingLifecycleErrors;
            string[] streamingLifecycleWarnings;
            try
            {
                ProductionWorldStreamingLifecycleEvidenceReadResult lifecycle =
                    ProductionWorldStreamingLifecycleEvidenceReader.ReadAndValidate();
                streamingLifecycleExecuted = lifecycle.Executed;
                streamingLifecyclePassed = lifecycle.Passed;
                streamingLifecycleEvidence = lifecycle.Evidence;
                streamingLifecycleErrors = lifecycle.Errors;
                streamingLifecycleWarnings = lifecycle.Warnings;
            }
            catch (Exception exception)
            {
                streamingLifecycleExecuted = false;
                streamingLifecyclePassed = false;
                streamingLifecycleEvidence = "Production streaming lifecycle evidence reader threw: " + exception.Message;
                streamingLifecycleErrors = new[] { streamingLifecycleEvidence };
                streamingLifecycleWarnings = Array.Empty<string>();
            }

            bool traversalExecuted;
            bool traversalPassed;
            string traversalEvidence;
            string[] traversalErrors;
            string[] traversalWarnings;
            try
            {
                WorldPilotTraversalEvidenceReadResult traversal =
                    WorldPilotTraversalEvidenceReader.ReadAndValidate();
                traversalExecuted = traversal.Executed;
                traversalPassed = traversal.Passed;
                traversalEvidence = traversal.Evidence;
                traversalErrors = traversal.Errors;
                traversalWarnings = traversal.Warnings;
            }
            catch (Exception exception)
            {
                traversalExecuted = false;
                traversalPassed = false;
                traversalEvidence = "M4 traversal evidence reader threw: " + exception.Message;
                traversalErrors = new[] { traversalEvidence };
                traversalWarnings = Array.Empty<string>();
            }

            bool performanceExecuted;
            bool performancePassed;
            string performanceEvidence;
            string[] performanceErrors;
            string[] performanceWarnings;
            try
            {
                WorldPilotPerformanceEvidenceReadResult performance =
                    WorldPilotPerformanceEvidenceReader.ReadAndValidate();
                performanceExecuted = performance.Executed;
                performancePassed = performance.Passed;
                performanceEvidence = performance.Evidence;
                performanceErrors = performance.Errors;
                performanceWarnings = performance.Warnings;
            }
            catch (Exception exception)
            {
                performanceExecuted = false;
                performancePassed = false;
                performanceEvidence = "Bounded performance evidence reader threw: " + exception.Message;
                performanceErrors = new[] { performanceEvidence };
                performanceWarnings = Array.Empty<string>();
            }

            string[] transferErrors = transfer.Errors.ToArray();
            bool onlyKnownHashDrift = transferErrors.Length > 0 && transferErrors.All(IsKnownDonorHashDrift);
            var validatorRuns = new[]
            {
                ValidatorRun(
                    "production-cell",
                    production.Passed,
                    production.Passed ? "Pass" : "Fail",
                    $"errors={production.Errors.Count};warnings={production.Warnings.Count};renderers={metrics.RendererCount};colliders={metrics.ColliderCount};lodGroups={metrics.LodGroupCount};triangles={metrics.TriangleCount}",
                    production.Errors,
                    production.Warnings),
                ValidatorRun(
                    "geometry-evaluation",
                    geometry.IsValid,
                    geometry.IsValid ? "Pass" : "Fail",
                    $"eligible={geometry.EligibleEntityCount};cells={geometry.CellCount};meshes={geometry.UniqueMeshCount};actual={geometry.ActualMeshEntityCount};fallback={geometry.BoundsFallbackCount}",
                    geometry.Errors,
                    geometry.Warnings),
                ValidatorRun(
                    "continuous-ground",
                    ground.IsValid,
                    ground.IsValid ? "Pass" : "Fail",
                    $"regions={ground.RegionCount};pieces={ground.PieceCount};vertices={ground.GeometryVertexCount};triangles={ground.GeometryTriangleCount};seamPairs={ground.SeamVertexPairCount};raycasts={ground.ColliderRaycastHitCount}/{ground.ColliderRaycastSampleCount}",
                    ground.Errors,
                    ground.Warnings),
                ValidatorRun(
                    "world-transfer",
                    transfer.IsValid,
                    transfer.IsValid ? "Pass" : onlyKnownHashDrift ? "KnownProvenanceDrift" : "Fail",
                    $"entities={transfer.EntityCount};eligible={transfer.EligibleEntityCount};cells={transfer.CellCount}",
                    transfer.Errors,
                    transfer.Warnings),
                ValidatorRun(
                    "production-cell-identity",
                    identityAudit.Passed,
                    identityAudit.Passed ? "Pass" : "Fail",
                    identityAudit.Evidence,
                    identityAudit.Errors,
                    Array.Empty<string>()),
                ValidatorRun(
                    "production-streaming-wiring",
                    productionStreamerWired,
                    productionStreamerWired ? "Pass" : "MissingRequiredImplementation",
                    streamingEvidence,
                    streamingErrors,
                    streamingWarnings),
                ValidatorRun(
                    ProductionWorldStreamingLifecycleEvidenceReader.ValidatorId,
                    streamingLifecyclePassed,
                    streamingLifecycleExecuted ? streamingLifecyclePassed ? "Pass" : "Fail" : "NotExecuted",
                    streamingLifecycleEvidence,
                    streamingLifecycleErrors,
                    streamingLifecycleWarnings,
                    streamingLifecycleExecuted),
                ValidatorRun(
                    WorldPilotTraversalEvidenceReader.ValidatorId,
                    traversalPassed,
                    traversalExecuted ? traversalPassed ? "Pass" : "Fail" : "NotExecuted",
                    traversalEvidence,
                    traversalErrors,
                    traversalWarnings,
                    traversalExecuted),
                ValidatorRun(
                    WorldPilotPerformanceEvidenceReader.ValidatorId,
                    performancePassed,
                    performanceExecuted ? performancePassed ? "Pass" : "Fail" : "NotExecuted",
                    performanceEvidence,
                    performanceErrors,
                    performanceWarnings,
                    performanceExecuted)
            };

            return new ValidationSnapshot(
                production,
                geometry,
                ground,
                transfer,
                metrics,
                identityAudit,
                productionStreamerWired,
                streamingEvidence,
                validatorRuns);
        }

        private static CellIdentityAudit ValidateProductionCellIdentitySets()
        {
            var errors = new List<string>();
            ValidateProductionCellIdentitySet(
                WorldRemasterPaths.PilotCellScene,
                WorldRemasterPaths.PilotZoneId,
                expectedMappedRecords: 24,
                new[]
                {
                    "3be598c0aa9798dd8ab43e20f4a35e8f",
                    "504a5620f62904b2d93d7803efc4eecf",
                    "6f4c37ebb3d0b019392e9fe6a16da65d",
                    "bb26b42e9fe463fd254bc0478a56946d",
                    "c4bbad1ab8714ff807bee9195a77399a",
                    "fc6a437b97ea997ca03a5e8bad1ba9b7",
                    "ff8e5e6cb145461b84108d4f24eaee07"
                },
                errors);
            ValidateProductionCellIdentitySet(
                WorldRemasterPaths.NextZoneCellScene,
                WorldRemasterPaths.NextZoneId,
                expectedMappedRecords: 9,
                new[]
                {
                    "0b03b3508f6be68192698ac5f7ae7550",
                    "2d9650c25f6324b6367493f48c56c908",
                    "615645f7ce4d6ee6836547a00c3dedb9",
                    "68783d0b852bcffb1eced37581f4aff8",
                    "7201412942822b5b4724b5e72c74065f",
                    "d7b7b8ce9d3e448a346b5344e18c2023",
                    "dcd8f98c103fb4eb4640d2abb757d089",
                    "dd8587032e6c12708151d60bd881aec6"
                },
                errors);
            return new CellIdentityAudit(
                errors,
                "cell_0_-3 runtimeIds=7 mappedRecords=24;cell_0_-2 runtimeIds=8 mappedRecords=9");
        }

        private static void ValidateProductionCellIdentitySet(
            string scenePath,
            string zoneId,
            int expectedMappedRecords,
            IReadOnlyCollection<string> expectedIds,
            ICollection<string> errors)
        {
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
                GameObject[] roots = scene.GetRootGameObjects();
                WorldRemasterPilotMarker[] markers = roots
                    .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                    .Where(marker => string.Equals(marker.ZoneId, zoneId, StringComparison.Ordinal))
                    .ToArray();
                if (markers.Length != 1)
                {
                    errors.Add($"Production cell {zoneId} has {markers.Length} zone markers; expected 1.");
                }
                else if (markers[0].MappedReferenceRecordCount != expectedMappedRecords)
                {
                    errors.Add($"Production cell {zoneId} reports {markers[0].MappedReferenceRecordCount} mapped records; expected {expectedMappedRecords}.");
                }

                string[] actualIds = roots
                    .SelectMany(root => root.GetComponentsInChildren<StableEntityIdAuthoring>(true))
                    .Select(identity => identity.SerializedId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                string[] expected = expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                if (!actualIds.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    errors.Add(
                        $"Production cell {zoneId} runtime stable-ID set differs from the reviewed contract: " +
                        $"actual={string.Join(";", actualIds)} expected={string.Join(";", expected)}");
                }

                if (actualIds.Any(id => !StableEntityId.TryParse(id, out _)))
                {
                    errors.Add("Production cell " + zoneId + " contains a non-canonical runtime stable ID.");
                }
            }
            catch (Exception exception)
            {
                errors.Add("Production cell identity audit failed for " + zoneId + ": " + exception.Message);
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static bool IsKnownDonorHashDrift(string error) =>
            error.StartsWith("Donor source hash differs from database provenance: sharedassets3.", StringComparison.Ordinal);

        private static WorldValidationValidatorRun ValidatorRun(
            string id,
            bool passed,
            string status,
            string evidence,
            IEnumerable<string> errors,
            IEnumerable<string> warnings,
            bool executed = true)
        {
            string[] errorArray = errors?.ToArray() ?? Array.Empty<string>();
            string[] warningArray = warnings?.ToArray() ?? Array.Empty<string>();
            return new WorldValidationValidatorRun
            {
                validatorId = id,
                executed = executed,
                passed = passed,
                status = status,
                errorCount = errorArray.Length,
                warningCount = warningArray.Length,
                evidence = evidence,
                errors = errorArray,
                warnings = warningArray
            };
        }

        public static WorldValidationDependencyAudit BuildDependencyAudit()
        {
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { WorldRemasterPaths.ProductionRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsAssetFile(path))
                {
                    seeds.Add(path);
                }
            }

            AddIfAssetExists(seeds, WorldRemasterPaths.PilotCellScene);
            AddIfAssetExists(seeds, WorldRemasterPaths.NextZoneCellScene);
            AddIfAssetExists(seeds, WorldValidationPaths.VoidFillWestScene);
            AddIfAssetExists(seeds, WorldValidationPaths.VoidFillEastScene);

            string[] buildScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            foreach (string scene in buildScenes)
            {
                AddIfAssetExists(seeds, scene);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queued = new HashSet<string>(seeds, StringComparer.Ordinal);
            var violations = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(seeds.OrderBy(path => path, StringComparer.Ordinal));
            int edgeCount = 0;
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (string dependency in AssetDatabase.GetDependencies(current, false)
                             .Where(path => !string.Equals(path, current, StringComparison.Ordinal))
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    edgeCount++;
                    if (IsForbiddenProductionDependency(dependency))
                    {
                        violations.Add(current + " -> " + dependency);
                    }

                    if (dependency.StartsWith("Assets/", StringComparison.Ordinal) &&
                        !visited.Contains(dependency) &&
                        queued.Add(dependency))
                    {
                        queue.Enqueue(dependency);
                    }
                }
            }

            foreach (string scene in buildScenes.Where(IsReferenceOnlyPath))
            {
                violations.Add("Enabled reference-only build scene: " + scene);
            }

            return new WorldValidationDependencyAudit
            {
                seedAssets = seeds.Count,
                visitedAssets = visited.Count,
                dependencyEdges = edgeCount,
                violations = violations.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                enabledBuildScenes = buildScenes,
                editorAssemblyPlayerAuditAvailable = false
            };
        }

        public static string[] LoadSupplementalStableIds()
        {
            string absolute = Path.GetFullPath(WorldValidationPaths.SupplementalVoidRegions);
            if (!File.Exists(absolute))
            {
                return Array.Empty<string>();
            }

            using var reader = new StringReader(File.ReadAllText(absolute, Encoding.UTF8));
            string headerLine = reader.ReadLine();
            if (headerLine == null)
            {
                return Array.Empty<string>();
            }

            List<string> headers = WorldEntityTable.ParseRow(headerLine);
            int stableIdIndex = headers.IndexOf("StableEntityId");
            if (stableIdIndex < 0)
            {
                throw new FormatException("M05C1 region CSV has no StableEntityId column.");
            }

            var ids = new List<string>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException("M05C1 region CSV row width does not match its header.");
                }

                ids.Add(values[stableIdIndex]);
            }

            return ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        private static WorldValidationCoverageRow[] BuildCoverage(
            IReadOnlyList<WorldEntityPlacement> entities,
            IReadOnlyList<WorldEntityPlacement> eligible,
            WorldProductionAssetRegistry registry,
            IReadOnlyList<WorldProductionAssetRecord> mapped,
            IReadOnlyList<string> supplementalIds,
            ValidationSnapshot validation)
        {
            HashSet<string> sourceIds = entities.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> mappedIds = mapped.Select(record => record.StableWorldId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> eligibleIds = eligible.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> allMappedIds = registry.Records
                .Where(record => record.HasProductionReplacement && sourceIds.Contains(record.StableWorldId))
                .Select(record => record.StableWorldId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> approvedEligibleIds = registry.Records
                .Where(record => eligibleIds.Contains(record.StableWorldId) &&
                                 record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified)
                .Select(record => record.StableWorldId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> approvedAllIds = registry.Records
                .Where(record => sourceIds.Contains(record.StableWorldId) &&
                                 record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified)
                .Select(record => record.StableWorldId)
                .ToHashSet(StringComparer.Ordinal);
            var rows = new List<WorldValidationCoverageRow>();

            foreach (IGrouping<string, WorldEntityPlacement> group in eligible
                         .GroupBy(entity => entity.Category, StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                int covered = group.Count(entity => mappedIds.Contains(entity.StableId));
                int approved = group.Count(entity => approvedEligibleIds.Contains(entity.StableId));
                rows.Add(CoverageRow(
                    "EligibleCategory",
                    group.Key,
                    group.Count(),
                    covered,
                    approved,
                    fallbacks: 0,
                    status: covered == group.Count() ? "Covered" : covered > 0 ? "Partial" : "Missing",
                    notes: "Direct stable-ID production replacement coverage over ReferenceWorldEligible records."));
            }

            foreach (IGrouping<string, WorldEntityPlacement> group in eligible
                         .Where(entity => entity.CellId != "excluded")
                         .GroupBy(entity => entity.CellId, StringComparer.Ordinal)
                         .OrderBy(group => ZoneSortKey(group.Key), StringComparer.Ordinal))
            {
                int covered = group.Count(entity => mappedIds.Contains(entity.StableId));
                int approved = group.Count(entity => approvedEligibleIds.Contains(entity.StableId));
                rows.Add(CoverageRow(
                    "Zone",
                    group.Key,
                    group.Count(),
                    covered,
                    approved,
                    fallbacks: 0,
                    status: covered == group.Count() ? "Covered" : covered > 0 ? "Partial" : "Missing",
                    notes: "Direct stable-ID bindings; project-authored supplemental safety pieces are reported separately."));
            }

            rows.Add(CoverageRow(
                "Universe",
                "CanonicalDonorInventory",
                entities.Count,
                allMappedIds.Count,
                approved: approvedAllIds.Count,
                fallbacks: 0,
                status: allMappedIds.SetEquals(sourceIds) ? "Covered" : allMappedIds.Count > 0 ? "Partial" : "Missing",
                notes: $"Includes {entities.Count - eligible.Count} classified non-world/excluded geometry records."));
            rows.Add(CoverageRow(
                "Universe",
                "ReferenceWorldEligible",
                eligible.Count,
                mapped.Count,
                approved: approvedEligibleIds.Count,
                fallbacks: validation.Geometry.BoundsFallbackCount,
                status: mapped.Count == eligible.Count ? "Covered" : mapped.Count > 0 ? "Partial" : "Missing",
                notes: "Primary FullWorld replacement denominator."));
            rows.Add(CoverageRow(
                "ReferenceRepresentation",
                "ActualMesh",
                eligible.Count,
                validation.Geometry.ActualMeshEntityCount,
                approved: 0,
                fallbacks: validation.Geometry.BoundsFallbackCount,
                status: "ReferenceOnly",
                notes: "05C reference geometry representation, not production coverage."));
            rows.Add(CoverageRow(
                "Supplemental",
                "M05C1SafetyTopology",
                supplementalIds.Count,
                supplementalIds.Count,
                approved: supplementalIds.Count,
                fallbacks: 0,
                status: "ApprovedBoundedPilot",
                notes: "Project-owned safety topology; excluded from donor replacement percentages and current Build Settings."));
            HashSet<string> concreteCellIds = eligible
                .Where(entity => entity.CellId != "global" && entity.CellId != "excluded")
                .Select(entity => entity.CellId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> boundCellIds = mapped
                .Select(record => record.ProductionZone)
                .Where(zone => zone != "global" && zone != "excluded")
                .ToHashSet(StringComparer.Ordinal);
            rows.Add(CoverageRow(
                "System",
                "ProductionBoundCells",
                concreteCellIds.Count,
                boundCellIds.Count,
                approved: 0,
                fallbacks: 0,
                status: boundCellIds.SetEquals(concreteCellIds) ? "Covered" : boundCellIds.Count > 0 ? "Partial" : "Missing",
                notes: "Direct donor-record production bindings in: " + string.Join(
                    ";",
                    mapped.Select(record => record.ProductionZone)
                        .Where(zone => zone != "global" && zone != "excluded")
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(zone => zone, StringComparer.Ordinal))));
            rows.Add(CoverageRow(
                "System",
                "ApprovedOrVerifiedReplacements",
                eligible.Count,
                approvedEligibleIds.Count,
                approved: approvedEligibleIds.Count,
                fallbacks: 0,
                status: approvedEligibleIds.SetEquals(eligibleIds)
                    ? "Covered"
                    : approvedEligibleIds.Count > 0 ? "Partial" : "Missing",
                notes: "FirstPass and ProductionCandidate are not promoted to Approved/Verified."));
            rows.Add(CountOnlyRow("ObservedContent", "ProductionRenderers", validation.StaticMetrics.RendererCount, "Fresh production-prefab scan."));
            rows.Add(CountOnlyRow("ObservedContent", "ProductionColliders", validation.StaticMetrics.ColliderCount, "Fresh production-prefab scan."));
            rows.Add(CountOnlyRow("ObservedContent", "ProductionLodGroups", validation.StaticMetrics.LodGroupCount, "Fresh production-prefab scan."));
            rows.Add(CountOnlyRow("ObservedContent", "InstanceTriangles", validation.StaticMetrics.TriangleCount, "Fresh instance-counted mesh scan."));

            return rows
                .OrderBy(row => CoverageScopeOrder(row.scope))
                .ThenBy(row => row.scopeId, StringComparer.Ordinal)
                .ToArray();
        }

        private static WorldValidationSpatialRow[] BuildSpatialDeviation(
            IReadOnlyList<WorldEntityPlacement> entities,
            WorldRemasterStaticMetrics metrics)
        {
            var rows = new List<WorldValidationSpatialRow>();
            WorldEntityPlacement garageOrigin = entities.FirstOrDefault(entity =>
                string.Equals(entity.StableId, "fb0f962be1b325cc19296c66751818c0", StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(garageOrigin.StableId))
            {
                float originDeviation = Vector3.Distance(Vector3.zero, garageOrigin.Position);
                rows.Add(Spatial(
                    "WORLD-ORIGIN-GARAGE",
                    "WorldOrigin",
                    "",
                    garageOrigin.StableId,
                    "0|0|0",
                    Vector(garageOrigin.Position),
                    originDeviation,
                    0.005f,
                    originDeviation <= 0.005f ? "MeasuredPass" : "MeasuredFail",
                    "04A1 coordinate-conversion fixture",
                    "Serialized transform conversion only; not a production mesh-fidelity claim."));
            }
            else
            {
                rows.Add(UnavailableSpatial(
                    "WORLD-ORIGIN-GARAGE",
                    "WorldOrigin",
                    string.Empty,
                    "Durable garage-origin record is missing."));
            }

            MeasureProductionCellAnchors(rows);
            MeasureGarageDoorCenters(rows, entities);
            MeasureVoidFillSeam(rows);

            if (float.IsNaN(metrics.HomePierSeamOverlapMeters))
            {
                rows.Add(UnavailableSpatial(
                    "HOME-PIER-SEAM-OVERLAP",
                    "TerrainSeam",
                    WorldRemasterPaths.NextZoneId,
                    "Pilot terrain or FootpathToPier fixture is missing."));
            }
            else
            {
                rows.Add(Spatial(
                    "HOME-PIER-SEAM-OVERLAP",
                    "TerrainSeam",
                    WorldRemasterPaths.NextZoneId,
                    string.Empty,
                    ">=1.0m overlap",
                    metrics.HomePierSeamOverlapMeters.ToString("0.######", Invariant) + "m overlap",
                    Mathf.Max(0f, 1f - metrics.HomePierSeamOverlapMeters),
                    0f,
                    metrics.HomePierSeamOverlapMeters >= 1f ? "MeasuredPass" : "MeasuredFail",
                    "Fresh ProductionCellValidationTool geometry fixture",
                    "Deviation records shortfall below the required overlap; zero means the minimum is met."));
            }

            if (float.IsNaN(metrics.PierMaximumColliderGapMeters))
            {
                rows.Add(UnavailableSpatial(
                    "PIER-COLLIDER-GAP",
                    "CollisionContinuity",
                    WorldRemasterPaths.NextZoneId,
                    "Pier deck collider fixture is missing."));
            }
            else
            {
                rows.Add(Spatial(
                    "PIER-COLLIDER-GAP",
                    "CollisionContinuity",
                    WorldRemasterPaths.NextZoneId,
                    string.Empty,
                    "<=0.10m",
                    metrics.PierMaximumColliderGapMeters.ToString("0.######", Invariant) + "m",
                    metrics.PierMaximumColliderGapMeters,
                    0.10f,
                    metrics.PierMaximumColliderGapMeters <= 0.10f ? "MeasuredPass" : "MeasuredFail",
                    "Fresh ProductionCellValidationTool geometry fixture",
                    "Maximum neighbouring plank-collider gap."));
            }

            AddUnavailableSpatialRows(rows);
            AddSpatialAggregates(rows);
            return rows.OrderBy(row => row.metricId, StringComparer.Ordinal).ToArray();
        }

        private static void MeasureProductionCellAnchors(ICollection<WorldValidationSpatialRow> rows)
        {
            MeasureCellAnchor(
                rows,
                WorldRemasterPaths.PilotCellScene,
                WorldRemasterPaths.PilotZoneId,
                WorldRemasterPaths.HomeGarageAnchor,
                0.01f,
                "PRODUCTION-HOME-ROOT");
            MeasureCellAnchor(
                rows,
                WorldRemasterPaths.NextZoneCellScene,
                WorldRemasterPaths.NextZoneId,
                WorldRemasterPaths.HomePierAnchor,
                0.05f,
                "PRODUCTION-PIER-ROOT");

            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(WorldRemasterPaths.NextZoneCellScene);
                WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                    .First(candidate => candidate.ZoneId == WorldRemasterPaths.NextZoneId);
                string[] hedgeIds =
                {
                    "b8de7336e204fae3ba333227b3e94d19",
                    "449b18de0c10f87887e3f3304a90366e",
                    "847f56ce8c1be238f4bcae514bb55fdf"
                };
                for (int index = 0; index < hedgeIds.Length; index++)
                {
                    Transform hedge = marker.GetComponentsInChildren<Transform>(true)
                        .First(candidate => candidate.name == "Hedge_" + hedgeIds[index]);
                    float deviation = Vector3.Distance(hedge.position, WorldRemasterPaths.HomeHedgeAnchors[index]);
                    rows.Add(Spatial(
                        "PRODUCTION-HEDGE-ANCHOR-" + (index + 1).ToString(Invariant),
                        "BuildingAnchor",
                        WorldRemasterPaths.NextZoneId,
                        hedgeIds[index],
                        Vector(WorldRemasterPaths.HomeHedgeAnchors[index]),
                        Vector(hedge.position),
                        deviation,
                        0.05f,
                        deviation <= 0.05f ? "MeasuredPass" : "MeasuredFail",
                        WorldRemasterPaths.NextZoneCellScene,
                        "Exact converted anchor fixture."));
                }
            }
            catch (Exception exception)
            {
                rows.Add(UnavailableSpatial(
                    "PRODUCTION-HEDGE-ANCHORS",
                    "BuildingAnchor",
                    WorldRemasterPaths.NextZoneId,
                    "Preview-scene measurement failed: " + exception.Message));
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void MeasureCellAnchor(
            ICollection<WorldValidationSpatialRow> rows,
            string scenePath,
            string zoneId,
            Vector3 expected,
            float tolerance,
            string metricId)
        {
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
                WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                    .First(candidate => candidate.ZoneId == zoneId);
                float deviation = Vector3.Distance(expected, marker.transform.position);
                rows.Add(Spatial(
                    metricId,
                    "ProductionAnchor",
                    zoneId,
                    string.Empty,
                    Vector(expected),
                    Vector(marker.transform.position),
                    deviation,
                    tolerance,
                    deviation <= tolerance ? "MeasuredPass" : "MeasuredFail",
                    scenePath,
                    "Production-cell root position."));
            }
            catch (Exception exception)
            {
                rows.Add(UnavailableSpatial(metricId, "ProductionAnchor", zoneId, exception.Message));
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void MeasureGarageDoorCenters(
            ICollection<WorldValidationSpatialRow> rows,
            IReadOnlyList<WorldEntityPlacement> entities)
        {
            Dictionary<string, WorldEntityPlacement> byId = entities.ToDictionary(entity => entity.StableId, StringComparer.Ordinal);
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(WorldRemasterPaths.PilotCellScene);
                WorldRemasterPilotMarker marker = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                    .First(candidate => candidate.ZoneId == WorldRemasterPaths.PilotZoneId);
                MeasureDoor(
                    rows,
                    marker,
                    byId["250d1cb74558e7c7e27e5e860981c938"],
                    "GarageDoorLeft",
                    "PRODUCTION-GARAGE-DOOR-LEFT");
                MeasureDoor(
                    rows,
                    marker,
                    byId["e8660bda40e1d2946e004456e245c803"],
                    "GarageDoorRight",
                    "PRODUCTION-GARAGE-DOOR-RIGHT");
            }
            catch (Exception exception)
            {
                rows.Add(UnavailableSpatial(
                    "PRODUCTION-GARAGE-DOORS",
                    "MovingArchitecture",
                    WorldRemasterPaths.PilotZoneId,
                    exception.Message));
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void MeasureDoor(
            ICollection<WorldValidationSpatialRow> rows,
            WorldRemasterPilotMarker marker,
            WorldEntityPlacement expected,
            string name,
            string metricId)
        {
            WorldHingedArchitecture hinge = marker.GetComponentsInChildren<WorldHingedArchitecture>(true)
                .First(candidate => candidate.name == name);
            Renderer renderer = hinge.GetComponentInChildren<Renderer>(true);
            float deviation = Vector3.Distance(expected.Bounds.center, renderer.bounds.center);
            rows.Add(Spatial(
                metricId,
                "MovingArchitecture",
                WorldRemasterPaths.PilotZoneId,
                expected.StableId,
                Vector(expected.Bounds.center),
                Vector(renderer.bounds.center),
                deviation,
                0.35f,
                deviation <= 0.35f ? "MeasuredPass" : "MeasuredFail",
                WorldRemasterPaths.PilotCellScene,
                "Renderer world-bounds center versus durable donor-layout bounds center."));
        }

        private static void MeasureVoidFillSeam(ICollection<WorldValidationSpatialRow> rows)
        {
            Mesh west = AssetDatabase.LoadAssetAtPath<Mesh>(WorldValidationPaths.VoidFillWestMesh);
            Mesh east = AssetDatabase.LoadAssetAtPath<Mesh>(WorldValidationPaths.VoidFillEastMesh);
            if (west == null || east == null || west.vertexCount != east.vertexCount || west.vertexCount % 2 != 0)
            {
                rows.Add(UnavailableSpatial(
                    "M05C1-VOID-CELL-SEAM",
                    "TerrainSeam",
                    "cell_-4_0;cell_-3_0",
                    "Generated void-fill meshes are missing or structurally incompatible."));
                return;
            }

            Vector3[] westVertices = west.vertices;
            Vector3[] eastVertices = east.vertices;
            float maximum = 0f;
            for (int row = 0; row < west.vertexCount / 2; row++)
            {
                Vector3 westPoint = westVertices[row * 2 + 1] + new Vector3(-2048f, 0f, 0f);
                Vector3 eastPoint = eastVertices[row * 2] + new Vector3(-1536f, 0f, 0f);
                maximum = Mathf.Max(maximum, Vector3.Distance(westPoint, eastPoint));
            }

            rows.Add(Spatial(
                "M05C1-VOID-CELL-SEAM",
                "TerrainSeam",
                "cell_-4_0;cell_-3_0",
                string.Empty,
                "matching world-space seam vertices",
                (west.vertexCount / 2).ToString(Invariant) + " seam pairs",
                maximum,
                0.0001f,
                maximum <= 0.0001f ? "MeasuredPass" : "MeasuredFail",
                WorldValidationPaths.VoidFillWestMesh + ";" + WorldValidationPaths.VoidFillEastMesh,
                "Project-owned safety topology; not donor replacement coverage."));
        }

        private static void AddUnavailableSpatialRows(ICollection<WorldValidationSpatialRow> rows)
        {
            rows.Add(UnavailableSpatial("FULL-TERRAIN-ELEVATION", "Terrain", string.Empty, "No production heightfield parity samples exist."));
            rows.Add(UnavailableSpatial("FULL-ROAD-CENTERLINE", "Road", string.Empty, "Production road centerline/width/elevation samples do not exist; Road bindings are 0/29."));
            rows.Add(UnavailableSpatial("FULL-JUNCTION-TOPOLOGY", "Road", string.Empty, "No junction graph or production junction fixtures exist."));
            rows.Add(UnavailableSpatial("FULL-SHORELINE-WATER-LEVEL", "Water", string.Empty, "Bounded shoreline presentation exists, but donor shoreline/elevation parity is unmeasured."));
            rows.Add(UnavailableSpatial("FULL-BUILDING-FOOTPRINTS", "Building", string.Empty, "Only bounded home anchors/garage doors are measured."));
            rows.Add(UnavailableSpatial("FULL-INTERIOR-FLOOR-LEVELS", "Interior", string.Empty, "Room/floor/portal parity is unmeasured."));
        }

        private static void AddSpatialAggregates(ICollection<WorldValidationSpatialRow> rows)
        {
            float[] measured = rows
                .Where(row => string.Equals(row.status, "MeasuredPass", StringComparison.Ordinal) ||
                              string.Equals(row.status, "MeasuredFail", StringComparison.Ordinal))
                .Where(row => !string.Equals(row.metricId, "HOME-PIER-SEAM-OVERLAP", StringComparison.Ordinal))
                .Select(row => row.deviationMeters)
                .ToArray();
            if (measured.Length == 0)
            {
                return;
            }

            float maximum = measured.Max();
            float average = measured.Average();
            float p95 = WorldValidationGateCalculator.Percentile95(measured);
            rows.Add(Spatial("SPATIAL-AGG-MAX", "Aggregate", string.Empty, string.Empty, "n/a", maximum.ToString("0.######", Invariant), maximum, 0f, "MeasuredStatistic", "05B measured fixture set", "Maximum over available metric deviations only."));
            rows.Add(Spatial("SPATIAL-AGG-MEAN", "Aggregate", string.Empty, string.Empty, "n/a", average.ToString("0.######", Invariant), average, 0f, "MeasuredStatistic", "05B measured fixture set", "Mean over available metric deviations only."));
            rows.Add(Spatial("SPATIAL-AGG-P95", "Aggregate", string.Empty, string.Empty, "n/a", p95.ToString("0.######", Invariant), p95, 0f, "MeasuredStatistic", "05B measured fixture set", "Nearest-rank p95 over available metric deviations only."));
        }

        private static WorldValidationPerformanceLocation[] BuildPerformanceLocations(
            WorldRemasterStaticMetrics metrics,
            IReadOnlyList<WorldEntityPlacement> eligible,
            IReadOnlyList<WorldProductionAssetRecord> mapped,
            ValidationSnapshot validation)
        {
            HashSet<string> mappedIds = mapped.Select(record => record.StableWorldId).ToHashSet(StringComparer.Ordinal);
            WorldEntityPlacement[] roadRecords = eligible
                .Where(entity => string.Equals(entity.Category, "Road", StringComparison.Ordinal))
                .ToArray();
            int mappedRoads = roadRecords.Count(record => mappedIds.Contains(record.StableId));
            bool measured = validation.ValidatorRuns.Any(run =>
                string.Equals(run.validatorId, WorldPilotPerformanceEvidenceReader.ValidatorId, StringComparison.Ordinal) &&
                run.executed &&
                run.passed);
            return new[]
            {
                Performance(
                    "pilot-home",
                    "cell_0_-3",
                    measured ? "MeasuredBounded" : "StaticOnly",
                    measured
                        ? "frame mean/p95/worst=3.014/3.620/3.894 ms;CPU p95=3.430 ms;GPU p95=3.329 ms (239 samples);Draw/Batches/SetPass mean=225.97/168.96/26.98"
                        : $"{metrics.PilotRendererCount} renderers;{metrics.PilotColliderCount} colliders;{metrics.PilotLodGroupCount} LOD groups;{metrics.PilotTriangleCount} triangles (fresh prefab scan)",
                    measured ? "resident VRAM;isolated Present;physics counter;validated streaming spike" : "current-world CPU/GPU frame time;memory;draw calls;VRAM;frame pacing",
                    measured ? WorldPilotPerformanceEvidenceReader.EvidencePath : "Docs/WorldRemaster/WORLD_REMASTER_PERFORMANCE_REPORT.md"),
                Performance(
                    "dense-vegetation",
                    "cell_0_-3",
                    measured ? "MeasuredBounded" : "StaticOnly",
                    measured
                        ? "frame mean/p95/worst=3.223/3.863/4.266 ms;CPU p95=3.669 ms;GPU p95=3.475 ms (261 samples);Draw/Batches/SetPass mean=223.03/193.02/27.99"
                        : $"{metrics.PilotLodGroupCount} pilot LOD groups present; tree/other split is not inferred",
                    measured ? "resident VRAM;isolated Present;physics counter;validated streaming spike" : "CPU/GPU cost;instancing batches;shadow cost",
                    measured ? WorldPilotPerformanceEvidenceReader.EvidencePath : "Assets/Game/World/Production/Prefabs/WR_PilotVegetation.prefab"),
                Performance(
                    "interior-transition",
                    "cell_0_-3",
                    measured ? "MeasuredBounded" : "StaticOnly",
                    measured
                        ? "frame mean/p95/worst=3.296/4.003/4.345 ms;CPU p95=3.737 ms;GPU p95=3.444 ms (284 samples);Draw/Batches/SetPass mean=231.97/214.95/30.99"
                        : "representative garage/living/kitchen/sauna slice present",
                    measured ? "resident VRAM;isolated Present;physics counter;validated streaming spike;lighting readability" : "exposure transition;CPU/GPU;visible set;memory",
                    measured ? WorldPilotPerformanceEvidenceReader.EvidencePath : "Assets/Game/World/Production/Prefabs/WR_HomeInteriorSlice.prefab"),
                Performance("road-driving-speed", "", "Unavailable", "bounded road strip only", "driving-speed frame time;streaming spikes;route frame pacing", $"Road bindings {mappedRoads}/{roadRecords.Length}; no validated drivable simulation route"),
                Performance(
                    "water-shoreline",
                    "cell_0_-2",
                    measured ? "MeasuredBounded" : "StaticOnly",
                    measured
                        ? "frame mean/p95/worst=3.131/3.803/4.001 ms;CPU p95=3.570 ms;GPU p95=3.442 ms (259 samples);Draw/Batches/SetPass mean=57.00/45.00/28.00"
                        : $"{metrics.NextZoneRendererCount} renderers;{metrics.NextZoneColliderCount} colliders;{metrics.NextZoneLodGroupCount} LOD groups;{metrics.NextZoneTriangleCount} triangles (fresh prefab scan)",
                    measured ? "resident VRAM;isolated Present;physics counter;validated streaming spike;underwater/weather cost" : "CPU/GPU water cost;VRAM;underwater/weather cost",
                    measured ? WorldPilotPerformanceEvidenceReader.EvidencePath : "Docs/Milestones/MILESTONE_05A_BATCH_01_CELL_0_-2.md"),
                Performance(
                    "most-expensive-production-cell",
                    metrics.PilotTriangleCount >= metrics.NextZoneTriangleCount ? "cell_0_-3" : "cell_0_-2",
                    measured ? "PartialMeasured" : "StaticOnly",
                    measured
                        ? $"steady-state locations captured;fresh triangle scan: cell_0_-3={metrics.PilotTriangleCount};cell_0_-2={metrics.NextZoneTriangleCount}"
                        : $"fresh triangle scan: cell_0_-3={metrics.PilotTriangleCount};cell_0_-2={metrics.NextZoneTriangleCount}",
                    "validated load spike;peak/recovered memory;most-expensive-cell proof",
                    measured ? WorldPilotPerformanceEvidenceReader.EvidencePath : "Only two production-bound cells exist"),
                Performance("vertical-slice-route", "", "Unavailable", "no continuous production route to a service destination", "all runtime performance metrics", "WORLD-ROAD-001")
            };
        }

        private static WorldValidationIssue[] BuildIssues(
            IReadOnlyList<WorldEntityPlacement> entities,
            IReadOnlyList<WorldEntityPlacement> eligible,
            WorldProductionAssetRegistry registry,
            IReadOnlyList<WorldProductionAssetRecord> mapped,
            IReadOnlyList<string> supplementalIds,
            WorldValidationDependencyAudit dependencyAudit,
            ValidationSnapshot validation,
            IReadOnlyList<WorldValidationSpatialRow> spatial)
        {
            HashSet<string> sourceIds = entities.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> eligibleIds = eligible.Select(entity => entity.StableId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> mappedIds = mapped.Select(record => record.StableWorldId).ToHashSet(StringComparer.Ordinal);
            bool sourceRegistryParity = registry.Records.Count == entities.Count &&
                                        registry.Records.Select(record => record.StableWorldId)
                                            .ToHashSet(StringComparer.Ordinal)
                                            .SetEquals(sourceIds);
            bool supplementsUnique = supplementalIds.Count == supplementalIds.Distinct(StringComparer.Ordinal).Count() &&
                                     supplementalIds.All(id => !sourceIds.Contains(id));
            int ledgerRows = CountCsvDataRows(WorldValidationPaths.ReplacementLedger);
            bool ledgerNormalized = ledgerRows == entities.Count;
            string defects = File.Exists(Path.GetFullPath(WorldValidationPaths.GeometryDefects))
                ? File.ReadAllText(Path.GetFullPath(WorldValidationPaths.GeometryDefects), Encoding.UTF8)
                : string.Empty;
            bool manualGeometrySynced = defects.Contains("ClosedManualReview", StringComparison.Ordinal) &&
                                        defects.Contains("AcceptedManualReview", StringComparison.Ordinal);
            WorldProductionZoneRecord pilot = registry.Zones.First(zone => zone.ZoneId == WorldRemasterPaths.PilotZoneId);
            bool pilotManualMetadataSynced = pilot.ManualValidation.Contains("DoorGatePass", StringComparison.Ordinal) &&
                                             pilot.ManualValidation.Contains("M4TraversalPass", StringComparison.Ordinal) &&
                                             pilot.ManualValidation.Contains("PerformancePending", StringComparison.Ordinal);
            bool fullPlayerTraversalValidated = validation.ValidatorRuns.Any(run =>
                string.Equals(run.validatorId, "m4-character-controller-traversal", StringComparison.Ordinal) &&
                run.executed &&
                run.passed);
            WorldValidationValidatorRun streamingLifecycleRun = validation.ValidatorRuns.FirstOrDefault(run =>
                string.Equals(
                    run.validatorId,
                    ProductionWorldStreamingLifecycleEvidenceReader.ValidatorId,
                    StringComparison.Ordinal));
            bool productionStreamingLifecycleValidated = streamingLifecycleRun != null &&
                                                          streamingLifecycleRun.executed &&
                                                          streamingLifecycleRun.passed;
            WorldValidationValidatorRun performanceRun = validation.ValidatorRuns.FirstOrDefault(run =>
                string.Equals(
                    run.validatorId,
                    WorldPilotPerformanceEvidenceReader.ValidatorId,
                    StringComparison.Ordinal));
            bool boundedPerformanceValidated = performanceRun != null &&
                                               performanceRun.executed &&
                                               performanceRun.passed;
            HashSet<string> approvedEligibleIds = registry.Records
                .Where(record => eligibleIds.Contains(record.StableWorldId) &&
                                 record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified)
                .Select(record => record.StableWorldId)
                .ToHashSet(StringComparer.Ordinal);
            int approvedOrVerified = approvedEligibleIds.Count;
            HashSet<string> concreteCellIds = eligible
                .Where(entity => entity.CellId != "global" && entity.CellId != "excluded")
                .Select(entity => entity.CellId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> boundCellIds = mapped
                .Select(record => record.ProductionZone)
                .Where(zone => zone != "global" && zone != "excluded")
                .ToHashSet(StringComparer.Ordinal);
            int concreteCells = concreteCellIds.Count;
            int boundCells = boundCellIds.Count;
            WorldEntityPlacement[] roadRecords = eligible
                .Where(entity => string.Equals(entity.Category, "Road", StringComparison.Ordinal))
                .ToArray();
            int mappedRoads = roadRecords.Count(record => mappedIds.Contains(record.StableId));
            bool roadCoverageComplete = roadRecords.Length > 0 && mappedRoads == roadRecords.Length;
            bool serviceDestinationBound = eligible.Any(entity =>
                mappedIds.Contains(entity.StableId) &&
                (entity.HierarchyPath.Contains("STORE", StringComparison.OrdinalIgnoreCase) ||
                 entity.HierarchyPath.Contains("TEIMO", StringComparison.OrdinalIgnoreCase)));
            bool fullCoverage = mappedIds.SetEquals(eligibleIds) &&
                                boundCellIds.SetEquals(concreteCellIds) &&
                                approvedEligibleIds.SetEquals(eligibleIds);
            bool lodCoverageComplete = fullCoverage && validation.StaticMetrics.LodGroupCount > 0;
            bool spatialCoverageComplete = spatial.All(row =>
                !string.Equals(row.status, "Unavailable", StringComparison.Ordinal) &&
                !string.Equals(row.status, "MeasuredFail", StringComparison.Ordinal));
            bool roadValidationComplete = roadCoverageComplete && serviceDestinationBound && spatial
                .Where(row => string.Equals(row.domain, "Road", StringComparison.Ordinal))
                .All(row => !string.Equals(row.status, "Unavailable", StringComparison.Ordinal) &&
                            !string.Equals(row.status, "MeasuredFail", StringComparison.Ordinal));
            string[] donorHashErrors = validation.Transfer.Errors.Where(IsKnownDonorHashDrift).ToArray();
            string[] transferStructuralErrors = validation.Transfer.Errors
                .Where(error => !IsKnownDonorHashDrift(error))
                .ToArray();
            bool collisionPolicyClassified = validation.StaticMetrics.ColliderCount > 0 &&
                                             validation.StaticMetrics.DefaultLayerColliderCount == 0 &&
                                             validation.StaticMetrics.NullPhysicsMaterialColliderCount == 0;
            bool voidFillRuntimeIntegrated = dependencyAudit.enabledBuildScenes.Contains(
                                                  WorldValidationPaths.VoidFillWestScene,
                                                  StringComparer.Ordinal) &&
                                              dependencyAudit.enabledBuildScenes.Contains(
                                                  WorldValidationPaths.VoidFillEastScene,
                                                  StringComparer.Ordinal);

            var issues = new List<WorldValidationIssue>
            {
                Issue(
                    "WORLD-ID-001",
                    sourceRegistryParity && supplementsUnique && ledgerNormalized ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "High",
                    "Identity",
                    sourceRegistryParity && supplementsUnique && ledgerNormalized ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Separate canonical donor IDs from project-owned 05C1 safety IDs.",
                    $"source={entities.Count};registry={registry.Records.Count};replacementLedger={ledgerRows};supplemental={supplementalIds.Count};setsValid={sourceRegistryParity && supplementsUnique}",
                    "Keep WORLD_REPLACEMENT_LEDGER at 13,509 donor records and track the two 05C1 pieces in their own manifest/porting ledger."),
                Issue(
                    "WORLD-ID-002",
                    donorHashErrors.Length == 0 ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Medium",
                    "Provenance",
                    donorHashErrors.Length == 0 ? string.Empty : "FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Current donor sharedassets3 containers differ from the frozen 04A1 extraction hashes.",
                    donorHashErrors.Length == 0
                        ? "WorldTransferValidator reports no donor source hash drift."
                        : string.Join(";", donorHashErrors),
                    "Run a separately approved audited re-extraction before rebasing provenance; do not rewrite frozen history in 05B."),
                Issue(
                    "WORLD-ID-003",
                    transferStructuralErrors.Length == 0 ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "IdentityValidation",
                    transferStructuralErrors.Length == 0 ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "The full WorldTransfer identity/generated-scene validator must complete without structural errors.",
                    transferStructuralErrors.Length == 0
                        ? $"entities={validation.Transfer.EntityCount};eligible={validation.Transfer.EligibleEntityCount};cells={validation.Transfer.CellCount}"
                        : string.Join(";", transferStructuralErrors),
                    "Fix only the reported deterministic data/identity defect; donor hash drift remains WORLD-ID-002."),
                Issue(
                    "WORLD-GEO-001",
                    fullCoverage ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Coverage",
                    fullCoverage ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Production replacement coverage is incomplete.",
                    $"eligible={mapped.Count}/{eligible.Count};cells={boundCells}/{concreteCells};approvedOrVerified={approvedOrVerified};unboundCells={concreteCells - boundCells}",
                    "Continue bounded production-zone replacement after the pilot gate is made operational."),
                Issue(
                    "WORLD-GEO-002",
                    spatialCoverageComplete ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "SpatialParity",
                    spatialCoverageComplete ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Full terrain, road, junction, shoreline, building and interior deviation sets are unavailable.",
                    "Only garage/pier/hedge anchors, garage-door centers, bounded seams and reference transform fixtures are measurable.",
                    "Author reviewed project-owned fixtures before claiming route or full-map spatial parity."),
                Issue(
                    "WORLD-GEO-003",
                    manualGeometrySynced ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Review",
                    "Metadata",
                    manualGeometrySynced ? string.Empty : "PilotGate",
                    string.Empty,
                    string.Empty,
                    "Synchronize 05C geometry findings with the accepted manual checklist.",
                    manualGeometrySynced ? "M05C-GEO-004=ClosedManualReview;M05C-GEO-005=AcceptedManualReview" : "05C status remains stale",
                    "Retain the accepted reference baseline without promoting it to production art."),
                Issue(
                    "WORLD-GEO-004",
                    validation.Geometry.BoundsFallbackCount == 0
                        ? WorldValidationIssueState.Closed
                        : WorldValidationIssueState.Open,
                    "Medium",
                    "ReferenceCoverage",
                    validation.Geometry.BoundsFallbackCount == 0 ? string.Empty : "FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "A material portion of reference geometry lacks object-level mesh/bounds fidelity.",
                    $"{validation.Geometry.BoundsFallbackCount}/{validation.Geometry.EligibleEntityCount} explicit bounds fallbacks reported by the fresh 05C validator",
                    "Resolve only gameplay-relevant records through separately audited mesh inspection."),
                Issue(
                    "WORLD-GEO-005",
                    validation.Production.Passed ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "ProductionValidation",
                    validation.Production.Passed ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "The production-cell validator and exact runtime-ID scene audit must pass.",
                    validation.Production.Passed
                        ? $"renderers={validation.StaticMetrics.RendererCount};colliders={validation.StaticMetrics.ColliderCount};lodGroups={validation.StaticMetrics.LodGroupCount};triangles={validation.StaticMetrics.TriangleCount}"
                        : string.Join(";", validation.Production.Errors),
                    "Fix the reported deterministic production-cell or identity-contract defect."),
                Issue(
                    "WORLD-GEO-006",
                    validation.Geometry.IsValid ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "ReferenceGeometryValidation",
                    validation.Geometry.IsValid ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "The 05C geometry evaluation validator must pass on the current generated reference baseline.",
                    validation.Geometry.IsValid
                        ? $"eligible={validation.Geometry.EligibleEntityCount};cells={validation.Geometry.CellCount};actual={validation.Geometry.ActualMeshEntityCount};fallback={validation.Geometry.BoundsFallbackCount}"
                        : string.Join(";", validation.Geometry.Errors),
                    "Repair only deterministic validation/generation defects; do not hide missing production coverage."),
                Issue(
                    "WORLD-GEO-007",
                    validation.Ground.IsValid ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "GroundValidation",
                    validation.Ground.IsValid ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    "cell_-4_0;cell_-3_0",
                    string.Empty,
                    "The bounded 05C1 seam/collision validator must pass.",
                    validation.Ground.IsValid
                        ? $"pieces={validation.Ground.PieceCount};seamPairs={validation.Ground.SeamVertexPairCount};raycasts={validation.Ground.ColliderRaycastHitCount}/{validation.Ground.ColliderRaycastSampleCount}"
                        : string.Join(";", validation.Ground.Errors),
                    "Fix only a regression in the accepted bounded safety-topology baseline."),
                Issue(
                    "WORLD-ROAD-001",
                    roadValidationComplete ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Road",
                    roadValidationComplete ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "No continuous production road graph or service route exists.",
                    $"Road bindings={mappedRoads}/{roadRecords.Length}; junction/width/elevation validation remains required.",
                    "Create and validate a bounded home-to-service production route in its owning world milestone."),
                Issue(
                    "WORLD-BLD-001",
                    serviceDestinationBound ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Buildings",
                    serviceDestinationBound ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "No production destination/service zone is available for the vertical slice.",
                    "Only home/garage and bounded home-shoreline content have production bindings.",
                    "Select and build the required service destination after the pilot gate remediation."),
                Issue(
                    "WORLD-COL-001",
                    roadValidationComplete ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Traversal",
                    roadValidationComplete ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "A continuous driveable production route is not validated.",
                    "Bounded road/driveway and 76m home-to-pier walkable raycast path exist; bridges and route driving do not.",
                    "Validate a vehicle-width swept fixture and actual driving only after Milestone 06 supplies a vehicle backend."),
                Issue(
                    "WORLD-COL-002",
                    fullPlayerTraversalValidated ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Traversal",
                    fullPlayerTraversalValidated ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    WorldRemasterPaths.PilotZoneId,
                    string.Empty,
                    "The full representative pilot traversal is not automated with the real M4 CharacterController.",
                    fullPlayerTraversalValidated
                        ? "Fingerprint-validated real M4 CharacterController evidence covers the authored exterior, garage, crouched house portal, representative interior and return route."
                        : "User accepted door/gate operation and the home-to-pier seam; automated tests use raycasts/overlap volumes, not a complete player walkthrough.",
                    fullPlayerTraversalValidated
                        ? "Retain the fingerprinted scene/player/route evidence and rerun the fixture after relevant content changes."
                        : "Add a deterministic real-player traversal fixture for the home/garage/interior route."),
                Issue(
                    "WORLD-COL-003",
                    collisionPolicyClassified ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Medium",
                    "CollisionPolicy",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Production world collision layers and PhysicMaterials are not classified.",
                    $"colliders={validation.StaticMetrics.ColliderCount};defaultLayer={validation.StaticMetrics.DefaultLayerColliderCount};nullPhysicMaterial={validation.StaticMetrics.NullPhysicsMaterialColliderCount}",
                    "Define surface/layer policy before vehicle tire and audio surface integration."),
                Issue(
                    "WORLD-COL-004",
                    pilotManualMetadataSynced ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Review",
                    "Metadata",
                    pilotManualMetadataSynced ? string.Empty : "PilotGate",
                    WorldRemasterPaths.PilotZoneId,
                    string.Empty,
                    "Synchronize pilot manual status with the recorded user smoke review.",
                    pilot.ManualValidation,
                    "Keep the automated M4 traversal pass and remaining performance work explicit."),
                Issue(
                    "WORLD-LOD-001",
                    lodCoverageComplete ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "High",
                    "ArtIntegrity",
                    lodCoverageComplete ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "LOD/HLOD coverage is limited to pilot trees and shoreline hedges.",
                    $"fresh LODGroup count={validation.StaticMetrics.LodGroupCount};eligible bindings={mapped.Count}/{eligible.Count};full category/HLOD evidence is absent",
                    "Profile and author category-specific LOD/HLOD in later production batches."),
                Issue(
                    "WORLD-STREAM-001",
                    validation.ProductionStreamerWired ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Streaming",
                    validation.ProductionStreamerWired ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "No production runtime streaming service is wired into Bootstrap or production scenes.",
                    validation.StreamingEvidence,
                    "Implement a bounded production streamer/composition-root integration in an explicitly approved remediation milestone."),
                Issue(
                    "WORLD-STREAM-002",
                    productionStreamingLifecycleValidated
                        ? WorldValidationIssueState.Closed
                        : WorldValidationIssueState.Open,
                    "High",
                    "ValidationTool",
                    productionStreamingLifecycleValidated
                        ? string.Empty
                        : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "The old load/unload fixture duplicated a cell already embedded in its playtest scene.",
                    productionStreamingLifecycleValidated
                        ? streamingLifecycleRun.evidence +
                          $" Preview identity audit passed={validation.CellIdentity.Passed}."
                        : streamingLifecycleRun?.evidence ??
                          "Production streaming lifecycle evidence has not been executed.",
                    "Regenerate fingerprinted two-cycle lifecycle evidence after relevant implementation or Bootstrap changes."),
                Issue(
                    "WORLD-STREAM-003",
                    WorldValidationIssueState.Open,
                    "High",
                    "Streaming",
                    "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Streaming memory recovery, persistence and stall limits are unmeasured.",
                    "No current-world load spike, memory before/peak/recovered, persistent landmark or state-preservation capture.",
                    "Measure these properties after a production streamer exists."),
                Issue(
                    "WORLD-STREAM-004",
                    boundCellIds.SetEquals(concreteCellIds) ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Streaming",
                    boundCellIds.SetEquals(concreteCellIds) ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Cross-cell production streaming contracts are incomplete.",
                    $"{boundCells}/{concreteCells} cells have direct production bindings; road/water/large-object/interior/persistent landmark contracts remain required.",
                    "Validate a continuous multi-cell route and ownership rules."),
                Issue(
                    "WORLD-STREAM-005",
                    voidFillRuntimeIntegrated ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "High",
                    "Streaming",
                    voidFillRuntimeIntegrated ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    "cell_-4_0;cell_-3_0",
                    string.Empty,
                    "Accepted 05C1 safety-fill scenes are not integrated into the runtime world.",
                    "Both scenes are intentionally absent from Build Settings and no production streamer registers them.",
                    "Integrate them only through the future production cell registry without broadening their bounded profile."),
                Issue(
                    "WORLD-PERF-001",
                    boundedPerformanceValidated ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "Blocker",
                    "Performance",
                    boundedPerformanceValidated ? string.Empty : "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "A current-world standalone bounded performance capture must exist.",
                    boundedPerformanceValidated
                        ? performanceRun.evidence
                        : performanceRun?.evidence ?? "No fingerprinted current-world standalone capture is available.",
                    boundedPerformanceValidated
                        ? "Retain the fingerprinted 1920x1080 capture and repeat it after material runtime changes."
                        : "Capture the current world slice in a Windows x64 Development Player at 1920x1080."),
                Issue(
                    "WORLD-PERF-002",
                    WorldValidationIssueState.Open,
                    "High",
                    "Performance",
                    "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "The full performance metric and route set is incomplete.",
                    boundedPerformanceValidated
                        ? "Bounded steady-state CPU/GPU timing, memory, Draw/Batches/SetPass and four visual checks are recorded; resident VRAM, isolated Present, physics cost, validated streaming spikes and route pacing remain unavailable."
                        : $"Fresh static audit: {validation.StaticMetrics.RendererCount} renderers, {validation.StaticMetrics.ColliderCount} colliders, {validation.StaticMetrics.LodGroupCount} LOD groups, {validation.StaticMetrics.TriangleCount} triangles; no accepted runtime timing series.",
                    "Measure the unavailable counters, validated streaming spikes and continuous-route frame pacing in a later performance pass."),
                Issue(
                    "WORLD-PERF-003",
                    WorldValidationIssueState.Open,
                    "Blocker",
                    "Performance",
                    "VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Required road-at-speed and vertical-slice performance locations do not exist as production content.",
                    "Home, vegetation, interior and shoreline can be profiled; route/destination cannot.",
                    "Profile those locations after the route is authored."),
                Issue(
                    "WORLD-DONOR-001",
                    dependencyAudit.violations.Length == 0 ? WorldValidationIssueState.Closed : WorldValidationIssueState.Open,
                    "High",
                    "ProductionIndependence",
                    dependencyAudit.violations.Length == 0 ? string.Empty : "PilotGate;VerticalSliceGate;FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "Audit the complete production/build dependency graph rather than a hard-coded asset list.",
                    $"seeds={dependencyAudit.seedAssets};visited={dependencyAudit.visitedAssets};edges={dependencyAudit.dependencyEdges};violations={dependencyAudit.violations.Length}",
                    "Retain the full graph audit and fail on ReferenceOnly, DonorGenerated or Editor asset dependencies."),
                Issue(
                    "WORLD-DONOR-002",
                    WorldValidationIssueState.Open,
                    "Medium",
                    "ProductionIndependence",
                    "FullWorldGate",
                    string.Empty,
                    string.Empty,
                    "A current-world standalone player assembly audit has not been executed.",
                    "Static asset graph is clean; only the older M3 build proves Editor DLL exclusion.",
                    "Audit ScriptingAssemblies.json in the next dedicated current-world development build.")
            };

            return issues.OrderBy(issue => issue.issueId, StringComparer.Ordinal).ToArray();
        }

        private static WorldValidationGateResult[] BuildGates(
            IReadOnlyList<WorldValidationIssue> issues,
            int eligibleCount,
            int mappedCount,
            WorldProductionAssetRegistry registry,
            ValidationSnapshot validation)
        {
            int boundCells = registry.Records
                .Where(record => record.HasProductionReplacement)
                .Select(record => record.ProductionZone)
                .Where(zone => zone != "global" && zone != "excluded")
                .Distinct(StringComparer.Ordinal)
                .Count();
            int approved = registry.Records.Count(record =>
                record.ReplacementStatus is WorldReplacementStatus.Approved or WorldReplacementStatus.Verified);
            bool m4TraversalPassed = validation.ValidatorRuns.Any(run =>
                string.Equals(run.validatorId, WorldPilotTraversalEvidenceReader.ValidatorId, StringComparison.Ordinal) &&
                run.executed &&
                run.passed);
            bool streamingLifecyclePassed = validation.ValidatorRuns.Any(run =>
                string.Equals(
                    run.validatorId,
                    ProductionWorldStreamingLifecycleEvidenceReader.ValidatorId,
                    StringComparison.Ordinal) &&
                run.executed &&
                run.passed);
            WorldValidationGateResult pilot = Gate(
                WorldValidationGate.PilotGate,
                issues,
                $"productionValidator={validation.Production.Passed};geometryValidator={validation.Geometry.IsValid};" +
                $"cellIdentity={validation.CellIdentity.Passed};productionStreamer={validation.ProductionStreamerWired};" +
                $"streamingLifecycle={streamingLifecyclePassed};m4Traversal={m4TraversalPassed}");
            WorldValidationGateResult vertical = RequirePrerequisite(
                Gate(
                    WorldValidationGate.VerticalSliceGate,
                    issues,
                    $"eligibleBindings={mappedCount}/{eligibleCount};productionStreamer={validation.ProductionStreamerWired};currentWorldPerformance=false"),
                pilot);
            WorldValidationGateResult full = RequirePrerequisite(
                Gate(
                    WorldValidationGate.FullWorldGate,
                    issues,
                    $"eligibleBindings={mappedCount}/{eligibleCount};productionBoundCells={boundCells};approvedOrVerified={approved}"),
                vertical);
            return new[] { pilot, vertical, full };
        }

        private static WorldValidationGateResult RequirePrerequisite(
            WorldValidationGateResult candidate,
            WorldValidationGateResult prerequisite)
        {
            if (prerequisite.achieved)
            {
                return candidate;
            }

            candidate.achieved = false;
            candidate.blockingIssueIds = candidate.blockingIssueIds
                .Concat(prerequisite.blockingIssueIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            candidate.evidence += $";prerequisite={prerequisite.gate}:false";
            return candidate;
        }

        private static WorldValidationGateResult Gate(
            WorldValidationGate gate,
            IReadOnlyList<WorldValidationIssue> issues,
            string evidence)
        {
            string[] blockers = issues
                .Where(issue => issue.Blocks(gate))
                .Select(issue => issue.issueId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return new WorldValidationGateResult
            {
                gate = gate.ToString(),
                achieved = blockers.Length == 0,
                blockingIssueIds = blockers,
                evidence = evidence
            };
        }

        private static WorldValidationCoverageRow CoverageRow(
            string scope,
            string scopeId,
            int total,
            int covered,
            int approved,
            int fallbacks,
            string status,
            string notes)
        {
            return new WorldValidationCoverageRow
            {
                scope = scope,
                scopeId = scopeId,
                total = total,
                covered = covered,
                coveragePercent = WorldValidationGateCalculator.CoveragePercent(covered, total),
                approved = approved,
                referenceFallbacks = fallbacks,
                missing = Math.Max(0, total - covered),
                blocked = 0,
                status = status,
                notes = notes
            };
        }

        private static WorldValidationCoverageRow CountOnlyRow(
            string scope,
            string id,
            int count,
            string notes) => new WorldValidationCoverageRow
        {
            scope = scope,
            scopeId = id,
            total = count,
            covered = count,
            coveragePercent = 100f,
            status = "CountOnly",
            notes = notes + " This is an observed count, not a full-world completion percentage."
        };

        private static WorldValidationSpatialRow Spatial(
            string id,
            string domain,
            string zone,
            string stableId,
            string expected,
            string measured,
            float deviation,
            float tolerance,
            string status,
            string evidence,
            string notes) => new WorldValidationSpatialRow
        {
            metricId = id,
            domain = domain,
            zoneId = zone,
            stableWorldId = stableId,
            expected = expected,
            measured = measured,
            deviationMeters = deviation,
            toleranceMeters = tolerance,
            status = status,
            evidence = evidence,
            notes = notes
        };

        private static WorldValidationSpatialRow UnavailableSpatial(
            string id,
            string domain,
            string zone,
            string notes) => Spatial(
            id,
            domain,
            zone,
            string.Empty,
            "Unavailable",
            "Unavailable",
            0f,
            0f,
            "Unavailable",
            string.Empty,
            notes);

        private static WorldValidationPerformanceLocation Performance(
            string id,
            string zone,
            string status,
            string available,
            string unavailable,
            string evidence) => new WorldValidationPerformanceLocation
        {
            locationId = id,
            zoneId = zone,
            status = status,
            availableMetrics = available,
            unavailableMetrics = unavailable,
            evidence = evidence
        };

        private static WorldValidationIssue Issue(
            string id,
            WorldValidationIssueState state,
            string severity,
            string domain,
            string gates,
            string zone,
            string stableId,
            string summary,
            string evidence,
            string action) => new WorldValidationIssue
        {
            issueId = id,
            state = state.ToString(),
            severity = severity,
            domain = domain,
            blockingGates = gates,
            zoneId = zone,
            stableWorldId = stableId,
            summary = summary,
            evidence = evidence,
            requiredAction = action
        };

        private static int CountCsvDataRows(string path)
        {
            string absolute = Path.GetFullPath(path);
            return File.Exists(absolute)
                ? Math.Max(0, File.ReadLines(absolute, Encoding.UTF8).Count(line => !string.IsNullOrWhiteSpace(line)) - 1)
                : 0;
        }

        private static int CoverageScopeOrder(string scope) => scope switch
        {
            "Universe" => 0,
            "System" => 1,
            "EligibleCategory" => 2,
            "Zone" => 3,
            "Supplemental" => 4,
            "ReferenceRepresentation" => 5,
            "ObservedContent" => 6,
            _ => 7
        };

        private static bool IssueAppliesToZone(WorldValidationIssue issue, string zoneId) =>
            string.IsNullOrWhiteSpace(issue.zoneId) || ZoneFieldApplies(issue.zoneId, zoneId);

        private static bool ZoneFieldApplies(string zoneField, string zoneId) =>
            string.IsNullOrWhiteSpace(zoneField) || zoneField.Split(';').Any(value =>
                string.Equals(value.Trim(), zoneId, StringComparison.Ordinal));

        private static string ZoneSortKey(string zone) => zone switch
        {
            WorldRemasterPaths.PilotZoneId => "0_" + zone,
            WorldRemasterPaths.NextZoneId => "1_" + zone,
            "global" => "9_global",
            _ => "2_" + zone
        };

        private static bool IsForbiddenProductionDependency(string path) =>
            IsReferenceOnlyPath(path) ||
            path.Contains("/Imported/DonorGenerated/", StringComparison.OrdinalIgnoreCase) ||
            (path.StartsWith("Assets/Game/", StringComparison.OrdinalIgnoreCase) &&
             path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase));

        private static bool IsReferenceOnlyPath(string path) =>
            path.Contains("/LegacyImport/ReferenceOnly/", StringComparison.OrdinalIgnoreCase);

        private static bool IsAssetFile(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith("Assets/", StringComparison.Ordinal) &&
            !AssetDatabase.IsValidFolder(path);

        private static void AddIfAssetExists(ISet<string> paths, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                paths.Add(path);
            }
        }

        private static string BuildIssueCsv(IEnumerable<WorldValidationIssue> issues)
        {
            var output = new StringBuilder();
            output.AppendLine("IssueId,State,Severity,Domain,BlockingGates,ZoneId,StableWorldId,Summary,Evidence,RequiredAction");
            foreach (WorldValidationIssue issue in issues.OrderBy(issue => issue.issueId, StringComparer.Ordinal))
            {
                AppendCsvRow(output,
                    issue.issueId,
                    issue.state,
                    issue.severity,
                    issue.domain,
                    issue.blockingGates,
                    issue.zoneId,
                    issue.stableWorldId,
                    issue.summary,
                    issue.evidence,
                    issue.requiredAction);
            }

            return output.ToString();
        }

        private static string BuildCoverageCsv(IEnumerable<WorldValidationCoverageRow> rows)
        {
            var output = new StringBuilder();
            output.AppendLine("Scope,ScopeId,Total,Covered,CoveragePercent,Approved,ReferenceFallbacks,Missing,Blocked,Status,Notes");
            foreach (WorldValidationCoverageRow row in rows)
            {
                AppendCsvRow(output,
                    row.scope,
                    row.scopeId,
                    row.total.ToString(Invariant),
                    row.covered.ToString(Invariant),
                    row.coveragePercent.ToString("0.000000", Invariant),
                    row.approved.ToString(Invariant),
                    row.referenceFallbacks.ToString(Invariant),
                    row.missing.ToString(Invariant),
                    row.blocked.ToString(Invariant),
                    row.status,
                    row.notes);
            }

            return output.ToString();
        }

        private static string BuildSpatialCsv(IEnumerable<WorldValidationSpatialRow> rows)
        {
            var output = new StringBuilder();
            output.AppendLine("MetricId,Domain,ZoneId,StableWorldId,Expected,Measured,DeviationMeters,ToleranceMeters,Status,Evidence,Notes");
            foreach (WorldValidationSpatialRow row in rows)
            {
                AppendCsvRow(output,
                    row.metricId,
                    row.domain,
                    row.zoneId,
                    row.stableWorldId,
                    row.expected,
                    row.measured,
                    row.deviationMeters.ToString("0.######", Invariant),
                    row.toleranceMeters.ToString("0.######", Invariant),
                    row.status,
                    row.evidence,
                    row.notes);
            }

            return output.ToString();
        }

        private static void AppendCsvRow(StringBuilder output, params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                output.Append('"');
                output.Append((values[index] ?? string.Empty).Replace("\"", "\"\""));
                output.Append('"');
            }

            output.AppendLine();
        }

        private static void AtomicWrite(string path, string content)
        {
            string absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? throw new InvalidOperationException());
            string temporary = absolute + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(absolute))
            {
                File.Replace(temporary, absolute, null);
            }
            else
            {
                File.Move(temporary, absolute);
            }
        }

        private static string Vector(Vector3 value) => string.Format(
            Invariant,
            "{0:0.######}|{1:0.######}|{2:0.######}",
            value.x,
            value.y,
            value.z);

        private sealed class CellIdentityAudit
        {
            public CellIdentityAudit(IReadOnlyCollection<string> errors, string evidence)
            {
                Errors = errors.ToArray();
                Evidence = evidence;
            }

            public string[] Errors { get; }
            public string Evidence { get; }
            public bool Passed => Errors.Length == 0;
        }

        private sealed class ValidationSnapshot
        {
            public ValidationSnapshot(
                WorldRemasterValidationResult production,
                WorldMapGeometryEvaluationValidationResult geometry,
                WorldContinuousGroundBaselineValidationResult ground,
                WorldTransferValidationResult transfer,
                WorldRemasterStaticMetrics staticMetrics,
                CellIdentityAudit cellIdentity,
                bool productionStreamerWired,
                string streamingEvidence,
                WorldValidationValidatorRun[] validatorRuns)
            {
                Production = production;
                Geometry = geometry;
                Ground = ground;
                Transfer = transfer;
                StaticMetrics = staticMetrics;
                CellIdentity = cellIdentity;
                ProductionStreamerWired = productionStreamerWired;
                StreamingEvidence = streamingEvidence;
                ValidatorRuns = validatorRuns;
            }

            public WorldRemasterValidationResult Production { get; }
            public WorldMapGeometryEvaluationValidationResult Geometry { get; }
            public WorldContinuousGroundBaselineValidationResult Ground { get; }
            public WorldTransferValidationResult Transfer { get; }
            public WorldRemasterStaticMetrics StaticMetrics { get; }
            public CellIdentityAudit CellIdentity { get; }
            public bool ProductionStreamerWired { get; }
            public string StreamingEvidence { get; }
            public WorldValidationValidatorRun[] ValidatorRuns { get; }
        }
    }
}
