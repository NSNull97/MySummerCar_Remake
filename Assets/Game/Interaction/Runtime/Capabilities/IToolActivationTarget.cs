namespace MSC.Interaction.Capabilities
{
    public interface IToolActivationTarget
    {
        string ToolPrompt { get; }

        bool CanActivateTool(in InteractionContext context);

        void ActivateTool(in InteractionContext context);
    }
}
