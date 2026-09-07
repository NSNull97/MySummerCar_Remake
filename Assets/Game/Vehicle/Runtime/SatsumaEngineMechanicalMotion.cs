using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaMechanicalDrive { Crankshaft, Camshaft, WaterPump, Alternator, RadiatorFan }

    [Serializable]
    public sealed class SatsumaShaftFastenerBinding
    {
        [SerializeField] private int shaftIndex;
        [SerializeField] private Transform leaf;
        public int ShaftIndex => shaftIndex;
        public Transform Leaf => leaf;
        public SatsumaShaftFastenerBinding(int source, Transform rendererLeaf) { shaftIndex = source; leaf = rendererLeaf; }
    }

    [Serializable]
    public sealed class SatsumaShaftVisualBinding
    {
        [SerializeField] private PartInstance owner;
        [SerializeField] private Transform leaf;
        [SerializeField] private Vector3 localAxis;
        [SerializeField] private SatsumaMechanicalDrive drive;
        public PartInstance Owner => owner;
        public Transform Leaf => leaf;
        public Vector3 LocalAxis => localAxis;
        public SatsumaMechanicalDrive Drive => drive;
        public SatsumaShaftVisualBinding(PartInstance part, Transform visual, Vector3 axis, SatsumaMechanicalDrive source)
        { owner = part; leaf = visual; localAxis = axis.normalized; drive = source; }
    }

    /// <summary>Transient presentation, after adjustment owners and before the existing visual vibration.</summary>
    [DefaultExecutionOrder(175)]
    [DisallowMultipleComponent]
    public sealed class SatsumaEngineMechanicalMotion : MonoBehaviour
    {
        // Outer mesh diameters, not a new drivetrain/physical belt solver.
        public const float PumpRatio = .120217f / .119247f;
        public const float AlternatorRatio = .120217f / .088049f;
        // Frozen radiator_flect quaternion curve: one turn in .13333334 seconds.
        public const float ElectricFanRpm = 450f;
        [SerializeField] private VehicleSimulationHost simulation;
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private PartInstance block;
        [SerializeField] private PartInstance crankshaft;
        [SerializeField] private PartInstance camshaft;
        [SerializeField] private PartInstance timingChain;
        [SerializeField] private AssemblyCamshaftTimingState timing;
        [SerializeField] private AssemblyEngineAdjustmentState alternatorSetting;
        [SerializeField] private SatsumaShaftVisualBinding[] shafts = Array.Empty<SatsumaShaftVisualBinding>();
        [SerializeField] private SatsumaReciprocatingVisualBinding[] reciprocating = Array.Empty<SatsumaReciprocatingVisualBinding>();
        [SerializeField] private SatsumaShaftFastenerBinding[] fasteners = Array.Empty<SatsumaShaftFastenerBinding>();
        private Quaternion[] baseRotations = Array.Empty<Quaternion>();
        private Quaternion[] appliedRotations = Array.Empty<Quaternion>();
        private bool[] applied = Array.Empty<bool>();
        private readonly float[] degrees = new float[5];
        private SatsumaReciprocatingMesh[] meshMotion = Array.Empty<SatsumaReciprocatingMesh>();
        private float beltPhase;
        private AssemblyAlternatorBeltPresentation belt;
        private VehicleSimulationHost subscribedHost;
        private FollowerPose[] followerPoses = Array.Empty<FollowerPose>();
        public SatsumaShaftVisualBinding[] Shafts => shafts;
        public SatsumaReciprocatingVisualBinding[] Reciprocating => reciprocating;
        public VehicleSimulationHost Simulation => simulation;
        public SatsumaShaftFastenerBinding[] Fasteners => fasteners;
        public float CrankDegrees => degrees[0];
        public float CamDegrees => degrees[1];

        public void Configure(VehicleSimulationHost host, VehicleAssemblyController owner, PartInstance engineBlock,
            PartInstance crank, PartInstance cam, PartInstance chain, AssemblyCamshaftTimingState camTiming,
            AssemblyEngineAdjustmentState alternator, SatsumaShaftVisualBinding[] rotating,
            SatsumaReciprocatingVisualBinding[] reciprocatingMeshes)
        {
            if (host == null || owner == null || engineBlock == null || crank == null || cam == null || chain == null ||
                camTiming == null || alternator == null || rotating == null || reciprocatingMeshes == null)
                throw new ArgumentException("Mechanical motion requires explicit existing engine bindings.");
            foreach (var binding in rotating)
                if (binding?.Owner == null || !SatsumaEngineVisualVibration.IsSafeVisualLeaf(binding.Leaf) ||
                    !binding.Leaf.IsChildOf(binding.Owner.transform) || binding.LocalAxis.sqrMagnitude < .99f ||
                    !Enum.IsDefined(typeof(SatsumaMechanicalDrive), binding.Drive))
                    throw new ArgumentException("Only explicit collider-free render leaves may rotate.");
            foreach (var binding in reciprocatingMeshes) binding.Validate();
            Release();
            simulation = host; assembly = owner; block = engineBlock; crankshaft = crank; camshaft = cam; timingChain = chain;
            timing = camTiming; alternatorSetting = alternator; shafts = rotating; reciprocating = reciprocatingMeshes;
            InitializeTracking();
        }

        public void ConfigureFasteners(SatsumaShaftFastenerBinding[] bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            foreach (var binding in bindings)
                if (binding == null || binding.ShaftIndex < 0 || binding.ShaftIndex >= shafts.Length ||
                    !SatsumaEngineVisualVibration.IsSafeVisualLeaf(binding.Leaf))
                    throw new ArgumentException("Only collider-free fastener render leaves may follow a shaft.");
            RemoveFollowerPoses(); fasteners = (SatsumaShaftFastenerBinding[])bindings.Clone();
            followerPoses = new FollowerPose[fasteners.Length];
        }

        private void InitializeTracking()
        {
            if (baseRotations.Length != shafts.Length)
            { baseRotations = new Quaternion[shafts.Length]; appliedRotations = new Quaternion[shafts.Length]; applied = new bool[shafts.Length]; }
            if (followerPoses.Length != fasteners.Length) followerPoses = new FollowerPose[fasteners.Length];
            if (subscribedHost != simulation)
            {
                Unsubscribe(); subscribedHost = simulation;
                if (subscribedHost != null)
                { subscribedHost.SimulationReset += ResetPresentation; subscribedHost.SimulationRestored += ResetPresentation; }
            }
        }

        private void LateUpdate() => ApplyFrame(Time.timeScale > 0f ? Time.deltaTime : 0f);

        public void ApplyFrame(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f || simulation?.State == null ||
                simulation.SatsumaOperatingSourceComponent is not SatsumaEngineOperatingSource source) return;
            InitializeTracking();
            RemoveFollowerPoses();
            RemoveRotations();
            float dt = Mathf.Min(deltaSeconds, .25f);
            bool crankConnected = block.IsInstalled && crankshaft.IsInstalled;
            bool camConnected = crankConnected && camshaft.IsInstalled && timingChain.IsInstalled && timing.Part.IsInstalled;
            float rpm = crankConnected && float.IsFinite(simulation.State.EngineRpm) ? Mathf.Max(0f, simulation.State.EngineRpm) : 0f;
            var conditions = source.CaptureConditions();
            float drive = Mathf.Clamp01(simulation.Root.SatsumaOperatingModel.LastPoint.BeltDrive01);
            bool beltConnected = crankConnected && conditions.Ancillary.BeltUsable;
            bool pumpConnected = beltConnected && conditions.Ancillary.WaterPumpUsable;
            bool alternatorConnected = beltConnected && conditions.Ancillary.AlternatorUsable;
            bool fanOn = simulation.State.SatsumaOperating.RadiatorFanRunning;
            Advance(0, rpm, dt); Advance(1, camConnected ? rpm * .5f : 0f, dt);
            Advance(2, pumpConnected ? rpm * drive * PumpRatio : 0f, dt);
            Advance(3, alternatorConnected ? rpm * drive * AlternatorRatio : 0f, dt);
            Advance(4, fanOn ? ElectricFanRpm : 0f, dt);
            for (int i = 0; i < shafts.Length; i++)
            {
                SatsumaShaftVisualBinding binding = shafts[i];
                if (binding.Owner == null || !binding.Owner.IsInstalled || binding.Leaf == null || !binding.Leaf.gameObject.activeInHierarchy) continue;
                bool connected = binding.Drive == SatsumaMechanicalDrive.RadiatorFan || crankConnected;
                if (!connected) continue;
                // Read the authoritative tuning pose every frame, then compose.
                // Do not absorb last frame's shaft phase into a saved setting.
                baseRotations[i] = binding.Leaf.localRotation;
                appliedRotations[i] = baseRotations[i] * Quaternion.AngleAxis(degrees[(int)binding.Drive], binding.LocalAxis);
                binding.Leaf.localRotation = appliedRotations[i]; applied[i] = true;
            }
            ApplyFollowerPoses();
            UpdateBelt(beltConnected && pumpConnected && alternatorConnected && rpm > 0f, rpm * drive, dt);
            ApplyReciprocating(crankConnected);
        }

        private void ApplyReciprocating(bool crankConnected)
        {
            if (meshMotion.Length != reciprocating.Length) meshMotion = new SatsumaReciprocatingMesh[reciprocating.Length];
            float valveCycle = degrees[1] * 2f + timing.AngleDegrees * 2f;
            for (int i = 0; i < reciprocating.Length; i++)
            {
                var binding = reciprocating[i];
                bool attached = binding.Owner != null && binding.Owner.IsInstalled &&
                    (binding.Kind == SatsumaReciprocatingKind.Piston ? crankConnected : block.IsInstalled && camshaft.IsInstalled);
                if (!attached)
                { meshMotion[i]?.Dispose(); meshMotion[i] = null; continue; }
                if (!binding.Filter.gameObject.activeInHierarchy) continue;
                meshMotion[i] ??= new SatsumaReciprocatingMesh(binding);
                meshMotion[i].Apply(degrees[0], valveCycle);
            }
        }

        private void Advance(int index, float rpm, float dt)
        { degrees[index] = Mathf.Repeat(degrees[index] + rpm * 6f * dt, index == 0 ? 720f : 360f); }

        private void ApplyFollowerPoses()
        {
            for (int i = 0; i < fasteners.Length; i++)
            {
                var binding = fasteners[i]; int source = binding.ShaftIndex;
                Transform leaf = binding.Leaf;
                if (!applied[source] || leaf == null || !leaf.gameObject.activeInHierarchy) continue;
                Transform shaft = shafts[source].Leaf;
                Quaternion baseWorld = (shaft.parent != null ? shaft.parent.rotation : Quaternion.identity) * baseRotations[source];
                Quaternion delta = shaft.rotation * Quaternion.Inverse(baseWorld);
                var pose = new FollowerPose { BasePosition = leaf.localPosition, BaseRotation = leaf.localRotation, Applied = true };
                leaf.SetPositionAndRotation(shaft.position + delta * (leaf.position - shaft.position), delta * leaf.rotation);
                pose.AppliedPosition = leaf.localPosition; pose.AppliedRotation = leaf.localRotation; followerPoses[i] = pose;
            }
        }

        private void RemoveFollowerPoses()
        {
            for (int i = 0; i < followerPoses.Length; i++)
            {
                var pose = followerPoses[i]; Transform leaf = fasteners[i].Leaf;
                if (pose.Applied && leaf != null)
                {
                    if ((leaf.localPosition - pose.AppliedPosition).sqrMagnitude < 1e-12f) leaf.localPosition = pose.BasePosition;
                    if (Mathf.Abs(Quaternion.Dot(leaf.localRotation, pose.AppliedRotation)) > .9999999f) leaf.localRotation = pose.BaseRotation;
                }
                followerPoses[i].Applied = false;
            }
        }

        private struct FollowerPose
        {
            public Vector3 BasePosition, AppliedPosition;
            public Quaternion BaseRotation, AppliedRotation;
            public bool Applied;
        }

        private void UpdateBelt(bool rotating, float rpm, float dt)
        {
            AssemblyAlternatorBeltPresentation current = null;
            if (assembly.Graph.TryGetMount(SatsumaConsumableAssemblyRules.BeltMountId, out MountPointRuntime mount) && mount.IsOccupied)
                current = mount.InstalledPart.GetComponent<AssemblyAlternatorBeltPresentation>();
            if (belt != current) { if (belt != null) belt.ResetOperatingMotion(); belt = current; }
            if (rotating) beltPhase = Mathf.Repeat(beltPhase - rpm / 60f * dt, 1f);
            if (belt != null) belt.ApplyOperatingMotion(beltPhase, alternatorSetting.Setting, rotating);
        }

        private void RemoveRotations()
        {
            for (int i = 0; i < applied.Length; i++)
            {
                if (applied[i] && shafts[i].Leaf != null && Mathf.Abs(Quaternion.Dot(shafts[i].Leaf.localRotation, appliedRotations[i])) > .9999999f)
                    shafts[i].Leaf.localRotation = baseRotations[i];
                applied[i] = false;
            }
        }
        public void ResetPresentation()
        {
            // Peel off the later presentation layer first, including when a
            // save restore/disable happens between LateUpdate and the next Update.
            GetComponent<SatsumaEngineVisualVibration>()?.ResetPresentation();
            RemoveFollowerPoses(); RemoveRotations(); Array.Clear(degrees, 0, degrees.Length); beltPhase = 0f;
            if (belt != null) belt.ResetOperatingMotion(); belt = null;
            foreach (var mesh in meshMotion) mesh?.Dispose();
            meshMotion = Array.Empty<SatsumaReciprocatingMesh>();
        }
        private void Unsubscribe()
        {
            if (subscribedHost != null)
            { subscribedHost.SimulationReset -= ResetPresentation; subscribedHost.SimulationRestored -= ResetPresentation; }
            subscribedHost = null;
        }
        private void Release() { ResetPresentation(); Unsubscribe(); }
        private void OnEnable() => InitializeTracking();
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
