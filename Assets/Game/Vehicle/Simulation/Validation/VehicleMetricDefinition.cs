using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    [Serializable]
    public sealed class VehicleMetricDefinition
    {
        [SerializeField] private string metricId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string unit = string.Empty;
        [SerializeField] private VehicleMetricDefinitionStatus status;
        [SerializeField] private VehicleReferenceClassification classification = VehicleReferenceClassification.Unknown;
        [SerializeField] private VehicleMetricStatistic comparisonStatistic;
        [SerializeField] private VehicleMetricDirection direction;
        [SerializeField, Min(0f)] private float defaultAbsoluteTolerance;
        [SerializeField, Min(0f)] private float defaultRelativeTolerance01;
        [SerializeField, Min(1)] private int minimumValidSamples = 1;
        [SerializeField] private bool enforceRepeatability;
        [SerializeField, Min(0f)] private float maximumCoefficientOfVariation;
        [SerializeField] private string blockedReason = string.Empty;
        [SerializeField, TextArea] private string notes = string.Empty;

        public string MetricId => metricId;
        public string DisplayName => displayName;
        public string Unit => unit;
        public VehicleMetricDefinitionStatus Status => status;
        public VehicleReferenceClassification Classification => classification;
        public VehicleMetricStatistic ComparisonStatistic => comparisonStatistic;
        public VehicleMetricDirection Direction => direction;
        public float DefaultAbsoluteTolerance => defaultAbsoluteTolerance;
        public float DefaultRelativeTolerance01 => defaultRelativeTolerance01;
        public int MinimumValidSamples => minimumValidSamples;
        public bool EnforceRepeatability => enforceRepeatability;
        public float MaximumCoefficientOfVariation => maximumCoefficientOfVariation;
        public string BlockedReason => blockedReason;
        public string Notes => notes;

        public VehicleMetricDefinition()
        {
        }

        public VehicleMetricDefinition(
            string valueMetricId,
            string valueDisplayName,
            string valueUnit,
            VehicleMetricDefinitionStatus valueStatus,
            VehicleReferenceClassification valueClassification,
            VehicleMetricStatistic valueComparisonStatistic,
            VehicleMetricDirection valueDirection,
            float valueDefaultAbsoluteTolerance,
            float valueDefaultRelativeTolerance01,
            int valueMinimumValidSamples,
            bool valueEnforceRepeatability,
            float valueMaximumCoefficientOfVariation,
            string valueBlockedReason,
            string valueNotes)
        {
            metricId = valueMetricId ?? string.Empty;
            displayName = valueDisplayName ?? string.Empty;
            unit = valueUnit ?? string.Empty;
            status = valueStatus;
            classification = valueClassification;
            comparisonStatistic = valueComparisonStatistic;
            direction = valueDirection;
            defaultAbsoluteTolerance = valueDefaultAbsoluteTolerance;
            defaultRelativeTolerance01 = valueDefaultRelativeTolerance01;
            minimumValidSamples = valueMinimumValidSamples;
            enforceRepeatability = valueEnforceRepeatability;
            maximumCoefficientOfVariation = valueMaximumCoefficientOfVariation;
            blockedReason = valueBlockedReason ?? string.Empty;
            notes = valueNotes ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(metricId) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(unit))
            {
                failure = "Metric ID, display name and unit must be explicit.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFiniteNonNegative(defaultAbsoluteTolerance) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(defaultRelativeTolerance01) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(maximumCoefficientOfVariation) ||
                minimumValidSamples < 1)
            {
                failure = $"Metric '{metricId}' contains invalid tolerance or sample values.";
                return false;
            }

            if (status == VehicleMetricDefinitionStatus.Blocked && string.IsNullOrWhiteSpace(blockedReason))
            {
                failure = $"Blocked metric '{metricId}' requires a reason.";
                return false;
            }

            if (classification == VehicleReferenceClassification.Unknown &&
                status == VehicleMetricDefinitionStatus.Active)
            {
                failure = $"Active metric '{metricId}' must have an explicit non-Unknown classification.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleReferenceTarget
    {
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string metricId = string.Empty;
        [SerializeField] private VehicleReferenceClassification classification = VehicleReferenceClassification.Unknown;
        [SerializeField] private VehicleReferenceTargetStatus status = VehicleReferenceTargetStatus.Unknown;
        [SerializeField] private VehicleReferenceTargetMode mode;
        [SerializeField] private float targetValue;
        [SerializeField] private float minimumValue;
        [SerializeField] private float maximumValue;
        [SerializeField] private bool overridesTolerance;
        [SerializeField, Min(0f)] private float absoluteTolerance;
        [SerializeField, Min(0f)] private float relativeTolerance01;
        [SerializeField, Range(0f, 1f)] private float confidence01;
        [SerializeField] private string source = string.Empty;
        [SerializeField, TextArea] private string notes = string.Empty;

        public string FixtureId => fixtureId;
        public string MetricId => metricId;
        public VehicleReferenceClassification Classification => classification;
        public VehicleReferenceTargetStatus Status => status;
        public VehicleReferenceTargetMode Mode => mode;
        public float TargetValue => targetValue;
        public float MinimumValue => minimumValue;
        public float MaximumValue => maximumValue;
        public bool OverridesTolerance => overridesTolerance;
        public float AbsoluteTolerance => absoluteTolerance;
        public float RelativeTolerance01 => relativeTolerance01;
        public float Confidence01 => confidence01;
        public string Source => source;
        public string Notes => notes;
        public bool CanCompare =>
            status == VehicleReferenceTargetStatus.Available ||
            status == VehicleReferenceTargetStatus.Provisional;

        public VehicleReferenceTarget()
        {
        }

        public VehicleReferenceTarget(
            string valueFixtureId,
            string valueMetricId,
            VehicleReferenceClassification valueClassification,
            VehicleReferenceTargetStatus valueStatus,
            VehicleReferenceTargetMode valueMode,
            float valueTargetValue,
            float valueMinimumValue,
            float valueMaximumValue,
            bool valueOverridesTolerance,
            float valueAbsoluteTolerance,
            float valueRelativeTolerance01,
            float valueConfidence01,
            string valueSource,
            string valueNotes)
        {
            fixtureId = valueFixtureId ?? string.Empty;
            metricId = valueMetricId ?? string.Empty;
            classification = valueClassification;
            status = valueStatus;
            mode = valueMode;
            targetValue = valueTargetValue;
            minimumValue = valueMinimumValue;
            maximumValue = valueMaximumValue;
            overridesTolerance = valueOverridesTolerance;
            absoluteTolerance = valueAbsoluteTolerance;
            relativeTolerance01 = valueRelativeTolerance01;
            confidence01 = valueConfidence01;
            source = valueSource ?? string.Empty;
            notes = valueNotes ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(fixtureId) || string.IsNullOrWhiteSpace(metricId))
            {
                failure = "Reference target requires fixture and metric IDs.";
                return false;
            }

            if (!VehicleCalibrationValidation.AreFinite(
                    targetValue,
                    minimumValue,
                    maximumValue,
                    absoluteTolerance,
                    relativeTolerance01,
                    confidence01) ||
                absoluteTolerance < 0f || relativeTolerance01 < 0f ||
                confidence01 < 0f || confidence01 > 1f)
            {
                failure = $"Reference target '{fixtureId}/{metricId}' contains invalid numeric values.";
                return false;
            }

            if (status == VehicleReferenceTargetStatus.Unknown || status == VehicleReferenceTargetStatus.Blocked)
            {
                if (classification != VehicleReferenceClassification.Unknown ||
                    mode != VehicleReferenceTargetMode.Unspecified ||
                    string.IsNullOrWhiteSpace(notes))
                {
                    failure = "Unknown or blocked targets must use Unknown/Unspecified and explain the gap.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            if (classification == VehicleReferenceClassification.Unknown ||
                mode == VehicleReferenceTargetMode.Unspecified ||
                string.IsNullOrWhiteSpace(source))
            {
                failure = "Comparable target requires classification, mode and source.";
                return false;
            }

            if (mode == VehicleReferenceTargetMode.InclusiveRange && minimumValue > maximumValue)
            {
                failure = "Reference target minimum exceeds maximum.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public float ResolveAbsoluteTolerance(VehicleMetricDefinition definition)
        {
            return overridesTolerance ? absoluteTolerance : definition.DefaultAbsoluteTolerance;
        }

        public float ResolveRelativeTolerance(VehicleMetricDefinition definition)
        {
            return overridesTolerance ? relativeTolerance01 : definition.DefaultRelativeTolerance01;
        }
    }

    [Serializable]
    public sealed class VehicleMetricResult
    {
        [SerializeField] private string runId = string.Empty;
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string metricId = string.Empty;
        [SerializeField] private VehicleReferenceClassification classification = VehicleReferenceClassification.Unknown;
        [SerializeField] private VehicleMetricResultStatus status;
        [SerializeField] private VehicleStatisticalSummary summary;
        [SerializeField] private float comparisonValue;
        [SerializeField] private float targetValue;
        [SerializeField] private float acceptedMinimum;
        [SerializeField] private float acceptedMaximum;
        [SerializeField] private float absoluteDelta;
        [SerializeField] private float relativeDelta01;
        [SerializeField] private bool hasRelativeDelta;
        [SerializeField] private bool repeatabilityPassed;
        [SerializeField] private string reason = string.Empty;

        public string RunId => runId;
        public string FixtureId => fixtureId;
        public string MetricId => metricId;
        public VehicleReferenceClassification Classification => classification;
        public VehicleMetricResultStatus Status => status;
        public VehicleStatisticalSummary Summary => summary;
        public float ComparisonValue => comparisonValue;
        public float TargetValue => targetValue;
        public float AcceptedMinimum => acceptedMinimum;
        public float AcceptedMaximum => acceptedMaximum;
        public float AbsoluteDelta => absoluteDelta;
        public float RelativeDelta01 => relativeDelta01;
        public bool HasRelativeDelta => hasRelativeDelta;
        public bool RepeatabilityPassed => repeatabilityPassed;
        public string Reason => reason;
        public bool IsPass => status == VehicleMetricResultStatus.Passed;

        public VehicleMetricResult()
        {
        }

        public VehicleMetricResult(
            string valueRunId,
            string valueFixtureId,
            string valueMetricId,
            VehicleReferenceClassification valueClassification,
            VehicleMetricResultStatus valueStatus,
            VehicleStatisticalSummary valueSummary,
            float valueComparisonValue,
            float valueTargetValue,
            float valueAcceptedMinimum,
            float valueAcceptedMaximum,
            float valueAbsoluteDelta,
            float valueRelativeDelta01,
            bool valueHasRelativeDelta,
            bool valueRepeatabilityPassed,
            string valueReason)
        {
            runId = valueRunId ?? string.Empty;
            fixtureId = valueFixtureId ?? string.Empty;
            metricId = valueMetricId ?? string.Empty;
            classification = valueClassification;
            status = valueStatus;
            summary = valueSummary;
            comparisonValue = valueComparisonValue;
            targetValue = valueTargetValue;
            acceptedMinimum = valueAcceptedMinimum;
            acceptedMaximum = valueAcceptedMaximum;
            absoluteDelta = valueAbsoluteDelta;
            relativeDelta01 = valueRelativeDelta01;
            hasRelativeDelta = valueHasRelativeDelta;
            repeatabilityPassed = valueRepeatabilityPassed;
            reason = valueReason ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(runId) ||
                string.IsNullOrWhiteSpace(fixtureId) ||
                string.IsNullOrWhiteSpace(metricId))
            {
                failure = "Metric result requires run, fixture and metric IDs.";
                return false;
            }

            if (!summary.Validate(out failure))
            {
                failure = $"Invalid metric summary: {failure}";
                return false;
            }

            if (!VehicleCalibrationValidation.AreFinite(
                    comparisonValue,
                    targetValue,
                    acceptedMinimum,
                    acceptedMaximum,
                    absoluteDelta,
                    relativeDelta01))
            {
                failure = "Metric result contains non-finite comparison data.";
                return false;
            }

            if (acceptedMinimum > acceptedMaximum)
            {
                failure = "Metric result accepted minimum exceeds maximum.";
                return false;
            }

            if (status == VehicleMetricResultStatus.Passed &&
                (!summary.HasValidSamples || summary.HasInvalidSamples ||
                 comparisonValue < acceptedMinimum || comparisonValue > acceptedMaximum ||
                 !repeatabilityPassed))
            {
                failure = "A passed metric result must have valid samples, accepted bounds, and repeatability.";
                return false;
            }

            if (status == VehicleMetricResultStatus.InvalidSamples && !summary.HasInvalidSamples)
            {
                failure = "InvalidSamples status requires at least one invalid sample.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
