using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MSC.Save
{
    public sealed class FileSystemSaveStorage : ISaveStorage
    {
        public const string CurrentFileName = "current.save.json";
        public const string TemporaryFileName = "current.save.json.tmp";
        public const string BackupFileName = "current.save.json.bak";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false, true);
        private readonly string rootDirectory;
        private readonly SaveDocumentCodec codec;
        private readonly Func<DateTimeOffset> utcNow;

        public FileSystemSaveStorage(string rootDirectory, SaveDocumentCodec codec = null, Func<DateTimeOffset> utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
            }

            this.rootDirectory = Path.GetFullPath(rootDirectory);
            this.codec = codec ?? new SaveDocumentCodec();
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public string RootDirectory => rootDirectory;

        public SaveWriteResult Write(string slotId, SaveDocument document)
        {
            SaveSlotId.Validate(slotId);
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string slotDirectory = SaveSlotId.CombineUnderRoot(rootDirectory, slotId);
            Directory.CreateDirectory(slotDirectory);
            string currentPath = Path.Combine(slotDirectory, CurrentFileName);
            string temporaryPath = Path.Combine(slotDirectory, TemporaryFileName);
            string backupPath = Path.Combine(slotDirectory, BackupFileName);

            SaveDocument snapshot = document.DeepClone();
            snapshot.Header ??= new SaveHeader();
            snapshot.Header.SlotId = slotId;
            string json = codec.Serialize(snapshot, true);

            try
            {
                if (File.Exists(currentPath) && !TryReadValid(currentPath, out _, out _))
                {
                    Quarantine(currentPath, slotDirectory);
                }

                WriteThrough(temporaryPath, Utf8WithoutBom.GetBytes(json));
                SaveDocument verified = codec.Deserialize(ReadBounded(temporaryPath));
                CommitTemporary(temporaryPath, currentPath, backupPath);
                return new SaveWriteResult(slotId, currentPath, verified);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        public SaveReadResult Read(string slotId, bool allowRecovery = true)
        {
            SaveSlotId.Validate(slotId);
            string slotDirectory = SaveSlotId.CombineUnderRoot(rootDirectory, slotId);
            string currentPath = Path.Combine(slotDirectory, CurrentFileName);
            string temporaryPath = Path.Combine(slotDirectory, TemporaryFileName);
            string backupPath = Path.Combine(slotDirectory, BackupFileName);
            List<string> quarantined = new List<string>();
            bool hadAnyCandidate = File.Exists(currentPath) || File.Exists(temporaryPath) || File.Exists(backupPath);

            if (TryReadValid(currentPath, out SaveDocument current, out Exception currentFailure))
            {
                return new SaveReadResult(slotId, SaveReadStatus.Loaded, current, currentPath, quarantined, string.Empty);
            }

            bool hadCurrent = File.Exists(currentPath);
            if (hadCurrent && allowRecovery)
            {
                quarantined.Add(Quarantine(currentPath, slotDirectory));
            }

            if (allowRecovery && TryReadValid(temporaryPath, out SaveDocument interrupted, out _))
            {
                PromoteRecovery(temporaryPath, currentPath);
                return new SaveReadResult(
                    slotId,
                    SaveReadStatus.RecoveredFromInterruptedWrite,
                    interrupted,
                    currentPath,
                    quarantined,
                    "Recovered a validated interrupted write.");
            }

            if (allowRecovery && File.Exists(temporaryPath))
            {
                quarantined.Add(Quarantine(temporaryPath, slotDirectory));
            }

            if (allowRecovery && TryReadValid(backupPath, out SaveDocument backup, out _))
            {
                RestoreBackup(backupPath, currentPath);
                return new SaveReadResult(
                    slotId,
                    SaveReadStatus.RecoveredFromBackup,
                    backup,
                    currentPath,
                    quarantined,
                    "Recovered the last validated backup.");
            }

            if (allowRecovery && File.Exists(backupPath))
            {
                quarantined.Add(Quarantine(backupPath, slotDirectory));
            }

            if (!hadAnyCandidate)
            {
                return new SaveReadResult(slotId, SaveReadStatus.Missing, null, string.Empty, quarantined, "Save slot is empty.");
            }

            string message = currentFailure?.Message ?? "No valid save candidate was found.";
            return new SaveReadResult(slotId, SaveReadStatus.Corrupt, null, string.Empty, quarantined, message);
        }

        public IReadOnlyList<SaveSlotSummary> EnumerateSlots()
        {
            if (!Directory.Exists(rootDirectory))
            {
                return Array.Empty<SaveSlotSummary>();
            }

            string[] directories = Directory.EnumerateDirectories(rootDirectory)
                .Take(SaveLimits.MaximumSlotCount + 1)
                .ToArray();
            if (directories.Length > SaveLimits.MaximumSlotCount)
            {
                throw new InvalidDataException($"Save root exceeds {SaveLimits.MaximumSlotCount} slot directories.");
            }

            List<SaveSlotSummary> summaries = new List<SaveSlotSummary>();
            foreach (string directory in directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string slotId = Path.GetFileName(directory);
                try
                {
                    SaveSlotId.Validate(slotId);
                    SaveReadResult result = InspectSlotWithoutMutation(
                        slotId,
                        directory);
                    summaries.Add(new SaveSlotSummary(
                        slotId,
                        result.Document?.Metadata,
                        result.Document?.Header?.UpdatedUtc,
                        result.IsSuccess,
                        result.Message));
                }
                catch (Exception exception) when (IsDocumentOrIoFailure(exception))
                {
                    summaries.Add(new SaveSlotSummary(slotId, null, string.Empty, false, exception.Message));
                }
            }

            return summaries;
        }

        private SaveReadResult InspectSlotWithoutMutation(
            string slotId,
            string slotDirectory)
        {
            string currentPath = Path.Combine(slotDirectory, CurrentFileName);
            string temporaryPath = Path.Combine(slotDirectory, TemporaryFileName);
            string backupPath = Path.Combine(slotDirectory, BackupFileName);
            bool hadAnyCandidate = File.Exists(currentPath) ||
                                   File.Exists(temporaryPath) ||
                                   File.Exists(backupPath);

            if (TryReadValid(currentPath, out SaveDocument current, out Exception currentFailure))
            {
                return new SaveReadResult(
                    slotId,
                    SaveReadStatus.Loaded,
                    current,
                    currentPath,
                    Array.Empty<string>(),
                    string.Empty);
            }

            if (TryReadValid(temporaryPath, out SaveDocument interrupted, out _))
            {
                return new SaveReadResult(
                    slotId,
                    SaveReadStatus.RecoveredFromInterruptedWrite,
                    interrupted,
                    temporaryPath,
                    Array.Empty<string>(),
                    "Validated interrupted write; recovery will run on load.");
            }

            if (TryReadValid(backupPath, out SaveDocument backup, out _))
            {
                return new SaveReadResult(
                    slotId,
                    SaveReadStatus.RecoveredFromBackup,
                    backup,
                    backupPath,
                    Array.Empty<string>(),
                    "Validated backup; recovery will run on load.");
            }

            return new SaveReadResult(
                slotId,
                hadAnyCandidate ? SaveReadStatus.Corrupt : SaveReadStatus.Missing,
                null,
                string.Empty,
                Array.Empty<string>(),
                currentFailure?.Message ??
                    (hadAnyCandidate
                        ? "No valid save candidate was found."
                        : "Save slot is empty."));
        }

        private bool TryReadValid(string path, out SaveDocument document, out Exception failure)
        {
            document = null;
            failure = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                document = codec.Deserialize(ReadBounded(path));
                return true;
            }
            catch (Exception exception) when (IsDocumentOrIoFailure(exception))
            {
                failure = exception;
                return false;
            }
        }

        private static string ReadBounded(string path)
        {
            FileInfo info = new FileInfo(path);
            if (info.Length > SaveLimits.MaximumDocumentBytes)
            {
                throw new InvalidDataException($"Save file exceeds {SaveLimits.MaximumDocumentBytes} bytes.");
            }

            return File.ReadAllText(path, Utf8WithoutBom);
        }

        private static void WriteThrough(string path, byte[] bytes)
        {
            using FileStream stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                8192,
                FileOptions.WriteThrough);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static void CommitTemporary(string temporaryPath, string currentPath, string backupPath)
        {
            if (!File.Exists(currentPath))
            {
                File.Move(temporaryPath, currentPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, currentPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                CommitFallback(temporaryPath, currentPath, backupPath);
            }
            catch (IOException)
            {
                CommitFallback(temporaryPath, currentPath, backupPath);
            }
        }

        private static void CommitFallback(string temporaryPath, string currentPath, string backupPath)
        {
            File.Copy(currentPath, backupPath, true);
            File.Copy(temporaryPath, currentPath, true);
            using (FileStream stream = new FileStream(currentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                stream.Flush(true);
            }

            File.Delete(temporaryPath);
        }

        private static void PromoteRecovery(string temporaryPath, string currentPath)
        {
            if (File.Exists(currentPath))
            {
                File.Delete(currentPath);
            }

            File.Move(temporaryPath, currentPath);
        }

        private static void RestoreBackup(string backupPath, string currentPath)
        {
            string recoveryPath = currentPath + ".recovery";
            try
            {
                File.Copy(backupPath, recoveryPath, true);
                if (File.Exists(currentPath))
                {
                    File.Delete(currentPath);
                }

                File.Move(recoveryPath, currentPath);
            }
            finally
            {
                TryDelete(recoveryPath);
            }
        }

        private string Quarantine(string path, string slotDirectory)
        {
            string corruptDirectory = Path.Combine(slotDirectory, "corrupt");
            Directory.CreateDirectory(corruptDirectory);
            string timestamp = utcNow().UtcDateTime.ToString("yyyyMMddTHHmmssfffffffZ", System.Globalization.CultureInfo.InvariantCulture);
            string baseName = Path.GetFileName(path) + ".corrupt." + timestamp;
            string candidate = Path.Combine(corruptDirectory, baseName);
            int suffix = 0;
            while (File.Exists(candidate))
            {
                suffix++;
                candidate = Path.Combine(corruptDirectory, baseName + "." + suffix);
            }

            File.Move(path, candidate);
            return candidate;
        }

        private static bool IsDocumentOrIoFailure(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is FormatException ||
                   exception is InvalidDataException ||
                   exception is NotSupportedException ||
                   exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is OverflowException;
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
