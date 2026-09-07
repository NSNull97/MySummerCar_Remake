using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Reviewed purchased-item sockets; no changes to ordinary authored parts.</summary>
    public static class SatsumaConsumableAssemblyRules
    {
        public const string SparkPlugPartId = "vehicle.satsuma.part.spark-plug";
        public const string BeltPartId = "vehicle.satsuma.part.alternator-belt";
        public const string BulbPartId = "vehicle.satsuma.part.light-bulb";
        public const string BeltMountId = "mount.satsuma.engine-block.alternator-belt";
        public const string AlternatorMountId = "mount.satsuma.engine-block.alternator";
        public const float MaximumBeltAssemblyAlternatorSettingExclusive = 4f;
        public const int SparkPlugMaximumStage = 8;
        private static readonly string[] PlugMounts =
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
        };

        public static string SparkPlugMountId(int index) =>
            "mount.satsuma.cylinder-head.spark-plug-" + index;

        public static string SparkPlugFastenerId(int index) =>
            "fastener.satsuma.cylinder-head-spark-plug-" + index + ".thread";

        public static bool IsSparkPlugMount(string id)
        {
            for (int index = 0; index < PlugMounts.Length; index++)
                if (id == PlugMounts[index]) return true;
            return false;
        }

        public static AssemblyOperationResult EvaluateInstall(
            PartInstance part, MountPointRuntime mount, AssemblyGraph graph)
        {
            if (!IsBelt(part, mount)) return Success(AssemblyOperation.Install);
            // Frozen GAME 110896 Requirements tests three Installed flags;
            // the mount definition owns those gates. Check rot requires <4,
            // including a rejection at exactly 4, regardless of Bolted.
            if (!TryGetAlternator(graph, out MountPointRuntime alternator) ||
                !IsAlternatorSlack(alternator))
                return AssemblyOperationResult.Failure(AssemblyOperation.Install,
                    AssemblyFailureReason.MissingPrerequisite,
                    "Поверните генератор к двигателю перед установкой ремня.");
            return Success(AssemblyOperation.Install);
        }

        public static AssemblyOperationResult EvaluateRemoval(
            PartInstance part, MountPointRuntime mount, AssemblyGraph graph)
        {
            if (!IsBelt(part, mount)) return Success(AssemblyOperation.Remove);
            // alternatorbelt0 Removal.Requirements: !Bolted -> REMOVE directly;
            // Bolted -> Check rot, where only Rotation <4 permits removal.
            if (TryGetAlternator(graph, out MountPointRuntime alternator) &&
                alternator.FastenerGroup.IsBolted && !IsAlternatorSlack(alternator))
                return AssemblyOperationResult.Failure(AssemblyOperation.Remove,
                    AssemblyFailureReason.MissingPrerequisite,
                    "Ослабьте крепление генератора или натяжение ремня.");
            return Success(AssemblyOperation.Remove);
        }

        private static bool IsBelt(PartInstance part, MountPointRuntime mount) =>
            part != null && part.Definition != null && part.Definition.DefinitionId == BeltPartId &&
            mount != null && mount.MountId == BeltMountId;

        private static bool TryGetAlternator(AssemblyGraph graph, out MountPointRuntime mount)
        {
            mount = null;
            return graph != null && graph.TryGetMount(AlternatorMountId, out mount) &&
                mount.IsOccupied && mount.InstalledPart != null;
        }

        private static bool IsAlternatorSlack(MountPointRuntime mount)
        {
            AssemblyEngineAdjustmentState adjustment =
                mount.InstalledPart.GetComponent<AssemblyEngineAdjustmentState>();
            return adjustment != null && adjustment.Kind == SatsumaEngineAdjustmentKind.Alternator &&
                float.IsFinite(adjustment.Setting) &&
                adjustment.Setting < MaximumBeltAssemblyAlternatorSettingExclusive;
        }

        private static AssemblyOperationResult Success(AssemblyOperation operation) =>
            AssemblyOperationResult.Success(operation, string.Empty);
    }
}
