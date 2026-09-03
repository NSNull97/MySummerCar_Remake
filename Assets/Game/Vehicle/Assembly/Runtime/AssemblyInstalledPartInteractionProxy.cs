using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Keeps installed kinematic presentation parts raycastable without
    /// re-enabling their solid colliders inside the chassis compound. The
    /// trigger is attached to the part Rigidbody so it follows a moving
    /// suspension part instead of becoming a stale nested physics actor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AssemblyInstalledPartInteractionProxy : MonoBehaviour
    {
        [SerializeField] private PartInstance part;
        [SerializeField] private BoxCollider interactionCollider;

        public Collider InteractionCollider => interactionCollider;

        public void Configure(
            PartInstance ownerPart,
            Vector3 localCenter,
            Vector3 localSize)
        {
            part = ownerPart;
            interactionCollider = GetComponent<BoxCollider>();

            interactionCollider.center = localCenter;
            interactionCollider.size = new Vector3(
                Mathf.Max(0.075f, localSize.x),
                Mathf.Max(0.075f, localSize.y),
                Mathf.Max(0.075f, localSize.z));
            interactionCollider.isTrigger = true;
            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
            }

            interactionCollider.enabled = part != null && part.IsInstalled &&
                !part.IsAssemblyRoot;
        }

        private void Awake()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
            }

            RefreshAvailability();
        }
    }
}
