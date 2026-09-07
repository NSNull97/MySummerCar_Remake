using System;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Converts the explicit M06 AssemblyGraph fixture into a cached simulation
    /// prerequisite snapshot. Graph scanning occurs only after a graph mutation.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class AssemblyVehiclePrerequisiteAdapter : MonoBehaviour,
        IVehicleSimulationPrerequisiteSource, IVehicleFluidReadinessTestOverride, IVehicleFuelReadinessTestOverride
    {
        private static readonly string[] DefaultEngineParts = { "m06.engine" };
        private static readonly string[] DefaultStarterParts = { "m06.starter" };
        private static readonly string[] DefaultBatteryParts = { "m06.battery" };
        private static readonly string[] DefaultFuelParts = { "m06.fuel_tank" };
        private static readonly string[] DefaultDrivetrainParts =
        {
            "m06.clutch",
            "m06.gearbox",
            "m06.differential"
        };
        private static readonly string[] DefaultDrivenWheelParts =
        {
            "m06.wheel.fl",
            "m06.wheel.fr",
            "m06.wheel.rl",
            "m06.wheel.rr"
        };

        [SerializeField] private VehicleAssemblyController assemblyController;
        [SerializeField] private string[] requiredEnginePartIds = DefaultEngineParts;
        [SerializeField] private string[] requiredStarterPartIds = DefaultStarterParts;
        [SerializeField] private string[] requiredBatteryPartIds = DefaultBatteryParts;
        [SerializeField] private string[] requiredFuelPartIds = DefaultFuelParts;
        [SerializeField] private string[] requiredDrivetrainPartIds = DefaultDrivetrainParts;
        [SerializeField] private string[] requiredDrivenWheelPartIds = DefaultDrivenWheelParts;
        [SerializeField] private bool fuelAvailable = true;
        [SerializeField] private bool oilAvailable = true;
        [SerializeField] private bool coolantAvailable = true;
        [SerializeField, Min(0f)] private float batteryVoltage = 12.6f;
        [SerializeField, Min(0f)] private float minimumCrankingVoltage = 9.5f;
        [SerializeField] private bool useSatsumaAssemblyRequirements;
        [SerializeField] private SatsumaElectricalSystem satsumaElectrical;

        private int cachedGraphMutationCount = int.MinValue;
        private VehicleSimulationPrerequisiteFailure cachedAssemblyFailures =
            VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable;
        private bool cachedSatsumaStructuralCombustionReady;
        private bool simulationOwnsOperatingFluids;

        // Opt-in host composition: the solver's real litres replace the old
        // authored prototype availability flags, not assembly/fuel-line gates.
        internal void UseSimulationFluidAuthority(bool value) => simulationOwnsOperatingFluids = value;

        private static readonly string[] SatsumaDrivetrainParts =
        {
            "vehicle.satsuma.part.clutch", "vehicle.satsuma.part.gearbox",
            "vehicle.satsuma.part.halfshaft-1", "vehicle.satsuma.part.halfshaft-2",
        };
        private static readonly string[] SatsumaDrivenMountIds =
        {
            "mount.satsuma.wheelfl-new", "mount.satsuma.wheelfr-new",
        };

        public VehicleAssemblyController AssemblyController => assemblyController;
        public bool UsesSatsumaAssemblyRequirements => useSatsumaAssemblyRequirements;
        public SatsumaElectricalSystem SatsumaElectrical => satsumaElectrical;

        // Deliberately not serialized or captured by native saves. Does not
        // bypass a missing tank, engine, battery or any electrical circuit.
        public bool IgnoreFluidReadinessForTesting { get; private set; }
        public bool IgnoreFuelReadinessForTesting { get; private set; }
        public void SetFuelReadinessTestOverride(bool enabled) => IgnoreFuelReadinessForTesting = enabled;

        public void SetFluidReadinessTestOverride(bool enabled) =>
            IgnoreFluidReadinessForTesting = enabled;

        public int CachedGraphMutationCount => cachedGraphMutationCount;

        public VehicleSimulationPrerequisiteFailure CachedAssemblyFailures => cachedAssemblyFailures;

        public void Configure(VehicleAssemblyController controller)
        {
            assemblyController = controller;
            useSatsumaAssemblyRequirements = false;
            satsumaElectrical = null;
            UseDefaultRequirements();
            InvalidateCache();
        }

        public void ConfigureSatsumaRequirements(SatsumaElectricalSystem electrical)
        {
            satsumaElectrical = electrical != null ? electrical :
                throw new ArgumentNullException(nameof(electrical));
            useSatsumaAssemblyRequirements = true;
            InvalidateCache();
        }

        public void ConfigureRequirements(
            string[] enginePartIds,
            string[] starterPartIds,
            string[] batteryPartIds,
            string[] fuelPartIds,
            string[] drivetrainPartIds,
            string[] drivenWheelPartIds)
        {
            requiredEnginePartIds = enginePartIds ?? Array.Empty<string>();
            requiredStarterPartIds = starterPartIds ?? Array.Empty<string>();
            requiredBatteryPartIds = batteryPartIds ?? Array.Empty<string>();
            requiredFuelPartIds = fuelPartIds ?? Array.Empty<string>();
            requiredDrivetrainPartIds = drivetrainPartIds ?? Array.Empty<string>();
            requiredDrivenWheelPartIds = drivenWheelPartIds ?? Array.Empty<string>();
            InvalidateCache();
        }

        public void SetPrototypeAvailability(
            bool hasFuel,
            bool hasOil,
            bool hasCoolant,
            float voltage)
        {
            fuelAvailable = hasFuel;
            oilAvailable = hasOil;
            coolantAvailable = hasCoolant;
            batteryVoltage = Mathf.Max(0f, voltage);
        }

        /// <summary>
        /// Updates only the electrical feed exposed to the simulation. The
        /// Satsuma electrical graph owns this value after authoring; fluids
        /// remain independent prerequisites.
        /// </summary>
        public void SetBatteryVoltage(float voltage)
        {
            batteryVoltage = Mathf.Max(0f, voltage);
        }

        public void Evaluate(in VehicleInputState input, ref VehicleSimulationPrerequisites result)
        {
            RefreshAssemblyCacheIfRequired();
            result.Reset();
            result.Add(cachedAssemblyFailures);

            if (useSatsumaAssemblyRequirements)
            {
                result.UseIndependentCrankingRequirements();
                if (satsumaElectrical == null || !satsumaElectrical.StarterCircuitReady)
                    result.Add(VehicleSimulationPrerequisiteFailure.StarterMissing);
                if (!CanSatsumaAlternatorCharge())
                    result.Add(VehicleSimulationPrerequisiteFailure.AlternatorUnavailable);
                // Conditions live on purchased wrappers and can change without
                // an assembly mutation. Never cache their wear as graph state.
                if (!cachedSatsumaStructuralCombustionReady || assemblyController == null ||
                    !SatsumaEngineAssemblyReadiness.HasFiringPairs(
                        SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(assemblyController.Graph)))
                    result.Add(VehicleSimulationPrerequisiteFailure.CombustionUnavailable);
            }

            if (!simulationOwnsOperatingFluids && !fuelAvailable && !IgnoreFluidReadinessForTesting && !IgnoreFuelReadinessForTesting)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.FuelUnavailable);
            }

            if (!simulationOwnsOperatingFluids && !oilAvailable && !IgnoreFluidReadinessForTesting)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.OilUnavailable);
            }

            if (!simulationOwnsOperatingFluids && !coolantAvailable && !IgnoreFluidReadinessForTesting)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.CoolantUnavailable);
            }

            if (batteryVoltage < minimumCrankingVoltage)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.BatteryVoltageLow);
            }

            if (!input.IgnitionOn)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.IgnitionOff);
            }
        }

        public void InvalidateCache()
        {
            cachedGraphMutationCount = int.MinValue;
        }

        private bool CanSatsumaAlternatorCharge()
        {
            AssemblyGraph graph = assemblyController != null ? assemblyController.Graph : null;
            if (graph == null || satsumaElectrical == null || !satsumaElectrical.IsBatteryInstalled ||
                !graph.IsPartDefinitionInstalled("vehicle.satsuma.part.alternator") ||
                !satsumaElectrical.IsConnectionInstalled(SatsumaElectricalConnection.Alternator) ||
                !satsumaElectrical.IsConnectionInstalled(SatsumaElectricalConnection.RegulatorHarness) ||
                !graph.TryGetMount(SatsumaConsumableAssemblyRules.BeltMountId, out MountPointRuntime belt) ||
                !belt.IsOccupied)
                return false;
            IAssemblyItemCondition condition = belt.InstalledPart.GetComponent<IAssemblyItemCondition>();
            // Donor belt Wear>99 removes it from the charging chain. Item
            // condition is remaining health (100-Wear), so require at least1
            // even if the item's explicit broken flag has not caught up yet.
            return condition != null && !condition.IsBroken &&
                float.IsFinite(condition.ConditionPercent) && condition.ConditionPercent >= 1f;
        }

        private void RefreshAssemblyCacheIfRequired()
        {
            if (assemblyController == null)
            {
                cachedAssemblyFailures =
                    VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable;
                cachedGraphMutationCount = int.MinValue;
                return;
            }

            assemblyController.Initialize();
            int mutationCount = assemblyController.GraphMutationCount;
            if (mutationCount == cachedGraphMutationCount)
            {
                return;
            }

            AssemblyGraph graph = assemblyController.Graph;
            VehicleSimulationPrerequisiteFailure failures =
                VehicleSimulationPrerequisiteFailure.None;

            if (useSatsumaAssemblyRequirements)
            {
                if (!graph.IsPartDefinitionBolted(SatsumaEngineAssemblyReadiness.BlockId))
                    failures |= VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing;
                if (!graph.IsPartDefinitionInstalled(SatsumaElectricalSystem.StarterPartDefinitionId))
                    failures |= VehicleSimulationPrerequisiteFailure.StarterMissing;
                if (!graph.IsPartDefinitionInstalled(SatsumaElectricalSystem.BatteryPartDefinitionId))
                    failures |= VehicleSimulationPrerequisiteFailure.BatteryMissing;
                if (!SatsumaEngineAssemblyReadiness.IsStockFuelDeliveryReady(graph))
                    failures |= VehicleSimulationPrerequisiteFailure.FuelUnavailable;
                if (!AreAllInstalled(graph, SatsumaDrivetrainParts))
                    failures |= VehicleSimulationPrerequisiteFailure.DrivetrainMissing;
                for (int i = 0; i < SatsumaDrivenMountIds.Length; i++)
                {
                    if (!graph.TryGetMount(SatsumaDrivenMountIds[i], out MountPointRuntime wheel) || !wheel.IsOccupied)
                        failures |= VehicleSimulationPrerequisiteFailure.DrivenWheelsMissing;
                    else if (!wheel.FastenerGroup.IsBolted)
                        failures |= VehicleSimulationPrerequisiteFailure.DrivenWheelsUnsecured;
                }
                cachedSatsumaStructuralCombustionReady = SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(graph);
                cachedAssemblyFailures = failures;
                cachedGraphMutationCount = mutationCount;
                return;
            }

            if (!AreAllInstalledAndSecured(graph, requiredEnginePartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing;
            }

            if (!AreAllInstalledAndSecured(graph, requiredStarterPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.StarterMissing;
            }

            if (!AreAllInstalledAndSecured(graph, requiredBatteryPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.BatteryMissing;
            }

            if (!AreAllInstalledAndSecured(graph, requiredFuelPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.FuelUnavailable;
            }

            if (!AreAllInstalledAndSecured(graph, requiredDrivetrainPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.DrivetrainMissing;
            }

            if (!AreAllInstalled(graph, requiredDrivenWheelPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.DrivenWheelsMissing;
            }
            else if (!AreAllSecured(graph, requiredDrivenWheelPartIds))
            {
                failures |= VehicleSimulationPrerequisiteFailure.DrivenWheelsUnsecured;
            }

            cachedAssemblyFailures = failures;
            cachedGraphMutationCount = mutationCount;
        }

        private static bool AreAllInstalledAndSecured(AssemblyGraph graph, string[] definitionIds)
        {
            return AreAllInstalled(graph, definitionIds) && AreAllSecured(graph, definitionIds);
        }

        private static bool AreAllInstalled(AssemblyGraph graph, string[] definitionIds)
        {
            if (definitionIds == null || definitionIds.Length == 0)
            {
                return false;
            }

            for (int requirementIndex = 0; requirementIndex < definitionIds.Length; requirementIndex++)
            {
                if (!TryFindInstalledPart(graph, definitionIds[requirementIndex], out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreAllSecured(AssemblyGraph graph, string[] definitionIds)
        {
            if (definitionIds == null || definitionIds.Length == 0)
            {
                return false;
            }

            for (int requirementIndex = 0; requirementIndex < definitionIds.Length; requirementIndex++)
            {
                if (!TryFindInstalledPart(
                        graph,
                        definitionIds[requirementIndex],
                        out PartInstance part))
                {
                    return false;
                }

                if (part.IsAssemblyRoot)
                {
                    continue;
                }

                MountPointRuntime mount = graph.FindMountForPart(part);
                if (mount == null)
                {
                    return false;
                }

                FastenerInstance[] fasteners = mount.Fasteners;
                for (int fastenerIndex = 0; fastenerIndex < fasteners.Length; fastenerIndex++)
                {
                    FastenerInstance fastener = fasteners[fastenerIndex];
                    if (fastener == null || fastener.Definition == null)
                    {
                        return false;
                    }

                    if (fastener.Definition.RequiredForRemoval &&
                        fastener.State != FastenerState.Tightened)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryFindInstalledPart(
            AssemblyGraph graph,
            string definitionId,
            out PartInstance part)
        {
            PartInstance[] parts = graph.AllRuntimeParts;
            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance candidate = parts[index];
                if (candidate != null && candidate.Definition != null && candidate.IsInstalled &&
                    string.Equals(
                        candidate.Definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    part = candidate;
                    return true;
                }
            }

            part = null;
            return false;
        }

        private void UseDefaultRequirements()
        {
            requiredEnginePartIds = DefaultEngineParts;
            requiredStarterPartIds = DefaultStarterParts;
            requiredBatteryPartIds = DefaultBatteryParts;
            requiredFuelPartIds = DefaultFuelParts;
            requiredDrivetrainPartIds = DefaultDrivetrainParts;
            requiredDrivenWheelPartIds = DefaultDrivenWheelParts;
        }
    }
}
