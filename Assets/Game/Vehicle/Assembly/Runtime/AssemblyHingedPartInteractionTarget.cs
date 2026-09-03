using System.Collections.Generic;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Donor-profiled held interaction for an installed body panel. The panel
    /// remains a real Rigidbody connected by a HingeJoint: mouse input applies
    /// torque, releasing the mouse preserves inertia, and world collision can
    /// physically stop the panel. The assembly graph remains attachment and
    /// removal authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyHingedPartInteractionTarget : MonoBehaviour,
        IContinuousContextInteractionTarget
    {
        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private PartInstance part;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private string openPrompt = "Открыть";
        [SerializeField] private string closePrompt = "Закрыть";
        [SerializeField] private GameObject[] installedOnlyPresentationObjects =
            System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] detachedOnlyPresentationObjects =
            System.Array.Empty<GameObject>();

        private AssemblyHingeMountAuthoring activeMount;
        private AssemblyInstalledPhysicsLink installedPhysicsLink;
        private HingeJoint activeHinge;
        private FixedJoint closedLatch;
        private float openNormalized;
        private bool targetOpen;
        private bool held;
        private bool closedLatched;
        private bool openHeld;
        private bool releaseGranted;
        private int closeLatchStableFixedSteps;
        private readonly List<ColliderPair> ignoredConnectedBodyCollisions =
            new List<ColliderPair>();

        private const int RequiredCloseLatchStableFixedSteps = 2;

        public string InteractionPrompt =>
            $"Удерживайте ЛКМ — {openPrompt} / ПКМ — {closePrompt}";

        public bool UsesDirectionalHold => CanOperate();

        public bool IsAttachedToHinge => activeMount != null &&
            activeHinge != null && installedPhysicsLink != null &&
            installedPhysicsLink.IsAttached;

        public float OpenNormalized
        {
            get
            {
                RefreshHingeMeasurement();
                return openNormalized;
            }
        }

        public bool TargetOpen => targetOpen;

        public bool IsClosedLatched => closedLatched && closedLatch != null;

        public bool IsOpenHeld => openHeld && activeHinge != null;

        public bool RequiresReleaseBeforeOpening => activeMount != null &&
            activeMount.RequiresReleaseBeforeOpening;

        public bool IsReleasedForOpening => !RequiresReleaseBeforeOpening ||
            releaseGranted;

        public IReadOnlyList<GameObject> InstalledOnlyPresentationObjects =>
            installedOnlyPresentationObjects;

        public IReadOnlyList<GameObject> DetachedOnlyPresentationObjects =>
            detachedOnlyPresentationObjects;

        public void Configure(
            VehicleAssemblyController assemblyController,
            PartInstance authoredPart,
            Rigidbody authoredBody,
            float authoredAngularSpeedDegrees = 150f,
            string authoredOpenPrompt = "Открыть",
            string authoredClosePrompt = "Закрыть")
        {
            controller = assemblyController;
            part = authoredPart;
            targetBody = authoredBody;
            openPrompt = string.IsNullOrWhiteSpace(authoredOpenPrompt)
                ? "Открыть"
                : authoredOpenPrompt;
            closePrompt = string.IsNullOrWhiteSpace(authoredClosePrompt)
                ? "Закрыть"
                : authoredClosePrompt;
        }

        public void Configure(
            PartInstance authoredPart,
            Rigidbody authoredBody,
            float authoredAngularSpeedDegrees = 150f,
            string authoredOpenPrompt = "Открыть",
            string authoredClosePrompt = "Закрыть")
        {
            Configure(
                null,
                authoredPart,
                authoredBody,
                authoredAngularSpeedDegrees,
                authoredOpenPrompt,
                authoredClosePrompt);
        }

        public void BindAssemblyController(
            VehicleAssemblyController assemblyController)
        {
            controller = assemblyController;
        }

        /// <summary>
        /// Configures donor objects whose ownership swaps when the part is
        /// assembled. The bootlid uses this for its two hinge-arm copies: the
        /// body-side copy is visible while the lid is loose, then the donor
        /// Assembly FSM disables it and enables the copy parented to the lid.
        /// </summary>
        public void ConfigureInstalledPresentationSwap(
            GameObject[] authoredInstalledOnlyObjects,
            GameObject[] authoredDetachedOnlyObjects)
        {
            installedOnlyPresentationObjects =
                authoredInstalledOnlyObjects ?? System.Array.Empty<GameObject>();
            detachedOnlyPresentationObjects =
                authoredDetachedOnlyObjects ?? System.Array.Empty<GameObject>();
            ApplyInstalledPresentationState(part != null && part.IsInstalled);
        }

        public bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!CanOperate())
            {
                return false;
            }

            RefreshHingeMeasurement();
            if (direction == ContinuousContextInteractionDirection.Primary)
            {
                return IsReleasedForOpening && openNormalized < 0.995f;
            }

            return openNormalized > 0.005f;
        }

        public void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!CanBeginContinuousInteraction(context, direction))
            {
                held = false;
                return;
            }

            targetOpen = direction ==
                ContinuousContextInteractionDirection.Primary;
            if (targetOpen)
            {
                ReleaseClosedLatch(markReleased: true);
            }
            else
            {
                ReleaseOpenHold();
            }

            closeLatchStableFixedSteps = 0;
            held = true;
            targetBody?.WakeUp();
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!held || !CanOperate())
            {
                return false;
            }

            RefreshHingeMeasurement();
            return true;
        }

        public void EndContinuousInteraction()
        {
            held = false;
            closeLatchStableFixedSteps = 0;
        }

        /// <summary>
        /// Releases a donor-style separately locked panel, currently the hood.
        /// Ordinary doors and the bootlid release their latch on opening input.
        /// </summary>
        public bool ReleaseForOpening()
        {
            if (!CanOperate() || !RequiresReleaseBeforeOpening ||
                !IsClosedLatched)
            {
                return false;
            }

            releaseGranted = true;
            targetOpen = false;
            ReleaseClosedLatch(markReleased: true);
            targetBody?.WakeUp();
            return true;
        }

        internal void NotifyInstalled(
            MountPointAuthoring mountPoint,
            Quaternion? restoredWorldRotation = null)
        {
            activeMount = mountPoint != null
                ? mountPoint.GetComponent<AssemblyHingeMountAuthoring>()
                : null;
            installedPhysicsLink = GetComponent<AssemblyInstalledPhysicsLink>();
            activeHinge = installedPhysicsLink != null
                ? installedPhysicsLink.InstalledHinge
                : null;
            openNormalized = 0f;
            targetOpen = false;
            held = false;
            closedLatched = false;
            openHeld = false;
            closeLatchStableFixedSteps = 0;
            releaseGranted = activeMount == null ||
                !activeMount.RequiresReleaseBeforeOpening;
            ApplyInstalledPresentationState(installed: true);
            if (activeMount == null || activeHinge == null)
            {
                return;
            }

            activeMount.AttachFastenerTargets(transform);
            IgnoreConnectedBodyCollisions();
            ApplyFullTravelLimits();

            if (restoredWorldRotation.HasValue)
            {
                Quaternion mountRotation = mountPoint.Pose.rotation;
                Quaternion restoredLocalRotation =
                    Quaternion.Inverse(mountRotation) *
                    restoredWorldRotation.Value.normalized;
                openNormalized = CalculateOpenNormalized(
                    restoredLocalRotation,
                    activeMount.LocalAxis,
                    activeMount.OpenAngleDegrees);
            }

            ApplySavedPose(openNormalized);
            targetOpen = openNormalized >= 0.5f;
            if (openNormalized <= 0.001f)
            {
                LatchClosed();
            }
            else if (openNormalized >= 0.985f)
            {
                HoldOpen();
            }
        }

        internal void NotifyDetached()
        {
            ReleaseOpenHold();
            DestroyClosedLatch();
            RestoreConnectedBodyCollisions();
            activeMount?.ResetFastenerTargets();
            activeMount = null;
            installedPhysicsLink = null;
            activeHinge = null;
            openNormalized = 0f;
            targetOpen = false;
            held = false;
            closedLatched = false;
            openHeld = false;
            releaseGranted = false;
            closeLatchStableFixedSteps = 0;
            ApplyInstalledPresentationState(installed: false);
        }

        public void RestoreOpenState(float restoredOpenNormalized)
        {
            openNormalized = Mathf.Clamp01(restoredOpenNormalized);
            targetOpen = openNormalized >= 0.5f;
            held = false;
            closeLatchStableFixedSteps = 0;
            ReleaseOpenHold();
            DestroyClosedLatch();
            releaseGranted = activeMount == null ||
                !activeMount.RequiresReleaseBeforeOpening ||
                openNormalized > 0.001f;
            ApplySavedPose(openNormalized);
            if (activeMount != null && openNormalized <= 0.001f)
            {
                LatchClosed();
            }
            else if (activeMount != null && openNormalized >= 0.985f)
            {
                HoldOpen();
            }
        }

        private void FixedUpdate()
        {
            if (!CanOperate())
            {
                return;
            }

            RefreshHingeMeasurement();
            bool reachedOpenStop = targetOpen && openNormalized >= 0.985f;
            if (reachedOpenStop && !IsCompletelyUnfastened() &&
                activeMount.HasDonorOpenHold)
            {
                HoldOpen();
                held = false;
                return;
            }

            if (!held)
            {
                return;
            }

            Vector3 torque = targetOpen
                ? activeMount.DonorOpenTorqueLocal
                : activeMount.DonorCloseTorqueLocal;
            if (torque.sqrMagnitude > 0.0001f)
            {
                targetBody.AddRelativeTorque(torque, ForceMode.Force);
            }

            float physicalTravelFromClosed = openNormalized * Mathf.Abs(
                activeMount.OpenAngleDegrees);
            if (!targetOpen && physicalTravelFromClosed <=
                activeMount.CloseLatchThresholdDegrees)
            {
                closeLatchStableFixedSteps++;
                if (closeLatchStableFixedSteps >=
                    RequiredCloseLatchStableFixedSteps)
                {
                    LatchClosed();
                    held = false;
                    return;
                }
            }
            else
            {
                closeLatchStableFixedSteps = 0;
            }

            if (targetOpen && openNormalized >= 0.985f &&
                IsCompletelyUnfastened())
            {
                held = false;
                controller?.TryRemove(part);
            }
        }

        private void Update()
        {
            if (CanOperate())
            {
                RefreshHingeMeasurement();
            }
        }

        private void RefreshHingeMeasurement()
        {
            if (activeMount == null || activeHinge == null)
            {
                return;
            }

            float openAngle = activeMount.OpenAngleDegrees;
            if (Mathf.Abs(openAngle) < 0.001f)
            {
                openNormalized = 0f;
                return;
            }

            Transform mountPose = activeMount.MountPoint != null
                ? activeMount.MountPoint.Pose
                : null;
            if (mountPose == null || targetBody == null)
            {
                return;
            }

            // HingeJoint.angle is referenced to PhysX' internally generated
            // joint frame and can wrap or briefly report the opposite side on
            // mirrored hinges. That made one door falsely look closed during
            // broad travel and could leave its mirror outside the latch
            // threshold. The donor FSM measures actual panel rotation, so use
            // the project mount frame instead of the joint diagnostic scalar.
            Quaternion localRotation = Quaternion.Inverse(
                mountPose.rotation) * targetBody.rotation;
            float measured = CalculateOpenNormalized(
                localRotation,
                activeMount.LocalAxis,
                openAngle);
            if (float.IsFinite(measured))
            {
                openNormalized = measured;
            }
        }

        private void ApplySavedPose(float normalized)
        {
            if (activeMount == null || activeMount.MountPoint == null)
            {
                return;
            }

            Transform mountPose = activeMount.MountPoint.Pose;
            Quaternion rotation = mountPose.rotation * Quaternion.AngleAxis(
                activeMount.OpenAngleDegrees * Mathf.Clamp01(normalized),
                activeMount.LocalAxis);
            transform.SetPositionAndRotation(mountPose.position, rotation);
            if (targetBody != null)
            {
                targetBody.position = mountPose.position;
                targetBody.rotation = rotation;
                Rigidbody connectedBody = installedPhysicsLink != null
                    ? installedPhysicsLink.ConnectedBody
                    : null;
                if (connectedBody != null)
                {
                    targetBody.linearVelocity = connectedBody.GetPointVelocity(
                        targetBody.worldCenterOfMass);
                    targetBody.angularVelocity = connectedBody.angularVelocity;
                }
            }

            Physics.SyncTransforms();
        }

        private void LatchClosed()
        {
            if (activeMount == null || activeHinge == null ||
                targetBody == null)
            {
                return;
            }

            ReleaseOpenHold();
            DestroyClosedLatch();
            ApplySavedPose(0f);
            openNormalized = 0f;
            targetOpen = false;
            closedLatched = true;
            closeLatchStableFixedSteps = 0;
            releaseGranted = !activeMount.RequiresReleaseBeforeOpening;

            Rigidbody connectedBody = installedPhysicsLink != null
                ? installedPhysicsLink.ConnectedBody
                : null;
            if (connectedBody == null)
            {
                return;
            }

            closedLatch = targetBody.gameObject.AddComponent<FixedJoint>();
            closedLatch.connectedBody = connectedBody;
            closedLatch.enableCollision = false;
            closedLatch.enablePreprocessing = true;
            closedLatch.breakForce = activeMount.DonorLatchBreakForce > 0f
                ? activeMount.DonorLatchBreakForce
                : float.PositiveInfinity;
            closedLatch.breakTorque = activeMount.DonorLatchBreakTorque > 0f
                ? activeMount.DonorLatchBreakTorque
                : float.PositiveInfinity;
        }

        private void ReleaseClosedLatch(bool markReleased)
        {
            DestroyClosedLatch();
            closedLatched = false;
            closeLatchStableFixedSteps = 0;
            if (markReleased)
            {
                releaseGranted = true;
            }
        }

        private void HoldOpen()
        {
            if (activeMount == null || activeHinge == null ||
                !activeMount.HasDonorOpenHold)
            {
                return;
            }

            JointLimits limits = activeHinge.limits;
            limits.min = activeMount.OpenHoldMinimumAngleDegrees;
            limits.max = activeMount.OpenHoldMaximumAngleDegrees;
            limits.bounciness = 0f;
            limits.contactDistance = Mathf.Min(
                0.25f,
                activeMount.DonorOpenHoldWindowDegrees * 0.25f);
            activeHinge.limits = limits;
            activeHinge.useLimits = true;
            openHeld = true;
        }

        private void ReleaseOpenHold()
        {
            if (!openHeld)
            {
                return;
            }

            ApplyFullTravelLimits();
            openHeld = false;
        }

        private void ApplyFullTravelLimits()
        {
            if (activeMount == null || activeHinge == null)
            {
                return;
            }

            JointLimits limits = activeHinge.limits;
            limits.min = activeMount.MinimumAngleDegrees;
            limits.max = activeMount.MaximumAngleDegrees;
            limits.bounciness = 0f;
            limits.contactDistance = 0.25f;
            activeHinge.limits = limits;
            activeHinge.useLimits = true;
        }

        private void DestroyClosedLatch()
        {
            if (closedLatch == null)
            {
                return;
            }

            FixedJoint latch = closedLatch;
            closedLatch = null;
            // Joint is not a Behaviour and cannot be disabled. Keep its
            // connected body intact until Unity destroys it at frame end;
            // assigning null here would briefly weld the panel to the world.
            if (Application.isPlaying)
            {
                Destroy(latch);
            }
            else
            {
                DestroyImmediate(latch);
            }
        }

        private bool IsCompletelyUnfastened()
        {
            if (controller == null || activeMount?.MountPoint == null)
            {
                return false;
            }

            MountPointRuntime mount = controller.ResolveMount(
                activeMount.MountPoint);
            if (mount == null || mount.Fasteners.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < mount.Fasteners.Length; index++)
            {
                if (mount.Fasteners[index].Stage > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanOperate() => enabled && gameObject.activeInHierarchy &&
            part != null && part.IsInstalled && activeMount != null &&
            activeHinge != null && targetBody != null;

        internal void SynchronizeInstalledPose(
            Vector3 mountWorldPosition,
            Quaternion mountWorldRotation)
        {
            // Dynamic panel pose is owned by PhysX. This fallback only exists
            // for stale content that has hinge metadata but no installed link.
            if (IsAttachedToHinge || activeMount == null)
            {
                return;
            }

            Quaternion rotation = mountWorldRotation * Quaternion.AngleAxis(
                activeMount.OpenAngleDegrees * openNormalized,
                activeMount.LocalAxis);
            transform.SetPositionAndRotation(mountWorldPosition, rotation);
            if (targetBody != null)
            {
                targetBody.position = mountWorldPosition;
                targetBody.rotation = rotation;
            }
        }

        private void IgnoreConnectedBodyCollisions()
        {
            RestoreConnectedBodyCollisions();
            Rigidbody connectedBody = installedPhysicsLink != null
                ? installedPhysicsLink.ConnectedBody
                : null;
            if (connectedBody == null || targetBody == null ||
                connectedBody == targetBody)
            {
                return;
            }

            Collider[] ownColliders = targetBody
                .GetComponentsInChildren<Collider>(true);
            Collider[] connectedColliders = connectedBody
                .GetComponentsInChildren<Collider>(true);
            for (int ownIndex = 0; ownIndex < ownColliders.Length; ownIndex++)
            {
                Collider ownCollider = ownColliders[ownIndex];
                if (ownCollider == null || ownCollider.isTrigger ||
                    ownCollider.attachedRigidbody != targetBody)
                {
                    continue;
                }

                for (int connectedIndex = 0;
                     connectedIndex < connectedColliders.Length;
                     connectedIndex++)
                {
                    Collider connectedCollider =
                        connectedColliders[connectedIndex];
                    if (connectedCollider == null ||
                        connectedCollider.isTrigger ||
                        connectedCollider.attachedRigidbody != connectedBody ||
                        Physics.GetIgnoreCollision(
                            ownCollider,
                            connectedCollider))
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(
                        ownCollider,
                        connectedCollider,
                        true);
                    ignoredConnectedBodyCollisions.Add(new ColliderPair(
                        ownCollider,
                        connectedCollider));
                }
            }
        }

        private void RestoreConnectedBodyCollisions()
        {
            for (int index = 0;
                 index < ignoredConnectedBodyCollisions.Count;
                 index++)
            {
                ColliderPair pair = ignoredConnectedBodyCollisions[index];
                if (pair.OwnCollider != null &&
                    pair.ConnectedCollider != null)
                {
                    Physics.IgnoreCollision(
                        pair.OwnCollider,
                        pair.ConnectedCollider,
                        false);
                }
            }

            ignoredConnectedBodyCollisions.Clear();
        }

        private void OnDisable()
        {
            held = false;
            closeLatchStableFixedSteps = 0;
        }

        private void OnDestroy()
        {
            DestroyClosedLatch();
            RestoreConnectedBodyCollisions();
        }

        private static float CalculateOpenNormalized(
            Quaternion localRotation,
            Vector3 axis,
            float openAngleDegrees)
        {
            if (Mathf.Abs(openAngleDegrees) < 0.001f)
            {
                return 0f;
            }

            Vector3 normalizedAxis = axis.sqrMagnitude > 0.0001f
                ? axis.normalized
                : Vector3.forward;
            Vector3 reference = Mathf.Abs(Vector3.Dot(
                    normalizedAxis,
                    Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;
            reference = Vector3.ProjectOnPlane(reference, normalizedAxis)
                .normalized;
            float signedAngle = Vector3.SignedAngle(
                reference,
                localRotation * reference,
                normalizedAxis);
            return Mathf.Clamp01(signedAngle / openAngleDegrees);
        }

        private void ApplyInstalledPresentationState(bool installed)
        {
            if (installed)
            {
                SetPresentationObjectsActive(
                    detachedOnlyPresentationObjects,
                    active: false);
                SetPresentationObjectsActive(
                    installedOnlyPresentationObjects,
                    active: true);
                return;
            }

            SetPresentationObjectsActive(
                installedOnlyPresentationObjects,
                active: false);
            SetPresentationObjectsActive(
                detachedOnlyPresentationObjects,
                active: true);
        }

        private static void SetPresentationObjectsActive(
            IReadOnlyList<GameObject> objects,
            bool active)
        {
            if (objects == null)
            {
                return;
            }

            for (int index = 0; index < objects.Count; index++)
            {
                GameObject candidate = objects[index];
                if (candidate != null && candidate.activeSelf != active)
                {
                    candidate.SetActive(active);
                }
            }
        }

        private readonly struct ColliderPair
        {
            public ColliderPair(
                Collider ownCollider,
                Collider connectedCollider)
            {
                OwnCollider = ownCollider;
                ConnectedCollider = connectedCollider;
            }

            public Collider OwnCollider { get; }

            public Collider ConnectedCollider { get; }
        }
    }
}
