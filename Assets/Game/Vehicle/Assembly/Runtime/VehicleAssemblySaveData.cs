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
    public sealed class VehicleAssemblySaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public PartSaveDto[] parts = Array.Empty<PartSaveDto>();
        public MountSaveDto[] mounts = Array.Empty<MountSaveDto>();
        public FastenerSaveDto[] fasteners = Array.Empty<FastenerSaveDto>();

        public bool HasSupportedSchema => schemaVersion == CurrentSchemaVersion;
    }
}
