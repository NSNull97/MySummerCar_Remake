using System;
using UnityEngine;

namespace MSC.Player
{
    [Serializable]
    public sealed class FirstPersonMotorSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private bool crouching;
        [SerializeField] private PlayerPosture posture;
        [SerializeField] private float verticalSpeedMetersPerSecond;

        public int SchemaVersion => schemaVersion;
        public PlayerPosture Posture =>
            posture == PlayerPosture.Standing && crouching
                ? PlayerPosture.Crouch
                : posture;
        public bool Crouching => Posture != PlayerPosture.Standing;
        public float VerticalSpeedMetersPerSecond => verticalSpeedMetersPerSecond;

        public static FirstPersonMotorSaveDto Create(
            bool isCrouching,
            float verticalSpeed) =>
            Create(
                isCrouching
                    ? PlayerPosture.Crouch
                    : PlayerPosture.Standing,
                verticalSpeed);

        public static FirstPersonMotorSaveDto Create(
            PlayerPosture configuredPosture,
            float verticalSpeed)
        {
            if (!float.IsFinite(verticalSpeed))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalSpeed),
                    "Player vertical speed must be finite.");
            }

            if (!PlayerLocomotionState.IsValidPosture(configuredPosture))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredPosture),
                    "Player posture is invalid.");
            }

            return new FirstPersonMotorSaveDto
            {
                crouching = configuredPosture != PlayerPosture.Standing,
                posture = configuredPosture,
                verticalSpeedMetersPerSecond = verticalSpeed,
            };
        }

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported player motor schema {schemaVersion}.";
                return false;
            }

            if (!float.IsFinite(verticalSpeedMetersPerSecond) ||
                Mathf.Abs(verticalSpeedMetersPerSecond) > 200f)
            {
                failure = "Player vertical speed is invalid.";
                return false;
            }

            if (!PlayerLocomotionState.IsValidPosture(Posture))
            {
                failure = "Player posture is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FirstPersonLookSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private float pitchDegrees;

        public int SchemaVersion => schemaVersion;
        public float PitchDegrees => pitchDegrees;

        public static FirstPersonLookSaveDto Create(float pitch)
        {
            if (!float.IsFinite(pitch))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pitch),
                    "Player look pitch must be finite.");
            }

            return new FirstPersonLookSaveDto { pitchDegrees = pitch };
        }

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported player look schema {schemaVersion}.";
                return false;
            }

            if (!float.IsFinite(pitchDegrees) || Mathf.Abs(pitchDegrees) > 89f)
            {
                failure = "Player look pitch is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Domain-owned native state for the currently implemented first-person
    /// player. Save owns aggregation and storage, not this DTO's semantics.
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentConfigurationId = "player.first-person.v1";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string configurationId = CurrentConfigurationId;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Quaternion worldRotation = Quaternion.identity;
        [SerializeField] private FirstPersonMotorSaveDto motor;
        [SerializeField] private FirstPersonLookSaveDto look;
        [SerializeField] private bool hasDrivingState;
        [SerializeField] private PlayerDrivingSaveDto drivingState;

        public int SchemaVersion => schemaVersion;
        public string ConfigurationId => configurationId;
        public Vector3 WorldPosition => worldPosition;
        public Quaternion WorldRotation => worldRotation;
        public FirstPersonMotorSaveDto Motor => motor;
        public FirstPersonLookSaveDto Look => look;
        public bool HasDrivingState => hasDrivingState;
        public PlayerDrivingSaveDto DrivingState => hasDrivingState ? drivingState : null;

        public static PlayerSaveDto Create(
            Vector3 position,
            Quaternion rotation,
            FirstPersonMotorSaveDto motorState,
            FirstPersonLookSaveDto lookState,
            PlayerDrivingSaveDto driving = null)
        {
            var dto = new PlayerSaveDto
            {
                worldPosition = position,
                worldRotation = Normalize(rotation),
                motor = motorState,
                look = lookState,
                hasDrivingState = driving != null,
                drivingState = driving,
            };

            if (!dto.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(position));
            }

            return dto;
        }

        public bool TryValidate(out string failure)
        {
            if (hasDrivingState && (drivingState == null || !drivingState.TryValidate(out _)))
            {
                failure = "Player driving state is missing or invalid.";
                return false;
            }
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported player schema {schemaVersion}.";
                return false;
            }

            if (!string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal))
            {
                failure = $"Unsupported player configuration '{configurationId}'.";
                return false;
            }

            if (!IsFinite(worldPosition) || !IsValidRotation(worldRotation))
            {
                failure = "Player world pose is invalid.";
                return false;
            }

            if (motor == null)
            {
                failure = "Player motor state is missing.";
                return false;
            }

            if (!motor.TryValidate(out failure))
            {
                return false;
            }

            if (look == null)
            {
                failure = "Player look state is missing.";
                return false;
            }

            if (!look.TryValidate(out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        internal static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        internal static bool IsValidRotation(Quaternion value)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) ||
                !float.IsFinite(value.w))
            {
                return false;
            }

            float magnitudeSquared = value.x * value.x + value.y * value.y +
                                     value.z * value.z + value.w * value.w;
            return magnitudeSquared > 0.000001f &&
                   magnitudeSquared < 1000000f;
        }

        internal static Quaternion Normalize(Quaternion value)
        {
            if (!IsValidRotation(value))
            {
                return value;
            }

            float inverseMagnitude = 1f / Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }
    }

    public static class PlayerPersistence
    {
        public static PlayerSaveDto Capture(
            FirstPersonMotor motor,
            FirstPersonLook look)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            return PlayerSaveDto.Create(
                motor.transform.position,
                motor.transform.rotation,
                motor.CaptureSaveState(),
                look.CaptureSaveState(),
                motor.GetComponent<IPlayerDrivingPersistence>()?.CaptureDrivingState());
        }

        public static bool CanRestoreDrivingState(PlayerSaveDto state, FirstPersonMotor motor, out string failure)
        {
            IPlayerDrivingPersistence driving = motor != null
                ? motor.GetComponent<IPlayerDrivingPersistence>() : null;
            if (state != null && state.HasDrivingState && driving == null)
            { failure = "This player has no binding for the saved driver station."; return false; }
            if (driving != null) return driving.CanRestoreDrivingState(state?.DrivingState, out failure);
            failure = string.Empty;
            return true;
        }

        public static bool TryRestore(
            PlayerSaveDto state,
            FirstPersonMotor motor,
            FirstPersonLook look,
            out string failure)
        {
            if (state == null)
            {
                failure = "Player state is missing.";
                return false;
            }

            if (!state.TryValidate(out failure))
            {
                return false;
            }

            if (motor == null || look == null)
            {
                failure = "Player persistence references are incomplete.";
                return false;
            }

            if (!motor.CanRestoreSaveState(state.Motor, out failure) ||
                !look.CanRestoreSaveState(state.Look, out failure) ||
                !CanRestoreDrivingState(state, motor, out failure))
            {
                return false;
            }

            PlayerSaveDto checkpoint = Capture(motor, look);
            IPlayerDrivingPersistence driving = motor.GetComponent<IPlayerDrivingPersistence>();
            driving?.ReleaseForPlayerPoseRestore();
            if (!motor.TryRestoreWorldPose(
                    state.WorldPosition,
                    state.WorldRotation,
                    out failure) ||
                !motor.TryRestoreSaveState(state.Motor, out failure) ||
                !look.TryRestoreSaveState(state.Look, out failure) ||
                driving != null && !driving.TryRestoreDrivingState(state.DrivingState, out failure))
            {
                // Every input was preflighted, so rollback is expected to be
                // infallible. Preserve the original failure if authoring was
                // externally invalidated between preflight and apply.
                string originalFailure = failure;
                driving?.ReleaseForPlayerPoseRestore();
                motor.TryRestoreWorldPose(
                    checkpoint.WorldPosition,
                    checkpoint.WorldRotation,
                    out _);
                motor.TryRestoreSaveState(checkpoint.Motor, out _);
                look.TryRestoreSaveState(checkpoint.Look, out _);
                driving?.TryRestoreDrivingState(checkpoint.DrivingState, out _);
                failure = originalFailure;
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
