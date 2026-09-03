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
    public sealed class VehicleAssemblySaveData
    {
        public const int CurrentSchemaVersion = 2;
        public const int LegacySchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public PartSaveDto[] parts = Array.Empty<PartSaveDto>();
        public MountSaveDto[] mounts = Array.Empty<MountSaveDto>();
        public FastenerSaveDto[] fasteners = Array.Empty<FastenerSaveDto>();
        public FastenerGroupSaveDto[] fastenerGroups =
            Array.Empty<FastenerGroupSaveDto>();

        public bool HasSupportedSchema =>
            schemaVersion == CurrentSchemaVersion ||
            schemaVersion == LegacySchemaVersion;
    }
}
