using System;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StableEntityIdAuthoring))]
    public sealed class PartInstance : MonoBehaviour
    {
        [SerializeField]
        private PartDefinition definition;

        [SerializeField]
        private StableEntityIdAuthoring stableIdAuthoring;

        [SerializeField]
        private Rigidbody targetBody;

        [SerializeField]
        private PhysicsPickupTarget pickupTarget;

        [SerializeField]
        private bool assemblyRoot;

        [SerializeField]
        private string initialMountId = string.Empty;

        private PartRuntimeState runtimeState;
        private Transform detachedParent;
        private RigidbodyDefaults bodyDefaults;
        private bool initialized;

        public PartDefinition Definition => definition;

        public Rigidbody Body => targetBody;

        public PhysicsPickupTarget PickupTarget => pickupTarget;

        public PartRuntimeState RuntimeState
        {
            get
            {
                EnsureInitialized();
                return runtimeState;
            }
        }

        public bool IsAssemblyRoot => assemblyRoot;

        public bool IsInstalled => RuntimeState.IsInstalled;

        public string InitialMountId => initialMountId;

        public StableEntityId StableId =>
            stableIdAuthoring != null && stableIdAuthoring.TryGetStableId(out StableEntityId value)
                ? value
                : default;

        public void Configure(
            PartDefinition partDefinition,
            StableEntityIdAuthoring identity,
            Rigidbody body,
            PhysicsPickupTarget pickup,
            bool isRoot,
            string mountedAtStart)
        {
            definition = partDefinition;
            stableIdAuthoring = identity;
            targetBody = body;
            pickupTarget = pickup;
            assemblyRoot = isRoot;
            initialMountId = mountedAtStart ?? string.Empty;
            initialized = false;
            EnsureInitialized();
        }

        internal void InstallAt(Transform mountPose, string mountId)
        {
            EnsureInitialized();
            if (mountPose == null)
            {
                throw new ArgumentNullException(nameof(mountPose));
            }

            // Mount authoring scale is presentation data and must never resize a physical part.
            transform.SetParent(mountPose, true);
            transform.SetPositionAndRotation(mountPose.position, mountPose.rotation);
            if (targetBody != null)
            {
                if (!targetBody.isKinematic)
                {
                    targetBody.linearVelocity = Vector3.zero;
                    targetBody.angularVelocity = Vector3.zero;
                }

                targetBody.useGravity = false;
                targetBody.isKinematic = true;
                targetBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            runtimeState.SetInstalled(mountId, false);
        }

        internal void MarkAssemblyRoot()
        {
            EnsureInitialized();
            runtimeState.SetInstalled(string.Empty, true);
            if (targetBody != null)
            {
                if (!targetBody.isKinematic)
                {
                    targetBody.linearVelocity = Vector3.zero;
                    targetBody.angularVelocity = Vector3.zero;
                }

                targetBody.useGravity = false;
                targetBody.isKinematic = true;
            }
        }

        internal void Detach(Transform loosePartsParent, Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            transform.SetParent(loosePartsParent != null ? loosePartsParent : detachedParent, true);
            transform.SetPositionAndRotation(position, rotation);
            bodyDefaults.Restore(targetBody);
            runtimeState.SetLoose(position, rotation);
        }

        internal void RefreshLoosePose()
        {
            if (!IsInstalled)
            {
                runtimeState.SetLoose(transform.position, transform.rotation);
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            detachedParent = transform.parent;
            bodyDefaults = RigidbodyDefaults.Capture(targetBody);
            if (!assemblyRoot && !string.IsNullOrEmpty(initialMountId))
            {
                bodyDefaults = RigidbodyDefaults.CreateLooseDefault(targetBody);
            }

            runtimeState = new PartRuntimeState();
            runtimeState.SetIdentity(
                StableId.IsValid ? StableId.Value : string.Empty,
                definition != null ? definition.DefinitionId : string.Empty);
            runtimeState.SetLoose(transform.position, transform.rotation);
            initialized = true;
        }

        private readonly struct RigidbodyDefaults
        {
            private RigidbodyDefaults(
                bool useGravity,
                bool isKinematic,
                CollisionDetectionMode collisionMode,
                RigidbodyInterpolation interpolation)
            {
                UseGravity = useGravity;
                IsKinematic = isKinematic;
                CollisionMode = collisionMode;
                Interpolation = interpolation;
            }

            private bool UseGravity { get; }

            private bool IsKinematic { get; }

            private CollisionDetectionMode CollisionMode { get; }

            private RigidbodyInterpolation Interpolation { get; }

            public static RigidbodyDefaults Capture(Rigidbody body)
            {
                return body == null
                    ? default
                    : new RigidbodyDefaults(
                        body.useGravity,
                        body.isKinematic,
                        body.collisionDetectionMode,
                        body.interpolation);
            }

            public static RigidbodyDefaults CreateLooseDefault(Rigidbody body)
            {
                return body == null
                    ? default
                    : new RigidbodyDefaults(
                        true,
                        false,
                        CollisionDetectionMode.Continuous,
                        body.interpolation);
            }

            public void Restore(Rigidbody body)
            {
                if (body == null)
                {
                    return;
                }

                body.useGravity = UseGravity;
                body.isKinematic = IsKinematic;
                body.collisionDetectionMode = CollisionMode;
                body.interpolation = Interpolation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
