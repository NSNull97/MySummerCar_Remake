using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Retires only the mixture-adjustment pseudo-fastener accidentally imported
    /// into the stock carburetor mounting group. The tuning value has no truthful
    /// mapping from its old eight assembly stages and uses its optional DTO/default.
    /// </summary>
    public static class SatsumaCarburetorFastenerMigration
    {
        public const string MountId = "mount.satsuma.cylinder-head.carburetor";
        public const string FastenerPrefix = "fastener.satsuma.cylinder-head-carburetor.boltpm-";
        public const string RetiredId = FastenerPrefix + "3";
        public static string[] CanonicalIds => new[]
        { FastenerPrefix + "1", FastenerPrefix + "2", FastenerPrefix + "4", FastenerPrefix + "5" };

        public static bool IsCanonicalShape(IReadOnlyList<string> ids) => Matches(ids, false);
        public static bool IsLegacyShape(IReadOnlyList<string> ids) => Matches(ids, true);

        public static bool TryMigrate(VehicleAssemblySaveData source, MountPointDefinition target,
            out VehicleAssemblySaveData prepared, out AssemblyOperationResult result)
        {
            prepared = source;
            result = AssemblyOperationResult.Success(AssemblyOperation.Restore,
                "No carburetor pseudo-fastener retirement required.");
            if (source == null || !source.HasSupportedSchema || target == null || target.DefinitionId != MountId)
                return true;
            var targetIds = new List<string>();
            foreach (FastenerDefinition definition in target.Fasteners)
            {
                if (definition == null || definition.MaximumStage != 8 || !definition.RequiredForRemoval ||
                    definition.DefinitionId != RetiredId && definition.Size != FastenerSize.Millimeter8)
                    return Fail("Carburetor target definition drifted.", out result);
                targetIds.Add(definition.DefinitionId);
            }
            // An old generated prefab retains the old shape. Never migrate it
            // forward into a set of IDs which its current graph cannot accept.
            if (IsLegacyShape(targetIds)) return true;
            if (!IsCanonicalShape(targetIds) || target.FastenerGroup == null ||
                !IsCanonicalShape(target.FastenerGroup.FastenerDefinitionIds) ||
                target.FastenerGroup.AggregateMaximumTightness != 32 ||
                target.FastenerGroup.BoltedOnThreshold != 8 || target.FastenerGroup.BoltedOffThreshold != 0)
                return Fail("Carburetor target must be the reviewed four-bolt ON8/OFF0 contract.", out result);
            if (source.fasteners == null) return true;

            var saved = new Dictionary<string, FastenerSaveDto>(StringComparer.Ordinal);
            foreach (FastenerSaveDto state in source.fasteners)
            {
                if (state == null || state.mountId != MountId) continue;
                if (string.IsNullOrEmpty(state.fastenerDefinitionId) || !saved.TryAdd(state.fastenerDefinitionId, state))
                    return Fail("Duplicate or empty carburetor fastener identity.", out result);
            }
            var savedIds = new List<string>(saved.Keys);
            if (savedIds.Count == 0 || IsCanonicalShape(savedIds)) return true;
            if (!IsLegacyShape(savedIds))
                return Fail("Partial or unknown carburetor pseudo-fastener payload; original save preserved.", out result);

            int oldTightness = 0;
            foreach (FastenerSaveDto state in saved.Values)
            {
                if (state.stage < 0 || state.stage > 8 ||
                    !state.inserted && (state.seated || state.stage != 0) ||
                    !state.seated && state.stage != 0)
                    return Fail("Invalid retired carburetor fastener state; original save preserved.", out result);
                oldTightness += state.stage;
            }
            MountSaveDto savedMount = null;
            foreach (MountSaveDto mount in source.mounts ?? Array.Empty<MountSaveDto>())
            {
                if (mount == null || mount.mountId != MountId) continue;
                if (savedMount != null) return Fail("Duplicate carburetor mount state.", out result);
                savedMount = mount;
            }
            if (savedMount == null) return Fail("Legacy carburetor fasteners have no mount state.", out result);
            bool occupied = !string.IsNullOrWhiteSpace(savedMount.installedPartStableEntityId);
            if (!occupied)
                foreach (FastenerSaveDto state in saved.Values)
                    if (state.inserted || state.seated || state.stage != 0)
                        return Fail("Empty carburetor mount contains inserted fasteners.", out result);

            FastenerGroupSaveDto savedGroup = null;
            foreach (FastenerGroupSaveDto group in source.fastenerGroups ?? Array.Empty<FastenerGroupSaveDto>())
            {
                if (group == null || group.mountId != MountId) continue;
                if (savedGroup != null) return Fail("Duplicate carburetor latch state.", out result);
                savedGroup = group;
            }
            if (source.schemaVersion >= VehicleAssemblySaveData.FastenerGroupSchemaVersion && savedGroup == null)
                return Fail("Legacy carburetor shape is missing its group latch.", out result);
            if (savedGroup != null && savedGroup.isBolted != (occupied && oldTightness >= 1))
                return Fail("Legacy carburetor latch contradicts its ON1/OFF0 contract.", out result);

            var states = new List<FastenerSaveDto>(source.fasteners.Length - 1);
            foreach (FastenerSaveDto state in source.fasteners)
                if (state == null || state.mountId != MountId || state.fastenerDefinitionId != RetiredId)
                    states.Add(state);
            int tightness = oldTightness - saved[RetiredId].stage;
            FastenerGroupSaveDto[] groups = source.fastenerGroups;
            if (savedGroup != null)
            {
                groups = (FastenerGroupSaveDto[])source.fastenerGroups.Clone();
                bool latch = target.FastenerGroup.IsLatchConsistent(tightness, savedGroup.isBolted, occupied)
                    ? savedGroup.isBolted : occupied && tightness >= target.FastenerGroup.BoltedOnThreshold;
                for (int index = 0; index < groups.Length; index++)
                    if (ReferenceEquals(groups[index], savedGroup))
                        groups[index] = new FastenerGroupSaveDto { mountId = MountId, isBolted = latch };
            }
            prepared = new VehicleAssemblySaveData
            {
                schemaVersion = source.schemaVersion, parts = source.parts, mounts = source.mounts,
                dynamicParts = source.dynamicParts,
                fasteners = states.ToArray(), fastenerGroups = groups,
            };
            result = AssemblyOperationResult.Success(AssemblyOperation.Restore,
                "Retired the exact mixture pseudo-bolt; preserved all four mounting states.");
            return true;
        }

        private static bool Matches(IReadOnlyList<string> ids, bool legacy)
        {
            if (ids == null || ids.Count != (legacy ? 5 : 4)) return false;
            var found = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                bool expected = legacy && id == RetiredId;
                foreach (string canonical in CanonicalIds) expected |= id == canonical;
                if (!expected || !found.Add(id)) return false;
            }
            return true;
        }

        private static bool Fail(string message, out AssemblyOperationResult result)
        {
            result = AssemblyOperationResult.Failure(AssemblyOperation.Restore,
                AssemblyFailureReason.InvalidSaveData, message);
            return false;
        }
    }
}
