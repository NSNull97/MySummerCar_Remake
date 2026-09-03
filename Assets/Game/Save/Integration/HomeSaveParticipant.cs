using System;
using System.IO;
using System.Linq;
using MSC.Home;
using MSC.Save;

namespace MSC.Save.Integration
{
    internal sealed class HomeSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "home.state";

        private readonly HomeSystemRuntime home;

        public HomeSaveParticipant(HomeSystemRuntime configuredHome)
        {
            home = configuredHome ??
                throw new ArgumentNullException(nameof(configuredHome));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                HomeStateDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(home.CaptureDto());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            HomeStateDto dto =
                SaveParticipantJson.Deserialize<HomeStateDto>(
                    envelope.PayloadJson);
            if (!dto.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Home-state save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint() =>
            home.CaptureDto();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!home.TryRestoreDto(
                    (HomeStateDto)preparedState,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Home-state restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!home.TryRestoreDto(
                    (HomeStateDto)checkpoint,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Home-state rollback failed: " + failure);
            }
        }
    }

    /// <summary>
    /// Adds the authoritative domestic-state domain to accepted version 5
    /// saves. Fresh state starts with active fridge power, all player-operated
    /// heat/water fixtures inactive and the sauna at its validated ambient
    /// temperature.
    /// </summary>
    internal sealed class Milestone09CHomeSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 5;
        public int ToVersion => 6;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "09C home migration requires a version 5 save document.",
                    nameof(source));
            }

            if (source.Domains.Any(domain => string.Equals(
                    domain?.DomainId,
                    HomeSaveParticipant.DomainId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Version 5 save unexpectedly already contains home state.");
            }

            HomeStateDto freshState = HomeStateDto.Fresh();
            if (!freshState.TryValidate(out string failure))
            {
                throw new InvalidOperationException(
                    "Fresh home state is invalid: " + failure);
            }

            SaveDocument migrated = source.DeepClone();
            migrated.Header.DocumentVersion = ToVersion;
            migrated.Domains = migrated.Domains
                .Concat(new[]
                {
                    new SaveDomainEnvelope
                    {
                        DomainId = HomeSaveParticipant.DomainId,
                        SchemaVersion =
                            HomeStateDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson =
                            SaveParticipantJson.Serialize(freshState),
                    },
                })
                .OrderBy(domain => domain.DomainId, StringComparer.Ordinal)
                .ToArray();
            return migrated;
        }
    }

    /// <summary>
    /// Upgrades the 09C home domain from discrete sauna controls to the
    /// donor-compatible ranged knob state. Legacy fields remain populated for
    /// compatibility while the new knob values become authoritative.
    /// </summary>
    internal sealed class Milestone09CSaunaControlsSaveMigration :
        ISaveDocumentMigration
    {
        public int FromVersion => 6;
        public int ToVersion => 7;

        public SaveDocument Migrate(
            SaveDocument source,
            UnresolvedContentReport unresolvedContent)
        {
            if (source?.Header == null ||
                source.Header.DocumentVersion != FromVersion)
            {
                throw new ArgumentException(
                    "09C sauna-controls migration requires a version 6 save document.",
                    nameof(source));
            }

            SaveDocument migrated = source.DeepClone();
            SaveDomainEnvelope homeEnvelope = migrated.Domains
                .SingleOrDefault(domain => string.Equals(
                    domain?.DomainId,
                    HomeSaveParticipant.DomainId,
                    StringComparison.Ordinal));
            if (homeEnvelope == null)
            {
                throw new InvalidOperationException(
                    "Version 6 save has no required home-state domain.");
            }

            if (!homeEnvelope.Required)
            {
                throw new InvalidOperationException(
                    "Version 6 home-state domain is unexpectedly optional.");
            }

            HomeStateDto migratedState;
            if (homeEnvelope.SchemaVersion == 1)
            {
                LegacyHomeStateDtoV1 legacyState =
                    SaveParticipantJson.Deserialize<LegacyHomeStateDtoV1>(
                        homeEnvelope.PayloadJson);
                if (!legacyState.TryValidate(out string failure))
                {
                    throw new InvalidDataException(
                        "Version 6 home-state payload is invalid: " +
                        failure);
                }

                migratedState = legacyState.Upgrade();
            }
            else if (homeEnvelope.SchemaVersion ==
                     HomeStateDto.CurrentSchemaVersion)
            {
                migratedState =
                    SaveParticipantJson.Deserialize<HomeStateDto>(
                        homeEnvelope.PayloadJson);
                if (!migratedState.TryValidate(out string failure))
                {
                    throw new InvalidDataException(
                        "Version 6 current home-state payload is invalid: " +
                        failure);
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Unsupported version 6 home-state schema " +
                    $"{homeEnvelope.SchemaVersion}.");
            }

            if (!migratedState.TryValidate(out string migratedFailure))
            {
                throw new InvalidDataException(
                    "Migrated home-state payload is invalid: " +
                    migratedFailure);
            }

            homeEnvelope.SchemaVersion = HomeStateDto.CurrentSchemaVersion;
            homeEnvelope.PayloadJson =
                SaveParticipantJson.Serialize(migratedState);
            migrated.Header.DocumentVersion = ToVersion;
            return migrated;
        }

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

            public bool TryValidate(out string failure)
            {
                if (schemaVersion != 1)
                {
                    failure =
                        $"Expected home-state payload schema 1, got {schemaVersion}.";
                    return false;
                }

                if (!float.IsFinite(electricSaunaTimerSecondsRemaining) ||
                    electricSaunaTimerSecondsRemaining < 0f ||
                    electricSaunaTimerSecondsRemaining >
                    24f * 60f * 60f ||
                    !float.IsFinite(saunaTemperatureCelsius) ||
                    saunaTemperatureCelsius < -80f ||
                    saunaTemperatureCelsius > 250f ||
                    !float.IsFinite(saunaSteamNormalized) ||
                    saunaSteamNormalized < 0f ||
                    saunaSteamNormalized > 1f)
                {
                    failure =
                        "Home-state values are outside supported limits.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            public HomeStateDto Upgrade()
            {
                float timerKnobDegrees =
                    electricSaunaTimerSecondsRemaining > 0f
                        ? Math.Min(
                            Math.Max(
                                electricSaunaTimerSecondsRemaining /
                                HomeSaunaControlRange.TimerSecondsPerDegree,
                                HomeSaunaControlRange.MinimumTimerDegrees),
                            HomeSaunaControlRange.MaximumTimerDegrees)
                        : HomeSaunaControlRange.MinimumTimerDegrees;
                // The provisional 30/60/90-minute timer is intentionally
                // migrated into the donor-evidenced 6..720-second range.
                // Keeping the old multi-hour countdown would produce a schema
                // 2 state that disagrees with its physical knob position.
                float migratedTimerSecondsRemaining =
                    electricSaunaTimerSecondsRemaining > 0f
                        ? timerKnobDegrees *
                          HomeSaunaControlRange.TimerSecondsPerDegree
                        : 0f;
                return new HomeStateDto
                {
                    schemaVersion = HomeStateDto.CurrentSchemaVersion,
                    kitchenTapOpen = kitchenTapOpen,
                    showerSwitchOn = showerSwitchOn,
                    showerValveOpen = showerValveOpen,
                    electricSaunaPowerOn = electricSaunaPowerOn,
                    electricSaunaTimerSecondsRemaining =
                        migratedTimerSecondsRemaining,
                    electricSaunaHeatKnobDegrees =
                        electricSaunaPowerOn
                            ? HomeSaunaControlRange.MaximumHeatDegrees
                            : HomeSaunaControlRange.MinimumHeatDegrees,
                    electricSaunaTimerKnobDegrees = timerKnobDegrees,
                    saunaTemperatureCelsius = saunaTemperatureCelsius,
                    saunaSteamNormalized = saunaSteamNormalized,
                    fridgePowered = fridgePowered,
                    stovePowered = stovePowered,
                    televisionPowered = televisionPowered,
                    fireplaceLit = fireplaceLit,
                };
            }
        }
    }
}
