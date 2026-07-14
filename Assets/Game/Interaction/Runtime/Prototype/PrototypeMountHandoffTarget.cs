using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeMountHandoffTarget : MonoBehaviour, IMountHandoffTarget
    {
        [SerializeField]
        private Transform mountPose;

        [SerializeField]
        private string handoffPrompt = "Установить в крепление";

        private IPickupTarget mountedTarget;

        public string HandoffPrompt => handoffPrompt;

        public bool HasMountedTarget => mountedTarget != null;

        public bool CanAccept(IPickupTarget pickupTarget, in InteractionContext context)
        {
            return pickupTarget != null && pickupTarget.Body != null && mountedTarget == null && mountPose != null;
        }

        public void Accept(IPickupTarget pickupTarget, in InteractionContext context)
        {
            if (!CanAccept(pickupTarget, context))
            {
                throw new System.InvalidOperationException("Mount handoff was accepted without a valid target or free mount pose.");
            }

            mountedTarget = pickupTarget;
            Rigidbody body = pickupTarget.Body;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.position = mountPose.position;
            body.rotation = mountPose.rotation;
        }

        public void Configure(Transform pose)
        {
            mountPose = pose;
        }
    }
}
