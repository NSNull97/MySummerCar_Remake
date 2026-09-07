using System;
using UnityEngine;

namespace MSC.Player
{
    [Serializable]
    public sealed class PlayerDrivingSaveDto
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string stationId;
        [SerializeField] private float localYawDegrees;
        [SerializeField] private Vector3 releaseLocalPosition;
        [SerializeField] private PlayerPosture releasePosture;
        public int SchemaVersion => schemaVersion;
        public string StationId => stationId;
        public float LocalYawDegrees => localYawDegrees;
        public Vector3 ReleaseLocalPosition => releaseLocalPosition;
        public PlayerPosture ReleasePosture => releasePosture;

        public static PlayerDrivingSaveDto Create(string id, float yaw, Vector3 releasePosition, PlayerPosture posture)
        {
            var dto = new PlayerDrivingSaveDto
            { stationId = id, localYawDegrees = yaw, releaseLocalPosition = releasePosition, releasePosture = posture };
            if (!dto.TryValidate(out string failure)) throw new ArgumentException(failure);
            return dto;
        }

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != 1 || string.IsNullOrWhiteSpace(stationId) || stationId.Length > 128 ||
                !float.IsFinite(localYawDegrees) || Mathf.Abs(localYawDegrees) > 180f ||
                !PlayerSaveDto.IsFinite(releaseLocalPosition) || releaseLocalPosition.sqrMagnitude > 25f ||
                !PlayerLocomotionState.IsValidPosture(releasePosture))
            { failure = "Invalid player driving attachment state."; return false; }
            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Optional explicit Player/composition boundary. No Vehicle or Bootstrap
    /// dependency enters the player assembly; all inputs remain transient.
    /// </summary>
    public interface IPlayerDrivingPersistence
    {
        string OwnerSaveDomainId { get; }
        PlayerDrivingSaveDto CaptureDrivingState();
        bool CanRestoreDrivingState(PlayerDrivingSaveDto state, out string failure);
        bool ValidateOwnerSavePayload(string payload, out string failure);
        void ReleaseForPlayerPoseRestore();
        bool TryRestoreDrivingState(PlayerDrivingSaveDto state, out string failure);
    }
}
