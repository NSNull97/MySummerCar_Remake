using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Audio
{
    [Serializable]
    public sealed class AudioParameterMapEntry
    {
        [SerializeField] private string audioParameterId = string.Empty;
        [SerializeField] private string backendParameterName = string.Empty;
        [SerializeField] private float minimumValue;
        [SerializeField] private float maximumValue = 1f;
        [SerializeField] private float defaultValue;
        [SerializeField, Min(0f)] private float updateDeadband = 0.001f;

        public AudioParameterMapEntry()
        {
        }

        public AudioParameterMapEntry(
            string stableId,
            string backendName,
            float minimum,
            float maximum,
            float defaultParameterValue,
            float deadband = 0.001f)
        {
            audioParameterId = stableId ?? string.Empty;
            backendParameterName = backendName ?? string.Empty;
            minimumValue = AudioMath.FiniteOrZero(minimum);
            maximumValue = AudioMath.FiniteOrZero(maximum);
            defaultValue = AudioMath.FiniteOrZero(defaultParameterValue);
            updateDeadband = AudioMath.NonNegative(deadband);
        }

        public string StableId => audioParameterId;
        public string BackendParameterName => backendParameterName;
        public float MinimumValue => minimumValue;
        public float MaximumValue => maximumValue;
        public float DefaultValue => defaultValue;
        public float UpdateDeadband => updateDeadband;

        public float Clamp(float value)
        {
            value = AudioMath.FiniteOrZero(value);
            return maximumValue < minimumValue
                ? minimumValue
                : Mathf.Clamp(value, minimumValue, maximumValue);
        }

        public bool TryGetId(out AudioParameterId id, out string failure)
        {
            if (!AudioStableId.TryValidate(audioParameterId, out failure))
            {
                id = default;
                return false;
            }

            id = new AudioParameterId(audioParameterId);
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "AudioParameterMap",
        menuName = "MSC Remake/Audio/Parameter Map")]
    public sealed class AudioParameterMap : ScriptableObject
    {
        [SerializeField] private AudioParameterMapEntry[] entries =
            Array.Empty<AudioParameterMapEntry>();

        public IReadOnlyList<AudioParameterMapEntry> Entries => entries;

        public bool TryGet(
            AudioParameterId parameterId,
            out AudioParameterMapEntry entry)
        {
            if (parameterId.IsEmpty)
            {
                entry = null;
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                AudioParameterMapEntry candidate = entries[index];
                if (candidate != null &&
                    string.Equals(
                        candidate.StableId,
                        parameterId.Value,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForTests(params AudioParameterMapEntry[] configuredEntries) =>
            entries = configuredEntries ?? Array.Empty<AudioParameterMapEntry>();
#endif
    }
}
