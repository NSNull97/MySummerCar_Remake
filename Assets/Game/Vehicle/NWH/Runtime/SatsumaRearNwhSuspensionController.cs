using System;
using MSC.Vehicle.Assembly;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    [Serializable]
    public struct SatsumaRearNwhCornerBinding
    {
        [SerializeField] private string cornerId;
        [SerializeField] private WheelController wheel;
        [SerializeField] private MountPointAuthoring trailingArmMount;
        [SerializeField] private MountPointAuthoring drumMount;
        [SerializeField] private MountPointAuthoring roadWheelMount;
        [SerializeField] private string drumMountId;
        [SerializeField] private string roadWheelMountId;
        [SerializeField] private string stockSpringMountId;
        [SerializeField] private string longSpringMountId;
        [SerializeField] private Vector3 neutralArmPivotLocalPosition;
        [SerializeField] private Quaternion neutralArmLocalRotation;
        [SerializeField] private Vector3 neutralDrumMountLocalPosition;
        [SerializeField] private Quaternion neutralDrumMountLocalRotation;
        [SerializeField] private Vector3 neutralRoadWheelMountLocalPosition;
        [SerializeField] private Quaternion neutralRoadWheelMountLocalRotation;

        public string CornerId => cornerId ?? string.Empty;
        public WheelController Wheel => wheel;
        public MountPointAuthoring TrailingArmMount => trailingArmMount;
        public MountPointAuthoring DrumMount => drumMount;
        public MountPointAuthoring RoadWheelMount => roadWheelMount;
        public string DrumMountId => drumMountId ?? string.Empty;
        public string RoadWheelMountId => roadWheelMountId ?? string.Empty;
        public string StockSpringMountId => stockSpringMountId ?? string.Empty;
        public string LongSpringMountId => longSpringMountId ?? string.Empty;
        public Vector3 NeutralArmPivotLocalPosition =>
            neutralArmPivotLocalPosition;
        public Quaternion NeutralArmLocalRotation => neutralArmLocalRotation;
        public Vector3 NeutralDrumMountLocalPosition =>
            neutralDrumMountLocalPosition;
        public Quaternion NeutralDrumMountLocalRotation =>
            neutralDrumMountLocalRotation;
        public Vector3 NeutralRoadWheelMountLocalPosition =>
            neutralRoadWheelMountLocalPosition;
        public Quaternion NeutralRoadWheelMountLocalRotation =>
            neutralRoadWheelMountLocalRotation;

        public static SatsumaRearNwhCornerBinding Create(
            Transform vehicleRoot,
            SatsumaRearSuspensionCornerBinding corner,
            WheelController targetWheel,
            MountPointAuthoring armMount,
            MountPointAuthoring configuredDrumMount,
            MountPointAuthoring configuredRoadWheelMount)
        {
            if (vehicleRoot == null) throw new ArgumentNullException(nameof(vehicleRoot));
            if (corner == null) throw new ArgumentNullException(nameof(corner));
            if (targetWheel == null) throw new ArgumentNullException(nameof(targetWheel));
            if (armMount == null || armMount.Pose == null)
            {
                throw new ArgumentNullException(nameof(armMount));
            }
            if (configuredDrumMount == null)
            {
                throw new ArgumentNullException(nameof(configuredDrumMount));
            }
            if (configuredRoadWheelMount == null)
            {
                throw new ArgumentNullException(nameof(configuredRoadWheelMount));
            }

            return new SatsumaRearNwhCornerBinding
            {
                cornerId = corner.CornerId,
                wheel = targetWheel,
                trailingArmMount = armMount,
                drumMount = configuredDrumMount,
                roadWheelMount = configuredRoadWheelMount,
                drumMountId = corner.DrumMountId,
                roadWheelMountId = corner.RoadWheelMountId,
                stockSpringMountId = corner.StockSpringMountId,
                longSpringMountId = corner.LongSpringMountId,
                neutralArmPivotLocalPosition = vehicleRoot.InverseTransformPoint(
                    armMount.Pose.position),
                neutralArmLocalRotation = Quaternion.Inverse(vehicleRoot.rotation) *
                    armMount.Pose.rotation,
                neutralDrumMountLocalPosition =
                    configuredDrumMount.transform.localPosition,
                neutralDrumMountLocalRotation =
                    configuredDrumMount.transform.localRotation,
                neutralRoadWheelMountLocalPosition =
                    configuredRoadWheelMount.transform.localPosition,
                neutralRoadWheelMountLocalRotation =
                    configuredRoadWheelMount.transform.localRotation,
            };
        }
    }

    /// <summary>
    /// Donor-faithful rear suspension authority adapter. WheelController owns
    /// the ground ray, wheel-space spring and damping force on the chassis;
    /// the trailing arm, drum and fitted road wheel are kinematic presentation
    /// driven from that measured compression, matching Wheel.cs + rear IK.
    /// </summary>
    [DefaultExecutionOrder(125)]
    [DisallowMultipleComponent]
    public sealed class SatsumaRearNwhSuspensionController : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private SatsumaRearSuspensionController presentationController;
        [SerializeField] private SatsumaRearNwhCornerBinding[] corners =
            Array.Empty<SatsumaRearNwhCornerBinding>();

        public VehicleAssemblyController AssemblyController => assemblyController;
        public SatsumaRearSuspensionController PresentationController =>
            presentationController;
        public SatsumaRearNwhCornerBinding[] Corners => corners;

        public void Configure(
            VehicleAssemblyController assembly,
            SatsumaRearSuspensionController presentation,
            SatsumaRearNwhCornerBinding[] configuredCorners)
        {
            ReleaseAllCorners();
            assemblyController = assembly;
            presentationController = presentation;
            presentationController?.SetExternalWheelAuthority(active: true);
            corners = configuredCorners ?? Array.Empty<SatsumaRearNwhCornerBinding>();
            ApplyNow();
        }

        private void OnEnable()
        {
            presentationController?.SetExternalWheelAuthority(active: true);
            ApplyNow();
        }

        private void FixedUpdate() => ApplyNow();

        private void LateUpdate() => ApplyNow();

        private void OnDisable()
        {
            ReleaseAllCorners();
            presentationController?.SetExternalWheelAuthority(active: false);
        }

        public void ApplyNow()
        {
            if (assemblyController == null)
            {
                return;
            }

            for (int index = 0; index < corners.Length; index++)
            {
                ApplyCorner(corners[index]);
            }

            assemblyController.SynchronizeInstalledParts();
            for (int index = 0; index < corners.Length; index++)
            {
                ApplyCarrierRotation(corners[index]);
            }

            // The first pass settles the arm and neutral carrier mounts. The
            // second copies the NWH roll into the installed drum, its fastener
            // presentation and the fitted road wheel without creating another
            // physical axle or contact collider.
            assemblyController.SynchronizeInstalledParts();
            presentationController?.RefreshPresentationNow();
        }

        private void ApplyCorner(SatsumaRearNwhCornerBinding binding)
        {
            PartInstance arm = ResolveInstalledPart(
                binding.TrailingArmMount?.MountId);
            PartInstance drum = ResolveInstalledPart(binding.DrumMountId);
            PartInstance roadWheel = ResolveInstalledPart(
                binding.RoadWheelMountId);
            RestoreNeutralCarrierMounts(binding);
            if (arm == null || binding.TrailingArmMount?.Pose == null)
            {
                return;
            }

            // The donor wheel ray exists only once the physical hub carrier is
            // assembled. Until the brake drum is fitted the accepted V38 arm
            // remains a real gravity/contact hinge and must be allowed to sag.
            bool nwhOwnsCorner = drum != null && binding.Wheel != null &&
                binding.Wheel.enabled;
            if (!nwhOwnsCorner)
            {
                RestorePhysicalCorner(binding, arm, drum, roadWheel);
                return;
            }

            // The donor Wheel applies force to the chassis and only moves the
            // model/IK target. Solid installed-part contacts must not become a
            // second suspension owner alongside that raycast.
            arm.SetInstalledPhysicalAttachmentActive(active: false);
            drum.SetInstalledPhysicalAttachmentActive(active: false);
            roadWheel?.SetInstalledPhysicalAttachmentActive(active: false);

            bool hasStockSpring = ResolveInstalledPart(
                binding.StockSpringMountId) != null;
            bool hasLongSpring = ResolveInstalledPart(
                binding.LongSpringMountId) != null;
            float compression = ResolveCompression(binding.Wheel);
            float angle = SatsumaRearSuspensionTravel.ResolveArmAngle(
                binding.NeutralArmPivotLocalPosition,
                compression,
                hasStockSpring,
                hasLongSpring);

            Transform pose = binding.TrailingArmMount.Pose;
            pose.SetPositionAndRotation(
                transform.TransformPoint(binding.NeutralArmPivotLocalPosition),
                transform.rotation * Quaternion.AngleAxis(angle, Vector3.right) *
                binding.NeutralArmLocalRotation);
        }

        private void ApplyCarrierRotation(
            SatsumaRearNwhCornerBinding binding)
        {
            PartInstance arm = ResolveInstalledPart(
                binding.TrailingArmMount?.MountId);
            PartInstance drum = ResolveInstalledPart(binding.DrumMountId);
            WheelController wheel = binding.Wheel;
            Transform nonRotating = wheel != null
                ? wheel.wheel.nonRotatingContainer
                : null;
            Transform rotating = wheel != null
                ? wheel.wheel.rotatingContainer
                : null;
            if (arm == null || drum == null || wheel == null ||
                !wheel.enabled || nonRotating == null || rotating == null)
            {
                return;
            }

            Quaternion rollDelta = rotating.rotation *
                Quaternion.Inverse(nonRotating.rotation);
            ApplyWorldRoll(binding.DrumMount, rollDelta);
            ApplyWorldRoll(binding.RoadWheelMount, rollDelta);
        }

        private static void ApplyWorldRoll(
            MountPointAuthoring mount,
            Quaternion rollDelta)
        {
            if (mount == null)
            {
                return;
            }

            Transform mountTransform = mount.transform;
            mountTransform.SetPositionAndRotation(
                mountTransform.position,
                rollDelta * mountTransform.rotation);
        }

        private void RestorePhysicalCorner(
            SatsumaRearNwhCornerBinding binding,
            PartInstance arm,
            PartInstance drum,
            PartInstance roadWheel)
        {
            RestoreNeutralCarrierMounts(binding);
            Transform pose = binding.TrailingArmMount?.Pose;
            if (pose != null)
            {
                // Restore the authored zero BEFORE recreating the HingeJoint.
                // Otherwise the current NWH angle becomes Unity's new joint
                // reference and silently shifts the accepted V38 limits.
                pose.SetPositionAndRotation(
                    transform.TransformPoint(
                        binding.NeutralArmPivotLocalPosition),
                    transform.rotation * binding.NeutralArmLocalRotation);
            }

            assemblyController?.SynchronizeInstalledParts();
            arm?.SetInstalledPhysicalAttachmentActive(active: true);
            drum?.SetInstalledPhysicalAttachmentActive(active: true);
            roadWheel?.SetInstalledPhysicalAttachmentActive(active: true);
        }

        private static void RestoreNeutralCarrierMounts(
            SatsumaRearNwhCornerBinding binding)
        {
            SetLocalPose(
                binding.DrumMount,
                binding.NeutralDrumMountLocalPosition,
                binding.NeutralDrumMountLocalRotation);
            SetLocalPose(
                binding.RoadWheelMount,
                binding.NeutralRoadWheelMountLocalPosition,
                binding.NeutralRoadWheelMountLocalRotation);
        }

        private static void SetLocalPose(
            MountPointAuthoring mount,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            if (mount == null)
            {
                return;
            }

            mount.transform.SetLocalPositionAndRotation(
                localPosition,
                localRotation);
        }

        private void ReleaseAllCorners()
        {
            if (assemblyController == null || corners == null)
            {
                return;
            }

            for (int index = 0; index < corners.Length; index++)
            {
                SatsumaRearNwhCornerBinding binding = corners[index];
                RestorePhysicalCorner(
                    binding,
                    ResolveInstalledPart(binding.TrailingArmMount?.MountId),
                    ResolveInstalledPart(binding.DrumMountId),
                    ResolveInstalledPart(binding.RoadWheelMountId));
            }
        }

        private PartInstance ResolveInstalledPart(string mountId)
        {
            return !string.IsNullOrEmpty(mountId) &&
                   assemblyController.Graph.TryGetMount(
                       mountId,
                       out MountPointRuntime mount) &&
                   mount != null && mount.IsOccupied
                ? mount.InstalledPart
                : null;
        }

        private static float ResolveCompression(WheelController wheel)
        {
            if (wheel == null || !wheel.enabled ||
                !float.IsFinite(wheel.SpringMaxLength) ||
                !float.IsFinite(wheel.SpringLength))
            {
                return 0f;
            }

            return Mathf.Clamp(
                wheel.SpringMaxLength - wheel.SpringLength,
                0f,
                wheel.SpringMaxLength);
        }

    }
}
