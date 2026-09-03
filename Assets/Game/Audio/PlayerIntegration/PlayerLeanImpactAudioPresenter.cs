using MSC.Player;
using UnityEngine;

namespace MSC.Audio.PlayerIntegration
{
    /// <summary>
    /// Routes the project-owned lean impact notification through the active
    /// audio backend. BetterMSC remains behavioral evidence only.
    /// </summary>
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class PlayerLeanImpactAudioPresenter : MonoBehaviour
    {
        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private MonoBehaviour emitterComponent;

        [Header("Impact mapping")]
        [SerializeField, Min(0f)] private float minimumSpeedMetersPerSecond = 3f;
        [SerializeField, Min(0f)] private float fullIntensitySpeedMetersPerSecond = 6f;

        private IAudioBackend backend;
        private IAudioEmitter emitter;
        private bool subscribed;

        public bool IsBound =>
            subscribed && backend != null && emitter != null;
        public int PostedImpactCount { get; private set; }
        public Vector3 LastImpactPoint { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;

        public bool Configure(
            GameObject playerRoot,
            MonoBehaviour authoredBackend,
            MonoBehaviour authoredEmitter)
        {
            motor = playerRoot == null
                ? null
                : playerRoot.GetComponentInChildren<FirstPersonMotor>(true);
            backendComponent = authoredBackend;
            emitterComponent = authoredEmitter;
            return !isActiveAndEnabled || TryBind(out _);
        }

        public bool TryValidate(out string failure)
        {
            if (motor == null)
            {
                failure = "Lean impact audio requires FirstPersonMotor.";
                return false;
            }

            if (!(backendComponent is IAudioBackend candidateBackend) ||
                candidateBackend == null)
            {
                failure = "Lean impact audio backend does not implement IAudioBackend.";
                return false;
            }

            if (!(emitterComponent is IAudioEmitter candidateEmitter) ||
                candidateEmitter == null ||
                string.IsNullOrWhiteSpace(candidateEmitter.StableId) ||
                candidateEmitter.AudioTransform == null)
            {
                failure = "Lean impact audio requires a configured IAudioEmitter.";
                return false;
            }

            if (!IsFiniteNonNegative(minimumSpeedMetersPerSecond) ||
                !IsFiniteNonNegative(fullIntensitySpeedMetersPerSecond) ||
                fullIntensitySpeedMetersPerSecond <=
                    minimumSpeedMetersPerSecond)
            {
                failure = "Lean impact audio speed range is invalid.";
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
            emitter = (IAudioEmitter)emitterComponent;
            motor.ForwardLeanImpactOccurred += HandleLeanImpact;
            subscribed = true;
            LastFailure = string.Empty;
            return true;
        }

        private void Unbind()
        {
            if (subscribed && motor != null)
            {
                motor.ForwardLeanImpactOccurred -= HandleLeanImpact;
            }

            subscribed = false;
            backend = null;
            emitter = null;
        }

        private void HandleLeanImpact(PlayerLeanImpact impact)
        {
            LastImpactPoint = impact.Point;
            if (!IsBound ||
                !IsAlive(backend) ||
                !IsAlive(emitter) ||
                !backend.IsReady ||
                !emitter.IsAudioEmitterActive)
            {
                return;
            }

            float intensity01 = Mathf.InverseLerp(
                minimumSpeedMetersPerSecond,
                fullIntensitySpeedMetersPerSecond,
                impact.ForwardSpeedMetersPerSecond);
            backend.SetParameter(
                AudioProjectIds.Parameters.InteractionImpactIntensity,
                intensity01,
                emitter);

            var request = new AudioEventRequest(
                AudioProjectIds.Events.InteractionImpact,
                emitter,
                impact.Point);
            IAudioEventHandle handle = backend.PostEvent(in request);
            if (handle == null || !handle.IsValid)
            {
                LastFailure = string.IsNullOrWhiteSpace(backend.FailureReason)
                    ? "Lean impact audio event was rejected."
                    : backend.FailureReason;
                return;
            }

            PostedImpactCount++;
            LastFailure = string.Empty;
        }

        private bool HasAuthoredReferences() =>
            motor != null || backendComponent != null || emitterComponent != null;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsAlive(object instance) =>
            instance != null &&
            (!(instance is Object unityObject) || unityObject != null);
    }
}
