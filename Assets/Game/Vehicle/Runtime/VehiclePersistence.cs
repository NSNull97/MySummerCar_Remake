using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    [Serializable]
    public sealed class VehiclePhysicsSaveDto
    {
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public bool sleeping;

        public bool TryValidate(out string failure)
        {
            if (!IsFinite(worldPosition) ||
                !IsFinite(linearVelocity) ||
                !IsFinite(angularVelocity) ||
                !IsValidRotation(worldRotation) ||
                linearVelocity.sqrMagnitude > 250000f ||
                angularVelocity.sqrMagnitude > 40000f)
            {
                failure = "Vehicle chassis physics state is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        internal static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        internal static bool IsValidRotation(Quaternion value)
        {
            if (!float.IsFinite(value.x) ||
                !float.IsFinite(value.y) ||
                !float.IsFinite(value.z) ||
                !float.IsFinite(value.w))
            {
                return false;
            }

            float magnitudeSquared = value.x * value.x + value.y * value.y +
                                     value.z * value.z + value.w * value.w;
            return magnitudeSquared > 0.000001f &&
                   magnitudeSquared < 1000000f;
        }
    }

    [Serializable]
    public sealed class VehicleSaveRecordDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string stableVehicleId = string.Empty;
        public string configurationId = string.Empty;
        public int tuningSchemaVersion;
        public VehicleAssemblySaveData assembly;
        public VehicleSimulationStateDto simulation;
        public VehiclePhysicsSaveDto physics;
        public bool ignitionOn;
        // Optional backward-compatible extension. Legacy records omit it and
        // therefore retain their authored body colour.
        public VehiclePaintSaveDto paint;

        public bool TryValidateBasic(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported vehicle record schema {schemaVersion}.";
                return false;
            }

            if (!StableEntityId.TryParse(stableVehicleId, out _) ||
                string.IsNullOrWhiteSpace(configurationId) ||
                configurationId.Length > 128 ||
                tuningSchemaVersion <= 0)
            {
                failure = "Vehicle identity or tuning identity is invalid.";
                return false;
            }

            if (assembly == null || !assembly.HasSupportedSchema ||
                assembly.parts == null || assembly.parts.Length > 2048 ||
                assembly.mounts == null || assembly.mounts.Length > 2048 ||
                assembly.fasteners == null || assembly.fasteners.Length > 8192)
            {
                failure = "Vehicle assembly payload is invalid or oversized.";
                return false;
            }

            if (simulation == null ||
                simulation.schemaVersion != VehicleSimulationStateDto.CurrentSchemaVersion ||
                simulation.wheels == null || simulation.wheels.Length > 32)
            {
                failure = "Vehicle simulation payload is invalid or oversized.";
                return false;
            }

            if (physics == null)
            {
                failure = "Vehicle physics payload is missing.";
                return false;
            }

            if (!physics.TryValidate(out failure))
            {
                return false;
            }

            if (paint != null && !paint.TryValidate(out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleDomainSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentConfigurationId = "vehicles.native.v1";

        public int schemaVersion = CurrentSchemaVersion;
        public string configurationId = CurrentConfigurationId;
        public VehicleSaveRecordDto[] vehicles = Array.Empty<VehicleSaveRecordDto>();

        public bool TryValidateBasic(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                !string.Equals(
                    configurationId,
                    CurrentConfigurationId,
                    StringComparison.Ordinal) ||
                vehicles == null || vehicles.Length > 64)
            {
                failure = "Vehicle domain schema, configuration, or collection is invalid.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < vehicles.Length; index++)
            {
                VehicleSaveRecordDto record = vehicles[index];
                if (record == null)
                {
                    failure = $"Vehicle record {index} is missing.";
                    return false;
                }

                if (!record.TryValidateBasic(out failure))
                {
                    return false;
                }

                if (!ids.Add(record.stableVehicleId))
                {
                    failure = $"Duplicate vehicle ID '{record.stableVehicleId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Explicit project-owned aggregate boundary shared by the M05/M06 fixture
    /// and Phase 1 vehicles. Presentation provenance remains a separate concern.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehiclePersistenceBindingCore : MonoBehaviour
    {
        [SerializeField] private StableEntityIdAuthoring stableVehicleIdentity;
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private VehicleSimulationHost simulationHost;
        [SerializeField] private VehicleInputRouter inputRouter;
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private VehiclePaintStateController paintController;
        [SerializeField] private AssemblyChassisMassController chassisMassController;
        [SerializeField] private MonoBehaviour physicsRestoreSynchronizerComponent;

        public string StableVehicleId =>
            stableVehicleIdentity != null &&
            stableVehicleIdentity.TryGetStableId(out StableEntityId id)
                ? id.Value
                : string.Empty;

        public AssemblyChassisMassController ChassisMassController =>
            chassisMassController;

        public VehicleAssemblyController AssemblyController =>
            assemblyController;

        public Rigidbody Chassis => chassis;

        public MonoBehaviour PhysicsRestoreSynchronizerComponent =>
            physicsRestoreSynchronizerComponent;

        public IVehiclePhysicsRestoreSynchronizer PhysicsRestoreSynchronizer =>
            physicsRestoreSynchronizerComponent as
                IVehiclePhysicsRestoreSynchronizer;

        public void Configure(
            StableEntityIdAuthoring identity,
            VehicleAssemblyController assembly,
            VehicleSimulationHost simulation,
            VehicleInputRouter input,
            Rigidbody targetChassis,
            VehiclePaintStateController paint = null,
            AssemblyChassisMassController massController = null,
            MonoBehaviour physicsRestoreSynchronizer = null)
        {
            stableVehicleIdentity = identity;
            assemblyController = assembly;
            simulationHost = simulation;
            inputRouter = input;
            chassis = targetChassis;
            paintController = paint;
            chassisMassController = massController;
            physicsRestoreSynchronizerComponent =
                physicsRestoreSynchronizer;
        }

        public bool TryCapture(out VehicleSaveRecordDto record, out string failure)
        {
            record = null;
            if (!TryValidateBinding(out failure))
            {
                return false;
            }

            record = new VehicleSaveRecordDto
            {
                stableVehicleId = StableVehicleId,
                configurationId = simulationHost.Config.ConfigurationId,
                tuningSchemaVersion = simulationHost.Config.TuningSchemaVersion,
                assembly = assemblyController.CaptureSaveData(),
                simulation = simulationHost.State.CaptureDto(),
                physics = new VehiclePhysicsSaveDto
                {
                    worldPosition = chassis.position,
                    worldRotation = chassis.rotation,
                    linearVelocity = chassis.linearVelocity,
                    angularVelocity = chassis.angularVelocity,
                    sleeping = chassis.IsSleeping(),
                },
                ignitionOn = inputRouter != null && inputRouter.IgnitionOn,
                paint = paintController != null
                    ? paintController.CaptureSaveData()
                    : null,
            };

            if (!record.TryValidateBasic(out failure))
            {
                record = null;
                return false;
            }

            return true;
        }

        public bool CanRestore(VehicleSaveRecordDto record, out string failure)
        {
            if (!TryValidateBinding(out failure) ||
                record == null ||
                !record.TryValidateBasic(out failure))
            {
                failure = record == null
                    ? "Vehicle save record is missing."
                    : failure;
                return false;
            }

            VehicleSimulationConfig config = simulationHost.Config;
            if (!string.Equals(
                    StableVehicleId,
                    record.stableVehicleId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    config.ConfigurationId,
                    record.configurationId,
                    StringComparison.Ordinal) ||
                config.TuningSchemaVersion != record.tuningSchemaVersion)
            {
                failure = "Vehicle stable ID or tuning identity does not match runtime content.";
                return false;
            }

            AssemblyOperationResult assemblyValidation =
                assemblyController.ValidateSaveDataForRestore(record.assembly);
            if (!assemblyValidation.Succeeded)
            {
                failure = assemblyValidation.Message;
                return false;
            }

            var simulationProbe = new VehicleSimulationState();
            simulationProbe.Reset(config);
            if (!simulationProbe.TryRestoreDto(record.simulation, config))
            {
                failure = "Vehicle simulation payload failed domain validation.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRestore(VehicleSaveRecordDto record, out string failure)
        {
            if (!CanRestore(record, out failure) ||
                !TryCapture(out VehicleSaveRecordDto checkpoint, out failure))
            {
                return false;
            }

            bool hostWasEnabled = simulationHost.enabled;
            simulationHost.enabled = false;
            if (ApplyValidated(record, out failure))
            {
                simulationHost.enabled = hostWasEnabled;
                return true;
            }

            string originalFailure = failure;
            ApplyValidated(checkpoint, out _);
            simulationHost.enabled = hostWasEnabled;
            failure = originalFailure;
            return false;
        }

        private bool ApplyValidated(VehicleSaveRecordDto record, out string failure)
        {
            bool wasKinematic = chassis.isKinematic;
            chassis.isKinematic = true;
            chassis.position = record.physics.worldPosition;
            chassis.rotation = record.physics.worldRotation.normalized;
            chassis.transform.SetPositionAndRotation(
                record.physics.worldPosition,
                record.physics.worldRotation.normalized);
            bool restoreSucceeded = false;
            try
            {
                AssemblyOperationResult assemblyResult =
                    assemblyController.RestoreSaveData(record.assembly);
                if (!assemblyResult.Succeeded)
                {
                    failure = assemblyResult.Message;
                    return false;
                }

                assemblyController.SynchronizeInstalledParts();
                simulationHost.PrerequisiteSource.InvalidateCache();
                if (!simulationHost.TryRestoreSimulationState(
                        record.simulation,
                        out failure))
                {
                    return false;
                }

                inputRouter?.RestorePersistentState(record.ignitionOn);
                if (paintController != null &&
                    !paintController.TryRestore(record.paint, out failure))
                {
                    return false;
                }

                IVehiclePhysicsRestoreSynchronizer physicsSynchronizer =
                    PhysicsRestoreSynchronizer;
                if (physicsRestoreSynchronizerComponent != null &&
                    physicsSynchronizer == null)
                {
                    failure =
                        "Vehicle physics restore synchronizer is invalid.";
                    return false;
                }

                if (physicsSynchronizer != null &&
                    !physicsSynchronizer.TrySynchronizeRestoredPhysics(
                        out failure))
                {
                    return false;
                }

                // RestoreSaveData intentionally publishes no ordinary assembly
                // action. Refresh the compound mass explicitly while the body
                // is still kinematic instead of waiting one live physics tick.
                assemblyController.SynchronizeInstalledParts();
                chassisMassController?.RefreshMass(force: true);

                Physics.SyncTransforms();
                restoreSucceeded = true;
                failure = string.Empty;
                return true;
            }
            finally
            {
                chassis.isKinematic = wasKinematic;
                if (!wasKinematic && restoreSucceeded)
                {
                    chassis.linearVelocity = record.physics.linearVelocity;
                    chassis.angularVelocity = record.physics.angularVelocity;
                    if (record.physics.sleeping)
                    {
                        chassis.Sleep();
                    }
                    else
                    {
                        chassis.WakeUp();
                    }
                }
            }
        }

        private bool TryValidateBinding(out string failure)
        {
            if (string.IsNullOrEmpty(StableVehicleId) ||
                assemblyController == null ||
                simulationHost == null ||
                simulationHost.Config == null ||
                simulationHost.State == null ||
                simulationHost.PrerequisiteSource == null ||
                chassis == null ||
                (physicsRestoreSynchronizerComponent != null &&
                 PhysicsRestoreSynchronizer == null))
            {
                failure = "Vehicle persistence binding is incomplete or uninitialized.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
