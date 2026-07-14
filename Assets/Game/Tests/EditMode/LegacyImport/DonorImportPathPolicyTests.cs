using MSC.LegacyImport.Editor.Pipeline;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorImportPathPolicyTests
    {
        [Test]
        public void ReferencePath_NormalizesSeparatorsInsideReferenceRoot()
        {
            bool valid = DonorImportPathPolicy.TryNormalizeReferenceAssetPath(
                "Assets\\Game\\LegacyImport\\ReferenceOnly\\Proof//part.fbx",
                out string normalized,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(normalized, Is.EqualTo(
                "Assets/Game/LegacyImport/ReferenceOnly/Proof/part.fbx"));
        }

        [TestCase("../outside.fbx")]
        [TestCase("C:\\outside.fbx")]
        [TestCase("Assets/Game/World/Content/not-reference.fbx")]
        [TestCase("Assets/Game/LegacyImport/ReferenceOnly/part.fbx.meta")]
        public void ReferencePath_RejectsUnsafeDestination(string path)
        {
            Assert.That(
                DonorImportPathPolicy.TryNormalizeReferenceAssetPath(path, out _, out _),
                Is.False);
        }

        [Test]
        public void ProductionPath_RejectsReferenceAndDonorGeneratedRoots()
        {
            Assert.That(
                DonorImportPathPolicy.TryNormalizeProductionAssetPath(
                    "Assets/Game/LegacyImport/ReferenceOnly/part.prefab",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                DonorImportPathPolicy.TryNormalizeProductionAssetPath(
                    "Assets/Game/Imported/DonorGenerated/part.prefab",
                    out _,
                    out _),
                Is.False);
            Assert.That(
                DonorImportPathPolicy.TryNormalizeProductionAssetPath(
                    "Assets/Game/Vehicle/Content/part.prefab",
                    out string normalized,
                    out _),
                Is.True);
            Assert.That(normalized, Is.EqualTo("Assets/Game/Vehicle/Content/part.prefab"));
        }
    }
}
