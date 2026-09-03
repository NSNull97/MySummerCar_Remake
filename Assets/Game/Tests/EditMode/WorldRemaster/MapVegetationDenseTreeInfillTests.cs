using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationDenseTreeInfillTests
    {
        [Test]
        public void DensePolicy_IsBoundedClusteredAndKeepsHardClearances()
        {
            MapVegetationRebuildOptions options = AssetDatabase
                .LoadAssetAtPath<MapVegetationRebuildOptions>(
                    MapVegetationRebuildOptions.AssetPath);
            Assert.That(options, Is.Not.Null);
            Assert.That(options.NaturalInfillSpacingMeters,
                Is.EqualTo(8.5f));
            Assert.That(options.NaturalInfillDensity,
                Is.EqualTo(0.94f));
            Assert.That(options.NaturalInfillMinimumTreeDistanceMeters,
                Is.EqualTo(5.75f));
            Assert.That(options.NaturalInfillForestInfluenceMeters,
                Is.EqualTo(72f));
            Assert.That(options.NaturalInfillMaximumOriginalFraction,
                Is.EqualTo(0.65f));
            Assert.That(MapVegetationPlanning.NaturalInfillPolicyVersion,
                Is.EqualTo("msc.natural-tree-infill.v3-cap65"));
            Assert.That(options.NaturalInfillMinimumDonorTrees,
                Is.EqualTo(3));
            Assert.That(options.NaturalInfillAngularSectorCount,
                Is.EqualTo(12));
            Assert.That(options.NaturalInfillMaximumEmptyArcDegrees,
                Is.EqualTo(180f));
            Assert.That(options.NaturalInfillClusterScaleMeters,
                Is.EqualTo(48f));
            Assert.That(options.TreeSpeciesPercentages,
                Is.EqualTo(new[] { 65f, 20f, 7.5f, 7.5f }),
                "Dense infill must still enter the exact global species quota pass.");

            foreach (MapVegetationExclusionKind exclusion in new[]
                     {
                         MapVegetationExclusionKind.AsphaltRoad,
                         MapVegetationExclusionKind.DirtRoad,
                         MapVegetationExclusionKind.Railway,
                         MapVegetationExclusionKind.Building,
                         MapVegetationExclusionKind.GarageOpening,
                         MapVegetationExclusionKind.Driveway,
                         MapVegetationExclusionKind.Bridge,
                         MapVegetationExclusionKind.Water,
                         MapVegetationExclusionKind.AgriculturalField,
                         MapVegetationExclusionKind.VehicleRoute,
                         MapVegetationExclusionKind.OpenSpace,
                         MapVegetationExclusionKind.ArtificialStructure
                     })
                Assert.That(options.Placement.GetMargin(
                    MapVegetationKind.Tree, exclusion), Is.GreaterThan(0f),
                    exclusion + " must remain a hard tree clearance.");
        }

        [Test]
        public void DensePolicy_RaisesOnlyTheEligibleInternalForestBudget()
        {
            const int freshV11OriginalBasis = 37678;
            const int freshV11Eligible = 25474;
            const int freshV11Accepted = 20722;
            int budget = MapVegetationPlanning.NaturalInfillBudget(
                freshV11OriginalBasis, 0.65f);

            Assert.That(budget, Is.EqualTo(24490));
            Assert.That(Mathf.Min(freshV11Eligible, budget),
                Is.EqualTo(24490));
            Assert.That(budget - freshV11Accepted, Is.EqualTo(3768),
                "V11 evidence supports an 18.2% infill increase without admitting any previously ineligible candidate.");
            Assert.That(MapVegetationPlanning.NaturalInfillBudget(
                    freshV11OriginalBasis, 0.55f),
                Is.EqualTo(freshV11Accepted));
        }

        [Test]
        public void DonorNeighborhood_UsesSmoothInteriorFalloffAndRejectsOneSidedEdges()
        {
            var index = new MapVegetationPlanning.SpacingIndex(72f);
            foreach (float angle in new[]
                     { 0f, 36f, 72f, 108f, 144f, 180f, 216f, 252f, 288f, 324f })
                index.Add(Polar(24f + angle % 11f, angle));
            MapVegetationPlanning.ForestNeighborhood deep = index
                .MeasureForestNeighborhood(Vector3.zero, 72f, 12);
            Assert.That(deep.HasMultidirectionalEvidence(3, 180f), Is.True);

            var fringeIndex = new MapVegetationPlanning.SpacingIndex(72f);
            foreach (float angle in new[] { 0f, 120f, 240f })
                fringeIndex.Add(Polar(38f, angle));
            MapVegetationPlanning.ForestNeighborhood fringe = fringeIndex
                .MeasureForestNeighborhood(Vector3.zero, 72f, 12);
            Assert.That(fringe.HasMultidirectionalEvidence(3, 180f), Is.True);
            Assert.That(deep.SmoothInteriorDensityFactor(3, 180f),
                Is.GreaterThan(fringe.SmoothInteriorDensityFactor(3, 180f)));
            Assert.That(fringe.SmoothInteriorDensityFactor(3, 180f),
                Is.InRange(0.58f, 1f));

            var oneSidedIndex = new MapVegetationPlanning.SpacingIndex(72f);
            foreach (float angle in new[] { -35f, -10f, 15f, 40f, 65f })
                oneSidedIndex.Add(Polar(30f, angle));
            MapVegetationPlanning.ForestNeighborhood oneSided = oneSidedIndex
                .MeasureForestNeighborhood(Vector3.zero, 72f, 12);
            Assert.That(oneSided.HasMultidirectionalEvidence(3, 180f),
                Is.False);
            Assert.That(oneSided.SmoothInteriorDensityFactor(3, 180f),
                Is.Zero);
        }

        [Test]
        public void DenseCandidates_AreDeterministicDenserThanLegacyAndRespectTrunkSpacing()
        {
            DenseCandidate[] legacy = Candidates(13f, 0.62f, false).ToArray();
            DenseCandidate[] dense = Candidates(8.5f, 0.94f, true).ToArray();
            IReadOnlyList<DenseCandidate> legacyAccepted = Select(legacy, 8f);
            IReadOnlyList<DenseCandidate> denseAccepted = Select(dense, 5.75f);
            IReadOnlyList<DenseCandidate> reversed = Select(
                dense.Reverse(), 5.75f);

            Assert.That(denseAccepted.Count,
                Is.GreaterThan(legacyAccepted.Count * 2),
                "The revised bounded policy must be a noticeable density change.");
            Assert.That(reversed.Select(item => item.Id),
                Is.EqualTo(denseAccepted.Select(item => item.Id)));
            var spacingVerifier = new MapVegetationPlanning.SpacingIndex(5.75f);
            foreach (DenseCandidate candidate in denseAccepted)
            {
                Assert.That(spacingVerifier.HasNeighbor(candidate.Position,
                    5.75f), Is.False);
                spacingVerifier.Add(candidate.Position);
            }
        }

        [Test]
        public void ClusterNoise_IsStableSmoothAndBounded()
        {
            Vector3 position = new Vector3(-137.25f, 0f, 812.5f);
            float first = MapVegetationPlanning.StableForestClusterFactor(
                position, 48f, 20260831);
            float repeat = MapVegetationPlanning.StableForestClusterFactor(
                position, 48f, 20260831);
            float nearby = MapVegetationPlanning.StableForestClusterFactor(
                position + new Vector3(0.1f, 0f, 0.1f), 48f, 20260831);
            Assert.That(first, Is.EqualTo(repeat));
            Assert.That(first, Is.InRange(0.72f, 1.08f));
            Assert.That(Mathf.Abs(first - nearby), Is.LessThan(0.01f));
        }

        [Test]
        public void SelectedTrees_PassStrictRealTrunkAndNearFoliageGate()
        {
            foreach (string species in new[]
                     { "Spruce", "Pine", "Birch", "Aspen" })
            {
                var selected = new HashSet<GameObject>();
                for (int index = 0; index < 32; index++)
                    selected.Add(MapVegetationTreePresentation
                        .SelectPrefab(species, "dense-quality:" + species +
                            ":" + index, 20260831, 7f + index % 14));
                Assert.That(selected.Count, Is.GreaterThan(1),
                    species + " must keep reviewed visual variation.");
                foreach (GameObject prefab in selected)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    try
                    {
                        MapVegetationMaterialBindings.Apply(instance);
                        MapVegetationTreePresentation.Apply(instance, prefab);
                        TreeAcceptanceEvidence evidence =
                            MapVegetationTreeAcceptance.AuditPrefab(instance,
                                species, AssetDatabase.GetAssetPath(prefab));
                        TreeLodGeometryEvidence near = evidence.lods[0];
                        Assert.That(near.nonFlatNearGeometry, Is.True);
                        Assert.That(near.hasVolumetricTrunk, Is.True);
                        Assert.That(near.hasBarkMaterialGeometry, Is.True);
                        Assert.That(near.hasFoliageGeometry, Is.True);
                        Assert.That(near.nearProxyRejected, Is.True);
                        Assert.That(near.billboardTriangleCount, Is.Zero);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static IEnumerable<DenseCandidate> Candidates(float spacing,
            float density, bool clustered)
        {
            const int seed = 20260831;
            int extent = Mathf.CeilToInt(340f / spacing);
            for (int z = -extent; z <= extent; z++)
            for (int x = -extent; x <= extent; x++)
            {
                Vector3 position = MapVegetationPlanning.Candidate(x, z,
                    spacing, seed ^ 0x39B17);
                uint random = VegetationStableHash.Hash(x, z,
                    seed ^ 0x5A71D);
                float probability = density;
                if (clustered)
                    probability *= MapVegetationPlanning
                        .StableForestClusterFactor(position, 48f,
                            seed ^ 0x2C71D);
                if (VegetationStableHash.ToUnitFloat(random) >=
                    Mathf.Clamp01(probability)) continue;
                string id = "fixture:" + x + ":" + z;
                yield return new DenseCandidate(id, position,
                    MapVegetationPlanning.HashId(id, seed));
            }
        }

        private static IReadOnlyList<DenseCandidate> Select(
            IEnumerable<DenseCandidate> candidates, float spacing) =>
            MapVegetationPlanning.SelectStableCappedIndependentSet(
                candidates, Array.Empty<Vector3>(), item => item.Score,
                item => item.Id, item => item.Position, spacing, 100000,
                out _);

        private static Vector3 Polar(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians) * radius, 0f,
                Mathf.Sin(radians) * radius);
        }

        private readonly struct DenseCandidate
        {
            public readonly string Id;
            public readonly Vector3 Position;
            public readonly uint Score;

            public DenseCandidate(string id, Vector3 position, uint score)
            {
                Id = id;
                Position = position;
                Score = score;
            }
        }
    }
}
