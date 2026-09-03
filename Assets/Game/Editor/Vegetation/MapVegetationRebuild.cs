using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Editor.WorldBaseline;
using MSC.Editor.WorldStreaming;
using MSC.Traffic;
using MSC.World.Partition;
using MSC.World.Presentation;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>Regenerates only explicitly owned presentation, after preview/pilot validation.</summary>
    public static class MapVegetationRebuild
    {
        public const string Reports = "Artifacts/VegetationRebuild";
        public const string PlacementPath = "Assets/Game/Editor/Vegetation/MapVegetationPlacementSettings.asset";
        private const string LayerId = "vegetation";
        private const string PreviousRoot = "PHASE1_VEGETATION_REPLACEMENT_LICENSED_THIRD_PARTY";

        public static MapVegetationRebuildOptions LoadOptions()
        {
            var options = AssetDatabase.LoadAssetAtPath<MapVegetationRebuildOptions>(MapVegetationRebuildOptions.AssetPath);
            if (options != null)
            {
                ConfigureTrafficDefault(options.Placement);
                return options;
            }
            var placement = AssetDatabase.LoadAssetAtPath<MapVegetationPlacementSettings>(PlacementPath);
            if (placement == null)
            {
                placement = ScriptableObject.CreateInstance<MapVegetationPlacementSettings>();
                var trees = new List<GameObject>();
                foreach (string path in Phase1ForestRemediationBuilder.SprucePrefabs)
                    AddPrefab(trees, path);
                foreach (string species in new[] { "Aspen", "Birch", "Pine" })
                for (int i = 1; i <= (species == "Birch" ? 4 : 5); i++)
                    AddPrefab(trees, $"Assets/Chernobyl/Prefabs/Vegetation/Tree/{species}_{i:00}.prefab");
                var shrubs = new List<GameObject>();
                foreach (string prefab in new[] { "Bushes_01", "Bushes_02", "Bushes_03", "Fern_01", "Fern_02" })
                    AddPrefab(shrubs, "Assets/Chernobyl/Prefabs/Vegetation/" + prefab + ".prefab");
                placement.Category(MapVegetationKind.Tree).SetPrefabs(trees.ToArray());
                placement.Category(MapVegetationKind.Shrub).SetPrefabs(shrubs.ToArray());
                AssetDatabase.CreateAsset(placement, PlacementPath);
            }
            ConfigureTrafficDefault(placement);
            options = ScriptableObject.CreateInstance<MapVegetationRebuildOptions>();
            var profiles = new List<VegetationProfile>();
            foreach (string name in new[] { "ShortGrass", "MeadowGrass", "TallGrass" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<VegetationProfile>(VegetationAssetBuilder.ProfilePath + "/" + name + ".asset");
                if (profile != null) profiles.Add(profile);
            }
            options.Initialize(placement, profiles.ToArray());
            AssetDatabase.CreateAsset(options, MapVegetationRebuildOptions.AssetPath);
            AssetDatabase.SaveAssets();
            return options;
        }

        private static void ConfigureTrafficDefault(MapVegetationPlacementSettings placement)
        {
            if (placement == null || placement.TrafficNetwork != null) return;
            var network = AssetDatabase.LoadAssetAtPath<TrafficRoadNetworkCatalog>(
                "Assets/Game/Traffic/Content/Phase1/TrafficRoadNetworkCatalog.asset");
            if (network == null) return;
            var serialized = new SerializedObject(placement);
            serialized.FindProperty("trafficNetwork").objectReferenceValue = network;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placement);
        }

        private static void AddPrefab(List<GameObject> list, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
        }

        public static void RunPilotBatch() => Run(LoadOptions(), false, false, true);
        public static void RunAllBatch() => Run(LoadOptions(), true, false);
        public static void ValidateAllBatch() => Run(LoadOptions(), true, true);

        public static void PrepareApprovedArtBatch()
        {
            MapVegetationRebuildOptions options = LoadOptions();
            Material[] materials = MapVegetationMaterialBindings.BuildMaterials();
            VegetationProfile[] profiles = MapVegetationGrassBindings.BuildProfiles();
            MapVegetationTreePresentation.BuildAssetsBatch();
            MapVegetationForestFloorBindings.BuildAll();
            GameObject[] approvedTrees =
                MapVegetationTreePresentation.LoadAllApprovedPrefabs();
            options.Placement.Category(MapVegetationKind.Tree).SetPrefabs(approvedTrees);
            var serialized = new SerializedObject(options);
            SerializedProperty profileProperty = serialized.FindProperty("grassProfiles");
            profileProperty.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++) profileProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            SerializedProperty materialProperty = serialized.FindProperty("materialOverrides");
            materialProperty.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++) materialProperty.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // This is the already integrated solid grass-material terrain patch,
            // not new geometry or a blanket opt-in for all Terrain/WorldSolid objects.
            var placement = new SerializedObject(options.Placement);
            float grassFootprint = profiles.Max(profile => MapVegetationGrassBindings.MeasureMaximumFootprintRadius(profile,
                options.Placement.Category(MapVegetationKind.Grass)));
            float minimumGrassClearance = Mathf.Ceil((grassFootprint + .02f) * 100f) / 100f;
            SerializedProperty clearances = placement.FindProperty("clearances");
            for (int i = 0; i < clearances.arraySize; i++)
            {
                SerializedProperty grassMargin = clearances.GetArrayElementAtIndex(i).FindPropertyRelative("grassMeters");
                grassMargin.floatValue = Mathf.Max(grassMargin.floatValue, minimumGrassClearance);
            }
            Debug.Log($"MAP_VEGETATION_GRASS_FOOTPRINT radius={grassFootprint:R} minimumClearance={minimumGrassClearance:R}");
            SerializedProperty paths = placement.FindProperty("canonicalNaturalGroundPaths");
            const string existingTerrainPatch = "BetterMSC/MissingTerrain";
            bool containsPatch = false;
            for (int i = 0; i < paths.arraySize; i++) containsPatch |= paths.GetArrayElementAtIndex(i).stringValue == existingTerrainPatch;
            if (!containsPatch) { paths.InsertArrayElementAtIndex(paths.arraySize); paths.GetArrayElementAtIndex(paths.arraySize - 1).stringValue = existingTerrainPatch; }
            placement.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(options);
            EditorUtility.SetDirty(options.Placement);
            AssetDatabase.SaveAssets();
            Debug.Log($"MAP_VEGETATION_ART_BINDINGS_OK grassProfiles={profiles.Length} materialOverrides={materials.Length}");
        }

        public static void Run(MapVegetationRebuildOptions options, bool allCells, bool validateOnly, bool chooseRepresentativePilot = false)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Categories == MapVegetationCategories.None) throw new InvalidOperationException("Select at least one vegetation category.");
            Directory.CreateDirectory(Reports);
            string settingsHash = SettingsHash(options);
            string pilotPath = Reports + "/pilot-gate.json";
            PilotGate pilotGate = null;
            if (allCells && !validateOnly)
            {
                if (!File.Exists(pilotPath)) throw new InvalidOperationException("Run and validate one pilot cell before generating the entire map.");
                pilotGate = JsonUtility.FromJson<PilotGate>(File.ReadAllText(pilotPath));
                if (pilotGate == null || pilotGate.settingsHash != settingsHash || !pilotGate.passed)
                    throw new InvalidOperationException("Settings changed or pilot failed. Run the pilot again before full-map generation.");
                string capturePath = Reports + "/VisualAudit/capture-report.json";
                CaptureGate capture = File.Exists(capturePath) ? JsonUtility.FromJson<CaptureGate>(File.ReadAllText(capturePath)) : null;
                if (capture == null || !capture.passed || capture.cellId != pilotGate.cellId ||
                    capture.settingsHash != settingsHash || capture.generatedFingerprint != pilotGate.fingerprint)
                    throw new InvalidOperationException("Capture and inspect the current pilot in HDRP before full-map generation. The capture must match its settings and saved fingerprint and contain no unsupported shaders.");
            }
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var report = new RunReport { runId = runId, settingsHash = settingsHash,
                scope = allCells ? "all" : options.SelectedCell, validationOnly = validateOnly };
            string reportPath = Reports + "/" + runId;
            Directory.CreateDirectory(reportPath);
            try
            {
                using var context = new MapVegetationContext(options);
                report.sourceFingerprint = context.SourceFingerprint;
                if (pilotGate != null)
                {
                    if (pilotGate.sourceFingerprint != context.SourceFingerprint)
                        throw new InvalidOperationException("Source positions, ground or exclusions changed after the pilot. Run the pilot again before full-map generation.");
                    MapVegetationCellPlan repeatedPilot = context.CellPlan(ParseCell(pilotGate.cellId), true);
                    if (repeatedPilot.Fingerprint != pilotGate.fingerprint)
                        throw new InvalidOperationException("The pilot plan changed. Regenerate and validate the pilot before processing the map.");
                }
                report.sourceTrees = context.Source.Trees.Count;
                report.sourceShrubs = context.Source.Shrubs.Count;
                report.sourceRocks = context.Source.Rocks.Count;
                report.boundarySegments = context.Source.BoundarySegments.Count;
                report.sourceScenes = context.SourceScenes;
                report.naturalInfillOriginalBasis =
                    context.NaturalInfillOriginalBasisCount;
                report.naturalInfillCandidates =
                    context.NaturalInfillCandidateCount;
                report.naturalInfillEligible =
                    context.NaturalInfillEligibleCount;
                report.naturalInfillAccepted =
                    context.NaturalInfillAcceptedCount;
                report.naturalInfillRejectedByCap =
                    context.NaturalInfillRejectedByCap;
                report.naturalInfillAcceptedOnOpenSpace = context
                    .NaturalInfillAcceptedOnOpenSpaceCount;
                report.naturalInfillRejectionReasons = context
                    .NaturalInfillRejections.OrderBy(pair => pair.Key,
                        StringComparer.Ordinal).Select(pair =>
                        new RejectionCount
                        {
                            reason = pair.Key,
                            count = pair.Value
                        }).ToArray();
                report.boundaryNearInwardCandidates = context
                    .BoundaryNearInwardCandidateCount;
                report.boundaryNearInwardAccepted = context
                    .BoundaryNearInwardAcceptedCount;
                report.boundaryNearOutwardAccepted = context
                    .BoundaryNearOutwardAcceptedCount;
                report.boundaryNearAcceptedOnOpenSpace = context
                    .BoundaryNearAcceptedOnOpenSpaceCount;
                report.boundaryNearTreeBudget = options.BoundaryMaximumTrees;
                report.boundaryNearCandidatesSkippedAtCap = context
                    .BoundaryNearCandidatesSkippedAtCap;
                report.boundaryNearForestFloorAccepted = context
                    .BoundaryNearForestFloorAcceptedCount;
                report.boundaryNearForestFloorBudget = options
                    .BoundaryMaximumForestFloor;
                report.boundaryNearForestFloorCandidatesSkippedAtCap = context
                    .BoundaryNearForestFloorCandidatesSkippedAtCap;
                report.distantForestEligible =
                    context.DistantForestEligibleCount;
                report.distantForestAccepted =
                    context.DistantBackdrop.Count;
                report.distantForestRejectedByCap =
                    context.DistantForestRejectedByCap;
                report.distantForestCandidateCount =
                    context.DistantForestCandidateCount;
                report.distantForestSilhouetteFallbackCount = context
                    .DistantForestSilhouetteFallbackCount;
                report.distantForestCoverageBackboneCount = context
                    .DistantForestCoverageBackboneCount;
                report.distantForestCoverageSilhouetteOverrideCount = context
                    .DistantForestCoverageSilhouetteOverrideCount;
                report.distantForestOuterEnvelopeCount = context
                    .DistantForestOuterEnvelopeCount;
                report.distantForestSyntheticClosureSegmentCount = context
                    .DistantForestSyntheticClosureSegmentCount;
                report.distantForestBoundaryLengthMeters =
                    context.DistantForestBoundaryLengthMeters;
                report.distantForestAcceptedMinimumBoundaryDistanceMeters =
                    context.DistantForestAcceptedMinimumBoundaryDistanceMeters;
                report.distantForestAcceptedMaximumBoundaryDistanceMeters =
                    context.DistantForestAcceptedMaximumBoundaryDistanceMeters;
                report.distantForestRejectionReasons = context
                    .DistantForestRejections.OrderBy(pair => pair.Key,
                        StringComparer.Ordinal).Select(pair =>
                        new RejectionCount
                        {
                            reason = pair.Key,
                            count = pair.Value
                        }).ToArray();
                report.forestLoadingRadiusCells =
                    options.ForestLoadingRadiusCells;
                report.maximumResidentForestCellLayers =
                    (options.ForestLoadingRadiusCells * 2 + 1) *
                    (options.ForestLoadingRadiusCells * 2 + 1);
                report.activeLegacyCardRenderers = context.Source.Diagnostics.Count(d => d.Code == "ActiveLegacyVegetationRenderer");
                report.activeLegacyCanonicalRockRenderers = context.Source
                    .Diagnostics.Count(d => d.Code ==
                        "ActiveLegacyCanonicalRockRenderer");
                // Include rejected source points outside every allowed-ground
                // cell too; per-generated-cell reports alone omit those points.
                var rejectedSourceIds = new HashSet<string>(
                    StringComparer.Ordinal);
                using (var rejectedSources = new StreamWriter(reportPath + "/original-tree-source-rejections.csv", false, new UTF8Encoding(false)))
                {
                    rejectedSources.WriteLine("id,cell,x,y,z,reason,source");
                    var reasonCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    foreach (var entry in context.WoodyCells.OrderBy(p => p.Key.Z).ThenBy(p => p.Key.X))
                    foreach (MapVegetationIssue issue in entry.Value.Issues)
                    {
                        if (issue.category != MapVegetationCategories.OriginalTrees.ToString()) continue;
                        rejectedSourceIds.Add(issue.id);
                        reasonCounts.TryGetValue(issue.reason, out int count);
                        reasonCounts[issue.reason] = count + 1;
                        rejectedSources.WriteLine(Csv(issue.id) + "," + issue.cellId + "," + Coordinates(issue.position) + "," + Csv(issue.reason) + "," + Csv(issue.source));
                    }
                    report.globalOriginalTreePlanRejected = reasonCounts.Values.Sum();
                    report.globalOriginalTreePlanAccepted =
                        context.NaturalInfillOriginalBasisCount;
                    report.globalOriginalTreeRejectionReasons = reasonCounts.Select(p => new RejectionCount { reason = p.Key, count = p.Value }).ToArray();
                }
                report.sourceTreeProvenancePathCount = context.Source.Trees
                    .Select(tree => tree.SourcePath).Distinct(
                        StringComparer.Ordinal).Count();
                report.sourceTreeDuplicateIdCount = context.Source.Trees.Count -
                    context.Source.Trees.Select(tree => tree.Id).Distinct(
                        StringComparer.Ordinal).Count();
                report.sourceTreeDuplicateMillimetrePositionCount = context
                    .Source.Trees.GroupBy(tree =>
                        QuantizedMillimetrePosition(tree.Position))
                    .Sum(group => Mathf.Max(0, group.Count() - 1));
                using (var provenance = new StreamWriter(reportPath +
                    "/original-tree-provenance-summary.csv", false,
                    new UTF8Encoding(false)))
                {
                    provenance.WriteLine(
                        "sourcePath,sourceMeshGuid,sourceHash,candidates,accepted,rejected,methods");
                    foreach (IGrouping<string, MapVegetationSourcePoint> group
                             in context.Source.Trees.GroupBy(tree =>
                                     tree.SourcePath, StringComparer.Ordinal)
                                 .OrderBy(group => group.Key,
                                     StringComparer.Ordinal))
                    {
                        MapVegetationSourcePoint first = group.First();
                        int candidates = group.Count();
                        int rejected = group.Count(tree =>
                            rejectedSourceIds.Contains(tree.Id));
                        string methods = string.Join(";", group.GroupBy(tree =>
                                tree.Method, StringComparer.Ordinal)
                            .OrderBy(method => method.Key,
                                StringComparer.Ordinal)
                            .Select(method => method.Key + "=" +
                                method.Count().ToString(
                                    CultureInfo.InvariantCulture)));
                        provenance.WriteLine(Csv(group.Key) + "," +
                            Csv(first.SourceMeshGuid) + "," +
                            Csv(first.SourceHash) + "," + candidates + "," +
                            (candidates - rejected) + "," + rejected + "," +
                            Csv(methods));
                    }
                }
                using (var summary = new StreamWriter(reportPath + "/woody-plan-summary.csv", false, new UTF8Encoding(false)))
                {
                    summary.WriteLine("cell,accepted,skipped,mainReason");
                    foreach (var entry in context.WoodyCells.OrderBy(p => p.Key.Z).ThenBy(p => p.Key.X))
                        summary.WriteLine(entry.Key.Id + "," + entry.Value.Woody.Count + "," + entry.Value.Rejections.Values.Sum() + "," +
                            Csv(entry.Value.Rejections.OrderByDescending(p => p.Value).Select(p => p.Key).FirstOrDefault()));
                }
                File.WriteAllText(reportPath + "/source-diagnostics.json", JsonUtility.ToJson(new SourceReport(context), true));
                WorldCellIndex[] cells = allCells ? CollectFullRebuildCells(context.Cells, context.Manifest.CellLayers)
                    : new[] { ParseCell(options.SelectedCell) };
                int[] infillPerCell = cells.Select(cell =>
                        context.WoodyCells.TryGetValue(cell,
                            out MapVegetationCellPlan cellPlan)
                            ? cellPlan.Woody.Count(placement =>
                                placement.method ==
                                "DeterministicGreenForestInfill")
                            : 0)
                    .OrderBy(count => count).ToArray();
                report.naturalInfillCellCount = infillPerCell.Length;
                report.naturalInfillCellsWithTrees = infillPerCell.Count(
                    count => count > 0);
                report.naturalInfillPerCellMinimum = infillPerCell.Length == 0
                    ? 0 : infillPerCell[0];
                report.naturalInfillPerCellMaximum = infillPerCell.Length == 0
                    ? 0 : infillPerCell[infillPerCell.Length - 1];
                report.naturalInfillPerCellMedian = infillPerCell.Length == 0
                    ? 0f
                    : infillPerCell.Length % 2 == 1
                        ? infillPerCell[infillPerCell.Length / 2]
                        : (infillPerCell[infillPerCell.Length / 2 - 1] +
                           infillPerCell[infillPerCell.Length / 2]) * 0.5f;
                if (chooseRepresentativePilot && !allCells && !validateOnly)
                {
                    MapVegetationCellPlan selected = context.CellPlan(cells[0], true);
                    WriteCellReport(reportPath, selected, new CellReport { cellId = cells[0].Id, fingerprint = selected.Fingerprint });
                    if (!Representative(selected, options.Categories))
                    {
                        bool found = false;
                        foreach (var candidate in context.WoodyCells.Values
                            .Where(p => p.Woody.Any(w => w.category == MapVegetationCategories.OriginalTrees))
                            .OrderByDescending(p => p.Woody.Any(w => w.category == MapVegetationCategories.BoundaryForest))
                            .ThenByDescending(p => p.Woody.Count).Take(8))
                        {
                            MapVegetationCellPlan replacement = context.CellPlan(candidate.Cell, true);
                            if (!Representative(replacement, options.Categories)) continue;
                            cells[0] = candidate.Cell;
                            var serializedOptions = new SerializedObject(options);
                            serializedOptions.FindProperty("selectedCell").stringValue = candidate.Cell.Id;
                            serializedOptions.ApplyModifiedPropertiesWithoutUndo();
                            AssetDatabase.SaveAssets();
                            report.scope = candidate.Cell.Id;
                            Debug.Log("MAP_VEGETATION_PILOT_SELECTED " + candidate.Cell.Id + " from validated ground/source evidence.");
                            found = true;
                            break;
                        }
                        if (!found) throw new InvalidOperationException("No representative pilot found. Inspect woody-plan-summary.csv and selected-cell rejection reasons; no scene was changed.");
                    }
                }
                if (!validateOnly) ValidatePreviousTreeMigration(cells, options.Categories, allCells);
                var registrations = new List<ProductionWorldCellLayerScene>();
                if (!validateOnly)
                {
                    Backup(WorldBaseline06B2Paths.GlobalScene, runId);
                    Backup(WorldBaseline06B2Paths.ActiveManifest, runId);
                    Backup("ProjectSettings/EditorBuildSettings.asset", runId);
                }
                for (int index = 0; index < cells.Length; index++)
                {
                    WorldCellIndex cell = cells[index];
                    EditorUtility.DisplayProgressBar("Map vegetation", cell.Id, (float)index / cells.Length);
                    MapVegetationCellPlan plan = context.CellPlan(cell, true);
                    Debug.Log($"MAP_VEGETATION_PLAN {cell.Id} woody={plan.Woody.Count} grass={plan.Grass.Count} groundTriangles={context.Surfaces.GroundTriangleCount} exclusions={context.Surfaces.ExclusionCount}");
                    WriteCellReport(reportPath, plan, new CellReport { cellId = cell.Id, fingerprint = plan.Fingerprint });
                    if (!allCells && !validateOnly)
                    {
                        MapVegetationCellPlan repeated = context.CellPlan(cell, true);
                        if (plan.Fingerprint != repeated.Fingerprint) throw new InvalidOperationException("Pilot generation is not reproducible.");
                        if (RequiresRepresentativePilot(
                                allCells, validateOnly,
                                chooseRepresentativePilot) &&
                            !Representative(plan, options.Categories))
                            throw new InvalidOperationException("Choose a pilot cell containing the selected vegetation categories; current pilot has no representative placements.");
                    }
                    string scenePath = CellScenePath(cell);
                    if (!validateOnly) WriteCell(context, plan, scenePath, runId);
                    CellReport validated = ValidateSavedCell(context, plan, scenePath);
                    report.cells.Add(validated);
                    WriteCellReport(reportPath, plan, validated);
                    if (validated.errors.Count > 0)
                        throw new InvalidOperationException(cell.Id + " failed validation: " + string.Join("; ", validated.errors.Take(8)));
                    if (!validateOnly)
                        registrations.Add(new ProductionWorldCellLayerScene(LayerId, cell, -1, scenePath,
                            options.ForestLoadingRadiusCells, options.ForestLoadingRadiusCells + 1));
                    Debug.Log($"MAP_VEGETATION_CELL_OK {cell.Id} originals={validated.originalTrees} infill={validated.naturalInfillTrees} boundary={validated.boundaryTrees} shrubs={validated.shrubs} grass={validated.grass} fingerprint={plan.Fingerprint}");
                    if (allCells && ((index + 1) % 4 == 0 || index == cells.Length - 1))
                    {
                        // Scenes have been saved, reopened for validation and
                        // closed. Release completed cell arrays between batches;
                        // retain managed Editor references and the native lease.
                        plan = null;
                        GC.Collect();
                        EditorUtility.UnloadUnusedAssetsImmediate(true);
                        GC.Collect();
                    }
                }
                if (!validateOnly)
                {
                    ProductionWorldCellLayerBuilder.ReplaceLayerScenes(context.Manifest, LayerId, registrations, allCells ? null : cells);
                }
                // The deep-boundary pilot capture must inspect the same saved,
                // streamed backdrop scenes that the full run will register. This
                // layer is independently owned and deterministic, so writing it
                // here does not mutate unapproved near-cell content elsewhere.
                bool requiresBackdrop =
                    (options.Categories & MapVegetationCategories.BoundaryForest) != 0;
                if (!allCells && !validateOnly && requiresBackdrop)
                {
                    report.backdropPresentation =
                        MapVegetationBackdropPresentation.WriteAndRegister(
                            context, runId);
                    if (!report.backdropPresentation.passed)
                        throw new InvalidOperationException(
                            "Streamed distant vegetation backdrop failed pilot validation: " +
                            string.Join("; ", report.backdropPresentation.errors));
                }
                if (allCells)
                {
                    report.globalPresentation = validateOnly
                        ? MapVegetationGlobalPresentation.Validate(context)
                        : MapVegetationGlobalPresentation.WriteAndRegister(
                            context, runId);
                    if (!report.globalPresentation.passed)
                        throw new InvalidOperationException(
                            "Global vegetation presentation failed validation: " +
                            string.Join("; ", report.globalPresentation.errors));

                    report.backdropPresentation = validateOnly
                        ? MapVegetationBackdropPresentation.Validate(context)
                        : MapVegetationBackdropPresentation.WriteAndRegister(
                            context, runId);
                    if (!report.backdropPresentation.passed)
                        throw new InvalidOperationException(
                            "Streamed distant vegetation backdrop failed validation: " +
                            string.Join("; ", report.backdropPresentation.errors));
                }
                if (!validateOnly)
                {
                    RetirePreviousForest(cells, options.Categories, allCells);
                    AssetDatabase.SaveAssets();
                }
                report.passed = true;
                if (!allCells && !validateOnly)
                    File.WriteAllText(pilotPath, JsonUtility.ToJson(new PilotGate { passed = true, settingsHash = settingsHash,
                        sourceFingerprint = context.SourceFingerprint, cellId = cells[0].Id,
                        fingerprint = report.cells[0].fingerprint, runId = runId }, true));
                File.WriteAllText(reportPath + "/report.json", JsonUtility.ToJson(report, true));
                File.WriteAllText(Reports + "/latest-report.json", JsonUtility.ToJson(report, true));
                Debug.Log($"MAP_VEGETATION_{(allCells ? "FULL" : "PILOT")}_OK cells={report.cells.Count} report={reportPath}");
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                File.WriteAllText(reportPath + "/report.json", JsonUtility.ToJson(report, true));
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                MapVegetationPackedWoodyBuilder.ClearCaches();
            }
        }

        private static bool Representative(MapVegetationCellPlan plan, MapVegetationCategories categories)
        {
            if ((categories & MapVegetationCategories.GrassCoverage) != 0 && plan.Grass.Count == 0) return false;
            if ((categories & MapVegetationCategories.OriginalTrees) != 0 && !plan.Woody.Any(p => p.category == MapVegetationCategories.OriginalTrees)) return false;
            if ((categories & MapVegetationCategories.BoundaryForest) != 0 && !plan.Woody.Any(p => p.category == MapVegetationCategories.BoundaryForest)) return false;
            if ((categories & MapVegetationCategories.ShrubsAndUndergrowth) != 0 && !plan.Woody.Any(p => p.category == MapVegetationCategories.ShrubsAndUndergrowth)) return false;
            return (categories & ~MapVegetationCategories.GrassCoverage) == 0 || plan.Woody.Count > 0;
        }

        internal static bool RequiresRepresentativePilot(
            bool allCells,
            bool validateOnly,
            bool chooseRepresentativePilot) =>
            !allCells && !validateOnly && chooseRepresentativePilot;

        public static string SettingsHash(MapVegetationRebuildOptions options)
        {
            return MapVegetationPlanning.FingerprintSettings(options);
        }

        public static WorldCellIndex ParseCell(string value)
        {
            string[] parts = (value ?? string.Empty).Split('_');
            if (parts.Length != 3 || parts[0] != "cell" || !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int z))
                throw new ArgumentException("Cell ID must be cell_X_Z, for example cell_0_0.");
            return new WorldCellIndex(x, z);
        }

        public static WorldCellIndex[] CollectFullRebuildCells(IEnumerable<WorldCellIndex> currentCells,
            IReadOnlyList<ProductionWorldCellLayerScene> existingLayers)
        {
            if (currentCells == null) throw new ArgumentNullException(nameof(currentCells));
            if (existingLayers == null) throw new ArgumentNullException(nameof(existingLayers));
            var cells = new HashSet<WorldCellIndex>(currentCells);
            // A changed source/ground scope can remove every current candidate
            // from a previously generated cell. Visit it anyway: WriteCell clears
            // only selected categories and preserves the other/manual roots.
            foreach (ProductionWorldCellLayerScene layer in existingLayers)
                if (string.Equals(layer.LayerId, LayerId, StringComparison.Ordinal)) cells.Add(layer.Index);
            return cells.OrderBy(cell => cell.Z).ThenBy(cell => cell.X).ToArray();
        }

        public static string CellScenePath(WorldCellIndex cell) => MapVegetationRebuildOptions.GeneratedRoot + "/Scenes/World_Cell_" + cell.X + "_" + cell.Z + "_Vegetation.unity";

        private static void WriteCell(MapVegetationContext context, MapVegetationCellPlan plan, string path, string runId)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Backup(path, runId);
            bool existing = File.Exists(path);
            Scene scene = existing ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (existing) ValidateExistingCellSceneForRegeneration(scene, plan.Cell, context.Manifest);
                ValidateGeneratedOwnership(scene, plan.Cell.Id);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GeneratedVegetationGroup owner = root.GetComponent<GeneratedVegetationGroup>();
                    if (owner == null || owner.GeneratorId != MapVegetationRebuildOptions.GeneratorId) continue;
                    if (Enum.TryParse(owner.Category, out MapVegetationCategories category) && (context.Options.Categories & category) != 0)
                        Object.DestroyImmediate(root);
                }
                foreach (MapVegetationCategories category in new[] { MapVegetationCategories.OriginalTrees, MapVegetationCategories.BoundaryForest,
                    MapVegetationCategories.ShrubsAndUndergrowth, MapVegetationCategories.GrassCoverage })
                {
                    if ((context.Options.Categories & category) == 0) continue;
                    var root = new GameObject(category.ToString());
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.AddComponent<GeneratedVegetationGroup>().Configure(MapVegetationRebuildOptions.GeneratorId, plan.Cell.Id, category.ToString(), plan.Fingerprint);
                    if (category == MapVegetationCategories.GrassCoverage)
                    {
                        WriteGrass(context, plan, root, runId);
                    }
                    else
                    {
                        string packedPath =
                            MapVegetationPackedWoodyBuilder.CellAssetPath(
                                plan.Cell.Id, category);
                        Backup(packedPath, runId);
                        MapVegetationPackedWoodyBuilder.WriteCategory(
                            context, plan, category, root);
                    }
                }
                ValidateGeneratedOwnership(scene, plan.Cell.Id);
                ValidateMissingReferences(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void WriteGrass(MapVegetationContext context, MapVegetationCellPlan plan, GameObject root, string runId)
        {
            string folder = MapVegetationRebuildOptions.GeneratedRoot + "/Data/" + plan.Cell.Id;
            EnsureFolder(folder);
            string cellPath = folder + "/GrassCell.asset", catalogPath = folder + "/Catalog.asset", maskPath = folder + "/Density.asset";
            Backup(cellPath, runId); Backup(catalogPath, runId); Backup(maskPath, runId);
            var mask = LoadOrCreate<Texture2D>(maskPath, () => new Texture2D(512, 512, TextureFormat.RGBA32, false, true) { name = plan.Cell.Id + "_GeneratedDensity" });
            var cell = LoadOrCreate<VegetationCellAsset>(cellPath, ScriptableObject.CreateInstance<VegetationCellAsset>);
            float size = context.Manifest.CellSizeMeters;
            cell.ConfigureForAuthoring(plan.Cell.Id, plan.Cell,
                new Bounds(new Vector3((plan.Cell.X + 0.5f) * size, 0f, (plan.Cell.Z + 0.5f) * size), new Vector3(size, 4096f, size)), 512, 32f, mask);
            var serialized = new SerializedObject(cell);
            serialized.FindProperty("tiles").ClearArray(); serialized.ApplyModifiedPropertiesWithoutUndo();
            var pixels = new Color32[512 * 512];
            var tiles = new SortedDictionary<int, List<MapVegetationPlacement>>();
            foreach (MapVegetationPlacement p in plan.Grass)
            {
                Vector2Int tile = cell.WorldToTileCoordinate(p.position);
                int key = tile.y * cell.TileCountX + tile.x;
                if (!tiles.TryGetValue(key, out List<MapVegetationPlacement> list)) tiles[key] = list = new List<MapVegetationPlacement>();
                list.Add(p);
                Vector2Int pixel = cell.WorldToMaskPixel(p.position);
                Color32 color = pixels[pixel.y * 512 + pixel.x];
                switch (context.Options.GrassProfiles[p.profile].DensityChannel)
                {
                    case VegetationDensityChannel.ShortGrass: color.r = 255; break;
                    case VegetationDensityChannel.MeadowGrass: color.g = 255; break;
                    case VegetationDensityChannel.TallGrass: color.b = 255; break;
                    case VegetationDensityChannel.Decorative: color.a = 255; break;
                }
                pixels[pixel.y * 512 + pixel.x] = color;
            }
            mask.SetPixels32(pixels); mask.Apply(false, false); EditorUtility.SetDirty(mask);
            foreach (var tile in tiles)
            {
                var groups = new List<VegetationProfileTileInstances>();
                var bounds = new Bounds(tile.Value[0].position, Vector3.zero);
                for (int profileIndex = 0; profileIndex < context.Options.GrassProfiles.Length; profileIndex++)
                {
                    VegetationProfile profile = context.Options.GrassProfiles[profileIndex];
                    var records = new List<VegetationInstanceRecord>();
                    foreach (MapVegetationPlacement p in tile.Value)
                    {
                        if (p.profile != profileIndex) continue;
                        bounds.Encapsulate(p.position);
                        float scale = Mathf.Lerp(profile.UniformScaleRange.x, profile.UniformScaleRange.y, p.variation);
                        Vector2 categoryScale = context.Options.Placement.Category(MapVegetationKind.Grass).ScaleRange;
                        scale *= Mathf.Lerp(categoryScale.x, categoryScale.y, p.variation);
                        Vector3 alignedNormal = Vector3.Slerp(Vector3.up, p.normal,
                            profile.SurfaceNormalAlignment * context.Options.Placement.Category(MapVegetationKind.Grass).NormalAlignment).normalized;
                        records.Add(new VegetationInstanceRecord(p.position, alignedNormal, p.yaw, scale, (byte)profileIndex,
                            (byte)Mathf.RoundToInt(p.variation * 255f), (ushort)(MapVegetationPlanning.HashId(p.id, 17) & 0xffff)));
                    }
                    if (records.Count > 0) groups.Add(new VegetationProfileTileInstances(profileIndex, records.ToArray()));
                }
                bounds.Expand(4f);
                cell.ReplaceTileForAuthoring(new VegetationTileRecord(tile.Key % cell.TileCountX, tile.Key / cell.TileCountX,
                    bounds, groups.ToArray(), Array.Empty<Vector3>(), Array.Empty<VegetationRejectedSample>()));
            }
            EditorUtility.SetDirty(cell);
            var catalog = LoadOrCreate<VegetationCellCatalog>(catalogPath, ScriptableObject.CreateInstance<VegetationCellCatalog>);
            var catalogProfiles = new List<VegetationProfile>(context.Options.GrassProfiles);
            var channels = new HashSet<VegetationDensityChannel>(catalogProfiles.Select(p => p.DensityChannel));
            // Preserve the painter/catalog's four-channel contract; extra bindings
            // have no instances and do not change the selected grass mixture.
            foreach (string profileName in new[] { "ShortGrass", "MeadowGrass", "TallGrass", "Decorative" })
            {
                var fallback = AssetDatabase.LoadAssetAtPath<VegetationProfile>(VegetationAssetBuilder.ProfilePath + "/" + profileName + ".asset");
                if (fallback != null && channels.Add(fallback.DensityChannel)) catalogProfiles.Add(fallback);
            }
            catalog.ConfigureForAuthoring(new[] { cell }, catalogProfiles.ToArray(), ~0, 2048f, 4096f);
            var catalogErrors = catalog.ValidateConfiguration();
            if (catalogErrors.Count > 0) throw new InvalidDataException("Generated grass catalog: " + string.Join("; ", catalogErrors));
            EditorUtility.SetDirty(catalog);
            root.AddComponent<VegetationWorldRenderer>().ConfigureForAuthoring(catalog);
            AssetDatabase.SaveAssets();
        }

        private static CellReport ValidateSavedCell(MapVegetationContext context, MapVegetationCellPlan plan, string path)
        {
            var report = new CellReport { cellId = plan.Cell.Id, scenePath = path, fingerprint = plan.Fingerprint,
                originalTrees = plan.Woody.Count(p => p.category == MapVegetationCategories.OriginalTrees),
                naturalInfillTrees = plan.Woody.Count(p => p.method ==
                    "DeterministicGreenForestInfill"),
                boundaryTrees = plan.Woody.Count(p => p.category == MapVegetationCategories.BoundaryForest),
                rockAccentPlacements = plan.Woody.Count(p =>
                    string.Equals(p.species, "forest-floor-rock",
                        StringComparison.OrdinalIgnoreCase) ||
                    (p.id ?? string.Empty).StartsWith("forest-rock:",
                        StringComparison.Ordinal)),
                shrubs = plan.Woody.Count(p => p.category == MapVegetationCategories.ShrubsAndUndergrowth), grass = plan.Grass.Count,
                grassProfileCounts = Enumerable.Range(0,
                        context.Options.GrassProfiles.Length)
                    .Select(profileIndex => plan.Grass.Count(placement =>
                        placement.profile == profileIndex)).ToArray(),
                sourceRejected = plan.Issues.Count(p => p.category == MapVegetationCategories.OriginalTrees.ToString()), grassCandidates = plan.GrassCandidates };
            if (report.rockAccentPlacements != 0)
                report.errors.Add(
                    "Random RockAccent/forest-rock placements are forbidden; canonical rocks are validated only in the global renderer override.");
            if (!File.Exists(path)) { report.errors.Add("Missing generated scene"); return report; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                ValidateMissingReferences(scene);
                ValidateGeneratedOwnership(scene, plan.Cell.Id);
                var expected = new Dictionary<Hash128, MapVegetationPlacement>();
                foreach (MapVegetationPlacement placement in plan.Woody)
                {
                    Hash128 stableHash = Hash128.Compute(placement.id);
                    if (!expected.TryAdd(stableHash, placement))
                        report.errors.Add("Stable-ID Hash128 collision in woody plan: " + placement.id);
                }
                var expectedGrass = plan.Grass.ToDictionary(p => p.position);
                int found = 0, grassFound = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                    if (group == null || group.GeneratorId != MapVegetationRebuildOptions.GeneratorId) continue;
                    if (group.CellId != plan.Cell.Id) report.errors.Add("Wrong cell ownership: " + root.name);
                    if (!Enum.TryParse(group.Category, out MapVegetationCategories category) || (context.Options.Categories & category) == 0) continue;
                    if (group.Fingerprint != plan.Fingerprint) report.errors.Add("Saved generation fingerprint differs from plan: " + root.name);
                    if (category == MapVegetationCategories.GrassCoverage)
                    {
                        VegetationWorldRenderer renderer = root.GetComponent<VegetationWorldRenderer>();
                        if (renderer == null || renderer.Catalog == null || renderer.Catalog.Cells.Count != 1)
                        { report.errors.Add("Grass renderer must reference exactly its own cell"); continue; }
                        report.errors.AddRange(renderer.Catalog.ValidateConfiguration());
                        if (renderer.Catalog.Cells[0] == null || renderer.Catalog.Cells[0].CellId != plan.Cell.Id)
                        { report.errors.Add("Grass catalog references another cell"); continue; }
                        foreach (VegetationTileRecord tile in renderer.Catalog.Cells[0].Tiles)
                        foreach (VegetationProfileTileInstances profile in tile.ProfileInstances)
                        foreach (VegetationInstanceRecord p in profile.Instances)
                        {
                            grassFound++;
                            if (profile.ProfileIndex < 0 || profile.ProfileIndex >= renderer.Catalog.Profiles.Count ||
                                p.ProfileIndex != profile.ProfileIndex || renderer.Catalog.Profiles[profile.ProfileIndex] == null)
                            { report.errors.Add("Invalid grass profile binding"); continue; }
                            if (!expectedGrass.TryGetValue(p.WorldPosition, out MapVegetationPlacement expectedPlacement))
                            { report.errors.Add("Unexpected/duplicate saved grass position: " + p.WorldPosition); continue; }
                            expectedGrass.Remove(p.WorldPosition);
                            if (expectedPlacement.profile != profile.ProfileIndex ||
                                renderer.Catalog.Profiles[profile.ProfileIndex] != context.Options.GrassProfiles[expectedPlacement.profile])
                                report.errors.Add("Saved grass profile differs from plan");
                            if (!MapVegetationPlanning.CellAt(p.WorldPosition, context.Manifest.CellSizeMeters).Equals(plan.Cell)) report.errors.Add("Grass belongs to another cell");
                            VegetationProfile savedProfile = renderer.Catalog.Profiles[profile.ProfileIndex];
                            if (!context.Surfaces.TryResolveGrass(p.WorldPosition, savedProfile.DensityChannel, out MapVegetationSurfaceHit hit, out string reason)
                                || Mathf.Abs(hit.Position.y - p.WorldPosition.y) > 0.04f)
                            { if (report.errors.Count < 50) report.errors.Add("Invalid saved grass: " + p.WorldPosition + " " + reason); }
                        }
                        continue;
                    }

                    report.generatedRootCount++;
                    Transform[] generatedTransforms =
                        root.GetComponentsInChildren<Transform>(true);
                    report.generatedGameObjects += generatedTransforms.Length;
                    foreach (Transform generatedTransform in generatedTransforms)
                    {
                        report.generatedComponents += generatedTransform
                            .GetComponents<Component>().Length;
                        if (PrefabUtility.IsPartOfPrefabInstance(
                            generatedTransform.gameObject))
                            report.prefabInstanceGameObjects++;
                    }
                    report.directMeshRenderers += root
                        .GetComponentsInChildren<MeshRenderer>(true).Length;
                    report.serializedColliderComponents += root
                        .GetComponentsInChildren<Collider>(true).Length;
                    if (root.transform.childCount != 0)
                        report.errors.Add("Packed woody root serialized child GameObjects: " + root.name);
                    if (root.GetComponentsInChildren<LODGroup>(true).Length != 0)
                        report.errors.Add("Packed woody root serialized LODGroups: " + root.name);
                    if (root.GetComponentsInChildren<Renderer>(true).Length != 0)
                        report.errors.Add("Packed woody root serialized direct renderers: " + root.name);
                    if (root.GetComponentsInChildren<Collider>(true).Length != 0)
                        report.errors.Add("Packed woody root serialized collider components: " + root.name);

                    PackedWoodyCellRenderer packedRenderer =
                        root.GetComponent<PackedWoodyCellRenderer>();
                    PackedWoodyCellAsset asset = packedRenderer != null
                        ? packedRenderer.CellAsset : null;
                    if (asset == null)
                    {
                        report.errors.Add("Packed woody renderer/asset is missing: " + root.name);
                        continue;
                    }
                    report.errors.AddRange(asset.ValidateConfiguration());
                    if (asset.CellId != plan.Cell.Id ||
                        asset.PlanFingerprint != plan.Fingerprint ||
                        asset.GeneratorId != MapVegetationRebuildOptions.GeneratorId ||
                        asset.PresentationVersion !=
                        MapVegetationPackedWoodyBuilder.PresentationVersion)
                        report.errors.Add("Packed woody provenance differs from the plan: " + root.name);
                    string packedAssetPath = AssetDatabase.GetAssetPath(asset);
                    if (File.Exists(packedAssetPath))
                        report.packedAssetBytes += new FileInfo(packedAssetPath).Length;
                    report.packedPrototypeCount += asset.Prototypes.Count;
                    report.packedBatchCount += asset.BatchCount;
                    report.packedMatrixCount += asset.Batches.Sum(batch => batch.Count);
                    report.packedPlacementMetadataCount += asset.InstanceCount;
                    report.packedCollisionRecords += asset.CollisionRecordCount;
                    report.spruce += asset.CountSpecies(PackedWoodySpecies.Spruce);
                    report.pine += asset.CountSpecies(PackedWoodySpecies.Pine);
                    report.birch += asset.CountSpecies(PackedWoodySpecies.Birch);
                    report.aspen += asset.CountSpecies(PackedWoodySpecies.Aspen);

                    PackedWoodyCollisionPool collisionPool =
                        root.GetComponent<PackedWoodyCollisionPool>();
                    var expectedCollisionById = new Dictionary<Hash128,
                        MapVegetationPlacement>();
                    if (category == MapVegetationCategories.OriginalTrees &&
                        context.Options.CreateTreeColliders)
                    {
                        foreach (MapVegetationPlacement placement in plan.Woody)
                        {
                            if (placement.category != category) continue;
                            Hash128 stableId = Hash128.Compute(placement.id);
                            if (!expectedCollisionById.TryAdd(
                                stableId, placement))
                                report.errors.Add(
                                    "Expected packed collision ID is duplicated: " +
                                    placement.id);
                        }
                    }
                    int expectedCollisionRecords =
                        expectedCollisionById.Count;
                    if (asset.CollisionRecordCount != expectedCollisionRecords)
                        report.errors.Add("Packed collision-record count differs from policy: " + root.name);
                    if ((expectedCollisionRecords > 0) != (collisionPool != null))
                        report.errors.Add("Packed collision-pool component differs from policy: " + root.name);
                    if (collisionPool != null && collisionPool.CellAsset != asset)
                        report.errors.Add("Packed collision pool references another asset: " + root.name);
                    var savedCollisionIds = new HashSet<Hash128>();
                    foreach (PackedWoodyCollisionRecord collision in
                        asset.CollisionRecords)
                    {
                        if (!savedCollisionIds.Add(collision.StableIdHash))
                        {
                            report.errors.Add(
                                "Duplicate packed collision record: " +
                                collision.StableIdHash);
                            continue;
                        }
                        if (!expectedCollisionById.TryGetValue(
                            collision.StableIdHash,
                            out MapVegetationPlacement placement))
                        {
                            report.errors.Add(
                                "Unexpected packed collision record: " +
                                collision.StableIdHash);
                            continue;
                        }
                        bool naturalInfill = placement.method ==
                            "DeterministicGreenForestInfill";
                        if (naturalInfill)
                            report.packedNaturalInfillCollisionRecords++;
                        else report.packedOriginalDonorCollisionRecords++;
                        float expectedHeight = Mathf.Max(
                            1f, placement.height * 0.72f);
                        float expectedRadius = Mathf.Max(
                            0.14f, placement.height * 0.012f);
                        if ((collision.BottomCenter - placement.position)
                                .sqrMagnitude > 0.000001f ||
                            Mathf.Abs(collision.CapsuleHeight -
                                expectedHeight) > 0.0001f ||
                            Mathf.Abs(collision.CapsuleRadius -
                                expectedRadius) > 0.0001f)
                            report.errors.Add(
                                "Packed collision geometry differs from policy: " +
                                placement.id);
                    }
                    if (!savedCollisionIds.SetEquals(
                        expectedCollisionById.Keys))
                        report.errors.Add(
                            "Packed collision stable-ID set differs from createTreeColliders policy: " +
                            root.name);
                    if (collisionPool != null)
                    {
                        report.collisionPoolEstimatedMaximumCandidates = Mathf.Max(
                            report.collisionPoolEstimatedMaximumCandidates,
                            MapVegetationPackedWoodyBuilder.EstimateMaximumCollisionCandidates(
                                asset, PackedWoodyCollisionPool.DefaultActivationRadiusMeters));
                        report.collisionPoolEstimatedOverflow = Mathf.Max(
                            0, report.collisionPoolEstimatedMaximumCandidates -
                               PackedWoodyCollisionPool.DefaultMaximumColliderCount);
                        if (report.collisionPoolEstimatedOverflow > 0)
                            report.warnings.Add(
                                "PACKED_WOODY_COLLISION_POOL_CAP estimatedCandidates=" +
                                report.collisionPoolEstimatedMaximumCandidates +
                                " cap=" +
                                PackedWoodyCollisionPool.DefaultMaximumColliderCount +
                                " estimatedOverflow=" +
                                report.collisionPoolEstimatedOverflow);
                    }

                    foreach (PackedWoodyPlacementRecord saved in asset.Placements)
                    {
                        if (!expected.TryGetValue(saved.StableIdHash,
                            out MapVegetationPlacement p))
                        {
                            report.errors.Add("Unexpected/duplicate packed woody placement: " + saved.StableIdHash);
                            continue;
                        }
                        expected.Remove(saved.StableIdHash);
                        found++;
                        if (p.category != category) report.errors.Add("Wrong generated category: " + p.id);
                        if ((saved.WorldPosition - p.position).sqrMagnitude > 0.0001f)
                            report.errors.Add("Saved packed position mismatch: " + p.id);
                        if (p.category == MapVegetationCategories.OriginalTrees && new Vector2(saved.WorldPosition.x - p.sourcePosition.x, saved.WorldPosition.z - p.sourcePosition.z).sqrMagnitude > 0.000001f)
                            report.errors.Add("Original XZ changed: " + p.id);
                        MapVegetationPackedWoodyBuilder.ExpectedPlacement expectedPresentation =
                            MapVegetationPackedWoodyBuilder.ResolveForValidation(context, p);
                        if (saved.PrototypeIndex < 0 ||
                            saved.PrototypeIndex >= asset.Prototypes.Count ||
                            saved.BatchIndex < 0 ||
                            saved.BatchIndex >= asset.Batches.Count)
                        {
                            report.errors.Add(
                                "Packed presentation pointer is invalid: " +
                                p.id);
                            continue;
                        }
                        PackedWoodyBatch savedBatch =
                            asset.Batches[saved.BatchIndex];
                        if (savedBatch == null || saved.MatrixIndex < 0 ||
                            saved.MatrixIndex >= savedBatch.Matrices.Count)
                        {
                            report.errors.Add(
                                "Packed matrix pointer is invalid: " + p.id);
                            continue;
                        }
                        PackedWoodyPrototypeAsset savedPrototype =
                            asset.Prototypes[saved.PrototypeIndex];
                        Matrix4x4 savedMatrix =
                            savedBatch.Matrices[saved.MatrixIndex];
                        if (savedPrototype == null ||
                            savedPrototype.StableKey != expectedPresentation.PrototypeStableKey ||
                            saved.Method != expectedPresentation.Method ||
                            saved.Species != expectedPresentation.Species ||
                            Mathf.Abs(saved.HeightMeters - expectedPresentation.HeightMeters) > 0.001f ||
                            !MatrixApproximately(savedMatrix, expectedPresentation.Matrix, 0.0001f))
                            report.errors.Add("Packed prefab/transform metadata differs from deterministic plan: " + p.id);
                        MapVegetationKind kind = category == MapVegetationCategories.ShrubsAndUndergrowth ? MapVegetationKind.Shrub : MapVegetationKind.Tree;
                        float surfaceOffset = ForestFloorSurfaceOffset(p.species);
                        // Forest-floor roots are intentionally offset along the
                        // authored surface normal (rocks are slightly buried).
                        // Resolve from the original contact point; querying from
                        // below a sloped/overlapping mesh can select a different
                        // lower triangle even though the saved root is correct.
                        Vector3 surfaceQuery = saved.WorldPosition - p.normal * surfaceOffset;
                        if (!context.Surfaces.TryResolve(surfaceQuery, kind, out MapVegetationSurfaceHit hit, out string reason)
                            || Mathf.Abs((hit.Position + hit.Normal * surfaceOffset).y - saved.WorldPosition.y) > 0.04f)
                            report.errors.Add("Invalid saved packed ground: " + p.id + " " + reason);
                    }
                }
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    report.savedSceneGameObjects += transforms.Length;
                    foreach (Transform transform in transforms)
                        report.savedSceneComponents += transform.GetComponents<Component>().Length;
                }
                report.serializedSceneBytes = new FileInfo(path).Length;
                report.packedAssetByteBudget =
                    MapVegetationPackedWoodyBuilder.PackedAssetByteBudget(
                        report.packedPlacementMetadataCount);
                report.packedAssetBytesPerPlacement =
                    report.packedPlacementMetadataCount > 0
                        ? (double)report.packedAssetBytes /
                          report.packedPlacementMetadataCount
                        : 0d;
                if (report.generatedGameObjects >
                    MapVegetationPackedWoodyBuilder.SerializedSceneGameObjectBudget)
                    report.errors.Add("Generated woody GameObject budget exceeded");
                if (report.generatedComponents >
                    MapVegetationPackedWoodyBuilder.SerializedSceneComponentBudget)
                    report.errors.Add("Generated woody component budget exceeded");
                if (report.prefabInstanceGameObjects != 0 ||
                    report.directMeshRenderers != 0 ||
                    report.serializedColliderComponents != 0)
                    report.errors.Add("Packed woody roots retained prefab/render/collider scene objects");
                if (report.serializedSceneBytes >
                    MapVegetationPackedWoodyBuilder.SerializedSceneByteBudget)
                    report.errors.Add("Serialized vegetation scene byte budget exceeded");
                if (!MapVegetationPackedWoodyBuilder
                        .PackedAssetBytesFitBudget(
                            report.packedAssetBytes,
                            report.packedPlacementMetadataCount))
                    report.errors.Add(
                        "Packed vegetation asset byte budget exceeded: " +
                        report.packedAssetBytes + " > " +
                        report.packedAssetByteBudget);
                if (found != plan.Woody.Count) report.errors.Add("Saved woody instance count differs from plan");
                if (report.packedMatrixCount != found ||
                    report.packedPlacementMetadataCount != found)
                    report.errors.Add("Packed placement/matrix/metadata counts are not exact");
                if ((context.Options.Categories & MapVegetationCategories.GrassCoverage) != 0 && grassFound != plan.Grass.Count)
                    report.errors.Add("Saved grass instance count differs from plan");
                if (expectedGrass.Count != 0) report.errors.Add("Expected grass positions are missing from saved data");
                if (expected.Count != 0) report.errors.Add("Expected woody placements are missing from packed data");
            }
            catch (Exception exception) { report.errors.Add(exception.Message); }
            finally { EditorSceneManager.CloseScene(scene, true); }
            return report;
        }

        private static bool MatrixApproximately(
            Matrix4x4 left,
            Matrix4x4 right,
            float tolerance)
        {
            for (int index = 0; index < 16; index++)
                if (Mathf.Abs(left[index] - right[index]) > tolerance)
                    return false;
            return true;
        }

        private static float ForestFloorSurfaceOffset(string species)
        {
            switch ((species ?? string.Empty).ToLowerInvariant())
            {
                case "forest-floor-rock":
                    return MapVegetationForestFloorBindings.Policy(
                        MapVegetationForestFloorBindings.Pool.RocksAndBoulders).SurfaceOffsetMeters;
                case "forest-floor-shrub":
                    return MapVegetationForestFloorBindings.Policy(
                        MapVegetationForestFloorBindings.Pool.Shrubs).SurfaceOffsetMeters;
                case "forest-floor-understory":
                    return MapVegetationForestFloorBindings.Policy(
                        MapVegetationForestFloorBindings.Pool.Understory).SurfaceOffsetMeters;
                default:
                    return 0f;
            }
        }

        private static void ValidateMissingReferences(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new InvalidDataException("Missing script on " + transform.name);
                MeshFilter filter = transform.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == null) throw new InvalidDataException("Missing mesh on " + transform.name);
                Renderer renderer = transform.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterials.Any(m => m == null || m.shader == null))
                    throw new InvalidDataException("Missing material/shader on " + transform.name);
            }
        }

        private static void ValidateGeneratedOwnership(Scene scene, string cellId)
        {
            var categories = new HashSet<MapVegetationCategories>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                if (group == null || group.GeneratorId != MapVegetationRebuildOptions.GeneratorId) continue;
                if (group.CellId != cellId) throw new InvalidDataException("Refusing to modify a generated root owned by another cell: " + root.name);
                if (!Enum.TryParse(group.Category, out MapVegetationCategories category) ||
                    category == MapVegetationCategories.None || category == MapVegetationCategories.All ||
                    ((int)category & ((int)category - 1)) != 0)
                    throw new InvalidDataException("Invalid generated category ownership: " + root.name);
                if (!categories.Add(category)) throw new InvalidDataException("Duplicate generated category roots: " + category);
            }
        }

        public static void ValidateExistingCellSceneForRegeneration(Scene scene,
            WorldCellIndex cell, ProductionWorldStreamingManifest manifest)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path, CellScenePath(cell), StringComparison.Ordinal))
                throw new InvalidDataException("Regeneration requires this generator's exact loaded cell-scene path.");
            GameObject[] roots = scene.GetRootGameObjects();
            bool hasGeneratedOwnership = roots.Any(root =>
                root.GetComponent<GeneratedVegetationGroup>()?.GeneratorId == MapVegetationRebuildOptions.GeneratorId);
            bool hasRegisteredOwnership = manifest != null && manifest.CellLayers.Any(entry =>
                entry.LayerId == LayerId && entry.Index.Equals(cell) &&
                string.Equals(entry.ScenePath, scene.path, StringComparison.Ordinal));
            // Clear removes category roots and unregisters an empty scene. Its
            // saved file is intentionally retained, so an empty canonical scene
            // is safe to repopulate. Manual-only leftovers retain the existing
            // layer registration and are preserved by WriteCell.
            if (roots.Length > 0 && !hasGeneratedOwnership && !hasRegisteredOwnership)
                throw new InvalidOperationException("Refusing to overwrite a nonempty scene without matching generated ownership: " + scene.path);
            ValidateGeneratedOwnership(scene, cell.Id);
        }

        private static void RetirePreviousForest(IReadOnlyCollection<WorldCellIndex> cells, MapVegetationCategories categories, bool all)
        {
            Scene scene = EditorSceneManager.OpenScene(WorldBaseline06B2Paths.GlobalScene, OpenSceneMode.Single);
            var selected = new HashSet<string>(cells.Select(c => "Forest_Cell_" + c.X + "_" + c.Z), StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null || transform.name != PreviousRoot) continue;
                bool trees = (categories & (MapVegetationCategories.OriginalTrees | MapVegetationCategories.BoundaryForest)) ==
                    (MapVegetationCategories.OriginalTrees | MapVegetationCategories.BoundaryForest);
                foreach (Transform cell in transform.Cast<Transform>().ToArray())
                {
                    if (!all && !selected.Contains(cell.name)) continue;
                    foreach (Transform category in cell.Cast<Transform>().ToArray())
                        if ((trees && category.name == "Trees") ||
                            ((categories & MapVegetationCategories.ShrubsAndUndergrowth) != 0 && category.name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            ((categories & MapVegetationCategories.GrassCoverage) != 0 && category.name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0))
                            Object.DestroyImmediate(category.gameObject);
                }
                // Keep the established single wind owner; no second Enviro/weather integration.
            }
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the previous-forest retirement scene.");
        }

        private static void ValidatePreviousTreeMigration(IReadOnlyCollection<WorldCellIndex> cells, MapVegetationCategories categories, bool all)
        {
            MapVegetationCategories selectedTrees = categories & (MapVegetationCategories.OriginalTrees | MapVegetationCategories.BoundaryForest);
            if (selectedTrees == MapVegetationCategories.None ||
                selectedTrees == (MapVegetationCategories.OriginalTrees | MapVegetationCategories.BoundaryForest)) return;
            var selected = new HashSet<string>(cells.Select(c => "Forest_Cell_" + c.X + "_" + c.Z), StringComparer.Ordinal);
            Scene scene = EditorSceneManager.OpenScene(WorldBaseline06B2Paths.GlobalScene, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name != PreviousRoot) continue;
                    foreach (Transform cell in transform)
                    {
                        if (!all && !selected.Contains(cell.name)) continue;
                        foreach (Transform child in cell)
                            if (child.name == "Trees" && child.childCount > 0)
                                throw new InvalidOperationException("The previous generated forest mixes original and boundary trees in " + cell.name +
                                    ". Migrate Original Trees and Boundary Forest together once; subsequent regeneration may select either category independently.");
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        public static void Clear(MapVegetationRebuildOptions options, bool allCells)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard unsaved scene edits before clearing generated vegetation. No scene was changed.");
            var manifest = AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(WorldBaseline06B2Paths.ActiveManifest);
            if (manifest == null) throw new InvalidOperationException("The active streaming manifest is missing.");
            WorldCellIndex selected = ParseCell(options.SelectedCell);
            var entries = manifest.CellLayers.Where(c => c.LayerId == LayerId && (allCells || c.Index.Equals(selected))).ToArray();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string runId = "clear-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var retained = new List<ProductionWorldCellLayerScene>();
            try
            {
                Backup(WorldBaseline06B2Paths.ActiveManifest, runId);
                Backup("ProjectSettings/EditorBuildSettings.asset", runId);
                foreach (var entry in entries)
                {
                    if (!string.Equals(entry.ScenePath, CellScenePath(entry.Index), StringComparison.Ordinal))
                        throw new InvalidDataException("Refusing to clear a vegetation layer outside this generator's scene path: " + entry.ScenePath);
                    Backup(entry.ScenePath, runId);
                    Scene scene = EditorSceneManager.OpenScene(entry.ScenePath, OpenSceneMode.Additive);
                    try
                    {
                        ValidateGeneratedOwnership(scene, entry.Index.Id);
                        bool modified = false;
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                            if (group != null && group.GeneratorId == MapVegetationRebuildOptions.GeneratorId &&
                                Enum.TryParse(group.Category, out MapVegetationCategories category) && (options.Categories & category) != 0)
                            {
                                Object.DestroyImmediate(root);
                                modified = true;
                            }
                        }
                        if (!modified || scene.GetRootGameObjects().Length > 0) retained.Add(entry);
                        if (modified && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + entry.ScenePath);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                ProductionWorldCellLayerBuilder.ReplaceLayerScenes(manifest, LayerId, retained, entries.Select(c => c.Index).ToArray());
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static void WriteCellReport(string folder, MapVegetationCellPlan plan, CellReport report)
        {
            File.WriteAllText(folder + "/" + plan.Cell.Id + ".json", JsonUtility.ToJson(report, true));
            using var writer = new StreamWriter(folder + "/" + plan.Cell.Id + "-placements.csv", false, new UTF8Encoding(false));
            writer.WriteLine("id,category,cell,x,y,z,status,reason,source");
            foreach (MapVegetationPlacement p in plan.Woody)
                writer.WriteLine(Csv(p.id) + "," + p.category + "," + p.cellId + "," + Coordinates(p.position) + ",Accepted," + Csv(p.method) + "," + Csv(p.source));
            foreach (MapVegetationIssue issue in plan.Issues)
                writer.WriteLine(Csv(issue.id) + "," + issue.category + "," + issue.cellId + "," + Coordinates(issue.position) + ",Skipped," + Csv(issue.reason) + "," + Csv(issue.source));
            using var reasons = new StreamWriter(folder + "/" + plan.Cell.Id + "-reasons.csv", false, new UTF8Encoding(false));
            reasons.WriteLine("reason,count");
            foreach (var reason in plan.Rejections.OrderBy(p => p.Key, StringComparer.Ordinal)) reasons.WriteLine(Csv(reason.Key) + "," + reason.Value);
        }

        private static string Csv(string text) => "\"" + (text ?? string.Empty).Replace("\"", "\"\"") + "\"";
        private static string Coordinates(Vector3 p) => p.x.ToString("R", CultureInfo.InvariantCulture) + "," + p.y.ToString("R", CultureInfo.InvariantCulture) + "," + p.z.ToString("R", CultureInfo.InvariantCulture);
        private static string QuantizedMillimetrePosition(Vector3 position) =>
            Mathf.RoundToInt(position.x * 1000f) + "|" +
            Mathf.RoundToInt(position.y * 1000f) + "|" +
            Mathf.RoundToInt(position.z * 1000f);
        private static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = create(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
        private static void Backup(string path, string runId)
        {
            if (!File.Exists(path)) return;
            string target = Reports + "/Backups/" + runId + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target)) File.Copy(path, target);
            if (File.Exists(path + ".meta") && !File.Exists(target + ".meta")) File.Copy(path + ".meta", target + ".meta");
        }

        [Serializable] private sealed class PilotGate { public bool passed; public string cellId, fingerprint, settingsHash, sourceFingerprint, runId; }
        [Serializable] private sealed class CaptureGate { public bool passed; public string cellId, settingsHash, generatedFingerprint; }
        [Serializable] private sealed class SourceReport
        {
            public MapVegetationSourcePoint[] trees, shrubs;
            public MapVegetationRockAnchor[] rocks;
            public MapVegetationBoundarySegment[] boundarySegments;
            public MapVegetationSourceDiagnostic[] diagnostics;
            public string[] billboardSourcePaths, surfaceWarnings;
            public string sourceRevision, canonicalSceneSha256;
            public bool positionsAreProjectSpace;
            public int sourceMeshCount, connectedComponentCount, pairedCardCount, unpairedCardCount;
            public int groundTriangleCount, exclusionCount;
            public SourceReport(MapVegetationContext context)
            {
                MapVegetationSourceSnapshot source = context.Source;
                trees = source.Trees.ToArray(); shrubs = source.Shrubs.ToArray();
                rocks = source.Rocks.ToArray();
                boundarySegments = source.BoundarySegments.ToArray();
                diagnostics = source.Diagnostics.ToArray(); billboardSourcePaths = source.BillboardSourcePaths.ToArray();
                sourceRevision = source.SourceRevision; canonicalSceneSha256 = source.CanonicalSceneSha256;
                positionsAreProjectSpace = source.PositionsAreProjectSpace; sourceMeshCount = source.SourceMeshCount;
                connectedComponentCount = source.ConnectedComponentCount; pairedCardCount = source.PairedCardCount; unpairedCardCount = source.UnpairedCardCount;
                surfaceWarnings = context.Surfaces.Warnings.ToArray(); groundTriangleCount = context.Surfaces.GroundTriangleCount;
                exclusionCount = context.Surfaces.ExclusionCount;
            }
        }
        [Serializable] private sealed class RunReport
        {
            public string runId, scope, settingsHash, sourceFingerprint, failure;
            public bool passed, validationOnly;
            public int sourceTrees, sourceShrubs, sourceRocks, boundarySegments,
                sourceScenes, activeLegacyCardRenderers,
                activeLegacyCanonicalRockRenderers;
            public int globalOriginalTreePlanAccepted, globalOriginalTreePlanRejected;
            public int naturalInfillOriginalBasis, naturalInfillCandidates,
                naturalInfillEligible, naturalInfillAccepted,
                naturalInfillRejectedByCap,
                naturalInfillAcceptedOnOpenSpace;
            public int naturalInfillCellCount, naturalInfillCellsWithTrees,
                naturalInfillPerCellMinimum, naturalInfillPerCellMaximum;
            public float naturalInfillPerCellMedian;
            public int boundaryNearInwardCandidates,
                boundaryNearInwardAccepted,
                boundaryNearOutwardAccepted,
                boundaryNearAcceptedOnOpenSpace,
                boundaryNearTreeBudget,
                boundaryNearCandidatesSkippedAtCap,
                boundaryNearForestFloorAccepted,
                boundaryNearForestFloorBudget,
                boundaryNearForestFloorCandidatesSkippedAtCap;
            public int distantForestEligible, distantForestAccepted,
                distantForestRejectedByCap, distantForestCandidateCount,
                distantForestSilhouetteFallbackCount,
                distantForestCoverageBackboneCount,
                distantForestCoverageSilhouetteOverrideCount,
                distantForestOuterEnvelopeCount,
                distantForestSyntheticClosureSegmentCount;
            public float distantForestBoundaryLengthMeters,
                distantForestAcceptedMinimumBoundaryDistanceMeters,
                distantForestAcceptedMaximumBoundaryDistanceMeters;
            public RejectionCount[] distantForestRejectionReasons;
            public int sourceTreeProvenancePathCount,
                sourceTreeDuplicateIdCount,
                sourceTreeDuplicateMillimetrePositionCount;
            public int forestLoadingRadiusCells,
                maximumResidentForestCellLayers;
            public RejectionCount[] naturalInfillRejectionReasons;
            public RejectionCount[] globalOriginalTreeRejectionReasons;
            public MapVegetationGlobalPresentation.GlobalPresentationReport
                globalPresentation;
            public MapVegetationBackdropPresentation.BackdropPresentationReport
                backdropPresentation;
            public List<CellReport> cells = new List<CellReport>();
        }
        [Serializable] private sealed class RejectionCount { public string reason; public int count; }
        [Serializable] private sealed class CellReport
        {
            public string cellId, scenePath, fingerprint;
            public int originalTrees, naturalInfillTrees, sourceRejected,
                boundaryTrees, rockAccentPlacements, shrubs, grass,
                grassCandidates;
            public int[] grassProfileCounts;
            public int spruce, pine, birch, aspen;
            public int generatedRootCount, generatedGameObjects,
                generatedComponents, savedSceneGameObjects,
                savedSceneComponents, prefabInstanceGameObjects,
                directMeshRenderers, serializedColliderComponents;
            public int packedPrototypeCount, packedBatchCount,
                packedMatrixCount, packedPlacementMetadataCount,
                packedCollisionRecords,
                packedOriginalDonorCollisionRecords,
                packedNaturalInfillCollisionRecords,
                collisionPoolEstimatedMaximumCandidates,
                collisionPoolEstimatedOverflow;
            public long serializedSceneBytes, packedAssetBytes,
                packedAssetByteBudget;
            public double packedAssetBytesPerPlacement;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }
    }
}
