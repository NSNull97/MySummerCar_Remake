using System;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Items
{
    public enum ItemActionKind
    {
        Used = 0,
        Opened = 1,
        Closed = 2,
        Enabled = 3,
        Disabled = 4,
        VariantChanged = 5,
        ChildDispensed = 6,
        LiquidTransferred = 7,
        Recovered = 8,
        PaintApplied = 9,
        LiquidFilled = 10,
        LiquidSpilled = 11,
        Ignited = 12,
        Burned = 13,
        ConsumptionStarted = 14,
        FreshnessChanged = 15,
        CookingChanged = 16,
        FuelTransferred = 17,
        Extinguished = 18,
        EmbersStarted = 19,
    }

    public readonly struct ItemActionCompleted
    {
        public ItemActionCompleted(
            ItemActionKind action,
            StableEntityId stableId,
            string definitionId,
            float remainingContent,
            ItemUseEffectDelta useEffects = default,
            float affectedAmount = 0f,
            string liquidId = "")
        {
            Action = action;
            StableId = stableId;
            DefinitionId = definitionId ?? string.Empty;
            RemainingContent = remainingContent;
            UseEffects = useEffects;
            AffectedAmount = affectedAmount;
            LiquidId = liquidId ?? string.Empty;
        }

        public ItemActionKind Action { get; }
        public StableEntityId StableId { get; }
        public string DefinitionId { get; }
        public float RemainingContent { get; }
        public ItemUseEffectDelta UseEffects { get; }
        public float AffectedAmount { get; }
        public string LiquidId { get; }
    }

    public enum ItemHeldUsePhase
    {
        Started = 0,
        Ended = 1,
    }

    /// <summary>
    /// Presentation-only lifecycle of a continuous action on a real carried
    /// item. Mutable gameplay state remains authoritative in the item instance.
    /// </summary>
    public readonly struct ItemHeldUseStateChanged
    {
        public ItemHeldUseStateChanged(
            ItemHeldUsePhase phase,
            StableEntityId stableId,
            string definitionId)
        {
            Phase = phase;
            StableId = stableId;
            DefinitionId = definitionId ?? string.Empty;
        }

        public ItemHeldUsePhase Phase { get; }
        public StableEntityId StableId { get; }
        public string DefinitionId { get; }
    }

    /// <summary>
    /// Vendor-neutral result of consuming one authored item portion. The 09B
    /// item domain publishes it without depending on the 09C needs owner.
    /// </summary>
    public readonly struct ItemUseEffectDelta
    {
        public ItemUseEffectDelta(
            float hunger,
            float thirst,
            float stress,
            float weight,
            float intoxication = 0f,
            float urine = 0f,
            float fatigue = 0f,
            float dirtiness = 0f)
        {
            Hunger = hunger;
            Thirst = thirst;
            Stress = stress;
            Weight = weight;
            Intoxication = intoxication;
            Urine = urine;
            Fatigue = fatigue;
            Dirtiness = dirtiness;
        }

        public float Hunger { get; }
        public float Thirst { get; }
        public float Stress { get; }
        public float Weight { get; }
        public float Intoxication { get; }
        public float Urine { get; }
        public float Fatigue { get; }
        public float Dirtiness { get; }

        public bool HasAnyEffect =>
            Mathf.Abs(Hunger) > 0.0001f ||
            Mathf.Abs(Thirst) > 0.0001f ||
            Mathf.Abs(Stress) > 0.0001f ||
            Mathf.Abs(Weight) > 0.0001f ||
            Mathf.Abs(Intoxication) > 0.0001f ||
            Mathf.Abs(Urine) > 0.0001f ||
            Mathf.Abs(Fatigue) > 0.0001f ||
            Mathf.Abs(Dirtiness) > 0.0001f;

        public ItemUseEffectDelta Scaled(float scale)
        {
            if (!float.IsFinite(scale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scale),
                    "Item effect scale must be finite.");
            }

            return new ItemUseEffectDelta(
                Hunger * scale,
                Thirst * scale,
                Stress * scale,
                Weight * scale,
                Intoxication * scale,
                Urine * scale,
                Fatigue * scale,
                Dirtiness * scale);
        }
    }

    public interface IItemStatusSource
    {
        string DefinitionId { get; }
        StableEntityId StableId { get; }
        string StatusText { get; }
        event Action<IItemStatusSource> StatusChanged;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(StableEntityIdAuthoring))]
    public sealed class WorldItemInstance : MonoBehaviour,
        IInteractionDisplayTarget,
        IInteractionLocalizationTarget,
        IContextInteractionTarget,
        IHeldActivationTarget,
        IContinuousHeldActivationTarget,
        IContinuousActivationPickupTarget,
        IHeldActivationReleaseRequest,
        IHeldTargetActivationSource,
        IHeldToolIdentity,
        ILiquidContainerTarget,
        IItemStatusSource
    {
        // The donor drink_rotate presentation lasts 5.866667 seconds. A
        // standard 0.33 litre bottle therefore empties at this rate while the
        // project-owned simulation remains independent from animation frames.
        private const float HeldDrinkLitresPerSecond = 0.05625f;
        private const float HeldDrinkFeedbackIntervalSeconds = 0.1f;

        private ItemWorldRuntime owner;
        private ItemDefinitionRecord definition;
        private StableEntityIdAuthoring identity;
        private PhysicsPickupTarget pickupTarget;
        private Rigidbody body;
        private ItemInstanceState state;
        private HeldActivationReleaseMode pendingHeldRelease;
        private bool continuousHeldActivationActive;
        private float continuousHeldFeedbackElapsed;
        private float continuousHeldConsumedAmount;
        private float pendingFoodConsumptionSeconds = -1f;

        private static readonly int BaseColorPropertyId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId =
            Shader.PropertyToID("_Color");

        public event Action<IItemStatusSource> StatusChanged;
        public event Action<WorldItemInstance> StateRestored;

        public ItemDefinitionRecord Definition => definition;
        public GameObject PresentationRoot { get; private set; }
        public string DefinitionId => definition?.DefinitionId ?? string.Empty;
        public StableEntityId StableId =>
            identity != null && identity.TryGetStableId(out StableEntityId id)
                ? id
                : default;
        public ItemInstanceState State => state?.DeepClone();
        public bool IsOpen => state?.isOpen == true;
        public bool IsEmpty => state == null || state.IsEmpty;
        public float ContentAmount => state?.content ?? 0f;
        public float LiquidAmountLitres => state?.content ?? 0f;
        public string LiquidId => state?.liquidId ?? string.Empty;
        public string InteractionDisplayName => definition?.DisplayName ?? string.Empty;
        public string InteractionLocalizationKey => DefinitionId;
        public string InteractionPrompt => BuildInteractionPrompt();
        public string StatusText => BuildStatusText();
        public string ActiveToolType => definition?.ToolType ?? string.Empty;
        public string ActiveToolVariant =>
            definition != null && definition.ToolVariants.Count > 0
                ? definition.ToolVariants[Mathf.Clamp(
                    state?.variantIndex ?? 0,
                    0,
                    definition.ToolVariants.Count - 1)]
                : string.Empty;
        public string ToolType => ActiveToolType;
        public string ToolVariant => ActiveToolVariant;
        public bool IsContinuousHeldActivationActive =>
            continuousHeldActivationActive;
        public bool IsIgnited => state != null && state.isEnabled &&
            (definition?.PrimaryAction == ItemPrimaryAction.Ignite ||
             definition?.Combustion.IsConfigured == true &&
             RemainingBurnTime > 0.0001f);
        public bool IsEnabled => state?.isEnabled == true;
        public bool IsCombusting =>
            definition?.Combustion.IsConfigured == true &&
            state?.isEnabled == true;
        public bool IsEmbering => IsCombusting && !IsIgnited;
        public float RemainingBurnTime =>
            definition?.Combustion.IsConfigured == true &&
            TryGetScalar(
                definition.Combustion.BurnTimeStateId,
                out float remaining)
                ? remaining
                : 0f;
        public bool CanToggleOpen =>
            owner != null && definition?.CanOpen == true && state != null &&
            !state.isBroken && !state.isConsumed &&
            pendingFoodConsumptionSeconds < 0f;
        public bool IsFood => definition?.Food.IsConfigured == true;
        public float Freshness => state?.condition ?? 0f;
        public float CookingSeconds => state?.cookingSeconds ?? 0f;
        public FoodCookState CookState => definition?.Food.ResolveCookState(
            state?.cookingSeconds ?? 0f) ?? FoodCookState.Raw;
        public bool IsSpoiled => definition?.Food.Perishable == true &&
                                 (state?.condition ?? 0f) <= 0.0001f;

        public bool CanInteract(in InteractionContext context) =>
            CanPerformPrimaryAction() &&
            pendingFoodConsumptionSeconds < 0f &&
            !SupportsContinuousHeldConsumption();

        public void Interact(in InteractionContext context)
        {
            TryPerformPrimaryAction(context);
        }

        public bool CanActivateHeld(in InteractionContext context) =>
            (CanPerformPrimaryAction() &&
             pendingFoodConsumptionSeconds < 0f &&
             !SupportsContinuousHeldConsumption()) ||
            CanSpillLiquid();

        public void ActivateHeld(in InteractionContext context)
        {
            if (CanSpillLiquid())
            {
                TrySpillLiquid(
                    Mathf.Max(0.0001f, definition.UseAmount),
                    out _);
                return;
            }

            if (!TryPerformPrimaryAction(context) ||
                definition.PrimaryAction != ItemPrimaryAction.Consume ||
                state.content > 0.0001f)
            {
                return;
            }

            pendingHeldRelease = definition.RetainWhenEmpty
                ? HeldActivationReleaseMode.Throw
                : HeldActivationReleaseMode.Release;
        }

        public HeldActivationReleaseMode ConsumeHeldActivationReleaseRequest()
        {
            HeldActivationReleaseMode request = pendingHeldRelease;
            pendingHeldRelease = HeldActivationReleaseMode.None;
            return request;
        }

        public bool CanBeginContinuousHeldActivation(
            in InteractionContext context)
        {
            return CanUseCurrentState() &&
                   SupportsContinuousHeldConsumption() &&
                   state.content > 0.0001f;
        }

        public bool CanPickupForContinuousActivation(
            in InteractionContext context)
        {
            return CanBeginContinuousHeldActivation(context);
        }

        public void BeginContinuousHeldActivation(
            in InteractionContext context)
        {
            if (!CanBeginContinuousHeldActivation(context))
            {
                return;
            }

            pendingHeldRelease = HeldActivationReleaseMode.None;
            continuousHeldFeedbackElapsed = 0f;
            continuousHeldConsumedAmount = 0f;
            continuousHeldActivationActive = true;
            owner?.PublishHeldUseState(new ItemHeldUseStateChanged(
                ItemHeldUsePhase.Started,
                StableId,
                DefinitionId));
        }

        public bool ContinueContinuousHeldActivation(
            float unscaledDeltaTime)
        {
            if (!continuousHeldActivationActive ||
                !float.IsFinite(unscaledDeltaTime) ||
                unscaledDeltaTime < 0f)
            {
                return false;
            }

            if (unscaledDeltaTime <= 0f)
            {
                return state.content > 0.0001f;
            }

            float consumedAmount = Mathf.Min(
                state.content,
                HeldDrinkLitresPerSecond * unscaledDeltaTime);
            if (consumedAmount <= 0.000001f)
            {
                return false;
            }

            state.content -= consumedAmount;
            continuousHeldConsumedAmount += consumedAmount;
            continuousHeldFeedbackElapsed += unscaledDeltaTime;
            bool depleted = state.content <= 0.0001f;
            if (depleted)
            {
                state.content = 0f;
                state.liquidId = string.Empty;
            }

            if (continuousHeldFeedbackElapsed >=
                    HeldDrinkFeedbackIntervalSeconds ||
                depleted)
            {
                FlushContinuousHeldConsumption();
            }

            return !depleted;
        }

        public void EndContinuousHeldActivation()
        {
            StopContinuousHeldActivation(flushEffects: true);
        }

        public bool CanActivateHeldOn(
            IContextInteractionTarget target,
            in InteractionContext context)
        {
            if (target == null)
            {
                return false;
            }

            if (!CanHandleLiquid())
            {
                return false;
            }

            float transferAmount = Mathf.Max(0.0001f, definition.UseAmount);
            float capacityRemaining = Mathf.Max(
                0f,
                definition.MaximumContent - state.content);
            if (target is ILiquidSourceTarget source &&
                capacityRemaining > 0.0001f &&
                source.CanProvideLiquid(
                    state.liquidId,
                    Mathf.Min(transferAmount, capacityRemaining),
                    context))
            {
                return true;
            }

            if (target is ILiquidReceiverTarget receiver &&
                state.content > 0.0001f &&
                !string.IsNullOrEmpty(state.liquidId) &&
                receiver.CanReceiveLiquid(
                    state.liquidId,
                    Mathf.Min(transferAmount, state.content),
                    context))
            {
                return true;
            }

            return target is WorldItemInstance targetItem &&
                   CanTransferLiquidTo(targetItem);
        }

        public void ActivateHeldOn(
            IContextInteractionTarget target,
            in InteractionContext context)
        {
            if (!CanHandleLiquid())
            {
                return;
            }

            float requestedLitres = Mathf.Max(
                0.0001f,
                definition.UseAmount);
            if (target is ILiquidSourceTarget source)
            {
                float capacityRemaining = Mathf.Max(
                    0f,
                    definition.MaximumContent - state.content);
                float requestedFill = Mathf.Min(
                    requestedLitres,
                    capacityRemaining);
                if (source.TryProvideLiquid(
                        state.liquidId,
                        requestedFill,
                        context,
                        out string liquidId,
                        out float providedLitres))
                {
                    TryFillLiquid(liquidId, providedLitres, out _);
                }

                return;
            }

            if (target is ILiquidReceiverTarget receiver)
            {
                float offeredLitres = Mathf.Min(
                    requestedLitres,
                    state.content);
                if (receiver.TryReceiveLiquid(
                        state.liquidId,
                        offeredLitres,
                        context,
                        out float acceptedLitres))
                {
                    TryRemoveLiquid(
                        acceptedLitres,
                        ItemActionKind.LiquidTransferred,
                        out _);
                }

                return;
            }

            if (target is WorldItemInstance targetItem)
            {
                TryTransferLiquidTo(
                    targetItem,
                    requestedLitres,
                    out _);
            }
        }

        public bool TryPerformPrimaryAction(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return false;
            }

            switch (definition.PrimaryAction)
            {
                case ItemPrimaryAction.Consume:
                    return owner.TryConsume(this);

                case ItemPrimaryAction.ToggleOpen:
                    return TryToggleOpen();

                case ItemPrimaryAction.ToggleDevice:
                    state.isEnabled = !state.isEnabled;
                    PublishStateChanged(
                        state.isEnabled
                            ? ItemActionKind.Enabled
                            : ItemActionKind.Disabled);
                    return true;

                case ItemPrimaryAction.CycleVariant:
                    if (definition.ToolVariants.Count == 0)
                    {
                        return false;
                    }

                    state.variantIndex =
                        (state.variantIndex + 1) % definition.ToolVariants.Count;
                    PublishStateChanged(ItemActionKind.VariantChanged);
                    return true;

                case ItemPrimaryAction.DispenseChild:
                    return owner.TryDispenseChild(this, context);

                case ItemPrimaryAction.ConsumeContainedChild:
                    return owner.TryConsumeContainedChild(this, context);

                case ItemPrimaryAction.Ignite:
                    if (state.isEnabled)
                    {
                        return false;
                    }

                    state.isEnabled = true;
                    PublishStateChanged(ItemActionKind.Ignited);
                    return true;

                case ItemPrimaryAction.IgniteFuel:
                    return TryIgniteFuel();

                default:
                    return false;
            }
        }

        /// <summary>
        /// Toggles an authored cover independently from the item's primary
        /// action. A portable grill can therefore keep ToggleDevice on its
        /// body while the aimed cover exposes its own ordinary F interaction.
        /// </summary>
        public bool TryToggleOpen()
        {
            if (!CanToggleOpen)
            {
                return false;
            }

            state.isOpen = !state.isOpen;
            PublishStateChanged(
                state.isOpen
                    ? ItemActionKind.Opened
                    : ItemActionKind.Closed);
            return true;
        }

        public bool TryTransferLiquidTo(
            WorldItemInstance target,
            float requestedLitres,
            out float transferredLitres)
        {
            transferredLitres = 0f;
            if (target == null || target == this ||
                definition == null || target.definition == null ||
                state == null || target.state == null ||
                !definition.SupportsLiquidTransfer ||
                !target.definition.SupportsLiquidTransfer ||
                state.isBroken || state.isConsumed ||
                target.state.isBroken || target.state.isConsumed ||
                requestedLitres <= 0f ||
                !float.IsFinite(requestedLitres) ||
                state.content <= 0f ||
                string.IsNullOrEmpty(state.liquidId) ||
                definition.CanOpen && !state.isOpen ||
                target.definition.CanOpen && !target.state.isOpen ||
                (!string.IsNullOrEmpty(target.state.liquidId) &&
                 !string.Equals(
                     target.state.liquidId,
                     state.liquidId,
                     StringComparison.Ordinal)))
            {
                return false;
            }

            float capacityRemaining = Mathf.Max(
                0f,
                target.definition.MaximumContent - target.state.content);
            transferredLitres = Mathf.Min(
                requestedLitres,
                state.content,
                capacityRemaining);
            if (transferredLitres <= 0.0001f)
            {
                transferredLitres = 0f;
                return false;
            }

            target.state.liquidId = state.liquidId;
            target.state.content += transferredLitres;
            state.content -= transferredLitres;
            if (state.content <= 0.0001f)
            {
                state.content = 0f;
                state.liquidId = string.Empty;
            }

            PublishStateChanged(ItemActionKind.LiquidTransferred);
            target.PublishStateChanged(ItemActionKind.LiquidTransferred);
            return true;
        }

        public bool TryTransferFuelTo(
            WorldItemInstance target,
            float requestedAmount,
            out float transferredAmount)
        {
            transferredAmount = 0f;
            if (!CanTransferFuelTo(target) ||
                !float.IsFinite(requestedAmount) || requestedAmount <= 0f)
            {
                return false;
            }

            float capacityRemaining = Mathf.Max(
                0f,
                target.definition.MaximumContent - target.state.content);
            transferredAmount = Mathf.Min(
                requestedAmount,
                state.content,
                capacityRemaining);
            if (transferredAmount <= 0.0001f)
            {
                transferredAmount = 0f;
                return false;
            }

            state.content -= transferredAmount;
            target.state.content += transferredAmount;
            if (state.content <= 0.0001f)
            {
                state.content = 0f;
                if (!definition.RetainWhenEmpty)
                {
                    state.isConsumed = true;
                    pendingHeldRelease = HeldActivationReleaseMode.Release;
                }
            }

            PublishStateChanged(
                ItemActionKind.FuelTransferred,
                affectedAmount: transferredAmount);
            target.PublishStateChanged(
                ItemActionKind.FuelTransferred,
                affectedAmount: transferredAmount);
            ApplyConsumedPresentationState();
            return true;
        }

        public bool TryIgniteFuel()
        {
            if (!CanIgniteFuel())
            {
                return false;
            }

            ItemScalarState burnTime = FindScalarState(
                definition.Combustion.BurnTimeStateId);
            if (burnTime == null)
            {
                return false;
            }

            burnTime.value = definition.Combustion.ActiveDurationSeconds;
            state.isEnabled = true;
            PublishStateChanged(ItemActionKind.Ignited);
            return true;
        }

        public bool TryFillLiquid(
            string liquidId,
            float requestedLitres,
            out float filledLitres)
        {
            filledLitres = 0f;
            if (!CanHandleLiquid() ||
                string.IsNullOrWhiteSpace(liquidId) ||
                requestedLitres <= 0f ||
                !float.IsFinite(requestedLitres) ||
                (!string.IsNullOrEmpty(state.liquidId) &&
                 !string.Equals(
                     state.liquidId,
                     liquidId,
                     StringComparison.Ordinal)))
            {
                return false;
            }

            filledLitres = Mathf.Min(
                requestedLitres,
                Mathf.Max(0f, definition.MaximumContent - state.content));
            if (filledLitres <= 0.0001f)
            {
                filledLitres = 0f;
                return false;
            }

            state.liquidId = liquidId.Trim();
            state.content += filledLitres;
            PublishStateChanged(ItemActionKind.LiquidFilled);
            return true;
        }

        public bool CanAcceptLiquid(
            string liquidId,
            float requestedLitres)
        {
            return CanHandleLiquid() &&
                   !string.IsNullOrWhiteSpace(liquidId) &&
                   float.IsFinite(requestedLitres) &&
                   requestedLitres > 0f &&
                   state.content < definition.MaximumContent - 0.0001f &&
                   (string.IsNullOrEmpty(state.liquidId) ||
                    string.Equals(
                        state.liquidId,
                        liquidId,
                        StringComparison.Ordinal));
        }

        public bool TryAcceptLiquid(
            string liquidId,
            float requestedLitres,
            out float acceptedLitres) =>
            TryFillLiquid(
                liquidId,
                requestedLitres,
                out acceptedLitres);

        public bool TrySpillLiquid(
            float requestedLitres,
            out float spilledLitres) =>
            TryRemoveLiquid(
                requestedLitres,
                ItemActionKind.LiquidSpilled,
                out spilledLitres);

        public bool TryBurnInGarbageBarrel()
        {
            if (definition == null || state == null ||
                state.isConsumed || state.isBroken ||
                definition.CriticalRecovery ||
                definition.PrimaryAction == ItemPrimaryAction.Ignite ||
                definition.Combustion.IsConfigured ||
                definition.HeatSource.ProvidesCookingHeat)
            {
                return false;
            }

            state.isConsumed = true;
            state.isEnabled = false;
            state.content = 0f;
            state.liquidId = string.Empty;
            state.containedStableIds = Array.Empty<string>();
            PublishStateChanged(ItemActionKind.Burned);
            ApplyConsumedPresentationState();
            return true;
        }

        public bool TryGetScalar(string stateId, out float value)
        {
            ItemScalarState scalar = state?.scalarStates?.FirstOrDefault(
                candidate => string.Equals(
                    candidate.stateId,
                    stateId,
                    StringComparison.Ordinal));
            if (scalar != null)
            {
                value = scalar.value;
                return true;
            }

            value = 0f;
            return false;
        }

        public bool TrySetScalar(string stateId, float value)
        {
            if (!float.IsFinite(value) || definition == null || state == null)
            {
                return false;
            }

            ItemScalarDefinition stateDefinition =
                definition.ScalarStates.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.StateId,
                        stateId,
                        StringComparison.Ordinal));
            ItemScalarState scalar = state.scalarStates.FirstOrDefault(
                candidate => string.Equals(
                    candidate.stateId,
                    stateId,
                    StringComparison.Ordinal));
            if (stateDefinition == null || scalar == null ||
                value < stateDefinition.Minimum ||
                value > stateDefinition.Maximum)
            {
                return false;
            }

            scalar.value = value;
            PublishStateChanged(ItemActionKind.Used);
            return true;
        }

        public bool TryGetFlag(string stateId, out bool value)
        {
            ItemFlagState flag = state?.flagStates?.FirstOrDefault(
                candidate => string.Equals(
                    candidate.stateId,
                    stateId,
                    StringComparison.Ordinal));
            if (flag != null)
            {
                value = flag.value;
                return true;
            }

            value = false;
            return false;
        }

        public bool TrySetFlag(string stateId, bool value)
        {
            ItemFlagState flag = state?.flagStates?.FirstOrDefault(
                candidate => string.Equals(
                    candidate.stateId,
                    stateId,
                    StringComparison.Ordinal));
            if (flag == null)
            {
                return false;
            }

            flag.value = value;
            PublishStateChanged(ItemActionKind.Used);
            return true;
        }

        public bool TryGetPaint(
            out Color color,
            out bool matte,
            out bool applied)
        {
            color = Color.white;
            matte = false;
            applied = false;
            float red = 0f;
            float green = 0f;
            float blue = 0f;
            bool valid = TryGetScalar(ItemPaintStateIds.ColorRed, out red) &&
                         TryGetScalar(ItemPaintStateIds.ColorGreen, out green) &&
                         TryGetScalar(ItemPaintStateIds.ColorBlue, out blue) &&
                         TryGetFlag(ItemPaintStateIds.Matte, out matte) &&
                         TryGetFlag(ItemPaintStateIds.Applied, out applied);
            if (valid)
            {
                color = new Color(red, green, blue, 1f);
            }

            return valid;
        }

        public bool TryApplyPaint(Color color, bool matte)
        {
            if (definition == null || state == null ||
                !float.IsFinite(color.r) || !float.IsFinite(color.g) ||
                !float.IsFinite(color.b))
            {
                return false;
            }

            ItemScalarState red = FindScalarState(ItemPaintStateIds.ColorRed);
            ItemScalarState green = FindScalarState(ItemPaintStateIds.ColorGreen);
            ItemScalarState blue = FindScalarState(ItemPaintStateIds.ColorBlue);
            ItemFlagState applied = FindFlagState(ItemPaintStateIds.Applied);
            ItemFlagState matteState = FindFlagState(ItemPaintStateIds.Matte);
            if (red == null || green == null || blue == null ||
                applied == null || matteState == null)
            {
                return false;
            }

            red.value = Mathf.Clamp01(color.r);
            green.value = Mathf.Clamp01(color.g);
            blue.value = Mathf.Clamp01(color.b);
            applied.value = true;
            matteState.value = matte;
            PublishStateChanged(ItemActionKind.PaintApplied);
            return true;
        }

        private ItemScalarState FindScalarState(string stateId) =>
            state?.scalarStates?.FirstOrDefault(candidate => string.Equals(
                candidate.stateId,
                stateId,
                StringComparison.Ordinal));

        private ItemFlagState FindFlagState(string stateId) =>
            state?.flagStates?.FirstOrDefault(candidate => string.Equals(
                candidate.stateId,
                stateId,
                StringComparison.Ordinal));

        public ItemInstanceState CaptureState()
        {
            // Consumption is applied to the physical container every tick,
            // while authored need effects are batched briefly. Flush the
            // pending proportional dose at the save boundary so a save made
            // mid-sip cannot restore less liquid without the matching effects.
            if (continuousHeldActivationActive)
            {
                FlushContinuousHeldConsumption();
            }

            string failure = string.Empty;
            if (definition == null || state == null ||
                !state.TryValidate(definition, out failure))
            {
                throw new InvalidOperationException(
                    "Cannot capture item state: " + failure);
            }

            return state.DeepClone();
        }

        public void ApplyState(
            ItemInstanceState restored,
            Vector3? restoredWorldPosition = null)
        {
            string failure = string.Empty;
            if (restored == null || definition == null ||
                !restored.TryValidate(definition, out failure) ||
                !string.Equals(
                    restored.stableEntityId,
                    StableId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Cannot restore item state: " + failure);
            }

            StopContinuousHeldActivation(flushEffects: false);
            pendingFoodConsumptionSeconds = -1f;
            state = restored.DeepClone();
            owner?.ReconcileFoodAfterRestore(
                this,
                restoredWorldPosition ?? transform.position);
            RefreshDynamicMass();
            RefreshFoodPresentation();
            ApplyConsumedPresentationState();
            UpdatePickupPrompt();
            StatusChanged?.Invoke(this);
            StateRestored?.Invoke(this);
        }

        internal void Configure(
            ItemWorldRuntime configuredOwner,
            ItemDefinitionRecord configuredDefinition,
            StableEntityIdAuthoring configuredIdentity,
            int initialVariantIndex)
        {
            owner = configuredOwner ??
                throw new ArgumentNullException(nameof(configuredOwner));
            definition = configuredDefinition ??
                throw new ArgumentNullException(nameof(configuredDefinition));
            identity = configuredIdentity ??
                throw new ArgumentNullException(nameof(configuredIdentity));
            pickupTarget = GetComponent<PhysicsPickupTarget>();
            body = GetComponent<Rigidbody>();
            if (!identity.TryGetStableId(out StableEntityId stableId))
            {
                throw new InvalidOperationException(
                    "World item has no explicit project-owned stable identity.");
            }

            state = ItemInstanceState.CreateInitial(
                definition,
                stableId,
                initialVariantIndex);
            if (definition.Food.IsConfigured)
            {
                state.lastFoodSimulationGameSeconds =
                    owner.CurrentGameTimeSeconds;
                state.foodSimulationInitialized = true;
            }
            RefreshDynamicMass();
            UpdatePickupPrompt();
        }

        internal void SetPresentationRoot(GameObject configuredRoot)
        {
            PresentationRoot = configuredRoot;
            RefreshFoodPresentation();
        }

        internal ItemWorldRuntime RuntimeOwner => owner;

        internal void InheritFoodStateFrom(WorldItemInstance source)
        {
            if (source?.state == null || state == null ||
                !definition.Food.IsConfigured)
            {
                return;
            }

            state.condition = Mathf.Clamp(source.state.condition, 0f, 100f);
            state.lastFoodSimulationGameSeconds =
                source.state.lastFoodSimulationGameSeconds;
            state.foodSimulationInitialized =
                source.state.foodSimulationInitialized;
            RefreshFoodPresentation();
            StatusChanged?.Invoke(this);
        }

        internal bool AdvanceFreshness(
            double elapsedGameSeconds,
            bool refrigerated,
            double currentGameSeconds,
            bool publish = true)
        {
            if (state == null || definition == null ||
                !definition.Food.Perishable || state.isConsumed ||
                !double.IsFinite(elapsedGameSeconds) ||
                elapsedGameSeconds <= 0d ||
                !double.IsFinite(currentGameSeconds) ||
                currentGameSeconds < 0d)
            {
                return false;
            }

            float rate = refrigerated
                ? definition.Food.RefrigeratedDecayPerGameMinute
                : definition.Food.AmbientDecayPerGameMinute;
            float previous = state.condition;
            state.condition = Mathf.Max(
                0f,
                state.condition - (float)(elapsedGameSeconds / 60d) * rate);
            state.lastFoodSimulationGameSeconds = currentGameSeconds;
            state.foodSimulationInitialized = true;
            if (Mathf.Approximately(previous, state.condition))
            {
                return false;
            }

            RefreshFoodPresentation();
            if (publish)
            {
                PublishStateChanged(ItemActionKind.FreshnessChanged);
            }
            else
            {
                StatusChanged?.Invoke(this);
            }
            return true;
        }

        internal bool AdvanceCooking(float elapsedRealSeconds, float rate)
        {
            if (state == null || definition == null ||
                !definition.Food.Cookable || state.isConsumed ||
                !float.IsFinite(elapsedRealSeconds) || elapsedRealSeconds <= 0f ||
                !float.IsFinite(rate) || rate <= 0f)
            {
                return false;
            }

            float previous = state.cookingSeconds;
            FoodCookState previousState =
                definition.Food.ResolveCookState(previous);
            state.cookingSeconds = Mathf.Min(
                definition.Food.MaximumCookingSeconds,
                state.cookingSeconds + elapsedRealSeconds * rate);
            if (Mathf.Approximately(previous, state.cookingSeconds))
            {
                return false;
            }

            FoodCookState currentState =
                definition.Food.ResolveCookState(state.cookingSeconds);
            if (currentState != previousState)
            {
                RefreshFoodPresentation();
                PublishStateChanged(ItemActionKind.CookingChanged);
            }
            else
            {
                StatusChanged?.Invoke(this);
            }

            return true;
        }

        internal bool AdvanceCombustion(float elapsedRealSeconds)
        {
            if (state == null || definition?.Combustion.IsConfigured != true ||
                !state.isEnabled || state.isConsumed || state.isBroken ||
                !float.IsFinite(elapsedRealSeconds) || elapsedRealSeconds <= 0f)
            {
                return false;
            }

            ItemCombustionDefinition combustion = definition.Combustion;
            ItemScalarState burnTime = FindScalarState(
                combustion.BurnTimeStateId);
            ItemFlagState wet = FindFlagState(combustion.WetStateId);
            if (burnTime == null || wet == null)
            {
                return false;
            }

            if (wet.value ||
                state.content <= combustion.ExtinguishFuelThreshold + 0.0001f)
            {
                state.content = Mathf.Max(0f, state.content);
                burnTime.value = 0f;
                state.isEnabled = false;
                PublishStateChanged(ItemActionKind.Extinguished);
                return true;
            }

            float remainingStep = elapsedRealSeconds;
            bool enteredEmbers = false;
            while (remainingStep > 0.000001f && state.isEnabled)
            {
                bool activeFlame = burnTime.value > 0.0001f;
                float rate = activeFlame
                    ? combustion.ActiveFuelPerSecond
                    : combustion.EmberFuelPerSecond;
                float fuelSeconds = Mathf.Max(
                    0f,
                    (state.content - combustion.ExtinguishFuelThreshold) /
                    rate);
                float phaseSeconds = activeFlame
                    ? burnTime.value
                    : float.PositiveInfinity;
                float simulated = Mathf.Min(
                    remainingStep,
                    fuelSeconds,
                    phaseSeconds);

                if (simulated <= 0.000001f)
                {
                    state.content = Mathf.Max(
                        state.content,
                        combustion.ExtinguishFuelThreshold);
                    burnTime.value = 0f;
                    state.isEnabled = false;
                    break;
                }

                state.content = Mathf.Max(
                    combustion.ExtinguishFuelThreshold,
                    state.content - rate * simulated);
                if (activeFlame)
                {
                    burnTime.value = Mathf.Max(
                        0f,
                        burnTime.value - simulated);
                }

                remainingStep -= simulated;
                if (state.content <=
                    combustion.ExtinguishFuelThreshold + 0.0001f)
                {
                    burnTime.value = 0f;
                    state.isEnabled = false;
                    break;
                }

                if (activeFlame && burnTime.value <= 0.0001f)
                {
                    burnTime.value = 0f;
                    enteredEmbers = true;
                }
            }

            if (!state.isEnabled)
            {
                PublishStateChanged(ItemActionKind.Extinguished);
            }
            else if (enteredEmbers)
            {
                PublishStateChanged(ItemActionKind.EmbersStarted);
            }
            else
            {
                RefreshDynamicMass();
                StatusChanged?.Invoke(this);
            }

            return true;
        }

        internal bool TryTakeContainedIdentity(out StableEntityId childId)
        {
            childId = default;
            if (state?.containedStableIds == null ||
                state.containedStableIds.Length == 0 ||
                !StableEntityId.TryParse(
                    state.containedStableIds[0],
                    out childId))
            {
                return false;
            }

            state.containedStableIds = state.containedStableIds
                .Skip(1)
                .ToArray();
            state.content = Mathf.Max(0f, state.content - 1f);
            PublishStateChanged(ItemActionKind.ChildDispensed);
            return true;
        }

        internal bool TryPeekContainedIdentity(out StableEntityId childId)
        {
            childId = default;
            return state?.containedStableIds != null &&
                   state.containedStableIds.Length > 0 &&
                   StableEntityId.TryParse(
                       state.containedStableIds[0],
                       out childId);
        }

        internal bool TryConsumeContent(out bool depleted)
        {
            depleted = false;
            if (state == null || state.content <= 0f)
            {
                return false;
            }

            if (definition.Food.Edible)
            {
                if (pendingFoodConsumptionSeconds >= 0f)
                {
                    return false;
                }

                float duration = Application.isPlaying
                    ? definition.Food.ConsumptionDurationSeconds
                    : 0f;
                if (duration > 0.0001f)
                {
                    pendingFoodConsumptionSeconds = duration;
                    PublishStateChanged(ItemActionKind.ConsumptionStarted);
                    return true;
                }

                return CompleteFoodConsumption(out depleted);
            }

            state.content = Mathf.Max(
                0f,
                state.content - Mathf.Max(0.0001f, definition.UseAmount));
            depleted = state.content <= 0.0001f;
            if (depleted)
            {
                state.content = 0f;
                state.liquidId = string.Empty;
                if (!definition.RetainWhenEmpty)
                {
                    state.isConsumed = true;
                }
            }

            PublishStateChanged(
                ItemActionKind.Used,
                new ItemUseEffectDelta(
                    definition.HungerEffect,
                    definition.ThirstEffect,
                    definition.StressEffect,
                    definition.WeightEffect,
                    definition.IntoxicationEffect,
                    definition.UrineEffect,
                    definition.FatigueEffect,
                    definition.DirtinessEffect));
            ApplyConsumedPresentationState();
            return true;
        }

        private bool CompleteFoodConsumption(out bool depleted)
        {
            depleted = false;
            if (state == null || definition == null ||
                !definition.Food.Edible || state.content <= 0f)
            {
                return false;
            }

            ItemUseEffectDelta resolvedEffects =
                definition.Food.ResolveEffects(
                    CreateUseEffectDelta(1f),
                    state.condition,
                    state.cookingSeconds,
                    1f);
            float amount = definition.Food.ConsumeWhole
                ? state.content
                : Mathf.Max(0.0001f, definition.UseAmount);
            state.content = Mathf.Max(0f, state.content - amount);
            depleted = state.content <= 0.0001f;
            if (depleted)
            {
                state.content = 0f;
                state.liquidId = string.Empty;
                if (!definition.RetainWhenEmpty ||
                    definition.Food.ConsumeWhole)
                {
                    state.isConsumed = true;
                }
            }

            pendingFoodConsumptionSeconds = -1f;
            PublishStateChanged(ItemActionKind.Used, resolvedEffects);
            ApplyConsumedPresentationState();
            return true;
        }

        internal void NotifyRecovered()
        {
            PublishStateChanged(ItemActionKind.Recovered);
        }

        private void OnDestroy()
        {
            StopContinuousHeldActivation(flushEffects: true);
            owner?.NotifyDestroyed(this);
        }

        private void Update()
        {
            if (pendingFoodConsumptionSeconds < 0f)
            {
                return;
            }

            pendingFoodConsumptionSeconds -= Time.deltaTime;
            if (pendingFoodConsumptionSeconds <= 0f)
            {
                CompleteFoodConsumption(out _);
            }
        }

        private bool CanUseCurrentState()
        {
            return owner != null && definition != null && state != null &&
                   !state.isBroken && !state.isConsumed &&
                   definition.PrimaryAction != ItemPrimaryAction.None &&
                   !((definition.PrimaryAction == ItemPrimaryAction.Ignite ||
                      definition.PrimaryAction ==
                          ItemPrimaryAction.IgniteFuel) &&
                     state.isEnabled);
        }

        private bool CanPerformPrimaryAction()
        {
            if (!CanUseCurrentState())
            {
                return false;
            }

            return definition.PrimaryAction switch
            {
                ItemPrimaryAction.Consume => state.content > 0.0001f,
                ItemPrimaryAction.ToggleOpen => CanToggleOpen,
                ItemPrimaryAction.ToggleDevice => true,
                ItemPrimaryAction.CycleVariant =>
                    definition.ToolVariants.Count > 0,
                ItemPrimaryAction.DispenseChild =>
                    state.containedStableIds?.Length > 0 &&
                    !string.IsNullOrEmpty(definition.ChildDefinitionId),
                ItemPrimaryAction.ConsumeContainedChild =>
                    state.containedStableIds?.Length > 0 &&
                    !string.IsNullOrEmpty(definition.ChildDefinitionId),
                ItemPrimaryAction.Ignite => true,
                ItemPrimaryAction.IgniteFuel => CanIgniteFuel(),
                _ => false,
            };
        }

        private bool CanIgniteFuel()
        {
            return definition?.Combustion.IsConfigured == true &&
                   state != null &&
                   !state.isBroken &&
                   !state.isConsumed &&
                   !state.isEnabled &&
                   state.content >
                       definition.Combustion.MinimumFuelToIgnite &&
                   TryGetFlag(
                       definition.Combustion.WetStateId,
                       out bool wet) &&
                   !wet &&
                   FindScalarState(
                       definition.Combustion.BurnTimeStateId) != null;
        }

        private bool CanHandleLiquid()
        {
            return owner != null && definition != null && state != null &&
                   definition.SupportsLiquidTransfer &&
                   !state.isBroken && !state.isConsumed &&
                   (!definition.CanOpen || state.isOpen);
        }

        private bool CanSpillLiquid()
        {
            return CanHandleLiquid() &&
                   definition.PrimaryAction == ItemPrimaryAction.None &&
                   state.content > 0.0001f &&
                   !string.IsNullOrEmpty(state.liquidId);
        }

        private bool CanTransferLiquidTo(WorldItemInstance target)
        {
            return target != null && target != this &&
                   target.definition != null && target.state != null &&
                   target.definition.SupportsLiquidTransfer &&
                   !target.state.isBroken && !target.state.isConsumed &&
                   state.content > 0.0001f &&
                   !string.IsNullOrEmpty(state.liquidId) &&
                   (!target.definition.CanOpen || target.state.isOpen) &&
                   (string.IsNullOrEmpty(target.state.liquidId) ||
                    string.Equals(
                        target.state.liquidId,
                        state.liquidId,
                        StringComparison.Ordinal)) &&
                   target.state.content < target.definition.MaximumContent;
        }

        private bool CanTransferFuelTo(WorldItemInstance target)
        {
            return owner != null && definition != null && state != null &&
                   target != null && target != this &&
                   target.owner != null && target.definition != null &&
                   target.state != null &&
                   target.definition.Combustion.IsConfigured &&
                   string.Equals(
                       target.definition.Combustion.AcceptedFuelDefinitionId,
                       definition.DefinitionId,
                       StringComparison.Ordinal) &&
                   !state.isBroken && !state.isConsumed &&
                   !target.state.isBroken && !target.state.isConsumed &&
                   !target.state.isEnabled &&
                   state.content > 0.0001f &&
                   target.state.content <
                       target.definition.MaximumContent - 0.0001f;
        }

        private bool TryRemoveLiquid(
            float requestedLitres,
            ItemActionKind action,
            out float removedLitres)
        {
            removedLitres = 0f;
            if (!CanHandleLiquid() ||
                requestedLitres <= 0f ||
                !float.IsFinite(requestedLitres) ||
                state.content <= 0.0001f ||
                string.IsNullOrEmpty(state.liquidId))
            {
                return false;
            }

            string removedLiquidId = state.liquidId;
            removedLitres = Mathf.Min(requestedLitres, state.content);
            state.content -= removedLitres;
            if (state.content <= 0.0001f)
            {
                state.content = 0f;
                state.liquidId = string.Empty;
            }

            PublishStateChanged(
                action,
                affectedAmount: removedLitres,
                liquidId: removedLiquidId);
            return true;
        }

        private bool SupportsContinuousHeldConsumption()
        {
            return definition != null &&
                   definition.PrimaryAction == ItemPrimaryAction.Consume &&
                   definition.ContentMeasure == ItemContentMeasure.Litres &&
                   definition.RetainWhenEmpty;
        }

        private void FlushContinuousHeldConsumption()
        {
            if (continuousHeldConsumedAmount <= 0.000001f ||
                definition == null)
            {
                continuousHeldFeedbackElapsed = 0f;
                continuousHeldConsumedAmount = 0f;
                return;
            }

            float authoredPortion = Mathf.Max(
                0.0001f,
                definition.UseAmount);
            float effectScale =
                continuousHeldConsumedAmount / authoredPortion;
            continuousHeldFeedbackElapsed = 0f;
            continuousHeldConsumedAmount = 0f;
            PublishStateChanged(
                ItemActionKind.Used,
                CreateUseEffectDelta(effectScale));
        }

        private void StopContinuousHeldActivation(bool flushEffects)
        {
            if (!continuousHeldActivationActive)
            {
                return;
            }

            if (flushEffects)
            {
                FlushContinuousHeldConsumption();
            }

            continuousHeldActivationActive = false;
            continuousHeldFeedbackElapsed = 0f;
            continuousHeldConsumedAmount = 0f;
            owner?.PublishHeldUseState(new ItemHeldUseStateChanged(
                ItemHeldUsePhase.Ended,
                StableId,
                DefinitionId));
        }

        private ItemUseEffectDelta CreateUseEffectDelta(float scale)
        {
            return new ItemUseEffectDelta(
                definition.HungerEffect * scale,
                definition.ThirstEffect * scale,
                definition.StressEffect * scale,
                definition.WeightEffect * scale,
                definition.IntoxicationEffect * scale,
                definition.UrineEffect * scale,
                definition.FatigueEffect * scale,
                definition.DirtinessEffect * scale);
        }

        private void PublishStateChanged(
            ItemActionKind action,
            ItemUseEffectDelta useEffects = default,
            float affectedAmount = 0f,
            string liquidId = "")
        {
            RefreshDynamicMass();
            UpdatePickupPrompt();
            StatusChanged?.Invoke(this);
            owner?.PublishAction(new ItemActionCompleted(
                action,
                StableId,
                DefinitionId,
                state?.content ?? 0f,
                useEffects,
                affectedAmount,
                liquidId));
        }

        private void RefreshDynamicMass()
        {
            if (body == null || definition == null || state == null)
            {
                return;
            }

            float massKilograms = definition.MassKilograms;
            if (definition.EmptyContainerMassKilograms > 0f &&
                definition.InitialContent > 0.0001f)
            {
                float remainingRatio = Mathf.Clamp01(
                    state.content / definition.InitialContent);
                massKilograms = Mathf.Lerp(
                    definition.EmptyContainerMassKilograms,
                    definition.MassKilograms,
                    remainingRatio);
            }

            body.mass = Mathf.Max(0.001f, massKilograms);
        }

        private void ApplyConsumedPresentationState()
        {
            if (state != null && definition != null)
            {
                gameObject.SetActive(!state.isConsumed);
            }
        }

        private void RefreshFoodPresentation()
        {
            if (PresentationRoot == null || definition == null || state == null ||
                !definition.Food.IsConfigured)
            {
                return;
            }

            Color tint = definition.Food.ResolveTint(
                state.condition,
                state.cookingSeconds);
            Renderer[] renderers =
                PresentationRoot.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorPropertyId, tint);
                block.SetColor(ColorPropertyId, tint);
                renderer.SetPropertyBlock(block);
                block.Clear();
            }
        }

        private void UpdatePickupPrompt()
        {
            if (pickupTarget != null)
            {
                pickupTarget.SetPrompt("Поднять");
            }
        }

        private string BuildInteractionPrompt()
        {
            if (definition == null || state == null)
            {
                return string.Empty;
            }

            switch (definition.PrimaryAction)
            {
                case ItemPrimaryAction.Consume:
                    return IsEmpty ? "Пусто" : "Использовать";
                case ItemPrimaryAction.ToggleOpen:
                    return state.isOpen ? "Закрыть" : "Открыть";
                case ItemPrimaryAction.ToggleDevice:
                    return state.isEnabled ? "Выключить" : "Включить";
                case ItemPrimaryAction.CycleVariant:
                    return "Сменить режим";
                case ItemPrimaryAction.DispenseChild:
                    return IsEmpty ? "Пусто" : "Достать";
                case ItemPrimaryAction.ConsumeContainedChild:
                    return IsEmpty ? "Пусто" : "Выпить";
                case ItemPrimaryAction.Ignite:
                    return state.isEnabled ? "Горит" : "Поджечь";
                case ItemPrimaryAction.IgniteFuel:
                    if (state.isEnabled)
                    {
                        return IsIgnited ? "Горит" : "Тлеет";
                    }
                    if (TryGetFlag(
                            definition.Combustion.WetStateId,
                            out bool wet) && wet)
                    {
                        return "Слишком мокро";
                    }
                    return state.content <=
                        definition.Combustion.MinimumFuelToIgnite
                        ? "Насыпать уголь"
                        : "Зажечь";
                default:
                    return string.Empty;
            }
        }

        private string BuildStatusText()
        {
            if (definition == null || state == null)
            {
                return string.Empty;
            }

            string amount = definition.ContentMeasure switch
            {
                ItemContentMeasure.Litres => $"{state.content:0.##} л",
                ItemContentMeasure.None => string.Empty,
                _ => $"{state.content:0.##}/{definition.MaximumContent:0.##}",
            };
            string openness = definition.CanOpen
                ? state.isOpen ? "открыто" : "закрыто"
                : string.Empty;
            string variant = string.IsNullOrEmpty(ActiveToolVariant)
                ? string.Empty
                : ActiveToolVariant;
            string foodState = string.Empty;
            if (definition.Food.IsConfigured)
            {
                string freshness = definition.Food.Perishable
                    ? IsSpoiled
                        ? "испорчено"
                        : $"свежесть {state.condition:0}%"
                    : string.Empty;
                string cooking = definition.Food.Cookable
                    ? CookState switch
                    {
                        FoodCookState.Cooked => "готово",
                        FoodCookState.Burned => "подгорело",
                        _ => "сырое",
                    }
                    : string.Empty;
                foodState = string.Join(
                    ", ",
                    new[] { freshness, cooking }
                        .Where(value => !string.IsNullOrEmpty(value)));
            }
            string combustionState = definition.Combustion.IsConfigured
                ? state.isEnabled
                    ? IsIgnited ? "горит" : "тлеет"
                    : "не горит"
                : string.Empty;
            return string.Join(
                " · ",
                new[]
                    {
                        definition.DisplayName,
                        amount,
                        openness,
                        variant,
                        foodState,
                        combustionState,
                    }
                    .Where(value => !string.IsNullOrEmpty(value)));
        }
    }
}
