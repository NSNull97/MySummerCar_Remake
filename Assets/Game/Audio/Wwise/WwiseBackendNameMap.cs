using System;
using System.Collections.Generic;
using MSC.Audio;
using UnityEngine;

namespace MSC.Audio.Wwise
{
    [Serializable]
    public sealed class WwiseBackendNameMapEntry
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string backendName = string.Empty;

        public WwiseBackendNameMapEntry()
        {
        }

        public WwiseBackendNameMapEntry(string projectStableId, string wwiseName)
        {
            stableId = projectStableId?.Trim() ?? string.Empty;
            backendName = wwiseName?.Trim() ?? string.Empty;
        }

        public string StableId => stableId;
        public string BackendName => backendName;
    }

    /// <summary>
    /// Keeps project-owned switch/state stable IDs separate from Wwise object
    /// names. Event and RTPC names remain in AudioEventMap/AudioParameterMap.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WwiseBackendNameMap",
        menuName = "MSC Remake/Audio/Wwise Backend Name Map")]
    public sealed class WwiseBackendNameMap : ScriptableObject
    {
        [SerializeField] private WwiseBackendNameMapEntry[] entries =
            Array.Empty<WwiseBackendNameMapEntry>();

        public IReadOnlyList<WwiseBackendNameMapEntry> Entries => entries;

        public bool TryGet(AudioSwitchId id, out string backendName) =>
            TryGet(id.Value, out backendName);

        public bool TryGet(AudioStateId id, out string backendName) =>
            TryGet(id.Value, out backendName);

        private bool TryGet(string stableId, out string backendName)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                backendName = string.Empty;
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                WwiseBackendNameMapEntry candidate = entries[index];
                if (candidate != null &&
                    string.Equals(candidate.StableId, stableId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(candidate.BackendName))
                {
                    backendName = candidate.BackendName.Trim();
                    return true;
                }
            }

            backendName = string.Empty;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForTests(params WwiseBackendNameMapEntry[] configuredEntries) =>
            entries = configuredEntries ?? Array.Empty<WwiseBackendNameMapEntry>();
#endif
    }
}
