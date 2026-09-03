using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Binds a mount authored relative to another loose assembly part. The
    /// mount stays in the vehicle registry, but follows its owner part in both
    /// loose and installed states without changing save identity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MountPointAuthoring))]
    public sealed class AssemblyOwnedMountAuthoring : MonoBehaviour
    {
        [SerializeField] private PartInstance ownerPart;
        [SerializeField] private bool requireInstalledOwner;

        public PartInstance OwnerPart => ownerPart;

        public bool RequireInstalledOwner => requireInstalledOwner;

        public bool IsAvailable => ownerPart != null &&
            (!requireInstalledOwner || ownerPart.IsInstalled);

        public void Configure(
            PartInstance authoredOwnerPart,
            bool authoredRequireInstalledOwner = false)
        {
            ownerPart = authoredOwnerPart;
            requireInstalledOwner = authoredRequireInstalledOwner;
        }

        public void RefreshAvailability()
        {
            bool available = IsAvailable;
            if (gameObject.activeSelf != available)
            {
                gameObject.SetActive(available);
            }
        }
    }
}
