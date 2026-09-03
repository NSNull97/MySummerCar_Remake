using System;
using System.Collections.Generic;
using System.IO;
using Enviro;
using MSC.Audio.Composition;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public sealed class HybridEnvironmentMigrationWindow : EditorWindow
    {
        private Vector2 scroll;
        private string report = "Run Dry Run before applying the migration.";

        [MenuItem("Tools/MSC/Environment/Migrate Enviro To Hybrid HDRP")]
        public static void Open()
        {
            GetWindow<HybridEnvironmentMigrationWindow>(
                "Enviro Hybrid Migration");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Dry Run is read-only. Apply changes only the project-owned " +
                "Bootstrap scene and project-owned profile assets.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run"))
                {
                    report = HybridEnvironmentMigrationTool.DryRun();
                }

                if (GUILayout.Button("Apply Migration"))
                {
                    report = HybridEnvironmentMigrationTool.Apply();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    public static class HybridEnvironmentMigrationTool
    {
        public const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string EnviroSkyProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/ProductionEnviroSkyVolume.asset";
        public const string NativeHdrpProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/ProductionNativeHDRPVolume.asset";
        public const string RenderProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/HybridWeatherRenderProfile.asset";
        public const string ClosedZoneProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/WeatherZone_ClosedInterior.asset";
        public const string ShelterZoneProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/WeatherZone_Shelter.asset";
        public const string ZoneCellCatalogPath =
            "Assets/Game/Weather/Production/Content/Zones/WeatherZoneCellCatalog.asset";

        private const string TeimoCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
        private const string FleetariCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_3_-1_Legacy.unity";
        private const string CabinCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-1_Legacy.unity";
        private const string CabinShedCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_0_Legacy.unity";
        private const string CottageCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-2_-2_Legacy.unity";
        private const string DancehallCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_1_0_Legacy.unity";
        private const string JailCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-1_-5_Legacy.unity";
        private const string AbandonedWestCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_2_-1_Legacy.unity";
        private const string FactoryCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_4_-3_Legacy.unity";
        private const string FarmCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-2_0_Legacy.unity";
        private const string StrawberryCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_-4_Legacy.unity";
        private const string HomeCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-3_Legacy.unity";
        private const string HighwayBridgeCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_3_0_Legacy.unity";

        private const string VendorRoot = "Assets/Enviro 3 - Sky and Weather/";
        private const string HomeHouseShelterStableId =
            "weather.shelter.home.house.interior.v1";
        private const float HomeInteriorFloorMeters = 0.8f;
        private const float HomeInteriorCeilingMeters = 4.5f;

        private static readonly CompoundInteriorVolume[] HomeHouseVolumes =
        {
            // Canonical donor NoRain footprints extruded only through the
            // playable interior height. Their local Z axes point world-up.
            new CompoundInteriorVolume(
                new Vector3(162.06999f, 2.65f, -1037.315f),
                new Quaternion(
                    -0.70710665f,
                    0.0000000115f,
                    -0.0000000049f,
                    0.7071069f),
                new Vector3(
                    6.0411706f,
                    2.3143263f,
                    HomeInteriorCeilingMeters - HomeInteriorFloorMeters)),
            new CompoundInteriorVolume(
                new Vector3(154.79999f, 2.65f, -1038.375f),
                new Quaternion(
                    -0.70710665f,
                    0.0000000115f,
                    0.0000000006f,
                    0.7071069f),
                new Vector3(
                    8.573909f,
                    10.44192f,
                    HomeInteriorCeilingMeters - HomeInteriorFloorMeters)),
            new CompoundInteriorVolume(
                new Vector3(162.79845f, 2.65f, -1030.7153f),
                new Quaternion(
                    -0.7071066f,
                    -0.0000000212f,
                    -0.0000000286f,
                    0.70710695f),
                new Vector3(
                    11.431875f,
                    10.848738f,
                    HomeInteriorCeilingMeters - HomeInteriorFloorMeters)),
        };

        [MenuItem("Tools/MSC/Environment/Migrate Enviro To Hybrid HDRP Dry Run")]
        public static void DryRunMenu() => Debug.Log(DryRun());

        public static void ApplyBatch() => Debug.Log(Apply());

        public static string DryRun()
        {
            var lines = new List<string>(32)
            {
                "MSC Enviro -> Hybrid HDRP migration dry run",
                "Unity: " + Application.unityVersion,
                "Scene: " + BootstrapScenePath,
            };

            Scene scene = OpenBootstrapForInspection(out bool openedHere);
            try
            {
                EnviroManager manager = FindSingle<EnviroManager>(scene);
                ProductionEnvironmentController controller =
                    FindSingle<ProductionEnvironmentController>(scene);
                Enviro3EnvironmentAdapter adapter =
                    FindSingle<Enviro3EnvironmentAdapter>(scene);
                HybridEnvironmentMarker marker =
                    FindOptionalSingle<HybridEnvironmentMarker>(scene);
                lines.Add("EnviroManager: " + HierarchyPath(manager.transform));
                lines.Add("EnvironmentController: " +
                          HierarchyPath(controller.transform));
                lines.Add("Adapter: " + HierarchyPath(adapter.transform));
                lines.Add("Existing hybrid marker: " +
                          (marker != null ? HierarchyPath(marker.transform) : "none"));
                lines.Add("Planned project assets:");
                lines.Add("  CREATE/UPDATE " + EnviroSkyProfilePath);
                lines.Add("  CREATE/UPDATE " + NativeHdrpProfilePath);
                lines.Add("  CREATE/UPDATE " + RenderProfilePath);
                lines.Add("  CREATE/UPDATE " + ClosedZoneProfilePath);
                lines.Add("  CREATE/UPDATE " + ShelterZoneProfilePath);
                lines.Add("  CREATE/UPDATE " + ZoneCellCatalogPath);
                lines.Add("Planned scene changes:");
                lines.Add("  EnviroSkyVolume keeps VisualEnvironment + EnviroHDRPSky");
                lines.Add("  MSC_HDRP_GlobalVolume becomes the sole active Fog/Exposure writer");
                lines.Add("  Add WeatherRuntimeState source, zone registry/resolver and debug overlay");
                lines.Add("  Disable legacy Enviro3ShelterRemovalBridge when hybrid is active");
                int measuredShelters = FindAll<ProductionShelterVolumeAuthoring>(scene).Length;
                lines.Add("  Convert measured shelter volumes only: " + measuredShelters);
                lines.Add("  Stream 20 measured interior/shelter volumes across 17 logical locations");
                lines.Add("  Do not infer unverified interiors from Renderer.bounds");
                lines.Add("Vendor package changes: 0");
                lines.Add("KWS/water changes: 0");
                return string.Join("\n", lines);
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public static string Apply()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return "Migration cancelled before any change.";
            }

            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            EnviroManager manager = FindSingle<EnviroManager>(scene);
            ProductionEnvironmentController controller =
                FindSingle<ProductionEnvironmentController>(scene);
            Enviro3EnvironmentAdapter adapter =
                FindSingle<Enviro3EnvironmentAdapter>(scene);
            HybridEnvironmentMarker existing =
                FindOptionalSingle<HybridEnvironmentMarker>(scene);
            if (existing != null && existing.IsComplete &&
                existing.MigrationVersion ==
                HybridEnvironmentMarker.CurrentMigrationVersion)
            {
                return "Hybrid migration is already applied and complete.";
            }

            VolumeProfile sourceProfile = manager.volumeHDRP != null
                ? manager.volumeHDRP.sharedProfile
                : null;
            if (sourceProfile == null)
            {
                throw new InvalidOperationException(
                    "Enviro manager has no source HDRP profile to migrate.");
            }

            RejectVendorAsset(sourceProfile);
            VolumeProfile enviroSky = EnsureEnviroSkyProfile(sourceProfile);
            VolumeProfile nativeHdrp = EnsureNativeHdrpProfile(sourceProfile);
            HybridWeatherRenderProfile renderProfile = EnsureRenderProfile();
            WeatherZoneProfile closedProfile = EnsureClosedZoneProfile();
            WeatherZoneProfile shelterProfile = EnsureShelterZoneProfile();
            WeatherZoneCellCatalog zoneCellCatalog =
                EnsureZoneCellCatalog(closedProfile, shelterProfile);

            Undo.SetCurrentGroupName("Migrate Enviro to Hybrid HDRP");
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(manager, "Assign Enviro sky-only HDRP profile");
            Undo.RecordObject(manager.volumeHDRP, "Configure Enviro sky volume");
            manager.volumeHDRP.name = "EnviroSkyVolume";
            manager.volumeHDRP.isGlobal = true;
            manager.volumeHDRP.priority = 10f;
            manager.volumeHDRP.sharedProfile = enviroSky;

            GameObject composition = controller.gameObject;
            WeatherZoneRegistry registry = GetOrAdd<WeatherZoneRegistry>(composition);
            WeatherZoneStreamingBinder zoneStreamingBinder =
                GetOrAdd<WeatherZoneStreamingBinder>(composition);
            zoneStreamingBinder.ConfigureForAuthoring(zoneCellCatalog);
            ProductionWeatherStateSource source =
                GetOrAdd<ProductionWeatherStateSource>(composition);
            source.ConfigureForAuthoring(controller);
            WeatherExposureResolver resolver =
                GetOrAdd<WeatherExposureResolver>(composition);

            AudioListener listener = FindOptionalSingle<AudioListener>(scene);
            Camera camera = FindOptionalSingle<Camera>(scene);
            if (listener != null &&
                (!listener.enabled || !listener.gameObject.activeInHierarchy))
            {
                listener = null;
            }

            if (camera != null &&
                (!camera.enabled || !camera.gameObject.activeInHierarchy))
            {
                camera = null;
            }

            resolver.ConfigureForAuthoring(
                registry,
                source,
                camera != null ? camera.transform : null,
                listener,
                controller.transform);

            Transform nativeVolumeTransform =
                FindOrCreateChild(composition.transform, "MSC_HDRP_GlobalVolume");
            Volume nativeVolume = GetOrAdd<Volume>(nativeVolumeTransform.gameObject);
            Undo.RecordObject(nativeVolume, "Configure native HDRP global volume");
            nativeVolume.isGlobal = true;
            nativeVolume.priority = 20f;
            nativeVolume.sharedProfile = nativeHdrp;
            NativeHdrpWeatherBridge hdrpBridge =
                GetOrAdd<NativeHdrpWeatherBridge>(nativeVolumeTransform.gameObject);
            hdrpBridge.ConfigureForAuthoring(
                source,
                resolver,
                renderProfile,
                nativeVolume,
                manager.Lighting != null
                    ? manager.Lighting.directionalLightHDRP
                    : null,
                manager.Lighting != null
                    ? manager.Lighting.additionalLightHDRP
                    : null);

            adapter.ConfigureHybridOwnershipForAuthoring(true);
            EditorUtility.SetDirty(adapter);
            Enviro3ShelterRemovalBridge legacyRemoval =
                FindOptionalSingle<Enviro3ShelterRemovalBridge>(scene);
            if (legacyRemoval != null)
            {
                Undo.RecordObject(legacyRemoval, "Disable legacy shelter bridge");
                legacyRemoval.enabled = false;
            }

            Enviro3WeatherZoneRemovalBridge zoneRemoval =
                GetOrAdd<Enviro3WeatherZoneRemovalBridge>(composition);
            zoneRemoval.ConfigureForAuthoring(manager, registry, resolver);
            ProductionAudioComposition audioComposition =
                FindOptionalSingle<ProductionAudioComposition>(scene);
            if (audioComposition != null)
            {
                audioComposition.ConfigureWeatherExposureForAuthoring(resolver);
                EditorUtility.SetDirty(audioComposition);
            }
            ConvertMeasuredShelters(scene, registry, closedProfile);

            HybridEnvironmentMarker marker =
                existing != null
                    ? existing
                    : GetOrAdd<HybridEnvironmentMarker>(composition);
            marker.ConfigureForAuthoring(
                source,
                registry,
                resolver,
                hdrpBridge,
                zoneStreamingBinder);
            HybridEnvironmentDebugOverlay overlay =
                GetOrAdd<HybridEnvironmentDebugOverlay>(composition);
            overlay.ConfigureForAuthoring(source, resolver, hdrpBridge);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(manager.volumeHDRP);
            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(zoneStreamingBinder);
            EditorUtility.SetDirty(zoneCellCatalog);
            EditorUtility.SetDirty(resolver);
            EditorUtility.SetDirty(hdrpBridge);
            EditorUtility.SetDirty(zoneRemoval);
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(overlay);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException("Bootstrap scene save failed.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);
            return "Hybrid migration applied with the expanded streamed building catalog. " +
                   "Run both environment validators and complete only the documented " +
                   "dynamic portal gaps.";
        }

        private static WeatherZoneCellCatalog EnsureZoneCellCatalog(
            WeatherZoneProfile closedProfile,
            WeatherZoneProfile shelterProfile)
        {
            WeatherZoneCellCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeatherZoneCellCatalog>(
                    ZoneCellCatalogPath);
            if (catalog == null)
            {
                string directory = Path.GetDirectoryName(ZoneCellCatalogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                catalog = ScriptableObject.CreateInstance<WeatherZoneCellCatalog>();
                catalog.name = "WeatherZoneCellCatalog";
                AssetDatabase.CreateAsset(catalog, ZoneCellCatalogPath);
            }

            catalog.ConfigureForAuthoring(
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_0.teimo.shop.v1",
                    "Teimo shop interior",
                    TeimoCellScenePath,
                    new Vector3(-1377.4739f, 6.45f, 142.78076f),
                    new Quaternion(
                        -0.62024206f,
                        0.33955848f,
                        0.33955857f,
                        0.6202417f),
                    new Vector3(6.6733313f, 10.67478f, 3.60f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_0.teimo.pub.v1",
                    "Teimo pub interior",
                    TeimoCellScenePath,
                    new Vector3(-1380.2303f, 6.45f, 142.82202f),
                    new Quaternion(
                        -0.19847305f,
                        0.6786816f,
                        0.67868125f,
                        0.19847289f),
                    new Vector3(6.251738f, 9.712752f, 3.60f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_3_-1.fleetari.workshop.v1",
                    "Fleetari workshop interior",
                    FleetariCellScenePath,
                    new Vector3(1732.8201f, 8.75f, -307.565f),
                    new Quaternion(
                        -0.15141028f,
                        0.6907061f,
                        0.69070613f,
                        0.15141031f),
                    new Vector3(17.05844f, 21.653284f, 5.40f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_0_-1.cabin.interior.v1",
                    "Lake cabin interior",
                    CabinCellScenePath,
                    new Vector3(4.37427f, -1.219851f, -19.98401f),
                    new Quaternion(
                        -0.7070147f,
                        -0.011417751f,
                        -0.011417752f,
                        0.70701456f),
                    new Vector3(4.60f, 3.10f, 2.10f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_0_0.cabin.shed.v1",
                    "Lake cabin shed shelter",
                    CabinShedCellScenePath,
                    new Vector3(0.112701f, -1.20f, 0.059082f),
                    new Quaternion(
                        0.11463707f,
                        0.6977525f,
                        0.69775224f,
                        -0.114636876f),
                    new Vector3(4.50f, 3.00f, 2.40f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-2_-2.cottage.interior.v1",
                    "Island cottage interior",
                    CottageCellScenePath,
                    new Vector3(-678.70026f, 0.05f, -533.81104f),
                    new Quaternion(
                        -0.10262026f,
                        0.6996206f,
                        0.6996207f,
                        0.1026203f),
                    new Vector3(7.00f, 9.00f, 2.60f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_0.rowhouse.apartment.v1",
                    "Perajarvi rowhouse apartment interior",
                    TeimoCellScenePath,
                    new Vector3(-1117.8445f, 4.20f, 39.125122f),
                    new Quaternion(
                        -0.34436122f,
                        0.61758834f,
                        0.61758864f,
                        0.3443607f),
                    new Vector3(9.00f, 13.00f, 2.20f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_1_0.dancehall.shelter.v1",
                    "Dance pavilion shelter",
                    DancehallCellScenePath,
                    new Vector3(624.4209f, 16.20f, 277.5377f),
                    new Quaternion(
                        0.49999282f,
                        -0.50000525f,
                        0.500007f,
                        0.499995f),
                    new Vector3(15.60f, 19.50f, 3.00f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-1_-5.jail.interior.v1",
                    "Jail cell interior",
                    JailCellScenePath,
                    new Vector3(-485.32f, 7.20f, -2194.0352f),
                    new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f),
                    new Vector3(4.9425206f, 6.1726527f, 3.25f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_2_-1.abandoned_house.west.v1",
                    "Abandoned house west shelter",
                    AbandonedWestCellScenePath,
                    new Vector3(1530.33f, 14.80f, -233.61212f),
                    new Quaternion(
                        -0.3036791f,
                        0.6385758f,
                        0.6385758f,
                        0.3036791f),
                    new Vector3(2.5000021f, 2.5000007f, 2.60f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_2_-1.abandoned_house.south.v1",
                    "Abandoned house south shelter",
                    AbandonedWestCellScenePath,
                    new Vector3(1532.8782f, 14.80f, -242.01532f),
                    new Quaternion(
                        -0.3036791f,
                        0.6385758f,
                        0.6385758f,
                        0.30367908f),
                    new Vector3(2.50f, 3.0000007f, 2.60f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_3_-1.abandoned_house.main.v1",
                    "Abandoned house main shelter",
                    FleetariCellScenePath,
                    new Vector3(1538.01f, 14.80f, -240.90503f),
                    new Quaternion(
                        -0.30367902f,
                        0.6385759f,
                        0.6385757f,
                        0.30367905f),
                    new Vector3(6.0000005f, 19.50f, 2.60f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_0.inspection.hall.v1",
                    "Inspection hall interior",
                    TeimoCellScenePath,
                    new Vector3(-1351.1182f, 6.55f, 218.78186f),
                    new Quaternion(
                        0.37548485f,
                        0.5991751f,
                        0.59917533f,
                        -0.3754854f),
                    new Vector3(42.57523f, 15.707606f, 4.00f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_0.inspection.office.v1",
                    "Inspection office interior",
                    TeimoCellScenePath,
                    new Vector3(-1361.4513f, 6.55f, 228.3429f),
                    new Quaternion(
                        -0.15817289f,
                        0.68918866f,
                        0.68918926f,
                        0.15817252f),
                    new Vector3(23.046154f, 16.99217f, 4.00f),
                    250,
                    true,
                    closedProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_4_-3.factory.machine_hall.v1",
                    "Factory machine hall shelter",
                    FactoryCellScenePath,
                    new Vector3(2308.372f, 4.20f, -1511.4948f),
                    new Quaternion(
                        -0.70710593f,
                        0.0012539818f,
                        0.0012539219f,
                        0.7071054f),
                    new Vector3(7.716518f, 18.880304f, 3.00f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-2_0.farm.machine_hall.v1",
                    "Farm machine hall shelter",
                    FarmCellScenePath,
                    new Vector3(-679.6721f, 6.69f, 318.30054f),
                    new Quaternion(
                        -0.44186738f,
                        0.4667721f,
                        0.5537591f,
                        0.5293655f),
                    new Vector3(7.716517f, 18.880302f, 4.20f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_-3_-4.strawberry.machine_hall.v1",
                    "Strawberry field machine hall shelter",
                    StrawberryCellScenePath,
                    new Vector3(-1082.9297f, 5.08f, -1640.3057f),
                    new Quaternion(
                        -0.6588833f,
                        0.25665745f,
                        0.25665763f,
                        0.65888286f),
                    new Vector3(7.716518f, 18.880304f, 4.20f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_0_-3.yard.machine_hall.v1",
                    "Home yard machine hall shelter",
                    HomeCellScenePath,
                    new Vector3(224.95195f, 3.36f, -1120.7959f),
                    new Quaternion(
                        -0.7050418f,
                        -0.054003656f,
                        0.069369115f,
                        0.70369565f),
                    new Vector3(7.7165165f, 18.880299f, 4.20f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_3_-1.bridge.dirt.shelter.v1",
                    "Dirt-road bridge shelter",
                    FleetariCellScenePath,
                    new Vector3(1606.0381f, -1.1989537f, -291.53296f),
                    new Quaternion(
                        -0.50755906f,
                        0.4924195f,
                        0.4967043f,
                        0.5031816f),
                    new Vector3(2.10f, 14.00f, 2.40f),
                    150,
                    false,
                    shelterProfile),
                WeatherZoneCellDefinition.CreateForAuthoring(
                    "weather.zone.cell_3_0.bridge.highway.shelter.v1",
                    "Highway bridge shelter",
                    HighwayBridgeCellScenePath,
                    new Vector3(1615.712f, 8.281064f, 34.90747f),
                    new Quaternion(
                        -0.35601863f,
                        0.6109422f,
                        0.6109425f,
                        0.3560192f),
                    new Vector3(6.00f, 50.00f, 2.40f),
                    150,
                    false,
                    shelterProfile));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ConvertMeasuredShelters(
            Scene scene,
            WeatherZoneRegistry registry,
            WeatherZoneProfile profile)
        {
            ProductionShelterVolumeAuthoring[] shelters =
                FindAll<ProductionShelterVolumeAuthoring>(scene);
            for (int index = 0; index < shelters.Length; index++)
            {
                ProductionShelterVolumeAuthoring shelter = shelters[index];
                if (!shelter.TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure))
                {
                    Debug.LogWarning(failure, shelter);
                    continue;
                }

                WeatherZone zone = shelter.GetComponent<WeatherZone>();
                if (zone == null)
                {
                    zone = Undo.AddComponent<WeatherZone>(shelter.gameObject);
                }

                if (string.Equals(
                        volume.StableId,
                        HomeHouseShelterStableId,
                        StringComparison.Ordinal))
                {
                    ConfigureCompoundHomeHouseZone(
                        shelter,
                        zone,
                        registry,
                        profile);
                    continue;
                }

                BoxCollider collider = shelter.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<BoxCollider>(shelter.gameObject);
                }
                collider.isTrigger = true;
                collider.center = shelter.transform.InverseTransformPoint(
                    volume.Center);
                Vector3 scale = shelter.transform.lossyScale;
                collider.size = new Vector3(
                    volume.Extents.x * 2f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    volume.Extents.y * 2f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    volume.Extents.z * 2f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
                zone.ConfigureForAuthoring(
                    volume.StableId,
                    profile,
                    100,
                    collider);
                registry.Register(zone);

                Transform fogTransform = FindOrCreateChild(
                    shelter.transform,
                    "HDRP_FogVoid");
                fogTransform.position = volume.Center;
                LocalVolumetricFog localFog =
                    GetOrAdd<LocalVolumetricFog>(fogTransform.gameObject);
                WeatherFogVoidAuthoring fogAuthoring =
                    GetOrAdd<WeatherFogVoidAuthoring>(fogTransform.gameObject);
                fogAuthoring.ConfigureForAuthoring(
                    zone,
                    volume.Extents * 2f,
                    profile.FogVoidBlendDistanceMeters);
                EditorUtility.SetDirty(zone);
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(localFog);
                EditorUtility.SetDirty(fogAuthoring);
            }
        }

        private static void ConfigureCompoundHomeHouseZone(
            ProductionShelterVolumeAuthoring shelter,
            WeatherZone zone,
            WeatherZoneRegistry registry,
            WeatherZoneProfile profile)
        {
            BoxCollider legacyCollider = shelter.GetComponent<BoxCollider>();
            if (legacyCollider != null)
            {
                Undo.RecordObject(
                    legacyCollider,
                    "Disable superseded home weather-zone bounds");
                legacyCollider.enabled = false;
                EditorUtility.SetDirty(legacyCollider);
            }

            Transform legacyFog = shelter.transform.Find("HDRP_FogVoid");
            if (legacyFog != null)
            {
                Undo.RecordObject(
                    legacyFog.gameObject,
                    "Disable superseded home fog void");
                legacyFog.gameObject.SetActive(false);
                EditorUtility.SetDirty(legacyFog.gameObject);
            }

            var colliders = new Collider[HomeHouseVolumes.Length];
            for (int index = 0; index < HomeHouseVolumes.Length; index++)
            {
                CompoundInteriorVolume definition = HomeHouseVolumes[index];
                Transform volumeTransform = FindOrCreateChild(
                    shelter.transform,
                    "WeatherZoneVolume_" + (index + 1).ToString("D2"));
                Undo.RecordObject(
                    volumeTransform,
                    "Configure compound home weather volume");
                volumeTransform.SetPositionAndRotation(
                    definition.WorldCenter,
                    definition.WorldRotation);
                volumeTransform.localScale = Vector3.one;

                BoxCollider collider =
                    GetOrAdd<BoxCollider>(volumeTransform.gameObject);
                Undo.RecordObject(
                    collider,
                    "Configure compound home weather collider");
                collider.enabled = true;
                collider.isTrigger = true;
                collider.center = Vector3.zero;
                collider.size = definition.SizeMeters;
                colliders[index] = collider;

                Transform fogTransform = FindOrCreateChild(
                    volumeTransform,
                    "HDRP_FogVoid");
                Undo.RecordObject(
                    fogTransform,
                    "Configure compound home fog void");
                fogTransform.localPosition = Vector3.zero;
                fogTransform.localRotation = Quaternion.identity;
                fogTransform.localScale = Vector3.one;
                LocalVolumetricFog localFog =
                    GetOrAdd<LocalVolumetricFog>(fogTransform.gameObject);
                WeatherFogVoidAuthoring fogAuthoring =
                    GetOrAdd<WeatherFogVoidAuthoring>(fogTransform.gameObject);
                fogAuthoring.ConfigureForAuthoring(
                    zone,
                    definition.SizeMeters,
                    profile.FogVoidBlendDistanceMeters);

                EditorUtility.SetDirty(volumeTransform);
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(localFog);
                EditorUtility.SetDirty(fogAuthoring);
            }

            zone.ConfigureForAuthoring(
                HomeHouseShelterStableId,
                profile,
                100,
                colliders);
            registry.Register(zone);
            EditorUtility.SetDirty(zone);
        }

        private readonly struct CompoundInteriorVolume
        {
            public CompoundInteriorVolume(
                Vector3 worldCenter,
                Quaternion worldRotation,
                Vector3 sizeMeters)
            {
                WorldCenter = worldCenter;
                WorldRotation = worldRotation;
                SizeMeters = sizeMeters;
            }

            public Vector3 WorldCenter { get; }
            public Quaternion WorldRotation { get; }
            public Vector3 SizeMeters { get; }
        }

        private static VolumeProfile EnsureEnviroSkyProfile(VolumeProfile source)
        {
            VolumeProfile profile = LoadOrCreateVolumeProfile(
                EnviroSkyProfilePath,
                "ProductionEnviroSkyVolume");
            CopyOrConfigure<VisualEnvironment>(
                source,
                profile,
                value =>
                {
                    value.skyType.Override(990);
                    value.cloudType.Override(0);
                });
            CopyOrConfigure<EnviroHDRPSky>(source, profile, value => { });
            RemoveIfPresent<Fog>(profile);
            RemoveIfPresent<Exposure>(profile);
            RemoveIfPresent<IndirectLightingController>(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile EnsureNativeHdrpProfile(VolumeProfile source)
        {
            VolumeProfile profile = LoadOrCreateVolumeProfile(
                NativeHdrpProfilePath,
                "ProductionNativeHDRPVolume");
            CopyOrConfigure<Fog>(source, profile, value =>
            {
                value.active = true;
                value.enabled.Override(true);
                value.enableVolumetricFog.Override(true);
            });
            CopyOrConfigure<Exposure>(source, profile, value =>
            {
                value.active = true;
                value.mode.Override(ExposureMode.Fixed);
                value.fixedExposure.Override(
                    NativeHdrpExposureMath
                        .FinnishSummerDaylightFixedExposureEv);
                value.compensation.Override(0f);
            });
            CopyOrConfigure<IndirectLightingController>(source, profile, value =>
            {
                value.active = true;
            });
            CopyOptional<Tonemapping>(source, profile);
            CopyOptional<ColorAdjustments>(source, profile);
            CopyOptional<Bloom>(source, profile);
            CopyOptional<WhiteBalance>(source, profile);
            RemoveIfPresent<EnviroHDRPSky>(profile);
            RemoveIfPresent<VisualEnvironment>(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static HybridWeatherRenderProfile EnsureRenderProfile()
        {
            HybridWeatherRenderProfile profile =
                AssetDatabase.LoadAssetAtPath<HybridWeatherRenderProfile>(
                    RenderProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    HybridWeatherRenderProfile>();
                AssetDatabase.CreateAsset(profile, RenderProfilePath);
            }

            profile.ConfigureForAuthoring(
                HybridWeatherRenderSettings.Default,
                new[]
                {
                    HybridWeatherRenderSettings.Create(
                        "weather.partly_cloudy", new Color(0.9f, 0.94f, 0.98f),
                        0f, 90f, 6000f, 0.02f, Color.white, 0f, 0.95f,
                        new Color(0.95f, 0.98f, 1f),
                        -8f, -1f, -5f, -12f),
                    HybridWeatherRenderSettings.Create(
                        "weather.overcast", new Color(0.8f, 0.86f, 0.9f),
                        0f, 90f, 5000f, 0.05f, Color.white, -0.2f, 0.8f,
                        new Color(0.97f, 0.985f, 1f),
                        -4f, 0f, -8f, -15f),
                    HybridWeatherRenderSettings.Create(
                        "weather.drizzle", new Color(0.72f, 0.8f, 0.84f),
                        0f, 80f, 3500f, 0.1f, Color.white, -0.3f, 0.75f,
                        new Color(0.92f, 0.96f, 1f),
                        -12f, -2f, -9f, -17f),
                    HybridWeatherRenderSettings.Create(
                        "weather.steady_rain", new Color(0.68f, 0.76f, 0.81f),
                        0f, 75f, 2800f, 0.12f, Color.white, -0.42f, 0.65f,
                        new Color(0.9f, 0.95f, 1f),
                        -16f, -3f, -10f, -18f),
                    HybridWeatherRenderSettings.Create(
                        "weather.heavy_rain", new Color(0.62f, 0.7f, 0.76f),
                        0f, 70f, 2200f, 0.15f, Color.white, -0.55f, 0.55f,
                        new Color(0.88f, 0.94f, 1f),
                        -20f, -4f, -12f, -20f),
                    HybridWeatherRenderSettings.Create(
                        "weather.thunderstorm", new Color(0.52f, 0.6f, 0.68f),
                        0f, 70f, 1600f, 0.2f, Color.white, -0.75f, 0.4f,
                        new Color(0.86f, 0.92f, 1f),
                        -24f, -5f, -14f, -22f),
                    HybridWeatherRenderSettings.Create(
                        "weather.morning_mist", new Color(0.86f, 0.9f, 0.92f),
                        0f, 35f, 900f, 0.25f, Color.white, 0f, 0.8f,
                        new Color(0.96f, 0.98f, 1f),
                        -6f, 0f, -10f, -16f),
                    HybridWeatherRenderSettings.Create(
                        "weather.dense_fog", new Color(0.82f, 0.86f, 0.88f),
                        0f, 45f, 350f, 0.3f, Color.white, -0.15f, 0.65f,
                        new Color(0.98f, 0.99f, 1f),
                        -3f, 0f, -14f, -20f),
                });
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WeatherZoneProfile EnsureClosedZoneProfile()
        {
            WeatherZoneProfile profile = LoadOrCreateZoneProfile(
                ClosedZoneProfilePath,
                "WeatherZone_ClosedInterior");
            profile.ConfigureForAuthoring(
                "environment.zone.closed_interior",
                WeatherZoneKind.ClosedInterior,
                new WeatherExposureState(
                    1f, 1f, 0f, 0.05f, 0.05f, 0.15f, 0.65f, 0f, 1f),
                0.8f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WeatherZoneProfile EnsureShelterZoneProfile()
        {
            WeatherZoneProfile profile = LoadOrCreateZoneProfile(
                ShelterZoneProfilePath,
                "WeatherZone_Shelter");
            profile.ConfigureForAuthoring(
                "environment.zone.shelter",
                WeatherZoneKind.Shelter,
                new WeatherExposureState(
                    0f, 0.9f, 0.1f, 1f, 1f, 1f, 1f, 0f, 0f),
                1.2f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VolumeProfile LoadOrCreateVolumeProfile(
            string path,
            string name)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = name;
                AssetDatabase.CreateAsset(profile, path);
            }

            return profile;
        }

        private static WeatherZoneProfile LoadOrCreateZoneProfile(
            string path,
            string name)
        {
            WeatherZoneProfile profile =
                AssetDatabase.LoadAssetAtPath<WeatherZoneProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WeatherZoneProfile>();
                profile.name = name;
                AssetDatabase.CreateAsset(profile, path);
            }

            return profile;
        }

        private static void CopyOptional<T>(
            VolumeProfile source,
            VolumeProfile destination) where T : VolumeComponent
        {
            if (source.TryGet(out T _))
            {
                CopyOrConfigure<T>(source, destination, value => { });
            }
        }

        private static void CopyOrConfigure<T>(
            VolumeProfile source,
            VolumeProfile destination,
            Action<T> configure) where T : VolumeComponent
        {
            T target = GetOrAdd<T>(destination);
            if (source.TryGet(out T sourceComponent))
            {
                EditorUtility.CopySerialized(sourceComponent, target);
            }

            configure(target);
            EditorUtility.SetDirty(target);
        }

        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>(true);
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component)))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            return component;
        }

        private static void RemoveIfPresent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                return;
            }

            profile.components.Remove(component);
            if (AssetDatabase.Contains(component))
            {
                Object.DestroyImmediate(component, true);
            }
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void RejectVendorAsset(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (path.StartsWith(VendorRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Migration refuses to modify vendor asset: " + path);
            }
        }

        private static Scene OpenBootstrapForInspection(out bool openedHere)
        {
            Scene existing = SceneManager.GetSceneByPath(BootstrapScenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                openedHere = false;
                return existing;
            }

            openedHere = true;
            return EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Additive);
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T[] found = FindAll<T>(scene);
            if (found.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {typeof(T).Name}, found {found.Length}.");
            }

            return found[0];
        }

        private static T FindOptionalSingle<T>(Scene scene) where T : Component
        {
            T[] found = FindAll<T>(scene);
            if (found.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Expected at most one {typeof(T).Name}, found {found.Length}.");
            }

            return found.Length == 1 ? found[0] : null;
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                result.AddRange(roots[index].GetComponentsInChildren<T>(true));
            }

            return result.ToArray();
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
