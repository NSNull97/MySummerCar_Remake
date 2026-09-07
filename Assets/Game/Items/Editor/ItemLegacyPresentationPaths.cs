using System;

namespace MSC.Items.Editor
{
    internal static class ItemLegacyPresentationPaths
    {
        public const string EntityTableAssetPath =
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv";
        public const string ExpectedEntityTableSha256 =
            "a0f45c9eb50b2f7dff90caa9d848a0a20be463ce333eae19da576f91dd31a408";
        public const string ExpectedDonorRevision =
            "39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31";

        public const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        public const string GeneratedMeshRoot = GeneratedRoot + "/Meshes";
        public const string GeneratedMaterialRoot = GeneratedRoot + "/Materials";
        public const string GeneratedPrefabRoot = GeneratedRoot + "/Prefabs";
        public const string GeneratedSourceRoot = GeneratedRoot + "/Source";
        public const string GeneratedTextureRoot =
            GeneratedSourceRoot + "/Texture2D";
        public const string GeneratedFontRoot = GeneratedSourceRoot + "/Font";
        public const string GeneratedNativeMaterialRoot =
            GeneratedSourceRoot + "/Material";
        public const string GeneratedDonorMeshSourceRoot =
            GeneratedSourceRoot + "/Mesh";
        public const string DonorExportedAssetsRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets";
        public const string SurfaceManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1ItemSurfacePresentationManifest.json";
        public const string GlobalLegacyScene =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity";
        public const string ExistingWorldMaterialRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Materials/LegacyTextured";

        public const string MarkdownReportPath =
            "Docs/Phase1/ITEM_LEGACY_PRESENTATION_BUILD_REPORT.md";
        public const string CsvReportPath =
            "Docs/Phase1/ITEM_LEGACY_PRESENTATION_BUILD_REPORT.csv";

        // The old WOP:<number> evidence values are not SourceObjectId values.
        // This explicit, reviewed FeatureId mapping is the only accepted bridge
        // from project-owned definitions to frozen donor hierarchy evidence.
        public static readonly ItemLegacyPresentationSource[] Sources =
        {
            new(
                "P1.COMMS.005.envelope",
                "ITEMS/parts magazine(itemx)/EnvelopeSpawn/envelope(xxxxx)",
                includeInactiveGeometry: true),
            MailOrder(1, "marker lights(Clone)"),
            MailOrder(2, "twin carburators(Clone)"),
            MailOrder(3, "steel headers(Clone)"),
            MailOrder(4, "wheel cover plush(Clone)"),
            MailOrder(5, "wheelset slot(Clone)"),
            MailOrder(6, "window grille(Clone)"),
            MailOrder(7, "racing flywheel(Clone)"),
            MailOrder(8, "seat cover leopard(Clone)"),
            MailOrder(9, "wheelset steelwide(Clone)"),
            MailOrder(10, "subwoofers(Clone)"),
            MailOrder(11, "ratchet set(Clone)"),
            MailOrder(12, "tachometer(Clone)"),
            MailOrder(13, "wheelset racing(Clone)"),
            MailOrder(14, "racing muffler(Clone)"),
            MailOrder(15, "dash cover leopard(Clone)"),
            MailOrder(16, "racing exhaust(Clone)"),
            MailOrder(17, "rear spoiler(Clone)"),
            MailOrder(18, "dash cover plush(Clone)"),
            MailOrder(19, "cd player(Clone)"),
            MailOrder(20, "n2o kit(Clone)"),
            MailOrder(21, "seat cover zebra(Clone)"),
            MailOrder(22, "windows black wrap(Clone)"),
            MailOrder(23, "rear spoiler2(Clone)"),
            MailOrder(24, "fender flares(Clone)"),
            MailOrder(25, "racing harness(Clone)"),
            MailOrder(26, "sport steering wheel(Clone)"),
            MailOrder(27, "wheel cover zebra(Clone)"),
            MailOrder(28, "wheelset spoke(Clone)"),
            MailOrder(29, "wheel cover leopard(Clone)"),
            MailOrder(30, "fuel mixture gauge(Clone)"),
            MailOrder(31, "extra gauges(Clone)"),
            MailOrder(32, "wheelset turbine(Clone)"),
            MailOrder(33, "seat cover plush(Clone)"),
            MailOrder(34, "bucket seat driver(Clone)"),
            MailOrder(35, "antenna(Clone)"),
            MailOrder(36, "rally steering wheel(Clone)"),
            MailOrder(37, "racing carburators(Clone)"),
            MailOrder(38, "front spoiler(Clone)"),
            MailOrder(39, "exhaust dual tip(Clone)"),
            MailOrder(40, "wheelset rally(Clone)"),
            MailOrder(41, "wheelset hayosiko(Clone)"),
            MailOrder(42, "wheelset octo(Clone)"),
            MailOrder(43, "racing radiator(Clone)"),
            MailOrder(44, "rally suspension kit(Clone)"),
            MailOrder(45, "dash cover zebra(Clone)"),
            MailOrder(46, "fiberglass hood(Clone)"),
            new("P1.ITEM.101", "ITEMS/sausagesx0"),
            new("P1.ITEM.102", "ITEMS/macaron boxx"),
            new("P1.ITEM.103", "ITEMS/pizzax"),
            DonorPrefab(
                "P1.ITEM.104",
                "GameObject/potato chips.prefab",
                "de9c4097f52ede6945315986b63d84995257ef4dc9f1d33561c7eddc5bf1bf36",
                Mesh(
                    "f229226034339264b9d01b29dd371988",
                    "Mesh/chips.asset",
                    "63e82942212e08557acbe19439cfee12b86a269f6dcb00a03e4b5f11f9d5b3cc",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            new("P1.ITEM.105", "ITEMS/milkx"),
            DonorPrefab("P1.ITEM.106", "GameObject/yeast.prefab",
                "c213c46a2843bfa65a015f1b0e9975f7f626d21550da97c8128c58a0b9292b5f",
                Mesh("b29cb45b46858474ea33b2c6530ca0fc", "Mesh/yeast.asset",
                    "19bc37aa888507b384e71e462879bfa4cee377dcc0e6acbe465cbb09f4c2e5f2",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            DonorPrefab("P1.ITEM.107", "GameObject/sugar.prefab",
                "6f8fe4ff372e93a690c201931d5aa86e7b320cb897dbabd4863174bbd1657a79",
                Mesh("e48b34936ac346d48b38e27f37ffc438", "Mesh/sugar.asset",
                    "9a0e5e6049ba87ceeeb5ecb520e6e4a065715fef1ed7c88d2ce6b0c89365f6f0",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            DonorPrefab("P1.ITEM.108", "GameObject/juice concentrate.prefab",
                "0dff1b187f56f5658831125ad4032df1882f369a8d1830d09c765bb6145a9f59",
                Mesh("bd858252dfdeb924bb9c23f1b4f3cdd6", "Mesh/mehukatti.asset",
                    "3c86fa630efcf9d86a31763275ae1d0cdad9097fca37bbea8210766cbc330715",
                    "56d8bfd532b19e149a6987e1a81d1d10",
                    rotation: new UnityEngine.Quaternion(0f, 0f, -0.38268334f, 0.9238796f)),
                Mesh("d93eed44810b3554c8cd09e945e3968e", "Mesh/mehukatti_001.asset",
                    "a26c907182b0414a3b20f4affbf41bd1bf4b4a5e4f6f200e76b7ac599bd6203c",
                    "56d8bfd532b19e149a6987e1a81d1d10",
                    rotation: new UnityEngine.Quaternion(0f, 0f, -0.38268334f, 0.9238796f))),
            DonorPrefab("P1.ITEM.109", "GameObject/groundcoffee0.prefab",
                "139023d78a23d61b3c82c680a249b73cb596e176d3b039fbe43de030a848a901",
                Mesh("ccf212d7b4630304fa176b389886ce4d", "Mesh/coffee.asset",
                    "e3dfbfa3832ae74c785c71867f7a3d560159a75014ccc27bc61dcd2cb2fc182d",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            DonorPrefab("P1.ITEM.110", "GameObject/grillcharcoal0.prefab",
                "d4a0c3ad8f4473f0e6346fe79282bfe3f896811c20c5b3382925152f3337e98e",
                Mesh("0c5d1df4ca13df145bbaebb087d9fe47", "Mesh/charcoal_bag.asset",
                    "1451c662bac5be56a64b6634193b0716da949bce67a5e349653c91faf6b2b486",
                    "c34448a7491c8d047810e4e19aee2ab6")),
            DonorPrefab("P1.ITEM.111", "GameObject/cigarettes0.prefab",
                "63dc6b6ab36c71ade2b784d0818a1ee7928565fe15ca4b050435434f9a9f409e",
                Mesh("d2c681aec612fe9439182cc95acfb2bb", "Mesh/cigarette_box.asset",
                    "80c66a34301ba00e0624698eba502c58f025039ba6e7ff493f2971475ef11028",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            DonorPrefab("P1.ITEM.112", "GameObject/mosquito spray.prefab",
                "7e72a1f4e8edfe59f0ecb4201e0d3658fab28616f49b89ab2e50c6222459e519",
                Mesh("c4613cac1220f2f49b5cc139a3927a92", "Mesh/hus_spray.asset",
                    "6d18c2fc9f19a42479623d942b75256036198e6feec9356894f61726b7aa5047",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            new(
                "P1.ITEM.113",
                "ITEMS/beercase0",
                -1,
                new UnityEngine.Quaternion(
                    -0.70150095f,
                    -0.08886231f,
                    -0.088862255f,
                    0.70150083f)),
            DonorPrefab(
                "P1.ITEM.116",
                "GameObject/sausage0.prefab",
                "617423878c44653d9d749be20c9fb30395e6b907ad669d55d71849c138eeb2d7",
                Mesh(
                    "40349fe3963aa084399f36244ccb3448",
                    "Mesh/sausage_fresh.asset",
                    "fd9bb2cd379afe5a4834e28f5dea163a76b074fd2ae0dcf2dfd5587e484181d6",
                    "2dea1710c6f0e2c4ba0680d85935b47c")),
            DonorPrefab(
                "P1.ITEM.117",
                "GameObject/Sausage-Potatoes.prefab",
                "c7312f6c9a972d5c6bb133a2df58cbabb206029e292a5a3356c0c62d040002b1",
                Mesh(
                    "9fb50be10853e4049b0bc2980585fb93",
                    "Mesh/grill_box.asset",
                    "16a3b16a557322c34a3d5768da35aeb050fa2a163270c70e3a0a95bc5761fd91",
                    "56d8bfd532b19e149a6987e1a81d1d10"),
                Mesh(
                    "d7da12041a31de744bd02e1eca47b27e",
                    "Mesh/grill_box_food.asset",
                    "1a60c5e9b29297c9e7f7630395bfccb37ca5fe8661c954cb32a6899381f56102",
                    "56d8bfd532b19e149a6987e1a81d1d10")),
            DirectCan("P1.ITEM.120", "GameObject/brakefluid0.prefab",
                "e195231e5375950b3a5fa8160d7507f66ffb58011e09cd20ac82b6af47624000",
                "2ee5a0dcc07d81c40a5739f011237ced", "Mesh/brakefluid_can.asset",
                "cfa0e5c594b25ebc349e9895c84a23e2e0fb4a48082ce0fda90d4115b19562a1"),
            DirectCan("P1.ITEM.121", "GameObject/coolant0.prefab",
                "72126e1022a14c4a7917094a6773e7bd190ae600e689bcba70ed81584f85d355",
                "8a749f7d33bdeea4cb4710a3a29a6e15", "Mesh/coolant_can.asset",
                "ceb9ff0f821298e3b5e48468b21494f8f03142346f7bf64e3ec37cb67f3422a8"),
            DirectCan("P1.ITEM.122", "GameObject/motoroil0.prefab",
                "74d70f38a2cb8ed310b7075d42a895961830ff17dd33fbc7fc6eb2c6ba52f36c",
                "9ee2c2738c6da0e4ba64c8e2344e7ee0", "Mesh/motor_oil.asset",
                "07a81ffd55c7926c25f2b6e13b30d3f8cc7e4d53bc79d864004abd9e96c20d33"),
            DirectCan("P1.ITEM.123", "GameObject/twostroke0.prefab",
                "2970a6c15ffbe20e710e1318c249bee8bedbcdc8cbcaa9e05ed110fca8155103",
                "9eadd974df6f66947b3974931a077695", "Mesh/twostroke_can.asset",
                "979d9a1cdbb504ef2bfa660c8e31ea202935c556bd749e830e95967e02bdb643"),
            DonorPrefab("P1.ITEM.124", "GameObject/alternatorbelt0.prefab",
                "ed14118fd00e542aa0e68655745be5c428f5f9d717299a02f9833eb50e2531a0",
                Mesh("c717b94d7c4af5645bfb7239893d8345", "Mesh/motor_fanbelt_new.asset",
                    "57ac014d4acd911fab8ae82204a2589bec42b61aeebdef47eb8cc71708f1d19d",
                    "c3e79b0de6c4fd74d88a23aa7b70628d"),
                Mesh("2ac2f32de08f0e24d9c5f42f836db3e1", "Mesh/motor_fanbelt_package.asset",
                    "30dbbd6f4e9b4c0237c39f6d0a1f9e8f868a1958f6f821128a5ddf8cc717ffe2",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.125", "GameObject/oilfilter.prefab",
                "be236c413649d67eb5db60c1aa17bf0f249c20959df292239dc66d6e94dbca4a",
                Mesh("d25a445299f721846b44d6468450597c", "Mesh/oil_filter.asset",
                    "7dc43ef8b18fd3d8ff8039a99ddca2296d9d1abdd1e188babe24e2aa7e622a5a",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.126", "GameObject/sparkplugbox0.prefab",
                "c6d92a0ff67a41912feebdc9d9a7fa178d7e2d535be50d00eae6020b32f63e9b",
                Mesh("caf1ab731cfd81244a8896f559f7f709", "Mesh/sparkplugs_box.asset",
                    "b6b3df0ac5b9928830d87484605d433ea1b936fd55e7adcd1fb475a6a1523dc6",
                    "a298bd379cf5c5548ab715ee6d85d4a9")),
            // The dispensed unit is not its package. Only the donor's new-plug
            // tip1 is visible here; worn tip variants remain presentation debt.
            DonorPrefab("P1.ITEM.126.child", "GameObject/sparkplug0.prefab",
                "ee6cfbe2e65a79ad515bbc1d20863503a75773d2b7f7016c656e8716cd5cce4c",
                Mesh("ade5d406ceb80bb43a221e997175ae61", "Mesh/motor_sparkplug.asset",
                    "bd36754272a7b3cfe0eec3c9632bd423f73bce787a3e4bdcb36796d85bc13a60",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83"),
                Mesh("9bd5285a7f9085248bb12fbedf0feb42", "Mesh/motor_sparkplug_tip1.asset",
                    "13fd58a56bf22235e9a7b9895459562a64391e4bfee95f1659a4e4b037703099",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.127", "GameObject/lightbulbbox0.prefab",
                "70fe0c71298b24e23d34d80fb3d7b5844eb14aa7ed52836dc6b99e19624f3e31",
                Mesh("7b28c2f50f5c84a44889851602e100eb", "Mesh/lightbulbbox.asset",
                    "c925519730f5abb50cb727cbae609d08d2b4efd0bdedfd4f09a314dfd11ea72d",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.127.child", "GameObject/lightbulb0.prefab",
                "c30af154f4a123fbf0b2bb366cd574698f26a3362685304e67a48c613b5f9b62",
                Mesh("a90bdae7134d57e4bac2be1dee2f2335", "Mesh/headlight_bulb.asset",
                    "5fc4c50188eee434ab88c7f6f448659c3291a51b2eefab8bf5d7e558a734675c",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.128", "GameObject/fusepackage0.prefab",
                "01a3b44c16c98ccd37c5ad5e3cadb0c0e218c976ceef6116a90c187b48347e24",
                Mesh("a463c6c4a3b3ea04d88b04cc06bc5e61", "Mesh/fuse_package.asset",
                    "42419a1afaad85dbef2cccd0e253aff621cea112ab606300defec576d3698370",
                    "c34448a7491c8d047810e4e19aee2ab6")),
            DonorPrefab("P1.ITEM.129", "GameObject/r20batterybox0.prefab",
                "62f781eec4899082a4df650cbdea58e343c8832951e2ed0dfb879c0d70e5b665",
                Mesh("21c5c4aa82c7aff4bb25a78dfd9624a7", "Mesh/battery_d_package.asset",
                    "f6b7d16e435e157b58e2b661c01a5688fe5f1866aa6b3833c7ec5991096327e8",
                    "c34448a7491c8d047810e4e19aee2ab6")),
            DonorPrefab("P1.ITEM.130", "GameObject/battery.prefab",
                "11475069210bf843a90a6b80a2b56be13cb847e65f1cb354155459961a3303ce",
                Mesh("6a339fb9dc702654cbd6edd8fa371752", "Mesh/motor_battery.asset",
                    "7ce29d0d31f396b0fbc7627e07c8d30adfdab1fa318a9e6176c3b8c9b9b13715",
                    "ad2f7b6e8cc080845a7a7fd4264fbb83")),
            DonorPrefab("P1.ITEM.131", "GameObject/fireextinguisher0.prefab",
                "2a131ea0be4ac38112a124552c000d1f3ee0f8c5fab9e0b80acf6322d0150998",
                Mesh("728be805c71257344bca642a87883a59", "Mesh/fire_extinguisher.asset",
                    "16a0d6f01384c185e858390bc36417b095ae297ec26bcd4ce7f255c6c39878e2",
                    "a298bd379cf5c5548ab715ee6d85d4a9")),
            Spray(0), Spray(1), Spray(2), Spray(3), Spray(4), Spray(5),
            Spray(6), Spray(7), Spray(8), Spray(9), Spray(10), Spray(11),
            Spray(12),
            new("P1.ITEM.133", "ITEMS/gasoline(itemx)"),
            new("P1.ITEM.134", "ITEMS/diesel(itemx)"),
            new("P1.ITEM.135", "ITEMS/ax(itemx)"),
            new("P1.ITEM.136", "ITEMS/digging bar(itemx)"),
            new("P1.ITEM.137", "ITEMS/sledgehammer(itemx)"),
            new("P1.ITEM.138", "ITEMS/spanner set(itemx)"),
            new("P1.ITEM.139", "ITEMS/wiring mess(itemx)"),
            new("P1.ITEM.140", "ITEMS/car jack(itemx)"),
            new("P1.ITEM.141", "ITEMS/floor jack(itemx)"),
            new("P1.ITEM.142", "ITEMS/motor hoist(itemx)"),
            new("P1.ITEM.143", "ITEMS/bucket(itemx)"),
            new("P1.ITEM.144", "ITEMS/bucket lid(itemx)"),
            new("P1.ITEM.145", "ITEMS/water bucket(itemx)"),
            new("P1.ITEM.146", "ITEMS/dipper(itemx)"),
            new("P1.ITEM.147", "ITEMS/wood carrier(itemx)"),
            new("P1.ITEM.148", "ITEMS/garbage barrel(itemx)"),
            new("P1.ITEM.149", "ITEMS/grill(itemx)"),
            new("P1.ITEM.150", "ITEMS/coffee pan(itemx)"),
            new("P1.ITEM.151", "ITEMS/coffee cup(itemx)"),
            new("P1.ITEM.152", "ITEMS/fish trap(itemx)"),
            new("P1.ITEM.153", "ITEMS/flashlight(itemx)"),
            new("P1.ITEM.154", "ITEMS/lantern(itemx)"),
            new("P1.ITEM.155", "ITEMS/helmet(itemx)"),
            new("P1.ITEM.156", "ITEMS/radar buster(Clone)"),
            new("P1.ITEM.157", "ITEMS/radio(itemx)"),
            new("P1.ITEM.158", "ITEMS/camera(itemx)"),
            new("P1.ITEM.159", "ITEMS/diskette(itemx)"),
            new("P1.ITEM.161", "ITEMS/notepad(itemx)"),
            new("P1.ITEM.162", "ITEMS/parts magazine(itemx)"),
            new("P1.ITEM.163", "ITEMS/tv remote control(itemx)"),
            new("P1.ITEM.164", "ITEMS/CDs/cd(item1)"),
            new("P1.ITEM.165", "ITEMS/CDs/cd(item2)"),
            new("P1.ITEM.166", "ITEMS/CDs/cd(item3)"),
            new("P1.ITEM.167", "ITEMS/CDs/cd case(item1)"),
            new("P1.ITEM.168", "ITEMS/CDs/cd case(item2)"),
            new("P1.ITEM.169", "ITEMS/CDs/cd case(item3)"),
            new("P1.ITEM.170", "ITEMS/sofa(itemx)"),
            new("P1.ITEM.171", "ITEMS/basketball(Clone)"),
            new("P1.ITEM.172", "SOCCER/LOD/football(Clone)"),
            new("P1.ITEM.176", "MISC/fireworks bag(itemx)"),
            new(
                "P1.ITEM.177",
                "KILJUGUY/HikerPivot/JokkeHiker2/suitcase(itemx)"),
        };

        private static ItemLegacyPresentationSource DirectCan(
            string featureId,
            string prefabPath,
            string prefabSha256,
            string meshGuid,
            string meshPath,
            string meshSha256) => DonorPrefab(
            featureId,
            prefabPath,
            prefabSha256,
            Mesh(
                meshGuid,
                meshPath,
                meshSha256,
                "a298bd379cf5c5548ab715ee6d85d4a9"));

        private static ItemLegacyPresentationSource MailOrder(
            int featureOrdinal,
            string donorBoxName) =>
            new(
                $"P1.ITEM.017.catalog.{featureOrdinal:00}",
                "STORE/Boxes/" + donorBoxName,
                preferUnpackedGeometry: true);

        private static ItemLegacyPresentationSource Spray(int variantIndex) =>
            DonorPrefab(
                "P1.ITEM.132",
                "GameObject/spraycan0.prefab",
                "1d999d2c465a66fad11edfb9d729fdd1606498795f373f692804f6e7fec1be27",
                variantIndex,
                Mesh("3aea4eaa79b8eae48a4b770b336e5512", "Mesh/spraycan.asset",
                    "d2be33312eac64809e8facb86b607d0299607f217da0a55d3ef00dec0f4df3e0",
                    "a298bd379cf5c5548ab715ee6d85d4a9"),
                Mesh("4b5ec55720c4b634babed2243ad2819f", "Mesh/spraycan_cap.asset",
                    "41498b5304ef31467ad87283cdd9ed6b64df0f775fa3b17b0b29946a8c28527c",
                    "5ff650016d86e7445be12e42b88a66ea"));

        private static ItemLegacyPresentationSource DonorPrefab(
            string featureId,
            string prefabPath,
            string prefabSha256,
            params ItemLegacyPresentationMeshSource[] meshes) =>
            new(featureId, prefabPath, prefabSha256, -1, meshes);

        private static ItemLegacyPresentationSource DonorPrefab(
            string featureId,
            string prefabPath,
            string prefabSha256,
            int variantIndex,
            params ItemLegacyPresentationMeshSource[] meshes) =>
            new(featureId, prefabPath, prefabSha256, variantIndex, meshes);

        private static ItemLegacyPresentationMeshSource Mesh(
            string meshGuid,
            string assetPath,
            string sha256,
            string materialGuid,
            UnityEngine.Vector3 position = default,
            UnityEngine.Quaternion rotation = default,
            UnityEngine.Vector3 scale = default) => new(
            meshGuid,
            assetPath,
            sha256,
            new[] { materialGuid },
            position,
            IsUnsetQuaternion(rotation)
                ? UnityEngine.Quaternion.identity
                : rotation,
            scale == default ? UnityEngine.Vector3.one : scale);

        private static bool IsUnsetQuaternion(
            UnityEngine.Quaternion value) =>
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w < 0.000001f;
    }

    internal readonly struct ItemLegacyPresentationSource
    {
        public ItemLegacyPresentationSource(
            string featureId,
            string sourceHierarchyRoot,
            int variantIndex = -1,
            UnityEngine.Quaternion presentationRotation = default,
            bool includeInactiveGeometry = false,
            bool preferUnpackedGeometry = false)
        {
            FeatureId = featureId ?? throw new ArgumentNullException(
                nameof(featureId));
            SourceHierarchyRoot = sourceHierarchyRoot ??
                throw new ArgumentNullException(nameof(sourceHierarchyRoot));
            VariantIndex = variantIndex;
            DonorPrefabRelativePath = string.Empty;
            DonorPrefabSha256 = string.Empty;
            DirectMeshes = Array.Empty<ItemLegacyPresentationMeshSource>();
            PresentationRotation = IsUnset(presentationRotation)
                ? UnityEngine.Quaternion.identity
                : presentationRotation;
            IncludeInactiveGeometry = includeInactiveGeometry;
            PreferUnpackedGeometry = preferUnpackedGeometry;
        }

        public ItemLegacyPresentationSource(
            string featureId,
            string donorPrefabRelativePath,
            string donorPrefabSha256,
            int variantIndex,
            ItemLegacyPresentationMeshSource[] directMeshes)
        {
            FeatureId = featureId ?? throw new ArgumentNullException(
                nameof(featureId));
            SourceHierarchyRoot = string.Empty;
            VariantIndex = variantIndex;
            DonorPrefabRelativePath = donorPrefabRelativePath ??
                throw new ArgumentNullException(nameof(donorPrefabRelativePath));
            DonorPrefabSha256 = donorPrefabSha256 ??
                throw new ArgumentNullException(nameof(donorPrefabSha256));
            DirectMeshes = directMeshes ??
                throw new ArgumentNullException(nameof(directMeshes));
            PresentationRotation = UnityEngine.Quaternion.identity;
            IncludeInactiveGeometry = false;
            PreferUnpackedGeometry = false;
        }

        public string FeatureId { get; }
        public string SourceHierarchyRoot { get; }
        public int VariantIndex { get; }
        public string DonorPrefabRelativePath { get; }
        public string DonorPrefabSha256 { get; }
        public ItemLegacyPresentationMeshSource[] DirectMeshes { get; }
        public bool IsDirectDonorPrefab =>
            !string.IsNullOrWhiteSpace(DonorPrefabRelativePath);
        public UnityEngine.Quaternion PresentationRotation { get; }
        public bool IncludeInactiveGeometry { get; }
        public bool PreferUnpackedGeometry { get; }
        public string EvidencePath => IsDirectDonorPrefab
            ? "DONOR_PREFAB/" + DonorPrefabRelativePath
            : SourceHierarchyRoot;

        private static bool IsUnset(UnityEngine.Quaternion value) =>
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w < 0.000001f;
    }

    internal readonly struct ItemLegacyPresentationMeshSource
    {
        public ItemLegacyPresentationMeshSource(
            string meshGuid,
            string assetRelativePath,
            string sha256,
            string[] materialGuids,
            UnityEngine.Vector3 localPosition,
            UnityEngine.Quaternion localRotation,
            UnityEngine.Vector3 localScale)
        {
            MeshGuid = meshGuid ?? throw new ArgumentNullException(nameof(meshGuid));
            AssetRelativePath = assetRelativePath ??
                throw new ArgumentNullException(nameof(assetRelativePath));
            Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
            MaterialGuids = materialGuids ??
                throw new ArgumentNullException(nameof(materialGuids));
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public string MeshGuid { get; }
        public string AssetRelativePath { get; }
        public string Sha256 { get; }
        public string[] MaterialGuids { get; }
        public UnityEngine.Vector3 LocalPosition { get; }
        public UnityEngine.Quaternion LocalRotation { get; }
        public UnityEngine.Vector3 LocalScale { get; }
    }
}
