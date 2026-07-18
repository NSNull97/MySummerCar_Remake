using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Audio
{
    /// <summary>
    /// Project-owned spatial emitter identity and environment metadata. A
    /// streamed scene may be registered by AudioBackendRouter, while dynamically
    /// spawned emitters can use the explicit backend reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioEmitterAuthoring : MonoBehaviour, IAudioEmitter
    {
        [SerializeField] private StableEntityIdAuthoring stableEntityId;
        [SerializeField] private string transientAudioId = string.Empty;
        [SerializeField] private Transform audioTransform;
        [SerializeField] private MonoBehaviour explicitBackendComponent;

        [Header("Surface")]
        [SerializeField] private string surfaceSwitchId = "audio.switch.vehicle.surface.unknown";
        [SerializeField, Range(0f, 1f)] private float surfaceWetness01;
        [SerializeField, Range(0f, 1f)] private float surfaceRoughness01 = 0.5f;

        [Header("Environment")]
        [SerializeField] private AudioListenerSpace listenerSpace = AudioListenerSpace.Exterior;
        [SerializeField, Range(0f, 1f)] private float shelter01;
        [SerializeField, Range(0f, 1f)] private float obstruction01;
        [SerializeField, Range(0f, 1f)] private float reverbSend01;

        private IAudioBackend explicitBackend;
        private bool explicitlyRegistered;

        public string StableId
        {
            get
            {
                if (stableEntityId != null &&
                    stableEntityId.TryGetStableId(out StableEntityId entityId))
                {
                    return entityId.Value;
                }

                return transientAudioId?.Trim() ?? string.Empty;
            }
        }

        public Transform AudioTransform => audioTransform != null ? audioTransform : transform;

        public int OwningSceneHandle => gameObject.scene.handle;

        public bool IsAudioEmitterActive => isActiveAndEnabled && gameObject.activeInHierarchy;

        public AudioSurfaceContext SurfaceContext
        {
            get
            {
                string candidate = surfaceSwitchId?.Trim() ?? string.Empty;
                return AudioStableId.TryValidate(candidate, out _)
                    ? new AudioSurfaceContext(
                        new AudioSwitchId(candidate),
                        surfaceWetness01,
                        surfaceRoughness01)
                    : AudioSurfaceContext.Unknown;
            }
        }

        public AudioEnvironmentContext EnvironmentContext => new AudioEnvironmentContext(
            listenerSpace,
            shelter01,
            obstruction01,
            reverbSend01,
            0f,
            0f,
            0.5f);

        public bool TryValidate(out string failure)
        {
            if (!AudioStableId.TryValidate(StableId, out failure))
            {
                failure = "Emitter stable ID is invalid: " + failure;
                return false;
            }

            if (AudioTransform == null)
            {
                failure = "Emitter transform is missing.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(surfaceSwitchId) &&
                !AudioStableId.TryValidate(surfaceSwitchId.Trim(), out failure))
            {
                failure = "Surface switch ID is invalid: " + failure;
                return false;
            }

            if (explicitBackendComponent != null &&
                !(explicitBackendComponent is IAudioBackend))
            {
                failure = "Explicit backend component does not implement IAudioBackend.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public void Configure(
            string stableAudioId,
            MonoBehaviour backendComponent = null,
            Transform sourceTransform = null)
        {
            UnregisterExplicitBackend();
            stableEntityId = null;
            transientAudioId = stableAudioId?.Trim() ?? string.Empty;
            explicitBackendComponent = backendComponent;
            audioTransform = sourceTransform;
            if (isActiveAndEnabled)
            {
                RegisterExplicitBackend();
            }
        }

        public void SetSurface(in AudioSurfaceContext surface)
        {
            surfaceSwitchId = surface.SurfaceId.Value ?? string.Empty;
            surfaceWetness01 = surface.Wetness01;
            surfaceRoughness01 = surface.Roughness01;
        }

        private void Reset()
        {
            stableEntityId = GetComponent<StableEntityIdAuthoring>();
            audioTransform = transform;
        }

        private void OnEnable()
        {
            RegisterExplicitBackend();
        }

        private void Update()
        {
            if (!explicitlyRegistered)
            {
                RegisterExplicitBackend();
            }
        }

        private void OnDisable()
        {
            UnregisterExplicitBackend();
        }

        private void RegisterExplicitBackend()
        {
            explicitBackend = explicitBackendComponent as IAudioBackend;
            if (explicitBackend == null || explicitlyRegistered)
            {
                return;
            }

            // Backend initialization order is intentionally independent from
            // emitter authoring. Wwise may still be loading banks while the
            // Unity fallback is finishing Awake, so registration retries once
            // the router reports a ready backend.
            if (!explicitBackend.IsReady)
            {
                return;
            }

            if (!explicitBackend.RegisterEmitter(this, out string failure))
            {
                Debug.LogError(failure, this);
                return;
            }

            explicitlyRegistered = true;
        }

        private void UnregisterExplicitBackend()
        {
            if (explicitBackend != null && explicitlyRegistered)
            {
                explicitBackend.UnregisterEmitter(this);
            }

            explicitBackend = null;
            explicitlyRegistered = false;
        }
    }
}
