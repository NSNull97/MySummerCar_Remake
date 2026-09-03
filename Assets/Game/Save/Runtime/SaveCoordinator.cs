using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MSC.Save
{
    public enum SaveOperationKind
    {
        Save = 0,
        Load = 1,
        EnumerateSlots = 2,
        Recovery = 3,
    }

    public sealed class SaveOperationEventArgs : EventArgs
    {
        public SaveOperationEventArgs(SaveOperationKind operation, string slotId, string message, Exception exception = null)
        {
            Operation = operation;
            SlotId = slotId ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public SaveOperationKind Operation { get; }
        public string SlotId { get; }
        public string Message { get; }
        public Exception Exception { get; }
    }

    public sealed class SaveRequest
    {
        public string SlotId = string.Empty;
        public string BuildId = string.Empty;
        public string SaveId = string.Empty;
        public SaveMetadata Metadata = new SaveMetadata();
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveDocument document, SaveReadStatus readStatus, UnresolvedContentReport unresolvedContent)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            ReadStatus = readStatus;
            UnresolvedContent = unresolvedContent ?? throw new ArgumentNullException(nameof(unresolvedContent));
        }

        public SaveDocument Document { get; }
        public SaveReadStatus ReadStatus { get; }
        public UnresolvedContentReport UnresolvedContent { get; }
    }

    public sealed class SaveCoordinator : ISaveService
    {
        private readonly object operationLock = new object();
        private readonly ISaveStorage storage;
        private readonly SaveParticipantRegistry participants;
        private readonly ISaveMigrationPipeline migrations;
        private readonly DeferredStableEntityStore deferredEntities;
        private readonly Func<DateTimeOffset> utcNow;
        private SaveDomainEnvelope[] retainedOptionalDomains = Array.Empty<SaveDomainEnvelope>();
        private bool isOperationInProgress;

        public SaveCoordinator(
            ISaveStorage storage,
            SaveParticipantRegistry participants,
            ISaveMigrationPipeline migrations,
            DeferredStableEntityStore deferredEntities,
            Func<DateTimeOffset> utcNow = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
            this.migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
            this.deferredEntities = deferredEntities ?? throw new ArgumentNullException(nameof(deferredEntities));
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public bool IsOperationInProgress
        {
            get
            {
                lock (operationLock)
                {
                    return isOperationInProgress;
                }
            }
        }

        public event EventHandler<SaveOperationEventArgs> OperationStarted;
        public event EventHandler<SaveOperationEventArgs> OperationCompleted;
        public event EventHandler<SaveOperationEventArgs> OperationFailed;
        public event EventHandler<SaveOperationEventArgs> RecoveryPerformed;

        public SaveWriteResult Save(SaveRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            SaveSlotId.Validate(request.SlotId);
            Begin(SaveOperationKind.Save, request.SlotId);
            try
            {
                DateTimeOffset now = utcNow().ToUniversalTime();
                string timestamp = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                SaveReadResult previous = storage.Read(request.SlotId, false);
                string createdUtc = previous.IsSuccess ? previous.Document.Header.CreatedUtc : timestamp;
                string saveId = !string.IsNullOrWhiteSpace(request.SaveId)
                    ? request.SaveId
                    : previous.IsSuccess ? previous.Document.Header.SaveId : Guid.NewGuid().ToString("N");

                SaveDomainEnvelope[] captured = participants.CaptureDomains();
                HashSet<string> capturedIds = new HashSet<string>(captured.Select(domain => domain.DomainId), StringComparer.Ordinal);
                SaveDomainEnvelope[] domains = captured
                    .Concat(retainedOptionalDomains
                        .Where(domain => domain != null && !domain.Required && !capturedIds.Contains(domain.DomainId))
                        .Select(domain => domain.DeepClone()))
                    .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                    .ToArray();

                SaveDocument document = new SaveDocument
                {
                    Header = new SaveHeader
                    {
                        FormatId = SaveHeader.CurrentFormatId,
                        DocumentVersion = SaveDocument.CurrentDocumentVersion,
                        SaveId = saveId,
                        SlotId = request.SlotId,
                        BuildId = request.BuildId ?? string.Empty,
                        CreatedUtc = createdUtc,
                        UpdatedUtc = timestamp,
                    },
                    Metadata = request.Metadata?.DeepClone() ?? new SaveMetadata(),
                    Domains = domains,
                };

                SaveDocumentValidator.Validate(document, true);
                SaveWriteResult result = storage.Write(request.SlotId, document);
                Complete(SaveOperationKind.Save, request.SlotId, "Save completed.");
                return result;
            }
            catch (Exception exception)
            {
                Fail(SaveOperationKind.Save, request.SlotId, exception);
                throw;
            }
            finally
            {
                End();
            }
        }

        public SaveLoadResult Load(string slotId)
        {
            SaveSlotId.Validate(slotId);
            Begin(SaveOperationKind.Load, slotId);
            try
            {
                SaveReadResult read = storage.Read(slotId, true);
                if (!read.IsSuccess)
                {
                    throw new FileNotFoundException($"No valid save exists in slot '{slotId}'. {read.Message}");
                }

                if (read.WasRecovered)
                {
                    RecoveryPerformed?.Invoke(
                        this,
                        new SaveOperationEventArgs(SaveOperationKind.Recovery, slotId, read.Message));
                }

                UnresolvedContentReport unresolved = new UnresolvedContentReport();
                SaveDocument migrated = migrations.MigrateToCurrent(read.Document.DeepClone(), unresolved);
                SaveDocumentValidator.Validate(migrated, true);

                DeferredStableEntityStore stagedDeferred = new DeferredStableEntityStore();
                stagedDeferred.Restore(deferredEntities.Snapshot());
                PreparedSaveRestore prepared = participants.PrepareRestore(migrated, unresolved, stagedDeferred);
                participants.ApplyRestore(prepared, unresolved, stagedDeferred);
                deferredEntities.Restore(stagedDeferred.Snapshot());

                HashSet<string> participantIds = new HashSet<string>(
                    participants.OrderedParticipants.Select(participant => participant.Descriptor.DomainId),
                    StringComparer.Ordinal);
                retainedOptionalDomains = migrated.Domains
                    .Where(domain => !domain.Required && !participantIds.Contains(domain.DomainId))
                    .Select(domain => domain.DeepClone())
                    .ToArray();

                Complete(SaveOperationKind.Load, slotId, "Load completed.");
                return new SaveLoadResult(migrated.DeepClone(), read.Status, unresolved);
            }
            catch (Exception exception)
            {
                Fail(SaveOperationKind.Load, slotId, exception);
                throw;
            }
            finally
            {
                End();
            }
        }

        public IReadOnlyList<SaveSlotSummary> EnumerateSlots()
        {
            Begin(SaveOperationKind.EnumerateSlots, string.Empty);
            try
            {
                IReadOnlyList<SaveSlotSummary> result = storage.EnumerateSlots();
                Complete(SaveOperationKind.EnumerateSlots, string.Empty, "Save slots enumerated.");
                return result;
            }
            catch (Exception exception)
            {
                Fail(SaveOperationKind.EnumerateSlots, string.Empty, exception);
                throw;
            }
            finally
            {
                End();
            }
        }

        private void Begin(SaveOperationKind operation, string slotId)
        {
            lock (operationLock)
            {
                if (isOperationInProgress)
                {
                    throw new InvalidOperationException("A save operation is already in progress.");
                }

                isOperationInProgress = true;
            }

            try
            {
                OperationStarted?.Invoke(this, new SaveOperationEventArgs(operation, slotId, "Save operation started."));
            }
            catch
            {
                End();
                throw;
            }
        }

        private void End()
        {
            lock (operationLock)
            {
                isOperationInProgress = false;
            }
        }

        private void Complete(SaveOperationKind operation, string slotId, string message)
        {
            OperationCompleted?.Invoke(this, new SaveOperationEventArgs(operation, slotId, message));
        }

        private void Fail(SaveOperationKind operation, string slotId, Exception exception)
        {
            OperationFailed?.Invoke(this, new SaveOperationEventArgs(operation, slotId, exception.Message, exception));
        }
    }
}
