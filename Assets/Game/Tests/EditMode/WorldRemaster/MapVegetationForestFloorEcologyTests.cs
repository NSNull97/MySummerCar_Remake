using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationForestFloorEcologyTests
    {
        [Test]
        public void Plan_UsesOverlappingAcceptedCanopiesAndNeverEmitsOrphanSpots()
        {
            var trees = new[]
            {
                Tree("spruce-a", new Vector3(0f, 0f, 0f), "Spruce"),
                Tree("pine-b", new Vector3(6f, 0f, 0f), "Pine"),
                Tree("birch-a", new Vector3(32f, 0f, 0f), "Birch"),
                Tree("aspen-b", new Vector3(38f, 0f, 0f), "Aspen"),
                Tree("isolated", new Vector3(100f, 0f, 100f), "Spruce")
            };

            // This fixture seed keeps one litter sample clear of each pair's
            // trunk footprints, so both species-bound litter branches are
            // exercised instead of being legitimately rejected on clearance.
            MapVegetationForestFloorEcologyPlan plan =
                MapVegetationForestFloorEcology.Plan(trees, 20260832,
                    512f, 1000);

            Assert.That(plan.Clusters, Is.Not.Empty);
            Assert.That(plan.Clusters.All(cluster =>
                    cluster.Candidates.Count >=
                    MapVegetationForestFloorEcology
                        .MinimumCandidatesPerCluster), Is.True);
            Assert.That(plan.Clusters.All(cluster =>
                    cluster.CanopyOverlap >= 2), Is.True);
            Assert.That(plan.Clusters.SelectMany(cluster =>
                    cluster.Candidates).All(candidate =>
                    candidate.CanopyInfluence > 0f &&
                    !candidate.Id.Contains("rock")), Is.True);
            Assert.That(plan.Report.RejectionCount("IsolatedCanopy"),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(plan.Clusters.SelectMany(cluster => cluster.Candidates)
                    .Any(candidate => candidate.Semantic ==
                        MapVegetationForestFloorSemantic.ConiferLitter),
                Is.True);
            Assert.That(plan.Clusters.SelectMany(cluster => cluster.Candidates)
                    .Any(candidate => candidate.Semantic ==
                        MapVegetationForestFloorSemantic.DeciduousLitter),
                Is.True);
        }

        [Test]
        public void Plan_IsRepeatableAndKeepsWholeClustersInsideCellBudget()
        {
            List<MapVegetationPlacement> trees = DenseStand(20);
            MapVegetationForestFloorEcologyPlan first =
                MapVegetationForestFloorEcology.Plan(trees, 7719, 512f, 12);
            MapVegetationForestFloorEcologyPlan second =
                MapVegetationForestFloorEcology.Plan(trees, 7719, 512f, 12);

            string[] firstRecords = first.Clusters
                .SelectMany(cluster => cluster.Candidates)
                .Select(candidate => candidate.Id + "|" +
                    candidate.Position.ToString("R") + "|" +
                    candidate.Semantic).ToArray();
            string[] secondRecords = second.Clusters
                .SelectMany(cluster => cluster.Candidates)
                .Select(candidate => candidate.Id + "|" +
                    candidate.Position.ToString("R") + "|" +
                    candidate.Semantic).ToArray();

            Assert.That(secondRecords, Is.EqualTo(firstRecords));
            Assert.That(first.Clusters.SelectMany(cluster =>
                    cluster.Candidates).GroupBy(candidate => candidate.CellId)
                .All(group => group.Count() <= 12), Is.True);
            Assert.That(first.Clusters.All(cluster =>
                    cluster.Candidates.Count >= 2), Is.True);
            Assert.That(first.Report.RejectionCount("PerCellBudget"),
                Is.GreaterThan(0));
        }

        [Test]
        public void DenseCanopyOverlap_ProducesMoreLocalStructureThanOnePair()
        {
            MapVegetationForestFloorEcologyPlan pair =
                MapVegetationForestFloorEcology.Plan(new[]
                {
                    Tree("a", new Vector3(0f, 0f, 0f), "Spruce"),
                    Tree("b", new Vector3(6f, 0f, 0f), "Pine")
                }, 81, 512f, 1000);
            MapVegetationForestFloorEcologyPlan dense =
                MapVegetationForestFloorEcology.Plan(DenseStand(8), 81,
                    512f, 1000);

            Assert.That(pair.Clusters, Has.Count.EqualTo(1));
            Assert.That(dense.Clusters, Is.Not.Empty);
            Assert.That(dense.Clusters.Max(cluster => cluster.CanopyOverlap),
                Is.GreaterThan(pair.Clusters.Max(cluster =>
                    cluster.CanopyOverlap)));
            Assert.That(dense.Report.PlannedCandidateCount,
                Is.GreaterThan(pair.Report.PlannedCandidateCount));
        }

        [Test]
        public void SemanticBindings_KeepCoverAndSpeciesLitterInReviewedFamilies()
        {
            const int seed = 20260831;
            string cover = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:ground-cover:0", seed);
            string conifer = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:conifer-litter:0", seed);
            string deciduous = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:deciduous-litter:0", seed);
            string debris = MapVegetationForestFloorBindings.SelectSourcePath(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:woody-debris:0", seed);

            Assert.That(cover, Does.Not.Contain("detail_branches"));
            Assert.That(cover, Does.Not.Contain("detail_poplar_leaves"));
            Assert.That(conifer,
                Does.EndWith("prefab_detail_branches_01.prefab"));
            Assert.That(deciduous,
                Does.EndWith("prefab_detail_poplar_leaves_01_1.prefab"));
            Assert.That(debris,
                Does.EndWith("prefab_detail_branches_01.prefab"));
            Assert.That(new[] { cover, conifer, deciduous, debris }.All(path =>
                    !path.Contains("Rock") && !path.Contains("stone")),
                Is.True);

            var debrisPolicy = MapVegetationForestFloorBindings.Policy(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:woody-debris:0");
            var coverPolicy = MapVegetationForestFloorBindings.Policy(
                MapVegetationForestFloorBindings.Pool.Understory,
                "forest-floor:ecology:a:ground-cover:0");
            Assert.That(debrisPolicy.MinimumSpacingMeters,
                Is.GreaterThan(coverPolicy.MinimumSpacingMeters));
            Assert.That(debrisPolicy.UniformScaleRange.x,
                Is.GreaterThan(coverPolicy.UniformScaleRange.x));
            Assert.That(debrisPolicy.SurfaceOffsetMeters,
                Is.EqualTo(coverPolicy.SurfaceOffsetMeters),
                "The existing saved-cell surface-offset validator uses the common understory contact contract.");
        }

        private static List<MapVegetationPlacement> DenseStand(int count)
        {
            var result = new List<MapVegetationPlacement>();
            for (int index = 0; index < count; index++)
            {
                int x = index % 4;
                int z = index / 4;
                string species = index % 4 == 0 ? "Birch" :
                    index % 4 == 1 ? "Aspen" :
                    index % 4 == 2 ? "Spruce" : "Pine";
                result.Add(Tree("tree-" + index,
                    new Vector3(x * 5f, 0f, z * 5f), species));
            }
            return result;
        }

        private static MapVegetationPlacement Tree(string id,
            Vector3 position, string species)
        {
            return new MapVegetationPlacement
            {
                id = id,
                source = "accepted-test-tree",
                method = "test",
                species = species,
                cellId = "cell_0_0",
                category = MapVegetationCategories.OriginalTrees,
                sourcePosition = position,
                position = position,
                normal = Vector3.up,
                height = 18f
            };
        }
    }
}
