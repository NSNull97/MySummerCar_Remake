using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Replays the same assembly-dependent NWH and suspension presentation
    /// handoff that an ordinary part mutation receives, while persistence still
    /// holds the chassis kinematic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaNwhPhysicsRestoreSynchronizer : MonoBehaviour,
        IVehiclePhysicsRestoreSynchronizer
    {
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private NwhAssemblyWheelSupportController wheelSupport;
        [SerializeField] private SatsumaFrontSteeringController frontSteering;
        [SerializeField] private SatsumaFrontSuspensionController frontSuspension;
        [SerializeField] private SatsumaRearNwhSuspensionController rearSuspension;
        [SerializeField] private SatsumaHandbrakeNwhAdapter handbrake;

        public VehicleAssemblyController AssemblyController =>
            assemblyController;
        public Rigidbody Chassis => chassis;
        public NwhAssemblyWheelSupportController WheelSupport => wheelSupport;
        public SatsumaFrontSteeringController FrontSteering => frontSteering;
        public SatsumaFrontSuspensionController FrontSuspension =>
            frontSuspension;
        public SatsumaRearNwhSuspensionController RearSuspension =>
            rearSuspension;
        public SatsumaHandbrakeNwhAdapter Handbrake => handbrake;
        public int SynchronizationCount { get; private set; }
        public bool LastSynchronizationHeldChassisKinematic {
            get;
            private set;
        }

        public void Configure(
            VehicleAssemblyController configuredAssemblyController,
            Rigidbody configuredChassis,
            NwhAssemblyWheelSupportController configuredWheelSupport,
            SatsumaFrontSteeringController configuredFrontSteering,
            SatsumaFrontSuspensionController configuredFrontSuspension,
            SatsumaRearNwhSuspensionController configuredRearSuspension,
            SatsumaHandbrakeNwhAdapter configuredHandbrake = null)
        {
            assemblyController = configuredAssemblyController;
            chassis = configuredChassis;
            wheelSupport = configuredWheelSupport;
            frontSteering = configuredFrontSteering;
            frontSuspension = configuredFrontSuspension;
            rearSuspension = configuredRearSuspension;
            handbrake = configuredHandbrake;
        }

        public bool TrySynchronizeRestoredPhysics(out string failure)
        {
            if (assemblyController == null || chassis == null ||
                wheelSupport == null || frontSteering == null ||
                frontSuspension == null || rearSuspension == null)
            {
                failure =
                    "Satsuma post-load physics synchronizer is incomplete.";
                return false;
            }

            if (!chassis.isKinematic)
            {
                failure =
                    "Satsuma post-load physics was released before synchronization.";
                return false;
            }

            LastSynchronizationHeldChassisKinematic = true;
            wheelSupport.RefreshSupport(force: true);
            frontSteering.ApplyNow();
            frontSuspension.ApplyNow();
            rearSuspension.ApplyNow();
            assemblyController.SynchronizeInstalledParts();
            handbrake?.ApplyNow();

            if (Application.isPlaying)
            {
                NwhAssemblyWheelSupportBinding[] bindings =
                    wheelSupport.Bindings;
                for (int index = 0; index < bindings.Length; index++)
                {
                    WheelController wheel = bindings[index].Wheel;
                    if (wheel != null && wheel.enabled)
                    {
                        wheel.WakeFromSleep();
                    }
                }
            }

            SynchronizationCount++;
            failure = string.Empty;
            return true;
        }
    }
}
