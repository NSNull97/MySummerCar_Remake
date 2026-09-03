using System;
using System.Linq;
using MSC.Items;
using NUnit.Framework;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class VehicleJackItemStateSaveMigrationTests
    {
        [Test]
        public void VersionFifteenMigrationAddsLiftHeightOnlyToJacks()
        {
            string carJackId = ItemStableIdUtility.CreateDeterministic(
                "migration.car-jack").Value;
            string floorJackId = ItemStableIdUtility.CreateDeterministic(
                "migration.floor-jack").Value;
            string otherId = ItemStableIdUtility.CreateDeterministic(
                "migration.other-item").Value;
            var itemDomain = new ItemDomainSaveDto
            {
                instances = new[]
                {
                    CreateRecord(carJackId, "item.car-jack"),
                    CreateRecord(floorJackId, "item.floor-jack"),
                    CreateRecord(otherId, "item.test"),
                },
            };
            SaveDocument source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 15,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = ItemSaveParticipant.DomainId,
                        SchemaVersion =
                            ItemDomainSaveDto.CurrentSchemaVersion,
                        PayloadJson =
                            SaveParticipantJson.Serialize(itemDomain),
                    },
                },
            };

            SaveDocument migrated =
                new VehicleJackItemStateSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());

            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(16));
            ItemDomainSaveDto migratedItems =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    migrated.Domains.Single().PayloadJson);
            Assert.That(
                Find(migratedItems, carJackId).scalarStates
                    .Single(state => state.stateId == "lift-height").value,
                Is.Zero);
            Assert.That(
                Find(migratedItems, floorJackId).scalarStates
                    .Single(state => state.stateId == "lift-height").value,
                Is.Zero);
            Assert.That(
                Find(migratedItems, otherId).scalarStates,
                Is.Empty);
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(15));
        }

        private static ItemInstanceState Find(
            ItemDomainSaveDto domain,
            string stableId) => domain.instances.Single(record =>
                record.state.stableEntityId == stableId).state;

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
