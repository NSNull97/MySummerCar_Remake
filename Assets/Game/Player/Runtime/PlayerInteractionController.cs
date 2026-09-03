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
        private CarryRotationAxis carryRotationAxis;
        private float rotationAxisHintUntil;
        private IContinuousContextInteractionTarget activeContinuousTarget;
        private ContinuousContextInteractionDirection activeContinuousDirection;
        private IFirstPersonToolSelectionTarget activeFirstPersonTool;
        private bool firstPersonToolSelectionTransitionActive;
        private float firstPersonToolSelectionTransitionStartedAt;
        private float firstPersonToolSelectionTransitionDuration;
        private Vector3 firstPersonToolSelectionStartLocalPosition;
        private Quaternion firstPersonToolSelectionStartLocalRotation;

        public bool IsInteractionEnabled => interactionEnabled && enabled && gameObject.activeInHierarchy;

        public InteractionCandidate CurrentCandidate => currentCandidate.IsValid ? currentCandidate : default;

        public bool HasCandidate => currentCandidate.IsValid;

        public bool HasHeldObject => carryController != null && carryController.HasHeldObject;

        public bool IsFirstPersonToolModeActive =>
            IsAlive(activeFirstPersonTool);

        public bool TryGetHeldCapability<TCapability>(
            out TCapability capability)
            where TCapability : class
        {
            if (IsFirstPersonToolModeActive &&
                activeFirstPersonTool is TCapability toolCapability)
            {
                capability = toolCapability;
                return true;
            }

            if (carryController != null)
            {
                return carryController.TryGetHeldCapability(out capability);
            }

            capability = null;
            return false;
        }

        public string CurrentPrompt
        {
            get
            {
                if (!IsInteractionEnabled || !currentCandidate.IsValid)
                {
                    return string.Empty;
                }

                InteractionContext context = CreateContext();
                if (IsFirstPersonToolModeActive)
                {
                    return currentCandidate.TryGetCapability(
                            out IScrollHeldToolActivationTarget _) &&
                        currentCandidate.TryGetCapability(
                            out IToolActivationTarget toolPrompt)
                            ? toolPrompt.ToolPrompt
                            : string.Empty;
                }

                return currentCandidate.GetPrompt(
                    HasHeldObject,
                    HasHeldObject ? carryController.HeldTarget : null,
                    context);
            }
        }

        public string CurrentDisplayName
        {
            get
            {
                if (!IsInteractionEnabled || !currentCandidate.IsValid ||
                    !currentCandidate.TryGetCapability(
                        out IInteractionDisplayTarget displayTarget))
                {
                    return string.Empty;
                }

                return displayTarget.InteractionDisplayName ?? string.Empty;
            }
        }

        public string CurrentDisplayLocalizationKey
        {
            get
            {
                if (!IsInteractionEnabled || !currentCandidate.IsValid ||
                    !currentCandidate.TryGetCapability(
                        out IInteractionLocalizationTarget localizationTarget))
                {
                    return string.Empty;
                }

                return localizationTarget.InteractionLocalizationKey ??
                    string.Empty;
            }
        }

        public string CurrentPromptBindingLabel
        {
            get
            {
                if (!IsInteractionEnabled || !currentCandidate.IsValid)
                {
                    return string.Empty;
                }

                InteractionContext context = CreateContext();
                if (IsFirstPersonToolModeActive &&
                    currentCandidate.TryGetCapability(
                        out IScrollHeldToolActivationTarget _))
                {
                    return "КОЛЕСО";
                }

                if (!HasHeldObject &&
                    currentCandidate.TryGetCapability(
                        out IFirstPersonToolSelectionTarget selectionTarget) &&
                    selectionTarget.CanSelect(context))
                {
                    return "ЛКМ";
                }

                if (HasHeldObject &&
                    currentCandidate.TryGetCapability(
                        out IMountHandoffTarget mountTarget) &&
                    mountTarget.CanAccept(carryController.HeldTarget, context))
                {
                    return "ЛКМ";
                }

                if (!HasHeldObject &&
                    currentCandidate.TryGetCapability(
                        out IPickupTarget pickupTarget) &&
                    pickupTarget.CanPickup(context))
                {
                    if (currentCandidate.TryGetCapability(
                            out IToolActivationTarget pickupTool) &&
                        pickupTool.CanActivateTool(context))
                    {
                        return string.Empty;
                    }

                    return "ЛКМ";
                }

                if (currentCandidate.TryGetCapability(
                        out IContinuousContextInteractionTarget continuousTarget) &&
                    (continuousTarget.CanBeginContinuousInteraction(
                         context,
                         ContinuousContextInteractionDirection.Primary) ||
                     continuousTarget.CanBeginContinuousInteraction(
                         context,
                         ContinuousContextInteractionDirection.Secondary)))
                {
                    return "ЛКМ";
                }

                if (currentCandidate.TryGetCapability(
                        out IContextInteractionTarget interactionTarget) &&
                    interactionTarget.CanInteract(context))
                {
                    return interactionTarget is ISecondaryInteractionOnlyTarget
                        ? "ПКМ"
                        : "ЛКМ";
                }

                if (!HasHeldObject &&
                    currentCandidate.TryGetCapability(
                        out IIncrementalInteractionTarget incrementalTarget) &&
                    incrementalTarget.CanAdjust(context))
                {
                    return "КОЛЕСО";
                }

                if (currentCandidate.TryGetCapability(
                        out IToolActivationTarget toolTarget) &&
                    toolTarget.CanActivateTool(context))
                {
                    return "F";
                }

                return string.Empty;
            }
        }

        public string HeldStableId => carryController != null ? carryController.HeldStableId : string.Empty;

        public string RotationAxisHint =>
            HasHeldObject && Time.unscaledTime <= rotationAxisHintUntil
                ? $"сменить ось вращения (сейчас {GetRotationAxisLabel(carryRotationAxis)})"
                : string.Empty;

        public bool CanRotateHeldObject =>
            HasHeldObject &&
            !carryController.IsHeldUseReady &&
            !carryController.HasActiveContinuousHeldActivation;

        /// <summary>
        /// Returns the current interaction presentation without mutating the
        /// target. Input dispatch and hint composition deliberately live on
        /// the same boundary so a plaque cannot promise an action that the
        /// controller would route somewhere else.
        /// </summary>
        public InteractionActionSnapshot CurrentActionSnapshot =>
            BuildCurrentActionSnapshot();

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
            candidateSource.SetScrollToolTargetsOnly(
                IsFirstPersonToolModeActive);
            candidateSource.SetCarriedObjectTarget(
                HasHeldObject ? carryController.HeldTarget : null);
            currentCandidate = candidateSource.Query();
            RefreshFirstPersonToolPresentation();
        }

        private InteractionActionSnapshot BuildCurrentActionSnapshot()
        {
            if (!IsInteractionEnabled)
            {
                return InteractionActionSnapshot.Empty;
            }

            InteractionContext context = CreateContext();
            if (IsFirstPersonToolModeActive)
            {
                return BuildFirstPersonToolActionSnapshot(context);
            }

            if (HasHeldObject)
            {
                return BuildHeldObjectActionSnapshot(context);
            }

            return BuildWorldActionSnapshot(context);
        }

        private InteractionActionSnapshot BuildFirstPersonToolActionSnapshot(
            in InteractionContext context)
        {
            InteractionActionSnapshot snapshot =
                InteractionActionSnapshot.Empty;
            if (currentCandidate.IsValid &&
                TryGetHeldCapability(out IHeldToolIdentity heldTool) &&
                currentCandidate.TryGetCapability(
                    out IScrollHeldToolActivationTarget scrollTarget))
            {
                if (scrollTarget is
                    IDirectionalScrollHeldToolActivationTarget directional)
                {
                    snapshot = AddHeldToolScrollAction(
                        snapshot,
                        directional,
                        heldTool,
                        context,
                        InteractionScrollDirection.Positive);
                    snapshot = AddHeldToolScrollAction(
                        snapshot,
                        directional,
                        heldTool,
                        context,
                        InteractionScrollDirection.Negative);
                }
                else if (currentCandidate.TryGetCapability(
                    out IToolActivationTarget promptTarget))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Scroll,
                        promptTarget.ToolPrompt));
                }
            }

            return snapshot.Add(new InteractionActionHint(
                InteractionActionBinding.Interact,
                "УБРАТЬ ИНСТРУМЕНТ"));
        }

        private InteractionActionSnapshot BuildHeldObjectActionSnapshot(
            in InteractionContext context)
        {
            InteractionActionSnapshot snapshot =
                InteractionActionSnapshot.Empty;
            IContinuousContextInteractionTarget continuousTarget = null;
            bool directionalHold =
                currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(out continuousTarget) &&
                continuousTarget.UsesDirectionalHold;

            if (directionalHold)
            {
                if (continuousTarget.CanBeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        continuousTarget.InteractionPrompt));
                }
            }
            else
            {
                bool canInstall =
                    currentCandidate.IsValid &&
                    currentCandidate.TryGetCapability(
                        out IMountHandoffTarget mountTarget) &&
                    mountTarget.CanAccept(carryController.HeldTarget, context);
                if (canInstall)
                {
                    snapshot = snapshot
                        .WithReticle(InteractionReticleKind.Install)
                        .Add(new InteractionActionHint(
                            InteractionActionBinding.Interact,
                            "УСТАНОВИТЬ"));
                }
                else if (currentCandidate.IsValid &&
                    currentCandidate.TryGetCapability(
                        out IContextInteractionTarget interactionTarget) &&
                    interactionTarget is not ISecondaryInteractionOnlyTarget &&
                    interactionTarget.CanInteract(context))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        interactionTarget.InteractionPrompt));
                }
                else
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        "ОТПУСТИТЬ"));
                }
            }

            if (directionalHold)
            {
                if (continuousTarget.CanBeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Throw,
                        continuousTarget.InteractionPrompt));
                }
            }
            else
            {
                snapshot = snapshot.Add(new InteractionActionHint(
                    InteractionActionBinding.Throw,
                    "БРОСИТЬ"));
            }

            if (CanRotateHeldObject)
            {
                snapshot = snapshot.Add(new InteractionActionHint(
                    InteractionActionBinding.Scroll,
                    "ВРАЩАТЬ"));
            }

            return snapshot;
        }

        private InteractionActionSnapshot BuildWorldActionSnapshot(
            in InteractionContext context)
        {
            InteractionActionSnapshot snapshot =
                InteractionActionSnapshot.Empty;
            if (!currentCandidate.IsValid)
            {
                return snapshot;
            }

            if (currentCandidate.TryGetCapability(
                    out IContinuousContextInteractionTarget continuousTarget) &&
                continuousTarget.UsesDirectionalHold)
            {
                if (continuousTarget.CanBeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        continuousTarget.InteractionPrompt));
                }

                if (continuousTarget.CanBeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary))
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Throw,
                        continuousTarget.InteractionPrompt));
                }

                return AddToolAction(snapshot, context);
            }

            bool hasPrimaryAction = false;
            if (currentCandidate.TryGetCapability(
                    out IFirstPersonToolSelectionTarget selectionTarget) &&
                selectionTarget.CanSelect(context))
            {
                snapshot = snapshot
                    .WithReticle(InteractionReticleKind.Pickup)
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        "ВЗЯТЬ"));
                hasPrimaryAction = true;
            }
            else if (currentCandidate.TryGetCapability(
                         out IPickupTarget pickupTarget) &&
                     pickupTarget.CanPickup(context))
            {
                snapshot = snapshot
                    .WithReticle(InteractionReticleKind.Pickup)
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        "ВЗЯТЬ"));
                hasPrimaryAction = true;
            }

            if (currentCandidate.TryGetCapability(
                    out IContextInteractionTarget interactionTarget) &&
                interactionTarget.CanInteract(context))
            {
                if (interactionTarget is IRemovalInteractionTarget)
                {
                    snapshot = snapshot.WithReticle(
                        InteractionReticleKind.Remove);
                }

                if (interactionTarget is ISecondaryInteractionOnlyTarget)
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Throw,
                        interactionTarget.InteractionPrompt));
                }
                else if (!hasPrimaryAction)
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        interactionTarget.InteractionPrompt));
                    hasPrimaryAction = true;
                }
            }

            if (currentCandidate.TryGetCapability(
                    out IIncrementalInteractionTarget incrementalTarget) &&
                incrementalTarget.CanAdjust(context))
            {
                if (incrementalTarget is
                    IDirectionalIncrementalInteractionTarget directional)
                {
                    snapshot = AddIncrementalScrollAction(
                        snapshot,
                        directional,
                        context,
                        InteractionScrollDirection.Positive);
                    snapshot = AddIncrementalScrollAction(
                        snapshot,
                        directional,
                        context,
                        InteractionScrollDirection.Negative);
                }
                else
                {
                    snapshot = snapshot.Add(new InteractionActionHint(
                        InteractionActionBinding.Scroll,
                        incrementalTarget.AdjustmentPrompt));
                }
            }

            return AddToolAction(snapshot, context);
        }

        private InteractionActionSnapshot AddToolAction(
            InteractionActionSnapshot snapshot,
            in InteractionContext context)
        {
            if (currentCandidate.TryGetCapability(
                    out IToolActivationTarget toolTarget) &&
                toolTarget.CanActivateTool(context))
            {
                snapshot = snapshot.Add(new InteractionActionHint(
                    InteractionActionBinding.ToolActivate,
                    toolTarget.ToolPrompt));
            }

            return snapshot;
        }

        private static InteractionActionSnapshot AddHeldToolScrollAction(
            InteractionActionSnapshot snapshot,
            IDirectionalScrollHeldToolActivationTarget target,
            IHeldToolIdentity heldTool,
            in InteractionContext context,
            InteractionScrollDirection direction)
        {
            return target.CanActivateHeldTool(heldTool, context, direction)
                ? snapshot.Add(new InteractionActionHint(
                    InteractionActionBinding.Scroll,
                    target.GetHeldToolScrollPrompt(direction),
                    direction))
                : snapshot;
        }

        private static InteractionActionSnapshot AddIncrementalScrollAction(
            InteractionActionSnapshot snapshot,
            IDirectionalIncrementalInteractionTarget target,
            in InteractionContext context,
            InteractionScrollDirection direction)
        {
            return target.CanAdjust(context, direction)
                ? snapshot.Add(new InteractionActionHint(
                    InteractionActionBinding.Scroll,
                    target.GetAdjustmentPrompt(direction),
                    direction))
                : snapshot;
        }

        public bool TryPrimaryInteraction()
        {
            if (!IsInteractionEnabled || !currentCandidate.IsValid || carryController == null)
            {
                return false;
            }

            InteractionContext context = CreateContext();
            if (TrySelectCurrentFirstPersonTool(context))
            {
                return true;
            }

            if (carryController.HasHeldObject &&
                currentCandidate.TryGetCapability(out IMountHandoffTarget mountTarget))
            {
                return carryController.TryHandoff(mountTarget, context);
            }

            if (!carryController.HasHeldObject &&
                currentCandidate.TryGetCapability(out IPickupTarget pickupTarget))
            {
                return carryController.TryPickup(
                    pickupTarget,
                    context,
                    currentCandidate.Point);
            }

            if (currentCandidate.TryGetCapability(out IContextInteractionTarget interactionTarget) &&
                interactionTarget is not ISecondaryInteractionOnlyTarget &&
                interactionTarget.CanInteract(context))
            {
                interactionTarget.Interact(context);
                return true;
            }

            return false;
        }

        public bool TryPickupOrPlace()
        {
            if (!IsInteractionEnabled || carryController == null)
            {
                return false;
            }

            InteractionContext context = CreateContext();
            if (IsFirstPersonToolModeActive)
            {
                return DeselectFirstPersonTool();
            }

            if (!carryController.HasHeldObject)
            {
                if (TrySelectCurrentFirstPersonTool(context))
                {
                    return true;
                }

                bool pickedUp =
                    currentCandidate.IsValid &&
                    currentCandidate.TryGetCapability(
                        out IPickupTarget pickupTarget) &&
                    carryController.TryPickup(
                        pickupTarget,
                        context,
                        currentCandidate.Point);
                if (pickedUp)
                {
                    carryRotationAxis = CarryRotationAxis.Yaw;
                    return true;
                }

                if (currentCandidate.IsValid &&
                    currentCandidate.TryGetCapability(
                        out IContextInteractionTarget interactionTarget) &&
                    interactionTarget is not ISecondaryInteractionOnlyTarget &&
                    interactionTarget.CanInteract(context))
                {
                    interactionTarget.Interact(context);
                    return true;
                }

                return false;
            }

            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IMountHandoffTarget mountTarget) &&
                carryController.TryHandoff(mountTarget, context))
            {
                return true;
            }

            // A precise context zone (for example, a door handle) wins over
            // releasing the carried object. This preserves one-button donor-
            // style interaction without requiring a modifier while carrying.
            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IContextInteractionTarget heldInteractionTarget) &&
                heldInteractionTarget is not ISecondaryInteractionOnlyTarget &&
                heldInteractionTarget.CanInteract(context))
            {
                heldInteractionTarget.Interact(context);
                return true;
            }

            // Phase 1 follows the donor control contract: LMB releases the
            // carried body with its current physical momentum. Surface-snapped
            // placement remains available as an explicit API for the future
            // Phase 2 placement mode, but is not the ordinary LMB action.
            return DropHeldObject();
        }

        public bool TryBeginPrimaryInteraction()
        {
            if (TryBeginContinuousContextInteraction(
                    ContinuousContextInteractionDirection.Primary,
                    out bool capabilityPresent))
            {
                return true;
            }

            // A directional handle consumes its button even at the matching
            // end stop. This prevents LMB from dropping a carried object while
            // the player is still aiming at fully opened garage doors.
            return capabilityPresent || TryPickupOrPlace();
        }

        public bool TryBeginSecondaryInteraction()
        {
            if (IsFirstPersonToolModeActive)
            {
                return DeselectFirstPersonTool();
            }

            if (TryBeginContinuousContextInteraction(
                    ContinuousContextInteractionDirection.Secondary,
                    out bool capabilityPresent))
            {
                return true;
            }

            if (capabilityPresent)
            {
                return true;
            }

            if (!HasHeldObject && currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IContextInteractionTarget secondaryTarget) &&
                secondaryTarget is ISecondaryInteractionOnlyTarget)
            {
                InteractionContext context = CreateContext();
                if (secondaryTarget.CanInteract(context))
                {
                    secondaryTarget.Interact(context);
                    return true;
                }
            }

            // With a carried body RMB keeps its ordinary throw behavior.
            return ThrowHeldObject();
        }

        public bool ContinuePrimaryInteraction(float deltaTime) =>
            ContinueContinuousContextInteraction(
                ContinuousContextInteractionDirection.Primary,
                deltaTime);

        public bool ContinueSecondaryInteraction(float deltaTime) =>
            ContinueContinuousContextInteraction(
                ContinuousContextInteractionDirection.Secondary,
                deltaTime);

        public void EndPrimaryInteraction()
        {
            EndContinuousContextInteraction(
                ContinuousContextInteractionDirection.Primary);
        }

        public void EndSecondaryInteraction()
        {
            EndContinuousContextInteraction(
                ContinuousContextInteractionDirection.Secondary);
        }

        public bool TryToolActivation()
        {
            return TryToolActivation(allowContinuousHeldActivation: false);
        }

        public bool TryBeginToolActivation()
        {
            return TryToolActivation(allowContinuousHeldActivation: true);
        }

        public bool ContinueToolActivation(float unscaledDeltaTime)
        {
            return IsInteractionEnabled &&
                   carryController != null &&
                   carryController.ContinueContinuousHeldActivation(
                       unscaledDeltaTime);
        }

        public void EndToolActivation()
        {
            carryController?.EndContinuousHeldActivation();
        }

        private bool TryToolActivation(
            bool allowContinuousHeldActivation)
        {
            if (!IsInteractionEnabled)
            {
                return false;
            }

            if (IsFirstPersonToolModeActive)
            {
                // Wrenches are operated exclusively by mouse-wheel direction.
                // F must not retain the old magic one-button fastening path.
                return false;
            }

            InteractionContext context = CreateContext();
            if ((carryController == null || !carryController.HasHeldObject) &&
                currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IContextInteractionTarget directTarget) &&
                directTarget is not IPrimaryInteractionOnlyTarget &&
                directTarget is not ISecondaryInteractionOnlyTarget &&
                directTarget.CanInteract(context))
            {
                directTarget.Interact(context);
                return true;
            }

            if (allowContinuousHeldActivation &&
                carryController != null &&
                !carryController.HasHeldObject &&
                currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IContinuousActivationPickupTarget usePickupTarget) &&
                usePickupTarget.CanPickupForContinuousActivation(context) &&
                currentCandidate.TryGetCapability(
                    out IPickupTarget pickupTarget) &&
                carryController.TryPickupForContinuousHeldActivation(
                    pickupTarget,
                    context,
                    currentCandidate.Point))
            {
                carryRotationAxis = CarryRotationAxis.Yaw;
                return true;
            }

            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IContextInteractionTarget interactionTarget) &&
                interactionTarget is not IPrimaryInteractionOnlyTarget &&
                interactionTarget is not ISecondaryInteractionOnlyTarget &&
                carryController != null &&
                carryController.TryActivateHeldOn(interactionTarget, context))
            {
                return true;
            }

            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IHeldToolActivationTarget heldToolTarget))
            {
                if (carryController != null && carryController.HasHeldObject &&
                    carryController.TryGetHeldCapability(
                        out IHeldToolIdentity heldTool) &&
                    heldToolTarget.CanActivateHeldTool(heldTool, context))
                {
                    heldToolTarget.ActivateHeldTool(heldTool, context);
                    return true;
                }

                // An explicit held-tool boundary must not fall through to the
                // legacy parameterless tool capability on the same host.
                // Otherwise pressing F with empty hands can activate tools or
                // fasteners that require an actually carried tool.
                return false;
            }

            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IToolActivationTarget target) &&
                target.CanActivateTool(context))
            {
                target.ActivateTool(context);
                return true;
            }

            if (carryController == null)
            {
                return false;
            }

            if (allowContinuousHeldActivation &&
                !carryController.IsHeldUseReady &&
                carryController.TryPrepareHeldContinuousActivation(context))
            {
                carryRotationAxis = CarryRotationAxis.Yaw;
                return true;
            }

            if (allowContinuousHeldActivation &&
                carryController.IsHeldUseReady &&
                carryController.TryBeginContinuousHeldActivation(context))
            {
                return true;
            }

            return carryController.TryActivateHeld(context);
        }

        public bool DropHeldObject()
        {
            return IsFirstPersonToolModeActive
                ? DeselectFirstPersonTool()
                : carryController != null && carryController.Drop();
        }

        public bool ThrowHeldObject()
        {
            return IsFirstPersonToolModeActive
                ? DeselectFirstPersonTool()
                : carryController != null && viewpoint != null &&
                  carryController.Throw(viewpoint.forward);
        }

        public bool TryPlaceHeldObject()
        {
            if (IsFirstPersonToolModeActive || carryController == null ||
                !carryController.HasHeldObject || viewpoint == null)
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

        public void RotateHeldObject(float degrees)
        {
            if (carryController != null)
            {
                carryController.RotateHeld(degrees, carryRotationAxis);
            }
        }

        public bool TryOperateCurrentHeldTool(float signedNotches)
        {
            if (!IsInteractionEnabled ||
                !IsFirstPersonToolModeActive ||
                !currentCandidate.IsValid ||
                !float.IsFinite(signedNotches) ||
                Mathf.Abs(signedNotches) < 0.001f ||
                !TryGetHeldCapability(
                    out IHeldToolIdentity heldTool) ||
                !currentCandidate.TryGetCapability(
                    out IScrollHeldToolActivationTarget target))
            {
                return false;
            }

            return target.TryActivateHeldTool(
                heldTool,
                CreateContext(),
                signedNotches);
        }

        public bool TryAdjustCurrentInteraction(float signedNotches)
        {
            if (!IsInteractionEnabled ||
                HasHeldObject ||
                !currentCandidate.IsValid ||
                !float.IsFinite(signedNotches) ||
                Mathf.Abs(signedNotches) < 0.001f ||
                !currentCandidate.TryGetCapability(
                    out IIncrementalInteractionTarget target))
            {
                return false;
            }

            InteractionContext context = CreateContext();
            return target.CanAdjust(context) &&
                   target.TryAdjust(context, signedNotches);
        }

        public void CycleHeldRotationAxis()
        {
            if (!HasHeldObject)
            {
                return;
            }

            carryRotationAxis = (CarryRotationAxis)(
                ((int)carryRotationAxis + 1) % 3);
            rotationAxisHintUntil = Time.unscaledTime + 1.25f;
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

        private void OnDisable()
        {
            EndAllContinuousContextInteraction();
            EndToolActivation();
            currentCandidate = default;
            RefreshFirstPersonToolPresentation();
        }

        private void RefreshFirstPersonToolPresentation()
        {
            if (!IsFirstPersonToolModeActive)
            {
                return;
            }

            IFirstPersonToolSelectionTarget heldTool =
                activeFirstPersonTool;
            Transform visual = heldTool.ToolVisual;
            if (visual == null)
            {
                DeselectFirstPersonTool();
                return;
            }

            InteractionContext context = CreateContext();
            if (currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out IFirstPersonToolSnapTarget snapTarget) &&
                snapTarget.TryResolveToolSnapAnchor(
                    heldTool,
                    context,
                    out Transform anchor) &&
                anchor != null)
            {
                // BetterMSC-style acquisition is immediate: the wrench head
                // is welded visually to the authored bolt pose on hover.
                firstPersonToolSelectionTransitionActive = false;
                visual.SetParent(null, true);
                visual.SetPositionAndRotation(
                    anchor.position,
                    anchor.rotation);
                return;
            }

            if (viewpoint == null)
            {
                return;
            }

            if (visual.parent != viewpoint)
            {
                visual.SetParent(viewpoint, true);
            }

            if (firstPersonToolSelectionTransitionActive)
            {
                float elapsed = Time.unscaledTime -
                    firstPersonToolSelectionTransitionStartedAt;
                float normalized = firstPersonToolSelectionTransitionDuration > 0f
                    ? Mathf.Clamp01(
                        elapsed / firstPersonToolSelectionTransitionDuration)
                    : 1f;
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                visual.localPosition = Vector3.LerpUnclamped(
                    firstPersonToolSelectionStartLocalPosition,
                    heldTool.IdleLocalPosition,
                    eased);
                visual.localRotation = Quaternion.SlerpUnclamped(
                    firstPersonToolSelectionStartLocalRotation,
                    heldTool.IdleLocalRotation,
                    eased);
                if (normalized < 1f)
                {
                    return;
                }

                firstPersonToolSelectionTransitionActive = false;
            }

            visual.localPosition = heldTool.IdleLocalPosition;
            visual.localRotation = heldTool.IdleLocalRotation;
        }

        private bool TrySelectCurrentFirstPersonTool(
            in InteractionContext context)
        {
            if (IsFirstPersonToolModeActive || HasHeldObject ||
                !currentCandidate.IsValid ||
                !currentCandidate.TryGetCapability(
                    out IFirstPersonToolSelectionTarget selectionTarget) ||
                !selectionTarget.CanSelect(context) ||
                selectionTarget.ToolVisual == null || viewpoint == null)
            {
                return false;
            }

            activeFirstPersonTool = selectionTarget;
            selectionTarget.NotifySelected(context);
            BeginFirstPersonToolSelectionPresentation(selectionTarget);
            RefreshFirstPersonToolPresentation();
            return true;
        }

        private void BeginFirstPersonToolSelectionPresentation(
            IFirstPersonToolSelectionTarget selectionTarget)
        {
            Transform visual = selectionTarget.ToolVisual;
            visual.SetParent(viewpoint, true);
            firstPersonToolSelectionStartLocalPosition = visual.localPosition;
            firstPersonToolSelectionStartLocalRotation = visual.localRotation;
            firstPersonToolSelectionTransitionDuration =
                selectionTarget is IFirstPersonToolSelectionTransition transition
                    ? Mathf.Max(
                        0f,
                        transition.SelectionTransitionDurationSeconds)
                    : 0f;
            firstPersonToolSelectionTransitionStartedAt = Time.unscaledTime;
            firstPersonToolSelectionTransitionActive =
                firstPersonToolSelectionTransitionDuration > 0f;
        }

        private bool DeselectFirstPersonTool()
        {
            if (!IsFirstPersonToolModeActive)
            {
                activeFirstPersonTool = null;
                return false;
            }

            IFirstPersonToolSelectionTarget selected =
                activeFirstPersonTool;
            activeFirstPersonTool = null;
            firstPersonToolSelectionTransitionActive = false;
            selected.NotifyDeselected();
            candidateSource?.SetScrollToolTargetsOnly(false);
            return true;
        }

        private static bool IsAlive(
            IFirstPersonToolSelectionTarget target)
        {
            if (target == null)
            {
                return false;
            }

            return target is not Object unityObject || unityObject != null;
        }

        private bool TryBeginContinuousContextInteraction(
            ContinuousContextInteractionDirection direction,
            out bool capabilityPresent)
        {
            IContinuousContextInteractionTarget target = null;
            capabilityPresent = IsInteractionEnabled &&
                currentCandidate.IsValid &&
                currentCandidate.TryGetCapability(
                    out target) &&
                target.UsesDirectionalHold;
            if (!capabilityPresent)
            {
                return false;
            }

            InteractionContext context = CreateContext();
            if (!target.CanBeginContinuousInteraction(context, direction))
            {
                return false;
            }

            EndAllContinuousContextInteraction();
            target.BeginContinuousInteraction(context, direction);
            activeContinuousTarget = target;
            activeContinuousDirection = direction;
            return true;
        }

        private bool ContinueContinuousContextInteraction(
            ContinuousContextInteractionDirection direction,
            float deltaTime)
        {
            if (activeContinuousTarget == null ||
                activeContinuousDirection != direction)
            {
                return false;
            }

            if (activeContinuousTarget.ContinueContinuousInteraction(
                    deltaTime))
            {
                return true;
            }

            activeContinuousTarget.EndContinuousInteraction();
            activeContinuousTarget = null;
            return false;
        }

        private void EndContinuousContextInteraction(
            ContinuousContextInteractionDirection direction)
        {
            if (activeContinuousTarget == null ||
                activeContinuousDirection != direction)
            {
                return;
            }

            activeContinuousTarget.EndContinuousInteraction();
            activeContinuousTarget = null;
        }

        private void EndAllContinuousContextInteraction()
        {
            if (activeContinuousTarget == null)
            {
                return;
            }

            activeContinuousTarget.EndContinuousInteraction();
            activeContinuousTarget = null;
        }

        private InteractionContext CreateContext()
        {
            Transform source = viewpoint != null ? viewpoint : transform;
            return new InteractionContext(gameObject, source.position, source.forward);
        }

        private static string GetRotationAxisLabel(CarryRotationAxis axis)
        {
            return axis switch
            {
                CarryRotationAxis.Pitch => "X",
                CarryRotationAxis.Roll => "Z",
                _ => "Y",
            };
        }
    }
}
