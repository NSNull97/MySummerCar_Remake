using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Explicit deterministic input hook for PlayMode fixtures and bounded route
    /// scripts. It never reads devices or searches the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScriptedVehicleInputSource : MonoBehaviour, IVehicleInputSource
    {
        [SerializeField, Range(0f, 1f)] private float throttle01;
        [SerializeField, Range(0f, 1f)] private float clutchPedal01;
        [SerializeField, Range(0f, 1f)] private float brake01;
        [SerializeField, Range(-1f, 1f)] private float steering;
        [SerializeField] private bool ignitionOn;
        [SerializeField] private bool starterRequested;

        private bool gearChangeRequested;
        private int requestedGear;
        private bool resetRequested;

        public void SetContinuousControls(
            float throttle,
            float clutchPedal,
            float brake,
            float steeringInput,
            bool ignition,
            bool starter)
        {
            throttle01 = Mathf.Clamp01(throttle);
            clutchPedal01 = Mathf.Clamp01(clutchPedal);
            brake01 = Mathf.Clamp01(brake);
            steering = Mathf.Clamp(steeringInput, -1f, 1f);
            ignitionOn = ignition;
            starterRequested = starter;
        }

        public void RequestGear(int gear)
        {
            requestedGear = gear;
            gearChangeRequested = true;
        }

        public void RequestReset()
        {
            resetRequested = true;
        }

        public void Clear()
        {
            SetContinuousControls(0f, 0f, 0f, 0f, ignition: false, starter: false);
            gearChangeRequested = false;
            requestedGear = 0;
            resetRequested = false;
        }

        public VehicleInputState ConsumeFixedInput(int currentSelectedGear)
        {
            int targetGear = gearChangeRequested ? requestedGear : currentSelectedGear;
            var result = new VehicleInputState(
                throttle01,
                clutchPedal01,
                brake01,
                steering,
                ignitionOn,
                starterRequested,
                gearChangeRequested,
                targetGear);
            gearChangeRequested = false;
            return result;
        }

        public bool ConsumeResetRequest()
        {
            bool result = resetRequested;
            resetRequested = false;
            return result;
        }
    }
}
