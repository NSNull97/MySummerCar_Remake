namespace MSC.Interaction.Capabilities
{
    /// <summary>Read-only opening view. A renderer must never own or transfer liquid.</summary>
    public interface ILiquidLevelPresentationSource
    {
        bool IsLiquidLevelVisible { get; }
        float LiquidLevel01 { get; }
    }
}
