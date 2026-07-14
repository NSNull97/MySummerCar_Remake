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

        [SerializeField]
        private bool pickupEnabled = true;

        private bool isCarried;

        public string PickupPrompt => pickupPrompt;

        public Rigidbody Body => targetBody;

        public StableEntityId StableId =>
            stableIdAuthoring != null && stableIdAuthoring.TryGetStableId(out StableEntityId id) ? id : default;

        public bool IsCarried => isCarried;

        public bool CanPickup(in InteractionContext context)
        {
            return pickupEnabled &&
                !isCarried &&
                targetBody != null &&
                !targetBody.isKinematic &&
                targetBody.mass <= maximumCarryMassKilograms &&
                StableId.IsValid;
        }

        public void NotifyPickedUp(in InteractionContext context)
        {
            isCarried = true;
        }

        public void NotifyReleased(PickupReleaseReason reason)
        {
            isCarried = false;
        }

        public void Configure(
            Rigidbody body,
            StableEntityIdAuthoring identity,
            string prompt,
            float maximumMassKilograms)
        {
            targetBody = body;
            stableIdAuthoring = identity;
            pickupPrompt = string.IsNullOrWhiteSpace(prompt) ? "Поднять" : prompt;
            maximumCarryMassKilograms = Mathf.Max(0.01f, maximumMassKilograms);
        }

        private void Reset()
        {
            targetBody = GetComponent<Rigidbody>();
            stableIdAuthoring = GetComponent<StableEntityIdAuthoring>();
        }
    }
}
