using System;
using System.Collections.Generic;
using MSC.Core.Identity;

namespace MSC.Vehicle.Assembly
{
    public enum VehicleAssemblyValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public readonly struct VehicleAssemblyValidationIssue
    {
        public VehicleAssemblyValidationIssue(
            VehicleAssemblyValidationSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public VehicleAssemblyValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }
    }

    public static class VehicleAssemblyValidator
    {
        public static IReadOnlyList<VehicleAssemblyValidationIssue> Validate(
            PartInstance[] parts,
            MountPointAuthoring[] mounts,
            AssemblyDependency[] dependencies,
            ToolDefinition[] tools)
        {
            parts = parts ?? Array.Empty<PartInstance>();
            mounts = mounts ?? Array.Empty<MountPointAuthoring>();
            dependencies = dependencies ?? Array.Empty<AssemblyDependency>();
            tools = tools ?? Array.Empty<ToolDefinition>();

            var issues = new List<VehicleAssemblyValidationIssue>();
            var definitionsById = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parts.Length; i++)
            {
                PartInstance part = parts[i];
                if (part == null || part.Definition == null)
                {
                    AddError(issues, "PART-NULL", "PartInstance или PartDefinition отсутствует.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(part.Definition.DefinitionId))
                {
                    AddError(issues, "PART-ID", "PartDefinition не имеет ID: " + part.name);
                }
                else if (definitionsById.TryGetValue(
                    part.Definition.DefinitionId,
                    out PartDefinition registeredDefinition) && registeredDefinition != part.Definition)
                {
                    AddError(issues, "PART-DUPLICATE", "Повтор PartDefinition ID: " + part.Definition.DefinitionId);
                }
                else
                {
                    definitionsById[part.Definition.DefinitionId] = part.Definition;
                    definitionIds.Add(part.Definition.DefinitionId);
                }

                if (part.Definition.VisualPrefab == null)
                {
                    AddError(issues, "PART-PREFAB", "Не задан visual prefab: " + part.Definition.DefinitionId);
                }

                if (!part.StableId.IsValid)
                {
                    AddError(issues, "PART-STABLE-ID", "Некорректный stable ID: " + part.name);
                }
                else if (!stableIds.Add(part.StableId.Value))
                {
                    AddError(issues, "PART-STABLE-DUPLICATE", "Повтор stable ID: " + part.StableId.Value);
                }
            }

            var mountIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < mounts.Length; i++)
            {
                MountPointAuthoring mount = mounts[i];
                if (mount == null || mount.Definition == null)
                {
                    AddError(issues, "MOUNT-NULL", "MountPointAuthoring или definition отсутствует.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(mount.MountId) || !mountIds.Add(mount.MountId))
                {
                    AddError(issues, "MOUNT-ID", "Пустой или повторяющийся mount ID: " + mount.MountId);
                }

                if (mount.Pose == null)
                {
                    AddError(issues, "MOUNT-POSE", "Не задан mount pose: " + mount.MountId);
                }

                for (int acceptedIndex = 0;
                     acceptedIndex < mount.Definition.AcceptedPartDefinitionIds.Length;
                     acceptedIndex++)
                {
                    string acceptedId = mount.Definition.AcceptedPartDefinitionIds[acceptedIndex];
                    if (!definitionIds.Contains(acceptedId))
                    {
                        AddError(issues, "MOUNT-PART", $"{mount.MountId} ссылается на неизвестную деталь {acceptedId}.");
                    }
                }

                var fastenerIds = new HashSet<string>(StringComparer.Ordinal);
                FastenerDefinition[] fasteners = mount.Definition.Fasteners;
                int fastenerStageSum = 0;
                for (int fastenerIndex = 0; fastenerIndex < fasteners.Length; fastenerIndex++)
                {
                    FastenerDefinition fastener = fasteners[fastenerIndex];
                    if (fastener == null || string.IsNullOrWhiteSpace(fastener.DefinitionId))
                    {
                        AddError(issues, "FASTENER-ID", "Mount содержит пустой FastenerDefinition: " + mount.MountId);
                        continue;
                    }

                    if (!fastenerIds.Add(fastener.DefinitionId))
                    {
                        AddError(issues, "FASTENER-DUPLICATE", $"{mount.MountId}: повтор {fastener.DefinitionId}.");
                    }

                    fastenerStageSum += fastener.MaximumStage;

                    bool compatibleToolExists = false;
                    for (int toolIndex = 0; toolIndex < tools.Length; toolIndex++)
                    {
                        if (fastener.ToolRule != null && fastener.ToolRule.Matches(tools[toolIndex]))
                        {
                            compatibleToolExists = true;
                            break;
                        }
                    }

                    if (!compatibleToolExists)
                    {
                        AddError(issues, "FASTENER-TOOL", $"Нет совместимого инструмента для {fastener.DefinitionId}.");
                    }
                }

                ValidateFastenerGroup(
                    mount,
                    fastenerIds,
                    fastenerStageSum,
                    issues);
            }

            for (int index = 0; index < mounts.Length; index++)
            {
                MountPointAuthoring mount = mounts[index];
                if (mount?.Definition == null)
                {
                    continue;
                }

                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.RequiredOccupiedMountIds,
                    mountIds,
                    "MOUNT-REQUIRES-INSTALLED",
                    issues);
                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.RequiredAnyOccupiedMountIds,
                    mountIds,
                    "MOUNT-REQUIRES-ANY-INSTALLED",
                    issues);
                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.RequiredBoltedMountIds,
                    mountIds,
                    "MOUNT-REQUIRES-BOLTED",
                    issues);
                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.RequiredAnyBoltedMountIds,
                    mountIds,
                    "MOUNT-REQUIRES-ANY-BOLTED",
                    issues);
                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.BlockedWhileOccupiedMountIds,
                    mountIds,
                    "MOUNT-BLOCKED",
                    issues);
                ValidateMountReferences(
                    mount.MountId,
                    mount.Definition.RemovalBlockedWhileOccupiedMountIds,
                    mountIds,
                    "MOUNT-REMOVAL-BLOCKED",
                    issues);
            }

            ValidateDependencies(definitionIds, dependencies, issues);
            return issues;
        }

        private static void ValidateDependencies(
            HashSet<string> definitionIds,
            AssemblyDependency[] dependencies,
            List<VehicleAssemblyValidationIssue> issues)
        {
            var installEdges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < dependencies.Length; i++)
            {
                AssemblyDependency dependency = dependencies[i];
                if (dependency == null ||
                    !definitionIds.Contains(dependency.DependentPartDefinitionId) ||
                    !definitionIds.Contains(dependency.RelatedPartDefinitionId))
                {
                    AddError(issues, "DEPENDENCY-ID", "Зависимость ссылается на неизвестную деталь.");
                    continue;
                }

                if (dependency.Kind !=
                        AssemblyDependencyKind.InstallRequiresInstalled &&
                    dependency.Kind !=
                        AssemblyDependencyKind.InstallRequiresBolted)
                {
                    continue;
                }

                if (!installEdges.TryGetValue(
                        dependency.DependentPartDefinitionId,
                        out List<string> targets))
                {
                    targets = new List<string>();
                    installEdges.Add(dependency.DependentPartDefinitionId, targets);
                }

                targets.Add(dependency.RelatedPartDefinitionId);
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string partId in definitionIds)
            {
                if (HasCycle(partId, installEdges, visiting, visited))
                {
                    AddError(issues, "DEPENDENCY-CYCLE", "Цикл install-зависимостей включает: " + partId);
                    break;
                }
            }
        }

        private static void ValidateFastenerGroup(
            MountPointAuthoring mount,
            HashSet<string> fastenerIds,
            int fastenerStageSum,
            List<VehicleAssemblyValidationIssue> issues)
        {
            FastenerGroupDefinition group = mount.Definition.FastenerGroup;
            if (fastenerIds.Count == 0)
            {
                if (group != null && group.HasFasteners)
                {
                    AddError(
                        issues,
                        "FASTENER-GROUP-EMPTY-MOUNT",
                        mount.MountId +
                        " имеет группу крепежа без FastenerDefinition.");
                }

                return;
            }

            if (group == null)
            {
                AddError(
                    issues,
                    "FASTENER-GROUP-MISSING",
                    mount.MountId +
                    " имеет крепёж, но не имеет BoltCheck-группы.");
                return;
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            string[] configuredIds = group.FastenerDefinitionIds;
            for (int index = 0; index < configuredIds.Length; index++)
            {
                string id = configuredIds[index];
                if (string.IsNullOrWhiteSpace(id) || !groupIds.Add(id) ||
                    !fastenerIds.Contains(id))
                {
                    AddError(
                        issues,
                        "FASTENER-GROUP-ID",
                        mount.MountId +
                        " содержит пустой, повторный или чужой group fastener: " +
                        id);
                }
            }

            if (groupIds.Count == 0 ||
                group.AggregateMaximumTightness <= 0 ||
                group.AggregateMaximumTightness > fastenerStageSum ||
                group.BoltedOffThreshold > group.BoltedOnThreshold ||
                group.BoltedOnThreshold >
                    group.AggregateMaximumTightness)
            {
                AddError(
                    issues,
                    "FASTENER-GROUP-THRESHOLD",
                    mount.MountId +
                    " имеет некорректные aggregate/max/on/off значения.");
            }

            if (group.SpeedRetentionPolicy !=
                    FastenerSpeedRetentionPolicy.None &&
                (group.BreakAction == FastenerBreakAction.None ||
                 group.PartialCheckSpeedKph < group.LooseBreakSpeedKph ||
                 group.ChanceDivisor <= 0f))
            {
                AddError(
                    issues,
                    "FASTENER-GROUP-BREAK",
                    mount.MountId +
                    " имеет неполную donor BREAK policy.");
            }
        }

        private static void ValidateMountReferences(
            string mountId,
            string[] references,
            HashSet<string> mountIds,
            string code,
            List<VehicleAssemblyValidationIssue> issues)
        {
            references = references ?? Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < references.Length; index++)
            {
                string reference = references[index];
                if (string.IsNullOrWhiteSpace(reference) ||
                    !seen.Add(reference) ||
                    !mountIds.Contains(reference) ||
                    string.Equals(reference, mountId, StringComparison.Ordinal))
                {
                    AddError(
                        issues,
                        code,
                        mountId +
                        " содержит неизвестную, повторную или self-ссылку: " +
                        reference);
                }
            }
        }

        private static bool HasCycle(
            string node,
            Dictionary<string, List<string>> edges,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(node))
            {
                return false;
            }

            if (!visiting.Add(node))
            {
                return true;
            }

            if (edges.TryGetValue(node, out List<string> targets))
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (HasCycle(targets[i], edges, visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(node);
            visited.Add(node);
            return false;
        }

        private static void AddError(
            List<VehicleAssemblyValidationIssue> issues,
            string code,
            string message)
        {
            issues.Add(new VehicleAssemblyValidationIssue(
                VehicleAssemblyValidationSeverity.Error,
                code,
                message));
        }
    }
}
