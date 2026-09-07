using System;
using MSC.Vehicle.Simulation;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Thin Unity fixed-step composition host. Device sampling stays in an input
    /// source; all simulation orchestration stays in VehicleSimulationRoot.
    /// </summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class VehicleSimulationHost : MonoBehaviour
    {
        private static readonly ProfilerMarker FixedTickMarker =
            new ProfilerMarker("MSC.Vehicle.Simulation.FixedTick");

        [SerializeField] private VehicleSimulationConfig config;
        [SerializeField] private MonoBehaviour wheelBackendComponent;
        [SerializeField] private AssemblyVehiclePrerequisiteAdapter prerequisiteSource;
        [SerializeField] private MonoBehaviour inputSourceComponent;
        [SerializeField] private VehicleResetController resetController;
        [SerializeField] private MonoBehaviour satsumaOperatingSource;

        private IWheelPhysicsBackend wheelBackend;
        private IVehicleInputSource inputSource;
        private VehicleSimulationRoot root;

        public VehicleSimulationConfig Config => config;

        public MonoBehaviour BackendComponent => wheelBackendComponent;

        public IWheelPhysicsBackend Backend => wheelBackend;

        public AssemblyVehiclePrerequisiteAdapter PrerequisiteSource => prerequisiteSource;

        public MonoBehaviour InputSourceComponent => inputSourceComponent;
        public MonoBehaviour SatsumaOperatingSourceComponent => satsumaOperatingSource;

        public IVehicleInputSource InputSource => inputSource;

        public VehicleSimulationRoot Root => root;

        public VehicleSimulationState State => root?.State;

        public VehicleTelemetry Telemetry => root?.Telemetry;

        public VehicleInputState LastInput { get; private set; }

        public int FixedTickCount { get; private set; }

        public bool IsInitialized => root != null;

        public event Action SimulationReset;

        public event Action SimulationRestored;

        private void Awake()
        {
            if (!TryInitialize(out string failure))
            {
                Debug.LogError("M06 vehicle simulation host initialization failed: " + failure, this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (root == null)
            {
                return;
            }

            if (inputSource != null && inputSource.ConsumeResetRequest())
            {
                ResetToSpawn();
                return;
            }

            LastInput = inputSource != null
                ? inputSource.ConsumeFixedInput(root.State.SelectedGear)
                : VehicleInputState.Neutral();
            using (FixedTickMarker.Auto())
            {
                root.Tick(Time.fixedDeltaTime, LastInput);
            }

            FixedTickCount++;
        }

        public void Configure(
            VehicleSimulationConfig simulationConfig,
            MonoBehaviour backend,
            AssemblyVehiclePrerequisiteAdapter prerequisites,
            MonoBehaviour runtimeInputSource)
        {
            config = simulationConfig;
            wheelBackendComponent = backend;
            prerequisiteSource = prerequisites;
            inputSourceComponent = runtimeInputSource;
            root = null;
            wheelBackend = null;
            inputSource = null;
        }

        public void Configure(
            VehicleSimulationConfig simulationConfig,
            PrototypeRaycastWheelPhysicsBackend backend,
            AssemblyVehiclePrerequisiteAdapter prerequisites,
            MonoBehaviour runtimeInputSource)
        {
            Configure(simulationConfig, (MonoBehaviour)backend, prerequisites, runtimeInputSource);
        }

        public void ConfigureResetController(VehicleResetController controller)
        {
            resetController = controller;
        }

        public void ConfigureSatsumaOperatingSource(MonoBehaviour source)
        {
            if (root != null) throw new InvalidOperationException("Configure operating conditions before simulation initialization.");
            if (source != null && source is not ISatsumaOperatingConditionSource)
                throw new ArgumentException("The source must implement ISatsumaOperatingConditionSource.", nameof(source));
            satsumaOperatingSource = source;
        }

        public bool TryInitialize(out string failure)
        {
            if (root != null)
            {
                failure = string.Empty;
                return true;
            }

            if (config == null)
            {
                failure = "VehicleSimulationConfig is not assigned.";
                return false;
            }

            if (!config.Validate(out failure))
            {
                return false;
            }

            wheelBackend = wheelBackendComponent as IWheelPhysicsBackend;
            if (wheelBackend == null)
            {
                failure = "The serialized backend component does not implement IWheelPhysicsBackend.";
                return false;
            }

            if (prerequisiteSource == null)
            {
                failure = "AssemblyVehiclePrerequisiteAdapter is not assigned.";
                return false;
            }

            if (inputSourceComponent != null)
            {
                inputSource = inputSourceComponent as IVehicleInputSource;
                if (inputSource == null)
                {
                    failure = "The serialized input component does not implement IVehicleInputSource.";
                    return false;
                }
            }

            if (satsumaOperatingSource != null && satsumaOperatingSource is not ISatsumaOperatingConditionSource)
            {
                failure = "The serialized Satsuma operating source has an unsupported component type.";
                return false;
            }
            if (satsumaOperatingSource is SatsumaEngineOperatingSource operating && !operating.ValidateBindings(out failure))
                return false;
            root = new VehicleSimulationRoot(config, wheelBackend, prerequisiteSource,
                satsumaOperatingSource as ISatsumaOperatingConditionSource);
            prerequisiteSource.UseSimulationFluidAuthority(satsumaOperatingSource != null);
            LastInput = VehicleInputState.Neutral();
            FixedTickCount = 0;
            failure = string.Empty;
            return true;
        }

        public void SetInputSourceForTesting(IVehicleInputSource source)
        {
            inputSource = source;
            inputSourceComponent = source as MonoBehaviour;
        }

        public void ResetSimulation()
        {
            root?.Reset();
            LastInput = VehicleInputState.Neutral();
            FixedTickCount = 0;
            SimulationReset?.Invoke();
        }

        public bool TryRestoreSimulationState(
            VehicleSimulationStateDto dto,
            out string failure)
        {
            if (root == null && !TryInitialize(out failure))
            {
                return false;
            }

            if (!root.TryRestoreState(dto, out failure))
            {
                return false;
            }

            LastInput = VehicleInputState.Neutral();
            FixedTickCount = 0;
            SimulationRestored?.Invoke();
            failure = string.Empty;
            return true;
        }

        public void ResetToSpawn()
        {
            if (resetController != null)
            {
                resetController.ResetToSpawn();
                return;
            }

            ResetSimulation();
        }
    }
}
