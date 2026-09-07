using System;

namespace MSC.Vehicle.Assembly
{
    public enum SatsumaEngineAdjustmentKind
    {
        Unbound = 0,
        Alternator = 1,
        Distributor = 2,
        CarburetorMixture = 3,
        OilFilter = 4,
    }

    /// <summary>Reviewed stock-part controls, not a generic engine tuning model.</summary>
    public static class SatsumaEngineAdjustmentRules
    {
        public static string PartId(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => "vehicle.satsuma.part.alternator",
            SatsumaEngineAdjustmentKind.Distributor => "vehicle.satsuma.part.distributor",
            SatsumaEngineAdjustmentKind.CarburetorMixture => "vehicle.satsuma.part.carburetor",
            SatsumaEngineAdjustmentKind.OilFilter => "vehicle.satsuma.part.oilfilter0",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static string MountId(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => "mount.satsuma.engine-block.alternator",
            SatsumaEngineAdjustmentKind.Distributor => "mount.satsuma.engine-block.distributor",
            SatsumaEngineAdjustmentKind.CarburetorMixture => "mount.satsuma.cylinder-head.carburetor",
            SatsumaEngineAdjustmentKind.OilFilter => "mount.satsuma.engine-block.oil-filter",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static string ClampFastenerId(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => "fastener.satsuma.engine-block-alternator.boltpm-3",
            SatsumaEngineAdjustmentKind.Distributor => "fastener.satsuma.engine-block-distributor.boltpm-1",
            _ => string.Empty,
        };

        public static float InitialValue(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => 2f,
            SatsumaEngineAdjustmentKind.Distributor => 15f,
            SatsumaEngineAdjustmentKind.CarburetorMixture => 15f,
            SatsumaEngineAdjustmentKind.OilFilter => 0f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static float Minimum(SatsumaEngineAdjustmentKind kind) =>
            kind == SatsumaEngineAdjustmentKind.CarburetorMixture ? 10f : 0f;

        public static float Maximum(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => 8f,
            SatsumaEngineAdjustmentKind.Distributor => 20f,
            SatsumaEngineAdjustmentKind.CarburetorMixture => 22f,
            SatsumaEngineAdjustmentKind.OilFilter => 8f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static float Step(SatsumaEngineAdjustmentKind kind) => kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => 0.5f,
            SatsumaEngineAdjustmentKind.Distributor => 0.2f,
            SatsumaEngineAdjustmentKind.CarburetorMixture => 0.2f,
            SatsumaEngineAdjustmentKind.OilFilter => 1f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static bool IsValid(SatsumaEngineAdjustmentKind kind, float value) =>
            (int)kind >= (int)SatsumaEngineAdjustmentKind.Alternator &&
            (int)kind <= (int)SatsumaEngineAdjustmentKind.OilFilter && float.IsFinite(value) &&
            value >= Minimum(kind) && value <= Maximum(kind) &&
            (kind != SatsumaEngineAdjustmentKind.OilFilter || value == (int)value);

        public static bool TryStep(SatsumaEngineAdjustmentKind kind, float value,
            float signedNotches, out float next)
        {
            next = value;
            if (!IsValid(kind, value) || !float.IsFinite(signedNotches) ||
                Math.Abs(signedNotches) < 0.001f) return false;
            // Frozen HandRotate and mixture Screw map positive scroll to a
            // decreasing value; the filter instead tightens by increasing Stage.
            float direction = signedNotches > 0f ? 1f : -1f;
            if (kind != SatsumaEngineAdjustmentKind.OilFilter) direction = -direction;
            next = Math.Clamp(value + direction * Step(kind), Minimum(kind), Maximum(kind));
            return next != value || kind == SatsumaEngineAdjustmentKind.CarburetorMixture;
        }
    }
}
