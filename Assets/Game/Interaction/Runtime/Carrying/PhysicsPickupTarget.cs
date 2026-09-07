using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Carrying
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(StableEntityIdAuthoring))]
    public sealed class PhysicsPickupTarget : MonoBehaviour, IPickupTarget
    {
        [SerializeField]
        private Rigidbody targetBody;

        [SerializeField]
        private StableEntityIdAuthoring stableIdAuthoring;

        [SerializeField]
        private string pickupPrompt = "Поднять";

        [SerializeField, Min(0.01f)]
        private float maximumCarryMassKilograms = 35f;

        [SerializeField] private bool allowAssemblyMassDebugOverride;

        [SerializeField]
        private bool pickupEnabled = true;

        [SerializeField]
        private bool restoreGravityWhenReleased;

        [SerializeField]
        private bool allowKinematicPickup;

        private bool isCarried;

        public string PickupPrompt => pickupPrompt;

        public Rigidbody Body => targetBody;

        public StableEntityId StableId =>
            stableIdAuthoring != null && stableIdAuthoring.TryGetStableId(out StableEntityId id) ? id : default;

        public bool IsCarried => isCarried;
        public float MaximumCarryMassKilograms => maximumCarryMassKilograms;

        /// <summary>
        /// Loose world items use ordinary dynamic Rigidbody physics. This is
        /// authored explicitly because mounted vehicle parts can share the
        /// pickup boundary while legitimately remaining kinematic.
        /// </summary>
        public bool UsesGravityWhenLoose => restoreGravityWhenReleased;

        public bool CanPickup(in InteractionContext context)
        {
            return pickupEnabled &&
                !isCarried &&
                targetBody != null &&
                (!targetBody.isKinematic || allowKinematicPickup) &&
                (targetBody.mass <= maximumCarryMassKilograms || HasAssemblyMassOverride(context)) &&
                StableId.IsValid;
        }

        private bool HasAssemblyMassOverride(in InteractionContext context) =>
            allowAssemblyMassDebugOverride && context.Interactor != null &&
            context.Interactor.GetComponentInParent<AssemblyCarryDebugOverride>() is
                AssemblyCarryDebugOverride debug && debug.IgnoreAssemblyMassLimit;

        public void ConfigureAssemblyCarryLimit(float maximumMassKilograms)
        {
            maximumCarryMassKilograms = Mathf.Max(0.01f, maximumMassKilograms);
            allowAssemblyMassDebugOverride = true;
        }

        public void NotifyPickedUp(in InteractionContext context)
        {
            isCarried = true;
        }

        public void NotifyReleased(PickupReleaseReason reason)
        {
            isCarried = false;
            if (!restoreGravityWhenReleased || targetBody == null)
            {
                return;
            }

            targetBody.detectCollisions = true;
            targetBody.isKinematic = false;
            targetBody.useGravity = true;
            targetBody.WakeUp();
        }

        public void Configure(
            Rigidbody body,
            StableEntityIdAuthoring identity,
            string prompt,
            float maximumMassKilograms)
        {
            Configure(
                body,
                identity,
                prompt,
                maximumMassKilograms,
                useGravityWhenLoose: false);
        }

        public void Configure(
            Rigidbody body,
            StableEntityIdAuthoring identity,
            string prompt,
            float maximumMassKilograms,
            bool useGravityWhenLoose,
            bool canPickupKinematicBody = false)
        {
            targetBody = body;
            stableIdAuthoring = identity;
            pickupPrompt = string.IsNullOrWhiteSpace(prompt) ? "Поднять" : prompt;
            maximumCarryMassKilograms = Mathf.Max(0.01f, maximumMassKilograms);
            restoreGravityWhenReleased = useGravityWhenLoose;
            allowKinematicPickup = canPickupKinematicBody;
        }

        public void SetPrompt(string prompt)
        {
            pickupPrompt = string.IsNullOrWhiteSpace(prompt)
                ? "Предмет"
                : prompt;
        }

        public void SetPickupEnabled(bool enabled)
        {
            pickupEnabled = enabled;
        }

        private void Reset()
        {
            targetBody = GetComponent<Rigidbody>();
            stableIdAuthoring = GetComponent<StableEntityIdAuthoring>();
        }
    }
}
