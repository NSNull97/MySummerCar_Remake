using MSC.Interaction.Query;

namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Marks an explicitly authored nested target that may be selected through
    /// the solid collider of its own registered parent. Unrelated world solids
    /// remain authoritative occluders.
    /// </summary>
    public interface IParentColliderOcclusionBypass
    {
        bool CanBypassParentCollider(InteractionTargetHost parentHost);
    }
}
