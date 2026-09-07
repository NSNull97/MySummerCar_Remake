using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Keeps the isolated, player-only kinematic actor on its dynamic chassis.
    /// Parenting alone does not make a nested Rigidbody follow another actor.
    /// </summary>
    [DefaultExecutionOrder(800)]
    [DisallowMultipleComponent]
    public sealed class VehiclePlayerCollisionFollower : MonoBehaviour
    {
        [SerializeField] private Rigidbody chassis;
        [SerializeField] private Rigidbody playerCollisionBody;
        [SerializeField] private Vector3 chassisLocalPosition;
        [SerializeField] private Quaternion chassisLocalRotation = Quaternion.identity;
        public Rigidbody Chassis => chassis;
        public Rigidbody PlayerCollisionBody => playerCollisionBody;

        public void Configure(Rigidbody source, Rigidbody follower)
        {
            chassis = source;
            playerCollisionBody = follower;
            chassisLocalPosition = source.transform.InverseTransformPoint(follower.transform.position);
            chassisLocalRotation = Quaternion.Inverse(source.transform.rotation) * follower.transform.rotation;
            follower.interpolation = RigidbodyInterpolation.None;
        }

        private void FixedUpdate()
        {
            if (chassis == null || playerCollisionBody == null || !playerCollisionBody.isKinematic) return;
            playerCollisionBody.position = chassis.position + chassis.rotation * chassisLocalPosition;
            playerCollisionBody.rotation = chassis.rotation * chassisLocalRotation;
        }

        private void LateUpdate()
        {
            if (chassis == null || playerCollisionBody == null || !playerCollisionBody.isKinematic) return;
            playerCollisionBody.transform.SetPositionAndRotation(
                chassis.transform.TransformPoint(chassisLocalPosition),
                chassis.transform.rotation * chassisLocalRotation);
        }
    }
}
