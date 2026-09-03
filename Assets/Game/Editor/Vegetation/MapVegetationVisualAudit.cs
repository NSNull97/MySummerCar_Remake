using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Editor.WorldBaseline;
using MSC.LegacyImport;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>Actual HDRP renders of saved cell content. Never saves an input scene.</summary>
    public static class MapVegetationVisualAudit
    {
        private const string Output = "Artifacts/VegetationRebuild/VisualAudit";
        private const int Width = 1600;
        private const int Height = 1000;
        internal const float GrassAuditNeighborhoodRadiusMeters = 10f;
        internal const int GrassAuditMinimumNearbyInstances = 256;
        internal const int GrassAuditMinimumForwardInstances = 40;
        internal const float GrassAuditMaximumNearestForwardMeters = 1.5f;

        public static void CapturePilotBatch()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Visual audit requires a graphics-enabled Unity process; remove -nographics.");
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset))
                throw new InvalidOperationException("The project's existing HDRP pipeline must be active.");
            var options = AssetDatabase.LoadAssetAtPath<MapVegetationRebuildOptions>(MapVegetationRebuildOptions.AssetPath);
            if (options == null) throw new InvalidOperationException("Generate the pilot first; no settings are created by capture.");
            string[] arguments = Environment.GetCommandLineArgs();
            WorldCellIndex cell = AuditCell(arguments, options.SelectedCell, out bool cellOverride);
            Vector2? focusXZ = AuditFocus(arguments, cellOverride);
            string outputPath = cellOverride ? Path.Combine(MapVegetationRebuild.Reports, "VisualAuditCells", cell.Id) : Output;
            CaptureCell(options, cell, outputPath, cellOverride, focusXZ);
        }

        private static WorldCellIndex AuditCell(string[] arguments, string selectedCell, out bool cellOverride)
        {
            cellOverride = false;
            string requestedCell = selectedCell;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (!string.Equals(arguments[i], "-vegetationAuditCell", StringComparison.OrdinalIgnoreCase)) continue;
                if (cellOverride || i + 1 >= arguments.Length || !arguments[i + 1].StartsWith("cell_", StringComparison.Ordinal))
                    throw new ArgumentException("Use -vegetationAuditCell cell_X_Z exactly once with a valid cell ID.");
                cellOverride = true;
                requestedCell = arguments[++i];
            }
            return MapVegetationRebuild.ParseCell(requestedCell);
        }

        private static Vector2? AuditFocus(string[] arguments, bool cellOverride)
        {
            bool hasX = false, hasZ = false;
            float x = 0f, z = 0f;
            for (int i = 0; i < arguments.Length; i++)
            {
                bool readX = string.Equals(arguments[i], "-vegetationAuditFocusX", StringComparison.OrdinalIgnoreCase);
                bool readZ = string.Equals(arguments[i], "-vegetationAuditFocusZ", StringComparison.OrdinalIgnoreCase);
                if (!readX && !readZ) continue;
                if ((readX ? hasX : hasZ) || i + 1 >= arguments.Length)
                    throw new ArgumentException("Each vegetation audit focus coordinate must occur once with an invariant finite number.");
                if (!float.TryParse(arguments[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
                    throw new ArgumentException("Vegetation audit focus coordinates require invariant finite numbers, for example 155 and -1035.");
                if (readX) { x = value; hasX = true; }
                else { z = value; hasZ = true; }
            }
            if (!hasX && !hasZ) return null;
            if (!cellOverride || !hasX || !hasZ)
                throw new ArgumentException("Use both -vegetationAuditFocusX and -vegetationAuditFocusZ with -vegetationAuditCell cell_X_Z.");
            return new Vector2(x, z);
        }

        private static void CaptureCell(MapVegetationRebuildOptions options, WorldCellIndex cell, string outputPath, bool cellOverride, Vector2? focusXZ)
        {
            int vegetationRadius = options.ForestLoadingRadiusCells;
            if (vegetationRadius < 1 || vegetationRadius > 4)
                throw new InvalidOperationException("Forest loading radius must be between one and four cells; capture does not modify settings.");
            string pilotPath = MapVegetationRebuild.CellScenePath(cell);
            if (!File.Exists(pilotPath)) throw new FileNotFoundException("Saved vegetation cell scene is missing.", pilotPath);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard unsaved scene edits before capture. Nothing was changed.");

            var report = new AuditReport { cellId = cell.Id, generatedScene = pilotPath,
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                settingsHash = MapVegetationRebuild.SettingsHash(options), cellOverride = cellOverride,
                outputDirectory = outputPath, vegetationLoadingRadiusCells = vegetationRadius,
                explicitFocus = focusXZ.HasValue, requestedFocusXZ = focusXZ.GetValueOrDefault(),
                grassCloseViewRequired = (options.Categories & MapVegetationCategories.GrassCoverage) != 0 };
            SceneSetup[] original = EditorSceneManager.GetSceneManagerSetup();
            var temporary = new List<Object>();
            RenderTexture target = null;
            Texture2D pixels = null;
            Camera camera = null;
            MapVegetationAssetLease assetLease = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory(outputPath);
                var manifest = AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(WorldBaseline06B2Paths.ActiveManifest);
                if (manifest == null) throw new InvalidOperationException("Active streaming manifest is missing.");
                Vector3 cellCenter = new Vector3((cell.X + 0.5f) * manifest.CellSizeMeters, 0f, (cell.Z + 0.5f) * manifest.CellSizeMeters);
                Vector3 center = focusXZ.HasValue ? new Vector3(focusXZ.Value.x, 0f, focusXZ.Value.y) : cellCenter;
                report.cameraSelectionCenterXZ = new Vector2(center.x, center.z);
                if (focusXZ.HasValue && !MapVegetationPlanning.CellAt(center, manifest.CellSizeMeters).Equals(cell))
                    throw new ArgumentException("Vegetation audit focus must lie within the requested cell; no scenes or settings were changed.");
                // Hold native asset references across OpenScene(Single); managed
                // variables alone do not stop Unity unloading Editor-only settings.
                assetLease = ScriptableObject.CreateInstance<MapVegetationAssetLease>();
                assetLease.hideFlags = HideFlags.HideAndDontSave;
                assetLease.Keep(options, options.Placement, manifest);
                string globalPath = string.IsNullOrWhiteSpace(options.Placement.SourceGlobalScenePath)
                    ? WorldBaseline06B2Paths.GlobalScene : options.Placement.SourceGlobalScenePath;
                Scene global = EditorSceneManager.OpenScene(globalPath, OpenSceneMode.Single);
                report.loadedScenes.Add(globalPath);
                var sourceRoots = new List<GameObject>(global.GetRootGameObjects());
                foreach (ProductionWorldCellScene sourceCell in manifest.Cells)
                {
                    if (Mathf.Abs(sourceCell.Index.X - cell.X) > 1 || Mathf.Abs(sourceCell.Index.Z - cell.Z) > 1) continue;
                    Scene scene = EditorSceneManager.OpenScene(sourceCell.ScenePath, OpenSceneMode.Additive);
                    report.loadedScenes.Add(sourceCell.ScenePath);
                    sourceRoots.AddRange(scene.GetRootGameObjects());
                }
                Scene pilot = EditorSceneManager.OpenScene(pilotPath, OpenSceneMode.Additive);
                report.loadedScenes.Add(pilotPath);
                GameObject[] pilotRoots = pilot.GetRootGameObjects();
                var backdropRoots = new List<GameObject>();
                for (int neighbourZ = cell.Z - vegetationRadius; neighbourZ <= cell.Z + vegetationRadius; neighbourZ++)
                for (int neighbourX = cell.X - vegetationRadius; neighbourX <= cell.X + vegetationRadius; neighbourX++)
                {
                    if (neighbourX == cell.X && neighbourZ == cell.Z) continue;
                    ProductionWorldCellLayerScene layer = manifest.CellLayers.FirstOrDefault(value => value.LayerId == "vegetation" &&
                        value.Index.X == neighbourX && value.Index.Z == neighbourZ);
                    if (string.IsNullOrEmpty(layer.ScenePath) || !File.Exists(layer.ScenePath))
                    {
                        report.unavailableNeighborVegetationCells.Add(new WorldCellIndex(neighbourX, neighbourZ).Id);
                        continue;
                    }
                    EditorSceneManager.OpenScene(layer.ScenePath, OpenSceneMode.Additive);
                    report.loadedScenes.Add(layer.ScenePath);
                    report.loadedNeighborVegetationScenes.Add(layer.ScenePath);
                }
                if (report.unavailableNeighborVegetationCells.Count > 0)
                    report.notes.Add("Some neighbouring generated vegetation cells are unavailable within the configured forest radius. Distant appearance is cell context, not full-map boundary acceptance.");
                ProductionWorldCellLayerScene[] nearbyBackdropLayers = manifest
                    .CellLayers.Where(layer =>
                        layer.LayerId == MapVegetationBackdropPresentation.LayerId &&
                        Mathf.Max(Mathf.Abs(layer.Index.X - cell.X),
                            Mathf.Abs(layer.Index.Z - cell.Z)) <=
                        layer.GetLoadingRadius(manifest.LoadingRadiusCells))
                    .OrderBy(layer => layer.Index.Z)
                    .ThenBy(layer => layer.Index.X).ToArray();
                report.registeredNearbyBackdropSceneCount =
                    nearbyBackdropLayers.Length;
                foreach (ProductionWorldCellLayerScene layer in
                         nearbyBackdropLayers)
                {
                    if (string.IsNullOrEmpty(layer.ScenePath) ||
                        !File.Exists(layer.ScenePath))
                    {
                        report.unavailableBackdropCells.Add(layer.CellId);
                        continue;
                    }
                    Scene backdrop = EditorSceneManager.OpenScene(
                        layer.ScenePath, OpenSceneMode.Additive);
                    report.loadedScenes.Add(layer.ScenePath);
                    report.loadedBackdropScenes.Add(layer.ScenePath);
                    report.loadedBackdropCellIds.Add(layer.CellId);
                    backdropRoots.AddRange(backdrop.GetRootGameObjects());
                }
                var treePositions = new List<Vector3>();
                var boundaryPositions = new List<Vector3>();
                var grass = new List<GrassPoint>();
                var treeBounds = new List<Bounds>();
                var backdropRendererBounds = new List<Bounds>();
                var materials = new HashSet<Material>();
                var auditedGrassProfiles = new HashSet<string>(StringComparer.Ordinal);
                MapVegetationCaptureScope scope = MapVegetationCaptureScope.Evaluate(
                    options.Categories, cell.Id, pilotRoots, requireRepresentative: !cellOverride);
                report.selectedCategories = options.Categories.ToString();
                report.requiresRepresentativeSelectedCategories = !cellOverride;
                report.backdropLayerRequired = scope.RequiresDeepBoundaryView;
                report.generatedFingerprint = scope.Fingerprint;
                report.preservedCategoryFingerprints.AddRange(scope.PreservedCategoryFingerprints);
                report.notes.AddRange(scope.Errors);
                foreach (GameObject root in pilotRoots)
                {
                    GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                    if (group == null || group.GeneratorId != MapVegetationRebuildOptions.GeneratorId) continue;
                    PackedWoodyCellRenderer packedRenderer = root
                        .GetComponent<PackedWoodyCellRenderer>();
                    PackedWoodyCellAsset packed = packedRenderer != null
                        ? packedRenderer.CellAsset : null;
                    if (packed != null)
                    {
                        foreach (PackedWoodyPrototypeAsset prototype in
                                 packed.Prototypes)
                        {
                            if (prototype == null) continue;
                            foreach (PackedWoodyLod lod in prototype.Lods)
                            foreach (PackedWoodyDrawPart part in lod.DrawParts)
                                if (part?.Material != null)
                                    materials.Add(part.Material);
                        }
                    }
                    if (group.Category == MapVegetationCategories.OriginalTrees.ToString() || group.Category == MapVegetationCategories.BoundaryForest.ToString())
                    {
                        if (packed != null)
                        {
                            foreach (PackedWoodyPlacementRecord placement in
                                     packed.Placements)
                            {
                                Vector3 position = placement.WorldPosition;
                                treePositions.Add(position);
                                if (placement.Category ==
                                    PackedWoodyCategory.BoundaryForest)
                                    boundaryPositions.Add(position);
                                PackedWoodyBatch batch =
                                    packed.Batches[placement.BatchIndex];
                                Matrix4x4 matrix =
                                    batch.Matrices[placement.MatrixIndex];
                                PackedWoodyPrototypeAsset prototype =
                                    packed.Prototypes[
                                        placement.PrototypeIndex];
                                treeBounds.Add(MapVegetationPackedWoodyBuilder
                                    .TransformBounds(prototype.LocalBounds,
                                        matrix));
                            }
                        }
                        else
                        {
                            // Compatibility with an already-saved pre-packing
                            // pilot while the current deterministic writer is
                            // migrating the same category in place.
                            foreach (Transform child in root.transform)
                            {
                                treePositions.Add(child.position);
                                if (group.Category == MapVegetationCategories
                                        .BoundaryForest.ToString())
                                    boundaryPositions.Add(child.position);
                                Renderer[] renderers = child
                                    .GetComponentsInChildren<Renderer>(true);
                                if (renderers.Length == 0) continue;
                                Bounds bounds = renderers[0].bounds;
                                foreach (Renderer renderer in renderers)
                                    bounds.Encapsulate(renderer.bounds);
                                treeBounds.Add(bounds);
                            }
                        }
                    }
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                        foreach (Material material in renderer.sharedMaterials) materials.Add(material);
                    foreach (VegetationWorldRenderer renderer in root.GetComponentsInChildren<VegetationWorldRenderer>(true))
                    {
                        if (!renderer.enabled || renderer.Catalog == null) continue;
                        renderer.RebuildGpuResources();
                        report.gpuGrassBatches += renderer.GpuBatchCount;
                        foreach (VegetationProfile profile in renderer.Catalog.Profiles)
                            if (profile != null) materials.Add(profile.Material);
                        foreach (VegetationCellAsset grassCell in renderer.Catalog.Cells)
                        foreach (VegetationTileRecord tile in grassCell.Tiles)
                        foreach (VegetationProfileTileInstances batch in tile.ProfileInstances)
                        {
                            VegetationProfile profile = renderer.Catalog.Profiles[batch.ProfileIndex];
                            if (profile != null && auditedGrassProfiles.Add(profile.ProfileId))
                                report.grassProfiles.Add(GrassProfileAudit.From(profile,
                                    options.Placement.Category(MapVegetationKind.Grass)));
                            foreach (VegetationInstanceRecord instance in batch.Instances)
                                grass.Add(new GrassPoint(instance.WorldPosition, profile.CullingDistance));
                        }
                    }
                }
                foreach (GameObject root in backdropRoots)
                {
                    foreach (GeneratedDistantForestBatch batch in root
                                 .GetComponentsInChildren<
                                     GeneratedDistantForestBatch>(true))
                        report.loadedBackdropTrees += batch.InstanceCount;
                    report.loadedBackdropColliderCount += root
                        .GetComponentsInChildren<Collider>(true).Length;
                    foreach (MeshRenderer renderer in root
                                 .GetComponentsInChildren<MeshRenderer>(true))
                    {
                        report.loadedBackdropRendererCount++;
                        backdropRendererBounds.Add(renderer.bounds);
                        Mesh mesh = renderer.GetComponent<MeshFilter>()
                            ?.sharedMesh;
                        if (mesh != null)
                            report.loadedBackdropVertices += mesh.vertexCount;
                        foreach (Material material in renderer.sharedMaterials)
                            materials.Add(material);
                    }
                }
                report.savedTrees = treePositions.Count;
                report.savedBoundaryTrees = boundaryPositions.Count;
                report.savedGrassInstances = grass.Count;
                if (File.Exists(MapVegetationBackdropPresentation.ReportPath))
                {
                    MapVegetationBackdropPresentation.BackdropPresentationReport
                        backdropReport = JsonUtility.FromJson<
                            MapVegetationBackdropPresentation
                                .BackdropPresentationReport>(File.ReadAllText(
                                    MapVegetationBackdropPresentation
                                        .ReportPath));
                    if (backdropReport != null)
                    {
                        report.backdropGeometricReportPassed =
                            backdropReport.passed;
                        report.backdropBoundarySegmentCount =
                            backdropReport.boundarySegmentCount;
                        report.backdropBoundarySampleCount =
                            backdropReport.boundarySampleCount;
                        report.backdropUncoveredBoundarySampleCount =
                            backdropReport.uncoveredBoundarySampleCount;
                        report.backdropUncoveredCrownIntervalCount =
                            backdropReport.uncoveredCrownIntervalCount;
                        report.backdropMaximumCrownSilhouetteGapMeters =
                            backdropReport.maximumCrownSilhouetteGapMeters;
                        report.backdropOuterEnclosureCount =
                            backdropReport.outerEnclosureCount;
                        report.backdropSyntheticClosureSegmentCount =
                            backdropReport.syntheticClosureSegmentCount;
                        report.backdropPlannedSyntheticClosureSegmentCount =
                            backdropReport
                                .plannedSyntheticClosureSegmentCount;
                        report.backdropWrongSideOrDepthPlacementCount =
                            backdropReport.wrongSideOrDepthPlacementCount;
                        report.backdropInsideEnclosurePlacementCount =
                            backdropReport.insideEnclosurePlacementCount;
                        report.backdropDepthBandCoverage =
                            backdropReport.depthBandCoverage ?? Array.Empty<
                                MapVegetationBackdropPresentation
                                    .BackdropDepthBandCoverage>();
                        report.backdropDepthBandsPassed = report
                            .backdropDepthBandCoverage.Length == 3 && report
                            .backdropDepthBandCoverage.All(band =>
                                band.sampleCount > 0 &&
                                band.coveredSampleCount == band.sampleCount);
                        report.backdropUncoveredBoundarySegmentIds.AddRange(
                            backdropReport.uncoveredBoundarySegmentIds ??
                            Array.Empty<string>());
                    }
                }
                else if (report.backdropLayerRequired)
                {
                    report.notes.Add(
                        "The streamed backdrop geometry report is missing; full-perimeter coverage is unverified.");
                }
                foreach (Material material in materials)
                {
                    if (material == null) report.missingOrUnsupportedShaders.Add("Missing material reference");
                    else if (material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(material.shader))
                        report.missingOrUnsupportedShaders.Add(material.name + ": " + (material.shader == null ? "missing shader" : material.shader.name));
                }
                if (scope.PreservedCategoryFingerprints.Count > 0)
                    report.notes.Add("Unselected generated categories remain rendered as context; their preserved fingerprints are not the current pilot's generation fingerprint.");

                Scene auditScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SetActiveScene(auditScene);
                camera = CreateCamera(temporary);
                CreateLighting(temporary);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                Vector3 centerGround = NearestGrass(grass, center, treePositions.FirstOrDefault());

                camera.orthographic = true;
                camera.orthographicSize = manifest.CellSizeMeters * 0.55f;
                camera.transform.position = new Vector3(cellCenter.x, centerGround.y + 600f, cellCenter.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Capture(camera, target, pixels, "01_Pilot_Overhead", "Whole audited cell remains centered regardless of an explicit eye-level focus. Actual LODs: small trees can be LOD-culled, far vertical billboards are edge-on from above, and grass is distance-culled; this is not a tree-count image.", treeBounds, grass, report, outputPath);

                Vector3 treeTarget = treePositions.Count > 0 ? Nearest(treePositions, center) : centerGround;
                Vector3 eye = NearestGrass(grass, treeTarget + new Vector3(0f, 0f, -18f), centerGround) + Vector3.up * 1.7f;
                ConfigureEye(camera, eye, treeTarget + Vector3.up * 3f);
                Capture(camera, target, pixels, "02_Forest_EyeLevel", "Camera grounded on a saved generated grass point; trees and grass use their actual saved LOD/culling settings.", treeBounds, grass, report, outputPath);

                if (report.grassCloseViewRequired)
                {
                    GrassAuditSelection grassSelection =
                        SelectDenseGrassAuditPoint(grass.Select(point =>
                            point.Position).ToArray(), center, centerGround,
                            GrassAuditNeighborhoodRadiusMeters);
                    report.grassAuditSelectedPoint = grassSelection.Position;
                    report.grassAuditSelectedNearbyInstances =
                        grassSelection.NearbyInstances;
                    report.grassAuditNeighborhoodRadiusMeters =
                        GrassAuditNeighborhoodRadiusMeters;
                    report.grassAuditMinimumNearbyInstances =
                        GrassAuditMinimumNearbyInstances;
                    report.grassAuditForwardInstances =
                        grassSelection.ForwardInstances;
                    report.grassAuditMinimumForwardInstances =
                        GrassAuditMinimumForwardInstances;
                    report.grassAuditNearestForwardInstanceMeters =
                        grassSelection.NearestForwardInstanceMeters;
                    report.grassAuditMaximumNearestForwardMeters =
                        GrassAuditMaximumNearestForwardMeters;
                    Vector3 grassEye = grassSelection.Position +
                                       Vector3.up * 0.65f;
                    ConfigureEye(camera, grassEye,
                        grassEye + grassSelection.LookDirection * 9f -
                        Vector3.up * 0.35f);
                    Capture(camera, target, pixels, "02B_Grass_Carpet_Close",
                        "Dedicated low eye-level acceptance frame from the densest deterministic ten-metre saved-grass neighbourhood. Inspect that near cover reads as a low, irregular meadow carpet with crossed volume, not rows of tall identical tussocks; the gate also requires at least 256 nearby saved records, but visual acceptance remains manual.",
                        treeBounds, grass, report, outputPath);
                    ViewReport grassView = report.views[report.views.Count - 1];
                    report.grassCloseViewPassed =
                        grassSelection.NearbyInstances >=
                        GrassAuditMinimumNearbyInstances &&
                        grassSelection.ForwardInstances >=
                        GrassAuditMinimumForwardInstances &&
                        grassSelection.NearestForwardInstanceMeters >= 0f &&
                        grassSelection.NearestForwardInstanceMeters <=
                        GrassAuditMaximumNearestForwardMeters &&
                        grassView.grassInstancesInFrustumWithinCullingDistance >=
                        GrassAuditMinimumNearbyInstances;
                }

                if (TryRoadView(sourceRoots, cell, manifest.CellSizeMeters, center, out Vector3 road, out Vector3 roadDirection))
                {
                    ConfigureEye(camera, road + Vector3.up * 1.7f, road + roadDirection * 25f + Vector3.up * 1.2f);
                    MapVegetationRoadVisualDiagnostics.WriteForLoadedRoots(sourceRoots, pilotRoots, options.Placement,
                        camera.transform.position, Path.Combine(outputPath, "road-surface-diagnostics.json"));
                    Capture(camera, target, pixels, "03_Road_EyeLevel", "Camera on an active canonical paved/dirt-road triangle, looking along its longest edge; roadside vegetation and the actual road are both visible.", treeBounds, grass, report, outputPath);
                }
                else
                {
                    ConfigureEye(camera, NearestGrass(grass, treeTarget + new Vector3(18f, 0f, 0f), centerGround) + Vector3.up * 1.7f, treeTarget + Vector3.up * 3f);
                    Capture(camera, target, pixels, "03_Forest_Alternate", "No eligible road triangle in the audited cell; alternate eye-level view, not a road-clearance visual test.", treeBounds, grass, report, outputPath);
                }

                Vector3 edgeTarget = boundaryPositions.Count > 0 ? Nearest(boundaryPositions, center) : center + Vector3.right * manifest.CellSizeMeters * 0.45f;
                Vector3 inward = center - edgeTarget; inward.y = 0f;
                if (inward.sqrMagnitude < 1f) inward = Vector3.back;
                Vector3 edgeEye = NearestGrass(grass, edgeTarget + inward.normalized * 35f, centerGround) + Vector3.up * 1.7f;
                ConfigureEye(camera, edgeEye, new Vector3(edgeTarget.x, edgeEye.y + 2f, edgeTarget.z));
                string edgeNote = boundaryPositions.Count > 0
                    ? "Front transition of the selected boundary band, intentionally retaining the original nearest-centre view. Young growth here is not a view of the deepest wall buffer."
                    : "Audited cell has no generated boundary trees. Cell-edge context only; world-boundary visual acceptance remains untested.";
                if (Mathf.Abs(edgeEye.y - 1.7f - edgeTarget.y) > 10f)
                    edgeNote = "Cell-edge diagnostic from a different ground elevation than the selected tree. The grass-grounded camera can land on an existing low technical patch; this is not evidence of the front transition from the original high ground. Inspect the recorded camera position and the separate deep-boundary view.";
                Capture(camera, target, pixels, "04_Edge_EyeLevel", edgeNote, treeBounds, grass, report, outputPath);

                if (scope.RequiresDeepBoundaryView && TryDeepBoundaryView(sourceRoots, boundaryPositions, grass, center, options, out BoundaryProbe probe))
                {
                    if (cellOverride)
                        AdjustContextBoundaryCamera(probe, grass, treePositions, report.notes);
                    report.deepBoundary = probe;
                    ConfigureEye(camera, probe.eye, probe.wallPoint + Vector3.up * (probe.eye.y - probe.wallPoint.y + 0.5f));
                    Capture(camera, target, pixels, "05_DeepBoundary_EyeLevel",
                        "Diagnostic view toward the actual recovered wall segment from the generated buffer. Nearby registered vegetation-backdrop cell layers are loaded with their saved bounds and materials; inspect whether they conceal the exterior gap. One sector cannot prove the whole perimeter, so capture-report also carries the all-segment geometric coverage result.", treeBounds, grass, report, outputPath,
                        backdropRendererBounds);
                    report.deepBoundaryBackdropFrustumPassed = report.views[
                            report.views.Count - 1]
                        .backdropRenderersIntersectingFrustum > 0;
                    if (!report.deepBoundaryBackdropFrustumPassed)
                        report.notes.Add(
                            "05_DeepBoundary_EyeLevel contains zero backdrop renderer bounds in its camera frustum; the PNG is not backdrop evidence.");
                    if (backdropRendererBounds.Count > 0)
                    {
                        Vector3 backdropTarget = backdropRendererBounds
                            .OrderBy(bounds => new Vector2(
                                bounds.center.x - probe.wallPoint.x,
                                bounds.center.z - probe.wallPoint.z)
                                .sqrMagnitude).First().center;
                        Vector3 outward = backdropTarget - probe.wallPoint;
                        outward.y = 0f;
                        if (outward.sqrMagnitude < 1f)
                            outward = probe.wallPoint - center;
                        if (outward.sqrMagnitude < 1f)
                            outward = Vector3.forward;
                        outward.Normalize();
                        Vector3 ringEye = probe.wallPoint - outward * 110f +
                            Vector3.up * 90f;
                        ConfigureEye(camera, ringEye,
                            backdropTarget + Vector3.up * 8f);
                        Capture(camera, target, pixels,
                            "05B_Backdrop_Ring_Oblique",
                            "Oblique evidence of the actually loaded collisionless vegetation-backdrop cell batches outside this recovered wall sector. Renderers retain normal frustum culling, materials and saved bounds; this frame is local evidence only, while the report's sampled-segment counters audit the complete boundary.",
                            treeBounds, grass, report, outputPath,
                            backdropRendererBounds);
                        report.backdropRingViewCaptured = true;
                        report.backdropRingFrustumPassed = report.views[
                                report.views.Count - 1]
                            .backdropRenderersIntersectingFrustum > 0;
                        if (!report.backdropRingFrustumPassed)
                            report.notes.Add(
                                "05B_Backdrop_Ring_Oblique contains zero backdrop renderer bounds in its camera frustum; the PNG is not backdrop evidence.");
                    }
                }
                else if (scope.RequiresDeepBoundaryView)
                    report.notes.Add("No grounded deep-boundary camera could be resolved from actual wall geometry; deep-buffer visual acceptance remains pending.");

                if (focusXZ.HasValue)
                {
                    // Explicit focus is restricted to context captures by AuditFocus.
                    // Reuse the reported nearby saved-ground height; do not move
                    // geometry, force LODs, or claim this is a raycast at the focus.
                    Vector3 focusTarget = new Vector3(focusXZ.Value.x, centerGround.y, focusXZ.Value.y);
                    Vector3 focusEye = focusTarget + new Vector3(0f, 25f, -40f);
                    ConfigureEye(camera, focusEye, focusTarget);
                    Capture(camera, target, pixels, "06_Focus_Oblique",
                        "Looks at the exact requested focus XZ from +25m height and -40m Z. Target Y reuses centerGround from the nearest saved grass point (fallback: first saved tree, or zero if neither exists), not a ground raycast at the focus. Original geometry, occlusion and LODs remain active; inspect landmark visibility.", treeBounds, grass, report, outputPath);
                    ViewReport focusView = report.views[report.views.Count - 1];
                    focusView.hasExplicitLookTarget = true;
                    focusView.explicitLookTarget = focusTarget;
                }

                report.grassGeometryPassed = !report.grassCloseViewRequired ||
                    report.grassProfiles.Count > 0 && report.grassProfiles.All(profile => profile.geometryPassed);
                if (report.grassCloseViewRequired)
                {
                    if (!report.grassCloseViewPassed)
                        report.notes.Add("02B_Grass_Carpet_Close.png did not capture the required dense saved-grass neighbourhood; visual acceptance cannot use this frame.");
                    report.notes.Add("Visual grass acceptance still requires inspecting 02B_Grass_Carpet_Close.png; dense-neighbourhood evidence cannot semantically decide whether the meadow looks natural.");
                }
                report.backdropLayerPassed = !report.backdropLayerRequired ||
                    report.loadedBackdropScenes.Count > 0 &&
                    report.unavailableBackdropCells.Count == 0 &&
                    report.loadedBackdropTrees > 0 &&
                    report.loadedBackdropColliderCount == 0 &&
                    report.backdropGeometricReportPassed &&
                    report.backdropUncoveredBoundarySampleCount == 0 &&
                    report.backdropUncoveredCrownIntervalCount == 0 &&
                    report.backdropMaximumCrownSilhouetteGapMeters <=
                        0.001f &&
                    report.backdropOuterEnclosureCount > 0 &&
                    report.backdropSyntheticClosureSegmentCount ==
                    report.backdropPlannedSyntheticClosureSegmentCount &&
                    report.backdropDepthBandsPassed &&
                    report.backdropWrongSideOrDepthPlacementCount == 0 &&
                    report.backdropInsideEnclosurePlacementCount == 0 &&
                    report.backdropRingViewCaptured &&
                    report.deepBoundaryBackdropFrustumPassed &&
                    report.backdropRingFrustumPassed;
                int expectedViews = scope.RequiredViewCount + (report.grassCloseViewRequired ? 1 : 0) +
                    (report.backdropLayerRequired ? 1 : 0) +
                    (focusXZ.HasValue ? 1 : 0);
                report.passed = scope.IsValid && report.grassGeometryPassed &&
                    (!report.grassCloseViewRequired ||
                     report.grassCloseViewPassed) &&
                    report.backdropLayerPassed &&
                    report.missingOrUnsupportedShaders.Count == 0 &&
                    report.views.Count == expectedViews &&
                    report.views.All(view => view.intensityRange >= 3 && view.magentaPixelFraction <= 0.01f);
                File.WriteAllText(Path.Combine(outputPath, "capture-report.json"), JsonUtility.ToJson(report, true));
                if (!report.passed)
                    throw new InvalidOperationException("Saved HDRP capture failed material/reference/image checks; inspect capture-report.json and actual PNGs before full generation.");
                Debug.Log("MAP_VEGETATION_VISUAL_CAPTURE_OK cell=" + cell.Id + " views=" + report.views.Count + " output=" + Path.GetFullPath(outputPath));
            }
            catch (Exception exception)
            {
                report.passed = false;
                report.notes.Add("Capture failed: " + exception);
                Directory.CreateDirectory(outputPath);
                File.WriteAllText(Path.Combine(outputPath, "capture-report.json"), JsonUtility.ToJson(report, true));
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
                try
                {
                    if (original.Any(setup => !string.IsNullOrEmpty(setup.path))) EditorSceneManager.RestoreSceneManagerSetup(original);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally { if (assetLease != null) Object.DestroyImmediate(assetLease); }
            }
        }

        private static Camera CreateCamera(List<Object> temporary)
        {
            var owner = new GameObject("Vegetation audit camera") { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(owner);
            Camera camera = owner.AddComponent<Camera>();
            HDAdditionalCameraData hdCamera = owner.AddComponent<HDAdditionalCameraData>();
            hdCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            hdCamera.antialiasing =
                HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hdCamera.TAAQuality =
                HDAdditionalCameraData.TAAQualityLevel.High;
            hdCamera.customRenderingSettings = true;
            SetFrameSetting(hdCamera, FrameSettingsField.Postprocess, true);
            SetFrameSetting(hdCamera, FrameSettingsField.Antialiasing, true);
            SetFrameSetting(hdCamera, FrameSettingsField.MotionVectors, true);
            SetFrameSetting(
                hdCamera,
                FrameSettingsField.ObjectMotionVectors,
                true);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.aspect = Width / (float)Height;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1200f;
            return camera;
        }

        private static void CreateLighting(List<Object> temporary)
        {
            var volumeObject = new GameObject("Vegetation audit daylight exposure") { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(volumeObject);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100000f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            temporary.Add(profile);
            VisualEnvironment environment = profile.Add<VisualEnvironment>();
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            GradientSky sky = profile.Add<GradientSky>();
            sky.top.Override(new Color(0.38f, 0.48f, 0.58f));
            sky.middle.Override(new Color(0.62f, 0.67f, 0.70f));
            sky.bottom.Override(new Color(0.20f, 0.21f, 0.19f));
            sky.skyIntensityMode.Override(SkyIntensityMode.Exposure);
            sky.exposure.Override(13f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            Exposure exposure = profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(14f);
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            // The gameplay fallback profile enables motion blur. Keep it active
            // in this audit so invalid procedural motion vectors become visible
            // instead of passing a static geometry-only screenshot.
            MotionBlur motionBlur = profile.Add<MotionBlur>();
            motionBlur.intensity.Override(0.5f);
            motionBlur.maximumVelocity.Override(200f);
            motionBlur.cameraMotionBlur.Override(true);
            Fog fog = profile.Add<Fog>();
            fog.enabled.Override(false);
            volume.sharedProfile = profile;
            var sun = new GameObject("Vegetation audit sun") { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(sun);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            HDAdditionalLightData hdLight = sun.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Lux;
            light.intensity = 100000f;
            light.color = new Color(1f, 0.98f, 0.94f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            hdLight.UpdateAllLightValues();
            RenderSettings.sun = light;
            if (RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline) pipeline.RequestSkyEnvironmentUpdate();
        }

        private static void SetFrameSetting(
            HDAdditionalCameraData hdCamera,
            FrameSettingsField field,
            bool enabled)
        {
            hdCamera.renderingPathCustomFrameSettings.SetEnabled(
                field,
                enabled);
            FrameSettingsOverrideMask mask =
                hdCamera.renderingPathCustomFrameSettingsOverrideMask;
            mask.mask[(uint)field] = true;
            hdCamera.renderingPathCustomFrameSettingsOverrideMask = mask;
        }

        private static void ConfigureEye(Camera camera, Vector3 eye, Vector3 target)
        {
            camera.orthographic = false;
            camera.fieldOfView = 65f;
            camera.transform.position = eye;
            camera.transform.LookAt(target);
        }

        private static void Capture(Camera camera, RenderTexture target,
            Texture2D pixels, string name, string note, List<Bounds> trees,
            List<GrassPoint> grass, AuditReport report, string outputPath,
            List<Bounds> backdropRenderers = null)
        {
            camera.Render();
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            pixels.Apply();
            string path = Path.Combine(outputPath, name + ".png");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            int treeCount = trees.Count(bounds => GeometryUtility.TestPlanesAABB(frustum, bounds));
            int backdropRendererCount = backdropRenderers?.Count(bounds =>
                GeometryUtility.TestPlanesAABB(frustum, bounds)) ?? 0;
            int grassCount = 0;
            foreach (GrassPoint point in grass)
            {
                if ((point.Position - camera.transform.position).sqrMagnitude > point.CullDistance * point.CullDistance) continue;
                if (GeometryUtility.TestPlanesAABB(frustum, new Bounds(point.Position + Vector3.up * 0.4f, Vector3.one))) grassCount++;
            }
            Color32[] data = pixels.GetPixels32();
            int magenta = 0;
            byte min = 255, max = 0;
            foreach (Color32 color in data)
            {
                if (color.r > 210 && color.b > 210 && color.g < 45) magenta++;
                byte intensity = (byte)((color.r + color.g + color.b) / 3);
                min = Math.Min(min, intensity); max = Math.Max(max, intensity);
            }
            report.views.Add(new ViewReport { file = path, position = camera.transform.position,
                rotationEuler = camera.transform.eulerAngles, note = note,
                treesIntersectingFrustum = treeCount, grassInstancesInFrustumWithinCullingDistance = grassCount,
                backdropRenderersIntersectingFrustum = backdropRendererCount,
                magentaPixelFraction = magenta / (float)data.Length, intensityRange = max - min });
            if (max - min < 3) report.notes.Add(name + ": nearly uniform rendered image; visual acceptance failed/pending inspection.");
            if (magenta > data.Length / 100) report.notes.Add(name + ": >1% magenta pixels; inspect shader/material errors.");
        }

        private static bool TryRoadView(List<GameObject> roots, WorldCellIndex cell, float cellSize, Vector3 center, out Vector3 point, out Vector3 direction)
        {
            point = default;
            direction = Vector3.forward;
            float nearest = float.PositiveInfinity;
            foreach (GameObject root in roots)
            foreach (DonorWorldBaselineEntityMetadata entity in root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(true))
            {
                string path = entity.SourceHierarchyPath ?? string.Empty;
                if (!RoadPath(path, "/TERRAIN_OBJ/ROAD") && !RoadPath(path, "/TERRAIN_OBJ/DIRTROAD")) continue;
                foreach (MeshFilter filter in entity.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !filter.gameObject.activeInHierarchy ||
                        filter.GetComponentInParent<DonorWorldBaselineEntityMetadata>() != entity) continue;
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    int[] triangles = filter.sharedMesh.triangles;
                    Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]), b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]), c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                        Vector3 position = (a + b + c) / 3f;
                        if (!MapVegetationPlanning.CellAt(position, cellSize).Equals(cell)) continue;
                        if (Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, Vector3.up)) < 0.7f) continue;
                        float distance = (new Vector2(position.x - center.x, position.z - center.z)).sqrMagnitude;
                        if (distance < nearest)
                        {
                            nearest = distance; point = position;
                            Vector3 edge = b - a;
                            if ((c - b).sqrMagnitude > edge.sqrMagnitude) edge = c - b;
                            if ((a - c).sqrMagnitude > edge.sqrMagnitude) edge = a - c;
                            direction = edge.normalized;
                        }
                    }
                }
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private static bool RoadPath(string path, string segment)
        {
            int index = path.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
            int end = index + segment.Length;
            return index >= 0 && (end == path.Length || path[end] == '/');
        }

        private static Vector3 Nearest(IEnumerable<Vector3> positions, Vector3 target)
        {
            Vector3 result = target;
            float nearest = float.PositiveInfinity;
            foreach (Vector3 position in positions)
            {
                float distance = (new Vector2(position.x - target.x, position.z - target.z)).sqrMagnitude;
                if (distance < nearest) { nearest = distance; result = position; }
            }
            return result;
        }

        internal static GrassAuditSelection SelectDenseGrassAuditPoint(
            IReadOnlyList<Vector3> positions, Vector3 target,
            Vector3 fallback, float neighborhoodRadiusMeters =
                GrassAuditNeighborhoodRadiusMeters)
        {
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));
            if (!float.IsFinite(neighborhoodRadiusMeters) ||
                neighborhoodRadiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(neighborhoodRadiusMeters));

            float bucketSize = neighborhoodRadiusMeters * 0.5f;
            var buckets = new Dictionary<Vector2Int, List<Vector3>>();
            foreach (Vector3 position in positions)
            {
                if (!float.IsFinite(position.x) ||
                    !float.IsFinite(position.y) ||
                    !float.IsFinite(position.z))
                    continue;
                var key = new Vector2Int(
                    Mathf.FloorToInt(position.x / bucketSize),
                    Mathf.FloorToInt(position.z / bucketSize));
                if (!buckets.TryGetValue(key, out List<Vector3> values))
                    buckets.Add(key, values = new List<Vector3>());
                values.Add(position);
            }
            if (buckets.Count == 0)
                return new GrassAuditSelection(fallback, 0,
                    Vector3.forward);

            Vector2Int selectedBucket = default;
            int selectedApproximateCount = -1;
            float selectedBucketDistance = float.PositiveInfinity;
            bool hasBucket = false;
            foreach (Vector2Int key in buckets.Keys.OrderBy(value => value.x)
                         .ThenBy(value => value.y))
            {
                int approximateCount = 0;
                for (int z = -2; z <= 2; z++)
                for (int x = -2; x <= 2; x++)
                    if (buckets.TryGetValue(new Vector2Int(key.x + x,
                            key.y + z), out List<Vector3> nearby))
                        approximateCount += nearby.Count;
                Vector2 bucketCenter = new Vector2(
                    (key.x + 0.5f) * bucketSize,
                    (key.y + 0.5f) * bucketSize);
                float targetDistance = (bucketCenter -
                    new Vector2(target.x, target.z)).sqrMagnitude;
                if (!hasBucket || approximateCount >
                    selectedApproximateCount ||
                    approximateCount == selectedApproximateCount &&
                    (targetDistance < selectedBucketDistance - 0.0001f ||
                     Mathf.Abs(targetDistance - selectedBucketDistance) <=
                     0.0001f && CompareBucket(key, selectedBucket) < 0))
                {
                    selectedBucket = key;
                    selectedApproximateCount = approximateCount;
                    selectedBucketDistance = targetDistance;
                    hasBucket = true;
                }
            }

            float radiusSquared = neighborhoodRadiusMeters *
                                  neighborhoodRadiusMeters;
            Vector3 selected = fallback;
            int selectedExactCount = -1;
            float selectedTargetDistance = float.PositiveInfinity;
            foreach (Vector3 candidate in buckets[selectedBucket]
                         .OrderBy(value => value.x).ThenBy(value => value.z)
                         .ThenBy(value => value.y))
            {
                int exactCount = 0;
                for (int z = -2; z <= 2; z++)
                for (int x = -2; x <= 2; x++)
                    if (buckets.TryGetValue(new Vector2Int(
                            selectedBucket.x + x,
                            selectedBucket.y + z),
                            out List<Vector3> nearby))
                        foreach (Vector3 point in nearby)
                            if (HorizontalDistanceSquared(candidate, point) <=
                                radiusSquared + 0.0001f)
                                exactCount++;
                float targetDistance = HorizontalDistanceSquared(candidate,
                    target);
                if (exactCount > selectedExactCount ||
                    exactCount == selectedExactCount &&
                    (targetDistance < selectedTargetDistance - 0.0001f ||
                     Mathf.Abs(targetDistance - selectedTargetDistance) <=
                     0.0001f && ComparePosition(candidate, selected) < 0))
                {
                    selected = candidate;
                    selectedExactCount = exactCount;
                    selectedTargetDistance = targetDistance;
                }
            }

            var selectedNeighborhood = new List<Vector3>();
            for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++)
                if (buckets.TryGetValue(new Vector2Int(selectedBucket.x + x,
                        selectedBucket.y + z), out List<Vector3> nearby))
                    foreach (Vector3 point in nearby)
                        if (HorizontalDistanceSquared(selected, point) <=
                            radiusSquared + 0.0001f)
                            selectedNeighborhood.Add(point);

            Vector3 lookDirection = Vector3.forward;
            int forwardInstances = 0;
            float nearestForwardMeters = -1f;
            Vector3 selectedDirectionTarget = selected;
            bool hasDirection = false;
            const float minimumForwardDot = 0.70710678f;
            foreach (Vector3 directionTarget in selectedNeighborhood
                         .OrderBy(value => value.x).ThenBy(value => value.z)
                         .ThenBy(value => value.y))
            {
                Vector3 candidateDirection = directionTarget - selected;
                candidateDirection.y = 0f;
                if (candidateDirection.sqrMagnitude < 0.0001f) continue;
                candidateDirection.Normalize();
                int candidateForwardInstances = 0;
                float candidateNearest = float.PositiveInfinity;
                foreach (Vector3 point in selectedNeighborhood)
                {
                    Vector3 offset = point - selected;
                    offset.y = 0f;
                    float distanceSquared = offset.sqrMagnitude;
                    if (distanceSquared < 0.0001f) continue;
                    float distance = Mathf.Sqrt(distanceSquared);
                    if (Vector3.Dot(candidateDirection, offset / distance) <
                        minimumForwardDot)
                        continue;
                    candidateForwardInstances++;
                    candidateNearest = Mathf.Min(candidateNearest, distance);
                }
                if (!hasDirection || candidateForwardInstances >
                    forwardInstances || candidateForwardInstances ==
                    forwardInstances &&
                    (candidateNearest < nearestForwardMeters - 0.0001f ||
                     Mathf.Abs(candidateNearest - nearestForwardMeters) <=
                     0.0001f && ComparePosition(directionTarget,
                         selectedDirectionTarget) < 0))
                {
                    lookDirection = candidateDirection;
                    forwardInstances = candidateForwardInstances;
                    nearestForwardMeters = candidateNearest;
                    selectedDirectionTarget = directionTarget;
                    hasDirection = true;
                }
            }
            return new GrassAuditSelection(selected,
                Mathf.Max(0, selectedExactCount), lookDirection,
                forwardInstances, nearestForwardMeters);
        }

        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        private static int CompareBucket(Vector2Int a, Vector2Int b)
        {
            int order = a.x.CompareTo(b.x);
            return order != 0 ? order : a.y.CompareTo(b.y);
        }

        private static int ComparePosition(Vector3 a, Vector3 b)
        {
            int order = a.x.CompareTo(b.x);
            if (order == 0) order = a.z.CompareTo(b.z);
            return order != 0 ? order : a.y.CompareTo(b.y);
        }

        private static Vector3 NearestGrass(List<GrassPoint> grass, Vector3 target, Vector3 fallback) => grass.Count == 0 ? fallback : Nearest(grass.Select(point => point.Position), target);

        private static bool TryDeepBoundaryView(List<GameObject> sourceRoots, List<Vector3> boundary, List<GrassPoint> grass,
            Vector3 center, MapVegetationRebuildOptions options, out BoundaryProbe probe)
        {
            probe = null;
            if (boundary.Count == 0) return false;
            // A boundary-only regeneration can legitimately contain no grass.
            // Resolve camera height from current ground, without adding instances
            // or changing the saved category counts used by the capture gate.
            MapVegetationSurfaceQuery cameraGroundQuery = grass.Count == 0
                ? MapVegetationSurfaceQuery.Build(sourceRoots, options.Placement) : null;
            var wallRoots = new List<GameObject>();
            foreach (GameObject root in sourceRoots)
            foreach (DonorWorldBaselineEntityMetadata entity in root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(true))
                if (entity.SourceHierarchyPath == "MAP/MESH/FOLIAGE/TREEWALL_LOW" || entity.SourceHierarchyPath == "MAP/MESH/FOLIAGE/TREEWALL_HI")
                    wallRoots.Add(entity.gameObject);
            // Read only the two wall entities, not all aggregate source trees.
            List<MapVegetationBoundarySegment> segments = MapVegetationDonorSource.Read(wallRoots).BoundarySegments;
            Matrix4x4 mapping = options.Placement.DonorCoordinateMatrix;
            float nearestToCenter = float.PositiveInfinity;
            foreach (Vector3 tree in boundary)
            {
                float closest = float.PositiveInfinity;
                Vector3 closestWall = default;
                string closestId = null;
                foreach (MapVegetationBoundarySegment segment in segments)
                {
                    Vector3 a = mapping.MultiplyPoint3x4(segment.A), b = mapping.MultiplyPoint3x4(segment.B);
                    Vector2 edge = new Vector2(b.x - a.x, b.z - a.z);
                    if (edge.sqrMagnitude < 0.01f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(tree.x - a.x, tree.z - a.z), edge) / edge.sqrMagnitude);
                    Vector3 wall = Vector3.Lerp(a, b, t);
                    float distance = new Vector2(tree.x - wall.x, tree.z - wall.z).magnitude;
                    if (distance < closest) { closest = distance; closestWall = wall; closestId = segment.Id; }
                }
                if (closest > Mathf.Min(15f, options.BoundaryDepthMeters * 0.2f)) continue;
                float centerDistance = new Vector2(tree.x - center.x, tree.z - center.z).sqrMagnitude;
                if (centerDistance >= nearestToCenter) continue;
                Vector3 inward = center - closestWall; inward.y = 0f;
                if (inward.sqrMagnitude < 1f) continue;
                Vector3 requestedGround = tree + inward.normalized * 20f;
                Vector3 cameraGround;
                if (cameraGroundQuery != null)
                {
                    if (!cameraGroundQuery.TryResolve(requestedGround, MapVegetationKind.Grass,
                            out MapVegetationSurfaceHit hit, out _)) continue;
                    cameraGround = hit.Position;
                }
                else cameraGround = NearestGrass(grass, requestedGround, requestedGround);
                if (new Vector2(cameraGround.x - requestedGround.x, cameraGround.z - requestedGround.z).sqrMagnitude > 100f) continue;
                nearestToCenter = centerDistance;
                probe = new BoundaryProbe { sourceSegmentId = closestId, selectedTree = tree, wallPoint = closestWall,
                    selectedTreeDistanceToWallMeters = closest, eye = cameraGround + Vector3.up * 1.7f };
            }
            return probe != null;
        }
        private static void AdjustContextBoundaryCamera(BoundaryProbe probe, List<GrassPoint> grass,
            List<Vector3> trees, List<string> notes)
        {
            const float maximumHorizontalMove = 8f;
            const float maximumGroundHeightChange = 2f;
            const float minimumTrunkDistance = 2.25f;
            probe.cameraClearanceAttempted = true;
            probe.eyeBeforeClearance = probe.eye;
            Vector3 originalGround = probe.eye - Vector3.up * 1.7f;
            Vector3 bestGround = default;
            float bestDistanceSquared = float.PositiveInfinity;
            bool found = false;
            foreach (GrassPoint candidate in grass)
            {
                Vector3 point = candidate.Position;
                float distanceSquared = new Vector2(point.x - originalGround.x, point.z - originalGround.z).sqrMagnitude;
                if (distanceSquared > maximumHorizontalMove * maximumHorizontalMove ||
                    Mathf.Abs(point.y - originalGround.y) > maximumGroundHeightChange ||
                    distanceSquared > bestDistanceSquared) continue;
                // Lexical tie-breaking keeps selection independent of saved tile order.
                if (found && distanceSquared == bestDistanceSquared &&
                    CompareCameraGround(point, bestGround) >= 0) continue;
                bool clear = true;
                foreach (Vector3 tree in trees)
                {
                    if (new Vector2(tree.x - point.x, tree.z - point.z).sqrMagnitude >
                        minimumTrunkDistance * minimumTrunkDistance) continue;
                    clear = false;
                    break;
                }
                if (!clear) continue;
                found = true;
                bestGround = point;
                bestDistanceSquared = distanceSquared;
            }
            probe.cameraClearanceCandidateFound = found;
            if (found)
            {
                probe.eye = bestGround + Vector3.up * 1.7f;
                probe.cameraClearanceAdjusted = (probe.eye - probe.eyeBeforeClearance).sqrMagnitude > 0.000001f;
                probe.cameraClearanceAdjustmentHorizontalMeters = Mathf.Sqrt(bestDistanceSquared);
                probe.cameraClearanceAdjustmentVerticalMeters = probe.eye.y - probe.eyeBeforeClearance.y;
                notes.Add("Context deep-boundary camera uses the nearest saved grass point within 8m horizontally and 2m vertically, more than 2.25m from every audited-cell tree root. This clears nearby trunk centres only; crowns, neighbouring-cell trees and other occluders may remain visible.");
            }
            else
                notes.Add("No saved grass point met the context deep-boundary camera's 8m horizontal / 2m vertical search and 2.25m tree-root clearance. The original grounded camera was retained; foreground obstruction remains unverified.");
            float nearestTrunkSquared = float.PositiveInfinity;
            foreach (Vector3 tree in trees)
                nearestTrunkSquared = Mathf.Min(nearestTrunkSquared,
                    new Vector2(tree.x - probe.eye.x, tree.z - probe.eye.z).sqrMagnitude);
            probe.cameraNearestCurrentCellTrunkMeters = trees.Count > 0 ? Mathf.Sqrt(nearestTrunkSquared) : -1f;
        }

        private static int CompareCameraGround(Vector3 a, Vector3 b)
        {
            int order = a.x.CompareTo(b.x);
            if (order == 0) order = a.z.CompareTo(b.z);
            return order != 0 ? order : a.y.CompareTo(b.y);
        }

        private readonly struct GrassPoint
        {
            public readonly Vector3 Position;
            public readonly float CullDistance;
            public GrassPoint(Vector3 position, float distance) { Position = position; CullDistance = distance; }
        }

        internal readonly struct GrassAuditSelection
        {
            public readonly Vector3 Position;
            public readonly int NearbyInstances;
            public readonly Vector3 LookDirection;
            public readonly int ForwardInstances;
            public readonly float NearestForwardInstanceMeters;

            public GrassAuditSelection(Vector3 position, int nearbyInstances,
                Vector3 lookDirection, int forwardInstances = 0,
                float nearestForwardInstanceMeters = -1f)
            {
                Position = position;
                NearbyInstances = nearbyInstances;
                LookDirection = lookDirection;
                ForwardInstances = forwardInstances;
                NearestForwardInstanceMeters = nearestForwardInstanceMeters;
            }
        }

        [Serializable]
        private sealed class GrassProfileAudit
        {
            public string profileId, channel, material, shader;
            public Vector2 heightRangeMeters;
            public Vector3 lodDistancesMeters;
            public Vector3 nearBoundsMetersAtMaximumScale;
            public int nearTriangles, middleTriangles, farTriangles;
            public float nearHorizontalAspectRatio, nearMinimumProjectedSpanMeters,
                maximumFootprintRadiusMeters, projectedOccupiedCellFraction,
                projectedMaximumEmptyRadiusMeters, alphaCutoff;
            public Vector2 approvedFootprintRadiusRangeMeters;
            public float projectedCoverageDiameterMeters, projectedCoverageProxyRadiusMeters,
                projectedCoverageScaleMeters,
                minimumProjectedOccupiedCellFraction, maximumProjectedEmptyRadiusMeters;
            public int projectedOccupiedCells, projectedEvaluatedCells;
            public Color baseColor;
            public bool projectedCoverageRequired, geometryPassed;

            public static GrassProfileAudit From(VegetationProfile profile,
                MapVegetationCategorySettings grassCategory)
            {
                Mesh near = profile.GetLodMesh(0);
                Mesh middle = profile.GetLodMesh(1);
                Mesh far = profile.GetLodMesh(2);
                float scale = profile.UniformScaleRange.y;
                Vector3 nearSize = near != null ? near.bounds.size : Vector3.zero;
                float minimumHorizontal = Mathf.Min(nearSize.x, nearSize.z);
                float maximumHorizontal = Mathf.Max(nearSize.x, nearSize.z);
                MapVegetationGrassBindings.BindingAudit approved =
                    MapVegetationGrassBindings.ApprovedBindings().Single(binding =>
                        binding.Channel == profile.DensityChannel);
                int maximumTriangles = approved.MaximumNearTriangles;
                float minimumSpan = approved.MinimumNearProjectedSpanMeters;
                int nearTriangles = Triangles(near), middleTriangles = Triangles(middle), farTriangles = Triangles(far);
                float aspect = maximumHorizontal > 0f ? minimumHorizontal / maximumHorizontal : 0f;
                float projectedSpan = minimumHorizontal * scale;
                float footprintRadius = MapVegetationGrassBindings.MeasureMaximumFootprintRadius(
                    profile, grassCategory);
                bool coverageRequired = approved.CoverageDiameterMeters > 0f;
                MapVegetationGrassBindings.ProjectedCoverageAudit coverage = coverageRequired
                    ? MapVegetationGrassBindings.MeasureProjectedCoverage(near, scale,
                        approved.CoverageDiameterMeters, approved.CoverageProxyRadiusMeters)
                    : default;
                Material material = profile.Material;
                return new GrassProfileAudit
                {
                    profileId = profile.ProfileId,
                    channel = profile.DensityChannel.ToString(),
                    material = material != null ? AssetDatabase.GetAssetPath(material) : string.Empty,
                    shader = material != null && material.shader != null ? material.shader.name : string.Empty,
                    heightRangeMeters = profile.UniformScaleRange,
                    lodDistancesMeters = new Vector3(profile.MiddleLodDistance, profile.FarLodDistance, profile.CullingDistance),
                    nearBoundsMetersAtMaximumScale = nearSize * scale,
                    nearTriangles = nearTriangles,
                    middleTriangles = middleTriangles,
                    farTriangles = farTriangles,
                    nearHorizontalAspectRatio = aspect,
                    nearMinimumProjectedSpanMeters = projectedSpan,
                    maximumFootprintRadiusMeters = footprintRadius,
                    approvedFootprintRadiusRangeMeters = approved.FootprintRadiusRangeMeters,
                    projectedCoverageRequired = coverageRequired,
                    projectedCoverageDiameterMeters = approved.CoverageDiameterMeters,
                    projectedCoverageProxyRadiusMeters = approved.CoverageProxyRadiusMeters,
                    projectedCoverageScaleMeters = scale,
                    projectedOccupiedCellFraction = coverage.OccupiedFraction,
                    projectedMaximumEmptyRadiusMeters = coverage.MaximumEmptyRadiusMeters,
                    projectedOccupiedCells = coverage.OccupiedCells,
                    projectedEvaluatedCells = coverage.EvaluatedCells,
                    minimumProjectedOccupiedCellFraction = approved.MinimumOccupiedCoverage,
                    maximumProjectedEmptyRadiusMeters = approved.MaximumEmptyRadiusMeters,
                    alphaCutoff = material != null && material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : -1f,
                    baseColor = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.clear,
                    geometryPassed = near != null && middle != null && far != null &&
                        profile.UniformScaleRange.y <= 0.5001f &&
                        Mathf.Abs(nearSize.y - 1f) <= 0.001f &&
                        nearTriangles >= 24 && nearTriangles <= maximumTriangles &&
                        middleTriangles <= nearTriangles && farTriangles <= nearTriangles &&
                        aspect >= 0.35f && projectedSpan >= minimumSpan &&
                        footprintRadius >= approved.FootprintRadiusRangeMeters.x &&
                        footprintRadius <= approved.FootprintRadiusRangeMeters.y &&
                        (!coverageRequired ||
                            coverage.OccupiedFraction >= approved.MinimumOccupiedCoverage &&
                            coverage.MaximumEmptyRadiusMeters <= approved.MaximumEmptyRadiusMeters)
                };
            }

            private static int Triangles(Mesh mesh)
            {
                if (mesh == null) return 0;
                int count = 0;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                    count += (int)mesh.GetIndexCount(subMesh) / 3;
                return count;
            }
        }

        [Serializable]
        private sealed class AuditReport
        {
            public string cellId, generatedScene, unityVersion, graphicsDevice, settingsHash, generatedFingerprint, outputDirectory;
            public bool passed, cellOverride, explicitFocus,
                grassCloseViewRequired, grassCloseViewPassed,
                grassGeometryPassed;
            public Vector3 grassAuditSelectedPoint;
            public float grassAuditNeighborhoodRadiusMeters,
                grassAuditMaximumNearestForwardMeters,
                grassAuditNearestForwardInstanceMeters;
            public int grassAuditMinimumNearbyInstances,
                grassAuditSelectedNearbyInstances,
                grassAuditMinimumForwardInstances,
                grassAuditForwardInstances;
            public bool backdropLayerRequired, backdropLayerPassed,
                backdropGeometricReportPassed, backdropRingViewCaptured,
                backdropDepthBandsPassed,
                deepBoundaryBackdropFrustumPassed,
                backdropRingFrustumPassed;
            public string selectedCategories;
            public bool requiresRepresentativeSelectedCategories;
            public List<string> preservedCategoryFingerprints = new List<string>();
            public Vector2 requestedFocusXZ, cameraSelectionCenterXZ;
            public string focusScope = "Optional focus changes eye-level tree, road and boundary selection only. Overhead remains centered on the whole audited cell. Focus does not guarantee a landmark or unobstructed sightline appears in a frame.";
            public int vegetationLoadingRadiusCells;
            public int registeredNearbyBackdropSceneCount;
            public int loadedBackdropTrees, loadedBackdropRendererCount,
                loadedBackdropVertices, loadedBackdropColliderCount;
            public int backdropBoundarySegmentCount,
                backdropBoundarySampleCount,
                backdropUncoveredBoundarySampleCount,
                backdropUncoveredCrownIntervalCount,
                backdropOuterEnclosureCount,
                backdropSyntheticClosureSegmentCount,
                backdropPlannedSyntheticClosureSegmentCount,
                backdropWrongSideOrDepthPlacementCount,
                backdropInsideEnclosurePlacementCount;
            public float backdropMaximumCrownSilhouetteGapMeters;
            public MapVegetationBackdropPresentation
                .BackdropDepthBandCoverage[] backdropDepthBandCoverage =
                Array.Empty<MapVegetationBackdropPresentation
                    .BackdropDepthBandCoverage>();
            public int sourceContextRadiusCells = 1;
            public string lighting = "Transient fixed EV14, explicit HDRP directional LightUnit.Lux=100000, neutral gradient sky exposure13 with dynamic ambient, fog disabled. Not the game's Enviro weather or final appearance.";
            public string visibilityMetric = "Audited-cell frustum/cull eligibility counts only; neighbouring generated layers within the configured forest radius may be visible but are not counted. Canonical base cells are source visual context. Occlusion, LOD and pixel-visible coverage require inspecting PNGs.";
            public List<string> loadedScenes = new List<string>();
            public List<string> missingOrUnsupportedShaders = new List<string>();
            public List<string> notes = new List<string>();
            public List<string> loadedNeighborVegetationScenes = new List<string>();
            public List<string> unavailableNeighborVegetationCells = new List<string>();
            public List<string> loadedBackdropScenes = new List<string>();
            public List<string> loadedBackdropCellIds = new List<string>();
            public List<string> unavailableBackdropCells = new List<string>();
            public List<string> backdropUncoveredBoundarySegmentIds =
                new List<string>();
            public BoundaryProbe deepBoundary;
            public List<ViewReport> views = new List<ViewReport>();
            public List<GrassProfileAudit> grassProfiles = new List<GrassProfileAudit>();
            public int savedTrees, savedBoundaryTrees, savedGrassInstances, gpuGrassBatches;
        }

        [Serializable]
        private sealed class BoundaryProbe
        {
            public string sourceSegmentId;
            public Vector3 selectedTree, wallPoint, eye;
            public float selectedTreeDistanceToWallMeters;
            public bool cameraClearanceAttempted, cameraClearanceCandidateFound, cameraClearanceAdjusted;
            public Vector3 eyeBeforeClearance;
            public float cameraClearanceAdjustmentHorizontalMeters, cameraClearanceAdjustmentVerticalMeters;
            public float cameraNearestCurrentCellTrunkMeters;
        }

        [Serializable]
        private sealed class ViewReport
        {
            public string file, note;
            public Vector3 position, rotationEuler;
            public bool hasExplicitLookTarget;
            public Vector3 explicitLookTarget;
            public int treesIntersectingFrustum, grassInstancesInFrustumWithinCullingDistance, intensityRange;
            public int backdropRenderersIntersectingFrustum;
            public float magentaPixelFraction;
        }
    }
}
