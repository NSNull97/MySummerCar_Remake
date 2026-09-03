using System;
using System.Collections.Generic;
using MSC.Vehicle.Assembly;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    [Serializable]
    public struct SatsumaFrontSuspensionCornerBinding
    {
        [SerializeField] private string cornerId;
        [SerializeField] private WheelController wheel;
        [SerializeField] private MountPointAuthoring wishboneMount;
        [SerializeField] private MountPointAuthoring spindleMount;
        [SerializeField] private MountPointAuthoring strutMount;
        [SerializeField] private Transform shockBottomTarget;
        [SerializeField]
        private SatsumaFrontStrutPresentation strutPresentation;
        [SerializeField] private MountPointAuthoring steeringRodMount;
        [SerializeField] private MountPointAuthoring discBrakeMount;
        [SerializeField] private MountPointAuthoring roadWheelMount;
        [SerializeField] private Transform steeringOuterTarget;
        [SerializeField]
        private SatsumaFrontSteeringRodPresentation steeringRodPresentation;
        [SerializeField] private Vector3 fullDroopHubLocalPosition;
        [SerializeField] private Quaternion fullDroopHubLocalRotation;
        [SerializeField] private Vector3 canonicalNoStrutHubLocalPosition;
        [SerializeField] private Quaternion canonicalNoStrutHubLocalRotation;
        [SerializeField] private Vector3 wishboneBodyPivotLocalPosition;
        [SerializeField] private Quaternion wishboneMeshZeroLocalRotation;
        [SerializeField] private Vector3 hubToWishboneTargetLocalOffset;
        [SerializeField] private Vector3 hubToSpindleMeshLocalOffset;
        [SerializeField] private Quaternion spindleMeshLocalRotation;
        [SerializeField] private Vector3 hubToShockBottomLocalOffset;
        [SerializeField] private Quaternion shockBottomLocalRotation;
        [SerializeField] private Vector3 hubToSteeringOuterLocalOffset;
        [SerializeField] private Quaternion steeringOuterLocalRotation;
        [SerializeField] private Vector3 hubToDiscBrakeLocalOffset;
        [SerializeField] private Quaternion discBrakeLocalRotation;
        [SerializeField] private Vector3 hubToRoadWheelLocalOffset;
        [SerializeField] private Quaternion roadWheelLocalRotation;
        [SerializeField] private bool leftSide;

        public static SatsumaFrontSuspensionCornerBinding Create(
            string id,
            WheelController configuredWheel,
            MountPointAuthoring configuredWishboneMount,
            MountPointAuthoring configuredSpindleMount,
            MountPointAuthoring configuredStrutMount,
            Transform configuredShockBottomTarget,
            SatsumaFrontStrutPresentation configuredStrutPresentation,
            MountPointAuthoring configuredSteeringRodMount,
            MountPointAuthoring configuredDiscBrakeMount,
            MountPointAuthoring configuredRoadWheelMount,
            Transform configuredSteeringOuterTarget,
            SatsumaFrontSteeringRodPresentation
                configuredSteeringRodPresentation,
            Vector3 configuredFullDroopHubLocalPosition,
            Quaternion configuredFullDroopHubLocalRotation,
            Vector3 configuredCanonicalNoStrutHubLocalPosition,
            Quaternion configuredCanonicalNoStrutHubLocalRotation,
            Vector3 configuredWishboneBodyPivotLocalPosition,
            Quaternion configuredWishboneMeshZeroLocalRotation,
            Vector3 configuredHubToWishboneTargetLocalOffset,
            Vector3 configuredHubToSpindleMeshLocalOffset,
            Quaternion configuredSpindleMeshLocalRotation,
            Vector3 configuredHubToShockBottomLocalOffset,
            Quaternion configuredShockBottomLocalRotation,
            Vector3 configuredHubToSteeringOuterLocalOffset,
            Quaternion configuredSteeringOuterLocalRotation,
            Vector3 configuredHubToDiscBrakeLocalOffset,
            Quaternion configuredDiscBrakeLocalRotation,
            Vector3 configuredHubToRoadWheelLocalOffset,
            Quaternion configuredRoadWheelLocalRotation,
            bool configuredLeftSide)
        {
            return new SatsumaFrontSuspensionCornerBinding
            {
                cornerId = id ?? string.Empty,
                wheel = configuredWheel,
                wishboneMount = configuredWishboneMount,
                spindleMount = configuredSpindleMount,
                strutMount = configuredStrutMount,
                shockBottomTarget = configuredShockBottomTarget,
                strutPresentation = configuredStrutPresentation,
                steeringRodMount = configuredSteeringRodMount,
                discBrakeMount = configuredDiscBrakeMount,
                roadWheelMount = configuredRoadWheelMount,
                steeringOuterTarget = configuredSteeringOuterTarget,
                steeringRodPresentation =
                    configuredSteeringRodPresentation,
                fullDroopHubLocalPosition = configuredFullDroopHubLocalPosition,
                fullDroopHubLocalRotation =
                    configuredFullDroopHubLocalRotation,
                canonicalNoStrutHubLocalPosition =
                    configuredCanonicalNoStrutHubLocalPosition,
                canonicalNoStrutHubLocalRotation =
                    configuredCanonicalNoStrutHubLocalRotation,
                wishboneBodyPivotLocalPosition =
                    configuredWishboneBodyPivotLocalPosition,
                wishboneMeshZeroLocalRotation =
                    configuredWishboneMeshZeroLocalRotation,
                hubToWishboneTargetLocalOffset =
                    configuredHubToWishboneTargetLocalOffset,
                hubToSpindleMeshLocalOffset =
                    configuredHubToSpindleMeshLocalOffset,
                spindleMeshLocalRotation = configuredSpindleMeshLocalRotation,
                hubToShockBottomLocalOffset =
                    configuredHubToShockBottomLocalOffset,
                shockBottomLocalRotation = configuredShockBottomLocalRotation,
                hubToSteeringOuterLocalOffset =
                    configuredHubToSteeringOuterLocalOffset,
                steeringOuterLocalRotation =
                    configuredSteeringOuterLocalRotation,
                hubToDiscBrakeLocalOffset =
                    configuredHubToDiscBrakeLocalOffset,
                discBrakeLocalRotation = configuredDiscBrakeLocalRotation,
                hubToRoadWheelLocalOffset =
                    configuredHubToRoadWheelLocalOffset,
                roadWheelLocalRotation = configuredRoadWheelLocalRotation,
                leftSide = configuredLeftSide,
            };
        }

        public string CornerId => cornerId ?? string.Empty;
        public WheelController Wheel => wheel;
        public MountPointAuthoring WishboneMount => wishboneMount;
        public MountPointAuthoring SpindleMount => spindleMount;
        public MountPointAuthoring StrutMount => strutMount;
        public Transform ShockBottomTarget => shockBottomTarget;
        public SatsumaFrontStrutPresentation StrutPresentation =>
            strutPresentation;
        public MountPointAuthoring SteeringRodMount => steeringRodMount;
        public MountPointAuthoring DiscBrakeMount => discBrakeMount;
        public MountPointAuthoring RoadWheelMount => roadWheelMount;
        public Transform SteeringOuterTarget => steeringOuterTarget;
        public SatsumaFrontSteeringRodPresentation SteeringRodPresentation =>
            steeringRodPresentation;
        public Vector3 FullDroopHubLocalPosition =>
            fullDroopHubLocalPosition;
        public Quaternion FullDroopHubLocalRotation =>
            fullDroopHubLocalRotation;
        public Vector3 CanonicalNoStrutHubLocalPosition =>
            canonicalNoStrutHubLocalPosition;
        public Quaternion CanonicalNoStrutHubLocalRotation =>
            canonicalNoStrutHubLocalRotation;
        public Vector3 WishboneBodyPivotLocalPosition =>
            wishboneBodyPivotLocalPosition;
        public Quaternion WishboneMeshZeroLocalRotation =>
            wishboneMeshZeroLocalRotation;
        public Vector3 HubToWishboneTargetLocalOffset =>
            hubToWishboneTargetLocalOffset;
        public Vector3 HubToSpindleMeshLocalOffset =>
            hubToSpindleMeshLocalOffset;
        public Quaternion SpindleMeshLocalRotation =>
            spindleMeshLocalRotation;
        public Vector3 HubToShockBottomLocalOffset =>
            hubToShockBottomLocalOffset;
        public Quaternion ShockBottomLocalRotation =>
            shockBottomLocalRotation;
        public Vector3 HubToSteeringOuterLocalOffset =>
            hubToSteeringOuterLocalOffset;
        public Quaternion SteeringOuterLocalRotation =>
            steeringOuterLocalRotation;
        public Vector3 HubToDiscBrakeLocalOffset =>
            hubToDiscBrakeLocalOffset;
        public Quaternion DiscBrakeLocalRotation =>
            discBrakeLocalRotation;
        public Vector3 HubToRoadWheelLocalOffset =>
            hubToRoadWheelLocalOffset;
        public Quaternion RoadWheelLocalRotation =>
            roadWheelLocalRotation;
        public bool LeftSide => leftSide;
    }

    /// <summary>
    /// Rebuilds the donor front-suspension presentation around the hub centre.
    /// The donor drives the installed wishbone by transform IK, never by a
    /// free gravitational Rigidbody. NWH supplies the ground-dependent hub
    /// even without a strut; the canonical pose is only an uninitialized/air
    /// fallback, never an override of a live contact solution.
    /// </summary>
    [DefaultExecutionOrder(125)]
    [DisallowMultipleComponent]
    public sealed class SatsumaFrontSuspensionController : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private Collider[] chassisColliders =
            Array.Empty<Collider>();
        [SerializeField] private SatsumaFrontSuspensionCornerBinding[] corners =
            Array.Empty<SatsumaFrontSuspensionCornerBinding>();

        private readonly Dictionary<PartInstance, bool>
            ignoredCollisionState = new Dictionary<PartInstance, bool>();

        public VehicleAssemblyController AssemblyController =>
            assemblyController;
        public Collider[] ChassisColliders => chassisColliders;
        public SatsumaFrontSuspensionCornerBinding[] Corners => corners;

        public void Configure(
            VehicleAssemblyController configuredAssemblyController,
            SatsumaFrontSuspensionCornerBinding[] configuredCorners)
        {
            Configure(
                configuredAssemblyController,
                Array.Empty<Collider>(),
                configuredCorners);
        }

        public void Configure(
            VehicleAssemblyController configuredAssemblyController,
            Collider[] configuredChassisColliders,
            SatsumaFrontSuspensionCornerBinding[] configuredCorners)
        {
            assemblyController = configuredAssemblyController;
            chassisColliders = configuredChassisColliders ??
                Array.Empty<Collider>();
            corners = configuredCorners ??
                Array.Empty<SatsumaFrontSuspensionCornerBinding>();
            ApplyNow();
        }

        private void OnEnable()
        {
            ApplyNow();
        }

        private void FixedUpdate()
        {
            ApplyNow();
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<PartInstance, bool> pair in
                     ignoredCollisionState)
            {
                if (pair.Key != null && pair.Value)
                {
                    SetChassisCollisionIgnored(pair.Key, ignored: false);
                }
            }

            ignoredCollisionState.Clear();
        }

        public void ApplyNow()
        {
            Transform chassis = transform;
            for (int index = 0; index < corners.Length; index++)
            {
                ApplyCorner(chassis, corners[index]);
            }

            RefreshInstalledCollisionPairs();

            // VehicleAssemblyController runs before this adapter. Repeat its
            // cheap pose pass after moving the donor mount roots so installed
            // parts, child mounts, fasteners and raycast proxies stay together.
            assemblyController?.SynchronizeInstalledParts();
        }

        private void ApplyCorner(
            Transform chassis,
            SatsumaFrontSuspensionCornerBinding binding)
        {
            PartInstance wishbone = ResolveInstalledPart(
                binding.WishboneMount);
            PartInstance spindle = ResolveInstalledPart(
                binding.SpindleMount);
            PartInstance roadWheel = ResolveInstalledPart(
                binding.RoadWheelMount);
            bool strutInstalled = ResolveInstalledPart(
                binding.StrutMount) != null;

            if (!strutInstalled)
            {
                // The same contact solver moves this hub up when ground enters
                // its travel. Only an airborne/uninitialized hub uses -0.20 m.
                // The mesh remains IK presentation, not a free gravity hinge.
                ApplyKinematicCornerPose(
                    chassis,
                    binding,
                    useCanonicalNoStrutPose: true);
                wishbone?.SetInstalledPhysicalAttachmentActive(active: false);
                spindle?.SetInstalledPhysicalAttachmentActive(active: false);
                roadWheel?.SetInstalledPhysicalAttachmentActive(
                    active: binding.Wheel == null || !binding.Wheel.enabled);
                binding.StrutPresentation?.RefreshPresentation();
                binding.SteeringRodPresentation?.RefreshPresentation();
                return;
            }

            // A complete strut changes the NWH spring stage. Presentation stays
            // kinematic so it never fights the existing contact authority.
            ApplyKinematicCornerPose(
                chassis,
                binding,
                useCanonicalNoStrutPose: false);
            spindle?.SetInstalledPhysicalAttachmentActive(active: false);
            wishbone?.SetInstalledPhysicalAttachmentActive(active: false);
            roadWheel?.SetInstalledPhysicalAttachmentActive(active: false);
            binding.StrutPresentation?.RefreshPresentation();
            binding.SteeringRodPresentation?.RefreshPresentation();
        }

        private static void ApplyKinematicCornerPose(
            Transform chassis,
            SatsumaFrontSuspensionCornerBinding binding,
            bool useCanonicalNoStrutPose)
        {
            ResolveHubPose(
                chassis,
                binding,
                useCanonicalNoStrutPose,
                out Vector3 hubWorldPosition,
                out Quaternion hubWorldRotation,
                out Quaternion rotatingHubWorldRotation);

            Vector3 wishboneTargetWorld = hubWorldPosition +
                hubWorldRotation * binding.HubToWishboneTargetLocalOffset;
            Vector3 wishboneTargetLocal = chassis.InverseTransformPoint(
                wishboneTargetWorld);
            Vector3 wishboneDirection = wishboneTargetLocal -
                binding.WishboneBodyPivotLocalPosition;
            wishboneDirection.z = 0f;
            Vector3 outward = binding.LeftSide
                ? Vector3.left
                : Vector3.right;
            float wishboneAngle = wishboneDirection.sqrMagnitude > 0.0000001f
                ? Vector3.SignedAngle(
                    outward,
                    wishboneDirection.normalized,
                    Vector3.forward)
                : 0f;
            Quaternion wishboneWorldRotation = chassis.rotation *
                Quaternion.AngleAxis(wishboneAngle, Vector3.forward) *
                binding.WishboneMeshZeroLocalRotation;
            SetMountWorldPose(
                binding.WishboneMount,
                chassis.TransformPoint(
                    binding.WishboneBodyPivotLocalPosition),
                wishboneWorldRotation);

            Vector3 spindleWorldPosition = hubWorldPosition +
                hubWorldRotation * binding.HubToSpindleMeshLocalOffset;
            Quaternion spindleWorldRotation = hubWorldRotation *
                binding.SpindleMeshLocalRotation;
            SetMountWorldPose(
                binding.SpindleMount,
                spindleWorldPosition,
                spindleWorldRotation);

            if (binding.ShockBottomTarget != null)
            {
                binding.ShockBottomTarget.SetPositionAndRotation(
                    hubWorldPosition +
                    hubWorldRotation * binding.HubToShockBottomLocalOffset,
                    hubWorldRotation * binding.ShockBottomLocalRotation);
            }

            if (binding.SteeringOuterTarget != null)
            {
                binding.SteeringOuterTarget.SetPositionAndRotation(
                    hubWorldPosition + hubWorldRotation *
                    binding.HubToSteeringOuterLocalOffset,
                    hubWorldRotation *
                    binding.SteeringOuterLocalRotation);
            }

            SetMountWorldPose(
                binding.DiscBrakeMount,
                hubWorldPosition + rotatingHubWorldRotation *
                binding.HubToDiscBrakeLocalOffset,
                rotatingHubWorldRotation *
                binding.DiscBrakeLocalRotation);
            SetMountWorldPose(
                binding.RoadWheelMount,
                hubWorldPosition + rotatingHubWorldRotation *
                binding.HubToRoadWheelLocalOffset,
                rotatingHubWorldRotation *
                binding.RoadWheelLocalRotation);

        }

        private static void ResolveHubPose(
            Transform chassis,
            SatsumaFrontSuspensionCornerBinding binding,
            bool useCanonicalNoStrutPose,
            out Vector3 worldPosition,
            out Quaternion worldRotation,
            out Quaternion rotatingWorldRotation)
        {
            WheelController wheel = binding.Wheel;
            Transform nonRotating = wheel != null
                ? wheel.wheel.nonRotatingContainer
                : null;
            Transform rotating = wheel != null
                ? wheel.wheel.rotatingContainer
                : null;
            bool useNwhPose = Application.isPlaying &&
                wheel != null &&
                wheel.enabled &&
                nonRotating != null &&
                wheel.wheel.meshCollider != null;
            if (useNwhPose)
            {
                worldPosition = wheel.WheelPosition;
                worldRotation = nonRotating.rotation;
                rotatingWorldRotation = rotating != null
                    ? rotating.rotation
                    : worldRotation;
                return;
            }

            if (useCanonicalNoStrutPose)
            {
                worldPosition = chassis.TransformPoint(
                    binding.CanonicalNoStrutHubLocalPosition);
                worldRotation = chassis.rotation *
                    binding.CanonicalNoStrutHubLocalRotation;
                rotatingWorldRotation = worldRotation;
                return;
            }

            worldPosition = chassis.TransformPoint(
                binding.FullDroopHubLocalPosition);
            worldRotation = chassis.rotation *
                binding.FullDroopHubLocalRotation;
            rotatingWorldRotation = worldRotation;
        }

        private PartInstance ResolveInstalledPart(
            MountPointAuthoring mount)
        {
            if (assemblyController == null || mount == null ||
                !assemblyController.Graph.TryGetMount(
                    mount.MountId,
                    out MountPointRuntime runtime))
            {
                return null;
            }

            return runtime.InstalledPart;
        }

        private void RefreshInstalledCollisionPairs()
        {
            if (assemblyController == null)
            {
                return;
            }

            PartInstance[] parts = assemblyController.Parts;
            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance part = parts[index];
                if (!IsTransientFrontPart(part))
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
                    Collider chassisCollider = chassisColliders[chassisIndex];
                    if (chassisCollider != null &&
                        !chassisCollider.isTrigger)
                    {
                        Physics.IgnoreCollision(
                            partCollider,
                            chassisCollider,
                            ignored);
                    }
                }
            }
        }

        private static bool IsTransientFrontPart(PartInstance part)
        {
            string id = part?.Definition?.DefinitionId;
            return !string.IsNullOrEmpty(id) &&
                (id.IndexOf("wishbone-", StringComparison.Ordinal) >= 0 ||
                 id.IndexOf("spindle-", StringComparison.Ordinal) >= 0);
        }

        private static void SetMountWorldPose(
            MountPointAuthoring mount,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (mount == null)
            {
                return;
            }

            Transform target = mount.transform;
            target.SetPositionAndRotation(worldPosition, worldRotation);
            target.localScale = Vector3.one;
        }
    }
}
