using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class VehiclePhysicsValidationRig : MonoBehaviour
    {
        [SerializeField] private VehicleCalibrationProfile profile;
        [SerializeField] private VehicleSimulationHost simulationHost;
        [SerializeField] private VehicleValidationRouteAuthoring route;

        public VehicleCalibrationProfile Profile => profile;
        public VehicleSimulationHost SimulationHost => simulationHost;
        public VehicleValidationRouteAuthoring Route => route;

        public void Configure(
            VehicleCalibrationProfile configuredProfile,
            VehicleSimulationHost configuredSimulationHost,
            VehicleValidationRouteAuthoring configuredRoute)
        {
            profile = configuredProfile;
            simulationHost = configuredSimulationHost;
            route = configuredRoute;
        }

        public bool Validate(out string failure)
        {
            if (profile == null || simulationHost == null || route == null)
            {
                failure = "Validation rig requires a profile, simulation host, and route.";
                return false;
            }

            if (!profile.Validate(out failure))
            {
                return false;
            }

            if (!route.Validate(out failure))
            {
                return false;
            }

            if (simulationHost.Config != profile.VehicleConfiguration)
            {
                failure = "Validation profile and runtime host must reference the same vehicle config.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
