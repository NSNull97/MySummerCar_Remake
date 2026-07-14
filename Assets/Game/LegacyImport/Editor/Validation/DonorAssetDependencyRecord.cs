using System.Collections.Generic;

namespace MSC.LegacyImport.Editor.Validation
{
    public sealed class DonorAssetDependencyRecord
    {
        public DonorAssetDependencyRecord(string assetPath, IEnumerable<string> dependencies)
        {
            AssetPath = assetPath ?? string.Empty;
            Dependencies = dependencies == null
                ? new List<string>()
                : new List<string>(dependencies);
        }

        public string AssetPath { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
