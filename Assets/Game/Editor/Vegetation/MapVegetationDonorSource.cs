using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.LegacyImport;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    [Serializable]
    public sealed class MapVegetationSourcePoint
    {
        public string Id;
        public Vector3 Position;
        public string CellId;
        public string SourceStableId;
        public string SourcePath;
        public string SourceMeshGuid;
        public string SourceHash;
        public string SecondarySourceStableId;
        public string SecondarySourceMeshGuid;
        public string SecondarySourceHash;
        public string SourceHashKind = "SanitizedMeshAssetSHA256";
        public string Method;
        public string Species = "Unknown";
        public string SourceFamily;
        public float Height;
    }

    [Serializable]
    public sealed class MapVegetationBoundarySegment
    {
        public string Id;
        public Vector3 A;
        public Vector3 B;
        public Vector3 AdjacentTriangleNormal;
        public Vector3 Outward;
        public bool HasValidatedOutward;
        public string EnclosureId;
        public bool IsOuterEnvelope;
        public bool EnclosureClosedBySmallGap;
        public float EnclosureSignedArea;
        public float EnclosurePerimeter;
        public int PositiveTreeSideEvidenceCount;
        public int NegativeTreeSideEvidenceCount;
        public float TreeSideConfidence;
        public string OrientationMethod;
        public string SourceStableId;
        public string SourcePath;
        public string SourceMeshGuid;
        public string SourceHash;
    }

    [Serializable]
    public sealed class MapVegetationRockAnchor
    {
        public string Id;
        public Vector3 Position;
        public Vector3 SourceSize;
        public float Yaw;
        public string CellId;
        public string ReplacementKey;
        public string SourceStableId;
        public string SourcePath;
        public string SourceMeshGuid;
        public string SourceHash;
        public string Method = "CanonicalRockGeometryComponent";
    }

    [Serializable]
    public sealed class MapVegetationSourceDiagnostic
    {
        public string Code;
        public string Message;
        public string SourcePath;
        public Vector3 Position;
        public string CellId;
    }

    [Serializable]
    public sealed class MapVegetationSourceSnapshot
    {
        public List<MapVegetationSourcePoint> Trees = new List<MapVegetationSourcePoint>();
        public List<MapVegetationSourcePoint> Shrubs = new List<MapVegetationSourcePoint>();
        public List<MapVegetationRockAnchor> Rocks = new List<MapVegetationRockAnchor>();
        public List<MapVegetationBoundarySegment> BoundarySegments = new List<MapVegetationBoundarySegment>();
        public List<MapVegetationSourceDiagnostic> Diagnostics = new List<MapVegetationSourceDiagnostic>();
        public List<string> BillboardSourcePaths = new List<string>();
        public bool PositionsAreProjectSpace = true;
        public string SourceRevision = "msc-world-baseline-04a1.1-c3f2f337";
        public string CanonicalSceneSha256 = "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";
        public int SourceMeshCount;
        public int ConnectedComponentCount;
        public int PairedCardCount;
        public int UnpairedCardCount;
    }

    /// <summary>
    /// Reads only explicitly supplied sanitized donor roots. Imported mesh world
    /// transforms already include the garage-origin conversion; applying the donor
    /// translation again is incorrect. This reader never changes scenes or assets.
    /// </summary>
    public static class MapVegetationDonorSource
    {
        private const float VerticalTolerance = 0.025f;
        private const float PairIndexSize = 8f;

        public static MapVegetationSourceSnapshot Read(IEnumerable<GameObject> explicitRoots)
        {
            if (explicitRoots == null) throw new ArgumentNullException(nameof(explicitRoots));
            var result = new MapVegetationSourceSnapshot();
            var metadata = new Dictionary<string, DonorWorldBaselineEntityMetadata>(StringComparer.Ordinal);
            foreach (GameObject root in explicitRoots)
            {
                if (root == null) continue;
                foreach (DonorWorldBaselineEntityMetadata entity in root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(true))
                {
                    if (!string.IsNullOrEmpty(entity.StableId)) metadata[entity.StableId] = entity;
                }
            }

            var cards = new List<Card>();
            var hashCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (DonorWorldBaselineEntityMetadata entity in metadata.Values.OrderBy(value => value.StableId, StringComparer.Ordinal))
            {
                string path = entity.SourceHierarchyPath ?? string.Empty;
                bool boundary = Contains(path, "/FOLIAGE/TREEWALL_");
                bool tree = !boundary && Contains(path, "/FOLIAGE/TREES");
                bool shrub = Contains(path, "/FOLIAGE/BUSHES");
                bool rock = string.Equals(entity.SemanticCategory, "Rock", StringComparison.Ordinal);
                bool canonicalRock = rock &&
                    (string.Equals(path, "ROCKS", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith("/ROCKS", StringComparison.OrdinalIgnoreCase));
                bool individual = !boundary && !tree && !shrub &&
                    string.Equals(entity.SemanticCategory, "VegetationTree", StringComparison.Ordinal) &&
                    !Contains(path, "_COLL") && !Contains(path, "treewallcoll");
                if (!boundary && !tree && !shrub && !individual && !rock) continue;
                if (rock && !canonicalRock)
                {
                    AddDiagnostic(result, "ContinuousRockSurfacePreserved",
                        "Continuous RockPale/Rockwall geometry is not an interchangeable boulder placement; its legacy renderer and exact mesh collision stay authoritative.",
                        path, entity.transform.position);
                    continue;
                }
                if (boundary && !result.BillboardSourcePaths.Contains(path)) result.BillboardSourcePaths.Add(path);

                foreach (MeshFilter filter in entity.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.GetComponentInParent<DonorWorldBaselineEntityMetadata>() != entity || filter.sharedMesh == null) continue;
                    Renderer sourceRenderer = filter.GetComponent<Renderer>();
                    if (sourceRenderer != null && sourceRenderer.enabled &&
                        sourceRenderer.gameObject.activeInHierarchy)
                    {
                        if (boundary || tree || shrub)
                            AddDiagnostic(result,
                                "ActiveLegacyVegetationRenderer",
                                "Legacy card/boundary renderer is still visible and must be retired after its replacement validates.",
                                path, filter.transform.position);
                        else if (canonicalRock)
                            AddDiagnostic(result,
                                "ActiveLegacyCanonicalRockRenderer",
                                "Canonical rock renderer remains source evidence until the renderer-only production override validates; its exact legacy collider remains authoritative.",
                                path, filter.transform.position);
                    }
                    Mesh mesh = filter.sharedMesh;
                    Vector3[] vertices;
                    int[] triangles;
                    try { vertices = mesh.vertices; triangles = mesh.triangles; }
                    catch (UnityException exception)
                    {
                        AddDiagnostic(result, "UnreadableSourceMesh", exception.Message, path, filter.transform.position);
                        continue;
                    }
                    if (vertices.Length == 0 || triangles.Length == 0) continue;
                    if (vertices.Any(value => !IsFinite(value)) || triangles.Any(index => index < 0 || index >= vertices.Length))
                    {
                        AddDiagnostic(result, "InvalidSourceGeometry", "Non-finite vertex or out-of-range triangle; source mesh was not sampled.", path, filter.transform.position);
                        continue;
                    }
                    result.SourceMeshCount++;
                    Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    if (vertices.Any(value => !IsFinite(value)))
                    {
                        AddDiagnostic(result, "InvalidSourceTransform", "Source transform produces non-finite project coordinates.", path, Vector3.zero);
                        continue;
                    }
                    string assetPath = AssetDatabase.GetAssetPath(mesh);
                    string meshId = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId)
                        ? guid + ":" + localId.ToString(CultureInfo.InvariantCulture) : mesh.name;
                    var source = new Source(entity, HashAsset(assetPath, hashCache), Family(path), shrub);
                    if (canonicalRock)
                    {
                        ReadCanonicalRocks(vertices, triangles, source, meshId,
                            result, emitted);
                        continue;
                    }
                    if (boundary)
                    {
                        ReadBoundary(vertices, triangles, source, result, emitted);
                        continue;
                    }
                    if (individual)
                    {
                        Bounds bounds = BoundsOf(vertices);
                        if (bounds.size.x > 45f || bounds.size.z > 45f || bounds.size.y < 0.3f || bounds.size.y > 90f)
                        {
                            AddDiagnostic(result, "AmbiguousIndividualTransform", "Semantic tree record has aggregate-sized or invalid geometry; transform was not used.", path, entity.transform.position);
                            continue;
                        }
                        AddPoint(result, source, "transform", entity.transform.position, bounds.size.y, "IndividualDonorTransform", emitted);
                        break;
                    }

                    List<List<int>> components = ConnectedComponents(vertices, triangles);
                    result.ConnectedComponentCount += components.Count;
                    for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
                    {
                        List<int> component = components[componentIndex];
                        string componentId = meshId + ":component:" + component[0].ToString(CultureInfo.InvariantCulture);
                        if (TryReadCard(vertices, component, source, componentId, out Card card))
                        {
                            cards.Add(card);
                            continue;
                        }
                        Bounds bounds = BoundsOf(vertices, component);
                        if (component.Count < 4 || bounds.size.y < 0.3f || bounds.size.y > 90f || bounds.size.x > 45f || bounds.size.z > 45f)
                        {
                            AddDiagnostic(result, "AmbiguousAggregateComponent", "Component cannot provide one bounded plant root and was skipped.", path, bounds.center);
                            continue;
                        }
                        Vector3 root = BottomCenter(vertices, component, bounds.min.y);
                        AddPoint(result, source, componentId, root, bounds.size.y, "GeometryComponentRootReconstructed", emitted);
                    }
                }
            }
            PairCards(cards, result, emitted);
            ResolveBoundaryOrientation(result);
            result.Trees.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            result.Shrubs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            result.Rocks.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            result.BoundarySegments.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        private static void ReadCanonicalRocks(
            Vector3[] vertices,
            int[] triangles,
            Source source,
            string meshId,
            MapVegetationSourceSnapshot result,
            HashSet<string> emitted)
        {
            List<List<int>> components = ConnectedTriangleComponents(vertices, triangles);
            if (components.Count == 0)
            {
                AddDiagnostic(result, "CanonicalRockWithoutComponents",
                    "Rock mesh has no bounded triangle components; no replacement anchor was inferred.",
                    source.Path, BoundsOf(vertices).center);
                return;
            }

            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                List<int> component = components[componentIndex];
                Bounds bounds = BoundsOf(vertices, component);
                if (component.Count < 4 ||
                    bounds.size.x < 0.08f || bounds.size.y < 0.04f ||
                    bounds.size.z < 0.08f ||
                    !IsFinite(bounds.center) || !IsFinite(bounds.size))
                {
                    AddDiagnostic(result, "DegenerateCanonicalRockComponent",
                        "Degenerate canonical rock component was skipped.",
                        source.Path, bounds.center);
                    continue;
                }

                float horizontalSpan = Mathf.Max(bounds.size.x, bounds.size.z);
                if (horizontalSpan > 14f)
                {
                    AddDiagnostic(result, "OversizedCanonicalRockPreserved",
                        "A connected rock component exceeds the reviewed 14 metre boulder envelope; it remains legacy geometry instead of being approximated by scattered prefabs.",
                        source.Path, bounds.center);
                    continue;
                }
                EmitRockAnchor(vertices, component, source,
                    meshId + ":component:" + componentIndex,
                    "CanonicalRockGeometryComponent", result, emitted);
            }
        }

        private static void EmitRockAnchor(
            Vector3[] vertices,
            List<int> component,
            Source source,
            string componentId,
            string method,
            MapVegetationSourceSnapshot result,
            HashSet<string> emitted)
        {
            string id = "canonical-rock:" + source.Id + ":" + componentId;
            if (!emitted.Add(id)) return;
            Bounds bounds = BoundsOf(vertices, component);
            Vector3 position = BottomCenter(vertices, component, bounds.min.y);
            if (!IsFinite(position)) position = new Vector3(
                bounds.center.x, bounds.min.y, bounds.center.z);
            result.Rocks.Add(new MapVegetationRockAnchor
            {
                Id = id,
                Position = position,
                SourceSize = bounds.size,
                Yaw = PrincipalYaw(vertices, component),
                CellId = CellId(position),
                ReplacementKey = source.ReplacementKey,
                SourceStableId = source.Id,
                SourcePath = source.Path,
                SourceMeshGuid = source.MeshGuid,
                SourceHash = source.Hash,
                Method = method
            });
        }

        private static float PrincipalYaw(Vector3[] vertices, List<int> component)
        {
            double meanX = 0d;
            double meanZ = 0d;
            foreach (int index in component)
            {
                meanX += vertices[index].x;
                meanZ += vertices[index].z;
            }
            meanX /= component.Count;
            meanZ /= component.Count;
            double xx = 0d;
            double zz = 0d;
            double xz = 0d;
            foreach (int index in component)
            {
                double x = vertices[index].x - meanX;
                double z = vertices[index].z - meanZ;
                xx += x * x;
                zz += z * z;
                xz += x * z;
            }
            // Principal-axis direction in XZ. The 180-degree ambiguity is
            // irrelevant for rock silhouettes and stays deterministic.
            return Mathf.Repeat(0.5f * Mathf.Atan2((float)(2d * xz),
                (float)(zz - xx)) * Mathf.Rad2Deg, 180f);
        }

        private static List<List<int>> ConnectedTriangleComponents(
            Vector3[] vertices,
            int[] triangles)
        {
            int[] parents = Enumerable.Range(0, vertices.Length).ToArray();
            var referenced = new HashSet<int>();
            var welded = new Dictionary<VertexKey, int>();
            for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = triangles[triangle + corner];
                    referenced.Add(index);
                    VertexKey key = new VertexKey(vertices[index]);
                    if (welded.TryGetValue(key, out int previous))
                        Union(parents, index, previous);
                    else
                        welded.Add(key, index);
                }
                Union(parents, triangles[triangle], triangles[triangle + 1]);
                Union(parents, triangles[triangle], triangles[triangle + 2]);
            }

            var groups = new Dictionary<int, List<int>>();
            foreach (int index in referenced.OrderBy(value => value))
            {
                int parent = Find(parents, index);
                if (!groups.TryGetValue(parent, out List<int> group))
                    groups.Add(parent, group = new List<int>());
                group.Add(index);
            }
            return groups.Values.OrderBy(group => group[0]).ToList();
        }

        private static void PairCards(List<Card> cards, MapVegetationSourceSnapshot result, HashSet<string> emitted)
        {
            cards.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var cells = new Dictionary<GridKey, List<int>>();
            for (int i = 0; i < cards.Count; i++)
            {
                GridKey key = new GridKey(cards[i].Center, PairIndexSize);
                if (!cells.TryGetValue(key, out List<int> indices)) cells.Add(key, indices = new List<int>());
                indices.Add(i);
            }
            var used = new bool[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                if (used[i]) continue;
                Card card = cards[i];
                GridKey key = new GridKey(card.Center, PairIndexSize);
                int match = -1;
                float nearest = float.PositiveInfinity;
                Vector3 root = default;
                int compatiblePartners = 0;
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue(new GridKey(key.X + x, key.Z + z), out List<int> indices)) continue;
                    foreach (int j in indices)
                    {
                        if (j == i || used[j] || cards[j].Source.Family != card.Source.Family) continue;
                        if (!TryIntersectCards(card, cards[j], out Vector3 candidate)) continue;
                        compatiblePartners++;
                        float distance = (card.Center - cards[j].Center).sqrMagnitude;
                        if (distance < nearest)
                        {
                            nearest = distance;
                            match = j;
                            root = candidate;
                        }
                    }
                }
                used[i] = true;
                if (compatiblePartners > 1)
                {
                    AddDiagnostic(result, "MultipleCompatibleCardPartners", compatiblePartners + " geometry-compatible partners; nearest bottom-edge pair selected deterministically. Review this ambiguous original location.", card.Source.Path, card.Center);
                }
                if (match >= 0)
                {
                    used[match] = true;
                    result.PairedCardCount += 2;
                    Card other = cards[match];
                    AddPoint(result, card.Source, card.Key + "+" + other.Key, root,
                        Mathf.Max(card.Height, other.Height), "CrossedCardRootReconstructed", emitted, other.Source);
                }
                else
                {
                    result.UnpairedCardCount++;
                    // A single billboard still proves a root on its bottom edge, but
                    // not an original serialized transform or a paired-card intersection.
                    AddPoint(result, card.Source, card.Key, card.Center, card.Height,
                        "SingleCardRootReconstructed", emitted);
                    AddDiagnostic(result, "UnpairedSourceCard", "No compatible perpendicular partner. Kept its exact bottom-edge midpoint as lower-confidence evidence; no random displacement.", card.Source.Path, card.Center);
                }
            }
        }

        private static bool TryIntersectCards(Card a, Card b, out Vector3 root)
        {
            root = default;
            if (Mathf.Abs(a.Center.y - b.Center.y) > VerticalTolerance || Mathf.Abs(a.Height - b.Height) > VerticalTolerance) return false;
            Vector2 p = Xz(a.A), q = Xz(b.A), u = Xz(a.B - a.A), v = Xz(b.B - b.A);
            float lengthA = u.magnitude, lengthB = v.magnitude;
            if (lengthA < 0.1f || lengthB < 0.1f || Mathf.Abs(Vector2.Dot(u / lengthA, v / lengthB)) > 0.2f) return false;
            if (Vector2.Distance(Xz(a.Center), Xz(b.Center)) > Mathf.Min(lengthA, lengthB) * 0.25f) return false;
            float denominator = Cross(u, v);
            float t = Cross(q - p, v) / denominator;
            float s = Cross(q - p, u) / denominator;
            if (t < 0.25f || t > 0.75f || s < 0.25f || s > 0.75f) return false;
            Vector2 intersection = p + t * u;
            float yA = Mathf.LerpUnclamped(a.A.y, a.B.y, t);
            float yB = Mathf.LerpUnclamped(b.A.y, b.B.y, s);
            root = new Vector3(intersection.x, (yA + yB) * 0.5f, intersection.y);
            return true;
        }

        private static bool TryReadCard(Vector3[] vertices, List<int> component, Source source, string id, out Card card)
        {
            card = null;
            if (component.Count != 4) return false;
            Bounds bounds = BoundsOf(vertices, component);
            if (bounds.size.y < 0.3f || bounds.size.y > 90f) return false;
            // Donor cards can have terrain-conforming sloped bottom/top edges.
            // Selecting only vertices near global min-Y would select one corner
            // and move the inferred trunk sideways by half the card width.
            Vector3[] ordered = component.Select(index => vertices[index]).OrderBy(value => value.y).ToArray();
            Vector3[] bottom = { ordered[0], ordered[1] };
            Vector3[] top = { ordered[2], ordered[3] };
            if (ordered[2].y - ordered[1].y < 0.3f ||
                ordered[1].y - ordered[0].y > bounds.size.y * 0.3f ||
                ordered[3].y - ordered[2].y > bounds.size.y * 0.3f ||
                Vector2.Distance(Xz(bottom[0]), Xz(bottom[1])) > 45f) return false;
            foreach (Vector3 upper in top)
            {
                if (Mathf.Min(Vector2.Distance(Xz(upper), Xz(bottom[0])), Vector2.Distance(Xz(upper), Xz(bottom[1]))) > 0.05f) return false;
            }
            float height = (top[0].y + top[1].y - bottom[0].y - bottom[1].y) * 0.5f;
            card = new Card(source, id, bottom[0], bottom[1], height);
            return true;
        }

        private static List<List<int>> ConnectedComponents(Vector3[] vertices, int[] triangles)
        {
            int[] parents = new int[vertices.Length];
            var welded = new Dictionary<VertexKey, int>();
            for (int i = 0; i < parents.Length; i++)
            {
                parents[i] = i;
                VertexKey key = new VertexKey(vertices[i]);
                if (welded.TryGetValue(key, out int previous)) Union(parents, i, previous);
                else welded.Add(key, i);
            }
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Union(parents, triangles[i], triangles[i + 1]);
                Union(parents, triangles[i], triangles[i + 2]);
            }
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < parents.Length; i++)
            {
                int parent = Find(parents, i);
                if (!groups.TryGetValue(parent, out List<int> group)) groups.Add(parent, group = new List<int>());
                group.Add(i);
            }
            return groups.Values.OrderBy(value => value[0]).ToList();
        }

        private static void ReadBoundary(Vector3[] vertices, int[] triangles, Source source, MapVegetationSourceSnapshot result, HashSet<string> emitted)
        {
            // Quantization only identifies coincident vertices; output endpoints
            // retain the original floating-point coordinates without snapping.
            var columns = new Dictionary<GridKey, Vector2>();
            foreach (Vector3 vertex in vertices)
            {
                GridKey key = new GridKey(vertex, 0.02f);
                if (columns.TryGetValue(key, out Vector2 range)) columns[key] = new Vector2(Mathf.Min(range.x, vertex.y), Mathf.Max(range.y, vertex.y));
                else columns.Add(key, new Vector2(vertex.y, vertex.y));
            }
            var edges = new Dictionary<EdgeKey, Edge>();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 adjacentNormal = Vector3.Cross(b - a, c - a);
                if (IsFinite(adjacentNormal) && adjacentNormal.sqrMagnitude >
                    0.000001f)
                    adjacentNormal.Normalize();
                else
                    adjacentNormal = Vector3.zero;
                AddEdge(edges, a, b, adjacentNormal);
                AddEdge(edges, b, c, adjacentNormal);
                AddEdge(edges, c, a, adjacentNormal);
            }
            int before = result.BoundarySegments.Count;
            foreach (Edge edge in edges.Values)
            {
                Vector2 rangeA = columns[new GridKey(edge.A, 0.02f)], rangeB = columns[new GridKey(edge.B, 0.02f)];
                if (edge.Count != 1 || Vector2.Distance(Xz(edge.A), Xz(edge.B)) < 0.2f ||
                    rangeA.y - rangeA.x < 1f || rangeB.y - rangeB.x < 1f ||
                    edge.A.y > rangeA.x + VerticalTolerance || edge.B.y > rangeB.x + VerticalTolerance) continue;
                string id = source.Id + ":boundary:" + PositionId(edge.A) + ":" + PositionId(edge.B);
                if (!emitted.Add(id)) continue;
                result.BoundarySegments.Add(new MapVegetationBoundarySegment
                {
                    Id = id, A = edge.A, B = edge.B,
                    AdjacentTriangleNormal = edge.AdjacentNormal,
                    SourceStableId = source.Id,
                    SourcePath = source.Path, SourceMeshGuid = source.MeshGuid, SourceHash = source.Hash
                });
            }
            if (result.BoundarySegments.Count == before)
                AddDiagnostic(result, "BoundaryWithoutLowerEdges", "No unambiguous lower open edges found; no inferred rectangular boundary was substituted.", source.Path, BoundsOf(vertices).center);
        }

        private static void AddEdge(Dictionary<EdgeKey, Edge> edges,
            Vector3 a, Vector3 b, Vector3 adjacentNormal)
        {
            EdgeKey key = new EdgeKey(a, b);
            if (edges.TryGetValue(key, out Edge edge)) edge.Count++;
            else edges.Add(key, new Edge(a, b, adjacentNormal));
        }

        private static void ResolveBoundaryOrientation(
            MapVegetationSourceSnapshot result)
        {
            Vector3[] donorTrees = result.Trees.Select(tree => tree.Position)
                .ToArray();
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure> enclosures;
            try
            {
                enclosures = MapVegetationPlanning.BuildBoundaryEnclosures(
                    result.BoundarySegments);
            }
            catch (Exception exception)
            {
                foreach (MapVegetationBoundarySegment segment in
                         result.BoundarySegments)
                {
                    segment.HasValidatedOutward = false;
                    segment.OrientationMethod = "AmbiguousTopology:" +
                                                exception.Message;
                    AddDiagnostic(result, "AmbiguousBoundaryTopology",
                        exception.Message, segment.SourcePath,
                        (segment.A + segment.B) * 0.5f);
                }
                return;
            }

            foreach (MapVegetationPlanning.BoundaryEnclosure enclosure in
                     enclosures)
            {
                int inside = donorTrees.Count(tree => enclosure.Contains(
                    new Vector2(tree.x, tree.z)));
                int outside = donorTrees.Length - inside;
                float confidence = donorTrees.Length == 0 ? 0f :
                    (float)inside / donorTrees.Length;
                bool donorSideValidated = donorTrees.Length >= 100 &&
                                          confidence >= 0.72f;
                foreach (MapVegetationPlanning.OrientedBoundarySegment item in
                         enclosure.Segments)
                {
                    if (item.IsSyntheticClosure)
                        continue;
                    MapVegetationBoundarySegment segment = item.Segment;
                    Vector2 normal = new Vector2(
                        segment.AdjacentTriangleNormal.x,
                        segment.AdjacentTriangleNormal.z);
                    Vector2 outward = item.Outward;
                    bool trianglePlaneValidated =
                        normal.sqrMagnitude >= 0.25f &&
                        Mathf.Abs(Vector2.Dot(normal.normalized,
                            outward)) >= 0.7f;
                    segment.Outward = new Vector3(outward.x, 0f,
                        outward.y);
                    segment.HasValidatedOutward = donorSideValidated &&
                                                   trianglePlaneValidated;
                    segment.EnclosureId = enclosure.Id;
                    segment.IsOuterEnvelope = enclosure.IsOuterEnvelope;
                    segment.EnclosureClosedBySmallGap =
                        enclosure.ClosedBySmallGap;
                    segment.EnclosureSignedArea = enclosure.SignedArea;
                    segment.EnclosurePerimeter = enclosure.Perimeter;
                    segment.PositiveTreeSideEvidenceCount = inside;
                    segment.NegativeTreeSideEvidenceCount = outside;
                    segment.TreeSideConfidence = confidence;
                    segment.OrientationMethod =
                        segment.HasValidatedOutward
                            ? (enclosure.ClosedBySmallGap
                                ? "SmallGapClosedPolygonOutwardValidatedByAdjacentTrianglePlaneAndDonorTreeContainment"
                                : "ClosedPolygonOutwardValidatedByAdjacentTrianglePlaneAndDonorTreeContainment")
                            : !donorSideValidated
                                ? "Ambiguous:DonorTreesDoNotValidatePolygonInterior"
                                : "Ambiguous:AdjacentTriangleNormalDoesNotValidatePolygonPlane";
                    if (!segment.HasValidatedOutward)
                        AddDiagnostic(result, "AmbiguousBoundaryOutward",
                            segment.OrientationMethod, segment.SourcePath,
                            (segment.A + segment.B) * 0.5f);
                }
            }
        }

        private static void AddPoint(MapVegetationSourceSnapshot result, Source source, string componentId, Vector3 position, float height, string method, HashSet<string> emitted, Source secondary = null)
        {
            string id = source.Id + ":" + componentId;
            if (!emitted.Add(id)) return;
            var point = new MapVegetationSourcePoint
            {
                Id = id, Position = position, CellId = CellId(position), SourceStableId = source.Id,
                SourcePath = source.Path, SourceMeshGuid = source.MeshGuid, SourceHash = source.Hash,
                SecondarySourceStableId = secondary?.Id, SecondarySourceMeshGuid = secondary?.MeshGuid,
                SecondarySourceHash = secondary?.Hash,
                Method = method, SourceFamily = source.Family, Height = height
            };
            (source.Shrub ? result.Shrubs : result.Trees).Add(point);
        }

        private static void AddDiagnostic(MapVegetationSourceSnapshot result, string code, string message, string path, Vector3 position)
        {
            result.Diagnostics.Add(new MapVegetationSourceDiagnostic { Code = code, Message = message, SourcePath = path, Position = position, CellId = CellId(position) });
        }

        private static string HashAsset(string path, Dictionary<string, string> cache)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (!Path.IsPathRooted(path)) path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (!File.Exists(path)) return string.Empty;
            if (cache.TryGetValue(path, out string hash)) return hash;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            cache.Add(path, hash);
            return hash;
        }

        private static string Family(string path)
        {
            if (Contains(path, "/BUSHES")) return "DonorShrubAtlas";
            if (Contains(path, "/TREES_SMALL")) return "DonorSmallTreeAtlas";
            if (Contains(path, "/TREES_MEDIUM")) return "DonorMediumTreeAtlas";
            return "DonorLargeTreeAtlas";
        }

        private static Bounds BoundsOf(Vector3[] vertices, List<int> indices = null)
        {
            Bounds bounds = new Bounds(vertices[indices == null ? 0 : indices[0]], Vector3.zero);
            if (indices == null) { foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex); }
            else { foreach (int index in indices) bounds.Encapsulate(vertices[index]); }
            return bounds;
        }

        private static Vector3 BottomCenter(Vector3[] vertices, List<int> component, float minY)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, minY, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, minY, float.NegativeInfinity);
            foreach (int i in component)
            {
                if (vertices[i].y > minY + VerticalTolerance) continue;
                min.x = Mathf.Min(min.x, vertices[i].x); min.z = Mathf.Min(min.z, vertices[i].z);
                max.x = Mathf.Max(max.x, vertices[i].x); max.z = Mathf.Max(max.z, vertices[i].z);
            }
            return (min + max) * 0.5f;
        }

        private static int Find(int[] parents, int i) { while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; } return i; }
        private static void Union(int[] parents, int a, int b) { a = Find(parents, a); b = Find(parents, b); if (a != b) parents[Math.Max(a, b)] = Math.Min(a, b); }
        private static bool Contains(string value, string token) => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private static Vector2 Xz(Vector3 value) => new Vector2(value.x, value.z);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static string CellId(Vector3 position) => "cell_" + Mathf.FloorToInt(position.x / 512f) + "_" + Mathf.FloorToInt(position.z / 512f);
        private static string PositionId(Vector3 value) => value.x.ToString("R", CultureInfo.InvariantCulture) + "," + value.y.ToString("R", CultureInfo.InvariantCulture) + "," + value.z.ToString("R", CultureInfo.InvariantCulture);

        private sealed class Source
        {
            public readonly string Id, Path, MeshGuid, Hash, Family,
                ReplacementKey;
            public readonly bool Shrub;
            public Source(DonorWorldBaselineEntityMetadata entity, string hash, string family, bool shrub)
            {
                Id = entity.StableId;
                Path = entity.SourceHierarchyPath;
                MeshGuid = entity.SourceMeshGuid;
                Hash = hash;
                Family = family;
                ReplacementKey = entity.ReplacementKey;
                Shrub = shrub;
            }
        }

        private sealed class Card
        {
            public readonly Source Source;
            public readonly string Key;
            public readonly Vector3 A, B, Center;
            public readonly float Height;
            public Card(Source source, string id, Vector3 a, Vector3 b, float height)
            { Source = source; Key = source.Id + ":" + id; A = a; B = b; Center = (a + b) * 0.5f; Height = height; }
        }

        private sealed class Edge
        {
            public readonly Vector3 A, B;
            public readonly Vector3 AdjacentNormal;
            public int Count = 1;
            public Edge(Vector3 a, Vector3 b, Vector3 adjacentNormal)
            {
                A = a;
                B = b;
                AdjacentNormal = adjacentNormal;
            }
        }

        private readonly struct GridKey : IEquatable<GridKey>
        {
            public readonly int X, Z;
            public GridKey(Vector3 point, float size) { X = Mathf.FloorToInt(point.x / size); Z = Mathf.FloorToInt(point.z / size); }
            public GridKey(int x, int z) { X = x; Z = z; }
            public bool Equals(GridKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is GridKey other && Equals(other);
            public override int GetHashCode() => unchecked(X * 397 ^ Z);
        }

        private readonly struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
        {
            private readonly long x, y, z;
            public VertexKey(Vector3 point) { x = (long)Math.Round(point.x * 10000d); y = (long)Math.Round(point.y * 10000d); z = (long)Math.Round(point.z * 10000d); }
            public bool Equals(VertexKey other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode() => unchecked((x.GetHashCode() * 397 ^ y.GetHashCode()) * 397 ^ z.GetHashCode());
            public int CompareTo(VertexKey other) { int c = x.CompareTo(other.x); if (c != 0) return c; c = y.CompareTo(other.y); return c != 0 ? c : z.CompareTo(other.z); }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly VertexKey a, b;
            public EdgeKey(Vector3 first, Vector3 second)
            { var x = new VertexKey(first); var y = new VertexKey(second); if (x.CompareTo(y) <= 0) { a = x; b = y; } else { a = y; b = x; } }
            public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked(a.GetHashCode() * 397 ^ b.GetHashCode());
        }
    }
}
