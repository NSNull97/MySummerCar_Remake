using System;
using System.IO;
using System.Linq;
using MSC.Needs;
using MSC.Save;

namespace MSC.Save.Integration
{
    internal sealed class PlayerNeedsSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "player.needs";

        private readonly PlayerNeedsRuntime needs;

        public PlayerNeedsSaveParticipant(PlayerNeedsRuntime configuredNeeds)
        {
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                PlayerNeedsSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(needs.CaptureDto());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            PlayerNeedsSaveDto dto =
                SaveParticipantJson.Deserialize<PlayerNeedsSaveDto>(
                    envelope.PayloadJson);
            if (!dto.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Player needs save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() =>
            needs.CaptureDto();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!needs.TryRestoreDto(
                    (PlayerNeedsSaveDto)preparedState,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Player needs restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!needs.TryRestoreDto(
                    (PlayerNeedsSaveDto)checkpoint,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Player needs rollback failed: " + failure);
            }
        }
    }

    /// <summary>
    /// Adds the new authoritative needs domain to accepted 09B saves. Fresh
    /// values match a new donor-compatible session and do not guess progress.
    /// </summary>
    internal sealed class Milestone09CNeedsSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 2;
        public int ToVersion => 3;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "09C needs migration requires a version 2 save document.",
                    nameof(source));
            }

            if (source.Domains.Any(domain => string.Equals(
                    domain?.DomainId,
                    PlayerNeedsSaveParticipant.DomainId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Version 2 save unexpectedly already contains player needs.");
            }

            SaveDocument migrated = source.DeepClone();
            migrated.Header.DocumentVersion = ToVersion;
            migrated.Domains = migrated.Domains
                .Concat(new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = PlayerNeedsSaveParticipant.DomainId,
                        SchemaVersion =
                            PlayerNeedsSaveDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(
                            PlayerNeedsSaveDto.Fresh()),
                    },
                })
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            return migrated;
        }
    }

    /// <summary>
    /// Preserves early 09C saves while adding delayed digestion and alcohol
    /// buffers. Missing buffered effects are intentionally initialized empty.
    /// </summary>
    internal sealed class Milestone09CDelayedNeedsSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 3;
        public int ToVersion => 4;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "Delayed-needs migration requires a version 3 save document.",
                    nameof(source));
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope needsEnvelope = migrated.Domains
                .SingleOrDefault(domain => string.Equals(
                    domain?.DomainId,
                    PlayerNeedsSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            if (needsEnvelope == null)
            {
                throw new InvalidOperationException(
                    "Version 3 save has no required player-needs domain.");
            }

            if (needsEnvelope.SchemaVersion == 1)
            {
                PlayerNeedsSaveDto dto =
                    SaveParticipantJson.Deserialize<PlayerNeedsSaveDto>(
                        needsEnvelope.PayloadJson);
                if (dto.schemaVersion != 1)
                {
                    throw new InvalidOperationException(
                        "Version 3 player-needs payload schema is inconsistent.");
                }

                dto.schemaVersion = PlayerNeedsSaveDto.CurrentSchemaVersion;
                needsEnvelope.SchemaVersion =
                    PlayerNeedsSaveDto.CurrentSchemaVersion;
                needsEnvelope.PayloadJson =
                    SaveParticipantJson.Serialize(dto);
            }
            else if (needsEnvelope.SchemaVersion !=
                     PlayerNeedsSaveDto.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported version 3 player-needs schema " +
                    $"{needsEnvelope.SchemaVersion}.");
            }

            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }

    /// <summary>
    /// Extends the 09C needs payload with hangover and delayed life-action
    /// effects. New fields intentionally start at zero for existing saves.
    /// </summary>
    internal sealed class Milestone09CLifeActionsSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 4;
        public int ToVersion => 5;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "09C life-actions migration requires a version 4 save document.",
                    nameof(source));
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope needsEnvelope = migrated.Domains
                .SingleOrDefault(domain => string.Equals(
                    domain?.DomainId,
                    PlayerNeedsSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            if (needsEnvelope == null)
            {
                throw new InvalidOperationException(
                    "Version 4 save has no required player-needs domain.");
            }

            if (needsEnvelope.SchemaVersion == 2)
            {
                PlayerNeedsSaveDto dto =
                    SaveParticipantJson.Deserialize<PlayerNeedsSaveDto>(
                        needsEnvelope.PayloadJson);
                if (dto.schemaVersion != 2)
                {
                    throw new InvalidOperationException(
                        "Version 4 player-needs payload schema is inconsistent.");
                }

                dto.schemaVersion = PlayerNeedsSaveDto.CurrentSchemaVersion;
                needsEnvelope.SchemaVersion =
                    PlayerNeedsSaveDto.CurrentSchemaVersion;
                needsEnvelope.PayloadJson =
                    SaveParticipantJson.Serialize(dto);
            }
            else if (needsEnvelope.SchemaVersion !=
                     PlayerNeedsSaveDto.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported version 4 player-needs schema " +
                    $"{needsEnvelope.SchemaVersion}.");
            }

            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }
}
