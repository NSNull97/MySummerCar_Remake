using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MSC.Editor.Vegetation
{
    /// <summary>Exact rounded species quotas over accepted placements, without moving/adding trees.</summary>
    public static class MapVegetationTreeMixture
    {
        public static void Assign(IEnumerable<MapVegetationPlacement> placements, float[] percentages)
        {
            if (percentages == null || percentages.Length != 4 || percentages.Any(p => !float.IsFinite(p) || p < 0f) ||
                Math.Abs(percentages.Sum() - 100f) > .001f) throw new ArgumentException("Four percentages totaling 100 are required.");
            using SHA256 sha = SHA256.Create();
            var trees = placements.Where(p => p.category == MapVegetationCategories.OriginalTrees || p.category == MapVegetationCategories.BoundaryForest)
                .Select(p => new { placement = p, rank = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(p.cellId + "|" + p.id))) })
                .OrderBy(p => p.rank, StringComparer.Ordinal).ThenBy(p => p.placement.cellId, StringComparer.Ordinal)
                .ThenBy(p => p.placement.id, StringComparer.Ordinal).ToArray();
            double total = percentages.Sum(p => (double)p);
            int[] counts = percentages.Select(p => (int)(trees.Length * (double)p / total)).ToArray();
            int[] remainders = Enumerable.Range(0, 4).OrderByDescending(i => trees.Length * (double)percentages[i] / total - counts[i]).ThenBy(i => i).ToArray();
            int remaining = trees.Length - counts.Sum();
            for (int i = 0; i < remaining; i++) counts[remainders[i]]++;
            string[] species = { "Spruce", "Pine", "Birch", "Aspen" };
            int cursor = 0;
            for (int i = 0; i < species.Length; i++)
                for (int j = 0; j < counts[i]; j++) trees[cursor++].placement.species = species[i];
        }
    }
}
