using System.Collections.Generic;
using UnityEngine;

namespace MSC.Audio
{
    /// <summary>
    /// Explicit player-listener bridge. Weather and zone systems provide typed
    /// values; the listener forwards one normalized context to the active backend.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class AudioListenerContextPresenter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private Transform listenerTransform;
        [SerializeField] private string stableListenerId = "audio.listener.player";

        private readonly List<AudioEnvironmentZone> activeZones =
            new List<AudioEnvironmentZone>();
        private IAudioBackend backend;
        private float precipitation01;
        private float wind01;
        private float normalizedDayTime01 = 0.5f;
        private AudioListenerSpace weatherListenerSpace = AudioListenerSpace.Exterior;
        private float weatherShelter01;
        private float weatherObstruction01;
        private float weatherReverbSend01;
        private bool hasContinuousWeatherContext;

        public AudioEnvironmentContext CurrentEnvironment { get; private set; } =
            AudioEnvironmentContext.Exterior;

        public void Configure(
            MonoBehaviour audioBackend,
            Transform authoredListenerTransform,
            string listenerId = "audio.listener.player")
        {
            backendComponent = audioBackend;
            listenerTransform = authoredListenerTransform;
            stableListenerId = listenerId?.Trim() ?? string.Empty;
            backend = backendComponent as IAudioBackend;
            enabled = ValidateConfiguration();
        }

        public void SetWeatherContext(
            float precipitationIntensity01,
            float windIntensity01,
            float dayTime01,
            AudioListenerSpace listenerSpace = AudioListenerSpace.Exterior)
        {
            precipitation01 = AudioMath.Clamp01(precipitationIntensity01);
            wind01 = AudioMath.Clamp01(windIntensity01);
            normalizedDayTime01 = AudioMath.Clamp01(dayTime01);
            weatherListenerSpace = System.Enum.IsDefined(
                typeof(AudioListenerSpace),
                listenerSpace)
                ? listenerSpace
                : AudioListenerSpace.Exterior;
            hasContinuousWeatherContext = false;
        }

        public void SetWeatherExposureContext(
            float precipitationIntensity01,
            float windIntensity01,
            float dayTime01,
            AudioListenerSpace listenerSpace,
            float shelter01,
            float obstruction01,
            float reverbSend01)
        {
            precipitation01 = AudioMath.Clamp01(precipitationIntensity01);
            wind01 = AudioMath.Clamp01(windIntensity01);
            normalizedDayTime01 = AudioMath.Clamp01(dayTime01);
            weatherListenerSpace = System.Enum.IsDefined(
                typeof(AudioListenerSpace),
                listenerSpace)
                ? listenerSpace
                : AudioListenerSpace.Exterior;
            weatherShelter01 = AudioMath.Clamp01(shelter01);
            weatherObstruction01 = AudioMath.Clamp01(obstruction01);
            weatherReverbSend01 = AudioMath.Clamp01(reverbSend01);
            hasContinuousWeatherContext = true;
        }

        private void Reset()
        {
            listenerTransform = transform;
        }

        private void Awake()
        {
            backend = backendComponent as IAudioBackend;
        }

        private void Start()
        {
            // AddComponent on an active player invokes Awake before the
            // composition root can provide its explicit backend. Validate at
            // Start so same-frame Configure is supported without a false error.
            enabled = ValidateConfiguration();
        }

        private bool ValidateConfiguration()
        {
            if (backend == null)
            {
                Debug.LogError(
                    "Audio listener requires an explicit component implementing IAudioBackend.",
                    this);
                return false;
            }

            if (!AudioStableId.TryValidate(stableListenerId?.Trim(), out string failure))
            {
                Debug.LogError("Invalid audio listener ID: " + failure, this);
                return false;
            }

            return true;
        }

        private void LateUpdate()
        {
            Transform source = listenerTransform != null ? listenerTransform : transform;
            CurrentEnvironment = ResolveEnvironment();
            var context = new AudioListenerContext(
                stableListenerId.Trim(),
                source.position,
                source.forward,
                source.up,
                CurrentEnvironment,
                Application.isFocused);
            backend.SetListenerContext(in context);
            backend.SetParameter(
                AudioProjectIds.Parameters.EnvironmentShelter,
                CurrentEnvironment.Shelter01);
            backend.SetParameter(
                AudioProjectIds.Parameters.EnvironmentTimeOfDay,
                CurrentEnvironment.NormalizedDayTime01);
            backend.SetState(
                AudioProjectIds.States.EnvironmentGroup,
                MapEnvironmentState(CurrentEnvironment.ListenerSpace));
            backend.SetState(
                AudioProjectIds.States.DayPhaseGroup,
                MapDayPhaseState(CurrentEnvironment.NormalizedDayTime01));
        }

        private void OnTriggerEnter(Collider other)
        {
            AudioEnvironmentZone zone = other.GetComponentInParent<AudioEnvironmentZone>();
            if (zone != null && !activeZones.Contains(zone))
            {
                activeZones.Add(zone);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            AudioEnvironmentZone zone = other.GetComponentInParent<AudioEnvironmentZone>();
            if (zone != null)
            {
                activeZones.Remove(zone);
            }
        }

        private AudioEnvironmentContext ResolveEnvironment()
        {
            AudioEnvironmentZone selected = null;
            for (int index = activeZones.Count - 1; index >= 0; index--)
            {
                AudioEnvironmentZone candidate = activeZones[index];
                if (candidate == null)
                {
                    activeZones.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsUsable)
                {
                    continue;
                }

                if (selected == null || candidate.Priority > selected.Priority)
                {
                    selected = candidate;
                }
            }

            return selected == null
                ? hasContinuousWeatherContext
                    ? CreateContinuousWeatherContext(
                        weatherListenerSpace,
                        weatherShelter01,
                        weatherObstruction01,
                        weatherReverbSend01,
                        precipitation01,
                        wind01,
                        normalizedDayTime01)
                    : CreateWeatherFallbackContext(
                        weatherListenerSpace,
                        precipitation01,
                        wind01,
                        normalizedDayTime01)
                : selected.CreateContext(
                    precipitation01,
                    wind01,
                    normalizedDayTime01);
        }

        public static AudioEnvironmentContext CreateContinuousWeatherContext(
            AudioListenerSpace listenerSpace,
            float shelter01,
            float obstruction01,
            float reverbSend01,
            float precipitationIntensity01,
            float windIntensity01,
            float dayTime01) => new AudioEnvironmentContext(
                listenerSpace,
                shelter01,
                obstruction01,
                reverbSend01,
                precipitationIntensity01,
                windIntensity01,
                dayTime01);

        /// <summary>
        /// Converts the production weather shelter result into the audio
        /// listener fallback. Explicit authored audio zones still win while
        /// active; this path keeps the already validated house and garage
        /// shelter volumes authoritative without duplicating trigger geometry.
        /// </summary>
        public static AudioEnvironmentContext CreateWeatherFallbackContext(
            AudioListenerSpace listenerSpace,
            float precipitationIntensity01,
            float windIntensity01,
            float dayTime01)
        {
            switch (listenerSpace)
            {
                case AudioListenerSpace.Sheltered:
                    return new AudioEnvironmentContext(
                        listenerSpace,
                        0.65f,
                        0.2f,
                        0.15f,
                        precipitationIntensity01,
                        windIntensity01,
                        dayTime01);
                case AudioListenerSpace.Interior:
                    return new AudioEnvironmentContext(
                        listenerSpace,
                        1f,
                        0.6f,
                        0.35f,
                        precipitationIntensity01,
                        windIntensity01,
                        dayTime01);
                case AudioListenerSpace.VehicleInterior:
                    return new AudioEnvironmentContext(
                        listenerSpace,
                        0.9f,
                        0.5f,
                        0.25f,
                        precipitationIntensity01,
                        windIntensity01,
                        dayTime01);
                default:
                    return new AudioEnvironmentContext(
                        AudioListenerSpace.Exterior,
                        0f,
                        0f,
                        0f,
                        precipitationIntensity01,
                        windIntensity01,
                        dayTime01);
            }
        }

        private static AudioStateId MapEnvironmentState(AudioListenerSpace space)
        {
            switch (space)
            {
                case AudioListenerSpace.Sheltered:
                    return AudioProjectIds.States.EnvironmentSheltered;
                case AudioListenerSpace.Interior:
                    return AudioProjectIds.States.EnvironmentInterior;
                case AudioListenerSpace.VehicleInterior:
                    return AudioProjectIds.States.EnvironmentVehicleInterior;
                default:
                    return AudioProjectIds.States.EnvironmentExterior;
            }
        }

        public static AudioStateId MapDayPhaseState(float normalizedDayTime01)
        {
            float time = float.IsNaN(normalizedDayTime01) ||
                         float.IsInfinity(normalizedDayTime01)
                ? 0f
                : Mathf.Repeat(normalizedDayTime01, 1f);
            if (time >= 0.18f && time < 0.25f)
            {
                return AudioProjectIds.States.DayPhaseDawn;
            }

            if (time >= 0.25f && time < 0.75f)
            {
                return AudioProjectIds.States.DayPhaseDay;
            }

            if (time >= 0.75f && time < 0.86f)
            {
                return AudioProjectIds.States.DayPhaseEvening;
            }

            return AudioProjectIds.States.DayPhaseNight;
        }
    }
}
