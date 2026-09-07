namespace MSC.Interaction.Capabilities
{
    /// <summary>Optional visual gate; does not disable pickup or other capabilities.</summary>
    public interface IInteractionOutlineVisibility
    {
        bool ShouldShowOutline { get; }
    }
}
