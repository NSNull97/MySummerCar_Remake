using System;
using System.Collections.Generic;
using System.Linq;
using MSC.World.Partition;
using MSC.World.Vegetation;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    public enum MapVegetationForestFloorSemantic
    {
        General = 0,
        GroundCover = 1,
        ConiferLitter = 2,
        DeciduousLitter = 3,
        WoodyDebris = 4
    }

    public sealed class MapVegetationForestFloorEcologyReport
    {
        private readonly Dictionary<string, int> rejections =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<MapVegetationForestFloorSemantic, int>
            acceptedBySemantic =
                new Dictionary<MapVegetationForestFloorSemantic, int>();

        public int AcceptedTreeCount { get; internal set; }
        public int PairCandidateCount { get; internal set; }
        public int PlannedClusterCount { get; internal set; }
        public int PlannedCandidateCount { get; internal set; }
        public int AcceptedClusterCount { get; private set; }
        public int AcceptedCandidateCount { get; private set; }
        public IReadOnlyDictionary<string, int> Rejections => rejections;
        public IReadOnlyDictionary<MapVegetationForestFloorSemantic, int>
            AcceptedBySemantic => acceptedBySemantic;

        internal void Reject(string reason, int count = 1)
        {
            if (count <= 0) return;
            reason = string.IsNullOrWhiteSpace(reason)
                ? "Unknown" : reason;
            rejections[reason] = rejections.TryGetValue(reason,
                out int current) ? current + count : count;
        }

        internal void AcceptCluster(
            IEnumerable<MapVegetationForestFloorEcologyCandidate> candidates)
        {
            AcceptedClusterCount++;
            foreach (MapVegetationForestFloorEcologyCandidate candidate in
                     candidates)
            {
                AcceptedCandidateCount++;
                acceptedBySemantic[candidate.Semantic] =
                    acceptedBySemantic.TryGetValue(candidate.Semantic,
                        out int current) ? current + 1 : 1;
            }
        }

        public int RejectionCount(string reason) =>
            rejections.TryGetValue(reason, out int count) ? count : 0;

        public int AcceptedCount(
            MapVegetationForestFloorSemantic semantic) =>
            acceptedBySemantic.TryGetValue(semantic, out int count)
                ? count : 0;

        public string Summary =>
            $"trees={AcceptedTreeCount} pairs={PairCandidateCount} " +
            $"plannedClusters={PlannedClusterCount} " +
            $"plannedCandidates={PlannedCandidateCount} " +
            $"acceptedClusters={AcceptedClusterCount} " +
            $"acceptedCandidates={AcceptedCandidateCount} " +
            "rejections=" + string.Join(",", rejections
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value));
    }

    public readonly struct MapVegetationForestFloorEcologyCandidate
    {
        public readonly string Id;
        public readonly string ClusterId;
        public readonly string CellId;
        public readonly string SourceTreeA;
        public readonly string SourceTreeB;
        public readonly Vector3 Position;
        public readonly MapVegetationForestFloorSemantic Semantic;
        public readonly int CanopyOverlap;
        public readonly float CanopyInfluence;

        public MapVegetationForestFloorEcologyCandidate(
            string id,
            string clusterId,
            string cellId,
            string sourceTreeA,
            string sourceTreeB,
            Vector3 position,
            MapVegetationForestFloorSemantic semantic,
            int canopyOverlap,
            float canopyInfluence)
        {
            Id = id;
            ClusterId = clusterId;
            CellId = cellId;
            SourceTreeA = sourceTreeA;
            SourceTreeB = sourceTreeB;
            Position = position;
            Semantic = semantic;
            CanopyOverlap = canopyOverlap;
            CanopyInfluence = canopyInfluence;
        }
    }

    public sealed class MapVegetationForestFloorEcologyCluster
    {
        public string Id { get; internal set; }
        public int CanopyOverlap { get; internal set; }
        public float CanopyInfluence { get; internal set; }
        public uint StableScore { get; internal set; }
        public List<MapVegetationForestFloorEcologyCandidate> Candidates
            { get; } =
                new List<MapVegetationForestFloorEcologyCandidate>();
    }

    public sealed class MapVegetationForestFloorEcologyPlan
    {
        public List<MapVegetationForestFloorEcologyCluster> Clusters
            { get; } =
                new List<MapVegetationForestFloorEcologyCluster>();
        public MapVegetationForestFloorEcologyReport Report { get; } =
            new MapVegetationForestFloorEcologyReport();
    }

    /// <summary>
    /// Creates collisionless, cell-budgeted forest-floor clusters only from
    /// accepted tree canopies. Clearings have no canopy contribution and thus
    /// naturally thin to zero before surface exclusions apply.
    /// </summary>
    public static class MapVegetationForestFloorEcology
    {
        public const string PolicyVersion =
            "msc.map-vegetation-forest-floor-ecology.v1";
        public const int MaximumPlacementsPerCell = 192;
        public const int MinimumCandidatesPerCluster = 2;
        public const float MinimumTrunkClearanceMeters = 1.05f;
        private const float MaximumPairDistanceMeters = 17f;
        private const float InfluenceRadiusMultiplier = 1.7f;
        private const float MinimumClusterInfluence = 0.62f;
        private const float MinimumCandidateInfluence = 0.22f;

        public static MapVegetationForestFloorEcologyPlan Plan(
            IEnumerable<MapVegetationPlacement> acceptedPlacements,
            int seed,
            float cellSizeMeters,
            int maximumPlacementsPerCell = MaximumPlacementsPerCell)
        {
            if (acceptedPlacements == null)
                throw new ArgumentNullException(nameof(acceptedPlacements));
            if (!float.IsFinite(cellSizeMeters) || cellSizeMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));
            if (maximumPlacementsPerCell < MinimumCandidatesPerCluster)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPlacementsPerCell));

            var result = new MapVegetationForestFloorEcologyPlan();
            CanopyTree[] trees = acceptedPlacements
                .Where(IsAcceptedTree)
                .Where(placement => Finite(placement.position) &&
                                    !string.IsNullOrWhiteSpace(placement.id))
                .OrderBy(placement => placement.id, StringComparer.Ordinal)
                .Select(ToCanopyTree).ToArray();
            result.Report.AcceptedTreeCount = trees.Length;
            if (trees.Length < 2) return result;

            var index = new CanopyIndex(trees);
            var pairKeys = new HashSet<string>(StringComparer.Ordinal);
            var rawClusters = new List<MapVegetationForestFloorEcologyCluster>();
            foreach (CanopyTree tree in trees)
            {
                CanopyTree nearest = default;
                float nearestSquared = float.PositiveInfinity;
                foreach (CanopyTree other in index.Query(tree.Position,
                             MaximumPairDistanceMeters))
                {
                    if (ReferenceEquals(tree.Placement, other.Placement))
                        continue;
                    float squared = HorizontalSquared(tree.Position,
                        other.Position);
                    float pairReach = Mathf.Min(MaximumPairDistanceMeters,
                        (tree.CanopyRadius + other.CanopyRadius) * 1.7f);
                    if (squared <= 0.0001f ||
                        squared > pairReach * pairReach ||
                        squared > nearestSquared + 0.0001f)
                        continue;
                    if (Mathf.Abs(squared - nearestSquared) <= 0.0001f &&
                        nearest.Placement != null &&
                        string.CompareOrdinal(other.Id, nearest.Id) >= 0)
                        continue;
                    nearest = other;
                    nearestSquared = squared;
                }

                if (nearest.Placement == null)
                {
                    result.Report.Reject("IsolatedCanopy");
                    continue;
                }

                string first = string.CompareOrdinal(tree.Id, nearest.Id) <= 0
                    ? tree.Id : nearest.Id;
                string second = string.CompareOrdinal(tree.Id, nearest.Id) <= 0
                    ? nearest.Id : tree.Id;
                string pairKey = first + "|" + second;
                if (!pairKeys.Add(pairKey)) continue;
                result.Report.PairCandidateCount++;
                MapVegetationForestFloorEcologyCluster cluster = BuildCluster(
                    tree.Id == first ? tree : nearest,
                    tree.Id == first ? nearest : tree,
                    pairKey, seed, cellSizeMeters, index, result.Report);
                if (cluster != null) rawClusters.Add(cluster);
            }

            var usedPerCell = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (MapVegetationForestFloorEcologyCluster cluster in
                     rawClusters
                         .OrderByDescending(item => item.CanopyOverlap)
                         .ThenByDescending(item => item.CanopyInfluence)
                         .ThenBy(item => item.StableScore)
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                IGrouping<string,
                    MapVegetationForestFloorEcologyCandidate>[] perCell =
                    cluster.Candidates.GroupBy(candidate => candidate.CellId,
                        StringComparer.Ordinal).ToArray();
                bool fits = perCell.All(group =>
                    (usedPerCell.TryGetValue(group.Key, out int used)
                        ? used : 0) + group.Count() <=
                    maximumPlacementsPerCell);
                if (!fits)
                {
                    result.Report.Reject("PerCellBudget",
                        cluster.Candidates.Count);
                    continue;
                }

                foreach (IGrouping<string,
                             MapVegetationForestFloorEcologyCandidate> group in
                         perCell)
                    usedPerCell[group.Key] =
                        (usedPerCell.TryGetValue(group.Key, out int used)
                            ? used : 0) + group.Count();
                result.Clusters.Add(cluster);
            }

            result.Report.PlannedClusterCount = result.Clusters.Count;
            result.Report.PlannedCandidateCount = result.Clusters.Sum(
                cluster => cluster.Candidates.Count);
            return result;
        }

        public static float MinimumSpacingMeters(
            MapVegetationForestFloorSemantic semantic)
        {
            switch (semantic)
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return 1.8f;
                case MapVegetationForestFloorSemantic.ConiferLitter:
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return 2.1f;
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return 9f;
                default:
                    return 2.1f;
            }
        }

        private static MapVegetationForestFloorEcologyCluster BuildCluster(
            CanopyTree first,
            CanopyTree second,
            string pairKey,
            int seed,
            float cellSizeMeters,
            CanopyIndex index,
            MapVegetationForestFloorEcologyReport report)
        {
            uint pairHash = MapVegetationPlanning.HashId(pairKey,
                seed ^ 0x4E371);
            Vector3 delta = second.Position - first.Position;
            delta.y = 0f;
            Vector3 perpendicular = delta.sqrMagnitude > 0.0001f
                ? new Vector3(-delta.z, 0f, delta.x).normalized
                : Vector3.right;
            float signedJitter =
                VegetationStableHash.ToUnitFloat(pairHash) * 2f - 1f;
            Vector3 centre = Vector3.Lerp(first.Position, second.Position,
                0.5f) + perpendicular * signedJitter *
                Mathf.Min(first.CanopyRadius, second.CanopyRadius) * 0.18f;
            CanopySample sample = index.Sample(centre);
            if (sample.Overlap < 2)
            {
                report.Reject("InsufficientCanopyOverlap");
                return null;
            }
            if (sample.TotalInfluence < MinimumClusterInfluence)
            {
                report.Reject("LowClusterCanopyInfluence");
                return null;
            }

            string clusterId = "forest-floor:ecology:" +
                               pairHash.ToString("x8");
            var cluster = new MapVegetationForestFloorEcologyCluster
            {
                Id = clusterId,
                CanopyOverlap = sample.Overlap,
                CanopyInfluence = sample.TotalInfluence,
                StableScore = VegetationStableHash.Hash(pairHash ^
                    0x95AB61D3u)
            };
            int coverCount = sample.Overlap >= 4 ? 3 : 2;
            int litterCount = sample.Overlap >= 5 ? 3 : 2;
            for (int indexInCluster = 0;
                 indexInCluster < coverCount; indexInCluster++)
                TryAddCandidate(cluster, centre, first, second,
                    MapVegetationForestFloorSemantic.GroundCover,
                    indexInCluster, coverCount, 0x11C7u, seed,
                    cellSizeMeters, index, report);

            float deciduousFraction = sample.TotalInfluence > 0.0001f
                ? sample.DeciduousInfluence / sample.TotalInfluence : 0f;
            for (int indexInCluster = 0;
                 indexInCluster < litterCount; indexInCluster++)
            {
                uint semanticHash = VegetationStableHash.Hash(pairHash ^
                    (uint)(indexInCluster * 0x19E7 + 0x63B1));
                MapVegetationForestFloorSemantic semantic =
                    VegetationStableHash.ToUnitFloat(semanticHash) <
                    deciduousFraction
                        ? MapVegetationForestFloorSemantic.DeciduousLitter
                        : MapVegetationForestFloorSemantic.ConiferLitter;
                TryAddCandidate(cluster, centre, first, second, semantic,
                    indexInCluster, litterCount, 0x48D1u, seed,
                    cellSizeMeters, index, report);
            }

            float debrisChance = Mathf.Clamp01(0.08f +
                (sample.Overlap - 2) * 0.035f);
            if (VegetationStableHash.ToUnitFloat(
                    VegetationStableHash.Hash(pairHash ^ 0xCB751u)) <
                debrisChance)
                TryAddCandidate(cluster, centre, first, second,
                    MapVegetationForestFloorSemantic.WoodyDebris,
                    0, 1, 0x7A91u, seed, cellSizeMeters, index, report);

            if (cluster.Candidates.Count < MinimumCandidatesPerCluster)
            {
                report.Reject("ClusterBelowMinimum",
                    cluster.Candidates.Count);
                return null;
            }
            return cluster;
        }

        private static void TryAddCandidate(
            MapVegetationForestFloorEcologyCluster cluster,
            Vector3 centre,
            CanopyTree first,
            CanopyTree second,
            MapVegetationForestFloorSemantic semantic,
            int indexInCluster,
            int semanticCount,
            uint salt,
            int seed,
            float cellSizeMeters,
            CanopyIndex index,
            MapVegetationForestFloorEcologyReport report)
        {
            string marker = SemanticMarker(semantic);
            string id = cluster.Id + ":" + marker + ":" +
                        indexInCluster;
            uint hash = MapVegetationPlanning.HashId(id, seed ^
                unchecked((int)salt));
            float baseAngle = VegetationStableHash.ToUnitFloat(hash) *
                              Mathf.PI * 2f;
            float radiusMinimum = semantic ==
                MapVegetationForestFloorSemantic.WoodyDebris ? 1.5f : 0.75f;
            float radiusMaximum = semantic ==
                MapVegetationForestFloorSemantic.GroundCover ? 3.3f : 2.9f;
            if (semantic == MapVegetationForestFloorSemantic.WoodyDebris)
                radiusMaximum = 3.8f;
            float radial = Mathf.Sqrt(VegetationStableHash.ToUnitFloat(
                VegetationStableHash.Hash(hash ^ 0x51B4D2E9u)));
            float radius = Mathf.Lerp(radiusMinimum, radiusMaximum, radial);
            float angle = baseAngle + indexInCluster *
                Mathf.PI * 2f / Mathf.Max(1, semanticCount);
            Vector3 position = centre + new Vector3(Mathf.Cos(angle) * radius,
                0f, Mathf.Sin(angle) * radius);
            CanopySample sample = index.Sample(position);
            if (sample.TotalInfluence < MinimumCandidateInfluence)
            {
                report.Reject("CandidateOutsideCanopyFade");
                return;
            }
            if (index.DistanceToNearestTrunk(position) <
                MinimumTrunkClearanceMeters)
            {
                report.Reject("TrunkFootprint");
                return;
            }

            WorldCellIndex cell = MapVegetationPlanning.CellAt(position,
                cellSizeMeters);
            cluster.Candidates.Add(
                new MapVegetationForestFloorEcologyCandidate(id,
                    cluster.Id, cell.Id, first.Id, second.Id, position,
                    semantic, sample.Overlap, sample.TotalInfluence));
        }

        private static string SemanticMarker(
            MapVegetationForestFloorSemantic semantic)
        {
            switch (semantic)
            {
                case MapVegetationForestFloorSemantic.GroundCover:
                    return "ground-cover";
                case MapVegetationForestFloorSemantic.ConiferLitter:
                    return "conifer-litter";
                case MapVegetationForestFloorSemantic.DeciduousLitter:
                    return "deciduous-litter";
                case MapVegetationForestFloorSemantic.WoodyDebris:
                    return "woody-debris";
                default:
                    return "general";
            }
        }

        private static bool IsAcceptedTree(MapVegetationPlacement placement)
        {
            return placement != null &&
                   (placement.category ==
                        MapVegetationCategories.OriginalTrees ||
                    placement.category ==
                        MapVegetationCategories.BoundaryForest);
        }

        private static CanopyTree ToCanopyTree(
            MapVegetationPlacement placement)
        {
            string species = placement.species ?? string.Empty;
            float height = Mathf.Clamp(placement.height, 3f, 30f);
            float radius;
            if (species.Equals("Spruce", StringComparison.OrdinalIgnoreCase))
                radius = Mathf.Clamp(height * 0.24f, 2.8f, 6.5f);
            else if (species.Equals("Pine",
                         StringComparison.OrdinalIgnoreCase))
                radius = Mathf.Clamp(height * 0.22f, 2.8f, 6.2f);
            else if (species.Equals("Birch",
                         StringComparison.OrdinalIgnoreCase))
                radius = Mathf.Clamp(height * 0.20f, 2.5f, 5.2f);
            else
                radius = Mathf.Clamp(height * 0.19f, 2.4f, 5f);
            bool deciduous = species.Equals("Birch",
                                  StringComparison.OrdinalIgnoreCase) ||
                              species.Equals("Aspen",
                                  StringComparison.OrdinalIgnoreCase);
            return new CanopyTree(placement, radius, deciduous);
        }

        private static bool Finite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static float HorizontalSquared(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return x * x + z * z;
        }

        private readonly struct CanopyTree
        {
            public readonly MapVegetationPlacement Placement;
            public readonly float CanopyRadius;
            public readonly bool Deciduous;
            public string Id => Placement != null ? Placement.id : string.Empty;
            public Vector3 Position => Placement != null
                ? Placement.position : default;

            public CanopyTree(MapVegetationPlacement placement,
                float canopyRadius, bool deciduous)
            {
                Placement = placement;
                CanopyRadius = canopyRadius;
                Deciduous = deciduous;
            }
        }

        private readonly struct CanopySample
        {
            public readonly int Overlap;
            public readonly float TotalInfluence;
            public readonly float DeciduousInfluence;

            public CanopySample(int overlap, float totalInfluence,
                float deciduousInfluence)
            {
                Overlap = overlap;
                TotalInfluence = totalInfluence;
                DeciduousInfluence = deciduousInfluence;
            }
        }

        private sealed class CanopyIndex
        {
            private const float BucketSize = 12f;
            private readonly Dictionary<Vector2Int, List<CanopyTree>> buckets =
                new Dictionary<Vector2Int, List<CanopyTree>>();

            public CanopyIndex(IEnumerable<CanopyTree> trees)
            {
                foreach (CanopyTree tree in trees)
                {
                    Vector2Int key = Key(tree.Position);
                    if (!buckets.TryGetValue(key,
                            out List<CanopyTree> values))
                        buckets[key] = values = new List<CanopyTree>();
                    values.Add(tree);
                }
            }

            public IEnumerable<CanopyTree> Query(Vector3 position,
                float radius)
            {
                Vector2Int key = Key(position);
                int range = Mathf.CeilToInt(radius / BucketSize);
                float radiusSquared = radius * radius;
                for (int z = key.y - range; z <= key.y + range; z++)
                for (int x = key.x - range; x <= key.x + range; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, z),
                            out List<CanopyTree> values))
                        foreach (CanopyTree tree in values)
                            if (HorizontalSquared(position, tree.Position) <=
                                radiusSquared)
                                yield return tree;
            }

            public CanopySample Sample(Vector3 position)
            {
                int overlap = 0;
                float total = 0f;
                float deciduous = 0f;
                foreach (CanopyTree tree in Query(position,
                             MaximumPairDistanceMeters))
                {
                    float distance = Mathf.Sqrt(HorizontalSquared(position,
                        tree.Position));
                    float reach = tree.CanopyRadius *
                                  InfluenceRadiusMultiplier;
                    if (distance > reach) continue;
                    float normalized = distance /
                        Mathf.Max(0.01f, reach);
                    float influence = 1f - normalized * normalized *
                        (3f - 2f * normalized);
                    if (influence <= 0f) continue;
                    overlap++;
                    total += influence;
                    if (tree.Deciduous) deciduous += influence;
                }
                return new CanopySample(overlap, total, deciduous);
            }

            public float DistanceToNearestTrunk(Vector3 position)
            {
                float nearestSquared = float.PositiveInfinity;
                foreach (CanopyTree tree in Query(position, 4f))
                    nearestSquared = Mathf.Min(nearestSquared,
                        HorizontalSquared(position, tree.Position));
                return float.IsPositiveInfinity(nearestSquared)
                    ? float.PositiveInfinity : Mathf.Sqrt(nearestSquared);
            }

            private static Vector2Int Key(Vector3 position) =>
                new Vector2Int(Mathf.FloorToInt(position.x / BucketSize),
                    Mathf.FloorToInt(position.z / BucketSize));
        }
    }
}
