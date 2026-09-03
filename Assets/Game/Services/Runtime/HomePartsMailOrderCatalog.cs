using System;
using System.Collections.Generic;
using System.Linq;

namespace MSC.Services
{
    public readonly struct HomePartsMailOrderOffer
    {
        public HomePartsMailOrderOffer(
            string id,
            string displayName,
            long priceMinorUnits,
            string itemDefinitionId = "")
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PriceMinorUnits = priceMinorUnits;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public long PriceMinorUnits { get; }
        public string ItemDefinitionId { get; }
    }

    public readonly struct HomeMailOrderEnvelopePlan
    {
        public HomeMailOrderEnvelopePlan(
            string orderId,
            string envelopeStableEntityId,
            IReadOnlyList<HomePartsMailOrderOffer> offers,
            long amountMinorUnits)
        {
            OrderId = orderId ?? string.Empty;
            EnvelopeStableEntityId = envelopeStableEntityId ?? string.Empty;
            Offers = offers ?? Array.Empty<HomePartsMailOrderOffer>();
            AmountMinorUnits = amountMinorUnits;
        }

        public string OrderId { get; }
        public string EnvelopeStableEntityId { get; }
        public IReadOnlyList<HomePartsMailOrderOffer> Offers { get; }
        public long AmountMinorUnits { get; }
    }

    public readonly struct HomeMailOrderDeliveryPlan
    {
        public HomeMailOrderDeliveryPlan(
            string orderId,
            IReadOnlyList<HomePartsMailOrderOffer> offers,
            long amountMinorUnits)
        {
            OrderId = orderId ?? string.Empty;
            Offers = offers ?? Array.Empty<HomePartsMailOrderOffer>();
            AmountMinorUnits = amountMinorUnits;
        }

        public string OrderId { get; }
        public IReadOnlyList<HomePartsMailOrderOffer> Offers { get; }
        public long AmountMinorUnits { get; }
    }

    /// <summary>
    /// Single project-owned authority for the donor-evidenced home magazine.
    /// Page presentation, saved orders and delivered item definitions consume
    /// these exact IDs and prices.
    /// </summary>
    public static class HomePartsMailOrderCatalog
    {
        public const string EnvelopeDefinitionId = "item.mail-order-envelope";

        private static readonly HomePartsMailOrderOffer[] Values =
        {
            Part("marker-light-left", "Marker lights", 239),
            Part("twin-carburators", "Twin carburators", 1750),
            Part("steel-headers", "Steel headers", 649),
            Part("wheel-cover-plush", "Plush wheel cover", 49),
            Part("wheelset-slot", "Slot rims", 1950),
            Part("window-grille", "Window grille", 219),
            Part("racing-flywheel", "Racing flywheel", 1495),
            Part("seat-cover-leopard", "Leopard seat covers", 278),
            Part("wheelset-steelwide", "Steel wide rims", 1205),
            Part("subwoofers", "Subwoofers", 1995),
            Part("ratchet-set", "Ratchet set", 359),
            Part("tachometer", "Tachometer", 829),
            Part("wheelset-racing", "Racing rims", 2545),
            Part("racing-muffler", "Racing muffler", 169),
            Part("dash-cover-leopard", "Leopard dashboard cover", 295),
            Part("racing-exhaust", "Racing exhaust", 429),
            Part("rear-spoiler", "Rear spoiler", 199),
            Part("dash-cover-plush", "Plush dashboard cover", 295),
            Part("cd-player", "CD player", 1395),
            Part("n2o-kit", "N2O kit", 5145),
            Part("seat-cover-zebra", "Zebra seat covers", 278),
            Part("window-black-wrap", "Black window wrap", 299),
            Part("rear-spoiler-2", "Rear spoiler 2", 329),
            Part("fender-flares", "Fender flares", 1195),
            Part("racing-harness", "Racing harness", 645),
            Part("sport-wheel", "Sport steering wheel", 349),
            Part("wheel-cover-zebra", "Zebra wheel cover", 49),
            Part("wheelset-spoke", "Spoke rims", 2200),
            Part("wheel-cover-leopard", "Leopard wheel cover", 49),
            Part("fuel-mixture-gauge", "Fuel mixture gauge", 549),
            Part("extra-gauges", "Extra gauges", 299),
            Part("wheelset-turbine", "Turbine rims", 2310),
            Part("seat-cover-plush", "Plush seat covers", 278),
            Part("bucket-seats", "Bucket seats", 5095),
            Part("antenna", "Antenna", 319),
            Part("rally-wheel", "Rally steering wheel", 895),
            Part("racing-carburators", "Racing carburators", 7250),
            Part("front-spoiler", "Front spoiler", 249),
            Part("exhaust-dual-tip", "Dual exhaust tip", 59),
            Part("wheelset-rally", "Rally rims", 2590),
            Part("wheelset-hayosiko", "Hayosiko rims", 1975),
            Part("wheelset-octo", "Octo rims", 1895),
            Part("racing-radiator", "Racing radiator", 1215),
            Part("rally-suspension", "Rally suspension", 9550),
            Part("dash-cover-zebra", "Zebra dashboard cover", 295),
            Part("fiberglass-hood", "Fiberglass hood", 2245),
        };

        private static readonly IReadOnlyDictionary<string, HomePartsMailOrderOffer>
            ById = Values.ToDictionary(value => value.Id, StringComparer.Ordinal);

        public static IReadOnlyList<HomePartsMailOrderOffer> Offers => Values;

        public static bool TryGet(
            string offerId,
            out HomePartsMailOrderOffer offer) =>
            ById.TryGetValue(offerId ?? string.Empty, out offer);

        private static HomePartsMailOrderOffer Part(
            string slug,
            string displayName,
            long priceMarks) =>
            new(
                "home.catalog.part." + slug,
                displayName,
                checked(priceMarks * 100L),
                "item.mail-order." + slug);
    }
}
