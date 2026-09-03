using System.Linq;
using MSC.Needs;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class PlayerNeedsSaveMigrationTests
    {
        [Test]
        public void VersionFourNeedsPayload_MigratesToLifeActionSchema()
        {
            var oldNeeds = new PlayerNeedsSaveDto
            {
                schemaVersion = 2,
                thirst = 14f,
                hunger = 25f,
                stress = 36f,
                urine = 47f,
                fatigue = 58f,
                dirtiness = 69f,
                weightKilograms = 84f,
                intoxication = 11f,
                pendingHungerEffect = -3f,
                pendingThirstEffect = -4f,
                pendingWeightEffect = 0.5f,
                pendingIntoxicationEffect = 6f,
            };
            var source = new SaveDocument
            {
                Header = new SaveHeader
                {
                    DocumentVersion = 4,
                },
                Domains = new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = "player.needs",
                        SchemaVersion = 2,
                        Required = true,
                        PayloadJson = JsonUtility.ToJson(oldNeeds),
                    },
                },
            };

            SaveDocument migrated =
                new Milestone09CLifeActionsSaveMigration().Migrate(
                    source,
                    new UnresolvedContentReport());
            SaveDomainEnvelope envelope = migrated.Domains.Single();
            PlayerNeedsSaveDto restored =
                JsonUtility.FromJson<PlayerNeedsSaveDto>(
                    envelope.PayloadJson);

            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(5));
            Assert.That(envelope.SchemaVersion, Is.EqualTo(3));
            Assert.That(restored.schemaVersion, Is.EqualTo(3));
            Assert.That(restored.thirst, Is.EqualTo(14f));
            Assert.That(restored.intoxication, Is.EqualTo(11f));
            Assert.That(restored.pendingIntoxicationEffect, Is.EqualTo(6f));
            Assert.That(restored.hangover, Is.Zero);
            Assert.That(restored.pendingUrineEffect, Is.Zero);
            Assert.That(restored.pendingFatigueEffect, Is.Zero);
            Assert.That(restored.pendingDirtinessEffect, Is.Zero);
            Assert.That(restored.TryValidate(out string failure), Is.True, failure);
        }
    }
}
