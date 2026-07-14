using System;
using System.Collections.Generic;
using MSC.LegacyImport;
using MSC.LegacyImport.Editor.Pipeline;

namespace MSC.Tests.EditMode.LegacyImport
{
    internal static class DonorTestData
    {
        public static DonorAssetRecord CreateRecord(
            string stagedFileHash,
            string referencePath = "Assets/Game/LegacyImport/ReferenceOnly/Proof/environment_fixture.obj",
            string stagedPath = "normalized/proof/environment_fixture.obj",
            string recordId = "environment-fixture",
            string sourceContainerHash = null)
        {
            return new DonorAssetRecord(
                recordId,
                "mysummercar_Data/sharedassets0.assets",
                "Environment Fixture",
                sourceContainerHash ?? stagedFileHash,
                stagedFileHash,
                DonorAssetKind.Mesh,
                DonorTransferClassification.ReferenceOnly,
                stagedPath,
                referencePath,
                "Assets/Game/World/Content/Proof/EnvironmentFixture.prefab",
                DonorAssetStatus.Staged,
                "SyntheticTestImporter",
                "1.0.0",
                new List<string> { "sharedassets0.resource" },
                "Synthetic test fixture; not donor content.");
        }

        public static DonorAssetManifest CreateManifest(params DonorAssetRecord[] records)
        {
            return new DonorAssetManifest(
                "synthetic-test-manifest",
                DateTime.UtcNow.ToString("O"),
                DonorImportPipelineInfo.CurrentVersion,
                records);
        }
    }
}
