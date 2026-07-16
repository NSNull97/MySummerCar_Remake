namespace MSC.Editor.VehicleSimulation
{
    public static class VehicleSimulationPrototypePaths
    {
        public const string BuilderVersion = "1.1.0";
        public const string MenuRoot = "Tools/MSC Remake/Vehicle Simulation/";

        public const string ContentRoot = "Assets/Game/Vehicle/Content/Simulation";
        public const string Configurations = ContentRoot + "/Configurations";
        public const string AssemblyRoot = ContentRoot + "/Assembly";
        public const string PartDefinitions = AssemblyRoot + "/Definitions/Parts";
        public const string MountDefinitions = AssemblyRoot + "/Definitions/Mounts";
        public const string FastenerDefinitions = AssemblyRoot + "/Definitions/Fasteners";
        public const string ToolDefinitions = AssemblyRoot + "/Definitions/Tools";
        public const string AssemblyPrefabs = AssemblyRoot + "/Prefabs";
        public const string Materials = ContentRoot + "/Materials";
        public const string Input = ContentRoot + "/Input";
        public const string Scenes = ContentRoot + "/Scenes";

        public const string PrototypeConfig = Configurations + "/M06_VehicleSimulationConfig.asset";
        public const string PrototypeInputActions = Input + "/M06_Vehicle.inputactions";
        public const string PrototypeScene = Scenes + "/VehicleSimulationPrototype.unity";

        public const string BootstrapScene = "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string PilotProductionCell =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity";
        public const string NextProductionCell =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity";

        public const string TelemetryDirectory = "VehicleTelemetry/Milestone06";
        public const string PerformanceReport = "Docs/Vehicle/M06_SIMULATION_PERFORMANCE.json";
    }
}
