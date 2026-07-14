namespace MSC.Core.Identity
{
    /// <summary>
    /// Resolves loaded persistent entities without relying on Unity instance IDs, names, or hierarchy paths.
    /// </summary>
    public interface IEntityIdProvider
    {
        bool TryResolve(StableEntityId entityId, out StableEntityIdAuthoring entity);
    }
}
