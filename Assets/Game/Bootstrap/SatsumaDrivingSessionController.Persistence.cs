using System;
using MSC.Player;
using MSC.Vehicle;
using UnityEngine;

namespace MSC.Bootstrap
{
    public sealed partial class SatsumaDrivingSessionController
    {
        public string OwnerSaveDomainId => "vehicle.satsuma";

        public PlayerDrivingSaveDto CaptureDrivingState() => IsDriving
            ? PlayerDrivingSaveDto.Create(station.StationId, localYawDegrees, releaseLocalPosition, releasePosture)
            : null;

        public bool CanRestoreDrivingState(PlayerDrivingSaveDto state, out string failure)
        {
            if (state == null) { failure = string.Empty; return true; }
            if (!state.TryValidate(out failure)) return false;
            if (!initialized || station == null || station.DriverEyeAnchor == null ||
                station.DriverSeat == null || !string.Equals(state.StationId, station.StationId, StringComparison.Ordinal))
            { failure = "The saved driver station has no matching explicit binding."; return false; }
            Vector3 releasePosition = station.transform.TransformPoint(state.ReleaseLocalPosition);
            if (!station.ContainsPlayerPose(capsule, releasePosition, station.transform.rotation))
            { failure = "Saved driving release pose lies outside the interior driver trigger."; return false; }
            failure = string.Empty;
            return true;
        }

        public bool ValidateOwnerSavePayload(string payload, out string failure)
        {
            failure = "The saved vehicle does not contain the installed driver seat.";
            if (!initialized || station == null || station.DriverSeat == null) return false;
            try
            {
                VehicleDomainSaveDto state = JsonUtility.FromJson<VehicleDomainSaveDto>(payload);
                if (state == null || !state.TryValidateBasic(out failure)) return false;
                string seatId = station.DriverSeat.StableId.Value;
                foreach (var record in state.vehicles)
                {
                    if (record.stableVehicleId != station.VehicleStableId) continue;
                    bool partPresent = false, mountPresent = false;
                    foreach (var part in record.assembly.parts)
                        if (part.stableEntityId == seatId && part.installedMountId == SatsumaDriverStation.StockSeatMountId)
                            partPresent = true;
                    foreach (var mount in record.assembly.mounts)
                        if (mount.mountId == SatsumaDriverStation.StockSeatMountId && mount.installedPartStableEntityId == seatId)
                            mountPresent = true;
                    if (partPresent && mountPresent) { failure = string.Empty; return true; }
                }
            }
            catch (ArgumentException exception) { failure = exception.Message; return false; }
            failure = "The saved vehicle does not contain the installed driver seat.";
            return false;
        }

        public void ReleaseForPlayerPoseRestore() => ReleasePose(returnToCabin: false);

        public bool TryRestoreDrivingState(PlayerDrivingSaveDto state, out string failure)
        {
            if (!CanRestoreDrivingState(state, out failure)) return false;
            if (IsDriving) ReleaseForPlayerPoseRestore();
            if (state == null) return true;
            releaseLocalPosition = state.ReleaseLocalPosition;
            releasePosture = state.ReleasePosture;
            localYawDegrees = state.LocalYawDegrees;
            // Owner contents were checked in native preflight. Do not sample
            // current Installed during reverse transaction rollback: vehicle
            // restoration completes later in that same synchronous transaction.
            EnterPose();
            failure = string.Empty;
            return true;
        }
    }
}
