namespace MSC.Player
{
    /// <summary>
    /// Read-only subtitle feed consumed by the contextual HUD. The source owns
    /// timing and gameplay meaning; the HUD owns only placement and styling.
    /// </summary>
    public interface IPlayerSubtitleSource
    {
        string ActiveSubtitle { get; }

        void SetContextHudPresenterActive(bool active);
    }
}
