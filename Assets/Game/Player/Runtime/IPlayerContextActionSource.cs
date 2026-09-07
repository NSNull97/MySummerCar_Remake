namespace MSC.Player
{
    /// <summary>One additional location/mode action in the existing three-slot HUD.</summary>
    public interface IPlayerContextActionSource
    {
        InteractionActionHint ContextActionHint { get; }
    }
}
