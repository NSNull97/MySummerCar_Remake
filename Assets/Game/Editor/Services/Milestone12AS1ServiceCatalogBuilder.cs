using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Bootstrap;
using MSC.Economy;
using MSC.Items;
using MSC.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Services
{
    /// <summary>
    /// Deterministic, audited Phase-1 service catalog authoring. Donor FSMs are
    /// evidence only; the generated asset contains project-owned IDs and data.
    /// </summary>
    public static class Milestone12AS1ServiceCatalogBuilder
    {
        public const string CatalogPath =
            "Assets/Game/Services/Content/Phase1/Phase1ServiceCatalog.asset";

        private const string EconomyCatalogPath =
            "Assets/Game/Economy/Content/Phase1/EconomyPriceCatalog.asset";
        private const string ItemCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string LockedSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";

        private const string StoreLocationId = "service.location.teimo-store";
        private const string PubLocationId = "service.location.teimo-pub";
        private const string FuelLocationId = "service.location.teimo-fuel";
        private const string DieselLocationId =
            "service.location.teimo-fuel-diesel";
        private const string FuelOilLocationId =
            "service.location.teimo-fuel-oil";
        private const string WorkshopLocationId =
            "service.location.workshop.fleetari";
        private const string InspectionLocationId =
            "service.location.inspection-station";

        private const int MondayThroughSaturdayMask = 126;
        private const int MondayThroughFridayMask = 62;

        private static readonly RetailSeed[] RetailSeeds =
        {
            new("juice", "Juice concentrate", "item.juice-concentrate", 6),
            new("yeast", "Yeast", "item.yeast", 8),
            new("sugar", "Sugar", "item.sugar", 9),
            new("coffee", "Coffee", "item.coffee-package", 9),
            new("chips", "Potato chips", "item.potato-chips", 8),
            new("sausages", "Sausages", "item.sausages-package", 12),
            new("beer", "Beer case", "item.beer-case", 5),
            new("milk", "Milk", "item.milk", 8),
            new("macaron-box", "Macaroni box", "item.macaroni-box", 8),
            new("pizza", "Pizza", "item.pizza", 6),
            new("mosquito-spray", "Mosquito spray", "item.mosquito-spray", 6),
            new("two-stroke", "Two-stroke oil", "item.two-stroke-oil", 5),
            new("motor-oil", "Motor oil", "item.motor-oil", 5),
            new("coolant", "Coolant", "item.coolant", 4),
            new("fanbelt", "Alternator belt", "item.alternator-belt", 3),
            new("car-battery", "Car battery", "item.car-battery", 4),
            new("brake-fluid", "Brake fluid", "item.brake-fluid", 5),
            new("extinguisher", "Fire extinguisher", "item.fire-extinguisher", 3),
            new("cigarettes", "Cigarettes", "item.cigarettes", 9),
            new("oilfilter", "Oil filter", "item.oil-filter", 4),
            new("charcoal", "Charcoal", "item.charcoal", 4),
            new("sparkplugs", "Spark plugs", "item.sparkplug-box", 5),
            new("spray-matte01", "Matte spray paint", "item.spray-paint-variants", 3, 0),
            new("spray01", "Spray paint 01", "item.spray-paint-variants", 3, 1),
            new("spray02", "Spray paint 02", "item.spray-paint-variants", 3, 2),
            new("spray03", "Spray paint 03", "item.spray-paint-variants", 3, 3),
            new("spray04", "Spray paint 04", "item.spray-paint-variants", 3, 4),
            new("spray05", "Spray paint 05", "item.spray-paint-variants", 3, 5),
            new("spray06", "Spray paint 06", "item.spray-paint-variants", 3, 6),
            new("spray07", "Spray paint 07", "item.spray-paint-variants", 3, 7),
            new("spray08", "Spray paint 08", "item.spray-paint-variants", 3, 8),
            new("spray09", "Spray paint 09", "item.spray-paint-variants", 3, 9),
            new("spray10", "Spray paint 10", "item.spray-paint-variants", 3, 10),
            new("spray11", "Spray paint 11", "item.spray-paint-variants", 3, 11),
            new("spray12", "Spray paint 12", "item.spray-paint-variants", 3, 12),
            new("lightbulb", "Lightbulb box", "item.lightbulb-box", 4),
            new("fuse-package", "Fuse package", "item.fuse-package", 5),
            new("r20-battery-box", "R20 battery box", "item.r20-battery", 5),
        };

        private static readonly RetailSeed[] ExpandedShopRetailSeeds =
        {
            new("expanded-shop-buttermilk", "Buttermilk", "item.buttermilk", 25),
            new("expanded-shop-orange-juice", "Orange juice", "item.orange-juice", 11),
            new("expanded-shop-bug-spray", "Bug spray", "item.bug-spray", 8),
            new("expanded-shop-laundry-detergent", "Laundry detergent", "item.laundry-detergent", 5),
            new("expanded-shop-sponge", "Sponge", "item.sponge", 9),
            new("expanded-shop-shampoo", "Shampoo", "item.shampoo", 6),
            new("expanded-shop-hand-soap", "Hand soap", "item.hand-soap", 6),
            new("expanded-shop-dish-soap", "Dish soap", "item.dish-soap", 7),
            new("expanded-shop-soap", "Soap", "item.soap", 9),
            new("expanded-shop-wheat-flour", "Wheat flour", "item.wheat-flour", 10),
            new("expanded-shop-rye-flour", "Rye flour", "item.rye-flour", 9),
            new("expanded-shop-mustard", "Mustard", "item.mustard", 8),
            new("expanded-shop-ketchup", "Ketchup", "item.ketchup", 11),
            new("expanded-shop-meat-soup", "Meat soup", "item.meat-soup", 8),
            new("expanded-shop-pea-soup", "Pea soup", "item.pea-soup", 15),
            new("expanded-shop-canned-meatballs", "Canned meatballs", "item.canned-meatballs", 14),
            new("expanded-shop-sausage", "Sausage", "item.loose-sausage", 4),
            new("expanded-shop-fishstick-box", "Fishstick box", "item.fishstick-box", 39),
            new("expanded-shop-can-opener", "Can opener", "item.can-opener", 1),
        };

        private static readonly FixedRetailSeed[] SuomiCoverSeeds =
        {
            new(
                "suomi-dashboard-cover",
                "SUOMI dashboard cover",
                29_900,
                "effect.service.store.suomi-dashboard-cover.deferred"),
            new(
                "suomi-seat-cover",
                "SUOMI seat cover",
                16_900,
                "effect.service.store.suomi-seat-cover.deferred"),
            new(
                "suomi-steering-wheel-cover",
                "SUOMI steering wheel cover",
                7_900,
                "effect.service.store.suomi-steering-wheel-cover.deferred"),
        };

        private static readonly FixedOfferSeed[] PubSeeds =
        {
            new("beer", "Beer", 800, "item.beer-bottle", ""),
            new(
                "vodka-shot",
                "Vodka shot",
                3_000,
                "",
                "effect.service.pub.vodka-shot.deferred"),
            new(
                "sausage-and-fries",
                "Sausage and fries",
                2_500,
                "item.sausage-and-potatoes-meal",
                ""),
            new("coffee", "Coffee", 700, "item.coffee-cup", ""),
            new("cigarettes", "Cigarettes", 1_700, "item.cigarettes", ""),
        };

        private static readonly WorkshopSeed[] WorkshopSeeds =
        {
            new("body-repair", "Body repair", 875_000),
            new("door-left", "Left door repair", 123_000),
            new("door-right", "Right door repair", 123_000),
            new("fender-left", "Left fender repair", 84_500),
            new("fender-right", "Right fender repair", 84_500),
            new("hood", "Hood repair", 62_000),
            new("bootlid", "Bootlid repair", 47_500),
            new("bumper-front", "Front bumper repair", 78_000),
            new("bumper-rear", "Rear bumper repair", 78_000),
            new("grille", "Grille repair", 51_000),
            new("toe-alignment", "Toe alignment", 49_500),
            new("brakes", "Brake service", 123_000),
            new("engine-repair", "Engine repair", 291_500),
            new("engine-adjustment", "Engine adjustment", 129_000),
            new("engine-tune", "Engine tuning", 406_500),
            new("windshield", "Windshield replacement", 211_000),
            new("suspension", "Suspension service", 920_000),
            new("rollcage-install", "Install roll cage", 550_000),
            new("rollcage-remove", "Remove roll cage", 79_000),
            new("n2o-bottle-fill", "N2O bottle fill", 85_000),
            new("final-gear", "Final gear ratio", 135_000),
            new("paint-regular", "Regular paint", 1_015_000, "service.workshop.group.paint"),
            new("paint-metallic", "Metallic paint", 1_895_000, "service.workshop.group.paint"),
            new("paint-art", "Artist paint", 2_170_000, "service.workshop.group.paint"),
            new("paint-gt", "GT paint", 1_390_000, "service.workshop.group.paint"),
            new("rim-regular", "Regular rim paint", 115_000, "service.workshop.group.rims"),
            new("rim-metallic", "Metallic rim paint", 192_500, "service.workshop.group.rims"),
            new("rim-polish", "Rim polish", 63_500, "service.workshop.group.rims"),
            new("tires-standard", "Standard tires", 175_000, "service.workshop.group.tires"),
            new("tires-gommer-gobra", "Gommer Gobra tires", 211_000, "service.workshop.group.tires"),
            new("tires-europeiska", "Europeiska tires", 320_000, "service.workshop.group.tires"),
            new("tires-sutasiko", "Sutasiko tires", 295_000, "service.workshop.group.tires"),
        };

        [MenuItem("Tools/My Summer Car/Phase 1/Build Services (12A-S1)")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Milestone 12A-S1",
                "The audited Phase-1 service catalog was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static void Build()
        {
            ServiceCatalog candidate = CreateConfiguredCatalog();
            try
            {
                string directory = Path.GetDirectoryName(CatalogPath) ??
                                   "Assets/Game/Services/Content/Phase1";
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                ServiceCatalog asset =
                    AssetDatabase.LoadAssetAtPath<ServiceCatalog>(CatalogPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ServiceCatalog>();
                    AssetDatabase.CreateAsset(asset, CatalogPath);
                }

                asset.name = "Phase1ServiceCatalog";
                asset.ConfigureForAuthoring(
                    candidate.CatalogId,
                    candidate.DonorSceneSha256,
                    candidate.Locations.ToArray(),
                    candidate.Offers.ToArray(),
                    candidate.FuelPrices.ToArray());
                if (!asset.TryValidate(out string failure))
                {
                    throw new InvalidOperationException(failure);
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                BindBootstrap(asset);
                AssetDatabase.Refresh();
                Debug.Log(
                    $"Milestone 12A-S1 built: {asset.Locations.Count} locations, " +
                    $"{asset.Offers.Count} offers and {asset.FuelPrices.Count} fuel grades.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        private static void BindBootstrap(ServiceCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller[] installers = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<
                    ProductionWorldStreamingInstaller>(true))
                .ToArray();
            if (installers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Bootstrap must contain exactly one " +
                    $"{nameof(ProductionWorldStreamingInstaller)}, found " +
                    $"{installers.Length}.");
            }

            installers[0].ConfigureServicesForAuthoring(catalog);
            EditorUtility.SetDirty(installers[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Failed to save Bootstrap scene '{BootstrapScenePath}'.");
            }
        }

        public static ServiceCatalog CreateConfiguredCatalog()
        {
            EconomyPriceCatalog economy = AssetDatabase.LoadAssetAtPath<
                EconomyPriceCatalog>(EconomyCatalogPath);
            ItemDefinitionCatalog items = AssetDatabase.LoadAssetAtPath<
                ItemDefinitionCatalog>(ItemCatalogPath);
            string economyFailure = "The 12A-E1 economy catalog is missing.";
            if (economy == null || !economy.TryValidate(out economyFailure))
            {
                throw new InvalidOperationException(
                    economyFailure);
            }

            IReadOnlyList<string> itemFailures =
                items?.ValidateConfiguration() ?? new[] { "Item catalog is missing." };
            if (items == null || itemFailures.Count != 0)
            {
                throw new InvalidOperationException(string.Join(" | ", itemFailures));
            }

            ServiceLocationDefinition[] locations = BuildLocations();
            ServiceOfferDefinition[] offers = BuildOffers();
            ServiceFuelPriceDefinition[] fuelPrices = BuildFuelPrices();
            ValidateDependencies(economy, items, offers);

            var catalog = ScriptableObject.CreateInstance<ServiceCatalog>();
            catalog.name = "Phase1ServiceCatalog";
            catalog.ConfigureForAuthoring(
                "catalog.services.phase1.12a-s1.v1",
                LockedSceneSha256,
                locations,
                offers,
                fuelPrices);
            if (!catalog.TryValidate(out string failure))
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                throw new InvalidOperationException(failure);
            }

            return catalog;
        }

        private static ServiceLocationDefinition[] BuildLocations() => new[]
        {
            Location(
                StoreLocationId,
                "Teimo's shop",
                "service.source.store.teimo",
                ServiceLocationKind.Store,
                "service.anchor.store.teimo.register",
                "service.anchor.store.teimo.bag",
                new Vector3(-1381.5675f, 6.5730f, 141.6774f),
                new Vector3(-1381.072f, 6.6149993f, 142.13538f),
                MondayThroughSaturdayMask,
                600,
                1200),
            Location(
                PubLocationId,
                "Teimo's pub",
                "service.source.pub.teimo",
                ServiceLocationKind.Pub,
                "service.anchor.pub.teimo.order",
                "service.anchor.pub.teimo.counter",
                new Vector3(-1376.1135f, 6.5730f, 145.17139f),
                // Donor FoodSpawnPoint: the service counter, not the cash till.
                new Vector3(-1375.6228468f, 6.3336284f, 145.7850387f),
                MondayThroughSaturdayMask,
                1200,
                120),
            Location(
                FuelLocationId,
                "Teimo gasoline 98 pump",
                "service.source.fuel.teimo.gasoline98",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.gasoline98",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1389.9272f, 5.7450f, 138.3104f),
                new Vector3(-1389.9272f, 5.7450f, 138.3104f),
                MondayThroughSaturdayMask,
                600,
                1200),
            Location(
                DieselLocationId,
                "Teimo diesel pump",
                "service.source.fuel.teimo.diesel",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.diesel",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1391.6022f, 5.7450f, 137.2173f),
                new Vector3(-1391.6022f, 5.7450f, 137.2173f),
                MondayThroughSaturdayMask,
                600,
                1200),
            Location(
                FuelOilLocationId,
                "Teimo fuel oil pump",
                "service.source.fuel.teimo.fuel-oil",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.fuel-oil",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1404.1373f, 5.8890f, 138.3541f),
                new Vector3(-1404.1373f, 5.8890f, 138.3541f),
                MondayThroughSaturdayMask,
                600,
                1200),
            Location(
                WorkshopLocationId,
                "Fleetari workshop",
                "service.source.workshop.fleetari",
                ServiceLocationKind.Workshop,
                "service.anchor.workshop.fleetari.interaction",
                "service.anchor.workshop.fleetari.handoff",
                new Vector3(1725.0482f, 6.3119974f, -301.45422f),
                new Vector3(1725.0482f, 6.3119974f, -301.45422f),
                MondayThroughFridayMask,
                480,
                960),
            Location(
                InspectionLocationId,
                "Vehicle inspection station",
                "character.inspection-officer",
                ServiceLocationKind.Inspection,
                "anchor.service.inspection.order",
                "anchor.service.inspection.receipt",
                new Vector3(-1358.953560f, 5.872081f, 221.328050f),
                new Vector3(-1358.630644f, 5.652081f, 221.400061f),
                MondayThroughFridayMask,
                480,
                960),
        };

        private static ServiceOfferDefinition[] BuildOffers()
        {
            var offers = new List<ServiceOfferDefinition>(101);
            foreach (RetailSeed seed in RetailSeeds.Concat(
                         ExpandedShopRetailSeeds))
            {
                offers.Add(Offer(
                    "service.store." + seed.Slug,
                    StoreLocationId,
                    seed.DisplayName,
                    ServiceOfferKind.RetailItem,
                    "price.store." + seed.Slug,
                    seed.ItemDefinitionId,
                    "",
                    "",
                    seed.VariantIndex,
                    seed.StockCapacity));
            }

            foreach (FixedRetailSeed seed in SuomiCoverSeeds)
            {
                offers.Add(Offer(
                    "service.store." + seed.Slug,
                    StoreLocationId,
                    seed.DisplayName,
                    ServiceOfferKind.RetailItem,
                    "",
                    "",
                    seed.EffectId,
                    "",
                    0,
                    1,
                    seed.PriceMinorUnits,
                    restockable: false));
            }

            foreach (FixedOfferSeed seed in PubSeeds)
            {
                offers.Add(Offer(
                    "service.pub." + seed.Slug,
                    PubLocationId,
                    seed.DisplayName,
                    ServiceOfferKind.PubItem,
                    "",
                    seed.ItemDefinitionId,
                    seed.EffectId,
                    "",
                    0,
                    0,
                    seed.PriceMinorUnits));
            }

            offers.Add(FuelOffer(
                "service.fuel.gasoline98",
                FuelLocationId,
                "Gasoline 98",
                FuelGrade.Gasoline98));
            offers.Add(FuelOffer(
                "service.fuel.diesel",
                DieselLocationId,
                "Diesel",
                FuelGrade.Diesel));
            offers.Add(FuelOffer(
                "service.fuel.fuel-oil",
                FuelOilLocationId,
                "Fuel oil",
                FuelGrade.FuelOil));

            foreach (WorkshopSeed seed in WorkshopSeeds)
            {
                offers.Add(Offer(
                    "service.workshop." + seed.Slug,
                    WorkshopLocationId,
                    seed.DisplayName,
                    ServiceOfferKind.Workshop,
                    "",
                    "",
                    "",
                    seed.ExclusiveGroupId,
                    0,
                    0,
                    seed.PriceMinorUnits));
            }

            offers.Add(Offer(
                "service.offer.inspection.vehicle",
                InspectionLocationId,
                "Vehicle inspection",
                ServiceOfferKind.Inspection,
                "",
                "",
                "",
                "",
                0,
                0,
                32_500));

            if (offers.Count != 101)
            {
                throw new InvalidOperationException(
                    $"Expected 101 audited and extension offers, built {offers.Count}.");
            }

            return offers.ToArray();
        }

        private static ServiceFuelPriceDefinition[] BuildFuelPrices() => new[]
        {
            FuelPrice(FuelGrade.Gasoline98, 475, 410, 530),
            FuelPrice(FuelGrade.Diesel, 423, 330, 405),
            FuelPrice(FuelGrade.FuelOil, 213, 150, 270),
        };

        private static void ValidateDependencies(
            EconomyPriceCatalog economy,
            ItemDefinitionCatalog items,
            IReadOnlyList<ServiceOfferDefinition> offers)
        {
            var referencedPrices = new HashSet<string>(StringComparer.Ordinal);
            foreach (ServiceOfferDefinition offer in offers)
            {
                if (!string.IsNullOrEmpty(offer.PriceId) &&
                    (!economy.TryGetPrice(offer.PriceId, out _) ||
                     !referencedPrices.Add(offer.PriceId)))
                {
                    throw new InvalidOperationException(
                        $"Service offer price '{offer.PriceId}' is missing or duplicated.");
                }

                if (!string.IsNullOrEmpty(offer.ItemDefinitionId) &&
                    !items.TryGet(offer.ItemDefinitionId, out _))
                {
                    throw new InvalidOperationException(
                        $"Service item '{offer.ItemDefinitionId}' is unavailable.");
                }
            }

            int expectedPriceCount =
                RetailSeeds.Length + ExpandedShopRetailSeeds.Length;
            if (referencedPrices.Count != expectedPriceCount ||
                economy.Prices.Any(price => !referencedPrices.Contains(price.PriceId)))
            {
                throw new InvalidOperationException(
                    "The service catalog does not cover the exact 38-price E1 set " +
                    "plus the 19-price Expanded Shop extension.");
            }

            Dictionary<string, int> groups = offers
                .Where(offer => !string.IsNullOrEmpty(offer.ExclusiveGroupId))
                .GroupBy(offer => offer.ExclusiveGroupId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            RequireGroup(groups, "service.workshop.group.paint", 4);
            RequireGroup(groups, "service.workshop.group.rims", 3);
            RequireGroup(groups, "service.workshop.group.tires", 4);
            if (groups.Count != 3)
            {
                throw new InvalidOperationException(
                    "Workshop contains an unaudited exclusive group.");
            }
        }

        private static void RequireGroup(
            IReadOnlyDictionary<string, int> groups,
            string groupId,
            int count)
        {
            if (!groups.TryGetValue(groupId, out int actual) || actual != count)
            {
                throw new InvalidOperationException(
                    $"Workshop group '{groupId}' requires {count} offers, got {actual}.");
            }
        }

        private static ServiceLocationDefinition Location(
            string locationId,
            string displayName,
            string sourceStableId,
            ServiceLocationKind kind,
            string interactionAnchorId,
            string handoffAnchorId,
            Vector3 position,
            Vector3 handoffPosition,
            int dayMask,
            int startMinute,
            int endMinute)
        {
            var availability = new ServiceAvailabilityWindow();
            availability.ConfigureForAuthoring(dayMask, startMinute, endMinute);
            var location = new ServiceLocationDefinition();
            location.ConfigureForAuthoring(
                locationId,
                displayName,
                sourceStableId,
                kind,
                interactionAnchorId,
                handoffAnchorId,
                position,
                handoffPosition,
                new[] { availability });
            return location;
        }

        private static ServiceOfferDefinition Offer(
            string offerId,
            string locationId,
            string displayName,
            ServiceOfferKind kind,
            string priceId,
            string itemDefinitionId,
            string effectId,
            string exclusiveGroupId,
            int variantIndex,
            int stockCapacity,
            long basePriceMinorUnits = 0,
            bool restockable = true)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                locationId,
                displayName,
                kind,
                priceId,
                itemDefinitionId,
                effectId,
                exclusiveGroupId,
                variantIndex,
                configuredQuantityPerUnit: 1,
                configuredStockCapacity: stockCapacity,
                configuredRestockDay: DayOfWeek.Thursday,
                configuredBasePriceMinorUnits: basePriceMinorUnits,
                configuredRestockable: restockable);
            return offer;
        }

        private static ServiceOfferDefinition FuelOffer(
            string offerId,
            string locationId,
            string displayName,
            FuelGrade grade)
        {
            var offer = new ServiceOfferDefinition();
            offer.ConfigureForAuthoring(
                offerId,
                locationId,
                displayName,
                ServiceOfferKind.Fuel,
                configuredPriceId: "",
                configuredFuelGrade: grade);
            return offer;
        }

        private static ServiceFuelPriceDefinition FuelPrice(
            FuelGrade grade,
            long initial,
            long minimum,
            long maximum)
        {
            var price = new ServiceFuelPriceDefinition();
            price.ConfigureForAuthoring(grade, initial, minimum, maximum);
            return price;
        }

        private readonly struct RetailSeed
        {
            public RetailSeed(
                string slug,
                string displayName,
                string itemDefinitionId,
                int stockCapacity,
                int variantIndex = 0)
            {
                Slug = slug;
                DisplayName = displayName;
                ItemDefinitionId = itemDefinitionId;
                StockCapacity = stockCapacity;
                VariantIndex = variantIndex;
            }

            public string Slug { get; }
            public string DisplayName { get; }
            public string ItemDefinitionId { get; }
            public int StockCapacity { get; }
            public int VariantIndex { get; }
        }

        private readonly struct FixedRetailSeed
        {
            public FixedRetailSeed(
                string slug,
                string displayName,
                long priceMinorUnits,
                string effectId)
            {
                Slug = slug;
                DisplayName = displayName;
                PriceMinorUnits = priceMinorUnits;
                EffectId = effectId;
            }

            public string Slug { get; }
            public string DisplayName { get; }
            public long PriceMinorUnits { get; }
            public string EffectId { get; }
        }

        private readonly struct FixedOfferSeed
        {
            public FixedOfferSeed(
                string slug,
                string displayName,
                long priceMinorUnits,
                string itemDefinitionId,
                string effectId)
            {
                Slug = slug;
                DisplayName = displayName;
                PriceMinorUnits = priceMinorUnits;
                ItemDefinitionId = itemDefinitionId;
                EffectId = effectId;
            }

            public string Slug { get; }
            public string DisplayName { get; }
            public long PriceMinorUnits { get; }
            public string ItemDefinitionId { get; }
            public string EffectId { get; }
        }

        private readonly struct WorkshopSeed
        {
            public WorkshopSeed(
                string slug,
                string displayName,
                long priceMinorUnits,
                string exclusiveGroupId = "")
            {
                Slug = slug;
                DisplayName = displayName;
                PriceMinorUnits = priceMinorUnits;
                ExclusiveGroupId = exclusiveGroupId;
            }

            public string Slug { get; }
            public string DisplayName { get; }
            public long PriceMinorUnits { get; }
            public string ExclusiveGroupId { get; }
        }
    }
}
