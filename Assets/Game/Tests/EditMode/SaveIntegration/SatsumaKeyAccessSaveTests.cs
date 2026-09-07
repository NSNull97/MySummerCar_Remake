using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using NUnit.Framework;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class SatsumaKeyAccessSaveTests
    {
        private const string LaterDomainId = "vehicle.tests.after-key";

        [Test]
        public void FreshStateGrantsExactLogicalKeyAndCanBeRevoked()
        {
            var state = new SatsumaKeyAccessState();

            Assert.That(SatsumaKeyAccessState.KeyId,
                Is.EqualTo("vehicle.satsuma.key"));
            Assert.That(state.HasAccess, Is.True);

            state.SetAccess(false);

            Assert.That(state.HasAccess, Is.False);
        }

        [Test]
        public void ParticipantIsRequiredGlobalDomainAndCapturesFalse()
        {
            var state = new SatsumaKeyAccessState();
            state.SetAccess(false);
            var participant = new SatsumaKeyAccessSaveParticipant(state);

            Assert.That(participant.Descriptor.DomainId,
                Is.EqualTo(SatsumaKeyAccessSaveParticipant.DomainId));
            Assert.That(participant.Descriptor.SchemaVersion,
                Is.EqualTo(SatsumaKeyAccessSaveDto.CurrentSchemaVersion));
            Assert.That(participant.Descriptor.Required, Is.True);
            Assert.That(participant.Descriptor.RestorePhase,
                Is.EqualTo(SaveRestorePhase.GlobalState));
            Assert.That(participant.Descriptor.Dependencies, Is.Empty);

            SatsumaKeyAccessSaveDto captured =
                SaveParticipantJson.Deserialize<SatsumaKeyAccessSaveDto>(
                    participant.CapturePayload());
            Assert.That(captured.TryValidate(out string failure), Is.True,
                failure);
            Assert.That(captured.hasAccess, Is.False);
        }

        [Test]
        public void PrepareDoesNotMutateAndApplyRollbackPreserveFalse()
        {
            var state = new SatsumaKeyAccessState();
            var participant = new SatsumaKeyAccessSaveParticipant(state);
            object checkpoint = participant.CaptureCheckpoint();

            object prepared = participant.PrepareRestore(
                Envelope(hasAccess: false), PreparationContext());

            Assert.That(state.HasAccess, Is.True);
            participant.ApplyPreparedRestore(prepared, RestoreContext());
            Assert.That(state.HasAccess, Is.False);
            participant.Rollback(checkpoint);
            Assert.That(state.HasAccess, Is.True);
        }

        [Test]
        public void CapturedFalseRestoresFalseIntoFreshOwner()
        {
            var capturedState = new SatsumaKeyAccessState();
            capturedState.SetAccess(false);
            var capturingParticipant =
                new SatsumaKeyAccessSaveParticipant(capturedState);
            var capturedEnvelope = new SaveDomainEnvelope
            {
                DomainId = SatsumaKeyAccessSaveParticipant.DomainId,
                SchemaVersion =
                    SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = capturingParticipant.CapturePayload(),
            };
            var restoredState = new SatsumaKeyAccessState();
            var restoringParticipant =
                new SatsumaKeyAccessSaveParticipant(restoredState);

            object prepared = restoringParticipant.PrepareRestore(
                capturedEnvelope,
                PreparationContext());

            Assert.That(restoredState.HasAccess, Is.True);
            restoringParticipant.ApplyPreparedRestore(
                prepared,
                RestoreContext());
            Assert.That(restoredState.HasAccess, Is.False);
        }

        [TestCase(0, SatsumaKeyAccessSaveDto.CurrentConfigurationId,
            SatsumaKeyAccessState.KeyId)]
        [TestCase(SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
            "vehicle.satsuma.key-access.invalid",
            SatsumaKeyAccessState.KeyId)]
        [TestCase(SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
            SatsumaKeyAccessSaveDto.CurrentConfigurationId,
            "vehicle.satsuma.other-key")]
        public void PrepareRejectsIncompatiblePayloadWithoutMutation(
            int schemaVersion,
            string configurationId,
            string keyId)
        {
            var state = new SatsumaKeyAccessState();
            state.SetAccess(false);
            var participant = new SatsumaKeyAccessSaveParticipant(state);
            var dto = new SatsumaKeyAccessSaveDto
            {
                schemaVersion = schemaVersion,
                configurationId = configurationId,
                keyId = keyId,
                hasAccess = true,
            };

            Assert.Throws<InvalidDataException>(() =>
                participant.PrepareRestore(
                    Envelope(dto), PreparationContext()));
            Assert.That(state.HasAccess, Is.False);
        }

        [Test]
        public void RequiredDomainCannotBeOmitted()
        {
            var participant = new SatsumaKeyAccessSaveParticipant(
                new SatsumaKeyAccessState());
            var registry = new SaveParticipantRegistry(
                new ISaveParticipant[] { participant });

            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => registry.PrepareRestore(
                    CurrentDocument(),
                    new UnresolvedContentReport(),
                    new DeferredStableEntityStore()));

            Assert.That(failure.Message,
                Does.Contain(SatsumaKeyAccessSaveParticipant.DomainId));
        }

        [Test]
        public void MigrationAddsRequiredFreshDomainWithoutMutatingVersionSixteen()
        {
            SaveDocument source = VersionSixteenDocument(
                new SaveDomainEnvelope
                {
                    DomainId = "a.tests.existing",
                    SchemaVersion = 1,
                    Required = false,
                    PayloadJson = "{}",
                });
            var codec = new SaveDocumentCodec();
            string serializedSourceBefore = codec.Serialize(source);

            SaveDocument migrated = new SatsumaKeyAccessSaveMigration().Migrate(
                source,
                new UnresolvedContentReport());

            Assert.That(source.Header.DocumentVersion, Is.EqualTo(16));
            Assert.That(source.Domains.Length, Is.EqualTo(1));
            Assert.That(codec.Serialize(source),
                Is.EqualTo(serializedSourceBefore));
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(17));
            Assert.That(migrated.Domains.Select(domain => domain.DomainId),
                Is.Ordered.Using<string>(StringComparer.Ordinal));
            SaveDomainEnvelope added = migrated.Domains.Single(domain =>
                domain.DomainId == SatsumaKeyAccessSaveParticipant.DomainId);
            Assert.That(added.Required, Is.True);
            Assert.That(added.SchemaVersion,
                Is.EqualTo(SatsumaKeyAccessSaveDto.CurrentSchemaVersion));
            SatsumaKeyAccessSaveDto dto =
                SaveParticipantJson.Deserialize<SatsumaKeyAccessSaveDto>(
                    added.PayloadJson);
            Assert.That(dto.hasAccess, Is.True);
            Assert.That(dto.TryValidate(out string failure), Is.True, failure);

            migrated.Domains.Single(domain =>
                    domain.DomainId == "a.tests.existing")
                .PayloadJson = "{\"mutated\":true}";
            Assert.That(source.Domains.Single().PayloadJson, Is.EqualTo("{}"));
            Assert.That(codec.Serialize(source),
                Is.EqualTo(serializedSourceBefore));
        }

        [Test]
        public void MigrationRejectsInvalidVersionAndPreexistingDomain()
        {
            var migration = new SatsumaKeyAccessSaveMigration();

            Assert.Throws<ArgumentException>(() => migration.Migrate(
                null,
                new UnresolvedContentReport()));
            Assert.Throws<ArgumentException>(() => migration.Migrate(
                CurrentDocument(),
                new UnresolvedContentReport()));

            SaveDocument preexisting = VersionSixteenDocument(
                Envelope(hasAccess: false));
            Assert.Throws<InvalidDataException>(() => migration.Migrate(
                preexisting,
                new UnresolvedContentReport()));
            Assert.That(preexisting.Header.DocumentVersion, Is.EqualTo(16));
            Assert.That(preexisting.Domains.Single().DomainId,
                Is.EqualTo(SatsumaKeyAccessSaveParticipant.DomainId));
        }

        [Test]
        public void GlobalKeyRestoreRunsBeforeVehicleAssemblyPhase()
        {
            var state = new SatsumaKeyAccessState();
            var later = new RecordingParticipant(state);
            var registry = new SaveParticipantRegistry(
                new ISaveParticipant[]
                {
                    later,
                    new SatsumaKeyAccessSaveParticipant(state),
                });
            SaveDocument document = CurrentDocument(
                Envelope(hasAccess: false),
                LaterEnvelope());

            PreparedSaveRestore prepared = registry.PrepareRestore(
                document,
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());
            registry.ApplyRestore(
                prepared,
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());

            Assert.That(later.KeyAccessObservedDuringApply, Is.False);
            Assert.That(registry.OrderedParticipants[0].Descriptor.DomainId,
                Is.EqualTo(SatsumaKeyAccessSaveParticipant.DomainId));
        }

        [Test]
        public void LaterApplyFailureRollsKeyStateBack()
        {
            var state = new SatsumaKeyAccessState();
            var later = new RecordingParticipant(state)
            {
                ThrowDuringApply = true,
            };
            var registry = new SaveParticipantRegistry(
                new ISaveParticipant[]
                {
                    new SatsumaKeyAccessSaveParticipant(state),
                    later,
                });
            PreparedSaveRestore prepared = registry.PrepareRestore(
                CurrentDocument(
                    Envelope(hasAccess: false),
                    LaterEnvelope()),
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());

            Assert.Throws<InvalidOperationException>(() =>
                registry.ApplyRestore(
                    prepared,
                    new UnresolvedContentReport(),
                    new DeferredStableEntityStore()));

            Assert.That(later.KeyAccessObservedDuringApply, Is.False);
            Assert.That(state.HasAccess, Is.True);
        }

        [Test]
        public void CoordinatorLoadsVersionSixteenOverRevokedStateAsFreshAccess()
        {
            var state = new SatsumaKeyAccessState();
            state.SetAccess(false);
            var participant = new SatsumaKeyAccessSaveParticipant(state);
            var storage = new MemoryStorage(VersionSixteenDocument());
            var coordinator = new SaveCoordinator(
                storage,
                new SaveParticipantRegistry(
                    new ISaveParticipant[] { participant }),
                new VersionSixteenKeyMigrationPipeline(),
                new DeferredStableEntityStore());

            SaveLoadResult result = coordinator.Load("slot-key");

            Assert.That(result.Document.Header.DocumentVersion, Is.EqualTo(SaveDocument.CurrentDocumentVersion));
            Assert.That(state.HasAccess, Is.True);
            Assert.That(storage.Source.Header.DocumentVersion, Is.EqualTo(16));
            Assert.That(storage.Source.Domains, Is.Empty);
        }

        private static SaveDomainEnvelope Envelope(bool hasAccess) =>
            Envelope(new SatsumaKeyAccessSaveDto
            {
                hasAccess = hasAccess,
            });

        private static SaveDomainEnvelope Envelope(
            SatsumaKeyAccessSaveDto dto) => new()
        {
            DomainId = SatsumaKeyAccessSaveParticipant.DomainId,
            SchemaVersion = SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
            Required = true,
            PayloadJson = SaveParticipantJson.Serialize(dto),
        };

        private static SaveDomainEnvelope LaterEnvelope() => new()
        {
            DomainId = LaterDomainId,
            SchemaVersion = 1,
            Required = true,
            PayloadJson = "false",
        };

        private static SaveDocument CurrentDocument(
            params SaveDomainEnvelope[] domains) => Document(
                SaveDocument.CurrentDocumentVersion,
                domains);

        private static SaveDocument VersionSixteenDocument(
            params SaveDomainEnvelope[] domains) => Document(16, domains);

        private static SaveDocument Document(
            int version,
            params SaveDomainEnvelope[] domains)
        {
            string timestamp = new DateTimeOffset(
                    2026, 9, 5, 0, 0, 0, TimeSpan.Zero)
                .ToString("O", CultureInfo.InvariantCulture);
            return new SaveDocument
            {
                Header = new SaveHeader
                {
                    FormatId = SaveHeader.CurrentFormatId,
                    DocumentVersion = version,
                    SaveId = "save-key-tests",
                    SlotId = "slot-key",
                    BuildId = "tests",
                    CreatedUtc = timestamp,
                    UpdatedUtc = timestamp,
                },
                Metadata = new SaveMetadata(),
                Domains = domains ?? Array.Empty<SaveDomainEnvelope>(),
            };
        }

        private static SaveRestorePreparationContext PreparationContext() =>
            new(
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());

        private static SaveRestoreContext RestoreContext() => new(
            new UnresolvedContentReport(),
            new DeferredStableEntityStore());

        private sealed class RecordingParticipant : ISaveParticipant
        {
            private readonly ISatsumaKeyAccess keyAccess;

            public RecordingParticipant(ISatsumaKeyAccess configuredKeyAccess)
            {
                keyAccess = configuredKeyAccess;
            }

            public SaveParticipantDescriptor Descriptor { get; } =
                new(
                    LaterDomainId,
                    1,
                    required: true,
                    SaveRestorePhase.VehicleAssembly);

            public bool? KeyAccessObservedDuringApply { get; private set; }
            public bool ThrowDuringApply { get; set; }

            public string CapturePayload() => "false";

            public object PrepareRestore(
                SaveDomainEnvelope envelope,
                SaveRestorePreparationContext context) => false;

            public object CaptureCheckpoint() => null;

            public void ApplyPreparedRestore(
                object preparedState,
                SaveRestoreContext context)
            {
                KeyAccessObservedDuringApply = keyAccess.HasAccess;
                if (ThrowDuringApply)
                {
                    throw new InvalidOperationException("expected failure");
                }
            }

            public void Rollback(object checkpoint)
            {
            }
        }

        private sealed class VersionSixteenKeyMigrationPipeline :
            ISaveMigrationPipeline
        {
            public SaveDocument MigrateToCurrent(
                SaveDocument source,
                UnresolvedContentReport unresolvedContent)
            {
                if (source?.Header?.DocumentVersion != 16)
                {
                    throw new NotSupportedException(
                        "Test pipeline accepts only version 16.");
                }

                SaveDocument keyMigrated = new SatsumaKeyAccessSaveMigration().Migrate(source, unresolvedContent);
                return new SatsumaDynamicAssemblySaveMigration().Migrate(keyMigrated, unresolvedContent);
            }
        }

        private sealed class MemoryStorage : ISaveStorage
        {
            public MemoryStorage(SaveDocument source)
            {
                Source = source;
            }

            public SaveDocument Source { get; private set; }

            public SaveWriteResult Write(string slotId, SaveDocument document)
            {
                Source = document.DeepClone();
                return new SaveWriteResult(
                    slotId,
                    "memory://" + slotId,
                    Source.DeepClone());
            }

            public SaveReadResult Read(
                string slotId,
                bool allowRecovery = true) => new(
                    slotId,
                    SaveReadStatus.Loaded,
                    Source.DeepClone(),
                    "memory://" + slotId,
                    Array.Empty<string>(),
                    string.Empty);

            public IReadOnlyList<SaveSlotSummary> EnumerateSlots() =>
                Array.Empty<SaveSlotSummary>();
        }
    }
}
