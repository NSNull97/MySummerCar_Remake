using System;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Services.Presentation
{
    /// <summary>
    /// Common project-owned binding for service interaction fixtures. The
    /// authored IDs are resolved only through ServiceCatalog; donor hierarchy
    /// names and scene searches are deliberately absent.
    /// </summary>
    [RequireComponent(typeof(InteractionTargetHost))]
    public abstract class ServiceInteractionTargetBase : MonoBehaviour,
        IContextInteractionTarget,
        IInteractionDisplayTarget,
        IPrimaryInteractionOnlyTarget
    {
        [SerializeField] private string locationId = string.Empty;
        [SerializeField] private string interactionAnchorId = string.Empty;

        private ServiceRuntime runtime;
        private ServiceInteractionFeedbackHandler feedbackHandler;

        public abstract string InteractionPrompt { get; }
        public abstract string InteractionDisplayName { get; }

        public string LocationId => locationId;
        public string InteractionAnchorId => interactionAnchorId;
        public ServiceRuntime Runtime => runtime;
        public ServiceInteractionFeedback LastFeedback { get; private set; }
        public bool HasFeedback { get; private set; }

        public event Action<ServiceInteractionFeedback> FeedbackRaised;

        public virtual bool CanInteract(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy &&
            TryResolveLocation(ExpectedLocationKind, out _, out _);

        public abstract void Interact(in InteractionContext context);

        public bool TryValidateBinding(out string failure) =>
            TryResolveLocation(ExpectedLocationKind, out _, out failure);

        protected abstract ServiceLocationKind ExpectedLocationKind { get; }

        protected void ConfigureBindingForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId)
        {
            locationId = configuredLocationId ?? string.Empty;
            interactionAnchorId = configuredInteractionAnchorId ?? string.Empty;
            BindHost();
        }

        protected void BindRuntime(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler configuredFeedback = null)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            feedbackHandler = configuredFeedback;
            BindHost();
        }

        protected bool TryResolveLocation(
            ServiceLocationKind expectedKind,
            out ServiceLocationDefinition location,
            out string failure)
        {
            location = null;
            if (runtime == null || !runtime.IsInitialized)
            {
                failure = "Service runtime is not bound or initialized.";
                return false;
            }

            if (!runtime.Catalog.TryGetLocation(locationId, out location) ||
                location.Kind != expectedKind ||
                !string.Equals(
                    location.InteractionAnchorId,
                    interactionAnchorId,
                    StringComparison.Ordinal))
            {
                failure =
                    $"Service interaction binding '{locationId}'/'{interactionAnchorId}' " +
                    "does not match the runtime catalog.";
                location = null;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        protected void Publish(
            ServiceInteractionKind kind,
            string offerId,
            ServiceResult result,
            string fallbackMessage)
        {
            var feedback = new ServiceInteractionFeedback(
                kind,
                locationId,
                offerId,
                result,
                fallbackMessage,
                transform.position,
                result.Succeeded
                    ? ServiceInteractionFeedbackCue.Confirm
                    : ServiceInteractionFeedbackCue.Reject);
            LastFeedback = feedback;
            HasFeedback = true;
            feedbackHandler?.Invoke(feedback);
            FeedbackRaised?.Invoke(feedback);
        }

        protected static string MessageFor(
            in ServiceResult result,
            string successMessage)
        {
            if (result.Succeeded)
            {
                return successMessage ?? string.Empty;
            }

            return result.FailureReason switch
            {
                ServiceFailureReason.Closed =>
                    "\u0421\u0435\u0439\u0447\u0430\u0441 \u0437\u0430\u043a\u0440\u044b\u0442\u043e",
                ServiceFailureReason.OutOfStock =>
                    "\u0422\u043e\u0432\u0430\u0440 \u0437\u0430\u043a\u043e\u043d\u0447\u0438\u043b\u0441\u044f",
                ServiceFailureReason.EmptyBasket =>
                    "\u041a\u043e\u0440\u0437\u0438\u043d\u0430 \u043f\u0443\u0441\u0442\u0430",
                ServiceFailureReason.InsufficientFunds =>
                    "\u041d\u0435 \u0445\u0432\u0430\u0442\u0430\u0435\u0442 \u0434\u0435\u043d\u0435\u0433",
                ServiceFailureReason.PendingFulfillment =>
                    "\u041e\u043f\u043b\u0430\u0447\u0435\u043d\u043e, \u0432\u044b\u0434\u0430\u0447\u0430 \u043e\u0436\u0438\u0434\u0430\u0435\u0442\u0441\u044f",
                ServiceFailureReason.HandoffUnavailable =>
                    "\u0422\u043e\u0432\u0430\u0440 \u043f\u043e\u043a\u0430 \u043d\u0435\u043b\u044c\u0437\u044f \u0432\u044b\u0434\u0430\u0442\u044c",
                ServiceFailureReason.VehicleOutcomeUnavailable =>
                    "\u0423\u0441\u043b\u0443\u0433\u0430 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u0430: \u043d\u0435\u0442 \u0441\u0435\u0440\u0432\u0438\u0441\u043d\u043e\u0433\u043e \u0430\u0432\u0442\u043e\u043c\u043e\u0431\u0438\u043b\u044f",
                ServiceFailureReason.InspectionUnavailable =>
                    "\u0422\u0435\u0445\u043e\u0441\u043c\u043e\u0442\u0440 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d: \u043d\u0435\u0442 \u0430\u0432\u0442\u043e\u043c\u043e\u0431\u0438\u043b\u044f",
                _ => "\u0414\u0435\u0439\u0441\u0442\u0432\u0438\u0435 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u043e",
            };
        }

        protected virtual void Awake() => BindHost();

        private void BindHost() =>
            GetComponent<InteractionTargetHost>()?.Configure(this);
    }

    public abstract class ServiceOfferInteractionTargetBase :
        ServiceInteractionTargetBase
    {
        [SerializeField] private string offerId = string.Empty;

        public string OfferId => offerId;

        public override string InteractionDisplayName =>
            TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _)
                ? offer.DisplayName
                : string.Empty;

        public override bool CanInteract(in InteractionContext context) =>
            base.CanInteract(context) &&
            TryResolveOffer(ExpectedOfferKind, out _, out _, out _);

        protected abstract ServiceOfferKind ExpectedOfferKind { get; }

        protected void ConfigureOfferForAuthoring(string configuredOfferId) =>
            offerId = configuredOfferId ?? string.Empty;

        protected bool TryResolveOffer(
            ServiceOfferKind expectedKind,
            out ServiceLocationDefinition location,
            out ServiceOfferDefinition offer,
            out string failure)
        {
            offer = null;
            if (!TryResolveLocation(ExpectedLocationKind, out location, out failure))
            {
                return false;
            }

            if (!Runtime.Catalog.TryGetOffer(offerId, out offer) ||
                offer.Kind != expectedKind ||
                !string.Equals(
                    offer.LocationId,
                    location.LocationId,
                    StringComparison.Ordinal))
            {
                failure =
                    $"Service offer '{offerId}' does not match location " +
                    $"'{location.LocationId}'.";
                offer = null;
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TeimoStoreOfferInteractionTarget :
        ServiceOfferInteractionTargetBase
    {
        [SerializeField, Min(1)] private int quantity = 1;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Store;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.RetailItem;

        public override string InteractionPrompt
        {
            get
            {
                if (!TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
                {
                    return string.Empty;
                }

                if (!Runtime.IsLocationOpen(offer.LocationId))
                {
                    return "\u041c\u0430\u0433\u0430\u0437\u0438\u043d \u0437\u0430\u043a\u0440\u044b\u0442";
                }

                return Runtime.GetRemainingStock(offer.OfferId) >= quantity
                    ? "\u0414\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u0432 \u043a\u043e\u0440\u0437\u0438\u043d\u0443"
                    : "\u041d\u0435\u0442 \u0432 \u043d\u0430\u043b\u0438\u0447\u0438\u0438";
            }
        }

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId,
            int configuredQuantity = 1)
        {
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
            quantity = Mathf.Max(1, configuredQuantity);
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) ||
                !TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
            {
                return;
            }

            ServiceResult result = Runtime.TryAddStoreItem(offer.OfferId, quantity);
            Publish(
                ServiceInteractionKind.StoreBasket,
                offer.OfferId,
                result,
                MessageFor(result, "\u0414\u043e\u0431\u0430\u0432\u043b\u0435\u043d\u043e \u0432 \u043a\u043e\u0440\u0437\u0438\u043d\u0443"));
        }
    }

    [DisallowMultipleComponent]
    public sealed class TeimoStoreCheckoutInteractionTarget :
        ServiceInteractionTargetBase
    {
        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Store;

        public override string InteractionPrompt =>
            TryResolveLocation(ExpectedLocationKind, out ServiceLocationDefinition location, out _) &&
            Runtime.IsLocationOpen(location.LocationId)
                ? "\u041e\u043f\u043b\u0430\u0442\u0438\u0442\u044c \u043f\u043e\u043a\u0443\u043f\u043a\u0438"
                : "\u041a\u0430\u0441\u0441\u0430 \u0437\u0430\u043a\u0440\u044b\u0442\u0430";

        public override string InteractionDisplayName => "\u041a\u0430\u0441\u0441\u0430 \u0422\u0435\u0439\u043c\u043e";

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId) =>
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            if (!Runtime.IsLocationOpen(LocationId))
            {
                ServiceResult closed = new(false, ServiceFailureReason.Closed);
                Publish(
                    ServiceInteractionKind.StoreCheckout,
                    string.Empty,
                    closed,
                    MessageFor(closed, string.Empty));
                return;
            }

            string operationId = Runtime.AllocateOperationId("store-checkout");
            ServiceResult result = Runtime.TryCheckoutStore(operationId);
            Publish(
                ServiceInteractionKind.StoreCheckout,
                string.Empty,
                result,
                MessageFor(result, "\u041f\u043e\u043a\u0443\u043f\u043a\u0438 \u043e\u043f\u043b\u0430\u0447\u0435\u043d\u044b"));
        }
    }

    [DisallowMultipleComponent]
    public sealed class TeimoPubOfferInteractionTarget :
        ServiceOfferInteractionTargetBase
    {
        [SerializeField, Min(1)] private int quantity = 1;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Pub;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.PubItem;

        public override string InteractionPrompt =>
            TryResolveOffer(ExpectedOfferKind, out ServiceLocationDefinition location, out _, out _) &&
            Runtime.IsLocationOpen(location.LocationId)
                ? "\u041a\u0443\u043f\u0438\u0442\u044c"
                : "\u041f\u0430\u0431 \u0437\u0430\u043a\u0440\u044b\u0442";

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId,
            int configuredQuantity = 1)
        {
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
            quantity = Mathf.Max(1, configuredQuantity);
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) ||
                !TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
            {
                return;
            }

            if (!Runtime.IsLocationOpen(offer.LocationId))
            {
                ServiceResult closed = new(false, ServiceFailureReason.Closed);
                Publish(
                    ServiceInteractionKind.PubPurchase,
                    offer.OfferId,
                    closed,
                    MessageFor(closed, string.Empty));
                return;
            }

            string operationId = Runtime.AllocateOperationId("pub-purchase");
            ServiceResult result = Runtime.TryPurchasePub(
                operationId,
                offer.OfferId,
                quantity);
            Publish(
                ServiceInteractionKind.PubPurchase,
                offer.OfferId,
                result,
                MessageFor(result, "\u0417\u0430\u043a\u0430\u0437 \u043e\u043f\u043b\u0430\u0447\u0435\u043d"));
        }
    }

    [DisallowMultipleComponent]
    public sealed class ServiceFuelDispenseInteractionTarget :
        ServiceOfferInteractionTargetBase
    {
        [SerializeField] private MonoBehaviour receiverComponent;
        [SerializeField, Min(0.001f)] private float requestedLitres = 1f;

        private bool dispensedSinceLastNozzleRelease;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.FuelStation;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.Fuel;

        public override string InteractionDisplayName
        {
            get
            {
                if (!TryResolveOffer(
                        ExpectedOfferKind,
                        out _,
                        out ServiceOfferDefinition offer,
                        out _))
                {
                    return string.Empty;
                }

                long price = Runtime.GetFuelPriceMinorUnitsPerLiter(
                    offer.FuelGrade);
                return $"{offer.DisplayName} — {FormatFuelPrice(price)} MK/л";
            }
        }

        public override string InteractionPrompt => HasUsableReceiver
            ? "\u0417\u0430\u043f\u0440\u0430\u0432\u0438\u0442\u044c"
            : "\u041d\u0435\u0447\u0435\u0433\u043e \u0437\u0430\u043f\u0440\u0430\u0432\u043b\u044f\u0442\u044c";

        public bool HasUsableReceiver =>
            receiverComponent is ILiquidReceiverTarget &&
            receiverComponent != null;

        public bool HasActiveFuelSession => dispensedSinceLastNozzleRelease;

        private static string FormatFuelPrice(long minorUnitsPerLiter)
        {
            long whole = minorUnitsPerLiter / 100L;
            long fraction = Math.Abs(minorUnitsPerLiter % 100L);
            return $"{whole},{fraction:00}";
        }

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId,
            MonoBehaviour configuredReceiver,
            float configuredRequestedLitres = 1f)
        {
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
            receiverComponent = configuredReceiver;
            requestedLitres = float.IsFinite(configuredRequestedLitres)
                ? Mathf.Max(0.001f, configuredRequestedLitres)
                : 1f;
            dispensedSinceLastNozzleRelease = false;
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) ||
                !TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
            {
                return;
            }

            if (!HasUsableReceiver ||
                !Runtime.IsLocationOpen(offer.LocationId))
            {
                ServiceResult unavailable = new(
                    false,
                    HasUsableReceiver
                        ? ServiceFailureReason.Closed
                        : ServiceFailureReason.InvalidRequest);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    unavailable,
                    HasUsableReceiver
                        ? MessageFor(unavailable, string.Empty)
                        : "\u0422\u043e\u043f\u043b\u0438\u0432\u043d\u044b\u0439 \u043f\u0440\u0438\u0451\u043c\u043d\u0438\u043a \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d");
                return;
            }

            if (!TryConvertLitresToMilliliters(
                    requestedLitres,
                    out long requestedMilliliters))
            {
                ServiceResult invalidVolume = new(
                    false,
                    ServiceFailureReason.InvalidRequest);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    invalidVolume,
                    "\u041d\u0435\u0432\u0435\u0440\u043d\u044b\u0439 \u043e\u0431\u044a\u0451\u043c \u0437\u0430\u043f\u0440\u0430\u0432\u043a\u0438");
                return;
            }

            ServiceResult preflight = Runtime.TryPreflightAcceptedFuel(
                offer.FuelGrade,
                requestedMilliliters);
            if (!preflight.Succeeded)
            {
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    preflight,
                    MessageFor(preflight, string.Empty));
                return;
            }

            var receiver = (ILiquidReceiverTarget)receiverComponent;
            string liquidId = FuelLiquidId(offer.FuelGrade);
            if (!receiver.CanReceiveLiquid(liquidId, requestedLitres, context) ||
                !receiver.TryReceiveLiquid(
                    liquidId,
                    requestedLitres,
                    context,
                    out float acceptedLitres) ||
                !float.IsFinite(acceptedLitres) ||
                acceptedLitres <= 0f ||
                acceptedLitres > requestedLitres)
            {
                ServiceResult rejected = new(false, ServiceFailureReason.InvalidRequest);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    rejected,
                    "\u041f\u0440\u0438\u0451\u043c\u043d\u0438\u043a \u043d\u0435 \u043f\u0440\u0438\u043d\u044f\u043b \u0442\u043e\u043f\u043b\u0438\u0432\u043e");
                return;
            }

            if (!TryConvertLitresToMilliliters(
                    acceptedLitres,
                    out long acceptedMilliliters))
            {
                ServiceResult invalidAcceptedVolume = new(
                    false,
                    ServiceFailureReason.InvalidRequest);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    invalidAcceptedVolume,
                    "\u041f\u0440\u0438\u0451\u043c\u043d\u0438\u043a \u0432\u0435\u0440\u043d\u0443\u043b \u043d\u0435\u0432\u0435\u0440\u043d\u044b\u0439 \u043e\u0431\u044a\u0451\u043c \u0442\u043e\u043f\u043b\u0438\u0432\u0430");
                return;
            }

            ServiceResult result = Runtime.TryRecordAcceptedFuel(
                offer.FuelGrade,
                acceptedMilliliters);
            if (result.Succeeded)
            {
                dispensedSinceLastNozzleRelease = true;
            }

            Publish(
                ServiceInteractionKind.FuelDispense,
                offer.OfferId,
                result,
                MessageFor(
                    result,
                    $"\u041f\u0440\u0438\u043d\u044f\u0442\u043e \u0442\u043e\u043f\u043b\u0438\u0432\u0430: {acceptedLitres:0.###} \u043b"));
        }

        /// <summary>
        /// Completes the physical nozzle lifecycle. A presentation controller
        /// may call this only after this target successfully dispensed fuel;
        /// an unused or unavailable nozzle cannot create donor theft state.
        /// </summary>
        public ServiceResult TryReleaseNozzle()
        {
            if (!dispensedSinceLastNozzleRelease ||
                !TryResolveOffer(
                    ExpectedOfferKind,
                    out _,
                    out ServiceOfferDefinition offer,
                    out _))
            {
                ServiceResult invalidRelease = new(
                    false,
                    ServiceFailureReason.InvalidRequest);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    OfferId,
                    invalidRelease,
                    "\u0422\u043e\u043f\u043b\u0438\u0432\u043e \u0435\u0449\u0451 \u043d\u0435 \u043f\u043e\u0434\u0430\u0432\u0430\u043b\u043e\u0441\u044c");
                return invalidRelease;
            }

            ServiceResult result = Runtime.TryReleaseFuelNozzle(
                offer.FuelGrade);
            if (result.Succeeded)
            {
                dispensedSinceLastNozzleRelease = false;
            }

            Publish(
                ServiceInteractionKind.FuelDispense,
                offer.OfferId,
                result,
                MessageFor(
                    result,
                    "\u0422\u043e\u043f\u043b\u0438\u0432\u043d\u044b\u0439 \u043f\u0438\u0441\u0442\u043e\u043b\u0435\u0442 \u0432\u043e\u0437\u0432\u0440\u0430\u0449\u0451\u043d"));
            return result;
        }

        private static bool TryConvertLitresToMilliliters(
            float litres,
            out long milliliters)
        {
            milliliters = 0;
            if (!float.IsFinite(litres) || litres <= 0f)
            {
                return false;
            }

            try
            {
                milliliters = Math.Max(
                    1L,
                    checked((long)Math.Round(
                        (double)litres * 1000d,
                        MidpointRounding.AwayFromZero)));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public static string FuelLiquidId(FuelGrade grade) => grade switch
        {
            // Item definitions use these canonical liquid identities. The
            // retail grade and the physical liquid are deliberately separate
            // concepts, but must agree at the container boundary.
            FuelGrade.Gasoline98 => "liquid.gasoline",
            FuelGrade.Diesel => "liquid.diesel",
            FuelGrade.FuelOil => "liquid.fuel.fuel-oil",
            _ => throw new ArgumentOutOfRangeException(nameof(grade)),
        };
    }

    [DisallowMultipleComponent]
    public sealed class FleetariWorkshopInteractionTarget :
        ServiceOfferInteractionTargetBase
    {
        [SerializeField] private StableEntityIdAuthoring playerVehicle;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Workshop;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.Workshop;

        public bool HasRealServiceTarget =>
            Runtime != null && Runtime.WorkshopOrderingAvailable &&
            playerVehicle != null &&
            playerVehicle.TryGetStableId(out StableEntityId stableId) &&
            stableId.IsValid;

        public override string InteractionPrompt => HasRealServiceTarget
            ? "\u0417\u0430\u043a\u0430\u0437\u0430\u0442\u044c \u0443\u0441\u043b\u0443\u0433\u0443"
            : "\u041d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u043e: \u043d\u0435\u0442 \u0441\u0435\u0440\u0432\u0438\u0441\u043d\u043e\u0433\u043e \u0430\u0432\u0442\u043e";

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId,
            StableEntityIdAuthoring configuredPlayerVehicle = null)
        {
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
            playerVehicle = configuredPlayerVehicle;
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) ||
                !TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
            {
                return;
            }

            if (!HasRealServiceTarget ||
                !playerVehicle.TryGetStableId(out StableEntityId stableId))
            {
                ServiceResult unavailable = new(
                    false,
                    ServiceFailureReason.VehicleOutcomeUnavailable);
                Publish(
                    ServiceInteractionKind.WorkshopOrder,
                    offer.OfferId,
                    unavailable,
                    MessageFor(unavailable, string.Empty));
                return;
            }

            ServiceResult selection = Runtime.TrySetWorkshopSelection(
                new WorkshopSelectionStateDto
                {
                    offerIds = new[] { offer.OfferId },
                    paintVariant = -1,
                    rimVariant = -1,
                    tireVariant = -1,
                    finalGearRatio = 4.286f,
                });
            if (!selection.Succeeded)
            {
                Publish(
                    ServiceInteractionKind.WorkshopOrder,
                    offer.OfferId,
                    selection,
                    MessageFor(selection, string.Empty));
                return;
            }

            string orderId = Runtime.AllocateOperationId("fleetari-order");
            ServiceResult result = Runtime.TryPlaceWorkshopOrder(
                orderId,
                stableId.Value);
            Publish(
                ServiceInteractionKind.WorkshopOrder,
                offer.OfferId,
                result,
                MessageFor(result, "\u0417\u0430\u043a\u0430\u0437 \u0424\u043b\u0438\u0442\u0430\u0440\u0438 \u043f\u0440\u0438\u043d\u044f\u0442"));
        }
    }

    [DisallowMultipleComponent]
    public sealed class VehicleInspectionInteractionTarget :
        ServiceOfferInteractionTargetBase
    {
        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Inspection;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.Inspection;

        public override string InteractionPrompt =>
            Runtime != null && Runtime.InspectionOrderingAvailable
                ? "\u041f\u0440\u043e\u0439\u0442\u0438 \u0442\u0435\u0445\u043e\u0441\u043c\u043e\u0442\u0440"
                : "\u0422\u0435\u0445\u043e\u0441\u043c\u043e\u0442\u0440 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d";

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId)
        {
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        public override void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) ||
                !TryResolveOffer(ExpectedOfferKind, out _, out ServiceOfferDefinition offer, out _))
            {
                return;
            }

            if (!Runtime.InspectionOrderingAvailable)
            {
                ServiceResult unavailable = new(
                    false,
                    ServiceFailureReason.InspectionUnavailable);
                Publish(
                    ServiceInteractionKind.VehicleInspection,
                    offer.OfferId,
                    unavailable,
                    MessageFor(unavailable, string.Empty));
                return;
            }

            string operationId = Runtime.AllocateOperationId("vehicle-inspection");
            ServiceResult result = Runtime.TryStartInspection(
                operationId,
                offer.OfferId);
            Publish(
                ServiceInteractionKind.VehicleInspection,
                offer.OfferId,
                result,
                MessageFor(result, "\u0422\u0435\u0445\u043e\u0441\u043c\u043e\u0442\u0440 \u0437\u0430\u0432\u0435\u0440\u0448\u0451\u043d"));
        }
    }
}
