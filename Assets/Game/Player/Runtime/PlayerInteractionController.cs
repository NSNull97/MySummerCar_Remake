using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Player
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionController : MonoBehaviour, IInteractionService
    {
        private readonly RaycastHit[] placementHits = new RaycastHit[16];

        [SerializeField]
        private RaycastInteractionCandidateSource candidateSource;

        [SerializeField]
        private PhysicalCarryController carryController;

        [SerializeField]
        private Transform viewpoint;

        [SerializeField, Min(0.1f)]
        private float placementDistance = 2.75f;

        [SerializeField]
        private LayerMask placementMask = ~0;

        [SerializeField]
        private bool interactionEnabled = true;

        private InteractionCandidate currentCandidate;

        public bool IsInteractionEnabled => interactionEnabled && enabled && gameObject.activeInHierarchy;

        public InteractionCandidate CurrentCandidate => currentCandidate.IsValid ? currentCandidate : default;

        public bool HasCandidate => currentCandidate.IsValid;

        public bool HasHeldObject => carryController != null && carryController.HasHeldObject;

        public string CurrentPrompt
        {
            get
            {
                if (!IsInteractionEnabled || !currentCandidate.IsValid)
                {
                    return string.Empty;
                }

                InteractionContext context = CreateContext();
                return currentCandidate.GetPrompt(HasHeldObject, context);
            }
        }

        public string HeldStableId => carryController != null ? carryController.HeldStableId : string.Empty;

        public void RefreshCandidate()
        {
            if (!IsInteractionEnabled || candidateSource == null)
            {
                currentCandidate = default;
                return;
            }

            candidateSource.SetIgnoredBody(
                carryController != null && carryController.HasHeldObject
                    ? carryController.HeldBody
                    : null);
            currentCandidate = candidateSource.Query();
        }

        public bool TryPrimaryInteraction()
        {
            if (!IsInteractionEnabled || !currentCandidate.IsValid || carryController == null)
            {
                return false;
            }

            InteractionContext context = CreateContext();
            if (carryController.HasHeldObject &&
                currentCandidate.TryGetCapability(out IMountHandoffTarget mountTarget))
            {
                return carryController.TryHandoff(mountTarget, context);
            }

            if (!carryController.HasHeldObject &&
                currentCandidate.TryGetCapability(out IPickupTarget pickupTarget))
            {
                return carryController.TryPickup(pickupTarget, context);
            }

            if (currentCandidate.TryGetCapability(out IContextInteractionTarget interactionTarget) &&
                interactionTarget.CanInteract(context))
            {
                interactionTarget.Interact(context);
                return true;
            }

            return false;
        }

        public bool TryToolActivation()
        {
            if (!IsInteractionEnabled || !currentCandidate.IsValid)
            {
                return false;
            }

            InteractionContext context = CreateContext();
            if (!currentCandidate.TryGetCapability(out IToolActivationTarget target) ||
                !target.CanActivateTool(context))
            {
                return false;
            }

            target.ActivateTool(context);
            return true;
        }

        public bool DropHeldObject()
        {
            return carryController != null && carryController.Drop();
        }

        public bool ThrowHeldObject()
        {
            return carryController != null && viewpoint != null && carryController.Throw(viewpoint.forward);
        }

        public bool TryPlaceHeldObject()
        {
            if (carryController == null || !carryController.HasHeldObject || viewpoint == null)
            {
                return false;
            }

            int count = Physics.RaycastNonAlloc(
                viewpoint.position,
                viewpoint.forward,
                placementHits,
                placementDistance,
                placementMask,
                QueryTriggerInteraction.Ignore);

            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            Rigidbody heldBody = carryController.HeldTarget.Body;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = placementHits[i];
                if (hit.collider == null ||
                    hit.collider.attachedRigidbody == heldBody ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
            {
                return false;
            }

            RaycastHit surface = placementHits[bestIndex];
            return carryController.TryPlace(surface.point, surface.normal, placementMask);
        }

        public void RotateHeldObject(Vector2 degrees)
        {
            if (carryController != null)
            {
                carryController.RotateHeld(degrees);
            }
        }

        public CarriedObjectSaveState CaptureCarriedObjectState()
        {
            return carryController != null
                ? carryController.CaptureSaveState()
                : CarriedObjectSaveState.Empty();
        }

        public void Configure(
            RaycastInteractionCandidateSource source,
            PhysicalCarryController carry,
            Transform view,
            LayerMask surfaceMask)
        {
            candidateSource = source;
            carryController = carry;
            viewpoint = view;
            placementMask = surfaceMask;
        }

        private void Update()
        {
            RefreshCandidate();
        }

        private InteractionContext CreateContext()
        {
            Transform source = viewpoint != null ? viewpoint : transform;
            return new InteractionContext(gameObject, source.position, source.forward);
        }
    }
}
