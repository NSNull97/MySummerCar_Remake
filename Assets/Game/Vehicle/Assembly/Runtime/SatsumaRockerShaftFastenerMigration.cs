using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Exact 13-pseudo-bolt to five-retention-bolt migration; valve settings are never inferred from stages.</summary>
    public static class SatsumaRockerShaftFastenerMigration
    {
        public const string MountId = "mount.satsuma.cylinder-head.rocker-shaft";
        public const string Prefix = "fastener.satsuma.cylinder-head-rocker-shaft.boltpm-";
        public static string[] CanonicalIds => new[] { Prefix + "2", Prefix + "5", Prefix + "6", Prefix + "8", Prefix + "9" };
        public static string[] RetiredIds => new[] { Prefix + "1", Prefix + "3", Prefix + "4", Prefix + "7",
            Prefix + "10", Prefix + "11", Prefix + "12", Prefix + "13" };
        public static bool IsCanonicalShape(IReadOnlyList<string> ids) => Matches(ids, false);
        public static bool IsLegacyShape(IReadOnlyList<string> ids) => Matches(ids, true);

        public static bool TryMigrate(VehicleAssemblySaveData source, MountPointDefinition target,
            out VehicleAssemblySaveData prepared, out AssemblyOperationResult result)
        {
            prepared = source;
            result = AssemblyOperationResult.Success(AssemblyOperation.Restore, "No rocker-shaft retirement required.");
            if (source == null || !source.HasSupportedSchema || target == null || target.DefinitionId != MountId) return true;
            var targetIds = new List<string>();
            var canonical = new HashSet<string>(CanonicalIds, StringComparer.Ordinal);
            foreach (FastenerDefinition definition in target.Fasteners)
            {
                if (definition == null || definition.MaximumStage != 8 || !definition.RequiredForRemoval ||
                    canonical.Contains(definition.DefinitionId) && definition.Size != FastenerSize.Millimeter8)
                    return Fail("Rocker-shaft target fastener contract drifted.", out result);
                targetIds.Add(definition.DefinitionId);
            }
            if (IsLegacyShape(targetIds)) return true;
            if (!IsCanonicalShape(targetIds) || target.FastenerGroup == null ||
                !IsCanonicalShape(target.FastenerGroup.FastenerDefinitionIds) ||
                target.FastenerGroup.AggregateMaximumTightness != 40 ||
                target.FastenerGroup.BoltedOnThreshold != 16 || target.FastenerGroup.BoltedOffThreshold != 0)
                return Fail("Rocker-shaft target must have the reviewed five-bolt ON16/OFF0 contract.", out result);
            if (source.fasteners == null) return true;
            var saved = new Dictionary<string, FastenerSaveDto>(StringComparer.Ordinal);
            foreach (FastenerSaveDto state in source.fasteners)
            {
                if (state == null || state.mountId != MountId) continue;
                if (string.IsNullOrEmpty(state.fastenerDefinitionId) || !saved.TryAdd(state.fastenerDefinitionId, state))
                    return Fail("Duplicate or empty rocker-shaft fastener identity.", out result);
            }
            var savedIds = new List<string>(saved.Keys);
            if (savedIds.Count == 0 || IsCanonicalShape(savedIds)) return true;
            if (!IsLegacyShape(savedIds)) return Fail("Partial or unknown rocker-shaft retirement payload.", out result);
            int oldTightness = 0, retainedTightness = 0;
            foreach (FastenerSaveDto state in saved.Values)
            {
                if (state.stage < 0 || state.stage > 8 || !state.inserted && (state.seated || state.stage != 0) ||
                    !state.seated && state.stage != 0)
                    return Fail("Invalid retired rocker-shaft fastener state.", out result);
                oldTightness += state.stage;
                if (canonical.Contains(state.fastenerDefinitionId)) retainedTightness += state.stage;
            }
            MountSaveDto savedMount = null;
            foreach (MountSaveDto mount in source.mounts ?? Array.Empty<MountSaveDto>())
            {
                if (mount == null || mount.mountId != MountId) continue;
                if (savedMount != null) return Fail("Duplicate rocker-shaft mount state.", out result);
                savedMount = mount;
            }
            if (savedMount == null) return Fail("Retired rocker-shaft stages have no mount state.", out result);
            bool occupied = !string.IsNullOrEmpty(savedMount.installedPartStableEntityId);
            if (!occupied)
                foreach (FastenerSaveDto state in saved.Values)
                    if (state.inserted || state.seated || state.stage != 0)
                        return Fail("Empty rocker-shaft mount has inserted fasteners.", out result);
            FastenerGroupSaveDto savedGroup = null;
            foreach (FastenerGroupSaveDto group in source.fastenerGroups ?? Array.Empty<FastenerGroupSaveDto>())
            {
                if (group == null || group.mountId != MountId) continue;
                if (savedGroup != null) return Fail("Duplicate rocker-shaft latch state.", out result);
                savedGroup = group;
            }
            if (source.schemaVersion >= VehicleAssemblySaveData.FastenerGroupSchemaVersion && savedGroup == null)
                return Fail("Legacy rocker-shaft group latch is missing.", out result);
            if (savedGroup != null && savedGroup.isBolted != (occupied && oldTightness >= 1))
                return Fail("Legacy rocker-shaft latch contradicts its ON1/OFF0 contract.", out result);
            var states = new List<FastenerSaveDto>(source.fasteners.Length - 8);
            foreach (FastenerSaveDto state in source.fasteners)
                if (state == null || state.mountId != MountId || canonical.Contains(state.fastenerDefinitionId)) states.Add(state);
            FastenerGroupSaveDto[] groups = source.fastenerGroups;
            if (savedGroup != null)
            {
                groups = (FastenerGroupSaveDto[])source.fastenerGroups.Clone();
                bool latch = target.FastenerGroup.IsLatchConsistent(retainedTightness, savedGroup.isBolted, occupied)
                    ? savedGroup.isBolted : occupied && retainedTightness >= 16;
                for (int i = 0; i < groups.Length; i++)
                    if (ReferenceEquals(groups[i], savedGroup)) groups[i] = new FastenerGroupSaveDto { mountId = MountId, isBolted = latch };
            }
            prepared = new VehicleAssemblySaveData
            {
                schemaVersion = source.schemaVersion, parts = source.parts, mounts = source.mounts,
                dynamicParts = source.dynamicParts, fasteners = states.ToArray(), fastenerGroups = groups,
            };
            result = AssemblyOperationResult.Success(AssemblyOperation.Restore,
                "Retired eight valve pseudo-bolts; preserved all five mounting states and part-owned settings.");
            return true;
        }
        private static bool Matches(IReadOnlyList<string> ids, bool legacy)
        {
            if (ids == null || ids.Count != (legacy ? 13 : 5)) return false;
            var expected = new HashSet<string>(CanonicalIds, StringComparer.Ordinal);
            if (legacy) expected.UnionWith(RetiredIds);
            var found = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++) if (!expected.Contains(ids[i]) || !found.Add(ids[i])) return false;
            return true;
        }
        private static bool Fail(string message, out AssemblyOperationResult result)
        {
            result = AssemblyOperationResult.Failure(AssemblyOperation.Restore, AssemblyFailureReason.InvalidSaveData, message);
            return false;
        }
    }
}
