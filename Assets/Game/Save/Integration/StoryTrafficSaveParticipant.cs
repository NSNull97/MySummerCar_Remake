using System;
using System.IO;
using MSC.NPC;
using MSC.Traffic;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Optional additive domain for physical story-traffic incidents. Keeping
    /// it separate lets accepted version-13 saves load unchanged; absent legacy
    /// payloads start from the project-owned fresh traffic state.
    /// </summary>
    internal sealed class StoryTrafficSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "traffic.state";

        private readonly NpcWorldRuntime runtime;
        private readonly TrafficWorldRuntime trafficRuntime;

        public StoryTrafficSaveParticipant(NpcWorldRuntime configuredRuntime)
            : this(configuredRuntime, null)
        {
        }

        public StoryTrafficSaveParticipant(
            NpcWorldRuntime configuredRuntime,
            TrafficWorldRuntime configuredTrafficRuntime)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            trafficRuntime = configuredTrafficRuntime;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                StoryTrafficStateDto.CurrentSchemaVersion,
                required: false,
                SaveRestorePhase.GlobalState,
                CoreTimeSaveParticipant.DomainId,
                NpcSaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload()
        {
            StoryTrafficStateDto dto = runtime.CaptureTrafficDto();
            if (trafficRuntime != null)
            {
                dto = trafficRuntime.AttachAmbientState(dto);
            }

            return SaveParticipantJson.Serialize(dto);
        }

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            StoryTrafficStateDto dto =
                SaveParticipantJson.Deserialize<StoryTrafficStateDto>(
                    envelope.PayloadJson);
            if (!runtime.TryValidateTrafficDto(dto, out string failure))
            {
                throw new InvalidDataException(
                    "Story-traffic save preflight failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryValidateAmbientActors(
                    dto.ambientActors,
                    out failure))
            {
                throw new InvalidDataException(
                    "Ambient-traffic save preflight failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryValidateTransportActors(
                    dto.transportActors,
                    out failure))
            {
                throw new InvalidDataException(
                    "Transport-traffic save preflight failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryValidateEventState(
                    dto.eventActors,
                    dto.eventGroups,
                    out failure))
            {
                throw new InvalidDataException(
                    "Event-traffic save preflight failed: " + failure);
            }

            return dto;
        }

        public object CaptureCheckpoint()
        {
            StoryTrafficStateDto dto = runtime.CaptureTrafficDto();
            return trafficRuntime != null
                ? trafficRuntime.AttachAmbientState(dto)
                : dto;
        }

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!runtime.TryRestoreTrafficDto(
                    (StoryTrafficStateDto)preparedState,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Story-traffic restore failed after preflight: " +
                    failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreAmbientActors(
                    ((StoryTrafficStateDto)preparedState).ambientActors,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Ambient-traffic restore failed after preflight: " +
                    failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreTransportActors(
                    ((StoryTrafficStateDto)preparedState).transportActors,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Transport-traffic restore failed after preflight: " +
                    failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreEventState(
                    ((StoryTrafficStateDto)preparedState).eventActors,
                    ((StoryTrafficStateDto)preparedState).eventGroups,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Event-traffic restore failed after preflight: " +
                    failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!runtime.TryRestoreTrafficDto(
                    (StoryTrafficStateDto)checkpoint,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Story-traffic rollback failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreAmbientActors(
                    ((StoryTrafficStateDto)checkpoint).ambientActors,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Ambient-traffic rollback failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreTransportActors(
                    ((StoryTrafficStateDto)checkpoint).transportActors,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Transport-traffic rollback failed: " + failure);
            }

            if (trafficRuntime != null &&
                !trafficRuntime.TryRestoreEventState(
                    ((StoryTrafficStateDto)checkpoint).eventActors,
                    ((StoryTrafficStateDto)checkpoint).eventGroups,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Event-traffic rollback failed: " + failure);
            }
        }
    }
}
