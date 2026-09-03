using UnityEngine;

namespace MSC.Vehicle.ItemsIntegration
{
    /// <summary>
    /// A broad, explicit project-owned jacking pad. It follows the chassis and
    /// replaces the donor's needlessly tiny interaction volume without making
    /// arbitrary body surfaces liftable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class VehicleJackLiftPoint : MonoBehaviour
    {
        [SerializeField] private Rigidbody chassis;
        [SerializeField, Min(0.05f)] private float horizontalCaptureRadius =
            0.2f;

        public Rigidbody Chassis => chassis;
        public float HorizontalCaptureRadius => horizontalCaptureRadius;
        public Vector3 ContactPosition => transform.position;

        public void Configure(
            Rigidbody targetChassis,
            float configuredCaptureRadius)
        {
            chassis = targetChassis;
            horizontalCaptureRadius = Mathf.Max(
                0.05f,
                configuredCaptureRadius);
            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = horizontalCaptureRadius;
        }
    }
}
