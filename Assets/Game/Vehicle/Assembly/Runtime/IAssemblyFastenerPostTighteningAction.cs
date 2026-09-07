namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Explicit optional action for a reviewed fastener after its last stage.
    /// The caller has already verified the held tool. Ordinary fasteners have
    /// no binding and retain their existing hard maximum.
    /// </summary>
    public interface IAssemblyFastenerPostTighteningAction
    {
        string PostTighteningPrompt { get; }
        bool CanApply(AssemblyFastenerInteractionTarget source);
        bool TryApply(AssemblyFastenerInteractionTarget source);
    }
}
