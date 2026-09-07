using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Vehicle.Assembly;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    /// <summary>Explicit additive night-packet identity set; also reconstructs historical test inputs.</summary>
    internal static class SatsumaCanonicalNightTestShape
    {
        internal static readonly string[] AddedMountIds =
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
            "mount.satsuma.engine-block.alternator-belt", "mount.satsuma.headlight-left.light-bulb",
            "mount.satsuma.headlight-right.light-bulb",
        };
        private static readonly string[] HeadlightKeys =
        {
            "mount.satsuma.headlight-left/fastener.satsuma.headlight-left.boltpm-1",
            "mount.satsuma.headlight-left/fastener.satsuma.headlight-left.boltpm-2",
            "mount.satsuma.headlight-right/fastener.satsuma.headlight-right.boltpm-1",
            "mount.satsuma.headlight-right/fastener.satsuma.headlight-right.boltpm-2",
        };
        private static readonly string[] Previous298AddedKeys = BuildPrevious298AddedKeys();
        private static readonly string[] AddedKeys = Previous298AddedKeys.Concat(HeadlightKeys).ToArray();
        internal static bool IsAddedMount(string id) => AddedMountIds.Contains(id, StringComparer.Ordinal);
        internal static bool IsAddedFastener(FastenerSaveDto value) => value != null &&
            AddedKeys.Contains(value.mountId + "/" + value.fastenerDefinitionId, StringComparer.Ordinal);
        internal static bool IsAddedFastener(AssemblyFastenerInteractionTarget value) => value != null &&
            AddedKeys.Contains(value.MountId + "/" + value.FastenerDefinitionId, StringComparer.Ordinal);
        internal static bool IsHeadlightFastener(AssemblyFastenerInteractionTarget value) => value != null &&
            HeadlightKeys.Contains(value.MountId + "/" + value.FastenerDefinitionId, StringComparer.Ordinal);

        internal static void AssertCanonical(VehicleAssemblyController assembly)
        {
            Assert.That(assembly.MountPoints, Has.Length.EqualTo(124));
            AssertAddedMounts(assembly.MountPoints.Select(value => value.MountId));
            AssertCanonicalTargets(assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true));
        }

        internal static void AssertCanonicalTargets(IEnumerable<AssemblyFastenerInteractionTarget> targets)
        {
            string[] keys = targets.Select(value => value.MountId + "/" + value.FastenerDefinitionId).ToArray();
            Assert.That(keys, Has.Length.EqualTo(294));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(294));
            Assert.That(AddedKeys, Has.Length.EqualTo(29));
            Assert.That(keys.Where(key => AddedKeys.Contains(key, StringComparer.Ordinal)), Is.EquivalentTo(AddedKeys));
            Assert.That(keys.Count(key => !AddedKeys.Contains(key, StringComparer.Ordinal)), Is.EqualTo(265));
        }

        internal static void AssertPrevious298Keys(IEnumerable<string> keysSource)
        {
            string[] keys = keysSource.ToArray();
            Assert.That(keys, Has.Length.EqualTo(298));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(298));
            Assert.That(keys.Any(key => HeadlightKeys.Contains(key, StringComparer.Ordinal)), Is.False);
            Assert.That(keys.Where(key => Previous298AddedKeys.Contains(key, StringComparer.Ordinal)),
                Is.EquivalentTo(Previous298AddedKeys));
            Assert.That(keys.Count(key => !Previous298AddedKeys.Contains(key, StringComparer.Ordinal)), Is.EqualTo(273));
        }

        internal static void AssertCanonical(VehicleAssemblySaveData save)
        {
            Assert.That(save.mounts, Has.Length.EqualTo(124));
            Assert.That(save.fastenerGroups, Has.Length.EqualTo(124));
            Assert.That(save.fasteners, Has.Length.EqualTo(294));
            AssertAddedMounts(save.mounts.Select(value => value.mountId));
            AssertAddedMounts(save.fastenerGroups.Select(value => value.mountId));
            Assert.That(save.fasteners.Where(IsAddedFastener).Select(value => value.mountId + "/" + value.fastenerDefinitionId),
                Is.EquivalentTo(AddedKeys));
            Assert.That(save.fasteners.Count(value => !IsAddedFastener(value)), Is.EqualTo(265));
        }

        // Historical fixtures retain their real old ID roster. Do not relabel a
        // shortened current payload as a historical 252/260/298 save.
        internal static FastenerSaveDto[] RestoreHistoricalValveFasteners(IEnumerable<FastenerSaveDto> source)
        {
            FastenerSaveDto[] values = source.ToArray();
            FastenerSaveDto[] rocker = values.Where(value => value.mountId == SatsumaRockerShaftFastenerMigration.MountId).ToArray();
            Assert.That(SatsumaRockerShaftFastenerMigration.IsCanonicalShape(rocker.Select(value => value.fastenerDefinitionId).ToArray()), Is.True);
            return values.Concat(SatsumaRockerShaftFastenerMigration.RetiredIds.Select(id => new FastenerSaveDto
            {
                mountId = SatsumaRockerShaftFastenerMigration.MountId, fastenerDefinitionId = id,
                inserted = rocker[0].inserted, seated = rocker[0].seated, stage = 0,
            })).ToArray();
        }

        internal static void StripAddedMountsFromHistoricalInput(VehicleAssemblySaveData save)
        {
            save.mounts = save.mounts.Where(value => !IsAddedMount(value.mountId)).ToArray();
            save.fastenerGroups = save.fastenerGroups.Where(value => !IsAddedMount(value.mountId)).ToArray();
            Assert.That(save.mounts, Has.Length.EqualTo(117));
            Assert.That(save.fastenerGroups, Has.Length.EqualTo(117));
        }

        private static void AssertAddedMounts(IEnumerable<string> ids)
        {
            Assert.That(ids.Where(IsAddedMount), Is.EquivalentTo(AddedMountIds));
        }

        private static string[] BuildPrevious298AddedKeys()
        {
            var keys = new List<string>();
            foreach (var group in new[] { ("exhaust-pipe", 3), ("exhaust-muffler", 1),
                ("fuel-tank", 7), ("seat-driver", 4), ("seat-passenger", 4), ("seat-rear", 2) })
                for (int i = 1; i <= group.Item2; i++)
                    keys.Add("mount.satsuma." + group.Item1 + "/fastener.satsuma." + group.Item1 + ".boltpm-" + i);
            for (int i = 1; i <= 4; i++)
                keys.Add("mount.satsuma.cylinder-head.spark-plug-" + i +
                    "/fastener.satsuma.cylinder-head-spark-plug-" + i + ".thread");
            return keys.ToArray();
        }
    }
}
