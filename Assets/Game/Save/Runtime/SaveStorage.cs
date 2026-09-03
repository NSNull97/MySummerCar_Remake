using System;
using System.Collections.Generic;

namespace MSC.Save
{
    public enum SaveReadStatus
    {
        Loaded = 0,
        Missing = 1,
        RecoveredFromBackup = 2,
        RecoveredFromInterruptedWrite = 3,
        Corrupt = 4,
    }

    public sealed class SaveReadResult
    {
        public SaveReadResult(
            string slotId,
            SaveReadStatus status,
            SaveDocument document,
            string sourcePath,
            IReadOnlyList<string> quarantinedPaths,
            string message)
        {
            SlotId = slotId ?? string.Empty;
            Status = status;
            Document = document;
            SourcePath = sourcePath ?? string.Empty;
            QuarantinedPaths = quarantinedPaths ?? Array.Empty<string>();
            Message = message ?? string.Empty;
        }

        public string SlotId { get; }
        public SaveReadStatus Status { get; }
        public SaveDocument Document { get; }
        public string SourcePath { get; }
        public IReadOnlyList<string> QuarantinedPaths { get; }
        public string Message { get; }
        public bool IsSuccess => Document != null;
        public bool WasRecovered => Status == SaveReadStatus.RecoveredFromBackup ||
                                    Status == SaveReadStatus.RecoveredFromInterruptedWrite;
    }

    public sealed class SaveWriteResult
    {
        public SaveWriteResult(string slotId, string path, SaveDocument document)
        {
            SlotId = slotId ?? string.Empty;
            Path = path ?? string.Empty;
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public string SlotId { get; }
        public string Path { get; }
        public SaveDocument Document { get; }
    }

    public sealed class SaveSlotSummary
    {
        public SaveSlotSummary(string slotId, SaveMetadata metadata, string updatedUtc, bool isValid, string message)
        {
            SlotId = slotId ?? string.Empty;
            Metadata = metadata?.DeepClone();
            UpdatedUtc = updatedUtc ?? string.Empty;
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public string SlotId { get; }
        public SaveMetadata Metadata { get; }
        public string UpdatedUtc { get; }
        public bool IsValid { get; }
        public string Message { get; }
    }

    public interface ISaveStorage
    {
        SaveWriteResult Write(string slotId, SaveDocument document);
        SaveReadResult Read(string slotId, bool allowRecovery = true);
        IReadOnlyList<SaveSlotSummary> EnumerateSlots();
    }
}
