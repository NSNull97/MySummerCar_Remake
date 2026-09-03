using System;
using MSC.Vehicle.Assembly;
using NWH.WheelController3D;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// One authored suspension stage. Forces are N, travel m and damping Ns/m;
    /// NWH's normalized spring curve is explicitly part of the conversion.
    /// </summary>
    [Serializable]
    public struct NwhAssemblySuspensionStage
    {
        [SerializeField] private Vector3 topLocalPosition;
        [SerializeField] private float travelMeters;
        [SerializeField] private float maximumForceNewtons;
        [SerializeField] private float camberDegrees;
        [SerializeField] private AnimationCurve forceCurve;
        [SerializeField] private float bumpRate;
        [SerializeField] private float reboundRate;
        [SerializeField] private float slowBump;
        [SerializeField] private float fastBump;
        [SerializeField] private float slowRebound;
        [SerializeField] private float fastRebound;
        [SerializeField] private float bumpTransitionVelocity;
        [SerializeField] private float reboundTransitionVelocity;

        public Vector3 TopLocalPosition => topLocalPosition;
        public float TravelMeters => travelMeters;
        public float MaximumForceNewtons => maximumForceNewtons;
        public float CamberDegrees => camberDegrees;
        public AnimationCurve ForceCurve => forceCurve;
        public float BumpRate => bumpRate;
        public float ReboundRate => reboundRate;
        public float SlowBump => slowBump;
        public float FastBump => fastBump;
        public float SlowRebound => slowRebound;
        public float FastRebound => fastRebound;
        public float BumpTransitionVelocity => bumpTransitionVelocity;
        public float ReboundTransitionVelocity => reboundTransitionVelocity;

        public static NwhAssemblySuspensionStage Capture(WheelController wheel)
        {
            return new NwhAssemblySuspensionStage
            {
                topLocalPosition = wheel.transform.localPosition,
                travelMeters = wheel.SpringMaxLength,
                maximumForceNewtons = wheel.SpringMaxForce,
                camberDegrees = wheel.Camber,
                forceCurve = wheel.spring.forceCurve,
                bumpRate = wheel.DamperBumpRate,
                reboundRate = wheel.DamperReboundRate,
                slowBump = wheel.damper.slowBump,
                fastBump = wheel.damper.fastBump,
                slowRebound = wheel.damper.slowRebound,
                fastRebound = wheel.damper.fastRebound,
                bumpTransitionVelocity = wheel.damper.bumpDivisionVelocity,
                reboundTransitionVelocity = wheel.damper.reboundDivisionVelocity,
            };
        }

        public static NwhAssemblySuspensionStage CreateUnstrung(
            Vector3 top,
            float travel,
            float springRateNewtonPerMeter,
            float damperRateNewtonSecondsPerMeter,
            float transitionVelocity,
            float fastDamperFactor,
            float configuredCamberDegrees = 0f)
        {
            return new NwhAssemblySuspensionStage
            {
                topLocalPosition = top,
                travelMeters = travel,
                maximumForceNewtons = springRateNewtonPerMeter * travel,
                camberDegrees = configuredCamberDegrees,
                forceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                bumpRate = damperRateNewtonSecondsPerMeter,
                reboundRate = damperRateNewtonSecondsPerMeter,
                slowBump = 1f,
                fastBump = fastDamperFactor,
                slowRebound = 1f,
                fastRebound = fastDamperFactor,
                bumpTransitionVelocity = transitionVelocity,
                reboundTransitionVelocity = transitionVelocity,
            };
        }

        internal void Apply(WheelController wheel)
        {
            Vector3 oldTop = wheel.transform.localPosition;
            bool travelChanged = !Mathf.Approximately(
                wheel.SpringMaxLength, travelMeters);
            bool poseChanged = (oldTop - topLocalPosition).sqrMagnitude >
                0.00000001f || travelChanged;

            // Preserve the hub height across a stage change and clear the old
            // velocity sample. A changed spring top is not a chassis impact.
            float length = Mathf.Clamp(
                wheel.spring.length + topLocalPosition.y - oldTop.y,
                0f, travelMeters);
            wheel.transform.localPosition = topLocalPosition;
            wheel.SpringMaxLength = travelMeters;
            wheel.SpringMaxForce = maximumForceNewtons;
            wheel.Camber = camberDegrees;
            if (forceCurve != null && forceCurve.length > 0)
            {
                wheel.spring.forceCurve = forceCurve;
            }
            wheel.DamperBumpRate = bumpRate;
            wheel.DamperReboundRate = reboundRate;
            wheel.damper.slowBump = slowBump;
            wheel.damper.fastBump = fastBump;
            wheel.damper.slowRebound = slowRebound;
            wheel.damper.fastRebound = fastRebound;
            wheel.damper.bumpDivisionVelocity = bumpTransitionVelocity;
            wheel.damper.reboundDivisionVelocity = reboundTransitionVelocity;
            if (poseChanged)
            {
                wheel.spring.length = length;
                wheel.spring.prevLength = length;
                wheel.spring.compressionVelocity = 0f;
                wheel.spring.compression = travelMeters > 0f
                    ? 1f - length / travelMeters : 1f;
                wheel.WakeFromSleep();
            }
        }
    }

    [Serializable]
    public struct NwhAssemblySuspensionStageProfile
    {
        [SerializeField] private bool enabled;
        [SerializeField] private string springMountId;
        [SerializeField] private string alternateSpringMountId;
        [SerializeField] private NwhAssemblySuspensionStage unstrung;
        [SerializeField] private NwhAssemblySuspensionStage sprung;
        [SerializeField] private NwhAssemblySuspensionStage alternateSprung;

        public NwhAssemblySuspensionStageProfile(
            string mountId,
            NwhAssemblySuspensionStage withoutSpring,
            NwhAssemblySuspensionStage withSpring)
        {
            enabled = true;
            springMountId = mountId;
            alternateSpringMountId = string.Empty;
            unstrung = withoutSpring;
            sprung = withSpring;
            alternateSprung = default;
        }

        public NwhAssemblySuspensionStageProfile(
            string primaryMountId,
            string alternateMountId,
            NwhAssemblySuspensionStage withoutSpring,
            NwhAssemblySuspensionStage withPrimarySpring,
            NwhAssemblySuspensionStage withAlternateSpring)
        {
            enabled = true;
            springMountId = primaryMountId ?? string.Empty;
            alternateSpringMountId = alternateMountId ?? string.Empty;
            unstrung = withoutSpring;
            sprung = withPrimarySpring;
            alternateSprung = withAlternateSpring;
        }

        public bool Enabled => enabled;
        public string SpringMountId => springMountId ?? string.Empty;
        public string AlternateSpringMountId =>
            alternateSpringMountId ?? string.Empty;
        public NwhAssemblySuspensionStage Unstrung => unstrung;
        public NwhAssemblySuspensionStage Sprung => sprung;
        public NwhAssemblySuspensionStage AlternateSprung => alternateSprung;
    }

    [Serializable]
    public struct NwhAssemblyWheelStageProfile
    {
        [SerializeField] private bool enabled;
        [SerializeField] private string roadWheelMountId;
        [SerializeField] private string damperMountId;
        [SerializeField] private float assemblyContactRadiusMeters;
        [SerializeField] private float assemblyContactWidthMeters;
        [SerializeField] private float rimContactRadiusMeters;
        [SerializeField] private float rimContactWidthMeters;
        [SerializeField] private float roadWheelRadiusMeters;
        [SerializeField] private float roadWheelWidthMeters;
        [SerializeField] private float springOnlyDamperRate;
        [SerializeField] private float dampedBumpRate;
        [SerializeField] private float dampedReboundRate;
        [SerializeField] private float assemblyLongitudinalGrip;
        [SerializeField] private float assemblyLateralGrip;
        [SerializeField] private float roadLongitudinalGrip;
        [SerializeField] private float roadLateralGrip;

        public NwhAssemblyWheelStageProfile(
            string installedRoadWheelMountId,
            string installedDamperMountId,
            float assemblyRadiusMeters,
            float assemblyWidthMeters,
            float roadRadiusMeters,
            float roadWidthMeters,
            float configuredSpringOnlyDamperRate,
            float configuredDampedBumpRate,
            float configuredDampedReboundRate,
            float configuredAssemblyLongitudinalGrip,
            float configuredAssemblyLateralGrip,
            float configuredRoadLongitudinalGrip,
            float configuredRoadLateralGrip)
            : this(
                installedRoadWheelMountId,
                installedDamperMountId,
                assemblyRadiusMeters,
                assemblyWidthMeters,
                roadRadiusMeters,
                roadWidthMeters,
                roadRadiusMeters,
                roadWidthMeters,
                configuredSpringOnlyDamperRate,
                configuredDampedBumpRate,
                configuredDampedReboundRate,
                configuredAssemblyLongitudinalGrip,
                configuredAssemblyLateralGrip,
                configuredRoadLongitudinalGrip,
                configuredRoadLateralGrip)
        {
        }

        public NwhAssemblyWheelStageProfile(
            string installedRoadWheelMountId,
            string installedDamperMountId,
            float assemblyRadiusMeters,
            float assemblyWidthMeters,
            float rimRadiusMeters,
            float rimWidthMeters,
            float roadRadiusMeters,
            float roadWidthMeters,
            float configuredSpringOnlyDamperRate,
            float configuredDampedBumpRate,
            float configuredDampedReboundRate,
            float configuredAssemblyLongitudinalGrip,
            float configuredAssemblyLateralGrip,
            float configuredRoadLongitudinalGrip,
            float configuredRoadLateralGrip)
        {
            enabled = true;
            roadWheelMountId = installedRoadWheelMountId ?? string.Empty;
            damperMountId = installedDamperMountId ?? string.Empty;
            assemblyContactRadiusMeters = Mathf.Max(
                0.01f,
                assemblyRadiusMeters);
            assemblyContactWidthMeters = Mathf.Max(
                0.01f,
                assemblyWidthMeters);
            rimContactRadiusMeters = Mathf.Max(0.01f, rimRadiusMeters);
            rimContactWidthMeters = Mathf.Max(0.01f, rimWidthMeters);
            roadWheelRadiusMeters = Mathf.Max(0.01f, roadRadiusMeters);
            roadWheelWidthMeters = Mathf.Max(0.01f, roadWidthMeters);
            springOnlyDamperRate = Mathf.Max(
                0f,
                configuredSpringOnlyDamperRate);
            dampedBumpRate = Mathf.Max(0f, configuredDampedBumpRate);
            dampedReboundRate = Mathf.Max(
                0f,
                configuredDampedReboundRate);
            assemblyLongitudinalGrip = Mathf.Max(
                0f,
                configuredAssemblyLongitudinalGrip);
            assemblyLateralGrip = Mathf.Max(
                0f,
                configuredAssemblyLateralGrip);
            roadLongitudinalGrip = Mathf.Max(
                0f,
                configuredRoadLongitudinalGrip);
            roadLateralGrip = Mathf.Max(
                0f,
                configuredRoadLateralGrip);
        }

        public bool Enabled => enabled;
        public string RoadWheelMountId => roadWheelMountId ?? string.Empty;
        public string DamperMountId => damperMountId ?? string.Empty;
        public float AssemblyContactRadiusMeters =>
            assemblyContactRadiusMeters;
        public float AssemblyContactWidthMeters => assemblyContactWidthMeters;
        public float RimContactRadiusMeters => rimContactRadiusMeters;
        public float RimContactWidthMeters => rimContactWidthMeters;
        public float RoadWheelRadiusMeters => roadWheelRadiusMeters;
        public float RoadWheelWidthMeters => roadWheelWidthMeters;
        public float SpringOnlyDamperRate => springOnlyDamperRate;
        public float DampedBumpRate => dampedBumpRate;
        public float DampedReboundRate => dampedReboundRate;
        public float AssemblyLongitudinalGrip => assemblyLongitudinalGrip;
        public float AssemblyLateralGrip => assemblyLateralGrip;
        public float RoadLongitudinalGrip => roadLongitudinalGrip;
        public float RoadLateralGrip => roadLateralGrip;
    }

    [Serializable]
    public struct NwhAssemblyWheelSupportBinding
    {
        [SerializeField] private string wheelId;
        [SerializeField] private WheelController wheel;
        [SerializeField] private bool disabled;
        [SerializeField] private string[] requiredOccupiedMountIds;
        [SerializeField] private string[] requiredAnyOccupiedMountIds;
        [SerializeField] private NwhAssemblyWheelStageProfile stageProfile;
        [SerializeField] private NwhAssemblySuspensionStageProfile suspensionProfile;

        public NwhAssemblyWheelSupportBinding(
            string id,
            WheelController targetWheel,
            string[] requiredMountIds,
            string[] requiredAnyMountIds)
        {
            wheelId = id ?? string.Empty;
            wheel = targetWheel;
            disabled = false;
            requiredOccupiedMountIds = requiredMountIds ?? Array.Empty<string>();
            requiredAnyOccupiedMountIds = requiredAnyMountIds ?? Array.Empty<string>();
            stageProfile = default;
            suspensionProfile = default;
        }

        public NwhAssemblyWheelSupportBinding(
            string id,
            WheelController targetWheel,
            string[] requiredMountIds,
            string[] requiredAnyMountIds,
            NwhAssemblyWheelStageProfile configuredStageProfile)
        {
            wheelId = id ?? string.Empty;
            wheel = targetWheel;
            disabled = false;
            requiredOccupiedMountIds = requiredMountIds ?? Array.Empty<string>();
            requiredAnyOccupiedMountIds = requiredAnyMountIds ?? Array.Empty<string>();
            stageProfile = configuredStageProfile;
            suspensionProfile = default;
        }

        public NwhAssemblyWheelSupportBinding(
            string id,
            WheelController targetWheel,
            string[] requiredMountIds,
            string[] requiredAnyMountIds,
            NwhAssemblyWheelStageProfile configuredStageProfile,
            NwhAssemblySuspensionStageProfile configuredSuspensionProfile)
            : this(id, targetWheel, requiredMountIds, requiredAnyMountIds,
                configuredStageProfile)
        {
            suspensionProfile = configuredSuspensionProfile;
        }

        public static NwhAssemblyWheelSupportBinding CreateDisabled(
            string id,
            WheelController targetWheel)
        {
            var binding = new NwhAssemblyWheelSupportBinding(
                id,
                targetWheel,
                Array.Empty<string>(),
                Array.Empty<string>());
            binding.disabled = true;
            return binding;
        }

        public string WheelId => wheelId;
        public WheelController Wheel => wheel;
        public bool Enabled => !disabled;
        public string[] RequiredOccupiedMountIds =>
            requiredOccupiedMountIds ?? Array.Empty<string>();
        public string[] RequiredAnyOccupiedMountIds =>
            requiredAnyOccupiedMountIds ?? Array.Empty<string>();
        public NwhAssemblyWheelStageProfile StageProfile => stageProfile;
        public NwhAssemblySuspensionStageProfile SuspensionProfile =>
            suspensionProfile;
    }

    /// <summary>
    /// Keeps NWH contact forces subordinate to the project-owned assembly
    /// graph. Missing structural prerequisites must not support the chassis
    /// through an invisible physics wheel. A binding can be explicitly disabled
    /// while an assembly pass is validated without wheel-physics authority.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class NwhAssemblyWheelSupportController : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private NwhAssemblyWheelSupportBinding[] bindings =
            Array.Empty<NwhAssemblyWheelSupportBinding>();

        private int observedGraphMutationCount = int.MinValue;
        private int observedWheelStageStateHash = int.MinValue;

        public VehicleAssemblyController AssemblyController =>
            assemblyController;

        public NwhAssemblyWheelSupportBinding[] Bindings => bindings;

        public void Configure(
            VehicleAssemblyController controller,
            NwhAssemblyWheelSupportBinding[] configuredBindings)
        {
            assemblyController = controller;
            bindings = configuredBindings ??
                Array.Empty<NwhAssemblyWheelSupportBinding>();
            observedGraphMutationCount = int.MinValue;
            observedWheelStageStateHash = int.MinValue;
            RefreshSupport(force: true);
        }

        private void OnEnable()
        {
            observedGraphMutationCount = int.MinValue;
            observedWheelStageStateHash = int.MinValue;
            RefreshSupport(force: true);
        }

        private void Update()
        {
            RefreshSupport(force: false);
        }

        private void FixedUpdate()
        {
            RefreshSupport(force: false);
        }

        public void RefreshSupport(bool force)
        {
            if (assemblyController == null)
            {
                SetAllSupported(false);
                return;
            }

            int mutationCount = assemblyController.GraphMutationCount;
            AssemblyGraph graph = assemblyController.Graph;
            int wheelStageStateHash = ComputeWheelStageStateHash(graph);
            if (!force && mutationCount == observedGraphMutationCount &&
                wheelStageStateHash == observedWheelStageStateHash)
            {
                // NWH creates its collider in Start, after our first OnEnable.
                // Its OnDisable does not disable that generated collider.
                for (int index = 0; index < bindings.Length; index++)
                {
                    WheelController wheel = bindings[index].Wheel;
                    SetGeneratedColliderEnabled(wheel,
                        wheel != null && wheel.enabled);
                }
                return;
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                NwhAssemblyWheelSupportBinding binding = bindings[index];
                if (Application.isPlaying && binding.Enabled &&
                    binding.Wheel != null && binding.SuspensionProfile.Enabled)
                {
                    NwhAssemblySuspensionStageProfile profile =
                        binding.SuspensionProfile;
                    NwhAssemblySuspensionStage stage = IsMountOccupied(
                        graph, profile.AlternateSpringMountId)
                        ? profile.AlternateSprung
                        : IsMountOccupied(graph, profile.SpringMountId)
                            ? profile.Sprung
                            : profile.Unstrung;
                    stage.Apply(binding.Wheel);
                }
                // Spring type owns travel/rate while the independently fitted
                // shock owns bump/rebound damping. Apply that override last.
                ApplyStageProfile(graph, binding);
                SetSupported(binding.Wheel, IsSupportPresent(graph, binding));
            }

            observedGraphMutationCount = mutationCount;
            observedWheelStageStateHash = wheelStageStateHash;
        }

        public bool IsSupported(int index)
        {
            return index >= 0 && index < bindings.Length &&
                bindings[index].Wheel != null &&
                bindings[index].Wheel.enabled;
        }

        public bool AllowsAxleStability(WheelController wheel)
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                NwhAssemblyWheelSupportBinding binding = bindings[index];
                if (binding.Wheel != wheel)
                {
                    continue;
                }

                return !binding.SuspensionProfile.Enabled ||
                    (assemblyController != null &&
                     (IsMountOccupied(
                          assemblyController.Graph,
                          binding.SuspensionProfile.SpringMountId) ||
                      IsMountOccupied(
                          assemblyController.Graph,
                          binding.SuspensionProfile.AlternateSpringMountId)));
            }

            return true;
        }

        private void SetAllSupported(bool supported)
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                SetSupported(bindings[index].Wheel, supported);
            }
        }

        private static bool IsSupportPresent(
            AssemblyGraph graph,
            NwhAssemblyWheelSupportBinding binding)
        {
            if (!binding.Enabled)
            {
                return false;
            }

            string[] required = binding.RequiredOccupiedMountIds;
            for (int index = 0; index < required.Length; index++)
            {
                if (!graph.TryGetMount(
                        required[index],
                        out MountPointRuntime mount) ||
                    !mount.IsOccupied)
                {
                    return false;
                }
            }

            string[] requiredAny = binding.RequiredAnyOccupiedMountIds;
            if (requiredAny.Length == 0)
            {
                return true;
            }

            for (int index = 0; index < requiredAny.Length; index++)
            {
                if (graph.TryGetMount(
                        requiredAny[index],
                        out MountPointRuntime mount) &&
                    mount.IsOccupied)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetSupported(
            WheelController wheel,
            bool supported)
        {
            if (wheel == null)
            {
                return;
            }

            SetGeneratedColliderEnabled(wheel, supported);
            if (wheel.enabled == supported)
            {
                return;
            }

            wheel.MotorTorque = 0f;
            wheel.BrakeTorque = 0f;
            wheel.SteerAngle = 0f;
            wheel.enabled = supported;
            if (supported)
            {
                wheel.WakeFromSleep();
            }
        }

        private static void SetGeneratedColliderEnabled(
            WheelController wheel, bool enabled)
        {
            // Use the live reference: Initialize destroys the previous child
            // deferred, so Find("Collider") can return the obsolete one.
            if (wheel != null && wheel.wheel.meshCollider != null)
            {
                wheel.wheel.meshCollider.enabled = enabled;
            }
        }

        private static void ApplyStageProfile(
            AssemblyGraph graph,
            NwhAssemblyWheelSupportBinding binding)
        {
            WheelController wheel = binding.Wheel;
            NwhAssemblyWheelStageProfile profile = binding.StageProfile;
            if (!binding.Enabled || wheel == null || !profile.Enabled)
            {
                return;
            }

            MountPointRuntime roadWheelMount = ResolveOccupiedMount(
                graph,
                profile.RoadWheelMountId);
            bool roadWheelInstalled = roadWheelMount != null;
            AssemblyWheelTireState tireState = roadWheelInstalled
                ? roadWheelMount.InstalledPart.GetComponentInChildren<
                    AssemblyWheelTireState>(true)
                : null;
            bool tireInstalled = roadWheelInstalled &&
                (tireState != null
                    ? tireState.HasTire
                    : Mathf.Approximately(
                        profile.RimContactRadiusMeters,
                        profile.RoadWheelRadiusMeters));
            bool damperInstalled = IsMountOccupied(
                graph,
                profile.DamperMountId);

            float rimRadius = tireState != null
                ? tireState.RimRadiusMeters
                : profile.RimContactRadiusMeters;
            float rimWidth = tireState != null
                ? tireState.RimWidthMeters
                : profile.RimContactWidthMeters;
            float tireRadius = tireState != null
                ? tireState.TireRadiusMeters
                : profile.RoadWheelRadiusMeters;
            float tireWidth = tireState != null
                ? tireState.TireWidthMeters
                : profile.RoadWheelWidthMeters;
            float targetRadius = !roadWheelInstalled
                ? profile.AssemblyContactRadiusMeters
                : tireInstalled
                    ? tireRadius
                    : rimRadius;
            float targetWidth = !roadWheelInstalled
                ? profile.AssemblyContactWidthMeters
                : tireInstalled
                    ? tireWidth
                    : rimWidth;
            bool geometryChanged =
                !Mathf.Approximately(wheel.Radius, targetRadius) ||
                !Mathf.Approximately(wheel.Width, targetWidth);

            wheel.Radius = targetRadius;
            wheel.Width = targetWidth;
            wheel.DamperBumpRate = damperInstalled
                ? profile.DampedBumpRate
                : profile.SpringOnlyDamperRate;
            wheel.DamperReboundRate = damperInstalled
                ? profile.DampedReboundRate
                : profile.SpringOnlyDamperRate;
            wheel.forwardFriction.grip = tireInstalled
                ? profile.RoadLongitudinalGrip
                : profile.AssemblyLongitudinalGrip;
            wheel.sideFriction.grip = tireInstalled
                ? profile.RoadLateralGrip
                : profile.AssemblyLateralGrip;

            // Radius/width changes after Start otherwise leave NWH's generated
            // physical collider at the previous stage (for example a full
            // tyre surrounding a bare brake drum). Rebuild only after NWH has
            // created its runtime collider; Start will handle the first stage.
            Collider existingCollider = wheel.wheel.meshCollider;
            if (Application.isPlaying &&
                geometryChanged &&
                existingCollider != null)
            {
                existingCollider.gameObject.SetActive(false);
                wheel.Initialize();
            }
        }

        private static bool IsMountOccupied(
            AssemblyGraph graph,
            string mountId)
        {
            return ResolveOccupiedMount(graph, mountId) != null;
        }

        private static MountPointRuntime ResolveOccupiedMount(
            AssemblyGraph graph,
            string mountId)
        {
            return !string.IsNullOrEmpty(mountId) &&
                graph.TryGetMount(mountId, out MountPointRuntime mount) &&
                mount.IsOccupied
                    ? mount
                    : null;
        }

        private int ComputeWheelStageStateHash(AssemblyGraph graph)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < bindings.Length; index++)
                {
                    NwhAssemblyWheelStageProfile profile =
                        bindings[index].StageProfile;
                    if (!profile.Enabled)
                    {
                        continue;
                    }

                    MountPointRuntime mount = ResolveOccupiedMount(
                        graph,
                        profile.RoadWheelMountId);
                    PartInstance part = mount?.InstalledPart;
                    hash = hash * 31 + (part != null
                        ? part.GetInstanceID()
                        : 0);
                    AssemblyWheelTireState tireState = part != null
                        ? part.GetComponentInChildren<
                            AssemblyWheelTireState>(true)
                        : null;
                    hash = hash * 31 + (tireState != null
                        ? tireState.Revision
                        : 0);
                    hash = hash * 31 + (tireState != null &&
                        tireState.HasTire
                            ? 1
                            : 0);
                }

                return hash;
            }
        }
    }
}
