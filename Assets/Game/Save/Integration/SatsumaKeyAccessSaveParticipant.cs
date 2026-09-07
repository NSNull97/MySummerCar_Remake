using System;
using System.IO;
using System.Linq;
using MSC.Vehicle;

namespace MSC.Save.Integration
{
    [Serializable]
    internal sealed class SatsumaKeyAccessSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentConfigurationId =
            "vehicle.satsuma.key-access.native.v1";

        public int schemaVersion = CurrentSchemaVersion;
        public string configurationId = CurrentConfigurationId;
        public string keyId = SatsumaKeyAccessState.KeyId;
        public bool hasAccess = true;

        public static SatsumaKeyAccessSaveDto Fresh() => new();

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure =
                    $"Schema {schemaVersion} is not supported; expected {CurrentSchemaVersion}.";
                return false;
            }

            if (!string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal))
            {
                failure = "Configuration identity is incompatible.";
                return false;
            }

            if (!string.Equals(
                    keyId,
                    SatsumaKeyAccessState.KeyId,
                    StringComparison.Ordinal))
            {
                failure = "Logical key identity is incompatible.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    internal sealed class SatsumaKeyAccessSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "vehicle.satsuma.key-access";

        private readonly SatsumaKeyAccessState state;

        public SatsumaKeyAccessSaveParticipant(
            SatsumaKeyAccessState configuredState)
        {
            state = configuredState ??
                throw new ArgumentNullException(nameof(configuredState));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() => SaveParticipantJson.Serialize(
            new SatsumaKeyAccessSaveDto
            {
                hasAccess = state.HasAccess,
            });

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            SatsumaKeyAccessSaveDto dto =
                SaveParticipantJson.Deserialize<SatsumaKeyAccessSaveDto>(
                    envelope?.PayloadJson);
            if (!dto.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Satsuma key-access save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() => state.HasAccess;

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            state.SetAccess(
                ((SatsumaKeyAccessSaveDto)preparedState).hasAccess);
        }

        public void Rollback(object checkpoint)
        {
            state.SetAccess((bool)checkpoint);
        }
    }

    /// <summary>
    /// Version 16 predates explicit logical Satsuma-key ownership. The locked
    /// donor/new-game contract grants that key, so legacy saves receive true.
    /// </summary>
    internal sealed class SatsumaKeyAccessSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 16;

        public int ToVersion => 17;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "Satsuma key-access migration requires a version 16 save document.",
                    nameof(source));
            }

            if ((source.Domains ?? Array.Empty<SaveDomainEnvelope>()).Any(
                    domain => string.Equals(
                        domain?.DomainId,
                        SatsumaKeyAccessSaveParticipant.DomainId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Version 16 save unexpectedly already contains Satsuma key-access state.");
            }

            SaveDocument migrated = source.DeepClone();
            migrated.Domains = (migrated.Domains ??
                    Array.Empty<SaveDomainEnvelope>())
                .Concat(new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = SatsumaKeyAccessSaveParticipant.DomainId,
                        SchemaVersion =
                            SatsumaKeyAccessSaveDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = SaveParticipantJson.Serialize(
                            SatsumaKeyAccessSaveDto.Fresh()),
                    },
                })
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }
    }
}
