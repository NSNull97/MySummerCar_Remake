using MSC.LegacyImport;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class LegacySatsumaBaselineMetadataTests
    {
        [Test]
        public void MetadataLocksProjectIdentityAndTemporaryClassification()
        {
            var root = new GameObject("Satsuma Metadata Test");
            try
            {
                LegacySatsumaBaselineMetadata metadata =
                    root.AddComponent<LegacySatsumaBaselineMetadata>();
                metadata.Configure(
                    "323d9fece916469ea30c705ebfcf68df",
                    "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4",
                    64200L,
                    "SATSUMA(557kg, 248)",
                    new Vector3(153.74904f, 1.611f, -1029.251f),
                    Quaternion.Euler(0f, 180f, 0f),
                    9,
                    23);

                Assert.That(metadata.TryValidate(out string failure),
                    Is.True,
                    failure);
                Assert.That(metadata.FeatureId, Is.EqualTo("P1.CAR.001"));
                Assert.That(metadata.VehicleContentId, Is.EqualTo("vehicle.satsuma"));
                Assert.That(
                    metadata.Classification,
                    Is.EqualTo(DonorTransferClassification.TemporaryDirectImport));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
