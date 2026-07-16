using System.IO;
using MSC.Editor.WorldTransfer;

namespace MSC.Editor.WorldBaseline
{
    public static class WorldBaselinePaths
    {
        public const string RuntimeRoot = "Assets/Game/LegacyImport/RuntimeBaseline";
        public const string GeneratedRoot = RuntimeRoot + "/Generated";
        public const string WorldRoot = GeneratedRoot + "/World";
        public const string SceneRoot = WorldRoot + "/Scenes";
        public const string MeshRoot = WorldRoot + "/Meshes";
        public const string SourceMeshRoot = MeshRoot + "/Source";
        public const string DerivedMeshRoot = MeshRoot + "/Derived";
        public const string MaterialRoot = WorldRoot + "/Materials";
        public const string CanonicalScene =
            SceneRoot + "/World_DonorBaseline_Canonical.unity";
        public const string SourceManifest =
            "Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json";
        public const string CaptureRoot = "PerformanceCaptures/Milestone06B1";

        public const string SourceRevisionId =
            "msc-world-baseline-04a1.1-c3f2f337";
        public const string SourceSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";

        public static string CategoryMaterial(string category)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                category = category.Replace(invalid, '_');
            }

            string fileName = category.Length > 96
                ? category.Substring(0, 96)
                : category;
            return MaterialRoot + "/" + fileName + ".mat";
        }

        public static string ToAbsoluteProjectPath(string path) =>
            WorldTransferPaths.ToAbsoluteProjectPath(path);
    }
}
