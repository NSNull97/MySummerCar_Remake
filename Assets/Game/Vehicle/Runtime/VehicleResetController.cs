using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class VehicleResetController : MonoBehaviour
    {
        [SerializeField] private VehicleSimulationHost host;
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private Transform resetPose;

        private Vector3 capturedSpawnPosition;
        private Quaternion capturedSpawnRotation = Quaternion.identity;
        private bool hasCapturedSpawnPose;

        public VehicleSimulationHost Host => host;

        public Rigidbody Chassis => chassis;

        public Transform ResetPose => resetPose;

        public int ResetCount { get; private set; }

        private void Awake()
        {
            CaptureConfiguredSpawnPose();
        }

        public void Configure(
            VehicleSimulationHost simulationHost,
            Rigidbody targetChassis,
            Transform targetResetPose)
        {
            host = simulationHost;
            chassis = targetChassis;
            resetPose = targetResetPose;
            CaptureConfiguredSpawnPose();
        }

        public void SetSpawnPose(Vector3 position, Quaternion rotation)
        {
            if (!VehicleSimulationMath.IsFinite(position) ||
                !VehicleSimulationMath.IsFinite(new Vector3(rotation.x, rotation.y, rotation.z)) ||
                !VehicleSimulationMath.IsFinite(rotation.w))
            {
                return;
            }

            capturedSpawnPosition = position;
            capturedSpawnRotation = rotation.normalized;
            hasCapturedSpawnPose = true;
        }

        public void ResetToSpawn()
        {
            if (chassis == null)
            {
                Debug.LogError("M06 reset controller has no proxy Rigidbody.", this);
                return;
            }

            if (resetPose != null)
            {
                SetSpawnPose(resetPose.position, resetPose.rotation);
            }
            else if (!hasCapturedSpawnPose)
            {
                SetSpawnPose(chassis.position, chassis.rotation);
            }

            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            chassis.position = capturedSpawnPosition;
            chassis.rotation = capturedSpawnRotation;
            chassis.Sleep();
            Physics.SyncTransforms();
            chassis.WakeUp();
            host?.ResetSimulation();
            ResetCount++;
        }

        private void CaptureConfiguredSpawnPose()
        {
            if (resetPose != null)
            {
                SetSpawnPose(resetPose.position, resetPose.rotation);
            }
            else if (chassis != null)
            {
                SetSpawnPose(chassis.position, chassis.rotation);
            }
        }
    }
}
