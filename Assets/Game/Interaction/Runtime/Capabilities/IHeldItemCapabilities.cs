using UnityEngine;

namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Project-owned identity exposed by a selected tool. Compatibility checks
    /// use authored type/variant values, never hierarchy or asset names.
    /// </summary>
    public interface IHeldToolIdentity
    {
        string ToolType { get; }

        string ToolVariant { get; }
    }

    /// <summary>
    /// A selected first-person tool is a non-physical mode, not a carried
    /// world prop. The real renderer may be reused, but while selected it is
    /// held kinematically with collisions disabled at this camera-local pose.
    /// </summary>
    public interface IFirstPersonToolModeIdentity : IHeldToolIdentity
    {
        Vector3 IdleLocalPosition { get; }

        Quaternion IdleLocalRotation { get; }
    }

    public interface IFirstPersonToolSelectionTarget :
        IFirstPersonToolModeIdentity
    {
        string SelectionPrompt { get; }

        Transform ToolVisual { get; }

        bool CanSelect(in InteractionContext context);

        void NotifySelected(in InteractionContext context);

        void NotifyDeselected();
    }

    /// <summary>
    /// Optional presentation-only draw timing. Selection state is committed
    /// immediately; gameplay never waits for this animation to finish.
    /// </summary>
    public interface IFirstPersonToolSelectionTransition
    {
        float SelectionTransitionDurationSeconds { get; }
    }

    public interface IHeldToolActivationTarget
    {
        bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context);

        void ActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context);
    }

    /// <summary>
    /// World target operated by mouse-wheel direction while a non-physical
    /// first-person tool mode is active.
    /// </summary>
    public interface IScrollHeldToolActivationTarget
    {
        bool TryActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            float signedNotches);
    }

    /// <summary>
    /// Optional read-only directional contract used by contextual UI. Tool
    /// targets expose what each wheel direction would do without performing
    /// or probing the gameplay operation.
    /// </summary>
    public interface IDirectionalScrollHeldToolActivationTarget :
        IScrollHeldToolActivationTarget
    {
        bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            InteractionScrollDirection direction);

        string GetHeldToolScrollPrompt(InteractionScrollDirection direction);
    }

    /// <summary>
    /// Supplies the exact world pose where a selected first-person tool snaps
    /// as soon as the player aims at the target.
    /// </summary>
    public interface IFirstPersonToolSnapTarget
    {
        bool TryResolveToolSnapAnchor(
            IHeldToolIdentity tool,
            in InteractionContext context,
            out Transform anchor);
    }

    public interface IHeldTargetActivationSource
    {
        bool CanActivateHeldOn(
            IContextInteractionTarget target,
            in InteractionContext context);

        void ActivateHeldOn(
            IContextInteractionTarget target,
            in InteractionContext context);
    }

    /// <summary>
    /// Neutral liquid boundary used by carried containers. World fixtures do
    /// not depend on the item module and items do not dispatch by object name.
    /// </summary>
    public interface ILiquidSourceTarget
    {
        bool CanProvideLiquid(
            string currentLiquidId,
            float requestedLitres,
            in InteractionContext context);

        bool TryProvideLiquid(
            string currentLiquidId,
            float requestedLitres,
            in InteractionContext context,
            out string liquidId,
            out float providedLitres);
    }

    public interface ILiquidReceiverTarget
    {
        bool CanReceiveLiquid(
            string liquidId,
            float offeredLitres,
            in InteractionContext context);

        bool TryReceiveLiquid(
            string liquidId,
            float offeredLitres,
            in InteractionContext context,
            out float acceptedLitres);
    }

    /// <summary>
    /// Passive liquid-container boundary used by physical fixture streams.
    /// The stream does not need to know which item module owns the collider.
    /// </summary>
    public interface ILiquidContainerTarget
    {
        bool CanAcceptLiquid(
            string liquidId,
            float requestedLitres);

        bool TryAcceptLiquid(
            string liquidId,
            float requestedLitres,
            out float acceptedLitres);
    }

    public static class LiquidTypeIds
    {
        public const string Water = "liquid.water";
    }
}
