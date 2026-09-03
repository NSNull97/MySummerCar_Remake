using System;
using System.Linq;
using MSC.Items;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class WorldItemPhysicsSaveMigrationTests
    {
        [Test]
        public void VersionNineMigrationRepairsOnlyAuthoritativeItemBodies()
        {
            string itemId = ItemStableIdUtility.CreateDeterministic(
                "migration.item").Value;
            string unrelatedId = ItemStableIdUtility.CreateDeterministic(
                "migration.unrelated-pickup").Value;
            var legacyWorld = new WorldEntityDomainSaveDto
            {
                schemaVersion = 1,
                configurationId = "world.entities.native.v1",
                entities = new[]
                {
                    CreateWorldState(itemId, sleeping: true),
                    CreateWorldState(unrelatedId, sleeping: true),
                },
            };
            var itemDomain = new ItemDomainSaveDto
            {
                instances = new[]
                {
                    new ItemRuntimeSaveRecord
                    {
                        state = new ItemInstanceState
                        {
                            stableEntityId = itemId,
                            definitionId = "item.test",
                        },
                    },
                },
            };
            SaveDocument source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 9,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = ItemSaveParticipant.DomainId,
                        SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
                        PayloadJson = SaveParticipantJson.Serialize(itemDomain),
                    },
                    new SaveDomainEnvelope
                    {
                        DomainId = WorldEntitySaveParticipant.DomainId,
                        SchemaVersion = 1,
                        PayloadJson = SaveParticipantJson.Serialize(legacyWorld),
                    },
                },
            };

            SaveDocument migrated = new WorldItemPhysicsSaveMigration().Migrate(
                source,
                new UnresolvedContentReport());

            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(10));
            SaveDomainEnvelope migratedEnvelope = migrated.Domains.Single(
                domain => domain.DomainId == WorldEntitySaveParticipant.DomainId);
            Assert.That(
                migratedEnvelope.SchemaVersion,
                Is.EqualTo(WorldEntityDomainSaveDto.CurrentSchemaVersion));
            WorldEntityDomainSaveDto migratedWorld =
                SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    migratedEnvelope.PayloadJson);
            WorldEntityStateDto repaired = migratedWorld.entities.Single(
                entity => entity.stableEntityId == itemId);
            WorldEntityStateDto unrelated = migratedWorld.entities.Single(
                entity => entity.stableEntityId == unrelatedId);
            Assert.That(repaired.useGravity, Is.True);
            Assert.That(repaired.isKinematic, Is.False);
            Assert.That(repaired.sleeping, Is.False);
            Assert.That(unrelated.useGravity, Is.False);
            Assert.That(unrelated.sleeping, Is.True);

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(9));
            Assert.That(
                source.Domains.Single(domain =>
                    domain.DomainId == WorldEntitySaveParticipant.DomainId)
                    .SchemaVersion,
                Is.EqualTo(1));
        }

        private static WorldEntityStateDto CreateWorldState(
            string stableEntityId,
            bool sleeping) => new WorldEntityStateDto
        {
            stableEntityId = stableEntityId,
            worldRotation = Quaternion.identity,
            useGravity = false,
            isKinematic = false,
            sleeping = sleeping,
        };
    }
}
