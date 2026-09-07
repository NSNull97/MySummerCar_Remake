using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Fixed hard-line fitting, not an eighth tank mounting bolt. This packet
    /// implements fastening/save state only; fuel leakage is not simulated yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaFuelLineConnection : MonoBehaviour
    {
        public const string ConnectionId = "connection.satsuma.fuel-line-tank";
        public const string TankPartId = "vehicle.satsuma.part.fuel-tank";
        public const string TankMountId = "mount.satsuma.fuel-tank";
        public const int MaximumStage = 8;

        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private PartInstance tank;
        [SerializeField] private int stage;
        [SerializeField] private bool isBolted;

        public VehicleAssemblyController Assembly => assembly;
        public PartInstance Tank => tank;
        public int Stage => stage;
        public bool IsBolted => isBolted;
        public event Action StateChanged;

        public bool IsAvailable => isActiveAndEnabled && assembly != null && tank != null &&
            tank.Definition != null && tank.Definition.DefinitionId == TankPartId &&
            tank.RuntimeState.LifecycleState == PartLifecycleState.Installed &&
            tank.RuntimeState.InstalledMountId == TankMountId &&
            tank.transform.IsChildOf(assembly.transform);

        public void Configure(VehicleAssemblyController owner, PartInstance installedTank)
        {
            if (owner == null || owner.gameObject != gameObject || installedTank == null ||
                installedTank.Definition == null || installedTank.Definition.DefinitionId != TankPartId ||
                !installedTank.transform.IsChildOf(owner.transform))
                throw new ArgumentException("Fuel-line connection requires its explicit vehicle and stock tank.");
            assembly = owner;
            tank = installedTank;
            // Authoring/rebinding must not reset a previously saved latch.
        }

        public bool TryTurn(float signedNotches)
        {
            if (!IsAvailable || !float.IsFinite(signedNotches) || signedNotches == 0f) return false;
            int next = Mathf.Clamp(stage + (signedNotches > 0f ? 1 : -1), 0, MaximumStage);
            if (next == stage) return false;
            stage = next;
            if (stage == MaximumStage) isBolted = true;
            else if (stage == 0) isBolted = false;
            StateChanged?.Invoke();
            return true;
        }

        public SatsumaFuelLineConnectionSaveDto CaptureSaveData() => new()
        { stage = stage, isBolted = isBolted };

        public bool TryRestore(SatsumaFuelLineConnectionSaveDto dto, out string failure)
        {
            if (dto != null && !dto.TryValidate(out failure)) return false;
            stage = dto?.stage ?? 0;
            isBolted = dto?.isBolted ?? false;
            StateChanged?.Invoke();
            failure = string.Empty;
            return true;
        }
    }
}
