using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MSC.Core.Time;
using MSC.Editor.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Services.Tests.EditMode
{
    public sealed class ServiceCatalogTests
    {
        private static readonly IReadOnlyDictionary<string, int> StoreCaps =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["juice"] = 6,
                ["yeast"] = 8,
                ["sugar"] = 9,
                ["coffee"] = 9,
                ["chips"] = 8,
                ["sausages"] = 12,
                ["beer"] = 5,
                ["milk"] = 8,
                ["macaron-box"] = 8,
                ["pizza"] = 6,
                ["mosquito-spray"] = 6,
                ["two-stroke"] = 5,
                ["motor-oil"] = 5,
                ["coolant"] = 4,
                ["fanbelt"] = 3,
                ["car-battery"] = 4,
                ["brake-fluid"] = 5,
                ["extinguisher"] = 3,
                ["cigarettes"] = 9,
                ["oilfilter"] = 4,
                ["charcoal"] = 4,
                ["sparkplugs"] = 5,
                ["spray-matte01"] = 3,
                ["spray01"] = 3,
                ["spray02"] = 3,
                ["spray03"] = 3,
                ["spray04"] = 3,
                ["spray05"] = 3,
                ["spray06"] = 3,
                ["spray07"] = 3,
                ["spray08"] = 3,
                ["spray09"] = 3,
                ["spray10"] = 3,
                ["spray11"] = 3,
                ["spray12"] = 3,
                ["lightbulb"] = 4,
                ["fuse-package"] = 5,
                ["r20-battery-box"] = 5,
                ["expanded-shop-buttermilk"] = 25,
                ["expanded-shop-orange-juice"] = 11,
                ["expanded-shop-bug-spray"] = 8,
                ["expanded-shop-laundry-detergent"] = 5,
                ["expanded-shop-sponge"] = 9,
                ["expanded-shop-shampoo"] = 6,
                ["expanded-shop-hand-soap"] = 6,
                ["expanded-shop-dish-soap"] = 7,
                ["expanded-shop-soap"] = 9,
                ["expanded-shop-wheat-flour"] = 10,
                ["expanded-shop-rye-flour"] = 9,
                ["expanded-shop-mustard"] = 8,
                ["expanded-shop-ketchup"] = 11,
                ["expanded-shop-meat-soup"] = 8,
                ["expanded-shop-pea-soup"] = 15,
                ["expanded-shop-canned-meatballs"] = 14,
                ["expanded-shop-sausage"] = 4,
                ["expanded-shop-fishstick-box"] = 39,
                ["expanded-shop-can-opener"] = 1,
            };

        [OneTimeSetUp]
        public void BuildCatalog()
        {
            Milestone12AS1ServiceCatalogBuilder.Build();
        }

        [Test]
        public void GeneratedCatalog_IsValidCompleteAndDeterministic()
        {
            ServiceCatalog first = RequireCatalog();
            string firstSignature = Signature(first);

            Milestone12AS1ServiceCatalogBuilder.Build();
            ServiceCatalog second = RequireCatalog();

            Assert.That(second.TryValidate(out string failure), Is.True, failure);
            Assert.That(second.CatalogId, Is.EqualTo(
                "catalog.services.phase1.12a-s1.v1"));
            Assert.That(second.DonorSceneSha256, Is.EqualTo(
                "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4"));
            Assert.That(second.Locations.Count, Is.EqualTo(7));
            Assert.That(second.Offers.Count, Is.EqualTo(101));
            Assert.That(second.FuelPrices.Count, Is.EqualTo(3));
            Assert.That(second.Locations.Select(location => location.LocationId),
                Is.Unique);
            Assert.That(second.Offers.Select(offer => offer.OfferId), Is.Unique);
            Assert.That(Signature(second), Is.EqualTo(firstSignature));
        }

        [Test]
        public void Store_ContainsE1AndExpandedShopPricesWithAuditedStockCaps()
        {
            ServiceCatalog catalog = RequireCatalog();
            ServiceOfferDefinition[] finiteRetail = catalog.Offers
                .Where(offer =>
                    offer.Kind == ServiceOfferKind.RetailItem &&
                    !string.IsNullOrEmpty(offer.PriceId))
                .ToArray();

            Assert.That(finiteRetail, Has.Length.EqualTo(57));
            foreach (KeyValuePair<string, int> pair in StoreCaps)
            {
                string slug = pair.Key;
                int capacity = pair.Value;
                string offerId = "service.store." + slug;
                Assert.That(catalog.TryGetOffer(offerId, out ServiceOfferDefinition offer),
                    Is.True, offerId);
                Assert.That(offer.PriceId, Is.EqualTo("price.store." + slug));
                Assert.That(offer.BasePriceMinorUnits, Is.Zero);
                Assert.That(offer.StockCapacity, Is.EqualTo(capacity), offerId);
                Assert.That(offer.RestockDayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
                Assert.That(offer.Restockable, Is.True, offerId);
                Assert.That(offer.ItemDefinitionId, Is.Not.Empty, offerId);
            }

            Assert.That(catalog.TryGetOffer(
                "service.store.expanded-shop-sausage",
                out ServiceOfferDefinition sausage), Is.True);
            Assert.That(sausage.ItemDefinitionId, Is.EqualTo(
                "item.loose-sausage"));
            Assert.That(catalog.TryGetOffer(
                "service.store.expanded-shop-fishstick-box",
                out ServiceOfferDefinition fishsticks), Is.True);
            Assert.That(fishsticks.ItemDefinitionId, Is.EqualTo(
                "item.fishstick-box"));
        }

        [Test]
        public void SuomiCovers_HaveSingleStockFixedDeferredOffers()
        {
            AssertFixedDeferredCover("suomi-dashboard-cover", 29_900);
            AssertFixedDeferredCover("suomi-seat-cover", 16_900);
            AssertFixedDeferredCover("suomi-steering-wheel-cover", 7_900);
        }

        [Test]
        public void Pub_ContainsExactFiveFixedOffers()
        {
            ServiceOfferDefinition[] offers = RequireCatalog().Offers
                .Where(offer => offer.Kind == ServiceOfferKind.PubItem)
                .ToArray();
            Assert.That(offers, Has.Length.EqualTo(5));
            AssertPrice("service.pub.beer", 800, "item.beer-bottle");
            AssertPrice(
                "service.pub.vodka-shot",
                3_000,
                "",
                "effect.service.pub.vodka-shot.deferred");
            AssertPrice(
                "service.pub.sausage-and-fries",
                2_500,
                "item.sausage-and-potatoes-meal");
            AssertPrice("service.pub.coffee", 700, "item.coffee-cup");
            AssertPrice("service.pub.cigarettes", 1_700, "item.cigarettes");
        }

        [Test]
        public void Fuel_ContainsExactGradesRangesAndPumpPositions()
        {
            ServiceCatalog catalog = RequireCatalog();
            AssertFuel(catalog, FuelGrade.Gasoline98, 475, 410, 530);
            AssertFuel(catalog, FuelGrade.Diesel, 423, 330, 405);
            AssertFuel(catalog, FuelGrade.FuelOil, 213, 150, 270);
            AssertFuelOffer(
                catalog,
                "service.fuel.gasoline98",
                "service.location.teimo-fuel",
                FuelGrade.Gasoline98);
            AssertFuelOffer(
                catalog,
                "service.fuel.diesel",
                "service.location.teimo-fuel-diesel",
                FuelGrade.Diesel);
            AssertFuelOffer(
                catalog,
                "service.fuel.fuel-oil",
                "service.location.teimo-fuel-oil",
                FuelGrade.FuelOil);
            AssertLocation(
                catalog,
                "service.location.teimo-fuel",
                "service.source.fuel.teimo.gasoline98",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.gasoline98",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1389.9272f, 5.7450f, 138.3104f),
                new Vector3(-1389.9272f, 5.7450f, 138.3104f));
            AssertLocation(
                catalog,
                "service.location.teimo-fuel-diesel",
                "service.source.fuel.teimo.diesel",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.diesel",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1391.6022f, 5.7450f, 137.2173f),
                new Vector3(-1391.6022f, 5.7450f, 137.2173f));
            AssertLocation(
                catalog,
                "service.location.teimo-fuel-oil",
                "service.source.fuel.teimo.fuel-oil",
                ServiceLocationKind.FuelStation,
                "service.anchor.fuel.teimo.fuel-oil",
                "service.anchor.fuel.teimo.checkout",
                new Vector3(-1404.1373f, 5.8890f, 138.3541f),
                new Vector3(-1404.1373f, 5.8890f, 138.3541f));
        }

        [Test]
        public void Fuel_FreshDonorInitialPricesRoundTripBeforeFirstThursdayRoll()
        {
            ServiceCatalog catalog = RequireCatalog();
            var gameTime = new GameTimeService(
                GameTimeConfig.RemakeDesignTargetDefaults);
            ServiceStateDto fresh = ServiceRuntime.CreateFreshState(
                catalog,
                gameTime.Snapshot);

            Assert.That(
                fresh.TryValidate(catalog, out string failure),
                Is.True,
                failure);
            Assert.That(
                fresh.fuelPrices.Single(value =>
                    value.grade == (int)FuelGrade.Diesel)
                    .currentMinorUnitsPerLiter,
                Is.EqualTo(423),
                "The donor diesel first-session price intentionally sits above its later roll range.");
        }

        [Test]
        public void Workshop_ContainsExactPriceGroupsAndNoInventedGearLinkage()
        {
            var prices = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["body-repair"] = 875_000,
                ["door-left"] = 123_000,
                ["door-right"] = 123_000,
                ["fender-left"] = 84_500,
                ["fender-right"] = 84_500,
                ["hood"] = 62_000,
                ["bootlid"] = 47_500,
                ["bumper-front"] = 78_000,
                ["bumper-rear"] = 78_000,
                ["grille"] = 51_000,
                ["toe-alignment"] = 49_500,
                ["brakes"] = 123_000,
                ["engine-repair"] = 291_500,
                ["engine-adjustment"] = 129_000,
                ["engine-tune"] = 406_500,
                ["windshield"] = 211_000,
                ["suspension"] = 920_000,
                ["rollcage-install"] = 550_000,
                ["rollcage-remove"] = 79_000,
                ["n2o-bottle-fill"] = 85_000,
                ["final-gear"] = 135_000,
                ["paint-regular"] = 1_015_000,
                ["paint-metallic"] = 1_895_000,
                ["paint-art"] = 2_170_000,
                ["paint-gt"] = 1_390_000,
                ["rim-regular"] = 115_000,
                ["rim-metallic"] = 192_500,
                ["rim-polish"] = 63_500,
                ["tires-standard"] = 175_000,
                ["tires-gommer-gobra"] = 211_000,
                ["tires-europeiska"] = 320_000,
                ["tires-sutasiko"] = 295_000,
            };

            ServiceCatalog catalog = RequireCatalog();
            ServiceOfferDefinition[] workshop = catalog.Offers
                .Where(offer => offer.Kind == ServiceOfferKind.Workshop)
                .ToArray();
            Assert.That(workshop, Has.Length.EqualTo(prices.Count));
            foreach (KeyValuePair<string, long> pair in prices)
            {
                string slug = pair.Key;
                long price = pair.Value;
                string id = "service.workshop." + slug;
                Assert.That(catalog.TryGetOffer(id, out ServiceOfferDefinition offer),
                    Is.True, id);
                Assert.That(offer.BasePriceMinorUnits, Is.EqualTo(price), id);
                string expectedGroup = slug.StartsWith(
                    "paint-",
                    StringComparison.Ordinal)
                    ? "service.workshop.group.paint"
                    : slug.StartsWith("rim-", StringComparison.Ordinal)
                        ? "service.workshop.group.rims"
                        : slug.StartsWith("tires-", StringComparison.Ordinal)
                            ? "service.workshop.group.tires"
                            : string.Empty;
                Assert.That(offer.ExclusiveGroupId, Is.EqualTo(expectedGroup), id);
            }

            Assert.That(workshop.Count(offer =>
                offer.ExclusiveGroupId == "service.workshop.group.paint"), Is.EqualTo(4));
            Assert.That(workshop.Count(offer =>
                offer.ExclusiveGroupId == "service.workshop.group.rims"), Is.EqualTo(3));
            Assert.That(workshop.Count(offer =>
                offer.ExclusiveGroupId == "service.workshop.group.tires"), Is.EqualTo(4));
            Assert.That(catalog.TryGetOffer("service.workshop.gear-linkage", out _),
                Is.False);
            Assert.That(workshop.All(offer =>
                string.IsNullOrEmpty(offer.ItemDefinitionId) &&
                string.IsNullOrEmpty(offer.EffectId)), Is.True,
                "Fleetari is catalog/timer-only until a real player vehicle exists; " +
                "the builder must not invent a Satsuma target or fake an outcome.");
        }

        [Test]
        public void Inspection_IsPriceOnlyShellWithoutInventedAssessmentOutcome()
        {
            ServiceCatalog catalog = RequireCatalog();
            AssertLocation(
                catalog,
                "service.location.inspection-station",
                "character.inspection-officer",
                ServiceLocationKind.Inspection,
                "anchor.service.inspection.order",
                "anchor.service.inspection.receipt",
                new Vector3(-1358.953560f, 5.872081f, 221.328050f),
                new Vector3(-1358.630644f, 5.652081f, 221.400061f));

            ServiceAvailabilityWindow availability = Window(
                catalog,
                "service.location.inspection-station");
            Assert.That(availability.DayMask, Is.EqualTo(62));
            Assert.That(availability.StartMinute, Is.EqualTo(480));
            Assert.That(availability.EndMinute, Is.EqualTo(960));
            Assert.That(availability.Contains(DayOfWeek.Monday, 480), Is.True);
            Assert.That(availability.Contains(DayOfWeek.Friday, 959), Is.True);
            Assert.That(availability.Contains(DayOfWeek.Friday, 960), Is.False);
            Assert.That(availability.Contains(DayOfWeek.Saturday, 600), Is.False);

            Assert.That(catalog.TryGetOffer(
                "service.offer.inspection.vehicle",
                out ServiceOfferDefinition offer), Is.True);
            Assert.That(offer.Kind, Is.EqualTo(ServiceOfferKind.Inspection));
            Assert.That(offer.LocationId, Is.EqualTo(
                "service.location.inspection-station"));
            Assert.That(offer.PriceId, Is.Empty);
            Assert.That(offer.BasePriceMinorUnits, Is.EqualTo(32_500));
            Assert.That(offer.ItemDefinitionId, Is.Empty);
            Assert.That(offer.EffectId, Is.Empty);
            Assert.That(offer.ExclusiveGroupId, Is.Empty);
            Assert.That(offer.StockCapacity, Is.Zero);
            Assert.That(catalog.Offers.Count(candidate =>
                candidate.Kind == ServiceOfferKind.Inspection), Is.EqualTo(1));
            Assert.That(catalog.Offers.Any(candidate =>
                candidate.OfferId.Contains("satsuma", StringComparison.OrdinalIgnoreCase) ||
                candidate.ItemDefinitionId.Contains(
                    "satsuma",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.EffectId.Contains(
                    "satsuma",
                    StringComparison.OrdinalIgnoreCase)), Is.False,
                "The S1 inspection shell must not invent a missing vehicle or " +
                "assessment outcome.");
        }

        [Test]
        public void AvailabilityAndPrimaryAnchors_MatchLockedEvidence()
        {
            ServiceCatalog catalog = RequireCatalog();
            ServiceAvailabilityWindow store = Window(catalog, "service.location.teimo-store");
            Assert.That(store.Contains(DayOfWeek.Monday, 600), Is.True);
            Assert.That(store.Contains(DayOfWeek.Saturday, 1199), Is.True);
            Assert.That(store.Contains(DayOfWeek.Saturday, 1200), Is.False);
            Assert.That(store.Contains(DayOfWeek.Sunday, 700), Is.False);

            ServiceAvailabilityWindow pub = Window(catalog, "service.location.teimo-pub");
            Assert.That(pub.Contains(DayOfWeek.Monday, 1200), Is.True);
            Assert.That(pub.Contains(DayOfWeek.Tuesday, 119), Is.True);
            Assert.That(pub.Contains(DayOfWeek.Sunday, 119), Is.True);
            Assert.That(pub.Contains(DayOfWeek.Sunday, 1200), Is.False);

            ServiceAvailabilityWindow fleetari = Window(
                catalog,
                "service.location.workshop.fleetari");
            Assert.That(fleetari.Contains(DayOfWeek.Monday, 480), Is.True);
            Assert.That(fleetari.Contains(DayOfWeek.Friday, 959), Is.True);
            Assert.That(fleetari.Contains(DayOfWeek.Saturday, 600), Is.False);

            AssertLocation(
                catalog,
                "service.location.teimo-store",
                "service.source.store.teimo",
                ServiceLocationKind.Store,
                "service.anchor.store.teimo.register",
                "service.anchor.store.teimo.bag",
                new Vector3(-1381.5675f, 6.5730f, 141.6774f),
                new Vector3(-1381.072f, 6.6149993f, 142.13538f));
            AssertLocation(
                catalog,
                "service.location.teimo-pub",
                "service.source.pub.teimo",
                ServiceLocationKind.Pub,
                "service.anchor.pub.teimo.order",
                "service.anchor.pub.teimo.counter",
                new Vector3(-1376.1135f, 6.5730f, 145.17139f),
                new Vector3(-1375.6228468f, 6.3336284f, 145.7850387f));
            AssertLocation(
                catalog,
                "service.location.workshop.fleetari",
                "service.source.workshop.fleetari",
                ServiceLocationKind.Workshop,
                "service.anchor.workshop.fleetari.interaction",
                "service.anchor.workshop.fleetari.handoff",
                new Vector3(1725.0482f, 6.3119974f, -301.45422f),
                new Vector3(1725.0482f, 6.3119974f, -301.45422f));
        }

        private static ServiceCatalog RequireCatalog()
        {
            ServiceCatalog catalog = AssetDatabase.LoadAssetAtPath<ServiceCatalog>(
                Milestone12AS1ServiceCatalogBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static void AssertFixedDeferredCover(string slug, long price)
        {
            ServiceCatalog catalog = RequireCatalog();
            string id = "service.store." + slug;
            Assert.That(catalog.TryGetOffer(id, out ServiceOfferDefinition offer),
                Is.True, id);
            Assert.That(offer.Kind, Is.EqualTo(ServiceOfferKind.RetailItem));
            Assert.That(offer.PriceId, Is.Empty);
            Assert.That(offer.BasePriceMinorUnits, Is.EqualTo(price));
            Assert.That(offer.StockCapacity, Is.EqualTo(1));
            Assert.That(offer.Restockable, Is.False);
            Assert.That(offer.ItemDefinitionId, Is.Empty);
            Assert.That(offer.EffectId, Is.EqualTo(
                $"effect.service.store.{slug}.deferred"));
        }

        private static void AssertPrice(
            string id,
            long price,
            string itemDefinitionId,
            string effectId = "")
        {
            ServiceCatalog catalog = RequireCatalog();
            Assert.That(catalog.TryGetOffer(id, out ServiceOfferDefinition offer),
                Is.True, id);
            Assert.That(offer.BasePriceMinorUnits, Is.EqualTo(price));
            Assert.That(offer.PriceId, Is.Empty);
            Assert.That(offer.ItemDefinitionId, Is.EqualTo(itemDefinitionId));
            Assert.That(offer.EffectId, Is.EqualTo(effectId));
        }

        private static void AssertFuel(
            ServiceCatalog catalog,
            FuelGrade grade,
            long initial,
            long minimum,
            long maximum)
        {
            Assert.That(catalog.TryGetFuelPrice(grade, out ServiceFuelPriceDefinition price),
                Is.True, grade.ToString());
            Assert.That(price.InitialMinorUnitsPerLiter, Is.EqualTo(initial));
            Assert.That(price.MinimumMinorUnitsPerLiter, Is.EqualTo(minimum));
            Assert.That(price.MaximumMinorUnitsPerLiter, Is.EqualTo(maximum));
        }

        private static void AssertFuelOffer(
            ServiceCatalog catalog,
            string offerId,
            string locationId,
            FuelGrade grade)
        {
            Assert.That(catalog.TryGetOffer(
                offerId,
                out ServiceOfferDefinition offer), Is.True, offerId);
            Assert.That(offer.Kind, Is.EqualTo(ServiceOfferKind.Fuel));
            Assert.That(offer.LocationId, Is.EqualTo(locationId));
            Assert.That(offer.FuelGrade, Is.EqualTo(grade));
            Assert.That(offer.PriceId, Is.Empty);
            Assert.That(offer.BasePriceMinorUnits, Is.Zero);
        }

        private static ServiceAvailabilityWindow Window(
            ServiceCatalog catalog,
            string locationId)
        {
            Assert.That(catalog.TryGetLocation(
                locationId,
                out ServiceLocationDefinition location), Is.True, locationId);
            Assert.That(location.Availability.Count, Is.EqualTo(1));
            return location.Availability[0];
        }

        private static void AssertLocation(
            ServiceCatalog catalog,
            string locationId,
            string sourceStableId,
            ServiceLocationKind kind,
            string interactionAnchorId,
            string handoffAnchorId,
            Vector3 expectedPosition,
            Vector3 expectedHandoffPosition)
        {
            Assert.That(catalog.TryGetLocation(
                locationId,
                out ServiceLocationDefinition location), Is.True, locationId);
            Assert.That(location.SourceStableId, Is.EqualTo(sourceStableId));
            Assert.That(location.Kind, Is.EqualTo(kind));
            Assert.That(location.InteractionAnchorId, Is.EqualTo(interactionAnchorId));
            Assert.That(location.HandoffAnchorId, Is.EqualTo(handoffAnchorId));
            AssertVector(location.WorldPosition, expectedPosition, locationId);
            AssertVector(
                location.HandoffWorldPosition,
                expectedHandoffPosition,
                locationId + " handoff");
        }

        private static void AssertVector(
            Vector3 actual,
            Vector3 expected,
            string message)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f), message);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f), message);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f), message);
        }

        private static string Signature(ServiceCatalog catalog)
        {
            IEnumerable<string> locations = catalog.Locations.Select(location =>
                string.Join("|",
                    location.LocationId,
                    location.DisplayName,
                    location.SourceStableId,
                    (int)location.Kind,
                    location.InteractionAnchorId,
                    location.HandoffAnchorId,
                    location.WorldPosition.x.ToString("R", CultureInfo.InvariantCulture),
                    location.WorldPosition.y.ToString("R", CultureInfo.InvariantCulture),
                    location.WorldPosition.z.ToString("R", CultureInfo.InvariantCulture),
                    location.HandoffWorldPosition.x.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                    location.HandoffWorldPosition.y.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                    location.HandoffWorldPosition.z.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                    location.Availability[0].DayMask,
                    location.Availability[0].StartMinute,
                    location.Availability[0].EndMinute));
            IEnumerable<string> offers = catalog.Offers.Select(offer =>
                string.Join("|",
                    offer.OfferId,
                    offer.LocationId,
                    offer.DisplayName,
                    (int)offer.Kind,
                    offer.PriceId,
                    offer.BasePriceMinorUnits,
                    offer.ItemDefinitionId,
                    offer.EffectId,
                    offer.ExclusiveGroupId,
                    offer.VariantIndex,
                    offer.QuantityPerUnit,
                    offer.StockCapacity,
                    (int)offer.RestockDayOfWeek,
                    offer.Restockable,
                    (int)offer.FuelGrade));
            IEnumerable<string> fuel = catalog.FuelPrices.Select(price =>
                string.Join("|",
                    (int)price.Grade,
                    price.InitialMinorUnitsPerLiter,
                    price.MinimumMinorUnitsPerLiter,
                    price.MaximumMinorUnitsPerLiter));
            return string.Join(
                "\n",
                new[] { catalog.CatalogId, catalog.DonorSceneSha256 }
                    .Concat(locations)
                    .Concat(offers)
                    .Concat(fuel));
        }
    }
}
