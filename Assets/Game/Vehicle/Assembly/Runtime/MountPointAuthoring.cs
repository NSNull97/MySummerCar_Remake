using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class MountPointAuthoring : MonoBehaviour
    {
        private readonly Collider[] obstructionBuffer = new Collider[32];

        [SerializeField]
        private MountPointDefinition definition;

        [SerializeField]
        private string mountId = string.Empty;

        [SerializeField]
        private Transform mountPose;

        [SerializeField]
        private LayerMask obstructionMask = ~0;

        public MountPointDefinition Definition => definition;

        public string MountId => mountId;

        public Transform Pose => mountPose != null ? mountPose : transform;

        public MountPose AuthoredPose => MountPose.Capture(Pose);

        public void Configure(
            MountPointDefinition mountDefinition,
            string runtimeMountId,
            Transform pose,
            LayerMask blockingLayers)
        {
            definition = mountDefinition;
            mountId = runtimeMountId ?? string.Empty;
            mountPose = pose;
            obstructionMask = blockingLayers;
        }

        public bool IsObstructed(PartInstance candidate)
        {
            if (definition == null || definition.Constraint.ObstructionRadiusMeters <= 0f)
            {
                return false;
            }

            int count = Physics.OverlapSphereNonAlloc(
                Pose.position,
                definition.Constraint.ObstructionRadiusMeters,
                obstructionBuffer,
                obstructionMask,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = obstructionBuffer[i];
                if (collider == null ||
                    (candidate != null && collider.transform.IsChildOf(candidate.transform)))
                {
                    continue;
                }

                if (collider.GetComponentInParent<AssemblyMountObstruction>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null)
            {
                return;
            }

            Transform pose = Pose;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pose.position, definition.Constraint.PositionToleranceMeters);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(pose.position, pose.forward * 0.35f);
            if (definition.Constraint.ObstructionRadiusMeters > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pose.position, definition.Constraint.ObstructionRadiusMeters);
            }
        }
    }

}
