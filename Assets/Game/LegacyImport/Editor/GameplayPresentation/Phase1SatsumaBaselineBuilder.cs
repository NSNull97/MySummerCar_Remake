using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Core.Identity;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using MSC.Weather.Production;
using NWH.WheelController3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaBaselineBuilder
    {
        public const string BuilderVersion = "11A-V1d.66";
        public const string StableVehicleId = "323d9fece916469ea30c705ebfcf68df";
        public const string LockedSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";

        private const string ConfigurationPath = "Config/DonorPaths.local.json";
        private const int DonorBroadBodyCollisionLayer = 17;
        private const int DonorDetailedBodyCollisionLayer = 22;
        private const int DonorPlayerOnlyCollisionLayer = 23;
        private const int DonorHingedObjectCollisionLayer = 9;
        private const int DonorIgnoreRaycastLayer = 2;
        private const long DonorBroadBodyColliderComponentId = 92119L;
        private const int ProjectPlayerCollisionLayer = 9;
        private const string SourceAssetsRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets";
        private const string SourceSceneRelativePath =
            SourceAssetsRelativePath + "/_Scenes/GAME.unity";
        private const string CanonicalGeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        // Unity 6 treats dotted sibling folder names inconsistently during
        // AssetDatabase rename/move. Keep the transactional roots extensionless
        // so the validated build can be promoted atomically.
        private const string StagingGeneratedRoot =
            CanonicalGeneratedRoot + "_Staging";
        private static string activeGeneratedRoot = CanonicalGeneratedRoot;
        private static string GeneratedRoot => activeGeneratedRoot;
        private static string MeshRoot => GeneratedRoot + "/Meshes";
        private static string TextureRoot => GeneratedRoot + "/Textures";
        private static string MaterialRoot => GeneratedRoot + "/Materials";
        private static string RustPackedDetailTexturePath =>
            TextureRoot + "/rust_detail_hdrp.png";
        private static string RustPackedMaskTexturePath =>
            TextureRoot + "/rust_mask_hdrp.png";
        private static string RimRustBaseColorTexturePath =>
            TextureRoot + "/rim_rust_base_color_hdrp.png";
        private static string RimRustPackedDetailTexturePath =>
            TextureRoot + "/rim_rust_detail_hdrp.png";
        private static string RimRustPackedMaskTexturePath =>
            TextureRoot + "/rim_rust_mask_hdrp.png";
        private static string GlassRainNeutralDetailTexturePath =>
            TextureRoot + "/glass_rain_neutral_detail.png";
        private const string DonorRustPaintMaterialSourceGuid =
            "be77a835916639d47b5dfdf770515a88";
        private const string DonorRustDetailAlbedoSourceGuid =
            "1fd5c7fdea46d2445b3cee8c4827e0e6";
        private const string DonorRustDetailNormalSourceGuid =
            "5222f649e28596b4497761262c7cbaea";
        private const string DonorRustDetailMaskSourceGuid =
            "b00415adc363ca74eb2b1b1b75b61932";
        private const string DonorRustSpecGlossSourceGuid =
            "4b981dacef514ba42b1120fba88f9c0c";
        private const string DonorRimRustMaterialSourceGuid =
            "8dfb833f348cbed4bb4a2993a4bc797f";
        private const string DonorRimMetallicMaterialSourceGuid =
            "a1c2918870a9f154c95ff2a5abfae8be";
        private const string DonorRimAoSourceGuid =
            "d45a9b87d024f7f4ab83022e28f28db5";
        private const string DonorRimRustPatchesSourceGuid =
            "185f797705cd07a48bbeec0a7ab9e36d";
        private const string DonorRimRustNormalSourceGuid =
            "c872e1ac53d1f444c8dc66167fd6c1b8";
        private const string DonorRimSpecGlossSourceGuid =
            "5c58d7ab0f2101b44a2a4baa242b4ec2";
        private const string DonorRegularPaintMaterialSourceGuid =
            "a7920469770f2054e95e0192995f5c8e";
        private const string DonorMetallicPaintMaterialSourceGuid =
            "45f05d54331fec44fbb1d7e0d8471b04";
        private const string DonorMattePaintMaterialSourceGuid =
            "9346388ac807d004eb03249a37391bbf";
        private const string DonorArtPaintMaterialSourceGuid =
            "91fd2f13517790a419b6b956916bae5c";
        private const string DonorGtPaintMaterialSourceGuid =
            "8c04c51e133e58e4ca4dff80deaa221e";
        private const string DonorGt2PaintMaterialSourceGuid =
            "7c5406a46edef5c4dbe430e832debea0";
        private const string DonorWindshieldGlassMaterialSourceGuid =
            "ac664fa7a2ad68d4ca5eca5c8b6c02cf";
        private const string DonorCabinGlassMaterialSourceGuid =
            "423931766c7a0b14ea1fa5c0f98790a4";
        private const string DonorGlassMainTextureSourceGuid =
            "21054c2230a0774449736507c8ae7506";
        private const string DonorStockTireMeshSourceGuid =
            "a57310273d2148c44a2cd90a2766bd56";
        private const string DonorStockTireColliderMeshSourceGuid =
            "97345147525d5584b9b9c2871b6a2127";
        private const string DonorStockTireMaterialSourceGuid =
            "385e2c95dfb52264aafc4c5b16fec253";
        private const string DonorFastenerMeshSourceGuid =
            "e711c8a15b1135c4089caad19b8f56e8";
        private const string DonorShortBoltMeshSourceGuid =
            "aec6c756751308a4d830708366ad5cdb";
        private const string DonorLongBoltMeshSourceGuid =
            "bd64aade39680ac43a380f1c62373e0b";
        private const string DonorFastenerMaterialSourceGuid =
            "98697bae08a8c114ba9774c487f2658d";
        private const string DonorStandardMetalMaterialSourceGuid =
            "ad2f7b6e8cc080845a7a7fd4264fbb83";
        private const string DonorHandbrakeRodMeshSourceGuid =
            "1114fb760fb4d7a4cac87438f8bdc8a9";
        private const string DonorHandbrakeRodMaterialSourceGuid =
            "244b34167b8e8de4992ebb466651484d";
        private const string DonorWiperTapMeshSourceGuid =
            "a40b23abe89f9b8469ef646fd52f0957";
        private const string DonorWiperRodMeshSourceGuid =
            "ca35edd514b81b44d9864911ad9f5d5c";
        private const string DonorWiringSwitchLightsMeshSourceGuid =
            "3f95a862b9f417d4d8a5a74cc3fa2127";
        private const string DonorWiringBatteryHarnessMeshSourceGuid =
            "32aa759866b501a4eac8558a23c3eea4";
        private const string DonorWiringIgnitionMeshSourceGuid =
            "cfe641defee778e40a43c7ed120df1f1";
        private const string DonorWiringBatteryPlusMeshSourceGuid =
            "c78646cea8d36a44bb7ae5837f51ab87";
        private const string DonorWiringBatteryMinusMeshSourceGuid =
            "1720722b21c41b5439f711a928c13821";
        // Frozen MeshFilter references, not an inference from a BoltPM name.
        // The legacy default is a nut; only these reviewed front markers change.
        private static readonly IReadOnlyDictionary<long, string>
            ReviewedFrontFastenerMeshSourceGuids = new Dictionary<long, string>
            {
                { 47815L, DonorFastenerMeshSourceGuid },
                { 53776L, DonorFastenerMeshSourceGuid },
                { 61168L, DonorFastenerMeshSourceGuid },
                { 41902L, DonorFastenerMeshSourceGuid },
                { 42496L, DonorFastenerMeshSourceGuid },
                { 50660L, DonorFastenerMeshSourceGuid },
                { 39160L, DonorShortBoltMeshSourceGuid },
                { 49488L, DonorShortBoltMeshSourceGuid },
                { 64879L, DonorShortBoltMeshSourceGuid },
                { 65179L, DonorShortBoltMeshSourceGuid },
                { 38251L, DonorShortBoltMeshSourceGuid },
                { 51736L, DonorShortBoltMeshSourceGuid },
                { 65041L, DonorShortBoltMeshSourceGuid },
                { 70471L, DonorShortBoltMeshSourceGuid },
                { 68946L, DonorShortBoltMeshSourceGuid },
                { 70485L, DonorShortBoltMeshSourceGuid },
                { 68709L, DonorShortBoltMeshSourceGuid },
                { 61842L, DonorShortBoltMeshSourceGuid },
                { 52063L, DonorShortBoltMeshSourceGuid },
                { 58500L, DonorShortBoltMeshSourceGuid },
                { 62435L, DonorShortBoltMeshSourceGuid },
                { 62595L, DonorShortBoltMeshSourceGuid },
                { 48657L, DonorShortBoltMeshSourceGuid },
                { 67681L, DonorShortBoltMeshSourceGuid },
                { 68239L, DonorShortBoltMeshSourceGuid },
                { 69913L, DonorShortBoltMeshSourceGuid },
                { 59140L, DonorLongBoltMeshSourceGuid },
                { 65219L, DonorLongBoltMeshSourceGuid },
                { 56492L, DonorLongBoltMeshSourceGuid },
                { 64309L, DonorLongBoltMeshSourceGuid },
            };
        // Frozen active stock-rear MeshFilter references. Springs have no
        // BoltPM children and the road-wheel lug markers intentionally retain
        // the default nut mesh, so this table covers only suspension and drums.
        private static readonly IReadOnlyDictionary<long, string>
            ReviewedRearSuspensionFastenerMeshSourceGuids =
                new Dictionary<long, string>
                {
                    { 63892L, DonorLongBoltMeshSourceGuid },
                    { 71971L, DonorLongBoltMeshSourceGuid },
                    { 39901L, DonorLongBoltMeshSourceGuid },
                    { 70647L, DonorLongBoltMeshSourceGuid },
                    { 51042L, DonorShortBoltMeshSourceGuid },
                    { 55634L, DonorShortBoltMeshSourceGuid },
                    { 53482L, DonorShortBoltMeshSourceGuid },
                    { 56872L, DonorShortBoltMeshSourceGuid },
                    { 48747L, DonorShortBoltMeshSourceGuid },
                    { 63595L, DonorShortBoltMeshSourceGuid },
                    { 58968L, DonorFastenerMeshSourceGuid },
                    { 51409L, DonorFastenerMeshSourceGuid },
                };
        private const string ProjectWheelTirePresentationName =
            "Project Wheel Tire Presentation";
        private const string ProjectWheelTireColliderName =
            "Project Wheel Tire Collider";
        private const float DonorWheelRimRadiusMeters = 0.163018f;
        private const float DonorWheelRimWidthMeters = 0.148608f;
        private const float DonorStockTireRadiusMeters = 0.272667f;
        private const float DonorStockTireWidthMeters = 0.15057f;
        private const float DonorRearDrumContactRadiusMeters = 0.0871f;
        private const float DonorRearDrumContactWidthMeters = 0.08f;
        // All currently accepted stock and GT wheels use the donor wheel_regula
        // branch. The owner transforms retain the captured offset-seat poses, so
        // Pivot1 is recovered by moving the front seats 43 mm and rear seats
        // 40 mm inward along each mirrored mount's local X axis.
        private const float DonorRegularFrontWheelMountLocalXOffsetMeters =
            -0.043f;
        private const float DonorRegularRearWheelMountLocalXOffsetMeters =
            -0.040f;
        private static string LoosePartPresentationRoot =>
            GeneratedRoot + "/LoosePartPresentations";
        private static string LoosePartDefinitionRoot =>
            GeneratedRoot + "/LoosePartDefinitions";
        private static string MountDefinitionRoot =>
            GeneratedRoot + "/MountDefinitions";
        private static string FastenerDefinitionRoot =>
            GeneratedRoot + "/FastenerDefinitions";
        private static string ToolDefinitionRoot =>
            GeneratedRoot + "/ToolDefinitions";
        private static string PrefabRoot =>
            GeneratedRoot + "/Resources/Phase1Vehicles";
        private static string PresentationPrefabPath =>
            GeneratedRoot + "/Satsuma_BodyPresentation.prefab";
        private static string RootPartDefinitionPath =>
            GeneratedRoot + "/Satsuma_ChassisPartDefinition.asset";
        public const string RuntimePrefabPath =
            CanonicalGeneratedRoot +
            "/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab";
        private static string ActiveRuntimePrefabPath =>
            PrefabRoot + "/Satsuma_Phase1_V1a.prefab";
        public const string CompatibilityCatalogPath =
            "Assets/Game/Vehicle/Content/Phase1/Satsuma/" +
            "SatsumaMailOrderCompatibility.asset";
        public const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1aManifest.json";
        public const string MountCandidateAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cMountCandidateAudit.csv";
        public const string MountPoseAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cMountPoseAudit.csv";
        public const string AssemblyFsmAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cAssemblyFsmAudit.csv";
        public const string FastenerAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cFastenerAudit.csv";
        public const string BoltCheckAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1dBoltCheckAudit.csv";
        public const string HingeJointAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cHingeJointAudit.csv";
        public const string HingedPartFsmAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cHingedPartFsmAudit.csv";
        public const string HingedAssemblyConfigurationAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1cHingedAssemblyConfigurationAudit.csv";
        public const string LoosePartAssemblyFsmAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1dLoosePartAssemblyFsmAudit.csv";
        public const string LoosePartFastenerAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1dLoosePartFastenerAudit.csv";
        public const string EngineMountTriangulationAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaV1dEngineMountTriangulationAudit.csv";
        private const string SimulationConfigPath =
            "Assets/Game/Vehicle/Content/Simulation/Configurations/" +
            "M06_VehicleSimulationConfig.asset";
        private const string InputActionsPath =
            "Assets/Game/Vehicle/Content/Simulation/Input/M06_Vehicle.inputactions";

        private const string SatsumaRootName = "SATSUMA(557kg, 248)";
        private const string CarPartsRootName = "CARPARTS";
        private static readonly string[] CarPartGroupNames =
        {
            "PartsCar",
            "PartsMotor",
            "PartsGT",
            "PartsExtra",
        };
        private static readonly Vector3 ProjectSpawnPosition =
            new Vector3(153.74904f, 1.611f, -1029.251f);
        private static readonly Quaternion ProjectSpawnRotation =
            new Quaternion(0f, 1f, 0f, -0.0000001629f);

        private static readonly string[] ExcludedBodyPathTokens =
        {
            "/shadow_body",
            "/BrokenPivot/",
            "/black windows(xxxxx)",
            "/TaxStickerSatsuma",
            "/ClearGlass/Sticker",
        };

        private static readonly ColliderSpec[] ChassisColliders =
        {
            Collider("collider_floor", "67f3aaeaa94459e45a97961ab48a9e13"),
            Collider("collider_roofright", "43bc851a25580b34fa93723d6eaac950"),
            Collider("collider_fender_in_left", "ba48a95da2b8e5f428ccdc5a011603fb"),
            Collider("collider_wheelwell_right", "f18e7b1e67a2907418f4cf0472451074"),
            Collider("collider_rear", "68c5fc6453472cb4dbc4b05640569a71"),
            Collider("collider_front", "a42b37d9a5337da488128a1ad1ab7e4e"),
            Collider("collider_firewall_right", "72b2cdcddf3d47545a41ef9f1ff9e952"),
            Collider("collider_floor2", "ad8c832f4de19a84287c6f4479cd1dbd"),
            Collider("collider_firewall", "bd6e7aa4d2f04c3448ee895e62c40f6c"),
            Collider("collider_rear_left", "b05cc7cdfabacfb4f8c79fb9b257a59f"),
            Collider("collider_pillar_left", "def19b12e40786147a4b8532a90109a6"),
            Collider("collider_rearwindow", "8a2bec9e0ecc9c14a911e498faa9b6b9"),
            Collider("collider_roof", "a036f8bff732632498866a1c0fda6ee4"),
            Collider("collider_left", "f47f635231fa8f74e95a660815a507e9"),
            Collider("collider_rear_right", "626210b0be6e704419b0e8d4d9256025"),
            Collider("collider_floor3", "6fced4532db64dc45ac69cff5068620f"),
            Collider("collider_firewall_left", "2077cd2fd16d6984b9bc1367dbe1e589"),
            Collider("collider_pillar_right", "5a79d206c6d84fe46940bc36229a0957"),
            Collider("collider_roofleft", "b51d0ac570042b84ea8550dc93a48dac"),
            Collider("collider_fender_in_right", "c0fa2f1a697a46f4ea3a9754265e809d"),
            Collider("collider_right", "849aea00d1659b749adafd79341d3bb3"),
            Collider("collider_wheelwell_left", "49c95dcfea3ed99478864ccb58472154"),
        };

        private static readonly Vector3[] WheelAnchors =
        {
            new Vector3(-0.6299995f, 0f, 1.1669996f),
            new Vector3(0.6300007f, 0f, 1.1669996f),
            new Vector3(-0.6029993f, 0f, -1.167f),
            new Vector3(0.603001f, 0f, -1.1669996f),
        };

        // Frozen donor no-spring Wheel carrier height. Runtime stock/long
        // stages replace it before solving rear contact.
        private const float DonorRearDrumCenterLocalY = -0.1500003f;

        private static readonly string[] WheelIds =
        {
            "vehicle.satsuma.wheel.fl",
            "vehicle.satsuma.wheel.fr",
            "vehicle.satsuma.wheel.rl",
            "vehicle.satsuma.wheel.rr",
        };

        // Neutral car-local mount transforms from the locked donor scene. The
        // trailing-arm mount stores the donor visible-child orientation folded
        // into the loose part root; rear Wheel contact drives its IK-equivalent
        // rotation around the donor body-side pivot.
        private static readonly ReviewedRearSuspensionPose[]
            ReviewedRearSuspensionPoses =
            {
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.trail-arm-rl",
                    new Vector3(-0.42300016f, -0.2153f, -0.85700095f),
                    new Quaternion(
                        0.000000030908627f,
                        0.7071068f,
                        0.70710677f,
                        -0.000000030908623f)),
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.trail-arm-rr",
                    new Vector3(0.4230005f, -0.2153f, -0.85700065f),
                    new Quaternion(
                        0.00000003090862f,
                        0.7071068f,
                        0.70710677f,
                        -0.000000030908613f)),
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.coilspring-rl",
                    new Vector3(-0.4300001f, -0.079f, -0.98564017f),
                    new Quaternion(
                        0.116225995f,
                        -0.006005722f,
                        0.007918469f,
                        0.99317306f)),
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.coilspring-rr",
                    new Vector3(0.4300005f, -0.079f, -0.9850009f),
                    new Quaternion(
                        0.11622601f,
                        -0.0060060006f,
                        0.007918001f,
                        0.99317306f)),
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.shock-rl",
                    new Vector3(-0.47f, 0.14f, -1.1500002f),
                    new Quaternion(
                        -0.7071069f,
                        0f,
                        0.00000006181726f,
                        0.7071067f)),
                new ReviewedRearSuspensionPose(
                    "mount.satsuma.shock-rr",
                    new Vector3(0.46999958f, 0.1400002f, -1.1499999f),
                    new Quaternion(
                        -0.7071069f,
                        -0.000000030908623f,
                        0.00000003090862f,
                        0.7071067f)),
            };

        // Runtime matrices captured read-only from the licensed donor while a
        // complete rear corner was assembled. The donor does not use four
        // unrelated static props here: the drum and the spring/shock lower
        // anchors follow the trailing arm. Keep those invariant arm-local
        // relations instead of reusing the loose-parts/staging transforms.
        private static readonly RearSuspensionRuntimeRigPose[]
            RearSuspensionRuntimeRigPoses =
            {
                new RearSuspensionRuntimeRigPose(
                    "rl",
                    new Vector3(
                        0.18000037f,
                        -0.31490782f,
                        -0.000143628f),
                    new Quaternion(
                        -0.76663f,
                        -0.0000022593f,
                        0.00000218644f,
                        0.642089f),
                    new Vector3(
                        -0.42999992f,
                        -0.07900001f,
                        -0.98563987f),
                    new Quaternion(
                        0.07793739f,
                        -0.08643087f,
                        0.70787865f,
                        0.69668025f),
                    new Vector3(
                        0.005048308f,
                        -0.15492851f,
                        0.025472417f),
                    new Quaternion(
                        -0.56166166f,
                        -0.5477373f,
                        -0.4375173f,
                        0.43942988f),
                    new Vector3(
                        -0.4700004f,
                        0.13999999f,
                        -1.1499993f),
                    new Quaternion(
                        -0.7552422f,
                        0.004838879f,
                        -0.004858736f,
                        0.65540993f),
                    new Vector3(
                        0.051999438f,
                        -0.23999995f,
                        -0.027999982f),
                    new Quaternion(
                        -0.013967162f,
                        -0.005849807f,
                        -0.000980103f,
                        0.9998849f)),
                new RearSuspensionRuntimeRigPose(
                    "rr",
                    new Vector3(
                        -0.1799964f,
                        -0.31491333f,
                        -0.000137855f),
                    new Quaternion(
                        0.00000466596f,
                        -0.766646f,
                        -0.64207f,
                        0.00000411877f),
                    new Vector3(
                        0.42999977f,
                        -0.078999996f,
                        -0.9849998f),
                    new Quaternion(
                        0.0779372f,
                        -0.086431086f,
                        0.70787835f,
                        0.6966805f),
                    new Vector3(
                        -0.008948238f,
                        -0.15428959f,
                        0.025472542f),
                    new Quaternion(
                        0.56166154f,
                        0.5477374f,
                        0.43751764f,
                        -0.43942952f),
                    new Vector3(
                        0.4700002f,
                        0.1400002f,
                        -1.1499999f),
                    new Quaternion(
                        -0.75524294f,
                        -0.004848789f,
                        0.004851726f,
                        0.6554091f),
                    new Vector3(
                        -0.0519993f,
                        -0.23999984f,
                        -0.028000052f),
                    new Quaternion(
                        -0.013980586f,
                        0.005852222f,
                        0.000989631f,
                        0.99988484f)),
            };

        // Final installed wheel roots captured from the licensed donor and
        // expressed relative to the moving visible trailing-arm roots. The
        // donor handoff triggers are chassis-space helpers only; using them as
        // final sockets leaves both rear wheels behind when the arm moves and
        // mirrors the right wheel incorrectly.
        private static readonly ReviewedRearRoadWheelPose[]
            ReviewedRearRoadWheelPoses =
            {
                new ReviewedRearRoadWheelPose(
                    "mount.satsuma.wheelrl-new",
                    "vehicle.satsuma.part.trail-arm-rl",
                    new Vector3(
                        0.21999949f,
                        -0.31319135f,
                        -0.00074551045f),
                    new Quaternion(
                        -0.6341037f,
                        0.00000061712484f,
                        -0.00000042188907f,
                        -0.77324796f)),
                new ReviewedRearRoadWheelPose(
                    "mount.satsuma.wheelrr-new",
                    "vehicle.satsuma.part.trail-arm-rr",
                    new Vector3(
                        -0.22000039f,
                        -0.31318748f,
                        -0.0005988565f),
                    new Quaternion(
                        -0.00000050477684f,
                        -0.752298f,
                        -0.65882295f,
                        -0.000001705379f)),
            };

        // Read-only runtime capture from the licensed donor after both front
        // corners were assembled, the left side was fully tightened and the
        // save was reloaded. The donor's wheelFL/wheelFR objects are fixed
        // steering carriers; their child Spindle transforms are the moving
        // physical hub centres. NWH owns that hub pose in the remake and this
        // table preserves every donor-local relation hanging from it.
        private static readonly FrontSuspensionRuntimeRigPose[]
            FrontSuspensionRuntimeRigPoses =
            {
                new FrontSuspensionRuntimeRigPose(
                    "fl",
                    true,
                    new Vector3(-0.6299995f, -0.25873545f, 1.1669996f),
                    new Quaternion(0f, 0f, -0.012217001f, 0.9999254f),
                    new Vector3(-0.6299995f, -0.35f, 1.1669996f),
                    Quaternion.identity,
                    new Vector3(-0.3200001f, -0.2754303f, 1.1670004f),
                    new Quaternion(
                        0.037008166f,
                        0.70613766f,
                        0.7061379f,
                        0.03700602f),
                    new Vector3(0.05f, -0.075f, 0.0080001f),
                    new Vector3(0.05f, 0f, 0.0020769f),
                    new Quaternion(0f, 0.7071068f, 0.7071068f, 0f),
                    new Vector3(
                        0.10708289f,
                        0.13889302f,
                        -0.005999684f),
                    new Quaternion(
                        0.7368282f,
                        0.67517865f,
                        -0.02357779f,
                        0.025730578f),
                    new Vector3(-0.4854795f, 0.30649278f, 1.1305116f),
                    new Quaternion(
                        0.04881538f,
                        0.9981031f,
                        -0.037471f,
                        0.0018324745f),
                    "susp_fl_shock",
                    new Bounds(
                        new Vector3(
                            -0.24558218f,
                            -0.0009179334f,
                            0.04358541f),
                        new Vector3(
                            0.60093087f,
                            0.3660085f,
                            0.38038272f)),
                    "mount.satsuma.wheelfl-new",
                    new Vector3(
                        -0.248897165f,
                        -0.191638887f,
                        0.992956042f),
                    new Quaternion(
                        -1.81724288E-10f,
                        -0.9876181f,
                        -9.145738E-09f,
                        0.156877875f),
                    new Quaternion(
                        -0.7052109f,
                        -0.0517460741f,
                        -0.0517461225f,
                        0.7052108f),
                    new Quaternion(
                        5.33866782E-08f,
                        0.7071069f,
                        -0.707106769f,
                        -5.33866746E-08f),
                    // pivot_steering_fl is under OFFSET (+0.05 m), not
                    // directly under the hub. Preserve the complete chain.
                    new Vector3(
                        0.11290412f,
                        0.0357951969f,
                        -0.117504239f),
                    new Quaternion(
                        0.995174944f,
                        -0.03834642f,
                        -0.08854294f,
                        -0.0177913625f),
                    "susp_fl_steer_rubber_001",
                    new Bounds(
                        new Vector3(
                            -0.1452306f,
                            -0.000186990947f,
                            5.85000962E-05f),
                        new Vector3(
                            0.31345722f,
                            0.05442406f,
                            0.0533770472f)),
                    new Vector3(0.05f, 0f, 0f),
                    new Quaternion(
                        2.8E-07f,
                        5.2E-08f,
                        -1f,
                        2.25E-06f),
                    new Vector3(-0.043f, 0f, 0f),
                    new Quaternion(0f, 1f, 0f, -4.371139E-08f)),
                new FrontSuspensionRuntimeRigPose(
                    "fr",
                    false,
                    new Vector3(0.6300007f, -0.25873807f, 1.1669996f),
                    new Quaternion(0f, 0f, 0.012217001f, 0.9999254f),
                    new Vector3(0.6300007f, -0.35f, 1.167f),
                    Quaternion.identity,
                    new Vector3(0.31999928f, -0.27543026f, 1.1669999f),
                    new Quaternion(
                        -0.03700906f,
                        0.7061378f,
                        0.7061378f,
                        -0.037005287f),
                    new Vector3(-0.05f, -0.075f, 0.0080001f),
                    new Vector3(-0.05f, 0f, 0.0020769f),
                    new Quaternion(0f, 0.7071068f, 0.7071068f, 0f),
                    new Vector3(
                        -0.10708146f,
                        0.1386766f,
                        -0.0060004f),
                    new Quaternion(
                        0.67517865f,
                        0.73682827f,
                        -0.025730638f,
                        0.023577726f),
                    new Vector3(0.48548f, 0.306493f, 1.130512f),
                    new Quaternion(
                        -0.04881539f,
                        0.9981031f,
                        -0.03747099f,
                        -0.0018326808f),
                    "susp_fr_shock",
                    new Bounds(
                        new Vector3(
                            -0.24618334f,
                            -0.005957458f,
                            0.02812137f),
                        new Vector3(
                            0.594651f,
                            0.37117377f,
                            0.34872043f)),
                    "mount.satsuma.wheelfr-new",
                    new Vector3(0.2488971f, -0.191639f, 0.992956f),
                    new Quaternion(
                        1.772997E-09f,
                        0.026623724f,
                        -3.50203755E-10f,
                        0.9996456f),
                    new Quaternion(
                        -0.7032283f,
                        -0.07396009f,
                        -0.07396014f,
                        0.7032282f),
                    new Quaternion(
                        5.33866782E-08f,
                        0.7071069f,
                        -0.707106769f,
                        -5.33866746E-08f),
                    // Mirrored OFFSET contributes -0.05 m on the right.
                    new Vector3(
                        -0.11423402f,
                        0.0334164947f,
                        -0.1176604f),
                    new Quaternion(
                        0.08457585f,
                        -0.0481333323f,
                        -0.993975163f,
                        0.0504336357f),
                    "susp_fl_steer_rubber_001",
                    new Bounds(
                        new Vector3(
                            -0.1452306f,
                            -0.000186990947f,
                            5.85000962E-05f),
                        new Vector3(
                            0.31345722f,
                            0.05442406f,
                            0.0533770472f)),
                    new Vector3(-0.05f, 0f, 0f),
                    new Quaternion(
                        0.7071068f,
                        0f,
                        0f,
                        -0.7071068f),
                    new Vector3(0.043f, 0f, 0f),
                    Quaternion.identity),
            };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Build V1d Baseline")]
        public static void Build()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string sourceAssetsRoot = Combine(
                paths.DonorStagingDirectory,
                SourceAssetsRelativePath);
            string scenePath = Combine(
                paths.DonorStagingDirectory,
                SourceSceneRelativePath);
            RequireHash(scenePath, LockedSceneSha256);

            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);
            DonorTransformRecord satsumaRoot =
                scene.GetUniqueTransformByPath(SatsumaRootName);
            DonorTransformRecord bodyRoot = scene.GetUniqueDirectChildByName(
                satsumaRoot.TransformId,
                "Body");
            DonorTransformRecord collidersRoot = scene.GetUniqueDirectChildByName(
                satsumaRoot.TransformId,
                "Colliders");
            DonorTransformRecord carPartsRoot =
                scene.GetUniqueTransformByPath(CarPartsRootName);

            DonorColliderRecord[] chassisColliders = scene
                .GetCollidersBelowIncludingInactive(
                    collidersRoot.TransformId,
                    includeTriggers: false)
                .Where(value => value.Enabled)
                .OrderBy(value => value.ComponentId)
                .ToArray();
            if (chassisColliders.Length != 29 ||
                chassisColliders.Count(value =>
                    value.Kind == DonorColliderKind.Mesh) != 23 ||
                chassisColliders.Any(value =>
                    value.Kind == DonorColliderKind.Mesh && !value.Convex))
            {
                throw new InvalidDataException(
                    "Locked donor Satsuma chassis collision contract changed: " +
                    $"total={chassisColliders.Length}, " +
                    $"mesh={chassisColliders.Count(value => value.Kind == DonorColliderKind.Mesh)}, " +
                    $"nonConvexMesh={chassisColliders.Count(value => value.Kind == DonorColliderKind.Mesh && !value.Convex)}.");
            }

            DonorColliderRecord[] broadBodyColliders = chassisColliders
                .Where(value =>
                    scene.GetGameObjectLayer(value.GameObjectId) ==
                    DonorBroadBodyCollisionLayer)
                .ToArray();
            if (broadBodyColliders.Length != 1 ||
                broadBodyColliders[0].ComponentId !=
                DonorBroadBodyColliderComponentId ||
                chassisColliders.Count(value =>
                    scene.GetGameObjectLayer(value.GameObjectId) ==
                    DonorDetailedBodyCollisionLayer) != 21 ||
                chassisColliders.Count(value =>
                    scene.GetGameObjectLayer(value.GameObjectId) ==
                    DonorPlayerOnlyCollisionLayer) != 4 ||
                chassisColliders.Count(value =>
                    scene.GetGameObjectLayer(value.GameObjectId) ==
                    DonorHingedObjectCollisionLayer) != 2 ||
                chassisColliders.Count(value =>
                    scene.GetGameObjectLayer(value.GameObjectId) ==
                    DonorIgnoreRaycastLayer) != 1)
            {
                throw new InvalidDataException(
                    "Locked donor Satsuma collider layer roles changed; " +
                    "refusing to flatten an unknown collision contract.");
            }

            DonorStaticRendererRecord[] bodyRenderers = scene
                .GetActiveStaticRenderersBelow(new[] { bodyRoot.TransformId })
                .Where(renderer => !ShouldExcludeBodyRenderer(scene, renderer))
                .OrderBy(renderer => renderer.ComponentId)
                .ToArray();
            if (bodyRenderers.Length < 7 || bodyRenderers.Length > 12)
            {
                throw new InvalidDataException(
                    $"Expected 7-12 sanitized Satsuma shell renderers; found {bodyRenderers.Length}.");
            }

            LoosePartSource[] looseParts = DiscoverLooseParts(
                scene,
                carPartsRoot);
            if (looseParts.Length < 100)
            {
                throw new InvalidDataException(
                    $"Expected at least 100 donor CARPARTS roots; found {looseParts.Length}.");
            }
            InstalledMountCandidate[] mountCandidates =
                DiscoverInstalledMountCandidates(
                    scene,
                    satsumaRoot,
                    looseParts);
            WriteMountCandidateAudit(scene, looseParts, mountCandidates);
            MeshDerivedMountPose[] mountPoses = DiscoverMeshDerivedMountPoses(
                scene,
                satsumaRoot,
                looseParts);
            WriteMountPoseAudit(scene, mountPoses);
            DonorAssemblyFsmEvidence[] assemblyFsmEvidence =
                DiscoverAssemblyFsmEvidence(scene, satsumaRoot);
            WriteAssemblyFsmAudit(scene, assemblyFsmEvidence);
            DonorFastenerEvidence[] fastenerEvidence =
                DiscoverFastenerEvidence(scene, assemblyFsmEvidence);
            WriteFastenerAudit(scene, fastenerEvidence);
            DonorBoltCheckEvidence[] boltCheckEvidence =
                DiscoverBoltCheckEvidence(scene, satsumaRoot, looseParts);
            WriteBoltCheckAudit(scene, boltCheckEvidence);
            WriteHingeJointAudit(scene, satsumaRoot);
            WriteHingedPartFsmAudit(scene, looseParts);
            WriteHingedAssemblyConfigurationAudit(
                scene,
                satsumaRoot,
                looseParts,
                assemblyFsmEvidence);
            DonorAssemblyFsmEvidence[] loosePartAssemblyEvidence =
                DiscoverAssemblyFsmEvidence(scene, carPartsRoot);
            WriteLoosePartAssemblyFsmAudit(
                scene,
                looseParts,
                loosePartAssemblyEvidence);
            DonorFastenerEvidence[] loosePartFastenerEvidence =
                DiscoverFastenerEvidence(
                    scene,
                    loosePartAssemblyEvidence);
            WriteFastenerAudit(
                scene,
                loosePartFastenerEvidence,
                LoosePartFastenerAuditPath);

            activeGeneratedRoot = StagingGeneratedRoot;
            bool promoted = false;
            try
            {
                DeleteGeneratedRoot();
                EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(MountDefinitionRoot);
            EnsureFolder(FastenerDefinitionRoot);
            EnsureFolder(ToolDefinitionRoot);
            EnsureFolder(CompatibilityCatalogPath.Substring(
                0,
                CompatibilityCatalogPath.LastIndexOf('/')));

            Dictionary<string, string> sourceByGuid =
                BuildGuidIndex(sourceAssetsRoot);
            string[] meshGuids = bodyRenderers
                .Select(value => value.MeshGuid)
                .Concat(chassisColliders
                    .Where(value =>
                        value.Kind == DonorColliderKind.Mesh &&
                        !string.IsNullOrWhiteSpace(value.MeshGuid))
                    .Select(value => value.MeshGuid))
                .Concat(looseParts.SelectMany(value =>
                    value.Renderers.Select(renderer => renderer.MeshGuid)))
                .Concat(looseParts.SelectMany(value =>
                    value.Colliders
                        .Where(collider =>
                            collider.Kind == DonorColliderKind.Mesh &&
                            !string.IsNullOrWhiteSpace(collider.MeshGuid) &&
                            !IsUnityBuiltInGuid(collider.MeshGuid))
                        .Select(collider => collider.MeshGuid)))
                .Concat(BuildSatsumaElectricalDonorConnectionDefinitions()
                    .Select(value => value.MeshGuid))
                .Concat(new[]
                {
                    DonorStockTireMeshSourceGuid,
                    DonorStockTireColliderMeshSourceGuid,
                    DonorFastenerMeshSourceGuid,
                    DonorShortBoltMeshSourceGuid,
                    DonorLongBoltMeshSourceGuid,
                    DonorHandbrakeRodMeshSourceGuid,
                    DonorWiperTapMeshSourceGuid,
                    DonorWiperRodMeshSourceGuid,
                    DonorWiringBatteryPlusMeshSourceGuid,
                    DonorWiringBatteryMinusMeshSourceGuid,
                    Phase1SatsumaIgnitionAuthoring.KeyMeshGuid,
                    Phase1SatsumaIgnitionAuthoring.SocketMeshGuid,
                })
                .Where(value => !IsUnityBuiltInGuid(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] materialGuids = bodyRenderers
                .SelectMany(value => value.MaterialGuids)
                .Concat(looseParts.SelectMany(value =>
                    value.Renderers.SelectMany(renderer =>
                        renderer.MaterialGuids)))
                .Concat(new[]
                {
                    DonorRustPaintMaterialSourceGuid,
                    DonorRegularPaintMaterialSourceGuid,
                    DonorMetallicPaintMaterialSourceGuid,
                    DonorMattePaintMaterialSourceGuid,
                    DonorArtPaintMaterialSourceGuid,
                    DonorGtPaintMaterialSourceGuid,
                    DonorGt2PaintMaterialSourceGuid,
                    DonorRimRustMaterialSourceGuid,
                    DonorRimMetallicMaterialSourceGuid,
                    DonorStockTireMaterialSourceGuid,
                    DonorFastenerMaterialSourceGuid,
                    DonorStandardMetalMaterialSourceGuid,
                    DonorHandbrakeRodMaterialSourceGuid,
                    Phase1SatsumaIgnitionAuthoring.AtlasMaterialGuid,
                })
                .Where(value => !IsUnityBuiltInGuid(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var materialSpecs = materialGuids.ToDictionary(
                guid => guid,
                guid => ReadMaterialSpec(
                    guid,
                    RequireGuidPath(sourceByGuid, guid, "material")),
                StringComparer.OrdinalIgnoreCase);
            string[] textureGuids = materialSpecs.Values
                .Select(value => value.MainTextureGuid)
                .Concat(new[]
                {
                    DonorRustDetailAlbedoSourceGuid,
                    DonorRustDetailNormalSourceGuid,
                    DonorRustDetailMaskSourceGuid,
                    DonorRustSpecGlossSourceGuid,
                    DonorRimAoSourceGuid,
                    DonorRimRustPatchesSourceGuid,
                    DonorRimRustNormalSourceGuid,
                    DonorRimSpecGlossSourceGuid,
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Dictionary<string, string> importedMeshes = ImportAssets(
                meshGuids,
                sourceByGuid,
                MeshRoot,
                "satsuma-mesh");
            Dictionary<string, string> importedTextures = ImportAssets(
                textureGuids,
                sourceByGuid,
                TextureRoot,
                "satsuma-texture");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildRustHdrpTextures(sourceByGuid);
            BuildRimRustHdrpTextures(sourceByGuid);
            BuildGlassRainNeutralDetailTexture(sourceByGuid);

            Dictionary<string, Material> materials = materialSpecs.ToDictionary(
                pair => pair.Key,
                pair => CreateMaterial(
                    pair.Key,
                    pair.Value,
                    importedTextures),
                StringComparer.OrdinalIgnoreCase);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GameObject presentationPrefab = BuildPresentationPrefab(
                scene,
                satsumaRoot.TransformId,
                bodyRenderers,
                importedMeshes,
                materials);
            PartDefinition chassisDefinition = BuildChassisDefinition(
                presentationPrefab);
            LoosePartBuild[] loosePartBuilds = BuildLoosePartAssets(
                scene,
                looseParts,
                importedMeshes,
                materials,
                mountCandidates);
            MountBuild[] mountBuilds = BuildMountAssets(
                scene,
                satsumaRoot,
                loosePartBuilds,
                mountCandidates,
                assemblyFsmEvidence,
                fastenerEvidence,
                loosePartAssemblyEvidence,
                loosePartFastenerEvidence);
            mountBuilds = LockReviewedRearSuspensionMountPoses(
                mountBuilds,
                loosePartBuilds);
            mountBuilds = LockReviewedFrontSuspensionMountPoses(mountBuilds);
            ConfigureInterchangeableRoadWheels(
                mountBuilds,
                loosePartBuilds);
            mountBuilds = ApplyReviewedAssemblyFastenerCoverage(mountBuilds);
            ApplyReviewedFrontConnectionFasteners(scene, mountBuilds);
            ApplyReviewedFrontFastenerMeshes(scene, mountBuilds);
            ApplyReviewedRearSuspensionFastenerMeshes(scene, mountBuilds);
            ApplyReviewedHandbrakeFastenerMeshes(scene, mountBuilds);
            ApplyReviewedEngineFastenerMeshes(scene, mountBuilds);
            ApplyAdditionalEngineFastenerMeshes(scene, mountBuilds);
            ConfigureFastenerGroups(mountBuilds, boltCheckEvidence);
            ValidateReferencedBoltCoverage(
                mountBuilds,
                loosePartBuilds,
                assemblyFsmEvidence,
                loosePartAssemblyEvidence);
            ConfigureSatsumaMountSequence(mountBuilds);
            ConfigureSatsumaFrontMountSequence(mountBuilds);
            ConfigureSatsumaRemovalChecks(mountBuilds);
            Phase1SatsumaEngineAssemblyRules.ApplyEngineAccessRules(
                mountBuilds.Select(value => value.Definition).ToArray());
            Phase1SatsumaEngineCompoundAssemblyRules.ApplyEngineCompoundRules(
                mountBuilds.Select(value => value.Definition).ToArray());
            Phase1SatsumaEngineCompoundAssemblyRules.ApplyEngineToCarRules(
                mountBuilds.Select(value => value.Definition).ToArray());
            Phase1SatsumaCockpitRules.ApplyDashboardMeterLatch(mountBuilds.Single(value =>
                value.Definition.DefinitionId ==
                Phase1SatsumaCockpitRules.DashboardMeterMountId).Definition);
            Phase1SatsumaCockpitRules.ApplySteeringWheelRules(mountBuilds.Single(value =>
                value.Definition.DefinitionId ==
                Phase1SatsumaCockpitRules.SteeringWheelMountId).Definition);
            ToolDefinition[] toolDefinitions = BuildToolAssets(mountBuilds);
            AssemblyDependency[] dependencies = BuildVerifiedAssemblyDependencies(
                scene,
                loosePartBuilds,
                assemblyFsmEvidence
                    .Concat(loosePartAssemblyEvidence)
                    .ToArray());
            dependencies = Phase1SatsumaEngineCompoundAssemblyRules.FilterEngineDependencies(dependencies);
            GameObject runtimePrefab = BuildRuntimePrefab(
                scene,
                satsumaRoot,
                chassisColliders,
                presentationPrefab,
                chassisDefinition,
                importedMeshes,
                materials,
                bodyRenderers.Length,
                loosePartBuilds,
                mountBuilds,
                toolDefinitions,
                dependencies);
            VehicleDeliveredPartCompatibilityCatalog compatibility =
                BuildCompatibilityCatalog();
            AssetDatabase.SaveAssets();
            ValidateBuiltContent(
                runtimePrefab,
                presentationPrefab,
                compatibility,
                bodyRenderers.Length,
                loosePartBuilds,
                mountBuilds);
            WriteManifest(
                scene,
                satsumaRoot,
                bodyRenderers,
                meshGuids,
                materialGuids,
                textureGuids,
                compatibility,
                loosePartBuilds,
                chassisColliders.Length);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
                PromoteGeneratedRoot();
                promoted = true;
                Debug.Log(
                    $"PHASE1_SATSUMA_BUILD_OK version={BuilderVersion} " +
                $"renderers={bodyRenderers.Length} colliders={chassisColliders.Length} " +
                $"looseParts={loosePartBuilds.Length} " +
                $"activeLooseParts={loosePartBuilds.Count(value => IsLoosePartActiveAtNewGame(value.Source))} " +
                $"mounts={mountBuilds.Length} " +
                $"runtimeFasteners={mountBuilds.Sum(value => value.Fasteners.Count)} " +
                $"hingedMounts={mountBuilds.Count(value => value.Hinge.HasValue)} " +
                $"ownedMounts={mountBuilds.Count(value => !string.IsNullOrEmpty(value.OwnerPartDefinitionId))} " +
                $"uniqueMountCandidates={mountCandidates.Count(value => value.IsUnique)} " +
                $"uniqueMeshMountPoses={mountPoses.Count(value => value.IsUnique)} " +
                $"assemblyFsms={assemblyFsmEvidence.Length} " +
                $"fastenerMarkers={fastenerEvidence.Length} " +
                    $"mailOrderMappings={compatibility.Entries.Count} prefab={RuntimePrefabPath}");
            }
            finally
            {
                activeGeneratedRoot = CanonicalGeneratedRoot;
                if (!promoted &&
                    AssetDatabase.IsValidFolder(StagingGeneratedRoot))
                {
                    AssetDatabase.DeleteAsset(StagingGeneratedRoot);
                }
            }
        }

        public static void RunBatch()
        {
            Build();
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Front Installation Rules")]
        public static void RefreshFrontInstallationRulesBatch()
        {
            // Reuse the already generated, hash-locked baseline. In particular,
            // do not parse GAME, rebuild presentation or rewrite the full-build
            // manifest just to migrate a small set of authored mechanical rules.
            ManifestDto manifest = JsonUtility.FromJson<ManifestDto>(
                File.ReadAllText(ToFileSystemPath(ManifestPath)));
            if (manifest == null || manifest.sourceSceneSha256 != LockedSceneSha256 ||
                manifest.stableVehicleId != StableVehicleId ||
                manifest.runtimePrefabPath != RuntimePrefabPath ||
                manifest.builderVersion != "11A-V1d.64" &&
                manifest.builderVersion != "11A-V1d.65" &&
                manifest.builderVersion != BuilderVersion)
            {
                throw new InvalidDataException(
                    "Front rules refresh requires the existing V64/V65/V66 locked Satsuma baseline.");
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            try
            {
                MountPointDefinition[] changed = RefreshFrontInstallationRules(
                    contents, out int removedDependencies);
                if (removedDependencies > 0 && PrefabUtility.SaveAsPrefabAsset(
                        contents, RuntimePrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the scoped front dependency refresh.");
                }

                foreach (MountPointDefinition definition in changed)
                {
                    AssetDatabase.SaveAssetIfDirty(definition);
                }

                Debug.Log("PHASE1_SATSUMA_FRONT_RULES_REFRESH_OK version=" +
                    BuilderVersion + " fullRebuild=false manifestUnchanged=true" +
                    " changedMountDefinitions=" + changed.Length +
                    " removedLegacyDependencies=" + removedDependencies +
                    " sourceSceneSha256=" + LockedSceneSha256);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static MountPointDefinition[] RefreshFrontInstallationRules(
            GameObject prefabContents,
            out int removedDependencies)
        {
            removedDependencies = 0;
            VehicleAssemblyController assembly = prefabContents != null
                ? prefabContents.GetComponent<VehicleAssemblyController>() : null;
            if (assembly == null || assembly.MountPoints.Any(value => value == null) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(
                    assembly.MountPoints.Select(value => value.MountId)))
            {
                throw new InvalidDataException(
                    "Front rules refresh requires the reviewed base or additive Satsuma mount roster.");
            }

            string[] ids = new[] { "sub-frame", "steering-rack", "steering-column" }
                .Concat(new[] { "fl", "fr" }.SelectMany(corner =>
                    new[] { "wishbone", "spindle", "strut", "steering-rod", "discbrake", "halfshaft" }
                        .Select(slug => slug + "-" + corner)))
                .Select(slug => "mount.satsuma." + slug).ToArray();
            MountBuild[] current = ids.Select(id =>
            {
                MountPointAuthoring authoring = assembly.MountPoints.SingleOrDefault(
                    value => value != null && value.MountId == id);
                MountPointDefinition definition = authoring != null ? authoring.Definition : null;
                if (definition == null || definition.DefinitionId != id ||
                    definition.OwnerPartDefinitionId != "vehicle.satsuma.part.body-shell" ||
                    AssetDatabase.GetAssetPath(definition) !=
                    CanonicalGeneratedRoot + "/MountDefinitions/" + id + ".asset")
                {
                    throw new InvalidDataException(
                        "Unexpected front definition binding during scoped refresh: " + id);
                }

                return ExistingMountDefinition(definition);
            }).ToArray();
            var obsolete = new HashSet<string>(StringComparer.Ordinal);
            foreach (string corner in new[] { "fl", "fr" })
            {
                obsolete.Add(BuildDependencyKey(AssemblyDependency.Create(
                    "vehicle.satsuma.part.spindle-" + corner,
                    "vehicle.satsuma.part.wishbone-" + corner,
                    AssemblyDependencyKind.InstallRequiresBolted)));
                obsolete.Add(BuildDependencyKey(AssemblyDependency.Create(
                    "vehicle.satsuma.part.strut-" + corner,
                    "vehicle.satsuma.part.spindle-" + corner,
                    AssemblyDependencyKind.InstallRequiresBolted)));
                obsolete.Add(BuildDependencyKey(InstallRequires(
                    "steering-rod-" + corner, "spindle-" + corner)));
            }

            string[] keys = assembly.Dependencies.Select(BuildDependencyKey).ToArray();
            int oldCount = keys.Count(obsolete.Contains);
            // The later, independently scoped engine migration removes exactly
            // four false-positive clutch/chain edges. Accept either complete
            // generation, never a partially migrated or count-only lookalike.
            int legacyEngineCount = assembly.Dependencies.Length -
                Phase1SatsumaEngineCompoundAssemblyRules.FilterEngineDependencies(
                    assembly.Dependencies).Length;
            int migratedFrontCount = legacyEngineCount == 0 ? 30 : 34;
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
                oldCount != 0 && oldCount != 6 ||
                legacyEngineCount != 0 && legacyEngineCount != 4 ||
                keys.Length != migratedFrontCount + oldCount)
            {
                throw new InvalidDataException(
                    "Front rules refresh expected 40/34 unique dependencies before the engine migration, " +
                    "or 36/30 with all four reviewed engine edges absent.");
            }

            // Validate all sizes/stages and old-or-new rule shapes on disposable
            // definitions before dirtying even the first real asset.
            var expected = new List<MountBuild>();
            try
            {
                foreach (MountBuild build in current)
                {
                    MountPointDefinition copy = UnityEngine.Object.Instantiate(build.Definition);
                    copy.name = build.Definition.name;
                    expected.Add(ExistingMountDefinition(copy));
                }

                ConfigureFrontFastenerGroups(expected);
                ConfigureSatsumaFrontMountSequence(expected);
                for (int index = 0; index < current.Length; index++)
                {
                    ValidateFrontRefreshShape(current[index].Definition, expected[index].Definition);
                }

                string[] before = current.Select(value =>
                    EditorJsonUtility.ToJson(value.Definition)).ToArray();
                bool alreadyCurrent = current.Select((value, index) =>
                    before[index] == EditorJsonUtility.ToJson(expected[index].Definition)).All(value => value);
                if (!alreadyCurrent)
                {
                    ConfigureFrontFastenerGroups(current);
                    ConfigureSatsumaFrontMountSequence(current);
                }

                var serialized = new SerializedObject(assembly);
                SerializedProperty dependencies = serialized.FindProperty("dependencies");
                for (int index = keys.Length - 1; index >= 0; index--)
                {
                    if (obsolete.Contains(keys[index]))
                    {
                        dependencies.DeleteArrayElementAtIndex(index);
                        removedDependencies++;
                    }
                }

                if (removedDependencies > 0)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                return current.Where((value, index) => before[index] !=
                    EditorJsonUtility.ToJson(value.Definition)).Select(value => value.Definition).ToArray();
            }
            finally
            {
                foreach (MountBuild build in expected)
                {
                    UnityEngine.Object.DestroyImmediate(build.Definition);
                }
            }
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Suspension Removal Rules")]
        public static void RefreshSuspensionRemovalRulesBatch()
        {
            // This migration only updates the reviewed mount definitions. It
            // deliberately leaves the prefab, full-build manifest and donor
            // extraction untouched.
            ManifestDto manifest = JsonUtility.FromJson<ManifestDto>(
                File.ReadAllText(ToFileSystemPath(ManifestPath)));
            if (manifest == null || manifest.sourceSceneSha256 != LockedSceneSha256 ||
                manifest.stableVehicleId != StableVehicleId ||
                manifest.runtimePrefabPath != RuntimePrefabPath ||
                manifest.builderVersion != "11A-V1d.64" &&
                manifest.builderVersion != "11A-V1d.65" &&
                manifest.builderVersion != BuilderVersion)
            {
                throw new InvalidDataException(
                    "Removal rules refresh requires the existing V64/V65/V66 locked Satsuma baseline.");
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            try
            {
                MountPointDefinition[] changed = RefreshSuspensionRemovalRules(contents);
                foreach (MountPointDefinition definition in changed)
                {
                    AssetDatabase.SaveAssetIfDirty(definition);
                }

                Debug.Log("PHASE1_SATSUMA_REMOVAL_RULES_REFRESH_OK version=" +
                    BuilderVersion + " fullRebuild=false prefabUnchanged=true" +
                    " manifestUnchanged=true changedMountDefinitions=" + changed.Length +
                    " sourceSceneSha256=" + LockedSceneSha256);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        internal static MountPointDefinition[] RefreshSuspensionRemovalRules(
            GameObject prefabContents)
        {
            VehicleAssemblyController assembly = prefabContents != null
                ? prefabContents.GetComponent<VehicleAssemblyController>() : null;
            if (assembly == null || assembly.MountPoints.Any(value => value == null) ||
                !Phase1SatsumaReviewedGraphShape.HasReviewedMountRoster(
                    assembly.MountPoints.Select(value => value.MountId)))
            {
                throw new InvalidDataException(
                    "Removal rules refresh requires the reviewed base or additive Satsuma mount roster.");
            }

            string[] ids = new[] { "fl", "fr" }.SelectMany(corner => new[]
                {
                    "strut-" + corner,
                    "halfshaft-" + corner,
                    "discbrake-" + corner,
                    "spindle-" + corner,
                })
                .Concat(new[] { "rl", "rr" }.SelectMany(corner => new[]
                {
                    "trail-arm-" + corner,
                    "coilspring-" + corner,
                    "long-coilspring-" + corner,
                }))
                .Select(slug => "mount.satsuma." + slug).ToArray();
            MountBuild[] current = ids.Select(id =>
            {
                MountPointAuthoring authoring = assembly.MountPoints.SingleOrDefault(
                    value => value != null && value.MountId == id);
                MountPointDefinition definition = authoring != null ? authoring.Definition : null;
                if (definition == null || definition.DefinitionId != id ||
                    definition.OwnerPartDefinitionId != "vehicle.satsuma.part.body-shell" ||
                    AssetDatabase.GetAssetPath(definition) !=
                    CanonicalGeneratedRoot + "/MountDefinitions/" + id + ".asset")
                {
                    throw new InvalidDataException(
                        "Unexpected removal definition binding during scoped refresh: " + id);
                }

                return ExistingMountDefinition(definition);
            }).ToArray();

            var expected = new List<MountBuild>();
            try
            {
                foreach (MountBuild build in current)
                {
                    MountPointDefinition copy = UnityEngine.Object.Instantiate(build.Definition);
                    copy.name = build.Definition.name;
                    expected.Add(ExistingMountDefinition(copy));
                }

                ConfigureSatsumaRemovalChecks(expected);
                for (int index = 0; index < current.Length; index++)
                {
                    ValidateRemovalRefreshShape(current[index].Definition,
                        expected[index].Definition);
                }

                string[] before = current.Select(value =>
                    EditorJsonUtility.ToJson(value.Definition)).ToArray();
                bool alreadyCurrent = current.Select((value, index) =>
                    before[index] == EditorJsonUtility.ToJson(expected[index].Definition)).All(value => value);
                if (!alreadyCurrent)
                {
                    ConfigureSatsumaRemovalChecks(current);
                }

                return current.Where((value, index) => before[index] !=
                    EditorJsonUtility.ToJson(value.Definition)).Select(value => value.Definition).ToArray();
            }
            finally
            {
                foreach (MountBuild build in expected)
                {
                    UnityEngine.Object.DestroyImmediate(build.Definition);
                }
            }
        }

        private static void ValidateRemovalRefreshShape(
            MountPointDefinition current,
            MountPointDefinition expected)
        {
            string id = current.DefinitionId;
            bool legacy = current.RemovalBlockedWhileBoltedMountIds.Length == 0 &&
                current.RemovalIgnoredDependentMountIds.Length == 0;
            bool migrated = current.RemovalBlockedWhileOccupiedMountIds.SequenceEqual(
                    expected.RemovalBlockedWhileOccupiedMountIds) &&
                current.RemovalBlockedWhileBoltedMountIds.SequenceEqual(
                    expected.RemovalBlockedWhileBoltedMountIds) &&
                current.RemovalIgnoredDependentMountIds.SequenceEqual(
                    expected.RemovalIgnoredDependentMountIds);
            if (id.Contains("strut-", StringComparison.Ordinal))
            {
                legacy &= current.RemovalBlockedWhileOccupiedMountIds.Length == 0;
            }
            else if (id.Contains("coilspring-", StringComparison.Ordinal))
            {
                string corner = id.EndsWith("-rl", StringComparison.Ordinal) ? "rl" : "rr";
                legacy &= current.RemovalBlockedWhileOccupiedMountIds.Length == 0 ||
                    current.RemovalBlockedWhileOccupiedMountIds.SequenceEqual(
                        new[] { "mount.satsuma.shock-" + corner });
                string oppositeSpring = id.Contains("long-coilspring-", StringComparison.Ordinal)
                    ? "mount.satsuma.coilspring-" + corner
                    : "mount.satsuma.long-coilspring-" + corner;
                bool commonInstall = current.RequiredOccupiedMountIds.SequenceEqual(
                        new[] { "mount.satsuma.trail-arm-" + corner }) &&
                    current.RequiredAnyOccupiedMountIds.Length == 0 &&
                    current.RequiredBoltedMountIds.SequenceEqual(
                        new[] { "mount.satsuma.trail-arm-" + corner });
                legacy &= commonInstall && current.BlockedWhileOccupiedMountIds.SequenceEqual(
                    new[] { oppositeSpring });
                migrated &= commonInstall && current.BlockedWhileOccupiedMountIds.SequenceEqual(
                    new[] { oppositeSpring, "mount.satsuma.shock-" + corner });
            }
            else
            {
                legacy &= current.RemovalBlockedWhileOccupiedMountIds.Length == 0;
            }

            if (id.Contains("trail-arm-", StringComparison.Ordinal))
            {
                string corner = id.EndsWith("-rl", StringComparison.Ordinal) ? "rl" : "rr";
                bool legacyInstall = current.RequiredOccupiedMountIds.Length == 0 &&
                    current.RequiredAnyOccupiedMountIds.Length == 0 &&
                    current.BlockedWhileOccupiedMountIds.Length == 0;
                bool migratedInstall = current.RequiredOccupiedMountIds.Length == 0 &&
                    current.RequiredAnyOccupiedMountIds.Length == 0 &&
                    current.BlockedWhileOccupiedMountIds.SequenceEqual(
                        new[] { "mount.satsuma.shock-" + corner });
                legacy &= legacyInstall;
                migrated &= migratedInstall;
            }

            if ((!legacy && !migrated) ||
                current.RequiredBoltedMountIds.Length != expected.RequiredBoltedMountIds.Length ||
                !current.RequiredBoltedMountIds.SequenceEqual(expected.RequiredBoltedMountIds) ||
                !current.RequiredAnyBoltedMountIds.SequenceEqual(expected.RequiredAnyBoltedMountIds))
            {
                throw new InvalidDataException(
                    "Removal rules refresh refuses an unexpected old/new rule shape: " + id);
            }
        }

        private static MountBuild ExistingMountDefinition(MountPointDefinition definition) =>
            new MountBuild(null, definition, Vector3.zero, Quaternion.identity,
                Vector3.one, 0L, Array.Empty<FastenerBuild>());

        private static void ValidateFrontRefreshShape(
            MountPointDefinition current, MountPointDefinition expected)
        {
            string id = current.DefinitionId;
            string corner = id.EndsWith("-fl", StringComparison.Ordinal) ? "fl" : "fr";
            string[] oldRequired = expected.RequiredOccupiedMountIds;
            if (id.Contains("discbrake-"))
            {
                oldRequired = new[] { "mount.satsuma.spindle-" + corner, "mount.satsuma.strut-" + corner };
            }
            else if (id.Contains("halfshaft-") || id.Contains("steering-rod-") ||
                     id == "mount.satsuma.steering-rack" || id == "mount.satsuma.steering-column")
            {
                oldRequired = Array.Empty<string>();
            }

            FastenerGroupDefinition group = current.FastenerGroup;
            FastenerGroupDefinition expectedGroup = expected.FastenerGroup;
            int oldOn = id.Contains("wishbone-") || id.Contains("spindle-") ? 2 :
                id.Contains("steering-rod-") ? 8 : 1;
            bool legacy = group != null && group.BoltedOnThreshold == oldOn &&
                current.RequiredOccupiedMountIds.SequenceEqual(oldRequired) &&
                string.IsNullOrEmpty(current.InstallAttemptBoltedSupportMountId) &&
                current.InstallationBlockedWhileBoltedMountIds.Length == 0;
            bool migrated = group != null && group.BoltedOnThreshold == expectedGroup.BoltedOnThreshold &&
                current.RequiredOccupiedMountIds.SequenceEqual(expected.RequiredOccupiedMountIds) &&
                current.InstallAttemptBoltedSupportMountId == expected.InstallAttemptBoltedSupportMountId &&
                current.InstallationBlockedWhileBoltedMountIds.SequenceEqual(expected.InstallationBlockedWhileBoltedMountIds);
            if ((!legacy && !migrated) || group == null ||
                group.AggregateMaximumTightness != expectedGroup.AggregateMaximumTightness ||
                !group.FastenerDefinitionIds.SequenceEqual(expectedGroup.FastenerDefinitionIds) ||
                group.BoltedOffThreshold != 0 ||
                group.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None ||
                group.BreakAction != FastenerBreakAction.None ||
                group.LooseBreakSpeedKph != 0f || group.PartialCheckSpeedKph != 0f ||
                group.ChanceDivisor != 100f ||
                current.RequiredAnyOccupiedMountIds.Length != 0 ||
                current.BlockedWhileOccupiedMountIds.Length != 0 ||
                !HasSupportedFrontRemovalShape(current) ||
                current.RequiredBoltedMountIds.Length != 0 ||
                current.RequiredAnyBoltedMountIds.Length != 0)
            {
                throw new InvalidDataException(
                    "Front rules refresh refuses an unexpected old/new rule shape: " + id);
            }
        }

        private static bool HasSupportedFrontRemovalShape(
            MountPointDefinition definition)
        {
            string id = definition.DefinitionId;
            string corner = id.EndsWith("-fl", StringComparison.Ordinal) ? "fl" : "fr";
            string[] occupied = definition.RemovalBlockedWhileOccupiedMountIds;
            string[] bolted = definition.RemovalBlockedWhileBoltedMountIds;
            string[] ignored = definition.RemovalIgnoredDependentMountIds;
            bool allLegacy = occupied.Length == 0 && bolted.Length == 0 && ignored.Length == 0;
            if (allLegacy)
            {
                return true;
            }

            if (id.Contains("strut-", StringComparison.Ordinal))
            {
                return occupied.SequenceEqual(new[] { "mount.satsuma.steering-rod-" + corner }) &&
                    bolted.Length == 0 && ignored.Length == 0;
            }

            if (id.Contains("halfshaft-", StringComparison.Ordinal))
            {
                return occupied.Length == 0 &&
                    bolted.SequenceEqual(new[] { "mount.satsuma.discbrake-" + corner }) &&
                    ignored.Length == 0;
            }

            if (id.Contains("discbrake-", StringComparison.Ordinal))
            {
                return occupied.Length == 0 && bolted.Length == 0 &&
                    ignored.SequenceEqual(new[] { "mount.satsuma.halfshaft-" + corner });
            }

            if (id.Contains("spindle-", StringComparison.Ordinal))
            {
                return occupied.Length == 0 && bolted.Length == 0 &&
                    ignored.SequenceEqual(new[] { "mount.satsuma.discbrake-" + corner });
            }

            return false;
        }

        private static GameObject BuildPresentationPrefab(
            DonorUnitySceneModel scene,
            long satsumaRootTransformId,
            IReadOnlyList<DonorStaticRendererRecord> bodyRenderers,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("Satsuma Body Presentation");
            try
            {
                for (int index = 0; index < bodyRenderers.Count; index++)
                {
                    DonorStaticRendererRecord donor = bodyRenderers[index];
                    long transformId = scene.GetTransformIdForGameObject(
                        donor.GameObjectId);
                    scene.GetTransformRelativeTo(
                        transformId,
                        satsumaRootTransformId,
                        out Vector3 localPosition,
                        out Quaternion localRotation,
                        out Vector3 localScale);

                    var owner = new GameObject(
                        SanitizeName(scene.GetGameObjectName(donor.GameObjectId)) +
                        "_" + donor.ComponentId.ToString(CultureInfo.InvariantCulture));
                    owner.transform.SetParent(root.transform, false);
                    owner.transform.SetLocalPositionAndRotation(
                        localPosition,
                        localRotation);
                    owner.transform.localScale = localScale;
                    Mesh mesh = RequireAsset<Mesh>(
                        importedMeshes[donor.MeshGuid]);
                    owner.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = donor.MaterialGuids
                        .Select(guid => materials[guid])
                        .ToArray();
                    renderer.shadowCastingMode = donor.MaterialGuids.Any(
                        IsDonorGlassMaterialSourceGuid)
                            ? ShadowCastingMode.Off
                            : ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PresentationPrefabPath);
                return prefab ?? throw new InvalidOperationException(
                    "Could not save sanitized Satsuma presentation prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static PartDefinition BuildChassisDefinition(
            GameObject presentationPrefab)
        {
            var definition = ScriptableObject.CreateInstance<PartDefinition>();
            definition.Configure(
                "vehicle.satsuma.part.body-shell",
                "Satsuma body shell",
                PartCategory.Structural,
                389f,
                presentationPrefab,
                Array.Empty<PartCompatibilityRule>());
            AssetDatabase.CreateAsset(definition, RootPartDefinitionPath);
            return definition;
        }

        private static LoosePartSource[] DiscoverLooseParts(
            DonorUnitySceneModel scene,
            DonorTransformRecord carPartsRoot)
        {
            var sources = new List<LoosePartSource>();
            foreach (string groupName in CarPartGroupNames)
            {
                DonorTransformRecord group = scene.GetUniqueDirectChildByName(
                    carPartsRoot.TransformId,
                    groupName);
                foreach (DonorTransformRecord partRoot in scene.GetDirectChildren(
                             group.TransformId))
                {
                    string donorName = scene.GetGameObjectName(
                        partRoot.GameObjectId);
                    if (string.Equals(
                            groupName,
                            "PartsGT",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            donorName,
                            "WoodSheet",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    DonorStaticRendererRecord[] renderers = scene
                        .GetStaticRenderersBelowIncludingInactive(
                            partRoot.TransformId)
                        .Where(value => !IsUnityBuiltInGuid(value.MeshGuid))
                        .OrderBy(value => value.ComponentId)
                        .ToArray();
                    DonorColliderRecord[] colliders = scene
                        .GetCollidersBelowIncludingInactive(
                            partRoot.TransformId,
                            includeTriggers: false)
                        .ToArray();
                    scene.TryGetRigidbody(
                        partRoot.TransformId,
                        out DonorRigidbodyRecord rigidbody);
                    bool activeAtStart = scene.IsTransformActiveBelow(
                        partRoot.TransformId,
                        carPartsRoot.TransformId);
                    sources.Add(new LoosePartSource(
                        groupName,
                        donorName,
                        partRoot,
                        activeAtStart,
                        rigidbody,
                        renderers,
                        colliders));
                }
            }

            AssignStableDefinitionIds(sources);
            return sources
                .OrderBy(value => value.GroupName, StringComparer.Ordinal)
                .ThenBy(value => value.Root.TransformId)
                .ToArray();
        }

        private static void AssignStableDefinitionIds(
            IReadOnlyList<LoosePartSource> sources)
        {
            foreach (IGrouping<string, LoosePartSource> group in sources
                         .GroupBy(
                             value => GetBaseDefinitionSlug(
                                 value.GroupName,
                                 value.DonorName),
                             StringComparer.Ordinal))
            {
                LoosePartSource[] ordered = group
                    .OrderBy(value => value.Root.TransformId)
                    .ToArray();
                for (int index = 0; index < ordered.Length; index++)
                {
                    string suffix = ordered.Length > 1
                        ? "-" + (index + 1).ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty;
                    ordered[index].DefinitionId =
                        "vehicle.satsuma.part." + group.Key + suffix;
                }
            }
        }

        private static InstalledMountCandidate[] DiscoverInstalledMountCandidates(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartSource> looseParts)
        {
            var excludedRootNames = new HashSet<string>(
                new[]
                {
                    "Body",
                    "Colliders",
                    "PlayerTrigger",
                    "TrafficTrigger",
                    "FIRE",
                    "DeadBody",
                    "SuspensionDebug",
                },
                StringComparer.Ordinal);
            DonorTransformRecord[] installedTransforms = scene
                .GetDescendants(satsumaRoot.TransformId)
                .Where(value => !IsBelowExcludedSatsumaRoot(
                    scene,
                    satsumaRoot,
                    value,
                    excludedRootNames))
                .ToArray();
            var results = new List<InstalledMountCandidate>(looseParts.Count);
            foreach (LoosePartSource loose in looseParts)
            {
                string looseKey = NormalizeDonorPartIdentity(loose.DonorName);
                InstalledCandidateScore[] candidates = installedTransforms
                    .Select(transform => new InstalledCandidateScore(
                        transform,
                        ScoreInstalledCandidate(
                            scene,
                            looseKey,
                            transform)))
                    .Where(value => value.Score >= 100)
                    .OrderByDescending(value => value.Score)
                    .ThenBy(value => scene.GetTransformDepth(
                        value.Transform.TransformId))
                    .ThenBy(value => value.Transform.TransformId)
                    .ToArray();
                int topScore = candidates.Length > 0
                    ? candidates[0].Score
                    : 0;
                DonorTransformRecord[] top = candidates
                    .Where(value => value.Score == topScore)
                    .Select(value => value.Transform)
                    .ToArray();
                results.Add(new InstalledMountCandidate(
                    loose,
                    topScore,
                    top));
            }

            return results.ToArray();
        }

        private static bool IsBelowExcludedSatsumaRoot(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            DonorTransformRecord candidate,
            ISet<string> excludedRootNames)
        {
            DonorTransformRecord current = candidate;
            while (current.FatherTransformId != 0L &&
                   current.FatherTransformId != satsumaRoot.TransformId)
            {
                current = scene.GetTransform(current.FatherTransformId);
            }

            return current.FatherTransformId == satsumaRoot.TransformId &&
                   excludedRootNames.Contains(
                       scene.GetGameObjectName(current.GameObjectId));
        }

        private static int ScoreInstalledCandidate(
            DonorUnitySceneModel scene,
            string looseKey,
            DonorTransformRecord candidate)
        {
            string name = scene.GetGameObjectName(candidate.GameObjectId);
            if (name.StartsWith("trigger_", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "BoltPM", StringComparison.Ordinal) ||
                name.StartsWith("bolt", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string candidateKey = NormalizeDonorPartIdentity(name);
            if (string.IsNullOrEmpty(looseKey) || string.IsNullOrEmpty(candidateKey))
            {
                return 0;
            }

            if (string.Equals(looseKey, candidateKey, StringComparison.Ordinal))
            {
                return 300;
            }

            if (candidateKey.StartsWith(
                    looseKey + "-",
                    StringComparison.Ordinal) ||
                candidateKey.EndsWith(
                    "-" + looseKey,
                    StringComparison.Ordinal))
            {
                return 220;
            }

            if (candidateKey.Contains(looseKey, StringComparison.Ordinal) &&
                looseKey.Length >= 6)
            {
                return 180;
            }

            string singularLoose = Regex.Replace(
                looseKey,
                @"[1-4]$",
                string.Empty,
                RegexOptions.CultureInvariant);
            return singularLoose.Length >= 6 &&
                   candidateKey.Contains(singularLoose, StringComparison.Ordinal)
                ? 100
                : 0;
        }

        private static string NormalizeDonorPartIdentity(string value)
        {
            string normalized = Regex.Replace(
                    value ?? string.Empty,
                    @"\((?:Clone|x+|leftx|rightx?|left)\)$",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Trim()
                .ToLowerInvariant()
                .Replace("carburator", "carburetor");
            return Regex.Replace(
                    normalized,
                    @"[^a-z0-9]+",
                    "-",
                    RegexOptions.CultureInvariant)
                .Trim('-');
        }

        private static void WriteMountCandidateAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartSource> looseParts,
            IReadOnlyList<InstalledMountCandidate> candidates)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "Group,DonorLooseName,PartDefinitionId,LooseTransformId,Status,TopScore,CandidateCount,CandidateTransformIds,CandidateHierarchyPaths");
            foreach (InstalledMountCandidate candidate in candidates
                         .OrderBy(value => value.LoosePart.GroupName,
                             StringComparer.Ordinal)
                         .ThenBy(value => value.LoosePart.Root.TransformId))
            {
                string status = candidate.Candidates.Count switch
                {
                    0 => "Unresolved",
                    1 => "UniqueCandidatePendingBehavioralReview",
                    _ => "Ambiguous",
                };
                builder.Append(Csv(candidate.LoosePart.GroupName)).Append(',')
                    .Append(Csv(candidate.LoosePart.DonorName)).Append(',')
                    .Append(Csv(candidate.LoosePart.DefinitionId)).Append(',')
                    .Append(candidate.LoosePart.Root.TransformId).Append(',')
                    .Append(status).Append(',')
                    .Append(candidate.TopScore).Append(',')
                    .Append(candidate.Candidates.Count).Append(',')
                    .Append(Csv(string.Join(
                        ";",
                        candidate.Candidates.Select(value =>
                            value.TransformId.ToString(
                                CultureInfo.InvariantCulture)))))
                    .Append(',')
                    .Append(Csv(string.Join(
                        " | ",
                        candidate.Candidates.Select(value =>
                            scene.GetHierarchyPath(value.TransformId)))))
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(MountCandidateAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static MeshDerivedMountPose[] DiscoverMeshDerivedMountPoses(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartSource> looseParts)
        {
            Dictionary<string, int> loosePartUsageByMesh = looseParts
                .SelectMany(part => part.Renderers
                    .Select(renderer => new
                    {
                        renderer.MeshGuid,
                        PartTransformId = part.Root.TransformId,
                    }))
                .Where(value => !string.IsNullOrWhiteSpace(value.MeshGuid))
                .GroupBy(value => value.MeshGuid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(value => value.PartTransformId)
                        .Distinct()
                        .Count(),
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DonorStaticRendererRecord[]> installedByMesh = scene
                .GetStaticRenderersBelowIncludingInactive(satsumaRoot.TransformId)
                .Where(value =>
                    !IsUnityBuiltInGuid(value.MeshGuid) &&
                    !IsDonorPrimitiveMeshGuid(value.MeshGuid))
                .GroupBy(value => value.MeshGuid, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() <= 8)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(value => value.ComponentId).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var results = new List<MeshDerivedMountPose>(looseParts.Count);
            foreach (LoosePartSource loose in looseParts)
            {
                var evidence = new List<MeshMountPoseEvidence>();
                foreach (DonorStaticRendererRecord looseRenderer in loose.Renderers)
                {
                    if (IsDonorPrimitiveMeshGuid(looseRenderer.MeshGuid) ||
                        !loosePartUsageByMesh.TryGetValue(
                            looseRenderer.MeshGuid,
                            out int loosePartUsage) ||
                        loosePartUsage > 4)
                    {
                        continue;
                    }

                    if (!installedByMesh.TryGetValue(
                            looseRenderer.MeshGuid,
                            out DonorStaticRendererRecord[] installedMatches))
                    {
                        continue;
                    }

                    long looseRendererTransformId = scene
                        .GetTransformIdForGameObject(looseRenderer.GameObjectId);
                    Matrix4x4 looseRendererRelative = GetRelativeMatrix(
                        scene,
                        looseRendererTransformId,
                        loose.Root.TransformId);
                    foreach (DonorStaticRendererRecord installedRenderer in installedMatches)
                    {
                        long installedTransformId = scene
                            .GetTransformIdForGameObject(installedRenderer.GameObjectId);
                        Matrix4x4 installedRendererRelative = GetRelativeMatrix(
                            scene,
                            installedTransformId,
                            satsumaRoot.TransformId);
                        Matrix4x4 rootPose = installedRendererRelative *
                                             looseRendererRelative.inverse;
                        evidence.Add(new MeshMountPoseEvidence(
                            looseRenderer.ComponentId,
                            installedRenderer.ComponentId,
                            installedTransformId,
                            rootPose.MultiplyPoint3x4(Vector3.zero),
                            rootPose.rotation,
                            rootPose.lossyScale));
                    }
                }

                var clusters = new List<MeshMountPoseCluster>();
                foreach (MeshMountPoseEvidence item in evidence
                             .OrderBy(value => value.LooseRendererComponentId)
                             .ThenBy(value => value.InstalledRendererComponentId))
                {
                    MeshMountPoseCluster cluster = clusters.FirstOrDefault(value =>
                        value.Matches(item));
                    if (cluster == null)
                    {
                        cluster = new MeshMountPoseCluster(item);
                        clusters.Add(cluster);
                    }
                    else
                    {
                        cluster.Add(item);
                    }
                }

                MeshMountPoseCluster[] ordered = clusters
                    .OrderByDescending(value => value.DistinctLooseRendererCount)
                    .ThenByDescending(value => value.Evidence.Count)
                    .ThenBy(value => value.Position.x)
                    .ThenBy(value => value.Position.y)
                    .ThenBy(value => value.Position.z)
                    .ToArray();
                int bestRendererSupport = ordered.Length > 0
                    ? ordered[0].DistinctLooseRendererCount
                    : 0;
                int bestPairSupport = ordered.Length > 0
                    ? ordered[0].Evidence.Count
                    : 0;
                MeshMountPoseCluster[] best = ordered
                    .Where(value =>
                        value.DistinctLooseRendererCount == bestRendererSupport &&
                        value.Evidence.Count == bestPairSupport)
                    .ToArray();
                results.Add(new MeshDerivedMountPose(
                    loose,
                    evidence.Count,
                    best));
            }

            return results.ToArray();
        }

        private static Matrix4x4 GetRelativeMatrix(
            DonorUnitySceneModel scene,
            long transformId,
            long referenceRootTransformId)
        {
            scene.GetTransformRelativeTo(
                transformId,
                referenceRootTransformId,
                out Vector3 position,
                out Quaternion rotation,
                out Vector3 scale);
            return Matrix4x4.TRS(position, rotation, scale);
        }

        private static void WriteMountPoseAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<MeshDerivedMountPose> mountPoses)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "Group,DonorLooseName,PartDefinitionId,LooseTransformId,Status,LooseRendererCount,MatchedPairCount,BestPoseCount,BestDistinctRendererSupport,BestPairSupport,LocalPosition,LocalRotation,LocalScale,InstalledRendererTransformIds,InstalledRendererHierarchyPaths");
            foreach (MeshDerivedMountPose result in mountPoses
                         .OrderBy(value => value.LoosePart.GroupName,
                             StringComparer.Ordinal)
                         .ThenBy(value => value.LoosePart.Root.TransformId))
            {
                MeshMountPoseCluster best = result.BestPoses.FirstOrDefault();
                string status = result.MatchedPairCount == 0
                    ? "Unresolved"
                    : result.IsUnique
                        ? "UniqueMeshDerivedPose"
                        : "AmbiguousMeshDerivedPose";
                builder.Append(Csv(result.LoosePart.GroupName)).Append(',')
                    .Append(Csv(result.LoosePart.DonorName)).Append(',')
                    .Append(Csv(result.LoosePart.DefinitionId)).Append(',')
                    .Append(result.LoosePart.Root.TransformId).Append(',')
                    .Append(status).Append(',')
                    .Append(result.LoosePart.Renderers.Count).Append(',')
                    .Append(result.MatchedPairCount).Append(',')
                    .Append(result.BestPoses.Count).Append(',')
                    .Append(best?.DistinctLooseRendererCount ?? 0).Append(',')
                    .Append(best?.Evidence.Count ?? 0).Append(',')
                    .Append(Csv(FormatVector(best?.Position))).Append(',')
                    .Append(Csv(FormatQuaternion(best?.Rotation))).Append(',')
                    .Append(Csv(FormatVector(best?.Scale))).Append(',')
                    .Append(Csv(best == null
                        ? string.Empty
                        : string.Join(
                            ";",
                            best.Evidence
                                .Select(value => value.InstalledTransformId)
                                .Distinct()
                                .OrderBy(value => value))))
                    .Append(',')
                    .Append(Csv(best == null
                        ? string.Empty
                        : string.Join(
                            " | ",
                            best.Evidence
                                .Select(value => value.InstalledTransformId)
                                .Distinct()
                                .OrderBy(value => value)
                                .Select(scene.GetHierarchyPath))))
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(MountPoseAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string FormatVector(Vector3? value) =>
            value.HasValue
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:R};{1:R};{2:R}",
                    value.Value.x,
                    value.Value.y,
                    value.Value.z)
                : string.Empty;

        private static string FormatQuaternion(Quaternion? value) =>
            value.HasValue
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:R};{1:R};{2:R};{3:R}",
                    value.Value.x,
                    value.Value.y,
                    value.Value.z,
                    value.Value.w)
                : string.Empty;

        private static DonorAssemblyFsmEvidence[] DiscoverAssemblyFsmEvidence(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot)
        {
            var results = new List<DonorAssemblyFsmEvidence>();
            foreach (DonorMonoBehaviourRecord behaviour in scene.MonoBehaviours)
            {
                string body = behaviour.SerializedBody;
                if (!Regex.IsMatch(
                        body,
                        @"(?m)^\s{4}name:\s*Assembly\s*$",
                        RegexOptions.CultureInvariant) ||
                    !scene.TryGetTransformIdForGameObject(
                        behaviour.GameObjectId,
                        out long triggerTransformId) ||
                    !IsDescendantOf(scene, triggerTransformId, satsumaRoot.TransformId))
                {
                    continue;
                }

                string handChild = ExtractFsmStringVariable(body, "HandChild");
                DonorNamedGameObjectReference[] activationRoots =
                    ExtractFsmGameObjectVariables(
                        body,
                        @"^ActivateThis\d*$");
                long activateThisGameObjectId = activationRoots
                    .FirstOrDefault(value => string.Equals(
                        value.VariableName,
                        "ActivateThis",
                        StringComparison.Ordinal))
                    ?.GameObjectId ?? activationRoots
                    .FirstOrDefault()?.GameObjectId ?? 0L;
                long requiredGameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "db_PartRequired");
                long required1GameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "db_PartRequired1");
                long thisPartGameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "db_ThisPart");
                long detachPartGameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "DetachPart");
                long parentGameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "Parent");
                long boltsGameObjectId = ExtractFsmGameObjectVariable(
                    body,
                    "Bolts");
                string triggerName = scene.GetGameObjectName(
                    behaviour.GameObjectId);
                results.Add(new DonorAssemblyFsmEvidence(
                    behaviour.ComponentId,
                    behaviour.GameObjectId,
                    triggerTransformId,
                    triggerName,
                    handChild,
                    activateThisGameObjectId,
                    ResolveHierarchyPath(scene, activateThisGameObjectId),
                    activationRoots,
                    requiredGameObjectId,
                    ResolveHierarchyPath(scene, requiredGameObjectId),
                    required1GameObjectId,
                    ResolveHierarchyPath(scene, required1GameObjectId),
                    thisPartGameObjectId,
                    ResolveGameObjectName(scene, thisPartGameObjectId),
                    detachPartGameObjectId,
                    ResolveHierarchyPath(scene, detachPartGameObjectId),
                    parentGameObjectId,
                    ResolveHierarchyPath(scene, parentGameObjectId),
                    boltsGameObjectId,
                    ResolveHierarchyPath(scene, boltsGameObjectId),
                    HasFsmVariableReference(body, "Bolts"),
                    HasFsmVariableReference(body, "Bolted")));
            }

            return results
                .OrderBy(value => value.TriggerName, StringComparer.Ordinal)
                .ThenBy(value => value.TriggerTransformId)
                .ThenBy(value => value.ComponentId)
                .ToArray();
        }

        private static bool IsDescendantOf(
            DonorUnitySceneModel scene,
            long transformId,
            long ancestorTransformId)
        {
            long current = transformId;
            while (current != 0L)
            {
                if (current == ancestorTransformId)
                {
                    return true;
                }

                current = scene.GetTransform(current).FatherTransformId;
            }

            return false;
        }

        private static string ExtractFsmStringVariable(
            string serializedBody,
            string variableName)
        {
            return FindLastFsmVariableValue(
                serializedBody,
                variableName,
                value => !value.StartsWith("{fileID:", StringComparison.Ordinal));
        }

        private static long ExtractFsmGameObjectVariable(
            string serializedBody,
            string variableName)
        {
            string value = FindLastFsmVariableValue(
                serializedBody,
                variableName,
                candidate => candidate.StartsWith(
                    "{fileID:",
                    StringComparison.Ordinal));
            Match id = Regex.Match(
                value,
                @"\{fileID:\s*(?<id>-?\d+)\}",
                RegexOptions.CultureInvariant);
            return id.Success
                ? long.Parse(id.Groups["id"].Value, CultureInfo.InvariantCulture)
                : 0L;
        }

        private static DonorNamedGameObjectReference[]
            ExtractFsmGameObjectVariables(
                string serializedBody,
                string variableNamePattern)
        {
            var results = new List<DonorNamedGameObjectReference>();
            string[] lines = serializedBody.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            var namePattern = new Regex(
                variableNamePattern,
                RegexOptions.CultureInvariant);
            for (int index = 0; index < lines.Length; index++)
            {
                string trimmed = lines[index].Trim();
                if (!trimmed.StartsWith("name:", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = trimmed.Substring("name:".Length).Trim();
                if (!namePattern.IsMatch(name))
                {
                    continue;
                }

                int limit = Math.Min(lines.Length, index + 10);
                for (int scan = index + 1; scan < limit; scan++)
                {
                    string candidate = lines[scan].Trim();
                    if (!candidate.StartsWith(
                            "value:",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Match id = Regex.Match(
                        candidate,
                        @"\{fileID:\s*(?<id>-?\d+)\}",
                        RegexOptions.CultureInvariant);
                    if (id.Success)
                    {
                        long gameObjectId = long.Parse(
                            id.Groups["id"].Value,
                            CultureInfo.InvariantCulture);
                        if (gameObjectId != 0L && results.All(value =>
                                !string.Equals(
                                    value.VariableName,
                                    name,
                                    StringComparison.Ordinal)))
                        {
                            results.Add(new DonorNamedGameObjectReference(
                                name,
                                gameObjectId));
                        }
                    }

                    break;
                }
            }

            return results
                .OrderBy(value => value.VariableName,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static string FindLastFsmVariableValue(
            string serializedBody,
            string variableName,
            Func<string, bool> accept)
        {
            string[] lines = serializedBody.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            string expected = "name: " + variableName;
            string found = string.Empty;
            for (int index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(
                        lines[index].Trim(),
                        expected,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int limit = Math.Min(lines.Length, index + 10);
                for (int scan = index + 1; scan < limit; scan++)
                {
                    string trimmed = lines[scan].Trim();
                    if (!trimmed.StartsWith("value:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = trimmed.Substring("value:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value) && accept(value))
                    {
                        found = value;
                    }

                    break;
                }
            }

            return found;
        }

        private static bool HasFsmVariableReference(
            string serializedBody,
            string value) =>
            Regex.IsMatch(
                serializedBody,
                @"(?m)^\s*value:\s*" + Regex.Escape(value) + @"\s*$",
                RegexOptions.CultureInvariant);

        private static string ResolveHierarchyPath(
            DonorUnitySceneModel scene,
            long gameObjectId) =>
            gameObjectId != 0L && scene.TryGetTransformIdForGameObject(
                gameObjectId,
                out long transformId)
                    ? scene.GetHierarchyPath(transformId)
                    : string.Empty;

        private static string ResolveGameObjectName(
            DonorUnitySceneModel scene,
            long gameObjectId) =>
            gameObjectId != 0L
                ? scene.GetGameObjectName(gameObjectId)
                : string.Empty;

        private static void WriteAssemblyFsmAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorAssemblyFsmEvidence> evidence)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ComponentId,TriggerGameObjectId,TriggerTransformId,TriggerName,TriggerHierarchyPath,HandChild,ActivateThisGameObjectId,ActivateThisHierarchyPath,ActivationRoots,RequiredGameObjectId,RequiredHierarchyPath,Required1GameObjectId,Required1HierarchyPath,ThisPartGameObjectId,ThisPartName,DetachPartGameObjectId,DetachPartHierarchyPath,ParentGameObjectId,ParentHierarchyPath,BoltsGameObjectId,BoltsHierarchyPath,BoltReferenceStatus,ReferencesBolts,ReferencesBolted");
            foreach (DonorAssemblyFsmEvidence row in evidence)
            {
                builder.Append(row.ComponentId).Append(',')
                    .Append(row.TriggerGameObjectId).Append(',')
                    .Append(row.TriggerTransformId).Append(',')
                    .Append(Csv(row.TriggerName)).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(row.TriggerTransformId))).Append(',')
                    .Append(Csv(row.HandChild)).Append(',')
                    .Append(row.ActivateThisGameObjectId).Append(',')
                    .Append(Csv(row.ActivateThisHierarchyPath)).Append(',')
                    .Append(Csv(FormatActivationRoots(scene, row))).Append(',')
                    .Append(row.RequiredGameObjectId).Append(',')
                    .Append(Csv(row.RequiredHierarchyPath)).Append(',')
                    .Append(row.Required1GameObjectId).Append(',')
                    .Append(Csv(row.Required1HierarchyPath)).Append(',')
                    .Append(row.ThisPartGameObjectId).Append(',')
                    .Append(Csv(row.ThisPartName)).Append(',')
                    .Append(row.DetachPartGameObjectId).Append(',')
                    .Append(Csv(row.DetachPartHierarchyPath)).Append(',')
                    .Append(row.ParentGameObjectId).Append(',')
                    .Append(Csv(row.ParentHierarchyPath)).Append(',')
                    .Append(row.BoltsGameObjectId).Append(',')
                    .Append(Csv(row.BoltsHierarchyPath)).Append(',')
                    .Append(row.ReferencesBolts
                        ? row.BoltsGameObjectId != 0L
                            ? "BoundDonorObject"
                            : "EvidenceBackedNullDonorObjectException"
                        : "NotReferenced").Append(',')
                    .Append(row.ReferencesBolts ? "true" : "false").Append(',')
                    .Append(row.ReferencesBolted ? "true" : "false")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(AssemblyFsmAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string FormatActivationRoots(
            DonorUnitySceneModel scene,
            DonorAssemblyFsmEvidence evidence) =>
            string.Join(
                "|",
                evidence.ActivationRoots.Select(value =>
                    value.VariableName + "=" + value.GameObjectId + ":" +
                    ResolveHierarchyPath(scene, value.GameObjectId)));

        private static DonorFastenerEvidence[] DiscoverFastenerEvidence(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence)
        {
            var results = new List<DonorFastenerEvidence>();
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence)
            {
                long[] installedGameObjectIds = assembly.ActivationRoots
                    .Select(value => value.GameObjectId)
                    .Where(value => value != 0L)
                    .Distinct()
                    .ToArray();
                if (installedGameObjectIds.Length == 0 &&
                    assembly.ParentGameObjectId != 0L)
                {
                    installedGameObjectIds = new[]
                    {
                        assembly.ParentGameObjectId,
                    };
                }

                if (installedGameObjectIds.Length == 0 &&
                    assembly.TriggerName.StartsWith(
                        "TriggerWheel",
                        StringComparison.Ordinal))
                {
                    installedGameObjectIds = scene
                        .GetDescendants(
                            assembly.TriggerTransformId,
                            includeRoot: true)
                        .Where(value => string.Equals(
                            scene.GetGameObjectName(value.GameObjectId),
                            "Bolts",
                            StringComparison.Ordinal))
                        .Select(value => value.GameObjectId)
                        .Distinct()
                        .ToArray();
                }

                foreach (long installedGameObjectId in installedGameObjectIds)
                {
                    if (!scene.TryGetTransformIdForGameObject(
                            installedGameObjectId,
                            out long installedTransformId))
                    {
                        continue;
                    }

                    DonorTransformRecord[] markers = scene
                        .GetDescendants(installedTransformId, includeRoot: true)
                        .Where(value => string.Equals(
                            scene.GetGameObjectName(value.GameObjectId),
                            "BoltPM",
                            StringComparison.Ordinal))
                        .ToArray();
                    foreach (DonorTransformRecord marker in markers)
                    {
                        DonorColliderRecord sphere = scene
                            .GetCollidersForGameObject(
                                marker.GameObjectId,
                                includeDisabled: true,
                                includeTriggers: true)
                            .FirstOrDefault(value =>
                                value.Kind == DonorColliderKind.Sphere);
                        scene.GetTransformRelativeTo(
                            marker.TransformId,
                            installedTransformId,
                            out Vector3 localPosition,
                            out Quaternion localRotation,
                            out Vector3 localScale);
                        int inferredWrenchMillimeters = Mathf.RoundToInt(
                            Mathf.Abs(marker.LocalScale.x) * 10f);
                        bool supportedWrench =
                            inferredWrenchMillimeters != 0 &&
                            Enum.IsDefined(
                                typeof(FastenerSize),
                                inferredWrenchMillimeters);
                        results.Add(new DonorFastenerEvidence(
                            assembly,
                            installedTransformId,
                            marker.TransformId,
                            marker.GameObjectId,
                            localPosition,
                            localRotation,
                            localScale,
                            sphere?.Radius ?? 0f,
                            inferredWrenchMillimeters,
                            supportedWrench,
                            ExtractDonorFastenerMaximumStage(
                                scene,
                                marker.GameObjectId)));
                    }
                }
            }

            return results
                .GroupBy(value => value.MarkerTransformId)
                .Select(group => group
                    .OrderBy(value => value.Assembly.TriggerName,
                        StringComparer.Ordinal)
                    .First())
                .OrderBy(value => value.Assembly.TriggerName,
                    StringComparer.Ordinal)
                .ThenBy(value => value.MarkerTransformId)
                .ToArray();
        }

        private static void WriteFastenerAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorFastenerEvidence> evidence,
            string outputPath = FastenerAuditPath)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "AssemblyComponentId,TriggerName,HandChild,InstalledTransformId,InstalledHierarchyPath,MarkerGameObjectId,MarkerTransformId,MarkerHierarchyPath,LocalPosition,LocalRotation,LocalScale,DonorSphereRadiusMeters,InferredWrenchMillimeters,DonorMaximumStage,TransferStatus");
            foreach (DonorFastenerEvidence row in evidence)
            {
                builder.Append(row.Assembly.ComponentId).Append(',')
                    .Append(Csv(row.Assembly.TriggerName)).Append(',')
                    .Append(Csv(row.Assembly.HandChild)).Append(',')
                    .Append(row.InstalledTransformId).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(
                        row.InstalledTransformId))).Append(',')
                    .Append(row.MarkerGameObjectId).Append(',')
                    .Append(row.MarkerTransformId).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(
                        row.MarkerTransformId))).Append(',')
                    .Append(Csv(FormatVector(row.LocalPosition))).Append(',')
                    .Append(Csv(FormatQuaternion(row.LocalRotation))).Append(',')
                    .Append(Csv(FormatVector(row.LocalScale))).Append(',')
                    .Append(row.ColliderRadiusMeters.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.InferredWrenchMillimeters).Append(',')
                    .Append(row.MaximumStage).Append(',')
                    .Append(row.HasSupportedWrenchSize
                        ? "ConfigurationTransferred"
                        : "BehavioralReference")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(outputPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static int ExtractDonorFastenerMaximumStage(
            DonorUnitySceneModel scene,
            long markerGameObjectId)
        {
            int maximum = 0;
            foreach (DonorMonoBehaviourRecord behaviour in scene.MonoBehaviours)
            {
                if (behaviour.GameObjectId != markerGameObjectId ||
                    !Regex.IsMatch(
                        behaviour.SerializedBody,
                        @"(?m)^\s{4}name:\s*Screw\s*$",
                        RegexOptions.CultureInvariant))
                {
                    continue;
                }

                MatchCollection states = Regex.Matches(
                    behaviour.SerializedBody,
                    @"(?m)^\s{4}- name:\s*(?<stage>\d+)(?:\s+\d+)?\s*$",
                    RegexOptions.CultureInvariant);
                foreach (Match state in states)
                {
                    maximum = Mathf.Max(
                        maximum,
                        int.Parse(
                            state.Groups["stage"].Value,
                            CultureInfo.InvariantCulture));
                }
            }

            if (maximum <= 0)
            {
                throw new InvalidDataException(
                    "Donor BoltPM has no extractable Screw stage clamp: " +
                    markerGameObjectId + ".");
            }

            return maximum;
        }

        private static DonorBoltCheckEvidence[] DiscoverBoltCheckEvidence(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartSource> looseParts)
        {
            long[] stockBodyPartRoots = (looseParts ??
                                         Array.Empty<LoosePartSource>())
                .Where(value => IsStockBodyFastenedPanelDefinitionId(
                    value.DefinitionId))
                .Select(value => value.Root.TransformId)
                .ToArray();
            var results = new List<DonorBoltCheckEvidence>();
            foreach (DonorMonoBehaviourRecord behaviour in scene.MonoBehaviours)
            {
                string body = behaviour.SerializedBody;
                bool isBoltCheckFsm = Regex.IsMatch(
                    body,
                    @"(?m)^\s{4}name:\s*BoltCheck\s*$",
                    RegexOptions.CultureInvariant);
                bool isWheelRetentionFsm = Regex.IsMatch(
                        body,
                        @"(?m)^\s{4}name:\s*Use\s*$",
                        RegexOptions.CultureInvariant) &&
                    Regex.IsMatch(
                        body,
                        @"(?m)^\s{4}- name:\s*Bolts OFF\s*$",
                        RegexOptions.CultureInvariant) &&
                    Regex.IsMatch(
                        body,
                        @"(?m)^\s{4}- name:\s*Chance\s*$",
                        RegexOptions.CultureInvariant) &&
                    Regex.IsMatch(
                        body,
                        @"(?m)^\s{4}- name:\s*Break off\s*$",
                        RegexOptions.CultureInvariant);
                if ((!isBoltCheckFsm && !isWheelRetentionFsm) ||
                    !scene.TryGetTransformIdForGameObject(
                        behaviour.GameObjectId,
                        out long transformId))
                {
                    continue;
                }

                // The donor keeps BoltCheck on a loose body-panel root and
                // enables it only after Assembly. Those nine roots live below
                // CARPARTS rather than SATSUMA, so a chassis-only scan silently
                // lost the exact removal latch thresholds for every body panel.
                bool isSatsumaBoltCheck = IsDescendantOf(
                    scene,
                    transformId,
                    satsumaRoot.TransformId);
                bool isLooseStockBodyBoltCheck = stockBodyPartRoots.Any(root =>
                    IsDescendantOf(scene, transformId, root));
                if (isBoltCheckFsm &&
                    !isSatsumaBoltCheck &&
                    !isLooseStockBodyBoltCheck)
                {
                    continue;
                }

                bool hasBoltedYes = TryExtractFsmFloatVariable(
                    body,
                    "BoltedYES",
                    out float boltedYes);
                bool hasBoltedNo = TryExtractFsmFloatVariable(
                    body,
                    "BoltedNO",
                    out float boltedNo);
                bool hasTightness = TryExtractFsmFloatVariable(
                    body,
                    "Tightness",
                    out _);
                bool hasChanceState = Regex.IsMatch(
                    body,
                    @"(?m)^\s{4}- name:\s*Chance\s*$",
                    RegexOptions.CultureInvariant);
                bool hasBreak = Regex.IsMatch(
                    body,
                    @"(?m)^\s+(?:name|toState):\s*(?:BREAK|Break off)\s*$",
                    RegexOptions.CultureInvariant);
                bool hasLooseBreakSpeed = ContainsSerializedFloat(body, 5f);
                bool hasPartialCheckSpeed = ContainsSerializedFloat(body, 33f);
                bool referencesSpeed = hasBreak && hasChanceState &&
                    hasLooseBreakSpeed && hasPartialCheckSpeed;
                bool hasChanceFormula = hasChanceState &&
                    body.Contains(
                        "HutongGames.PlayMaker.Actions.FloatOperator") &&
                    body.Contains(
                        "HutongGames.PlayMaker.Actions.SendRandomEvent") &&
                    ContainsSerializedFloat(body, 32f) &&
                    ContainsSerializedFloat(body, 100f);

                results.Add(new DonorBoltCheckEvidence(
                    behaviour.ComponentId,
                    behaviour.GameObjectId,
                    transformId,
                    scene.GetHierarchyPath(transformId),
                    hasBoltedYes,
                    boltedYes,
                    hasBoltedNo,
                    boltedNo,
                    hasTightness,
                    hasBreak,
                    referencesSpeed,
                    referencesSpeed && hasLooseBreakSpeed
                        ? 5f
                        : 0f,
                    referencesSpeed && hasPartialCheckSpeed
                        ? 33f
                        : 0f,
                    hasChanceFormula ? 32f : 0f,
                    hasChanceFormula ? 100f : 0f,
                    hasChanceFormula,
                    isWheelRetentionFsm));
            }

            return results
                .OrderBy(value => value.HierarchyPath, StringComparer.Ordinal)
                .ThenBy(value => value.ComponentId)
                .ToArray();
        }

        private static bool TryExtractFsmFloatVariable(
            string serializedBody,
            string variableName,
            out float value)
        {
            value = 0f;
            Match sectionMatch = Regex.Match(
                serializedBody ?? string.Empty,
                @"(?ms)^\s{6}floatVariables:\s*$" +
                @"(?<section>.*?)" +
                @"(?=^\s{6}(?:intVariables|boolVariables|stringVariables|" +
                @"vector2Variables|vector3Variables|colorVariables|" +
                @"rectVariables|quaternionVariables|gameObjectVariables):)",
                RegexOptions.CultureInvariant);
            if (!sectionMatch.Success)
            {
                return false;
            }

            Match variable = Regex.Match(
                sectionMatch.Groups["section"].Value,
                @"(?ms)^\s{6}- useVariable:.*?" +
                @"^\s{8}name:\s*" + Regex.Escape(variableName) +
                @"\s*$.*?^\s{8}value:\s*(?<value>" +
                @"[-+0-9.eE]+)\s*$",
                RegexOptions.CultureInvariant);
            return variable.Success && float.TryParse(
                variable.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool ContainsSerializedFloat(
            string serializedBody,
            float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            var hex = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                hex.Append(bytes[index].ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }

            return (serializedBody ?? string.Empty).IndexOf(
                hex.ToString(),
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WriteBoltCheckAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorBoltCheckEvidence> evidence)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ComponentId,GameObjectId,TransformId,HierarchyPath,HasBoltedYES,BoltedYES,HasBoltedNO,BoltedNO,ReferencesTightness,ReferencesBREAK,ReferencesSpeedKMH,LooseBreakSpeedKph,PartialCheckSpeedKph,ChanceMaximumTightness,ChanceDivisor,HasChanceFormula,IsWheelRetentionPolicy,TransferStatus");
            foreach (DonorBoltCheckEvidence row in evidence)
            {
                builder.Append(row.ComponentId).Append(',')
                    .Append(row.GameObjectId).Append(',')
                    .Append(row.TransformId).Append(',')
                    .Append(Csv(row.HierarchyPath)).Append(',')
                    .Append(row.HasBoltedYes ? "true" : "false").Append(',')
                    .Append(row.BoltedYes.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.HasBoltedNo ? "true" : "false").Append(',')
                    .Append(row.BoltedNo.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.ReferencesTightness ? "true" : "false")
                    .Append(',')
                    .Append(row.ReferencesBreak ? "true" : "false")
                    .Append(',')
                    .Append(row.ReferencesSpeed ? "true" : "false")
                    .Append(',')
                    .Append(row.LooseBreakSpeedKph.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.PartialCheckSpeedKph.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.ChanceMaximumTightness.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.ChanceDivisor.ToString(
                        "R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(row.HasChanceFormula ? "true" : "false")
                    .Append(',')
                    .Append(row.IsWheelRetentionPolicy ? "true" : "false")
                    .Append(',')
                    .Append("ConfigurationTransferred")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(BoltCheckAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static void WriteHingeJointAudit(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ComponentId,GameObjectId,TransformId,GameObjectName,HierarchyPath,OwnBodyBelowSatsuma,ConnectedRigidbodyComponentId,ConnectedGameObjectId,ConnectedHierarchyPath,ConnectedBodyBelowSatsuma,Anchor,Axis,AutoConfigureConnectedAnchor,ConnectedAnchor,UseSpring,Spring,Damper,TargetPosition,UseLimits,MinimumLimitDegrees,MaximumLimitDegrees,BreakForce,BreakTorque,EnableCollision,TransferStatus");

            foreach (DonorHingeJointRecord hinge in scene.HingeJoints)
            {
                if (!scene.TryGetTransformIdForGameObject(
                        hinge.GameObjectId,
                        out long transformId))
                {
                    continue;
                }

                bool ownBodyBelowSatsuma =
                    IsDescendantOf(scene, transformId, satsumaRoot.TransformId);
                bool hasConnectedBody = scene.TryGetRigidbodyByComponentId(
                    hinge.ConnectedBodyComponentId,
                    out DonorRigidbodyRecord connectedBody);
                long connectedTransformId = hasConnectedBody &&
                                            scene.TryGetTransformIdForGameObject(
                                                connectedBody.GameObjectId,
                                                out long resolvedConnectedTransformId)
                    ? resolvedConnectedTransformId
                    : 0L;
                bool connectedBodyBelowSatsuma = connectedTransformId != 0L &&
                    IsDescendantOf(
                        scene,
                        connectedTransformId,
                        satsumaRoot.TransformId);
                builder.Append(hinge.ComponentId).Append(',')
                    .Append(hinge.GameObjectId).Append(',')
                    .Append(transformId).Append(',')
                    .Append(Csv(scene.GetGameObjectName(hinge.GameObjectId)))
                    .Append(',')
                    .Append(Csv(scene.GetHierarchyPath(transformId))).Append(',')
                    .Append(ownBodyBelowSatsuma ? "true" : "false").Append(',')
                    .Append(hinge.ConnectedBodyComponentId).Append(',')
                    .Append(hasConnectedBody ? connectedBody.GameObjectId : 0L)
                    .Append(',')
                    .Append(Csv(connectedTransformId != 0L
                        ? scene.GetHierarchyPath(connectedTransformId)
                        : string.Empty)).Append(',')
                    .Append(connectedBodyBelowSatsuma ? "true" : "false")
                    .Append(',')
                    .Append(Csv(FormatVector(hinge.Anchor))).Append(',')
                    .Append(Csv(FormatVector(hinge.Axis))).Append(',')
                    .Append(hinge.AutoConfigureConnectedAnchor
                        ? "true"
                        : "false").Append(',')
                    .Append(Csv(FormatVector(hinge.ConnectedAnchor))).Append(',')
                    .Append(hinge.UseSpring ? "true" : "false").Append(',')
                    .Append(FormatFloat(hinge.Spring)).Append(',')
                    .Append(FormatFloat(hinge.Damper)).Append(',')
                    .Append(FormatFloat(hinge.TargetPosition)).Append(',')
                    .Append(hinge.UseLimits ? "true" : "false").Append(',')
                    .Append(FormatFloat(hinge.MinimumLimitDegrees)).Append(',')
                    .Append(FormatFloat(hinge.MaximumLimitDegrees)).Append(',')
                    .Append(FormatFloat(hinge.BreakForce)).Append(',')
                    .Append(FormatFloat(hinge.BreakTorque)).Append(',')
                    .Append(hinge.EnableCollision ? "true" : "false").Append(',')
                    .Append(ownBodyBelowSatsuma || connectedBodyBelowSatsuma
                        ? "ConfigurationTransferred"
                        : "BehavioralReference")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(HingeJointAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string FormatFloat(float value) =>
            float.IsPositiveInfinity(value)
                ? "Infinity"
                : float.IsNegativeInfinity(value)
                    ? "-Infinity"
                    : value.ToString("R", CultureInfo.InvariantCulture);

        private static void WriteHingedPartFsmAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartSource> looseParts)
        {
            LoosePartSource[] hingedParts = looseParts
                .Where(value =>
                    string.Equals(
                        value.DefinitionId,
                        "vehicle.satsuma.part.bootlid",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        value.DefinitionId,
                        "vehicle.satsuma.part.door-left",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        value.DefinitionId,
                        "vehicle.satsuma.part.door-right",
                        StringComparison.Ordinal))
                .ToArray();
            var builder = new StringBuilder();
            builder.AppendLine(
                "PartDefinitionId,PartRootTransformId,ComponentId,GameObjectId,HierarchyPath,FsmName,Actions");
            foreach (LoosePartSource part in hingedParts)
            {
                foreach (DonorMonoBehaviourRecord behaviour in scene.MonoBehaviours)
                {
                    if (!scene.TryGetTransformIdForGameObject(
                            behaviour.GameObjectId,
                            out long transformId) ||
                        !IsDescendantOf(
                            scene,
                            transformId,
                            part.Root.TransformId))
                    {
                        continue;
                    }

                    string fsmName = Regex.Match(
                        behaviour.SerializedBody,
                        @"(?ms)^[ \t]*fsm:[ \t]*\r?\n.*?^[ \t]{4}name:[ \t]*(?<value>[^\r\n]*)")
                        .Groups["value"].Value.Trim();
                    string actions = string.Join(";", Regex.Matches(
                            behaviour.SerializedBody,
                            @"(?m)^\s*-\s+(?<value>[^\r\n]*Actions\.[^\r\n]+)\s*$")
                        .Cast<Match>()
                        .Select(value => value.Groups["value"].Value.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal));
                    builder.Append(Csv(part.DefinitionId)).Append(',')
                        .Append(part.Root.TransformId).Append(',')
                        .Append(behaviour.ComponentId).Append(',')
                        .Append(behaviour.GameObjectId).Append(',')
                        .Append(Csv(scene.GetHierarchyPath(transformId))).Append(',')
                        .Append(Csv(fsmName)).Append(',')
                        .Append(Csv(actions))
                        .AppendLine();
                }
            }

            File.WriteAllText(
                ToFileSystemPath(HingedPartFsmAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static void WriteHingedAssemblyConfigurationAudit(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartSource> looseParts,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "TriggerName,AssemblyComponentId,ParentGameObjectId,ParentTransformId,ParentHierarchyPath,PartDefinitionId,LoosePartTransformId,LocalPosition,LocalRotation,LocalAxis,MinimumAngleDegrees,MaximumAngleDegrees,OpenHoldWindowDegrees,UseFsmComponentId,OpenTorqueLocal,CloseTorqueLocal,BreakForce,BreakTorque,RequiresReleaseBeforeOpening,SupportedFastenerCount,Classification");
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence
                         .Where(value =>
                             value.ParentGameObjectId != 0L &&
                             IsDeferredHingeAssembly(value.TriggerName))
                         .OrderBy(value => value.TriggerName,
                             StringComparer.Ordinal))
            {
                string sourceIdentity = new[]
                    {
                        assembly.HandChild,
                        assembly.ThisPartName,
                        Regex.Replace(
                            assembly.TriggerName ?? string.Empty,
                            @"^trigger_?",
                            string.Empty,
                            RegexOptions.IgnoreCase |
                            RegexOptions.CultureInvariant),
                    }
                    .Select(NormalizeDonorPartIdentity)
                    .FirstOrDefault(value => !string.IsNullOrEmpty(value)) ??
                    string.Empty;
                LoosePartSource[] matches = looseParts
                    .Where(value => string.Equals(
                        NormalizeDonorPartIdentity(value.DonorName),
                        sourceIdentity,
                        StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1 ||
                    !scene.TryGetTransformIdForGameObject(
                        assembly.ParentGameObjectId,
                        out long parentTransformId))
                {
                    throw new InvalidDataException(
                        "Could not audit the unique donor hinged assembly " +
                        assembly.TriggerName + ".");
                }

                LoosePartSource part = matches[0];
                HingeMountBuild hinge = GetHingeMountBuild(
                    assembly.TriggerName);
                scene.GetTransformRelativeTo(
                    parentTransformId,
                    satsumaRoot.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out _);
                int supportedFasteners = scene
                    .GetDescendants(part.Root.TransformId, includeRoot: true)
                    .Count(marker =>
                    {
                        if (!string.Equals(
                                scene.GetGameObjectName(marker.GameObjectId),
                                "BoltPM",
                                StringComparison.Ordinal))
                        {
                            return false;
                        }

                        int wrenchMillimeters = Mathf.RoundToInt(
                            Mathf.Abs(marker.LocalScale.x) * 10f);
                        return wrenchMillimeters != 0 &&
                            Enum.IsDefined(
                                typeof(FastenerSize),
                                wrenchMillimeters);
                    });
                builder.Append(Csv(assembly.TriggerName)).Append(',')
                    .Append(assembly.ComponentId).Append(',')
                    .Append(assembly.ParentGameObjectId).Append(',')
                    .Append(parentTransformId).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(parentTransformId)))
                    .Append(',')
                    .Append(Csv(part.DefinitionId)).Append(',')
                    .Append(part.Root.TransformId).Append(',')
                    .Append(Csv(FormatVector(localPosition))).Append(',')
                    .Append(Csv(FormatQuaternion(localRotation))).Append(',')
                    .Append(Csv(FormatVector(hinge.LocalAxis))).Append(',')
                    .Append(FormatFloat(hinge.MinimumAngleDegrees)).Append(',')
                    .Append(FormatFloat(hinge.MaximumAngleDegrees)).Append(',')
                    .Append(FormatFloat(
                        hinge.DonorOpenHoldWindowDegrees)).Append(',')
                    .Append(hinge.UseFsmComponentId).Append(',')
                    .Append(Csv(FormatVector(hinge.DonorOpenTorqueLocal)))
                    .Append(',')
                    .Append(Csv(FormatVector(hinge.DonorCloseTorqueLocal)))
                    .Append(',')
                    .Append(FormatFloat(hinge.DonorBreakForce)).Append(',')
                    .Append(FormatFloat(hinge.DonorBreakTorque)).Append(',')
                    .Append(hinge.RequiresReleaseBeforeOpening
                        ? "true"
                        : "false").Append(',')
                    .Append(supportedFasteners).Append(',')
                    .Append("PivotSource;ConfigurationTransferred;Reimplemented")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(HingedAssemblyConfigurationAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string Csv(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static void WriteLoosePartAssemblyFsmAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartSource> looseParts,
            IReadOnlyList<DonorAssemblyFsmEvidence> evidence)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "OwnerPartDefinitionId,OwnerPartTransformId,ComponentId,TriggerGameObjectId,TriggerTransformId,TriggerName,TriggerHierarchyPath,HandChild,ActivateThisGameObjectId,ActivateThisHierarchyPath,ActivationRoots,RequiredGameObjectId,RequiredHierarchyPath,Required1GameObjectId,Required1HierarchyPath,ThisPartGameObjectId,ThisPartName,DetachPartGameObjectId,DetachPartHierarchyPath,ParentGameObjectId,ParentHierarchyPath,BoltsGameObjectId,BoltsHierarchyPath,BoltReferenceStatus,ReferencesBolts,ReferencesBolted");
            foreach (DonorAssemblyFsmEvidence row in evidence)
            {
                LoosePartSource owner = looseParts.SingleOrDefault(value =>
                    IsDescendantOf(
                        scene,
                        row.TriggerTransformId,
                        value.Root.TransformId));
                if (owner == null)
                {
                    continue;
                }

                builder.Append(Csv(owner.DefinitionId)).Append(',')
                    .Append(owner.Root.TransformId).Append(',')
                    .Append(row.ComponentId).Append(',')
                    .Append(row.TriggerGameObjectId).Append(',')
                    .Append(row.TriggerTransformId).Append(',')
                    .Append(Csv(row.TriggerName)).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(row.TriggerTransformId)))
                    .Append(',')
                    .Append(Csv(row.HandChild)).Append(',')
                    .Append(row.ActivateThisGameObjectId).Append(',')
                    .Append(Csv(row.ActivateThisHierarchyPath)).Append(',')
                    .Append(Csv(FormatActivationRoots(scene, row))).Append(',')
                    .Append(row.RequiredGameObjectId).Append(',')
                    .Append(Csv(row.RequiredHierarchyPath)).Append(',')
                    .Append(row.Required1GameObjectId).Append(',')
                    .Append(Csv(row.Required1HierarchyPath)).Append(',')
                    .Append(row.ThisPartGameObjectId).Append(',')
                    .Append(Csv(row.ThisPartName)).Append(',')
                    .Append(row.DetachPartGameObjectId).Append(',')
                    .Append(Csv(row.DetachPartHierarchyPath)).Append(',')
                    .Append(row.ParentGameObjectId).Append(',')
                    .Append(Csv(row.ParentHierarchyPath)).Append(',')
                    .Append(row.BoltsGameObjectId).Append(',')
                    .Append(Csv(row.BoltsHierarchyPath)).Append(',')
                    .Append(row.ReferencesBolts
                        ? row.BoltsGameObjectId != 0L
                            ? "BoundDonorObject"
                            : "EvidenceBackedNullDonorObjectException"
                        : "NotReferenced").Append(',')
                    .Append(row.ReferencesBolts ? "true" : "false")
                    .Append(',')
                    .Append(row.ReferencesBolted ? "true" : "false")
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(LoosePartAssemblyFsmAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string GetBaseDefinitionSlug(
            string groupName,
            string donorName)
        {
            string name = Regex.Replace(
                    donorName ?? string.Empty,
                    @"\(Clone\)$",
                    string.Empty,
                    RegexOptions.CultureInvariant)
                .Trim()
                .ToLowerInvariant();
            string compact = Regex.Replace(
                    name.Replace("carburator", "carburetor"),
                    @"[^a-z0-9]+",
                    "-",
                    RegexOptions.CultureInvariant)
                .Trim('-');

            if (string.Equals(groupName, "PartsMotor", StringComparison.Ordinal) &&
                string.Equals(compact, "block", StringComparison.Ordinal))
            {
                return "engine-block";
            }

            if (string.Equals(compact, "battery0", StringComparison.Ordinal))
            {
                return "battery";
            }

            if (string.Equals(compact, "clutch-disc", StringComparison.Ordinal))
            {
                return "clutch";
            }

            Match wheel = Regex.Match(
                compact,
                @"^wheel-(?<family>steel|gt)(?<number>[1-4])$",
                RegexOptions.CultureInvariant);
            if (wheel.Success)
            {
                string family = string.Equals(
                    wheel.Groups["family"].Value,
                    "steel",
                    StringComparison.Ordinal)
                        ? "wheel-stock"
                        : "wheel-gt";
                string corner = wheel.Groups["number"].Value switch
                {
                    "1" => "fl",
                    "2" => "fr",
                    "3" => "rl",
                    _ => "rr",
                };
                return family + "-" + corner;
            }

            string groupPrefix = groupName switch
            {
                "PartsGT" => "gt-",
                "PartsExtra" => "extra-",
                _ => string.Empty,
            };
            return groupPrefix + (string.IsNullOrEmpty(compact)
                ? "donor-part"
                : compact);
        }

        private static LoosePartBuild[] BuildLoosePartAssets(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartSource> sources,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            IReadOnlyList<InstalledMountCandidate> mountCandidates)
        {
            EnsureFolder(LoosePartPresentationRoot);
            EnsureFolder(LoosePartDefinitionRoot);
            var builds = new List<LoosePartBuild>(sources.Count);
            for (int index = 0; index < sources.Count; index++)
            {
                LoosePartSource source = sources[index];
                string assetStem = source.Root.TransformId.ToString(
                    CultureInfo.InvariantCulture) + "_" +
                    SanitizeName(source.DonorName);
                string presentationPath = LoosePartPresentationRoot + "/" +
                                          assetStem + ".prefab";
                var presentationRoot = new GameObject(
                    source.DonorName + " Presentation");
                GameObject presentationPrefab;
                try
                {
                    for (int rendererIndex = 0;
                         rendererIndex < source.Renderers.Count;
                         rendererIndex++)
                    {
                        DonorStaticRendererRecord donor =
                            source.Renderers[rendererIndex];
                        string donorGameObjectName = scene.GetGameObjectName(
                            donor.GameObjectId);
                        if (IsEmbeddedStockFenderMudflapRenderer(
                                source.DefinitionId,
                                donor.GameObjectId))
                        {
                            // These are donor ActivateThis placeholders and are
                            // inactive until the separate mudflap part is fitted.
                            // Importing them as live fender geometry duplicates
                            // the actual installable mudflap PartInstance.
                            continue;
                        }
                        if (IsDonorLoosePartFastenerRenderer(
                                source.DefinitionId,
                                donorGameObjectName))
                        {
                            // Donor loose-part hierarchies carry presentation
                            // copies of their bolts and nuts. The installed mount
                            // owns the project fastener state and interaction
                            // targets; keeping both creates an always-visible dead
                            // set on top of the live runtime fasteners.
                            continue;
                        }

                        long transformId = scene.GetTransformIdForGameObject(
                            donor.GameObjectId);
                        GetLoosePartRelativeTransform(
                            scene,
                            transformId,
                            source.Root.TransformId,
                            out Vector3 localPosition,
                            out Quaternion localRotation,
                            out Vector3 localScale);
                        var owner = new GameObject(
                            SanitizeName(donorGameObjectName) + "_" +
                            donor.ComponentId.ToString(
                                CultureInfo.InvariantCulture));
                        owner.transform.SetParent(
                            presentationRoot.transform,
                            false);
                        owner.transform.SetLocalPositionAndRotation(
                            localPosition,
                            localRotation);
                        owner.transform.localScale = localScale;
                        owner.AddComponent<MeshFilter>().sharedMesh =
                            RequireAsset<Mesh>(importedMeshes[donor.MeshGuid]);
                        MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = donor.MaterialGuids
                            .Select(guid => materials.TryGetValue(
                                guid,
                                out Material material)
                                    ? material
                                    : null)
                            .ToArray();
                        renderer.shadowCastingMode = donor.MaterialGuids.Any(
                            IsDonorGlassMaterialSourceGuid)
                                ? ShadowCastingMode.Off
                                : ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                        owner.SetActive(scene.IsTransformActiveBelow(
                            transformId,
                            source.Root.TransformId,
                            treatRootAsActive: true));
                    }

                    if (IsRoadWheelDefinitionId(source.DefinitionId))
                    {
                        BuildWheelTirePresentation(
                            presentationRoot.transform,
                            source.DefinitionId,
                            importedMeshes,
                            materials);
                    }

                    presentationPrefab = PrefabUtility.SaveAsPrefabAsset(
                        presentationRoot,
                        presentationPath) ??
                        throw new InvalidOperationException(
                            "Could not save loose Satsuma part presentation: " +
                            source.DonorName);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(presentationRoot);
                }

                float mass = source.Rigidbody != null
                    ? Mathf.Max(0.01f, source.Rigidbody.MassKilograms)
                    : InferFallbackMass(source.DonorName);
                InstalledMountCandidate acceptedMount = mountCandidates
                    .SingleOrDefault(value =>
                        value.LoosePart.Root.TransformId ==
                        source.Root.TransformId &&
                        IsAcceptedMountCandidate(value));
                PartCompatibilityRule[] compatibilityRules =
                    acceptedMount == null &&
                    !IsHingedPartDefinitionId(source.DefinitionId)
                        ? Array.Empty<PartCompatibilityRule>()
                        : new[]
                        {
                            PartCompatibilityRule.Create(
                                GetMountSocketType(source.DefinitionId),
                                "vehicle.satsuma.part.body-shell"),
                        };
                var definition = ScriptableObject.CreateInstance<PartDefinition>();
                definition.Configure(
                    source.DefinitionId,
                    NormalizeDisplayName(source.DonorName),
                    InferPartCategory(source.DonorName),
                    mass,
                    presentationPrefab,
                    compatibilityRules);
                string definitionPath = LoosePartDefinitionRoot + "/" +
                                        assetStem + ".asset";
                AssetDatabase.CreateAsset(definition, definitionPath);
                builds.Add(new LoosePartBuild(
                    source,
                    presentationPrefab,
                    definition));
            }

            return builds.ToArray();
        }

        private static void GetLoosePartRelativeTransform(
            DonorUnitySceneModel scene,
            long transformId,
            long loosePartRootTransformId,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            DonorTransformRecord transform = scene.GetTransform(transformId);
            if (transform.FatherTransformId == loosePartRootTransformId)
            {
                // Matrix decomposition is ambiguous for a negative determinant.
                // The donor right-door handle is a direct child with identity
                // rotation and scale (-1, 1, 1); decomposing the relative
                // matrix produced an extra 180-degree Z rotation and mirrored
                // the combined handle/keyhole mesh around the wrong axis.
                localPosition = transform.LocalPosition;
                localRotation = transform.LocalRotation;
                localScale = transform.LocalScale;
                return;
            }

            scene.GetTransformRelativeTo(
                transformId,
                loosePartRootTransformId,
                out localPosition,
                out localRotation,
                out localScale);
        }

        private static void BuildWheelTirePresentation(
            Transform presentationRoot,
            string definitionId,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var tire = new GameObject(ProjectWheelTirePresentationName);
            tire.transform.SetParent(presentationRoot, false);
            tire.transform.localPosition = Vector3.zero;
            tire.transform.localRotation = Quaternion.identity;
            tire.transform.localScale = Vector3.one;
            tire.AddComponent<MeshFilter>().sharedMesh = RequireAsset<Mesh>(
                importedMeshes[DonorStockTireMeshSourceGuid]);
            MeshRenderer renderer = tire.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[
                DonorStockTireMaterialSourceGuid];
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.enabled = IsStockWheelDefinitionId(definitionId);
        }

        private static MountBuild[] BuildMountAssets(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<InstalledMountCandidate> mountCandidates,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence,
            IReadOnlyList<DonorAssemblyFsmEvidence> loosePartAssemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> loosePartFastenerEvidence)
        {
            var buildsByTransformId = loosePartBuilds.ToDictionary(
                value => value.Source.Root.TransformId);
            var results = new List<MountBuild>();
            foreach (InstalledMountCandidate candidate in mountCandidates
                         .Where(IsAcceptedMountCandidate)
                         .OrderBy(value => value.LoosePart.DefinitionId,
                             StringComparer.Ordinal))
            {
                LoosePartBuild part = buildsByTransformId[
                    candidate.LoosePart.Root.TransformId];
                DonorTransformRecord installed = candidate.Candidates[0];
                scene.GetTransformRelativeTo(
                    installed.TransformId,
                    satsumaRoot.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                string slug = candidate.LoosePart.DefinitionId.Substring(
                    "vehicle.satsuma.part.".Length);
                string mountId = "mount.satsuma." + slug;
                string socketType = GetMountSocketType(
                    candidate.LoosePart.DefinitionId);
                DonorFastenerEvidence[] donorFasteners =
                    ResolveMountFastenerEvidence(
                        scene,
                        installed.TransformId,
                        part,
                        loosePartBuilds,
                        assemblyEvidence,
                        fastenerEvidence);
                FastenerBuild[] fasteners = BuildFastenerAssets(
                    slug,
                    part.Definition.DisplayName,
                    donorFasteners);
                if (IsStockBodyFastenedPanelDefinitionId(
                        part.Definition.DefinitionId))
                {
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                else if (string.Equals(
                             part.Definition.DefinitionId,
                             "vehicle.satsuma.part.steering-column",
                             StringComparison.Ordinal))
                {
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                var definition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                definition.Configure(
                    mountId,
                    "Satsuma: " + part.Definition.DisplayName,
                    socketType,
                    "vehicle.satsuma.part.body-shell",
                    new[] { candidate.LoosePart.DefinitionId },
                    new MountConstraint(0.35f, 40f, 1.25f, 0f),
                    0f,
                    fasteners
                        .Select(value => value.Definition)
                        .ToArray());
                string path = MountDefinitionRoot + "/" +
                              SanitizeName(mountId) + ".asset";
                AssetDatabase.CreateAsset(definition, path);
                results.Add(new MountBuild(
                    candidate.LoosePart,
                    definition,
                    localPosition,
                    localRotation,
                    localScale,
                    installed.TransformId,
                    fasteners));
            }

            AddRearDrumMounts(
                scene,
                satsumaRoot,
                loosePartBuilds,
                assemblyEvidence,
                fastenerEvidence,
                results);
            AddFixedAssemblyParentMounts(
                scene,
                satsumaRoot,
                loosePartBuilds,
                assemblyEvidence,
                fastenerEvidence,
                results);
            AddHingedAssemblyParentMounts(
                scene,
                satsumaRoot,
                loosePartBuilds,
                assemblyEvidence,
                results);
            AddAssemblyTargetMounts(
                scene,
                satsumaRoot,
                loosePartBuilds,
                assemblyEvidence,
                fastenerEvidence,
                results);
            AddLoosePartOwnedMounts(
                scene,
                loosePartBuilds,
                loosePartAssemblyEvidence,
                loosePartFastenerEvidence,
                results);
            AddEngineAssemblyMount(
                scene,
                satsumaRoot,
                loosePartBuilds,
                assemblyEvidence,
                results);

            return results.ToArray();
        }

        private static DonorFastenerEvidence[] ResolveMountFastenerEvidence(
            DonorUnitySceneModel scene,
            long mountTransformId,
            LoosePartBuild part,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence)
        {
            DonorAssemblyFsmEvidence[] matchingAssemblies = assemblyEvidence
                .Where(assembly => ResolveAssemblyPartCandidates(
                        assembly,
                        loosePartBuilds)
                    .Any(candidate => candidate.Definition != null &&
                        part.Definition != null && string.Equals(
                            candidate.Definition.DefinitionId,
                            part.Definition.DefinitionId,
                            StringComparison.Ordinal)))
                .OrderBy(value => value.ComponentId)
                .ToArray();
            var resolved = new List<DonorFastenerEvidence>();

            if (part.Definition != null && string.Equals(
                    part.Definition.DefinitionId,
                    "vehicle.satsuma.part.grille",
                    StringComparison.Ordinal))
            {
                // The locked donor trigger_grille is structurally orphaned:
                // Parent/ThisPart/HandChild are all null. Its two Screw markers
                // are nevertheless frozen and unambiguous below the loose
                // grille. Keep the exact IDs so generic FSM inference cannot
                // silently turn this panel into an unfastened decoration.
                DonorAssemblyFsmEvidence grilleAssembly = assemblyEvidence
                    .Single(value => string.Equals(
                        value.TriggerName,
                        "trigger_grille",
                        StringComparison.Ordinal));
                foreach (long markerTransformId in new[] { 47050L, 72072L })
                {
                    DonorTransformRecord marker = scene.GetTransform(
                        markerTransformId);
                    if (!string.Equals(
                            scene.GetGameObjectName(marker.GameObjectId),
                            "BoltPM",
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Locked donor grille BoltPM drifted: " +
                            markerTransformId);
                    }

                    resolved.Add(CreateLoosePartFastenerEvidence(
                        scene,
                        grilleAssembly,
                        part.Source.Root.TransformId,
                        mountTransformId,
                        marker));
                }

                return resolved
                    .OrderBy(value => value.MarkerTransformId)
                    .ToArray();
            }

            // The donor sub-frame is secured by exactly four 10 mm bolts. Its
            // installation transform is also reused by three unrelated FSM
            // marker records, so the generic InstalledTransformId match below
            // would incorrectly graft those markers onto the sub-frame. The
            // four direct BoltPM descendants are the stronger donor authority.
            bool useDirectHierarchyOnly = part.Definition != null &&
                string.Equals(
                    part.Definition.DefinitionId,
                    "vehicle.satsuma.part.sub-frame",
                    StringComparison.Ordinal);

            if (!useDirectHierarchyOnly)
            {
                foreach (DonorFastenerEvidence evidence in fastenerEvidence
                             .Where(value =>
                                 value.InstalledTransformId == mountTransformId &&
                                 value.HasSupportedWrenchSize))
                {
                    resolved.Add(evidence);
                }
            }

            // Several donor parts (notably both front wishbones) place the
            // visible mesh below an IK carrier while BoltPM markers remain on
            // that carrier. The mesh-derived mount is authoritative, so the
            // markers must be reprojected into its frame rather than silently
            // disappearing because their immediate parent transform differs.
            foreach (DonorAssemblyFsmEvidence assembly in
                     useDirectHierarchyOnly
                         ? Array.Empty<DonorAssemblyFsmEvidence>()
                         : matchingAssemblies)
            {
                foreach (DonorFastenerEvidence evidence in fastenerEvidence
                             .Where(value =>
                                 value.Assembly.ComponentId ==
                                 assembly.ComponentId &&
                                 value.HasSupportedWrenchSize))
                {
                    resolved.Add(ReprojectFastenerEvidence(
                        scene,
                        assembly,
                        mountTransformId,
                        scene.GetTransform(evidence.MarkerTransformId)));
                }
            }

            // The sub-frame donor FSM does not reference ActivateThis/Parent,
            // but its four BoltPM markers are unambiguous descendants of the
            // installed part. Preserve such direct hierarchy evidence too.
            DonorAssemblyFsmEvidence fallbackAssembly =
                matchingAssemblies.FirstOrDefault();
            if (fallbackAssembly != null)
            {
                if (part.Definition != null &&
                    IsStockBodyFastenedPanelDefinitionId(
                        part.Definition.DefinitionId))
                {
                    // The stock grille is the one body panel whose accepted
                    // mount is resolved by the initial mesh-candidate pass.
                    // Its two Screw/BoltPM markers live below the loose part,
                    // not below the installed renderer transform.
                    foreach (DonorTransformRecord marker in scene
                                 .GetDescendants(
                                     part.Source.Root.TransformId,
                                     includeRoot: true)
                                 .Where(value => string.Equals(
                                     scene.GetGameObjectName(
                                         value.GameObjectId),
                                     "BoltPM",
                                     StringComparison.Ordinal)))
                    {
                        resolved.Add(CreateLoosePartFastenerEvidence(
                            scene,
                            fallbackAssembly,
                            part.Source.Root.TransformId,
                            mountTransformId,
                            marker));
                    }
                }

                foreach (DonorTransformRecord marker in scene
                             .GetDescendants(mountTransformId, includeRoot: true)
                             .Where(value => string.Equals(
                                 scene.GetGameObjectName(value.GameObjectId),
                                 "BoltPM",
                                 StringComparison.Ordinal))
                             .Where(value =>
                                 !useDirectHierarchyOnly ||
                                 value.FatherTransformId != 0L &&
                                 string.Equals(
                                     scene.GetGameObjectName(
                                         scene.GetTransform(
                                             value.FatherTransformId)
                                             .GameObjectId),
                                     "Bolts",
                                     StringComparison.Ordinal)))
                {
                    resolved.Add(ReprojectFastenerEvidence(
                        scene,
                        fallbackAssembly,
                        mountTransformId,
                        marker));
                }
            }

            DonorFastenerEvidence[] unique = resolved
                .Where(value => value.HasSupportedWrenchSize)
                .GroupBy(value => value.MarkerTransformId)
                .Select(group => group.First())
                .OrderBy(value => value.MarkerTransformId)
                .ToArray();
            if (part.Definition != null && string.Equals(
                    part.Definition.DefinitionId,
                    "vehicle.satsuma.part.steering-column",
                    StringComparison.Ordinal))
            {
                // The steering-column Assembly FSM also activates the
                // tachometer branch. Its 5 mm marker (59336) must not become a
                // third column fastener; the two markers below Bolts are the
                // reviewed donor authority.
                long[] steeringColumnMarkerIds = { 57828L, 60175L };
                unique = unique
                    .Where(value => steeringColumnMarkerIds.Contains(
                        value.MarkerTransformId))
                    .ToArray();
                if (unique.Length != 2 || unique.Any(value =>
                        value.InferredWrenchMillimeters != 8))
                {
                    throw new InvalidDataException(
                        "Locked donor steering-column fasteners drifted.");
                }
            }

            return unique;
        }

        private static DonorFastenerEvidence ReprojectFastenerEvidence(
            DonorUnitySceneModel scene,
            DonorAssemblyFsmEvidence assembly,
            long mountTransformId,
            DonorTransformRecord marker)
        {
            DonorColliderRecord sphere = scene
                .GetCollidersForGameObject(
                    marker.GameObjectId,
                    includeDisabled: true,
                    includeTriggers: true)
                .FirstOrDefault(value =>
                    value.Kind == DonorColliderKind.Sphere);
            scene.GetTransformRelativeTo(
                marker.TransformId,
                mountTransformId,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out Vector3 localScale);
            int inferredWrenchMillimeters = Mathf.RoundToInt(
                Mathf.Abs(marker.LocalScale.x) * 10f);
            bool supportedWrench = inferredWrenchMillimeters != 0 &&
                Enum.IsDefined(
                    typeof(FastenerSize),
                    inferredWrenchMillimeters);
            return new DonorFastenerEvidence(
                assembly,
                mountTransformId,
                marker.TransformId,
                marker.GameObjectId,
                localPosition,
                localRotation,
                localScale,
                sphere?.Radius ?? 0f,
                inferredWrenchMillimeters,
                supportedWrench,
                ExtractDonorFastenerMaximumStage(
                    scene,
                    marker.GameObjectId));
        }

        private static MountBuild[] LockReviewedRearSuspensionMountPoses(
            IReadOnlyList<MountBuild> source,
            IReadOnlyList<LoosePartBuild> looseParts)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            MountBuild[] result = source.ToArray();
            foreach (ReviewedRearSuspensionPose reviewed in
                     ReviewedRearSuspensionPoses)
            {
                int index = Array.FindIndex(
                    result,
                    value => value.Definition != null && string.Equals(
                        value.Definition.DefinitionId,
                        reviewed.MountId,
                        StringComparison.Ordinal));
                if (index < 0)
                {
                    throw new InvalidDataException(
                        "Locked donor rear-suspension mount is missing: " +
                        reviewed.MountId);
                }

                MountBuild extracted = result[index];
                float positionDelta = Vector3.Distance(
                    extracted.LocalPosition,
                    reviewed.LocalPosition);
                float rotationDelta = Quaternion.Angle(
                    extracted.LocalRotation,
                    reviewed.LocalRotation);
                if (positionDelta > 0.002f || rotationDelta > 0.25f)
                {
                    throw new InvalidDataException(
                        "Locked donor rear-suspension pose drifted for " +
                        reviewed.MountId + ": positionDelta=" +
                        positionDelta.ToString("R", CultureInfo.InvariantCulture) +
                        "m, rotationDelta=" +
                        rotationDelta.ToString("R", CultureInfo.InvariantCulture) +
                        "deg.");
                }

                result[index] = extracted.WithPose(
                    reviewed.LocalPosition,
                    reviewed.LocalRotation);
            }

            foreach (RearSuspensionRuntimeRigPose runtimePose in
                     RearSuspensionRuntimeRigPoses)
            {
                string drumMountId =
                    "mount.satsuma.drum-brake-" + runtimePose.CornerId;
                int index = Array.FindIndex(
                    result,
                    value => value.Definition != null && string.Equals(
                        value.Definition.DefinitionId,
                        drumMountId,
                        StringComparison.Ordinal));
                if (index < 0)
                {
                    throw new InvalidDataException(
                        "Donor rear-drum mount is missing: " +
                        drumMountId);
                }

                // The serialized scene contains this socket under the donor
                // IK hierarchy. The live invariant is relative to the moving
                // visible arm root, which is the owner used by our assembly.
                MountBuild extracted = result[index];
                result[index] = extracted.WithPose(
                    runtimePose.DrumLocalPosition,
                    runtimePose.DrumLocalRotation);
            }

            foreach (ReviewedRearRoadWheelPose wheelPose in
                     ReviewedRearRoadWheelPoses)
            {
                OverrideMountPoseAndOwner(
                    result,
                    looseParts,
                    wheelPose.MountId,
                    wheelPose.OwnerPartDefinitionId,
                    wheelPose.LocalPosition,
                    wheelPose.LocalRotation);
            }

            return result;
        }

        private static MountBuild[] LockReviewedFrontSuspensionMountPoses(
            IReadOnlyList<MountBuild> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            MountBuild[] result = source.ToArray();
            foreach (FrontSuspensionRuntimeRigPose pose in
                     FrontSuspensionRuntimeRigPoses)
            {
                OverrideMountPose(
                    result,
                    "mount.satsuma.wishbone-" + pose.CornerId,
                    pose.WishboneBodyPivotLocalPosition,
                    pose.WishboneMeshZeroLocalRotation);
                OverrideMountPose(
                    result,
                    "mount.satsuma.spindle-" + pose.CornerId,
                    pose.SpindleMountLocalPosition,
                    pose.SpindleMountLocalRotation);
                OverrideMountPose(
                    result,
                    "mount.satsuma.strut-" + pose.CornerId,
                    pose.StrutTopLocalPosition,
                    pose.StrutTopLocalRotation);
                OverrideMountPose(
                    result,
                    "mount.satsuma.steering-rod-" + pose.CornerId,
                    pose.SteeringRodRootLocalPosition,
                    pose.SteeringRodRootLocalRotation);
                OverrideMountPose(
                    result,
                    "mount.satsuma.discbrake-" + pose.CornerId,
                    pose.FullDroopHubLocalPosition +
                    pose.FullDroopHubLocalRotation *
                    pose.HubToDiscBrakeLocalOffset,
                    pose.FullDroopHubLocalRotation *
                    pose.DiscBrakeLocalRotation);
                OverrideMountPose(
                    result,
                    pose.RoadWheelMountId,
                    pose.FullDroopHubLocalPosition +
                    pose.FullDroopHubLocalRotation *
                    pose.HubToRoadWheelLocalOffset,
                    pose.FullDroopHubLocalRotation *
                    pose.RoadWheelLocalRotation);
            }

            OverrideMountPose(
                result,
                "mount.satsuma.steering-rack",
                new Vector3(
                    2.99885869E-07f,
                    -0.192317009f,
                    1.00799644f),
                new Quaternion(
                    -1.49020264E-06f,
                    0.707106769f,
                    0.707106948f,
                    1.58693615E-06f));
            OverrideMountPose(
                result,
                "mount.satsuma.steering-column",
                new Vector3(
                    -0.234629244f,
                    0.07962226f,
                    0.6509674f),
                new Quaternion(
                    0.00668044249f,
                    0.4470459f,
                    0.894293547f,
                    -0.0185724664f));

            return result;
        }

        private static void ConfigureInterchangeableRoadWheels(
            IReadOnlyList<MountBuild> mounts,
            IReadOnlyList<LoosePartBuild> looseParts)
        {
            string[] roadWheelPartIds = looseParts
                .Where(value => value.Definition != null &&
                    IsRoadWheelDefinitionId(value.Definition.DefinitionId))
                .Select(value => value.Definition.DefinitionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (roadWheelPartIds.Length != 8)
            {
                throw new InvalidDataException(
                    "The donor Satsuma requires eight interchangeable stock/GT road wheels; found " +
                    roadWheelPartIds.Length.ToString(
                        CultureInfo.InvariantCulture) + ".");
            }

            string[] roadWheelMountIds =
            {
                "mount.satsuma.wheelfl-new",
                "mount.satsuma.wheelfr-new",
                "mount.satsuma.wheelrl-new",
                "mount.satsuma.wheelrr-new",
            };
            MountPointDefinition[] roadWheelMounts = roadWheelMountIds
                .Select(id => mounts.Single(value =>
                    value.Definition != null &&
                    string.Equals(
                        value.Definition.DefinitionId,
                        id,
                        StringComparison.Ordinal)).Definition)
                .ToArray();

            for (int index = 0; index < roadWheelMounts.Length; index++)
            {
                MountPointDefinition definition = roadWheelMounts[index];
                definition.Configure(
                    definition.DefinitionId,
                    definition.DisplayName,
                    definition.SocketType,
                    definition.OwnerPartDefinitionId,
                    roadWheelPartIds,
                    definition.Constraint,
                    definition.ReferenceCandidateRadiusMeters,
                    definition.Fasteners);
                EditorUtility.SetDirty(definition);
            }

            PartCompatibilityRule[] rules = roadWheelMounts
                .Select(value => PartCompatibilityRule.Create(
                    value.SocketType,
                    value.OwnerPartDefinitionId))
                .GroupBy(
                    value => value.MountSocketType + "\n" +
                        value.RequiredOwnerPartDefinitionId,
                    StringComparer.Ordinal)
                .Select(value => value.First())
                .ToArray();
            foreach (LoosePartBuild wheel in looseParts.Where(value =>
                         value.Definition != null &&
                         IsRoadWheelDefinitionId(
                             value.Definition.DefinitionId)))
            {
                PartDefinition definition = wheel.Definition;
                definition.Configure(
                    definition.DefinitionId,
                    definition.DisplayName,
                    definition.Category,
                    definition.MassKilograms,
                    definition.VisualPrefab,
                    rules);
                EditorUtility.SetDirty(definition);
            }
        }

        private static MountBuild[] ApplyReviewedAssemblyFastenerCoverage(
            IReadOnlyList<MountBuild> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            MountBuild[] result = source.ToArray();
            var wheelBoltPositions = new[]
            {
                new Vector3(0.0343f, -0.0328f, 0.0316f),
                new Vector3(0.0343f, 0.0328f, 0.0316f),
                new Vector3(0.0343f, 0.0328f, -0.0316f),
                new Vector3(0.0343f, -0.0328f, -0.0316f),
            };
            Quaternion wheelBoltRotation = new Quaternion(
                -2.3040468E-07f,
                -0.7071069f,
                -2.30404623E-07f,
                -0.7071067f);
            long[][] wheelMarkerTransformIds =
            {
                new[] { 366416L, 370664L, 376128L, 381862L },
                new[] { 361198L, 395616L, 402002L, 430452L },
                new[] { 394144L, 402390L, 410624L, 428086L },
                new[] { 369572L, 390256L, 405372L, 413334L },
            };
            string[] wheelMountIds =
            {
                "mount.satsuma.wheelfl-new",
                "mount.satsuma.wheelfr-new",
                "mount.satsuma.wheelrl-new",
                "mount.satsuma.wheelrr-new",
            };
            string[] wheelSlugs =
            {
                "wheel-fl",
                "wheel-fr",
                "wheel-rl",
                "wheel-rr",
            };

            for (int wheelIndex = 0;
                 wheelIndex < wheelMountIds.Length;
                 wheelIndex++)
            {
                var fasteners = new FastenerBuild[wheelBoltPositions.Length];
                for (int boltIndex = 0;
                     boltIndex < wheelBoltPositions.Length;
                     boltIndex++)
                {
                    string fastenerId = "fastener.satsuma." +
                                        wheelSlugs[wheelIndex] +
                                        ".lug-" + (boltIndex + 1);
                    fasteners[boltIndex] = CreateReviewedFastener(
                        fastenerId,
                        "Road wheel " +
                        wheelSlugs[wheelIndex].Substring("wheel-".Length)
                            .ToUpperInvariant() +
                        " lug nut " + (boltIndex + 1),
                        FastenerSize.Millimeter13,
                        wheelBoltPositions[boltIndex],
                        wheelBoltRotation,
                        Vector3.one * 1.3f,
                        0.012f,
                        wheelMarkerTransformIds[wheelIndex][boltIndex]);
                }

                OverrideMountFasteners(
                    result,
                    wheelMountIds[wheelIndex],
                    fasteners);
            }

            return result;
        }

        private static void ApplyReviewedFrontConnectionFasteners(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            foreach (FrontSuspensionRuntimeRigPose pose in
                     FrontSuspensionRuntimeRigPoses)
            {
                // Frozen Assembly 107329/108451 activates a separate
                // ActivateSpindleBolts root, outside the strut's ActivateThis.
                // Keep the existing three upper IDs; append the omitted four
                // lower bolts in the moving pivot_shock frame, not the top mount.
                long pivotId = pose.LeftSide ? 64686L : 44833L;
                long lowerRootId = pose.LeftSide ? 50059L : 70627L;
                long outerBoltId = pose.LeftSide ? 68946L : 70485L;
                DonorTransformRecord[] lowerMarkers = scene
                    .GetDescendants(lowerRootId, includeRoot: true)
                    .Where(value => string.Equals(
                        scene.GetGameObjectName(value.GameObjectId),
                        "BoltPM",
                        StringComparison.Ordinal))
                    .OrderBy(value => value.TransformId)
                    .ToArray();
                string mountId = "mount.satsuma.strut-" + pose.CornerId;
                int mountIndex = Array.FindIndex(mounts, value =>
                    value.Definition.DefinitionId == mountId);
                if (mountIndex < 0 || lowerMarkers.Length != 4 ||
                    mounts[mountIndex].Fasteners.Count != 3 ||
                    mounts[mountIndex].Fasteners.Any(value =>
                        value.Definition.Size != FastenerSize.Millimeter10))
                {
                    throw new InvalidDataException(
                        "Expected donor strut 3 upper / 4 lower bolts: " + mountId);
                }

                FastenerBuild[] combined = mounts[mountIndex].Fasteners
                    .Concat(lowerMarkers.Select((marker, index) =>
                        CreateReviewedFrontConnectionFastener(
                            scene,
                            marker.TransformId,
                            pivotId,
                            "fastener.satsuma.strut-" + pose.CornerId +
                                ".lower-" + (index + 1),
                            "Strut " + pose.CornerId.ToUpperInvariant() +
                                " lower bolt " + (index + 1),
                            FastenerSize.Millimeter9,
                            pose.LeftSide ? 13821L : 15127L)))
                    .ToArray();
                MountPointDefinition definition = mounts[mountIndex].Definition;
                definition.Configure(
                    definition.DefinitionId,
                    definition.DisplayName,
                    definition.SocketType,
                    definition.OwnerPartDefinitionId,
                    definition.AcceptedPartDefinitionIds,
                    definition.Constraint,
                    definition.ReferenceCandidateRadiusMeters,
                    combined.Select(value => value.Definition).ToArray());
                EditorUtility.SetDirty(definition);
                mounts[mountIndex] = mounts[mountIndex].WithFasteners(combined);

                // Rod Assembly 111295/110868 activates Bolt (12 mm, Stage 0..8)
                // AND ActivateBolt (14 mm toe adjustment, no Stage). The latter
                // must not masquerade as fastening. Retain the project save ID.
                OverrideMountFasteners(
                    mounts,
                    "mount.satsuma.steering-rod-" + pose.CornerId,
                    new[]
                    {
                        CreateReviewedFrontConnectionFastener(
                            scene,
                            outerBoltId,
                            pivotId,
                            "fastener.satsuma.steering-rod-" + pose.CornerId +
                                ".outer-joint",
                            "Steering rod " + pose.CornerId.ToUpperInvariant() +
                                " outer joint bolt",
                            FastenerSize.Millimeter12,
                            pose.LeftSide ? 24431L : 2810L),
                    });
            }
        }

        private static void ApplyReviewedFrontFastenerMeshes(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            string[] mountIds =
            {
                "mount.satsuma.sub-frame",
                "mount.satsuma.steering-rack",
                "mount.satsuma.wishbone-fl",
                "mount.satsuma.wishbone-fr",
                "mount.satsuma.spindle-fl",
                "mount.satsuma.spindle-fr",
                "mount.satsuma.strut-fl",
                "mount.satsuma.strut-fr",
                "mount.satsuma.steering-rod-fl",
                "mount.satsuma.steering-rod-fr",
            };
            var reviewedMarkers = new HashSet<long>();
            foreach (string mountId in mountIds)
            {
                int mountIndex = Array.FindIndex(mounts, value =>
                    value.Definition.DefinitionId == mountId);
                if (mountIndex < 0)
                {
                    throw new InvalidDataException(
                        "Missing reviewed front fastener mount: " + mountId);
                }

                FastenerBuild[] fasteners = mounts[mountIndex].Fasteners.ToArray();
                for (int index = 0; index < fasteners.Length; index++)
                {
                    FastenerBuild fastener = fasteners[index];
                    if (!ReviewedFrontFastenerMeshSourceGuids.TryGetValue(
                            fastener.MarkerTransformId, out string expectedGuid) ||
                        !reviewedMarkers.Add(fastener.MarkerTransformId))
                    {
                        throw new InvalidDataException(
                            "Reviewed front fastener marker coverage drifted: " +
                            fastener.Definition.DefinitionId);
                    }

                    string meshGuid = ReadReviewedFrontFastenerMeshGuid(
                        scene, fastener.MarkerTransformId, expectedGuid);
                    fasteners[index] = fastener.WithMeshSourceGuid(meshGuid);
                }

                mounts[mountIndex] = mounts[mountIndex].WithFasteners(fasteners);
            }

            if (reviewedMarkers.Count != ReviewedFrontFastenerMeshSourceGuids.Count)
            {
                throw new InvalidDataException(
                    "Expected all 30 reviewed front fastener presentation markers.");
            }
        }

        private static void ApplyReviewedRearSuspensionFastenerMeshes(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            string[] mountIds =
            {
                "mount.satsuma.trail-arm-rl",
                "mount.satsuma.trail-arm-rr",
                "mount.satsuma.shock-rl",
                "mount.satsuma.shock-rr",
                "mount.satsuma.drum-brake-rl",
                "mount.satsuma.drum-brake-rr",
            };
            var reviewedMarkers = new HashSet<long>();
            foreach (string mountId in mountIds)
            {
                int mountIndex = Array.FindIndex(mounts, value =>
                    value.Definition.DefinitionId == mountId);
                if (mountIndex < 0)
                {
                    throw new InvalidDataException(
                        "Missing reviewed rear fastener mount: " + mountId);
                }

                FastenerBuild[] fasteners = mounts[mountIndex].Fasteners.ToArray();
                for (int index = 0; index < fasteners.Length; index++)
                {
                    FastenerBuild fastener = fasteners[index];
                    if (!ReviewedRearSuspensionFastenerMeshSourceGuids.TryGetValue(
                            fastener.MarkerTransformId, out string expectedGuid) ||
                        !reviewedMarkers.Add(fastener.MarkerTransformId))
                    {
                        throw new InvalidDataException(
                            "Reviewed rear fastener marker coverage drifted: " +
                            fastener.Definition.DefinitionId);
                    }

                    string meshGuid =
                        ReadReviewedRearSuspensionFastenerMeshGuid(
                            scene,
                            fastener.MarkerTransformId,
                            expectedGuid);
                    fasteners[index] = fastener.WithMeshSourceGuid(meshGuid);
                }

                mounts[mountIndex] = mounts[mountIndex].WithFasteners(fasteners);
            }

            if (reviewedMarkers.Count !=
                ReviewedRearSuspensionFastenerMeshSourceGuids.Count)
            {
                throw new InvalidDataException(
                    "Expected all 12 reviewed rear fastener presentation markers.");
            }
        }

        private static void ApplyReviewedHandbrakeFastenerMeshes(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            int mountIndex = Array.FindIndex(mounts, value =>
                value.Definition.DefinitionId == "mount.satsuma.handbrake");
            long[] markerIds = { 55735L, 63139L, 65746L, 68029L, 68693L };
            if (mountIndex < 0 || mounts[mountIndex].Fasteners.Count != markerIds.Length)
            {
                throw new InvalidDataException("Locked donor handbrake mount/fasteners drifted.");
            }

            FastenerBuild[] fasteners = mounts[mountIndex].Fasteners.ToArray();
            for (int index = 0; index < fasteners.Length; index++)
            {
                FastenerBuild fastener = fasteners[index];
                if (fastener.MarkerTransformId != markerIds[index])
                {
                    throw new InvalidDataException("Locked donor handbrake marker order drifted.");
                }

                // All five actual donor MeshFilters use the short bolt, including
                // the smaller 5 mm drive attachment. BoltPM alone does not prove type.
                fasteners[index] = fastener.WithMeshSourceGuid(
                    ReadReviewedFastenerMeshGuid(scene, markerIds[index],
                        DonorShortBoltMeshSourceGuid, "handbrake"));
            }

            mounts[mountIndex] = mounts[mountIndex].WithFasteners(fasteners);
        }

        private static void ApplyAdditionalEngineFastenerMeshes(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            foreach (Phase1SatsumaEngineAdditionalFastenerPresentation.Binding binding in
                     Phase1SatsumaEngineAdditionalFastenerPresentation.ReviewedBindings)
            {
                int mountIndex = Array.FindIndex(mounts, value =>
                    value.Definition.DefinitionId == binding.MountId);
                if (mountIndex < 0)
                {
                    throw new InvalidDataException("Missing reviewed engine mount: " + binding.MountId);
                }
                FastenerBuild[] fasteners = mounts[mountIndex].Fasteners.ToArray();
                int index = Array.FindIndex(fasteners, value =>
                    value.Definition.DefinitionId == binding.FastenerId);
                if (index < 0 || fasteners[index].MarkerTransformId != binding.MarkerTransformId ||
                    (int)fasteners[index].Definition.Size != binding.SizeMillimeters ||
                    fasteners[index].MeshSourceGuid != DonorFastenerMeshSourceGuid)
                {
                    throw new InvalidDataException("Reviewed engine fastener binding drifted: " + binding.FastenerId);
                }
                Phase1SatsumaEngineAdditionalFastenerPresentation.ValidateDonorBinding(scene, binding);
                fasteners[index] = fasteners[index].WithMeshSourceGuid(binding.MeshSourceGuid);
                mounts[mountIndex] = mounts[mountIndex].WithFasteners(fasteners);
            }
        }

        private static void ApplyReviewedEngineFastenerMeshes(
            DonorUnitySceneModel scene,
            MountBuild[] mounts)
        {
            IReadOnlyList<Phase1SatsumaEngineFastenerPresentation.
                ReviewedFastenerBinding> bindings =
                Phase1SatsumaEngineFastenerPresentation.ReviewedBindings;
            var reviewedMarkers = new HashSet<long>();
            foreach (IGrouping<string, Phase1SatsumaEngineFastenerPresentation.
                         ReviewedFastenerBinding> group in bindings.GroupBy(
                         value => value.MountId,
                         StringComparer.Ordinal))
            {
                int mountIndex = Array.FindIndex(mounts, value =>
                    value.Definition.DefinitionId == group.Key);
                Phase1SatsumaEngineFastenerPresentation.
                    ReviewedFastenerBinding[] expected = group.ToArray();
                if (mountIndex < 0 ||
                    mounts[mountIndex].Fasteners.Count != expected.Length)
                {
                    throw new InvalidDataException(
                        "Locked donor engine fastener mount drifted: " +
                        group.Key);
                }

                FastenerBuild[] fasteners = mounts[mountIndex].Fasteners.ToArray();
                for (int index = 0; index < fasteners.Length; index++)
                {
                    FastenerBuild fastener = fasteners[index];
                    Phase1SatsumaEngineFastenerPresentation.
                        ReviewedFastenerBinding binding = expected[index];
                    if (fastener.Definition.DefinitionId != binding.FastenerId ||
                        fastener.Definition.Size != binding.ExpectedSize ||
                        fastener.MarkerTransformId !=
                        binding.DonorMarkerTransformId ||
                        fastener.MeshSourceGuid != DonorFastenerMeshSourceGuid ||
                        !reviewedMarkers.Add(binding.DonorMarkerTransformId))
                    {
                        throw new InvalidDataException(
                            "Locked donor engine fastener order drifted: " +
                            binding.FastenerId);
                    }

                    string expectedMeshGuid = binding.ExpectedMeshSourceGuid;
                    if (expectedMeshGuid != DonorShortBoltMeshSourceGuid &&
                        expectedMeshGuid != DonorLongBoltMeshSourceGuid)
                    {
                        throw new InvalidDataException(
                            "Locked donor engine fastener mesh is unsupported: " +
                            binding.FastenerId);
                    }

                    fasteners[index] = fastener.WithMeshSourceGuid(
                        ReadReviewedFastenerMeshGuid(
                            scene,
                            binding.DonorMarkerTransformId,
                            expectedMeshGuid,
                            "engine",
                            binding.ExpectedDonorRendererChildLocalPosition));
                }

                mounts[mountIndex] = mounts[mountIndex].WithFasteners(fasteners);
            }

            if (reviewedMarkers.Count != bindings.Count)
            {
                throw new InvalidDataException(
                    "Expected all 16 reviewed engine fastener presentation markers.");
            }
        }

        internal static string ReadReviewedFrontFastenerMeshGuid(
            DonorUnitySceneModel scene,
            long markerTransformId,
            string expectedMeshGuid) => ReadReviewedFastenerMeshGuid(
                scene,
                markerTransformId,
                expectedMeshGuid,
                "front");

        internal static string ReadReviewedRearSuspensionFastenerMeshGuid(
            DonorUnitySceneModel scene,
            long markerTransformId,
            string expectedMeshGuid) => ReadReviewedFastenerMeshGuid(
                scene,
                markerTransformId,
                expectedMeshGuid,
                "rear suspension");

        private static string ReadReviewedFastenerMeshGuid(
            DonorUnitySceneModel scene,
            long markerTransformId,
            string expectedMeshGuid,
            string scope,
            Vector3? expectedVisualLocalPosition = null)
        {
            IReadOnlyList<DonorStaticRendererRecord> renderers =
                scene.GetStaticRenderersBelowIncludingInactive(markerTransformId);
            if (renderers.Count != 1)
            {
                throw new InvalidDataException(
                    "Expected one reviewed " + scope +
                    " fastener renderer: " +
                    markerTransformId);
            }

            DonorStaticRendererRecord renderer = renderers[0];
            DonorTransformRecord visual = scene.GetTransform(
                scene.GetTransformIdForGameObject(renderer.GameObjectId));
            Vector3 expectedLocalPosition =
                expectedVisualLocalPosition ?? Vector3.zero;
            // A changed child frame needs explicit pose/animation review rather
            // than silently flattening that transform onto the marker.
            if (visual.FatherTransformId != markerTransformId ||
                !float.IsFinite(expectedLocalPosition.x) ||
                !float.IsFinite(expectedLocalPosition.y) ||
                !float.IsFinite(expectedLocalPosition.z) ||
                !float.IsFinite(visual.LocalPosition.x) ||
                !float.IsFinite(visual.LocalPosition.y) ||
                !float.IsFinite(visual.LocalPosition.z) ||
                (visual.LocalPosition - expectedLocalPosition).sqrMagnitude >
                    0.000000000001f ||
                Quaternion.Angle(visual.LocalRotation, Quaternion.identity) > 0.001f ||
                Vector3.Distance(visual.LocalScale, Vector3.one) > 0.00001f ||
                !string.Equals(renderer.MeshGuid, expectedMeshGuid,
                    StringComparison.OrdinalIgnoreCase) ||
                renderer.MaterialGuids.Count != 1 ||
                !string.Equals(renderer.MaterialGuids[0],
                    DonorFastenerMaterialSourceGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Reviewed " + scope +
                    " fastener mesh/material/child frame drifted: " +
                    markerTransformId);
            }

            return renderer.MeshGuid;
        }

        private static FastenerBuild CreateReviewedFrontConnectionFastener(
            DonorUnitySceneModel scene,
            long markerTransformId,
            long pivotTransformId,
            string definitionId,
            string displayName,
            FastenerSize expectedSize,
            long expectedOwnerGameObjectId)
        {
            DonorTransformRecord marker = scene.GetTransform(markerTransformId);
            // PLAYER/Raycast Check 105041 compares Bolt.localScale.x with
            // ToolWrenchSize. This is the donor's tool rule, not mesh guessing.
            bool hasStagedOwner = scene.GetMonoBehaviours(marker.GameObjectId)
                .Any(value => IsStagedFrontConnectionScrew(
                    value.SerializedBody, expectedOwnerGameObjectId));
            if (!hasStagedOwner ||
                Mathf.RoundToInt(Mathf.Abs(marker.LocalScale.x) * 10f) !=
                    (int)expectedSize ||
                ExtractDonorFastenerMaximumStage(scene, marker.GameObjectId) != 8)
            {
                throw new InvalidDataException(
                    "Reviewed front Screw size/stages drifted: " + definitionId);
            }

            scene.GetTransformRelativeTo(
                markerTransformId,
                pivotTransformId,
                out Vector3 position,
                out Quaternion rotation,
                out Vector3 scale);
            DonorColliderRecord sphere = scene.GetCollidersForGameObject(
                    marker.GameObjectId,
                    includeDisabled: true,
                    includeTriggers: true)
                .Single(value => value.Kind == DonorColliderKind.Sphere);
            return CreateReviewedFastener(
                definitionId,
                displayName,
                expectedSize,
                position,
                rotation,
                scale,
                sphere.Radius,
                markerTransformId);
        }

        internal static bool IsStagedFrontConnectionScrew(
            string serializedBody,
            long expectedOwnerGameObjectId)
        {
            // Numeric events also exist on the toe adjuster. Require an actual
            // integer Stage variable plus the correct PartAssembled owner.
            Match integers = Regex.Match(serializedBody,
                @"(?ms)^    variables:\r?\n.*?^      intVariables:[ \t]*\r?\n(?<items>.*?)(?=^      \w+Variables:)",
                RegexOptions.CultureInvariant);
            return integers.Success && Regex.IsMatch(serializedBody,
                       @"(?m)^    name:\s*Screw\s*$",
                       RegexOptions.CultureInvariant) &&
                   Regex.IsMatch(integers.Groups["items"].Value,
                       @"(?m)^        name:\s*Stage\s*$",
                       RegexOptions.CultureInvariant) &&
                   ExtractFsmGameObjectVariable(serializedBody, "PartAssembled") ==
                       expectedOwnerGameObjectId;
        }

        private static FastenerBuild CreateReviewedFastener(
            string fastenerId,
            string displayName,
            FastenerSize size,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 sourceLocalScale,
            float colliderRadiusMeters,
            long markerTransformId)
        {
            var definition = ScriptableObject
                .CreateInstance<FastenerDefinition>();
            definition.Configure(
                fastenerId,
                displayName,
                size,
                8,
                FastenerDirection.ClockwiseToTighten,
                insertOnInstall: true,
                blocksRemoval: true,
                ToolCompatibilityRule.Create("Wrench", size));
            AssetDatabase.CreateAsset(
                definition,
                FastenerDefinitionRoot + "/" +
                SanitizeName(fastenerId) + ".asset");
            return new FastenerBuild(
                definition,
                localPosition,
                localRotation,
                sourceLocalScale,
                colliderRadiusMeters,
                markerTransformId);
        }

        private static void ConfigureFastenerGroups(
            IReadOnlyList<MountBuild> mounts,
            IReadOnlyList<DonorBoltCheckEvidence> boltChecks)
        {
            ValidateLockedBoltCheckEvidence(boltChecks);

            foreach (MountBuild mount in mounts)
            {
                FastenerDefinition[] fasteners = mount.Definition.Fasteners ??
                    Array.Empty<FastenerDefinition>();
                FastenerGroupDefinition group = fasteners.Length == 0
                    ? null
                    : FastenerGroupDefinition.CreateCompatibility(fasteners);
                mount.Definition.ConfigureFastenerGroup(group);
                EditorUtility.SetDirty(mount.Definition);
            }

            ConfigureFrontFastenerGroups(mounts);
            ConfigureOtherFastenerGroups(mounts);
        }

        private static void ConfigureFrontFastenerGroups(
            IReadOnlyList<MountBuild> mounts)
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.wishbone-" + corner,
                    new[] { FastenerSize.Millimeter10, FastenerSize.Millimeter10 },
                    aggregateMaximumTightness: 16,
                    boltedOnThreshold: 2,
                    boltedOffThreshold: 0);
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.spindle-" + corner,
                    new[] { FastenerSize.Millimeter12 },
                    aggregateMaximumTightness: 8,
                    boltedOnThreshold: 2,
                    boltedOffThreshold: 0);
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.steering-rod-" + corner,
                    new[] { FastenerSize.Millimeter12 },
                    aggregateMaximumTightness: 8,
                    boltedOnThreshold: 8,
                    boltedOffThreshold: 0);
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.strut-" + corner,
                    new[]
                    {
                        FastenerSize.Millimeter10, FastenerSize.Millimeter10,
                        FastenerSize.Millimeter10, FastenerSize.Millimeter9,
                        FastenerSize.Millimeter9, FastenerSize.Millimeter9,
                        FastenerSize.Millimeter9,
                    },
                    aggregateMaximumTightness: 56,
                    boltedOnThreshold: 3,
                    boltedOffThreshold: 0);
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.discbrake-" + corner,
                    new[] { FastenerSize.Millimeter14 },
                    aggregateMaximumTightness: 8,
                    boltedOnThreshold: 2,
                    boltedOffThreshold: 0);
                ConfigureLockedFastenerGroup(
                    mounts,
                    "mount.satsuma.halfshaft-" + corner,
                    new[]
                    {
                        FastenerSize.Millimeter9, FastenerSize.Millimeter9,
                        FastenerSize.Millimeter9,
                    },
                    aggregateMaximumTightness: 24,
                    boltedOnThreshold: 2,
                    boltedOffThreshold: 0);
            }

            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.sub-frame",
                Enumerable.Repeat(FastenerSize.Millimeter10, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 26,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.steering-rack",
                Enumerable.Repeat(FastenerSize.Millimeter9, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 24,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.steering-column",
                new[] { FastenerSize.Millimeter8, FastenerSize.Millimeter8 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 10,
                boltedOffThreshold: 0);
        }

        private static void ConfigureOtherFastenerGroups(
            IReadOnlyList<MountBuild> mounts)
        {
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.trail-arm-rl",
                new[] { FastenerSize.Millimeter12, FastenerSize.Millimeter12 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 12,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.trail-arm-rr",
                new[] { FastenerSize.Millimeter12, FastenerSize.Millimeter12 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 12,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.drum-brake-rl",
                new[] { FastenerSize.Millimeter14 },
                aggregateMaximumTightness: 8,
                boltedOnThreshold: 8,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.drum-brake-rr",
                new[] { FastenerSize.Millimeter14 },
                aggregateMaximumTightness: 8,
                boltedOnThreshold: 8,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.shock-rl",
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter6,
                    FastenerSize.Millimeter6,
                },
                aggregateMaximumTightness: 24,
                boltedOnThreshold: 2,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.shock-rr",
                new[]
                {
                    FastenerSize.Millimeter12,
                    FastenerSize.Millimeter6,
                    FastenerSize.Millimeter6,
                },
                aggregateMaximumTightness: 24,
                boltedOnThreshold: 2,
                boltedOffThreshold: 0);

            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.bumper-rear",
                new[] { FastenerSize.Millimeter8, FastenerSize.Millimeter8 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 6,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.fender-right",
                Enumerable.Repeat(FastenerSize.Millimeter5, 5).ToArray(),
                aggregateMaximumTightness: 40,
                boltedOnThreshold: 28,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.grille",
                new[] { FastenerSize.Millimeter6, FastenerSize.Millimeter6 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 6,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.bootlid",
                Enumerable.Repeat(FastenerSize.Millimeter6, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 24,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.door-left",
                Enumerable.Repeat(FastenerSize.Millimeter10, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 28,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.hood",
                Enumerable.Repeat(FastenerSize.Millimeter6, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 8,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.door-right",
                Enumerable.Repeat(FastenerSize.Millimeter10, 4).ToArray(),
                aggregateMaximumTightness: 32,
                boltedOnThreshold: 28,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.bumper-front",
                new[] { FastenerSize.Millimeter8, FastenerSize.Millimeter8 },
                aggregateMaximumTightness: 16,
                boltedOnThreshold: 6,
                boltedOffThreshold: 0);
            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.fender-left",
                Enumerable.Repeat(FastenerSize.Millimeter5, 5).ToArray(),
                aggregateMaximumTightness: 40,
                boltedOnThreshold: 28,
                boltedOffThreshold: 0);

            ConfigureLockedFastenerGroup(
                mounts,
                "mount.satsuma.handbrake",
                new[]
                {
                    FastenerSize.Millimeter8, FastenerSize.Millimeter8,
                    FastenerSize.Millimeter8, FastenerSize.Millimeter8,
                    FastenerSize.Millimeter5,
                },
                aggregateMaximumTightness: 40,
                boltedOnThreshold: 6,
                boltedOffThreshold: 0);

            foreach (string wheelMountId in new[]
                     {
                         "mount.satsuma.wheelfl-new",
                         "mount.satsuma.wheelfr-new",
                         "mount.satsuma.wheelrl-new",
                         "mount.satsuma.wheelrr-new",
                     })
            {
                ConfigureLockedFastenerGroup(
                    mounts,
                    wheelMountId,
                    new[]
                    {
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                        FastenerSize.Millimeter13,
                    },
                    aggregateMaximumTightness: 32,
                    boltedOnThreshold: 1,
                    boltedOffThreshold: 0,
                    retentionPolicy:
                        FastenerSpeedRetentionPolicy.DonorWheelBoltCheck,
                    looseBreakSpeedKph: 5f,
                    partialCheckSpeedKph: 33f,
                    chanceDivisor: 100f,
                    breakAction: FastenerBreakAction.DetachInstalledPart);
            }
        }

        private static void ValidateLockedBoltCheckEvidence(
            IReadOnlyList<DonorBoltCheckEvidence> evidence)
        {
            DonorBoltCheckEvidence[] rows = evidence?.ToArray() ??
                                            Array.Empty<DonorBoltCheckEvidence>();
            if (rows.Length == 0)
            {
                throw new InvalidDataException(
                    "Donor Satsuma contains no extractable BoltCheck FSMs.");
            }

            ValidateLockedPartBoltCheck(rows, "/IK_wishbone_fl/wishbone fl", 2f);
            ValidateLockedPartBoltCheck(rows, "/IK_wishbone_fr/wishbone fr", 2f);
            ValidateLockedPartBoltCheck(rows, "/SpindleFL/OFFSET/spindle fl", 2f);
            ValidateLockedPartBoltCheck(rows, "/SpindleFR/OFFSET/spindle fr", 2f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/steering rod fl", 8f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/steering rod fr", 8f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/sub frame(xxxxx)", 26f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/steering rack(xxxxx)", 24f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Dashboard/Steering/CarSteeringPivot/steering column(xxxxx)",
                10f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/FL/strut fl(xxxxx)", 3f);
            ValidateLockedPartBoltCheck(rows, "/Chassis/FR/strut fr(xxxxx)", 3f);
            ValidateLockedPartBoltCheck(
                rows,
                "/FL/AckerFL/wheelFL/TireFL/OFFSET/discbrake(flxxx)",
                2f);
            ValidateLockedPartBoltCheck(
                rows,
                "/FR/AckerFR/wheelFR/TireFR/OFFSET/discbrake(frxxx)",
                2f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/FL/halfshaft_fl/halfshaft(xxxxx)",
                2f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/FR/halfshaft_fr/halfshaft(xxxxx)",
                2f);
            ValidateLockedPartBoltCheck(rows, "/MiscParts/HandBrake/handbrake(xxxxx)", 6f);

            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/RL/TrailArm/Arm/trail arm rl",
                12f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/RR/TrailArm/Arm/trail arm rr",
                12f);
            ValidateLockedPartBoltCheck(
                rows,
                "/RL/wheelRL/TireRL/drumbrake rl",
                8f);
            ValidateLockedPartBoltCheck(
                rows,
                "/RR/wheelRR/TireRR/drumbrake rr",
                8f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/RL/ShockTop/Arm/shock absorber(rl",
                2f);
            ValidateLockedPartBoltCheck(
                rows,
                "/Chassis/RR/ShockTop/Arm/shock absorber(rr",
                2f);

            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/bumper rear(Clone)",
                6f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/fender right(Clone)",
                28f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/grille(Clone)",
                6f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/bootlid(Clone)",
                24f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/door left(Clone)",
                28f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/hood(Clone)",
                8f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/door right(Clone)",
                28f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/bumper front(Clone)",
                6f);
            ValidateLockedPartBoltCheck(
                rows,
                "CARPARTS/PartsCar/fender left(Clone)",
                28f);

            DonorBoltCheckEvidence[] wheelPolicies = rows
                .Where(value => value.IsWheelRetentionPolicy)
                .ToArray();
            if (wheelPolicies.Length == 0 || wheelPolicies.Any(value =>
                    !value.ReferencesBreak ||
                    !value.ReferencesSpeed ||
                    !value.HasChanceFormula ||
                    !Mathf.Approximately(value.BoltedYes, 1f) ||
                    !Mathf.Approximately(value.BoltedNo, 0f) ||
                    !Mathf.Approximately(value.LooseBreakSpeedKph, 5f) ||
                    !Mathf.Approximately(value.PartialCheckSpeedKph, 33f) ||
                    !Mathf.Approximately(
                        value.ChanceMaximumTightness,
                        32f) ||
                    !Mathf.Approximately(value.ChanceDivisor, 100f)))
            {
                throw new InvalidDataException(
                    "Expected donor wheel Use/BoltCheck policies " +
                    "with on/off 1/0, BREAK 5/33 km/h and Chance 32/100.");
            }
        }

        private static void ValidateLockedPartBoltCheck(
            IReadOnlyList<DonorBoltCheckEvidence> rows,
            string hierarchyToken,
            float boltedOnThreshold)
        {
            DonorBoltCheckEvidence[] matches = rows.Where(value =>
                    value.HierarchyPath.IndexOf(
                        hierarchyToken,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    value.HierarchyPath.IndexOf(
                        "rally",
                        StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();
            if (matches.Length != 1 ||
                !matches[0].HasBoltedYes ||
                !matches[0].HasBoltedNo ||
                !matches[0].ReferencesTightness ||
                !Mathf.Approximately(
                    matches[0].BoltedYes,
                    boltedOnThreshold) ||
                !Mathf.Approximately(matches[0].BoltedNo, 0f))
            {
                throw new InvalidDataException(
                    "Locked donor BoltCheck drifted for " + hierarchyToken +
                    ": expected unique on/off " +
                    boltedOnThreshold.ToString(
                        "R",
                        CultureInfo.InvariantCulture) +
                    "/0 with Tightness.");
            }
        }

        private static void ConfigureLockedFastenerGroup(
            IReadOnlyList<MountBuild> mounts,
            string mountId,
            FastenerSize[] expectedSizes,
            int aggregateMaximumTightness,
            int boltedOnThreshold,
            int boltedOffThreshold,
            FastenerSpeedRetentionPolicy retentionPolicy =
                FastenerSpeedRetentionPolicy.None,
            float looseBreakSpeedKph = 0f,
            float partialCheckSpeedKph = 0f,
            float chanceDivisor = 100f,
            FastenerBreakAction breakAction = FastenerBreakAction.None)
        {
            MountBuild mount = mounts.SingleOrDefault(value =>
                value.Definition != null && string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (mount.Definition == null)
            {
                throw new InvalidDataException(
                    "Locked donor BoltCheck mount is missing: " + mountId);
            }

            FastenerDefinition[] fasteners = mount.Definition.Fasteners ??
                Array.Empty<FastenerDefinition>();
            FastenerSize[] actualSizes = fasteners
                .Select(value => value.Size)
                .OrderBy(value => (int)value)
                .ToArray();
            FastenerSize[] sortedExpected = (expectedSizes ??
                                              Array.Empty<FastenerSize>())
                .OrderBy(value => (int)value)
                .ToArray();
            if (!actualSizes.SequenceEqual(sortedExpected) ||
                fasteners.Any(value => value.MaximumStage != 8) ||
                fasteners.Sum(value => value.MaximumStage) !=
                aggregateMaximumTightness)
            {
                throw new InvalidDataException(
                    "Locked donor BoltCheck fasteners drifted for " +
                    mountId + ": expected sizes " +
                    string.Join("/", sortedExpected.Select(value =>
                        ((int)value).ToString(CultureInfo.InvariantCulture))) +
                    " and aggregate max " + aggregateMaximumTightness +
                    "; actual sizes " +
                    string.Join("/", actualSizes.Select(value =>
                        ((int)value).ToString(CultureInfo.InvariantCulture))) +
                    ", stages " +
                    string.Join("/", fasteners.Select(value =>
                        value.MaximumStage.ToString(
                            CultureInfo.InvariantCulture))) +
                    ", aggregate " +
                    fasteners.Sum(value => value.MaximumStage).ToString(
                        CultureInfo.InvariantCulture) + ".");
            }

            var group = new FastenerGroupDefinition();
            group.Configure(
                fasteners.Select(value => value.DefinitionId).ToArray(),
                aggregateMaximumTightness,
                boltedOnThreshold,
                boltedOffThreshold,
                retentionPolicy,
                looseBreakSpeedKph,
                partialCheckSpeedKph,
                chanceDivisor,
                breakAction);
            mount.Definition.ConfigureFastenerGroup(group);
            EditorUtility.SetDirty(mount.Definition);
        }

        private static void ValidateReferencedBoltCoverage(
            IReadOnlyList<MountBuild> mounts,
            IReadOnlyList<LoosePartBuild> looseParts,
            params IReadOnlyList<DonorAssemblyFsmEvidence>[] evidenceSets)
        {
            var failures = new List<string>();
            foreach (DonorAssemblyFsmEvidence assembly in evidenceSets
                         .Where(set => set != null)
                         .SelectMany(set => set)
                         .Where(value => value.ReferencesBolts)
                         .OrderBy(value => value.ComponentId))
            {
                // Some donor Assembly templates keep the generic "Bolts"
                // variable/actions even though that concrete instance binds
                // the variable to null.  The serialized null binding is the
                // explicit evidence-backed exception required by this import
                // contract; a non-null donor Bolts object is never exempt.
                if (assembly.BoltsGameObjectId == 0L)
                {
                    continue;
                }

                LoosePartBuild[] candidates = ResolveAssemblyPartCandidates(
                    assembly,
                    looseParts);
                if (candidates.Length == 0)
                {
                    failures.Add(
                        assembly.ComponentId + ":" + assembly.TriggerName +
                        " has ReferencesBolts=true but no resolved part " +
                        "candidate and no evidence-backed exception.");
                    continue;
                }

                var candidateIds = new HashSet<string>(
                    candidates.Select(value =>
                        value.Definition.DefinitionId),
                    StringComparer.Ordinal);
                MountBuild[] compatibleMounts = mounts
                    .Where(value => value.Definition != null &&
                        value.Definition.AcceptedPartDefinitionIds.Any(
                            candidateIds.Contains))
                    .OrderBy(value => value.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .ToArray();
                MountBuild[] uncovered = compatibleMounts
                    .Where(value =>
                        value.Definition.Fasteners == null ||
                        value.Definition.Fasteners.Length == 0)
                    .ToArray();
                if (compatibleMounts.Length == 0 || uncovered.Length > 0)
                {
                    failures.Add(
                        assembly.ComponentId + ":" + assembly.TriggerName +
                        " has ReferencesBolts=true but generated mounts are " +
                        (compatibleMounts.Length == 0
                            ? "missing"
                            : "unfastened: " + string.Join(
                                "/",
                                uncovered.Select(value =>
                                    value.Definition.DefinitionId))) +
                        "; candidates=" + string.Join(
                            "/",
                            candidateIds.OrderBy(value => value,
                                StringComparer.Ordinal)) +
                        ". No evidence-backed exception exists.");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidDataException(
                    "Donor ReferencesBolts coverage validation failed (" +
                    failures.Count + "):\n" + string.Join("\n", failures));
            }
        }

        private static void OverrideMountFasteners(
            MountBuild[] mounts,
            string mountId,
            IReadOnlyList<FastenerBuild> fasteners)
        {
            int index = Array.FindIndex(
                mounts,
                value => value.Definition != null && string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidDataException(
                    "Reviewed donor fastener mount is missing: " + mountId);
            }

            MountBuild mount = mounts[index];
            if (mount.Fasteners.Count != 0)
            {
                throw new InvalidDataException(
                    "Reviewed donor fasteners would overwrite extracted " +
                    "fasteners on " + mountId + ".");
            }

            FastenerBuild[] fastenerArray = fasteners?.ToArray() ??
                                            Array.Empty<FastenerBuild>();
            MountPointDefinition definition = mount.Definition;
            definition.Configure(
                definition.DefinitionId,
                definition.DisplayName,
                definition.SocketType,
                definition.OwnerPartDefinitionId,
                definition.AcceptedPartDefinitionIds,
                definition.Constraint,
                definition.ReferenceCandidateRadiusMeters,
                fastenerArray.Select(value => value.Definition).ToArray());
            EditorUtility.SetDirty(definition);
            mounts[index] = mount.WithFasteners(fastenerArray);
        }

        private static void OverrideMountPose(
            MountBuild[] mounts,
            string mountId,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            int index = Array.FindIndex(
                mounts,
                value => value.Definition != null && string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidDataException(
                    "Locked donor front-suspension mount is missing: " +
                    mountId);
            }

            // These are live runtime poses. The serialized donor scene stores
            // the front pieces below its IK carrier and therefore extracts a
            // static pose that is wrong as soon as suspension travel begins.
            // Do not compare that flattened staging pose to this runtime lock.
            mounts[index] = mounts[index].WithPose(
                localPosition,
                localRotation);
        }

        private static void OverrideMountPoseAndOwner(
            MountBuild[] mounts,
            IReadOnlyList<LoosePartBuild> looseParts,
            string mountId,
            string ownerPartDefinitionId,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            int index = Array.FindIndex(
                mounts,
                value => value.Definition != null && string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidDataException(
                    "Donor mount is missing: " + mountId);
            }

            MountBuild extracted = mounts[index];
            MountPointDefinition definition = extracted.Definition;
            definition.Configure(
                definition.DefinitionId,
                definition.DisplayName,
                definition.SocketType,
                ownerPartDefinitionId,
                definition.AcceptedPartDefinitionIds,
                definition.Constraint,
                definition.ReferenceCandidateRadiusMeters,
                definition.Fasteners);

            foreach (string acceptedPartId in
                     definition.AcceptedPartDefinitionIds)
            {
                LoosePartBuild part = looseParts.Single(value =>
                    value.Definition != null && string.Equals(
                        value.Definition.DefinitionId,
                        acceptedPartId,
                        StringComparison.Ordinal));
                PartCompatibilityRule[] rules = part.Definition
                    .CompatibilityRules
                    .Where(value => value != null)
                    .Concat(new[]
                    {
                        PartCompatibilityRule.Create(
                            definition.SocketType,
                            ownerPartDefinitionId),
                    })
                    .GroupBy(
                        value => value.MountSocketType + "\n" +
                            value.RequiredOwnerPartDefinitionId,
                        StringComparer.Ordinal)
                    .Select(value => value.First())
                    .ToArray();
                part.Definition.Configure(
                    part.Definition.DefinitionId,
                    part.Definition.DisplayName,
                    part.Definition.Category,
                    part.Definition.MassKilograms,
                    part.Definition.VisualPrefab,
                    rules);
            }

            mounts[index] = extracted.WithPoseAndOwner(
                localPosition,
                localRotation,
                ownerPartDefinitionId);
        }

        private static void AddEngineAssemblyMount(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            ICollection<MountBuild> results)
        {
            LoosePartBuild engineBlock = loosePartBuilds.Single(value =>
                string.Equals(
                    value.Definition.DefinitionId,
                    "vehicle.satsuma.part.engine-block",
                    StringComparison.Ordinal));
            DonorAssemblyFsmEvidence[] engineMountFsms = assemblyEvidence
                .Where(value => value.TriggerName.StartsWith(
                    "Trigger_motor",
                    StringComparison.Ordinal))
                .OrderBy(value => value.TriggerName, StringComparer.Ordinal)
                .ToArray();
            if (engineMountFsms.Length != 3)
            {
                throw new InvalidDataException(
                    "Expected exactly three donor Satsuma motor mount FSMs; found " +
                    engineMountFsms.Length + ".");
            }

            long[] blockTriggerTransformIds =
            {
                RequireFsmGameObjectTransform(
                    scene,
                    engineMountFsms[0].ComponentId,
                    "Trigger"),
                RequireFsmGameObjectTransform(
                    scene,
                    engineMountFsms[1].ComponentId,
                    "Trigger"),
                RequireFsmGameObjectTransform(
                    scene,
                    engineMountFsms[2].ComponentId,
                    "Trigger"),
            };
            Vector3[] blockPoints = blockTriggerTransformIds
                .Select(value => GetLocalPosition(
                    scene,
                    value,
                    engineBlock.Source.Root.TransformId))
                .ToArray();
            Vector3[] chassisPoints = engineMountFsms
                .Select(value => GetLocalPosition(
                    scene,
                    value.TriggerTransformId,
                    satsumaRoot.TransformId))
                .ToArray();
            SolveThreePointRigidTransform(
                blockPoints,
                chassisPoints,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out float maximumResidualMeters);
            if (maximumResidualMeters > 0.01f)
            {
                throw new InvalidDataException(
                    "Donor Satsuma motor mount triangulation residual exceeds 10 mm: " +
                    maximumResidualMeters.ToString("R", CultureInfo.InvariantCulture));
            }

            DonorFastenerEvidence[] markers = engineMountFsms
                .Select((assembly, index) =>
                {
                    long markerTransformId = RequireFsmGameObjectTransform(
                        scene,
                        assembly.ComponentId,
                        "Bolt");
                    return CreateLoosePartFastenerEvidence(
                        scene,
                        assembly,
                        satsumaRoot.TransformId,
                        assembly.TriggerTransformId,
                        scene.GetTransform(markerTransformId));
                })
                .Where(value => value.HasSupportedWrenchSize)
                .OrderBy(value => value.Assembly.TriggerName, StringComparer.Ordinal)
                .ToArray();
            if (markers.Length != 3)
            {
                throw new InvalidDataException(
                    "Expected one supported donor bolt for each Satsuma motor mount.");
            }

            FastenerBuild[] fasteners = BuildFastenerAssets(
                "engine-assembly",
                engineBlock.Definition.DisplayName,
                markers);
            string mountId = "mount.satsuma.engine-assembly";
            string socketType = "satsuma.socket.engine-assembly";
            var definition = ScriptableObject.CreateInstance<MountPointDefinition>();
            definition.Configure(
                mountId,
                "Satsuma: complete engine assembly",
                socketType,
                "vehicle.satsuma.part.body-shell",
                new[] { engineBlock.Definition.DefinitionId },
                new MountConstraint(0.35f, 40f, 1.25f, 0f),
                0.1f,
                fasteners.Select(value => value.Definition).ToArray());
            AssetDatabase.CreateAsset(
                definition,
                MountDefinitionRoot + "/" + SanitizeName(mountId) + ".asset");
            results.Add(new MountBuild(
                engineBlock.Source,
                definition,
                localPosition,
                localRotation,
                Vector3.one,
                engineMountFsms[0].TriggerTransformId,
                fasteners));

            engineBlock.Definition.Configure(
                engineBlock.Definition.DefinitionId,
                engineBlock.Definition.DisplayName,
                engineBlock.Definition.Category,
                engineBlock.Definition.MassKilograms,
                engineBlock.Definition.VisualPrefab,
                engineBlock.Definition.CompatibilityRules
                    .Concat(new[]
                    {
                        PartCompatibilityRule.Create(
                            socketType,
                            "vehicle.satsuma.part.body-shell"),
                    })
                    .ToArray());
            EditorUtility.SetDirty(engineBlock.Definition);
            WriteEngineMountTriangulationAudit(
                scene,
                engineMountFsms,
                blockTriggerTransformIds,
                blockPoints,
                chassisPoints,
                localPosition,
                localRotation,
                maximumResidualMeters);
        }

        private static long RequireFsmGameObjectTransform(
            DonorUnitySceneModel scene,
            long componentId,
            string variableName)
        {
            DonorMonoBehaviourRecord behaviour = scene.MonoBehaviours.Single(value =>
                value.ComponentId == componentId);
            long gameObjectId = ExtractFsmGameObjectVariable(
                behaviour.SerializedBody,
                variableName);
            if (gameObjectId == 0L ||
                !scene.TryGetTransformIdForGameObject(gameObjectId, out long transformId))
            {
                throw new InvalidDataException(
                    "Missing donor FSM GameObject variable " + variableName +
                    " on component " + componentId + ".");
            }

            return transformId;
        }

        private static Vector3 GetLocalPosition(
            DonorUnitySceneModel scene,
            long transformId,
            long referenceTransformId)
        {
            scene.GetTransformRelativeTo(
                transformId,
                referenceTransformId,
                out Vector3 position,
                out _,
                out _);
            return position;
        }

        private static void SolveThreePointRigidTransform(
            IReadOnlyList<Vector3> source,
            IReadOnlyList<Vector3> target,
            out Vector3 translation,
            out Quaternion rotation,
            out float maximumResidualMeters)
        {
            if (source == null || target == null ||
                source.Count != 3 || target.Count != 3)
            {
                throw new ArgumentException(
                    "Rigid transform triangulation requires exactly three source and target points.");
            }

            Vector3 sourceX = (source[0] - source[1]).normalized;
            Vector3 sourceMidpoint = (source[0] + source[1]) * 0.5f;
            Vector3 sourceYRaw = source[2] - sourceMidpoint;
            Vector3 sourceY = (sourceYRaw -
                Vector3.Dot(sourceYRaw, sourceX) * sourceX).normalized;
            Vector3 targetX = (target[0] - target[1]).normalized;
            Vector3 targetMidpoint = (target[0] + target[1]) * 0.5f;
            Vector3 targetYRaw = target[2] - targetMidpoint;
            Vector3 targetY = (targetYRaw -
                Vector3.Dot(targetYRaw, targetX) * targetX).normalized;
            if (sourceX.sqrMagnitude < 0.99f || sourceY.sqrMagnitude < 0.99f ||
                targetX.sqrMagnitude < 0.99f || targetY.sqrMagnitude < 0.99f)
            {
                throw new InvalidDataException(
                    "Donor Satsuma motor mount points are degenerate.");
            }

            Quaternion sourceBasis = Quaternion.LookRotation(
                Vector3.Cross(sourceX, sourceY).normalized,
                sourceY);
            Quaternion targetBasis = Quaternion.LookRotation(
                Vector3.Cross(targetX, targetY).normalized,
                targetY);
            rotation = targetBasis * Quaternion.Inverse(sourceBasis);
            Vector3 sourceCentroid = (source[0] + source[1] + source[2]) / 3f;
            Vector3 targetCentroid = (target[0] + target[1] + target[2]) / 3f;
            translation = targetCentroid - rotation * sourceCentroid;
            maximumResidualMeters = 0f;
            for (int index = 0; index < 3; index++)
            {
                maximumResidualMeters = Mathf.Max(
                    maximumResidualMeters,
                    Vector3.Distance(
                        translation + rotation * source[index],
                        target[index]));
            }
        }

        private static void WriteEngineMountTriangulationAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorAssemblyFsmEvidence> engineMountFsms,
            IReadOnlyList<long> blockTriggerTransformIds,
            IReadOnlyList<Vector3> blockPoints,
            IReadOnlyList<Vector3> chassisPoints,
            Vector3 solvedPosition,
            Quaternion solvedRotation,
            float maximumResidualMeters)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "AssemblyComponentId,ChassisTriggerTransformId,ChassisTriggerPath,BlockTriggerTransformId,BlockTriggerPath,BlockLocalPoint,ChassisLocalPoint,SolvedBlockPosition,SolvedBlockRotation,MaximumResidualMeters,TransferStatus");
            for (int index = 0; index < engineMountFsms.Count; index++)
            {
                builder.Append(engineMountFsms[index].ComponentId).Append(',')
                    .Append(engineMountFsms[index].TriggerTransformId).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(
                        engineMountFsms[index].TriggerTransformId))).Append(',')
                    .Append(blockTriggerTransformIds[index]).Append(',')
                    .Append(Csv(scene.GetHierarchyPath(
                        blockTriggerTransformIds[index]))).Append(',')
                    .Append(Csv(FormatVector(blockPoints[index]))).Append(',')
                    .Append(Csv(FormatVector(chassisPoints[index]))).Append(',')
                    .Append(Csv(FormatVector(solvedPosition))).Append(',')
                    .Append(Csv(FormatQuaternion(solvedRotation))).Append(',')
                    .Append(maximumResidualMeters.ToString(
                        "R",
                        CultureInfo.InvariantCulture)).Append(',')
                    .AppendLine("ConfigurationTransferred");
            }

            File.WriteAllText(
                ToFileSystemPath(EngineMountTriangulationAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static void AddAssemblyTargetMounts(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence,
            ICollection<MountBuild> results)
        {
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence
                         .Where(value => value.ParentGameObjectId == 0L &&
                             !value.TriggerName.StartsWith(
                                 "Trigger_motor",
                                 StringComparison.Ordinal))
                         .OrderBy(value => value.TriggerName,
                             StringComparer.Ordinal)
                         .ThenBy(value => value.ComponentId))
            {
                LoosePartBuild[] matches = ResolveAssemblyPartCandidates(
                    assembly,
                    loosePartBuilds);
                if (matches.Length == 0)
                {
                    continue;
                }

                long poseTransformId = assembly.TriggerTransformId;
                if (assembly.ActivateThisGameObjectId != 0L &&
                    scene.TryGetTransformIdForGameObject(
                        assembly.ActivateThisGameObjectId,
                        out long activatedTransformId))
                {
                    poseTransformId = activatedTransformId;
                }

                if (string.Equals(
                        assembly.TriggerName,
                        "trigger_grille",
                        StringComparison.Ordinal))
                {
                    // trigger_grille has no regular Parent variable, but its
                    // donor Assemble state explicitly SetParent-resets the part
                    // to Pivot1 (GO 32032 / transform 68091). Using the trigger
                    // itself loses the roughly 90-degree compound pivot
                    // rotation and mounts the grille sideways.
                    const long donorGrillePivotTransformId = 68091L;
                    DonorTransformRecord grillePivot = scene.GetTransform(
                        donorGrillePivotTransformId);
                    if (!string.Equals(
                            scene.GetGameObjectName(grillePivot.GameObjectId),
                            "pivot_grille",
                            StringComparison.Ordinal) ||
                        !IsDescendantOf(
                            scene,
                            donorGrillePivotTransformId,
                            satsumaRoot.TransformId))
                    {
                        throw new InvalidDataException(
                            "Locked donor grille Pivot1 drifted.");
                    }

                    poseTransformId = donorGrillePivotTransformId;
                }

                if (results.Any(value =>
                        value.InstalledTransformId == poseTransformId))
                {
                    continue;
                }

                string mountSlug = NormalizeDonorPartIdentity(
                    assembly.TriggerName);
                if (mountSlug.StartsWith("trigger-", StringComparison.Ordinal))
                {
                    mountSlug = mountSlug.Substring("trigger-".Length);
                }
                else if (mountSlug.StartsWith("trigger", StringComparison.Ordinal))
                {
                    mountSlug = mountSlug.Substring("trigger".Length)
                        .TrimStart('-');
                }

                string mountId = "mount.satsuma." + mountSlug;
                if (string.IsNullOrEmpty(mountSlug) || results.Any(value =>
                        string.Equals(
                            value.Definition.DefinitionId,
                            mountId,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                // The donor exposes both an assembly FSM named "subframe" and
                // the installed sub-frame mesh. The latter is the authoritative
                // mounting pose; treating the FSM target as a second socket makes
                // one physical part randomly choose between two nearby poses.
                // Keep this evidence-backed exception narrow because genuinely
                // interchangeable suspension parts intentionally accept L/R mounts.
                if (string.Equals(
                        mountId,
                        "mount.satsuma.subframe",
                        StringComparison.Ordinal) &&
                    matches.Any(value => string.Equals(
                        value.Definition.DefinitionId,
                        "vehicle.satsuma.part.sub-frame",
                        StringComparison.Ordinal)) &&
                    results.Any(value => string.Equals(
                        value.Definition.DefinitionId,
                        "mount.satsuma.sub-frame",
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                scene.GetTransformRelativeTo(
                    poseTransformId,
                    satsumaRoot.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                // Assembly may expose several activated presentation roots
                // (for example stock rear shock uses ActivateThis for the
                // upper 12 mm fastener and ActivateThis2 for both lower 6 mm
                // fasteners).  The mount pose still comes from the primary
                // activated root, but every fastener owned by the same donor
                // Assembly FSM must be expressed in that mount frame.
                DonorFastenerEvidence[] markers = fastenerEvidence
                    .Where(value =>
                        value.Assembly.ComponentId == assembly.ComponentId &&
                        value.HasSupportedWrenchSize)
                    .Select(value => value.InstalledTransformId ==
                                     poseTransformId
                        ? value
                        : ReprojectFastenerEvidence(
                            scene,
                            assembly,
                            poseTransformId,
                            scene.GetTransform(value.MarkerTransformId)))
                    .GroupBy(value => value.MarkerTransformId)
                    .Select(value => value.First())
                    .OrderBy(value => value.MarkerTransformId)
                    .ToArray();
                if (string.Equals(
                        assembly.TriggerName,
                        "trigger_steering_column",
                        StringComparison.Ordinal))
                {
                    // ActivateThis2 contains the tachometer branch. Its 5 mm
                    // BoltPM (59336) is the tachometer's own fastener, not a
                    // third steering-column bolt. The column itself has exactly
                    // the two reviewed 8 mm markers below its Bolts object.
                    long[] steeringColumnMarkerIds = { 57828L, 60175L };
                    markers = markers
                        .Where(value => steeringColumnMarkerIds.Contains(
                            value.MarkerTransformId))
                        .OrderBy(value => value.MarkerTransformId)
                        .ToArray();
                    if (markers.Length != 2 || markers.Any(value =>
                            value.InferredWrenchMillimeters != 8))
                    {
                        throw new InvalidDataException(
                            "Locked donor steering-column fasteners drifted.");
                    }
                }
                if (string.Equals(
                        assembly.TriggerName,
                        "trigger_grille",
                        StringComparison.Ordinal))
                {
                    // Unlike every other stock body panel, trigger_grille has
                    // no Parent reference and therefore reaches this fallback
                    // mount path. Its fasteners are still ordinary loose-part
                    // Screw children and must be projected from the grille root.
                    LoosePartBuild grille = matches.Single(value =>
                        value.Definition != null && string.Equals(
                            value.Definition.DefinitionId,
                            "vehicle.satsuma.part.grille",
                            StringComparison.Ordinal));
                    markers = new[] { 47050L, 72072L }
                        .Select(markerTransformId =>
                            CreateLoosePartFastenerEvidence(
                                scene,
                                assembly,
                                grille.Source.Root.TransformId,
                                poseTransformId,
                                scene.GetTransform(markerTransformId)))
                        .OrderBy(value => value.MarkerTransformId)
                        .ToArray();
                }
                FastenerBuild[] fasteners = BuildFastenerAssets(
                    mountSlug,
                    matches[0].Definition.DisplayName,
                    markers);
                if (matches.Any(value => value.Definition != null &&
                        IsStockBodyFastenedPanelDefinitionId(
                            value.Definition.DefinitionId)))
                {
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                else if (string.Equals(
                             assembly.TriggerName,
                             "trigger_steering_column",
                             StringComparison.Ordinal))
                {
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                string socketType = "satsuma.socket." + mountSlug;
                string[] acceptedPartIds = matches
                    .Select(value => value.Definition.DefinitionId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var definition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                definition.Configure(
                    mountId,
                    "Satsuma: " + matches[0].Definition.DisplayName,
                    socketType,
                    "vehicle.satsuma.part.body-shell",
                    acceptedPartIds,
                    new MountConstraint(0.35f, 40f, 1.25f, 0f),
                    GetLargestTriggerSphereRadius(
                        scene,
                        new[] { assembly.TriggerGameObjectId }),
                    fasteners.Select(value => value.Definition).ToArray());
                AssetDatabase.CreateAsset(
                    definition,
                    MountDefinitionRoot + "/" +
                    SanitizeName(mountId) + ".asset");
                results.Add(new MountBuild(
                    matches[0].Source,
                    definition,
                    localPosition,
                    localRotation,
                    localScale,
                    poseTransformId,
                    fasteners));

                foreach (LoosePartBuild match in matches)
                {
                    PartDefinition part = match.Definition;
                    part.Configure(
                        part.DefinitionId,
                        part.DisplayName,
                        part.Category,
                        part.MassKilograms,
                        part.VisualPrefab,
                        part.CompatibilityRules
                            .Concat(new[]
                            {
                                PartCompatibilityRule.Create(
                                    socketType,
                                    "vehicle.satsuma.part.body-shell"),
                            })
                            .ToArray());
                    EditorUtility.SetDirty(part);
                }
            }
        }

        private static LoosePartBuild[] ResolveAssemblyPartCandidates(
            DonorAssemblyFsmEvidence assembly,
            IReadOnlyList<LoosePartBuild> loosePartBuilds)
        {
            if (assembly == null)
            {
                return Array.Empty<LoosePartBuild>();
            }

            Match wheelCorner = Regex.Match(
                assembly.TriggerName,
                @"^TriggerWheel(?<corner>FL|FR|RL|RR)_New$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (wheelCorner.Success)
            {
                string suffix = "-" +
                    wheelCorner.Groups["corner"].Value.ToLowerInvariant();
                return loosePartBuilds
                    .Where(value =>
                        value.Definition.DefinitionId.Contains(
                            ".wheel-",
                            StringComparison.Ordinal) &&
                        value.Definition.DefinitionId.EndsWith(
                            suffix,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .ToArray();
            }

            string[] evidenceNames =
            {
                assembly.HandChild,
                assembly.ThisPartName,
                Regex.Replace(
                    assembly.TriggerName ?? string.Empty,
                    @"^trigger_?",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            };
            for (int evidenceIndex = 0;
                 evidenceIndex < evidenceNames.Length;
                 evidenceIndex++)
            {
                string identity = CompactAssemblyIdentity(
                    evidenceNames[evidenceIndex]);
                if (string.IsNullOrEmpty(identity))
                {
                    continue;
                }

                LoosePartBuild[] matches = loosePartBuilds
                    .Where(value => AssemblyIdentityMatches(
                        value,
                        identity,
                        allowVariantAffixes: evidenceIndex == 2))
                    .OrderBy(value => value.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .ToArray();
                if (matches.Length > 0)
                {
                    return matches;
                }
            }

            return Array.Empty<LoosePartBuild>();
        }

        private static bool AssemblyIdentityMatches(
            LoosePartBuild part,
            string evidenceIdentity,
            bool allowVariantAffixes)
        {
            string donorIdentity = CompactAssemblyIdentity(
                part.Source.DonorName);
            string definitionIdentity = CompactAssemblyIdentity(
                part.Definition.DefinitionId.Substring(
                    "vehicle.satsuma.part.".Length));
            if (string.Equals(
                    donorIdentity,
                    evidenceIdentity,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionIdentity,
                    evidenceIdentity,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (donorIdentity.EndsWith("0", StringComparison.Ordinal) &&
                string.Equals(
                    donorIdentity.Substring(0, donorIdentity.Length - 1),
                    evidenceIdentity,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (!allowVariantAffixes || evidenceIdentity.Length < 5)
            {
                return false;
            }

            return donorIdentity.StartsWith(
                       evidenceIdentity,
                       StringComparison.Ordinal) ||
                   donorIdentity.EndsWith(
                       evidenceIdentity,
                       StringComparison.Ordinal) ||
                   definitionIdentity.StartsWith(
                       evidenceIdentity,
                       StringComparison.Ordinal) ||
                   definitionIdentity.EndsWith(
                       evidenceIdentity,
                       StringComparison.Ordinal);
        }

        private static string CompactAssemblyIdentity(string value)
        {
            return NormalizeDonorPartIdentity(value)
                .Replace("-", string.Empty);
        }

        private static void AddLoosePartOwnedMounts(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence,
            ICollection<MountBuild> results)
        {
            var ownerByTransformId = loosePartBuilds.ToDictionary(
                value => value.Source.Root.TransformId);
            var existingPartIds = new HashSet<string>(
                results.SelectMany(value =>
                    value.Definition.AcceptedPartDefinitionIds),
                StringComparer.Ordinal);
            var candidates = new List<OwnedPartMountCandidate>();
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence
                         .Where(value =>
                             value.ParentGameObjectId != 0L ||
                             value.ActivateThisGameObjectId != 0L))
            {
                LoosePartBuild[] parts = ResolveAssemblyPartCandidates(
                        assembly,
                        loosePartBuilds)
                    .Where(value => !existingPartIds.Contains(
                        value.Definition.DefinitionId))
                    .ToArray();
                long poseGameObjectId = assembly.ParentGameObjectId != 0L
                    ? assembly.ParentGameObjectId
                    : assembly.ActivateThisGameObjectId;
                if (parts.Length == 0 ||
                    !scene.TryGetTransformIdForGameObject(
                        poseGameObjectId,
                        out long parentTransformId))
                {
                    continue;
                }

                LoosePartBuild[] owners = ownerByTransformId.Values
                    .Where(value => IsDescendantOf(
                        scene,
                        assembly.TriggerTransformId,
                        value.Source.Root.TransformId))
                    .OrderByDescending(value => scene.GetTransformDepth(
                        value.Source.Root.TransformId))
                    .ToArray();
                if (owners.Length == 0)
                {
                    continue;
                }

                foreach (LoosePartBuild part in parts)
                {
                    candidates.Add(new OwnedPartMountCandidate(
                        assembly,
                        owners[0],
                        part,
                        parentTransformId));
                }
            }

            foreach (IGrouping<long, OwnedPartMountCandidate> group in
                     candidates.GroupBy(value => value.ParentTransformId)
                         .OrderBy(value => value.Key))
            {
                OwnedPartMountCandidate[] alternatives = group
                    .OrderBy(value => value.Part.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .ToArray();
                LoosePartBuild owner = alternatives[0].Owner;
                if (alternatives.Any(value => !string.Equals(
                        value.Owner.Definition.DefinitionId,
                        owner.Definition.DefinitionId,
                        StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        "One loose-part mount pivot resolved to multiple owners.");
                }

                string[] acceptedPartIds = alternatives
                    .Select(value => value.Part.Definition.DefinitionId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string mountSlug = NormalizeDonorPartIdentity(
                    scene.GetGameObjectName(
                        scene.GetTransform(group.Key).GameObjectId));
                if (mountSlug.StartsWith("pivot-", StringComparison.Ordinal))
                {
                    mountSlug = mountSlug.Substring("pivot-".Length);
                }

                string ownerSlug = owner.Definition.DefinitionId.Substring(
                    "vehicle.satsuma.part.".Length);
                string mountId = "mount.satsuma." + ownerSlug + "." +
                                 mountSlug;
                if (results.Any(value => string.Equals(
                        value.Definition.DefinitionId,
                        mountId,
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                scene.GetTransformRelativeTo(
                    group.Key,
                    owner.Source.Root.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                DonorFastenerEvidence[] markers = alternatives
                    .SelectMany(alternative => scene.GetDescendants(
                            alternative.Part.Source.Root.TransformId,
                            includeRoot: true)
                        .Where(value => string.Equals(
                            scene.GetGameObjectName(value.GameObjectId),
                            "BoltPM",
                            StringComparison.Ordinal))
                        .Select(marker => CreateLoosePartFastenerEvidence(
                            scene,
                            alternative.Assembly,
                            alternative.Part.Source.Root.TransformId,
                            group.Key,
                            marker)))
                    .Concat(alternatives
                        .Where(alternative =>
                            alternative.Assembly.ParentGameObjectId == 0L &&
                            alternative.Assembly.ActivateThisGameObjectId != 0L)
                        .SelectMany(alternative => scene.GetDescendants(
                                alternative.Owner.Source.Root.TransformId,
                                includeRoot: true)
                            .Where(value => string.Equals(
                                scene.GetGameObjectName(value.GameObjectId),
                                "BoltPM",
                                StringComparison.Ordinal))
                            .OrderBy(value => Vector3.SqrMagnitude(
                                scene.GetWorldPosition(value.TransformId) -
                                scene.GetWorldPosition(group.Key)))
                            .ThenBy(value => value.TransformId)
                            .Take(1)
                            .Select(marker =>
                                CreateLoosePartFastenerEvidence(
                                    scene,
                                    alternative.Assembly,
                                    group.Key,
                                    group.Key,
                                    marker))))
                    .Concat(fastenerEvidence.Where(value =>
                        value.InstalledTransformId == group.Key))
                    .Where(value => value.HasSupportedWrenchSize)
                    .GroupBy(value => value.MarkerTransformId)
                    .Select(value => value.First())
                    .OrderBy(value => value.MarkerTransformId)
                    .ToArray();
                FastenerBuild[] fasteners = BuildFastenerAssets(
                    ownerSlug + "-" + mountSlug,
                    alternatives[0].Part.Definition.DisplayName,
                    markers);
                string socketType = "satsuma.socket." + ownerSlug + "." +
                                    mountSlug;
                var definition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                definition.Configure(
                    mountId,
                    "Satsuma: " + alternatives[0].Part.Definition.DisplayName,
                    socketType,
                    owner.Definition.DefinitionId,
                    acceptedPartIds,
                    new MountConstraint(0.22f, 50f, 0.75f, 0f),
                    GetLargestTriggerSphereRadius(
                        scene,
                        alternatives.Select(value =>
                            value.Assembly.TriggerGameObjectId)),
                    fasteners.Select(value => value.Definition).ToArray());
                AssetDatabase.CreateAsset(
                    definition,
                    MountDefinitionRoot + "/" +
                    SanitizeName(mountId) + ".asset");
                results.Add(new MountBuild(
                    alternatives[0].Part.Source,
                    definition,
                    localPosition,
                    localRotation,
                    localScale,
                    group.Key,
                    fasteners,
                    ownerPartDefinitionId: owner.Definition.DefinitionId));

                foreach (OwnedPartMountCandidate alternative in alternatives)
                {
                    PartDefinition part = alternative.Part.Definition;
                    part.Configure(
                        part.DefinitionId,
                        part.DisplayName,
                        part.Category,
                        part.MassKilograms,
                        part.VisualPrefab,
                        part.CompatibilityRules
                            .Concat(new[]
                            {
                                PartCompatibilityRule.Create(
                                    socketType,
                                    owner.Definition.DefinitionId),
                            })
                            .ToArray());
                    EditorUtility.SetDirty(part);
                    existingPartIds.Add(part.DefinitionId);
                }
            }
        }

        private static void AddHingedAssemblyParentMounts(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            ICollection<MountBuild> results)
        {
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence
                         .Where(value =>
                             value.ParentGameObjectId != 0L &&
                             IsDeferredHingeAssembly(value.TriggerName))
                         .OrderBy(value => value.TriggerName,
                             StringComparer.Ordinal))
            {
                LoosePartBuild[] matches = ResolveAssemblyPartCandidates(
                        assembly,
                        loosePartBuilds)
                    .ToArray();
                if (matches.Length != 1 ||
                    !scene.TryGetTransformIdForGameObject(
                        assembly.ParentGameObjectId,
                        out long parentTransformId))
                {
                    throw new InvalidDataException(
                        "Could not resolve the unique donor hinged assembly " +
                        assembly.TriggerName + ".");
                }

                LoosePartBuild part = matches[0];
                string slug = part.Definition.DefinitionId.Substring(
                    "vehicle.satsuma.part.".Length);
                string mountId = "mount.satsuma." + slug;
                if (results.Any(value => string.Equals(
                        value.Definition.DefinitionId,
                        mountId,
                        StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        "Duplicate hinged Satsuma mount: " + mountId + ".");
                }

                scene.GetTransformRelativeTo(
                    parentTransformId,
                    satsumaRoot.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                DonorFastenerEvidence[] donorFasteners = scene
                    .GetDescendants(
                        part.Source.Root.TransformId,
                        includeRoot: true)
                    .Where(value => string.Equals(
                        scene.GetGameObjectName(value.GameObjectId),
                        "BoltPM",
                        StringComparison.Ordinal))
                    .Select(marker => CreateLoosePartFastenerEvidence(
                        scene,
                        assembly,
                        part.Source.Root.TransformId,
                        parentTransformId,
                        marker))
                    .Where(value => value.HasSupportedWrenchSize)
                    .OrderBy(value => value.MarkerTransformId)
                    .ToArray();
                FastenerBuild[] fasteners = BuildFastenerAssets(
                    slug,
                    part.Definition.DisplayName,
                    donorFasteners);
                // All 16 donor fasteners on the two doors, bootlid and hood use
                // Mesh/bolt.asset. The generic fallback is Mesh/bolt2.asset,
                // which is a nut and was visibly wrong on these hinges.
                fasteners = fasteners
                    .Select(value => value.WithMeshSourceGuid(
                        DonorShortBoltMeshSourceGuid))
                    .ToArray();
                HingeMountBuild hinge = GetHingeMountBuild(
                    assembly.TriggerName);
                var definition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                definition.Configure(
                    mountId,
                    "Satsuma: " + part.Definition.DisplayName,
                    GetMountSocketType(part.Definition.DefinitionId),
                    "vehicle.satsuma.part.body-shell",
                    new[] { part.Definition.DefinitionId },
                    new MountConstraint(0.35f, 40f, 1.25f, 0f),
                    GetLargestTriggerSphereRadius(
                        scene,
                        new[] { assembly.TriggerGameObjectId }),
                    fasteners.Select(value => value.Definition).ToArray());
                AssetDatabase.CreateAsset(
                    definition,
                    MountDefinitionRoot + "/" +
                    SanitizeName(mountId) + ".asset");
                results.Add(new MountBuild(
                    part.Source,
                    definition,
                    localPosition,
                    localRotation,
                    localScale,
                    parentTransformId,
                    fasteners,
                    hinge));
            }
        }

        private static DonorFastenerEvidence
            CreateLoosePartFastenerEvidence(
                DonorUnitySceneModel scene,
                DonorAssemblyFsmEvidence assembly,
                long loosePartTransformId,
                long installedTransformId,
                DonorTransformRecord marker)
        {
            DonorColliderRecord sphere = scene
                .GetCollidersForGameObject(
                    marker.GameObjectId,
                    includeDisabled: true,
                    includeTriggers: true)
                .FirstOrDefault(value =>
                    value.Kind == DonorColliderKind.Sphere);
            scene.GetTransformRelativeTo(
                marker.TransformId,
                loosePartTransformId,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out Vector3 localScale);
            int inferredWrenchMillimeters = Mathf.RoundToInt(
                Mathf.Abs(marker.LocalScale.x) * 10f);
            bool supportedWrench = inferredWrenchMillimeters != 0 &&
                Enum.IsDefined(
                    typeof(FastenerSize),
                    inferredWrenchMillimeters);
            return new DonorFastenerEvidence(
                assembly,
                installedTransformId,
                marker.TransformId,
                marker.GameObjectId,
                localPosition,
                localRotation,
                localScale,
                sphere?.Radius ?? 0f,
                inferredWrenchMillimeters,
                supportedWrench,
                ExtractDonorFastenerMaximumStage(
                    scene,
                    marker.GameObjectId));
        }

        private static HingeMountBuild GetHingeMountBuild(
            string triggerName)
        {
            return triggerName switch
            {
                "trigger_bootlid" => new HingeMountBuild(
                    Vector3.right,
                    -70f,
                    0f,
                    500f,
                    500f,
                    105480L,
                    new Vector3(-30f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    donorOpenHoldWindowDegrees: 1f),
                "trigger_door_left" => new HingeMountBuild(
                    Vector3.forward,
                    0f,
                    80f,
                    1100f,
                    1100f,
                    110762L,
                    new Vector3(0f, 0f, 120f),
                    new Vector3(0f, 0f, -150f)),
                "trigger_door_right" => new HingeMountBuild(
                    Vector3.forward,
                    -80f,
                    0f,
                    1100f,
                    1100f,
                    112706L,
                    new Vector3(0f, 0f, -120f),
                    new Vector3(0f, 0f, 150f)),
                "trigger_hood" => new HingeMountBuild(
                    Vector3.right,
                    -87f,
                    0f,
                    1000f,
                    1000f,
                    111924L,
                    new Vector3(-30f, 0f, 0f),
                    new Vector3(15f, 0f, 0f),
                    requiresReleaseBeforeOpening: true),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(triggerName),
                    triggerName,
                    "Unknown donor hinge assembly."),
            };
        }

        private static void AddFixedAssemblyParentMounts(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence,
            ICollection<MountBuild> results)
        {
            var existingPartIds = new HashSet<string>(
                results.SelectMany(value =>
                    value.Definition.AcceptedPartDefinitionIds),
                StringComparer.Ordinal);
            var candidates = new List<ParentMountCandidate>();
            foreach (DonorAssemblyFsmEvidence assembly in assemblyEvidence
                         .Where(value => value.ParentGameObjectId != 0L &&
                             !IsDeferredHingeAssembly(value.TriggerName)))
            {
                LoosePartBuild[] matches = ResolveAssemblyPartCandidates(
                        assembly,
                        loosePartBuilds)
                    .Where(value => !existingPartIds.Contains(
                        value.Definition.DefinitionId))
                    .ToArray();
                if (matches.Length == 0 ||
                    !scene.TryGetTransformIdForGameObject(
                        assembly.ParentGameObjectId,
                        out long parentTransformId))
                {
                    continue;
                }

                foreach (LoosePartBuild match in matches)
                {
                    candidates.Add(new ParentMountCandidate(
                        assembly,
                        parentTransformId,
                        match));
                }
            }

            foreach (IGrouping<long, ParentMountCandidate> group in candidates
                         .GroupBy(value => value.ParentTransformId)
                         .OrderBy(value => value.Key))
            {
                ParentMountCandidate[] alternatives = group
                    .OrderBy(value => value.Part.Definition.DefinitionId,
                        StringComparer.Ordinal)
                    .ToArray();
                string[] acceptedPartIds = alternatives
                    .Select(value => value.Part.Definition.DefinitionId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (acceptedPartIds.Length == 0)
                {
                    continue;
                }

                string mountSlug = NormalizeDonorPartIdentity(
                    scene.GetGameObjectName(
                        scene.GetTransform(group.Key).GameObjectId));
                if (mountSlug.StartsWith("pivot-", StringComparison.Ordinal))
                {
                    mountSlug = mountSlug.Substring("pivot-".Length);
                }
                string mountId = "mount.satsuma." + mountSlug;
                if (results.Any(value => string.Equals(
                        value.Definition.DefinitionId,
                        mountId,
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                scene.GetTransformRelativeTo(
                    group.Key,
                    satsumaRoot.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                IEnumerable<DonorFastenerEvidence> markerCandidates =
                    fastenerEvidence.Where(value =>
                        value.InstalledTransformId == group.Key &&
                        value.HasSupportedWrenchSize);
                if (string.Equals(
                        mountId,
                        "mount.satsuma.steering-wheel",
                        StringComparison.Ordinal))
                {
                    // The installed pivot is empty until either steering wheel
                    // is parented to it, so chassis-side discovery sees no
                    // marker. Stock and GT wheels each carry the same one-nut
                    // layout. Use the reviewed stock marker as the shared mount
                    // fastener and validate the GT counterpart has not drifted.
                    ParentMountCandidate stockWheel = alternatives.Single(
                        value => string.Equals(
                            value.Part.Definition.DefinitionId,
                            "vehicle.satsuma.part.stock-steering-wheel",
                            StringComparison.Ordinal));
                    ParentMountCandidate gtWheel = alternatives.Single(
                        value => string.Equals(
                            value.Part.Definition.DefinitionId,
                            "vehicle.satsuma.part.gt-gt-steering-wheel",
                            StringComparison.Ordinal));
                    DonorTransformRecord stockMarker = scene.GetTransform(
                        62790L);
                    DonorTransformRecord gtMarker = scene.GetTransform(52786L);
                    if (!IsDescendantOf(
                            scene,
                            stockMarker.TransformId,
                            stockWheel.Part.Source.Root.TransformId) ||
                        !IsDescendantOf(
                            scene,
                            gtMarker.TransformId,
                            gtWheel.Part.Source.Root.TransformId) ||
                        Mathf.RoundToInt(
                            Mathf.Abs(stockMarker.LocalScale.x) * 10f) != 10 ||
                        Mathf.RoundToInt(
                            Mathf.Abs(gtMarker.LocalScale.x) * 10f) != 10 ||
                        ExtractDonorFastenerMaximumStage(
                            scene,
                            stockMarker.GameObjectId) != 8 ||
                        ExtractDonorFastenerMaximumStage(
                            scene,
                            gtMarker.GameObjectId) != 8)
                    {
                        throw new InvalidDataException(
                            "Locked donor steering-wheel nut drifted.");
                    }

                    markerCandidates = new[]
                    {
                        CreateLoosePartFastenerEvidence(
                            scene,
                            stockWheel.Assembly,
                            stockWheel.Part.Source.Root.TransformId,
                            group.Key,
                            stockMarker),
                    };
                }
                if (acceptedPartIds.Any(
                        IsStockBodyFastenedPanelDefinitionId))
                {
                    // Fixed stock body panels keep their Screw/BoltPM data
                    // below the loose part. Their Assembly FSM supplies a
                    // separate installed parent, so the chassis-side fastener
                    // audit alone used to leave these mounts completely bare.
                    markerCandidates = markerCandidates.Concat(
                        alternatives.SelectMany(alternative => scene
                            .GetDescendants(
                                alternative.Part.Source.Root.TransformId,
                                includeRoot: true)
                            .Where(value => string.Equals(
                                scene.GetGameObjectName(value.GameObjectId),
                                "BoltPM",
                                StringComparison.Ordinal))
                            .Select(marker =>
                                CreateLoosePartFastenerEvidence(
                                    scene,
                                    alternative.Assembly,
                                    alternative.Part.Source.Root.TransformId,
                                    group.Key,
                                    marker))));
                }

                DonorFastenerEvidence[] markers = markerCandidates
                    .Where(value => value.HasSupportedWrenchSize)
                    .GroupBy(value => value.MarkerTransformId)
                    .Select(value => value.First())
                    .OrderBy(value => value.MarkerTransformId)
                    .ToArray();
                if (string.Equals(
                        mountId,
                        "mount.satsuma.steering-column",
                        StringComparison.Ordinal))
                {
                    // This parent-mount path can also see the dashboard
                    // tachometer's 5 mm BoltPM (59336). It belongs to the
                    // tachometer branch, not to the steering column. The donor
                    // column has exactly these two 8 mm short bolts.
                    long[] steeringColumnMarkerIds = { 57828L, 60175L };
                    markers = markers
                        .Where(value => steeringColumnMarkerIds.Contains(
                            value.MarkerTransformId))
                        .OrderBy(value => value.MarkerTransformId)
                        .ToArray();
                    if (markers.Length != 2 || markers.Any(value =>
                            value.InferredWrenchMillimeters != 8))
                    {
                        throw new InvalidDataException(
                            "Locked donor steering-column fasteners drifted.");
                    }
                }
                FastenerBuild[] fasteners = BuildFastenerAssets(
                    mountSlug,
                    alternatives[0].Part.Definition.DisplayName,
                    markers);
                if (acceptedPartIds.Any(
                        IsStockBodyFastenedPanelDefinitionId))
                {
                    // Every one of the 20 fixed-panel donor markers references
                    // Mesh/bolt.asset, never the default nut mesh.
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                else if (string.Equals(
                             mountId,
                             "mount.satsuma.steering-column",
                             StringComparison.Ordinal))
                {
                    fasteners = fasteners
                        .Select(value => value.WithMeshSourceGuid(
                            DonorShortBoltMeshSourceGuid))
                        .ToArray();
                }
                string socketType = "satsuma.socket." + mountSlug;
                var definition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                definition.Configure(
                    mountId,
                    "Satsuma: " + alternatives[0].Part.Definition.DisplayName,
                    socketType,
                    "vehicle.satsuma.part.body-shell",
                    acceptedPartIds,
                    new MountConstraint(0.35f, 40f, 1.25f, 0f),
                    GetLargestTriggerSphereRadius(
                        scene,
                        alternatives.Select(value =>
                            value.Assembly.TriggerGameObjectId)),
                    fasteners.Select(value => value.Definition).ToArray());
                AssetDatabase.CreateAsset(
                    definition,
                    MountDefinitionRoot + "/" +
                    SanitizeName(mountId) + ".asset");
                results.Add(new MountBuild(
                    alternatives[0].Part.Source,
                    definition,
                    localPosition,
                    localRotation,
                    localScale,
                    group.Key,
                    fasteners));

                foreach (ParentMountCandidate alternative in alternatives)
                {
                    PartDefinition part = alternative.Part.Definition;
                    part.Configure(
                        part.DefinitionId,
                        part.DisplayName,
                        part.Category,
                        part.MassKilograms,
                        part.VisualPrefab,
                        part.CompatibilityRules
                            .Concat(new[]
                            {
                                PartCompatibilityRule.Create(
                                    socketType,
                                    "vehicle.satsuma.part.body-shell"),
                            })
                            .ToArray());
                    EditorUtility.SetDirty(part);
                    existingPartIds.Add(part.DefinitionId);
                }
            }
        }

        private static bool IsDeferredHingeAssembly(string triggerName) =>
            string.Equals(triggerName, "trigger_bootlid", StringComparison.Ordinal) ||
            string.Equals(triggerName, "trigger_door_left", StringComparison.Ordinal) ||
            string.Equals(triggerName, "trigger_door_right", StringComparison.Ordinal) ||
            string.Equals(triggerName, "trigger_hood", StringComparison.Ordinal);

        private static bool IsHingedPartDefinitionId(string definitionId) =>
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.bootlid",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.door-left",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.door-right",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.hood",
                StringComparison.Ordinal);

        private static float GetLargestTriggerSphereRadius(
            DonorUnitySceneModel scene,
            IEnumerable<long> triggerGameObjectIds)
        {
            return triggerGameObjectIds
                .SelectMany(value => scene.GetCollidersForGameObject(
                    value,
                    includeDisabled: true,
                    includeTriggers: true))
                .Where(value => value.Kind == DonorColliderKind.Sphere)
                .Select(value => value.Radius)
                .DefaultIfEmpty(0f)
                .Max();
        }

        private static FastenerBuild[] BuildFastenerAssets(
            string partSlug,
            string partDisplayName,
            IReadOnlyList<DonorFastenerEvidence> evidence)
        {
            var builds = new FastenerBuild[evidence.Count];
            for (int index = 0; index < evidence.Count; index++)
            {
                DonorFastenerEvidence marker = evidence[index];
                FastenerSize size = (FastenerSize)
                    marker.InferredWrenchMillimeters;
                string fastenerId = "fastener.satsuma." + partSlug +
                                    ".boltpm-" + (index + 1);
                var definition = ScriptableObject
                    .CreateInstance<FastenerDefinition>();
                definition.Configure(
                    fastenerId,
                    partDisplayName + " bolt " + (index + 1),
                    size,
                    marker.MaximumStage,
                    FastenerDirection.ClockwiseToTighten,
                    insertOnInstall: true,
                    blocksRemoval: true,
                    ToolCompatibilityRule.Create("Wrench", size));
                AssetDatabase.CreateAsset(
                    definition,
                    FastenerDefinitionRoot + "/" +
                    SanitizeName(fastenerId) + ".asset");
                builds[index] = new FastenerBuild(
                    definition,
                    marker.LocalPosition,
                    marker.LocalRotation,
                    marker.LocalScale,
                    marker.ColliderRadiusMeters,
                    marker.MarkerTransformId);
            }

            return builds;
        }

        private static void AddRearDrumMounts(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> assemblyEvidence,
            IReadOnlyList<DonorFastenerEvidence> fastenerEvidence,
            ICollection<MountBuild> results)
        {
            LoosePartBuild[] drums = loosePartBuilds
                .Where(value => string.Equals(
                    NormalizeDonorPartIdentity(value.Source.DonorName),
                    "drum-brake",
                    StringComparison.Ordinal))
                .OrderBy(value => value.Source.Root.TransformId)
                .ToArray();
            if (drums.Length != 2)
            {
                throw new InvalidDataException(
                    $"Expected exactly two interchangeable loose rear drums; found {drums.Length}.");
            }

            string[] acceptedPartIds = drums
                .Select(value => value.Definition.DefinitionId)
                .ToArray();
            var compatibilityRules = new List<PartCompatibilityRule>();
            foreach (string corner in new[] { "rl", "rr" })
            {
                string triggerName = "trigger_drumbrake_" + corner;
                DonorAssemblyFsmEvidence assembly = assemblyEvidence.Single(
                    value => string.Equals(
                        value.TriggerName,
                        triggerName,
                        StringComparison.Ordinal));
                if (!scene.TryGetTransformIdForGameObject(
                        assembly.ActivateThisGameObjectId,
                        out long installedTransformId))
                {
                    throw new InvalidDataException(
                        $"Rear drum {corner} Assembly FSM has no installed transform.");
                }

                DonorFastenerEvidence marker = fastenerEvidence.Single(value =>
                    value.Assembly.ComponentId == assembly.ComponentId &&
                    value.HasSupportedWrenchSize);
                if (marker.InferredWrenchMillimeters != 14)
                {
                    throw new InvalidDataException(
                        $"Rear drum {corner} must preserve donor wrench 14; found {marker.InferredWrenchMillimeters}.");
                }

                DonorColliderRecord triggerSphere = scene
                    .GetCollidersForGameObject(
                        assembly.TriggerGameObjectId,
                        includeDisabled: true,
                        includeTriggers: true)
                    .Single(value => value.Kind == DonorColliderKind.Sphere);
                string mountId = "mount.satsuma.drum-brake-" + corner;
                string ownerPartId = "vehicle.satsuma.part.trail-arm-" + corner;
                MountBuild ownerMount = results.Single(value =>
                    value.Definition.AcceptedPartDefinitionIds.Contains(
                        ownerPartId,
                        StringComparer.Ordinal));
                // The drum socket and installed arm must be measured inside
                // the same donor hierarchy. Referencing the loose PartsCar arm
                // mixes two coordinate spaces and used to place this socket
                // several metres away after the arm was installed.
                scene.GetTransformRelativeTo(
                    installedTransformId,
                    ownerMount.InstalledTransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                string socketType = "satsuma.socket.drum-brake-" + corner;
                string fastenerId = "fastener.satsuma.drum-brake-" +
                                    corner + ".boltpm";
                var fastenerDefinition = ScriptableObject
                    .CreateInstance<FastenerDefinition>();
                fastenerDefinition.Configure(
                    fastenerId,
                    "BoltPM заднего барабана " + corner.ToUpperInvariant(),
                    FastenerSize.Millimeter14,
                    8,
                    FastenerDirection.ClockwiseToTighten,
                    insertOnInstall: true,
                    blocksRemoval: true,
                    ToolCompatibilityRule.Create(
                        "Wrench",
                        FastenerSize.Millimeter14));
                string fastenerPath = FastenerDefinitionRoot + "/" +
                                      SanitizeName(fastenerId) + ".asset";
                AssetDatabase.CreateAsset(
                    fastenerDefinition,
                    fastenerPath);

                var mountDefinition = ScriptableObject
                    .CreateInstance<MountPointDefinition>();
                mountDefinition.Configure(
                    mountId,
                    "Satsuma: rear drum " + corner.ToUpperInvariant(),
                    socketType,
                    ownerPartId,
                    acceptedPartIds,
                    new MountConstraint(0.35f, 40f, 1.25f, 0f),
                    triggerSphere.Radius,
                    new[] { fastenerDefinition });
                string mountPath = MountDefinitionRoot + "/" +
                                   SanitizeName(mountId) + ".asset";
                AssetDatabase.CreateAsset(mountDefinition, mountPath);
                results.Add(new MountBuild(
                    drums[0].Source,
                    mountDefinition,
                    localPosition,
                    localRotation,
                    localScale,
                    installedTransformId,
                    new[]
                    {
                        new FastenerBuild(
                            fastenerDefinition,
                            marker.LocalPosition,
                            marker.LocalRotation,
                            marker.LocalScale,
                            marker.ColliderRadiusMeters,
                            marker.MarkerTransformId),
                    },
                    ownerPartDefinitionId: ownerPartId));
                compatibilityRules.Add(PartCompatibilityRule.Create(
                    socketType,
                    ownerPartId));
            }

            foreach (LoosePartBuild drum in drums)
            {
                PartDefinition definition = drum.Definition;
                definition.Configure(
                    definition.DefinitionId,
                    definition.DisplayName,
                    definition.Category,
                    definition.MassKilograms,
                    definition.VisualPrefab,
                    compatibilityRules.ToArray());
                EditorUtility.SetDirty(definition);
            }
        }

        private static bool IsAcceptedMountCandidate(
            InstalledMountCandidate candidate) =>
            candidate != null &&
            candidate.Candidates.Count == 1 &&
            candidate.TopScore == 300 &&
            IsAcceptedInstalledPartPath(candidate.LoosePart.DonorName);

        private static bool IsAcceptedInstalledPartPath(string donorLooseName)
        {
            string normalized = NormalizeDonorPartIdentity(donorLooseName);
            return !string.Equals(normalized, "dashboard", StringComparison.Ordinal) &&
                   !string.Equals(normalized, "radio", StringComparison.Ordinal) &&
                   !string.Equals(normalized, "electrics", StringComparison.Ordinal) &&
                   !string.Equals(normalized, "starter", StringComparison.Ordinal);
        }

        private static string GetMountSocketType(string partDefinitionId) =>
            "satsuma.socket." + partDefinitionId.Substring(
                "vehicle.satsuma.part.".Length);

        private static void ConfigureSatsumaFrontMountSequence(
            IReadOnlyList<MountBuild> mounts)
        {
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wishbone-fl",
                new[] { "mount.satsuma.sub-frame" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wishbone-fr",
                new[] { "mount.satsuma.sub-frame" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.spindle-fl",
                new[] { "mount.satsuma.wishbone-fl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.spindle-fr",
                new[] { "mount.satsuma.wishbone-fr" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.strut-fl",
                new[] { "mount.satsuma.spindle-fl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.strut-fr",
                new[] { "mount.satsuma.spindle-fr" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.discbrake-fl",
                new[] { "mount.satsuma.spindle-fl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.discbrake-fr",
                new[] { "mount.satsuma.spindle-fr" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.halfshaft-fl",
                new[] { "mount.satsuma.discbrake-fl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.halfshaft-fr",
                new[] { "mount.satsuma.discbrake-fr" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureSatsumaFrontInstallationChecks(mounts);
        }

        private static void ConfigureSatsumaMountSequence(
            IReadOnlyList<MountBuild> mounts)
        {
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wheelfl-new",
                new[] { "mount.satsuma.discbrake-fl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wheelfr-new",
                new[] { "mount.satsuma.discbrake-fr" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.drum-brake-rl",
                new[] { "mount.satsuma.trail-arm-rl" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "mount.satsuma.trail-arm-rl" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.drum-brake-rr",
                new[] { "mount.satsuma.trail-arm-rr" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "mount.satsuma.trail-arm-rr" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.coilspring-rl",
                new[] { "mount.satsuma.trail-arm-rl" },
                Array.Empty<string>(),
                new[] { "mount.satsuma.long-coilspring-rl" },
                new[] { "mount.satsuma.trail-arm-rl" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.long-coilspring-rl",
                new[] { "mount.satsuma.trail-arm-rl" },
                Array.Empty<string>(),
                new[] { "mount.satsuma.coilspring-rl" },
                new[] { "mount.satsuma.trail-arm-rl" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.shock-rl",
                new[] { "mount.satsuma.trail-arm-rl" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "mount.satsuma.trail-arm-rl" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.coilspring-rr",
                new[] { "mount.satsuma.trail-arm-rr" },
                Array.Empty<string>(),
                new[] { "mount.satsuma.long-coilspring-rr" },
                new[] { "mount.satsuma.trail-arm-rr" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.long-coilspring-rr",
                new[] { "mount.satsuma.trail-arm-rr" },
                Array.Empty<string>(),
                new[] { "mount.satsuma.coilspring-rr" },
                new[] { "mount.satsuma.trail-arm-rr" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.shock-rr",
                new[] { "mount.satsuma.trail-arm-rr" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "mount.satsuma.trail-arm-rr" });
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wheelrl-new",
                new[] { "mount.satsuma.drum-brake-rl" },
                Array.Empty<string>(),
                Array.Empty<string>());
            ConfigureMountSequence(
                mounts,
                "mount.satsuma.wheelrr-new",
                new[] { "mount.satsuma.drum-brake-rr" },
                Array.Empty<string>(),
                Array.Empty<string>());

            // Donor Removal FSMs for both stock and rally/long rear springs
            // expose mouse-over/remove only after the same-corner shock (stock
            // or rally) is absent. The Phase 1 baseline currently has one shock
            // socket per corner, so both spring variants share that blocker.
            ConfigureMountRemovalBlockers(
                mounts,
                "mount.satsuma.coilspring-rl",
                new[] { "mount.satsuma.shock-rl" });
            ConfigureMountRemovalBlockers(
                mounts,
                "mount.satsuma.long-coilspring-rl",
                new[] { "mount.satsuma.shock-rl" });
            ConfigureMountRemovalBlockers(
                mounts,
                "mount.satsuma.coilspring-rr",
                new[] { "mount.satsuma.shock-rr" });
            ConfigureMountRemovalBlockers(
                mounts,
                "mount.satsuma.long-coilspring-rr",
                new[] { "mount.satsuma.shock-rr" });
        }

        private static void ConfigureSatsumaFrontInstallationChecks(
            IReadOnlyList<MountBuild> mounts)
        {
            // Frozen donor Assembly Check bolts is evaluated on the install
            // command, after a point is offered for an installed support. It is
            // not a persistent structural-retention condition or a preview gate.
            ConfigureMountSequence(
                mounts, "mount.satsuma.steering-rack",
                new[] { "mount.satsuma.sub-frame" },
                Array.Empty<string>(), Array.Empty<string>());
            ConfigureMountSequence(
                mounts, "mount.satsuma.steering-column",
                new[] { "mount.satsuma.steering-rack" },
                Array.Empty<string>(), Array.Empty<string>());
            ConfigureMountInstallationChecks(
                mounts, "mount.satsuma.steering-rack", "mount.satsuma.sub-frame");
            ConfigureMountInstallationChecks(
                mounts, "mount.satsuma.steering-column", "mount.satsuma.steering-rack");
            foreach (string corner in new[] { "fl", "fr" })
            {
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.wishbone-" + corner,
                    "mount.satsuma.sub-frame");
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.spindle-" + corner,
                    "mount.satsuma.wishbone-" + corner);
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.strut-" + corner,
                    "mount.satsuma.spindle-" + corner);
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.discbrake-" + corner,
                    "mount.satsuma.spindle-" + corner);
                ConfigureMountSequence(
                    mounts, "mount.satsuma.steering-rod-" + corner,
                    new[] { "mount.satsuma.steering-rack" },
                    Array.Empty<string>(), Array.Empty<string>());
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.steering-rod-" + corner,
                    "mount.satsuma.steering-rack");

                // Halfshaft Assembly Brake bolted has the opposite branch:
                // an installed disc must be unbolted before the shaft fits.
                // Both shafts are interchangeable, so this belongs to the
                // selected corner mount rather than a global part dependency.
                ConfigureMountInstallationChecks(
                    mounts, "mount.satsuma.halfshaft-" + corner, string.Empty,
                    new[] { "mount.satsuma.discbrake-" + corner });
            }
        }

        private static void ConfigureSatsumaRemovalChecks(
            IReadOnlyList<MountBuild> mounts)
        {
            foreach (string corner in new[] { "fl", "fr" })
            {
                // Removal FSM 108086/108453 reads steering-rod Installed,
                // independent of the rod's own Bolted latch.
                ConfigureMountRemovalBlockers(
                    mounts,
                    "mount.satsuma.strut-" + corner,
                    new[] { "mount.satsuma.steering-rod-" + corner });

                // Removal FSM 107526/110144 reads disc Bolted. Keeping this
                // separate from InstallationBlockedWhileBolted preserves the
                // opposite installation/removal predicates.
                ConfigureMountRemovalChecks(
                    mounts,
                    "mount.satsuma.halfshaft-" + corner,
                    new[] { "mount.satsuma.discbrake-" + corner },
                    Array.Empty<string>());

                // Donor disc Removal checks wheel presence, not the halfshaft;
                // spindle Removal checks strut presence, not the disc. Suppress
                // only those two inferred reverse installation dependencies.
                ConfigureMountRemovalChecks(
                    mounts,
                    "mount.satsuma.discbrake-" + corner,
                    Array.Empty<string>(),
                    new[] { "mount.satsuma.halfshaft-" + corner });
                ConfigureMountRemovalChecks(
                    mounts,
                    "mount.satsuma.spindle-" + corner,
                    Array.Empty<string>(),
                    new[] { "mount.satsuma.discbrake-" + corner });
            }

            foreach (string corner in new[] { "rl", "rr" })
            {
                // Rear arm Assembly 106827/104310 blocks installation while
                // the same-corner stock shock is installed. This is an install
                // gate only; the pending spring/manual-removal outcome remains
                // governed by the existing reverse dependency behavior.
                ConfigureMountSequence(
                    mounts,
                    "mount.satsuma.trail-arm-" + corner,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { "mount.satsuma.shock-" + corner });

                // All six reviewed rear Assembly FSMs reject an install while
                // the same-corner stock shock is present. Preserve the existing
                // stock/long mutual exclusion alongside that new install gate.
                ConfigureMountSequence(
                    mounts,
                    "mount.satsuma.coilspring-" + corner,
                    new[] { "mount.satsuma.trail-arm-" + corner },
                    Array.Empty<string>(),
                    new[]
                    {
                        "mount.satsuma.long-coilspring-" + corner,
                        "mount.satsuma.shock-" + corner,
                    },
                    new[] { "mount.satsuma.trail-arm-" + corner });
                ConfigureMountSequence(
                    mounts,
                    "mount.satsuma.long-coilspring-" + corner,
                    new[] { "mount.satsuma.trail-arm-" + corner },
                    Array.Empty<string>(),
                    new[]
                    {
                        "mount.satsuma.coilspring-" + corner,
                        "mount.satsuma.shock-" + corner,
                    },
                    new[] { "mount.satsuma.trail-arm-" + corner });

                // Preserve the independently accepted spring removal gate.
                ConfigureMountRemovalBlockers(
                    mounts,
                    "mount.satsuma.coilspring-" + corner,
                    new[] { "mount.satsuma.shock-" + corner });
                ConfigureMountRemovalBlockers(
                    mounts,
                    "mount.satsuma.long-coilspring-" + corner,
                    new[] { "mount.satsuma.shock-" + corner });
            }
        }

        private static void ConfigureMountInstallationChecks(
            IReadOnlyList<MountBuild> mounts,
            string mountId,
            string boltedSupportMountId,
            string[] blockedWhileBoltedMountIds = null)
        {
            MountBuild build = mounts.SingleOrDefault(value =>
                value.Definition != null &&
                string.Equals(value.Definition.DefinitionId, mountId,
                    StringComparison.Ordinal));
            if (build.Definition == null)
            {
                throw new InvalidOperationException(
                    "Required Satsuma installation-check mount is missing: " + mountId);
            }

            build.Definition.ConfigureInstallationChecks(
                boltedSupportMountId, blockedWhileBoltedMountIds);
            EditorUtility.SetDirty(build.Definition);
        }

        private static void ConfigureMountSequence(
            IReadOnlyList<MountBuild> mounts,
            string mountId,
            string[] required,
            string[] requiredAny,
            string[] blocked,
            string[] requiredBolted = null,
            string[] requiredAnyBolted = null)
        {
            MountBuild build = mounts.SingleOrDefault(value =>
                value.Definition != null &&
                string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (build.Definition == null)
            {
                throw new InvalidOperationException(
                    "Required Satsuma sequence mount is missing: " + mountId);
            }

            build.Definition.ConfigureSequence(required, requiredAny, blocked);
            build.Definition.ConfigureBoltedSequence(
                requiredBolted,
                requiredAnyBolted);
            EditorUtility.SetDirty(build.Definition);
        }

        private static void ConfigureMountRemovalBlockers(
            IReadOnlyList<MountBuild> mounts,
            string mountId,
            string[] blockedMountIds)
        {
            MountBuild build = mounts.SingleOrDefault(value =>
                value.Definition != null &&
                string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (build.Definition == null)
            {
                throw new InvalidOperationException(
                    "Required Satsuma removal mount is missing: " + mountId);
            }

            build.Definition.ConfigureRemovalBlockers(blockedMountIds);
            EditorUtility.SetDirty(build.Definition);
        }

        private static void ConfigureMountRemovalChecks(
            IReadOnlyList<MountBuild> mounts,
            string mountId,
            string[] blockedWhileBoltedMountIds,
            string[] ignoredDependentMountIds)
        {
            MountBuild build = mounts.SingleOrDefault(value =>
                value.Definition != null &&
                string.Equals(
                    value.Definition.DefinitionId,
                    mountId,
                    StringComparison.Ordinal));
            if (build.Definition == null)
            {
                throw new InvalidOperationException(
                    "Required Satsuma removal-check mount is missing: " + mountId);
            }

            build.Definition.ConfigureRemovalChecks(
                blockedWhileBoltedMountIds,
                ignoredDependentMountIds);
            EditorUtility.SetDirty(build.Definition);
        }

        private static NwhAssemblyWheelSupportBinding BuildSatsumaWheelBinding(
            int index,
            WheelController wheel)
        {
            if (index < FrontSuspensionRuntimeRigPoses.Length)
            {
                FrontSuspensionRuntimeRigPose pose =
                    FrontSuspensionRuntimeRigPoses[index];
                var stageProfile = new NwhAssemblyWheelStageProfile(
                    pose.RoadWheelMountId,
                    "mount.satsuma.strut-" + pose.CornerId,
                    assemblyRadiusMeters: 0.12f,
                    assemblyWidthMeters: 0.1f,
                    rimRadiusMeters: DonorWheelRimRadiusMeters,
                    rimWidthMeters: DonorWheelRimWidthMeters,
                    roadRadiusMeters: DonorStockTireRadiusMeters,
                    roadWidthMeters: DonorStockTireWidthMeters,
                    configuredSpringOnlyDamperRate:
                        SatsumaRearSuspensionForce.NoShockDamper,
                    configuredDampedBumpRate: wheel.DamperBumpRate,
                    configuredDampedReboundRate: wheel.DamperReboundRate,
                    configuredAssemblyLongitudinalGrip: 0.35f,
                    configuredAssemblyLateralGrip: 0.42f,
                    configuredRoadLongitudinalGrip:
                        wheel.forwardFriction.grip,
                    configuredRoadLateralGrip: wheel.sideFriction.grip);
                return new NwhAssemblyWheelSupportBinding(
                    WheelIds[index],
                    wheel,
                    new[]
                    {
                        "mount.satsuma.wishbone-" + pose.CornerId,
                    },
                    Array.Empty<string>(),
                    stageProfile,
                    new NwhAssemblySuspensionStageProfile(
                        "mount.satsuma.strut-" + pose.CornerId,
                        // Frozen Wheel.cs still probes the ground before a
                        // strut exists. k=2 N/m is a near-free suspension,
                        // NOT a 2 kN/m supporting spring. TireStatus/Rim off
                        // supplies the existing 0.12 m assembly contact.
                        NwhAssemblySuspensionStage.CreateUnstrung(
                            pose.CanonicalNoStrutHubLocalPosition +
                                Vector3.up * 0.20f,
                            travel: 0.20f,
                            springRateNewtonPerMeter: 2f,
                            damperRateNewtonSecondsPerMeter: 2f,
                            transitionVelocity: 0.3f,
                            fastDamperFactor: 0.3f,
                            configuredCamberDegrees: wheel.Camber),
                        NwhAssemblySuspensionStage.Capture(wheel)));
            }

            string rearCorner = index == 2 ? "rl" : "rr";
            Vector3 noSpringTop = WheelAnchors[index];
            noSpringTop.y = SatsumaRearSuspensionTravel.NoSpringWheelRootY;
            Vector3 stockTop = WheelAnchors[index];
            stockTop.y = SatsumaRearSuspensionTravel.StockWheelRootY;
            Vector3 longTop = WheelAnchors[index];
            longTop.y = SatsumaRearSuspensionTravel.LongWheelRootY;
            var rearStageProfile = new NwhAssemblyWheelStageProfile(
                "mount.satsuma.wheel" + rearCorner + "-new",
                "mount.satsuma.shock-" + rearCorner,
                assemblyRadiusMeters: DonorRearDrumContactRadiusMeters,
                assemblyWidthMeters: DonorRearDrumContactWidthMeters,
                rimRadiusMeters: DonorWheelRimRadiusMeters,
                rimWidthMeters: DonorWheelRimWidthMeters,
                roadRadiusMeters: DonorStockTireRadiusMeters,
                roadWidthMeters: DonorStockTireWidthMeters,
                configuredSpringOnlyDamperRate:
                    SatsumaRearSuspensionForce.NoShockDamper,
                configuredDampedBumpRate:
                    SatsumaRearSuspensionForce.StockShockDamper,
                configuredDampedReboundRate:
                    SatsumaRearSuspensionForce.StockShockDamper,
                configuredAssemblyLongitudinalGrip: 0.35f,
                configuredAssemblyLateralGrip: 0.42f,
                configuredRoadLongitudinalGrip: wheel.forwardFriction.grip,
                configuredRoadLateralGrip: wheel.sideFriction.grip);
            return new NwhAssemblyWheelSupportBinding(
                WheelIds[index],
                wheel,
                new[]
                {
                    "mount.satsuma.trail-arm-" + rearCorner,
                    "mount.satsuma.drum-brake-" + rearCorner,
                },
                Array.Empty<string>(),
                rearStageProfile,
                new NwhAssemblySuspensionStageProfile(
                    "mount.satsuma.coilspring-" + rearCorner,
                    "mount.satsuma.long-coilspring-" + rearCorner,
                    NwhAssemblySuspensionStage.CreateUnstrung(
                        noSpringTop,
                        SatsumaRearSuspensionTravel.StockSuspensionTravel,
                        SatsumaRearSuspensionForce.NoSpringWheelRate,
                        SatsumaRearSuspensionForce.NoShockDamper,
                        SatsumaRearSuspensionForce.FastDamperSpeed,
                        SatsumaRearSuspensionForce.FastDamperFactor),
                    NwhAssemblySuspensionStage.CreateUnstrung(
                        stockTop,
                        SatsumaRearSuspensionTravel.StockSuspensionTravel,
                        SatsumaRearSuspensionForce.StockWheelRate,
                        SatsumaRearSuspensionForce.NoShockDamper,
                        SatsumaRearSuspensionForce.FastDamperSpeed,
                        SatsumaRearSuspensionForce.FastDamperFactor),
                    NwhAssemblySuspensionStage.CreateUnstrung(
                        longTop,
                        SatsumaRearSuspensionTravel.LongSuspensionTravel,
                        SatsumaRearSuspensionForce.LongWheelRate,
                        SatsumaRearSuspensionForce.NoShockDamper,
                        SatsumaRearSuspensionForce.FastDamperSpeed,
                        SatsumaRearSuspensionForce.FastDamperFactor)));
        }

        private static void BuildRestrictedInteriorPostureVolume(
            Transform vehicleRoot)
        {
            var volumeObject = new GameObject(
                "Project Vehicle Interior Posture Volume");
            volumeObject.transform.SetParent(vehicleRoot, false);
            // Bounds follow the four donor PlayerColl floor/rocker shapes but
            // remain a trigger, so they describe occupancy without sealing the
            // doorway or pushing carried parts out of the cabin.
            volumeObject.transform.localPosition =
                new Vector3(0f, 0.35f, -0.103f);
            volumeObject.transform.localRotation = Quaternion.identity;
            BoxCollider volumeCollider =
                volumeObject.AddComponent<BoxCollider>();
            volumeCollider.center = Vector3.zero;
            volumeCollider.size = new Vector3(1.28f, 1.2f, 1.67f);
            volumeCollider.isTrigger = true;
            RestrictedInteriorPostureVolume volume =
                volumeObject.AddComponent<RestrictedInteriorPostureVolume>();
            volume.Configure(volumeCollider);
        }

        private static void BuildJackLiftPoints(
            Transform vehicleRoot,
            Rigidbody chassis)
        {
            // The donor relies on tiny, awkward trigger volumes. These four
            // explicit pads preserve the chassis-relative areas while giving
            // the player a humane capture radius for physical jack placement.
            Vector3[] localPositions =
            {
                new Vector3(-0.56f, -0.26f, 0.83f),
                new Vector3(0.56f, -0.26f, 0.83f),
                new Vector3(-0.54f, -0.26f, -0.82f),
                new Vector3(0.54f, -0.26f, -0.82f),
            };
            string[] names =
            {
                "Jack lift point FL",
                "Jack lift point FR",
                "Jack lift point RL",
                "Jack lift point RR",
            };
            for (int index = 0; index < localPositions.Length; index++)
            {
                var pointObject = new GameObject(names[index]);
                pointObject.transform.SetParent(vehicleRoot, false);
                pointObject.transform.localPosition = localPositions[index];
                pointObject.transform.localRotation = Quaternion.identity;
                VehicleJackLiftPoint point =
                    pointObject.AddComponent<VehicleJackLiftPoint>();
                point.Configure(chassis, configuredCaptureRadius: 0.2f);
            }
        }

        private static AssemblyDependency[] BuildVerifiedAssemblyDependencies(
            DonorUnitySceneModel scene,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<DonorAssemblyFsmEvidence> loosePartAssemblyEvidence)
        {
            var dependencies = new List<AssemblyDependency>
            {
                InstallRequires("wishbone-fl", "sub-frame"),
                RemovalBlocked("sub-frame", "wishbone-fl"),
                InstallRequires("wishbone-fr", "sub-frame"),
                RemovalBlocked("sub-frame", "wishbone-fr"),
                InstallRequires("spindle-fl", "wishbone-fl"),
                RemovalBlocked("wishbone-fl", "spindle-fl"),
                InstallRequires("spindle-fr", "wishbone-fr"),
                RemovalBlocked("wishbone-fr", "spindle-fr"),
                InstallRequires("strut-fl", "spindle-fl"),
                RemovalBlocked("spindle-fl", "strut-fl"),
                InstallRequires("strut-fr", "spindle-fr"),
                RemovalBlocked("spindle-fr", "strut-fr"),
                InstallRequires("steering-rack", "sub-frame"),
                RemovalBlocked("sub-frame", "steering-rack"),
                InstallRequires("steering-column", "steering-rack"),
                RemovalBlocked("steering-rack", "steering-column"),
                InstallRequires("steering-rod-fl", "steering-rack"),
                RemovalBlocked("steering-rack", "steering-rod-fl"),
                InstallRequires("steering-rod-fr", "steering-rack"),
                RemovalBlocked("steering-rack", "steering-rod-fr"),
            };

            var keys = new HashSet<string>(
                dependencies.Select(BuildDependencyKey),
                StringComparer.Ordinal);
            foreach (DonorAssemblyFsmEvidence evidence in
                     loosePartAssemblyEvidence)
            {
                LoosePartBuild[] dependentParts =
                    ResolveAssemblyPartCandidates(
                        evidence,
                        loosePartBuilds);
                if (dependentParts.Length == 0)
                {
                    continue;
                }

                // Positional donor triggers may accept interchangeable physical
                // instances (springs, drums, wheels). A part-level dependency
                // cannot encode "the prerequisite for the chosen corner" and
                // would incorrectly require both corners at once. Their mount
                // owner/occupancy contract remains authoritative instead.
                if (dependentParts.Length != 1)
                {
                    continue;
                }

                foreach (LoosePartBuild dependentPart in dependentParts)
                {
                    string dependentPartDefinitionId =
                        dependentPart.Definition.DefinitionId;
                    foreach (long requiredGameObjectId in new[]
                             {
                                 evidence.RequiredGameObjectId,
                                 evidence.Required1GameObjectId,
                             }.Where(value => value != 0L).Distinct())
                    {
                        if (!TryResolveLoosePartDefinitionId(
                                loosePartBuilds,
                                ResolveGameObjectName(
                                    scene,
                                    requiredGameObjectId),
                                out string requiredPartDefinitionId))
                        {
                            continue;
                        }

                        AddDependencyIfUnique(
                            dependencies,
                            keys,
                            AssemblyDependency.Create(
                                dependentPartDefinitionId,
                                requiredPartDefinitionId,
                                AssemblyDependencyKind.InstallRequiresInstalled));
                        AddDependencyIfUnique(
                            dependencies,
                            keys,
                            AssemblyDependency.Create(
                                requiredPartDefinitionId,
                                dependentPartDefinitionId,
                                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
                    }

                    if (evidence.DetachPartGameObjectId != 0L &&
                        TryResolveLoosePartDefinitionId(
                            loosePartBuilds,
                            ResolveGameObjectName(
                                scene,
                                evidence.DetachPartGameObjectId),
                            out string detachPartDefinitionId))
                    {
                        AddDependencyIfUnique(
                            dependencies,
                            keys,
                            AssemblyDependency.Create(
                                detachPartDefinitionId,
                                dependentPartDefinitionId,
                                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
                    }
                }
            }

            return dependencies
                .OrderBy(value => value.DependentPartDefinitionId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.RelatedPartDefinitionId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.Kind)
                .ToArray();
        }

        private static bool TryResolveLoosePartDefinitionId(
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            string donorName,
            out string definitionId)
        {
            string identity = CompactAssemblyIdentity(donorName);
            LoosePartBuild[] matches = loosePartBuilds
                .Where(value => string.Equals(
                    CompactAssemblyIdentity(value.Source.DonorName),
                    identity,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 1)
            {
                definitionId = matches[0].Definition.DefinitionId;
                return true;
            }

            definitionId = string.Empty;
            return false;
        }

        private static void AddDependencyIfUnique(
            ICollection<AssemblyDependency> dependencies,
            ISet<string> keys,
            AssemblyDependency dependency)
        {
            if (keys.Add(BuildDependencyKey(dependency)))
            {
                dependencies.Add(dependency);
            }
        }

        private static string BuildDependencyKey(AssemblyDependency dependency) =>
            ((int)dependency.Kind).ToString(CultureInfo.InvariantCulture) + "|" +
            dependency.DependentPartDefinitionId + "|" +
            dependency.RelatedPartDefinitionId;

        private static ToolDefinition[] BuildToolAssets(
            IReadOnlyList<MountBuild> mountBuilds)
        {
            FastenerSize[] sizes = mountBuilds
                .SelectMany(build => build.Fasteners)
                .Select(build => build.Definition.Size)
                .Where(size => size != FastenerSize.None)
                .Distinct()
                .OrderBy(size => (int)size)
                .ToArray();
            var tools = new ToolDefinition[sizes.Length];
            for (int index = 0; index < sizes.Length; index++)
            {
                FastenerSize size = sizes[index];
                int millimeters = (int)size;
                var definition = ScriptableObject
                    .CreateInstance<ToolDefinition>();
                definition.Configure(
                    "tool.satsuma.wrench." + millimeters,
                    "Wrench " + millimeters + " mm",
                    "Wrench",
                    size);
                string path = ToolDefinitionRoot + "/wrench-" +
                              millimeters + "mm.asset";
                AssetDatabase.CreateAsset(definition, path);
                tools[index] = definition;
            }

            return tools;
        }

        private static AssemblyDependency InstallRequires(
            string dependentSlug,
            string relatedSlug) =>
            AssemblyDependency.Create(
                "vehicle.satsuma.part." + dependentSlug,
                "vehicle.satsuma.part." + relatedSlug,
                AssemblyDependencyKind.InstallRequiresInstalled);

        private static AssemblyDependency RemovalBlocked(
            string dependentSlug,
            string installedBlockerSlug) =>
            AssemblyDependency.Create(
                "vehicle.satsuma.part." + dependentSlug,
                "vehicle.satsuma.part." + installedBlockerSlug,
                AssemblyDependencyKind.RemovalBlockedWhileInstalled);

        private static GameObject BuildRuntimePrefab(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<DonorColliderRecord> chassisColliders,
            GameObject presentationPrefab,
            PartDefinition chassisDefinition,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            int rendererCount,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<MountBuild> mountBuilds,
            IReadOnlyList<ToolDefinition> toolDefinitions,
            IReadOnlyList<AssemblyDependency> dependencies)
        {
            VehicleSimulationConfig config =
                RequireAsset<VehicleSimulationConfig>(SimulationConfigPath);
            if (!config.Validate(out string configFailure))
            {
                throw new InvalidDataException(
                    "Existing M06 vehicle config is invalid: " + configFailure);
            }

            InputActionAsset input = RequireAsset<InputActionAsset>(InputActionsPath);
            var root = new GameObject("Satsuma_Phase1_V1b");
            try
            {
                StableEntityIdAuthoring identity =
                    root.AddComponent<StableEntityIdAuthoring>();
                SetStableId(identity, StableVehicleId);

                Rigidbody body = root.AddComponent<Rigidbody>();
                // Locked donor GAME.unity Rigidbody contract. The object name
                // contains "557kg", but the serialized physical mass is 389.
                body.mass = 389f;
                body.centerOfMass = config.Dynamics.CenterOfMassMeters;
                body.linearDamping = 0.02f;
                body.angularDamping = 0.205f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // Paired actual-road audit: speculative CCD generated a
                // 9.2 kN s false floor contact and rolled this compound body.
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.isKinematic = false;
                body.useGravity = true;
                body.maxDepenetrationVelocity = 1.5f;
                body.maxLinearVelocity = 70f;
                body.maxAngularVelocity = 12f;
                root.AddComponent<VehicleSpawnPhysicsActivator>().Configure(
                    body,
                    configuredActivationFixedSteps: 2);

                GameObject presentation = (GameObject)PrefabUtility
                    .InstantiatePrefab(presentationPrefab);
                presentation.name = "Temporary Direct Import Presentation";
                presentation.transform.SetParent(root.transform, false);

                Transform collisionRoot = new GameObject(
                    "Sanitized Donor Chassis Colliders").transform;
                collisionRoot.SetParent(root.transform, false);
                Transform playerCollisionProxy = new GameObject(
                    "Project Player Collision Proxy").transform;
                playerCollisionProxy.SetParent(collisionRoot, false);
                Rigidbody playerCollisionProxyBody = playerCollisionProxy
                    .gameObject.AddComponent<Rigidbody>();
                playerCollisionProxyBody.isKinematic = true;
                playerCollisionProxyBody.useGravity = false;
                playerCollisionProxyBody.interpolation =
                    RigidbodyInterpolation.Interpolate;
                playerCollisionProxyBody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;
                var runtimeChassisColliders = new List<Collider>(
                    chassisColliders.Count);
                for (int index = 0; index < chassisColliders.Count; index++)
                {
                    DonorColliderRecord donor = chassisColliders[index];
                    int donorLayer = scene.GetGameObjectLayer(
                        donor.GameObjectId);
                    long donorTransformId = scene.GetTransformIdForGameObject(
                        donor.GameObjectId);
                    scene.GetTransformRelativeTo(
                        donorTransformId,
                        satsumaRoot.TransformId,
                        out Vector3 localPosition,
                        out Quaternion localRotation,
                        out Vector3 localScale);
                    var owner = new GameObject(
                        SanitizeName(scene.GetGameObjectName(
                            donor.GameObjectId)) + "_" +
                        donor.ComponentId.ToString(CultureInfo.InvariantCulture));
                    owner.transform.SetParent(
                        donorLayer == DonorPlayerOnlyCollisionLayer
                            ? playerCollisionProxy
                            : collisionRoot,
                        false);
                    owner.transform.SetLocalPositionAndRotation(
                        localPosition,
                        localRotation);
                    owner.transform.localScale = localScale;
                    Collider collider;
                    switch (donor.Kind)
                    {
                        case DonorColliderKind.Mesh:
                            var mesh = owner.AddComponent<MeshCollider>();
                            mesh.sharedMesh = RequireAsset<Mesh>(
                                importedMeshes[donor.MeshGuid]);
                            mesh.convex = donor.Convex;
                            collider = mesh;
                            break;
                        case DonorColliderKind.Box:
                            var box = owner.AddComponent<BoxCollider>();
                            box.center = donor.Center;
                            box.size = donor.Size;
                            collider = box;
                            break;
                        case DonorColliderKind.Sphere:
                            var sphere = owner.AddComponent<SphereCollider>();
                            sphere.center = donor.Center;
                            sphere.radius = donor.Radius;
                            collider = sphere;
                            break;
                        case DonorColliderKind.Capsule:
                            var capsule = owner.AddComponent<CapsuleCollider>();
                            capsule.center = donor.Center;
                            capsule.radius = donor.Radius;
                            capsule.height = donor.Height;
                            capsule.direction = donor.Direction;
                            collider = capsule;
                            break;
                        default:
                            throw new InvalidDataException(
                                "Unsupported donor chassis collider kind: " +
                                donor.Kind + ".");
                    }

                    collider.isTrigger = donor.IsTrigger;
                    runtimeChassisColliders.Add(collider);
                    // The donor kept this one broad convex hull on its own
                    // collision layer. Flattening it onto Default turns the
                    // entire cabin into a solid block and suspends the bare
                    // shell on a presentation proxy. Keep it registered for
                    // provenance, but let the other 28 detailed shapes own
                    // physical contact in the independent runtime.
                    bool isBroadBodyProxy =
                        donorLayer == DonorBroadBodyCollisionLayer;
                    collider.enabled = donor.Enabled && !isBroadBodyProxy;
                    if (donorLayer == DonorPlayerOnlyCollisionLayer)
                    {
                        // These four simple shapes are the donor's humane
                        // player floor/rocker proxy. They must follow the car
                        // and stop the CharacterController without putting its
                        // effectively infinite solver mass directly against
                        // the dynamic chassis Rigidbody.
                        collider.excludeLayers =
                            ~(1 << ProjectPlayerCollisionLayer);
                        collider.layerOverridePriority = 1;
                    }
                    else
                    {
                        // CharacterController contacts against the dynamic
                        // chassis can reverse a rolling 500+ kg car regardless
                        // of the player's authored mass. Physical chassis
                        // shapes still collide with the world; only the
                        // kinematic donor PlayerColl proxy meets the player.
                        collider.excludeLayers |=
                            1 << ProjectPlayerCollisionLayer;
                        collider.layerOverridePriority = Mathf.Max(
                            collider.layerOverridePriority,
                            1);
                    }
                }


                BuildRestrictedInteriorPostureVolume(root.transform);

                // Loose parts remain physically solid against the world, but
                // can be pulled out from beneath their owning shell while the
                // player carries them. Collision pairs are restored on drop.
                root.AddComponent<CarryCollisionBypassScope>().Configure(
                    runtimeChassisColliders.ToArray());

                // The locked donor Rigidbody does not override centre of mass.
                // Unity derives it from the active world-facing chassis shapes;
                // the player-only proxy is deliberately a separate kinematic
                // body and therefore contributes no phantom physical inertia.
                // The old M06 prototype value was authored for a different box
                // collider and made the bare shell unnecessarily top-heavy.
                body.ResetCenterOfMass();
                body.ResetInertiaTensor();

                PartInstance chassisPart = root.AddComponent<PartInstance>();
                chassisPart.Configure(
                    chassisDefinition,
                    identity,
                    body,
                    pickup: null,
                    isRoot: true,
                    mountedAtStart: string.Empty);

                Transform loosePartsRoot = new GameObject(
                    "Donor CARPARTS Loose Parts").transform;
                loosePartsRoot.SetParent(root.transform, false);
                PartInstance[] parts = BuildLoosePartInstances(
                    scene,
                    satsumaRoot,
                    root.transform,
                    loosePartsRoot,
                    loosePartBuilds,
                    importedMeshes)
                    .Prepend(chassisPart)
                    .ToArray();
                LegacySatsumaLoosePartsRoot looseRootBinding =
                    root.AddComponent<LegacySatsumaLoosePartsRoot>();
                looseRootBinding.Configure(loosePartsRoot);
                VehicleAssemblyController assembly =
                    root.AddComponent<VehicleAssemblyController>();
                MountPointAuthoring[] mounts = BuildMountInstances(
                    root.transform,
                    mountBuilds,
                    assembly,
                    toolDefinitions,
                    importedMeshes,
                    materials);
                assembly.Configure(
                    parts,
                    mounts,
                    dependencies.ToArray(),
                    toolDefinitions.ToArray(),
                    detachedPartsRoot: loosePartsRoot,
                    permitAdditiveSaveMigration: true,
                    configuredRetentionSpeedSource: body);
                BuildJackLiftPoints(root.transform, body);
                ConfigureInstalledPartInteractions(parts, assembly);
                Phase1SatsumaEnginePickupAuthoring.Configure(assembly);
                ConfigureBootlidHingeArmPresentation(
                    presentation,
                    parts);
                ConfigureHoodReleaseInteraction(parts);
                ConfigureRearSuspensionRuntimeRig(
                    root.transform,
                    body,
                    runtimeChassisColliders,
                    parts,
                    assembly);

                AssemblyVehiclePrerequisiteAdapter prerequisites =
                    root.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
                prerequisites.Configure(assembly);
                prerequisites.ConfigureRequirements(
                    new[] { "vehicle.satsuma.part.engine-block" },
                    new[] { "vehicle.satsuma.part.starter" },
                    new[] { "vehicle.satsuma.part.battery" },
                    new[] { "vehicle.satsuma.part.fuel-tank" },
                    new[]
                    {
                        "vehicle.satsuma.part.clutch",
                        "vehicle.satsuma.part.gearbox",
                        "vehicle.satsuma.part.differential",
                    },
                    new[]
                    {
                        "vehicle.satsuma.part.wheel-stock-fl",
                        "vehicle.satsuma.part.wheel-stock-fr",
                        "vehicle.satsuma.part.wheel-stock-rl",
                        "vehicle.satsuma.part.wheel-stock-rr",
                    });
                prerequisites.SetPrototypeAvailability(
                    hasFuel: false,
                    hasOil: false,
                    hasCoolant: false,
                    voltage: 0f);

                var physicsWheels = new WheelController[WheelIds.Length];
                var wheelBindings = new NwhAssemblyWheelSupportBinding[
                    WheelIds.Length];
                for (int index = 0; index < WheelIds.Length; index++)
                {
                    var wheelObject = new GameObject(
                        "NWH_PhysicsWheel_" + WheelIds[index]);
                    wheelObject.transform.SetParent(root.transform, false);
                    wheelObject.transform.localPosition =
                        ResolveNwhSuspensionTopLocalPosition(
                            index,
                            config.Dynamics.SuspensionTravelMeters);
                    wheelObject.transform.localRotation = Quaternion.identity;

                    WheelController wheel =
                        wheelObject.AddComponent<WheelController>();
                    bool rearWheel = index >= 2;
                    wheel.Radius = rearWheel
                        ? DonorRearDrumContactRadiusMeters
                        : config.Dynamics.WheelRadiusMeters;
                    wheel.Width = rearWheel
                        ? DonorRearDrumContactWidthMeters
                        : Mathf.Clamp(
                            config.Dynamics.WheelRadiusMeters * 0.62f,
                            0.14f,
                            0.24f);
                    wheel.Mass =
                        config.Dynamics.WheelInertiaKilogramSquareMeters /
                        Mathf.Max(
                            0.0001f,
                            config.Dynamics.WheelRadiusMeters *
                            config.Dynamics.WheelRadiusMeters);
                    wheel.SpringMaxLength = rearWheel
                        ? SatsumaRearSuspensionTravel.StockSuspensionTravel
                        : config.Dynamics.SuspensionTravelMeters;
                    if (index < FrontSuspensionRuntimeRigPoses.Length)
                    {
                        // NWH mirrors the authored camber value by wheel side.
                        // -1.4 therefore yields donor -1.4° FL / +1.4° FR.
                        wheel.Camber = -1.4f;
                    }
                    wheel.SpringMaxForce = rearWheel
                        ? SatsumaRearSuspensionForce.NoSpringWheelRate *
                          SatsumaRearSuspensionTravel.StockSuspensionTravel
                        : Mathf.Max(
                            config.Dynamics.SpringRateNewtonPerMeter *
                            config.Dynamics.SuspensionTravelMeters,
                            body.mass * Mathf.Abs(Physics.gravity.y) * 0.75f);
                    wheel.DamperBumpRate = rearWheel
                        ? SatsumaRearSuspensionForce.NoShockDamper
                        : config.Dynamics.DamperRateNewtonSecondsPerMeter;
                    wheel.DamperReboundRate = rearWheel
                        ? SatsumaRearSuspensionForce.NoShockDamper
                        : config.Dynamics.DamperRateNewtonSecondsPerMeter;
                    if (rearWheel)
                    {
                        wheel.spring.forceCurve = AnimationCurve.Linear(
                            0f,
                            0f,
                            1f,
                            1f);
                        wheel.damper.slowBump = 1f;
                        wheel.damper.fastBump =
                            SatsumaRearSuspensionForce.FastDamperFactor;
                        wheel.damper.slowRebound = 1f;
                        wheel.damper.fastRebound =
                            SatsumaRearSuspensionForce.FastDamperFactor;
                        wheel.damper.bumpDivisionVelocity =
                            SatsumaRearSuspensionForce.FastDamperSpeed;
                        wheel.damper.reboundDivisionVelocity =
                            SatsumaRearSuspensionForce.FastDamperSpeed;
                    }
                    wheel.MaxLoad = body.mass *
                        Mathf.Abs(Physics.gravity.y) * 0.5f;
                    wheel.forwardFriction.grip = index >= 2 ? 0.88f : 0.92f;
                    wheel.sideFriction.grip = index >= 2 ? 0.9f : 0.94f;
                    wheel.forwardFriction.stiffness = 0.82f;
                    wheel.sideFriction.stiffness = 0.9f;
                    wheel.rollingResistanceTorque =
                        config.Dynamics.RollingResistanceCoefficient *
                        body.mass * Mathf.Abs(Physics.gravity.y) * 0.25f *
                        config.Dynamics.WheelRadiusMeters;
                    wheel.forceApplicationPointDistance = 0.72f;
                    wheel.frictionSubsteps = 20;
                    wheel.useContactModification = true;
                    int worldSurfaceLayer =
                        LayerMask.NameToLayer("WorldSurface");
                    int worldSolidLayer = LayerMask.NameToLayer("WorldSolid");
                    // The private donor world baseline still contains reviewed
                    // driveway/floor collision on Unity's Default layer. The
                    // old synthetic test used only WorldSurface and therefore
                    // hid the live-game zero-force failure. NWH filters every
                    // collider belonging to its own chassis after Initialize,
                    // so Default is safe and is also required for jacks/props.
                    int groundLayers =
                        (1 << 0) |
                        (worldSurfaceLayer >= 0
                            ? 1 << worldSurfaceLayer
                            : 0) |
                        (worldSolidLayer >= 0
                            ? 1 << worldSolidLayer
                            : 0);
                    wheel.layerMask = groundLayers;
                    int ignoreRaycastLayer =
                        LayerMask.NameToLayer("Ignore Raycast");
                    wheel.meshColliderLayer = ignoreRaycastLayer >= 0
                        ? ignoreRaycastLayer
                        : 2;
                    physicsWheels[index] = wheel;
                    wheelBindings[index] = BuildSatsumaWheelBinding(
                        index,
                        wheel);
                }

                NwhAssemblyWheelSupportController wheelSupport = root
                    .AddComponent<NwhAssemblyWheelSupportController>();
                wheelSupport.Configure(assembly, wheelBindings);
                SatsumaRearSuspensionController rearPresentation = root
                    .GetComponent<SatsumaRearSuspensionController>();
                if (rearPresentation == null ||
                    rearPresentation.Corners.Count != 2)
                {
                    throw new InvalidDataException(
                        "Rear NWH authority requires the two-corner donor presentation rig.");
                }
                SatsumaRearNwhCornerBinding[] rearNwhCorners =
                    rearPresentation.Corners
                        .Select((corner, cornerIndex) =>
                            SatsumaRearNwhCornerBinding.Create(
                                root.transform,
                                corner,
                                physicsWheels[cornerIndex + 2],
                                RequireMountByDefinitionId(
                                    assembly.MountPoints,
                                    corner.TrailingArmMountId),
                                RequireMountByDefinitionId(
                                    assembly.MountPoints,
                                    corner.DrumMountId),
                                RequireMountByDefinitionId(
                                    assembly.MountPoints,
                                    corner.RoadWheelMountId)))
                        .ToArray();
                SatsumaRearNwhSuspensionController rearNwhController = root
                    .AddComponent<SatsumaRearNwhSuspensionController>();
                rearNwhController.Configure(
                    assembly,
                    rearPresentation,
                    rearNwhCorners);
                NwhWheelPhysicsBackend backend =
                    root.AddComponent<NwhWheelPhysicsBackend>();
                backend.Configure(body, physicsWheels);
                backend.ConfigureUnbrakedSlopeRelease(true);
                backend.ConfigureAssemblySupport(wheelSupport);
                backend.ConfigureAxleStability(
                    configuredFrontAntiRollForceNewtons: 2200f,
                    // Frozen donor rear Wheel setup has no anti-roll force.
                    configuredRearAntiRollForceNewtons: 0f);
                ConfigureFrontSuspensionRuntimeRig(
                    scene,
                    root.transform,
                    parts,
                    mounts,
                    assembly,
                    runtimeChassisColliders,
                    physicsWheels,
                    importedMeshes,
                    materials);
                SatsumaFrontSuspensionController frontSuspension = root
                    .GetComponent<SatsumaFrontSuspensionController>();
                SatsumaFrontSteeringController frontSteering = root
                    .GetComponent<SatsumaFrontSteeringController>();
                if (frontSuspension == null || frontSteering == null)
                {
                    throw new InvalidDataException(
                        "Satsuma restore synchronization requires the reviewed front rig.");
                }
                SatsumaHandbrakeController handbrake = BuildSatsumaHandbrake(
                    scene, satsumaRoot.TransformId, root.transform, parts,
                    assembly, importedMeshes, materials);
                SatsumaHandbrakeNwhAdapter handbrakeBrakes = root
                    .AddComponent<SatsumaHandbrakeNwhAdapter>();
                handbrakeBrakes.Configure(handbrake, backend,
                    physicsWheels[2], physicsWheels[3]);
                AssemblyChassisMassController massController = root
                    .AddComponent<AssemblyChassisMassController>();
                massController.Configure(
                    body,
                    assembly,
                    389f);
                SatsumaNwhPhysicsRestoreSynchronizer physicsRestore = root
                    .AddComponent<SatsumaNwhPhysicsRestoreSynchronizer>();
                physicsRestore.Configure(
                    assembly,
                    body,
                    wheelSupport,
                    frontSteering,
                    frontSuspension,
                    rearNwhController,
                    handbrakeBrakes);
                VehicleInputRouter inputRouter =
                    root.AddComponent<VehicleInputRouter>();
                inputRouter.Configure(
                    input,
                    "Vehicle",
                    config.Gearbox.ForwardGearCount,
                    initialIgnition: false);
                inputRouter.enabled = false;
                VehicleSimulationHost simulation =
                    root.AddComponent<VehicleSimulationHost>();
                simulation.Configure(config, backend, prerequisites, inputRouter);
                BuildSatsumaElectricalAndWipers(
                    scene,
                    satsumaRoot,
                    root.transform,
                    parts,
                    loosePartBuilds,
                    assembly,
                    simulation,
                    prerequisites,
                    importedMeshes,
                    materials,
                    out SatsumaElectricalSystem electrical,
                    out SatsumaWiperController wipers);
                VehiclePersistenceBinding persistence =
                    root.AddComponent<VehiclePersistenceBinding>();
                Material donorRustPaintMaterial = RequireAsset<Material>(
                    MaterialRoot + "/" +
                    DonorRustPaintMaterialSourceGuid +
                    ".mat");
                Renderer[] paintableBodyRenderers = presentation
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(value => value.sharedMaterials.Contains(
                        donorRustPaintMaterial))
                    .ToArray();
                if (paintableBodyRenderers.Length != 1)
                {
                    throw new InvalidDataException(
                        "Expected exactly one donor Satsuma rust-paint body renderer; found " +
                        paintableBodyRenderers.Length + ".");
                }
                (string definitionId, string surfaceId)[] paintablePanels =
                {
                    ("vehicle.satsuma.part.door-left", SatsumaPaintSurfaceIds.DoorLeft),
                    ("vehicle.satsuma.part.door-right", SatsumaPaintSurfaceIds.DoorRight),
                    ("vehicle.satsuma.part.fender-left", SatsumaPaintSurfaceIds.FenderLeft),
                    ("vehicle.satsuma.part.fender-right", SatsumaPaintSurfaceIds.FenderRight),
                    ("vehicle.satsuma.part.hood", SatsumaPaintSurfaceIds.Hood),
                    ("vehicle.satsuma.part.bootlid", SatsumaPaintSurfaceIds.Bootlid),
                };
                VehiclePaintStateController paint =
                    root.AddComponent<VehiclePaintStateController>();
                var paintBindingList = new List<VehiclePaintSurfaceBinding>
                {
                    CreatePaintSurfaceBinding(
                        SatsumaPaintSurfaceIds.Body,
                        paintableBodyRenderers[0],
                        donorRustPaintMaterial),
                };
                foreach ((string definitionId, string surfaceId) panel in
                         paintablePanels)
                {
                    PartInstance part = parts.Single(value =>
                        value.Definition != null &&
                        string.Equals(
                            value.Definition.DefinitionId,
                            panel.definitionId,
                            StringComparison.Ordinal));
                    Renderer[] renderers = part
                        .GetComponentsInChildren<Renderer>(true)
                        .Where(renderer => renderer.sharedMaterials.Any(
                            material => material == donorRustPaintMaterial))
                        .Distinct()
                        .ToArray();
                    if (renderers.Length != 1)
                    {
                        throw new InvalidDataException(
                            $"Expected exactly one donor rust-paint renderer " +
                            $"for '{panel.definitionId}'; found {renderers.Length}.");
                    }

                    paintBindingList.Add(CreatePaintSurfaceBinding(
                        panel.surfaceId,
                        renderers[0],
                        donorRustPaintMaterial));
                }

                VehiclePaintSurfaceBinding[] paintBindings =
                    paintBindingList.ToArray();
                if (paintBindings.Length != SatsumaPaintSurfaceIds.All.Count)
                {
                    throw new InvalidDataException(
                        "The generated Satsuma must bind all seven donor paint surfaces.");
                }
                if (paintBindings.Any(binding =>
                        binding.MaterialIndices.Count == 0))
                {
                    throw new InvalidDataException(
                        "A Satsuma paint binding has no CAR_PAINT_RUSTY material slot.");
                }
                VehiclePaintMaterialProfile[] paintProfiles =
                {
                    new(
                        VehiclePaintType.NoChange,
                        donorRustPaintMaterial,
                        shouldApplyBodyColor: true),
                    new(
                        VehiclePaintType.Regular,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorRegularPaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: true),
                    new(
                        VehiclePaintType.Metallic,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorMetallicPaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: true),
                    new(
                        VehiclePaintType.Matte,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorMattePaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: true),
                    new(
                        VehiclePaintType.Art,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorArtPaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: true),
                    new(
                        VehiclePaintType.Gt,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorGtPaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: false),
                    new(
                        VehiclePaintType.Gt2,
                        RequireAsset<Material>(MaterialRoot + "/" +
                            DonorGt2PaintMaterialSourceGuid + ".mat"),
                        shouldApplyBodyColor: false),
                };
                paint.Configure(paintBindings, paintProfiles);
                ConfigureVehicleGlassRainPresentation(root, body, wipers);
                persistence.Configure(
                    identity,
                    assembly,
                    simulation,
                    inputRouter,
                    body,
                    paint,
                    massController,
                    physicsRestore,
                    electrical,
                    wipers,
                    handbrake);

                Phase1SatsumaIgnitionAuthoring.ApplyBindings(root, GeneratedRoot);
                Phase1SatsumaEngineFastenerPresentation.ApplyReviewedMeshes(
                    root,
                    GeneratedRoot);
                Phase1SatsumaEngineAdditionalFastenerPresentation.ApplyReviewedMeshes(
                    root,
                    GeneratedRoot);
                Phase1SatsumaCamshaftTimingAuthoring.ApplyToInstance(assembly, GeneratedRoot);
                Phase1SatsumaEngineScrewdriverAuthoring.Configure(assembly,
                    Phase1SatsumaEngineScrewdriverAuthoring.GetOrCreateScrewdriver(ToolDefinitionRoot));
                Phase1SatsumaEngineCapAndCoverAuthoring.Configure(root, GeneratedRoot);
                Phase1SatsumaEngineAdjustmentsAuthoring.ApplyToInstance(assembly, GeneratedRoot);
                Phase1SatsumaValveAdjustmentsAuthoring.ApplyToInstance(assembly);
                Phase1SatsumaServiceCapsAuthoring.ApplyToInstance(assembly, GeneratedRoot);
            Phase1SatsumaOperatingAuthoring.ApplyToInstance(assembly);
            Phase1SatsumaEngineMotionAuthoring.ApplyToInstance(assembly);
                Phase1SatsumaEngineFastenerTravel.ApplyReviewedTravel(root);
                Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(assembly);
                Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly);

                // Additive first-start packet runs after the original 117-mount
                // assembly passes. Keep staging asset references within staging.
                Phase1SatsumaInstalledBeltAssets.ImportReviewedAssets(GeneratedRoot);
                Phase1SatsumaStartableCarNightBatch.ApplyToInstance(assembly, GeneratedRoot);
                Phase1SatsumaInstrumentAuthoring.ApplyToInstance(assembly, GeneratedRoot);
                Phase1SatsumaFlexibleConnectionsAuthoring.ApplyToInstance(assembly, GeneratedRoot);

                LegacySatsumaBaselineMetadata metadata =
                    root.AddComponent<LegacySatsumaBaselineMetadata>();
                metadata.Configure(
                    StableVehicleId,
                    LockedSceneSha256,
                    satsumaRoot.TransformId,
                    scene.GetHierarchyPath(satsumaRoot.TransformId),
                    ProjectSpawnPosition,
                    ProjectSpawnRotation,
                    rendererCount,
                    chassisColliders.Count);
                metadata.ConfigureLoosePartRoster(
                    loosePartBuilds.Count,
                    loosePartBuilds.Count(value =>
                        IsLoosePartActiveAtNewGame(value.Source)));

            Phase1SatsumaDriverStationAuthoring.ApplyToInstance(assembly);
            Phase1SatsumaCockpitSteeringAuthoring.ApplyToInstance(assembly);
            Phase1SatsumaRoadPhysicsAuthoring.ApplyToInstance(assembly);

                GameObject prefab = SavePrefabPreservingScriptReferences(
                    root,
                    ActiveRuntimePrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Could not save the Phase 1 Satsuma runtime prefab.");
                }

                AssetDatabase.ImportAsset(
                    ActiveRuntimePrefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                return RequireAsset<GameObject>(ActiveRuntimePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SatsumaHandbrakeController BuildSatsumaHandbrake(
            DonorUnitySceneModel scene,
            long satsumaRootTransformId,
            Transform vehicleRoot,
            IReadOnlyList<PartInstance> parts,
            VehicleAssemblyController assembly,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            // Frozen rod 60945 is always present on the chassis, including
            // while the removable lever/base part is lying in the garage.
            Transform rod = CreateDonorTransform(scene, 60945L,
                satsumaRootTransformId, vehicleRoot, "Handbrake fixed linkage rod");
            AddReviewedDirectRenderer(scene, 60945L, rod.gameObject,
                DonorHandbrakeRodMeshSourceGuid, importedMeshes, materials,
                DonorHandbrakeRodMaterialSourceGuid, ShadowCastingMode.Off);
            PartInstance part = parts.Single(value => value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part.handbrake");
            const string leverMeshGuid = "ab90c359dad610a4a8325d7a8a3c3162";
            Mesh leverMesh = RequireAsset<Mesh>(importedMeshes[leverMeshGuid]);
            MeshFilter lever = part.GetComponentsInChildren<MeshFilter>(true)
                .Single(value => value.sharedMesh == leverMesh);
            GameObject presentationInstance = PrefabUtility
                .GetOutermostPrefabInstanceRoot(lever.gameObject);
            if (presentationInstance != null)
            {
                if (presentationInstance == part.gameObject ||
                    !presentationInstance.transform.IsChildOf(part.transform))
                {
                    throw new InvalidDataException("Handbrake presentation escaped its part wrapper.");
                }

                // Unity refuses to reparent a nested prefab's mesh child. Only
                // unpack this generated temporary presentation, never the car.
                PrefabUtility.UnpackPrefabInstance(presentationInstance,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            // The loose donor part's root is the lever pivot, not the fixed base.
            // Moving the PartInstance would rotate its mount, base and pickup body.
            Transform pivot = new GameObject("Handbrake lever pivot").transform;
            pivot.SetParent(part.transform, false);
            lever.transform.SetParent(pivot, true);
            if (lever.transform.parent != pivot ||
                pivot.GetComponentsInChildren<MeshFilter>(true).Length != 1)
            {
                throw new InvalidDataException("Handbrake moving-only lever binding was not applied.");
            }

            SatsumaHandbrakeController handbrake = vehicleRoot.gameObject
                .AddComponent<SatsumaHandbrakeController>();
            handbrake.Configure(assembly, part, pivot);

            DonorColliderRecord source = scene.GetCollidersForGameObject(
                    9481L, includeDisabled: true, includeTriggers: true)
                .Single(value => value.ComponentId == 100769L);
            if (source.Kind != DonorColliderKind.Capsule)
            {
                throw new InvalidDataException("Locked donor handbrake interaction shape drifted.");
            }
            var targetObject = new GameObject("Handbrake held interaction");
            targetObject.transform.SetParent(part.transform, false);
            var capsule = targetObject.AddComponent<CapsuleCollider>();
            capsule.center = source.Center;
            capsule.radius = source.Radius;
            capsule.height = source.Height;
            capsule.direction = source.Direction;
            capsule.isTrigger = true;
            SatsumaHandbrakeInteractionTarget target = targetObject
                .AddComponent<SatsumaHandbrakeInteractionTarget>();
            target.Configure(handbrake, capsule);
            InteractionTargetHost host = targetObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureOutlineRenderers(lever.GetComponent<Renderer>());
            host.ConfigureSelectionPriority(45);
            return handbrake;
        }

        private static void BuildSatsumaElectricalAndWipers(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            Transform vehicleRoot,
            IReadOnlyList<PartInstance> parts,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            VehicleAssemblyController assembly,
            VehicleSimulationHost simulation,
            AssemblyVehiclePrerequisiteAdapter prerequisites,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            out SatsumaElectricalSystem electrical,
            out SatsumaWiperController wipers)
        {
            Transform electricalPresentationRoot = new GameObject(
                "Donor Satsuma Electrical Presentation").transform;
            electricalPresentationRoot.SetParent(vehicleRoot, false);

            SatsumaElectricalDonorConnectionDefinition[] connectionDefinitions =
                BuildSatsumaElectricalDonorConnectionDefinitions();
            GameObject batteryPlus = CreateDirectDonorRendererObject(
                scene,
                satsumaRoot.TransformId,
                electricalPresentationRoot,
                60044L,
                DonorWiringBatteryPlusMeshSourceGuid,
                importedMeshes,
                materials);
            GameObject batteryMinus = CreateDirectDonorRendererObject(
                scene,
                satsumaRoot.TransformId,
                electricalPresentationRoot,
                65291L,
                DonorWiringBatteryMinusMeshSourceGuid,
                importedMeshes,
                materials);
            var wirePresentations = new Dictionary<
                SatsumaElectricalConnection,
                GameObject>();
            for (int index = 0; index < connectionDefinitions.Length; index++)
            {
                SatsumaElectricalDonorConnectionDefinition definition =
                    connectionDefinitions[index];
                wirePresentations.Add(
                    definition.Connection,
                    CreateDirectDonorRendererObject(
                        scene,
                        satsumaRoot.TransformId,
                        electricalPresentationRoot,
                        definition.PresentationTransformId,
                        definition.MeshGuid,
                        importedMeshes,
                        materials));
            }

            electrical = vehicleRoot.gameObject
                .AddComponent<SatsumaElectricalSystem>();
            var electricalBindings = new List<SatsumaElectricalConnectionBinding>(
                connectionDefinitions.Length + 2)
            {
                new SatsumaElectricalConnectionBinding(
                    SatsumaElectricalPresentationRule.BatteryPositiveShoe,
                    batteryPlus),
                new SatsumaElectricalConnectionBinding(
                    SatsumaElectricalPresentationRule.BatteryNegativeShoe,
                    batteryMinus),
            };
            for (int index = 0; index < connectionDefinitions.Length; index++)
            {
                SatsumaElectricalDonorConnectionDefinition definition =
                    connectionDefinitions[index];
                electricalBindings.Add(new SatsumaElectricalConnectionBinding(
                    definition.Connection,
                    wirePresentations[definition.Connection]));
            }

            electrical.Configure(
                assembly,
                simulation,
                prerequisites,
                electricalBindings.ToArray());

            for (int index = 0; index < connectionDefinitions.Length; index++)
            {
                SatsumaElectricalDonorConnectionDefinition definition =
                    connectionDefinitions[index];
                BuildSatsumaWiringEndpoint(
                    scene,
                    satsumaRoot.TransformId,
                    vehicleRoot,
                    electrical,
                    definition.Connection,
                    0,
                    definition.FirstEndpoint);
                BuildSatsumaWiringEndpoint(
                    scene,
                    satsumaRoot.TransformId,
                    vehicleRoot,
                    electrical,
                    definition.Connection,
                    1,
                    definition.SecondEndpoint);
            }

            BuildSatsumaElectricalFastener(
                scene,
                batteryPlus.transform,
                60044L,
                53709L,
                55114L,
                electrical,
                SatsumaElectricalFastener.BatteryPositiveTerminal,
                DonorShortBoltMeshSourceGuid,
                importedMeshes,
                materials);
            BuildSatsumaElectricalFastener(
                scene,
                batteryMinus.transform,
                65291L,
                63573L,
                44832L,
                electrical,
                SatsumaElectricalFastener.BatteryNegativeTerminal,
                DonorShortBoltMeshSourceGuid,
                importedMeshes,
                materials);
            BuildSatsumaElectricalFastener(
                scene,
                wirePresentations[SatsumaElectricalConnection.Starter].transform,
                67843L,
                66724L,
                48986L,
                electrical,
                SatsumaElectricalFastener.StarterCable,
                DonorFastenerMeshSourceGuid,
                importedMeshes,
                materials);

            Transform wiperRoot = CreateDonorTransform(
                scene,
                65304L,
                satsumaRoot.TransformId,
                vehicleRoot,
                "Fixed donor wipers");
            Transform leftPivot = CreateDonorChildTransform(
                scene, 53042L, wiperRoot, "WiperLeftPivot");
            Transform leftTap = CreateDonorChildRendererTransform(
                scene,
                38584L,
                leftPivot,
                DonorWiperTapMeshSourceGuid,
                importedMeshes,
                materials);
            Transform leftRod = CreateDonorChildRendererTransform(
                scene,
                38914L,
                leftTap,
                DonorWiperRodMeshSourceGuid,
                importedMeshes,
                materials);
            Transform rightPivot = CreateDonorChildTransform(
                scene, 62572L, wiperRoot, "WiperRightPivot");
            Transform rightTap = CreateDonorChildRendererTransform(
                scene,
                39441L,
                rightPivot,
                DonorWiperTapMeshSourceGuid,
                importedMeshes,
                materials);
            Transform rightRod = CreateDonorChildRendererTransform(
                scene,
                51240L,
                rightTap,
                DonorWiperRodMeshSourceGuid,
                importedMeshes,
                materials);

            PartInstance dashboardMeters = parts.Single(value =>
                value.Definition != null &&
                string.Equals(
                    value.Definition.DefinitionId,
                    "vehicle.satsuma.part.dashboard-meters",
                    StringComparison.Ordinal));
            LoosePartBuild dashboardBuild = loosePartBuilds.Single(value =>
                value.Source.DefinitionId ==
                "vehicle.satsuma.part.dashboard-meters");
            if (dashboardBuild.Source.Root == null)
            {
                throw new InvalidDataException(
                    "Dashboard meters donor source is missing.");
            }

            Transform switchKnob = dashboardMeters
                .GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "knob_75512");
            wipers = vehicleRoot.gameObject.AddComponent<SatsumaWiperController>();
            wipers.Configure(
                electrical,
                leftTap,
                leftRod,
                rightTap,
                rightRod,
                switchKnob);

            SphereCollider switchCollider = switchKnob.gameObject
                .AddComponent<SphereCollider>();
            switchCollider.isTrigger = true;
            switchCollider.center = new Vector3(0f, -0.03f, 0f);
            switchCollider.radius = 0.025f;
            SatsumaWiperSwitchInteractionTarget switchTarget = switchKnob
                .gameObject.AddComponent<SatsumaWiperSwitchInteractionTarget>();
            switchTarget.Configure(wipers);
            InteractionTargetHost switchHost = switchKnob.gameObject
                .AddComponent<InteractionTargetHost>();
            switchHost.Configure(switchTarget);
            Renderer knobRenderer = switchKnob.GetComponent<Renderer>();
            if (knobRenderer != null)
            {
                switchHost.ConfigureOutlineRenderers(knobRenderer);
            }
            switchHost.ConfigureSelectionPriority(45);
        }

        private static SatsumaElectricalDonorConnectionDefinition[]
            BuildSatsumaElectricalDonorConnectionDefinitions()
        {
            const string EngineBlock = "vehicle.satsuma.part.engine-block";
            const string Alternator = "vehicle.satsuma.part.alternator";
            const string PlugWires = "vehicle.satsuma.part.electrics";
            const string Starter = "vehicle.satsuma.part.starter";
            const string Battery = "vehicle.satsuma.part.battery";
            const string SteeringColumn = "vehicle.satsuma.part.steering-column";
            const string Dashboard = "vehicle.satsuma.part.dashboard";
            const string DashboardMeters = "vehicle.satsuma.part.dashboard-meters";
            const string HeadlightLeft = "vehicle.satsuma.part.headlight-left";
            const string HeadlightRight = "vehicle.satsuma.part.headlight-right";
            const string RearlightLeft = "vehicle.satsuma.part.rear-light-left";
            const string RearlightRight = "vehicle.satsuma.part.rear-light-right";
            const string FuelTank = "vehicle.satsuma.part.fuel-tank";
            const string Radio = "vehicle.satsuma.part.radio";
            const string Amplifier = "vehicle.satsuma.part.amplifier";
            const string CdPlayer = "vehicle.satsuma.part.cd-player";
            const string SubwooferPanel = "vehicle.satsuma.part.subwoofer-panel";
            const string SubwooferLeft = "vehicle.satsuma.part.subwoofer-left";
            const string SubwooferRight = "vehicle.satsuma.part.subwoofer-right";
            const string GaugeAfr = "vehicle.satsuma.part.gauge-fuel-mixture";
            const string GaugeExtra = "vehicle.satsuma.part.gauge-cluster-extra";
            const string Radiator = "vehicle.satsuma.part.radiator";
            const string RacingRadiator = "vehicle.satsuma.part.radiator-racing";
            const string MarkerLeft = "vehicle.satsuma.part.marker-light-left";
            const string MarkerRight = "vehicle.satsuma.part.marker-light-right";

            return new[]
            {
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Alternator, 66635L,
                    "5ccfd79fcd2aaf14db01de51b09f58f6",
                    new SatsumaElectricalDonorEndpointDefinition(
                        41844L, "REGULATOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        44324L, "ALTERNATOR", EngineBlock, Alternator)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.AmplifierPower, 68322L,
                    "869cee1ada5c95948b1138860f57aef3",
                    new SatsumaElectricalDonorEndpointDefinition(
                        57373L, "AMPLIFIER POWER", Amplifier),
                    new SatsumaElectricalDonorEndpointDefinition(
                        57885L, "RADIO HARNESS")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.AmplifierAudio, 37309L,
                    "d3ea9bced9ed4264fb1a0b6db93c49a6",
                    new SatsumaElectricalDonorEndpointDefinition(
                        71781L, "RADIO", Dashboard, CdPlayer),
                    new SatsumaElectricalDonorEndpointDefinition(
                        71826L, "AMPLIFIER AUDIO", Amplifier)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.BatteryHarness, 42873L,
                    "32aa759866b501a4eac8558a23c3eea4",
                    new SatsumaElectricalDonorEndpointDefinition(
                        41555L, "POSITIVE TERMINAL", Battery),
                    new SatsumaElectricalDonorEndpointDefinition(
                        57488L, "MAIN HARNESS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.CoilHarness, 53935L,
                    "419972dfeb53f2f4385d9f5138c43620",
                    new SatsumaElectricalDonorEndpointDefinition(
                        52626L, "IGNITION COIL", EngineBlock, PlugWires),
                    new SatsumaElectricalDonorEndpointDefinition(
                        57998L, "MAIN HARNESS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Dash1, 47855L,
                    "3d3dd037494c743459863835417239a1",
                    new SatsumaElectricalDonorEndpointDefinition(
                        56657L, "FUSEBOX"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        70777L, "INSTRUMENT PANEL 1", Dashboard, DashboardMeters)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Dash2, 51049L,
                    "7c5e9d95b59b9034aa42181561235837",
                    new SatsumaElectricalDonorEndpointDefinition(
                        60508L, "INSTRUMENT PANEL 2", Dashboard, DashboardMeters),
                    new SatsumaElectricalDonorEndpointDefinition(
                        68787L, "FUSEBOX")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.GroundBattery, 64472L,
                    "b3ac49dc3af2f7b4a9412230029a84e6",
                    new SatsumaElectricalDonorEndpointDefinition(
                        48345L, "NEGATIVE TERMINAL", Battery),
                    new SatsumaElectricalDonorEndpointDefinition(
                        68822L,
                        "BATTERY GROUND CONNECTOR",
                        "mount.satsuma.engine-plate.starter",
                        "fastener.satsuma.engine-plate-starter.boltpm-1",
                        SatsumaElectricalSystem.FastenerMaximumStage,
                        EngineBlock,
                        Starter)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.MarkerLeft, 46703L,
                    "4a3b866b71e8b4640a8cc7046b1eacfe",
                    new SatsumaElectricalDonorEndpointDefinition(
                        55203L, "MARKER LIGHT LEFT", MarkerLeft),
                    new SatsumaElectricalDonorEndpointDefinition(
                        58259L, "FRONT LIGHTS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.MarkerRight, 57519L,
                    "39154d39fb60344489885b0f753a79ef",
                    new SatsumaElectricalDonorEndpointDefinition(
                        46091L, "MARKER LIGHT RIGHT", MarkerRight),
                    new SatsumaElectricalDonorEndpointDefinition(
                        55349L, "FRONT LIGHTS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.FuelTank, 56521L,
                    "08b26f69bf9535d418a73ade2aefc71a",
                    new SatsumaElectricalDonorEndpointDefinition(
                        45978L, "FUEL TANK", FuelTank),
                    new SatsumaElectricalDonorEndpointDefinition(
                        51731L, "REAR HARNESS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.GaugeAfr, 45497L,
                    "6825e6ff13f0c444baa4c60d12d420d7",
                    new SatsumaElectricalDonorEndpointDefinition(
                        65672L, "DASH HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        69261L, "AFR GAUGE", GaugeAfr)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.GaugeExtra, 40412L,
                    "c931e1ce0b5a0d448982de18bf5b2bb1",
                    new SatsumaElectricalDonorEndpointDefinition(
                        37329L, "DASH HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        37816L, "EXTRA GAUGES", GaugeExtra)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.FrontLightsHarness, 71083L,
                    "6ac03bfec2ae9c04b9cd49689a6d0bdc",
                    new SatsumaElectricalDonorEndpointDefinition(
                        67529L, "MAIN HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        70826L, "FRONT LIGHTS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.HeadlightLeft, 44457L,
                    "0da71a3e35b07204da0a1db3e909ad53",
                    new SatsumaElectricalDonorEndpointDefinition(
                        37388L, "FRONT LIGHTS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        70421L, "HEADLIGHT LEFT", HeadlightLeft)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.HeadlightRight, 47785L,
                    "65b14cfed2719314a967da6ba14bcda4",
                    new SatsumaElectricalDonorEndpointDefinition(
                        43848L, "HEADLIGHT RIGHT", HeadlightRight),
                    new SatsumaElectricalDonorEndpointDefinition(
                        69464L, "FRONT LIGHTS CONNECTOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Ignition, 59161L,
                    "cfe641defee778e40a43c7ed120df1f1",
                    new SatsumaElectricalDonorEndpointDefinition(
                        41681L, "FUSEBOX"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        63749L, "IGNITION SWITCH", SteeringColumn)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.RadiatorFan, 41561L,
                    "668c26486f8a1674c8e33780fc4035b2",
                    new SatsumaElectricalDonorEndpointDefinition(
                        49930L, "MAIN HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        58684L, "RADIATOR FAN CONNECTOR", true,
                        Radiator, RacingRadiator)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Radio, 36561L,
                    "4f910676b30befe428f83c68f16216aa",
                    new SatsumaElectricalDonorEndpointDefinition(
                        38589L, "RADIO HARNESS"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        53201L, "RADIO", Radio)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.RearlightLeft, 57481L,
                    "400a68d53f8e56244b5055cf5fe957fc",
                    new SatsumaElectricalDonorEndpointDefinition(
                        41971L, "REAR HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        64591L, "REARLIGHT LEFT", RearlightLeft)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.RearlightRight, 42145L,
                    "acc7629d4f4718043891bd8915f7d62a",
                    new SatsumaElectricalDonorEndpointDefinition(
                        58001L, "REAR HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        67494L, "REARLIGHT RIGHT", RearlightRight)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.RegulatorHarness, 63726L,
                    "2816aa1ae63beba47acb9049e10aecb4",
                    new SatsumaElectricalDonorEndpointDefinition(
                        39474L, "MAIN HARNESS CONNECTOR"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        68358L, "REGULATOR")),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.Starter, 67843L,
                    "5e66c44bc1e7b6b4289c10c20c2cd22e",
                    new SatsumaElectricalDonorEndpointDefinition(
                        61154L, "STARTER", EngineBlock, Starter),
                    new SatsumaElectricalDonorEndpointDefinition(
                        62386L, "POSITIVE TERMINAL", Battery)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.SubwooferLeft, 58974L,
                    "889bc1d4c1e6ae440b194b5eda935569",
                    new SatsumaElectricalDonorEndpointDefinition(
                        60335L, "SUBWOOFER LEFT", SubwooferLeft,
                        SubwooferPanel, Amplifier),
                    new SatsumaElectricalDonorEndpointDefinition(
                        64687L, "AUDIO OUT LEFT", SubwooferLeft,
                        SubwooferPanel, Amplifier)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.SubwooferRight, 63178L,
                    "09b2da146902a484fa3b8c7e62da45fd",
                    new SatsumaElectricalDonorEndpointDefinition(
                        50217L, "SUBWOOFER RIGHT", SubwooferRight,
                        SubwooferPanel, Amplifier),
                    new SatsumaElectricalDonorEndpointDefinition(
                        55178L, "AUDIO OUT RIGHT", SubwooferRight,
                        SubwooferPanel, Amplifier)),
                new SatsumaElectricalDonorConnectionDefinition(
                    SatsumaElectricalConnection.SwitchLights, 39247L,
                    "3f95a862b9f417d4d8a5a74cc3fa2127",
                    new SatsumaElectricalDonorEndpointDefinition(
                        43339L, "LIGHT SWITCH"),
                    new SatsumaElectricalDonorEndpointDefinition(
                        69005L, "DASH HARNESS CONNECTOR", Dashboard, DashboardMeters)),
            };
        }

        private static GameObject CreateDirectDonorRendererObject(
            DonorUnitySceneModel scene,
            long satsumaRootTransformId,
            Transform parent,
            long donorTransformId,
            string expectedMeshGuid,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            Transform owner = CreateDonorTransform(
                scene,
                donorTransformId,
                satsumaRootTransformId,
                parent,
                SanitizeName(scene.GetGameObjectName(
                    scene.GetTransform(donorTransformId).GameObjectId)));
            AddReviewedDirectRenderer(
                scene,
                donorTransformId,
                owner.gameObject,
                expectedMeshGuid,
                importedMeshes,
                materials);
            return owner.gameObject;
        }

        private static Transform CreateDonorTransform(
            DonorUnitySceneModel scene,
            long donorTransformId,
            long referenceTransformId,
            Transform parent,
            string name)
        {
            scene.GetTransformRelativeTo(
                donorTransformId,
                referenceTransformId,
                out Vector3 position,
                out Quaternion rotation,
                out Vector3 scale);
            Transform result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.SetLocalPositionAndRotation(position, rotation);
            result.localScale = scale;
            return result;
        }

        private static Transform CreateDonorChildTransform(
            DonorUnitySceneModel scene,
            long donorTransformId,
            Transform parent,
            string name)
        {
            DonorTransformRecord donor = scene.GetTransform(donorTransformId);
            Transform result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.SetLocalPositionAndRotation(
                donor.LocalPosition,
                donor.LocalRotation);
            result.localScale = donor.LocalScale;
            return result;
        }

        private static Transform CreateDonorChildRendererTransform(
            DonorUnitySceneModel scene,
            long donorTransformId,
            Transform parent,
            string expectedMeshGuid,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            Transform result = CreateDonorChildTransform(
                scene,
                donorTransformId,
                parent,
                SanitizeName(scene.GetGameObjectName(
                    scene.GetTransform(donorTransformId).GameObjectId)));
            AddReviewedDirectRenderer(
                scene,
                donorTransformId,
                result.gameObject,
                expectedMeshGuid,
                importedMeshes,
                materials);
            return result;
        }

        private static void AddReviewedDirectRenderer(
            DonorUnitySceneModel scene,
            long donorTransformId,
            GameObject owner,
            string expectedMeshGuid,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials,
            string expectedMaterialGuid = DonorStandardMetalMaterialSourceGuid,
            ShadowCastingMode shadowCastingMode = ShadowCastingMode.On)
        {
            DonorTransformRecord transform = scene.GetTransform(donorTransformId);
            DonorStaticRendererRecord[] matches = scene
                .GetStaticRenderersBelowIncludingInactive(donorTransformId)
                .Where(value => value.GameObjectId == transform.GameObjectId)
                .ToArray();
            if (matches.Length != 1 ||
                !string.Equals(
                    matches[0].MeshGuid,
                    expectedMeshGuid,
                    StringComparison.OrdinalIgnoreCase) ||
                matches[0].MaterialGuids.Count != 1 ||
                !string.Equals(
                    matches[0].MaterialGuids[0],
                    expectedMaterialGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Reviewed Satsuma direct renderer drifted at " +
                    donorTransformId.ToString(CultureInfo.InvariantCulture) + ".");
            }

            owner.gameObject.AddComponent<MeshFilter>().sharedMesh = RequireAsset<Mesh>(
                importedMeshes[expectedMeshGuid]);
            MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[expectedMaterialGuid];
            renderer.shadowCastingMode = shadowCastingMode;
            renderer.receiveShadows = true;
        }

        private static void BuildSatsumaWiringEndpoint(
            DonorUnitySceneModel scene,
            long satsumaRootTransformId,
            Transform vehicleRoot,
            SatsumaElectricalSystem electrical,
            SatsumaElectricalConnection connection,
            int endpointIndex,
            SatsumaElectricalDonorEndpointDefinition definition)
        {
            Transform owner = CreateDonorTransform(
                scene,
                definition.TransformId,
                satsumaRootTransformId,
                vehicleRoot,
                "Wiring endpoint " +
                definition.TransformId.ToString(CultureInfo.InvariantCulture));
            owner.localScale = Vector3.one;
            SphereCollider collider = owner.gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            // The donor finds an endpoint within 0.1 m of the held wiring tool.
            // Preserve that tolerance for the explicit project raycast target;
            // a 28 mm invisible hotspot made bare harness points hard to find.
            collider.radius = SatsumaElectricalSystem.DonorEndpointToleranceMeters;
            SatsumaWiringConnectorInteractionTarget target = owner.gameObject
                .AddComponent<SatsumaWiringConnectorInteractionTarget>();
            target.Configure(
                electrical,
                connection,
                endpointIndex,
                definition.Prompt,
                definition.RequiredPartDefinitionIds,
                definition.MatchAnyRequiredPart);
            if (!string.IsNullOrEmpty(definition.RequiredMountId))
            {
                // Ground endpoint 68822 reads starter array index 0: donor
                // Screw 110254 / marker 57844, not the positive-cable nut.
                // IntCompare routes Stage < 8 to ASSEMBLE, equality to LOOP.
                target.ConfigureMountFastenerGate(
                    definition.RequiredMountId,
                    definition.RequiredMountFastenerDefinitionId,
                    definition.RequiredMountFastenerMaximumStageExclusive);
            }
            InteractionTargetHost host = owner.gameObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureSelectionPriority(35);
        }

        private static void BuildSatsumaElectricalFastener(
            DonorUnitySceneModel scene,
            Transform connectionPresentation,
            long connectionRootTransformId,
            long boltTransformId,
            long rendererTransformId,
            SatsumaElectricalSystem electrical,
            SatsumaElectricalFastener fastener,
            string fastenerMeshGuid,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            int layer = LayerMask.NameToLayer(FastenerToolRaycastLayer.Name);
            if (layer < 0)
            {
                throw new InvalidDataException(
                    "Required fastener tool layer is missing.");
            }

            Transform owner = CreateDonorTransform(
                scene,
                boltTransformId,
                connectionRootTransformId,
                connectionPresentation,
                fastener + " fastener");
            owner.gameObject.layer = layer;
            Transform rendererTransform = CreateDonorChildTransform(
                scene,
                rendererTransformId,
                owner,
                fastener + " fastener mesh");
            rendererTransform.gameObject.layer = layer;
            rendererTransform.gameObject.AddComponent<MeshFilter>().sharedMesh =
                RequireAsset<Mesh>(importedMeshes[fastenerMeshGuid]);
            MeshRenderer renderer = rendererTransform.gameObject
                .AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[DonorFastenerMaterialSourceGuid];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SphereCollider collider = owner.gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.024f;
            SatsumaElectricalTerminalFastenerInteractionTarget target = owner
                .gameObject.AddComponent<
                    SatsumaElectricalTerminalFastenerInteractionTarget>();
            target.Configure(electrical, fastener, owner, renderer);
            InteractionTargetHost host = owner.gameObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureOutlineRenderers(renderer);
            host.ConfigureSelectionPriority(50);
        }

        private static void ConfigureVehicleGlassRainPresentation(
            GameObject vehicleRoot,
            Rigidbody vehicleBody,
            SatsumaWiperController wipers)
        {
            Material windshieldMaterial = RequireAsset<Material>(
                MaterialRoot + "/" +
                DonorWindshieldGlassMaterialSourceGuid + ".mat");
            Material cabinGlassMaterial = RequireAsset<Material>(
                MaterialRoot + "/" +
                DonorCabinGlassMaterialSourceGuid + ".mat");
            Renderer[] allRenderers = vehicleRoot
                .GetComponentsInChildren<Renderer>(true);
            Renderer[] windshieldRenderers = allRenderers
                .Where(value => value.sharedMaterials.Contains(
                    windshieldMaterial))
                .ToArray();
            Renderer[] cabinRenderers = allRenderers
                .Where(value => value.sharedMaterials.Contains(
                    cabinGlassMaterial))
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            if (windshieldRenderers.Length != 1 ||
                cabinRenderers.Length != 4)
            {
                throw new InvalidDataException(
                    "The donor Satsuma rain atlas requires one windshield " +
                    "and four cabin-window renderers; found " +
                    windshieldRenderers.Length + " and " +
                    cabinRenderers.Length + ".");
            }

            Renderer[] windows = windshieldRenderers
                .Concat(cabinRenderers)
                .ToArray();
            float[] opacities = windows
                .Select((_, index) => index == 0
                    ? VehicleGlassRainPresenter.DonorFrontRainOpacity
                    : VehicleGlassRainPresenter.DonorCabinRainOpacity)
                .ToArray();
            VehicleGlassRainPresenter rain = vehicleRoot
                .AddComponent<VehicleGlassRainPresenter>();
            rain.ConfigureForAuthoring(
                windows,
                opacities,
                vehicleBody,
                wipers);
        }

        private static Vector3 ResolveNwhSuspensionTopLocalPosition(
            int wheelIndex,
            float suspensionTravelMeters)
        {
            Vector3 anchor = WheelAnchors[wheelIndex];
            if (wheelIndex >= 2)
            {
                anchor.y = DonorRearDrumCenterLocalY;
                return anchor;
            }

            FrontSuspensionRuntimeRigPose pose =
                FrontSuspensionRuntimeRigPoses[wheelIndex];
            // WheelController.WheelPosition is the actual hub centre. Put the
            // NWH spring top exactly one travel above the donor full-droop hub
            // rather than confusing the fixed steering carrier with the hub.
            return pose.FullDroopHubLocalPosition +
                Vector3.up * suspensionTravelMeters;
        }

        private static MountPointAuthoring[] BuildMountInstances(
            Transform vehicleRoot,
            IReadOnlyList<MountBuild> builds,
            VehicleAssemblyController assembly,
            IReadOnlyList<ToolDefinition> toolDefinitions,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            Dictionary<string, Mesh> fastenerPresentationMeshes = builds
                .SelectMany(value => value.Fasteners)
                .Select(value => value.MeshSourceGuid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    value => value,
                    value => RequireAsset<Mesh>(importedMeshes[value]),
                    StringComparer.OrdinalIgnoreCase);
            Material fastenerPresentationMaterial = materials[
                DonorFastenerMaterialSourceGuid];
            int fastenerLayer = LayerMask.NameToLayer(
                FastenerToolRaycastLayer.Name);
            if (fastenerLayer < 0)
            {
                throw new InvalidDataException(
                    $"Required Unity layer '{FastenerToolRaycastLayer.Name}' is missing.");
            }

            Transform mountRoot = new GameObject(
                "Donor Verified Mount Points").transform;
            mountRoot.SetParent(vehicleRoot, false);
            Dictionary<string, PartInstance> partByDefinitionId = vehicleRoot
                .GetComponentsInChildren<PartInstance>(true)
                .Where(value => value.Definition != null)
                .ToDictionary(
                    value => value.Definition.DefinitionId,
                    StringComparer.Ordinal);
            var authorings = new List<MountPointAuthoring>(builds.Count);
            foreach (MountBuild build in builds)
            {
                var owner = new GameObject(build.Definition.DefinitionId);
                Transform ownerTransform = mountRoot;
                PartInstance ownerPart = null;
                if (!string.IsNullOrEmpty(build.OwnerPartDefinitionId))
                {
                    ownerPart = partByDefinitionId[
                        build.OwnerPartDefinitionId];
                    ownerTransform = ownerPart.transform;
                }
                owner.transform.SetParent(ownerTransform, false);
                owner.transform.SetLocalPositionAndRotation(
                    build.LocalPosition,
                    build.LocalRotation);
                owner.transform.localScale = Vector3.one;

                Transform pose = new GameObject("MountPose").transform;
                pose.SetParent(owner.transform, false);
                pose.localPosition = ResolveInstalledRoadWheelMountLocalPosition(
                    build.Definition.DefinitionId);
                pose.localRotation = Quaternion.identity;
                pose.localScale = Vector3.one;

                MountPointAuthoring authoring =
                    owner.AddComponent<MountPointAuthoring>();
                authoring.Configure(
                    build.Definition,
                    build.Definition.DefinitionId,
                    pose,
                    0);
                if (ownerPart != null)
                {
                    owner.AddComponent<AssemblyOwnedMountAuthoring>()
                        .Configure(
                            ownerPart,
                            authoredRequireInstalledOwner:
                                build.Definition.DefinitionId.StartsWith(
                                    "mount.satsuma.drum-brake-",
                                    StringComparison.Ordinal) ||
                                string.Equals(
                                    build.Definition.DefinitionId,
                                    "mount.satsuma.wheelrl-new",
                                    StringComparison.Ordinal) ||
                                string.Equals(
                                    build.Definition.DefinitionId,
                                    "mount.satsuma.wheelrr-new",
                                    StringComparison.Ordinal));
                }
                AssemblyMountHandoffTarget handoff =
                    owner.AddComponent<AssemblyMountHandoffTarget>();
                handoff.Configure(assembly, authoring);
                InteractionTargetHost mountHost = owner
                    .AddComponent<InteractionTargetHost>();
                mountHost.Configure(handoff);
                mountHost.ConfigureSelectionPriority(20);
                SphereCollider handoffCollider = owner.AddComponent<SphereCollider>();
                handoffCollider.isTrigger = true;
                handoffCollider.radius = ResolveMountHandoffRadius(build);
                string stockBodyPartDefinitionId = build.Definition
                    .AcceptedPartDefinitionIds
                    .SingleOrDefault(IsStockBodyFastenedPanelDefinitionId);
                PartInstance stockBodyPresentationPart = null;
                if (!string.IsNullOrEmpty(stockBodyPartDefinitionId) &&
                    !partByDefinitionId.TryGetValue(
                        stockBodyPartDefinitionId,
                        out stockBodyPresentationPart))
                {
                    throw new InvalidDataException(
                        "Stock body fastener presentation owner is missing: " +
                        stockBodyPartDefinitionId);
                }
                var fastenerTargets = new List<Transform>(
                    build.Fasteners.Count);
                for (int fastenerIndex = 0;
                     fastenerIndex < build.Fasteners.Count;
                     fastenerIndex++)
                {
                    FastenerBuild fastener = build.Fasteners[fastenerIndex];
                    var marker = new GameObject(
                        fastener.Definition.DefinitionId);
                    marker.layer = fastenerLayer;
                    marker.transform.SetParent(owner.transform, false);
                    marker.transform.SetLocalPositionAndRotation(
                        fastener.LocalPosition,
                        fastener.LocalRotation);
                    marker.transform.localScale = Vector3.one;
                    var presentationObject = new GameObject(
                        "Visible inserted bolt or nut");
                    presentationObject.layer = fastenerLayer;
                    bool preserveLooseBodyBolt =
                        stockBodyPresentationPart != null;
                    presentationObject.transform.SetParent(
                        preserveLooseBodyBolt
                            ? stockBodyPresentationPart.transform
                            : marker.transform,
                        false);
                    presentationObject.transform.localPosition =
                        preserveLooseBodyBolt
                            ? fastener.LocalPosition
                            : Vector3.zero;
                    presentationObject.transform.localRotation =
                        preserveLooseBodyBolt
                            ? fastener.LocalRotation
                            : Quaternion.identity;
                    float donorChildScale = preserveLooseBodyBolt &&
                        (string.Equals(
                             stockBodyPartDefinitionId,
                             "vehicle.satsuma.part.door-left",
                             StringComparison.Ordinal) ||
                         string.Equals(
                             stockBodyPartDefinitionId,
                             "vehicle.satsuma.part.door-right",
                             StringComparison.Ordinal))
                            ? 0.9f
                            : 1f;
                    presentationObject.transform.localScale =
                        fastener.SourceLocalScale * donorChildScale;
                    presentationObject.AddComponent<MeshFilter>().sharedMesh =
                        fastenerPresentationMeshes[fastener.MeshSourceGuid];
                    MeshRenderer fastenerRenderer = presentationObject
                        .AddComponent<MeshRenderer>();
                    fastenerRenderer.sharedMaterial =
                        fastenerPresentationMaterial;
                    // Preserve the existing part-owned presentation without
                    // keeping dead imported boltN duplicates. The scoped door
                    // visibility authorer hides its eight bolts while loose;
                    // the other panel visibility contracts remain unchanged.
                    fastenerRenderer.enabled = preserveLooseBodyBolt;
                    if (preserveLooseBodyBolt)
                    {
                        InteractionTargetHost partHost =
                            stockBodyPresentationPart.GetComponent<
                                InteractionTargetHost>();
                        if (partHost != null)
                        {
                            partHost.ConfigureOutlineRenderers(
                                partHost.OutlineRenderers
                                    .Concat(new Renderer[] { fastenerRenderer })
                                    .ToArray());
                        }
                    }
                    SphereCollider collider = marker.AddComponent<SphereCollider>();
                    collider.isTrigger = true;
                    // Preserve the donor bolt pose, but use a humane invisible
                    // raycast volume. The donor marker itself is the famous
                    // mouse-anus interaction and is not a fidelity target.
                    collider.radius = Mathf.Clamp(
                        fastener.ColliderRadiusMeters * 2.25f,
                        0.028f,
                        0.045f);
                    collider.enabled = false;
                    AssemblyFastenerInteractionTarget target = marker
                        .AddComponent<AssemblyFastenerInteractionTarget>();
                    ToolDefinition tool = toolDefinitions.Single(value =>
                        value.Size == fastener.Definition.Size &&
                        string.Equals(
                            value.ToolType,
                            "Wrench",
                            StringComparison.Ordinal));
                    target.Configure(
                        assembly,
                        build.Definition.DefinitionId,
                        fastener.Definition.DefinitionId,
                        tool,
                        reverseAtLimits: true,
                        authoredFastenerPresentation:
                            presentationObject.transform,
                        // Donor Screw moves the visible child 2.5 mm per
                        // stage below a non-uniformly scaled BoltPM marker.
                        // Body-panel visuals are reparented directly to their
                        // loose part so they follow the panel throughout assembly;
                        // carry the missing parent-Z scale explicitly or a
                        // fully tightened bootlid bolt travels twice as far.
                        authoredFastenerPresentationStageTravelScale:
                            preserveLooseBodyBolt
                                ? Mathf.Abs(fastener.SourceLocalScale.z)
                                : 1f);
                    InteractionTargetHost fastenerHost = marker
                        .AddComponent<InteractionTargetHost>();
                    fastenerHost.Configure(target);
                    fastenerHost.ConfigureOutlineRenderers(
                        new Renderer[] { fastenerRenderer });
                    fastenerHost.ConfigureSelectionPriority(40);
                    fastenerTargets.Add(marker.transform);
                }
                if (build.Hinge.HasValue)
                {
                    HingeMountBuild hinge = build.Hinge.Value;
                    AssemblyHingeMountAuthoring hingeAuthoring = owner
                        .AddComponent<AssemblyHingeMountAuthoring>();
                    hingeAuthoring.Configure(
                        authoring,
                        hinge.LocalAxis,
                        hinge.MinimumAngleDegrees,
                        hinge.MaximumAngleDegrees,
                        hinge.DonorBreakForce,
                        hinge.DonorBreakTorque,
                        hinge.DonorOpenTorqueLocal,
                        hinge.DonorCloseTorqueLocal,
                        authoredCloseLatchThresholdDegrees: 1f,
                        authoredDonorLatchBreakForce: 12000f,
                        authoredDonorLatchBreakTorque: 12000f,
                        authoredDonorOpenHoldWindowDegrees:
                            hinge.DonorOpenHoldWindowDegrees,
                        authoredRequiresReleaseBeforeOpening:
                            hinge.RequiresReleaseBeforeOpening,
                        authoredFastenerTargets: fastenerTargets.ToArray());
                }
                authorings.Add(authoring);
            }

            return authorings.ToArray();
        }

        private static Vector3 ResolveInstalledRoadWheelMountLocalPosition(
            string mountId)
        {
            if (string.Equals(
                    mountId,
                    "mount.satsuma.wheelfl-new",
                    StringComparison.Ordinal) ||
                string.Equals(
                    mountId,
                    "mount.satsuma.wheelfr-new",
                    StringComparison.Ordinal))
            {
                return new Vector3(
                    DonorRegularFrontWheelMountLocalXOffsetMeters,
                    0f,
                    0f);
            }

            if (string.Equals(
                    mountId,
                    "mount.satsuma.wheelrl-new",
                    StringComparison.Ordinal) ||
                string.Equals(
                    mountId,
                    "mount.satsuma.wheelrr-new",
                    StringComparison.Ordinal))
            {
                return new Vector3(
                    DonorRegularRearWheelMountLocalXOffsetMeters,
                    0f,
                    0f);
            }

            return Vector3.zero;
        }

        private static float ResolveMountHandoffRadius(MountBuild build)
        {
            string mountId = build.Definition != null
                ? build.Definition.DefinitionId
                : string.Empty;
            if (mountId.StartsWith(
                    "mount.satsuma.wheel",
                    StringComparison.Ordinal))
            {
                return 0.24f;
            }

            if (mountId.StartsWith(
                    "mount.satsuma.discbrake-",
                    StringComparison.Ordinal))
            {
                return 0.20f;
            }

            return Mathf.Clamp(
                build.Definition.ReferenceCandidateRadiusMeters > 0.001f
                    ? build.Definition.ReferenceCandidateRadiusMeters
                    : 0.045f,
                0.03f,
                0.075f);
        }

        private static void ConfigureInstalledPartInteractions(
            IEnumerable<PartInstance> parts,
            VehicleAssemblyController assembly)
        {
            PartInstance[] partArray = parts
                .Where(part => part != null)
                .ToArray();
            string assemblyRootDefinitionId = partArray
                .FirstOrDefault(part => part.IsAssemblyRoot)
                ?.Definition?.DefinitionId ?? string.Empty;
            foreach (PartInstance part in partArray)
            {
                if (part == null)
                {
                    continue;
                }

                InteractionTargetHost host = part.GetComponent<
                    InteractionTargetHost>();
                if (host == null)
                {
                    host = part.gameObject.AddComponent<InteractionTargetHost>();
                }

                host.AddCapability(part);

                AssemblySurfaceMountHandoffTarget surfaceHandoff = part
                    .gameObject
                    .AddComponent<AssemblySurfaceMountHandoffTarget>();
                surfaceHandoff.Configure(assembly, part);
                host.AddCapabilityFirst(surfaceHandoff);
                if (part.IsAssemblyRoot)
                {
                    continue;
                }

                AssemblyInstalledPartInteractionTarget removal = part
                    .gameObject
                    .AddComponent<AssemblyInstalledPartInteractionTarget>();
                removal.Configure(assembly, part);
                host.AddCapabilityFirst(removal);
                bool installsOnNestedSubassembly =
                    part.Definition != null &&
                    assembly.MountPoints.Any(mount =>
                        mount != null && mount.Definition != null &&
                        !string.IsNullOrEmpty(
                            mount.Definition.OwnerPartDefinitionId) &&
                        !string.Equals(
                            mount.Definition.OwnerPartDefinitionId,
                            assemblyRootDefinitionId,
                            StringComparison.Ordinal) &&
                        mount.Definition.AcceptedPartDefinitionIds.Contains(
                            part.Definition.DefinitionId,
                            StringComparer.Ordinal));
                host.ConfigureSelectionPriority(
                    installsOnNestedSubassembly ? 15 : 10);
                BuildInstalledPartInteractionProxy(part);
                part.GetComponent<AssemblyHingedPartInteractionTarget>()
                    ?.BindAssemblyController(assembly);
            }
        }

        private static void ConfigureBootlidHingeArmPresentation(
            GameObject chassisPresentation,
            IEnumerable<PartInstance> parts)
        {
            if (chassisPresentation == null)
            {
                throw new ArgumentNullException(nameof(chassisPresentation));
            }

            PartInstance bootlid = parts.Single(value =>
                value != null && value.Definition != null &&
                string.Equals(
                    value.Definition.DefinitionId,
                    "vehicle.satsuma.part.bootlid",
                    StringComparison.Ordinal));
            AssemblyHingedPartInteractionTarget hinge = bootlid
                .GetComponent<AssemblyHingedPartInteractionTarget>();
            Transform chassisArms = chassisPresentation
                .GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(value => string.Equals(
                    value.name,
                    "hooks_78823",
                    StringComparison.Ordinal));
            Transform movingArms = bootlid
                .GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(value => string.Equals(
                    value.name,
                    "hooks_77027",
                    StringComparison.Ordinal));
            if (hinge == null || chassisArms == null || movingArms == null ||
                chassisArms.GetComponent<MeshFilter>()?.sharedMesh == null ||
                movingArms.GetComponent<MeshFilter>()?.sharedMesh == null ||
                !string.Equals(
                    chassisArms.GetComponent<MeshFilter>().sharedMesh.name,
                    "bootlid_hooks",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    movingArms.GetComponent<MeshFilter>().sharedMesh.name,
                    "bootlid_hooks",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Reviewed donor bootlid hinge-arm copies are missing.");
            }

            // Frozen Assembly FSM &104306, state End: body hooks OFF,
            // Handles ON, loose-root hooks ON. Removal FSM &110021 reverses
            // those three active states. Keep both donor copies and swap which
            // one is visible at the authoritative assembly transition.
            hinge.ConfigureInstalledPresentationSwap(
                new[] { movingArms.gameObject },
                new[] { chassisArms.gameObject });
        }

        private static void ConfigureHoodReleaseInteraction(
            IEnumerable<PartInstance> parts)
        {
            PartInstance[] partArray = parts
                .Where(value => value?.Definition != null)
                .ToArray();
            PartInstance dashboard = partArray.Single(value => string.Equals(
                value.Definition.DefinitionId,
                "vehicle.satsuma.part.dashboard",
                StringComparison.Ordinal));
            PartInstance hoodPart = partArray.Single(value => string.Equals(
                value.Definition.DefinitionId,
                "vehicle.satsuma.part.hood",
                StringComparison.Ordinal));
            AssemblyHingedPartInteractionTarget hood = hoodPart
                .GetComponent<AssemblyHingedPartInteractionTarget>();
            if (hood == null)
            {
                throw new InvalidDataException(
                    "Generated hood has no hinged interaction target.");
            }

            Transform lever = dashboard
                .GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(value => string.Equals(
                    value.name,
                    "dash_hood_lock_80638",
                    StringComparison.Ordinal));
            if (lever == null)
            {
                throw new InvalidDataException(
                    "Reviewed donor dashboard hood-release visual is missing.");
            }

            SphereCollider collider = lever.gameObject
                .AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.03f;
            AssemblyHoodReleaseInteractionTarget release = lever.gameObject
                .AddComponent<AssemblyHoodReleaseInteractionTarget>();
            release.Configure(dashboard, hood, lever);
            InteractionTargetHost host = lever.gameObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(release);
            Renderer leverRenderer = lever.GetComponent<Renderer>();
            if (leverRenderer != null)
            {
                host.ConfigureOutlineRenderers(leverRenderer);
            }

            host.ConfigureSelectionPriority(35);
        }

        internal static DonorStaticRendererRecord ReadReviewedSteeringAlignmentNut(
            DonorUnitySceneModel scene,
            bool leftSide)
        {
            long markerId = leftSide ? 64267L : 42271L;
            DonorTransformRecord marker = scene.GetTransform(markerId);
            DonorStaticRendererRecord[] renderers = scene
                .GetStaticRenderersBelowIncludingInactive(markerId).ToArray();
            if (renderers.Length != 1)
            {
                throw new InvalidDataException("Expected one donor steering alignment nut.");
            }

            DonorStaticRendererRecord renderer = renderers[0];
            DonorTransformRecord visual = scene.GetTransform(
                scene.GetTransformIdForGameObject(renderer.GameObjectId));
            bool screwMatches = scene.GetMonoBehaviours(marker.GameObjectId).Any(value =>
                IsReviewedSteeringAlignmentScrew(value.SerializedBody,
                    leftSide ? 34245L : 32418L, leftSide ? 29428L : 13829L));
            if (marker.FatherTransformId != (leftSide ? 39613L : 66023L) ||
                Vector3.Distance(marker.LocalScale, Vector3.one * 1.4f) > 0.00001f ||
                visual.FatherTransformId != markerId ||
                Vector3.Distance(visual.LocalPosition,
                    new Vector3(-6.748291E-8f, 2.2814543E-8f, -0.00438223f)) > 0.000001f ||
                Quaternion.Angle(visual.LocalRotation, Quaternion.identity) > 0.001f ||
                Vector3.Distance(visual.LocalScale, Vector3.one) > 0.00001f ||
                renderer.MeshGuid != DonorFastenerMeshSourceGuid ||
                renderer.MaterialGuids.Count != 1 ||
                renderer.MaterialGuids[0] != DonorFastenerMaterialSourceGuid || !screwMatches)
            {
                throw new InvalidDataException(
                    "Donor 14 mm adjustment mesh/frame/Screw drifted: " + markerId);
            }

            return renderer;
        }

        internal static bool IsReviewedSteeringAlignmentScrew(
            string body, long visualGameObjectId, long wheelGameObjectId)
        {
            if (string.IsNullOrEmpty(body) ||
                !Regex.IsMatch(body, @"(?m)^      intVariables:\s*\[\]\s*$") ||
                !TryExtractFsmFloatVariable(body, "Alignment", out _) ||
                ExtractFsmGameObjectVariable(body, "ThisBolt") != visualGameObjectId ||
                ExtractFsmGameObjectVariable(body, "Wheel") != wheelGameObjectId)
            {
                return false;
            }

            // Bound the decoded donor actions to their named state; the 12 mm
            // fastening Screw also has numeric events but owns Stage instead.
            foreach (bool tighten in new[] { true, false })
            {
                Match state = Regex.Match(body,
                    @"(?ms)^    - name: " + (tighten ? "Screw" : "Unscrew") +
                    @"\r?\n(?<state>.*?)(?=^    - name:|\z)");
                string data = state.Groups["state"].Value;
                if (!state.Success ||
                    !data.Contains("HutongGames.PlayMaker.Actions.Rotate") ||
                    !data.Contains("HutongGames.PlayMaker.Actions.FloatAdd") ||
                    !ContainsSerializedFloat(data, tighten ? -13f : 13f) ||
                    !ContainsSerializedFloat(data, tighten ? -0.1f : 0.1f))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CreateSteeringAlignmentTarget(
            DonorUnitySceneModel scene, bool leftSide, string cornerId,
            Transform outerBone, AssemblySteeringAlignmentState alignment,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            DonorStaticRendererRecord renderer = ReadReviewedSteeringAlignmentNut(scene, leftSide);
            DonorTransformRecord marker = scene.GetTransform(leftSide ? 64267L : 42271L);
            DonorTransformRecord visual = scene.GetTransform(
                scene.GetTransformIdForGameObject(renderer.GameObjectId));
            int layer = LayerMask.NameToLayer(FastenerToolRaycastLayer.Name);
            if (layer < 0)
            {
                throw new InvalidDataException("Required fastener tool layer is missing.");
            }

            var owner = new GameObject("adjustment.satsuma.steering-rod-" + cornerId + ".toe");
            owner.layer = layer;
            owner.transform.SetParent(outerBone, false);
            owner.transform.SetLocalPositionAndRotation(marker.LocalPosition, marker.LocalRotation);
            // Keep tool-anchor offsets in metres, flattening donor marker scale
            // only into the visible nut and its non-zero child translation.
            owner.transform.localScale = Vector3.one;
            var nut = new GameObject("Visible steering alignment nut");
            nut.layer = layer;
            nut.transform.SetParent(owner.transform, false);
            nut.transform.SetLocalPositionAndRotation(
                Vector3.Scale(marker.LocalScale, visual.LocalPosition), visual.LocalRotation);
            nut.transform.localScale = Vector3.Scale(marker.LocalScale, visual.LocalScale);
            nut.AddComponent<MeshFilter>().sharedMesh = RequireAsset<Mesh>(importedMeshes[renderer.MeshGuid]);
            MeshRenderer nutRenderer = nut.AddComponent<MeshRenderer>();
            nutRenderer.sharedMaterial = materials[renderer.MaterialGuids[0]];
            nutRenderer.shadowCastingMode = ShadowCastingMode.Off;
            nutRenderer.receiveShadows = false;
            nutRenderer.enabled = false;
            SphereCollider collider = owner.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.045f;
            collider.enabled = false;
            AssemblySteeringAlignmentInteractionTarget target = owner
                .AddComponent<AssemblySteeringAlignmentInteractionTarget>();
            target.Configure(alignment, nut.transform, nutRenderer, cornerId);
            InteractionTargetHost host = owner.AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureSelectionPriority(40);
            host.ConfigureOutlineRenderers(new Renderer[] { nutRenderer });
        }

        private static void ConfigureFrontSuspensionRuntimeRig(
            DonorUnitySceneModel scene,
            Transform vehicleRoot,
            IReadOnlyList<PartInstance> parts,
            IReadOnlyList<MountPointAuthoring> mounts,
            VehicleAssemblyController assembly,
            IReadOnlyList<Collider> chassisColliders,
            IReadOnlyList<WheelController> wheels,
            IReadOnlyDictionary<string, string> importedMeshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            if (vehicleRoot == null)
            {
                throw new ArgumentNullException(nameof(vehicleRoot));
            }

            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            if (wheels == null ||
                wheels.Count < FrontSuspensionRuntimeRigPoses.Length)
            {
                throw new InvalidDataException(
                    "The front suspension rig requires both NWH front wheels.");
            }

            PartInstance[] partArray = parts
                .Where(value => value != null && value.Definition != null)
                .ToArray();
            PartInstance subframe = RequirePartByDefinitionId(
                partArray,
                "vehicle.satsuma.part.sub-frame");
            AssemblyInstalledPhysicsLink subframeLink = subframe
                .gameObject.AddComponent<AssemblyInstalledPhysicsLink>();
            subframeLink.Configure(
                subframe.Body,
                AssemblyInstalledPhysicsLinkMode.Fixed);
            Transform rigRoot = new GameObject(
                "Donor Front Suspension Runtime Rig").transform;
            rigRoot.SetParent(vehicleRoot, false);
            var bindings = new List<SatsumaFrontSuspensionCornerBinding>(
                FrontSuspensionRuntimeRigPoses.Length);

            for (int index = 0;
                 index < FrontSuspensionRuntimeRigPoses.Length;
                 index++)
            {
                FrontSuspensionRuntimeRigPose pose =
                    FrontSuspensionRuntimeRigPoses[index];
                MountPointAuthoring wishboneMount =
                    RequireMountByDefinitionId(
                        mounts,
                        "mount.satsuma.wishbone-" + pose.CornerId);
                MountPointAuthoring spindleMount =
                    RequireMountByDefinitionId(
                        mounts,
                        "mount.satsuma.spindle-" + pose.CornerId);
                MountPointAuthoring strutMount =
                    RequireMountByDefinitionId(
                        mounts,
                        "mount.satsuma.strut-" + pose.CornerId);
                MountPointAuthoring steeringRodMount =
                    RequireMountByDefinitionId(
                        mounts,
                        "mount.satsuma.steering-rod-" + pose.CornerId);
                MountPointAuthoring discBrakeMount =
                    RequireMountByDefinitionId(
                        mounts,
                        "mount.satsuma.discbrake-" + pose.CornerId);
                MountPointAuthoring roadWheelMount =
                    RequireMountByDefinitionId(
                        mounts,
                        pose.RoadWheelMountId);
                PartInstance strut = RequirePartByDefinitionId(
                    partArray,
                    "vehicle.satsuma.part.strut-" + pose.CornerId);
                PartInstance steeringRod = RequirePartByDefinitionId(
                    partArray,
                    "vehicle.satsuma.part.steering-rod-" + pose.CornerId);
                AssemblySteeringAlignmentState alignment = steeringRod.gameObject
                    .AddComponent<AssemblySteeringAlignmentState>();
                alignment.Configure(steeringRod);
                MeshRenderer looseRenderer = RequireMeshRenderer(
                    strut,
                    pose.StrutMeshName);

                Vector3 shockBottomLocalPosition =
                    pose.FullDroopHubLocalPosition +
                    pose.FullDroopHubLocalRotation *
                    pose.HubToShockBottomLocalOffset;
                Quaternion shockBottomLocalRotation =
                    pose.FullDroopHubLocalRotation *
                    pose.ShockBottomLocalRotation;
                Transform shockBottom = CreateRearRigTarget(
                    rigRoot,
                    "Front shock bottom " +
                    pose.CornerId.ToUpperInvariant(),
                    shockBottomLocalPosition,
                    shockBottomLocalRotation);

                for (int boltIndex = 1; boltIndex <= 4; boltIndex++)
                {
                    ReparentReviewedFastenerMarker(
                        strutMount,
                        "fastener.satsuma.strut-" + pose.CornerId +
                            ".lower-" + boltIndex,
                        shockBottom);
                }
                ReparentReviewedFastenerMarker(
                    steeringRodMount,
                    "fastener.satsuma.steering-rod-" +
                        pose.CornerId + ".outer-joint",
                    shockBottom);

                Vector3 steeringOuterLocalPosition =
                    pose.FullDroopHubLocalPosition +
                    pose.FullDroopHubLocalRotation *
                    pose.HubToSteeringOuterLocalOffset;
                Quaternion steeringOuterLocalRotation =
                    pose.FullDroopHubLocalRotation *
                    pose.SteeringOuterLocalRotation;
                Transform steeringOuter = CreateRearRigTarget(
                    rigRoot,
                    "Front steering outer " +
                    pose.CornerId.ToUpperInvariant(),
                    steeringOuterLocalPosition,
                    steeringOuterLocalRotation);
                CreateSteeringAlignmentTarget(scene, pose.LeftSide, pose.CornerId,
                    steeringOuter, alignment, importedMeshes, materials);

                Transform armature = new GameObject(
                    "Installed Armature_001").transform;
                armature.SetParent(strut.transform, false);
                armature.localPosition = Vector3.zero;
                armature.localRotation = new Quaternion(
                    -0.7071069f,
                    0f,
                    0f,
                    0.70710677f);
                armature.localScale = Vector3.one;
                Transform upperBone = new GameObject(
                    "Installed strut pivot").transform;
                upperBone.SetParent(armature, false);
                upperBone.localPosition = Vector3.zero;
                upperBone.localRotation = new Quaternion(
                    0.5f,
                    -0.5f,
                    0.5f,
                    0.5f);
                upperBone.localScale = Vector3.one;

                var installedObject = new GameObject(
                    "Installed skinned " + pose.StrutMeshName);
                installedObject.transform.SetParent(strut.transform, false);
                installedObject.transform.localPosition = Vector3.zero;
                installedObject.transform.localRotation = Quaternion.identity;
                installedObject.transform.localScale = Vector3.one;
                SkinnedMeshRenderer installedRenderer = installedObject
                    .AddComponent<SkinnedMeshRenderer>();
                installedRenderer.sharedMesh = looseRenderer
                    .GetComponent<MeshFilter>().sharedMesh;
                installedRenderer.sharedMaterials =
                    looseRenderer.sharedMaterials;
                installedRenderer.bones = new[] { upperBone, shockBottom };
                installedRenderer.rootBone = upperBone;
                installedRenderer.localBounds = pose.StrutLocalBounds;
                installedRenderer.updateWhenOffscreen = true;
                installedRenderer.shadowCastingMode =
                    looseRenderer.shadowCastingMode;
                installedRenderer.receiveShadows =
                    looseRenderer.receiveShadows;
                installedRenderer.lightProbeUsage =
                    looseRenderer.lightProbeUsage;
                installedRenderer.reflectionProbeUsage =
                    looseRenderer.reflectionProbeUsage;
                installedRenderer.enabled = false;

                SatsumaFrontStrutPresentation presentation = strut
                    .gameObject.AddComponent<
                        SatsumaFrontStrutPresentation>();
                presentation.Configure(
                    strut,
                    looseRenderer,
                    installedRenderer,
                    upperBone,
                    shockBottom);

                MeshRenderer steeringRodLooseRenderer =
                    RequireMeshRenderer(
                        steeringRod,
                        pose.SteeringRodMeshName);
                Transform steeringRodArmature = new GameObject(
                    "Installed steering Armature").transform;
                steeringRodArmature.SetParent(
                    steeringRod.transform,
                    false);
                steeringRodArmature.localPosition = Vector3.zero;
                steeringRodArmature.localRotation =
                    pose.SteeringRodArmatureLocalRotation;
                steeringRodArmature.localScale = Vector3.one;
                Transform steeringInnerBone = new GameObject(
                    "Installed steering rod pivot").transform;
                steeringInnerBone.SetParent(steeringRodArmature, false);
                steeringInnerBone.localPosition = Vector3.zero;
                steeringInnerBone.localRotation =
                    pose.SteeringRodPivotLocalRotation;
                steeringInnerBone.localScale = Vector3.one;

                var steeringRodInstalledObject = new GameObject(
                    "Installed skinned " + pose.SteeringRodMeshName);
                steeringRodInstalledObject.transform.SetParent(
                    steeringRod.transform,
                    false);
                steeringRodInstalledObject.transform.localPosition =
                    Vector3.zero;
                steeringRodInstalledObject.transform.localRotation =
                    Quaternion.identity;
                steeringRodInstalledObject.transform.localScale =
                    Vector3.one;
                SkinnedMeshRenderer steeringRodInstalledRenderer =
                    steeringRodInstalledObject.AddComponent<
                        SkinnedMeshRenderer>();
                steeringRodInstalledRenderer.sharedMesh =
                    steeringRodLooseRenderer.GetComponent<MeshFilter>()
                        .sharedMesh;
                steeringRodInstalledRenderer.sharedMaterials =
                    steeringRodLooseRenderer.sharedMaterials;
                steeringRodInstalledRenderer.bones = new[]
                {
                    steeringInnerBone,
                    steeringOuter,
                };
                steeringRodInstalledRenderer.rootBone = steeringInnerBone;
                steeringRodInstalledRenderer.localBounds =
                    pose.SteeringRodLocalBounds;
                steeringRodInstalledRenderer.updateWhenOffscreen = true;
                steeringRodInstalledRenderer.shadowCastingMode =
                    steeringRodLooseRenderer.shadowCastingMode;
                steeringRodInstalledRenderer.receiveShadows =
                    steeringRodLooseRenderer.receiveShadows;
                steeringRodInstalledRenderer.lightProbeUsage =
                    steeringRodLooseRenderer.lightProbeUsage;
                steeringRodInstalledRenderer.reflectionProbeUsage =
                    steeringRodLooseRenderer.reflectionProbeUsage;
                steeringRodInstalledRenderer.enabled = false;

                SatsumaFrontSteeringRodPresentation steeringPresentation =
                    steeringRod.gameObject.AddComponent<
                        SatsumaFrontSteeringRodPresentation>();
                steeringPresentation.Configure(
                    steeringRod,
                    steeringRodLooseRenderer,
                    steeringRodInstalledRenderer,
                    steeringInnerBone,
                    steeringOuter);
                bindings.Add(SatsumaFrontSuspensionCornerBinding.Create(
                    pose.CornerId,
                    wheels[index],
                    wishboneMount,
                    spindleMount,
                    strutMount,
                    shockBottom,
                    presentation,
                    steeringRodMount,
                    discBrakeMount,
                    roadWheelMount,
                    steeringOuter,
                    steeringPresentation,
                    pose.FullDroopHubLocalPosition,
                    pose.FullDroopHubLocalRotation,
                    pose.CanonicalNoStrutHubLocalPosition,
                    pose.CanonicalNoStrutHubLocalRotation,
                    pose.WishboneBodyPivotLocalPosition,
                    pose.WishboneMeshZeroLocalRotation,
                    pose.HubToWishboneTargetLocalOffset,
                    pose.HubToSpindleMeshLocalOffset,
                    pose.SpindleMeshLocalRotation,
                    pose.HubToShockBottomLocalOffset,
                    pose.ShockBottomLocalRotation,
                    pose.HubToSteeringOuterLocalOffset,
                    pose.SteeringOuterLocalRotation,
                    pose.HubToDiscBrakeLocalOffset,
                    pose.DiscBrakeLocalRotation,
                    pose.HubToRoadWheelLocalOffset,
                    pose.RoadWheelLocalRotation,
                    pose.LeftSide));
            }

            SatsumaFrontSuspensionController controller = vehicleRoot
                .gameObject.AddComponent<SatsumaFrontSuspensionController>();
            controller.Configure(
                assembly,
                chassisColliders?.Where(value => value != null).ToArray() ??
                    Array.Empty<Collider>(),
                bindings.ToArray());
            SatsumaFrontSteeringController steeringController = vehicleRoot
                .gameObject.AddComponent<SatsumaFrontSteeringController>();
            steeringController.Configure(assembly, vehicleRoot.GetComponent<Rigidbody>(),
                controller.Corners);
            vehicleRoot.GetComponent<NwhWheelPhysicsBackend>()
                .ConfigureSteeringController(steeringController);
        }

        private static MountPointAuthoring RequireMountByDefinitionId(
            IEnumerable<MountPointAuthoring> mounts,
            string definitionId)
        {
            MountPointAuthoring[] matches = mounts
                .Where(value => value != null && value.Definition != null &&
                    string.Equals(
                        value.Definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected one generated Satsuma mount " + definitionId +
                    "; found " +
                    matches.Length.ToString(CultureInfo.InvariantCulture) +
                    ".");
            }

            return matches[0];
        }

        private static void ReparentReviewedFastenerMarker(
            MountPointAuthoring mount,
            string fastenerDefinitionId,
            Transform reviewedParent)
        {
            if (mount == null || reviewedParent == null)
            {
                throw new ArgumentNullException(
                    mount == null ? nameof(mount) : nameof(reviewedParent));
            }

            Transform marker = mount.transform.Find(fastenerDefinitionId);
            if (marker == null)
            {
                throw new InvalidDataException(
                    "Reviewed moving fastener marker is missing: " +
                    fastenerDefinitionId);
            }

            // The front connection evidence is measured in pivot_shock_fl/fr,
            // represented by the moving lower-strut target reviewedParent.
            // Keep that exact local pose instead of baking the current world
            // position while the suspension is at one arbitrary travel value.
            marker.SetParent(reviewedParent, false);
        }

        private static void ConfigureRearSuspensionRuntimeRig(
            Transform vehicleRoot,
            Rigidbody chassisBody,
            IReadOnlyList<Collider> chassisColliders,
            IReadOnlyList<PartInstance> parts,
            VehicleAssemblyController assembly)
        {
            if (vehicleRoot == null)
            {
                throw new ArgumentNullException(nameof(vehicleRoot));
            }

            if (chassisBody == null)
            {
                throw new ArgumentNullException(nameof(chassisBody));
            }

            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            PartInstance[] partArray = parts
                .Where(value => value != null && value.Definition != null)
                .ToArray();
            PartInstance[] drums = partArray
                .Where(value => value.Definition.DefinitionId.StartsWith(
                    "vehicle.satsuma.part.drum-brake-",
                    StringComparison.Ordinal))
                .OrderBy(value => value.Definition.DefinitionId,
                    StringComparer.Ordinal)
                .ToArray();
            if (drums.Length != 2)
            {
                throw new InvalidDataException(
                    "The rear suspension rig requires exactly two interchangeable drums; found " +
                    drums.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            foreach (PartInstance drum in drums)
            {
                AssemblyInstalledPhysicsLink link = drum.gameObject
                    .AddComponent<AssemblyInstalledPhysicsLink>();
                link.Configure(
                    drum.Body,
                    AssemblyInstalledPhysicsLinkMode.Fixed);
            }

            PartInstance[] roadWheels = partArray
                .Where(value => IsRoadWheelDefinitionId(
                    value.Definition.DefinitionId))
                .OrderBy(
                    value => value.Definition.DefinitionId,
                    StringComparer.Ordinal)
                .ToArray();
            if (roadWheels.Length != 8)
            {
                throw new InvalidDataException(
                    "The physical road-wheel roster requires eight interchangeable wheels; found " +
                    roadWheels.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            foreach (PartInstance roadWheel in roadWheels)
            {
                AssemblyInstalledPhysicsLink wheelLink = roadWheel.gameObject
                    .AddComponent<AssemblyInstalledPhysicsLink>();
                wheelLink.Configure(
                    roadWheel.Body,
                    AssemblyInstalledPhysicsLinkMode.RoadWheelAxle);
            }

            ConfigureRearSpringPresentations(partArray);
            ConfigureRearShockPresentations(partArray);

            Transform rigRoot = new GameObject(
                "Donor Rear Suspension Runtime Rig").transform;
            rigRoot.SetParent(vehicleRoot, false);
            var bindings = new List<SatsumaRearSuspensionCornerBinding>(
                RearSuspensionRuntimeRigPoses.Length);
            foreach (RearSuspensionRuntimeRigPose pose in
                     RearSuspensionRuntimeRigPoses)
            {
                PartInstance arm = RequirePartByDefinitionId(
                    partArray,
                    "vehicle.satsuma.part.trail-arm-" + pose.CornerId);
                AssemblyInstalledPhysicsLink armLink = arm.gameObject
                    .AddComponent<AssemblyInstalledPhysicsLink>();
                MountPointAuthoring armMount = RequireMountByDefinitionId(
                    assembly.MountPoints, "mount.satsuma.trail-arm-" + pose.CornerId);
                Vector2 initialTravelLimits = SatsumaRearSuspensionTravel.ResolveArmLimits(
                    vehicleRoot.InverseTransformPoint(armMount.Pose.position),
                    hasStockSpring: false, hasLongSpring: false);
                armLink.Configure(
                    arm.Body,
                    AssemblyInstalledPhysicsLinkMode.TrailingArmHinge,
                    minimumDegrees: initialTravelLimits.x,
                    maximumDegrees: initialTravelLimits.y);

                Transform springTop = CreateRearRigTarget(
                    rigRoot,
                    "Spring top " + pose.CornerId.ToUpperInvariant(),
                    pose.SpringTopCarLocalPosition,
                    pose.SpringTopCarLocalRotation);
                Transform springBottom = CreateRearRigTarget(
                    arm.transform,
                    "Spring bottom anchor " +
                    pose.CornerId.ToUpperInvariant(),
                    pose.SpringBottomArmLocalPosition,
                    pose.SpringBottomArmLocalRotation);
                Transform springBottomRender = CreateRearRigTarget(
                    rigRoot,
                    "Spring animated bottom bone " +
                    pose.CornerId.ToUpperInvariant(),
                    pose.SpringTopCarLocalPosition,
                    pose.SpringBottomArmLocalRotation);
                Transform shockTop = CreateRearRigTarget(
                    rigRoot,
                    "Shock top " + pose.CornerId.ToUpperInvariant(),
                    pose.ShockTopCarLocalPosition,
                    pose.ShockTopCarLocalRotation);
                Transform shockBottom = CreateRearRigTarget(
                    arm.transform,
                    "Shock bottom " + pose.CornerId.ToUpperInvariant(),
                    pose.ShockBottomArmLocalPosition,
                    pose.ShockBottomArmLocalRotation);
                bindings.Add(SatsumaRearSuspensionCornerBinding.Create(
                    pose.CornerId,
                    springTop,
                    springBottom,
                    springBottomRender,
                    shockTop,
                    shockBottom));
            }

            SatsumaRearSuspensionController controller = vehicleRoot
                .gameObject.AddComponent<SatsumaRearSuspensionController>();
            controller.Configure(
                assembly,
                chassisBody,
                chassisColliders?.Where(value => value != null).ToArray() ??
                    Array.Empty<Collider>(),
                bindings.ToArray());
            foreach (SatsumaRearSuspensionPartPresentation presentation in
                     partArray.Select(value => value.GetComponent<
                         SatsumaRearSuspensionPartPresentation>())
                         .Where(value => value != null && value.IsSpring))
            {
                presentation.BindSuspensionController(controller);
            }
        }

        private static void ConfigureRearSpringPresentations(
            IReadOnlyList<PartInstance> parts)
        {
            string[] definitionIds =
            {
                "vehicle.satsuma.part.coil-spring-1",
                "vehicle.satsuma.part.coil-spring-2",
                "vehicle.satsuma.part.extra-long-coil-spring-1",
                "vehicle.satsuma.part.extra-long-coil-spring-2",
            };
            foreach (string definitionId in definitionIds)
            {
                PartInstance spring = RequirePartByDefinitionId(
                    parts,
                    definitionId);
                MeshRenderer renderer = RequireMeshRenderer(
                    spring,
                    "rear_spring");
                spring.gameObject
                    .AddComponent<SatsumaRearSuspensionPartPresentation>()
                    .ConfigureSpring(renderer);
            }
        }

        private static void ConfigureRearShockPresentations(
            IReadOnlyList<PartInstance> parts)
        {
            string[] definitionIds =
            {
                "vehicle.satsuma.part.shock-absorber-1",
                "vehicle.satsuma.part.shock-absorber-2",
            };
            foreach (string definitionId in definitionIds)
            {
                PartInstance shock = RequirePartByDefinitionId(
                    parts,
                    definitionId);
                MeshRenderer top = RequireMeshRenderer(
                    shock,
                    "susp_rear_coil_top");
                MeshRenderer bottom = RequireMeshRenderer(
                    shock,
                    "susp_rear_coil_bottom");
                shock.gameObject
                    .AddComponent<SatsumaRearSuspensionPartPresentation>()
                    .ConfigureShock(top.transform, bottom.transform);
            }
        }

        private static PartInstance RequirePartByDefinitionId(
            IEnumerable<PartInstance> parts,
            string definitionId)
        {
            PartInstance[] matches = parts
                .Where(value => value != null && value.Definition != null &&
                    string.Equals(
                        value.Definition.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected one generated Satsuma part " + definitionId +
                    "; found " +
                    matches.Length.ToString(CultureInfo.InvariantCulture) +
                    ".");
            }

            return matches[0];
        }

        private static MeshRenderer RequireMeshRenderer(
            PartInstance part,
            string meshName)
        {
            MeshRenderer[] matches = part
                .GetComponentsInChildren<MeshRenderer>(true)
                .Where(value => value != null &&
                    value.GetComponent<MeshFilter>()?.sharedMesh != null &&
                    string.Equals(
                        value.GetComponent<MeshFilter>().sharedMesh.name,
                        meshName,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected one mesh '" + meshName + "' in " +
                    part.Definition.DefinitionId + "; found " +
                    matches.Length.ToString(CultureInfo.InvariantCulture) +
                    ".");
            }

            return matches[0];
        }

        private static Transform CreateRearRigTarget(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            Transform target = new GameObject(name).transform;
            target.SetParent(parent, false);
            target.SetLocalPositionAndRotation(localPosition, localRotation);
            target.localScale = Vector3.one;
            return target;
        }

        private static void BuildInstalledPartInteractionProxy(
            PartInstance part)
        {
            // Owned sockets are children of their physical part. Their helper
            // renderers must not inflate this raycast proxy into a car-sized
            // trigger; only the part's own imported presentation defines its
            // selectable surface.
            Transform presentationRoot = part.transform.Find(
                "Temporary Direct Import Presentation");
            bool hasBounds = false;
            Vector3 minimum = Vector3.zero;
            Vector3 maximum = Vector3.zero;
            MeshFilter[] meshFilters = presentationRoot != null
                ? presentationRoot.GetComponentsInChildren<MeshFilter>(true)
                : part.GetComponentsInChildren<MeshFilter>(true)
                    .Where(filter => filter != null &&
                        filter.GetComponentInParent<MountPointAuthoring>() ==
                        null)
                    .ToArray();
            for (int meshIndex = 0;
                 meshIndex < meshFilters.Length;
                 meshIndex++)
            {
                MeshFilter filter = meshFilters[meshIndex];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                // Renderer.bounds is a world-axis-aligned box. Converting its
                // corners back into a rotated part inflated the old trailing
                // arm proxy to roughly a whole wheel well. Transform the mesh's
                // own local bounds instead so the ray target hugs the part.
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 meshLocal = center + new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z);
                    Vector3 local = part.transform.InverseTransformPoint(
                        filter.transform.TransformPoint(meshLocal));
                    if (!hasBounds)
                    {
                        minimum = local;
                        maximum = local;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum = Vector3.Min(minimum, local);
                        maximum = Vector3.Max(maximum, local);
                    }
                }
            }

            Vector3 localCenter = hasBounds
                ? (minimum + maximum) * 0.5f
                : Vector3.zero;
            Vector3 localSize = hasBounds
                ? maximum - minimum + Vector3.one * 0.035f
                : Vector3.one * 0.2f;
            var proxyObject = new GameObject(
                "Installed interaction proxy");
            proxyObject.transform.SetParent(part.transform, false);
            AssemblyInstalledPartInteractionProxy proxy = proxyObject
                .AddComponent<AssemblyInstalledPartInteractionProxy>();
            proxy.Configure(part, localCenter, localSize);
        }

        private static IEnumerable<PartInstance> BuildLoosePartInstances(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            Transform vehicleRoot,
            Transform loosePartsRoot,
            IReadOnlyList<LoosePartBuild> builds,
            IReadOnlyDictionary<string, string> importedMeshes)
        {
            Vector3 donorSatsumaPosition = scene.GetWorldPosition(
                satsumaRoot.TransformId);
            Quaternion donorSatsumaRotation = scene.GetWorldRotation(
                satsumaRoot.TransformId);
            Quaternion donorToProjectRotation = ProjectSpawnRotation *
                                                Quaternion.Inverse(
                                                    donorSatsumaRotation);
            for (int index = 0; index < builds.Count; index++)
            {
                LoosePartBuild build = builds[index];
                LoosePartSource source = build.Source;
                var owner = new GameObject(
                    source.DonorName + " [" + source.Root.TransformId + "]");
                owner.transform.SetParent(loosePartsRoot, false);
                Vector3 donorWorldPosition = scene.GetWorldPosition(
                    source.Root.TransformId);
                Quaternion donorWorldRotation = scene.GetWorldRotation(
                    source.Root.TransformId);
                owner.transform.localPosition = Quaternion.Inverse(
                        ProjectSpawnRotation) *
                    (ProjectSpawnPosition + donorToProjectRotation *
                        (donorWorldPosition - donorSatsumaPosition) -
                     ProjectSpawnPosition);
                owner.transform.localRotation = Quaternion.Inverse(
                    ProjectSpawnRotation) * donorToProjectRotation *
                    donorWorldRotation;
                owner.transform.localScale = Vector3.one;

                // Temporary Phase 1 assembly-test placement. Keep both wheel
                // families beside the Satsuma while wheel/tire assembly is
                // exercised: stock wheels start with tyres, GT wheels remain
                // bare and therefore contact the world on their metal rims.
                if (TryGetTemporaryWheelTestPose(
                        build.Definition.DefinitionId,
                        out Vector3 wheelPosition,
                        out Quaternion wheelRotation))
                {
                    owner.transform.localPosition = wheelPosition;
                    owner.transform.localRotation = wheelRotation;
                }

                StableEntityIdAuthoring identity =
                    owner.AddComponent<StableEntityIdAuthoring>();
                SetStableId(
                    identity,
                    DeriveGeneratedGuid(
                        "satsuma-loose-part-instance:" +
                        source.Root.TransformId.ToString(
                            CultureInfo.InvariantCulture)));

                Rigidbody body = owner.AddComponent<Rigidbody>();
                ConfigureLoosePartRigidbody(body, source);

                GameObject presentation = (GameObject)PrefabUtility
                    .InstantiatePrefab(build.PresentationPrefab);
                presentation.name = "Temporary Direct Import Presentation";
                presentation.transform.SetParent(owner.transform, false);
                SanitizeStockBodyPanelPresentation(
                    build.Definition.DefinitionId,
                    presentation);
                BuildLoosePartColliders(
                    scene,
                    source,
                    owner.transform,
                    importedMeshes);
                ConfigureWheelTireState(
                    build.Definition.DefinitionId,
                    owner.transform,
                    presentation,
                    importedMeshes);

                PhysicsPickupTarget pickup =
                    owner.AddComponent<PhysicsPickupTarget>();
                float carryLimit = Mathf.Max(
                    45f,
                    build.Definition.MassKilograms + 0.01f);
                pickup.Configure(
                    body,
                    identity,
                    "Поднять " + build.Definition.DisplayName,
                    carryLimit,
                    useGravityWhenLoose: true);
                InteractionTargetHost host =
                    owner.AddComponent<InteractionTargetHost>();
                host.Configure(pickup);
                host.ConfigureOutlineRenderers(
                    presentation.GetComponentsInChildren<Renderer>(true));

                bool isHingedPart = IsHingedPartDefinitionId(
                    build.Definition.DefinitionId);
                if (isHingedPart)
                {
                    AssemblyInstalledPhysicsLink installedLink = owner
                        .AddComponent<AssemblyInstalledPhysicsLink>();
                    installedLink.Configure(
                        body,
                        AssemblyInstalledPhysicsLinkMode.OperablePanelHinge);
                }

                PartInstance part = owner.AddComponent<PartInstance>();
                part.Configure(
                    build.Definition,
                    identity,
                    body,
                    pickup,
                    isRoot: false,
                    mountedAtStart: string.Empty);
                host.AddCapability(part);

                if (isHingedPart)
                {
                    AssemblyHingedPartInteractionTarget hinged = owner
                        .AddComponent<AssemblyHingedPartInteractionTarget>();
                    hinged.Configure(
                        part,
                        body,
                        authoredAngularSpeedDegrees: 150f,
                        authoredOpenPrompt: "Открыть " +
                            build.Definition.DisplayName,
                        authoredClosePrompt: "Закрыть " +
                            build.Definition.DisplayName);
                    host.AddCapability(hinged);
                }

                owner.SetActive(IsLoosePartActiveAtNewGame(source));
                yield return part;
            }
        }

        private static void ConfigureWheelTireState(
            string definitionId,
            Transform owner,
            GameObject presentation,
            IReadOnlyDictionary<string, string> importedMeshes)
        {
            if (!IsRoadWheelDefinitionId(definitionId))
            {
                return;
            }

            Transform tireTransform = presentation.transform.Find(
                ProjectWheelTirePresentationName);
            MeshRenderer tireRenderer = tireTransform != null
                ? tireTransform.GetComponent<MeshRenderer>()
                : null;
            if (tireRenderer == null)
            {
                throw new InvalidDataException(
                    "Generated road wheel has no tire presentation: " +
                    definitionId);
            }

            Transform collisionRoot = owner.Find("Sanitized Donor Colliders");
            if (collisionRoot == null)
            {
                throw new InvalidDataException(
                    "Generated road wheel has no collision root: " +
                    definitionId);
            }

            var colliderObject = new GameObject(
                ProjectWheelTireColliderName);
            colliderObject.transform.SetParent(collisionRoot, false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            MeshCollider tireCollider = colliderObject
                .AddComponent<MeshCollider>();
            tireCollider.sharedMesh = RequireAsset<Mesh>(
                importedMeshes[DonorStockTireColliderMeshSourceGuid]);
            tireCollider.convex = true;

            AssemblyWheelTireState state = owner.gameObject
                .AddComponent<AssemblyWheelTireState>();
            state.Configure(
                IsStockWheelDefinitionId(definitionId),
                DonorWheelRimRadiusMeters,
                DonorWheelRimWidthMeters,
                DonorStockTireRadiusMeters,
                DonorStockTireWidthMeters,
                new Renderer[] { tireRenderer },
                new Collider[] { tireCollider });
        }

        private static bool IsRoadWheelDefinitionId(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) &&
            (definitionId.StartsWith(
                 "vehicle.satsuma.part.wheel-stock-",
                 StringComparison.Ordinal) ||
              definitionId.StartsWith(
                 "vehicle.satsuma.part.wheel-gt-",
                 StringComparison.Ordinal));

        private static bool IsDonorLoosePartFastenerRenderer(
            string definitionId,
            string donorGameObjectName)
        {
            if (string.IsNullOrWhiteSpace(definitionId) ||
                string.IsNullOrWhiteSpace(donorGameObjectName))
            {
                return false;
            }

            string normalized = donorGameObjectName.Trim();
            bool numberedBolt = normalized.Length >= 5 &&
                normalized.StartsWith(
                    "bolt",
                    StringComparison.OrdinalIgnoreCase) &&
                char.IsDigit(normalized[4]);
            if (!numberedBolt)
            {
                return false;
            }

            if (IsRoadWheelDefinitionId(definitionId))
            {
                return normalized[4] >= '0' && normalized[4] <= '3' &&
                       (normalized.Length == 5 ||
                        !char.IsDigit(normalized[5]));
            }

            if (IsSteeringWheelDefinitionId(definitionId))
            {
                // Both donor steering wheels carry an inactive Bolts/BoltPM/
                // bolt0 presentation. The live mount owns that single 10 mm
                // nut after installation; importing bolt0 here duplicates it.
                return string.Equals(
                    normalized,
                    "bolt0",
                    StringComparison.OrdinalIgnoreCase);
            }

            return IsStockBodyFastenedPanelDefinitionId(definitionId);
        }

        private static bool IsSteeringWheelDefinitionId(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) &&
            definitionId.EndsWith(
                "steering-wheel",
                StringComparison.Ordinal);

        private static bool IsEmbeddedStockFenderMudflapRenderer(
            string definitionId,
            long donorGameObjectId) =>
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.fender-left",
                StringComparison.Ordinal) && donorGameObjectId == 5511L ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.fender-right",
                StringComparison.Ordinal) && donorGameObjectId == 12503L;

        private static bool IsStockBodyFastenedPanelDefinitionId(
            string definitionId) =>
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.door-left",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.door-right",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.bootlid",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.hood",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.fender-left",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.fender-right",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.bumper-front",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.bumper-rear",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "vehicle.satsuma.part.grille",
                StringComparison.Ordinal);

        private static bool IsStockWheelDefinitionId(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) &&
            definitionId.StartsWith(
                "vehicle.satsuma.part.wheel-stock-",
                StringComparison.Ordinal);

        private static bool TryGetTemporaryWheelTestPose(
            string definitionId,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            // A wheel is about 0.15 m wide. The 0.19 m spacing avoids an
            // initial interpenetration while still reading as one tidy stack.
            const float stackZ = -0.35f;
            const float bottomCenterY = -0.4275f;
            const float stackStepY = 0.19f;

            int stackIndex = definitionId switch
            {
                "vehicle.satsuma.part.wheel-stock-fl" => 0,
                "vehicle.satsuma.part.wheel-stock-fr" => 1,
                "vehicle.satsuma.part.wheel-stock-rl" => 2,
                "vehicle.satsuma.part.wheel-stock-rr" => 3,
                "vehicle.satsuma.part.wheel-gt-fl" => 0,
                "vehicle.satsuma.part.wheel-gt-fr" => 1,
                "vehicle.satsuma.part.wheel-gt-rl" => 2,
                "vehicle.satsuma.part.wheel-gt-rr" => 3,
                _ => -1,
            };

            if (stackIndex < 0)
            {
                localPosition = default;
                localRotation = default;
                return false;
            }

            float stackX = IsStockWheelDefinitionId(definitionId)
                ? -1.05f
                : -1.68f;
            localPosition = new Vector3(
                stackX,
                bottomCenterY + stackIndex * stackStepY,
                stackZ);
            localRotation = Quaternion.Euler(0f, 0f, 90f);
            return true;
        }

        private static bool IsLoosePartActiveAtNewGame(LoosePartSource source) =>
            source != null &&
            (source.ActiveAtStart ||
             IsRoadWheelDefinitionId(source.DefinitionId) ||
             string.Equals(
                source.GroupName,
                "PartsCar",
                StringComparison.Ordinal));

        private static void SanitizeStockBodyPanelPresentation(
            string partDefinitionId,
            GameObject presentation)
        {
            if (presentation == null ||
                partDefinitionId != "vehicle.satsuma.part.door-left" &&
                partDefinitionId != "vehicle.satsuma.part.door-right" &&
                partDefinitionId != "vehicle.satsuma.part.hood" &&
                partDefinitionId != "vehicle.satsuma.part.bootlid")
            {
                return;
            }

            Transform[] descendants = presentation
                .GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                Transform candidate = descendants[index];
                if (candidate == null || candidate == presentation.transform)
                {
                    continue;
                }

                string name = candidate.name.ToLowerInvariant();
                bool isDonorBootlidHandleGarnish =
                    partDefinitionId == "vehicle.satsuma.part.bootlid" &&
                    name.Contains("bootlid_emblem");
                if (!isDonorBootlidHandleGarnish &&
                    (name.Contains("rally_sticker") ||
                    name.Contains("regplate") ||
                    name.Contains("numberplate") ||
                    name.Contains("licenseplate") ||
                    name.Contains("emblem") ||
                    name.Contains("badge") ||
                    name.StartsWith("turbo_", StringComparison.Ordinal)))
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private static void ConfigureLoosePartRigidbody(
            Rigidbody body,
            LoosePartSource source)
        {
            DonorRigidbodyRecord donor = source.Rigidbody;
            body.mass = donor != null
                ? Mathf.Max(0.01f, donor.MassKilograms)
                : InferFallbackMass(source.DonorName);
            body.linearDamping = donor != null
                ? Mathf.Max(0f, donor.LinearDamping)
                : 0.02f;
            body.angularDamping = donor != null
                ? Mathf.Max(0f, donor.AngularDamping)
                : 0.05f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        private static void BuildLoosePartColliders(
            DonorUnitySceneModel scene,
            LoosePartSource source,
            Transform owner,
            IReadOnlyDictionary<string, string> importedMeshes)
        {
            Transform collisionRoot = new GameObject(
                "Sanitized Donor Colliders").transform;
            collisionRoot.SetParent(owner, false);
            int created = 0;
            for (int index = 0; index < source.Colliders.Count; index++)
            {
                DonorColliderRecord donor = source.Colliders[index];
                if (donor.Kind == DonorColliderKind.Mesh &&
                    (string.IsNullOrWhiteSpace(donor.MeshGuid) ||
                     !importedMeshes.ContainsKey(donor.MeshGuid)))
                {
                    continue;
                }

                long transformId = scene.GetTransformIdForGameObject(
                    donor.GameObjectId);
                GetLoosePartRelativeTransform(
                    scene,
                    transformId,
                    source.Root.TransformId,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                var colliderOwner = new GameObject(
                    "Collider_" + donor.ComponentId.ToString(
                        CultureInfo.InvariantCulture));
                colliderOwner.transform.SetParent(collisionRoot, false);
                colliderOwner.transform.SetLocalPositionAndRotation(
                    localPosition,
                    localRotation);
                colliderOwner.transform.localScale = localScale;

                switch (donor.Kind)
                {
                    case DonorColliderKind.Mesh:
                        var meshCollider = colliderOwner
                            .AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = RequireAsset<Mesh>(
                            importedMeshes[donor.MeshGuid]);
                        meshCollider.convex = true;
                        break;
                    case DonorColliderKind.Box:
                        var box = colliderOwner.AddComponent<BoxCollider>();
                        box.center = donor.Center;
                        box.size = donor.Size;
                        break;
                    case DonorColliderKind.Sphere:
                        var sphere = colliderOwner.AddComponent<SphereCollider>();
                        sphere.center = donor.Center;
                        sphere.radius = Mathf.Max(0.001f, donor.Radius);
                        break;
                    case DonorColliderKind.Capsule:
                        var capsule = colliderOwner
                            .AddComponent<CapsuleCollider>();
                        capsule.center = donor.Center;
                        capsule.radius = Mathf.Max(0.001f, donor.Radius);
                        capsule.height = Mathf.Max(
                            capsule.radius * 2f,
                            donor.Height);
                        capsule.direction = Mathf.Clamp(donor.Direction, 0, 2);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                created++;
            }

            if (created > 0)
            {
                return;
            }

            Bounds bounds = CalculateLocalPresentationBounds(owner);
            var fallback = collisionRoot.gameObject.AddComponent<BoxCollider>();
            fallback.center = bounds.center;
            fallback.size = new Vector3(
                Mathf.Max(0.02f, bounds.size.x),
                Mathf.Max(0.02f, bounds.size.y),
                Mathf.Max(0.02f, bounds.size.z));
        }

        private static Bounds CalculateLocalPresentationBounds(Transform owner)
        {
            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            }

            Bounds world = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                world.Encapsulate(renderers[index].bounds);
            }

            Vector3 center = owner.InverseTransformPoint(world.center);
            Vector3 scale = owner.lossyScale;
            return new Bounds(
                center,
                new Vector3(
                    Mathf.Abs(scale.x) > 0.0001f
                        ? world.size.x / Mathf.Abs(scale.x)
                        : world.size.x,
                    Mathf.Abs(scale.y) > 0.0001f
                        ? world.size.y / Mathf.Abs(scale.y)
                        : world.size.y,
                    Mathf.Abs(scale.z) > 0.0001f
                        ? world.size.z / Mathf.Abs(scale.z)
                        : world.size.z));
        }

        private static VehicleDeliveredPartCompatibilityCatalog
            BuildCompatibilityCatalog()
        {
            VehicleDeliveredPartCompatibilityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<VehicleDeliveredPartCompatibilityCatalog>(
                    CompatibilityCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    VehicleDeliveredPartCompatibilityCatalog>();
                AssetDatabase.CreateAsset(catalog, CompatibilityCatalogPath);
            }

            catalog.ConfigureForAuthoring(
                SatsumaMailOrderCompatibilityDefaults.CatalogId,
                SatsumaMailOrderCompatibilityDefaults.CreateRecords());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ValidateBuiltContent(
            GameObject runtimePrefab,
            GameObject presentationPrefab,
            VehicleDeliveredPartCompatibilityCatalog compatibility,
            int expectedRendererCount,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            IReadOnlyList<MountBuild> mountBuilds)
        {
            int rendererCount = presentationPrefab != null
                ? presentationPrefab.GetComponentsInChildren<MeshRenderer>(true).Length
                : 0;
            int colliderCount = runtimePrefab != null
                ? runtimePrefab.GetComponentsInChildren<MeshCollider>(true).Length
                : 0;
            int partCount = runtimePrefab != null
                ? runtimePrefab.GetComponentsInChildren<PartInstance>(true).Length
                : 0;
            int pickupCount = runtimePrefab != null
                ? runtimePrefab.GetComponentsInChildren<PhysicsPickupTarget>(true).Length
                : 0;
            int mountCount = runtimePrefab != null
                ? runtimePrefab.GetComponentsInChildren<MountPointAuthoring>(true).Length
                : 0;
            string[] missing = runtimePrefab == null
                ? new[] { "runtimePrefab" }
                : new[]
                {
                    runtimePrefab.GetComponent<StableEntityIdAuthoring>() == null
                        ? nameof(StableEntityIdAuthoring)
                        : string.Empty,
                    runtimePrefab.GetComponent<Rigidbody>() == null
                        ? nameof(Rigidbody)
                        : string.Empty,
                    runtimePrefab.GetComponent<VehicleAssemblyController>() == null
                        ? nameof(VehicleAssemblyController)
                        : string.Empty,
                    runtimePrefab.GetComponent<VehicleSimulationHost>() == null
                        ? nameof(VehicleSimulationHost)
                        : string.Empty,
                    runtimePrefab.GetComponent<VehiclePersistenceBinding>() == null
                        ? nameof(VehiclePersistenceBinding)
                        : string.Empty,
                    runtimePrefab.GetComponent<AssemblyChassisMassController>() == null
                        ? nameof(AssemblyChassisMassController)
                        : string.Empty,
                    runtimePrefab.GetComponent<
                        SatsumaNwhPhysicsRestoreSynchronizer>() == null
                        ? nameof(SatsumaNwhPhysicsRestoreSynchronizer)
                        : string.Empty,
                    runtimePrefab.GetComponent<LegacySatsumaBaselineMetadata>() == null
                        ? nameof(LegacySatsumaBaselineMetadata)
                        : string.Empty,
                }.Where(value => !string.IsNullOrEmpty(value)).ToArray();
            if (missing.Length > 0 ||
                rendererCount != expectedRendererCount ||
                colliderCount < ChassisColliders.Length ||
                partCount != loosePartBuilds.Count + 1 ||
                pickupCount != loosePartBuilds.Count ||
                mountCount != mountBuilds.Count)
            {
                throw new InvalidDataException(
                    "Built Satsuma runtime prefab is structurally incomplete: " +
                    $"missing=[{string.Join(",", missing)}], " +
                    $"renderers={rendererCount}/{expectedRendererCount}, " +
                    $"meshColliders={colliderCount}>={ChassisColliders.Length}, " +
                    $"parts={partCount}/{loosePartBuilds.Count + 1}, " +
                    $"pickups={pickupCount}/{loosePartBuilds.Count}, " +
                    $"mounts={mountCount}/{mountBuilds.Count}.");
            }

            Transform chassisCollisionRoot = runtimePrefab.transform.Find(
                "Sanitized Donor Chassis Colliders");
            Transform playerCollisionProxy = chassisCollisionRoot != null
                ? chassisCollisionRoot.Find("Project Player Collision Proxy")
                : null;
            Rigidbody playerCollisionProxyBody = playerCollisionProxy != null
                ? playerCollisionProxy.GetComponent<Rigidbody>()
                : null;
            Collider[] authoredChassisColliders = chassisCollisionRoot != null
                ? chassisCollisionRoot.GetComponentsInChildren<Collider>(true)
                : Array.Empty<Collider>();
            Collider[] playerOnlyColliders = authoredChassisColliders
                .Where(collider => collider.name.StartsWith(
                    "PlayerColl_",
                    StringComparison.Ordinal))
                .ToArray();
            Collider[] worldFacingChassisColliders = authoredChassisColliders
                .Where(collider => !collider.name.StartsWith(
                    "PlayerColl_",
                    StringComparison.Ordinal))
                .ToArray();
            int playerLayerMask = 1 << ProjectPlayerCollisionLayer;
            Rigidbody runtimeChassisBody = runtimePrefab
                .GetComponent<Rigidbody>();
            Rigidbody[] nestedCollisionBodies = chassisCollisionRoot != null
                ? chassisCollisionRoot.GetComponentsInChildren<Rigidbody>(true)
                : Array.Empty<Rigidbody>();
            int playerWrongHierarchyCount = playerOnlyColliders.Count(
                collider => playerCollisionProxy == null ||
                    !collider.transform.IsChildOf(playerCollisionProxy));
            int playerWrongMaskCount = playerOnlyColliders.Count(collider =>
                collider.excludeLayers.value != ~playerLayerMask);
            int playerWrongPriorityCount = playerOnlyColliders.Count(collider =>
                collider.layerOverridePriority < 1);
            int worldWrongHierarchyCount = worldFacingChassisColliders.Count(
                collider => playerCollisionProxy != null &&
                    collider.transform.IsChildOf(playerCollisionProxy));
            int worldMissingPlayerExclusionCount =
                worldFacingChassisColliders.Count(collider =>
                    (collider.excludeLayers.value & playerLayerMask) == 0);
            int worldWrongPriorityCount = worldFacingChassisColliders.Count(
                collider => collider.layerOverridePriority < 1);
            bool invalidPlayerCollisionIsolation =
                authoredChassisColliders.Length != 29 ||
                playerCollisionProxyBody == null ||
                !playerCollisionProxyBody.isKinematic ||
                playerCollisionProxyBody.useGravity ||
                runtimeChassisBody == null ||
                nestedCollisionBodies.Length != 1 ||
                nestedCollisionBodies[0] != playerCollisionProxyBody ||
                playerOnlyColliders.Length != 4 ||
                playerWrongHierarchyCount != 0 ||
                playerWrongMaskCount != 0 ||
                playerWrongPriorityCount != 0 ||
                worldFacingChassisColliders.Length != 25 ||
                worldWrongHierarchyCount != 0 ||
                worldMissingPlayerExclusionCount != 0 ||
                worldWrongPriorityCount != 0;
            if (invalidPlayerCollisionIsolation)
            {
                throw new InvalidDataException(
                    "Built Satsuma player collision isolation is invalid: " +
                    $"all={authoredChassisColliders.Length}/29, " +
                    $"playerOnly={playerOnlyColliders.Length}/4, " +
                    $"worldFacing={worldFacingChassisColliders.Length}/25, " +
                    $"proxyBody={(playerCollisionProxyBody != null ? "present" : "missing")}, " +
                    $"proxyKinematic={playerCollisionProxyBody?.isKinematic}, " +
                    $"proxyGravity={playerCollisionProxyBody?.useGravity}, " +
                    $"nestedBodies={nestedCollisionBodies.Length}/1, " +
                    $"playerWrongHierarchy={playerWrongHierarchyCount}, " +
                    $"playerWrongMask={playerWrongMaskCount}, " +
                    $"playerWrongPriority={playerWrongPriorityCount}, " +
                    $"worldWrongHierarchy={worldWrongHierarchyCount}, " +
                    $"worldMissingPlayerExclusion={worldMissingPlayerExclusionCount}, " +
                    $"worldWrongPriority={worldWrongPriorityCount}.");
            }


            VehicleItemAssemblyBridge itemBridge = runtimePrefab.GetComponent<VehicleItemAssemblyBridge>();
            PartDefinition[] dynamicDefinitions = Array.Empty<PartDefinition>();
            if (itemBridge != null)
            {
                if (itemBridge.Assembly != runtimePrefab.GetComponent<VehicleAssemblyController>() || itemBridge.Catalog == null)
                    throw new InvalidDataException("The item assembly bridge must reference this Satsuma and its reviewed catalog.");
                dynamicDefinitions = itemBridge.Catalog.GetPartDefinitions();
            }
            IReadOnlyList<VehicleAssemblyValidationIssue> assemblyIssues =
                VehicleAssemblyValidator.Validate(
                    runtimePrefab.GetComponent<VehicleAssemblyController>().Parts,
                    runtimePrefab.GetComponent<VehicleAssemblyController>().MountPoints,
                    runtimePrefab.GetComponent<VehicleAssemblyController>().Dependencies,
                    runtimePrefab.GetComponent<VehicleAssemblyController>().Tools,
                    dynamicDefinitions);
            VehicleAssemblyValidationIssue[] errors = assemblyIssues
                .Where(value =>
                    value.Severity == VehicleAssemblyValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidDataException(
                    "Built Satsuma loose-part roster is invalid: " +
                    string.Join(
                        " | ",
                        errors.Select(value => value.Code + ":" + value.Message)));
            }

            NwhAssemblyWheelSupportController wheelSupport = runtimePrefab
                .GetComponent<NwhAssemblyWheelSupportController>();
            NwhWheelPhysicsBackend wheelBackend = runtimePrefab
                .GetComponent<NwhWheelPhysicsBackend>();
            bool invalidRearSupport = wheelSupport == null ||
                wheelSupport.Bindings.Length != WheelIds.Length ||
                !wheelSupport.Bindings[0].Enabled ||
                !wheelSupport.Bindings[1].Enabled ||
                !IsValidRearNwhBinding(wheelSupport.Bindings[2], 2, "rl") ||
                !IsValidRearNwhBinding(wheelSupport.Bindings[3], 3, "rr") ||
                wheelBackend == null ||
                !Mathf.Approximately(
                    wheelBackend.RearAntiRollForceNewtons,
                    0f);
            AssemblyInstalledPhysicsLink[] rearLinks = runtimePrefab
                .GetComponentsInChildren<AssemblyInstalledPhysicsLink>(true);
            AssemblyInstalledPhysicsLink[] armLinks = rearLinks
                .Where(link => link != null && link.GetComponent<PartInstance>()
                    ?.Definition?.DefinitionId.Contains(
                        "trail-arm-",
                        StringComparison.Ordinal) == true)
                .ToArray();
            AssemblyInstalledPhysicsLink[] drumLinks = rearLinks
                .Where(link => link != null && link.GetComponent<PartInstance>()
                    ?.Definition?.DefinitionId.Contains(
                        "drum-brake-",
                        StringComparison.Ordinal) == true)
                .ToArray();
            AssemblyInstalledPhysicsLink[] roadWheelLinks = rearLinks
                .Where(link => link != null && IsRoadWheelDefinitionId(
                    link.GetComponent<PartInstance>()
                        ?.Definition?.DefinitionId))
                .ToArray();
            SatsumaRearSuspensionController rearController = runtimePrefab
                .GetComponent<SatsumaRearSuspensionController>();
            SatsumaRearNwhSuspensionController rearNwhController = runtimePrefab
                .GetComponent<SatsumaRearNwhSuspensionController>();
            SatsumaNwhPhysicsRestoreSynchronizer restoreSynchronizer =
                runtimePrefab.GetComponent<
                    SatsumaNwhPhysicsRestoreSynchronizer>();
            AssemblyChassisMassController massController = runtimePrefab
                .GetComponent<AssemblyChassisMassController>();
            VehiclePersistenceBinding persistence = runtimePrefab
                .GetComponent<VehiclePersistenceBinding>();
            VehicleAssemblyController runtimeAssembly = runtimePrefab
                .GetComponent<VehicleAssemblyController>();
            Rigidbody runtimeChassis = runtimePrefab.GetComponent<Rigidbody>();
            bool invalidRestoreSynchronization =
                restoreSynchronizer == null || massController == null ||
                persistence == null ||
                restoreSynchronizer.AssemblyController != runtimeAssembly ||
                restoreSynchronizer.Chassis != runtimeChassis ||
                restoreSynchronizer.WheelSupport != wheelSupport ||
                restoreSynchronizer.FrontSteering != runtimePrefab
                    .GetComponent<SatsumaFrontSteeringController>() ||
                restoreSynchronizer.FrontSuspension != runtimePrefab
                    .GetComponent<SatsumaFrontSuspensionController>() ||
                restoreSynchronizer.RearSuspension != rearNwhController ||
                persistence.ChassisMassController != massController ||
                persistence.PhysicsRestoreSynchronizerComponent !=
                    restoreSynchronizer;
            SatsumaRearSuspensionPartPresentation[] rearPresentations =
                runtimePrefab.GetComponentsInChildren<
                    SatsumaRearSuspensionPartPresentation>(true);
            bool invalidPhysicalRig =
                armLinks.Length != 2 || armLinks.Any(link =>
                    link.LinkMode !=
                        AssemblyInstalledPhysicsLinkMode.TrailingArmHinge) ||
                drumLinks.Length != 2 || drumLinks.Any(link =>
                    link.LinkMode != AssemblyInstalledPhysicsLinkMode.Fixed ||
                    !Mathf.Approximately(
                        link.WeldedProjectionDistance,
                        0.001f) ||
                    !Mathf.Approximately(
                        link.WeldedProjectionAngle,
                        0.5f) ||
                    link.InstalledSolverIterations < 20 ||
                    link.InstalledSolverVelocityIterations < 8) ||
                roadWheelLinks.Length != 8 || roadWheelLinks.Any(link =>
                    link.LinkMode !=
                        AssemblyInstalledPhysicsLinkMode.RoadWheelAxle ||
                    link.ConnectedBodyOverride != null) ||
                rearController == null || rearController.Corners.Count != 2 ||
                !rearController.ExternalWheelAuthority ||
                rearNwhController == null ||
                rearNwhController.Corners.Length != 2 ||
                rearNwhController.Corners.Any(corner =>
                    corner.DrumMount == null ||
                    corner.RoadWheelMount == null) ||
                !Mathf.Approximately(
                    rearController.StockWheelSpringRate,
                    21200f) ||
                !Mathf.Approximately(
                    rearController.WheelShockDamper,
                    1000f) ||
                rearPresentations.Count(value => value.IsSpring) != 4 ||
                rearPresentations.Count(value => value.IsShock) != 2;
            if (invalidRearSupport || invalidPhysicalRig ||
                invalidRestoreSynchronization)
            {
                throw new InvalidDataException(
                    "Built Satsuma rear suspension is incomplete: all donor " +
                    "Wheel contacts must be enabled, rear compression must own " +
                    "the kinematic arm rig, all interchangeable rear parts " +
                    "must retain their authored assembly metadata, and save " +
                    "restore must synchronize physics before release.");
            }

            string prefabYaml = File.ReadAllText(
                ToFileSystemPath(ActiveRuntimePrefabPath));
            string persistenceGuid = AssetDatabase.AssetPathToGUID(
                "Assets/Game/Vehicle/Runtime/VehiclePersistenceBinding.cs");
            if (string.IsNullOrWhiteSpace(persistenceGuid) ||
                !prefabYaml.Contains(
                    "guid: " + persistenceGuid,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Built Satsuma prefab does not serialize the vehicle persistence script reference.");
            }

            LegacySatsumaBaselineMetadata metadata =
                runtimePrefab.GetComponent<LegacySatsumaBaselineMetadata>();
            if (!metadata.TryValidate(out string metadataFailure))
            {
                throw new InvalidDataException(metadataFailure);
            }

            IReadOnlyList<string> catalogFailures =
                compatibility.ValidateConfiguration();
            if (catalogFailures.Count > 0)
            {
                throw new InvalidDataException(
                    "Satsuma mail-order compatibility is invalid: " +
                    string.Join(" | ", catalogFailures));
            }
        }

        private static bool IsValidRearNwhBinding(
            NwhAssemblyWheelSupportBinding binding,
            int wheelIndex,
            string corner)
        {
            if (!binding.Enabled || binding.Wheel == null ||
                binding.RequiredOccupiedMountIds.Length != 2 ||
                !binding.RequiredOccupiedMountIds.Contains(
                    "mount.satsuma.trail-arm-" + corner) ||
                !binding.RequiredOccupiedMountIds.Contains(
                    "mount.satsuma.drum-brake-" + corner) ||
                binding.RequiredAnyOccupiedMountIds.Length != 0)
            {
                return false;
            }

            NwhAssemblyWheelStageProfile wheelProfile = binding.StageProfile;
            NwhAssemblySuspensionStageProfile suspension =
                binding.SuspensionProfile;
            return wheelProfile.Enabled &&
                wheelProfile.RoadWheelMountId ==
                    "mount.satsuma.wheel" + corner + "-new" &&
                wheelProfile.DamperMountId ==
                    "mount.satsuma.shock-" + corner &&
                Mathf.Approximately(
                    wheelProfile.AssemblyContactRadiusMeters,
                    DonorRearDrumContactRadiusMeters) &&
                Mathf.Approximately(
                    wheelProfile.AssemblyContactWidthMeters,
                    DonorRearDrumContactWidthMeters) &&
                Mathf.Approximately(
                    wheelProfile.SpringOnlyDamperRate,
                    SatsumaRearSuspensionForce.NoShockDamper) &&
                Mathf.Approximately(
                    wheelProfile.DampedBumpRate,
                    SatsumaRearSuspensionForce.StockShockDamper) &&
                Mathf.Approximately(
                    wheelProfile.DampedReboundRate,
                    SatsumaRearSuspensionForce.StockShockDamper) &&
                suspension.Enabled &&
                suspension.SpringMountId ==
                    "mount.satsuma.coilspring-" + corner &&
                suspension.AlternateSpringMountId ==
                    "mount.satsuma.long-coilspring-" + corner &&
                IsValidRearNwhStage(
                    suspension.Unstrung,
                    wheelIndex,
                    SatsumaRearSuspensionTravel.NoSpringWheelRootY,
                    SatsumaRearSuspensionTravel.StockSuspensionTravel,
                    SatsumaRearSuspensionForce.NoSpringWheelRate) &&
                IsValidRearNwhStage(
                    suspension.Sprung,
                    wheelIndex,
                    SatsumaRearSuspensionTravel.StockWheelRootY,
                    SatsumaRearSuspensionTravel.StockSuspensionTravel,
                    SatsumaRearSuspensionForce.StockWheelRate) &&
                IsValidRearNwhStage(
                    suspension.AlternateSprung,
                    wheelIndex,
                    SatsumaRearSuspensionTravel.LongWheelRootY,
                    SatsumaRearSuspensionTravel.LongSuspensionTravel,
                    SatsumaRearSuspensionForce.LongWheelRate);
        }

        private static bool IsValidRearNwhStage(
            NwhAssemblySuspensionStage stage,
            int wheelIndex,
            float expectedTopY,
            float expectedTravel,
            float expectedSpringRate)
        {
            Vector3 expectedTop = WheelAnchors[wheelIndex];
            expectedTop.y = expectedTopY;
            AnimationCurve curve = stage.ForceCurve;
            return (stage.TopLocalPosition - expectedTop).sqrMagnitude <=
                    0.00000001f &&
                Mathf.Approximately(stage.TravelMeters, expectedTravel) &&
                Mathf.Approximately(
                    stage.MaximumForceNewtons,
                    expectedSpringRate * expectedTravel) &&
                Mathf.Approximately(
                    stage.BumpRate,
                    SatsumaRearSuspensionForce.NoShockDamper) &&
                Mathf.Approximately(
                    stage.ReboundRate,
                    SatsumaRearSuspensionForce.NoShockDamper) &&
                Mathf.Approximately(stage.SlowBump, 1f) &&
                Mathf.Approximately(
                    stage.FastBump,
                    SatsumaRearSuspensionForce.FastDamperFactor) &&
                Mathf.Approximately(stage.SlowRebound, 1f) &&
                Mathf.Approximately(
                    stage.FastRebound,
                    SatsumaRearSuspensionForce.FastDamperFactor) &&
                Mathf.Approximately(
                    stage.BumpTransitionVelocity,
                    SatsumaRearSuspensionForce.FastDamperSpeed) &&
                Mathf.Approximately(
                    stage.ReboundTransitionVelocity,
                    SatsumaRearSuspensionForce.FastDamperSpeed) &&
                curve != null && curve.length == 2 &&
                Mathf.Approximately(curve.Evaluate(0f), 0f) &&
                Mathf.Approximately(curve.Evaluate(1f), 1f);
        }

        private static bool ShouldExcludeBodyRenderer(
            DonorUnitySceneModel scene,
            DonorStaticRendererRecord renderer)
        {
            string path = scene.GetHierarchyPath(
                scene.GetTransformIdForGameObject(renderer.GameObjectId));
            return ExcludedBodyPathTokens.Any(token =>
                path.IndexOf(token, StringComparison.Ordinal) >= 0);
        }

        private static Dictionary<string, string> ImportAssets(
            IEnumerable<string> guids,
            IReadOnlyDictionary<string, string> sourceByGuid,
            string destinationRoot,
            string identityPrefix)
        {
            EnsureFolder(destinationRoot);
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string guid in guids)
            {
                string source = RequireGuidPath(sourceByGuid, guid, "asset");
                string destination = destinationRoot + "/" + guid +
                                     Path.GetExtension(source).ToLowerInvariant();
                CopyAssetAndMeta(
                    source,
                    destination,
                    guid,
                    DeriveGeneratedGuid(identityPrefix + ":" + guid));
                result.Add(guid, destination);
            }

            return result;
        }

        private static void BuildRustHdrpTextures(
            IReadOnlyDictionary<string, string> sourceByGuid)
        {
            string detailAlbedoPath = RequireGuidPath(
                sourceByGuid,
                DonorRustDetailAlbedoSourceGuid,
                "rust detail albedo");
            string detailNormalPath = RequireGuidPath(
                sourceByGuid,
                DonorRustDetailNormalSourceGuid,
                "rust detail normal");
            string detailMaskPath = RequireGuidPath(
                sourceByGuid,
                DonorRustDetailMaskSourceGuid,
                "rust detail mask");
            string specGlossPath = RequireGuidPath(
                sourceByGuid,
                DonorRustSpecGlossSourceGuid,
                "rust metallic/smoothness");

            using var detailAlbedo = new TemporaryReadableTexture(
                detailAlbedoPath,
                "rust detail albedo");
            using var detailNormal = new TemporaryReadableTexture(
                detailNormalPath,
                "rust detail normal");
            using var detailMask = new TemporaryReadableTexture(
                detailMaskPath,
                "rust detail mask");
            using var specGloss = new TemporaryReadableTexture(
                specGlossPath,
                "rust metallic/smoothness");

            Color32[] albedoPixels = detailAlbedo.Texture.GetPixels32();
            Color32[] normalPixels = detailNormal.Texture.GetPixels32();
            int detailWidth = detailAlbedo.Texture.width;
            int detailHeight = detailAlbedo.Texture.height;
            var packedDetail = new Color32[detailWidth * detailHeight];
            for (int y = 0; y < detailHeight; y++)
            {
                int normalY = y * detailNormal.Texture.height / detailHeight;
                for (int x = 0; x < detailWidth; x++)
                {
                    int normalX = x * detailNormal.Texture.width / detailWidth;
                    Color32 albedo = albedoPixels[y * detailWidth + x];
                    Color32 normal = normalPixels[
                        normalY * detailNormal.Texture.width + normalX];
                    packedDetail[y * detailWidth + x] = new Color32(
                        GetPackedDetailAlbedoLuminance(albedo),
                        normal.g,
                        128,
                        normal.r);
                }
            }

            if (detailMask.Texture.width != specGloss.Texture.width ||
                detailMask.Texture.height != specGloss.Texture.height)
            {
                throw new InvalidDataException(
                    "The donor Satsuma rust mask and metallic texture dimensions differ.");
            }

            Color32[] maskPixels = detailMask.Texture.GetPixels32();
            Color32[] specPixels = specGloss.Texture.GetPixels32();
            var packedMask = new Color32[maskPixels.Length];
            for (int index = 0; index < packedMask.Length; index++)
            {
                packedMask[index] = new Color32(
                    specPixels[index].r,
                    255,
                    maskPixels[index].r,
                    specPixels[index].a);
            }

            WritePackedTexture(
                RustPackedDetailTexturePath,
                detailWidth,
                detailHeight,
                packedDetail,
                detailAlbedoPath,
                DonorRustDetailAlbedoSourceGuid,
                "satsuma-rust-hdrp-detail");
            WritePackedTexture(
                RustPackedMaskTexturePath,
                detailMask.Texture.width,
                detailMask.Texture.height,
                packedMask,
                detailMaskPath,
                DonorRustDetailMaskSourceGuid,
                "satsuma-rust-hdrp-mask");
        }

        private static void BuildRimRustHdrpTextures(
            IReadOnlyDictionary<string, string> sourceByGuid)
        {
            string aoPath = RequireGuidPath(
                sourceByGuid,
                DonorRimAoSourceGuid,
                "rim ambient-occlusion albedo");
            string rustPatchesPath = RequireGuidPath(
                sourceByGuid,
                DonorRimRustPatchesSourceGuid,
                "rim rust detail albedo");
            string normalPath = RequireGuidPath(
                sourceByGuid,
                DonorRimRustNormalSourceGuid,
                "rim rust detail normal");
            string specGlossPath = RequireGuidPath(
                sourceByGuid,
                DonorRimSpecGlossSourceGuid,
                "rim specular/smoothness");

            using var ao = new TemporaryReadableTexture(
                aoPath,
                "rim ambient-occlusion albedo");
            using var rustPatches = new TemporaryReadableTexture(
                rustPatchesPath,
                "rim rust detail albedo");
            using var normal = new TemporaryReadableTexture(
                normalPath,
                "rim rust detail normal");
            using var specGloss = new TemporaryReadableTexture(
                specGlossPath,
                "rim specular/smoothness");

            int baseWidth = ao.Texture.width;
            int baseHeight = ao.Texture.height;
            Color32[] aoPixels = ao.Texture.GetPixels32();
            Color32[] rustPixels = rustPatches.Texture.GetPixels32();
            var bakedBaseColor = new Color32[baseWidth * baseHeight];
            for (int y = 0; y < baseHeight; y++)
            {
                int rustY = y * rustPatches.Texture.height / baseHeight;
                for (int x = 0; x < baseWidth; x++)
                {
                    int rustX = x * rustPatches.Texture.width / baseWidth;
                    Color32 basePixel = aoPixels[y * baseWidth + x];
                    Color32 detailPixel = rustPixels[
                        rustY * rustPatches.Texture.width + rustX];
                    bakedBaseColor[y * baseWidth + x] = new Color32(
                        BakeLegacyDetailMulX2(
                            basePixel.r,
                            detailPixel.r),
                        BakeLegacyDetailMulX2(
                            basePixel.g,
                            detailPixel.g),
                        BakeLegacyDetailMulX2(
                            basePixel.b,
                            detailPixel.b),
                        255);
                }
            }

            Color32[] normalPixels = normal.Texture.GetPixels32();
            var packedDetail = new Color32[normalPixels.Length];
            for (int index = 0; index < packedDetail.Length; index++)
            {
                Color32 pixel = normalPixels[index];
                packedDetail[index] = new Color32(
                    128,
                    pixel.g,
                    128,
                    pixel.r);
            }

            Color32[] specPixels = specGloss.Texture.GetPixels32();
            var packedMask = new Color32[specPixels.Length];
            for (int index = 0; index < packedMask.Length; index++)
            {
                packedMask[index] = new Color32(
                    0,
                    255,
                    255,
                    specPixels[index].a);
            }

            WritePackedTexture(
                RimRustBaseColorTexturePath,
                baseWidth,
                baseHeight,
                bakedBaseColor,
                aoPath,
                DonorRimAoSourceGuid,
                "satsuma-rim-rust-hdrp-base-color",
                sRgbTexture: true);
            WritePackedTexture(
                RimRustPackedDetailTexturePath,
                normal.Texture.width,
                normal.Texture.height,
                packedDetail,
                normalPath,
                DonorRimRustNormalSourceGuid,
                "satsuma-rim-rust-hdrp-detail");
            WritePackedTexture(
                RimRustPackedMaskTexturePath,
                specGloss.Texture.width,
                specGloss.Texture.height,
                packedMask,
                specGlossPath,
                DonorRimSpecGlossSourceGuid,
                "satsuma-rim-rust-hdrp-mask");
        }

        private static void BuildGlassRainNeutralDetailTexture(
            IReadOnlyDictionary<string, string> sourceByGuid)
        {
            string sourcePath = RequireGuidPath(
                sourceByGuid,
                DonorGlassMainTextureSourceGuid,
                "window rain atlas base");
            var pixels = new Color32[16];
            for (int index = 0; index < pixels.Length; index++)
            {
                // HDRP detail packing: R albedo, G normal Y,
                // B smoothness and A normal X. 128 is neutral in every slot.
                pixels[index] = new Color32(128, 128, 128, 128);
            }

            WritePackedTexture(
                GlassRainNeutralDetailTexturePath,
                4,
                4,
                pixels,
                sourcePath,
                DonorGlassMainTextureSourceGuid,
                "satsuma-glass-rain-neutral-detail");
        }

        private static byte BakeLegacyDetailMulX2(
            byte baseChannel,
            byte detailChannel)
        {
            float baseLinear = Mathf.GammaToLinearSpace(baseChannel / 255f);
            float detailLinear = Mathf.GammaToLinearSpace(detailChannel / 255f);
            float combinedLinear = Mathf.Clamp01(
                baseLinear * detailLinear * 2f);
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt(
                    Mathf.LinearToGammaSpace(combinedLinear) * 255f),
                0,
                255);
        }

        private static void WritePackedTexture(
            string assetPath,
            int width,
            int height,
            Color32[] pixels,
            string sourcePath,
            string sourceGuid,
            string generatedIdentity,
            bool sRgbTexture = false)
        {
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(
                    updateMipmaps: false,
                    makeNoLongerReadable: false);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    throw new InvalidDataException(
                        "Could not encode generated Satsuma material texture: " +
                        assetPath);
                }

                string destination = ToFileSystemPath(assetPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(destination) ??
                    throw new InvalidOperationException(
                        "Generated rust texture path has no directory."));
                File.WriteAllBytes(destination, png);

                string meta = File.ReadAllText(sourcePath + ".meta");
                string sourceGuidLine = "guid: " + sourceGuid;
                if (!meta.Contains(sourceGuidLine))
                {
                    throw new InvalidDataException(
                        "Donor rust texture metadata contains no GUID " +
                        sourceGuid + ".");
                }

                File.WriteAllText(
                    destination + ".meta",
                    meta.Replace(
                        sourceGuidLine,
                        "guid: " + DeriveGeneratedGuid(generatedIdentity)),
                    new UTF8Encoding(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as
                TextureImporter ?? throw new InvalidOperationException(
                    "Generated Satsuma material texture has no TextureImporter: " +
                    assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRgbTexture;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }

        private static byte GetPackedDetailAlbedoLuminance(Color32 pixel)
        {
            int weighted =
                54 * pixel.r +
                183 * pixel.g +
                19 * pixel.b;
            return (byte)((weighted + 128) >> 8);
        }

        private sealed class TemporaryReadableTexture : IDisposable
        {
            public TemporaryReadableTexture(string sourcePath, string label)
            {
                Texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true);
                if (!ImageConversion.LoadImage(
                        Texture,
                        File.ReadAllBytes(sourcePath),
                        markNonReadable: false))
                {
                    UnityEngine.Object.DestroyImmediate(Texture);
                    Texture = null;
                    throw new InvalidDataException(
                        "Could not decode donor " + label + ": " + sourcePath);
                }
            }

            public Texture2D Texture { get; private set; }

            public void Dispose()
            {
                if (Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(Texture);
                    Texture = null;
                }
            }
        }

        private static Dictionary<string, string> BuildGuidIndex(
            string donorAssetsRoot)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string metaPath in Directory.EnumerateFiles(
                         donorAssetsRoot,
                         "*.meta",
                         SearchOption.AllDirectories))
            {
                using var reader = new StreamReader(metaPath);
                for (int index = 0; index < 8 && !reader.EndOfStream; index++)
                {
                    string line = reader.ReadLine();
                    if (line == null || !line.StartsWith(
                            "guid: ",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.TryAdd(
                        line.Substring(6).Trim(),
                        metaPath.Substring(0, metaPath.Length - 5));
                    break;
                }
            }

            return result;
        }

        private static SourceMaterialSpec ReadMaterialSpec(
            string sourceGuid,
            string path)
        {
            string yaml = File.ReadAllText(path);
            string textureGuid = MatchValue(
                yaml,
                @"first:\s*name:\s*_MainTex\s*second:\s*m_Texture:\s*\{fileID:\s*-?\d+(?:,\s*guid:\s*(?<value>[0-9a-fA-F]+))?");
            Color color = MatchColor(yaml, "_Color", Color.white);
            float metallic = MatchFloat(yaml, "_Metallic", 0f);
            float smoothness = MatchFloat(yaml, "_Glossiness", 0.25f);
            float destinationBlend = MatchFloat(yaml, "_DstBlend", 0f);
            float zWrite = MatchFloat(yaml, "_ZWrite", 1f);
            int queue = (int)MatchFloat(
                yaml,
                "m_CustomRenderQueue",
                -1f,
                directField: true);
            bool transparent = IsDonorGlassMaterialSourceGuid(sourceGuid) ||
                               queue >= 3000 ||
                               Mathf.Approximately(destinationBlend, 10f) ||
                               Mathf.Approximately(zWrite, 0f);
            return new SourceMaterialSpec(
                textureGuid,
                color,
                Mathf.Clamp01(metallic),
                Mathf.Clamp01(smoothness),
                transparent);
        }

        private static Material CreateMaterial(
            string sourceGuid,
            SourceMaterialSpec spec,
            IReadOnlyDictionary<string, string> importedTextures)
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            var material = new Material(shader)
            {
                name = "Phase 1 Satsuma " + sourceGuid,
                enableInstancing = true,
            };
            if (!string.IsNullOrWhiteSpace(spec.MainTextureGuid))
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                    importedTextures[spec.MainTextureGuid]);
                if (texture is Texture2D)
                {
                    material.SetTexture("_BaseColorMap", texture);
                }
            }

            material.SetColor("_BaseColor", spec.BaseColor);
            material.SetFloat("_Metallic", spec.Metallic);
            material.SetFloat("_Smoothness", spec.Smoothness);
            material.SetFloat("_SurfaceType", spec.Transparent ? 1f : 0f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", spec.Transparent ? 0f : 1f);
            if (string.Equals(
                    sourceGuid,
                    DonorRustPaintMaterialSourceGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                Texture2D detail = RequireAsset<Texture2D>(
                    RustPackedDetailTexturePath);
                Texture2D mask = RequireAsset<Texture2D>(
                    RustPackedMaskTexturePath);
                material.SetTexture("_DetailMap", detail);
                material.SetTextureScale("_DetailMap", new Vector2(22f, 22f));
                material.SetTextureOffset("_DetailMap", Vector2.zero);
                material.SetFloat("_DetailAlbedoScale", 1f);
                material.SetFloat("_DetailNormalScale", 0.3f);
                material.SetFloat("_DetailSmoothnessScale", 0f);
                material.SetFloat("_LinkDetailsWithBase", 0f);
                material.SetFloat("_UVDetail", 0f);
                material.SetColor(
                    "_UVDetailsMappingMask",
                    new Color(1f, 0f, 0f, 0f));
                material.SetTexture("_MaskMap", mask);
                material.EnableKeyword("_DETAIL_MAP");
                material.EnableKeyword("_MASKMAP");
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            }
            if (string.Equals(
                    sourceGuid,
                    DonorRimRustMaterialSourceGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                Texture2D baseColor = RequireAsset<Texture2D>(
                    RimRustBaseColorTexturePath);
                Texture2D detail = RequireAsset<Texture2D>(
                    RimRustPackedDetailTexturePath);
                Texture2D mask = RequireAsset<Texture2D>(
                    RimRustPackedMaskTexturePath);
                Texture2D specularColor = RequireAsset<Texture2D>(
                    importedTextures[DonorRimSpecGlossSourceGuid]);
                material.SetTexture("_BaseColorMap", baseColor);
                material.SetTextureScale("_BaseColorMap", Vector2.one);
                material.SetTextureOffset("_BaseColorMap", Vector2.zero);
                material.SetTexture("_DetailMap", detail);
                material.SetTextureScale("_DetailMap", Vector2.one);
                material.SetTextureOffset("_DetailMap", Vector2.zero);
                material.SetFloat("_DetailAlbedoScale", 0f);
                material.SetFloat("_DetailNormalScale", 0.5f);
                material.SetFloat("_DetailSmoothnessScale", 0f);
                material.SetFloat("_LinkDetailsWithBase", 0f);
                material.SetFloat("_UVDetail", 0f);
                material.SetColor(
                    "_UVDetailsMappingMask",
                    new Color(1f, 0f, 0f, 0f));
                material.SetTexture("_MaskMap", mask);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_MetallicRemapMin", 0f);
                material.SetFloat("_MetallicRemapMax", 0f);
                material.SetFloat("_SmoothnessRemapMin", 0f);
                material.SetFloat("_SmoothnessRemapMax", spec.Smoothness);
                material.SetColor(
                    "_SpecularColor",
                    new Color(
                        0.6764706f,
                        0.6764706f,
                        0.6764706f,
                        1f));
                material.SetTexture("_SpecularColorMap", specularColor);
                material.SetFloat("_EnergyConservingSpecularColor", 1f);
                material.SetFloat("_MaterialID", (float)MaterialId.LitSpecular);
                material.EnableKeyword("_DETAIL_MAP");
                material.EnableKeyword("_MASKMAP");
                material.EnableKeyword("_SPECULARCOLORMAP");
                material.EnableKeyword("_MATERIAL_FEATURE_SPECULAR_COLOR");
            }
            if (IsDonorGlassMaterialSourceGuid(sourceGuid))
            {
                Texture2D neutralRainDetail = RequireAsset<Texture2D>(
                    GlassRainNeutralDetailTexturePath);
                float donorRainOpacity = string.Equals(
                    sourceGuid,
                    DonorWindshieldGlassMaterialSourceGuid,
                    StringComparison.OrdinalIgnoreCase)
                    ? VehicleGlassRainPresenter.DonorFrontRainOpacity
                    : VehicleGlassRainPresenter.DonorCabinRainOpacity;
                material.SetTexture("_DetailMap", neutralRainDetail);
                material.SetTextureScale("_DetailMap", Vector2.one);
                material.SetTextureOffset("_DetailMap", Vector2.zero);
                material.SetFloat("_DetailAlbedoScale", 0f);
                material.SetFloat("_DetailNormalScale", donorRainOpacity);
                material.SetFloat(
                    "_DetailSmoothnessScale",
                    donorRainOpacity);
                material.SetFloat("_LinkDetailsWithBase", 0f);
                material.SetFloat("_UVDetail", 0f);
                material.SetColor(
                    "_UVDetailsMappingMask",
                    new Color(1f, 0f, 0f, 0f));
                material.EnableKeyword("_DETAIL_MAP");
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", 1f);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", spec.Transparent ? 10f : 0f);
            }

            if (spec.Transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_TransparentZWrite"))
                {
                    material.SetFloat("_TransparentZWrite", 0f);
                }
                if (material.HasProperty("_EnableFogOnTransparent"))
                {
                    material.SetFloat("_EnableFogOnTransparent", 1f);
                }
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                HDMaterial.SetSurfaceType(material, transparent: true);
                HDMaterial.SetRenderingPass(
                    material,
                    HDMaterial.RenderingPass.Default);
                if (!HDMaterial.ValidateMaterial(material))
                {
                    throw new InvalidOperationException(
                        "Generated HDRP transparent material failed validation: " +
                        sourceGuid);
                }
            }
            else
            {
                material.renderQueue = (int)RenderQueue.Geometry;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (string.Equals(
                    sourceGuid,
                    DonorRimRustMaterialSourceGuid,
                    StringComparison.OrdinalIgnoreCase) &&
                !HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Generated HDRP rim-rust material failed validation: " +
                    sourceGuid);
            }

            string path = MaterialRoot + "/" + sourceGuid + ".mat";
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static bool IsDonorGlassMaterialSourceGuid(string sourceGuid) =>
            string.Equals(
                sourceGuid,
                DonorWindshieldGlassMaterialSourceGuid,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                sourceGuid,
                DonorCabinGlassMaterialSourceGuid,
                StringComparison.OrdinalIgnoreCase);

        private static VehiclePaintSurfaceBinding CreatePaintSurfaceBinding(
            string surfaceId,
            Renderer renderer,
            Material donorRustPaintMaterial)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            int[] materialIndices = renderer.sharedMaterials
                .Select((material, index) => new { material, index })
                .Where(value => value.material == donorRustPaintMaterial)
                .Select(value => value.index)
                .ToArray();
            return new VehiclePaintSurfaceBinding(
                surfaceId,
                renderer,
                materialIndices);
        }

        private static float MatchFloat(
            string text,
            string field,
            float fallback,
            bool directField = false)
        {
            string pattern = directField
                ? @"^\s*" + Regex.Escape(field) + @":\s*(?<value>[-+0-9.eE]+)"
                : @"first:\s*name:\s*" + Regex.Escape(field) +
                  @"\s*second:\s*(?<value>[-+0-9.eE]+)";
            Match match = Regex.Match(
                text,
                pattern,
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success && float.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : fallback;
        }

        private static Color MatchColor(
            string text,
            string field,
            Color fallback)
        {
            Match match = Regex.Match(
                text,
                @"first:\s*name:\s*" + Regex.Escape(field) +
                @"\s*second:\s*\{r:\s*(?<r>[^,]+),\s*g:\s*(?<g>[^,]+),\s*b:\s*(?<b>[^,]+),\s*a:\s*(?<a>[^}]+)\}",
                RegexOptions.CultureInvariant);
            return match.Success
                ? new Color(
                    ParseFloat(match.Groups["r"].Value),
                    ParseFloat(match.Groups["g"].Value),
                    ParseFloat(match.Groups["b"].Value),
                    ParseFloat(match.Groups["a"].Value))
                : fallback;
        }

        private static PartCategory InferPartCategory(string donorName)
        {
            string value = (donorName ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(
                    value,
                    "wheel", "hubcap", "strut", "shock", "spring",
                    "wishbone", "trail arm", "spindle", "steering rod",
                    "steering rack", "sub frame", "halfshaft"))
            {
                return value.Contains("wheel", StringComparison.Ordinal) ||
                       value.Contains("hubcap", StringComparison.Ordinal)
                    ? PartCategory.Wheel
                    : PartCategory.Suspension;
            }

            if (ContainsAny(
                    value,
                    "brake", "handbrake", "clutch master"))
            {
                return PartCategory.Brake;
            }

            if (ContainsAny(
                    value,
                    "radiator", "water pump"))
            {
                return PartCategory.Cooling;
            }

            if (ContainsAny(
                    value,
                    "battery", "starter", "alternator", "electrics",
                    "distributor", "light", "radio", "gauge",
                    "dashboard meters"))
            {
                return PartCategory.Electrical;
            }

            if (ContainsAny(value, "exhaust", "headers", "muffler"))
            {
                return PartCategory.Exhaust;
            }

            if (ContainsAny(
                    value,
                    "seat", "dashboard", "steering wheel", "gear stick",
                    "fur dices", "cover suomi"))
            {
                return PartCategory.Interior;
            }

            if (ContainsAny(
                    value,
                    "door", "hood", "bootlid", "fender", "bumper",
                    "grille", "panel", "mudflap"))
            {
                return PartCategory.Body;
            }

            return PartCategory.Engine;
        }

        private static bool ContainsAny(string value, params string[] tokens) =>
            tokens.Any(token => value.Contains(token, StringComparison.Ordinal));

        private static float InferFallbackMass(string donorName)
        {
            string value = (donorName ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(value, "door", "hood", "bootlid", "sub frame"))
            {
                return 12f;
            }

            if (ContainsAny(value, "seat", "wheel", "fuel tank", "radiator"))
            {
                return 6f;
            }

            if (ContainsAny(value, "engine block", "block(clone)", "gearbox"))
            {
                return 25f;
            }

            return 1f;
        }

        private static string NormalizeDisplayName(string donorName) =>
            Regex.Replace(
                    donorName ?? "Satsuma part",
                    @"\(Clone\)$",
                    string.Empty,
                    RegexOptions.CultureInvariant)
                .Trim();

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(
                text,
                pattern,
                RegexOptions.CultureInvariant);
            return match.Success
                ? match.Groups["value"].Value.ToLowerInvariant()
                : string.Empty;
        }

        private static float ParseFloat(string value) =>
            float.Parse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);

        private static string RequireGuidPath(
            IReadOnlyDictionary<string, string> sourceByGuid,
            string guid,
            string label) =>
            sourceByGuid.TryGetValue(guid, out string path) && File.Exists(path)
                ? path
                : throw new FileNotFoundException(
                    $"Locked donor {label} GUID '{guid}' could not be resolved.");

        private static bool IsUnityBuiltInGuid(string guid) =>
            string.Equals(
                guid,
                "0000000000000000e000000000000000",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                guid,
                "0000000000000000f000000000000000",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsDonorPrimitiveMeshGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return true;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string normalized = assetPath.Replace('\\', '/');
                return normalized.IndexOf(
                    "/Primitive/",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(
                        "/Primitives/",
                        StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private static void CopyAssetAndMeta(
            string source,
            string destinationAssetPath,
            string sourceGuid,
            string generatedGuid)
        {
            string destination = ToFileSystemPath(destinationAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, overwrite: true);
            string meta = File.ReadAllText(source + ".meta");
            string sourceLine = "guid: " + sourceGuid;
            if (!meta.Contains(sourceLine))
            {
                throw new InvalidDataException(
                    $"Donor metadata for '{source}' contains no GUID {sourceGuid}.");
            }

            File.WriteAllText(
                destination + ".meta",
                meta.Replace(sourceLine, "guid: " + generatedGuid),
                new UTF8Encoding(false));
        }

        private static GameObject SavePrefabPreservingScriptReferences(
            GameObject root,
            string prefabPath)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath);
            if (prefab == null)
            {
                return null;
            }

            string path = ToFileSystemPath(prefabPath);
            string yaml = File.ReadAllText(path);
            const string missingScript = "  m_Script: {fileID: 0}";
            if (!yaml.Contains(missingScript))
            {
                return prefab;
            }

            string scriptPath = AssetDatabase.GetAllAssetPaths()
                .SingleOrDefault(candidate => string.Equals(
                    Path.GetFileName(candidate),
                    "VehiclePersistenceBinding.cs",
                    StringComparison.Ordinal));
            string scriptGuid = string.IsNullOrWhiteSpace(scriptPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrWhiteSpace(scriptGuid))
            {
                throw new InvalidDataException(
                    "VehiclePersistenceBinding script GUID could not be resolved.");
            }

            yaml = yaml.Replace(
                missingScript,
                "  m_Script: {fileID: 11500000, guid: " + scriptGuid +
                ", type: 3}");
            File.WriteAllText(path, yaml, new UTF8Encoding(false));
            return prefab;
        }

        private static string DeriveGeneratedGuid(string value)
        {
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
            return string.Concat(digest.Take(16).Select(item =>
                item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void SetStableId(
            StableEntityIdAuthoring authoring,
            string stableId)
        {
            if (!StableEntityId.TryParse(stableId, out _))
            {
                throw new InvalidDataException(
                    "Generated Satsuma stable ID is not canonical.");
            }

            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("stableId").stringValue = stableId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteManifest(
            DonorUnitySceneModel scene,
            DonorTransformRecord satsumaRoot,
            IReadOnlyList<DonorStaticRendererRecord> renderers,
            IReadOnlyList<string> meshGuids,
            IReadOnlyList<string> materialGuids,
            IReadOnlyList<string> textureGuids,
            VehicleDeliveredPartCompatibilityCatalog compatibility,
            IReadOnlyList<LoosePartBuild> loosePartBuilds,
            int chassisColliderCount)
        {
            var manifest = new ManifestDto
            {
                schemaVersion = 1,
                builderVersion = BuilderVersion,
                featureId = "P1.CAR.001",
                vehicleContentId = "vehicle.satsuma",
                stableVehicleId = StableVehicleId,
                sourceSceneSha256 = LockedSceneSha256,
                sourceRootTransformId = satsumaRoot.TransformId,
                sourceHierarchyPath = scene.GetHierarchyPath(
                    satsumaRoot.TransformId),
                transferClassification =
                    DonorTransferClassification.TemporaryDirectImport.ToString(),
                runtimePrefabPath = RuntimePrefabPath,
                rendererCount = renderers.Count,
                colliderCount = chassisColliderCount,
                meshGuids = meshGuids.ToArray(),
                materialGuids = materialGuids.ToArray(),
                textureGuids = textureGuids.ToArray(),
                mailOrderCompatibilityCount = compatibility.Entries.Count,
                loosePartCount = loosePartBuilds.Count,
                activeLoosePartCount = loosePartBuilds.Count(value =>
                    IsLoosePartActiveAtNewGame(value.Source)),
                looseParts = loosePartBuilds
                    .Select(value => new LoosePartManifestDto
                    {
                        group = value.Source.GroupName,
                        donorName = value.Source.DonorName,
                        donorTransformId = value.Source.Root.TransformId,
                        partDefinitionId = value.Source.DefinitionId,
                        stableEntityId = DeriveGeneratedGuid(
                            "satsuma-loose-part-instance:" +
                            value.Source.Root.TransformId.ToString(
                                CultureInfo.InvariantCulture)),
                        donorActiveInScene = value.Source.ActiveAtStart,
                        activeAtStart = IsLoosePartActiveAtNewGame(value.Source),
                        rendererCount = value.Source.Renderers.Count,
                        colliderCount = value.Source.Colliders.Count,
                        massKilograms = value.Definition.MassKilograms,
                    })
                    .ToArray(),
                knownLimitations = new[]
                {
                    "V1d.1 materializes the stock PartsCar new-game roster instead of mistaking its donor scene-disabled pre-initialization state for absence; donor GT/Extra placement remains separately preserved.",
                    "The selected New Game body colour applies to all seven donor paint surfaces: shell, both doors, both fenders, hood and bootlid. Temporary rally stickers and registration plates are suppressed, while the donor-active datsun_bootlid_001 exterior handle garnish remains present.",
                    "The chassis is a live 389 kg PhysX body. Assembly mass and centre of mass are recomputed from installed parts; driving remains gated by fluids, wiring, tuning, wear and driver-seat authority.",
                    "Temporary donor geometry, materials and collision remain removable Phase 1 presentation, never ProductionReady art.",
                },
            };
            string json = JsonUtility.ToJson(manifest, true) + Environment.NewLine;
            File.WriteAllText(
                ToFileSystemPath(ManifestPath),
                json,
                new UTF8Encoding(false));
        }

        private static void DeleteGeneratedRoot()
        {
            if (AssetDatabase.IsValidFolder(GeneratedRoot) &&
                !AssetDatabase.DeleteAsset(GeneratedRoot))
            {
                throw new IOException(
                    "Could not replace the bounded generated Satsuma baseline folder.");
            }
        }

        private static void PromoteGeneratedRoot()
        {
            if (!string.Equals(
                    GeneratedRoot,
                    StagingGeneratedRoot,
                    StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(StagingGeneratedRoot))
            {
                throw new InvalidOperationException(
                    "Satsuma promotion requires a validated staging root.");
            }

            string canonicalPath = ToFileSystemPath(CanonicalGeneratedRoot);
            string stagingPath = ToFileSystemPath(StagingGeneratedRoot);
            string projectRoot = Path.GetFullPath(
                Directory.GetCurrentDirectory());
            string tempRoot = Path.Combine(projectRoot, "Temp");
            string backupPath = Path.Combine(
                tempRoot,
                "Phase1SatsumaGeneratedBackup");
            if (!string.Equals(
                    Path.GetDirectoryName(canonicalPath),
                    Path.GetDirectoryName(stagingPath),
                    StringComparison.OrdinalIgnoreCase) ||
                !backupPath.StartsWith(
                    tempRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Satsuma promotion escaped its bounded project paths.");
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(tempRoot);
            if (Directory.Exists(backupPath))
            {
                FileUtil.DeleteFileOrDirectory(backupPath);
            }

            bool backupCreated = false;
            bool promotionSucceeded = false;
            try
            {
                if (Directory.Exists(canonicalPath))
                {
                    FileUtil.ReplaceDirectory(canonicalPath, backupPath);
                    backupCreated = true;
                }

                // AssetDatabase cannot rename this large imported folder after
                // its directory monitor overflows, and a raw Directory.Move is
                // denied while Unity owns the watcher handle. ReplaceDirectory
                // updates the contents in place, retaining the canonical root
                // GUID and allowing a bounded rollback from project Temp.
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    FileUtil.ReplaceDirectory(stagingPath, canonicalPath);
                    if (!AssetDatabase.DeleteAsset(StagingGeneratedRoot))
                    {
                        throw new IOException(
                            "Could not remove promoted Satsuma staging root.");
                    }
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                }

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.IsValidFolder(CanonicalGeneratedRoot) ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        RuntimePrefabPath) == null)
                {
                    throw new IOException(
                        "Promoted Satsuma root did not register its runtime " +
                        "prefab.");
                }

                promotionSucceeded = true;
            }
            catch (Exception promotionFailure)
            {
                string rollbackStatus = "not-required";
                if (backupCreated && Directory.Exists(backupPath))
                {
                    try
                    {
                        FileUtil.ReplaceDirectory(backupPath, canonicalPath);
                        AssetDatabase.Refresh(
                            ImportAssetOptions.ForceSynchronousImport);
                        rollbackStatus = "ok";
                    }
                    catch (Exception rollbackFailure)
                    {
                        rollbackStatus = rollbackFailure.Message;
                    }
                }

                throw new IOException(
                    "Could not promote validated Satsuma staging content. " +
                    "rollback=" + rollbackStatus,
                    promotionFailure);
            }
            finally
            {
                if (Directory.Exists(backupPath))
                {
                    FileUtil.DeleteFileOrDirectory(backupPath);
                }
            }

            if (!promotionSucceeded)
            {
                throw new IOException(
                    "Satsuma promotion ended without a validated result.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string Combine(string root, string relative) =>
            Path.GetFullPath(Path.Combine(
                root,
                relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string ToFileSystemPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private static void RequireHash(string path, string expected)
        {
            using var sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string actual = string.Concat(sha.ComputeHash(stream).Select(item =>
                item.ToString("x2", CultureInfo.InvariantCulture)));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked donor GAME.unity hash changed: {actual}.");
            }
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException(
                $"Unity could not load '{path}' as {typeof(T).Name}.");

        private static string SanitizeName(string value) =>
            Regex.Replace(
                string.IsNullOrWhiteSpace(value) ? "SatsumaPart" : value,
                @"[^A-Za-z0-9_.-]+",
                "_");

        private static ColliderSpec Collider(string name, string meshGuid) =>
            new ColliderSpec(name, meshGuid);

        private readonly struct SatsumaElectricalDonorEndpointDefinition
        {
            public SatsumaElectricalDonorEndpointDefinition(
                long transformId,
                string prompt,
                params string[] requiredPartDefinitionIds)
                : this(
                    transformId,
                    prompt,
                    false,
                    requiredPartDefinitionIds)
            {
            }

            public SatsumaElectricalDonorEndpointDefinition(
                long transformId,
                string prompt,
                string requiredMountId,
                string requiredMountFastenerDefinitionId,
                int requiredMountFastenerMaximumStageExclusive,
                params string[] requiredPartDefinitionIds)
            {
                TransformId = transformId;
                Prompt = prompt ?? string.Empty;
                MatchAnyRequiredPart = false;
                RequiredPartDefinitionIds = requiredPartDefinitionIds ??
                    Array.Empty<string>();
                RequiredMountId = requiredMountId ?? string.Empty;
                RequiredMountFastenerDefinitionId =
                    requiredMountFastenerDefinitionId ?? string.Empty;
                RequiredMountFastenerMaximumStageExclusive =
                    requiredMountFastenerMaximumStageExclusive;
            }

            public SatsumaElectricalDonorEndpointDefinition(
                long transformId,
                string prompt,
                bool matchAnyRequiredPart,
                params string[] requiredPartDefinitionIds)
            {
                TransformId = transformId;
                Prompt = prompt ?? string.Empty;
                MatchAnyRequiredPart = matchAnyRequiredPart;
                RequiredPartDefinitionIds = requiredPartDefinitionIds ??
                    Array.Empty<string>();
                RequiredMountId = string.Empty;
                RequiredMountFastenerDefinitionId = string.Empty;
                RequiredMountFastenerMaximumStageExclusive = 0;
            }

            public long TransformId { get; }
            public string Prompt { get; }
            public bool MatchAnyRequiredPart { get; }
            public string[] RequiredPartDefinitionIds { get; }
            public string RequiredMountId { get; }
            public string RequiredMountFastenerDefinitionId { get; }
            public int RequiredMountFastenerMaximumStageExclusive { get; }
        }

        private readonly struct SatsumaElectricalDonorConnectionDefinition
        {
            public SatsumaElectricalDonorConnectionDefinition(
                SatsumaElectricalConnection connection,
                long presentationTransformId,
                string meshGuid,
                SatsumaElectricalDonorEndpointDefinition firstEndpoint,
                SatsumaElectricalDonorEndpointDefinition secondEndpoint)
            {
                Connection = connection;
                PresentationTransformId = presentationTransformId;
                MeshGuid = meshGuid ?? string.Empty;
                FirstEndpoint = firstEndpoint;
                SecondEndpoint = secondEndpoint;
            }

            public SatsumaElectricalConnection Connection { get; }
            public long PresentationTransformId { get; }
            public string MeshGuid { get; }
            public SatsumaElectricalDonorEndpointDefinition FirstEndpoint { get; }
            public SatsumaElectricalDonorEndpointDefinition SecondEndpoint { get; }
        }

        private readonly struct ColliderSpec
        {
            public ColliderSpec(string objectName, string meshGuid)
            {
                ObjectName = objectName;
                MeshGuid = meshGuid;
            }

            public string ObjectName { get; }
            public string MeshGuid { get; }
        }

        private readonly struct SourceMaterialSpec
        {
            public SourceMaterialSpec(
                string mainTextureGuid,
                Color baseColor,
                float metallic,
                float smoothness,
                bool transparent)
            {
                MainTextureGuid = mainTextureGuid ?? string.Empty;
                BaseColor = baseColor;
                Metallic = metallic;
                Smoothness = smoothness;
                Transparent = transparent;
            }

            public string MainTextureGuid { get; }
            public Color BaseColor { get; }
            public float Metallic { get; }
            public float Smoothness { get; }
            public bool Transparent { get; }
        }

        private sealed class LoosePartSource
        {
            public LoosePartSource(
                string groupName,
                string donorName,
                DonorTransformRecord root,
                bool activeAtStart,
                DonorRigidbodyRecord rigidbody,
                IReadOnlyList<DonorStaticRendererRecord> renderers,
                IReadOnlyList<DonorColliderRecord> colliders)
            {
                GroupName = groupName ?? string.Empty;
                DonorName = donorName ?? string.Empty;
                Root = root ?? throw new ArgumentNullException(nameof(root));
                ActiveAtStart = activeAtStart;
                Rigidbody = rigidbody;
                Renderers = renderers ?? Array.Empty<DonorStaticRendererRecord>();
                Colliders = colliders ?? Array.Empty<DonorColliderRecord>();
            }

            public string GroupName { get; }
            public string DonorName { get; }
            public DonorTransformRecord Root { get; }
            public bool ActiveAtStart { get; }
            public DonorRigidbodyRecord Rigidbody { get; }
            public IReadOnlyList<DonorStaticRendererRecord> Renderers { get; }
            public IReadOnlyList<DonorColliderRecord> Colliders { get; }
            public string DefinitionId { get; set; } = string.Empty;
        }

        private readonly struct LoosePartBuild
        {
            public LoosePartBuild(
                LoosePartSource source,
                GameObject presentationPrefab,
                PartDefinition definition)
            {
                Source = source;
                PresentationPrefab = presentationPrefab;
                Definition = definition;
            }

            public LoosePartSource Source { get; }
            public GameObject PresentationPrefab { get; }
            public PartDefinition Definition { get; }
        }

        private readonly struct MountBuild
        {
            public MountBuild(
                LoosePartSource source,
                MountPointDefinition definition,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 sourceLocalScale,
                long installedTransformId,
                IReadOnlyList<FastenerBuild> fasteners,
                HingeMountBuild? hinge = null,
                string ownerPartDefinitionId = "")
            {
                Source = source;
                Definition = definition;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                SourceLocalScale = sourceLocalScale;
                InstalledTransformId = installedTransformId;
                Fasteners = fasteners ?? Array.Empty<FastenerBuild>();
                Hinge = hinge;
                OwnerPartDefinitionId = ownerPartDefinitionId ?? string.Empty;
            }

            public LoosePartSource Source { get; }
            public MountPointDefinition Definition { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 SourceLocalScale { get; }
            public long InstalledTransformId { get; }
            public IReadOnlyList<FastenerBuild> Fasteners { get; }
            public HingeMountBuild? Hinge { get; }
            public string OwnerPartDefinitionId { get; }

            public MountBuild WithPose(
                Vector3 localPosition,
                Quaternion localRotation)
            {
                return new MountBuild(
                    Source,
                    Definition,
                    localPosition,
                    localRotation,
                    SourceLocalScale,
                    InstalledTransformId,
                    Fasteners,
                    Hinge,
                    OwnerPartDefinitionId);
            }

            public MountBuild WithPoseAndOwner(
                Vector3 localPosition,
                Quaternion localRotation,
                string ownerPartDefinitionId)
            {
                return new MountBuild(
                    Source,
                    Definition,
                    localPosition,
                    localRotation,
                    SourceLocalScale,
                    InstalledTransformId,
                    Fasteners,
                    Hinge,
                    ownerPartDefinitionId);
            }

            public MountBuild WithFasteners(
                IReadOnlyList<FastenerBuild> fasteners)
            {
                return new MountBuild(
                    Source,
                    Definition,
                    LocalPosition,
                    LocalRotation,
                    SourceLocalScale,
                    InstalledTransformId,
                    fasteners,
                    Hinge,
                    OwnerPartDefinitionId);
            }
        }

        private readonly struct ReviewedRearSuspensionPose
        {
            public ReviewedRearSuspensionPose(
                string mountId,
                Vector3 localPosition,
                Quaternion localRotation)
            {
                MountId = mountId ?? string.Empty;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }

            public string MountId { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
        }

        private readonly struct ReviewedRearRoadWheelPose
        {
            public ReviewedRearRoadWheelPose(
                string mountId,
                string ownerPartDefinitionId,
                Vector3 localPosition,
                Quaternion localRotation)
            {
                MountId = mountId ?? string.Empty;
                OwnerPartDefinitionId = ownerPartDefinitionId ?? string.Empty;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }

            public string MountId { get; }
            public string OwnerPartDefinitionId { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
        }

        private readonly struct FrontSuspensionRuntimeRigPose
        {
            public FrontSuspensionRuntimeRigPose(
                string cornerId,
                bool leftSide,
                Vector3 fullDroopHubLocalPosition,
                Quaternion fullDroopHubLocalRotation,
                Vector3 canonicalNoStrutHubLocalPosition,
                Quaternion canonicalNoStrutHubLocalRotation,
                Vector3 wishboneBodyPivotLocalPosition,
                Quaternion wishboneMeshZeroLocalRotation,
                Vector3 hubToWishboneTargetLocalOffset,
                Vector3 hubToSpindleMeshLocalOffset,
                Quaternion spindleMeshLocalRotation,
                Vector3 hubToShockBottomLocalOffset,
                Quaternion shockBottomLocalRotation,
                Vector3 strutTopLocalPosition,
                Quaternion strutTopLocalRotation,
                string strutMeshName,
                Bounds strutLocalBounds,
                string roadWheelMountId,
                Vector3 steeringRodRootLocalPosition,
                Quaternion steeringRodRootLocalRotation,
                Quaternion steeringRodArmatureLocalRotation,
                Quaternion steeringRodPivotLocalRotation,
                Vector3 hubToSteeringOuterLocalOffset,
                Quaternion steeringOuterLocalRotation,
                string steeringRodMeshName,
                Bounds steeringRodLocalBounds,
                Vector3 hubToDiscBrakeLocalOffset,
                Quaternion discBrakeLocalRotation,
                Vector3 hubToRoadWheelLocalOffset,
                Quaternion roadWheelLocalRotation)
            {
                CornerId = cornerId ?? string.Empty;
                LeftSide = leftSide;
                FullDroopHubLocalPosition = fullDroopHubLocalPosition;
                FullDroopHubLocalRotation = fullDroopHubLocalRotation;
                CanonicalNoStrutHubLocalPosition =
                    canonicalNoStrutHubLocalPosition;
                CanonicalNoStrutHubLocalRotation =
                    canonicalNoStrutHubLocalRotation;
                WishboneBodyPivotLocalPosition =
                    wishboneBodyPivotLocalPosition;
                WishboneMeshZeroLocalRotation =
                    wishboneMeshZeroLocalRotation;
                HubToWishboneTargetLocalOffset =
                    hubToWishboneTargetLocalOffset;
                HubToSpindleMeshLocalOffset =
                    hubToSpindleMeshLocalOffset;
                SpindleMeshLocalRotation = spindleMeshLocalRotation;
                HubToShockBottomLocalOffset =
                    hubToShockBottomLocalOffset;
                ShockBottomLocalRotation = shockBottomLocalRotation;
                StrutTopLocalPosition = strutTopLocalPosition;
                StrutTopLocalRotation = strutTopLocalRotation;
                StrutMeshName = strutMeshName ?? string.Empty;
                StrutLocalBounds = strutLocalBounds;
                RoadWheelMountId = roadWheelMountId ?? string.Empty;
                SteeringRodRootLocalPosition =
                    steeringRodRootLocalPosition;
                SteeringRodRootLocalRotation =
                    steeringRodRootLocalRotation;
                SteeringRodArmatureLocalRotation =
                    steeringRodArmatureLocalRotation;
                SteeringRodPivotLocalRotation =
                    steeringRodPivotLocalRotation;
                HubToSteeringOuterLocalOffset =
                    hubToSteeringOuterLocalOffset;
                SteeringOuterLocalRotation = steeringOuterLocalRotation;
                SteeringRodMeshName = steeringRodMeshName ?? string.Empty;
                SteeringRodLocalBounds = steeringRodLocalBounds;
                HubToDiscBrakeLocalOffset = hubToDiscBrakeLocalOffset;
                DiscBrakeLocalRotation = discBrakeLocalRotation;
                HubToRoadWheelLocalOffset = hubToRoadWheelLocalOffset;
                RoadWheelLocalRotation = roadWheelLocalRotation;
            }

            public string CornerId { get; }
            public bool LeftSide { get; }
            public Vector3 FullDroopHubLocalPosition { get; }
            public Quaternion FullDroopHubLocalRotation { get; }
            public Vector3 CanonicalNoStrutHubLocalPosition { get; }
            public Quaternion CanonicalNoStrutHubLocalRotation { get; }
            public Vector3 WishboneBodyPivotLocalPosition { get; }
            public Quaternion WishboneMeshZeroLocalRotation { get; }
            public Vector3 HubToWishboneTargetLocalOffset { get; }
            public Vector3 HubToSpindleMeshLocalOffset { get; }
            public Quaternion SpindleMeshLocalRotation { get; }
            public Vector3 HubToShockBottomLocalOffset { get; }
            public Quaternion ShockBottomLocalRotation { get; }
            public Vector3 StrutTopLocalPosition { get; }
            public Quaternion StrutTopLocalRotation { get; }
            public string StrutMeshName { get; }
            public Bounds StrutLocalBounds { get; }
            public string RoadWheelMountId { get; }
            public Vector3 SteeringRodRootLocalPosition { get; }
            public Quaternion SteeringRodRootLocalRotation { get; }
            public Quaternion SteeringRodArmatureLocalRotation { get; }
            public Quaternion SteeringRodPivotLocalRotation { get; }
            public Vector3 HubToSteeringOuterLocalOffset { get; }
            public Quaternion SteeringOuterLocalRotation { get; }
            public string SteeringRodMeshName { get; }
            public Bounds SteeringRodLocalBounds { get; }
            public Vector3 HubToDiscBrakeLocalOffset { get; }
            public Quaternion DiscBrakeLocalRotation { get; }
            public Vector3 HubToRoadWheelLocalOffset { get; }
            public Quaternion RoadWheelLocalRotation { get; }

            public Vector3 SpindleMountLocalPosition =>
                FullDroopHubLocalPosition +
                FullDroopHubLocalRotation * HubToSpindleMeshLocalOffset;

            public Quaternion SpindleMountLocalRotation =>
                FullDroopHubLocalRotation * SpindleMeshLocalRotation;
        }

        private readonly struct RearSuspensionRuntimeRigPose
        {
            public RearSuspensionRuntimeRigPose(
                string cornerId,
                Vector3 drumLocalPosition,
                Quaternion drumLocalRotation,
                Vector3 springTopCarLocalPosition,
                Quaternion springTopCarLocalRotation,
                Vector3 springBottomArmLocalPosition,
                Quaternion springBottomArmLocalRotation,
                Vector3 shockTopCarLocalPosition,
                Quaternion shockTopCarLocalRotation,
                Vector3 shockBottomArmLocalPosition,
                Quaternion shockBottomArmLocalRotation)
            {
                CornerId = cornerId ?? string.Empty;
                DrumLocalPosition = drumLocalPosition;
                DrumLocalRotation = drumLocalRotation;
                SpringTopCarLocalPosition = springTopCarLocalPosition;
                SpringTopCarLocalRotation = springTopCarLocalRotation;
                SpringBottomArmLocalPosition = springBottomArmLocalPosition;
                SpringBottomArmLocalRotation = springBottomArmLocalRotation;
                ShockTopCarLocalPosition = shockTopCarLocalPosition;
                ShockTopCarLocalRotation = shockTopCarLocalRotation;
                ShockBottomArmLocalPosition = shockBottomArmLocalPosition;
                ShockBottomArmLocalRotation = shockBottomArmLocalRotation;
            }

            public string CornerId { get; }
            public Vector3 DrumLocalPosition { get; }
            public Quaternion DrumLocalRotation { get; }
            public Vector3 SpringTopCarLocalPosition { get; }
            public Quaternion SpringTopCarLocalRotation { get; }
            public Vector3 SpringBottomArmLocalPosition { get; }
            public Quaternion SpringBottomArmLocalRotation { get; }
            public Vector3 ShockTopCarLocalPosition { get; }
            public Quaternion ShockTopCarLocalRotation { get; }
            public Vector3 ShockBottomArmLocalPosition { get; }
            public Quaternion ShockBottomArmLocalRotation { get; }
        }

        private readonly struct OwnedPartMountCandidate
        {
            public OwnedPartMountCandidate(
                DonorAssemblyFsmEvidence assembly,
                LoosePartBuild owner,
                LoosePartBuild part,
                long parentTransformId)
            {
                Assembly = assembly;
                Owner = owner;
                Part = part;
                ParentTransformId = parentTransformId;
            }

            public DonorAssemblyFsmEvidence Assembly { get; }
            public LoosePartBuild Owner { get; }
            public LoosePartBuild Part { get; }
            public long ParentTransformId { get; }
        }

        private readonly struct HingeMountBuild
        {
            public HingeMountBuild(
                Vector3 localAxis,
                float minimumAngleDegrees,
                float maximumAngleDegrees,
                float donorBreakForce,
                float donorBreakTorque,
                long useFsmComponentId,
                Vector3 donorOpenTorqueLocal,
                Vector3 donorCloseTorqueLocal,
                bool requiresReleaseBeforeOpening = false,
                float donorOpenHoldWindowDegrees = 0f)
            {
                LocalAxis = localAxis;
                MinimumAngleDegrees = minimumAngleDegrees;
                MaximumAngleDegrees = maximumAngleDegrees;
                DonorBreakForce = donorBreakForce;
                DonorBreakTorque = donorBreakTorque;
                UseFsmComponentId = useFsmComponentId;
                DonorOpenTorqueLocal = donorOpenTorqueLocal;
                DonorCloseTorqueLocal = donorCloseTorqueLocal;
                DonorOpenHoldWindowDegrees =
                    donorOpenHoldWindowDegrees;
                RequiresReleaseBeforeOpening =
                    requiresReleaseBeforeOpening;
            }

            public Vector3 LocalAxis { get; }
            public float MinimumAngleDegrees { get; }
            public float MaximumAngleDegrees { get; }
            public float DonorBreakForce { get; }
            public float DonorBreakTorque { get; }
            public long UseFsmComponentId { get; }
            public Vector3 DonorOpenTorqueLocal { get; }
            public Vector3 DonorCloseTorqueLocal { get; }
            public float DonorOpenHoldWindowDegrees { get; }
            public bool RequiresReleaseBeforeOpening { get; }
        }

        private readonly struct FastenerBuild
        {
            public FastenerBuild(
                FastenerDefinition definition,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 sourceLocalScale,
                float colliderRadiusMeters,
                long markerTransformId,
                string meshSourceGuid = DonorFastenerMeshSourceGuid)
            {
                Definition = definition;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                SourceLocalScale = sourceLocalScale;
                ColliderRadiusMeters = colliderRadiusMeters;
                MarkerTransformId = markerTransformId;
                MeshSourceGuid = meshSourceGuid;
            }

            public FastenerDefinition Definition { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 SourceLocalScale { get; }
            public float ColliderRadiusMeters { get; }
            public long MarkerTransformId { get; }
            public string MeshSourceGuid { get; }

            public FastenerBuild WithMeshSourceGuid(string meshSourceGuid) =>
                new FastenerBuild(
                    Definition,
                    LocalPosition,
                    LocalRotation,
                    SourceLocalScale,
                    ColliderRadiusMeters,
                    MarkerTransformId,
                    meshSourceGuid);
        }

        private readonly struct ParentMountCandidate
        {
            public ParentMountCandidate(
                DonorAssemblyFsmEvidence assembly,
                long parentTransformId,
                LoosePartBuild part)
            {
                Assembly = assembly ?? throw new ArgumentNullException(
                    nameof(assembly));
                ParentTransformId = parentTransformId;
                Part = part;
            }

            public DonorAssemblyFsmEvidence Assembly { get; }
            public long ParentTransformId { get; }
            public LoosePartBuild Part { get; }
        }

        private readonly struct InstalledCandidateScore
        {
            public InstalledCandidateScore(
                DonorTransformRecord transform,
                int score)
            {
                Transform = transform;
                Score = score;
            }

            public DonorTransformRecord Transform { get; }
            public int Score { get; }
        }

        private sealed class InstalledMountCandidate
        {
            public InstalledMountCandidate(
                LoosePartSource loosePart,
                int topScore,
                IReadOnlyList<DonorTransformRecord> candidates)
            {
                LoosePart = loosePart;
                TopScore = topScore;
                Candidates = candidates ?? Array.Empty<DonorTransformRecord>();
            }

            public LoosePartSource LoosePart { get; }
            public int TopScore { get; }
            public IReadOnlyList<DonorTransformRecord> Candidates { get; }
            public bool IsUnique => Candidates.Count == 1;
        }

        private readonly struct MeshMountPoseEvidence
        {
            public MeshMountPoseEvidence(
                long looseRendererComponentId,
                long installedRendererComponentId,
                long installedTransformId,
                Vector3 position,
                Quaternion rotation,
                Vector3 scale)
            {
                LooseRendererComponentId = looseRendererComponentId;
                InstalledRendererComponentId = installedRendererComponentId;
                InstalledTransformId = installedTransformId;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public long LooseRendererComponentId { get; }
            public long InstalledRendererComponentId { get; }
            public long InstalledTransformId { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private sealed class MeshMountPoseCluster
        {
            private const float PositionToleranceMeters = 0.0025f;
            private const float RotationToleranceDegrees = 0.25f;
            private const float ScaleTolerance = 0.0025f;
            private readonly List<MeshMountPoseEvidence> evidence =
                new List<MeshMountPoseEvidence>();

            public MeshMountPoseCluster(MeshMountPoseEvidence first)
            {
                Position = first.Position;
                Rotation = first.Rotation;
                Scale = first.Scale;
                evidence.Add(first);
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
            public IReadOnlyList<MeshMountPoseEvidence> Evidence => evidence;
            public int DistinctLooseRendererCount => evidence
                .Select(value => value.LooseRendererComponentId)
                .Distinct()
                .Count();

            public bool Matches(MeshMountPoseEvidence candidate) =>
                Vector3.Distance(Position, candidate.Position) <=
                PositionToleranceMeters &&
                Quaternion.Angle(Rotation, candidate.Rotation) <=
                RotationToleranceDegrees &&
                Vector3.Distance(Scale, candidate.Scale) <= ScaleTolerance;

            public void Add(MeshMountPoseEvidence candidate)
            {
                evidence.Add(candidate);
            }
        }

        private sealed class MeshDerivedMountPose
        {
            public MeshDerivedMountPose(
                LoosePartSource loosePart,
                int matchedPairCount,
                IReadOnlyList<MeshMountPoseCluster> bestPoses)
            {
                LoosePart = loosePart;
                MatchedPairCount = matchedPairCount;
                BestPoses = bestPoses ?? Array.Empty<MeshMountPoseCluster>();
            }

            public LoosePartSource LoosePart { get; }
            public int MatchedPairCount { get; }
            public IReadOnlyList<MeshMountPoseCluster> BestPoses { get; }
            public bool IsUnique => BestPoses.Count == 1;
        }

        private sealed class DonorAssemblyFsmEvidence
        {
            public DonorAssemblyFsmEvidence(
                long componentId,
                long triggerGameObjectId,
                long triggerTransformId,
                string triggerName,
                string handChild,
                long activateThisGameObjectId,
                string activateThisHierarchyPath,
                IReadOnlyList<DonorNamedGameObjectReference> activationRoots,
                long requiredGameObjectId,
                string requiredHierarchyPath,
                long required1GameObjectId,
                string required1HierarchyPath,
                long thisPartGameObjectId,
                string thisPartName,
                long detachPartGameObjectId,
                string detachPartHierarchyPath,
                long parentGameObjectId,
                string parentHierarchyPath,
                long boltsGameObjectId,
                string boltsHierarchyPath,
                bool referencesBolts,
                bool referencesBolted)
            {
                ComponentId = componentId;
                TriggerGameObjectId = triggerGameObjectId;
                TriggerTransformId = triggerTransformId;
                TriggerName = triggerName ?? string.Empty;
                HandChild = handChild ?? string.Empty;
                ActivateThisGameObjectId = activateThisGameObjectId;
                ActivateThisHierarchyPath = activateThisHierarchyPath ?? string.Empty;
                ActivationRoots = activationRoots ??
                    Array.Empty<DonorNamedGameObjectReference>();
                RequiredGameObjectId = requiredGameObjectId;
                RequiredHierarchyPath = requiredHierarchyPath ?? string.Empty;
                Required1GameObjectId = required1GameObjectId;
                Required1HierarchyPath = required1HierarchyPath ?? string.Empty;
                ThisPartGameObjectId = thisPartGameObjectId;
                ThisPartName = thisPartName ?? string.Empty;
                DetachPartGameObjectId = detachPartGameObjectId;
                DetachPartHierarchyPath = detachPartHierarchyPath ?? string.Empty;
                ParentGameObjectId = parentGameObjectId;
                ParentHierarchyPath = parentHierarchyPath ?? string.Empty;
                BoltsGameObjectId = boltsGameObjectId;
                BoltsHierarchyPath = boltsHierarchyPath ?? string.Empty;
                ReferencesBolts = referencesBolts;
                ReferencesBolted = referencesBolted;
            }

            public long ComponentId { get; }
            public long TriggerGameObjectId { get; }
            public long TriggerTransformId { get; }
            public string TriggerName { get; }
            public string HandChild { get; }
            public long ActivateThisGameObjectId { get; }
            public string ActivateThisHierarchyPath { get; }
            public IReadOnlyList<DonorNamedGameObjectReference>
                ActivationRoots { get; }
            public long RequiredGameObjectId { get; }
            public string RequiredHierarchyPath { get; }
            public long Required1GameObjectId { get; }
            public string Required1HierarchyPath { get; }
            public long ThisPartGameObjectId { get; }
            public string ThisPartName { get; }
            public long DetachPartGameObjectId { get; }
            public string DetachPartHierarchyPath { get; }
            public long ParentGameObjectId { get; }
            public string ParentHierarchyPath { get; }
            public long BoltsGameObjectId { get; }
            public string BoltsHierarchyPath { get; }
            public bool ReferencesBolts { get; }
            public bool ReferencesBolted { get; }
        }

        private sealed class DonorNamedGameObjectReference
        {
            public DonorNamedGameObjectReference(
                string variableName,
                long gameObjectId)
            {
                VariableName = variableName ?? string.Empty;
                GameObjectId = gameObjectId;
            }

            public string VariableName { get; }
            public long GameObjectId { get; }
        }

        private sealed class DonorFastenerEvidence
        {
            public DonorFastenerEvidence(
                DonorAssemblyFsmEvidence assembly,
                long installedTransformId,
                long markerTransformId,
                long markerGameObjectId,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale,
                float colliderRadiusMeters,
                int inferredWrenchMillimeters,
                bool hasSupportedWrenchSize,
                int maximumStage)
            {
                Assembly = assembly ?? throw new ArgumentNullException(
                    nameof(assembly));
                InstalledTransformId = installedTransformId;
                MarkerTransformId = markerTransformId;
                MarkerGameObjectId = markerGameObjectId;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
                ColliderRadiusMeters = colliderRadiusMeters;
                InferredWrenchMillimeters = inferredWrenchMillimeters;
                HasSupportedWrenchSize = hasSupportedWrenchSize;
                MaximumStage = maximumStage;
            }

            public DonorAssemblyFsmEvidence Assembly { get; }
            public long InstalledTransformId { get; }
            public long MarkerTransformId { get; }
            public long MarkerGameObjectId { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public float ColliderRadiusMeters { get; }
            public int InferredWrenchMillimeters { get; }
            public bool HasSupportedWrenchSize { get; }
            public int MaximumStage { get; }
        }

        private sealed class DonorBoltCheckEvidence
        {
            public DonorBoltCheckEvidence(
                long componentId,
                long gameObjectId,
                long transformId,
                string hierarchyPath,
                bool hasBoltedYes,
                float boltedYes,
                bool hasBoltedNo,
                float boltedNo,
                bool referencesTightness,
                bool referencesBreak,
                bool referencesSpeed,
                float looseBreakSpeedKph,
                float partialCheckSpeedKph,
                float chanceMaximumTightness,
                float chanceDivisor,
                bool hasChanceFormula,
                bool isWheelRetentionPolicy)
            {
                ComponentId = componentId;
                GameObjectId = gameObjectId;
                TransformId = transformId;
                HierarchyPath = hierarchyPath ?? string.Empty;
                HasBoltedYes = hasBoltedYes;
                BoltedYes = boltedYes;
                HasBoltedNo = hasBoltedNo;
                BoltedNo = boltedNo;
                ReferencesTightness = referencesTightness;
                ReferencesBreak = referencesBreak;
                ReferencesSpeed = referencesSpeed;
                LooseBreakSpeedKph = looseBreakSpeedKph;
                PartialCheckSpeedKph = partialCheckSpeedKph;
                ChanceMaximumTightness = chanceMaximumTightness;
                ChanceDivisor = chanceDivisor;
                HasChanceFormula = hasChanceFormula;
                IsWheelRetentionPolicy = isWheelRetentionPolicy;
            }

            public long ComponentId { get; }
            public long GameObjectId { get; }
            public long TransformId { get; }
            public string HierarchyPath { get; }
            public bool HasBoltedYes { get; }
            public float BoltedYes { get; }
            public bool HasBoltedNo { get; }
            public float BoltedNo { get; }
            public bool ReferencesTightness { get; }
            public bool ReferencesBreak { get; }
            public bool ReferencesSpeed { get; }
            public float LooseBreakSpeedKph { get; }
            public float PartialCheckSpeedKph { get; }
            public float ChanceMaximumTightness { get; }
            public float ChanceDivisor { get; }
            public bool HasChanceFormula { get; }
            public bool IsWheelRetentionPolicy { get; }
        }

        [Serializable]
        private sealed class ManifestDto
        {
            public int schemaVersion;
            public string builderVersion;
            public string featureId;
            public string vehicleContentId;
            public string stableVehicleId;
            public string sourceSceneSha256;
            public long sourceRootTransformId;
            public string sourceHierarchyPath;
            public string transferClassification;
            public string runtimePrefabPath;
            public int rendererCount;
            public int colliderCount;
            public string[] meshGuids;
            public string[] materialGuids;
            public string[] textureGuids;
            public int mailOrderCompatibilityCount;
            public int loosePartCount;
            public int activeLoosePartCount;
            public LoosePartManifestDto[] looseParts;
            public string[] knownLimitations;
        }

        [Serializable]
        private sealed class LoosePartManifestDto
        {
            public string group;
            public string donorName;
            public long donorTransformId;
            public string partDefinitionId;
            public string stableEntityId;
            public bool activeAtStart;
            public bool donorActiveInScene;
            public int rendererCount;
            public int colliderCount;
            public float massKilograms;
        }
    }
}
