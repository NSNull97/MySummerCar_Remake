using MSC.Audio;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Presentation-only bridge from fixed-step simulation telemetry to audio.
    /// It reads stable typed state and never changes simulation or physics state.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class VehicleAudioPresenter : MonoBehaviour
    {
        [SerializeField] private VehicleSimulationHost simulationHost;
        [SerializeField] private MonoBehaviour backendComponent;

        private IVehicleAudioBackend backend;
        private VehicleSimulationHost subscribedResetHost;
        private VehicleEngineStatus previousEngineStatus;
        private bool hasPreviousEngineStatus;

        public VehicleSimulationHost SimulationHost => simulationHost;

        public MonoBehaviour BackendComponent => backendComponent;

        public bool IsInitialized => backend != null;

        private void Awake()
        {
            if (!TryInitialize(out string failure))
            {
                Debug.LogWarning("M06 vehicle audio presenter disabled: " + failure, this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            VehicleSimulationState state = simulationHost.State;
            VehicleTelemetry telemetry = simulationHost.Telemetry;
            VehicleSimulationConfig config = simulationHost.Config;
            if (state == null || telemetry == null || config == null)
            {
                return;
            }

            float maximumWheelSlip = 0f;
            float greatestContactLoad = -1f;
            VehicleAudioSurface dominantSurface = VehicleAudioSurface.Unknown;
            for (int wheelIndex = 0; wheelIndex < telemetry.WheelCount; wheelIndex++)
            {
                VehicleWheelTelemetry wheel = telemetry.GetWheel(wheelIndex);
                float wheelSlip = Mathf.Max(
                    Mathf.Abs(wheel.LongitudinalSlip),
                    Mathf.Abs(wheel.LateralSlip));
                maximumWheelSlip = Mathf.Max(maximumWheelSlip, wheelSlip);
                if (wheel.HasContact && wheel.NormalLoadNewtons > greatestContactLoad)
                {
                    greatestContactLoad = wheel.NormalLoadNewtons;
                    dominantSurface = MapSurface(wheel.Surface);
                }
            }

            var parameters = new VehicleAudioParameters(
                MapEngineState(state.EngineStatus),
                telemetry.EngineRpm,
                config.Engine.RedlineRpm,
                telemetry.EngineLoad01,
                telemetry.Throttle01,
                telemetry.SelectedGear,
                telemetry.ClutchSlipRpm,
                telemetry.VehicleSpeedMetersPerSecond,
                maximumWheelSlip,
                dominantSurface,
                telemetry.BatteryVoltage,
                telemetry.EngineTemperatureCelsius);
            backend.SetVehicleParameters(in parameters);
            PublishStateTransition(state.EngineStatus);
        }

        private void OnEnable()
        {
            SubscribeToSimulationReset();
        }

        private void OnDisable()
        {
            UnsubscribeFromSimulationReset();
            if (backend != null)
            {
                backend.PostVehicleEvent(VehicleAudioEvent.Reset);
            }

            hasPreviousEngineStatus = false;
        }

        public void Configure(VehicleSimulationHost host, MonoBehaviour audioBackend)
        {
            UnsubscribeFromSimulationReset();
            simulationHost = host;
            backendComponent = audioBackend;
            backend = null;
            hasPreviousEngineStatus = false;
            if (isActiveAndEnabled)
            {
                SubscribeToSimulationReset();
            }
        }

        public bool TryInitialize(out string failure)
        {
            if (simulationHost == null)
            {
                failure = "VehicleSimulationHost is not assigned.";
                return false;
            }

            backend = backendComponent as IVehicleAudioBackend;
            if (backend == null)
            {
                failure = "The serialized backend component does not implement IVehicleAudioBackend.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void SubscribeToSimulationReset()
        {
            if (simulationHost == null || subscribedResetHost == simulationHost)
            {
                return;
            }

            UnsubscribeFromSimulationReset();
            subscribedResetHost = simulationHost;
            subscribedResetHost.SimulationReset += HandleSimulationReset;
        }

        private void UnsubscribeFromSimulationReset()
        {
            if (subscribedResetHost == null)
            {
                return;
            }

            subscribedResetHost.SimulationReset -= HandleSimulationReset;
            subscribedResetHost = null;
        }

        private void HandleSimulationReset()
        {
            backend?.PostVehicleEvent(VehicleAudioEvent.Reset);
            hasPreviousEngineStatus = false;
        }

        private void PublishStateTransition(VehicleEngineStatus current)
        {
            if (!hasPreviousEngineStatus)
            {
                previousEngineStatus = current;
                hasPreviousEngineStatus = true;
                if (current == VehicleEngineStatus.Cranking)
                {
                    backend.PostVehicleEvent(VehicleAudioEvent.StarterEngaged);
                }
                else if (current == VehicleEngineStatus.Running)
                {
                    backend.PostVehicleEvent(VehicleAudioEvent.EngineStarted);
                }

                return;
            }

            if (current == previousEngineStatus)
            {
                return;
            }

            if (previousEngineStatus == VehicleEngineStatus.Cranking)
            {
                backend.PostVehicleEvent(VehicleAudioEvent.StarterDisengaged);
            }

            switch (current)
            {
                case VehicleEngineStatus.Cranking:
                    backend.PostVehicleEvent(VehicleAudioEvent.StarterEngaged);
                    break;
                case VehicleEngineStatus.Running:
                    backend.PostVehicleEvent(VehicleAudioEvent.EngineStarted);
                    break;
                case VehicleEngineStatus.Stalled:
                    backend.PostVehicleEvent(VehicleAudioEvent.EngineStalled);
                    break;
                case VehicleEngineStatus.Off:
                    backend.PostVehicleEvent(VehicleAudioEvent.EngineStopped);
                    break;
            }

            previousEngineStatus = current;
        }

        private static VehicleAudioEngineState MapEngineState(VehicleEngineStatus state)
        {
            switch (state)
            {
                case VehicleEngineStatus.Cranking: return VehicleAudioEngineState.Cranking;
                case VehicleEngineStatus.Running: return VehicleAudioEngineState.Running;
                case VehicleEngineStatus.Stalled: return VehicleAudioEngineState.Stalled;
                default: return VehicleAudioEngineState.Off;
            }
        }

        private static VehicleAudioSurface MapSurface(VehicleSurfaceType surface)
        {
            switch (surface)
            {
                case VehicleSurfaceType.Paved: return VehicleAudioSurface.Paved;
                case VehicleSurfaceType.Gravel: return VehicleAudioSurface.Gravel;
                case VehicleSurfaceType.Dirt: return VehicleAudioSurface.Dirt;
                case VehicleSurfaceType.Grass: return VehicleAudioSurface.Grass;
                case VehicleSurfaceType.MudWet: return VehicleAudioSurface.MudWet;
                default: return VehicleAudioSurface.Unknown;
            }
        }
    }
}
