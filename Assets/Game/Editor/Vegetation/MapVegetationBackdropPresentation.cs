using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Editor.WorldStreaming;
using MSC.World.Partition;
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
    /// <summary>
    /// Writes the collisionless far forest as independently streamed cell
    /// scenes. No distant renderer is placed in the always-loaded global scene.
    /// </summary>
    public static class MapVegetationBackdropPresentation
    {
        public const string LayerId = "vegetation-backdrop";
        public const string SceneRoot =
            MapVegetationRebuildOptions.GeneratedRoot + "/Backdrop/Scenes";
        public const string DataRoot =
            MapVegetationRebuildOptions.GeneratedRoot + "/Backdrop/Data";
        public const string ReportPath =
            "Artifacts/VegetationRebuild/backdrop-presentation.json";
        private const string GeneratorId =
            "msc.map-vegetation-backdrop.v1";

        public static string ScenePath(WorldCellIndex cell) => SceneRoot +
            "/World_" + cell.Id + "_VegetationBackdrop.unity";

        public static BackdropPresentationReport WriteAndRegister(
            MapVegetationContext context, string runId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            BackdropPresentationReport report = CreateReport(context, runId);
            BackdropBuildPlan plan = BuildPlan(context, report);
            ApplyBudgetAndCoverageValidation(context, report);
            if (report.errors.Count != 0)
            {
                WriteReport(report);
                throw new InvalidOperationException(
                    "Distant backdrop preflight failed: " +
                    string.Join(" | ", report.errors));
            }

            EnsureFolder(SceneRoot);
            EnsureFolder(DataRoot);
            foreach (BackdropScenePlan scenePlan in plan.Scenes)
                WriteScene(context, scenePlan, runId);

            report = Validate(context, plan, report,
                requireRegistration: false);
            WriteReport(report);
            if (!report.passed)
                throw new InvalidDataException(
                    "Generated distant backdrop failed validation: " +
                    string.Join(" | ", report.errors));

            var registrations = plan.Scenes.Select(scene =>
                new ProductionWorldCellLayerScene(LayerId, scene.Cell, -1,
                    scene.ScenePath,
                    context.Options.DistantForestLoadingRadiusCells,
                    context.Options.DistantForestLoadingRadiusCells + 1,
                    deferInitialLoad: true))
                .ToArray();
            ProductionWorldCellLayerBuilder.ReplaceLayerScenes(
                context.Manifest, LayerId, registrations);

            report = Validate(context, plan, report,
                requireRegistration: true);
            WriteReport(report);
            if (!report.passed)
                throw new InvalidDataException(
                    "Registered distant backdrop failed validation: " +
                    string.Join(" | ", report.errors));
            return report;
        }

        public static BackdropPresentationReport Validate(
            MapVegetationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            BackdropPresentationReport report = CreateReport(context,
                string.Empty);
            BackdropBuildPlan plan = BuildPlan(context, report);
            return Validate(context, plan, report,
                requireRegistration: true);
        }

        private static BackdropPresentationReport Validate(
            MapVegetationContext context,
            BackdropBuildPlan plan,
            BackdropPresentationReport report,
            bool requireRegistration)
        {
            report.errors.Clear();
            report.sceneReports.Clear();
            report.validatedScenePaths = Array.Empty<string>();
            report.unavailableCellIds = Array.Empty<string>();
            report.distantRendererCount = 0;
            report.distantVertexCount = 0L;
            report.distantTriangleCount = 0L;
            report.distantColliderCount = 0;
            report.largestSceneRendererCount = 0;
            report.largestSceneVertexCount = 0L;
            report.largestSceneBatchCount = 0;
            report.largestRendererVertexCount = 0;
            report.largestRendererBounds = Vector3.zero;

            var validatedPaths = new List<string>();
            var unavailableCells = new List<string>();
            foreach (BackdropScenePlan expected in plan.Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        expected.ScenePath) == null)
                {
                    unavailableCells.Add(expected.CellId);
                    report.errors.Add("Missing backdrop scene: " +
                        expected.ScenePath);
                    continue;
                }
                Scene scene = EditorSceneManager.OpenScene(expected.ScenePath,
                    OpenSceneMode.Additive);
                try
                {
                    BackdropSceneReport sceneReport = ValidateScene(context,
                        expected, scene);
                    report.sceneReports.Add(sceneReport);
                    foreach (string error in sceneReport.errors)
                        report.errors.Add(expected.CellId + ": " + error);
                    report.distantRendererCount +=
                        sceneReport.rendererCount;
                    report.distantVertexCount += sceneReport.vertexCount;
                    report.distantTriangleCount += sceneReport.triangleCount;
                    report.distantColliderCount += sceneReport.colliderCount;
                    report.largestSceneRendererCount = Mathf.Max(
                        report.largestSceneRendererCount,
                        sceneReport.rendererCount);
                    report.largestSceneVertexCount = Math.Max(
                        report.largestSceneVertexCount,
                        sceneReport.vertexCount);
                    report.largestSceneBatchCount = Mathf.Max(
                        report.largestSceneBatchCount,
                        sceneReport.batchCount);
                    report.largestRendererVertexCount = Mathf.Max(
                        report.largestRendererVertexCount,
                        sceneReport.largestRendererVertexCount);
                    if (sceneReport.largestRendererBounds.x *
                        sceneReport.largestRendererBounds.z >
                        report.largestRendererBounds.x *
                        report.largestRendererBounds.z)
                        report.largestRendererBounds =
                            sceneReport.largestRendererBounds;
                    validatedPaths.Add(expected.ScenePath);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            report.validatedScenePaths = validatedPaths.ToArray();
            report.unavailableCellIds = unavailableCells.ToArray();
            report.sceneCount = plan.Scenes.Length;
            report.globalDistantBatchCount = CountGlobalDistantBatches();
            if (report.globalDistantBatchCount != 0)
                report.errors.Add(
                    "Always-loaded global scene contains distant forest batches.");

            if (requireRegistration)
                ValidateRegistration(context, plan, report);
            ApplyBudgetAndCoverageValidation(context, report);
            report.passed = report.errors.Count == 0;
            return report;
        }

        private static BackdropSceneReport ValidateScene(
            MapVegetationContext context,
            BackdropScenePlan expected,
            Scene scene)
        {
            var report = new BackdropSceneReport
            {
                cellId = expected.CellId,
                scenePath = expected.ScenePath,
                fingerprint = expected.Fingerprint,
                expectedTreeCount = expected.TreeCount
            };
            GameObject[] roots = scene.GetRootGameObjects();
            GeneratedVegetationGroup[] owners = roots.SelectMany(root =>
                    root.GetComponentsInChildren<GeneratedVegetationGroup>(true))
                .ToArray();
            if (owners.Length != 1 || owners[0].GeneratorId != GeneratorId ||
                owners[0].CellId != expected.CellId ||
                owners[0].Fingerprint != expected.Fingerprint)
                report.errors.Add("Ownership/fingerprint differs from plan.");
            GeneratedDistantForestBatch[] batches = roots.SelectMany(root =>
                    root.GetComponentsInChildren<
                        GeneratedDistantForestBatch>(true))
                .ToArray();
            MeshFilter[] filters = roots.SelectMany(root =>
                root.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            MeshRenderer[] renderers = roots.SelectMany(root =>
                root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            report.batchCount = batches.Length;
            report.rendererCount = renderers.Length;
            report.colliderCount = roots.Sum(root =>
                root.GetComponentsInChildren<Collider>(true).Length);
            report.vertexCount = filters.Sum(filter =>
                (long)(filter.sharedMesh != null
                    ? filter.sharedMesh.vertexCount : 0));
            report.triangleCount = filters.Sum(filter =>
                filter.sharedMesh == null ? 0L : Enumerable.Range(0,
                        filter.sharedMesh.subMeshCount)
                    .Sum(subMesh => (long)filter.sharedMesh.GetIndexCount(
                        subMesh) / 3L));
            report.largestRendererVertexCount = filters.Length == 0 ? 0 :
                filters.Max(filter => filter.sharedMesh != null
                    ? filter.sharedMesh.vertexCount : 0);
            report.largestRendererBounds = renderers.Length == 0
                ? Vector3.zero
                : renderers.Select(renderer => renderer.bounds.size)
                    .OrderByDescending(size => size.x * size.z).First();
            int encodedTreeCount = batches.Sum(batch => batch.InstanceCount);
            if (encodedTreeCount != expected.TreeCount)
                report.errors.Add("Tree count differs from plan.");
            var expectedSpecies = expected.SpeciesBatches.ToDictionary(
                batch => batch.Species, batch => batch.Placements.Length,
                StringComparer.Ordinal);
            var seenSpecies = new HashSet<string>(StringComparer.Ordinal);
            foreach (GeneratedDistantForestBatch batch in batches)
            {
                if (batch.CellId != expected.CellId ||
                    !seenSpecies.Add(batch.Species) ||
                    !expectedSpecies.TryGetValue(batch.Species,
                        out int expectedCount) ||
                    batch.InstanceCount != expectedCount)
                    report.errors.Add("Batch metadata differs: " +
                        batch.Species);
                if (batch.GetComponentsInChildren<MeshRenderer>(true).Length !=
                    batch.TemplatePartCount)
                    report.errors.Add("Template part count differs: " +
                        batch.Species);
            }
            if (seenSpecies.Count != expectedSpecies.Count)
                report.errors.Add("Species batch ownership differs from plan.");
            if (report.colliderCount != 0)
                report.errors.Add("Backdrop must contain zero colliders.");
            if (renderers.Any(renderer =>
                    renderer.shadowCastingMode != ShadowCastingMode.Off ||
                    renderer.receiveShadows ||
                    renderer.motionVectorGenerationMode !=
                        MotionVectorGenerationMode.ForceNoMotion ||
                    renderer.lightProbeUsage != LightProbeUsage.Off ||
                    renderer.reflectionProbeUsage != ReflectionProbeUsage.Off))
                report.errors.Add("Cheap renderer policy drifted.");
            float maximumSpan = context.Manifest.CellSizeMeters +
                context.Options.DistantForestHeightRange.y * 2f + 2f;
            if (renderers.Any(renderer =>
                    renderer.bounds.size.x > maximumSpan ||
                    renderer.bounds.size.z > maximumSpan))
                report.errors.Add("Renderer bounds escaped the source cell.");
            if (report.rendererCount != expected.RendererCount ||
                report.vertexCount != expected.VertexCount)
                report.errors.Add("Renderer/vertex complexity differs from preflight.");
            return report;
        }

        private static BackdropBuildPlan BuildPlan(
            MapVegetationContext context,
            BackdropPresentationReport report)
        {
            var templates = new Dictionary<string,
                MapVegetationGlobalPresentation.DistantTemplate>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string species in new[]
                     { "Spruce", "Pine", "Birch", "Aspen" })
                templates[species] = MapVegetationGlobalPresentation
                    .DistantTemplate.Load(species);

            var scenes = new List<BackdropScenePlan>();
            foreach (IGrouping<string, MapVegetationPlacement> cellGroup in
                     context.DistantBackdrop.GroupBy(placement =>
                             placement.cellId, StringComparer.Ordinal)
                         .OrderBy(group => group.Key,
                             StringComparer.Ordinal))
            {
                MapVegetationPlacement[] cellPlacements = cellGroup
                    .OrderBy(placement => placement.id,
                        StringComparer.Ordinal).ToArray();
                WorldCellIndex cell = MapVegetationPlanning.CellAt(
                    cellPlacements[0].position,
                    context.Manifest.CellSizeMeters);
                if (cell.Id != cellGroup.Key || cellPlacements.Any(placement =>
                        MapVegetationPlanning.CellAt(placement.position,
                            context.Manifest.CellSizeMeters).Id !=
                        cellGroup.Key))
                    throw new InvalidDataException(
                        "Distant placement escaped serialized cell ownership: " +
                        cellGroup.Key);
                var speciesPlans = new List<BackdropSpeciesPlan>();
                long sceneVertices = 0L;
                long sceneTriangles = 0L;
                int sceneRenderers = 0;
                foreach (IGrouping<string, MapVegetationPlacement> speciesGroup
                         in cellPlacements.GroupBy(placement =>
                                 placement.species, StringComparer.Ordinal)
                             .OrderBy(group => group.Key,
                                 StringComparer.Ordinal))
                {
                    MapVegetationPlacement[] placements = speciesGroup
                        .OrderBy(placement => placement.id,
                            StringComparer.Ordinal).ToArray();
                    MapVegetationGlobalPresentation.DistantTemplate template =
                        templates[speciesGroup.Key];
                    var speciesPlan = new BackdropSpeciesPlan
                    {
                        Species = speciesGroup.Key,
                        Placements = placements,
                        Template = template
                    };
                    speciesPlans.Add(speciesPlan);
                    sceneRenderers = checked(sceneRenderers +
                        template.Parts.Count);
                    foreach (MapVegetationGlobalPresentation.DistantPart part in
                             template.Parts)
                    {
                        sceneVertices = checked(sceneVertices +
                            (long)part.Vertices.Length * placements.Length);
                        sceneTriangles = checked(sceneTriangles +
                            (long)part.Indices.Length / 3L *
                            placements.Length);
                        report.largestRendererVertexCount = Mathf.Max(
                            report.largestRendererVertexCount,
                            part.Vertices.Length > 0 && placements.Length >
                            int.MaxValue / part.Vertices.Length
                                ? int.MaxValue
                                : part.Vertices.Length * placements.Length);
                    }
                }
                var scenePlan = new BackdropScenePlan
                {
                    Cell = cell,
                    CellId = cell.Id,
                    ScenePath = ScenePath(cell),
                    Origin = new Vector3(
                        cell.X * context.Manifest.CellSizeMeters, 0f,
                        cell.Z * context.Manifest.CellSizeMeters),
                    SpeciesBatches = speciesPlans.ToArray(),
                    TreeCount = cellPlacements.Length,
                    RendererCount = sceneRenderers,
                    VertexCount = sceneVertices,
                    TriangleCount = sceneTriangles,
                    Fingerprint = FingerprintCell(context, cell.Id,
                        cellPlacements)
                };
                scenes.Add(scenePlan);
                report.distantRendererCount = checked(
                    report.distantRendererCount + sceneRenderers);
                report.distantVertexCount = checked(
                    report.distantVertexCount + sceneVertices);
                report.distantTriangleCount = checked(
                    report.distantTriangleCount + sceneTriangles);
                report.largestSceneRendererCount = Mathf.Max(
                    report.largestSceneRendererCount, sceneRenderers);
                report.largestSceneVertexCount = Math.Max(
                    report.largestSceneVertexCount, sceneVertices);
                report.largestSceneBatchCount = Mathf.Max(
                    report.largestSceneBatchCount, speciesPlans.Count);
            }
            report.sceneCount = scenes.Count;
            MeasureBoundaryCoverage(context, report, templates);
            return new BackdropBuildPlan { Scenes = scenes.ToArray() };
        }

        private static void WriteScene(MapVegetationContext context,
            BackdropScenePlan scenePlan, string runId)
        {
            EnsureFolder(Path.GetDirectoryName(scenePlan.ScenePath)
                ?.Replace('\\', '/'));
            EnsureFolder(DataRoot + "/" + scenePlan.CellId);
            Backup(scenePlan.ScenePath, runId);
            Scene scene = File.Exists(scenePlan.ScenePath)
                ? EditorSceneManager.OpenScene(scenePlan.ScenePath,
                    OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            try
            {
                ValidateExistingOwnership(scene);
                foreach (GameObject oldRoot in scene.GetRootGameObjects())
                    Object.DestroyImmediate(oldRoot);
                var root = new GameObject("DistantForestBackdrop_" +
                    scenePlan.CellId);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<GeneratedVegetationGroup>().Configure(
                    GeneratorId, scenePlan.CellId,
                    MapVegetationCategories.BoundaryForest.ToString(),
                    scenePlan.Fingerprint);
                foreach (BackdropSpeciesPlan speciesPlan in
                         scenePlan.SpeciesBatches)
                    WriteSpeciesBatch(scenePlan, speciesPlan,
                        root.transform);
                if (!EditorSceneManager.SaveScene(scene,
                        scenePlan.ScenePath))
                    throw new IOException("Could not save distant backdrop " +
                        scenePlan.ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void WriteSpeciesBatch(BackdropScenePlan scenePlan,
            BackdropSpeciesPlan speciesPlan, Transform parent)
        {
            string safeSpecies = Sanitize(speciesPlan.Species);
            var batch = new GameObject(safeSpecies);
            batch.transform.SetParent(parent, false);
            batch.transform.position = scenePlan.Origin;
            batch.AddComponent<GeneratedDistantForestBatch>()
                .ConfigureForAuthoring(scenePlan.CellId,
                    speciesPlan.Species, speciesPlan.Placements.Length,
                    speciesPlan.Template.Parts.Count);
            GameObjectUtility.SetStaticEditorFlags(batch,
                StaticEditorFlags.OccludeeStatic);
            for (int partIndex = 0;
                 partIndex < speciesPlan.Template.Parts.Count;
                 partIndex++)
            {
                MapVegetationGlobalPresentation.DistantPart part =
                    speciesPlan.Template.Parts[partIndex];
                string partName = partIndex.ToString("D2",
                    CultureInfo.InvariantCulture) + "_" +
                    Sanitize(part.Name);
                string meshPath = DataRoot + "/" + scenePlan.CellId + "/" +
                    safeSpecies + "_" + partName + ".asset";
                Mesh mesh = MapVegetationGlobalPresentation.BuildBatchMesh(
                    part, speciesPlan.Template.Height,
                    speciesPlan.Placements,
                    "DistantForestBatch_" + scenePlan.CellId + "_" +
                    safeSpecies + "_" + partName, scenePlan.Origin);
                Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    saved = mesh;
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, saved);
                    Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(saved);
                }
                var partObject = new GameObject(partName);
                partObject.transform.SetParent(batch.transform, false);
                partObject.AddComponent<MeshFilter>().sharedMesh = saved;
                MeshRenderer renderer = partObject
                    .AddComponent<MeshRenderer>();
                renderer.sharedMaterial = part.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
                GameObjectUtility.SetStaticEditorFlags(partObject,
                    StaticEditorFlags.OccludeeStatic);
            }
        }

        private static void ApplyBudgetAndCoverageValidation(
            MapVegetationContext context,
            BackdropPresentationReport report)
        {
            report.treeBudget = context.Options.DistantForestMaximumTrees;
            report.sceneBudget = context.Options.DistantForestMaximumScenes;
            report.rendererBudget =
                context.Options.DistantForestMaximumRenderers;
            report.vertexBudget =
                context.Options.DistantForestMaximumVertices;
            report.rendererVertexBudget =
                context.Options.DistantForestMaximumVerticesPerBatch;
            report.sceneVertexBudget =
                context.Options.DistantForestMaximumVerticesPerScene;
            report.loadingRadiusCells =
                context.Options.DistantForestLoadingRadiusCells;
            report.unloadingRadiusCells = report.loadingRadiusCells + 1;
            if (report.distantTreeCount > report.treeBudget)
                AddError(report, "Distant tree hard cap was exceeded.");
            if (report.sceneCount > report.sceneBudget)
                AddError(report, "Backdrop scene hard budget was exceeded.");
            if (report.distantRendererCount > report.rendererBudget)
                AddError(report, "Backdrop renderer hard budget was exceeded.");
            if (report.distantVertexCount > report.vertexBudget)
                AddError(report, "Backdrop total vertex hard budget was exceeded.");
            if (report.largestRendererVertexCount >
                report.rendererVertexBudget)
                AddError(report,
                    "A backdrop renderer exceeded its vertex hard budget.");
            if (report.largestSceneVertexCount > report.sceneVertexBudget)
                AddError(report,
                    "A backdrop scene exceeded its vertex hard budget.");
            if (report.uncoveredBoundarySampleCount != 0)
                AddError(report,
                    "Distant forest alpha-safe crowns do not cover all three signed depth layers along every sampled outer-envelope segment.");
            if (report.uncoveredCrownIntervalCount != 0 ||
                report.maximumCrownSilhouetteGapMeters > 0.001f)
                AddError(report,
                    "Distant forest contains an exact alpha-safe crown interval gap on the outer-envelope skyline.");
            if (report.wrongSideOrDepthPlacementCount != 0)
                AddError(report,
                    "Distant forest contains placements outside their proven outward segment/depth band.");
            if (report.insideEnclosurePlacementCount != 0)
                AddError(report,
                    "Distant forest contains placements inside a proven boundary enclosure.");
            if (report.outerEnclosureCount <= 0 ||
                report.outerEnclosureCount !=
                report.plannedOuterEnvelopeCount)
                AddError(report,
                    "Measured outer-envelope topology differs from the generation plan.");
            if (report.syntheticClosureSegmentCount !=
                report.plannedSyntheticClosureSegmentCount)
                AddError(report,
                    "Measured synthetic small-gap closures differ from the generation plan.");
        }

        private static void MeasureBoundaryCoverage(
            MapVegetationContext context,
            BackdropPresentationReport report,
            IReadOnlyDictionary<string,
                MapVegetationGlobalPresentation.DistantTemplate> templates)
        {
            MapVegetationBoundarySegment[] distantEnvelope =
                MapVegetationPlanning.BuildDistantOuterEnvelopeSegments(
                    context.Source.BoundarySegments);
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure> enclosures =
                MapVegetationPlanning.BuildBoundaryEnclosures(
                    distantEnvelope);
            MapVegetationPlanning.BoundaryEnclosure[] outer = enclosures
                .Where(enclosure => enclosure.IsOuterEnvelope).ToArray();
            report.sourceBoundarySegmentCount =
                context.Source.BoundarySegments.Count;
            report.outerEnclosureCount = outer.Length;
            report.innerEnclosureCount = enclosures.Count - outer.Length;
            report.syntheticClosureSegmentCount = outer.Sum(enclosure =>
                enclosure.Segments.Count(segment =>
                    segment.IsSyntheticClosure));
            report.enclosures = enclosures.Select(enclosure =>
                new BackdropEnclosureReport
                {
                    enclosureId = enclosure.Id,
                    isOuterEnvelope = enclosure.IsOuterEnvelope,
                    signedArea = enclosure.SignedArea,
                    perimeter = enclosure.Perimeter,
                    closingGap = enclosure.ClosingGap,
                    closedBySmallGap = enclosure.ClosedBySmallGap,
                    sourceSegmentCount = enclosure.Segments.Count(segment =>
                        !segment.IsSyntheticClosure),
                    syntheticClosureSegmentCount = enclosure.Segments.Count(
                        segment => segment.IsSyntheticClosure)
                }).ToArray();
            float minimumScaledCrownRadius = templates.Values.Min(template =>
                template.CrownRadius * (context
                    .DistantForestCoverageMinimumHeightMeters /
                    template.Height));
            report.minimumScaledCrownRadiusMeters =
                minimumScaledCrownRadius;
            float maximumCenterSpacing = MapVegetationPlanning
                .MaximumDistantCrownCenterSpacing(
                    minimumScaledCrownRadius);
            float sampleSpacing = Mathf.Max(0.5f, Mathf.Min(2f,
                minimumScaledCrownRadius *
                MapVegetationPlanning.DistantCrownOpaqueFillFraction));
            report.maximumAlongEdgeGapMeters = maximumCenterSpacing;
            report.coverageBackboneAlongSpacingMeters = context
                .DistantForestCoverageAlongSpacingMeters;
            report.coverageBackboneMinimumHeightMeters = context
                .DistantForestCoverageMinimumHeightMeters;
            report.crownOpacitySafetyFraction = MapVegetationPlanning
                .DistantCrownOpaqueFillFraction;
            var bandReports = new[]
            {
                new BackdropDepthBandCoverage
                {
                    name = "near-95-200",
                    minimumSignedDistance = context.Options
                        .DistantForestMinimumBoundaryDistanceMeters,
                    maximumSignedDistance = 200f
                },
                new BackdropDepthBandCoverage
                {
                    name = "middle-200-400",
                    minimumSignedDistance = 200f,
                    maximumSignedDistance = 400f
                },
                new BackdropDepthBandCoverage
                {
                    name = "far-400-650",
                    minimumSignedDistance = 400f,
                    maximumSignedDistance = context.Options
                        .DistantForestDepthMeters
                }
            };
            if (bandReports[0].minimumSignedDistance >= 200f ||
                context.Options.DistantForestDepthMeters <= 400f)
            {
                report.errors.Add(
                    "Distant backdrop depth cannot provide the required three signed layers (minimum-200, 200-400, 400-depth)." );
                report.depthBandCoverage = bandReports;
                return;
            }

            var uncoveredSegments = new HashSet<string>(
                StringComparer.Ordinal);
            var placementBySource = context.DistantBackdrop.GroupBy(
                    placement => placement.source ?? string.Empty,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(),
                    StringComparer.Ordinal);
            int samples = 0;
            int covered = 0;
            int wrongSide = 0;
            int insideEnclosure = 0;
            Matrix4x4 mapping = context.Options.Placement
                .DonorCoordinateMatrix;
            Matrix4x4 inverseMapping = MapVegetationPlanning
                .InvertCoordinateMapping(mapping);
            var knownSources = new HashSet<string>(outer.SelectMany(enclosure =>
                    enclosure.Segments).Select(item => item.Segment.Id),
                StringComparer.Ordinal);
            foreach (MapVegetationPlacement placement in
                     context.DistantBackdrop)
            {
                if (!knownSources.Contains(placement.source ?? string.Empty))
                    wrongSide++;
                if (MapVegetationPlanning.ContainsMappedPoint(enclosures,
                        placement.position, inverseMapping))
                    insideEnclosure++;
            }

            foreach (MapVegetationPlanning.BoundaryEnclosure enclosure in
                     outer.OrderBy(item => item.Id,
                         StringComparer.Ordinal))
            foreach (MapVegetationPlanning.OrientedBoundarySegment segment in
                     enclosure.Segments.OrderBy(item => item.Segment.Id,
                         StringComparer.Ordinal))
            {
                Vector3 a3 = mapping.MultiplyPoint3x4(segment.Segment.A);
                Vector3 b3 = mapping.MultiplyPoint3x4(segment.Segment.B);
                Vector2 a = new Vector2(a3.x, a3.z);
                Vector2 b = new Vector2(b3.x, b3.z);
                Vector2 edge = b - a;
                float length = edge.magnitude;
                if (length < 0.01f)
                {
                    uncoveredSegments.Add(segment.Segment.Id +
                                          "|degenerate");
                    continue;
                }
                Vector3 sourceMid = (segment.Segment.A +
                                     segment.Segment.B) * 0.5f;
                Vector3 mappedOutwardPoint = mapping.MultiplyPoint3x4(
                    sourceMid + segment.Segment.Outward);
                Vector3 mappedMid = mapping.MultiplyPoint3x4(sourceMid);
                Vector2 outwardEvidence = new Vector2(
                    mappedOutwardPoint.x - mappedMid.x,
                    mappedOutwardPoint.z - mappedMid.z);
                Vector2 along = edge / length;
                Vector2 outward = new Vector2(-along.y, along.x);
                if (Vector2.Dot(outward, outwardEvidence) < 0f)
                    outward = -outward;
                placementBySource.TryGetValue(segment.Segment.Id,
                    out MapVegetationPlacement[] placements);
                placements ??= Array.Empty<MapVegetationPlacement>();
                var crownsByBand = new List<MapVegetationPlanning
                    .DistantCrownProjection>[]
                {
                    new List<MapVegetationPlanning.DistantCrownProjection>(),
                    new List<MapVegetationPlanning.DistantCrownProjection>(),
                    new List<MapVegetationPlanning.DistantCrownProjection>()
                };
                foreach (MapVegetationPlacement placement in placements)
                {
                    Vector2 delta = new Vector2(placement.position.x,
                        placement.position.z) - a;
                    float alongDistance = Vector2.Dot(delta, along);
                    float signedDistance = Vector2.Dot(delta, outward);
                    int band = MapVegetationPlanning.DistantDepthBand(
                        signedDistance, context.Options
                            .DistantForestMinimumBoundaryDistanceMeters,
                        context.Options.DistantForestDepthMeters);
                    if (band < 0 || alongDistance < -0.01f ||
                        alongDistance > length + 0.01f)
                    {
                        wrongSide++;
                        continue;
                    }
                    if (!templates.TryGetValue(placement.species,
                            out MapVegetationGlobalPresentation
                                .DistantTemplate template))
                    {
                        wrongSide++;
                        continue;
                    }
                    float crownRadius = template.CrownRadius *
                        (placement.height / template.Height) *
                        MapVegetationPlanning
                            .DistantCrownOpaqueFillFraction;
                    crownsByBand[band].Add(new MapVegetationPlanning
                        .DistantCrownProjection(
                            Mathf.Clamp(alongDistance, 0f, length),
                            crownRadius));
                }

                int steps = Mathf.Max(1, Mathf.CeilToInt(length /
                    sampleSpacing));
                for (int band = 0; band < bandReports.Length; band++)
                {
                    MapVegetationPlanning.DistantCrownCoverage crownCoverage =
                        MapVegetationPlanning.MeasureDistantCrownCoverage(
                            length, crownsByBand[band]);
                    bandReports[band].crownProjectionCount +=
                        crownCoverage.ProjectionCount;
                    bandReports[band].crownGapCount +=
                        crownCoverage.GapCount;
                    bandReports[band].maximumCrownGapMeters = Mathf.Max(
                        bandReports[band].maximumCrownGapMeters,
                        crownCoverage.LargestGap);
                    report.crownProjectionCount +=
                        crownCoverage.ProjectionCount;
                    report.uncoveredCrownIntervalCount +=
                        crownCoverage.GapCount;
                    report.maximumCrownSilhouetteGapMeters = Mathf.Max(
                        report.maximumCrownSilhouetteGapMeters,
                        crownCoverage.LargestGap);
                    if (!crownCoverage.IsClosed)
                    {
                        string crownId = segment.Segment.Id + "|" +
                            bandReports[band].name + "|crown-gap";
                        uncoveredSegments.Add(crownId);
                        if (!bandReports[band].uncoveredSegmentIds.Contains(
                                crownId))
                            bandReports[band].uncoveredSegmentIds.Add(
                                crownId);
                    }
                    for (int step = 0; step <= steps; step++)
                    {
                    float sample = (float)step / steps * length;
                    samples++;
                    bandReports[band].sampleCount++;
                    if (crownsByBand[band].Any(crown =>
                            Mathf.Abs(crown.AlongDistance - sample) <=
                            crown.EffectiveRadius))
                    {
                        covered++;
                        bandReports[band].coveredSampleCount++;
                    }
                    else
                    {
                        string id = segment.Segment.Id + "|" +
                                    bandReports[band].name;
                        uncoveredSegments.Add(id);
                        if (!bandReports[band].uncoveredSegmentIds.Contains(id))
                            bandReports[band].uncoveredSegmentIds.Add(id);
                    }
                    }
                }
            }
            report.boundarySegmentCount =
                outer.Sum(enclosure => enclosure.Segments.Count);
            report.boundarySampleCount = samples;
            report.coveredBoundarySampleCount = covered;
            report.uncoveredBoundarySampleCount = samples - covered;
            report.uncoveredBoundarySegmentIds =
                uncoveredSegments.OrderBy(id => id,
                    StringComparer.Ordinal).ToArray();
            report.boundaryCoverageSampleSpacingMeters = sampleSpacing;
            report.boundaryCoverageRadiusMeters =
                minimumScaledCrownRadius * MapVegetationPlanning
                    .DistantCrownOpaqueFillFraction;
            report.wrongSideOrDepthPlacementCount = wrongSide;
            report.insideEnclosurePlacementCount = insideEnclosure;
            report.depthBandCoverage = bandReports;
        }

        private static void ValidateRegistration(
            MapVegetationContext context,
            BackdropBuildPlan plan,
            BackdropPresentationReport report)
        {
            ProductionWorldCellLayerScene[] registered = context.Manifest
                .CellLayers.Where(layer => layer.LayerId == LayerId)
                .OrderBy(layer => layer.Index.Z)
                .ThenBy(layer => layer.Index.X).ToArray();
            BackdropScenePlan[] expected = plan.Scenes
                .OrderBy(scene => scene.Cell.Z)
                .ThenBy(scene => scene.Cell.X).ToArray();
            if (registered.Length != expected.Length)
            {
                report.errors.Add(
                    "Backdrop manifest scene count differs from plan.");
                return;
            }
            for (int index = 0; index < expected.Length; index++)
            {
                ProductionWorldCellLayerScene actual = registered[index];
                BackdropScenePlan planned = expected[index];
                if (!actual.Index.Equals(planned.Cell) ||
                    actual.ScenePath != planned.ScenePath ||
                    actual.BuildIndex < 0 ||
                    actual.MinimumLoadingRadiusCells !=
                        context.Options.DistantForestLoadingRadiusCells ||
                    actual.MinimumUnloadingRadiusCells !=
                        context.Options.DistantForestLoadingRadiusCells + 1 ||
                    !actual.DeferInitialLoad)
                    report.errors.Add("Backdrop manifest differs: " +
                        planned.CellId);
            }
        }

        private static int CountGlobalDistantBatches()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    MapVegetationGlobalPresentation.ScenePath) == null)
                return 0;
            Scene scene = EditorSceneManager.OpenScene(
                MapVegetationGlobalPresentation.ScenePath,
                OpenSceneMode.Additive);
            try
            {
                return scene.GetRootGameObjects().Sum(root =>
                    root.GetComponentsInChildren<
                        GeneratedDistantForestBatch>(true).Length);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static BackdropPresentationReport CreateReport(
            MapVegetationContext context, string runId) =>
            new BackdropPresentationReport
            {
                runId = runId,
                settingsHash = MapVegetationRebuild.SettingsHash(
                    context.Options),
                sourceFingerprint = context.SourceFingerprint,
                distantTreeCount = context.DistantBackdrop.Count,
                eligibleTreeCount = context.DistantForestEligibleCount,
                rejectedByCap = context.DistantForestRejectedByCap,
                candidateCount = context.DistantForestCandidateCount,
                silhouetteFallbackTreeCount =
                    context.DistantForestSilhouetteFallbackCount,
                coverageBackboneTreeCount =
                    context.DistantForestCoverageBackboneCount,
                coverageSilhouetteOverrideTreeCount = context
                    .DistantForestCoverageSilhouetteOverrideCount,
                plannedOuterEnvelopeCount =
                    context.DistantForestOuterEnvelopeCount,
                plannedSyntheticClosureSegmentCount = context
                    .DistantForestSyntheticClosureSegmentCount,
                totalBoundaryLengthMeters =
                    context.DistantForestBoundaryLengthMeters,
                acceptedMinimumBoundaryDistanceMeters = context
                    .DistantForestAcceptedMinimumBoundaryDistanceMeters,
                acceptedMaximumBoundaryDistanceMeters = context
                    .DistantForestAcceptedMaximumBoundaryDistanceMeters,
                rejectionReasons = context.DistantForestRejections
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new BackdropRejectionCount
                    {
                        reason = pair.Key,
                        count = pair.Value
                    }).ToArray()
            };

        private static string FingerprintCell(MapVegetationContext context,
            string cellId, IEnumerable<MapVegetationPlacement> placements)
        {
            using SHA256 sha = SHA256.Create();
            var text = new StringBuilder(4096)
                .Append(GeneratorId).Append('\n')
                .Append(MapVegetationRebuild.SettingsHash(context.Options))
                .Append('\n').Append(context.SourceFingerprint).Append('\n')
                .Append(cellId).Append('\n');
            foreach (MapVegetationPlacement placement in placements
                         .OrderBy(item => item.id, StringComparer.Ordinal))
                text.Append(placement.id).Append('|')
                    .Append(placement.species).Append('|')
                    .Append(Vector(placement.position)).Append('|')
                    .Append(placement.height.ToString("R",
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(placement.yaw.ToString("R",
                        CultureInfo.InvariantCulture)).Append('\n');
            return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text.ToString())))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Vector(Vector3 value) =>
            value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.y.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.z.ToString("R", CultureInfo.InvariantCulture);

        private static void ValidateExistingOwnership(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup owner =
                    root.GetComponent<GeneratedVegetationGroup>();
                if (owner == null || owner.GeneratorId != GeneratorId)
                    throw new InvalidOperationException(
                        "Refusing to overwrite non-owned backdrop content: " +
                        root.name);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Backup(string path, string runId)
        {
            if (!File.Exists(path)) return;
            string relative = path.Substring(
                MapVegetationRebuildOptions.GeneratedRoot.Length)
                .TrimStart('/');
            string destination =
                "Artifacts/VegetationRebuild/Backups/Backdrop/" + runId +
                "/" + relative;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, false);
            if (File.Exists(path + ".meta"))
                File.Copy(path + ".meta", destination + ".meta", false);
        }

        private static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? string.Empty).Select(character =>
                invalid.Contains(character) || character == '|'
                    ? '_' : character).ToArray());
        }

        private static void AddError(BackdropPresentationReport report,
            string error)
        {
            if (!report.errors.Contains(error)) report.errors.Add(error);
        }

        private static void WriteReport(BackdropPresentationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
        }

        private sealed class BackdropBuildPlan
        {
            public BackdropScenePlan[] Scenes =
                Array.Empty<BackdropScenePlan>();
        }

        private sealed class BackdropScenePlan
        {
            public WorldCellIndex Cell;
            public string CellId;
            public string ScenePath;
            public string Fingerprint;
            public Vector3 Origin;
            public BackdropSpeciesPlan[] SpeciesBatches;
            public int TreeCount;
            public int RendererCount;
            public long VertexCount;
            public long TriangleCount;
        }

        private sealed class BackdropSpeciesPlan
        {
            public string Species;
            public MapVegetationPlacement[] Placements;
            public MapVegetationGlobalPresentation.DistantTemplate Template;
        }

        [Serializable]
        public sealed class BackdropPresentationReport
        {
            public string generator = GeneratorId;
            public string runId;
            public string settingsHash;
            public string sourceFingerprint;
            public bool passed;
            public int eligibleTreeCount;
            public int distantTreeCount;
            public int rejectedByCap;
            public int candidateCount;
            public int silhouetteFallbackTreeCount;
            public int coverageBackboneTreeCount;
            public int coverageSilhouetteOverrideTreeCount;
            public int plannedOuterEnvelopeCount;
            public int plannedSyntheticClosureSegmentCount;
            public float totalBoundaryLengthMeters;
            public float acceptedMinimumBoundaryDistanceMeters;
            public float acceptedMaximumBoundaryDistanceMeters;
            public BackdropRejectionCount[] rejectionReasons =
                Array.Empty<BackdropRejectionCount>();
            public int sceneCount;
            public int distantRendererCount;
            public long distantVertexCount;
            public long distantTriangleCount;
            public int distantColliderCount;
            public int largestSceneRendererCount;
            public long largestSceneVertexCount;
            public int largestSceneBatchCount;
            public int largestRendererVertexCount;
            public Vector3 largestRendererBounds;
            public int treeBudget;
            public int sceneBudget;
            public int rendererBudget;
            public int vertexBudget;
            public int rendererVertexBudget;
            public int sceneVertexBudget;
            public int loadingRadiusCells;
            public int unloadingRadiusCells;
            public int globalDistantBatchCount;
            public int sourceBoundarySegmentCount;
            public int boundarySegmentCount;
            public int outerEnclosureCount;
            public int innerEnclosureCount;
            public int syntheticClosureSegmentCount;
            public int boundarySampleCount;
            public int coveredBoundarySampleCount;
            public int uncoveredBoundarySampleCount;
            public float boundaryCoverageSampleSpacingMeters;
            public float boundaryCoverageRadiusMeters;
            public float minimumScaledCrownRadiusMeters;
            public float maximumAlongEdgeGapMeters;
            public float coverageBackboneAlongSpacingMeters;
            public float coverageBackboneMinimumHeightMeters;
            public float crownOpacitySafetyFraction;
            public int crownProjectionCount;
            public int uncoveredCrownIntervalCount;
            public float maximumCrownSilhouetteGapMeters;
            public int wrongSideOrDepthPlacementCount;
            public int insideEnclosurePlacementCount;
            public string[] uncoveredBoundarySegmentIds =
                Array.Empty<string>();
            public BackdropEnclosureReport[] enclosures =
                Array.Empty<BackdropEnclosureReport>();
            public BackdropDepthBandCoverage[] depthBandCoverage =
                Array.Empty<BackdropDepthBandCoverage>();
            public string[] validatedScenePaths = Array.Empty<string>();
            public string[] unavailableCellIds = Array.Empty<string>();
            public List<BackdropSceneReport> sceneReports =
                new List<BackdropSceneReport>();
            public List<string> errors = new List<string>();
            public string ownershipPolicy =
                "One independently streamed vegetation-backdrop scene per source cell; one far-LOD mesh renderer per audited species template part; no always-loaded distant root.";
            public string rendererPolicy =
                "Collisionless, shadowless, probe-free, ForceNoMotion, bounded to one 512 m cell; exact instance count comes from GeneratedDistantForestBatch metadata.";
            public string coveragePolicy =
                "A deterministic convex outside masking envelope is derived per donor-validated outer enclosure so deep offsets cannot re-enter a concavity. Every derived outer segment receives three mandatory signed depth layers. Backbone spacing is bounded by twice the minimum selected far-LOD crown radius with a 0.9 alpha-coverage safety factor. Validation unions the actual scaled crown interval of every assigned species/height and rejects any exact skyline gap; a nearby trunk alone cannot pass. Every placement remains outside all derived enclosures. Surface/exclusion bypass is limited to mandatory collisionless coverage silhouettes; optional density trees retain every surface veto.";
        }

        [Serializable]
        public sealed class BackdropEnclosureReport
        {
            public string enclosureId;
            public bool isOuterEnvelope;
            public bool closedBySmallGap;
            public float signedArea;
            public float perimeter;
            public float closingGap;
            public int sourceSegmentCount;
            public int syntheticClosureSegmentCount;
        }

        [Serializable]
        public sealed class BackdropDepthBandCoverage
        {
            public string name;
            public float minimumSignedDistance;
            public float maximumSignedDistance;
            public int sampleCount;
            public int coveredSampleCount;
            public int crownProjectionCount;
            public int crownGapCount;
            public float maximumCrownGapMeters;
            public List<string> uncoveredSegmentIds = new List<string>();
        }

        [Serializable]
        public sealed class BackdropRejectionCount
        {
            public string reason;
            public int count;
        }

        [Serializable]
        public sealed class BackdropSceneReport
        {
            public string cellId;
            public string scenePath;
            public string fingerprint;
            public int expectedTreeCount;
            public int batchCount;
            public int rendererCount;
            public long vertexCount;
            public long triangleCount;
            public int colliderCount;
            public int largestRendererVertexCount;
            public Vector3 largestRendererBounds;
            public List<string> errors = new List<string>();
        }
    }
}
