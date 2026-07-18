using System;
using MSC.Interaction.Notifications;
using UnityEngine;

namespace MSC.Audio.InteractionIntegration
{
    /// <summary>
    /// Presentation-only adapter from completed interaction actions to stable
    /// project audio IDs. Interaction runtime remains unaware of audio and of
    /// the selected backend vendor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionAudioBridge : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour actionSourceComponent;

        [SerializeField]
        private MonoBehaviour backendComponent;

        [SerializeField]
        private MonoBehaviour emitterComponent;

        [Header("Tunable impact hints")]
        [SerializeField, Range(0f, 1f)]
        private float pickupIntensity01 = 0.2f;

        [SerializeField, Range(0f, 1f)]
        private float dropIntensity01 = 0.55f;

        [SerializeField, Range(0f, 1f)]
        private float placeIntensity01 = 0.2f;

        [SerializeField, Range(0f, 1f)]
        private float throwIntensity01 = 1f;

        [SerializeField, Range(0f, 1f)]
        private float mountHandoffIntensity01 = 0.35f;

        private IInteractionActionSource actionSource;
        private IAudioBackend backend;
        private IAudioEmitter emitter;
        private bool subscribed;

        public bool IsBound => subscribed && actionSource != null && backend != null && emitter != null;

        public string LastFailure { get; private set; } = string.Empty;

        public bool Configure(
            MonoBehaviour authoredActionSource,
            MonoBehaviour authoredBackend,
            MonoBehaviour authoredEmitter)
        {
            Unbind();
            actionSourceComponent = authoredActionSource;
            backendComponent = authoredBackend;
            emitterComponent = authoredEmitter;
            return !isActiveAndEnabled || TryBind(out _);
        }

        public bool TryValidate(out string failure)
        {
            if (!(actionSourceComponent is IInteractionActionSource))
            {
                failure =
                    "Interaction audio requires an explicit component implementing " +
                    "IInteractionActionSource.";
                return false;
            }

            if (!(backendComponent is IAudioBackend))
            {
                failure =
                    "Interaction audio requires an explicit component implementing IAudioBackend.";
                return false;
            }

            if (!(emitterComponent is IAudioEmitter candidateEmitter))
            {
                failure =
                    "Interaction audio requires an explicit component implementing IAudioEmitter.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidateEmitter.StableId))
            {
                failure = "Interaction audio emitter requires a stable ID.";
                return false;
            }

            if (candidateEmitter.AudioTransform == null)
            {
                failure = "Interaction audio emitter requires an audio transform.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static AudioEventId MapEvent(InteractionActionKind action)
        {
            switch (action)
            {
                case InteractionActionKind.Pickup:
                    return AudioProjectIds.Events.InteractionPickup;
                case InteractionActionKind.Drop:
                    return AudioProjectIds.Events.InteractionDrop;
                case InteractionActionKind.Place:
                    return AudioProjectIds.Events.InteractionPlace;
                case InteractionActionKind.Throw:
                    return AudioProjectIds.Events.InteractionThrow;
                case InteractionActionKind.MountHandoff:
                    return AudioProjectIds.Events.InteractionMountHandoff;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private void OnEnable()
        {
            if (!TryBind(out string failure) &&
                (actionSourceComponent != null || backendComponent != null || emitterComponent != null))
            {
                Debug.LogError(failure, this);
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private bool TryBind(out string failure)
        {
            Unbind();
            if (!TryValidate(out failure))
            {
                LastFailure = failure;
                return false;
            }

            actionSource = (IInteractionActionSource)actionSourceComponent;
            backend = (IAudioBackend)backendComponent;
            emitter = (IAudioEmitter)emitterComponent;
            actionSource.ActionCompleted += HandleActionCompleted;
            subscribed = true;
            LastFailure = string.Empty;
            return true;
        }

        private void Unbind()
        {
            if (subscribed && actionSource != null)
            {
                actionSource.ActionCompleted -= HandleActionCompleted;
            }

            subscribed = false;
            actionSource = null;
            backend = null;
            emitter = null;
        }

        private void HandleActionCompleted(InteractionActionCompleted notification)
        {
            if (!IsBound || !IsAlive(backend) || !IsAlive(emitter) ||
                !backend.IsReady || !emitter.IsAudioEmitterActive)
            {
                return;
            }

            AudioEventId eventId = MapEvent(notification.Action);
            float intensity01 = ResolveIntensity(notification.Action);
            backend.SetParameter(
                AudioProjectIds.Parameters.InteractionImpactIntensity,
                intensity01,
                emitter);

            var request = new AudioEventRequest(
                eventId,
                emitter,
                notification.WorldPosition);
            IAudioEventHandle handle = backend.PostEvent(in request);
            if (handle == null || !handle.IsValid)
            {
                LastFailure = backend.FailureReason;
            }
            else
            {
                LastFailure = string.Empty;
            }
        }

        private float ResolveIntensity(InteractionActionKind action)
        {
            switch (action)
            {
                case InteractionActionKind.Pickup:
                    return pickupIntensity01;
                case InteractionActionKind.Drop:
                    return dropIntensity01;
                case InteractionActionKind.Place:
                    return placeIntensity01;
                case InteractionActionKind.Throw:
                    return throwIntensity01;
                case InteractionActionKind.MountHandoff:
                    return mountHandoffIntensity01;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private static bool IsAlive(object instance) =>
            instance != null &&
            (!(instance is UnityEngine.Object unityObject) || unityObject != null);
    }
}
