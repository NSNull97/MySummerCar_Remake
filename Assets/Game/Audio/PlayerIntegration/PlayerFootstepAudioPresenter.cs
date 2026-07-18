using MSC.Player;
using UnityEngine;

namespace MSC.Audio.PlayerIntegration
{
    /// <summary>
    /// Presentation adapter from actual CharacterController displacement to a
    /// project-owned footstep event. The surface ray is issued only when the
    /// distance cadence reaches a step.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class PlayerFootstepAudioPresenter : MonoBehaviour
    {
        private const int SurfaceHitCapacity = 8;

        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private MonoBehaviour emitterComponent;

        [Header("Cadence")]
        [SerializeField, Min(0.1f)] private float walkingStepDistanceMeters = 1.65f;
        [SerializeField, Min(0.1f)] private float crouchingStepDistanceMeters = 1.2f;
        [SerializeField, Min(0.5f)] private float teleportThresholdMeters = 2.5f;

        [Header("Surface probe")]
        [SerializeField] private LayerMask surfaceProbeMask = ~0;
        [SerializeField, Min(0.05f)] private float surfaceProbeHeightMeters = 0.3f;
        [SerializeField, Min(0.05f)] private float surfaceProbeDistanceMeters = 0.8f;

        private readonly RaycastHit[] surfaceHits = new RaycastHit[SurfaceHitCapacity];
        private IAudioBackend backend;
        private IAudioEmitter emitter;
        private FootstepCadenceTracker cadence;

        public bool IsBound => backend != null && emitter != null && cadence != null;
        public int PostedStepCount { get; private set; }
        public AudioSurfaceKind LastSurfaceKind { get; private set; } = AudioSurfaceKind.Unknown;
        public string LastFailure { get; private set; } = string.Empty;

        public bool Configure(
            GameObject playerRoot,
            MonoBehaviour authoredBackend,
            MonoBehaviour authoredEmitter)
        {
            motor = playerRoot == null
                ? null
                : playerRoot.GetComponentInChildren<FirstPersonMotor>(true);
            characterController = motor == null
                ? null
                : motor.GetComponent<CharacterController>();
            backendComponent = authoredBackend;
            emitterComponent = authoredEmitter;
            return !isActiveAndEnabled || TryBind(out _);
        }

        public bool TryValidate(out string failure)
        {
            if (motor == null)
            {
                failure = "Footstep audio requires FirstPersonMotor.";
                return false;
            }

            if (characterController == null)
            {
                failure = "Footstep audio requires the motor CharacterController.";
                return false;
            }

            if (!(backendComponent is IAudioBackend candidateBackend))
            {
                failure = "Footstep audio backend does not implement IAudioBackend.";
                return false;
            }

            if (!(emitterComponent is IAudioEmitter candidateEmitter))
            {
                failure = "Footstep audio emitter does not implement IAudioEmitter.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidateEmitter.StableId) ||
                candidateEmitter.AudioTransform == null)
            {
                failure = "Footstep audio emitter requires a stable ID and transform.";
                return false;
            }

            if (!IsPositive(walkingStepDistanceMeters) ||
                !IsPositive(crouchingStepDistanceMeters) ||
                !IsPositive(teleportThresholdMeters) ||
                !IsPositive(surfaceProbeHeightMeters) ||
                !IsPositive(surfaceProbeDistanceMeters))
            {
                failure = "Footstep audio distances must be finite and positive.";
                return false;
            }

            if (candidateBackend == null)
            {
                failure = "Footstep audio backend is unavailable.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (HasAuthoredReferences() && !TryBind(out string failure))
            {
                Debug.LogError(failure, this);
            }
        }

        private void OnDisable()
        {
            backend = null;
            emitter = null;
            cadence = null;
        }

        private void LateUpdate()
        {
            if (!IsBound && !TryBind(out _))
            {
                return;
            }

            bool stepDue = cadence.Advance(
                characterController.transform.position,
                characterController.enabled && characterController.isGrounded,
                motor.IsCrouching);
            if (!stepDue)
            {
                return;
            }

            AudioSurfaceMetadata surface = ProbeSurface();
            LastSurfaceKind = surface.Kind;

            if (!backend.IsReady || !emitter.IsAudioEmitterActive)
            {
                return;
            }

            AudioSwitchId surfaceSwitch = AudioSurfaceMapping.ToFootstepSwitch(surface);
            if (!backend.SetSwitch(
                    AudioProjectIds.Switches.FootstepSurfaceGroup,
                    surfaceSwitch,
                    emitter))
            {
                LastFailure = string.IsNullOrWhiteSpace(backend.FailureReason)
                    ? "Footstep surface switch update failed."
                    : backend.FailureReason;
            }

            var request = new AudioEventRequest(
                AudioProjectIds.Events.PlayerFootstep,
                emitter,
                characterController.transform.position,
                motor.IsCrouching ? 0.75f : 1f);
            IAudioEventHandle handle = backend.PostEvent(in request);
            if (handle == null || !handle.IsValid)
            {
                LastFailure = string.IsNullOrWhiteSpace(backend.FailureReason)
                    ? "Footstep event was rejected by the active backend."
                    : backend.FailureReason;
                return;
            }

            PostedStepCount++;
            LastFailure = string.Empty;
        }

        private bool TryBind(out string failure)
        {
            backend = null;
            emitter = null;
            cadence = null;

            if (!TryValidate(out failure))
            {
                LastFailure = failure;
                return false;
            }

            backend = (IAudioBackend)backendComponent;
            emitter = (IAudioEmitter)emitterComponent;
            cadence = new FootstepCadenceTracker(
                walkingStepDistanceMeters,
                crouchingStepDistanceMeters,
                teleportThresholdMeters);
            cadence.Reset(characterController.transform.position);
            LastFailure = string.Empty;
            return true;
        }

        private AudioSurfaceMetadata ProbeSurface()
        {
            Bounds bounds = characterController.bounds;
            Vector3 origin = new Vector3(
                bounds.center.x,
                bounds.min.y + surfaceProbeHeightMeters,
                bounds.center.z);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                surfaceHits,
                surfaceProbeHeightMeters + surfaceProbeDistanceMeters,
                surfaceProbeMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            IAudioSurfaceMetadataProvider nearestProvider = null;
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = surfaceHits[index].collider;
                if (hitCollider == null || IsPlayerCollider(hitCollider))
                {
                    continue;
                }

                IAudioSurfaceMetadataProvider provider = FindSurfaceProvider(hitCollider.transform);
                if (provider != null && surfaceHits[index].distance < nearestDistance)
                {
                    nearestDistance = surfaceHits[index].distance;
                    nearestProvider = provider;
                }
            }

            return nearestProvider?.AudioSurface ?? AudioSurfaceMetadata.Unknown;
        }

        private bool IsPlayerCollider(Collider candidate) =>
            candidate == characterController ||
            candidate.transform == characterController.transform ||
            candidate.transform.IsChildOf(characterController.transform);

        private static IAudioSurfaceMetadataProvider FindSurfaceProvider(Transform start)
        {
            Transform current = start;
            while (current != null)
            {
                Component component = current.GetComponent(
                    typeof(IAudioSurfaceMetadataProvider));
                if (component is IAudioSurfaceMetadataProvider provider)
                {
                    return provider;
                }

                current = current.parent;
            }

            return null;
        }

        private bool HasAuthoredReferences() =>
            motor != null || characterController != null ||
            backendComponent != null || emitterComponent != null;

        private static bool IsPositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
