using System;
using System.Linq;
using MSC.Home;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class HomeSaveMigrationTests
    {
        [Test]
        public void VersionFiveSave_AddsFreshRequiredHomeDomain()
        {
            var existingDomain = new SaveDomainEnvelope
            {
                DomainId = "core.time",
                SchemaVersion = 1,
                Required = true,
                PayloadJson = "{}",
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 5,
                },
                Domains = new[]
                {
                    existingDomain,
                },
            };

            SaveDocument migrated =
                new Milestone09CHomeSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            SaveDomainEnvelope homeEnvelope = migrated.Domains.Single(
                domain => domain.DomainId == HomeSaveParticipant.DomainId);
            HomeStateDto homeState =
                JsonUtility.FromJson<HomeStateDto>(
                    homeEnvelope.PayloadJson);

            Assert.That(migrated, Is.Not.SameAs(source));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(6));
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(5));
            Assert.That(source.Domains, Has.Length.EqualTo(1));
            Assert.That(homeEnvelope.Required, Is.True);
            Assert.That(
                homeEnvelope.SchemaVersion,
                Is.EqualTo(HomeStateDto.CurrentSchemaVersion));
            Assert.That(
                homeState.schemaVersion,
                Is.EqualTo(HomeStateDto.CurrentSchemaVersion));
            Assert.That(
                homeState.TryValidate(out string failure),
                Is.True,
                failure);
            Assert.That(homeState.kitchenTapOpen, Is.False);
            Assert.That(homeState.showerSwitchOn, Is.True);
            Assert.That(homeState.showerValveOpen, Is.False);
            Assert.That(homeState.electricSaunaPowerOn, Is.False);
            Assert.That(
                homeState.electricSaunaHeatKnobDegrees,
                Is.EqualTo(1f));
            Assert.That(
                homeState.electricSaunaTimerKnobDegrees,
                Is.EqualTo(1f));
            Assert.That(
                homeState.electricSaunaTimerSecondsRemaining,
                Is.Zero);
            Assert.That(homeState.saunaTemperatureCelsius, Is.EqualTo(20f));
            Assert.That(
                migrated.Domains.Select(domain => domain.DomainId),
                Is.Ordered);
        }

        [Test]
        public void SourceWithUnsupportedVersion_IsRejectedWithoutMutation()
        {
            var source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 4,
                },
            };

            Assert.That(
                () => new Milestone09CHomeSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport()),
                Throws.TypeOf<ArgumentException>());
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(4));
            Assert.That(source.Domains, Is.Empty);
        }

        [Test]
        public void VersionFiveSave_WithUnexpectedHomeDomain_IsRejected()
        {
            var source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 5,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = HomeSaveParticipant.DomainId,
                        SchemaVersion =
                            HomeStateDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = JsonUtility.ToJson(
                            HomeStateDto.Fresh()),
                    },
                },
            };

            Assert.That(
                () => new Milestone09CHomeSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport()),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(5));
            Assert.That(source.Domains, Has.Length.EqualTo(1));
        }

        [Test]
        public void VersionSixSchemaOneHomeState_MapsSaunaKnobsAndPreservesState()
        {
            var legacyState = new LegacyHomeStateDtoV1
            {
                schemaVersion = 1,
                kitchenTapOpen = true,
                showerSwitchOn = true,
                showerValveOpen = true,
                electricSaunaPowerOn = true,
                electricSaunaTimerSecondsRemaining = 360f,
                saunaTemperatureCelsius = 74f,
                saunaSteamNormalized = 0.35f,
                fridgePowered = true,
                stovePowered = true,
                televisionPowered = true,
                fireplaceLit = true,
            };
            SaveDocument source = CreateVersionSixHomeSave(
                1,
                JsonUtility.ToJson(legacyState));

            SaveDocument migrated =
                new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            SaveDomainEnvelope envelope = migrated.Domains.Single();
            HomeStateDto homeState =
                JsonUtility.FromJson<HomeStateDto>(
                    envelope.PayloadJson);

            Assert.That(migrated, Is.Not.SameAs(source));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(7));
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(6));
            Assert.That(envelope.SchemaVersion, Is.EqualTo(2));
            Assert.That(homeState.schemaVersion, Is.EqualTo(2));
            Assert.That(homeState.electricSaunaHeatKnobDegrees, Is.EqualTo(150f));
            Assert.That(homeState.electricSaunaTimerKnobDegrees, Is.EqualTo(60f));
            Assert.That(
                homeState.electricSaunaTimerSecondsRemaining,
                Is.EqualTo(legacyState.electricSaunaTimerSecondsRemaining));
            Assert.That(homeState.kitchenTapOpen, Is.True);
            Assert.That(homeState.showerSwitchOn, Is.True);
            Assert.That(homeState.showerValveOpen, Is.True);
            Assert.That(homeState.saunaTemperatureCelsius, Is.EqualTo(74f));
            Assert.That(homeState.saunaSteamNormalized, Is.EqualTo(0.35f));
            Assert.That(homeState.fridgePowered, Is.True);
            Assert.That(homeState.stovePowered, Is.True);
            Assert.That(homeState.televisionPowered, Is.True);
            Assert.That(homeState.fireplaceLit, Is.True);
            Assert.That(
                homeState.TryValidate(out string failure),
                Is.True,
                failure);
        }

        [Test]
        public void VersionSixSchemaOneInactiveSauna_MapsBothKnobsToMinimum()
        {
            var legacyState = new LegacyHomeStateDtoV1
            {
                schemaVersion = 1,
                saunaTemperatureCelsius = 20f,
            };
            SaveDocument source = CreateVersionSixHomeSave(
                1,
                JsonUtility.ToJson(legacyState));

            SaveDocument migrated =
                new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            HomeStateDto homeState =
                JsonUtility.FromJson<HomeStateDto>(
                    migrated.Domains.Single().PayloadJson);

            Assert.That(homeState.electricSaunaHeatKnobDegrees, Is.EqualTo(1f));
            Assert.That(homeState.electricSaunaTimerKnobDegrees, Is.EqualTo(1f));
            Assert.That(homeState.electricSaunaPowerOn, Is.False);
            Assert.That(homeState.electricSaunaTimerSecondsRemaining, Is.Zero);
        }

        [Test]
        public void VersionSixSchemaOneProvisionalLongTimer_IsClampedAndSynchronized()
        {
            var legacyState = new LegacyHomeStateDtoV1
            {
                schemaVersion = 1,
                electricSaunaPowerOn = true,
                electricSaunaTimerSecondsRemaining = 90f * 60f,
                saunaTemperatureCelsius = 20f,
            };
            SaveDocument source = CreateVersionSixHomeSave(
                1,
                JsonUtility.ToJson(legacyState));

            SaveDocument migrated =
                new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            HomeStateDto homeState =
                JsonUtility.FromJson<HomeStateDto>(
                    migrated.Domains.Single().PayloadJson);

            Assert.That(homeState.electricSaunaTimerKnobDegrees, Is.EqualTo(120f));
            Assert.That(
                homeState.electricSaunaTimerSecondsRemaining,
                Is.EqualTo(720f));
            Assert.That(
                homeState.TryValidate(out string failure),
                Is.True,
                failure);
        }

        [Test]
        public void VersionSixSchemaTwoHomeState_IsValidatedAndPreserved()
        {
            var currentState = new HomeStateDto
            {
                kitchenTapOpen = true,
                electricSaunaPowerOn = true,
                electricSaunaHeatKnobDegrees = 75f,
                electricSaunaTimerKnobDegrees = 45f,
                electricSaunaTimerSecondsRemaining = 270f,
                saunaTemperatureCelsius = 62f,
                saunaSteamNormalized = 0.2f,
            };
            SaveDocument source = CreateVersionSixHomeSave(
                HomeStateDto.CurrentSchemaVersion,
                JsonUtility.ToJson(currentState));

            SaveDocument migrated =
                new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            HomeStateDto homeState =
                JsonUtility.FromJson<HomeStateDto>(
                    migrated.Domains.Single().PayloadJson);

            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(7));
            Assert.That(
                migrated.Domains.Single().SchemaVersion,
                Is.EqualTo(HomeStateDto.CurrentSchemaVersion));
            Assert.That(homeState.electricSaunaHeatKnobDegrees, Is.EqualTo(75f));
            Assert.That(homeState.electricSaunaTimerKnobDegrees, Is.EqualTo(45f));
            Assert.That(
                homeState.electricSaunaTimerSecondsRemaining,
                Is.EqualTo(270f));
            Assert.That(homeState.kitchenTapOpen, Is.True);
        }

        [Test]
        public void VersionSixHomeState_WithMismatchedPayloadSchema_IsRejected()
        {
            var legacyState = new LegacyHomeStateDtoV1
            {
                schemaVersion = 2,
                saunaTemperatureCelsius = 20f,
            };
            SaveDocument source = CreateVersionSixHomeSave(
                1,
                JsonUtility.ToJson(legacyState));

            Assert.That(
                () => new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport()),
                Throws.TypeOf<System.IO.InvalidDataException>());
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(6));
        }

        [Test]
        public void VersionSixSave_WithoutHomeDomain_IsRejected()
        {
            var source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 6,
                },
            };

            Assert.That(
                () => new Milestone09CSaunaControlsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport()),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(6));
        }

        private static SaveDocument CreateVersionSixHomeSave(
            int schemaVersion,
            string payloadJson) =>
            new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 6,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = HomeSaveParticipant.DomainId,
                        SchemaVersion = schemaVersion,
                        Required = true,
                        PayloadJson = payloadJson,
                    },
                },
            };

        [Serializable]
        private sealed class LegacyHomeStateDtoV1
        {
            public int schemaVersion;
            public bool kitchenTapOpen;
            public bool showerSwitchOn;
            public bool showerValveOpen;
            public bool electricSaunaPowerOn;
            public float electricSaunaTimerSecondsRemaining;
            public float saunaTemperatureCelsius;
            public float saunaSteamNormalized;
            public bool fridgePowered;
            public bool stovePowered;
            public bool televisionPowered;
            public bool fireplaceLit;
        }
    }
}
