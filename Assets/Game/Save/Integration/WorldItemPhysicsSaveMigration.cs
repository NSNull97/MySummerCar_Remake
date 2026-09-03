using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Items;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Repairs version 9 item Rigidbody records. The gravity field was added
    /// to the original world-entity schema without a schema migration, so an
    /// absent legacy JSON value deserialized as false and was then persisted.
    /// Project-owned loose item identity is taken from the authoritative item
    /// domain; unrelated pickup targets retain their saved physics flags.
    /// </summary>
    internal sealed class WorldItemPhysicsSaveMigration :
        ISaveDocumentMigration
    {
        private const int LegacyWorldEntitySchemaVersion = 1;
        private const string LegacyWorldEntityConfigurationId =
            "world.entities.native.v1";

        public int FromVersion => 9;

        public int ToVersion => 10;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "World-item physics migration requires a version 9 save document.",
                    nameof(source));
            }

            SaveDomainEnvelope worldEnvelope = RequireSingleDomain(
                source,
                WorldEntitySaveParticipant.DomainId);
            SaveDomainEnvelope itemEnvelope = RequireSingleDomain(
                source,
                ItemSaveParticipant.DomainId);
            if (worldEnvelope.SchemaVersion != LegacyWorldEntitySchemaVersion ||
                itemEnvelope.SchemaVersion != ItemDomainSaveDto.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    "Version 9 world-item physics schemas are unsupported.");
            }

            WorldEntityDomainSaveDto world =
                SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    worldEnvelope.PayloadJson);
            ItemDomainSaveDto items =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    itemEnvelope.PayloadJson);
            ValidateLegacyWorldState(world);
            HashSet<string> itemIds = CollectItemIds(items);

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope migratedWorldEnvelope = RequireSingleDomain(
                migrated,
                WorldEntitySaveParticipant.DomainId);
            WorldEntityDomainSaveDto migratedWorld =
                SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    migratedWorldEnvelope.PayloadJson);
            foreach (WorldEntityStateDto entity in migratedWorld.entities)
            {
                if (!itemIds.Contains(entity.stableEntityId))
                {
                    continue;
                }

                bool wasMissingLooseGravity = !entity.useGravity;
                entity.useGravity = true;
                entity.isKinematic = false;
                if (wasMissingLooseGravity)
                {
                    entity.sleeping = false;
                }
            }

            migratedWorld.schemaVersion =
                WorldEntityDomainSaveDto.CurrentSchemaVersion;
            migratedWorld.configurationId =
                WorldEntityDomainSaveDto.CurrentConfigurationId;
            if (!migratedWorld.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Migrated world-item physics state is invalid: " + failure);
            }

            migratedWorldEnvelope.SchemaVersion =
                WorldEntityDomainSaveDto.CurrentSchemaVersion;
            migratedWorldEnvelope.PayloadJson =
                SaveParticipantJson.Serialize(migratedWorld);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }

        private static SaveDomainEnvelope RequireSingleDomain(
            SaveDocument document,
            string domainId)
        {
            SaveDomainEnvelope[] matches =
                (document.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .Where(domain => string.Equals(
                    domain?.DomainId,
                    domainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 ||
                string.IsNullOrWhiteSpace(matches[0].PayloadJson))
            {
                throw new InvalidDataException(
                    $"Save must contain exactly one '{domainId}' domain.");
            }

            return matches[0];
        }

        private static void ValidateLegacyWorldState(
            WorldEntityDomainSaveDto world)
        {
            if (world == null ||
                world.schemaVersion != LegacyWorldEntitySchemaVersion ||
                !string.Equals(
                    world.configurationId,
                    LegacyWorldEntityConfigurationId,
                    StringComparison.Ordinal) ||
                world.entities == null ||
                world.entities.Length > SaveLimits.MaximumDeferredEntities)
            {
                throw new InvalidDataException(
                    "Version 9 world-entity payload is invalid.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldEntityStateDto entity in world.entities)
            {
                if (entity == null)
                {
                    throw new InvalidDataException(
                        "Version 9 world-entity record is missing.");
                }

                if (!entity.TryValidate(out string failure))
                {
                    throw new InvalidDataException(
                        "Version 9 world-entity record is invalid: " + failure);
                }

                if (!ids.Add(entity.stableEntityId))
                {
                    throw new InvalidDataException(
                        "Version 9 world-entity identity is duplicated.");
                }
            }
        }

        private static HashSet<string> CollectItemIds(ItemDomainSaveDto items)
        {
            if (items == null ||
                items.schemaVersion != ItemDomainSaveDto.CurrentSchemaVersion ||
                items.instances == null)
            {
                throw new InvalidDataException(
                    "Version 9 item payload is invalid.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemRuntimeSaveRecord record in items.instances)
            {
                string stableEntityId = record?.state?.stableEntityId;
                if (!StableEntityId.TryParse(stableEntityId, out _) ||
                    !ids.Add(stableEntityId))
                {
                    throw new InvalidDataException(
                        "Version 9 item payload contains invalid or duplicate identity.");
                }
            }

            return ids;
        }
    }
}
