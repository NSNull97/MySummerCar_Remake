namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Explicitly allows a compact interaction trigger to remain selectable
    /// when the first-person ray origin is already inside it. Unity raycasts do
    /// not report a collider that contains the ray origin.
    /// </summary>
    public interface IRaycastOriginOverlapTarget
    {
    }
}
