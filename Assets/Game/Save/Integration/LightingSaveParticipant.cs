using System;
using System.Collections.Generic;
using System.IO;
using MSC.Lighting;
using MSC.Save;

namespace MSC.Save.Integration
{
    internal sealed class LightingSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "lighting.electrical-grid";
        private const string FlashlightSourceId = "source.item.flashlight";
        private const string TrafficVehicleSourceId = "source.traffic.vehicle";
        private const string FlashlightCircuitPrefix = "grid.item.flashlight.";
        private const string TrafficVehicleCircuitPrefix =
            "grid.traffic.vehicle.";
        private const string FlashlightSwitchPrefix =
            "switch.item.flashlight.";
        private readonly ElectricalGridService grid;

        public LightingSaveParticipant(ElectricalGridService configuredGrid)
        {
            grid = configuredGrid ??
                throw new ArgumentNullException(nameof(configuredGrid));
            // Optional preserves compatibility with accepted v15 saves. All
            // newly written saves include the domain.
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                ElectricalGridStateDto.CurrentSchemaVersion,
                required: false,
                SaveRestorePhase.GlobalState);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(CapturePersistentState());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            ElectricalGridStateDto state =
                SaveParticipantJson.Deserialize<ElectricalGridStateDto>(
                    envelope.PayloadJson);
            if (state == null)
            {
                throw new InvalidDataException(
                    "Lighting-grid save preflight failed: payload is null.");
            }

            if (!state.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Lighting-grid save preflight failed: " + failure);
            }

            // Older schema-1 saves captured electrical nodes created by
            // runtime-only flashlight and traffic presenters. Those nodes are
            // not authoritative state: the item domain owns flashlight state,
            // while traffic lighting is derived from the active presentation
            // and environment policy. They may legitimately be absent during
            // the early global-state restore, so discard only these explicitly
            // recognized legacy entries before the grid performs its otherwise
            // strict unknown-ID check.
            return RemoveRuntimeDerivedState(state);
        }

        public object CaptureCheckpoint() => CapturePersistentState();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!grid.TryRestoreState(
                    (ElectricalGridStateDto)preparedState,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Lighting-grid restore failed: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!grid.TryRestoreState(
                    (ElectricalGridStateDto)checkpoint,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Lighting-grid rollback failed: " + failure);
            }
        }

        private ElectricalGridStateDto CapturePersistentState() =>
            RemoveRuntimeDerivedState(grid.CaptureState());

        private static ElectricalGridStateDto RemoveRuntimeDerivedState(
            ElectricalGridStateDto state)
        {
            return new ElectricalGridStateDto
            {
                schemaVersion = state.schemaVersion,
                sources = Filter(
                    state.sources,
                    IsRuntimeDerivedSource),
                circuits = Filter(
                    state.circuits,
                    IsRuntimeDerivedCircuit),
                switches = Filter(
                    state.switches,
                    IsRuntimeDerivedSwitch),
            };
        }

        private static ElectricalBooleanStateDto[] Filter(
            ElectricalBooleanStateDto[] values,
            Predicate<string> isRuntimeDerived)
        {
            values ??= Array.Empty<ElectricalBooleanStateDto>();
            var persistent = new List<ElectricalBooleanStateDto>(values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                ElectricalBooleanStateDto value = values[index];
                if (!isRuntimeDerived(value.id))
                {
                    persistent.Add(value);
                }
            }

            return persistent.ToArray();
        }

        private static bool IsRuntimeDerivedSource(string id) =>
            string.Equals(id, FlashlightSourceId, StringComparison.Ordinal) ||
            string.Equals(id, TrafficVehicleSourceId, StringComparison.Ordinal);

        private static bool IsRuntimeDerivedCircuit(string id) =>
            id.StartsWith(FlashlightCircuitPrefix, StringComparison.Ordinal) ||
            id.StartsWith(
                TrafficVehicleCircuitPrefix,
                StringComparison.Ordinal);

        private static bool IsRuntimeDerivedSwitch(string id) =>
            id.StartsWith(FlashlightSwitchPrefix, StringComparison.Ordinal);
    }
}
