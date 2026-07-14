using UnityEngine;

namespace MSC.Interaction.Query
{
    [DisallowMultipleComponent]
    public sealed class RaycastInteractionCandidateSource : MonoBehaviour, IInteractionCandidateSource
    {
        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        [SerializeField]
        private Transform rayOrigin;

        [SerializeField, Min(0.1f)]
        private float maximumDistance = 2.25f;

        [SerializeField]
        private LayerMask interactionMask = ~0;

        private RaycastHit lastHit;
        private bool hasLastHit;
        private Rigidbody ignoredBody;

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
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                maximumDistance,
                interactionMask,
                QueryTriggerInteraction.Ignore);
            int nearestIndex = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.collider == null || IsIgnored(hit.collider) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestIndex = index;
                nearestDistance = hit.distance;
            }

            hasLastHit = nearestIndex >= 0;
            if (!hasLastHit)
            {
                lastHit = default;
                return default;
            }

            lastHit = hitBuffer[nearestIndex];

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

        /// <summary>
        /// Excludes one explicitly owned body from the query without allowing unrelated geometry
        /// to become transparent to interaction.
        /// </summary>
        public void SetIgnoredBody(Rigidbody body)
        {
            ignoredBody = body;
        }

        private bool IsIgnored(Collider candidate)
        {
            if (ignoredBody == null || candidate == null)
            {
                return false;
            }

            return candidate.attachedRigidbody == ignoredBody ||
                candidate.transform.IsChildOf(ignoredBody.transform);
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
