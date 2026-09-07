using System;

namespace MSC.Items.Presentation
{
    // Serialized project-owned identities. Donor hierarchy names are resolved
    // by the Editor importer only, never by the runtime interaction system.
    public enum SpannerSetAuxiliaryToolKind
    {
        Unbound = 0,
        Screwdriver = 1,
        SparkPlugWrench = 2,
        Ruler = 3,
    }

    public static class SpannerSetAuxiliaryTools
    {
        public const string UnsizedVariant = "0";

        public static string ToolType(SpannerSetAuxiliaryToolKind kind) => kind switch
        {
            SpannerSetAuxiliaryToolKind.Screwdriver => "Screwdriver",
            SpannerSetAuxiliaryToolKind.SparkPlugWrench => "SparkPlugWrench",
            SpannerSetAuxiliaryToolKind.Ruler => "Ruler",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        public static string DisplayName(SpannerSetAuxiliaryToolKind kind) => kind switch
        {
            SpannerSetAuxiliaryToolKind.Screwdriver => "Отвёртка",
            SpannerSetAuxiliaryToolKind.SparkPlugWrench => "Свечной ключ",
            SpannerSetAuxiliaryToolKind.Ruler => "Линейка",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
