using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Home
{
    /// <summary>
    /// Thin, explicitly configured scene capability. Stable action identity and
    /// action kind are authored inputs; neither hierarchy nor GameObject names
    /// participate in dispatch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeInteractionTarget :
        MonoBehaviour,
        IContextInteractionTarget,
        ILiquidSourceTarget,
        ILiquidReceiverTarget
    {
        [SerializeField] private string stableActionId =
            HomeActionIds.KitchenTapOpen;
        [SerializeField] private string prompt = "Открыть кран";
        [SerializeField] private HomeActionKind action =
            HomeActionKind.KitchenTapOpen;

        private HomeSystemRuntime runtime;

        public string StableActionId => stableActionId;
        public HomeActionKind Action => action;
        public string InteractionPrompt
        {
            get
            {
                string dynamicPrompt = runtime != null
                    ? runtime.GetInteractionPrompt(action)
                    : string.Empty;
                return string.IsNullOrWhiteSpace(dynamicPrompt)
                    ? prompt
                    : dynamicPrompt;
            }
        }

        public void Configure(
            string configuredStableActionId,
            string configuredPrompt,
            HomeActionKind configuredAction,
            HomeSystemRuntime configuredRuntime)
        {
            if (string.IsNullOrWhiteSpace(configuredStableActionId))
            {
                throw new ArgumentException(
                    "Home interaction requires a stable project-owned ID.",
                    nameof(configuredStableActionId));
            }

            if (!HomeActionIds.Matches(
                    configuredStableActionId,
                    configuredAction))
            {
                throw new ArgumentException(
                    "Home action ID does not match the configured action.",
                    nameof(configuredStableActionId));
            }

            if (string.IsNullOrWhiteSpace(configuredPrompt))
            {
                throw new ArgumentException(
                    "Home interaction requires a visible prompt.",
                    nameof(configuredPrompt));
            }

            stableActionId = configuredStableActionId;
            prompt = configuredPrompt;
            action = configuredAction;
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
        }

        public bool CanInteract(in InteractionContext context) =>
            enabled &&
            gameObject.activeInHierarchy &&
            runtime != null &&
            action != HomeActionKind.ElectricSaunaSteam &&
            runtime.CanPerformAction(stableActionId, action);

        public void Interact(in InteractionContext context)
        {
            if (runtime != null)
            {
                runtime.TryPerformAction(
                    stableActionId,
                    action,
                    out _);
            }
        }

        public bool CanProvideLiquid(
            string currentLiquidId,
            float requestedLitres,
            in InteractionContext context)
        {
            return action == HomeActionKind.KitchenTapConsume &&
                   runtime != null &&
                   runtime.Snapshot.KitchenTapOpen &&
                   requestedLitres > 0f &&
                   float.IsFinite(requestedLitres) &&
                   (string.IsNullOrEmpty(currentLiquidId) ||
                    string.Equals(
                        currentLiquidId,
                        LiquidTypeIds.Water,
                        StringComparison.Ordinal));
        }

        public bool TryProvideLiquid(
            string currentLiquidId,
            float requestedLitres,
            in InteractionContext context,
            out string liquidId,
            out float providedLitres)
        {
            liquidId = LiquidTypeIds.Water;
            providedLitres = CanProvideLiquid(
                currentLiquidId,
                requestedLitres,
                context)
                ? requestedLitres
                : 0f;
            return providedLitres > 0.0001f;
        }

        public bool CanReceiveLiquid(
            string liquidId,
            float offeredLitres,
            in InteractionContext context)
        {
            return action == HomeActionKind.ElectricSaunaSteam &&
                   runtime != null &&
                   offeredLitres > 0f &&
                   float.IsFinite(offeredLitres) &&
                   string.Equals(
                       liquidId,
                       LiquidTypeIds.Water,
                       StringComparison.Ordinal) &&
                   runtime.CanPerformAction(stableActionId, action);
        }

        public bool TryReceiveLiquid(
            string liquidId,
            float offeredLitres,
            in InteractionContext context,
            out float acceptedLitres)
        {
            acceptedLitres = 0f;
            if (!CanReceiveLiquid(liquidId, offeredLitres, context) ||
                !runtime.TryPerformAction(stableActionId, action, out _))
            {
                return false;
            }

            acceptedLitres = offeredLitres;
            return true;
        }
    }
}
