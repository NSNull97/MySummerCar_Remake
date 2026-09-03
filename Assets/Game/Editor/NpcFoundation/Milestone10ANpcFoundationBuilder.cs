using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Audio.UnityFallback;
using MSC.Bootstrap;
using MSC.Characters;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.NPC;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.NpcFoundation
{
    public static class Milestone10ANpcFoundationBuilder
    {
        private const string ContentRoot =
            "Assets/Game/NPC/Content/Phase1Foundation";
        private const string CharacterCatalogPath = ContentRoot +
            "/CharacterDefinitionCatalog.asset";
        private const string FoundationCatalogPath = ContentRoot +
            "/NpcFoundationCatalog.asset";
        private const string DialogueCatalogPath = ContentRoot +
            "/NpcDialogueCatalog.asset";
        private const string TeimoBicycleRouteManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1TeimoBicycleRouteManifest.json";
        private const string LockedDonorSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";
        private const string StreamingManifestPath =
            "Assets/Game/World/Content/Streaming/" +
            "ProductionWorldStreamingManifest.asset";
        private static readonly Vector3 AuditedSourceToProjectTranslation =
            new Vector3(169.98f, 1.611f, -1040.625f);
        private const string TeimoBicyclePresentationBindingId =
            "presentation.character.teimo-bicycle";
        private const string TeimoStoreToPubRouteId =
            "route.teimo.store-to-pub";
        private const string TeimoStoreToPubScheduleId =
            "schedule.teimo.store-to-pub";
        private const double TeimoStoreToPubStartSecondsOfDay = 72000d;
        // Donor teimo_move_bar root motion is exactly 4.75 real seconds. The
        // project clock advances at the locked 12x service-time rate.
        private const double TeimoStoreToPubDurationGameSeconds = 57d;
        private const double TeimoStoreToPubSourceDurationSeconds = 4.75d;
        private static readonly Vector3 TeimoStorePosition =
            new(-1381.5231f, 5.959f, 142.64197f);
        private static readonly Vector3 TeimoServiceRotationEuler =
            new(0f, 147.4f, 0f);
        private const string JaniCarPresentationBindingId =
            "presentation.character.jani-car";
        private const string PetteriCarPresentationBindingId =
            "presentation.character.petteri-car";
        private const string JaniStoryRouteId =
            "route.story-traffic.jani-review";
        private const string PetteriStoryRouteId =
            "route.story-traffic.petteri-review";
        private const string JaniDepartureRouteId =
            "route.story-traffic.jani-perajarvi-departure";
        private const string PetteriDepartureRouteId =
            "route.story-traffic.petteri-perajarvi-departure";
        private const string JaniRaceRouteId =
            "route.story-traffic.jani-race";
        private const string PetteriRaceRouteId =
            "route.story-traffic.petteri-race";
        private const string JaniDancehallRouteId =
            "route.story-traffic.jani-dancehall-cycle";
        private const string PetteriDancehallRouteId =
            "route.story-traffic.petteri-dancehall-cycle";
        // Donor Jani/Petteri throttle FSM: SpeedMin=115 km/h,
        // SpeedMax=185 km/h. Unloaded coarse simulation uses the midpoint;
        // loaded cars advance only from their physical chassis and vary their
        // target continuously inside the full donor interval.
        private const float StoryTrafficSpeedMetersPerRealSecond = 150f / 3.6f;
        // The locked 61904 formation starts in Perajarvi beside Village
        // waypoints 122/123 and faces toward decreasing waypoint indices.
        // Village 22 -> RoadRace 622 and RoadRace 248 -> Highway 464 are the
        // measured sub-metre route-graph handoffs for the initial departure.
        private const int JaniVillageStartWaypoint = 122;
        private const int PetteriVillageStartWaypoint = 123;
        private const int VillageRoadRaceJunctionWaypoint = 22;
        private const int RoadRaceVillageJunctionWaypoint = 622;
        private const int RoadRaceHighwayJunctionWaypoint = 248;
        private const int HighwayRoadRaceJunctionWaypoint = 464;
        // Locked Navigation FSM 105723/111758. Its initial serialized state is
        // Trackfield 228..290, followed by Village 0..294, RoadRace 0..623,
        // Trackfield 0..201 and repeated 73..201 field laps. Dancehall is a
        // separate 0..268 out / 268..0 return event route.
        private const int TrackFieldInitialStartWaypoint = 228;
        private const int TrackFieldInitialEndWaypoint = 290;
        private const int TrackFieldLoopStartWaypoint = 73;
        private const int TrackFieldLoopEndWaypoint = 201;
        private const int TrackFieldLoopCount = 8;
        private const double StoryTrafficStartSecondsOfDay = 57600d;
        private const double StoryTrafficEndSecondsOfDay = 7200d;
        // Donor GlobalDay 3/5/6/7 = Wednesday/Friday/Saturday/Sunday.
        // Project day index zero is the locked Wednesday 02.08.1995.
        private const int StoryTrafficDonorDayMask = 29;
        private const int StoryTrafficInactiveDayMask = 127 ^
                                                        StoryTrafficDonorDayMask;
        private const string PresentationCatalogPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters/Resources/" +
            "Phase1Characters/CharacterPresentationCatalog.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";

        [MenuItem("Tools/My Summer Car/Phase 1/Build Current NPC Foundation (10B-R3)")]
        public static void BuildFromMenu()
        {
            Build(includePrivatePresentation: true);
            EditorUtility.DisplayDialog(
                "Milestone 10B-R3",
                "NPC catalogs, BetterMSC Suski passenger presentation, story traffic and Bootstrap binding were rebuilt for 10B-R3.",
                "OK");
        }

        public static void BuildFromBatch() =>
            Build(includePrivatePresentation: true);

        public static void BuildCatalogsFromBatch() =>
            Build(includePrivatePresentation: false);

        public static void Build(bool includePrivatePresentation)
        {
            if (includePrivatePresentation)
            {
                Phase1CharacterPresentationImporter.Build();
                Phase1StoryTrafficPresentationImporter.Build();
                Phase1NpcVoiceImporter.Build();
                Phase1StoryTrafficAudioImporter.Build();
            }

            TeimoBicycleRouteManifest teimoBicycle =
                LoadTeimoBicycleRouteManifest();
            LockedTrafficRouteSet trafficRoutes =
                Phase1TrafficRouteEvidence.LoadLockedRouteSet();
            LockedStoryTrafficSpawnEvidence storyTrafficSpawn =
                Phase1TrafficRouteEvidence
                    .LoadLockedStoryTrafficSpawnEvidence();
            ProductionWorldStreamingManifest streamingManifest =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    StreamingManifestPath) ??
                throw new InvalidOperationException(
                    "The production world streaming manifest is missing.");

            EnsureFolder(ContentRoot);
            CharacterDefinitionCatalog characters =
                LoadOrCreate<CharacterDefinitionCatalog>(CharacterCatalogPath);
            characters.ConfigureForAuthoring(
                "characters.phase1.foundation.v3",
                CreateCharacters());
            EditorUtility.SetDirty(characters);

            NpcFoundationCatalog foundation =
                LoadOrCreate<NpcFoundationCatalog>(FoundationCatalogPath);
            foundation.ConfigureForAuthoring(
                "npc.foundation.phase1.v3",
                CreateAnchors(
                    teimoBicycle,
                    trafficRoutes,
                    storyTrafficSpawn,
                    streamingManifest),
                CreateRoutes(
                    teimoBicycle,
                    trafficRoutes,
                    storyTrafficSpawn),
                CreateSchedule(
                    teimoBicycle,
                    trafficRoutes,
                    storyTrafficSpawn));
            EditorUtility.SetDirty(foundation);

            NpcDialogueCatalog dialogue =
                LoadOrCreate<NpcDialogueCatalog>(DialogueCatalogPath);
            dialogue.ConfigureForAuthoring(CreateDialogue());
            EditorUtility.SetDirty(dialogue);

            AssetDatabase.SaveAssets();
            Validate(characters, foundation, dialogue);
            BindBootstrap(characters, foundation, dialogue);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "Milestone 10B-R3 foundation built: the accepted R2 roster plus Jani and Petteri story traffic, with BetterMSC Suski passenger/rescue state switching.");
        }

        private static CharacterDefinition[] CreateCharacters() => new[]
        {
            new CharacterDefinition(
                "character.fixture.stationary-service",
                "P1.NPC.001",
                "Teimo, shop and pub service owner",
                "10a00000000000000000000000000001",
                "presentation.character.fixture.stationary-service",
                "presentation.character.fixture.stationary-service",
                "anchor.fixture.stationary-service",
                "anchor.fixture.stationary-service",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.fixture.scheduled-roaming",
                "P1.NPC.022",
                "10A scheduled roaming fixture (Alpo evidence)",
                "10a00000000000000000000000000002",
                "presentation.character.fixture.scheduled-roaming",
                "presentation.character.fixture.scheduled-roaming",
                "anchor.fixture.roaming-start",
                "anchor.fixture.roaming-end",
                CharacterFixturePattern.ScheduledRoaming,
                isFrameworkFixtureOnly: true),
            new CharacterDefinition(
                "character.fixture.vehicle-linked",
                "P1.NPC.018",
                "10A vehicle-linked fixture (Latanen evidence)",
                "10a00000000000000000000000000003",
                "presentation.character.fixture.vehicle-linked",
                "presentation.character.fixture.vehicle-linked",
                "anchor.fixture.vehicle-linked",
                "anchor.fixture.vehicle-linked",
                CharacterFixturePattern.VehicleLinked,
                isFrameworkFixtureOnly: true,
                isStateOnly: true),
            new CharacterDefinition(
                "character.fleetari",
                "P1.NPC.002",
                "Fleetari, repair-shop owner and service/job contact",
                "10b10000000000000000000000000002",
                "presentation.character.fleetari",
                "presentation.character.fleetari",
                "anchor.fleetari.repair-shop",
                "anchor.fleetari.repair-shop",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.farmer",
                "P1.NPC.007",
                "Toivo Kesseli, hay/combine job client",
                "10b10000000000000000000000000007",
                "presentation.character.farmer",
                "presentation.character.farmer",
                "anchor.farmer.farm",
                "anchor.farmer.farm",
                CharacterFixturePattern.ScheduledRoaming,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.berryman",
                "P1.NPC.008",
                "Strawberry-field job owner",
                "10b10000000000000000000000000008",
                "presentation.character.berryman",
                "presentation.character.berryman",
                "anchor.berryman.strawberry-field",
                "anchor.berryman.strawberry-field",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.uncle-kesseli",
                "P1.NPC.003",
                "Uncle Kesseli, family relationship and vehicle-key contact",
                "10b20000000000000000000000000003",
                "presentation.character.uncle-kesseli",
                "presentation.character.uncle-kesseli",
                "anchor.uncle-kesseli.home",
                "anchor.uncle-kesseli.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.grandmother",
                "P1.NPC.004",
                "Grandmother, family visit and delivery contact",
                "10b20000000000000000000000000004",
                "presentation.character.grandmother",
                "presentation.character.grandmother",
                "anchor.grandmother.home",
                "anchor.grandmother.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.jokke",
                "P1.NPC.005",
                "Jokke, kilju buyer, hiker and story relationship",
                "10b20000000000000000000000000005",
                "presentation.character.jokke",
                "presentation.character.jokke",
                "anchor.jokke.kilju-camp",
                "anchor.jokke.kilju-camp",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.suski",
                "P1.NPC.006",
                "Suski, relationship and passenger state",
                "10b20000000000000000000000000006",
                "presentation.character.suski",
                "presentation.character.suski",
                "anchor.suski.store-hiker",
                "anchor.suski.store-hiker",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.sewage-client-1",
                "P1.NPC.009",
                "Sewage customer 1",
                "10b20000000000000000000000000009",
                "presentation.character.sewage-client-1",
                "presentation.character.sewage-client-1",
                "anchor.sewage-client-1.home",
                "anchor.sewage-client-1.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.sewage-client-2",
                "P1.NPC.010",
                "Sewage customer 2",
                "10b20000000000000000000000000010",
                "presentation.character.sewage-client-2",
                "presentation.character.sewage-client-2",
                "anchor.sewage-client-2.home",
                "anchor.sewage-client-2.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.sewage-client-3",
                "P1.NPC.011",
                "Sewage customer 3",
                "10b20000000000000000000000000011",
                "presentation.character.sewage-client-3",
                "presentation.character.sewage-client-3",
                "anchor.sewage-client-3.home",
                "anchor.sewage-client-3.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.sewage-client-4",
                "P1.NPC.012",
                "Sewage customer 4",
                "10b20000000000000000000000000012",
                "presentation.character.sewage-client-4",
                "presentation.character.sewage-client-4",
                "anchor.sewage-client-4.home",
                "anchor.sewage-client-4.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.sewage-client-5",
                "P1.NPC.013",
                "Sewage customer 5",
                "10b20000000000000000000000000013",
                "presentation.character.sewage-client-5",
                "presentation.character.sewage-client-5",
                "anchor.sewage-client-5.home",
                "anchor.sewage-client-5.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.firewood-customer",
                "P1.NPC.014",
                "Firewood delivery customer",
                "10b20000000000000000000000000014",
                "presentation.character.firewood-customer",
                "presentation.character.firewood-customer",
                "anchor.firewood-customer.home",
                "anchor.firewood-customer.home",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.inspection-officer",
                "P1.NPC.015",
                "Vehicle inspection officer",
                "10b20000000000000000000000000015",
                "presentation.character.inspection-officer",
                "presentation.character.inspection-officer",
                "anchor.inspection-officer.station",
                "anchor.inspection-officer.station",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.wastewater-attendant",
                "P1.NPC.016",
                "Wastewater facility attendant",
                "10b20000000000000000000000000016",
                "presentation.character.wastewater-attendant",
                "presentation.character.wastewater-attendant",
                "anchor.wastewater-attendant.facility",
                "anchor.wastewater-attendant.facility",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.ventti-pigman",
                "P1.NPC.017",
                "Ventti dealer and Pigman",
                "10b20000000000000000000000000017",
                "presentation.character.ventti-pigman",
                "presentation.character.ventti-pigman",
                "anchor.ventti-pigman.cabin",
                "anchor.ventti-pigman.cabin",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.jani",
                "P1.NPC.049",
                "Jani, story traffic driver with Suski passenger",
                "10b30000000000000000000000000049",
                JaniCarPresentationBindingId,
                JaniCarPresentationBindingId,
                GetStoryTrafficAnchorId("jani", 0),
                GetStoryTrafficAnchorId("jani", 0),
                CharacterFixturePattern.VehicleLinked,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.petteri",
                "P1.NPC.050",
                "Petteri, story traffic driver",
                "10b30000000000000000000000000050",
                PetteriCarPresentationBindingId,
                PetteriCarPresentationBindingId,
                GetStoryTrafficAnchorId("petteri", 0),
                GetStoryTrafficAnchorId("petteri", 0),
                CharacterFixturePattern.VehicleLinked,
                isFrameworkFixtureOnly: false),
            new CharacterDefinition(
                "character.jokke-wife-state",
                "P1.NPC.101",
                "Jokke wife relationship state (non-physical donor role)",
                "10b20000000000000000000000000101",
                "presentation.character.jokke-wife-state",
                "presentation.character.jokke-wife-state",
                "anchor.jokke-wife.state",
                "anchor.jokke-wife.state",
                CharacterFixturePattern.StationaryService,
                isFrameworkFixtureOnly: false,
                isStateOnly: true),
        };

        private static NpcAnchorDefinition[] CreateAnchors(
            TeimoBicycleRouteManifest teimoBicycle,
            LockedTrafficRouteSet trafficRoutes,
            LockedStoryTrafficSpawnEvidence storyTrafficSpawn,
            ProductionWorldStreamingManifest streamingManifest)
        {
            var anchors = new List<NpcAnchorDefinition>
            {
                new NpcAnchorDefinition(
                "anchor.fixture.stationary-service",
                "cell_-3_0",
                TeimoStorePosition,
                TeimoServiceRotationEuler),
            new NpcAnchorDefinition(
                "anchor.fixture.roaming-start",
                "cell_-3_0",
                new Vector3(-1362.72f, 5.564f, 179.475f),
                new Vector3(0f, 90f, 0f)),
            new NpcAnchorDefinition(
                "anchor.fixture.roaming-end",
                "cell_-3_0",
                new Vector3(-1354.72f, 5.564f, 179.475f),
                new Vector3(0f, 270f, 0f)),
            new NpcAnchorDefinition(
                "anchor.fixture.vehicle-linked",
                "cell_-3_0",
                new Vector3(-1375.3416f, 6.713971f, 123.4718f),
                new Vector3(0f, 61.7f, 0f),
                "vehicle.fixture.bus"),
            new NpcAnchorDefinition(
                "anchor.teimo.pub",
                "cell_-3_0",
                new Vector3(-1376.0472f, 5.9590025f, 146.14417f),
                TeimoServiceRotationEuler),
            new NpcAnchorDefinition(
                "anchor.teimo.store-to-pub.turn",
                "cell_-3_0",
                ConvertTeimoStoreToPubRootMotion(
                    new Vector3(-0.305f, 0f, -0.189f)),
                TeimoServiceRotationEuler),
            new NpcAnchorDefinition(
                "anchor.teimo.store-to-pub.pub-door",
                "cell_-3_0",
                ConvertTeimoStoreToPubRootMotion(
                    new Vector3(-6f, 0f, -0.33f)),
                TeimoServiceRotationEuler),
            new NpcAnchorDefinition(
                "anchor.fleetari.repair-shop",
                "cell_3_-1",
                new Vector3(1725.0482f, 6.3119974f, -301.45422f),
                new Vector3(0f, -16.75525f, 0f)),
            new NpcAnchorDefinition(
                "anchor.farmer.farm",
                "cell_-2_0",
                new Vector3(-662.27997f, 3.6009998f, 307.68494f),
                new Vector3(0f, -49.95187f, 0f)),
            new NpcAnchorDefinition(
                "anchor.farmer.target-1",
                "cell_-2_0",
                new Vector3(-663.62f, 3.611f, 308.115f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.farmer.target-2",
                "cell_-2_0",
                new Vector3(-669.92f, 3.511f, 293.31494f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.farmer.target-3",
                "cell_-2_0",
                new Vector3(-688.42f, 3.511f, 272.115f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.farmer.target-4",
                "cell_-2_0",
                new Vector3(-664.22f, 3.511f, 285.81494f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.farmer.target-5",
                "cell_-2_0",
                new Vector3(-638.82f, 2.3709998f, 269.615f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.farmer.target-6",
                "cell_-2_0",
                new Vector3(-627.95f, 2.611f, 231.44495f),
                Vector3.zero),
            new NpcAnchorDefinition(
                "anchor.berryman.strawberry-field",
                "cell_-3_-4",
                new Vector3(-1034.2885f, 2.372983f, -1673.2478f),
                new Vector3(0f, -75.1405f, 0f)),
            new NpcAnchorDefinition(
                "anchor.uncle-kesseli.home",
                "cell_0_-3",
                new Vector3(197.63251f, 0.511f, -1077.05005f),
                new Vector3(0f, -117.5515f, 0f)),
            new NpcAnchorDefinition(
                "anchor.grandmother.home",
                "cell_1_-5",
                new Vector3(630.52356f, 4.68f, -2371.27614f),
                new Vector3(0f, -137.5379f, 0f)),
            new NpcAnchorDefinition(
                "anchor.jokke.kilju-camp",
                "cell_4_-3",
                new Vector3(2111.60719f, 8.821f, -1267.3056f),
                new Vector3(0f, 32.3162f, 0f)),
            new NpcAnchorDefinition(
                "anchor.suski.store-hiker",
                "cell_-3_0",
                new Vector3(-1377.438f, 4.486f, 138.193f),
                new Vector3(0f, 155.5488f, 0f)),
            new NpcAnchorDefinition(
                "anchor.story.suski-rescue-bed",
                "cell_0_-3",
                // Frozen M04A1 parents' bed contact position. The sideways
                // orientation is project-owned presentation for the temporary
                // rigid rescue body; story logic references this stable anchor.
                new Vector3(168.3286f, 1.62f, -1027.5336f),
                new Vector3(0f, 180f, 90f)),
            new NpcAnchorDefinition(
                "anchor.sewage-client-1.home",
                "cell_3_-4",
                new Vector3(2047.42943f, -1.647f, -1828.13461f),
                new Vector3(0f, 78.2371f, 0f)),
            new NpcAnchorDefinition(
                "anchor.sewage-client-2.home",
                "cell_-3_0",
                new Vector3(-1131.75942f, 3.147f, 80.51726f),
                new Vector3(0f, -157.722f, 0f)),
            new NpcAnchorDefinition(
                "anchor.sewage-client-3.home",
                "cell_-3_0",
                new Vector3(-1186.25254f, 4.176f, 173.32578f),
                new Vector3(0f, 172.6513f, 0f)),
            new NpcAnchorDefinition(
                "anchor.sewage-client-4.home",
                "cell_3_-1",
                new Vector3(1708.55321f, 6.419f, -323.39861f),
                new Vector3(0f, -9.792f, 0f)),
            new NpcAnchorDefinition(
                "anchor.sewage-client-5.home",
                "cell_3_-1",
                new Vector3(1753.32647f, 5.574f, -386.04823f),
                new Vector3(0f, -35.0556f, 0f)),
            new NpcAnchorDefinition(
                "anchor.firewood-customer.home",
                "cell_4_-3",
                new Vector3(2087.25037f, 5.901f, -1463.07105f),
                new Vector3(0f, -7.1057f, 0f)),
            new NpcAnchorDefinition(
                "anchor.inspection-officer.station",
                "cell_-3_0",
                new Vector3(-1358.75951f, 4.66108f, 222.25988f),
                new Vector3(0f, 154.1483f, 0f)),
            new NpcAnchorDefinition(
                "anchor.wastewater-attendant.facility",
                "cell_-3_0",
                new Vector3(-1350.88078f, 7.319f, 304.21465f),
                new Vector3(0f, 57.31f, 0f)),
            new NpcAnchorDefinition(
                "anchor.ventti-pigman.cabin",
                "cell_0_-1",
                new Vector3(3.01176f, -2.208f, -21.19f),
                new Vector3(0f, 88.7995f, 0f)),
            new NpcAnchorDefinition(
                "anchor.jokke-wife.state",
                "cell_4_-3",
                new Vector3(2118.83519f, 10.441f, -1265.14236f),
                new Vector3(0f, 133.0352f, 0f)),
            };

            foreach (TeimoBicycleRouteSpec route in teimoBicycle.routes)
            {
                for (int index = 0; index < route.waypoints.Length; index++)
                {
                    Vector3 position = ConvertTeimoBicyclePosition(
                        teimoBicycle,
                        route.waypoints[index]);
                    WorldCellIndex cellIndex =
                        WorldCellMembershipUtility.FromPosition(
                            position,
                            streamingManifest.CellSizeMeters);
                    if (!streamingManifest.TryGetCell(
                            cellIndex,
                            out ProductionWorldCellScene cell))
                    {
                        throw new InvalidOperationException(
                            $"Converted Teimo bicycle waypoint " +
                            $"'{route.id}:{index}' at {position} resolves to " +
                            $"missing streaming cell '{cellIndex.Id}'.");
                    }

                    anchors.Add(new NpcAnchorDefinition(
                        GetTeimoBicycleAnchorId(route, index),
                        cell.CellId,
                        position,
                        Vector3.zero,
                        "vehicle.teimo.bicycle"));
                }
            }

            for (int index = 0;
                 index < teimoBicycle.shopArrival.localWaypoints.Length;
                 index++)
            {
                Vector3 position = ConvertTeimoShopArrivalPosition(
                    teimoBicycle,
                    teimoBicycle.shopArrival.localWaypoints[index]);
                WorldCellIndex cellIndex =
                    WorldCellMembershipUtility.FromPosition(
                        position,
                        streamingManifest.CellSizeMeters);
                if (!streamingManifest.TryGetCell(
                        cellIndex,
                        out ProductionWorldCellScene cell))
                {
                    throw new InvalidOperationException(
                        $"Converted Teimo shop-arrival waypoint {index} at " +
                        $"{position} resolves to missing streaming cell " +
                        $"'{cellIndex.Id}'.");
                }

                anchors.Add(new NpcAnchorDefinition(
                    GetTeimoShopArrivalAnchorId(index),
                    cell.CellId,
                    position,
                    Vector3.zero));
            }

            IReadOnlyList<Vector3> highwayPoints = trafficRoutes
                .RequireRoute("route.traffic.highway")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> villagePoints = trafficRoutes
                .RequireRoute("route.traffic.village")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> roadRacePoints = trafficRoutes
                .RequireRoute("route.traffic.road-race")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> trackFieldPoints = trafficRoutes
                .RequireRoute("route.traffic.track-field")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> dancehallPoints = trafficRoutes
                .RequireRoute("route.traffic.dancehall")
                .ProjectWorldPoints;
            AddTrafficRouteAnchors(
                anchors,
                "highway",
                "vehicle.traffic.highway",
                highwayPoints,
                streamingManifest);
            AddTrafficRouteAnchors(
                anchors,
                "village",
                "vehicle.traffic.village",
                villagePoints,
                streamingManifest);
            AddTrafficRouteAnchors(
                anchors,
                "road-race",
                "vehicle.traffic.road-race",
                roadRacePoints,
                streamingManifest);
            AddTrafficRouteAnchors(
                anchors,
                "track-field",
                "vehicle.traffic.track-field",
                trackFieldPoints,
                streamingManifest);
            AddTrafficRouteAnchors(
                anchors,
                "dancehall",
                "vehicle.traffic.dancehall",
                dancehallPoints,
                streamingManifest);

            // These two aliases preserve the accepted 10B-R3 character-home
            // IDs and current-version saves while restoring the exact donor
            // Perajarvi formation spawn instead of a mid-Highway substitute.
            anchors.Add(CreateStoryTrafficCompatibilityAnchor(
                "jani",
                storyTrafficSpawn.JaniProjectPosition,
                streamingManifest));
            anchors.Add(CreateStoryTrafficCompatibilityAnchor(
                "petteri",
                storyTrafficSpawn.PetteriProjectPosition,
                streamingManifest));

            return anchors.ToArray();
        }

        private static Vector3 ConvertTeimoStoreToPubRootMotion(
            Vector3 donorLocalPosition)
        {
            Vector3 worldPosition = TeimoStorePosition +
                Quaternion.Euler(TeimoServiceRotationEuler) *
                donorLocalPosition;
            // The audited donor root motion has only numerical Y drift. Keep
            // every waypoint on the reviewed shop/pub floor plane.
            worldPosition.y = TeimoStorePosition.y;
            return worldPosition;
        }

        private static NpcRouteDefinition[] CreateRoutes(
            TeimoBicycleRouteManifest teimoBicycle,
            LockedTrafficRouteSet trafficRoutes,
            LockedStoryTrafficSpawnEvidence storyTrafficSpawn)
        {
            var routes = new List<NpcRouteDefinition>
            {
                new NpcRouteDefinition(
                    "route.fixture.roaming",
                    "anchor.fixture.roaming-start",
                    "anchor.fixture.roaming-end",
                    durationGameSeconds: 120d,
                    NpcRouteTraversalMode.PingPong),
                new NpcRouteDefinition(
                    "route.farmer.field-rounds",
                    new[]
                    {
                        "anchor.farmer.target-1",
                        "anchor.farmer.target-2",
                        "anchor.farmer.target-3",
                        "anchor.farmer.target-4",
                        "anchor.farmer.target-5",
                        "anchor.farmer.target-6",
                    },
                    durationGameSeconds: 1800d,
                    NpcRouteTraversalMode.PingPong),
            };

            routes.AddRange(teimoBicycle.routes.Select(route =>
                new NpcRouteDefinition(
                    route.id,
                    Enumerable.Range(0, route.waypoints.Length)
                        .Select(index =>
                            GetTeimoBicycleAnchorId(route, index)),
                    CalculateTeimoBicycleTraversalGameSeconds(
                        teimoBicycle,
                        route),
                    NpcRouteTraversalMode.Once,
                    NpcRouteInterpolationMode.CatmullRom)));
            routes.Add(new NpcRouteDefinition(
                teimoBicycle.shopArrival.routeId,
                Enumerable.Range(
                        0,
                        teimoBicycle.shopArrival.localWaypoints.Length)
                    .Select(GetTeimoShopArrivalAnchorId),
                CalculateTeimoShopArrivalTraversalGameSeconds(
                    teimoBicycle),
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.CatmullRom,
                teimoBicycle.shopArrival.localWaypoints.Select(
                    waypoint => (double)waypoint.timeSeconds /
                                teimoBicycle.shopArrival
                                    .sourceDurationRealSeconds)));
            routes.Add(new NpcRouteDefinition(
                TeimoStoreToPubRouteId,
                new[]
                {
                    "anchor.fixture.stationary-service",
                    "anchor.teimo.store-to-pub.turn",
                    "anchor.teimo.store-to-pub.pub-door",
                    "anchor.teimo.pub",
                },
                TeimoStoreToPubDurationGameSeconds,
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.Linear,
                new[]
                {
                    0d,
                    0.48333332d / TeimoStoreToPubSourceDurationSeconds,
                    4d / TeimoStoreToPubSourceDurationSeconds,
                    1d,
                },
                NpcRouteSurfaceMode.AuthoredHeight));
            IReadOnlyList<Vector3> highwayPoints = trafficRoutes
                .RequireRoute("route.traffic.highway")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> villagePoints = trafficRoutes
                .RequireRoute("route.traffic.village")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> roadRacePoints = trafficRoutes
                .RequireRoute("route.traffic.road-race")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> trackFieldPoints = trafficRoutes
                .RequireRoute("route.traffic.track-field")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> dancehallPoints = trafficRoutes
                .RequireRoute("route.traffic.dancehall")
                .ProjectWorldPoints;
            string[] janiDepartureWaypoints =
                CreateStoryTrafficDepartureWaypointIds(
                    "jani",
                    JaniVillageStartWaypoint);
            string[] petteriDepartureWaypoints =
                CreateStoryTrafficDepartureWaypointIds(
                    "petteri",
                    PetteriVillageStartWaypoint);
            string[] janiWaypoints = CreateFullStoryTrafficWaypointIds(
                "jani",
                highwayPoints.Count,
                JaniVillageStartWaypoint);
            string[] petteriWaypoints = CreateFullStoryTrafficWaypointIds(
                "petteri",
                highwayPoints.Count,
                PetteriVillageStartWaypoint);
            Vector3[] janiFullRoutePositions =
                CreateFullStoryTrafficPositions(
                    villagePoints,
                    roadRacePoints,
                    highwayPoints,
                    JaniVillageStartWaypoint,
                    storyTrafficSpawn.JaniProjectPosition);
            Vector3[] petteriFullRoutePositions =
                CreateFullStoryTrafficPositions(
                    villagePoints,
                    roadRacePoints,
                    highwayPoints,
                    PetteriVillageStartWaypoint,
                    storyTrafficSpawn.PetteriProjectPosition);
            routes.Add(new NpcRouteDefinition(
                JaniDepartureRouteId,
                janiDepartureWaypoints,
                CalculateOpenTrafficTraversalGameSeconds(
                    teimoBicycle,
                    CreateStoryTrafficDeparturePositions(
                        villagePoints,
                        roadRacePoints,
                        highwayPoints,
                        JaniVillageStartWaypoint,
                        storyTrafficSpawn.JaniProjectPosition)),
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.CatmullRom));
            routes.Add(new NpcRouteDefinition(
                PetteriDepartureRouteId,
                petteriDepartureWaypoints,
                CalculateOpenTrafficTraversalGameSeconds(
                    teimoBicycle,
                    CreateStoryTrafficDeparturePositions(
                        villagePoints,
                        roadRacePoints,
                        highwayPoints,
                        PetteriVillageStartWaypoint,
                        storyTrafficSpawn.PetteriProjectPosition)),
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.CatmullRom));
            routes.Add(new NpcRouteDefinition(
                JaniStoryRouteId,
                janiWaypoints,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    janiFullRoutePositions),
                NpcRouteTraversalMode.Loop,
                NpcRouteInterpolationMode.CatmullRom));
            routes.Add(new NpcRouteDefinition(
                PetteriStoryRouteId,
                petteriWaypoints,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    petteriFullRoutePositions),
                NpcRouteTraversalMode.Loop,
                NpcRouteInterpolationMode.CatmullRom));
            string[] janiRaceWaypointIds =
                CreateStoryTrafficRaceWaypointIds(
                    "jani",
                    villagePoints.Count,
                    roadRacePoints.Count);
            string[] petteriRaceWaypointIds =
                CreateStoryTrafficRaceWaypointIds(
                    "petteri",
                    villagePoints.Count,
                    roadRacePoints.Count);
            Vector3[] janiRacePositions = CreateStoryTrafficRacePositions(
                storyTrafficSpawn.JaniProjectPosition,
                villagePoints,
                roadRacePoints,
                trackFieldPoints);
            Vector3[] petteriRacePositions = CreateStoryTrafficRacePositions(
                storyTrafficSpawn.PetteriProjectPosition,
                villagePoints,
                roadRacePoints,
                trackFieldPoints);
            routes.Add(new NpcRouteDefinition(
                JaniRaceRouteId,
                janiRaceWaypointIds,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    janiRacePositions),
                // These are already dense donor road samples. A second
                // Catmull-Rom fit cuts the inside of Trackfield and Teimo's
                // forecourt; linear evaluation preserves the measured lane.
                // The sequence terminates after the eighth Trackfield circuit.
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.Linear,
                CalculateOpenWaypointProgress01(janiRacePositions)));
            routes.Add(new NpcRouteDefinition(
                PetteriRaceRouteId,
                petteriRaceWaypointIds,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    petteriRacePositions),
                NpcRouteTraversalMode.Once,
                NpcRouteInterpolationMode.Linear,
                CalculateOpenWaypointProgress01(petteriRacePositions)));
            string[] janiDancehallWaypointIds =
                CreateDancehallCycleWaypointIds(
                    "jani",
                    dancehallPoints.Count);
            string[] petteriDancehallWaypointIds =
                CreateDancehallCycleWaypointIds(
                    "petteri",
                    dancehallPoints.Count);
            Vector3[] janiDancehallPositions =
                CreateDancehallCyclePositions(
                    storyTrafficSpawn.JaniProjectPosition,
                    dancehallPoints);
            Vector3[] petteriDancehallPositions =
                CreateDancehallCyclePositions(
                    storyTrafficSpawn.PetteriProjectPosition,
                    dancehallPoints);
            routes.Add(new NpcRouteDefinition(
                JaniDancehallRouteId,
                janiDancehallWaypointIds,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    janiDancehallPositions),
                NpcRouteTraversalMode.Loop,
                NpcRouteInterpolationMode.CatmullRom));
            routes.Add(new NpcRouteDefinition(
                PetteriDancehallRouteId,
                petteriDancehallWaypointIds,
                CalculateTrafficTraversalGameSeconds(
                    teimoBicycle,
                    petteriDancehallPositions),
                NpcRouteTraversalMode.Loop,
                NpcRouteInterpolationMode.CatmullRom));
            return routes.ToArray();
        }

        private static NpcScheduleBlock[] CreateSchedule(
            TeimoBicycleRouteManifest teimoBicycle,
            LockedTrafficRouteSet trafficRoutes,
            LockedStoryTrafficSpawnEvidence storyTrafficSpawn)
        {
            IReadOnlyList<Vector3> villagePoints = trafficRoutes
                .RequireRoute("route.traffic.village")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> roadRacePoints = trafficRoutes
                .RequireRoute("route.traffic.road-race")
                .ProjectWorldPoints;
            IReadOnlyList<Vector3> highwayPoints = trafficRoutes
                .RequireRoute("route.traffic.highway")
                .ProjectWorldPoints;
            double janiDepartureEnd = StoryTrafficStartSecondsOfDay +
                CalculateOpenTrafficTraversalGameSeconds(
                    teimoBicycle,
                    CreateStoryTrafficDeparturePositions(
                        villagePoints,
                        roadRacePoints,
                        highwayPoints,
                        JaniVillageStartWaypoint,
                        storyTrafficSpawn.JaniProjectPosition));
            double petteriDepartureEnd = StoryTrafficStartSecondsOfDay +
                CalculateOpenTrafficTraversalGameSeconds(
                    teimoBicycle,
                    CreateStoryTrafficDeparturePositions(
                        villagePoints,
                        roadRacePoints,
                        highwayPoints,
                        PetteriVillageStartWaypoint,
                        storyTrafficSpawn.PetteriProjectPosition));
            var schedule = new List<NpcScheduleBlock>
            {
                new NpcScheduleBlock(
                    "schedule.teimo.shop",
                    "character.fixture.stationary-service",
                    63,
                    36000d,
                    72000d,
                    "anchor.fixture.stationary-service",
                    string.Empty,
                    CharacterActivityState.Working),
                new NpcScheduleBlock(
                    "schedule.teimo.pub",
                    "character.fixture.stationary-service",
                    63,
                    TeimoStoreToPubStartSecondsOfDay +
                    TeimoStoreToPubDurationGameSeconds,
                    7200d,
                    "anchor.teimo.pub",
                    string.Empty,
                    CharacterActivityState.Working),
                new NpcScheduleBlock(
                    TeimoStoreToPubScheduleId,
                    "character.fixture.stationary-service",
                    63,
                    TeimoStoreToPubStartSecondsOfDay,
                    TeimoStoreToPubStartSecondsOfDay +
                    TeimoStoreToPubDurationGameSeconds,
                    "anchor.fixture.stationary-service",
                    TeimoStoreToPubRouteId,
                    CharacterActivityState.Walking),
                new NpcScheduleBlock(
                    "schedule.fixture.scheduled-roaming",
                    "character.fixture.scheduled-roaming",
                    127,
                    0d,
                    86400d,
                    "anchor.fixture.roaming-start",
                    "route.fixture.roaming",
                    CharacterActivityState.Walking),
                new NpcScheduleBlock(
                    "schedule.fleetari.workday",
                    "character.fleetari",
                    31,
                    28800d,
                    57600d,
                    "anchor.fleetari.repair-shop",
                    string.Empty,
                    CharacterActivityState.Working),
                new NpcScheduleBlock(
                    "schedule.farmer.day-work",
                    "character.farmer",
                    127,
                    21600d,
                    72000d,
                    "anchor.farmer.target-1",
                    "route.farmer.field-rounds",
                    CharacterActivityState.Walking),
                new NpcScheduleBlock(
                    "schedule.berryman.open",
                    "character.berryman",
                    127,
                    21600d,
                    43200d,
                    "anchor.berryman.strawberry-field",
                    string.Empty,
                    CharacterActivityState.Working),
                // R2 keeps the event-driven Uncle and Jokke-wife state hidden
                // until their owning story milestones can drive them.
                // The remaining actors use bounded availability baselines so
                // their audited placement/presentation can be reviewed now;
                // later job/service milestones replace these with donor-exact
                // gates without changing character or anchor identity.
                Availability(
                    "grandmother",
                    "character.grandmother",
                    "anchor.grandmother.home",
                    CharacterActivityState.Idle),
                Availability(
                    "jokke",
                    "character.jokke",
                    "anchor.jokke.kilju-camp",
                    CharacterActivityState.Idle),
                Availability(
                    "sewage-client-1",
                    "character.sewage-client-1",
                    "anchor.sewage-client-1.home",
                    CharacterActivityState.Working),
                Availability(
                    "sewage-client-2",
                    "character.sewage-client-2",
                    "anchor.sewage-client-2.home",
                    CharacterActivityState.Working),
                Availability(
                    "sewage-client-3",
                    "character.sewage-client-3",
                    "anchor.sewage-client-3.home",
                    CharacterActivityState.Working),
                Availability(
                    "sewage-client-4",
                    "character.sewage-client-4",
                    "anchor.sewage-client-4.home",
                    CharacterActivityState.Working),
                Availability(
                    "sewage-client-5",
                    "character.sewage-client-5",
                    "anchor.sewage-client-5.home",
                    CharacterActivityState.Working),
                Availability(
                    "firewood-customer",
                    "character.firewood-customer",
                    "anchor.firewood-customer.home",
                    CharacterActivityState.Working),
                Availability(
                    "inspection-officer",
                    "character.inspection-officer",
                    "anchor.inspection-officer.station",
                    CharacterActivityState.Working),
                Availability(
                    "wastewater-attendant",
                    "character.wastewater-attendant",
                    "anchor.wastewater-attendant.facility",
                    CharacterActivityState.Working),
                Availability(
                    "ventti-pigman",
                    "character.ventti-pigman",
                    "anchor.ventti-pigman.cabin",
                    CharacterActivityState.Idle),
                new NpcScheduleBlock(
                    "schedule.story-traffic.jani-perajarvi-departure",
                    "character.jani",
                    StoryTrafficDonorDayMask,
                    StoryTrafficStartSecondsOfDay,
                    janiDepartureEnd,
                    GetStoryTrafficAnchorId("jani", 0),
                    string.Empty,
                    CharacterActivityState.VehicleSeated,
                    JaniCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.jani-review",
                    "character.jani",
                    StoryTrafficDonorDayMask,
                    janiDepartureEnd,
                    StoryTrafficEndSecondsOfDay,
                    GetStoryTrafficAnchorId("jani", 0),
                    string.Empty,
                    CharacterActivityState.VehicleSeated,
                    JaniCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.jani-hidden",
                    "character.jani",
                    StoryTrafficDonorDayMask,
                    StoryTrafficEndSecondsOfDay,
                    StoryTrafficStartSecondsOfDay,
                    GetStoryTrafficAnchorId("jani", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    JaniCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.jani-inactive-days",
                    "character.jani",
                    StoryTrafficInactiveDayMask,
                    StoryTrafficEndSecondsOfDay,
                    86400d,
                    GetStoryTrafficAnchorId("jani", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    JaniCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.jani-inactive-early",
                    "character.jani",
                    64,
                    0d,
                    StoryTrafficEndSecondsOfDay,
                    GetStoryTrafficAnchorId("jani", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    JaniCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.petteri-perajarvi-departure",
                    "character.petteri",
                    StoryTrafficDonorDayMask,
                    StoryTrafficStartSecondsOfDay,
                    petteriDepartureEnd,
                    GetStoryTrafficAnchorId("petteri", 0),
                    string.Empty,
                    CharacterActivityState.VehicleSeated,
                    PetteriCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.petteri-review",
                    "character.petteri",
                    StoryTrafficDonorDayMask,
                    petteriDepartureEnd,
                    StoryTrafficEndSecondsOfDay,
                    GetStoryTrafficAnchorId("petteri", 0),
                    string.Empty,
                    CharacterActivityState.VehicleSeated,
                    PetteriCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.petteri-hidden",
                    "character.petteri",
                    StoryTrafficDonorDayMask,
                    StoryTrafficEndSecondsOfDay,
                    StoryTrafficStartSecondsOfDay,
                    GetStoryTrafficAnchorId("petteri", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    PetteriCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.petteri-inactive-days",
                    "character.petteri",
                    StoryTrafficInactiveDayMask,
                    StoryTrafficEndSecondsOfDay,
                    86400d,
                    GetStoryTrafficAnchorId("petteri", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    PetteriCarPresentationBindingId),
                new NpcScheduleBlock(
                    "schedule.story-traffic.petteri-inactive-early",
                    "character.petteri",
                    64,
                    0d,
                    StoryTrafficEndSecondsOfDay,
                    GetStoryTrafficAnchorId("petteri", 0),
                    string.Empty,
                    CharacterActivityState.Hidden,
                    PetteriCarPresentationBindingId),
            };

            schedule.AddRange(teimoBicycle.routes.Select(route =>
            {
                double duration = CalculateTeimoBicycleTraversalGameSeconds(
                    teimoBicycle,
                    route);
                return new NpcScheduleBlock(
                    route.scheduleBlockId,
                    "character.fixture.stationary-service",
                    route.dayMask,
                    route.startSecondsOfDay,
                    route.startSecondsOfDay + duration,
                    GetTeimoBicycleAnchorId(route, 0),
                    route.id,
                    CharacterActivityState.VehicleSeated,
                    TeimoBicyclePresentationBindingId);
            }));

            TeimoBicycleRouteSpec routeToStore = teimoBicycle.routes.Single(
                route => string.Equals(
                    route.id,
                    "route.teimo.bicycle-to-store",
                    StringComparison.Ordinal));
            double arrivalStart = routeToStore.startSecondsOfDay +
                                  CalculateTeimoBicycleTraversalGameSeconds(
                                      teimoBicycle,
                                      routeToStore);
            double arrivalEnd = arrivalStart +
                                CalculateTeimoShopArrivalTraversalGameSeconds(
                                    teimoBicycle);
            schedule.Add(new NpcScheduleBlock(
                teimoBicycle.shopArrival.scheduleBlockId,
                "character.fixture.stationary-service",
                routeToStore.dayMask,
                arrivalStart,
                arrivalEnd,
                GetTeimoShopArrivalAnchorId(0),
                teimoBicycle.shopArrival.routeId,
                CharacterActivityState.Walking));
            schedule.Add(new NpcScheduleBlock(
                teimoBicycle.shopArrival.waitingScheduleBlockId,
                "character.fixture.stationary-service",
                routeToStore.dayMask,
                arrivalEnd,
                36000d,
                "anchor.fixture.stationary-service",
                string.Empty,
                CharacterActivityState.Idle));
            return schedule.ToArray();
        }

        private static NpcScheduleBlock Availability(
            string suffix,
            string characterId,
            string anchorId,
            CharacterActivityState activityState) =>
            new NpcScheduleBlock(
                "schedule.r2-review." + suffix,
                characterId,
                127,
                0d,
                86400d,
                anchorId,
                string.Empty,
                activityState);

        private static NpcDialogueDefinition[] CreateDialogue()
        {
            var definitions = new List<NpcDialogueDefinition>
            {
                Dialogue(
                    "dialogue.fixture.alpo",
                    "character.fixture.scheduled-roaming",
                    Line(
                        "line.alpo.foundation",
                        "npc.alpo.foundation",
                        "Альпо кивает и продолжает свой путь.",
                        "audio.npc.alpo.foundation",
                        "event.npc.alpo.dialogue-selected")),
                Dialogue(
                    "dialogue.fixture.latanen",
                    "character.fixture.vehicle-linked",
                    Line(
                        "line.latanen.foundation",
                        "npc.latanen.foundation",
                        "Водитель занят рейсом.",
                        "audio.npc.latanen.foundation",
                        "event.npc.latanen.dialogue-selected")),
            };

            definitions.AddRange(
                Phase1NpcVoiceImporter.LoadDialogueSpecs()
                    .GroupBy(
                        spec => spec.CharacterDefinitionId,
                        StringComparer.Ordinal)
                    .Select(group => new NpcDialogueDefinition(
                        GetDialogueId(group.Key),
                        group.Key,
                        group.Select(CreateVoiceLine))));
            definitions.AddRange(CreateR2IdentityDialogues());
            return definitions.ToArray();
        }

        private static IEnumerable<NpcDialogueDefinition>
            CreateR2IdentityDialogues()
        {
            yield return IdentityDialogue("uncle-kesseli", "Дядя Кессели");
            yield return IdentityDialogue("grandmother", "Бабушка");
            yield return IdentityDialogue("jokke", "Йокке");
            yield return IdentityDialogue("suski", "Суски");
            yield return IdentityDialogue("sewage-client-1", "Клиент ассенизации №1");
            yield return IdentityDialogue("sewage-client-2", "Клиент ассенизации №2");
            yield return IdentityDialogue("sewage-client-3", "Клиент ассенизации №3");
            yield return IdentityDialogue("sewage-client-4", "Клиент ассенизации №4");
            yield return IdentityDialogue("sewage-client-5", "Клиент ассенизации №5");
            yield return IdentityDialogue("firewood-customer", "Заказчик дров");
            yield return IdentityDialogue("inspection-officer", "Инспектор техосмотра");
            yield return IdentityDialogue("wastewater-attendant", "Работник очистных сооружений");
            yield return IdentityDialogue("ventti-pigman", "Игрок в вентти");
            yield return IdentityDialogue("jokke-wife-state", "Состояние отношений с женой Йокке");
            yield return IdentityDialogue("jani", "Яни");
            yield return IdentityDialogue("petteri", "Петтери");
        }

        private static NpcDialogueDefinition IdentityDialogue(
            string suffix,
            string fallbackSubtitle) =>
            Dialogue(
                "dialogue.r2." + suffix,
                "character." + suffix,
                Line(
                    "line.r2." + suffix + ".identity",
                    "npc.r2." + suffix + ".identity",
                    fallbackSubtitle,
                    "audio.npc.r2." + suffix + ".pending",
                    "event.npc.r2." + suffix + ".interaction-selected"));

        private static string GetDialogueId(string characterDefinitionId) =>
            characterDefinitionId switch
            {
                "character.fixture.stationary-service" =>
                    "dialogue.teimo.service",
                "character.fleetari" => "dialogue.fleetari.service",
                "character.farmer" => "dialogue.farmer.jobs",
                "character.berryman" => "dialogue.berryman.work",
                _ => throw new InvalidOperationException(
                    $"NPC R1 voice manifest contains an unsupported character '{characterDefinitionId}'."),
            };

        private static NpcDialogueLine CreateVoiceLine(
            Phase1NpcVoiceDialogueSpec spec)
        {
            NpcDialogueCondition[] conditions =
                string.IsNullOrEmpty(spec.ScheduleBlockId)
                    ? Array.Empty<NpcDialogueCondition>()
                    : new[]
                    {
                        new NpcDialogueCondition(
                            NpcDialogueConditionKind.ActiveScheduleBlock,
                            spec.ScheduleBlockId),
                    };
            return new NpcDialogueLine(
                spec.LineId,
                spec.LocalizationKey,
                spec.FallbackSubtitle,
                spec.AudioEventId,
                configuredCooldownGameSeconds: 0d,
                conditions,
                spec.EmittedEventId);
        }

        private static NpcDialogueDefinition Dialogue(
            string dialogueId,
            string characterId,
            params NpcDialogueLine[] lines) =>
            new NpcDialogueDefinition(dialogueId, characterId, lines);

        private static NpcDialogueLine Line(
            string lineId,
            string localizationKey,
            string fallbackSubtitle,
            string audioEventId,
            string emittedEventId) =>
            new NpcDialogueLine(
                lineId,
                localizationKey,
                fallbackSubtitle,
                audioEventId,
                configuredCooldownGameSeconds: 20d,
                Array.Empty<NpcDialogueCondition>(),
                emittedEventId);

        private static void Validate(
            CharacterDefinitionCatalog characters,
            NpcFoundationCatalog foundation,
            NpcDialogueCatalog dialogue)
        {
            string[] failures = characters.ValidateConfiguration()
                .Concat(foundation.ValidateConfiguration(characters))
                .Concat(dialogue.ValidateConfiguration(characters))
                .ToArray();
            if (failures.Length > 0)
            {
                throw new InvalidOperationException(
                    "Generated 10B-R3 catalogs are invalid: " +
                    string.Join(" | ", failures));
            }

            string[] missingDialogue = characters.Definitions
                .Where(character => !dialogue.TryGetForCharacter(
                    character.DefinitionId,
                    out _))
                .Select(character => character.DefinitionId)
                .ToArray();
            if (missingDialogue.Length > 0)
            {
                throw new InvalidOperationException(
                    "Generated 10B-R3 dialogue catalog is incomplete: " +
                    string.Join(", ", missingDialogue));
            }
        }

        private static void BindBootstrap(
            CharacterDefinitionCatalog characters,
            NpcFoundationCatalog foundation,
            NpcDialogueCatalog dialogue)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            // Opening the scene may run an unused-asset sweep. Reload the
            // private ignored catalog afterwards so its transient editor object
            // cannot become a destroyed/fake-null reference before validation.
            CharacterPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                    PresentationCatalogPath) ??
                throw new InvalidOperationException(
                    "Private Phase 1 character presentation catalog is missing. " +
                    "Run the character presentation importer first.");
            ProductionWorldStreamingInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include) ??
                throw new InvalidOperationException(
                    "Bootstrap scene has no production streaming installer.");
            installer.ConfigureNpcFoundationForAuthoring(
                characters,
                foundation,
                dialogue,
                presentation);
            EditorUtility.SetDirty(installer);
            UnityAudioBackend audioBackend =
                UnityEngine.Object.FindFirstObjectByType<UnityAudioBackend>(
                    FindObjectsInactive.Include) ??
                throw new InvalidOperationException(
                    "Bootstrap scene has no Unity Audio fallback for private Phase 1 NPC voices.");
            audioBackend.ConfigureSupplementalEventLibrariesForAuthoring(
                Phase1NpcVoiceImporter.LoadGeneratedLibrary(),
                Phase1StoryTrafficAudioImporter.LoadGeneratedLibrary());
            EditorUtility.SetDirty(audioBackend);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static TeimoBicycleRouteManifest
            LoadTeimoBicycleRouteManifest()
        {
            string fullPath = Path.GetFullPath(
                TeimoBicycleRouteManifestPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "The project-owned Teimo bicycle route manifest is missing.",
                    fullPath);
            }

            TeimoBicycleRouteManifest manifest =
                JsonUtility.FromJson<TeimoBicycleRouteManifest>(
                    File.ReadAllText(fullPath));
            if (manifest == null || manifest.schemaVersion != 2 ||
                !string.Equals(
                    manifest.sourceSceneSha256,
                    LockedDonorSceneSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                manifest.sourceMoveFsmComponentFileId != 105851L ||
                manifest.sourceSplineMoveComponentFileId != 105852L ||
                !Mathf.Approximately(
                    manifest.sourceSpeedMetersPerRealSecond,
                    4f) ||
                !Mathf.Approximately(
                    manifest.gameSecondsPerRealSecond,
                    12f) ||
                !string.Equals(
                    manifest.sourceCoordinateSpace,
                    "donor-world",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.projectCoordinateSpace,
                    "m04a1-garage-anchored",
                    StringComparison.Ordinal) ||
                manifest.sourceToProjectTranslation == null ||
                manifest.sourceToProjectTranslation.Length != 3 ||
                Vector3.Distance(
                    manifest.SourceToProjectTranslation,
                    AuditedSourceToProjectTranslation) > 0.0001f ||
                manifest.routes == null || manifest.routes.Length != 2 ||
                !IsValidTeimoShopArrival(manifest.shopArrival))
            {
                throw new InvalidOperationException(
                    "The Teimo bicycle route manifest is not the reviewed 10B-R1 source revision.");
            }

            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            var scheduleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TeimoBicycleRouteSpec route in manifest.routes)
            {
                string failure = string.Empty;
                if (route == null ||
                    !CharacterStableId.TryValidate(
                        route.id,
                        "route.teimo.bicycle-",
                        out failure) ||
                    !CharacterStableId.TryValidate(
                        route.scheduleBlockId,
                        "schedule.teimo.bicycle-",
                        out failure) ||
                    !routeIds.Add(route.id) ||
                    !scheduleIds.Add(route.scheduleBlockId) ||
                    route.sourcePathManagerComponentFileId <= 0L ||
                    route.dayMask <= 0 || route.dayMask > 127 ||
                    !double.IsFinite(route.startSecondsOfDay) ||
                    route.startSecondsOfDay < 0d ||
                    route.startSecondsOfDay >= 86400d ||
                    route.waypoints == null ||
                    route.waypoints.Length != 37)
                {
                    throw new InvalidOperationException(
                        $"Teimo bicycle route '{route?.id}' is invalid: {failure}");
                }

                var waypointIds = new HashSet<long>();
                foreach (TeimoBicycleWaypointSpec waypoint in route.waypoints)
                {
                    if (waypoint == null ||
                        waypoint.transformFileId <= 0L ||
                        !waypointIds.Add(waypoint.transformFileId) ||
                        waypoint.position == null ||
                        waypoint.position.Length != 3 ||
                        waypoint.position.Any(value => !float.IsFinite(value)))
                    {
                        throw new InvalidOperationException(
                            $"Teimo bicycle route '{route.id}' has invalid or duplicate waypoint evidence.");
                    }
                }

                double endSeconds = route.startSecondsOfDay +
                    CalculateTeimoBicycleTraversalGameSeconds(
                        manifest,
                        route);
                if (endSeconds <= route.startSecondsOfDay ||
                    endSeconds > 86400d)
                {
                    throw new InvalidOperationException(
                        $"Teimo bicycle route '{route.id}' crosses the game-day boundary unexpectedly.");
                }
            }

            return manifest;
        }

        private static bool IsValidTeimoShopArrival(
            TeimoShopArrivalSpec arrival)
        {
            if (arrival == null ||
                !CharacterStableId.TryValidate(
                    arrival.routeId,
                    "route.teimo.",
                    out _) ||
                !CharacterStableId.TryValidate(
                    arrival.scheduleBlockId,
                    "schedule.teimo.",
                    out _) ||
                !CharacterStableId.TryValidate(
                    arrival.waitingScheduleBlockId,
                    "schedule.teimo.",
                    out _) ||
                string.IsNullOrWhiteSpace(arrival.sourceAnimationRelativePath) ||
                string.IsNullOrWhiteSpace(arrival.sourceAnimationGuid) ||
                string.IsNullOrWhiteSpace(arrival.sourceAnimationSha256) ||
                !float.IsFinite(arrival.sourceDurationRealSeconds) ||
                arrival.sourceDurationRealSeconds <= 0f ||
                arrival.sourceOriginPosition == null ||
                arrival.sourceOriginPosition.Length != 3 ||
                arrival.sourceOriginRotation == null ||
                arrival.sourceOriginRotation.Length != 4 ||
                arrival.localWaypoints == null ||
                arrival.localWaypoints.Length < 3 ||
                arrival.parkedBicycleEntityStableIds == null ||
                arrival.parkedBicycleEntityStableIds.Length != 6 ||
                arrival.parkedBicycleEntityStableIds.Any(
                    string.IsNullOrWhiteSpace) ||
                arrival.parkedBicycleEntityStableIds.Distinct(
                    StringComparer.Ordinal).Count() !=
                arrival.parkedBicycleEntityStableIds.Length ||
                string.IsNullOrWhiteSpace(
                    arrival.serviceDoorEntityStableId) ||
                arrival.serviceDoorPivotPosition == null ||
                arrival.serviceDoorPivotPosition.Length != 3 ||
                arrival.serviceDoorPivotRotation == null ||
                arrival.serviceDoorPivotRotation.Length != 4 ||
                !float.IsFinite(arrival.doorOpenEventRealSeconds) ||
                !float.IsFinite(arrival.doorCloseEventRealSeconds) ||
                !float.IsFinite(arrival.doorOpenAngleDegrees) ||
                !float.IsFinite(arrival.doorOpenAnimationRealSeconds) ||
                !float.IsFinite(arrival.doorCloseAnimationRealSeconds) ||
                arrival.doorOpenEventRealSeconds < 0f ||
                arrival.doorCloseEventRealSeconds <=
                arrival.doorOpenEventRealSeconds ||
                arrival.doorOpenAngleDegrees <= 0f ||
                arrival.doorOpenAnimationRealSeconds <= 0f ||
                arrival.doorCloseAnimationRealSeconds <= 0f)
            {
                return false;
            }

            float previousTime = -1f;
            for (int index = 0; index < arrival.localWaypoints.Length; index++)
            {
                TeimoShopArrivalWaypointSpec waypoint =
                    arrival.localWaypoints[index];
                if (waypoint == null ||
                    !float.IsFinite(waypoint.timeSeconds) ||
                    waypoint.timeSeconds <= previousTime ||
                    waypoint.position == null ||
                    waypoint.position.Length != 3 ||
                    waypoint.position.Any(value => !float.IsFinite(value)))
                {
                    return false;
                }

                previousTime = waypoint.timeSeconds;
            }

            return Mathf.Approximately(
                       arrival.localWaypoints[0].timeSeconds,
                       0f) &&
                   Mathf.Approximately(
                       previousTime,
                       arrival.sourceDurationRealSeconds);
        }

        private static string GetTeimoBicycleAnchorId(
            TeimoBicycleRouteSpec route,
            int waypointIndex) =>
            route.id.Replace("route.", "anchor.") + "." +
            waypointIndex.ToString("D2");

        private static string GetTeimoShopArrivalAnchorId(int waypointIndex) =>
            "anchor.teimo.shop-arrival." + waypointIndex.ToString("D2");

        private static string GetStoryTrafficAnchorId(
            string driver,
            int waypointIndex) =>
            "anchor.story-traffic." + driver + "." +
            waypointIndex.ToString("D3");

        private static string GetTrafficRouteAnchorId(
            string routeName,
            int waypointIndex) =>
            "anchor.traffic." + routeName + "." +
            waypointIndex.ToString("D4");

        private static void AddTrafficRouteAnchors(
            ICollection<NpcAnchorDefinition> anchors,
            string routeName,
            string vehicleId,
            IReadOnlyList<Vector3> points,
            ProductionWorldStreamingManifest streamingManifest)
        {
            for (int index = 0; index < points.Count; index++)
            {
                Vector3 position = points[index];
                WorldCellIndex cellIndex =
                    WorldCellMembershipUtility.FromPosition(
                        position,
                        streamingManifest.CellSizeMeters);
                string availabilityId = ResolveTrafficAvailabilityId(
                    streamingManifest,
                    cellIndex);

                anchors.Add(new NpcAnchorDefinition(
                    GetTrafficRouteAnchorId(routeName, index),
                    availabilityId,
                    position,
                    Vector3.zero,
                    vehicleId));
            }
        }

        private static NpcAnchorDefinition
            CreateStoryTrafficCompatibilityAnchor(
                string driver,
                Vector3 position,
                ProductionWorldStreamingManifest streamingManifest)
        {
            WorldCellIndex cellIndex =
                WorldCellMembershipUtility.FromPosition(
                    position,
                    streamingManifest.CellSizeMeters);
            string availabilityId = ResolveTrafficAvailabilityId(
                streamingManifest,
                cellIndex);

            return new NpcAnchorDefinition(
                GetStoryTrafficAnchorId(driver, 0),
                availabilityId,
                position,
                Vector3.zero,
                "vehicle.story-traffic." + driver + "-car");
        }

        private static string ResolveTrafficAvailabilityId(
            ProductionWorldStreamingManifest streamingManifest,
            WorldCellIndex cellIndex)
        {
            if (streamingManifest.TryGetCell(
                    cellIndex,
                    out ProductionWorldCellScene cell))
            {
                return cell.CellId;
            }

            ProductionWorldGlobalScene global = streamingManifest
                .GlobalScenes
                .FirstOrDefault(candidate => string.Equals(
                    candidate.SceneId,
                    "global-legacy",
                    StringComparison.Ordinal));
            if (string.IsNullOrEmpty(global.SceneId))
            {
                throw new InvalidOperationException(
                    $"Traffic waypoint resolves to sparse cell " +
                    $"'{cellIndex.Id}', and the global legacy scene is not " +
                    $"available for continuous road ownership.");
            }

            // The donor road/terrain are continuous global-baseline geometry;
            // sparse cell manifests intentionally omit cells that own no
            // separate static object. The moving actor remains stream-safe
            // because its logical state is independent of presentation.
            return global.SceneId;
        }

        private static string[] CreateTrafficRouteWaypointIds(
            string routeName,
            int pointCount,
            int startIndex,
            bool reverse)
        {
            if (pointCount < 2 || startIndex < 0 || startIndex >= pointCount)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            return Enumerable.Range(0, pointCount)
                .Select(offset => reverse
                    ? (startIndex - offset + pointCount) % pointCount
                    : (startIndex + offset) % pointCount)
                .Select(index => GetTrafficRouteAnchorId(
                    routeName,
                    index))
                .ToArray();
        }

        private static string[] CreateStoryTrafficDepartureWaypointIds(
            string driver,
            int villageStartWaypoint)
        {
            var waypointIds = new List<string>
            {
                GetStoryTrafficAnchorId(driver, 0),
            };
            for (int index = villageStartWaypoint - 1;
                 index >= VillageRoadRaceJunctionWaypoint;
                 index--)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "village",
                    index));
            }

            for (int index = RoadRaceVillageJunctionWaypoint;
                 index >= RoadRaceHighwayJunctionWaypoint;
                 index--)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "road-race",
                    index));
            }

            waypointIds.Add(GetTrafficRouteAnchorId(
                "highway",
                HighwayRoadRaceJunctionWaypoint));
            return waypointIds.ToArray();
        }

        private static Vector3[] CreateStoryTrafficDeparturePositions(
            IReadOnlyList<Vector3> villagePoints,
            IReadOnlyList<Vector3> roadRacePoints,
            IReadOnlyList<Vector3> highwayPoints,
            int villageStartWaypoint,
            Vector3 storyDriverStartPosition)
        {
            var positions = new List<Vector3>
            {
                storyDriverStartPosition,
            };
            for (int index = villageStartWaypoint - 1;
                 index >= VillageRoadRaceJunctionWaypoint;
                 index--)
            {
                positions.Add(villagePoints[index]);
            }

            for (int index = RoadRaceVillageJunctionWaypoint;
                 index >= RoadRaceHighwayJunctionWaypoint;
                 index--)
            {
                positions.Add(roadRacePoints[index]);
            }

            positions.Add(highwayPoints[HighwayRoadRaceJunctionWaypoint]);
            return positions.ToArray();
        }

        private static string[] CreateFullStoryTrafficWaypointIds(
            string driver,
            int highwayPointCount,
            int villageStartWaypoint)
        {
            var waypointIds = new List<string>
            {
                GetStoryTrafficAnchorId(driver, 0),
            };
            for (int index = villageStartWaypoint - 1;
                 index >= VillageRoadRaceJunctionWaypoint;
                 index--)
            {
                waypointIds.Add(GetTrafficRouteAnchorId("village", index));
            }

            for (int index = RoadRaceVillageJunctionWaypoint;
                 index >= RoadRaceHighwayJunctionWaypoint;
                 index--)
            {
                waypointIds.Add(GetTrafficRouteAnchorId("road-race", index));
            }

            waypointIds.AddRange(CreateTrafficRouteWaypointIds(
                "highway",
                highwayPointCount,
                HighwayRoadRaceJunctionWaypoint,
                reverse: true));
            waypointIds.Add(GetTrafficRouteAnchorId(
                "highway",
                HighwayRoadRaceJunctionWaypoint));
            for (int index = RoadRaceHighwayJunctionWaypoint + 1;
                 index <= RoadRaceVillageJunctionWaypoint;
                 index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId("road-race", index));
            }

            for (int index = VillageRoadRaceJunctionWaypoint;
                 index <= villageStartWaypoint;
                 index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId("village", index));
            }

            return waypointIds.ToArray();
        }

        private static Vector3[] CreateFullStoryTrafficPositions(
            IReadOnlyList<Vector3> villagePoints,
            IReadOnlyList<Vector3> roadRacePoints,
            IReadOnlyList<Vector3> highwayPoints,
            int villageStartWaypoint,
            Vector3 storyDriverStartPosition)
        {
            var positions = new List<Vector3>
            {
                storyDriverStartPosition,
            };
            for (int index = villageStartWaypoint - 1;
                 index >= VillageRoadRaceJunctionWaypoint;
                 index--)
            {
                positions.Add(villagePoints[index]);
            }

            for (int index = RoadRaceVillageJunctionWaypoint;
                 index >= RoadRaceHighwayJunctionWaypoint;
                 index--)
            {
                positions.Add(roadRacePoints[index]);
            }

            for (int offset = 0; offset < highwayPoints.Count; offset++)
            {
                int index = (HighwayRoadRaceJunctionWaypoint - offset +
                             highwayPoints.Count) % highwayPoints.Count;
                positions.Add(highwayPoints[index]);
            }

            positions.Add(highwayPoints[HighwayRoadRaceJunctionWaypoint]);
            for (int index = RoadRaceHighwayJunctionWaypoint + 1;
                 index <= RoadRaceVillageJunctionWaypoint;
                 index++)
            {
                positions.Add(roadRacePoints[index]);
            }

            for (int index = VillageRoadRaceJunctionWaypoint;
                 index <= villageStartWaypoint;
                 index++)
            {
                positions.Add(villagePoints[index]);
            }

            return positions.ToArray();
        }

        private static string[] CreateStoryTrafficRaceWaypointIds(
            string driver,
            int villagePointCount,
            int roadRacePointCount)
        {
            if (villagePointCount != 295 || roadRacePointCount != 624)
            {
                throw new InvalidOperationException(
                    "Locked story-traffic race route cardinality changed.");
            }

            var waypointIds = new List<string>(
                1 + (TrackFieldInitialEndWaypoint -
                     TrackFieldInitialStartWaypoint + 1) +
                villagePointCount + roadRacePointCount +
                (TrackFieldLoopEndWaypoint + 1) +
                (TrackFieldLoopEndWaypoint -
                 TrackFieldLoopStartWaypoint + 1) *
                (TrackFieldLoopCount - 1));
            waypointIds.Add(GetStoryTrafficAnchorId(driver, 0));
            for (int index = TrackFieldInitialStartWaypoint;
                 index <= TrackFieldInitialEndWaypoint;
                 index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "track-field",
                    index));
            }

            for (int index = 0; index < villagePointCount; index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "village",
                    index));
            }

            for (int index = 0; index < roadRacePointCount; index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "road-race",
                    index));
            }

            // Trackfield 0..201 contains the drive to the field and the first
            // circuit. The donor New Loop state then sets 73..201 and counts
            // the remaining circuits toward TrackLoopsWanted=8.
            for (int index = 0; index <= TrackFieldLoopEndWaypoint; index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "track-field",
                    index));
            }

            for (int lap = 1; lap < TrackFieldLoopCount; lap++)
            {
                for (int index = TrackFieldLoopStartWaypoint;
                     index <= TrackFieldLoopEndWaypoint;
                     index++)
                {
                    waypointIds.Add(GetTrafficRouteAnchorId(
                        "track-field",
                        index));
                }
            }

            return waypointIds.ToArray();
        }

        private static Vector3[] CreateStoryTrafficRacePositions(
            Vector3 storyDriverStartPosition,
            IReadOnlyList<Vector3> villagePoints,
            IReadOnlyList<Vector3> roadRacePoints,
            IReadOnlyList<Vector3> trackFieldPoints)
        {
            if (trackFieldPoints.Count != 291)
            {
                throw new InvalidOperationException(
                    "Locked Trackfield route cardinality changed.");
            }

            var positions = new List<Vector3>(
                1 + (TrackFieldInitialEndWaypoint -
                     TrackFieldInitialStartWaypoint + 1) +
                villagePoints.Count + roadRacePoints.Count +
                (TrackFieldLoopEndWaypoint + 1) +
                (TrackFieldLoopEndWaypoint -
                 TrackFieldLoopStartWaypoint + 1) *
                (TrackFieldLoopCount - 1));
            positions.Add(storyDriverStartPosition);
            for (int index = TrackFieldInitialStartWaypoint;
                 index <= TrackFieldInitialEndWaypoint;
                 index++)
            {
                positions.Add(trackFieldPoints[index]);
            }

            positions.AddRange(villagePoints);
            positions.AddRange(roadRacePoints);
            for (int index = 0; index <= TrackFieldLoopEndWaypoint; index++)
            {
                positions.Add(trackFieldPoints[index]);
            }

            for (int lap = 1; lap < TrackFieldLoopCount; lap++)
            {
                for (int index = TrackFieldLoopStartWaypoint;
                     index <= TrackFieldLoopEndWaypoint;
                     index++)
                {
                    positions.Add(trackFieldPoints[index]);
                }
            }

            return positions.ToArray();
        }

        private static string[] CreateDancehallCycleWaypointIds(
            string driver,
            int pointCount)
        {
            if (pointCount != 269)
            {
                throw new InvalidOperationException(
                    "Locked Dancehall route cardinality changed.");
            }

            var waypointIds = new List<string>(pointCount * 2 - 1)
            {
                GetStoryTrafficAnchorId(driver, 0),
            };
            for (int index = 0; index < pointCount; index++)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "dancehall",
                    index));
            }

            for (int index = pointCount - 2; index >= 1; index--)
            {
                waypointIds.Add(GetTrafficRouteAnchorId(
                    "dancehall",
                    index));
            }

            return waypointIds.ToArray();
        }

        private static Vector3[] CreateDancehallCyclePositions(
            Vector3 storyDriverStartPosition,
            IReadOnlyList<Vector3> dancehallPoints)
        {
            if (dancehallPoints.Count != 269)
            {
                throw new InvalidOperationException(
                    "Locked Dancehall route cardinality changed.");
            }

            var positions = new List<Vector3>(dancehallPoints.Count * 2 - 1)
            {
                storyDriverStartPosition,
            };
            positions.AddRange(dancehallPoints);
            for (int index = dancehallPoints.Count - 2; index >= 1; index--)
            {
                positions.Add(dancehallPoints[index]);
            }

            return positions.ToArray();
        }

        private static double CalculateTrafficTraversalGameSeconds(
            TeimoBicycleRouteManifest manifest,
            IReadOnlyList<Vector3> points)
        {
            double metres = 0d;
            for (int index = 0; index < points.Count; index++)
            {
                metres += Vector3.Distance(
                    points[index],
                    points[(index + 1) % points.Count]);
            }

            return metres /
                   StoryTrafficSpeedMetersPerRealSecond *
                   manifest.gameSecondsPerRealSecond;
        }

        private static double CalculateOpenTrafficTraversalGameSeconds(
            TeimoBicycleRouteManifest manifest,
            IReadOnlyList<Vector3> points)
        {
            double metres = 0d;
            for (int index = 1; index < points.Count; index++)
            {
                metres += Vector3.Distance(
                    points[index - 1],
                    points[index]);
            }

            return metres /
                   StoryTrafficSpeedMetersPerRealSecond *
                   manifest.gameSecondsPerRealSecond;
        }

        private static double[] CalculateOpenWaypointProgress01(
            IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                throw new ArgumentException(
                    "An open traffic route requires at least two points.",
                    nameof(points));
            }

            var cumulativeDistance = new double[points.Count];
            for (int index = 1; index < points.Count; index++)
            {
                cumulativeDistance[index] = cumulativeDistance[index - 1] +
                    Vector3.Distance(points[index - 1], points[index]);
            }

            double totalDistance = cumulativeDistance[^1];
            if (!double.IsFinite(totalDistance) || totalDistance <= 0.001d)
            {
                throw new InvalidOperationException(
                    "Open traffic route distance is invalid.");
            }

            var progress = new double[points.Count];
            for (int index = 1; index < points.Count; index++)
            {
                progress[index] = cumulativeDistance[index] / totalDistance;
            }

            progress[^1] = 1d;
            return progress;
        }

        private static Vector3 ConvertTeimoBicyclePosition(
            TeimoBicycleRouteManifest manifest,
            TeimoBicycleWaypointSpec waypoint) =>
            waypoint.ToVector3() + manifest.SourceToProjectTranslation;

        private static Vector3 ConvertTeimoShopArrivalPosition(
            TeimoBicycleRouteManifest manifest,
            TeimoShopArrivalWaypointSpec waypoint) =>
            manifest.shopArrival.SourceOriginPosition +
            manifest.shopArrival.SourceOriginRotation *
            waypoint.ToVector3() +
            manifest.SourceToProjectTranslation;

        private static double CalculateTeimoBicycleTraversalGameSeconds(
            TeimoBicycleRouteManifest manifest,
            TeimoBicycleRouteSpec route)
        {
            double metres = 0d;
            for (int index = 1; index < route.waypoints.Length; index++)
            {
                metres += Vector3.Distance(
                    route.waypoints[index - 1].ToVector3(),
                    route.waypoints[index].ToVector3());
            }

            return metres /
                   manifest.sourceSpeedMetersPerRealSecond *
                   manifest.gameSecondsPerRealSecond;
        }

        private static double CalculateTeimoShopArrivalTraversalGameSeconds(
            TeimoBicycleRouteManifest manifest) =>
            manifest.shopArrival.sourceDurationRealSeconds *
            manifest.gameSecondsPerRealSecond;

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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

        [Serializable]
        private sealed class TeimoBicycleRouteManifest
        {
            public int schemaVersion;
            public string sourceSceneSha256 = string.Empty;
            public long sourceMoveFsmComponentFileId;
            public long sourceSplineMoveComponentFileId;
            public float sourceSpeedMetersPerRealSecond;
            public float gameSecondsPerRealSecond;
            public string sourceCoordinateSpace = string.Empty;
            public string projectCoordinateSpace = string.Empty;
            public float[] sourceToProjectTranslation = Array.Empty<float>();
            public TeimoBicycleRouteSpec[] routes =
                Array.Empty<TeimoBicycleRouteSpec>();
            public TeimoShopArrivalSpec shopArrival;

            public Vector3 SourceToProjectTranslation => new Vector3(
                sourceToProjectTranslation[0],
                sourceToProjectTranslation[1],
                sourceToProjectTranslation[2]);
        }

        [Serializable]
        private sealed class TeimoBicycleRouteSpec
        {
            public string id = string.Empty;
            public string scheduleBlockId = string.Empty;
            public long sourcePathManagerComponentFileId;
            public string sourceEvent = string.Empty;
            public int dayMask;
            public double startSecondsOfDay;
            public TeimoBicycleWaypointSpec[] waypoints =
                Array.Empty<TeimoBicycleWaypointSpec>();
        }

        [Serializable]
        private sealed class TeimoBicycleWaypointSpec
        {
            public long transformFileId;
            public float[] position = Array.Empty<float>();

            public Vector3 ToVector3() =>
                new Vector3(position[0], position[1], position[2]);
        }

        [Serializable]
        private sealed class TeimoShopArrivalSpec
        {
            public string routeId = string.Empty;
            public string scheduleBlockId = string.Empty;
            public string waitingScheduleBlockId = string.Empty;
            public string sourceAnimationRelativePath = string.Empty;
            public string sourceAnimationGuid = string.Empty;
            public string sourceAnimationSha256 = string.Empty;
            public float sourceDurationRealSeconds;
            public float[] sourceOriginPosition = Array.Empty<float>();
            public float[] sourceOriginRotation = Array.Empty<float>();
            public TeimoShopArrivalWaypointSpec[] localWaypoints =
                Array.Empty<TeimoShopArrivalWaypointSpec>();
            public string[] parkedBicycleEntityStableIds =
                Array.Empty<string>();
            public string serviceDoorEntityStableId = string.Empty;
            public float[] serviceDoorPivotPosition = Array.Empty<float>();
            public float[] serviceDoorPivotRotation = Array.Empty<float>();
            public string sourceDoorOpenAnimationSha256 = string.Empty;
            public string sourceDoorCloseAnimationSha256 = string.Empty;
            public float doorOpenEventRealSeconds;
            public float doorCloseEventRealSeconds;
            public float doorOpenAngleDegrees;
            public float doorOpenAnimationRealSeconds;
            public float doorCloseAnimationRealSeconds;

            public Vector3 SourceOriginPosition => new Vector3(
                sourceOriginPosition[0],
                sourceOriginPosition[1],
                sourceOriginPosition[2]);

            public Quaternion SourceOriginRotation => new Quaternion(
                sourceOriginRotation[0],
                sourceOriginRotation[1],
                sourceOriginRotation[2],
                sourceOriginRotation[3]);
        }

        [Serializable]
        private sealed class TeimoShopArrivalWaypointSpec
        {
            public float timeSeconds;
            public float[] position = Array.Empty<float>();

            public Vector3 ToVector3() =>
                new Vector3(position[0], position[1], position[2]);
        }
    }

    [CustomEditor(typeof(NpcWorldRuntime))]
    public sealed class NpcWorldRuntimeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var runtime = (NpcWorldRuntime)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Phase 1 NPC diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Initialized", runtime.IsInitialized.ToString());
            EditorGUILayout.LabelField(
                "Materialized presentations",
                runtime.MaterializedPresentationCount.ToString());
            if (!runtime.IsInitialized)
            {
                return;
            }

            foreach (CharacterInstance instance in runtime.Simulation.Instances
                         .OrderBy(value => value.Definition.DefinitionId))
            {
                EditorGUILayout.LabelField(
                    instance.Definition.DefinitionId,
                    $"{instance.ActivityState}; {instance.CurrentAnchorId}; " +
                    $"{instance.CurrentRouteId} {instance.RouteProgress01:F3}");
            }

            EditorGUILayout.LabelField("Recent trace", EditorStyles.boldLabel);
            foreach (NpcTraceEntry entry in runtime.Trace.TakeLast(12))
            {
                EditorGUILayout.HelpBox(
                    $"{entry.GameSeconds:F1} {entry.CharacterId} " +
                    $"{entry.EventId}: {entry.Details}",
                    MessageType.None);
            }
        }
    }
}
