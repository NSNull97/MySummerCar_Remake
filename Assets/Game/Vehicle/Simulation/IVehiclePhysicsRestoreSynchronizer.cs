namespace MSC.Vehicle.Simulation
{
    /// <summary>
    /// Rebuilds backend-specific physics state after persisted assembly and
    /// simulation data have been applied, but before the chassis is released
    /// from its temporary kinematic restore guard.
    /// </summary>
    public interface IVehiclePhysicsRestoreSynchronizer
    {
        bool TrySynchronizeRestoredPhysics(out string failure);
    }
}
