using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Audio
{
    [Serializable]
    public sealed class AudioEventMapEntry
    {
        [SerializeField] private string audioEventId = string.Empty;
        [SerializeField] private string backendEventName = string.Empty;
        [SerializeField] private string requiredBankName = string.Empty;
        [SerializeField] private bool spatialized = true;
        [SerializeField] private bool allowMultiple = true;

        public AudioEventMapEntry()
        {
        }

        public AudioEventMapEntry(
            string stableId,
            string backendName,
            string bankName,
            bool isSpatialized = true,
            bool canOverlap = true)
        {
            audioEventId = stableId ?? string.Empty;
            backendEventName = backendName ?? string.Empty;
            requiredBankName = bankName ?? string.Empty;
            spatialized = isSpatialized;
            allowMultiple = canOverlap;
        }

        public string StableId => audioEventId;
        public string BackendEventName => backendEventName;
        public string RequiredBankName => requiredBankName;
        public bool Spatialized => spatialized;
        public bool AllowMultiple => allowMultiple;

        public bool TryGetId(out AudioEventId id, out string failure)
        {
            if (!AudioStableId.TryValidate(audioEventId, out failure))
            {
                id = default;
                return false;
            }

            id = new AudioEventId(audioEventId);
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "AudioEventMap",
        menuName = "MSC Remake/Audio/Event Map")]
    public sealed class AudioEventMap : ScriptableObject
    {
        [SerializeField] private AudioEventMapEntry[] entries =
            Array.Empty<AudioEventMapEntry>();

        public IReadOnlyList<AudioEventMapEntry> Entries => entries;

        public bool TryGet(AudioEventId eventId, out AudioEventMapEntry entry)
        {
            if (eventId.IsEmpty)
            {
                entry = null;
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                AudioEventMapEntry candidate = entries[index];
                if (candidate != null &&
                    string.Equals(candidate.StableId, eventId.Value, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForTests(params AudioEventMapEntry[] configuredEntries) =>
            entries = configuredEntries ?? Array.Empty<AudioEventMapEntry>();
#endif
    }
}
