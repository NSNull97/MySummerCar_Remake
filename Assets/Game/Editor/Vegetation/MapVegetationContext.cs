using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Editor.WorldBaseline;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Vegetation
{
    /// <summary>Snapshots source evidence and exclusions; never writes input scenes.</summary>
    public sealed class MapVegetationContext : IDisposable
    {
        private readonly SceneSetup[] originalSetup;
        private MapVegetationAssetLease assetLease;
        private bool disposed;
        private string scratchScenePath;
        public readonly MapVegetationRebuildOptions Options;
        public readonly ProductionWorldStreamingManifest Manifest;
        public readonly MapVegetationSurfaceQuery Surfaces;
        public readonly MapVegetationSourceSnapshot Source;
        public readonly Dictionary<WorldCellIndex, MapVegetationCellPlan> WoodyCells = new Dictionary<WorldCellIndex, MapVegetationCellPlan>();
        public readonly List<MapVegetationPlacement> DistantBackdrop =
            new List<MapVegetationPlacement>();
        public readonly MapVegetationPlanning.SpacingIndex TreeSpacing;
        public readonly HashSet<WorldCellIndex> Cells = new HashSet<WorldCellIndex>();
        private readonly string settingsFingerprint;
        private readonly HashSet<string> sourceTreeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> sourceShrubIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> sourceRockIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> sourceBoundaryIds = new HashSet<string>(StringComparer.Ordinal);
        public int SourceScenes;
        public int NaturalInfillOriginalBasisCount { get; private set; }
        public int NaturalInfillCandidateCount { get; private set; }
        public int NaturalInfillEligibleCount { get; private set; }
        public int NaturalInfillAcceptedCount { get; private set; }
        public int NaturalInfillRejectedByCap { get; private set; }
        public int NaturalInfillAcceptedOnOpenSpaceCount { get; private set; }
        public readonly Dictionary<string, int> NaturalInfillRejections =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public int BoundaryNearInwardCandidateCount { get; private set; }
        public int BoundaryNearInwardAcceptedCount { get; private set; }
        public int BoundaryNearOutwardAcceptedCount { get; private set; }
        public int BoundaryNearAcceptedOnOpenSpaceCount { get; private set; }
        public int BoundaryNearCandidatesSkippedAtCap { get; private set; }
        public int BoundaryNearForestFloorAcceptedCount { get; private set; }
        public int BoundaryNearForestFloorCandidatesSkippedAtCap
            { get; private set; }
        public int DistantForestEligibleCount { get; private set; }
        public int DistantForestRejectedByCap { get; private set; }
        public int DistantForestCandidateCount { get; private set; }
        public int DistantForestSilhouetteFallbackCount { get; private set; }
        public int DistantForestCoverageBackboneCount { get; private set; }
        public int DistantForestCoverageSilhouetteOverrideCount
            { get; private set; }
        public float DistantForestCoverageAlongSpacingMeters
            { get; private set; }
        public float DistantForestCoverageMinimumHeightMeters
            { get; private set; }
        public float DistantForestCoverageMinimumScaledCrownRadiusMeters
            { get; private set; }
        public int DistantForestOuterEnvelopeCount { get; private set; }
        public int DistantForestSyntheticClosureSegmentCount
            { get; private set; }
        public float DistantForestBoundaryLengthMeters { get; private set; }
        public float DistantForestAcceptedMinimumBoundaryDistanceMeters
            { get; private set; }
        public float DistantForestAcceptedMaximumBoundaryDistanceMeters
            { get; private set; }
        public readonly Dictionary<string, int> DistantForestRejections =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public MapVegetationForestFloorEcologyReport ForestFloorEcologyReport
            { get; private set; }
        public string SourceFingerprint { get; private set; }

        public MapVegetationContext(MapVegetationRebuildOptions options)
        {
            Options = options;
            if (options == null) throw new ArgumentNullException(nameof(options));
            string error = options.Validate();
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard your unsaved scene edits before generating vegetation. No scene was changed.");
            originalSetup = EditorSceneManager.GetSceneManagerSetup();
            Manifest = AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(WorldBaseline06B2Paths.ActiveManifest);
            if (Manifest == null || !Manifest.PrivateLocalRuntimeBaseline)
                throw new InvalidOperationException("A private Phase 1 streaming manifest is required.");
            // Scene replacement unloads editor-only assets even when ordinary C#
            // fields still reference their managed wrappers. Hold native references
            // from a temporary DontUnload object throughout snapshot/generation.
            assetLease = ScriptableObject.CreateInstance<MapVegetationAssetLease>();
            assetLease.hideFlags = HideFlags.HideAndDontSave;
            assetLease.Keep(options, options.Placement, Manifest);
            settingsFingerprint = MapVegetationPlanning.FingerprintSettings(options);
            Surfaces = MapVegetationSurfaceQuery.Build(Array.Empty<GameObject>(), options.Placement);
            TreeSpacing = new MapVegetationPlanning.SpacingIndex(options.Placement.Category(MapVegetationKind.Tree).MinimumSpacingMeters);
            try
            {
                string global = string.IsNullOrWhiteSpace(options.Placement.SourceGlobalScenePath)
                    ? WorldBaseline06B2Paths.GlobalScene : options.Placement.SourceGlobalScenePath;
                Scene sourceScene = EditorSceneManager.OpenScene(global, OpenSceneMode.Single);
                GameObject[] roots = sourceScene.GetRootGameObjects();
                Source = new MapVegetationSourceSnapshot();
                MergeSource(MapVegetationDonorSource.Read(roots));
                Surfaces.AddRoots(roots);
                SourceScenes++;
                IEnumerable<string> cellPaths = options.Placement.SourceCellScenePaths.Count > 0
                    ? options.Placement.SourceCellScenePaths : Manifest.Cells.Select(c => c.ScenePath);
                foreach (string path in cellPaths.Distinct().OrderBy(p => p, StringComparer.Ordinal))
                {
                    Scene cell = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        GameObject[] cellRoots = cell.GetRootGameObjects();
                        MergeSource(MapVegetationDonorSource.Read(cellRoots));
                        Surfaces.AddRoots(cellRoots);
                        SourceScenes++;
                    }
                    finally { EditorSceneManager.CloseScene(cell, true); }
                }
                foreach (Bounds bounds in Surfaces.GroundBounds)
                {
                    WorldCellIndex min = MapVegetationPlanning.CellAt(bounds.min, Manifest.CellSizeMeters);
                    WorldCellIndex max = MapVegetationPlanning.CellAt(bounds.max - new Vector3(0.001f, 0f, 0.001f), Manifest.CellSizeMeters);
                    for (int z = min.Z; z <= max.Z; z++)
                    for (int x = min.X; x <= max.X; x++) Cells.Add(new WorldCellIndex(x, z));
                }
                SourceFingerprint = ComputeSourceFingerprint(Source, Surfaces.ComputeGeometryFingerprint());
                // Release the old globally serialized forest before creating any replacements.
                // A saved, owned scratch scene avoids Unity's single-unsaved-scene
                // restriction when WriteCell subsequently opens an additive scene.
                Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string scratchFolder = MapVegetationRebuildOptions.GeneratedRoot + "/EditorScratch";
                EnsureAssetFolder(scratchFolder);
                scratchScenePath = scratchFolder + "/Context_" + Guid.NewGuid().ToString("N") + ".unity";
                if (!EditorSceneManager.SaveScene(scratch, scratchScenePath))
                    throw new IOException("Could not save the owned empty vegetation scratch scene.");
                BuildWoody();
                MapVegetationTreeMixture.Assign(
                    WoodyCells.Values.SelectMany(p => p.Woody)
                        .Concat(DistantBackdrop),
                    options.TreeSpeciesPercentages);
                Debug.Log($"MAP_VEGETATION_SOURCE_READY scenes={SourceScenes} trees={Source.Trees.Count} shrubs={Source.Shrubs.Count} rocks={Source.Rocks.Count} boundaries={Source.BoundarySegments.Count} distant={DistantBackdrop.Count} cells={Cells.Count}");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EditorUtility.ClearProgressBar();
            try
            {
                if (originalSetup != null && originalSetup.Any(setup => !string.IsNullOrEmpty(setup.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            finally
            {
                if (!string.IsNullOrEmpty(scratchScenePath))
                {
                    Scene scratch = SceneManager.GetSceneByPath(scratchScenePath);
                    if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
                    // This is exactly the unique empty scene created above, not a
                    // directory cleanup and never a user's input/generated cell.
                    if (!AssetDatabase.DeleteAsset(scratchScenePath) && File.Exists(scratchScenePath))
                        Debug.LogWarning("Could not remove owned vegetation scratch scene: " + scratchScenePath);
                }
                if (assetLease != null) UnityEngine.Object.DestroyImmediate(assetLease);
            }
        }

        internal static string ComputeSourceFingerprint(MapVegetationSourceSnapshot source, string geometryFingerprint)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrEmpty(geometryFingerprint)) throw new ArgumentException("Copied ground/exclusion fingerprint is required.", nameof(geometryFingerprint));
            using var sha = SHA256.Create();
            using var stream = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write("msc.map-vegetation-source.v1");
            writer.Write(source.SourceRevision ?? string.Empty);
            writer.Write(source.CanonicalSceneSha256 ?? string.Empty);
            writer.Write(source.PositionsAreProjectSpace);
            WriteSourcePoints(writer, "trees", source.Trees);
            WriteSourcePoints(writer, "shrubs", source.Shrubs);
            writer.Write("rocks");
            writer.Write(source.Rocks.Count);
            foreach (MapVegetationRockAnchor rock in source.Rocks
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                writer.Write(rock.Id ?? string.Empty);
                WriteVector(writer, rock.Position);
                WriteVector(writer, rock.SourceSize);
                writer.Write(rock.Yaw);
                writer.Write(rock.CellId ?? string.Empty);
                writer.Write(rock.ReplacementKey ?? string.Empty);
                writer.Write(rock.SourceStableId ?? string.Empty);
                writer.Write(rock.SourcePath ?? string.Empty);
                writer.Write(rock.SourceMeshGuid ?? string.Empty);
                writer.Write(rock.SourceHash ?? string.Empty);
                writer.Write(rock.Method ?? string.Empty);
            }
            writer.Write("boundaries");
            writer.Write(source.BoundarySegments.Count);
            foreach (MapVegetationBoundarySegment segment in source.BoundarySegments.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                writer.Write(segment.Id ?? string.Empty);
                WriteVector(writer, segment.A); WriteVector(writer, segment.B);
                WriteVector(writer, segment.AdjacentTriangleNormal);
                WriteVector(writer, segment.Outward);
                writer.Write(segment.HasValidatedOutward);
                writer.Write(segment.EnclosureId ?? string.Empty);
                writer.Write(segment.IsOuterEnvelope);
                writer.Write(segment.EnclosureClosedBySmallGap);
                writer.Write(segment.EnclosureSignedArea);
                writer.Write(segment.EnclosurePerimeter);
                writer.Write(segment.PositiveTreeSideEvidenceCount);
                writer.Write(segment.NegativeTreeSideEvidenceCount);
                writer.Write(segment.TreeSideConfidence);
                writer.Write(segment.OrientationMethod ?? string.Empty);
                writer.Write(segment.SourceStableId ?? string.Empty);
                writer.Write(segment.SourceMeshGuid ?? string.Empty);
                writer.Write(segment.SourceHash ?? string.Empty);
            }
            writer.Write(geometryFingerprint);
            writer.Flush();
            stream.FlushFinalBlock();
            return BitConverter.ToString(sha.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void WriteSourcePoints(BinaryWriter writer, string category, List<MapVegetationSourcePoint> points)
        {
            writer.Write(category); writer.Write(points.Count);
            foreach (MapVegetationSourcePoint point in points.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                writer.Write(point.Id ?? string.Empty); WriteVector(writer, point.Position); writer.Write(point.Height);
                writer.Write(point.Method ?? string.Empty); writer.Write(point.Species ?? string.Empty);
                writer.Write(point.SourceStableId ?? string.Empty); writer.Write(point.SourceMeshGuid ?? string.Empty);
                writer.Write(point.SourceHash ?? string.Empty); writer.Write(point.SecondarySourceStableId ?? string.Empty);
                writer.Write(point.SecondarySourceMeshGuid ?? string.Empty); writer.Write(point.SecondarySourceHash ?? string.Empty);
            }
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public MapVegetationCellPlan CellPlan(WorldCellIndex cell, bool includeGrass)
        {
            var plan = new MapVegetationCellPlan { Cell = cell, SettingsFingerprint = settingsFingerprint };
            if (WoodyCells.TryGetValue(cell, out MapVegetationCellPlan woody))
            {
                plan.Woody.AddRange(woody.Woody.Where(p => (Options.Categories & p.category) != 0));
                plan.Issues.AddRange(woody.Issues);
                foreach (var pair in woody.Rejections) plan.Rejections[pair.Key] = pair.Value;
            }
            if (includeGrass && (Options.Categories & MapVegetationCategories.GrassCoverage) != 0) BuildGrass(plan);
            plan.Fingerprint = MapVegetationPlanning.Fingerprint(plan);
            return plan;
        }

        private void MergeSource(MapVegetationSourceSnapshot snapshot)
        {
            foreach (MapVegetationSourcePoint tree in snapshot.Trees)
                if (sourceTreeIds.Add(tree.Id)) Source.Trees.Add(tree);
            foreach (MapVegetationSourcePoint shrub in snapshot.Shrubs)
                if (sourceShrubIds.Add(shrub.Id)) Source.Shrubs.Add(shrub);
            foreach (MapVegetationRockAnchor rock in snapshot.Rocks)
                if (sourceRockIds.Add(rock.Id)) Source.Rocks.Add(rock);
            foreach (MapVegetationBoundarySegment boundary in snapshot.BoundarySegments)
                if (sourceBoundaryIds.Add(boundary.Id)) Source.BoundarySegments.Add(boundary);
            foreach (string path in snapshot.BillboardSourcePaths)
                if (!Source.BillboardSourcePaths.Contains(path)) Source.BillboardSourcePaths.Add(path);
            Source.Diagnostics.AddRange(snapshot.Diagnostics);
            Source.SourceMeshCount += snapshot.SourceMeshCount;
            Source.ConnectedComponentCount += snapshot.ConnectedComponentCount;
            Source.PairedCardCount += snapshot.PairedCardCount;
            Source.UnpairedCardCount += snapshot.UnpairedCardCount;
        }

        private void BuildWoody()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            foreach (MapVegetationSourcePoint point in Source.Trees.OrderBy(p => p.Id, StringComparer.Ordinal))
                Place(point.Id, point.Position, point.SourcePath, point.Method, point.Species, point.Height,
                    MapVegetationCategories.OriginalTrees, MapVegetationKind.Tree, TreeSpacing,
                    alreadyMapped: false);
            MapVegetationPlanning.SpacingIndex sourceTreeEvidence =
                BuildOriginalTreeEvidenceIndex();
            BuildNaturalTreeInfill(sourceTreeEvidence);
            var understory = new MapVegetationPlanning.SpacingIndex(
                MapVegetationForestFloorBindings.Policy(MapVegetationForestFloorBindings.Pool.Understory).MinimumSpacingMeters);
            var shrubs = new MapVegetationPlanning.SpacingIndex(
                MapVegetationForestFloorBindings.Policy(MapVegetationForestFloorBindings.Pool.Shrubs).MinimumSpacingMeters);
            foreach (MapVegetationSourcePoint point in Source.Shrubs.OrderBy(p => p.Id, StringComparer.Ordinal))
                PlaceForestFloor(point.Id, point.Position, point.SourcePath, point.Method,
                    MapVegetationForestFloorBindings.Pool.Shrubs, shrubs,
                    alreadyMapped: false, applyPoolDensity: false);

            // The source is already in project space. This authored matrix is an
            // optional correction, never the canonical raw-to-project offset again.
            Matrix4x4 mapping = Options.Placement.DonorCoordinateMatrix;
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> outward =
                MapVegetationPlanning.SignedOutwardBoundaryCandidates(
                    Source.BoundarySegments,
                    Options.BoundaryCandidateSpacingMeters, Options.BoundaryDepthMeters,
                    Options.Placement.Seed ^ 419, mapping);
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> inward =
                MapVegetationPlanning.SignedInwardBoundaryCandidates(
                    Source.BoundarySegments,
                    Options.BoundaryCandidateSpacingMeters,
                    Options.BoundaryDepthMeters,
                    Options.Placement.Seed ^ 419, mapping);
            BoundaryNearInwardCandidateCount = inward.Count;
            NearBoundaryCandidate[] nearTreeCandidates = outward.Select(
                    candidate => CreateNearBoundaryCandidate(candidate,
                        inwardSide: false, Options, 1123, 2579u))
                .Concat(inward.Where(candidate =>
                        sourceTreeEvidence.HasSurroundingNeighbors(
                            candidate.Position,
                            Options.NaturalInfillForestInfluenceMeters))
                    .Select(candidate => CreateNearBoundaryCandidate(candidate,
                        inwardSide: true, Options, 1123, 2579u)))
                .Where(candidate => candidate.DensityPassed)
                .OrderBy(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Id,
                    StringComparer.Ordinal).ToArray();
            int acceptedNearTrees = 0;
            for (int index = 0; index < nearTreeCandidates.Length; index++)
            {
                if (acceptedNearTrees >= Options.BoundaryMaximumTrees)
                {
                    BoundaryNearCandidatesSkippedAtCap =
                        nearTreeCandidates.Length - index;
                    break;
                }
                NearBoundaryCandidate planned = nearTreeCandidates[index];
                MapVegetationPlanning.BoundaryCandidate candidate =
                    planned.Candidate;
                // Inward candidates require surrounding accepted original-tree
                // evidence; OpenSpace and all other exclusions remain hard
                // surface vetoes. Outward candidates still require real ground.
                if (Place(planned.Id, candidate.Position, candidate.SourceId,
                    planned.InwardSide
                        ? "BoundaryFrontInwardOnExistingGround"
                        : "BoundaryOuterOnExistingGround",
                    "mixed", planned.Height,
                    MapVegetationCategories.BoundaryForest,
                    MapVegetationKind.Tree, TreeSpacing,
                    alreadyMapped: true))
                    acceptedNearTrees++;
            }

            // Undergrowth has an independent grid: its exposed spacing control
            // must work even when tree density or trunk exclusions change.
            outward = MapVegetationPlanning.SignedOutwardBoundaryCandidates(
                Source.BoundarySegments,
                Options.UndergrowthSpacingMeters, Options.BoundaryDepthMeters, Options.Placement.Seed ^ 761, mapping);
            inward = MapVegetationPlanning.SignedInwardBoundaryCandidates(
                Source.BoundarySegments,
                Options.UndergrowthSpacingMeters, Options.BoundaryDepthMeters,
                Options.Placement.Seed ^ 761, mapping);
            NearBoundaryCandidate[] nearFloorCandidates = outward.Select(
                    candidate => CreateNearBoundaryCandidate(candidate,
                        inwardSide: false, Options, 1867, 0u))
                .Concat(inward.Where(candidate =>
                        sourceTreeEvidence.HasSurroundingNeighbors(
                            candidate.Position,
                            Options.NaturalInfillForestInfluenceMeters))
                    .Select(candidate => CreateNearBoundaryCandidate(candidate,
                        inwardSide: true, Options, 1867, 0u)))
                .Where(candidate => VegetationStableHash.ToUnitFloat(
                    candidate.Random) < Mathf.Clamp01(
                    Options.UndergrowthDensity))
                .OrderBy(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Id,
                    StringComparer.Ordinal).ToArray();
            for (int index = 0; index < nearFloorCandidates.Length; index++)
            {
                if (BoundaryNearForestFloorAcceptedCount >=
                    Options.BoundaryMaximumForestFloor)
                {
                    BoundaryNearForestFloorCandidatesSkippedAtCap =
                        nearFloorCandidates.Length - index;
                    break;
                }
                NearBoundaryCandidate planned = nearFloorCandidates[index];
                MapVegetationPlanning.BoundaryCandidate candidate =
                    planned.Candidate;
                uint random = planned.Random;
                bool useShrub = (random & 3u) == 0u;
                MapVegetationForestFloorBindings.Pool pool = useShrub
                    ? MapVegetationForestFloorBindings.Pool.Shrubs
                    : MapVegetationForestFloorBindings.Pool.Understory;
                if (PlaceForestFloor("undergrowth:" + candidate.X + ":" +
                    candidate.Z, candidate.Position,
                    candidate.SourceId, planned.InwardSide
                        ? "BoundaryForestFloorInwardDonorSurrounded"
                        : "BoundaryForestFloorOutward", pool,
                    useShrub ? shrubs : understory, alreadyMapped: true,
                    applyPoolDensity: false))
                    BoundaryNearForestFloorAcceptedCount++;
            }

            MapVegetationPlacement[] nearBoundary = WoodyCells.Values
                .SelectMany(plan => plan.Woody)
                .Where(placement => placement.category ==
                    MapVegetationCategories.BoundaryForest).ToArray();
            BoundaryNearInwardAcceptedCount = nearBoundary.Count(placement =>
                placement.method ==
                "BoundaryFrontInwardOnExistingGround");
            BoundaryNearOutwardAcceptedCount = nearBoundary.Count(placement =>
                placement.method == "BoundaryOuterOnExistingGround");
            BoundaryNearAcceptedOnOpenSpaceCount = nearBoundary.Count(
                placement => Surfaces.TryGetExclusion(placement.position,
                    MapVegetationKind.Tree,
                    out MapVegetationExclusionKind exclusion, out _) &&
                    exclusion == MapVegetationExclusionKind.OpenSpace);
            if (BoundaryNearAcceptedOnOpenSpaceCount != 0)
                throw new InvalidDataException(
                    "Near boundary trees entered a donor OpenSpace footprint.");
            if (BoundaryNearInwardCandidateCount > 0 &&
                BoundaryNearInwardAcceptedCount == 0)
                throw new InvalidDataException(
                    "Signed boundary planning produced no donor-surrounded inward front layer.");
            BuildDistantForestBackdrop(mapping);
            // Species must be final before forest-floor planning: conifers own
            // twig/branch litter while birch/aspen own deciduous leaf litter.
            // The constructor repeats the same exact assignment after this
            // method, which is intentionally idempotent.
            MapVegetationTreeMixture.Assign(
                WoodyCells.Values.SelectMany(plan => plan.Woody)
                    .Concat(DistantBackdrop),
                Options.TreeSpeciesPercentages);
            BuildEcologicalForestFloor();
            Debug.Log($"MAP_VEGETATION_WOODY_TIMING seconds={timer.Elapsed.TotalSeconds:F2} {Surfaces.DiagnosticSummary}");
        }

        private MapVegetationPlanning.SpacingIndex
            BuildOriginalTreeEvidenceIndex()
        {
            var evidence = new MapVegetationPlanning.SpacingIndex(
                Options.NaturalInfillForestInfluenceMeters);
            foreach (MapVegetationPlacement placement in WoodyCells.Values
                         .SelectMany(plan => plan.Woody)
                         .Where(placement => placement.category ==
                             MapVegetationCategories.OriginalTrees))
                evidence.Add(placement.position);
            return evidence;
        }

        private void BuildNaturalTreeInfill(
            MapVegetationPlanning.SpacingIndex sourceInfluence)
        {
            float spacing = Options.NaturalInfillSpacingMeters;
            float minimumTreeDistance = Mathf.Max(
                Options.NaturalInfillMinimumTreeDistanceMeters,
                Options.Placement.Category(MapVegetationKind.Tree)
                    .MinimumSpacingMeters);
            var originalPositions = new List<Vector3>();
            foreach (MapVegetationCellPlan plan in WoodyCells.Values)
            foreach (MapVegetationPlacement placement in plan.Woody)
                if (placement.category == MapVegetationCategories.OriginalTrees)
                {
                    originalPositions.Add(placement.position);
                    NaturalInfillOriginalBasisCount++;
                }

            var visited = new HashSet<Vector2Int>();
            var resolved = new List<ResolvedTreeCandidate>();
            foreach (Bounds bounds in Surfaces.GroundBounds)
            {
                int minimumX = Mathf.FloorToInt(bounds.min.x / spacing);
                int maximumX = Mathf.CeilToInt(bounds.max.x / spacing);
                int minimumZ = Mathf.FloorToInt(bounds.min.z / spacing);
                int maximumZ = Mathf.CeilToInt(bounds.max.z / spacing);
                for (int z = minimumZ; z <= maximumZ; z++)
                for (int x = minimumX; x <= maximumX; x++)
                {
                    var grid = new Vector2Int(x, z);
                    if (!visited.Add(grid)) continue;
                    NaturalInfillCandidateCount++;
                    Vector3 candidate = MapVegetationPlanning.Candidate(
                        x, z, spacing, Options.Placement.Seed ^ 0x39B17);
                    uint random = VegetationStableHash.Hash(
                        x, z, Options.Placement.Seed ^ 0x5A71D);
                    string id = $"natural-infill:{x}:{z}";
                    uint placementRandom = MapVegetationPlanning.HashId(
                        id, Options.Placement.Seed);
                    // Donor canopy is the positive forest mask. Multidirectional
                    // evidence still vetoes one-sided fields/clearings, while a
                    // continuous confidence makes the edge thin out instead of
                    // cutting a binary ring of random bald patches.
                    MapVegetationPlanning.ForestNeighborhood neighborhood =
                        sourceInfluence.MeasureForestNeighborhood(candidate,
                            Options.NaturalInfillForestInfluenceMeters,
                            Options.NaturalInfillAngularSectorCount);
                    float forestFactor = neighborhood
                        .SmoothInteriorDensityFactor(
                            Options.NaturalInfillMinimumDonorTrees,
                            Options.NaturalInfillMaximumEmptyArcDegrees);
                    if (forestFactor <= 0f)
                    {
                        RejectNaturalInfill(
                            "InsufficientSurroundingDonorForest");
                        continue;
                    }
                    float clusterFactor = MapVegetationPlanning
                        .StableForestClusterFactor(candidate,
                            Options.NaturalInfillClusterScaleMeters,
                            Options.Placement.Seed ^ 0x2C71D);
                    float acceptance = Mathf.Clamp01(
                        Options.NaturalInfillDensity * forestFactor *
                        clusterFactor);
                    if (VegetationStableHash.ToUnitFloat(random) >= acceptance ||
                        VegetationStableHash.ToUnitFloat(
                            VegetationStableHash.Hash(
                                placementRandom ^ 2917u)) >=
                        Options.Placement.Category(MapVegetationKind.Tree)
                            .Density)
                    {
                        RejectNaturalInfill("Density");
                        continue;
                    }
                    if (TreeSpacing.HasNeighbor(candidate,
                            minimumTreeDistance))
                    {
                        RejectNaturalInfill("NearExistingTree");
                        continue;
                    }
                    if (!MapVegetationPlanning.TryResolveTreePlacementSurface(
                            Surfaces, candidate,
                            out MapVegetationSurfaceHit hit,
                            out string surfaceReason))
                    {
                        RejectNaturalInfill("Surface:" +
                            RejectionFamily(surfaceReason));
                        continue;
                    }
                    float height = Mathf.Lerp(Options.NaturalInfillHeightRange.x,
                        Options.NaturalInfillHeightRange.y,
                        VegetationStableHash.ToUnitFloat(
                            VegetationStableHash.Hash(random ^ 0xA917C31u)));
                    resolved.Add(new ResolvedTreeCandidate(id, candidate, hit,
                        height, placementRandom,
                        VegetationStableHash.Hash(
                            placementRandom ^ 0xD3517A9u)));
                }
            }

            int cap = MapVegetationPlanning.NaturalInfillBudget(
                NaturalInfillOriginalBasisCount,
                Options.NaturalInfillMaximumOriginalFraction);
            // Build one deterministic, globally ranked independent set before
            // applying the hard cap. Cell/ground-bound iteration order cannot
            // make the first visited part of the map consume the whole budget.
            IReadOnlyList<ResolvedTreeCandidate> accepted =
                MapVegetationPlanning.SelectStableCappedIndependentSet(
                    resolved, originalPositions, item => item.Score,
                    item => item.Id, item => item.Hit.Position,
                    minimumTreeDistance, cap,
                    out int eligibleCount);
            NaturalInfillEligibleCount = eligibleCount;
            NaturalInfillAcceptedCount = accepted.Count;
            NaturalInfillRejectedByCap = eligibleCount - accepted.Count;
            NaturalInfillAcceptedOnOpenSpaceCount = accepted.Count(candidate =>
                Surfaces.TryGetExclusion(candidate.Hit.Position,
                    MapVegetationKind.Tree,
                    out MapVegetationExclusionKind exclusion, out _) &&
                exclusion == MapVegetationExclusionKind.OpenSpace);
            if (NaturalInfillAcceptedOnOpenSpaceCount != 0)
                throw new InvalidDataException(
                    "Natural tree infill entered a donor OpenSpace footprint.");
            foreach (ResolvedTreeCandidate candidate in accepted)
                CommitNaturalInfill(candidate);
        }

        private void BuildDistantForestBackdrop(Matrix4x4 mapping)
        {
            MapVegetationBoundarySegment[] distantEnvelope =
                MapVegetationPlanning.BuildDistantOuterEnvelopeSegments(
                    Source.BoundarySegments);
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure> enclosures =
                MapVegetationPlanning.BuildBoundaryEnclosures(
                    distantEnvelope);
            MapVegetationPlanning.BoundaryEnclosure[] outer = enclosures
                .Where(enclosure => enclosure.IsOuterEnvelope).ToArray();
            DistantForestOuterEnvelopeCount = outer.Length;
            DistantForestSyntheticClosureSegmentCount = outer.Sum(enclosure =>
                enclosure.Segments.Count(segment =>
                    segment.IsSyntheticClosure));
            DistantForestBoundaryLengthMeters = outer.Sum(enclosure =>
                enclosure.Segments.Sum(segment => Vector3.Distance(
                    mapping.MultiplyPoint3x4(segment.Segment.A),
                    mapping.MultiplyPoint3x4(segment.Segment.B))));
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> candidates =
                MapVegetationPlanning.SignedOutwardBoundaryCandidates(
                    distantEnvelope,
                    Options.DistantForestSpacingMeters,
                    Options.DistantForestDepthMeters,
                    Options.Placement.Seed ^ 0x2671,
                    mapping);
            var resolved = new List<DistantResolvedCandidate>();
            var boundaryDistances = new Dictionary<string, float>(
                StringComparer.Ordinal);
            foreach (MapVegetationPlanning.BoundaryCandidate candidate in candidates)
            {
                if (candidate.Distance + 0.001f <
                    Options.DistantForestMinimumBoundaryDistanceMeters)
                {
                    RejectDistant("InsideMinimumBoundaryOffset");
                    continue;
                }
                if (TreeSpacing.HasNeighbor(candidate.Position,
                        Options.DistantForestMinimumBoundaryDistanceMeters *
                        0.55f))
                {
                    RejectDistant("NearPlayableTree");
                    continue;
                }
                uint random = VegetationStableHash.Hash(candidate.X,
                    candidate.Z, Options.Placement.Seed ^ 0x6179);
                bool densityPassed = VegetationStableHash.ToUnitFloat(random) <
                                     Options.DistantForestDensity;
                bool silhouetteFallback = false;
                MapVegetationSurfaceHit hit;
                if (!MapVegetationPlanning.TryResolveTreePlacementSurface(
                        Surfaces, candidate.Position, out hit,
                        out string reason))
                {
                    if (!string.Equals(reason, "NoAllowedGround",
                            StringComparison.Ordinal))
                    {
                        RejectDistant("Surface:" + RejectionFamily(reason));
                        continue;
                    }
                    Vector3 silhouette = candidate.Position;
                    silhouette.y = candidate.BoundaryPoint.y + Options
                        .Placement.Category(MapVegetationKind.Tree)
                        .SurfaceOffsetMeters;
                    if (Surfaces.IsExcludedWithoutGround(silhouette,
                            MapVegetationKind.Tree,
                            out string exclusionReason))
                    {
                        RejectDistant("SilhouetteExclusion:" +
                                      RejectionFamily(exclusionReason));
                        continue;
                    }
                    var silhouetteSurface = new SurfaceInfo(
                        "BoundarySilhouette:" + candidate.SourceId,
                        "CollisionlessDistantSilhouette", null, 0, null);
                    hit = new MapVegetationSurfaceHit(silhouette,
                        Vector3.up, silhouetteSurface);
                    silhouetteFallback = true;
                }
                float height = Mathf.Lerp(Options.DistantForestHeightRange.x,
                    Options.DistantForestHeightRange.y,
                    VegetationStableHash.ToUnitFloat(
                        VegetationStableHash.Hash(random ^ 0xB731C4Du)));
                string id = $"distant-forest:{candidate.X}:{candidate.Z}";
                boundaryDistances[id] = candidate.Distance;
                var tree = new ResolvedTreeCandidate(id,
                    candidate.Position, hit, height, random,
                    VegetationStableHash.Hash(random ^ 0x5D419A7u),
                    candidate.SourceId, silhouetteFallback);
                int band = MapVegetationPlanning.DistantDepthBand(
                    candidate.Distance,
                    Options.DistantForestMinimumBoundaryDistanceMeters,
                    Options.DistantForestDepthMeters);
                if (band < 0)
                {
                    RejectDistant("DepthBand");
                    continue;
                }
                Vector2 edge = new Vector2(
                    candidate.BoundaryPoint.x,
                    candidate.BoundaryPoint.z);
                resolved.Add(new DistantResolvedCandidate(tree,
                    densityPassed, band, edge,
                    isCoverageBackbone: false,
                    usesCoverageSurfaceOverride: false));
            }

            DistantForestCoverageMinimumHeightMeters =
                MapVegetationPlanning.DistantCoverageBackboneMinimumHeight(
                    Options.DistantForestHeightRange);
            DistantForestCoverageMinimumScaledCrownRadiusMeters =
                MapVegetationGlobalPresentation
                    .MinimumScaledDistantCrownRadius(
                        DistantForestCoverageMinimumHeightMeters);
            DistantForestCoverageAlongSpacingMeters = Mathf.Min(
                Options.DistantForestSpacingMeters,
                MapVegetationPlanning.MaximumDistantCrownCenterSpacing(
                    DistantForestCoverageMinimumScaledCrownRadiusMeters));
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate>
                coverageTargets = MapVegetationPlanning
                    .DistantCoverageBackboneCandidates(distantEnvelope,
                        Options.DistantForestMinimumBoundaryDistanceMeters,
                        Options.DistantForestDepthMeters,
                        DistantForestCoverageAlongSpacingMeters, mapping);
            DistantForestCandidateCount = checked(candidates.Count +
                coverageTargets.Count);
            var coverageBackbone = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (MapVegetationPlanning.BoundaryCandidate candidate in
                     coverageTargets)
            {
                int band = MapVegetationPlanning.DistantDepthBand(
                    candidate.Distance,
                    Options.DistantForestMinimumBoundaryDistanceMeters,
                    Options.DistantForestDepthMeters);
                if (band < 0)
                    throw new InvalidDataException(
                        "Mandatory distant coverage target escaped its signed depth band.");
                string id = "distant-forest-coverage:" +
                            candidate.SourceId + ":" + band + ":" +
                            candidate.X;
                if (!coverageBackbone.Add(id))
                    throw new InvalidDataException(
                        "Duplicate mandatory distant coverage target: " + id);
                uint random = MapVegetationPlanning.HashId(id,
                    Options.Placement.Seed ^ 0x6179);
                bool usesCoverageSurfaceOverride = false;
                MapVegetationSurfaceHit hit;
                if (!MapVegetationPlanning.TryResolveTreePlacementSurface(
                        Surfaces, candidate.Position, out hit, out _))
                {
                    // This exception is deliberately limited to the mandatory
                    // collisionless horizon backbone. The point was already
                    // proven outside the convex donor-derived envelope and in
                    // its signed 95-650 m band. Playable/infill trees and the
                    // optional distant density pool retain every surface veto.
                    Vector3 silhouette = candidate.Position;
                    silhouette.y = candidate.BoundaryPoint.y + Options
                        .Placement.Category(MapVegetationKind.Tree)
                        .SurfaceOffsetMeters;
                    var silhouetteSurface = new SurfaceInfo(
                        "BoundaryCoverageSilhouette:" + candidate.SourceId,
                        "CollisionlessMandatoryDistantCoverage", null, 0,
                        null);
                    hit = new MapVegetationSurfaceHit(silhouette,
                        Vector3.up, silhouetteSurface);
                    usesCoverageSurfaceOverride = true;
                }
                float height = Mathf.Lerp(
                    DistantForestCoverageMinimumHeightMeters,
                    Options.DistantForestHeightRange.y,
                    VegetationStableHash.ToUnitFloat(
                        VegetationStableHash.Hash(random ^ 0xB731C4Du)));
                boundaryDistances[id] = candidate.Distance;
                var tree = new ResolvedTreeCandidate(id,
                    candidate.Position, hit, height, random,
                    VegetationStableHash.Hash(random ^ 0x5D419A7u),
                    candidate.SourceId, usesCoverageSurfaceOverride);
                resolved.Add(new DistantResolvedCandidate(tree,
                    densityPassed: false, depthBand: band,
                    boundaryPoint: new Vector2(candidate.BoundaryPoint.x,
                        candidate.BoundaryPoint.z),
                    isCoverageBackbone: true,
                    usesCoverageSurfaceOverride:
                        usesCoverageSurfaceOverride));
            }
            DistantForestCoverageBackboneCount = coverageBackbone.Count;
            if (DistantForestCoverageBackboneCount >
                Options.DistantForestMaximumTrees)
                throw new InvalidDataException(
                    "The required three-layer distant coverage backbone exceeds the reviewed tree budget.");
            DistantResolvedCandidate[] eligible = resolved.Where(item =>
                    item.DensityPassed || item.IsCoverageBackbone)
                .ToArray();
            DistantForestEligibleCount = eligible.Length;
            int densityRejected = resolved.Count - eligible.Length;
            if (densityRejected > 0)
                DistantForestRejections["Density"] = densityRejected;
            DistantResolvedCandidate[] acceptedCandidates = eligible
                .Where(item => item.IsCoverageBackbone)
                .OrderBy(item => item.Tree.Score)
                .ThenBy(item => item.Tree.Id, StringComparer.Ordinal)
                .Concat(eligible.Where(item =>
                        !item.IsCoverageBackbone)
                    .OrderBy(item => item.Tree.Score)
                    .ThenBy(item => item.Tree.Id,
                        StringComparer.Ordinal))
                .Take(Options.DistantForestMaximumTrees).ToArray();
            MapVegetationPlacement[] accepted = acceptedCandidates
                .Select(item =>
                {
                    ResolvedTreeCandidate candidate = item.Tree;
                    string method = item.IsCoverageBackbone
                        ? item.UsesCoverageSurfaceOverride
                            ? "StreamedCollisionlessBoundaryBackdropCoverageSilhouetteOverride"
                            : "StreamedCollisionlessBoundaryBackdropCoverageBackbone"
                        : candidate.UsesSilhouetteFallback
                            ? "StreamedCollisionlessBoundaryBackdropSilhouetteContinuation"
                            : "StreamedCollisionlessBoundaryBackdrop";
                    return new MapVegetationPlacement
                    {
                        id = candidate.Id,
                        source = candidate.Source,
                        method = method,
                        species = "mixed",
                        cellId = MapVegetationPlanning.CellAt(
                            candidate.Hit.Position,
                            Manifest.CellSizeMeters).Id,
                        category = MapVegetationCategories.BoundaryForest,
                        sourcePosition = candidate.Position,
                        position = candidate.Hit.Position,
                        normal = Vector3.up,
                        height = candidate.Height,
                        yaw = VegetationStableHash.ToUnitFloat(
                            candidate.Random) * 360f,
                        variation = VegetationStableHash.ToUnitFloat(
                            VegetationStableHash.Hash(
                                candidate.Random ^ 0x83A9u)),
                        profile = 0
                    };
                }).ToArray();
            DistantForestSilhouetteFallbackCount = accepted.Count(placement =>
                placement.method ==
                    "StreamedCollisionlessBoundaryBackdropSilhouetteContinuation" ||
                placement.method ==
                    "StreamedCollisionlessBoundaryBackdropCoverageSilhouetteOverride");
            DistantForestCoverageSilhouetteOverrideCount = accepted.Count(
                placement => placement.method ==
                    "StreamedCollisionlessBoundaryBackdropCoverageSilhouetteOverride");
            DistantBackdrop.AddRange(accepted);
            DistantForestRejectedByCap = DistantForestEligibleCount -
                DistantBackdrop.Count;
            if (DistantForestRejectedByCap > 0)
                DistantForestRejections["HardCap"] =
                    DistantForestRejectedByCap;
            float[] acceptedDistances = DistantBackdrop.Select(placement =>
                    boundaryDistances[placement.id]).ToArray();
            DistantForestAcceptedMinimumBoundaryDistanceMeters =
                acceptedDistances.Length == 0 ? 0f : acceptedDistances.Min();
            DistantForestAcceptedMaximumBoundaryDistanceMeters =
                acceptedDistances.Length == 0 ? 0f : acceptedDistances.Max();
            DistantBackdrop.Sort((left, right) =>
                string.CompareOrdinal(left.id, right.id));
        }

        private void RejectDistant(string reason)
        {
            DistantForestRejections.TryGetValue(reason, out int count);
            DistantForestRejections[reason] = count + 1;
        }

        private void RejectNaturalInfill(string reason)
        {
            NaturalInfillRejections.TryGetValue(reason, out int count);
            NaturalInfillRejections[reason] = count + 1;
        }

        private static string RejectionFamily(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "Unknown";
            int separator = reason.IndexOf(':');
            return separator < 0 ? reason : reason.Substring(0, separator);
        }

        private void CommitNaturalInfill(ResolvedTreeCandidate candidate)
        {
            WorldCellIndex cell = MapVegetationPlanning.CellAt(
                candidate.Hit.Position, Manifest.CellSizeMeters);
            if (!WoodyCells.TryGetValue(cell, out MapVegetationCellPlan plan))
                WoodyCells[cell] = plan = new MapVegetationCellPlan { Cell = cell };
            TreeSpacing.Add(candidate.Hit.Position);
            Cells.Add(cell);
            plan.Woody.Add(new MapVegetationPlacement
            {
                id = candidate.Id,
                source = candidate.Hit.Source,
                method = "DeterministicGreenForestInfill",
                species = "mixed",
                cellId = cell.Id,
                category = MapVegetationCategories.OriginalTrees,
                sourcePosition = candidate.Position,
                position = candidate.Hit.Position,
                normal = candidate.Hit.Normal,
                height = candidate.Height,
                yaw = VegetationStableHash.ToUnitFloat(
                    candidate.Random) * 360f,
                variation = VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(candidate.Random)),
                profile = 0
            });
        }

        private readonly struct ResolvedTreeCandidate
        {
            public readonly string Id;
            public readonly string Source;
            public readonly Vector3 Position;
            public readonly MapVegetationSurfaceHit Hit;
            public readonly float Height;
            public readonly uint Random;
            public readonly uint Score;
            public readonly bool UsesSilhouetteFallback;

            public ResolvedTreeCandidate(string id, Vector3 position,
                MapVegetationSurfaceHit hit, float height, uint random,
                uint score, string source = null,
                bool usesSilhouetteFallback = false)
            {
                Id = id;
                Source = source ?? hit.Source;
                Position = position;
                Hit = hit;
                Height = height;
                Random = random;
                Score = score;
                UsesSilhouetteFallback = usesSilhouetteFallback;
            }
        }

        private readonly struct DistantResolvedCandidate
        {
            public readonly ResolvedTreeCandidate Tree;
            public readonly bool DensityPassed;
            public readonly int DepthBand;
            public readonly Vector2 BoundaryPoint;
            public readonly bool IsCoverageBackbone;
            public readonly bool UsesCoverageSurfaceOverride;

            public DistantResolvedCandidate(ResolvedTreeCandidate tree,
                bool densityPassed, int depthBand, Vector2 boundaryPoint,
                bool isCoverageBackbone,
                bool usesCoverageSurfaceOverride)
            {
                Tree = tree;
                DensityPassed = densityPassed;
                DepthBand = depthBand;
                BoundaryPoint = boundaryPoint;
                IsCoverageBackbone = isCoverageBackbone;
                UsesCoverageSurfaceOverride =
                    usesCoverageSurfaceOverride;
            }
        }

        private readonly struct NearBoundaryCandidate
        {
            public readonly MapVegetationPlanning.BoundaryCandidate Candidate;
            public readonly string Id;
            public readonly uint Random;
            public readonly uint Score;
            public readonly float Height;
            public readonly bool InwardSide;
            public readonly bool DensityPassed;

            public NearBoundaryCandidate(
                MapVegetationPlanning.BoundaryCandidate candidate,
                string id, uint random, uint score, float height,
                bool inwardSide, bool densityPassed)
            {
                Candidate = candidate;
                Id = id;
                Random = random;
                Score = score;
                Height = height;
                InwardSide = inwardSide;
                DensityPassed = densityPassed;
            }
        }

        private static NearBoundaryCandidate CreateNearBoundaryCandidate(
            MapVegetationPlanning.BoundaryCandidate candidate,
            bool inwardSide, MapVegetationRebuildOptions options,
            int randomSalt, uint heightSalt)
        {
            uint random = VegetationStableHash.Hash(candidate.X,
                candidate.Z, options.Placement.Seed ^ randomSalt);
            float normalizedDepth = candidate.Distance /
                                    options.BoundaryDepthMeters;
            float density = Mathf.Lerp(options.BoundaryBackDensity,
                options.BoundaryFrontDensity, normalizedDepth);
            Vector2 heights = Vector2.Lerp(
                options.BoundaryBackHeightRange,
                options.BoundaryFrontHeightRange, normalizedDepth);
            float height = Mathf.Lerp(heights.x, heights.y,
                VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(random ^ heightSalt)));
            string id = "boundary:" + candidate.X + ":" + candidate.Z;
            return new NearBoundaryCandidate(candidate, id, random,
                VegetationStableHash.Hash(random ^ 0x9B41D7u), height,
                inwardSide, VegetationStableHash.ToUnitFloat(random) <
                             Mathf.Clamp01(density));
        }

        private void BuildEcologicalForestFloor()
        {
            MapVegetationPlacement[] acceptedTrees = WoodyCells.Values
                .SelectMany(plan => plan.Woody)
                .Where(placement => placement.category ==
                                        MapVegetationCategories.OriginalTrees ||
                                    placement.category ==
                                        MapVegetationCategories.BoundaryForest)
                .ToArray();
            MapVegetationForestFloorEcologyPlan ecology =
                MapVegetationForestFloorEcology.Plan(acceptedTrees,
                    Options.Placement.Seed, Manifest.CellSizeMeters);
            ForestFloorEcologyReport = ecology.Report;

            var coverSpacing = new MapVegetationPlanning.SpacingIndex(
                MapVegetationForestFloorEcology.MinimumSpacingMeters(
                    MapVegetationForestFloorSemantic.GroundCover));
            var litterSpacing = new MapVegetationPlanning.SpacingIndex(
                MapVegetationForestFloorEcology.MinimumSpacingMeters(
                    MapVegetationForestFloorSemantic.ConiferLitter));
            var debrisSpacing = new MapVegetationPlanning.SpacingIndex(
                MapVegetationForestFloorEcology.MinimumSpacingMeters(
                    MapVegetationForestFloorSemantic.WoodyDebris));
            var existingFloorSpacing =
                new MapVegetationPlanning.SpacingIndex(1.2f);
            foreach (MapVegetationPlacement placement in WoodyCells.Values
                         .SelectMany(plan => plan.Woody)
                         .Where(placement => placement.category ==
                             MapVegetationCategories.ShrubsAndUndergrowth))
                existingFloorSpacing.Add(placement.position);

            foreach (MapVegetationForestFloorEcologyCluster cluster in
                     ecology.Clusters)
            {
                var pending =
                    new List<PendingEcologicalForestFloorPlacement>();
                foreach (MapVegetationForestFloorEcologyCandidate candidate in
                         cluster.Candidates)
                {
                    MapVegetationPlanning.SpacingIndex semanticSpacing =
                        candidate.Semantic ==
                            MapVegetationForestFloorSemantic.GroundCover
                            ? coverSpacing
                            : candidate.Semantic ==
                              MapVegetationForestFloorSemantic.WoodyDebris
                                ? debrisSpacing : litterSpacing;
                    TryResolveEcologicalForestFloor(candidate,
                        semanticSpacing, existingFloorSpacing, pending);
                }

                if (pending.Count <
                    MapVegetationForestFloorEcology
                        .MinimumCandidatesPerCluster)
                {
                    ForestFloorEcologyReport.Reject(
                        "ResolvedClusterBelowMinimum",
                        Mathf.Max(1, pending.Count));
                    foreach (PendingEcologicalForestFloorPlacement item in
                             pending)
                        item.Plan.Reject(item.Candidate.Id,
                            item.Candidate.Position,
                            MapVegetationCategories.ShrubsAndUndergrowth,
                            "EcologyOrphanCluster",
                            item.Placement.source);
                    continue;
                }

                foreach (PendingEcologicalForestFloorPlacement item in pending)
                {
                    item.SemanticSpacing.Add(item.Placement.position);
                    existingFloorSpacing.Add(item.Placement.position);
                    item.Plan.Woody.Add(item.Placement);
                    Cells.Add(item.Cell);
                }
                ForestFloorEcologyReport.AcceptCluster(pending.Select(item =>
                    item.Candidate));
            }

            Debug.Log("MAP_VEGETATION_FOREST_FLOOR_ECOLOGY " +
                      ForestFloorEcologyReport.Summary);
        }

        private bool TryResolveEcologicalForestFloor(
            MapVegetationForestFloorEcologyCandidate candidate,
            MapVegetationPlanning.SpacingIndex semanticSpacing,
            MapVegetationPlanning.SpacingIndex existingFloorSpacing,
            List<PendingEcologicalForestFloorPlacement> pending)
        {
            Vector3 sourcePosition = candidate.Position;
            WorldCellIndex sourceCell = MapVegetationPlanning.CellAt(
                sourcePosition, Manifest.CellSizeMeters);
            if (!WoodyCells.TryGetValue(sourceCell,
                    out MapVegetationCellPlan plan))
                WoodyCells[sourceCell] = plan = new MapVegetationCellPlan
                    { Cell = sourceCell };
            MapVegetationForestFloorBindings.PlacementPolicy policy =
                MapVegetationForestFloorBindings.Policy(
                    MapVegetationForestFloorBindings.Pool.Understory,
                    candidate.Id);
            if (!Surfaces.TryResolve(sourcePosition,
                    MapVegetationKind.Shrub,
                    out MapVegetationSurfaceHit hit, out string reason))
            {
                plan.Reject(candidate.Id, sourcePosition,
                    MapVegetationCategories.ShrubsAndUndergrowth, reason,
                    "accepted-canopy:" + candidate.ClusterId);
                ForestFloorEcologyReport.Reject(
                    "SurfaceVeto:" + RejectionCategory(reason));
                return false;
            }
            if (Vector3.Angle(Vector3.up, hit.Normal) >
                policy.MaximumSlopeDegrees + 0.001f)
            {
                plan.Reject(candidate.Id, sourcePosition,
                    MapVegetationCategories.ShrubsAndUndergrowth,
                    "ForestFloorExcessiveSlope",
                    "accepted-canopy:" + candidate.ClusterId);
                ForestFloorEcologyReport.Reject("ExcessiveSlope");
                return false;
            }
            float spacing = MapVegetationForestFloorEcology
                .MinimumSpacingMeters(candidate.Semantic);
            if (semanticSpacing.HasNeighbor(hit.Position, spacing) ||
                PendingHasNeighbor(pending, candidate.Semantic,
                    hit.Position, spacing))
            {
                plan.Reject(candidate.Id, sourcePosition,
                    MapVegetationCategories.ShrubsAndUndergrowth,
                    "ForestFloorEcologySpacing",
                    "accepted-canopy:" + candidate.ClusterId);
                ForestFloorEcologyReport.Reject("SemanticSpacing");
                return false;
            }
            if (existingFloorSpacing.HasNeighbor(hit.Position, 1.2f))
            {
                plan.Reject(candidate.Id, sourcePosition,
                    MapVegetationCategories.ShrubsAndUndergrowth,
                    "ExistingForestFloorSpacing",
                    "accepted-canopy:" + candidate.ClusterId);
                ForestFloorEcologyReport.Reject("ExistingFloorSpacing");
                return false;
            }
            if (TreeSpacing.HasNeighbor(hit.Position,
                    MapVegetationForestFloorEcology
                        .MinimumTrunkClearanceMeters))
            {
                plan.Reject(candidate.Id, sourcePosition,
                    MapVegetationCategories.ShrubsAndUndergrowth,
                    "TrunkFootprint",
                    "accepted-canopy:" + candidate.ClusterId);
                ForestFloorEcologyReport.Reject("TrunkFootprint");
                return false;
            }

            Vector3 finalPosition = hit.Position + hit.Normal *
                                    policy.SurfaceOffsetMeters;
            WorldCellIndex finalCell = MapVegetationPlanning.CellAt(
                finalPosition, Manifest.CellSizeMeters);
            if (!WoodyCells.TryGetValue(finalCell,
                    out MapVegetationCellPlan finalPlan))
                WoodyCells[finalCell] = finalPlan =
                    new MapVegetationCellPlan { Cell = finalCell };
            uint random = MapVegetationPlanning.HashId(candidate.Id,
                Options.Placement.Seed);
            var placement = new MapVegetationPlacement
            {
                id = candidate.Id,
                source = "accepted-canopy:" + candidate.SourceTreeA + "|" +
                         candidate.SourceTreeB,
                method = EcologicalMethod(candidate.Semantic),
                species = "forest-floor-understory",
                cellId = finalCell.Id,
                category = MapVegetationCategories.ShrubsAndUndergrowth,
                sourcePosition = sourcePosition,
                position = finalPosition,
                normal = hit.Normal,
                height = 1f,
                yaw = VegetationStableHash.ToUnitFloat(random) * 360f,
                variation = VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(random)),
                profile = 0
            };
            pending.Add(new PendingEcologicalForestFloorPlacement(candidate,
                finalCell, finalPlan, placement, semanticSpacing));
            return true;
        }

        private static bool PendingHasNeighbor(
            IEnumerable<PendingEcologicalForestFloorPlacement> pending,
            MapVegetationForestFloorSemantic semantic,
            Vector3 position,
            float distance)
        {
            float squared = distance * distance;
            foreach (PendingEcologicalForestFloorPlacement item in pending)
            {
                if (SameSpacingLayer(item.Candidate.Semantic, semantic) &&
                    HorizontalSquared(item.Placement.position, position) <
                    squared)
                    return true;
            }
            return false;
        }

        private static bool SameSpacingLayer(
            MapVegetationForestFloorSemantic left,
            MapVegetationForestFloorSemantic right)
        {
            if (left == MapVegetationForestFloorSemantic.WoodyDebris ||
                right == MapVegetationForestFloorSemantic.WoodyDebris)
                return left == right;
            if (left == MapVegetationForestFloorSemantic.GroundCover ||
                right == MapVegetationForestFloorSemantic.GroundCover)
                return left == right;
            return true;
        }

        private static float HorizontalSquared(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return x * x + z * z;
        }

        private static string RejectionCategory(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "Unknown";
            int separator = reason.IndexOf(':');
            return separator > 0 ? reason.Substring(0, separator) : reason;
        }

        private static string EcologicalMethod(
            MapVegetationForestFloorSemantic semantic)
        {
            switch (semantic)
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return "CanopyEcologyGroundCover";
                case MapVegetationForestFloorSemantic.ConiferLitter:
                    return "CanopyEcologyConiferLitter";
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return "CanopyEcologyDeciduousLitter";
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return "CanopyEcologyWoodyDebris";
                default:
                    return "CanopyEcology";
            }
        }

        private readonly struct PendingEcologicalForestFloorPlacement
        {
            public readonly MapVegetationForestFloorEcologyCandidate Candidate;
            public readonly WorldCellIndex Cell;
            public readonly MapVegetationCellPlan Plan;
            public readonly MapVegetationPlacement Placement;
            public readonly MapVegetationPlanning.SpacingIndex SemanticSpacing;

            public PendingEcologicalForestFloorPlacement(
                MapVegetationForestFloorEcologyCandidate candidate,
                WorldCellIndex cell,
                MapVegetationCellPlan plan,
                MapVegetationPlacement placement,
                MapVegetationPlanning.SpacingIndex semanticSpacing)
            {
                Candidate = candidate;
                Cell = cell;
                Plan = plan;
                Placement = placement;
                SemanticSpacing = semanticSpacing;
            }
        }

        private bool PlaceForestFloor(string id, Vector3 original, string source, string method,
            MapVegetationForestFloorBindings.Pool pool, MapVegetationPlanning.SpacingIndex spacing,
            bool alreadyMapped, bool applyPoolDensity)
        {
            MapVegetationForestFloorBindings.PlacementPolicy policy =
                MapVegetationForestFloorBindings.Policy(pool);
            Vector3 p = alreadyMapped ? original : Options.Placement.DonorToWorld(original);
            WorldCellIndex cell = MapVegetationPlanning.CellAt(p, Manifest.CellSizeMeters);
            if (!WoodyCells.TryGetValue(cell, out MapVegetationCellPlan plan))
                WoodyCells[cell] = plan = new MapVegetationCellPlan { Cell = cell };
            uint random = MapVegetationPlanning.HashId(id, Options.Placement.Seed);
            if (applyPoolDensity && VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(random ^ 0x49AD71u)) >= policy.Density)
            {
                plan.Reject(id, p, MapVegetationCategories.ShrubsAndUndergrowth,
                    "ForestFloorDensity", source);
                return false;
            }
            if (!Surfaces.TryResolve(p, MapVegetationKind.Shrub,
                    out MapVegetationSurfaceHit hit, out string reason))
            {
                plan.Reject(id, p, MapVegetationCategories.ShrubsAndUndergrowth, reason, source);
                return false;
            }
            if (Vector3.Angle(Vector3.up, hit.Normal) > policy.MaximumSlopeDegrees + 0.001f)
            {
                plan.Reject(id, p, MapVegetationCategories.ShrubsAndUndergrowth,
                    "ForestFloorExcessiveSlope", source);
                return false;
            }
            if (spacing.HasNeighbor(hit.Position, policy.MinimumSpacingMeters))
            {
                plan.Reject(id, p, MapVegetationCategories.ShrubsAndUndergrowth,
                    "ForestFloorMinimumSpacing", source);
                return false;
            }
            if (TreeSpacing.HasNeighbor(hit.Position, 0.8f))
            {
                plan.Reject(id, p, MapVegetationCategories.ShrubsAndUndergrowth,
                    "TrunkFootprint", source);
                return false;
            }
            Vector3 finalPosition = hit.Position + hit.Normal * policy.SurfaceOffsetMeters;
            spacing.Add(finalPosition);
            Cells.Add(cell);
            string species = pool == MapVegetationForestFloorBindings.Pool.RocksAndBoulders
                ? "forest-floor-rock"
                : pool == MapVegetationForestFloorBindings.Pool.Shrubs
                    ? "forest-floor-shrub"
                    : "forest-floor-understory";
            plan.Woody.Add(new MapVegetationPlacement
            {
                id = id,
                source = source,
                method = method,
                species = species,
                cellId = cell.Id,
                category = MapVegetationCategories.ShrubsAndUndergrowth,
                sourcePosition = p,
                position = finalPosition,
                normal = hit.Normal,
                height = 1f,
                yaw = VegetationStableHash.ToUnitFloat(random) * 360f,
                variation = VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random)),
                profile = 0
            });
            return true;
        }

        private bool Place(string id, Vector3 original, string source, string method, string species, float height,
            MapVegetationCategories category, MapVegetationKind kind, MapVegetationPlanning.SpacingIndex spacing, bool alreadyMapped = false)
        {
            Vector3 p = alreadyMapped ? original : Options.Placement.DonorToWorld(original);
            WorldCellIndex cell = MapVegetationPlanning.CellAt(p, Manifest.CellSizeMeters);
            if (!WoodyCells.TryGetValue(cell, out MapVegetationCellPlan plan))
                WoodyCells[cell] = plan = new MapVegetationCellPlan { Cell = cell };
            uint random = MapVegetationPlanning.HashId(id, Options.Placement.Seed);
            if (VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random ^ 2917u)) >= Options.Placement.Category(kind).Density)
            { plan.Reject(id, p, category, "CategoryDensity", source); return false; }
            if (!Surfaces.TryResolve(p, kind, out MapVegetationSurfaceHit hit, out string reason))
            { plan.Reject(id, p, category, reason, source); return false; }
            if (spacing.HasNeighbor(p, Options.Placement.Category(kind).MinimumSpacingMeters))
            { plan.Reject(id, p, category, "MinimumTrunkSpacing", source); return false; }
            if (kind == MapVegetationKind.Shrub && TreeSpacing.HasNeighbor(p, 0.8f))
            { plan.Reject(id, p, category, "TrunkFootprint", source); return false; }
            spacing.Add(p);
            Cells.Add(cell);
            plan.Woody.Add(new MapVegetationPlacement { id = id, source = source, method = method, species = species,
                cellId = cell.Id, category = category, sourcePosition = p, position = hit.Position, normal = hit.Normal,
                height = height, yaw = VegetationStableHash.ToUnitFloat(random) * 360f,
                variation = VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random)), profile = 0 });
            return true;
        }

        private void BuildGrass(MapVegetationCellPlan plan)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            float cellSize = Manifest.CellSizeMeters, spacing = Options.GrassSpacingMeters;
            string cellId = plan.Cell.Id;
            float minX = plan.Cell.X * cellSize, minZ = plan.Cell.Z * cellSize;
            for (int z = Mathf.FloorToInt(minZ / spacing); z <= Mathf.CeilToInt((minZ + cellSize) / spacing); z++)
            for (int x = Mathf.FloorToInt(minX / spacing); x <= Mathf.CeilToInt((minX + cellSize) / spacing); x++)
            {
                Vector3 p = MapVegetationPlanning.Candidate(x, z, spacing, Options.Placement.Seed ^ 773);
                if (!MapVegetationPlanning.CellAt(p, cellSize).Equals(plan.Cell)) continue;
                plan.GrassCandidates++;
                uint random = VegetationStableHash.Hash(x, z, Options.Placement.Seed ^ 1459);
                float noise = Mathf.PerlinNoise(p.x * 0.035f + 517.3f, p.z * 0.035f + 199.7f);
                float profileSelector = MapVegetationPlanning.StableGrassProfileSelector(random);
                float density = Options.GrassDensity * Options.Placement.Category(MapVegetationKind.Grass).Density * Mathf.Lerp(0.85f, 1f, noise);
                if (VegetationStableHash.ToUnitFloat(random) >= Mathf.Clamp01(density)) continue;
                if (!Surfaces.TryResolve(p, MapVegetationKind.Grass, out MapVegetationSurfaceHit hit, out string reason))
                { plan.Reject($"grass:{x}:{z}", p, MapVegetationCategories.GrassCoverage, reason, string.Empty, false); continue; }
                if (TreeSpacing.HasNeighbor(p, 0.45f)) continue;
                if (!TryChooseGrassProfile(profileSelector, hit, out int profile))
                { plan.Reject($"grass:{x}:{z}", p, MapVegetationCategories.GrassCoverage, "GrassProfileSlopeOrHeight", hit.Source, false); continue; }
                float profileDensity = Options.GrassProfiles[profile].DensityMultiplier;
                if (VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random ^ 1913u)) >= Mathf.Clamp01(profileDensity)) continue;
                plan.Grass.Add(new MapVegetationPlacement { id = $"grass:{x}:{z}", source = hit.Source,
                    method = "NaturalSurfaceCoverage", cellId = cellId, category = MapVegetationCategories.GrassCoverage,
                    sourcePosition = p, position = hit.Position, normal = hit.Normal,
                    yaw = VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random)) * 360f,
                    variation = VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(random ^ 1997u)), profile = profile });
            }
            Debug.Log($"MAP_VEGETATION_GRASS_TIMING {plan.Cell.Id} seconds={timer.Elapsed.TotalSeconds:F2} accepted={plan.Grass.Count} {Surfaces.DiagnosticSummary}");
        }

        private bool TryChooseGrassProfile(float selector, MapVegetationSurfaceHit hit, out int profile)
        {
            int preferred = MapVegetationPlanning.PreferredGrassProfile(
                selector, Options.GrassProfiles.Length);
            float slope = Vector3.Angle(Vector3.up, hit.Normal);
            for (int offset = 0; offset < Options.GrassProfiles.Length; offset++)
            {
                int index = (preferred + offset) % Options.GrassProfiles.Length;
                VegetationProfile candidate = Options.GrassProfiles[index];
                if (candidate.DensityMultiplier > 0f && hit.AllowsChannel(candidate.DensityChannel) &&
                    slope <= candidate.MaximumSlopeDegrees + 0.001f &&
                    hit.Position.y >= candidate.WorldHeightRange.x && hit.Position.y <= candidate.WorldHeightRange.y &&
                    Surfaces.AllowsGrassChannel(hit, candidate.DensityChannel, out _))
                { profile = index; return true; }
            }
            profile = -1;
            return false;
        }
    }

    internal sealed class MapVegetationAssetLease : ScriptableObject
    {
        [SerializeField] private UnityEngine.Object[] resources;
        public void Keep(params UnityEngine.Object[] values) { resources = values; }
    }
}
