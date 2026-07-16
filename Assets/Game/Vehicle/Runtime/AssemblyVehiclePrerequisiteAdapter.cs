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
        IVehicleSimulationPrerequisiteSource
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

        private int cachedGraphMutationCount = int.MinValue;
        private VehicleSimulationPrerequisiteFailure cachedAssemblyFailures =
            VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable;

        public VehicleAssemblyController AssemblyController => assemblyController;

        public int CachedGraphMutationCount => cachedGraphMutationCount;

        public VehicleSimulationPrerequisiteFailure CachedAssemblyFailures => cachedAssemblyFailures;

        public void Configure(VehicleAssemblyController controller)
        {
            assemblyController = controller;
            UseDefaultRequirements();
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

        public void Evaluate(in VehicleInputState input, ref VehicleSimulationPrerequisites result)
        {
            RefreshAssemblyCacheIfRequired();
            result.Reset();
            result.Add(cachedAssemblyFailures);

            if (!fuelAvailable)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.FuelUnavailable);
            }

            if (!oilAvailable)
            {
                result.Add(VehicleSimulationPrerequisiteFailure.OilUnavailable);
            }

            if (!coolantAvailable)
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
            PartInstance[] parts = graph.Parts;
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
