using System;
using System.Linq;

namespace MSC.World.Remaster.Editor
{
    public enum WorldValidationGate
    {
        None,
        PilotGate,
        VerticalSliceGate,
        FullWorldGate
    }

    public enum WorldValidationIssueState
    {
        Open,
        Closed
    }

    [Serializable]
    public sealed class WorldValidationIssue
    {
        public string issueId = string.Empty;
        public string state = string.Empty;
        public string severity = string.Empty;
        public string domain = string.Empty;
        public string blockingGates = string.Empty;
        public string zoneId = string.Empty;
        public string stableWorldId = string.Empty;
        public string summary = string.Empty;
        public string evidence = string.Empty;
        public string requiredAction = string.Empty;

        public bool IsOpen => string.Equals(state, WorldValidationIssueState.Open.ToString(), StringComparison.Ordinal);

        public bool Blocks(WorldValidationGate gate) =>
            IsOpen && blockingGates.Split(';').Any(value =>
                string.Equals(value.Trim(), gate.ToString(), StringComparison.Ordinal));
    }

    [Serializable]
    public sealed class WorldValidationCoverageRow
    {
        public string scope = string.Empty;
        public string scopeId = string.Empty;
        public int total;
        public int covered;
        public float coveragePercent;
        public int approved;
        public int referenceFallbacks;
        public int missing;
        public int blocked;
        public string status = string.Empty;
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class WorldValidationSpatialRow
    {
        public string metricId = string.Empty;
        public string domain = string.Empty;
        public string zoneId = string.Empty;
        public string stableWorldId = string.Empty;
        public string expected = string.Empty;
        public string measured = string.Empty;
        public float deviationMeters;
        public float toleranceMeters;
        public string status = string.Empty;
        public string evidence = string.Empty;
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class WorldValidationPerformanceLocation
    {
        public string locationId = string.Empty;
        public string zoneId = string.Empty;
        public string status = string.Empty;
        public string availableMetrics = string.Empty;
        public string unavailableMetrics = string.Empty;
        public string evidence = string.Empty;
    }

    [Serializable]
    public sealed class WorldValidationGateResult
    {
        public string gate = string.Empty;
        public bool achieved;
        public string[] blockingIssueIds = Array.Empty<string>();
        public string evidence = string.Empty;
    }

    [Serializable]
    public sealed class WorldValidationDependencyAudit
    {
        public int seedAssets;
        public int visitedAssets;
        public int dependencyEdges;
        public string[] violations = Array.Empty<string>();
        public string[] enabledBuildScenes = Array.Empty<string>();
        public bool editorAssemblyPlayerAuditAvailable;
    }

    [Serializable]
    public sealed class WorldValidationValidatorRun
    {
        public string validatorId = string.Empty;
        public bool executed;
        public bool passed;
        public string status = string.Empty;
        public int errorCount;
        public int warningCount;
        public string evidence = string.Empty;
        public string[] errors = Array.Empty<string>();
        public string[] warnings = Array.Empty<string>();
    }

    [Serializable]
    public sealed class WorldValidationResult
    {
        public int schemaVersion = 1;
        public string validatorVersion = "05B.1";
        public string sourceDatabaseVersion = string.Empty;
        public string scope = "Project";
        public string evaluatedGate = string.Empty;
        public string selectedZone = string.Empty;
        public string achievedGate = string.Empty;
        public int sourceRecordCount;
        public int eligibleWorldRecordCount;
        public int productionBindingCount;
        public int concreteCellCount;
        public int productionBoundCellCount;
        public int supplementalSafetyPieceCount;
        public int approvedReplacementCount;
        public WorldValidationGateResult[] gates = Array.Empty<WorldValidationGateResult>();
        public WorldValidationIssue[] issues = Array.Empty<WorldValidationIssue>();
        public WorldValidationCoverageRow[] coverage = Array.Empty<WorldValidationCoverageRow>();
        public WorldValidationSpatialRow[] spatialDeviation = Array.Empty<WorldValidationSpatialRow>();
        public WorldValidationPerformanceLocation[] performanceLocations = Array.Empty<WorldValidationPerformanceLocation>();
        public WorldValidationDependencyAudit dependencyAudit = new WorldValidationDependencyAudit();
        public WorldValidationValidatorRun[] validatorRuns = Array.Empty<WorldValidationValidatorRun>();

        public WorldValidationGate HighestAchievedGate =>
            WorldValidationGateCalculator.HighestAchieved(gates);
    }

    public static class WorldValidationGateCalculator
    {
        public static WorldValidationGate HighestAchieved(WorldValidationGateResult[] results)
        {
            if (!IsAchieved(results, WorldValidationGate.PilotGate))
            {
                return WorldValidationGate.None;
            }

            if (!IsAchieved(results, WorldValidationGate.VerticalSliceGate))
            {
                return WorldValidationGate.PilotGate;
            }

            return IsAchieved(results, WorldValidationGate.FullWorldGate)
                ? WorldValidationGate.FullWorldGate
                : WorldValidationGate.VerticalSliceGate;
        }

        public static float CoveragePercent(int covered, int total) =>
            total <= 0 ? 0f : 100f * covered / total;

        public static float Percentile95(float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return float.NaN;
            }

            float[] ordered = values.OrderBy(value => value).ToArray();
            int index = (int)Math.Ceiling(ordered.Length * 0.95) - 1;
            return ordered[Math.Max(0, Math.Min(index, ordered.Length - 1))];
        }

        private static bool IsAchieved(WorldValidationGateResult[] results, WorldValidationGate gate) =>
            results != null && results.Any(result =>
                string.Equals(result.gate, gate.ToString(), StringComparison.Ordinal) && result.achieved);
    }
}
