using System;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Builds the bounded M06 fully-installed assembly fixture through the public
    /// assembly save/restore boundary. This is prototype setup, not save storage.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class PrototypeAssemblyStateInitializer : MonoBehaviour
    {
        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private bool initializeOnAwake = true;

        public VehicleAssemblyController AssemblyController => assemblyController;

        public bool IsInitialized { get; private set; }

        public string LastFailure { get; private set; } = string.Empty;

        public void Configure(VehicleAssemblyController controller, bool initializeAutomatically = true)
        {
            assemblyController = controller;
            initializeOnAwake = initializeAutomatically;
        }

        private void Awake()
        {
            if (!initializeOnAwake)
            {
                return;
            }

            if (!TryInitialize(out string failure))
            {
                Debug.LogError("M06 prototype assembly initialization failed: " + failure, this);
            }
        }

        public bool TryInitialize(out string failure)
        {
            IsInitialized = false;
            LastFailure = string.Empty;
            failure = string.Empty;

            if (assemblyController == null)
            {
                return Fail("VehicleAssemblyController is not assigned.", out failure);
            }

            assemblyController.Initialize();
            VehicleAssemblySaveData data = assemblyController.CaptureSaveData();
            AssemblyGraph graph = assemblyController.Graph;
            MountPointRuntime[] runtimeMounts = graph.Mounts;
            var claimedMounts = new bool[runtimeMounts.Length];

            for (int mountIndex = 0; mountIndex < data.mounts.Length; mountIndex++)
            {
                MountSaveDto mount = data.mounts[mountIndex];
                if (mount == null)
                {
                    return Fail("Assembly save data contains a null mount DTO.", out failure);
                }

                mount.installedPartStableEntityId = string.Empty;
            }

            for (int partIndex = 0; partIndex < data.parts.Length; partIndex++)
            {
                PartSaveDto partDto = data.parts[partIndex];
                PartInstance part = partIndex < assemblyController.Parts.Length
                    ? assemblyController.Parts[partIndex]
                    : null;
                if (partDto == null || part == null || part.Definition == null)
                {
                    return Fail("Assembly fixture contains a missing part or definition.", out failure);
                }

                if (part.IsAssemblyRoot)
                {
                    partDto.lifecycleState = PartLifecycleState.AssemblyRoot;
                    partDto.installedMountId = string.Empty;
                    continue;
                }

                int mountIndex = FindFixtureMount(part, partDto.installedMountId, runtimeMounts, claimedMounts);
                if (mountIndex < 0)
                {
                    return Fail(
                        "No unique compatible mount exists for part definition '" +
                        part.Definition.DefinitionId + "'.",
                        out failure);
                }

                claimedMounts[mountIndex] = true;
                MountPointRuntime runtimeMount = runtimeMounts[mountIndex];
                partDto.lifecycleState = PartLifecycleState.Installed;
                partDto.installedMountId = runtimeMount.MountId;

                int mountDtoIndex = FindMountDto(data.mounts, runtimeMount.MountId);
                if (mountDtoIndex < 0)
                {
                    return Fail(
                        "Save DTO is missing mount '" + runtimeMount.MountId + "'.",
                        out failure);
                }

                data.mounts[mountDtoIndex].installedPartStableEntityId = partDto.stableEntityId;
            }

            for (int fastenerIndex = 0; fastenerIndex < data.fasteners.Length; fastenerIndex++)
            {
                FastenerSaveDto fastenerDto = data.fasteners[fastenerIndex];
                if (fastenerDto == null ||
                    !graph.TryGetMount(fastenerDto.mountId, out MountPointRuntime mount) ||
                    !mount.TryGetFastener(fastenerDto.fastenerDefinitionId, out FastenerInstance fastener) ||
                    fastener.Definition == null)
                {
                    return Fail("Assembly fixture contains an unresolved fastener DTO.", out failure);
                }

                int occupiedMountDtoIndex = FindMountDto(data.mounts, mount.MountId);
                bool occupied = occupiedMountDtoIndex >= 0 &&
                                !string.IsNullOrEmpty(
                                    data.mounts[occupiedMountDtoIndex].installedPartStableEntityId);
                fastenerDto.inserted = occupied;
                fastenerDto.seated = occupied;
                fastenerDto.stage = occupied ? fastener.Definition.MaximumStage : 0;
            }

            // Schema 2+ captures the sticky latch independently from bolt stages.
            // This synthetic fixture has just fully tightened occupied mounts;
            // retaining the captured loose latches would create an invalid DTO.
            // Never apply this reconstruction to a user's save or live graph.
            for (int groupIndex = 0; groupIndex < data.fastenerGroups.Length; groupIndex++)
            {
                FastenerGroupSaveDto groupDto = data.fastenerGroups[groupIndex];
                if (groupDto == null ||
                    !graph.TryGetMount(groupDto.mountId, out MountPointRuntime mount))
                {
                    return Fail("Assembly fixture contains an unresolved fastener group DTO.", out failure);
                }

                int mountDtoIndex = FindMountDto(data.mounts, mount.MountId);
                bool occupied = mountDtoIndex >= 0 && !string.IsNullOrEmpty(
                    data.mounts[mountDtoIndex].installedPartStableEntityId);
                FastenerGroupDefinition definition = mount.FastenerGroup.Definition;
                groupDto.isBolted = occupied && definition.HasFasteners &&
                    definition.AggregateMaximumTightness >= definition.BoltedOnThreshold;
            }

            AssemblyOperationResult result = assemblyController.RestoreSaveData(data);
            if (!result.Succeeded)
            {
                return Fail(
                    result.FailureReason + ": " + result.Message,
                    out failure);
            }

            IsInitialized = true;
            return true;
        }

        private static int FindFixtureMount(
            PartInstance part,
            string preferredMountId,
            MountPointRuntime[] mounts,
            bool[] claimedMounts)
        {
            if (!string.IsNullOrEmpty(preferredMountId))
            {
                for (int index = 0; index < mounts.Length; index++)
                {
                    MountPointRuntime mount = mounts[index];
                    if (!claimedMounts[index] && mount != null &&
                        string.Equals(mount.MountId, preferredMountId, StringComparison.Ordinal) &&
                        part.Definition.IsCompatibleWith(mount.Definition))
                    {
                        return index;
                    }
                }
            }

            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointRuntime mount = mounts[index];
                if (!claimedMounts[index] && mount != null &&
                    part.Definition.IsCompatibleWith(mount.Definition))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindMountDto(MountSaveDto[] mounts, string mountId)
        {
            for (int index = 0; index < mounts.Length; index++)
            {
                MountSaveDto mount = mounts[index];
                if (mount != null && string.Equals(mount.mountId, mountId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool Fail(string message, out string failure)
        {
            LastFailure = message ?? string.Empty;
            failure = LastFailure;
            return false;
        }
    }
}
