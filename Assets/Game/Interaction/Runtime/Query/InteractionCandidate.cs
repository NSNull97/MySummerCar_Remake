using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Query
{
    public readonly struct InteractionCandidate
    {
        public InteractionCandidate(InteractionTargetHost host, Vector3 point, Vector3 normal, float distance)
        {
            Host = host;
            Point = point;
            Normal = normal;
            Distance = distance;
        }

        public InteractionTargetHost Host { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

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
            if (!IsValid)
            {
                return string.Empty;
            }

            if (hasCarriedObject && TryGetCapability(out IMountHandoffTarget mountTarget))
            {
                return mountTarget.HandoffPrompt;
            }

            if (!hasCarriedObject &&
                TryGetCapability(out IPickupTarget pickupTarget) &&
                pickupTarget.CanPickup(context))
            {
                return pickupTarget.PickupPrompt;
            }

            if (TryGetCapability(out IContextInteractionTarget interactionTarget) &&
                interactionTarget.CanInteract(context))
            {
                return interactionTarget.InteractionPrompt;
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
