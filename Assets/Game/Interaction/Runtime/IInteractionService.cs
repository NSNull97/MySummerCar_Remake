namespace MSC.Interaction
{
    /// <summary>
    /// Exposes whether player interaction intent may currently be processed.
    /// </summary>
    public interface IInteractionService
    {
        bool IsInteractionEnabled { get; }
    }
}
