using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [Serializable]
    public sealed class PartSaveDto
    {
        public string stableEntityId = string.Empty;
        public string partDefinitionId = string.Empty;
        public PartLifecycleState lifecycleState;
        public string installedMountId = string.Empty;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        // Optional, independently versioned extension, keyed by this part's
        // existing stable identity. Pre-toe saves omit it.
        // JsonUtility may materialize a null inline DTO as a default object,
        // so the explicit presence bit, not nullness, owns optionality.
        public bool hasSteeringAlignment;
        public AssemblySteeringAlignmentSaveDto steeringAlignment;
        // Optional timing setting on the existing camshaft-gear identity.
        // Pre-timing saves omit it; the installed compatibility pose is zero.
        public bool hasCamshaftTiming;
        public AssemblyCamshaftTimingSaveDto camshaftTiming;
        // Optional stock-engine setting. Presence remains explicit for
        // JsonUtility and pre-adjustment native saves; graph identity is unchanged.
        public bool hasEngineAdjustment;
        public AssemblyEngineAdjustmentSaveDto engineAdjustment;
        // One staged engine-mount turn may exist while the engine is still a
        // loose rigidbody. Installed engine bolts remain in the ordinary graph.
        public bool hasEngineDocking;
        public AssemblyEngineDockingSaveDto engineDocking;
        public bool hasMechanicalCondition;
        public AssemblyMechanicalConditionSaveDto mechanicalCondition;
        public bool hasValveAdjustment;
        public AssemblyValveAdjustmentSaveDto valveAdjustment;
        public bool hasServiceCaps;
        public AssemblyServiceCapSaveDto serviceCaps;
    }

    [Serializable]
    public sealed class MountSaveDto
    {
        public string mountId = string.Empty;
        public string installedPartStableEntityId = string.Empty;
    }

    [Serializable]
    public sealed class FastenerSaveDto
    {
        public string mountId = string.Empty;
        public string fastenerDefinitionId = string.Empty;
        public bool inserted;
        public bool seated;
        public int stage;
    }

    [Serializable]
    public sealed class FastenerGroupSaveDto
    {
        public string mountId = string.Empty;
        public bool isBolted;
    }

    [Serializable]
    public sealed class DynamicAssemblyPartSaveDto
    {
        public string itemDefinitionId = string.Empty;
        public PartSaveDto part;
        // Loose purchased parts previously had world-entity physics ownership.
        // Retain that state when their ownership moves into this aggregate.
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public bool sleeping;
    }

    [Serializable]
    public sealed class VehicleAssemblySaveData
    {
        public const int CurrentSchemaVersion = 3;
        public const int FastenerGroupSchemaVersion = 2;
        public const int LegacySchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public PartSaveDto[] parts = Array.Empty<PartSaveDto>();
        public DynamicAssemblyPartSaveDto[] dynamicParts =
            Array.Empty<DynamicAssemblyPartSaveDto>();
        public MountSaveDto[] mounts = Array.Empty<MountSaveDto>();
        public FastenerSaveDto[] fasteners = Array.Empty<FastenerSaveDto>();
        public FastenerGroupSaveDto[] fastenerGroups =
            Array.Empty<FastenerGroupSaveDto>();

        public bool HasSupportedSchema =>
            schemaVersion == CurrentSchemaVersion ||
            schemaVersion == FastenerGroupSchemaVersion ||
            schemaVersion == LegacySchemaVersion;
    }
}
