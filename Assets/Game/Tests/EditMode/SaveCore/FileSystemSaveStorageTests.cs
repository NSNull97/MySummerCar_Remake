using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace MSC.Save.Tests.EditMode
{
    public sealed class FileSystemSaveStorageTests
    {
        private string directory;
        private FileSystemSaveStorage storage;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MSC_Save_Core_Tests", Guid.NewGuid().ToString("N"));
            storage = new FileSystemSaveStorage(
                directory,
                utcNow: () => new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void WriteAndRead_RoundTripsValidatedDocument()
        {
            storage.Write("slot-a", SaveTestData.CreateDocument("slot-a"));

            SaveReadResult result = storage.Read("slot-a");

            Assert.That(result.Status, Is.EqualTo(SaveReadStatus.Loaded));
            Assert.That(result.Document.Metadata.DisplayName, Is.EqualTo("Test slot"));
            Assert.That(File.Exists(Path.Combine(directory, "slot-a", FileSystemSaveStorage.TemporaryFileName)), Is.False);
        }

        [Test]
        public void CorruptCurrent_QuarantinesAndRecoversBackup()
        {
            SaveDocument first = SaveTestData.CreateDocument("slot-a", "{\"value\":1}");
            SaveDocument second = SaveTestData.CreateDocument("slot-a", "{\"value\":2}");
            second.Header.UpdatedUtc = "2026-07-21T11:00:00.0000000+00:00";
            storage.Write("slot-a", first);
            storage.Write("slot-a", second);
            string slotDirectory = Path.Combine(directory, "slot-a");
            File.WriteAllText(
                Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName),
                "{ broken",
                new UTF8Encoding(false));

            SaveReadResult result = storage.Read("slot-a");

            Assert.That(result.Status, Is.EqualTo(SaveReadStatus.RecoveredFromBackup));
            Assert.That(result.Document.Domains[0].PayloadJson, Is.EqualTo("{\"value\":1}"));
            Assert.That(result.QuarantinedPaths, Has.Count.EqualTo(1));
            Assert.That(File.Exists(result.QuarantinedPaths[0]), Is.True);
            Assert.That(storage.Read("slot-a", false).Status, Is.EqualTo(SaveReadStatus.Loaded));
        }

        [Test]
        public void ValidTemporaryWithoutCurrent_RecoversInterruptedWrite()
        {
            string slotDirectory = Path.Combine(directory, "slot-a");
            Directory.CreateDirectory(slotDirectory);
            string temporary = Path.Combine(slotDirectory, FileSystemSaveStorage.TemporaryFileName);
            File.WriteAllText(
                temporary,
                new SaveDocumentCodec().Serialize(SaveTestData.CreateDocument("slot-a")),
                new UTF8Encoding(false));

            SaveReadResult result = storage.Read("slot-a");

            Assert.That(result.Status, Is.EqualTo(SaveReadStatus.RecoveredFromInterruptedWrite));
            Assert.That(File.Exists(temporary), Is.False);
            Assert.That(File.Exists(Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName)), Is.True);
        }

        [Test]
        public void EnumerateSlots_ExposesValidInterruptedWriteWithoutMutatingIt()
        {
            string slotDirectory = Path.Combine(directory, "slot-a");
            Directory.CreateDirectory(slotDirectory);
            string temporary = Path.Combine(
                slotDirectory,
                FileSystemSaveStorage.TemporaryFileName);
            File.WriteAllText(
                temporary,
                new SaveDocumentCodec().Serialize(
                    SaveTestData.CreateDocument("slot-a")),
                new UTF8Encoding(false));

            SaveSlotSummary summary = storage.EnumerateSlots().Single();

            Assert.That(summary.IsValid, Is.True);
            Assert.That(summary.Message, Does.Contain("recovery"));
            Assert.That(File.Exists(temporary), Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    slotDirectory,
                    FileSystemSaveStorage.CurrentFileName)),
                Is.False);
        }

        [Test]
        public void EnumerateSlots_ExposesValidBackupBehindCorruptCurrent()
        {
            SaveDocument first = SaveTestData.CreateDocument(
                "slot-a",
                "{\"value\":1}");
            SaveDocument second = SaveTestData.CreateDocument(
                "slot-a",
                "{\"value\":2}");
            storage.Write("slot-a", first);
            storage.Write("slot-a", second);
            string slotDirectory = Path.Combine(directory, "slot-a");
            string current = Path.Combine(
                slotDirectory,
                FileSystemSaveStorage.CurrentFileName);
            File.WriteAllText(current, "not-json", new UTF8Encoding(false));

            SaveSlotSummary summary = storage.EnumerateSlots().Single();

            Assert.That(summary.IsValid, Is.True);
            Assert.That(summary.Message, Does.Contain("backup"));
            Assert.That(File.ReadAllText(current), Is.EqualTo("not-json"));
        }

        [Test]
        public void InvalidOnlyCandidate_IsQuarantinedAndReportedCorrupt()
        {
            string slotDirectory = Path.Combine(directory, "slot-a");
            Directory.CreateDirectory(slotDirectory);
            File.WriteAllText(
                Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName),
                "not-json",
                new UTF8Encoding(false));

            SaveReadResult result = storage.Read("slot-a");

            Assert.That(result.Status, Is.EqualTo(SaveReadStatus.Corrupt));
            Assert.That(result.QuarantinedPaths, Has.Count.EqualTo(1));
        }

        [Test]
        public void WriteOverCorruptCurrent_QuarantinesInsteadOfCreatingCorruptBackup()
        {
            string slotDirectory = Path.Combine(directory, "slot-a");
            Directory.CreateDirectory(slotDirectory);
            string current = Path.Combine(slotDirectory, FileSystemSaveStorage.CurrentFileName);
            File.WriteAllText(current, "not-json", new UTF8Encoding(false));

            storage.Write("slot-a", SaveTestData.CreateDocument("slot-a"));

            Assert.That(storage.Read("slot-a", false).Status, Is.EqualTo(SaveReadStatus.Loaded));
            Assert.That(File.Exists(Path.Combine(slotDirectory, FileSystemSaveStorage.BackupFileName)), Is.False);
            Assert.That(Directory.GetFiles(Path.Combine(slotDirectory, "corrupt")), Has.Length.EqualTo(1));
        }
    }
}
