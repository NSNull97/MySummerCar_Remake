using UnityEngine;

namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Optional authored/runtime scope for an interaction whose collider is a
    /// logical marker beside the visible object (for example a bolt marker).
    /// </summary>
    public interface IInteractionOutlineRendererSource
    {
        Renderer ResolveOutlineRenderer();
    }
}
