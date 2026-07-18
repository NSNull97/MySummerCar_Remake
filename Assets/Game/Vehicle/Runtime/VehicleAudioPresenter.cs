using System;
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
        [Header("Suspension impact mapping")]
        [SerializeField, Min(0f)] private float impactLoadRiseStartNewtons = 1000f;
        [SerializeField, Min(0f)] private float impactLoadRiseFullNewtons = 6000f;
        [SerializeField, Range(0f, 1f)] private float impactCompressionRiseStart = 0.04f;
        [SerializeField, Range(0f, 1f)] private float impactCompressionRiseFull = 0.3f;
        [SerializeField, Min(0f)] private float impactReleasePerSecond = 5f;

        private IVehicleAudioBackend backend;
        private VehicleSimulationHost subscribedResetHost;
        private VehicleEngineStatus previousEngineStatus;
        private bool hasPreviousEngineStatus;
        private float[] previousWheelLoads = Array.Empty<float>();
        private float[] previousWheelCompressions = Array.Empty<float>();
        private bool hasPreviousWheelSample;
        private float suspensionImpact01;

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
            float aggregateWheelSpeed = 0f;
            float greatestContactLoad = -1f;
            VehicleAudioSurface dominantSurface = VehicleAudioSurface.Unknown;
            EnsureWheelTrackingCapacity(telemetry.WheelCount);
            float impactTarget = 0f;
            for (int wheelIndex = 0; wheelIndex < telemetry.WheelCount; wheelIndex++)
            {
                VehicleWheelTelemetry wheel = telemetry.GetWheel(wheelIndex);
                float wheelSlip = Mathf.Max(
                    Mathf.Abs(wheel.LongitudinalSlip),
                    Mathf.Abs(wheel.LateralSlip));
                maximumWheelSlip = Mathf.Max(maximumWheelSlip, wheelSlip);
                aggregateWheelSpeed += Mathf.Abs(wheel.AngularSpeedRadiansPerSecond);
                if (wheel.HasContact && wheel.NormalLoadNewtons > greatestContactLoad)
                {
                    greatestContactLoad = wheel.NormalLoadNewtons;
                    dominantSurface = MapSurface(wheel.Surface);
                }

                float currentLoad = wheel.HasContact ? Mathf.Max(0f, wheel.NormalLoadNewtons) : 0f;
                float currentCompression = wheel.HasContact
                    ? Mathf.Clamp01(wheel.SuspensionCompression01)
                    : 0f;
                if (hasPreviousWheelSample)
                {
                    float loadRise = Mathf.Max(0f, currentLoad - previousWheelLoads[wheelIndex]);
                    float compressionRise = Mathf.Max(
                        0f,
                        currentCompression - previousWheelCompressions[wheelIndex]);
                    impactTarget = Mathf.Max(
                        impactTarget,
                        Mathf.Max(
                            NormalizeRise(
                                loadRise,
                                impactLoadRiseStartNewtons,
                                impactLoadRiseFullNewtons),
                            NormalizeRise(
                                compressionRise,
                                impactCompressionRiseStart,
                                impactCompressionRiseFull)));
                }

                previousWheelLoads[wheelIndex] = currentLoad;
                previousWheelCompressions[wheelIndex] = currentCompression;
            }

            hasPreviousWheelSample = true;
            aggregateWheelSpeed = telemetry.WheelCount > 0
                ? aggregateWheelSpeed / telemetry.WheelCount
                : 0f;
            float fixedStepSeconds = Mathf.Max(0f, telemetry.FixedStepSeconds);
            suspensionImpact01 = Mathf.Max(
                impactTarget,
                Mathf.MoveTowards(
                    suspensionImpact01,
                    0f,
                    impactReleasePerSecond * fixedStepSeconds));

            VehicleInputState input = simulationHost.LastInput;
            var supplemental = new VehicleAudioSupplementalParameters(
                input.IgnitionOn,
                input.StarterRequested,
                state.EngineStatus == VehicleEngineStatus.Cranking,
                telemetry.Brake01,
                aggregateWheelSpeed,
                telemetry.VehicleSpeedMetersPerSecond,
                suspensionImpact01);

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
                telemetry.EngineTemperatureCelsius,
                supplemental);
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
            ResetSuspensionTracking();
        }

        public void Configure(VehicleSimulationHost host, MonoBehaviour audioBackend)
        {
            UnsubscribeFromSimulationReset();
            simulationHost = host;
            backendComponent = audioBackend;
            backend = null;
            hasPreviousEngineStatus = false;
            ResetSuspensionTracking();
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

            VehicleTelemetry telemetry = simulationHost.Telemetry;
            EnsureWheelTrackingCapacity(telemetry != null ? telemetry.WheelCount : 0);

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
            ResetSuspensionTracking();
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

        private void EnsureWheelTrackingCapacity(int wheelCount)
        {
            wheelCount = Mathf.Max(0, wheelCount);
            if (previousWheelLoads.Length == wheelCount &&
                previousWheelCompressions.Length == wheelCount)
            {
                return;
            }

            previousWheelLoads = new float[wheelCount];
            previousWheelCompressions = new float[wheelCount];
            hasPreviousWheelSample = false;
            suspensionImpact01 = 0f;
        }

        private void ResetSuspensionTracking()
        {
            Array.Clear(previousWheelLoads, 0, previousWheelLoads.Length);
            Array.Clear(previousWheelCompressions, 0, previousWheelCompressions.Length);
            hasPreviousWheelSample = false;
            suspensionImpact01 = 0f;
        }

        private static float NormalizeRise(float value, float start, float full)
        {
            start = Mathf.Max(0f, start);
            full = Mathf.Max(start + 0.0001f, full);
            return Mathf.Clamp01((value - start) / (full - start));
        }
    }
}
