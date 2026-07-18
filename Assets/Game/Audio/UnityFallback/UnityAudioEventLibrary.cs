using System;
using System.Collections.Generic;
using MSC.Audio;
using UnityEngine;

namespace MSC.Audio.UnityFallback
{
    public enum UnityAudioCategory
    {
        Vehicle = 0,
        Effects = 1,
        Ambience = 2,
        Music = 3,
        UserInterface = 4,
    }

    /// <summary>
    /// Project-owned Unity Audio mapping used only by the development fallback.
    /// AudioClip references in this asset must point to project-owned content;
    /// donor diagnostic clips continue to be loaded from external staging.
    /// </summary>
    [Serializable]
    public sealed class UnityAudioEventDefinition
    {
        [SerializeField] private string eventId = string.Empty;
        [SerializeField] private AudioClip clip;
        [SerializeField] private UnityAudioCategory category = UnityAudioCategory.Effects;
        [SerializeField] private bool loop;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] private float minimumDistanceMeters = 1f;
        [SerializeField, Min(0.01f)] private float maximumDistanceMeters = 40f;

        public string EventId => eventId;
        public AudioClip Clip => clip;
        public UnityAudioCategory Category => category;
        public bool Loop => loop;
        public float Volume => Mathf.Clamp01(volume);
        public float Pitch => Mathf.Clamp(pitch, 0.1f, 3f);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public float MinimumDistanceMeters => Mathf.Max(0.01f, minimumDistanceMeters);
        public float MaximumDistanceMeters => Mathf.Max(MinimumDistanceMeters, maximumDistanceMeters);

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredEventId,
            AudioClip configuredClip,
            UnityAudioCategory configuredCategory,
            bool configuredLoop,
            float configuredVolume,
            float configuredPitch,
            float configuredSpatialBlend,
            float configuredMinimumDistanceMeters,
            float configuredMaximumDistanceMeters)
        {
            eventId = configuredEventId?.Trim() ?? string.Empty;
            clip = configuredClip;
            category = configuredCategory;
            loop = configuredLoop;
            volume = Mathf.Clamp01(configuredVolume);
            pitch = Mathf.Clamp(configuredPitch, 0.1f, 3f);
            spatialBlend = Mathf.Clamp01(configuredSpatialBlend);
            minimumDistanceMeters = Mathf.Max(0.01f, configuredMinimumDistanceMeters);
            maximumDistanceMeters = Mathf.Max(
                minimumDistanceMeters,
                configuredMaximumDistanceMeters);
        }
#endif

        internal bool Validate(int index, List<string> failures)
        {
            bool valid = true;
            try
            {
                _ = new AudioEventId(eventId);
            }
            catch (ArgumentException exception)
            {
                failures.Add($"Entry {index} has an invalid event ID: {exception.Message}");
                valid = false;
            }

            if (clip == null)
            {
                failures.Add($"Entry {index} ({eventId}) has no AudioClip.");
                valid = false;
            }

            if (!Enum.IsDefined(typeof(UnityAudioCategory), category))
            {
                failures.Add($"Entry {index} ({eventId}) has an invalid category.");
                valid = false;
            }

            return valid;
        }
    }

    [CreateAssetMenu(
        fileName = "UnityAudioEventLibrary",
        menuName = "MSC Remake/Audio/Unity Fallback Event Library")]
    public sealed class UnityAudioEventLibrary : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private UnityAudioEventDefinition[] events =
            Array.Empty<UnityAudioEventDefinition>();

        [NonSerialized] private Dictionary<string, UnityAudioEventDefinition> lookup;

        public int DefinitionCount => events?.Length ?? 0;

        public bool TryResolve(
            AudioEventId eventId,
            out UnityAudioEventDefinition definition)
        {
            if (eventId.IsEmpty)
            {
                definition = null;
                return false;
            }

            EnsureLookup();
            return lookup.TryGetValue(eventId.Value, out definition);
        }

        public bool Validate(out string[] failures)
        {
            var results = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            UnityAudioEventDefinition[] definitions = events ??
                                                      Array.Empty<UnityAudioEventDefinition>();

            for (int index = 0; index < definitions.Length; index++)
            {
                UnityAudioEventDefinition definition = definitions[index];
                if (definition == null)
                {
                    results.Add($"Entry {index} is null.");
                    continue;
                }

                bool valid = definition.Validate(index, results);
                if (valid && !ids.Add(definition.EventId))
                {
                    results.Add($"Duplicate Unity fallback event ID: {definition.EventId}.");
                }
            }

            failures = results.ToArray();
            return failures.Length == 0;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            lookup = null;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            params UnityAudioEventDefinition[] configuredEvents)
        {
            events = configuredEvents ?? Array.Empty<UnityAudioEventDefinition>();
            lookup = null;
        }
#endif

        private void OnValidate()
        {
            lookup = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, UnityAudioEventDefinition>(StringComparer.Ordinal);
            UnityAudioEventDefinition[] definitions = events ??
                                                      Array.Empty<UnityAudioEventDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                UnityAudioEventDefinition definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.EventId))
                {
                    continue;
                }

                if (!lookup.ContainsKey(definition.EventId))
                {
                    lookup.Add(definition.EventId, definition);
                }
            }
        }
    }
}
