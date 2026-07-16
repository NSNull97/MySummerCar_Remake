using System;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>Presentation-only wheel pose bridge for the raycast backend.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class VehicleSimulationPresenter : MonoBehaviour
    {
        [SerializeField] private VehicleSimulationHost host;
        [SerializeField] private PrototypeRaycastWheelPhysicsBackend backend;
        [SerializeField] private Transform[] wheelVisuals = Array.Empty<Transform>();

        private Quaternion[] authoredRelativeRotations = Array.Empty<Quaternion>();

        public VehicleSimulationHost Host => host;

        public PrototypeRaycastWheelPhysicsBackend Backend => backend;

        public Transform[] WheelVisuals => wheelVisuals;

        private void Awake()
        {
            ResolveBackend();
            CaptureAuthoredRotations();
        }

        private void LateUpdate()
        {
            if (backend == null || wheelVisuals == null)
            {
                return;
            }

            for (int index = 0; index < wheelVisuals.Length && index < backend.WheelCount; index++)
            {
                Transform visual = wheelVisuals[index];
                if (visual == null ||
                    !backend.TryGetVisualState(index, out RaycastWheelVisualState state))
                {
                    continue;
                }

                Quaternion authoredRotation = index < authoredRelativeRotations.Length
                    ? authoredRelativeRotations[index]
                    : Quaternion.identity;
                visual.SetPositionAndRotation(
                    state.WorldCenter,
                    state.WorldSteeringRotation *
                    Quaternion.AngleAxis(state.SpinDegrees, Vector3.right) *
                    authoredRotation);
            }
        }

        public void Configure(VehicleSimulationHost simulationHost, Transform[] visuals)
        {
            host = simulationHost;
            wheelVisuals = visuals ?? Array.Empty<Transform>();
            ResolveBackend();
            CaptureAuthoredRotations();
        }

        public void Configure(
            VehicleSimulationHost simulationHost,
            PrototypeRaycastWheelPhysicsBackend physicsBackend,
            Transform[] visuals)
        {
            host = simulationHost;
            backend = physicsBackend;
            wheelVisuals = visuals ?? Array.Empty<Transform>();
            CaptureAuthoredRotations();
        }

        private void ResolveBackend()
        {
            if (backend == null && host != null)
            {
                backend = host.BackendComponent as PrototypeRaycastWheelPhysicsBackend;
            }
        }

        private void CaptureAuthoredRotations()
        {
            if (wheelVisuals == null)
            {
                authoredRelativeRotations = Array.Empty<Quaternion>();
                return;
            }

            authoredRelativeRotations = new Quaternion[wheelVisuals.Length];
            Quaternion chassisRotation = backend != null && backend.Chassis != null
                ? backend.Chassis.rotation
                : Quaternion.identity;
            Quaternion inverseChassisRotation = Quaternion.Inverse(chassisRotation);
            for (int index = 0; index < wheelVisuals.Length; index++)
            {
                Transform visual = wheelVisuals[index];
                authoredRelativeRotations[index] = visual != null
                    ? inverseChassisRotation * visual.rotation
                    : Quaternion.identity;
            }
        }
    }
}
