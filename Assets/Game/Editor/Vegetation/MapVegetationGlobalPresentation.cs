using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Owns only exact-source canonical rock replacements whose donor authority
    /// is global. Distant forest belongs to streamed backdrop cell scenes.
    /// </summary>
    public static class MapVegetationGlobalPresentation
    {
        public const string DistantTemplateSelectionVersion =
            "msc.distant-template.cheap-broad-far-crown.v3";
        internal const int DistantTemplateMaximumParts = 1;
        internal const int DistantTemplateMaximumVertices = 64;
        public const string SceneId = "vegetation-global-presentation";
        public const string ScenePath = MapVegetationRebuildOptions.GeneratedRoot +
            "/Global/World_Global_VegetationPresentation.unity";
        public const string DataRoot = MapVegetationRebuildOptions.GeneratedRoot +
            "/Global/Data";
        public const string ReportPath =
            "Artifacts/VegetationRebuild/global-presentation.json";
        private const string GeneratorId =
            "msc.map-vegetation-global-presentation.v1";

        public static GlobalPresentationReport WriteAndRegister(
            MapVegetationContext context,
            string runId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            Backup(ScenePath, runId);

            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
            var report = new GlobalPresentationReport
            {
                runId = runId,
                settingsHash = MapVegetationRebuild.SettingsHash(context.Options),
                sourceFingerprint = context.SourceFingerprint,
                canonicalRockCount = context.Source.Rocks.Count,
                rockAccentPlacementCount = context.WoodyCells.Values
                    .SelectMany(plan => plan.Woody).Count(placement =>
                        string.Equals(placement.species,
                            "forest-floor-rock",
                            StringComparison.OrdinalIgnoreCase) ||
                        (placement.id ?? string.Empty).StartsWith(
                            "forest-rock:", StringComparison.Ordinal)),
                legacyReplacementKeys = context.Source.Rocks
                    .Select(rock => rock.ReplacementKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray()
            };
            report.fingerprint = Fingerprint(context, report);
            ApplyBudgetValidation(context, report);
            if (report.errors.Count != 0)
                throw new InvalidOperationException(
                    "Global vegetation presentation exceeds its reviewed hard budget: " +
                    string.Join(" | ", report.errors));
            try
            {
                ValidateExistingOwnership(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    GeneratedVegetationGroup owner =
                        root.GetComponent<GeneratedVegetationGroup>();
                    if (owner != null && owner.GeneratorId == GeneratorId)
                        Object.DestroyImmediate(root);
                }

                var rockRoot = new GameObject("CanonicalRockReplacements");
                SceneManager.MoveGameObjectToScene(rockRoot, scene);
                rockRoot.AddComponent<GeneratedVegetationGroup>().Configure(
                    GeneratorId, "global",
                    MapVegetationCategories.ShrubsAndUndergrowth.ToString(),
                    report.fingerprint);
                rockRoot.AddComponent<DonorWorldLegacyRendererOverride>()
                    .ConfigureForAuthoring(report.legacyReplacementKeys);
                WriteCanonicalRocks(context, rockRoot.transform, report);

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save global vegetation presentation scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            GlobalPresentationReport validated = Validate(context, report);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(validated, true),
                new UTF8Encoding(false));
            if (!validated.passed)
                throw new InvalidDataException(
                    "Generated global vegetation presentation failed validation: " +
                    string.Join(" | ", validated.errors));
            RegisterGlobalScene(context.Manifest);
            return validated;
        }

        public static GlobalPresentationReport Validate(
            MapVegetationContext context,
            GlobalPresentationReport expected = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var report = expected ?? new GlobalPresentationReport
            {
                settingsHash = MapVegetationRebuild.SettingsHash(context.Options),
                sourceFingerprint = context.SourceFingerprint,
                canonicalRockCount = context.Source.Rocks.Count,
                legacyReplacementKeys = context.Source.Rocks
                    .Select(rock => rock.ReplacementKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal).ToArray()
            };
            report.errors.Clear();
            report.canonicalRockMissingAnchorCount = 0;
            report.canonicalRockDuplicateAnchorCount = 0;
            report.canonicalRockUnexpectedAnchorCount = 0;
            report.canonicalRockMissingRendererCount = 0;
            report.canonicalRockMissingMetadataCount = 0;
            report.canonicalRockMetadataMismatchCount = 0;
            report.rockAccentPlacementCount = context.WoodyCells.Values
                .SelectMany(plan => plan.Woody).Count(placement =>
                    string.Equals(placement.species,
                        "forest-floor-rock",
                        StringComparison.OrdinalIgnoreCase) ||
                    (placement.id ?? string.Empty).StartsWith(
                        "forest-rock:", StringComparison.Ordinal));
            report.maximumCanonicalRockAnchorDeviationMeters = 0f;
            report.maximumCanonicalRockMetadataAnchorDeviationMeters = 0f;
            report.maximumCanonicalRockMappedSizeDeviationMeters = 0f;
            report.maximumCanonicalRockPrincipalDirectionDeviationDegrees = 0f;
            report.maximumCanonicalRockUniformScaleDeviation = 0f;
            report.fingerprint = Fingerprint(context, report);
            ApplyBudgetValidation(context, report);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                report.errors.Add("Global vegetation presentation scene is missing.");
                return report;
            }
            Scene scene = EditorSceneManager.OpenScene(ScenePath,
                OpenSceneMode.Additive);
            try
            {
                GeneratedVegetationGroup[] groups = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        GeneratedVegetationGroup>(true)).ToArray();
                if (groups.Length != 1 || groups.Any(group =>
                        group.GeneratorId != GeneratorId ||
                        group.CellId != "global" ||
                        group.Fingerprint != report.fingerprint))
                    report.errors.Add("Global presentation ownership/fingerprint differs from plan.");

                Transform rockRoot = scene.GetRootGameObjects()
                    .Select(root => root.transform)
                    .FirstOrDefault(root => root.name == "CanonicalRockReplacements");
                if (rockRoot == null)
                {
                    report.errors.Add("Global canonical rock root is missing.");
                    return report;
                }
                report.globalDistantBatchCount = scene.GetRootGameObjects()
                    .Sum(root => root.GetComponentsInChildren<
                        GeneratedDistantForestBatch>(true).Length);
                if (report.globalDistantBatchCount != 0)
                    report.errors.Add(
                        "Always-loaded global scene must contain no distant forest batches.");

                int rocks = rockRoot.childCount;
                report.canonicalRockSceneCount = rocks;
                report.canonicalRockRootChildCount = rockRoot.childCount;
                report.canonicalRockRendererCount = rockRoot
                    .GetComponentsInChildren<Renderer>(true).Length;
                report.largestGeneratedRootChildCount =
                    report.canonicalRockRootChildCount;
                if (rocks != report.canonicalRockCount)
                    report.errors.Add($"Canonical rock count differs: scene={rocks}, source={report.canonicalRockCount}.");
                DonorWorldLegacyRendererOverride replacement =
                    rockRoot.GetComponent<DonorWorldLegacyRendererOverride>();
                if (replacement == null || !replacement.ReplacementKeys
                        .SequenceEqual(report.legacyReplacementKeys))
                    report.errors.Add("Canonical rock replacement keys differ from source metadata.");
                if (rockRoot.GetComponent<DonorWorldLegacyReplacementOverride>() !=
                    null)
                    report.errors.Add("Canonical rock visuals must not disable legacy collision.");
                report.canonicalRockColliderCount = rockRoot
                    .GetComponentsInChildren<Collider>(true).Length;
                if (report.canonicalRockColliderCount != 0)
                    report.errors.Add("Canonical ALP rock visuals must contain zero colliders; exact legacy collision remains authoritative.");
                ValidateCanonicalRockAnchors(context, rockRoot, report);
                ApplyBudgetValidation(context, report);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            report.passed = report.errors.Count == 0;
            return report;
        }

        private static void WriteDistantBatches(
            MapVegetationContext context,
            Transform parent,
            DistantBuildPlan plan,
            GlobalPresentationReport report)
        {
            foreach (DistantBatchPlan batchPlan in plan.Batches)
            {
                var batch = new GameObject(batchPlan.SafeName);
                batch.transform.SetParent(parent, false);
                batch.transform.position = batchPlan.Origin;
                batch.AddComponent<GeneratedDistantForestBatch>()
                    .ConfigureForAuthoring(batchPlan.CellId,
                        batchPlan.Species, batchPlan.Placements.Length,
                        batchPlan.Template.Parts.Count);
                GameObjectUtility.SetStaticEditorFlags(batch,
                    StaticEditorFlags.OccludeeStatic);

                for (int partIndex = 0;
                     partIndex < batchPlan.Template.Parts.Count;
                     partIndex++)
                {
                    DistantPart part = batchPlan.Template.Parts[partIndex];
                    string partName = partIndex.ToString("D2",
                        CultureInfo.InvariantCulture) + "_" +
                        Sanitize(part.Name);
                    string meshPath = DataRoot + "/DistantForest/" +
                        batchPlan.SafeName + "_" + partName + ".asset";
                    Mesh mesh = BuildBatchMesh(part,
                        batchPlan.Template.Height, batchPlan.Placements,
                        "DistantForestBatch_" + batchPlan.SafeName + "_" +
                        partName, batchPlan.Origin);
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(
                        meshPath);
                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        existing = mesh;
                    }
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        Object.DestroyImmediate(mesh);
                        EditorUtility.SetDirty(existing);
                    }

                    var partObject = new GameObject(partName);
                    partObject.transform.SetParent(batch.transform, false);
                    partObject.AddComponent<MeshFilter>().sharedMesh = existing;
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
        }

        internal static Mesh BuildBatchMesh(
            DistantPart part,
            float templateHeight,
            IReadOnlyList<MapVegetationPlacement> placements,
            string name,
            Vector3 origin)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            int vertexCount = checked(part.Vertices.Length * placements.Count);
            int indexCount = checked(part.Indices.Length * placements.Count);
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            var uv1 = new Vector4[vertexCount];
            var uv2 = new Vector4[vertexCount];
            var colors = new Color[vertexCount];
            var triangles = new int[indexCount];
            int vertexCursor = 0;
            int indexCursor = 0;
            foreach (MapVegetationPlacement placement in placements)
            {
                float scale = placement.height / Mathf.Max(templateHeight,
                    0.01f);
                Quaternion rotation = Quaternion.Euler(0f, placement.yaw, 0f);
                for (int vertex = 0; vertex < part.Vertices.Length; vertex++)
                {
                    vertices[vertexCursor + vertex] = placement.position -
                        origin + rotation * (part.Vertices[vertex] * scale);
                    normals[vertexCursor + vertex] = (rotation *
                        part.Normals[vertex]).normalized;
                    Vector4 sourceTangent = part.Tangents[vertex];
                    Vector3 tangent = rotation * new Vector3(
                        sourceTangent.x, sourceTangent.y,
                        sourceTangent.z);
                    tangents[vertexCursor + vertex] = new Vector4(
                        tangent.x, tangent.y, tangent.z, sourceTangent.w);
                    uvs[vertexCursor + vertex] = part.Uvs[vertex];
                    uv1[vertexCursor + vertex] = part.Uv1[vertex];
                    uv2[vertexCursor + vertex] = part.Uv2[vertex];
                    colors[vertexCursor + vertex] = part.Colors[vertex];
                }
                for (int index = 0; index < part.Indices.Length; index++)
                    triangles[indexCursor + index] = vertexCursor +
                        part.Indices[index];
                vertexCursor += part.Vertices.Length;
                indexCursor += part.Indices.Length;
            }
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertexCount > ushort.MaxValue
                    ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.SetUVs(1, uv1.ToList());
            mesh.SetUVs(2, uv2.ToList());
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0, true);
            return mesh;
        }

        private static DistantBuildPlan PlanDistantBatches(
            MapVegetationContext context,
            GlobalPresentationReport report)
        {
            var templates = new Dictionary<string, DistantTemplate>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string species in new[]
                     { "Spruce", "Pine", "Birch", "Aspen" })
                templates[species] = DistantTemplate.Load(species);

            var batches = new List<DistantBatchPlan>();
            long vertices = 0L;
            long triangles = 0L;
            int renderers = 0;
            int largestRendererVertices = 0;
            int largestBatchRenderers = 0;
            int largestBatchInstances = 0;
            foreach (IGrouping<string, MapVegetationPlacement> group in
                     context.DistantBackdrop.GroupBy(placement =>
                             placement.cellId + "|" + placement.species,
                             StringComparer.Ordinal)
                         .OrderBy(group => group.Key,
                             StringComparer.Ordinal))
            {
                MapVegetationPlacement[] placements = group
                    .OrderBy(item => item.id, StringComparer.Ordinal).ToArray();
                DistantTemplate template = templates[placements[0].species];
                WorldCellIndex cell = MapVegetationPlanning.CellAt(
                    placements[0].position, context.Manifest.CellSizeMeters);
                var batch = new DistantBatchPlan
                {
                    CellId = placements[0].cellId,
                    Species = placements[0].species,
                    SafeName = Sanitize(placements[0].cellId + "_" +
                        placements[0].species),
                    Origin = new Vector3(
                        cell.X * context.Manifest.CellSizeMeters, 0f,
                        cell.Z * context.Manifest.CellSizeMeters),
                    Placements = placements,
                    Template = template
                };
                batches.Add(batch);
                renderers = checked(renderers + template.Parts.Count);
                largestBatchRenderers = Mathf.Max(largestBatchRenderers,
                    template.Parts.Count);
                largestBatchInstances = Mathf.Max(largestBatchInstances,
                    placements.Length);
                foreach (DistantPart part in template.Parts)
                {
                    long partVertices = checked((long)part.Vertices.Length *
                        placements.Length);
                    vertices = checked(vertices + partVertices);
                    triangles = checked(triangles +
                        (long)part.Indices.Length / 3L * placements.Length);
                    if (partVertices <= int.MaxValue)
                        largestRendererVertices = Mathf.Max(
                            largestRendererVertices, (int)partVertices);
                    else
                        largestRendererVertices = int.MaxValue;
                }
            }

            report.distantBatchGroupCount = batches.Count;
            report.distantRendererCount = renderers;
            report.distantDrawCallBudget = renderers;
            report.distantVertexCount = vertices;
            report.distantTriangleCount = triangles;
            report.largestDistantRendererVertexCount =
                largestRendererVertices;
            report.largestDistantBatchRendererCount = largestBatchRenderers;
            report.largestDistantBatchInstanceCount = largestBatchInstances;
            return new DistantBuildPlan { Batches = batches.ToArray() };
        }

        internal static float MinimumScaledDistantCrownRadius(
            float targetHeight)
        {
            if (!float.IsFinite(targetHeight) || targetHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(targetHeight));
            return new[] { "Spruce", "Pine", "Birch", "Aspen" }
                .Select(DistantTemplate.Load)
                .Min(template => template.CrownRadius *
                                 (targetHeight / template.Height));
        }

        private static void ApplyBudgetValidation(
            MapVegetationContext context,
            GlobalPresentationReport report)
        {
            report.distantTreeBudget =
                context.Options.DistantForestMaximumTrees;
            report.distantRendererBudget =
                context.Options.DistantForestMaximumRenderers;
            report.distantVertexBudget =
                context.Options.DistantForestMaximumVertices;
            report.distantRendererVertexBudget =
                context.Options.DistantForestMaximumVerticesPerBatch;
            report.canonicalRockBudget =
                context.Options.CanonicalRockMaximumCount;
            if (report.distantTreeCount > report.distantTreeBudget)
                AddError(report, "Distant tree hard cap was exceeded.");
            if (report.distantRendererCount > report.distantRendererBudget)
                AddError(report, "Distant renderer hard budget was exceeded.");
            if (report.distantVertexCount > report.distantVertexBudget)
                AddError(report, "Distant total vertex hard budget was exceeded.");
            if (report.largestDistantRendererVertexCount >
                report.distantRendererVertexBudget)
                AddError(report,
                    "A distant renderer exceeded its per-batch vertex hard budget.");
            if (report.canonicalRockCount > report.canonicalRockBudget)
                AddError(report, "Canonical rock hard budget was exceeded.");
            if (report.rockAccentPlacementCount != 0)
                AddError(report,
                    "Random RockAccent/forest-rock placements are forbidden; only canonical source anchors may be replaced.");
        }

        private static void AddError(GlobalPresentationReport report,
            string error)
        {
            if (!report.errors.Contains(error)) report.errors.Add(error);
        }

        private static void WriteCanonicalRocks(
            MapVegetationContext context,
            Transform parent,
            GlobalPresentationReport report)
        {
            Matrix4x4 mapping = context.Options.Placement
                .DonorCoordinateMatrix;
            foreach (MapVegetationRockAnchor rock in context.Source.Rocks
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                CanonicalRockPlacement placement = MapCanonicalRockAnchor(
                    rock, mapping);
                GameObject instance = MapVegetationForestFloorBindings
                    .InstantiateCanonicalRock(rock.Id,
                        context.Options.Placement.Seed,
                        parent, placement.Position, placement.SourceSize,
                        placement.Yaw);
                GameObjectUtility.SetStaticEditorFlags(instance,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic);
            }
        }

        private static void ValidateCanonicalRockAnchors(
            MapVegetationContext context, Transform rockRoot,
            GlobalPresentationReport report)
        {
            const float maximumAllowedDeviationMeters = 0.03f;
            Dictionary<string, MapVegetationRockAnchor> anchors = context
                .Source.Rocks.ToDictionary(rock => rock.Id,
                    StringComparer.Ordinal);
            Transform[] children = Enumerable.Range(0, rockRoot.childCount)
                .Select(rockRoot.GetChild).ToArray();
            Matrix4x4 mapping = context.Options.Placement
                .DonorCoordinateMatrix;
            report.canonicalRockUnexpectedAnchorCount = children.Count(child =>
                !anchors.ContainsKey(child.name));
            foreach (IGrouping<string, Transform> duplicate in children
                         .GroupBy(child => child.name,
                             StringComparer.Ordinal))
                report.canonicalRockDuplicateAnchorCount += Mathf.Max(0,
                    duplicate.Count() - 1);

            foreach (MapVegetationRockAnchor anchor in anchors.Values
                         .OrderBy(rock => rock.Id,
                             StringComparer.Ordinal))
            {
                Transform[] matches = children.Where(child =>
                        string.Equals(child.name, anchor.Id,
                            StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length == 0)
                {
                    report.canonicalRockMissingAnchorCount++;
                    continue;
                }
                if (!TryRendererBounds(matches[0].gameObject,
                        out Bounds bounds))
                {
                    report.canonicalRockMissingRendererCount++;
                    continue;
                }
                Vector3 renderedBottomCentre = new Vector3(bounds.center.x,
                    bounds.min.y, bounds.center.z);
                CanonicalRockPlacement expected = MapCanonicalRockAnchor(
                    anchor, mapping);
                Vector3 expectedWorldPosition = expected.Position;
                float deviation = Vector3.Distance(renderedBottomCentre,
                    expectedWorldPosition);
                report.maximumCanonicalRockAnchorDeviationMeters = Mathf.Max(
                    report.maximumCanonicalRockAnchorDeviationMeters,
                    deviation);
                CanonicalRockReplacementAnchor metadata = matches[0]
                    .GetComponent<CanonicalRockReplacementAnchor>();
                if (metadata == null)
                {
                    report.canonicalRockMissingMetadataCount++;
                    continue;
                }
                if (!string.Equals(metadata.StableId, anchor.Id,
                        StringComparison.Ordinal))
                    report.canonicalRockMetadataMismatchCount++;
                report.maximumCanonicalRockMappedSizeDeviationMeters =
                    Mathf.Max(
                        report.maximumCanonicalRockMappedSizeDeviationMeters,
                        Vector3.Distance(metadata.MappedSourceSize,
                            expected.SourceSize));
                report.maximumCanonicalRockMetadataAnchorDeviationMeters =
                    Mathf.Max(report
                            .maximumCanonicalRockMetadataAnchorDeviationMeters,
                        Vector3.Distance(metadata.WorldBottomCenter,
                            expected.Position));
                Vector3 expectedDirection = Quaternion.Euler(0f,
                    expected.Yaw, 0f) * Vector3.forward;
                Vector3 metadataDirection = Quaternion.Euler(0f,
                    metadata.PrincipalYawDegrees, 0f) * Vector3.forward;
                Vector3 savedDirection = matches[0].forward;
                savedDirection.y = 0f;
                report.maximumCanonicalRockPrincipalDirectionDeviationDegrees =
                    Mathf.Max(report
                            .maximumCanonicalRockPrincipalDirectionDeviationDegrees,
                        Vector3.Angle(expectedDirection,
                            metadataDirection),
                        savedDirection.sqrMagnitude <= 0.000001f
                            ? 180f
                            : Vector3.Angle(expectedDirection,
                                savedDirection.normalized));
                Vector3 savedScale = matches[0].localScale;
                report.maximumCanonicalRockUniformScaleDeviation = Mathf.Max(
                    report.maximumCanonicalRockUniformScaleDeviation,
                    Mathf.Abs(savedScale.x - metadata.AuthoredUniformScale),
                    Mathf.Abs(savedScale.y - metadata.AuthoredUniformScale),
                    Mathf.Abs(savedScale.z - metadata.AuthoredUniformScale));
            }
            if (report.canonicalRockMissingAnchorCount != 0 ||
                report.canonicalRockDuplicateAnchorCount != 0 ||
                report.canonicalRockUnexpectedAnchorCount != 0 ||
                report.canonicalRockMissingRendererCount != 0 ||
                report.canonicalRockMissingMetadataCount != 0 ||
                report.canonicalRockMetadataMismatchCount != 0)
                report.errors.Add(
                    "Canonical rock child IDs/renderers/metadata do not map one-to-one to source anchors.");
            if (report.maximumCanonicalRockAnchorDeviationMeters >
                maximumAllowedDeviationMeters)
                report.errors.Add(
                    "Canonical rock rendered bottom-centre drifted from its measured source anchor.");
            if (report.maximumCanonicalRockMetadataAnchorDeviationMeters >
                    0.0001f ||
                report.maximumCanonicalRockMappedSizeDeviationMeters >
                    0.0001f ||
                report.maximumCanonicalRockPrincipalDirectionDeviationDegrees >
                    0.01f ||
                report.maximumCanonicalRockUniformScaleDeviation > 0.0001f)
                report.errors.Add(
                    "Canonical rock saved mapped anchor/size/principal direction/scale differs from the deterministic source policy.");
        }

        internal static bool TryRendererBounds(GameObject root,
            out Bounds bounds)
        {
            Renderer[] renderers = root == null
                ? Array.Empty<Renderer>()
                : root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        internal static CanonicalRockPlacement MapCanonicalRockAnchor(
            MapVegetationRockAnchor anchor,
            Matrix4x4 donorToWorld)
        {
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            Vector3 position = donorToWorld.MultiplyPoint3x4(anchor.Position);
            Vector3 sourceSize = anchor.SourceSize;
            Vector3 mappedSize = new Vector3(
                Mathf.Abs(donorToWorld.m00) * sourceSize.x +
                Mathf.Abs(donorToWorld.m01) * sourceSize.y +
                Mathf.Abs(donorToWorld.m02) * sourceSize.z,
                Mathf.Abs(donorToWorld.m10) * sourceSize.x +
                Mathf.Abs(donorToWorld.m11) * sourceSize.y +
                Mathf.Abs(donorToWorld.m12) * sourceSize.z,
                Mathf.Abs(donorToWorld.m20) * sourceSize.x +
                Mathf.Abs(donorToWorld.m21) * sourceSize.y +
                Mathf.Abs(donorToWorld.m22) * sourceSize.z);
            Vector3 sourceForward = Quaternion.Euler(0f, anchor.Yaw, 0f) *
                                    Vector3.forward;
            Vector3 mappedForward = donorToWorld.MultiplyVector(sourceForward);
            mappedForward.y = 0f;
            if (!Finite(position) || !Finite(mappedSize) ||
                mappedSize.x <= 0f || mappedSize.y <= 0f ||
                mappedSize.z <= 0f || !Finite(mappedForward) ||
                mappedForward.sqrMagnitude <= 0.000001f)
            {
                throw new InvalidDataException(
                    "Canonical rock coordinate mapping is non-finite or " +
                    "collapses its horizontal principal axis: " + anchor.Id);
            }

            float yaw = Mathf.Repeat(Mathf.Atan2(mappedForward.x,
                mappedForward.z) * Mathf.Rad2Deg, 360f);
            return new CanonicalRockPlacement(position, mappedSize, yaw);
        }

        internal readonly struct CanonicalRockPlacement
        {
            public readonly Vector3 Position;
            public readonly Vector3 SourceSize;
            public readonly float Yaw;

            public CanonicalRockPlacement(Vector3 position,
                Vector3 sourceSize, float yaw)
            {
                Position = position;
                SourceSize = sourceSize;
                Yaw = yaw;
            }
        }

        private static bool Finite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static void RegisterGlobalScene(
            ProductionWorldStreamingManifest manifest)
        {
            if (manifest == null || !manifest.PrivateLocalRuntimeBaseline)
                throw new InvalidOperationException(
                    "Global temporary vegetation requires the private local runtime manifest.");
            var buildScenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            if (!buildScenes.Any(scene => string.Equals(scene.path,
                    ScenePath, StringComparison.Ordinal)))
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else
            {
                for (int index = 0; index < buildScenes.Count; index++)
                    if (string.Equals(buildScenes[index].path, ScenePath,
                            StringComparison.Ordinal) && !buildScenes[index].enabled)
                        throw new InvalidOperationException(
                            "Global vegetation scene already exists as a disabled build entry. Refusing to re-enable it in place because that would shift every following stable enabled build index; remove the stale disabled entry and regenerate.");
            }
            int buildIndex = EnabledBuildIndex(buildScenes, ScenePath);
            if (buildIndex < 0)
                throw new InvalidOperationException(
                    "Global vegetation scene has no enabled build index.");

            var globals = manifest.GlobalScenes
                .Where(scene => !string.Equals(scene.SceneId, SceneId,
                    StringComparison.Ordinal))
                .ToList();
            if (globals.Any(scene => string.Equals(scene.ScenePath, ScenePath,
                    StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Global vegetation scene path is owned by another stable scene ID.");
            globals.Add(new ProductionWorldGlobalScene(SceneId, buildIndex,
                ScenePath));
            globals.Sort((left, right) => string.Compare(left.SceneId,
                right.SceneId, StringComparison.Ordinal));

            ProductionWorldStreamingManifest candidate =
                Object.Instantiate(manifest);
            try
            {
                candidate.ConfigureGlobalScenesForAuthoring(globals.ToArray());
                IReadOnlyList<string> errors = candidate.ValidateConfiguration();
                if (errors.Count != 0)
                    throw new InvalidOperationException(
                        "Global vegetation registration is invalid: " +
                        string.Join(" | ", errors));
            }
            finally
            {
                Object.DestroyImmediate(candidate);
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            manifest.ConfigureGlobalScenesForAuthoring(globals.ToArray());
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }

        private static int EnabledBuildIndex(
            IReadOnlyList<EditorBuildSettingsScene> scenes,
            string path)
        {
            int enabled = 0;
            for (int index = 0; index < scenes.Count; index++)
            {
                if (!scenes[index].enabled) continue;
                if (string.Equals(scenes[index].path, path,
                        StringComparison.Ordinal)) return enabled;
                enabled++;
            }
            return -1;
        }

        private static string Fingerprint(
            MapVegetationContext context,
            GlobalPresentationReport report)
        {
            using SHA256 sha = SHA256.Create();
            var text = new StringBuilder(4096)
                .Append(GeneratorId).Append('\n')
                .Append(report.settingsHash).Append('\n')
                .Append(report.sourceFingerprint).Append('\n');
            foreach (MapVegetationRockAnchor rock in context.Source.Rocks)
                text.Append(rock.Id).Append('|').Append(rock.ReplacementKey)
                    .Append('|').Append(Vector(rock.Position)).Append('|')
                    .Append(Vector(rock.SourceSize)).Append('|')
                    .Append(rock.Yaw.ToString("R", CultureInfo.InvariantCulture))
                    .Append('\n');
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
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup owner =
                    root.GetComponent<GeneratedVegetationGroup>();
                if (owner == null || owner.GeneratorId != GeneratorId)
                    throw new InvalidOperationException(
                        "Refusing to overwrite a global scene with non-owned content: " +
                        root.name);
            }
        }

        private static void Backup(string path, string runId)
        {
            if (!File.Exists(path)) return;
            string relative = path.Substring(
                MapVegetationRebuildOptions.GeneratedRoot.Length).TrimStart('/');
            string destination =
                "Artifacts/VegetationRebuild/Backups/Global/" + runId + "/" +
                relative;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, false);
            if (File.Exists(path + ".meta"))
                File.Copy(path + ".meta", destination + ".meta", false);
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitize(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? string.Empty).Select(character =>
                invalid.Contains(character) || character == '|'
                    ? '_' : character).ToArray());
        }

        private sealed class DistantBuildPlan
        {
            public DistantBatchPlan[] Batches = Array.Empty<DistantBatchPlan>();
        }

        private sealed class DistantBatchPlan
        {
            public string CellId;
            public string Species;
            public string SafeName;
            public Vector3 Origin;
            public MapVegetationPlacement[] Placements;
            public DistantTemplate Template;
        }

        internal sealed class DistantPart
        {
            public string Name;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uvs;
            public Vector4[] Uv1;
            public Vector4[] Uv2;
            public Color[] Colors;
            public int[] Indices;
            public Material Material;
        }

        internal sealed class DistantTemplate
        {
            public readonly List<DistantPart> Parts =
                new List<DistantPart>();
            public float Height;
            public float CrownRadius;
            public string SourcePrefabPath;
            public float NormalizedCrownRadius => CrownRadius /
                Mathf.Max(Height, 0.01f);
            public int TotalVertexCount => Parts.Sum(part =>
                part?.Vertices?.Length ?? 0);
            public bool FitsBackdropCost =>
                Parts.Count == DistantTemplateMaximumParts &&
                TotalVertexCount > 0 &&
                TotalVertexCount <= DistantTemplateMaximumVertices;

            public static DistantTemplate Load(string species)
            {
                // The backdrop owns one cheap template per species. Broad
                // vendor far LODs can still contain hundreds of vertices and
                // several material parts, which multiplies into millions of
                // vertices and hundreds of extra renderers at 16k instances.
                // First enforce the reviewed one-part/64-vertex proxy budget;
                // only then select the broadest approved crown in that cheap
                // pool. This preserves skyline closure without buying it with
                // an unbounded mesh.
                DistantTemplate[] candidates = MapVegetationTreePresentation
                    .LoadSpeciesPrefabs(species)
                    .OrderBy(prefab => AssetDatabase.GetAssetPath(prefab),
                        StringComparer.Ordinal)
                    .Select(LoadSingle)
                    .ToArray();
                DistantTemplate[] eligible = candidates
                    .Where(template => template.FitsBackdropCost)
                    .ToArray();
                if (eligible.Length == 0)
                    throw new InvalidDataException(
                        "Approved distant " + species +
                        " sources contain no one-part far LOD at or below " +
                        DistantTemplateMaximumVertices + " vertices. " +
                        "Candidate costs: " + string.Join(", ", candidates
                            .Select(template => template.SourcePrefabPath +
                                " parts=" + template.Parts.Count +
                                " vertices=" + template.TotalVertexCount)));
                return eligible
                    .OrderByDescending(template =>
                        template.NormalizedCrownRadius)
                    .ThenBy(template => template.TotalVertexCount)
                    .ThenBy(template => template.SourcePrefabPath,
                        StringComparer.Ordinal)
                    .First();
            }

            internal static DistantTemplate LoadSingleForTests(
                GameObject sourcePrefab) => LoadSingle(sourcePrefab);

            private static DistantTemplate LoadSingle(
                GameObject sourcePrefab)
            {
                GameObject prefab = Object.Instantiate(sourcePrefab);
                prefab.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    prefab.transform.SetPositionAndRotation(Vector3.zero,
                        sourcePrefab.transform.localRotation);
                    prefab.transform.localScale = Vector3.one;
                    // Read the exact far renderer set only after the same
                    // project-owned material and LOD policies used by near
                    // trees. Reading the vendor prefab directly would reinsert
                    // its metallic/bleached materials into the backdrop.
                    MapVegetationMaterialBindings.Apply(prefab);
                    MapVegetationTreePresentation.Apply(prefab, sourcePrefab);
                LODGroup group = prefab.GetComponentInChildren<LODGroup>(true);
                if (group == null) throw new InvalidDataException(
                    "Distant tree source has no LODGroup: " + prefab.name);
                Renderer[] farRenderers = group.GetLODs().Last().renderers ??
                    Array.Empty<Renderer>();
                MeshRenderer[] meshRenderers = farRenderers
                    .Where(renderer => renderer != null)
                    .OfType<MeshRenderer>()
                    .Distinct()
                    .OrderBy(renderer => HierarchyPath(renderer.transform),
                        StringComparer.Ordinal)
                    .ToArray();
                if (meshRenderers.Length != farRenderers.Count(renderer =>
                        renderer != null))
                    throw new InvalidDataException(
                        "Distant tree far LOD contains an unsupported non-mesh renderer: " +
                        prefab.name);
                if (meshRenderers.Length == 0)
                    throw new InvalidDataException(
                        "Distant tree source has no far-LOD mesh renderers: " +
                        prefab.name);

                var template = new DistantTemplate
                {
                    SourcePrefabPath = AssetDatabase.GetAssetPath(
                        sourcePrefab)
                };
                Bounds combined = default;
                bool hasBounds = false;
                for (int rendererIndex = 0;
                     rendererIndex < meshRenderers.Length;
                     rendererIndex++)
                {
                    MeshRenderer renderer = meshRenderers[rendererIndex];
                    Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null)
                        throw new InvalidDataException(
                            "Distant far-LOD renderer has no mesh: " +
                            prefab.name + "/" + renderer.name);
                    Material[] materials = renderer.sharedMaterials;
                    if (materials.Length < mesh.subMeshCount)
                        throw new InvalidDataException(
                            "Distant far-LOD renderer has fewer materials than submeshes: " +
                            prefab.name + "/" + renderer.name);

                    using Mesh.MeshDataArray data =
                        MeshUtility.AcquireReadOnlyMeshData(mesh);
                    Mesh.MeshData source = data[0];
                    using var sourceVertices = new NativeArray<Vector3>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceNormals = new NativeArray<Vector3>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceUvs = new NativeArray<Vector2>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceTangents = new NativeArray<Vector4>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceUv1 = new NativeArray<Vector4>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceUv2 = new NativeArray<Vector4>(
                        source.vertexCount, Allocator.Temp);
                    using var sourceColors = new NativeArray<Color>(
                        source.vertexCount, Allocator.Temp);
                    source.GetVertices(sourceVertices);
                    bool hasNormals = source.HasVertexAttribute(
                        VertexAttribute.Normal);
                    bool hasTangents = source.HasVertexAttribute(
                        VertexAttribute.Tangent);
                    bool hasUvs = source.HasVertexAttribute(
                        VertexAttribute.TexCoord0);
                    bool hasUv1 = source.HasVertexAttribute(
                        VertexAttribute.TexCoord1);
                    bool hasUv2 = source.HasVertexAttribute(
                        VertexAttribute.TexCoord2);
                    bool hasColors = source.HasVertexAttribute(
                        VertexAttribute.Color);
                    if (hasNormals) source.GetNormals(sourceNormals);
                    if (hasTangents) source.GetTangents(sourceTangents);
                    if (hasUvs) source.GetUVs(0, sourceUvs);
                    if (hasUv1) source.GetUVs(1, sourceUv1);
                    if (hasUv2) source.GetUVs(2, sourceUv2);
                    if (hasColors) source.GetColors(sourceColors);
                    Matrix4x4 toRoot = Matrix4x4.Rotate(
                        prefab.transform.localRotation) * RelativeMatrix(
                             renderer.transform, prefab.transform);
                    Matrix4x4 normalMatrix = toRoot.inverse.transpose;
                    bool mirrored = toRoot.determinant < 0f;

                    for (int subMeshIndex = 0;
                         subMeshIndex < source.subMeshCount;
                         subMeshIndex++)
                    {
                        SubMeshDescriptor subMesh = source.GetSubMesh(
                            subMeshIndex);
                        if (subMesh.topology != MeshTopology.Triangles ||
                            subMesh.indexCount < 3 ||
                            subMesh.indexCount % 3 != 0)
                            throw new InvalidDataException(
                                "Distant far-LOD submesh is not triangles: " +
                                mesh.name + "/" + subMeshIndex);
                        if (materials[subMeshIndex] == null)
                            throw new InvalidDataException(
                                "Distant far-LOD submesh material is missing: " +
                                mesh.name + "/" + subMeshIndex);
                        using var sourceIndices = new NativeArray<int>(
                            subMesh.indexCount, Allocator.Temp);
                        source.GetIndices(sourceIndices, subMeshIndex, true);
                        int[] compactIndices = new int[sourceIndices.Length];
                        var remap = new Dictionary<int, int>();
                        var used = new List<int>();
                        for (int index = 0; index < sourceIndices.Length;
                             index++)
                        {
                            int sourceIndex = sourceIndices[index];
                            if (sourceIndex < 0 ||
                                sourceIndex >= source.vertexCount)
                                throw new InvalidDataException(
                                    "Distant far-LOD index escaped its vertex buffer: " +
                                    mesh.name);
                            if (!remap.TryGetValue(sourceIndex,
                                    out int compactIndex))
                            {
                                compactIndex = used.Count;
                                remap.Add(sourceIndex, compactIndex);
                                used.Add(sourceIndex);
                            }
                            compactIndices[index] = compactIndex;
                        }

                        var vertices = new Vector3[used.Count];
                        var normals = new Vector3[used.Count];
                        var tangents = new Vector4[used.Count];
                        var uvs = new Vector2[used.Count];
                        var uv1 = new Vector4[used.Count];
                        var uv2 = new Vector4[used.Count];
                        var colors = new Color[used.Count];
                        for (int index = 0; index < used.Count; index++)
                        {
                            int sourceIndex = used[index];
                            vertices[index] = toRoot.MultiplyPoint3x4(
                                sourceVertices[sourceIndex]);
                            Vector3 sourceNormal = hasNormals
                                ? sourceNormals[sourceIndex] : Vector3.up;
                            normals[index] = normalMatrix.MultiplyVector(
                                sourceNormal).normalized;
                            Vector4 sourceTangent = hasTangents
                                ? sourceTangents[sourceIndex]
                                : FallbackTangent(sourceNormal);
                            Vector3 tangent = toRoot.MultiplyVector(
                                new Vector3(sourceTangent.x, sourceTangent.y,
                                    sourceTangent.z)).normalized;
                            if (!FiniteNonZero(tangent))
                            {
                                Vector4 fallback = FallbackTangent(
                                    normals[index]);
                                tangent = new Vector3(fallback.x, fallback.y,
                                    fallback.z);
                            }
                            tangents[index] = new Vector4(tangent.x,
                                tangent.y, tangent.z,
                                (mirrored ? -1f : 1f) *
                                (sourceTangent.w < 0f ? -1f : 1f));
                            uvs[index] = hasUvs ? sourceUvs[sourceIndex]
                                : Vector2.zero;
                            uv1[index] = hasUv1 ? sourceUv1[sourceIndex]
                                : Vector4.zero;
                            uv2[index] = hasUv2 ? sourceUv2[sourceIndex]
                                : Vector4.zero;
                            colors[index] = hasColors
                                ? sourceColors[sourceIndex] : Color.white;
                            if (!hasBounds)
                            {
                                combined = new Bounds(vertices[index],
                                    Vector3.zero);
                                hasBounds = true;
                            }
                                else combined.Encapsulate(vertices[index]);
                        }
                        if (mirrored)
                            for (int index = 0;
                                 index < compactIndices.Length; index += 3)
                            {
                                int swap = compactIndices[index + 1];
                                compactIndices[index + 1] =
                                    compactIndices[index + 2];
                                compactIndices[index + 2] = swap;
                            }
                        template.Parts.Add(new DistantPart
                        {
                            Name = rendererIndex.ToString("D2",
                                CultureInfo.InvariantCulture) + "_" +
                                renderer.name + "_sm" + subMeshIndex,
                            Vertices = vertices,
                            Normals = normals,
                            Tangents = tangents,
                            Uvs = uvs,
                            Uv1 = uv1,
                            Uv2 = uv2,
                            Colors = colors,
                            Indices = compactIndices,
                            Material = materials[subMeshIndex]
                        });
                    }
                }
                if (!hasBounds || template.Parts.Count == 0)
                    throw new InvalidDataException(
                        "Distant tree source yielded no renderable far-LOD parts: " +
                        prefab.name);
                template.Height = Mathf.Max(0.01f, combined.size.y);
                template.CrownRadius = Mathf.Max(0.01f,
                    Mathf.Max(combined.extents.x, combined.extents.z));
                return template;
                }
                finally
                {
                    Object.DestroyImmediate(prefab);
                }
            }

            private static bool FiniteNonZero(Vector3 value) =>
                float.IsFinite(value.x) && float.IsFinite(value.y) &&
                float.IsFinite(value.z) && value.sqrMagnitude > 0.000001f;

            private static Vector4 FallbackTangent(Vector3 normal)
            {
                Vector3 normalized = FiniteNonZero(normal)
                    ? normal.normalized : Vector3.up;
                Vector3 axis = Mathf.Abs(normalized.y) < 0.99f
                    ? Vector3.up : Vector3.right;
                Vector3 tangent = Vector3.Cross(axis, normalized).normalized;
                return new Vector4(tangent.x, tangent.y, tangent.z, 1f);
            }

            private static string HierarchyPath(Transform transform)
            {
                var parts = new Stack<string>();
                while (transform != null)
                {
                    parts.Push(transform.name);
                    transform = transform.parent;
                }
                return string.Join("/", parts);
            }

            private static Matrix4x4 RelativeMatrix(
                Transform child,
                Transform ancestor)
            {
                Matrix4x4 matrix = Matrix4x4.identity;
                while (child != ancestor)
                {
                    if (child == null) throw new InvalidDataException(
                        "Distant billboard escaped its tree prefab hierarchy.");
                    matrix = Matrix4x4.TRS(child.localPosition,
                        child.localRotation, child.localScale) * matrix;
                    child = child.parent;
                }
                return matrix;
            }
        }

        [Serializable]
        public sealed class GlobalPresentationReport
        {
            public string generator = GeneratorId;
            public string runId;
            public string settingsHash;
            public string sourceFingerprint;
            public string fingerprint;
            public bool passed;
            public int distantTreeCount;
            public int distantTreeBudget;
            public int distantBatchGroupCount;
            public int distantRendererCount;
            public int distantRendererBudget;
            public int distantDrawCallBudget;
            public long distantVertexCount;
            public int distantVertexBudget;
            public long distantTriangleCount;
            public int distantColliderCount;
            public int distantRendererVertexBudget;
            public int largestDistantRendererVertexCount;
            public int largestDistantBatchRendererCount;
            public int largestDistantBatchInstanceCount;
            public int distantRootChildCount;
            public Vector3 largestDistantRendererBounds;
            public int canonicalRockCount;
            public int canonicalRockBudget;
            public int canonicalRockSceneCount;
            public int canonicalRockColliderCount;
            public int canonicalRockRendererCount;
            public int canonicalRockRootChildCount;
            public int canonicalRockMissingAnchorCount;
            public int canonicalRockDuplicateAnchorCount;
            public int canonicalRockUnexpectedAnchorCount;
            public int canonicalRockMissingRendererCount;
            public int canonicalRockMissingMetadataCount;
            public int canonicalRockMetadataMismatchCount;
            public int rockAccentPlacementCount;
            public float maximumCanonicalRockAnchorDeviationMeters;
            public float maximumCanonicalRockMetadataAnchorDeviationMeters;
            public float maximumCanonicalRockMappedSizeDeviationMeters;
            public float maximumCanonicalRockPrincipalDirectionDeviationDegrees;
            public float maximumCanonicalRockUniformScaleDeviation;
            public int largestGeneratedRootChildCount;
            public int globalDistantBatchCount;
            public string[] legacyReplacementKeys = Array.Empty<string>();
            public List<string> errors = new List<string>();
            public string distantPolicy =
                "No distant forest is allowed in this always-loaded scene; it is owned by the vegetation-backdrop streamed cell layer.";
            public string rockPolicy =
                "Renderer-only ALP variants replace canonical MAP/MESH/ROCKS components at measured bottom-centre/principal-yaw/component-size anchors; zero new colliders, exact legacy MeshCollider retained, RockPale/Rockwall untouched, no free scatter.";
        }
    }
}
