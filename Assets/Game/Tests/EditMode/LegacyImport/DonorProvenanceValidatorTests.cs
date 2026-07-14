using MSC.LegacyImport;
using MSC.LegacyImport.Editor.Validation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorProvenanceValidatorTests
    {
        [Test]
        public void MissingProvenance_IsReportedForUnregisteredReferenceAsset()
        {
            const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            DonorAssetRecord registered = DonorTestData.CreateRecord(hash);
            string[] referenceAssets =
            {
                registered.ReferenceAssetPath,
                "Assets/Game/LegacyImport/ReferenceOnly/Proof/unregistered.fbx"
            };

            var issues = DonorProvenanceValidator.FindMissingProvenance(
                referenceAssets,
                new[] { registered });

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Code, Is.EqualTo("MissingProvenance"));
        }

        [Test]
        public void GeneratedWorldReference_UsesItsDedicatedWorldDatabaseProvenance()
        {
            string[] generatedAssets =
            {
                "Assets/Game/LegacyImport/ReferenceOnly/World/Generated/Scenes/World_cell_0_0.unity",
                "Assets/Game/LegacyImport/ReferenceOnly/World/Generated/Materials/Road.mat"
            };

            var issues = DonorProvenanceValidator.FindMissingProvenance(
                generatedAssets,
                System.Array.Empty<DonorAssetRecord>());

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void ProductionPrefabDonorDependency_IsReported()
        {
            var assets = new[]
            {
                new DonorAssetDependencyRecord(
                    "Assets/Game/Vehicle/Content/ProductionPart.prefab",
                    new[]
                    {
                        "Assets/Game/Vehicle/Content/ProductionPart.prefab",
                        "Assets/Game/LegacyImport/ReferenceOnly/Proof/donor_part.fbx"
                    })
            };

            var issues = DonorProvenanceValidator.FindProductionReferenceLeaks(assets);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Code, Is.EqualTo("ProductionDonorDependency"));
        }
    }
}
