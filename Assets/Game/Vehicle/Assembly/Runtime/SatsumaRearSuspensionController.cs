using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class SatsumaRearSuspensionCornerBinding
    {
        [SerializeField] private string cornerId = string.Empty;
        [SerializeField] private string trailingArmMountId = string.Empty;
        [SerializeField] private string drumMountId = string.Empty;
        [SerializeField] private string roadWheelMountId = string.Empty;
        [SerializeField] private string stockSpringMountId = string.Empty;
        [SerializeField] private string longSpringMountId = string.Empty;
        [SerializeField] private string shockMountId = string.Empty;
        [SerializeField] private Transform springTopBone;
        [SerializeField] private Transform springBottomAnchor;
        [SerializeField] private Transform springBottomRenderBone;
        [SerializeField] private Transform shockTopTarget;
        [SerializeField] private Transform shockBottomTarget;

        [NonSerialized] private PartInstance presentedSpring;
        [NonSerialized] private PartInstance presentedShock;
        [NonSerialized] private bool springStateInitialized;
        [NonSerialized] private bool springInstalled;
        [NonSerialized] private bool animateSpringExpansion;
        [NonSerialized] private float springExpansion01 = 1f;

        public string CornerId => cornerId;
        public string TrailingArmMountId => trailingArmMountId;
        public string DrumMountId => drumMountId;
        public string RoadWheelMountId => roadWheelMountId;
        public string StockSpringMountId => stockSpringMountId;
        public string LongSpringMountId => longSpringMountId;
        public string ShockMountId => shockMountId;
        public Transform SpringTopBone => springTopBone;
        public Transform SpringBottomAnchor => springBottomAnchor;
        public Transform SpringBottomRenderBone => springBottomRenderBone;
        public Transform ShockTopTarget => shockTopTarget;
        public Transform ShockBottomTarget => shockBottomTarget;
        public float SpringExpansion01 => springExpansion01;

        public static SatsumaRearSuspensionCornerBinding Create(
            string id,
            Transform topSpringBone,
            Transform bottomSpringAnchor,
            Transform bottomSpringRenderBone,
            Transform topShockTarget,
            Transform bottomShockTarget)
        {
            return new SatsumaRearSuspensionCornerBinding
            {
                cornerId = id ?? string.Empty,
                trailingArmMountId = "mount.satsuma.trail-arm-" + id,
                drumMountId = "mount.satsuma.drum-brake-" + id,
                roadWheelMountId = "mount.satsuma.wheel" + id + "-new",
                stockSpringMountId = "mount.satsuma.coilspring-" + id,
                longSpringMountId = "mount.satsuma.long-coilspring-" + id,
                shockMountId = "mount.satsuma.shock-" + id,
                springTopBone = topSpringBone,
                springBottomAnchor = bottomSpringAnchor,
                springBottomRenderBone = bottomSpringRenderBone,
                shockTopTarget = topShockTarget,
                shockBottomTarget = bottomShockTarget,
            };
        }

        internal void UpdatePresentation(
            VehicleAssemblyController assembly,
            Transform chassisReference)
        {
            PartInstance spring = ResolveInstalledPart(
                assembly,
                stockSpringMountId) ?? ResolveInstalledPart(
                assembly,
                longSpringMountId);
            if (presentedSpring != spring)
            {
                RestorePresentation(presentedSpring);
                presentedSpring = spring;
            }

            UpdateSpringRenderBone();
            presentedSpring?.GetComponent<
                    SatsumaRearSuspensionPartPresentation>()
                ?.PresentInstalledSpring(
                    springTopBone,
                    springBottomRenderBone);

            PartInstance shock = ResolveInstalledPart(assembly, shockMountId);
            if (presentedShock != shock)
            {
                RestorePresentation(presentedShock);
                presentedShock = shock;
            }

            UpdateShockTargetRotations(chassisReference);

            presentedShock?.GetComponent<
                    SatsumaRearSuspensionPartPresentation>()
                ?.PresentInstalledShock(shockTopTarget, shockBottomTarget);
        }

        internal void BeginSpringInstallationAnimation()
        {
            springStateInitialized = true;
            springInstalled = true;
            animateSpringExpansion = true;
            springExpansion01 = 0f;
            UpdateSpringRenderBone();
        }

        internal float AdvanceSpringExpansion(
            bool hasSpring,
            float deltaTime,
            float durationSeconds)
        {
            if (!springStateInitialized)
            {
                springStateInitialized = true;
                springInstalled = hasSpring;
                springExpansion01 = hasSpring ? 1f : 0f;
                animateSpringExpansion = false;
                return springExpansion01;
            }

            if (!hasSpring)
            {
                springInstalled = false;
                animateSpringExpansion = false;
                springExpansion01 = 0f;
                return springExpansion01;
            }

            if (!springInstalled)
            {
                // No assembly event means this came from initial/save state;
                // it must already be at its settled extension on load.
                springInstalled = true;
                springExpansion01 = 1f;
                animateSpringExpansion = false;
                return springExpansion01;
            }

            if (animateSpringExpansion)
            {
                springExpansion01 = Mathf.MoveTowards(
                    springExpansion01,
                    1f,
                    Mathf.Max(0f, deltaTime) /
                    Mathf.Max(0.05f, durationSeconds));
                if (springExpansion01 >= 0.9999f)
                {
                    springExpansion01 = 1f;
                    animateSpringExpansion = false;
                }
            }

            return springExpansion01;
        }

        internal bool MatchesSpringMount(string mountId) =>
            string.Equals(
                mountId,
                stockSpringMountId,
                StringComparison.Ordinal) ||
            string.Equals(
                mountId,
                longSpringMountId,
                StringComparison.Ordinal);

        private void UpdateSpringRenderBone()
        {
            if (springTopBone == null || springBottomAnchor == null ||
                springBottomRenderBone == null)
            {
                return;
            }

            Vector3 top = springTopBone.position;
            Vector3 bottom = springBottomAnchor.position;
            Vector3 topToBottom = bottom - top;
            float distance = topToBottom.magnitude;
            Vector3 direction = distance > 0.0001f
                ? topToBottom / distance
                : -springTopBone.up;
            float compressedLength = Mathf.Min(distance, 0.062f);
            float eased = springExpansion01 * springExpansion01 *
                (3f - 2f * springExpansion01);
            springBottomRenderBone.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    top + direction * compressedLength,
                    bottom,
                    eased),
                springBottomAnchor.rotation);
        }

        private void UpdateShockTargetRotations(Transform chassisReference)
        {
            if (shockTopTarget == null || shockBottomTarget == null ||
                chassisReference == null)
            {
                return;
            }

            // In the donor the upper and lower meshes are driven by separate
            // IK chains. Both local +Z axes point from the moving arm anchor
            // towards the fixed chassis anchor, but their roll differs by
            // 180 degrees. Rebuilding that tiny relation here keeps the two
            // halves coaxial throughout suspension travel instead of merely
            // matching one captured rest frame.
            Vector3 bottomToTop =
                shockTopTarget.position - shockBottomTarget.position;
            if (bottomToTop.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 axis = bottomToTop.normalized;
            shockTopTarget.rotation = Quaternion.LookRotation(
                axis,
                -chassisReference.forward);
            shockBottomTarget.rotation = Quaternion.LookRotation(
                axis,
                chassisReference.forward);
        }

        internal HingeJoint ResolveTrailingArmHinge(
            VehicleAssemblyController assembly)
        {
            PartInstance arm = ResolveInstalledPart(
                assembly,
                trailingArmMountId);
            return arm != null
                ? arm.GetComponent<AssemblyInstalledPhysicsLink>()
                    ?.InstalledHinge
                : null;
        }

        internal bool TryGetTrailingArmBodies(
            VehicleAssemblyController assembly,
            out HingeJoint hinge,
            out Rigidbody armBody,
            out Rigidbody connectedBody)
        {
            hinge = ResolveTrailingArmHinge(assembly);
            armBody = hinge != null ? hinge.GetComponent<Rigidbody>() : null;
            connectedBody = hinge != null ? hinge.connectedBody : null;
            return hinge != null && armBody != null && connectedBody != null;
        }

        internal bool HasStockSpring(VehicleAssemblyController assembly) =>
            ResolveInstalledPart(assembly, stockSpringMountId) != null;

        internal bool HasLongSpring(VehicleAssemblyController assembly) =>
            ResolveInstalledPart(assembly, longSpringMountId) != null;

        internal bool HasShock(VehicleAssemblyController assembly) =>
            ResolveInstalledPart(assembly, shockMountId) != null;

        internal bool TryGetInstalledDrumAndRoadWheel(
            VehicleAssemblyController assembly,
            out PartInstance drum,
            out PartInstance roadWheel)
        {
            drum = ResolveInstalledPart(assembly, drumMountId);
            roadWheel = ResolveInstalledPart(assembly, roadWheelMountId);
            return drum != null && roadWheel != null;
        }

        internal void RestorePresentation()
        {
            RestorePresentation(presentedSpring);
            RestorePresentation(presentedShock);
            presentedSpring = null;
            presentedShock = null;
        }

        private static PartInstance ResolveInstalledPart(
            VehicleAssemblyController assembly,
            string mountId)
        {
            if (assembly == null || string.IsNullOrEmpty(mountId) ||
                !assembly.Graph.TryGetMount(
                    mountId,
                    out MountPointRuntime mount) ||
                mount == null || !mount.IsOccupied)
            {
                return null;
            }

            return mount.InstalledPart;
        }

        private static void RestorePresentation(PartInstance part)
        {
            part?.GetComponent<SatsumaRearSuspensionPartPresentation>()
                ?.RestoreLoosePresentation();
        }
    }

    /// <summary>
    /// Project-owned rear suspension presentation and assembly lifecycle for
    /// the Phase 1 Satsuma. The donor Wheel/NWH adapter owns complete-corner
    /// ground support; an incomplete arm remains an ordinary physical hinge.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class SatsumaRearSuspensionController : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private Rigidbody chassisBody;
        [SerializeField] private Collider[] chassisColliders =
            Array.Empty<Collider>();
        [SerializeField] private SatsumaRearSuspensionCornerBinding[] corners =
            Array.Empty<SatsumaRearSuspensionCornerBinding>();
        // V38 coil-space settings are retained for serialized/public diagnostic
        // compatibility only. They are NOT the active V39 wheel force model.
        // Keeping the data avoids silently reinterpreting old prefab units.
        [SerializeField, HideInInspector] private float stockSpringRate = 32000f;
        [SerializeField, HideInInspector] private float longSpringRate = 36000f;
        [SerializeField, HideInInspector] private float stockSpringFreeLength = 0.24f;
        [SerializeField, HideInInspector] private float longSpringFreeLength = 0.27f;
        [SerializeField, HideInInspector] private float springOnlyDamper = 650f;
        [SerializeField, HideInInspector] private float shockDamper = 2600f;
        [SerializeField] private float installationExpansionSeconds = 0.55f;
        [SerializeField, HideInInspector] private float maximumSpringForce = 6000f;
        [SerializeField] private bool externalWheelAuthority;

        private readonly Dictionary<PartInstance, bool> ignoredCollisionState =
            new Dictionary<PartInstance, bool>();
        private readonly Dictionary<string, RearCornerCollisionPairState>
            ignoredCornerCollisionState =
                new Dictionary<string, RearCornerCollisionPairState>(
                    StringComparer.Ordinal);
        private VehicleAssemblyController subscribedAssembly;
        private sealed class RearCornerCollisionPairState
        {
            public PartInstance Drum;
            public PartInstance RoadWheel;
            public bool Ignored;
        }

        public IReadOnlyList<SatsumaRearSuspensionCornerBinding> Corners =>
            corners;

        // Legacy coil-space diagnostics retained for API compatibility. Active
        // V39 coefficients are wheel-referenced and exposed separately below.
        public float StockSpringRate => stockSpringRate;

        public float StockSpringAngularStiffness => stockSpringRate;

        public float ShockDamper => shockDamper;

        public float StockWheelSpringRate => SatsumaRearSuspensionForce.StockWheelRate;
        public float LongWheelSpringRate => SatsumaRearSuspensionForce.LongWheelRate;
        public float WheelShockDamper => SatsumaRearSuspensionForce.StockShockDamper;
        public bool ExternalWheelAuthority => externalWheelAuthority;

        public void SetExternalWheelAuthority(bool active)
        {
            externalWheelAuthority = active;
        }

        internal bool TryResolveSpringTransitionTargets(
            string mountId,
            out Transform topTarget,
            out Transform bottomTarget)
        {
            for (int index = 0; index < corners.Length; index++)
            {
                SatsumaRearSuspensionCornerBinding corner = corners[index];
                if (corner == null || !corner.MatchesSpringMount(mountId))
                {
                    continue;
                }

                topTarget = corner.SpringTopBone;
                bottomTarget = corner.SpringBottomAnchor;
                return topTarget != null && bottomTarget != null;
            }

            topTarget = null;
            bottomTarget = null;
            return false;
        }

        public void Configure(
            VehicleAssemblyController assemblyController,
            Rigidbody vehicleBody,
            Collider[] vehicleColliders,
            params SatsumaRearSuspensionCornerBinding[] cornerBindings)
        {
            UnsubscribeFromAssembly();
            assembly = assemblyController;
            chassisBody = vehicleBody;
            chassisColliders = vehicleColliders ?? Array.Empty<Collider>();
            corners = cornerBindings ??
                Array.Empty<SatsumaRearSuspensionCornerBinding>();
            if (isActiveAndEnabled)
            {
                SubscribeToAssembly();
            }
        }

        private void OnEnable()
        {
            SubscribeToAssembly();
        }

        private void FixedUpdate()
        {
            if (assembly == null || chassisBody == null)
            {
                return;
            }

            for (int index = 0; index < corners.Length; index++)
            {
                SatsumaRearSuspensionCornerBinding corner = corners[index];
                if (corner == null)
                {
                    continue;
                }

                bool hasSpring = corner.HasStockSpring(assembly) ||
                    corner.HasLongSpring(assembly);
                corner.AdvanceSpringExpansion(
                    hasSpring,
                    Time.fixedDeltaTime,
                    installationExpansionSeconds);
            }

            RefreshInstalledCollisionPairs();
        }

        private void LateUpdate()
        {
            RefreshPresentationNow();
        }

        public void RefreshPresentationNow()
        {
            if (assembly == null)
            {
                return;
            }

            for (int index = 0; index < corners.Length; index++)
            {
                corners[index]?.UpdatePresentation(assembly, transform);
            }
        }

        private void UpdateTravelLimits(HingeJoint hinge, Rigidbody armBody,
            Rigidbody connectedBody, bool hasStockSpring, bool hasLongSpring)
        {
            // The donor's Wheel travel drives an IK target, not a free arm
            // with arbitrary -32/+8 stops. Transfer that angular envelope to
            // the existing physical joint, including when no spring exists.
            Vector3 pivotCarLocal = connectedBody == chassisBody
                ? hinge.connectedAnchor
                : chassisBody.transform.InverseTransformPoint(
                    connectedBody.transform.TransformPoint(hinge.connectedAnchor));
            Vector2 range = SatsumaRearSuspensionTravel.ResolveArmLimits(
                pivotCarLocal, hasStockSpring, hasLongSpring);
            JointLimits limits = hinge.limits;
            if (hinge.useLimits && Mathf.Approximately(limits.min, range.x) &&
                Mathf.Approximately(limits.max, range.y))
            {
                return;
            }

            limits.min = range.x;
            limits.max = range.y;
            hinge.limits = limits;
            hinge.useLimits = true;
            armBody.WakeUp();
            if (!connectedBody.isKinematic)
            {
                connectedBody.WakeUp();
            }
        }

        private void SubscribeToAssembly()
        {
            if (assembly == null || subscribedAssembly == assembly)
            {
                return;
            }

            UnsubscribeFromAssembly();
            assembly.ActionCompleted += OnAssemblyActionCompleted;
            subscribedAssembly = assembly;
        }

        private void UnsubscribeFromAssembly()
        {
            if (subscribedAssembly == null)
            {
                return;
            }

            subscribedAssembly.ActionCompleted -= OnAssemblyActionCompleted;
            subscribedAssembly = null;
        }

        private void OnAssemblyActionCompleted(AssemblyActionCompleted action)
        {
            bool installed = action.Action == AssemblyActionKind.PartInstalled;
            if ((!installed && action.Action != AssemblyActionKind.PartRemoved) ||
                chassisBody == null)
            {
                return;
            }

            for (int index = 0; index < corners.Length; index++)
            {
                SatsumaRearSuspensionCornerBinding corner = corners[index];
                if (corner == null)
                {
                    continue;
                }

                // Update before the next physics step on arm installation or
                // spring replacement/removal; never reset the arm transform.
                if (corner.TryGetTrailingArmBodies(assembly, out HingeJoint hinge,
                        out Rigidbody armBody, out Rigidbody connectedBody))
                {
                    UpdateTravelLimits(hinge, armBody, connectedBody,
                        corner.HasStockSpring(assembly), corner.HasLongSpring(assembly));
                }
                if (installed && corner.MatchesSpringMount(action.MountId))
                {
                    corner.BeginSpringInstallationAnimation();
                    chassisBody?.WakeUp();
                }
            }
        }

        private void RefreshInstalledCollisionPairs()
        {
            PartInstance[] parts = assembly.Parts;
            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance part = parts[index];
                if (!IsPhysicalRearPart(part))
                {
                    continue;
                }

                bool shouldIgnore = part.IsInstalled &&
                    part.UsesDynamicInstalledPhysics;
                if (ignoredCollisionState.TryGetValue(
                        part,
                        out bool current) && current == shouldIgnore)
                {
                    continue;
                }

                SetChassisCollisionIgnored(part, shouldIgnore);
                ignoredCollisionState[part] = shouldIgnore;
            }

            RefreshRearCornerCollisionPairs();
        }

        private void RefreshRearCornerCollisionPairs()
        {
            for (int index = 0; index < corners.Length; index++)
            {
                SatsumaRearSuspensionCornerBinding corner = corners[index];
                if (corner == null || string.IsNullOrEmpty(corner.CornerId))
                {
                    continue;
                }

                corner.TryGetInstalledDrumAndRoadWheel(
                    assembly,
                    out PartInstance drum,
                    out PartInstance roadWheel);
                bool shouldIgnore = drum != null && roadWheel != null &&
                    drum.UsesDynamicInstalledPhysics &&
                    roadWheel.UsesDynamicInstalledPhysics;

                ignoredCornerCollisionState.TryGetValue(
                    corner.CornerId,
                    out RearCornerCollisionPairState current);
                bool sameParts = current != null &&
                    current.Drum == drum &&
                    current.RoadWheel == roadWheel;
                if (sameParts && current.Ignored == shouldIgnore)
                {
                    continue;
                }

                if (current != null && current.Ignored)
                {
                    SetPartCollisionIgnored(
                        current.Drum,
                        current.RoadWheel,
                        false);
                }

                if (shouldIgnore)
                {
                    SetPartCollisionIgnored(drum, roadWheel, true);
                }

                ignoredCornerCollisionState[corner.CornerId] =
                    new RearCornerCollisionPairState
                    {
                        Drum = drum,
                        RoadWheel = roadWheel,
                        Ignored = shouldIgnore,
                    };
            }
        }

        private void SetChassisCollisionIgnored(
            PartInstance part,
            bool ignored)
        {
            if (part == null)
            {
                return;
            }

            Collider[] partColliders = part.GetComponentsInChildren<Collider>(
                true);
            for (int partIndex = 0;
                 partIndex < partColliders.Length;
                 partIndex++)
            {
                Collider partCollider = partColliders[partIndex];
                if (partCollider == null || partCollider.isTrigger)
                {
                    continue;
                }

                for (int chassisIndex = 0;
                     chassisIndex < chassisColliders.Length;
                     chassisIndex++)
                {
                    Collider chassisCollider =
                        chassisColliders[chassisIndex];
                    if (chassisCollider != null && !chassisCollider.isTrigger)
                    {
                        Physics.IgnoreCollision(
                            partCollider,
                            chassisCollider,
                            ignored);
                    }
                }
            }
        }

        private static void SetPartCollisionIgnored(
            PartInstance first,
            PartInstance second,
            bool ignored)
        {
            if (first == null || second == null)
            {
                return;
            }

            Collider[] firstColliders = first.GetComponentsInChildren<Collider>(
                true);
            Collider[] secondColliders = second.GetComponentsInChildren<Collider>(
                true);
            for (int firstIndex = 0;
                 firstIndex < firstColliders.Length;
                 firstIndex++)
            {
                Collider firstCollider = firstColliders[firstIndex];
                if (firstCollider == null || firstCollider.isTrigger)
                {
                    continue;
                }

                for (int secondIndex = 0;
                     secondIndex < secondColliders.Length;
                     secondIndex++)
                {
                    Collider secondCollider = secondColliders[secondIndex];
                    if (secondCollider != null && !secondCollider.isTrigger)
                    {
                        Physics.IgnoreCollision(
                            firstCollider,
                            secondCollider,
                            ignored);
                    }
                }
            }
        }

        private static bool IsPhysicalRearPart(PartInstance part)
        {
            string id = part?.Definition?.DefinitionId;
            return !string.IsNullOrEmpty(id) &&
                (id.IndexOf("trail-arm-", StringComparison.Ordinal) >= 0 ||
                 id.IndexOf("drum-brake-", StringComparison.Ordinal) >= 0 ||
                 id.IndexOf("wheel-stock-r", StringComparison.Ordinal) >= 0 ||
                 id.IndexOf("wheel-gt-r", StringComparison.Ordinal) >= 0);
        }

        private void OnDisable()
        {
            UnsubscribeFromAssembly();

            for (int index = 0; index < corners.Length; index++)
            {
                corners[index]?.RestorePresentation();
            }

            foreach (KeyValuePair<PartInstance, bool> pair in
                     ignoredCollisionState)
            {
                if (pair.Key != null && pair.Value)
                {
                    SetChassisCollisionIgnored(pair.Key, false);
                }
            }

            ignoredCollisionState.Clear();

            foreach (RearCornerCollisionPairState pair in
                     ignoredCornerCollisionState.Values)
            {
                if (pair != null && pair.Ignored)
                {
                    SetPartCollisionIgnored(
                        pair.Drum,
                        pair.RoadWheel,
                        false);
                }
            }

            ignoredCornerCollisionState.Clear();
        }
    }
}
