using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Items;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Adds the persistent lift height introduced by the project-owned jack
    /// controllers. Existing item identity, pose and donor-derived state are
    /// retained verbatim.
    /// </summary>
    internal sealed class VehicleJackItemStateSaveMigration :
        ISaveDocumentMigration
    {
        private const string CarJackDefinitionId = "item.car-jack";
        private const string FloorJackDefinitionId = "item.floor-jack";
        private const string LiftHeightStateId = "lift-height";

        public int FromVersion => 15;

        public int ToVersion => 16;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "Vehicle-jack migration requires a version 15 save document.",
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
                    "Version 15 item payload is invalid.");
            }

            foreach (ItemRuntimeSaveRecord record in items.instances)
            {
                ItemInstanceState state = record?.state ??
                    throw new InvalidDataException(
                        "Version 15 item record has no state.");
                if (!string.Equals(
                        state.definitionId,
                        CarJackDefinitionId,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        state.definitionId,
                        FloorJackDefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                state.scalarStates = AddMissingLiftHeight(
                    state.scalarStates);
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

        private static ItemScalarState[] AddMissingLiftHeight(
            ItemScalarState[] existing)
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
                        "Version 15 jack scalar state is invalid.");
                }
            }

            if (ids.Add(LiftHeightStateId))
            {
                result.Add(new ItemScalarState
                {
                    stateId = LiftHeightStateId,
                    value = 0f,
                });
            }

            return result.ToArray();
        }
    }
}
