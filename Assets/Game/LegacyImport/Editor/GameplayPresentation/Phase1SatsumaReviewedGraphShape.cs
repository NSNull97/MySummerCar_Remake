using System;
using System.Collections.Generic;
using System.Linq;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Frozen pre-consumable mount identities plus the reviewed additive
    /// packets. Scoped refreshes must not mistake an arbitrary larger roster
    /// for a supported graph revision. This is validation, not runtime lookup.
    /// </summary>
    internal static class Phase1SatsumaReviewedGraphShape
    {
        private static readonly string[] BaseMountSlugs =
        {
            "battery", "bootlid", "brake-lining", "brake-master-cylinder", "bumper-front", "bumper-rear",
            "camshaft-gear.timing-chain", "camshaft.camshaft-gear", "carburetor.airfilter",
            "clutch-cover-plate.clutch-disc", "clutch-cover-plate.clutch-pressure-plate", "clutch-lining",
            "clutch-master-cylinder", "coilspring-rl", "coilspring-rr", "crankshaft.crankshaft-pulley",
            "crankshaft.flywheel", "cylinder-head.carburetor", "cylinder-head.headers",
            "cylinder-head.rocker-cover", "cylinder-head.rocker-shaft", "dashboard",
            "dashboard-meters.gauge", "dashboard-meters.radio", "dashboard.dash-cover", "dashboard.meters",
            "discbrake-fl", "discbrake-fr", "door-left", "door-right", "drum-brake-rl", "drum-brake-rr",
            "electrics", "engine-assembly", "engine-block.alternator", "engine-block.camshaft",
            "engine-block.crankshaft", "engine-block.cylinder-head", "engine-block.distributor",
            "engine-block.engine-plate", "engine-block.fuel-pump", "engine-block.gearbox",
            "engine-block.head-gasket", "engine-block.main-bearing1", "engine-block.main-bearing2",
            "engine-block.main-bearing3", "engine-block.oil-filter", "engine-block.oilpan",
            "engine-block.piston1", "engine-block.piston2", "engine-block.piston3", "engine-block.piston4",
            "engine-block.radiator-hose2", "engine-block.timing-cover", "engine-plate.starter",
            "exhaust-muffler", "exhaust-pipe", "fender-left", "fender-left.mudflap-fl", "fender-right",
            "fender-right.mudflap-fr", "flywheel.clutch-cover-plate", "fuel-strainer", "fuel-tank",
            "fuel-tank-pipe", "fur-dices", "gear-linkage", "gear-stick", "gearbox.drive-gear",
            "gearbox.inspection-cover", "grille", "halfshaft-fl", "halfshaft-fr", "handbrake",
            "headlight-left", "headlight-right", "hood", "long-coilspring-rl", "long-coilspring-rr",
            "mudflap-rl", "mudflap-rr", "panel-back", "radiator", "radiator-hose1", "radiator-hose3",
            "rearlight-left", "rearlight-right", "seat-driver", "seat-passenger", "seat-rear",
            "shock-rl", "shock-rr", "spindle-fl", "spindle-fr", "steering-column", "steering-rack",
            "steering-rod-fl", "steering-rod-fr", "steering-wheel", "stock-steering-wheel.wheel-cover",
            "strut-fl", "strut-fr", "sub-frame", "timing-cover.water-pump", "trail-arm-rl", "trail-arm-rr",
            "water-pump.water-pump-pulley", "wheel-stock-fl.hubcap", "wheel-stock-fr.hubcap",
            "wheel-stock-rl.hubcap", "wheel-stock-rr.hubcap", "wheelfl-new", "wheelfr-new",
            "wheelrl-new", "wheelrr-new", "wishbone-fl", "wishbone-fr",
        };
        private static readonly HashSet<string> BaseMountIds = new HashSet<string>(
            BaseMountSlugs.Select(slug => "mount.satsuma." + slug), StringComparer.Ordinal);
        private static readonly HashSet<string> AddedMountIds = new HashSet<string>(new[]
        {
            "mount.satsuma.cylinder-head.spark-plug-1", "mount.satsuma.cylinder-head.spark-plug-2",
            "mount.satsuma.cylinder-head.spark-plug-3", "mount.satsuma.cylinder-head.spark-plug-4",
            "mount.satsuma.engine-block.alternator-belt", "mount.satsuma.headlight-left.light-bulb",
            "mount.satsuma.headlight-right.light-bulb",
        }, StringComparer.Ordinal);
        private static readonly HashSet<string> StockFastenerIds = BuildStockFastenerIds();
        private static readonly HashSet<string> HeadlightFastenerIds = new HashSet<string>(new[]
        {
            "fastener.satsuma.headlight-left.boltpm-1", "fastener.satsuma.headlight-left.boltpm-2",
            "fastener.satsuma.headlight-right.boltpm-1", "fastener.satsuma.headlight-right.boltpm-2",
        }, StringComparer.Ordinal);
        private static readonly HashSet<string> PlugFastenerIds = new HashSet<string>(
            Enumerable.Range(1, 4).Select(index => "fastener.satsuma.cylinder-head-spark-plug-" + index + ".thread"),
            StringComparer.Ordinal);

        internal static bool HasReviewedMountRoster(IEnumerable<string> mountIds)
        {
            if (mountIds == null) return false;
            string[] values = mountIds.ToArray();
            var unique = new HashSet<string>(values, StringComparer.Ordinal);
            if (values.Length != unique.Count || values.Any(string.IsNullOrWhiteSpace)) return false;
            if (unique.SetEquals(BaseMountIds)) return true;
            return unique.Count == BaseMountIds.Count + AddedMountIds.Count &&
                BaseMountIds.IsSubsetOf(unique) && AddedMountIds.IsSubsetOf(unique);
        }

        internal static bool HasReviewedFastenerRoster(IEnumerable<string> fastenerIds, int predecessorCount)
        {
            if (fastenerIds == null) return false;
            string[] values = fastenerIds.ToArray();
            var unique = new HashSet<string>(values, StringComparer.Ordinal);
            if (values.Length != unique.Count || values.Any(string.IsNullOrWhiteSpace)) return false;
            int stock = unique.Count(StockFastenerIds.Contains);
            int plugs = unique.Count(PlugFastenerIds.Contains);
            int headlights = unique.Count(HeadlightFastenerIds.Contains);
            return (stock == 0 || stock == StockFastenerIds.Count) &&
                (plugs == 0 || plugs == PlugFastenerIds.Count) &&
                // The four headlamp bolts were added after the complete
                // stock21 packet. Keep the headlamp-less298 cohort loadable,
                // but never recognize a partial or reordered body revision.
                (headlights == 0 || headlights == HeadlightFastenerIds.Count && stock == StockFastenerIds.Count) &&
                unique.Count - stock - plugs - headlights == predecessorCount;
        }

        private static HashSet<string> BuildStockFastenerIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in new[] { ("exhaust-pipe", 3), ("exhaust-muffler", 1), ("fuel-tank", 7),
                ("seat-driver", 4), ("seat-passenger", 4), ("seat-rear", 2) })
                for (int index = 1; index <= group.Item2; index++)
                    ids.Add("fastener.satsuma." + group.Item1 + ".boltpm-" + index);
            return ids;
        }
    }
}
