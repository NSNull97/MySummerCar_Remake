using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationTreeMixtureTests
    {
        private static readonly float[] CurrentPercentages = { 65f, 20f, 7.5f, 7.5f };

        [Test]
        public void CurrentSavedPopulation_AssignsExactRoundedSpeciesQuotas()
        {
            List<MapVegetationPlacement> placements = Trees(66910);
            MapVegetationTreeMixture.Assign(placements, CurrentPercentages);
            Dictionary<string, int> counts = placements.GroupBy(p => p.species).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(placements.Count, Is.EqualTo(66910));
            Assert.That(placements.Count(p => p.category == MapVegetationCategories.OriginalTrees), Is.EqualTo(37678));
            Assert.That(placements.Count(p => p.category == MapVegetationCategories.BoundaryForest), Is.EqualTo(29232));
            Assert.That(counts.Count, Is.EqualTo(4));
            Assert.That(counts["Spruce"], Is.EqualTo(43492));
            Assert.That(counts["Pine"], Is.EqualTo(13382));
            Assert.That(counts["Birch"], Is.EqualTo(5018));
            Assert.That(counts["Aspen"], Is.EqualTo(5018));
        }

        [Test]
        public void ReversingInput_KeepsEveryIdentityInTheSameSpecies()
        {
            List<MapVegetationPlacement> placements = Trees(1027);
            MapVegetationTreeMixture.Assign(placements, CurrentPercentages);
            Dictionary<string, string> expected = placements.ToDictionary(p => p.cellId + "|" + p.id, p => p.species);
            placements.Reverse();
            MapVegetationTreeMixture.Assign(placements, CurrentPercentages);
            foreach (MapVegetationPlacement placement in placements)
                Assert.That(placement.species, Is.EqualTo(expected[placement.cellId + "|" + placement.id]));
        }

        [Test]
        public void Assignment_PreservesPlacementDataCategoriesAndNonTreeSpecies()
        {
            List<MapVegetationPlacement> placements = Trees(41);
            placements[0].category = MapVegetationCategories.BoundaryForest;
            placements.Add(new MapVegetationPlacement { id = "shrub", cellId = "cell_-1_0",
                category = MapVegetationCategories.ShrubsAndUndergrowth, species = "ExistingShrub", position = new Vector3(4, 7, 2), height = 1.25f });
            placements.Add(new MapVegetationPlacement { id = "grass", cellId = "cell_-1_0",
                category = MapVegetationCategories.GrassCoverage, species = "ExistingGrass", position = new Vector3(8, 2, 6), height = 0.75f });
            var before = placements.Select(p => (p.id, p.cellId, p.category, p.position, p.sourcePosition,
                p.normal, p.height, p.yaw, p.variation, p.species)).ToArray();
            MapVegetationTreeMixture.Assign(placements, CurrentPercentages);
            Assert.That(placements.Count, Is.EqualTo(before.Length));
            for (int i = 0; i < placements.Count; i++)
            {
                MapVegetationPlacement after = placements[i];
                Assert.That(after.id, Is.EqualTo(before[i].id));
                Assert.That(after.cellId, Is.EqualTo(before[i].cellId));
                Assert.That(after.category, Is.EqualTo(before[i].category));
                Assert.That(after.position.Equals(before[i].position), Is.True);
                Assert.That(after.sourcePosition.Equals(before[i].sourcePosition), Is.True);
                Assert.That(after.normal.Equals(before[i].normal), Is.True);
                Assert.That(after.height, Is.EqualTo(before[i].height));
                Assert.That(after.yaw, Is.EqualTo(before[i].yaw));
                Assert.That(after.variation, Is.EqualTo(before[i].variation));
                if (after.category == MapVegetationCategories.ShrubsAndUndergrowth || after.category == MapVegetationCategories.GrassCoverage)
                    Assert.That(after.species, Is.EqualTo(before[i].species));
            }
        }

        [Test]
        public void InvalidPercentages_FailBeforeChangingAnySpecies()
        {
            float[][] invalid = { null, Array.Empty<float>(), new[] { 50f, 50f }, new[] { 50f, 35f, 5f, 5f },
                new[] { 50f, 60f, -5f, -5f }, new[] { 50f, 35f, float.NaN, 15f },
                new[] { 50f, 35f, float.PositiveInfinity, 15f } };
            foreach (float[] percentages in invalid)
            {
                List<MapVegetationPlacement> placements = Trees(8);
                Assert.Throws<ArgumentException>(() => MapVegetationTreeMixture.Assign(placements, percentages));
                Assert.That(placements.All(p => p.species == "Unknown"), Is.True);
            }
        }

        private static List<MapVegetationPlacement> Trees(int count) => Enumerable.Range(0, count).Select(i =>
            new MapVegetationPlacement { id = "authored-source:" + i, cellId = "cell_" + (i % 7) + "_" + -(i % 5),
                category = i < Math.Min(count, 37678) ? MapVegetationCategories.OriginalTrees : MapVegetationCategories.BoundaryForest,
                species = "Unknown", position = new Vector3(i % 83, i % 13 * 0.25f, -(i % 107)),
                sourcePosition = new Vector3(i % 83, 0, -(i % 107)), normal = new Vector3(0.1f, 1f, 0.03f).normalized,
                height = 2f + i % 17, yaw = i % 360, variation = i % 251 / 250f }).ToList();
    }
}
