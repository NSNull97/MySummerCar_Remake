namespace MSC.Interaction.Capabilities
{
    public interface IContextInteractionTarget
    {
        string InteractionPrompt { get; }

        bool CanInteract(in InteractionContext context);

        void Interact(in InteractionContext context);
    }
}
