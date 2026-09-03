using System;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StableEntityIdAuthoring))]
    public sealed class PartInstance : MonoBehaviour,
        IInteractionDisplayTarget,
        IInteractionLocalizationTarget
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
        private Transform installedPose;
        private MountPointAuthoring installedMountPoint;
        private AssemblyInstalledPhysicsLink installedPhysicsLink;
        private AssemblyHingedPartInteractionTarget hingedPartInteraction;
        private bool hingeInteractionResolved;
        private RigidbodyDefaults bodyDefaults;
        private ColliderDefault[] colliderDefaults =
            Array.Empty<ColliderDefault>();
        private bool colliderDefaultsCaptured;
        private bool initialized;

        public PartDefinition Definition => definition;

        public string InteractionDisplayName =>
            ShouldShowInteractionTitle
                ? definition.DisplayName
                : string.Empty;

        public string InteractionLocalizationKey =>
            ShouldShowInteractionTitle
                ? definition.DefinitionId
                : string.Empty;

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

        private bool ShouldShowInteractionTitle =>
            definition != null && !assemblyRoot && !IsInstalled;

        internal Transform InstalledPose => installedPose;

        public bool UsesDynamicInstalledPhysics =>
            installedPhysicsLink != null && installedPhysicsLink.IsAttached;

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
            InstallAt(mountPose, null, mountId, null);
        }

        internal void InstallAt(
            MountPointAuthoring mountPoint,
            string mountId,
            Quaternion? restoredWorldRotation = null)
        {
            if (mountPoint == null)
            {
                throw new ArgumentNullException(nameof(mountPoint));
            }

            InstallAt(
                mountPoint.Pose,
                mountPoint,
                mountId,
                restoredWorldRotation);
        }

        private void InstallAt(
            Transform mountPose,
            MountPointAuthoring mountPoint,
            string mountId,
            Quaternion? restoredWorldRotation)
        {
            EnsureInitialized();
            if (mountPose == null)
            {
                throw new ArgumentNullException(nameof(mountPose));
            }

            // Mount authoring scale is presentation data and must never resize a physical part.
            installedPose = mountPose;
            installedMountPoint = mountPoint;
            transform.SetParent(mountPose, true);
            transform.SetPositionAndRotation(mountPose.position, mountPose.rotation);
            if (targetBody != null)
            {
                // A dynamic Rigidbody owns its world pose independently of the
                // Transform hierarchy. Teleport both representations before a
                // joint is created so PhysX cannot preserve the loose-part pose
                // as an accidental joint offset on the first fixed step.
                targetBody.position = mountPose.position;
                targetBody.rotation = mountPose.rotation;
            }
            if (installedPhysicsLink == null)
            {
                installedPhysicsLink = GetComponent<AssemblyInstalledPhysicsLink>();
            }

            bool attachedPhysically = installedPhysicsLink != null &&
                mountPoint != null &&
                installedPhysicsLink.TryAttach(mountPoint);
            EnsureColliderDefaultsCaptured();
            if (targetBody != null && !attachedPhysically)
            {
                ConfigureKinematicInstalledBody();
            }

            pickupTarget?.SetPickupEnabled(false);
            runtimeState.SetInstalled(mountId, false);
            ApplyInstalledColliderState(attachedPhysically);
            GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true)
                ?.RefreshAvailability();
            ResolveHingedPartInteraction()
                ?.NotifyInstalled(mountPoint, restoredWorldRotation);
        }

        internal void MarkAssemblyRoot()
        {
            EnsureInitialized();
            runtimeState.SetInstalled(string.Empty, true);
        }

        internal void Detach(Transform loosePartsParent, Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            ResolveHingedPartInteraction()?.NotifyDetached();
            installedPhysicsLink?.DetachJoint();
            transform.SetParent(loosePartsParent != null ? loosePartsParent : detachedParent, true);
            transform.SetPositionAndRotation(position, rotation);
            installedPose = null;
            installedMountPoint = null;
            bodyDefaults.Restore(targetBody);
            RestoreColliderDefaults();
            pickupTarget?.SetPickupEnabled(true);
            runtimeState.SetLoose(position, rotation);
            GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true)
                ?.RefreshAvailability();
        }

        /// <summary>
        /// A nested Rigidbody remains a separate PhysX actor even when its
        /// Transform is parented below the chassis. Installed bodies therefore
        /// need an explicit pose copy from their authoritative mount after the
        /// chassis or an owning subassembly moves.
        /// </summary>
        internal void SynchronizeInstalledPose()
        {
            if (!IsInstalled || assemblyRoot || installedPose == null ||
                UsesDynamicInstalledPhysics)
            {
                return;
            }

            Vector3 position = installedPose.position;
            Quaternion rotation = installedPose.rotation;
            AssemblyHingedPartInteractionTarget hingedPart =
                ResolveHingedPartInteraction();
            if (hingedPart != null && hingedPart.IsAttachedToHinge)
            {
                hingedPart.SynchronizeInstalledPose(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
            if (targetBody != null)
            {
                targetBody.position = position;
                targetBody.rotation = rotation;
            }
        }

        internal void RefreshLoosePose()
        {
            if (!IsInstalled)
            {
                runtimeState.SetLoose(transform.position, transform.rotation);
            }
        }

        /// <summary>
        /// Switches an already-installed structural part between a real jointed
        /// PhysX actor and NWH-owned kinematic presentation. This is used only
        /// at an explicit suspension-authority handoff; it never invents a
        /// physical link for parts without authored link metadata.
        /// </summary>
        public bool SetInstalledPhysicalAttachmentActive(bool active)
        {
            EnsureInitialized();
            if (!IsInstalled || assemblyRoot || installedPose == null ||
                installedMountPoint == null || installedPhysicsLink == null)
            {
                return false;
            }

            bool wasActive = UsesDynamicInstalledPhysics;
            if (wasActive == active)
            {
                if (active && installedPhysicsLink.RequiresOwnerReattach(
                        installedMountPoint))
                {
                    bool reattached = installedPhysicsLink.TryAttach(
                        installedMountPoint);
                    ApplyInstalledColliderState(reattached);
                    if (reattached)
                    {
                        GetComponentInParent<VehicleAssemblyController>()
                            ?.NotifyInstalledPhysicsStateChanged();
                    }

                    return reattached;
                }

                return true;
            }

            bool isActive;
            if (active)
            {
                transform.SetPositionAndRotation(
                    installedPose.position,
                    installedPose.rotation);
                if (targetBody != null)
                {
                    targetBody.position = installedPose.position;
                    targetBody.rotation = installedPose.rotation;
                }

                isActive = installedPhysicsLink.TryAttach(
                    installedMountPoint);
                ApplyInstalledColliderState(isActive);
            }
            else
            {
                installedPhysicsLink.DetachJoint();
                ConfigureKinematicInstalledBody();
                ApplyInstalledColliderState(attachedPhysically: false);
                isActive = false;
            }

            if (wasActive != isActive)
            {
                GetComponentInParent<VehicleAssemblyController>()
                    ?.NotifyInstalledPhysicsStateChanged();
            }

            return isActive == active;
        }

        private void Awake()
        {
            // Generated Phase 1 prefabs predate target-title presentation.
            // Register explicitly at runtime so existing serialized capability
            // arrays remain compatible and no content rebuild is required.
            GetComponent<InteractionTargetHost>()?.AddCapability(this);
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            detachedParent = transform.parent;
            installedPhysicsLink = GetComponent<AssemblyInstalledPhysicsLink>();
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

        private void EnsureColliderDefaultsCaptured()
        {
            if (colliderDefaultsCaptured)
            {
                return;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            colliderDefaults = new ColliderDefault[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                colliderDefaults[index] = new ColliderDefault(
                    colliders[index],
                    colliders[index] != null && colliders[index].enabled);
            }

            colliderDefaultsCaptured = true;
        }

        private void ApplyInstalledColliderState(bool attachedPhysically)
        {
            bool keepSolidColliders = attachedPhysically ||
                ResolveHingedPartInteraction() != null;
            for (int index = 0; index < colliderDefaults.Length; index++)
            {
                Collider collider = colliderDefaults[index].Collider;
                if (collider == null ||
                    collider.GetComponent<
                        AssemblyInstalledPartInteractionProxy>() != null)
                {
                    continue;
                }

                collider.enabled = colliderDefaults[index].Enabled &&
                    (keepSolidColliders || collider.isTrigger);
            }
        }

        private void ConfigureKinematicInstalledBody()
        {
            if (targetBody == null)
            {
                return;
            }

            if (!targetBody.isKinematic)
            {
                targetBody.linearVelocity = Vector3.zero;
                targetBody.angularVelocity = Vector3.zero;
            }

            targetBody.useGravity = false;
            targetBody.isKinematic = true;
            // Installed presentation is kinematic, but its dedicated trigger
            // remains queryable by the player ray. Solid loose-part colliders
            // are controlled independently by ApplyInstalledColliderState.
            targetBody.detectCollisions = true;
            targetBody.collisionDetectionMode =
                CollisionDetectionMode.Discrete;
        }

        private void RestoreColliderDefaults()
        {
            for (int index = 0; index < colliderDefaults.Length; index++)
            {
                Collider collider = colliderDefaults[index].Collider;
                if (collider != null)
                {
                    collider.enabled = colliderDefaults[index].Enabled;
                }
            }
        }

        private AssemblyHingedPartInteractionTarget
            ResolveHingedPartInteraction()
        {
            if (!hingeInteractionResolved)
            {
                hingedPartInteraction =
                    GetComponent<AssemblyHingedPartInteractionTarget>();
                hingeInteractionResolved = true;
            }

            return hingedPartInteraction;
        }

        private readonly struct RigidbodyDefaults
        {
            private RigidbodyDefaults(
                bool useGravity,
                bool isKinematic,
                bool detectCollisions,
                CollisionDetectionMode collisionMode,
                RigidbodyInterpolation interpolation,
                int solverIterations,
                int solverVelocityIterations)
            {
                UseGravity = useGravity;
                IsKinematic = isKinematic;
                DetectCollisions = detectCollisions;
                CollisionMode = collisionMode;
                Interpolation = interpolation;
                SolverIterations = solverIterations;
                SolverVelocityIterations = solverVelocityIterations;
            }

            private bool UseGravity { get; }

            private bool IsKinematic { get; }

            private bool DetectCollisions { get; }

            private CollisionDetectionMode CollisionMode { get; }

            private RigidbodyInterpolation Interpolation { get; }

            private int SolverIterations { get; }

            private int SolverVelocityIterations { get; }

            public static RigidbodyDefaults Capture(Rigidbody body)
            {
                return body == null
                    ? default
                    : new RigidbodyDefaults(
                        body.useGravity,
                        body.isKinematic,
                        body.detectCollisions,
                        body.collisionDetectionMode,
                        body.interpolation,
                        body.solverIterations,
                        body.solverVelocityIterations);
            }

            public static RigidbodyDefaults CreateLooseDefault(Rigidbody body)
            {
                return body == null
                    ? default
                    : new RigidbodyDefaults(
                        true,
                        false,
                        true,
                        CollisionDetectionMode.Continuous,
                        body.interpolation,
                        body.solverIterations,
                        body.solverVelocityIterations);
            }

            public void Restore(Rigidbody body)
            {
                if (body == null)
                {
                    return;
                }

                body.useGravity = UseGravity;
                body.isKinematic = IsKinematic;
                body.detectCollisions = DetectCollisions;
                body.collisionDetectionMode = CollisionMode;
                body.interpolation = Interpolation;
                body.solverIterations = SolverIterations;
                body.solverVelocityIterations = SolverVelocityIterations;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private readonly struct ColliderDefault
        {
            public ColliderDefault(Collider collider, bool enabled)
            {
                Collider = collider;
                Enabled = enabled;
            }

            public Collider Collider { get; }

            public bool Enabled { get; }
        }
    }
}
