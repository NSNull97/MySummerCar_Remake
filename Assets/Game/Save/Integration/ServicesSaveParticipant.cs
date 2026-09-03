using System;
using System.IO;
using System.Linq;
using MSC.Services;

namespace MSC.Save.Integration
{
    internal sealed class ServicesSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "services.state";

        private readonly ServiceRuntime runtime;
        private readonly ProductionEnvironmentRestoreBridge restoreBridge;

        public ServicesSaveParticipant(ServiceRuntime configuredRuntime)
            : this(configuredRuntime, null)
        {
        }

        public ServicesSaveParticipant(
            ServiceRuntime configuredRuntime,
            ProductionEnvironmentRestoreBridge configuredRestoreBridge)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            restoreBridge = configuredRestoreBridge;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                ServiceStateDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState,
                CoreTimeSaveParticipant.DomainId,
                EconomySaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(runtime.CaptureDto());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            ServiceStateDto dto =
                SaveParticipantJson.Deserialize<ServiceStateDto>(
                    envelope.PayloadJson);
            string failure = "State is missing.";
            if (dto == null ||
                !runtime.TryValidateRestoreCandidate(dto, out failure))
            {
                throw new InvalidDataException(
                    "Services save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() => runtime.CaptureDto();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            ServiceStateDto dto = (ServiceStateDto)preparedState;
            bool restored = restoreBridge == null
                ? runtime.TryRestoreDto(dto, out string failure)
                : runtime.TryRestoreDtoForStagedClock(
                    dto,
                    restoreBridge.GetPreparedAuthoritativeDayIndex(),
                    out failure);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "Services restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            ServiceStateDto dto = (ServiceStateDto)checkpoint;
            bool restored = restoreBridge == null
                ? runtime.TryRestoreDto(dto, out string failure)
                : runtime.TryRestoreDtoForStagedClock(
                    dto,
                    restoreBridge.GetRollbackAuthoritativeDayIndex(),
                    out failure);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "Services rollback failed: " + failure);
            }
        }
    }

    internal sealed class Milestone12AServicesSaveMigration :
        ISaveDocumentMigration
    {
        private readonly ServiceStateDto freshState;
        private readonly bool domainRequired;

        public Milestone12AServicesSaveMigration(
            ServiceStateDto configuredFreshState,
            bool domainRequired)
        {
            if (domainRequired && configuredFreshState == null)
            {
                throw new ArgumentNullException(nameof(configuredFreshState));
            }

            freshState = configuredFreshState?.DeepClone();
            this.domainRequired = domainRequired;
        }

        public int FromVersion => 14;
        public int ToVersion => 15;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "12A-S1 services migration requires a version 14 save document.",
                    nameof(source));
            }

            if ((source.Domains ?? Array.Empty<SaveDomainEnvelope>()).Any(
                    domain => string.Equals(
                        domain?.DomainId,
                        ServicesSaveParticipant.DomainId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Version 14 save unexpectedly already contains services state.");
            }

            if (freshState == null)
            {
                throw new InvalidOperationException(
                    "Cannot migrate a version 14 save to services-aware version 15 " +
                    "without an initialized ServiceRuntime.");
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope[] addition =
                new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = ServicesSaveParticipant.DomainId,
                        SchemaVersion = ServiceStateDto.CurrentSchemaVersion,
                        Required = domainRequired,
                        PayloadJson =
                            SaveParticipantJson.Serialize(freshState),
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
