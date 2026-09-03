using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items.Editor
{
    public static class Milestone09BItemCatalogBuilder
    {
        public const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";
        public const string PlacementCatalogPath =
            "Assets/Game/Items/Content/Placements/Phase1ItemPlacementCatalog.asset";
        private const string RosterPath =
            "Docs/Phase1/ITEM_DEFINITION_SUBROSTER.csv";
        private const string WorldEntitiesPath =
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string DonorRevision =
            "msc-world-baseline-04a1.1-c3f2f337";
        private const int CanonicalPlacementCount = 43;
        private const int MailOrderRequiredDefinitionCount = 47;

        private static readonly IReadOnlyDictionary<string, float>
            DonorMassKilogramsByFeatureId =
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    ["P1.ITEM.101"] = 0.8f,
                    ["P1.ITEM.102"] = 0.5f,
                    ["P1.ITEM.103"] = 0.5f,
                    ["P1.ITEM.105"] = 1.1f,
                    ["P1.ITEM.110"] = 1.8f,
                    ["P1.ITEM.113"] = 9f,
                    ["P1.ITEM.133"] = 21f,
                    ["P1.ITEM.134"] = 21f,
                    ["P1.ITEM.135"] = 5f,
                    ["P1.ITEM.136"] = 7f,
                    ["P1.ITEM.137"] = 15f,
                    ["P1.ITEM.138"] = 8f,
                    ["P1.ITEM.139"] = 1f,
                    ["P1.ITEM.140"] = 2f,
                    ["P1.ITEM.141"] = 9999f,
                    ["P1.ITEM.142"] = 9999f,
                    ["P1.ITEM.143"] = 2f,
                    ["P1.ITEM.144"] = 1f,
                    ["P1.ITEM.145"] = 6f,
                    ["P1.ITEM.146"] = 1f,
                    ["P1.ITEM.147"] = 10f,
                    ["P1.ITEM.148"] = 30f,
                    ["P1.ITEM.149"] = 6f,
                    ["P1.ITEM.150"] = 2f,
                    ["P1.ITEM.151"] = 0.8f,
                    ["P1.ITEM.152"] = 3f,
                    ["P1.ITEM.153"] = 1f,
                    ["P1.ITEM.154"] = 1f,
                    ["P1.ITEM.155"] = 1f,
                    ["P1.ITEM.156"] = 0.5f,
                    ["P1.ITEM.157"] = 8f,
                    ["P1.ITEM.158"] = 0.7f,
                    ["P1.ITEM.159"] = 1f,
                    ["P1.ITEM.161"] = 0.5f,
                    ["P1.ITEM.163"] = 0.8f,
                    ["P1.ITEM.164"] = 1f,
                    ["P1.ITEM.165"] = 1f,
                    ["P1.ITEM.166"] = 1f,
                    ["P1.ITEM.167"] = 0.5f,
                    ["P1.ITEM.168"] = 0.5f,
                    ["P1.ITEM.169"] = 0.5f,
                    ["P1.ITEM.170"] = 90f,
                    ["P1.ITEM.171"] = 0.6f,
                    ["P1.ITEM.172"] = 0.6f,
                    ["P1.ITEM.176"] = 7f,
                    ["P1.ITEM.177"] = 18f,
                };

        private static readonly IReadOnlyDictionary<string, string>
            RuntimeDisplayNamesByFeatureId =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["P1.ITEM.113"] = "Ящик пива",
                    ["P1.ITEM.114"] = "Бутылка пива",
                };

        private static readonly IReadOnlyDictionary<string, string>
            RootFeatureIds = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["car jack(itemx)"] = "P1.ITEM.140",
                ["macaron boxx"] = "P1.ITEM.102",
                ["ax(itemx)"] = "P1.ITEM.135",
                ["lantern(itemx)"] = "P1.ITEM.154",
                ["sausagesx0"] = "P1.ITEM.101",
                ["wood carrier(itemx)"] = "P1.ITEM.147",
                ["garbage barrel(itemx)"] = "P1.ITEM.148",
                ["pizzax"] = "P1.ITEM.103",
                ["grill(itemx)"] = "P1.ITEM.149",
                ["gasoline(itemx)"] = "P1.ITEM.133",
                ["dipper(itemx)"] = "P1.ITEM.146",
                ["radar buster(Clone)"] = "P1.ITEM.156",
                ["motor hoist(itemx)"] = "P1.ITEM.142",
                ["water bucket(itemx)"] = "P1.ITEM.145",
                ["camera(itemx)"] = "P1.ITEM.158",
                ["coffee pan(itemx)"] = "P1.ITEM.150",
                ["tv remote control(itemx)"] = "P1.ITEM.163",
                ["diesel(itemx)"] = "P1.ITEM.134",
                ["sofa(itemx)"] = "P1.ITEM.170",
                ["basketball(Clone)"] = "P1.ITEM.171",
                ["parts magazine(itemx)"] = "P1.ITEM.162",
                ["fish trap(itemx)"] = "P1.ITEM.152",
                ["CDs"] = "P1.ITEM.160",
                ["bucket(itemx)"] = "P1.ITEM.143",
                ["beercase0"] = "P1.ITEM.113",
                ["milkx"] = "P1.ITEM.105",
                ["diskette(itemx)"] = "P1.ITEM.159",
                ["digging bar(itemx)"] = "P1.ITEM.136",
                ["coffee cup(itemx)"] = "P1.ITEM.151",
                ["wiring mess(itemx)"] = "P1.ITEM.139",
                ["helmet(itemx)"] = "P1.ITEM.155",
                ["sledgehammer(itemx)"] = "P1.ITEM.137",
                ["flashlight(itemx)"] = "P1.ITEM.153",
                ["spanner set(itemx)"] = "P1.ITEM.138",
                ["floor jack(itemx)"] = "P1.ITEM.141",
                ["radio(itemx)"] = "P1.ITEM.157",
                ["notepad(itemx)"] = "P1.ITEM.161",
                ["bucket lid(itemx)"] = "P1.ITEM.144",
            };

        private const string ExpandedShopDllSha256 =
            "2E51250495D97E628AE98A8892B733FE76C775A7278D104DD73E2BE719789A7E";
        private const string ExpandedShopBundleSha256 =
            "997E3F2D227AAED732B91DAAF2EB40EE180D75A9051319813E28B534B3AB0773";

        private static readonly Milestone09BItemCatalogBuilder.ExpandedShopItemSeed[] ExpandedShopItemSeeds =
        {
            ExpandedConsumable(
                1,
                "buttermilk",
                "Buttermilk",
                "DrinkFood",
                1f,
                new Vector3(0.09f, 0.22f, 0.09f),
                new Vector4(-2f, -4f, 0f, 0.07f),
                ExpandedFood(
                    perishable: true,
                    ambientRate: 0.04f,
                    refrigeratedRate: 0.0009f,
                    spoiledEffects: SpoiledEffect(0.07f),
                    consumptionPresentation:
                        ItemConsumptionPresentation.Drink),
                urineEffect: 2.5f,
                emptyContainerMassKilograms: 0.2f),
            ExpandedConsumable(
                2,
                "orange-juice",
                "Orange juice",
                "DrinkFood",
                1f,
                new Vector3(0.08f, 0.25f, 0.08f),
                new Vector4(-2f, -4.3f, 0f, 0.09f),
                ExpandedFood(
                    consumptionPresentation:
                        ItemConsumptionPresentation.Drink),
                urineEffect: 1.7f,
                emptyContainerMassKilograms: 0.2f),
            ExpandedUtility(
                3,
                "bug-spray",
                "Bug spray",
                "ServiceSupply",
                0.35f,
                new Vector3(0.06f, 0.20f, 0.06f),
                ItemContentMeasure.Condition,
                250f),
            ExpandedUtility(
                4,
                "laundry-detergent",
                "Laundry detergent",
                "ServiceSupply",
                1.5f,
                new Vector3(0.18f, 0.28f, 0.10f)),
            ExpandedUtility(
                5,
                "sponge",
                "Sponge",
                "ServiceSupply",
                0.04f,
                new Vector3(0.12f, 0.04f, 0.08f)),
            ExpandedUtility(
                6,
                "shampoo",
                "Shampoo",
                "ServiceSupply",
                0.4f,
                new Vector3(0.07f, 0.21f, 0.07f)),
            ExpandedUtility(
                7,
                "hand-soap",
                "Hand soap",
                "ServiceSupply",
                0.3f,
                new Vector3(0.08f, 0.14f, 0.07f)),
            ExpandedUtility(
                8,
                "dish-soap",
                "Dish soap",
                "ServiceSupply",
                0.5f,
                new Vector3(0.07f, 0.23f, 0.07f)),
            ExpandedUtility(
                9,
                "soap",
                "Soap",
                "ServiceSupply",
                0.1f,
                new Vector3(0.10f, 0.04f, 0.07f)),
            ExpandedUtility(
                10,
                "wheat-flour",
                "Wheat flour",
                "Ingredient",
                1f,
                new Vector3(0.14f, 0.24f, 0.10f)),
            ExpandedUtility(
                11,
                "rye-flour",
                "Rye flour",
                "Ingredient",
                1f,
                new Vector3(0.14f, 0.24f, 0.10f)),
            ExpandedConsumable(
                12,
                "mustard",
                "Mustard",
                "Food",
                0.4f,
                new Vector3(0.06f, 0.20f, 0.06f),
                new Vector4(-4f, 1f, -11f, 0.09f),
                ExpandedFood(
                    consumptionPresentation:
                        ItemConsumptionPresentation.Drink),
                urineEffect: 1.5f),
            ExpandedConsumable(
                13,
                "ketchup",
                "Ketchup",
                "Food",
                0.4f,
                new Vector3(0.06f, 0.20f, 0.06f),
                new Vector4(-4f, 1f, -11f, 0.09f),
                ExpandedFood(
                    consumptionPresentation:
                        ItemConsumptionPresentation.Drink),
                urineEffect: 1.5f),
            ExpandedConsumable(
                14,
                "meat-soup",
                "Meat soup",
                "Food",
                0.5f,
                new Vector3(0.10f, 0.12f, 0.10f),
                new Vector4(-70f, 7f, 0f, 0.035f),
                ExpandedFood()),
            ExpandedConsumable(
                15,
                "pea-soup",
                "Pea soup",
                "Food",
                0.5f,
                new Vector3(0.10f, 0.12f, 0.10f),
                new Vector4(-60f, 10f, 0f, 0.03f),
                ExpandedFood()),
            ExpandedConsumable(
                16,
                "canned-meatballs",
                "Canned meatballs",
                "Food",
                0.5f,
                new Vector3(0.10f, 0.12f, 0.10f),
                new Vector4(-65f, 3f, 0f, 0.05f),
                ExpandedFood()),
            ExpandedPackage(
                17,
                "fishstick-box",
                "Fishstick box",
                0.5f,
                new Vector3(0.18f, 0.05f, 0.28f),
                10,
                "item.fishstick",
                ExpandedFood(
                    edible: false,
                    perishable: true,
                    ambientRate: 0.1f,
                    refrigeratedRate: 0.01f,
                    consumptionDurationSeconds: 0f,
                    spoiledEffects: SpoiledEffect(0.02f))),
            ExpandedUtility(
                18,
                "can-opener",
                "Can opener",
                "Tool",
                0.12f,
                new Vector3(0.20f, 0.03f, 0.06f),
                toolType: "CanOpener"),
            ExpandedConsumable(
                19,
                "fishstick",
                "Fishstick",
                "Food",
                0.05f,
                new Vector3(0.14f, 0.025f, 0.03f),
                new Vector4(-1.5f, 0f, 12f, 0.03f),
                ExpandedFood(
                    perishable: true,
                    cookable: true,
                    ambientRate: 0.1f,
                    refrigeratedRate: 0.01f,
                    cookedAfterSeconds: 40f,
                    burnedAfterAdditionalSeconds: 10f,
                    cookedEffects: Effect(
                        true,
                        new Vector4(-10f, 0f, -3.2f, 0.03f)),
                    burnedEffects: Effect(
                        true,
                        new Vector4(-1f, 5f, 12f, 0.03f)),
                    spoiledEffects: SpoiledEffect(0.03f))),
        };

        [MenuItem("MSC/Items/09B/Build catalogs and bind Bootstrap")]
        public static void BuildMenu()
        {
            Build();
            Debug.Log("M09B_ITEM_CATALOG_BUILD_PASS");
        }

        public static void Build()
        {
            CsvTable roster = CsvTable.Read(RosterPath);
            List<ItemDefinitionRecord> definitions = roster.Rows
                .Select(BuildDefinition)
                .ToList();
            AddSpawnedChildDefinitions(definitions);
            AddHomeMailOrderDefinitions(definitions);
            AddExpandedShopDefinitions(definitions);

            ItemDefinitionCatalog definitionCatalog =
                LoadOrCreate<ItemDefinitionCatalog>(DefinitionCatalogPath);
            definitionCatalog.ConfigureForAuthoring(
                "phase1.items.09b.v1",
                definitions.ToArray());
            EditorUtility.SetDirty(definitionCatalog);

            Dictionary<string, ItemDefinitionRecord> definitionsByFeature =
                definitions
                    .Where(definition => definition.FeatureId.StartsWith(
                        "P1.ITEM.",
                        StringComparison.Ordinal))
                    .GroupBy(definition => definition.FeatureId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.Ordinal);
            ItemPlacementRecord[] placements = BuildPlacements(
                CsvTable.Read(WorldEntitiesPath),
                definitionsByFeature);
            ItemPlacementCatalog placementCatalog =
                LoadOrCreate<ItemPlacementCatalog>(PlacementCatalogPath);
            placementCatalog.ConfigureForAuthoring(
                "phase1.item-placements.09b.v1",
                DonorRevision,
                placements);
            EditorUtility.SetDirty(placementCatalog);

            AssertCatalogs(definitionCatalog, placementCatalog);
            BindBootstrap(definitionCatalog, placementCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static ItemDefinitionRecord BuildDefinition(CsvRow row)
        {
            string featureId = row["FeatureId"];
            string name = row["Definition"];
            string displayName = RuntimeDisplayNamesByFeatureId.TryGetValue(
                featureId,
                out string localizedDisplayName)
                    ? localizedDisplayName
                    : name;
            string category = row["Category"];
            string definitionId = "item." + Slug(name);
            var plan = DefinitionPlan.Create(category);
            ApplyEvidenceOverrides(featureId, definitionId, plan);
            if (DonorMassKilogramsByFeatureId.TryGetValue(
                    featureId,
                    out float donorMassKilograms))
            {
                plan.Mass = donorMassKilograms;
            }

            var definition = new ItemDefinitionRecord();
            definition.ConfigureForAuthoring(
                definitionId,
                featureId,
                displayName,
                category,
                row["ParentFeatureId"],
                NormalizeEvidence(row["DonorEvidence"]),
                "legacy.item." + definitionId.Substring("item.".Length),
                string.Equals(row["Required"], "Yes", StringComparison.Ordinal),
                plan.Calibration,
                plan.Measure,
                plan.Action,
                plan.Maximum,
                plan.Initial,
                plan.UseAmount,
                plan.Mass,
                plan.EmptyContainerMass,
                plan.MaximumCarryMass,
                plan.ProxySize,
                plan.CanOpen,
                plan.StartsOpen,
                plan.RetainWhenEmpty,
                plan.SupportsLiquid,
                plan.InitialLiquidId,
                plan.CriticalRecovery,
                plan.InitialChildCount,
                plan.ChildDefinitionId,
                plan.ProducedDefinitionId,
                plan.ToolType,
                plan.ToolVariants,
                plan.NeedEffects,
                plan.IntoxicationEffect,
                plan.Scalars.Select(value => value.Build()).ToArray(),
                plan.Flags.Select(value => value.Build()).ToArray());
            definition.ConfigureLifeEffectsForAuthoring(
                plan.UrineEffect,
                plan.FatigueEffect,
                plan.DirtinessEffect);
            definition.ConfigureFoodForAuthoring(
                BuildFoodDefinition(featureId),
                BuildHeatSourceDefinition(featureId));
            definition.ConfigureCombustionForAuthoring(
                BuildCombustionDefinition(featureId));
            definition.ConfigurePhysicalShapesForAuthoring(
                BuildColliderShapes(featureId));
            return definition;
        }

        private static void ApplyEvidenceOverrides(
            string featureId,
            string definitionId,
            DefinitionPlan plan)
        {
            switch (featureId)
            {
                case "P1.ITEM.101":
                    plan.SetChildPackage(4, "item.loose-sausage");
                    plan.EmptyContainerMass = 0.12f;
                    plan.NeedEffects = new Vector4(-100f, 12f, 0f, 0.136f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.034f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.0005f);
                    break;
                case "P1.ITEM.102":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 25f);
                    plan.NeedEffects = new Vector4(-80f, 24f, 0f, 0.06f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.032f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.0004f);
                    break;
                case "P1.ITEM.103":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 25f);
                    plan.NeedEffects = new Vector4(-50f, 22f, 0f, 0.068f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.026f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.0003f);
                    break;
                case "P1.ITEM.104":
                    plan.SetContent(ItemContentMeasure.Units, 1f, 1f, 1f);
                    plan.NeedEffects = new Vector4(-90f, 60f, 0f, 0.2f);
                    break;
                case "P1.ITEM.105":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 10f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.06f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.001f);
                    break;
                case "P1.ITEM.109":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 1f);
                    break;
                case "P1.ITEM.110":
                    plan.SetContent(ItemContentMeasure.Units, 140f, 140f, 12f);
                    // Charcoal is poured into a compatible fuel receiver; F on
                    // the carried bag must not consume it as food.
                    plan.Action = ItemPrimaryAction.None;
                    break;
                case "P1.ITEM.111":
                    // Observable Phase 1 baseline. Exact donor stress and
                    // fatigue coefficients remain capture/calibration pending.
                    plan.SetContent(ItemContentMeasure.Units, 20f, 20f, 1f);
                    plan.Action = ItemPrimaryAction.Consume;
                    plan.NeedEffects =
                        new Vector4(0f, 0f, -10f, 0f);
                    plan.FatigueEffect = -1f;
                    break;
                case "P1.ITEM.112":
                    plan.SetContent(ItemContentMeasure.Units, 100f, 100f, 10f);
                    break;
                case "P1.ITEM.113":
                    plan.SetContent(ItemContentMeasure.Units, 24f, 24f, 1f);
                    plan.Action = ItemPrimaryAction.DispenseChild;
                    plan.EmptyContainerMass = 1.5f;
                    plan.CanOpen = false;
                    plan.StartsOpen = false;
                    plan.SupportsLiquid = false;
                    plan.InitialLiquidId = string.Empty;
                    plan.InitialChildCount = 24;
                    plan.ChildDefinitionId = "item.beer-bottle";
                    break;
                case "P1.ITEM.114":
                    plan.SetLiquid(0.33f, 0.33f, "liquid.beer", 0.33f);
                    plan.Action = ItemPrimaryAction.Consume;
                    plan.CanOpen = false;
                    // Exact donor calibration remains pending. The Phase 1
                    // baseline must still provide the observable contract that
                    // drinking beer reduces the project-owned thirst need.
                    plan.NeedEffects = new Vector4(0f, -20f, 0f, 0f);
                    plan.IntoxicationEffect = 8f;
                    plan.UrineEffect = 4f;
                    break;
                case "P1.ITEM.116":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 100f);
                    plan.NeedEffects = new Vector4(-33.3f, 3f, 0f, 0.034f);
                    plan.AddScalar("grill-state", 0f, 40f, 0f);
                    plan.AddScalar("grill-threshold", 0f, 300f, 30f);
                    plan.AddScalar("burn-threshold", 0f, 300f, 10f);
                    break;
                case "P1.ITEM.117":
                    plan.NeedEffects = new Vector4(-33f, 33f, 0f, 1.12f);
                    break;
                case "P1.ITEM.118":
                    plan.SetContent(ItemContentMeasure.Condition, 40f, 40f, 40f);
                    plan.NeedEffects = new Vector4(-130f, 16f, 0f, 0.165f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.033f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.0018f);
                    plan.AddScalar("grill-threshold", 0f, 300f, 240f);
                    plan.AddScalar("burn-threshold", 0f, 300f, 20f);
                    break;
                case "P1.ITEM.119":
                    plan.SetContent(ItemContentMeasure.Condition, 40f, 40f, 40f);
                    plan.NeedEffects = new Vector4(-150f, 22f, -40f, 0.41f);
                    plan.AddScalar("spoil-rate", 0f, 1f, 0.031f);
                    plan.AddScalar("fridge-rate", 0f, 1f, 0.0012f);
                    plan.AddScalar("grill-threshold", 0f, 300f, 240f);
                    plan.AddScalar("burn-threshold", 0f, 300f, 20f);
                    break;
                case "P1.ITEM.120": plan.SetLiquid(1f, 1f, "liquid.brake-fluid", 0.1f); break;
                case "P1.ITEM.121": plan.SetLiquid(10f, 10f, "liquid.coolant", 0.25f); break;
                case "P1.ITEM.122": plan.SetLiquid(4f, 4f, "liquid.motor-oil", 0.1f); break;
                case "P1.ITEM.123": plan.SetLiquid(5f, 5f, "liquid.two-stroke-oil", 0.1f); break;
                case "P1.ITEM.124":
                    plan.AddScalar("wear", 0f, 100f, 0f);
                    plan.AddFlag("installed", false);
                    break;
                case "P1.ITEM.125":
                    plan.AddScalar("dirt", 0f, 1f, 1f);
                    plan.AddScalar("tightness", 0f, 8f, 0f);
                    plan.AddFlag("installed", false);
                    plan.AddFlag("consumed", false);
                    break;
                case "P1.ITEM.126":
                    plan.SetChildPackage(4, "item.spark-plug");
                    break;
                case "P1.ITEM.127":
                    plan.SetChildPackage(1, "item.light-bulb");
                    break;
                case "P1.ITEM.128":
                    plan.SetChildPackage(5, "item.fuse-unit");
                    break;
                case "P1.ITEM.129":
                    plan.SetChildPackage(4, "item.r20-battery-unit");
                    break;
                case "P1.ITEM.130":
                    plan.SetContent(ItemContentMeasure.Charge, 145f, 128f, 0f);
                    plan.AddScalar("discharge-rate", 0f, 1f, 0.0003f);
                    plan.AddFlag("installed", false);
                    plan.AddFlag("consumed", false);
                    plan.AddFlag("charged", false);
                    break;
                case "P1.ITEM.131":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 1f);
                    plan.AddFlag("installed", false);
                    plan.AddFlag("in-use", false);
                    break;
                case "P1.ITEM.132":
                    plan.SetContent(ItemContentMeasure.Condition, 100f, 100f, 1f);
                    plan.ToolVariants = new[]
                    {
                        "matte01",
                        "spray01",
                        "spray02",
                        "spray03",
                        "spray04",
                        "spray05",
                        "spray06",
                        "spray07",
                        "spray08",
                        "spray09",
                        "spray10",
                        "spray11",
                        "spray12",
                    };
                    plan.AddScalar("color-id", 0f, 15f, 0f);
                    plan.AddFlag("matte", false);
                    break;
                case "P1.ITEM.133":
                    plan.SetLiquid(20f, 2f, "liquid.gasoline", 0.25f);
                    plan.CriticalRecovery = true;
                    break;
                case "P1.ITEM.134":
                    plan.SetLiquid(20f, 4f, "liquid.diesel", 0.25f);
                    plan.AddFlag("fuel-oil", false);
                    plan.CriticalRecovery = true;
                    break;
                case "P1.ITEM.138":
                    // The donor item is the toolbox, not a magic wrench that
                    // changes size in place. F toggles its lid; individual
                    // project-owned wrench targets are exposed by the reviewed
                    // presentation wrapper while it is open.
                    plan.Action = ItemPrimaryAction.ToggleOpen;
                    plan.CanOpen = true;
                    plan.StartsOpen = false;
                    plan.ToolType = string.Empty;
                    plan.ToolVariants = new[]
                    {
                        "5", "6", "7", "8", "9", "10", "11",
                        "12", "13", "14", "15",
                    };
                    break;
                case "P1.ITEM.140":
                    plan.AddFlag("open", false);
                    plan.AddScalar("lift-step", 0f, 1f, 0.04f);
                    plan.AddScalar("lift-height", 0f, 0.48f, 0f);
                    break;
                case "P1.ITEM.141":
                    plan.AddScalar("lift-height", 0f, 0.38f, 0f);
                    break;
                case "P1.ITEM.142":
                    plan.AddScalar("bolted-threshold", 0f, 20f, 10f);
                    plan.AddScalar("hook-break-force", 0f, 100000f, 62000f);
                    break;
                case "P1.ITEM.143":
                    plan.SetLiquid(30f, 0f, string.Empty, 1f);
                    // The body remains a pickup; the separate lid target owns
                    // ordinary F open/close through the same saved isOpen bit.
                    plan.Action = ItemPrimaryAction.None;
                    plan.CanOpen = true;
                    plan.AddScalar("sugar", 0f, 20f, 0f);
                    plan.AddScalar("yeast", 0f, 20f, 0f);
                    plan.AddScalar("alcohol", 0f, 100f, 0f);
                    plan.AddScalar("sweetness", 0f, 100f, 0f);
                    plan.AddScalar("vinegar", 0f, 100f, 0f);
                    plan.AddScalar("brew-time", 0f, 680f, 0f);
                    plan.AddFlag("lid", false);
                    plan.AddFlag("finished", false);
                    break;
                case "P1.ITEM.145":
                    plan.SetLiquid(10f, 0f, string.Empty, 0.5f);
                    plan.Action = ItemPrimaryAction.None;
                    plan.CanOpen = false;
                    plan.StartsOpen = true;
                    break;
                case "P1.ITEM.146":
                    plan.SetLiquid(0.5f, 0f, string.Empty, 0.1f);
                    plan.Action = ItemPrimaryAction.None;
                    plan.CanOpen = false;
                    plan.StartsOpen = true;
                    break;
                case "P1.ITEM.147": plan.AddScalar("wood-count", 0f, 64f, 0f); break;
                case "P1.ITEM.148":
                    // Donor Rigidbody 90058: 30 kg. The donor uses eight
                    // convex mesh colliders around the open barrel shell.
                    plan.Mass = 30f;
                    plan.ProxySize = new Vector3(
                        0.592f,
                        0.592f,
                        0.876f);
                    plan.Action = ItemPrimaryAction.Ignite;
                    plan.CanOpen = false;
                    plan.StartsOpen = true;
                    break;
                case "P1.ITEM.149":
                    plan.SetContent(ItemContentMeasure.Units, 100f, 0f, 12f);
                    plan.Action = ItemPrimaryAction.IgniteFuel;
                    // The body owns ignition while the sanitized presentation
                    // exposes the donor cover as a separate F target backed by
                    // this independent save bit.
                    plan.CanOpen = true;
                    plan.StartsOpen = false;
                    plan.AddScalar("burn-time", 0f, 120f, 0f);
                    plan.AddFlag("wet", false);
                    break;
                case "P1.ITEM.150":
                    plan.SetLiquid(0.6f, 0f, string.Empty, 0.03f);
                    plan.AddScalar("grounds", 0f, 26f, 0f);
                    plan.AddScalar("coffee", 0f, 0.6f, 0f);
                    plan.AddScalar("caffeine", 0f, 100f, 0f);
                    plan.AddScalar("cooking-rate", 0f, 1f, 0.3f);
                    plan.AddScalar("boil-rate", 0f, 1f, 0.0005f);
                    break;
                case "P1.ITEM.151": plan.SetLiquid(0.25f, 0f, string.Empty, 0.03f); break;
                case "P1.ITEM.152":
                    plan.SetContent(ItemContentMeasure.Units, 12f, 0f, 1f);
                    plan.Action = ItemPrimaryAction.ToggleOpen;
                    plan.CanOpen = true;
                    break;
                case "P1.ITEM.153":
                    plan.SetContent(ItemContentMeasure.Charge, 100f, 100f, 0f);
                    plan.Action = ItemPrimaryAction.ToggleDevice;
                    plan.AddScalar("consumption-rate", 0f, 1f, 0.0013f);
                    plan.AddFlag("battery-installed", false);
                    break;
                case "P1.ITEM.155":
                    // Donor helmet Paint FSM persists a Color and PaintType.
                    // Project-owned state keeps that contract independent of
                    // the temporary donor mesh/material wrapper.
                    plan.AddScalar("paint-color-r", 0f, 1f, 0.21896628f);
                    plan.AddScalar("paint-color-g", 0f, 1f, 0.41388258f);
                    plan.AddScalar("paint-color-b", 0f, 1f, 0.5514706f);
                    plan.AddFlag("paint-applied", false);
                    plan.AddFlag("paint-matte", false);
                    break;
                case "P1.ITEM.157":
                    plan.SetContent(ItemContentMeasure.Charge, 100f, 100f, 0f);
                    plan.Action = ItemPrimaryAction.ToggleDevice;
                    plan.AddScalar("channel", 0f, 10f, 0f);
                    plan.AddScalar("volume", 0f, 1f, 0.5f);
                    plan.AddScalar("consumption-divider", 1f, 10000f, 8000f);
                    break;
                case "P1.ITEM.159":
                    plan.ToolVariants = new[] { "rapula-780kb", "massacre-1310kb", "joulu-320kb" };
                    break;
            }

            if (plan.ToolType.Length > 0)
            {
                plan.CriticalRecovery = true;
            }
        }

        private static ItemFoodDefinition BuildFoodDefinition(string featureId)
        {
            bool edible = false;
            bool perishable = false;
            bool cookable = false;
            bool consumeWhole = false;
            float duration = 0f;
            float ambientRate = 0f;
            float fridgeRate = 0f;
            float cookedAfter = 0f;
            float burnedAfter = 0f;
            ItemConsumptionPresentation consumptionPresentation =
                ItemConsumptionPresentation.None;
            FoodEffectDefinition cookedEffects = Effect();
            FoodEffectDefinition burnedEffects = Effect();
            FoodEffectDefinition spoiledEffects = Effect();

            switch (featureId)
            {
                case "P1.ITEM.101":
                    perishable = true;
                    ambientRate = 0.034f;
                    fridgeRate = 0.0005f;
                    break;
                case "P1.ITEM.102":
                    SetOrdinaryFood(
                        true,
                        0.032f,
                        0.0004f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    spoiledEffects = SpoiledEffect(0.06f);
                    break;
                case "P1.ITEM.103":
                    SetOrdinaryFood(
                        true,
                        0.026f,
                        0.0003f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    spoiledEffects = SpoiledEffect(0.068f);
                    break;
                case "P1.ITEM.104":
                    SetOrdinaryFood(
                        false,
                        0f,
                        0f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    break;
                case "P1.ITEM.105":
                    SetOrdinaryFood(
                        true,
                        0.06f,
                        0.001f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    spoiledEffects = SpoiledEffect(0.1f);
                    consumptionPresentation =
                        ItemConsumptionPresentation.Drink;
                    break;
                case "P1.ITEM.108":
                    // The donor juice-container Use FSM explicitly dispatches
                    // to the Hand/Drink presentation. Keep the existing
                    // project item authority, but let the physical bottle stay
                    // in the shared drink grip until the timed use completes.
                    SetOrdinaryFood(
                        false,
                        0f,
                        0f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    consumptionPresentation =
                        ItemConsumptionPresentation.Drink;
                    break;
                case "P1.ITEM.116":
                    edible = true;
                    perishable = true;
                    cookable = true;
                    consumeWhole = true;
                    duration = 1.35f;
                    ambientRate = 0.034f;
                    fridgeRate = 0.0005f;
                    cookedAfter = 30f;
                    burnedAfter = 10f;
                    cookedEffects = Effect(
                        true,
                        new Vector4(-42f, 4f, -15f, 0.034f));
                    // Burned/spoiled consequences are explicit provisional
                    // remake targets because the captured donor table has no
                    // normalized coefficients for these two branches yet.
                    burnedEffects = Effect(
                        true,
                        new Vector4(-5f, 12f, 10f, 0.034f),
                        dirtiness: 2f);
                    spoiledEffects = SpoiledEffect(0.034f);
                    break;
                case "P1.ITEM.117":
                    SetOrdinaryFood(
                        false,
                        0f,
                        0f,
                        out edible,
                        out perishable,
                        out consumeWhole,
                        out duration,
                        out ambientRate,
                        out fridgeRate);
                    break;
                case "P1.ITEM.118":
                    edible = true;
                    perishable = true;
                    cookable = true;
                    consumeWhole = true;
                    duration = 1.35f;
                    ambientRate = 0.033f;
                    fridgeRate = 0.0018f;
                    cookedAfter = 240f;
                    burnedAfter = 20f;
                    burnedEffects = Effect(
                        true,
                        new Vector4(-10f, 18f, 12f, 0.165f),
                        dirtiness: 2f);
                    spoiledEffects = SpoiledEffect(0.165f);
                    break;
                case "P1.ITEM.119":
                    edible = true;
                    perishable = true;
                    cookable = true;
                    consumeWhole = true;
                    duration = 1.35f;
                    ambientRate = 0.031f;
                    fridgeRate = 0.0012f;
                    cookedAfter = 240f;
                    burnedAfter = 20f;
                    burnedEffects = Effect(
                        true,
                        new Vector4(-12f, 24f, 15f, 0.41f),
                        dirtiness: 3f);
                    spoiledEffects = SpoiledEffect(0.41f);
                    break;
            }

            var food = new ItemFoodDefinition();
            food.ConfigureForAuthoring(
                edible,
                perishable,
                cookable,
                consumeWhole,
                duration,
                ambientRate,
                fridgeRate,
                cookedAfter,
                burnedAfter,
                cookedEffects,
                burnedEffects,
                spoiledEffects,
                Color.white,
                new Color(0.72f, 0.42f, 0.2f, 1f),
                new Color(0.12f, 0.08f, 0.05f, 1f),
                new Color(0.42f, 0.62f, 0.25f, 1f),
                consumptionPresentation);
            return food;
        }

        private static ItemHeatSourceDefinition BuildHeatSourceDefinition(
            string featureId)
        {
            var heat = new ItemHeatSourceDefinition();
            if (string.Equals(
                    featureId,
                    "P1.ITEM.149",
                    StringComparison.Ordinal))
            {
                heat.ConfigureForAuthoring(
                    configuredProvidesHeat: true,
                    configuredRequiresEnabled: true,
                    configuredCookingRate: 1f,
                    // Donor FireTrigger relative to the grill root is
                    // (-0.017, 0, 0.111). The imported grill's local +Z is
                    // world-up, so cooking and VFX volumes must follow it.
                    configuredLocalCenter: new Vector3(-0.017f, 0f, 0.111f),
                    configuredLocalSize: new Vector3(0.5f, 0.5f, 0.24f),
                    configuredLocalUp: Vector3.forward);
            }

            return heat;
        }

        private static ItemCombustionDefinition BuildCombustionDefinition(
            string featureId)
        {
            var combustion = new ItemCombustionDefinition();
            if (string.Equals(
                    featureId,
                    "P1.ITEM.149",
                    StringComparison.Ordinal))
            {
                // Locked donor SetFire/charcoal FSM values: >10 units to
                // ignite, stop at <=5, 120 s visible flame, then embers.
                combustion.ConfigureForAuthoring(
                    configuredFuelDefinitionId: "item.charcoal",
                    configuredBurnTimeStateId: "burn-time",
                    configuredWetStateId: "wet",
                    configuredMinimumFuelToIgnite: 10f,
                    configuredExtinguishFuelThreshold: 5f,
                    configuredActiveDurationSeconds: 120f,
                    configuredActiveFuelPerSecond: 0.1f,
                    configuredEmberFuelPerSecond: 0.04f,
                    configuredFuelPourRatePerSecond: 12f,
                    configuredMinimumFuelPourTiltDegrees: 80f,
                    configuredFirePresentationScale: 0.48f,
                    configuredFireEmissionMultiplier: 0.16f,
                    configuredFireLightIntensity: 145f,
                    configuredFireLightRange: 1.65f);
            }

            return combustion;
        }

        private static ItemColliderShapeDefinition[] BuildColliderShapes(
            string featureId)
        {
            switch (featureId)
            {
                case "P1.ITEM.101":
                    return Shapes(Box(
                        new Vector3(-0.00004755f, 0.0001345f, -0.000886f),
                        new Vector3(0.205247f, 0.21296501f, 0.024234008f)));
                case "P1.ITEM.102":
                    return Shapes(Box(
                        new Vector3(0.00000095f, 0f, 0f),
                        new Vector3(0.130412f, 0.099876f, 0.039598003f)));
                case "P1.ITEM.103":
                    return Shapes(Box(
                        new Vector3(-0.00020959f, -0.011435f, -0.0031665f),
                        new Vector3(0.217957f, 0.027544003f, 0.268697f)));
                case "P1.ITEM.105":
                    return Shapes(Box(
                        new Vector3(0.00000095f, 0f, -0.00000003f),
                        new Vector3(0.07326601f, 0.073616005f, 0.241678f)));
                case "P1.ITEM.113":
                    return Shapes(Box(
                        new Vector3(0f, 0f, -0.00439103f),
                        new Vector3(0.4f, 0.27000007f, 0.20000005f)));
                case "P1.ITEM.116":
                    return Shapes(Capsule(Vector3.zero, 0.025f, 0.16f, 2));
                case "P1.ITEM.135":
                    return Shapes(
                        Box(
                            new Vector3(0f, 0.33f, -0.07f),
                            new Vector3(0.03f, 0.12f, 0.226201f)),
                        Capsule(
                            new Vector3(0f, -0.02f, 0f),
                            0.03f,
                            0.81f,
                            1));
                case "P1.ITEM.171":
                    return Shapes(Sphere(Vector3.zero, 0.12f));
                case "P1.ITEM.144":
                    return Shapes(
                        Box(
                            new Vector3(-0.08f, -0.1f, 0.06f),
                            new Vector3(0.04f, 0.04f, 0.12f)),
                        Box(
                            new Vector3(0f, 0f, -0.0029565f),
                            new Vector3(0.35f, 0.35f, 0.02f)));
                case "P1.ITEM.158":
                    return Shapes(Box(
                        new Vector3(0.01f, 0.000639f, -0.0026775f),
                        new Vector3(0.04f, 0.12790601f, 0.066645f)));
                case "P1.ITEM.140":
                    return Shapes(
                        Capsule(Vector3.zero, 0.02f, 0.51f, 0),
                        Box(
                            new Vector3(0f, -0.005f, 0f),
                            new Vector3(0.4f, 0.04f, 0.1f)));
                case "P1.ITEM.151":
                    return Shapes(Box(
                        new Vector3(0f, 0f, 0.007f),
                        new Vector3(0.075f, 0.075f, 0.1f)));
                case "P1.ITEM.150":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.19f, 0.19f, 0.163935f)));
                case "P1.ITEM.134":
                case "P1.ITEM.133":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.16f, 0.35000014f, 0.47f)));
                case "P1.ITEM.136":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.028f, 0.028f, 1.5f)));
                case "P1.ITEM.146":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.098596f, 0.44669813f, 0.04429201f)));
                case "P1.ITEM.159":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.09f, 0.094f, 0.006f)));
                case "P1.ITEM.153":
                    return Shapes(Box(
                        new Vector3(0f, 0f, 0.00000006f),
                        new Vector3(0.09f, 0.17f, 0.12f)));
                case "P1.ITEM.141":
                    return Shapes(Box(
                        new Vector3(0f, -0.07f, 0f),
                        new Vector3(0.17f, 0.04f, 0.7f)));
                case "P1.ITEM.154":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.1608f, 0.16590798f, 0.30882797f)));
                case "P1.ITEM.142":
                    return Shapes(Box(
                        new Vector3(0f, 0.92f, 0.71f),
                        new Vector3(0.1f, 0.1f, 1.5f)));
                case "P1.ITEM.161":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.1f, 0.15f, 0.016f)));
                case "P1.ITEM.162":
                    return Shapes(Box(
                        Vector3.zero,
                        new Vector3(0.21f, 0.02f, 0.3f),
                        trigger: true));
                case "P1.ITEM.156":
                    return Shapes(Box(
                        new Vector3(0.000233f, 0.00000155f, -0.00903f),
                        new Vector3(0.10342804f, 0.088800065f, 0.056430023f)));
                case "P1.ITEM.157":
                    return Shapes(Box(
                        new Vector3(0f, -0.005f, -0.03f),
                        new Vector3(0.56f, 0.155f, 0.29f)));
                case "P1.ITEM.137":
                    return Shapes(
                        Capsule(
                            new Vector3(0f, -0.1f, 0.008f),
                            0.03f,
                            1f,
                            1),
                        Box(
                            new Vector3(0f, 0.335f, 0.021f),
                            new Vector3(0.09f, 0.09f, 0.22f)));
                case "P1.ITEM.170":
                    return Shapes(Box(
                        new Vector3(-0.0000019f, 0.075f, -0.45f),
                        new Vector3(1.1f, 0.65f, 0.22f)));
                case "P1.ITEM.138":
                    return Shapes(Box(
                        new Vector3(0.013f, 0f, 0.012f),
                        new Vector3(0.29f, 0.397777f, 0.075f)));
                case "P1.ITEM.163":
                    return Shapes(Box(
                        new Vector3(-0.0069555f, 0.000906f, -0.0030025f),
                        new Vector3(0.20016101f, 0.07181202f, 0.025087016f)));
                case "P1.ITEM.145":
                    return Shapes(
                        Box(
                            new Vector3(0f, 0f, 0.06f),
                            new Vector3(0.23f, 0.23f, 0.18f)),
                        Box(
                            new Vector3(0.12f, 0f, -0.07f),
                            new Vector3(0.03f, 0.08f, 0.15f)));
                case "P1.ITEM.139":
                    return Shapes(Box(
                        new Vector3(0f, 0f, 0.013f),
                        new Vector3(0.09f, 0.09f, 0.055f)));
                case "P1.ITEM.147":
                    return Shapes(
                        Box(
                            new Vector3(0f, -0.181176f, 0f),
                            new Vector3(0.45f, 0.09f, 0.45f)),
                        Box(
                            new Vector3(0f, 0.166197f, 0f),
                            new Vector3(0.45f, 0.78f, 0.07f)));
                default:
                    return Array.Empty<ItemColliderShapeDefinition>();
            }
        }

        private static ItemColliderShapeDefinition[] Shapes(
            params ItemColliderShapeDefinition[] values) => values;

        private static ItemColliderShapeDefinition Box(
            Vector3 center,
            Vector3 size,
            bool trigger = false) => Shape(
                ItemColliderShapeKind.Box,
                center,
                size,
                0.05f,
                0.1f,
                1,
                trigger);

        private static ItemColliderShapeDefinition Sphere(
            Vector3 center,
            float radius) => Shape(
                ItemColliderShapeKind.Sphere,
                center,
                Vector3.one * radius * 2f,
                radius,
                radius * 2f,
                1,
                false);

        private static ItemColliderShapeDefinition Capsule(
            Vector3 center,
            float radius,
            float height,
            int direction) => Shape(
                ItemColliderShapeKind.Capsule,
                center,
                Vector3.one * radius * 2f,
                radius,
                height,
                direction,
                false);

        private static ItemColliderShapeDefinition Shape(
            ItemColliderShapeKind kind,
            Vector3 center,
            Vector3 size,
            float radius,
            float height,
            int direction,
            bool trigger)
        {
            var shape = new ItemColliderShapeDefinition();
            shape.ConfigureForAuthoring(
                kind,
                center,
                size,
                radius,
                height,
                direction,
                trigger);
            return shape;
        }

        private static void SetOrdinaryFood(
            bool perishes,
            float configuredAmbientRate,
            float configuredFridgeRate,
            out bool edible,
            out bool perishable,
            out bool consumeWhole,
            out float duration,
            out float ambientRate,
            out float fridgeRate)
        {
            edible = true;
            perishable = perishes;
            consumeWhole = true;
            duration = 1.35f;
            ambientRate = configuredAmbientRate;
            fridgeRate = configuredFridgeRate;
        }

        private static FoodEffectDefinition SpoiledEffect(float weight)
        {
            return Effect(
                true,
                new Vector4(12f, 18f, 25f, weight),
                dirtiness: 5f);
        }

        private static FoodEffectDefinition Effect(
            bool overridesBase = false,
            Vector4 primary = default,
            float intoxication = 0f,
            float urine = 0f,
            float fatigue = 0f,
            float dirtiness = 0f)
        {
            var effect = new FoodEffectDefinition();
            effect.ConfigureForAuthoring(
                overridesBase,
                primary,
                intoxication,
                urine,
                fatigue,
                dirtiness);
            return effect;
        }

        private static void AddSpawnedChildDefinitions(
            ICollection<ItemDefinitionRecord> definitions)
        {
            foreach ((string id, string name, string parentFeature, float mass) in new[]
            {
                ("item.spark-plug", "Spark plug", "P1.ITEM.126", 0.05f),
                ("item.light-bulb", "Light bulb", "P1.ITEM.127", 0.04f),
                ("item.fuse-unit", "Fuse", "P1.ITEM.128", 0.01f),
                ("item.r20-battery-unit", "R20 battery unit", "P1.ITEM.129", 0.12f),
            })
            {
                var definition = new ItemDefinitionRecord();
                ItemScalarDefinition[] scalars = id == "item.r20-battery-unit"
                    ? new[] { Scalar("charge", 0f, 100f, 100f) }
                    : Array.Empty<ItemScalarDefinition>();
                definition.ConfigureForAuthoring(
                    id,
                    parentFeature + ".child",
                    name,
                    "SpawnedUnit",
                    parentFeature,
                    "Frozen prefab child evidence; normalized in M09B evidence report",
                    "legacy." + id,
                    false,
                    ItemCalibrationStatus.EvidenceBacked,
                    ItemContentMeasure.None,
                    ItemPrimaryAction.None,
                    0f,
                    0f,
                    0f,
                    mass,
                    0f,
                    35f,
                    Vector3.one * 0.08f,
                    false,
                    false,
                    true,
                    false,
                    string.Empty,
                    true,
                    0,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Array.Empty<string>(),
                    Vector4.zero,
                    0f,
                    scalars,
                    Array.Empty<ItemFlagDefinition>());
                definitions.Add(definition);
            }
        }

        private static void AddExpandedShopDefinitions(
            ICollection<ItemDefinitionRecord> definitions)
        {
            foreach (ExpandedShopItemSeed seed in ExpandedShopItemSeeds)
            {
                var definition = new ItemDefinitionRecord();
                definition.ConfigureForAuthoring(
                    seed.DefinitionId,
                    seed.FeatureId,
                    seed.DisplayName,
                    seed.Category,
                    "EXT.EXPANDED_SHOP",
                    seed.Evidence,
                    "extension.expanded-shop." + seed.Slug,
                    false,
                    ItemCalibrationStatus.Provisional,
                    seed.Measure,
                    seed.Action,
                    seed.MaximumContent,
                    seed.InitialContent,
                    seed.UseAmount,
                    seed.MassKilograms,
                    seed.EmptyContainerMassKilograms,
                    35f,
                    seed.ProxySize,
                    false,
                    false,
                    true,
                    false,
                    string.Empty,
                    false,
                    seed.InitialChildCount,
                    seed.ChildDefinitionId,
                    string.Empty,
                    seed.ToolType,
                    Array.Empty<string>(),
                    seed.NeedEffects,
                    0f,
                    Array.Empty<ItemScalarDefinition>(),
                    Array.Empty<ItemFlagDefinition>());
                definition.ConfigureLifeEffectsForAuthoring(
                    seed.UrineEffect,
                    0f,
                    0f);
                definition.ConfigureFoodForAuthoring(
                    seed.Food,
                    new ItemHeatSourceDefinition());
                definition.ConfigurePhysicalShapesForAuthoring(
                    Array.Empty<ItemColliderShapeDefinition>());
                definitions.Add(definition);
            }
        }

        private static ExpandedShopItemSeed ExpandedUtility(
            int ordinal,
            string slug,
            string displayName,
            string category,
            float massKilograms,
            Vector3 proxySize,
            ItemContentMeasure measure = ItemContentMeasure.None,
            float maximumContent = 0f,
            string toolType = "") =>
            new(
                ordinal,
                slug,
                displayName,
                category,
                measure,
                ItemPrimaryAction.None,
                maximumContent,
                maximumContent,
                0f,
                massKilograms,
                0f,
                proxySize,
                0,
                string.Empty,
                toolType,
                Vector4.zero,
                0f,
                new ItemFoodDefinition());

        private static ExpandedShopItemSeed ExpandedConsumable(
            int ordinal,
            string slug,
            string displayName,
            string category,
            float massKilograms,
            Vector3 proxySize,
            Vector4 needEffects,
            ItemFoodDefinition food,
            float urineEffect = 0f,
            float emptyContainerMassKilograms = 0f) =>
            new(
                ordinal,
                slug,
                displayName,
                category,
                ItemContentMeasure.Condition,
                ItemPrimaryAction.Consume,
                100f,
                100f,
                100f,
                massKilograms,
                emptyContainerMassKilograms,
                proxySize,
                0,
                string.Empty,
                string.Empty,
                needEffects,
                urineEffect,
                food);

        private static ExpandedShopItemSeed ExpandedPackage(
            int ordinal,
            string slug,
            string displayName,
            float massKilograms,
            Vector3 proxySize,
            int initialChildCount,
            string childDefinitionId,
            ItemFoodDefinition food) =>
            new(
                ordinal,
                slug,
                displayName,
                "FoodPackage",
                ItemContentMeasure.Units,
                ItemPrimaryAction.DispenseChild,
                initialChildCount,
                initialChildCount,
                1f,
                massKilograms,
                0.08f,
                proxySize,
                initialChildCount,
                childDefinitionId,
                string.Empty,
                Vector4.zero,
                0f,
                food);

        private static ItemFoodDefinition ExpandedFood(
            bool edible = true,
            bool perishable = false,
            bool cookable = false,
            float ambientRate = 0f,
            float refrigeratedRate = 0f,
            float cookedAfterSeconds = 0f,
            float burnedAfterAdditionalSeconds = 0f,
            float consumptionDurationSeconds = 1.35f,
            FoodEffectDefinition cookedEffects = null,
            FoodEffectDefinition burnedEffects = null,
            FoodEffectDefinition spoiledEffects = null,
            ItemConsumptionPresentation consumptionPresentation =
                ItemConsumptionPresentation.None)
        {
            var food = new ItemFoodDefinition();
            food.ConfigureForAuthoring(
                edible,
                perishable,
                cookable,
                edible,
                consumptionDurationSeconds,
                ambientRate,
                refrigeratedRate,
                cookedAfterSeconds,
                burnedAfterAdditionalSeconds,
                cookedEffects ?? Effect(),
                burnedEffects ?? Effect(),
                spoiledEffects ?? Effect(),
                Color.white,
                new Color(0.72f, 0.42f, 0.2f, 1f),
                new Color(0.12f, 0.08f, 0.05f, 1f),
                new Color(0.42f, 0.62f, 0.25f, 1f),
                consumptionPresentation);
            return food;
        }

        private static void AddHomeMailOrderDefinitions(
            ICollection<ItemDefinitionRecord> definitions)
        {
            AddMailOrderDefinition(
                definitions,
                HomePartsMailOrderCatalog.EnvelopeDefinitionId,
                "P1.COMMS.005.envelope",
                "Конверт с заказом",
                "MailOrderEnvelope",
                "P1.COMMS.005",
                "ITEMS/parts magazine(itemx)/EnvelopeSpawn/envelope(xxxxx)",
                0.02f,
                new Vector3(0.21f, 0.012f, 0.15f));

            int featureOrdinal = 1;
            foreach (HomePartsMailOrderOffer offer in
                     HomePartsMailOrderCatalog.Offers)
            {
                AddMailOrderDefinition(
                    definitions,
                    offer.ItemDefinitionId,
                    $"P1.ITEM.017.catalog.{featureOrdinal:00}",
                    offer.DisplayName,
                    "OrderedVehiclePart",
                    "P1.ITEM.017",
                    "Donor home parts magazine preFill offer and Delivery/Purchased package flow",
                    ResolveMailOrderMass(offer.Id),
                    ResolveMailOrderProxySize(offer.Id));
                featureOrdinal++;
            }
        }

        private static void AddMailOrderDefinition(
            ICollection<ItemDefinitionRecord> definitions,
            string definitionId,
            string featureId,
            string displayName,
            string category,
            string parentFeatureId,
            string donorEvidence,
            float massKilograms,
            Vector3 proxySize)
        {
            var definition = new ItemDefinitionRecord();
            definition.ConfigureForAuthoring(
                definitionId,
                featureId,
                displayName,
                category,
                parentFeatureId,
                donorEvidence,
                "legacy." + definitionId,
                true,
                ItemCalibrationStatus.Provisional,
                ItemContentMeasure.None,
                ItemPrimaryAction.None,
                0f,
                0f,
                0f,
                massKilograms,
                0f,
                35f,
                proxySize,
                false,
                false,
                true,
                false,
                string.Empty,
                true,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Vector4.zero,
                0f,
                Array.Empty<ItemScalarDefinition>(),
                Array.Empty<ItemFlagDefinition>());
            definitions.Add(definition);
        }

        private static float ResolveMailOrderMass(string offerId)
        {
            if (offerId.Contains("wheelset", StringComparison.Ordinal) ||
                offerId.Contains("suspension", StringComparison.Ordinal))
            {
                return 20f;
            }

            if (offerId.Contains("seat", StringComparison.Ordinal) ||
                offerId.Contains("hood", StringComparison.Ordinal))
            {
                return 10f;
            }

            return 3f;
        }

        private static Vector3 ResolveMailOrderProxySize(string offerId)
        {
            if (offerId.Contains("wheelset", StringComparison.Ordinal))
            {
                return new Vector3(0.62f, 0.42f, 0.62f);
            }

            if (offerId.Contains("hood", StringComparison.Ordinal) ||
                offerId.Contains("spoiler", StringComparison.Ordinal))
            {
                return new Vector3(0.9f, 0.12f, 0.38f);
            }

            return new Vector3(0.42f, 0.22f, 0.32f);
        }

        private static ItemPlacementRecord[] BuildPlacements(
            CsvTable world,
            IReadOnlyDictionary<string, ItemDefinitionRecord> definitionsByFeature)
        {
            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
            var placements = new List<ItemPlacementRecord>();
            foreach (CsvRow row in world.Rows.Where(candidate =>
                         IsTopLevelItem(candidate["HierarchyPath"])))
            {
                string rootName = row["HierarchyPath"].Substring("ITEMS/".Length);
                if (!RootFeatureIds.TryGetValue(rootName, out string featureId) ||
                    !definitionsByFeature.TryGetValue(
                        featureId,
                        out ItemDefinitionRecord definition))
                {
                    throw new InvalidDataException(
                        $"No project-owned item definition mapping exists for '{rootName}'.");
                }

                int ordinal = ordinals.TryGetValue(
                        definition.DefinitionId,
                        out int existing)
                    ? existing + 1
                    : 1;
                ordinals[definition.DefinitionId] = ordinal;
                string placementId =
                    $"placement.{definition.DefinitionId.Substring(5)}.{ordinal:00}";
                var placement = new ItemPlacementRecord();
                placement.ConfigureForAuthoring(
                    placementId,
                    ItemStableIdUtility.CreateDeterministic(placementId).Value,
                    definition.DefinitionId,
                    row.Vector3(
                        "ConvertedPositionX",
                        "ConvertedPositionY",
                        "ConvertedPositionZ"),
                    row.Quaternion(
                        "SourceRotationX",
                        "SourceRotationY",
                        "SourceRotationZ",
                        "SourceRotationW"),
                    row["StableId"],
                    row["SourceObjectId"],
                    row["HierarchyPath"],
                    row["SourceSha256"],
                    definition.DefinitionId == "item.floppy-disk"
                        ? ordinal - 1
                        : 0);
                ConfigurePlacementPhysics(placement, rootName);
                placements.Add(placement);
            }

            return placements
                .OrderBy(placement => placement.PlacementId, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ConfigurePlacementPhysics(
            ItemPlacementRecord placement,
            string donorRootName)
        {
            bool kinematic =
                string.Equals(donorRootName, "macaron boxx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(donorRootName, "sausagesx0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(donorRootName, "pizzax", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(donorRootName, "milkx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(donorRootName, "beercase0", StringComparison.OrdinalIgnoreCase);
            bool motorHoist = string.Equals(
                donorRootName,
                "motor hoist(itemx)",
                StringComparison.OrdinalIgnoreCase);
            bool floorJack = string.Equals(
                donorRootName,
                "floor jack(itemx)",
                StringComparison.OrdinalIgnoreCase);
            bool partsMagazine = string.Equals(
                donorRootName,
                "parts magazine(itemx)",
                StringComparison.OrdinalIgnoreCase);

            float linearDamping = 0f;
            float angularDamping = 0.05f;
            switch (donorRootName.ToLowerInvariant())
            {
                case "motor hoist(itemx)":
                    linearDamping = 99999f;
                    angularDamping = 99999f;
                    break;
                case "floor jack(itemx)":
                    linearDamping = 9999f;
                    angularDamping = 9999f;
                    break;
                case "coffee pan(itemx)":
                case "basketball(clone)":
                    linearDamping = 0.05f;
                    angularDamping = 0.05f;
                    break;
                case "sofa(itemx)":
                    linearDamping = 0.1f;
                    angularDamping = 0.1f;
                    break;
                case "coffee cup(itemx)":
                    linearDamping = 0.5f;
                    angularDamping = 0.5f;
                    break;
                case "notepad(itemx)":
                    linearDamping = 0.08f;
                    angularDamping = 0.08f;
                    break;
            }

            placement.ConfigurePhysicsForAuthoring(
                Vector3.one,
                "9c88c287a45c52e91f6ac6b345b20e10",
                motorHoist || floorJack ? 15 : partsMagazine ? 0 : 19,
                !partsMagazine,
                configuredUseGravity: true,
                configuredIsKinematic: kinematic || partsMagazine,
                configuredDetectCollisions: true,
                configuredCollisionMode: motorHoist
                    ? CollisionDetectionMode.Discrete
                    : CollisionDetectionMode.Continuous,
                configuredInterpolation: RigidbodyInterpolation.None,
                configuredConstraints: motorHoist
                    ? (RigidbodyConstraints)52
                    : floorJack
                        ? RigidbodyConstraints.FreezePositionY |
                            RigidbodyConstraints.FreezeRotationX |
                            RigidbodyConstraints.FreezeRotationZ
                        : RigidbodyConstraints.None,
                configuredLinearDamping: linearDamping,
                configuredAngularDamping: angularDamping);
        }

        private static void AssertCatalogs(
            ItemDefinitionCatalog definitions,
            ItemPlacementCatalog placements)
        {
            IReadOnlyList<string> failures = definitions.ValidateConfiguration()
                .Concat(placements.ValidateConfiguration(definitions))
                .ToArray();
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Generated 09B catalogs are invalid: " +
                    string.Join(" | ", failures));
            }

            int requiredRosterCount = CsvTable.Read(RosterPath).Rows.Count(row =>
                string.Equals(row["Required"], "Yes", StringComparison.Ordinal));
            int generatedRequiredCount = definitions.Definitions.Count(definition =>
                definition.Required);
            int expectedRequiredCount =
                requiredRosterCount + MailOrderRequiredDefinitionCount;
            if (requiredRosterCount != 79 ||
                generatedRequiredCount != expectedRequiredCount ||
                placements.Placements.Count != CanonicalPlacementCount)
            {
                throw new InvalidOperationException(
                    $"09B closure drifted: required={generatedRequiredCount}/" +
                    $"{requiredRosterCount}, placements={placements.Placements.Count}/" +
                    $"{CanonicalPlacementCount}.");
            }
        }

        private static void BindBootstrap(
            ItemDefinitionCatalog definitions,
            ItemPlacementCatalog placements)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller installer = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    ProductionWorldStreamingInstaller>(true))
                .Single();
            installer.ConfigureItemsForAuthoring(definitions, placements);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string NormalizeEvidence(string evidence)
        {
            if (!evidence.StartsWith("WOP:", StringComparison.Ordinal))
            {
                return evidence;
            }

            return evidence +
                   " (legacy row reference; normalized placement stores " +
                   "SourceObjectId + StableId + hierarchy path)";
        }

        private static bool IsTopLevelItem(string path) =>
            path.StartsWith("ITEMS/", StringComparison.Ordinal) &&
            path.IndexOf('/', "ITEMS/".Length) < 0;

        private static string Slug(string value)
        {
            var result = new StringBuilder();
            bool pendingDash = false;
            foreach (char character in value.ToLowerInvariant())
            {
                if (character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9')
                {
                    if (pendingDash && result.Length > 0)
                    {
                        result.Append('-');
                    }

                    result.Append(character);
                    pendingDash = false;
                }
                else
                {
                    pendingDash = true;
                }
            }

            return result.ToString().Trim('-');
        }

        private static ItemScalarDefinition Scalar(
            string id,
            float minimum,
            float maximum,
            float initial)
        {
            var result = new ItemScalarDefinition();
            result.ConfigureForAuthoring(id, minimum, maximum, initial);
            return result;
        }

        private sealed class ExpandedShopItemSeed
        {
            public ExpandedShopItemSeed(
                int ordinal,
                string slug,
                string displayName,
                string category,
                ItemContentMeasure measure,
                ItemPrimaryAction action,
                float maximumContent,
                float initialContent,
                float useAmount,
                float massKilograms,
                float emptyContainerMassKilograms,
                Vector3 proxySize,
                int initialChildCount,
                string childDefinitionId,
                string toolType,
                Vector4 needEffects,
                float urineEffect,
                ItemFoodDefinition food)
            {
                Slug = slug;
                DefinitionId = "item." + slug;
                FeatureId = $"EXT.EXPANDED_SHOP.ITEM.{ordinal:000}";
                DisplayName = displayName;
                Category = category;
                Measure = measure;
                Action = action;
                MaximumContent = maximumContent;
                InitialContent = initialContent;
                UseAmount = useAmount;
                MassKilograms = massKilograms;
                EmptyContainerMassKilograms =
                    emptyContainerMassKilograms;
                ProxySize = proxySize;
                InitialChildCount = initialChildCount;
                ChildDefinitionId = childDefinitionId ?? string.Empty;
                ToolType = toolType ?? string.Empty;
                NeedEffects = needEffects;
                UrineEffect = urineEffect;
                Food = food ?? new ItemFoodDefinition();
                Evidence =
                    $"ExpandedShop.dll SHA-256 {ExpandedShopDllSha256}; " +
                    $"embedded drinks AssetBundle SHA-256 {ExpandedShopBundleSha256}; " +
                    $"{displayName} prefab and managed configuration inspected read-only";
            }

            public string Slug { get; }
            public string DefinitionId { get; }
            public string FeatureId { get; }
            public string DisplayName { get; }
            public string Category { get; }
            public ItemContentMeasure Measure { get; }
            public ItemPrimaryAction Action { get; }
            public float MaximumContent { get; }
            public float InitialContent { get; }
            public float UseAmount { get; }
            public float MassKilograms { get; }
            public float EmptyContainerMassKilograms { get; }
            public Vector3 ProxySize { get; }
            public int InitialChildCount { get; }
            public string ChildDefinitionId { get; }
            public string ToolType { get; }
            public Vector4 NeedEffects { get; }
            public float UrineEffect { get; }
            public ItemFoodDefinition Food { get; }
            public string Evidence { get; }
        }

        private sealed class DefinitionPlan
        {
            public ItemCalibrationStatus Calibration =
                ItemCalibrationStatus.Provisional;
            public ItemContentMeasure Measure = ItemContentMeasure.None;
            public ItemPrimaryAction Action = ItemPrimaryAction.None;
            public float Maximum;
            public float Initial;
            public float UseAmount;
            public float Mass = 0.5f;
            public float EmptyContainerMass;
            public float MaximumCarryMass = 35f;
            public Vector3 ProxySize = Vector3.one * 0.25f;
            public bool CanOpen;
            public bool StartsOpen;
            public bool RetainWhenEmpty = true;
            public bool SupportsLiquid;
            public string InitialLiquidId = string.Empty;
            public bool CriticalRecovery;
            public int InitialChildCount;
            public string ChildDefinitionId = string.Empty;
            public string ProducedDefinitionId = string.Empty;
            public string ToolType = string.Empty;
            public string[] ToolVariants = Array.Empty<string>();
            public Vector4 NeedEffects;
            public float IntoxicationEffect;
            public float UrineEffect;
            public float FatigueEffect;
            public float DirtinessEffect;
            public readonly List<ScalarPlan> Scalars = new List<ScalarPlan>();
            public readonly List<FlagPlan> Flags = new List<FlagPlan>();

            public static DefinitionPlan Create(string category)
            {
                var plan = new DefinitionPlan();
                switch (category)
                {
                    case "Food":
                    case "DrinkFood":
                    case "Ingredient":
                    case "Consumable":
                    case "FuelConsumable":
                    case "Perishable":
                        plan.Mass = 0.5f;
                        plan.SetContent(ItemContentMeasure.Units, 1f, 1f, 1f);
                        plan.Action = ItemPrimaryAction.Consume;
                        break;
                    case "DrinkContainer":
                        plan.Mass = 1f;
                        plan.SetLiquid(1f, 1f, "liquid.unresolved", 0.1f);
                        plan.Action = ItemPrimaryAction.Consume;
                        break;
                    case "ServiceFluid":
                        plan.Mass = 4f;
                        plan.SetLiquid(1f, 1f, "liquid.unresolved", 0.1f);
                        plan.CriticalRecovery = true;
                        break;
                    case "ToolContainer":
                        plan.Mass = 3f;
                        plan.SetLiquid(1f, 1f, "liquid.unresolved", 0.1f);
                        plan.CriticalRecovery = true;
                        break;
                    case "Container":
                        plan.Mass = 2f;
                        plan.Action = ItemPrimaryAction.ToggleOpen;
                        plan.CanOpen = true;
                        break;
                    case "CargoContainer":
                        plan.Mass = 10f;
                        plan.Action = ItemPrimaryAction.ToggleOpen;
                        plan.CanOpen = true;
                        break;
                    case "ConsumableContainer":
                        plan.Mass = 3f;
                        plan.Action = ItemPrimaryAction.ToggleOpen;
                        plan.CanOpen = true;
                        break;
                    case "PhysicalMediaContainer":
                        plan.Mass = 0.5f;
                        plan.Action = ItemPrimaryAction.ToggleOpen;
                        plan.CanOpen = true;
                        break;
                    case "Device":
                        plan.Mass = 6f;
                        plan.Action = ItemPrimaryAction.ToggleDevice;
                        break;
                    case "PortableDevice":
                        plan.Mass = 1f;
                        plan.Action = ItemPrimaryAction.ToggleDevice;
                        break;
                    case "Tool":
                        plan.Mass = 2f;
                        plan.ToolType = "Generic";
                        plan.CriticalRecovery = true;
                        break;
                    case "MovableCargo":
                        plan.Mass = 40f;
                        plan.MaximumCarryMass = 35f;
                        plan.ProxySize = new Vector3(1.4f, 0.8f, 0.7f);
                        break;
                    case "VehiclePartSupply":
                        plan.Mass = 1f;
                        break;
                    case "Battery":
                        plan.Mass = 8f;
                        break;
                    case "SafetySupply":
                        plan.Mass = 5f;
                        break;
                    case "ServiceSupply":
                    case "ContainerPart":
                    case "WearableSafety":
                        plan.Mass = 1f;
                        break;
                    case "PhysicalMedia":
                        plan.Mass = 0.2f;
                        break;
                    case "Readable":
                    case "Decoration":
                    case "Candidate":
                        plan.Mass = 0.5f;
                        break;
                    case "SportsItem":
                        plan.Mass = 0.6f;
                        break;
                    case "Cargo":
                        plan.Mass = 4f;
                        break;
                    case "StoryCargo":
                        plan.Mass = 18f;
                        break;
                }

                return plan;
            }

            public void SetContent(
                ItemContentMeasure measure,
                float maximum,
                float initial,
                float useAmount)
            {
                Measure = measure;
                Maximum = maximum;
                Initial = initial;
                UseAmount = useAmount;
                SupportsLiquid = false;
                InitialLiquidId = string.Empty;
                Action = ItemPrimaryAction.Consume;
            }

            public void SetLiquid(
                float capacity,
                float initial,
                string liquidId,
                float transferAmount)
            {
                Measure = ItemContentMeasure.Litres;
                Maximum = capacity;
                Initial = initial;
                UseAmount = transferAmount;
                SupportsLiquid = true;
                InitialLiquidId = liquidId ?? string.Empty;
                CanOpen = true;
                StartsOpen = false;
                Action = ItemPrimaryAction.ToggleOpen;
            }

            public void SetChildPackage(int count, string childDefinition)
            {
                Measure = ItemContentMeasure.Units;
                Maximum = count;
                Initial = count;
                UseAmount = 1f;
                Action = ItemPrimaryAction.DispenseChild;
                InitialChildCount = count;
                ChildDefinitionId = childDefinition;
            }

            public void AddScalar(
                string id,
                float minimum,
                float maximum,
                float initial) =>
                Scalars.Add(new ScalarPlan(id, minimum, maximum, initial));

            public void AddFlag(string id, bool initial) =>
                Flags.Add(new FlagPlan(id, initial));
        }

        private readonly struct ScalarPlan
        {
            public ScalarPlan(string id, float minimum, float maximum, float initial)
            {
                Id = id;
                Minimum = minimum;
                Maximum = maximum;
                Initial = initial;
            }

            private string Id { get; }
            private float Minimum { get; }
            private float Maximum { get; }
            private float Initial { get; }

            public ItemScalarDefinition Build() =>
                Scalar(Id, Minimum, Maximum, Initial);
        }

        private readonly struct FlagPlan
        {
            public FlagPlan(string id, bool initial)
            {
                Id = id;
                Initial = initial;
            }

            private string Id { get; }
            private bool Initial { get; }

            public ItemFlagDefinition Build()
            {
                var result = new ItemFlagDefinition();
                result.ConfigureForAuthoring(Id, Initial);
                return result;
            }
        }

        private sealed class CsvTable
        {
            public List<CsvRow> Rows { get; } = new List<CsvRow>();

            public static CsvTable Read(string projectPath)
            {
                string absolute = Path.GetFullPath(projectPath);
                if (!File.Exists(absolute))
                {
                    throw new FileNotFoundException(projectPath);
                }

                List<List<string>> records = Parse(
                    File.ReadAllText(absolute, Encoding.UTF8));
                if (records.Count < 2)
                {
                    throw new InvalidDataException(
                        $"CSV '{projectPath}' has no data rows.");
                }

                string[] headers = records[0].ToArray();
                var table = new CsvTable();
                for (int index = 1; index < records.Count; index++)
                {
                    if (records[index].Count == 1 &&
                        string.IsNullOrEmpty(records[index][0]))
                    {
                        continue;
                    }

                    table.Rows.Add(new CsvRow(headers, records[index]));
                }

                return table;
            }

            private static List<List<string>> Parse(string text)
            {
                var records = new List<List<string>>();
                var record = new List<string>();
                var field = new StringBuilder();
                bool quoted = false;
                for (int index = 0; index < text.Length; index++)
                {
                    char character = text[index];
                    if (quoted)
                    {
                        if (character == '"')
                        {
                            if (index + 1 < text.Length && text[index + 1] == '"')
                            {
                                field.Append('"');
                                index++;
                            }
                            else
                            {
                                quoted = false;
                            }
                        }
                        else
                        {
                            field.Append(character);
                        }

                        continue;
                    }

                    switch (character)
                    {
                        case '"': quoted = true; break;
                        case ',':
                            record.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r': break;
                        case '\n':
                            record.Add(field.ToString());
                            field.Clear();
                            records.Add(record);
                            record = new List<string>();
                            break;
                        default: field.Append(character); break;
                    }
                }

                if (field.Length > 0 || record.Count > 0)
                {
                    record.Add(field.ToString());
                    records.Add(record);
                }

                if (quoted)
                {
                    throw new InvalidDataException("CSV ends inside a quoted field.");
                }

                return records;
            }
        }

        private sealed class CsvRow
        {
            private readonly IReadOnlyDictionary<string, string> values;

            public CsvRow(IReadOnlyList<string> headers, IReadOnlyList<string> fields)
            {
                if (headers.Count != fields.Count)
                {
                    throw new InvalidDataException(
                        $"CSV row has {fields.Count} fields; expected {headers.Count}.");
                }

                values = headers
                    .Select((header, index) => (header, fields[index]))
                    .ToDictionary(pair => pair.header, pair => pair.Item2, StringComparer.Ordinal);
            }

            public string this[string column] => values[column];

            public Vector3 Vector3(string x, string y, string z) => new Vector3(
                Number(x),
                Number(y),
                Number(z));

            public Quaternion Quaternion(string x, string y, string z, string w) =>
                new Quaternion(Number(x), Number(y), Number(z), Number(w)).normalized;

            private float Number(string column) => float.Parse(
                this[column],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }
    }
}
