using UnityEngine;

namespace MSC.Interaction.Query
{
    [DisallowMultipleComponent]
    public sealed class RaycastInteractionCandidateSource : MonoBehaviour, IInteractionCandidateSource
    {
        [SerializeField]
        private Transform rayOrigin;

        [SerializeField, Min(0.1f)]
        private float maximumDistance = 2.25f;

        [SerializeField]
        private LayerMask interactionMask = ~0;

        private RaycastHit lastHit;
        private bool hasLastHit;

        public float MaximumDistance => maximumDistance;

        public bool HasLastHit => hasLastHit;

        public RaycastHit LastHit => lastHit;

        public InteractionCandidate Query()
        {
            if (rayOrigin == null)
            {
                hasLastHit = false;
                return default;
            }

            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
            hasLastHit = Physics.Raycast(
                ray,
                out lastHit,
                maximumDistance,
                interactionMask,
                QueryTriggerInteraction.Ignore);

            if (!hasLastHit)
            {
                return default;
            }

            InteractionTargetHost host = lastHit.collider.GetComponentInParent<InteractionTargetHost>();
            if (host == null)
            {
                return default;
            }

            return new InteractionCandidate(host, lastHit.point, lastHit.normal, lastHit.distance);
        }

        public void Configure(Transform origin, float distance, LayerMask mask)
        {
            rayOrigin = origin;
            maximumDistance = Mathf.Max(0.1f, distance);
            interactionMask = mask;
        }

        private void OnDrawGizmosSelected()
        {
            if (rayOrigin == null)
            {
                return;
            }

            Gizmos.color = hasLastHit ? Color.green : Color.yellow;
            Gizmos.DrawLine(rayOrigin.position, rayOrigin.position + rayOrigin.forward * maximumDistance);
            if (hasLastHit)
            {
                Gizmos.DrawWireSphere(lastHit.point, 0.035f);
            }
        }
    }
}
