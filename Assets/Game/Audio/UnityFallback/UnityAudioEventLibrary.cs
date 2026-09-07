using System;
using System.Collections.Generic;
using MSC.Audio;
using UnityEngine;

namespace MSC.Audio.UnityFallback
{
    // Zero must remain Linear: old serialized libraries did not have this field.
    public enum UnityAudioDistanceRolloff
    {
        Linear = 0,
        Logarithmic = 1,
    }

    public enum UnityAudioCategory
    {
        Vehicle = 0,
        Effects = 1,
        Ambience = 2,
        Music = 3,
        UserInterface = 4,
        Dialogue = 5,
    }

    /// <summary>
    /// Project-owned Unity Audio mapping used only by the development fallback.
    /// The primary library points to project-owned content. Explicit private
    /// Phase 1 supplemental libraries may point into the ignored sanitized
    /// RuntimeBaseline boundary and remain replaceable by stable event ID.
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
        [SerializeField] private UnityAudioDistanceRolloff distanceRolloff;
        // Zero preserves old libraries. Content calibration is independent of
        // request/RTPC gain and the user's category/master settings.
        [SerializeField, Range(-12f, 12f)] private float calibrationGainDb;
        // Event balance is separate from replacement-clip normalization. The
        // replacement importer inherits this from its existing event template.
        // Zero is an exact bypass for every previously authored event.
        [SerializeField, Range(-12f, 18f)] private float mixGainDb;
        // Optional ceiling for ADDED positive mix gain, not the original sound.
        // User/request scaling moves the ceiling too, preserving their control.
        [SerializeField, Range(0f, 1f)] private float mixBoostCeiling;
        // Optional explicit content headroom, including the calibrated base.
        // Zero preserves all existing events; this is not a global DSP limiter.
        [SerializeField, Range(0f, 1f)] private float outputGainCeiling;
        public const float MaximumCombinedGainDb = 18f; // Uncapped sum: < the existing 8x overflow ceiling.
        // A positive added-boost ceiling bounds the final gain by the greater
        // of the original calibrated level (at most 4x) and user-scaled 1x.
        // It therefore permits stronger quiet-end drive without widening DSP.
        private static bool ExceedsUncappedGain(float calibrationDb, float mixDb, float ceiling) =>
            calibrationDb + mixDb > MaximumCombinedGainDb && !(mixDb > 0f && ceiling > 0f);
        // Optional scoped RTPCs. Empty IDs preserve every pre-existing event.
        [SerializeField] private string volumeParameterId = string.Empty;
        [SerializeField] private string pitchParameterId = string.Empty;

        public string EventId => eventId;
        public AudioClip Clip => clip;
        public UnityAudioCategory Category => category;
        public bool Loop => loop;
        public float Volume => Mathf.Clamp01(volume);
        public float CalibrationGainDb => calibrationGainDb;
        public float CalibrationLinearGain => Mathf.Pow(10f, calibrationGainDb / 20f);
        public float MixGainDb => mixGainDb;
        public float MixLinearGain => mixGainDb == 0f ? 1f : Mathf.Pow(10f, mixGainDb / 20f);
        public float MixBoostCeiling => mixBoostCeiling;
        public float OutputGainCeiling => outputGainCeiling;
        public float ApplyMixGain(float calibratedGain, float requestAndUserGain)
        {
            float mixed = calibratedGain * MixLinearGain;
            mixed = mixGainDb > 0f && mixBoostCeiling > 0f
                ? Mathf.Min(mixed, Mathf.Max(calibratedGain, mixBoostCeiling * Mathf.Clamp01(requestAndUserGain)))
                : mixed;
            return outputGainCeiling > 0f ? Mathf.Min(mixed, outputGainCeiling * Mathf.Clamp01(requestAndUserGain)) : mixed;
        }
        public float Pitch => Mathf.Clamp(pitch, 0.1f, 3f);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public float MinimumDistanceMeters => Mathf.Max(0.01f, minimumDistanceMeters);
        public float MaximumDistanceMeters => Mathf.Max(MinimumDistanceMeters, maximumDistanceMeters);
        public AudioRolloffMode RolloffMode => distanceRolloff == UnityAudioDistanceRolloff.Logarithmic
            ? AudioRolloffMode.Logarithmic : AudioRolloffMode.Linear;
        public AudioParameterId VolumeParameterId => string.IsNullOrEmpty(volumeParameterId)
            ? default : new AudioParameterId(volumeParameterId);
        public AudioParameterId PitchParameterId => string.IsNullOrEmpty(pitchParameterId)
            ? default : new AudioParameterId(pitchParameterId);

#if UNITY_EDITOR
        public void ConfigureOutputGainCeilingForAuthoring(float ceiling)
        {
            if (!float.IsFinite(ceiling) || ceiling < 0f || ceiling > 1f) throw new ArgumentOutOfRangeException(nameof(ceiling));
            outputGainCeiling = ceiling;
        }

        public void ConfigureCalibrationForAuthoring(float gainDb)
        {
            if (!float.IsFinite(gainDb) || gainDb < -12f || gainDb > 12f ||
                ExceedsUncappedGain(gainDb, mixGainDb, mixBoostCeiling))
                throw new ArgumentOutOfRangeException(nameof(gainDb));
            calibrationGainDb = gainDb;
        }

        public void ConfigureMixGainForAuthoring(float gainDb, float boostCeiling = 0f)
        {
            if (!float.IsFinite(gainDb) || gainDb < -12f || gainDb > 18f ||
                ExceedsUncappedGain(calibrationGainDb, gainDb, boostCeiling) ||
                !float.IsFinite(boostCeiling) || boostCeiling < 0f || boostCeiling > 1f)
                throw new ArgumentOutOfRangeException(nameof(gainDb));
            mixGainDb = gainDb;
            mixBoostCeiling = boostCeiling;
        }

        public void ConfigureDistanceRolloffForAuthoring(UnityAudioDistanceRolloff value)
        {
            distanceRolloff = value;
        }

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

        public void ConfigureParameterBindingsForAuthoring(
            AudioParameterId volumeParameter, AudioParameterId pitchParameter)
        {
            volumeParameterId = volumeParameter.Value ?? string.Empty;
            pitchParameterId = pitchParameter.Value ?? string.Empty;
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

            if (!Enum.IsDefined(typeof(UnityAudioDistanceRolloff), distanceRolloff))
            {
                failures.Add($"Entry {index} ({eventId}) has an invalid distance rolloff.");
                valid = false;
            }

            if (!float.IsFinite(calibrationGainDb) || calibrationGainDb < -12f || calibrationGainDb > 12f)
            {
                failures.Add($"Entry {index} ({eventId}) has an invalid content calibration gain.");
                valid = false;
            }

            if (!float.IsFinite(mixGainDb) || mixGainDb < -12f || mixGainDb > 18f ||
                ExceedsUncappedGain(calibrationGainDb, mixGainDb, mixBoostCeiling) ||
                !float.IsFinite(mixBoostCeiling) || mixBoostCeiling < 0f || mixBoostCeiling > 1f ||
                !float.IsFinite(outputGainCeiling) || outputGainCeiling < 0f || outputGainCeiling > 1f)
            {
                failures.Add($"Entry {index} ({eventId}) has an invalid event mix gain or exceeds the combined gain ceiling.");
                valid = false;
            }

            foreach (string parameter in new[] { volumeParameterId, pitchParameterId })
            {
                if (!string.IsNullOrEmpty(parameter) && !AudioIdValidation.TryValidate(parameter, out _))
                {
                    failures.Add($"Entry {index} ({eventId}) has an invalid parameter binding.");
                    valid = false;
                }
            }

            if (!loop && !string.IsNullOrEmpty(pitchParameterId))
            {
                failures.Add($"Entry {index} ({eventId}) may bind changing pitch only for a loop; one-shot expiry uses fixed pitch.");
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
        public IReadOnlyList<UnityAudioEventDefinition> Definitions =>
            events ?? Array.Empty<UnityAudioEventDefinition>();

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
