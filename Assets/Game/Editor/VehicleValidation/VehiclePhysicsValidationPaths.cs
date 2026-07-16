using MSC.Vehicle.Simulation;

namespace MSC.Editor.VehicleValidation
{
    public static class VehiclePhysicsValidationPaths
    {
        public const string BuilderVersion = VehiclePhysicsValidationProtocol.ProfileRevision;
        public const string MenuRoot = "Tools/MSC Remake/Vehicle Physics Validation/";

        public const string ContentRoot = "Assets/Game/Vehicle/Content/Validation";
        public const string Profiles = ContentRoot + "/Resources";
        public const string Scenes = ContentRoot + "/Scenes";
        public const string Materials = ContentRoot + "/Materials";
        public const string Profile = Profiles + "/M06A_VehicleCalibrationProfile.asset";
        public const string Scene = Scenes + "/VehiclePhysicsValidation.unity";

        public const string DocumentationRoot = "Docs/VehicleValidation";
        public const string PerformanceEvidence =
            DocumentationRoot + "/M06A_PHYSICS_VALIDATION_PERFORMANCE.json";
        public const string PhysXEvidence =
            DocumentationRoot + "/M06A_PHYSX_RUN_EVIDENCE.json";
    }
}
