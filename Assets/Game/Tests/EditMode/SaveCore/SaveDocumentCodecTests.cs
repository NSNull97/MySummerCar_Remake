using System;
using System.IO;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class SaveDocumentCodecTests
    {
        [Test]
        public void Serialize_IsDeterministicAndSortsDomains()
        {
            SaveDocument document = SaveTestData.CreateDocument("slot-a");
            document.Domains = new[]
            {
                SaveTestData.Domain("world.entities", "{\"value\":2}"),
                SaveTestData.Domain("core.time", "{\"value\":1}"),
            };
            SaveDocumentCodec codec = new SaveDocumentCodec();

            string first = codec.Serialize(document);
            string second = codec.Serialize(document);
            SaveDocument restored = codec.Deserialize(first, true);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(restored.Domains[0].DomainId, Is.EqualTo("core.time"));
            Assert.That(restored.Domains[1].DomainId, Is.EqualTo("world.entities"));
            Assert.That(restored.Header.IntegritySha256, Has.Length.EqualTo(64));
        }

        [Test]
        public void Deserialize_TamperedContentFailsIntegrity()
        {
            SaveDocumentCodec codec = new SaveDocumentCodec();
            string json = codec.Serialize(SaveTestData.CreateDocument("slot-a"));
            string tampered = json.Replace("Test slot", "Changed slot");

            Assert.That(
                () => codec.Deserialize(tampered),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("integrity"));
        }

        [TestCase("../escape")]
        [TestCase("UpperCase")]
        [TestCase("dot.value")]
        [TestCase("")]
        public void SlotId_RejectsUnsafeValues(string slotId)
        {
            Assert.That(() => SaveSlotId.Validate(slotId), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void DuplicateDomainIds_AreRejected()
        {
            SaveDocument document = SaveTestData.CreateDocument("slot-a");
            document.Domains = new[]
            {
                SaveTestData.Domain("core.time", "{}"),
                SaveTestData.Domain("core.time", "{}"),
            };

            Assert.That(() => new SaveDocumentCodec().Serialize(document), Throws.TypeOf<InvalidDataException>());
        }
    }

    internal static class SaveTestData
    {
        public static SaveDocument CreateDocument(string slotId, string payload = "{\"value\":1}")
        {
            return new SaveDocument
            {
                Header = new SaveHeader
                {
                    FormatId = SaveHeader.CurrentFormatId,
                    DocumentVersion = SaveDocument.CurrentDocumentVersion,
                    SaveId = "save-001",
                    SlotId = slotId,
                    BuildId = "test-build",
                    CreatedUtc = "2026-07-21T10:00:00.0000000+00:00",
                    UpdatedUtc = "2026-07-21T10:00:00.0000000+00:00",
                },
                Metadata = new SaveMetadata
                {
                    DisplayName = "Test slot",
                    PlayTimeSeconds = 42d,
                    GameTimestamp = "1976-08-01T14:00",
                    LocationStableId = "location.home",
                },
                Domains = new[] { Domain("core.time", payload) },
            };
        }

        public static SaveDomainEnvelope Domain(string domainId, string payload, bool required = true)
        {
            return new SaveDomainEnvelope
            {
                DomainId = domainId,
                SchemaVersion = 1,
                Required = required,
                PayloadJson = payload,
            };
        }
    }
}
