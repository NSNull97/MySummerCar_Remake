using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Assembly
{
    public static class SatsumaMailOrderCompatibilityDefaults
    {
        public const string VehicleContentId = "vehicle.satsuma";
        public const string CatalogId = "vehicle.satsuma.mail-order-compatibility.v1";
        public const int ExpectedEntryCount = 46;

        public static IReadOnlyList<VehicleDeliveredPartCompatibilityRecord> CreateRecords()
        {
            const string part = "vehicle.satsuma.part.";
            return new[]
            {
                Single("marker-light-left", "lighting.marker", part + "marker-light-left"),
                Single("twin-carburators", "engine.intake", part + "carburators-twin"),
                Single("steel-headers", "engine.exhaust-manifold", part + "headers-steel"),
                Cosmetic("wheel-cover-plush", "interior.steering-cover", part + "steering-cover-plush"),
                Wheels("wheelset-slot", part + "wheel-slot"),
                Single("window-grille", "body.rear-window", part + "window-grille"),
                Single("racing-flywheel", "engine.flywheel", part + "flywheel-racing"),
                PairCosmetic("seat-cover-leopard", "interior.seat-covers", part + "seat-cover-leopard-left", part + "seat-cover-leopard-right"),
                Wheels("wheelset-steelwide", part + "wheel-steelwide"),
                Pair("subwoofers", "audio.subwoofers", part + "subwoofer-left", part + "subwoofer-right"),
                Tool("ratchet-set", "tool.ratchet-set"),
                Single("tachometer", "dashboard.tachometer", part + "tachometer"),
                Wheels("wheelset-racing", part + "wheel-racing"),
                Single("racing-muffler", "exhaust.muffler", part + "muffler-racing"),
                Cosmetic("dash-cover-leopard", "interior.dashboard-cover", part + "dashboard-cover-leopard"),
                Single("racing-exhaust", "exhaust.pipe", part + "exhaust-racing"),
                Single("rear-spoiler", "body.rear-spoiler", part + "rear-spoiler"),
                Cosmetic("dash-cover-plush", "interior.dashboard-cover", part + "dashboard-cover-plush"),
                Single("cd-player", "audio.head-unit", part + "cd-player"),
                Kit("n2o-kit", "engine.n2o", part + "n2o-bottle", part + "n2o-holder", part + "n2o-button", part + "n2o-injector"),
                PairCosmetic("seat-cover-zebra", "interior.seat-covers", part + "seat-cover-zebra-left", part + "seat-cover-zebra-right"),
                Cosmetic("window-black-wrap", "body.rear-window-finish", part + "window-black-wrap"),
                Single("rear-spoiler-2", "body.rear-spoiler", part + "rear-spoiler-2"),
                Kit("fender-flares", "body.fender-flares", part + "fender-flare-fl", part + "fender-flare-fr", part + "fender-flare-rl", part + "fender-flare-rr"),
                Pair("racing-harness", "interior.harness", part + "racing-harness-left", part + "racing-harness-right"),
                Single("sport-wheel", "interior.steering-wheel", part + "steering-wheel-sport"),
                Cosmetic("wheel-cover-zebra", "interior.steering-cover", part + "steering-cover-zebra"),
                Wheels("wheelset-spoke", part + "wheel-spoke"),
                Cosmetic("wheel-cover-leopard", "interior.steering-cover", part + "steering-cover-leopard"),
                Single("fuel-mixture-gauge", "dashboard.gauge.fuel-mixture", part + "gauge-fuel-mixture"),
                Single("extra-gauges", "dashboard.gauge.cluster", part + "gauge-cluster-extra"),
                Wheels("wheelset-turbine", part + "wheel-turbine"),
                PairCosmetic("seat-cover-plush", "interior.seat-covers", part + "seat-cover-plush-left", part + "seat-cover-plush-right"),
                Pair("bucket-seats", "interior.seats", part + "bucket-seat-left", part + "bucket-seat-right"),
                Single("antenna", "body.antenna", part + "antenna"),
                Single("rally-wheel", "interior.steering-wheel", part + "steering-wheel-rally"),
                Single("racing-carburators", "engine.intake", part + "carburators-racing"),
                Single("front-spoiler", "body.front-spoiler", part + "front-spoiler"),
                Single("exhaust-dual-tip", "exhaust.tip", part + "exhaust-tip-dual"),
                Wheels("wheelset-rally", part + "wheel-rally"),
                Wheels("wheelset-hayosiko", part + "wheel-hayosiko"),
                Wheels("wheelset-octo", part + "wheel-octo"),
                Single("racing-radiator", "engine.cooling.radiator", part + "radiator-racing"),
                Kit("rally-suspension", "chassis.suspension-rally", part + "strut-rally-fl", part + "strut-rally-fr", part + "shock-rally-rl", part + "shock-rally-rr", part + "spring-rally-fl", part + "spring-rally-fr", part + "spring-rally-rl", part + "spring-rally-rr"),
                Cosmetic("dash-cover-zebra", "interior.dashboard-cover", part + "dashboard-cover-zebra"),
                Single("fiberglass-hood", "body.hood", part + "hood-fiberglass"),
            };
        }

        private static VehicleDeliveredPartCompatibilityRecord Single(
            string slug,
            string group,
            string target) => Create(
                slug,
                group,
                DeliveredPartResolutionKind.SinglePart,
                target);

        private static VehicleDeliveredPartCompatibilityRecord Pair(
            string slug,
            string group,
            params string[] targets) => Create(
                slug,
                group,
                DeliveredPartResolutionKind.MultiPartKit,
                targets);

        private static VehicleDeliveredPartCompatibilityRecord Kit(
            string slug,
            string group,
            params string[] targets) => Pair(slug, group, targets);

        private static VehicleDeliveredPartCompatibilityRecord Cosmetic(
            string slug,
            string group,
            string target) => Create(
                slug,
                group,
                DeliveredPartResolutionKind.CosmeticBinding,
                target);

        private static VehicleDeliveredPartCompatibilityRecord PairCosmetic(
            string slug,
            string group,
            params string[] targets) => Create(
                slug,
                group,
                DeliveredPartResolutionKind.CosmeticBinding,
                targets);

        private static VehicleDeliveredPartCompatibilityRecord Tool(
            string slug,
            string group) => Create(
                slug,
                group,
                DeliveredPartResolutionKind.Tool);

        private static VehicleDeliveredPartCompatibilityRecord Wheels(
            string slug,
            string wheelDefinitionPrefix) => Kit(
                slug,
                "chassis.wheels",
                wheelDefinitionPrefix + "-fl",
                wheelDefinitionPrefix + "-fr",
                wheelDefinitionPrefix + "-rl",
                wheelDefinitionPrefix + "-rr");

        private static VehicleDeliveredPartCompatibilityRecord Create(
            string slug,
            string group,
            DeliveredPartResolutionKind kind,
            params string[] targets) =>
            VehicleDeliveredPartCompatibilityRecord.Create(
                "item.mail-order." + slug,
                group,
                kind,
                targets);
    }
}
