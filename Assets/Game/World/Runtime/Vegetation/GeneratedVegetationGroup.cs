using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>Explicit ownership boundary for replaceable, cell-local presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedVegetationGroup : MonoBehaviour
    {
        [SerializeField] private string generatorId;
        [SerializeField] private string cellId;
        [SerializeField] private string category;
        [SerializeField] private string fingerprint;

        public string GeneratorId => generatorId;
        public string CellId => cellId;
        public string Category => category;
        public string Fingerprint => fingerprint;

#if UNITY_EDITOR
        public void Configure(string owner, string cell, string kind, string hash)
        {
            generatorId = owner;
            cellId = cell;
            category = kind;
            fingerprint = hash;
        }
#endif
    }
}
