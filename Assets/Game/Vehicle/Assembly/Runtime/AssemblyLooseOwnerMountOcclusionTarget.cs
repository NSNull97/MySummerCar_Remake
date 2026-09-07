using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Opt-in socket access for registered loose engine subassemblies.</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyLooseOwnerMountOcclusionTarget : MonoBehaviour,
        IParentColliderOcclusionBypass
    {
        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private MountPointAuthoring mount;
        [SerializeField] private AssemblyMountHandoffTarget ordinaryTarget;
        public VehicleAssemblyController Controller => controller;
        public MountPointAuthoring Mount => mount;
        public AssemblyMountHandoffTarget OrdinaryTarget => ordinaryTarget;

        public void Configure(VehicleAssemblyController assembly, MountPointAuthoring socket,
            AssemblyMountHandoffTarget existingTarget)
        {
            controller = assembly;
            mount = socket;
            ordinaryTarget = existingTarget;
        }

        public bool CanBypassParentCollider(InteractionTargetHost parentHost)
        {
            if (ordinaryTarget != null && ordinaryTarget.CanBypassParentCollider(parentHost))
            {
                return true;
            }
            if (controller == null || mount == null || parentHost == null ||
                !transform.IsChildOf(parentHost.transform))
            {
                return false;
            }
            PartInstance parent = parentHost.GetComponent<PartInstance>();
            if (parent == null || parent.IsAssemblyRoot || parent.IsInstalled ||
                System.Array.IndexOf(controller.AllRuntimeParts, parent) < 0)
            {
                return false;
            }
            PartInstance owner = mount.GetComponent<AssemblyOwnedMountAuthoring>()?.OwnerPart;
            for (int depth = 0; depth < controller.AllRuntimeParts.Length; depth++)
            {
                if (owner == null || owner.IsAssemblyRoot)
                {
                    return false;
                }
                if (owner == parent)
                {
                    return true;
                }
                if (!owner.IsInstalled)
                {
                    return false;
                }
                MountPointRuntime ownerMount = controller.Graph.FindMountForPart(owner);
                owner = ownerMount?.Authoring != null
                    ? ownerMount.Authoring.GetComponent<AssemblyOwnedMountAuthoring>()?.OwnerPart : null;
            }
            return false;
        }
    }
}
