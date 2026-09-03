using System;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveDocumentCodecRoundTripTests
    {
        [Test]
        public void Serialize_RoundTripsHighPrecisionPlayTimes()
        {
            var codec = new SaveDocumentCodec();
            var random = new Random(0x5A7E);
            for (int index = 0; index < 2048; index++)
            {
                SaveDocument document = SaveTestData.CreateDocument("slot-a");
                document.Metadata.PlayTimeSeconds =
                    random.Next(0, 1_000_000) + random.NextDouble();

                string json = codec.Serialize(document);
                Assert.That(
                    () => codec.Deserialize(json, requireCurrentVersion: true),
                    Throws.Nothing,
                    "PlayTimeSeconds=" +
                    document.Metadata.PlayTimeSeconds.ToString("R"));
            }
        }
    }
}
