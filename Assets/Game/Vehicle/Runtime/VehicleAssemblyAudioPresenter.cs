using MSC.Audio;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Presentation adapter for successful assembly mutations and physical
    /// Satsuma body impacts. It never mutates the assembly graph.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleAssemblyAudioPresenter : MonoBehaviour
    {
        public const string FallbackOverrideResourcesPath =
            "Phase1SatsumaAssemblyAudio/Phase1SatsumaAssemblyAudioEventLibrary";

        private const string EmitterId =
            "audio.emitter.vehicle.satsuma.assembly";

        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring emitter;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.75f;
        [SerializeField, Min(0f)] private float highImpactSpeed = 5f;
        [SerializeField, Min(0.01f)] private float impactFullScaleSpeed = 5f;
        [SerializeField, Min(0f)] private float impactCooldownSeconds = 0.08f;

        private IAudioBackend backend;
        private bool subscribed;
        private float nextImpactTime;

        public bool IsBound => subscribed && backend != null && emitter != null;
        public string LastFailure { get; private set; } = string.Empty;

        public bool Configure(
            VehicleAssemblyController assemblyController,
            MonoBehaviour audioBackend)
        {
            Unbind();
            controller = assemblyController;
            backendComponent = audioBackend;
            emitter = GetComponent<AudioEmitterAuthoring>() ??
                gameObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure(EmitterId, audioBackend, transform);
            return !isActiveAndEnabled || TryBind(out _);
        }

        public bool TryValidate(out string failure)
        {
            if (controller == null)
            {
                failure = "Satsuma assembly audio requires an assembly controller.";
                return false;
            }

            if (!(backendComponent is IAudioBackend))
            {
                failure = "Satsuma assembly audio requires an IAudioBackend component.";
                return false;
            }

            if (emitter == null)
            {
                failure = "Satsuma assembly audio emitter is missing.";
                return false;
            }

            if (!emitter.TryValidate(out failure))
            {
                failure = "Satsuma assembly audio emitter is invalid: " + failure;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if ((controller != null || backendComponent != null) &&
                !TryBind(out string failure))
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

            backend = (IAudioBackend)backendComponent;
            controller.ActionCompleted += HandleAssemblyAction;
            subscribed = true;
            LastFailure = string.Empty;
            return true;
        }

        private void Unbind()
        {
            if (subscribed && controller != null)
            {
                controller.ActionCompleted -= HandleAssemblyAction;
            }

            subscribed = false;
            backend = null;
        }

        private void HandleAssemblyAction(AssemblyActionCompleted notification)
        {
            AudioEventId eventId;
            switch (notification.Action)
            {
                case AssemblyActionKind.PartInstalled:
                    eventId = AudioProjectIds.Events.InteractionPartInstall;
                    break;
                case AssemblyActionKind.PartRemoved:
                    eventId = AudioProjectIds.Events.InteractionPartRemove;
                    break;
                case AssemblyActionKind.FastenerTightened:
                    eventId = AudioProjectIds.Events.InteractionFastenerTighten;
                    break;
                case AssemblyActionKind.FastenerLoosened:
                    eventId = AudioProjectIds.Events.InteractionFastenerLoosen;
                    break;
                default:
                    return;
            }

            Post(eventId, notification.WorldPosition, 1f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsBound || collision == null || Time.time < nextImpactTime)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (!float.IsFinite(speed) || speed < minimumImpactSpeed)
            {
                return;
            }

            bool high = speed >= highImpactSpeed;
            AudioEventId eventId = high
                ? Random.value < 0.5f
                    ? AudioProjectIds.Events.VehicleBodyImpactHigh01
                    : AudioProjectIds.Events.VehicleBodyImpactHigh02
                : Random.value < 0.5f
                    ? AudioProjectIds.Events.VehicleBodyImpactLow01
                    : AudioProjectIds.Events.VehicleBodyImpactLow02;
            float intensity01 = Mathf.Clamp01(speed / impactFullScaleSpeed);
            Vector3 position = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            nextImpactTime = Time.time + impactCooldownSeconds;
            Post(eventId, position, Mathf.Max(0.1f, intensity01));
        }

        private void Post(
            AudioEventId eventId,
            Vector3 worldPosition,
            float intensity01)
        {
            if (!IsBound || !backend.IsReady ||
                !emitter.IsAudioEmitterActive)
            {
                return;
            }

            backend.SetParameter(
                AudioProjectIds.Parameters.InteractionImpactIntensity,
                Mathf.Clamp01(intensity01),
                emitter);
            var request = new AudioEventRequest(
                eventId,
                emitter,
                worldPosition,
                Mathf.Clamp01(intensity01));
            IAudioEventHandle handle = backend.PostEvent(in request);
            LastFailure = handle == null || !handle.IsValid
                ? backend.FailureReason
                : string.Empty;
        }
    }
}
