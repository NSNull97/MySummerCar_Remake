using System;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Transfers the installed lever/cable state to the existing rear contact
    /// authority. It neither creates contacts nor locks the vehicle Rigidbody.
    /// </summary>
    // Support toggles at -10, service commands at 0, NWH simulates at 100.
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class SatsumaHandbrakeNwhAdapter : MonoBehaviour
    {
        // Locked donor rear axle: 500 N m, balance 0 (rear multiplier 1).
        // Wheel.FixedUpdate multiplies brake-friction torques by 2; NWH's
        // BrakeTorque is already the full torque, so do not halve it again.
        public const float MaximumWheelTorqueNewtonMeters = 1000f;

        [SerializeField] private SatsumaHandbrakeController controller;
        [SerializeField] private NwhWheelPhysicsBackend backend;
        [SerializeField] private WheelController rearLeftWheel;
        [SerializeField] private WheelController rearRightWheel;

        public SatsumaHandbrakeController Controller => controller;
        public NwhWheelPhysicsBackend Backend => backend;
        public WheelController RearLeftWheel => rearLeftWheel;
        public WheelController RearRightWheel => rearRightWheel;

        public void Configure(
            SatsumaHandbrakeController configuredController,
            NwhWheelPhysicsBackend configuredBackend,
            WheelController configuredRearLeftWheel,
            WheelController configuredRearRightWheel)
        {
            ClearTorque();
            controller = configuredController;
            backend = configuredBackend;
            rearLeftWheel = configuredRearLeftWheel;
            rearRightWheel = configuredRearRightWheel;
            if (!TryValidate(out string failure))
            {
                throw new ArgumentException(failure);
            }

            ApplyNow();
        }

        public bool TryValidate(out string failure)
        {
            if (controller == null || backend == null ||
                rearLeftWheel == null || rearRightWheel == null ||
                rearLeftWheel == rearRightWheel ||
                Array.IndexOf(backend.Wheels, rearLeftWheel) < 0 ||
                Array.IndexOf(backend.Wheels, rearRightWheel) < 0)
            {
                failure = "Handbrake requires its controller and two distinct rear wheels of the configured backend.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void OnEnable() => ApplyNow();
        private void FixedUpdate() => ApplyNow();
        private void OnDisable() => ClearTorque();

        public void ApplyNow()
        {
            if (backend == null || rearLeftWheel == null || rearRightWheel == null)
            {
                return;
            }

            float input = isActiveAndEnabled && controller != null &&
                controller.isActiveAndEnabled ? controller.BrakeInput01 : 0f;
            float torque = Mathf.Clamp01(input) * MaximumWheelTorqueNewtonMeters;
            backend.SetParkingBrakeTorque(rearLeftWheel, torque);
            backend.SetParkingBrakeTorque(rearRightWheel, torque);
        }

        private void ClearTorque()
        {
            if (backend == null)
            {
                return;
            }

            if (rearLeftWheel != null && Array.IndexOf(backend.Wheels, rearLeftWheel) >= 0)
            {
                backend.SetParkingBrakeTorque(rearLeftWheel, 0f);
            }
            if (rearRightWheel != null && Array.IndexOf(backend.Wheels, rearRightWheel) >= 0)
            {
                backend.SetParkingBrakeTorque(rearRightWheel, 0f);
            }
        }
    }
}
