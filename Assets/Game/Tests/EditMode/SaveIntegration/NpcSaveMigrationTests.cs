using System;
using System.Linq;
using MSC.Characters;
using MSC.NPC;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class NpcSaveMigrationTests
    {
        [Test]
        public void StoryTrafficDomain_IsOptionalAndRestoresAfterNpcState()
        {
            var owner = new GameObject("StoryTrafficSaveParticipantTest");
            try
            {
                NpcWorldRuntime runtime = owner.AddComponent<NpcWorldRuntime>();
                var participant = new StoryTrafficSaveParticipant(runtime);

                Assert.That(
                    participant.Descriptor.DomainId,
                    Is.EqualTo(StoryTrafficSaveParticipant.DomainId));
                Assert.That(participant.Descriptor.Required, Is.False);
                Assert.That(
                    participant.Descriptor.SchemaVersion,
                    Is.EqualTo(
                        StoryTrafficStateDto.CurrentSchemaVersion));
                Assert.That(
                    participant.Descriptor.Dependencies,
                    Does.Contain(NpcSaveParticipant.DomainId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VersionSeven_AddsRequiredNpcFoundationDomainWithoutMutatingSource()
        {
            var fresh = new NpcStateDto
            {
                characters = new[]
                {
                    new CharacterInstanceSnapshot
                    {
                        definitionId = "character.fixture.test",
                        stableInstanceId = "10a000000000000000000000000000ff",
                        activeScheduleBlockId = "schedule.fixture.test",
                        currentAnchorId = "anchor.fixture.start",
                        currentRouteId = string.Empty,
                        routeProgress01 = 0d,
                        activityState = CharacterActivityState.Idle,
                    },
                },
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 7 },
                Domains = Array.Empty<SaveDomainEnvelope>(),
            };
            var migration = new Milestone10ANpcSaveMigration(
                fresh,
                domainRequired: true);

            SaveDocument migrated = migration.Migrate(
                source,
                new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(7));
            Assert.That(source.Domains, Is.Empty);
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(8));
            SaveDomainEnvelope domain = migrated.Domains.Single();
            Assert.That(domain.DomainId, Is.EqualTo(NpcSaveParticipant.DomainId));
            Assert.That(domain.Required, Is.True);
            Assert.That(domain.SchemaVersion, Is.EqualTo(NpcStateDto.CurrentSchemaVersion));
        }

        [Test]
        public void VersionEight_PreservesAcceptedNpcStateAndAddsR1Roster()
        {
            CharacterInstanceSnapshot acceptedTeimo = Snapshot(
                "character.fixture.stationary-service",
                "10a00000000000000000000000000001");
            acceptedTeimo.flags = new[]
            {
                new CharacterFlagState
                {
                    flagId = "flag.teimo.met",
                    value = true,
                },
            };
            acceptedTeimo.dialogueCooldowns = null;
            var legacyState = new NpcStateDto
            {
                schemaVersion = 1,
                characters = new[]
                {
                    acceptedTeimo,
                    Snapshot(
                        "character.fixture.scheduled-roaming",
                        "10a00000000000000000000000000002"),
                    Snapshot(
                        "character.fixture.vehicle-linked",
                        "10a00000000000000000000000000003"),
                },
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 8 },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = NpcSaveParticipant.DomainId,
                        SchemaVersion = 1,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(legacyState),
                    },
                },
            };
            var fresh = new NpcStateDto
            {
                characters = new[]
                {
                    Snapshot("character.fixture.stationary-service", "10a00000000000000000000000000001"),
                    Snapshot("character.fixture.scheduled-roaming", "10a00000000000000000000000000002"),
                    Snapshot("character.fixture.vehicle-linked", "10a00000000000000000000000000003"),
                    Snapshot("character.fleetari", "10b10000000000000000000000000002"),
                    Snapshot("character.farmer", "10b10000000000000000000000000007"),
                    Snapshot("character.berryman", "10b10000000000000000000000000008"),
                },
            };

            SaveDocument migrated = new Milestone10BR1NpcSaveMigration(fresh)
                .Migrate(source, new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(8));
            Assert.That(source.Domains.Single().SchemaVersion, Is.EqualTo(1));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(9));
            SaveDomainEnvelope envelope = migrated.Domains.Single();
            Assert.That(
                envelope.SchemaVersion,
                Is.EqualTo(NpcStateDto.CurrentSchemaVersion));
            NpcStateDto restored =
                SaveParticipantJson.Deserialize<NpcStateDto>(
                    envelope.PayloadJson);
            Assert.That(restored.characters, Has.Length.EqualTo(6));
            CharacterInstanceSnapshot teimo = restored.characters.Single(value =>
                value.definitionId == "character.fixture.stationary-service");
            Assert.That(teimo.flags.Single().value, Is.True);
            Assert.That(teimo.dialogueCooldowns, Is.Empty);
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.fleetari"));
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.farmer"));
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.berryman"));
        }

        [Test]
        public void VersionEleven_PreservesAcceptedNpcStateAndAddsR2Roster()
        {
            CharacterInstanceSnapshot acceptedTeimo = Snapshot(
                "character.fixture.stationary-service",
                "10a00000000000000000000000000001");
            acceptedTeimo.flags = new[]
            {
                new CharacterFlagState
                {
                    flagId = "flag.teimo.r1-accepted",
                    value = true,
                },
            };
            var acceptedState = new NpcStateDto
            {
                characters = new[]
                {
                    acceptedTeimo,
                    Snapshot("character.fixture.scheduled-roaming", "10a00000000000000000000000000002"),
                    Snapshot("character.fixture.vehicle-linked", "10a00000000000000000000000000003"),
                    Snapshot("character.fleetari", "10b10000000000000000000000000002"),
                    Snapshot("character.farmer", "10b10000000000000000000000000007"),
                    Snapshot("character.berryman", "10b10000000000000000000000000008"),
                },
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 11 },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = NpcSaveParticipant.DomainId,
                        SchemaVersion = NpcStateDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(acceptedState),
                    },
                },
            };
            var fresh = new NpcStateDto
            {
                characters = new[]
                {
                    Snapshot("character.fixture.stationary-service", "10a00000000000000000000000000001"),
                    Snapshot("character.fixture.scheduled-roaming", "10a00000000000000000000000000002"),
                    Snapshot("character.fixture.vehicle-linked", "10a00000000000000000000000000003"),
                    Snapshot("character.fleetari", "10b10000000000000000000000000002"),
                    Snapshot("character.farmer", "10b10000000000000000000000000007"),
                    Snapshot("character.berryman", "10b10000000000000000000000000008"),
                    Snapshot("character.uncle-kesseli", "10b20000000000000000000000000003"),
                    Snapshot("character.grandmother", "10b20000000000000000000000000004"),
                    Snapshot("character.jokke", "10b20000000000000000000000000005"),
                    Snapshot("character.suski", "10b20000000000000000000000000006"),
                    Snapshot("character.sewage-client-1", "10b20000000000000000000000000009"),
                    Snapshot("character.sewage-client-2", "10b20000000000000000000000000010"),
                    Snapshot("character.sewage-client-3", "10b20000000000000000000000000011"),
                    Snapshot("character.sewage-client-4", "10b20000000000000000000000000012"),
                    Snapshot("character.sewage-client-5", "10b20000000000000000000000000013"),
                    Snapshot("character.firewood-customer", "10b20000000000000000000000000014"),
                    Snapshot("character.inspection-officer", "10b20000000000000000000000000015"),
                    Snapshot("character.wastewater-attendant", "10b20000000000000000000000000016"),
                    Snapshot("character.ventti-pigman", "10b20000000000000000000000000017"),
                    Snapshot("character.jokke-wife-state", "10b20000000000000000000000000101"),
                },
            };

            SaveDocument migrated = new Milestone10BR2NpcSaveMigration(fresh)
                .Migrate(source, new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(11));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(12));
            NpcStateDto restored = SaveParticipantJson.Deserialize<NpcStateDto>(
                migrated.Domains.Single().PayloadJson);
            Assert.That(restored.characters, Has.Length.EqualTo(20));
            Assert.That(
                restored.characters.Single(value =>
                    value.definitionId ==
                    "character.fixture.stationary-service")
                    .flags.Single().value,
                Is.True);
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.jokke-wife-state"));
        }

        [Test]
        public void VersionTwelve_PreservesSuskiRescueStateAndAddsR3StoryTraffic()
        {
            CharacterInstanceSnapshot acceptedSuski = Snapshot(
                "character.suski",
                "10b20000000000000000000000000006");
            acceptedSuski.flags = new[]
            {
                new CharacterFlagState
                {
                    flagId = NpcWorldRuntime.SuskiRescuedFromJaniCrashFlagId,
                    value = true,
                },
            };
            CharacterInstanceSnapshot[] acceptedCharacters =
            {
                Snapshot("character.fixture.stationary-service", "10a00000000000000000000000000001"),
                Snapshot("character.fixture.scheduled-roaming", "10a00000000000000000000000000002"),
                Snapshot("character.fixture.vehicle-linked", "10a00000000000000000000000000003"),
                Snapshot("character.fleetari", "10b10000000000000000000000000002"),
                Snapshot("character.farmer", "10b10000000000000000000000000007"),
                Snapshot("character.berryman", "10b10000000000000000000000000008"),
                Snapshot("character.uncle-kesseli", "10b20000000000000000000000000003"),
                Snapshot("character.grandmother", "10b20000000000000000000000000004"),
                Snapshot("character.jokke", "10b20000000000000000000000000005"),
                acceptedSuski,
                Snapshot("character.sewage-client-1", "10b20000000000000000000000000009"),
                Snapshot("character.sewage-client-2", "10b20000000000000000000000000010"),
                Snapshot("character.sewage-client-3", "10b20000000000000000000000000011"),
                Snapshot("character.sewage-client-4", "10b20000000000000000000000000012"),
                Snapshot("character.sewage-client-5", "10b20000000000000000000000000013"),
                Snapshot("character.firewood-customer", "10b20000000000000000000000000014"),
                Snapshot("character.inspection-officer", "10b20000000000000000000000000015"),
                Snapshot("character.wastewater-attendant", "10b20000000000000000000000000016"),
                Snapshot("character.ventti-pigman", "10b20000000000000000000000000017"),
                Snapshot("character.jokke-wife-state", "10b20000000000000000000000000101"),
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader { DocumentVersion = 12 },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = NpcSaveParticipant.DomainId,
                        SchemaVersion = NpcStateDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(
                            new NpcStateDto { characters = acceptedCharacters }),
                    },
                },
            };
            var fresh = new NpcStateDto
            {
                characters = acceptedCharacters
                    .Select(snapshot => snapshot.DeepClone())
                    .Concat(new[]
                    {
                        Snapshot("character.jani", "10b30000000000000000000000000049"),
                        Snapshot("character.petteri", "10b30000000000000000000000000050"),
                    })
                    .ToArray(),
            };

            SaveDocument migrated = new Milestone10BR3NpcSaveMigration(fresh)
                .Migrate(source, new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(12));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(13));
            NpcStateDto restored = SaveParticipantJson.Deserialize<NpcStateDto>(
                migrated.Domains.Single().PayloadJson);
            Assert.That(restored.characters, Has.Length.EqualTo(22));
            Assert.That(
                restored.characters.Single(value =>
                        value.definitionId == "character.suski")
                    .flags.Single(value => value.flagId ==
                        NpcWorldRuntime.SuskiRescuedFromJaniCrashFlagId)
                    .value,
                Is.True);
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.jani"));
            Assert.That(
                restored.characters.Select(value => value.definitionId),
                Does.Contain("character.petteri"));
        }

        private static CharacterInstanceSnapshot Snapshot(
            string definitionId,
            string stableInstanceId) =>
            new CharacterInstanceSnapshot
            {
                definitionId = definitionId,
                stableInstanceId = stableInstanceId,
                activeScheduleBlockId = string.Empty,
                currentAnchorId = "anchor.fixture.test",
                currentRouteId = string.Empty,
                routeProgress01 = 0d,
                activityState = CharacterActivityState.Hidden,
            };
    }
}
