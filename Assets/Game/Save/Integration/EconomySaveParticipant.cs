using System;
using System.IO;
using System.Linq;
using MSC.Economy;
using MSC.Save;

namespace MSC.Save.Integration
{
    internal sealed class EconomySaveParticipant : ISaveParticipant
    {
        public const string DomainId = "economy.player";

        private readonly EconomyRuntime economy;
        private readonly ProductionEnvironmentRestoreBridge restoreBridge;

        public EconomySaveParticipant(EconomyRuntime configuredEconomy)
            : this(configuredEconomy, null)
        {
        }

        public EconomySaveParticipant(
            EconomyRuntime configuredEconomy,
            ProductionEnvironmentRestoreBridge configuredRestoreBridge)
        {
            economy = configuredEconomy ??
                throw new ArgumentNullException(nameof(configuredEconomy));
            restoreBridge = configuredRestoreBridge;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                EconomyStateDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState,
                CoreTimeSaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(economy.CaptureDto());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            EconomyStateDto dto = SaveParticipantJson.Deserialize<EconomyStateDto>(
                envelope.PayloadJson);
            if (!dto.TryValidate(economy.Catalog, out string failure))
            {
                throw new InvalidDataException(
                    "Economy save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() => economy.CaptureDto();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            EconomyStateDto dto = (EconomyStateDto)preparedState;
            bool restored = restoreBridge == null
                ? economy.TryRestoreDto(dto, out string failure)
                : economy.TryRestoreDtoForStagedClock(
                    dto,
                    restoreBridge.GetPreparedAuthoritativeDayIndex(),
                    out failure);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "Economy restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            EconomyStateDto dto = (EconomyStateDto)checkpoint;
            bool restored = restoreBridge == null
                ? economy.TryRestoreDto(dto, out string failure)
                : economy.TryRestoreDtoForStagedClock(
                    dto,
                    restoreBridge.GetRollbackAuthoritativeDayIndex(),
                    out failure);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "Economy rollback failed: " + failure);
            }
        }
    }

    internal sealed class Milestone12AEconomySaveMigration :
        ISaveDocumentMigration
    {
        private readonly EconomyStateDto freshState;
        private readonly bool domainRequired;

        public Milestone12AEconomySaveMigration(
            EconomyStateDto configuredFreshState,
            bool domainRequired)
        {
            if (domainRequired && configuredFreshState == null)
            {
                throw new ArgumentNullException(nameof(configuredFreshState));
            }

            freshState = configuredFreshState?.DeepClone();
            this.domainRequired = domainRequired;
        }

        public int FromVersion => 13;
        public int ToVersion => 14;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "12A economy migration requires a version 13 save document.",
                    nameof(source));
            }

            if ((source.Domains ?? Array.Empty<SaveDomainEnvelope>()).Any(
                    domain => string.Equals(
                        domain?.DomainId,
                        EconomySaveParticipant.DomainId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Version 13 save unexpectedly already contains economy state.");
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope[] addition = freshState == null
                ? Array.Empty<SaveDomainEnvelope>()
                : new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = EconomySaveParticipant.DomainId,
                        SchemaVersion = EconomyStateDto.CurrentSchemaVersion,
                        Required = domainRequired,
                        PayloadJson = SaveParticipantJson.Serialize(freshState),
                    },
                };
            migrated.Domains = (migrated.Domains ??
                    Array.Empty<SaveDomainEnvelope>())
                .Concat(addition)
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }
}
