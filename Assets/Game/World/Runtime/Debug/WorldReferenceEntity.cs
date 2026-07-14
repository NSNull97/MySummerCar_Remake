using UnityEngine;

namespace MSC.World.Debugging
{
    [DisallowMultipleComponent]
    public sealed class WorldReferenceEntity : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private long donorObjectId;
        [SerializeField] private string donorHierarchyPath = string.Empty;
        [SerializeField] private string semanticCategory = string.Empty;
        [SerializeField] private string replacementStatus = string.Empty;
        [SerializeField] private string transferStatus = string.Empty;

        public string StableId => stableId;
        public long DonorObjectId => donorObjectId;
        public string DonorHierarchyPath => donorHierarchyPath;
        public string SemanticCategory => semanticCategory;
        public string ReplacementStatus => replacementStatus;
        public string TransferStatus => transferStatus;

        public void Configure(string id, long sourceObjectId, string hierarchyPath, string category, string replacement, string transfer)
        {
            stableId = id;
            donorObjectId = sourceObjectId;
            donorHierarchyPath = hierarchyPath;
            semanticCategory = category;
            replacementStatus = replacement;
            transferStatus = transfer;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldGeneratedSceneStamp : MonoBehaviour
    {
        [SerializeField] private string databaseVersion = string.Empty;
        [SerializeField] private string generatorVersion = string.Empty;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private int generatedEntityCount;

        public string DatabaseVersion => databaseVersion;
        public string GeneratorVersion => generatorVersion;
        public string CellId => cellId;
        public int GeneratedEntityCount => generatedEntityCount;

        public void Configure(string database, string generator, string cell, int count)
        {
            databaseVersion = database;
            generatorVersion = generator;
            cellId = cell;
            generatedEntityCount = count;
        }
    }
}
