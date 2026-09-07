using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyEngineAdjustmentSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public SatsumaEngineAdjustmentKind kind;
        public float value;
        public bool IsValid => schemaVersion == CurrentSchemaVersion &&
            SatsumaEngineAdjustmentRules.IsValid(kind, value);
        public bool IsValidFor(SatsumaEngineAdjustmentKind expected) => IsValid && kind == expected;
    }

    [Serializable]
    public sealed class AssemblyEngineAdjustmentPresentation
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 partLocalPosition;
        [SerializeField] private Quaternion partLocalRotation = Quaternion.identity;
        [SerializeField] private bool installedOnly;
        public Transform Target => target;
        public Vector3 PartLocalPosition => partLocalPosition;
        public Quaternion PartLocalRotation => partLocalRotation;
        public bool InstalledOnly => installedOnly;

        public AssemblyEngineAdjustmentPresentation(Transform configuredTarget,
            Vector3 authoredPartLocalPosition, Quaternion authoredPartLocalRotation,
            bool requiresInstalled = false)
        {
            target = configuredTarget;
            partLocalPosition = authoredPartLocalPosition;
            partLocalRotation = authoredPartLocalRotation;
            installedOnly = requiresInstalled;
        }
    }

    /// <summary>
    /// Per-part saved settings from frozen HandRotate/Screw/Data. Imported
    /// presentation is flattened, so explicit part-space bindings reconstruct
    /// the measured pivot without changing physics roots or mount identities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyEngineAdjustmentState : MonoBehaviour
    {
        [SerializeField] private SatsumaEngineAdjustmentKind kind;
        [SerializeField] private PartInstance part;
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private Vector3 localPivot;
        [SerializeField] private Vector3 localAxis = Vector3.right;
        [SerializeField] private float presentationReferenceAngle;
        [SerializeField] private AssemblyEngineAdjustmentPresentation[] presentations =
            Array.Empty<AssemblyEngineAdjustmentPresentation>();

        private bool initialized;
        private float setting;
        private float mixtureScrewVisualAngle;
        private bool wasInstalled;
        private int revision;

        public SatsumaEngineAdjustmentKind Kind => kind;
        public PartInstance Part => part;
        public VehicleAssemblyController Assembly => assembly;
        public AssemblyEngineAdjustmentPresentation[] Presentations => presentations;
        public Vector3 LocalPivot => localPivot;
        public Vector3 LocalAxis => localAxis;
        public float PresentationReferenceAngle => presentationReferenceAngle;
        public float Setting { get { EnsureInitialized(); return setting; } }
        public int Revision => revision;
        public bool BlocksRemoval => kind == SatsumaEngineAdjustmentKind.OilFilter &&
            part != null && part.IsInstalled && Setting > 0f;

        public void Configure(SatsumaEngineAdjustmentKind configuredKind,
            PartInstance configuredPart, VehicleAssemblyController configuredAssembly,
            Vector3 authoredPivot, Vector3 authoredAxis, float authoredReferenceAngle,
            AssemblyEngineAdjustmentPresentation[] authoredPresentations)
        {
            if (configuredPart?.Definition == null || configuredPart.Definition.DefinitionId !=
                SatsumaEngineAdjustmentRules.PartId(configuredKind))
                throw new ArgumentException("Engine adjustment part identity mismatch.", nameof(configuredPart));
            if (configuredAssembly == null) throw new ArgumentNullException(nameof(configuredAssembly));
            if (authoredPresentations == null || authoredPresentations.Length == 0 ||
                authoredAxis.sqrMagnitude < 0.99f)
                throw new ArgumentException("Explicit presentation and pivot axis are required.");
            foreach (AssemblyEngineAdjustmentPresentation binding in authoredPresentations)
                if (binding?.Target == null || binding.Target == configuredPart.transform ||
                    binding.Target == configuredAssembly.transform)
                    throw new ArgumentException("Never use a physical part or assembly root as presentation.");
            kind = configuredKind;
            part = configuredPart;
            assembly = configuredAssembly;
            localPivot = authoredPivot;
            localAxis = authoredAxis.normalized;
            presentationReferenceAngle = authoredReferenceAngle;
            presentations = authoredPresentations;
        }

        public bool IsAvailable
        {
            get
            {
                if (!isActiveAndEnabled || part == null || !part.gameObject.activeInHierarchy ||
                    assembly == null || kind == SatsumaEngineAdjustmentKind.Unbound) return false;
                // Unlike the distributor, the frozen alternator HandRotate
                // also runs on the loose part. No installed clamp exists then.
                if (!part.IsInstalled) return kind == SatsumaEngineAdjustmentKind.Alternator;
                if (!assembly.Graph.TryGetMount(SatsumaEngineAdjustmentRules.MountId(kind), out MountPointRuntime mount) ||
                    mount.InstalledPart != part) return false;
                string clampId = SatsumaEngineAdjustmentRules.ClampFastenerId(kind);
                return clampId.Length == 0 || mount.TryGetFastener(clampId, out FastenerInstance clamp) &&
                    clamp.IsInserted && clamp.Stage < 8;
            }
        }

        public bool CanAdjust(float signedNotches) => IsAvailable &&
            SatsumaEngineAdjustmentRules.TryStep(kind, Setting, signedNotches, out _);

        public bool TryAdjust(float signedNotches)
        {
            if (!IsAvailable || !SatsumaEngineAdjustmentRules.TryStep(kind, Setting, signedNotches, out float next))
                return false;
            setting = next;
            if (kind == SatsumaEngineAdjustmentKind.CarburetorMixture)
                mixtureScrewVisualAngle += signedNotches > 0f ? -3f : 3f;
            revision++;
            ApplyPresentation();
            return true;
        }

        public AssemblyEngineAdjustmentSaveDto CaptureSaveData()
        {
            // A save may run in the same frame as a forced assembly detach.
            RefreshPresentation();
            return new AssemblyEngineAdjustmentSaveDto { kind = kind, value = setting };
        }

        public void RestoreValidated(AssemblyEngineAdjustmentSaveDto data)
        {
            if (data != null && !data.IsValidFor(kind))
                throw new ArgumentException("Invalid or mismatched engine adjustment save.", nameof(data));
            setting = data?.value ?? SatsumaEngineAdjustmentRules.InitialValue(kind);
            mixtureScrewVisualAngle = 0f; // Donor stores mixture, not screw visual turns.
            initialized = true;
            wasInstalled = part != null && part.IsInstalled;
            revision++;
            ApplyPresentation();
        }

        public void RefreshPresentation()
        {
            EnsureInitialized();
            bool installed = part != null && part.IsInstalled;
            // Ordinary filter removal is possible only at zero. Forced
            // detach must not leave an unattached object secretly screwed in.
            if (wasInstalled && !installed && kind == SatsumaEngineAdjustmentKind.OilFilter && setting != 0f)
            { setting = 0f; revision++; }
            wasInstalled = installed;
            ApplyPresentation();
        }

        private void EnsureInitialized()
        {
            if (!initialized) RestoreValidated(null);
        }

        private void ApplyPresentation()
        {
            if (part == null || !initialized) return;
            bool installed = part.IsInstalled;
            float angle = kind == SatsumaEngineAdjustmentKind.CarburetorMixture
                ? mixtureScrewVisualAngle : setting - presentationReferenceAngle;
            Quaternion delta = kind == SatsumaEngineAdjustmentKind.OilFilter
                ? Quaternion.identity : Quaternion.AngleAxis(angle, localAxis);
            Vector3 translation = kind == SatsumaEngineAdjustmentKind.OilFilter && installed
                ? Vector3.back * (setting * 0.0025f) : Vector3.zero;
            foreach (AssemblyEngineAdjustmentPresentation binding in presentations)
            {
                if (binding?.Target == null || binding.InstalledOnly && !installed) continue;
                Vector3 position = localPivot + delta * (binding.PartLocalPosition - localPivot) + translation;
                binding.Target.SetPositionAndRotation(part.transform.TransformPoint(position),
                    part.transform.rotation * delta * binding.PartLocalRotation);
            }
        }

        private void Start() => EnsureInitialized();
        private void LateUpdate() => RefreshPresentation();
    }
}
