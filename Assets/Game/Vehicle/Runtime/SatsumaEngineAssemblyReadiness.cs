using MSC.Vehicle.Assembly;

namespace MSC.Vehicle
{
    /// <summary>
    /// Undamaged stock assembly gates, reimplemented from the frozen donor
    /// Combustion/Powertrain and Cylinders records. This is not a wear, tuning
    /// or random-misfire simulation. Tightness is not a blanket eight-stage gate.
    /// </summary>
    public static class SatsumaEngineAssemblyReadiness
    {
        public const string BlockId = "vehicle.satsuma.part.engine-block";
        private static readonly string[] PowertrainIds =
        {
            "vehicle.satsuma.part.crankshaft", "vehicle.satsuma.part.camshaft",
            "vehicle.satsuma.part.timing-chain", "vehicle.satsuma.part.rocker-shaft",
        };
        private static readonly string[] PistonIds =
        {
            "vehicle.satsuma.part.piston1", "vehicle.satsuma.part.piston2",
            "vehicle.satsuma.part.piston3", "vehicle.satsuma.part.piston4",
        };
        private static readonly string[] PlugMountIds =
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
        };

        public static bool IsStructuralCombustionReady(AssemblyGraph graph)
        {
            if (graph == null) return false;
            for (int i = 0; i < PowertrainIds.Length; i++)
                if (!graph.IsPartDefinitionInstalled(PowertrainIds[i])) return false;

            // The current fixed roster contains the stock flywheel only. Do not
            // invent a racing-part ID; its future bridge must extend this OR.
            return graph.IsPartDefinitionBolted("vehicle.satsuma.part.cylinder-head") &&
                graph.IsPartDefinitionInstalled("vehicle.satsuma.part.flywheel") &&
                graph.IsPartDefinitionInstalled("vehicle.satsuma.part.electrics") &&
                graph.IsPartDefinitionBolted("vehicle.satsuma.part.distributor");
        }

        public static int EvaluateFiringCylinderMask(AssemblyGraph graph)
        {
            if (graph == null) return 0;
            int mask = 0;
            for (int i = 0; i < PistonIds.Length; i++)
            {
                if (!graph.IsPartDefinitionInstalled(PistonIds[i]) ||
                    !graph.TryGetMount(PlugMountIds[i], out MountPointRuntime mount) ||
                    !mount.IsOccupied || mount.InstalledPart.Definition == null ||
                    mount.InstalledPart.Definition.DefinitionId != SatsumaConsumableAssemblyRules.SparkPlugPartId)
                    continue;

                // Optional fixed-roster wear extension. Pre-extension fixtures
                // retain the already accepted assembly-only behavior.
                PartInstance[] parts = graph.AllRuntimeParts;
                bool pistonUsable = true;
                for (int p = 0; p < parts.Length; p++)
                {
                    PartInstance piston = parts[p];
                    if (piston?.Definition == null || !piston.IsInstalled ||
                        piston.Definition.DefinitionId != PistonIds[i]) continue;
                    AssemblyMechanicalConditionState health = piston.GetComponent<AssemblyMechanicalConditionState>();
                    pistonUsable = health == null || !health.IsBroken && health.ConditionPercent >= 10f;
                    break;
                }
                if (!pistonUsable) continue;

                IAssemblyItemCondition condition = mount.InstalledPart.GetComponent<IAssemblyItemCondition>();
                // Purchased units must retain the item-owned condition bridge;
                // a bare test/fake PartInstance is not an implicitly new plug.
                if (condition != null && IsPlugUsable(condition.ConditionPercent, condition.IsBroken))
                    mask |= 1 << i;
            }
            return mask;
        }

        // FuelLine/Flow: an untightened hard line leaks; a missing air filter
        // accumulates dirt. Neither is a binary startup veto in this record.
        public static bool IsStockFuelDeliveryReady(AssemblyGraph graph) => graph != null &&
            graph.IsPartDefinitionInstalled("vehicle.satsuma.part.fuel-tank") &&
            graph.IsPartDefinitionInstalled("vehicle.satsuma.part.fuel-strainer") &&
            graph.IsPartDefinitionInstalled("vehicle.satsuma.part.fuel-pump") &&
            graph.IsPartDefinitionBolted("vehicle.satsuma.part.carburetor") &&
            graph.IsPartDefinitionInstalled("vehicle.satsuma.part.timing-chain");

        public static bool IsPlugUsable(float conditionPercent, bool broken) =>
            !broken && float.IsFinite(conditionPercent) && conditionPercent >= 1f;

        // Original Piston pairs ok?: at least one of 1/4 AND one of 2/3.
        // Loose plugs and wear <10 lead to misfires, not this binary veto.
        public static bool HasFiringPairs(int mask) => (mask & 0b1001) != 0 && (mask & 0b0110) != 0;
    }
}
