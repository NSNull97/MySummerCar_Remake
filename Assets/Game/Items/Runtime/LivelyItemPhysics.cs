using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// Converts residual tangential velocity at a real contact point into
    /// angular velocity. This supplements PhysX friction for small legacy item
    /// proxies whose simple colliders otherwise tend to slide without rolling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LivelyItemPhysics : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float minimumSlipSpeed = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float contactSpinTransfer = 0.72f;

        [SerializeField, Min(0f)]
        private float maximumAngularAcceleration = 55f;

        [SerializeField, Min(0f)]
        private float maximumAngularSpeed = 35f;

        private Rigidbody targetBody;

        private void Awake()
        {
            targetBody = GetComponent<Rigidbody>();
        }

        private void OnCollisionStay(Collision collision)
        {
            if (targetBody == null || targetBody.isKinematic ||
                !targetBody.useGravity || collision.contactCount == 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            Rigidbody surfaceBody =
                contact.otherCollider != null
                    ? contact.otherCollider.attachedRigidbody
                    : null;
            Vector3 surfaceVelocity =
                surfaceBody != null && surfaceBody != targetBody
                    ? surfaceBody.GetPointVelocity(contact.point)
                    : Vector3.zero;
            Vector3 relativePointVelocity =
                targetBody.GetPointVelocity(contact.point) - surfaceVelocity;
            Vector3 tangentialVelocity = Vector3.ProjectOnPlane(
                relativePointVelocity,
                contact.normal);
            float slipSpeed = tangentialVelocity.magnitude;
            if (slipSpeed < minimumSlipSpeed)
            {
                return;
            }

            Vector3 rotationAxis = Vector3.Cross(
                contact.normal,
                tangentialVelocity / slipSpeed);
            if (rotationAxis.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float contactRadius = Mathf.Max(
                0.03f,
                Mathf.Abs(Vector3.Dot(
                    targetBody.worldCenterOfMass - contact.point,
                    contact.normal)));
            float requestedCorrection =
                slipSpeed / contactRadius * contactSpinTransfer;
            float appliedCorrection = Mathf.Min(
                requestedCorrection,
                maximumAngularAcceleration * Time.fixedDeltaTime);
            targetBody.angularVelocity = Vector3.ClampMagnitude(
                targetBody.angularVelocity +
                rotationAxis.normalized * appliedCorrection,
                maximumAngularSpeed);
            targetBody.WakeUp();
        }
    }
}
