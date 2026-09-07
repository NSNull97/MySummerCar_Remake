using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class AssemblyCamshaftTimingSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public float angleDegrees;

        public bool IsValid => schemaVersion == CurrentSchemaVersion &&
            float.IsFinite(angleDegrees) && angleDegrees >= 0f && angleDegrees <= 360f;
    }

    /// <summary>
    /// Frozen camshaft-gear Screw104426 / BoltCheck112367 / Data106546.
    /// Extra tightening at stage eight advances only the gear mesh, +5 degrees
    /// around donor local X, while the chain is absent. Data.Angle is a logical
    /// saved setting, not the loose part's world transform or camera orientation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyCamshaftTimingState : MonoBehaviour,
        IAssemblyFastenerPostTighteningAction
    {
        public const string PartDefinitionId = "vehicle.satsuma.part.camshaft-gear";
        public const string GearMountId = "mount.satsuma.camshaft.camshaft-gear";
        public const string GearFastenerId = "fastener.satsuma.camshaft-camshaft-gear.boltpm-1";
        public const string ChainMountId = "mount.satsuma.camshaft-gear.timing-chain";
        public const float StepDegrees = 5f;
        public const int MaximumStage = 8;

        [SerializeField] private PartInstance part;
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private Transform gearMesh;
        [SerializeField] private Quaternion meshBaseLocalRotation = Quaternion.identity;

        private bool initialized;
        private float angleDegrees;
        private Quaternion adjustmentRotation = Quaternion.identity;
        private int revision;

        public PartInstance Part => part;
        public VehicleAssemblyController Assembly => assembly;
        public Transform GearMesh => gearMesh;
        public Quaternion MeshBaseLocalRotation => meshBaseLocalRotation;
        public string PostTighteningPrompt => "ВЫСТАВИТЬ МЕТКУ";
        public int Revision { get { EnsureInitialized(); return revision; } }
        public float AngleDegrees { get { EnsureInitialized(); return angleDegrees; } }

        public void Configure(PartInstance configuredPart,
            VehicleAssemblyController configuredAssembly, Transform configuredGearMesh,
            Quaternion authoredMeshBaseLocalRotation)
        {
            if (configuredPart == null || configuredPart.Definition == null ||
                configuredPart.Definition.DefinitionId != PartDefinitionId)
                throw new ArgumentException("Camshaft timing must belong to the camshaft gear.", nameof(configuredPart));
            if (configuredAssembly == null)
                throw new ArgumentNullException(nameof(configuredAssembly));
            if (configuredGearMesh == null || configuredGearMesh == configuredPart.transform ||
                !configuredGearMesh.IsChildOf(configuredPart.transform))
                throw new ArgumentException("Bind the explicit child gear mesh, never the part/root pose.", nameof(configuredGearMesh));
            part = configuredPart;
            assembly = configuredAssembly;
            gearMesh = configuredGearMesh;
            meshBaseLocalRotation = authoredMeshBaseLocalRotation;
            if (initialized) ApplyPresentation();
        }

        public bool CanApply(AssemblyFastenerInteractionTarget source)
        {
            if (source == null || source.Controller != assembly ||
                source.MountId != GearMountId || source.FastenerDefinitionId != GearFastenerId ||
                part == null || !part.IsInstalled || gearMesh == null || assembly == null ||
                !assembly.Graph.TryGetMount(GearMountId, out MountPointRuntime gear) ||
                gear.InstalledPart != part ||
                !gear.TryGetFastener(GearFastenerId, out FastenerInstance bolt) ||
                !bolt.IsInserted || bolt.Stage != MaximumStage ||
                bolt.Definition.MaximumStage != MaximumStage ||
                !gear.FastenerGroup.IsBolted || gear.Authoring.IsObstructed(part) ||
                !assembly.Graph.TryGetMount(ChainMountId, out MountPointRuntime chain))
                return false;
            return !chain.IsOccupied;
        }

        public bool TryApply(AssemblyFastenerInteractionTarget source)
        {
            if (!CanApply(source)) return false;
            EnsureInitialized();
            adjustmentRotation *= Quaternion.Euler(StepDegrees, 0f, 0f);
            // Match the donor's Rotate -> GetRotation(localEulerAngles.x) ->
            // RoundToInt -> Data.Angle sequence, including its Euler readout.
            // Do not reset the live quaternion from that readout each turn.
            angleDegrees = Mathf.RoundToInt(adjustmentRotation.eulerAngles.x);
            revision++;
            ApplyPresentation();
            return true;
        }

        public AssemblyCamshaftTimingSaveDto CaptureSaveData() =>
            new AssemblyCamshaftTimingSaveDto { angleDegrees = AngleDegrees };

        public void RestoreValidated(AssemblyCamshaftTimingSaveDto data)
        {
            if (data != null && !data.IsValid)
                throw new ArgumentException("Invalid camshaft timing setting.", nameof(data));
            // Same compatibility policy as accepted steering alignment:
            // installed old saves retain their previous zero-angle visual;
            // loose loads follow the donor Rand angle branch even if saved.
            SetAngle(part != null && part.IsInstalled
                ? data?.angleDegrees ?? 0f
                : UnityEngine.Random.Range(0, 71) * StepDegrees);
        }

        private void SetAngle(float value)
        {
            angleDegrees = value;
            adjustmentRotation = Quaternion.Euler(value, 0f, 0f);
            initialized = true;
            revision++;
            ApplyPresentation();
        }

        private void EnsureInitialized()
        {
            if (!initialized) SetAngle(UnityEngine.Random.Range(0, 71) * StepDegrees);
        }

        private void ApplyPresentation()
        {
            if (gearMesh != null)
                gearMesh.localRotation = meshBaseLocalRotation * adjustmentRotation;
        }

        private void Start() => EnsureInitialized();
    }
}
