using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    public enum AssemblyDependencyKind
    {
        InstallRequiresInstalled = 0,
        RemovalBlockedWhileInstalled = 1,
        InstallRequiresBolted = 2
    }

    [Serializable]
    public sealed class AssemblyDependency
    {
        [UnityEngine.SerializeField]
        private string dependentPartDefinitionId = string.Empty;

        [UnityEngine.SerializeField]
        private string relatedPartDefinitionId = string.Empty;

        [UnityEngine.SerializeField]
        private AssemblyDependencyKind kind;

        public string DependentPartDefinitionId => dependentPartDefinitionId;

        public string RelatedPartDefinitionId => relatedPartDefinitionId;

        public AssemblyDependencyKind Kind => kind;

        public static AssemblyDependency Create(
            string dependentPartId,
            string relatedPartId,
            AssemblyDependencyKind dependencyKind)
        {
            return new AssemblyDependency
            {
                dependentPartDefinitionId = dependentPartId ?? string.Empty,
                relatedPartDefinitionId = relatedPartId ?? string.Empty,
                kind = dependencyKind
            };
        }
    }

    public sealed class MountPointRuntime
    {
        private readonly FastenerInstance[] fasteners;

        public MountPointRuntime(MountPointAuthoring authoring)
        {
            Authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
            FastenerDefinition[] definitions = authoring.Definition != null
                ? authoring.Definition.Fasteners
                : Array.Empty<FastenerDefinition>();
            fasteners = new FastenerInstance[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                fasteners[i] = new FastenerInstance(definitions[i]);
            }

            FastenerGroup = new FastenerGroupState(
                authoring.Definition?.FastenerGroup,
                fasteners);
        }

        public MountPointAuthoring Authoring { get; }

        public MountPointDefinition Definition => Authoring.Definition;

        public string MountId => Authoring.MountId;

        public PartInstance InstalledPart { get; private set; }

        public FastenerInstance[] Fasteners => fasteners;

        public FastenerGroupState FastenerGroup { get; }

        public bool IsOccupied => InstalledPart != null;

        public bool TryGetFastener(string fastenerDefinitionId, out FastenerInstance fastener)
        {
            for (int i = 0; i < fasteners.Length; i++)
            {
                FastenerInstance candidate = fasteners[i];
                if (candidate.Definition != null && string.Equals(
                        candidate.Definition.DefinitionId,
                        fastenerDefinitionId,
                        StringComparison.Ordinal))
                {
                    fastener = candidate;
                    return true;
                }
            }

            fastener = null;
            return false;
        }

        internal bool TryOccupy(PartInstance part)
        {
            if (part == null || IsOccupied)
            {
                return false;
            }

            InstalledPart = part;
            for (int i = 0; i < fasteners.Length; i++)
            {
                fasteners[i].ResetForInstalledPart();
            }

            FastenerGroup.Reset();
            FastenerGroup.Reevaluate(mountOccupied: true);

            return true;
        }

        internal PartInstance Release()
        {
            PartInstance released = InstalledPart;
            InstalledPart = null;
            for (int i = 0; i < fasteners.Length; i++)
            {
                fasteners[i].ResetForEmptyMount();
            }

            FastenerGroup.Reset();

            return released;
        }

        internal void Reset()
        {
            Release();
        }

        internal void ReevaluateFastenerGroup()
        {
            FastenerGroup.Reevaluate(IsOccupied);
        }

        internal bool TryRestoreFastenerGroupLatch(bool isBolted)
        {
            return FastenerGroup.TryRestoreLatch(isBolted, IsOccupied);
        }
    }

    public sealed class AssemblyGraph
    {
        private readonly PartInstance[] parts;
        private readonly MountPointRuntime[] mounts;
        private readonly AssemblyDependency[] dependencies;

        public AssemblyGraph(
            PartInstance[] registeredParts,
            MountPointRuntime[] registeredMounts,
            AssemblyDependency[] registeredDependencies)
        {
            parts = registeredParts ?? Array.Empty<PartInstance>();
            mounts = registeredMounts ?? Array.Empty<MountPointRuntime>();
            dependencies = registeredDependencies ?? Array.Empty<AssemblyDependency>();
        }

        public PartInstance[] Parts => parts;

        public MountPointRuntime[] Mounts => mounts;

        public AssemblyDependency[] Dependencies => dependencies;

        public bool IsPartDefinitionInstalled(string partDefinitionId)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                if (part != null && part.Definition != null && part.IsInstalled && string.Equals(
                        part.Definition.DefinitionId,
                        partDefinitionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPartDefinitionBolted(string partDefinitionId)
        {
            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointRuntime mount = mounts[index];
                if (mount?.InstalledPart?.Definition != null &&
                    string.Equals(
                        mount.InstalledPart.Definition.DefinitionId,
                        partDefinitionId,
                        StringComparison.Ordinal))
                {
                    return mount.FastenerGroup.IsBolted;
                }
            }

            return false;
        }

        public bool AreInstallPrerequisitesMet(PartDefinition part, out string missingPartId)
        {
            if (part == null)
            {
                missingPartId = string.Empty;
                return false;
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                AssemblyDependency dependency = dependencies[i];
                if (dependency == null ||
                    dependency.Kind != AssemblyDependencyKind.InstallRequiresInstalled &&
                    dependency.Kind != AssemblyDependencyKind.InstallRequiresBolted ||
                    !string.Equals(
                        dependency.DependentPartDefinitionId,
                        part.DefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool satisfied = dependency.Kind ==
                        AssemblyDependencyKind.InstallRequiresBolted
                    ? IsPartDefinitionBolted(dependency.RelatedPartDefinitionId)
                    : IsPartDefinitionInstalled(dependency.RelatedPartDefinitionId);
                if (!satisfied)
                {
                    missingPartId = dependency.RelatedPartDefinitionId;
                    return false;
                }
            }

            missingPartId = string.Empty;
            return true;
        }

        public bool AreMountInstallPrerequisitesMet(
            MountPointRuntime target,
            out string blockingMountId)
        {
            blockingMountId = string.Empty;
            if (target?.Definition == null)
            {
                return false;
            }

            string[] required = target.Definition.RequiredOccupiedMountIds;
            for (int index = 0; index < required.Length; index++)
            {
                if (!TryGetMount(required[index], out MountPointRuntime mount) ||
                    !mount.IsOccupied)
                {
                    blockingMountId = required[index];
                    return false;
                }
            }

            string[] requiredAny = target.Definition.RequiredAnyOccupiedMountIds;
            if (requiredAny.Length > 0)
            {
                bool anyOccupied = false;
                for (int index = 0; index < requiredAny.Length; index++)
                {
                    if (TryGetMount(requiredAny[index], out MountPointRuntime mount) &&
                        mount.IsOccupied)
                    {
                        anyOccupied = true;
                        break;
                    }
                }

                if (!anyOccupied)
                {
                    blockingMountId = requiredAny[0];
                    return false;
                }
            }

            string[] blocked = target.Definition.BlockedWhileOccupiedMountIds;
            for (int index = 0; index < blocked.Length; index++)
            {
                if (TryGetMount(blocked[index], out MountPointRuntime mount) &&
                    mount.IsOccupied)
                {
                    blockingMountId = blocked[index];
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Mount-level Bolted predicates describe structural retention, not
        /// whether the player may place the next part. InstallRequiresBolted
        /// dependencies express the separate donor installation checks (e.g.
        /// front spindle/wishbone); they must not create a retention cascade.
        /// </summary>
        public bool AreMountStructuralRetentionPrerequisitesMet(
            MountPointRuntime target,
            out string blockingMountId)
        {
            blockingMountId = string.Empty;
            if (target?.Definition == null)
            {
                return false;
            }

            string[] requiredBolted = target.Definition.RequiredBoltedMountIds;
            for (int index = 0; index < requiredBolted.Length; index++)
            {
                if (!TryGetMount(
                        requiredBolted[index],
                        out MountPointRuntime mount) ||
                    !mount.IsOccupied ||
                    !mount.FastenerGroup.IsBolted)
                {
                    blockingMountId = requiredBolted[index];
                    return false;
                }
            }

            string[] requiredAnyBolted =
                target.Definition.RequiredAnyBoltedMountIds;
            if (requiredAnyBolted.Length == 0)
            {
                return true;
            }

            string firstOccupiedMountId = string.Empty;
            for (int index = 0; index < requiredAnyBolted.Length; index++)
            {
                if (!TryGetMount(
                        requiredAnyBolted[index],
                        out MountPointRuntime mount) ||
                    !mount.IsOccupied)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(firstOccupiedMountId))
                {
                    firstOccupiedMountId = mount.MountId;
                }

                if (mount.FastenerGroup.IsBolted)
                {
                    return true;
                }
            }

            blockingMountId = !string.IsNullOrEmpty(firstOccupiedMountId)
                ? firstOccupiedMountId
                : requiredAnyBolted[0];
            return false;
        }

        public bool IsMountDependentOn(
            MountPointRuntime candidate,
            MountPointRuntime prerequisite)
        {
            if (candidate?.Definition == null || prerequisite == null ||
                candidate == prerequisite)
            {
                return false;
            }

            PartDefinition prerequisitePart =
                prerequisite.InstalledPart?.Definition;
            if (prerequisitePart != null && string.Equals(
                    candidate.Definition.OwnerPartDefinitionId,
                    prerequisitePart.DefinitionId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            string mountId = prerequisite.MountId;
            return Contains(
                       candidate.Definition.RequiredOccupiedMountIds,
                       mountId) ||
                   Contains(
                       candidate.Definition.RequiredAnyOccupiedMountIds,
                       mountId) ||
                   Contains(
                       candidate.Definition.RequiredBoltedMountIds,
                       mountId) ||
                   Contains(
                       candidate.Definition.RequiredAnyBoltedMountIds,
                       mountId);
        }

        public bool HasInstalledRemovalBlocker(PartDefinition part, out string blockerPartId)
        {
            if (part == null)
            {
                blockerPartId = string.Empty;
                return true;
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                AssemblyDependency dependency = dependencies[i];
                if (dependency == null ||
                    dependency.Kind != AssemblyDependencyKind.RemovalBlockedWhileInstalled ||
                    !string.Equals(
                        dependency.DependentPartDefinitionId,
                        part.DefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsPartDefinitionInstalled(dependency.RelatedPartDefinitionId))
                {
                    blockerPartId = dependency.RelatedPartDefinitionId;
                    return true;
                }
            }

            for (int i = 0; i < mounts.Length; i++)
            {
                MountPointRuntime mount = mounts[i];
                if (mount == null || !mount.IsOccupied ||
                    mount.Definition == null ||
                    !string.Equals(
                        mount.Definition.OwnerPartDefinitionId,
                        part.DefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                blockerPartId = mount.InstalledPart?.Definition?.DefinitionId ??
                                mount.MountId;
                return true;
            }

            MountPointRuntime installedAt = FindMountForPartDefinition(
                part.DefinitionId);
            if (installedAt != null)
            {
                for (int index = 0; index < mounts.Length; index++)
                {
                    MountPointRuntime candidate = mounts[index];
                    if (candidate == null || !candidate.IsOccupied ||
                        candidate.Definition == null || candidate == installedAt)
                    {
                        continue;
                    }

                    if (Contains(
                            candidate.Definition.RequiredOccupiedMountIds,
                            installedAt.MountId) ||
                        Contains(
                            candidate.Definition.RequiredAnyOccupiedMountIds,
                            installedAt.MountId) ||
                        Contains(
                            candidate.Definition.RequiredBoltedMountIds,
                            installedAt.MountId) ||
                        Contains(
                            candidate.Definition.RequiredAnyBoltedMountIds,
                            installedAt.MountId))
                    {
                        blockerPartId = candidate.InstalledPart?.Definition?.DefinitionId ??
                            candidate.MountId;
                        return true;
                    }
                }
            }

            blockerPartId = string.Empty;
            return false;
        }

        public bool HasInstalledRemovalBlocker(
            PartInstance part,
            out string blockerPartId)
        {
            if (part?.Definition == null)
            {
                blockerPartId = string.Empty;
                return true;
            }

            MountPointRuntime installedAt = FindMountForPart(part);
            if (installedAt?.Definition != null)
            {
                string[] blockedMountIds = installedAt.Definition
                    .RemovalBlockedWhileOccupiedMountIds;
                for (int index = 0; index < blockedMountIds.Length; index++)
                {
                    if (!TryGetMount(
                            blockedMountIds[index],
                            out MountPointRuntime blocker) ||
                        !blocker.IsOccupied)
                    {
                        continue;
                    }

                    blockerPartId = blocker.InstalledPart?.Definition
                        ?.DefinitionId ?? blocker.MountId;
                    return true;
                }
            }

            return HasInstalledRemovalBlocker(
                part.Definition,
                out blockerPartId);
        }

        private MountPointRuntime FindMountForPartDefinition(
            string definitionId)
        {
            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointRuntime mount = mounts[index];
                if (mount?.InstalledPart?.Definition != null &&
                    string.Equals(
                        mount.InstalledPart.Definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    return mount;
                }
            }

            return null;
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetMount(string mountId, out MountPointRuntime mount)
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                MountPointRuntime candidate = mounts[i];
                if (candidate != null && string.Equals(
                        candidate.MountId,
                        mountId,
                        StringComparison.Ordinal))
                {
                    mount = candidate;
                    return true;
                }
            }

            mount = null;
            return false;
        }

        public bool TryGetPartByStableId(string stableId, out PartInstance part)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance candidate = parts[i];
                if (candidate != null && candidate.StableId.IsValid && string.Equals(
                        candidate.StableId.Value,
                        stableId,
                        StringComparison.Ordinal))
                {
                    part = candidate;
                    return true;
                }
            }

            part = null;
            return false;
        }

        public MountPointRuntime FindMountForPart(PartInstance part)
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i] != null && mounts[i].InstalledPart == part)
                {
                    return mounts[i];
                }
            }

            return null;
        }

        public float CalculateCompleteness01()
        {
            int installableCount = 0;
            int completeCount = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                if (part == null || part.IsAssemblyRoot)
                {
                    continue;
                }

                installableCount++;
                if (!part.IsInstalled)
                {
                    continue;
                }

                MountPointRuntime mount = FindMountForPart(part);
                if (mount == null)
                {
                    continue;
                }

                if (mount.FastenerGroup.IsFullySafe)
                {
                    completeCount++;
                }
            }

            return installableCount == 0 ? 1f : (float)completeCount / installableCount;
        }
    }
}
