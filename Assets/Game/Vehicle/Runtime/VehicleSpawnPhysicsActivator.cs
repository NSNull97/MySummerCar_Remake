using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Gives a newly spawned or restored dynamic chassis a bounded pair of
    /// physics ticks in the awake state. Sleeping is an optimization, not
    /// authoritative vehicle state, and must not leave an unsupported body
    /// suspended after its collision contract changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class VehicleSpawnPhysicsActivator : MonoBehaviour
    {
        [SerializeField]
        private Rigidbody chassis;

        [SerializeField, Min(1)]
        private int activationFixedSteps = 2;

        private int remainingFixedSteps;

        public Rigidbody Chassis => chassis;

        public int ActivationFixedSteps => activationFixedSteps;

        public void Configure(
            Rigidbody configuredChassis,
            int configuredActivationFixedSteps = 2)
        {
            chassis = configuredChassis;
            activationFixedSteps = Mathf.Max(
                1,
                configuredActivationFixedSteps);
            remainingFixedSteps = activationFixedSteps;
            enabled = chassis != null;
        }

        private void Awake()
        {
            if (chassis == null)
            {
                chassis = GetComponent<Rigidbody>();
            }
        }

        private void OnEnable()
        {
            remainingFixedSteps = Mathf.Max(1, activationFixedSteps);
        }

        private void FixedUpdate()
        {
            if (chassis == null || chassis.isKinematic || !chassis.useGravity)
            {
                enabled = false;
                return;
            }

            chassis.WakeUp();
            remainingFixedSteps--;
            if (remainingFixedSteps <= 0)
            {
                enabled = false;
            }
        }
    }
}
