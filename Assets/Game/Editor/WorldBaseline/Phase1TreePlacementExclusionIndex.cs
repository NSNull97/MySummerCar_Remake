using System;
using System.Collections.Generic;
using MSC.LegacyImport;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    internal sealed class Phase1TreePlacementExclusionIndex
    {
        internal const float RoadClearanceMeters = 4.75f;
        internal const float BuildingClearanceMeters = 3.5f;
        internal const float WaterClearanceMeters = 3.5f;

        private const float IndexCellSizeMeters = 32f;
        private const float MinimumTriangleArea = 0.001f;
        private const float MaximumFallbackBoundsArea = 16000f;
        private const float BuildingFallbackClearanceMeters = 1.5f;
        private const float MaximumBuildingBoundsArea = 18000f;
        private const float MaximumBuildingBoundsSpan = 180f;

        private readonly Dictionary<CellKey, List<Footprint>> footprintsByCell =
            new Dictionary<CellKey, List<Footprint>>();

        private Phase1TreePlacementExclusionIndex()
        {
        }

        public int FootprintCount { get; private set; }
        public int RoadTriangleCount { get; private set; }
        public int BuildingTriangleCount { get; private set; }
        public int BuildingBoundsCount { get; private set; }
        public int WaterTriangleCount { get; private set; }
        public int OversizedBuildingBoundsSkipped { get; private set; }
        public double BuildingProjectedAreaSum { get; private set; }
        public float LargestBuildingBoundsArea { get; private set; }
        public float LargestBuildingBoundsSpan { get; private set; }
        public string LargestBuildingBoundsSource { get; private set; } =
            string.Empty;
        public int BuildingBoundsOutsideSourceCell { get; private set; }
        public string FirstOutsideSourceCellSource { get; private set; } =
            string.Empty;
        public int SupplementalEntityCount { get; private set; }
        public int SupplementalTriangleCount { get; private set; }
        public int SupplementalBoundsCount { get; private set; }

        public static Phase1TreePlacementExclusionIndex Build(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadataItems)
        {
            if (metadataItems == null)
            {
                throw new ArgumentNullException(nameof(metadataItems));
            }

            var result = new Phase1TreePlacementExclusionIndex();
            result.Add(metadataItems);
            return result;
        }

        public void Add(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadataItems)
        {
            if (metadataItems == null)
            {
                throw new ArgumentNullException(nameof(metadataItems));
            }

            foreach (DonorWorldBaselineEntityMetadata metadata in metadataItems)
            {
                if (metadata == null)
                {
                    continue;
                }

                ExclusionKind kind = Classify(
                    metadata.SemanticCategory,
                    metadata.SourceHierarchyPath);
                if (kind == ExclusionKind.None)
                {
                    continue;
                }

                int trianglesBefore = RoadTriangleCount +
                                      BuildingTriangleCount +
                                      WaterTriangleCount;
                foreach (MeshFilter filter in metadata
                             .GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!IsOwnedBy(filter.transform, metadata))
                    {
                        continue;
                    }
                    AddSurfaceTriangles(
                        filter,
                        kind,
                        metadata.SourceHierarchyPath);
                }

                int trianglesAfter = RoadTriangleCount +
                                     BuildingTriangleCount +
                                     WaterTriangleCount;
                if (trianglesAfter == trianglesBefore)
                {
                    AddBoundedFallback(metadata, kind);
                }
            }
        }

        public void Add(
            IEnumerable<DonorWorldSupplementalEntityMetadata> metadataItems)
        {
            if (metadataItems == null)
            {
                throw new ArgumentNullException(nameof(metadataItems));
            }

            foreach (DonorWorldSupplementalEntityMetadata metadata in
                     metadataItems)
            {
                if (metadata == null ||
                    !IsJobLocationSupplemental(
                        metadata.ManifestId,
                        metadata.SourceHierarchyPath))
                {
                    continue;
                }

                SupplementalEntityCount++;
                int trianglesBefore = BuildingTriangleCount;
                int footprintsBefore = FootprintCount;
                foreach (MeshFilter filter in metadata
                             .GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!IsOwnedBy(filter.transform, metadata))
                    {
                        continue;
                    }
                    AddSurfaceTriangles(
                        filter,
                        ExclusionKind.Building,
                        metadata.SourceHierarchyPath);
                }

                int addedTriangles =
                    BuildingTriangleCount - trianglesBefore;
                SupplementalTriangleCount += addedTriangles;
                if (addedTriangles == 0)
                {
                    AddSupplementalFallback(metadata);
                    SupplementalBoundsCount +=
                        FootprintCount - footprintsBefore;
                }
            }
        }

        public bool Blocks(Vector3 worldPosition)
        {
            return TryGetBlockingInfo(
                worldPosition,
                out _,
                out _);
        }

        public bool TryGetBlockingInfo(
            Vector3 worldPosition,
            out string kind,
            out string source)
        {
            kind = string.Empty;
            source = string.Empty;
            CellKey key = CellKey.From(worldPosition);
            if (!footprintsByCell.TryGetValue(
                    key,
                    out List<Footprint> footprints))
            {
                return false;
            }

            Vector2 point = new Vector2(worldPosition.x, worldPosition.z);
            for (int index = 0; index < footprints.Count; index++)
            {
                if (footprints[index].Contains(point))
                {
                    kind = footprints[index].Kind.ToString();
                    source = footprints[index].Source;
                    return true;
                }
            }
            return false;
        }

        internal static bool IsPointWithinTriangleClearance(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float clearanceMeters)
        {
            if (PointInTriangle(point, a, b, c))
            {
                return true;
            }

            float clearanceSquared = clearanceMeters * clearanceMeters;
            return DistanceSquaredToSegment(point, a, b) <= clearanceSquared ||
                   DistanceSquaredToSegment(point, b, c) <= clearanceSquared ||
                   DistanceSquaredToSegment(point, c, a) <= clearanceSquared;
        }

        internal static string ClassifyForTests(
            string semanticCategory,
            string sourceHierarchyPath)
        {
            return Classify(semanticCategory, sourceHierarchyPath).ToString();
        }

        internal static string ClassifySupplementalForTests(
            string manifestId,
            string sourceHierarchyPath)
        {
            return IsJobLocationSupplemental(
                    manifestId,
                    sourceHierarchyPath)
                ? ExclusionKind.Building.ToString()
                : ExclusionKind.None.ToString();
        }

        private void RecordSourceCellAlignment(
            Bounds bounds,
            DonorWorldBaselineEntityMetadata metadata)
        {
            if (!TryParseCellId(
                    metadata.SourceCellId,
                    out int cellX,
                    out int cellZ))
            {
                return;
            }

            const float cellSize = 512f;
            const float tolerance = 2f;
            float minimumX = cellX * cellSize - tolerance;
            float maximumX = (cellX + 1) * cellSize + tolerance;
            float minimumZ = cellZ * cellSize - tolerance;
            float maximumZ = (cellZ + 1) * cellSize + tolerance;
            Vector3 center = bounds.center;
            if (center.x >= minimumX && center.x <= maximumX &&
                center.z >= minimumZ && center.z <= maximumZ)
            {
                return;
            }

            BuildingBoundsOutsideSourceCell++;
            if (string.IsNullOrEmpty(FirstOutsideSourceCellSource))
            {
                FirstOutsideSourceCellSource =
                    (metadata.SourceHierarchyPath ?? string.Empty) +
                    " center=" +
                    center +
                    " sourceCell=" +
                    metadata.SourceCellId;
            }
        }

        private static bool TryParseCellId(
            string value,
            out int x,
            out int z)
        {
            x = 0;
            z = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string[] parts = value.Split('_');
            return parts.Length == 3 &&
                   string.Equals(
                       parts[0],
                       "cell",
                       StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(parts[1], out x) &&
                   int.TryParse(parts[2], out z);
        }

        private void AddSurfaceTriangles(
            MeshFilter filter,
            ExclusionKind kind,
            string source)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices;
            int[] triangles;
            try
            {
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning(
                    "Skipping non-readable tree exclusion mesh '" +
                    mesh.name +
                    "': " +
                    exception.Message);
                return;
            }

            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            float clearance = ClearanceFor(kind);
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 worldA = matrix.MultiplyPoint3x4(
                    vertices[triangles[index]]);
                Vector3 worldB = matrix.MultiplyPoint3x4(
                    vertices[triangles[index + 1]]);
                Vector3 worldC = matrix.MultiplyPoint3x4(
                    vertices[triangles[index + 2]]);
                Vector2 a = new Vector2(worldA.x, worldA.z);
                Vector2 b = new Vector2(worldB.x, worldB.z);
                Vector2 c = new Vector2(worldC.x, worldC.z);
                if (Mathf.Abs(Cross(b - a, c - a)) < MinimumTriangleArea)
                {
                    continue;
                }

                AddFootprint(
                    Footprint.Triangle(
                        a,
                        b,
                        c,
                        clearance,
                        kind,
                        source));
                if (kind == ExclusionKind.Water)
                {
                    WaterTriangleCount++;
                }
                else if (kind == ExclusionKind.Building)
                {
                    BuildingTriangleCount++;
                }
                else
                {
                    RoadTriangleCount++;
                }
            }
        }

        private void AddBoundedFallback(
            DonorWorldBaselineEntityMetadata metadata,
            ExclusionKind kind)
        {
            float clearance = kind == ExclusionKind.Building
                ? BuildingFallbackClearanceMeters
                : ClearanceFor(kind);
            foreach (Renderer renderer in metadata
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (!IsOwnedBy(renderer.transform, metadata))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (kind == ExclusionKind.Building)
                {
                    RecordSourceCellAlignment(bounds, metadata);
                }
                float projectedArea = bounds.size.x * bounds.size.z;
                if (projectedArea <= MaximumFallbackBoundsArea)
                {
                    AddBoundsFootprint(
                        bounds,
                        clearance,
                        kind,
                        metadata.SourceHierarchyPath);
                }
            }

            foreach (Collider collider in metadata
                         .GetComponentsInChildren<Collider>(true))
            {
                if (!IsOwnedBy(collider.transform, metadata))
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (kind == ExclusionKind.Building)
                {
                    RecordSourceCellAlignment(bounds, metadata);
                }
                float projectedArea = bounds.size.x * bounds.size.z;
                if (projectedArea <= MaximumFallbackBoundsArea)
                {
                    AddBoundsFootprint(
                        bounds,
                        clearance,
                        kind,
                        metadata.SourceHierarchyPath);
                }
            }
        }

        private void AddSupplementalFallback(
            DonorWorldSupplementalEntityMetadata metadata)
        {
            foreach (Renderer renderer in metadata
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (!IsOwnedBy(renderer.transform, metadata))
                {
                    continue;
                }
                AddBoundsFootprint(
                    renderer.bounds,
                    BuildingFallbackClearanceMeters,
                    ExclusionKind.Building,
                    metadata.SourceHierarchyPath);
            }

            foreach (Collider collider in metadata
                         .GetComponentsInChildren<Collider>(true))
            {
                if (!IsOwnedBy(collider.transform, metadata))
                {
                    continue;
                }
                AddBoundsFootprint(
                    collider.bounds,
                    BuildingFallbackClearanceMeters,
                    ExclusionKind.Building,
                    metadata.SourceHierarchyPath);
            }
        }

        private static float ClearanceFor(ExclusionKind kind)
        {
            switch (kind)
            {
                case ExclusionKind.Building:
                    return BuildingClearanceMeters;
                case ExclusionKind.Water:
                    return WaterClearanceMeters;
                default:
                    return RoadClearanceMeters;
            }
        }

        private void AddBoundsFootprint(
            Bounds bounds,
            float clearanceMeters,
            ExclusionKind kind,
            string source = "")
        {
            if (!IsFinite(bounds.min) ||
                !IsFinite(bounds.max) ||
                bounds.size.x <= 0.001f ||
                bounds.size.z <= 0.001f)
            {
                return;
            }
            if (kind == ExclusionKind.Building &&
                (bounds.size.x * bounds.size.z >
                     MaximumBuildingBoundsArea ||
                 Mathf.Max(bounds.size.x, bounds.size.z) >
                     MaximumBuildingBoundsSpan))
            {
                OversizedBuildingBoundsSkipped++;
                return;
            }
            if (kind == ExclusionKind.Building)
            {
                float area = bounds.size.x * bounds.size.z;
                float span = Mathf.Max(bounds.size.x, bounds.size.z);
                BuildingProjectedAreaSum += area;
                if (area > LargestBuildingBoundsArea)
                {
                    LargestBuildingBoundsArea = area;
                    LargestBuildingBoundsSpan = span;
                    LargestBuildingBoundsSource = source ?? string.Empty;
                }
            }

            AddFootprint(Footprint.Rectangle(
                new Vector2(bounds.min.x, bounds.min.z),
                new Vector2(bounds.max.x, bounds.max.z),
                clearanceMeters,
                kind,
                source));
            if (kind == ExclusionKind.Building)
            {
                BuildingBoundsCount++;
            }
        }

        private void AddFootprint(Footprint footprint)
        {
            int minimumCellX = Mathf.FloorToInt(
                footprint.Minimum.x / IndexCellSizeMeters);
            int maximumCellX = Mathf.FloorToInt(
                footprint.Maximum.x / IndexCellSizeMeters);
            int minimumCellZ = Mathf.FloorToInt(
                footprint.Minimum.y / IndexCellSizeMeters);
            int maximumCellZ = Mathf.FloorToInt(
                footprint.Maximum.y / IndexCellSizeMeters);
            for (int x = minimumCellX; x <= maximumCellX; x++)
            {
                for (int z = minimumCellZ; z <= maximumCellZ; z++)
                {
                    var key = new CellKey(x, z);
                    if (!footprintsByCell.TryGetValue(
                            key,
                            out List<Footprint> footprints))
                    {
                        footprints = new List<Footprint>();
                        footprintsByCell.Add(key, footprints);
                    }
                    footprints.Add(footprint);
                }
            }
            FootprintCount++;
        }

        private static ExclusionKind Classify(
            string semanticCategory,
            string sourceHierarchyPath)
        {
            string category = semanticCategory ?? string.Empty;
            string path = sourceHierarchyPath ?? string.Empty;
            if (string.Equals(
                    category,
                    "Water",
                    StringComparison.OrdinalIgnoreCase) &&
                IsTreeBlockingWaterSurface(path))
            {
                return ExclusionKind.Water;
            }
            if (string.Equals(
                    category,
                    "Road",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "RoadShoulder",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Driveway",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Bridge",
                    StringComparison.OrdinalIgnoreCase) ||
                ContainsAny(
                    path,
                    "/TERRAIN_OBJ/ROAD",
                    "/TERRAIN_OBJ/DIRTROAD",
                    "/TERRAIN_OBJ/ASPHALT",
                    "/BRIDGE",
                    "/RAILROAD"))
            {
                return ExclusionKind.Road;
            }
            if (string.Equals(
                    category,
                    "Roof",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Floor",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Door",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Window",
                    StringComparison.OrdinalIgnoreCase) ||
                IsStructuralBuildingEntity(category, path))
            {
                return ExclusionKind.Building;
            }
            return ExclusionKind.None;
        }

        private static bool IsTreeBlockingWaterSurface(string path)
        {
            return !ContainsAny(
                path,
                "WATERUNDER",
                "WATERCOLOR",
                "LAKEBED",
                "LAKESMALLBOTTOM",
                "/FOLIAGE/",
                "LAKE_VEGETATION");
        }

        private static bool IsStructuralBuildingEntity(
            string category,
            string path)
        {
            if (!string.Equals(
                    category,
                    "BuildingExterior",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    category,
                    "BuildingInterior",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int separator = path.LastIndexOf('/');
            string leafName = separator >= 0
                ? path.Substring(separator + 1)
                : path;
            return ContainsAny(
                leafName,
                "building",
                "house",
                "garage",
                "shed",
                "barn",
                "cabin",
                "cottage",
                "church",
                "school",
                "warehouse",
                "workshop",
                "facility",
                "station",
                "office",
                "roof",
                "floor",
                "wall",
                "foundation",
                "ceiling");
        }

        private static bool IsJobLocationSupplemental(
            string manifestId,
            string sourceHierarchyPath)
        {
            if (!string.IsNullOrEmpty(manifestId) &&
                manifestId.StartsWith(
                    "phase1-job-location-presentation-",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string path = sourceHierarchyPath ?? string.Empty;
            return ContainsAny(
                path,
                "JOBS/StrawberryField/",
                "JOBS/HouseShit1/",
                "JOBS/HouseShit2/",
                "JOBS/HouseShit3/",
                "JOBS/HouseShit4/",
                "JOBS/HouseShit5/",
                "JOBS/Farm/");
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
            {
                if (value.IndexOf(
                        tokens[index],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOwnedBy(
            Transform transform,
            DonorWorldBaselineEntityMetadata expectedOwner)
        {
            DonorWorldBaselineEntityMetadata owner =
                transform.GetComponentInParent<
                    DonorWorldBaselineEntityMetadata>();
            return owner == expectedOwner;
        }

        private static bool IsOwnedBy(
            Transform transform,
            DonorWorldSupplementalEntityMetadata expectedOwner)
        {
            DonorWorldSupplementalEntityMetadata owner =
                transform.GetComponentInParent<
                    DonorWorldSupplementalEntityMetadata>();
            return owner == expectedOwner;
        }

        private static bool PointInTriangle(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            float first = Cross(b - a, point - a);
            float second = Cross(c - b, point - b);
            float third = Cross(a - c, point - c);
            bool hasNegative = first < 0f || second < 0f || third < 0f;
            bool hasPositive = first > 0f || second > 0f || third > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float DistanceSquaredToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return (point - start).sqrMagnitude;
            }
            float t = Mathf.Clamp01(
                Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 closest = start + segment * t;
            return (point - closest).sqrMagnitude;
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        internal enum ExclusionKind
        {
            None,
            Road,
            Building,
            Water
        }

        private readonly struct CellKey : IEquatable<CellKey>
        {
            public CellKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public static CellKey From(Vector3 position)
            {
                return new CellKey(
                    Mathf.FloorToInt(position.x / IndexCellSizeMeters),
                    Mathf.FloorToInt(position.z / IndexCellSizeMeters));
            }

            public bool Equals(CellKey other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is CellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }

        private readonly struct Footprint
        {
            private Footprint(
                Vector2 a,
                Vector2 b,
                Vector2 c,
                Vector2 minimum,
                Vector2 maximum,
                float clearanceMeters,
                bool isTriangle,
                ExclusionKind kind,
                string source)
            {
                A = a;
                B = b;
                C = c;
                Minimum = minimum;
                Maximum = maximum;
                ClearanceMeters = clearanceMeters;
                IsTriangle = isTriangle;
                Kind = kind;
                Source = source ?? string.Empty;
            }

            private Vector2 A { get; }
            private Vector2 B { get; }
            private Vector2 C { get; }
            public Vector2 Minimum { get; }
            public Vector2 Maximum { get; }
            private float ClearanceMeters { get; }
            private bool IsTriangle { get; }
            public ExclusionKind Kind { get; }
            public string Source { get; }

            public static Footprint Triangle(
                Vector2 a,
                Vector2 b,
                Vector2 c,
                float clearanceMeters,
                ExclusionKind kind,
                string source)
            {
                Vector2 minimum = Vector2.Min(a, Vector2.Min(b, c)) -
                                  Vector2.one * clearanceMeters;
                Vector2 maximum = Vector2.Max(a, Vector2.Max(b, c)) +
                                  Vector2.one * clearanceMeters;
                return new Footprint(
                    a,
                    b,
                    c,
                    minimum,
                    maximum,
                    clearanceMeters,
                    true,
                    kind,
                    source);
            }

            public static Footprint Rectangle(
                Vector2 minimum,
                Vector2 maximum,
                float clearanceMeters,
                ExclusionKind kind,
                string source)
            {
                return new Footprint(
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    minimum - Vector2.one * clearanceMeters,
                    maximum + Vector2.one * clearanceMeters,
                    0f,
                    false,
                    kind,
                    source);
            }

            public bool Contains(Vector2 point)
            {
                if (point.x < Minimum.x ||
                    point.x > Maximum.x ||
                    point.y < Minimum.y ||
                    point.y > Maximum.y)
                {
                    return false;
                }
                return !IsTriangle ||
                       IsPointWithinTriangleClearance(
                           point,
                           A,
                           B,
                           C,
                           ClearanceMeters);
            }
        }
    }
}
