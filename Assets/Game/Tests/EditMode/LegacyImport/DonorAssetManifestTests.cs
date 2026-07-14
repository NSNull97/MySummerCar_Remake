using MSC.LegacyImport;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorAssetManifestTests
    {
        [Test]
        public void JsonRoundTrip_PreservesManifestAndRecordProvenance()
        {
            const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            DonorAssetManifest source = DonorTestData.CreateManifest(DonorTestData.CreateRecord(hash));

            string json = source.ToJson();
            DonorAssetManifest restored = DonorAssetManifest.FromJson(json);

            Assert.That(restored.SchemaVersion, Is.EqualTo(DonorAssetManifest.CurrentSchemaVersion));
            Assert.That(restored.ManifestId, Is.EqualTo(source.ManifestId));
            Assert.That(restored.PipelineVersion, Is.EqualTo(source.PipelineVersion));
            Assert.That(restored.Records, Has.Count.EqualTo(1));
            Assert.That(restored.Records[0].RecordId, Is.EqualTo("environment-fixture"));
            Assert.That(restored.Records[0].SourceSha256, Is.EqualTo(hash));
            Assert.That(restored.Records[0].StagedFileSha256, Is.EqualTo(hash));
            Assert.That(restored.Records[0].Dependencies, Is.EquivalentTo(new[] { "sharedassets0.resource" }));
        }

        [Test]
        public void Sha256Digest_RequiresCanonicalLowerCaseHex()
        {
            const string canonical = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            Assert.That(Sha256Digest.IsCanonical(canonical), Is.True);
            Assert.That(Sha256Digest.IsCanonical(canonical.ToUpperInvariant()), Is.False);
            Assert.That(Sha256Digest.IsCanonical("short"), Is.False);
        }
    }
}
