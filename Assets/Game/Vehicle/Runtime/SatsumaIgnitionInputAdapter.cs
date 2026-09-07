using System;
using MSC.Vehicle.Simulation;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Replaces only ignition and starter intent while retaining every other
    /// accepted VehicleInputRouter channel and reset edge.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class SatsumaIgnitionInputAdapter : MonoBehaviour, IVehicleInputSource
    {
        [SerializeField] private VehicleInputRouter router;
        [SerializeField] private SatsumaIgnitionController ignition;
        [SerializeField] private AssemblyCarburetorThrottleTarget carburetorThrottle;

        public AssemblyCarburetorThrottleTarget CarburetorThrottle => carburetorThrottle;
        public void ConfigureCarburetorThrottle(AssemblyCarburetorThrottleTarget target) =>
            carburetorThrottle = target;

        public VehicleInputRouter Router => router;
        public SatsumaIgnitionController Ignition => ignition;

        public void Configure(
            VehicleInputRouter configuredRouter,
            SatsumaIgnitionController configuredIgnition)
        {
            router = configuredRouter != null
                ? configuredRouter
                : throw new ArgumentNullException(nameof(configuredRouter));
            ignition = configuredIgnition != null
                ? configuredIgnition
                : throw new ArgumentNullException(nameof(configuredIgnition));
        }

        public VehicleInputState ConsumeFixedInput(int currentSelectedGear)
        {
            // Assembly/key loss can happen between the last interaction frame
            // and FixedUpdate. Cancel stale START before exporting intent.
            ignition?.RefreshAvailability();
            VehicleInputState source = router != null
                ? router.ConsumeFixedInput(currentSelectedGear)
                : VehicleInputState.Neutral();
            return new VehicleInputState(
                Mathf.Max(source.Throttle01, carburetorThrottle != null ? carburetorThrottle.RequestedThrottle01 : 0f),
                source.ClutchPedal01,
                source.Brake01,
                source.SteeringMinusOneToOne,
                ignition != null && ignition.IgnitionOn,
                ignition != null && ignition.StarterRequested,
                source.GearChangeRequested,
                source.RequestedGear);
        }

        public bool ConsumeResetRequest() =>
            router != null && router.ConsumeResetRequest();
    }
}
