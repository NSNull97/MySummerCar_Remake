using System;
using System.Collections.Generic;
using System.Linq;

namespace MSC.Vehicle.Assembly
{
    public sealed partial class VehicleAssemblyController
    {
        // Reviewed Phase 1 additions. Counts are only sanity checks: every
        // source/target identity is checked before using these migration rules.
        private static readonly string[] ConsumableMountIds =
        {
            "mount.satsuma.cylinder-head.spark-plug-1",
            "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3",
            "mount.satsuma.cylinder-head.spark-plug-4",
            "mount.satsuma.engine-block.alternator-belt",
            "mount.satsuma.headlight-left.light-bulb",
            "mount.satsuma.headlight-right.light-bulb",
        };

        private static readonly string[] BodyAdditionSlugs =
            { "exhaust-pipe", "exhaust-muffler", "fuel-tank", "seat-driver", "seat-passenger", "seat-rear" };
        private static readonly int[] BodyAdditionCounts = { 3, 1, 7, 4, 4, 2 };
        private static readonly int[] BodyAdditionOnThresholds = { 6, 2, 12, 7, 7, 6 };
        private static readonly FastenerSize[] BodyAdditionSizes =
        {
            FastenerSize.Millimeter7, FastenerSize.Millimeter7, FastenerSize.Millimeter11,
            FastenerSize.Millimeter9, FastenerSize.Millimeter9, FastenerSize.Millimeter9,
        };
        private static readonly string[] HeadlightAdditionSlugs = { "headlight-left", "headlight-right" };

        private sealed class ReviewedSaveAdditions
        {
            public readonly HashSet<string> MountIds = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> FastenerKeys = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> BodyMountIds = new HashSet<string>(StringComparer.Ordinal);
            public bool HasAny => MountIds.Count != 0 || FastenerKeys.Count != 0;
            public bool IsMissingFastener(string mountId, string fastenerId) => FastenerKeys.Contains(mountId + "/" + fastenerId);
        }

        private bool TryIdentifyReviewedSaveAdditions(VehicleAssemblySaveData data,
            out ReviewedSaveAdditions additions, out AssemblyOperationResult failure)
        {
            additions = new ReviewedSaveAdditions();
            ReviewedSaveAdditions plan = additions;
            failure = default;
            if (parts.Length != 126) return true;
            var targetBodyKeys = new HashSet<string>(StringComparer.Ordinal);
            int authoredBodyFasteners = 0;
            for (int index = 0; index < BodyAdditionSlugs.Length; index++)
            {
                string slug = BodyAdditionSlugs[index];
                if (graph.TryGetMount("mount.satsuma." + slug, out MountPointRuntime mount))
                    authoredBodyFasteners += mount.Fasteners.Length;
            }
            if (authoredBodyFasteners != 0)
            {
                for (int index = 0; index < BodyAdditionSlugs.Length; index++)
                {
                    string slug = BodyAdditionSlugs[index];
                    string mountId = "mount.satsuma." + slug;
                    if (!graph.TryGetMount(mountId, out MountPointRuntime mount) ||
                        mount.Fasteners.Length != BodyAdditionCounts[index])
                    {
                        failure = InvalidSave("The reviewed 21-fastener target revision is incomplete.");
                        return false;
                    }
                    var expectedIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int number = 1; number <= BodyAdditionCounts[index]; number++)
                    {
                        string id = "fastener.satsuma." + slug + ".boltpm-" + number;
                        if (!mount.TryGetFastener(id, out FastenerInstance fastener) ||
                            !MatchesReviewedFastener(fastener.Definition, BodyAdditionSizes[index], "Wrench"))
                        {
                            failure = InvalidSave("The reviewed body fastener identity or maximum stage differs.");
                            return false;
                        }
                        expectedIds.Add(id);
                        targetBodyKeys.Add(mountId + "/" + id);
                    }
                    FastenerGroupDefinition group = mount.FastenerGroup.Definition;
                    if (!expectedIds.SetEquals(group.FastenerDefinitionIds) ||
                        group.AggregateMaximumTightness != 8 * BodyAdditionCounts[index] ||
                        group.BoltedOnThreshold != BodyAdditionOnThresholds[index] || group.BoltedOffThreshold != 0)
                    {
                        failure = InvalidSave("The reviewed body fastener group contract differs.");
                        return false;
                    }
                }
                int present = data.fasteners.Count(dto => dto != null && targetBodyKeys.Contains(dto.mountId + "/" + dto.fastenerDefinitionId));
                if (present != 0 && (present != 21 || !targetBodyKeys.SetEquals(data.fasteners
                        .Where(dto => dto != null && targetBodyKeys.Contains(dto.mountId + "/" + dto.fastenerDefinitionId))
                        .Select(dto => dto.mountId + "/" + dto.fastenerDefinitionId))))
                {
                    failure = InvalidSave("A partial 21-fastener source revision cannot be migrated.");
                    return false;
                }
                if (present == 0)
                {
                    additions.FastenerKeys.UnionWith(targetBodyKeys);
                    foreach (string slug in BodyAdditionSlugs) additions.BodyMountIds.Add("mount.satsuma." + slug);
                    if (data.fastenerGroups != null && data.fastenerGroups.Any(group => group != null &&
                            plan.BodyMountIds.Contains(group.mountId) && group.isBolted))
                    {
                        failure = InvalidSave("An old empty body fastener group cannot already be bolted.");
                        return false;
                    }
                }
            }

            if (!TryIdentifyReviewedHeadlightAdditions(data, targetBodyKeys, additions, out failure))
                return false;

            int targetMounts = ConsumableMountIds.Count(id => graph.TryGetMount(id, out _));
            int savedMounts = data.mounts.Count(dto => dto != null && ConsumableMountIds.Contains(dto.mountId));
            if (targetMounts != 0 && targetMounts != ConsumableMountIds.Length ||
                savedMounts != 0 && savedMounts != ConsumableMountIds.Length)
            {
                failure = InvalidSave("The reviewed seven-socket revision must be complete.");
                return false;
            }
            if (targetMounts == ConsumableMountIds.Length)
            {
                for (int index = 0; index < ConsumableMountIds.Length; index++)
                {
                    graph.TryGetMount(ConsumableMountIds[index], out MountPointRuntime mount);
                    bool sparkPlug = index < 4;
                    if (mount.Fasteners.Length != (sparkPlug ? 1 : 0) || sparkPlug &&
                        (!mount.TryGetFastener(SatsumaConsumableAssemblyRules.SparkPlugFastenerId(index + 1), out FastenerInstance thread) ||
                         !MatchesReviewedFastener(thread.Definition, FastenerSize.None, SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchType)))
                    {
                        failure = InvalidSave("The reviewed four plug threads or empty belt/bulb fastener sets differ.");
                        return false;
                    }
                }
            }
            if (targetMounts == ConsumableMountIds.Length && savedMounts == 0)
            {
                additions.MountIds.UnionWith(ConsumableMountIds);
                if (data.fasteners.Any(dto => dto != null && plan.MountIds.Contains(dto.mountId)) ||
                    (data.fastenerGroups ?? Array.Empty<FastenerGroupSaveDto>()).Any(dto => dto != null && plan.MountIds.Contains(dto.mountId)))
                {
                    failure = InvalidSave("A missing socket has orphan fastener or group records.");
                    return false;
                }
                foreach (string id in ConsumableMountIds)
                {
                    graph.TryGetMount(id, out MountPointRuntime mount);
                    foreach (FastenerInstance fastener in mount.Fasteners)
                        additions.FastenerKeys.Add(id + "/" + fastener.Definition.DefinitionId);
                }
            }
            return true;
        }

        private bool TryIdentifyReviewedHeadlightAdditions(VehicleAssemblySaveData data,
            HashSet<string> reviewedBodyKeys, ReviewedSaveAdditions additions, out AssemblyOperationResult failure)
        {
            failure = default;
            int authoredHeadlightFasteners = 0;
            foreach (string slug in HeadlightAdditionSlugs)
                if (graph.TryGetMount("mount.satsuma." + slug, out MountPointRuntime mount))
                    authoredHeadlightFasteners += mount.Fasteners.Length;
            if (authoredHeadlightFasteners == 0) return true;

            // The four headlight bolts are a later packet. Folding them into
            // body21 would reject the valid intermediate 124-mount/298-bolt save.
            if (reviewedBodyKeys.Count != 21)
            {
                failure = InvalidSave("The reviewed headlight target requires the complete 21-fastener body revision.");
                return false;
            }
            var headlightKeys = new HashSet<string>(StringComparer.Ordinal);
            var headlightMounts = new HashSet<string>(StringComparer.Ordinal);
            foreach (string slug in HeadlightAdditionSlugs)
            {
                string mountId = "mount.satsuma." + slug;
                if (!graph.TryGetMount(mountId, out MountPointRuntime mount) || mount.Fasteners.Length != 2)
                {
                    failure = InvalidSave("The reviewed four-headlight-fastener target revision is incomplete.");
                    return false;
                }
                var expectedIds = new HashSet<string>(StringComparer.Ordinal);
                for (int number = 1; number <= 2; number++)
                {
                    string id = "fastener.satsuma." + slug + ".boltpm-" + number;
                    if (!mount.TryGetFastener(id, out FastenerInstance fastener) ||
                        !MatchesReviewedFastener(fastener.Definition, FastenerSize.Millimeter7, "Wrench"))
                    {
                        failure = InvalidSave("The reviewed headlight fastener identity or tool contract differs.");
                        return false;
                    }
                    expectedIds.Add(id);
                    headlightKeys.Add(mountId + "/" + id);
                }
                FastenerGroupDefinition group = mount.FastenerGroup.Definition;
                if (group == null || group.FastenerDefinitionIds.Length != 2 ||
                    !expectedIds.SetEquals(group.FastenerDefinitionIds) ||
                    group.AggregateMaximumTightness != 16 || group.BoltedOnThreshold != 2 || group.BoltedOffThreshold != 0)
                {
                    failure = InvalidSave("The reviewed headlight fastener group contract differs.");
                    return false;
                }
                headlightMounts.Add(mountId);
            }

            string[] presentKeys = data.fasteners
                .Where(dto => dto != null && headlightKeys.Contains(dto.mountId + "/" + dto.fastenerDefinitionId))
                .Select(dto => dto.mountId + "/" + dto.fastenerDefinitionId).ToArray();
            if (presentKeys.Length != 0)
            {
                if (presentKeys.Length != 4 || !headlightKeys.SetEquals(presentKeys))
                {
                    failure = InvalidSave("A partial four-headlight-fastener source revision cannot be migrated.");
                    return false;
                }
                if (data.fasteners.Count(dto => dto != null &&
                        reviewedBodyKeys.Contains(dto.mountId + "/" + dto.fastenerDefinitionId)) != 21)
                {
                    failure = InvalidSave("The headlight source revision requires the complete 21-fastener body packet.");
                    return false;
                }
                return true;
            }

            if ((data.fastenerGroups ?? Array.Empty<FastenerGroupSaveDto>()).Any(group =>
                    group != null && headlightMounts.Contains(group.mountId) && group.isBolted))
            {
                failure = InvalidSave("An old empty headlight fastener group cannot already be bolted.");
                return false;
            }
            // Existing assembly materialization supplies insertion/seating from
            // saved mount occupancy, stage zero and a false latch for these IDs.
            additions.FastenerKeys.UnionWith(headlightKeys);
            additions.BodyMountIds.UnionWith(headlightMounts);
            return true;
        }

        private static bool MatchesReviewedFastener(FastenerDefinition definition, FastenerSize size, string toolType) =>
            definition != null && definition.MaximumStage == 8 && definition.Size == size &&
            definition.TighteningDirection == FastenerDirection.ClockwiseToTighten &&
            definition.InsertedOnInstall && definition.RequiredForRemoval && definition.ToolRule != null &&
            definition.ToolRule.ToolType == toolType && definition.ToolRule.FastenerSize == size;
    }
}
