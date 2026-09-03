using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Items;
using UnityEngine;

namespace MSC.Services.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionTargetHost))]
    public sealed class HomeMailOrderBoxInteractionTarget : MonoBehaviour,
        IInteractionDisplayTarget,
        IMountHandoffTarget
    {
        private ServiceRuntime runtime;
        private ItemWorldRuntime itemRuntime;
        private Action<string> feedback;

        public string InteractionDisplayName => "Почтовый ящик заказов";
        public string HandoffPrompt => "Опустить конверт заказа";

        public void Bind(
            ServiceRuntime configuredRuntime,
            ItemWorldRuntime configuredItemRuntime,
            Action<string> configuredFeedback = null)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            itemRuntime = configuredItemRuntime ??
                throw new ArgumentNullException(nameof(configuredItemRuntime));
            feedback = configuredFeedback;
            GetComponent<InteractionTargetHost>().Configure(this);
        }

        public bool CanAccept(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            WorldItemInstance item = ResolveItem(pickupTarget);
            return runtime != null && itemRuntime != null && item != null &&
                   string.Equals(
                       item.DefinitionId,
                       HomePartsMailOrderCatalog.EnvelopeDefinitionId,
                       StringComparison.Ordinal) &&
                   runtime.CurrentHomeMailOrderPhase ==
                       HomeMailOrderPhase.EnvelopeCreated;
        }

        public void Accept(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            WorldItemInstance item = ResolveItem(pickupTarget);
            if (item == null)
            {
                feedback?.Invoke("В ящик нужен именно конверт заказа");
                return;
            }

            ServiceResult result = runtime.TrySubmitHomeMailOrderEnvelope(
                item.StableId.Value);
            if (!result.Succeeded)
            {
                feedback?.Invoke("Этот конверт не относится к текущему заказу");
                return;
            }

            if (!itemRuntime.TryRemoveDynamic(item))
            {
                Debug.LogError(
                    "Submitted mail-order envelope could not be removed from item runtime.",
                    this);
            }

            feedback?.Invoke(
                "Конверт отправлен. Теймо сообщит, когда детали приедут");
        }

        private static WorldItemInstance ResolveItem(IPickupTarget pickupTarget) =>
            pickupTarget?.Body != null
                ? pickupTarget.Body.GetComponent<WorldItemInstance>()
                : null;

        private void Awake() =>
            GetComponent<InteractionTargetHost>().Configure(this);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionTargetHost))]
    public sealed class HomeMailOrderPaymentInteractionTarget : MonoBehaviour,
        IInteractionDisplayTarget,
        IContextInteractionTarget,
        IPrimaryInteractionOnlyTarget
    {
        private ServiceRuntime runtime;
        private Func<HomeMailOrderDeliveryPlan, bool> deliveryMaterializer;
        private Action<string> feedback;

        public string InteractionDisplayName => "Оплата почтового заказа";
        public string InteractionPrompt => runtime == null
            ? "Почта недоступна"
            : runtime.CurrentHomeMailOrderPhase ==
                HomeMailOrderPhase.PaidPendingMaterialization
                    ? "Забрать оплаченные детали"
                    : "Оплатить заказ деталей";

        public void Bind(
            ServiceRuntime configuredRuntime,
            Func<HomeMailOrderDeliveryPlan, bool> configuredMaterializer,
            Action<string> configuredFeedback = null)
        {
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            deliveryMaterializer = configuredMaterializer ??
                throw new ArgumentNullException(nameof(configuredMaterializer));
            feedback = configuredFeedback;
            GetComponent<InteractionTargetHost>().Configure(this);
        }

        public bool CanInteract(in InteractionContext context) =>
            runtime != null &&
            (runtime.CurrentHomeMailOrderPhase ==
                 HomeMailOrderPhase.ReadyForPayment ||
             runtime.CurrentHomeMailOrderPhase ==
                 HomeMailOrderPhase.PaidPendingMaterialization);

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                feedback?.Invoke("Готового почтового заказа сейчас нет");
                return;
            }

            ServiceResult result = runtime.TryPayAndMaterializeHomeMailOrder(
                deliveryMaterializer);
            feedback?.Invoke(result.Succeeded
                ? "Заказ оплачен — детали выданы у почтовой стойки"
                : result.FailureReason == ServiceFailureReason.InsufficientFunds
                    ? "Не хватает денег на почтовый заказ"
                    : result.FailureReason == ServiceFailureReason.Closed
                        ? "Теймо сейчас не обслуживает почту"
                        : "Оплата прошла, но выдача деталей ждёт повторной попытки");
        }

        private void Awake() =>
            GetComponent<InteractionTargetHost>().Configure(this);
    }
}
