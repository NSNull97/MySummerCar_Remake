using System;
using System.Linq;
using MSC.Items;
using NUnit.Framework;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class HelmetPaintItemStateSaveMigrationTests
    {
        [Test]
        public void VersionTenMigrationAddsOnlyMissingHelmetPaintState()
        {
            string helmetId = ItemStableIdUtility.CreateDeterministic(
                "migration.helmet").Value;
            string otherId = ItemStableIdUtility.CreateDeterministic(
                "migration.other-item").Value;
            var itemDomain = new ItemDomainSaveDto
            {
                instances = new[]
                {
                    CreateRecord(helmetId, "item.helmet"),
                    CreateRecord(otherId, "item.test"),
                },
            };
            SaveDocument source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 10,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = ItemSaveParticipant.DomainId,
                        SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                        PayloadJson = SaveParticipantJson.Serialize(itemDomain),
                    },
                },
            };

            SaveDocument migrated =
                new HelmetPaintItemStateSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());

            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(11));
            ItemDomainSaveDto migratedItems =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    migrated.Domains.Single().PayloadJson);
            ItemInstanceState helmet = migratedItems.instances.Single(record =>
                record.state.stableEntityId == helmetId).state;
            ItemInstanceState other = migratedItems.instances.Single(record =>
                record.state.stableEntityId == otherId).state;
            Assert.That(helmet.scalarStates.Select(state => state.stateId),
                Is.EquivalentTo(new[]
                {
                    ItemPaintStateIds.ColorRed,
                    ItemPaintStateIds.ColorGreen,
                    ItemPaintStateIds.ColorBlue,
                }));
            Assert.That(helmet.flagStates.Select(state => state.stateId),
                Is.EquivalentTo(new[]
                {
                    ItemPaintStateIds.Applied,
                    ItemPaintStateIds.Matte,
                }));
            Assert.That(other.scalarStates, Is.Empty);
            Assert.That(other.flagStates, Is.Empty);
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(10));
            ItemDomainSaveDto original =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    source.Domains.Single().PayloadJson);
            Assert.That(original.instances[0].state.scalarStates, Is.Empty);
        }

        private static ItemRuntimeSaveRecord CreateRecord(
            string stableId,
            string definitionId) => new ItemRuntimeSaveRecord
        {
            state = new ItemInstanceState
            {
                stableEntityId = stableId,
                definitionId = definitionId,
                scalarStates = Array.Empty<ItemScalarState>(),
                flagStates = Array.Empty<ItemFlagState>(),
            },
        };
    }
}
