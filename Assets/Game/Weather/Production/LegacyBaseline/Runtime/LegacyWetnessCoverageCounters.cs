namespace MSC.Weather.Production.LegacyBaseline
{
    /// <summary>
    /// Runtime shader-family disposition for one explicit legacy binding slot.
    /// Temporary water is deliberately grouped with transparent materials because
    /// both are rejected before the project-owned allowlist is considered.
    /// </summary>
    public enum LegacyWetnessSlotCoverage
    {
        OpaqueLit = 0,
        AlphaClipLit = 1,
        ExcludedTransparentOrWater = 2,
        ExcludedUnlit = 3,
        ExcludedEmissive = 4,
        ExcludedUnsupported = 5
    }

    /// <summary>
    /// Immutable coverage report for explicit bindings in loaded scenes.
    /// Physical shader compatibility and reviewed production approval are reported
    /// separately so an opaque material is never mistaken for approved coverage.
    /// </summary>
    public readonly struct LegacyWetnessCoverageCounters
    {
        public LegacyWetnessCoverageCounters(
            int registeredSceneCount,
            int registeredBindingCount,
            int rejectedBindingCount,
            int totalSlotCount,
            int opaqueLitSlotCount,
            int alphaClipLitSlotCount,
            int approvedGroundSlotCount,
            int approvedRoadSlotCount,
            int approvedExteriorSlotCount,
            int approvedVegetationSlotCount,
            int excludedTransparentOrWaterSlotCount,
            int excludedUnlitSlotCount,
            int excludedEmissiveSlotCount,
            int excludedUnsupportedSlotCount,
            int excludedNotAllowlistedSlotCount,
            int excludedCategoryMismatchSlotCount,
            int excludedInactivePresentationSlotCount)
        {
            RegisteredSceneCount = registeredSceneCount;
            RegisteredBindingCount = registeredBindingCount;
            RejectedBindingCount = rejectedBindingCount;
            TotalSlotCount = totalSlotCount;
            OpaqueLitSlotCount = opaqueLitSlotCount;
            AlphaClipLitSlotCount = alphaClipLitSlotCount;
            ApprovedGroundSlotCount = approvedGroundSlotCount;
            ApprovedRoadSlotCount = approvedRoadSlotCount;
            ApprovedExteriorSlotCount = approvedExteriorSlotCount;
            ApprovedVegetationSlotCount = approvedVegetationSlotCount;
            ExcludedTransparentOrWaterSlotCount =
                excludedTransparentOrWaterSlotCount;
            ExcludedUnlitSlotCount = excludedUnlitSlotCount;
            ExcludedEmissiveSlotCount = excludedEmissiveSlotCount;
            ExcludedUnsupportedSlotCount = excludedUnsupportedSlotCount;
            ExcludedNotAllowlistedSlotCount =
                excludedNotAllowlistedSlotCount;
            ExcludedCategoryMismatchSlotCount =
                excludedCategoryMismatchSlotCount;
            ExcludedInactivePresentationSlotCount =
                excludedInactivePresentationSlotCount;
        }

        public int RegisteredSceneCount { get; }
        public int RegisteredBindingCount { get; }
        public int RejectedBindingCount { get; }
        public int TotalSlotCount { get; }
        public int OpaqueLitSlotCount { get; }
        public int AlphaClipLitSlotCount { get; }
        public int ApprovedGroundSlotCount { get; }
        public int ApprovedRoadSlotCount { get; }
        public int ApprovedExteriorSlotCount { get; }
        public int ApprovedVegetationSlotCount { get; }
        public int ExcludedTransparentOrWaterSlotCount { get; }
        public int ExcludedUnlitSlotCount { get; }
        public int ExcludedEmissiveSlotCount { get; }
        public int ExcludedUnsupportedSlotCount { get; }
        public int ExcludedNotAllowlistedSlotCount { get; }
        public int ExcludedCategoryMismatchSlotCount { get; }
        public int ExcludedInactivePresentationSlotCount { get; }

        public int FamilyCompatibleSlotCount =>
            OpaqueLitSlotCount + AlphaClipLitSlotCount;

        public int ApprovedSlotCount =>
            ApprovedGroundSlotCount +
            ApprovedRoadSlotCount +
            ApprovedExteriorSlotCount +
            ApprovedVegetationSlotCount;

        public int ExcludedSlotCount => TotalSlotCount - ApprovedSlotCount;
    }
}
