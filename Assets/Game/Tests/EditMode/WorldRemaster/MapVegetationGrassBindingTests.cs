using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationGrassBindingTests
    {
        [Test]
        public void ApprovedBindings_UseThreeDistinctShortForestFamiliesWithoutCerealOrSeedHeads()
        {
            Assert.That(MapVegetationGrassBindings.BindingVersion,
                Is.EqualTo("msc.map-vegetation-grass-bindings.v12"));
            var bindings = MapVegetationGrassBindings.ApprovedBindings().ToArray();
            Assert.That(bindings, Has.Length.EqualTo(3));
            Assert.That(bindings.Select(binding => binding.Channel), Is.EquivalentTo(new[]
            {
                VegetationDensityChannel.ShortGrass,
                VegetationDensityChannel.MeadowGrass,
                VegetationDensityChannel.TallGrass
            }));

            string[] sources = bindings.SelectMany(binding => new[]
                { binding.NearSource, binding.MiddleSource, binding.FarSource }).ToArray();
            Assert.That(sources, Has.Length.EqualTo(9));
            Assert.That(sources.Distinct().Count(), Is.EqualTo(3),
                "Each reviewed family uses its cheapest approved short clump in all LODs; density comes from project-owned composites.");
            Assert.That(sources.All(path => path.Contains("NatureManufacture Assets")), Is.True);
            Assert.That(sources.All(path => path.Contains(
                "Forest Environment Dynamic Nature/Foliage and Grass/Prefabs/prefab_grass_")), Is.True);
            Assert.That(sources.Any(path => path.Contains("Meadow Environment") ||
                path.ToLowerInvariant().Contains("grain") || path.ToLowerInvariant().Contains("cereal") ||
                path.ToLowerInvariant().Contains("wheat") || path.ToLowerInvariant().Contains("oat") ||
                path.ToLowerInvariant().Contains("barley") || path.ToLowerInvariant().Contains("reed")), Is.False);
            Assert.That(bindings.Select(binding => binding.VisualFamily),
                Is.EquivalentTo(new[] { "Forest grass 01", "Forest grass 02", "Forest grass 03" }));
            Assert.That(bindings.All(binding => binding.IsShortCarpetSource), Is.True);
            Assert.That(bindings.Any(binding => binding.ContainsCerealOrSeedHead), Is.False);
            Assert.That(bindings.Single(binding => binding.Channel == VegetationDensityChannel.ShortGrass)
                .NearSource, Does.Contain("Forest Environment Dynamic Nature/Foliage and Grass/Prefabs/prefab_grass_02_3"));
            Assert.That(bindings.Single(binding => binding.Channel == VegetationDensityChannel.MeadowGrass)
                .NearSource, Does.Contain("prefab_grass_01_3"));
            Assert.That(bindings.Single(binding => binding.Channel == VegetationDensityChannel.TallGrass)
                .NearSource, Does.Contain("prefab_grass_03_3"));
            foreach (string source in sources)
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(source), Is.Not.Null, source);

            MapVegetationGrassBindings.MixtureAudit mixture =
                MapVegetationGrassBindings.ApprovedMixtureAudit();
            Assert.That(mixture.Passed, Is.True, mixture.Diagnostic);
            Assert.That(mixture.DistinctShortFamilyCount, Is.EqualTo(3));
            Assert.That(mixture.DistinctNearSourceCount, Is.EqualTo(3));
            Assert.That(mixture.CerealOrSeedHeadSourceCount, Is.Zero);
            Assert.That(mixture.SelectionWeightSum, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ApprovedBindings_AreLowAndUseCheaperDistanceRings()
        {
            var bindings = MapVegetationGrassBindings.ApprovedBindings().ToArray();
            var shortGrass = bindings.Single(binding => binding.Channel == VegetationDensityChannel.ShortGrass);
            var broad = bindings.Single(binding => binding.Channel == VegetationDensityChannel.MeadowGrass);
            var dryFine = bindings.Single(binding => binding.Channel == VegetationDensityChannel.TallGrass);

            Assert.That(shortGrass.HeightRange, Is.EqualTo(new Vector2(0.16f, 0.24f)));
            Assert.That(broad.HeightRange, Is.EqualTo(new Vector2(0.15f, 0.22f)));
            Assert.That(dryFine.HeightRange, Is.EqualTo(new Vector2(0.14f, 0.20f)));
            Assert.That(bindings.Max(binding => binding.HeightRange.y), Is.LessThanOrEqualTo(0.24f));
            Assert.That(bindings.All(binding => binding.NearClusterCopies == 16), Is.True);
            Assert.That(bindings.All(binding => binding.NearClusterLayout ==
                MapVegetationGrassBindings.ClusterLayout.BlueNoiseDisk), Is.True);
            Assert.That(shortGrass.MiddleClusterCopies, Is.EqualTo(8));
            Assert.That(broad.MiddleClusterCopies, Is.EqualTo(8));
            Assert.That(dryFine.MiddleClusterCopies, Is.EqualTo(8));
            Assert.That(bindings.All(binding => binding.FarClusterCopies == 6), Is.True);
            Assert.That(bindings.Min(binding => binding.Distances.x), Is.GreaterThanOrEqualTo(18f));
            Assert.That(bindings.Min(binding => binding.Distances.y), Is.GreaterThanOrEqualTo(42f));
            Assert.That(bindings.Max(binding => binding.Distances.z), Is.LessThanOrEqualTo(60f));
            Assert.That(bindings.All(binding => binding.MinimumCutoff >= 0.28f &&
                binding.MinimumCutoff <= 0.30f), Is.True);
            Assert.That(bindings.All(binding => binding.Tint.maxColorComponent <= 0.82f), Is.True);
            Assert.That(shortGrass.MaximumNearTriangles, Is.EqualTo(1024));
            Assert.That(broad.MaximumNearTriangles, Is.EqualTo(512));
            Assert.That(dryFine.MaximumNearTriangles, Is.EqualTo(640));
            Assert.That(bindings.Min(binding => binding.NearClusterSpreadNormalized),
                Is.GreaterThanOrEqualTo(1.95f));
            Assert.That(bindings.Min(binding => binding.MiddleClusterSpreadNormalized),
                Is.GreaterThanOrEqualTo(2.15f));
            Assert.That(bindings.Min(binding => binding.FarClusterSpreadNormalized),
                Is.GreaterThanOrEqualTo(2.55f));
            Assert.That(shortGrass.MinimumNearProjectedSpanMeters, Is.EqualTo(1.18f));
            Assert.That(broad.MinimumNearProjectedSpanMeters, Is.EqualTo(1.10f));
            Assert.That(dryFine.MinimumNearProjectedSpanMeters, Is.EqualTo(1.05f));
            Assert.That(shortGrass.FootprintRadiusRangeMeters, Is.EqualTo(new Vector2(0.72f, 1.00f)));
            Assert.That(broad.FootprintRadiusRangeMeters, Is.EqualTo(new Vector2(0.66f, 0.92f)));
            Assert.That(dryFine.FootprintRadiusRangeMeters, Is.EqualTo(new Vector2(0.64f, 0.92f)));
            Assert.That(bindings.All(binding => binding.CoverageDiameterMeters == 0.80f), Is.True);
            Assert.That(bindings.All(binding => binding.CoverageProxyRadiusMeters == 0.035f), Is.True);
            Assert.That(bindings.Min(binding => binding.MinimumLodOccupiedCoverage.x),
                Is.GreaterThanOrEqualTo(0.56f));
            Assert.That(bindings.Min(binding => binding.MinimumLodOccupiedCoverage.z),
                Is.GreaterThanOrEqualTo(0.28f));
            Assert.That(bindings.Max(binding => binding.MaximumLodEmptyRadiusMeters.x),
                Is.LessThanOrEqualTo(0.17f));
            Assert.That(bindings.Max(binding => binding.MaximumLodEmptyRadiusMeters.z),
                Is.LessThanOrEqualTo(0.25f));
            Assert.That(bindings.Select(binding => binding.SelectionRange), Is.EqualTo(new[]
            {
                new Vector2(0f, 0.46f), new Vector2(0.46f, 0.78f), new Vector2(0.78f, 1f)
            }));
        }

        [Test]
        public void PreferredProfile_UsesAllThreeReviewedCarpetIntervals()
        {
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(0f, 3), Is.EqualTo(0));
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(0.46f, 3), Is.EqualTo(0));
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(0.46001f, 3), Is.EqualTo(1));
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(0.78f, 3), Is.EqualTo(1));
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(0.78001f, 3), Is.EqualTo(2));
            Assert.That(MapVegetationPlanning.PreferredGrassProfile(1f, 2), Is.EqualTo(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                MapVegetationPlanning.PreferredGrassProfile(0.5f, 0));
        }

        [Test]
        public void StableProfileSelector_RealizesReviewedMixtureInsteadOfPerlinBias()
        {
            var counts = new int[3];
            const int side = 256;
            for (int z = -side / 2; z < side / 2; z++)
            for (int x = -side / 2; x < side / 2; x++)
            {
                uint candidateHash = VegetationStableHash.Hash(x, z, 1459);
                float selector = MapVegetationPlanning.StableGrassProfileSelector(
                    candidateHash);
                counts[MapVegetationPlanning.PreferredGrassProfile(selector, 3)]++;
            }

            float total = counts.Sum();
            Assert.That(counts[0] / total, Is.EqualTo(0.46f).Within(0.01f));
            Assert.That(counts[1] / total, Is.EqualTo(0.32f).Within(0.01f));
            Assert.That(counts[2] / total, Is.EqualTo(0.22f).Within(0.01f));
        }

        [Test]
        public void ProjectedCoverage_DetectsIslandsEvenWhenOuterBoundsMatch()
        {
            Mesh filled = new Mesh();
            Mesh islands = new Mesh();
            try
            {
                filled.vertices = new[]
                {
                    new Vector3(-0.30f, 0f, -0.30f), new Vector3(0.30f, 0f, -0.30f),
                    new Vector3(0.30f, 0f, 0.30f), new Vector3(-0.30f, 0f, 0.30f)
                };
                filled.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                var islandVertices = new System.Collections.Generic.List<Vector3>();
                var islandTriangles = new System.Collections.Generic.List<int>();
                foreach (Vector2 center in new[]
                {
                    new Vector2(-0.28f, -0.28f), new Vector2(-0.28f, 0.28f),
                    new Vector2(0.28f, -0.28f), new Vector2(0.28f, 0.28f)
                })
                {
                    int first = islandVertices.Count;
                    islandVertices.Add(new Vector3(center.x - 0.02f, 0f, center.y - 0.02f));
                    islandVertices.Add(new Vector3(center.x + 0.02f, 0f, center.y - 0.02f));
                    islandVertices.Add(new Vector3(center.x, 0f, center.y + 0.02f));
                    islandTriangles.Add(first); islandTriangles.Add(first + 1); islandTriangles.Add(first + 2);
                }
                islands.SetVertices(islandVertices);
                islands.SetTriangles(islandTriangles, 0);
                filled.RecalculateBounds();
                islands.RecalculateBounds();

                Assert.That(islands.bounds.size.x, Is.EqualTo(filled.bounds.size.x).Within(0.0001f));
                Assert.That(islands.bounds.size.z, Is.EqualTo(filled.bounds.size.z).Within(0.0001f));

                MapVegetationGrassBindings.ProjectedCoverageAudit filledCoverage =
                    MapVegetationGrassBindings.MeasureProjectedCoverage(filled, 1f, 0.65f, 0.01f);
                MapVegetationGrassBindings.ProjectedCoverageAudit islandCoverage =
                    MapVegetationGrassBindings.MeasureProjectedCoverage(islands, 1f, 0.65f, 0.01f);

                Assert.That(filledCoverage.OccupiedFraction, Is.GreaterThan(0.90f));
                Assert.That(islandCoverage.OccupiedFraction, Is.LessThan(0.15f));
                Assert.That(islandCoverage.MaximumEmptyRadiusMeters, Is.GreaterThan(0.11f));
                Assert.That(filledCoverage.MaximumEmptyRadiusMeters,
                    Is.LessThan(islandCoverage.MaximumEmptyRadiusMeters));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(filled);
                UnityEngine.Object.DestroyImmediate(islands);
            }
        }

        [Test]
        public void PreparedProfiles_MatchLowCarpetContractAndRemainInsideExistingClearance()
        {
            MapVegetationRebuildOptions options = AssetDatabase.LoadAssetAtPath<MapVegetationRebuildOptions>(
                MapVegetationRebuildOptions.AssetPath);
            Assert.That(options, Is.Not.Null);
            Assert.That(options.GrassSpacingMeters, Is.EqualTo(0.80f));
            Assert.That(options.GrassDensity, Is.EqualTo(0.96f));
            Assert.That(options.Placement.GrassTextureMaskEnabled, Is.True);
            Assert.That(options.Placement.GrassTextureGreenDilationPixels, Is.EqualTo(3));
            Assert.That(options.Placement.GrassTextureCarpetRadiusPixels, Is.EqualTo(12));
            Assert.That(options.Placement.GrassTextureCarpetMinimumGreenFraction,
                Is.EqualTo(0.30f));
            Assert.That(options.Placement.GrassTextureCarpetMinimumSectors,
                Is.EqualTo(3));

            var approvedBindings = MapVegetationGrassBindings.ApprovedBindings();
            foreach (MapVegetationGrassBindings.BindingAudit binding in approvedBindings)
            {
                string profilePath = MapVegetationGrassBindings.Root + "/" + binding.Name + "/Profile.asset";
                VegetationProfile profile = AssetDatabase.LoadAssetAtPath<VegetationProfile>(profilePath);
                Assert.That(profile, Is.Not.Null, profilePath + " must be prepared before validation.");
                Assert.That(profile.DensityChannel, Is.EqualTo(binding.Channel));
                Assert.That(profile.CandidateSpacingMeters, Is.EqualTo(0.80f),
                    binding.Name + " must improve coverage inside one record rather than multiplying saved roots.");
                Assert.That(profile.UniformScaleRange, Is.EqualTo(binding.HeightRange));
                Assert.That(profile.MiddleLodDistance, Is.EqualTo(binding.Distances.x));
                Assert.That(profile.FarLodDistance, Is.EqualTo(binding.Distances.y));
                Assert.That(profile.CullingDistance, Is.EqualTo(binding.Distances.z));
                Assert.That(profile.Material.shader.name, Is.EqualTo("MSC/HDRP/Vegetation Indirect"));
                Assert.That(profile.DensityMultiplier, Is.EqualTo(1f));
                Assert.That(profile.Material.GetFloat("_Cutoff"), Is.GreaterThanOrEqualTo(binding.MinimumCutoff));
                Assert.That(profile.Material.GetFloat("_Cutoff"), Is.LessThanOrEqualTo(0.30f),
                    binding.Name + " must not alpha-cut the already sparse licensed blade atlas back into speckles.");
                Assert.That(profile.Material.GetFloat("_Smoothness"), Is.EqualTo(0.04f).Within(0.001f));
                Color specular = profile.Material.GetColor("_SpecularColor");
                Assert.That(specular.r, Is.EqualTo(0.018f).Within(0.001f));
                Assert.That(specular.g, Is.EqualTo(0.018f).Within(0.001f));
                Assert.That(specular.b, Is.EqualTo(0.018f).Within(0.001f));
                Assert.That(profile.Material.GetColor("_EmissiveColor").maxColorComponent, Is.Zero);
                Assert.That(profile.Material.GetColor("_EmissionColor").maxColorComponent, Is.Zero);
                Assert.That(
                    (profile.Material.globalIlluminationFlags &
                     MaterialGlobalIlluminationFlags.EmissiveIsBlack) ==
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                    Is.True,
                    binding.Name + " must stay explicitly non-emissive when grass materials are regenerated.");
                Color outputTint = profile.Material.GetColor("_BaseColor");
                Assert.That(outputTint.r, Is.InRange(0.4f, binding.Tint.r));
                Assert.That(outputTint.g, Is.InRange(0.4f, binding.Tint.g));
                Assert.That(outputTint.b, Is.InRange(0.4f, binding.Tint.b));
                Assert.That(profile.GetLodMesh(0).bounds.size.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(profile.GetLodMesh(1).bounds.size.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(profile.GetLodMesh(2).bounds.size.y, Is.EqualTo(1f).Within(0.001f));
                float footprint = MapVegetationGrassBindings.MeasureMaximumFootprintRadius(
                    profile, options.Placement.Category(MapVegetationKind.Grass));
                Assert.That(footprint, Is.InRange(binding.FootprintRadiusRangeMeters.x,
                    binding.FootprintRadiusRangeMeters.y), binding.Name);
                int nearTriangles = (int)profile.GetLodMesh(0).GetIndexCount(0) / 3;
                Assert.That(nearTriangles, Is.InRange(24, binding.MaximumNearTriangles), binding.Name);
                int expectedNearTriangles = binding.Channel == VegetationDensityChannel.ShortGrass
                    ? 944 : binding.Channel == VegetationDensityChannel.MeadowGrass ? 496 : 576;
                Assert.That(nearTriangles, Is.EqualTo(expectedNearTriangles),
                    binding.Name + " must retain the reviewed sixteen-clump source triangle count.");
                int middleTriangles = (int)profile.GetLodMesh(1).GetIndexCount(0) / 3;
                int farTriangles = (int)profile.GetLodMesh(2).GetIndexCount(0) / 3;
                int expectedMiddleTriangles = binding.Channel == VegetationDensityChannel.ShortGrass
                    ? 472 : binding.Channel == VegetationDensityChannel.MeadowGrass ? 248 : 288;
                int expectedFarTriangles = binding.Channel == VegetationDensityChannel.ShortGrass
                    ? 354 : binding.Channel == VegetationDensityChannel.MeadowGrass ? 186 : 216;
                Assert.That(middleTriangles, Is.EqualTo(expectedMiddleTriangles), binding.Name);
                Assert.That(farTriangles, Is.EqualTo(expectedFarTriangles), binding.Name);
                Assert.That(middleTriangles, Is.LessThan(nearTriangles), binding.Name);
                Assert.That(farTriangles, Is.LessThan(middleTriangles), binding.Name);
                float nearMinimumSpan = Mathf.Min(profile.GetLodMesh(0).bounds.size.x,
                    profile.GetLodMesh(0).bounds.size.z) * profile.UniformScaleRange.y;
                Assert.That(nearMinimumSpan, Is.GreaterThanOrEqualTo(
                    binding.MinimumNearProjectedSpanMeters), binding.Name);
                Assert.That(nearMinimumSpan, Is.GreaterThanOrEqualTo(
                    options.GrassSpacingMeters * 1.075f),
                    binding.Name + " must overlap adjacent saved roots instead of reading as separate flowerbeds.");
                if (binding.CoverageDiameterMeters > 0f)
                {
                    for (int lod = 0; lod < 3; lod++)
                    {
                        MapVegetationGrassBindings.ProjectedCoverageAudit coverage =
                            MapVegetationGrassBindings.MeasureProjectedCoverage(profile.GetLodMesh(lod),
                                binding.CoverageAuditScaleMeters, binding.CoverageDiameterMeters,
                                binding.CoverageProxyRadiusMeters);
                        float minimumOccupied = Component(binding.MinimumLodOccupiedCoverage, lod);
                        float maximumEmpty = Component(binding.MaximumLodEmptyRadiusMeters, lod);
                        float span = Mathf.Min(profile.GetLodMesh(lod).bounds.size.x,
                            profile.GetLodMesh(lod).bounds.size.z) * binding.CoverageAuditScaleMeters;
                        Assert.That(span, Is.GreaterThanOrEqualTo(
                            Component(binding.MinimumLodProjectedSpanMeters, lod)),
                            binding.Name + " LOD" + lod + " projected span");
                        Assert.That(coverage.OccupiedFraction,
                            Is.GreaterThanOrEqualTo(minimumOccupied),
                            binding.Name + " LOD" + lod + " occupied coverage");
                        Assert.That(coverage.MaximumEmptyRadiusMeters,
                            Is.LessThanOrEqualTo(maximumEmpty),
                            binding.Name + " LOD" + lod + " maximum hole");
                    }

                    float packedP10Scale = profile.UniformScaleRange.x *
                        options.Placement.Category(MapVegetationKind.Grass).UniformScaleRange.x;
                    MapVegetationGrassBindings.ProjectedCoverageAudit p10Coverage =
                        MapVegetationGrassBindings.MeasureProjectedCoverage(profile.GetLodMesh(0),
                            packedP10Scale, binding.CoverageDiameterMeters,
                            binding.CoverageProxyRadiusMeters);
                    float p10Span = Mathf.Min(profile.GetLodMesh(0).bounds.size.x,
                        profile.GetLodMesh(0).bounds.size.z) * packedP10Scale;
                    Assert.That(p10Span, Is.GreaterThanOrEqualTo(0.70f),
                        binding.Name + " small packed near record must still bridge most of the 0.80m root grid.");
                    Assert.That(p10Coverage.OccupiedFraction, Is.GreaterThanOrEqualTo(0.50f),
                        binding.Name + " small packed near record regressed to isolated tufts.");
                    Assert.That(p10Coverage.MaximumEmptyRadiusMeters, Is.LessThanOrEqualTo(0.20f),
                        binding.Name + " small packed near record contains a visible carpet hole.");
                }
            }

            Assert.That(options.Placement.GetMargin(MapVegetationKind.Grass,
                MapVegetationExclusionKind.AsphaltRoad), Is.GreaterThanOrEqualTo(0.94f));
            Assert.That(options.Placement.GetMargin(MapVegetationKind.Grass,
                MapVegetationExclusionKind.Building), Is.GreaterThanOrEqualTo(0.94f));
            Assert.That(options.Placement.GetMargin(MapVegetationKind.Grass,
                MapVegetationExclusionKind.AgriculturalField), Is.GreaterThanOrEqualTo(0.94f));
            float maximumPreparedFootprint = options.GrassProfiles.Max(profile =>
                MapVegetationGrassBindings.MeasureMaximumFootprintRadius(profile,
                    options.Placement.Category(MapVegetationKind.Grass)));
            foreach (MapVegetationExclusionKind kind in
                     (MapVegetationExclusionKind[])System.Enum.GetValues(
                         typeof(MapVegetationExclusionKind)))
            {
                if (kind == MapVegetationExclusionKind.None) continue;
                Assert.That(options.Placement.GetMargin(MapVegetationKind.Grass, kind),
                    Is.GreaterThan(maximumPreparedFootprint),
                    "The broader carpet mesh must remain inside the hard " + kind + " clearance.");
            }
        }

        private static float Component(Vector3 value, int index) =>
            index == 0 ? value.x : index == 1 ? value.y : value.z;
    }
}
