using System;
using System.Collections.Generic;

namespace MSC.Editor.WorldBaseline
{
    public static class WorldBaseline06B3Paths
    {
        public const int SchemaVersion = 1;
        public const string ValidatorVersion = "1.1.0-06B3";
        public const string BaselineRevisionId =
            "DonorWorldBaseline-v001";
        public const string DebtCatalogueRevision =
            "legacy-visual-debt-v001";
        public const string PendingManualVehicleTraversal =
            "PendingManualVehicleTraversal";
        public const string PassedHumanAccepted =
            "PassedHumanAccepted";
        public const string Frozen =
            "Frozen";
        public const string RevisionPendingValidationStatus =
            "StructuralPass_ManualVehicleTraversalPending";
        public const string RevisionAcceptedValidationStatus =
            "StructuralPass_ManualVehicleTraversalAccepted";
        public const string FirstExportHashPlaceholder =
            "pending-first-export";
        public const string ManualVehicleTraversalEvidence =
            "PerformanceCaptures/Milestone06B3/" +
            "M06B3_VehicleTraversalEvidence.json";

        public const string FullMapValidationReport =
            "Docs/WorldBaseline/FULL_MAP_VALIDATION_REPORT.md";
        public const string TraversalValidation =
            "Docs/WorldBaseline/TRAVERSAL_VALIDATION.csv";
        public const string CollisionAndOutOfBoundsSafety =
            "Docs/WorldBaseline/COLLISION_AND_OOB_SAFETY.md";
        public const string LegacyVisualDebt =
            "Docs/WorldBaseline/LEGACY_VISUAL_DEBT.csv";
        public const string BaselineRevision =
            "Docs/WorldBaseline/BASELINE_REVISION.json";
        public const string BaselineRegenerationPolicy =
            "Docs/WorldBaseline/BASELINE_REGENERATION_POLICY.md";
        public const string PrivateBuildContentAudit =
            "Docs/WorldBaseline/PRIVATE_BUILD_CONTENT_AUDIT.md";
        public const string WeatherHandoff =
            "Docs/WorldBaseline/WEATHER_HANDOFF.md";
        public const string MilestoneReport =
            "Docs/Milestones/MILESTONE_06B3_REPORT.md";
        public const string FullMapValidationResult =
            "Docs/WorldBaseline/FULL_MAP_VALIDATION_RESULT.json";

        private static readonly string[] RequiredOutputFileArray =
        {
            FullMapValidationReport,
            TraversalValidation,
            CollisionAndOutOfBoundsSafety,
            LegacyVisualDebt,
            BaselineRevision,
            BaselineRegenerationPolicy,
            PrivateBuildContentAudit,
            WeatherHandoff,
            MilestoneReport
        };

        private static readonly string[] TraversalColumnArray =
        {
            "RouteId",
            "RouteKind",
            "Method",
            "Start",
            "End",
            "RequiredCoverage",
            "CellsObserved",
            "StreamingBoundaries",
            "CollisionFailures",
            "VisibleDuplicateOrMissingSections",
            "PeakLoadSpikeMilliseconds",
            "RecoveryUses",
            "KnownLegacyDebt",
            "Status",
            "Evidence",
            "Notes"
        };

        private static readonly string[] DebtColumnArray =
        {
            "DebtId",
            "Revision",
            "Category",
            "Classification",
            "LocationOrScope",
            "SourceEvidence",
            "CurrentSafety",
            "Blocking06B3",
            "ReplacementTarget",
            "Status",
            "Notes"
        };

        private static readonly string[] AllowedDebtClassificationArray =
        {
            "VisualOnly",
            "TraversalRisk",
            "GameplayBlocker",
            "PerformanceRisk",
            "ReplacementPlanned",
            "Unknown"
        };

        public static IReadOnlyList<string> RequiredOutputFiles =>
            Array.AsReadOnly(RequiredOutputFileArray);

        public static IReadOnlyList<string> TraversalColumns =>
            Array.AsReadOnly(TraversalColumnArray);

        public static IReadOnlyList<string> DebtColumns =>
            Array.AsReadOnly(DebtColumnArray);

        public static IReadOnlyList<string> AllowedDebtClassifications =>
            Array.AsReadOnly(AllowedDebtClassificationArray);
    }
}
