using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Lighting
{
    public readonly struct ElectricalStateChanged
    {
        public ElectricalStateChanged(string stateId, bool value)
        {
            StateId = stateId;
            Value = value;
        }

        public string StateId { get; }
        public bool Value { get; }
    }

    [Serializable]
    public sealed class ElectricalGridStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public ElectricalBooleanStateDto[] sources =
            Array.Empty<ElectricalBooleanStateDto>();
        public ElectricalBooleanStateDto[] circuits =
            Array.Empty<ElectricalBooleanStateDto>();
        public ElectricalBooleanStateDto[] switches =
            Array.Empty<ElectricalBooleanStateDto>();

        public bool TryValidate(out string failure)
        {
            failure = string.Empty;
            if (schemaVersion != CurrentSchemaVersion ||
                !ValidateSet(sources, out failure) ||
                !ValidateSet(circuits, out failure) ||
                !ValidateSet(switches, out failure))
            {
                if (string.IsNullOrEmpty(failure))
                {
                    failure = "Electrical-grid schema is unsupported.";
                }

                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool ValidateSet(
            ElectricalBooleanStateDto[] values,
            out string failure)
        {
            values ??= Array.Empty<ElectricalBooleanStateDto>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                ElectricalBooleanStateDto value = values[index];
                if (value == null || string.IsNullOrWhiteSpace(value.id) ||
                    !ids.Add(value.id))
                {
                    failure =
                        "Electrical-grid state contains an invalid or duplicate ID.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ElectricalBooleanStateDto
    {
        public string id = string.Empty;
        public bool value;
    }

    public sealed class ElectricalGridService
    {
        private readonly Dictionary<string, bool> sources =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> circuitSources =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> circuits =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> switches =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public event Action<ElectricalStateChanged> StateChanged;

        public bool HasSource(string sourceId) => sources.ContainsKey(sourceId);
        public bool HasCircuit(string circuitId) => circuits.ContainsKey(circuitId);
        public bool HasSwitch(string switchId) => switches.ContainsKey(switchId);

        public void RegisterSource(string sourceId, bool available)
        {
            ValidateId(sourceId, nameof(sourceId));
            if (!sources.TryAdd(sourceId, available))
            {
                throw new InvalidOperationException(
                    $"Power source '{sourceId}' is already registered.");
            }
        }

        public void RegisterCircuit(
            string circuitId,
            string sourceId,
            bool enabled)
        {
            ValidateId(circuitId, nameof(circuitId));
            ValidateId(sourceId, nameof(sourceId));
            if (!sources.ContainsKey(sourceId))
            {
                throw new InvalidOperationException(
                    $"Circuit '{circuitId}' references missing source '{sourceId}'.");
            }

            if (!circuits.TryAdd(circuitId, enabled))
            {
                throw new InvalidOperationException(
                    $"Power circuit '{circuitId}' is already registered.");
            }

            circuitSources.Add(circuitId, sourceId);
        }

        public void RegisterSwitch(string switchId, bool isOn)
        {
            ValidateId(switchId, nameof(switchId));
            if (!switches.TryAdd(switchId, isOn))
            {
                throw new InvalidOperationException(
                    $"Light switch '{switchId}' is already registered.");
            }
        }

        public bool IsActuallyOn(
            string circuitId,
            string switchId,
            bool runtimePolicyAllows,
            bool fixtureIsAvailable)
        {
            if (!circuits.TryGetValue(circuitId, out bool circuitEnabled) ||
                !circuitSources.TryGetValue(circuitId, out string sourceId) ||
                !sources.TryGetValue(sourceId, out bool sourceAvailable))
            {
                return false;
            }

            bool switchState = string.IsNullOrWhiteSpace(switchId) ||
                switches.TryGetValue(switchId, out bool registeredSwitch) &&
                registeredSwitch;
            return sourceAvailable &&
                   circuitEnabled &&
                   switchState &&
                   runtimePolicyAllows &&
                   fixtureIsAvailable;
        }

        public bool TryGetSwitchState(string switchId, out bool isOn) =>
            switches.TryGetValue(switchId, out isOn);

        public void SetSourceAvailable(string sourceId, bool available) =>
            Set(sources, sourceId, available);

        public void SetCircuitEnabled(string circuitId, bool enabled) =>
            Set(circuits, circuitId, enabled);

        public void SetSwitchState(string switchId, bool isOn) =>
            Set(switches, switchId, isOn);

        public void ToggleSwitch(string switchId)
        {
            if (!switches.TryGetValue(switchId, out bool current))
            {
                throw new KeyNotFoundException(
                    $"Light switch '{switchId}' is not registered.");
            }

            SetSwitchState(switchId, !current);
        }

        public ElectricalGridStateDto CaptureState()
        {
            return new ElectricalGridStateDto
            {
                sources = Capture(sources),
                circuits = Capture(circuits),
                switches = Capture(switches),
            };
        }

        public bool TryRestoreState(
            ElectricalGridStateDto state,
            out string failure)
        {
            if (state == null)
            {
                failure = "Electrical grid state is required.";
                return false;
            }

            if (!state.TryValidate(out failure))
            {
                return false;
            }

            if (!CanApplyExisting(sources, state.sources, out failure) ||
                !CanApplyExisting(circuits, state.circuits, out failure) ||
                !CanApplyExisting(switches, state.switches, out failure))
            {
                return false;
            }

            ApplyExisting(sources, state.sources);
            ApplyExisting(circuits, state.circuits);
            ApplyExisting(switches, state.switches);

            failure = string.Empty;
            return true;
        }

        private void Set(
            Dictionary<string, bool> states,
            string id,
            bool value)
        {
            if (!states.TryGetValue(id, out bool previous))
            {
                throw new KeyNotFoundException(
                    $"Electrical state '{id}' is not registered.");
            }

            if (previous == value)
            {
                return;
            }

            states[id] = value;
            StateChanged?.Invoke(new ElectricalStateChanged(id, value));
        }

        private static ElectricalBooleanStateDto[] Capture(
            Dictionary<string, bool> source)
        {
            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            var result = new ElectricalBooleanStateDto[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                result[index] = new ElectricalBooleanStateDto
                {
                    id = key,
                    value = source[key],
                };
            }

            return result;
        }

        private static bool CanApplyExisting(
            Dictionary<string, bool> target,
            ElectricalBooleanStateDto[] values,
            out string failure)
        {
            values ??= Array.Empty<ElectricalBooleanStateDto>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                ElectricalBooleanStateDto value = values[index];
                if (value == null || string.IsNullOrWhiteSpace(value.id) ||
                    !seen.Add(value.id) || !target.ContainsKey(value.id))
                {
                    failure = "Electrical-grid state contains an unknown or duplicate ID.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private void ApplyExisting(
            Dictionary<string, bool> target,
            ElectricalBooleanStateDto[] values)
        {
            values ??= Array.Empty<ElectricalBooleanStateDto>();
            for (int index = 0; index < values.Length; index++)
            {
                ElectricalBooleanStateDto value = values[index];
                if (target[value.id] == value.value)
                {
                    continue;
                }

                target[value.id] = value.value;
                StateChanged?.Invoke(new ElectricalStateChanged(
                    value.id,
                    value.value));
            }
        }

        private static void ValidateId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Electrical IDs cannot be empty.",
                    parameterName);
            }
        }
    }
}
