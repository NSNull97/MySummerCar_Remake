namespace MSC.Editor.VehicleAssembly
{
    public static class VehicleAssemblyPrototypePaths
    {
        public const string BuilderVersion = "1.1.0";
        public const string ContentRoot = "Assets/Game/Vehicle/Content/Assembly";
        public const string PartDefinitions = ContentRoot + "/Definitions/Parts";
        public const string MountDefinitions = ContentRoot + "/Definitions/Mounts";
        public const string FastenerDefinitions = ContentRoot + "/Definitions/Fasteners";
        public const string ToolDefinitions = ContentRoot + "/Definitions/Tools";
        public const string Materials = ContentRoot + "/Materials";
        public const string Prefabs = ContentRoot + "/Prefabs";
        public const string Scenes = ContentRoot + "/Scenes";
        public const string PrototypeScene = Scenes + "/VehicleAssemblyPrototype.unity";
        public const string ValidationReport = "Docs/Vehicle/ASSEMBLY_VALIDATION_REPORT.md";
        public const string PlayerPrefab = "Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab";
        public const string NeutralLightingPrefab =
            "Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_Neutral.prefab";
        public const string ReauthoredDrumPrefab =
            "Assets/Game/Vehicle/Content/Proof/ReauthoredRearBrakeDrum.prefab";
    }
}
