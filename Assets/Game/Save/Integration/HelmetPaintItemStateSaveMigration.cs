using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Items;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Adds the project-owned counterpart of the donor helmet Paint FSM state
    /// to version 10 item records without retaining donor runtime logic.
    /// </summary>
    internal sealed class HelmetPaintItemStateSaveMigration :
        ISaveDocumentMigration
    {
        private const string HelmetDefinitionId = "item.helmet";

        public int FromVersion => 10;

        public int ToVersion => 11;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "Helmet paint migration requires a version 10 save document.",
                    nameof(source));
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope itemEnvelope = RequireSingleItemDomain(migrated);
            ItemDomainSaveDto items =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    itemEnvelope.PayloadJson);
            if (items == null ||
                items.schemaVersion != ItemDomainSaveDto.CurrentSchemaVersion ||
                !string.Equals(
                    items.configurationId,
                    ItemDomainSaveDto.CurrentConfigurationId,
                    StringComparison.Ordinal) ||
                items.instances == null || items.instances.Length > 4096)
            {
                throw new InvalidDataException(
                    "Version 10 item payload is invalid.");
            }

            foreach (ItemRuntimeSaveRecord record in items.instances)
            {
                ItemInstanceState state = record?.state ??
                    throw new InvalidDataException(
                        "Version 10 item record has no state.");
                if (!string.Equals(
                        state.definitionId,
                        HelmetDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                state.scalarStates = AddMissingScalars(
                    state.scalarStates,
                    new ItemScalarState
                    {
                        stateId = ItemPaintStateIds.ColorRed,
                        value = 0.21896628f,
                    },
                    new ItemScalarState
                    {
                        stateId = ItemPaintStateIds.ColorGreen,
                        value = 0.41388258f,
                    },
                    new ItemScalarState
                    {
                        stateId = ItemPaintStateIds.ColorBlue,
                        value = 0.5514706f,
                    });
                state.flagStates = AddMissingFlags(
                    state.flagStates,
                    new ItemFlagState
                    {
                        stateId = ItemPaintStateIds.Applied,
                        value = false,
                    },
                    new ItemFlagState
                    {
                        stateId = ItemPaintStateIds.Matte,
                        value = false,
                    });
            }

            itemEnvelope.PayloadJson = SaveParticipantJson.Serialize(items);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }

        private static SaveDomainEnvelope RequireSingleItemDomain(
            SaveDocument document)
        {
            SaveDomainEnvelope[] matches =
                (document.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .Where(domain => string.Equals(
                    domain?.DomainId,
                    ItemSaveParticipant.DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 ||
                matches[0].SchemaVersion !=
                ItemDomainSaveDto.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(matches[0].PayloadJson))
            {
                throw new InvalidDataException(
                    "Save must contain one current item domain.");
            }

            return matches[0];
        }

        private static ItemScalarState[] AddMissingScalars(
            ItemScalarState[] existing,
            params ItemScalarState[] additions)
        {
            var result = new List<ItemScalarState>(
                existing ?? Array.Empty<ItemScalarState>());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemScalarState state in result)
            {
                if (state == null || !float.IsFinite(state.value) ||
                    !ids.Add(state.stateId ?? string.Empty))
                {
                    throw new InvalidDataException(
                        "Version 10 helmet scalar state is invalid.");
                }
            }

            foreach (ItemScalarState addition in additions)
            {
                if (ids.Add(addition.stateId))
                {
                    result.Add(addition);
                }
            }

            return result.ToArray();
        }

        private static ItemFlagState[] AddMissingFlags(
            ItemFlagState[] existing,
            params ItemFlagState[] additions)
        {
            var result = new List<ItemFlagState>(
                existing ?? Array.Empty<ItemFlagState>());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemFlagState state in result)
            {
                if (state == null || !ids.Add(state.stateId ?? string.Empty))
                {
                    throw new InvalidDataException(
                        "Version 10 helmet flag state is invalid.");
                }
            }

            foreach (ItemFlagState addition in additions)
            {
                if (ids.Add(addition.stateId))
                {
                    result.Add(addition);
                }
            }

            return result.ToArray();
        }
    }
}
