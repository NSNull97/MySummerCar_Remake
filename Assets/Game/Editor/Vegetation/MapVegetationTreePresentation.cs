using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.Vegetation
{
    /// <summary>Presentation-only bindings for existing placements; never samples or moves plants.</summary>
    public static class MapVegetationTreePresentation
    {
        public const string Version = "msc.tree-presentation.v7";
        public const string SelectionPolicy =
            "fixed-reviewed-species-list+strict-lod0-3d-trunk-foliage-acceptance+height-matched-variant";
        public const string ReportPath = "Artifacts/VegetationRebuild/TreePresentationAudit/authored-trunk-provenance.json";
        private const string BarkGuid = "b307f3aa8aaa23a418578ba77355f781";
        private const string MeshRoot = MapVegetationRebuildOptions.GeneratedRoot + "/TreePresentation/Trunks";
        private static readonly string[] SpeciesNames = { "Spruce", "Pine", "Birch", "Aspen" };
        private static readonly string[][] PrefabGuids =
        {
            new[] { "cf20a0710e07e6240887a28dccb24e52", "696cd56d0d9b30043af71846ef37e605", "41c9136b21d576d469b04e7004b8fa43", "4b30c62703dc36e47866ce29cf3f1f4a", "c680e57e3033fd843a4115b58d0d1bb7" },
            new[] { "6ba00aaf4735c224aac3a4240aa7a818", "761702ee9032aa1448a58109360daec2", "d035ff98f14f83f4480891f44c17ba6f", "8a682bc4c0a4a214d8253ba85a744bd7", "379ff3d69bd575a40a311cb735fe5f40" },
            new[] { "7c10f9881bfc5ab43a0e8f7cc7e81825", "094b0b07cdb5bb341b34fe47fe6420ce", "de0957f46d22f8044a96816ab6a4b284", "fdab057734110594f85f8625927914e5" },
            new[] { "e4a261cf12128854d8ff76eaae222e77", "a994ba8c97ac878489424914d4a6e774", "cb5cbbfadb89ffa4089db57a1f05b026", "39324e82554919f49a538a2ec7120a10" }
        };
        private static readonly RejectedTreeVariant[] RejectedVariants =
        {
            new RejectedTreeVariant
            {
                species = "Aspen",
                prefabGuid = "4778a89862ad262488d9d29b49559c22",
                prefabPath =
                    "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_02.prefab",
                classification = "RejectedForFidelity",
                reason =
                    "Strict LOD0 acceptance found bark bounds shaped like a rock/log volume rather than a vertically extensive 3D trunk. No synthetic trunk fallback is permitted."
            }
        };
        private static readonly Dictionary<string, Mesh> Trunks = new Dictionary<string, Mesh>();
        private static readonly Dictionary<GameObject, float> PrefabHeights = new Dictionary<GameObject, float>();
        private static readonly Dictionary<string, GameObject[]> PrefabCache = new Dictionary<string, GameObject[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<GameObject, string> SpeciesCache = new Dictionary<GameObject, string>();
        private static readonly HashSet<GameObject> QualityAcceptedPrefabs =
            new HashSet<GameObject>();

        public static void BuildAssetsBatch()
        {
            Mesh[] meshes = BuildAssets();
            Debug.Log("MAP_TREE_PRESENTATION_OK alpSprucePrefabs=" +
                MapVegetationAlpSpruceBindings.OutputPrefabPaths.Length +
                " legacyAuthoredTrunks=" + meshes.Length + " report=" + ReportPath);
        }

        public static Mesh[] BuildAssets()
        {
            EnsureFolder(MeshRoot);
            Trunks.Clear();
            PrefabHeights.Clear();
            MapVegetationAlpSpruceBindings.BuildPrefabs();
            PrefabCache.Clear();
            SpeciesCache.Clear();
            QualityAcceptedPrefabs.Clear();
            var output = new List<Mesh>();
            var report = new TrunkReport
            {
                version = Version,
                strategy = "ALP wrapper prefabs preserve authored 4/5-stage LOD meshes and their real bark submesh at every near/middle stage; the rejected Engelmann primitive/low-trunk patch is no longer used.",
                rejectedVariants = RejectedVariants
            };
            AssetDatabase.SaveAssets();
            foreach (TrunkEvidence evidence in report.meshes) evidence.outputSha256 = HashFile(evidence.outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            return output.ToArray();
        }

        public static GameObject[] LoadSpeciesPrefabs(string species)
        {
            int index = Array.FindIndex(SpeciesNames, s => string.Equals(s, species, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new ArgumentException("Unknown approved tree species: " + species, nameof(species));
            if (PrefabCache.TryGetValue(species, out GameObject[] cached) && cached.All(p => p != null)) return cached;
            GameObject[] loaded;
            if (index == 0)
            {
                loaded = MapVegetationAlpSpruceBindings.OutputPrefabPaths
                    .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path)).ToArray();
                if (loaded.Any(prefab => prefab == null))
                    loaded = MapVegetationAlpSpruceBindings.BuildPrefabs();
            }
            else
            {
                loaded = PrefabGuids[index].Select(g =>
                    AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))
                    ?? throw new FileNotFoundException("Approved tree prefab is missing: " + g)).ToArray();
            }
            var qualityFailures = new List<string>();
            foreach (GameObject prefab in loaded)
            {
                SpeciesCache[prefab] = SpeciesNames[index];
                try
                {
                    RequireQualityAccepted(prefab, SpeciesNames[index]);
                }
                catch (Exception exception)
                {
                    qualityFailures.Add(AssetDatabase.GetAssetPath(prefab) +
                        ": " + exception.Message);
                }
            }
            if (qualityFailures.Count > 0)
                throw new InvalidDataException(
                    "Every active " + SpeciesNames[index] +
                    " candidate was audited; rejected variants:\n" +
                    string.Join("\n", qualityFailures));
            PrefabCache[species] = loaded;
            return loaded;
        }

        public static GameObject[] LoadAllApprovedPrefabs()
        {
            var loaded = new List<GameObject>();
            var failures = new List<string>();
            foreach (string species in SpeciesNames)
            {
                try
                {
                    loaded.AddRange(LoadSpeciesPrefabs(species));
                }
                catch (Exception exception)
                {
                    failures.Add(species + ": " + exception.Message);
                }
            }
            if (failures.Count > 0)
                throw new InvalidDataException(
                    "Active tree quality audit rejected one or more species " +
                    "pools:\n" + string.Join("\n", failures));
            return loaded.ToArray();
        }

        internal static IReadOnlyList<RejectedTreeVariant>
            RejectedVariantsForTests => RejectedVariants;

        private static void RequireQualityAccepted(GameObject prefab,
            string species)
        {
            if (QualityAcceptedPrefabs.Contains(prefab)) return;
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                // Chernobyl prefabs remain read-only vendor geometry. The
                // actual packed/runtime presentation always receives the
                // generated HDRP bindings on a transient copy before its LODs
                // are extracted, so the selection gate must audit that same
                // representation instead of rejecting the untouched vendor
                // material reference on the source prefab.
                MapVegetationMaterialBindings.Apply(instance);
                Apply(instance, prefab);
                TreeAcceptanceEvidence evidence = MapVegetationTreeAcceptance
                    .AuditPrefab(instance, species, path);
                TreeLodGeometryEvidence near = evidence.lods[0];
                if (!near.nonFlatNearGeometry || !near.hasVolumetricTrunk ||
                    !near.hasBarkMaterialGeometry ||
                    !near.hasFoliageGeometry || !near.nearProxyRejected)
                    throw new InvalidDataException(
                        "Reviewed tree failed the strict near-quality gate: " +
                        path);
                QualityAcceptedPrefabs.Add(prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        public static string GetSpecies(GameObject sourcePrefab)
        {
            if (sourcePrefab == null) throw new ArgumentNullException(nameof(sourcePrefab));
            if (SpeciesCache.TryGetValue(sourcePrefab, out string species)) return species;
            string path = AssetDatabase.GetAssetPath(sourcePrefab);
            if (Array.IndexOf(MapVegetationAlpSpruceBindings.OutputPrefabPaths, path) >= 0)
            {
                SpeciesCache[sourcePrefab] = "Spruce";
                return "Spruce";
            }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourcePrefab));
            for (int index = 1; index < SpeciesNames.Length; index++)
                if (Array.IndexOf(PrefabGuids[index], guid) >= 0) { SpeciesCache[sourcePrefab] = SpeciesNames[index]; return SpeciesNames[index]; }
            throw new InvalidDataException("Tree prefab is outside the approved presentation list: " + sourcePrefab);
        }

        /// <summary>Matches the writer's all-renderer height at authored root rotation and unit root scale.</summary>
        public static float GetPrefabHeight(GameObject prefab)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if ((prefab.transform.localScale - Vector3.one).sqrMagnitude > 0.0000000001f)
                throw new InvalidDataException("Approved tree prefab must retain its audited unit root scale: " + prefab.name);
            return Height(prefab);
        }

        /// <summary>The caller assigns exact species quotas; this only picks a suitable authored variant.</summary>
        public static GameObject SelectPrefab(string species, string placementId, int seed, float preservedHeightMeters = 0f)
        {
            GameObject[] candidates = LoadSpeciesPrefabs(species);
            if (preservedHeightMeters > 0f)
            {
                if (!float.IsFinite(preservedHeightMeters)) throw new ArgumentOutOfRangeException(nameof(preservedHeightMeters));
                float[] scores = candidates.Select(p => Mathf.Abs(Mathf.Log(Height(p) / preservedHeightMeters))).ToArray();
                float best = scores.Min();
                // A mature tree must not become a 1 m / 190-triangle sapling enlarged twenty-fold.
                candidates = candidates.Where((p, i) => scores[i] <= best + 0.35f).ToArray();
            }
            uint hash = MapVegetationPlanning.HashId(placementId, seed);
            return candidates[hash % (uint)candidates.Length];
        }

        /// <summary>Changes mesh/LOD presentation only. Root transform and all colliders are untouched.</summary>
        public static void Apply(GameObject instance, GameObject sourcePrefab)
        {
            if (instance == null || sourcePrefab == null) throw new ArgumentNullException();
            string species = GetSpecies(sourcePrefab);
            LODGroup group = RequireGroup(instance);
            ConfigureLods(group, species == "Spruce");
        }

        internal static void ConfigureLods(LODGroup group, bool spruce)
        {
            LOD[] lods = group.GetLODs();
            float[] thresholds;
            if (spruce)
            {
                thresholds = MapVegetationAlpSpruceBindings.Thresholds(lods.Length);
            }
            else if (lods.Length == 4) thresholds = new[] { 0.20f, 0.075f, 0.025f, 0.0035f };
            else if (lods.Length == 3) thresholds = new[] { 0.20f, 0.055f, 0.005f };
            else if (lods.Length == 2) thresholds = new[] { 0.20f, 0.005f };
            else throw new InvalidDataException("Unexpected Chernobyl LOD count: " + lods.Length);
            for (int index = 0; index < lods.Length; index++)
            {
                // Preserve every authored renderer set, including variants without a billboard.
                if (lods[index].renderers.Length == 0 || lods[index].renderers.Any(r => r == null))
                    throw new InvalidDataException("Tree LOD has no valid renderer.");
                lods[index].screenRelativeTransitionHeight = thresholds[index];
                lods[index].fadeTransitionWidth = 0.12f;
            }
            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = false;
            Record(group);
        }

        internal static Mesh ExtractAuthoredTrunk(Mesh source, Matrix4x4 toTree, out TrunkEvidence evidence)
        {
            // Editor-only snapshot reads non-readable imported FBX without changing its importer.
            using Mesh.MeshDataArray data = MeshUtility.AcquireReadOnlyMeshData(source);
            Mesh.MeshData read = data[0];
            if (read.subMeshCount != 1 || read.GetSubMesh(0).topology != MeshTopology.Triangles)
                throw new InvalidDataException("Approved spruce bark must be one triangle submesh.");
            using var vertexData = new NativeArray<Vector3>(read.vertexCount, Allocator.Temp);
            read.GetVertices(vertexData);
            Vector3[] vertices = vertexData.ToArray();
            using var indexData = new NativeArray<int>(read.GetSubMesh(0).indexCount, Allocator.Temp);
            read.GetIndices(indexData, 0, true);
            int[] indices = indexData.ToArray();
            if (vertices.Length == 0 || indices.Length < 3 || indices.Length % 3 != 0) throw new InvalidDataException("Invalid bark topology.");
            int[] parents = Enumerable.Range(0, vertices.Length).ToArray();
            float tolerance = Mathf.Max(source.bounds.size.magnitude * 0.000001f, 0.000001f);
            var welded = new Dictionary<WeldKey, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = new WeldKey(vertices[i], tolerance);
                if (welded.TryGetValue(key, out int other)) Union(parents, i, other);
                else welded.Add(key, i);
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                Union(parents, indices[i], indices[i + 1]);
                Union(parents, indices[i], indices[i + 2]);
            }
            Bounds full = new Bounds(toTree.MultiplyPoint3x4(vertices[indices[0]]), Vector3.zero);
            var components = new Dictionary<int, ComponentBounds>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                int root = Find(parents, indices[i]);
                if (!components.TryGetValue(root, out ComponentBounds component))
                {
                    component = new ComponentBounds { root = root, bounds = new Bounds(toTree.MultiplyPoint3x4(vertices[indices[i]]), Vector3.zero) };
                    components.Add(root, component);
                }
                component.triangles++;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 p = toTree.MultiplyPoint3x4(vertices[indices[i + j]]);
                    component.bounds.Encapsulate(p); full.Encapsulate(p);
                }
            }
            // Bark01_1 contains detached branch pieces outside the main stem's Y range:
            // the actual 2380-triangle stem spans only 76.177% of the whole bark bounds.
            // Compare connected structures, not the union of disconnected branches.
            float maximumComponentHeight = components.Values.Max(c => c.bounds.size.y);
            ComponentBounds[] candidates = components.Values.Where(c =>
                c.bounds.size.y >= maximumComponentHeight * 0.8f &&
                Mathf.Max(c.bounds.size.x, c.bounds.size.z) <= c.bounds.size.y * 0.2f).ToArray();
            ComponentBounds[] tallest = components.Values.OrderByDescending(c => c.bounds.size.y).Take(8).ToArray();
            string diagnostic = "full " + Describe(full) + "; tallest=" + string.Join(" | ", tallest.Select(c =>
                "triangles=" + c.triangles + " " + Describe(c.bounds)));
            if (candidates.Length != 1) throw new InvalidDataException("Authored main trunk is ambiguous: " + source.name +
                ", candidates=" + candidates.Length + "; " + diagnostic);
            ComponentBounds selected = candidates[0];
            if (selected.triangles > 10000) throw new InvalidDataException("Authored trunk exceeds the bounded 10000-triangle budget; heavy bark fallback is forbidden; " + diagnostic);
            var oldIndices = new List<int>(); var compactIndices = new List<int>(); var remap = new Dictionary<int, int>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (Find(parents, indices[i]) != selected.root) continue;
                for (int j = 0; j < 3; j++)
                {
                    int original = indices[i + j];
                    if (!remap.TryGetValue(original, out int compact))
                    { compact = oldIndices.Count; oldIndices.Add(original); remap.Add(original, compact); }
                    compactIndices.Add(compact);
                }
            }
            var mesh = new Mesh { name = source.name + " AuthoredMainTrunk", indexFormat = oldIndices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = oldIndices.Select(i => vertices[i]).ToArray();
            if (read.HasVertexAttribute(VertexAttribute.Normal))
            {
                using var values = new NativeArray<Vector3>(read.vertexCount, Allocator.Temp); read.GetNormals(values);
                mesh.normals = oldIndices.Select(i => values[i]).ToArray();
            }
            if (read.HasVertexAttribute(VertexAttribute.Tangent))
            {
                using var values = new NativeArray<Vector4>(read.vertexCount, Allocator.Temp); read.GetTangents(values);
                mesh.tangents = oldIndices.Select(i => values[i]).ToArray();
            }
            for (int channel = 0; channel < 8; channel++)
            {
                if (!read.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel))) continue;
                using var values = new NativeArray<Vector4>(read.vertexCount, Allocator.Temp); read.GetUVs(channel, values);
                mesh.SetUVs(channel, oldIndices.Select(i => values[i]).ToList());
            }
            if (read.HasVertexAttribute(VertexAttribute.Color))
            {
                using var values = new NativeArray<Color>(read.vertexCount, Allocator.Temp); read.GetColors(values);
                mesh.colors = oldIndices.Select(i => values[i]).ToArray();
            }
            mesh.SetTriangles(compactIndices, 0, true);
            if (!read.HasVertexAttribute(VertexAttribute.Normal)) mesh.RecalculateNormals();
            evidence = new TrunkEvidence { sourceVertices = vertices.Length, sourceTriangles = indices.Length / 3,
                connectedComponents = components.Count, outputVertices = oldIndices.Count, outputTriangles = compactIndices.Count / 3,
                selectedBoundsInTree = selected.bounds, sourceBoundsInTree = full, weldTolerance = tolerance,
                maximumComponentHeight = maximumComponentHeight, sourceToSelectionMatrix = toTree,
                selectedHeightFractionOfBark = selected.bounds.size.y / Mathf.Max(full.size.y, 0.000001f),
                tallestComponents = tallest.Select(c => new ComponentEvidence { triangles = c.triangles, bounds = c.bounds }).ToArray(),
                method = "Unique authored connected component spanning >=80% maximum connected-component height and <=20% width, measured in authored upright tree frame at unit root scale/zero origin; original mesh-space vertices/UV/normals/tangents retained; no simplification or primitive fallback." };
            return mesh;
        }

        internal static Matrix4x4 TrunkSelectionMatrix(GameObject prefab, Renderer bark)
        {
            // Some imported prefab roots carry the authoring axis correction. Include
            // that rotation for selection only; output geometry stays in source space.
            return Matrix4x4.Rotate(prefab.transform.localRotation) * RelativeMatrix(bark.transform, prefab.transform);
        }

        private static string Describe(Bounds bounds) => string.Format(CultureInfo.InvariantCulture,
            "min=({0:R},{1:R},{2:R}) max=({3:R},{4:R},{5:R}) size=({6:R},{7:R},{8:R})",
            bounds.min.x, bounds.min.y, bounds.min.z, bounds.max.x, bounds.max.y, bounds.max.z,
            bounds.size.x, bounds.size.y, bounds.size.z);

        private static float Height(GameObject prefab)
        {
            if (PrefabHeights.TryGetValue(prefab, out float height)) return height;
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Tree prefab has no renderers.");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            height = Mathf.Max(bounds.size.y, 0.01f); PrefabHeights[prefab] = height; return height;
        }

        private static LODGroup RequireGroup(GameObject instance)
        {
            LODGroup[] groups = instance.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length != 1) throw new InvalidDataException("Expected one tree LODGroup: " + instance.name);
            return groups[0];
        }

        private static Renderer FindBark(IEnumerable<Renderer> renderers)
        {
            Renderer[] matches = renderers.Where(r => r != null && r.sharedMaterials.Length == 1 &&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(r.sharedMaterial)) == BarkGuid).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Expected exactly one approved authored spruce bark renderer.");
            return matches[0];
        }

        private static void SetLocalMatrix(Transform transform, Matrix4x4 matrix)
        {
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) < 0.000001f) throw new InvalidDataException("Degenerate authored trunk transform.");
            if (matrix.determinant < 0f) scale.x = -scale.x;
            Quaternion rotation = Quaternion.LookRotation(z / scale.z, y / scale.y);
            Vector3 position = matrix.GetColumn(3);
            Matrix4x4 reconstructed = Matrix4x4.TRS(position, rotation, scale);
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(reconstructed[i] - matrix[i]) > 0.0001f * Mathf.Max(1f, Mathf.Abs(matrix[i])))
                    throw new InvalidDataException("Authored trunk transform contains shear; no approximate transform was applied.");
            transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale;
        }

        private static Matrix4x4 RelativeMatrix(Transform child, Transform ancestor)
        {
            // Compose local authoring transforms instead of cancelling kilometre-scale
            // world translations, which would introduce avoidable trunk offsets.
            Matrix4x4 matrix = Matrix4x4.identity;
            while (child != ancestor)
            {
                if (child == null) throw new InvalidDataException("Authored trunk escaped its tree hierarchy.");
                matrix = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * matrix;
                child = child.parent;
            }
            return matrix;
        }

        private static int Find(int[] parent, int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        private static void Union(int[] parent, int a, int b) { a = Find(parent, a); b = Find(parent, b); if (a != b) parent[b] = a; }
        private static string TrunkPath(GameObject prefab) => MeshRoot + "/" + prefab.name + "_AuthoredTrunk.asset";
        private static void Record(UnityEngine.Object target) { if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
        private static string HashFile(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(); }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private sealed class ComponentBounds { public int root, triangles; public Bounds bounds; }
        private readonly struct WeldKey : IEquatable<WeldKey>
        {
            private readonly long x, y, z;
            public WeldKey(Vector3 p, float step) { x = (long)Math.Round(p.x / (double)step); y = (long)Math.Round(p.y / (double)step); z = (long)Math.Round(p.z / (double)step); }
            public bool Equals(WeldKey other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) => obj is WeldKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(x, y, z);
        }
        [Serializable] private sealed class TrunkReport
        {
            public string version, strategy;
            public RejectedTreeVariant[] rejectedVariants =
                Array.Empty<RejectedTreeVariant>();
            public List<TrunkEvidence> meshes = new List<TrunkEvidence>();
        }
        [Serializable] internal sealed class RejectedTreeVariant
        {
            public string species, prefabGuid, prefabPath, classification,
                reason;
        }
        [Serializable] internal sealed class TrunkEvidence
        {
            public string prefabPath, prefabGuid, prefabSha256, sourceMeshPath, sourceMeshGuid, sourceMeshLocalId, sourceFileSha256, outputPath, outputSha256, barkMaterialGuid, method;
            public int sourceVertices, sourceTriangles, connectedComponents, outputVertices, outputTriangles;
            public float weldTolerance, maximumComponentHeight, selectedHeightFractionOfBark;
            public Bounds selectedBoundsInTree, sourceBoundsInTree;
            public Quaternion authoredRootRotation;
            public Matrix4x4 sourceToSelectionMatrix;
            public ComponentEvidence[] tallestComponents;
        }
        [Serializable] internal sealed class ComponentEvidence { public int triangles; public Bounds bounds; }
    }
}
