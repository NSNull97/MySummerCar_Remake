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

        // Locked Satsuma EventSounds/Crashes TimeBased retrigger interval.
        public const float MinimumBodyImpactIntervalSeconds = 1.5f;

        private const string EmitterId =
            "audio.emitter.vehicle.satsuma.assembly";

        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private AudioEmitterAuthoring emitter;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.75f;
        [SerializeField, Min(0f)] private float highImpactSpeed = 5f;
        [SerializeField, Min(0.01f)] private float impactFullScaleSpeed = 5f;
        [SerializeField, Min(MinimumBodyImpactIntervalSeconds)]
        private float impactCooldownSeconds = MinimumBodyImpactIntervalSeconds;

        private IAudioBackend backend;
        private SatsumaHandbrakeController handbrake;
        private bool subscribed;
        private float nextImpactTime;
        private int bodyImpactWorldLayers;

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
            // Project categories, not copied donor layer numbers. The global
            // baseline uses Default as well as explicit world surface/solid.
            bodyImpactWorldLayers = LayerMask.GetMask("Default", "WorldSurface", "WorldSolid");
            controller.ActionCompleted += HandleAssemblyAction;
            handbrake = GetComponent<SatsumaHandbrakeController>();
            if (handbrake != null)
            {
                handbrake.HoldStarted += HandleHandbrakeHoldStarted;
            }

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

            if (handbrake != null)
            {
                handbrake.HoldStarted -= HandleHandbrakeHoldStarted;
            }

            subscribed = false;
            backend = null;
            handbrake = null;
        }

        private void HandleHandbrakeHoldStarted(bool raising)
        {
            // Donor Use emits once when entering INCREASE or DECREASE, not
            // once per frame or per lever angle. Restoring a save is silent.
            Post(
                raising
                    ? AudioProjectIds.Events.VehicleHandbrakeRaise
                    : AudioProjectIds.Events.VehicleHandbrakeLower,
                handbrake.LeverPivot != null
                    ? handbrake.LeverPivot.position
                    : handbrake.transform.position,
                1f,
                worldPositionOnly: true);
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
            if (!IsBound || collision == null || collision.contactCount == 0 || collision.impulse.sqrMagnitude <= 0f)
            {
                return;
            }

            // Travelling along a road at 110 km/h is not a 110 km/h body
            // impact. Measure the normal component, not tangential road speed.
            float speed = 0f;
            for (int index = 0; index < collision.contactCount; index++)
                speed = Mathf.Max(speed, NormalImpactSpeed(collision.relativeVelocity, collision.GetContact(index).normal));
            if (!TryAcceptBodyImpact(collision.collider, speed, Time.time))
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
            Post(eventId, position, Mathf.Max(0.1f, intensity01));
        }

        private bool TryAcceptBodyImpact(Collider other, float speed, float now)
        {
            if (!float.IsFinite(now) || !float.IsFinite(speed) ||
                now < nextImpactTime || speed < minimumImpactSpeed ||
                !IsBodyImpactTarget(other)) return false;

            // Retain old serialized fields/prefabs; an older .08 value cannot
            // bypass the corrected donor-evidenced lower bound.
            nextImpactTime = now + Mathf.Max(MinimumBodyImpactIntervalSeconds, impactCooldownSeconds);
            return true;
        }

        private static float NormalImpactSpeed(Vector3 relativeVelocity, Vector3 contactNormal) =>
            Mathf.Abs(Vector3.Dot(relativeVelocity, contactNormal));

        private bool IsBodyImpactTarget(Collider other)
        {
            if (other == null || other.isTrigger ||
                (bodyImpactWorldLayers & (1 << other.gameObject.layer)) == 0)
                return false;

            // Nested kinematic part actors are still our own assembly, not a
            // road impact. Self/player-proxy pairs are not world-body impacts.
            return other.GetComponentInParent<VehicleAssemblyController>() != controller;
        }

        private void Post(
            AudioEventId eventId,
            Vector3 worldPosition,
            float intensity01,
            bool worldPositionOnly = false)
        {
            // A disabled presenter must remain silent even if an event was
            // already dispatched before its lifecycle unsubscription completed.
            if (!isActiveAndEnabled || !IsBound || !backend.IsReady ||
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
                worldPositionOnly ? null : emitter,
                worldPosition,
                Mathf.Clamp01(intensity01));
            IAudioEventHandle handle = backend.PostEvent(in request);
            LastFailure = handle == null || !handle.IsValid
                ? backend.FailureReason
                : string.Empty;
        }
    }
}
