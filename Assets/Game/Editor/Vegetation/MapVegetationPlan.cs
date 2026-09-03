using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.World.Partition;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    [Serializable]
    public sealed class MapVegetationPlacement
    {
        public string id, source, method, species, cellId;
        public MapVegetationCategories category;
        public Vector3 sourcePosition, position, normal;
        public float height, yaw, variation;
        public int profile;
    }

    [Serializable]
    public sealed class MapVegetationIssue
    {
        public string id, cellId, category, reason, source;
        public Vector3 position;
    }

    public sealed class MapVegetationCellPlan
    {
        public WorldCellIndex Cell;
        public readonly List<MapVegetationPlacement> Woody = new List<MapVegetationPlacement>();
        public readonly List<MapVegetationPlacement> Grass = new List<MapVegetationPlacement>();
        public readonly List<MapVegetationIssue> Issues = new List<MapVegetationIssue>();
        public readonly Dictionary<string, int> Rejections = new Dictionary<string, int>(StringComparer.Ordinal);
        public int GrassCandidates;
        public string SettingsFingerprint;
        public string Fingerprint;

        public void Reject(string id, Vector3 position, MapVegetationCategories category, string reason, string source, bool keepDetail = true)
        {
            string key = category + ":" + reason;
            Rejections.TryGetValue(key, out int count);
            Rejections[key] = count + 1;
            if (keepDetail || Issues.Count < 2000)
                Issues.Add(new MapVegetationIssue { id = id, position = position, cellId = Cell.Id,
                    category = category.ToString(), reason = reason, source = source });
        }
    }

    /// <summary>World-space deterministic sampling shared by preview and saved output.</summary>
    public static class MapVegetationPlanning
    {
        // Far-LOD foliage cards are alpha clipped, so their full mesh bounds
        // overstate the opaque crown. Keep a deterministic safety margin when
        // deciding whether adjacent crowns actually close the skyline.
        public const float DistantCrownOpaqueFillFraction = 0.9f;
        public const float DistantCoverageBackboneHeightFraction = 0.35f;
        public const string DistantCrownCoveragePolicyVersion =
            "msc.distant-crown-coverage.v2";
        public const string NaturalInfillPolicyVersion =
            "msc.natural-tree-infill.v3-cap65";

        public readonly struct DistantCrownProjection
        {
            public readonly float AlongDistance;
            public readonly float EffectiveRadius;

            public DistantCrownProjection(float alongDistance,
                float effectiveRadius)
            {
                AlongDistance = alongDistance;
                EffectiveRadius = effectiveRadius;
            }
        }

        public readonly struct DistantCrownCoverage
        {
            public readonly int ProjectionCount;
            public readonly int GapCount;
            public readonly float CoveredLength;
            public readonly float LargestGap;

            public DistantCrownCoverage(int projectionCount, int gapCount,
                float coveredLength, float largestGap)
            {
                ProjectionCount = projectionCount;
                GapCount = gapCount;
                CoveredLength = coveredLength;
                LargestGap = largestGap;
            }

            public bool IsClosed => GapCount == 0;
        }

        public readonly struct BoundaryCandidate
        {
            public readonly int X, Z;
            public readonly Vector3 Position;
            public readonly Vector3 BoundaryPoint;
            public readonly Vector3 Outward;
            public readonly float Distance;
            public readonly string SourceId;
            public BoundaryCandidate(int x, int z, Vector3 position, float distance, string sourceId)
                : this(x, z, position, position, Vector3.zero, distance,
                    sourceId)
            {
            }

            public BoundaryCandidate(int x, int z, Vector3 position,
                Vector3 boundaryPoint, Vector3 outward, float distance,
                string sourceId)
            {
                X = x;
                Z = z;
                Position = position;
                BoundaryPoint = boundaryPoint;
                Outward = outward;
                Distance = distance;
                SourceId = sourceId;
            }
        }

        public readonly struct OrientedBoundarySegment
        {
            public MapVegetationBoundarySegment Segment { get; }
            public Vector2 From { get; }
            public Vector2 To { get; }
            public Vector2 Outward { get; }
            public bool IsSyntheticClosure { get; }

            internal OrientedBoundarySegment(
                MapVegetationBoundarySegment segment, Vector2 from,
                Vector2 to, Vector2 outward,
                bool isSyntheticClosure = false)
            {
                Segment = segment;
                From = from;
                To = to;
                Outward = outward;
                IsSyntheticClosure = isSyntheticClosure;
            }
        }

        public sealed class BoundaryEnclosure
        {
            private readonly Vector2[] vertices;
            private readonly OrientedBoundarySegment[] segments;

            public string Id { get; }
            public IReadOnlyList<Vector2> Vertices => vertices;
            public IReadOnlyList<OrientedBoundarySegment> Segments => segments;
            public float SignedArea { get; }
            public float Perimeter { get; }
            public float ClosingGap { get; }
            public bool ClosedBySmallGap { get; }
            public bool IsOuterEnvelope { get; internal set; }

            internal BoundaryEnclosure(string id, Vector2[] polygon,
                OrientedBoundarySegment[] orientedSegments,
                float signedArea, float perimeter, float closingGap,
                bool closedBySmallGap)
            {
                Id = id;
                vertices = polygon;
                segments = orientedSegments;
                SignedArea = signedArea;
                Perimeter = perimeter;
                ClosingGap = closingGap;
                ClosedBySmallGap = closedBySmallGap;
            }

            public bool Contains(Vector2 point)
            {
                bool inside = false;
                for (int current = 0, previous = vertices.Length - 1;
                     current < vertices.Length; previous = current++)
                {
                    Vector2 a = vertices[previous];
                    Vector2 b = vertices[current];
                    if (DistanceToSegment(point, a, b) <= 0.05f)
                        return true;
                    bool crosses = (a.y > point.y) != (b.y > point.y) &&
                                   point.x < (b.x - a.x) *
                                   (point.y - a.y) /
                                   (b.y - a.y) + a.x;
                    if (crosses) inside = !inside;
                }
                return inside;
            }
        }

        public static IReadOnlyList<BoundaryEnclosure>
            BuildBoundaryEnclosures(
                IEnumerable<MapVegetationBoundarySegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(
                nameof(segments));
            MapVegetationBoundarySegment[] source = segments
                .Where(segment => segment != null)
                .OrderBy(segment => segment.SourceStableId ??
                                    segment.SourcePath ?? string.Empty,
                    StringComparer.Ordinal)
                .ThenBy(segment => segment.Id, StringComparer.Ordinal)
                .ToArray();
            if (source.Length == 0)
                throw new InvalidDataException(
                    "No boundary segments were available for topology analysis.");

            var result = new List<BoundaryEnclosure>();
            foreach (IGrouping<string, MapVegetationBoundarySegment> group in
                     source.GroupBy(segment =>
                             segment.SourceStableId ?? segment.SourcePath ??
                             string.Empty,
                         StringComparer.Ordinal))
            {
                MapVegetationBoundarySegment[] grouped = group.ToArray();
                var adjacency = new Dictionary<BoundaryNodeKey, List<int>>();
                var endpoints = new (BoundaryNodeKey A, BoundaryNodeKey B)[
                    grouped.Length];
                for (int index = 0; index < grouped.Length; index++)
                {
                    BoundaryNodeKey a = new BoundaryNodeKey(grouped[index].A);
                    BoundaryNodeKey b = new BoundaryNodeKey(grouped[index].B);
                    if (a.Equals(b))
                        throw new InvalidDataException(
                            "Boundary segment collapses to one topology node: " +
                            grouped[index].Id);
                    endpoints[index] = (a, b);
                    AddAdjacency(adjacency, a, index);
                    AddAdjacency(adjacency, b, index);
                }

                var remaining = new HashSet<int>(Enumerable.Range(0,
                    grouped.Length));
                while (remaining.Count > 0)
                {
                    int seedIndex = remaining.OrderBy(index =>
                        grouped[index].Id, StringComparer.Ordinal).First();
                    var component = new HashSet<int>();
                    var pending = new Stack<int>();
                    pending.Push(seedIndex);
                    while (pending.Count > 0)
                    {
                        int index = pending.Pop();
                        if (!component.Add(index)) continue;
                        remaining.Remove(index);
                        foreach (BoundaryNodeKey node in new[]
                                 { endpoints[index].A, endpoints[index].B })
                        foreach (int neighbour in adjacency[node])
                            if (!component.Contains(neighbour))
                                pending.Push(neighbour);
                    }
                    result.Add(BuildBoundaryEnclosure(group.Key, grouped,
                        endpoints, adjacency, component));
                }
            }

            for (int index = 0; index < result.Count; index++)
            {
                BoundaryEnclosure candidate = result[index];
                bool contained = false;
                for (int otherIndex = 0; otherIndex < result.Count;
                     otherIndex++)
                {
                    if (otherIndex == index) continue;
                    BoundaryEnclosure other = result[otherIndex];
                    if (Mathf.Abs(other.SignedArea) <=
                        Mathf.Abs(candidate.SignedArea))
                        continue;
                    int inside = candidate.Vertices.Count(vertex =>
                        other.Contains(vertex));
                    if (inside >= Mathf.CeilToInt(
                            candidate.Vertices.Count * 0.95f))
                    {
                        contained = true;
                        break;
                    }
                }
                candidate.IsOuterEnvelope = !contained;
                foreach (OrientedBoundarySegment item in candidate.Segments)
                    if (item.IsSyntheticClosure)
                        item.Segment.IsOuterEnvelope =
                            candidate.IsOuterEnvelope;
            }
            if (!result.Any(enclosure => enclosure.IsOuterEnvelope))
                throw new InvalidDataException(
                    "Boundary topology has no outer envelope.");
            return result.OrderBy(enclosure => enclosure.Id,
                StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Builds the true outside masking envelope for the distant forest.
        /// The donor tree-wall ring contains deep concavities: offsetting every
        /// concave edge by 200-650 m can enter the donor enclosure again, making
        /// an outside-only three-layer backdrop geometrically impossible. A
        /// convex hull per disjoint donor-validated outer enclosure preserves
        /// the proven map extent while giving the collisionless horizon one
        /// unambiguous signed outside at every depth.
        /// </summary>
        public static MapVegetationBoundarySegment[]
            BuildDistantOuterEnvelopeSegments(
                IEnumerable<MapVegetationBoundarySegment> sourceSegments)
        {
            IReadOnlyList<BoundaryEnclosure> sourceTopology =
                BuildBoundaryEnclosures(sourceSegments);
            var result = new List<MapVegetationBoundarySegment>();
            int envelopeOrdinal = 0;
            foreach (BoundaryEnclosure enclosure in sourceTopology
                         .Where(item => item.IsOuterEnvelope)
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                Vector3[] points = enclosure.Segments
                    .SelectMany(item => new[]
                    {
                        item.Segment.A, item.Segment.B
                    })
                    .OrderBy(point => point.x)
                    .ThenBy(point => point.z)
                    .ThenBy(point => point.y)
                    .Aggregate(new List<Vector3>(), (unique, point) =>
                    {
                        if (unique.Count == 0 ||
                            Mathf.Abs(unique[unique.Count - 1].x - point.x) >
                            0.0001f ||
                            Mathf.Abs(unique[unique.Count - 1].z - point.z) >
                            0.0001f)
                            unique.Add(point);
                        return unique;
                    }).ToArray();
                if (points.Length < 3)
                    throw new InvalidDataException(
                        "Distant outer envelope has fewer than three unique points: " +
                        enclosure.Id);

                var lower = new List<Vector3>();
                foreach (Vector3 point in points)
                {
                    while (lower.Count >= 2 && Cross2(
                               new Vector2(lower[lower.Count - 1].x -
                                           lower[lower.Count - 2].x,
                                   lower[lower.Count - 1].z -
                                   lower[lower.Count - 2].z),
                               new Vector2(point.x -
                                           lower[lower.Count - 1].x,
                                   point.z -
                                   lower[lower.Count - 1].z)) <= 0.001f)
                        lower.RemoveAt(lower.Count - 1);
                    lower.Add(point);
                }
                var upper = new List<Vector3>();
                for (int index = points.Length - 1; index >= 0; index--)
                {
                    Vector3 point = points[index];
                    while (upper.Count >= 2 && Cross2(
                               new Vector2(upper[upper.Count - 1].x -
                                           upper[upper.Count - 2].x,
                                   upper[upper.Count - 1].z -
                                   upper[upper.Count - 2].z),
                               new Vector2(point.x -
                                           upper[upper.Count - 1].x,
                                   point.z -
                                   upper[upper.Count - 1].z)) <= 0.001f)
                        upper.RemoveAt(upper.Count - 1);
                    upper.Add(point);
                }
                lower.RemoveAt(lower.Count - 1);
                upper.RemoveAt(upper.Count - 1);
                Vector3[] hull = lower.Concat(upper).ToArray();
                if (hull.Length < 3)
                    throw new InvalidDataException(
                        "Distant outer envelope convex hull is degenerate: " +
                        enclosure.Id);

                float signedArea = SignedArea(hull.Select(point =>
                    new Vector2(point.x, point.z)).ToArray());
                if (signedArea <= 0f)
                    throw new InvalidDataException(
                        "Distant outer envelope hull must be counter-clockwise: " +
                        enclosure.Id);
                float perimeter = 0f;
                for (int index = 0; index < hull.Length; index++)
                    perimeter += Vector3.Distance(hull[index],
                        hull[(index + 1) % hull.Length]);
                string stableId = "distant-outer-envelope:" +
                                  envelopeOrdinal.ToString(
                                      CultureInfo.InvariantCulture) + ":" +
                                  HashId(enclosure.Id, 0x4D51).ToString("x8",
                                      CultureInfo.InvariantCulture);
                for (int index = 0; index < hull.Length; index++)
                {
                    Vector3 a = hull[index];
                    Vector3 b = hull[(index + 1) % hull.Length];
                    Vector2 along = new Vector2(b.x - a.x,
                        b.z - a.z).normalized;
                    Vector3 outward = new Vector3(along.y, 0f, -along.x);
                    result.Add(new MapVegetationBoundarySegment
                    {
                        Id = stableId + ":segment:" +
                             index.ToString("D4",
                                 CultureInfo.InvariantCulture),
                        A = a,
                        B = b,
                        AdjacentTriangleNormal = -outward,
                        Outward = outward,
                        HasValidatedOutward = true,
                        EnclosureId = stableId,
                        IsOuterEnvelope = true,
                        EnclosureClosedBySmallGap = false,
                        EnclosureSignedArea = signedArea,
                        EnclosurePerimeter = perimeter,
                        PositiveTreeSideEvidenceCount = 1,
                        NegativeTreeSideEvidenceCount = 0,
                        TreeSideConfidence = 1f,
                        OrientationMethod =
                            "ConvexOuterEnvelopeDerivedFromDonorValidatedBoundaryForCollisionlessDistantMasking",
                        SourceStableId = stableId,
                        SourcePath = "DerivedFrom:" + enclosure.Id,
                        SourceHash = HashId(enclosure.Id, 0x7A13)
                            .ToString("x8", CultureInfo.InvariantCulture)
                    });
                }
                envelopeOrdinal++;
            }
            if (result.Count == 0)
                throw new InvalidDataException(
                    "No donor-validated outer envelope was available for the distant forest.");
            return result.OrderBy(segment => segment.Id,
                StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Returns a deterministic mandatory tree at <= maximumAlongSpacing
        /// intervals in every signed distant depth band. These points are not a
        /// density pool; callers must keep them ahead of the hard cap.
        /// </summary>
        public static IReadOnlyList<BoundaryCandidate>
            DistantCoverageBackboneCandidates(
                IEnumerable<MapVegetationBoundarySegment> envelopeSegments,
                float minimumDistance, float maximumDistance,
                float maximumAlongSpacing, Matrix4x4 coordinateMapping)
        {
            if (!float.IsFinite(minimumDistance) ||
                !float.IsFinite(maximumDistance) ||
                minimumDistance < 0f || minimumDistance >= 200f ||
                maximumDistance <= 400f ||
                !float.IsFinite(maximumAlongSpacing) ||
                maximumAlongSpacing <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDistance));
            IReadOnlyList<BoundaryEnclosure> topology =
                BuildBoundaryEnclosures(envelopeSegments);
            Matrix4x4 inverseMapping = InvertCoordinateMapping(
                coordinateMapping);
            var result = new List<BoundaryCandidate>();
            float[] distances =
            {
                (minimumDistance + 200f) * 0.5f,
                300f,
                (400f + maximumDistance) * 0.5f
            };
            foreach (OrientedBoundarySegment item in topology
                         .Where(enclosure => enclosure.IsOuterEnvelope)
                         .SelectMany(enclosure => enclosure.Segments)
                         .OrderBy(segment => segment.Segment.Id,
                             StringComparer.Ordinal))
            {
                Vector3 a = coordinateMapping.MultiplyPoint3x4(
                    item.Segment.A);
                Vector3 b = coordinateMapping.MultiplyPoint3x4(
                    item.Segment.B);
                Vector2 edge = new Vector2(b.x - a.x, b.z - a.z);
                float length = edge.magnitude;
                if (!float.IsFinite(length) || length < 0.01f)
                    throw new InvalidDataException(
                        "Mapped distant envelope segment is degenerate: " +
                        item.Segment.Id);
                Vector2 along = edge / length;
                Vector3 sourceMid = (item.Segment.A + item.Segment.B) *
                                    0.5f;
                Vector3 mappedOutwardPoint = coordinateMapping
                    .MultiplyPoint3x4(sourceMid + item.Segment.Outward);
                Vector3 mappedMid = coordinateMapping.MultiplyPoint3x4(
                    sourceMid);
                Vector2 outwardEvidence = new Vector2(
                    mappedOutwardPoint.x - mappedMid.x,
                    mappedOutwardPoint.z - mappedMid.z);
                Vector2 outward = new Vector2(-along.y, along.x);
                if (Vector2.Dot(outward, outwardEvidence) < 0f)
                    outward = -outward;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length /
                    maximumAlongSpacing));
                for (int band = 0; band < distances.Length; band++)
                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    Vector3 boundaryPoint = Vector3.Lerp(a, b, t);
                    Vector3 position = boundaryPoint + new Vector3(
                        outward.x, 0f, outward.y) * distances[band];
                    if (ContainsMappedPoint(topology, position,
                            inverseMapping))
                        throw new InvalidDataException(
                            "Distant coverage target entered its convex outer envelope: " +
                            item.Segment.Id + " band=" + band +
                            " step=" + step);
                    if (DistantDepthBand(distances[band], minimumDistance,
                            maximumDistance) != band)
                        throw new InvalidDataException(
                            "Distant coverage target escaped its signed depth band.");
                    result.Add(new BoundaryCandidate(step, band, position,
                        boundaryPoint,
                        new Vector3(outward.x, 0f, outward.y),
                        distances[band], item.Segment.Id));
                }
            }
            return result;
        }

        public static float DistantCoverageBackboneMinimumHeight(
            Vector2 heightRange)
        {
            if (!float.IsFinite(heightRange.x) ||
                !float.IsFinite(heightRange.y) || heightRange.x <= 0f ||
                heightRange.y < heightRange.x)
                throw new ArgumentOutOfRangeException(nameof(heightRange));
            return Mathf.Lerp(heightRange.x, heightRange.y,
                DistantCoverageBackboneHeightFraction);
        }

        public static float MaximumDistantCrownCenterSpacing(
            float minimumScaledCrownRadius)
        {
            if (!float.IsFinite(minimumScaledCrownRadius) ||
                minimumScaledCrownRadius <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumScaledCrownRadius));
            return 2f * minimumScaledCrownRadius *
                   DistantCrownOpaqueFillFraction;
        }

        /// <summary>
        /// Measures exact interval union across one boundary segment. Unlike
        /// the old nearest-trunk check, this fails whenever alpha-safe crown
        /// projections leave even a narrow visible skyline hole.
        /// </summary>
        public static DistantCrownCoverage MeasureDistantCrownCoverage(
            float segmentLength,
            IEnumerable<DistantCrownProjection> projections)
        {
            if (!float.IsFinite(segmentLength) || segmentLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(segmentLength));
            if (projections == null)
                throw new ArgumentNullException(nameof(projections));
            var intervals = new List<Vector2>();
            foreach (DistantCrownProjection projection in projections)
            {
                if (!float.IsFinite(projection.AlongDistance) ||
                    !float.IsFinite(projection.EffectiveRadius) ||
                    projection.EffectiveRadius <= 0f)
                    continue;
                float start = Mathf.Max(0f, projection.AlongDistance -
                                            projection.EffectiveRadius);
                float end = Mathf.Min(segmentLength,
                    projection.AlongDistance + projection.EffectiveRadius);
                if (end >= start)
                    intervals.Add(new Vector2(start, end));
            }
            intervals.Sort((left, right) =>
            {
                int byStart = left.x.CompareTo(right.x);
                return byStart != 0 ? byStart : left.y.CompareTo(right.y);
            });

            const float tolerance = 0.001f;
            int gaps = 0;
            float largestGap = 0f;
            float covered = 0f;
            float cursor = 0f;
            foreach (Vector2 interval in intervals)
            {
                if (interval.x > cursor + tolerance)
                {
                    float gap = interval.x - cursor;
                    gaps++;
                    largestGap = Mathf.Max(largestGap, gap);
                    cursor = interval.x;
                }
                if (interval.y <= cursor) continue;
                covered += interval.y - cursor;
                cursor = interval.y;
                if (cursor >= segmentLength - tolerance)
                {
                    cursor = segmentLength;
                    break;
                }
            }
            if (cursor < segmentLength - tolerance)
            {
                float gap = segmentLength - cursor;
                gaps++;
                largestGap = Mathf.Max(largestGap, gap);
            }
            return new DistantCrownCoverage(intervals.Count, gaps,
                Mathf.Clamp(covered, 0f, segmentLength), largestGap);
        }

        private static BoundaryEnclosure BuildBoundaryEnclosure(
            string sourceId, MapVegetationBoundarySegment[] grouped,
            (BoundaryNodeKey A, BoundaryNodeKey B)[] endpoints,
            Dictionary<BoundaryNodeKey, List<int>> adjacency,
            HashSet<int> component)
        {
            BoundaryNodeKey[] nodes = component.SelectMany(index => new[]
                    { endpoints[index].A, endpoints[index].B })
                .Distinct().OrderBy(node => node).ToArray();
            BoundaryNodeKey[] openEnds = nodes.Where(node =>
                    adjacency[node].Count(index => component.Contains(index)) ==
                    1)
                .ToArray();
            bool closed = openEnds.Length == 0 && nodes.All(node =>
                adjacency[node].Count(index => component.Contains(index)) ==
                2);
            bool openChain = openEnds.Length == 2 && nodes.All(node =>
            {
                int degree = adjacency[node].Count(index =>
                    component.Contains(index));
                return degree == 1 || degree == 2;
            });
            if (!closed && !openChain)
                throw new InvalidDataException(
                    "Boundary component branches or has unsupported endpoints: " +
                    sourceId);

            BoundaryNodeKey start = closed ? nodes[0] : openEnds[0];
            BoundaryNodeKey current = start;
            var used = new HashSet<int>();
            var ordered = new List<(MapVegetationBoundarySegment Segment,
                Vector2 From, Vector2 To)>();
            while (used.Count < component.Count)
            {
                int[] available = adjacency[current]
                    .Where(index => component.Contains(index) &&
                                    !used.Contains(index))
                    .OrderBy(index => grouped[index].Id,
                        StringComparer.Ordinal).ToArray();
                if (available.Length == 0)
                    throw new InvalidDataException(
                        "Boundary chain ended before all segments were visited: " +
                        sourceId);
                int nextIndex = available[0];
                used.Add(nextIndex);
                bool forward = endpoints[nextIndex].A.Equals(current);
                BoundaryNodeKey next = forward ? endpoints[nextIndex].B :
                    endpoints[nextIndex].A;
                Vector3 from3 = forward ? grouped[nextIndex].A :
                    grouped[nextIndex].B;
                Vector3 to3 = forward ? grouped[nextIndex].B :
                    grouped[nextIndex].A;
                ordered.Add((grouped[nextIndex],
                    new Vector2(from3.x, from3.z),
                    new Vector2(to3.x, to3.z)));
                current = next;
            }
            if ((closed && !current.Equals(start)) ||
                (openChain && current.Equals(start)))
                throw new InvalidDataException(
                    "Boundary chain closure differs from its node degrees: " +
                    sourceId);

            float perimeter = ordered.Sum(item =>
                Vector2.Distance(item.From, item.To));
            float closingGap = closed ? 0f : Vector2.Distance(
                ordered[ordered.Count - 1].To, ordered[0].From);
            bool smallGap = !closed && closingGap <= 256f &&
                            closingGap <= perimeter * 0.02f;
            if (!closed && !smallGap)
                throw new InvalidDataException(
                    "Open boundary gap is not a strict small-gap closure: source=" +
                    sourceId + " gap=" + closingGap.ToString("R",
                        CultureInfo.InvariantCulture) + " perimeter=" +
                    perimeter.ToString("R", CultureInfo.InvariantCulture));
            Vector2[] polygon = (openChain
                    ? ordered.Select(item => item.From).Concat(new[]
                        { ordered[ordered.Count - 1].To })
                    : ordered.Select(item => item.From))
                .ToArray();
            float signedArea = SignedArea(polygon);
            if (!float.IsFinite(signedArea) ||
                Mathf.Abs(signedArea) < 1000f)
                throw new InvalidDataException(
                    "Boundary polygon area is degenerate: " + sourceId);
            if (HasSelfIntersection(polygon))
                throw new InvalidDataException(
                    "Boundary polygon self-intersects: " + sourceId);

            bool counterClockwise = signedArea > 0f;
            string componentId = sourceId + ":component:" +
                                 component.Select(index => grouped[index].Id)
                                     .OrderBy(id => id,
                                         StringComparer.Ordinal).First();
            int orientedCount = ordered.Count + (smallGap ? 1 : 0);
            var oriented = new OrientedBoundarySegment[orientedCount];
            for (int index = 0; index < ordered.Count; index++)
            {
                Vector2 along = (ordered[index].To -
                                 ordered[index].From).normalized;
                Vector2 outward = counterClockwise
                    ? new Vector2(along.y, -along.x)
                    : new Vector2(-along.y, along.x);
                oriented[index] = new OrientedBoundarySegment(
                    ordered[index].Segment, ordered[index].From,
                    ordered[index].To, outward);
            }
            if (smallGap)
            {
                Vector2 from = ordered[ordered.Count - 1].To;
                Vector2 to = ordered[0].From;
                Vector2 along = (to - from).normalized;
                Vector2 outward = counterClockwise
                    ? new Vector2(along.y, -along.x)
                    : new Vector2(-along.y, along.x);
                MapVegetationBoundarySegment provenance = ordered[0].Segment;
                var synthetic = new MapVegetationBoundarySegment
                {
                    Id = componentId + ":synthetic-small-gap-closure",
                    A = new Vector3(from.x, EndpointY(
                        ordered[ordered.Count - 1].Segment, from), from.y),
                    B = new Vector3(to.x, EndpointY(
                        ordered[0].Segment, to), to.y),
                    Outward = new Vector3(outward.x, 0f, outward.y),
                    HasValidatedOutward = true,
                    EnclosureId = componentId,
                    EnclosureClosedBySmallGap = true,
                    EnclosureSignedArea = signedArea,
                    EnclosurePerimeter = perimeter,
                    OrientationMethod =
                        "SyntheticSmallGapClosureForDistantMaskingOnly",
                    SourceStableId = provenance.SourceStableId,
                    SourcePath = provenance.SourcePath +
                                 ":synthetic-small-gap-closure",
                    SourceMeshGuid = provenance.SourceMeshGuid,
                    SourceHash = provenance.SourceHash
                };
                oriented[ordered.Count] = new OrientedBoundarySegment(
                    synthetic, from, to, outward,
                    isSyntheticClosure: true);
            }
            return new BoundaryEnclosure(componentId, polygon, oriented,
                signedArea, perimeter, closingGap, smallGap);
        }

        private static float EndpointY(MapVegetationBoundarySegment segment,
            Vector2 endpoint)
        {
            Vector2 a = new Vector2(segment.A.x, segment.A.z);
            Vector2 b = new Vector2(segment.B.x, segment.B.z);
            return (endpoint - a).sqrMagnitude <=
                   (endpoint - b).sqrMagnitude
                ? segment.A.y : segment.B.y;
        }

        private static void AddAdjacency(
            Dictionary<BoundaryNodeKey, List<int>> adjacency,
            BoundaryNodeKey node, int segmentIndex)
        {
            if (!adjacency.TryGetValue(node, out List<int> indices))
                adjacency.Add(node, indices = new List<int>());
            indices.Add(segmentIndex);
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            double twiceArea = 0d;
            for (int current = 0, previous = polygon.Count - 1;
                 current < polygon.Count; previous = current++)
                twiceArea += (double)polygon[previous].x * polygon[current].y -
                             (double)polygon[current].x * polygon[previous].y;
            return (float)(twiceArea * 0.5d);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a,
            Vector2 b)
        {
            Vector2 edge = b - a;
            if (edge.sqrMagnitude < 0.000001f)
                return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) /
                                    edge.sqrMagnitude);
            return Vector2.Distance(point, a + edge * t);
        }

        private static bool HasSelfIntersection(IReadOnlyList<Vector2> polygon)
        {
            int count = polygon.Count;
            for (int first = 0; first < count; first++)
            {
                int firstNext = (first + 1) % count;
                for (int second = first + 1; second < count; second++)
                {
                    int secondNext = (second + 1) % count;
                    if (first == second || firstNext == second ||
                        secondNext == first)
                        continue;
                    if (SegmentsCross(polygon[first], polygon[firstNext],
                            polygon[second], polygon[secondNext]))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c,
            Vector2 d)
        {
            float abC = Cross2(b - a, c - a);
            float abD = Cross2(b - a, d - a);
            float cdA = Cross2(d - c, a - c);
            float cdB = Cross2(d - c, b - c);
            const float epsilon = 0.001f;
            return ((abC > epsilon && abD < -epsilon) ||
                    (abC < -epsilon && abD > epsilon)) &&
                   ((cdA > epsilon && cdB < -epsilon) ||
                    (cdA < -epsilon && cdB > epsilon));
        }

        private static float Cross2(Vector2 left, Vector2 right) =>
            left.x * right.y - left.y * right.x;

        private readonly struct BoundaryNodeKey :
            IEquatable<BoundaryNodeKey>, IComparable<BoundaryNodeKey>
        {
            private readonly long x;
            private readonly long y;
            private readonly long z;

            public BoundaryNodeKey(Vector3 point)
            {
                x = (long)Math.Round(point.x * 50d);
                y = (long)Math.Round(point.y * 50d);
                z = (long)Math.Round(point.z * 50d);
            }

            public bool Equals(BoundaryNodeKey other) =>
                x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) =>
                obj is BoundaryNodeKey other && Equals(other);
            public override int GetHashCode() => unchecked(
                (x.GetHashCode() * 397 ^ y.GetHashCode()) * 397 ^
                z.GetHashCode());
            public int CompareTo(BoundaryNodeKey other)
            {
                int comparison = x.CompareTo(other.x);
                if (comparison != 0) return comparison;
                comparison = z.CompareTo(other.z);
                return comparison != 0 ? comparison : y.CompareTo(other.y);
            }
        }

        public static IReadOnlyList<BoundaryCandidate>
            SignedOutwardBoundaryCandidates(
                IEnumerable<MapVegetationBoundarySegment> segments,
                float spacing, float depth, int seed,
                Matrix4x4 coordinateMapping)
        {
            return SignedBoundaryCandidates(segments, spacing, depth, seed,
                coordinateMapping, outwardSide: true);
        }

        public static IReadOnlyList<BoundaryCandidate>
            SignedInwardBoundaryCandidates(
                IEnumerable<MapVegetationBoundarySegment> segments,
                float spacing, float depth, int seed,
                Matrix4x4 coordinateMapping)
        {
            return SignedBoundaryCandidates(segments, spacing, depth, seed,
                coordinateMapping, outwardSide: false);
        }

        private static IReadOnlyList<BoundaryCandidate>
            SignedBoundaryCandidates(
                IEnumerable<MapVegetationBoundarySegment> segments,
                float spacing, float depth, int seed,
                Matrix4x4 coordinateMapping, bool outwardSide)
        {
            if (!float.IsFinite(spacing) || spacing <= 0f ||
                !float.IsFinite(depth) || depth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacing));
            if (segments == null) throw new ArgumentNullException(
                nameof(segments));
            IReadOnlyList<BoundaryEnclosure> enclosures =
                BuildBoundaryEnclosures(segments);
            Matrix4x4 inverseMapping = InvertCoordinateMapping(
                coordinateMapping);
            foreach (BoundaryEnclosure enclosure in enclosures.Where(
                         item => item.IsOuterEnvelope))
            foreach (OrientedBoundarySegment item in enclosure.Segments)
            {
                if (!item.IsSyntheticClosure &&
                    (!item.Segment.HasValidatedOutward ||
                    item.Segment.IsOuterEnvelope != enclosure.IsOuterEnvelope ||
                    Vector2.Dot(new Vector2(item.Segment.Outward.x,
                            item.Segment.Outward.z).normalized,
                        item.Outward) < 0.99f))
                    throw new InvalidDataException(
                        "Distant forest boundary topology differs from its donor-validated signed orientation: " +
                        item.Segment.Id);
            }
            MapVegetationBoundarySegment[] source = enclosures
                .Where(enclosure => enclosure.IsOuterEnvelope)
                .SelectMany(enclosure => enclosure.Segments)
                .Select(item => item.Segment)
                .OrderBy(segment => segment.Id, StringComparer.Ordinal)
                .ToArray();
            MapVegetationBoundarySegment ambiguous = source.FirstOrDefault(
                segment => segment == null ||
                           !segment.HasValidatedOutward);
            if (ambiguous != null)
                throw new InvalidDataException(
                    "Distant forest requires a donor-tree-validated signed outward for every boundary segment; ambiguous segment=" +
                    (ambiguous.Id ?? "<null>"));

            var nearest = new Dictionary<Vector2Int, BoundaryCandidate>();
            foreach (MapVegetationBoundarySegment segment in source)
            {
                Vector3 a = coordinateMapping.MultiplyPoint3x4(segment.A);
                Vector3 b = coordinateMapping.MultiplyPoint3x4(segment.B);
                Vector2 edge = new Vector2(b.x - a.x, b.z - a.z);
                float edgeLengthSquared = edge.sqrMagnitude;
                if (!float.IsFinite(edgeLengthSquared) ||
                    edgeLengthSquared < 0.01f)
                    throw new InvalidDataException(
                        "Mapped boundary segment is degenerate: " +
                        segment.Id);
                Vector3 sourceMid = (segment.A + segment.B) * 0.5f;
                Vector3 mappedOutwardPoint = coordinateMapping
                    .MultiplyPoint3x4(sourceMid + segment.Outward);
                Vector3 mappedMid = coordinateMapping.MultiplyPoint3x4(
                    sourceMid);
                Vector2 outwardEvidence = new Vector2(
                    mappedOutwardPoint.x - mappedMid.x,
                    mappedOutwardPoint.z - mappedMid.z);
                if (outwardEvidence.sqrMagnitude < 0.01f)
                    throw new InvalidDataException(
                        "Mapped boundary outward is degenerate: " +
                        segment.Id);
                Vector2 along = edge.normalized;
                Vector2 outward = new Vector2(-along.y, along.x);
                if (Vector2.Dot(outward, outwardEvidence) < 0f)
                    outward = -outward;

                int minX = Mathf.FloorToInt((Mathf.Min(a.x, b.x) -
                    depth) / spacing);
                int maxX = Mathf.FloorToInt((Mathf.Max(a.x, b.x) +
                    depth) / spacing);
                int minZ = Mathf.FloorToInt((Mathf.Min(a.z, b.z) -
                    depth) / spacing);
                int maxZ = Mathf.FloorToInt((Mathf.Max(a.z, b.z) +
                    depth) / spacing);
                for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 p = Candidate(x, z, spacing, seed);
                    Vector2 fromA = new Vector2(p.x - a.x, p.z - a.z);
                    float t = Vector2.Dot(fromA, edge) /
                              edgeLengthSquared;
                    if (t < 0f || t > 1f)
                        continue;
                    Vector3 boundaryPoint = Vector3.Lerp(a, b, t);
                    Vector2 delta = new Vector2(
                        p.x - boundaryPoint.x,
                        p.z - boundaryPoint.z);
                    float signedDistance = Vector2.Dot(delta, outward);
                    float sideDistance = outwardSide
                        ? signedDistance : -signedDistance;
                    if (sideDistance < 0f || sideDistance > depth)
                        continue;
                    bool insideEnclosure = ContainsMappedPoint(enclosures, p,
                        inverseMapping);
                    if (outwardSide ? insideEnclosure : !insideEnclosure)
                        continue;
                    p.y = boundaryPoint.y;
                    var key = new Vector2Int(x, z);
                    if (nearest.TryGetValue(key,
                            out BoundaryCandidate existing) &&
                        (existing.Distance < sideDistance ||
                         existing.Distance == sideDistance &&
                         string.Compare(existing.SourceId, segment.Id,
                             StringComparison.Ordinal) <= 0))
                        continue;
                    nearest[key] = new BoundaryCandidate(x, z, p,
                        boundaryPoint,
                        new Vector3(outward.x, 0f, outward.y),
                        sideDistance, segment.Id);
                }
            }

            return nearest.Values.OrderBy(candidate => candidate.Z)
                .ThenBy(candidate => candidate.X).ToArray();
        }

        public static Matrix4x4 InvertCoordinateMapping(
            Matrix4x4 coordinateMapping)
        {
            for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                if (!float.IsFinite(coordinateMapping[row, column]))
                    throw new InvalidDataException(
                        "Boundary coordinate mapping contains a non-finite value.");
            float determinant = coordinateMapping.determinant;
            if (!float.IsFinite(determinant) ||
                Mathf.Abs(determinant) < 0.0000001f)
                throw new InvalidDataException(
                    "Boundary coordinate mapping is not invertible.");
            return coordinateMapping.inverse;
        }

        public static bool ContainsMappedPoint(
            IEnumerable<BoundaryEnclosure> sourceSpaceEnclosures,
            Vector3 mappedPoint, Matrix4x4 inverseCoordinateMapping)
        {
            if (sourceSpaceEnclosures == null)
                throw new ArgumentNullException(
                    nameof(sourceSpaceEnclosures));
            Vector3 sourcePoint = inverseCoordinateMapping.MultiplyPoint3x4(
                mappedPoint);
            if (!float.IsFinite(sourcePoint.x) ||
                !float.IsFinite(sourcePoint.z))
                throw new InvalidDataException(
                    "Mapped boundary point cannot be transformed to source space.");
            var sourceXz = new Vector2(sourcePoint.x, sourcePoint.z);
            return sourceSpaceEnclosures.Any(enclosure =>
                enclosure.Contains(sourceXz));
        }

        public static int DistantDepthBand(float signedDistance,
            float minimumDistance, float maximumDistance)
        {
            if (!float.IsFinite(signedDistance) ||
                !float.IsFinite(minimumDistance) ||
                !float.IsFinite(maximumDistance) ||
                signedDistance < minimumDistance ||
                signedDistance > maximumDistance)
                return -1;
            if (signedDistance < 200f) return 0;
            if (signedDistance < 400f) return 1;
            return maximumDistance > 400f ? 2 : -1;
        }

        public static float MaximumDistantAlongEdgeGap(float spacing,
            float minimumScaledCrownRadius)
        {
            if (!float.IsFinite(spacing) || spacing <= 0f ||
                !float.IsFinite(minimumScaledCrownRadius) ||
                minimumScaledCrownRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacing));
            return MaximumDistantCrownCenterSpacing(
                minimumScaledCrownRadius);
        }

        public static bool HasAlongEdgeCoverage(float sample,
            IEnumerable<float> treeAlongDistances, float maximumGap)
        {
            if (!float.IsFinite(sample) || !float.IsFinite(maximumGap) ||
                maximumGap < 0f || treeAlongDistances == null)
                return false;
            foreach (float distance in treeAlongDistances)
                if (float.IsFinite(distance) &&
                    Mathf.Abs(distance - sample) <= maximumGap)
                    return true;
            return false;
        }

        public static IReadOnlyList<BoundaryCandidate> BoundaryCandidates(
            IEnumerable<MapVegetationBoundarySegment> segments, float spacing, float depth,
            int seed, Matrix4x4 coordinateMapping)
        {
            if (!float.IsFinite(spacing) || spacing <= 0f || !float.IsFinite(depth) || depth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacing));
            // Union only the evidence-backed boundary bands. A candidate belongs
            // to its nearest segment before density/spacing decisions are made.
            var nearest = new Dictionary<Vector2Int, BoundaryCandidate>();
            foreach (MapVegetationBoundarySegment segment in segments)
            {
                Vector3 a = coordinateMapping.MultiplyPoint3x4(segment.A);
                Vector3 b = coordinateMapping.MultiplyPoint3x4(segment.B);
                Vector2 edge = new Vector2(b.x - a.x, b.z - a.z);
                if (!float.IsFinite(edge.sqrMagnitude) || edge.sqrMagnitude < 0.01f) continue;
                int minX = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - depth) / spacing);
                int maxX = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + depth) / spacing);
                int minZ = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - depth) / spacing);
                int maxZ = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + depth) / spacing);
                for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 p = Candidate(x, z, spacing, seed);
                    Vector2 fromA = new Vector2(p.x - a.x, p.z - a.z);
                    float t = Mathf.Clamp01(Vector2.Dot(fromA, edge) / edge.sqrMagnitude);
                    float distance = (fromA - edge * t).magnitude;
                    if (distance > depth) continue;
                    var key = new Vector2Int(x, z);
                    if (nearest.TryGetValue(key, out BoundaryCandidate existing) &&
                        (existing.Distance < distance || existing.Distance == distance &&
                         string.Compare(existing.SourceId, segment.Id, StringComparison.Ordinal) <= 0)) continue;
                    nearest[key] = new BoundaryCandidate(x, z, p, distance, segment.Id);
                }
            }

            return nearest.Values.OrderBy(candidate => candidate.Z).ThenBy(candidate => candidate.X).ToArray();
        }

        public static WorldCellIndex CellAt(Vector3 position, float size) => new WorldCellIndex(
            Mathf.FloorToInt(position.x / size), Mathf.FloorToInt(position.z / size));

        internal static bool TryResolveTreePlacementSurface(
            MapVegetationSurfaceQuery surfaces,
            Vector3 candidate,
            out MapVegetationSurfaceHit hit,
            out string reason)
        {
            if (surfaces == null) throw new ArgumentNullException(
                nameof(surfaces));
            // Tree eligibility is geometry/exclusion/slope based. The albedo
            // mask is authored solely for dense grass coverage and would reject
            // valid brown forest soil and the uninhabited outer map.
            return surfaces.TryResolve(candidate, MapVegetationKind.Tree,
                out hit, out reason);
        }

        public static Vector3 Candidate(int x, int z, float spacing, int seed)
        {
            Vector2 jitter = VegetationStableHash.JitteredGridOffset(x, z, seed);
            return new Vector3((x + 0.05f + jitter.x * 0.9f) * spacing, 0f,
                (z + 0.05f + jitter.y * 0.9f) * spacing);
        }

        public static float StableGrassProfileSelector(uint candidateHash)
        {
            // Keep the art mixture independent from Perlin's centre-heavy
            // distribution. A separate salted hash gives the authored selector
            // intervals their real area share while density may remain patchy.
            return VegetationStableHash.ToUnitFloat(VegetationStableHash.Hash(
                candidateHash ^ 0x62a9d9edu));
        }

        public static int PreferredGrassProfile(float selector, int profileCount)
        {
            if (profileCount <= 0) throw new ArgumentOutOfRangeException(nameof(profileCount));
            // Reviewed short-family selection intervals: 46%, 32%, 22%.
            if (selector > 0.78f) return Math.Min(2, profileCount - 1);
            if (selector > 0.46f) return Math.Min(1, profileCount - 1);
            return 0;
        }

        public static uint HashId(string id, int seed)
        {
            unchecked
            {
                uint hash = (uint)seed ^ 2166136261u;
                foreach (char c in id ?? string.Empty) hash = (hash ^ c) * 16777619u;
                return VegetationStableHash.Hash(hash);
            }
        }

        internal static IReadOnlyList<T> SelectStableCappedIndependentSet<T>(
            IEnumerable<T> candidates,
            IEnumerable<Vector3> fixedAnchors,
            Func<T, uint> score,
            Func<T, string> stableId,
            Func<T, Vector3> position,
            float minimumDistance,
            int maximumAccepted,
            out int eligibleCount)
        {
            if (candidates == null) throw new ArgumentNullException(
                nameof(candidates));
            if (score == null) throw new ArgumentNullException(nameof(score));
            if (stableId == null) throw new ArgumentNullException(
                nameof(stableId));
            if (position == null) throw new ArgumentNullException(
                nameof(position));
            if (!float.IsFinite(minimumDistance) || minimumDistance <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumDistance));
            if (maximumAccepted < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumAccepted));

            var spacing = new SpacingIndex(minimumDistance);
            if (fixedAnchors != null)
                foreach (Vector3 anchor in fixedAnchors) spacing.Add(anchor);
            var eligible = new List<T>();
            foreach (T candidate in candidates.OrderBy(score)
                         .ThenBy(stableId, StringComparer.Ordinal))
            {
                Vector3 candidatePosition = position(candidate);
                if (spacing.HasNeighbor(candidatePosition, minimumDistance))
                    continue;
                spacing.Add(candidatePosition);
                eligible.Add(candidate);
            }
            eligibleCount = eligible.Count;
            return eligible.Take(maximumAccepted).ToArray();
        }

        public static string Fingerprint(MapVegetationCellPlan plan)
        {
            using SHA256 hash = SHA256.Create();
            using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write("map-vegetation-plan-v3");
            writer.Write(plan.Cell.Id);
            writer.Write(plan.SettingsFingerprint ?? string.Empty);
            writer.Write(plan.Woody.Count);
            writer.Write(plan.Grass.Count);
            writer.Write(plan.GrassCandidates);
            // Binary streaming preserves every float bit without allocating
            // formatting strings for hundreds of thousands of grass instances.
            for (int group = 0; group < 2; group++)
            {
                List<MapVegetationPlacement> placements = group == 0 ? plan.Woody : plan.Grass;
                foreach (MapVegetationPlacement p in placements)
                {
                    writer.Write(p.id ?? string.Empty);
                    writer.Write((int)p.category);
                    writer.Write(p.cellId ?? string.Empty);
                    writer.Write(p.source ?? string.Empty);
                    writer.Write(p.method ?? string.Empty);
                    writer.Write(p.species ?? string.Empty);
                    WriteVector(writer, p.sourcePosition);
                    WriteVector(writer, p.position);
                    WriteVector(writer, p.normal);
                    writer.Write(p.height);
                    writer.Write(p.yaw);
                    writer.Write(p.variation);
                    writer.Write(p.profile);
                }
            }

            foreach (KeyValuePair<string, int> rejection in plan.Rejections.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            { writer.Write(rejection.Key); writer.Write(rejection.Value); }
            writer.Flush();
            stream.FlushFinalBlock();
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }

        public static string FingerprintSettings(MapVegetationRebuildOptions options)
        {
            using SHA256 hash = SHA256.Create();
            AppendSettings(hash, options);
            AppendSettings(hash, options.Placement);
            foreach (VegetationProfile profile in options.GrassProfiles) AppendSettings(hash, profile);
            Append(hash, "naturalInfill=" + NaturalInfillPolicyVersion +
                "\n");
            Append(hash, "distantCrownCoverage=" +
                DistantCrownCoveragePolicyVersion + "\n");
            Append(hash, "distantTemplate=" + MapVegetationGlobalPresentation
                .DistantTemplateSelectionVersion + "\n");
            Append(hash, "grassArt=" + MapVegetationGrassBindings.BindingVersion + "\n");
            Append(hash, "forestFloor=" + MapVegetationForestFloorBindings.PlacementFingerprint + "\n");
            Append(hash, "packedWoody=" +
                MapVegetationPackedWoodyBuilder.ProjectPolicySignature() + "\n");
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }

        private static void AppendSettings(HashAlgorithm hash, UnityEngine.Object settings)
        {
            if (settings == null) { Append(hash, "<null>\n"); return; }
            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyPath.StartsWith("m_", StringComparison.Ordinal) ||
                    property.propertyPath == "selectedCell" || property.propertyPath == "showPreview" ||
                    property.propertyPath == "showExclusions" || property.propertyPath == "maximumPreviewSamples") continue;
                string value;
                switch (property.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        UnityEngine.Object referenced = property.objectReferenceValue;
                        if (referenced == null) { value = "null"; break; }
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(referenced, out string guid, out long localId))
                            value = guid + ":" + localId + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(referenced));
                        else value = referenced.GetType().FullName + ":" + referenced.name;
                        break;
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.LayerMask: value = property.intValue.ToString(CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Boolean: value = property.boolValue ? "1" : "0"; break;
                    case SerializedPropertyType.Float: value = property.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.String: value = property.stringValue; break;
                    case SerializedPropertyType.Vector2:
                        value = property.vector2Value.x.ToString("R", CultureInfo.InvariantCulture) + "," + property.vector2Value.y.ToString("R", CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Vector3: value = VectorText(property.vector3Value); break;
                    case SerializedPropertyType.Bounds: value = VectorText(property.boundsValue.center) + ";" + VectorText(property.boundsValue.size); break;
                    default: continue;
                }
                Append(hash, property.propertyPath + "=" + value + "\n");
            }
        }

        private static string VectorText(Vector3 value) => value.x.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.y.ToString("R", CultureInfo.InvariantCulture) + "," + value.z.ToString("R", CultureInfo.InvariantCulture);

        private static void Append(HashAlgorithm hash, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);
            hash.TransformBlock(data, 0, data.Length, null, 0);
        }

        public readonly struct ForestNeighborhood
        {
            public readonly int TreeCount;
            public readonly int OccupiedSectors;
            public readonly int SectorCount;
            public readonly float LargestEmptyArcDegrees;
            public readonly float NearestTreeDistanceMeters;

            public ForestNeighborhood(int treeCount, int occupiedSectors,
                int sectorCount, float largestEmptyArcDegrees,
                float nearestTreeDistanceMeters)
            {
                TreeCount = treeCount;
                OccupiedSectors = occupiedSectors;
                SectorCount = sectorCount;
                LargestEmptyArcDegrees = largestEmptyArcDegrees;
                NearestTreeDistanceMeters = nearestTreeDistanceMeters;
            }

            public bool HasMultidirectionalEvidence(int minimumTrees,
                float maximumEmptyArcDegrees) =>
                TreeCount >= minimumTrees && OccupiedSectors >= 3 &&
                LargestEmptyArcDegrees < maximumEmptyArcDegrees - 0.001f;

            public float SmoothInteriorDensityFactor(int minimumTrees,
                float maximumEmptyArcDegrees)
            {
                if (!HasMultidirectionalEvidence(minimumTrees,
                        maximumEmptyArcDegrees)) return 0f;
                float count = Mathf.InverseLerp(minimumTrees,
                    minimumTrees + 7f, TreeCount);
                float sectors = Mathf.InverseLerp(3f,
                    Mathf.Min(SectorCount, 8), OccupiedSectors);
                float arc = Mathf.InverseLerp(maximumEmptyArcDegrees,
                    Mathf.Max(55f, maximumEmptyArcDegrees * 0.52f),
                    LargestEmptyArcDegrees);
                float confidence = count * 0.45f + sectors * 0.25f +
                                   arc * 0.30f;
                // Fringe gaps remain possible but thin smoothly. Deep canopy
                // reaches the authored density instead of being punched into
                // isolated binary patches.
                return Mathf.Lerp(0.58f, 1f, Mathf.Clamp01(confidence));
            }
        }

        public static float StableForestClusterFactor(Vector3 position,
            float clusterScaleMeters, int seed)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.z) ||
                !float.IsFinite(clusterScaleMeters) || clusterScaleMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(clusterScaleMeters));
            float scaledX = position.x / clusterScaleMeters;
            float scaledZ = position.z / clusterScaleMeters;
            int x = Mathf.FloorToInt(scaledX);
            int z = Mathf.FloorToInt(scaledZ);
            float tx = scaledX - x;
            float tz = scaledZ - z;
            tx = tx * tx * (3f - 2f * tx);
            tz = tz * tz * (3f - 2f * tz);
            float a = VegetationStableHash.ToUnitFloat(
                VegetationStableHash.Hash(x, z, seed));
            float b = VegetationStableHash.ToUnitFloat(
                VegetationStableHash.Hash(x + 1, z, seed));
            float c = VegetationStableHash.ToUnitFloat(
                VegetationStableHash.Hash(x, z + 1, seed));
            float d = VegetationStableHash.ToUnitFloat(
                VegetationStableHash.Hash(x + 1, z + 1, seed));
            float noise = Mathf.Lerp(Mathf.Lerp(a, b, tx),
                Mathf.Lerp(c, d, tx), tz);
            // Gentle low-frequency clumping hides the candidate lattice while
            // retaining enough probability to avoid new bald islands.
            return Mathf.Lerp(0.72f, 1.08f, noise);
        }

        public static int NaturalInfillBudget(int originalTreeCount,
            float maximumOriginalFraction)
        {
            if (originalTreeCount < 0 ||
                !float.IsFinite(maximumOriginalFraction) ||
                maximumOriginalFraction < 0f ||
                maximumOriginalFraction > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumOriginalFraction));
            return Mathf.FloorToInt(originalTreeCount *
                                    maximumOriginalFraction);
        }

        public sealed class SpacingIndex
        {
            private readonly float bucketSize;
            private readonly Dictionary<Vector2Int, List<Vector3>> buckets = new Dictionary<Vector2Int, List<Vector3>>();
            public SpacingIndex(float spacing) { bucketSize = Mathf.Max(0.1f, spacing); }
            public void Add(Vector3 p)
            {
                var key = Key(p);
                if (!buckets.TryGetValue(key, out List<Vector3> values)) buckets[key] = values = new List<Vector3>();
                values.Add(p);
            }
            public bool HasNeighbor(Vector3 p, float distance)
            {
                Vector2Int key = Key(p);
                int range = Mathf.CeilToInt(distance / bucketSize);
                for (int z = key.y - range; z <= key.y + range; z++)
                for (int x = key.x - range; x <= key.x + range; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, z), out List<Vector3> values))
                        foreach (Vector3 q in values)
                            if ((p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z) < distance * distance) return true;
                return false;
            }
            public bool HasSurroundingNeighbors(Vector3 p, float radius,
                int minimumCount = 4, int angularSectorCount = 8,
                float maximumEmptyArcDegrees = 180f)
            {
                if (!float.IsFinite(radius) || radius <= 0f ||
                    minimumCount < 1 || angularSectorCount < 3 ||
                    !float.IsFinite(maximumEmptyArcDegrees) ||
                    maximumEmptyArcDegrees <= 0f ||
                    maximumEmptyArcDegrees > 360f)
                    throw new ArgumentOutOfRangeException(nameof(radius));
                var angles = new List<float>();
                var occupied = new bool[angularSectorCount];
                Vector2Int key = Key(p);
                int range = Mathf.CeilToInt(radius / bucketSize);
                float radiusSquared = radius * radius;
                for (int z = key.y - range; z <= key.y + range; z++)
                for (int x = key.x - range; x <= key.x + range; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, z),
                            out List<Vector3> values))
                        foreach (Vector3 q in values)
                        {
                            float dx = q.x - p.x;
                            float dz = q.z - p.z;
                            float squared = dx * dx + dz * dz;
                            if (squared > 0.0001f &&
                                squared <= radiusSquared)
                                AddDirection(dx, dz, angularSectorCount,
                                    angles, occupied);
                        }
                return HasSurroundingAngles(angles, occupied, minimumCount,
                    maximumEmptyArcDegrees);
            }

            public ForestNeighborhood MeasureForestNeighborhood(Vector3 p,
                float radius, int angularSectorCount)
            {
                if (!float.IsFinite(p.x) || !float.IsFinite(p.z) ||
                    !float.IsFinite(radius) || radius <= 0f ||
                    angularSectorCount < 3 || angularSectorCount > 32)
                    throw new ArgumentOutOfRangeException(nameof(radius));
                int count = 0;
                uint coarseMask = 0u;
                ulong fine0 = 0ul, fine1 = 0ul, fine2 = 0ul;
                float nearestSquared = float.PositiveInfinity;
                Vector2Int key = Key(p);
                int range = Mathf.CeilToInt(radius / bucketSize);
                float radiusSquared = radius * radius;
                for (int z = key.y - range; z <= key.y + range; z++)
                for (int x = key.x - range; x <= key.x + range; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, z),
                            out List<Vector3> values))
                        foreach (Vector3 q in values)
                        {
                            float dx = q.x - p.x;
                            float dz = q.z - p.z;
                            float squared = dx * dx + dz * dz;
                            if (squared <= 0.0001f || squared > radiusSquared)
                                continue;
                            count++;
                            nearestSquared = Mathf.Min(nearestSquared, squared);
                            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
                            if (angle < 0f) angle += 360f;
                            int sector = Mathf.Min(angularSectorCount - 1,
                                Mathf.FloorToInt(angle / 360f *
                                    angularSectorCount));
                            coarseMask |= 1u << sector;
                            int fine = Mathf.Min(179,
                                Mathf.FloorToInt(angle * 0.5f));
                            if (fine < 64) fine0 |= 1ul << fine;
                            else if (fine < 128) fine1 |= 1ul << (fine - 64);
                            else fine2 |= 1ul << (fine - 128);
                        }

                if (count == 0)
                    return new ForestNeighborhood(0, 0,
                        angularSectorCount, 360f, float.PositiveInfinity);
                int largestEmptyRun = 0;
                int currentEmptyRun = 0;
                // Two laps capture a gap crossing north; cap at one complete
                // circle so a single sample reports 360 degrees, not 718.
                for (int index = 0; index < 360; index++)
                {
                    int fine = index % 180;
                    bool occupied = fine < 64
                        ? (fine0 & (1ul << fine)) != 0ul
                        : fine < 128
                            ? (fine1 & (1ul << (fine - 64))) != 0ul
                            : (fine2 & (1ul << (fine - 128))) != 0ul;
                    if (occupied) currentEmptyRun = 0;
                    else
                    {
                        currentEmptyRun = Mathf.Min(179,
                            currentEmptyRun + 1);
                        largestEmptyRun = Mathf.Max(largestEmptyRun,
                            currentEmptyRun);
                    }
                }
                float largestEmptyArc = Mathf.Min(360f,
                    (largestEmptyRun + 1) * 2f);
                return new ForestNeighborhood(count, CountBits(coarseMask),
                    angularSectorCount, largestEmptyArc,
                    Mathf.Sqrt(nearestSquared));
            }

            private static int CountBits(uint value)
            {
                int count = 0;
                while (value != 0u)
                {
                    value &= value - 1u;
                    count++;
                }
                return count;
            }
            private Vector2Int Key(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / bucketSize), Mathf.FloorToInt(p.z / bucketSize));
        }

        public static bool HasSurroundingDonorForest(Vector3 candidate,
            IEnumerable<Vector3> donorTrees, float radius,
            int minimumCount = 4, int angularSectorCount = 8,
            float maximumEmptyArcDegrees = 180f)
        {
            if (donorTrees == null) throw new ArgumentNullException(
                nameof(donorTrees));
            if (!float.IsFinite(candidate.x) ||
                !float.IsFinite(candidate.z) ||
                !float.IsFinite(radius) || radius <= 0f ||
                minimumCount < 1 || angularSectorCount < 3 ||
                !float.IsFinite(maximumEmptyArcDegrees) ||
                maximumEmptyArcDegrees <= 0f ||
                maximumEmptyArcDegrees > 360f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            float radiusSquared = radius * radius;
            var angles = new List<float>();
            var occupied = new bool[angularSectorCount];
            foreach (Vector3 tree in donorTrees)
            {
                if (!float.IsFinite(tree.x) || !float.IsFinite(tree.z))
                    continue;
                float dx = tree.x - candidate.x;
                float dz = tree.z - candidate.z;
                float squared = dx * dx + dz * dz;
                if (squared <= 0.0001f || squared > radiusSquared)
                    continue;
                AddDirection(dx, dz, angularSectorCount, angles, occupied);
            }
            return HasSurroundingAngles(angles, occupied, minimumCount,
                maximumEmptyArcDegrees);
        }

        private static void AddDirection(float dx, float dz,
            int angularSectorCount, ICollection<float> angles,
            bool[] occupied)
        {
            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            angles.Add(angle);
            int sector = Mathf.Min(angularSectorCount - 1,
                Mathf.FloorToInt(angle / 360f * angularSectorCount));
            occupied[sector] = true;
        }

        private static bool HasSurroundingAngles(List<float> angles,
            bool[] occupied, int minimumCount,
            float maximumEmptyArcDegrees)
        {
            if (angles.Count < minimumCount ||
                occupied.Count(value => value) < 3)
                return false;
            angles.Sort();
            float largestGap = angles[0] + 360f -
                               angles[angles.Count - 1];
            for (int index = 1; index < angles.Count; index++)
                largestGap = Mathf.Max(largestGap,
                    angles[index] - angles[index - 1]);
            // Exactly a half-circle still permits all evidence on one side.
            return largestGap < maximumEmptyArcDegrees - 0.001f;
        }
    }
}
