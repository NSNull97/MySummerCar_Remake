using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    public enum AssemblyDependencyKind
    {
        InstallRequiresInstalled = 0,
        RemovalBlockedWhileInstalled = 1
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
        }

        public MountPointAuthoring Authoring { get; }

        public MountPointDefinition Definition => Authoring.Definition;

        public string MountId => Authoring.MountId;

        public PartInstance InstalledPart { get; private set; }

        public FastenerInstance[] Fasteners => fasteners;

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

            return released;
        }

        internal void Reset()
        {
            Release();
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
                    dependency.Kind != AssemblyDependencyKind.InstallRequiresInstalled ||
                    !string.Equals(
                        dependency.DependentPartDefinitionId,
                        part.DefinitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsPartDefinitionInstalled(dependency.RelatedPartDefinitionId))
                {
                    missingPartId = dependency.RelatedPartDefinitionId;
                    return false;
                }
            }

            missingPartId = string.Empty;
            return true;
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

            blockerPartId = string.Empty;
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

                bool secured = true;
                for (int fastenerIndex = 0; fastenerIndex < mount.Fasteners.Length; fastenerIndex++)
                {
                    FastenerInstance fastener = mount.Fasteners[fastenerIndex];
                    if (fastener.Definition != null && fastener.Definition.RequiredForRemoval &&
                        fastener.State != FastenerState.Tightened)
                    {
                        secured = false;
                        break;
                    }
                }

                if (secured)
                {
                    completeCount++;
                }
            }

            return installableCount == 0 ? 1f : (float)completeCount / installableCount;
        }
    }
}
