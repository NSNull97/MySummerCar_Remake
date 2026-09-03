using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace MSC.Save.EditorTools.Tests.EditMode
{
    public sealed class NativeSaveInspectionServiceTests
    {
        private string rootDirectory;
        private FileSystemSaveStorage storage;
        private NativeSaveInspectionService inspector;

        [SetUp]
        public void SetUp()
        {
            rootDirectory = Path.Combine(
                Path.GetTempPath(),
                "MSC_Save_Editor_Tests",
                Guid.NewGuid().ToString("N"));
            storage = new FileSystemSaveStorage(rootDirectory);
            inspector = new NativeSaveInspectionService(rootDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }

        [Test]
        public void InspectSlot_IsReadOnlyAndReportsValidatedBackup()
        {
            storage.Write("slot-a", CreateDocument("slot-a", "{\"value\":1}", "2026-07-21T10:00:00.0000000+00:00"));
            storage.Write("slot-a", CreateDocument("slot-a", "{\"value\":2}", "2026-07-21T11:00:00.0000000+00:00"));
            string slotDirectory = Path.Combine(rootDirectory, "slot-a");
            string currentPath = Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName);
            File.WriteAllText(currentPath, "{ broken", new UTF8Encoding(false));

            NativeSaveSlotInspection result = inspector.InspectSlot("slot-a");

            Assert.That(result.State, Is.EqualTo(NativeSaveSlotState.Recoverable));
            Assert.That(result.PreferredCandidate, Is.EqualTo(SaveCandidateKind.Backup));
            Assert.That(result.PreferredDocument.Domains[0].PayloadJson, Is.EqualTo("{\"value\":1}"));
            Assert.That(File.ReadAllText(currentPath), Is.EqualTo("{ broken"));
            Assert.That(Directory.Exists(Path.Combine(slotDirectory, "corrupt")), Is.False);
        }

        [Test]
        public void RecoverSlot_ExplicitlyPromotesBackupAndRecordsQuarantine()
        {
            storage.Write("slot-a", CreateDocument("slot-a", "{\"value\":1}", "2026-07-21T10:00:00.0000000+00:00"));
            storage.Write("slot-a", CreateDocument("slot-a", "{\"value\":2}", "2026-07-21T11:00:00.0000000+00:00"));
            string currentPath = Path.Combine(rootDirectory, "slot-a", FileSystemSaveStorage.CurrentFileName);
            File.WriteAllText(currentPath, "{ broken", new UTF8Encoding(false));

            SaveReadResult recovery = inspector.RecoverSlot("slot-a");
            NativeSaveSlotInspection after = inspector.InspectSlot("slot-a");

            Assert.That(recovery.Status, Is.EqualTo(SaveReadStatus.RecoveredFromBackup));
            Assert.That(recovery.QuarantinedPaths, Has.Count.EqualTo(1));
            Assert.That(File.Exists(recovery.QuarantinedPaths[0]), Is.True);
            Assert.That(after.State, Is.EqualTo(NativeSaveSlotState.Valid));
            Assert.That(after.Candidates.Any(candidate => candidate.Kind == SaveCandidateKind.Quarantined), Is.True);
        }

        [Test]
        public void ExtractStableEntityReferences_ClassifiesPayloadEvidenceWithoutClaimingAllMissing()
        {
            SaveDocument document = CreateDocument(
                "slot-a",
                "{\"DeferredEntries\":[{\"StableEntityId\":\"world.entity-a\"}]," +
                "\"UnresolvedEntries\":[{\"StableEntityId\":\"world.entity-b\"}]," +
                "\"Current\":{\"StableId\":\"world\\u002eentity-c\"}}",
                "2026-07-21T10:00:00.0000000+00:00");

            inspector.ExtractStableEntityReferences(document, out var references, out var issues);

            Assert.That(issues, Is.Empty);
            Assert.That(references.Count, Is.EqualTo(3));
            Assert.That(
                references.Single(item => item.StableEntityId == "world.entity-a").Kind,
                Is.EqualTo(StableEntityReferenceKind.Deferred));
            Assert.That(
                references.Single(item => item.StableEntityId == "world.entity-b").Kind,
                Is.EqualTo(StableEntityReferenceKind.Unresolved));
            Assert.That(
                references.Single(item => item.StableEntityId == "world.entity-c").Kind,
                Is.EqualTo(StableEntityReferenceKind.Referenced));
        }

        [Test]
        public void ExtractStableEntityReferences_ReportsMalformedDomainPayload()
        {
            SaveDocument document = CreateDocument(
                "slot-a",
                "{\"DeferredEntries\":[",
                "2026-07-21T10:00:00.0000000+00:00");

            inspector.ExtractStableEntityReferences(document, out var references, out var issues);

            Assert.That(references, Is.Empty);
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].DomainId, Is.EqualTo("test.domain"));
        }

        [Test]
        public void ValidateRoot_ReportsCorruptSlotWithoutRecoverableCandidate()
        {
            string slotDirectory = Path.Combine(rootDirectory, "slot-a");
            Directory.CreateDirectory(slotDirectory);
            File.WriteAllText(
                Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName),
                "not-json",
                new UTF8Encoding(false));

            NativeSaveRootValidation validation = inspector.ValidateRoot();

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Slots, Has.Count.EqualTo(1));
            Assert.That(validation.Slots[0].State, Is.EqualTo(NativeSaveSlotState.Corrupt));
            Assert.That(validation.Issues.Any(issue => issue.Contains("slot-a")), Is.True);
        }

        private static SaveDocument CreateDocument(string slotId, string payloadJson, string updatedUtc)
        {
            return new SaveDocument
            {
                Header = new SaveHeader
                {
                    FormatId = SaveHeader.CurrentFormatId,
                    DocumentVersion = SaveDocument.CurrentDocumentVersion,
                    SaveId = "save-editor-test",
                    SlotId = slotId,
                    BuildId = "editor-tests",
                    CreatedUtc = "2026-07-21T09:00:00.0000000+00:00",
                    UpdatedUtc = updatedUtc,
                },
                Metadata = new SaveMetadata
                {
                    DisplayName = "Editor test",
                    PlayTimeSeconds = 42d,
                    GameTimestamp = "1976-08-01T12:00",
                    LocationStableId = "world.home",
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = "test.domain",
                        SchemaVersion = 1,
                        Required = true,
                        PayloadJson = payloadJson,
                    },
                },
            };
        }
    }
}
