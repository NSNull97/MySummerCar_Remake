using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Optional installation aim point, independent of the physical mounting
    /// pivot. Old mounts keep using Pose when this explicit binding is absent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyMountInteractionAnchor : MonoBehaviour
    {
        [SerializeField] private Vector3 localPosition;

        public Vector3 LocalPosition => localPosition;
        public Vector3 WorldPosition => transform.TransformPoint(localPosition);

        public void Configure(Vector3 position) => localPosition = position;

        public static Vector3 ResolveWorldPosition(MountPointAuthoring mount)
        {
            var anchor = mount.GetComponent<AssemblyMountInteractionAnchor>();
            return anchor != null ? anchor.WorldPosition : mount.Pose.position;
        }
    }
}
