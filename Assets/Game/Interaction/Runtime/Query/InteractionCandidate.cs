using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Query
{
    public readonly struct InteractionCandidate
    {
        public InteractionCandidate(
            InteractionTargetHost host,
            Vector3 point,
            Vector3 normal,
            float distance)
            : this(host, point, normal, distance, null)
        {
        }

        public InteractionCandidate(
            InteractionTargetHost host,
            Vector3 point,
            Vector3 normal,
            float distance,
            Collider sourceCollider)
        {
            Host = host;
            Point = point;
            Normal = normal;
            Distance = distance;
            SourceCollider = sourceCollider;
        }

        public InteractionTargetHost Host { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        /// <summary>
        /// Exact collider selected by the interaction query. Presentation can
        /// use it to resolve the aimed visual without relying on object names.
        /// </summary>
        public Collider SourceCollider { get; }

        public bool IsValid => Host != null && Host.HasCapabilities;

        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
            if (Host != null)
            {
                return Host.TryGetCapability(out capability);
            }

            capability = null;
            return false;
        }

        public string GetPrompt(bool hasCarriedObject, in InteractionContext context)
        {
            return GetPrompt(hasCarriedObject, null, context);
        }

        public string GetPrompt(
            bool hasCarriedObject,
            IPickupTarget carriedTarget,
            in InteractionContext context)
        {
            if (!IsValid)
            {
                return string.Empty;
            }

            if (!hasCarriedObject &&
                TryGetCapability(
                    out IFirstPersonToolSelectionTarget selectionTarget) &&
                selectionTarget.CanSelect(context))
            {
                return selectionTarget.SelectionPrompt;
            }

            if (hasCarriedObject && TryGetCapability(out IMountHandoffTarget mountTarget))
            {
                if (carriedTarget != null)
                {
                    // CanAccept also refreshes the target-specific prompt.
                    // Never reuse text produced for the previously held part.
                    mountTarget.CanAccept(carriedTarget, context);
                }

                return mountTarget.HandoffPrompt;
            }

            if (hasCarriedObject &&
                carriedTarget is IHeldToolIdentity &&
                TryGetCapability(
                    out IScrollHeldToolActivationTarget _) &&
                TryGetCapability(
                    out IToolActivationTarget scrollToolPrompt))
            {
                // Keep the bolt actionable even with the wrong wrench size so
                // outline feedback can turn red instead of making it vanish.
                return scrollToolPrompt.ToolPrompt;
            }

            if (!hasCarriedObject &&
                TryGetCapability(out IPickupTarget pickupTarget) &&
                pickupTarget.CanPickup(context))
            {
                if (TryGetCapability(out IToolActivationTarget pickupTool) &&
                    pickupTool.CanActivateTool(context))
                {
                    return $"{pickupTarget.PickupPrompt} (ЛКМ) / " +
                           $"{pickupTool.ToolPrompt} (F)";
                }

                return pickupTarget.PickupPrompt;
            }

            if (TryGetCapability(
                    out IContinuousContextInteractionTarget continuousTarget) &&
                (continuousTarget.CanBeginContinuousInteraction(
                     context,
                     ContinuousContextInteractionDirection.Primary) ||
                 continuousTarget.CanBeginContinuousInteraction(
                     context,
                     ContinuousContextInteractionDirection.Secondary)))
            {
                return continuousTarget.InteractionPrompt;
            }

            if (TryGetCapability(out IContextInteractionTarget contextTarget) &&
                contextTarget.CanInteract(context) &&
                TryGetCapability(out IToolActivationTarget pairedToolTarget) &&
                pairedToolTarget.CanActivateTool(context))
            {
                return $"{contextTarget.InteractionPrompt} (ЛКМ) / " +
                       $"{pairedToolTarget.ToolPrompt} (F)";
            }

            if (TryGetCapability(out IContextInteractionTarget interactionTarget) &&
                interactionTarget.CanInteract(context))
            {
                return interactionTarget.InteractionPrompt;
            }

            if (!hasCarriedObject &&
                TryGetCapability(
                    out IIncrementalInteractionTarget incrementalTarget) &&
                incrementalTarget.CanAdjust(context))
            {
                return incrementalTarget.AdjustmentPrompt;
            }

            if (TryGetCapability(out IToolActivationTarget toolTarget) &&
                toolTarget.CanActivateTool(context))
            {
                return toolTarget.ToolPrompt;
            }

            return string.Empty;
        }
    }
}
