using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.NPC;
using MSC.Save.Migration;

namespace MSC.Save.Integration
{
    internal sealed class NpcSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "npc.state";

        private readonly NpcWorldRuntime runtime;

        public NpcSaveParticipant(NpcWorldRuntime configuredRuntime)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                NpcStateDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState,
                CoreTimeSaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(runtime.CaptureDto());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            NpcStateDto dto = SaveParticipantJson.Deserialize<NpcStateDto>(
                envelope.PayloadJson);
            if (!runtime.TryValidateDto(dto, out string failure))
            {
                throw new InvalidDataException(
                    "NPC-state save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() => runtime.CaptureDto();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!runtime.TryRestoreDto(
                    (NpcStateDto)preparedState,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "NPC-state restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!runtime.TryRestoreDto(
                    (NpcStateDto)checkpoint,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "NPC-state rollback failed: " + failure);
            }
        }
    }

    internal sealed class Milestone10ANpcSaveMigration :
        ISaveDocumentMigration
    {
        private readonly NpcStateDto freshState;
        private readonly bool required;

        public Milestone10ANpcSaveMigration(
            NpcStateDto configuredFreshState,
            bool domainRequired)
        {
            freshState = configuredFreshState?.DeepClone() ??
                throw new ArgumentNullException(nameof(configuredFreshState));
            if (!freshState.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredFreshState));
            }

            required = domainRequired;
        }

        public int FromVersion => 7;
        public int ToVersion => 8;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "10A NPC migration requires a version 7 save document.",
                    nameof(source));
            }

            if (source.Domains.Any(domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Version 7 save unexpectedly already contains NPC state.");
            }

            SaveDocument migrated = source.DeepClone();
            migrated.Header.DocumentVersion = ToVersion;
            migrated.Domains = migrated.Domains.Concat(new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = NpcSaveParticipant.DomainId,
                        SchemaVersion = NpcStateDto.CurrentSchemaVersion,
                        Required = required,
                        PayloadJson = SaveParticipantJson.Serialize(freshState),
                    },
                })
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            return migrated;
        }
    }

    /// <summary>
    /// Expands the accepted 10A NPC state into the first real 10B roster
    /// package and introduces persisted dialogue cooldowns. Existing snapshots
    /// are retained verbatim; newly configured roster rows use fresh state.
    /// </summary>
    internal sealed class Milestone10BR1NpcSaveMigration :
        ISaveDocumentMigration
    {
        private const int LegacyNpcSchemaVersion = 1;
        private readonly NpcStateDto freshState;

        public Milestone10BR1NpcSaveMigration(
            NpcStateDto configuredFreshState)
        {
            freshState = configuredFreshState?.DeepClone() ??
                throw new ArgumentNullException(nameof(configuredFreshState));
            if (!freshState.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredFreshState));
            }
        }

        public int FromVersion => 8;
        public int ToVersion => 9;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "10B-R1 NPC migration requires a version 8 save document.",
                    nameof(source));
            }

            SaveDomainEnvelope[] npcDomains =
                (source.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .Where(domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (npcDomains.Length != 1)
            {
                throw new InvalidDataException(
                    "Version 8 save must contain exactly one NPC-state domain.");
            }

            SaveDomainEnvelope sourceEnvelope = npcDomains[0];
            if ((sourceEnvelope.SchemaVersion != LegacyNpcSchemaVersion &&
                 sourceEnvelope.SchemaVersion !=
                     NpcStateDto.CurrentSchemaVersion) ||
                string.IsNullOrWhiteSpace(sourceEnvelope.PayloadJson))
            {
                throw new InvalidDataException(
                    $"Unsupported version 8 NPC-state schema {sourceEnvelope.SchemaVersion}.");
            }

            NpcStateDto legacyState =
                SaveParticipantJson.Deserialize<NpcStateDto>(
                    sourceEnvelope.PayloadJson);
            if (legacyState == null ||
                legacyState.schemaVersion != sourceEnvelope.SchemaVersion)
            {
                throw new InvalidDataException(
                    "Version 8 NPC-state payload and envelope schemas differ.");
            }

            var configuredByDefinition =
                (freshState.characters ??
                    Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
                .ToDictionary(
                    snapshot => snapshot.definitionId,
                    StringComparer.Ordinal);
            var preservedByDefinition =
                new Dictionary<string, MSC.Characters.CharacterInstanceSnapshot>(
                    StringComparer.Ordinal);
            foreach (MSC.Characters.CharacterInstanceSnapshot snapshot in
                     legacyState.characters ??
                     Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
            {
                if (snapshot == null ||
                    string.IsNullOrWhiteSpace(snapshot.definitionId) ||
                    !StableEntityId.TryParse(snapshot.stableInstanceId, out _) ||
                    !configuredByDefinition.TryGetValue(
                        snapshot.definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot configured) ||
                    !string.Equals(
                        configured.stableInstanceId,
                        snapshot.stableInstanceId,
                        StringComparison.Ordinal) ||
                    !preservedByDefinition.TryAdd(
                        snapshot.definitionId,
                        snapshot.DeepClone()))
                {
                    throw new InvalidDataException(
                        "Version 8 NPC-state contains unknown, duplicate or incompatible character identity data.");
                }
            }

            NpcStateDto migratedState = freshState.DeepClone();
            for (int index = 0; index < migratedState.characters.Length; index++)
            {
                string definitionId =
                    migratedState.characters[index].definitionId;
                if (!preservedByDefinition.TryGetValue(
                        definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot preserved))
                {
                    continue;
                }

                preserved.dialogueCooldowns ??=
                    Array.Empty<MSC.Characters.CharacterDialogueCooldownState>();
                migratedState.characters[index] = preserved;
            }

            migratedState.schemaVersion = NpcStateDto.CurrentSchemaVersion;
            if (!migratedState.TryValidate(out string migratedFailure))
            {
                throw new InvalidDataException(
                    "Migrated 10B-R1 NPC state is invalid: " +
                    migratedFailure);
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope migratedEnvelope = migrated.Domains.Single(
                domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            migratedEnvelope.SchemaVersion = NpcStateDto.CurrentSchemaVersion;
            migratedEnvelope.PayloadJson =
                SaveParticipantJson.Serialize(migratedState);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }

    /// <summary>
    /// Expands an existing current-schema NPC domain with the R2 roster while
    /// preserving every compatible accepted character snapshot verbatim.
    /// </summary>
    internal sealed class Milestone10BR2NpcSaveMigration :
        ISaveDocumentMigration
    {
        private readonly NpcStateDto freshState;

        public Milestone10BR2NpcSaveMigration(
            NpcStateDto configuredFreshState)
        {
            freshState = configuredFreshState?.DeepClone() ??
                throw new ArgumentNullException(nameof(configuredFreshState));
            if (!freshState.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredFreshState));
            }
        }

        public int FromVersion => 11;
        public int ToVersion => 12;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "10B-R2 NPC migration requires a version 11 save document.",
                    nameof(source));
            }

            SaveDomainEnvelope[] npcDomains =
                (source.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .Where(domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (npcDomains.Length != 1)
            {
                throw new InvalidDataException(
                    "Version 11 save must contain exactly one NPC-state domain.");
            }

            SaveDomainEnvelope sourceEnvelope = npcDomains[0];
            if (sourceEnvelope.SchemaVersion !=
                    NpcStateDto.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(sourceEnvelope.PayloadJson))
            {
                throw new InvalidDataException(
                    $"Unsupported version 11 NPC-state schema {sourceEnvelope.SchemaVersion}.");
            }

            NpcStateDto acceptedState =
                SaveParticipantJson.Deserialize<NpcStateDto>(
                    sourceEnvelope.PayloadJson);
            if (acceptedState == null ||
                acceptedState.schemaVersion != sourceEnvelope.SchemaVersion)
            {
                throw new InvalidDataException(
                    "Version 11 NPC-state payload and envelope schemas differ.");
            }

            var configuredByDefinition =
                (freshState.characters ??
                    Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
                .ToDictionary(
                    snapshot => snapshot.definitionId,
                    StringComparer.Ordinal);
            var preservedByDefinition =
                new Dictionary<string, MSC.Characters.CharacterInstanceSnapshot>(
                    StringComparer.Ordinal);
            foreach (MSC.Characters.CharacterInstanceSnapshot snapshot in
                     acceptedState.characters ??
                     Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
            {
                if (snapshot == null ||
                    string.IsNullOrWhiteSpace(snapshot.definitionId) ||
                    !StableEntityId.TryParse(snapshot.stableInstanceId, out _) ||
                    !configuredByDefinition.TryGetValue(
                        snapshot.definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot configured) ||
                    !string.Equals(
                        configured.stableInstanceId,
                        snapshot.stableInstanceId,
                        StringComparison.Ordinal) ||
                    !preservedByDefinition.TryAdd(
                        snapshot.definitionId,
                        snapshot.DeepClone()))
                {
                    throw new InvalidDataException(
                        "Version 11 NPC-state contains unknown, duplicate or incompatible character identity data.");
                }
            }

            NpcStateDto migratedState = freshState.DeepClone();
            for (int index = 0; index < migratedState.characters.Length; index++)
            {
                string definitionId =
                    migratedState.characters[index].definitionId;
                if (preservedByDefinition.TryGetValue(
                        definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot preserved))
                {
                    migratedState.characters[index] = preserved;
                }
            }

            if (!migratedState.TryValidate(out string migratedFailure))
            {
                throw new InvalidDataException(
                    "Migrated 10B-R2 NPC state is invalid: " +
                    migratedFailure);
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope migratedEnvelope = migrated.Domains.Single(
                domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            migratedEnvelope.SchemaVersion = NpcStateDto.CurrentSchemaVersion;
            migratedEnvelope.PayloadJson =
                SaveParticipantJson.Serialize(migratedState);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }

    /// <summary>
    /// Adds the R3 story-traffic drivers to an accepted R2 NPC domain while
    /// preserving all compatible character state, including Suski's rescue
    /// flag. Newly introduced Jani and Petteri snapshots use fresh state.
    /// </summary>
    internal sealed class Milestone10BR3NpcSaveMigration :
        ISaveDocumentMigration
    {
        private readonly NpcStateDto freshState;

        public Milestone10BR3NpcSaveMigration(
            NpcStateDto configuredFreshState)
        {
            freshState = configuredFreshState?.DeepClone() ??
                throw new ArgumentNullException(nameof(configuredFreshState));
            if (!freshState.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(configuredFreshState));
            }
        }

        public int FromVersion => 12;
        public int ToVersion => 13;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "10B-R3 NPC migration requires a version 12 save document.",
                    nameof(source));
            }

            SaveDomainEnvelope[] npcDomains =
                (source.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .Where(domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (npcDomains.Length != 1)
            {
                throw new InvalidDataException(
                    "Version 12 save must contain exactly one NPC-state domain.");
            }

            SaveDomainEnvelope sourceEnvelope = npcDomains[0];
            if (sourceEnvelope.SchemaVersion !=
                    NpcStateDto.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(sourceEnvelope.PayloadJson))
            {
                throw new InvalidDataException(
                    $"Unsupported version 12 NPC-state schema {sourceEnvelope.SchemaVersion}.");
            }

            NpcStateDto acceptedState =
                SaveParticipantJson.Deserialize<NpcStateDto>(
                    sourceEnvelope.PayloadJson);
            if (acceptedState == null ||
                acceptedState.schemaVersion != sourceEnvelope.SchemaVersion)
            {
                throw new InvalidDataException(
                    "Version 12 NPC-state payload and envelope schemas differ.");
            }

            var configuredByDefinition =
                (freshState.characters ??
                    Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
                .ToDictionary(
                    snapshot => snapshot.definitionId,
                    StringComparer.Ordinal);
            var preservedByDefinition =
                new Dictionary<string, MSC.Characters.CharacterInstanceSnapshot>(
                    StringComparer.Ordinal);
            foreach (MSC.Characters.CharacterInstanceSnapshot snapshot in
                     acceptedState.characters ??
                     Array.Empty<MSC.Characters.CharacterInstanceSnapshot>())
            {
                if (snapshot == null ||
                    string.IsNullOrWhiteSpace(snapshot.definitionId) ||
                    !StableEntityId.TryParse(snapshot.stableInstanceId, out _) ||
                    !configuredByDefinition.TryGetValue(
                        snapshot.definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot configured) ||
                    !string.Equals(
                        configured.stableInstanceId,
                        snapshot.stableInstanceId,
                        StringComparison.Ordinal) ||
                    !preservedByDefinition.TryAdd(
                        snapshot.definitionId,
                        snapshot.DeepClone()))
                {
                    throw new InvalidDataException(
                        "Version 12 NPC-state contains unknown, duplicate or incompatible character identity data.");
                }
            }

            NpcStateDto migratedState = freshState.DeepClone();
            for (int index = 0; index < migratedState.characters.Length; index++)
            {
                string definitionId =
                    migratedState.characters[index].definitionId;
                if (preservedByDefinition.TryGetValue(
                        definitionId,
                        out MSC.Characters.CharacterInstanceSnapshot preserved))
                {
                    migratedState.characters[index] = preserved;
                }
            }

            if (!migratedState.TryValidate(out string migratedFailure))
            {
                throw new InvalidDataException(
                    "Migrated 10B-R3 NPC state is invalid: " +
                    migratedFailure);
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope migratedEnvelope = migrated.Domains.Single(
                domain => string.Equals(
                    domain?.DomainId,
                    NpcSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            migratedEnvelope.SchemaVersion = NpcStateDto.CurrentSchemaVersion;
            migratedEnvelope.PayloadJson =
                SaveParticipantJson.Serialize(migratedState);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }
}
