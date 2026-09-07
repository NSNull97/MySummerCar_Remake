using System;
using System.Collections.Generic;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Render leaves and validated render-only skin rigs. Part roots, joint frames, mounting points,
    /// carry bodies and collision never move. Offset is removed before simulation
    /// presentation updates and reapplied afterwards; saves contain no noise.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class SatsumaEngineVisualVibration : MonoBehaviour
    {
        [SerializeField] private VehicleSimulationHost simulation;
        [SerializeField] private PartInstance block;
        [SerializeField] private Transform[] visualLeaves = Array.Empty<Transform>();
        [SerializeField] private PartInstance[] owners = Array.Empty<PartInstance>();
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private PartInstance[] transmittedOwners = Array.Empty<PartInstance>();
        [SerializeField] private float[] transmissionGains = Array.Empty<float>();
        private readonly List<DrivenVisual> driven = new();
        private int observedMutation = -1;
        private float phase;
        public Transform[] VisualLeaves => visualLeaves;
        public PartInstance[] Owners => owners;
        public VehicleSimulationHost Simulation => simulation;
        public PartInstance Block => block;
        public VehicleAssemblyController Assembly => assembly;
        public PartInstance[] TransmittedOwners => transmittedOwners;
        public float[] TransmissionGains => transmissionGains;
        public int DrivenVisualCount => driven.Count;

        public void Configure(VehicleSimulationHost host, PartInstance engineBlock,
            Transform[] leaves, PartInstance[] partOwners)
        {
            RemoveOffsets();
            if (host == null || engineBlock == null || leaves == null || partOwners == null ||
                leaves.Length != partOwners.Length) throw new ArgumentException("Explicit engine visual bindings are required.");
            for (int i = 0; i < leaves.Length; i++)
                if (partOwners[i] == null || !IsSafeVisualLeaf(leaves[i]))
                    throw new ArgumentException("Vibration may only bind collider-free renderer leaves, never physical transforms.");
            simulation = host; block = engineBlock;
            visualLeaves = (Transform[])leaves.Clone(); owners = (PartInstance[])partOwners.Clone();
            RebuildBindings();
        }

        public void ConfigureTransmission(VehicleAssemblyController source, PartInstance[] parts, float[] gains)
        {
            if (source == null || parts == null || gains == null || parts.Length != gains.Length)
                throw new ArgumentException("Explicit assembly and resilient presentation bindings are required.");
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] == null || !float.IsFinite(gains[i]) || gains[i] <= 0f || gains[i] > 1f)
                    throw new ArgumentException("Transmission bindings require a part and a bounded positive gain.");
            RemoveOffsets(); assembly = source;
            transmittedOwners = (PartInstance[])parts.Clone(); transmissionGains = (float[])gains.Clone();
            observedMutation = -1;
        }

        public static bool IsSafeVisualLeaf(Transform leaf) => leaf != null &&
            leaf.childCount == 0 && leaf.GetComponent<Renderer>() != null &&
            leaf.GetComponent<Collider>() == null && leaf.GetComponent<Rigidbody>() == null &&
            leaf.GetComponent<PartInstance>() == null;

        private void Awake() => observedMutation = -1;
        private void Update() => RemoveOffsets();
        private void LateUpdate()
        {
            if (simulation == null || simulation.State == null || simulation.Telemetry == null) return;
            ApplyFrame(simulation.State.EngineStatus, simulation.Telemetry.EngineRpm,
                simulation.Telemetry.EngineLoad01, Time.deltaTime);
        }

        public void ApplyFrame(VehicleEngineStatus status, float rpm, float load, float deltaSeconds)
        {
            RemoveOffsets();
            if (driven.Count == 0 || assembly != null && observedMutation != assembly.GraphMutationCount) RebuildBindings();
            if (block == null || !block.IsInstalled || status != VehicleEngineStatus.Running ||
                !float.IsFinite(rpm) || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0f) return;
            // Frozen MotorShake period depends on RPM, not rpm/60. Integrate the
            // visible sample over a frame to suppress high-RPM temporal aliasing.
            float advance = SatsumaEngineFeedbackRules.VibrationFrequencyHz(rpm) * (2f * Mathf.PI) * deltaSeconds;
            phase = Mathf.Repeat(phase + advance, 2f * Mathf.PI);
            float samplePhase = phase - advance * .5f;
            float attenuation = advance > .0001f ? Mathf.Sin(advance * .5f) / (advance * .5f) : 1f;
            Vector3 worldOffset = block.transform.TransformDirection(
                SatsumaEngineFeedbackRules.VibrationOffset(samplePhase, rpm, load, true)) * attenuation;
            float roll = Mathf.Sin(samplePhase) * Mathf.Lerp(.035f, .012f, Mathf.InverseLerp(800f, 6000f, rpm)) * attenuation;
            for (int i = 0; i < driven.Count; i++)
            {
                DrivenVisual binding = driven[i]; Transform leaf = binding.Leaf;
                if (leaf == null || binding.Owner == null || !binding.Owner.IsInstalled || !leaf.gameObject.activeInHierarchy) continue;
                binding.BasePosition = leaf.localPosition; binding.BaseRotation = leaf.localRotation;
                Quaternion rotation = Quaternion.AngleAxis(roll * binding.Gain, block.transform.right);
                Vector3 pivot = binding.Gain == 1f ? block.transform.position : binding.Owner.transform.position;
                leaf.SetPositionAndRotation(pivot + rotation * (leaf.position - pivot) + worldOffset * binding.Gain,
                    rotation * leaf.rotation);
                binding.AppliedPosition = leaf.localPosition; binding.AppliedRotation = leaf.localRotation; binding.Applied = true;
            }
        }

        /// <summary>Called only on graph changes, never allocates during an unchanged running frame.</summary>
        public void RebuildBindings()
        {
            RemoveOffsets(); driven.Clear();
            var seen = new HashSet<Transform>();
            void Add(Transform leaf, PartInstance owner, float gain)
            { if (leaf != null && seen.Add(leaf)) driven.Add(new DrivenVisual { Leaf = leaf, Owner = owner, Gain = gain }); }
            if (assembly == null)
            {
                for (int i = 0; i < visualLeaves.Length; i++) Add(visualLeaves[i], owners[i], 1f);
                return;
            }
            PartInstance[] parts = assembly.AllRuntimeParts;
            var gains = new Dictionary<PartInstance, float>();
            foreach (PartInstance part in parts)
            {
                if (part == null || !part.IsInstalled) continue;
                float gain = ConnectedToBlock(part, parts) ? 1f : 0f;
                for (int i = 0; i < transmittedOwners.Length; i++)
                    if (part == transmittedOwners[i]) gain = transmissionGains[i];
                if (gain <= 0f) continue;
                gains.Add(part, gain);
                var belt = part.GetComponent<AssemblyAlternatorBeltPresentation>();
                Transform rig = belt != null && belt.InstalledRig != null ? belt.InstalledRig.transform : null;
                // Skin vertices follow bones, not the renderer Transform. Move
                // the entire presentation-only rig, never its physical wrapper.
                if (rig != null && IsSafePresentationTree(rig)) Add(rig, part, gain);
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                    if (renderer.GetComponentInParent<PartInstance>(true) == part &&
                        renderer.GetComponentInParent<AssemblyFastenerInteractionTarget>(true) == null &&
                        (rig == null || !renderer.transform.IsChildOf(rig)) && IsSafeVisualLeaf(renderer.transform))
                        Add(renderer.transform, part, gain);
            }
            foreach (AssemblyFastenerInteractionTarget target in assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true))
            {
                if (!assembly.Graph.TryGetMount(target.MountId, out MountPointRuntime mount) ||
                    mount.InstalledPart == null || !gains.TryGetValue(mount.InstalledPart, out float gain)) continue;
                foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                    if (IsSafeVisualLeaf(renderer.transform)) Add(renderer.transform, mount.InstalledPart, gain);
            }
            observedMutation = assembly.GraphMutationCount;
        }

        private bool ConnectedToBlock(PartInstance part, PartInstance[] parts)
        {
            for (int depth = 0; part != null && part.IsInstalled && depth <= parts.Length; depth++)
            {
                if (part == block) return true;
                if (!assembly.Graph.TryGetMount(part.RuntimeState.InstalledMountId, out MountPointRuntime mount)) return false;
                string ownerId = mount.Definition.OwnerPartDefinitionId;
                part = null;
                foreach (PartInstance candidate in parts)
                    if (candidate != null && candidate.IsInstalled && candidate.Definition.DefinitionId == ownerId)
                    { part = candidate; break; }
            }
            return false;
        }

        private static bool IsSafePresentationTree(Transform root) =>
            root.GetComponentsInChildren<Collider>(true).Length == 0 &&
            root.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
            root.GetComponentsInChildren<PartInstance>(true).Length == 0;

        private void RemoveOffsets()
        {
            for (int i = 0; i < driven.Count; i++)
            {
                DrivenVisual binding = driven[i]; Transform leaf = binding.Leaf;
                // A tuning/presentation owner may replace its pose after us.
                // Never subtract an old offset from that authoritative new pose.
                if (binding.Applied && leaf != null)
                {
                    if ((leaf.localPosition - binding.AppliedPosition).sqrMagnitude < 1e-12f) leaf.localPosition = binding.BasePosition;
                    if (Mathf.Abs(Quaternion.Dot(leaf.localRotation, binding.AppliedRotation)) > .9999999f) leaf.localRotation = binding.BaseRotation;
                }
                binding.Applied = false;
            }
        }
        public void ResetPresentation() { RemoveOffsets(); phase = 0f; observedMutation = -1; }
        private void OnDisable() => ResetPresentation();

        private sealed class DrivenVisual
        {
            public Transform Leaf; public PartInstance Owner; public float Gain;
            public Vector3 BasePosition, AppliedPosition;
            public Quaternion BaseRotation, AppliedRotation;
            public bool Applied;
        }
    }
}
