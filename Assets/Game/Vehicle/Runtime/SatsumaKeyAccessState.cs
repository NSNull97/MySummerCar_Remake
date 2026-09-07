namespace MSC.Vehicle
{
    /// <summary>
    /// Read-only capability consumed by Satsuma controls that require the
    /// project-owned logical ignition key.
    /// </summary>
    public interface ISatsumaKeyAccess
    {
        bool HasAccess { get; }
    }

    /// <summary>
    /// Session-owned persistent state for the logical Satsuma ignition key.
    /// The physical key item is outside this bounded save integration.
    /// </summary>
    public sealed class SatsumaKeyAccessState : ISatsumaKeyAccess
    {
        public const string KeyId = "vehicle.satsuma.key";

        public SatsumaKeyAccessState()
        {
            HasAccess = true;
        }

        public bool HasAccess { get; private set; }

        public void SetAccess(bool hasAccess)
        {
            HasAccess = hasAccess;
        }
    }
}
