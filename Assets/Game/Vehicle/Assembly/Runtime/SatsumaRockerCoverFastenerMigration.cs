using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Exact retirement of six GT duplicate mounting IDs. Source DTOs are never
    /// mutated; the save storage layer retains its original recoverable payload.
    /// No other cover, fastener group or schema is inferred from object counts.
    /// </summary>
    public static class SatsumaRockerCoverFastenerMigration
    {
        public const string MountId = "mount.satsuma.cylinder-head.rocker-cover";
        public const string FastenerPrefix = "fastener.satsuma.cylinder-head-rocker-cover.boltpm-";
        private static readonly int[] StockNumbers = { 2, 4, 8, 9, 10, 12 };
        private static readonly int[] RetiredNumbers = { 5, 11, 3, 7, 6, 1 };

        public static string[] CanonicalIds => CreateIds(StockNumbers);
        public static string[] RetiredIds => CreateIds(RetiredNumbers);

        public static bool IsCanonicalShape(IReadOnlyList<string> ids) => Matches(ids, false);
        public static bool IsLegacyShape(IReadOnlyList<string> ids) => Matches(ids, true);

        public static bool TryMigrate(VehicleAssemblySaveData source, MountPointDefinition target,
            out VehicleAssemblySaveData prepared, out AssemblyOperationResult result)
        {
            prepared = source;
            result = AssemblyOperationResult.Success(AssemblyOperation.Restore, "No rocker-cover alias migration required.");
            if (source == null || !source.HasSupportedSchema || target == null || target.DefinitionId != MountId) return true;
            var targetIds = new List<string>();
            foreach (FastenerDefinition definition in target.Fasteners)
            {
                if (definition == null || definition.MaximumStage != 8 ||
                    definition.Size != FastenerSize.Millimeter7 || !definition.RequiredForRemoval)
                    return Fail("Rocker-cover target definition drifted.", out result);
                targetIds.Add(definition.DefinitionId);
            }
            // An older authored prefab is not migrated into a shape it cannot load.
            if (IsLegacyShape(targetIds)) return true;
            if (!IsCanonicalShape(targetIds) || target.FastenerGroup == null ||
                !IsCanonicalShape(target.FastenerGroup.FastenerDefinitionIds) ||
                target.FastenerGroup.AggregateMaximumTightness != 48 ||
                target.FastenerGroup.BoltedOnThreshold != 2 || target.FastenerGroup.BoltedOffThreshold != 0)
                return Fail("Rocker-cover target must be the reviewed six-bolt contract.", out result);
            if (source.fasteners == null) return true; // General save validator owns null sections.

            var coverStates = new Dictionary<string, FastenerSaveDto>(StringComparer.Ordinal);
            foreach (FastenerSaveDto state in source.fasteners)
            {
                if (state == null || state.mountId != MountId) continue;
                if (string.IsNullOrEmpty(state.fastenerDefinitionId) ||
                    coverStates.ContainsKey(state.fastenerDefinitionId))
                    return Fail("Duplicate or empty rocker-cover fastener identity.", out result);
                coverStates.Add(state.fastenerDefinitionId, state);
            }
            var savedIds = new List<string>(coverStates.Keys);
            if (savedIds.Count == 0 || IsCanonicalShape(savedIds)) return true;
            if (!IsLegacyShape(savedIds))
                return Fail("Partial or unknown rocker-cover alias payload; original save preserved.", out result);

            int oldTightness = 0;
            foreach (FastenerSaveDto state in coverStates.Values)
            {
                if (state.stage < 0 || state.stage > 8 ||
                    (!state.inserted && (state.seated || state.stage != 0)) ||
                    (!state.seated && state.stage != 0))
                    return Fail("Invalid retired rocker-cover fastener state; original save preserved.", out result);
                oldTightness += state.stage;
            }
            MountSaveDto savedMount = null;
            foreach (MountSaveDto mount in source.mounts ?? Array.Empty<MountSaveDto>())
            {
                if (mount == null || mount.mountId != MountId) continue;
                if (savedMount != null) return Fail("Duplicate rocker-cover mount state.", out result);
                savedMount = mount;
            }
            if (savedMount == null) return Fail("Legacy rocker-cover fasteners have no mount state.", out result);
            bool occupied = !string.IsNullOrWhiteSpace(savedMount.installedPartStableEntityId);
            if (!occupied)
                foreach (FastenerSaveDto state in coverStates.Values)
                    if (state.inserted || state.seated || state.stage != 0)
                        return Fail("Empty rocker-cover mount contains inserted fasteners.", out result);

            FastenerGroupSaveDto savedGroup = null;
            foreach (FastenerGroupSaveDto group in source.fastenerGroups ?? Array.Empty<FastenerGroupSaveDto>())
            {
                if (group == null || group.mountId != MountId) continue;
                if (savedGroup != null) return Fail("Duplicate rocker-cover latch state.", out result);
                savedGroup = group;
            }
            if (source.schemaVersion >= VehicleAssemblySaveData.FastenerGroupSchemaVersion && savedGroup == null)
                return Fail("Legacy rocker-cover shape is missing its group latch.", out result);
            if (savedGroup != null && savedGroup.isBolted != (occupied && oldTightness >= 1))
                return Fail("Legacy rocker-cover latch contradicts its ON1/OFF0 contract.", out result);

            var states = new List<FastenerSaveDto>(source.fasteners.Length - 6);
            foreach (FastenerSaveDto state in source.fasteners)
                if (state == null || state.mountId != MountId) states.Add(state);
            int tightness = 0;
            for (int index = 0; index < StockNumbers.Length; index++)
            {
                FastenerSaveDto stock = coverStates[FastenerPrefix + StockNumbers[index]];
                FastenerSaveDto duplicate = coverStates[FastenerPrefix + RetiredNumbers[index]];
                int stage = Math.Max(stock.stage, duplicate.stage);
                tightness += stage;
                states.Add(new FastenerSaveDto
                {
                    mountId = MountId, fastenerDefinitionId = stock.fastenerDefinitionId,
                    inserted = stock.inserted || duplicate.inserted,
                    seated = stock.seated || duplicate.seated, stage = stage,
                });
            }
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
                "Migrated six exact rocker-cover aliases using the maximum stage in each physical pair.");
            return true;
        }

        private static string[] CreateIds(int[] numbers)
        {
            var ids = new string[numbers.Length];
            for (int index = 0; index < ids.Length; index++) ids[index] = FastenerPrefix + numbers[index];
            return ids;
        }

        private static bool Matches(IReadOnlyList<string> ids, bool legacy)
        {
            if (ids == null || ids.Count != (legacy ? 12 : 6)) return false;
            var found = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                bool expected = false;
                for (int slot = 0; slot < StockNumbers.Length; slot++)
                    expected |= id == FastenerPrefix + StockNumbers[slot] ||
                        legacy && id == FastenerPrefix + RetiredNumbers[slot];
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
