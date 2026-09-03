using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Query
{
    [DisallowMultipleComponent]
    public sealed class RaycastInteractionCandidateSource : MonoBehaviour, IInteractionCandidateSource
    {
        private const float RegisteredTargetOcclusionToleranceMeters = 0.075f;
        private const float UnregisteredOcclusionToleranceMeters = 0.003f;
        private const float RayOriginOverlapRadiusMeters = 0.006f;
        // A generated Satsuma already contributes 29 solid chassis colliders,
        // installed-part interaction proxies and more than one hundred mount
        // triggers. A 32-hit query silently truncated dense wheel-well rays,
        // making the exact same socket work from one angle and disappear from
        // another. Keep the query allocation-free, but size the fixed buffers
        // for the authored vehicle rather than the old prototype scene.
        private readonly RaycastHit[] hitBuffer = new RaycastHit[256];
        private readonly Collider[] originOverlapBuffer = new Collider[64];

        [SerializeField]
        private Transform rayOrigin;

        [SerializeField, Min(0.1f)]
        private float maximumDistance = 2.25f;

        [SerializeField]
        private LayerMask interactionMask = ~0;

        private RaycastHit lastHit;
        private bool hasLastHit;
        private Rigidbody ignoredBody;
        private bool scrollToolTargetsOnly;
        private bool carriedObjectTargetsEnabled;
        private IPickupTarget carriedObjectTarget;

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
            var interactionContext = new InteractionContext(
                gameObject,
                ray.origin,
                ray.direction);
            LayerMask activeMask = interactionMask;
            int fastenerLayer = LayerMask.NameToLayer(
                FastenerToolRaycastLayer.Name);
            if (scrollToolTargetsOnly)
            {
                activeMask = fastenerLayer >= 0
                    ? 1 << fastenerLayer
                    : 0;
            }
            else if (fastenerLayer >= 0)
            {
                // Bolts and nuts are a dedicated tool-mode surface. Keeping
                // them out of the ordinary interaction ray prevents a visible
                // fastener from masking the mount/part directly behind it.
                activeMask &= ~(1 << fastenerLayer);
            }

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                maximumDistance,
                activeMask,
                QueryTriggerInteraction.Collide);
            int nearestSolidIndex = -1;
            float nearestSolidDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.collider == null ||
                    IsIgnored(hit.collider) ||
                    hit.collider.isTrigger ||
                    hit.distance >= nearestSolidDistance)
                {
                    continue;
                }

                nearestSolidIndex = index;
                nearestSolidDistance = hit.distance;
            }

            InteractionTargetHost nearestSolidHost = nearestSolidIndex >= 0
                ? hitBuffer[nearestSolidIndex].collider
                    .GetComponentInParent<InteractionTargetHost>()
                : null;

            int selectedIndex = -1;
            int selectedPriority = int.MinValue;
            float selectedDistance = float.PositiveInfinity;
            InteractionTargetHost selectedHost = null;
            Collider selectedCollider = null;
            Vector3 selectedPoint = Vector3.zero;
            Vector3 selectedNormal = Vector3.zero;
            float selectedOverlapVolume = float.PositiveInfinity;

            int overlapCount = Physics.OverlapSphereNonAlloc(
                ray.origin,
                RayOriginOverlapRadiusMeters,
                originOverlapBuffer,
                activeMask,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < overlapCount; index++)
            {
                Collider overlap = originOverlapBuffer[index];
                if (overlap == null || !overlap.isTrigger ||
                    IsIgnored(overlap) || IsUnregisteredTrigger(overlap))
                {
                    continue;
                }

                InteractionTargetHost overlapHost = overlap
                    .GetComponentInParent<InteractionTargetHost>();
                if (overlapHost == null ||
                    !overlapHost.TryGetCapability(
                        out IRaycastOriginOverlapTarget _) ||
                    IsDisabledCarriedObjectTarget(overlapHost) ||
                    IsIncompatibleCarriedObjectTarget(
                        overlapHost,
                        interactionContext) ||
                    scrollToolTargetsOnly &&
                    !overlapHost.TryGetCapability(
                        out IScrollHeldToolActivationTarget _))
                {
                    continue;
                }

                int priority = overlapHost.SelectionPriority;
                Vector3 size = overlap.bounds.size;
                float volume = Mathf.Max(
                    0.000001f,
                    size.x * size.y * size.z);
                if (selectedHost != null &&
                    (priority < selectedPriority ||
                     priority == selectedPriority &&
                     volume >= selectedOverlapVolume))
                {
                    continue;
                }

                selectedIndex = -1;
                selectedPriority = priority;
                selectedDistance = 0f;
                selectedOverlapVolume = volume;
                selectedHost = overlapHost;
                selectedCollider = overlap;
                selectedPoint = ray.origin;
                selectedNormal = -ray.direction;
            }

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.collider == null || IsIgnored(hit.collider) ||
                    IsUnregisteredTrigger(hit.collider))
                {
                    continue;
                }

                InteractionTargetHost candidateHost = hit.collider
                    .GetComponentInParent<InteractionTargetHost>();
                if (candidateHost == null)
                {
                    continue;
                }

                if (IsDisabledCarriedObjectTarget(candidateHost))
                {
                    continue;
                }

                if (IsIncompatibleCarriedObjectTarget(
                        candidateHost,
                        interactionContext))
                {
                    continue;
                }

                if (scrollToolTargetsOnly &&
                    !candidateHost.TryGetCapability(
                        out IScrollHeldToolActivationTarget _))
                {
                    // The dedicated layer already excludes ordinary geometry;
                    // keep the capability check as a second authority boundary
                    // so an accidentally mis-layered object cannot steal the
                    // wrench ray.
                    continue;
                }

                if (nearestSolidIndex >= 0)
                {
                    float tolerance = nearestSolidHost != null
                        ? RegisteredTargetOcclusionToleranceMeters
                        : UnregisteredOcclusionToleranceMeters;
                    bool bypassesOwnParentCollider =
                        nearestSolidHost != null &&
                        candidateHost != nearestSolidHost &&
                        candidateHost.transform.IsChildOf(
                            nearestSolidHost.transform) &&
                        candidateHost.TryGetCapability(
                            out IParentColliderOcclusionBypass bypass) &&
                        bypass.CanBypassParentCollider(nearestSolidHost);
                    if (!bypassesOwnParentCollider &&
                        hit.distance > nearestSolidDistance + tolerance)
                    {
                        continue;
                    }
                }

                int priority = candidateHost.SelectionPriority;
                if (selectedHost == null || priority > selectedPriority ||
                    priority == selectedPriority &&
                    hit.distance < selectedDistance)
                {
                    selectedIndex = index;
                    selectedPriority = priority;
                    selectedDistance = hit.distance;
                    selectedOverlapVolume = float.PositiveInfinity;
                    selectedHost = candidateHost;
                    selectedCollider = hit.collider;
                    selectedPoint = hit.point;
                    selectedNormal = hit.normal;
                }
            }

            if (selectedHost == null)
            {
                hasLastHit = nearestSolidIndex >= 0;
                lastHit = hasLastHit
                    ? hitBuffer[nearestSolidIndex]
                    : default;
                return default;
            }

            if (selectedIndex >= 0)
            {
                hasLastHit = true;
                lastHit = hitBuffer[selectedIndex];
            }
            else
            {
                // RaycastHit cannot represent an origin-overlap query. The
                // returned InteractionCandidate remains authoritative.
                hasLastHit = false;
                lastHit = default;
            }

            return new InteractionCandidate(
                selectedHost,
                selectedPoint,
                selectedNormal,
                selectedDistance,
                selectedCollider);
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

        public void SetScrollToolTargetsOnly(bool value)
        {
            scrollToolTargetsOnly = value;
        }

        public void SetCarriedObjectTargetsEnabled(bool value)
        {
            carriedObjectTargetsEnabled = value;
            if (!value)
            {
                carriedObjectTarget = null;
            }
        }

        public void SetCarriedObjectTarget(IPickupTarget pickupTarget)
        {
            carriedObjectTarget = pickupTarget;
            carriedObjectTargetsEnabled = pickupTarget != null;
        }

        private bool IsDisabledCarriedObjectTarget(
            InteractionTargetHost host) =>
            !carriedObjectTargetsEnabled && host != null &&
            host.TryGetCapability(
                out IRequiresCarriedObjectRaycastTarget _);

        private bool IsIncompatibleCarriedObjectTarget(
            InteractionTargetHost host,
            in InteractionContext context) =>
            carriedObjectTargetsEnabled && carriedObjectTarget != null &&
            host != null &&
            host.TryGetCapability(
                out ICarriedObjectRaycastFilter filter) &&
            !filter.CanSelectForCarriedObject(
                carriedObjectTarget,
                context);

        private bool IsIgnored(Collider candidate)
        {
            if (ignoredBody == null || candidate == null)
            {
                return false;
            }

            return candidate.attachedRigidbody == ignoredBody ||
                candidate.transform.IsChildOf(ignoredBody.transform);
        }

        private static bool IsUnregisteredTrigger(Collider candidate) =>
            candidate.isTrigger &&
            candidate.GetComponentInParent<InteractionTargetHost>() == null;

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
