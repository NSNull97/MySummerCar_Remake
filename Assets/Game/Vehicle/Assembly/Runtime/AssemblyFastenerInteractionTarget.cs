using System.Collections;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyFastenerInteractionTarget : MonoBehaviour,
        IToolActivationTarget,
        IHeldToolActivationTarget,
        IDirectionalScrollHeldToolActivationTarget,
        IFirstPersonToolSnapTarget,
        IInteractionOutlineFeedbackSource,
        IInteractionOutlineRendererSource
    {
        // BetterMSC ToolHand.ToolData["Spanner"] and ScrewBolt coroutine,
        // inspected read-only from the locally installed mod. The remake owns
        // this clean procedural presentation and does not execute donor code.
        private static readonly Vector3 BetterMscSpannerBoltOffset =
            new Vector3(-0.073f, 0.028f, 0.016f);
        private static readonly Vector3 BetterMscSpannerBoltEuler =
            new Vector3(0f, 0f, 160f);
        private const float BetterMscWorkArcDegrees = 60f;
        private const float DonorStageTravelMeters = 0.0025f;
        private const float DonorStageRotationDegrees = 45f;
        private const float ApproachSeconds = 0.22f;
        private const float WorkSeconds = 0.24f;
        private const float RegripSeconds = 0.12f;
        private const float ReturnSeconds = 0.16f;

        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private string mountId = string.Empty;
        [SerializeField] private string fastenerDefinitionId = string.Empty;
        [SerializeField] private ToolDefinition tool;
        [SerializeField] private bool automaticReverseAtLimits = true;
        [SerializeField] private Transform fastenerPresentation;
        [SerializeField] private Vector3 fastenerPresentationBaseLocalPosition;
        [SerializeField] private Quaternion fastenerPresentationBaseLocalRotation =
            Quaternion.identity;
        [SerializeField, Min(0f)]
        private float fastenerPresentationStageTravelScale = 1f;

        private bool loosening;
        private bool hasFastenerPresentationBasePose;
        private Transform wrenchPresentationAnchor;
        private Coroutine wrenchAnimation;
        private PhysicalCarryController animatedCarry;
        private IPickupTarget animatedTool;
        private Coroutine snappedTurnAnimation;
        private float snappedTurnAngleDegrees;
        private PartInstance cachedRendererPart;
        private Renderer cachedOutlineRenderer;
        private Renderer[] presentationRenderers = System.Array.Empty<Renderer>();
        private Collider interactionCollider;
        private int observedGraphMutationCount = -1;

        public VehicleAssemblyController Controller => controller;
        public string MountId => mountId;
        public string FastenerDefinitionId => fastenerDefinitionId;

        public float FastenerPresentationStageTravelScale =>
            fastenerPresentationStageTravelScale;

        public void RefreshAvailability()
        {
            RefreshInteractionAvailability(force: true);
        }

        public string ToolPrompt
        {
            get
            {
                if (!TryGetFastener(out FastenerInstance fastener))
                {
                    return "Крепёж недоступен";
                }

                return $"Колесо вверх — затянуть, вниз — ослабить " +
                    $"{fastener.Definition.DisplayName} " +
                    $"({fastener.Stage}/{fastener.Definition.MaximumStage}, " +
                    $"{tool?.DisplayName ?? "нет инструмента"})";
            }
        }

        public void Configure(
            VehicleAssemblyController assemblyController,
            string runtimeMountId,
            string fastenerId,
            ToolDefinition activeTool,
            bool reverseAtLimits,
            Transform authoredFastenerPresentation = null,
            float authoredFastenerPresentationStageTravelScale = 1f)
        {
            controller = assemblyController;
            mountId = runtimeMountId ?? string.Empty;
            fastenerDefinitionId = fastenerId ?? string.Empty;
            tool = activeTool;
            automaticReverseAtLimits = reverseAtLimits;
            fastenerPresentation = authoredFastenerPresentation != null
                ? authoredFastenerPresentation
                : fastenerPresentation;
            fastenerPresentationStageTravelScale = Mathf.Max(
                0f,
                authoredFastenerPresentationStageTravelScale);
            CacheFastenerPresentationBasePose();
            loosening = false;
            CacheInteractionCollider();
            RefreshInteractionAvailability(force: true);
        }

        public bool CanActivateTool(in InteractionContext context)
        {
            RefreshInteractionAvailability();
            // Fasteners never expose the old magic F action. The capability is
            // retained as a prompt surface for compatibility; actual mutation
            // requires a selected wrench and directional mouse-wheel input.
            return false;
        }

        public void ActivateTool(in InteractionContext context)
        {
            TryOperate(context, animateHeldTool: false);
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity heldTool,
            in InteractionContext context) =>
            ToolMatches(heldTool) && CanActivateTool(context);

        public void ActivateHeldTool(
            IHeldToolIdentity heldTool,
            in InteractionContext context)
        {
            if (ToolMatches(heldTool))
            {
                TryOperate(context, animateHeldTool: true);
            }
        }

        public bool TryActivateHeldTool(
            IHeldToolIdentity heldTool,
            in InteractionContext context,
            float signedNotches)
        {
            if (!ToolMatches(heldTool) ||
                !float.IsFinite(signedNotches) ||
                Mathf.Abs(signedNotches) < 0.001f ||
                !TryGetFastener(out FastenerInstance fastener) ||
                !fastener.IsInserted)
            {
                return false;
            }

            // Consume extra wheel input while one BetterMSC work/regrip cycle
            // is still running. One visible stroke is exactly one donor stage;
            // a high-resolution wheel must not tighten eight stages in a frame.
            if (snappedTurnAnimation != null)
            {
                return true;
            }

            bool clockwiseTightens = fastener.Definition.TighteningDirection ==
                FastenerDirection.ClockwiseToTighten;
            bool tighten = signedNotches > 0f;
            FastenerRotationDirection direction =
                tighten == clockwiseTightens
                    ? FastenerRotationDirection.Clockwise
                    : FastenerRotationDirection.CounterClockwise;
            int previousStage = fastener.Stage;
            AssemblyOperationResult result = controller.TryTurnFastener(
                mountId,
                fastenerDefinitionId,
                tool,
                direction);
            if (!result.Succeeded)
            {
                return false;
            }

            BeginSnappedWrenchTurn(
                direction,
                previousStage,
                fastener.Stage);
            return true;
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity heldTool,
            in InteractionContext context,
            InteractionScrollDirection direction)
        {
            if (!ToolMatches(heldTool) ||
                !TryGetFastener(out FastenerInstance fastener) ||
                fastener.Definition == null ||
                !fastener.IsInserted ||
                controller == null ||
                !controller.Graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount) ||
                mount.Authoring == null ||
                mount.Authoring.IsObstructed(mount.InstalledPart))
            {
                return false;
            }

            return direction == InteractionScrollDirection.Positive
                ? fastener.Stage < fastener.Definition.MaximumStage
                : fastener.Stage > 0;
        }

        public string GetHeldToolScrollPrompt(
            InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive
                ? "ЗАТЯНУТЬ"
                : "ОСЛАБИТЬ";

        public bool TryResolveToolSnapAnchor(
            IHeldToolIdentity heldTool,
            in InteractionContext context,
            out Transform anchor)
        {
            RefreshInteractionAvailability();
            if (heldTool == null ||
                !TryGetFastener(out FastenerInstance fastener) ||
                !fastener.IsInserted)
            {
                anchor = null;
                return false;
            }

            EnsureWrenchPresentationAnchor();
            if (snappedTurnAnimation == null)
            {
                ApplySnappedWrenchPose();
            }
            anchor = wrenchPresentationAnchor;
            return anchor != null;
        }

        public InteractionOutlineFeedback GetOutlineFeedback(
            IHeldToolIdentity heldTool)
        {
            if (!TryGetFastener(out FastenerInstance fastener))
            {
                return InteractionOutlineFeedback.Default;
            }

            if (heldTool != null && !ToolMatches(heldTool))
            {
                return InteractionOutlineFeedback.Invalid;
            }

            if (fastener.Stage <= 0)
            {
                return InteractionOutlineFeedback.Loose;
            }

            return fastener.Stage >= fastener.Definition.MaximumStage
                ? InteractionOutlineFeedback.Complete
                : InteractionOutlineFeedback.Partial;
        }

        public Renderer ResolveOutlineRenderer()
        {
            // Stock body-panel fastener visuals are deliberately parented to
            // the installed panel so that they follow an operable door or
            // bootlid. They are therefore not descendants of this interaction
            // marker. Prefer the explicitly authored visual before looking at
            // children or falling back to the nearest renderer on the part;
            // the old fallback outlined the entire fender/bumper/bootlid.
            Renderer authoredPresentation = fastenerPresentation != null
                ? fastenerPresentation.GetComponent<Renderer>() ??
                  fastenerPresentation.GetComponentInChildren<Renderer>(true)
                : null;
            if (authoredPresentation != null &&
                authoredPresentation.enabled &&
                !authoredPresentation.forceRenderingOff)
            {
                return authoredPresentation;
            }

            CachePresentationRenderers();
            for (int index = 0; index < presentationRenderers.Length; index++)
            {
                Renderer presentation = presentationRenderers[index];
                if (presentation != null && presentation.enabled &&
                    presentation.gameObject.activeInHierarchy)
                {
                    return presentation;
                }
            }

            if (!TryGetFastener(out _) ||
                !controller.Graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount) ||
                mount.InstalledPart == null)
            {
                cachedRendererPart = null;
                cachedOutlineRenderer = null;
                return null;
            }

            if (cachedRendererPart == mount.InstalledPart &&
                cachedOutlineRenderer != null)
            {
                return cachedOutlineRenderer;
            }

            cachedRendererPart = mount.InstalledPart;
            cachedOutlineRenderer = null;
            float bestDistance = float.PositiveInfinity;
            Renderer[] renderers = mount.InstalledPart
                .GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer candidate = renderers[index];
                if (candidate == null || !candidate.enabled ||
                    candidate.forceRenderingOff)
                {
                    continue;
                }

                float distance = candidate.bounds.SqrDistance(
                    transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    cachedOutlineRenderer = candidate;
                }
            }

            return cachedOutlineRenderer;
        }

        private void TryOperate(
            in InteractionContext context,
            bool animateHeldTool)
        {
            if (!TryGetFastener(out FastenerInstance fastener))
            {
                return;
            }

            if (automaticReverseAtLimits)
            {
                if (fastener.Stage >= fastener.Definition.MaximumStage)
                {
                    loosening = true;
                }
                else if (fastener.Stage <= 0)
                {
                    loosening = false;
                }
            }

            bool tighten = !loosening;
            bool clockwiseTightens = fastener.Definition.TighteningDirection ==
                FastenerDirection.ClockwiseToTighten;
            FastenerRotationDirection rotationDirection =
                tighten == clockwiseTightens
                    ? FastenerRotationDirection.Clockwise
                    : FastenerRotationDirection.CounterClockwise;
            AssemblyOperationResult result = controller.TryTurnFastener(
                mountId,
                fastenerDefinitionId,
                tool,
                rotationDirection);
            if (result.Succeeded && animateHeldTool)
            {
                BeginWrenchAnimation(context, rotationDirection);
            }
        }

        private void BeginWrenchAnimation(
            in InteractionContext context,
            FastenerRotationDirection direction)
        {
            PhysicalCarryController carry = context.Interactor != null
                ? context.Interactor.GetComponent<PhysicalCarryController>()
                : null;
            if (carry == null || !carry.HasHeldObject)
            {
                return;
            }

            StopWrenchAnimation(clearPose: true);
            animatedCarry = carry;
            animatedTool = carry.HeldTarget;
            EnsureWrenchPresentationAnchor();
            float sign = direction == FastenerRotationDirection.Clockwise
                ? -1f
                : 1f;
            wrenchAnimation = StartCoroutine(AnimateWrench(sign));
        }

        private IEnumerator AnimateWrench(float directionSign)
        {
            Quaternion baseRotation = Quaternion.Euler(
                BetterMscSpannerBoltEuler);
            wrenchPresentationAnchor.localPosition =
                BetterMscSpannerBoltOffset;
            wrenchPresentationAnchor.localRotation = baseRotation;

            float elapsed = 0f;
            while (elapsed < ApproachSeconds && IsAnimatedToolHeld())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ApproachSeconds);
                animatedCarry.SetHeldPresentationAnchor(
                    wrenchPresentationAnchor,
                    EaseOutCubic(t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < WorkSeconds && IsAnimatedToolHeld())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / WorkSeconds);
                float eased = EaseInOutQuart(t);
                wrenchPresentationAnchor.localRotation =
                    baseRotation * Quaternion.AngleAxis(
                        directionSign * BetterMscWorkArcDegrees * eased,
                        Vector3.forward);
                wrenchPresentationAnchor.localPosition =
                    BetterMscSpannerBoltOffset +
                    Vector3.forward * (0.03f * Mathf.Sin(t * Mathf.PI));
                animatedCarry.SetHeldPresentationAnchor(
                    wrenchPresentationAnchor,
                    1f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < ReturnSeconds && IsAnimatedToolHeld())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ReturnSeconds);
                animatedCarry.SetHeldPresentationAnchor(
                    wrenchPresentationAnchor,
                    1f - EaseInQuart(t));
                yield return null;
            }

            if (IsAnimatedToolHeld())
            {
                animatedCarry.ClearHeldPresentationPose();
            }

            wrenchAnimation = null;
            animatedCarry = null;
            animatedTool = null;
        }

        private void EnsureWrenchPresentationAnchor()
        {
            if (wrenchPresentationAnchor != null)
            {
                return;
            }

            wrenchPresentationAnchor = new GameObject(
                "BetterMSC spanner action pose").transform;
            wrenchPresentationAnchor.SetParent(transform, false);
            ApplySnappedWrenchPose();
        }

        private void BeginSnappedWrenchTurn(
            FastenerRotationDirection direction,
            int previousStage,
            int targetStage)
        {
            EnsureWrenchPresentationAnchor();
            float sign = direction == FastenerRotationDirection.Clockwise
                ? -1f
                : 1f;
            snappedTurnAngleDegrees = 0f;
            snappedTurnAnimation = StartCoroutine(
                AnimateSnappedWrenchTurn(
                    sign,
                    previousStage,
                    targetStage));
        }

        private IEnumerator AnimateSnappedWrenchTurn(
            float directionSign,
            int previousStage,
            int targetStage)
        {
            float elapsed = 0f;
            while (elapsed < WorkSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / WorkSeconds);
                float eased = EaseInOutQuart(normalized);
                snappedTurnAngleDegrees = directionSign *
                    BetterMscWorkArcDegrees * eased;
                ApplySnappedWrenchPose();
                ApplyFastenerPresentation(Mathf.LerpUnclamped(
                    previousStage,
                    targetStage,
                    eased));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < RegripSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / RegripSeconds);
                snappedTurnAngleDegrees = Mathf.LerpUnclamped(
                    directionSign * BetterMscWorkArcDegrees,
                    0f,
                    EaseOutQuart(normalized));
                ApplySnappedWrenchPose(
                    0.024f * Mathf.Sin(normalized * Mathf.PI));
                yield return null;
            }

            snappedTurnAngleDegrees = 0f;
            ApplySnappedWrenchPose();
            ApplyFastenerPresentation(targetStage);
            snappedTurnAnimation = null;
        }

        private void ApplySnappedWrenchPose(float regripLift = 0f)
        {
            if (wrenchPresentationAnchor == null)
            {
                return;
            }

            wrenchPresentationAnchor.localPosition =
                BetterMscSpannerBoltOffset +
                Vector3.forward * regripLift;
            wrenchPresentationAnchor.localRotation =
                Quaternion.Euler(BetterMscSpannerBoltEuler) *
                Quaternion.AngleAxis(
                    snappedTurnAngleDegrees,
                    Vector3.forward);
        }

        private bool IsAnimatedToolHeld() =>
            animatedCarry != null && animatedCarry.HasHeldObject &&
            ReferenceEquals(animatedCarry.HeldTarget, animatedTool);

        private void StopWrenchAnimation(bool clearPose)
        {
            if (wrenchAnimation != null)
            {
                StopCoroutine(wrenchAnimation);
                wrenchAnimation = null;
            }

            if (clearPose && IsAnimatedToolHeld())
            {
                animatedCarry.ClearHeldPresentationPose();
            }

            animatedCarry = null;
            animatedTool = null;
        }

        private void OnDisable()
        {
            if (snappedTurnAnimation != null)
            {
                StopCoroutine(snappedTurnAnimation);
                snappedTurnAnimation = null;
            }

            snappedTurnAngleDegrees = 0f;
            ApplySnappedWrenchPose();
            if (TryGetFastener(out FastenerInstance fastener))
            {
                ApplyFastenerPresentation(fastener.Stage);
            }

            StopWrenchAnimation(clearPose: true);
        }

        private void Awake()
        {
            CacheFastenerPresentationBasePose();
            CacheInteractionCollider();
        }

        private void Update()
        {
            RefreshInteractionAvailability();
        }

        private void CacheInteractionCollider()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            CachePresentationRenderers();
        }

        private void CachePresentationRenderers()
        {
            if (presentationRenderers == null ||
                presentationRenderers.Length == 0)
            {
                presentationRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void RefreshInteractionAvailability(bool force = false)
        {
            CacheInteractionCollider();
            if (interactionCollider == null || controller == null)
            {
                return;
            }

            int mutationCount = controller.GraphMutationCount;
            if (!force && mutationCount == observedGraphMutationCount)
            {
                return;
            }

            observedGraphMutationCount = mutationCount;
            FastenerInstance fastener = null;
            bool available = controller.Graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount) &&
                mount.IsOccupied &&
                mount.TryGetFastener(
                    fastenerDefinitionId,
                    out fastener) &&
                fastener.IsInserted;
            interactionCollider.enabled = available;
            if (available && snappedTurnAnimation == null)
            {
                ApplyFastenerPresentation(fastener.Stage);
            }
            CachePresentationRenderers();
            for (int index = 0; index < presentationRenderers.Length; index++)
            {
                if (presentationRenderers[index] != null)
                {
                    presentationRenderers[index].enabled = available;
                }
            }
            if (!available)
            {
                cachedRendererPart = null;
                cachedOutlineRenderer = null;
            }
        }

        private void CacheFastenerPresentationBasePose()
        {
            if (fastenerPresentation == null)
            {
                Transform candidate = transform.Find(
                    "Visible inserted bolt or nut");
                if (candidate != null)
                {
                    fastenerPresentation = candidate;
                }
            }

            if (fastenerPresentation == null ||
                hasFastenerPresentationBasePose)
            {
                return;
            }

            fastenerPresentationBaseLocalPosition =
                fastenerPresentation.localPosition;
            fastenerPresentationBaseLocalRotation =
                fastenerPresentation.localRotation;
            hasFastenerPresentationBasePose = true;
        }

        private void ApplyFastenerPresentation(float stage)
        {
            CacheFastenerPresentationBasePose();
            if (fastenerPresentation == null ||
                !hasFastenerPresentationBasePose)
            {
                return;
            }

            float clampedStage = Mathf.Max(0f, stage);
            fastenerPresentation.localPosition =
                fastenerPresentationBaseLocalPosition +
                fastenerPresentationBaseLocalRotation *
                (Vector3.back *
                 (DonorStageTravelMeters * clampedStage *
                  fastenerPresentationStageTravelScale));
            fastenerPresentation.localRotation =
                fastenerPresentationBaseLocalRotation *
                Quaternion.AngleAxis(
                    DonorStageRotationDegrees * clampedStage,
                    Vector3.forward);
        }

        private bool ToolMatches(IHeldToolIdentity heldTool) =>
            heldTool != null && tool != null &&
            string.Equals(
                heldTool.ToolType,
                tool.ToolType,
                System.StringComparison.Ordinal) &&
            int.TryParse(heldTool.ToolVariant, out int size) &&
            size == (int)tool.Size;

        private bool TryGetFastener(out FastenerInstance fastener)
        {
            fastener = null;
            if (controller == null ||
                !controller.Graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount) ||
                !mount.IsOccupied)
            {
                return false;
            }

            return mount.TryGetFastener(fastenerDefinitionId, out fastener);
        }

        private static float EaseOutCubic(float value)
        {
            float shifted = value - 1f;
            return shifted * shifted * shifted + 1f;
        }

        private static float EaseOutQuart(float value)
        {
            float shifted = value - 1f;
            return 1f - shifted * shifted * shifted * shifted;
        }

        private static float EaseInQuart(float value) =>
            value * value * value * value;

        private static float EaseInOutQuart(float value)
        {
            return value < 0.5f
                ? 8f * value * value * value * value
                : 1f - Mathf.Pow(-2f * value + 2f, 4f) * 0.5f;
        }
    }
}
