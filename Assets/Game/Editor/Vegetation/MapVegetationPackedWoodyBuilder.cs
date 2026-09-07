using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.World.Vegetation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Replaces thousands of streamed prefab hierarchies with deterministic,
    /// category-local matrix assets and shared proxy-mesh prototypes.
    /// </summary>
    public static class MapVegetationPackedWoodyBuilder
    {
        public const string PresentationVersion = "msc.map-packed-woody.v1";
        public const float BatchTileSizeMeters = 32f;
        public const int SerializedSceneGameObjectBudget = 8;
        public const int SerializedSceneComponentBudget = 32;
        public const long SerializedSceneByteBudget = 1024 * 1024;
        public const long PackedAssetBaseByteBudget =
            PackedWoodyCellAsset.SerializedAssetBaseByteBudget;
        public const long PackedAssetBytesPerPlacementBudget =
            PackedWoodyCellAsset.SerializedAssetBytesPerPlacementBudget;

        public static long PackedAssetByteBudget(int placementMetadataCount)
        {
            return PackedWoodyCellAsset.SerializedAssetByteBudget(
                placementMetadataCount);
        }

        public static bool PackedAssetBytesFitBudget(
            long serializedBytes,
            int placementMetadataCount)
        {
            return PackedWoodyCellAsset.SerializedAssetBytesFitBudget(
                serializedBytes, placementMetadataCount);
        }

        private const string PrototypeRoot =
            MapVegetationRebuildOptions.GeneratedRoot + "/PackedWoody/Prototypes";
        private static readonly Dictionary<GameObject, PackedWoodyPrototypeAsset>
            PrototypeCache =
                new Dictionary<GameObject, PackedWoodyPrototypeAsset>();
        private static readonly Dictionary<GameObject, float> HeightCache =
            new Dictionary<GameObject, float>();
        private static string policySignatureCache;

        public static string CellAssetPath(
            string cellId,
            MapVegetationCategories category) =>
            MapVegetationRebuildOptions.GeneratedRoot + "/Data/" + cellId +
            "/Packed" + category + ".asset";

        public static PackedWoodyCellAsset WriteCategory(
            MapVegetationContext context,
            MapVegetationCellPlan plan,
            MapVegetationCategories category,
            GameObject root)
        {
            if (context == null || plan == null || root == null)
                throw new ArgumentNullException();
            PackedWoodyCategory packedCategory = ToPackedCategory(category);
            MapVegetationPlacement[] selected = plan.Woody
                .Where(placement => placement.category == category)
                .OrderBy(placement => placement.id, StringComparer.Ordinal)
                .ToArray();

            var resolved = new List<ResolvedPlacement>(selected.Length);
            var prototypes = new List<PackedWoodyPrototypeAsset>();
            var prototypeIndices =
                new Dictionary<PackedWoodyPrototypeAsset, int>();
            foreach (MapVegetationPlacement placement in selected)
            {
                ResolvedPlacement item = Resolve(context, placement, true);
                if (!prototypeIndices.TryGetValue(item.Prototype, out int index))
                {
                    index = prototypes.Count;
                    prototypes.Add(item.Prototype);
                    prototypeIndices.Add(item.Prototype, index);
                }
                item.PrototypeIndex = index;
                resolved.Add(item);
            }

            var groups = new SortedDictionary<BatchKey, List<ResolvedPlacement>>();
            foreach (ResolvedPlacement item in resolved)
            {
                Vector3 position = item.Placement.position;
                var key = new BatchKey(
                    item.PrototypeIndex,
                    Mathf.FloorToInt(position.x / BatchTileSizeMeters),
                    Mathf.FloorToInt(position.z / BatchTileSizeMeters));
                if (!groups.TryGetValue(key, out List<ResolvedPlacement> values))
                    groups.Add(key, values = new List<ResolvedPlacement>());
                values.Add(item);
            }

            var batches = new List<PackedWoodyBatch>();
            var records = new List<PackedWoodyPlacementRecord>(resolved.Count);
            Bounds worldBounds = CellBounds(context, plan);
            bool hasRenderedBounds = false;
            foreach (KeyValuePair<BatchKey, List<ResolvedPlacement>> entry in groups)
            {
                List<ResolvedPlacement> values = entry.Value;
                values.Sort((left, right) => string.Compare(
                    left.Placement.id, right.Placement.id,
                    StringComparison.Ordinal));
                for (int offset = 0; offset < values.Count;
                     offset += PackedWoodyBatch.MaximumInstanceCount)
                {
                    int count = Mathf.Min(
                        PackedWoodyBatch.MaximumInstanceCount,
                        values.Count - offset);
                    var matrices = new Matrix4x4[count];
                    Bounds batchBounds = default;
                    float maximumHeight = 0f;
                    int batchIndex = batches.Count;
                    for (int matrixIndex = 0;
                         matrixIndex < count; matrixIndex++)
                    {
                        ResolvedPlacement item = values[offset + matrixIndex];
                        matrices[matrixIndex] = item.Matrix;
                        Bounds transformed = TransformBounds(
                            item.Prototype.LocalBounds, item.Matrix);
                        if (matrixIndex == 0) batchBounds = transformed;
                        else batchBounds.Encapsulate(transformed);
                        maximumHeight = Mathf.Max(
                            maximumHeight, item.HeightMeters);
                        records.Add(new PackedWoodyPlacementRecord(
                            Hash128.Compute(item.Placement.id),
                            item.PrototypeIndex,
                            batchIndex,
                            matrixIndex,
                            packedCategory,
                            item.Method,
                            item.Species,
                            item.Placement.position,
                            item.HeightMeters));
                    }
                    batchBounds.Expand(0.05f);
                    if (!hasRenderedBounds)
                    {
                        worldBounds = batchBounds;
                        hasRenderedBounds = true;
                    }
                    else worldBounds.Encapsulate(batchBounds);
                    batches.Add(new PackedWoodyBatch(
                        entry.Key.PrototypeIndex,
                        batchBounds,
                        maximumHeight,
                        matrices));
                }
            }

            BuildCollisionIndex(
                context,
                packedCategory,
                resolved,
                out PackedWoodyCollisionTile[] collisionTiles,
                out PackedWoodyCollisionRecord[] collisionRecords);

            string assetPath = CellAssetPath(plan.Cell.Id, category);
            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            PackedWoodyCellAsset asset =
                AssetDatabase.LoadAssetAtPath<PackedWoodyCellAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
                asset.name = plan.Cell.Id + " Packed " + category;
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.ConfigureForAuthoring(
                MapVegetationRebuildOptions.GeneratorId,
                PresentationVersion,
                plan.Cell.Id,
                plan.Fingerprint,
                packedCategory,
                worldBounds,
                prototypes.ToArray(),
                batches.ToArray(),
                records.ToArray(),
                collisionTiles,
                collisionRecords);
            EditorUtility.SetDirty(asset);
            IReadOnlyList<string> errors = asset.ValidateConfiguration();
            if (errors.Count > 0)
                throw new InvalidDataException(
                    "Packed woody cell is invalid: " +
                    string.Join("; ", errors.Take(12)));

            var renderer = root.AddComponent<PackedWoodyCellRenderer>();
            renderer.ConfigureForAuthoring(asset);
            if (packedCategory == PackedWoodyCategory.OriginalTree &&
                context.Options.CreateTreeColliders &&
                collisionRecords.Length > 0)
            {
                var pool = root.AddComponent<PackedWoodyCollisionPool>();
                pool.ConfigureForAuthoring(asset);
            }
            return asset;
        }

        public static ExpectedPlacement ResolveForValidation(
            MapVegetationContext context,
            MapVegetationPlacement placement)
        {
            ResolvedPlacement resolved = Resolve(context, placement, false);
            return new ExpectedPlacement(
                Hash128.Compute(placement.id),
                resolved.Prototype.StableKey,
                resolved.Matrix,
                resolved.Method,
                resolved.Species,
                resolved.HeightMeters);
        }

        public static int EstimateMaximumCollisionCandidates(
            PackedWoodyCellAsset asset,
            float radiusMeters)
        {
            if (asset == null || asset.CollisionRecords.Count == 0) return 0;
            int maximum = 0;
            IReadOnlyList<PackedWoodyCollisionTile> tiles = asset.CollisionTiles;
            int tileRange = Mathf.CeilToInt(
                radiusMeters / BatchTileSizeMeters);
            for (int index = 0; index < tiles.Count; index++)
            {
                PackedWoodyCollisionTile center = tiles[index];
                int sum = 0;
                for (int otherIndex = 0; otherIndex < tiles.Count; otherIndex++)
                {
                    PackedWoodyCollisionTile other = tiles[otherIndex];
                    if (Mathf.Abs(other.TileX - center.TileX) <= tileRange &&
                        Mathf.Abs(other.TileZ - center.TileZ) <= tileRange)
                        sum += other.Count;
                }
                maximum = Mathf.Max(maximum, sum);
            }
            return maximum;
        }

        public static void ClearCaches()
        {
            PrototypeCache.Clear();
            HeightCache.Clear();
            policySignatureCache = null;
        }

        private static ResolvedPlacement Resolve(
            MapVegetationContext context,
            MapVegetationPlacement placement,
            bool rebuildPrototype)
        {
            bool shrub = placement.category ==
                MapVegetationCategories.ShrubsAndUndergrowth;
            string speciesText =
                (placement.species ?? string.Empty).ToLowerInvariant();
            if (shrub && speciesText.StartsWith(
                "forest-floor-", StringComparison.Ordinal))
            {
                MapVegetationForestFloorBindings.Pool pool =
                    speciesText == "forest-floor-rock"
                        ? MapVegetationForestFloorBindings.Pool.RocksAndBoulders
                        : speciesText == "forest-floor-shrub"
                            ? MapVegetationForestFloorBindings.Pool.Shrubs
                            : MapVegetationForestFloorBindings.Pool.Understory;
                GameObject floorPrefab = MapVegetationForestFloorBindings.SelectPrefab(
                    pool, placement.id, context.Options.Placement.Seed);
                PackedWoodySpecies packedSpecies = pool ==
                    MapVegetationForestFloorBindings.Pool.RocksAndBoulders
                        ? PackedWoodySpecies.RockAccent
                        : pool == MapVegetationForestFloorBindings.Pool.Shrubs
                            ? PackedWoodySpecies.Shrub
                            : PackedWoodySpecies.Understory;
                PackedWoodyPrototypeAsset prototype = GetPrototype(
                    floorPrefab, packedSpecies, rebuildPrototype, false);
                MapVegetationForestFloorBindings.PlacementPolicy policy =
                    MapVegetationForestFloorBindings.Policy(pool,
                        placement.id);
                int salt = pool ==
                    MapVegetationForestFloorBindings.Pool.RocksAndBoulders
                        ? 0x2F19
                        : pool == MapVegetationForestFloorBindings.Pool.Shrubs
                            ? 0x53D7 : 0x6A31;
                uint hash = MapVegetationPlanning.HashId(
                    placement.id, context.Options.Placement.Seed ^ salt);
                float variation = VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(hash ^ 0xA511E9B3u));
                float floorScale = Mathf.Lerp(
                    policy.UniformScaleRange.x,
                    policy.UniformScaleRange.y,
                    variation);
                float yaw = VegetationStableHash.ToUnitFloat(hash) * 360f;
                Vector3 normal = placement.normal.sqrMagnitude > 0.0001f
                    ? placement.normal.normalized : Vector3.up;
                Quaternion tilt = Quaternion.Slerp(
                    Quaternion.identity,
                    Quaternion.FromToRotation(Vector3.up, normal),
                    policy.NormalAlignment);
                Matrix4x4 matrix = Matrix4x4.TRS(
                    placement.position,
                    tilt * Quaternion.Euler(0f, yaw, 0f),
                    Vector3.one * floorScale);
                return new ResolvedPlacement(
                    placement,
                    prototype,
                    matrix,
                    prototype.SourceHeightMeters * floorScale,
                    PackedWoodyPlacementMethod.ForestFloor,
                    packedSpecies);
            }

            MapVegetationKind kind = shrub
                ? MapVegetationKind.Shrub : MapVegetationKind.Tree;
            MapVegetationCategorySettings rules =
                context.Options.Placement.Category(kind);
            GameObject prefab;
            PackedWoodySpecies species;
            if (!shrub)
            {
                prefab = MapVegetationTreePresentation.SelectPrefab(
                    placement.species,
                    placement.id,
                    context.Options.Placement.Seed,
                    Mathf.Clamp(placement.height, 1f, 35f));
                species = ParseTreeSpecies(placement.species);
            }
            else
            {
                GameObject[] available = rules.Prefabs
                    .Where(candidate => candidate != null).ToArray();
                if (available.Length == 0)
                    throw new InvalidOperationException(
                        "No available shrub prefabs.");
                string token = speciesText.Contains("pine") ? "pine"
                    : speciesText.Contains("birch") ? "birch"
                    : speciesText.Contains("aspen") ? "aspen"
                    : speciesText.Contains("spruce") ? "spruce"
                    : string.Empty;
                GameObject[] matched = string.IsNullOrEmpty(token)
                    ? available
                    : available.Where(candidate => candidate.name.IndexOf(
                        token, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (matched.Length == 0) matched = available;
                uint hash = MapVegetationPlanning.HashId(
                    placement.id, context.Options.Placement.Seed);
                prefab = matched[hash % (uint)matched.Length];
                species = PackedWoodySpecies.Shrub;
            }

            PackedWoodyPrototypeAsset selectedPrototype = GetPrototype(
                prefab, species, rebuildPrototype, !shrub);
            float targetHeight = shrub
                ? Mathf.Clamp(placement.height, 0.35f, 3f)
                : Mathf.Clamp(placement.height, 1f, 35f);
            float scale = targetHeight /
                Mathf.Max(0.01f, selectedPrototype.SourceHeightMeters) *
                Mathf.Lerp(
                    rules.ScaleRange.x,
                    rules.ScaleRange.y,
                    placement.variation);
            Quaternion tiltRotation = Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.FromToRotation(Vector3.up, placement.normal),
                rules.NormalAlignment);
            Quaternion rotation = tiltRotation *
                Quaternion.Euler(0f, placement.yaw, 0f) *
                prefab.transform.localRotation;
            return new ResolvedPlacement(
                placement,
                selectedPrototype,
                Matrix4x4.TRS(
                    placement.position, rotation, Vector3.one * scale),
                targetHeight * Mathf.Lerp(
                    rules.ScaleRange.x,
                    rules.ScaleRange.y,
                    placement.variation),
                shrub
                    ? PackedWoodyPlacementMethod.DonorShrub
                    : placement.method == "DeterministicGreenForestInfill"
                        ? PackedWoodyPlacementMethod.NaturalInfill
                        : placement.category ==
                          MapVegetationCategories.BoundaryForest
                            ? PackedWoodyPlacementMethod.BoundaryForest
                            : PackedWoodyPlacementMethod.DonorOriginal,
                species);
        }

        private static PackedWoodyPrototypeAsset GetPrototype(
            GameObject prefab,
            PackedWoodySpecies species,
            bool rebuild,
            bool tree)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (PrototypeCache.TryGetValue(
                prefab, out PackedWoodyPrototypeAsset cached) && cached != null)
                return cached;
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabPath) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    prefab, out string guid, out long localId))
                throw new InvalidDataException(
                    "Packed woody source must be a persistent prefab asset: " +
                    prefab.name);
            string dependency =
                AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            string policySignature = ProjectPolicySignature();
            string stableKey = guid + ":" + localId + ":" + species;
            string path = PrototypeRoot + "/" + guid + "_" + localId +
                "_" + species + ".asset";
            PackedWoodyPrototypeAsset prototype =
                AssetDatabase.LoadAssetAtPath<PackedWoodyPrototypeAsset>(path);
            if (!rebuild)
            {
                if (prototype == null ||
                    prototype.PresentationVersion != PresentationVersion ||
                    prototype.StableKey != stableKey ||
                    prototype.SourceDependencyHash != dependency ||
                    prototype.ProjectPolicySignature != policySignature)
                    throw new InvalidDataException(
                        "Packed woody prototype is stale or missing: " + path);
                PrototypeCache[prefab] = prototype;
                return prototype;
            }

            prototype = RebuildPrototypeTransactional(
                prefab,
                species,
                tree,
                path,
                guid,
                stableKey,
                dependency,
                policySignature,
                prototype);
            PrototypeCache[prefab] = prototype;
            return prototype;
        }

        /// <summary>
        /// Test seam for proving that a failed rebuild leaves an existing
        /// prototype byte-for-byte intact. Production callers use GetPrototype.
        /// </summary>
        public static PackedWoodyPrototypeAsset
            RebuildPrototypeAtPathForTests(GameObject prefab, string path)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "A project-relative .asset path is required.",
                    nameof(path));
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabPath) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    prefab, out string guid, out long localId))
                throw new InvalidDataException(
                    "Packed woody source must be a persistent prefab asset: " +
                    prefab.name);
            string dependency =
                AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            string stableKey = guid + ":" + localId + ":" +
                PackedWoodySpecies.Shrub;
            return RebuildPrototypeTransactional(
                prefab,
                PackedWoodySpecies.Shrub,
                false,
                path,
                guid,
                stableKey,
                dependency,
                ProjectPolicySignature(),
                AssetDatabase.LoadAssetAtPath<PackedWoodyPrototypeAsset>(
                    path));
        }

        private static PackedWoodyPrototypeAsset
            RebuildPrototypeTransactional(
                GameObject prefab,
                PackedWoodySpecies species,
                bool tree,
                string path,
                string guid,
                string stableKey,
                string dependency,
                string policySignature,
                PackedWoodyPrototypeAsset existing)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            PackedWoodyPrototypeAsset candidate = null;
            PackedWoodyLod[] lods = null;
            bool meshesCommitted = false;
            try
            {
                instance.transform.SetPositionAndRotation(
                    Vector3.zero, prefab.transform.localRotation);
                instance.transform.localScale = Vector3.one;
                MapVegetationMaterialBindings.Apply(instance);
                if (tree)
                    MapVegetationTreePresentation.Apply(instance, prefab);
                float sourceHeight = tree
                    ? MapVegetationTreePresentation.GetPrefabHeight(prefab)
                    : MeasureHeight(prefab);
                lods = BuildProxyLods(
                    instance, path, out Bounds localBounds);
                candidate = ScriptableObject.CreateInstance<
                    PackedWoodyPrototypeAsset>();
                candidate.name = prefab.name + " Packed Prototype Candidate";
                candidate.ConfigureForAuthoring(
                    PresentationVersion,
                    stableKey,
                    guid,
                    dependency,
                    policySignature,
                    species,
                    sourceHeight,
                    localBounds,
                    lods);
                ValidatePrototype(candidate);

                PackedWoodyPrototypeAsset committed = CommitPrototype(
                    path,
                    prefab.name + " Packed Prototype",
                    existing,
                    PresentationVersion,
                    stableKey,
                    guid,
                    dependency,
                    policySignature,
                    species,
                    sourceHeight,
                    localBounds,
                    lods);
                meshesCommitted = true;
                return committed;
            }
            finally
            {
                Object.DestroyImmediate(instance);
                if (candidate != null) Object.DestroyImmediate(candidate);
                if (!meshesCommitted) DestroyDetachedProxyMeshes(lods);
            }
        }

        private static PackedWoodyLod[] BuildProxyLods(
            GameObject root,
            string ownerPath,
            out Bounds localBounds)
        {
            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length > 1)
                throw new InvalidDataException(
                    "Packed woody prototype currently requires zero or one LODGroup: " +
                    root.name);
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled &&
                    renderer.gameObject.activeInHierarchy).ToArray();
            if (all.Length == 0)
                throw new InvalidDataException(
                    "Packed woody source contains no active renderers: " +
                    root.name);
            if (all.Any(renderer => !(renderer is MeshRenderer)))
                throw new InvalidDataException(
                    "Packed woody source contains a non-MeshRenderer: " +
                    root.name);

            LOD[] sourceLods;
            Renderer[] outside;
            if (groups.Length == 1)
            {
                sourceLods = groups[0].GetLODs();
                var owned = new HashSet<Renderer>(sourceLods.SelectMany(
                    lod => lod.renderers ?? Array.Empty<Renderer>()));
                outside = all.Where(renderer => !owned.Contains(renderer)).ToArray();
            }
            else
            {
                sourceLods = new[]
                {
                    new LOD(0.003f, all)
                };
                outside = Array.Empty<Renderer>();
            }
            if (sourceLods.Length == 0)
                throw new InvalidDataException(
                    "Packed woody source has an empty LODGroup: " + root.name);

            var output = new PackedWoodyLod[sourceLods.Length];
            var createdMeshes = new List<Mesh>();
            bool hasBounds = false;
            localBounds = default;
            Matrix4x4 rootInverse = root.transform.worldToLocalMatrix;
            try
            {
                for (int lodIndex = 0;
                     lodIndex < sourceLods.Length; lodIndex++)
                {
                    Renderer[] renderers =
                        (sourceLods[lodIndex].renderers ??
                            Array.Empty<Renderer>())
                        .Concat(outside)
                        .Where(renderer => renderer != null)
                        .Distinct()
                        .ToArray();
                    if (renderers.Length == 0)
                        throw new InvalidDataException(
                            "Packed woody LOD has no renderer: " + root.name);
                    var pieces = new List<ProxyPiece>();
                    foreach (Renderer renderer in renderers)
                    {
                        var meshRenderer = (MeshRenderer)renderer;
                        MeshFilter filter =
                            meshRenderer.GetComponent<MeshFilter>();
                        Mesh mesh = filter != null
                            ? filter.sharedMesh : null;
                        if (mesh == null)
                            throw new InvalidDataException(
                                "Packed woody MeshRenderer has no mesh: " +
                                renderer.name);
                        Material[] materials = renderer.sharedMaterials;
                        if (materials.Length < mesh.subMeshCount)
                            throw new InvalidDataException(
                                "Packed woody renderer has fewer materials than submeshes: " +
                                renderer.name);
                        Matrix4x4 relative = rootInverse *
                            renderer.transform.localToWorldMatrix;
                        bool negativeWinding = relative.determinant < 0f;
                        for (int subMesh = 0;
                             subMesh < mesh.subMeshCount; subMesh++)
                        {
                            Material material = materials[subMesh];
                            if (material == null || material.shader == null)
                                throw new InvalidDataException(
                                    "Packed woody renderer has a missing material/shader: " +
                                    renderer.name);
                            pieces.Add(new ProxyPiece(
                                mesh,
                                subMesh,
                                relative,
                                material,
                                negativeWinding,
                                renderer.shadowCastingMode,
                                renderer.receiveShadows));
                        }
                    }

                    var parts = new List<PackedWoodyDrawPart>();
                    int partIndex = 0;
                    foreach (IGrouping<ProxyPartKey, ProxyPiece> group in
                             pieces.GroupBy(piece => new ProxyPartKey(
                                     piece.Material,
                                     piece.NegativeWinding,
                                     piece.ShadowCasting,
                                     piece.ReceiveShadows))
                                 .OrderBy(group =>
                                         AssetIdentity(group.Key.Material),
                                     StringComparer.Ordinal)
                                 .ThenBy(group => group.Key.NegativeWinding)
                                 .ThenBy(group =>
                                     (int)group.Key.ShadowCasting)
                                 .ThenBy(group => group.Key.ReceiveShadows))
                    {
                        ProxyPiece[] grouped = group.ToArray();
                        string proxyName = root.name + " Packed LOD" +
                            lodIndex + " Part" + partIndex;
                        Mesh proxy = BuildCombinedProxy(grouped, proxyName);
                        createdMeshes.Add(proxy);
                        ValidateCombinedProxy(grouped, proxy);
                        if (group.Key.NegativeWinding)
                            FlipWinding(proxy);
                        proxy.RecalculateBounds();
                        proxy.UploadMeshData(true);
                        parts.Add(new PackedWoodyDrawPart(
                            proxy,
                            group.Key.Material,
                            0,
                            group.Key.ShadowCasting,
                            group.Key.ReceiveShadows));
                        if (!hasBounds)
                        {
                            localBounds = proxy.bounds;
                            hasBounds = true;
                        }
                        else localBounds.Encapsulate(proxy.bounds);
                        partIndex++;
                    }
                    output[lodIndex] = new PackedWoodyLod(
                        sourceLods[lodIndex]
                            .screenRelativeTransitionHeight,
                        parts.ToArray());
                }
                if (!hasBounds)
                    throw new InvalidDataException(
                        "Packed woody proxy generation produced no bounds: " +
                        ownerPath);
                return output;
            }
            catch
            {
                foreach (Mesh mesh in createdMeshes)
                    if (mesh != null) Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static PackedWoodyPrototypeAsset CommitPrototype(
            string path,
            string assetName,
            PackedWoodyPrototypeAsset existing,
            string presentationVersion,
            string stableKey,
            string guid,
            string dependency,
            string policySignature,
            PackedWoodySpecies species,
            float sourceHeight,
            Bounds localBounds,
            PackedWoodyLod[] lods)
        {
            AssetDatabase.SaveAssets();
            string absolutePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", path));
            bool existed = File.Exists(absolutePath);
            byte[] backup = existed
                ? File.ReadAllBytes(absolutePath) : null;
            try
            {
                PackedWoodyPrototypeAsset target = existing;
                if (target == null)
                {
                    target = ScriptableObject.CreateInstance<
                        PackedWoodyPrototypeAsset>();
                    target.name = assetName;
                    AssetDatabase.CreateAsset(target, path);
                }

                Mesh[] oldMeshes = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Mesh>().ToArray();
                foreach (Mesh mesh in EnumerateProxyMeshes(lods))
                    AssetDatabase.AddObjectToAsset(mesh, target);
                target.ConfigureForAuthoring(
                    presentationVersion,
                    stableKey,
                    guid,
                    dependency,
                    policySignature,
                    species,
                    sourceHeight,
                    localBounds,
                    lods);
                EditorUtility.SetDirty(target);
                ValidatePrototype(target);
                foreach (Mesh oldMesh in oldMeshes)
                    if (oldMesh != null)
                        Object.DestroyImmediate(oldMesh, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                target = AssetDatabase.LoadAssetAtPath<
                    PackedWoodyPrototypeAsset>(path);
                if (target == null)
                    throw new InvalidDataException(
                        "Committed packed woody prototype did not reload: " +
                        path);
                ValidatePrototype(target);
                return target;
            }
            catch
            {
                if (existed)
                {
                    File.WriteAllBytes(absolutePath, backup);
                    AssetDatabase.ImportAsset(
                        path,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    AssetDatabase.DeleteAsset(path);
                }
                throw;
            }
        }

        private static void ValidatePrototype(
            PackedWoodyPrototypeAsset prototype)
        {
            MonoScript script = MonoScript.FromScriptableObject(prototype);
            string scriptPath = script != null
                ? AssetDatabase.GetAssetPath(script)
                : string.Empty;
            if (script == null ||
                script.GetClass() != typeof(PackedWoodyPrototypeAsset) ||
                !string.Equals(
                    Path.GetFileNameWithoutExtension(scriptPath),
                    nameof(PackedWoodyPrototypeAsset),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Packed woody prototype has no matching Unity MonoScript. " +
                    "The ScriptableObject source file must be named " +
                    nameof(PackedWoodyPrototypeAsset) + ".cs.");
            }
            foreach (PackedWoodyLod lod in prototype.Lods)
            foreach (PackedWoodyDrawPart part in lod.DrawParts)
            {
                if (part.Material == null || !part.Material.enableInstancing)
                    throw new InvalidDataException(
                        "Reviewed woody material binding did not enable " +
                        "instancing: " + AssetDatabase.GetAssetPath(
                            part.Material));
            }
            IReadOnlyList<string> errors = prototype.ValidateConfiguration();
            if (errors.Count > 0)
                throw new InvalidDataException(
                    "Packed woody prototype is invalid: " +
                    string.Join("; ", errors.Take(12)));
        }

        private static IEnumerable<Mesh> EnumerateProxyMeshes(
            IEnumerable<PackedWoodyLod> lods) =>
            (lods ?? Array.Empty<PackedWoodyLod>())
            .Where(lod => lod != null)
            .SelectMany(lod => lod.DrawParts)
            .Where(part => part != null && part.Mesh != null)
            .Select(part => part.Mesh)
            .Distinct();

        private static void DestroyDetachedProxyMeshes(
            IEnumerable<PackedWoodyLod> lods)
        {
            foreach (Mesh mesh in EnumerateProxyMeshes(lods))
                if (mesh != null && !AssetDatabase.Contains(mesh))
                    Object.DestroyImmediate(mesh);
        }

        private static Mesh BuildCombinedProxy(
            ProxyPiece[] sources,
            string meshName)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException(
                    "Packed woody proxy sources are required.",
                    nameof(sources));
            long vertexTotal = sources.Sum(source =>
                (long)source.Mesh.vertexCount);
            long indexTotal = sources.Sum(source =>
                (long)source.Mesh.GetIndexCount(source.SubMeshIndex));
            if (vertexTotal <= 0 || vertexTotal > int.MaxValue ||
                indexTotal <= 0 || indexTotal > int.MaxValue)
                throw new InvalidDataException(
                    "Packed woody proxy exceeds managed mesh limits.");

            bool normalsRequired = sources.Any(source =>
                source.Mesh.HasVertexAttribute(VertexAttribute.Normal));
            bool tangentsRequired = sources.Any(source =>
                source.Mesh.HasVertexAttribute(VertexAttribute.Tangent));
            bool colorsRequired = sources.Any(source =>
                source.Mesh.HasVertexAttribute(VertexAttribute.Color));
            var uvRequired = new bool[8];
            for (int channel = 0; channel < uvRequired.Length; channel++)
            {
                VertexAttribute attribute = (VertexAttribute)(
                    (int)VertexAttribute.TexCoord0 + channel);
                uvRequired[channel] = sources.Any(source =>
                    source.Mesh.HasVertexAttribute(attribute));
            }

            int vertexCapacity = (int)vertexTotal;
            var vertices = new List<Vector3>(vertexCapacity);
            var normals = normalsRequired
                ? new List<Vector3>(vertexCapacity) : null;
            var tangents = tangentsRequired
                ? new List<Vector4>(vertexCapacity) : null;
            var colors = colorsRequired
                ? new List<Color>(vertexCapacity) : null;
            var uvs = new List<Vector4>[8];
            for (int channel = 0; channel < uvs.Length; channel++)
                if (uvRequired[channel])
                    uvs[channel] = new List<Vector4>(vertexCapacity);
            var indices = new List<int>((int)indexTotal);

            foreach (ProxyPiece source in sources)
            {
                if (source.Mesh.GetTopology(source.SubMeshIndex) !=
                    MeshTopology.Triangles)
                    throw new InvalidDataException(
                        "Packed woody proxy requires triangle source submeshes.");
                if (!float.IsFinite(source.Transform.determinant) ||
                    Mathf.Abs(source.Transform.determinant) < 0.00000001f)
                    throw new InvalidDataException(
                        "Packed woody renderer transform is degenerate.");

                using Mesh.MeshDataArray data =
                    MeshUtility.AcquireReadOnlyMeshData(source.Mesh);
                Mesh.MeshData read = data[0];
                int sourceVertexCount = read.vertexCount;
                int baseVertex = vertices.Count;
                using (var values = new NativeArray<Vector3>(
                    sourceVertexCount, Allocator.Temp))
                {
                    read.GetVertices(values);
                    for (int index = 0; index < values.Length; index++)
                        vertices.Add(source.Transform.MultiplyPoint3x4(
                            values[index]));
                }

                Vector3[] transformedNormals = normalsRequired
                    ? new Vector3[sourceVertexCount] : null;
                bool sourceHasNormals = read.HasVertexAttribute(
                    VertexAttribute.Normal);
                if (normalsRequired)
                {
                    if (sourceHasNormals)
                    {
                        using var values = new NativeArray<Vector3>(
                            sourceVertexCount, Allocator.Temp);
                        read.GetNormals(values);
                        Matrix4x4 normalMatrix =
                            source.Transform.inverse.transpose;
                        for (int index = 0; index < values.Length; index++)
                        {
                            Vector3 value = normalMatrix.MultiplyVector(
                                values[index]);
                            value = value.sqrMagnitude > 0.00000001f
                                ? value.normalized : Vector3.up;
                            transformedNormals[index] = value;
                            normals.Add(value);
                        }
                    }
                    else
                    {
                        Vector3 value = source.Transform.inverse.transpose
                            .MultiplyVector(Vector3.up).normalized;
                        for (int index = 0;
                             index < sourceVertexCount; index++)
                        {
                            transformedNormals[index] = value;
                            normals.Add(value);
                        }
                    }
                }

                if (tangentsRequired)
                {
                    if (read.HasVertexAttribute(VertexAttribute.Tangent))
                    {
                        using var values = new NativeArray<Vector4>(
                            sourceVertexCount, Allocator.Temp);
                        read.GetTangents(values);
                        for (int index = 0; index < values.Length; index++)
                        {
                            Vector4 sourceValue = values[index];
                            Vector3 value = source.Transform.MultiplyVector(
                                new Vector3(sourceValue.x, sourceValue.y,
                                    sourceValue.z));
                            if (normalsRequired)
                                value -= transformedNormals[index] *
                                    Vector3.Dot(transformedNormals[index], value);
                            value = value.sqrMagnitude > 0.00000001f
                                ? value.normalized : Vector3.right;
                            tangents.Add(new Vector4(
                                value.x, value.y, value.z, sourceValue.w));
                        }
                    }
                    else
                    {
                        Vector3 value = source.Transform.MultiplyVector(
                            Vector3.right).normalized;
                        for (int index = 0;
                             index < sourceVertexCount; index++)
                            tangents.Add(new Vector4(
                                value.x, value.y, value.z, 1f));
                    }
                }

                if (colorsRequired)
                {
                    if (read.HasVertexAttribute(VertexAttribute.Color))
                    {
                        using var values = new NativeArray<Color>(
                            sourceVertexCount, Allocator.Temp);
                        read.GetColors(values);
                        for (int index = 0; index < values.Length; index++)
                            colors.Add(values[index]);
                    }
                    else
                    {
                        for (int index = 0;
                             index < sourceVertexCount; index++)
                            colors.Add(Color.white);
                    }
                }

                for (int channel = 0; channel < uvs.Length; channel++)
                {
                    if (!uvRequired[channel]) continue;
                    VertexAttribute attribute = (VertexAttribute)(
                        (int)VertexAttribute.TexCoord0 + channel);
                    if (read.HasVertexAttribute(attribute))
                    {
                        using var values = new NativeArray<Vector4>(
                            sourceVertexCount, Allocator.Temp);
                        read.GetUVs(channel, values);
                        for (int index = 0; index < values.Length; index++)
                            uvs[channel].Add(values[index]);
                    }
                    else
                    {
                        for (int index = 0;
                             index < sourceVertexCount; index++)
                            uvs[channel].Add(Vector4.zero);
                    }
                }

                SubMeshDescriptor subMesh = read.GetSubMesh(
                    source.SubMeshIndex);
                using (var values = new NativeArray<int>(
                    subMesh.indexCount, Allocator.Temp))
                {
                    read.GetIndices(values, source.SubMeshIndex, true);
                    for (int index = 0; index < values.Length; index++)
                    {
                        int sourceIndex = values[index];
                        if (sourceIndex < 0 ||
                            sourceIndex >= sourceVertexCount)
                            throw new InvalidDataException(
                                "Packed woody source index is outside its vertex buffer.");
                        indices.Add(baseVertex + sourceIndex);
                    }
                }
            }

            var proxy = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            proxy.SetVertices(vertices);
            if (normalsRequired) proxy.SetNormals(normals);
            if (tangentsRequired) proxy.SetTangents(tangents);
            if (colorsRequired) proxy.SetColors(colors);
            for (int channel = 0; channel < uvs.Length; channel++)
                if (uvRequired[channel]) proxy.SetUVs(channel, uvs[channel]);
            proxy.SetTriangles(indices, 0, true);
            return proxy;
        }

        internal static Mesh BuildSingleProxyForTests(
            Mesh source,
            int subMeshIndex,
            Matrix4x4 transform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var piece = new ProxyPiece(
                source,
                subMeshIndex,
                transform,
                null,
                transform.determinant < 0f,
                ShadowCastingMode.On,
                true);
            Mesh proxy = BuildCombinedProxy(
                new[] { piece }, "Packed woody test proxy");
            ValidateCombinedProxy(new[] { piece }, proxy);
            if (piece.NegativeWinding) FlipWinding(proxy);
            proxy.RecalculateBounds();
            return proxy;
        }

        private static void ValidateCombinedProxy(
            ProxyPiece[] sources,
            Mesh proxy)
        {
            if (sources.Any(source => source.Mesh.GetTopology(
                    source.SubMeshIndex) != MeshTopology.Triangles))
                throw new InvalidDataException(
                    "Packed woody proxy requires triangle source submeshes.");
            long expectedIndices = sources.Sum(source =>
                (long)source.Mesh.GetIndexCount(source.SubMeshIndex));
            if (proxy.subMeshCount != 1 || proxy.vertexCount <= 0 ||
                (long)proxy.GetIndexCount(0) != expectedIndices)
                throw new InvalidDataException(
                    "Packed woody proxy lost source vertices/submesh indices.");

            foreach (VertexAttribute attribute in new[]
            {
                VertexAttribute.Normal,
                VertexAttribute.Tangent,
                VertexAttribute.Color,
                VertexAttribute.TexCoord0,
                VertexAttribute.TexCoord1,
                VertexAttribute.TexCoord2,
                VertexAttribute.TexCoord3,
                VertexAttribute.TexCoord4,
                VertexAttribute.TexCoord5,
                VertexAttribute.TexCoord6,
                VertexAttribute.TexCoord7
            })
            {
                bool presentInSource = sources.Any(source =>
                    source.Mesh.HasVertexAttribute(attribute));
                if (presentInSource && !proxy.HasVertexAttribute(attribute))
                    throw new InvalidDataException(
                        "Packed woody proxy lost vertex attribute " +
                        attribute + ".");
            }
        }

        private static void FlipWinding(Mesh mesh)
        {
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] indices = mesh.GetIndices(subMesh);
                if (indices.Length % 3 != 0)
                    throw new InvalidDataException(
                        "Packed woody proxy requires triangle topology.");
                for (int index = 0; index < indices.Length; index += 3)
                {
                    int value = indices[index + 1];
                    indices[index + 1] = indices[index + 2];
                    indices[index + 2] = value;
                }
                mesh.SetIndices(
                    indices, MeshTopology.Triangles, subMesh, false);
            }
            Vector4[] tangents = mesh.tangents;
            for (int index = 0; index < tangents.Length; index++)
                tangents[index].w = -tangents[index].w;
            if (tangents.Length > 0) mesh.tangents = tangents;
        }

        private static void BuildCollisionIndex(
            MapVegetationContext context,
            PackedWoodyCategory category,
            List<ResolvedPlacement> placements,
            out PackedWoodyCollisionTile[] tiles,
            out PackedWoodyCollisionRecord[] records)
        {
            if (category != PackedWoodyCategory.OriginalTree ||
                !context.Options.CreateTreeColliders)
            {
                tiles = Array.Empty<PackedWoodyCollisionTile>();
                records = Array.Empty<PackedWoodyCollisionRecord>();
                return;
            }

            var groups = new SortedDictionary<CollisionKey,
                List<PackedWoodyCollisionRecord>>();
            foreach (ResolvedPlacement placement in placements)
            {
                float height = Mathf.Max(
                    1f, placement.Placement.height * 0.72f);
                float radius = Mathf.Max(
                    0.14f, placement.Placement.height * 0.012f);
                var record = new PackedWoodyCollisionRecord(
                    Hash128.Compute(placement.Placement.id),
                    placement.Placement.position,
                    height,
                    radius);
                var key = new CollisionKey(
                    Mathf.FloorToInt(
                        placement.Placement.position.x /
                        BatchTileSizeMeters),
                    Mathf.FloorToInt(
                        placement.Placement.position.z /
                        BatchTileSizeMeters));
                if (!groups.TryGetValue(
                    key, out List<PackedWoodyCollisionRecord> values))
                    groups.Add(key, values =
                        new List<PackedWoodyCollisionRecord>());
                values.Add(record);
            }

            var outputRecords =
                new List<PackedWoodyCollisionRecord>(placements.Count);
            var outputTiles =
                new List<PackedWoodyCollisionTile>(groups.Count);
            foreach (KeyValuePair<CollisionKey,
                List<PackedWoodyCollisionRecord>> entry in groups)
            {
                PackedWoodyCollisionRecord[] sorted = entry.Value
                    .OrderBy(record => record.StableIdHash.ToString(),
                        StringComparer.Ordinal)
                    .ToArray();
                int start = outputRecords.Count;
                outputRecords.AddRange(sorted);
                Bounds bounds = new Bounds(
                    sorted[0].BottomCenter,
                    Vector3.zero);
                foreach (PackedWoodyCollisionRecord record in sorted)
                {
                    bounds.Encapsulate(record.BottomCenter +
                        Vector3.up * record.CapsuleHeight);
                    bounds.Expand(record.CapsuleRadius * 2f);
                }
                outputTiles.Add(new PackedWoodyCollisionTile(
                    entry.Key.X,
                    entry.Key.Z,
                    start,
                    sorted.Length,
                    bounds));
            }
            tiles = outputTiles.ToArray();
            records = outputRecords.ToArray();
        }

        private static float MeasureHeight(GameObject prefab)
        {
            if (HeightCache.TryGetValue(prefab, out float value)) return value;
            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.transform.SetPositionAndRotation(
                    Vector3.zero, prefab.transform.localRotation);
                instance.transform.localScale = Vector3.one;
                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidDataException(
                        "Packed woody source has no renderers: " + prefab.name);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1))
                    bounds.Encapsulate(renderer.bounds);
                value = Mathf.Max(0.01f, bounds.size.y);
                HeightCache[prefab] = value;
                return value;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Bounds CellBounds(
            MapVegetationContext context,
            MapVegetationCellPlan plan)
        {
            float size = context.Manifest.CellSizeMeters;
            return new Bounds(
                new Vector3(
                    (plan.Cell.X + 0.5f) * size,
                    0f,
                    (plan.Cell.Z + 0.5f) * size),
                new Vector3(size, 4096f, size));
        }

        public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(
                new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(
                new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(
                new Vector3(0f, 0f, extents.z));
            return new Bounds(center, new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)) * 2f);
        }

        private static PackedWoodyCategory ToPackedCategory(
            MapVegetationCategories category)
        {
            switch (category)
            {
                case MapVegetationCategories.OriginalTrees:
                    return PackedWoodyCategory.OriginalTree;
                case MapVegetationCategories.BoundaryForest:
                    return PackedWoodyCategory.BoundaryForest;
                case MapVegetationCategories.ShrubsAndUndergrowth:
                    return PackedWoodyCategory.ShrubOrUndergrowth;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        private static PackedWoodySpecies ParseTreeSpecies(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "spruce": return PackedWoodySpecies.Spruce;
                case "pine": return PackedWoodySpecies.Pine;
                case "birch": return PackedWoodySpecies.Birch;
                case "aspen": return PackedWoodySpecies.Aspen;
                default:
                    throw new InvalidDataException(
                        "Packed tree has an unsupported species: " + value);
            }
        }

        private static string AssetIdentity(Object value)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                value, out string guid, out long localId)
                    ? guid + ":" + localId
                    : value != null ? value.name : string.Empty;
        }

        public static string ProjectPolicySignature()
        {
            if (!string.IsNullOrEmpty(policySignatureCache))
                return policySignatureCache;
            var evidence = new StringBuilder(4096)
                .Append(PresentationVersion).Append('|')
                .Append(MapVegetationTreePresentation.Version).Append('\n');
            foreach (string path in new[]
            {
                "Assets/Game/Editor/Vegetation/MapVegetationPackedWoodyBuilder.cs",
                "Assets/Game/Editor/Vegetation/MapVegetationMaterialBindings.cs",
                "Assets/Game/Editor/Vegetation/MapVegetationTreeMaterialPolicy.cs",
                "Assets/Game/Editor/Vegetation/MapVegetationTreePresentation.cs",
                "Assets/Game/Editor/Vegetation/MapVegetationAlpSpruceBindings.cs",
                "Assets/Game/Editor/Vegetation/MapVegetationBillboardWindingRepair.cs",
                "Assets/Game/World/Runtime/Vegetation/PackedWoodyPrototypeAsset.cs",
                "Assets/Game/World/Runtime/Vegetation/PackedWoodyTypes.cs",
                "Assets/Game/World/Runtime/Vegetation/PackedWoodyCellAsset.cs",
                "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindHDRP.shader",
                "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl"
            })
            {
                evidence.Append(path).Append(':')
                    .Append(File.Exists(path) ? HashFile(path) : "missing")
                    .Append('\n');
            }
            using SHA256 sha = SHA256.Create();
            policySignatureCache = BitConverter.ToString(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(evidence.ToString())))
                .Replace("-", string.Empty).ToLowerInvariant();
            return policySignatureCache;
        }

        public static bool HasCurrentProjectPolicy(
            PackedWoodyPrototypeAsset prototype) =>
            prototype != null &&
            prototype.PresentationVersion == PresentationVersion &&
            prototype.ProjectPolicySignature == ProjectPolicySignature();

        private static string HashFile(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Folder path is required.");
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
                throw new InvalidOperationException(
                    "Invalid packed woody folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public readonly struct ExpectedPlacement
        {
            public readonly Hash128 StableIdHash;
            public readonly string PrototypeStableKey;
            public readonly Matrix4x4 Matrix;
            public readonly PackedWoodyPlacementMethod Method;
            public readonly PackedWoodySpecies Species;
            public readonly float HeightMeters;

            public ExpectedPlacement(
                Hash128 stableIdHash,
                string prototypeStableKey,
                Matrix4x4 matrix,
                PackedWoodyPlacementMethod method,
                PackedWoodySpecies species,
                float heightMeters)
            {
                StableIdHash = stableIdHash;
                PrototypeStableKey = prototypeStableKey;
                Matrix = matrix;
                Method = method;
                Species = species;
                HeightMeters = heightMeters;
            }
        }

        private sealed class ResolvedPlacement
        {
            public readonly MapVegetationPlacement Placement;
            public readonly PackedWoodyPrototypeAsset Prototype;
            public readonly Matrix4x4 Matrix;
            public readonly float HeightMeters;
            public readonly PackedWoodyPlacementMethod Method;
            public readonly PackedWoodySpecies Species;
            public int PrototypeIndex;

            public ResolvedPlacement(
                MapVegetationPlacement placement,
                PackedWoodyPrototypeAsset prototype,
                Matrix4x4 matrix,
                float heightMeters,
                PackedWoodyPlacementMethod method,
                PackedWoodySpecies species)
            {
                Placement = placement;
                Prototype = prototype;
                Matrix = matrix;
                HeightMeters = heightMeters;
                Method = method;
                Species = species;
            }
        }

        private readonly struct BatchKey : IComparable<BatchKey>
        {
            public readonly int PrototypeIndex;
            public readonly int TileX;
            public readonly int TileZ;
            public BatchKey(int prototypeIndex, int tileX, int tileZ)
            {
                PrototypeIndex = prototypeIndex;
                TileX = tileX;
                TileZ = tileZ;
            }
            public int CompareTo(BatchKey other)
            {
                int value = PrototypeIndex.CompareTo(other.PrototypeIndex);
                if (value != 0) return value;
                value = TileZ.CompareTo(other.TileZ);
                return value != 0 ? value : TileX.CompareTo(other.TileX);
            }
        }

        private readonly struct CollisionKey : IComparable<CollisionKey>
        {
            public readonly int X;
            public readonly int Z;
            public CollisionKey(int x, int z) { X = x; Z = z; }
            public int CompareTo(CollisionKey other)
            {
                int value = Z.CompareTo(other.Z);
                return value != 0 ? value : X.CompareTo(other.X);
            }
        }

        private readonly struct ProxyPiece
        {
            public readonly Mesh Mesh;
            public readonly int SubMeshIndex;
            public readonly Matrix4x4 Transform;
            public readonly Material Material;
            public readonly bool NegativeWinding;
            public readonly ShadowCastingMode ShadowCasting;
            public readonly bool ReceiveShadows;
            public ProxyPiece(
                Mesh mesh,
                int subMeshIndex,
                Matrix4x4 transform,
                Material material,
                bool negativeWinding,
                ShadowCastingMode shadowCasting,
                bool receiveShadows)
            {
                Mesh = mesh;
                SubMeshIndex = subMeshIndex;
                Transform = transform;
                Material = material;
                NegativeWinding = negativeWinding;
                ShadowCasting = shadowCasting;
                ReceiveShadows = receiveShadows;
            }
        }

        private readonly struct ProxyPartKey : IEquatable<ProxyPartKey>
        {
            public readonly Material Material;
            public readonly bool NegativeWinding;
            public readonly ShadowCastingMode ShadowCasting;
            public readonly bool ReceiveShadows;
            public ProxyPartKey(
                Material material,
                bool negativeWinding,
                ShadowCastingMode shadowCasting,
                bool receiveShadows)
            {
                Material = material;
                NegativeWinding = negativeWinding;
                ShadowCasting = shadowCasting;
                ReceiveShadows = receiveShadows;
            }
            public bool Equals(ProxyPartKey other) =>
                Material == other.Material &&
                NegativeWinding == other.NegativeWinding &&
                ShadowCasting == other.ShadowCasting &&
                ReceiveShadows == other.ReceiveShadows;
            public override bool Equals(object obj) =>
                obj is ProxyPartKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Material != null
                        ? Material.GetEntityId().GetHashCode() : 0;
                    hash = hash * 397 ^ NegativeWinding.GetHashCode();
                    hash = hash * 397 ^ (int)ShadowCasting;
                    hash = hash * 397 ^ ReceiveShadows.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
