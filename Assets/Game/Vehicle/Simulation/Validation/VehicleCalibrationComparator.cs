using System;
using System.Collections.Generic;

namespace MSC.Vehicle.Simulation
{
    public static class VehicleCalibrationComparator
    {
        private const float NearZero = 0.000001f;

        public static VehicleStatisticalSummary Summarize(IReadOnlyList<float> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return VehicleStatisticalSummary.Empty();
            }

            var valid = new float[samples.Count];
            int validCount = 0;
            double mean = 0d;
            double sumOfSquaredDifferences = 0d;
            float minimum = 0f;
            float maximum = 0f;
            float last = 0f;

            for (int index = 0; index < samples.Count; index++)
            {
                float value = samples[index];
                if (!VehicleCalibrationValidation.IsFinite(value))
                {
                    continue;
                }

                valid[validCount] = value;
                validCount++;
                last = value;
                if (validCount == 1)
                {
                    minimum = value;
                    maximum = value;
                }
                else
                {
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }

                double delta = value - mean;
                mean += delta / validCount;
                double deltaAfterMean = value - mean;
                sumOfSquaredDifferences += delta * deltaAfterMean;
            }

            int invalidCount = samples.Count - validCount;
            if (validCount == 0)
            {
                return VehicleStatisticalSummary.Empty(invalidCount);
            }

            Array.Sort(valid, 0, validCount);
            double median = validCount % 2 == 0
                ? ((double)valid[(validCount / 2) - 1] + valid[validCount / 2]) * 0.5d
                : valid[validCount / 2];
            double variance = validCount > 1
                ? Math.Max(0d, sumOfSquaredDifferences / (validCount - 1))
                : 0d;
            double standardDeviation = Math.Sqrt(variance);

            bool hasCoefficientOfVariation;
            double coefficientOfVariation;
            if (Math.Abs(mean) > NearZero)
            {
                coefficientOfVariation = Math.Abs(standardDeviation / mean);
                hasCoefficientOfVariation = true;
            }
            else if (standardDeviation <= NearZero)
            {
                coefficientOfVariation = 0d;
                hasCoefficientOfVariation = true;
            }
            else
            {
                coefficientOfVariation = 0d;
                hasCoefficientOfVariation = false;
            }

            return new VehicleStatisticalSummary(
                samples.Count,
                validCount,
                invalidCount,
                ToFiniteFloat(mean),
                ToFiniteFloat(median),
                minimum,
                maximum,
                ToFiniteNonNegativeFloat(standardDeviation),
                last,
                ToFiniteNonNegativeFloat(coefficientOfVariation),
                hasCoefficientOfVariation);
        }

        public static VehicleMetricResult Compare(
            string runId,
            VehicleMetricDefinition definition,
            VehicleReferenceTarget target,
            IReadOnlyList<float> samples,
            float fixtureRepeatedRunTolerance01 = -1f)
        {
            VehicleStatisticalSummary summary = Summarize(samples);
            return CompareSummary(
                runId,
                definition,
                target,
                summary,
                fixtureRepeatedRunTolerance01);
        }

        public static VehicleMetricResult CompareSummary(
            string runId,
            VehicleMetricDefinition definition,
            VehicleReferenceTarget target,
            VehicleStatisticalSummary summary,
            float fixtureRepeatedRunTolerance01 = -1f)
        {
            string fixtureId = target?.FixtureId ?? "invalid.fixture";
            string metricId = definition?.MetricId ?? target?.MetricId ?? "invalid.metric";
            VehicleReferenceClassification classification =
                target?.Classification ?? definition?.Classification ?? VehicleReferenceClassification.Unknown;
            bool summaryIsValid = summary.Validate(out string summaryFailure);

            if (definition == null || target == null ||
                string.IsNullOrWhiteSpace(runId) ||
                !summaryIsValid ||
                (fixtureRepeatedRunTolerance01 < 0f && fixtureRepeatedRunTolerance01 != -1f) ||
                !VehicleCalibrationValidation.IsFinite(fixtureRepeatedRunTolerance01))
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.InvalidConfiguration,
                    summary,
                    0f,
                    0f,
                    0f,
                    0f,
                    false,
                    $"Invalid comparator configuration or summary: {summaryFailure}");
            }

            bool definitionIsValid = definition.Validate(out string definitionFailure);
            bool targetIsValid = target.Validate(out string targetFailure);
            if (!definitionIsValid || !targetIsValid ||
                !string.Equals(definition.MetricId, target.MetricId, StringComparison.Ordinal))
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.InvalidConfiguration,
                    summary,
                    0f,
                    0f,
                    0f,
                    0f,
                    false,
                    $"Invalid metric or target: {definitionFailure}{targetFailure}" +
                    (string.Equals(definition.MetricId, target.MetricId, StringComparison.Ordinal)
                        ? string.Empty
                        : " Metric IDs do not match."));
            }

            float comparisonValue = summary.HasValidSamples
                ? summary.Select(definition.ComparisonStatistic)
                : 0f;

            if (summary.HasInvalidSamples)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.InvalidSamples,
                    summary,
                    comparisonValue,
                    target.TargetValue,
                    target.MinimumValue,
                    target.MaximumValue,
                    false,
                    $"{summary.InvalidSampleCount} non-finite sample(s) were rejected.");
            }

            if (definition.Status == VehicleMetricDefinitionStatus.Blocked ||
                target.Status == VehicleReferenceTargetStatus.Blocked)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.Blocked,
                    summary,
                    comparisonValue,
                    0f,
                    0f,
                    0f,
                    false,
                    FirstNonEmpty(definition.BlockedReason, target.Notes, "Metric is blocked."));
            }

            if (target.Status == VehicleReferenceTargetStatus.Unknown)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    VehicleReferenceClassification.Unknown,
                    VehicleMetricResultStatus.UnknownReference,
                    summary,
                    comparisonValue,
                    0f,
                    0f,
                    0f,
                    false,
                    target.Notes);
            }

            if (summary.ValidSampleCount < definition.MinimumValidSamples)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.Inconclusive,
                    summary,
                    comparisonValue,
                    target.TargetValue,
                    target.MinimumValue,
                    target.MaximumValue,
                    false,
                    $"Expected at least {definition.MinimumValidSamples} valid samples; received {summary.ValidSampleCount}.");
            }

            if (definition.Status == VehicleMetricDefinitionStatus.Informational ||
                definition.Direction == VehicleMetricDirection.Informational ||
                target.Mode == VehicleReferenceTargetMode.Informational)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.Informational,
                    summary,
                    comparisonValue,
                    target.TargetValue,
                    target.MinimumValue,
                    target.MaximumValue,
                    true,
                    "Metric is recorded for information and is not a pass/fail gate.");
            }

            bool repeatabilityPassed = EvaluateRepeatability(
                definition,
                summary,
                fixtureRepeatedRunTolerance01,
                out string repeatabilityFailure);
            if (!repeatabilityPassed)
            {
                return CreateResult(
                    runId,
                    fixtureId,
                    metricId,
                    classification,
                    VehicleMetricResultStatus.Failed,
                    summary,
                    comparisonValue,
                    target.TargetValue,
                    target.MinimumValue,
                    target.MaximumValue,
                    false,
                    repeatabilityFailure);
            }

            float absoluteTolerance = target.ResolveAbsoluteTolerance(definition);
            float relativeTolerance = target.ResolveRelativeTolerance(definition);
            ResolveAcceptedRange(
                target,
                absoluteTolerance,
                relativeTolerance,
                out float acceptedMinimum,
                out float acceptedMaximum,
                out float comparisonTarget);

            bool passed = comparisonValue >= acceptedMinimum && comparisonValue <= acceptedMaximum;
            return CreateResult(
                runId,
                fixtureId,
                metricId,
                classification,
                passed ? VehicleMetricResultStatus.Passed : VehicleMetricResultStatus.Failed,
                summary,
                comparisonValue,
                comparisonTarget,
                acceptedMinimum,
                acceptedMaximum,
                true,
                passed
                    ? "Observed statistic is within the accepted target bounds."
                    : "Observed statistic is outside the accepted target bounds.");
        }

        public static bool IsWithinRepeatedRunTolerance(
            VehicleStatisticalSummary summary,
            float maximumNormalizedSpread01,
            out float normalizedSpread01)
        {
            normalizedSpread01 = 0f;
            if (!summary.Validate(out _) ||
                !summary.HasValidSamples ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(maximumNormalizedSpread01))
            {
                return false;
            }

            float denominator = Math.Max(Math.Abs(summary.Mean), NearZero);
            normalizedSpread01 = ToFiniteNonNegativeFloat(
                ((double)summary.Maximum - summary.Minimum) / denominator);
            return normalizedSpread01 <= maximumNormalizedSpread01;
        }

        private static bool EvaluateRepeatability(
            VehicleMetricDefinition definition,
            VehicleStatisticalSummary summary,
            float fixtureRepeatedRunTolerance01,
            out string failure)
        {
            if (definition.EnforceRepeatability)
            {
                if (!summary.HasCoefficientOfVariation)
                {
                    failure = "Coefficient of variation is undefined for a near-zero mean.";
                    return false;
                }

                if (summary.CoefficientOfVariation > definition.MaximumCoefficientOfVariation)
                {
                    failure =
                        $"Coefficient of variation {summary.CoefficientOfVariation:G6} exceeds " +
                        $"{definition.MaximumCoefficientOfVariation:G6}.";
                    return false;
                }
            }

            if (fixtureRepeatedRunTolerance01 >= 0f &&
                !IsWithinRepeatedRunTolerance(summary, fixtureRepeatedRunTolerance01, out float spread))
            {
                failure =
                    $"Normalized repeated-run spread {spread:G6} exceeds " +
                    $"{fixtureRepeatedRunTolerance01:G6}.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static void ResolveAcceptedRange(
            VehicleReferenceTarget target,
            float absoluteTolerance,
            float relativeTolerance,
            out float acceptedMinimum,
            out float acceptedMaximum,
            out float comparisonTarget)
        {
            float toleranceReference = target.Mode == VehicleReferenceTargetMode.InclusiveRange
                ? Math.Max(Math.Abs(target.MinimumValue), Math.Abs(target.MaximumValue))
                : Math.Abs(target.TargetValue);
            float tolerance = Math.Max(
                absoluteTolerance,
                ToFiniteNonNegativeFloat((double)toleranceReference * relativeTolerance));

            switch (target.Mode)
            {
                case VehicleReferenceTargetMode.InclusiveRange:
                    acceptedMinimum = SafeSubtract(target.MinimumValue, tolerance);
                    acceptedMaximum = SafeAdd(target.MaximumValue, tolerance);
                    comparisonTarget = ToFiniteFloat(
                        ((double)target.MinimumValue + target.MaximumValue) * 0.5d);
                    break;
                case VehicleReferenceTargetMode.Maximum:
                    acceptedMinimum = -float.MaxValue;
                    acceptedMaximum = SafeAdd(target.TargetValue, tolerance);
                    comparisonTarget = target.TargetValue;
                    break;
                case VehicleReferenceTargetMode.Minimum:
                    acceptedMinimum = SafeSubtract(target.TargetValue, tolerance);
                    acceptedMaximum = float.MaxValue;
                    comparisonTarget = target.TargetValue;
                    break;
                default:
                    acceptedMinimum = SafeSubtract(target.TargetValue, tolerance);
                    acceptedMaximum = SafeAdd(target.TargetValue, tolerance);
                    comparisonTarget = target.TargetValue;
                    break;
            }
        }

        private static VehicleMetricResult CreateResult(
            string runId,
            string fixtureId,
            string metricId,
            VehicleReferenceClassification classification,
            VehicleMetricResultStatus status,
            VehicleStatisticalSummary summary,
            float comparisonValue,
            float targetValue,
            float acceptedMinimum,
            float acceptedMaximum,
            bool repeatabilityPassed,
            string reason)
        {
            float signedDelta = comparisonValue - targetValue;
            bool hasRelativeDelta = Math.Abs(targetValue) > NearZero;
            float relativeDelta = hasRelativeDelta
                ? ToFiniteFloat((double)signedDelta / Math.Abs(targetValue))
                : 0f;
            return new VehicleMetricResult(
                string.IsNullOrWhiteSpace(runId) ? "invalid.run" : runId,
                string.IsNullOrWhiteSpace(fixtureId) ? "invalid.fixture" : fixtureId,
                string.IsNullOrWhiteSpace(metricId) ? "invalid.metric" : metricId,
                classification,
                status,
                summary,
                VehicleCalibrationValidation.IsFinite(comparisonValue) ? comparisonValue : 0f,
                VehicleCalibrationValidation.IsFinite(targetValue) ? targetValue : 0f,
                VehicleCalibrationValidation.IsFinite(acceptedMinimum) ? acceptedMinimum : 0f,
                VehicleCalibrationValidation.IsFinite(acceptedMaximum) ? acceptedMaximum : 0f,
                ToFiniteNonNegativeFloat(Math.Abs((double)signedDelta)),
                relativeDelta,
                hasRelativeDelta,
                repeatabilityPassed,
                reason ?? string.Empty);
        }

        private static float SafeAdd(float left, float right)
        {
            return ToFiniteFloat((double)left + right);
        }

        private static float SafeSubtract(float left, float right)
        {
            return ToFiniteFloat((double)left - right);
        }

        private static float ToFiniteNonNegativeFloat(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
            {
                return 0f;
            }

            return value >= float.MaxValue ? float.MaxValue : (float)value;
        }

        private static float ToFiniteFloat(double value)
        {
            if (double.IsNaN(value))
            {
                return 0f;
            }

            if (value >= float.MaxValue)
            {
                return float.MaxValue;
            }

            if (value <= -float.MaxValue)
            {
                return -float.MaxValue;
            }

            return (float)value;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index];
                }
            }

            return string.Empty;
        }
    }
}
