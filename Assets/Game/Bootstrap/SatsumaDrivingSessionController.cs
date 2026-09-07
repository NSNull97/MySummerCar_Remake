using System;
using MSC.Player;
using MSC.Vehicle;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Bounded composition bridge: player stays in the gameplay hierarchy;
    /// the explicit interior station owns pose and pedal permission while seated.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed partial class SatsumaDrivingSessionController : MonoBehaviour,
        IPlayerContextActionSource, IPlayerDrivingPersistence
    {
        private PlayerInputRouter playerInput;
        private PlayerInteractionController interaction;
        private FirstPersonMotor motor;
        private FirstPersonLook look;
        private CharacterController capsule;
        private Camera playerCamera;
        private SatsumaDriverStation station;
        private VehicleInputRouter vehicleInput;
        private Action<string> showStatus;
        private Vector3 releaseLocalPosition;
        private PlayerPosture releasePosture;
        private Vector3 lastReleaseWorldPosition;
        private float localYawDegrees;
        private bool motorWasEnabled;
        private bool capsuleWasEnabled;
        private bool initialized;
        private bool subscribed;
        private Development.SatsumaDrivingDebugView debugView;

        public bool IsDriving { get; private set; }
        public SatsumaDriverStation Station => station;
        public float LocalYawDegrees => localYawDegrees;
        public string LastFailure { get; private set; } = string.Empty;
        public bool CanEnter => initialized && isActiveAndEnabled && !IsDriving &&
            playerInput != null && playerInput.IsGameplayInputEnabled && Time.timeScale > 0f &&
            motor != null && motor.enabled && station != null && station.IsAvailable &&
            station.ContainsPlayer(capsule) && interaction != null &&
            !interaction.HasHeldObject && !interaction.IsFirstPersonToolModeActive;

        public InteractionActionHint ContextActionHint => !initialized || !isActiveAndEnabled
            ? default
            : IsDriving
                ? new InteractionActionHint(InteractionActionBinding.DrivingMode,
                    station != null && station.CanReleaseWhileStationary ? "ВЫЙТИ ИЗ РЕЖИМА ВОДИТЕЛЯ" : "СНАЧАЛА ОСТАНОВИТЕСЬ")
                : CanEnter ? new InteractionActionHint(InteractionActionBinding.DrivingMode, "РЕЖИМ ВОДИТЕЛЯ") : default;

        public void Initialize(PlayerInputRouter input, FirstPersonMotor movement, FirstPersonLook playerLook,
            PlayerInteractionController interactions, Camera camera, SatsumaDriverStation driverStation,
            VehicleInputRouter drivingInput, Action<string> status = null)
        {
            if (initialized) throw new InvalidOperationException("A driving session is already bound.");
            if (input == null || movement == null || playerLook == null || interactions == null ||
                camera == null || driverStation == null || drivingInput == null ||
                movement.gameObject != gameObject || input.gameObject != gameObject ||
                !camera.transform.IsChildOf(transform))
                throw new ArgumentException("Explicit player, camera and canonical driver-station references are required.");
            playerInput = input; motor = movement; look = playerLook; interaction = interactions;
            playerCamera = camera; station = driverStation; vehicleInput = drivingInput; showStatus = status;
            capsule = motor.GetComponent<CharacterController>();
            if (capsule == null) throw new ArgumentException("The player traversal capsule is missing.");
            vehicleInput.ConfigureDriverSessionManagement();
            interaction.BindContextActionSource(this);
            initialized = true;
            if (Application.isEditor || Debug.isDebugBuild)
            {
                debugView = gameObject.AddComponent<Development.SatsumaDrivingDebugView>();
                debugView.Initialize(this, input, camera, driverStation.VehicleBody);
            }
            Subscribe();
        }

        public bool TryEnterDriving(out string failure)
        {
            if (!CanEnter)
            {
                failure = interaction != null && (interaction.HasHeldObject || interaction.IsFirstPersonToolModeActive)
                    ? "Освободите руки перед переходом в режим водителя."
                    : "Нужно находиться внутри триггера установленного водительского сиденья.";
                LastFailure = failure; return false;
            }
            releaseLocalPosition = station.transform.InverseTransformPoint(transform.position);
            releasePosture = motor.Posture;
            localYawDegrees = 0f;
            if (!look.TryRestoreSaveState(FirstPersonLookSaveDto.Create(0f), out failure)) return false;
            EnterPose();
            LastFailure = failure = string.Empty;
            return true;
        }

        public bool TryExitDriving(out string failure)
        {
            if (!initialized || !IsDriving || Time.timeScale <= 0f || !playerInput.IsGameplayInputEnabled)
            { failure = "Режим водителя сейчас недоступен."; return false; }
            if (station != null && !station.CanReleaseWhileStationary)
            { LastFailure = failure = "Сначала полностью остановите машину."; return false; }
            ReleasePose(returnToCabin: true);
            LastFailure = failure = string.Empty;
            return true;
        }

        private void EnterPose()
        {
            motorWasEnabled = motor.enabled;
            capsuleWasEnabled = capsule.enabled;
            interaction.EndPrimaryInteraction();
            interaction.EndSecondaryInteraction();
            interaction.EndToolActivation();
            playerInput.SetLocomotionInputEnabled(false);
            motor.ResetInputIntent();
            // The pose snapshot retains a normal crouch definition; the disabled
            // capsule never exerts forces or collides with its own moving car.
            if (!motor.TryRestoreSaveState(FirstPersonMotorSaveDto.Create(PlayerPosture.Crouch, 0f), out string failure))
                throw new InvalidOperationException(failure);
            motor.enabled = false;
            capsule.enabled = false;
            IsDriving = true;
            RefreshSeatedPose();
            RefreshInputOwnership();
        }

        private void HandleDrivingModeRequested()
        {
            if (!initialized) return;
            bool succeeded = IsDriving ? TryExitDriving(out string failure) : TryEnterDriving(out failure);
            if (!succeeded && (IsDriving || station != null && station.ContainsPlayer(capsule)))
                showStatus?.Invoke(failure);
        }

        private void HandleLookApplied(Vector2 degrees)
        {
            if (IsDriving) localYawDegrees = Mathf.DeltaAngle(0f, localYawDegrees + degrees.x);
        }

        private void Update()
        {
            if (!initialized) return;
            if (IsDriving && (station == null || !station.IsAvailable))
            {
                ReleasePose(returnToCabin: true);
                LastFailure = "Водительское сиденье недоступно — фиксация снята.";
                showStatus?.Invoke(LastFailure);
            }
            RefreshInputOwnership();
        }

        private void RefreshInputOwnership()
        {
            if (vehicleInput == null) return;
            bool driving = IsDriving && playerInput != null && playerInput.IsGameplayInputEnabled &&
                isActiveAndEnabled;
            if (vehicleInput.IsDriverSessionActive != driving) vehicleInput.SetDriverSessionActive(driving);
        }

        private void LateUpdate()
        {
            RefreshSeatedPose();
            debugView?.ApplyCameraPose();
        }

        public void RefreshSeatedPose()
        {
            debugView?.RestoreCameraPose();
            if (!IsDriving || station == null || station.DriverEyeAnchor == null || playerCamera == null) return;
            Transform eyes = station.DriverEyeAnchor;
            transform.rotation = eyes.rotation * Quaternion.Euler(0f, localYawDegrees, 0f);
            // Keep the accepted camera hierarchy and pitch/look behavior. Only
            // the externally controlled root follows the authored eye anchor.
            transform.position = eyes.position - (playerCamera.transform.position - transform.position);
            lastReleaseWorldPosition = station.transform.TransformPoint(releaseLocalPosition);
        }

        private void ReleasePose(bool returnToCabin)
        {
            if (!IsDriving) return;
            RefreshSeatedPose();
            IsDriving = false;
            if (vehicleInput != null) vehicleInput.SetDriverSessionActive(false);
            interaction?.EndPrimaryInteraction();
            interaction?.EndSecondaryInteraction();
            interaction?.EndToolActivation();
            if (returnToCabin && motor != null)
            {
                Quaternion upright = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                if (!motor.TryRestoreWorldPose(lastReleaseWorldPosition, upright, out string failure) ||
                    !motor.TryRestoreSaveState(FirstPersonMotorSaveDto.Create(releasePosture, 0f), out failure))
                    throw new InvalidOperationException("Could not release the player in the cabin: " + failure);
            }
            if (motor != null) { motor.ResetInputIntent(); motor.enabled = motorWasEnabled; }
            if (capsule != null) capsule.enabled = capsuleWasEnabled;
            playerInput?.SetLocomotionInputEnabled(true);
        }

        private void Subscribe()
        {
            if (!initialized || subscribed) return;
            playerInput.DrivingModeRequested += HandleDrivingModeRequested;
            look.LookApplied += HandleLookApplied;
            subscribed = true;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable()
        {
            ReleasePose(returnToCabin: true);
            if (!subscribed) return;
            if (playerInput != null) playerInput.DrivingModeRequested -= HandleDrivingModeRequested;
            if (look != null) look.LookApplied -= HandleLookApplied;
            subscribed = false;
        }
        private void OnDestroy()
        {
            if (interaction != null) interaction.BindContextActionSource(null);
        }
    }
}
