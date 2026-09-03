using System;
using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Deterministic count metadata for one cell/species distant-forest batch.
    /// The generated child renderers may use arbitrary audited index counts, so
    /// validation never guesses instance counts from triangles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedDistantForestBatch : MonoBehaviour
    {
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private string species = string.Empty;
        [SerializeField, Min(0)] private int instanceCount;
        [SerializeField, Min(0)] private int templatePartCount;

        public string CellId => cellId;
        public string Species => species;
        public int InstanceCount => instanceCount;
        public int TemplatePartCount => templatePartCount;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(string configuredCellId,
            string configuredSpecies, int configuredInstanceCount,
            int configuredTemplatePartCount)
        {
            if (string.IsNullOrWhiteSpace(configuredCellId))
                throw new ArgumentException("Cell ID is required.",
                    nameof(configuredCellId));
            if (string.IsNullOrWhiteSpace(configuredSpecies))
                throw new ArgumentException("Species is required.",
                    nameof(configuredSpecies));
            if (configuredInstanceCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(configuredInstanceCount));
            if (configuredTemplatePartCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(configuredTemplatePartCount));
            cellId = configuredCellId.Trim();
            species = configuredSpecies.Trim();
            instanceCount = configuredInstanceCount;
            templatePartCount = configuredTemplatePartCount;
        }
#endif
    }
}
