using MSC.Vehicle.Simulation;

namespace MSC.Vehicle
{
    /// <summary>Runtime input boundary sampled once by the fixed-step host.</summary>
    public interface IVehicleInputSource
    {
        VehicleInputState ConsumeFixedInput(int currentSelectedGear);

        bool ConsumeResetRequest();
    }
}
